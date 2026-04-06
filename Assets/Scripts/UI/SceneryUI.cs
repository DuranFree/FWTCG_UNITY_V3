using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-23 → DOT-5: Ambient decorative effects for the game board.
    ///
    /// Manages five categories of purely cosmetic animation via DOTween loops:
    ///   1. Center spinning rings  (spin-slow 20s CW / 12s CCW)
    ///   2. Center sigil rotation  (30s CW outer / 20s CCW inner)
    ///   3. Divider energy orb     (3.5s sinusoidal Y oscillation)
    ///   4. Corner gem pulse       (4s alpha flash, all 4 corners in sync)
    ///   5. Legend slot glow       (5s breathing alpha — disabled, see VFX-7)
    ///
    /// All Image references are wired by SceneBuilder at build time.
    /// Null-safe: missing refs are silently skipped.
    /// </summary>
    public class SceneryUI : MonoBehaviour
    {
        // ── Duration constants (public for test visibility) ──────────────────
        public const float SPIN_OUTER_DURATION  = 20f;   // seconds per revolution (CW)
        public const float SPIN_INNER_DURATION  = 12f;   // seconds per revolution (CCW)
        public const float SIGIL_OUTER_DURATION = 30f;   // seconds per revolution (CW)
        public const float SIGIL_INNER_DURATION = 20f;   // seconds per revolution (CCW)
        public const float DIVIDER_ORB_DURATION = 3.5f;  // seconds per oscillation cycle
        public const float CORNER_GEM_DURATION  = 4f;    // seconds per pulse cycle
        public const float LEGEND_GLOW_DURATION = 5f;    // seconds per breath cycle

        // ── Alpha range constants ─────────────────────────────────────────────
        public const float CORNER_GEM_ALPHA_MIN  = 0.3f;
        public const float CORNER_GEM_ALPHA_MAX  = 0.9f;
        public const float LEGEND_GLOW_ALPHA_MIN = 0.15f;
        public const float LEGEND_GLOW_ALPHA_MAX = 0.6f;

        // ── Divider orb oscillation ───────────────────────────────────────────
        public const float DIVIDER_ORB_AMPLITUDE = 20f;  // pixels

        // ── Wired by SceneBuilder ─────────────────────────────────────────────
        [SerializeField] public Image spinOuter;          // 20s CW ring
        [SerializeField] public Image spinInner;          // 12s CCW ring
        [SerializeField] public Image sigilOuter;         // 30s CW sigil layer
        [SerializeField] public Image sigilInner;         // 20s CCW sigil layer
        [SerializeField] public Image dividerOrb;         // oscillating energy orb
        [SerializeField] public Image[] cornerGems;       // 4 corner L-bracket gems
        [SerializeField] public Image playerLegendGlow;   // player hero slot glow
        [SerializeField] public Image enemyLegendGlow;    // enemy hero slot glow

        // ── DOTween handles ──────────────────────────────────────────────────
        private Tween _spinOuterTween;
        private Tween _spinInnerTween;
        private Tween _sigilOuterTween;
        private Tween _sigilInnerTween;
        private Tween _dividerOrbTween;
        private Tween _cornerGemTween;
        // VFX-7: legend glow disabled — gold frame border replaces breathing effect.
        // Re-enable via: TweenHelper.PulseAlpha(img, LEGEND_GLOW_ALPHA_MIN, LEGEND_GLOW_ALPHA_MAX, LEGEND_GLOW_DURATION)

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (spinOuter  != null) _spinOuterTween  = CreateSpinTween(spinOuter.rectTransform,  SPIN_OUTER_DURATION,  true);
            if (spinInner  != null) _spinInnerTween  = CreateSpinTween(spinInner.rectTransform,  SPIN_INNER_DURATION,  false);
            if (sigilOuter != null) _sigilOuterTween = CreateSpinTween(sigilOuter.rectTransform, SIGIL_OUTER_DURATION, true);
            if (sigilInner != null) _sigilInnerTween = CreateSpinTween(sigilInner.rectTransform, SIGIL_INNER_DURATION, false);

            if (dividerOrb != null)
            {
                var rt = dividerOrb.rectTransform;
                // DEV-26: only cache base Y; X reads live so layout changes don't drift the center
                float baseY = rt.anchoredPosition.y;
                _dividerOrbTween = DOVirtual.Float(0f, 1f, DIVIDER_ORB_DURATION, v =>
                {
                    if (rt == null) return;
                    float y = Mathf.Sin(v * Mathf.PI * 2f) * DIVIDER_ORB_AMPLITUDE;
                    var pos = rt.anchoredPosition;
                    rt.anchoredPosition = new Vector2(pos.x, baseY + y);
                }).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear)
                  .SetTarget(dividerOrb.gameObject);
            }

            if (cornerGems != null && cornerGems.Length > 0)
            {
                _cornerGemTween = DOVirtual.Float(CORNER_GEM_ALPHA_MIN, CORNER_GEM_ALPHA_MAX,
                    CORNER_GEM_DURATION * 0.5f, SetCornerAlpha)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.Linear)
                    .SetTarget(gameObject);
            }
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _spinOuterTween);
            TweenHelper.KillSafe(ref _spinInnerTween);
            TweenHelper.KillSafe(ref _sigilOuterTween);
            TweenHelper.KillSafe(ref _sigilInnerTween);
            TweenHelper.KillSafe(ref _dividerOrbTween);
            TweenHelper.KillSafe(ref _cornerGemTween);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Creates an infinite spin tween (CW = negative Z, CCW = positive Z).</summary>
        private static Tween CreateSpinTween(RectTransform rt, float secondsPerRev, bool clockwise)
        {
            float endZ = clockwise ? -360f : 360f;
            return rt.DOLocalRotate(new Vector3(0f, 0f, endZ), secondsPerRev, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetTarget(rt.gameObject);
        }

        private void SetCornerAlpha(float a)
        {
            foreach (var gem in cornerGems)
            {
                if (gem == null) continue;
                Color c = gem.color;
                gem.color = new Color(c.r, c.g, c.b, a);
            }
        }
    }
}
