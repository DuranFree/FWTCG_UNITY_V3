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
    /// DOT-2: Tests for 8 small file DOTween replacements.
    /// Verifies coroutine → DOTween migration preserved behavior and cleaned up properly.
    /// </summary>
    [TestFixture]
    public class DOT2ReplacementTests : DOTweenTestBase
    {
        // ── FloatText ────────────────────────────────────────────────────────

        [Test]
        public void FloatText_HasNoCoroutineField()
        {
            var field = typeof(FloatText).GetField("_routine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "FloatText._routine should be removed (replaced by _seq)");
        }

        [Test]
        public void FloatText_HasSequenceField()
        {
            var field = typeof(FloatText).GetField("_seq",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "FloatText must have _seq Sequence field");
            Assert.AreEqual(typeof(Sequence), field.FieldType);
        }

        [Test]
        public void FloatText_Show_WithNullCanvasRoot_ReturnsNull()
        {
            var result = FloatText.Show(Vector2.zero, "test", Color.white, null);
            Assert.IsNull(result);
        }

        // ── DamagePopup ──────────────────────────────────────────────────────

        [Test]
        public void DamagePopup_HasSequenceField()
        {
            var field = typeof(DamagePopup).GetField("_seq",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "DamagePopup must have _seq Sequence field");
            Assert.AreEqual(typeof(Sequence), field.FieldType);
        }

        [Test]
        public void DamagePopup_HasNoAnimateRoutine()
        {
            var method = typeof(DamagePopup).GetMethod("AnimateRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "DamagePopup.AnimateRoutine should be removed");
        }

        [Test]
        public void DamagePopup_Create_ReturnsValidComponent()
        {
            var canvas = new GameObject("Canvas");
            canvas.AddComponent<Canvas>();
            var rt = canvas.GetComponent<RectTransform>();

            var popup = DamagePopup.Create(5, Vector2.zero, rt);
            Assert.IsNotNull(popup);
            Assert.IsNotNull(popup.gameObject);

            Object.DestroyImmediate(popup.gameObject);
            Object.DestroyImmediate(canvas);
        }

        // ── ButtonCharge ─────────────────────────────────────────────────────

        [Test]
        public void ButtonCharge_HasTweenField()
        {
            var field = typeof(ButtonCharge).GetField("_sweepTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "ButtonCharge must have _sweepTween Tween field");
            Assert.AreEqual(typeof(Tween), field.FieldType);
        }

        [Test]
        public void ButtonCharge_HasNoCoroutineField()
        {
            var field = typeof(ButtonCharge).GetField("_sweepCoroutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "ButtonCharge._sweepCoroutine should be removed");
        }

        [Test]
        public void ButtonCharge_HasNoSweepRoutine()
        {
            var method = typeof(ButtonCharge).GetMethod("SweepRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "ButtonCharge.SweepRoutine should be removed");
        }

        [Test]
        public void ButtonCharge_Constants_Unchanged()
        {
            var durationField = typeof(ButtonCharge).GetField("DURATION",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(durationField);
            Assert.AreEqual(1.5f, (float)durationField.GetValue(null));

            var factorField = typeof(ButtonCharge).GetField("SWEEP_WIDTH_FACTOR",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(factorField);
            Assert.AreEqual(0.45f, (float)factorField.GetValue(null));
        }

        // ── ButtonHoverGlow ──────────────────────────────────────────────────

        [Test]
        public void ButtonHoverGlow_HasTweenField()
        {
            var field = typeof(ButtonHoverGlow).GetField("_pulseTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "ButtonHoverGlow must have _pulseTween Tween field");
            Assert.AreEqual(typeof(Tween), field.FieldType);
        }

        [Test]
        public void ButtonHoverGlow_HasNoPulseRoutine()
        {
            var method = typeof(ButtonHoverGlow).GetMethod("PulseRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "ButtonHoverGlow.PulseRoutine should be removed");
        }

        [Test]
        public void ButtonHoverGlow_HasNoCoroutineField()
        {
            var field = typeof(ButtonHoverGlow).GetField("_pulseCoroutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "ButtonHoverGlow._pulseCoroutine should be removed");
        }

        // ── MouseTrail ───────────────────────────────────────────────────────

        [Test]
        public void MouseTrail_HasNoClickEffectRoutine()
        {
            var method = typeof(MouseTrail).GetMethod("ClickEffectRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "MouseTrail.ClickEffectRoutine should be replaced by SpawnClickEffect");
        }

        [Test]
        public void MouseTrail_HasSpawnClickEffect()
        {
            var method = typeof(MouseTrail).GetMethod("SpawnClickEffect",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "MouseTrail must have SpawnClickEffect method");
        }

        [Test]
        public void MouseTrail_Constants_Unchanged()
        {
            Assert.AreEqual(18, MouseTrail.TRAIL_LENGTH);
            Assert.AreEqual(0.65f, MouseTrail.TRAIL_HEAD_ALPHA);
            Assert.AreEqual(8f, MouseTrail.DOT_MAX_SIZE);
        }

        // ── ToastUI ──────────────────────────────────────────────────────────

        [Test]
        public void ToastUI_HasFadeTweenField()
        {
            var field = typeof(ToastUI).GetField("_fadeTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "ToastUI must have _fadeTween Tween field");
            Assert.AreEqual(typeof(Tween), field.FieldType);
        }

        [Test]
        public void ToastUI_HasNoShowToastMethod()
        {
            // Old method was ShowToast, replaced by ShowToastRoutine
            var oldMethod = typeof(ToastUI).GetMethod("ShowToast",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(oldMethod, "ToastUI.ShowToast should be renamed to ShowToastRoutine");
        }

        [Test]
        public void ToastUI_HasShowToastRoutine()
        {
            var method = typeof(ToastUI).GetMethod("ShowToastRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "ToastUI must have ShowToastRoutine coroutine");
        }

        [Test]
        public void ToastUI_Constants_Unchanged()
        {
            var fadeIn = typeof(ToastUI).GetField("FADE_IN",
                BindingFlags.NonPublic | BindingFlags.Static);
            var stay = typeof(ToastUI).GetField("STAY",
                BindingFlags.NonPublic | BindingFlags.Static);
            var fadeOut = typeof(ToastUI).GetField("FADE_OUT",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.AreEqual(0.15f, (float)fadeIn.GetValue(null));
            Assert.AreEqual(0.8f, (float)stay.GetValue(null));
            Assert.AreEqual(0.2f, (float)fadeOut.GetValue(null));
        }

        // ── ReactiveWindowUI ─────────────────────────────────────────────────

        [Test]
        public void ReactiveWindowUI_HasCountdownTweenField()
        {
            var field = typeof(ReactiveWindowUI).GetField("_countdownTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "ReactiveWindowUI must have _countdownTween Tween field");
            Assert.AreEqual(typeof(Tween), field.FieldType);
        }

        [Test]
        public void ReactiveWindowUI_HasNoCountdownRoutine()
        {
            var method = typeof(ReactiveWindowUI).GetMethod("CountdownRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "ReactiveWindowUI.CountdownRoutine should be replaced by StartCountdown");
        }

        [Test]
        public void ReactiveWindowUI_HasStartCountdown()
        {
            var method = typeof(ReactiveWindowUI).GetMethod("StartCountdown",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "ReactiveWindowUI must have StartCountdown method");
        }

        [Test]
        public void ReactiveWindowUI_HasNoCoroutineField()
        {
            var field = typeof(ReactiveWindowUI).GetField("_countdownRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "ReactiveWindowUI._countdownRoutine should be removed");
        }

        // ── AnimMatFX removal verification ───────────────────────────────────

        [Test]
        public void CardView_DissolveRoutine_NoAnimMatFXReference()
        {
            // Verify CardView no longer has a direct field dependency on AnimMatFX
            var fields = typeof(FWTCG.UI.CardView).GetFields(
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                Assert.AreNotEqual(typeof(FWTCG.FX.AnimMatFX), field.FieldType,
                    $"CardView.{field.Name} still references AnimMatFX — should use TweenMatFX");
            }
        }

        // ── TweenHelper integration ──────────────────────────────────────────

        [Test]
        public void TweenHelper_KillSafe_Sequence_NullSafe()
        {
            Sequence seq = null;
            Assert.DoesNotThrow(() => TweenHelper.KillSafe(ref seq));
            Assert.IsNull(seq);
        }

        [Test]
        public void TweenHelper_KillSafe_Tween_NullSafe()
        {
            Tween tw = null;
            Assert.DoesNotThrow(() => TweenHelper.KillSafe(ref tw));
            Assert.IsNull(tw);
        }

        [Test]
        public void TweenHelper_FadeCanvasGroup_NullSafe()
        {
            var tw = TweenHelper.FadeCanvasGroup(null, 1f, 0.5f);
            Assert.IsNull(tw);
        }

        [Test]
        public void TweenHelper_FadeImage_NullSafe()
        {
            var tw = TweenHelper.FadeImage(null, 1f, 0.5f);
            Assert.IsNull(tw);
        }

        [Test]
        public void TweenHelper_PulseAlpha_Image_NullSafe()
        {
            var tw = TweenHelper.PulseAlpha((Image)null, 0f, 1f, 1f);
            Assert.IsNull(tw);
        }

        [Test]
        public void TweenHelper_ShakeUI_NullSafe()
        {
            var tw = TweenHelper.ShakeUI(null);
            Assert.IsNull(tw);
        }
    }
}
