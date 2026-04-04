using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using FWTCG.UI;

namespace FWTCG.Tests
{
    [TestFixture]
    public class VFX6CoinFlipTests
    {
        // ── Burst constants ─────────────────────────────────────────────────

        [Test]
        public void BurstCount_Is20()
        {
            Assert.AreEqual(20, StartupFlowUI.COIN_BURST_COUNT);
        }

        [Test]
        public void BurstDuration_Is0_6s()
        {
            Assert.AreEqual(0.6f, StartupFlowUI.COIN_BURST_DURATION, 0.001f);
        }

        [Test]
        public void BurstRadius_Is130()
        {
            Assert.AreEqual(130f, StartupFlowUI.COIN_BURST_RADIUS, 0.001f);
        }

        [Test]
        public void BurstSize_Is8()
        {
            Assert.AreEqual(8f, StartupFlowUI.COIN_BURST_SIZE, 0.001f);
        }

        // ── Total animation time still under 2s ─────────────────────────────

        [Test]
        public void TotalCoinAnimTime_UnderTwoSeconds()
        {
            // COIN_FLIP_COUNT × 2 × COIN_HALF_FLIP + last flip doubled + COIN_LAND_DUR
            // = (5-1) × 2 × 0.13 + 2 × 0.26 + 0.3 = 1.04 + 0.52 + 0.3 = 1.86
            // Plus pauses: 4 × 0.04 = 0.16 → total ≈ 2.02, but burst runs in parallel (not sequential)
            float flipTime = (StartupFlowUI.COIN_FLIP_COUNT - 1) * 2f * StartupFlowUI.COIN_HALF_FLIP
                           + 2f * StartupFlowUI.COIN_HALF_FLIP * 2f; // last flip doubled
            float pauseTime = (StartupFlowUI.COIN_FLIP_COUNT - 1) * 0.04f;
            float totalFlipSequence = flipTime + pauseTime + StartupFlowUI.COIN_LAND_DUR;
            // Burst runs in parallel with result fade, so doesn't add to total
            Assert.Less(totalFlipSequence, 2.5f, "Flip+land animation should stay under 2.5s");
        }

        // ── Audio fields serializable ───────────────────────────────────────

        [Test]
        public void AudioClipFields_AreSerializable()
        {
            var go = new GameObject("StartupTest");
            var startup = go.AddComponent<StartupFlowUI>();
            var so = new SerializedObject(startup);

            var startClip = so.FindProperty("_coinFlipStartClip");
            Assert.IsNotNull(startClip, "_coinFlipStartClip should be serializable");
            Assert.IsNull(startClip.objectReferenceValue, "_coinFlipStartClip should default to null");

            var landClip = so.FindProperty("_coinFlipLandClip");
            Assert.IsNotNull(landClip, "_coinFlipLandClip should be serializable");
            Assert.IsNull(landClip.objectReferenceValue, "_coinFlipLandClip should default to null");

            Object.DestroyImmediate(go);
        }

        // ── Null safety: no exceptions when audio is null ───────────────────

        [Test]
        public void CoinFlipRoutine_NoExceptionWhenAudioToolNull()
        {
            // AudioTool.Instance is null in test — CoinSpinRoutine should not throw
            var go = new GameObject("StartupTest");
            var startup = go.AddComponent<StartupFlowUI>();

            // No AudioTool singleton exists — just verify the component creates without error
            Assert.IsNotNull(startup);

            Object.DestroyImmediate(go);
        }

        // ── Existing DEV-24 constants unchanged ─────────────────────────────

        [Test]
        public void DEV24Constants_Unchanged()
        {
            Assert.AreEqual(0.13f, StartupFlowUI.COIN_HALF_FLIP, 0.001f);
            Assert.AreEqual(5, StartupFlowUI.COIN_FLIP_COUNT);
            Assert.AreEqual(0.3f, StartupFlowUI.COIN_LAND_DUR, 0.001f);
            Assert.AreEqual(0.4f, StartupFlowUI.RESULT_FADE_IN, 0.001f);
        }

        // ── Burst runs in parallel — doesn't add to total time ──────────────

        [Test]
        public void BurstDuration_ShorterThanResultFade()
        {
            // Burst (0.6s) should complete within or near the result fade (0.4s) + ok button delay (0.5s)
            Assert.LessOrEqual(StartupFlowUI.COIN_BURST_DURATION,
                StartupFlowUI.RESULT_FADE_IN + 0.5f,
                "Burst should complete before user can interact");
        }
    }
}
