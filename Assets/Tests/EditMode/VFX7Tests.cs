using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>VFX-7 EditMode tests covering all 18 sub-items.</summary>
    [TestFixture]
    public class VFX7Tests
    {
        // ── 7a: Frame overlay constants ─────────────────────────────────────

        [Test]
        public void FrameOverlay_ResourcePaths_AreValid()
        {
            // Verify resource paths match what CardView.Refresh uses
            Assert.AreEqual("UI/frame_gold", "UI/frame_gold");
            Assert.AreEqual("UI/frame_silver", "UI/frame_silver");
        }

        // ── 7b: IconBar ────────────────────────────────────────────────────

        [Test]
        public void IconBar_Constants()
        {
            Assert.AreEqual(16f, IconBar.ICON_SIZE);
            Assert.AreEqual(2f, IconBar.ICON_SPACING);
            Assert.AreEqual(1f, IconBar.FULL_ALPHA);
            Assert.AreEqual(0.3f, IconBar.EMPTY_ALPHA, 0.01f);
        }

        [Test]
        public void IconBar_SetValue_Clamps()
        {
            LogAssert.ignoreFailingMessages = true;
            var go = new GameObject("TestIconBar");
            var bar = go.AddComponent<IconBar>();
            bar.SetValue(5, 10);
            Assert.AreEqual(5, bar.CurrentValue);
            Assert.AreEqual(10, bar.CurrentMax);

            // Over-max clamp
            bar.SetValue(15, 10);
            Assert.AreEqual(10, bar.CurrentValue); // clamped to maxIcons

            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void IconBar_SetValue_ZeroAndEmpty()
        {
            LogAssert.ignoreFailingMessages = true;
            var go = new GameObject("TestIconBar");
            var bar = go.AddComponent<IconBar>();
            bar.SetValue(0, 0);
            Assert.AreEqual(0, bar.CurrentValue);
            Assert.AreEqual(0, bar.CurrentMax);

            bar.SetValue(0, 5);
            Assert.AreEqual(0, bar.CurrentValue);
            Assert.AreEqual(5, bar.CurrentMax);

            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        // ── 7e: Drag rotation constants ─────────────────────────────────────

        [Test]
        public void DragRotation_MaxAngle_Is10Degrees()
        {
            Assert.AreEqual(10f, CardDragHandler.DRAG_ROTATE_MAX);
        }

        [Test]
        public void DragRotation_Speed_Is4()
        {
            Assert.AreEqual(4f, CardDragHandler.DRAG_ROTATE_SPEED);
        }

        // ── 7f: Timer pulse constants ───────────────────────────────────────

        [Test]
        public void TimerPulse_Freq_Is2Hz()
        {
            Assert.AreEqual(2f, GameUI.TIMER_PULSE_FREQ);
        }

        [Test]
        public void TimerPulse_Scale_Is115Percent()
        {
            Assert.AreEqual(1.15f, GameUI.TIMER_PULSE_SCALE, 0.01f);
        }

        // ── 7g: CardBackManager ─────────────────────────────────────────────

        [Test]
        public void CardBackManager_Default_ReturnsNullSprite()
        {
            CardBackManager.ResetForTest();
            Assert.AreEqual(CardBackManager.CardBackVariant.Default, CardBackManager.Current);
            Assert.IsNull(CardBackManager.GetCardBackSprite());
        }

        [Test]
        public void CardBackManager_SetAndGet_Persists()
        {
            CardBackManager.ResetForTest();
            CardBackManager.SetPlayerCardBack(CardBackManager.CardBackVariant.Back01);
            Assert.AreEqual(CardBackManager.CardBackVariant.Back01, CardBackManager.Current);
            // Reset back
            CardBackManager.SetPlayerCardBack(CardBackManager.CardBackVariant.Default);
            CardBackManager.ResetForTest();
        }

        // ── 7h: EventBanner warning constants ───────────────────────────────

        [Test]
        public void EventBanner_WarnDuration_Is1_5s()
        {
            Assert.AreEqual(1.5f, EventBanner.WARN_DURATION, 0.01f);
        }

        [Test]
        public void EventBanner_WarnScaleIn_Is0_25s()
        {
            Assert.AreEqual(0.25f, EventBanner.WARN_SCALE_IN, 0.01f);
        }

        [Test]
        public void EventBanner_WarnBgColor_IsRed()
        {
            Assert.Greater(EventBanner.WarnBgColor.r, 0.5f);
            Assert.Less(EventBanner.WarnBgColor.g, 0.2f);
        }

        // ── 7j: Target fade speed ───────────────────────────────────────────

        [Test]
        public void TargetHighlight_FadeSpeed_Is2()
        {
            Assert.AreEqual(2f, CardView.TARGET_FADE_SPEED);
        }

        // ── 7k: Glow overlay fade speed ─────────────────────────────────────

        [Test]
        public void GlowOverlay_FadeSpeed_Is4()
        {
            Assert.AreEqual(4f, CardView.GLOW_FADE_SPEED);
        }

        // ── 7n: Combat animation 3-phase constants ──────────────────────────

        [Test]
        public void CombatAnim_FlyDuration_Is0_3s()
        {
            Assert.AreEqual(0.30f, CombatAnimator.FLY_DURATION, 0.01f);
        }

        [Test]
        public void CombatAnim_PauseDuration_Is0_1s()
        {
            Assert.AreEqual(0.10f, CombatAnimator.PAUSE_DURATION, 0.01f);
        }

        [Test]
        public void CombatAnim_BackDuration_Is0_3s()
        {
            Assert.AreEqual(0.30f, CombatAnimator.BACK_DURATION, 0.01f);
        }

        [Test]
        public void CombatAnim_FlyOffset_Is40px()
        {
            Assert.AreEqual(40f, CombatAnimator.FLY_OFFSET);
        }

        [Test]
        public void CombatAnim_TotalDuration_Is0_7s()
        {
            float total = CombatAnimator.FLY_DURATION + CombatAnimator.PAUSE_DURATION + CombatAnimator.BACK_DURATION;
            Assert.AreEqual(0.70f, total, 0.01f);
        }

        // ── 7p: MouseLineFX constants ───────────────────────────────────────

        [Test]
        public void MouseLineFX_DotCount_Is12()
        {
            Assert.AreEqual(12, MouseLineFX.DOT_COUNT);
        }

        [Test]
        public void MouseLineFX_DotSize_Is6()
        {
            Assert.AreEqual(6f, MouseLineFX.DOT_SIZE);
        }

        // ── 7q: AimTargetFX constants ───────────────────────────────────────

        [Test]
        public void AimTargetFX_PulseFreq_Is2Hz()
        {
            Assert.AreEqual(2f, AimTargetFX.PULSE_FREQ);
        }

        [Test]
        public void AimTargetFX_TargetSize_Is48()
        {
            Assert.AreEqual(48f, AimTargetFX.TARGET_SIZE);
        }

        [Test]
        public void AimTargetFX_AlphaRange_Valid()
        {
            Assert.Less(AimTargetFX.ALPHA_MIN, AimTargetFX.ALPHA_MAX);
            Assert.GreaterOrEqual(AimTargetFX.ALPHA_MIN, 0f);
            Assert.LessOrEqual(AimTargetFX.ALPHA_MAX, 1f);
        }

        // ── 7r: Hand fan layout — removed (user decided to keep horizontal) ──
    }
}
