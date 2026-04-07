using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-19: General-purpose async prompt dialog.
    ///
    /// Supports two modes:
    ///   1. Card-pick — shows a scrollable card list; player taps a card → Task&lt;UnitInstance&gt; completes.
    ///   2. Confirm  — shows title + message + Confirm / Cancel buttons → Task&lt;bool&gt; completes.
    ///
    /// Usage:
    ///   var chosen = await AskPromptUI.Instance.WaitForCardChoice(cards, "选择一张牌");
    ///   bool ok    = await AskPromptUI.Instance.WaitForConfirm("确认", "是否继续？");
    ///
    /// Panel stays hidden (CanvasGroup alpha=0, blocksRaycasts=false) when not in use.
    /// All references wired by SceneBuilder; accessible via singleton Instance.
    ///
    /// DOT-3: ShowRoutine/HideRoutine coroutines → DOScale tweens.
    /// </summary>
    public class AskPromptUI : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static AskPromptUI Instance { get; private set; }

        /// <summary>True while the popup is visible (including entry/exit animation).</summary>
        public static bool IsShowing { get; private set; }

        // ── Inspector refs (wired by SceneBuilder) ────────────────────────────
        [SerializeField] private GameObject  _panel;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text        _titleText;
        [SerializeField] private Text        _messageText;
        [SerializeField] private Transform   _cardContainer;
        [SerializeField] private GameObject  _cardViewPrefab;
        [SerializeField] private Button      _confirmBtn;
        [SerializeField] private Button      _cancelBtn;
        [SerializeField] private Text        _confirmBtnText;
        [SerializeField] private Text        _cancelBtnText;

        // ── Async state ───────────────────────────────────────────────────────
        private TaskCompletionSource<UnitInstance> _cardTcs;
        private TaskCompletionSource<bool>         _confirmTcs;

        // ── Animation constants ───────────────────────────────────────────────
        private const float SHOW_DURATION = 0.20f;
        private const float HIDE_DURATION = 0.12f;

        // ── DOTween state ─────────────────────────────────────────────────────
        private Tween _animTween;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Hide();

            if (_messageText != null) _messageText.supportRichText = true;
            if (_confirmBtn != null) _confirmBtn.onClick.AddListener(OnConfirmClicked);
            if (_cancelBtn  != null) _cancelBtn.onClick.AddListener(OnCancelClicked);
        }

        private void OnEnable()  { GameEventBus.OnClearBanners += OnCancelClicked; }
        private void OnDisable() { GameEventBus.OnClearBanners -= OnCancelClicked; }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _animTween);
            // Cancel pending tasks on scene teardown so awaiters get TaskCanceledException
            // rather than a sentinel value that looks like a legitimate user decision (H-1 fix)
            _cardTcs?.TrySetCanceled();
            _confirmTcs?.TrySetCanceled();
            if (Instance == this) Instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the card-pick dialog. Player must tap a card from the list.
        /// Completes immediately with null when the list is null or empty.
        /// </summary>
        public Task<UnitInstance> WaitForCardChoice(
            List<UnitInstance> cards,
            string title,
            string message = "")
        {
            // Cancel any pending prompt before starting a new one (HIGH fix)
            _cardTcs?.TrySetResult(null);
            _confirmTcs?.TrySetResult(false);

            _cardTcs    = new TaskCompletionSource<UnitInstance>();
            _confirmTcs = null;

            if (_titleText   != null) _titleText.text   = title;
            if (_messageText != null) _messageText.text  = message;

            ClearCardContainer();

            // Hide confirm/cancel for card-pick mode
            if (_confirmBtn != null) _confirmBtn.gameObject.SetActive(false);
            if (_cancelBtn  != null)
            {
                _cancelBtn.gameObject.SetActive(true);
                if (_cancelBtnText != null) _cancelBtnText.text = "取消";
            }

            // Auto-complete if nothing to pick — also hide any previously visible panel (H-2 fix)
            if (cards == null || cards.Count == 0)
            {
                Hide();
                _cardTcs.TrySetResult(null);
                return _cardTcs.Task;
            }

            // Populate card list
            if (_cardContainer != null && _cardViewPrefab != null)
            {
                foreach (UnitInstance card in cards)
                {
                    UnitInstance captured = card;
                    GameObject go = Instantiate(_cardViewPrefab, _cardContainer);
                    var cv = go.GetComponent<CardView>();
                    if (cv != null)
                        cv.Setup(captured, true, OnCardChosen);
                }
            }

            Show();
            return _cardTcs.Task;
        }

        /// <summary>
        /// Shows the confirm dialog with Confirm / Cancel buttons.
        /// Returns true when Confirm is clicked, false when Cancel is clicked.
        /// </summary>
        public Task<bool> WaitForConfirm(
            string title,
            string message,
            string confirmLabel = "确认",
            string cancelLabel  = "取消")
        {
            // Cancel any pending prompt before starting a new one (HIGH fix)
            _cardTcs?.TrySetResult(null);
            _confirmTcs?.TrySetResult(false);

            _confirmTcs = new TaskCompletionSource<bool>();
            _cardTcs    = null;

            if (_titleText   != null) _titleText.text   = title;
            if (_messageText != null) _messageText.text  = message;

            ClearCardContainer();

            if (_confirmBtn     != null) _confirmBtn.gameObject.SetActive(true);
            if (_cancelBtn      != null) _cancelBtn.gameObject.SetActive(true);
            if (_confirmBtnText != null) _confirmBtnText.text = confirmLabel;
            if (_cancelBtnText  != null) _cancelBtnText.text  = cancelLabel;

            Show();
            return _confirmTcs.Task;
        }

        // ── Bot helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Bot calls this to programmatically answer the current dialog.
        /// For confirm dialogs: confirm=true → click Confirm, false → click Cancel.
        /// For card-choice dialogs: picks the first card in the list (confirm ignored).
        /// </summary>
        public void BotAutoAnswer(bool confirm)
        {
            if (_confirmTcs != null)
            {
                Hide();
                _confirmTcs?.TrySetResult(confirm);
            }
            else if (_cardTcs != null)
            {
                // Pick first available card button in container
                if (_cardContainer != null)
                {
                    var cv = _cardContainer.GetComponentInChildren<CardView>();
                    if (cv != null && cv.Unit != null)
                    {
                        Hide();
                        _cardTcs?.TrySetResult(cv.Unit);
                        return;
                    }
                }
                Hide();
                _cardTcs?.TrySetResult(null);
            }
        }

        // ── Test helpers (allow unit tests to resolve pending tasks) ──────────

        /// <summary>Cancels any pending task. Used by unit tests to avoid hanging.</summary>
        public void CancelFromTest()
        {
            _cardTcs?.TrySetResult(null);
            _confirmTcs?.TrySetResult(false);
            Hide();
        }

        /// <summary>Resets the singleton for unit tests.</summary>
        public static void ResetInstanceForTest()
        {
            Instance = null;
        }

        // ── Private callbacks ─────────────────────────────────────────────────

        private void OnCardChosen(UnitInstance card)
        {
            Hide();
            _cardTcs?.TrySetResult(card);
        }

        private void OnConfirmClicked()
        {
            Hide();
            _confirmTcs?.TrySetResult(true);
        }

        private void OnCancelClicked()
        {
            Hide();
            _confirmTcs?.TrySetResult(false);
            _cardTcs?.TrySetResult(null);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void Show()
        {
            IsShowing = true;
            transform.SetAsLastSibling();
            if (_panel != null)
            {
                _panel.SetActive(true);
                _panel.transform.localScale = Vector3.zero;
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = 1f;
                _canvasGroup.interactable   = true;
                _canvasGroup.blocksRaycasts = true;
            }
            TweenHelper.KillSafe(ref _animTween);
            if (_panel != null && gameObject.activeInHierarchy)
            {
                _animTween = _panel.transform.DOScale(1f, SHOW_DURATION)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetTarget(_panel);
            }
            else if (_panel != null)
            {
                _panel.transform.localScale = Vector3.one;
            }
        }

        private void Hide()
        {
            IsShowing = false;
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable   = false;
                _canvasGroup.blocksRaycasts = false;
            }
            TweenHelper.KillSafe(ref _animTween);
            bool panelVisible = _panel != null && _panel.activeSelf;
            if (gameObject.activeInHierarchy && panelVisible)
            {
                _animTween = _panel.transform.DOScale(0f, HIDE_DURATION)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true)
                    .SetTarget(_panel)
                    .OnComplete(() =>
                    {
                        if (_panel != null) _panel.SetActive(false);
                        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                    });
            }
            else
            {
                if (_panel != null) _panel.SetActive(false);
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            }
        }

        private void ClearCardContainer()
        {
            if (_cardContainer == null) return;
            foreach (Transform child in _cardContainer)
                Destroy(child.gameObject);
        }
    }
}
