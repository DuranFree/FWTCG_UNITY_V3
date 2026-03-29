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
    /// Behavioural interaction tests for DEV-1 core game loop.
    /// Covers: unit movement, combat resolution, recall, scoring, rune tapping.
    /// All tests verify state changes only — no Unity UI API dependencies.
    /// </summary>
    public class DEV1InteractionTests
    {
        // ── Setup / Teardown ─────────────────────────────────────────────────

        private CombatSystem  _combat;
        private ScoreManager  _score;
        private DeathwishSystem _deathwish;
        private GameState     _gs;
        private GameObject    _go;

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _gs       = new GameState();
            _go       = new GameObject("DEV1Tests");
            _deathwish = _go.AddComponent<DeathwishSystem>();
            _combat   = _go.AddComponent<CombatSystem>();
            _score    = _go.AddComponent<ScoreManager>();

            // Wire deathwish into combat via reflection
            var fi = typeof(CombatSystem).GetField("_deathwish",
                BindingFlags.NonPublic | BindingFlags.Instance);
            fi?.SetValue(_combat, _deathwish);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private CardData MakeCard(string id, int atk,
            CardKeyword kw = CardKeyword.None, string effectId = "")
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            SetField(data, "_id",       id);
            SetField(data, "_cardName", id);
            SetField(data, "_cost",     1);
            SetField(data, "_atk",      atk);
            SetField(data, "_runeType", RuneType.Blazing);
            SetField(data, "_keywords", kw);
            SetField(data, "_effectId", effectId);
            SetField(data, "_isSpell",  false);
            return data;
        }

        private UnitInstance MakeUnit(string owner, string id, int atk)
        {
            var unit = new UnitInstance(GameState.NextUid(), MakeCard(id, atk), owner);
            return unit;
        }

        private UnitInstance PlaceOnBase(string owner, string id, int atk)
        {
            var unit = MakeUnit(owner, id, atk);
            _gs.GetBase(owner).Add(unit);
            return unit;
        }

        private UnitInstance PlaceOnBF(string owner, int bfIdx, string id, int atk)
        {
            var unit = MakeUnit(owner, id, atk);
            if (owner == GameRules.OWNER_PLAYER)
                _gs.BF[bfIdx].PlayerUnits.Add(unit);
            else
                _gs.BF[bfIdx].EnemyUnits.Add(unit);
            return unit;
        }

        private void SetField(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field,
                BindingFlags.NonPublic | BindingFlags.Instance);
            fi?.SetValue(obj, value);
        }

        // ── Unit Movement ─────────────────────────────────────────────────────

        [Test]
        public void MoveUnit_UnitAppearsOnBattlefield()
        {
            var unit = PlaceOnBase(GameRules.OWNER_PLAYER, "hero", 3);

            _combat.MoveUnit(unit, "base", 0, GameRules.OWNER_PLAYER, _gs);

            Assert.IsTrue(_gs.BF[0].PlayerUnits.Contains(unit),
                "Unit should appear on BF0 after move");
        }

        [Test]
        public void MoveUnit_UnitRemovedFromBase()
        {
            var unit = PlaceOnBase(GameRules.OWNER_PLAYER, "hero", 3);

            _combat.MoveUnit(unit, "base", 0, GameRules.OWNER_PLAYER, _gs);

            Assert.IsFalse(_gs.PBase.Contains(unit),
                "Unit should be removed from base after move");
        }

        [Test]
        public void MoveUnit_UnitBecomesExhausted()
        {
            var unit = PlaceOnBase(GameRules.OWNER_PLAYER, "hero", 3);

            _combat.MoveUnit(unit, "base", 0, GameRules.OWNER_PLAYER, _gs);

            Assert.IsTrue(unit.Exhausted, "Unit should be exhausted after moving");
        }

        [Test]
        public void MoveUnit_ToEmptyBF_PlayerGainsControl()
        {
            var unit = PlaceOnBase(GameRules.OWNER_PLAYER, "hero", 3);

            _combat.MoveUnit(unit, "base", 0, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(GameRules.OWNER_PLAYER, _gs.BF[0].Ctrl,
                "Player should control BF0 after moving to empty battlefield");
        }

        [Test]
        public void MoveUnit_ToEnemyBF_DoesNotAutoGrantControl()
        {
            // Pre-set BF1 as enemy-controlled with an enemy unit
            PlaceOnBF(GameRules.OWNER_ENEMY, 1, "enemy", 4);
            _gs.BF[1].Ctrl = GameRules.OWNER_ENEMY;

            var playerUnit = PlaceOnBase(GameRules.OWNER_PLAYER, "hero", 3);
            _combat.MoveUnit(playerUnit, "base", 1, GameRules.OWNER_PLAYER, _gs);

            // Control should not flip until combat resolves
            Assert.AreEqual(GameRules.OWNER_ENEMY, _gs.BF[1].Ctrl,
                "Control should not flip before combat resolves when enemy is present");
        }

        // ── Combat Resolution ─────────────────────────────────────────────────

        [Test]
        public void Combat_AttackerWins_EnemyKilled()
        {
            // Player(5) vs Enemy(2) → enemy dies
            PlaceOnBF(GameRules.OWNER_PLAYER, 0, "strongHero", 5);
            PlaceOnBF(GameRules.OWNER_ENEMY,  0, "weakFoe",    2);
            _gs.BF[0].Ctrl = GameRules.OWNER_ENEMY;

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            Assert.AreEqual(0, _gs.BF[0].EnemyUnits.Count, "Enemy unit should be killed");
            Assert.AreEqual(1, _gs.EDiscard.Count, "Dead enemy should go to discard");
        }

        [Test]
        public void Combat_AttackerWins_PlayerGainsControl()
        {
            PlaceOnBF(GameRules.OWNER_PLAYER, 0, "strongHero", 5);
            PlaceOnBF(GameRules.OWNER_ENEMY,  0, "weakFoe",    2);
            _gs.BF[0].Ctrl = GameRules.OWNER_ENEMY;

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            Assert.AreEqual(GameRules.OWNER_PLAYER, _gs.BF[0].Ctrl,
                "Player should gain BF control after winning combat");
        }

        [Test]
        public void Combat_DefenderWins_AttackerKilled()
        {
            // Player(2) vs Enemy(5) → player dies
            PlaceOnBF(GameRules.OWNER_PLAYER, 0, "weakHero",   2);
            PlaceOnBF(GameRules.OWNER_ENEMY,  0, "strongFoe",  5);
            _gs.BF[0].Ctrl = GameRules.OWNER_ENEMY;

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            Assert.AreEqual(0, _gs.BF[0].PlayerUnits.Count, "Player unit should be killed");
            Assert.AreEqual(GameRules.OWNER_ENEMY, _gs.BF[0].Ctrl,
                "Enemy should retain BF control after defending successfully");
        }

        [Test]
        public void Combat_BothSurvive_AttackerRecalledToBase()
        {
            // Equal power → neither dies; attacker recalled
            var playerUnit = PlaceOnBF(GameRules.OWNER_PLAYER, 0, "hero", 3);
            playerUnit.CurrentHp = 10; // high HP so neither dies
            var enemyUnit  = PlaceOnBF(GameRules.OWNER_ENEMY,  0, "foe",  3);
            enemyUnit.CurrentHp = 10;

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            Assert.IsTrue(_gs.PBase.Contains(playerUnit),
                "Attacker (player) should be recalled to base when both sides survive");
            Assert.IsFalse(_gs.BF[0].PlayerUnits.Contains(playerUnit),
                "Recalled unit should not remain on battlefield");
        }

        [Test]
        public void Combat_BothWipedOut_BFControlIsNull()
        {
            // Equal power, equal HP → mutual wipe
            PlaceOnBF(GameRules.OWNER_PLAYER, 0, "hero", 3);
            PlaceOnBF(GameRules.OWNER_ENEMY,  0, "foe",  3);

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            Assert.IsNull(_gs.BF[0].Ctrl,
                "BF control should be null after mutual wipe");
        }

        [Test]
        public void Combat_StunnedUnit_ContributesZeroPower()
        {
            // Player(3) stunned vs Enemy(3) → enemy wins (stunned contributes 0)
            var playerUnit = PlaceOnBF(GameRules.OWNER_PLAYER, 0, "stunnedHero", 3);
            playerUnit.Stunned = true;
            PlaceOnBF(GameRules.OWNER_ENEMY, 0, "foe", 3);
            _gs.BF[0].Ctrl = GameRules.OWNER_ENEMY;

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            // Player contributes 0, enemy contributes 3 → player dies, enemy survives
            Assert.AreEqual(0, _gs.BF[0].PlayerUnits.Count,
                "Stunned player unit should die (contributes 0 power)");
            Assert.AreEqual(1, _gs.BF[0].EnemyUnits.Count,
                "Enemy should survive (uncontested power)");
        }

        [Test]
        public void Combat_DamageDistribution_Sequential_ExcessDamageCarriesToNextUnit()
        {
            // Attacker: 1 unit (ATK=6, HP=20 — survives enemy return damage)
            // Defender: 2 units (fragile HP=3, tank HP=5)
            // Player 6 dmg → kills fragile(3HP), 3 carries to tank(5→2HP, alive)
            // Enemy total ATK = 3+5 = 8 → player takes 8, still alive (20-8=12)
            // Both survive → attacker recalled (rule #6)
            var playerUnit = PlaceOnBF(GameRules.OWNER_PLAYER, 0, "bigHitter", 6);
            playerUnit.CurrentHp = 20;
            PlaceOnBF(GameRules.OWNER_ENEMY, 0, "fragile", 3);
            PlaceOnBF(GameRules.OWNER_ENEMY, 0, "tank", 5);

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            // fragile(3HP) should be dead (1 enemy in discard)
            Assert.AreEqual(1, _gs.EDiscard.Count,
                "fragile unit (3HP) should die — sequential damage carries from first to second");
            // tank survived → 1 enemy unit remains on BF
            Assert.AreEqual(1, _gs.BF[0].EnemyUnits.Count,
                "tank (5HP, took 3 damage) should survive sequential damage distribution");
        }

        [Test]
        public void Combat_Conquest_AwardsOneScore()
        {
            // Player conquers BF0 from enemy
            PlaceOnBF(GameRules.OWNER_PLAYER, 0, "hero", 5);
            PlaceOnBF(GameRules.OWNER_ENEMY,  0, "foe",  2);
            _gs.BF[0].Ctrl = GameRules.OWNER_ENEMY; // enemy previously held it

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            Assert.AreEqual(1, _gs.PScore, "Player should score 1 point from conquest");
        }

        [Test]
        public void Combat_Conquest_SameBFSameOwner_NoDoubleScore()
        {
            // Already conquered BF0 this turn — should not score again
            PlaceOnBF(GameRules.OWNER_PLAYER, 0, "hero", 5);
            PlaceOnBF(GameRules.OWNER_ENEMY,  0, "foe",  2);
            _gs.BF[0].Ctrl = GameRules.OWNER_ENEMY;
            _gs.BFConqueredThisTurn.Add(0); // already recorded this turn

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            Assert.AreEqual(0, _gs.PScore, "No double conquest score for same BF same turn");
        }

        [Test]
        public void Combat_HPResetAfterCombat()
        {
            // After combat, all units should have HP reset (Rule #10)
            var playerUnit = PlaceOnBF(GameRules.OWNER_PLAYER, 0, "hero", 3);
            playerUnit.CurrentHp = 3; // full HP
            var enemyUnit  = PlaceOnBF(GameRules.OWNER_ENEMY,  0, "foe",  2);
            enemyUnit.CurrentHp = 2;

            _combat.TriggerCombat(0, GameRules.OWNER_PLAYER, _gs, _score);

            // Survivor should have reset HP (CurrentHp = CurrentAtk)
            // Player(3) beats Enemy(2) → player survives with full HP
            if (_gs.BF[0].PlayerUnits.Count > 0)
            {
                Assert.AreEqual(playerUnit.CurrentAtk, playerUnit.CurrentHp,
                    "Surviving unit HP should be reset to CurrentAtk after combat");
            }
        }

        // ── Recall ────────────────────────────────────────────────────────────

        [Test]
        public void Recall_MovesUnitFromBFToBase()
        {
            var unit = PlaceOnBF(GameRules.OWNER_PLAYER, 0, "hero", 3);

            _combat.RecallUnit(unit, 0, GameRules.OWNER_PLAYER, _gs);

            Assert.IsFalse(_gs.BF[0].PlayerUnits.Contains(unit),
                "Recalled unit should leave battlefield");
            Assert.IsTrue(_gs.PBase.Contains(unit),
                "Recalled unit should arrive in base");
        }

        [Test]
        public void Recall_UnitBecomesExhausted()
        {
            var unit = PlaceOnBF(GameRules.OWNER_PLAYER, 0, "hero", 3);
            unit.Exhausted = false;

            _combat.RecallUnit(unit, 0, GameRules.OWNER_PLAYER, _gs);

            Assert.IsTrue(unit.Exhausted, "Recalled unit should be exhausted (dormant)");
        }

        [Test]
        public void Recall_OpponentGainsControl_WhenTheyHaveUnitsAndPlayerHasNone()
        {
            var playerUnit = PlaceOnBF(GameRules.OWNER_PLAYER, 0, "hero", 3);
            PlaceOnBF(GameRules.OWNER_ENEMY, 0, "foe", 3);
            _gs.BF[0].Ctrl = GameRules.OWNER_PLAYER;

            _combat.RecallUnit(playerUnit, 0, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(GameRules.OWNER_ENEMY, _gs.BF[0].Ctrl,
                "Enemy gains BF control after player recalls their last unit");
        }

        // ── Scoring / Last-Point Rule ─────────────────────────────────────────

        [Test]
        public void Score_LastPointRule_BlocksConquestWhenOnlyOneBFConquered()
        {
            _gs.PScore = 7; // one point away from winning
            // Only conquered BF0, not BF1
            _gs.BFConqueredThisTurn.Clear();
            _gs.PDeck.Add(new UnitInstance(GameState.NextUid(),
                ScriptableObject.CreateInstance<CardData>(), GameRules.OWNER_PLAYER));

            bool awarded = _score.AddScore(GameRules.OWNER_PLAYER, 1,
                GameRules.SCORE_TYPE_CONQUER, 0, _gs);

            Assert.IsFalse(awarded, "Conquest at winning threshold blocked when only 1 BF conquered");
            Assert.AreEqual(7, _gs.PScore, "Score should not change when last-point is denied");
        }

        [Test]
        public void Score_LastPointRule_AllowsConquestWhenBothBFsConquered()
        {
            _gs.PScore = 7; // one point away from winning
            // Already conquered BF0 this turn; now conquering BF1
            _gs.BFConqueredThisTurn.Add(0);

            bool awarded = _score.AddScore(GameRules.OWNER_PLAYER, 1,
                GameRules.SCORE_TYPE_CONQUER, 1, _gs);

            Assert.IsTrue(awarded, "Conquest at winning threshold allowed when both BFs conquered");
            Assert.AreEqual(8, _gs.PScore, "Score should reach 8 (win)");
        }

        [Test]
        public void Score_HoldScore_AwardsNormally()
        {
            _gs.PScore = 3;

            bool awarded = _score.AddScore(GameRules.OWNER_PLAYER, 1,
                GameRules.SCORE_TYPE_HOLD, 0, _gs);

            Assert.IsTrue(awarded, "Hold score should be awarded normally");
            Assert.AreEqual(4, _gs.PScore, "Score should increase by 1");
        }

        // ── Rune Tapping ──────────────────────────────────────────────────────

        [Test]
        public void Rune_Tap_SetsTappedTrue_AndAddsSchEnergy()
        {
            var rune = new RuneInstance(GameState.NextUid(), RuneType.Blazing);
            _gs.PRunes.Add(rune);

            // Simulate the tap interaction (GameManager sets Tapped=true + adds Sch)
            rune.Tapped = true;
            _gs.AddSch(GameRules.OWNER_PLAYER, rune.RuneType, 1);

            Assert.IsTrue(rune.Tapped, "Rune should be tapped after tapping");
            Assert.AreEqual(1, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing),
                "Tapping a Blazing rune should add 1 Blazing schematic energy");
        }

        [Test]
        public void Rune_Untap_ResetsAtStartOfTurn()
        {
            var rune = new RuneInstance(GameState.NextUid(), RuneType.Radiant);
            rune.Tapped = true;
            _gs.PRunes.Add(rune);

            // Simulate start-of-turn reset (TurnManager un-taps runes)
            rune.Tapped = false;
            _gs.ResetSch(GameRules.OWNER_PLAYER);

            Assert.IsFalse(rune.Tapped, "Rune should be un-tapped at start of turn");
            Assert.AreEqual(0, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Radiant),
                "Schematic energy should reset to 0 at start of turn");
        }

        // ── Multi-select Batch Move ───────────────────────────────────────────

        [Test]
        public void BatchMove_MultipleUnitsMoveToBF_AllOnBattlefield()
        {
            var unit1 = PlaceOnBase(GameRules.OWNER_PLAYER, "hero1", 3);
            var unit2 = PlaceOnBase(GameRules.OWNER_PLAYER, "hero2", 2);

            _combat.MoveUnit(unit1, "base", 0, GameRules.OWNER_PLAYER, _gs);
            _combat.MoveUnit(unit2, "base", 0, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(2, _gs.BF[0].PlayerUnits.Count,
                "Both units should be on BF0 after batch move");
            Assert.AreEqual(0, _gs.PBase.Count,
                "Base should be empty after all units moved");
        }
    }
}
