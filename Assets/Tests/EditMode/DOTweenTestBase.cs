using DG.Tweening;
using DG.Tweening.Core;
using NUnit.Framework;
using UnityEngine;

namespace FWTCG.Tests.EditMode
{
    /// <summary>
    /// Base class for DOTween tests — ensures DOTween is initialized and cleaned up.
    /// Inherit from this instead of raw [TestFixture] when testing tween-related code.
    /// Uses SetupInstance() which works in EditMode without requiring a live MonoBehaviour.
    /// </summary>
    public abstract class DOTweenTestBase
    {
        [SetUp]
        public virtual void SetUp()
        {
            // Ensure DOTween component exists on a GameObject
            if (DOTween.instance == null)
            {
                var go = new GameObject("[DOTween]");
                go.hideFlags = HideFlags.HideAndDontSave;
                DOTween.instance = go.AddComponent<DOTweenComponent>();
            }
            DOTween.Init(recycleAllByDefault: false, useSafeMode: false);
            DOTween.defaultAutoPlay = AutoPlay.All;
            DOTween.defaultAutoKill = false;
        }

        [TearDown]
        public virtual void TearDown()
        {
            DOTween.KillAll();
            DOTween.Clear();
            if (DOTween.instance != null)
            {
                Object.DestroyImmediate(DOTween.instance.gameObject);
            }
        }

        /// <summary>
        /// Advance tweens to completion instantly.
        /// Works reliably in EditMode (ManualUpdate does not work in batchmode).
        /// </summary>
        protected void CompleteAll()
        {
            DOTween.CompleteAll();
        }
    }
}
