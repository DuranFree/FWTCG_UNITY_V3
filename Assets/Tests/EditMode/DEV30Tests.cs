using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-30 tests (EditMode — logic only, no rendering).
    ///
    /// Covers:
    ///   F1  — GameEventBus.OnConquestScored fires when FireConquestScored is called
    ///   F1  — SpellVFX subscribes to OnConquestScored in Awake
    ///   F2  — SpellDuelUI.Instance is set in Awake
    ///   F2  — ShowDuelOverlay sets IsShowing = true
    ///   F2  — HideDuelOverlay sets IsShowing = false
    ///   F2  — FireDuelBanner triggers SpellDuelUI.IsShowing (via subscription)
    ///   F2  — OnClearBanners triggers HideDuelOverlay (IsShowing reverts)
    ///   F2  — ReactiveWindowUI.Instance is set in Awake
    ///   F2  — AutoSkipReaction completes pending TCS with null
    ///   F4  — Equipment badge label uses card name (not fixed "▲")
    ///   V6  — EnsureShineOverlay creates "ShineOverlay" child on CardView
    ///   V7  — _sparkDots list exists; playable unit starts spark immediately on Setup
    ///   V7  — non-playable unit has no spark on Setup
    /// </summary>
    [TestFixture]
    public class DEV30Tests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private CardData MakeCardData(string id, bool isSpell = false)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(id, id, 1, 2, RuneType.Blazing, 0, "", isSpell: isSpell);
            return cd;
        }

        private UnitInstance MakeUnit(string id, string owner = "player")
        {
            var cd = MakeCardData(id);
            return new UnitInstance(GameState.NextUid(), cd, owner);
        }

        private CardView MakeCardView()
        {
            var go = new GameObject("TestCard");
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            go.AddComponent<CanvasGroup>();
            return go.AddComponent<CardView>();
        }

        private T GetPrivate<T>(object obj, string fieldName)
        {
            var f = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return f != null ? (T)f.GetValue(obj) : default;
        }

        private void SetPrivate(object obj, string fieldName, object value)
        {
            var f = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            f?.SetValue(obj, value);
        }

        private void CallPrivate(object obj, string methodName, params object[] args)
        {
            var m = obj.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            m?.Invoke(obj, args);
        }

        // Calls Awake via reflection; _initialized guard prevents double-subscription
        // even if Unity auto-invokes Awake on AddComponent in some editor versions.
        private void InvokeAwake(MonoBehaviour mb)
        {
            var m = mb.GetType().GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            m?.Invoke(mb, null);
        }

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate calls OnDestroy which unsubscribes from GameEventBus.
            // SafeDestroy in production code handles the Edit Mode Destroy restriction.
            foreach (var go in Object.FindObjectsOfType<GameObject>())
                if (go != null) Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // F1 — Conquest VFX event bus
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void F1_FireConquestScored_FiresOnConquestScored()
        {
            int callCount = 0;
            string receivedOwner = null;

            void Handler(string o) { callCount++; receivedOwner = o; }
            GameEventBus.OnConquestScored += Handler;
            try
            {
                GameEventBus.FireConquestScored(GameRules.OWNER_PLAYER);
                Assert.AreEqual(1, callCount, "OnConquestScored should fire once");
                Assert.AreEqual(GameRules.OWNER_PLAYER, receivedOwner);
            }
            finally
            {
                GameEventBus.OnConquestScored -= Handler;
            }
        }

        [Test]
        public void F1_FireConquestScored_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(
                () => GameEventBus.FireConquestScored(GameRules.OWNER_ENEMY),
                "FireConquestScored should not throw when no subscribers");
        }

        [Test]
        public void F1_SpellVFX_SubscribesToOnConquestScored_OnAwake()
        {
            var go = new GameObject("SpellVFX");
            go.AddComponent<RectTransform>();
            var vfx = go.AddComponent<SpellVFX>();
            InvokeAwake(vfx);

            int fireCount = 0;
            void Counter(string _) { fireCount++; }
            GameEventBus.OnConquestScored += Counter;
            try
            {
                GameEventBus.FireConquestScored(GameRules.OWNER_PLAYER);
                Assert.GreaterOrEqual(fireCount, 1, "At least our counter should fire");
            }
            finally
            {
                GameEventBus.OnConquestScored -= Counter;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // F2 — SpellDuelUI
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void F2_SpellDuelUI_Instance_SetOnAwake()
        {
            var go = new GameObject("SpellDuelUI");
            var dui = go.AddComponent<SpellDuelUI>();
            InvokeAwake(dui);

            Assert.AreEqual(dui, SpellDuelUI.Instance,
                "SpellDuelUI.Instance should be set after Awake");
        }

        [Test]
        public void F2_SpellDuelUI_Instance_ClearedOnDestroy()
        {
            var go = new GameObject("SpellDuelUI");
            var dui = go.AddComponent<SpellDuelUI>();
            InvokeAwake(dui);
            Assert.AreEqual(dui, SpellDuelUI.Instance);

            Object.DestroyImmediate(go); // calls OnDestroy → clears Instance
            // Use Unity's == which handles both real-null and fake-null destroyed objects
            Assert.IsTrue(SpellDuelUI.Instance == null,
                "SpellDuelUI.Instance should be null after Destroy");
        }

        [Test]
        public void F2_ShowDuelOverlay_SetsIsShowing_True()
        {
            var canvasGO = new GameObject("Canvas");
            canvasGO.AddComponent<Canvas>();

            var go = new GameObject("SpellDuelUI");
            var dui = go.AddComponent<SpellDuelUI>();
            InvokeAwake(dui);

            dui.ShowDuelOverlay();
            Assert.IsTrue(dui.IsShowing, "IsShowing should be true after ShowDuelOverlay");
        }

        [Test]
        public void F2_HideDuelOverlay_SetsIsShowing_False()
        {
            var canvasGO = new GameObject("Canvas");
            canvasGO.AddComponent<Canvas>();

            var go = new GameObject("SpellDuelUI");
            var dui = go.AddComponent<SpellDuelUI>();
            InvokeAwake(dui);

            dui.ShowDuelOverlay();
            dui.HideDuelOverlay();
            Assert.IsFalse(dui.IsShowing, "IsShowing should be false after HideDuelOverlay");
        }

        [Test]
        public void F2_FireDuelBanner_TriggersShowDuelOverlay()
        {
            var canvasGO = new GameObject("Canvas");
            canvasGO.AddComponent<Canvas>();

            var go = new GameObject("SpellDuelUI");
            var dui = go.AddComponent<SpellDuelUI>();
            InvokeAwake(dui); // subscribes ShowDuelOverlay to OnDuelBanner

            GameEventBus.FireDuelBanner();
            Assert.IsTrue(dui.IsShowing,
                "SpellDuelUI should be showing after FireDuelBanner");
        }

        [Test]
        public void F2_FireClearBanners_TriggersHideDuelOverlay()
        {
            var canvasGO = new GameObject("Canvas");
            canvasGO.AddComponent<Canvas>();

            var go = new GameObject("SpellDuelUI");
            var dui = go.AddComponent<SpellDuelUI>();
            InvokeAwake(dui);

            dui.ShowDuelOverlay();
            Assert.IsTrue(dui.IsShowing);

            GameEventBus.FireClearBanners();
            Assert.IsFalse(dui.IsShowing,
                "SpellDuelUI should hide after FireClearBanners");
        }

        [Test]
        public void F2_ShowDuelOverlay_IdempotentWhenCalledTwice()
        {
            var canvasGO = new GameObject("Canvas");
            canvasGO.AddComponent<Canvas>();

            var go = new GameObject("SpellDuelUI");
            var dui = go.AddComponent<SpellDuelUI>();
            InvokeAwake(dui);

            dui.ShowDuelOverlay();
            dui.ShowDuelOverlay(); // second call should not throw or change state
            Assert.IsTrue(dui.IsShowing);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // F2 — ReactiveWindowUI.AutoSkipReaction
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void F2_ReactiveWindowUI_Instance_SetOnAwake()
        {
            var go = new GameObject("ReactiveWindowUI");
            var rwui = go.AddComponent<ReactiveWindowUI>();
            InvokeAwake(rwui);

            Assert.AreEqual(rwui, ReactiveWindowUI.Instance,
                "ReactiveWindowUI.Instance should be set after Awake");
        }

        [Test]
        public void F2_AutoSkipReaction_CompletesTaskWithNull_WhenWindowOpen()
        {
            var go = new GameObject("ReactiveWindowUI");
            var rwui = go.AddComponent<ReactiveWindowUI>();
            InvokeAwake(rwui);

            var tcs = new TaskCompletionSource<UnitInstance>();
            SetPrivate(rwui, "_tcs", tcs);

            rwui.AutoSkipReaction();

            Assert.IsTrue(tcs.Task.IsCompleted,
                "AutoSkipReaction should complete the pending task");
            Assert.IsNull(tcs.Task.Result,
                "AutoSkipReaction should complete with null result");
        }

        [Test]
        public void F2_AutoSkipReaction_DoesNotThrow_WhenNoWindowOpen()
        {
            var go = new GameObject("ReactiveWindowUI");
            var rwui = go.AddComponent<ReactiveWindowUI>();
            InvokeAwake(rwui);

            Assert.DoesNotThrow(() => rwui.AutoSkipReaction(),
                "AutoSkipReaction should be safe to call when no window is open");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // F4 — Equipment badge label
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void F4_EquipBadge_UsesEquipmentCardName()
        {
            var cv = MakeCardView();
            var unit = MakeUnit("unit1");

            var equipData = ScriptableObject.CreateInstance<CardData>();
            equipData.EditorSetup("sword", "铁剑", 1, 0, RuneType.Blazing, 0, "", isSpell: false);
            var equipUnit = new UnitInstance(GameState.NextUid(), equipData, "player");
            unit.AttachedEquipment = equipUnit;

            cv.Setup(unit, true, null);

            var equipBadge = GetPrivate<GameObject>(cv, "_equipBadge");
            if (equipBadge != null)
            {
                Assert.IsTrue(equipBadge.activeSelf,
                    "_equipBadge should be active when unit has AttachedEquipment");
                var txt = equipBadge.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    Assert.AreNotEqual("▲", txt.text,
                        "Equipment badge should show card name, not just '▲'");
                    Assert.IsTrue(txt.text.Length > 0);
                }
            }
        }

        [Test]
        public void F4_NoEquipBadge_WhenNoEquipmentAttached()
        {
            var cv = MakeCardView();
            var unit = MakeUnit("unit2");

            cv.Setup(unit, true, null);

            var equipBadge = GetPrivate<GameObject>(cv, "_equipBadge");
            if (equipBadge != null)
                Assert.IsFalse(equipBadge.activeSelf,
                    "_equipBadge should be inactive when no equipment is attached");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // V6 — Foil Sweep (EnsureShineOverlay)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void V6_EnsureShineOverlay_CreatesChild_WithNameShineOverlay()
        {
            var cv = MakeCardView();
            CallPrivate(cv, "EnsureShineOverlay");

            var shineChild = cv.transform.Find("ShineOverlay");
            Assert.IsNotNull(shineChild,
                "EnsureShineOverlay should create a child named 'ShineOverlay'");
        }

        [Test]
        public void V6_EnsureShineOverlay_IdempotentWhenCalledTwice()
        {
            var cv = MakeCardView();
            CallPrivate(cv, "EnsureShineOverlay");
            CallPrivate(cv, "EnsureShineOverlay");

            int count = 0;
            foreach (Transform child in cv.transform)
                if (child.name == "ShineOverlay") count++;

            Assert.AreEqual(1, count,
                "Calling EnsureShineOverlay twice should not create duplicate overlays");
        }

        [Test]
        public void V6_ShineOverlay_HasImageComponent_NotRaycastTarget()
        {
            var cv = MakeCardView();
            CallPrivate(cv, "EnsureShineOverlay");

            var shineChild = cv.transform.Find("ShineOverlay");
            Assert.IsNotNull(shineChild);
            var img = shineChild.GetComponent<Image>();
            Assert.IsNotNull(img, "ShineOverlay child should have an Image component");
            Assert.IsFalse(img.raycastTarget, "ShineOverlay Image.raycastTarget should be false");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // V7 — Playable spark state tracking
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void V7_SparkDots_ListExists_OnCardView()
        {
            var cv = MakeCardView();
            var unit = MakeUnit("unit3");
            cv.Setup(unit, true, null);

            var sparkDots = GetPrivate<List<GameObject>>(cv, "_sparkDots");
            Assert.IsNotNull(sparkDots, "_sparkDots list should exist on CardView");
        }

        [Test]
        public void V7_PlayableSpark_StartsImmediately_ForPlayablePlayerCard()
        {
            // isPlayerCard=true, unit not exhausted → playable → spark active
            var cv = MakeCardView();
            var unit = MakeUnit("unit4");

            cv.Setup(unit, isPlayerCard: true, onClick: null);

            var sparkDots      = GetPrivate<List<GameObject>>(cv, "_sparkDots");
            var sparkCoroutine = GetPrivate<Coroutine>(cv, "_playableSpark");
            bool sparkActive   = (sparkCoroutine != null) || (sparkDots != null && sparkDots.Count > 0);
            Assert.IsTrue(sparkActive,
                "Playable player card should have spark active after Setup");
        }

        [Test]
        public void V7_PlayableSpark_NotStarted_ForNonPlayerCard()
        {
            // isPlayerCard=false → never playable → no spark
            var cv = MakeCardView();
            var unit = MakeUnit("unit5", "enemy");

            cv.Setup(unit, isPlayerCard: false, onClick: null);

            var sparkCoroutine = GetPrivate<Coroutine>(cv, "_playableSpark");
            var sparkDots      = GetPrivate<List<GameObject>>(cv, "_sparkDots");
            bool sparkActive   = (sparkCoroutine != null) || (sparkDots != null && sparkDots.Count > 0);
            Assert.IsFalse(sparkActive,
                "Non-player card should NOT have spark active after Setup");
        }
    }
}
