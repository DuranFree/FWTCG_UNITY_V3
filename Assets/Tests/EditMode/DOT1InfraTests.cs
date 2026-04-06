using DG.Tweening;
using FWTCG;
using FWTCG.FX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FWTCG.Tests.EditMode
{
    /// <summary>
    /// DOT-1 tests: DOTween infrastructure — TweenHelper, TweenMatFX, DOTweenTestBase.
    /// </summary>
    [TestFixture]
    public class DOT1InfraTests : DOTweenTestBase
    {
        // ─── DOTween basic compile/init ───

        [Test]
        public void DOTween_IsInitialized()
        {
            Assert.IsTrue(DOTween.instance != null, "DOTween should be initialized by DOTweenTestBase");
        }

        [Test]
        public void DOTween_TransformDOMove_Compiles()
        {
            var go = new GameObject("test");
            var tw = go.transform.DOMove(Vector3.one, 0.5f).SetAutoKill(false);
            Assert.IsNotNull(tw, "transform.DOMove should return a Tween");
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DOTween_RectTransformDOAnchorPos_Compiles()
        {
            var go = new GameObject("test", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            var tw = rt.DOAnchorPos(Vector2.one * 100f, 0.5f).SetAutoKill(false);
            Assert.IsNotNull(tw, "DOAnchorPos should return a Tween");
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        // ─── TweenHelper.KillSafe ───

        [Test]
        public void KillSafe_Tween_NullInput_NoThrow()
        {
            Tween tw = null;
            Assert.DoesNotThrow(() => TweenHelper.KillSafe(ref tw));
            Assert.IsNull(tw);
        }

        [Test]
        public void KillSafe_Tween_ActiveTween_Kills()
        {
            var go = new GameObject("test");
            Tween tw = go.transform.DOMove(Vector3.one, 1f).SetAutoKill(false);
            Assert.IsTrue(tw.IsActive());
            TweenHelper.KillSafe(ref tw);
            Assert.IsNull(tw);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void KillSafe_Sequence_NullInput_NoThrow()
        {
            Sequence seq = null;
            Assert.DoesNotThrow(() => TweenHelper.KillSafe(ref seq));
            Assert.IsNull(seq);
        }

        [Test]
        public void KillSafe_Sequence_ActiveSequence_Kills()
        {
            var seq = DOTween.Sequence().SetAutoKill(false);
            seq.AppendInterval(1f);
            Assert.IsTrue(seq.IsActive());
            TweenHelper.KillSafe(ref seq);
            Assert.IsNull(seq);
        }

        // ─── TweenHelper.FadeCanvasGroup ───

        [Test]
        public void FadeCanvasGroup_NullCG_ReturnsNull()
        {
            var tw = TweenHelper.FadeCanvasGroup(null, 1f, 0.5f);
            Assert.IsNull(tw);
        }

        [Test]
        public void FadeCanvasGroup_ReturnsActiveTween()
        {
            var go = new GameObject("test", typeof(CanvasGroup));
            var cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            var tw = TweenHelper.FadeCanvasGroup(cg, 1f, 0.5f);
            Assert.IsNotNull(tw);
            Assert.IsTrue(tw.IsActive());
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FadeCanvasGroup_CompleteAll_ReachesTarget()
        {
            var go = new GameObject("test", typeof(CanvasGroup));
            var cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            var tw = TweenHelper.FadeCanvasGroup(cg, 1f, 0.5f).SetAutoKill(false);
            tw.Play();
            CompleteAll();
            Assert.AreEqual(1f, cg.alpha, 0.01f);
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        // ─── TweenHelper.FadeImage ───

        [Test]
        public void FadeImage_NullImg_ReturnsNull()
        {
            var tw = TweenHelper.FadeImage(null, 0f, 0.5f);
            Assert.IsNull(tw);
        }

        [Test]
        public void FadeImage_ReturnsActiveTween()
        {
            var go = new GameObject("test", typeof(Image));
            var img = go.GetComponent<Image>();
            var c = img.color; c.a = 1f; img.color = c;
            var tw = TweenHelper.FadeImage(img, 0f, 0.5f);
            Assert.IsNotNull(tw);
            Assert.IsTrue(tw.IsActive());
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        // ─── TweenHelper.PulseAlpha (Image) ───

        [Test]
        public void PulseAlpha_Image_NullImg_ReturnsNull()
        {
            var tw = TweenHelper.PulseAlpha((Image)null, 0.1f, 0.5f, 2f);
            Assert.IsNull(tw);
        }

        [Test]
        public void PulseAlpha_Image_SetsInitialAlpha()
        {
            var go = new GameObject("test", typeof(Image));
            var img = go.GetComponent<Image>();
            var tw = TweenHelper.PulseAlpha(img, 0.1f, 0.5f, 2f);
            Assert.AreEqual(0.1f, img.color.a, 0.01f);
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        [Test]
        public void PulseAlpha_Image_IsInfiniteLoop()
        {
            var go = new GameObject("test", typeof(Image));
            var img = go.GetComponent<Image>();
            var tw = TweenHelper.PulseAlpha(img, 0.1f, 0.5f, 2f);
            Assert.AreEqual(-1, tw.Loops());
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        // ─── TweenHelper.PulseAlpha (CanvasGroup) ───

        [Test]
        public void PulseAlpha_CG_NullCG_ReturnsNull()
        {
            var tw = TweenHelper.PulseAlpha((CanvasGroup)null, 0.1f, 0.5f, 2f);
            Assert.IsNull(tw);
        }

        [Test]
        public void PulseAlpha_CG_SetsInitialAlpha()
        {
            var go = new GameObject("test", typeof(CanvasGroup));
            var cg = go.GetComponent<CanvasGroup>();
            var tw = TweenHelper.PulseAlpha(cg, 0.2f, 0.8f, 3f);
            Assert.AreEqual(0.2f, cg.alpha, 0.01f);
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        // ─── TweenHelper.ShakeUI ───

        [Test]
        public void ShakeUI_NullRT_ReturnsNull()
        {
            var tw = TweenHelper.ShakeUI(null);
            Assert.IsNull(tw);
        }

        [Test]
        public void ShakeUI_ReturnsActiveTween()
        {
            var go = new GameObject("test", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            var tw = TweenHelper.ShakeUI(rt);
            Assert.IsNotNull(tw);
            Assert.IsTrue(tw.IsActive());
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        // ─── TweenHelper.KillAllOn ───

        [Test]
        public void KillAllOn_NullGO_NoThrow()
        {
            Assert.DoesNotThrow(() => TweenHelper.KillAllOn(null));
        }

        [Test]
        public void KillAllOn_KillsTweensOnTarget()
        {
            var go = new GameObject("test");
            var tw = go.transform.DOMove(Vector3.one, 1f).SetTarget(go).SetAutoKill(false);
            Assert.IsTrue(tw.IsActive());
            TweenHelper.KillAllOn(go);
            Assert.IsFalse(tw.IsActive());
            Object.DestroyImmediate(go);
        }

        // ─── TweenMatFX.DOFloat ───

        [Test]
        public void TweenMatFX_DOFloat_NullMat_ReturnsNull()
        {
            var tw = TweenMatFX.DOFloat(null, "_TestProp", 1f, 0.5f);
            Assert.IsNull(tw);
        }

        [Test]
        public void TweenMatFX_DOFloat_MissingProperty_ReturnsNull()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Property.*not found"));
            var tw = TweenMatFX.DOFloat(mat, "_NonExistent", 1f, 0.5f);
            Assert.IsNull(tw);
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void TweenMatFX_DOFloat_ValidProperty_ReturnsTween()
        {
            // UI/Default shader has _StencilComp as a float property
            var mat = new Material(Shader.Find("UI/Default"));
            if (!mat.HasProperty("_StencilComp"))
            {
                Object.DestroyImmediate(mat);
                Assert.Inconclusive("UI/Default shader doesn't have _StencilComp in this Unity version");
                return;
            }
            var tw = TweenMatFX.DOFloat(mat, "_StencilComp", 5f, 0.5f);
            Assert.IsNotNull(tw);
            tw.Kill();
            Object.DestroyImmediate(mat);
        }

        // ─── TweenMatFX.DOColor ───

        [Test]
        public void TweenMatFX_DOColor_NullMat_ReturnsNull()
        {
            var tw = TweenMatFX.DOColor(null, "_Color", Color.red, 0.5f);
            Assert.IsNull(tw);
        }

        [Test]
        public void TweenMatFX_DOColor_MissingProperty_ReturnsNull()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Property.*not found"));
            var tw = TweenMatFX.DOColor(mat, "_NonExistent", Color.red, 0.5f);
            Assert.IsNull(tw);
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void TweenMatFX_DOColor_ValidProperty_ReturnsTween()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            if (!mat.HasProperty("_Color"))
            {
                Object.DestroyImmediate(mat);
                Assert.Inconclusive("UI/Default shader doesn't have _Color in this Unity version");
                return;
            }
            var tw = TweenMatFX.DOColor(mat, "_Color", Color.red, 0.5f);
            Assert.IsNotNull(tw);
            tw.Kill();
            Object.DestroyImmediate(mat);
        }

        // ─── TweenMatFX.DissolveSequence ───

        [Test]
        public void TweenMatFX_DissolveSequence_ReturnsSequence()
        {
            // Even with null material, should return an empty sequence (graceful)
            bool called = false;
            var seq = TweenMatFX.DissolveSequence(null, 0.6f, () => called = true);
            Assert.IsNotNull(seq);
            seq.Kill();
        }

        // ─── DOTweenTestBase functionality ───

        [Test]
        public void DOTweenTestBase_CompleteAll_ReachesEndValue()
        {
            var go = new GameObject("test");
            go.transform.position = Vector3.zero;
            var tw = go.transform.DOMove(Vector3.one * 10f, 1f)
                .SetAutoKill(false)
                .SetEase(Ease.Linear);
            tw.Play();
            CompleteAll();
            Assert.AreEqual(10f, go.transform.position.x, 0.01f);
            tw.Kill();
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DOTweenTestBase_CompleteAll_FinishesAllTweens()
        {
            var go = new GameObject("test");
            go.transform.position = Vector3.zero;
            var tw = go.transform.DOMove(Vector3.one * 10f, 5f).SetAutoKill(false);
            tw.Play();
            CompleteAll();
            Assert.AreEqual(10f, go.transform.position.x, 0.01f);
            tw.Kill();
            Object.DestroyImmediate(go);
        }
    }
}
