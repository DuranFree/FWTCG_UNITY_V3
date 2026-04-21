using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using FWTCG;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// UI-OVERHAUL-1b: 符文标记 + CommitPreparedRunes + 校验逻辑测试。
    /// 通过反射调用 GameManager 私有方法；不依赖场景 UI。
    /// </summary>
    [TestFixture]
    public class UIOverhaul1bTests
    {
        private GameManager _gm;
        private GameState _gs;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestGM");
            _gm = go.AddComponent<GameManager>();

            _gs = new GameState();
            // 最小可用状态：玩家行动阶段 + 3 个未横置符文
            _gs.Turn  = GameRules.OWNER_PLAYER;
            _gs.Phase = GameRules.PHASE_ACTION;
            _gs.PMana = 0;
            _gs.PRunes.Add(new RuneInstance(0, RuneType.Blazing));
            _gs.PRunes.Add(new RuneInstance(1, RuneType.Blazing));
            _gs.PRunes.Add(new RuneInstance(2, RuneType.Radiant));

            // 注入 _gs 到 GameManager 私有字段
            SetPrivate(_gm, "_gs", _gs);
        }

        [TearDown]
        public void TearDown()
        {
            if (_gm != null) Object.DestroyImmediate(_gm.gameObject);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(f, $"字段 {field} 应存在");
            f.SetValue(obj, value);
        }

        private static object InvokePrivate(object obj, string method, params object[] args)
        {
            var m = obj.GetType().GetMethod(method,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(m, $"方法 {method} 应存在");
            return m.Invoke(obj, args);
        }

        // ── OnRuneClicked toggle 行为 ──────────────────────────────────────────

        [Test]
        public void OnRuneClicked_LeftClick_MarksAsTap()
        {
            _gm.OnRuneClicked(0, false);
            var tap = _gm.GetPreparedTapIdxs();
            Assert.IsTrue(tap.Contains(0));
            Assert.IsFalse(_gm.GetPreparedRecycleIdxs().Contains(0));
        }

        [Test]
        public void OnRuneClicked_RightClick_MarksAsRecycle()
        {
            _gm.OnRuneClicked(0, true);
            var rec = _gm.GetPreparedRecycleIdxs();
            Assert.IsTrue(rec.Contains(0));
            Assert.IsFalse(_gm.GetPreparedTapIdxs().Contains(0));
        }

        [Test]
        public void OnRuneClicked_SameLeftClick_Toggles()
        {
            _gm.OnRuneClicked(1, false);
            Assert.IsTrue(_gm.GetPreparedTapIdxs().Contains(1));
            _gm.OnRuneClicked(1, false);
            Assert.IsFalse(_gm.GetPreparedTapIdxs().Contains(1), "第二次左键应取消标记");
        }

        [Test]
        public void OnRuneClicked_LeftThenRight_Switches()
        {
            _gm.OnRuneClicked(2, false);
            _gm.OnRuneClicked(2, true);
            Assert.IsFalse(_gm.GetPreparedTapIdxs().Contains(2), "右键应清除 tap 标记");
            Assert.IsTrue(_gm.GetPreparedRecycleIdxs().Contains(2), "右键应加 recycle 标记");
        }

        [Test]
        public void OnRuneClicked_TappedRune_DoesNotMark()
        {
            _gs.PRunes[0].Tapped = true;
            _gm.OnRuneClicked(0, false);
            Assert.IsFalse(_gm.GetPreparedTapIdxs().Contains(0), "已横置符文不能被再次标记");
        }

        [Test]
        public void OnRuneClicked_OutOfRange_Ignored()
        {
            _gm.OnRuneClicked(-1, false);
            _gm.OnRuneClicked(99, true);
            Assert.AreEqual(0, _gm.GetPreparedTapIdxs().Count);
            Assert.AreEqual(0, _gm.GetPreparedRecycleIdxs().Count);
        }

        // ── ClearPreparedRunes ────────────────────────────────────────────────

        [Test]
        public void ClearPreparedRunes_ClearsBothSets()
        {
            _gm.OnRuneClicked(0, false);
            _gm.OnRuneClicked(2, true);
            _gm.ClearPreparedRunes();
            Assert.AreEqual(0, _gm.GetPreparedTapIdxs().Count);
            Assert.AreEqual(0, _gm.GetPreparedRecycleIdxs().Count);
        }

        // ── CommitPreparedRunes 真实 tap/recycle 效果 ──────────────────────────

        [Test]
        public void CommitPreparedRunes_TapsMarkedRunes_AndGainsMana()
        {
            _gm.OnRuneClicked(0, false); // prepared tap idx 0
            _gm.OnRuneClicked(1, false); // prepared tap idx 1
            int manaBefore = _gs.PMana;

            InvokePrivate(_gm, "CommitPreparedRunes");

            Assert.IsTrue(_gs.PRunes[0].Tapped);
            Assert.IsTrue(_gs.PRunes[1].Tapped);
            Assert.AreEqual(manaBefore + 2, _gs.PMana, "2 个 tap 应 +2 mana");
            Assert.AreEqual(0, _gm.GetPreparedTapIdxs().Count, "commit 后标记清空");
        }

        [Test]
        public void CommitPreparedRunes_RecyclesMarkedRunes_RemovedAndSchGained()
        {
            int originalCount = _gs.PRunes.Count;
            int schBefore = _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Radiant);

            _gm.OnRuneClicked(2, true); // Radiant rune recycle
            InvokePrivate(_gm, "CommitPreparedRunes");

            Assert.AreEqual(originalCount - 1, _gs.PRunes.Count, "回收后符文池少 1");
            Assert.AreEqual(schBefore + 1, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Radiant),
                "Radiant 符能 +1");
            Assert.AreEqual(0, _gm.GetPreparedRecycleIdxs().Count);
        }

        [Test]
        public void CommitPreparedRunes_MixedTapAndRecycle_BothApplied()
        {
            // idx 0: tap (Blazing); idx 2: recycle (Radiant)
            _gm.OnRuneClicked(0, false);
            _gm.OnRuneClicked(2, true);
            int manaBefore   = _gs.PMana;
            int radiantBefore = _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Radiant);

            InvokePrivate(_gm, "CommitPreparedRunes");

            Assert.AreEqual(manaBefore + 1, _gs.PMana);
            Assert.AreEqual(radiantBefore + 1, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Radiant));
        }
    }
}
