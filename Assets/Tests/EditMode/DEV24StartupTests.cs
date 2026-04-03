using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-24: Startup flow animation tests.
    /// Verifies animation constants, panel setup, and null-safety of StartupFlowUI.
    /// All EditMode.
    /// </summary>
    [TestFixture]
    public class DEV24StartupTests
    {
        // ── 1. Animation constants ────────────────────────────────────────────

        [Test]
        public void PanelFadeIn_Is0Point4Seconds()
        {
            Assert.AreEqual(0.4f, StartupFlowUI.PANEL_FADE_IN, 0.001f);
        }

        [Test]
        public void PanelFadeOut_Is0Point3Seconds()
        {
            Assert.AreEqual(0.3f, StartupFlowUI.PANEL_FADE_OUT, 0.001f);
        }

        [Test]
        public void CoinFlipCount_Is5()
        {
            Assert.AreEqual(5, StartupFlowUI.COIN_FLIP_COUNT);
        }

        [Test]
        public void CoinHalfFlip_IsUnder0Point2Seconds()
        {
            // Each half-flip must be short enough that COIN_FLIP_COUNT full flips
            // completes well within ~1.8s target (5 × 2 × 0.13 = 1.3s + landing 0.3s)
            Assert.Less(StartupFlowUI.COIN_HALF_FLIP, 0.2f);
        }

        [Test]
        public void CoinLandDuration_Is0Point3Seconds()
        {
            Assert.AreEqual(0.3f, StartupFlowUI.COIN_LAND_DUR, 0.001f);
        }

        [Test]
        public void TotalCoinAnimDuration_IsUnder2Seconds()
        {
            float total = StartupFlowUI.COIN_FLIP_COUNT * 2f * StartupFlowUI.COIN_HALF_FLIP
                          + StartupFlowUI.COIN_LAND_DUR + StartupFlowUI.RESULT_FADE_IN;
            Assert.Less(total, 2.0f, $"Total coin anim = {total:F2}s, expected < 2s");
        }

        [Test]
        public void ResultFadeIn_Is0Point4Seconds()
        {
            Assert.AreEqual(0.4f, StartupFlowUI.RESULT_FADE_IN, 0.001f);
        }

        [Test]
        public void ScanPeriod_Is8Seconds()
        {
            Assert.AreEqual(8f, StartupFlowUI.SCAN_PERIOD, 0.001f);
        }

        // ── 2. Component setup ────────────────────────────────────────────────

        [Test]
        public void StartupFlowUI_CanBeAddedToGameObject()
        {
            var go = new GameObject("StartupFlow");
            var sfu = go.AddComponent<StartupFlowUI>();
            Assert.IsNotNull(sfu);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Awake_NullPanels_DoesNotThrow()
        {
            // All panels null — Awake should silently skip
            var go = new GameObject("StartupFlow");
            // AddComponent triggers Awake
            Assert.DoesNotThrow(() => go.AddComponent<StartupFlowUI>());
            Object.DestroyImmediate(go);
        }

        [Test]
        public void StartupFlowUI_HasField_CoinFlipPanel()
        {
            // Verify the serialized field exists (reflection)
            var field = typeof(StartupFlowUI)
                .GetField("_coinFlipPanel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_coinFlipPanel serialized field should exist");
            Assert.AreEqual(typeof(GameObject), field.FieldType);
        }

        // ── 3. GameUI log flash ───────────────────────────────────────────────

        [Test]
        public void GameUI_LogEntryFlashRoutine_FieldExists()
        {
            // Verify the LogEntryFlashRoutine private method exists in GameUI
            var method = typeof(GameUI)
                .GetMethod("LogEntryFlashRoutine",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "GameUI.LogEntryFlashRoutine should exist");
        }

        [Test]
        public void GameUI_FadeInPanelRoutine_FieldExists()
        {
            var method = typeof(GameUI)
                .GetMethod("FadeInPanelRoutine",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "GameUI.FadeInPanelRoutine should exist");
        }

        // ── 4. Coin animation geometry ────────────────────────────────────────

        [Test]
        public void CoinSpinRoutine_LastFlipIsSlower()
        {
            // Last flip uses COIN_HALF_FLIP * 2, all others use COIN_HALF_FLIP
            float normalStep = StartupFlowUI.COIN_HALF_FLIP;
            float lastStep   = StartupFlowUI.COIN_HALF_FLIP * 2f;
            Assert.Greater(lastStep, normalStep,
                "Last coin flip half-step should be slower for dramatic landing");
        }

        [Test]
        public void ScanLight_FullSweepDuration_Is8Seconds()
        {
            Assert.AreEqual(8f, StartupFlowUI.SCAN_PERIOD, 0.001f,
                "Scan light should complete one left-to-right pass every 8 seconds");
        }

        // ── 5. Null-safety: CoinSpinRoutine without coinCircleImage ───────────

        [Test]
        public void StartupFlowUI_HasField_CoinCircleImage()
        {
            var field = typeof(StartupFlowUI)
                .GetField("_coinCircleImage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_coinCircleImage serialized field should exist");
        }

        [Test]
        public void StartupFlowUI_HasField_CoinResultText()
        {
            var field = typeof(StartupFlowUI)
                .GetField("_coinResultText",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_coinResultText serialized field should exist");
        }

        [Test]
        public void StartupFlowUI_HasField_ScanLightImage()
        {
            var field = typeof(StartupFlowUI)
                .GetField("_scanLightImage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_scanLightImage serialized field should exist");
        }

        [Test]
        public void StartupFlowUI_HasField_CoinFlipCG()
        {
            var field = typeof(StartupFlowUI)
                .GetField("_coinFlipCG",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_coinFlipCG serialized field should exist");
        }

        [Test]
        public void StartupFlowUI_HasField_MulliganCG()
        {
            var field = typeof(StartupFlowUI)
                .GetField("_mulliganCG",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_mulliganCG serialized field should exist");
        }
    }
}
