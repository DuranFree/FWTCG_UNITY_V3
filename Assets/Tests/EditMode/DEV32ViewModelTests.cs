using NUnit.Framework;
using FWTCG.Core;
using FWTCG.UI;

namespace FWTCG.Tests.EditMode
{
    /// <summary>
    /// DEV-32 A2: GameStateViewModel 骨架测试。
    /// 验证代理层正确转发 GameState / ScoreManager 事件，按 owner 分发；Dispose 解绑。
    /// </summary>
    public class DEV32ViewModelTests
    {
        [Test]
        public void Construct_NullGameState_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new GameStateViewModel(null));
        }

        [Test]
        public void ManaChanged_RoutedByOwner()
        {
            var gs = new GameState();
            var vm = new GameStateViewModel(gs);
            try
            {
                int playerCalls = 0, enemyCalls = 0;
                int lastPlayerVal = -1, lastEnemyVal = -1;
                vm.OnPlayerManaChanged += v => { playerCalls++; lastPlayerVal = v; };
                vm.OnEnemyManaChanged  += v => { enemyCalls++;  lastEnemyVal  = v; };

                gs.AddMana(GameRules.OWNER_PLAYER, 3);
                Assert.AreEqual(1, playerCalls); Assert.AreEqual(0, enemyCalls);
                Assert.AreEqual(3, lastPlayerVal);

                gs.AddMana(GameRules.OWNER_ENEMY, 5);
                Assert.AreEqual(1, playerCalls); Assert.AreEqual(1, enemyCalls);
                Assert.AreEqual(5, lastEnemyVal);
            }
            finally { vm.Dispose(); }
        }

        [Test]
        public void Dispose_UnsubscribesFromGameState()
        {
            var gs = new GameState();
            var vm = new GameStateViewModel(gs);
            int calls = 0;
            vm.OnPlayerManaChanged += _ => calls++;

            gs.AddMana(GameRules.OWNER_PLAYER, 1);
            Assert.AreEqual(1, calls);

            vm.Dispose();
            gs.AddMana(GameRules.OWNER_PLAYER, 1);
            Assert.AreEqual(1, calls, "Dispose 后不应再触发 VM 事件");
        }

        [Test]
        public void SyncQueries_ReflectCurrentState()
        {
            var gs = new GameState();
            gs.PMana = 7; gs.EMana = 4; gs.PScore = 3; gs.EScore = 2;
            var vm = new GameStateViewModel(gs);
            try
            {
                Assert.AreEqual(7, vm.PlayerMana);
                Assert.AreEqual(4, vm.EnemyMana);
                Assert.AreEqual(3, vm.PlayerScore);
                Assert.AreEqual(2, vm.EnemyScore);
                Assert.IsFalse(vm.GameOver);
            }
            finally { vm.Dispose(); }
        }

        [Test]
        public void DisposeIdempotent_DoubleDisposeNoThrow()
        {
            var gs = new GameState();
            var vm = new GameStateViewModel(gs);
            vm.Dispose();
            Assert.DoesNotThrow(() => vm.Dispose());
        }
    }
}
