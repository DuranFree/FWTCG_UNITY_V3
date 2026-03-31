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
        private bool _logCollapsed = true; // default collapsed
        private Coroutine _logAnimCoroutine;

        // ── Debug panel toggle (DEV-10) ──────────────────────────────────────
        [SerializeField] private GameObject _debugPanel;
        [SerializeField] private Button _debugToggleBtn;
        private bool _debugCollapsed = true;

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
            if (_debugToggleBtn != null) _debugToggleBtn.onClick.AddListener(ToggleDebug);

            // Default: log collapsed, debug collapsed
            if (_logPanel != null) _logPanel.SetActive(false);
            if (_logToggleText != null) _logToggleText.text = ">";
            // Set board/hand to full width (no log margin)
            if (_boardWrapperOuter != null)
                _boardWrapperOuter.offsetMax = new Vector2(0f, _boardWrapperOuter.offsetMax.y);
            if (_playerHandZoneRT != null)
                _playerHandZoneRT.offsetMax = new Vector2(0f, _playerHandZoneRT.offsetMax.y);
            if (_enemyHandZoneRT != null)
                _enemyHandZoneRT.offsetMax = new Vector2(0f, _enemyHandZoneRT.offsetMax.y);
            // Move log toggle button to collapsed position (right edge)
            if (_logToggleBtn != null)
            {
                var btnRT = _logToggleBtn.GetComponent<RectTransform>();
                if (btnRT != null) btnRT.anchoredPosition = new Vector2(-4f, btnRT.anchoredPosition.y);
            }

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

            // Load battlefield card art (landscape images)
            if (gs.BFNames != null)
            {
                LoadBFCardArt(_bf1PlayerContainer, gs.BFNames.Length > 0 ? gs.BFNames[0] : null);
                LoadBFCardArt(_bf2PlayerContainer, gs.BFNames.Length > 1 ? gs.BFNames[1] : null);
            }

            // Wire right-click on BF art slots for detail popup
            WireBFRightClick(_bf1PlayerContainer, gs.BFNames != null && gs.BFNames.Length > 0 ? gs.BFNames[0] : null);
            WireBFRightClick(_bf2PlayerContainer, gs.BFNames != null && gs.BFNames.Length > 1 ? gs.BFNames[1] : null);
        }

        private void WireBFRightClick(Transform bfContainer, string bfId)
        {
            if (bfContainer == null || string.IsNullOrEmpty(bfId) || _cardDetailPopup == null) return;
            Transform panel = bfContainer.parent;
            if (panel == null) return;

            // Only wire once
            if (panel.GetComponent<UnityEngine.EventSystems.EventTrigger>() != null) return;

            string bfName = GameRules.GetBattlefieldDisplayName(bfId);
            string capturedId = bfId;

            var et = panel.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
            entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
            entry.callback.AddListener((data) =>
            {
                var pd = (UnityEngine.EventSystems.PointerEventData)data;
                if (pd.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                {
                    string desc = GameRules.GetBattlefieldDescription(capturedId);
                    Sprite sprite = null;
                    if (_bfArtCache.TryGetValue(capturedId, out Sprite cached))
                        sprite = cached;
                    else
                        sprite = Resources.Load<Sprite>($"CardArt/bf_{capturedId}");
                    _cardDetailPopup.ShowSimple(bfName, desc ?? "战场卡", sprite);
                }
            });
            et.triggers.Add(entry);
        }

        private static readonly Dictionary<string, Sprite> _bfArtCache = new Dictionary<string, Sprite>();

        private void LoadBFCardArt(Transform bfPlayerContainer, string bfId)
        {
            if (bfPlayerContainer == null || string.IsNullOrEmpty(bfId)) return;

            // BFCardArt is inside LabelRow inside the BF panel
            Transform panel = bfPlayerContainer.parent;
            if (panel == null) return;
            Transform labelRow = panel.Find("LabelRow");
            Transform artSlot = labelRow != null ? labelRow.Find("BFCardArt") : panel.Find("BFCardArt");
            if (artSlot == null) return;

            Image artImg = artSlot.GetComponent<Image>();
            if (artImg == null) return;

            // Load from cache or Resources
            if (!_bfArtCache.TryGetValue(bfId, out Sprite sprite) || sprite == null)
            {
                sprite = Resources.Load<Sprite>($"CardArt/bf_{bfId}");
                if (sprite != null) _bfArtCache[bfId] = sprite;
            }

            if (sprite != null)
            {
                artImg.sprite = sprite;
                artImg.color = Color.white;
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

            // Wire right-click on legend containers for detail popup
            WireLegendRightClick(_playerLegendContainer, gs.PLegend);
            WireLegendRightClick(_enemyLegendContainer, gs.ELegend);
        }

        private void WireLegendRightClick(Transform container, LegendInstance legend)
        {
            if (container == null || legend == null || legend.DisplayData == null) return;
            if (_cardDetailPopup == null) return;

            // Add or get EventTrigger
            var et = container.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (et == null)
            {
                et = container.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
                entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
                entry.callback.AddListener((data) =>
                {
                    var pointerData = (UnityEngine.EventSystems.PointerEventData)data;
                    if (pointerData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                    {
                        // Create a temporary UnitInstance for display
                        var tempUnit = new UnitInstance(0, legend.DisplayData, legend.Owner);
                        _cardDetailPopup.Show(tempUnit);
                    }
                });
                et.triggers.Add(entry);

                // Ensure the container is raycast-targetable
                var img = container.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
            }
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

            if (hero != null)
            {
                // Remove placeholder if it exists
                Transform ph = container.Find("HeroPlaceholder");
                if (ph != null) Destroy(ph.gameObject);

                // Reuse existing CardView or create one
                CardView cv = container.GetComponentInChildren<CardView>();
                if (cv == null && _cardViewPrefab != null)
                {
                    GameObject go = Instantiate(_cardViewPrefab, container);
                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                    cv = go.GetComponent<CardView>();
                }

                if (cv != null)
                    cv.Setup(hero, isPlayer, isPlayer ? _onUnitClicked : null, _onCardRightClicked);
            }
            else
            {
                // Remove CardView if it exists
                CardView cv = container.GetComponentInChildren<CardView>();
                if (cv != null) Destroy(cv.gameObject);

                // Reuse or create placeholder
                Transform ph = container.Find("HeroPlaceholder");
                if (ph == null)
                {
                    GameObject phGo = new GameObject("HeroPlaceholder");
                    phGo.transform.SetParent(container, false);
                    var rt = phGo.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    var txt = phGo.AddComponent<Text>();
                    txt.text = "已出场";
                    txt.font = _playerScoreText != null ? _playerScoreText.font : Font.CreateDynamicFontFromOSFont("Arial", 12);
                    txt.fontSize = 11;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = new Color(1f, 1f, 1f, 0.5f);
                }
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
                    artImg.color = Color.white; // full brightness, no dimming
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
            if (units == null) units = new List<UnitInstance>();

            int existing = container.childCount;
            int needed = units.Count;

            // Remove excess children from the end
            for (int i = existing - 1; i >= needed; i--)
                Destroy(container.GetChild(i).gameObject);

            // Add missing children
            for (int i = existing; i < needed; i++)
            {
                if (_cardViewPrefab != null)
                    Instantiate(_cardViewPrefab, container);
            }

            // Update all children in-place (no destroy/recreate flicker)
            for (int i = 0; i < needed; i++)
            {
                CardView cv = container.GetChild(i).GetComponent<CardView>();
                if (cv != null)
                {
                    UnitInstance u = units[i];
                    cv.Setup(u, isPlayer, onClick, _onCardRightClicked);
                    if (_selectedBaseUnits != null && _selectedBaseUnits.Contains(u))
                        cv.SetSelected(true);
                    if (currentMana >= 0 && u.CardData.Cost > currentMana)
                        cv.SetCostInsufficient(true);
                }
            }
        }

        private void RefreshEnemyHand(Transform container, int count)
        {
            if (container == null) return;

            int existing = container.childCount;

            // Remove excess
            for (int i = existing - 1; i >= count; i--)
                Destroy(container.GetChild(i).gameObject);

            // Add missing
            for (int i = existing; i < count; i++)
            {
                if (_cardViewPrefab != null)
                {
                    GameObject go = Instantiate(_cardViewPrefab, container);
                    go.name = $"EnemyCard_{i}";
                }
            }

            // Update all in-place
            for (int i = 0; i < count; i++)
            {
                CardView cv = container.GetChild(i).GetComponent<CardView>();
                if (cv != null)
                    cv.SetFaceDown(true);
            }
        }

        // ── Rune zone renderer ────────────────────────────────────────────────

        // ── Rune art cache (loaded once from Resources/CardArt) ─────────────
        private static readonly Dictionary<Data.RuneType, Sprite> _runeArtCache = new Dictionary<Data.RuneType, Sprite>();

        private static Sprite GetRuneArt(Data.RuneType rt)
        {
            if (_runeArtCache.TryGetValue(rt, out Sprite cached) && cached != null)
                return cached;

            string artName;
            switch (rt)
            {
                case Data.RuneType.Blazing:  artName = "rune_blazing";  break;
                case Data.RuneType.Radiant:  artName = "rune_radiant";  break;
                case Data.RuneType.Verdant:  artName = "rune_verdant";  break;
                case Data.RuneType.Crushing: artName = "rune_crushing"; break;
                default: return null;
            }

            var sprite = Resources.Load<Sprite>($"CardArt/{artName}");
            if (sprite != null) _runeArtCache[rt] = sprite;
            return sprite;
        }

        private void RefreshRuneZone(Transform container, List<RuneInstance> runes, bool isPlayer)
        {
            if (container == null) return;
            if (runes == null) runes = new List<RuneInstance>();

            int existing = container.childCount;
            int needed = runes.Count;

            // Remove excess rune objects from end
            for (int i = existing - 1; i >= needed; i--)
                Destroy(container.GetChild(i).gameObject);

            // Add missing rune objects
            for (int i = existing; i < needed; i++)
            {
                if (_runeButtonPrefab == null) break;
                GameObject newGo = Instantiate(_runeButtonPrefab, container);

                // LayoutElement to prevent vertical stretching (added once)
                var le = newGo.AddComponent<LayoutElement>();
                le.preferredWidth = 46f;
                le.preferredHeight = 46f;
                le.minWidth = 46f;
                le.minHeight = 46f;
            }

            // Update all rune objects in-place
            for (int i = 0; i < needed; i++)
            {
                int idx = i;
                RuneInstance r = runes[i];
                GameObject go = container.GetChild(i).gameObject;
                go.name = $"Rune_{r.RuneType}_{i}";

                Transform circleT = go.transform.Find("RuneCircle");
                if (circleT != null)
                {
                    // Circle background color
                    Image circleImg = circleT.GetComponent<Image>();
                    if (circleImg != null)
                        circleImg.color = r.Tapped ? GameColors.RuneTapped : GameColors.GetRuneColor(r.RuneType);

                    // Art image
                    Transform artT = circleT.Find("RuneArt");
                    if (artT != null)
                    {
                        Image artImg = artT.GetComponent<Image>();
                        Sprite runeSprite = GetRuneArt(r.RuneType);
                        if (artImg != null && runeSprite != null)
                        {
                            artImg.sprite = runeSprite;
                            artImg.color = r.Tapped ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : Color.white;
                        }
                    }

                    // Tap button — clear old listeners, re-bind with current index
                    Button tapBtn = circleT.GetComponent<Button>();
                    if (tapBtn != null)
                    {
                        tapBtn.onClick.RemoveAllListeners();
                        tapBtn.interactable = isPlayer && !r.Tapped;
                        int capturedTapIdx = idx;
                        tapBtn.onClick.AddListener(() => _onRuneClicked?.Invoke(capturedTapIdx, false));
                    }

                    // Label text
                    Transform labelT = circleT.Find("RuneTypeText");
                    if (labelT != null)
                    {
                        Text label = labelT.GetComponent<Text>();
                        if (label != null)
                        {
                            label.text = r.Tapped ? "横" : RuneTypeShortName(r.RuneType);
                            label.color = Color.white;
                        }
                    }

                    // Right-click EventTrigger — clear old, re-bind
                    if (isPlayer)
                    {
                        var et = circleT.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                        if (et == null)
                            et = circleT.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                        et.triggers.Clear();

                        var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
                        entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
                        int capturedRecycleIdx = idx;
                        entry.callback.AddListener((data) =>
                        {
                            var pd = (UnityEngine.EventSystems.PointerEventData)data;
                            if (pd.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                                _onRuneClicked?.Invoke(capturedRecycleIdx, true);
                        });
                        et.triggers.Add(entry);
                    }
                }
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

                // Move log toggle button to track the log panel edge
                if (_logToggleBtn != null)
                {
                    var btnRT = _logToggleBtn.GetComponent<RectTransform>();
                    if (btnRT != null)
                        btnRT.anchoredPosition = new Vector2(offset - 4f, btnRT.anchoredPosition.y);
                }

                yield return null;
            }

            // Final: hide log panel when collapsed
            if (collapse && _logPanel != null)
                _logPanel.SetActive(false);
        }

        // ── Debug panel toggle (DEV-10) ──────────────────────────────────────

        public void ToggleDebug()
        {
            if (_debugPanel == null) return;
            _debugCollapsed = !_debugCollapsed;

            // Toggle all children except first (title bar)
            for (int i = 1; i < _debugPanel.transform.childCount; i++)
                _debugPanel.transform.GetChild(i).gameObject.SetActive(!_debugCollapsed);

            // Resize panel
            var rt = _debugPanel.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = _debugCollapsed ? new Vector2(130f, 30f) : new Vector2(130f, 215f);

            // Update title text
            var titleText = _debugPanel.transform.Find("DebugTitle/TitleText");
            if (titleText != null)
            {
                var txt = titleText.GetComponent<Text>();
                if (txt != null)
                    txt.text = _debugCollapsed ? "▶ DEBUG" : "── DEBUG ──";
            }
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

            float pct = _timerSeconds / 30f;

            // Color gradient: green → yellow → red
            Color timerColor;
            if (_timerSeconds > 15)
                timerColor = GameColors.PlayerGreen;
            else if (_timerSeconds > 5)
                timerColor = Color.Lerp(new Color(1f, 0.85f, 0.3f, 1f), GameColors.PlayerGreen,
                    (_timerSeconds - 5f) / 10f);
            else
                timerColor = Color.Lerp(new Color(0.95f, 0.2f, 0.15f, 1f), new Color(1f, 0.85f, 0.3f, 1f),
                    _timerSeconds / 5f);

            if (_timerFill != null)
            {
                _timerFill.fillAmount = pct;
                _timerFill.color = timerColor;
            }

            // Text color matches ring
            if (_timerText != null)
                _timerText.color = timerColor;
        }

        // ── Combat result display (DEV-10) ───────────────────────────────────
        [SerializeField] private GameObject _combatResultPanel;
        [SerializeField] private Text _crAttackerText;
        [SerializeField] private Text _crDefenderText;
        [SerializeField] private Text _crVsText;
        [SerializeField] private Text _crOutcomeText;
        [SerializeField] private Text _crBfNameText;

        public void ShowCombatResult(Systems.CombatSystem.CombatResult result)
        {
            if (_combatResultPanel == null) return;

            if (_crBfNameText != null) _crBfNameText.text = $"⚔ {result.BFName}";
            if (_crAttackerText != null)
            {
                _crAttackerText.text = $"{result.AttackerName}\n⚔ {result.AttackerPower}";
                _crAttackerText.color = result.AttackerName == "玩家" ? GameColors.PlayerGreen : GameColors.EnemyRed;
            }
            if (_crDefenderText != null)
            {
                _crDefenderText.text = $"{result.DefenderName}\n🛡 {result.DefenderPower}";
                _crDefenderText.color = result.DefenderName == "玩家" ? GameColors.PlayerGreen : GameColors.EnemyRed;
            }
            if (_crOutcomeText != null)
            {
                switch (result.Outcome)
                {
                    case "attacker_win": _crOutcomeText.text = $"🏆 {result.AttackerName} 征服！"; _crOutcomeText.color = GameColors.PlayerGreen; break;
                    case "defender_win": _crOutcomeText.text = $"🛡 {result.DefenderName} 防守成功"; _crOutcomeText.color = GameColors.EnemyRed; break;
                    case "both_survive": _crOutcomeText.text = "⚖ 双方存活，攻方召回"; _crOutcomeText.color = GameColors.GoldLight; break;
                    case "both_dead": _crOutcomeText.text = "💀 同归于尽"; _crOutcomeText.color = new Color(0.7f, 0.7f, 0.7f, 1f); break;
                }
            }

            _combatResultPanel.SetActive(true);
            StartCoroutine(HideCombatResult());
        }

        private IEnumerator HideCombatResult()
        {
            yield return new WaitForSeconds(2.5f);
            if (_combatResultPanel != null) _combatResultPanel.SetActive(false);
        }

        // ── Discard/Exile click setup (DEV-10) ──────────────────────────────

        private Action<string, string> _onPileClicked; // (owner, pileType)

        public void SetPileClickCallback(Action<string, string> callback)
        {
            _onPileClicked = callback;
        }

        /// <summary>Wire discard/exile pile buttons (call once after scene loads).</summary>
        public void WirePileButtons()
        {
            // Find discard/exile buttons by traversing the count text parents
            WireSinglePileButton(_playerDiscardCountText, GameRules.OWNER_PLAYER, "discard");
            WireSinglePileButton(_playerExileCountText, GameRules.OWNER_PLAYER, "exile");
            WireSinglePileButton(_enemyDiscardCountText, GameRules.OWNER_ENEMY, "discard");
            WireSinglePileButton(_enemyExileCountText, GameRules.OWNER_ENEMY, "exile");
        }

        private void WireSinglePileButton(Text countText, string owner, string pileType)
        {
            if (countText == null) return;
            // The count text's grandparent (Discard/Exile GO) has a Button
            Transform pileGO = countText.transform.parent;
            if (pileGO == null) return;
            Button btn = pileGO.GetComponent<Button>();
            if (btn == null) return;
            btn.onClick.AddListener(() => _onPileClicked?.Invoke(owner, pileType));
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
