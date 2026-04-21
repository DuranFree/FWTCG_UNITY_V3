using System;
using System.Collections.Generic;
using DG.Tweening;
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

        // ── B8 full: 待命区槽位（Rule 23 面朝下牌） ────────────────────────────
        // 运行时 Find by name: BF0Standby / BF1Standby（SceneBuilder 已创建）
        private Transform _bf0StandbyContainer;
        private Transform _bf1StandbyContainer;

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
        private Tween _logAnimTween;

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
        [SerializeField] private CountdownRingUI _countdownRingUI;
        private Tween _timerTween;
        private Tween _timerPulseTween; // VFX-7f
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
        private Tween _boardFlashTween;

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
        private Tween _phasePulseTween;

        // Animated banner coroutine handle
        private Sequence _bannerAnimSeq;
        private Sequence _gameOverSeq;

        // End-turn button persistent pulse
        private Tween _endTurnPulseTween;
        private bool      _endTurnPulseActive;

        // React button ribbon — previous interactable state to detect transitions
        private bool _reactBtnWasInteractable;
        [SerializeField] private Button _reactBtn;  // 由 SceneBuilder wire 注入，用于呼吸动画
        private Tween _reactBtnBreathTween;  // 迅捷/反应 按钮可用时的呼吸动画
        // 按钮用的是紫色 sprite，这里只调亮度 tint（白=原色，灰=压暗），避免污染 sprite 色相
        private static readonly Color ReactBtnLit   = Color.white;                          // 有可用牌：满亮度
        private static readonly Color ReactBtnPeak  = new Color(1.3f, 1.3f, 1.3f, 1f);      // 呼吸峰值（HDR 略过曝）
        private static readonly Color ReactBtnDim   = new Color(0.45f, 0.45f, 0.45f, 1f);   // 无可用牌：灰色压暗

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
        private Action<UnitInstance> _onHeroHoverEnter;
        private Action<UnitInstance> _onHeroHoverExit;

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
        private Tween _runeHighlightPulseTween;
        private System.Action _pendingEquipOnDone; // H-1: unblocks tcs2 if GameUI destroyed mid-animation

        // ── Message log state ─────────────────────────────────────────────────
        private const int MAX_MESSAGES = 5;
        private readonly Queue<Text> _messageTexts = new Queue<Text>();

        // ── DOT-8: Screen shake ───────────────────────────────────────────────
        private const int   SHAKE_BIG_DAMAGE_THRESHOLD = 5; // damage >= this triggers shake
        private const float SHAKE_STRENGTH   = 12f;
        private const float SHAKE_DURATION   = 0.35f;
        private Tween _canvasShakeTween;

        // ── DOT-8: Slow motion (bullet time) ─────────────────────────────────
        private const float SLOW_SCALE       = 0.3f;
        private const float SLOW_IN_DUR      = 0.05f;
        private const float SLOW_HOLD        = 0.45f;
        private const float SLOW_OUT_DUR     = 0.4f;
        private Tween _slowMotionTween;

        // ── DOT-8: Turn sweep banner ──────────────────────────────────────────
        private Sequence _turnSweepSeq;
        private Text     _turnSweepText;  // created lazily

        // ── DOT-8: Deck shake ─────────────────────────────────────────────────
        private int   _lastPlayerDeckCount = -1;
        private Tween _deckShakeTween;

        // ── DOT-8: Mana fill stagger ─────────────────────────────────────────
        private Sequence _manaFillSeq;

        // ── DOT-8: Victory confetti ───────────────────────────────────────────
        private const int   CONFETTI_COUNT = 25;
        private readonly System.Collections.Generic.List<GameObject> _confettiObjs =
            new System.Collections.Generic.List<GameObject>();

        // ── DOT-8: Opponent card preview ─────────────────────────────────────
        private Sequence  _opponentPreviewSeq;
        private GameObject _opponentPreviewGO;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this; // DEV-27: singleton ref for CardDragHandler.FindCardViewInScene
            NukeAllDecorativeBorders();
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_bannerPanel != null) _bannerPanel.SetActive(false);
            if (_endTurnButton != null) _endTurnButton.onClick.AddListener(HandleEndTurn);
            if (_bf1Button != null) _bf1Button.onClick.AddListener(() => _onBFClicked?.Invoke(0));
            if (_bf2Button != null) _bf2Button.onClick.AddListener(() => _onBFClicked?.Invoke(1));

            // B8 full: 查找待命区槽位并挂 Button 以触发翻开打出
            if (_rootCanvas != null)
            {
                _bf0StandbyContainer = _rootCanvas.transform.Find("MainArea/BF0Standby");
                _bf1StandbyContainer = _rootCanvas.transform.Find("MainArea/BF1Standby");
                WireStandbyZoneClick(_bf0StandbyContainer, 0);
                WireStandbyZoneClick(_bf1StandbyContainer, 1);
            }
            if (_restartButton != null) _restartButton.onClick.AddListener(HandleRestart);
            if (_logToggleBtn != null) _logToggleBtn.onClick.AddListener(ToggleLog);
            if (_viewerCloseBtn != null) _viewerCloseBtn.onClick.AddListener(CloseViewer);
            if (_viewerPanel != null) _viewerPanel.SetActive(false);
            if (_timerDisplay != null) _timerDisplay.SetActive(false);
            if (_debugToggleBtn != null) _debugToggleBtn.onClick.AddListener(ToggleDebug);

            // Debug 面板默认隐藏，按键盘 0 切换显隐（临时开发用）
            if (_debugPanel != null) _debugPanel.SetActive(false);

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
            // DOT-8: new visual effects
            GameEventBus.OnTurnChanged += OnTurnChangedHandler;
        }

        // DEV-26: OnUnitDamaged/OnUnitDied drive animations — only subscribe when component is enabled
        private void OnEnable()
        {
            GameEventBus.OnUnitDamaged += OnSpellUnitDamaged;  // DEV-27: migrated from GameManager
            GameEventBus.OnUnitDied    += OnUnitDiedHandler;   // DEV-27: migrated from GameManager
            GameEventBus.OnUnitDamaged += OnBigDamageHandler;  // DOT-8: screen shake
            GameEventBus.OnUnitDied    += OnFatalHitHandler;   // DOT-8: slow motion
        }

        private void OnDisable()
        {
            GameEventBus.OnUnitDamaged -= OnSpellUnitDamaged;
            GameEventBus.OnUnitDied    -= OnUnitDiedHandler;
            GameEventBus.OnUnitDamaged -= OnBigDamageHandler;  // DOT-8
            GameEventBus.OnUnitDied    -= OnFatalHitHandler;   // DOT-8
            // Kill infinite-loop tweens on disable to prevent callbacks on disabled object
            TweenHelper.KillSafe(ref _timerTween);
            TweenHelper.KillSafe(ref _timerPulseTween);
            TweenHelper.KillSafe(ref _runeHighlightPulseTween);
            TweenHelper.KillSafe(ref _endTurnPulseTween);
            _endTurnPulseActive = false;
        }

        private void OnDestroy()
        {
            // Kill all DOTween tweens targeting this gameObject
            DOTween.Kill(gameObject);
            TweenHelper.KillSafe(ref _runeHighlightPulseTween);
            TweenHelper.KillSafe(ref _bannerAnimSeq);
            TweenHelper.KillSafe(ref _phasePulseTween);
            TweenHelper.KillSafe(ref _boardFlashTween);
            TweenHelper.KillSafe(ref _logAnimTween);
            TweenHelper.KillSafe(ref _timerTween);
            TweenHelper.KillSafe(ref _timerPulseTween);
            TweenHelper.KillSafe(ref _endTurnPulseTween);
            TweenHelper.KillSafe(ref _crHideSeq);
            TweenHelper.KillSafe(ref _gameOverSeq);
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
            // DOT-8
            GameEventBus.OnTurnChanged -= OnTurnChangedHandler;
            TweenHelper.KillSafe(ref _canvasShakeTween);
            TweenHelper.KillSafe(ref _slowMotionTween);
            TweenHelper.KillSafe(ref _deckShakeTween);
            TweenHelper.KillSafe(ref _manaFillSeq);
            TweenHelper.KillSafe(ref _opponentPreviewSeq);
            if (_opponentPreviewGO != null) { Destroy(_opponentPreviewGO); _opponentPreviewGO = null; } // H-3
            TweenHelper.KillSafe(ref _turnSweepSeq); // M-2: consistent with other seqs
            Time.timeScale = 1f; // ensure timescale restored if we're destroyed mid slow-motion
            // H-2: destroy any in-flight confetti GOs (their tweens target the GO, not gameObject)
            foreach (var c in _confettiObjs) { if (c != null) { DOTween.Kill(c); Destroy(c); } }
            _confettiObjs.Clear();
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

        /// <summary>
        /// VFX-6 fix: Returns the canvas-local position of the target container
        /// where a card will land after being played (base/hero/center).
        /// </summary>
        public Vector2 GetCardPlayTargetPos(FWTCG.Core.UnitInstance card, string owner)
        {
            Canvas rootCanvas = GetRootCanvas();
            if (rootCanvas == null) return Vector2.zero;

            // Determine the target container based on card type
            Transform target = null;
            if (card?.CardData != null && card.CardData.IsSpell)
            {
                // Spells don't land in a container — use screen center
                return Vector2.zero;
            }
            else if (card?.CardData != null && card.CardData.IsHero)
            {
                target = owner == FWTCG.Core.GameRules.OWNER_PLAYER
                    ? _playerHeroContainer : _enemyHeroContainer;
            }
            else
            {
                // Units and equipment land in base
                target = owner == FWTCG.Core.GameRules.OWNER_PLAYER
                    ? _playerBaseContainer : _enemyBaseContainer;
            }

            if (target == null) return Vector2.zero;
            var targetRT = target.GetComponent<RectTransform>();
            if (targetRT == null) return Vector2.zero;

            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(null, targetRT.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(), screenPt,
                rootCanvas.worldCamera, out Vector2 localPos);
            return localPos;
        }

        // ── DEV-15: Legend evolution flash ───────────────────────────────────

        private void OnLegendEvolved(string owner, int newLevel)
        {
            Text legendText = owner == FWTCG.Core.GameRules.OWNER_PLAYER
                ? _playerLegendText
                : _enemyLegendText;
            if (legendText == null) return;
            // 4 cycles of gold flash, each cycle = 0.15s to gold + 0.15s back = 0.3s × 4 = 1.2s total
            Color original = legendText.color;
            Color flash    = new Color(1f, 0.85f, 0.1f);
            legendText.DOColor(flash, 0.15f)
                .SetLoops(8, LoopType.Yoyo) // 8 half-cycles = 4 full flash cycles
                .OnComplete(() => { if (legendText != null) legendText.color = original; })
                .SetTarget(gameObject);
        }

        public void ShowBanner(string text)
        {
            if (_bannerPanel == null) return;
            if (_bannerText != null) _bannerText.text = text;
            TweenHelper.KillSafe(ref _bannerAnimSeq);

            _bannerPanel.SetActive(true);
            var cg = _bannerPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = _bannerPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            const float IN_DUR = 0.2f;
            const float OUT_DUR = 0.2f;
            _bannerAnimSeq = DOTween.Sequence()
                .Append(cg.DOFade(1f, IN_DUR))
                .AppendInterval(BANNER_DURATION)
                .Append(cg.DOFade(0f, OUT_DUR))
                .OnComplete(() =>
                {
                    if (_bannerPanel != null) _bannerPanel.SetActive(false);
                    _bannerAnimSeq = null;
                })
                .SetTarget(gameObject);
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        public void SetCallbacks(Action onEndTurn, Action<int> onBF,
                                 Action<UnitInstance> onUnit, Action<int, bool> onRune,
                                 Action<UnitInstance> onCardRightClick = null,
                                 Action<UnitInstance> onCardHoverEnter = null,
                                 Action<UnitInstance> onCardHoverExit  = null,
                                 Action<UnitInstance> onHeroHoverEnter = null,
                                 Action<UnitInstance> onHeroHoverExit  = null)
        {
            _onEndTurnClicked   = onEndTurn;
            _onBFClicked        = onBF;
            _onUnitClicked      = onUnit;
            _onRuneClicked      = onRune;
            _onCardRightClicked = onCardRightClick;
            _onCardHoverEnter   = onCardHoverEnter;
            _onCardHoverExit    = onCardHoverExit;
            _onHeroHoverEnter   = onHeroHoverEnter;
            _onHeroHoverExit    = onHeroHoverExit;
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

            TweenHelper.KillSafe(ref _runeHighlightPulseTween);
            if (_runeHighlightTap.Count > 0 || _runeHighlightRecycle.Count > 0)
                _runeHighlightPulseTween = CreateRuneHighlightPulseTween();
        }

        /// <summary>Clears all rune highlights and stops the pulse tween.</summary>
        public void ClearRuneHighlights()
        {
            TweenHelper.KillSafe(ref _runeHighlightPulseTween);
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

        // Blue = needs tap for mana  |  Red = needs recycle for sch
        public static readonly Color RuneTapFill    = new Color(0.15f, 0.50f, 1.0f, 1f);
        public static readonly Color RuneTapOutline = new Color(0.45f, 0.80f, 1.0f, 1f);
        public static readonly Color RuneRecFill    = new Color(1.0f,  0.15f, 0.15f, 1f);
        public static readonly Color RuneRecOutline = new Color(1.0f,  0.50f, 0.50f, 1f);
        public const float RUNE_PULSE_FREQ = 3.5f; // rad/s multiplier (~1.75 Hz)

        private Tween CreateRuneHighlightPulseTween()
        {
            // DOVirtual.Float sine loop: 0→1→0 at ~1.75 Hz
            return DOVirtual.Float(0f, 1f, 1f, _ =>
            {
                if (this == null) return; // guard: GameUI destroyed mid-tween
                float pulse = (Mathf.Sin(Time.time * RUNE_PULSE_FREQ) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(0.25f, 1.0f, pulse);
                if (_playerRuneContainer != null)
                {
                    foreach (int idx in _runeHighlightTap)
                        SetRuneBorderGlow(idx, RuneTapFill, RuneTapOutline, alpha);
                    foreach (int idx in _runeHighlightRecycle)
                        SetRuneBorderGlow(idx, RuneRecFill, RuneRecOutline, alpha);
                }
            }).SetLoops(-1, LoopType.Restart).SetTarget(gameObject);
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
            RefreshStandbyZones(gs); // B8 full
            RefreshScoreTrack(gs);
            RefreshPileCounts(gs);
            RefreshBFControlBadges(gs);
            UpdateBFGlows(gs);        // DEV-18
            UpdateBFCardArt(gs);      // DEV-18
            RefreshInfoStrips(gs);
            RefreshActionButtons(gs.Phase, gs.Turn);

            // 迅捷/反应 按钮状态：有可用牌 → 亮+呼吸；无可用牌 → 暗（仍可点击）
            var gm = GameManager.Instance;
            if (gm != null) NotifyReactButtonState(gm.HasAffordableReactive());
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
                TweenHelper.KillSafe(ref _phasePulseTween);
                var rt = _roundPhaseText.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.localScale = Vector3.one;
                    _phasePulseTween = rt.DOScale(PHASE_PULSE_PEAK, PHASE_PULSE_DURATION * 0.5f)
                        .SetEase(Ease.Linear)
                        .SetLoops(2, LoopType.Yoyo)
                        .OnComplete(() => { if (rt != null) rt.localScale = Vector3.one; _phasePulseTween = null; })
                        .SetTarget(gameObject);
                }
            }
        }

        public const float PHASE_PULSE_DURATION = 0.4f;
        public const float PHASE_PULSE_PEAK     = 1.18f;

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
                            _onCardHoverEnter, _onCardHoverExit, playEnterAnim: true);
            // Enemy hand (face-down — show count only)
            RefreshEnemyHand(_enemyHandContainer, gs.EHand.Count);

            // Apply fan rotation to both hands (Pencil design: cards arc ±14°)
            ApplyHandFan(_playerHandContainer, isPlayer: true);
            ApplyHandFan(_enemyHandContainer, isPlayer: false);
        }

        // ── Fan reference positions from GameBoard_Pencil scene ────────────────
        // 7 slots: L14 → L9 → L5 → Center → R5 → R9 → R14
        // zone-local anchoredPosition (px) measured at 1920×1080 canvas.
        // Player hand: bottom of screen, cards peek upward.
        // Derived from new5.pen card centers converted to PlayerHandZone local space.
        private static readonly Vector2[] s_PlayerFanPos =
        {
            new Vector2(-261f, -28f),  // L14
            new Vector2(-167f,   1f),  // L9
            new Vector2( -93f,  19f),  // L5
            new Vector2(   0f,  29f),  // Center
            new Vector2(  92f,  27f),  // R5
            new Vector2( 166f,  18f),  // R9
            new Vector2( 257f,  -1f),  // R14
        };
        private static readonly float[] s_PlayerFanRot = { 14f, 9f, 5f, 0f, -5f, -9f, -14f };

        // Enemy hand: top of screen, cards peek downward (zone center above canvas).
        private static readonly Vector2[] s_EnemyFanPos =
        {
            new Vector2(-255f,  34f),  // L14
            new Vector2(-163f,   2f),  // L9
            new Vector2( -87f, -20f),  // L5
            new Vector2(   6f, -34f),  // Center
            new Vector2(  99f, -30f),  // R5
            new Vector2( 167f, -13f),  // R9
            new Vector2( 258f,   7f),  // R14
        };
        private static readonly float[] s_EnemyFanRot = { -14f, -9f, -5f, 0f, 5f, 9f, 14f };

        private static void ApplyHandFan(Transform container, bool isPlayer)
        {
            if (container == null) return;

            // Collect only CardView children (skip structural elements)
            var cards = new List<Transform>();
            for (int i = 0; i < container.childCount; i++)
            {
                var ch = container.GetChild(i);
                if (ch.GetComponent<CardView>() != null) cards.Add(ch);
            }
            int n = cards.Count;
            if (n == 0) return;

            Vector2[] refPos  = isPlayer ? s_PlayerFanPos : s_EnemyFanPos;
            float[]   refRot  = isPlayer ? s_PlayerFanRot  : s_EnemyFanRot;
            int       refLast = refPos.Length - 1; // 6

            for (int i = 0; i < n; i++)
            {
                var child = cards[i];
                var rt    = child.GetComponent<RectTransform>();
                if (rt == null) continue;

                // Fix card size to match Pencil design (110×154px), point-anchored at center
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(110f, 154f);
                rt.pivot     = new Vector2(0.5f, 0.5f);

                // Fixed step = 1 ref unit (~86px) → cards always overlap ~24px
                // regardless of count. Only stretch beyond 1 unit when n > 7.
                float step       = n <= refLast + 1 ? 1f : (float)refLast / (n - 1);
                float startParam = refLast / 2f - (n - 1) / 2f * step;
                float param      = Mathf.Clamp(startParam + i * step, 0f, refLast);

                // Interpolate between adjacent reference slots
                int     lo  = Mathf.Min(Mathf.FloorToInt(param), refLast - 1);
                float   t   = param - lo;
                Vector2 pos = Vector2.Lerp(refPos[lo], refPos[lo + 1], t);
                float   rot = Mathf.Lerp(refRot[lo],  refRot[lo + 1],  t);

                var cv = child.GetComponent<CardView>();
                if (cv != null)
                    cv.SetHandFan(rot, pos.x, pos.y);
                else
                {
                    rt.anchoredPosition = pos;
                    rt.localEulerAngles = new Vector3(0f, 0f, rot);
                }
            }
        }



        private void RefreshBases(GameState gs)
        {
            RefreshUnitList(_playerBaseContainer, gs.PBase, true, _onUnitClicked);
            RefreshUnitList(_enemyBaseContainer, gs.EBase, false, _onUnitClicked);

            // VFX-4: Apply battlefield visuals (shadow, micro-rotation) to base cards too
            ApplyBFVisuals(_playerBaseContainer);
            ApplyBFVisuals(_enemyBaseContainer);
        }

        private void RefreshBattlefields(GameState gs)
        {
            RefreshUnitList(_bf1PlayerContainer, gs.BF[0].PlayerUnits, true, _onUnitClicked);
            RefreshUnitList(_bf1EnemyContainer, gs.BF[0].EnemyUnits, false, _onUnitClicked);
            RefreshUnitList(_bf2PlayerContainer, gs.BF[1].PlayerUnits, true, _onUnitClicked);
            RefreshUnitList(_bf2EnemyContainer, gs.BF[1].EnemyUnits, false, _onUnitClicked);

            // VFX-4: Apply battlefield visuals (shadow, micro-rotation, idle FX) to units on field
            ApplyBFVisuals(_bf1PlayerContainer);
            ApplyBFVisuals(_bf1EnemyContainer);
            ApplyBFVisuals(_bf2PlayerContainer);
            ApplyBFVisuals(_bf2EnemyContainer);

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

        /// <summary>VFX-4: Apply battlefield visuals to all CardViews in a container.</summary>
        private static void ApplyBFVisuals(Transform container)
        {
            if (container == null) return;
            for (int i = 0; i < container.childCount; i++)
            {
                var cv = container.GetChild(i).GetComponent<CardView>();
                if (cv != null) cv.ApplyBattlefieldVisuals();
            }
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

            // VFX-7: wire legend hover glow (green for player, red for enemy)
            WireLegendHoverGlow(_playerLegendContainer, true);
            WireLegendHoverGlow(_enemyLegendContainer, false);

            // Wire right-click on legend containers for detail popup
            WireLegendRightClick(_playerLegendContainer, gs.PLegend);
            WireLegendRightClick(_enemyLegendContainer, gs.ELegend);
        }

        // VFX-7: 传奇卡悬停 glow 已禁用（用户嫌绿色干扰），保留函数入口只做隐藏 overlay
        private readonly System.Collections.Generic.HashSet<int> _legendGlowWired = new();
        private void WireLegendHoverGlow(Transform container, bool isPlayer)
        {
            if (container == null) return;
            var glowT = container.Find("LegendGlowOverlay");
            if (glowT == null) return;
            // 直接隐藏，不挂 hover 监听
            glowT.gameObject.SetActive(false);
        }

        public const float LEGEND_GLOW_SPEED = 4f;
        private static void FadeLegendGlow(Image img, float targetAlpha)
        {
            if (img == null) return;
            DOTween.Kill(img); // kill previous fade on this image
            float currentAlpha = img.color.a;
            float distance = Mathf.Abs(targetAlpha - currentAlpha);
            float duration = distance / LEGEND_GLOW_SPEED;
            if (duration < 0.01f) { var c = img.color; img.color = new Color(c.r, c.g, c.b, targetAlpha); return; }
            img.DOFade(targetAlpha, duration).SetEase(Ease.Linear).SetTarget(img);
        }

        private readonly System.Collections.Generic.HashSet<int> _legendRightClickWired = new();
        private void WireLegendRightClick(Transform container, LegendInstance legend)
        {
            if (container == null || legend == null || legend.DisplayData == null) return;
            if (_cardDetailPopup == null) return;
            int id = container.GetInstanceID();
            if (_legendRightClickWired.Contains(id)) return;
            _legendRightClickWired.Add(id);

            var et = container.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (et == null) et = container.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
            entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
            entry.callback.AddListener((data) =>
            {
                if (CardDragHandler.BlockPointerEvents) return;
                if (Input.GetMouseButton(0)) return;
                var pointerData = (UnityEngine.EventSystems.PointerEventData)data;
                if (pointerData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                {
                    var tempUnit = new UnitInstance(0, legend.DisplayData, legend.Owner);
                    _cardDetailPopup.Show(tempUnit);
                }
            });
            et.triggers.Add(entry);

            var img = container.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
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
                    cv.Setup(hero, isPlayer, isPlayer ? _onUnitClicked : null, _onCardRightClicked,
                             isPlayer ? _onHeroHoverEnter : null,
                             isPlayer ? _onHeroHoverExit  : null);
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

            // 传奇卡：彻底删除 LegendFrame sprite 和 LegendArt 上的 Outline
            Transform oldFrame = legendContainer.Find("LegendFrame");
            if (oldFrame != null)
            {
                if (Application.isPlaying) Destroy(oldFrame.gameObject);
                else DestroyImmediate(oldFrame.gameObject);
            }
            Transform legendArtT = legendContainer.Find("LegendArt");
            var legendArtImg = legendArtT != null ? legendArtT.GetComponent<Image>() : null;
            if (legendArtImg != null)
            {
                foreach (var o in legendArtImg.GetComponents<Outline>())
                    o.enabled = false;
            }

            // VFX-7: update description text from CardData
            if (legend.DisplayData != null)
            {
                Transform descT = legendContainer.Find("LegendDesc");
                if (descT != null)
                {
                    var descText = descT.GetComponent<Text>();
                    if (descText != null)
                        descText.text = legend.DisplayData.Description ?? "";
                }

                // Also update name text
                Transform nameT = legendContainer.Find("LegendText");
                if (nameT == null) nameT = legendContainer.Find("EnemyLegendText");
                if (nameT != null)
                {
                    var nameText = nameT.GetComponent<Text>();
                    if (nameText != null)
                        nameText.text = legend.DisplayData.CardName ?? "";
                }
            }
        }

        // ── Unit list renderer ────────────────────────────────────────────────

        private void RefreshUnitList(Transform container, List<UnitInstance> units,
                                     bool isPlayer, Action<UnitInstance> onClick,
                                     int currentMana = -1,
                                     Action<UnitInstance> onHoverEnter = null,
                                     Action<UnitInstance> onHoverExit  = null,
                                     bool playEnterAnim = false)
        {
            if (container == null) return;
            if (units == null) units = new List<UnitInstance>();

            // Collect only CardView children (skip structural children: borders, labels, slot groups)
            var cardChildren = new List<Transform>();
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child.GetComponent<FWTCG.UI.CardView>() != null)
                    cardChildren.Add(child);
            }

            int existing = cardChildren.Count;
            int needed = units.Count;

            // Remove excess CardView children
            for (int i = existing - 1; i >= needed; i--)
                Destroy(cardChildren[i].gameObject);

            // Add missing children
            for (int i = existing; i < needed; i++)
            {
                if (_cardViewPrefab != null)
                    Instantiate(_cardViewPrefab, container);
            }

            // Re-collect CardView children after add/remove
            cardChildren.Clear();
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child.GetComponent<FWTCG.UI.CardView>() != null)
                    cardChildren.Add(child);
            }

            // Update all CardView children in-place
            for (int i = 0; i < needed && i < cardChildren.Count; i++)
            {
                CardView cv = cardChildren[i].GetComponent<CardView>();
                if (cv != null)
                {
                    UnitInstance u = units[i];
                    // Reset CanvasGroup alpha — HideEquipCardInBase sets alpha=0 for fly
                    // animation; if this CardView is reused for a different unit the alpha
                    // must be restored or the new card appears transparent (white).
                    // BUT skip if the card is currently being dragged (hidden by CardDragHandler).
                    var dragH = cv.GetComponent<CardDragHandler>();
                    bool hiddenByDrag = dragH != null && dragH.IsHiddenDuringDrag;
                    var cg = cv.GetComponent<UnityEngine.CanvasGroup>();
                    if (cg != null && !hiddenByDrag) cg.alpha = 1f;
                    cv.Setup(u, isPlayer, onClick, _onCardRightClicked, onHoverEnter, onHoverExit,
                             playEnterAnim: playEnterAnim);
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

            // Only count CardView children (skip structural: borders, labels)
            var cardChildren = new List<Transform>();
            for (int c = 0; c < container.childCount; c++)
            {
                var ch = container.GetChild(c);
                if (ch.GetComponent<FWTCG.UI.CardView>() != null)
                    cardChildren.Add(ch);
            }
            int existing = cardChildren.Count;

            // Remove excess CardView children
            for (int i = existing - 1; i >= count; i--)
                Destroy(cardChildren[i].gameObject);

            // Add missing
            for (int i = existing; i < count; i++)
            {
                if (_cardViewPrefab != null)
                {
                    GameObject go = Instantiate(_cardViewPrefab, container);
                    go.name = $"EnemyCard_{i}";
                }
            }

            // Re-collect and update CardView children
            cardChildren.Clear();
            for (int c = 0; c < container.childCount; c++)
            {
                var ch = container.GetChild(c);
                if (ch.GetComponent<FWTCG.UI.CardView>() != null)
                    cardChildren.Add(ch);
            }
            for (int i = 0; i < count && i < cardChildren.Count; i++)
            {
                CardView cv = cardChildren[i].GetComponent<CardView>();
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

            // Count only rune children (skip structural: borders, labels, RuneRow)
            var runeChildren = new List<Transform>();
            for (int c = 0; c < container.childCount; c++)
            {
                var ch = container.GetChild(c);
                // Rune prefabs have a "RuneCircle" child; structural elements don't
                if (ch.Find("RuneCircle") != null || ch.GetComponent<FWTCG.UI.CardView>() != null)
                    runeChildren.Add(ch);
            }
            int existing = runeChildren.Count;
            int needed = runes.Count;

            // Remove excess rune objects
            for (int i = existing - 1; i >= needed; i--)
                Destroy(runeChildren[i].gameObject);

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

                // Entrance flash: scale-pop + bright white overlay fade
                PlayRuneEntranceFlash(newGo);
            }

            // Re-collect rune children after add/remove
            runeChildren.Clear();
            for (int c = 0; c < container.childCount; c++)
            {
                var ch = container.GetChild(c);
                if (ch.Find("RuneCircle") != null)
                    runeChildren.Add(ch);
            }

            // Update all rune objects in-place
            for (int i = 0; i < needed && i < runeChildren.Count; i++)
            {
                int idx = i;
                RuneInstance r = runes[i];
                GameObject go = runeChildren[i].gameObject;
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
            if (entry != null)
            {
                Color original = entry.color;
                entry.color = LOG_FLASH_GOLD;
                entry.DOColor(original, LOG_FLASH_DURATION).SetEase(Ease.Linear).SetTarget(entry);
            }
        }

        public static readonly Color LOG_FLASH_GOLD = new Color(0.95f, 0.82f, 0.40f, 1f);
        public const float LOG_FLASH_DURATION = 0.8f;

        // ── Game over overlay ─────────────────────────────────────────────────

        public void ShowGameOver(string msg)
        {
            if (_gameOverPanel == null) return;
            _gameOverPanel.SetActive(true);
            if (_gameOverText != null) _gameOverText.text = msg;
            if (_endTurnButton != null) _endTurnButton.interactable = false;

            // VFX-7c: detect win/lose and enhance
            bool isWin = msg != null && (msg.Contains("胜") || msg.Contains("Win") || msg.Contains("赢"));
            CreateGameOverSequence(_gameOverPanel, isWin);
        }

        public const float GAMEOVER_FADE_DUR = 0.5f;
        public const float GAMEOVER_WIN_SCALE_DUR = 0.4f;

        // VFX-7c: enhanced win/lose screen
        private void CreateGameOverSequence(GameObject panel, bool isWin)
        {
            TweenHelper.KillSafe(ref _gameOverSeq);
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // Set visual mood
            var bg = panel.GetComponent<Image>();
            if (bg != null)
                bg.color = isWin
                    ? new Color(0.05f, 0.02f, 0f, 0.90f)
                    : new Color(0.05f, 0.05f, 0.08f, 0.92f);

            if (_gameOverText != null)
                _gameOverText.color = isWin ? GameColors.Gold : new Color(0.6f, 0.6f, 0.65f, 1f);

            _gameOverSeq = DOTween.Sequence();
            _gameOverSeq.Append(cg.DOFade(1f, GAMEOVER_FADE_DUR));

            // VFX-7c: victory text scale pop
            if (isWin && _gameOverText != null)
            {
                var txtRT = _gameOverText.GetComponent<RectTransform>();
                if (txtRT != null)
                {
                    txtRT.localScale = Vector3.one * 0.3f;
                    _gameOverSeq.Append(txtRT.DOScale(1.1f, GAMEOVER_WIN_SCALE_DUR).SetEase(Ease.OutElastic));
                    _gameOverSeq.Append(txtRT.DOScale(1f, 0.15f).SetEase(Ease.InOutQuad));
                }
            }
            // DOT-8: victory confetti
            if (isWin) _gameOverSeq.AppendCallback(SpawnConfetti);
            _gameOverSeq.SetTarget(gameObject);
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void HandleEndTurn()
        {
            // DOT-8: squash & stretch on click
            if (_endTurnButton != null)
                TweenHelper.PunchScaleUI(_endTurnButton.transform, 0.15f, 0.25f, 1);
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
            // DEV-19: detect score increase → trigger pulse on newly-lit circles.
            // Save old values BEFORE updating cache so we know which circles to pulse.
            int prevPScore = _cachedPScore;
            int prevEScore = _cachedEScore;
            bool pScoreUp  = gs.PScore > _cachedPScore && _cachedPScore >= 0;
            bool eScoreUp  = gs.EScore > _cachedEScore && _cachedEScore >= 0;
            _cachedPScore  = gs.PScore;
            _cachedEScore  = gs.EScore;

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
                // Pulse every circle that lit up this tick (handles multi-point score jumps).
                if (pScoreUp)
                    for (int s = prevPScore; s < gs.PScore; s++)
                        if (s >= 0 && s < _playerScoreCircles.Length)
                            TriggerScorePulse(_playerScoreCircles[s]);
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
                // Pulse every circle that lit up this tick.
                if (eScoreUp)
                    for (int s = prevEScore; s < gs.EScore; s++)
                        if (s >= 0 && s < _enemyScoreCircles.Length)
                            TriggerScorePulse(_enemyScoreCircles[s]);
            }
        }

        // ── Pile count refresh (DEV-9) ───────────────────────────────────────

        public void RefreshPileCounts(GameState gs)
        {
            // DOT-8: deck shake when player deck runs low (≤ 2 cards)
            int newDeckCount = gs.PDeck.Count;
            if (newDeckCount != _lastPlayerDeckCount && newDeckCount <= 2 && newDeckCount > 0
                && _playerDeckCountText != null)
            {
                var deckRT = _playerDeckCountText.GetComponent<RectTransform>();
                if (deckRT != null)
                {
                    TweenHelper.KillSafe(ref _deckShakeTween);
                    _deckShakeTween = deckRT.DOShakeAnchorPos(0.4f, 6f, 10, 90f, false, true)
                        .SetTarget(gameObject)
                        .OnComplete(() => _deckShakeTween = null);
                }
            }
            _lastPlayerDeckCount = newDeckCount;

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
        public const float BOARD_FLASH_HALF = 0.425f;

        private void OnCardPlayedHandler(UnitInstance unit, string owner)
        {
            // DOT-8: opponent card preview ghost
            if (owner != FWTCG.Core.GameRules.OWNER_PLAYER)
                PlayOpponentCardPreview(unit);

            if (_boardFlashOverlay == null) return;
            TweenHelper.KillSafe(ref _boardFlashTween);

            Color flashCol = owner == FWTCG.Core.GameRules.OWNER_PLAYER
                ? new Color(0.78f, 0.67f, 0.43f, 0.18f)
                : new Color(0.97f, 0.30f, 0.30f, 0.12f);
            Color clear = new Color(flashCol.r, flashCol.g, flashCol.b, 0f);

            _boardFlashOverlay.color = clear;
            _boardFlashTween = DOTween.Sequence()
                .Append(_boardFlashOverlay.DOColor(flashCol, BOARD_FLASH_HALF))
                .Append(_boardFlashOverlay.DOColor(clear, BOARD_FLASH_HALF))
                .OnComplete(() => _boardFlashTween = null)
                .SetTarget(gameObject);
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

        // 迅捷/反应 按钮状态：有可用牌 = 亮紫 + 呼吸动画；无可用牌 = 暗紫但仍可点击
        // 允许玩家在没牌时也疯狂点击，抢到触发窗口打开的瞬间（GameManager.OnReactClicked 每次都会重新检查）
        public void NotifyReactButtonState(bool hasAffordableReactive)
        {
            _reactBtnWasInteractable = hasAffordableReactive;
            if (_reactBtn == null) return;

            // 按钮永远可点击，不锁住输入
            _reactBtn.interactable = true;

            var img = _reactBtn.GetComponent<Image>();
            if (img == null) return;

            // 清掉旧的呼吸 tween
            TweenHelper.KillSafe(ref _reactBtnBreathTween);

            if (hasAffordableReactive)
            {
                // 亮起 + 循环呼吸（Yoyo 在亮色和峰值之间来回）
                img.color = ReactBtnLit;
                _reactBtnBreathTween = img.DOColor(ReactBtnPeak, 0.8f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetTarget(_reactBtn.gameObject);
            }
            else
            {
                // 暗淡静态
                img.color = ReactBtnDim;
            }
        }

        // ── Log panel toggle (DEV-10) ────────────────────────────────────────

        public const float LOG_TOGGLE_DURATION = 0.3f;

        public void ToggleLog()
        {
            _logCollapsed = !_logCollapsed;

            if (_logToggleText != null)
                _logToggleText.text = _logCollapsed ? ">" : "<";

            TweenHelper.KillSafe(ref _logAnimTween);

            bool collapse = _logCollapsed;
            float startOffset = collapse ? -200f : 0f;
            float endOffset = collapse ? 0f : -200f;

            if (!collapse && _logPanel != null)
                _logPanel.SetActive(true);

            _logAnimTween = DOVirtual.Float(startOffset, endOffset, LOG_TOGGLE_DURATION, offset =>
            {
                if (_boardWrapperOuter != null)
                    _boardWrapperOuter.offsetMax = new Vector2(offset, _boardWrapperOuter.offsetMax.y);
                if (_playerHandZoneRT != null)
                    _playerHandZoneRT.offsetMax = new Vector2(offset, _playerHandZoneRT.offsetMax.y);
                if (_enemyHandZoneRT != null)
                    _enemyHandZoneRT.offsetMax = new Vector2(offset, _enemyHandZoneRT.offsetMax.y);
                if (_logToggleBtn != null)
                {
                    var btnRT = _logToggleBtn.GetComponent<RectTransform>();
                    if (btnRT != null)
                        btnRT.anchoredPosition = new Vector2(offset - 4f, btnRT.anchoredPosition.y);
                }
            }).SetEase(Ease.InOutQuad).OnComplete(() =>
            {
                if (collapse && _logPanel != null)
                    _logPanel.SetActive(false);
            }).SetTarget(gameObject);
        }

        // ── Debug panel toggle (DEV-10) ──────────────────────────────────────

        // 按键盘 0（主键区或小键盘）整体切换 Debug 面板显隐（直接全展开，无折叠态）
        private void Update()
        {
            if (_debugPanel == null) return;
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                bool show = !_debugPanel.activeSelf;
                _debugPanel.SetActive(show);
                if (show)
                {
                    // 强制全展开：显示所有子按钮 + 恢复完整尺寸
                    _debugCollapsed = false;
                    for (int i = 0; i < _debugPanel.transform.childCount; i++)
                        _debugPanel.transform.GetChild(i).gameObject.SetActive(true);
                    var rt = _debugPanel.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(130f, 360f);
                }
            }
        }

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
                            cv.Setup(cards[i], true, null, _onCardRightClicked, isDiscardView: true);
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

            // Old visual display is hidden; countdown ring drives the visual
            if (_timerDisplay != null) _timerDisplay.SetActive(false);
            _countdownRingUI?.ResetRing();
            UpdateTimerDisplay();

            TweenHelper.KillSafe(ref _timerTween);
            _timerTween = DOVirtual.Float(_timerSeconds, 0f, _timerSeconds, v =>
            {
                // 整数跳变：用于文字显示和颜色
                int newSec = Mathf.CeilToInt(v);
                if (newSec != _timerSeconds)
                {
                    _timerSeconds = newSec;
                    UpdateTimerDisplay();
                }
                // 浮点驱动宝石环：每帧连续，宝石出现间隔完全均匀
                _countdownRingUI?.SetProgress(1f - v / 30f);
            }).SetEase(Ease.Linear).OnComplete(() =>
            {
                _timerSeconds = 0;
                UpdateTimerDisplay();
                _countdownRingUI?.SetProgress(1f);
                _timerTween = null;
                if (_timerDisplay != null) _timerDisplay.SetActive(false);
                _onTimerExpired?.Invoke();
            }).SetTarget(gameObject);
        }

        public void ClearTurnTimer()
        {
            TweenHelper.KillSafe(ref _timerTween);
            TweenHelper.KillSafe(ref _timerPulseTween);
            if (_timerDisplay != null) _timerDisplay.SetActive(false);
            _countdownRingUI?.ResetRing();
            _onTimerExpired = null;
        }

        private void UpdateTimerDisplay()
        {
            if (_timerText != null)
                _timerText.text = _timerSeconds.ToString();

            // Color gradient: green → yellow → red
            // 注：宝石环由 StartTurnTimer 里的浮点回调直接驱动，这里不重复调用
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
                _timerFill.fillAmount = _timerSeconds / 30f;
                _timerFill.color = timerColor;
            }

            // Text color matches ring
            if (_timerText != null)
                _timerText.color = timerColor;

            // VFX-7f: pulse animation when <10s
            if (_timerSeconds <= 10 && _timerSeconds > 0 && _timerText != null)
            {
                if (_timerPulseTween == null || !_timerPulseTween.IsActive())
                    _timerPulseTween = CreateTimerPulseTween();
            }
            else if (_timerPulseTween != null)
            {
                TweenHelper.KillSafe(ref _timerPulseTween);
                if (_timerText != null)
                    _timerText.transform.localScale = Vector3.one;
            }
        }

        // VFX-7f: scale pulse for timer text when <10s
        public const float TIMER_PULSE_FREQ = 2f; // Hz
        public const float TIMER_PULSE_SCALE = 1.15f;
        private Tween CreateTimerPulseTween()
        {
            var tr = _timerText.transform;
            return DOVirtual.Float(0f, 1f, 1f, _ =>
            {
                if (tr == null) return;
                float s = 1f + (TIMER_PULSE_SCALE - 1f) *
                    ((Mathf.Sin(Time.time * TIMER_PULSE_FREQ * Mathf.PI * 2f) + 1f) * 0.5f);
                tr.localScale = new Vector3(s, s, 1f);
            }).SetLoops(-1, LoopType.Restart).SetTarget(gameObject);
        }

        // ── Combat result display (DEV-10) ───────────────────────────────────
        [SerializeField] private GameObject _combatResultPanel;
        [SerializeField] private Text _crAttackerText;
        [SerializeField] private Text _crDefenderText;
        [SerializeField] private Text _crVsText;
        [SerializeField] private Text _crOutcomeText;
        [SerializeField] private Text _crBfNameText;

        private Sequence _crHideSeq;

        public void ShowCombatResult(Systems.CombatSystem.CombatResult result)
        {
            if (_combatResultPanel == null) return;

            // Stop any in-progress hide
            TweenHelper.KillSafe(ref _crHideSeq);

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
            _crHideSeq = DOTween.Sequence()
                .Append(cg.DOFade(1f, CR_FADE_IN))
                .AppendInterval(CR_STAY)
                .Append(cg.DOFade(0f, CR_FADE_OUT))
                .OnComplete(() =>
                {
                    if (_combatResultPanel != null) _combatResultPanel.SetActive(false);
                    _crHideSeq = null;
                })
                .SetTarget(gameObject);
        }

        public const float CR_FADE_IN  = 0.2f;
        public const float CR_STAY     = 3.5f;
        public const float CR_FADE_OUT = 0.3f;

        // ── B8 full: 待命区点击（翻开打出）───────────────────────────────────

        /// <summary>Callback triggered when player clicks a face-up standby slot they want to flip.</summary>
        private Action<int> _onStandbyClicked;

        public void SetStandbyClickCallback(Action<int> callback)
        {
            _onStandbyClicked = callback;
        }

        /// <summary>给 BF 待命区挂 Button → 点击触发翻开回调。</summary>
        private void WireStandbyZoneClick(Transform zone, int bfId)
        {
            if (zone == null) return;
            var go = zone.gameObject;
            // 确保有 Image 作为 Button 的 target graphic（标签文本已有 Image 但不点中）
            var img = go.GetComponent<UnityEngine.UI.Image>();
            if (img == null)
            {
                img = go.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0f, 0f, 0f, 0.01f); // 几乎透明但接收射线
            }
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => _onStandbyClicked?.Invoke(bfId));
        }

        /// <summary>
        /// 刷新待命区视觉：有面朝下牌时用 CardBackManager 的卡背 sprite 显示；
        /// 可翻开时加绿色脉冲提示。空槽时透明。
        /// </summary>
        public void RefreshStandbyZones(FWTCG.Core.GameState gs)
        {
            RefreshOneStandbyZone(_bf0StandbyContainer, 0, gs);
            RefreshOneStandbyZone(_bf1StandbyContainer, 1, gs);
        }

        private void RefreshOneStandbyZone(Transform zone, int bfId, FWTCG.Core.GameState gs)
        {
            if (zone == null || gs == null || bfId >= gs.BF.Length) return;
            var bf = gs.BF[bfId];

            // 找到或创建专用的卡背 Image 子节点（避免覆盖 zone 自己的点击目标 Image）
            const string cardBackName = "StandbyCardBack";
            Transform cardBackTf = zone.Find(cardBackName);
            UnityEngine.UI.Image cardBackImg = null;
            if (cardBackTf != null) cardBackImg = cardBackTf.GetComponent<UnityEngine.UI.Image>();

            // 判定：优先显示玩家面朝下牌；其次 AI 的
            var card = bf.PlayerStandby ?? bf.EnemyStandby;
            bool ownedByPlayer = bf.PlayerStandby != null;
            bool canFlipNow = ownedByPlayer && card != null && card.StandbyReadyToFlip;

            if (card == null)
            {
                // 空槽 — 隐藏 CardBack 子节点
                if (cardBackImg != null) cardBackImg.enabled = false;
                return;
            }

            // 懒创建子节点
            if (cardBackImg == null)
            {
                var go = new GameObject(cardBackName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(zone, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.1f, 0.05f);
                rt.anchorMax = new Vector2(0.9f, 0.95f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                cardBackImg = go.GetComponent<UnityEngine.UI.Image>();
                cardBackImg.raycastTarget = false; // 点击走外层 zone
                cardBackImg.preserveAspect = true;
            }

            cardBackImg.enabled = true;
            cardBackImg.sprite = CardBackManager.GetCardBackSprite();

            // 色调：可翻开 = 明亮 + 绿色光晕；不可翻 = 微暗
            if (canFlipNow)
                cardBackImg.color = new Color(1f, 1f, 1f, 1f);
            else
                cardBackImg.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
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
        public const float SCORE_PULSE_HALF = 0.9f;
        public const float SCORE_PULSE_PEAK = 1.15f;

        private void TriggerScorePulse(Image circle)
        {
            if (circle == null) return;
            var rt = circle.GetComponent<RectTransform>();
            if (rt != null)
            {
                DOTween.Kill(rt); // kill any previous pulse on this circle
                Vector3 orig = rt.localScale;
                rt.DOScale(orig * SCORE_PULSE_PEAK, SCORE_PULSE_HALF)
                    .SetEase(Ease.OutElastic)
                    .SetLoops(2, LoopType.Yoyo)
                    .OnComplete(() => { if (rt != null) rt.localScale = orig; })
                    .SetTarget(rt);
            }
            SpawnScoreRing(circle);
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

            if (ringImg != null && rt != null)
            {
                var ringGo = ringImg.gameObject;
                var seq = DOTween.Sequence();
                seq.Append(rt.DOScale(rt.localScale * 2.5f, SCORE_RING_DURATION).SetEase(Ease.Linear));
                seq.Join(ringImg.DOFade(0f, SCORE_RING_DURATION).SetEase(Ease.Linear));
                seq.OnComplete(() => Destroy(ringGo));
                seq.SetTarget(ringGo);
            }
        }

        public const float SCORE_RING_DURATION = 2f;

        // ── DEV-19: End-turn button persistent pulse ───────────────────────────

        public const float ENDTURN_PULSE_PERIOD = 2f;
        public const float ENDTURN_PULSE_MIN_ALPHA = 0.60f;

        private void UpdateEndTurnPulse(bool active)
        {
            if (active == _endTurnPulseActive) return;
            _endTurnPulseActive = active;

            if (active)
            {
                if ((_endTurnPulseTween == null || !_endTurnPulseTween.IsActive()) && _endTurnButton != null)
                {
                    var cg = _endTurnButton.GetComponent<CanvasGroup>();
                    if (cg == null) cg = _endTurnButton.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 1f;
                    // Start from 1→0.6 (dim first), matching original coroutine behavior
                    float halfPeriod = ENDTURN_PULSE_PERIOD * 0.5f;
                    _endTurnPulseTween = DOTween.To(() => cg.alpha, x => cg.alpha = x,
                        ENDTURN_PULSE_MIN_ALPHA, halfPeriod)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetTarget(cg);
                }
            }
            else
            {
                TweenHelper.KillSafe(ref _endTurnPulseTween);
                if (_endTurnButton != null)
                {
                    var cg = _endTurnButton.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
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
        public const float REACT_REVEAL_DUR = 0.25f;
        public const float REACT_PULSE_DUR  = 2f;
        public const float REACT_PULSE_AMP  = 0.06f;

        public void PlayReactRibbonReveal(Button reactBtn)
        {
            if (reactBtn == null) return;
            var rt = reactBtn.GetComponent<RectTransform>();
            if (rt == null) return;

            Vector3 orig = rt.localScale;
            Vector3 flat = new Vector3(0f, orig.y, orig.z);
            rt.localScale = flat;

            var seq = DOTween.Sequence();
            // Scale-X reveal from 0 → orig
            seq.Append(DOVirtual.Float(0f, 1f, REACT_REVEAL_DUR, v =>
            {
                if (rt != null) rt.localScale = new Vector3(orig.x * v, orig.y, orig.z);
            }));
            // Brief 1-cycle scale pulse
            seq.Append(DOVirtual.Float(0f, 1f, REACT_PULSE_DUR, v =>
            {
                if (rt != null)
                {
                    float sin = Mathf.Sin(v * Mathf.PI * 2f);
                    rt.localScale = orig * (1f + sin * REACT_PULSE_AMP);
                }
            }));
            seq.OnComplete(() => { if (rt != null) rt.localScale = orig; });
            seq.SetTarget(gameObject);
        }

        /// <summary>
        /// Creates the RuneCircle / RuneArt / RuneTypeText hierarchy inside a rune GO
        /// if it doesn't already exist (handles case where prefab refs were lost after git reset).
        /// </summary>
        // Brief scale-pop + bright white overlay flash on rune spawn
        private static void PlayRuneEntranceFlash(GameObject runeGo)
        {
            var rt = runeGo.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = new Vector3(0.5f, 0.5f, 1f);
                rt.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetTarget(runeGo);
            }
            // White flash overlay sibling on top
            var flashGo = new GameObject("EntranceFlash", typeof(RectTransform));
            flashGo.transform.SetParent(runeGo.transform, false);
            flashGo.transform.SetAsLastSibling();
            var fRT = flashGo.GetComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
            fRT.offsetMin = Vector2.zero; fRT.offsetMax = Vector2.zero;
            var fImg = flashGo.AddComponent<Image>();
            fImg.color = new Color(1f, 1f, 0.85f, 0.95f);
            fImg.raycastTarget = false;
            fImg.DOFade(0f, 0.5f).SetEase(Ease.OutQuad)
                .OnComplete(() => { if (flashGo != null) Destroy(flashGo); })
                .SetTarget(flashGo);
        }

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
            StartEquipFlyTween(fromCanvasPos, toPos, onDone);
        }

        /// <summary>
        /// Spawns a ghost card that flies from fromCanvasPos back to basePos, then calls onDone.
        /// </summary>
        public void AnimateEquipFlyToBase(Vector2 fromCanvasPos, Vector2 basePos,
                                          System.Action onDone)
        {
            StartEquipFlyTween(fromCanvasPos, basePos, onDone);
        }

        // ── DOT-8: Screen shake ───────────────────────────────────────────────

        private void OnBigDamageHandler(UnitInstance unit, int damage, string spellName)
        {
            if (damage < SHAKE_BIG_DAMAGE_THRESHOLD || _rootCanvas == null) return;
            var canvasRT = _rootCanvas.GetComponent<RectTransform>();
            if (canvasRT == null) return;
            TweenHelper.KillSafe(ref _canvasShakeTween);
            _canvasShakeTween = canvasRT
                .DOShakeAnchorPos(SHAKE_DURATION, SHAKE_STRENGTH, 12, 90f, false, true)
                .SetTarget(gameObject)
                .OnComplete(() => _canvasShakeTween = null);
        }

        // ── DOT-8: Slow motion (bullet time on fatal hit) ─────────────────────

        private void OnFatalHitHandler(UnitInstance unit)
        {
            TweenHelper.KillSafe(ref _slowMotionTween);
            Time.timeScale = 1f; // H-1: reset before re-entering so double fatal hit can't trap at SLOW_SCALE
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(gameObject);
            seq.Append(DOTween.To(() => Time.timeScale, v => Time.timeScale = v, SLOW_SCALE, SLOW_IN_DUR)
                .SetEase(Ease.InQuad).SetUpdate(true));
            seq.AppendInterval(SLOW_HOLD);
            seq.Append(DOTween.To(() => Time.timeScale, v => Time.timeScale = v, 1f, SLOW_OUT_DUR)
                .SetEase(Ease.OutQuad).SetUpdate(true));
            seq.OnComplete(() => { Time.timeScale = 1f; _slowMotionTween = null; });
            _slowMotionTween = seq;
        }

        // ── DOT-8: Turn sweep banner + mana fill stagger ──────────────────────

        private void OnTurnChangedHandler(string owner, int round)
        {
            PlayTurnSweepBanner(owner, round);
            if (owner == FWTCG.Core.GameRules.OWNER_PLAYER)
                PlayManaFillStagger();
        }

        private void PlayTurnSweepBanner(string owner, int round)
        {
            if (_rootCanvas == null) return;
            if (_turnSweepText == null) _turnSweepText = CreateTurnSweepText();
            if (_turnSweepText == null) return;
            var rt = _turnSweepText.GetComponent<RectTransform>();
            if (rt == null) return;

            string label = owner == FWTCG.Core.GameRules.OWNER_PLAYER
                ? $"第 {round} 回合 — 你的回合"
                : $"第 {round} 回合 — 对手回合";
            _turnSweepText.text = label;
            _turnSweepText.gameObject.SetActive(true);

            float hw = _rootCanvas.GetComponent<RectTransform>().rect.width * 0.5f;
            rt.anchoredPosition = new Vector2(-hw - 300f, 0f); // start offscreen left

            TweenHelper.KillSafe(ref _turnSweepSeq); // M-2
            _turnSweepSeq = DOTween.Sequence().SetUpdate(true).SetTarget(gameObject);
            _turnSweepSeq.Append(rt.DOAnchorPos(Vector2.zero, 0.5f).SetEase(Ease.OutBack).SetUpdate(true));
            _turnSweepSeq.AppendInterval(1.0f);
            _turnSweepSeq.Append(rt.DOAnchorPos(new Vector2(hw + 300f, 0f), 0.35f).SetEase(Ease.InQuad).SetUpdate(true));
            _turnSweepSeq.OnComplete(() =>
            {
                if (_turnSweepText != null) _turnSweepText.gameObject.SetActive(false);
                _turnSweepSeq = null;
            });
        }

        private Text CreateTurnSweepText()
        {
            if (_rootCanvas == null) return null;
            var go = new GameObject("TurnSweepBanner");
            go.transform.SetParent(_rootCanvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620f, 80f);
            rt.anchoredPosition = Vector2.zero;

            // Background panel
            var bg = new GameObject("Bg");
            bg.transform.SetParent(go.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.65f);
            bg.transform.SetSiblingIndex(0);

            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 36;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(1f, 0.92f, 0.55f, 1f);
            go.SetActive(false);
            return txt;
        }

        private void PlayManaFillStagger()
        {
            if (_playerManaText == null) return;
            TweenHelper.KillSafe(ref _manaFillSeq);
            var rt = _playerManaText.GetComponent<RectTransform>();
            if (rt == null) return;
            _manaFillSeq = DOTween.Sequence().SetTarget(gameObject);
            for (int i = 0; i < 3; i++)
            {
                // M-5: Insert tween directly into sequence so KillSafe(_manaFillSeq) kills them too
                var t = TweenHelper.PunchScaleUI(rt, 0.12f, 0.18f, 1);
                if (t != null) _manaFillSeq.Insert(i * 0.08f, t.SetTarget(gameObject));
            }
            _manaFillSeq.OnComplete(() => _manaFillSeq = null);
        }

        // ── DOT-8: Victory confetti ───────────────────────────────────────────

        private void SpawnConfetti()
        {
            if (_rootCanvas == null) return;
            var canvasRT = _rootCanvas.GetComponent<RectTransform>();
            float hw = canvasRT.rect.width  * 0.5f;
            float hh = canvasRT.rect.height * 0.5f;

            for (int i = 0; i < CONFETTI_COUNT; i++)
            {
                var go = new GameObject("Confetti");
                _confettiObjs.Add(go);
                go.transform.SetParent(_rootCanvas.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(10f, 10f);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                float startX = UnityEngine.Random.Range(-hw, hw);
                rt.anchoredPosition = new Vector2(startX, hh + 20f);
                var img = go.AddComponent<Image>();
                img.color = ConfettiColor();
                float dur    = UnityEngine.Random.Range(1.5f, 2.8f);
                float rotAmt = UnityEngine.Random.Range(-540f, 540f);
                var seq = DOTween.Sequence().SetTarget(go);
                seq.Append(rt.DOAnchorPosY(-hh - 20f, dur).SetEase(Ease.Linear));
                seq.Join(rt.DOLocalRotate(new Vector3(0f, 0f, rotAmt), dur, RotateMode.FastBeyond360));
                seq.Join(img.DOFade(0f, dur * 0.6f).SetDelay(dur * 0.4f));
                seq.OnComplete(() =>
                {
                    _confettiObjs.Remove(go);
                    Destroy(go);
                });
            }
        }

        private static Color ConfettiColor()
        {
            int idx = UnityEngine.Random.Range(0, 5);
            switch (idx)
            {
                case 0: return new Color(0.95f, 0.82f, 0.25f); // gold
                case 1: return new Color(0.95f, 0.35f, 0.35f); // red
                case 2: return new Color(0.35f, 0.85f, 0.45f); // green
                case 3: return new Color(0.35f, 0.65f, 0.95f); // blue
                default: return new Color(0.90f, 0.55f, 0.95f); // purple
            }
        }

        // ── DOT-8: Opponent card preview ghost ────────────────────────────────

        private void PlayOpponentCardPreview(UnitInstance unit)
        {
            if (_rootCanvas == null) return;
            var canvasRT = _rootCanvas.GetComponent<RectTransform>();
            float hh = canvasRT.rect.height * 0.5f;

            var go = new GameObject("OpponentPreview");
            go.transform.SetParent(_rootCanvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80f, 112f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, hh - 60f); // near enemy hand area

            var img = go.AddComponent<Image>();
            img.color = new Color(0.97f, 0.3f, 0.3f, 0.8f);

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(go.transform, false);
            var nameRT = nameGo.AddComponent<RectTransform>();
            nameRT.anchorMin = Vector2.zero; nameRT.anchorMax = Vector2.one;
            nameRT.offsetMin = nameRT.offsetMax = Vector2.zero;
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.text = unit?.UnitName ?? "?";
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 13;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            TweenHelper.KillSafe(ref _opponentPreviewSeq);
            if (_opponentPreviewGO != null) { Destroy(_opponentPreviewGO); _opponentPreviewGO = null; } // H-3
            _opponentPreviewGO = go;
            _opponentPreviewSeq = DOTween.Sequence().SetTarget(gameObject);
            _opponentPreviewSeq.Append(cg.DOFade(1f, 0.15f));
            _opponentPreviewSeq.Join(rt.DOAnchorPosY(hh * 0.35f, 0.3f).SetEase(Ease.OutBack));
            _opponentPreviewSeq.AppendInterval(0.5f);
            _opponentPreviewSeq.Append(cg.DOFade(0f, 0.25f));
            _opponentPreviewSeq.OnComplete(() =>
            {
                _opponentPreviewSeq = null;
                _opponentPreviewGO = null;
                if (go != null) Destroy(go);
            });
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

        public const float EQUIP_FLY_DURATION = 0.35f;

        /// <summary>
        /// 兜底：场景里任何名字包含 "Border"/"Frame" 的装饰性 GameObject 一次性全部关闭。
        /// 包括 CreateZoneBorderFrame 的 BorderTop/Bottom/Left/Right，以及 LegendFrame / FrameOverlay 等。
        /// 所有 Image/RawImage 上的 Outline 组件也一并 disabled。
        /// </summary>
        private void NukeAllDecorativeBorders()
        {
            // 1. Border* / *Frame 命名的节点
            var allTransforms = GetComponentsInChildren<Transform>(true);
            if (transform.parent != null)
            {
                // 也扫整个 Canvas 根，以便抓到同级的区域边框
                var rootT = transform.root;
                allTransforms = rootT.GetComponentsInChildren<Transform>(true);
            }
            foreach (var t in allTransforms)
            {
                if (t == null || t == transform.root) continue;
                string n = t.name;
                if (n == null) continue;
                if (n.StartsWith("Border") || n == "LegendFrame" || n == "FrameOverlay"
                    || n == "HeroFrame" || n.EndsWith("Border") || n.EndsWith("Frame"))
                {
                    t.gameObject.SetActive(false);
                }
            }

            // 2. 所有 Outline 组件（残留的类型边 / 选中 glow 等）
            var allOutlines = GetComponentsInChildren<Outline>(true);
            foreach (var o in allOutlines)
                if (o != null) o.enabled = false;
            if (transform.root != null)
            {
                var rootOutlines = transform.root.GetComponentsInChildren<Outline>(true);
                foreach (var o in rootOutlines)
                    if (o != null) o.enabled = false;
            }
        }

        private void StartEquipFlyTween(Vector2 from, Vector2 to, System.Action onDone)
        {
            _pendingEquipOnDone = onDone;
            if (_rootCanvas == null) { _pendingEquipOnDone = null; onDone?.Invoke(); return; }

            var ghost = new GameObject("EquipFlyGhost");
            ghost.transform.SetParent(_rootCanvas.transform, false);
            var ghostRT = ghost.AddComponent<RectTransform>();
            ghostRT.sizeDelta = new Vector2(80f, 112f);
            ghostRT.anchoredPosition = from;

            var img = ghost.AddComponent<Image>();
            img.color = new Color(0.85f, 0.75f, 0.25f, 0.85f);

            var cg = ghost.AddComponent<CanvasGroup>();

            var seq = DOTween.Sequence();
            seq.Append(ghostRT.DOAnchorPos(to, EQUIP_FLY_DURATION).SetEase(Ease.InOutQuad));
            seq.Join(cg.DOFade(0.7f, EQUIP_FLY_DURATION)); // 1→0.7 slight fade
            seq.SetUpdate(true); // unscaled time
            seq.OnComplete(() =>
            {
                Destroy(ghost);
                _pendingEquipOnDone = null;
                onDone?.Invoke();
            });
            seq.SetTarget(ghost);
        }
    }
}
