using System;
using System.Collections;
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

        // ── Legend panels ──────────────────────────────────────────────────────
        [SerializeField] private Text _playerLegendText;   // shows Kaisa name / level / exhausted
        [SerializeField] private Text _enemyLegendText;    // shows Masteryi name / exhausted
        [SerializeField] private Button _legendSkillBtn;   // 虚空感知 button

        // ── Score track circles (DEV-9) ──────────────────────────────────────
        [SerializeField] private Image[] _playerScoreCircles;
        [SerializeField] private Image[] _enemyScoreCircles;

        // ── Pile count texts (DEV-9) ─────────────────────────────────────────
        [SerializeField] private Text _playerDeckCountText;
        [SerializeField] private Text _enemyDeckCountText;
        [SerializeField] private Text _playerRunePileCountText;
        [SerializeField] private Text _enemyRunePileCountText;
        [SerializeField] private Text _playerDiscardCountText;
        [SerializeField] private Text _enemyDiscardCountText;
        [SerializeField] private Text _playerExileCountText;
        [SerializeField] private Text _enemyExileCountText;

        // ── BF control badges (DEV-9) ────────────────────────────────────────
        [SerializeField] private Image _bf1CtrlBadge;
        [SerializeField] private Image _bf2CtrlBadge;
        [SerializeField] private Text _bf1CtrlBadgeText;
        [SerializeField] private Text _bf2CtrlBadgeText;

        // ── Hero zones (DEV-9) ───────────────────────────────────────────────
        [SerializeField] private Transform _playerHeroContainer;
        [SerializeField] private Transform _enemyHeroContainer;

        // ── Legend zone containers (DEV-10: for art overlay) ─────────────────
        [SerializeField] private Transform _playerLegendContainer;
        [SerializeField] private Transform _enemyLegendContainer;

        // ── Log panel toggle (DEV-10) ────────────────────────────────────────
        [SerializeField] private GameObject _logPanel;
        [SerializeField] private Button _logToggleBtn;
        [SerializeField] private Text _logToggleText;
        [SerializeField] private RectTransform _boardWrapperOuter;
        [SerializeField] private RectTransform _playerHandZoneRT;
        [SerializeField] private RectTransform _enemyHandZoneRT;
        private bool _logCollapsed = false;
        private Coroutine _logAnimCoroutine;

        // ── Discard/Exile viewer (DEV-10) ────────────────────────────────────
        [SerializeField] private GameObject _viewerPanel;
        [SerializeField] private Text _viewerTitle;
        [SerializeField] private Transform _viewerCardContainer;
        [SerializeField] private Button _viewerCloseBtn;

        // ── Turn timer (DEV-10) ──────────────────────────────────────────────
        [SerializeField] private Image _timerFill;
        [SerializeField] private Text _timerText;
        [SerializeField] private GameObject _timerDisplay;
        private Coroutine _timerCoroutine;
        private int _timerSeconds;
        private Action _onTimerExpired;

        // ── Action buttons (DEV-9) ───────────────────────────────────────────
        [SerializeField] private Button _tapAllRunesBtn;
        [SerializeField] private Button _cancelRunesBtn;
        [SerializeField] private Button _confirmRunesBtn;
        [SerializeField] private Button _skipReactionBtn;

        // ── Info strip texts (DEV-9) ─────────────────────────────────────────
        [SerializeField] private Text _playerRuneInfoText;
        [SerializeField] private Text _enemyRuneInfoText;
        [SerializeField] private Text _playerDeckInfoText;
        [SerializeField] private Text _enemyDeckInfoText;

        // ── Banner overlay ────────────────────────────────────────────────────
        [SerializeField] private GameObject _bannerPanel;
        [SerializeField] private Text _bannerText;
        private const float BANNER_DURATION = 1.8f;

        // ── Card detail popup ─────────────────────────────────────────────────
        [SerializeField] private CardDetailPopup _cardDetailPopup;

        // ── Callbacks set by GameManager ──────────────────────────────────────
        private Action _onEndTurnClicked;
        private Action<int> _onBFClicked;
        private Action<UnitInstance> _onUnitClicked;
        private Action<int, bool> _onRuneClicked; // (runeIdx, recycle)
        private Action<UnitInstance> _onCardRightClicked;

        // ── Message log state ─────────────────────────────────────────────────
        private const int MAX_MESSAGES = 5;
        private readonly Queue<Text> _messageTexts = new Queue<Text>();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_bannerPanel != null) _bannerPanel.SetActive(false);
            if (_endTurnButton != null) _endTurnButton.onClick.AddListener(HandleEndTurn);
            if (_bf1Button != null) _bf1Button.onClick.AddListener(() => _onBFClicked?.Invoke(0));
            if (_bf2Button != null) _bf2Button.onClick.AddListener(() => _onBFClicked?.Invoke(1));
            if (_restartButton != null) _restartButton.onClick.AddListener(HandleRestart);
            if (_logToggleBtn != null) _logToggleBtn.onClick.AddListener(ToggleLog);
            if (_viewerCloseBtn != null) _viewerCloseBtn.onClick.AddListener(CloseViewer);
            if (_viewerPanel != null) _viewerPanel.SetActive(false);
            if (_timerDisplay != null) _timerDisplay.SetActive(false);
            FWTCG.Systems.TurnManager.OnBannerRequest += ShowBanner;
        }

        private void OnDestroy()
        {
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveAllListeners();
            if (_bf1Button != null) _bf1Button.onClick.RemoveAllListeners();
            if (_bf2Button != null) _bf2Button.onClick.RemoveAllListeners();
            if (_restartButton != null) _restartButton.onClick.RemoveAllListeners();
            FWTCG.Systems.TurnManager.OnBannerRequest -= ShowBanner;
        }

        public void ShowBanner(string text)
        {
            if (_bannerPanel == null) return;
            if (_bannerText != null) _bannerText.text = text;
            _bannerPanel.SetActive(true);
            StopCoroutine(nameof(HideBannerCoroutine));
            StartCoroutine(nameof(HideBannerCoroutine));
        }

        private IEnumerator HideBannerCoroutine()
        {
            yield return new WaitForSeconds(BANNER_DURATION);
            if (_bannerPanel != null) _bannerPanel.SetActive(false);
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        public void SetCallbacks(Action onEndTurn, Action<int> onBF,
                                 Action<UnitInstance> onUnit, Action<int, bool> onRune,
                                 Action<UnitInstance> onCardRightClick = null)
        {
            _onEndTurnClicked = onEndTurn;
            _onBFClicked = onBF;
            _onUnitClicked = onUnit;
            _onRuneClicked = onRune;
            _onCardRightClicked = onCardRightClick;
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
            RefreshLegends(gs);
            RefreshHeroZones(gs);
            RefreshScoreTrack(gs);
            RefreshPileCounts(gs);
            RefreshBFControlBadges(gs);
            RefreshInfoStrips(gs);
            RefreshActionButtons(gs.Phase, gs.Turn);
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
            // Player hand (with cost-insufficient dimming)
            RefreshUnitList(_playerHandContainer, gs.PHand, true, _onUnitClicked, gs.PMana);
            // Enemy hand (face-down — show count only)
            RefreshEnemyHand(_enemyHandContainer, gs.EHand.Count);
        }

        private void RefreshBases(GameState gs)
        {
            RefreshUnitList(_playerBaseContainer, gs.PBase, true, _onUnitClicked);
            RefreshUnitList(_enemyBaseContainer, gs.EBase, false, _onUnitClicked);
        }

        private void RefreshBattlefields(GameState gs)
        {
            RefreshUnitList(_bf1PlayerContainer, gs.BF[0].PlayerUnits, true, _onUnitClicked);
            RefreshUnitList(_bf1EnemyContainer, gs.BF[0].EnemyUnits, false, _onUnitClicked);
            RefreshUnitList(_bf2PlayerContainer, gs.BF[1].PlayerUnits, true, _onUnitClicked);
            RefreshUnitList(_bf2EnemyContainer, gs.BF[1].EnemyUnits, false, _onUnitClicked);

            if (_bf1CtrlText != null)
            {
                string bf1Name = gs.BFNames != null && gs.BFNames.Length > 0 ? GameRules.GetBattlefieldDisplayName(gs.BFNames[0]) : "战场1";
                _bf1CtrlText.text = $"{bf1Name}\n{CtrlLabel(gs.BF[0].Ctrl)}";
            }
            if (_bf2CtrlText != null)
            {
                string bf2Name = gs.BFNames != null && gs.BFNames.Length > 1 ? GameRules.GetBattlefieldDisplayName(gs.BFNames[1]) : "战场2";
                _bf2CtrlText.text = $"{bf2Name}\n{CtrlLabel(gs.BF[1].Ctrl)}";
            }

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

        // ── Legend zone refresh ───────────────────────────────────────────────

        private void RefreshLegends(GameState gs)
        {
            // Player legend (Kaisa)
            if (_playerLegendText != null)
            {
                if (gs.PLegend != null)
                {
                    string lvl = gs.PLegend.Level >= 2 ? " [Lv.2]" : "";
                    string ex  = gs.PLegend.Exhausted ? " [休眠]" : "";
                    _playerLegendText.text = $"{gs.PLegend.Name}{lvl}{ex}";
                }
                else
                {
                    _playerLegendText.text = "传奇: -";
                }
            }

            // Skill button: usable while legend is not exhausted (虚空感知 costs exhausting self)
            if (_legendSkillBtn != null)
            {
                bool canUse = gs.PLegend != null
                              && !gs.PLegend.Exhausted
                              && !gs.PLegend.AbilityUsedThisTurn
                              && !gs.GameOver;
                _legendSkillBtn.interactable = canUse;
            }

            // Enemy legend (Masteryi)
            if (_enemyLegendText != null)
            {
                if (gs.ELegend != null)
                {
                    string eLvl = gs.ELegend.Level >= 2 ? " [Lv.2]" : "";
                    _enemyLegendText.text = $"{gs.ELegend.Name}{eLvl}";
                }
                else
                    _enemyLegendText.text = "传奇: -";
            }

            // Render legend art (DEV-10)
            RefreshLegendArt(_playerLegendContainer, gs.PLegend);
            RefreshLegendArt(_enemyLegendContainer, gs.ELegend);
        }

        // ── Hero zone refresh (DEV-10) ──────────────────────────────────────

        private void RefreshHeroZones(GameState gs)
        {
            RefreshSingleHeroZone(_playerHeroContainer, gs.PHero, true);
            RefreshSingleHeroZone(_enemyHeroContainer, gs.EHero, false);
        }

        private void RefreshSingleHeroZone(Transform container, UnitInstance hero, bool isPlayer)
        {
            if (container == null) return;

            // Clear existing children
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);

            if (hero != null)
            {
                if (_cardViewPrefab != null)
                {
                    GameObject go = Instantiate(_cardViewPrefab, container);
                    // Stretch to fill the hero slot area
                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }

                    CardView cv = go.GetComponent<CardView>();
                    if (cv != null)
                        cv.Setup(hero, isPlayer, isPlayer ? _onUnitClicked : null, _onCardRightClicked);
                }
            }
            else
            {
                GameObject ph = new GameObject("HeroPlaceholder");
                ph.transform.SetParent(container, false);
                var rt = ph.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var txt = ph.AddComponent<Text>();
                txt.text = "已出场";
                txt.font = _playerScoreText != null ? _playerScoreText.font : Font.CreateDynamicFontFromOSFont("Arial", 12);
                txt.fontSize = 11;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = new Color(1f, 1f, 1f, 0.5f);
            }
        }

        // ── Legend zone refresh with art (DEV-10) ─────────────────────────────

        private void RefreshLegendArt(Transform legendContainer, LegendInstance legend)
        {
            if (legendContainer == null || legend == null) return;

            // Find or create the art image
            Transform artTransform = legendContainer.Find("LegendArt");
            if (legend.DisplayData != null && legend.DisplayData.ArtSprite != null)
            {
                Image artImg;
                if (artTransform == null)
                {
                    var artGO = new GameObject("LegendArt");
                    artGO.transform.SetParent(legendContainer, false);
                    artGO.transform.SetAsFirstSibling();
                    var rt = artGO.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    // Ignore parent VerticalLayoutGroup — this is a background overlay
                    var le = artGO.AddComponent<LayoutElement>();
                    le.ignoreLayout = true;
                    artImg = artGO.AddComponent<Image>();
                    artImg.preserveAspect = true;
                    artImg.raycastTarget = false;
                    artImg.color = new Color(1f, 1f, 1f, 0.4f);
                }
                else
                {
                    artImg = artTransform.GetComponent<Image>();
                }

                if (artImg != null)
                    artImg.sprite = legend.DisplayData.ArtSprite;
            }
        }

        // ── Unit list renderer ────────────────────────────────────────────────

        private void RefreshUnitList(Transform container, List<UnitInstance> units,
                                     bool isPlayer, Action<UnitInstance> onClick,
                                     int currentMana = -1)
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
                    cv.Setup(u, isPlayer, onClick, _onCardRightClicked);
                    // Highlight multi-selected base units
                    if (_selectedBaseUnits != null && _selectedBaseUnits.Contains(u))
                        cv.SetSelected(true);
                    // Dim hand cards that can't be afforded
                    if (currentMana >= 0 && u.CardData.Cost > currentMana)
                        cv.SetCostInsufficient(true);
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
                        cv.SetFaceDown(true);
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
                    img.color = r.Tapped ? GameColors.RuneTapped : Color.white;
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

        // ── Score track refresh (DEV-9) ──────────────────────────────────────

        public void RefreshScoreTrack(GameState gs)
        {
            if (_playerScoreCircles != null)
            {
                for (int i = 0; i < _playerScoreCircles.Length; i++)
                {
                    if (_playerScoreCircles[i] == null) continue;
                    if (i < gs.PScore)
                        _playerScoreCircles[i].color = GameColors.ScoreCirclePlayer;
                    else if (i == gs.PScore && gs.PScore < GameRules.WIN_SCORE)
                        _playerScoreCircles[i].color = GameColors.ScoreCircleCurrent;
                    else
                        _playerScoreCircles[i].color = GameColors.ScoreCircleInactive;
                }
            }
            if (_enemyScoreCircles != null)
            {
                for (int i = 0; i < _enemyScoreCircles.Length; i++)
                {
                    if (_enemyScoreCircles[i] == null) continue;
                    if (i < gs.EScore)
                        _enemyScoreCircles[i].color = GameColors.ScoreCircleEnemy;
                    else if (i == gs.EScore && gs.EScore < GameRules.WIN_SCORE)
                        _enemyScoreCircles[i].color = GameColors.ScoreCircleCurrent;
                    else
                        _enemyScoreCircles[i].color = GameColors.ScoreCircleInactive;
                }
            }
        }

        // ── Pile count refresh (DEV-9) ───────────────────────────────────────

        public void RefreshPileCounts(GameState gs)
        {
            if (_playerDeckCountText != null)
                _playerDeckCountText.text = gs.PDeck.Count.ToString();
            if (_enemyDeckCountText != null)
                _enemyDeckCountText.text = gs.EDeck.Count.ToString();
            if (_playerRunePileCountText != null)
                _playerRunePileCountText.text = gs.PRuneDeck.Count.ToString();
            if (_enemyRunePileCountText != null)
                _enemyRunePileCountText.text = gs.ERuneDeck.Count.ToString();
            if (_playerDiscardCountText != null)
                _playerDiscardCountText.text = gs.PDiscard.Count.ToString();
            if (_enemyDiscardCountText != null)
                _enemyDiscardCountText.text = gs.EDiscard.Count.ToString();
            if (_playerExileCountText != null)
                _playerExileCountText.text = gs.PExile.Count.ToString();
            if (_enemyExileCountText != null)
                _enemyExileCountText.text = gs.EExile.Count.ToString();
        }

        // ── BF control badge refresh (DEV-9) ────────────────────────────────

        public void RefreshBFControlBadges(GameState gs)
        {
            if (gs.BF == null) return;

            if (_bf1CtrlBadge != null && gs.BF.Length > 0)
            {
                bool isPlayer = gs.BF[0].Ctrl == GameRules.OWNER_PLAYER;
                bool isEnemy  = gs.BF[0].Ctrl == GameRules.OWNER_ENEMY;
                _bf1CtrlBadge.color = isPlayer ? GameColors.CtrlBadgePlayer
                                    : isEnemy  ? GameColors.CtrlBadgeEnemy
                                    : GameColors.ScoreCircleInactive;
            }
            if (_bf1CtrlBadgeText != null && gs.BF.Length > 0)
            {
                _bf1CtrlBadgeText.text = gs.BF[0].Ctrl == GameRules.OWNER_PLAYER ? "玩"
                                       : gs.BF[0].Ctrl == GameRules.OWNER_ENEMY  ? "敌"
                                       : "—";
            }
            if (_bf2CtrlBadge != null && gs.BF.Length > 1)
            {
                bool isPlayer = gs.BF[1].Ctrl == GameRules.OWNER_PLAYER;
                bool isEnemy  = gs.BF[1].Ctrl == GameRules.OWNER_ENEMY;
                _bf2CtrlBadge.color = isPlayer ? GameColors.CtrlBadgePlayer
                                    : isEnemy  ? GameColors.CtrlBadgeEnemy
                                    : GameColors.ScoreCircleInactive;
            }
            if (_bf2CtrlBadgeText != null && gs.BF.Length > 1)
            {
                _bf2CtrlBadgeText.text = gs.BF[1].Ctrl == GameRules.OWNER_PLAYER ? "玩"
                                        : gs.BF[1].Ctrl == GameRules.OWNER_ENEMY  ? "敌"
                                        : "—";
            }
        }

        // ── Info strip refresh (DEV-9) ───────────────────────────────────────

        public void RefreshInfoStrips(GameState gs)
        {
            if (_playerRuneInfoText != null)
                _playerRuneInfoText.text = $"{gs.PRunes.Count}/{gs.PRunes.Count + gs.PRuneDeck.Count}";
            if (_enemyRuneInfoText != null)
                _enemyRuneInfoText.text = $"{gs.ERunes.Count}/{gs.ERunes.Count + gs.ERuneDeck.Count}";
            if (_playerDeckInfoText != null)
                _playerDeckInfoText.text = $"牌库:{gs.PDeck.Count}";
            if (_enemyDeckInfoText != null)
                _enemyDeckInfoText.text = $"牌库:{gs.EDeck.Count}";
        }

        // ── Action button refresh (DEV-9) ────────────────────────────────────

        public void RefreshActionButtons(string phase, string turn)
        {
            bool isPlayerAction = turn == GameRules.OWNER_PLAYER
                                  && phase == GameRules.PHASE_ACTION;

            if (_tapAllRunesBtn != null)
                _tapAllRunesBtn.interactable = isPlayerAction;
            if (_cancelRunesBtn != null)
                _cancelRunesBtn.interactable = isPlayerAction;
            if (_confirmRunesBtn != null)
                _confirmRunesBtn.interactable = isPlayerAction;
            if (_skipReactionBtn != null)
                _skipReactionBtn.interactable = isPlayerAction;
        }

        // ── Log panel toggle (DEV-10) ────────────────────────────────────────

        public void ToggleLog()
        {
            _logCollapsed = !_logCollapsed;

            if (_logToggleText != null)
                _logToggleText.text = _logCollapsed ? ">" : "<";

            if (_logAnimCoroutine != null) StopCoroutine(_logAnimCoroutine);
            _logAnimCoroutine = StartCoroutine(AnimateLogToggle(_logCollapsed));
        }

        private IEnumerator AnimateLogToggle(bool collapse)
        {
            float duration = 0.3f;
            float elapsed = 0f;

            // Right offset: -200 (log open) → 0 (log collapsed)
            float startOffset = collapse ? -200f : 0f;
            float endOffset = collapse ? 0f : -200f;

            // Reactivate log panel at start of expand animation
            if (!collapse && _logPanel != null)
                _logPanel.SetActive(true);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                float offset = Mathf.Lerp(startOffset, endOffset, t);

                // Move all areas that have -200 right margin for log
                if (_boardWrapperOuter != null)
                    _boardWrapperOuter.offsetMax = new Vector2(offset, _boardWrapperOuter.offsetMax.y);
                if (_playerHandZoneRT != null)
                    _playerHandZoneRT.offsetMax = new Vector2(offset, _playerHandZoneRT.offsetMax.y);
                if (_enemyHandZoneRT != null)
                    _enemyHandZoneRT.offsetMax = new Vector2(offset, _enemyHandZoneRT.offsetMax.y);

                yield return null;
            }

            // Final: hide log panel when collapsed
            if (collapse && _logPanel != null)
                _logPanel.SetActive(false);
        }

        // ── Discard / Exile viewer (DEV-10) ──────────────────────────────────

        public void ShowDiscardViewer(List<UnitInstance> cards, string title = "弃牌堆")
        {
            ShowViewer(cards, title);
        }

        public void ShowExileViewer(List<UnitInstance> cards, string title = "放逐堆")
        {
            ShowViewer(cards, title);
        }

        private void ShowViewer(List<UnitInstance> cards, string title)
        {
            if (_viewerPanel == null) return;

            _viewerPanel.SetActive(true);
            if (_viewerTitle != null)
                _viewerTitle.text = $"{title} ({(cards != null ? cards.Count : 0)})";

            // Clear existing children
            if (_viewerCardContainer != null)
            {
                for (int i = _viewerCardContainer.childCount - 1; i >= 0; i--)
                    Destroy(_viewerCardContainer.GetChild(i).gameObject);

                if (cards != null && _cardViewPrefab != null)
                {
                    // Show in reverse order (most recent first)
                    for (int i = cards.Count - 1; i >= 0; i--)
                    {
                        GameObject go = Instantiate(_cardViewPrefab, _viewerCardContainer);
                        CardView cv = go.GetComponent<CardView>();
                        if (cv != null)
                            cv.Setup(cards[i], true, null, _onCardRightClicked);
                    }
                }
            }
        }

        private void CloseViewer()
        {
            if (_viewerPanel != null) _viewerPanel.SetActive(false);
        }

        // ── Turn timer (DEV-10) ──────────────────────────────────────────────

        public void StartTurnTimer(Action onExpired)
        {
            _onTimerExpired = onExpired;
            _timerSeconds = 30;

            if (_timerDisplay != null) _timerDisplay.SetActive(true);
            UpdateTimerDisplay();

            if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
            _timerCoroutine = StartCoroutine(TimerCountdown());
        }

        public void ClearTurnTimer()
        {
            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }
            if (_timerDisplay != null) _timerDisplay.SetActive(false);
            _onTimerExpired = null;
        }

        private IEnumerator TimerCountdown()
        {
            while (_timerSeconds > 0)
            {
                yield return new WaitForSeconds(1f);
                _timerSeconds--;
                UpdateTimerDisplay();
            }

            // Timer expired
            _timerCoroutine = null;
            if (_timerDisplay != null) _timerDisplay.SetActive(false);
            _onTimerExpired?.Invoke();
        }

        private void UpdateTimerDisplay()
        {
            if (_timerText != null)
                _timerText.text = _timerSeconds.ToString();

            if (_timerFill != null)
            {
                float pct = _timerSeconds / 30f;
                _timerFill.fillAmount = pct;

                // Color: green > 15s, yellow 5-15s, red < 5s
                if (_timerSeconds > 15)
                    _timerFill.color = GameColors.ScoreCirclePlayer; // green
                else if (_timerSeconds > 5)
                    _timerFill.color = new Color(1f, 0.85f, 0.3f, 1f); // yellow
                else
                    _timerFill.color = GameColors.ScoreCircleEnemy; // red
            }
        }

        // ── Discard/Exile click setup (DEV-10) ──────────────────────────────

        private Action<string, string> _onPileClicked; // (owner, pileType)

        public void SetPileClickCallback(Action<string, string> callback)
        {
            _onPileClicked = callback;
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
