using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Audio;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// DOT-5: Pre-game startup overlay — coin flip + mulligan.
    /// Coin flip animation uses DOTween Sequences; panel fades use DOFade.
    /// Flow control coroutines (CoinFlipFlowRoutine, MulliganFlowRoutine) are preserved.
    /// </summary>
    public class StartupFlowUI : MonoBehaviour
    {
        public static StartupFlowUI Instance { get; private set; }

        // ── Coin flip panel ───────────────────────────────────────────────────
        [SerializeField] private GameObject _coinFlipPanel;
        [SerializeField] private Text _coinFlipText;
        [SerializeField] private Button _coinFlipOkButton;
        [SerializeField] private CanvasGroup _coinFlipCG;
        [SerializeField] private Image _coinCircleImage;
        [SerializeField] private Text _coinResultText;
        [SerializeField] private Image _scanLightImage;

        // 3D coin rim: found by name in Awake, animated during scaleX flip
        private Image _coinEdgeImage;

        // ── VFX-6: coin flip audio hooks ──────────────────────────────────
        [SerializeField] private AudioClip _coinFlipStartClip;
        [SerializeField] private AudioClip _coinFlipLandClip;

        // ── Mulligan panel ────────────────────────────────────────────────────
        [SerializeField] private GameObject _mulliganPanel;
        [SerializeField] private Text _mulliganTitleText;
        [SerializeField] private Transform _mulliganCardContainer;
        [SerializeField] private GameObject _cardViewPrefab;
        [SerializeField] private Button _mulliganConfirmButton;
        [SerializeField] private Text _mulliganConfirmLabel;
        [SerializeField] private CanvasGroup _mulliganCG;
        [SerializeField] private CardDetailPopup _cardDetailPopup;

        private const int MAX_MULLIGAN_SWAPS = 2;

        /// <summary>
        /// Bot 专用：设为 true 时，硬币结果和换牌面板出现后自动点击确认，无需人工操作。
        /// </summary>
        public static bool BotAutoAdvance = false;

        // DOT-8: mulligan flip constant
        private const float MULLIGAN_FLIP_HALF  = 0.11f;

        private Sequence _mulliganFlipSeq; // M-3: track flip tween for cleanup

        // DEV-24: animation timing constants (seconds)
        public const float PANEL_FADE_IN  = 0.4f;
        public const float PANEL_FADE_OUT = 0.3f;
        public const float COIN_HALF_FLIP = 0.13f;
        public const int   COIN_FLIP_COUNT = 5;
        public const float COIN_LAND_DUR  = 0.3f;
        public const float RESULT_FADE_IN = 0.4f;
        public const float SCAN_PERIOD    = 8f;

        // VFX-6: coin land burst constants
        public const int   COIN_BURST_COUNT    = 20;
        public const float COIN_BURST_DURATION = 0.6f;
        public const float COIN_BURST_RADIUS   = 130f;
        public const float COIN_BURST_SIZE     = 8f;

        // Scan light half-width (was local const in ScanLightLoop)
        private const float SCAN_HALF_W = 960f;

        private List<UnitInstance> _mulliganHand;
        private List<UnitInstance> _selectedForSwap = new List<UnitInstance>();
        private List<CardView> _mulliganCardViews   = new List<CardView>();

        // DOTween handles (replacing Coroutine fields)
        private Tween _scanLightTween;
        private Tween _hexBreathTween;
        private Tween _titleBeamTween;
        private Tween _bgGradientTween;
        private Tween _coinSpinSeq;       // H2: track coin spin for cleanup
        private Tween _titleEntranceTween; // H3: track title entrance for cleanup

        // DEV-30 V1-V5: title overlay elements (found by name in Awake)
        private Image _hexBreathOverlay;
        private Image _titleBeam;
        private Image _bgGradientOverlay;

        // Active TCS references — resolved in OnDestroy to prevent TCS leaks
        private TaskCompletionSource<bool> _activeCoinTcs;
        private TaskCompletionSource<bool> _activeMulliganTcs;

        // VFX-6: burst particle tracking for interrupt cleanup
        private readonly List<GameObject> _burstParticles = new List<GameObject>();

        private void Awake()
        {
            Instance = this;

            // Coin flip panel starts VISIBLE (alpha=1) — player must never see the board behind it.
            // It will be faded out after the coin flip flow completes.
            if (_coinFlipPanel != null)
            {
                _coinFlipCG = EnsureCG(_coinFlipPanel, _coinFlipCG);
                _coinFlipCG.alpha = 1f;
                _coinFlipCG.interactable = true;
                _coinFlipCG.blocksRaycasts = true;
                _coinFlipPanel.SetActive(true);
            }
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

                // 3D coin rim strip (CoinGroup/CoinContainer/CoinEdge)
                var edgeT = _coinFlipPanel.transform.Find("CoinGroup/CoinContainer/CoinEdge");
                if (edgeT != null) _coinEdgeImage = edgeT.GetComponent<Image>();
            }
        }

        private void OnDisable()
        {
            TweenHelper.KillSafe(ref _scanLightTween);
            TweenHelper.KillSafe(ref _hexBreathTween);
            TweenHelper.KillSafe(ref _titleBeamTween);
            TweenHelper.KillSafe(ref _bgGradientTween);
            TweenHelper.KillSafe(ref _coinSpinSeq);
            TweenHelper.KillSafe(ref _titleEntranceTween);
            TweenHelper.KillSafe(ref _mulliganFlipSeq); // M-3
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TweenHelper.KillSafe(ref _scanLightTween);
            TweenHelper.KillSafe(ref _hexBreathTween);
            TweenHelper.KillSafe(ref _titleBeamTween);
            TweenHelper.KillSafe(ref _bgGradientTween);
            TweenHelper.KillSafe(ref _coinSpinSeq);
            TweenHelper.KillSafe(ref _titleEntranceTween);
            TweenHelper.KillSafe(ref _mulliganFlipSeq); // M-3
            // VFX-6: destroy any lingering burst particles
            foreach (var p in _burstParticles)
            {
                if (p != null)
                {
                    DOTween.Kill(p);
                    Destroy(p);
                }
            }
            _burstParticles.Clear();
            // Resolve any pending TCS so callers don't hang if this component is destroyed mid-flow
            _activeCoinTcs?.TrySetResult(true);
            _activeMulliganTcs?.TrySetResult(true);
        }

        // ── Bot helpers ───────────────────────────────────────────────────────

        /// <summary>Bot 主动点击硬币确认按钮（面板未显示或按钮不可交互时静默忽略）。</summary>
        public bool TryBotClickCoinFlip()
        {
            if (_coinFlipOkButton == null) return false;
            if (!_coinFlipOkButton.interactable) return false;
            if (_coinFlipPanel == null || !_coinFlipPanel.activeSelf) return false;
            _coinFlipOkButton.onClick.Invoke();
            return true;
        }

        /// <summary>Bot 主动点击换牌确认按钮（面板未显示或按钮不可交互时静默忽略）。</summary>
        public bool TryBotClickMulligan()
        {
            if (_mulliganConfirmButton == null) return false;
            if (!_mulliganConfirmButton.interactable) return false;
            if (_mulliganPanel == null || !_mulliganPanel.activeSelf) return false;
            _mulliganConfirmButton.onClick.Invoke();
            return true;
        }

        // ── Public entry point ────────────────────────────────────────────────

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

        /// <summary>Fades a CanvasGroup from 0→1. Returns the tween (yield via WaitForCompletion).</summary>
        private static Tween FadeIn(CanvasGroup cg, float duration)
        {
            if (cg == null) return null;
            cg.alpha = 0f;
            return cg.DOFade(1f, duration).SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    if (cg == null) return;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                })
                .SetTarget(cg);
        }

        /// <summary>Fades a CanvasGroup from current→0. Returns the tween.</summary>
        private static Tween FadeOut(CanvasGroup cg, float duration)
        {
            if (cg == null) return null;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            return cg.DOFade(0f, duration).SetEase(Ease.Linear).SetTarget(cg);
        }

        private IEnumerator FadeOutAndResolve(GameObject panel, CanvasGroup cg,
                                               TaskCompletionSource<bool> tcs)
        {
            var t = FadeOut(cg, PANEL_FADE_OUT);
            if (t != null) yield return t.WaitForCompletion();
            if (panel != null) panel.SetActive(false);
            tcs.TrySetResult(true);
        }

        // ── Coin flip ─────────────────────────────────────────────────────────

        private Task ShowCoinFlip(GameState gs)
        {
            // Hotfix-7: 守卫同 ShowMulligan
            if (this == null || !gameObject.activeInHierarchy)
            {
                var t = new TaskCompletionSource<bool>();
                t.TrySetResult(true);
                return t.Task;
            }
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
            if (_coinCircleImage != null) _coinCircleImage.color = Color.clear;

            // ── Show panel (instant — no fade-in, player should never see the board) ─
            if (_coinFlipPanel != null) _coinFlipPanel.SetActive(true);
            _coinFlipCG = EnsureCG(_coinFlipPanel, _coinFlipCG);
            _coinFlipCG.alpha = 1f;
            _coinFlipCG.interactable = true;
            _coinFlipCG.blocksRaycasts = true;

            // ── Start scan light background loop ──────────────────────────────
            if (_scanLightImage != null)
                _scanLightTween = CreateScanLightTween();

            // ── DEV-30 V1-V5: start title animation tweens ──────────────────
            var coinTitle = _coinFlipPanel?.transform.Find("CoinTitle")?.GetComponent<Text>();
            if (coinTitle != null) _titleEntranceTween = CreateTitleEntranceSequence(coinTitle);
            if (_hexBreathOverlay  != null) _hexBreathTween  = CreateHexBreathTween();
            if (_titleBeam         != null) _titleBeamTween  = CreateTitleBeamTween();
            if (_bgGradientOverlay != null) _bgGradientTween = CreateBgGradientTween();

            // ── Coin flip animation ───────────────────────────────────────────
            _coinSpinSeq = CreateCoinSpinSequence(isPlayerFirst);
            if (_coinSpinSeq != null) yield return _coinSpinSeq.WaitForCompletion();

            // ── VFX-6: gold burst + land audio ───────────────────────────────
            if (AudioTool.Instance != null && _coinFlipLandClip != null)
                AudioTool.Instance.PlayOneShot(AudioTool.CH_UI, _coinFlipLandClip);
            if (_coinCircleImage != null)
                StartCoinBurstTweens(_coinCircleImage.rectTransform);

            // ── Fade in result text ───────────────────────────────────────────
            // Pencil split: short winner line in CoinResultText, arena info in CoinResultBadge.
            string winner = isPlayerFirst ? "玩家先手！" : "AI先手！";
            if (_coinResultText != null)
            {
                _coinResultText.text = winner;
                var txtTween = FadeTextIn(_coinResultText, RESULT_FADE_IN);
                if (txtTween != null) yield return txtTween.WaitForCompletion();
            }
            else if (_coinFlipText != null)
            {
                _coinFlipText.text = winner;
            }
            // Update result badge text (Pencil: green pill showing the gameplay consequence).
            var badgeText = _coinFlipPanel != null
                ? _coinFlipPanel.transform.Find("Panel/CoinResultBadge/CoinResultBadgeText")?.GetComponent<Text>()
                : null;
            if (badgeText != null) badgeText.text = $"战场：{bf0}    ·    {bf1}";

            // ── Enable button ──────────────────────────────────────────────────
            if (_coinFlipOkButton != null)
            {
                _coinFlipOkButton.onClick.RemoveAllListeners();
                _coinFlipOkButton.onClick.AddListener(() =>
                {
                    _coinFlipOkButton.interactable = false;
                    _coinFlipOkButton.onClick.RemoveAllListeners();
                    TweenHelper.KillSafe(ref _scanLightTween);
                    TweenHelper.KillSafe(ref _hexBreathTween);
                    TweenHelper.KillSafe(ref _titleBeamTween);
                    TweenHelper.KillSafe(ref _bgGradientTween);
                    StartCoroutine(FadeOutAndResolve(_coinFlipPanel, _coinFlipCG, tcs));
                });
                yield return new WaitForSeconds(0.5f);
                if (_coinFlipOkButton != null)
                {
                    _coinFlipOkButton.interactable = true;
                    // Bot 模式：自动点击确认按钮
                    if (BotAutoAdvance) _coinFlipOkButton.onClick.Invoke();
                }
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
                TweenHelper.KillSafe(ref _scanLightTween);
                TweenHelper.KillSafe(ref _hexBreathTween);
                TweenHelper.KillSafe(ref _titleBeamTween);
                TweenHelper.KillSafe(ref _bgGradientTween);
                yield return FadeOutAndResolve(_coinFlipPanel, _coinFlipCG, tcs);
            }
        }

        /// <summary>
        /// DOT-5: Coin flip animation as a DOTween Sequence.
        /// COIN_FLIP_COUNT quick flips (scaleX), then landing punch.
        /// </summary>
        private Tween CreateCoinSpinSequence(bool isPlayerFirst)
        {
            if (_coinCircleImage == null)
                return DOVirtual.Float(0f, 1f, 0.6f, _ => { }).SetTarget(gameObject);

            // VFX-6: start audio
            if (AudioTool.Instance != null && _coinFlipStartClip != null)
                AudioTool.Instance.PlayOneShot(AudioTool.CH_UI, _coinFlipStartClip);

            var rt = _coinCircleImage.rectTransform;
            var spriteFirst  = Resources.Load<Sprite>("CardArt/xianshou");
            var spriteSecond = Resources.Load<Sprite>("CardArt/houshou");
            bool hasSprites  = spriteFirst != null && spriteSecond != null;

            // Initial setup
            if (hasSprites)
            {
                _coinCircleImage.sprite         = spriteFirst;
                _coinCircleImage.color          = Color.white;
                _coinCircleImage.preserveAspect = true;
                if (_coinFlipText != null) _coinFlipText.text = "";
            }
            else
            {
                _coinCircleImage.sprite = null;
                _coinCircleImage.color  = new Color(0.78f, 0.67f, 0.43f, 1f); // Gold
            }

            var coinImg  = _coinCircleImage; // capture for closures
            var flipText = _coinFlipText;
            var edgeImg  = _coinEdgeImage;   // 3D rim strip

            // Ensure edge starts invisible
            if (edgeImg != null) { var ec = edgeImg.color; ec.a = 0f; edgeImg.color = ec; }

            var seq = DOTween.Sequence();
            for (int i = 0; i < COIN_FLIP_COUNT; i++)
            {
                bool isLast = (i == COIN_FLIP_COUNT - 1);
                float halfDur = isLast ? COIN_HALF_FLIP * 2f : COIN_HALF_FLIP;
                int ci = i;
                bool capturedLast = isLast;

                // Fold to edge (scaleX → 0)
                // OnUpdate drives edge alpha: edge fades IN as coin compresses (scaleX↓)
                seq.Append(rt.DOScaleX(0f, halfDur).SetEase(Ease.InOutSine)
                    .OnUpdate(() =>
                    {
                        if (edgeImg == null || rt == null) return;
                        float sx = Mathf.Clamp01(rt.localScale.x);
                        var c = edgeImg.color; c.a = 1f - sx; edgeImg.color = c;
                    }));

                // Swap face at midpoint (scaleX == 0, edge fully visible)
                seq.AppendCallback(() =>
                {
                    if (coinImg == null) return;
                    if (capturedLast)
                    {
                        if (hasSprites)
                            coinImg.sprite = isPlayerFirst ? spriteFirst : spriteSecond;
                        else
                            coinImg.color = new Color(0.78f, 0.67f, 0.43f, 1f);
                        if (flipText != null)
                            flipText.text = hasSprites ? "" : (isPlayerFirst ? "先" : "后");
                    }
                    else
                    {
                        if (hasSprites)
                            coinImg.sprite = (ci % 2 == 0) ? spriteSecond : spriteFirst;
                        else
                            coinImg.color = (ci % 2 == 0)
                                ? new Color(0.55f, 0.40f, 0.20f, 1f)   // Bronze
                                : new Color(0.78f, 0.67f, 0.43f, 1f);  // Gold
                        if (flipText != null && !hasSprites) flipText.text = "?";
                    }
                });

                // Unfold (scaleX → 1)
                // OnUpdate drives edge alpha: edge fades OUT as coin opens (scaleX↑)
                seq.Append(rt.DOScaleX(1f, halfDur).SetEase(Ease.InOutSine)
                    .OnUpdate(() =>
                    {
                        if (edgeImg == null || rt == null) return;
                        float sx = Mathf.Clamp01(rt.localScale.x);
                        var c = edgeImg.color; c.a = 1f - sx; edgeImg.color = c;
                    }));

                if (!isLast) seq.AppendInterval(0.04f);
            }

            // Ensure edge is fully hidden after all flips complete
            seq.AppendCallback(() =>
            {
                if (edgeImg == null) return;
                var c = edgeImg.color; c.a = 0f; edgeImg.color = c;
            });

            // Landing bounce
            seq.Append(rt.DOPunchScale(Vector3.one * 0.15f, COIN_LAND_DUR, 1, 0f));
            seq.SetTarget(_coinCircleImage.gameObject);
            return seq;
        }

        /// <summary>
        /// DOT-5: Gold particle burst from coin center on landing.
        /// Each particle gets an independent DOTween Sequence (fire-and-forget).
        /// </summary>
        private void StartCoinBurstTweens(RectTransform origin)
        {
            if (origin == null || _coinFlipPanel == null) return;

            var parentRT = _coinFlipPanel.GetComponent<RectTransform>();
            if (parentRT == null) return;

            // Convert coin world position to panel-local coordinates
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, origin.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, screenPt, null, out Vector2 center);

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

                var img = go.GetComponent<Image>();
                img.color = GameColors.Gold;
                img.raycastTarget = false;

                _burstParticles.Add(go);

                float angle = i / (float)COIN_BURST_COUNT * Mathf.PI * 2f;
                Vector2 endPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * COIN_BURST_RADIUS;

                // Per-particle animation: radial move + shrink + fade
                var pSeq = DOTween.Sequence();
                pSeq.Append(rt.DOAnchorPos(endPos, COIN_BURST_DURATION).SetEase(Ease.OutCubic));
                pSeq.Join(rt.DOSizeDelta(new Vector2(2f, 2f), COIN_BURST_DURATION).SetEase(Ease.InQuad));
                pSeq.Join(rt.DOLocalRotate(new Vector3(0f, 0f, 180f), COIN_BURST_DURATION, RotateMode.FastBeyond360).SetEase(Ease.Linear));
                pSeq.Join(img.DOFade(0f, COIN_BURST_DURATION).SetEase(Ease.InQuad));
                pSeq.SetTarget(go);
                pSeq.OnComplete(() =>
                {
                    _burstParticles.Remove(go);
                    Destroy(go);
                });
            }
        }

        /// <summary>Fades a Text from alpha 0→1. Returns tween for yield.</summary>
        private static Tween FadeTextIn(Text text, float duration)
        {
            if (text == null) return null;
            SetTextAlpha(text, 0f);
            return DOTween.To(() => text.color.a, a => SetTextAlpha(text, a), 1f, duration)
                .SetTarget(text.gameObject);
        }

        private static void SetTextAlpha(Text text, float a)
        {
            Color c = text.color;
            c.a = a;
            text.color = c;
        }

        // ── Background animation tweens ──────────────────────────────────────

        /// <summary>Scan light sweeps left→right over SCAN_PERIOD, repeating.</summary>
        private Tween CreateScanLightTween()
        {
            if (_scanLightImage == null) return null;
            var rt = _scanLightImage.rectTransform;
            rt.anchoredPosition = new Vector2(-SCAN_HALF_W, rt.anchoredPosition.y);
            return rt.DOAnchorPosX(SCAN_HALF_W, SCAN_PERIOD)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetTarget(_scanLightImage.gameObject);
        }

        /// <summary>V2: Hexagonal overlay alpha breath (3s sine, 0.15↔0.35).</summary>
        private Tween CreateHexBreathTween()
        {
            if (_hexBreathOverlay == null) return null;
            const float period   = 3f;
            const float minAlpha = 0.15f;
            const float maxAlpha = 0.35f;
            return DOVirtual.Float(0f, 1f, period, v =>
            {
                if (_hexBreathOverlay == null) return;
                float t = (Mathf.Sin(v * Mathf.PI * 2f) + 1f) * 0.5f;
                var col = _hexBreathOverlay.color;
                col.a = Mathf.Lerp(minAlpha, maxAlpha, t);
                _hexBreathOverlay.color = col;
            }).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear)
              .SetTarget(_hexBreathOverlay.gameObject);
        }

        /// <summary>V3: Center beam pulse (2s sine, alpha 0→0.45→0).</summary>
        private Tween CreateTitleBeamTween()
        {
            if (_titleBeam == null) return null;
            const float period   = 2f;
            const float maxAlpha = 0.45f;
            return DOVirtual.Float(0f, 1f, period, v =>
            {
                if (_titleBeam == null) return;
                float t = (Mathf.Sin(v * Mathf.PI * 2f) + 1f) * 0.5f;
                var col = _titleBeam.color;
                col.a = t * maxAlpha;
                _titleBeam.color = col;
            }).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear)
              .SetTarget(_titleBeam.gameObject);
        }

        /// <summary>V5: Background gradient overlay slow rotation (5°/s CW, 72s per revolution).</summary>
        private Tween CreateBgGradientTween()
        {
            if (_bgGradientOverlay == null) return null;
            return _bgGradientOverlay.transform
                .DOLocalRotate(new Vector3(0f, 0f, -360f), 72f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetTarget(_bgGradientOverlay.gameObject);
        }

        /// <summary>V1: Coin flip title text entrance — fadeInUp + scale + alpha.</summary>
        private static Tween CreateTitleEntranceSequence(Text titleText)
        {
            if (titleText == null) return null;
            const float dur = 0.45f;
            var rt = titleText.rectTransform;
            Vector2 endPos   = rt.anchoredPosition;
            Vector2 startPos = endPos + new Vector2(0f, -20f);

            rt.anchoredPosition = startPos;
            titleText.transform.localScale = Vector3.one * 0.85f;
            var c = titleText.color; c.a = 0f; titleText.color = c;

            var seq = DOTween.Sequence();
            seq.Append(rt.DOAnchorPos(endPos, dur).SetEase(Ease.OutQuad));
            seq.Join(titleText.transform.DOScale(1f, dur).SetEase(Ease.OutQuad));
            seq.Join(DOTween.To(() => titleText.color.a, a =>
            {
                var col = titleText.color;
                col.a = a;
                titleText.color = col;
            }, 1f, dur).SetEase(Ease.OutQuad));
            seq.SetTarget(titleText.gameObject);
            return seq;
        }

        // ── Mulligan ──────────────────────────────────────────────────────────

        private Task ShowMulligan(GameState gs)
        {
            // Hotfix-7: 守卫 — OnDestroy 触发 TrySetResult 后 continuation 会跑到这里，
            // 此时 GameObject 已 inactive，StartCoroutine 会抛错
            if (this == null || !gameObject.activeInHierarchy)
            {
                var t = new TaskCompletionSource<bool>();
                t.TrySetResult(true);
                return t.Task;
            }
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
                    // Pencil spec: mulligan cards are 200×300 (vs hand's 110×154 prefab size).
                    var cardRT = cardGO.GetComponent<RectTransform>();
                    if (cardRT != null) cardRT.sizeDelta = new Vector2(200f, 300f);
                    var cardLE = cardGO.GetComponent<LayoutElement>();
                    if (cardLE == null) cardLE = cardGO.AddComponent<LayoutElement>();
                    cardLE.preferredWidth = 200f;
                    cardLE.preferredHeight = 300f;
                    CardView cv = cardGO.GetComponent<CardView>();
                    if (cv != null)
                    {
                        cv.Setup(captured, true, OnMulliganCardClicked,
                            onRightClick: u => { if (_cardDetailPopup != null && u != null) _cardDetailPopup.Show(u); });
                        _mulliganCardViews.Add(cv);
                    }
                }
            }

            UpdateMulliganUI();

            if (_mulliganPanel != null) _mulliganPanel.SetActive(true);
            _mulliganCG = EnsureCG(_mulliganPanel, _mulliganCG);
            var fadeInTween = FadeIn(_mulliganCG, PANEL_FADE_IN);
            if (fadeInTween != null) yield return fadeInTween.WaitForCompletion();

            // DOT-8: shuffle animation removed — felt out of place

            if (_mulliganConfirmButton != null)
            {
                _mulliganConfirmButton.onClick.RemoveAllListeners();
                _mulliganConfirmButton.onClick.AddListener(() =>
                {
                    _mulliganConfirmButton.interactable = false;
                    _mulliganConfirmButton.onClick.RemoveAllListeners();
                    PerformMulligan(gs);
                    StartCoroutine(FadeOutAndResolve(_mulliganPanel, _mulliganCG, tcs));
                });
                // Bot 模式：自动点击确认（不换牌，直接接受初始手牌）
                if (BotAutoAdvance) _mulliganConfirmButton.onClick.Invoke();
            }
            else
            {
                tcs.TrySetResult(true);
            }
        }

        private void OnMulliganCardClicked(UnitInstance unit)
        {
            // 和手牌一样：单击切换选中；未选的卡在已达上限时忽略（不弹提示，直接无反应）
            if (_selectedForSwap.Contains(unit))
                _selectedForSwap.Remove(unit);
            else if (_selectedForSwap.Count < MAX_MULLIGAN_SWAPS)
                _selectedForSwap.Add(unit);
            else
                return; // 已满，未选的卡点击无效
            UpdateMulliganUI();
            // 翻牌动画已移除：选中视觉走 CardView.SetSelected（和手牌同一套 outline glow）
        }

        private void UpdateMulliganUI()
        {
            if (_mulliganTitleText != null)
                _mulliganTitleText.text =
                    $"已选 {_selectedForSwap.Count} / {MAX_MULLIGAN_SWAPS}    ·    被选中的牌将弃掉重抽";

            if (_mulliganConfirmLabel != null)
                _mulliganConfirmLabel.text = _selectedForSwap.Count > 0
                    ? $"确认换 {_selectedForSwap.Count} 张"
                    : "不换牌，开始";

            // 已达上限时，未选中的卡视觉压暗提示"不可再选"
            bool atCap = _selectedForSwap.Count >= MAX_MULLIGAN_SWAPS;
            for (int i = 0; i < _mulliganCardViews.Count && i < _mulliganHand.Count; i++)
            {
                bool selected = _selectedForSwap.Contains(_mulliganHand[i]);
                _mulliganCardViews[i].SetSelected(selected);

                // 非选中 + 已达上限 → 压暗；其他情况恢复正常色
                var cg = _mulliganCardViews[i].GetComponent<CanvasGroup>();
                if (cg == null) cg = _mulliganCardViews[i].gameObject.AddComponent<CanvasGroup>();
                cg.alpha = (atCap && !selected) ? 0.45f : 1f;
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
