using System;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;

namespace FWTCG.Systems
{
    /// <summary>
    /// Handles unit movement, combat resolution, and recall.
    ///
    /// Combat rules (per official Core Rules):
    /// - Movement causing contested BF → auto spell duel (immediate combat)
    /// - Damage distributed individually (sequential, barrier priority in future)
    /// - Both sides survive → attacker recalled to base (exhausted)
    /// - After each combat, ALL units everywhere have marked damage cleared
    /// - Conquest requires control change + BF not already scored this turn
    /// - Moving to empty BF with control change → conquest score
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        public static event Action<string> OnCombatLog;

        // ── Unit movement (#3, #9) ───────────────────────────────────────────

        /// <summary>
        /// Moves a unit to a battlefield. Does NOT trigger combat.
        /// Call CheckAndResolveCombat() after all units have been moved.
        /// </summary>
        public void MoveUnit(UnitInstance unit, string fromLoc, int toBF,
                             string owner, GameState gs)
        {
            if (gs.GameOver) return;

            BattlefieldState bf = gs.BF[toBF];

            // Remove from source
            RemoveFromSource(unit, fromLoc, owner, gs);

            // Add to destination
            if (owner == GameRules.OWNER_PLAYER)
                bf.PlayerUnits.Add(unit);
            else
                bf.EnemyUnits.Add(unit);

            unit.Exhausted = true;

            Log($"[移动] {unit.UnitName}({DisplayName(owner)}) → 战场{toBF + 1}");

            // Claim control if uncontested (no combat yet)
            string opponent = gs.Opponent(owner);
            if (!bf.HasUnits(opponent))
            {
                bf.Ctrl = owner;
            }
        }

        /// <summary>
        /// After batch-moving units, check a specific BF for combat and resolve.
        /// Awards conquest if control changes (empty BF or combat win).
        /// Returns true if combat occurred.
        /// </summary>
        public bool CheckAndResolveCombat(int bfId, string owner, GameState gs, ScoreManager score)
        {
            if (gs.GameOver) return false;

            BattlefieldState bf = gs.BF[bfId];
            string opponent = gs.Opponent(owner);

            if (bf.HasUnits(owner) && bf.HasUnits(opponent))
            {
                // Contested → auto spell duel
                TriggerCombat(bfId, owner, gs, score);
                return true;
            }

            // #9: Uncontested — check conquest (control change to empty BF)
            if (bf.HasUnits(owner) && !bf.HasUnits(opponent))
            {
                string previousCtrl = bf.Ctrl;
                bf.Ctrl = owner;

                bool controlChanged = (previousCtrl != owner);
                if (controlChanged && !gs.BFConqueredThisTurn.Contains(bfId))
                {
                    Log($"[征服] {DisplayName(owner)} 占领空战场{bfId + 1}");
                    score.AddScore(owner, 1, GameRules.SCORE_TYPE_CONQUER, bfId, gs);
                }
            }

            return false;
        }

        /// <summary>
        /// Safety net: resolves any remaining contested battlefields at end of action phase.
        /// With auto-combat on move, this should rarely trigger.
        /// </summary>
        public void ResolveAllBattlefields(string currentPlayer, GameState gs, ScoreManager score)
        {
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                if (gs.GameOver) return;
                BattlefieldState bf = gs.BF[i];
                if (bf.HasUnits(currentPlayer) && bf.HasUnits(gs.Opponent(currentPlayer)))
                {
                    Debug.LogWarning($"[CombatSystem] BF{i + 1} still contested at end of action — resolving");
                    TriggerCombat(i, currentPlayer, gs, score);
                }
            }
        }

        // ── Recall (#13) ─────────────────────────────────────────────────────

        /// <summary>
        /// Recalls a unit from battlefield to base. Unit becomes exhausted.
        /// Does NOT count as a move, does NOT trigger combat.
        /// If opponent now has uncontested presence, they gain control.
        /// </summary>
        public void RecallUnit(UnitInstance unit, int fromBF, string owner, GameState gs)
        {
            if (gs.GameOver) return;

            BattlefieldState bf = gs.BF[fromBF];

            // Remove from battlefield
            if (owner == GameRules.OWNER_PLAYER)
                bf.PlayerUnits.Remove(unit);
            else
                bf.EnemyUnits.Remove(unit);

            // Add to base
            gs.GetBase(owner).Add(unit);
            unit.Exhausted = true;

            Log($"[召回] {unit.UnitName}({DisplayName(owner)}) 从战场{fromBF + 1} → 基地（休眠）");

            // Update control if opponent now uncontested
            string opponent = gs.Opponent(owner);
            if (bf.HasUnits(opponent) && !bf.HasUnits(owner))
            {
                bf.Ctrl = opponent;
            }
            else if (!bf.HasUnits(opponent) && !bf.HasUnits(owner))
            {
                // Both sides empty — control stays with current controller
            }
        }

        // ── Combat resolution (#4, #5, #6, #8, #10) ─────────────────────────

        /// <summary>
        /// Resolves combat on a battlefield using individual damage distribution.
        /// attacker: the owner who initiated combat (moved unit onto BF).
        /// </summary>
        public void TriggerCombat(int bfId, string attacker, GameState gs, ScoreManager score)
        {
            if (gs.GameOver) return;

            BattlefieldState bf = gs.BF[bfId];
            string defender = gs.Opponent(attacker);
            string previousCtrl = bf.Ctrl;

            // Calculate total power per side (#5: stunned units contribute 0)
            int attackerPower = bf.TotalPower(attacker);
            int defenderPower = bf.TotalPower(defender);

            Log($"[法术对决] 战场{bfId + 1}: {DisplayName(attacker)}({attackerPower}) vs {DisplayName(defender)}({defenderPower})");

            // #4: Distribute damage individually
            List<UnitInstance> attackerUnits = GetBFUnits(attacker, bf);
            List<UnitInstance> defenderUnits = GetBFUnits(defender, bf);

            List<UnitInstance> deadDefenders = DistributeDamage(attackerPower, defenderUnits);
            List<UnitInstance> deadAttackers = DistributeDamage(defenderPower, attackerUnits);

            // Remove dead units
            RemoveDeadUnits(deadDefenders, bf, defender, gs);
            RemoveDeadUnits(deadAttackers, bf, attacker, gs);

            // Determine outcome
            bool attackerSurvivors = bf.HasUnits(attacker);
            bool defenderSurvivors = bf.HasUnits(defender);

            if (attackerSurvivors && defenderSurvivors)
            {
                // #6: Both survive → attacker recalled to base (exhausted)
                RecallAllUnitsToBase(attacker, bf, gs);
                Log($"[战斗] 双方存活，{DisplayName(attacker)}方单位召回基地");
                // Control unchanged
            }
            else if (attackerSurvivors && !defenderSurvivors)
            {
                // Attacker wins — conquer
                bf.Ctrl = attacker;
                Log($"[战斗] {DisplayName(attacker)} 获胜，征服战场{bfId + 1}");

                // #8: Conquest requires control change + not already scored
                bool controlChanged = (previousCtrl != attacker);
                if (controlChanged && !gs.BFConqueredThisTurn.Contains(bfId))
                {
                    score.AddScore(attacker, 1, GameRules.SCORE_TYPE_CONQUER, bfId, gs);
                }
            }
            else if (!attackerSurvivors && defenderSurvivors)
            {
                // Defender wins
                bf.Ctrl = defender;
                Log($"[战斗] {DisplayName(defender)} 防守成功，保持战场{bfId + 1}");
            }
            else
            {
                // Both wiped out
                bf.Ctrl = null;
                Log($"[战斗] 同归于尽！战场{bfId + 1} 无人控制");
            }

            // #10: Reset ALL units in ALL locations (Rule 627.5)
            ResetAllUnits(gs);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Distributes damage sequentially among target units.
        /// First unit absorbs damage until dead, excess carries to next unit.
        /// Returns list of units with CurrentHp <= 0 (dead).
        /// </summary>
        private List<UnitInstance> DistributeDamage(int totalDamage, List<UnitInstance> targets)
        {
            List<UnitInstance> dead = new List<UnitInstance>();
            int remaining = totalDamage;

            foreach (UnitInstance u in targets)
            {
                if (remaining <= 0) break;

                int dmg = Mathf.Min(remaining, u.CurrentHp);
                u.CurrentHp -= dmg;
                remaining -= dmg;

                if (u.CurrentHp <= 0)
                {
                    dead.Add(u);
                    // Excess damage carries over
                    // (already subtracted only what was needed)
                }
            }

            return dead;
        }

        private void RemoveDeadUnits(List<UnitInstance> dead, BattlefieldState bf,
                                      string owner, GameState gs)
        {
            foreach (UnitInstance u in dead)
            {
                Log($"[阵亡] {u.UnitName}({DisplayName(owner)})");
                gs.GetDiscard(owner).Add(u);

                if (owner == GameRules.OWNER_PLAYER)
                    bf.PlayerUnits.Remove(u);
                else
                    bf.EnemyUnits.Remove(u);
            }
        }

        /// <summary>
        /// Recalls all of an owner's units from a BF to base (exhausted).
        /// Used when both sides survive combat (#6).
        /// </summary>
        private void RecallAllUnitsToBase(string owner, BattlefieldState bf, GameState gs)
        {
            List<UnitInstance> units = GetBFUnits(owner, bf);
            List<UnitInstance> toRecall = new List<UnitInstance>(units);

            foreach (UnitInstance u in toRecall)
            {
                gs.GetBase(owner).Add(u);
                u.Exhausted = true;
            }

            units.Clear();
        }

        /// <summary>
        /// Resets ALL units in ALL locations for BOTH players (Rule 627.5).
        /// Clears all marked damage after each combat.
        /// </summary>
        private void ResetAllUnits(GameState gs)
        {
            foreach (string who in new[] { GameRules.OWNER_PLAYER, GameRules.OWNER_ENEMY })
            {
                foreach (UnitInstance u in gs.GetBase(who))
                    u.ResetEndOfTurn();

                for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                {
                    List<UnitInstance> bfUnits = GetBFUnits(who, gs.BF[i]);
                    foreach (UnitInstance u in bfUnits)
                        u.ResetEndOfTurn();
                }
            }
        }

        private List<UnitInstance> GetBFUnits(string owner, BattlefieldState bf)
        {
            return owner == GameRules.OWNER_PLAYER ? bf.PlayerUnits : bf.EnemyUnits;
        }

        private void RemoveFromSource(UnitInstance unit, string fromLoc, string owner, GameState gs)
        {
            if (fromLoc == "base")
            {
                gs.GetBase(owner).Remove(unit);
            }
            else if (int.TryParse(fromLoc, out int bfIdx) && bfIdx >= 0 && bfIdx < GameRules.BATTLEFIELD_COUNT)
            {
                BattlefieldState srcBf = gs.BF[bfIdx];
                if (owner == GameRules.OWNER_PLAYER)
                    srcBf.PlayerUnits.Remove(unit);
                else
                    srcBf.EnemyUnits.Remove(unit);
            }
            else
            {
                Debug.LogWarning($"[CombatSystem] Unknown fromLoc: {fromLoc}");
            }
        }

        private void Log(string msg)
        {
            Debug.Log(msg);
            OnCombatLog?.Invoke(msg);
        }

        private string DisplayName(string owner) =>
            owner == GameRules.OWNER_PLAYER ? "玩家" : "AI";
    }
}
