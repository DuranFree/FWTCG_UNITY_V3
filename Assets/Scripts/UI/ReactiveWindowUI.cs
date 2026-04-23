using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// Reaction window: shown when the player clicks the React button.
    /// Displays the player's reactive cards in hand.
    /// The player MUST select one card — there is no pass/cancel option.
    /// Auto-times out after REACTION_TIMEOUT seconds (SkipReaction called).
    ///
    /// Uses TaskCompletionSource for async/await integration.
    /// Returns the chosen card. Auto-completes with null if no cards provided.
    /// </summary>
    public class ReactiveWindowUI : MonoBehaviour, IReactionWindow
    {
        public static ReactiveWindowUI Instance { get; private set; }
        private bool _initialized;

        [SerializeField] private GameObject    _panel;
        [SerializeField] private Text          _contextText;
        [SerializeField] private Transform     _cardContainer;
        [SerializeField] private GameObject    _cardViewPrefab;
        [SerializeField] private CardDetailPopup _cardDetailPopup;
        [SerializeField] private Image         _countdownFill;  // Radial360 clock fill
        [SerializeField] private Text          _countdownText;  // seconds label

        // 边框倒计时光条（顺时针 Top→Right→Bottom→Left 按 25% 区间逐段消耗，颜色绿→黄→红）
        [SerializeField] private Image _borderTop;
        [SerializeField] private Image _borderRight;
        [SerializeField] private Image _borderBottom;
        [SerializeField] private Image _borderLeft;

        private const float REACTION_TIMEOUT = 15f;

        private TaskCompletionSource<UnitInstance> _tcs;
        private readonly List<CardView> _cardViews = new List<CardView>();
        private List<UnitInstance> _pendingCards;
        private Tween _countdownTween;
        private GameState _gs;

        private void Awake()
        {
            if (_initialized) return; // guard: prevents double-init if called twice
            _initialized = true;
            Instance = this;
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()  { GameEventBus.OnClearBanners += AutoPlayRandom; }
        private void OnDisable()
        {
            GameEventBus.OnClearBanners -= AutoPlayRandom;
            // Cancel any pending awaiter when this component is disabled (e.g. scene reload)
            _tcs?.TrySetCanceled();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TweenHelper.KillSafe(ref _countdownTween);
            // H-3: Cancel any pending awaiter when the window is destroyed mid-flow
            _tcs?.TrySetCanceled();
        }

        /// <summary>
        /// Called on turn change: must play a random card — reactive cards cannot be skipped.
        /// </summary>
        /// <summary>
        /// DEV-31 cleanup: 严格防御版 AutoPlayRandom — 不仅检测 chosen 是否在手，
        /// 先遍历 _pendingCards 过滤掉所有已失效的牌（可能因为其他效果离手），
        /// 然后再从剩余有效项里随机。仍无效则 SkipReaction。
        /// </summary>
        private void FilterStalePendingCards()
        {
            if (_pendingCards == null || _gs == null) return;
            _pendingCards.RemoveAll(c => c == null || !_gs.PHand.Contains(c));
        }

        private void AutoPlayRandom()
        {
            FilterStalePendingCards();
            if (_tcs == null || _tcs.Task.IsCompleted) return;
            if (_pendingCards == null || _pendingCards.Count == 0) { SkipReaction(); return; }
            var chosen = _pendingCards[UnityEngine.Random.Range(0, _pendingCards.Count)];
            // H-2: guard — card must still be in hand (state may have changed since window opened)
            if (_gs != null && !_gs.PHand.Contains(chosen)) { SkipReaction(); return; }
            HidePanel();
            _tcs.TrySetResult(chosen);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the reaction window with the given cards.
        /// The caller is responsible for filtering to only valid/affordable cards.
        /// Returns the card the player chose to play.
        /// Auto-completes with null if the card list is empty or UI is not set up.
        /// There is NO pass button — the player must select a card.
        /// </summary>
        public Task<UnitInstance> WaitForReaction(
            List<UnitInstance> cards,
            string contextMsg,
            GameState gs,
            System.Action<UnitInstance> onHoverEnter = null,
            System.Action<UnitInstance> onHoverExit  = null)
        {
            // Cancel any previous awaiter before replacing — prevents the old Task from hanging
            _tcs?.TrySetCanceled();
            _tcs = new TaskCompletionSource<UnitInstance>();
            _cardViews.Clear();
            _pendingCards = cards != null ? new List<UnitInstance>(cards) : null;
            _gs = gs;

            // Update context text
            if (_contextText != null)
                _contextText.text = string.IsNullOrEmpty(contextMsg)
                    ? "选择要打出的反应牌（必须打出一张）"
                    : contextMsg;

            // Clear any leftover card views
            if (_cardContainer != null)
            {
                foreach (Transform child in _cardContainer)
                    Destroy(child.gameObject);
            }

            // Auto-complete if no cards or UI missing
            if (cards == null || cards.Count == 0 || _panel == null)
            {
                _tcs.TrySetResult(null);
                return _tcs.Task;
            }

            // Instantiate a CardView per card
            if (_cardViewPrefab != null && _cardContainer != null)
            {
                foreach (UnitInstance card in cards)
                {
                    UnitInstance captured = card;
                    var go = Instantiate(_cardViewPrefab, _cardContainer);
                    var cv = go.GetComponent<CardView>();
                    if (cv != null)
                    {
                        cv.Setup(captured, true, OnCardClicked,
                                 onRightClick: u => { if (_cardDetailPopup != null && u != null) _cardDetailPopup.Show(u); },
                                 onHoverEnter: onHoverEnter,
                                 onHoverExit:  onHoverExit);
                        _cardViews.Add(cv);
                    }
                }
            }

            // Show the panel (no pass button — player must pick)
            if (_panel != null) _panel.SetActive(true);

            // Start countdown via DOTween
            TweenHelper.KillSafe(ref _countdownTween);
            StartCountdown();

            return _tcs.Task;
        }

        /// <summary>
        /// Skips the current reaction window, completing with null.
        /// Called by the "跳过响应" action button (DEV-9).
        /// </summary>
        public void SkipReaction()
        {
            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                HidePanel();
                _tcs.TrySetResult(null);
            }
        }

        /// <summary>
        /// Called by SpellDuelUI when the 30s duel countdown expires.
        /// Auto-completes the reaction window with null (no card selected). DEV-30 F2.
        /// </summary>
        public void AutoSkipReaction() => SkipReaction();

        // ── Countdown ─────────────────────────────────────────────────────────

        private void StartCountdown()
        {
            if (_countdownFill != null) _countdownFill.fillAmount = 1f;
            if (_countdownText != null) _countdownText.text = Mathf.CeilToInt(REACTION_TIMEOUT).ToString();

            // 初始化边框：全满、绿色
            ResetBorders();

            Color normalColor = _countdownText != null ? _countdownText.color : Color.white;
            _countdownTween = DOVirtual.Float(REACTION_TIMEOUT, 0f, REACTION_TIMEOUT, remaining =>
            {
                float t = Mathf.Clamp01(remaining / REACTION_TIMEOUT); // 1 → 0
                if (_countdownFill != null) _countdownFill.fillAmount = t;

                // 边框推进：剩余比例 t 分配到顺时针 4 段（Left/Bottom/Right/Top 按顺序保留）
                UpdateBorders(t);

                if (_countdownText != null)
                {
                    _countdownText.text = Mathf.CeilToInt(remaining).ToString();
                    // Last 3 seconds: pulse red warning
                    if (remaining < 3f)
                    {
                        float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                        _countdownText.color = Color.Lerp(normalColor,
                            new Color(1f, 0.2f, 0.2f, 1f), pulse * (3f - remaining) / 3f);
                    }
                    else
                    {
                        _countdownText.color = normalColor;
                    }
                }
            })
            .SetEase(Ease.Linear)
            .SetTarget(gameObject)
            .OnComplete(() =>
            {
                if (_countdownFill != null) _countdownFill.fillAmount = 0f;
                if (_countdownText != null) _countdownText.text = "0";
                UpdateBorders(0f); // 全部消耗完
                _countdownTween = null;
                SkipReaction();
            });
        }

        private void ResetBorders()
        {
            Color green = new Color(0.3f, 1f, 0.3f, 1f);
            if (_borderTop    != null) { _borderTop.fillAmount    = 1f; _borderTop.color    = green; }
            if (_borderRight  != null) { _borderRight.fillAmount  = 1f; _borderRight.color  = green; }
            if (_borderBottom != null) { _borderBottom.fillAmount = 1f; _borderBottom.color = green; }
            if (_borderLeft   != null) { _borderLeft.fillAmount   = 1f; _borderLeft.color   = green; }
        }

        /// <summary>
        /// 按剩余比例 t（1→0）推进 4 条边框。顺时针消耗：Top 段先空（t 从 1 降到 0.75），然后 Right（0.75→0.5），Bottom（0.5→0.25），Left（0.25→0）。
        /// 颜色基于 elapsed = 1-t 全局绿→黄→红。
        /// </summary>
        private void UpdateBorders(float t)
        {
            // 各边剩余量：每段占 0.25 区间
            float topRemain    = Mathf.Clamp01((t - 0.75f) * 4f);
            float rightRemain  = Mathf.Clamp01((t - 0.50f) * 4f);
            float bottomRemain = Mathf.Clamp01((t - 0.25f) * 4f);
            float leftRemain   = Mathf.Clamp01(t * 4f);

            if (_borderTop    != null) _borderTop.fillAmount    = topRemain;
            if (_borderRight  != null) _borderRight.fillAmount  = rightRemain;
            if (_borderBottom != null) _borderBottom.fillAmount = bottomRemain;
            if (_borderLeft   != null) _borderLeft.fillAmount   = leftRemain;

            // 全局颜色 绿→黄→红
            float elapsed = 1f - t;
            Color c = elapsed < 0.5f
                ? Color.Lerp(new Color(0.3f, 1f, 0.3f, 1f), new Color(1f, 0.9f, 0.2f, 1f), elapsed * 2f)
                : Color.Lerp(new Color(1f, 0.9f, 0.2f, 1f), new Color(1f, 0.2f, 0.2f, 1f), (elapsed - 0.5f) * 2f);
            if (_borderTop    != null) _borderTop.color    = c;
            if (_borderRight  != null) _borderRight.color  = c;
            if (_borderBottom != null) _borderBottom.color = c;
            if (_borderLeft   != null) _borderLeft.color   = c;
        }

        // ── Private callbacks ─────────────────────────────────────────────────

        private void OnCardClicked(UnitInstance card)
        {
            HidePanel();
            _tcs?.TrySetResult(card);
        }

        private void HidePanel()
        {
            TweenHelper.KillSafe(ref _countdownTween);
            if (_panel != null) _panel.SetActive(false);

            if (_cardContainer != null)
            {
                foreach (Transform child in _cardContainer)
                    Destroy(child.gameObject);
            }

            _cardViews.Clear();
        }
    }
}
