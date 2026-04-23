using NUnit.Framework;
using FWTCG.Core;

namespace FWTCG.Tests.EditMode
{
    /// <summary>
    /// DEV-32 A1: GameState facade — 可观察变更事件测试。
    /// 为 A2 UI ViewModel 奠基：订阅事件驱动 UI 而非 RefreshUI() 手动同步。
    /// </summary>
    public class DEV32GameStateFacadeTests
    {
        [Test]
        public void AddMana_FiresOnManaChanged_WithOldAndNewValues()
        {
            var gs = new GameState();
            gs.PMana = 5;

            string receivedOwner = null;
            int receivedOld = -1, receivedNew = -1;
            gs.OnManaChanged += (owner, oldVal, newVal) =>
            {
                receivedOwner = owner; receivedOld = oldVal; receivedNew = newVal;
            };

            gs.AddMana(GameRules.OWNER_PLAYER, 3);

            Assert.AreEqual(GameRules.OWNER_PLAYER, receivedOwner);
            Assert.AreEqual(5, receivedOld);
            Assert.AreEqual(8, receivedNew);
            Assert.AreEqual(8, gs.PMana);
        }

        [Test]
        public void AddMana_Zero_NoEvent()
        {
            var gs = new GameState();
            int callCount = 0;
            gs.OnManaChanged += (_, __, ___) => callCount++;
            gs.AddMana(GameRules.OWNER_PLAYER, 0);
            Assert.AreEqual(0, callCount, "amount=0 时不应触发事件");
        }

        [Test]
        public void SetMana_FiresEvent_WhenValueChanges()
        {
            var gs = new GameState();
            gs.EMana = 2;
            int eventCount = 0;
            gs.OnManaChanged += (_, __, ___) => eventCount++;
            gs.SetMana(GameRules.OWNER_ENEMY, 7);
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(7, gs.EMana);
        }

        [Test]
        public void SetMana_SameValue_NoEvent()
        {
            var gs = new GameState();
            gs.EMana = 3;
            int eventCount = 0;
            gs.OnManaChanged += (_, __, ___) => eventCount++;
            gs.SetMana(GameRules.OWNER_ENEMY, 3);
            Assert.AreEqual(0, eventCount, "SetMana 相同值不应触发事件");
        }

        [Test]
        public void DirectFieldAssignment_DoesNotFireEvent()
        {
            // 记录设计：直接赋值 PMana/EMana 不走 API，不会 Fire 事件
            // 已迁移的核心 mutation 走 AddMana / SetMana；直接赋值保留为初始化/测试用法
            var gs = new GameState();
            int count = 0;
            gs.OnManaChanged += (_, __, ___) => count++;
            gs.PMana = 10;
            Assert.AreEqual(0, count,
                "直接赋值 PMana 不 Fire 事件（intentional：仅 API 调用触发）");
        }
    }
}
