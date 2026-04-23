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
    /// DOT-7: Tests for CardView.cs DOTween replacements (17 coroutines → DOTween)
    /// + AnimMatFX.cs deletion + cleanup.
    /// </summary>
    [TestFixture]
    public class DOT7ReplacementTests : DOTweenTestBase
    {
        private const BindingFlags PRIV = BindingFlags.NonPublic | BindingFlags.Instance;

        // ═══════════════════════════════════════════════════════════════════════
        // Old coroutine method removal — IEnumerator methods should be gone
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void CardView_NoLiftFloatRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("LiftFloatRoutine", PRIV), "LiftFloatRoutine should be removed"); }

        [Test] public void CardView_NoReturnToRestRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("ReturnToRestRoutine", PRIV), "ReturnToRestRoutine should be removed"); }

        [Test] public void CardView_NoBreathGlowRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("BreathGlowRoutine", PRIV), "BreathGlowRoutine should be removed"); }

        [Test] public void CardView_NoStunPulseRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("StunPulseRoutine", PRIV), "StunPulseRoutine should be removed"); }

        [Test] public void CardView_NoFlashRedRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("FlashRedRoutine", PRIV), "FlashRedRoutine should be removed"); }

        [Test] public void CardView_NoShakeRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("ShakeRoutine", PRIV), "ShakeRoutine should be removed"); }

        [Test] public void CardView_NoBadgeScaleRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("BadgeScaleRoutine", PRIV), "BadgeScaleRoutine should be removed"); }

        [Test] public void CardView_NoTargetFadeOutRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("TargetFadeOutRoutine", PRIV), "TargetFadeOutRoutine should be removed"); }

        [Test] public void CardView_NoTargetPulseRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("TargetPulseRoutine", PRIV), "TargetPulseRoutine should be removed"); }

        [Test] public void CardView_NoOrbitRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("OrbitRoutine", PRIV), "OrbitRoutine should be removed"); }

        [Test] public void CardView_NoHeroAuraPulseRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("HeroAuraPulseRoutine", PRIV), "HeroAuraPulseRoutine should be removed"); }

        [Test] public void CardView_NoFoilSweepRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("FoilSweepRoutine", PRIV), "FoilSweepRoutine should be removed"); }

        [Test] public void CardView_NoFadeShadowIn()
        { Assert.IsNull(typeof(CardView).GetMethod("FadeShadowIn", PRIV), "FadeShadowIn should be removed"); }

        // EnterAnimRoutine replaced by EnterAnimSetup (still IEnumerator for 1-frame wait)
        [Test] public void CardView_NoEnterAnimRoutine()
        { Assert.IsNull(typeof(CardView).GetMethod("EnterAnimRoutine", PRIV), "EnterAnimRoutine should be removed"); }

        // AnimateSparkDot is now void, not IEnumerator
        [Test] public void CardView_AnimateSparkDot_IsVoid()
        {
            var m = typeof(CardView).GetMethod("AnimateSparkDot", PRIV);
            Assert.IsNotNull(m, "AnimateSparkDot method should exist");
            Assert.AreEqual(typeof(void), m.ReturnType, "AnimateSparkDot should return void (not IEnumerator)");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Old Coroutine field removal — should be replaced by Tween/Sequence
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void CardView_StunPulse_IsTween()
        { AssertFieldType<Tween>("_stunPulse"); }

        [Test] public void CardView_Shake_IsTween()
        { AssertFieldType<Tween>("_shake"); }

        [Test] public void CardView_Flash_IsTween()
        { AssertFieldType<Tween>("_flash"); }

        [Test] public void CardView_DeathSeq_IsSequence()
        { AssertFieldType<Sequence>("_deathSeq"); }

        [Test] public void CardView_AtkBreath_IsTween()
        { AssertFieldType<Tween>("_atkBreath"); }

        [Test] public void CardView_CostBreath_IsTween()
        { AssertFieldType<Tween>("_costBreath"); }

        [Test] public void CardView_SchBreath_IsTween()
        { AssertFieldType<Tween>("_schBreath"); }

        [Test] public void CardView_LiftFloat_IsTween()
        { AssertFieldType<Tween>("_liftFloat"); }

        [Test] public void CardView_ReturnToRest_IsTween()
        { AssertFieldType<Tween>("_returnToRest"); }

        [Test] public void CardView_TargetPulse_IsTween()
        { AssertFieldType<Tween>("_targetPulse"); }

        [Test] public void CardView_TargetFadeOut_IsTween()
        { AssertFieldType<Tween>("_targetFadeOut"); }

        [Test] public void CardView_OrbitTween_IsTween()
        { AssertFieldType<Tween>("_orbitTween"); }

        [Test] public void CardView_HeroAuraPulse_IsTween()
        { AssertFieldType<Tween>("_heroAuraPulse"); }

        [Test] public void CardView_FoilSweep_IsTween()
        { AssertFieldType<Tween>("_foilSweep"); }

        [Test] public void CardView_EnterAnimSeq_IsSequence()
        { AssertFieldType<Sequence>("_enterAnimSeq"); }

        // ═══════════════════════════════════════════════════════════════════════
        // New DOTween methods exist
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void CardView_HasStartLiftFloat()
        { Assert.IsNotNull(typeof(CardView).GetMethod("StartLiftFloat", PRIV), "StartLiftFloat should exist"); }

        [Test] public void CardView_HasStartReturnToRest()
        { Assert.IsNotNull(typeof(CardView).GetMethod("StartReturnToRest", PRIV), "StartReturnToRest should exist"); }

        [Test] public void CardView_HasCreateBreathGlowTween()
        { Assert.IsNotNull(typeof(CardView).GetMethod("CreateBreathGlowTween", PRIV), "CreateBreathGlowTween should exist"); }

        [Test] public void CardView_HasCreateStunPulseTween()
        { Assert.IsNotNull(typeof(CardView).GetMethod("CreateStunPulseTween", PRIV), "CreateStunPulseTween should exist"); }

        [Test] public void CardView_HasStartFoilSweep()
        { Assert.IsNotNull(typeof(CardView).GetMethod("StartFoilSweep", PRIV), "StartFoilSweep should exist"); }

        [Test] public void CardView_HasStartTargetPulse()
        { Assert.IsNotNull(typeof(CardView).GetMethod("StartTargetPulse", PRIV), "StartTargetPulse should exist"); }

        [Test] public void CardView_HasStartTargetFadeOut()
        { Assert.IsNotNull(typeof(CardView).GetMethod("StartTargetFadeOut", PRIV), "StartTargetFadeOut should exist"); }

        // ═══════════════════════════════════════════════════════════════════════
        // Constants verification
        // ═══════════════════════════════════════════════════════════════════════

        [Test] public void CardView_BreathGlowConstants()
        {
            Assert.AreEqual(0.08f, CardView.BREATH_GLOW_MIN, 0.001f);
            Assert.AreEqual(0.45f, CardView.BREATH_GLOW_MAX, 0.001f);
            Assert.AreEqual(1.4f, CardView.BREATH_GLOW_SPEED, 0.001f);
        }

        [Test] public void CardView_StunPulseConstants()
        {
            Assert.AreEqual(2f, CardView.STUN_PULSE_SPEED, 0.001f);
            Assert.AreEqual(0.15f, CardView.STUN_PULSE_MIN, 0.001f);
            Assert.AreEqual(0.45f, CardView.STUN_PULSE_MAX, 0.001f);
        }

        [Test] public void CardView_FlashRedConstants()
        {
            Assert.AreEqual(0.12f, CardView.FLASH_RED_HOLD, 0.001f);
            Assert.AreEqual(0.35f, CardView.FLASH_RED_FADE, 0.001f);
        }

        [Test] public void CardView_ShakeConstants()
        {
            Assert.AreEqual(10f, CardView.SHAKE_STRENGTH, 0.001f);
            Assert.AreEqual(0.08f, CardView.SHAKE_DURATION, 0.001f); // DEV-31 cleanup: source is 0.08f
            Assert.AreEqual(12, CardView.SHAKE_VIBRATO); // DEV-31 cleanup: source is 12
        }

        [Test] public void CardView_TargetPulseConstants()
        {
            Assert.AreEqual(1.2f, CardView.TARGET_PULSE_PERIOD, 0.001f);
            Assert.AreEqual(0.3f, CardView.TARGET_PULSE_MIN, 0.001f);
            Assert.AreEqual(0.85f, CardView.TARGET_PULSE_MAX, 0.001f);
        }

        [Test] public void CardView_OrbitConstants()
        {
            Assert.AreEqual(60f, CardView.ORBIT_RADIUS, 0.001f);
            Assert.AreEqual(6f, CardView.ORBIT_PERIOD, 0.001f);
        }

        [Test] public void CardView_HeroAuraConstants()
        {
            Assert.AreEqual(4f, CardView.HERO_AURA_PERIOD, 0.001f);
            Assert.AreEqual(0.25f, CardView.HERO_AURA_MIN, 0.001f);
            Assert.AreEqual(0.60f, CardView.HERO_AURA_MAX, 0.001f);
        }

        [Test] public void CardView_EnterAnimConstants()
        {
            Assert.AreEqual(0.42f, CardView.ENTER_ANIM_DURATION, 0.001f);
            Assert.AreEqual(0.82f, CardView.ENTER_ANIM_START_SCALE, 0.001f);
            Assert.AreEqual(-30f, CardView.ENTER_ANIM_Y_OFFSET, 0.001f);
        }

        [Test] public void CardView_FoilSweepConstants()
        { Assert.AreEqual(0.8f, CardView.FOIL_SWEEP_DURATION, 0.001f); }

        [Test] public void CardView_SparkConstants()
        {
            Assert.AreEqual(0.6f, CardView.SPARK_INTERVAL, 0.001f);
            Assert.AreEqual(0.5f, CardView.SPARK_DURATION, 0.001f);
            Assert.AreEqual(18f, CardView.SPARK_FLOAT_DIST, 0.001f);
            Assert.AreEqual(0.85f, CardView.SPARK_PEAK_ALPHA, 0.001f);
        }

        [Test] public void CardView_DeathConstants()
        {
            Assert.AreEqual(0.6f, CardView.DEATH_PHASE_A_DISSOLVE, 0.001f);
            Assert.AreEqual(0.30f, CardView.DEATH_PHASE_A_FALLBACK, 0.001f);
            Assert.AreEqual(0.50f, CardView.DEATH_PHASE_B, 0.001f);
            Assert.AreEqual(0.6f, CardView.DEATH_GHOST_START_SCALE, 0.001f);
            Assert.AreEqual(0.15f, CardView.DEATH_GHOST_END_SCALE, 0.001f);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Null-safety: FlashRed and Shake with null components
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CardView_FlashRed_NullCardBg_DoesNotThrow()
        {
            var go = new GameObject("TestCV");
            var cv = go.AddComponent<CardView>();
            Assert.DoesNotThrow(() => cv.FlashRed());
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CardView_Shake_DoesNotThrow()
        {
            var go = new GameObject("TestCV");
            go.AddComponent<RectTransform>();
            var cv = go.AddComponent<CardView>();
            Assert.DoesNotThrow(() => cv.Shake());
            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AnimMatFX.cs deletion
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void AnimMatFX_TypeDoesNotExist()
        {
            // DOT-7: AnimMatFX.cs has been deleted; type should not resolve
            var type = System.Type.GetType("FWTCG.FX.AnimMatFX, Assembly-CSharp");
            Assert.IsNull(type, "AnimMatFX type should be deleted in DOT-7");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Coroutine field type scan — no Coroutine fields except allowed ones
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CardView_NoUnexpectedCoroutineFields()
        {
            // Allowed coroutine fields (flow control, not animation):
            var allowed = new System.Collections.Generic.HashSet<string>
            {
                "_playableSpark",      // periodic spawning loop (WaitForSeconds)
                "_enterAnimSetup",     // 1-frame setup before DOTween sequence
            };

            var fields = typeof(CardView).GetFields(PRIV);
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(Coroutine) && !allowed.Contains(f.Name))
                    Assert.Fail($"Unexpected Coroutine field: CardView.{f.Name} — should be Tween/Sequence");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TweenHelper null-safety regression
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void TweenHelper_KillSafe_NullTween_DoesNotThrow()
        {
            Tween t = null;
            Assert.DoesNotThrow(() => TweenHelper.KillSafe(ref t));
            Assert.IsNull(t);
        }

        [Test]
        public void TweenHelper_KillSafe_NullSequence_DoesNotThrow()
        {
            Sequence s = null;
            Assert.DoesNotThrow(() => TweenHelper.KillSafe(ref s));
            Assert.IsNull(s);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Badge scale tweens field check
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CardView_BadgeScaleTweens_IsDictionary()
        {
            var field = typeof(CardView).GetField("_badgeScaleTweens", PRIV);
            Assert.IsNotNull(field, "_badgeScaleTweens field should exist");
            Assert.IsTrue(field.FieldType.Name.Contains("Dictionary"), "_badgeScaleTweens should be a Dictionary");
        }

        // Helper
        private void AssertFieldType<T>(string fieldName)
        {
            var field = typeof(CardView).GetField(fieldName, PRIV);
            Assert.IsNotNull(field, $"CardView.{fieldName} field should exist");
            Assert.AreEqual(typeof(T), field.FieldType, $"CardView.{fieldName} should be {typeof(T).Name}");
        }
    }
}
