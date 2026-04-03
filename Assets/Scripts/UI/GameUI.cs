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
        // ── Singleton (DEV-27: needed by CardDragHandler.FindCardViewInScene) ──
        public static GameUI Instance { get; private set; }

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

        // ── Root canvas ref (GameManager is a sibling root, not under Canvas) ──
        [SerializeField] private Canvas _rootCanvas;

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
        private const float BANNER_DURATION = 1.0f;

        // ── Card detail popup ─────────────────────────────────────────────────
        [SerializeField] private CardDetailPopup _cardDetailPopup;

        // ── DEV-18: BF visual effects ─────────────────────────────────────────
        [SerializeField] private BattlefieldGlow _bf1Glow;
        [SerializeField] private BattlefieldGlow _bf2Glow;
        [SerializeField] private Image _boardFlashOverlay;
        [SerializeField] private Image _bf1CardArt;
        [SerializeField] private Image _bf2CardArt;
        private Coroutine _boardFlashCoroutine;

        // ── DEV-18b: Event feedback ───────────────────────────────────────────
        // Zone anchor RTs used to position FloatText in score/rune areas.
        // These are set by SceneBuilder.WireGameUI or can be assigned in Inspector.
        [SerializeField] private RectTransform _playerScoreZoneRT;
        [SerializeField] private RectTransform _enemyScoreZoneRT;
        [SerializeField] private RectTransform _playerRuneZoneRT;
        [SerializeField] private RectTransform _enemyRuneZoneRT;

        // ── DEV-19: UI animations ─────────────────────────────────────────────
        // Score pulse — track cached scores to detect change in RefreshScoreTrack
        private int _cachedPScore = -1;
        private int _cachedEScore = -1;

        // Phase indicator pulse — detect phase change in RefreshRoundPhase
        private string _cachedPhase = null;
        private Coroutine _phasePulseCoroutine;

        // Animated banner coroutine handle
        private Coroutine _bannerAnimCoroutine;

        // End-turn button persistent pulse
        private Coroutine _endTurnPulseCoroutine;
        private bool      _endTurnPulseActive;

        // React button ribbon — previous interactable state to detect transitions
        private bool _reactBtnWasInteractable;

        // AskPromptUI reference (optional, set by SceneBuilder)
        [SerializeField] private AskPromptUI _askPromptUI;

        // ── Callbacks set by GameManager ──────────────────────────────────────
        private Action _onEndTurnClicked;
        private Action<int> _onBFClicked;
        private Action<UnitInstance> _onUnitClicked;
        private Action<int, bool> _onRuneClicked; // (runeIdx, recycle)
        private Action<UnitInstance> _onCardRightClicked;
        private Action<UnitInstance> _onCardHoverEnter;
        private Action<UnitInstance> _onCardHoverExit;

        // ── DEV-22: Drag callbacks ────────────────────────────────────────────
        private Action<UnitInstance>            _onDragCardToBase;      // hand unit (single) → base
        private Action<List<UnitInstance>>    _onDragHandGroupToBase; // hand units (multi) → base
        private Action<UnitInstance>            _onSpellDragOut;      // hand spell → target popup
        private Action<List<UnitInstance>>      _onSpellGroupDragOut; // multiple hand spells → group cast
        private Action<UnitInstance>            _onDragHeroToBase;    // hero zone card → base
        private Action<List<UnitInstance>, int> _onDragUnitsToBF;    // base units → BF

        // ── Rune highlight state (set by SetRuneHighlights) ───────────────────
        private System.Collections.Generic.HashSet<int> _runeHighlightTap     = new System.Collections.Generic.HashSet<int>();
        private System.Collections.Generic.HashSet<int> _runeHighlightRecycle = new System.Collections.Generic.HashSet<int>();
        private Coroutine _runeHighlightPulseCoroutine;
        private System.Action _pendingEquipOnDone; // H-1: unblocks tcs2 if GameUI destroyed mid-animation

        // ── Message log state ─────────────────────────────────────────────────
        private const int MAX_MESSAGES = 5;
        private readonly Queue<Text> _messageTexts = new Queue<Text>();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this; // DEV-27: singleton ref for CardDragHandler.FindCardViewInScene
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
            FWTCG.Systems.LegendSystem.OnLegendEvolved += OnLegendEvolved; // DEV-15
            GameEventBus.OnCardPlayFailed += ShakeHandCard;   // DEV-27: migrated from GameManager
            GameEventBus.OnCardPlayed     += OnCardPlayedHandler; // DEV-27: migrated from GameManager
            // DEV-18b: event feedback bus
            GameEventBus.OnUnitFloatText += OnUnitFloatTextHandler;
            GameEventBus.OnZoneFloatText  += OnZoneFloatTextHandler;
            // DEV-19
            FWTCG.Systems.ScoreManager.OnScoreAdded += OnScoreAddedHandler;
            GameEventBus.OnDuelBanner               += OnDuelBannerHandler;
        }

        // DEV-26: OnUnitDamaged/OnUnitDied drive animations — only subscribe when component is enabled
        private void OnEnable()
        {
            GameEventBus.OnUnitDamaged += OnSpellUnitDamaged;  // DEV-27: migrated from GameManager
            GameEventBus.OnUnitDied    += OnUnitDiedHandler;   // DEV-27: migrated from GameManager
        }

        private void OnDisable()
        {
            GameEventBus.OnUnitDamaged -= OnSpellUnitDamaged;
            GameEventBus.OnUnitDied    -= OnUnitDiedHandler;
        }

        private void OnDestroy()
        {
            if (_runeHighlightPulseCoroutine != null) StopCoroutine(_runeHighlightPulseCoroutine);
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveAllListeners();
            if (_bf1Button != null) _bf1Button.onClick.RemoveAllListeners();
            if (_bf2Button != null) _bf2Button.onClick.RemoveAllListeners();
            if (_restartButton != null) _restartButton.onClick.RemoveAllListeners();
            FWTCG.Systems.TurnManager.OnBannerRequest -= ShowBanner;
            FWTCG.Systems.LegendSystem.OnLegendEvolved -= OnLegendEvolved; // DEV-15
            GameEventBus.OnCardPlayFailed -= ShakeHandCard;
            GameEventBus.OnCardPlayed     -= OnCardPlayedHandler;
            // DEV-18b
            GameEventBus.OnUnitFloatText -= OnUnitFloatTextHandler;
            GameEventBus.OnZoneFloatText  -= OnZoneFloatTextHandler;
            // DEV-19
            FWTCG.Systems.ScoreManager.OnScoreAdded -= OnScoreAddedHandler;
            GameEventBus.OnDuelBanner               -= OnDuelBannerHandler;
            // DEV-22: clear static drag zone refs to prevent stale references after scene reload
            CardDragHandler.RootCanvas = null;
            CardDragHandler.HandZoneRT = null;
            CardDragHandler.BaseZoneRT = null;
            CardDragHandler.Bf1ZoneRT  = null;
            CardDragHandler.Bf2ZoneRT  = null;
            // H-1: unblock any pending equip-fly awaiter so ActivateEquipmentAsync doesn't hang
            var cb = _pendingEquipOnDone;
            _pendingEquipOnDone = null;
            cb?.Invoke();
            // DEV-27: clear singleton ref on destroy (prevent stale ref after scene reload)
            if (Instance == this) Instance = null;
        }

        // ── Card shake on play failure ────────────────────────────────────────

        private void ShakeHandCard(FWTCG.Core.UnitInstance unit)
        {
            if (_playerHandContainer == null) return;
            foreach (Transform child in _playerHandContainer)
            {
                var cv = child.GetComponent<CardView>();
                if (cv != null && cv.Unit == unit)
                {
                    cv.Shake();
                    return;
                }
            }
        }

        // ── Spell/combat hit feedback: flash red + shake + damage popup + toast ─

        private void OnSpellUnitDamaged(FWTCG.Core.UnitInstance unit, int damage, string spellName)
        {
            var cv = FindCardView(unit);
            if (cv != null)
            {
                cv.FlashRed();
                cv.Shake();
                SpawnDamagePopup(damage, cv);
            }
            string msg = string.IsNullOrEmpty(spellName)
                ? $"{unit.UnitName} 受到 {damage} 点伤害"
                : $"{spellName} 击中 {unit.UnitName}，造成 {damage} 点伤害";
            // Combat hits batch into EventBanner (waits for FireSetBannerDelay)
            // Spell/other hits show immediately via ToastUI
            if (spellName == "战斗")
                GameEventBus.FireEventBanner(msg, 1.0f);
            else
                GameManager.FireHintToast(msg);
        }

        /// <summary>Spawns a floating damage number at the card's position on the root canvas. DEV-17.</summary>
        private void SpawnDamagePopup(int damage, CardView cv)
        {
            var cvRT = cv.GetComponent<RectTransform>();
            if (cvRT == null) return;

            Canvas rootCanvas = _rootCanvas;
            if (rootCanvas == null) return;

            // Convert card world position → canvas local position
            var canvasRT = rootCanvas.GetComponent<RectTransform>();
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, cvRT.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenPt, rootCanvas.worldCamera, out Vector2 localPos);

            // Offset upward so it doesn't overlap the card centre
            localPos += new Vector2(0f, 30f);
            DamagePopup.Create(damage, localPos, rootCanvas.transform);
        }

        // ── Unit death animation (DEV-17) ─────────────────────────────────────

        private void OnUnitDiedHandler(FWTCG.Core.UnitInstance unit)
        {
            var cv = FindCardView(unit);

            // DEV-29: compute discard pile canvas-local position for death flight animation
            Canvas rootCanvas = GetRootCanvas();
            if (cv != null && rootCanvas != null)
            {
                Text discardText = unit.Owner == FWTCG.Core.GameRules.OWNER_PLAYER
                    ? _playerDiscardCountText : _enemyDiscardCountText;
                Vector2? flyTarget = null;
                if (discardText != null)
                {
                    var discardRT = discardText.GetComponent<RectTransform>();
                    if (discardRT != null)
                    {
                        var corners = new Vector3[4];
                        discardRT.GetWorldCorners(corners);
                        Vector2 screenCenter = new Vector2(
                            (corners[0].x + corners[2].x) * 0.5f,
                            (corners[0].y + corners[2].y) * 0.5f);
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            rootCanvas.GetComponent<RectTransform>(), screenCenter,
                            rootCanvas.worldCamera, out Vector2 localPos);
                        flyTarget = localPos;
                    }
                }
                cv.PlayDeathAnimation(flyTarget, rootCanvas);
            }
            else
            {
                cv?.PlayDeathAnimation(null, null);
            }

            // DEV-21: fire canvas position so SpellVFX can spawn death explosion
            if (cv != null && rootCanvas != null)
            {
                var cvRT = cv.GetComponent<RectTransform>();
                if (cvRT != null)
                {
                    Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, cvRT.position);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        rootCanvas.GetComponent<RectTransform>(), screenPt,
                        rootCanvas.worldCamera, out Vector2 localPos);
                    GameEventBus.FireUnitDiedAtPos(unit, localPos);
                }
            }
        }

        public CardView FindCardView(FWTCG.Core.UnitInstance unit)
        {
            var containers = new Transform[]
            {
                _playerHandContainer, _enemyHandContainer,
                _playerBaseContainer, _enemyBaseContainer,
                _bf1PlayerContainer,  _bf1EnemyContainer,
                _bf2PlayerContainer,  _bf2EnemyContainer,
                _playerHeroContainer, _enemyHeroContainer,
            };
            foreach (var c in containers)
            {
                if (c == null) continue;
                foreach (Transform child in c)
                {
                    var cv = child.GetComponent<CardView>();
                    if (cv != null && cv.Unit == unit) return cv;
                }
            }
            return null;
        }

        // ── DEV-28: Target highlights ────────────────────────────────────────

        /// <summary>
        /// Highlight all CardViews whose unit passes the filter as valid targets (green pulse).
        /// Call ClearTargetHighlights() after selection completes.
        /// </summary>
        public void ShowTargetHighlights(System.Func<FWTCG.Core.UnitInstance, bool> filter)
        {
            // DEV-29: exclude _playerHandContainer — hand cards are never valid spell/equipment targets
            var containers = new Transform[]
            {
                _playerBaseContainer, _enemyBaseContainer,
                _bf1PlayerContainer, _bf1EnemyContainer,
                _bf2PlayerContainer, _bf2EnemyContainer,
            };
            foreach (var c in containers)
            {
                if (c == null) continue;
                foreach (Transform child in c)
                {
                    var cv = child.GetComponent<CardView>();
                    if (cv == null || cv.Unit == null) continue;
                    cv.SetTargeted(filter(cv.Unit));
                }
            }
        }

        public void ClearTargetHighlights()
        {
            // DEV-29: matches ShowTargetHighlights — exclude hand container
            var containers = new Transform[]
            {
                _playerBaseContainer, _enemyBaseContainer,
                _bf1PlayerContainer, _bf1EnemyContainer,
                _bf2PlayerContainer, _bf2EnemyContainer,
            };
            foreach (var c in containers)
            {
                if (c == null) continue;
                foreach (Transform child in c)
                    child.GetComponent<CardView>()?.SetTargeted(false);
            }
        }

        // ── DEV-18b: FloatText handlers ──────────────────────────────────────

        /// <summary>Show float text on a specific unit's CardView position.</summary>
        private void OnUnitFloatTextHandler(FWTCG.Core.UnitInstance unit, string text, UnityEngine.Color color)
        {
            var cv = FindCardView(unit);
            if (cv == null) return;

            Canvas rootCanvas = GetRootCanvas();
            if (rootCanvas == null) return;

            var cvRT = cv.GetComponent<RectTransform>();
            if (cvRT == null) return;

            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, cvRT.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(), screenPt, rootCanvas.worldCamera, out Vector2 localPos);
            localPos += new Vector2(0f, 28f);

            FloatText.Show(localPos, text, color, rootCanvas.transform);
        }

        /// <summary>Show float text at a named zone anchor.</summary>
        private void OnZoneFloatTextHandler(string zone, string text, UnityEngine.Color color)
        {
            Canvas rootCanvas = GetRootCanvas();
            if (rootCanvas == null) return;

            RectTransform zoneRT = GetZoneRT(zone);
            if (zoneRT == null) return;

            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, zoneRT.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(), screenPt, rootCanvas.worldCamera, out Vector2 localPos);
            localPos += new Vector2(0f, 20f);

            bool large = zone.StartsWith("score_");
            FloatText.Show(localPos, text, color, rootCanvas.transform, large);
        }

        private RectTransform GetZoneRT(string zone)
        {
            switch (zone)
            {
                case "score_player": return _playerScoreZoneRT;
                case "score_enemy":  return _enemyScoreZoneRT;
                case "rune_player":  return _playerRuneZoneRT;
                case "rune_enemy":   return _enemyRuneZoneRT;
                default:             return null;
            }
        }

        private Canvas GetRootCanvas() => _rootCanvas;

        /// <summary>DEV-30 F1: Returns the canvas-local position of a named zone. Used by SpellVFX for conquest burst.</summary>
        public Vector2 GetZoneCanvasPos(string zone)
        {
            Canvas rootCanvas = GetRootCanvas();
            if (rootCanvas == null) return Vector2.zero;
            RectTransform zoneRT = GetZoneRT(zone);
            if (zoneRT == null) return Vector2.zero;
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, zoneRT.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(), screenPt, rootCanvas.worldCamera, out Vector2 localPos);
            return localPos;
        }

        // ── DEV-15: Legend evolution flash ───────────────────────────────────

        private void OnLegendEvolved(string owner, int newLevel)
        {
            Text legendText = owner == FWTCG.Core.GameRules.OWNER_PLAYER
                ? _playerLegendText
                : _enemyLegendText;
            if (legendText != null)
                StartCoroutine(FlashLegendText(legendText));
        }

        private IEnumerator FlashLegendText(Text text)
        {
            Color original = text.color;
            Color flash    = new Color(1f, 0.85f, 0.1f); // bright gold
            float duration = 0.15f;

            for (int i = 0; i < 4; i++)
            {
                float t = 0f;
                while (t < duration)
                {
                    text.color = Color.Lerp(original, flash, t / duration);
                    t += Time.deltaTime;
                    yield return null;
                }
                t = 0f;
                while (t < duration)
                {
                    text.color = Color.Lerp(flash, original, t / duration);
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            text.color = original;
        }

        public void ShowBanner(string text)
        {
            if (_bannerPanel == null) return;
            if (_bannerText != null) _bannerText.text = text;
            if (_bannerAnimCoroutine != null) StopCoroutine(_bannerAnimCoroutine);
            _bannerAnimCoroutine = StartCoroutine(BannerSlideRoutine());
        }

        // DEV-19: fade-only turn banner (no position slide)
        private IEnumerator BannerSlideRoutine()
        {
            _bannerPanel.SetActive(true);
            var cg = _bannerPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = _bannerPanel.AddComponent<CanvasGroup>();

            const float IN_DUR = 0.2f;
            float elapsed = 0f;
            while (elapsed < IN_DUR)
            {
                cg.alpha = elapsed / IN_DUR;
                elapsed += Time.deltaTime;
                yield return null;
            }
            cg.alpha = 1f;

            yield return new WaitForSeconds(BANNER_DURATION);

            const float OUT_DUR = 0.2f;
            elapsed = 0f;
            while (elapsed < OUT_DUR)
            {
                cg.alpha = 1f - elapsed / OUT_DUR;
                elapsed += Time.deltaTime;
                yield return null;
            }
            cg.alpha = 0f;
            _bannerPanel.SetActive(false);
            _bannerAnimCoroutine = null;
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        public void SetCallbacks(Action onEndTurn, Action<int> onBF,
                                 Action<UnitInstance> onUnit, Action<int, bool> onRune,
                                 Action<UnitInstance> onCardRightClick = null,
                                 Action<UnitInstance> onCardHoverEnter = null,
                                 Action<UnitInstance> onCardHoverExit  = null)
        {
            _onEndTurnClicked   = onEndTurn;
            _onBFClicked        = onBF;
            _onUnitClicked      = onUnit;
            _onRuneClicked      = onRune;
            _onCardRightClicked = onCardRightClick;
            _onCardHoverEnter   = onCardHoverEnter;
            _onCardHoverExit    = onCardHoverExit;
        }

        /// <summary>
        /// DEV-22: Set drag-to-play callbacks (called by GameManager.Start after SetCallbacks).
        /// </summary>
        public void SetDragCallbacks(
            Action<UnitInstance>            onDragCardToBase,
            Action<List<UnitInstance>>      onDragHandGroupToBase,
            Action<UnitInstance>            onSpellDragOut,
            Action<List<UnitInstance>>      onSpellGroupDragOut,
            Action<UnitInstance>            onDragHeroToBase,
            Action<List<UnitInstance>, int> onDragUnitsToBF)
        {
            _onDragCardToBase      = onDragCardToBase;
            _onDragHandGroupToBase = onDragHandGroupToBase;
            _onSpellDragOut        = onSpellDragOut;
            _onSpellGroupDragOut   = onSpellGroupDragOut;
            _onDragHeroToBase      = onDragHeroToBase;
            _onDragUnitsToBF       = onDragUnitsToBF;
        }

        /// <summary>
        /// DEV-22: Push zone RectTransforms to CardDragHandler static fields so every
        /// handler instance can detect drop zones without holding per-instance refs.
        /// Call once after scene is built.
        /// </summary>
        public void SetupDragZones()
        {
            CardDragHandler.RootCanvas = _rootCanvas;
            CardDragHandler.HandZoneRT = _playerHandZoneRT;

            // Base zone: parent panel of _playerBaseContainer
            if (_playerBaseContainer != null && _playerBaseContainer.parent != null)
                CardDragHandler.BaseZoneRT = _playerBaseContainer.parent.GetComponent<RectTransform>();

            // BF zones: use the BF button RTs (they cover the full BF panel area)
            if (_bf1Button != null) CardDragHandler.Bf1ZoneRT = _bf1Button.GetComponent<RectTransform>();
            if (_bf2Button != null) CardDragHandler.Bf2ZoneRT = _bf2Button.GetComponent<RectTransform>();
        }

        /// <summary>
        /// Highlights rune circles for the auto-consume plan preview.
        /// Blue pulse = needs tap (for mana), Red pulse = needs recycle (for sch).
        /// Call Refresh() after this to apply visual state.
        /// </summary>
        public void SetRuneHighlights(List<int> tapIndices, List<int> recycleIndices)
        {
            _runeHighlightTap.Clear();
            _runeHighlightRecycle.Clear();
            if (tapIndices     != null) foreach (int i in tapIndices)     _runeHighlightTap.Add(i);
            if (recycleIndices != null) foreach (int i in recycleIndices) _runeHighlightRecycle.Add(i);

            // Start breathing pulse coroutine
            if (_runeHighlightPulseCoroutine != null) StopCoroutine(_runeHighlightPulseCoroutine);
            if (_runeHighlightTap.Count > 0 || _runeHighlightRecycle.Count > 0)
                _runeHighlightPulseCoroutine = StartCoroutine(RuneHighlightPulseRoutine());
        }

        /// <summary>Clears all rune highlights and stops the pulse coroutine.</summary>
        public void ClearRuneHighlights()
        {
            if (_runeHighlightPulseCoroutine != null)
            {
                StopCoroutine(_runeHighlightPulseCoroutine);
                _runeHighlightPulseCoroutine = null;
            }
            // Reset border glow on highlighted runes immediately
            if (_playerRuneContainer != null)
            {
                foreach (int idx in _runeHighlightTap)
                    ResetRuneBorderGlow(idx);
                foreach (int idx in _runeHighlightRecycle)
                    ResetRuneBorderGlow(idx);
            }
            _runeHighlightTap.Clear();
            _runeHighlightRecycle.Clear();
        }

        /// <summary>
        /// Clears the RuneBorder glow on a single rune slot — Image transparent, Outline back to golden default.
        /// </summary>
        private void ResetRuneBorderGlow(int idx)
        {
            if (_playerRuneContainer == null || idx < 0 || idx >= _playerRuneContainer.childCount) return;
            Transform circleT = _playerRuneContainer.GetChild(idx).Find("RuneCircle");
            if (circleT == null) return;
            Transform borderT = circleT.Find("RuneBorder");
            if (borderT == null) return;
            Image img = borderT.GetComponent<Image>();
            if (img != null) img.color = new Color(0f, 0f, 0f, 0f); // fully transparent
            UnityEngine.UI.Outline outline = borderT.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null)
            {
                outline.effectColor    = new Color(0.471f, 0.353f, 0.157f, 0f); // golden, but invisible
                outline.effectDistance = new Vector2(2f, -2f); // restore default distance
            }
        }

        private IEnumerator RuneHighlightPulseRoutine()
        {
            // Blue = needs tap for mana  |  Red = needs recycle for sch
            // Alpha breathes 0.25 → 1.0 at ~1.75 Hz; solid RGB stays fixed for clarity
            Color tapFill    = new Color(0.15f, 0.50f, 1.0f, 1f);
            Color tapOutline = new Color(0.45f, 0.80f, 1.0f, 1f);
            Color recFill    = new Color(1.0f,  0.15f, 0.15f, 1f);
            Color recOutline = new Color(1.0f,  0.50f, 0.50f, 1f);

            while (true)
            {
                float pulse = (Mathf.Sin(Time.time * 3.5f) + 1f) * 0.5f; // 0→1→0, ~1.75 Hz
                float alpha = Mathf.Lerp(0.25f, 1.0f, pulse);

                if (_playerRuneContainer != null)
                {
                    foreach (int idx in _runeHighlightTap)
                        SetRuneBorderGlow(idx, tapFill, tapOutline, alpha);
                    foreach (int idx in _runeHighlightRecycle)
                        SetRuneBorderGlow(idx, recFill, recOutline, alpha);
                }
                yield return null;
            }
        }

        private void SetRuneBorderGlow(int idx, Color fillRGB, Color outlineRGB, float alpha)
        {
            if (_playerRuneContainer == null || idx < 0 || idx >= _playerRuneContainer.childCount) return;
            Transform circleT = _playerRuneContainer.GetChild(idx).Find("RuneCircle");
            if (circleT == null) return;
            Transform borderT = circleT.Find("RuneBorder");
            if (borderT == null) return;

            Image img = borderT.GetComponent<Image>();
            if (img != null) img.color = new Color(fillRGB.r, fillRGB.g, fillRGB.b, alpha * 0.35f); // subtle inner fill

            UnityEngine.UI.Outline outline = borderT.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(outlineRGB.r, outlineRGB.g, outlineRGB.b, alpha);
                outline.effectDistance = new Vector2(4f, -4f); // wider glow when active
            }
        }

        // ── Selection state (set by GameManager) ─────────────────────────────
        private List<UnitInstance> _selectedBaseUnits;
        private List<UnitInstance> _selectedHandUnits;

        // ── Full refresh ──────────────────────────────────────────────────────

        /// <summary>
        /// Redraws all UI panels to match current game state.
        /// </summary>
        public void Refresh(GameState gs)
        {
            Refresh(gs, _selectedBaseUnits, _selectedHandUnits); // preserve current selections on hover refresh
        }

        /// <summary>
        /// Redraws all UI panels. selectedBaseUnits highlights multi-selected units in green.
        /// </summary>
        public void Refresh(GameState gs, List<UnitInstance> selectedBaseUnits, List<UnitInstance> selectedHandUnits = null)
        {
            if (gs == null) return;

            _selectedBaseUnits = selectedBaseUnits;
            _selectedHandUnits = selectedHandUnits;

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
            UpdateBFGlows(gs);        // DEV-18
            UpdateBFCardArt(gs);      // DEV-18
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

            // Phase indicator pulse: fire when phase changes (DEV-19)
            if (gs.Phase != _cachedPhase && _roundPhaseText != null)
            {
                _cachedPhase = gs.Phase;
                if (_phasePulseCoroutine != null) StopCoroutine(_phasePulseCoroutine);
                _phasePulseCoroutine = StartCoroutine(PhasePulseRoutine(_roundPhaseText.GetComponent<RectTransform>()));
            }
        }

        private IEnumerator PhasePulseRoutine(RectTransform rt)
        {
            if (rt == null) yield break;
            const float DURATION = 0.4f;
            const float PEAK     = 1.18f;
            float half = DURATION * 0.5f;

            // Scale up
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(1f, PEAK, t / half);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            // Scale back down
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(PEAK, 1f, t / half);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _phasePulseCoroutine = null;
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
                    sb.Append($"{kv.Key.ToShort()}×{kv.Value}");
                    any = true;
                }
            }
            if (!any) sb.Append("—");
            return sb.ToString();
        }

        private void RefreshHands(GameState gs)
        {
            // Player hand (with cost-insufficient dimming + hover callbacks)
            RefreshUnitList(_playerHandContainer, gs.PHand, true, _onUnitClicked, gs.PMana,
                            _onCardHoverEnter, _onCardHoverExit);
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
                if (CardDragHandler.BlockPointerEvents) return;
                if (Input.GetMouseButton(0)) return;
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
                    if (CardDragHandler.BlockPointerEvents) return;
                    if (Input.GetMouseButton(0)) return;
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
                {
                    cv.Setup(hero, isPlayer, isPlayer ? _onUnitClicked : null, _onCardRightClicked);
                    // Wire drag callback for player hero card
                    if (isPlayer)
                    {
                        var dh = cv.GetComponent<CardDragHandler>();
                        if (dh != null)
                            dh.OnDragHeroToBase = _onDragHeroToBase;
                    }
                }
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
                                     int currentMana = -1,
                                     Action<UnitInstance> onHoverEnter = null,
                                     Action<UnitInstance> onHoverExit  = null)
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
                    // Reset CanvasGroup alpha — HideEquipCardInBase sets alpha=0 for fly
                    // animation; if this CardView is reused for a different unit the alpha
                    // must be restored or the new card appears transparent (white).
                    var cg = cv.GetComponent<UnityEngine.CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
                    cv.Setup(u, isPlayer, onClick, _onCardRightClicked, onHoverEnter, onHoverExit);
                    // Always reset first, then highlight only if in selection list
                    cv.SetSelected(false);
                    if (_selectedBaseUnits != null && _selectedBaseUnits.Contains(u))
                        cv.SetSelected(true);
                    if (_selectedHandUnits != null && _selectedHandUnits.Contains(u))
                        cv.SetSelected(true);
                    if (currentMana >= 0 && u.CardData.Cost > currentMana)
                        cv.SetCostInsufficient(true);

                    // DEV-22: Wire drag callbacks for player cards only
                    if (isPlayer)
                    {
                        var dh = cv.GetComponent<CardDragHandler>();
                        if (dh != null)
                        {
                            dh.OnDragToBase          = _onDragCardToBase;
                            dh.OnDragHandGroupToBase = _onDragHandGroupToBase;
                            dh.OnSpellDragOut        = _onSpellDragOut;
                            dh.OnSpellGroupDragOut   = _onSpellGroupDragOut;
                            dh.OnDragToBF            = _onDragUnitsToBF;
                        }
                    }
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

                // Ensure RuneCircle/RuneArt hierarchy exists (may be absent if prefab refs lost)
                EnsureRuneCircle(newGo);
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
                    // Circle background color — always shows real rune state; border glow handles highlight
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
                            label.text = r.Tapped ? "横" : r.RuneType.ToShort();
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
                            if (Input.GetMouseButton(0)) return;
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
            // DEV-24: brief gold flash on new log entry
            StartCoroutine(LogEntryFlashRoutine(entry));
        }

        private IEnumerator LogEntryFlashRoutine(Text entry)
        {
            if (entry == null) yield break;
            Color original = entry.color;
            Color gold = new Color(0.95f, 0.82f, 0.40f, 1f);
            const float DUR = 0.8f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / DUR;
                if (entry == null) yield break;
                entry.color = Color.Lerp(gold, original, t);
                yield return null;
            }
            if (entry != null) entry.color = original;
        }

        // ── Game over overlay ─────────────────────────────────────────────────

        public void ShowGameOver(string msg)
        {
            if (_gameOverPanel == null) return;
            _gameOverPanel.SetActive(true);
            if (_gameOverText != null) _gameOverText.text = msg;
            if (_endTurnButton != null) _endTurnButton.interactable = false;
            // DEV-24: fade in game over panel
            StartCoroutine(FadeInPanelRoutine(_gameOverPanel));
        }

        private IEnumerator FadeInPanelRoutine(GameObject panel)
        {
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            const float DUR = 0.5f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / DUR;
                if (cg == null) yield break; // guard: panel destroyed mid-fade
                cg.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            cg.alpha = 1f;
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void HandleEndTurn()
        {
            GameEventBus.FireClearBanners(); // immediately dismiss any showing banner
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
            // DEV-19: detect score increase → trigger pulse on newly-lit circle
            bool pScoreUp = gs.PScore > _cachedPScore && _cachedPScore >= 0;
            bool eScoreUp = gs.EScore > _cachedEScore && _cachedEScore >= 0;
            _cachedPScore = gs.PScore;
            _cachedEScore = gs.EScore;

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
                // Pulse the circle that just lit up (index = newScore - 1)
                if (pScoreUp && gs.PScore > 0 && gs.PScore - 1 < _playerScoreCircles.Length)
                    TriggerScorePulse(_playerScoreCircles[gs.PScore - 1]);
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
                if (eScoreUp && gs.EScore > 0 && gs.EScore - 1 < _enemyScoreCircles.Length)
                    TriggerScorePulse(_enemyScoreCircles[gs.EScore - 1]);
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

        // ── DEV-18: BF glow + board flash + BF art ───────────────────────────

        /// <summary>Update BattlefieldGlow components with current control state.</summary>
        public void UpdateBFGlows(GameState gs)
        {
            if (gs.BF == null) return;
            if (_bf1Glow != null && gs.BF.Length > 0)
                _bf1Glow.SetControl(gs.BF[0].Ctrl);
            if (_bf2Glow != null && gs.BF.Length > 1)
                _bf2Glow.SetControl(gs.BF[1].Ctrl);
        }

        /// <summary>
        /// Load BF card art sprites from Resources and assign to BFCardArt Images.
        /// Safe to call repeatedly — only loads when sprite is not already set.
        /// </summary>
        public void UpdateBFCardArt(GameState gs)
        {
            if (gs.BFNames == null) return;
            AssignBFArt(_bf1CardArt, gs.BFNames.Length > 0 ? gs.BFNames[0] : null);
            AssignBFArt(_bf2CardArt, gs.BFNames.Length > 1 ? gs.BFNames[1] : null);
        }

        private static void AssignBFArt(Image img, string bfId)
        {
            if (img == null || string.IsNullOrEmpty(bfId)) return;
            if (img.sprite != null) return; // already loaded
            Sprite sp = Resources.Load<Sprite>($"CardArt/bf_{bfId}");
            if (sp != null)
            {
                img.sprite = sp;
                img.color = Color.white;
                img.preserveAspect = true;
            }
        }

        /// <summary>Card-played board flash — brief golden overlay on the board. DEV-18.</summary>
        private void OnCardPlayedHandler(UnitInstance unit, string owner)
        {
            if (_boardFlashOverlay == null) return;
            if (_boardFlashCoroutine != null) StopCoroutine(_boardFlashCoroutine);
            _boardFlashCoroutine = StartCoroutine(BoardFlashRoutine(owner));
        }

        private IEnumerator BoardFlashRoutine(string owner)
        {
            // Player flash: gold. Enemy flash: red-tint.
            Color flashCol = owner == FWTCG.Core.GameRules.OWNER_PLAYER
                ? new Color(0.78f, 0.67f, 0.43f, 0.18f)
                : new Color(0.97f, 0.30f, 0.30f, 0.12f);
            Color clear = new Color(flashCol.r, flashCol.g, flashCol.b, 0f);

            const float HALF = 0.425f; // 0.85s total / 2
            float elapsed = 0f;

            // Fade in
            while (elapsed < HALF)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / HALF;
                _boardFlashOverlay.color = Color.Lerp(clear, flashCol, t);
                yield return null;
            }

            elapsed = 0f;
            // Fade out
            while (elapsed < HALF)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / HALF;
                _boardFlashOverlay.color = Color.Lerp(flashCol, clear, t);
                yield return null;
            }

            _boardFlashOverlay.color = clear;
            _boardFlashCoroutine = null;
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

            // DEV-19: end turn button persistent pulse during player action
            UpdateEndTurnPulse(isPlayerAction);
        }

        // DEV-19: called by GameManager when react button interactability changes
        public void NotifyReactButtonState(bool isInteractable)
        {
            bool transition = !_reactBtnWasInteractable && isInteractable;
            _reactBtnWasInteractable = isInteractable;
            if (transition)
                GameManager.FireHintToast(""); // no-op, ribbon animation below
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
                rt.sizeDelta = _debugCollapsed ? new Vector2(130f, 30f) : new Vector2(130f, 360f);

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

        private Coroutine _crHideRoutine;

        public void ShowCombatResult(Systems.CombatSystem.CombatResult result)
        {
            if (_combatResultPanel == null) return;

            // Stop any in-progress hide
            if (_crHideRoutine != null) { StopCoroutine(_crHideRoutine); _crHideRoutine = null; }

            if (_crBfNameText != null) _crBfNameText.text = $"⚔ {result.BFName}";

            // Attacker side
            if (_crAttackerText != null)
            {
                _crAttackerText.text  = $"{result.AttackerName}\n⚔ {result.AttackerPower}";
                _crAttackerText.color = result.AttackerName == "玩家" ? GameColors.PlayerGreen : GameColors.EnemyRed;
            }

            // Defender side
            if (_crDefenderText != null)
            {
                _crDefenderText.text  = $"{result.DefenderName}\n🛡 {result.DefenderPower}";
                _crDefenderText.color = result.DefenderName == "玩家" ? GameColors.PlayerGreen : GameColors.EnemyRed;
            }

            // Outcome + death list merged into one text block
            if (_crOutcomeText != null)
            {
                // Line 1: outcome
                string outcomeStr;
                Color  outcomeColor;
                switch (result.Outcome)
                {
                    case "attacker_win":
                        outcomeStr   = $"🏆 {result.AttackerName} 征服！";
                        outcomeColor = result.AttackerName == "玩家" ? GameColors.PlayerGreen : GameColors.EnemyRed;
                        break;
                    case "defender_win":
                        outcomeStr   = $"🛡 {result.DefenderName} 防守成功";
                        outcomeColor = result.DefenderName == "玩家" ? GameColors.PlayerGreen : GameColors.EnemyRed;
                        break;
                    case "both_survive":
                        outcomeStr   = "⚖ 双方存活，攻方召回";
                        outcomeColor = GameColors.GoldLight;
                        break;
                    default: // both_dead
                        outcomeStr   = "💀 同归于尽";
                        outcomeColor = new Color(0.72f, 0.72f, 0.72f, 1f);
                        break;
                }

                // Line 2+: deaths (灰色)
                var deaths = new System.Text.StringBuilder();
                if (result.DeadAttackers != null)
                    foreach (var n in result.DeadAttackers)
                        deaths.Append($"\n💀 {n}（{result.AttackerName}）");
                if (result.DeadDefenders != null)
                    foreach (var n in result.DeadDefenders)
                        deaths.Append($"\n💀 {n}（{result.DefenderName}）");

                _crOutcomeText.text  = outcomeStr + deaths;
                _crOutcomeText.color = outcomeColor;
            }

            // Ensure CanvasGroup exists for fade
            var cg = _combatResultPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = _combatResultPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _combatResultPanel.SetActive(true);
            _crHideRoutine = StartCoroutine(ShowHideCombatResult(cg));
        }

        private IEnumerator ShowHideCombatResult(CanvasGroup cg)
        {
            const float FADE_IN  = 0.2f;
            const float STAY     = 3.5f;   // 2.5s + 1s extra
            const float FADE_OUT = 0.3f;

            // Fade in
            float t = 0f;
            while (t < FADE_IN) { t += Time.deltaTime; cg.alpha = Mathf.Clamp01(t / FADE_IN); yield return null; }
            cg.alpha = 1f;

            yield return new WaitForSeconds(STAY);

            // Fade out
            t = 0f;
            while (t < FADE_OUT) { t += Time.deltaTime; cg.alpha = Mathf.Clamp01(1f - t / FADE_OUT); yield return null; }
            cg.alpha = 0f;
            if (_combatResultPanel != null) _combatResultPanel.SetActive(false);
            _crHideRoutine = null;
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



        private Button FindChildButton(GameObject parent, string name)
        {
            Transform t = parent.transform.Find(name);
            if (t != null) return t.GetComponent<Button>();
            return null;
        }

        // ── DEV-19: Score pulse + ring expand ─────────────────────────────────

        /// <summary>Subscribed to ScoreManager.OnScoreAdded — caches are already updated by RefreshScoreTrack.</summary>
        private void OnScoreAddedHandler(string owner, int newScore)
        {
            // Actual pulse is triggered inside RefreshScoreTrack via cached score comparison.
            // This handler is kept for future extensions (e.g., audio cue).
        }

        /// <summary>Scales a score circle 1→1.15→1 over 1.8 s and spawns an expanding ring.</summary>
        private void TriggerScorePulse(Image circle)
        {
            if (circle == null) return;
            StartCoroutine(ScorePulseRoutine(circle));
            SpawnScoreRing(circle);
        }

        private IEnumerator ScorePulseRoutine(Image circle)
        {
            var rt = circle.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector3 orig = rt.localScale;
            Vector3 peak = orig * 1.15f;
            const float HALF = 0.9f; // 1.8s total / 2

            float elapsed = 0f;
            while (elapsed < HALF)
            {
                rt.localScale = Vector3.Lerp(orig, peak, elapsed / HALF);
                elapsed += Time.deltaTime;
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < HALF)
            {
                rt.localScale = Vector3.Lerp(peak, orig, elapsed / HALF);
                elapsed += Time.deltaTime;
                yield return null;
            }
            rt.localScale = orig;
        }

        private void SpawnScoreRing(Image circle)
        {
            Canvas rootCanvas = GetRootCanvas();
            if (rootCanvas == null) return;

            var circleRT = circle.GetComponent<RectTransform>();
            if (circleRT == null) return;

            // Convert circle's world position to canvas local space
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, circleRT.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(), screenPt,
                rootCanvas.worldCamera, out Vector2 localPos);

            var ringGO  = new GameObject("ScoreRing_temp");
            ringGO.transform.SetParent(rootCanvas.transform, false);
            var ringImg = ringGO.AddComponent<Image>();
            ringImg.color = new Color(0.78f, 0.67f, 0.43f, 0.75f); // gold

            var rt = ringGO.GetComponent<RectTransform>();
            rt.anchoredPosition = localPos;
            rt.sizeDelta        = circleRT.sizeDelta;

            StartCoroutine(ScoreRingRoutine(ringImg, rt));
        }

        private IEnumerator ScoreRingRoutine(Image ring, RectTransform rt)
        {
            if (ring == null || rt == null) yield break;
            const float DUR = 2f;
            float elapsed = 0f;
            Vector3 initScale = rt.localScale;
            Color initColor   = ring.color;

            while (elapsed < DUR)
            {
                float t = elapsed / DUR;
                rt.localScale = initScale * Mathf.Lerp(1f, 2.5f, t);
                ring.color    = new Color(initColor.r, initColor.g, initColor.b,
                                          Mathf.Lerp(initColor.a, 0f, t));
                elapsed += Time.deltaTime;
                yield return null;
            }
            Destroy(ring.gameObject);
        }

        // ── DEV-19: End-turn button persistent pulse ───────────────────────────

        private void UpdateEndTurnPulse(bool active)
        {
            if (active == _endTurnPulseActive) return;
            _endTurnPulseActive = active;

            if (active)
            {
                if (_endTurnPulseCoroutine == null && _endTurnButton != null)
                    _endTurnPulseCoroutine = StartCoroutine(EndTurnPulseRoutine());
            }
            else
            {
                if (_endTurnPulseCoroutine != null)
                {
                    StopCoroutine(_endTurnPulseCoroutine);
                    _endTurnPulseCoroutine = null;
                }
                // Restore full opacity
                if (_endTurnButton != null)
                {
                    var cg = _endTurnButton.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
                }
            }
        }

        private IEnumerator EndTurnPulseRoutine()
        {
            var cg = _endTurnButton.GetComponent<CanvasGroup>();
            if (cg == null) cg = _endTurnButton.gameObject.AddComponent<CanvasGroup>();

            const float PERIOD = 2f;
            const float MIN_ALPHA = 0.60f;

            while (true)
            {
                float elapsed = 0f;
                // Fade dim
                while (elapsed < PERIOD * 0.5f)
                {
                    cg.alpha = Mathf.Lerp(1f, MIN_ALPHA, elapsed / (PERIOD * 0.5f));
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                elapsed = 0f;
                // Fade bright
                while (elapsed < PERIOD * 0.5f)
                {
                    cg.alpha = Mathf.Lerp(MIN_ALPHA, 1f, elapsed / (PERIOD * 0.5f));
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // ── DEV-19: Spell duel banner ─────────────────────────────────────────

        private void OnDuelBannerHandler()
        {
            ShowBanner("⚡ 法术对决！");
        }

        // ── DEV-19: React button ribbon reveal ────────────────────────────────

        /// <summary>
        /// Called by GameManager when the React button transitions to active.
        /// Plays a scale-X ribbon reveal animation on the button.
        /// </summary>
        public void PlayReactRibbonReveal(Button reactBtn)
        {
            if (reactBtn == null) return;
            StartCoroutine(ReactRibbonRevealRoutine(reactBtn));
        }

        private IEnumerator ReactRibbonRevealRoutine(Button btn)
        {
            var rt = btn.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector3 orig = rt.localScale;
            Vector3 flat = new Vector3(0f, orig.y, orig.z);

            const float REVEAL_DUR = 0.25f;
            float elapsed = 0f;

            rt.localScale = flat;
            while (elapsed < REVEAL_DUR)
            {
                float t = elapsed / REVEAL_DUR;
                rt.localScale = Vector3.Lerp(flat, orig, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            rt.localScale = orig;

            // Brief pulse after reveal (2 s period, 1 cycle)
            const float PULSE_DUR = 2f;
            elapsed = 0f;
            while (elapsed < PULSE_DUR)
            {
                float t   = elapsed / PULSE_DUR;
                float sin = Mathf.Sin(t * Mathf.PI * 2f); // one full cycle
                rt.localScale = orig * (1f + sin * 0.06f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            rt.localScale = orig;
        }

        /// <summary>
        /// Creates the RuneCircle / RuneArt / RuneTypeText hierarchy inside a rune GO
        /// if it doesn't already exist (handles case where prefab refs were lost after git reset).
        /// </summary>
        private static void EnsureRuneCircle(GameObject runeGo)
        {
            if (runeGo.transform.Find("RuneCircle") != null) return;

            // ── RuneCircle (fills parent, has Image + Button) ─────────────────
            var circleGo = new GameObject("RuneCircle");
            circleGo.transform.SetParent(runeGo.transform, false);
            var circleRT = circleGo.GetComponent<RectTransform>();
            if (circleRT == null) circleRT = circleGo.AddComponent<RectTransform>();
            circleRT.anchorMin = Vector2.zero;
            circleRT.anchorMax = Vector2.one;
            circleRT.offsetMin = Vector2.zero;
            circleRT.offsetMax = Vector2.zero;

            var circleImg = circleGo.AddComponent<Image>();
            circleImg.raycastTarget = true;

            var btn = circleGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 0.7f, 1f);
            btn.colors = colors;

            // ── RuneArt (80% of circle, non-raycast) ──────────────────────────
            var artGo = new GameObject("RuneArt");
            artGo.transform.SetParent(circleGo.transform, false);
            var artRT = artGo.AddComponent<RectTransform>();
            artRT.anchorMin = new Vector2(0.1f, 0.1f);
            artRT.anchorMax = new Vector2(0.9f, 0.9f);
            artRT.offsetMin = Vector2.zero;
            artRT.offsetMax = Vector2.zero;
            var artImg = artGo.AddComponent<Image>();
            artImg.raycastTarget = false;
            artImg.preserveAspect = true;

            // ── RuneTypeText (centred overlay) ────────────────────────────────
            var textGo = new GameObject("RuneTypeText");
            textGo.transform.SetParent(circleGo.transform, false);
            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var txt = textGo.AddComponent<Text>();
            txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Hide the old RuneTypeText that was directly on prefab root (if any)
            Transform oldLabel = runeGo.transform.Find("RuneTypeText");
            if (oldLabel != null) oldLabel.gameObject.SetActive(false);
        }

        // ── Equipment card activation animations ─────────────────────────────

        /// <summary>
        /// Hides the CardView for an equipment card in base and returns its canvas-local position.
        /// Call before showing the target-selection popup.
        /// </summary>
        public Vector2 HideEquipCardInBase(FWTCG.Core.UnitInstance equip)
        {
            var cv = FindCardView(equip);
            if (cv == null) return Vector2.zero;
            var cg = cv.GetComponent<CanvasGroup>();
            if (cg == null) cg = cv.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            return RectTransformToCanvasLocal(cv.GetComponent<RectTransform>());
        }

        /// <summary>
        /// Restores the CardView alpha for equipment still in base (cancel case).
        /// </summary>
        public void RestoreEquipCardInBase(FWTCG.Core.UnitInstance equip)
        {
            var cv = FindCardView(equip);
            if (cv == null) return;
            var cg = cv.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }

        /// <summary>
        /// Spawns a ghost card that flies from fromCanvasPos to a target unit's CardView,
        /// then calls onDone when complete.
        /// </summary>
        public void AnimateEquipFlyToUnit(Vector2 fromCanvasPos, FWTCG.Core.UnitInstance target,
                                          System.Action onDone)
        {
            var targetCV = FindCardView(target);
            Vector2 toPos = targetCV != null
                ? RectTransformToCanvasLocal(targetCV.GetComponent<RectTransform>())
                : Vector2.zero;
            StartCoroutine(EquipFlyRoutine(fromCanvasPos, toPos, onDone));
        }

        /// <summary>
        /// Spawns a ghost card that flies from fromCanvasPos back to basePos, then calls onDone.
        /// </summary>
        public void AnimateEquipFlyToBase(Vector2 fromCanvasPos, Vector2 basePos,
                                          System.Action onDone)
        {
            StartCoroutine(EquipFlyRoutine(fromCanvasPos, basePos, onDone));
        }

        /// <summary>Convert any RectTransform's centre to canvas-root local coords.</summary>
        private Vector2 RectTransformToCanvasLocal(RectTransform rt)
        {
            if (rt == null || _rootCanvas == null) return Vector2.zero;
            Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : _rootCanvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.GetComponent<RectTransform>(), screenPoint, cam, out Vector2 local);
            return local;
        }

        private IEnumerator EquipFlyRoutine(Vector2 from, Vector2 to, System.Action onDone)
        {
            _pendingEquipOnDone = onDone;
            if (_rootCanvas == null) { _pendingEquipOnDone = null; onDone?.Invoke(); yield break; }

            var ghost = new GameObject("EquipFlyGhost");
            ghost.transform.SetParent(_rootCanvas.transform, false);
            var ghostRT = ghost.AddComponent<RectTransform>();
            ghostRT.sizeDelta = new Vector2(80f, 112f);
            ghostRT.anchoredPosition = from;

            var img = ghost.AddComponent<Image>();
            img.color = new Color(0.85f, 0.75f, 0.25f, 0.85f); // gold ghost

            var cg = ghost.AddComponent<CanvasGroup>();

            const float dur = 0.35f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                ghostRT.anchoredPosition = Vector2.Lerp(from, to, p);
                cg.alpha = 1f - p * 0.3f; // slight fade toward end
                yield return null;
            }

            Destroy(ghost);
            _pendingEquipOnDone = null;
            onDone?.Invoke();
        }
    }
}
