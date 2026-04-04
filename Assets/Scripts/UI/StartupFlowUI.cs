using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Audio;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// Handles the pre-game startup overlay sequence:
    ///   1. Coin flip display with 1800ms flip animation (DEV-24)
    ///   2. Mulligan (player selects up to 2 cards to swap)
    ///
    /// Call RunStartupFlow() from GameManager before starting the game loop.
    /// </summary>
    public class StartupFlowUI : MonoBehaviour
    {
        // ── Coin flip panel ───────────────────────────────────────────────────
        [SerializeField] private GameObject _coinFlipPanel;
        [SerializeField] private Text _coinFlipText;          // coin face label: 先 / 后 / ?
        [SerializeField] private Button _coinFlipOkButton;
        [SerializeField] private CanvasGroup _coinFlipCG;     // DEV-24: panel fade
        [SerializeField] private Image _coinCircleImage;      // DEV-24: spinning coin disc
        [SerializeField] private Text _coinResultText;        // DEV-24: result + battlefield (fades in)
        [SerializeField] private Image _scanLightImage;       // DEV-24: horizontal scan sweep

        // ── VFX-6: coin flip audio hooks ──────────────────────────────────
        [SerializeField] private AudioClip _coinFlipStartClip;   // played when flip begins
        [SerializeField] private AudioClip _coinFlipLandClip;    // played when coin lands

        // ── Mulligan panel ────────────────────────────────────────────────────
        [SerializeField] private GameObject _mulliganPanel;
        [SerializeField] private Text _mulliganTitleText;
        [SerializeField] private Transform _mulliganCardContainer;
        [SerializeField] private GameObject _cardViewPrefab;
        [SerializeField] private Button _mulliganConfirmButton;
        [SerializeField] private Text _mulliganConfirmLabel;
        [SerializeField] private CanvasGroup _mulliganCG;     // DEV-24: panel fade

        private const int MAX_MULLIGAN_SWAPS = 2;

        // DEV-24: animation timing constants (seconds)
        public const float PANEL_FADE_IN  = 0.4f;
        public const float PANEL_FADE_OUT = 0.3f;
        public const float COIN_HALF_FLIP = 0.13f;  // each half-flip duration (fast part)
        public const int   COIN_FLIP_COUNT = 5;     // total full flips before landing
        public const float COIN_LAND_DUR  = 0.3f;   // landing bounce duration
        public const float RESULT_FADE_IN = 0.4f;   // result text fade duration
        public const float SCAN_PERIOD    = 8f;     // scan light full sweep period

        // VFX-6: coin land burst constants
        public const int   COIN_BURST_COUNT    = 20;    // particle count
        public const float COIN_BURST_DURATION = 0.6f;  // burst animation duration
        public const float COIN_BURST_RADIUS   = 130f;  // max radial distance
        public const float COIN_BURST_SIZE     = 8f;    // starting particle size

        private List<UnitInstance> _mulliganHand;
        private List<UnitInstance> _selectedForSwap = new List<UnitInstance>();
        private List<CardView> _mulliganCardViews   = new List<CardView>();
        private Coroutine _scanLightRoutine;

        // DEV-30 V1-V5: title overlay elements (found by name in Awake)
        private Image _hexBreathOverlay;
        private Image _titleBeam;
        private Image _bgGradientOverlay;
        private Coroutine _hexBreathRoutine;
        private Coroutine _titleBeamRoutine;
        private Coroutine _bgGradientRoutine;

        // Active TCS references — resolved in OnDestroy to prevent TCS leaks
        private TaskCompletionSource<bool> _activeCoinTcs;
        private TaskCompletionSource<bool> _activeMulliganTcs;

        // VFX-6: burst particle tracking for interrupt cleanup
        private readonly List<GameObject> _burstParticles = new List<GameObject>();

        private void Awake()
        {
            HidePanel(_coinFlipPanel, ref _coinFlipCG);
            HidePanel(_mulliganPanel, ref _mulliganCG);

            // DEV-30 V2/V3/V5: find overlay elements by name (created by SceneBuilder)
            if (_coinFlipPanel != null)
            {
                var hex = _coinFlipPanel.transform.Find("HexBreathOverlay");
                if (hex) _hexBreathOverlay = hex.GetComponent<Image>();
                var beam = _coinFlipPanel.transform.Find("TitleBeam");
                if (beam) _titleBeam = beam.GetComponent<Image>();
                var bgGrad = _coinFlipPanel.transform.Find("BgGradientOverlay");
                if (bgGrad) _bgGradientOverlay = bgGrad.GetComponent<Image>();
            }
        }

        // DEV-26: stop scan loop when component is disabled (not just destroyed)
        private void OnDisable()
        {
            if (_scanLightRoutine != null) { StopCoroutine(_scanLightRoutine); _scanLightRoutine = null; }
            // DEV-30: stop title animation loops
            if (_hexBreathRoutine  != null) { StopCoroutine(_hexBreathRoutine);  _hexBreathRoutine  = null; }
            if (_titleBeamRoutine  != null) { StopCoroutine(_titleBeamRoutine);  _titleBeamRoutine  = null; }
            if (_bgGradientRoutine != null) { StopCoroutine(_bgGradientRoutine); _bgGradientRoutine = null; }
        }

        private void OnDestroy()
        {
            // Stop background loops
            if (_scanLightRoutine != null) StopCoroutine(_scanLightRoutine);
            // VFX-6: destroy any lingering burst particles
            foreach (var p in _burstParticles)
                if (p != null) Destroy(p);
            _burstParticles.Clear();
            // Resolve any pending TCS so callers don't hang if this component is destroyed mid-flow
            _activeCoinTcs?.TrySetResult(true);
            _activeMulliganTcs?.TrySetResult(true);
        }

        // ── Public entry point ────────────────────────────────────────────────

        /// <summary>Runs the full startup sequence. Await this before starting the game loop.</summary>
        public async Task RunStartupFlow(GameState gs)
        {
            await ShowCoinFlip(gs);
            await ShowMulligan(gs);
        }

        // ── Panel helpers ─────────────────────────────────────────────────────

        private void HidePanel(GameObject panel, ref CanvasGroup cg)
        {
            if (panel == null) return;
            cg = EnsureCG(panel, cg);
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            panel.SetActive(false);
        }

        private static CanvasGroup EnsureCG(GameObject go, CanvasGroup existing)
        {
            if (existing != null) return existing;
            var cg = go.GetComponent<CanvasGroup>();
            return cg != null ? cg : go.AddComponent<CanvasGroup>();
        }

        private IEnumerator FadeIn(CanvasGroup cg, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                if (cg == null) yield break; // DEV-26: null guard
                t += Time.deltaTime / duration;
                cg.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            if (cg == null) yield break;
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        private IEnumerator FadeOut(CanvasGroup cg, float duration)
        {
            if (cg == null) yield break; // DEV-26: null guard
            cg.interactable = false;
            cg.blocksRaycasts = false;
            float t = 1f;
            while (t > 0f)
            {
                if (cg == null) yield break;
                t -= Time.deltaTime / duration;
                cg.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            if (cg != null) cg.alpha = 0f;
        }

        private IEnumerator FadeOutAndResolve(GameObject panel, CanvasGroup cg,
                                               TaskCompletionSource<bool> tcs)
        {
            if (cg != null) yield return FadeOut(cg, PANEL_FADE_OUT);
            if (panel != null) panel.SetActive(false);
            tcs.TrySetResult(true);
        }

        // ── Coin flip ─────────────────────────────────────────────────────────

        private Task ShowCoinFlip(GameState gs)
        {
            _activeCoinTcs = new TaskCompletionSource<bool>();
            StartCoroutine(CoinFlipFlowRoutine(gs, _activeCoinTcs));
            return _activeCoinTcs.Task;
        }

        private IEnumerator CoinFlipFlowRoutine(GameState gs, TaskCompletionSource<bool> tcs)
        {
            bool isPlayerFirst = gs.First == GameRules.OWNER_PLAYER;
            string bf0 = (gs.BFNames != null && gs.BFNames.Length > 0)
                ? GameRules.GetBattlefieldDisplayName(gs.BFNames[0]) : "战场1";
            string bf1 = (gs.BFNames != null && gs.BFNames.Length > 1)
                ? GameRules.GetBattlefieldDisplayName(gs.BFNames[1]) : "战场2";

            // ── Initial state ─────────────────────────────────────────────────
            if (_coinFlipText != null)    _coinFlipText.text = "?";
            if (_coinResultText != null)
            {
                _coinResultText.text = "";
                SetTextAlpha(_coinResultText, 0f);
            }
            if (_coinFlipOkButton != null)
                _coinFlipOkButton.interactable = false;
            // Hide coin image: sprite isn't loaded yet — prevents white+? flash during fade-in
            if (_coinCircleImage != null) _coinCircleImage.color = Color.clear;

            // ── Show panel ────────────────────────────────────────────────────
            if (_coinFlipPanel != null) _coinFlipPanel.SetActive(true);
            _coinFlipCG = EnsureCG(_coinFlipPanel, _coinFlipCG);
            yield return FadeIn(_coinFlipCG, PANEL_FADE_IN);

            // ── Start scan light background loop ──────────────────────────────
            if (_scanLightImage != null)
                _scanLightRoutine = StartCoroutine(ScanLightLoop());

            // ── DEV-30 V1-V5: start title animation loops ──────────────────────
            var coinTitle = _coinFlipPanel?.transform.Find("CoinTitle")?.GetComponent<Text>();
            if (coinTitle != null) StartCoroutine(TitleTextEntranceRoutine(coinTitle));
            if (_hexBreathOverlay  != null) _hexBreathRoutine  = StartCoroutine(HexBreathLoop());
            if (_titleBeam         != null) _titleBeamRoutine  = StartCoroutine(TitleBeamPulseLoop());
            if (_bgGradientOverlay != null) _bgGradientRoutine = StartCoroutine(BgGradientRotateLoop());

            // ── Coin flip animation ───────────────────────────────────────────
            yield return CoinSpinRoutine(isPlayerFirst);

            // ── VFX-6: gold burst + land audio ───────────────────────────────
            if (AudioTool.Instance != null && _coinFlipLandClip != null)
                AudioTool.Instance.PlayOneShot(AudioTool.CH_UI, _coinFlipLandClip);
            if (_coinCircleImage != null)
                StartCoroutine(CoinBurstParticles(_coinCircleImage.rectTransform));

            // ── Fade in result text ───────────────────────────────────────────
            string result = $"{(isPlayerFirst ? "玩家先手！" : "AI先手！")}\n\n战场：{bf0}  /  {bf1}";
            if (_coinResultText != null)
            {
                _coinResultText.text = result;
                yield return FadeTextIn(_coinResultText, RESULT_FADE_IN);
            }
            else if (_coinFlipText != null)
            {
                // Fallback: no separate result text — update the face label
                _coinFlipText.text = result;
            }

            // ── Enable button (disable on first click to prevent double-resolve) ──
            if (_coinFlipOkButton != null)
            {
                _coinFlipOkButton.onClick.RemoveAllListeners();
                _coinFlipOkButton.onClick.AddListener(() =>
                {
                    _coinFlipOkButton.interactable = false; // prevent double-click
                    _coinFlipOkButton.onClick.RemoveAllListeners();
                    if (_scanLightRoutine != null) StopCoroutine(_scanLightRoutine);
                    // DEV-30: stop title animation loops
                    if (_hexBreathRoutine  != null) { StopCoroutine(_hexBreathRoutine);  _hexBreathRoutine  = null; }
                    if (_titleBeamRoutine  != null) { StopCoroutine(_titleBeamRoutine);  _titleBeamRoutine  = null; }
                    if (_bgGradientRoutine != null) { StopCoroutine(_bgGradientRoutine); _bgGradientRoutine = null; }
                    StartCoroutine(FadeOutAndResolve(_coinFlipPanel, _coinFlipCG, tcs));
                });
                yield return new WaitForSeconds(0.5f);
                if (_coinFlipOkButton != null)
                    _coinFlipOkButton.interactable = true;
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
                if (_scanLightRoutine != null) StopCoroutine(_scanLightRoutine);
                yield return FadeOutAndResolve(_coinFlipPanel, _coinFlipCG, tcs);
            }
        }

        /// <summary>
        /// Animates the coin: COIN_FLIP_COUNT quick flips (scaleX flip), then lands with bounce.
        /// Total duration ≈ COIN_FLIP_COUNT × 2 × COIN_HALF_FLIP + COIN_LAND_DUR ≈ 1.8s.
        /// </summary>
        private IEnumerator CoinSpinRoutine(bool isPlayerFirst)
        {
            if (_coinCircleImage == null) { yield return new WaitForSeconds(0.6f); yield break; }

            // VFX-6: start audio
            if (AudioTool.Instance != null && _coinFlipStartClip != null)
                AudioTool.Instance.PlayOneShot(AudioTool.CH_UI, _coinFlipStartClip);

            // Load coin face sprites; fall back to color-only if not found
            var spriteFirst  = Resources.Load<Sprite>("CardArt/xianshou"); // 先手面
            var spriteSecond = Resources.Load<Sprite>("CardArt/houshou");  // 后手面
            bool hasSprites  = spriteFirst != null && spriteSecond != null;

            if (hasSprites)
            {
                _coinCircleImage.sprite         = spriteFirst;
                _coinCircleImage.color          = Color.white;
                _coinCircleImage.preserveAspect = true;
                if (_coinFlipText != null) _coinFlipText.text = ""; // sprite already conveys face
            }
            else
            {
                // No sprites: show gold circle background with "?" text
                _coinCircleImage.sprite = null;
                _coinCircleImage.color  = new Color(0.78f, 0.67f, 0.43f, 1f); // Gold
            }

            static Color Gold()   => new Color(0.78f, 0.67f, 0.43f, 1f);
            static Color Bronze() => new Color(0.55f, 0.40f, 0.20f, 1f);

            for (int i = 0; i < COIN_FLIP_COUNT; i++)
            {
                bool isLast = (i == COIN_FLIP_COUNT - 1);
                float halfDur = isLast ? COIN_HALF_FLIP * 2f : COIN_HALF_FLIP;

                // Fold to edge
                yield return ScaleCoinX(1f, 0f, halfDur);
                if (_coinCircleImage == null) yield break; // null-check after yield

                // Midpoint: swap face
                if (isLast)
                {
                    if (hasSprites)
                        _coinCircleImage.sprite = isPlayerFirst ? spriteFirst : spriteSecond;
                    else
                        _coinCircleImage.color = Gold();
                    if (_coinFlipText != null)
                        _coinFlipText.text = hasSprites ? "" : (isPlayerFirst ? "先" : "后");
                }
                else
                {
                    if (hasSprites)
                        _coinCircleImage.sprite = (i % 2 == 0) ? spriteSecond : spriteFirst;
                    else
                        _coinCircleImage.color = (i % 2 == 0) ? Bronze() : Gold();
                    if (_coinFlipText != null && !hasSprites) _coinFlipText.text = "?";
                }

                // Unfold
                yield return ScaleCoinX(0f, 1f, halfDur);
                if (_coinCircleImage == null) yield break; // null-check after yield

                if (!isLast) yield return new WaitForSeconds(0.04f);
            }

            if (_coinCircleImage == null) yield break;

            // Landing bounce
            float elapsed = 0f;
            while (elapsed < COIN_LAND_DUR)
            {
                elapsed += Time.deltaTime;
                if (_coinCircleImage == null) yield break;
                float s = 1f + Mathf.Sin(elapsed / COIN_LAND_DUR * Mathf.PI) * 0.15f;
                _coinCircleImage.rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (_coinCircleImage != null)
                _coinCircleImage.rectTransform.localScale = Vector3.one;
        }

        private IEnumerator ScaleCoinX(float from, float to, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                if (_coinCircleImage == null) yield break;
                float sx = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                _coinCircleImage.rectTransform.localScale = new Vector3(sx, 1f, 1f);
                yield return null;
            }
            if (_coinCircleImage != null)
                _coinCircleImage.rectTransform.localScale = new Vector3(to, 1f, 1f);
        }

        /// <summary>
        /// VFX-6: Gold particle burst from coin center on landing.
        /// 20 particles radiate outward with ease-out-quad, fading and shrinking.
        /// </summary>
        private IEnumerator CoinBurstParticles(RectTransform origin)
        {
            if (origin == null || _coinFlipPanel == null) yield break;

            var parentRT = _coinFlipPanel.GetComponent<RectTransform>();
            if (parentRT == null) yield break;

            // Convert coin world position to panel-local coordinates
            // (anchoredPosition math breaks under VerticalLayoutGroup)
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, origin.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, screenPt, null, out Vector2 center);

            var rts    = new RectTransform[COIN_BURST_COUNT];
            var imgs   = new Image[COIN_BURST_COUNT];
            var angles = new float[COIN_BURST_COUNT];

            for (int i = 0; i < COIN_BURST_COUNT; i++)
            {
                var go = new GameObject($"CoinBurstP{i}", typeof(RectTransform), typeof(Image));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(parentRT, false);
                rt.sizeDelta        = new Vector2(COIN_BURST_SIZE, COIN_BURST_SIZE);
                rt.anchoredPosition = center;

                // Exclude from VerticalLayoutGroup
                var le = go.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                var img  = go.GetComponent<Image>();
                img.color = GameColors.Gold;
                img.raycastTarget = false;

                rts[i]  = rt;
                imgs[i] = img;
                angles[i] = i / (float)COIN_BURST_COUNT * Mathf.PI * 2f;

                _burstParticles.Add(go); // track for interrupt cleanup
            }

            float elapsed = 0f;
            while (elapsed < COIN_BURST_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / COIN_BURST_DURATION);
                float easeOut = 1f - (1f - t) * (1f - t); // ease-out-quad

                float radius = easeOut * COIN_BURST_RADIUS;
                float size   = Mathf.Lerp(COIN_BURST_SIZE, 2f, t);
                float alpha  = 1f - t;

                for (int i = 0; i < COIN_BURST_COUNT; i++)
                {
                    if (rts[i] == null) continue;
                    float x = center.x + Mathf.Cos(angles[i]) * radius;
                    float y = center.y + Mathf.Sin(angles[i]) * radius;
                    rts[i].anchoredPosition = new Vector2(x, y);
                    rts[i].sizeDelta        = new Vector2(size, size);

                    if (imgs[i] != null)
                    {
                        var c = imgs[i].color;
                        c.a = alpha;
                        imgs[i].color = c;
                    }
                }
                yield return null;
            }

            // Cleanup
            for (int i = 0; i < COIN_BURST_COUNT; i++)
            {
                if (rts[i] != null)
                {
                    _burstParticles.Remove(rts[i].gameObject);
                    Destroy(rts[i].gameObject);
                }
            }
        }

        private IEnumerator FadeTextIn(Text text, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                SetTextAlpha(text, Mathf.Clamp01(t));
                yield return null;
            }
            SetTextAlpha(text, 1f);
        }

        private static void SetTextAlpha(Text text, float a)
        {
            Color c = text.color;
            c.a = a;
            text.color = c;
        }

        /// <summary>Scans a thin light bar from left to right, repeating every SCAN_PERIOD seconds.</summary>
        private IEnumerator ScanLightLoop()
        {
            if (_scanLightImage == null) yield break;
            var rt = _scanLightImage.rectTransform;
            const float HALF_W = 960f;
            while (true)
            {
                float elapsed = 0f;
                while (elapsed < SCAN_PERIOD)
                {
                    elapsed += Time.deltaTime;
                    float x = Mathf.Lerp(-HALF_W, HALF_W, elapsed / SCAN_PERIOD);
                    rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
                    yield return null;
                }
            }
        }

        // ── DEV-30 V1-V5 title animation routines ────────────────────────────

        /// <summary>V1: Coin flip title text entrance — fadeInUp + scale + alpha.</summary>
        private IEnumerator TitleTextEntranceRoutine(Text titleText)
        {
            if (titleText == null) yield break;
            const float dur = 0.45f;
            var rt = titleText.rectTransform;
            Vector2 endPos   = rt.anchoredPosition;
            Vector2 startPos = endPos + new Vector2(0f, -20f);

            rt.anchoredPosition = startPos;
            titleText.transform.localScale = Vector3.one * 0.85f;
            var c = titleText.color; c.a = 0f; titleText.color = c;

            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t    = Mathf.Clamp01(elapsed / dur);
                float ease = t * (2f - t); // EaseOutQuad
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
                titleText.transform.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, ease);
                var col = titleText.color; col.a = ease; titleText.color = col;
                yield return null;
            }
            rt.anchoredPosition = endPos;
            titleText.transform.localScale = Vector3.one;
            var finalC = titleText.color; finalC.a = 1f; titleText.color = finalC;
        }

        /// <summary>V2: Hexagonal overlay alpha breath (3s period, 0.15↔0.35).</summary>
        private IEnumerator HexBreathLoop()
        {
            if (_hexBreathOverlay == null) yield break;
            const float period   = 3f;
            const float minAlpha = 0.15f;
            const float maxAlpha = 0.35f;
            while (true)
            {
                float t = (Mathf.Sin(Time.time * Mathf.PI * 2f / period) + 1f) * 0.5f;
                var col = _hexBreathOverlay.color;
                col.a = Mathf.Lerp(minAlpha, maxAlpha, t);
                _hexBreathOverlay.color = col;
                yield return null;
            }
        }

        /// <summary>V3: Center beam pulse (2s period, alpha 0→0.45→0).</summary>
        private IEnumerator TitleBeamPulseLoop()
        {
            if (_titleBeam == null) yield break;
            const float period   = 2f;
            const float maxAlpha = 0.45f;
            while (true)
            {
                float t = (Mathf.Sin(Time.time * Mathf.PI * 2f / period) + 1f) * 0.5f;
                var col = _titleBeam.color;
                col.a = t * maxAlpha;
                _titleBeam.color = col;
                yield return null;
            }
        }

        /// <summary>V5: Background gradient overlay slow rotation (5°/s CW).</summary>
        private IEnumerator BgGradientRotateLoop()
        {
            if (_bgGradientOverlay == null) yield break;
            while (true)
            {
                _bgGradientOverlay.transform.Rotate(0f, 0f, -5f * Time.deltaTime);
                yield return null;
            }
        }

        // ── Mulligan ──────────────────────────────────────────────────────────

        private Task ShowMulligan(GameState gs)
        {
            _activeMulliganTcs = new TaskCompletionSource<bool>();
            StartCoroutine(MulliganFlowRoutine(gs, _activeMulliganTcs));
            return _activeMulliganTcs.Task;
        }

        private IEnumerator MulliganFlowRoutine(GameState gs, TaskCompletionSource<bool> tcs)
        {
            _mulliganHand = new List<UnitInstance>(gs.PHand);
            _selectedForSwap.Clear();
            _mulliganCardViews.Clear();

            if (_mulliganCardContainer != null)
                foreach (Transform child in _mulliganCardContainer)
                    Destroy(child.gameObject);

            if (_cardViewPrefab != null && _mulliganCardContainer != null)
            {
                foreach (UnitInstance unit in _mulliganHand)
                {
                    UnitInstance captured = unit;
                    GameObject cardGO = Instantiate(_cardViewPrefab, _mulliganCardContainer);
                    CardView cv = cardGO.GetComponent<CardView>();
                    if (cv != null)
                    {
                        cv.Setup(captured, true, OnMulliganCardClicked);
                        _mulliganCardViews.Add(cv);
                    }
                }
            }

            UpdateMulliganUI();

            if (_mulliganPanel != null) _mulliganPanel.SetActive(true);
            _mulliganCG = EnsureCG(_mulliganPanel, _mulliganCG);
            yield return FadeIn(_mulliganCG, PANEL_FADE_IN);

            if (_mulliganConfirmButton != null)
            {
                _mulliganConfirmButton.onClick.RemoveAllListeners();
                _mulliganConfirmButton.onClick.AddListener(() =>
                {
                    _mulliganConfirmButton.interactable = false; // prevent double-click
                    _mulliganConfirmButton.onClick.RemoveAllListeners();
                    PerformMulligan(gs);
                    StartCoroutine(FadeOutAndResolve(_mulliganPanel, _mulliganCG, tcs));
                });
            }
            else
            {
                tcs.TrySetResult(true);
            }
        }

        private void OnMulliganCardClicked(UnitInstance unit)
        {
            if (_selectedForSwap.Contains(unit))
                _selectedForSwap.Remove(unit);
            else if (_selectedForSwap.Count < MAX_MULLIGAN_SWAPS)
                _selectedForSwap.Add(unit);
            UpdateMulliganUI();
        }

        private void UpdateMulliganUI()
        {
            if (_mulliganTitleText != null)
                _mulliganTitleText.text =
                    $"梦想手牌调度（最多 {MAX_MULLIGAN_SWAPS} 张，已选 {_selectedForSwap.Count}）\n点击要换掉的牌，再点取消";

            if (_mulliganConfirmLabel != null)
                _mulliganConfirmLabel.text = _selectedForSwap.Count > 0
                    ? $"确认换 {_selectedForSwap.Count} 张"
                    : "不换牌，开始";

            for (int i = 0; i < _mulliganCardViews.Count && i < _mulliganHand.Count; i++)
            {
                bool selected = _selectedForSwap.Contains(_mulliganHand[i]);
                _mulliganCardViews[i].SetSelected(selected);
            }
        }

        private void PerformMulligan(GameState gs)
        {
            if (_selectedForSwap.Count == 0) return;

            List<UnitInstance> deck = gs.PDeck;
            List<UnitInstance> hand = gs.PHand;

            // Rule 117.3 + 594.1.a: return selected cards to BOTTOM of deck
            foreach (UnitInstance u in _selectedForSwap)
            {
                hand.Remove(u);
                deck.Add(u);
            }

            int count = _selectedForSwap.Count;
            for (int i = 0; i < count; i++)
            {
                if (deck.Count == 0) break;
                hand.Add(deck[0]);
                deck.RemoveAt(0);
            }

            Debug.Log($"[Mulligan] 换了 {count} 张牌，新手牌数: {hand.Count}");
        }
    }
}
