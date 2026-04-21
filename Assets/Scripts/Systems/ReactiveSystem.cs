using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG;

namespace FWTCG.Systems
{
    /// <summary>
    /// Handles reactive spell effects for DEV-4.
    /// All reactive spells use auto-targeting (no secondary target selection UI).
    ///
    /// ApplyReactive returns true if the triggering spell should be NEGATED.
    /// The reactive card is automatically moved from hand to discard.
    /// </summary>
    public class ReactiveSystem : MonoBehaviour
    {
        public static event System.Action<string> OnReactiveLog;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Applies a reactive spell's effect.
        /// Returns true if the triggering spell should be cancelled/negated.
        /// Automatically moves the reactive card from hand to discard.
        /// </summary>
        /// <param name="reactive">The reactive card being played</param>
        /// <param name="owner">Who is playing the reactive card ("player" or "enemy")</param>
        /// <param name="triggerSpell">The spell that triggered this reaction (may be null)</param>
        /// <param name="gs">Current game state</param>
        public bool ApplyReactive(UnitInstance reactive, string owner,
                                  UnitInstance triggerSpell, GameState gs)
        {
            Log($"[反应] {DisplayName(owner)} 发动 {reactive.UnitName}！");

            // Move reactive card from hand to discard first
            gs.GetHand(owner).Remove(reactive);
            gs.GetDiscard(owner).Add(reactive);

            bool negated = false;
            string effectId = reactive.CardData.EffectId;

            switch (effectId)
            {
                // ── Kaisa reactive spells ────────────────────────────────────

                case "swindle":
                    // 愚诈 — "让一名单位在本回合内-1，不得低于1。抽一张牌。"
                    {
                        var t = GetFirstEnemyUnit(owner, gs);
                        if (t != null)
                        {
                            ApplyThisTurnAtkDebuffMin1(t, 1);
                            Log($"[愚诈] {t.UnitName} 本回合-1战力（不低于1）");
                        }
                        else
                        {
                            Log("[愚诈] 无目标单位");
                        }
                        DrawCards(owner, 1, gs);
                    }
                    break;

                case "retreat_rune":
                    // 择日再战 — "让一名友方单位返回其所属者的手牌，然后其所属者召出一枚休眠的符文。"
                    {
                        var u = GetFirstFriendlyBFUnit(owner, gs);
                        if (u != null)
                        {
                            ReturnBFUnitToHand(u, owner, gs);
                            SummonDormantRune(owner, gs);
                        }
                        else
                        {
                            var baseU = GetFirstFriendlyBaseUnit(owner, gs);
                            if (baseU != null)
                            {
                                ReturnBaseUnitToHand(baseU, owner, gs);
                                SummonDormantRune(owner, gs);
                            }
                            else
                            {
                                Log("[择日再战] 无友方单位可返回");
                            }
                        }
                    }
                    break;

                // 罪恶快感已改为迅捷法术（见 SpellSystem.GuiltyPleasure）；
                // 不再在 ReactiveSystem 处理。

                case "smoke_bomb":
                    // 烟幕弹 — "让一名单位在本回合内-4，不得低于1。"
                    {
                        var t = GetFirstEnemyUnit(owner, gs);
                        if (t != null)
                        {
                            ApplyThisTurnAtkDebuffMin1(t, 4);
                            Log($"[烟幕弹] {t.UnitName} 本回合-4战力（不低于1）");
                        }
                        else
                        {
                            Log("[烟幕弹] 无敌方单位可削弱");
                        }
                    }
                    break;

                // ── Yi reactive spells ───────────────────────────────────────

                case "scoff":
                    // 藐视 — "无效化一个法术，但其费用不得高于[4]，符能费用不得高于[3]。"
                    if (triggerSpell != null &&
                        triggerSpell.CardData.Cost <= 4 &&
                        triggerSpell.CardData.RuneCost <= 3)
                    {
                        negated = true;
                        Log($"[藐视] 无效化 {triggerSpell.UnitName}（费用{triggerSpell.CardData.Cost}≤4，符能费用{triggerSpell.CardData.RuneCost}≤3）");
                    }
                    else
                    {
                        int c = triggerSpell?.CardData.Cost ?? 0;
                        int rc = triggerSpell?.CardData.RuneCost ?? 0;
                        Log($"[藐视] 目标法术超出限制（费用{c}，符能费用{rc}），无效化失败");
                    }
                    break;

                case "duel_stance":
                    // 冰斗架势 — "让一名友方单位在本回合内+1。如果它是你在这一回合打出的，则它本回合内额外获得+1。"
                    {
                        var t = GetFirstFriendlyUnit(owner, gs);
                        if (t != null)
                        {
                            t.TempAtkBonus += 1;
                            // B10: 如果目标是本回合打出的单位，额外 +1
                            if (t.PlayedThisTurn)
                            {
                                t.TempAtkBonus += 1;
                                Log($"[冰斗架势] {t.UnitName} 本回合+1战力（本回合打出，额外+1 共+2）");
                            }
                            else
                            {
                                Log($"[冰斗架势] {t.UnitName} 本回合+1战力");
                            }
                        }
                        else
                        {
                            Log("[冰斗架势] 无己方单位");
                        }
                    }
                    break;

                case "well_trained":
                    // 训练有素 — "让一名单位在本回合内+2，然后抽一张牌。"
                    {
                        var t = GetFirstFriendlyUnit(owner, gs);
                        if (t != null)
                        {
                            t.TempAtkBonus += 2;
                            Log($"[训练有素] {t.UnitName} 本回合+2战力");
                        }
                        else
                        {
                            Log("[训练有素] 无己方单位");
                        }
                        DrawCards(owner, 1, gs);
                    }
                    break;

                case "wind_wall":
                    // Reactive: negate any spell
                    negated = true;
                    Log($"[风墙] 无效化 {triggerSpell?.UnitName ?? "目标法术"}！");
                    break;

                case "flash_counter":
                    // Reactive: counter enemy spell (negate)
                    {
                        string enemy = gs.Opponent(owner);
                        if (triggerSpell != null && triggerSpell.Owner == enemy)
                        {
                            negated = true;
                            Log($"[闪电反制] 反制 {triggerSpell.UnitName}！");
                        }
                        else
                        {
                            Log("[闪电反制] 目标不是敌方法术，无效");
                        }
                    }
                    break;

                default:
                    Log($"[反应] 未实现效果: {effectId}（{reactive.UnitName}）");
                    break;
            }

            Log($"[反应] {reactive.UnitName} 结算完毕{(negated ? "，原法术已被无效化" : "")}");
            return negated;
        }

        // ── Auto-targeting helpers ────────────────────────────────────────────

        private UnitInstance GetFirstEnemyUnit(string owner, GameState gs)
        {
            string enemy = gs.Opponent(owner);

            // C-8: skip units with UntargetableBySpells
            foreach (var u in gs.GetBase(enemy))
                if (!u.UntargetableBySpells) return u;

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                var bfList = enemy == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;
                foreach (var u in bfList)
                    if (!u.UntargetableBySpells) return u;
            }
            return null;
        }

        private UnitInstance GetFirstFriendlyUnit(string owner, GameState gs)
        {
            var base_ = gs.GetBase(owner);
            if (base_.Count > 0) return base_[0];

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                var bfList = owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;
                if (bfList.Count > 0) return bfList[0];
            }
            return null;
        }

        private UnitInstance GetFirstFriendlyBFUnit(string owner, GameState gs)
        {
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                var bfList = owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;
                if (bfList.Count > 0) return bfList[0];
            }
            return null;
        }

        private UnitInstance GetFirstFriendlyBaseUnit(string owner, GameState gs)
        {
            var baseL = gs.GetBase(owner);
            return baseL.Count > 0 ? baseL[0] : null;
        }

        // ── Unit manipulation helpers ──────────────────────────────────────────

        /// <summary>Apply a TempAtkBonus reduction that won't drop EffectiveAtk below 1 (card text "不得低于1").</summary>
        private void ApplyThisTurnAtkDebuffMin1(UnitInstance unit, int amount)
        {
            int currentEff = Mathf.Max(0, unit.CurrentAtk + unit.TempAtkBonus);
            int actualReduction = Mathf.Min(amount, Mathf.Max(0, currentEff - 1));
            unit.TempAtkBonus -= actualReduction;
        }

        /// <summary>Return a BF unit back to its owner's hand.</summary>
        private void ReturnBFUnitToHand(UnitInstance unit, string owner, GameState gs)
        {
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                var bfList = owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;

                if (bfList.Remove(unit))
                {
                    gs.GetHand(owner).Add(unit);
                    // Reset unit state when returning to hand
                    unit.CurrentHp = unit.CurrentAtk;
                    unit.Exhausted = false;
                    unit.Stunned = false;
                    unit.TempAtkBonus = 0;
                    Log($"[择日再战] {unit.UnitName} 从战场{i + 1}返回手牌");
                    return;
                }
            }
        }

        /// <summary>Return a base unit back to its owner's hand.</summary>
        private void ReturnBaseUnitToHand(UnitInstance unit, string owner, GameState gs)
        {
            if (gs.GetBase(owner).Remove(unit))
            {
                gs.GetHand(owner).Add(unit);
                unit.CurrentHp = unit.CurrentAtk;
                unit.Exhausted = false;
                unit.Stunned = false;
                unit.TempAtkBonus = 0;
                Log($"[择日再战] {unit.UnitName} 从基地返回手牌");
            }
        }

        /// <summary>Summon a dormant (tapped) rune to the owner's rune zone.</summary>
        private void SummonDormantRune(string owner, GameState gs)
        {
            var runeDeck = gs.GetRuneDeck(owner);
            var runes = gs.GetRunes(owner);
            if (runeDeck.Count == 0)
            {
                Log("[择日再战] 符文牌库已空，无法召出符文");
                return;
            }
            if (runes.Count >= GameRules.MAX_RUNES_IN_PLAY)
            {
                Log("[择日再战] 符文区已满，无法召出符文");
                return;
            }
            var rune = runeDeck[0];
            runeDeck.RemoveAt(0);
            rune.Tapped = true; // dormant = exhausted
            runes.Add(rune);
            Log($"[择日再战] 召出一枚休眠符文 {rune.RuneType.ToChinese()}（共{runes.Count}）");
        }

        private void RemoveDeadUnit(UnitInstance unit, GameState gs)
        {
            string owner = unit.Owner;

            if (gs.GetBase(owner).Remove(unit))
            {
                gs.GetDiscard(owner).Add(unit);
                Log($"[死亡] {unit.UnitName} 从基地阵亡");
                return;
            }

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                var bfList = owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;

                if (bfList.Remove(unit))
                {
                    gs.GetDiscard(owner).Add(unit);
                    Log($"[死亡] {unit.UnitName} 从战场{i + 1}阵亡");
                    return;
                }
            }
        }

        private void DrawCards(string owner, int count, GameState gs)
        {
            var deck = gs.GetDeck(owner);
            var hand = gs.GetHand(owner);
            var discard = gs.GetDiscard(owner);
            int drawn = 0;

            for (int i = 0; i < count; i++)
            {
                if (deck.Count == 0)
                {
                    if (discard.Count == 0) break;
                    deck.AddRange(discard);
                    discard.Clear();
                }
                if (deck.Count == 0) break;
                hand.Add(deck[0]);
                deck.RemoveAt(0);
                drawn++;
            }

            Log($"[反应] {DisplayName(owner)} 摸 {drawn} 张牌（手牌 {hand.Count}）");
        }

        // ── Logging helpers ───────────────────────────────────────────────────

        private string DisplayName(string owner) =>
            owner == GameRules.OWNER_PLAYER ? "玩家" : "AI";

        private void Log(string msg)
        {
            Debug.Log(msg);
            OnReactiveLog?.Invoke(msg);
        }
    }
}
