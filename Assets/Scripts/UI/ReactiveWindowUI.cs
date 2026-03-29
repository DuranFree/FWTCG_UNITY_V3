using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// Reaction window: shown when the AI casts a spell (DEV-4).
    /// Displays the player's reactive cards in hand + a Pass button.
    /// The player can choose to play one reactive card or pass.
    ///
    /// Uses TaskCompletionSource for async/await integration.
    /// Returns the chosen reactive UnitInstance, or null if the player passes.
    /// </summary>
    public class ReactiveWindowUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _contextText;
        [SerializeField] private Transform _cardContainer;
        [SerializeField] private Button _passButton;
        [SerializeField] private GameObject _cardViewPrefab;

        private TaskCompletionSource<UnitInstance> _tcs;
        private readonly List<CardView> _cardViews = new List<CardView>();

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the reaction window with the given reactive cards.
        /// Returns the card the player chose to play, or null if they passed.
        /// If no reactive cards exist or UI is not set up, returns null immediately.
        /// </summary>
        public Task<UnitInstance> WaitForReaction(
            List<UnitInstance> reactiveCards,
            string triggerSpellName,
            GameState gs)
        {
            _tcs = new TaskCompletionSource<UnitInstance>();
            _cardViews.Clear();

            // Update context text
            if (_contextText != null)
                _contextText.text = $"对手发动了【{triggerSpellName}】\n你是否要反应？";

            // Clear any leftover card views
            if (_cardContainer != null)
            {
                foreach (Transform child in _cardContainer)
                    Destroy(child.gameObject);
            }

            // Auto-pass if no cards available or UI missing
            if (reactiveCards == null || reactiveCards.Count == 0 || _panel == null)
            {
                _tcs.TrySetResult(null);
                return _tcs.Task;
            }

            // Instantiate a CardView per reactive card
            if (_cardViewPrefab != null && _cardContainer != null)
            {
                foreach (UnitInstance card in reactiveCards)
                {
                    UnitInstance captured = card;
                    var go = Instantiate(_cardViewPrefab, _cardContainer);
                    var cv = go.GetComponent<CardView>();
                    if (cv != null)
                    {
                        cv.Setup(captured, true, OnReactiveCardClicked);
                        _cardViews.Add(cv);
                    }
                }
            }

            // Wire Pass button
            if (_passButton != null)
            {
                _passButton.onClick.RemoveAllListeners();
                _passButton.onClick.AddListener(() =>
                {
                    HidePanel();
                    _tcs.TrySetResult(null);
                });
            }
            else
            {
                // No pass button: auto-pass immediately
                _tcs.TrySetResult(null);
                return _tcs.Task;
            }

            // Show the panel
            if (_panel != null) _panel.SetActive(true);

            return _tcs.Task;
        }

        // ── Private callbacks ─────────────────────────────────────────────────

        private void OnReactiveCardClicked(UnitInstance card)
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
