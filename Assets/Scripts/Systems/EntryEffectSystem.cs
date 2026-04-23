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

        // darius 触发式技能：订阅 OnCardPlayed，每当打出第二张牌时触发在场的 darius
        private GameState _gsRef;
        public void Inject(GameState gs) { _gsRef = gs; }

        private void OnEnable()
        {
            FWTCG.UI.GameEventBus.OnCardPlayed += OnAnyCardPlayed;
            // DEV-32 A6: 订阅 OnUnitEntered，取代 GameManager/SimpleAI 的直接调用
            FWTCG.UI.GameEventBus.OnUnitEntered += OnUnitEnteredViaEvent;
        }

        private void OnDisable()
        {
            FWTCG.UI.GameEventBus.OnCardPlayed -= OnAnyCardPlayed;
            FWTCG.UI.GameEventBus.OnUnitEntered -= OnUnitEnteredViaEvent;
        }

        /// <summary>
        /// DEV-32 A6: 事件总线入口 — 从注入的 gs 引用读取 GameState，转调现有 OnUnitEntered 实现。
        /// 保留 OnUnitEntered(unit, owner, gs) 公开方法以便测试直接调用。
        /// </summary>
        private void OnUnitEnteredViaEvent(UnitInstance unit, string owner)
        {
            if (_gsRef == null) return;
            OnUnitEntered(unit, owner, _gsRef);
        }

        /// <summary>
        /// 贴图："每当你在本回合中打出第二张牌时，让我本回合+2，并让我变为活跃状态。"
        /// 实现：监听每次打出卡，CardsPlayedThisTurn 从 1 变 2 的那一下（即第二张刚落下）触发 darius。
        /// 已经触发过的本回合不再触发（一次性，用 PlayedThisTurn 追踪 darius 本回合是否已触发）。
        /// </summary>
        private void OnAnyCardPlayed(UnitInstance played, string owner)
        {
            if (_gsRef == null) return;
            if (_gsRef.CardsPlayedThisTurn != 2) return; // 恰好"打出第二张"的瞬间

            foreach (var u in AllUnitsFor(owner, _gsRef))
            {
                if (u.CardData == null || u.CardData.EffectId != "darius_second_card") continue;
                if (u._dariusBuffedThisTurn) continue;
                u.TempAtkBonus += 2;
                u.Exhausted = false;
                u._dariusBuffedThisTurn = true;
                Log($"[德莱厄斯] 本回合第二张牌触发 — {u.UnitName} +2战力·变活跃");
                FWTCG.UI.GameEventBus.FireUnitAtkBuff(u, 2);
                FWTCG.UI.EntryEffectVFX.Instance?.Play(u,
                    new List<UnitInstance> { u }, "+2战力·活跃", FWTCG.UI.GameColors.BuffColor);
            }
        }

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
                    // 入场即自检：若自己恰好是本回合第二张牌（或之后打出），立即获得加成。
                    // 其他情况由 OnAnyCardPlayed 监听每次打出时触发。
                    if (gs.CardsPlayedThisTurn >= 2 && !unit._dariusBuffedThisTurn)
                    {
                        unit.TempAtkBonus += 2;
                        unit.Exhausted = false;
                        unit._dariusBuffedThisTurn = true;
                        Log($"[德莱厄斯] 入场即触发（本回合已打出 {gs.CardsPlayedThisTurn} 张）— +2战力·变活跃");
                        FWTCG.UI.GameEventBus.FireUnitAtkBuff(unit, 2);
                        FWTCG.UI.EntryEffectVFX.Instance?.Play(unit,
                            new List<UnitInstance> { unit }, "+2战力·活跃", buffColor);
                    }
                    else
                    {
                        Log($"[入场] {unit.UnitName} 进场（等待第二张牌触发）");
                    }
                    break;

                case "thousand_tail_enter":
                    // 卡面："让所有敌方单位本回合内-3，不得低于1。"
                    // 使用 TempAtkBonus（回合结束自动清零），不修改 CurrentAtk。
                    string enemy = gs.Opponent(owner);
                    var debuffedTargets = new List<UnitInstance>();
                    foreach (UnitInstance u in AllUnitsFor(enemy, gs))
                    {
                        // 计算实际可扣的量（保证 EffectiveAtk >= 1）
                        int currentEff = Mathf.Max(0, u.CurrentAtk + u.TempAtkBonus);
                        int actualReduction = Mathf.Min(3, Mathf.Max(0, currentEff - 1));
                        if (actualReduction > 0)
                        {
                            u.TempAtkBonus -= actualReduction;
                            FWTCG.UI.GameEventBus.FireUnitAtkBuff(u, -actualReduction);
                        }
                        debuffedTargets.Add(u);
                    }
                    Log($"[入场] {unit.UnitName} — 所有敌方单位本回合-3战力（最低1，共{debuffedTargets.Count}个）");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit, debuffedTargets, "本回合-3", debuffColor);
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
                    // 动态被动：owner 有 jax 在场时，手牌中的装备可作反应打出（GameRules.IsJaxInPlay 查询）
                    Log($"[入场] {unit.UnitName} — 持续被动：手牌装备获得反应");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit, null, "装备获得反应", buffColor);
                    break;

                case "tiyana_enter":
                    // 卡面："如果我位于战场上，则对手无法得分。"
                    // 不再依赖静态 flag — ScoreManager 动态查 IsTiyanaOnBattlefield()。
                    // 入场时 tiyana 在基地，不立即激活；移动到战场后才禁对手得分。
                    Log($"[入场] {unit.UnitName} — 等待移至战场激活被动");
                    FWTCG.UI.EntryEffectVFX.Instance?.Play(unit, null, "移至战场封锁对手得分", playerGreen);
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
