using System.Collections.Generic;
using FWTCG.Data;

namespace FWTCG.Core
{
    /// <summary>
    /// DEV-20: Computes which runes must be auto-tapped or auto-recycled
    /// to afford a card's mana cost and schematic (符能) cost.
    ///
    /// **UI-OVERHAUL-1b 后使用范围**：玩家路径已切 prepared-runes 手动机制
    /// （见 GameManager._preparedTapIdxs / ValidateAndCommitPreparedFor）；
    /// 本类仅被 SimpleAI（AI 自动施法）/ Mulligan（开局手牌审阅）/ 反应窗口 3 处调用。
    /// 玩家主动出牌请勿再新增引用，以免绕过 prepared UI 流程。
    ///
    /// Algorithm:
    ///   1. manaDeficit  = max(0, card.Cost  – gs.GetMana(owner))
    ///   2. schDeficit   = max(0, card.RuneCost – gs.GetSch(owner, card.RuneType))
    ///   3. To cover schDeficit:  pick untapped runes of matching RuneType → RecycleIndices
    ///   4. To cover manaDeficit: pick remaining untapped runes (any type) → TapIndices
    ///   5. CanAfford = both deficits fully covered
    /// </summary>
    public static class RuneAutoConsume
    {
        public struct Plan
        {
            /// <summary>True when both mana and schematic costs can be covered.</summary>
            public bool CanAfford;

            /// <summary>Indices into gs.GetRunes(owner) to tap for mana.</summary>
            public List<int> TapIndices;

            /// <summary>Indices into gs.GetRunes(owner) to recycle for sch.</summary>
            public List<int> RecycleIndices;

            public bool NeedsOps => CanAfford && (TapCount > 0 || RecycleCount > 0);
            public int TapCount => TapIndices?.Count ?? 0;
            public int RecycleCount => RecycleIndices?.Count ?? 0;

            /// <summary>Human-readable confirm dialog body for the given card.</summary>
            public string BuildConfirmText(UnitInstance card)
            {
                string cd = card.CardData.CardName;
                var parts = new List<string>();
                parts.Add($"打出：{cd}（费用 {card.CardData.Cost}，符能 {card.CardData.RuneCost} {card.CardData.RuneType.ToChinese()}）");
                if (TapCount > 0)
                    parts.Add($"横置 {TapCount} 个符文获得 {TapCount} 点法力");
                if (RecycleCount > 0)
                    parts.Add($"回收 {RecycleCount} 个 {card.CardData.RuneType.ToChinese()} 符文获得 {RecycleCount} 点符能");
                return string.Join("\n", parts);
            }
        }

        /// <summary>
        /// Computes the auto-consume plan needed to afford <paramref name="card"/> from
        /// the runes currently in play for <paramref name="owner"/>.
        /// Returns a plan with CanAfford=false when costs cannot be met even after consuming
        /// all available runes.
        /// </summary>
        /// <summary>
        /// Single source of truth for whether a rune can be tapped for mana.
        /// Used by both Compute() and GameManager.OnRuneClicked() to ensure rule consistency.
        /// </summary>
        public static bool CanTap(RuneInstance rune) => rune != null && !rune.Tapped;

        /// <summary>
        /// Single source of truth for whether a rune can be recycled for schematic energy.
        /// Tap and recycle are mutually exclusive — a tapped rune cannot also be recycled.
        /// Used by both Compute() and GameManager.OnRuneClicked() to ensure rule consistency.
        /// </summary>
        public static bool CanRecycle(RuneInstance rune) => rune != null && !rune.Tapped;

        public static Plan Compute(UnitInstance card, GameState gs, string owner)
        {
            var plan = new Plan
            {
                TapIndices     = new List<int>(),
                RecycleIndices = new List<int>(),
                CanAfford      = false
            };

            if (card == null || gs == null) return plan;

            int manaCost = card.CardData.Cost;
            int schCost  = card.CardData.RuneCost;
            RuneType schType = card.CardData.RuneType;

            int currentMana = gs.GetMana(owner);
            int currentSch  = gs.GetSch(owner, schType);

            int manaDeficit = System.Math.Max(0, manaCost - currentMana);
            int schDeficit  = System.Math.Max(0, schCost  - currentSch);

            var runes = gs.GetRunes(owner);

            // Pass 1 — cover schDeficit by recycling eligible matching-type runes
            int schCovered = 0;
            for (int i = 0; i < runes.Count && schCovered < schDeficit; i++)
            {
                RuneInstance r = runes[i];
                if (CanRecycle(r) && r.RuneType == schType)
                {
                    plan.RecycleIndices.Add(i);
                    schCovered++;
                }
            }

            // Build a set of already-reserved indices so Pass 2 skips them
            var reserved = new System.Collections.Generic.HashSet<int>(plan.RecycleIndices);

            // Pass 2 — cover manaDeficit by tapping remaining eligible runes (any type)
            int manaCovered = 0;
            for (int i = 0; i < runes.Count && manaCovered < manaDeficit; i++)
            {
                RuneInstance r = runes[i];
                if (CanTap(r) && !reserved.Contains(i))
                {
                    plan.TapIndices.Add(i);
                    manaCovered++;
                }
            }

            bool schAffordable  = schCovered  >= schDeficit;
            bool manaAffordable = manaCovered >= manaDeficit;
            plan.CanAfford = schAffordable && manaAffordable;

            return plan;
        }
    }
}
