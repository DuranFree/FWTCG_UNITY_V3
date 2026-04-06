using DG.Tweening;
using UnityEngine;

namespace FWTCG.FX
{
    /// <summary>
    /// DOTween-based material property animator — replacement for AnimMatFX.
    /// Drives float/color properties on a Material via DOTween Sequences.
    /// </summary>
    public static class TweenMatFX
    {
        /// <summary>
        /// Animate a float shader property to targetValue over duration.
        /// Returns null if the material or property is invalid.
        /// </summary>
        public static Tween DOFloat(Material mat, string propertyName, float targetValue,
            float duration, Ease ease = Ease.Linear)
        {
            if (mat == null) return null;
            if (!mat.HasProperty(propertyName))
            {
                Debug.LogWarning($"[TweenMatFX] Property '{propertyName}' not found on {mat.name}.");
                return null;
            }
            return mat.DOFloat(targetValue, propertyName, duration).SetEase(ease);
        }

        /// <summary>
        /// Animate a color shader property to targetColor over duration.
        /// Returns null if the material or property is invalid.
        /// </summary>
        public static Tween DOColor(Material mat, string propertyName, Color targetColor,
            float duration, Ease ease = Ease.Linear)
        {
            if (mat == null) return null;
            if (!mat.HasProperty(propertyName))
            {
                Debug.LogWarning($"[TweenMatFX] Property '{propertyName}' not found on {mat.name}.");
                return null;
            }
            return mat.DOColor(targetColor, propertyName, duration).SetEase(ease);
        }

        /// <summary>
        /// Build a dissolve sequence: animate noise_fade 0→1 then invoke callback.
        /// Drop-in replacement for AnimMatFX dissolve pattern.
        /// </summary>
        public static Sequence DissolveSequence(Material mat, float duration,
            TweenCallback onComplete = null)
        {
            var seq = DOTween.Sequence();
            var floatTween = DOFloat(mat, "noise_fade", 1f, duration);
            if (floatTween != null)
                seq.Append(floatTween);
            if (onComplete != null)
                seq.AppendCallback(onComplete);
            return seq;
        }
    }
}
