using DG.Tweening;
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
        public const float DOT_MAX_SIZE    = 5f;

        private static Sprite _circleSprite;
        private static Sprite GetCircleSprite()
        {
            if (_circleSprite == null)
                _circleSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            return _circleSprite;
        }

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
                img.sprite = GetCircleSprite();
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
                SpawnClickEffect(mouseCanvas);
        }

        // ── Click effect ──────────────────────────────────────────────────────

        private void SpawnClickEffect(Vector2 origin)
        {
            const float DURATION = 0.5f;

            // Ripple circle: scale 1→4 (20→80px), fade 0.8→0
            var rippleGO = CreateDot("ClickRipple", origin, 20f, _fgLayer);
            var rippleImg = rippleGO.GetComponent<Image>();
            var rippleRT  = rippleGO.GetComponent<RectTransform>();
            rippleImg.color = new Color(TRAIL_COLOR.r, TRAIL_COLOR.g, TRAIL_COLOR.b, 0.8f);

            var rippleSeq = DOTween.Sequence().SetTarget(rippleGO);
            rippleSeq.Append(rippleRT.DOScale(4f, DURATION).SetEase(Ease.OutCubic));
            rippleSeq.Join(rippleImg.DOFade(0f, DURATION).SetEase(Ease.InQuad));
            rippleSeq.OnComplete(() => Destroy(rippleGO));

            // Six hex flash dots fly outward
            const int HEX = 6;
            for (int i = 0; i < HEX; i++)
            {
                var hexGO  = CreateDot($"HexDot_{i}", origin, 5f, _fgLayer);
                var hexRT  = hexGO.GetComponent<RectTransform>();
                var hexImg = hexGO.GetComponent<Image>();
                hexImg.color = new Color(HEX_COLOR.r, HEX_COLOR.g, HEX_COLOR.b, 0.9f);

                float angle = i * (360f / HEX) * Mathf.Deg2Rad;
                Vector2 endPos = origin + new Vector2(Mathf.Cos(angle) * 38f, Mathf.Sin(angle) * 38f);

                var hexSeq = DOTween.Sequence().SetTarget(hexGO);
                hexSeq.Append(hexRT.DOAnchorPos(endPos, DURATION).SetEase(Ease.OutCubic));
                hexSeq.Join(hexRT.DOLocalRotate(new Vector3(0f, 0f, 180f), DURATION, RotateMode.FastBeyond360).SetEase(Ease.Linear));
                hexSeq.Join(hexImg.DOFade(0f, DURATION).SetEase(Ease.InQuad));
                hexSeq.OnComplete(() => Destroy(hexGO));
            }
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
            img.sprite = GetCircleSprite();
            img.raycastTarget = false;
            return go;
        }
    }
}
