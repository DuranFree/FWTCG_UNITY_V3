using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
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

        private List<UnitInstance> _mulliganHand;
        private List<UnitInstance> _selectedForSwap = new List<UnitInstance>();
        private List<CardView> _mulliganCardViews   = new List<CardView>();
        private Coroutine _scanLightRoutine;

        // Active TCS references — resolved in OnDestroy to prevent TCS leaks
        private TaskCompletionSource<bool> _activeCoinTcs;
        private TaskCompletionSource<bool> _activeMulliganTcs;

        private void Awake()
        {
            HidePanel(_coinFlipPanel, ref _coinFlipCG);
            HidePanel(_mulliganPanel, ref _mulliganCG);
        }

        private void OnDestroy()
        {
            // Stop background loops
            if (_scanLightRoutine != null) StopCoroutine(_scanLightRoutine);
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
                t += Time.deltaTime / duration;
                cg.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        private IEnumerator FadeOut(CanvasGroup cg, float duration)
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.deltaTime / duration;
                cg.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            cg.alpha = 0f;
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

            // ── Show panel ────────────────────────────────────────────────────
            if (_coinFlipPanel != null) _coinFlipPanel.SetActive(true);
            _coinFlipCG = EnsureCG(_coinFlipPanel, _coinFlipCG);
            yield return FadeIn(_coinFlipCG, PANEL_FADE_IN);

            // ── Start scan light background loop ──────────────────────────────
            if (_scanLightImage != null)
                _scanLightRoutine = StartCoroutine(ScanLightLoop());

            // ── Coin flip animation ───────────────────────────────────────────
            yield return CoinSpinRoutine(isPlayerFirst);

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
                    _coinCircleImage.color = Gold();
                    if (_coinFlipText != null)
                        _coinFlipText.text = isPlayerFirst ? "先" : "后";
                }
                else
                {
                    _coinCircleImage.color = (i % 2 == 0) ? Bronze() : Gold();
                    if (_coinFlipText != null) _coinFlipText.text = "?";
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
