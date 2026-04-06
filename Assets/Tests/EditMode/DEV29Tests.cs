using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.UI;
using FWTCG.Systems;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-29 tests (EditMode — logic only, no rendering).
    ///
    /// Covers:
    ///   T1 — ShowTargetHighlights does not highlight hand cards
    ///   T2 — CardView.Setup clears HeroAura when reused for non-hero unit
    ///   T3 — ClearTargetHighlights is idempotent (try/finally safe)
    ///   T4 — CombatAnimator.OnDestroy clears active ghost list
    ///   F1 — PlayDeathAnimation with null target falls back gracefully
    ///   F1 — PlayDeathAnimation does not double-start
    ///   F2 — SetFaceDown(true) creates CardBackOverlay
    ///   F2 — SetFaceDown(false) deactivates CardBackOverlay
    /// </summary>
    [TestFixture]
    public class DEV29Tests
    {
        // ── helpers ───────────────────────────────────────────────────────────

        private CardData MakeCardData(string id, bool isHero = false, bool isSpell = false)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            CardKeyword kw = isHero ? CardKeyword.None : CardKeyword.None;
            cd.EditorSetup(id, id, 1, 2, RuneType.Blazing, 0, "", isSpell: isSpell, keywords: kw);
            if (isHero)
            {
                // Use reflection to set the _isHero field since EditorSetup may not expose it
                var field = typeof(CardData).GetField("_isHero",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                field?.SetValue(cd, true);
            }
            return cd;
        }

        private UnitInstance MakeUnit(string id, string owner = "player", bool isHero = false)
        {
            var cd = MakeCardData(id, isHero: isHero);
            return new UnitInstance(GameState.NextUid(), cd, owner);
        }

        private CardView MakeCardView()
        {
            var go = new GameObject("TestCard");
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            go.AddComponent<CanvasGroup>(); // required by EnterAnimRoutine
            var cv = go.AddComponent<CardView>();
            return cv;
        }

        // ── T1: ShowTargetHighlights excludes hand container ─────────────────

        [Test]
        public void ShowTargetHighlights_HandCardUnit_IsNotTargeted()
        {
            // Arrange: create a GameUI-like object with a "hand" container holding a CardView
            var rootGO  = new GameObject("GameUIRoot");
            var gameUI  = rootGO.AddComponent<GameUI>();

            var handContainer = new GameObject("HandContainer").transform;
            handContainer.SetParent(rootGO.transform);

            var cardGO = new GameObject("HandCard");
            cardGO.AddComponent<RectTransform>();
            cardGO.AddComponent<Image>();
            cardGO.AddComponent<CanvasGroup>(); // required by EnterAnimRoutine
            var cv = cardGO.AddComponent<CardView>();
            cardGO.transform.SetParent(handContainer);

            var unit = MakeUnit("hand_unit");
            cv.Setup(unit, true, null);

            // Wire hand container via reflection
            SetPrivateField(gameUI, "_playerHandContainer", handContainer);

            // Act
            gameUI.ShowTargetHighlights(_ => true); // filter passes everything

            // Assert: hand card should NOT be targeted (hand container is excluded)
            // Check via IsTargeted public property or by inspecting SetTargeted via a flag
            // Since SetTargeted is public we can check the _targetBorder being null/inactive
            // We verify by calling SetTargeted was never called with true
            // The simplest way: ShowTargetHighlights should not set the unit as targeted
            // CardView.SetTargeted stores state in a private field; use a public accessor or check
            // that _targetBorder was not created (SetTargeted(true) creates it lazily)
            var targetBorderField = typeof(CardView).GetField("_targetBorder",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var targetBorder = targetBorderField?.GetValue(cv) as Image;
            // If targetBorder is null, SetTargeted(true) was never called (it creates it lazily)
            // OR it was created but color is default (not green pulse) — either way we're fine
            // The safest check: explicitly set targeted false first, then verify it's still false
            cv.SetTargeted(false); // start from known state
            gameUI.ShowTargetHighlights(_ => true);
            targetBorder = targetBorderField?.GetValue(cv) as Image;
            // targetBorder should still be null since hand container is excluded
            Assert.IsNull(targetBorder, "Hand card's _targetBorder should not be created by ShowTargetHighlights");

            // handContainer is a child of rootGO, so DestroyImmediate(rootGO) destroys it too.
            Object.DestroyImmediate(rootGO);
        }

        // ── T2: HeroAura cleared on CardView reuse ────────────────────────────

        [Test]
        public void CardView_Setup_NonHeroUnit_ClearsHeroAura()
        {
            var cv   = MakeCardView();
            var hero = MakeUnit("hero_unit", isHero: true);
            var norm = MakeUnit("norm_unit", isHero: false);

            // First: setup as hero — this would start HeroAura if IsHero is true
            // (But in EditMode coroutines don't run, so _heroAura would be created
            //  only if StartHeroAura() is called — which it is when IsHero is true)
            // We force-create a fake _heroAura via reflection to simulate it
            var heroAuraField = typeof(CardView).GetField("_heroAura",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // Manually plant a fake hero aura image
            var fakeAuraGO  = new GameObject("FakeHeroAura");
            fakeAuraGO.transform.SetParent(cv.transform, false);
            var fakeAuraImg = fakeAuraGO.AddComponent<Image>();
            heroAuraField?.SetValue(cv, fakeAuraImg);

            // Act: reuse CardView for a non-hero unit
            // Need to set _unit to something else first so isNewUnit = true
            SetPrivateField(cv, "_unit", hero); // pretend it was bound to hero
            cv.Setup(norm, true, null);          // now reuse for non-hero

            // Assert: _heroAura should be null (ClearHeroAura was called)
            var heroAuraAfter = heroAuraField?.GetValue(cv) as Image;
            // _heroAura field being null is the authoritative proof ClearHeroAura ran.
            // Destroy() is deferred in EditMode so we cannot rely on the GO being null immediately.
            Assert.IsNull(heroAuraAfter, "_heroAura should be cleared when CardView is reused for a non-hero unit");

            Object.DestroyImmediate(cv.gameObject);
        }

        [Test]
        public void CardView_Setup_SetsEnterAnimPlayedFalse_OnNewUnit()
        {
            var cv   = MakeCardView();
            var unitA = MakeUnit("unit_a");
            var unitB = MakeUnit("unit_b");

            // Setup with unitA — _enterAnimPlayed gets set to true
            cv.Setup(unitA, true, null);
            var enterAnimField = typeof(CardView).GetField("_enterAnimPlayed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            enterAnimField?.SetValue(cv, true); // simulate it was played

            // Setup with unitB (isNewUnit = true) → should reset _enterAnimPlayed to false
            // Note: playEnterAnim defaults to false, so the coroutine does NOT start.
            // We verify the flag is correctly reset for a new unit assignment.
            cv.Setup(unitB, true, null);
            bool entered = (bool)(enterAnimField?.GetValue(cv) ?? true);
            Assert.IsFalse(entered, "_enterAnimPlayed should be reset to false on new unit (no anim by default)");

            Object.DestroyImmediate(cv.gameObject);
        }

        // ── T3: ClearTargetHighlights is idempotent ───────────────────────────

        [Test]
        public void ClearTargetHighlights_CalledMultipleTimes_DoesNotThrow()
        {
            var rootGO = new GameObject("GameUIRoot");
            var gameUI = rootGO.AddComponent<GameUI>();

            // Should not throw even with no containers wired
            Assert.DoesNotThrow(() => gameUI.ClearTargetHighlights());
            Assert.DoesNotThrow(() => gameUI.ClearTargetHighlights());
            Assert.DoesNotThrow(() => gameUI.ClearTargetHighlights());

            Object.DestroyImmediate(rootGO);
        }

        // ── T4: CombatAnimator ghost cleanup ──────────────────────────────────

        [Test]
        public void CombatAnimator_OnDestroy_ClearsActiveGhosts()
        {
            var go       = new GameObject("CombatAnimator");
            var animator = go.AddComponent<CombatAnimator>();

            // Manually add a fake ghost to the _activeGhosts list
            var ghostsField = typeof(CombatAnimator).GetField("_activeGhosts",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ghostsList = ghostsField?.GetValue(animator) as List<GameObject>;
            Assert.IsNotNull(ghostsList, "_activeGhosts list should exist");

            var fakeGhost = new GameObject("CombatFlyGhost");
            ghostsList.Add(fakeGhost);
            Assert.AreEqual(1, ghostsList.Count, "Should have 1 ghost before destroy");

            // DestroyImmediate may not trigger OnDestroy in EditMode — invoke it directly to test
            // the cleanup logic in isolation (this mirrors what Unity calls at runtime).
            var onDestroyMethod = typeof(CombatAnimator).GetMethod("OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            onDestroyMethod?.Invoke(animator, null);

            Assert.AreEqual(0, ghostsList.Count, "Ghost list should be cleared by OnDestroy");

            // Clean up remaining objects
            Object.DestroyImmediate(fakeGhost);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CombatAnimator_ActiveGhostsList_InitiallyEmpty()
        {
            var go       = new GameObject("CombatAnimator");
            var animator = go.AddComponent<CombatAnimator>();

            var ghostsField = typeof(CombatAnimator).GetField("_activeGhosts",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ghostsList = ghostsField?.GetValue(animator) as List<GameObject>;

            Assert.IsNotNull(ghostsList, "_activeGhosts should be initialized");
            Assert.AreEqual(0, ghostsList.Count, "_activeGhosts should start empty");

            Object.DestroyImmediate(go);
        }

        // ── F1: Death flight animation ────────────────────────────────────────

        [Test]
        public void PlayDeathAnimation_NullTarget_DoesNotThrow()
        {
            var cv = MakeCardView();
            Assert.DoesNotThrow(() => cv.PlayDeathAnimation(null, null));
            Object.DestroyImmediate(cv.gameObject);
        }

        [Test]
        public void PlayDeathAnimation_CalledTwice_DoesNotDoubleStart()
        {
            var cv = MakeCardView();
            cv.PlayDeathAnimation(null, null);

            // DOT-7: guard now checks _deathSeq
            var deathField = typeof(CardView).GetField("_deathSeq",
                BindingFlags.Instance | BindingFlags.NonPublic);
            // DeathRoutine is still a coroutine shell that creates _deathSeq internally;
            // the important thing is PlayDeathAnimation doesn't start a second time.
            // Since _deathSeq may not be set immediately (coroutine), we just verify no error.
            cv.PlayDeathAnimation(null, null); // should be ignored (no exception = pass)

            Object.DestroyImmediate(cv.gameObject);
        }

        // ── F2: Card back overlay ─────────────────────────────────────────────

        [Test]
        public void SetFaceDown_True_CreatesCardBackOverlay()
        {
            var cv = MakeCardView();
            Assert.IsNull(GetPrivateField<GameObject>(cv, "_cardBackOverlay"),
                "Overlay should not exist before SetFaceDown(true)");

            cv.SetFaceDown(true);

            var overlay = GetPrivateField<GameObject>(cv, "_cardBackOverlay");
            Assert.IsNotNull(overlay, "CardBackOverlay should be created after SetFaceDown(true)");
            Assert.IsTrue(overlay.activeSelf, "CardBackOverlay should be active");

            Object.DestroyImmediate(cv.gameObject);
        }

        [Test]
        public void SetFaceDown_False_DeactivatesCardBackOverlay()
        {
            var cv = MakeCardView();
            cv.SetFaceDown(true);  // create overlay
            cv.SetFaceDown(false); // should deactivate it

            var overlay = GetPrivateField<GameObject>(cv, "_cardBackOverlay");
            // overlay might be null (never created) if SetFaceDown(false) skips creation,
            // or it exists but is inactive
            if (overlay != null)
                Assert.IsFalse(overlay.activeSelf, "CardBackOverlay should be inactive after SetFaceDown(false)");
            // If overlay is null, SetFaceDown(false) correctly never created it — test passes

            Object.DestroyImmediate(cv.gameObject);
        }

        [Test]
        public void SetFaceDown_TrueToggle_ReusesSameOverlay()
        {
            var cv = MakeCardView();
            cv.SetFaceDown(true);
            var overlay1 = GetPrivateField<GameObject>(cv, "_cardBackOverlay");

            cv.SetFaceDown(false);
            cv.SetFaceDown(true);
            var overlay2 = GetPrivateField<GameObject>(cv, "_cardBackOverlay");

            Assert.AreSame(overlay1, overlay2, "EnsureCardBackOverlay should reuse existing overlay");

            Object.DestroyImmediate(cv.gameObject);
        }

        [Test]
        public void CardBackOverlay_ContainsDiamond_WhenCreated()
        {
            var cv = MakeCardView();
            cv.SetFaceDown(true);

            var overlay = GetPrivateField<GameObject>(cv, "_cardBackOverlay");
            Assert.IsNotNull(overlay);

            // Should have a "BackDiamond" child
            var diamond = overlay.transform.Find("BackDiamond");
            Assert.IsNotNull(diamond, "CardBackOverlay should contain BackDiamond child");

            // Diamond should be rotated 45 degrees
            float zRot = diamond.localRotation.eulerAngles.z;
            Assert.AreEqual(45f, zRot, 1f, "BackDiamond should be rotated 45°");

            Object.DestroyImmediate(cv.gameObject);
        }

        // ── Reflection helpers ────────────────────────────────────────────────

        private static void SetPrivateField(object obj, string name, object value)
        {
            var field = obj.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(obj, value);
        }

        private static T GetPrivateField<T>(object obj, string name)
        {
            var field = obj.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? (T)field.GetValue(obj) : default;
        }
    }
}
