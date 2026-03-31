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
    ///   1. Coin flip display (who goes first)
    ///   2. Mulligan (player selects up to 2 cards to swap)
    ///
    /// Call RunStartupFlow() from GameManager before starting the game loop.
    /// </summary>
    public class StartupFlowUI : MonoBehaviour
    {
        // ── Coin flip panel ───────────────────────────────────────────────────
        [SerializeField] private GameObject _coinFlipPanel;
        [SerializeField] private Text _coinFlipText;
        [SerializeField] private Button _coinFlipOkButton;

        // ── Mulligan panel ────────────────────────────────────────────────────
        [SerializeField] private GameObject _mulliganPanel;
        [SerializeField] private Text _mulliganTitleText;
        [SerializeField] private Transform _mulliganCardContainer;
        [SerializeField] private GameObject _cardViewPrefab;
        [SerializeField] private Button _mulliganConfirmButton;
        [SerializeField] private Text _mulliganConfirmLabel;

        private const int MAX_MULLIGAN_SWAPS = 2;

        private List<UnitInstance> _mulliganHand;
        private List<UnitInstance> _selectedForSwap = new List<UnitInstance>();
        private List<CardView> _mulliganCardViews = new List<CardView>();

        private void Awake()
        {
            if (_coinFlipPanel != null) _coinFlipPanel.SetActive(false);
            if (_mulliganPanel != null) _mulliganPanel.SetActive(false);
        }

        /// <summary>
        /// Runs the full startup sequence. Await this before starting the game loop.
        /// </summary>
        public async Task RunStartupFlow(GameState gs)
        {
            await ShowCoinFlip(gs);
            await ShowMulligan(gs);
        }

        // ── Coin flip ─────────────────────────────────────────────────────────

        private Task ShowCoinFlip(GameState gs)
        {
            var tcs = new TaskCompletionSource<bool>();

            string label = gs.First == GameRules.OWNER_PLAYER ? "玩家先手！" : "AI先手！";
            string bf0 = gs.BFNames != null && gs.BFNames.Length > 0 ? GameRules.GetBattlefieldDisplayName(gs.BFNames[0]) : "战场1";
            string bf1 = gs.BFNames != null && gs.BFNames.Length > 1 ? GameRules.GetBattlefieldDisplayName(gs.BFNames[1]) : "战场2";

            if (_coinFlipText != null)
                _coinFlipText.text = $"掷硬币\n{label}\n\n战场：{bf0}  /  {bf1}";

            if (_coinFlipPanel != null)
                _coinFlipPanel.SetActive(true);

            if (_coinFlipOkButton != null)
            {
                // Disable button briefly so player can read the result before clicking
                _coinFlipOkButton.interactable = false;
                _coinFlipOkButton.onClick.RemoveAllListeners();
                _coinFlipOkButton.onClick.AddListener(() =>
                {
                    if (_coinFlipPanel != null) _coinFlipPanel.SetActive(false);
                    tcs.TrySetResult(true);
                });
                StartCoroutine(EnableButtonAfterDelay(_coinFlipOkButton, 1.2f));
            }
            else
            {
                // No button assigned — resolve immediately
                tcs.TrySetResult(true);
            }

            return tcs.Task;
        }

        // ── Mulligan ──────────────────────────────────────────────────────────

        private Task ShowMulligan(GameState gs)
        {
            var tcs = new TaskCompletionSource<bool>();

            _mulliganHand = new List<UnitInstance>(gs.PHand);
            _selectedForSwap.Clear();
            _mulliganCardViews.Clear();

            // Clear previous card views
            if (_mulliganCardContainer != null)
            {
                foreach (Transform child in _mulliganCardContainer)
                    Destroy(child.gameObject);
            }

            // Create a CardView per hand card
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

            if (_mulliganPanel != null)
                _mulliganPanel.SetActive(true);

            if (_mulliganConfirmButton != null)
            {
                _mulliganConfirmButton.onClick.RemoveAllListeners();
                _mulliganConfirmButton.onClick.AddListener(() =>
                {
                    PerformMulligan(gs);
                    if (_mulliganPanel != null) _mulliganPanel.SetActive(false);
                    tcs.TrySetResult(true);
                });
            }
            else
            {
                tcs.TrySetResult(true);
            }

            return tcs.Task;
        }

        private void OnMulliganCardClicked(UnitInstance unit)
        {
            if (_selectedForSwap.Contains(unit))
            {
                _selectedForSwap.Remove(unit);
            }
            else if (_selectedForSwap.Count < MAX_MULLIGAN_SWAPS)
            {
                _selectedForSwap.Add(unit);
            }
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

            // Highlight selected cards using SetSelected on CardView
            for (int i = 0; i < _mulliganCardViews.Count && i < _mulliganHand.Count; i++)
            {
                bool selected = _selectedForSwap.Contains(_mulliganHand[i]);
                _mulliganCardViews[i].SetSelected(selected);
            }
        }

        private IEnumerator EnableButtonAfterDelay(Button btn, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (btn != null) btn.interactable = true;
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

            // Draw replacements
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
