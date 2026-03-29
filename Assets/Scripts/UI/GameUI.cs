using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.UI
{
    /// <summary>
    /// Main UI controller for FWTCG DEV-1.
    /// Redraws all panels when Refresh() is called.
    /// Functional, no animation — pure information display.
    ///
    /// Layout (1920×1080):
    ///   Top: Enemy score | Runes | Hand
    ///   Middle: BF1 | BF2 (two battlefield zones)
    ///   Bottom: Player Hand | Runes | Base | Mana/Sch | End Turn button
    ///   Left sidebar: Phase/Round info + Message log
    ///   Overlay: GameOver panel
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        // ── Score / header ────────────────────────────────────────────────────
        [SerializeField] private Text _playerScoreText;
        [SerializeField] private Text _enemyScoreText;
        [SerializeField] private Text _roundPhaseText;

        // ── Mana / energy ─────────────────────────────────────────────────────
        [SerializeField] private Text _playerManaText;
        [SerializeField] private Text _enemyManaText;
        [SerializeField] private Text _playerSchText;
        [SerializeField] private Text _enemySchText;

        // ── Hand areas ────────────────────────────────────────────────────────
        [SerializeField] private Transform _playerHandContainer;
        [SerializeField] private Transform _enemyHandContainer;
        [SerializeField] private GameObject _cardViewPrefab;

        // ── Base areas ────────────────────────────────────────────────────────
        [SerializeField] private Transform _playerBaseContainer;
        [SerializeField] private Transform _enemyBaseContainer;

        // ── Battlefield areas ─────────────────────────────────────────────────
        [SerializeField] private Transform _bf1PlayerContainer;
        [SerializeField] private Transform _bf1EnemyContainer;
        [SerializeField] private Transform _bf2PlayerContainer;
        [SerializeField] private Transform _bf2EnemyContainer;
        [SerializeField] private Text _bf1CtrlText;
        [SerializeField] private Text _bf2CtrlText;
        [SerializeField] private Button _bf1Button;
        [SerializeField] private Button _bf2Button;

        // ── Rune area ─────────────────────────────────────────────────────────
        [SerializeField] private Transform _playerRuneContainer;
        [SerializeField] private Transform _enemyRuneContainer;
        [SerializeField] private GameObject _runeButtonPrefab;

        // ── Controls ──────────────────────────────────────────────────────────
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private Text _endTurnLabel;

        // ── Message log ───────────────────────────────────────────────────────
        [SerializeField] private Transform _messageContainer;
        [SerializeField] private Text _messageTextPrefab;

        // ── Game over overlay ─────────────────────────────────────────────────
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private Text _gameOverText;
        [SerializeField] private Button _restartButton;

        // ── Callbacks set by GameManager ──────────────────────────────────────
        private Action _onEndTurnClicked;
        private Action<int> _onBFClicked;
        private Action<UnitInstance> _onUnitClicked;
        private Action<int, bool> _onRuneClicked; // (runeIdx, recycle)

        // ── Message log state ─────────────────────────────────────────────────
        private const int MAX_MESSAGES = 5;
        private readonly Queue<Text> _messageTexts = new Queue<Text>();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_endTurnButton != null) _endTurnButton.onClick.AddListener(HandleEndTurn);
            if (_bf1Button != null) _bf1Button.onClick.AddListener(() => _onBFClicked?.Invoke(0));
            if (_bf2Button != null) _bf2Button.onClick.AddListener(() => _onBFClicked?.Invoke(1));
            if (_restartButton != null) _restartButton.onClick.AddListener(HandleRestart);
        }

        private void OnDestroy()
        {
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveAllListeners();
            if (_bf1Button != null) _bf1Button.onClick.RemoveAllListeners();
            if (_bf2Button != null) _bf2Button.onClick.RemoveAllListeners();
            if (_restartButton != null) _restartButton.onClick.RemoveAllListeners();
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        public void SetCallbacks(Action onEndTurn, Action<int> onBF,
                                 Action<UnitInstance> onUnit, Action<int, bool> onRune)
        {
            _onEndTurnClicked = onEndTurn;
            _onBFClicked = onBF;
            _onUnitClicked = onUnit;
            _onRuneClicked = onRune;
        }

        // ── Selection state (set by GameManager) ─────────────────────────────
        private List<UnitInstance> _selectedBaseUnits;

        // ── Full refresh ──────────────────────────────────────────────────────

        /// <summary>
        /// Redraws all UI panels to match current game state.
        /// </summary>
        public void Refresh(GameState gs)
        {
            Refresh(gs, null);
        }

        /// <summary>
        /// Redraws all UI panels. selectedBaseUnits highlights multi-selected units in green.
        /// </summary>
        public void Refresh(GameState gs, List<UnitInstance> selectedBaseUnits)
        {
            if (gs == null) return;

            _selectedBaseUnits = selectedBaseUnits;

            RefreshScores(gs);
            RefreshRoundPhase(gs);
            RefreshMana(gs);
            RefreshHands(gs);
            RefreshBases(gs);
            RefreshBattlefields(gs);
            RefreshRunes(gs);
            RefreshEndTurnButton(gs);
        }

        // ── Individual panel refresh ──────────────────────────────────────────

        private void RefreshScores(GameState gs)
        {
            if (_playerScoreText != null)
                _playerScoreText.text = $"玩家  {gs.PScore} / {GameRules.WIN_SCORE}";
            if (_enemyScoreText != null)
                _enemyScoreText.text = $"AI  {gs.EScore} / {GameRules.WIN_SCORE}";
        }

        private void RefreshRoundPhase(GameState gs)
        {
            if (_roundPhaseText != null)
                _roundPhaseText.text = $"回合 {gs.Round + 1}  [{gs.Turn?.ToUpper() ?? "—"}]  {gs.Phase?.ToUpper() ?? "—"}";
        }

        private void RefreshMana(GameState gs)
        {
            if (_playerManaText != null)
                _playerManaText.text = $"法力: {gs.PMana}";
            if (_enemyManaText != null)
                _enemyManaText.text = $"法力: {gs.EMana}";
            if (_playerSchText != null)
                _playerSchText.text = BuildSchText(gs.PSch);
            if (_enemySchText != null)
                _enemySchText.text = BuildSchText(gs.ESch);
        }

        private string BuildSchText(Dictionary<RuneType, int> sch)
        {
            var sb = new System.Text.StringBuilder("符能: ");
            bool any = false;
            foreach (var kv in sch)
            {
                if (kv.Value > 0)
                {
                    if (any) sb.Append(" | ");
                    sb.Append($"{RuneTypeShortName(kv.Key)}×{kv.Value}");
                    any = true;
                }
            }
            if (!any) sb.Append("—");
            return sb.ToString();
        }

        private void RefreshHands(GameState gs)
        {
            // Player hand
            RefreshUnitList(_playerHandContainer, gs.PHand, true, _onUnitClicked);
            // Enemy hand (face-down — show count only)
            RefreshEnemyHand(_enemyHandContainer, gs.EHand.Count);
        }

        private void RefreshBases(GameState gs)
        {
            RefreshUnitList(_playerBaseContainer, gs.PBase, true, _onUnitClicked);
            RefreshUnitList(_enemyBaseContainer, gs.EBase, false, null);
        }

        private void RefreshBattlefields(GameState gs)
        {
            RefreshUnitList(_bf1PlayerContainer, gs.BF[0].PlayerUnits, true, _onUnitClicked);
            RefreshUnitList(_bf1EnemyContainer, gs.BF[0].EnemyUnits, false, null);
            RefreshUnitList(_bf2PlayerContainer, gs.BF[1].PlayerUnits, true, _onUnitClicked);
            RefreshUnitList(_bf2EnemyContainer, gs.BF[1].EnemyUnits, false, null);

            if (_bf1CtrlText != null)
                _bf1CtrlText.text = CtrlLabel(gs.BF[0].Ctrl);
            if (_bf2CtrlText != null)
                _bf2CtrlText.text = CtrlLabel(gs.BF[1].Ctrl);

        }

        private void RefreshRunes(GameState gs)
        {
            RefreshRuneZone(_playerRuneContainer, gs.PRunes, true);
            RefreshRuneZone(_enemyRuneContainer, gs.ERunes, false);
        }

        private void RefreshEndTurnButton(GameState gs)
        {
            bool isPlayerTurn = gs.Turn == GameRules.OWNER_PLAYER
                                && gs.Phase == GameRules.PHASE_ACTION;
            if (_endTurnButton != null)
                _endTurnButton.interactable = isPlayerTurn && !gs.GameOver;
            if (_endTurnLabel != null)
                _endTurnLabel.text = isPlayerTurn ? "结束回合" : "等待中…";
        }

        // ── Unit list renderer ────────────────────────────────────────────────

        private void RefreshUnitList(Transform container, List<UnitInstance> units,
                                     bool isPlayer, Action<UnitInstance> onClick)
        {
            if (container == null) return;

            // Clear existing children
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);

            if (units == null || _cardViewPrefab == null) return;

            foreach (UnitInstance u in units)
            {
                GameObject go = Instantiate(_cardViewPrefab, container);
                CardView cv = go.GetComponent<CardView>();
                if (cv != null)
                {
                    cv.Setup(u, isPlayer, onClick);
                    // Highlight multi-selected base units
                    if (_selectedBaseUnits != null && _selectedBaseUnits.Contains(u))
                        cv.SetSelected(true);
                }
            }
        }

        private void RefreshEnemyHand(Transform container, int count)
        {
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);

            // Show placeholder labels for enemy hand cards (face-down)
            for (int i = 0; i < count; i++)
            {
                if (_cardViewPrefab != null)
                {
                    GameObject go = Instantiate(_cardViewPrefab, container);
                    // Disable interaction — just show face-down placeholder
                    CardView cv = go.GetComponent<CardView>();
                    if (cv != null)
                    {
                        // We don't call Setup with real data — leave blank (face-down)
                        go.name = $"EnemyCard_{i}";
                    }
                }
            }
        }

        // ── Rune zone renderer ────────────────────────────────────────────────

        private void RefreshRuneZone(Transform container, List<RuneInstance> runes, bool isPlayer)
        {
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);

            if (runes == null || _runeButtonPrefab == null) return;

            for (int i = 0; i < runes.Count; i++)
            {
                int idx = i; // capture for lambda
                RuneInstance r = runes[i];

                GameObject go = Instantiate(_runeButtonPrefab, container);
                go.name = $"Rune_{r.RuneType}_{i}";

                Text label = go.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"{RuneTypeShortName(r.RuneType)}\n{(r.Tapped ? "[横置]" : "[就绪]")}";

                // Left-click: tap (mana)
                Button btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = isPlayer && !r.Tapped;
                    btn.onClick.AddListener(() => _onRuneClicked?.Invoke(idx, false));
                }

                // Right-click: recycle (implemented via separate button or context menu)
                // For DEV-1 we add a small "回收" sub-button if present
                Button recycleBtn = FindChildButton(go, "RecycleButton");
                if (recycleBtn != null)
                {
                    recycleBtn.interactable = isPlayer;
                    recycleBtn.onClick.AddListener(() => _onRuneClicked?.Invoke(idx, true));
                }

                // Visual tint for tapped state
                Image img = go.GetComponent<Image>();
                if (img != null)
                    img.color = r.Tapped ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
            }
        }

        // ── Message log ───────────────────────────────────────────────────────

        /// <summary>
        /// Adds a message to the scrolling message log (max 5 entries).
        /// </summary>
        public void ShowMessage(string msg)
        {
            if (_messageContainer == null) return;

            Text entry;
            if (_messageTexts.Count >= MAX_MESSAGES)
            {
                // Reuse oldest entry
                entry = _messageTexts.Dequeue();
                entry.transform.SetAsLastSibling();
            }
            else
            {
                if (_messageTextPrefab != null)
                {
                    entry = Instantiate(_messageTextPrefab, _messageContainer);
                    entry.text = "";
                }
                else
                {
                    return; // No prefab — skip
                }
            }

            entry.text = msg;
            _messageTexts.Enqueue(entry);
        }

        // ── Game over overlay ─────────────────────────────────────────────────

        public void ShowGameOver(string msg)
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(true);
            if (_gameOverText != null) _gameOverText.text = msg;
            if (_endTurnButton != null) _endTurnButton.interactable = false;
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void HandleEndTurn()
        {
            _onEndTurnClicked?.Invoke();
        }

        private void HandleRestart()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        // ── Utility ───────────────────────────────────────────────────────────

        private string CtrlLabel(string ctrl)
        {
            if (ctrl == GameRules.OWNER_PLAYER) return "玩家控制";
            if (ctrl == GameRules.OWNER_ENEMY) return "AI控制";
            return "无人控制";
        }

        private string RuneTypeShortName(RuneType rt)
        {
            switch (rt)
            {
                case RuneType.Blazing: return "炽";
                case RuneType.Radiant: return "灵";
                case RuneType.Verdant: return "翠";
                case RuneType.Crushing: return "摧";
                case RuneType.Chaos: return "混";
                case RuneType.Order: return "序";
                default: return rt.ToString();
            }
        }

        private Button FindChildButton(GameObject parent, string name)
        {
            Transform t = parent.transform.Find(name);
            if (t != null) return t.GetComponent<Button>();
            return null;
        }
    }
}
