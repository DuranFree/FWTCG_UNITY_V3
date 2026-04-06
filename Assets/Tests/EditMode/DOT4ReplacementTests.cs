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
    /// DOT-4: Tests for 3 game logic file DOTween replacements.
    /// Verifies coroutine → DOTween migration preserved behavior and cleaned up properly.
    /// </summary>
    [TestFixture]
    public class DOT4ReplacementTests : DOTweenTestBase
    {
        // ═══════════════════════════════════════════════════════════════════════
        // CombatAnimator — structural verification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CombatAnimator_HasNoFlyAndReturnRoutine()
        {
            var method = typeof(CombatAnimator).GetMethod("FlyAndReturnRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "FlyAndReturnRoutine coroutine should be removed");
        }

        [Test]
        public void CombatAnimator_HasFlyAndReturnMethod()
        {
            var method = typeof(CombatAnimator).GetMethod("FlyAndReturn",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "FlyAndReturn tween method must exist");
        }

        [Test]
        public void CombatAnimator_HasNoCoroutineShockwaveHandles()
        {
            var sw1 = typeof(CombatAnimator).GetField("_sw1Routine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(sw1, "_sw1Routine coroutine handle should be removed");

            var sw2 = typeof(CombatAnimator).GetField("_sw2Routine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(sw2, "_sw2Routine coroutine handle should be removed");
        }

        [Test]
        public void CombatAnimator_HasTweenShockwaveHandles()
        {
            var sw1 = typeof(CombatAnimator).GetField("_sw1Tween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(sw1, "_sw1Tween must exist");
            Assert.AreEqual(typeof(Tween), sw1.FieldType);

            var sw2 = typeof(CombatAnimator).GetField("_sw2Tween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(sw2, "_sw2Tween must exist");
            Assert.AreEqual(typeof(Tween), sw2.FieldType);
        }

        [Test]
        public void CombatAnimator_PlayShockwave_ReturnsTween()
        {
            var method = typeof(CombatAnimator).GetMethod("PlayShockwave",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "PlayShockwave must exist");
            Assert.AreEqual(typeof(Tween), method.ReturnType, "PlayShockwave must return Tween");
        }

        [Test]
        public void CombatAnimator_ConstantsUnchanged()
        {
            Assert.AreEqual(0.30f, CombatAnimator.FLY_DURATION, 0.001f);
            Assert.AreEqual(0.10f, CombatAnimator.PAUSE_DURATION, 0.001f);
            Assert.AreEqual(0.30f, CombatAnimator.BACK_DURATION, 0.001f);
            Assert.AreEqual(40f, CombatAnimator.FLY_OFFSET, 0.001f);
        }

        [Test]
        public void CombatAnimator_PlayShockwave_NullSafe()
        {
            var go = new GameObject("TestCA");
            var ca = go.AddComponent<CombatAnimator>();
            var method = typeof(CombatAnimator).GetMethod("PlayShockwave",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var result = method.Invoke(ca, new object[] { null });
            Assert.IsNull(result, "PlayShockwave(null) should return null");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CombatAnimator_PlayShockwave_CreatesTween()
        {
            var go = new GameObject("TestCA");
            var ca = go.AddComponent<CombatAnimator>();

            // Create a test shockwave image
            var swGO = new GameObject("TestSW");
            swGO.transform.SetParent(go.transform);
            var img = swGO.AddComponent<Image>();
            swGO.AddComponent<RectTransform>();

            var method = typeof(CombatAnimator).GetMethod("PlayShockwave",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var result = method.Invoke(ca, new object[] { img });
            Assert.IsNotNull(result, "PlayShockwave should return a tween for valid image");

            CompleteAll();
            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SpellVFX — structural verification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SpellVFX_BurstParticles_IsNotCoroutine()
        {
            var method = typeof(SpellVFX).GetMethod("BurstParticles",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "BurstParticles must exist");
            Assert.AreEqual(typeof(void), method.ReturnType,
                "BurstParticles should return void (not IEnumerator)");
        }

        [Test]
        public void SpellVFX_BurstConstants()
        {
            Assert.AreEqual(0.6f, SpellVFX.BURST_DURATION, 0.001f);
            Assert.AreEqual(110f, SpellVFX.BURST_RADIUS, 0.001f);
            Assert.AreEqual(6f, SpellVFX.BURST_START_SIZE, 0.001f);
            Assert.AreEqual(2f, SpellVFX.BURST_END_SIZE, 0.001f);
        }

        [Test]
        public void SpellVFX_LegendFlame_StillCoroutine()
        {
            // LegendFlame has per-frame velocity + respawn, kept as coroutine
            var method = typeof(SpellVFX).GetMethod("LegendFlame",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "LegendFlame must still exist");
            Assert.AreEqual(typeof(System.Collections.IEnumerator), method.ReturnType,
                "LegendFlame should remain a coroutine (per-frame simulation)");
        }

        [Test]
        public void SpellVFX_ProjectileThenFXRoutine_StillCoroutine()
        {
            var method = typeof(SpellVFX).GetMethod("ProjectileThenFXRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "ProjectileThenFXRoutine must still exist");
            Assert.AreEqual(typeof(System.Collections.IEnumerator), method.ReturnType,
                "ProjectileThenFXRoutine should remain a coroutine (game logic flow)");
        }

        [Test]
        public void SpellVFX_DelayedCardPlayFX_StillCoroutine()
        {
            var method = typeof(SpellVFX).GetMethod("DelayedCardPlayFX",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "DelayedCardPlayFX must still exist");
            Assert.AreEqual(typeof(System.Collections.IEnumerator), method.ReturnType,
                "DelayedCardPlayFX should remain a coroutine (game logic flow)");
        }

        [Test]
        public void SpellVFX_ExistingConstants_Unchanged()
        {
            Assert.AreEqual(0.4f, SpellVFX.PROJECTILE_DURATION, 0.001f);
            Assert.AreEqual(-300f, SpellVFX.PLAYER_HAND_Y, 0.001f);
            Assert.AreEqual(340f, SpellVFX.AI_ORIGIN_Y, 0.001f);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CardDragHandler — structural verification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CardDragHandler_HasNoCancelReturnRoutine()
        {
            var method = typeof(CardDragHandler).GetMethod("CancelReturnRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "CancelReturnRoutine coroutine should be removed");
        }

        [Test]
        public void CardDragHandler_HasCancelReturnTween()
        {
            var method = typeof(CardDragHandler).GetMethod("CancelReturnTween",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "CancelReturnTween must exist");
        }

        [Test]
        public void CardDragHandler_HasNoCoroutineCancelHandle()
        {
            var field = typeof(CardDragHandler).GetField("_cancelReturnCoroutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field, "_cancelReturnCoroutine should be removed");
        }

        [Test]
        public void CardDragHandler_HasTweenCancelHandle()
        {
            var field = typeof(CardDragHandler).GetField("_cancelReturnSeq",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_cancelReturnSeq must exist");
            Assert.AreEqual(typeof(Sequence), field.FieldType);
        }

        [Test]
        public void CardDragHandler_CancelConstants()
        {
            Assert.AreEqual(0.42f, CardDragHandler.CANCEL_RETURN_DURATION, 0.001f);
            Assert.AreEqual(0.05f, CardDragHandler.CANCEL_STAGGER_DELAY, 0.001f);
        }

        [Test]
        public void CardDragHandler_HasNoAnimateDropCard()
        {
            var method = typeof(CardDragHandler).GetMethod("AnimateDropCard",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(method, "AnimateDropCard static helper should be removed");
        }

        [Test]
        public void CardDragHandler_HasNoEaseHelpers()
        {
            var easeOut = typeof(CardDragHandler).GetMethod("EaseOutQuad",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(easeOut, "EaseOutQuad should be removed (using DOTween Ease)");

            var easeIn = typeof(CardDragHandler).GetMethod("EaseInQuad",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(easeIn, "EaseInQuad should be removed (using DOTween Ease)");

            var smoothstep = typeof(CardDragHandler).GetMethod("Smoothstep",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(smoothstep, "Smoothstep should be removed (using DOTween Ease)");
        }

        [Test]
        public void CardDragHandler_ClusterFollowRoutine_StillCoroutine()
        {
            // ClusterFollowRoutine has per-frame mouse tracking, kept as coroutine
            var method = typeof(CardDragHandler).GetMethod("ClusterFollowRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "ClusterFollowRoutine must still exist");
            Assert.AreEqual(typeof(System.Collections.IEnumerator), method.ReturnType,
                "ClusterFollowRoutine should remain a coroutine (per-frame mouse tracking)");
        }

        [Test]
        public void CardDragHandler_ExistingDragConstants_Unchanged()
        {
            Assert.AreEqual(10f, CardDragHandler.DRAG_ROTATE_MAX, 0.001f);
            Assert.AreEqual(4f, CardDragHandler.DRAG_ROTATE_SPEED, 0.001f);
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
        public void TweenHelper_KillAllOn_NullSafe()
        {
            Assert.DoesNotThrow(() => TweenHelper.KillAllOn(null));
        }
    }
}
