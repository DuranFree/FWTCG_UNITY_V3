using DG.Tweening;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Tests.EditMode;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DOT-5: Tests for 4 decorative file DOTween replacements.
    /// Verifies coroutine → DOTween migration preserved behavior and cleaned up properly.
    /// </summary>
    [TestFixture]
    public class DOT5ReplacementTests : DOTweenTestBase
    {
        // ═══════════════════════════════════════════════════════════════════════
        // SceneryUI — structural verification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SceneryUI_HasNoSpinLoopCoroutine()
        {
            var method = typeof(SceneryUI).GetMethod("SpinLoop",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.IsNull(method, "SpinLoop coroutine should be removed");
        }

        [Test]
        public void SceneryUI_HasNoDividerOrbLoopCoroutine()
        {
            var method = typeof(SceneryUI).GetMethod("DividerOrbLoop",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "DividerOrbLoop coroutine should be removed");
        }

        [Test]
        public void SceneryUI_HasNoCornerGemLoopCoroutine()
        {
            var method = typeof(SceneryUI).GetMethod("CornerGemLoop",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "CornerGemLoop coroutine should be removed");
        }

        [Test]
        public void SceneryUI_HasNoLegendGlowLoopCoroutine()
        {
            var method = typeof(SceneryUI).GetMethod("LegendGlowLoop",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.IsNull(method, "LegendGlowLoop coroutine should be removed");
        }

        [Test]
        public void SceneryUI_HasTweenFields()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var spinOuter = typeof(SceneryUI).GetField("_spinOuterTween", flags);
            Assert.IsNotNull(spinOuter, "_spinOuterTween must exist");
            Assert.AreEqual(typeof(Tween), spinOuter.FieldType);

            var spinInner = typeof(SceneryUI).GetField("_spinInnerTween", flags);
            Assert.IsNotNull(spinInner, "_spinInnerTween must exist");

            var sigilOuter = typeof(SceneryUI).GetField("_sigilOuterTween", flags);
            Assert.IsNotNull(sigilOuter, "_sigilOuterTween must exist");

            var sigilInner = typeof(SceneryUI).GetField("_sigilInnerTween", flags);
            Assert.IsNotNull(sigilInner, "_sigilInnerTween must exist");

            var dividerOrb = typeof(SceneryUI).GetField("_dividerOrbTween", flags);
            Assert.IsNotNull(dividerOrb, "_dividerOrbTween must exist");

            var cornerGem = typeof(SceneryUI).GetField("_cornerGemTween", flags);
            Assert.IsNotNull(cornerGem, "_cornerGemTween must exist");
        }

        [Test]
        public void SceneryUI_HasCreateSpinTweenMethod()
        {
            var method = typeof(SceneryUI).GetMethod("CreateSpinTween",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "CreateSpinTween static method must exist");
        }

        [Test]
        public void SceneryUI_ConstantsUnchanged()
        {
            Assert.AreEqual(20f, SceneryUI.SPIN_OUTER_DURATION);
            Assert.AreEqual(12f, SceneryUI.SPIN_INNER_DURATION);
            Assert.AreEqual(30f, SceneryUI.SIGIL_OUTER_DURATION);
            Assert.AreEqual(20f, SceneryUI.SIGIL_INNER_DURATION);
            Assert.AreEqual(3.5f, SceneryUI.DIVIDER_ORB_DURATION);
            Assert.AreEqual(4f, SceneryUI.CORNER_GEM_DURATION);
            Assert.AreEqual(5f, SceneryUI.LEGEND_GLOW_DURATION);
            Assert.AreEqual(0.3f, SceneryUI.CORNER_GEM_ALPHA_MIN);
            Assert.AreEqual(0.9f, SceneryUI.CORNER_GEM_ALPHA_MAX);
            Assert.AreEqual(0.15f, SceneryUI.LEGEND_GLOW_ALPHA_MIN);
            Assert.AreEqual(0.6f, SceneryUI.LEGEND_GLOW_ALPHA_MAX);
            Assert.AreEqual(20f, SceneryUI.DIVIDER_ORB_AMPLITUDE);
        }

        [Test]
        public void SceneryUI_HasOnDestroyCleanup()
        {
            var method = typeof(SceneryUI).GetMethod("OnDestroy",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "OnDestroy must exist for tween cleanup");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BattlefieldGlow — structural verification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void BattlefieldGlow_HasNoAmbientBreatheLoopCoroutine()
        {
            var method = typeof(BattlefieldGlow).GetMethod("AmbientBreatheLoop",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "AmbientBreatheLoop coroutine should be removed");
        }

        [Test]
        public void BattlefieldGlow_HasNoCtrlGlowLoopCoroutine()
        {
            var method = typeof(BattlefieldGlow).GetMethod("CtrlGlowLoop",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "CtrlGlowLoop coroutine should be removed");
        }

        [Test]
        public void BattlefieldGlow_HasNoCoroutineHandles()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var breathe = typeof(BattlefieldGlow).GetField("_breatheRoutine", flags);
            Assert.IsNull(breathe, "_breatheRoutine Coroutine handle should be removed");

            var ctrl = typeof(BattlefieldGlow).GetField("_ctrlRoutine", flags);
            Assert.IsNull(ctrl, "_ctrlRoutine Coroutine handle should be removed");
        }

        [Test]
        public void BattlefieldGlow_HasTweenFields()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var breathe = typeof(BattlefieldGlow).GetField("_breatheTween", flags);
            Assert.IsNotNull(breathe, "_breatheTween must exist");
            Assert.AreEqual(typeof(Tween), breathe.FieldType);

            var ctrl = typeof(BattlefieldGlow).GetField("_ctrlTween", flags);
            Assert.IsNotNull(ctrl, "_ctrlTween must exist");
            Assert.AreEqual(typeof(Tween), ctrl.FieldType);
        }

        [Test]
        public void BattlefieldGlow_ConstantsUnchanged()
        {
            Assert.AreEqual(5f, BattlefieldGlow.BREATHE_PERIOD);
            Assert.AreEqual(0.02f, BattlefieldGlow.BREATHE_MIN_A);
            Assert.AreEqual(0.08f, BattlefieldGlow.BREATHE_MAX_A);
            Assert.AreEqual(3f, BattlefieldGlow.CTRL_PERIOD);
            Assert.AreEqual(0.10f, BattlefieldGlow.CTRL_MIN_A);
            Assert.AreEqual(0.35f, BattlefieldGlow.CTRL_MAX_A);
        }

        [Test]
        public void BattlefieldGlow_SetControlPublicAPI()
        {
            var method = typeof(BattlefieldGlow).GetMethod("SetControl",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "SetControl public method must exist");
        }

        [Test]
        public void BattlefieldGlow_HasOnDestroyCleanup()
        {
            var method = typeof(BattlefieldGlow).GetMethod("OnDestroy",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "OnDestroy must exist for tween cleanup");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SpellShowcaseUI — structural verification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SpellShowcaseUI_HasNoShowCoroutine()
        {
            var method = typeof(SpellShowcaseUI).GetMethod("ShowCoroutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "ShowCoroutine should be removed");
        }

        [Test]
        public void SpellShowcaseUI_HasNoShowGroupCoroutine()
        {
            var method = typeof(SpellShowcaseUI).GetMethod("ShowGroupCoroutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "ShowGroupCoroutine should be removed");
        }

        [Test]
        public void SpellShowcaseUI_HasShowSeqField()
        {
            var field = typeof(SpellShowcaseUI).GetField("_showSeq",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_showSeq Sequence must exist");
            Assert.AreEqual(typeof(Sequence), field.FieldType);
        }

        [Test]
        public void SpellShowcaseUI_ConstantsUnchanged()
        {
            Assert.AreEqual(0.4f, SpellShowcaseUI.FLY_IN_DURATION);
            Assert.AreEqual(0.5f, SpellShowcaseUI.HOLD_DURATION);
            // DISSOLVE_DURATION replaces original 0.35f fly-out; FLY_OUT_DURATION aliases DISSOLVE_DURATION.
            Assert.AreEqual(0.73f, SpellShowcaseUI.DISSOLVE_DURATION);
            Assert.AreEqual(SpellShowcaseUI.DISSOLVE_DURATION, SpellShowcaseUI.FLY_OUT_DURATION);
            Assert.AreEqual(0.4f + 0.5f + 0.73f, SpellShowcaseUI.TOTAL_DURATION, 0.001f);
        }

        [Test]
        public void SpellShowcaseUI_ShowAsyncReturnsTask()
        {
            var method = typeof(SpellShowcaseUI).GetMethod("ShowAsync",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "ShowAsync must exist");
            Assert.AreEqual(typeof(System.Threading.Tasks.Task), method.ReturnType);
        }

        [Test]
        public void SpellShowcaseUI_ShowGroupAsyncReturnsTask()
        {
            var method = typeof(SpellShowcaseUI).GetMethod("ShowGroupAsync",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "ShowGroupAsync must exist");
            Assert.AreEqual(typeof(System.Threading.Tasks.Task), method.ReturnType);
        }

        [Test]
        public void SpellShowcaseUI_HasOnDestroyCleanup()
        {
            var method = typeof(SpellShowcaseUI).GetMethod("OnDestroy",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "OnDestroy must exist for tween cleanup");
        }

        [Test]
        public void SpellShowcaseUI_HasActiveTcsField()
        {
            // H1 fix: TCS tracked for OnDestroy resolution
            var field = typeof(SpellShowcaseUI).GetField("_activeTcs",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_activeTcs must exist to prevent TCS leak on destroy");
        }

        [Test]
        public void SpellShowcaseUI_NullSpellReturnsCompletedTask()
        {
            var go = new GameObject("TestShowcase");
            var ui = go.AddComponent<SpellShowcaseUI>();
            var task = ui.ShowAsync(null, "player");
            Assert.IsTrue(task.IsCompleted, "null spell should return completed task");
            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // StartupFlowUI — structural verification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void StartupFlowUI_HasNoScanLightLoopCoroutine()
        {
            var method = typeof(StartupFlowUI).GetMethod("ScanLightLoop",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "ScanLightLoop coroutine should be removed");
        }

        [Test]
        public void StartupFlowUI_HasNoHexBreathLoopCoroutine()
        {
            var method = typeof(StartupFlowUI).GetMethod("HexBreathLoop",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "HexBreathLoop coroutine should be removed");
        }

        [Test]
        public void StartupFlowUI_HasNoTitleBeamPulseLoopCoroutine()
        {
            var method = typeof(StartupFlowUI).GetMethod("TitleBeamPulseLoop",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "TitleBeamPulseLoop coroutine should be removed");
        }

        [Test]
        public void StartupFlowUI_HasNoBgGradientRotateLoopCoroutine()
        {
            var method = typeof(StartupFlowUI).GetMethod("BgGradientRotateLoop",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "BgGradientRotateLoop coroutine should be removed");
        }

        [Test]
        public void StartupFlowUI_HasNoCoinSpinRoutineCoroutine()
        {
            var method = typeof(StartupFlowUI).GetMethod("CoinSpinRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "CoinSpinRoutine coroutine should be removed");
        }

        [Test]
        public void StartupFlowUI_HasNoScaleCoinXCoroutine()
        {
            var method = typeof(StartupFlowUI).GetMethod("ScaleCoinX",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "ScaleCoinX helper should be removed");
        }

        [Test]
        public void StartupFlowUI_HasNoCoinBurstParticlesCoroutine()
        {
            var method = typeof(StartupFlowUI).GetMethod("CoinBurstParticles",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "CoinBurstParticles coroutine should be removed");
        }

        [Test]
        public void StartupFlowUI_HasNoTitleTextEntranceRoutineCoroutine()
        {
            var method = typeof(StartupFlowUI).GetMethod("TitleTextEntranceRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            Assert.IsNull(method, "TitleTextEntranceRoutine coroutine should be removed");
        }

        [Test]
        public void StartupFlowUI_HasNoCoroutineHandles()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var scan = typeof(StartupFlowUI).GetField("_scanLightRoutine", flags);
            Assert.IsNull(scan, "_scanLightRoutine Coroutine handle should be removed");

            var hex = typeof(StartupFlowUI).GetField("_hexBreathRoutine", flags);
            Assert.IsNull(hex, "_hexBreathRoutine Coroutine handle should be removed");

            var beam = typeof(StartupFlowUI).GetField("_titleBeamRoutine", flags);
            Assert.IsNull(beam, "_titleBeamRoutine Coroutine handle should be removed");

            var bg = typeof(StartupFlowUI).GetField("_bgGradientRoutine", flags);
            Assert.IsNull(bg, "_bgGradientRoutine Coroutine handle should be removed");
        }

        [Test]
        public void StartupFlowUI_HasTweenFields()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var scan = typeof(StartupFlowUI).GetField("_scanLightTween", flags);
            Assert.IsNotNull(scan, "_scanLightTween must exist");
            Assert.AreEqual(typeof(Tween), scan.FieldType);

            var hex = typeof(StartupFlowUI).GetField("_hexBreathTween", flags);
            Assert.IsNotNull(hex, "_hexBreathTween must exist");

            var beam = typeof(StartupFlowUI).GetField("_titleBeamTween", flags);
            Assert.IsNotNull(beam, "_titleBeamTween must exist");

            var bg = typeof(StartupFlowUI).GetField("_bgGradientTween", flags);
            Assert.IsNotNull(bg, "_bgGradientTween must exist");

            // H2/H3: coin spin + title entrance tracked for cleanup
            var coinSpin = typeof(StartupFlowUI).GetField("_coinSpinSeq", flags);
            Assert.IsNotNull(coinSpin, "_coinSpinSeq must exist for destroy cleanup");
            Assert.AreEqual(typeof(Tween), coinSpin.FieldType);

            var titleEntrance = typeof(StartupFlowUI).GetField("_titleEntranceTween", flags);
            Assert.IsNotNull(titleEntrance, "_titleEntranceTween must exist for destroy cleanup");
            Assert.AreEqual(typeof(Tween), titleEntrance.FieldType);
        }

        [Test]
        public void StartupFlowUI_HasCreateMethods()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            var spin = typeof(StartupFlowUI).GetMethod("CreateCoinSpinSequence", flags);
            Assert.IsNotNull(spin, "CreateCoinSpinSequence must exist");

            var burst = typeof(StartupFlowUI).GetMethod("StartCoinBurstTweens", flags);
            Assert.IsNotNull(burst, "StartCoinBurstTweens must exist");

            var scanLight = typeof(StartupFlowUI).GetMethod("CreateScanLightTween", flags);
            Assert.IsNotNull(scanLight, "CreateScanLightTween must exist");

            var hexBreath = typeof(StartupFlowUI).GetMethod("CreateHexBreathTween", flags);
            Assert.IsNotNull(hexBreath, "CreateHexBreathTween must exist");

            var titleBeam = typeof(StartupFlowUI).GetMethod("CreateTitleBeamTween", flags);
            Assert.IsNotNull(titleBeam, "CreateTitleBeamTween must exist");

            var bgGradient = typeof(StartupFlowUI).GetMethod("CreateBgGradientTween", flags);
            Assert.IsNotNull(bgGradient, "CreateBgGradientTween must exist");
        }

        [Test]
        public void StartupFlowUI_HasCreateTitleEntranceSequence()
        {
            var method = typeof(StartupFlowUI).GetMethod("CreateTitleEntranceSequence",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "CreateTitleEntranceSequence static method must exist");
        }

        [Test]
        public void StartupFlowUI_FadeInReturnsTween()
        {
            var method = typeof(StartupFlowUI).GetMethod("FadeIn",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "FadeIn must exist");
            Assert.AreEqual(typeof(Tween), method.ReturnType, "FadeIn must return Tween");
        }

        [Test]
        public void StartupFlowUI_FadeOutReturnsTween()
        {
            var method = typeof(StartupFlowUI).GetMethod("FadeOut",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "FadeOut must exist");
            Assert.AreEqual(typeof(Tween), method.ReturnType, "FadeOut must return Tween");
        }

        [Test]
        public void StartupFlowUI_FadeInNullSafe()
        {
            var method = typeof(StartupFlowUI).GetMethod("FadeIn",
                BindingFlags.NonPublic | BindingFlags.Static);
            var result = method.Invoke(null, new object[] { null, 0.5f });
            Assert.IsNull(result, "FadeIn(null) should return null");
        }

        [Test]
        public void StartupFlowUI_FadeOutNullSafe()
        {
            var method = typeof(StartupFlowUI).GetMethod("FadeOut",
                BindingFlags.NonPublic | BindingFlags.Static);
            var result = method.Invoke(null, new object[] { null, 0.5f });
            Assert.IsNull(result, "FadeOut(null) should return null");
        }

        [Test]
        public void StartupFlowUI_ConstantsUnchanged()
        {
            Assert.AreEqual(0.4f, StartupFlowUI.PANEL_FADE_IN);
            Assert.AreEqual(0.3f, StartupFlowUI.PANEL_FADE_OUT);
            Assert.AreEqual(0.13f, StartupFlowUI.COIN_HALF_FLIP);
            Assert.AreEqual(5, StartupFlowUI.COIN_FLIP_COUNT);
            Assert.AreEqual(0.3f, StartupFlowUI.COIN_LAND_DUR);
            Assert.AreEqual(0.4f, StartupFlowUI.RESULT_FADE_IN);
            Assert.AreEqual(8f, StartupFlowUI.SCAN_PERIOD);
            Assert.AreEqual(20, StartupFlowUI.COIN_BURST_COUNT);
            Assert.AreEqual(0.6f, StartupFlowUI.COIN_BURST_DURATION);
            Assert.AreEqual(130f, StartupFlowUI.COIN_BURST_RADIUS);
            Assert.AreEqual(8f, StartupFlowUI.COIN_BURST_SIZE);
        }

        [Test]
        public void StartupFlowUI_FlowCoroutinesPreserved()
        {
            // Flow control coroutines should still exist (they contain game logic)
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            var coinFlow = typeof(StartupFlowUI).GetMethod("CoinFlipFlowRoutine", flags);
            Assert.IsNotNull(coinFlow, "CoinFlipFlowRoutine must be preserved (flow control)");
            Assert.AreEqual(typeof(IEnumerator), coinFlow.ReturnType);

            var mulliganFlow = typeof(StartupFlowUI).GetMethod("MulliganFlowRoutine", flags);
            Assert.IsNotNull(mulliganFlow, "MulliganFlowRoutine must be preserved (flow control)");
            Assert.AreEqual(typeof(IEnumerator), mulliganFlow.ReturnType);

            var fadeOutResolve = typeof(StartupFlowUI).GetMethod("FadeOutAndResolve", flags);
            Assert.IsNotNull(fadeOutResolve, "FadeOutAndResolve must be preserved (flow control)");
            Assert.AreEqual(typeof(IEnumerator), fadeOutResolve.ReturnType);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TweenHelper null-safety (regression)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void TweenHelper_KillSafe_NullTween()
        {
            Tween t = null;
            TweenHelper.KillSafe(ref t);
            Assert.IsNull(t);
        }

        [Test]
        public void TweenHelper_KillSafe_NullSequence()
        {
            Sequence s = null;
            TweenHelper.KillSafe(ref s);
            Assert.IsNull(s);
        }

        [Test]
        public void TweenHelper_FadeCanvasGroup_NullSafe()
        {
            var result = TweenHelper.FadeCanvasGroup(null, 1f, 0.5f);
            Assert.IsNull(result);
        }

        [Test]
        public void TweenHelper_PulseAlpha_Image_NullSafe()
        {
            var result = TweenHelper.PulseAlpha((Image)null, 0f, 1f, 2f);
            Assert.IsNull(result);
        }
    }
}
