using NUnit.Framework;
using UnityEngine;
using FWTCG;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Tests
{
    /// <summary>
    /// UI-OVERHAUL-1c-α: 确定/取消按钮骨架 + 本回合入场栈数据结构测试。
    /// 只覆盖骨架逻辑（栈记录 / 查询 / 清空 + 按钮条件查询）；
    /// 真正的 combat 延迟触发 + 回滚留给 1c-β / 1c-γ。
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
            var unit = new UnitInstance(cd, GameRules.OWNER_PLAYER);
            _gs.BF[0].PlayerUnits.Add(unit);

            Assert.IsTrue(_gm.HasAnyPlayerUnitOnBattlefield(),
                "战场有我方单位应返回 true（确定按钮亮色）");
        }

        // ── HasThisTurnPlayActions / RecordPlayAction / ClearThisTurnPlayStack ─
        [Test]
        public void PlayStack_StartsEmpty()
        {
            Assert.IsFalse(_gm.HasThisTurnPlayActions(), "新回合栈应为空");
        }

        [Test]
        public void RecordPlayAction_IncreasesStack()
        {
            _gm.RecordPlayAction(new GameManager.PlayStackEntry
            {
                Kind = GameManager.PlayActionKind.HandToBase,
                ManaSpent = 2,
            });
            Assert.IsTrue(_gm.HasThisTurnPlayActions(), "记录后栈应非空（取消按钮亮色）");
        }

        [Test]
        public void ClearThisTurnPlayStack_Empties()
        {
            _gm.RecordPlayAction(new GameManager.PlayStackEntry { Kind = GameManager.PlayActionKind.BaseToBF });
            _gm.RecordPlayAction(new GameManager.PlayStackEntry { Kind = GameManager.PlayActionKind.HandToBase });
            _gm.ClearThisTurnPlayStack();
            Assert.IsFalse(_gm.HasThisTurnPlayActions(), "清空后栈应为空");
        }

        // ── OnConfirmClicked / OnCancelClicked stub 不抛异常 ──────────────────
        [Test]
        public void OnConfirmClicked_NoBattlefieldUnits_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _gm.OnConfirmClicked(),
                "空战场时点击确定应安全返回（广播提示，不崩溃）");
        }

        [Test]
        public void OnCancelClicked_EmptyStack_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _gm.OnCancelClicked(),
                "空栈时点击取消应安全返回（广播提示，不崩溃）");
        }

        [Test]
        public void OnCancelClicked_WithStack_ClearsStack_Stub()
        {
            _gm.RecordPlayAction(new GameManager.PlayStackEntry { Kind = GameManager.PlayActionKind.HandToBase });
            _gm.OnCancelClicked();
            Assert.IsFalse(_gm.HasThisTurnPlayActions(), "1c-α stub: 点击取消后栈应清空（实际回滚留给 1c-γ）");
        }
    }
}
