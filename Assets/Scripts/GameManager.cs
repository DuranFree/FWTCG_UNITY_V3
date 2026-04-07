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

        // ── Static event forwarders (DEV-27: events now live in GameEventBus) ──
        /// <summary>Allows non-GameManager code to send a hint toast.</summary>
        public static void FireHintToast(string msg) => UI.GameEventBus.FireHintToast(msg);
        /// <summary>Allows any system (spell, combat, reactive) to fire damage feedback.</summary>
        public static void FireUnitDamaged(UnitInstance unit, int damage, string source = "")
            => UI.GameEventBus.FireUnitDamaged(unit, damage, source);
        public static void FireUnitDied(UnitInstance unit) => UI.GameEventBus.FireUnitDied(unit);
        public static void FireCardPlayed(UnitInstance unit, string owner) => UI.GameEventBus.FireCardPlayed(unit, owner);

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
        [SerializeField] private SpellDuelUI  _spellDuelUI;    // DEV-30 F2

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
        [SerializeField] private Button _debugFloatBtn;    // DEV-18b: cycle float/banner test
        [SerializeField] private InputField _debugDmgInput; // damage value for hit buttons
        [SerializeField] private Button _debugTakeHitBtn;   // deal damage to player's units
        [SerializeField] private Button _debugDealHitBtn;   // deal damage to enemy's units
        [SerializeField] private SpellTargetPopup _spellTargetPopup;  // DEV-16b

        private int _debugFloatIndex = 0;
        private static readonly string[] _debugFloatLabels = {
            "⚡[1] 战力+2(buff)",  "⚡[2] 战力-1(debuff)", "⚡[3] 战力+3(buff)",
            "⚡[4] 战力-2(debuff)","⚡[5] 摸1张牌",         "⚡[6] 符能+1",
            "⚡[7] 击倒",
            "⚡[8] 区域+1分",      "⚡[9] 区域法力+1",     "⚡[10] 区域符能+1",
            "⚡[11] 据守横幅",     "⚡[12] 征服横幅",      "⚡[13] 燃尽横幅",
            "⚡[14] 传说技横幅",   "⚡[15] 进化横幅",      "⚡[16] 时间扭曲横幅",
            "⚡[17] 绝念横幅",
        };

        // ── Game state ────────────────────────────────────────────────────────
        private GameState _gs;

        // ── Interaction state ─────────────────────────────────────────────────
        private UnitInstance _selectedUnit;       // single-select (for BF recall)
        private string _selectedUnitLoc;          // "hand", "base", "0", "1"
        private List<UnitInstance> _selectedBaseUnits = new List<UnitInstance>(); // multi-select for batch BF move
        private List<UnitInstance> _selectedHandUnits = new List<UnitInstance>(); // multi-select for batch hand drag
        private UnitInstance _targetingSpell;     // non-null = awaiting target click for spell
        private bool _aiReactionPending;          // DEV-15: true while AI reaction resolves
        private bool _bfClickInFlight;            // DEV-17: true while OnBattlefieldClicked awaits post-combat delay
        private bool? _pendingDragHasteDecision;  // DEV-22: pre-answered Haste choice from drag flow prompt

        // ── DEV-22: Drag query helpers (used by CardDragHandler) ─────────────

        /// <summary>True when it is the player's action phase and no AI reaction is blocking.</summary>
        public bool IsPlayerActionPhase()
        {
            if (_gs == null || _gs.GameOver) return false;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return false;
            if (_gs.Phase != GameRules.PHASE_ACTION) return false;
            if (_aiReactionPending) return false;
            if (_aiReactionWindowActive) return false;
            return true;
        }

        /// <summary>Returns true if <paramref name="unit"/> is in the player's hand.</summary>
        public bool IsUnitInHand(UnitInstance unit) => _gs != null && _gs.PHand.Contains(unit);
        public bool IsUnitHero(UnitInstance unit)   => _gs != null && _gs.PHero == unit;

        /// <summary>Returns true if <paramref name="unit"/> is in the player's base.</summary>
        public bool IsUnitInBase(UnitInstance unit) => _gs != null && _gs.PBase.Contains(unit);

        /// <summary>
        /// Returns the current multi-select list for the player base.
        /// CardDragHandler uses this to build the cluster group.
        /// </summary>
        public List<UnitInstance> GetSelectedBaseUnits() => _selectedBaseUnits;

        /// <summary>Returns the current multi-select list for the player hand.</summary>
        public List<UnitInstance> GetSelectedHandUnits() => _selectedHandUnits;

        /// <summary>Clears all hand and base selections and refreshes the UI.</summary>
        public void ClearAllSelections()
        {
            _selectedHandUnits.Clear();
            _selectedBaseUnits.Clear();
            RefreshUI();
        }

        /// <summary>
        /// DEV-22: Drag-to-base — equivalent to clicking a hand card.
        /// Handles both unit cards (plays to base) and spell cards (enters targeting mode).
        /// </summary>
        public void OnDragCardToBase(UnitInstance unit)
        {
            if (!IsPlayerActionPhase()) return;
            if (_gs == null || !_gs.PHand.Contains(unit)) return;
            _ = PlayHandCardWithRuneConfirmAsync(unit);
        }

        /// <summary>
        /// DEV-22: Multi-select hand unit drag — plays all units in the group to base.
        /// </summary>
        public void OnDragHandGroupToBase(List<UnitInstance> units)
        {
            if (!IsPlayerActionPhase()) return;
            if (units == null || units.Count == 0) return;
            foreach (var u in units)
            {
                if (_gs != null && _gs.PHand.Contains(u) && !u.CardData.IsSpell)
                    _ = PlayHandCardWithRuneConfirmAsync(u);
            }
        }

        /// <summary>
        /// DEV-22: Spell dragged outside hand zone — equivalent to clicking the spell.
        /// Enters targeting mode (or casts immediately for SpellTargetType.None).
        /// </summary>
        public void OnSpellDraggedOut(UnitInstance unit)
        {
            if (!IsPlayerActionPhase()) return;
            if (_gs == null || !_gs.PHand.Contains(unit)) return;
            if (!unit.CardData.IsSpell) return;
            _ = PlayHandCardWithRuneConfirmAsync(unit);
        }

        /// <summary>
        /// DEV-22: Multiple selected spell cards dragged out of hand — shows group showcase then casts all.
        /// </summary>
        public void OnSpellGroupDraggedOut(List<UnitInstance> spells)
        {
            if (!IsPlayerActionPhase()) return;
            if (spells == null || spells.Count == 0) return;
            _ = PlaySpellGroupAsync(spells);
        }

        private async System.Threading.Tasks.Task PlaySpellGroupAsync(List<UnitInstance> spells)
        {
            // Filter to valid hand spells only, preserving selection order
            var valid = new List<UnitInstance>();
            foreach (var s in spells)
                if (_gs != null && _gs.PHand.Contains(s) && s.CardData.IsSpell) valid.Add(s);
            if (valid.Count == 0) return;

            // Cast each spell using the normal single-spell flow:
            //   - SpellTargetType.None  → auto-resolve (random/AoE), no popup
            //   - SpellTargetType != None → show target selection popup
            foreach (var spell in valid)
            {
                if (_gs == null || _gs.GameOver) break;
                if (_gs.PHand.Contains(spell))
                    await PlayHandCardWithRuneConfirmAsync(spell);
            }
        }

        /// <summary>
        /// Hero card dragged from hero zone to base — routes through rune confirm flow
        /// so the player sees a cost prompt before resources are spent.
        /// </summary>
        public void OnDragHeroToBase(UnitInstance hero)
        {
            if (!IsPlayerActionPhase()) return;
            if (_gs == null || _gs.PHero != hero) return;
            _ = PlayHeroWithRuneConfirmAsync(hero);
        }

        /// DEV-22: Drag base units to a battlefield — equivalent to multi-selecting then clicking the BF.
        /// Replaces the current selection with <paramref name="units"/> and routes to OnBattlefieldClicked.
        /// </summary>
        /// <summary>
        /// Returns true if dragging this unit to its zone should show a Haste prompt
        /// BEFORE the landing animation plays. CardDragHandler calls this to freeze ghosts first.
        /// </summary>
        public bool DragNeedsHasteChoice(UnitInstance unit)
        {
            if (unit == null || _gs == null) return false;
            if (!unit.CardData.HasKeyword(CardKeyword.Haste)) return false;
            int extraManaNeeded = unit.CardData.Cost + 1;
            int extraSchNeeded  = unit.CardData.RuneCost + 1;
            int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType);
            return _gs.PMana >= extraManaNeeded && haveSch >= extraSchNeeded;
        }

        /// <summary>
        /// Pre-answers the Haste choice so TryPlayUnitAsync / TryPlayHeroAsync skips the prompt.
        /// Called by CardDragHandler after the user confirms in the drag-flow prompt.
        /// </summary>
        public void SetDragHasteDecision(bool useHaste) => _pendingDragHasteDecision = useHaste;

        public void OnDragUnitsToBF(List<UnitInstance> units, int bfId)
        {
            if (!IsPlayerActionPhase()) return;
            if (units == null || units.Count == 0) return;
            // Replace current selection with dragged group
            _selectedBaseUnits.Clear();
            _selectedUnit     = null;
            _selectedUnitLoc  = null;
            foreach (var u in units)
            {
                if (_gs.PBase.Contains(u) && !u.Exhausted)
                    _selectedBaseUnits.Add(u);
            }
            if (_selectedBaseUnits.Count == 0) return;
            OnBattlefieldClicked(bfId);
        }

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
            if (_spellDuelUI == null) _spellDuelUI = gameObject.AddComponent<SpellDuelUI>(); // DEV-30 F2

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
            if (_debugFloatBtn != null)    _debugFloatBtn.onClick.AddListener(DebugCycleFloat);
            if (_debugTakeHitBtn != null)  _debugTakeHitBtn.onClick.AddListener(() => DebugApplyDamage(GameRules.OWNER_PLAYER));
            if (_debugDealHitBtn != null)  _debugDealHitBtn.onClick.AddListener(() => DebugApplyDamage(GameRules.OWNER_ENEMY));
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

        private void OnDestroy()
        {
            // Cancel static TCS fields so awaiters don't hang after scene unload (H-3 fix)
            _reactionWindowActive   = false;
            _aiReactionWindowActive = false;
            _reactionTcs?.TrySetCanceled();
            _reactionTcs = null;
            _aiReactionTcs?.TrySetCanceled();
            _aiReactionTcs = null;
            if (Instance == this) Instance = null;
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
                    onCardRightClick: OnCardRightClicked,
                    onCardHoverEnter: OnCardHoverEnter,
                    onCardHoverExit:  OnCardHoverExit,
                    onHeroHoverEnter: OnHeroHoverEnter,
                    onHeroHoverExit:  OnHeroHoverExit
                );
                _ui.SetPileClickCallback(OnPileClicked);
                _ui.WirePileButtons();
                // DEV-22: wire drag callbacks and push zone RTs to CardDragHandler
                _ui.SetDragCallbacks(
                    onDragCardToBase:      OnDragCardToBase,
                    onDragHandGroupToBase: OnDragHandGroupToBase,
                    onSpellDragOut:        OnSpellDraggedOut,
                    onSpellGroupDragOut:   OnSpellGroupDraggedOut,
                    onDragHeroToBase:      OnDragHeroToBase,
                    onDragUnitsToBF:       OnDragUnitsToBF
                );
                _ui.SetupDragZones();
            }

            InitGame();
            StartCoroutine(RunWithStartup());
        }

        // ── Initialisation ────────────────────────────────────────────────────

        private void InitGame()
        {
            TurnStateMachine.Reset(); // DEV-27: reset static state on game init (survives scene reload)
            GameState.ResetUidCounter();
            _gs = new GameState();

            // Inject dependencies into systems
            _turnMgr.Inject(_gs, _scoreMgr, _combatSys, _ai, _entryEffects,
                            _spellSys, _reactiveSys, _reactiveWindowUI, _legendSys, _bfSys);

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

            // Initialize legends — derived from each side's hero card (dynamic, not hardcoded)
            if (_legendSys != null)
            {
                AssignLegendFromHero(GameRules.OWNER_PLAYER);
                AssignLegendFromHero(GameRules.OWNER_ENEMY);
            }

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
            if (_aiReactionWindowActive) { TurnManager.BroadcastMessage_Static("[反应] AI 正在使用反应牌，请等待结算完毕…"); return; }

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

            // ── Hand card: toggle multi-select (drag is the play action) ──
            if (_gs.PHand.Contains(unit))
            {
                if (_selectedHandUnits.Contains(unit))
                {
                    _selectedHandUnits.Remove(unit);
                    TurnManager.BroadcastMessage_Static($"[取消选择] {unit.UnitName}（已选{_selectedHandUnits.Count}张）");
                }
                else
                {
                    // Spell cards: only one at a time — shake + hint if another spell already selected
                    if (unit.CardData.IsSpell && _selectedHandUnits.Exists(u => u.CardData.IsSpell))
                    {
                        UI.GameEventBus.FireCardPlayFailed(unit);
                        FireHintToast("一次只能打出一张法术牌");
                        return;
                    }
                    _selectedHandUnits.Add(unit);
                    TurnManager.BroadcastMessage_Static($"[选择] {unit.UnitName}（已选{_selectedHandUnits.Count}张）— 拖拽出牌");
                }
                RefreshUI();
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
                    else if (unit.CardData.IsEquipment && _selectedBaseUnits.Exists(u => u.CardData.IsEquipment))
                    {
                        // Equipment single-select restriction: only one equipment can be activated at a time
                        UI.GameEventBus.FireCardPlayFailed(unit);
                        FireHintToast("一次只能使用一张装备牌");
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

            // ── Hero zone: play hero card to base (with rune confirm) ──
            if (_gs.PHero == unit)
            {
                _ = PlayHeroWithRuneConfirmAsync(unit);
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
            try
            {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;
            if (_aiReactionPending) return; // DEV-15
            if (_bfClickInFlight) return;   // DEV-17: block reentrant clicks during post-combat delay
            if (_aiReactionWindowActive) { TurnManager.BroadcastMessage_Static("[反应] AI 正在使用反应牌，请等待结算完毕…"); return; }

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
                        if (u.CardData.IsEquipment)
                            await ActivateEquipmentAsync(u);
                        else
                            _combatSys.MoveUnit(u, "base", bfId, GameRules.OWNER_PLAYER, _gs);
                    }
                }

                // Refresh UI first so CardViews are at BF position before combat animations fire
                RefreshUI();

                if (_gs.BF[bfId].EnemyUnits.Count > 0)
                {
                    await System.Threading.Tasks.Task.Delay(500);  // unit lands → 0.5s pause
                    UI.GameEventBus.FireDuelBanner();
                    await System.Threading.Tasks.Task.Delay(2000); // banner 1.5s + 0.5s gap
                    UI.GameEventBus.FireSetBannerDelay(0.5f);      // combat EventBanners wait 0.5s
                }

                // After all units moved, check combat on this BF
                _combatSys.CheckAndResolveCombat(bfId, GameRules.OWNER_PLAYER, _gs, _scoreMgr);

                _selectedUnit = null;
                _selectedUnitLoc = null;
                // DEV-17: wait for hit flash + death animation before destroying CardViews (≈0.5s gap after combat)
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
                // Rule 722: only units with Roam keyword can move between battlefields
                if (!_selectedUnit.CardData.HasKeyword(CardKeyword.Roam))
                {
                    TurnManager.BroadcastMessage_Static($"[提示] {_selectedUnit.UnitName} 没有游走，无法移动到其他战场");
                    _selectedUnit = null;
                    _selectedUnitLoc = null;
                    return;
                }

                if (_selectedUnit.Exhausted)
                {
                    TurnManager.BroadcastMessage_Static($"[提示] {_selectedUnit.UnitName} 已休眠，无法移动");
                    _selectedUnit = null;
                    _selectedUnitLoc = null;
                    return;
                }

                _combatSys.MoveUnit(_selectedUnit, _selectedUnitLoc, bfId, GameRules.OWNER_PLAYER, _gs);
                RefreshUI(); // move CardView to BF position before combat animations fire

                if (_gs.BF[bfId].EnemyUnits.Count > 0)
                {
                    await System.Threading.Tasks.Task.Delay(500);  // unit lands → 0.5s pause
                    UI.GameEventBus.FireDuelBanner();
                    await System.Threading.Tasks.Task.Delay(2000); // banner 1.5s + 0.5s gap
                    UI.GameEventBus.FireSetBannerDelay(0.5f);      // combat EventBanners wait 0.5s
                }

                _combatSys.CheckAndResolveCombat(bfId, GameRules.OWNER_PLAYER, _gs, _scoreMgr);

                _selectedUnit = null;
                _selectedUnitLoc = null;
                // DEV-17: wait for hit flash + death animation before destroying CardViews (≈0.5s gap after combat)
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
            } // DEV-26: outer try
            catch (System.Exception ex)
            {
                Debug.LogError($"[OnBattlefieldClicked] 未处理异常: {ex}");
                _bfClickInFlight = false;
                RefreshUI(); // DEV-26: restore UI to consistent state after unexpected exception
            }
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
            _selectedHandUnits.Clear();
            _turnMgr.EndTurn();
            RefreshUI();
        }

        /// <summary>
        /// Called when the player clicks a rune button.
        /// recycle=false → tap for mana; recycle=true → recycle for schematic energy.
        /// </summary>
        public void OnRuneClicked(int runeIdx, bool recycle)
        {
            if (_gs == null || _gs.GameOver) return;
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
                // Use RuneAutoConsume.CanRecycle as single source of truth (same rule as Compute()).
                if (!RuneAutoConsume.CanRecycle(rune))
                {
                    TurnManager.BroadcastMessage_Static("[提示] 已横置的符文无法回收");
                    return;
                }
                // Recycle: remove from active runes, place at bottom of rune deck, gain +1 sch
                runes.RemoveAt(runeIdx);
                _gs.PRuneDeck.Add(rune);
                _gs.AddSch(GameRules.OWNER_PLAYER, rune.RuneType, 1);
                TurnManager.BroadcastMessage_Static(
                    $"[回收] 符文 {rune.RuneType.ToChinese()} 回收，获得 1 点{rune.RuneType.ToChinese()}符能");
                UI.GameEventBus.FireRuneRecycleFloat(GameRules.OWNER_PLAYER); // DEV-18b
            }
            else
            {
                // Use RuneAutoConsume.CanTap as single source of truth (same rule as Compute()).
                if (!RuneAutoConsume.CanTap(rune))
                {
                    TurnManager.BroadcastMessage_Static("[提示] 该符文已横置");
                    return;
                }
                rune.Tapped = true;
                _gs.PMana += 1;
                TurnManager.BroadcastMessage_Static(
                    $"[横置] 符文 {rune.RuneType.ToChinese()} 横置，法力 → {_gs.PMana}");
                UI.GameEventBus.FireRuneTapFloat(GameRules.OWNER_PLAYER); // DEV-18b
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
            UI.GameEventBus.FireHintToast(hint);
            if (card != null) UI.GameEventBus.FireCardPlayFailed(card);
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

        // ── Hero hover: show rune cost preview ──────────────────────────────

        private void OnHeroHoverEnter(UnitInstance unit)
        {
            if (_ui == null || _gs == null) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
            if (_gs.PHero != unit) return;

            var plan = RuneAutoConsume.Compute(unit, _gs, GameRules.OWNER_PLAYER);
            if (plan.NeedsOps)
            {
                _ui.SetRuneHighlights(plan.TapIndices, plan.RecycleIndices);
                _ui.Refresh(_gs);
            }
        }

        private void OnHeroHoverExit(UnitInstance unit)
        {
            if (_ui == null) return;
            _ui.ClearRuneHighlights();
            _ui.Refresh(_gs);
        }

        // ── Hero rune confirm flow ──────────────────────────────────────────

        private async System.Threading.Tasks.Task PlayHeroWithRuneConfirmAsync(UnitInstance hero)
        {
            if (_gs == null || _gs.GameOver) return;

            var plan = RuneAutoConsume.Compute(hero, _gs, GameRules.OWNER_PLAYER);

            if (!plan.CanAfford)
            {
                int haveMana = _gs.PMana;
                int haveSch  = _gs.GetSch(GameRules.OWNER_PLAYER, hero.CardData.RuneType);
                ShowPlayError(
                    $"[提示] 资源不足：需要法力{hero.CardData.Cost}（当前{haveMana}）" +
                    (hero.CardData.RuneCost > 0
                        ? $"，需要符能{hero.CardData.RuneCost} {hero.CardData.RuneType.ToColoredText()}（当前{haveSch}）"
                        : ""),
                    hero);
                return;
            }

            if (plan.NeedsOps)
            {
                _ui?.SetRuneHighlights(plan.TapIndices, plan.RecycleIndices);
                _ui?.Refresh(_gs);

                bool ok = false;
                try
                {
                    ok = await (AskPromptUI.Instance?.WaitForConfirm(
                        "英雄出场",
                        plan.BuildConfirmText(hero),
                        "确认出场",
                        "取消") ?? System.Threading.Tasks.Task.FromResult(false));
                }
                catch (System.OperationCanceledException)
                {
                    ok = false;
                }

                _ui?.ClearRuneHighlights();
                _ui?.Refresh(_gs);

                if (!ok) return;

                // Re-validate after async prompt
                if (_gs == null || _gs.GameOver) return;
                if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
                if (_gs.PHero != hero) return;

                var freshPlan = RuneAutoConsume.Compute(hero, _gs, GameRules.OWNER_PLAYER);
                if (!freshPlan.CanAfford)
                {
                    ShowPlayError("[提示] 资源状态已变更，操作已取消", hero);
                    return;
                }

                ExecuteRunePlan(freshPlan, GameRules.OWNER_PLAYER);
            }

            // Final re-validate
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
            if (_gs.PHero != hero) return;

            _ = TryPlayHeroAsync(hero);
        }

        // ── DEV-20: Rune auto-consume hover ──────────────────────────────────

        private void OnCardHoverEnter(UnitInstance unit)
        {
            if (_ui == null || _gs == null) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
            if (!_gs.PHand.Contains(unit)) return;

            var plan = RuneAutoConsume.Compute(unit, _gs, GameRules.OWNER_PLAYER);
            if (plan.NeedsOps)
            {
                _ui.SetRuneHighlights(plan.TapIndices, plan.RecycleIndices);
                _ui.Refresh(_gs);
            }
        }

        private void OnCardHoverExit(UnitInstance unit)
        {
            if (_ui == null) return;
            _ui.ClearRuneHighlights();
            _ui.Refresh(_gs);
        }

        // ── DEV-20: Async card play with rune confirm dialog ─────────────────

        private async System.Threading.Tasks.Task PlayHandCardWithRuneConfirmAsync(UnitInstance unit)
        {
            if (_gs == null || _gs.GameOver) return;

            var plan = RuneAutoConsume.Compute(unit, _gs, GameRules.OWNER_PLAYER);

            if (!plan.CanAfford)
            {
                // Not affordable even after consuming all runes — show error
                int haveMana = _gs.PMana;
                int haveSch  = _gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType);
                ShowPlayError(
                    $"[提示] 资源不足：需要法力{unit.CardData.Cost}（当前{haveMana}）" +
                    (unit.CardData.RuneCost > 0
                        ? $"，需要符能{unit.CardData.RuneCost} {unit.CardData.RuneType.ToColoredText()}（当前{haveSch}）"
                        : ""),
                    unit);
                return;
            }

            if (plan.NeedsOps)
            {
                // Show rune highlights immediately
                _ui?.SetRuneHighlights(plan.TapIndices, plan.RecycleIndices);
                _ui?.Refresh(_gs);

                bool ok = false;
                try
                {
                    ok = await (AskPromptUI.Instance?.WaitForConfirm(
                        "出牌确认",
                        plan.BuildConfirmText(unit),
                        "确认打出",
                        "取消") ?? System.Threading.Tasks.Task.FromResult(false));
                }
                catch (System.OperationCanceledException)
                {
                    ok = false;
                }

                _ui?.ClearRuneHighlights();
                _ui?.Refresh(_gs);

                if (!ok) return;

                // H-4: Re-validate state after await — turn/phase may have changed while prompt was open
                if (_gs == null || _gs.GameOver) return;
                if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
                if (!_gs.PHand.Contains(unit)) return;

                // Re-compute plan to verify runes haven't changed
                var freshPlan = RuneAutoConsume.Compute(unit, _gs, GameRules.OWNER_PLAYER);
                if (!freshPlan.CanAfford)
                {
                    ShowPlayError("[提示] 资源状态已变更，操作已取消", unit);
                    return;
                }

                ExecuteRunePlan(freshPlan, GameRules.OWNER_PLAYER);
            }

            // H-4: Re-validate once more before the final play
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
            if (!_gs.PHand.Contains(unit)) return;

            // Remove from selection only when the card is actually being played (not on drag start or cancel)
            _selectedHandUnits.Remove(unit);
            TryPlayCard(unit);
        }

        private void ExecuteRunePlan(RuneAutoConsume.Plan plan, string owner)
        {
            var runes = _gs.GetRunes(owner);

            // Recycle indices first (descending to preserve index validity)
            var sortedRecycle = new List<int>(plan.RecycleIndices);
            sortedRecycle.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in sortedRecycle)
            {
                if (idx < 0 || idx >= runes.Count) continue;
                RuneInstance r = runes[idx];
                _gs.AddSch(owner, r.RuneType, 1);
                runes.RemoveAt(idx);
                _gs.GetRuneDeck(owner).Add(r); // H-1: return rune to deck, not lost
                TurnManager.BroadcastMessage_Static($"[符文] 回收 {r.RuneType.ToChinese()} 符文，获得1点符能");
            }

            // Tap indices (after recycle, indices may have shifted — but tap list is from original state
            // so we must use original indices adjusted for removed items)
            // Sort tap indices ascending and adjust for removed runes
            var sortedTap = new List<int>(plan.TapIndices);
            sortedTap.Sort();
            int removedBefore = 0;
            // Build sorted recycle set for offset calculation
            var recycleAscending = new List<int>(plan.RecycleIndices);
            recycleAscending.Sort();
            int recyclePtr = 0;

            foreach (int origIdx in sortedTap)
            {
                // Count how many recycles happened at indices < origIdx
                while (recyclePtr < recycleAscending.Count && recycleAscending[recyclePtr] < origIdx)
                {
                    removedBefore++;
                    recyclePtr++;
                }
                int adjustedIdx = origIdx - removedBefore;
                if (adjustedIdx < 0 || adjustedIdx >= runes.Count) continue;
                runes[adjustedIdx].Tapped = true;
                _gs.AddMana(owner, 1);
                TurnManager.BroadcastMessage_Static($"[符文] 横置 {runes[adjustedIdx].RuneType.ToChinese()} 符文，获得1点法力");
            }
        }

        private void TryPlayCard(UnitInstance unit)
        {
            if (_aiReactionWindowActive) { TurnManager.BroadcastMessage_Static("[反应] AI 正在使用反应牌，请等待结算完毕…"); return; }
            if (unit.CardData.IsSpell)
                _ = TryPlaySpellAsync(unit);
            else if (unit.CardData.IsEquipment)
                TryDeployEquipmentToBase(unit);
            else
                _ = TryPlayUnitAsync(unit);
        }

        private async Task TryPlayUnitAsync(UnitInstance unit)
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
                    ShowPlayError($"[提示] 符能不足：需要 {unit.CardData.RuneCost} {unit.CardData.RuneType.ToColoredText()}，当前 {haveSch}", unit);
                    _selectedUnit = null;
                    return;
                }
            }

            // Rule 717: Haste — optional extra [1] mana + [1C] sch to enter active
            bool useHaste = false;
            if (unit.CardData.HasKeyword(CardKeyword.Haste))
            {
                if (_pendingDragHasteDecision.HasValue)
                {
                    // Pre-answered by drag-flow prompt — skip showing the dialog again
                    useHaste = _pendingDragHasteDecision.Value;
                    _pendingDragHasteDecision = null;
                }
                else
                {
                    int extraManaNeeded = unit.CardData.Cost + 1;
                    int extraSchNeeded  = unit.CardData.RuneCost + 1;
                    int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType);
                    bool canAfford = _gs.PMana >= extraManaNeeded && haveSch >= extraSchNeeded;

                    if (canAfford && AskPromptUI.Instance != null)
                    {
                        try
                        {
                            useHaste = await AskPromptUI.Instance.WaitForConfirm(
                                "急速",
                                $"额外支付 [1] 法力 + [1{unit.CardData.RuneType.ToColoredText()}] 符能，让 {unit.UnitName} 以活跃状态进场？",
                                "使用急速",
                                "休眠进场");
                        }
                        catch { useHaste = false; }
                    }
                }
            }

            // H-5: Re-validate after async Haste prompt — turn/resources may have changed
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
            if (!_gs.PHand.Contains(unit)) return;
            if (_gs.PMana < unit.CardData.Cost)
            {
                ShowPlayError("[提示] 资源状态已变更，操作已取消", unit);
                _selectedUnit = null;
                return;
            }
            if (unit.CardData.RuneCost > 0 &&
                _gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType) < unit.CardData.RuneCost)
            {
                ShowPlayError("[提示] 资源状态已变更，操作已取消", unit);
                _selectedUnit = null;
                return;
            }
            // If Haste was chosen, re-verify extra cost is still affordable; downgrade silently if not
            if (useHaste)
            {
                bool stillCanAffordHaste = _gs.PMana >= unit.CardData.Cost + 1 &&
                    _gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType) >= unit.CardData.RuneCost + 1;
                if (!stillCanAffordHaste) useHaste = false;
            }

            _gs.PHand.Remove(unit);
            _gs.PBase.Add(unit);
            _gs.PMana -= unit.CardData.Cost;
            if (unit.CardData.RuneCost > 0)
                _gs.SpendSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType, unit.CardData.RuneCost);

            // Pay Haste extra cost if chosen
            if (useHaste)
            {
                _gs.PMana -= 1;
                _gs.SpendSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType, 1);
                unit.Exhausted = false;
                TurnManager.BroadcastMessage_Static(
                    $"[急速] {unit.UnitName} 支付额外1法力+1{unit.CardData.RuneType.ToChinese()}符能，以活跃状态进场");
                UI.GameEventBus.FireUnitFloatText(unit, "急速！", UI.GameColors.BuffColor);
            }
            else
            {
                unit.Exhausted = true;
            }

            if (unit.IsEphemeral) unit.SummonedOnRound = _gs.Round;
            _gs.CardsPlayedThisTurn++;
            FireCardPlayed(unit, GameRules.OWNER_PLAYER);
            TurnManager.BroadcastMessage_Static(
                $"[打出] {unit.UnitName}（费用{unit.CardData.Cost}），剩余法力 {_gs.PMana}");

            // Trigger entry effects
            if (_entryEffects != null)
                _entryEffects.OnUnitEntered(unit, GameRules.OWNER_PLAYER, _gs);

            // DEV-26: Foresight prompt (player only — AI handled in EntryEffectSystem)
            if (unit.CardData.HasKeyword(CardKeyword.Foresight))
                await HandleForesightPromptAsync(GameRules.OWNER_PLAYER);

            // Check Kaisa evolution (4 distinct allied keywords → Lv.2)
            _legendSys?.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);

            _selectedUnit = null;
            _selectedUnitLoc = "base";
            RefreshUI();
        }

        /// <summary>
        /// Plays a hero card from the hero zone to the player's base.
        /// DEV-26: Shows a confirm dialog for Foresight keyword (preview top card, option to move to bottom).
        /// Only called for the player; AI handling remains in EntryEffectSystem (log only).
        /// </summary>
        private async System.Threading.Tasks.Task HandleForesightPromptAsync(string owner)
        {
            var deck = _gs.GetDeck(owner);
            if (deck.Count == 0) return;
            var topCard = deck[0];

            bool moveToBottom = false;
            try
            {
                moveToBottom = await (UI.AskPromptUI.Instance?.WaitForConfirm(
                    "预知",
                    $"牌库顶：{topCard.UnitName}\n费用 {topCard.CardData.Cost}  战力 {topCard.CardData.Atk}\n\n将此牌置底？",
                    "置底",
                    "保留") ?? System.Threading.Tasks.Task.FromResult(false));
            }
            catch (System.OperationCanceledException) { moveToBottom = false; }

            if (moveToBottom)
            {
                deck.RemoveAt(0);
                deck.Add(topCard);
                TurnManager.BroadcastMessage_Static($"[预知] {topCard.UnitName} 已置底");
            }
            else
            {
                TurnManager.BroadcastMessage_Static($"[预知] {topCard.UnitName} 保留在牌库顶");
            }
        }

        /// Mirrors TryPlayUnitAsync but sources from gs.PHero instead of gs.PHand.
        /// </summary>
        private async Task TryPlayHeroAsync(UnitInstance hero)
        {
            if (hero.CardData.Cost > _gs.PMana)
            {
                ShowPlayError($"[提示] 法力不足：需要 {hero.CardData.Cost}，当前 {_gs.PMana}", hero);
                return;
            }

            if (hero.CardData.RuneCost > 0)
            {
                int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, hero.CardData.RuneType);
                if (haveSch < hero.CardData.RuneCost)
                {
                    ShowPlayError($"[提示] 符能不足：需要 {hero.CardData.RuneCost} {hero.CardData.RuneType.ToColoredText()}，当前 {haveSch}", hero);
                    return;
                }
            }

            // Haste prompt (same as unit)
            bool useHaste = false;
            if (hero.CardData.HasKeyword(CardKeyword.Haste))
            {
                if (_pendingDragHasteDecision.HasValue)
                {
                    useHaste = _pendingDragHasteDecision.Value;
                    _pendingDragHasteDecision = null;
                }
                else
                {
                    int extraManaNeeded = hero.CardData.Cost + 1;
                    int extraSchNeeded  = hero.CardData.RuneCost + 1;
                    int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, hero.CardData.RuneType);
                    bool canAfford = _gs.PMana >= extraManaNeeded && haveSch >= extraSchNeeded;
                    if (canAfford && AskPromptUI.Instance != null)
                    {
                        try
                        {
                            useHaste = await AskPromptUI.Instance.WaitForConfirm(
                                "急速",
                                $"额外支付 [1] 法力 + [1{hero.CardData.RuneType.ToColoredText()}] 符能，让 {hero.UnitName} 以活跃状态进场？",
                                "使用急速",
                                "休眠进场");
                        }
                        catch { useHaste = false; }
                    }
                }
            }

            // Re-validate after async prompt
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
            if (_gs.PHero != hero) return; // hero already deployed or replaced
            if (_gs.PMana < hero.CardData.Cost)
            {
                ShowPlayError("[提示] 资源状态已变更，操作已取消", hero);
                return;
            }
            if (hero.CardData.RuneCost > 0 &&
                _gs.GetSch(GameRules.OWNER_PLAYER, hero.CardData.RuneType) < hero.CardData.RuneCost)
            {
                ShowPlayError("[提示] 资源状态已变更，操作已取消", hero);
                return;
            }
            if (useHaste)
            {
                bool stillOk = _gs.PMana >= hero.CardData.Cost + 1 &&
                               _gs.GetSch(GameRules.OWNER_PLAYER, hero.CardData.RuneType) >= hero.CardData.RuneCost + 1;
                if (!stillOk) useHaste = false;
            }

            // Deploy: remove from hero zone → add to base
            _gs.PHero = null;
            _gs.PBase.Add(hero);
            _gs.PMana -= hero.CardData.Cost;
            if (hero.CardData.RuneCost > 0)
                _gs.SpendSch(GameRules.OWNER_PLAYER, hero.CardData.RuneType, hero.CardData.RuneCost);

            if (useHaste)
            {
                _gs.PMana -= 1;
                _gs.SpendSch(GameRules.OWNER_PLAYER, hero.CardData.RuneType, 1);
                hero.Exhausted = false;
                TurnManager.BroadcastMessage_Static(
                    $"[急速] {hero.UnitName} 支付额外1法力+1{hero.CardData.RuneType.ToChinese()}符能，以活跃状态进场");
                UI.GameEventBus.FireUnitFloatText(hero, "急速！", UI.GameColors.BuffColor);
            }
            else
            {
                hero.Exhausted = true;
            }

            if (hero.IsEphemeral) hero.SummonedOnRound = _gs.Round;
            _gs.CardsPlayedThisTurn++;
            FireCardPlayed(hero, GameRules.OWNER_PLAYER);
            TurnManager.BroadcastMessage_Static(
                $"[英雄出场] {hero.UnitName}（费用{hero.CardData.Cost}），剩余法力 {_gs.PMana}");

            if (_entryEffects != null)
                _entryEffects.OnUnitEntered(hero, GameRules.OWNER_PLAYER, _gs);

            // DEV-26: mirror TryPlayUnitAsync — hero cards can also have Foresight
            if (hero.CardData.HasKeyword(CardKeyword.Foresight))
                await HandleForesightPromptAsync(GameRules.OWNER_PLAYER);

            _legendSys?.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);

            _selectedUnit = null;
            RefreshUI();
        }

        /// <summary>
        /// Phase 1: play equipment card from hand to base (pays mana/rune, card stays on base ready to activate).
        /// </summary>
        private void TryDeployEquipmentToBase(UnitInstance equip)
        {
            if (equip.CardData.Cost > _gs.PMana)
            {
                ShowPlayError($"[提示] 法力不足：需要 {equip.CardData.Cost}，当前 {_gs.PMana}", equip);
                return;
            }
            if (equip.CardData.RuneCost > 0)
            {
                int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, equip.CardData.RuneType);
                if (haveSch < equip.CardData.RuneCost)
                {
                    ShowPlayError($"[提示] 符能不足：需要 {equip.CardData.RuneCost} {equip.CardData.RuneType.ToColoredText()}，当前 {haveSch}", equip);
                    return;
                }
            }

            _gs.PHand.Remove(equip);
            _gs.PBase.Add(equip);
            _gs.PMana -= equip.CardData.Cost;
            if (equip.CardData.RuneCost > 0)
                _gs.SpendSch(GameRules.OWNER_PLAYER, equip.CardData.RuneType, equip.CardData.RuneCost);
            // Standby keyword = enters hibernating (face-down reactive); otherwise enters active
            equip.Exhausted = equip.CardData.HasKeyword(CardKeyword.Standby);
            _gs.CardsPlayedThisTurn++;
            FireCardPlayed(equip, GameRules.OWNER_PLAYER);
            TurnManager.BroadcastMessage_Static(
                $"[装备] {equip.UnitName} 部署到基地（费用{equip.CardData.Cost}），剩余法力 {_gs.PMana}");
            RefreshUI();
        }

        /// <summary>
        /// Phase 2: activate equipment from base — player selects a friendly unit via popup to attach to.
        /// Cancelling leaves the equipment in base for a future attempt (no refund, cost was paid on deploy).
        /// </summary>
        private async System.Threading.Tasks.Task ActivateEquipmentAsync(UnitInstance equip)
        {
            // Check activation rune cost (EquipRuneCost, separate from deploy mana cost)
            if (equip.CardData.EquipRuneCost > 0)
            {
                int haveSch = _gs.GetSch(GameRules.OWNER_PLAYER, equip.CardData.EquipRuneType);
                if (haveSch < equip.CardData.EquipRuneCost)
                {
                    ShowPlayError(
                        $"[提示] 符能不足：附着需要 {equip.CardData.EquipRuneCost} {equip.CardData.EquipRuneType.ToColoredText()}，当前 {haveSch}",
                        equip);
                    return;
                }
            }

            // Refresh UI first so CardViews are up to date, THEN hide the equipment card
            RefreshUI();
            UnityEngine.Vector2 baseCanvasPos = _ui != null
                ? _ui.HideEquipCardInBase(equip)
                : UnityEngine.Vector2.zero;

            // Let player choose target via popup — only non-equipment, non-spell friendly units
            UnitInstance target = null;
            if (_spellTargetPopup != null)
            {
                // DEV-28: highlight valid targets on board; DEV-29: try/finally ensures highlights are cleared even on exception
                _ui?.ShowTargetHighlights(u => !u.CardData.IsEquipment && !u.CardData.IsSpell && u.AttachedEquipment == null);
                try
                {
                    target = await _spellTargetPopup.ShowAsync(
                        SpellTargetType.FriendlyUnit, _gs,
                        u => !u.CardData.IsEquipment && !u.CardData.IsSpell && u.AttachedEquipment == null);
                }
                finally
                {
                    _ui?.ClearTargetHighlights();
                }
            }

            // Get mouse canvas position for fly-from point
            UnityEngine.Vector2 mouseCanvasPos = baseCanvasPos;
            if (_ui != null)
            {
                var canvas = _ui.GetComponent<UnityEngine.Canvas>() ??
                             _ui.GetComponentInParent<UnityEngine.Canvas>();
                if (canvas != null)
                {
                    UnityEngine.RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvas.GetComponent<UnityEngine.RectTransform>(),
                        UnityEngine.Input.mousePosition,
                        canvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                        out mouseCanvasPos);
                }
            }

            if (target == null)
            {
                // Cancel: fly ghost back to base position, then restore card
                TurnManager.BroadcastMessage_Static($"[装备] 取消附着 {equip.UnitName}，装备留在基地");
                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                _ui?.AnimateEquipFlyToBase(mouseCanvasPos, baseCanvasPos, () =>
                {
                    _ui?.RestoreEquipCardInBase(equip);
                    tcs.TrySetResult(true);
                });
                if (_ui != null) await tcs.Task;
                RefreshUI();
                return;
            }

            // Validate game state hasn't changed while popup was open
            if (_gs == null || _gs.GameOver) return;
            if (!_gs.PBase.Contains(equip)) return;
            if (equip.CardData.EquipRuneCost > 0 &&
                _gs.GetSch(GameRules.OWNER_PLAYER, equip.CardData.EquipRuneType) < equip.CardData.EquipRuneCost)
            {
                ShowPlayError("[提示] 资源状态已变更，操作已取消", equip);
                _ui?.RestoreEquipCardInBase(equip);
                return;
            }

            // Pay activation rune cost
            if (equip.CardData.EquipRuneCost > 0)
                _gs.SpendSch(GameRules.OWNER_PLAYER, equip.CardData.EquipRuneType, equip.CardData.EquipRuneCost);

            // Remove from base and attach (game state update first)
            _gs.PBase.Remove(equip);
            target.AttachedEquipment = equip;
            equip.AttachedTo = target;

            int bonus = equip.CardData.EquipAtkBonus;
            if (bonus > 0)
            {
                target.CurrentAtk += bonus;
                target.CurrentHp += bonus;
            }

            TurnManager.BroadcastMessage_Static(
                $"[装备] {equip.UnitName} 附着到 {target.UnitName}（+{bonus}战力）");
            _entryEffects?.OnUnitEntered(equip, GameRules.OWNER_PLAYER, _gs);

            // Fly ghost to target unit, then refresh UI
            var tcs2 = new System.Threading.Tasks.TaskCompletionSource<bool>();
            _ui?.AnimateEquipFlyToUnit(mouseCanvasPos, target, () =>
            {
                UI.GameEventBus.FireUnitFloatText(target, $"附着：{equip.UnitName}", UI.GameColors.BuffColor);
                tcs2.TrySetResult(true);
            });
            if (_ui != null) await tcs2.Task;
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
            FireCardPlayed(spell, GameRules.OWNER_PLAYER);
            _gs.PHand.Remove(spell);

            // Show spell showcase immediately on cast (skip particle VFX delay)
            if (_spellShowcase != null)
                _ = _spellShowcase.ShowAsync(spell, GameRules.OWNER_PLAYER);

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
                // DEV-28: highlight valid targets on board; DEV-29: try/finally ensures highlights are cleared even on exception
                var targetType = spell.CardData.SpellTargetType;
                _ui?.ShowTargetHighlights(u =>
                    !u.CardData.IsEquipment && !u.CardData.IsSpell &&
                    (targetType == SpellTargetType.AnyUnit ||
                    (targetType == SpellTargetType.EnemyUnit   && u.Owner == GameRules.OWNER_ENEMY) ||
                    (targetType == SpellTargetType.FriendlyUnit && u.Owner == GameRules.OWNER_PLAYER)));
                try
                {
                    target = await _spellTargetPopup.ShowAsync(spell.CardData.SpellTargetType, _gs);
                }
                finally
                {
                    _ui?.ClearTargetHighlights();
                }
            }
            else
            {
                // Fallback: no popup wired — use old targeting mode
                _targetingSpell = spell;
                string typeLabel = spell.CardData.SpellTargetType == SpellTargetType.EnemyUnit ? "敌方"
                    : spell.CardData.SpellTargetType == SpellTargetType.FriendlyUnit ? "己方" : "任意";
                string prompt = $"请点击一个{typeLabel}单位作为目标";
                TurnManager.BroadcastMessage_Static($"[法术] {spell.UnitName} — {prompt}（结束回合可取消）");
                UI.GameEventBus.FireHintToast(prompt); // toast so player notices
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

            // DEV-27: enter SpellDuel_OpenLoop so Swift cards become legal (Rule 718)
            TurnStateMachine.TransitionTo(TurnStateMachine.State.SpellDuel_OpenLoop);
            bool negated = AiTryReact(spell);
            // DEV-27: return to player action phase after AI reaction resolves
            TurnStateMachine.TransitionTo(TurnStateMachine.State.Normal_OpenLoop);

            if (negated)
            {
                await Task.Delay(300); // brief pause so player reads the negation log
                TurnManager.BroadcastMessage_Static($"[法术] {spell.UnitName} 被无效化！");
            }
            else if (!_gs.GameOver)
            {
                // Showcase already shown immediately in TryPlaySpellAsync

                if (_spellSys != null)
                    _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);
                else
                    _gs.PDiscard.Add(spell);
                _legendSys?.CheckKaisaEvolution(GameRules.OWNER_PLAYER, _gs);
            }

            // Wait for death animation to complete before RefreshUI rebuilds containers.
            // DeathRoutine = Phase A dissolve ~0.6s + Phase B ghost fly ~0.5s = ~1.1s total.
            // 550ms was too short — caused grey ghost cards when base units died mid-animation.
            await Task.Delay(1200);

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

            // Collect AI's affordable reactive + swift cards.
            // DEV-27: TurnStateMachine is in SpellDuel_OpenLoop here, so both Reactive and Swift
            // are legal responses (Rule 718).
            var reactives = new System.Collections.Generic.List<UnitInstance>();
            foreach (var c in _gs.EHand)
            {
                if (c.CardData.IsSpell &&
                    (c.CardData.HasKeyword(CardKeyword.Reactive) || c.CardData.HasKeyword(CardKeyword.Swift)) &&
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

        /// <summary>
        /// Assigns a legend to <paramref name="owner"/> based on which hero card was extracted.
        /// Kaisa hero → Kaisa legend; Yi hero → Masteryi legend.
        /// Display data is matched by legend ID so both sides work regardless of which deck they use.
        /// </summary>
        private void AssignLegendFromHero(string owner)
        {
            if (_legendSys == null) return;
            UnitInstance hero = _gs.GetHero(owner);
            if (hero == null) return;

            string legendId = LegendIdFromHeroId(hero.CardData.Id);
            if (string.IsNullOrEmpty(legendId)) return;

            LegendInstance legend = _legendSys.CreateLegend(legendId, owner);

            // Match display data by legend ID — works regardless of which side uses which deck
            if (legendId == LegendSystem.KAISA_LEGEND_ID && _kaisaLegendData != null)
                legend.DisplayData = _kaisaLegendData;
            else if (legendId == LegendSystem.YI_LEGEND_ID && _yiLegendData != null)
                legend.DisplayData = _yiLegendData;

            if (owner == GameRules.OWNER_PLAYER) _gs.PLegend = legend;
            else                                  _gs.ELegend = legend;

            Debug.Log($"[InitGame] {owner} legend assigned: {legend.Name} (from hero {hero.CardData.Id})");
        }

        private static string LegendIdFromHeroId(string heroId)
        {
            if (heroId == null) return null;
            if (heroId.StartsWith("kaisa")) return LegendSystem.KAISA_LEGEND_ID;
            if (heroId.StartsWith("yi"))    return LegendSystem.YI_LEGEND_ID;
            return null;
        }

        private void RefreshUI()
        {
            if (_ui == null) return;
            // Force-clear visual selections when it is no longer the player's action phase
            // (prevents highlights getting stuck through turn transitions and AI turns).
            // Do NOT clear selections while a confirmation popup is showing — freeze card state.
            if (!IsPlayerActionPhase() && !AskPromptUI.IsShowing)
            {
                _selectedBaseUnits.Clear();
                _selectedHandUnits.Clear();
            }
            _ui.Refresh(_gs, _selectedBaseUnits, _selectedHandUnits);
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

                // DEV-19: clear lingering banners/toasts + show turn banner at start of each turn
                if (_gs.Phase == GameRules.PHASE_AWAKEN)
                {
                    UI.GameEventBus.FireClearBanners();
                    string who = _gs.Turn == GameRules.OWNER_PLAYER ? "玩家" : "AI";
                    _ui.ShowBanner($"回合 {_gs.Round + 1} · {who}的回合");
                    UI.GameEventBus.FireTurnChanged(_gs.Turn, _gs.Round); // DOT-8: turn banner + mana fill
                }

                // Start/clear turn timer based on phase (DEV-10)
                if (_gs.Phase == GameRules.PHASE_ACTION && _gs.Turn == GameRules.OWNER_PLAYER)
                {
                    _ui.StartTurnTimer(OnTimerExpired);
                    // DEV-19: ribbon reveal on react button when player action starts
                    if (_reactBtn != null)
                        _ui.PlayReactRibbonReveal(_reactBtn);
                }
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
            // CombatResultPanel removed — outcome is visible on battlefield.
            // All post-combat info (deathwish, score, legend passives) shown via
            // EventBanner batch after the 0.5s FireSetBannerDelay.
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

            // H-3: Block re-entry if a player reaction window is already open
            if (_reactionWindowActive)
            {
                TurnManager.BroadcastMessage_Static("[反应] 反应窗口已开启，请先完成当前操作");
                return;
            }

            // Block if AI is currently resolving its own reactive card
            if (_aiReactionWindowActive)
            {
                TurnManager.BroadcastMessage_Static("[反应] AI 正在响应，请等待结算完毕…");
                return;
            }

            // Clear any lingering event banners before opening the reaction window
            UI.GameEventBus.FireClearBanners();

            // Collect affordable reactive + swift spells from player hand (including via rune auto-consume).
            // DEV-27: TurnStateMachine transitions to SpellDuel_OpenLoop here, making Swift legal (Rule 718).
            var reactives = new List<UnitInstance>();
            foreach (var c in _gs.PHand)
            {
                if (c.CardData.IsSpell &&
                    (c.CardData.HasKeyword(CardKeyword.Reactive) || c.CardData.HasKeyword(CardKeyword.Swift)))
                {
                    var affordPlan = RuneAutoConsume.Compute(c, _gs, GameRules.OWNER_PLAYER);
                    if (affordPlan.CanAfford)
                        reactives.Add(c);
                }
            }

            if (reactives.Count == 0)
            {
                TurnManager.BroadcastMessage_Static(
                    $"[反应] 当前没有可打出的反应牌（手牌无反应法术或资源不足，当前法力：{_gs.PMana}）");
                return;
            }

            // ── 反应窗口触发：冻结 AI 后续行动，直到反应牌结算完毕 ────────────
            _reactionWindowActive = true;
            _reactionTcs = new TaskCompletionSource<bool>();
            // DEV-27: enter SpellDuel_OpenLoop so Swift cards are legal (Rule 718)
            TurnStateMachine.TransitionTo(TurnStateMachine.State.SpellDuel_OpenLoop);
            TurnManager.ShowBanner_Static("⚡ 反应窗口触发！");
            TurnManager.BroadcastMessage_Static(
                $"[反应] 反应窗口开启，双方行动暂停（{reactives.Count}张可用，当前法力：{_gs.PMana}）");

            UnitInstance picked = null;
            try
            {
                picked = await _reactiveWindowUI.WaitForReaction(
                    reactives,
                    $"选择反应牌打出（当前法力：{_gs.PMana}）",
                    _gs,
                    onHoverEnter: u =>
                    {
                        var p = RuneAutoConsume.Compute(u, _gs, GameRules.OWNER_PLAYER);
                        if (p.NeedsOps) { _ui?.SetRuneHighlights(p.TapIndices, p.RecycleIndices); _ui?.Refresh(_gs); }
                    },
                    onHoverExit: u => { _ui?.ClearRuneHighlights(); _ui?.Refresh(_gs); });
            }
            catch (System.OperationCanceledException)
            {
                // ReactiveWindowUI was disabled/destroyed mid-flow (e.g. scene reload);
                // treat as skip — cleanup runs below.
                picked = null;
            }

            if (picked != null)
            {
                var reactPlan = RuneAutoConsume.Compute(picked, _gs, GameRules.OWNER_PLAYER);

                // Show confirm if rune consumption needed
                if (reactPlan.NeedsOps)
                {
                    _ui?.SetRuneHighlights(reactPlan.TapIndices, reactPlan.RecycleIndices);
                    _ui?.Refresh(_gs);

                    bool ok = false;
                    try
                    {
                        ok = await (AskPromptUI.Instance?.WaitForConfirm(
                            "反应确认",
                            reactPlan.BuildConfirmText(picked),
                            "确认打出",
                            "取消") ?? System.Threading.Tasks.Task.FromResult(false));
                    }
                    catch (System.OperationCanceledException)
                    {
                        ok = false;
                    }

                    _ui?.ClearRuneHighlights();
                    _ui?.Refresh(_gs);

                    if (!ok)
                    {
                        _reactionWindowActive = false;
                        _reactionTcs?.TrySetResult(true);
                        _reactionTcs = null;
                        return;
                    }

                    ExecuteRunePlan(reactPlan, GameRules.OWNER_PLAYER);
                }

                _gs.PMana -= picked.CardData.Cost;
                TurnManager.BroadcastMessage_Static(
                    $"[反应] 打出 {picked.UnitName}（费用{picked.CardData.Cost}），剩余法力 {_gs.PMana}");
                FireCardPlayed(picked, GameRules.OWNER_PLAYER); // triggers board flash
                if (_spellShowcase != null)
                    await _spellShowcase.ShowAsync(picked, GameRules.OWNER_PLAYER); // card art center reveal
                // ApplyReactive handles hand→discard move internally
                _reactiveSys?.ApplyReactive(picked, GameRules.OWNER_PLAYER, null, _gs);
                RefreshUI();
            }

            // ── 反应结算完毕：解除冻结 ────────────────────────────────────────
            _reactionWindowActive = false;
            _reactionTcs?.TrySetResult(true);
            _reactionTcs = null;
            // DEV-27: return to Normal_OpenLoop (still player action phase)
            TurnStateMachine.TransitionTo(TurnStateMachine.State.Normal_OpenLoop);
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
            if (used)
            {
                RefreshUI();
                // DOT-8: legend skill closeup
                if (_gs.PLegend != null)
                    UI.GameEventBus.FireLegendSkillFired(_gs.PLegend, GameRules.OWNER_PLAYER);
            }
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

            // Equipment cards are in the enemy deck (Yi); also search there for debug purposes
            if (found == null && cardType == "equip")
            {
                foreach (UnitInstance u in _gs.EDeck)
                {
                    if (u.CardData.IsEquipment) { found = u; deck = _gs.EDeck; break; }
                }
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

        private void DebugCycleFloat()
        {
            if (_gs == null) return;
            int idx = _debugFloatIndex % _debugFloatLabels.Length;
            var units = DebugGetBoardUnits();

            switch (idx)
            {
                // ── Unit stat changes + float texts ───────────────────────────
                case 0:
                    foreach (var u in units) { u.TempAtkBonus += 2;  UI.GameEventBus.FireUnitAtkBuff(u,  2); }
                    RefreshUI(); break;
                case 1:
                    foreach (var u in units) { u.TempAtkBonus -= 1;  UI.GameEventBus.FireUnitAtkBuff(u, -1); }
                    RefreshUI(); break;
                case 2:
                    foreach (var u in units) { u.TempAtkBonus += 3;  UI.GameEventBus.FireUnitAtkBuff(u,  3); }
                    RefreshUI(); break;
                case 3:
                    foreach (var u in units) { u.TempAtkBonus -= 2;  UI.GameEventBus.FireUnitAtkBuff(u, -2); }
                    RefreshUI(); break;
                case 4: foreach (var u in units) UI.GameEventBus.FireUnitFloatText(u, "摸1张牌", UnityEngine.Color.cyan); break;
                case 5: foreach (var u in units) UI.GameEventBus.FireUnitFloatText(u, "符能+1", UI.GameColors.SchColor); break;
                case 6: foreach (var u in units) UI.GameEventBus.FireUnitFloatText(u, "击倒", UnityEngine.Color.gray); break;
                // ── Zone float texts ─────────────────────────────────────────
                case 7:  UI.GameEventBus.FireScoreFloat(GameRules.OWNER_PLAYER, 1); break;
                case 8:  UI.GameEventBus.FireRuneTapFloat(GameRules.OWNER_PLAYER); break;
                case 9:  UI.GameEventBus.FireRuneRecycleFloat(GameRules.OWNER_PLAYER); break;
                // ── Event banners ─────────────────────────────────────────────
                case 10: UI.GameEventBus.FireHoldScoreBanner(); break;
                case 11: UI.GameEventBus.FireConquerScoreBanner(); break;
                case 12: UI.GameEventBus.FireBurnoutBanner(GameRules.OWNER_PLAYER); break;
                case 13: UI.GameEventBus.FireLegendSkillBanner("独影剑鸣", "孤独守卫+2战力"); break;
                case 14: UI.GameEventBus.FireLegendEvolvedBanner(); break;
                case 15: UI.GameEventBus.FireTimeWarpBanner(); break;
                case 16: UI.GameEventBus.FireDeathwishBanner("哀哀魄罗", "孤独阵亡—摸1张牌"); break;
            }

            int next = (_debugFloatIndex + 1) % _debugFloatLabels.Length;
            TurnManager.BroadcastMessage_Static($"[DEBUG] 飘字测试 {_debugFloatLabels[idx]}，下一个→ {_debugFloatLabels[next]}");

            _debugFloatIndex++;

            // Update button label to show next type
            if (_debugFloatBtn != null)
            {
                var lbl = _debugFloatBtn.GetComponentInChildren<UnityEngine.UI.Text>();
                if (lbl != null) lbl.text = _debugFloatLabels[next];
            }
        }

        /// <summary>
        /// Applies debug damage to all board/base units of the given owner.
        /// Handles death: fires OnUnitDied, moves to discard, triggers deathwish.
        /// </summary>
        private void DebugApplyDamage(string targetOwner)
        {
            if (_gs == null) return;

            int dmg = 3;
            if (_debugDmgInput != null && !string.IsNullOrEmpty(_debugDmgInput.text))
                int.TryParse(_debugDmgInput.text, out dmg);
            if (dmg <= 0) dmg = 1;

            string label = targetOwner == GameRules.OWNER_PLAYER ? "玩家" : "AI";
            TurnManager.BroadcastMessage_Static($"[DEBUG] 对{label}所有单位造成 {dmg} 点伤害");

            var deathwish = GetComponent<Systems.DeathwishSystem>();

            // ── Base units ───────────────────────────────────────────────────
            var baseList = _gs.GetBase(targetOwner);
            var baseDead = new List<UnitInstance>();
            foreach (var u in new List<UnitInstance>(baseList))
            {
                u.CurrentHp -= dmg;
                FireUnitDamaged(u, dmg, "DEBUG");
                if (u.CurrentHp <= 0) baseDead.Add(u);
            }
            foreach (var u in baseDead)
            {
                FireUnitDied(u);
                baseList.Remove(u);
                _gs.GetDiscard(targetOwner).Add(u);
            }
            deathwish?.OnUnitsDied(baseDead, -1, _gs);

            // ── Battlefield units ────────────────────────────────────────────
            foreach (var bf in _gs.BF)
            {
                var bfList = targetOwner == GameRules.OWNER_PLAYER ? bf.PlayerUnits : bf.EnemyUnits;
                var bfDead = new List<UnitInstance>();
                foreach (var u in new List<UnitInstance>(bfList))
                {
                    u.CurrentHp -= dmg;
                    FireUnitDamaged(u, dmg, "DEBUG");
                    if (u.CurrentHp <= 0) bfDead.Add(u);
                }
                foreach (var u in bfDead)
                {
                    FireUnitDied(u);
                    bfList.Remove(u);
                    _gs.GetDiscard(targetOwner).Add(u);
                }
                deathwish?.OnUnitsDied(bfDead, bf.Id, _gs);
            }

            RefreshUI();
        }

        /// <summary>Returns all units currently on both battlefields + both bases.</summary>
        private System.Collections.Generic.List<Core.UnitInstance> DebugGetBoardUnits()
        {
            var list = new System.Collections.Generic.List<Core.UnitInstance>();
            if (_gs == null) return list;
            list.AddRange(_gs.PBase);
            list.AddRange(_gs.EBase);
            foreach (var bf in _gs.BF)
            {
                list.AddRange(bf.PlayerUnits);
                list.AddRange(bf.EnemyUnits);
            }
            return list;
        }
    }
}
