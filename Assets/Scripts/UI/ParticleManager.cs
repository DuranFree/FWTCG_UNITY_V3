using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-21: Ambient particle effects manager.
    ///
    /// Creates and animates (all via Update / coroutine-free):
    ///   - BG_COUNT (55) background floating particles — gold/cyan/blue/purple dots
    ///   - RUNE_COUNT (8) Norse rune glyphs — rotating + floating up
    ///   - FIREFLY_COUNT (12) firefly particles — sinusoidal oscillation + alpha pulse
    ///   - MIST_COUNT (4) bottom mist layers — slow horizontal drift
    ///   - LINE_POOL (80) connection line segments — pairs within LINE_RADIUS (90 px)
    ///
    /// All child GameObjects are created at Start inside _bgLayer.
    /// raycastTarget = false on every particle so clicks pass through.
    /// </summary>
    public class ParticleManager : MonoBehaviour
    {
        // ── Public constants (used by tests) ──────────────────────────────────
        public const int   BG_COUNT    = 55;
        public const int   RUNE_COUNT  = 8;
        public const int   FIREFLY_COUNT = 12;
        public const int   MIST_COUNT  = 4;
        public const int   LINE_POOL   = 80;
        public const float LINE_RADIUS = 90f;

        // ── Inspector refs ────────────────────────────────────────────────────
        [SerializeField] public RectTransform _canvasRect;
        [SerializeField] public Transform     _bgLayer;    // behind game UI

        // ── Canvas size helpers ───────────────────────────────────────────────
        private float W => _canvasRect != null ? _canvasRect.rect.width  : 1920f;
        private float H => _canvasRect != null ? _canvasRect.rect.height : 1080f;

        // ── Background particles ──────────────────────────────────────────────
        private RectTransform[] _bgRts;
        private Image[]         _bgImgs;
        private Vector2[]       _bgVel;
        private float[]         _bgPhase;
        private float[]         _bgSinAmp;
        private float[]         _bgBaseX;

        // ── Rune glyphs ───────────────────────────────────────────────────────
        private static readonly string[] RUNE_CHARS = { "ᚠ", "ᚢ", "ᚦ", "ᚨ", "ᚱ", "ᚲ", "ᚷ", "ᚹ" };
        private RectTransform[] _runeRts;
        private Text[]          _runeTexts;
        private float[]         _runeSpinSpeed;
        private float[]         _runeRiseSpeed;
        private float[]         _runeAlphaPhase;

        // ── Fireflies ─────────────────────────────────────────────────────────
        private RectTransform[] _ffRts;
        private Image[]         _ffImgs;
        private Vector2[]       _ffCenter;
        private float[]         _ffPhaseX, _ffPhaseY;
        private float[]         _ffAmpX,   _ffAmpY;
        private float[]         _ffFreqX,  _ffFreqY;

        // ── Bottom mist ───────────────────────────────────────────────────────
        private RectTransform[] _mistRts;
        private float[]         _mistSpeed;

        // ── Connection lines ──────────────────────────────────────────────────
        private RectTransform[] _lineRts;
        private Image[]         _lineImgs;
        private Vector2[]       _bgPositionsSnap; // updated each frame for line calc

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_bgLayer == null) enabled = false;
        }

        private void Start()
        {
            if (_bgLayer == null) return;
            InitBGParticles();
            InitRuneGlyphs();
            InitFireflies();
            InitMist();
            InitConnectionLines();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float t  = Time.time;
            UpdateBGParticles(dt);
            UpdateRuneGlyphs(dt, t);
            UpdateFireflies(t);
            UpdateMist(dt);
            UpdateConnectionLines();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Background particles
        // ═══════════════════════════════════════════════════════════════════════

        private void InitBGParticles()
        {
            _bgRts      = new RectTransform[BG_COUNT];
            _bgImgs     = new Image[BG_COUNT];
            _bgVel      = new Vector2[BG_COUNT];
            _bgPhase    = new float[BG_COUNT];
            _bgSinAmp   = new float[BG_COUNT];
            _bgBaseX    = new float[BG_COUNT];

            // gold / cyan / blue / purple
            Color[] palette = {
                new Color(0.78f, 0.67f, 0.43f, 0.55f),
                new Color(0.04f, 0.78f, 0.73f, 0.50f),
                new Color(0.24f, 0.55f, 1.00f, 0.45f),
                new Color(0.60f, 0.25f, 0.85f, 0.40f),
            };

            for (int i = 0; i < BG_COUNT; i++)
            {
                var go = new GameObject($"BGParticle_{i}");
                go.transform.SetParent(_bgLayer, false);

                var rt = go.AddComponent<RectTransform>();
                float sz = Random.Range(2f, 6f);
                rt.sizeDelta = new Vector2(sz, sz);
                float startX = Random.Range(-W * 0.5f, W * 0.5f);
                float startY = Random.Range(-H * 0.5f, H * 0.5f);
                rt.anchoredPosition = new Vector2(startX, startY);

                var img = go.AddComponent<Image>();
                img.color = palette[i % palette.Length];
                img.raycastTarget = false;

                _bgRts[i]    = rt;
                _bgImgs[i]   = img;
                _bgVel[i]    = new Vector2(0f, Random.Range(8f, 28f));
                _bgPhase[i]  = Random.Range(0f, Mathf.PI * 2f);
                _bgSinAmp[i] = Random.Range(10f, 40f);
                _bgBaseX[i]  = startX;
            }
        }

        private void UpdateBGParticles(float dt)
        {
            for (int i = 0; i < BG_COUNT; i++)
            {
                var pos = _bgRts[i].anchoredPosition;
                pos.y += _bgVel[i].y * dt;

                _bgPhase[i] += dt * 0.8f;
                pos.x = _bgBaseX[i] + Mathf.Sin(_bgPhase[i]) * _bgSinAmp[i];

                // Wrap: respawn at bottom when particle exits top
                if (pos.y > H * 0.5f + 20f)
                {
                    pos.y        = -H * 0.5f - 20f;
                    float newX   = Random.Range(-W * 0.5f, W * 0.5f);
                    pos.x        = newX;
                    _bgBaseX[i]  = newX;
                    _bgPhase[i]  = Random.Range(0f, Mathf.PI * 2f);
                }

                _bgRts[i].anchoredPosition = pos;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Rune glyphs
        // ═══════════════════════════════════════════════════════════════════════

        private void InitRuneGlyphs()
        {
            _runeRts        = new RectTransform[RUNE_COUNT];
            _runeTexts      = new Text[RUNE_COUNT];
            _runeSpinSpeed  = new float[RUNE_COUNT];
            _runeRiseSpeed  = new float[RUNE_COUNT];
            _runeAlphaPhase = new float[RUNE_COUNT];

            Color[] runeColors = {
                new Color(0.78f, 0.67f, 0.43f, 0.3f),
                new Color(0.04f, 0.78f, 0.73f, 0.25f),
            };

            for (int i = 0; i < RUNE_COUNT; i++)
            {
                var go = new GameObject($"RuneGlyph_{i}");
                go.transform.SetParent(_bgLayer, false);

                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(40f, 40f);
                rt.anchoredPosition = new Vector2(
                    Random.Range(-W * 0.5f, W * 0.5f),
                    Random.Range(-H * 0.5f, H * 0.5f)
                );

                var txt = go.AddComponent<Text>();
                txt.text      = RUNE_CHARS[i % RUNE_CHARS.Length];
                txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize  = Random.Range(18, 32);
                txt.color     = runeColors[i % runeColors.Length];
                txt.alignment = TextAnchor.MiddleCenter;
                txt.raycastTarget = false;

                _runeRts[i]        = rt;
                _runeTexts[i]      = txt;
                _runeSpinSpeed[i]  = Random.Range(-35f, 35f);
                _runeRiseSpeed[i]  = Random.Range(6f, 15f);
                _runeAlphaPhase[i] = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        private void UpdateRuneGlyphs(float dt, float t)
        {
            for (int i = 0; i < RUNE_COUNT; i++)
            {
                var pos = _runeRts[i].anchoredPosition;
                pos.y += _runeRiseSpeed[i] * dt;

                if (pos.y > H * 0.5f + 40f)
                {
                    pos.y = -H * 0.5f - 40f;
                    pos.x = Random.Range(-W * 0.5f, W * 0.5f);
                }
                _runeRts[i].anchoredPosition = pos;

                // Spin
                var angles = _runeRts[i].eulerAngles;
                angles.z += _runeSpinSpeed[i] * dt;
                _runeRts[i].eulerAngles = angles;

                // Alpha pulse
                _runeAlphaPhase[i] += dt * 0.5f;
                var col = _runeTexts[i].color;
                col.a = (Mathf.Sin(_runeAlphaPhase[i]) * 0.5f + 0.5f) * 0.35f;
                _runeTexts[i].color = col;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Fireflies
        // ═══════════════════════════════════════════════════════════════════════

        private void InitFireflies()
        {
            _ffRts    = new RectTransform[FIREFLY_COUNT];
            _ffImgs   = new Image[FIREFLY_COUNT];
            _ffCenter = new Vector2[FIREFLY_COUNT];
            _ffPhaseX = new float[FIREFLY_COUNT];
            _ffPhaseY = new float[FIREFLY_COUNT];
            _ffAmpX   = new float[FIREFLY_COUNT];
            _ffAmpY   = new float[FIREFLY_COUNT];
            _ffFreqX  = new float[FIREFLY_COUNT];
            _ffFreqY  = new float[FIREFLY_COUNT];

            for (int i = 0; i < FIREFLY_COUNT; i++)
            {
                var go = new GameObject($"Firefly_{i}");
                go.transform.SetParent(_bgLayer, false);

                var rt = go.AddComponent<RectTransform>();
                float sz = Random.Range(4f, 9f);
                rt.sizeDelta = new Vector2(sz, sz);

                var center = new Vector2(
                    Random.Range(-W * 0.5f, W * 0.5f),
                    Random.Range(-H * 0.5f, H * 0.5f)
                );
                rt.anchoredPosition = center;

                var img = go.AddComponent<Image>();
                img.color = new Color(0.88f, 1f, 0.55f, 0.70f);
                img.raycastTarget = false;

                _ffRts[i]    = rt;
                _ffImgs[i]   = img;
                _ffCenter[i] = center;
                _ffPhaseX[i] = Random.Range(0f, Mathf.PI * 2f);
                _ffPhaseY[i] = Random.Range(0f, Mathf.PI * 2f);
                _ffAmpX[i]   = Random.Range(30f, 80f);
                _ffAmpY[i]   = Random.Range(20f, 60f);
                _ffFreqX[i]  = Random.Range(0.3f, 0.8f);
                _ffFreqY[i]  = Random.Range(0.4f, 0.9f);
            }
        }

        private void UpdateFireflies(float t)
        {
            for (int i = 0; i < FIREFLY_COUNT; i++)
            {
                float x = _ffCenter[i].x + Mathf.Sin(t * _ffFreqX[i] + _ffPhaseX[i]) * _ffAmpX[i];
                float y = _ffCenter[i].y + Mathf.Sin(t * _ffFreqY[i] + _ffPhaseY[i]) * _ffAmpY[i];
                _ffRts[i].anchoredPosition = new Vector2(x, y);

                float alpha = (Mathf.Sin(t * 1.5f + _ffPhaseX[i]) * 0.5f + 0.5f) * 0.65f + 0.1f;
                var col = _ffImgs[i].color;
                col.a = alpha;
                _ffImgs[i].color = col;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Bottom mist
        // ═══════════════════════════════════════════════════════════════════════

        private void InitMist()
        {
            _mistRts   = new RectTransform[MIST_COUNT];
            _mistSpeed = new float[MIST_COUNT];

            float[] yOffsets = { 30f, 60f, 90f, 120f };
            float[] alphas   = { 0.25f, 0.20f, 0.14f, 0.09f };
            Color cyanBase   = new Color(0.04f, 0.78f, 0.73f, 1f);

            for (int i = 0; i < MIST_COUNT; i++)
            {
                var go = new GameObject($"Mist_{i}");
                go.transform.SetParent(_bgLayer, false);

                var rt = go.AddComponent<RectTransform>();
                // 2.5× canvas width so seamless horizontal scroll
                rt.sizeDelta = new Vector2(W * 2.5f, 50f + i * 18f);
                rt.anchoredPosition = new Vector2(Random.Range(-W, 0f), -H * 0.5f + yOffsets[i]);

                var img = go.AddComponent<Image>();
                Color c = cyanBase;
                c.a = alphas[i];
                img.color = c;
                img.raycastTarget = false;

                _mistRts[i]   = rt;
                _mistSpeed[i] = Random.Range(8f, 22f) * (i % 2 == 0 ? 1f : -1f);
            }
        }

        private void UpdateMist(float dt)
        {
            for (int i = 0; i < MIST_COUNT; i++)
            {
                var pos = _mistRts[i].anchoredPosition;
                pos.x += _mistSpeed[i] * dt;

                float half = W * 1.25f;
                if (_mistSpeed[i] > 0 && pos.x >  half) pos.x -= W * 2.5f;
                if (_mistSpeed[i] < 0 && pos.x < -half) pos.x += W * 2.5f;

                _mistRts[i].anchoredPosition = pos;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Connection lines
        // ═══════════════════════════════════════════════════════════════════════

        private void InitConnectionLines()
        {
            _lineRts           = new RectTransform[LINE_POOL];
            _lineImgs          = new Image[LINE_POOL];
            _bgPositionsSnap   = new Vector2[BG_COUNT];

            for (int i = 0; i < LINE_POOL; i++)
            {
                var go = new GameObject($"Line_{i}");
                go.transform.SetParent(_bgLayer, false);

                var rt = go.AddComponent<RectTransform>();
                rt.pivot    = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(0f, 1.5f);

                var img = go.AddComponent<Image>();
                img.color = new Color(0.04f, 0.78f, 0.73f, 0f);
                img.raycastTarget = false;

                _lineRts[i]  = rt;
                _lineImgs[i] = img;
                go.SetActive(false);
            }
        }

        private void UpdateConnectionLines()
        {
            // Snapshot BG positions
            for (int i = 0; i < BG_COUNT; i++)
                _bgPositionsSnap[i] = _bgRts[i].anchoredPosition;

            int lineIdx = 0;
            for (int a = 0; a < BG_COUNT && lineIdx < LINE_POOL; a++)
            {
                for (int b = a + 1; b < BG_COUNT && lineIdx < LINE_POOL; b++)
                {
                    float dist = Vector2.Distance(_bgPositionsSnap[a], _bgPositionsSnap[b]);
                    if (dist < LINE_RADIUS)
                    {
                        float alpha = (1f - dist / LINE_RADIUS) * 0.22f;
                        DrawLine(lineIdx, _bgPositionsSnap[a], _bgPositionsSnap[b], alpha);
                        lineIdx++;
                    }
                }
            }

            // Deactivate unused pool entries
            for (int i = lineIdx; i < LINE_POOL; i++)
            {
                if (_lineRts[i].gameObject.activeSelf)
                    _lineRts[i].gameObject.SetActive(false);
            }
        }

        private void DrawLine(int idx, Vector2 a, Vector2 b, float alpha)
        {
            var go = _lineRts[idx].gameObject;
            if (!go.activeSelf) go.SetActive(true);

            Vector2 dir   = b - a;
            float   dist  = dir.magnitude;
            float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            _lineRts[idx].anchoredPosition = a;
            _lineRts[idx].sizeDelta        = new Vector2(dist, 1.5f);
            _lineRts[idx].localRotation    = Quaternion.Euler(0f, 0f, angle);

            var c = _lineImgs[idx].color;
            c.a = alpha;
            _lineImgs[idx].color = c;
        }
    }
}
