using NUnit.Framework;
using System.Collections.Generic;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;

namespace FWTCG.Tests
{
    /// <summary>
    /// Legend system tests — 对齐贴图原文:
    ///   卡莎·虚空之女（OGN-247）: 横置反应 → 获得 1 任意符能，仅可用于打出法术
    ///   无极剑圣（OGS-019）: 持续被动 — 防守单位仅 1 名时 +2
    /// 备注: "进化"机制原卡不存在，已废弃；相关测试删除。
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
            _gs.Turn  = GameRules.OWNER_PLAYER;
            _gs.Phase = GameRules.PHASE_ACTION;

            _legendSys = new UnityEngine.GameObject("LegendSys")
                .AddComponent<LegendSystem>();

            _gs.PLegend = _legendSys.CreateLegend(LegendSystem.KAISA_LEGEND_ID, GameRules.OWNER_PLAYER);
            _gs.ELegend = _legendSys.CreateLegend(LegendSystem.YI_LEGEND_ID,    GameRules.OWNER_ENEMY);
        }

        // ── LegendInstance 数据 ───────────────────────────────────────────────

        [Test]
        public void PLegend_InitializesCorrectly()
        {
            Assert.AreEqual(LegendSystem.KAISA_LEGEND_ID, _gs.PLegend.Id);
            Assert.IsFalse(_gs.PLegend.Exhausted);
            Assert.IsFalse(_gs.PLegend.AbilityUsedThisTurn);
            Assert.AreEqual(GameRules.OWNER_PLAYER, _gs.PLegend.Owner);
        }

        [Test]
        public void ELegend_InitializesCorrectly()
        {
            Assert.AreEqual(LegendSystem.YI_LEGEND_ID, _gs.ELegend.Id);
            Assert.AreEqual(GameRules.OWNER_ENEMY, _gs.ELegend.Owner);
        }

        [Test]
        public void GetLegend_ReturnsCorrectInstance()
        {
            Assert.AreEqual(_gs.PLegend, _gs.GetLegend(GameRules.OWNER_PLAYER));
            Assert.AreEqual(_gs.ELegend, _gs.GetLegend(GameRules.OWNER_ENEMY));
        }

        // ── Kaisa 横置激活 → 1 任意符能进专用池 ────────────────────────────────

        [Test]
        public void KaisaActive_AddsSpellOnlySch_ChosenColor()
        {
            // 选择灵光色
            int before = _gs.GetSpellOnlySch(GameRules.OWNER_PLAYER, RuneType.Radiant);
            bool ok = _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs, RuneType.Radiant);
            Assert.IsTrue(ok);
            Assert.AreEqual(before + 1,
                _gs.GetSpellOnlySch(GameRules.OWNER_PLAYER, RuneType.Radiant));
            // 主池不变
            Assert.AreEqual(0, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Radiant));
        }

        [Test]
        public void KaisaActive_SpellOnlyPool_CanBeSpentOnSpells()
        {
            _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs, RuneType.Blazing);
            Assert.AreEqual(1, _gs.GetTotalSch(GameRules.OWNER_PLAYER, RuneType.Blazing),
                "Total = main(0) + spellOnly(1)");

            // 法术支付时优先扣 spell-only
            _gs.SpendSchForSpell(GameRules.OWNER_PLAYER, RuneType.Blazing, 1);
            Assert.AreEqual(0, _gs.GetSpellOnlySch(GameRules.OWNER_PLAYER, RuneType.Blazing));
            Assert.AreEqual(0, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing));
        }

        [Test]
        public void KaisaActive_ExhaustsLegend()
        {
            _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs, RuneType.Blazing);
            Assert.IsTrue(_gs.PLegend.Exhausted);
            Assert.IsTrue(_gs.PLegend.AbilityUsedThisTurn);
        }

        [Test]
        public void KaisaActive_CannotUseAgainSameTurn()
        {
            _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs, RuneType.Blazing);
            bool secondUse = _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs, RuneType.Blazing);
            Assert.IsFalse(secondUse);
            Assert.AreEqual(1, _gs.GetSpellOnlySch(GameRules.OWNER_PLAYER, RuneType.Blazing));
        }

        [Test]
        public void KaisaActive_ResetForTurnClearsFlags()
        {
            _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs, RuneType.Blazing);
            _legendSys.ResetForTurn(GameRules.OWNER_PLAYER, _gs);
            Assert.IsFalse(_gs.PLegend.AbilityUsedThisTurn);
            Assert.IsFalse(_gs.PLegend.Exhausted);
        }

        [Test]
        public void KaisaActive_CanUseAgainAfterReset()
        {
            _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs, RuneType.Radiant);
            _legendSys.ResetForTurn(GameRules.OWNER_PLAYER, _gs);

            bool ok = _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs, RuneType.Radiant);
            Assert.IsTrue(ok);
            Assert.AreEqual(2, _gs.GetSpellOnlySch(GameRules.OWNER_PLAYER, RuneType.Radiant));
        }

        // ── Yi 被动 IsYiSoloDefender ─────────────────────────────────────────

        private UnitInstance MakeBasicUnit(string owner)
        {
            var data = UnityEngine.ScriptableObject.CreateInstance<CardData>();
            data.EditorSetup("basic", "BasicUnit", 1, 3, RuneType.Verdant, 0, "", CardKeyword.None, "");
            return new UnitInstance(GameState.NextUid(), data, owner);
        }

        [Test]
        public void Yi_IsSoloDefender_True_WhenSingleFriendlyUnit()
        {
            var defUnit = MakeBasicUnit(GameRules.OWNER_ENEMY);
            _gs.BF[0].EnemyUnits.Add(defUnit);
            _gs.BF[0].PlayerUnits.Add(MakeBasicUnit(GameRules.OWNER_PLAYER));

            Assert.IsTrue(LegendSystem.IsYiSoloDefender(defUnit, 0, GameRules.OWNER_ENEMY, _gs));
        }

        [Test]
        public void Yi_IsSoloDefender_False_WhenTwoFriendlyUnits()
        {
            var d1 = MakeBasicUnit(GameRules.OWNER_ENEMY);
            var d2 = MakeBasicUnit(GameRules.OWNER_ENEMY);
            _gs.BF[0].EnemyUnits.Add(d1);
            _gs.BF[0].EnemyUnits.Add(d2);

            Assert.IsFalse(LegendSystem.IsYiSoloDefender(d1, 0, GameRules.OWNER_ENEMY, _gs));
            Assert.IsFalse(LegendSystem.IsYiSoloDefender(d2, 0, GameRules.OWNER_ENEMY, _gs));
        }

        [Test]
        public void Yi_IsSoloDefender_False_WhenWrongLegendOwner()
        {
            // 玩家方（Kaisa 阵营）单独防守 — Yi 被动不适用
            var unit = MakeBasicUnit(GameRules.OWNER_PLAYER);
            _gs.BF[0].PlayerUnits.Add(unit);
            Assert.IsFalse(LegendSystem.IsYiSoloDefender(unit, 0, GameRules.OWNER_PLAYER, _gs));
        }
    }
}
