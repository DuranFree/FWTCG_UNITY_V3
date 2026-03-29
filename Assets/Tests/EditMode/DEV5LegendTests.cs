using NUnit.Framework;
using System.Collections.Generic;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-5: Legend system tests
    /// — LegendInstance data integrity
    /// — Kaisa active (虚空感知): usage, cooldown, reset
    /// — Kaisa passive (进化): evolution condition
    /// — Masteryi passive (独影剑鸣): lone defender buff
    /// — Legend death check
    /// </summary>
    public class DEV5LegendTests
    {
        private GameState _gs;
        private LegendSystem _legendSys;

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _gs = new GameState();
            _gs.Round = 0;
            _gs.Turn = GameRules.OWNER_PLAYER;
            _gs.Phase = GameRules.PHASE_ACTION;

            // LegendSystem as a plain instance (no MonoBehaviour in edit mode)
            _legendSys = new UnityEngine.GameObject("LegendSys")
                .AddComponent<LegendSystem>();

            // Initialize legends
            _gs.PLegend = _legendSys.CreateLegend(LegendSystem.KAISA_LEGEND_ID, GameRules.OWNER_PLAYER);
            _gs.ELegend = _legendSys.CreateLegend(LegendSystem.YI_LEGEND_ID, GameRules.OWNER_ENEMY);
        }

        // ── LegendInstance data ───────────────────────────────────────────────

        [Test]
        public void PLegend_InitializesCorrectly()
        {
            Assert.AreEqual(LegendSystem.KAISA_LEGEND_ID, _gs.PLegend.Id);
            Assert.AreEqual(GameRules.LEGEND_HP, _gs.PLegend.MaxHp);
            Assert.AreEqual(GameRules.LEGEND_HP, _gs.PLegend.CurrentHp);
            Assert.AreEqual(1, _gs.PLegend.Level);
            Assert.IsFalse(_gs.PLegend.Exhausted);
            Assert.IsFalse(_gs.PLegend.AbilityUsedThisTurn);
            Assert.AreEqual(GameRules.OWNER_PLAYER, _gs.PLegend.Owner);
        }

        [Test]
        public void ELegend_InitializesCorrectly()
        {
            Assert.AreEqual(LegendSystem.YI_LEGEND_ID, _gs.ELegend.Id);
            Assert.AreEqual(GameRules.LEGEND_HP, _gs.ELegend.MaxHp);
            Assert.AreEqual(GameRules.LEGEND_HP, _gs.ELegend.CurrentHp);
            Assert.AreEqual(1, _gs.ELegend.Level);
            Assert.AreEqual(GameRules.OWNER_ENEMY, _gs.ELegend.Owner);
        }

        [Test]
        public void GetLegend_ReturnsCorrectInstance()
        {
            Assert.AreEqual(_gs.PLegend, _gs.GetLegend(GameRules.OWNER_PLAYER));
            Assert.AreEqual(_gs.ELegend, _gs.GetLegend(GameRules.OWNER_ENEMY));
        }

        [Test]
        public void IsAlive_TrueWhenHpAboveZero()
        {
            Assert.IsTrue(_gs.PLegend.IsAlive);
            _gs.PLegend.CurrentHp = 1;
            Assert.IsTrue(_gs.PLegend.IsAlive);
        }

        [Test]
        public void IsAlive_FalseWhenHpZero()
        {
            _gs.PLegend.TakeDamage(GameRules.LEGEND_HP);
            Assert.IsFalse(_gs.PLegend.IsAlive);
        }

        [Test]
        public void TakeDamage_ClampsToZero()
        {
            _gs.PLegend.TakeDamage(GameRules.LEGEND_HP + 100);
            Assert.AreEqual(0, _gs.PLegend.CurrentHp);
        }

        // ── Kaisa active: 虚空感知 ────────────────────────────────────────────

        [Test]
        public void KaisaActive_AddsBlazingSch()
        {
            int before = _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing);
            bool ok = _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs);
            Assert.IsTrue(ok);
            Assert.AreEqual(before + 1, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing));
        }

        [Test]
        public void KaisaActive_ExhaustsLegend()
        {
            _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs);
            Assert.IsTrue(_gs.PLegend.Exhausted);
            Assert.IsTrue(_gs.PLegend.AbilityUsedThisTurn);
        }

        [Test]
        public void KaisaActive_CannotUseAgainSameTurn()
        {
            _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs);
            bool secondUse = _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs);
            Assert.IsFalse(secondUse);
            // Sch should still be only +1 (not +2)
            Assert.AreEqual(1, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing));
        }

        [Test]
        public void KaisaActive_ResetForTurnClearsFlags()
        {
            _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs);
            Assert.IsTrue(_gs.PLegend.AbilityUsedThisTurn);

            _legendSys.ResetForTurn(GameRules.OWNER_PLAYER, _gs);
            Assert.IsFalse(_gs.PLegend.AbilityUsedThisTurn);
            Assert.IsFalse(_gs.PLegend.Exhausted);
        }

        [Test]
        public void KaisaActive_CanUseAgainAfterReset()
        {
            _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs);
            _legendSys.ResetForTurn(GameRules.OWNER_PLAYER, _gs);

            bool ok = _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs);
            Assert.IsTrue(ok);
            // Second use gives another +1 Blazing
            Assert.AreEqual(2, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing));
        }

        [Test]
        public void KaisaActive_FailsWhenDead()
        {
            _gs.PLegend.TakeDamage(GameRules.LEGEND_HP);
            bool ok = _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs);
            Assert.IsFalse(ok);
        }

        // ── Kaisa passive: 进化 ───────────────────────────────────────────────

        private UnitInstance MakeUnit(CardKeyword kw)
        {
            var data = UnityEngine.ScriptableObject.CreateInstance<CardData>();
            data.EditorSetup("test_" + kw, "TestUnit", 1, 1, RuneType.Blazing, 0, "", kw, "");
            return new UnitInstance(GameState.NextUid(), data, GameRules.OWNER_PLAYER);
        }

        [Test]
        public void KaisaEvolution_NoTriggerWithFewKeywords()
        {
            // 3 distinct keywords — not enough
            _gs.PBase.Add(MakeUnit(CardKeyword.Haste));
            _gs.PBase.Add(MakeUnit(CardKeyword.Barrier));
            _gs.PBase.Add(MakeUnit(CardKeyword.SpellShield));

            _legendSys.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(1, _gs.PLegend.Level);
        }

        [Test]
        public void KaisaEvolution_TriggersAtFourDistinctKeywords()
        {
            _gs.PBase.Add(MakeUnit(CardKeyword.Haste));
            _gs.PBase.Add(MakeUnit(CardKeyword.Barrier));
            _gs.PBase.Add(MakeUnit(CardKeyword.SpellShield));
            _gs.PBase.Add(MakeUnit(CardKeyword.Deathwish));

            _legendSys.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(2, _gs.PLegend.Level);
        }

        [Test]
        public void KaisaEvolution_AtkAndHpIncrease()
        {
            int atkBefore = _gs.PLegend.Atk;
            int hpBefore  = _gs.PLegend.MaxHp;

            _gs.PBase.Add(MakeUnit(CardKeyword.Haste));
            _gs.PBase.Add(MakeUnit(CardKeyword.Barrier));
            _gs.PBase.Add(MakeUnit(CardKeyword.SpellShield));
            _gs.PBase.Add(MakeUnit(CardKeyword.Deathwish));

            _legendSys.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(atkBefore + 3, _gs.PLegend.Atk);
            Assert.AreEqual(hpBefore  + 3, _gs.PLegend.MaxHp);
        }

        [Test]
        public void KaisaEvolution_DoesNotTriggerTwice()
        {
            _gs.PBase.Add(MakeUnit(CardKeyword.Haste));
            _gs.PBase.Add(MakeUnit(CardKeyword.Barrier));
            _gs.PBase.Add(MakeUnit(CardKeyword.SpellShield));
            _gs.PBase.Add(MakeUnit(CardKeyword.Deathwish));

            _legendSys.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);
            int atkAfterFirst = _gs.PLegend.Atk;

            _legendSys.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(atkAfterFirst, _gs.PLegend.Atk); // no second +3
            Assert.AreEqual(2, _gs.PLegend.Level);
        }

        [Test]
        public void KaisaEvolution_SameKeywordDoesNotCountTwice()
        {
            // 3 distinct keywords via 4 units (one duplicate keyword)
            _gs.PBase.Add(MakeUnit(CardKeyword.Haste));
            _gs.PBase.Add(MakeUnit(CardKeyword.Haste)); // duplicate
            _gs.PBase.Add(MakeUnit(CardKeyword.Barrier));
            _gs.PBase.Add(MakeUnit(CardKeyword.SpellShield));

            _legendSys.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(1, _gs.PLegend.Level); // still only 3 distinct
        }

        // ── Masteryi passive: 独影剑鸣 ─────────────────────────────────────────

        private UnitInstance MakeBasicUnit(string owner)
        {
            var data = UnityEngine.ScriptableObject.CreateInstance<CardData>();
            data.EditorSetup("basic", "BasicUnit", 1, 3, RuneType.Verdant, 0, "", CardKeyword.None, "");
            return new UnitInstance(GameState.NextUid(), data, owner);
        }

        [Test]
        public void MasteryiPassive_BuffsLoneDefender()
        {
            var defUnit = MakeBasicUnit(GameRules.OWNER_ENEMY);
            _gs.BF[0].EnemyUnits.Add(defUnit);

            var attUnit = MakeBasicUnit(GameRules.OWNER_PLAYER);
            _gs.BF[0].PlayerUnits.Add(attUnit);

            int before = defUnit.TempAtkBonus;
            _legendSys.TryApplyMasteryiPassive(0, GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(before + 2, defUnit.TempAtkBonus);
        }

        [Test]
        public void MasteryiPassive_NoBuff_WhenTwoDefenders()
        {
            var def1 = MakeBasicUnit(GameRules.OWNER_ENEMY);
            var def2 = MakeBasicUnit(GameRules.OWNER_ENEMY);
            _gs.BF[0].EnemyUnits.Add(def1);
            _gs.BF[0].EnemyUnits.Add(def2);

            _legendSys.TryApplyMasteryiPassive(0, GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(0, def1.TempAtkBonus);
            Assert.AreEqual(0, def2.TempAtkBonus);
        }

        [Test]
        public void MasteryiPassive_NoBuff_WhenEnemyLegendDead()
        {
            _gs.ELegend.TakeDamage(GameRules.LEGEND_HP); // kill masteryi

            var defUnit = MakeBasicUnit(GameRules.OWNER_ENEMY);
            _gs.BF[0].EnemyUnits.Add(defUnit);

            _legendSys.TryApplyMasteryiPassive(0, GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(0, defUnit.TempAtkBonus);
        }

        // ── Legend death check ─────────────────────────────────────────────────

        [Test]
        public void CheckLegendDeaths_NullWhenBothAlive()
        {
            string result = _legendSys.CheckLegendDeaths(_gs);
            Assert.IsNull(result);
        }

        [Test]
        public void CheckLegendDeaths_ReturnsMessageWhenPlayerLegendDies()
        {
            _gs.PLegend.TakeDamage(GameRules.LEGEND_HP);
            string result = _legendSys.CheckLegendDeaths(_gs);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("AI"));
        }

        [Test]
        public void CheckLegendDeaths_ReturnsMessageWhenEnemyLegendDies()
        {
            _gs.ELegend.TakeDamage(GameRules.LEGEND_HP);
            string result = _legendSys.CheckLegendDeaths(_gs);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("玩家"));
        }
    }
}
