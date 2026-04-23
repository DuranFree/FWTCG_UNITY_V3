using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// Ambient breathe pulse on the battlefield panel overlay (5s sine loop).
    /// Control-based green/red glow has been removed per the unified border rule:
    ///   选中=绿 / 提示=蓝 / 回收=红 —— 战场占领不再用颜色来暗示。
    /// </summary>
    public class BattlefieldGlow : MonoBehaviour
    {
        [Tooltip("Semi-transparent Image used for the ambient breathe pulse.")]
        [SerializeField] private Image _ambientOverlay;

        public const float BREATHE_PERIOD  = 5f;
        public const float BREATHE_MIN_A   = 0.02f;
        public const float BREATHE_MAX_A   = 0.08f;

        private Tween _breatheTween;

        private void OnEnable()
        {
            if (_ambientOverlay == null) return;
            _breatheTween = DOVirtual.Float(0f, 1f, BREATHE_PERIOD, v =>
            {
                if (_ambientOverlay == null) return;
                float alpha = Mathf.Lerp(BREATHE_MIN_A, BREATHE_MAX_A,
                    (Mathf.Sin(v * Mathf.PI * 2f) + 1f) * 0.5f);
                _ambientOverlay.color = new Color(0.05f, 0.15f, 0.30f, alpha);
            }).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear)
              .SetTarget(_ambientOverlay.gameObject);
        }

        private void OnDisable()
        {
            TweenHelper.KillSafe(ref _breatheTween);
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _breatheTween);
        }
    }
}
