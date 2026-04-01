using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-21: Mouse trail + click effects.
    ///
    /// Trail: TRAIL_LENGTH (18) dots that follow the mouse each frame.
    ///   - Head dot is the largest (DOT_MAX_SIZE) and most opaque (TRAIL_HEAD_ALPHA).
    ///   - Each subsequent dot shrinks and fades linearly.
    ///   - Colour: Hextech cyan.
    ///
    /// Click effect (left mouse button):
    ///   - Expanding ripple circle (20 → 80 px, alpha 0.8 → 0, 0.5 s).
    ///   - Six hex-flash gold dots fly outward (0 → 35 px radius, 0.5 s).
    /// </summary>
    public class MouseTrail : MonoBehaviour
    {
        // ── Public constants (used by tests) ──────────────────────────────────
        public const int   TRAIL_LENGTH    = 18;
        public const float TRAIL_HEAD_ALPHA = 0.65f;
        public const float DOT_MAX_SIZE    = 8f;

        // ── Inspector refs ────────────────────────────────────────────────────
        [SerializeField] public RectTransform _canvasRect;
        [SerializeField] public Canvas        _canvas;
        [SerializeField] public Transform     _fgLayer;   // top-most canvas layer

        // ── Internal state ────────────────────────────────────────────────────
        private RectTransform[] _dotRts;
        private Image[]         _dotImgs;
        private Vector2[]       _positions;

        private static readonly Color TRAIL_COLOR = new Color(0.04f, 0.78f, 0.73f, 1f);
        private static readonly Color HEX_COLOR   = new Color(0.78f, 0.67f, 0.43f, 1f);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (_fgLayer == null || _canvasRect == null) { enabled = false; return; }

            _dotRts    = new RectTransform[TRAIL_LENGTH];
            _dotImgs   = new Image[TRAIL_LENGTH];
            _positions = new Vector2[TRAIL_LENGTH];

            Vector2 offscreen = new Vector2(-9999f, -9999f);

            for (int i = 0; i < TRAIL_LENGTH; i++)
            {
                var go = new GameObject($"TrailDot_{i}");
                go.transform.SetParent(_fgLayer, false);

                var rt = go.AddComponent<RectTransform>();
                float sz = DOT_MAX_SIZE * (1f - (float)i / TRAIL_LENGTH);
                sz = Mathf.Max(sz, 1f);
                rt.sizeDelta = new Vector2(sz, sz);

                var img = go.AddComponent<Image>();
                img.color = new Color(TRAIL_COLOR.r, TRAIL_COLOR.g, TRAIL_COLOR.b, 0f);
                img.raycastTarget = false;

                _dotRts[i]    = rt;
                _dotImgs[i]   = img;
                _positions[i] = offscreen;
            }
        }

        private void Update()
        {
            if (_dotRts == null) return;

            Vector2 mouseCanvas;
            bool inRect = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, Input.mousePosition, _canvas.worldCamera, out mouseCanvas);

            if (!inRect) return;

            // Shift: oldest position moves to end, newest goes to index 0
            for (int i = TRAIL_LENGTH - 1; i > 0; i--)
                _positions[i] = _positions[i - 1];
            _positions[0] = mouseCanvas;

            // Update dots
            for (int i = 0; i < TRAIL_LENGTH; i++)
            {
                _dotRts[i].anchoredPosition = _positions[i];
                float alpha = TRAIL_HEAD_ALPHA * (1f - (float)i / TRAIL_LENGTH);
                var c = _dotImgs[i].color;
                c.a = alpha;
                _dotImgs[i].color = c;
            }

            // Spawn click effect
            if (Input.GetMouseButtonDown(0))
                StartCoroutine(ClickEffectRoutine(mouseCanvas));
        }

        // ── Click effect ──────────────────────────────────────────────────────

        private IEnumerator ClickEffectRoutine(Vector2 origin)
        {
            const float DURATION = 0.5f;

            // Ripple circle
            var rippleGO = CreateDot("ClickRipple", origin, 20f, _fgLayer);
            var rippleImg = rippleGO.GetComponent<Image>();
            var rippleRT  = rippleGO.GetComponent<RectTransform>();
            rippleImg.color = new Color(TRAIL_COLOR.r, TRAIL_COLOR.g, TRAIL_COLOR.b, 0.8f);

            // Six hex flash dots
            const int HEX = 6;
            var hexGOs  = new GameObject[HEX];
            var hexRTs  = new RectTransform[HEX];
            var hexImgs = new Image[HEX];
            for (int i = 0; i < HEX; i++)
            {
                hexGOs[i]  = CreateDot($"HexDot_{i}", origin, 5f, _fgLayer);
                hexRTs[i]  = hexGOs[i].GetComponent<RectTransform>();
                hexImgs[i] = hexGOs[i].GetComponent<Image>();
                hexImgs[i].color = new Color(HEX_COLOR.r, HEX_COLOR.g, HEX_COLOR.b, 0.9f);
            }

            float elapsed = 0f;
            while (elapsed < DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / DURATION);

                // Ripple: 20 → 80 px, alpha → 0
                float rippleSize = Mathf.Lerp(20f, 80f, t);
                rippleRT.sizeDelta = new Vector2(rippleSize, rippleSize);
                var rc = rippleImg.color;
                rc.a = (1f - t) * 0.8f;
                rippleImg.color = rc;

                // Hex dots fly outward
                for (int i = 0; i < HEX; i++)
                {
                    float angle  = i * (360f / HEX) * Mathf.Deg2Rad;
                    float radius = Mathf.Lerp(0f, 38f, t);
                    hexRTs[i].anchoredPosition = origin + new Vector2(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius
                    );
                    var hc = hexImgs[i].color;
                    hc.a = (1f - t) * 0.9f;
                    hexImgs[i].color = hc;
                }

                yield return null;
            }

            Destroy(rippleGO);
            for (int i = 0; i < HEX; i++) Destroy(hexGOs[i]);
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private static GameObject CreateDot(string name, Vector2 pos, float size, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return go;
        }
    }
}
