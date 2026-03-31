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
    /// Behavioural tests for DEV-3: SpellSystem effects + targeting validation.
    /// All tests are engine-agnostic: they verify state changes, not UI mechanics.
    /// Run via Unity Test Runner → EditMode tab (or batch: -runTests -testPlatform EditMode).
    /// </summary>
    public class SpellSystemTests
    {
        // ── Setup / Teardown ─────────────────────────────────────────────────

        private SpellSystem _spellSys;
        private GameState   _gs;
        private GameObject  _go;

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _gs = new GameState();
            _go = new GameObject("SpellSysTest");
            _spellSys = _go.AddComponent<SpellSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private CardData MakeSpellCard(string id, int cost, string effectId,
            SpellTargetType targetType = SpellTargetType.None)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            SetField(data, "_id",              id);
            SetField(data, "_cardName",        id);
            SetField(data, "_cost",            cost);
            SetField(data, "_atk",             0);
            SetField(data, "_runeType",        RuneType.Blazing);
            SetField(data, "_effectId",        effectId);
            SetField(data, "_isSpell",         true);
            SetField(data, "_spellTargetType", targetType);
            return data;
        }

        private CardData MakeUnitCard(string id, int cost, int atk)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            SetField(data, "_id",       id);
            SetField(data, "_cardName", id);
            SetField(data, "_cost",     cost);
            SetField(data, "_atk",      atk);
            SetField(data, "_runeType", RuneType.Blazing);
            SetField(data, "_effectId", "");
            SetField(data, "_isSpell",  false);
            return data;
        }

        /// <summary>Puts a spell card into owner's hand and returns the UnitInstance.</summary>
        private UnitInstance MakeSpellInHand(string owner, string id, string effectId,
            SpellTargetType targetType = SpellTargetType.None)
        {
            var data  = MakeSpellCard(id, 1, effectId, targetType);
            var spell = new UnitInstance(GameState.NextUid(), data, owner);
            _gs.GetHand(owner).Add(spell);
            return spell;
        }

        /// <summary>Puts a unit into owner's base and returns it.</summary>
        private UnitInstance MakeUnitInBase(string owner, string id, int atk, int hp)
        {
            var data = MakeUnitCard(id, 1, atk);
            var unit = new UnitInstance(GameState.NextUid(), data, owner);
            unit.CurrentHp = hp;
            _gs.GetBase(owner).Add(unit);
            return unit;
        }

        /// <summary>Puts a unit on battlefield bfIdx.</summary>
        private UnitInstance MakeUnitOnBF(string owner, int bfIdx, string id, int atk, int hp)
        {
            var data = MakeUnitCard(id, 1, atk);
            var unit = new UnitInstance(GameState.NextUid(), data, owner);
            unit.CurrentHp = hp;
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

        // ── CastSpell: Hand / Discard movement ───────────────────────────────

        [Test]
        public void CastSpell_RemovesSpellFromHand()
        {
            var spell = MakeSpellInHand(GameRules.OWNER_PLAYER, "evolve_day", "evolve_day");
            Assert.AreEqual(1, _gs.PHand.Count, "Spell should be in hand before cast");

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);

            Assert.AreEqual(0, _gs.PHand.Count, "Spell should be removed from hand after cast");
        }

        [Test]
        public void CastSpell_AddsSpellToDiscard()
        {
            var spell = MakeSpellInHand(GameRules.OWNER_PLAYER, "evolve_day", "evolve_day");

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);

            Assert.AreEqual(1, _gs.PDiscard.Count, "Spell should be in discard after cast");
            Assert.AreSame(spell, _gs.PDiscard[0]);
        }

        // ── HexRay ────────────────────────────────────────────────────────────

        [Test]
        public void HexRay_Deals3DamageToTarget()
        {
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "hex_ray", "hex_ray", SpellTargetType.EnemyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_ENEMY, "enemy_unit", 3, 6);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            Assert.AreEqual(3, target.CurrentHp, "hex_ray should deal 3 damage (6-3=3)");
        }

        [Test]
        public void HexRay_WithSpellShield_DamageResolvesNormally_Rule721()
        {
            // Rule 721.1.c: SpellShield forces extra sch cost at TARGETING time.
            // Once the targeting cost is paid, the spell resolves fully — damage is NOT absorbed.
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "hex_ray", "hex_ray", SpellTargetType.EnemyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_ENEMY, "enemy_unit", 3, 6);
            target.HasSpellShield = true;
            // Simulate: targeting cost already paid (GameManager handles this before CastSpell)

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            Assert.AreEqual(3, target.CurrentHp, "Rule 721: SpellShield is a targeting cost, not damage absorb — hex_ray deals 3 damage normally (6-3=3)");
            Assert.IsTrue(target.HasSpellShield, "SpellShield charge is NOT consumed at resolution (it was already paid at targeting)");
        }

        // ── VoidSeek ──────────────────────────────────────────────────────────

        [Test]
        public void VoidSeek_Deals4DamageAndDrawsOneCard()
        {
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "void_seek", "void_seek", SpellTargetType.EnemyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_ENEMY, "enemy_unit", 3, 8);

            // Put a card in player's deck to draw
            var deckCard = new UnitInstance(GameState.NextUid(), MakeUnitCard("deck_card", 1, 2), GameRules.OWNER_PLAYER);
            _gs.PDeck.Add(deckCard);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            Assert.AreEqual(4, target.CurrentHp, "void_seek should deal 4 damage (8-4=4)");
            Assert.AreEqual(1, _gs.PHand.Count, "void_seek should draw 1 card into hand");
        }

        // ── Stardrop ──────────────────────────────────────────────────────────

        [Test]
        public void Stardrop_Deals6TotalDamage_WhenTargetSurvivesFirst3()
        {
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "stardrop", "stardrop", SpellTargetType.EnemyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_ENEMY, "tough_unit", 3, 10);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            Assert.AreEqual(4, target.CurrentHp, "stardrop should deal 3+3=6 damage to 10HP unit (10-6=4)");
        }

        [Test]
        public void Stardrop_OnlyDeals3Damage_WhenTargetDiesOnFirstHit()
        {
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "stardrop", "stardrop", SpellTargetType.EnemyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_ENEMY, "fragile_unit", 3, 3);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            // Unit should be dead and in discard, not double-hit
            Assert.AreEqual(0, _gs.EBase.Count, "Target should be removed from base after first 3 damage");
            Assert.AreEqual(1, _gs.EDiscard.Count, "Dead target should be in enemy discard");
        }

        // ── Starburst ─────────────────────────────────────────────────────────

        [Test]
        public void Starburst_Deals6DamageToTarget()
        {
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "starburst", "starburst", SpellTargetType.EnemyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_ENEMY, "enemy_unit", 3, 10);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            Assert.AreEqual(4, target.CurrentHp, "starburst should deal 6 damage (10-6=4)");
        }

        // ── EvolveDay ─────────────────────────────────────────────────────────

        [Test]
        public void EvolveDay_Draws4Cards()
        {
            var spell = MakeSpellInHand(GameRules.OWNER_PLAYER, "evolve_day", "evolve_day");

            // Put 4 cards in deck
            for (int i = 0; i < 4; i++)
                _gs.PDeck.Add(new UnitInstance(GameState.NextUid(), MakeUnitCard($"deck_{i}", 1, 2), GameRules.OWNER_PLAYER));

            int handBefore = _gs.PHand.Count; // 1 (the spell itself)

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);

            // After cast: spell removed from hand (1→0), then 4 drawn (0→4)
            Assert.AreEqual(4, _gs.PHand.Count, "evolve_day should draw 4 cards (spell leaves hand, 4 enter)");
        }

        // ── RallyCall ─────────────────────────────────────────────────────────

        [Test]
        public void RallyCall_UnexhaustsAllFriendlyUnits()
        {
            var spell = MakeSpellInHand(GameRules.OWNER_PLAYER, "rally_call", "rally_call");

            var unit1 = MakeUnitInBase(GameRules.OWNER_PLAYER, "unit1", 3, 5);
            var unit2 = MakeUnitOnBF(GameRules.OWNER_PLAYER, 0, "unit2", 3, 5);
            unit1.Exhausted = true;
            unit2.Exhausted = true;

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);

            Assert.IsFalse(unit1.Exhausted, "unit1 in base should be un-exhausted by rally_call");
            Assert.IsFalse(unit2.Exhausted, "unit2 on battlefield should be un-exhausted by rally_call");
        }

        [Test]
        public void RallyCall_DrawsOneCard()
        {
            var spell = MakeSpellInHand(GameRules.OWNER_PLAYER, "rally_call", "rally_call");
            _gs.PDeck.Add(new UnitInstance(GameState.NextUid(), MakeUnitCard("deck_card", 1, 2), GameRules.OWNER_PLAYER));

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);

            Assert.AreEqual(1, _gs.PHand.Count, "rally_call should draw 1 card into hand");
        }

        // ── Slam ──────────────────────────────────────────────────────────────

        [Test]
        public void Slam_StunsTarget()
        {
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "slam", "slam", SpellTargetType.EnemyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_ENEMY, "enemy_unit", 3, 5);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            Assert.IsTrue(target.Stunned, "slam should stun the target");
        }

        [Test]
        public void Slam_WithSpellShield_StunAppliesNormally_Rule721()
        {
            // Rule 721.1.c: SpellShield only forces extra sch cost to TARGET the unit.
            // The stun effect resolves normally once targeting cost is paid.
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "slam", "slam", SpellTargetType.EnemyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_ENEMY, "shielded_unit", 3, 5);
            target.HasSpellShield = true;
            // Simulate: targeting cost already paid by the caster (GameManager handles this)

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            Assert.IsTrue(target.Stunned, "Rule 721: SpellShield is a targeting cost gate, NOT a stun-blocker — slam stuns normally");
        }

        // ── StrikeAskLater ────────────────────────────────────────────────────

        [Test]
        public void StrikeAskLater_AddsFiveTempAtkBonusToFriendlyUnit()
        {
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "strike_ask_later", "strike_ask_later",
                SpellTargetType.FriendlyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_PLAYER, "friendly_unit", 3, 5);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            Assert.AreEqual(5, target.TempAtkBonus, "strike_ask_later should grant +5 TempAtkBonus");
        }

        // ── BalanceResolve ────────────────────────────────────────────────────

        [Test]
        public void BalanceResolve_DrawsOneCardAndSummonsRune()
        {
            var spell = MakeSpellInHand(GameRules.OWNER_PLAYER, "balance_resolve", "balance_resolve");
            _gs.PDeck.Add(new UnitInstance(GameState.NextUid(), MakeUnitCard("deck_card", 1, 2), GameRules.OWNER_PLAYER));

            // Add a rune to the rune deck
            _gs.PRuneDeck.Add(new RuneInstance(GameState.NextUid(), RuneType.Blazing));
            int runesBefore = _gs.PRunes.Count;

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);

            Assert.AreEqual(1, _gs.PHand.Count, "balance_resolve should draw 1 card");
            Assert.AreEqual(runesBefore + 1, _gs.PRunes.Count, "balance_resolve should summon 1 rune");
        }

        // ── AkasiStorm ────────────────────────────────────────────────────────

        [Test]
        public void AkasiStorm_DealsDamageToEnemyUnits()
        {
            var spell = MakeSpellInHand(GameRules.OWNER_PLAYER, "akasi_storm", "akasi_storm");

            // Put a tough enemy unit that can survive 6×2=12 damage total
            var enemy = MakeUnitInBase(GameRules.OWNER_ENEMY, "tank", 3, 20);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);

            Assert.Less(enemy.CurrentHp, 20, "akasi_storm should deal some damage to enemy unit");
        }

        [Test]
        public void AkasiStorm_RemovesDyingUnits()
        {
            var spell = MakeSpellInHand(GameRules.OWNER_PLAYER, "akasi_storm", "akasi_storm");

            // Weak enemy unit: should die from the storm
            var weakEnemy = MakeUnitInBase(GameRules.OWNER_ENEMY, "weak_unit", 1, 2);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);

            Assert.IsFalse(_gs.EBase.Contains(weakEnemy), "Dead unit should be removed from base by akasi_storm");
        }

        // ── Targeting Validation (GameManager logic, tested in isolation) ─────

        [Test]
        public void TargetValidation_EnemyUnitSpell_RejectsFriendlyTarget()
        {
            // Replicate the validation logic from GameManager.OnUnitClicked:
            //   if (targetType == SpellTargetType.EnemyUnit && unit.Owner != OWNER_ENEMY) → reject
            var spellData = MakeSpellCard("starburst", 2, "starburst", SpellTargetType.EnemyUnit);
            var spell     = new UnitInstance(GameState.NextUid(), spellData, GameRules.OWNER_PLAYER);
            var friendly  = MakeUnitInBase(GameRules.OWNER_PLAYER, "ally", 3, 5);

            SpellTargetType targetType = spell.CardData.SpellTargetType;
            bool isRejected = (targetType == SpellTargetType.EnemyUnit
                               && friendly.Owner != GameRules.OWNER_ENEMY);

            Assert.IsTrue(isRejected, "EnemyUnit spell should reject a friendly unit target");
        }

        [Test]
        public void TargetValidation_EnemyUnitSpell_AcceptsEnemyTarget()
        {
            var spellData = MakeSpellCard("starburst", 2, "starburst", SpellTargetType.EnemyUnit);
            var spell     = new UnitInstance(GameState.NextUid(), spellData, GameRules.OWNER_PLAYER);
            var enemy     = MakeUnitInBase(GameRules.OWNER_ENEMY, "foe", 3, 5);

            SpellTargetType targetType = spell.CardData.SpellTargetType;
            bool isRejected = (targetType == SpellTargetType.EnemyUnit
                               && enemy.Owner != GameRules.OWNER_ENEMY);

            Assert.IsFalse(isRejected, "EnemyUnit spell should accept an enemy unit target");
        }

        [Test]
        public void TargetValidation_FriendlyUnitSpell_RejectsEnemyTarget()
        {
            var spellData = MakeSpellCard("strike_ask_later", 2, "strike_ask_later",
                SpellTargetType.FriendlyUnit);
            var spell  = new UnitInstance(GameState.NextUid(), spellData, GameRules.OWNER_PLAYER);
            var enemy  = MakeUnitInBase(GameRules.OWNER_ENEMY, "foe", 3, 5);

            SpellTargetType targetType = spell.CardData.SpellTargetType;
            bool isRejected = (targetType == SpellTargetType.FriendlyUnit
                               && enemy.Owner != GameRules.OWNER_PLAYER);

            Assert.IsTrue(isRejected, "FriendlyUnit spell should reject an enemy target");
        }

        [Test]
        public void TargetValidation_FriendlyUnitSpell_AcceptsFriendlyTarget()
        {
            var spellData = MakeSpellCard("strike_ask_later", 2, "strike_ask_later",
                SpellTargetType.FriendlyUnit);
            var spell    = new UnitInstance(GameState.NextUid(), spellData, GameRules.OWNER_PLAYER);
            var friendly = MakeUnitInBase(GameRules.OWNER_PLAYER, "ally", 3, 5);

            SpellTargetType targetType = spell.CardData.SpellTargetType;
            bool isRejected = (targetType == SpellTargetType.FriendlyUnit
                               && friendly.Owner != GameRules.OWNER_PLAYER);

            Assert.IsFalse(isRejected, "FriendlyUnit spell should accept a friendly target");
        }

        // ── SpellShield + stardrop: both hits land (Rule 721) ────────────────

        [Test]
        public void Stardrop_WithSpellShield_BothHitsDealFullDamage_Rule721()
        {
            // Rule 721.1.c: SpellShield is a targeting cost paid ONCE at selection time.
            // Both stardrop hits resolve normally — neither is absorbed.
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "stardrop", "stardrop", SpellTargetType.EnemyUnit);
            var target = MakeUnitInBase(GameRules.OWNER_ENEMY, "shielded_tough", 3, 10);
            target.HasSpellShield = true;
            // Simulate: 1 sch targeting cost already paid before CastSpell

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

            // Both hits deal 3 damage → 10-3-3=4
            Assert.AreEqual(4, target.CurrentHp,
                "Rule 721: SpellShield is a targeting cost, not damage absorb — both stardrop hits land (10-3-3=4)");
        }

        // ── Dead unit removed from BF, control updated ─────────────────────

        [Test]
        public void HexRay_KillsEnemy_RemovesFromBFAndUpdatesControl()
        {
            var spell  = MakeSpellInHand(GameRules.OWNER_PLAYER, "hex_ray", "hex_ray", SpellTargetType.EnemyUnit);

            // Enemy unit on BF0 with only 2 HP (dies to hex_ray's 3)
            var enemy = MakeUnitOnBF(GameRules.OWNER_ENEMY, 0, "squishy_foe", 2, 2);
            // Player also has a unit on BF0
            MakeUnitOnBF(GameRules.OWNER_PLAYER, 0, "hero", 3, 5);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, enemy, _gs);

            Assert.IsFalse(_gs.BF[0].EnemyUnits.Contains(enemy), "Dead enemy should be removed from BF");
            Assert.AreEqual(GameRules.OWNER_PLAYER, _gs.BF[0].Ctrl,
                "BF control should transfer to player when enemy's last unit dies");
        }
    }
}
