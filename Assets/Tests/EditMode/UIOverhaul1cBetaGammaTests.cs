using NUnit.Framework;
using UnityEngine;
using FWTCG;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Tests
{
    /// <summary>
    /// UI-OVERHAUL-1c-β / 1c-γ: 确定按钮延迟 combat + 取消按钮 LIFO 回滚。
    /// 这里只覆盖骨架层面可纯逻辑测试的：
    ///   - OnCancelClicked 回滚 HandToBase：unit 回手 + mana/sch 还原 + Tap/Recycle 撤销
    ///   - OnCancelClicked 回滚 BaseToBF：unit 回基地
    ///   - OnCancelClicked 多条 LIFO
    /// combat 触发 / Haste 自动判定 / AI 适配的端到端路径依赖场景 + DOTween，由 Play Mode 人工验收。
    /// </summary>
    [TestFixture]
    public class UIOverhaul1cBetaGammaTests
    {
        private GameManager _gm;
        private GameState _gs;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestGM_1cβ");
            _gm = go.AddComponent<GameManager>();
            _gs = new GameState
            {
                Turn  = GameRules.OWNER_PLAYER,
                Phase = GameRules.PHASE_ACTION,
                PMana = 0,
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

        private CardData MakeUnit(int cost, int runeCost, RuneType type)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup("u", "U", cost, 2, type, runeCost, "");
            return cd;
        }

        private UnitInstance MakeUnitInstance(int cost, int runeCost, RuneType type)
            => new UnitInstance(0, MakeUnit(cost, runeCost, type), GameRules.OWNER_PLAYER);

        // ── OnCancelClicked 回滚 HandToBase ──────────────────────────────────
        [Test]
        public void OnCancelClicked_HandToBase_RestoresHandAndMana()
        {
            var unit = MakeUnitInstance(3, 0, RuneType.Blazing);
            _gs.PBase.Add(unit);
            _gs.PMana = 0; // 模拟：已扣 3 法力

            _gm.RecordPlayAction(new GameManager.PlayStackEntry
            {
                Kind      = GameManager.PlayActionKind.HandToBase,
                Unit      = unit,
                ManaSpent = 3,
            });

            _gm.OnCancelClicked();

            Assert.IsFalse(_gs.PBase.Contains(unit), "回滚后 unit 应离开 PBase");
            Assert.IsTrue(_gs.PHand.Contains(unit), "回滚后 unit 应回到 PHand");
            Assert.AreEqual(3, _gs.PMana, "回滚后 mana 应 +3");
            Assert.IsFalse(_gm.HasThisTurnPlayActions(), "栈应清空");
        }

        [Test]
        public void OnCancelClicked_HandToBase_RestoresPrimarySch()
        {
            var unit = MakeUnitInstance(1, 2, RuneType.Blazing);
            _gs.PBase.Add(unit);
            _gs.PMana = 0;

            _gm.RecordPlayAction(new GameManager.PlayStackEntry
            {
                Kind            = GameManager.PlayActionKind.HandToBase,
                Unit            = unit,
                ManaSpent       = 1,
                PrimaryType     = RuneType.Blazing,
                PrimarySchSpent = 2,
            });

            _gm.OnCancelClicked();

            Assert.AreEqual(2, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing),
                "回滚后 Blazing 符能应 +2");
        }

        [Test]
        public void OnCancelClicked_HandToBase_UndoesCommittedTapAndRecycle()
        {
            var unit = MakeUnitInstance(1, 0, RuneType.Blazing);
            // 预置两个 rune：一个 Tapped（模拟 commit 时 tap），一个已回收到 deck
            var tappedRune   = new RuneInstance(10, RuneType.Radiant) { Tapped = true };
            var recycledRune = new RuneInstance(11, RuneType.Verdant);
            _gs.PRunes.Add(tappedRune);
            _gs.PRuneDeck.Add(recycledRune);
            _gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Verdant, 1); // 模拟 recycle 增 1
            _gs.PMana = 1; // 模拟 tap 加了 1

            _gs.PBase.Add(unit);
            _gm.RecordPlayAction(new GameManager.PlayStackEntry
            {
                Kind                = GameManager.PlayActionKind.HandToBase,
                Unit                = unit,
                ManaSpent           = 0,
                CommittedTappedUids = new System.Collections.Generic.List<int> { 10 },
                CommittedRecycled   = new System.Collections.Generic.List<RuneInstance> { recycledRune },
            });

            _gm.OnCancelClicked();

            Assert.IsFalse(tappedRune.Tapped, "Tapped 应还原");
            Assert.AreEqual(0, _gs.PMana, "tap 带来的 +1 mana 应回撤");
            Assert.IsTrue(_gs.PRunes.Contains(recycledRune), "recycled rune 应回到 PRunes");
            Assert.IsFalse(_gs.PRuneDeck.Contains(recycledRune), "recycled rune 应从 PRuneDeck 移除");
            Assert.AreEqual(0, _gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Verdant),
                "Verdant 符能应回撤");
        }

        // ── OnCancelClicked 回滚 BaseToBF ─────────────────────────────────────
        [Test]
        public void OnCancelClicked_BaseToBF_RestoresBase()
        {
            var unit = MakeUnitInstance(1, 0, RuneType.Blazing);
            _gs.BF[0].PlayerUnits.Add(unit);
            unit.Exhausted = true; // 派遣后休眠

            _gm.RecordPlayAction(new GameManager.PlayStackEntry
            {
                Kind    = GameManager.PlayActionKind.BaseToBF,
                Unit    = unit,
                BFIndex = 0,
            });

            _gm.OnCancelClicked();

            Assert.IsFalse(_gs.BF[0].PlayerUnits.Contains(unit), "回滚后 unit 应离开 BF[0]");
            Assert.IsTrue(_gs.PBase.Contains(unit), "回滚后 unit 应回到 PBase");
            Assert.IsFalse(unit.Exhausted, "派遣前未休眠，回滚应恢复未休眠");
        }

        // ── LIFO 多条回滚 ─────────────────────────────────────────────────────
        [Test]
        public void OnCancelClicked_MultipleEntries_RollbackAll()
        {
            var unit1 = MakeUnitInstance(1, 0, RuneType.Blazing);
            var unit2 = MakeUnitInstance(2, 0, RuneType.Blazing);
            _gs.PBase.Add(unit1);
            _gs.BF[0].PlayerUnits.Add(unit2);
            _gs.PMana = 0;

            _gm.RecordPlayAction(new GameManager.PlayStackEntry
            {
                Kind = GameManager.PlayActionKind.HandToBase, Unit = unit1, ManaSpent = 1,
            });
            _gm.RecordPlayAction(new GameManager.PlayStackEntry
            {
                Kind = GameManager.PlayActionKind.BaseToBF, Unit = unit2, BFIndex = 0,
            });

            _gm.OnCancelClicked();

            Assert.IsTrue(_gs.PHand.Contains(unit1), "unit1 应回手");
            Assert.IsTrue(_gs.PBase.Contains(unit2), "unit2 应回基地");
            Assert.IsFalse(_gs.BF[0].PlayerUnits.Contains(unit2), "BF[0] 应清空");
            Assert.AreEqual(1, _gs.PMana, "mana 还原");
            Assert.IsFalse(_gm.HasThisTurnPlayActions(), "栈清空");
        }

        // ── OnConfirmClicked 空战场拒绝 ─────────────────────────────────────
        [Test]
        public void OnConfirmClicked_NoUnitOnBattlefield_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _gm.OnConfirmClicked(),
                "无我方单位时点击确定应安全返回");
            Assert.IsFalse(_gm.HasThisTurnPlayActions(), "栈应保持空");
        }

        // ── HeroToBase 回滚 ─────────────────────────────────────────────────
        [Test]
        public void OnCancelClicked_HeroToBase_RestoresHero()
        {
            var hero = MakeUnitInstance(2, 0, RuneType.Radiant);
            _gs.PBase.Add(hero);
            _gs.PMana = 0;

            _gm.RecordPlayAction(new GameManager.PlayStackEntry
            {
                Kind      = GameManager.PlayActionKind.HeroToBase,
                Unit      = hero,
                ManaSpent = 2,
            });

            _gm.OnCancelClicked();

            Assert.IsFalse(_gs.PBase.Contains(hero));
            Assert.AreSame(hero, _gs.PHero, "PHero 应恢复");
            Assert.AreEqual(2, _gs.PMana);
        }
    }
}
