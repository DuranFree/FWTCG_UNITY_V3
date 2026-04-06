using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-22: Drag portal / vortex visual effect.
    ///
    /// Creates three concentric spinning rings in UGUI space that follow the drag ghost.
    /// Instantiated lazily on first Show() call and reused across drags.
    ///
    /// Rings:
    ///   Ring 0 (outer):  60px radius, CW, 3s/rev, blue rgba(60,140,255)
    ///   Ring 1 (mid):    42px radius, CCW, 2s/rev, cyan rgba(0,200,180)
    ///   Ring 2 (inner):  24px radius, CW, 1.3s/rev, white rgba(220,240,255)
    /// Plus 8 orbital particles at Ring 0 radius, counter-rotating.
    ///
    /// DOT-3: FadeRoutine coroutine → DOFade tween.
    /// </summary>
    public class PortalVFX : MonoBehaviour
    {
        // ── Constants ────────────────────────────────────────────────────────
        public const int   RING_COUNT          = 3;
        public const float RING_OUTER_RADIUS   = 60f;
        public const float RING_MID_RADIUS     = 42f;
        public const float RING_INNER_RADIUS   = 24f;
        public const float RING_THICKNESS      = 5f;
        public const int   ORBITAL_COUNT       = 8;
        public const float ORBITAL_RADIUS      = 55f;
        public const float ORBITAL_DOT_SIZE    = 7f;
        public const float FADE_IN_DURATION    = 0.28f;
        public const float FADE_OUT_DURATION   = 0.22f;

        // ── References ───────────────────────────────────────────────────────
        [SerializeField] private Canvas _rootCanvas;

        // ── Runtime state ────────────────────────────────────────────────────
        private GameObject _vfxRoot;
        private CanvasGroup _vfxCg;
        private RectTransform _vfxRT;
        private Image[] _rings;
        private RectTransform[] _ringRTs;
        private float[] _ringSpeed;    // degrees / second (negative = CCW)
        private Image[] _orbitals;
        private RectTransform[] _orbitalRTs;
        private bool _visible;

        // ── DOTween state ────────────────────────────────────────────────────
        private Tween _fadeTween;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Show portal VFX at canvas-local <paramref name="canvasPos"/>.</summary>
        public void Show(Vector2 canvasPos)
        {
            EnsureBuilt();
            _vfxRT.localPosition = new Vector3(canvasPos.x, canvasPos.y, 0f);
            _vfxRoot.SetActive(true);
            _visible = true;

            TweenHelper.KillSafe(ref _fadeTween);
            if (_vfxCg != null)
            {
                _vfxCg.alpha = 0f;
                _fadeTween = _vfxCg.DOFade(1f, FADE_IN_DURATION)
                    .SetTarget(gameObject);
            }
        }

        /// <summary>Move portal to new canvas-local position (called from OnDrag).</summary>
        public void MoveTo(Vector2 canvasPos)
        {
            if (_vfxRT != null)
                _vfxRT.localPosition = new Vector3(canvasPos.x, canvasPos.y, 0f);
        }

        /// <summary>Fade out and hide the portal.</summary>
        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            TweenHelper.KillSafe(ref _fadeTween);
            if (_vfxCg != null)
            {
                _fadeTween = _vfxCg.DOFade(0f, FADE_OUT_DURATION)
                    .SetTarget(gameObject)
                    .OnComplete(() =>
                    {
                        if (_vfxRoot != null) _vfxRoot.SetActive(false);
                        _fadeTween = null;
                    });
            }
        }

        // ── Unity update ─────────────────────────────────────────────────────

        private void Update()
        {
            if (_vfxRoot == null || !_vfxRoot.activeSelf) return;

            float dt = Time.deltaTime;

            // Spin rings
            if (_ringRTs != null)
            {
                for (int i = 0; i < _ringRTs.Length; i++)
                {
                    if (_ringRTs[i] == null) continue;
                    _ringRTs[i].Rotate(0f, 0f, _ringSpeed[i] * dt);
                }
            }

            // Orbit particles
            if (_orbitalRTs != null)
            {
                float t = Time.time;
                for (int i = 0; i < _orbitalRTs.Length; i++)
                {
                    if (_orbitalRTs[i] == null) continue;
                    float angle = t * (-90f) + i * (360f / ORBITAL_COUNT); // CCW at 90 deg/s
                    float rad   = angle * Mathf.Deg2Rad;
                    _orbitalRTs[i].anchoredPosition = new Vector2(
                        Mathf.Cos(rad) * ORBITAL_RADIUS,
                        Mathf.Sin(rad) * ORBITAL_RADIUS);
                }
            }
        }

        // ── Construction ─────────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_vfxRoot != null) return;

            // Find root canvas
            if (_rootCanvas == null)
            {
                _rootCanvas = GetComponentInParent<Canvas>();
                // Ensure we have the root canvas, not an intermediate one
                if (_rootCanvas != null && !_rootCanvas.isRootCanvas)
                    _rootCanvas = _rootCanvas.rootCanvas;
            }

            if (_rootCanvas == null) return;

            // Build container
            _vfxRoot = new GameObject("PortalVFX", typeof(RectTransform), typeof(CanvasGroup));
            _vfxRoot.transform.SetParent(_rootCanvas.transform, worldPositionStays: false);
            _vfxRoot.transform.SetAsLastSibling();

            _vfxRT = _vfxRoot.GetComponent<RectTransform>();
            _vfxRT.sizeDelta = Vector2.zero;

            _vfxCg = _vfxRoot.GetComponent<CanvasGroup>();
            _vfxCg.alpha = 0f;
            _vfxCg.blocksRaycasts = false;
            _vfxCg.interactable   = false;

            // Build rings
            float[] radii  = { RING_OUTER_RADIUS, RING_MID_RADIUS, RING_INNER_RADIUS };
            float[] speeds = { 120f, -180f, 240f }; // CW, CCW, CW deg/s
            Color[] colors =
            {
                new Color(0.235f, 0.549f, 1.000f, 0.85f),  // blue
                new Color(0.000f, 0.784f, 0.706f, 0.80f),  // cyan
                new Color(0.863f, 0.941f, 1.000f, 0.90f),  // near-white
            };

            _rings   = new Image[RING_COUNT];
            _ringRTs = new RectTransform[RING_COUNT];
            _ringSpeed = speeds;

            for (int i = 0; i < RING_COUNT; i++)
            {
                var ring = new GameObject($"Ring{i}", typeof(RectTransform), typeof(Image));
                ring.transform.SetParent(_vfxRoot.transform, worldPositionStays: false);

                _ringRTs[i] = ring.GetComponent<RectTransform>();
                float size  = radii[i] * 2f;
                _ringRTs[i].sizeDelta = new Vector2(size, size);
                _ringRTs[i].anchoredPosition = Vector2.zero;

                _rings[i] = ring.GetComponent<Image>();
                _rings[i].color = colors[i];

                _rings[i].raycastTarget = false;

                float alpha = colors[i].a * (1f - (float)i / RING_COUNT * 0.3f);
                _rings[i].color = new Color(colors[i].r, colors[i].g, colors[i].b, alpha * 0.4f);
                _rings[i].raycastTarget = false;
            }

            // Build orbital particles
            _orbitals   = new Image[ORBITAL_COUNT];
            _orbitalRTs = new RectTransform[ORBITAL_COUNT];

            for (int i = 0; i < ORBITAL_COUNT; i++)
            {
                var dot = new GameObject($"Orbital{i}", typeof(RectTransform), typeof(Image));
                dot.transform.SetParent(_vfxRoot.transform, worldPositionStays: false);

                _orbitalRTs[i] = dot.GetComponent<RectTransform>();
                _orbitalRTs[i].sizeDelta = new Vector2(ORBITAL_DOT_SIZE, ORBITAL_DOT_SIZE);

                _orbitals[i] = dot.GetComponent<Image>();
                float brightness = 0.6f + (i % 2) * 0.4f;
                _orbitals[i].color = new Color(
                    0.235f * brightness + 0.765f,
                    0.549f * brightness + 0.451f,
                    1.000f, 0.90f);
                _orbitals[i].raycastTarget = false;
            }

            _vfxRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _fadeTween);
            if (_vfxRoot != null)
                Destroy(_vfxRoot);
        }
    }
}
