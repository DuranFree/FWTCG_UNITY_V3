using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FWTCG.Core;
using FWTCG;

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

        /// <summary>Fired after combat resolves with structured result data.</summary>
        public static event Action<CombatResult> OnCombatResult;

        /// <summary>DEV-28: Fired just before damage is calculated. Used by CombatAnimator for flight VFX.</summary>
        public static event Action<int, List<UnitInstance>, List<UnitInstance>> OnCombatWillStart;

        public struct CombatResult
        {
            public string BFName;
            public int    BFIndex;        // DEV-26: 0-based index, avoids string parsing in CombatAnimator
            public string AttackerName;
            public string DefenderName;
            public int AttackerPower;
            public int DefenderPower;
            public string Outcome; // "attacker_win", "defender_win", "both_survive", "both_dead"
            public List<string> DeadAttackers;  // unit names killed on attacker side
            public List<string> DeadDefenders;  // unit names killed on defender side
        }

        [SerializeField] private DeathwishSystem _deathwish;
        [SerializeField] private LegendSystem _legendSys;
        [SerializeField] private BattlefieldSystem _bfSys;

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

            // Leave source BF effect (back_alley_bar) — fire before removal
            if (fromLoc != "base" && int.TryParse(fromLoc, out int srcBfIdx))
                _bfSys?.OnUnitLeaveBattlefield(unit, srcBfIdx, owner, gs);

            // Add to destination
            if (owner == GameRules.OWNER_PLAYER)
                bf.PlayerUnits.Add(unit);
            else
                bf.EnemyUnits.Add(unit);

            // B11: rengar — "若本回合开始前我在基地，则我可以以活跃状态进场"
            // 从基地移出到战场时，如果是 rengar 且 WasInBaseAtTurnStart，则不休眠
            bool rengarActiveMove = unit.CardData.EffectId == "rengar_enter"
                                    && unit.WasInBaseAtTurnStart
                                    && fromLoc == "base";
            if (rengarActiveMove)
            {
                unit.Exhausted = false;
                // 触发一次后清零标记，避免下次移动复用（已"进场"了）
                unit.WasInBaseAtTurnStart = false;
                Log($"[雷恩加尔] {unit.UnitName} 本回合开始前在基地 → 以活跃状态进场");
            }
            else
            {
                unit.Exhausted = true;
            }

            Log($"[移动] {unit.UnitName}({DisplayName(owner)}) → 战场{toBF + 1}");

            // Enter BF effect (trifarian_warcamp)
            _bfSys?.OnUnitEnterBattlefield(unit, toBF, owner, gs);

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

            // vile_throat_nest: block recall from this BF
            if (_bfSys != null && !_bfSys.CanRecallFromBattlefield(fromBF, gs))
                return;

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

            // Masteryi passive: lone defender gets +2 temp attack (DEV-5)
            _legendSys?.TryApplyMasteryiPassive(bfId, attacker, gs);

            // #4: Collect units (needed for power calc and damage distribution)
            List<UnitInstance> attackerUnits = GetBFUnits(attacker, bf);
            List<UnitInstance> defenderUnits = GetBFUnits(defender, bf);

            // reckoner_arena: grant StrongAtk/Guard to units with power >= 5
            _bfSys?.OnCombatStart(bfId, attacker, gs);

            // DEV-28: notify CombatAnimator for flight VFX (non-blocking)
            OnCombatWillStart?.Invoke(bfId, attackerUnits, defenderUnits);

            // Calculate total power per side (#5: stunned units contribute 0)
            // StrongAtk/Guard bonuses are included via ComputeCombatPower
            int attackerPower = ComputeCombatPower(attackerUnits, isAttacking: true);
            int defenderPower = ComputeCombatPower(defenderUnits, isAttacking: false);

            string rawBfId = (gs.BFNames != null && gs.BFNames.Length > bfId && gs.BFNames[bfId] != null) ? gs.BFNames[bfId] : null;
            string bfDisplayName = rawBfId != null ? GameRules.GetBattlefieldDisplayName(rawBfId) : $"战场{bfId + 1}";
            Log($"[法术对决] {bfDisplayName}: {DisplayName(attacker)}({attackerPower}) vs {DisplayName(defender)}({defenderPower})");

            List<UnitInstance> deadDefenders = DistributeDamage(attackerPower, defenderUnits);
            List<UnitInstance> deadAttackers = DistributeDamage(defenderPower, attackerUnits);

            // Remove dead units and trigger deathwish
            RemoveDeadUnits(deadDefenders, bf, defender, gs);
            RemoveDeadUnits(deadAttackers, bf, attacker, gs);

            // DEV-27 #11: Multi-round resolution chain.
            // Deathwish effects may deal damage causing further deaths; loop until no new deaths.
            // Max 8 rounds to prevent infinite loops from pathological card interactions.
            const int maxChainDepth = 8;
            var allDead = new List<UnitInstance>(deadDefenders.Count + deadAttackers.Count);
            allDead.AddRange(deadDefenders);
            allDead.AddRange(deadAttackers);

            for (int chainDepth = 0; chainDepth < maxChainDepth && allDead.Count > 0; chainDepth++)
            {
                if (_deathwish == null) break;
                _deathwish.OnUnitsDied(allDead, bfId, gs);

                // Check for new deaths caused by deathwish effects this chain step
                var newDeadDefenders = FindNewlyDead(GetBFUnits(defender, bf), gs);
                var newDeadAttackers = FindNewlyDead(GetBFUnits(attacker, bf), gs);

                if (newDeadDefenders.Count == 0 && newDeadAttackers.Count == 0) break;

                RemoveDeadUnits(newDeadDefenders, bf, defender, gs);
                RemoveDeadUnits(newDeadAttackers, bf, attacker, gs);

                allDead = new List<UnitInstance>(newDeadDefenders.Count + newDeadAttackers.Count);
                allDead.AddRange(newDeadDefenders);
                allDead.AddRange(newDeadAttackers);

                if (gs.GameOver) break;
            }

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

                // Battlefield conquest-triggered effects
                _bfSys?.OnConquest(bfId, attacker, gs);

                // Unit conquest-triggered effects (cards with Conquest keyword)
                CheckUnitConquestTriggers(attacker, gs);

                // Defense failure effect for the losing player
                _bfSys?.OnDefenseFailure(bfId, defender, gs);
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

            // Fire combat result event for UI display
            string outcome = (attackerSurvivors && defenderSurvivors) ? "both_survive"
                : (attackerSurvivors && !defenderSurvivors) ? "attacker_win"
                : (!attackerSurvivors && defenderSurvivors) ? "defender_win"
                : "both_dead";
            OnCombatResult?.Invoke(new CombatResult
            {
                BFName         = bfDisplayName,
                BFIndex        = bfId,
                AttackerName   = DisplayName(attacker),
                DefenderName   = DisplayName(defender),
                AttackerPower  = attackerPower,
                DefenderPower  = defenderPower,
                Outcome        = outcome,
                DeadAttackers  = deadAttackers.Select(u => u.UnitName).ToList(),
                DeadDefenders  = deadDefenders.Select(u => u.UnitName).ToList(),
            });

            // #10: Reset ALL units in ALL locations (Rule 627.5)
            ResetAllUnits(gs);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Computes total combat power for a group of units.
        /// Stunned units contribute 0 (Rule 743).
        /// Rule 139.2: power &lt; 0 treated as 0 (not 1). Uses EffectiveAtk() for floor.
        /// StrongAtk adds +1 when attacking; Guard adds +1 when defending.
        /// </summary>
        private int ComputeCombatPower(List<UnitInstance> units, bool isAttacking)
        {
            int total = 0;
            foreach (UnitInstance u in units)
            {
                if (u.Stunned) continue;
                int power = u.EffectiveAtk();  // already Max(0, CurrentAtk + TempAtkBonus)
                if (isAttacking && u.HasStrongAtk) power += u.StrongAtkValue;
                if (!isAttacking && u.HasGuard)    power += u.GuardValue;
                total += power;
            }
            return total;
        }

        /// <summary>
        /// Distributes damage sequentially among target units.
        /// Rule 727 (Barrier): units with HasBarrier must receive lethal damage before non-Barrier units.
        /// Within each group, order is preserved. Excess damage carries over.
        /// Returns list of units with CurrentHp <= 0 (dead).
        /// </summary>
        private List<UnitInstance> DistributeDamage(int totalDamage, List<UnitInstance> targets)
        {
            List<UnitInstance> dead = new List<UnitInstance>();
            int remaining = totalDamage;

            // Barrier units must take lethal damage first (Rule 727.1.b)
            IEnumerable<UnitInstance> ordered = targets
                .Where(u => u.HasBarrier)
                .Concat(targets.Where(u => !u.HasBarrier));

            foreach (UnitInstance u in ordered)
            {
                if (remaining <= 0) break;

                int dmg = Mathf.Min(remaining, u.CurrentHp);
                u.CurrentHp -= dmg;
                remaining -= dmg;
                if (dmg > 0) GameManager.FireUnitDamaged(u, dmg, "战斗");

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
                // C-6: Guardian Angel — 附着装备型死亡替换
                if (TryGuardianProtect(u, bf, owner, gs))
                {
                    continue;
                }

                // B8-mini: Zhonya — 非附着型死亡替换（在基地只要还有一张 zhonya_equip 就能保护一次）
                if (TryZhonyaProtect(u, bf, owner, gs))
                {
                    continue;
                }

                // Fire BEFORE removal so GameUI can play death animation on the still-visible CardView (DEV-17)
                GameManager.FireUnitDied(u);
                Log($"[阵亡] {u.UnitName}({DisplayName(owner)})");
                gs.GetDiscard(owner).Add(u);

                if (owner == GameRules.OWNER_PLAYER)
                    bf.PlayerUnits.Remove(u);
                else
                    bf.EnemyUnits.Remove(u);

                // Clear Tiyana passive flag if she dies
                if (u.CardData.EffectId == "tiyana_enter" && gs.TiyanasInPlay.ContainsKey(owner))
                    gs.TiyanasInPlay[owner] = false;
            }
        }

        /// <summary>
        /// C-6 守护天使死亡替换：若将死单位附着 guardian_equip 装备，则销毁装备、单位回基地休眠。
        /// 返回 true 表示已触发保护，单位不应被当成阵亡处理。
        /// </summary>
        private bool TryGuardianProtect(UnitInstance unit, BattlefieldState bf, string owner, GameState gs)
        {
            var equip = unit.AttachedEquipment;
            if (equip == null) return false;
            if (equip.CardData.EffectId != "guardian_equip") return false;

            // 销毁装备 → 弃牌堆
            gs.GetDiscard(owner).Add(equip);
            // 从战场移除单位
            if (owner == GameRules.OWNER_PLAYER) bf.PlayerUnits.Remove(unit);
            else                                 bf.EnemyUnits.Remove(unit);
            // 卸下装备加成，恢复基础状态
            int bonus = equip.CardData.EquipAtkBonus;
            if (bonus > 0)
            {
                unit.CurrentAtk = Mathf.Max(0, unit.CurrentAtk - bonus);
            }
            unit.AttachedEquipment = null;
            equip.AttachedTo = null;
            // 回基地休眠
            unit.CurrentHp = unit.CurrentAtk; // 治愈
            unit.Exhausted = true;
            unit.TempAtkBonus = 0;
            unit.Stunned = false;
            gs.GetBase(owner).Add(unit);

            Log($"[守护天使] {equip.UnitName} 被摧毁，保护 {unit.UnitName} 休眠返回基地");
            return true;
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

        /// <summary>
        /// After a conquest, check if any of the conqueror's units have the Conquest keyword.
        /// bad_poro: draw 1 card on conquest.
        /// kaisa_hero: already handled by entry effect (adds sch).
        /// </summary>
        private void CheckUnitConquestTriggers(string attacker, GameState gs)
        {
            // Check all attacker's units (base + all BFs)
            var allUnits = new List<UnitInstance>(gs.GetBase(attacker));
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                allUnits.AddRange(attacker == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits : gs.BF[i].EnemyUnits);
            }

            foreach (var unit in allUnits)
            {
                if (!unit.CardData.HasKeyword(Data.CardKeyword.Conquest)) continue;

                switch (unit.CardData.EffectId)
                {
                    case "bad_poro_conquer":
                        // bad_poro 卡面："当我征服一处战场时，打出1枚休眠的「硬币」装备指示物。"
                        // 硬币装备指示物尚未建模，简化为摸一张牌作为占位。
                        FWTCG.UI.SpellShowcaseUI.Instance?.ShowAsync(unit, attacker);
                        DrawOneCardForConquest(attacker, gs, unit);
                        break;

                    case "kaisa_hero_conquer":
                        // 卡莎·九死一生："当我征服一处战场时，抽一张牌。"
                        FWTCG.UI.SpellShowcaseUI.Instance?.ShowAsync(unit, attacker);
                        DrawOneCardForConquest(attacker, gs, unit);
                        break;
                }
            }
        }

        /// <summary>
        /// B8-mini: 中娅沙漏 — 非附着型死亡替换。
        /// 如果 unit 所属者的基地里有一张 zhonya_equip 装备，销毁该装备，把 unit 休眠回基地。
        /// 多张同时在基地时只消耗一张。
        /// </summary>
        private bool TryZhonyaProtect(UnitInstance unit, BattlefieldState bf, string owner, GameState gs)
        {
            var baseList = gs.GetBase(owner);
            UnitInstance zhonya = null;
            for (int i = 0; i < baseList.Count; i++)
            {
                var c = baseList[i];
                if (c.CardData.IsEquipment && c.CardData.EffectId == "zhonya_equip" && c.AttachedTo == null)
                {
                    zhonya = c;
                    break;
                }
            }
            if (zhonya == null) return false;

            // 销毁 zhonya → 弃牌堆
            baseList.Remove(zhonya);
            gs.GetDiscard(owner).Add(zhonya);

            // 从战场移除单位
            if (owner == GameRules.OWNER_PLAYER) bf.PlayerUnits.Remove(unit);
            else                                 bf.EnemyUnits.Remove(unit);

            // 单位恢复状态 + 休眠回基地
            unit.CurrentHp = unit.CurrentAtk;
            unit.Exhausted = true;
            unit.TempAtkBonus = 0;
            unit.Stunned = false;
            baseList.Add(unit);

            Log($"[中娅沙漏] 销毁自身，保护 {unit.UnitName} 休眠返回基地");
            return true;
        }

        /// <summary>Shared: draw 1 card as a conquest-triggered effect.</summary>
        private void DrawOneCardForConquest(string owner, GameState gs, UnitInstance source)
        {
            var deck = gs.GetDeck(owner);
            var hand = gs.GetHand(owner);
            if (deck.Count > 0)
            {
                hand.Add(deck[0]);
                deck.RemoveAt(0);
                Log($"[征服触发] {source.UnitName}：抽1张牌（手牌 {hand.Count}）");
            }
        }

        /// <summary>
        /// DEV-27 #11: Scans a set of units and returns those with CurrentHp &lt;= 0
        /// that are still present on the battlefield (not yet removed).
        /// Used to detect new casualties caused by Deathwish chain effects.
        /// </summary>
        private List<UnitInstance> FindNewlyDead(IEnumerable<UnitInstance> candidates, GameState gs)
        {
            var result = new List<UnitInstance>();
            foreach (var u in candidates)
            {
                if (u.CurrentHp <= 0)
                    result.Add(u);
            }
            return result;
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
