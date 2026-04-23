using DG.Tweening;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using FWTCG.Tests.EditMode;
using FWTCG.UI;
using FWTCG.Audio;

namespace FWTCG.Tests
{
    /// <summary>
    /// DOT-3: Tests for 6 medium file DOTween replacements.
    /// Verifies coroutine → DOTween migration preserved behavior and cleaned up properly.
    /// </summary>
    [TestFixture]
    public class DOT3ReplacementTests : DOTweenTestBase
    {
        // ═══════════════════════════════════════════════════════════════════════
        // EventBanner
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void EventBanner_HasNoCoroutineAnimFields()
        {
            // _clearFadeRoutine should be removed
            var field = typeof(EventBanner).GetField("_clearFadeRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "EventBanner._clearFadeRoutine should be removed");
        }

        [Test]
        public void EventBanner_HasTweenFields()
        {
            var animField = typeof(EventBanner).GetField("_animTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(animField, "EventBanner must have _animTween field");
            Assert.AreEqual(typeof(Tween), animField.FieldType);

            var clearField = typeof(EventBanner).GetField("_clearFadeTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(clearField, "EventBanner must have _clearFadeTween field");
            Assert.AreEqual(typeof(Tween), clearField.FieldType);
        }

        [Test]
        public void EventBanner_HasNoAnimateInCoroutine()
        {
            var method = typeof(EventBanner).GetMethod("AnimateIn",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "EventBanner.AnimateIn coroutine should be removed");
        }

        [Test]
        public void EventBanner_HasNoAnimateOutCoroutine()
        {
            var method = typeof(EventBanner).GetMethod("AnimateOut",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "EventBanner.AnimateOut coroutine should be removed");
        }

        [Test]
        public void EventBanner_HasNoClearFadeRoutine()
        {
            var method = typeof(EventBanner).GetMethod("ClearFadeRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "EventBanner.ClearFadeRoutine should be removed");
        }

        [Test]
        public void EventBanner_HasNoEaseOutBack()
        {
            var method = typeof(EventBanner).GetMethod("EaseOutBack",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(method, "EventBanner.EaseOutBack helper should be removed (using DOTween Ease)");
        }

        [Test]
        public void EventBanner_HasCreateAnimateIn()
        {
            var method = typeof(EventBanner).GetMethod("CreateAnimateIn",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "EventBanner must have CreateAnimateIn method");
            Assert.AreEqual(typeof(Tween), method.ReturnType);
        }

        [Test]
        public void EventBanner_HasCreateAnimateOut()
        {
            var method = typeof(EventBanner).GetMethod("CreateAnimateOut",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "EventBanner must have CreateAnimateOut method");
            Assert.AreEqual(typeof(Tween), method.ReturnType);
        }

        [Test]
        public void EventBanner_Constants_Unchanged()
        {
            Assert.AreEqual(4, GetConstInt<EventBanner>("MAX_QUEUE"));
            Assert.AreEqual(0.1f, GetConstFloat<EventBanner>("ANIM_IN"), 0.001f);
            Assert.AreEqual(0.12f, GetConstFloat<EventBanner>("ANIM_OUT"), 0.001f);
            Assert.AreEqual(0.1f, GetConstFloat<EventBanner>("CLEAR_FADE"), 0.001f);
            Assert.AreEqual(1.5f, EventBanner.WARN_DURATION, 0.001f);
            Assert.AreEqual(0.25f, EventBanner.WARN_SCALE_IN, 0.001f);
        }

        [Test]
        public void EventBanner_HasOnDestroy()
        {
            var method = typeof(EventBanner).GetMethod("OnDestroy",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "EventBanner must have OnDestroy for tween cleanup");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AskPromptUI
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void AskPromptUI_HasNoCoroutineField()
        {
            var field = typeof(AskPromptUI).GetField("_animCoroutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "AskPromptUI._animCoroutine should be removed");
        }

        [Test]
        public void AskPromptUI_HasTweenField()
        {
            var field = typeof(AskPromptUI).GetField("_animTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "AskPromptUI must have _animTween field");
            Assert.AreEqual(typeof(Tween), field.FieldType);
        }

        [Test]
        public void AskPromptUI_HasNoShowRoutineCoroutine()
        {
            var method = typeof(AskPromptUI).GetMethod("ShowRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "AskPromptUI.ShowRoutine coroutine should be removed");
        }

        [Test]
        public void AskPromptUI_HasNoHideRoutineCoroutine()
        {
            var method = typeof(AskPromptUI).GetMethod("HideRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "AskPromptUI.HideRoutine coroutine should be removed");
        }

        [Test]
        public void AskPromptUI_Constants()
        {
            Assert.AreEqual(0.20f, GetConstFloat<AskPromptUI>("SHOW_DURATION"), 0.001f);
            Assert.AreEqual(0.12f, GetConstFloat<AskPromptUI>("HIDE_DURATION"), 0.001f);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SpellDuelUI
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SpellDuelUI_HasNoCoroutineFields()
        {
            var bp = typeof(SpellDuelUI).GetField("_borderPulse",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(bp, "SpellDuelUI._borderPulse Coroutine should be removed");

            var cr = typeof(SpellDuelUI).GetField("_countdownRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(cr, "SpellDuelUI._countdownRoutine Coroutine should be removed");
        }

        [Test]
        public void SpellDuelUI_HasTweenFields()
        {
            var bp = typeof(SpellDuelUI).GetField("_borderPulseTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(bp, "SpellDuelUI must have _borderPulseTween");
            Assert.AreEqual(typeof(Tween), bp.FieldType);

            var ct = typeof(SpellDuelUI).GetField("_countdownTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(ct, "SpellDuelUI must have _countdownTween");
            Assert.AreEqual(typeof(Tween), ct.FieldType);
        }

        [Test]
        public void SpellDuelUI_HasNoBorderPulseLoopCoroutine()
        {
            var method = typeof(SpellDuelUI).GetMethod("BorderPulseLoop",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "SpellDuelUI.BorderPulseLoop coroutine should be removed");
        }

        [Test]
        public void SpellDuelUI_HasNoCountdownRoutineCoroutine()
        {
            var method = typeof(SpellDuelUI).GetMethod("CountdownRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "SpellDuelUI.CountdownRoutine coroutine should be removed");
        }

        [Test]
        public void SpellDuelUI_HasKillTweens()
        {
            var method = typeof(SpellDuelUI).GetMethod("KillTweens",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "SpellDuelUI must have KillTweens cleanup method");
        }

        [Test]
        public void SpellDuelUI_HasNoStopRoutinesMethod()
        {
            var method = typeof(SpellDuelUI).GetMethod("StopRoutines",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "SpellDuelUI.StopRoutines should be removed (replaced by KillTweens)");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AudioTool
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void AudioTool_ChannelHasTweenField()
        {
            var field = typeof(AudioTool.AudioChannel).GetField("FadeTween");
            Assert.IsNotNull(field, "AudioChannel must have FadeTween field");
            Assert.AreEqual(typeof(Tween), field.FieldType);
        }

        [Test]
        public void AudioTool_ChannelHasNoCoroutineField()
        {
            var field = typeof(AudioTool.AudioChannel).GetField("FadeRoutine");
            Assert.IsNull(field, "AudioChannel.FadeRoutine Coroutine should be removed");
        }

        [Test]
        public void AudioTool_HasNoFadeRoutineCoroutine()
        {
            var method = typeof(AudioTool).GetMethod("FadeRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "AudioTool.FadeRoutine coroutine should be removed");
        }

        [Test]
        public void AudioTool_HasNoCrossFadeRoutineCoroutine()
        {
            var method = typeof(AudioTool).GetMethod("CrossFadeRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "AudioTool.CrossFadeRoutine coroutine should be removed");
        }

        [Test]
        public void AudioTool_HasStartFade()
        {
            var method = typeof(AudioTool).GetMethod("StartFade",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "AudioTool must have StartFade method");
        }

        [Test]
        public void AudioTool_HasStartCrossFade()
        {
            var method = typeof(AudioTool).GetMethod("StartCrossFade",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "AudioTool must have StartCrossFade method");
        }

        [Test]
        public void AudioTool_ChannelConstants_Unchanged()
        {
            Assert.AreEqual("bgm", AudioTool.CH_BGM);
            Assert.AreEqual("ui", AudioTool.CH_UI);
            Assert.AreEqual("card_spawn", AudioTool.CH_CARD_SPAWN);
            Assert.AreEqual("attack", AudioTool.CH_ATTACK);
            Assert.AreEqual("death", AudioTool.CH_DEATH);
            Assert.AreEqual("spell", AudioTool.CH_SPELL);
            Assert.AreEqual("ambient", AudioTool.CH_AMBIENT);
            Assert.AreEqual("score", AudioTool.CH_SCORE);
            Assert.AreEqual("legend", AudioTool.CH_LEGEND);
            Assert.AreEqual("duel", AudioTool.CH_DUEL);
            Assert.AreEqual("system", AudioTool.CH_SYSTEM);
        }

        [Test]
        public void AudioTool_PriorityConstants_Unchanged()
        {
            Assert.AreEqual(10, AudioTool.PRI_AMBIENT);
            Assert.AreEqual(20, AudioTool.PRI_UI);
            Assert.AreEqual(40, AudioTool.PRI_CARD);
            Assert.AreEqual(60, AudioTool.PRI_COMBAT);
            Assert.AreEqual(80, AudioTool.PRI_SPELL);
            Assert.AreEqual(100, AudioTool.PRI_SYSTEM);
        }

        [Test]
        public void AudioTool_FadeIn_SetsChannelTween()
        {
            // AudioSource assertions in batchmode
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("AudioTool");
            var at = go.AddComponent<AudioTool>();
            at.SendMessage("Awake");

            at.FadeIn(AudioTool.CH_BGM, 1f);
            var ch = at.GetChannel(AudioTool.CH_BGM);
            Assert.IsNotNull(ch.FadeTween, "FadeIn should create a FadeTween");
            Assert.IsTrue(ch.FadeTween.IsActive(), "FadeTween should be active");

            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void AudioTool_FadeOut_SetsChannelTween()
        {
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("AudioTool");
            var at = go.AddComponent<AudioTool>();
            at.SendMessage("Awake");

            at.FadeOut(AudioTool.CH_UI, 1f);
            var ch = at.GetChannel(AudioTool.CH_UI);
            Assert.IsNotNull(ch.FadeTween, "FadeOut should create a FadeTween");

            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void AudioTool_StopChannel_KillsFadeTween()
        {
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("AudioTool");
            var at = go.AddComponent<AudioTool>();
            at.SendMessage("Awake");

            at.FadeIn(AudioTool.CH_SPELL, 2f);
            at.StopChannel(AudioTool.CH_SPELL);
            var ch = at.GetChannel(AudioTool.CH_SPELL);
            Assert.IsNull(ch.FadeTween, "StopChannel should kill FadeTween");

            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void AudioTool_FadeIn_ZeroDuration_SetsVolumeImmediately()
        {
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("AudioTool");
            var at = go.AddComponent<AudioTool>();
            at.SendMessage("Awake");

            at.FadeIn(AudioTool.CH_BGM, 0f);
            var ch = at.GetChannel(AudioTool.CH_BGM);
            // BaseVolume for BGM is 0.4, master is 1.0
            Assert.AreEqual(0.4f, ch.Source.volume, 0.001f);
            Assert.IsNull(ch.FadeTween, "Zero duration should not create a tween");

            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CardHoverScale
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CardHoverScale_HasNoAnimatingField()
        {
            var field = typeof(CardHoverScale).GetField("_animating",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "CardHoverScale._animating should be removed");
        }

        [Test]
        public void CardHoverScale_HasNoTargetScaleField()
        {
            var field = typeof(CardHoverScale).GetField("_targetScale",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "CardHoverScale._targetScale should be removed");
        }

        [Test]
        public void CardHoverScale_HasTweenField()
        {
            var field = typeof(CardHoverScale).GetField("_scaleTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "CardHoverScale must have _scaleTween field");
            Assert.AreEqual(typeof(Tween), field.FieldType);
        }

        [Test]
        public void CardHoverScale_HasNoLerpSpeedConstant()
        {
            var field = typeof(CardHoverScale).GetField("LERP_SPEED",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(field, "CardHoverScale.LERP_SPEED should be removed (replaced by TWEEN_DURATION)");
        }

        [Test]
        public void CardHoverScale_HasTweenDurationConstant()
        {
            var val = GetConstFloat<CardHoverScale>("TWEEN_DURATION");
            Assert.Greater(val, 0f, "TWEEN_DURATION must be positive");
            Assert.LessOrEqual(val, 0.5f, "TWEEN_DURATION should be short for snappy feel");
        }

        [Test]
        public void CardHoverScale_HoverScaleConstant_Unchanged()
        {
            var val = GetConstFloat<CardHoverScale>("HOVER_SCALE");
            Assert.AreEqual(1.18f, val, 0.001f); // DEV-31 cleanup: source is 1.18f
        }

        [Test]
        public void CardHoverScale_HasOnDestroy()
        {
            var method = typeof(CardHoverScale).GetMethod("OnDestroy",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "CardHoverScale must have OnDestroy for tween cleanup");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PortalVFX
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void PortalVFX_HasNoCoroutineField()
        {
            var field = typeof(PortalVFX).GetField("_fadeCoroutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "PortalVFX._fadeCoroutine should be removed");
        }

        [Test]
        public void PortalVFX_HasTweenField()
        {
            var field = typeof(PortalVFX).GetField("_fadeTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "PortalVFX must have _fadeTween field");
            Assert.AreEqual(typeof(Tween), field.FieldType);
        }

        [Test]
        public void PortalVFX_HasNoFadeRoutineCoroutine()
        {
            var method = typeof(PortalVFX).GetMethod("FadeRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "PortalVFX.FadeRoutine coroutine should be removed");
        }

        [Test]
        public void PortalVFX_Constants_Unchanged()
        {
            Assert.AreEqual(3, PortalVFX.RING_COUNT);
            Assert.AreEqual(60f, PortalVFX.RING_OUTER_RADIUS, 0.001f);
            Assert.AreEqual(42f, PortalVFX.RING_MID_RADIUS, 0.001f);
            Assert.AreEqual(24f, PortalVFX.RING_INNER_RADIUS, 0.001f);
            Assert.AreEqual(8, PortalVFX.ORBITAL_COUNT);
            Assert.AreEqual(0.28f, PortalVFX.FADE_IN_DURATION, 0.001f);
            Assert.AreEqual(0.22f, PortalVFX.FADE_OUT_DURATION, 0.001f);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TweenHelper null-safety
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void TweenHelper_KillSafe_NullTween()
        {
            Tween t = null;
            TweenHelper.KillSafe(ref t);
            Assert.IsNull(t);
        }

        [Test]
        public void TweenHelper_FadeCanvasGroup_NullSafe()
        {
            var result = TweenHelper.FadeCanvasGroup(null, 0f, 1f);
            Assert.IsNull(result);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static int GetConstInt<T>(string name)
        {
            var field = typeof(T).GetField(name,
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(field, $"{typeof(T).Name}.{name} not found");
            return (int)field.GetValue(null);
        }

        private static float GetConstFloat<T>(string name)
        {
            var field = typeof(T).GetField(name,
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(field, $"{typeof(T).Name}.{name} not found");
            return (float)field.GetValue(null);
        }
    }
}
