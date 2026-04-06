using DG.Tweening;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Tests.EditMode;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DOT-6: Tests for GameUI.cs DOTween replacements (18 coroutines → DOTween).
    /// Verifies coroutine removal, new tween fields, constants, and null-safety.
    /// </summary>
    [TestFixture]
    public class DOT6ReplacementTests : DOTweenTestBase
    {
        private const BindingFlags PRIV = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PRIV_STATIC = BindingFlags.NonPublic | BindingFlags.Static;

        // ═══════════════════════════════════════════════════════════════════════
        // Old coroutine removal — verify IEnumerator methods are gone
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void GameUI_NoBannerSlideRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("BannerSlideRoutine", PRIV), "BannerSlideRoutine should be removed"); }

        [Test] public void GameUI_NoRuneHighlightPulseRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("RuneHighlightPulseRoutine", PRIV), "RuneHighlightPulseRoutine should be removed"); }

        [Test] public void GameUI_NoPhasePulseRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("PhasePulseRoutine", PRIV), "PhasePulseRoutine should be removed"); }

        [Test] public void GameUI_NoLogEntryFlashRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("LogEntryFlashRoutine", PRIV), "LogEntryFlashRoutine should be removed"); }

        [Test] public void GameUI_NoGameOverEnhancedRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("GameOverEnhancedRoutine", PRIV), "GameOverEnhancedRoutine should be removed"); }

        [Test] public void GameUI_NoFadeInPanelRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("FadeInPanelRoutine", PRIV), "FadeInPanelRoutine should be removed"); }

        [Test] public void GameUI_NoBoardFlashRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("BoardFlashRoutine", PRIV), "BoardFlashRoutine should be removed"); }

        [Test] public void GameUI_NoTimerPulseRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("TimerPulseRoutine", PRIV), "TimerPulseRoutine should be removed"); }

        [Test] public void GameUI_NoTimerCountdown()
        { Assert.IsNull(typeof(GameUI).GetMethod("TimerCountdown", PRIV), "TimerCountdown should be removed"); }

        [Test] public void GameUI_NoScorePulseRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("ScorePulseRoutine", PRIV), "ScorePulseRoutine should be removed"); }

        [Test] public void GameUI_NoScoreRingRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("ScoreRingRoutine", PRIV), "ScoreRingRoutine should be removed"); }

        [Test] public void GameUI_NoEndTurnPulseRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("EndTurnPulseRoutine", PRIV), "EndTurnPulseRoutine should be removed"); }

        [Test] public void GameUI_NoReactRibbonRevealRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("ReactRibbonRevealRoutine", PRIV), "ReactRibbonRevealRoutine should be removed"); }

        [Test] public void GameUI_NoEquipFlyRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("EquipFlyRoutine", PRIV), "EquipFlyRoutine should be removed"); }

        [Test] public void GameUI_NoFlashLegendTextRoutine()
        { Assert.IsNull(typeof(GameUI).GetMethod("FlashLegendText", PRIV), "FlashLegendText coroutine should be removed"); }

        [Test] public void GameUI_NoFadeLegendGlowRoutine()
        {
            // Static IEnumerator should be replaced with static void
            var method = typeof(GameUI).GetMethod("FadeLegendGlow", PRIV_STATIC);
            Assert.IsNotNull(method, "FadeLegendGlow should still exist as static method");
            Assert.AreEqual(typeof(void), method.ReturnType, "FadeLegendGlow should return void, not IEnumerator");
        }

        [Test] public void GameUI_NoAnimateLogToggle()
        { Assert.IsNull(typeof(GameUI).GetMethod("AnimateLogToggle", PRIV), "AnimateLogToggle should be removed"); }

        [Test] public void GameUI_NoShowHideCombatResult()
        { Assert.IsNull(typeof(GameUI).GetMethod("ShowHideCombatResult", PRIV), "ShowHideCombatResult should be removed"); }

        // ═══════════════════════════════════════════════════════════════════════
        // Old Coroutine fields removed
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void GameUI_NoCoroutineField_logAnimCoroutine()
        { Assert.IsNull(typeof(GameUI).GetField("_logAnimCoroutine", PRIV), "_logAnimCoroutine should be removed"); }

        [Test] public void GameUI_NoCoroutineField_timerCoroutine()
        { Assert.IsNull(typeof(GameUI).GetField("_timerCoroutine", PRIV), "_timerCoroutine should be removed"); }

        [Test] public void GameUI_NoCoroutineField_timerPulseRoutine()
        { Assert.IsNull(typeof(GameUI).GetField("_timerPulseRoutine", PRIV), "_timerPulseRoutine should be removed"); }

        [Test] public void GameUI_NoCoroutineField_boardFlashCoroutine()
        { Assert.IsNull(typeof(GameUI).GetField("_boardFlashCoroutine", PRIV), "_boardFlashCoroutine should be removed"); }

        [Test] public void GameUI_NoCoroutineField_phasePulseCoroutine()
        { Assert.IsNull(typeof(GameUI).GetField("_phasePulseCoroutine", PRIV), "_phasePulseCoroutine should be removed"); }

        [Test] public void GameUI_NoCoroutineField_bannerAnimCoroutine()
        { Assert.IsNull(typeof(GameUI).GetField("_bannerAnimCoroutine", PRIV), "_bannerAnimCoroutine should be removed"); }

        [Test] public void GameUI_NoCoroutineField_endTurnPulseCoroutine()
        { Assert.IsNull(typeof(GameUI).GetField("_endTurnPulseCoroutine", PRIV), "_endTurnPulseCoroutine should be removed"); }

        [Test] public void GameUI_NoCoroutineField_runeHighlightPulseCoroutine()
        { Assert.IsNull(typeof(GameUI).GetField("_runeHighlightPulseCoroutine", PRIV), "_runeHighlightPulseCoroutine should be removed"); }

        [Test] public void GameUI_NoCoroutineField_crHideRoutine()
        { Assert.IsNull(typeof(GameUI).GetField("_crHideRoutine", PRIV), "_crHideRoutine should be removed"); }

        // ═══════════════════════════════════════════════════════════════════════
        // New Tween fields exist
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void GameUI_HasTweenField_logAnimTween()
        { Assert.IsNotNull(typeof(GameUI).GetField("_logAnimTween", PRIV), "_logAnimTween must exist"); }

        [Test] public void GameUI_HasTweenField_timerTween()
        { Assert.IsNotNull(typeof(GameUI).GetField("_timerTween", PRIV), "_timerTween must exist"); }

        [Test] public void GameUI_HasTweenField_timerPulseTween()
        { Assert.IsNotNull(typeof(GameUI).GetField("_timerPulseTween", PRIV), "_timerPulseTween must exist"); }

        [Test] public void GameUI_HasTweenField_boardFlashTween()
        { Assert.IsNotNull(typeof(GameUI).GetField("_boardFlashTween", PRIV), "_boardFlashTween must exist"); }

        [Test] public void GameUI_HasTweenField_phasePulseTween()
        { Assert.IsNotNull(typeof(GameUI).GetField("_phasePulseTween", PRIV), "_phasePulseTween must exist"); }

        [Test] public void GameUI_HasSequenceField_bannerAnimSeq()
        { Assert.IsNotNull(typeof(GameUI).GetField("_bannerAnimSeq", PRIV), "_bannerAnimSeq must exist"); }

        [Test] public void GameUI_HasTweenField_endTurnPulseTween()
        { Assert.IsNotNull(typeof(GameUI).GetField("_endTurnPulseTween", PRIV), "_endTurnPulseTween must exist"); }

        [Test] public void GameUI_HasTweenField_runeHighlightPulseTween()
        { Assert.IsNotNull(typeof(GameUI).GetField("_runeHighlightPulseTween", PRIV), "_runeHighlightPulseTween must exist"); }

        [Test] public void GameUI_HasSequenceField_crHideSeq()
        { Assert.IsNotNull(typeof(GameUI).GetField("_crHideSeq", PRIV), "_crHideSeq must exist"); }

        [Test] public void GameUI_HasSequenceField_gameOverSeq()
        { Assert.IsNotNull(typeof(GameUI).GetField("_gameOverSeq", PRIV), "_gameOverSeq must exist"); }

        // ═══════════════════════════════════════════════════════════════════════
        // Constants preserved / new constants exist
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void GameUI_TimerPulseConstants()
        {
            Assert.AreEqual(2f, GameUI.TIMER_PULSE_FREQ, "TIMER_PULSE_FREQ should be 2 Hz");
            Assert.AreEqual(1.15f, GameUI.TIMER_PULSE_SCALE, "TIMER_PULSE_SCALE should be 1.15");
        }

        [Test] public void GameUI_PhasePulseConstants()
        {
            Assert.AreEqual(0.4f, GameUI.PHASE_PULSE_DURATION, "PHASE_PULSE_DURATION should be 0.4s");
            Assert.AreEqual(1.18f, GameUI.PHASE_PULSE_PEAK, "PHASE_PULSE_PEAK should be 1.18");
        }

        [Test] public void GameUI_BoardFlashConstants()
        {
            Assert.AreEqual(0.425f, GameUI.BOARD_FLASH_HALF, "BOARD_FLASH_HALF should be 0.425s");
        }

        [Test] public void GameUI_EndTurnPulseConstants()
        {
            Assert.AreEqual(2f, GameUI.ENDTURN_PULSE_PERIOD, "ENDTURN_PULSE_PERIOD should be 2s");
            Assert.AreEqual(0.60f, GameUI.ENDTURN_PULSE_MIN_ALPHA, "ENDTURN_PULSE_MIN_ALPHA should be 0.60");
        }

        [Test] public void GameUI_ReactRevealConstants()
        {
            Assert.AreEqual(0.25f, GameUI.REACT_REVEAL_DUR, "REACT_REVEAL_DUR should be 0.25s");
            Assert.AreEqual(2f, GameUI.REACT_PULSE_DUR, "REACT_PULSE_DUR should be 2s");
            Assert.AreEqual(0.06f, GameUI.REACT_PULSE_AMP, "REACT_PULSE_AMP should be 0.06");
        }

        [Test] public void GameUI_EquipFlyConstants()
        {
            Assert.AreEqual(0.35f, GameUI.EQUIP_FLY_DURATION, "EQUIP_FLY_DURATION should be 0.35s");
        }

        [Test] public void GameUI_LogFlashConstants()
        {
            Assert.AreEqual(0.8f, GameUI.LOG_FLASH_DURATION, "LOG_FLASH_DURATION should be 0.8s");
            Assert.AreEqual(new Color(0.95f, 0.82f, 0.40f, 1f), GameUI.LOG_FLASH_GOLD, "LOG_FLASH_GOLD should match");
        }

        [Test] public void GameUI_GameOverConstants()
        {
            Assert.AreEqual(0.5f, GameUI.GAMEOVER_FADE_DUR, "GAMEOVER_FADE_DUR should be 0.5s");
            Assert.AreEqual(0.4f, GameUI.GAMEOVER_WIN_SCALE_DUR, "GAMEOVER_WIN_SCALE_DUR should be 0.4s");
        }

        [Test] public void GameUI_CombatResultConstants()
        {
            Assert.AreEqual(0.2f, GameUI.CR_FADE_IN, "CR_FADE_IN should be 0.2s");
            Assert.AreEqual(3.5f, GameUI.CR_STAY, "CR_STAY should be 3.5s");
            Assert.AreEqual(0.3f, GameUI.CR_FADE_OUT, "CR_FADE_OUT should be 0.3s");
        }

        [Test] public void GameUI_ScorePulseConstants()
        {
            Assert.AreEqual(0.9f, GameUI.SCORE_PULSE_HALF, "SCORE_PULSE_HALF should be 0.9s");
            Assert.AreEqual(1.15f, GameUI.SCORE_PULSE_PEAK, "SCORE_PULSE_PEAK should be 1.15");
        }

        [Test] public void GameUI_ScoreRingConstants()
        {
            Assert.AreEqual(2f, GameUI.SCORE_RING_DURATION, "SCORE_RING_DURATION should be 2s");
        }

        [Test] public void GameUI_LogToggleConstants()
        {
            Assert.AreEqual(0.3f, GameUI.LOG_TOGGLE_DURATION, "LOG_TOGGLE_DURATION should be 0.3s");
        }

        [Test] public void GameUI_LegendGlowConstants()
        {
            Assert.AreEqual(4f, GameUI.LEGEND_GLOW_SPEED, "LEGEND_GLOW_SPEED should be 4 units/s");
        }

        [Test] public void GameUI_RunePulseConstants()
        {
            Assert.AreEqual(3.5f, GameUI.RUNE_PULSE_FREQ, "RUNE_PULSE_FREQ should be 3.5");
            Assert.AreEqual(new Color(0.15f, 0.50f, 1.0f, 1f), GameUI.RuneTapFill, "RuneTapFill color");
            Assert.AreEqual(new Color(1.0f, 0.15f, 0.15f, 1f), GameUI.RuneRecFill, "RuneRecFill color");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DOTween method existence
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void GameUI_HasCreateRuneHighlightPulseTween()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("CreateRuneHighlightPulseTween", PRIV), "CreateRuneHighlightPulseTween must exist"); }

        [Test] public void GameUI_HasCreateTimerPulseTween()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("CreateTimerPulseTween", PRIV), "CreateTimerPulseTween must exist"); }

        [Test] public void GameUI_HasCreateGameOverSequence()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("CreateGameOverSequence", PRIV), "CreateGameOverSequence must exist"); }

        [Test] public void GameUI_HasStartEquipFlyTween()
        { Assert.IsNotNull(typeof(GameUI).GetMethod("StartEquipFlyTween", PRIV), "StartEquipFlyTween must exist"); }

        // ═══════════════════════════════════════════════════════════════════════
        // No IEnumerator return types in GameUI
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GameUI_NoIEnumeratorMethods()
        {
            var methods = typeof(GameUI).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var m in methods)
            {
                if (m.DeclaringType != typeof(GameUI)) continue; // skip inherited
                Assert.AreNotEqual(typeof(System.Collections.IEnumerator), m.ReturnType,
                    $"GameUI.{m.Name} should not return IEnumerator — all coroutines should be replaced");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TweenHelper null-safety regression
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void TweenHelper_KillSafe_NullTween()
        {
            Tween t = null;
            Assert.DoesNotThrow(() => TweenHelper.KillSafe(ref t));
            Assert.IsNull(t);
        }

        [Test]
        public void TweenHelper_KillSafe_NullSequence()
        {
            Sequence s = null;
            Assert.DoesNotThrow(() => TweenHelper.KillSafe(ref s));
            Assert.IsNull(s);
        }

        [Test]
        public void TweenHelper_FadeCanvasGroup_NullSafe()
        {
            Assert.IsNull(TweenHelper.FadeCanvasGroup(null, 1f, 0.5f));
        }

        [Test]
        public void TweenHelper_PulseAlpha_CG_NullSafe()
        {
            Assert.IsNull(TweenHelper.PulseAlpha((CanvasGroup)null, 0.5f, 1f, 2f));
        }

        [Test]
        public void TweenHelper_FadeImage_NullSafe()
        {
            Assert.IsNull(TweenHelper.FadeImage(null, 1f, 0.5f));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FadeLegendGlow null-safety
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GameUI_FadeLegendGlow_NullSafe()
        {
            var method = typeof(GameUI).GetMethod("FadeLegendGlow", PRIV_STATIC);
            Assert.IsNotNull(method);
            Assert.DoesNotThrow(() => method.Invoke(null, new object[] { null, 0.5f }));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // No System.Collections using (coroutine infrastructure removed)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GameUI_NoCoroutineFieldsOfTypeCoroutine()
        {
            var fields = typeof(GameUI).GetFields(PRIV);
            foreach (var f in fields)
            {
                if (f.DeclaringType != typeof(GameUI)) continue;
                Assert.AreNotEqual(typeof(UnityEngine.Coroutine), f.FieldType,
                    $"Field {f.Name} should not be of type Coroutine");
            }
        }
    }
}
