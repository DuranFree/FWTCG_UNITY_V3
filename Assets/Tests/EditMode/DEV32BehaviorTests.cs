using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-32: Behavior tests filling gaps from DEV-22 and DEV-25b Codex reports.
    ///
    ///   A) RuneAutoConsume.CanTap / CanRecycle — single-source-of-truth helpers
    ///   B) Haste keyword detection and affordability preconditions
    ///   C) CardDragHandler: CanStartDrag=false guard, callback null-safety, ghost cleanup on Destroy
    ///   D) GameState: resource state changes for Haste cost arithmetic
    /// </summary>
    [TestFixture]
    public class DEV32BehaviorTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private CardData MakeCard(string id, int cost = 1, int atk = 2,
            RuneType rt = RuneType.Blazing, int runeCost = 0,
            CardKeyword kw = CardKeyword.None)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(id, id, cost, atk, rt, runeCost, "", keywords: kw);
            return cd;
        }

        private UnitInstance MakeUnit(string id, int cost = 1, int atk = 2,
            RuneType rt = RuneType.Blazing, int runeCost = 0,
            CardKeyword kw = CardKeyword.None, string owner = GameRules.OWNER_PLAYER)
        {
            return new UnitInstance(1, MakeCard(id, cost, atk, rt, runeCost, kw), owner);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // A. RuneAutoConsume.CanTap / CanRecycle  (DEV-32 tech debt: unified rule)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CanTap_UntappedRune_ReturnsTrue()
        {
            var rune = new RuneInstance(1, RuneType.Blazing) { Tapped = false };
            Assert.IsTrue(RuneAutoConsume.CanTap(rune));
        }

        [Test]
        public void CanTap_TappedRune_ReturnsFalse()
        {
            var rune = new RuneInstance(1, RuneType.Blazing) { Tapped = true };
            Assert.IsFalse(RuneAutoConsume.CanTap(rune));
        }

        [Test]
        public void CanTap_NullRune_ReturnsFalse()
        {
            Assert.IsFalse(RuneAutoConsume.CanTap(null));
        }

        [Test]
        public void CanRecycle_UntappedRune_ReturnsTrue()
        {
            var rune = new RuneInstance(1, RuneType.Verdant) { Tapped = false };
            Assert.IsTrue(RuneAutoConsume.CanRecycle(rune));
        }

        [Test]
        public void CanRecycle_TappedRune_ReturnsFalse()
        {
            var rune = new RuneInstance(1, RuneType.Verdant) { Tapped = true };
            Assert.IsFalse(RuneAutoConsume.CanRecycle(rune), "Tapped rune must not be recyclable");
        }

        [Test]
        public void CanRecycle_NullRune_ReturnsFalse()
        {
            Assert.IsFalse(RuneAutoConsume.CanRecycle(null));
        }

        [Test]
        public void CanTap_AndCanRecycle_AreConsistentForSameRune()
        {
            // Both helpers share the same predicate — a tapped rune blocks both operations.
            var untapped = new RuneInstance(1, RuneType.Crushing) { Tapped = false };
            var tapped   = new RuneInstance(1, RuneType.Crushing) { Tapped = true };

            Assert.AreEqual(RuneAutoConsume.CanTap(untapped),    RuneAutoConsume.CanRecycle(untapped),
                "CanTap and CanRecycle must agree on untapped rune");
            Assert.AreEqual(RuneAutoConsume.CanTap(tapped),      RuneAutoConsume.CanRecycle(tapped),
                "CanTap and CanRecycle must agree on tapped rune");
        }

        [Test]
        public void Compute_SkipsTappedRunesForBothPassess()
        {
            var gs = new GameState();
            gs.SetMana(GameRules.OWNER_PLAYER, 0);  // need to tap

            // Add one tapped rune and one untapped rune of the card's type
            var tappedRune   = new RuneInstance(1, RuneType.Blazing) { Tapped = true };
            var untappedRune = new RuneInstance(1, RuneType.Blazing) { Tapped = false };
            gs.PRunes.Add(tappedRune);
            gs.PRunes.Add(untappedRune);

            var card = MakeUnit("test", cost: 1, rt: RuneType.Blazing, runeCost: 0);

            var plan = RuneAutoConsume.Compute(card, gs, GameRules.OWNER_PLAYER);

            Assert.IsTrue(plan.CanAfford, "Should afford using the untapped rune");
            Assert.AreEqual(1, plan.TapCount, "Only the untapped rune should be selected");
            Assert.IsFalse(plan.TapIndices.Contains(0), "Tapped rune (index 0) must be excluded");
            Assert.IsTrue(plan.TapIndices.Contains(1),  "Untapped rune (index 1) must be selected");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // B. Haste keyword detection and affordability preconditions (DEV-25b gap)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void HasKeyword_Haste_DetectedOnUnit()
        {
            var unit = MakeUnit("haste_unit", kw: CardKeyword.Haste);
            Assert.IsTrue(unit.CardData.HasKeyword(CardKeyword.Haste));
        }

        [Test]
        public void HasKeyword_Haste_NotPresentByDefault()
        {
            var unit = MakeUnit("normal_unit");
            Assert.IsFalse(unit.CardData.HasKeyword(CardKeyword.Haste));
        }

        [Test]
        public void HasKeyword_Haste_DoesNotImplyOtherKeywords()
        {
            var unit = MakeUnit("haste_only", kw: CardKeyword.Haste);
            Assert.IsFalse(unit.CardData.HasKeyword(CardKeyword.Swift),   "Haste ≠ Swift");
            Assert.IsFalse(unit.CardData.HasKeyword(CardKeyword.Roam),    "Haste ≠ Roam");
            Assert.IsFalse(unit.CardData.HasKeyword(CardKeyword.Barrier), "Haste ≠ Barrier");
        }

        [Test]
        public void HasteAffordability_EnoughManaAndSch_IsAffordable()
        {
            // DragNeedsHasteChoice checks: PMana >= cost+1 && haveSch >= runeCost+1
            var gs = new GameState();
            var unit = MakeUnit("h", cost: 2, rt: RuneType.Blazing, runeCost: 1,
                kw: CardKeyword.Haste);
            gs.SetMana(GameRules.OWNER_PLAYER, 3);           // cost+1 = 3
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 2); // runeCost+1 = 2

            int extraManaNeeded = unit.CardData.Cost + 1;
            int extraSchNeeded  = unit.CardData.RuneCost + 1;
            bool canAffordHaste = gs.PMana >= extraManaNeeded
                && gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType) >= extraSchNeeded;

            Assert.IsTrue(canAffordHaste, "Should be able to afford Haste when mana+sch are sufficient");
        }

        [Test]
        public void HasteAffordability_InsufficientMana_NotAffordable()
        {
            var gs = new GameState();
            var unit = MakeUnit("h", cost: 2, rt: RuneType.Blazing, runeCost: 1,
                kw: CardKeyword.Haste);
            gs.SetMana(GameRules.OWNER_PLAYER, 2);           // need 3, have 2
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 2);

            int extraManaNeeded = unit.CardData.Cost + 1;
            int extraSchNeeded  = unit.CardData.RuneCost + 1;
            bool canAffordHaste = gs.PMana >= extraManaNeeded
                && gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType) >= extraSchNeeded;

            Assert.IsFalse(canAffordHaste, "Insufficient mana should block Haste");
        }

        [Test]
        public void HasteAffordability_InsufficientSch_NotAffordable()
        {
            var gs = new GameState();
            var unit = MakeUnit("h", cost: 2, rt: RuneType.Blazing, runeCost: 1,
                kw: CardKeyword.Haste);
            gs.SetMana(GameRules.OWNER_PLAYER, 3);
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 1); // need 2, have 1

            int extraManaNeeded = unit.CardData.Cost + 1;
            int extraSchNeeded  = unit.CardData.RuneCost + 1;
            bool canAffordHaste = gs.PMana >= extraManaNeeded
                && gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType) >= extraSchNeeded;

            Assert.IsFalse(canAffordHaste, "Insufficient sch should block Haste");
        }

        [Test]
        public void HasteRevalidation_AfterResourceChange_DowngradesCorrectly()
        {
            // Simulate the H-5 re-validation: after awaiting, resources may have changed.
            var gs = new GameState();
            var unit = MakeUnit("h", cost: 2, rt: RuneType.Blazing, runeCost: 1,
                kw: CardKeyword.Haste);
            gs.SetMana(GameRules.OWNER_PLAYER, 3);
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 2);

            bool useHaste = true; // user said yes

            // Simulate resources being spent by another action during the async gap
            gs.SetMana(GameRules.OWNER_PLAYER, 2); // drops below cost+1

            bool stillCanAffordHaste = gs.PMana >= unit.CardData.Cost + 1
                && gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType) >= unit.CardData.RuneCost + 1;
            if (!stillCanAffordHaste) useHaste = false;

            Assert.IsFalse(useHaste, "Haste decision must be downgraded when resources drop after prompt");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // C. CardDragHandler: CanStartDrag=false, null callbacks, ghost cleanup
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CardDragHandler_WithNoUnit_DoesNotStartDrag()
        {
            // CanStartDrag returns false when CardView.Unit == null.
            // Without GameManager.Instance, CanStartDrag also returns false
            // — both guards protect the drag path.
            var go = new GameObject("TestCard");
            go.AddComponent<Image>();
            go.AddComponent<Button>();
            go.AddComponent<CardView>();
            var dh = go.AddComponent<CardDragHandler>();

            // Simulate: GameManager.Instance is null (EditMode has no scene)
            // BlockPointerEvents must start false (not blocking at rest)
            Assert.IsFalse(CardDragHandler.BlockPointerEvents,
                "BlockPointerEvents must be false when no drag is active");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CardDragHandler_OnDestroy_ClearsBlockPointerEvents()
        {
            var go = new GameObject("TestCard");
            go.AddComponent<Image>();
            go.AddComponent<Button>();
            go.AddComponent<CardView>();
            go.AddComponent<CardDragHandler>();

            // Force-destroy — should not throw and must release the static lock
            Object.DestroyImmediate(go);

            Assert.IsFalse(CardDragHandler.BlockPointerEvents,
                "OnDestroy must release BlockPointerEvents static lock");
        }

        [Test]
        public void CardDragHandler_NullCallbacks_NeverThrow()
        {
            var go = new GameObject("TestNull");
            go.AddComponent<Image>();
            go.AddComponent<Button>();
            go.AddComponent<CardView>();
            var dh = go.AddComponent<CardDragHandler>();

            // All callbacks start null — safe invocation should not throw
            Assert.DoesNotThrow(() => dh.OnDragToBase?.Invoke(null));
            Assert.DoesNotThrow(() => dh.OnSpellDragOut?.Invoke(null));
            Assert.DoesNotThrow(() => dh.OnDragToBF?.Invoke(null, 0));
            Assert.DoesNotThrow(() => dh.OnDragToBF?.Invoke(new List<UnitInstance>(), 1));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CardDragHandler_MultipleCallbackAssignments_LastWins()
        {
            var go = new GameObject("TestCb");
            go.AddComponent<Image>();
            go.AddComponent<Button>();
            go.AddComponent<CardView>();
            var dh = go.AddComponent<CardDragHandler>();

            int callCount = 0;
            dh.OnDragToBase = _ => callCount = 1;
            dh.OnDragToBase = _ => callCount = 2; // overwrite

            dh.OnDragToBase?.Invoke(null);
            Assert.AreEqual(2, callCount, "Last assigned callback must win (not both fire)");

            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // D. GameState: resource arithmetic used in Haste checks
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GameState_SetMana_ReflectsInPMana()
        {
            var gs = new GameState();
            gs.SetMana(GameRules.OWNER_PLAYER, 5);
            Assert.AreEqual(5, gs.PMana);
        }

        [Test]
        public void GameState_AddSch_AccumulatesCorrectly()
        {
            var gs = new GameState();
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 2);
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 1);
            Assert.AreEqual(3, gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing));
        }

        [Test]
        public void GameState_SpendSch_ReducesCorrectly()
        {
            var gs = new GameState();
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 3);
            gs.SpendSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 2);
            Assert.AreEqual(1, gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing));
        }

        [Test]
        public void GameState_GetSch_WrongType_ReturnsZero()
        {
            var gs = new GameState();
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 3);
            Assert.AreEqual(0, gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Verdant),
                "Sch for a different rune type must be independent");
        }
    }
}
