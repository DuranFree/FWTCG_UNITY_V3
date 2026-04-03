using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    public class ReactiveWindowUI : MonoBehaviour
    {
        public static ReactiveWindowUI Instance { get; private set; }
        private bool _initialized;

        [SerializeField] private GameObject    _panel;
        [SerializeField] private Text          _contextText;
        [SerializeField] private Transform     _cardContainer;
        [SerializeField] private GameObject    _cardViewPrefab;
        [SerializeField] private Image         _countdownFill;  // Radial360 clock fill
        [SerializeField] private Text          _countdownText;  // seconds label

        private const float REACTION_TIMEOUT = 15f;

        private TaskCompletionSource<UnitInstance> _tcs;
        private readonly List<CardView> _cardViews = new List<CardView>();
        private List<UnitInstance> _pendingCards;
        private Coroutine _countdownRoutine;
        private GameState _gs;

        private void Awake()
        {
            if (_initialized) return; // guard: prevents double-init if called twice
            _initialized = true;
            Instance = this;
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()  { GameEventBus.OnClearBanners += AutoPlayRandom; }
        private void OnDisable() { GameEventBus.OnClearBanners -= AutoPlayRandom; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // H-3: Cancel any pending awaiter when the window is destroyed mid-flow
            _tcs?.TrySetCanceled();
        }

        /// <summary>
        /// Called on turn change: must play a random card — reactive cards cannot be skipped.
        /// </summary>
        private void AutoPlayRandom()
        {
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
                                 onRightClick: null,
                                 onHoverEnter: onHoverEnter,
                                 onHoverExit:  onHoverExit);
                        _cardViews.Add(cv);
                    }
                }
            }

            // Show the panel (no pass button — player must pick)
            if (_panel != null) _panel.SetActive(true);

            // Start countdown
            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
            _countdownRoutine = StartCoroutine(CountdownRoutine());

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

        private IEnumerator CountdownRoutine()
        {
            float remaining = REACTION_TIMEOUT;
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                float t = Mathf.Clamp01(remaining / REACTION_TIMEOUT);
                if (_countdownFill != null)  _countdownFill.fillAmount = t;
                if (_countdownText != null)  _countdownText.text = Mathf.CeilToInt(remaining).ToString();
                yield return null;
            }
            // Time's up → auto-skip
            if (_countdownFill != null) _countdownFill.fillAmount = 0f;
            if (_countdownText != null) _countdownText.text = "0";
            _countdownRoutine = null;
            SkipReaction();
        }

        // ── Private callbacks ─────────────────────────────────────────────────

        private void OnCardClicked(UnitInstance card)
        {
            HidePanel();
            _tcs?.TrySetResult(card);
        }

        private void HidePanel()
        {
            if (_countdownRoutine != null)
            {
                StopCoroutine(_countdownRoutine);
                _countdownRoutine = null;
            }
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
