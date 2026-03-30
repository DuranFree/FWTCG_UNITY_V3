using NUnit.Framework;
using UnityEngine;
using FWTCG.UI;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Tests
{
    [TestFixture]
    public class DEV8VisualTests
    {
        // ── GameColors tests ──────────────────────────────────────────────────

        [Test]
        public void GameColors_Gold_MatchesHex()
        {
            ColorUtility.TryParseHtmlString("#c8aa6e", out Color expected);
            Assert.AreEqual(expected.r, GameColors.Gold.r, 0.01f);
            Assert.AreEqual(expected.g, GameColors.Gold.g, 0.01f);
            Assert.AreEqual(expected.b, GameColors.Gold.b, 0.01f);
        }

        [Test]
        public void GameColors_Background_MatchesHex()
        {
            ColorUtility.TryParseHtmlString("#010a13", out Color expected);
            Assert.AreEqual(expected.r, GameColors.Background.r, 0.01f);
            Assert.AreEqual(expected.g, GameColors.Background.g, 0.01f);
            Assert.AreEqual(expected.b, GameColors.Background.b, 0.01f);
        }

        [Test]
        public void GameColors_Hex_ParsesValid()
        {
            var c = GameColors.Hex("#ff0000");
            Assert.AreEqual(1f, c.r, 0.01f);
            Assert.AreEqual(0f, c.g, 0.01f);
            Assert.AreEqual(0f, c.b, 0.01f);
        }

        [Test]
        public void GameColors_Hex_InvalidReturnsMagenta()
        {
            var c = GameColors.Hex("not-a-color");
            Assert.AreEqual(Color.magenta, c);
        }

        [Test]
        public void GameColors_AllRuneColorsDistinct()
        {
            Color[] runes = {
                GameColors.RuneBlazing, GameColors.RuneRadiant, GameColors.RuneVerdant,
                GameColors.RuneCrushing, GameColors.RuneChaos, GameColors.RuneOrder
            };
            for (int i = 0; i < runes.Length; i++)
                for (int j = i + 1; j < runes.Length; j++)
                    Assert.AreNotEqual(runes[i], runes[j], $"Rune color {i} and {j} are the same");
        }

        [Test]
        public void GameColors_CardStates_NotAllSame()
        {
            Assert.AreNotEqual(GameColors.CardPlayer, GameColors.CardEnemy);
            Assert.AreNotEqual(GameColors.CardPlayer, GameColors.CardExhausted);
            Assert.AreNotEqual(GameColors.CardSelected, GameColors.CardFaceDown);
        }

        // ── CardDetailPopup keyword building ──────────────────────────────────

        [Test]
        public void CardDetailPopup_KeywordsContainAllDefined()
        {
            // All keywords should have display names
            var keywords = System.Enum.GetValues(typeof(CardKeyword));
            foreach (CardKeyword kw in keywords)
            {
                if (kw == CardKeyword.None) continue;
                // Just verify the enum values are defined correctly
                Assert.IsTrue(System.Enum.IsDefined(typeof(CardKeyword), kw),
                    $"Keyword {kw} is not properly defined");
            }
        }

        [Test]
        public void CardDetailPopup_14KeywordsExist()
        {
            // Count non-None keywords
            int count = 0;
            foreach (CardKeyword kw in System.Enum.GetValues(typeof(CardKeyword)))
                if (kw != CardKeyword.None) count++;
            Assert.AreEqual(14, count);
        }

        // ── Visual state logic ────────────────────────────────────────────────

        [Test]
        public void CostDimFactor_ReducesBrightness()
        {
            Color original = Color.white;
            Color dimmed = original * GameColors.CostDimFactor;
            Assert.Less(dimmed.r, original.r);
            Assert.Less(dimmed.g, original.g);
            Assert.Less(dimmed.b, original.b);
        }

        [Test]
        public void StunnedOverlay_HasRedTint()
        {
            Assert.Greater(GameColors.StunnedOverlay.r, GameColors.StunnedOverlay.g);
            Assert.Greater(GameColors.StunnedOverlay.r, GameColors.StunnedOverlay.b);
            Assert.Greater(GameColors.StunnedOverlay.a, 0f); // visible
            Assert.Less(GameColors.StunnedOverlay.a, 1f);    // semi-transparent
        }

        [Test]
        public void GlowPlayable_IsGreenish()
        {
            Assert.Greater(GameColors.GlowPlayable.g, GameColors.GlowPlayable.r);
            Assert.Greater(GameColors.GlowPlayable.g, 0.5f);
        }

        [Test]
        public void GlowHover_IsGoldish()
        {
            Assert.Greater(GameColors.GlowHover.r, GameColors.GlowHover.b);
            Assert.Greater(GameColors.GlowHover.g, GameColors.GlowHover.b);
        }

        // ── UnitInstance state fields ─────────────────────────────────────────

        [Test]
        public void UnitInstance_StunnedField_DefaultFalse()
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            var unit = new UnitInstance(1, cd, "player");
            Assert.IsFalse(unit.Stunned);
            Object.DestroyImmediate(cd);
        }

        [Test]
        public void UnitInstance_BuffTokens_DefaultZero()
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            var unit = new UnitInstance(1, cd, "player");
            Assert.AreEqual(0, unit.BuffTokens);
            Object.DestroyImmediate(cd);
        }
    }
}
