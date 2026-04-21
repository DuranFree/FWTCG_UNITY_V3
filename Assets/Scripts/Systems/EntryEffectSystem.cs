using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Systems
{
    /// <summary>
    /// Handles unit entry (onSummon) effects when a unit moves from hand to base.
    /// Effects are identified by CardData.EffectId.
    /// </summary>
    public class EntryEffectSystem : MonoBehaviour
    {
        public static event System.Action<string> OnEffectLog;

        /// <summary>
        /// Trigger the entry effect of a unit that just entered the base.
        /// Call this after paying costs and placing the unit in base.
        /// </summary>
        public void OnUnitEntered(UnitInstance unit, string owner, GameState gs)
        {
            string effectId = unit.CardData.EffectId;
            bool hasEffect = !string.IsNullOrEmpty(effectId);
            bool isEquipment = unit.CardData.IsEquipment;

            // 统一通过 SpellShowcaseUI 队列播放：带入场效果的单位、装备牌都触发
            // 纯数值单位（白板，无 effectId 且非装备）不触发，避免骚扰
            if (hasEffect || isEquipment)
                FWTCG.UI.SpellShowcaseUI.Instance?.ShowAsync(unit, owner);

            if (!hasEffect) return;

            switch (effectId)
            {
                case "yordel_instructor_enter":
                    DrawCards(owner, 1, gs);
                    FWTCG.UI.GameEventBus.FireEntryEffectBanner(unit.UnitName, "入场：摸1张牌"); // DEV-18b
                    break;

                case "darius_second_card":
                    // If player has played more than 1 card this turn, +2 atk and un-exhaust
                    if (gs.CardsPlayedThisTurn > 1)
                    {
                        unit.TempAtkBonus += 2;
                        unit.Exhausted = false;
                        Log($"[入场] {unit.UnitName} — 本回合已出牌，获得+2战力并变为活跃");
                        FWTCG.UI.GameEventBus.FireEntryEffectBanner(unit.UnitName, "+2战力·变为活跃"); // DEV-18b
                        FWTCG.UI.GameEventBus.FireUnitAtkBuff(unit, 2); // DEV-18b
                    }
                    break;

                case "thousand_tail_enter":
                    // All enemy units -3 power (min 1)
                    string enemy = gs.Opponent(owner);
                    int debuffed = 0;
                    foreach (UnitInstance u in AllUnitsFor(enemy, gs))
                    {
                        int newAtk = Mathf.Max(1, u.CurrentAtk - 3);
                        int delta = newAtk - u.CurrentAtk;
                        u.CurrentAtk = newAtk;
                        FWTCG.UI.GameEventBus.FireUnitAtkBuff(u, delta); // DEV-18b
                        debuffed++;
                    }
                    Log($"[入场] {unit.UnitName} — 所有敌方单位-3战力（共{debuffed}个）");
                    FWTCG.UI.GameEventBus.FireEntryEffectBanner(unit.UnitName, $"所有敌方单位-3战力（{debuffed}个）"); // DEV-18b
                    break;

                case "foresight_mech_enter":
                    // 先见机甲卡面："你的「机械」属性单位获得【预知】。（当你打出我时，查看主牌堆顶…）"
                    // 架构说明：规则严格要求给所有"机械"类己方单位赋予预知关键词（Rule 29.1），
                    // 然后每个机械单位进场时各自触发一次预知。
                    // 当前简化：仅 foresight_mech 这一张"机械"卡存在，因此触发一次等价正确。
                    // 若未来加入其他机械卡，需引入 Tags 字段 + 关键词赋予机制。
                    // 播放阶段的查看+置底提示由 GameManager.HandleForesightPromptAsync 处理。
                    List<UnitInstance> deck = gs.GetDeck(owner);
                    if (deck.Count > 0)
                        Log($"[预知] {unit.UnitName} — 牌库顶：{deck[0].UnitName}（ATK:{deck[0].CardData.Atk} 费用:{deck[0].CardData.Cost}）");
                    else
                        Log($"[预知] {unit.UnitName} — 牌库为空");
                    break;

                case "jax_enter":
                    // Hand equipment cards gain Reactive keyword (tracked via flag in DEV-3+)
                    Log($"[入场] {unit.UnitName} — 手牌装备获得反应关键词（DEV-3实现）");
                    break;

                case "tiyana_enter":
                    // Passive: opponent can't gain hold score while Tiyana is in play
                    // Handled in ScoreManager — just log the entry
                    Log($"[入场] {unit.UnitName} — 被动启动：对手无法获得据守分");
                    gs.TiyanasInPlay[owner] = true;
                    break;

                case "noxus_recruit_enter":
                    // 军团（Legion）效果："我的费用减少[2]" 在支付费用时处理，
                    // 见 GameManager.ComputeEffectiveCost。此处仅记录入场。
                    Log($"[入场] {unit.UnitName} 进场（军团效果已在费用结算时处理）");
                    break;

                case "rengar_enter":
                    // 雷恩加尔：反应 + 强攻[2]（卡面"当我进攻时，+2"）。
                    // 默认 HasStrongAtk 由 CardKeyword 装载，但默认 StrongAtkValue=1 需要这里覆写为 2。
                    unit.HasReactive = true;
                    unit.StrongAtkValue = 2;
                    Log($"[入场] {unit.UnitName} — 反应·强攻[2]");
                    FWTCG.UI.GameEventBus.FireEntryEffectBanner(unit.UnitName, "反应·强攻[2]"); // DEV-18b
                    break;

                case "kaisa_hero_conquer":
                    // Kai'Sa, Survivor: "当我征服一处战场时，抽一张牌。"
                    // 入场阶段不触发任何效果，征服时由 CombatSystem.CheckUnitConquestTriggers 抽牌。
                    Log($"[入场] {unit.UnitName} 进场");
                    break;

                case "yi_hero_enter":
                    // 易·锋芒毕现: "游走。我以活跃状态进场。"
                    // 直接设为活跃（不走急速流程，因为本卡没有急速 extra-cost 选择）
                    unit.Exhausted = false;
                    Log($"[入场] {unit.UnitName} — 游走·活跃进场");
                    FWTCG.UI.GameEventBus.FireEntryEffectBanner(unit.UnitName, "游走·活跃进场"); // DEV-18b
                    break;

                case "sandshoal_deserter_enter":
                    // SpellShield + can't be targeted by spells
                    unit.HasSpellShield = true;
                    unit.UntargetableBySpells = true;
                    Log($"[入场] {unit.UnitName} — 法盾+法术无法选中");
                    break;

                // ── Equipment entry effects ──
                case "trinity_equip":
                    // +2 ATK to attached unit, hold score bonus handled in BF system
                    Log($"[装备] {unit.UnitName} — +2战力，据守额外+1分");
                    break;

                case "guardian_equip":
                    // +1 ATK, death protection handled in combat system
                    Log($"[装备] {unit.UnitName} — +1战力，阵亡保护");
                    break;

                case "dorans_equip":
                    // +2 ATK
                    Log($"[装备] {unit.UnitName} — +2战力");
                    break;
            }

            // Legion 代替了旧 Inspire：现在作为费用折扣在 GameManager 支付费用前处理，
            // 不再有"下一张盟友进场 +1/+1"的残留触发。
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void DrawCards(string owner, int count, GameState gs)
        {
            List<UnitInstance> deck = gs.GetDeck(owner);
            List<UnitInstance> hand = gs.GetHand(owner);
            List<UnitInstance> discard = gs.GetDiscard(owner);
            int drawn = 0;

            for (int i = 0; i < count; i++)
            {
                if (deck.Count == 0)
                {
                    if (discard.Count == 0) break;
                    // Simple shuffle back (no burnout for entry draws)
                    deck.AddRange(discard);
                    discard.Clear();
                }
                if (deck.Count == 0) break;
                hand.Add(deck[0]);
                deck.RemoveAt(0);
                drawn++;
            }

            Log($"[效果] 摸{drawn}张牌（手牌 {gs.GetHand(owner).Count}）");
        }

        private List<UnitInstance> AllUnitsFor(string owner, GameState gs)
        {
            var result = new List<UnitInstance>(gs.GetBase(owner));
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;
                result.AddRange(bfUnits);
            }
            return result;
        }

        private void Log(string msg)
        {
            Debug.Log(msg);
            OnEffectLog?.Invoke(msg);
        }
    }
}
