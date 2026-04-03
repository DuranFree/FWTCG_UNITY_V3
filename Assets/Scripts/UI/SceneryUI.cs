using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-23: Ambient decorative effects for the game board.
    ///
    /// Manages five categories of purely cosmetic animation:
    ///   1. Center spinning rings  (spin-slow 20s CW / 12s CCW)
    ///   2. Center sigil rotation  (30s CW outer / 20s CCW inner)
    ///   3. Divider energy orb     (3.5s sinusoidal Y oscillation)
    ///   4. Corner gem pulse       (4s alpha flash, all 4 corners in sync)
    ///   5. Legend slot glow       (5s breathing alpha, player + enemy)
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

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (spinOuter  != null) StartCoroutine(SpinLoop(spinOuter.rectTransform,  SPIN_OUTER_DURATION,  clockwise: true));
            if (spinInner  != null) StartCoroutine(SpinLoop(spinInner.rectTransform,  SPIN_INNER_DURATION,  clockwise: false));
            if (sigilOuter != null) StartCoroutine(SpinLoop(sigilOuter.rectTransform, SIGIL_OUTER_DURATION, clockwise: true));
            if (sigilInner != null) StartCoroutine(SpinLoop(sigilInner.rectTransform, SIGIL_INNER_DURATION, clockwise: false));

            if (dividerOrb != null) StartCoroutine(DividerOrbLoop());
            if (cornerGems != null && cornerGems.Length > 0) StartCoroutine(CornerGemLoop());
            if (playerLegendGlow != null) StartCoroutine(LegendGlowLoop(playerLegendGlow));
            if (enemyLegendGlow  != null) StartCoroutine(LegendGlowLoop(enemyLegendGlow));
        }

        // ── Private coroutines ────────────────────────────────────────────────

        /// <summary>Continuously rotates <paramref name="rt"/> one full revolution per <paramref name="secondsPerRev"/>.</summary>
        private static IEnumerator SpinLoop(RectTransform rt, float secondsPerRev, bool clockwise)
        {
            float sign = clockwise ? -1f : 1f; // Unity: negative Z = CW when viewed from front
            float accum = 0f;
            while (rt != null) // M-01: exit if target destroyed
            {
                accum = Mathf.Repeat(accum + Time.deltaTime, secondsPerRev); // L-02: prevent float drift
                float angle = sign * (accum / secondsPerRev) * 360f;
                rt.localEulerAngles = new Vector3(0f, 0f, angle);
                yield return null;
            }
        }

        /// <summary>Sinusoidally oscillates the divider orb ±<see cref="DIVIDER_ORB_AMPLITUDE"/> px over <see cref="DIVIDER_ORB_DURATION"/> s.</summary>
        private IEnumerator DividerOrbLoop()
        {
            var rt = dividerOrb.rectTransform;
            // DEV-26: only cache base Y; X reads live so layout changes don't drift the center
            float baseY = rt.anchoredPosition.y;
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float y = Mathf.Sin(t / DIVIDER_ORB_DURATION * Mathf.PI * 2f) * DIVIDER_ORB_AMPLITUDE;
                var pos = rt.anchoredPosition;
                rt.anchoredPosition = new Vector2(pos.x, baseY + y);
                yield return null;
            }
        }

        /// <summary>Pulses all corner gem alphas between <see cref="CORNER_GEM_ALPHA_MIN"/> and <see cref="CORNER_GEM_ALPHA_MAX"/>.</summary>
        private IEnumerator CornerGemLoop()
        {
            while (true)
            {
                float half = CORNER_GEM_DURATION * 0.5f;
                // Fade in
                for (float t = 0f; t < half; t += Time.deltaTime)
                {
                    float a = Mathf.Lerp(CORNER_GEM_ALPHA_MIN, CORNER_GEM_ALPHA_MAX, t / half);
                    SetCornerAlpha(a);
                    yield return null;
                }
                // Fade out
                for (float t = 0f; t < half; t += Time.deltaTime)
                {
                    float a = Mathf.Lerp(CORNER_GEM_ALPHA_MAX, CORNER_GEM_ALPHA_MIN, t / half);
                    SetCornerAlpha(a);
                    yield return null;
                }
            }
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

        /// <summary>Breathes <paramref name="img"/> alpha between glow min and max over <see cref="LEGEND_GLOW_DURATION"/> s.</summary>
        private static IEnumerator LegendGlowLoop(Image img)
        {
            while (img != null) // M-01: exit if target destroyed
            {
                float half = LEGEND_GLOW_DURATION * 0.5f;
                // Glow up
                for (float t = 0f; t < half; t += Time.deltaTime)
                {
                    float a = Mathf.Lerp(LEGEND_GLOW_ALPHA_MIN, LEGEND_GLOW_ALPHA_MAX, t / half);
                    Color c = img.color;
                    img.color = new Color(c.r, c.g, c.b, a);
                    yield return null;
                }
                // Glow down
                for (float t = 0f; t < half; t += Time.deltaTime)
                {
                    float a = Mathf.Lerp(LEGEND_GLOW_ALPHA_MAX, LEGEND_GLOW_ALPHA_MIN, t / half);
                    Color c = img.color;
                    img.color = new Color(c.r, c.g, c.b, a);
                    yield return null;
                }
            }
        }
    }
}
