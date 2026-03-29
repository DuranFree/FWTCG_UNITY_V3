using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-4 Interaction Tests — ReactiveSystem effects.
    /// 18 tests covering all 9 reactive spells + auto-targeting + negation.
    /// </summary>
    [TestFixture]
    public class DEV4InteractionTests
    {
        private GameObject _go;
        private ReactiveSystem _reactiveSys;
        private GameState _gs;

        // ── Helper: create a minimal CardData stub ────────────────────────────

        private static CardData MakeSpellCard(string id, string name, int cost,
            string effectId, CardKeyword kw = CardKeyword.Reactive)
        {
            var so = ScriptableObject.CreateInstance<CardData>();
            so.EditorSetup(id, name, cost, 0, RuneType.Blazing, 0,
                           "test spell", kw, effectId,
                           isSpell: true, spellTargetType: SpellTargetType.None);
            return so;
        }

        private static CardData MakeUnitCard(string id, string name, int atk)
        {
            var so = ScriptableObject.CreateInstance<CardData>();
            so.EditorSetup(id, name, 2, atk, RuneType.Blazing, 0, "test unit");
            return so;
        }

        private static UnitInstance MakeUnit(string name, int atk, string owner)
        {
            var data = MakeUnitCard(name, name, atk);
            return new UnitInstance(GameState.NextUid(), data, owner);
        }

        private static UnitInstance MakeSpell(string id, string name, int cost, string effectId)
        {
            var data = MakeSpellCard(id, name, cost, effectId);
            return new UnitInstance(GameState.NextUid(), data, GameRules.OWNER_PLAYER);
        }

        // ── Setup / Teardown ─────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _go = new GameObject("ReactiveSystemTest");
            _reactiveSys = _go.AddComponent<ReactiveSystem>();
            _gs = new GameState();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── swindle tests ────────────────────────────────────────────────────

        [Test]
        public void Swindle_ReducesEnemyTempAtkAndDraws()
        {
            // Arrange: enemy unit in base, player has card in deck, swindle in hand
            var enemy = MakeUnit("enemy1", 4, GameRules.OWNER_ENEMY);
            _gs.EBase.Add(enemy);
            var deckCard = MakeUnit("deckcard", 2, GameRules.OWNER_PLAYER);
            _gs.PDeck.Add(deckCard);
            var reactive = MakeSpell("swindle", "诡计", 1, "swindle");
            _gs.PHand.Add(reactive);

            // Act
            bool negated = _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            // Assert
            Assert.IsFalse(negated);
            Assert.AreEqual(-1, enemy.TempAtkBonus, "enemy unit TempAtkBonus should be -1");
            Assert.AreEqual(1, _gs.PHand.Count, "player hand should have 1 card (drawn)");
            Assert.IsTrue(_gs.PDiscard.Contains(reactive), "swindle should be in discard");
        }

        [Test]
        public void Swindle_MovedToDiscard()
        {
            // Need at least 1 deck card so DrawCards doesn't reshuffle swindle back from discard
            var deckCard = MakeUnit("deckcard", 2, GameRules.OWNER_PLAYER);
            _gs.PDeck.Add(deckCard);
            var reactive = MakeSpell("swindle", "诡计", 1, "swindle");
            _gs.PHand.Add(reactive);

            _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            Assert.IsFalse(_gs.PHand.Contains(reactive), "swindle removed from hand");
            Assert.IsTrue(_gs.PDiscard.Contains(reactive), "swindle in discard");
        }

        // ── retreat_rune tests ────────────────────────────────────────────────

        [Test]
        public void RetreatRune_RecallsUnitAndRecyclesRune()
        {
            // Arrange: player unit on BF0, a rune in play
            var unit = MakeUnit("unit1", 3, GameRules.OWNER_PLAYER);
            _gs.BF[0].PlayerUnits.Add(unit);
            var rune = new RuneInstance(GameState.NextUid(), RuneType.Blazing);
            _gs.PRunes.Add(rune);
            var reactive = MakeSpell("retreat_rune", "撤退符文", 1, "retreat_rune");
            _gs.PHand.Add(reactive);

            // Act
            _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            // Assert
            Assert.IsFalse(_gs.BF[0].PlayerUnits.Contains(unit), "unit removed from BF");
            Assert.IsTrue(_gs.PBase.Contains(unit), "unit recalled to base");
            Assert.IsTrue(unit.Exhausted, "recalled unit is exhausted");
            Assert.AreEqual(0, _gs.PRunes.Count, "rune removed from play");
            Assert.IsTrue(_gs.PRuneDeck.Contains(rune), "rune returned to rune deck");
        }

        [Test]
        public void RetreatRune_GainsSch()
        {
            var unit = MakeUnit("unit1", 3, GameRules.OWNER_PLAYER);
            _gs.BF[0].PlayerUnits.Add(unit);
            var rune = new RuneInstance(GameState.NextUid(), RuneType.Blazing);
            _gs.PRunes.Add(rune);
            var reactive = MakeSpell("retreat_rune", "撤退符文", 1, "retreat_rune");
            _gs.PHand.Add(reactive);

            _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            Assert.AreEqual(1, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing),
                "should gain 1 Blazing sch");
        }

        // ── guilty_pleasure tests ──────────────────────────────────────────────

        [Test]
        public void GuiltyPleasure_DiscardsAndDeals2Damage()
        {
            var enemy = MakeUnit("enemy1", 5, GameRules.OWNER_ENEMY);
            _gs.EBase.Add(enemy);
            var handCard = MakeUnit("handcard", 2, GameRules.OWNER_PLAYER);
            _gs.PHand.Add(handCard);
            var reactive = MakeSpell("guilty_pleasure", "罪恶乐趣", 2, "guilty_pleasure");
            _gs.PHand.Add(reactive);

            _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            Assert.AreEqual(3, enemy.CurrentHp, "enemy should have 5-2=3 HP");
            Assert.IsTrue(_gs.PDiscard.Contains(handCard), "hand card discarded");
            Assert.IsFalse(_gs.PHand.Contains(handCard), "hand card not in hand");
        }

        [Test]
        public void GuiltyPleasure_KillsEnemyIfLethal()
        {
            var enemy = MakeUnit("weakenemy", 2, GameRules.OWNER_ENEMY);
            _gs.EBase.Add(enemy);
            var handCard = MakeUnit("handcard", 1, GameRules.OWNER_PLAYER);
            _gs.PHand.Add(handCard);
            var reactive = MakeSpell("guilty_pleasure", "罪恶乐趣", 2, "guilty_pleasure");
            _gs.PHand.Add(reactive);

            _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            Assert.IsFalse(_gs.EBase.Contains(enemy), "dead enemy removed from base");
            Assert.IsTrue(_gs.EDiscard.Contains(enemy), "dead enemy in discard");
        }

        // ── smoke_bomb tests ──────────────────────────────────────────────────

        [Test]
        public void SmokeBomb_ReducesEnemyTempAtkBy4()
        {
            var enemy = MakeUnit("enemy1", 6, GameRules.OWNER_ENEMY);
            _gs.EBase.Add(enemy);
            var reactive = MakeSpell("smoke_bomb", "烟雾弹", 1, "smoke_bomb");
            _gs.PHand.Add(reactive);

            _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            Assert.AreEqual(-4, enemy.TempAtkBonus, "smoke_bomb -4 TempAtkBonus");
        }

        // ── scoff tests ───────────────────────────────────────────────────────

        [Test]
        public void Scoff_NegatesSpellWithCost4OrLess()
        {
            var trigger = MakeSpell("hex_ray", "虚空射线", 3, "hex_ray");
            trigger.CardData.GetType(); // just ensure it exists
            var reactive = MakeSpell("scoff", "嘲讽", 1, "scoff");
            _gs.PHand.Add(reactive);

            bool negated = _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, trigger, _gs);

            Assert.IsTrue(negated, "scoff should negate cost-3 spell");
        }

        [Test]
        public void Scoff_DoesNotNegateSpellWithCost5Plus()
        {
            var expensiveSpell = MakeSpell("akasi_storm", "狂暴", 5, "akasi_storm");
            var reactive = MakeSpell("scoff", "嘲讽", 1, "scoff");
            _gs.PHand.Add(reactive);

            bool negated = _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, expensiveSpell, _gs);

            Assert.IsFalse(negated, "scoff should NOT negate cost-5 spell");
        }

        // ── duel_stance tests ─────────────────────────────────────────────────

        [Test]
        public void DuelStance_GivesPermanentPlusOnePlusOne()
        {
            var ally = MakeUnit("ally1", 3, GameRules.OWNER_PLAYER);
            _gs.PBase.Add(ally);
            var reactive = MakeSpell("duel_stance", "决斗姿态", 1, "duel_stance");
            _gs.PHand.Add(reactive);

            _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            Assert.AreEqual(4, ally.CurrentAtk, "ally CurrentAtk should be 4");
            Assert.AreEqual(4, ally.CurrentHp, "ally CurrentHp should be 4");
            Assert.AreEqual(1, ally.BuffTokens, "ally BuffTokens should be 1 for persistence");
        }

        // ── well_trained tests ────────────────────────────────────────────────

        [Test]
        public void WellTrained_AddsTempAtkAndDrawsCard()
        {
            var ally = MakeUnit("ally1", 3, GameRules.OWNER_PLAYER);
            _gs.PBase.Add(ally);
            var deckCard = MakeUnit("deckcard", 2, GameRules.OWNER_PLAYER);
            _gs.PDeck.Add(deckCard);
            var reactive = MakeSpell("well_trained", "精英训练", 2, "well_trained");
            _gs.PHand.Add(reactive);

            _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            Assert.AreEqual(2, ally.TempAtkBonus, "well_trained +2 TempAtkBonus");
            Assert.AreEqual(1, _gs.PHand.Count, "drew 1 card");
        }

        // ── wind_wall tests ───────────────────────────────────────────────────

        [Test]
        public void WindWall_NegatesAnySpell()
        {
            var trigger = MakeSpell("akasi_storm", "狂暴", 7, "akasi_storm");
            var reactive = MakeSpell("wind_wall", "风墙", 2, "wind_wall");
            _gs.PHand.Add(reactive);

            bool negated = _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, trigger, _gs);

            Assert.IsTrue(negated, "wind_wall negates any spell");
        }

        [Test]
        public void WindWall_NegatesNullTrigger()
        {
            var reactive = MakeSpell("wind_wall", "风墙", 2, "wind_wall");
            _gs.PHand.Add(reactive);

            bool negated = _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);

            Assert.IsTrue(negated, "wind_wall negates even with null trigger");
        }

        // ── flash_counter tests ───────────────────────────────────────────────

        [Test]
        public void FlashCounter_CountersEnemySpell()
        {
            // The triggering spell belongs to the enemy
            var enemySpellData = MakeSpellCard("slam", "冲击", 2, "slam");
            var trigger = new UnitInstance(GameState.NextUid(), enemySpellData, GameRules.OWNER_ENEMY);
            var reactive = MakeSpell("flash_counter", "闪电反制", 1, "flash_counter");
            _gs.PHand.Add(reactive);

            bool negated = _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, trigger, _gs);

            Assert.IsTrue(negated, "flash_counter counters an enemy spell");
        }

        [Test]
        public void FlashCounter_DoesNotCounterFriendlySpell()
        {
            // The triggering spell belongs to the player (same side as the reactor)
            var friendlySpell = MakeSpell("hex_ray", "虚空射线", 1, "hex_ray");
            var reactive = MakeSpell("flash_counter", "闪电反制", 1, "flash_counter");
            _gs.PHand.Add(reactive);

            bool negated = _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, friendlySpell, _gs);

            Assert.IsFalse(negated, "flash_counter should not negate a friendly spell");
        }

        // ── Unknown effectId ──────────────────────────────────────────────────

        [Test]
        public void UnknownEffectId_DoesNotThrowAndReturnsFalse()
        {
            var reactive = MakeSpell("unknown_spell", "未知法术", 1, "unknown_effect");
            _gs.PHand.Add(reactive);

            bool negated = false;
            Assert.DoesNotThrow(() =>
            {
                negated = _reactiveSys.ApplyReactive(reactive, GameRules.OWNER_PLAYER, null, _gs);
            });
            Assert.IsFalse(negated);
            Assert.IsTrue(_gs.PDiscard.Contains(reactive), "unknown reactive still goes to discard");
        }
    }
}
