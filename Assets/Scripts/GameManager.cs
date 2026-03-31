using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;
using FWTCG.AI;
using FWTCG.UI;

namespace FWTCG
{
    /// <summary>
    /// Singleton GameManager: initialises the game, runs the main game loop,
    /// and routes all player-input events to the appropriate systems.
    ///
    /// Attach to a single GameObject in the scene. Assign CardData assets
    /// and UI references in the Inspector.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── Static events ─────────────────────────────────────────────────────
        /// <summary>Fired when a card play fails — subscribers show a floating hint toast.</summary>
        public static event System.Action<string> OnHintToast;
        /// <summary>Fired with the UnitInstance that failed to play — subscribers shake the CardView.</summary>
        public static event System.Action<UnitInstance> OnCardPlayFailed;
        /// <summary>Fired whenever any unit takes damage: (unit, amount, sourceName). Used for red flash + shake + toast.</summary>
        public static event System.Action<UnitInstance, int, string> OnUnitDamaged;

        /// <summary>Allows non-GameManager code to send a hint toast.</summary>
        public static void FireHintToast(string msg) => OnHintToast?.Invoke(msg);
        /// <summary>Allows any system (spell, combat, reactive) to fire damage feedback.</summary>
        public static void FireUnitDamaged(UnitInstance unit, int damage, string source = "")
            => OnUnitDamaged?.Invoke(unit, damage, source);

        /// <summary>Fired just BEFORE a unit is removed from game state (HP reached 0). Used for death animation. DEV-17.</summary>
        public static event System.Action<UnitInstance> OnUnitDied;
        public static void FireUnitDied(UnitInstance unit) => OnUnitDied?.Invoke(unit);

        // ── Card data (assign in Inspector) ──────────────────────────────────
        [SerializeField] private CardData[] _kaisaDeck;   // 5 cards
        [SerializeField] private CardData[] _yiDeck;      // 5 cards
        [SerializeField] private CardData _kaisaLegendData;  // legend display data
        [SerializeField] private CardData _yiLegendData;     // legend display data

        // ── System references (assign in Inspector or add to same GameObject) ─
        [SerializeField] private TurnManager _turnMgr;
        [SerializeField] private CombatSystem _combatSys;
        [SerializeField] private ScoreManager _scoreMgr;
        [SerializeField] private SimpleAI _ai;
        [SerializeField] private GameUI _ui;
        [SerializeField] private EntryEffectSystem _entryEffects;
        [SerializeField] private SpellSystem _spellSys;
        [SerializeField] private StartupFlowUI _startupFlowUI;
        [SerializeField] private ReactiveSystem _reactiveSys;
        [SerializeField] private ReactiveWindowUI _reactiveWindowUI;
        [SerializeField] private LegendSystem _legendSys;
        [SerializeField] private BattlefieldSystem _bfSys;
        [SerializeField] private CardDetailPopup _cardDetailPopup;
        [SerializeField] private SpellShowcaseUI _spellShowcase;  // DEV-16

        // ── React button / Legend skill button ────────────────────────────────
        [SerializeField] private Button _reactBtn;
        [SerializeField] private Button _legendSkillBtn;

        // ── Reaction window freeze (static so SimpleAI can await without a ref) ─
        // Player reaction window (player clicks React → AI waits)
        private static bool _reactionWindowActive;
        private static TaskCompletionSource<bool> _reactionTcs;

        // AI reaction window (AI plays reactive card → player must wait)
        private static bool _aiReactionWindowActive;
        private static TaskCompletionSource<bool> _aiReactionTcs;

        /// <summary>
        /// SimpleAI calls this after the spell announcement delay.
        /// Awaits until the player's reaction window closes.
        /// </summary>
        public static Task WaitIfReactionActive() =>
            _reactionWindowActive && _reactionTcs != null
                ? _reactionTcs.Task
                : Task.CompletedTask;

        /// <summary>
        /// Called by SimpleAI (DEV-5+) when AI plays a reactive card.
        /// Shows banner, freezes player's React button until AI resolves.
        /// </summary>
        public static void BeginAiReactionWindow(string cardName)
        {
            _aiReactionWindowActive = true;
            _aiReactionTcs = new TaskCompletionSource<bool>();
            TurnManager.ShowBanner_Static("⚡ [AI] 反应窗口触发！");
            TurnManager.BroadcastMessage_Static($"[反应] AI 打出反应牌 {cardName}，等待结算…");
        }

        /// <summary>
        /// Called by SimpleAI after the reactive card resolves.
        /// Unblocks any code awaiting WaitIfAiReactionActive().
        /// </summary>
        public static void EndAiReactionWindow()
        {
            _aiReactionWindowActive = false;
            _aiReactionTcs?.TrySetResult(true);
            _aiReactionTcs = null;
        }

        /// <summary>
        /// Future player-side code can await this to pause while AI is reacting.
        /// </summary>
        public static Task WaitIfAiReactionActive() =>
            _aiReactionWindowActive && _aiReactionTcs != null
                ? _aiReactionTcs.Task
                : Task.CompletedTask;

        // ── Action buttons (DEV-9) ───────────────────────────────────────────
        [SerializeField] private Button _tapAllRunesBtn;
        [SerializeField] private Button _skipReactionBtn;

        // ── DEBUG panel (left-bottom overlay, always visible in dev) ──────────
        [SerializeField] private Button _debugSpellBtn;
        [SerializeField] private Button _debugEquipBtn;
        [SerializeField] private Button _debugUnitBtn;
        [SerializeField] private Button _debugReactiveBtn;
        [SerializeField] private Button _debugManaBtn;
        [SerializeField] private Button _debugSchBtn;
        [SerializeField] private SpellTargetPopup _spellTargetPopup;  // DEV-16b

        // ── Game state ────────────────────────────────────────────────────────
        private GameState _gs;

        // ── Interaction state ─────────────────────────────────────────────────
        private UnitInstance _selectedUnit;       // single-select (for BF recall)
        private string _selectedUnitLoc;          // "hand", "base", "0", "1"
        private List<UnitInstance> _selectedBaseUnits = new List<UnitInstance>(); // multi-select for batch move
        private UnitInstance _targetingSpell;     // non-null = awaiting target click for spell
        private bool _aiReactionPending;          // DEV-15: true while AI reaction resolves
        private bool _bfClickInFlight;            // DEV-17: true while OnBattlefieldClicked awaits post-combat delay

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Validate required references
            if (_turnMgr == null) _turnMgr = GetComponent<TurnManager>();
            if (_combatSys == null) _combatSys = GetComponent<CombatSystem>();
            if (_scoreMgr == null) _scoreMgr = GetComponent<ScoreManager>();
            if (_ai == null) _ai = GetComponent<SimpleAI>();
            if (_entryEffects == null) _entryEffects = GetComponent<EntryEffectSystem>();
            if (_spellSys == null) _spellSys = GetComponent<SpellSystem>();
            if (_reactiveSys == null) _reactiveSys = GetComponent<ReactiveSystem>();
            if (_reactiveWindowUI == null) _reactiveWindowUI = GetComponent<ReactiveWindowUI>();
            if (_legendSys == null) _legendSys = GetComponent<LegendSystem>();

            // Wire react button
            if (_reactBtn != null) _reactBtn.onClick.AddListener(OnReactClicked);
            if (_legendSkillBtn != null) _legendSkillBtn.onClick.AddListener(OnLegendSkillClicked);

            // Wire DEV-9 action buttons
            if (_tapAllRunesBtn != null)   _tapAllRunesBtn.onClick.AddListener(OnTapAllRunesClicked);
            if (_skipReactionBtn != null)  _skipReactionBtn.onClick.AddListener(OnSkipReactionClicked);

            // Wire debug buttons
            if (_debugSpellBtn != null)    _debugSpellBtn.onClick.AddListener(() => DebugDraw("spell"));
            if (_debugEquipBtn != null)    _debugEquipBtn.onClick.AddListener(() => DebugDraw("equip"));
            if (_debugUnitBtn != null)     _debugUnitBtn.onClick.AddListener(() => DebugDraw("unit"));
            if (_debugReactiveBtn != null) _debugReactiveBtn.onClick.AddListener(() => DebugDraw("reactive"));
            if (_debugManaBtn != null)     _debugManaBtn.onClick.AddListener(DebugAddMana);
            if (_debugSchBtn != null)      _debugSchBtn.onClick.AddListener(DebugAddSch);
        }

        private void OnEnable()
        {
            TurnManager.OnMessage += HandleMessage;
            TurnManager.OnPhaseChanged += HandlePhaseChanged;
            CombatSystem.OnCombatLog += HandleMessage;
            CombatSystem.OnCombatResult += HandleCombatResult;
            ScoreManager.OnGameOver += HandleGameOver;
            ScoreManager.OnScoreChanged += HandleMessage;
            SpellSystem.OnSpellLog += HandleMessage;
            ReactiveSystem.OnReactiveLog += HandleMessage;
            LegendSystem.OnLegendLog += HandleMessage;
        }

        private void OnDisable()
        {
            TurnManager.OnMessage -= HandleMessage;
            TurnManager.OnPhaseChanged -= HandlePhaseChanged;
            CombatSystem.OnCombatLog -= HandleMessage;
            CombatSystem.OnCombatResult -= HandleCombatResult;
            ScoreManager.OnGameOver -= HandleGameOver;
            ScoreManager.OnScoreChanged -= HandleMessage;
            SpellSystem.OnSpellLog -= HandleMessage;
            ReactiveSystem.OnReactiveLog -= HandleMessage;
            LegendSystem.OnLegendLog -= HandleMessage;
        }

        private void Start()
        {
            // Wire UI callbacks
            if (_ui != null)
            {
                _ui.SetCallbacks(
                    onEndTurn: OnEndTurnClicked,
                    onBF: OnBattlefieldClicked,
                    onUnit: OnUnitClicked,
                    onRune: OnRuneClicked,
                    onCardRightClick: OnCardRightClicked
                );
                _ui.SetPileClickCallback(OnPileClicked);
                _ui.WirePileButtons();
            }

            InitGame();
            StartCoroutine(RunWithStartup());
        }

        // ── Initialisation ────────────────────────────────────────────────────

        private void InitGame()
        {
            GameState.ResetUidCounter();
            _gs = new GameState();

            // Inject dependencies into systems
            _turnMgr.Inject(_gs, _scoreMgr, _combatSys, _ai, _entryEffects,
                            _spellSys, _reactiveSys, _reactiveWindowUI, _legendSys, _bfSys);

            // Initialize legends (player = Kaisa/虚空, enemy = Masteryi/伊欧尼亚)
            if (_legendSys != null)
            {
                _gs.PLegend = _legendSys.CreateLegend(LegendSystem.KAISA_LEGEND_ID, GameRules.OWNER_PLAYER);
                _gs.ELegend = _legendSys.CreateLegend(LegendSystem.YI_LEGEND_ID, GameRules.OWNER_ENEMY);

                // Associate CardData for legend art display (DEV-10)
                if (_kaisaLegendData != null) _gs.PLegend.DisplayData = _kaisaLegendData;
                if (_yiLegendData != null)    _gs.ELegend.DisplayData = _yiLegendData;
            }

            // Random first player
            _gs.First = Random.value > 0.5f ? GameRules.OWNER_PLAYER : GameRules.OWNER_ENEMY;
            _gs.Turn = _gs.First;
            _gs.Round = 0;
            _gs.Phase = GameRules.PHASE_AWAKEN;

            // Build and shuffle decks
            BuildDeck(GameRules.OWNER_PLAYER, _kaisaDeck);
            BuildDeck(GameRules.OWNER_ENEMY, _yiDeck);

            // Extract hero cards from decks to hero zone (rule 103.2.a)
            ExtractHero(GameRules.OWNER_PLAYER);
            ExtractHero(GameRules.OWNER_ENEMY);

            // Deal initial hands
            DealInitialHand(GameRules.OWNER_PLAYER);
            DealInitialHand(GameRules.OWNER_ENEMY);

            // Build rune decks
            BuildRuneDeck(GameRules.OWNER_PLAYER, RuneType.Blazing, GameRules.RUNE_DECK_BLAZING,
                                                  RuneType.Radiant, GameRules.RUNE_DECK_RADIANT);
            BuildRuneDeck(GameRules.OWNER_ENEMY, RuneType.Verdant, GameRules.RUNE_DECK_VERDANT,
                                                 RuneType.Crushing, GameRules.RUNE_DECK_CRUSHING);

            // Random battlefield selection from faction pools
            _gs.BFNames[0] = GameRules.PickBattlefield(GameRules.KAISA_BF_POOL);
            _gs.BFNames[1] = GameRules.PickBattlefield(GameRules.YI_BF_POOL);

            string firstLabel = _gs.First == GameRules.OWNER_PLAYER ? "玩家" : "AI";
            TurnManager.BroadcastMessage_Static(
                $"[开局] {firstLabel} 先手。战场：{_gs.BFNames[0]} / {_gs.BFNames[1]}。发牌完成。");

            RefreshUI();
        }

        // ── Startup flow then game loop ───────────────────────────────────────

        private IEnumerator RunWithStartup()
        {
            if (_startupFlowUI != null)
            {
                System.Threading.Tasks.Task startupTask = _startupFlowUI.RunStartupFlow(_gs);
                yield return new WaitUntil(() => startupTask.IsCompleted);
                if (startupTask.IsFaulted)
                    Debug.LogError($"[StartupFlow] 异常: {startupTask.Exception}");
                RefreshUI();
            }
            StartCoroutine(GameLoop());
        }

        // ── Game loop (coroutine wrapper for async turn) ───────────────────────

        private IEnumerator GameLoop()
        {
            while (!_gs.GameOver)
            {
                string who = _gs.Turn;

                // Start turn returns a Task; yield until it completes
                Task turnTask = _turnMgr.StartTurn(who, _gs);
                yield return new WaitUntil(() => turnTask.IsCompleted);

                if (turnTask.IsFaulted)
                {
                    Debug.LogError($"[GameLoop] 回合任务异常: {turnTask.Exception}");
                }

                RefreshUI();

                if (!_gs.GameOver)
                {
                    // Brief pause between turns
                    yield return new WaitForSeconds(0.3f);
                }
            }
        }

        // ── Player input handlers ─────────────────────────────────────────────

        /// <summary>
        /// Called when the player clicks a unit card in hand, base, or battlefield.
        /// - Hand: plays card to base (pay mana)
        /// - Base: toggles multi-select (add/remove from batch)
        /// - Battlefield: single-select for recall (double-click to recall)
        /// After selecting base units, click a battlefield to batch-move them all.
        /// </summary>
        public void OnUnitClicked(UnitInstance unit)
        {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;
            if (_aiReactionPending) return; // DEV-15: block input while AI resolves reaction

            // ── Spell targeting mode: resolve target ──
            if (_targetingSpell != null)
            {
                // Hand cards can't be targeted (must be a unit in play)
                if (_gs.PHand.Contains(unit) || _gs.EHand.Contains(unit))
                {
                    TurnManager.BroadcastMessage_Static("[提示] 手牌中的卡牌无法作为法术目标");
                    return;
                }

                SpellTargetType targetType = _targetingSpell.CardData.SpellTargetType;
                if (targetType == SpellTargetType.EnemyUnit && unit.Owner != GameRules.OWNER_ENEMY)
                {
                    TurnManager.BroadcastMessage_Static("[提示] 请选择一个敌方单位作为目标");
                    return;
                }
                if (targetType == SpellTargetType.FriendlyUnit && unit.Owner != GameRules.OWNER_PLAYER)
                {
                    TurnManager.BroadcastMessage_Static("[提示] 请选择一个己方单位作为目标");
                    return;
                }

                // Rule 721: SpellShield forces caster to pay 1 extra sch to target the unit
                if (!TryPaySpellShieldCost(GameRules.OWNER_PLAYER, unit))
                {
                    TurnManager.BroadcastMessage_Static(
                        $"[法盾] 符能不足：{unit.UnitName} 拥有法盾，需要至少1点符能才能选为目标");
                    return; // Stay in targeting mode so player can pick another target
                }

                // Valid target — give AI a reaction window before resolving (DEV-15)
                UnitInstance spell = _targetingSpell;
                _targetingSpell = null;
                _ = CastPlayerSpellWithReactionAsync(spell, unit);
                RefreshUI();
                return;
            }

            // ── Hand card: play card (unit or spell) ──
            if (_gs.PHand.Contains(unit))
            {
                TryPlayCard(unit);
                return;
            }

            // ── Base unit: toggle multi-select ──
            if (_gs.PBase.Contains(unit))
            {
                // Clear any BF single-select
                _selectedUnit = null;
                _selectedUnitLoc = null;

                if (_selectedBaseUnits.Contains(unit))
                {
                    _selectedBaseUnits.Remove(unit);
                    TurnManager.BroadcastMessage_Static($"[取消选择] {unit.UnitName}（已选{_selectedBaseUnits.Count}个）");
                }
                else
                {
                    if (unit.Exhausted)
                    {
                        TurnManager.BroadcastMessage_Static($"[提示] {unit.UnitName} 已休眠，无法移动");
                    }
                    else
                    {
                        _selectedBaseUnits.Add(unit);
                        TurnManager.BroadcastMessage_Static($"[选择] {unit.UnitName}（已选{_selectedBaseUnits.Count}个） — 点击战场一起上场");
                    }
                }
                RefreshUI();
                return;
            }

            // ── Battlefield unit: single-select / recall ──
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                if (_gs.BF[i].PlayerUnits.Contains(unit))
                {
                    if (_selectedUnit == unit)
                    {
                        // Double-click: recall to base (#13)
                        if (unit.Exhausted)
                        {
                            TurnManager.BroadcastMessage_Static($"[提示] {unit.UnitName} 已休眠，无法召回");
                        }
                        else
                        {
                            _combatSys.RecallUnit(unit, i, GameRules.OWNER_PLAYER, _gs);
                        }
                        _selectedUnit = null;
                        _selectedUnitLoc = null;
                    }
                    else
                    {
                        // First click: select
                        _selectedUnit = unit;
                        _selectedUnitLoc = i.ToString();
                        _selectedBaseUnits.Clear(); // clear multi-select
                        TurnManager.BroadcastMessage_Static($"[选择] {unit.UnitName}（战场{i + 1}） — 再次点击召回基地");
                    }
                    RefreshUI();
                    return;
                }
            }
        }

        /// <summary>
        /// Called when the player clicks a battlefield zone.
        /// If base units are multi-selected, batch-move them all, then auto-combat.
        /// If a single BF unit is selected, move it to the other BF.
        /// </summary>
        public async void OnBattlefieldClicked(int bfId)
        {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;
            if (_aiReactionPending) return; // DEV-15
            if (_bfClickInFlight) return;   // DEV-17: block reentrant clicks during post-combat delay

            // Ignore BF click while in spell targeting mode
            if (_targetingSpell != null)
            {
                TurnManager.BroadcastMessage_Static("[提示] 请选择目标单位，或结束回合以取消法术");
                return;
            }

            // rockfall_path: block direct unit play from base to this BF
            if (_selectedBaseUnits.Count > 0 || (_selectedUnit != null && _selectedUnitLoc == "base"))
            {
                if (_bfSys != null && !_bfSys.CanPlayDirectlyToBattlefield(bfId, _gs))
                {
                    TurnManager.BroadcastMessage_Static("[落岩之径] 禁止从手牌直接打出到此战场！");
                    return;
                }
            }

            // ── Batch move from base ──
            if (_selectedBaseUnits.Count > 0)
            {
                // Move all selected base units to this BF
                List<UnitInstance> toMove = new List<UnitInstance>(_selectedBaseUnits);
                _selectedBaseUnits.Clear();

                foreach (UnitInstance u in toMove)
                {
                    if (!u.Exhausted && _gs.PBase.Contains(u))
                    {
                        _combatSys.MoveUnit(u, "base", bfId, GameRules.OWNER_PLAYER, _gs);
                    }
                }

                // After all units moved, check combat on this BF
                _combatSys.CheckAndResolveCombat(bfId, GameRules.OWNER_PLAYER, _gs, _scoreMgr);

                _selectedUnit = null;
                _selectedUnitLoc = null;
                // DEV-17: wait for hit flash + death animation before destroying CardViews
                _bfClickInFlight = true;
                try
                {
                    await System.Threading.Tasks.Task.Delay(550);
                    if (!_gs.GameOver) RefreshUI(); // re-check in case game ended during delay
                }
                finally { _bfClickInFlight = false; }
                return;
            }

            // ── Single BF unit move (to different BF) ──
            if (_selectedUnit != null && _selectedUnitLoc != null && _selectedUnitLoc != "base")
            {
                if (_selectedUnit.Exhausted)
                {
                    TurnManager.BroadcastMessage_Static($"[提示] {_selectedUnit.UnitName} 已休眠，无法移动");
                    _selectedUnit = null;
                    _selectedUnitLoc = null;
                    return;
                }

                _combatSys.MoveUnit(_selectedUnit, _selectedUnitLoc, bfId, GameRules.OWNER_PLAYER, _gs);
                _combatSys.CheckAndResolveCombat(bfId, GameRules.OWNER_PLAYER, _gs, _scoreMgr);

                _selectedUnit = null;
                _selectedUnitLoc = null;
                // DEV-17: wait for hit flash + death animation before destroying CardViews
                _bfClickInFlight = true;
                try
                {
                    await System.Threading.Tasks.Task.Delay(550);
                    if (!_gs.GameOver) RefreshUI(); // re-check in case game ended during delay
                }
                finally { _bfClickInFlight = false; }
                return;
            }

            TurnManager.BroadcastMessage_Static("[提示] 请先选择基地中的单位");
        }

        /// <summary>
        /// Called when the player clicks "End Turn" button.
        /// </summary>
        public void OnEndTurnClicked()
        {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;

            // Cancel any pending spell targeting — refund mana and return spell to hand
            if (_targetingSpell != null)
            {
                _gs.PHand.Add(_targetingSpell);
                _gs.PMana += _targetingSpell.CardData.Cost;
                _gs.CardsPlayedThisTurn--;
                TurnManager.BroadcastMessage_Static($"[法术] 取消 {_targetingSpell.UnitName} 的发动，法力退还");
                _targetingSpell = null;
            }

            _selectedUnit = null;
            _selectedUnitLoc = null;
            _selectedBaseUnits.Clear();
            _turnMgr.EndTurn();
            RefreshUI();
        }

        /// <summary>
        /// Called when the player clicks a rune button.
        /// recycle=false → tap for mana; recycle=true → recycle for schematic energy.
        /// </summary>
        public void OnRuneClicked(int runeIdx, bool recycle)
        {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;

            List<RuneInstance> runes = _gs.PRunes;
            if (runeIdx < 0 || runeIdx >= runes.Count)
            {
                Debug.LogWarning($"[GameManager] Invalid rune index: {runeIdx}");
                return;
            }

            RuneInstance rune = runes[runeIdx];

            if (recycle)
            {
                // Recycle: remove from active runes, place at bottom of rune deck, gain +1 sch
                runes.RemoveAt(runeIdx);
                _gs.PRuneDeck.Add(rune);
                _gs.AddSch(GameRules.OWNER_PLAYER, rune.RuneType, 1);
                TurnManager.BroadcastMessage_Static(
                    $"[回收] 符文 {rune.RuneType} 回收，获得 1 点{rune.RuneType}符能");
            }
            else
            {
                // Tap: gain +1 mana
                if (rune.Tapped)
                {
                    TurnManager.BroadcastMessage_Static("[提示] 该符文已横置");
                    return;
                }
                rune.Tapped = true;
                _gs.PMana += 1;
                TurnManager.BroadcastMessage_Static(
                    $"[横置] 符文 {rune.RuneType} 横置，法力 → {_gs.PMana}");
            }

            RefreshUI();
        }

        /// <summary>
        /// Called when the player right-clicks any visible card. Shows detail popup.
        /// </summary>
        public void OnCardRightClicked(UnitInstance unit)
        {
            if (unit == null) return;
            if (_cardDetailPopup != null)
                _cardDetailPopup.Show(unit);
        }

        // ── DEV-9: Action button handlers ───────────────────────────────────

        /// <summary>
        /// Tap all un-tapped player runes for mana.
        /// </summary>
        public void OnTapAllRunesClicked()
        {
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;

            int tapped = 0;
            foreach (var rune in _gs.PRunes)
            {
                if (!rune.Tapped)
                {
                    rune.Tapped = true;
                    _gs.PMana += 1;
                    tapped++;
                }
            }
            if (tapped > 0)
                TurnManager.BroadcastMessage_Static($"[全部横置] 横置 {tapped} 个符文，法力 → {_gs.PMana}");
            else
                TurnManager.BroadcastMessage_Static("[提示] 没有可横置的符文");

            RefreshUI();
        }

        /// <summary>
        /// Skip the current reaction window, delegating to ReactiveWindowUI.
        /// </summary>
        public void OnSkipReactionClicked()
        {
            if (_reactiveWindowUI != null)
                _reactiveWindowUI.SkipReaction();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>Show a floating hint toast and shake the card that failed to play.</summary>
        private void ShowPlayError(string msg, UnitInstance card)
        {
            TurnManager.BroadcastMessage_Static(msg);
            string hint = msg.StartsWith("[提示] ") ? msg.Substring(5) : msg;
            OnHintToast?.Invoke(hint);
            if (card != null) OnCardPlayFailed?.Invoke(card);
        }

        /// <summary>
        /// Rule 721: If the target has SpellShield, the caster must pay 1 sch of any type.
        /// Deducts 1 sch from the first available type for the given owner.
        /// Returns true if paid (or not needed), false if the cost cannot be met.
        /// </summary>
        private bool TryPaySpellShieldCost(string owner, UnitInstance target)
        {
            if (target == null || !target.HasSpellShield) return true;
            var sch = owner == GameRules.OWNER_PLAYER ? _gs.PSch : _gs.ESch;
            foreach (var kv in sch)
            {
                if (kv.Value > 0)
                {
                    _gs.SpendSch(owner, kv.Key, 1);
                    TurnManager.BroadcastMessage_Static(
                        $"[法盾] 支付1点{kv.Key}符能，将 {target.UnitName} 选为目标");
                    return true;
                }
            }
            return false; // can't afford
        }

        private void TryPlayCard(UnitInstance unit)
        {
            if (unit.CardData.IsSpell)
                _ = TryPlaySpellAsync(unit);
            else if (unit.CardData.IsEquipment)
                TryPlayEquipment(unit);
            else
                TryPlayUnit(unit);
        }

        private void TryPlayUnit(UnitInstance unit)
        {
            if (unit.CardData.Cost > _gs.PMana)
            {
                ShowPlayError($"[提示] 法力不足：需要 {unit.CardData.Cost}，当前 {_gs.PMana}", unit);
                _selectedUnit = null;
                return;
            }

            // Check schematic (rune) cost
            if (unit.CardData.RuneCost > 0)
            {
                int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType);
                if (haveSch < unit.CardData.RuneCost)
                {
                    ShowPlayError($"[提示] 符能不足：需要 {unit.CardData.RuneCost} {unit.CardData.RuneType}，当前 {haveSch}", unit);
                    _selectedUnit = null;
                    return;
                }
            }

            _gs.PHand.Remove(unit);
            _gs.PBase.Add(unit);
            _gs.PMana -= unit.CardData.Cost;
            if (unit.CardData.RuneCost > 0)
                _gs.SpendSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType, unit.CardData.RuneCost);
            unit.Exhausted = true;
            _gs.CardsPlayedThisTurn++;

            TurnManager.BroadcastMessage_Static(
                $"[打出] {unit.UnitName}（费用{unit.CardData.Cost}），剩余法力 {_gs.PMana}");

            // Trigger entry effects
            if (_entryEffects != null)
                _entryEffects.OnUnitEntered(unit, GameRules.OWNER_PLAYER, _gs);

            // Check Kaisa evolution (4 distinct allied keywords → Lv.2)
            _legendSys?.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);

            _selectedUnit = null;
            _selectedUnitLoc = "base";
            RefreshUI();
        }

        private void TryPlayEquipment(UnitInstance equip)
        {
            if (equip.CardData.Cost > _gs.PMana)
            {
                ShowPlayError($"[提示] 法力不足：需要 {equip.CardData.Cost}，当前 {_gs.PMana}", equip);
                return;
            }

            // Check schematic cost
            if (equip.CardData.RuneCost > 0)
            {
                int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, equip.CardData.RuneType);
                if (haveSch < equip.CardData.RuneCost)
                {
                    ShowPlayError($"[提示] 符能不足：需要 {equip.CardData.RuneCost} {equip.CardData.RuneType}，当前 {haveSch}", equip);
                    return;
                }
            }

            // Find best target for auto-attach: strongest non-equipped friendly unit
            UnitInstance target = null;
            int bestAtk = -1;
            foreach (var u in _gs.PBase)
            {
                if (u.AttachedEquipment == null && !u.CardData.IsSpell && !u.CardData.IsEquipment && u.CurrentAtk > bestAtk)
                {
                    target = u;
                    bestAtk = u.CurrentAtk;
                }
            }
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                foreach (var u in _gs.BF[i].PlayerUnits)
                {
                    if (u.AttachedEquipment == null && u.CurrentAtk > bestAtk)
                    {
                        target = u;
                        bestAtk = u.CurrentAtk;
                    }
                }
            }

            if (target == null)
            {
                TurnManager.BroadcastMessage_Static("[提示] 没有可附着的己方单位");
                return;
            }

            // Pay costs
            _gs.PHand.Remove(equip);
            _gs.PMana -= equip.CardData.Cost;
            if (equip.CardData.RuneCost > 0)
                _gs.SpendSch(GameRules.OWNER_PLAYER, equip.CardData.RuneType, equip.CardData.RuneCost);
            _gs.CardsPlayedThisTurn++;

            // Attach equipment
            target.AttachedEquipment = equip;
            equip.AttachedTo = target;

            // Apply equipment ATK bonus
            int bonus = equip.CardData.EquipAtkBonus;
            if (bonus > 0)
            {
                target.CurrentAtk += bonus;
                target.CurrentHp += bonus;
            }

            TurnManager.BroadcastMessage_Static(
                $"[装备] {equip.UnitName} 附着到 {target.UnitName}（+{bonus}战力），剩余法力 {_gs.PMana}");

            // Trigger entry effects
            _entryEffects?.OnUnitEntered(equip, GameRules.OWNER_PLAYER, _gs);

            RefreshUI();
        }

        private bool HasValidSpellTargets(SpellTargetType targetType)
        {
            switch (targetType)
            {
                case SpellTargetType.EnemyUnit:
                {
                    if (_gs.EBase.Count > 0) return true;
                    for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                        if (_gs.BF[i].EnemyUnits.Count > 0) return true;
                    return false;
                }
                case SpellTargetType.FriendlyUnit:
                {
                    if (_gs.PBase.Count > 0) return true;
                    for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                        if (_gs.BF[i].PlayerUnits.Count > 0) return true;
                    return false;
                }
                case SpellTargetType.AnyUnit:
                {
                    if (_gs.PBase.Count > 0 || _gs.EBase.Count > 0) return true;
                    for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                        if (_gs.BF[i].PlayerUnits.Count > 0 || _gs.BF[i].EnemyUnits.Count > 0) return true;
                    return false;
                }
                default:
                    return true;
            }
        }

        private async Task TryPlaySpellAsync(UnitInstance spell)
        {
            if (spell.CardData.Cost > _gs.PMana)
            {
                ShowPlayError($"[提示] 法力不足：需要 {spell.CardData.Cost}，当前 {_gs.PMana}", spell);
                return;
            }

            // Guard: no valid targets → reject before deducting anything
            if (spell.CardData.SpellTargetType != SpellTargetType.None &&
                !HasValidSpellTargets(spell.CardData.SpellTargetType))
            {
                string typeLabel = spell.CardData.SpellTargetType == SpellTargetType.EnemyUnit ? "敌方"
                    : spell.CardData.SpellTargetType == SpellTargetType.FriendlyUnit ? "己方" : "任意";
                ShowPlayError($"[提示] 场上没有可选的{typeLabel}单位", spell);
                return;
            }

            _gs.PMana -= spell.CardData.Cost;
            _gs.CardsPlayedThisTurn++;
            _gs.PHand.Remove(spell);

            if (spell.CardData.SpellTargetType == SpellTargetType.None)
            {
                // No target needed — give AI reaction window (DEV-15)
                _ = CastPlayerSpellWithReactionAsync(spell, null);
                RefreshUI();
                return;
            }

            // Needs a target — show popup
            UnitInstance target = null;
            if (_spellTargetPopup != null)
            {
                RefreshUI();
                target = await _spellTargetPopup.ShowAsync(spell.CardData.SpellTargetType, _gs);
            }
            else
            {
                // Fallback: no popup wired — use old targeting mode
                _targetingSpell = spell;
                string typeLabel = spell.CardData.SpellTargetType == SpellTargetType.EnemyUnit ? "敌方"
                    : spell.CardData.SpellTargetType == SpellTargetType.FriendlyUnit ? "己方" : "任意";
                string prompt = $"请点击一个{typeLabel}单位作为目标";
                TurnManager.BroadcastMessage_Static($"[法术] {spell.UnitName} — {prompt}（结束回合可取消）");
                OnHintToast?.Invoke(prompt); // toast so player notices
                RefreshUI();
                return;
            }

            if (target == null)
            {
                // Cancelled — refund mana and return spell to hand
                _gs.PHand.Add(spell);
                _gs.PMana += spell.CardData.Cost;
                _gs.CardsPlayedThisTurn--;
                TurnManager.BroadcastMessage_Static($"[法术] 取消 {spell.UnitName} 的目标选择，法力退还");
                RefreshUI();
                return;
            }

            // Rule 721: SpellShield forces caster to pay 1 extra sch to target the unit
            if (!TryPaySpellShieldCost(GameRules.OWNER_PLAYER, target))
            {
                // Can't afford — treat as cancelled, refund
                _gs.PHand.Add(spell);
                _gs.PMana += spell.CardData.Cost;
                _gs.CardsPlayedThisTurn--;
                ShowPlayError($"[法盾] 符能不足：{target.UnitName} 拥有法盾，需要至少1点符能才能选为目标", spell);
                RefreshUI();
                return;
            }

            _ = CastPlayerSpellWithReactionAsync(spell, target);
            RefreshUI();
        }

        // ── DEV-15: AI reaction to player spells ─────────────────────────���────

        /// <summary>
        /// Casts a player spell after giving the AI a brief window to play a reactive card.
        /// Fire-and-forget (assigned to _ to suppress warning). Sets _aiReactionPending
        /// to block player input during the window.
        /// </summary>
        private async Task CastPlayerSpellWithReactionAsync(UnitInstance spell, UnitInstance target)
        {
            _aiReactionPending = true;

            string targetName = target != null ? $" → {target.UnitName}" : "";
            TurnManager.BroadcastMessage_Static(
                $"[法术] {spell.UnitName}{targetName}！⚡ AI响应中…");
            RefreshUI();

            await Task.Delay(GameRules.AI_ACTION_DELAY_MS);
            if (_gs.GameOver) { _aiReactionPending = false; return; }

            bool negated = AiTryReact(spell);

            if (negated)
            {
                await Task.Delay(300); // brief pause so player reads the negation log
                TurnManager.BroadcastMessage_Static($"[法术] {spell.UnitName} 被无效化！");
            }
            else if (!_gs.GameOver)
            {
                // DEV-16: show spell showcase before resolving
                if (_spellShowcase != null)
                    await _spellShowcase.ShowAsync(spell, GameRules.OWNER_PLAYER);

                if (!_gs.GameOver)
                {
                    if (_spellSys != null)
                        _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);
                    else
                        _gs.PDiscard.Add(spell);
                    _legendSys?.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);
                }
            }

            // Wait for hit-flash + shake animations to complete before RefreshUI destroys CardViews
            await Task.Delay(550);

            _aiReactionPending = false;
            RefreshUI();
        }

        /// <summary>
        /// AI decides whether to play a reactive card in response to a player spell.
        /// Returns true if the player's spell was negated.
        /// </summary>
        private bool AiTryReact(UnitInstance playerSpell)
        {
            if (_reactiveSys == null) return false;

            // Collect AI's affordable reactive cards
            var reactives = new System.Collections.Generic.List<UnitInstance>();
            foreach (var c in _gs.EHand)
            {
                if (c.CardData.IsSpell &&
                    c.CardData.HasKeyword(CardKeyword.Reactive) &&
                    c.CardData.Cost <= _gs.EMana)
                {
                    reactives.Add(c);
                }
            }

            if (reactives.Count == 0) return false;

            UnitInstance chosen = SimpleAI.AiPickBestReactiveCard(reactives, playerSpell, _gs);
            if (chosen == null) return false;

            // Pay cost and apply reactive
            _gs.EMana -= chosen.CardData.Cost;
            TurnManager.ShowBanner_Static($"⚡ [AI] 反应！{chosen.UnitName}");
            bool negated = _reactiveSys.ApplyReactive(chosen, GameRules.OWNER_ENEMY, playerSpell, _gs);
            return negated;
        }

        private void BuildDeck(string owner, CardData[] cardDatas)
        {
            if (cardDatas == null || cardDatas.Length == 0)
            {
                Debug.LogWarning($"[GameManager] No card data assigned for {owner}");
                return;
            }

            List<UnitInstance> deck = _gs.GetDeck(owner);
            deck.Clear();

            // Add correct number of copies per card based on GameRules
            foreach (CardData data in cardDatas)
            {
                if (data == null) continue;
                int copies = GameRules.GetCardCopies(data.Id);
                for (int c = 0; c < copies; c++)
                    deck.Add(_gs.MakeUnit(data, owner));
            }

            // Fisher-Yates shuffle
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                UnitInstance temp = deck[i];
                deck[i] = deck[j];
                deck[j] = temp;
            }

            Debug.Log($"[InitGame] {owner} deck: {deck.Count} cards");
        }

        private void DealInitialHand(string owner)
        {
            List<UnitInstance> deck = _gs.GetDeck(owner);
            List<UnitInstance> hand = _gs.GetHand(owner);

            // Soft-weighted opening hand: 67% chance to ensure at least one ≤2-cost unit
            if (Random.value <= 0.67f)
            {
                SeedOpeningHand(deck, hand);
            }

            // Draw remaining cards to fill up to INITIAL_HAND_SIZE
            while (hand.Count < GameRules.INITIAL_HAND_SIZE && deck.Count > 0)
            {
                hand.Add(deck[0]);
                deck.RemoveAt(0);
            }
        }

        /// <summary>
        /// Soft-weighted opening hand: find a ≤2-cost non-spell unit in the deck
        /// and move it to hand (ensures early playable card).
        /// </summary>
        private void SeedOpeningHand(List<UnitInstance> deck, List<UnitInstance> hand)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                UnitInstance card = deck[i];
                if (!card.CardData.IsSpell && !card.CardData.IsEquipment && card.CardData.Cost <= 2)
                {
                    deck.RemoveAt(i);
                    hand.Add(card);
                    Debug.Log($"[软加权] 开局手牌加入低费单位: {card.UnitName}");
                    return;
                }
            }
        }

        private void BuildRuneDeck(string owner,
                                   RuneType typeA, int countA,
                                   RuneType typeB, int countB)
        {
            List<RuneInstance> runeDeck = _gs.GetRuneDeck(owner);
            runeDeck.Clear();

            for (int i = 0; i < countA; i++)
                runeDeck.Add(new RuneInstance(GameState.NextUid(), typeA));
            for (int i = 0; i < countB; i++)
                runeDeck.Add(new RuneInstance(GameState.NextUid(), typeB));

            // Shuffle rune deck
            for (int i = runeDeck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                RuneInstance temp = runeDeck[i];
                runeDeck[i] = runeDeck[j];
                runeDeck[j] = temp;
            }

            Debug.Log($"[InitGame] {owner} rune deck: {runeDeck.Count} runes");
        }

        private void ExtractHero(string owner)
        {
            List<UnitInstance> deck = _gs.GetDeck(owner);
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i].CardData.IsHero)
                {
                    _gs.SetHero(owner, deck[i]);
                    deck.RemoveAt(i);
                    Debug.Log($"[InitGame] {owner} hero extracted: {_gs.GetHero(owner).UnitName}");
                    return;
                }
            }
        }

        private void RefreshUI()
        {
            if (_ui != null)
                _ui.Refresh(_gs, _selectedBaseUnits);
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleMessage(string msg)
        {
            if (_ui != null) _ui.ShowMessage(msg);
        }

        private void HandlePhaseChanged(string msg)
        {
            if (_ui != null)
            {
                _ui.ShowMessage(msg);
                _ui.Refresh(_gs);

                // Start/clear turn timer based on phase (DEV-10)
                if (_gs.Phase == GameRules.PHASE_ACTION && _gs.Turn == GameRules.OWNER_PLAYER)
                    _ui.StartTurnTimer(OnTimerExpired);
                else
                    _ui.ClearTurnTimer();
            }
        }

        private void OnTimerExpired()
        {
            if (_gs == null || _gs.GameOver) return;
            TurnManager.BroadcastMessage_Static("[倒计时] 时间到，自动结束回合");
            OnEndTurnClicked();
        }

        private void OnPileClicked(string owner, string pileType)
        {
            if (_gs == null || _ui == null) return;

            if (pileType == "discard")
                _ui.ShowDiscardViewer(_gs.GetDiscard(owner), owner == GameRules.OWNER_PLAYER ? "玩家弃牌堆" : "敌方弃牌堆");
            else if (pileType == "exile")
                _ui.ShowExileViewer(_gs.GetExile(owner), owner == GameRules.OWNER_PLAYER ? "玩家放逐堆" : "敌方放逐堆");
        }

        private void HandleCombatResult(CombatSystem.CombatResult result)
        {
            if (_ui != null) _ui.ShowCombatResult(result);
        }

        private void HandleGameOver(string msg)
        {
            if (_ui != null)
            {
                _ui.ShowGameOver(msg);
                _ui.Refresh(_gs);
            }
        }

        // ── React button ──────────────────────────────────────────────────────

        /// <summary>
        /// Called when the player clicks the React button (any time, any turn).
        /// Collects affordable reactive cards from hand.
        /// If any exist → opens the reaction window (player must pick one, no cancel).
        /// If none → shows a message.
        /// </summary>
        private async void OnReactClicked()
        {
            if (_gs == null || _gs.GameOver) return;
            if (_reactiveWindowUI == null) return;

            // Block if AI is currently resolving its own reactive card
            if (_aiReactionWindowActive)
            {
                TurnManager.BroadcastMessage_Static("[反应] AI 正在响应，请等待结算完毕…");
                return;
            }

            // Collect affordable reactive spells from player hand
            var reactives = new List<UnitInstance>();
            foreach (var c in _gs.PHand)
            {
                if (c.CardData.IsSpell &&
                    c.CardData.HasKeyword(CardKeyword.Reactive) &&
                    c.CardData.Cost <= _gs.PMana)
                {
                    reactives.Add(c);
                }
            }

            if (reactives.Count == 0)
            {
                TurnManager.BroadcastMessage_Static(
                    $"[反应] 当前没有可打出的反应牌（手牌无反应法术或法力不足，当前法力：{_gs.PMana}）");
                return;
            }

            // ── 反应窗口触发：冻结 AI 后续行动，直到反应牌结算完毕 ────────────
            _reactionWindowActive = true;
            _reactionTcs = new TaskCompletionSource<bool>();
            TurnManager.ShowBanner_Static("⚡ 反应窗口触发！");
            TurnManager.BroadcastMessage_Static(
                $"[反应] 反应窗口开启，双方行动暂停（{reactives.Count}张可用，当前法力：{_gs.PMana}）");

            var picked = await _reactiveWindowUI.WaitForReaction(
                reactives,
                $"选择反应牌打出（必须打出一张，当前法力：{_gs.PMana}）",
                _gs);

            if (picked != null)
            {
                _gs.PMana -= picked.CardData.Cost;
                TurnManager.BroadcastMessage_Static(
                    $"[反应] 打出 {picked.UnitName}（费用{picked.CardData.Cost}），剩余法力 {_gs.PMana}");
                // ApplyReactive handles hand→discard move internally
                _reactiveSys?.ApplyReactive(picked, GameRules.OWNER_PLAYER, null, _gs);
                RefreshUI();
            }

            // ── 反应结算完毕：解除冻结 ────────────────────────────────────────
            _reactionWindowActive = false;
            _reactionTcs?.TrySetResult(true);
            _reactionTcs = null;
        }

        // ── Legend skill button ───────────────────────────────────────────────

        /// <summary>
        /// Player clicks the 虚空感知 button on the legend panel.
        /// Can be used as a reaction (outside own turn), or during own action phase.
        /// Kaisa only; no AI legend active skill.
        /// </summary>
        private void OnLegendSkillClicked()
        {
            if (_gs == null || _gs.GameOver) return;
            if (_legendSys == null) return;

            bool used = _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs);
            if (used) RefreshUI();
        }

        // ── DEBUG methods ─────────────────────────────────────────────────────

        /// <summary>
        /// Finds the first card of the given type in the player's deck and
        /// moves it to hand. Does NOT deduct mana.
        /// cardType: "spell" | "equip" | "unit"
        /// </summary>
        private void DebugDraw(string cardType)
        {
            if (_gs == null) return;

            List<UnitInstance> deck = _gs.PDeck;
            UnitInstance found = null;

            foreach (UnitInstance u in deck)
            {
                bool match = cardType == "spell"    ? (u.CardData.IsSpell && !u.CardData.HasKeyword(CardKeyword.Reactive))
                    : cardType == "equip"           ? u.CardData.IsEquipment
                    : cardType == "reactive"        ? (u.CardData.IsSpell && u.CardData.HasKeyword(CardKeyword.Reactive))
                    : !u.CardData.IsSpell && !u.CardData.IsEquipment;
                if (match) { found = u; break; }
            }

            if (found != null)
            {
                deck.Remove(found);
                _gs.PHand.Add(found);
                TurnManager.BroadcastMessage_Static($"[DEBUG] 强制摸牌 → {found.UnitName}（手牌 {_gs.PHand.Count}）");
            }
            else
            {
                string typeName = cardType == "spell" ? "法术" : cardType == "equip" ? "装备"
                    : cardType == "reactive" ? "反应" : "单位";
                TurnManager.BroadcastMessage_Static($"[DEBUG] 牌库中已无{typeName}牌");
            }

            RefreshUI();
        }

        /// <summary>
        /// Adds 5 mana to the player's current mana pool.
        /// </summary>
        private void DebugAddMana()
        {
            if (_gs == null) return;
            _gs.PMana += 5;
            TurnManager.BroadcastMessage_Static($"[DEBUG] +5法力 → {_gs.PMana}");
            RefreshUI();
        }

        private void DebugAddSch()
        {
            if (_gs == null) return;
            foreach (RuneType rt in System.Enum.GetValues(typeof(RuneType)))
                _gs.AddSch(GameRules.OWNER_PLAYER, rt, 5);
            TurnManager.BroadcastMessage_Static("[DEBUG] +5 全符能");
            RefreshUI();
        }
    }
}
