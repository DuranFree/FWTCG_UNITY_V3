using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-18: Attached to a BF panel to provide two visual effects:
    ///   1. Ambient breathe — subtle alpha pulse on the background overlay (5s loop).
    ///   2. Control glow   — colour-coded border flashes when control changes (3s loop).
    ///
    /// Call SetControl(owner) from GameUI.UpdateBFCtrlGlow() each Refresh().
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

        // Ambient breathe parameters
        private const float BREATHE_PERIOD  = 5f;
        private const float BREATHE_MIN_A   = 0.02f;
        private const float BREATHE_MAX_A   = 0.08f;

        // Control glow parameters
        private const float CTRL_PERIOD     = 3f;
        private const float CTRL_MIN_A      = 0.10f;
        private const float CTRL_MAX_A      = 0.35f;

        private string _currentCtrl = null;
        private Coroutine _breatheRoutine;
        private Coroutine _ctrlRoutine;

        private void OnEnable()
        {
            if (_ambientOverlay != null)
                _breatheRoutine = StartCoroutine(AmbientBreatheLoop());
            if (_ctrlGlowOverlay != null)
                _ctrlRoutine = StartCoroutine(CtrlGlowLoop());
        }

        private void OnDisable()
        {
            if (_breatheRoutine != null) { StopCoroutine(_breatheRoutine); _breatheRoutine = null; }
            if (_ctrlRoutine    != null) { StopCoroutine(_ctrlRoutine);    _ctrlRoutine    = null; }
        }

        /// <summary>Called by GameUI each Refresh() to update which player controls this BF.</summary>
        public void SetControl(string ownerOrNull)
        {
            _currentCtrl = ownerOrNull;
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator AmbientBreatheLoop()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime / BREATHE_PERIOD;
                float alpha = Mathf.Lerp(BREATHE_MIN_A, BREATHE_MAX_A,
                                         (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f);
                _ambientOverlay.color = new Color(0.05f, 0.15f, 0.30f, alpha);
                yield return null;
            }
        }

        private IEnumerator CtrlGlowLoop()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime / CTRL_PERIOD;
                float pulse = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(CTRL_MIN_A, CTRL_MAX_A, pulse);

                Color baseCol = _currentCtrl == GameRules.OWNER_PLAYER ? PlayerGlow
                              : _currentCtrl == GameRules.OWNER_ENEMY  ? EnemyGlow
                              : NoGlow;

                _ctrlGlowOverlay.color = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
                yield return null;
            }
        }
    }
}
