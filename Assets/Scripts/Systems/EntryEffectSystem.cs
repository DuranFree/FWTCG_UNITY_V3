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
            if (string.IsNullOrEmpty(effectId)) return;

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
                    // DEV-26: logs for all owners; player also gets an interactive "置底?" prompt
                    //         handled in GameManager.HandleForesightPromptAsync after this call returns.
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
                    // Rule 724.1.c: Inspire triggers only if another card was already played this turn
                    if (gs.CardsPlayedThisTurn > 1)
                    {
                        gs.InspireNextUnit = true;
                        Log($"[入场] {unit.UnitName} — 鼓舞触发：下一个出场的盟友+1战力");
                    }
                    else
                    {
                        Log($"[入场] {unit.UnitName} — 鼓舞未触发（本回合首张牌）");
                    }
                    break;

                case "rengar_enter":
                    // Reactive + StrongAtk + gain 1 Blazing sch
                    unit.HasReactive = true;
                    gs.AddSch(owner, RuneType.Blazing, 1);
                    Log($"[入场] {unit.UnitName} — 反应+强攻+1炽烈符能");
                    FWTCG.UI.GameEventBus.FireEntryEffectBanner(unit.UnitName, "反应·强攻·炽烈符能+1"); // DEV-18b
                    break;

                case "kaisa_hero_conquer":
                    // Conquest trigger + gain 1 Blazing sch
                    gs.AddSch(owner, RuneType.Blazing, 1);
                    Log($"[入场] {unit.UnitName} — 征服触发+1炽烈符能");
                    break;

                case "yi_hero_enter":
                    // Roam + Haste (payment handled by TryPlayUnitAsync/AI) + gain 1 Crushing sch
                    // Rule 717: Do NOT set Exhausted here — Haste payment is done at play time
                    gs.AddSch(owner, RuneType.Crushing, 1);
                    Log($"[入场] {unit.UnitName} — 游走+急速+1摧破符能");
                    FWTCG.UI.GameEventBus.FireEntryEffectBanner(unit.UnitName, "游走·急速·摧破符能+1"); // DEV-18b
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

            // Inspire check: if InspireNextUnit is set and this isn't the inspirer
            if (gs.InspireNextUnit && effectId != "noxus_recruit_enter")
            {
                gs.InspireNextUnit = false;
                unit.BuffTokens += 1;
                unit.CurrentAtk += 1;
                Log($"[鼓舞] {unit.UnitName} 受到鼓舞，+1战力");
                FWTCG.UI.GameEventBus.FireUnitAtkBuff(unit, 1); // DEV-18b
            }
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
