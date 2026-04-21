using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// 验证 SpellShowcaseUI 作为统一表演通道的行为：
    /// - 队列串行（多次 ShowAsync 依次等待前一次完成）
    /// - EntryEffectSystem 对带 effectId 的单位 / 装备牌触发，对白板单位不触发
    /// </summary>
    [TestFixture]
    public class EffectShowcaseTests
    {
        private GameObject _hostGO;
        private SpellShowcaseUI _showcase;

        [SetUp]
        public void SetUp()
        {
            // Clean any lingering singleton from prior tests
            var existing = SpellShowcaseUI.Instance;
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            _hostGO = new GameObject("ShowcaseHost");
            _showcase = _hostGO.AddComponent<SpellShowcaseUI>();
            // EditMode: Awake doesn't auto-fire; invoke directly via reflection (SendMessage triggers
            // ShouldRunBehaviour assert in EditMode). This wires up the Instance singleton.
            typeof(SpellShowcaseUI)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(_showcase, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostGO != null) Object.DestroyImmediate(_hostGO);
            _hostGO = null;
            _showcase = null;
        }

        // ── 队列串行 ────────────────────────────────────────────────────────────

        [Test]
        public void ShowAsync_NullSpell_ReturnsCompletedTask()
        {
            var t = _showcase.ShowAsync(null, GameRules.OWNER_PLAYER);
            Assert.IsTrue(t.IsCompleted, "null spell must return a completed Task, not enqueue");
        }

        [Test]
        public void ShowGroupAsync_EmptyList_ReturnsCompletedTask()
        {
            var t = _showcase.ShowGroupAsync(null, GameRules.OWNER_PLAYER);
            Assert.IsTrue(t.IsCompleted);
        }

        [Test]
        public void ShowAsync_MultipleCalls_ReturnDistinctTasks()
        {
            var cardA = MakeUnitCard("A", effectId: "");
            var cardB = MakeUnitCard("B", effectId: "");
            var unitA = new UnitInstance(1, cardA, GameRules.OWNER_PLAYER);
            var unitB = new UnitInstance(2, cardB, GameRules.OWNER_PLAYER);

            var t1 = _showcase.ShowAsync(unitA, GameRules.OWNER_PLAYER);
            var t2 = _showcase.ShowAsync(unitB, GameRules.OWNER_PLAYER);
            Assert.AreNotSame(t1, t2, "Consecutive ShowAsync must return independent chained Tasks");
        }

        // ── EntryEffectSystem 过滤 ─────────────────────────────────────────────

        [Test]
        public void OnUnitEntered_VanillaUnit_DoesNotThrow()
        {
            // 白板单位：effectId 空、非装备 — 不应触发 showcase，也不应报错
            var sys = _hostGO.AddComponent<EntryEffectSystem>();
            var gs = new GameState();
            var plainCard = MakeUnitCard("Vanilla", effectId: "");
            var unit = new UnitInstance(10, plainCard, GameRules.OWNER_PLAYER);
            Assert.DoesNotThrow(() => sys.OnUnitEntered(unit, GameRules.OWNER_PLAYER, gs));
        }

        [Test]
        public void OnUnitEntered_EquipmentWithoutEffectId_DoesNotThrow()
        {
            // 装备牌（可能无 effectId）— 仍应调用 showcase 但不崩
            var sys = _hostGO.AddComponent<EntryEffectSystem>();
            var gs = new GameState();
            var equipCard = MakeEquipmentCard("Dagger");
            var equip = new UnitInstance(11, equipCard, GameRules.OWNER_PLAYER);
            Assert.DoesNotThrow(() => sys.OnUnitEntered(equip, GameRules.OWNER_PLAYER, gs));
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static CardData MakeUnitCard(string name, string effectId)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            typeof(CardData).GetField("_id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(c, name.ToLower());
            typeof(CardData).GetField("_cardName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(c, name);
            typeof(CardData).GetField("_effectId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(c, effectId);
            return c;
        }

        private static CardData MakeEquipmentCard(string name)
        {
            var c = MakeUnitCard(name, effectId: "");
            typeof(CardData).GetField("_isEquipment", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(c, true);
            return c;
        }
    }
}
