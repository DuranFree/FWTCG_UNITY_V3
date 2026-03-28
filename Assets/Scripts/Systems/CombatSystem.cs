using System;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;

namespace FWTCG.Systems
{
    /// <summary>
    /// Handles all combat resolution.
    ///
    /// Combat rule: Attacker total power vs Defender total power.
    /// Lower-power side is destroyed. Equal power → both sides destroyed.
    /// Victor controls the battlefield and gains a conquest point.
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        public static event Action<string> OnCombatLog;

        // ── Unit movement ──────────────────────────────────────────────────────

        /// <summary>
        /// Moves a unit from a location to a battlefield slot.
        /// fromLoc: "base", "1", or "2" (battlefield index as string).
        /// If the destination battlefield has enemy units, combat is triggered.
        /// </summary>
        public void MoveUnit(UnitInstance unit, string fromLoc, int toBF,
                             string owner, GameState gs, ScoreManager score)
        {
            if (gs.GameOver) return;

            BattlefieldState bf = gs.BF[toBF];

            // Check slot availability
            if (!bf.HasSlot(owner))
            {
                Log($"[移动失败] {owner} 战场{toBF + 1} 槽位已满");
                return;
            }

            // Remove from source
            RemoveFromSource(unit, fromLoc, owner, gs);

            // Add to destination
            if (owner == GameRules.OWNER_PLAYER)
                bf.PlayerUnits.Add(unit);
            else
                bf.EnemyUnits.Add(unit);

            unit.Exhausted = true;

            Log($"[移动] {unit.UnitName}({owner}) → 战场{toBF + 1}");

            // Trigger combat if there are enemy units on this battlefield
            string opponent = gs.Opponent(owner);
            if (bf.HasUnits(opponent))
            {
                TriggerCombat(toBF, owner, gs, score);
            }
            else
            {
                // No enemies — this owner now controls the battlefield
                bf.Ctrl = owner;
            }
        }

        // ── Combat resolution ──────────────────────────────────────────────────

        /// <summary>
        /// Resolves combat on a battlefield when the attacker moves in.
        /// attacker: the owner who just moved a unit onto this battlefield.
        /// </summary>
        public void TriggerCombat(int bfId, string attacker, GameState gs, ScoreManager score)
        {
            if (gs.GameOver) return;

            BattlefieldState bf = gs.BF[bfId];
            string defender = gs.Opponent(attacker);

            int attackerPower = bf.TotalPower(attacker);
            int defenderPower = bf.TotalPower(defender);

            Log($"[战斗] 战场{bfId + 1}: {DisplayName(attacker)}({attackerPower}) vs {DisplayName(defender)}({defenderPower})");

            if (attackerPower > defenderPower)
            {
                // Attacker wins — destroy all defender units
                DestroyAllUnits(defender, bf, gs);
                bf.Ctrl = attacker;
                Log($"[战斗] {DisplayName(attacker)} 获胜，征服战场{bfId + 1}");
                score.AddScore(attacker, 1, GameRules.SCORE_TYPE_CONQUER, bfId, gs);
            }
            else if (defenderPower > attackerPower)
            {
                // Defender wins — destroy all attacker units
                DestroyAllUnits(attacker, bf, gs);
                bf.Ctrl = defender;
                Log($"[战斗] {DisplayName(defender)} 防守成功，保持战场{bfId + 1}");
                // No score for defender holding — hold score is handled in DoStart phase
            }
            else
            {
                // Tie — both sides destroyed
                DestroyAllUnits(attacker, bf, gs);
                DestroyAllUnits(defender, bf, gs);
                bf.Ctrl = null;
                Log($"[战斗] 同归于尽！战场{bfId + 1} 无人控制");
            }

            // Reset HP for all surviving units on this battlefield
            ResetSurvivors(bf);
        }

        // ── Private helpers ────────────────────────────────────────────────────

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

        private void DestroyAllUnits(string owner, BattlefieldState bf, GameState gs)
        {
            List<UnitInstance> units = owner == GameRules.OWNER_PLAYER
                ? new List<UnitInstance>(bf.PlayerUnits)
                : new List<UnitInstance>(bf.EnemyUnits);

            foreach (UnitInstance u in units)
            {
                Log($"[阵亡] {u.UnitName}({owner})");
                gs.GetDiscard(owner).Add(u);
            }

            if (owner == GameRules.OWNER_PLAYER)
                bf.PlayerUnits.Clear();
            else
                bf.EnemyUnits.Clear();
        }

        private void ResetSurvivors(BattlefieldState bf)
        {
            foreach (UnitInstance u in bf.PlayerUnits)
                u.ResetEndOfTurn();
            foreach (UnitInstance u in bf.EnemyUnits)
                u.ResetEndOfTurn();
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
