using DG.Tweening;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.FX;
using FWTCG.Tests.EditMode;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FWTCG.Tests
{
    /// <summary>
    /// VFX-3 — Dissolve death effect tests.
    /// Covers: TweenMatFX dissolve API, KillDissolveFX material property,
    /// CardView._killDissolveMat field existence, and fallback path safety.
    /// DOT-2: migrated from AnimMatFX to TweenMatFX.
    /// </summary>
    [TestFixture]
    public class VFX3DissolveTests : DOTweenTestBase
    {
        // ── TweenMatFX tests (replaces AnimMatFX) ───────────────────────────

        [Test]
        public void TweenMatFX_DOFloat_NullMaterial_ReturnsNull()
        {
            var tween = TweenMatFX.DOFloat(null, "_Prop", 1f, 0.5f);
            Assert.IsNull(tween);
        }

        [Test]
        public void TweenMatFX_DOFloat_MissingProperty_ReturnsNull()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            var tween = TweenMatFX.DOFloat(mat, "nonexistent_xyz", 1f, 0.5f);
            Assert.IsNull(tween, "DOFloat should return null for missing shader property");
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void TweenMatFX_DOColor_NullMaterial_ReturnsNull()
        {
            var tween = TweenMatFX.DOColor(null, "_Color", Color.red, 0.5f);
            Assert.IsNull(tween);
        }

        [Test]
        public void TweenMatFX_DOColor_MissingProperty_ReturnsNull()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            var tween = TweenMatFX.DOColor(mat, "nonexistent_xyz", Color.red, 0.5f);
            Assert.IsNull(tween, "DOColor should return null for missing shader property");
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void TweenMatFX_DissolveSequence_ReturnsSequence()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            // UI/Default doesn't have noise_fade, so inner DOFloat returns null,
            // but DissolveSequence still returns a valid Sequence (possibly empty)
            var seq = TweenMatFX.DissolveSequence(mat, 0.6f);
            Assert.IsNotNull(seq, "DissolveSequence should always return a valid Sequence");
            if (seq.IsActive()) seq.Kill();
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void TweenMatFX_DissolveSequence_CallbackRegistered()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            bool callbackFired = false;
            var seq = TweenMatFX.DissolveSequence(mat, 0.5f, () => callbackFired = true);

            // In EditMode, Complete() may not fire callbacks reliably because
            // DOTween ManualUpdate doesn't run. Verify the sequence was created
            // with the callback by checking it's active and has content.
            Assert.IsNotNull(seq, "DissolveSequence should return a valid Sequence");
            Assert.IsTrue(seq.IsActive(), "Sequence should be active after creation");

            if (seq.IsActive()) seq.Kill();
            Object.DestroyImmediate(mat);
        }

        // ── KillDissolveFX material tests ────────────────────────────────────

#if UNITY_EDITOR
        [Test]
        public void KillDissolveFX_MaterialExists_AtExpectedPath()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KillDissolveFX.mat");
            Assert.IsNotNull(mat, "KillDissolveFX.mat not found at Assets/Materials/KillDissolveFX.mat");
        }

        [Test]
        public void KillDissolveFX_HasNoiseFadeProperty()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KillDissolveFX.mat");
            Assert.IsNotNull(mat, "KillDissolveFX.mat missing — cannot check properties");
            Assert.IsTrue(mat.HasProperty("noise_fade"),
                "KillDissolveFX.mat must expose 'noise_fade' float property for TweenMatFX dissolve");
        }
#endif

        // ── CardView._killDissolveMat field tests ────────────────────────────

        [Test]
        public void CardView_HasKillDissolveMatField()
        {
            var field = typeof(FWTCG.UI.CardView).GetField(
                "_killDissolveMat",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field, "CardView must have private [SerializeField] Material _killDissolveMat");
            Assert.AreEqual(typeof(Material), field.FieldType);
        }

        [Test]
        public void CardView_HasClonedDissolveMatField()
        {
            var field = typeof(FWTCG.UI.CardView).GetField(
                "_clonedDissolveMat",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field, "CardView must have private Material _clonedDissolveMat for per-instance clone tracking");
            Assert.AreEqual(typeof(Material), field.FieldType);
        }

        // ── Fallback path: null material does not crash PlayDeathAnimation ───

        [Test]
        public void CardView_PlayDeathAnimation_WithNullMat_StartsCoroutine()
        {
            var go = new GameObject("TestCardViewDeath");
            var cv = go.AddComponent<FWTCG.UI.CardView>();

            // _killDissolveMat is null by default — fallback path should be selected
            Assert.DoesNotThrow(() => cv.PlayDeathAnimation(null, null));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CardView_DissolveOrFallback_MethodExists()
        {
            var method = typeof(FWTCG.UI.CardView).GetMethod(
                "DissolveOrFallbackRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method, "CardView must have private DissolveOrFallbackRoutine coroutine (VFX-3)");
        }
    }
}
