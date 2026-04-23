using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;

namespace FWTCG.Tests.EditMode
{
    /// <summary>
    /// Behavioural interaction tests for DEV-2 systems.
    /// Covers: EntryEffectSystem (6 effects), DeathwishSystem (2 effects),
    /// Tiyana passive (ScoreManager), and Mulligan swap logic.
    /// All tests verify state changes only — no Unity UI API dependencies.
    /// </summary>
    public class DEV2InteractionTests
    {
        // ── Setup / Teardown ─────────────────────────────────────────────────

        private EntryEffectSystem _entry;
        private DeathwishSystem   _deathwish;
        private ScoreManager      _score;
        private GameState         _gs;
        private GameObject        _go;

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _gs       = new GameState();
            _go       = new GameObject("DEV2Tests");
            _entry    = _go.AddComponent<EntryEffectSystem>();
            _deathwish = _go.AddComponent<DeathwishSystem>();
            _score    = _go.AddComponent<ScoreManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private CardData MakeCard(string id, int atk, string effectId = "",
            CardKeyword kw = CardKeyword.None)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            SetField(data, "_id",       id);
            SetField(data, "_cardName", id);
            SetField(data, "_cost",     1);
            SetField(data, "_atk",      atk);
            SetField(data, "_runeType", RuneType.Blazing);
            SetField(data, "_effectId", effectId);
            SetField(data, "_keywords", kw);
            SetField(data, "_isSpell",  false);
            return data;
        }

        private UnitInstance MakeUnit(string owner, string id, int atk,
            string effectId = "", CardKeyword kw = CardKeyword.None)
        {
            return new UnitInstance(GameState.NextUid(), MakeCard(id, atk, effectId, kw), owner);
        }

        private void AddDeckCards(string owner, int count)
        {
            for (int i = 0; i < count; i++)
                _gs.GetDeck(owner).Add(MakeUnit(owner, $"deck_{i}", 2));
        }

        private void SetField(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field,
                BindingFlags.NonPublic | BindingFlags.Instance);
            fi?.SetValue(obj, value);
        }

        // ── EntryEffectSystem: YordelInstructor ───────────────────────────────

        [Test]
        public void EntryEffect_YordelInstructor_DrawsOneCard()
        {
            AddDeckCards(GameRules.OWNER_PLAYER, 3);
            var unit = MakeUnit(GameRules.OWNER_PLAYER, "yordel", 2, "yordel_instructor_enter");
            _gs.PBase.Add(unit);

            _entry.OnUnitEntered(unit, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(1, _gs.PHand.Count, "YordelInstructor should draw 1 card on entry");
        }

        // ── EntryEffectSystem: Darius ─────────────────────────────────────────

        [Test]
        public void EntryEffect_Darius_SecondCard_GainsBonusAndUnexhausts()
        {
            _gs.CardsPlayedThisTurn = 2; // second card played this turn
            var unit = MakeUnit(GameRules.OWNER_PLAYER, "darius", 5, "darius_second_card");
            unit.Exhausted = true;
            _gs.PBase.Add(unit);

            _entry.OnUnitEntered(unit, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(2, unit.TempAtkBonus,
                "Darius should gain +2 TempAtkBonus when second card this turn");
            Assert.IsFalse(unit.Exhausted,
                "Darius should become active (un-exhausted) when second card this turn");
        }

        [Test]
        public void EntryEffect_Darius_FirstCard_NoBonus()
        {
            _gs.CardsPlayedThisTurn = 1; // first card this turn
            var unit = MakeUnit(GameRules.OWNER_PLAYER, "darius", 5, "darius_second_card");
            unit.Exhausted = true;

            _entry.OnUnitEntered(unit, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(0, unit.TempAtkBonus,
                "Darius should NOT gain bonus on first card played");
            Assert.IsTrue(unit.Exhausted,
                "Darius should remain exhausted when first card played");
        }

        // ── EntryEffectSystem: ThousandTail ───────────────────────────────────

        [Test]
        public void EntryEffect_ThousandTail_DebuffsAllEnemies()
        {
            // Enemy units with various ATK values
            var enemy1 = MakeUnit(GameRules.OWNER_ENEMY, "foe1", 5);
            var enemy2 = MakeUnit(GameRules.OWNER_ENEMY, "foe2", 4);
            _gs.EBase.Add(enemy1);
            _gs.BF[0].EnemyUnits.Add(enemy2);

            var unit = MakeUnit(GameRules.OWNER_PLAYER, "thousandtail", 3, "thousand_tail_enter");
            _entry.OnUnitEntered(unit, GameRules.OWNER_PLAYER, _gs);

            // ThousandTail 改为 TempAtkBonus（回合结束清零），查 EffectiveAtk
            Assert.AreEqual(2, enemy1.EffectiveAtk(), "Enemy1 (5 atk) should be debuffed to 2 (-3)");
            Assert.AreEqual(1, enemy2.EffectiveAtk(), "Enemy2 (4 atk) should be debuffed to 1 (-3)");
        }

        [Test]
        public void EntryEffect_ThousandTail_MinimumOneAtk()
        {
            // Enemy with ATK=1 → should not drop below 1
            var enemy = MakeUnit(GameRules.OWNER_ENEMY, "weakFoe", 1);
            _gs.EBase.Add(enemy);

            var unit = MakeUnit(GameRules.OWNER_PLAYER, "thousandtail", 3, "thousand_tail_enter");
            _entry.OnUnitEntered(unit, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(1, enemy.EffectiveAtk(),
                "Enemy effective ATK should not drop below 1 (min enforced by ThousandTail entry)");
        }

        // ── EntryEffectSystem: Tiyana ─────────────────────────────────────────

        [Test]
        public void Tiyana_InBase_DoesNotBlockOpponentScore()
        {
            // 新行为：Tiyana 在基地时被动不生效，必须在战场上
            var unit = MakeUnit(GameRules.OWNER_PLAYER, "tiyana", 4, "tiyana_enter");
            _gs.PBase.Add(unit);
            _entry.OnUnitEntered(unit, GameRules.OWNER_PLAYER, _gs);

            bool awarded = _score.AddScore(GameRules.OWNER_ENEMY, 1,
                GameRules.SCORE_TYPE_HOLD, 0, _gs);

            Assert.IsTrue(awarded, "Tiyana 在基地时不应阻止对手得分");
        }

        // ── ScoreManager: Tiyana passive blocks ALL score types ───────────────

        [Test]
        public void Tiyana_OnBattlefield_BlocksOpponentHoldScore()
        {
            var tiyana = MakeUnit(GameRules.OWNER_PLAYER, "tiyana", 4, "tiyana_enter");
            _gs.BF[0].PlayerUnits.Add(tiyana);

            bool awarded = _score.AddScore(GameRules.OWNER_ENEMY, 1,
                GameRules.SCORE_TYPE_HOLD, 0, _gs);

            Assert.IsFalse(awarded, "Tiyana 在战场上时应阻止对手据守得分");
            Assert.AreEqual(0, _gs.EScore);
        }

        [Test]
        public void Tiyana_OnBattlefield_BlocksOpponentConquestScore()
        {
            // 卡面："对手无法得分" → 所有得分类型均阻止
            var tiyana = MakeUnit(GameRules.OWNER_PLAYER, "tiyana", 4, "tiyana_enter");
            _gs.BF[0].PlayerUnits.Add(tiyana);
            _gs.BFConqueredThisTurn.Add(0);
            _gs.BFConqueredThisTurn.Add(1);

            bool awarded = _score.AddScore(GameRules.OWNER_ENEMY, 1,
                GameRules.SCORE_TYPE_CONQUER, 0, _gs);

            Assert.IsFalse(awarded, "Tiyana 在战场上时也应阻止对手征服得分");
            Assert.AreEqual(0, _gs.EScore);
        }

        [Test]
        public void Tiyana_OnBattlefield_DoesNotAffectOwnScore()
        {
            var tiyana = MakeUnit(GameRules.OWNER_PLAYER, "tiyana", 4, "tiyana_enter");
            _gs.BF[0].PlayerUnits.Add(tiyana);

            bool awarded = _score.AddScore(GameRules.OWNER_PLAYER, 1,
                GameRules.SCORE_TYPE_HOLD, 0, _gs);

            Assert.IsTrue(awarded, "Tiyana 被动只影响对手得分");
        }

        // ── DeathwishSystem: AlertSentinel ────────────────────────────────────

        [Test]
        public void Deathwish_AlertSentinel_DrawsCardOnDeath()
        {
            AddDeckCards(GameRules.OWNER_PLAYER, 3);
            var unit = MakeUnit(GameRules.OWNER_PLAYER, "sentinel", 3,
                "alert_sentinel_die", CardKeyword.Deathwish);

            _deathwish.OnUnitsDied(new List<UnitInstance> { unit }, 0, _gs);

            Assert.AreEqual(1, _gs.PHand.Count,
                "AlertSentinel deathwish should draw 1 card on death");
        }

        [Test]
        public void Deathwish_NonDeathwishUnit_DoesNotDrawCard()
        {
            AddDeckCards(GameRules.OWNER_PLAYER, 3);
            var unit = MakeUnit(GameRules.OWNER_PLAYER, "normalUnit", 3);
            // No Deathwish keyword

            _deathwish.OnUnitsDied(new List<UnitInstance> { unit }, 0, _gs);

            Assert.AreEqual(0, _gs.PHand.Count,
                "Unit without Deathwish keyword should not draw cards on death");
        }

        // ── DeathwishSystem: WailingPoro ──────────────────────────────────────

        [Test]
        public void Deathwish_WailingPoro_DrawsCardWhenAloneOnBF()
        {
            AddDeckCards(GameRules.OWNER_PLAYER, 3);
            // Poro was alone on BF0 → after dying, PlayerUnits on BF0 is empty
            var poro = MakeUnit(GameRules.OWNER_PLAYER, "poro", 2,
                "wailing_poro_die", CardKeyword.Deathwish);
            // BF0 is empty (poro already removed before deathwish triggers)

            _deathwish.OnUnitsDied(new List<UnitInstance> { poro }, 0, _gs);

            Assert.AreEqual(1, _gs.PHand.Count,
                "WailingPoro should draw card when it was alone on the battlefield");
        }

        [Test]
        public void Deathwish_WailingPoro_NoDrawWhenAlliesRemainOnBF()
        {
            AddDeckCards(GameRules.OWNER_PLAYER, 3);
            var poro = MakeUnit(GameRules.OWNER_PLAYER, "poro", 2,
                "wailing_poro_die", CardKeyword.Deathwish);

            // Another ally still on BF0
            var ally = MakeUnit(GameRules.OWNER_PLAYER, "ally", 3);
            _gs.BF[0].PlayerUnits.Add(ally);

            _deathwish.OnUnitsDied(new List<UnitInstance> { poro }, 0, _gs);

            Assert.AreEqual(0, _gs.PHand.Count,
                "WailingPoro should NOT draw card when allies remain on the battlefield");
        }

        // ── Mulligan Logic ────────────────────────────────────────────────────
        // Tests the state transitions that the mulligan UI triggers
        // (StartupFlowUI calls deck/hand swap operations directly)

        [Test]
        public void Mulligan_SwappingCard_ReplacesFromDeck()
        {
            // Setup: hand has 4 cards, deck has remaining cards
            var card1 = MakeUnit(GameRules.OWNER_PLAYER, "hand1", 2);
            var card2 = MakeUnit(GameRules.OWNER_PLAYER, "hand2", 3);
            var deckCard = MakeUnit(GameRules.OWNER_PLAYER, "newCard", 4);
            _gs.PHand.Add(card1);
            _gs.PHand.Add(card2);
            _gs.PDeck.Add(deckCard);

            // Simulate mulligan: return card1 to deck bottom, draw deckCard
            _gs.PHand.Remove(card1);
            _gs.PDeck.Add(card1);          // returned to deck
            _gs.PHand.Add(_gs.PDeck[0]);   // draw new top
            _gs.PDeck.RemoveAt(0);

            Assert.AreEqual(2, _gs.PHand.Count,
                "Hand count should remain 2 after single mulligan swap");
            Assert.IsTrue(_gs.PHand.Contains(deckCard),
                "New card from deck should be in hand after mulligan");
            Assert.IsFalse(_gs.PHand.Contains(card1),
                "Returned card should no longer be in hand after mulligan");
        }

        [Test]
        public void Mulligan_MaxTwoSwaps_HandCountUnchanged()
        {
            // Simulate 2-card mulligan
            var handCards = new List<UnitInstance>();
            for (int i = 0; i < 4; i++)
            {
                var c = MakeUnit(GameRules.OWNER_PLAYER, $"h{i}", 2);
                _gs.PHand.Add(c);
                handCards.Add(c);
            }
            for (int i = 0; i < 5; i++)
                _gs.PDeck.Add(MakeUnit(GameRules.OWNER_PLAYER, $"d{i}", 3));

            // Swap 2 cards
            for (int swap = 0; swap < 2; swap++)
            {
                var toReturn = handCards[swap];
                _gs.PHand.Remove(toReturn);
                _gs.PDeck.Add(toReturn);
                _gs.PHand.Add(_gs.PDeck[0]);
                _gs.PDeck.RemoveAt(0);
            }

            Assert.AreEqual(4, _gs.PHand.Count,
                "Hand should still have 4 cards after swapping 2 via mulligan");
        }
    }
}
