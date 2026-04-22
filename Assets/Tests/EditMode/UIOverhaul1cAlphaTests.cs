using NUnit.Framework;
using UnityEngine;
using FWTCG;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Tests
{
    /// <summary>
    /// UI-OVERHAUL-1c-α: 确定按钮亮/暗条件查询骨架测试。
    /// （取消按钮 + 撤销栈已在 2026-04-22 删除，游戏机制不再允许撤销）
    /// </summary>
    [TestFixture]
    public class UIOverhaul1cAlphaTests
    {
        private GameManager _gm;
        private GameState _gs;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestGM_1cα");
            _gm = go.AddComponent<GameManager>();
            _gs = new GameState
            {
                Turn  = GameRules.OWNER_PLAYER,
                Phase = GameRules.PHASE_ACTION,
            };
            typeof(GameManager).GetField("_gs",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_gm, _gs);
        }

        [TearDown]
        public void TearDown()
        {
            if (_gm != null) Object.DestroyImmediate(_gm.gameObject);
        }

        // ── HasAnyPlayerUnitOnBattlefield ─────────────────────────────────────
        [Test]
        public void HasAnyPlayerUnitOnBattlefield_Empty_False()
        {
            Assert.IsFalse(_gm.HasAnyPlayerUnitOnBattlefield(),
                "空战场应返回 false（确定按钮暗淡）");
        }

        [Test]
        public void HasAnyPlayerUnitOnBattlefield_WithUnit_True()
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup("x", "X", 1, 2, RuneType.Blazing, 0, "");
            var unit = new UnitInstance(0, cd, GameRules.OWNER_PLAYER);
            _gs.BF[0].PlayerUnits.Add(unit);

            Assert.IsTrue(_gm.HasAnyPlayerUnitOnBattlefield(),
                "战场有我方单位应返回 true（确定按钮亮色）");
        }

        // ── OnConfirmClicked stub 不抛异常 ──────────────────────────────────
        [Test]
        public void OnConfirmClicked_NoBattlefieldUnits_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _gm.OnConfirmClicked(),
                "空战场时点击确定应安全返回（广播提示，不崩溃）");
        }

        // ── HasPreparedRunes + 确定按钮激活条件 ──────────────────────────────
        [Test]
        public void HasPreparedRunes_Empty_False()
        {
            Assert.IsFalse(_gm.HasPreparedRunes(),
                "无 prepared 符文时应返回 false（确定按钮暗淡）");
        }

        [Test]
        public void HasPreparedRunes_AfterMarkTap_True()
        {
            _gs.PRunes.Add(new RuneInstance(0, RuneType.Blazing));
            _gm.OnRuneClicked(0, recycle: false);

            Assert.IsTrue(_gm.HasPreparedRunes(),
                "标记 tap 后应返回 true（确定按钮亮起）");
        }

        [Test]
        public void HasPreparedRunes_AfterMarkRecycle_True()
        {
            _gs.PRunes.Add(new RuneInstance(0, RuneType.Blazing));
            _gm.OnRuneClicked(0, recycle: true);

            Assert.IsTrue(_gm.HasPreparedRunes(),
                "标记 recycle 后应返回 true（确定按钮亮起）");
        }

        // ── OnConfirmClicked 独立 commit prepared 符文 ────────────────────────
        [Test]
        public void OnConfirmClicked_PreparedTapOnly_CommitsRuneAndGrantsMana()
        {
            _gs.PMana = 0;
            _gs.PRunes.Add(new RuneInstance(0, RuneType.Blazing));
            _gm.OnRuneClicked(0, recycle: false);
            Assert.IsTrue(_gm.HasPreparedRunes());

            _gm.OnConfirmClicked();

            Assert.IsFalse(_gm.HasPreparedRunes(), "commit 后 prepared 集合应清空");
            Assert.IsTrue(_gs.PRunes[0].Tapped, "符文应被真正横置");
            Assert.AreEqual(1, _gs.PMana, "横置应 +1 法力");
        }
    }
}
