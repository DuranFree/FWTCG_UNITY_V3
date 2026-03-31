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
                    // Reactive: first enemy unit -1 power this turn + draw 1
                    {
                        var t = GetFirstEnemyUnit(owner, gs);
                        if (t != null)
                        {
                            t.TempAtkBonus -= 1;
                            Log($"[诡计] {t.UnitName} -1战力（本回合）");
                        }
                        else
                        {
                            Log("[诡计] 无目标单位");
                        }
                        DrawCards(owner, 1, gs);
                    }
                    break;

                case "retreat_rune":
                    // Reactive: recall first friendly BF unit to base + recycle a rune
                    {
                        var u = GetFirstFriendlyBFUnit(owner, gs);
                        if (u != null)
                            RecallUnitToBase(u, owner, gs);
                        else
                            Log("[撤退符文] 战场上无己方单位可召回");

                        var runes = gs.GetRunes(owner);
                        if (runes.Count > 0)
                        {
                            var rune = runes[0];
                            runes.RemoveAt(0);
                            gs.GetRuneDeck(owner).Insert(0, rune);
                            gs.AddSch(owner, rune.RuneType, 1);
                            Log($"[撤退符文] 回收符文 {rune.RuneType}，+1{rune.RuneType}符能");
                        }
                        else
                        {
                            Log("[撤退符文] 无符文可回收");
                        }
                    }
                    break;

                case "guilty_pleasure":
                    // Reactive: discard first non-reactive card from hand + deal 2 damage to first enemy
                    {
                        var hand = gs.GetHand(owner);
                        UnitInstance toDiscard = null;
                        foreach (var c in hand)
                        {
                            // Prefer non-spell cards; fallback to any card
                            if (!c.CardData.IsSpell) { toDiscard = c; break; }
                        }
                        if (toDiscard == null && hand.Count > 0) toDiscard = hand[0];

                        if (toDiscard != null)
                        {
                            hand.Remove(toDiscard);
                            gs.GetDiscard(owner).Add(toDiscard);
                            Log($"[罪恶乐趣] 弃置 {toDiscard.UnitName}");
                        }
                        else
                        {
                            Log("[罪恶乐趣] 手牌已空，无牌可弃");
                        }

                        var t = GetFirstEnemyUnit(owner, gs);
                        if (t != null)
                        {
                            t.CurrentHp -= 2;
                            Log($"[罪恶乐趣] {t.UnitName} 受到2点伤害（剩余HP:{t.CurrentHp}）");
                            GameManager.FireUnitDamaged(t, 2, "罪恶乐趣");
                            if (t.CurrentHp <= 0) RemoveDeadUnit(t, gs);
                        }
                        else
                        {
                            Log("[罪恶乐趣] 无敌方单位可击");
                        }
                    }
                    break;

                case "smoke_bomb":
                    // Reactive: first enemy unit -4 power this turn
                    {
                        var t = GetFirstEnemyUnit(owner, gs);
                        if (t != null)
                        {
                            t.TempAtkBonus -= 4;
                            Log($"[烟雾弹] {t.UnitName} -4战力（本回合）");
                        }
                        else
                        {
                            Log("[烟雾弹] 无敌方单位可削弱");
                        }
                    }
                    break;

                // ── Yi reactive spells ───────────────────────────────────────

                case "scoff":
                    // Reactive: negate triggering spell if cost ≤ 4
                    if (triggerSpell != null && triggerSpell.CardData.Cost <= 4)
                    {
                        negated = true;
                        Log($"[嘲讽] 无效化 {triggerSpell.UnitName}（费用{triggerSpell.CardData.Cost}≤4）");
                    }
                    else
                    {
                        Log($"[嘲讽] 目标法术费用>4（{triggerSpell?.CardData.Cost ?? 0}），无效");
                    }
                    break;

                case "duel_stance":
                    // Reactive: first friendly unit gains +1/+1 permanently
                    {
                        var t = GetFirstFriendlyUnit(owner, gs);
                        if (t != null)
                        {
                            t.BuffTokens += 1;
                            t.CurrentAtk += 1;
                            t.CurrentHp += 1;
                            Log($"[决斗姿态] {t.UnitName} 获得+1/+1增益");
                        }
                        else
                        {
                            Log("[决斗姿态] 无己方单位");
                        }
                    }
                    break;

                case "well_trained":
                    // Reactive: first friendly unit +2 power this turn + draw 1
                    {
                        var t = GetFirstFriendlyUnit(owner, gs);
                        if (t != null)
                        {
                            t.TempAtkBonus += 2;
                            Log($"[精英训练] {t.UnitName} +2战力（本回合）");
                        }
                        else
                        {
                            Log("[精英训练] 无己方单位");
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

            var base_ = gs.GetBase(enemy);
            if (base_.Count > 0) return base_[0];

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                var bfList = enemy == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;
                if (bfList.Count > 0) return bfList[0];
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

        // ── Unit manipulation helpers ──────────────────────────────────────────

        private void RecallUnitToBase(UnitInstance unit, string owner, GameState gs)
        {
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                var bfList = owner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;

                if (bfList.Remove(unit))
                {
                    gs.GetBase(owner).Add(unit);
                    unit.Exhausted = true;
                    Log($"[撤退符文] {unit.UnitName} 从战场{i + 1}召回基地（休眠）");
                    return;
                }
            }
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
