using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace FWTCG.UI
{
    /// <summary>
    /// Three-state countdown ring:
    ///   time01 (baseRing)  — always visible as background
    ///   time02 (blueFill)  — Radial360 fill, blue gems, first 2/3 of turn
    ///   time03 (redFill)   — Radial360 fill, red gems, final 1/3 of turn
    ///
    /// Call SetProgress(0→1) where 0=turn just started, 1=turn expired.
    /// </summary>
    public class CountdownRingUI : MonoBehaviour
    {
        [SerializeField] public Image baseRing;   // time01 — empty ring, always shown
        [SerializeField] public Image blueFill;   // time02 — blue gems, Radial360
        [SerializeField] public Image redFill;    // time03 — red gems,  Radial360

        private const float PHASE_SWITCH = 2f / 3f;
        private bool _hasFlashed;
        private Tween _flashTween;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Set timer progress. 0 = just started, 1 = expired.</summary>
        public void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (progress < PHASE_SWITCH)
            {
                _hasFlashed = false;

                float blueT = progress / PHASE_SWITCH;
                if (blueFill != null)
                {
                    blueFill.gameObject.SetActive(true);
                    blueFill.fillAmount = blueT;
                }
                if (redFill != null) redFill.gameObject.SetActive(false);
            }
            else
            {
                if (!_hasFlashed) { TriggerFlash(); _hasFlashed = true; }

                if (blueFill != null) blueFill.gameObject.SetActive(false);

                float redT = (progress - PHASE_SWITCH) / (1f - PHASE_SWITCH);
                if (redFill != null)
                {
                    redFill.gameObject.SetActive(true);
                    redFill.fillAmount = Mathf.Clamp01(redT);
                }
            }
        }

        /// <summary>Reset to empty state (turn not started).</summary>
        public void ResetRing()
        {
            _hasFlashed = false;
            _flashTween?.Kill();
            if (baseRing != null) baseRing.color = Color.white;
            if (blueFill != null) { blueFill.fillAmount = 0f; blueFill.gameObject.SetActive(false); }
            if (redFill  != null) { redFill.fillAmount  = 0f; redFill.gameObject.SetActive(false); }
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void TriggerFlash()
        {
            if (baseRing == null) return;
            _flashTween?.Kill();
            // Brief brightness pulse: white → 3× bright → white, over 0.5 s
            _flashTween = DOVirtual.Float(0f, 1f, 0.5f, v =>
            {
                float b = 1f + Mathf.Sin(v * Mathf.PI) * 2f;
                if (baseRing != null) baseRing.color = new Color(b, b, b, 1f);
                if (blueFill != null) blueFill.color  = new Color(b, b, b, 1f);
            })
            .OnComplete(() =>
            {
                if (baseRing != null) baseRing.color = Color.white;
                if (blueFill != null) blueFill.color  = Color.white;
            })
            .SetTarget(gameObject);
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _flashTween);
        }
    }
}
