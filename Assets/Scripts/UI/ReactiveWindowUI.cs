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
    ///
    /// Uses TaskCompletionSource for async/await integration.
    /// Returns the chosen card. Auto-completes with null if no cards provided.
    /// </summary>
    public class ReactiveWindowUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _contextText;
        [SerializeField] private Transform _cardContainer;
        [SerializeField] private GameObject _cardViewPrefab;

        private TaskCompletionSource<UnitInstance> _tcs;
        private readonly List<CardView> _cardViews = new List<CardView>();

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
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
            GameState gs)
        {
            _tcs = new TaskCompletionSource<UnitInstance>();
            _cardViews.Clear();

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
                        cv.Setup(captured, true, OnCardClicked);
                        _cardViews.Add(cv);
                    }
                }
            }

            // Show the panel (no pass button — player must pick)
            if (_panel != null) _panel.SetActive(true);

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

        // ── Private callbacks ─────────────────────────────────────────────────

        private void OnCardClicked(UnitInstance card)
        {
            HidePanel();
            _tcs?.TrySetResult(card);
        }

        private void HidePanel()
        {
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
