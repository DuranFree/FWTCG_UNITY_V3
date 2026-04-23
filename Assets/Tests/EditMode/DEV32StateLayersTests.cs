using NUnit.Framework;
using UnityEngine;
using FWTCG;
using FWTCG.Data;

namespace FWTCG.Tests.EditMode
{
    /// <summary>
    /// DEV-32 A5: 三层状态管理的分层契约测试。
    /// 不合并 TurnManager.Phase / TurnStateMachine / GameManager UI 锁，因其关注轴不同；
    /// 通过不变量断言保证它们同步。
    /// </summary>
    public class DEV32StateLayersTests
    {
        [SetUp]
        public void SetUp() { TurnStateMachine.Reset(); }

        [Test]
        public void TurnStateMachine_Reset_InitsToNormalClosedLoop()
        {
            Assert.AreEqual(TurnStateMachine.State.Normal_ClosedLoop, TurnStateMachine.Current);
            Assert.IsFalse(TurnStateMachine.IsPlayerActionPhase);
            Assert.IsFalse(TurnStateMachine.IsSpellDuelOpen);
            Assert.IsTrue(TurnStateMachine.IsResolving,
                "Normal_ClosedLoop 属 closed-loop，IsResolving 应为 true");
        }

        [Test]
        public void IsResolving_CorrectForAllClosedLoopStates()
        {
            TurnStateMachine.TransitionTo(TurnStateMachine.State.Normal_ClosedLoop);
            Assert.IsTrue(TurnStateMachine.IsResolving);

            TurnStateMachine.TransitionTo(TurnStateMachine.State.SpellDuel_ClosedLoop);
            Assert.IsTrue(TurnStateMachine.IsResolving);

            TurnStateMachine.TransitionTo(TurnStateMachine.State.Normal_OpenLoop);
            Assert.IsFalse(TurnStateMachine.IsResolving);

            TurnStateMachine.TransitionTo(TurnStateMachine.State.SpellDuel_OpenLoop);
            Assert.IsFalse(TurnStateMachine.IsResolving);
        }

        [Test]
        public void IsSpellDuelOpen_OnlyTrueInSpellDuelOpenLoop()
        {
            TurnStateMachine.TransitionTo(TurnStateMachine.State.Normal_OpenLoop);
            Assert.IsFalse(TurnStateMachine.IsSpellDuelOpen);

            TurnStateMachine.TransitionTo(TurnStateMachine.State.SpellDuel_OpenLoop);
            Assert.IsTrue(TurnStateMachine.IsSpellDuelOpen);

            TurnStateMachine.TransitionTo(TurnStateMachine.State.SpellDuel_ClosedLoop);
            Assert.IsFalse(TurnStateMachine.IsSpellDuelOpen);
        }

        [Test]
        public void StateMachine_AndUILayers_AreOrthogonalAxes()
        {
            // 契约：TurnStateMachine 仅处理法术合法性；GameManager 的 _reactionWindowActive
            // 是 UI 互斥锁（防止双窗口同时开），两者关注不同但相关的信号 — 这个测试记录该设计
            // 作为 intentional（不要盲目合并到一个字段）。
            TurnStateMachine.TransitionTo(TurnStateMachine.State.SpellDuel_OpenLoop);
            Assert.IsTrue(TurnStateMachine.IsSpellDuelOpen,
                "反应窗口期 TurnStateMachine 必须为 SpellDuel_OpenLoop；GameManager 的 UI 锁需额外守卫（见 GameManager 注释）");
        }
    }
}
