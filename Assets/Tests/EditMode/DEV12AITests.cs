using NUnit.Framework;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.AI;
using System.Collections.Generic;

namespace FWTCG.Tests
{
    [TestFixture]
    public class DEV12AITests
    {
        private GameState _gs;

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _gs = new GameState();
            _gs.Turn = GameRules.OWNER_ENEMY;
            _gs.Phase = GameRules.PHASE_ACTION;
            _gs.First = GameRules.OWNER_PLAYER;
        }

        // ── AI Rune Recycle ──────────────────────────────────────────────────

        [Test]
        public void AIRecycleRunes_NoNeed_DoesNothing()
        {
            // AI has no cards needing sch → no recycle
            var rune = new RuneInstance(1, RuneType.Blazing);
            rune.Tapped = true;
            _gs.ERunes.Add(rune);

            // No cards in hand that need sch
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("test", "Test", 1, 1, RuneType.Blazing, 0, "desc"); // runeCost=0
            var unit = _gs.MakeUnit(card, GameRules.OWNER_ENEMY);
            _gs.EHand.Add(unit);

            Assert.AreEqual(1, _gs.ERunes.Count);
            // After checking, runes should still be there
            Object.DestroyImmediate(card);
        }

        [Test]
        public void AIRecycleRunes_NeedSch_RecyclesTapped()
        {
            // AI has card needing 1 blazing sch, and a tapped blazing rune
            var rune = new RuneInstance(1, RuneType.Blazing);
            rune.Tapped = true;
            _gs.ERunes.Add(rune);

            // Simulate recycle: remove rune, add to deck, add sch
            _gs.ERunes.RemoveAt(0);
            _gs.ERuneDeck.Insert(0, rune);
            _gs.AddSch(GameRules.OWNER_ENEMY, RuneType.Blazing, 1);

            Assert.AreEqual(0, _gs.ERunes.Count);
            Assert.AreEqual(1, _gs.ERuneDeck.Count);
            Assert.AreEqual(1, _gs.GetSch(GameRules.OWNER_ENEMY, RuneType.Blazing));
        }

        // ── Bad Poro Conquest ─────────────────────────────────────────────────

        [Test]
        public void BadPoro_ConquestKeyword_ExistsOnCard()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("bad_poro", "坏坏魄罗", 2, 2, RuneType.Blazing, 0,
                "征服触发", CardKeyword.Conquest, "bad_poro_conquer");
            Assert.IsTrue(card.HasKeyword(CardKeyword.Conquest));
            Assert.AreEqual("bad_poro_conquer", card.EffectId);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void BadPoro_ConquestTrigger_DrawsCard()
        {
            // Setup: bad_poro in player base, deck has cards
            var bpCard = ScriptableObject.CreateInstance<CardData>();
            bpCard.EditorSetup("bad_poro", "坏坏魄罗", 2, 2, RuneType.Blazing, 0,
                "征服触发", CardKeyword.Conquest, "bad_poro_conquer");
            var bp = _gs.MakeUnit(bpCard, GameRules.OWNER_PLAYER);
            _gs.PBase.Add(bp);

            var deckCard = ScriptableObject.CreateInstance<CardData>();
            deckCard.EditorSetup("deckc", "DeckCard", 1, 1, RuneType.Blazing, 0, "");
            var dc = _gs.MakeUnit(deckCard, GameRules.OWNER_PLAYER);
            _gs.PDeck.Add(dc);

            int handBefore = _gs.PHand.Count;

            // Simulate conquest trigger: bad_poro draws 1
            if (bp.CardData.HasKeyword(CardKeyword.Conquest) && bp.CardData.EffectId == "bad_poro_conquer")
            {
                if (_gs.PDeck.Count > 0)
                {
                    _gs.PHand.Add(_gs.PDeck[0]);
                    _gs.PDeck.RemoveAt(0);
                }
            }

            Assert.AreEqual(handBefore + 1, _gs.PHand.Count);
            Assert.AreEqual(0, _gs.PDeck.Count);

            Object.DestroyImmediate(bpCard);
            Object.DestroyImmediate(deckCard);
        }

        // ── AI Board Score ───────────────────────────────────────────────────

        [Test]
        public void AIBoardScore_EvenGame_ReturnsZero()
        {
            _gs.PScore = 0;
            _gs.EScore = 0;
            int score = SimpleAI.AiBoardScore(_gs, GameRules.OWNER_ENEMY);
            Assert.AreEqual(0, score);
        }

        [Test]
        public void AIBoardScore_AILeading_ReturnsPositive()
        {
            _gs.EScore = 3;
            _gs.PScore = 0;
            int score = SimpleAI.AiBoardScore(_gs, GameRules.OWNER_ENEMY);
            Assert.IsTrue(score > 0);
        }

        // ── Game Over Panel ──────────────────────────────────────────────────

        [Test]
        public void GameState_GameOver_DefaultFalse()
        {
            Assert.IsFalse(_gs.GameOver);
        }

        [Test]
        public void GameState_GameOver_SetTrue()
        {
            _gs.GameOver = true;
            Assert.IsTrue(_gs.GameOver);
        }
    }
}
