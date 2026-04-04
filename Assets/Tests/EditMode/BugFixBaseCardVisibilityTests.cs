using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// Bug fix tests: base card invisible after 2nd/3rd placement, hero cost preview.
    /// </summary>
    [TestFixture]
    public class BugFixBaseCardVisibilityTests
    {
        // ── Bug 1: CancelEnterAnim API exists and restores visual state ──

        [Test]
        public void CardView_CancelEnterAnim_MethodExists()
        {
            var method = typeof(FWTCG.UI.CardView).GetMethod(
                "CancelEnterAnim",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(method, "CardView must have public CancelEnterAnim() to allow DropAnimHost to cancel enter animation");
        }

        [Test]
        public void CardView_CancelEnterAnim_RestoresScale()
        {
            var go = new GameObject("TestCancelEnterAnim");
            go.AddComponent<CanvasGroup>();
            var cv = go.AddComponent<FWTCG.UI.CardView>();

            // Simulate mid-animation state: scale=0.82
            // (EnterAnimRoutine no longer touches alpha — only scale + position)
            go.transform.localScale = Vector3.one * 0.82f;

            cv.CancelEnterAnim();

            Assert.AreEqual(Vector3.one, go.transform.localScale, "CancelEnterAnim must restore scale to 1");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CardView_HasEnterAnimCoroutineField()
        {
            var field = typeof(FWTCG.UI.CardView).GetField(
                "_enterAnimCoroutine",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field, "CardView must track _enterAnimCoroutine for cancellation");
            Assert.AreEqual(typeof(Coroutine), field.FieldType);
        }

        // ── Bug 2: Hero hover & confirm flow ──

        [Test]
        public void GameUI_HasHeroHoverFields()
        {
            var enterField = typeof(FWTCG.UI.GameUI).GetField(
                "_onHeroHoverEnter",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var exitField = typeof(FWTCG.UI.GameUI).GetField(
                "_onHeroHoverExit",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(enterField, "GameUI must have _onHeroHoverEnter callback field");
            Assert.IsNotNull(exitField, "GameUI must have _onHeroHoverExit callback field");
        }

        [Test]
        public void GameManager_HasPlayHeroWithRuneConfirmAsync()
        {
            var method = typeof(GameManager).GetMethod(
                "PlayHeroWithRuneConfirmAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method, "GameManager must have PlayHeroWithRuneConfirmAsync for hero rune confirm flow");
        }

        [Test]
        public void GameManager_HasHeroHoverHandlers()
        {
            var enter = typeof(GameManager).GetMethod(
                "OnHeroHoverEnter",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var exit = typeof(GameManager).GetMethod(
                "OnHeroHoverExit",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(enter, "GameManager must have OnHeroHoverEnter for hero cost preview");
            Assert.IsNotNull(exit, "GameManager must have OnHeroHoverExit to clear highlights");
        }
    }
}
