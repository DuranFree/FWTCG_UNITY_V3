using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-18 → DOT-5: Attached to a BF panel to provide two visual effects:
    ///   1. Ambient breathe — subtle alpha pulse on the background overlay (5s sine loop).
    ///   2. Control glow   — colour-coded border flashes when control changes (3s sine loop).
    ///
    /// Call SetControl(owner) from GameUI.UpdateBFCtrlGlow() each Refresh().
    /// All animations use DOTween loops (no coroutines).
    /// </summary>
    public class BattlefieldGlow : MonoBehaviour
    {
        [Tooltip("Semi-transparent Image used for the ambient breathe pulse.")]
        [SerializeField] private Image _ambientOverlay;

        [Tooltip("Coloured border Image used for the control glow pulse.")]
        [SerializeField] private Image _ctrlGlowOverlay;

        // Player / enemy glow colours
        private static readonly Color PlayerGlow = new Color(0.29f, 0.87f, 0.50f, 0f); // green, starts transparent
        private static readonly Color EnemyGlow  = new Color(0.97f, 0.44f, 0.44f, 0f); // red, starts transparent
        private static readonly Color NoGlow     = new Color(0f, 0f, 0f, 0f);

        // Ambient breathe parameters (public for test visibility)
        public const float BREATHE_PERIOD  = 5f;
        public const float BREATHE_MIN_A   = 0.02f;
        public const float BREATHE_MAX_A   = 0.08f;

        // Control glow parameters (public for test visibility)
        public const float CTRL_PERIOD     = 3f;
        public const float CTRL_MIN_A      = 0.10f;
        public const float CTRL_MAX_A      = 0.35f;

        private string _currentCtrl = null;
        private Tween _breatheTween;
        private Tween _ctrlTween;

        private void OnEnable()
        {
            if (_ambientOverlay != null)
            {
                _breatheTween = DOVirtual.Float(0f, 1f, BREATHE_PERIOD, v =>
                {
                    if (_ambientOverlay == null) return;
                    float alpha = Mathf.Lerp(BREATHE_MIN_A, BREATHE_MAX_A,
                        (Mathf.Sin(v * Mathf.PI * 2f) + 1f) * 0.5f);
                    _ambientOverlay.color = new Color(0.05f, 0.15f, 0.30f, alpha);
                }).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear)
                  .SetTarget(_ambientOverlay.gameObject);
            }

            if (_ctrlGlowOverlay != null)
            {
                _ctrlTween = DOVirtual.Float(0f, 1f, CTRL_PERIOD, v =>
                {
                    if (_ctrlGlowOverlay == null) return;
                    float pulse = (Mathf.Sin(v * Mathf.PI * 2f) + 1f) * 0.5f;
                    float alpha = Mathf.Lerp(CTRL_MIN_A, CTRL_MAX_A, pulse);

                    // DEV-26: when no controller, alpha must be 0
                    if (_currentCtrl == null)
                    {
                        _ctrlGlowOverlay.color = NoGlow;
                    }
                    else
                    {
                        Color baseCol = _currentCtrl == GameRules.OWNER_PLAYER ? PlayerGlow : EnemyGlow;
                        _ctrlGlowOverlay.color = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
                    }
                }).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear)
                  .SetTarget(_ctrlGlowOverlay.gameObject);
            }
        }

        private void OnDisable()
        {
            TweenHelper.KillSafe(ref _breatheTween);
            TweenHelper.KillSafe(ref _ctrlTween);
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _breatheTween);
            TweenHelper.KillSafe(ref _ctrlTween);
        }

        /// <summary>Called by GameUI each Refresh() to update which player controls this BF.</summary>
        public void SetControl(string ownerOrNull)
        {
            _currentCtrl = ownerOrNull;
        }
    }
}
