using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.UI;

namespace FWTCG.Tests.EditMode
{
    /// <summary>
    /// DEV-32 A4: 验证 IReactionWindow 接口可 mock，GameManager.InjectReactionWindow 正确替换依赖。
    /// </summary>
    public class DEV32ReactionWindowTests
    {
        /// <summary>最小 mock：按预设返回 card / null。</summary>
        private class MockReactionWindow : IReactionWindow
        {
            public UnitInstance NextResult;
            public int WaitForReactionCalls;
            public int SkipReactionCalls;
            public List<UnitInstance> LastCards;
            public string LastContextMsg;

            public Task<UnitInstance> WaitForReaction(
                List<UnitInstance> cards, string contextMsg, GameState gs,
                Action<UnitInstance> onHoverEnter = null,
                Action<UnitInstance> onHoverExit = null)
            {
                WaitForReactionCalls++;
                LastCards = cards;
                LastContextMsg = contextMsg;
                return Task.FromResult(NextResult);
            }

            public void SkipReaction() { SkipReactionCalls++; }
        }

        [Test]
        public void IReactionWindow_CanBeMocked_WithoutMonoBehaviour()
        {
            var mock = new MockReactionWindow();
            Assert.IsInstanceOf<IReactionWindow>(mock, "MockReactionWindow 应实现 IReactionWindow");
        }

        [Test]
        public async Task MockWaitForReaction_ReturnsPresetResult()
        {
            var mock = new MockReactionWindow { NextResult = null };
            var task = mock.WaitForReaction(new List<UnitInstance>(), "test", new GameState());
            var result = await task;
            Assert.IsNull(result, "mock 应返回 null（玩家跳过）");
            Assert.AreEqual(1, mock.WaitForReactionCalls);
        }

        [Test]
        public void MockSkipReaction_IncrementsCounter()
        {
            var mock = new MockReactionWindow();
            mock.SkipReaction();
            mock.SkipReaction();
            Assert.AreEqual(2, mock.SkipReactionCalls);
        }

        [Test]
        public void GameManager_InjectReactionWindow_Replaces_ReactiveWindowUI()
        {
            var go = new GameObject("TestGM_A4");
            try
            {
                var gm = go.AddComponent<GameManager>();
                var mock = new MockReactionWindow();
                gm.InjectReactionWindow(mock);

                // 通过反射检查内部字段
                var field = typeof(GameManager).GetField("_reactionWindow",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(field, "_reactionWindow 内部字段应存在");
                Assert.AreSame(mock, field.GetValue(gm),
                    "InjectReactionWindow 应将 mock 写入 _reactionWindow");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ReactiveWindowUI_Implements_IReactionWindow()
        {
            // 编译时契约 —— 若 ReactiveWindowUI 去除 IReactionWindow 继承此测试编译失败
            Assert.IsTrue(typeof(IReactionWindow).IsAssignableFrom(typeof(ReactiveWindowUI)),
                "ReactiveWindowUI 必须 implement IReactionWindow");
        }
    }
}
