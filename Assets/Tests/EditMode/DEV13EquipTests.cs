using NUnit.Framework;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Tests
{
    [TestFixture]
    public class DEV13EquipTests
    {
        private GameState _gs;

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _gs = new GameState();
            _gs.Turn = GameRules.OWNER_PLAYER;
            _gs.Phase = GameRules.PHASE_ACTION;
        }

        // ── Equipment Attachment ─────────────────────────────────────────────

        [Test]
        public void UnitInstance_AttachedEquipment_DefaultNull()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("test", "Test", 3, 3, RuneType.Blazing, 0, "");
            var unit = new UnitInstance(1, card, GameRules.OWNER_PLAYER);
            Assert.IsNull(unit.AttachedEquipment);
            Assert.IsNull(unit.AttachedTo);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Equipment_AttachToUnit_SetsReferences()
        {
            var unitCard = ScriptableObject.CreateInstance<CardData>();
            unitCard.EditorSetup("warrior", "Warrior", 3, 3, RuneType.Blazing, 0, "");
            var unit = new UnitInstance(1, unitCard, GameRules.OWNER_PLAYER);

            var equipCard = ScriptableObject.CreateInstance<CardData>();
            equipCard.EditorSetup("sword", "Sword", 2, 0, RuneType.Crushing, 0, "+2 ATK",
                isEquipment: true, equipAtkBonus: 2);
            var equip = new UnitInstance(2, equipCard, GameRules.OWNER_PLAYER);

            // Attach
            unit.AttachedEquipment = equip;
            equip.AttachedTo = unit;
            unit.CurrentAtk += equipCard.EquipAtkBonus;
            unit.CurrentHp += equipCard.EquipAtkBonus;

            Assert.AreEqual(equip, unit.AttachedEquipment);
            Assert.AreEqual(unit, equip.AttachedTo);
            Assert.AreEqual(5, unit.CurrentAtk); // 3 + 2
            Assert.AreEqual(5, unit.CurrentHp);  // 3 + 2

            Object.DestroyImmediate(unitCard);
            Object.DestroyImmediate(equipCard);
        }

        [Test]
        public void Equipment_IsEquipment_True()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("equip", "Equip", 2, 0, RuneType.Crushing, 1, "",
                isEquipment: true, equipAtkBonus: 2);
            Assert.IsTrue(card.IsEquipment);
            Assert.AreEqual(2, card.EquipAtkBonus);
            Object.DestroyImmediate(card);
        }

        // ── Schematic Cost Validation ────────────────────────────────────────

        [Test]
        public void SchCostCheck_InsufficientSch_BlocksPlay()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("costly", "Costly", 1, 3, RuneType.Blazing, 2, "");
            _gs.PMana = 5;
            // 0 blazing sch → can't afford runeCost=2
            int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing);
            Assert.AreEqual(0, haveSch);
            Assert.IsTrue(haveSch < card.RuneCost);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void SchCostCheck_SufficientSch_AllowsPlay()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("costly", "Costly", 1, 3, RuneType.Blazing, 2, "");
            _gs.PMana = 5;
            _gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 3);
            int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing);
            Assert.AreEqual(3, haveSch);
            Assert.IsTrue(haveSch >= card.RuneCost);
            Object.DestroyImmediate(card);
        }

        // ── GameRules Constants ──────────────────────────────────────────────

        [Test]
        public void GameRules_WinScore_Is8()
        {
            Assert.AreEqual(8, GameRules.WIN_SCORE);
        }

        [Test]
        public void GameRules_InitialHand_Is4()
        {
            Assert.AreEqual(4, GameRules.INITIAL_HAND_SIZE);
        }

        [Test]
        public void GameRules_TurnTimer_Is30()
        {
            Assert.AreEqual(30, GameRules.TURN_TIMER_SECONDS);
        }

        [Test]
        public void GameRules_SecondPlayerRunes_Is3()
        {
            Assert.AreEqual(3, GameRules.RUNES_FIRST_TURN_SECOND);
        }

        [Test]
        public void GameRules_StrongPower_Is5()
        {
            Assert.AreEqual(5, GameRules.STRONG_POWER_THRESHOLD);
        }

        [Test]
        public void GameRules_MaxRunes_Is12()
        {
            Assert.AreEqual(12, GameRules.MAX_RUNES_IN_PLAY);
        }

        [Test]
        public void GameRules_BattlefieldCount_Is2()
        {
            Assert.AreEqual(2, GameRules.BATTLEFIELD_COUNT);
        }
    }
}
