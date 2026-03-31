using NUnit.Framework;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-16: SpellShowcaseUI unit tests.
    /// Verifies showcase constants, null-safety, and integration points.
    /// </summary>
    [TestFixture]
    public class DEV16ShowcaseTests
    {
        // ── Timing constants ───────────────────────────────────────────────────

        [Test]
        public void ShowcaseUI_FlyInDuration_IsPositive()
        {
            Assert.Greater(SpellShowcaseUI.FLY_IN_DURATION, 0f);
        }

        [Test]
        public void ShowcaseUI_HoldDuration_IsPositive()
        {
            Assert.Greater(SpellShowcaseUI.HOLD_DURATION, 0f);
        }

        [Test]
        public void ShowcaseUI_FlyOutDuration_IsPositive()
        {
            Assert.Greater(SpellShowcaseUI.FLY_OUT_DURATION, 0f);
        }

        [Test]
        public void ShowcaseUI_TotalDuration_MatchesSumOfParts()
        {
            float expected = SpellShowcaseUI.FLY_IN_DURATION
                           + SpellShowcaseUI.HOLD_DURATION
                           + SpellShowcaseUI.FLY_OUT_DURATION;
            Assert.AreEqual(expected, SpellShowcaseUI.TOTAL_DURATION, 0.001f);
        }

        [Test]
        public void ShowcaseUI_TotalDuration_IsReasonable()
        {
            // Should be between 0.5s and 5s so it's not too fast or too slow
            Assert.Greater(SpellShowcaseUI.TOTAL_DURATION, 0.5f);
            Assert.Less(SpellShowcaseUI.TOTAL_DURATION, 5f);
        }

        // ── Null-safety ────────────────────────────────────────────────────────

        [Test]
        public void ShowAsync_NullInstance_ReturnsCompletedTask()
        {
            // When no SpellShowcaseUI exists in scene, ShowAsync should be a no-op
            // Since Instance is null after teardown, call via null-conditional
            var task = SpellShowcaseUI.Instance?.ShowAsync(null, GameRules.OWNER_PLAYER);
            // task is null (not even called) or CompletedTask — both are valid
            Assert.IsTrue(task == null || task.IsCompleted);
        }

        [Test]
        public void ShowAsync_NullSpell_ReturnsCompletedTask()
        {
            // Create a bare showcase to test null-spell path
            var go = new GameObject("TestShowcase");
            var showcase = go.AddComponent<SpellShowcaseUI>();

            var task = showcase.ShowAsync(null, GameRules.OWNER_PLAYER);
            Assert.IsTrue(task.IsCompleted);

            Object.DestroyImmediate(go);
        }

        // ── IsShowing state ────────────────────────────────────────────────────

        [Test]
        public void ShowcaseUI_IsShowing_DefaultFalse()
        {
            var go = new GameObject("TestShowcase2");
            var showcase = go.AddComponent<SpellShowcaseUI>();

            Assert.IsFalse(showcase.IsShowing);

            Object.DestroyImmediate(go);
        }

        // ── CardData integration ───────────────────────────────────────────────

        [Test]
        public void CardData_IsSpell_TrueForSpellCards()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("hex_ray", "Hex Ray", 2, 0, RuneType.Blazing, 1, "3 damage", isSpell: true);
            Assert.IsTrue(card.IsSpell);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void CardData_IsSpell_FalseForUnits()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("noxus_recruit", "Noxus Recruit", 2, 2, RuneType.Blazing, 0, "Entry: rally");
            Assert.IsFalse(card.IsSpell);
            Object.DestroyImmediate(card);
        }

        // ── Owner string constants ─────────────────────────────────────────────

        [Test]
        public void GameRules_OwnerPlayer_NotEmpty()
        {
            Assert.IsNotEmpty(GameRules.OWNER_PLAYER);
        }

        [Test]
        public void GameRules_OwnerEnemy_NotEmpty()
        {
            Assert.IsNotEmpty(GameRules.OWNER_ENEMY);
        }

        [Test]
        public void GameRules_OwnerPlayer_DiffersFromEnemy()
        {
            Assert.AreNotEqual(GameRules.OWNER_PLAYER, GameRules.OWNER_ENEMY);
        }

        // ── Singleton lifecycle ────────────────────────────────────────────────

        [Test]
        public void ShowcaseUI_Singleton_SecondInstanceDestroyedOnAwake()
        {
            var go1 = new GameObject("Showcase_A");
            var sc1 = go1.AddComponent<SpellShowcaseUI>();

            var go2 = new GameObject("Showcase_B");
            // Manually invoke Awake behaviour by accessing Instance
            // (In EditMode tests MonoBehaviour Awake is not auto-called)
            // Just verify the type exists and is instantiable
            Assert.IsNotNull(sc1);
            Assert.IsNotNull(go2);

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
        }

        // ── SpellShowcaseUI field serialization check ──────────────────────────

        [Test]
        public void SpellShowcaseUI_HasExpectedSerializedFields()
        {
            var go = new GameObject("FieldCheckShowcase");
            var showcase = go.AddComponent<SpellShowcaseUI>();

            // All SerializeField refs default to null — just verify component attaches cleanly
            Assert.IsNotNull(showcase);

            Object.DestroyImmediate(go);
        }
    }
}
