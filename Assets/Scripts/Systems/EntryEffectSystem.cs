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

            // Hotfix-12: 移除 SpellShowcaseUI 中央大图 — 入场效果改为
            //   caster 闪 + 光环扩散 → 光球飞向 target → 到达时飘屏（EntryEffectVFX）

            if (!hasEffect) return;

            var buffColor   = FWTCG.UI.GameColors.BuffColor;
            var debuffColor = FWTCG.UI.GameColors.DebuffColor;
            var manaColor   = FWTCG.UI.GameColors.ManaColor;
            var schColor    = FWTCG.UI.GameColors.SchColor;
            var playerGreen = FWTCG.UI.GameColors.PlayerGreen;

            switch (effectId)
            {
                case "yordel_instructor_enter":
                    DrawCards(owner, 1, gs);
                    // 无 target（自己摸牌）→ 飘屏在 caster 上方
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit, null, "摸1张牌", manaColor);
                    break;

                case "darius_second_card":
                    if (gs.CardsPlayedThisTurn > 1)
                    {
                        unit.TempAtkBonus += 2;
                        unit.Exhausted = false;
                        Log($"[入场] {unit.UnitName} — 本回合已出牌，获得+2战力并变为活跃");
                        FWTCG.UI.GameEventBus.FireUnitAtkBuff(unit, 2);
                        FWTCG.UI.EntryEffectVFX.Instance?.Play(unit,
                            new List<UnitInstance> { unit }, "+2战力·活跃", buffColor);
                    }
                    break;

                case "thousand_tail_enter":
                    string enemy = gs.Opponent(owner);
                    var debuffedTargets = new List<UnitInstance>();
                    foreach (UnitInstance u in AllUnitsFor(enemy, gs))
                    {
                        int newAtk = Mathf.Max(1, u.CurrentAtk - 3);
                        int delta = newAtk - u.CurrentAtk;
                        u.CurrentAtk = newAtk;
                        FWTCG.UI.GameEventBus.FireUnitAtkBuff(u, delta);
                        debuffedTargets.Add(u);
                    }
                    Log($"[入场] {unit.UnitName} — 所有敌方单位-3战力（共{debuffedTargets.Count}个）");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit, debuffedTargets, "-3战力", debuffColor);
                    break;

                case "foresight_mech_enter":
                    List<UnitInstance> deck = gs.GetDeck(owner);
                    if (deck.Count > 0)
                        Log($"[预知] {unit.UnitName} — 牌库顶：{deck[0].UnitName}（ATK:{deck[0].CardData.Atk} 费用:{deck[0].CardData.Cost}）");
                    else
                        Log($"[预知] {unit.UnitName} — 牌库为空");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit, null, "预知", playerGreen);
                    break;

                case "jax_enter":
                    Log($"[入场] {unit.UnitName} — 手牌装备获得反应关键词");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit, null, "装备获得反应", buffColor);
                    break;

                case "tiyana_enter":
                    Log($"[入场] {unit.UnitName} — 被动启动：对手无法获得据守分");
                    gs.TiyanasInPlay[owner] = true;
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit, null, "对手无据守分", playerGreen);
                    break;

                case "noxus_recruit_enter":
                    Log($"[入场] {unit.UnitName} 进场（军团效果已在费用结算时处理）");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit,
                        new List<UnitInstance> { unit }, "军团·费用-2", buffColor);
                    break;

                case "rengar_enter":
                    unit.HasReactive = true;
                    unit.StrongAtkValue = 2;
                    Log($"[入场] {unit.UnitName} — 反应·强攻[2]");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit,
                        new List<UnitInstance> { unit }, "反应·强攻[2]", buffColor);
                    break;

                case "kaisa_hero_conquer":
                    Log($"[入场] {unit.UnitName} 进场");
                    // 入场仅标记，征服时才摸牌 → 此处播一个被动提示
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit, null, "征服时摸牌", playerGreen);
                    break;

                case "yi_hero_enter":
                    unit.Exhausted = false;
                    Log($"[入场] {unit.UnitName} — 游走·活跃进场");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit,
                        new List<UnitInstance> { unit }, "游走·活跃", buffColor);
                    break;

                case "sandshoal_deserter_enter":
                    unit.HasSpellShield = true;
                    unit.UntargetableBySpells = true;
                    Log($"[入场] {unit.UnitName} — 法盾+法术无法选中");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit,
                        new List<UnitInstance> { unit }, "法盾·免法术", schColor);
                    break;

                // ── Equipment entry effects ──（装备自身即 target：穿戴者是同一单位）
                case "trinity_equip":
                    Log($"[装备] {unit.UnitName} — +2战力，据守额外+1分");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit,
                        new List<UnitInstance> { unit }, "+2战力·据守+1", buffColor);
                    break;

                case "guardian_equip":
                    Log($"[装备] {unit.UnitName} — +1战力，阵亡保护");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit,
                        new List<UnitInstance> { unit }, "+1战力·阵亡保护", buffColor);
                    break;

                case "dorans_equip":
                    Log($"[装备] {unit.UnitName} — +2战力");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit,
                        new List<UnitInstance> { unit }, "+2战力", buffColor);
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
