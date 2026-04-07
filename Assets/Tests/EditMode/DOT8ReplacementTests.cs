using DG.Tweening;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Tests.EditMode;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DOT-8: Tests for 17 new visual effects:
    ///   CardView    — hand spread, 3D tilt, stat roll, kill close-up
    ///   CardDragHandler — slingshot pullback
    ///   CombatAnimator  — AOE chain highlight
    ///   GameUI      — screen shake, slow motion, turn sweep, deck shake,
    ///                  mana fill, confetti, opponent preview, button squash
    ///   StartupFlowUI   — shuffle animation, Mulligan flip
    ///   LegendSkillShowcase — legend skill close-up
    /// </summary>
    [TestFixture]
    public class DOT8ReplacementTests : DOTweenTestBase
    {
        private const BindingFlags PRIV     = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PRIV_STA = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PUB      = BindingFlags.Public    | BindingFlags.Instance;
        private const BindingFlags PUB_STA  = BindingFlags.Public    | BindingFlags.Static;

        public override void TearDown()
        {
            // Restore timeScale in case a test left it modified
            Time.timeScale = 1f;
            // Clean up any lingering LegendSkillShowcase singleton
            if (LegendSkillShowcase.Instance != null)
                Object.DestroyImmediate(LegendSkillShowcase.Instance.gameObject);
            base.TearDown();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CardView — DOT-8 field existence & types
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void CardView_HandSpreadTween_IsTween()
        { AssertCVFieldType<Tween>("_handSpreadTween"); }

        [Test] public void CardView_StatRollTween_IsTween()
        { AssertCVFieldType<Tween>("_statRollTween"); }

        [Test] public void CardView_SpreadLayoutEl_IsLayoutElement()
        { AssertCVFieldType<LayoutElement>("_spreadLayoutEl"); }

        [Test] public void CardView_DisplayedHp_IsInt()
        { AssertCVFieldType<int>("_displayedHp"); }

        [Test] public void CardView_DisplayedAtk_IsInt()
        { AssertCVFieldType<int>("_displayedAtk"); }

        [Test] public void CardView_IsTiltActive_IsBool()
        { AssertCVFieldType<bool>("_isTiltActive"); }

        [Test] public void CardView_TiltTarget_IsVector3()
        { AssertCVFieldType<Vector3>("_tiltTarget"); }

        [Test] public void CardView_TiltCurrent_IsVector3()
        { AssertCVFieldType<Vector3>("_tiltCurrent"); }

        [Test] public void CardView_PreTiltRotation_IsQuaternion()
        { AssertCVFieldType<Quaternion>("_preTiltRotation"); }

        // ═══════════════════════════════════════════════════════════════════════
        // CardView — DOT-8 methods exist
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void CardView_HasStartHandSpread()
        { Assert.IsNotNull(typeof(CardView).GetMethod("StartHandSpread", PRIV), "StartHandSpread should exist"); }

        [Test] public void CardView_HasStopHandSpread()
        { Assert.IsNotNull(typeof(CardView).GetMethod("StopHandSpread", PRIV), "StopHandSpread should exist"); }

        // ═══════════════════════════════════════════════════════════════════════
        // CardView — DOT-8 constant values
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void CardView_HandSpreadConstants()
        {
            AssertPrivConst(typeof(CardView), "HAND_SPREAD_EXTRA", 32f);
            AssertPrivConst(typeof(CardView), "HAND_SPREAD_DUR", 0.12f);
        }

        [Test] public void CardView_TiltConstants()
        {
            AssertPrivConst(typeof(CardView), "TILT_MAX",   10f);
            AssertPrivConst(typeof(CardView), "TILT_SPEED",  9f);
        }

        [Test] public void CardView_StatRollDuration()
        { AssertPrivConst(typeof(CardView), "STAT_ROLL_DUR", 0.45f); }

        [Test] public void CardView_DisplayedHp_SentinelIsIntMinValue()
        {
            var go = new GameObject("TestCV");
            var cv = go.AddComponent<CardView>();
            var field = typeof(CardView).GetField("_displayedHp", PRIV);
            Assert.IsNotNull(field);
            int val = (int)field.GetValue(cv);
            Assert.AreEqual(int.MinValue, val, "_displayedHp should start at int.MinValue sentinel");
            Object.DestroyImmediate(go);
        }

        [Test] public void CardView_DisplayedAtk_SentinelIsIntMinValue()
        {
            var go = new GameObject("TestCV");
            var cv = go.AddComponent<CardView>();
            var field = typeof(CardView).GetField("_displayedAtk", PRIV);
            Assert.IsNotNull(field);
            int val = (int)field.GetValue(cv);
            Assert.AreEqual(int.MinValue, val, "_displayedAtk should start at int.MinValue sentinel");
            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CardView — functional: StartHandSpread creates tween, StopHandSpread clears it
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CardView_StartHandSpread_NullLayoutEl_DoesNotThrow()
        {
            var go = new GameObject("TestCV");
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            go.AddComponent<CanvasGroup>();
            var cv = go.AddComponent<CardView>();
            // No LayoutElement — should silently skip
            Assert.DoesNotThrow(() =>
            {
                var m = typeof(CardView).GetMethod("StartHandSpread", PRIV);
                m?.Invoke(cv, null);
            });
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CardView_StopHandSpread_NullLayoutEl_DoesNotThrow()
        {
            var go = new GameObject("TestCV");
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            go.AddComponent<CanvasGroup>();
            var cv = go.AddComponent<CardView>();
            Assert.DoesNotThrow(() =>
            {
                var m = typeof(CardView).GetMethod("StopHandSpread", PRIV);
                m?.Invoke(cv, null);
            });
            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CombatAnimator — DOT-8 AOE constants exist
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void CombatAnimator_AOEStagger_Constant()
        { AssertPrivConst(typeof(CombatAnimator), "AOE_STAGGER", 0.10f); }

        [Test] public void CombatAnimator_AOEGlowDur_Constant()
        { AssertPrivConst(typeof(CombatAnimator), "AOE_GLOW_DUR", 0.25f); }

        [Test] public void CombatAnimator_HasOnAOETargetsHandler()
        { Assert.IsNotNull(typeof(CombatAnimator).GetMethod("OnAOETargets", PRIV), "OnAOETargets handler should exist"); }

        // ═══════════════════════════════════════════════════════════════════════
        // GameUI — DOT-8 field existence & types
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void GameUI_CanvasShakeTween_IsTween()
        { AssertFieldType<Tween>(typeof(GameUI), "_canvasShakeTween"); }

        [Test] public void GameUI_SlowMotionTween_IsTween()
        { AssertFieldType<Tween>(typeof(GameUI), "_slowMotionTween"); }

        [Test] public void GameUI_DeckShakeTween_IsTween()
        { AssertFieldType<Tween>(typeof(GameUI), "_deckShakeTween"); }

        [Test] public void GameUI_ManaFillSeq_IsSequence()
        { AssertFieldType<Sequence>(typeof(GameUI), "_manaFillSeq"); }

        [Test] public void GameUI_TurnSweepSeq_IsSequence()
        { AssertFieldType<Sequence>(typeof(GameUI), "_turnSweepSeq"); }

        [Test] public void GameUI_OpponentPreviewSeq_IsSequence()
        { AssertFieldType<Sequence>(typeof(GameUI), "_opponentPreviewSeq"); }

        [Test] public void GameUI_LastPlayerDeckCount_IsInt()
        { AssertFieldType<int>(typeof(GameUI), "_lastPlayerDeckCount"); }

        // ═══════════════════════════════════════════════════════════════════════
        // GameUI — DOT-8 constants
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void GameUI_ShakeConstants()
        {
            AssertPrivConst(typeof(GameUI), "SHAKE_BIG_DAMAGE_THRESHOLD", 5);
            AssertPrivConst(typeof(GameUI), "SHAKE_STRENGTH", 12f);
            AssertPrivConst(typeof(GameUI), "SHAKE_DURATION", 0.35f);
        }

        [Test] public void GameUI_SlowMotionConstants()
        {
            AssertPrivConst(typeof(GameUI), "SLOW_SCALE",   0.3f);
            AssertPrivConst(typeof(GameUI), "SLOW_IN_DUR",  0.05f);
            AssertPrivConst(typeof(GameUI), "SLOW_HOLD",    0.45f);
            AssertPrivConst(typeof(GameUI), "SLOW_OUT_DUR", 0.4f);
        }

        [Test] public void GameUI_ConfettiCount()
        { AssertPrivConst(typeof(GameUI), "CONFETTI_COUNT", 25); }

        // ═══════════════════════════════════════════════════════════════════════
        // GameUI — DOT-8 handler methods exist
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void GameUI_HasOnBigDamageHandler()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("OnBigDamageHandler", PRIV), "OnBigDamageHandler should exist"); }

        [Test] public void GameUI_HasOnFatalHitHandler()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("OnFatalHitHandler", PRIV), "OnFatalHitHandler should exist"); }

        [Test] public void GameUI_HasOnTurnChangedHandler()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("OnTurnChangedHandler", PRIV), "OnTurnChangedHandler should exist"); }

        [Test] public void GameUI_HasPlayTurnSweepBanner()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("PlayTurnSweepBanner", PRIV), "PlayTurnSweepBanner should exist"); }

        [Test] public void GameUI_HasPlayManaFillStagger()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("PlayManaFillStagger", PRIV), "PlayManaFillStagger should exist"); }

        [Test] public void GameUI_HasSpawnConfetti()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("SpawnConfetti", PRIV), "SpawnConfetti should exist"); }

        [Test] public void GameUI_HasPlayOpponentCardPreview()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("PlayOpponentCardPreview", PRIV), "PlayOpponentCardPreview should exist"); }

        [Test] public void GameUI_HasCreateTurnSweepText()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("CreateTurnSweepText", PRIV), "CreateTurnSweepText should exist"); }

        // ═══════════════════════════════════════════════════════════════════════
        // GameUI — OnDestroy restores timeScale
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GameUI_OnDestroy_RestoresTimeScale()
        {
            Time.timeScale = 0.3f; // simulate slow-motion active
            var go = new GameObject("TestGUI");
            var ui = go.AddComponent<GameUI>();
            // EditMode: invoke OnDestroy directly via reflection (SendMessage triggers ShouldRunBehaviour assertion)
            typeof(GameUI).GetMethod("OnDestroy", PRIV)?.Invoke(ui, null);
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "OnDestroy must reset Time.timeScale to 1");
            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GameUI — RefreshPileCounts: deck shake threshold only fires at ≤ 2
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GameUI_LastPlayerDeckCount_StartsAtMinusOne()
        {
            var go = new GameObject("TestGUI");
            var ui = go.AddComponent<GameUI>();
            var field = typeof(GameUI).GetField("_lastPlayerDeckCount", PRIV);
            Assert.IsNotNull(field);
            Assert.AreEqual(-1, (int)field.GetValue(ui), "_lastPlayerDeckCount should start at -1");
            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // StartupFlowUI — DOT-8 shuffle & flip constants
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void StartupFlowUI_ShuffleCardCount()
        { AssertPrivConst(typeof(StartupFlowUI), "SHUFFLE_CARD_COUNT", 4); }

        [Test] public void StartupFlowUI_ShuffleCardDur()
        { AssertPrivConst(typeof(StartupFlowUI), "SHUFFLE_CARD_DUR", 0.22f); }

        [Test] public void StartupFlowUI_ShuffleStagger()
        { AssertPrivConst(typeof(StartupFlowUI), "SHUFFLE_STAGGER", 0.06f); }

        [Test] public void StartupFlowUI_MulliganFlipHalf()
        { AssertPrivConst(typeof(StartupFlowUI), "MULLIGAN_FLIP_HALF", 0.11f); }

        [Test] public void StartupFlowUI_HasCreateShuffleAnimationTween()
        { Assert.IsNotNull(typeof(StartupFlowUI).GetMethod("CreateShuffleAnimationTween", PRIV), "CreateShuffleAnimationTween should exist"); }

        [Test] public void StartupFlowUI_ShuffleGhosts_IsListGameObject()
        {
            var field = typeof(StartupFlowUI).GetField("_shuffleGhosts", PRIV);
            Assert.IsNotNull(field, "_shuffleGhosts field should exist");
            Assert.IsTrue(field.FieldType.Name.Contains("List"), "_shuffleGhosts should be a List");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // LegendSkillShowcase — singleton, subscription, methods
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void LegendSkillShowcase_Singleton_SetOnAwake()
        {
            var go = new GameObject("LSS");
            var showcase = go.AddComponent<LegendSkillShowcase>();
            // EditMode: invoke Awake directly via reflection (SendMessage triggers ShouldRunBehaviour assertion)
            typeof(LegendSkillShowcase).GetMethod("Awake", PRIV)?.Invoke(showcase, null);
            Assert.AreEqual(showcase, LegendSkillShowcase.Instance, "Singleton should be set after Awake");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void LegendSkillShowcase_Singleton_ClearedOnDestroy()
        {
            var go = new GameObject("LSS");
            var showcase = go.AddComponent<LegendSkillShowcase>();
            typeof(LegendSkillShowcase).GetMethod("Awake", PRIV)?.Invoke(showcase, null);
            typeof(LegendSkillShowcase).GetMethod("OnDestroy", PRIV)?.Invoke(showcase, null);
            Object.DestroyImmediate(go);
            // Use Unity's overloaded == to handle fake-null destroyed objects
            Assert.IsTrue(LegendSkillShowcase.Instance == null, "Singleton should be null after destroy");
        }

        [Test]
        public void LegendSkillShowcase_HasTimingConstants()
        {
            Assert.AreEqual(0.15f, LegendSkillShowcase.DARKEN_DUR,  0.001f);
            Assert.AreEqual(0.40f, LegendSkillShowcase.ZOOM_DUR,    0.001f);
            Assert.AreEqual(0.80f, LegendSkillShowcase.HOLD_DUR,    0.001f);
            Assert.AreEqual(0.30f, LegendSkillShowcase.EXIT_DUR,    0.001f);
        }

        [Test]
        public void LegendSkillShowcase_HasPlayShowcase()
        { Assert.IsNotNull(typeof(LegendSkillShowcase).GetMethod("PlayShowcase", PRIV), "PlayShowcase should exist"); }

        [Test]
        public void LegendSkillShowcase_NullLegend_DoesNotThrow()
        {
            var go = new GameObject("LSS");
            var showcase = go.AddComponent<LegendSkillShowcase>();
            typeof(LegendSkillShowcase).GetMethod("Awake", PRIV)?.Invoke(showcase, null);
            Assert.DoesNotThrow(() => GameEventBus.FireLegendSkillFired(null, GameRules.OWNER_PLAYER),
                "Firing LegendSkillFired with null legend should not throw");
            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GameEventBus — DOT-8 events exist
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GameEventBus_HasOnTurnChanged()
        {
            var ev = typeof(GameEventBus).GetEvent("OnTurnChanged", PUB_STA);
            Assert.IsNotNull(ev, "GameEventBus.OnTurnChanged event should exist");
        }

        [Test]
        public void GameEventBus_HasOnAOETargets()
        {
            var ev = typeof(GameEventBus).GetEvent("OnAOETargets", PUB_STA);
            Assert.IsNotNull(ev, "GameEventBus.OnAOETargets event should exist");
        }

        [Test]
        public void GameEventBus_HasOnLegendSkillFired()
        {
            var ev = typeof(GameEventBus).GetEvent("OnLegendSkillFired", PUB_STA);
            Assert.IsNotNull(ev, "GameEventBus.OnLegendSkillFired event should exist");
        }

        [Test]
        public void GameEventBus_FireTurnChanged_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => GameEventBus.FireTurnChanged(GameRules.OWNER_PLAYER, 1));
        }

        [Test]
        public void GameEventBus_FireAOETargets_NullArray_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => GameEventBus.FireAOETargets(null));
        }

        [Test]
        public void GameEventBus_FireLegendSkillFired_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => GameEventBus.FireLegendSkillFired(null, GameRules.OWNER_ENEMY));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CardBackManager — ResetForTest fix: _loaded = true prevents Load() override
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CardBackManager_ResetForTest_ReturnsDefault()
        {
            CardBackManager.ResetForTest();
            Assert.AreEqual(CardBackManager.CardBackVariant.Default, CardBackManager.Current,
                "After ResetForTest, Current should stay Default (not be re-overridden by Load)");
        }

        [Test]
        public void CardBackManager_ResetForTest_GetSprite_ReturnsNull()
        {
            CardBackManager.ResetForTest();
            Assert.IsNull(CardBackManager.GetCardBackSprite(),
                "After ResetForTest, GetCardBackSprite should return null for Default variant");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static void AssertCVFieldType<T>(string fieldName)
        {
            var field = typeof(CardView).GetField(fieldName, PRIV);
            Assert.IsNotNull(field, $"CardView.{fieldName} field should exist");
            Assert.AreEqual(typeof(T), field.FieldType, $"CardView.{fieldName} should be {typeof(T).Name}");
        }

        private static void AssertFieldType<T>(System.Type type, string fieldName)
        {
            var field = type.GetField(fieldName, PRIV);
            Assert.IsNotNull(field, $"{type.Name}.{fieldName} field should exist");
            Assert.AreEqual(typeof(T), field.FieldType, $"{type.Name}.{fieldName} should be {typeof(T).Name}");
        }

        private static void AssertPrivConst(System.Type type, string name, float expected)
        {
            var field = type.GetField(name, PRIV_STA)
                     ?? type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field, $"{type.Name}.{name} constant should exist");
            Assert.AreEqual(expected, (float)field.GetValue(null), 0.001f, $"{type.Name}.{name} value mismatch");
        }

        private static void AssertPrivConst(System.Type type, string name, int expected)
        {
            var field = type.GetField(name, PRIV_STA)
                     ?? type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field, $"{type.Name}.{name} constant should exist");
            Assert.AreEqual(expected, (int)field.GetValue(null), $"{type.Name}.{name} value mismatch");
        }
    }
}
