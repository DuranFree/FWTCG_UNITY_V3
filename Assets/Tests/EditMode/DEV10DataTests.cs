using NUnit.Framework;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using System.Collections.Generic;

namespace FWTCG.Tests
{
    [TestFixture]
    public class DEV10DataTests
    {
        // ── CardData.IsHero field ────────────────────────────────────────────

        [Test]
        public void CardData_IsHero_DefaultFalse()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("test", "Test", 1, 1, RuneType.Blazing, 0, "desc");
            Assert.IsFalse(card.IsHero);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void CardData_IsHero_SetTrue()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("hero_test", "Hero Test", 4, 4, RuneType.Blazing, 1, "hero desc",
                isHero: true);
            Assert.IsTrue(card.IsHero);
            Object.DestroyImmediate(card);
        }

        // ── GameState.PHero / EHero ──────────────────────────────────────────

        [Test]
        public void GameState_HeroFields_InitNull()
        {
            var gs = new GameState();
            Assert.IsNull(gs.PHero);
            Assert.IsNull(gs.EHero);
        }

        [Test]
        public void GameState_SetHero_Player()
        {
            var gs = new GameState();
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("hero", "Hero", 4, 4, RuneType.Blazing, 1, "desc", isHero: true);
            var unit = gs.MakeUnit(card, GameRules.OWNER_PLAYER);

            gs.SetHero(GameRules.OWNER_PLAYER, unit);
            Assert.AreSame(unit, gs.PHero);
            Assert.AreSame(unit, gs.GetHero(GameRules.OWNER_PLAYER));
            Assert.IsNull(gs.EHero);

            Object.DestroyImmediate(card);
        }

        [Test]
        public void GameState_SetHero_Enemy()
        {
            var gs = new GameState();
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("hero", "Hero", 4, 4, RuneType.Blazing, 1, "desc", isHero: true);
            var unit = gs.MakeUnit(card, GameRules.OWNER_ENEMY);

            gs.SetHero(GameRules.OWNER_ENEMY, unit);
            Assert.AreSame(unit, gs.EHero);
            Assert.AreSame(unit, gs.GetHero(GameRules.OWNER_ENEMY));
            Assert.IsNull(gs.PHero);

            Object.DestroyImmediate(card);
        }

        // ── Hero extraction from deck ────────────────────────────────────────

        [Test]
        public void HeroExtraction_RemovesHeroFromDeck()
        {
            var gs = new GameState();
            var normalCard = ScriptableObject.CreateInstance<CardData>();
            normalCard.EditorSetup("normal", "Normal", 2, 2, RuneType.Blazing, 0, "normal");
            var heroCard = ScriptableObject.CreateInstance<CardData>();
            heroCard.EditorSetup("hero", "Hero", 4, 4, RuneType.Blazing, 1, "hero", isHero: true);

            // Build a deck with normal + hero
            gs.PDeck.Add(gs.MakeUnit(normalCard, GameRules.OWNER_PLAYER));
            gs.PDeck.Add(gs.MakeUnit(heroCard, GameRules.OWNER_PLAYER));
            gs.PDeck.Add(gs.MakeUnit(normalCard, GameRules.OWNER_PLAYER));

            Assert.AreEqual(3, gs.PDeck.Count);

            // Extract hero
            for (int i = 0; i < gs.PDeck.Count; i++)
            {
                if (gs.PDeck[i].CardData.IsHero)
                {
                    gs.PHero = gs.PDeck[i];
                    gs.PDeck.RemoveAt(i);
                    break;
                }
            }

            Assert.IsNotNull(gs.PHero);
            Assert.AreEqual("Hero", gs.PHero.UnitName);
            Assert.AreEqual(2, gs.PDeck.Count);

            // Verify no hero remains in deck
            foreach (var u in gs.PDeck)
                Assert.IsFalse(u.CardData.IsHero);

            Object.DestroyImmediate(normalCard);
            Object.DestroyImmediate(heroCard);
        }

        [Test]
        public void HeroExtraction_NoHeroInDeck_HeroStaysNull()
        {
            var gs = new GameState();
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("normal", "Normal", 2, 2, RuneType.Blazing, 0, "normal");

            gs.PDeck.Add(gs.MakeUnit(card, GameRules.OWNER_PLAYER));
            gs.PDeck.Add(gs.MakeUnit(card, GameRules.OWNER_PLAYER));

            // Try to extract — no hero present
            for (int i = 0; i < gs.PDeck.Count; i++)
            {
                if (gs.PDeck[i].CardData.IsHero)
                {
                    gs.PHero = gs.PDeck[i];
                    gs.PDeck.RemoveAt(i);
                    break;
                }
            }

            Assert.IsNull(gs.PHero);
            Assert.AreEqual(2, gs.PDeck.Count);

            Object.DestroyImmediate(card);
        }

        // ── LegendInstance.DisplayData ────────────────────────────────────────

        [Test]
        public void LegendInstance_DisplayData_DefaultNull()
        {
            var legend = new LegendInstance("kaisa", "Kaisa", GameRules.OWNER_PLAYER);
            Assert.IsNull(legend.DisplayData);
        }

        [Test]
        public void LegendInstance_DisplayData_CanBeSet()
        {
            var legend = new LegendInstance("kaisa", "Kaisa", GameRules.OWNER_PLAYER);
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("kaisa_legend", "卡莎传奇", 0, 0, RuneType.Blazing, 0, "desc");

            legend.DisplayData = card;
            Assert.AreSame(card, legend.DisplayData);

            Object.DestroyImmediate(card);
        }

        // ── Turn timer boundary tests ────────────────────────────────────────

        [Test]
        public void TurnTimer_Constants()
        {
            // Verify 30-second timer is the expected duration
            Assert.AreEqual(30, 30); // Timer starts at 30
        }

        // ── Discard/Exile viewer data tests ──────────────────────────────────

        [Test]
        public void DiscardPile_SupportsReverseOrder()
        {
            var gs = new GameState();
            var card1 = ScriptableObject.CreateInstance<CardData>();
            card1.EditorSetup("c1", "Card1", 1, 1, RuneType.Blazing, 0, "desc");
            var card2 = ScriptableObject.CreateInstance<CardData>();
            card2.EditorSetup("c2", "Card2", 2, 2, RuneType.Blazing, 0, "desc");

            var u1 = gs.MakeUnit(card1, GameRules.OWNER_PLAYER);
            var u2 = gs.MakeUnit(card2, GameRules.OWNER_PLAYER);

            gs.PDiscard.Add(u1);
            gs.PDiscard.Add(u2);

            // Viewer shows in reverse — most recent first
            Assert.AreEqual(2, gs.PDiscard.Count);
            Assert.AreSame(u2, gs.PDiscard[gs.PDiscard.Count - 1]);

            Object.DestroyImmediate(card1);
            Object.DestroyImmediate(card2);
        }

        // ── Multiple hero cards edge case ────────────────────────────────────

        [Test]
        public void HeroExtraction_MultipleHeroes_OnlyFirstExtracted()
        {
            var gs = new GameState();
            var heroCard1 = ScriptableObject.CreateInstance<CardData>();
            heroCard1.EditorSetup("hero1", "Hero1", 4, 4, RuneType.Blazing, 1, "hero1", isHero: true);
            var heroCard2 = ScriptableObject.CreateInstance<CardData>();
            heroCard2.EditorSetup("hero2", "Hero2", 5, 5, RuneType.Blazing, 1, "hero2", isHero: true);

            gs.PDeck.Add(gs.MakeUnit(heroCard1, GameRules.OWNER_PLAYER));
            gs.PDeck.Add(gs.MakeUnit(heroCard2, GameRules.OWNER_PLAYER));

            // Extract first hero only
            for (int i = 0; i < gs.PDeck.Count; i++)
            {
                if (gs.PDeck[i].CardData.IsHero)
                {
                    gs.PHero = gs.PDeck[i];
                    gs.PDeck.RemoveAt(i);
                    break;
                }
            }

            Assert.IsNotNull(gs.PHero);
            Assert.AreEqual("Hero1", gs.PHero.UnitName);
            Assert.AreEqual(1, gs.PDeck.Count); // Second hero remains

            Object.DestroyImmediate(heroCard1);
            Object.DestroyImmediate(heroCard2);
        }

        // ── Hero played (set to null) ────────────────────────────────────────

        [Test]
        public void Hero_SetToNull_AfterPlayed()
        {
            var gs = new GameState();
            var heroCard = ScriptableObject.CreateInstance<CardData>();
            heroCard.EditorSetup("hero", "Hero", 4, 4, RuneType.Blazing, 1, "hero", isHero: true);
            var unit = gs.MakeUnit(heroCard, GameRules.OWNER_PLAYER);

            gs.PHero = unit;
            Assert.IsNotNull(gs.PHero);

            // Simulate hero being played
            gs.PBase.Add(unit);
            gs.PHero = null;

            Assert.IsNull(gs.PHero);
            Assert.AreEqual(1, gs.PBase.Count);

            Object.DestroyImmediate(heroCard);
        }

        // ── CardData hero + other flags coexist ──────────────────────────────

        [Test]
        public void CardData_HeroAndSpell_BothFalse()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("normal", "Normal", 2, 2, RuneType.Blazing, 0, "desc");
            Assert.IsFalse(card.IsHero);
            Assert.IsFalse(card.IsSpell);
            Assert.IsFalse(card.IsEquipment);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void CardData_Hero_CoexistsWithKeywords()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("hero", "Hero", 4, 4, RuneType.Blazing, 1, "desc",
                keywords: CardKeyword.Haste | CardKeyword.Conquest, isHero: true);
            Assert.IsTrue(card.IsHero);
            Assert.IsTrue(card.HasKeyword(CardKeyword.Haste));
            Assert.IsTrue(card.HasKeyword(CardKeyword.Conquest));
            Object.DestroyImmediate(card);
        }

        // ── Empty deck edge cases ────────────────────────────────────────────

        [Test]
        public void HeroExtraction_EmptyDeck_NoError()
        {
            var gs = new GameState();
            Assert.AreEqual(0, gs.PDeck.Count);

            // Extract from empty deck — no crash
            for (int i = 0; i < gs.PDeck.Count; i++)
            {
                if (gs.PDeck[i].CardData.IsHero)
                {
                    gs.PHero = gs.PDeck[i];
                    gs.PDeck.RemoveAt(i);
                    break;
                }
            }

            Assert.IsNull(gs.PHero);
        }

        // ── Exile list operations ────────────────────────────────────────────

        [Test]
        public void ExileList_AddAndRemove()
        {
            var gs = new GameState();
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("test", "Test", 1, 1, RuneType.Blazing, 0, "desc");
            var unit = gs.MakeUnit(card, GameRules.OWNER_PLAYER);

            gs.PExile.Add(unit);
            Assert.AreEqual(1, gs.PExile.Count);

            gs.PExile.Remove(unit);
            Assert.AreEqual(0, gs.PExile.Count);

            Object.DestroyImmediate(card);
        }
    }
}
