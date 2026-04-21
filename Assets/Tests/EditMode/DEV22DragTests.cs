using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-22: Drag-to-play system tests.
    /// All EditMode — tests constants, zone logic, and callback wiring.
    /// No MonoBehaviour or canvas required.
    /// </summary>
    [TestFixture]
    public class DEV22DragTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private CardData MakeUnit(string id, int cost = 1)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(id, id, cost, 2, RuneType.Blazing, 0, "");
            return cd;
        }

        private CardData MakeSpell(string id, int cost = 1)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(id, id, cost, 0, RuneType.Blazing, 0, "法术", isSpell: true);
            return cd;
        }

        // ── PortalVFX: constant values ────────────────────────────────────────

        [Test]
        public void PortalVFX_RingCount_Is3()
            => Assert.AreEqual(3, PortalVFX.RING_COUNT);

        [Test]
        public void PortalVFX_OrbitalCount_Is8()
            => Assert.AreEqual(8, PortalVFX.ORBITAL_COUNT);

        [Test]
        public void PortalVFX_OuterRadius_Is60()
            => Assert.AreEqual(60f, PortalVFX.RING_OUTER_RADIUS, 0.01f);

        [Test]
        public void PortalVFX_MidRadius_Is42()
            => Assert.AreEqual(42f, PortalVFX.RING_MID_RADIUS, 0.01f);

        [Test]
        public void PortalVFX_InnerRadius_Is24()
            => Assert.AreEqual(24f, PortalVFX.RING_INNER_RADIUS, 0.01f);

        [Test]
        public void PortalVFX_OrbitalRadius_Is55()
            => Assert.AreEqual(55f, PortalVFX.ORBITAL_RADIUS, 0.01f);

        [Test]
        public void PortalVFX_OrbitalDotSize_Is7()
            => Assert.AreEqual(7f, PortalVFX.ORBITAL_DOT_SIZE, 0.01f);

        [Test]
        public void PortalVFX_FadeInDuration_Is0_28()
            => Assert.AreEqual(0.28f, PortalVFX.FADE_IN_DURATION, 0.001f);

        [Test]
        public void PortalVFX_FadeOutDuration_Is0_22()
            => Assert.AreEqual(0.22f, PortalVFX.FADE_OUT_DURATION, 0.001f);

        // ── CardDragHandler: drag callbacks fire when set ─────────────────────

        [Test]
        public void CardDragHandler_OnDragToBase_CanBeAssigned()
        {
            // Create a minimal GO with CardDragHandler
            var go = new GameObject("TestCard");
            go.AddComponent<Image>();
            go.AddComponent<Button>();
            go.AddComponent<CardView>();
            var dh = go.AddComponent<CardDragHandler>();

            bool called = false;
            dh.OnDragToBase = u => called = true;

            dh.OnDragToBase?.Invoke(null);
            Assert.IsTrue(called);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CardDragHandler_OnSpellDragOut_CanBeAssigned()
        {
            var go = new GameObject("TestSpell");
            go.AddComponent<Image>();
            go.AddComponent<Button>();
            go.AddComponent<CardView>();
            var dh = go.AddComponent<CardDragHandler>();

            bool called = false;
            dh.OnSpellDragOut = u => called = true;

            dh.OnSpellDragOut?.Invoke(null);
            Assert.IsTrue(called);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CardDragHandler_OnDragToBF_CanBeAssigned()
        {
            var go = new GameObject("TestBase");
            go.AddComponent<Image>();
            go.AddComponent<Button>();
            go.AddComponent<CardView>();
            var dh = go.AddComponent<CardDragHandler>();

            bool called  = false;
            int  bfIndex = -1;
            dh.OnDragToBF = (unit, bfId) => { called = true; bfIndex = bfId; };

            dh.OnDragToBF?.Invoke(null, 1);
            Assert.IsTrue(called);
            Assert.AreEqual(1, bfIndex);

            Object.DestroyImmediate(go);
        }

        // ── CardDragHandler: static zone refs survive assignment ──────────────

        [Test]
        public void CardDragHandler_StaticZoneRefs_CanBeSet()
        {
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            var handGO = new GameObject("Hand");
            var handRT = handGO.AddComponent<RectTransform>();
            var baseGO = new GameObject("Base");
            var baseRT = baseGO.AddComponent<RectTransform>();
            var bf1GO  = new GameObject("BF1");
            var bf1RT  = bf1GO.AddComponent<RectTransform>();
            var bf2GO  = new GameObject("BF2");
            var bf2RT  = bf2GO.AddComponent<RectTransform>();

            CardDragHandler.RootCanvas = canvas;
            CardDragHandler.HandZoneRT = handRT;
            CardDragHandler.BaseZoneRT = baseRT;
            CardDragHandler.Bf1ZoneRT  = bf1RT;
            CardDragHandler.Bf2ZoneRT  = bf2RT;

            Assert.AreSame(canvas,  CardDragHandler.RootCanvas);
            Assert.AreSame(handRT,  CardDragHandler.HandZoneRT);
            Assert.AreSame(baseRT,  CardDragHandler.BaseZoneRT);
            Assert.AreSame(bf1RT,   CardDragHandler.Bf1ZoneRT);
            Assert.AreSame(bf2RT,   CardDragHandler.Bf2ZoneRT);

            // Cleanup
            CardDragHandler.RootCanvas = null;
            CardDragHandler.HandZoneRT = null;
            CardDragHandler.BaseZoneRT = null;
            CardDragHandler.Bf1ZoneRT  = null;
            CardDragHandler.Bf2ZoneRT  = null;

            Object.DestroyImmediate(canvasGO);
            Object.DestroyImmediate(handGO);
            Object.DestroyImmediate(baseGO);
            Object.DestroyImmediate(bf1GO);
            Object.DestroyImmediate(bf2GO);
        }

        // ── Boundary: empty callback lists don't throw ────────────────────────

        [Test]
        public void CardDragHandler_NullCallbacks_DoNotThrow()
        {
            var go = new GameObject("TestNull");
            go.AddComponent<Image>();
            go.AddComponent<Button>();
            go.AddComponent<CardView>();
            var dh = go.AddComponent<CardDragHandler>();

            // All callbacks null — invoking them should not throw
            Assert.DoesNotThrow(() => dh.OnDragToBase?.Invoke(null));
            Assert.DoesNotThrow(() => dh.OnSpellDragOut?.Invoke(null));
            Assert.DoesNotThrow(() => dh.OnDragToBF?.Invoke(null, 0));

            Object.DestroyImmediate(go);
        }

        // ── CardData.IsSpell distinguishes unit vs spell drag ─────────────────

        [Test]
        public void CardData_IsSpell_False_ForUnitCard()
        {
            var cd = MakeUnit("test_unit");
            Assert.IsFalse(cd.IsSpell);
        }

        [Test]
        public void CardData_IsSpell_True_ForSpellCard()
        {
            var cd = MakeSpell("test_spell");
            Assert.IsTrue(cd.IsSpell);
        }
    }
}
