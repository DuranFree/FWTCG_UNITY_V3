using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG
{
    /// <summary>
    /// DOTween helper utilities — null-safe kill, common UI tween shortcuts.
    /// All tweens created here use SetTarget for automatic cleanup via DOTween.Kill(target).
    /// </summary>
    public static class TweenHelper
    {
        /// <summary>Kill a tween if it's alive, then null out the reference.</summary>
        public static void KillSafe(ref Tween tween)
        {
            if (tween != null && tween.IsActive())
                tween.Kill();
            tween = null;
        }

        /// <summary>Kill a Sequence if it's alive, then null out the reference.</summary>
        public static void KillSafe(ref Sequence seq)
        {
            if (seq != null && seq.IsActive())
                seq.Kill();
            seq = null;
        }

        /// <summary>Fade a CanvasGroup to target alpha.</summary>
        public static Tween FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration,
            Ease ease = Ease.Linear)
        {
            if (cg == null) return null;
            return cg.DOFade(targetAlpha, duration).SetEase(ease).SetTarget(cg);
        }

        /// <summary>Fade an Image to target alpha (color.a).</summary>
        public static Tween FadeImage(Image img, float targetAlpha, float duration,
            Ease ease = Ease.Linear)
        {
            if (img == null) return null;
            return img.DOFade(targetAlpha, duration).SetEase(ease).SetTarget(img);
        }

        /// <summary>
        /// Infinite alpha pulse on an Image (yoyo between min and max).
        /// Starts from minAlpha. Returns the tween for later KillSafe.
        /// </summary>
        public static Tween PulseAlpha(Image img, float minAlpha, float maxAlpha, float period,
            Ease ease = Ease.InOutSine)
        {
            if (img == null) return null;
            var c = img.color;
            c.a = minAlpha;
            img.color = c;
            float halfPeriod = period * 0.5f;
            return img.DOFade(maxAlpha, halfPeriod)
                .SetEase(ease)
                .SetLoops(-1, LoopType.Yoyo)
                .SetTarget(img);
        }

        /// <summary>
        /// Infinite alpha pulse on a CanvasGroup (yoyo between min and max).
        /// </summary>
        public static Tween PulseAlpha(CanvasGroup cg, float minAlpha, float maxAlpha, float period,
            Ease ease = Ease.InOutSine)
        {
            if (cg == null) return null;
            cg.alpha = minAlpha;
            float halfPeriod = period * 0.5f;
            return DOTween.To(() => cg.alpha, x => cg.alpha = x, maxAlpha, halfPeriod)
                .SetEase(ease)
                .SetLoops(-1, LoopType.Yoyo)
                .SetTarget(cg);
        }

        /// <summary>Shake a RectTransform's anchoredPosition on X axis (damage shake style).</summary>
        public static Tween ShakeUI(RectTransform rt, float strength = 10f, float duration = 0.28f,
            int vibrato = 7)
        {
            if (rt == null) return null;
            return rt.DOShakeAnchorPos(duration, new Vector2(strength, 0f), vibrato, 0f)
                .SetTarget(rt);
        }

        /// <summary>Kill all DOTween tweens targeting a specific GameObject.</summary>
        public static void KillAllOn(GameObject go)
        {
            if (go != null)
                DOTween.Kill(go);
        }
    }
}
