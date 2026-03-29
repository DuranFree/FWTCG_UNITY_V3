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

        // ── Card data (assign in Inspector) ──────────────────────────────────
        [SerializeField] private CardData[] _kaisaDeck;   // 5 cards
        [SerializeField] private CardData[] _yiDeck;      // 5 cards

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

        // ── DEBUG panel (left-bottom overlay, always visible in dev) ──────────
        [SerializeField] private Button _debugSpellBtn;
        [SerializeField] private Button _debugEquipBtn;
        [SerializeField] private Button _debugUnitBtn;
        [SerializeField] private Button _debugReactiveBtn;
        [SerializeField] private Button _debugManaBtn;

        // ── Game state ────────────────────────────────────────────────────────
        private GameState _gs;

        // ── Interaction state ─────────────────────────────────────────────────
        private UnitInstance _selectedUnit;       // single-select (for BF recall)
        private string _selectedUnitLoc;          // "hand", "base", "0", "1"
        private List<UnitInstance> _selectedBaseUnits = new List<UnitInstance>(); // multi-select for batch move
        private UnitInstance _targetingSpell;     // non-null = awaiting target click for spell

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

            // Wire debug buttons
            if (_debugSpellBtn != null)    _debugSpellBtn.onClick.AddListener(() => DebugDraw("spell"));
            if (_debugEquipBtn != null)    _debugEquipBtn.onClick.AddListener(() => DebugDraw("equip"));
            if (_debugUnitBtn != null)     _debugUnitBtn.onClick.AddListener(() => DebugDraw("unit"));
            if (_debugReactiveBtn != null) _debugReactiveBtn.onClick.AddListener(() => DebugDraw("reactive"));
            if (_debugManaBtn != null)     _debugManaBtn.onClick.AddListener(DebugAddMana);
        }

        private void OnEnable()
        {
            TurnManager.OnMessage += HandleMessage;
            TurnManager.OnPhaseChanged += HandlePhaseChanged;
            CombatSystem.OnCombatLog += HandleMessage;
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
                    onRune: OnRuneClicked
                );
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
                            _spellSys, _reactiveSys, _reactiveWindowUI, _legendSys);

            // Initialize legends (player = Kaisa/虚空, enemy = Masteryi/伊欧尼亚)
            if (_legendSys != null)
            {
                _gs.PLegend = _legendSys.CreateLegend(LegendSystem.KAISA_LEGEND_ID, GameRules.OWNER_PLAYER);
                _gs.ELegend = _legendSys.CreateLegend(LegendSystem.YI_LEGEND_ID, GameRules.OWNER_ENEMY);
            }

            // Random first player
            _gs.First = Random.value > 0.5f ? GameRules.OWNER_PLAYER : GameRules.OWNER_ENEMY;
            _gs.Turn = _gs.First;
            _gs.Round = 0;
            _gs.Phase = GameRules.PHASE_AWAKEN;

            // Build and shuffle decks
            BuildDeck(GameRules.OWNER_PLAYER, _kaisaDeck);
            BuildDeck(GameRules.OWNER_ENEMY, _yiDeck);

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

                // Valid target — cast spell
                UnitInstance spell = _targetingSpell;
                _targetingSpell = null;
                if (_spellSys != null)
                    _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, unit, _gs);
                else
                {
                    _gs.GetDiscard(GameRules.OWNER_PLAYER).Add(spell);
                }
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
        public void OnBattlefieldClicked(int bfId)
        {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;

            // Ignore BF click while in spell targeting mode
            if (_targetingSpell != null)
            {
                TurnManager.BroadcastMessage_Static("[提示] 请选择目标单位，或结束回合以取消法术");
                return;
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
                RefreshUI();
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
                RefreshUI();
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
                // Recycle: remove from active runes, place on top of rune deck, gain +1 sch
                runes.RemoveAt(runeIdx);
                _gs.PRuneDeck.Insert(0, rune);
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

        // ── Private helpers ───────────────────────────────────────────────────

        private void TryPlayCard(UnitInstance unit)
        {
            if (unit.CardData.IsSpell)
                TryPlaySpell(unit);
            else
                TryPlayUnit(unit);
        }

        private void TryPlayUnit(UnitInstance unit)
        {
            if (unit.CardData.Cost > _gs.PMana)
            {
                TurnManager.BroadcastMessage_Static(
                    $"[提示] 法力不足：需要 {unit.CardData.Cost}，当前 {_gs.PMana}");
                _selectedUnit = null;
                return;
            }

            _gs.PHand.Remove(unit);
            _gs.PBase.Add(unit);
            _gs.PMana -= unit.CardData.Cost;
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

        private void TryPlaySpell(UnitInstance spell)
        {
            if (spell.CardData.Cost > _gs.PMana)
            {
                TurnManager.BroadcastMessage_Static(
                    $"[提示] 法力不足：需要 {spell.CardData.Cost}，当前 {_gs.PMana}");
                return;
            }

            _gs.PMana -= spell.CardData.Cost;
            _gs.CardsPlayedThisTurn++;

            if (spell.CardData.SpellTargetType == SpellTargetType.None)
            {
                // No target needed — cast immediately
                if (_spellSys != null)
                    _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);
                else
                {
                    _gs.PHand.Remove(spell);
                    _gs.PDiscard.Add(spell);
                }
                RefreshUI();
            }
            else
            {
                // Needs target — remove from hand, enter targeting mode
                _gs.PHand.Remove(spell);
                _targetingSpell = spell;
                string typeLabel = spell.CardData.SpellTargetType == SpellTargetType.EnemyUnit ? "敌方"
                    : spell.CardData.SpellTargetType == SpellTargetType.FriendlyUnit ? "己方" : "任意";
                TurnManager.BroadcastMessage_Static(
                    $"[法术] {spell.UnitName} — 请点击一个{typeLabel}单位作为目标（结束回合可取消）");
                RefreshUI();
            }
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

            for (int i = 0; i < GameRules.INITIAL_HAND_SIZE; i++)
            {
                if (deck.Count == 0) break;
                hand.Add(deck[0]);
                deck.RemoveAt(0);
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
            }
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
    }
}
