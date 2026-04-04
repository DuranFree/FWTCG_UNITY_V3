using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.FX;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FWTCG.Tests
{
    /// <summary>
    /// VFX-3 — Dissolve death effect tests.
    /// Covers: AnimMatFX float-drive logic, KillDissolveFX material property,
    /// CardView._killDissolveMat field existence, and fallback path safety.
    /// </summary>
    [TestFixture]
    public class VFX3DissolveTests
    {
        // ── AnimMatFX tests ──────────────────────────────────────────────────

        [Test]
        public void AnimMatFX_Create_AttachesToGameObject()
        {
            var go = new GameObject("TestAnimMatFX");
            var mat = new Material(Shader.Find("UI/Default"));
            var anim = AnimMatFX.Create(go, mat);

            Assert.IsNotNull(anim);
            Assert.AreSame(anim, go.GetComponent<AnimMatFX>());

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void AnimMatFX_Create_Twice_ReturnsSameComponent()
        {
            var go = new GameObject("TestAnimMatFXReuse");
            var mat = new Material(Shader.Find("UI/Default"));
            var anim1 = AnimMatFX.Create(go, mat);
            var anim2 = AnimMatFX.Create(go, mat);

            Assert.AreSame(anim1, anim2, "Second Create should reuse existing component");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void AnimMatFX_SetFloat_UnknownProperty_DoesNotThrow()
        {
            var go = new GameObject("TestAnimMatFXUnknown");
            var mat = new Material(Shader.Find("UI/Default"));
            var anim = AnimMatFX.Create(go, mat);

            // Calling SetFloat with an unknown property name must not throw —
            // call directly so NUnit catches any exception without a lambda wrapper
            // (lambda wrappers in EditMode trigger ShouldRunBehaviour assertions).
            anim.SetFloat("nonexistent_property_xyz", 1f, 0.5f);

            // Verify the action was enqueued despite the unknown name
            var seqField = typeof(AnimMatFX).GetField("_sequence", BindingFlags.NonPublic | BindingFlags.Instance);
            var seq = seqField.GetValue(anim) as Queue<AnimMatAction>;
            Assert.AreEqual(1, seq.Count, "SetFloat should enqueue one action even for unknown properties");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void AnimMatFX_Clear_ResetsState()
        {
            var go = new GameObject("TestAnimMatFXClear");
            var mat = new Material(Shader.Find("UI/Default"));
            var anim = AnimMatFX.Create(go, mat);
            anim.SetFloat("_SomeFloat", 1f, 1f);

            // Call Clear directly — no lambda wrapper to avoid EditMode ShouldRunBehaviour assertions
            anim.Clear();

            // Verify sequence is empty and internal state is reset
            var seqField = typeof(AnimMatFX).GetField("_sequence", BindingFlags.NonPublic | BindingFlags.Instance);
            var seq = seqField.GetValue(anim) as Queue<AnimMatAction>;
            Assert.AreEqual(0, seq.Count, "Clear should empty the action sequence");

            Object.DestroyImmediate(go);
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
                "KillDissolveFX.mat must expose 'noise_fade' float property for AnimMatFX");
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
            // We verify that calling PlayDeathAnimation doesn't throw immediately
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
