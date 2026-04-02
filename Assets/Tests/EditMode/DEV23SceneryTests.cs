using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-23: Scenery / decorative ambient effects tests.
    /// All EditMode — verifies constants, initialization, and null-safety.
    /// </summary>
    [TestFixture]
    public class DEV23SceneryTests
    {
        // ── 1. Duration constants ─────────────────────────────────────────────

        [Test]
        public void SpinOuterDuration_Is20Seconds()
        {
            Assert.AreEqual(20f, SceneryUI.SPIN_OUTER_DURATION, 0.001f);
        }

        [Test]
        public void SpinInnerDuration_Is12Seconds()
        {
            Assert.AreEqual(12f, SceneryUI.SPIN_INNER_DURATION, 0.001f);
        }

        [Test]
        public void SigilOuterDuration_Is30Seconds()
        {
            Assert.AreEqual(30f, SceneryUI.SIGIL_OUTER_DURATION, 0.001f);
        }

        [Test]
        public void SigilInnerDuration_Is20Seconds()
        {
            Assert.AreEqual(20f, SceneryUI.SIGIL_INNER_DURATION, 0.001f);
        }

        [Test]
        public void DividerOrbDuration_Is3Point5Seconds()
        {
            Assert.AreEqual(3.5f, SceneryUI.DIVIDER_ORB_DURATION, 0.001f);
        }

        [Test]
        public void CornerGemDuration_Is4Seconds()
        {
            Assert.AreEqual(4f, SceneryUI.CORNER_GEM_DURATION, 0.001f);
        }

        [Test]
        public void LegendGlowDuration_Is5Seconds()
        {
            Assert.AreEqual(5f, SceneryUI.LEGEND_GLOW_DURATION, 0.001f);
        }

        // ── 2. Alpha range constants ──────────────────────────────────────────

        [Test]
        public void CornerGemAlphaMin_LessThan_AlphaMax()
        {
            Assert.Less(SceneryUI.CORNER_GEM_ALPHA_MIN, SceneryUI.CORNER_GEM_ALPHA_MAX);
        }

        [Test]
        public void CornerGemAlphaMax_AtMost1()
        {
            Assert.LessOrEqual(SceneryUI.CORNER_GEM_ALPHA_MAX, 1f);
        }

        [Test]
        public void LegendGlowAlphaMin_LessThan_AlphaMax()
        {
            Assert.Less(SceneryUI.LEGEND_GLOW_ALPHA_MIN, SceneryUI.LEGEND_GLOW_ALPHA_MAX);
        }

        [Test]
        public void LegendGlowAlphaMax_AtMost1()
        {
            Assert.LessOrEqual(SceneryUI.LEGEND_GLOW_ALPHA_MAX, 1f);
        }

        // ── 3. Divider orb amplitude ──────────────────────────────────────────

        [Test]
        public void DividerOrbAmplitude_IsPositive()
        {
            Assert.Greater(SceneryUI.DIVIDER_ORB_AMPLITUDE, 0f);
        }

        // ── 4. SceneryUI null-safe initialization (no refs wired) ─────────────

        [Test]
        public void SceneryUI_AddComponent_DoesNotThrow()
        {
            var go = new GameObject("SceneryHost");
            try
            {
                Assert.DoesNotThrow(() => go.AddComponent<SceneryUI>());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SceneryUI_FieldsAreNullByDefault_WhenNotWired()
        {
            var go = new GameObject("SceneryHost");
            try
            {
                var s = go.AddComponent<SceneryUI>();
                Assert.IsNull(s.spinOuter);
                Assert.IsNull(s.spinInner);
                Assert.IsNull(s.sigilOuter);
                Assert.IsNull(s.sigilInner);
                Assert.IsNull(s.dividerOrb);
                Assert.IsNull(s.cornerGems);
                Assert.IsNull(s.playerLegendGlow);
                Assert.IsNull(s.enemyLegendGlow);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SceneryUI_WiredRefs_AreRetainedAfterAssignment()
        {
            var host = new GameObject("SceneryHost");
            var imgGO = new GameObject("Img", typeof(RectTransform), typeof(Image));
            try
            {
                var s = host.AddComponent<SceneryUI>();
                var img = imgGO.GetComponent<Image>();
                s.spinOuter = img;
                s.dividerOrb = img;
                s.cornerGems = new Image[] { img };

                Assert.AreSame(img, s.spinOuter);
                Assert.AreSame(img, s.dividerOrb);
                Assert.AreEqual(1, s.cornerGems.Length);
                Assert.AreSame(img, s.cornerGems[0]);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(imgGO);
            }
        }

        // ── 5. GameColors.BlueSpell constants ────────────────────────────────

        [Test]
        public void BlueSpell_HasCorrectRGB()
        {
            Assert.AreEqual(60f / 255f,  GameColors.BlueSpell.r, 0.002f);
            Assert.AreEqual(140f / 255f, GameColors.BlueSpell.g, 0.002f);
            Assert.AreEqual(255f / 255f, GameColors.BlueSpell.b, 0.002f);
        }

        [Test]
        public void BlueSpell_IsFullyOpaque()
        {
            Assert.AreEqual(1f, GameColors.BlueSpell.a, 0.001f);
        }

        [Test]
        public void BlueSpellDim_IsTranslucent()
        {
            Assert.Less(GameColors.BlueSpellDim.a, 1f);
        }

        [Test]
        public void BlueSpellDim_HasSameRGBAsBlueSpell()
        {
            Assert.AreEqual(GameColors.BlueSpell.r, GameColors.BlueSpellDim.r, 0.002f);
            Assert.AreEqual(GameColors.BlueSpell.g, GameColors.BlueSpellDim.g, 0.002f);
            Assert.AreEqual(GameColors.BlueSpell.b, GameColors.BlueSpellDim.b, 0.002f);
        }
    }
}
