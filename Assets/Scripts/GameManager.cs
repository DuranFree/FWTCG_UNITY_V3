using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
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
        // DEV-32 A4: 接口引用 — 生产等于 _reactiveWindowUI，测试可注入 mock
        private IReactionWindow _reactionWindow;
        private IReactionWindow ReactionWindow => _reactionWindow ?? _reactiveWindowUI;
        /// <summary>DEV-32 A4 test hook: 注入 mock IReactionWindow 以绕过 MonoBehaviour 实例化。</summary>
        public void InjectReactionWindow(IReactionWindow window) { _reactionWindow = window; }
        [SerializeField] private LegendSystem _legendSys;
        [SerializeField] private BattlefieldSystem _bfSys;
        [SerializeField] private CardDetailPopup _cardDetailPopup;
        [SerializeField] private SpellShowcaseUI _spellShowcase;  // DEV-16
        [SerializeField] private SpellDuelUI  _spellDuelUI;    // DEV-30 F2

        // ── React button / Legend skill button ────────────────────────────────
        [SerializeField] private Button _reactBtn;
        [SerializeField] private Button _legendSkillBtn;

        // 迅捷/反应 按钮可用性 + 无牌时 toast 节流（避免疯狂点击刷屏）
        private float _lastNoReactiveToastTime = -10f;
        private const float NO_REACTIVE_TOAST_COOLDOWN = 2f;

        // ── Reaction window freeze (static so SimpleAI can await without a ref) ─
        // DEV-32 A5 分层说明（三层状态并存 — 各司其职，不合并）：
        //   1. TurnManager._gs.Phase  : 宏观回合流程（awaken/start/summon/draw/action/end）
        //   2. TurnStateMachine       : 法术合法性状态机（Rule 18/25/718，4 态）
        //   3. 本文件下方标志         : UI 互斥锁（防并发窗口、对决中屏蔽输入），**不是** state machine
        // 不变量：_reactionWindowActive=true → TurnStateMachine.IsSpellDuelOpen=true（由打开方保证）
        //         同理 _aiReactionWindowActive=true → IsSpellDuelOpen=true

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
        // UI-OVERHAUL-1b 后废弃：Haste 决策合入资源准备机制，不再走 drag-flow prompt
        // private bool? _pendingDragHasteDecision;

        // ── UI-OVERHAUL-1b: 符文手动标记池（左键=待横置 / 右键=待回收，互斥） ─────
        private readonly HashSet<int> _preparedTapIdxs     = new HashSet<int>();
        private readonly HashSet<int> _preparedRecycleIdxs = new HashSet<int>();

        /// <summary>外部查询：战场是否有我方单位（用于确定按钮亮/暗）。</summary>
        public bool HasAnyPlayerUnitOnBattlefield()
        {
            if (_gs == null) return false;
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                if (_gs.BF[i].PlayerUnits.Count > 0) return true;
            return false;
        }

        /// <summary>外部查询：是否有待提交的符文操作（用于确定按钮亮/暗）。</summary>
        public bool HasPreparedRunes()
            => _preparedTapIdxs.Count > 0 || _preparedRecycleIdxs.Count > 0;

        /// <summary>外部（UI）只读访问当前待横置符文下标。</summary>
        public IReadOnlyCollection<int> GetPreparedTapIdxs()     => _preparedTapIdxs;
        /// <summary>外部（UI）只读访问当前待回收符文下标。</summary>
        public IReadOnlyCollection<int> GetPreparedRecycleIdxs() => _preparedRecycleIdxs;

        /// <summary>清空所有符文准备标记（在回合切换或出牌完成后调用）。</summary>
        public void ClearPreparedRunes()
        {
            _preparedTapIdxs.Clear();
            _preparedRecycleIdxs.Clear();
        }

        /// <summary>统计已准备回收的符文中属于指定类型的数量。</summary>
        private int CountPreparedRecyclesOfType(FWTCG.Data.RuneType type)
        {
            if (_gs == null) return 0;
            int n = 0;
            foreach (var idx in _preparedRecycleIdxs)
                if (idx >= 0 && idx < _gs.PRunes.Count && _gs.PRunes[idx].RuneType == type) n++;
            return n;
        }

        /// <summary>
        /// 真正执行所有准备标记：tap 的 rune.Tapped=true + _gs.PMana+=N；
        /// recycle 的从 _gs.PRunes 移除 + 放入 PRuneDeck 底部 + AddSch。
        /// </summary>
        private void CommitPreparedRunes()
        {
            if (_gs == null) { ClearPreparedRunes(); return; }

            // Tap：小到大，不影响索引
            var tapSorted = new List<int>(_preparedTapIdxs);
            tapSorted.Sort();
            foreach (int idx in tapSorted)
            {
                if (idx < 0 || idx >= _gs.PRunes.Count) continue;
                var r = _gs.PRunes[idx];
                if (r == null || r.Tapped) continue;
                r.Tapped = true;
                _gs.PMana += 1;
            }

            // Recycle：大到小 remove，避免索引偏移
            var recSorted = new List<int>(_preparedRecycleIdxs);
            recSorted.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in recSorted)
            {
                if (idx < 0 || idx >= _gs.PRunes.Count) continue;
                var r = _gs.PRunes[idx];
                if (r == null || r.Tapped) continue;
                _gs.PRunes.RemoveAt(idx);
                _gs.PRuneDeck.Add(r);
                _gs.AddSch(GameRules.OWNER_PLAYER, r.RuneType, 1);
            }

            ClearPreparedRunes();
        }

        /// <summary>
        /// UI-OVERHAUL-1b/1c-β: 校验玩家当前 prepared 标记 + 现有资源能否支付 <paramref name="card"/>。
        /// 不够 → 飘屏列出缺口 + 清空标记 + 返回 false；
        /// 足够 → Commit prepared（真正 tap/recycle）+ 扣 mana/sch + Haste 自动判定 + 返回 true。
        ///
        /// Haste 自动判定：若单位有 Haste 且 prepared 资源"多出" +1 法力 + +1 主符能 → 激活 Haste，多扣 +1 +1，
        /// 单位以活跃状态进场（Exhausted=false）。
        /// </summary>
        private bool ValidateAndCommitPreparedFor(UnitInstance card)
        {
            _lastCommitUsedHaste = false; // 结果会被 TryPlayUnitAsync / TryPlayHeroAsync 读取
            if (_gs == null || card == null) return false;
            int cost           = ComputeLegionAdjustedCost(card, _gs);
            int manaAvailable  = _gs.PMana + _preparedTapIdxs.Count;

            // 基础 primary/secondary 需求
            int primaryNeed   = card.CardData.RuneCost;
            int secondaryNeed = card.CardData.HasSecondaryRune ? card.CardData.SecondaryRuneCost : 0;

            int primaryHave = _gs.GetSch(GameRules.OWNER_PLAYER, card.CardData.RuneType)
                              + CountPreparedRecyclesOfType(card.CardData.RuneType);
            int secondaryHave = 0;
            if (card.CardData.HasSecondaryRune)
                secondaryHave = _gs.GetSch(GameRules.OWNER_PLAYER, card.CardData.SecondaryRuneType)
                              + CountPreparedRecyclesOfType(card.CardData.SecondaryRuneType);

            int manaShort      = cost         - manaAvailable;
            int primaryShort   = primaryNeed  - primaryHave;
            int secondaryShort = secondaryNeed - secondaryHave;

            if (manaShort > 0 || primaryShort > 0 || secondaryShort > 0)
            {
                var lines = new List<FloatingTipUI.Line>();
                if (manaShort > 0)     lines.Add(FloatingTipUI.ManaShortLine(manaShort));
                if (primaryShort > 0)  lines.Add(FloatingTipUI.RuneShortLine(card.CardData.RuneType, primaryShort));
                if (secondaryShort > 0) lines.Add(FloatingTipUI.RuneShortLine(card.CardData.SecondaryRuneType, secondaryShort));
                var canvas = _ui != null ? _ui.RootCanvasRef : null;
                if (canvas != null) FloatingTipUI.Show(canvas, lines);

                UI.GameEventBus.FireCardPlayFailed(card);
                TurnManager.BroadcastMessage_Static(
                    $"[提示] {card.UnitName} 资源未准备到位（法力缺{Mathf.Max(0, manaShort)}，主符能缺{Mathf.Max(0, primaryShort)}，副符能缺{Mathf.Max(0, secondaryShort)}）");

                ClearPreparedRunes();
                RefreshUI();
                return false;
            }

            // Haste 自动判定（仅有 Haste 关键词时）：是否多出 +1 法力 + +1 主符能？
            bool useHaste = false;
            if (card.CardData.HasKeyword(CardKeyword.Haste)
                && manaAvailable >= cost + 1
                && primaryHave   >= primaryNeed + 1)
            {
                useHaste = true;
            }

            // Commit prepared runes（真正 tap/recycle）
            CommitPreparedRunes();

            // 扣 mana + 扣 sch（包括 Haste 额外 +1 +1）
            int manaTotal   = cost + (useHaste ? 1 : 0);
            int primaryTotal = primaryNeed + (useHaste ? 1 : 0);
            _gs.PMana -= manaTotal;
            if (primaryTotal > 0)
                _gs.SpendSch(GameRules.OWNER_PLAYER, card.CardData.RuneType, primaryTotal);
            if (card.CardData.HasSecondaryRune && secondaryNeed > 0)
                _gs.SpendSch(GameRules.OWNER_PLAYER, card.CardData.SecondaryRuneType, secondaryNeed);

            _lastCommitUsedHaste = useHaste;

            if (useHaste)
                TurnManager.BroadcastMessage_Static(
                    $"[急速] {card.UnitName} 自动激活（额外1法力+1{card.CardData.RuneType.ToChinese()}）");

            return true;
        }

        private bool _lastCommitUsedHaste;

        // ── DEV-22: Drag query helpers (used by CardDragHandler) ─────────────

        /// <summary>Exposes game state for Bot and tooling.</summary>
        public GameState GetState() => _gs;

        /// <summary>
        /// Bot (Strategic mode) entry point — runs SimpleAI for the player side.
        /// SimpleAI calls TurnManager.EndTurn() internally, which unblocks
        /// TurnManager.DoAction waiting on _actionComplete.
        /// </summary>
        public Task RunStrategicPlayerTurn()
        {
            if (_ai == null || _turnMgr == null || _gs == null)
                return Task.CompletedTask;
            return _ai.TakeAction(GameRules.OWNER_PLAYER, _gs, _turnMgr,
                                  _combatSys, _scoreMgr, _entryEffects,
                                  _spellSys, _reactiveSys, _reactiveWindowUI,
                                  _legendSys, _bfSys);
        }

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
        /// Returns the current selection list for the player base.
        /// **UI-OVERHAUL-1a 不变量**：单选化后恒 ≤ 1 项（Count 0 或 1）。
        /// 签名保留 List 形态只因 GameUI.Refresh / CardDragHandler 签名未一起重构；
        /// DEV-32 架构阶段可迁移到单元素字段（见 tech-debt UI-OVERHAUL-1a）。
        /// </summary>
        public List<UnitInstance> GetSelectedBaseUnits() => _selectedBaseUnits;

        /// <summary>
        /// Returns the current selection list for the player hand.
        /// **UI-OVERHAUL-1a 不变量**：单选化后恒 ≤ 1 项（见 GetSelectedBaseUnits 注释）。
        /// </summary>
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

        // DEV-31 cleanup: OnDragHandGroupToBase / OnSpellGroupDraggedOut / PlaySpellGroupAsync 已移除
        // UI-OVERHAUL-1a 单选化后无调用；UIOverhaul1aTests 会断言这些名字不存在

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

        // UI-OVERHAUL-1b 已废弃：DragNeedsHasteChoice / SetDragHasteDecision 由资源准备机制替代，
        // Haste 决策改为"多准备 +1 法力 + +1 符能 → 自动激活"。已确认 CardDragHandler 不再调用。

        /// <summary>
        /// UI-OVERHAUL-1a: 单选化 — 只拖拽单张基地单位到战场。
        /// </summary>
        public void OnDragUnitToBF(UnitInstance unit, int bfId)
        {
            if (!IsPlayerActionPhase()) return;
            if (unit == null || _gs == null || !_gs.PBase.Contains(unit) || unit.Exhausted) return;
            _selectedBaseUnits.Clear();
            _selectedBaseUnits.Add(unit);
            _selectedUnit     = null;
            _selectedUnitLoc  = null;
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
            // Hotfix-12: 确保 EntryEffectVFX 单例存在（自动挂在 GameManager 上）
            if (FWTCG.UI.EntryEffectVFX.Instance == null)
                gameObject.AddComponent<FWTCG.UI.EntryEffectVFX>();

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
                // B8 full: 待命区点击 → 尝试翻开己方面朝下牌
                _ui.SetStandbyClickCallback(bfId => _ = FlipAndPlayStandbyAsync(bfId, GameRules.OWNER_PLAYER));
                // DEV-22: wire drag callbacks and push zone RTs to CardDragHandler
                _ui.SetDragCallbacks(
                    onDragCardToBase: OnDragCardToBase,
                    onSpellDragOut:   OnSpellDraggedOut,
                    onDragHeroToBase: OnDragHeroToBase,
                    onDragUnitToBF:   OnDragUnitToBF
                );
                _ui.SetupDragZones();
            }

            InitGame();
            StartCoroutine(RunWithStartup());
        }

        // ── Bot: 原地重置（不重载场景）────────────────────────────────────────

        /// <summary>
        /// Bot 专用：不重载场景，直接重置游戏状态并重新跑完整启动流程。
        /// 保留所有游戏逻辑、动画、UI；仅清空当前局数据。
        /// </summary>
        public void RestartGameInPlace()
        {
            // 1. 让当前所有游戏逻辑感知到游戏已结束，让异步任务自然退出
            if (_gs != null) _gs.GameOver = true;

            // 2. 停止所有当前协程（GameLoop、RunWithStartup 等）
            StopAllCoroutines();

            // 2b. KillAll 前先清理残留 dissolve material —— 否则 coroutine 被中断时
            //     Image 仍持有 dissolve material + 可能空 sprite → 渲染 magenta
            CleanupDissolveMaterials();

            // 3. 停掉所有 DOTween（清除残留动画）
            DOTween.KillAll();

            // 4. 重置交互状态
            _selectedUnit               = null;
            _selectedUnitLoc            = null;
            _selectedBaseUnits.Clear();
            _selectedHandUnits.Clear();
            _targetingSpell             = null;
            _aiReactionPending          = false;
            _bfClickInFlight            = false;

            // 5. 重置静态反应窗口状态
            _reactionWindowActive   = false;
            _aiReactionWindowActive = false;
            _reactionTcs?.TrySetCanceled();
            _reactionTcs  = null;
            _aiReactionTcs?.TrySetCanceled();
            _aiReactionTcs = null;

            // 6. 关闭残留 UI（GameOver / 两个 Showcase / SpellDuel）——
            //    DOTween.KillAll 后 OnComplete 不会 fire，必须主动清理否则面板卡屏
            UI.GameUI.Instance?.HideGameOver();
            UI.LegendSkillShowcase.Instance?.ForceHide();
            UI.SpellShowcaseUI.Instance?.ForceHide();
            UI.SpellDuelUI.Instance?.HideDuelOverlay();

            // 7. 重新初始化并启动完整流程（含硬币+换牌）
            InitGame();
            StartCoroutine(RunWithStartup());
        }

        /// <summary>
        /// 扫描所有 UI Image 和 RawImage，清掉名字带 "Dissolve" 的自定义材质。
        /// 紫色 magenta 的主要来源：KillAll 打断 dissolve coroutine 后 Image 仍持有
        /// dissolve material + sprite 可能被销毁 → shader 采样空纹理 → 渲染紫色。
        /// </summary>
        private static void CleanupDissolveMaterials()
        {
            foreach (var img in FindObjectsOfType<UnityEngine.UI.Image>(true))
            {
                if (img == null) continue;
                var mat = img.material;
                if (mat != null && mat != img.defaultMaterial &&
                    (mat.shader == null || mat.shader.name.IndexOf("Dissolve", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    img.material = null;
                }
            }
            foreach (var ri in FindObjectsOfType<UnityEngine.UI.RawImage>(true))
            {
                if (ri == null) continue;
                var mat = ri.material;
                if (mat != null && mat != ri.defaultMaterial &&
                    (mat.shader == null || mat.shader.name.IndexOf("Dissolve", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    ri.material = null;
                }
            }
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
            _entryEffects?.Inject(_gs); // darius OnCardPlayed 监听器需要 GameState 引用
            // DEV-32 A6: DeathwishSystem 事件总线化后也需 gs 引用
            GetComponent<Systems.DeathwishSystem>()?.Inject(_gs);

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

            // Don't RefreshUI here — defer until after coin flip + mulligan
            // so card enter animations play AFTER startup overlays close.
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
            }
            // Refresh AFTER startup flow — card enter animations now visible
            RefreshUI();

            // Wait for card enter animations + foil sweep to finish before starting game loop.
            // This ensures the board is fully stable before turn banners / phase messages appear.
            yield return new WaitForSeconds(1.0f);

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

                // Valid target — play showcase, then give AI a reaction window before resolving (DEV-15)
                UnitInstance spell = _targetingSpell;
                _targetingSpell = null;
                if (_spellShowcase != null)
                    _ = _spellShowcase.ShowAsync(spell, GameRules.OWNER_PLAYER);
                _ = CastPlayerSpellWithReactionAsync(spell, unit);
                RefreshUI();
                return;
            }

            // ── Hand card: UI-OVERHAUL-1a 单选化 — 点击 A 清 B 再选 A，再次点 A 取消 ──
            if (_gs.PHand.Contains(unit))
            {
                bool alreadySelected = _selectedHandUnits.Contains(unit);
                _selectedHandUnits.Clear();
                Debug.Log($"[OnUnitClicked] Hand click {unit.UnitName}, alreadySelected={alreadySelected}");

                // Hotfix-5: 强制清除所有手牌 CardView 视觉选中态（防御 Refresh 路径漏掉的卡）
                if (_ui != null) _ui.ForceClearAllHandSelections();

                if (alreadySelected)
                {
                    TurnManager.BroadcastMessage_Static($"[取消选择] {unit.UnitName}");
                }
                else
                {
                    _selectedHandUnits.Add(unit);
                    TurnManager.BroadcastMessage_Static($"[选择] {unit.UnitName} — 拖拽出牌");
                }
                RefreshUI();
                return;
            }

            // ── Base unit: UI-OVERHAUL-1a 单选化 ──
            if (_gs.PBase.Contains(unit))
            {
                _selectedUnit = null;
                _selectedUnitLoc = null;

                bool alreadySelected = _selectedBaseUnits.Contains(unit);
                _selectedBaseUnits.Clear();
                if (alreadySelected)
                {
                    TurnManager.BroadcastMessage_Static($"[取消选择] {unit.UnitName}");
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
                        TurnManager.BroadcastMessage_Static($"[选择] {unit.UnitName} — 点击战场派遣");
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

            // ── Batch move from base ── UI-OVERHAUL-1c-β: 延迟 combat；combat 改由"确定"按钮触发
            if (_selectedBaseUnits.Count > 0)
            {
                List<UnitInstance> toMove = new List<UnitInstance>(_selectedBaseUnits);
                _selectedBaseUnits.Clear();

                foreach (UnitInstance u in toMove)
                {
                    if (!u.Exhausted && _gs.PBase.Contains(u))
                    {
                        if (u.CardData.IsEquipment)
                            await ActivateEquipmentAsync(u);
                        else
                        {
                            _combatSys.MoveUnit(u, "base", bfId, GameRules.OWNER_PLAYER, _gs);
                        }
                    }
                }

                _selectedUnit = null;
                _selectedUnitLoc = null;
                RefreshUI();
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

                // UI-OVERHAUL-1c-β: Roam 移动也延迟 combat；不计入回滚栈（Roam 非资源入场）
                _combatSys.MoveUnit(_selectedUnit, _selectedUnitLoc, bfId, GameRules.OWNER_PLAYER, _gs);
                _selectedUnit = null;
                _selectedUnitLoc = null;
                RefreshUI();
                await System.Threading.Tasks.Task.CompletedTask;
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
        /// UI-OVERHAUL-1c-β: 结束回合前自动 flush 待结算战斗（等价于隐式点击"确定"）。
        /// </summary>
        public async void OnEndTurnClicked()
        {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;
            if (_bfClickInFlight) return;

            // Cancel any pending spell targeting — refund mana and return spell to hand
            if (_targetingSpell != null)
            {
                _gs.PHand.Add(_targetingSpell);
                _gs.PMana += _targetingSpell.CardData.Cost;
                _gs.CardsPlayedThisTurn--;
                TurnManager.BroadcastMessage_Static($"[法术] 取消 {_targetingSpell.UnitName} 的发动，法力退还");
                _targetingSpell = null;
            }

            // 自动 flush 未结算战斗
            if (HasAnyPlayerUnitOnBattlefield())
            {
                _bfClickInFlight = true;
                try { await TriggerPendingCombatsAsync(); }
                finally { _bfClickInFlight = false; }
                if (_gs == null || _gs.GameOver) return;
            }

            _selectedUnit = null;
            _selectedUnitLoc = null;
            _selectedBaseUnits.Clear();
            _selectedHandUnits.Clear();
            ClearPreparedRunes();
            _turnMgr.EndTurn();
            RefreshUI();
        }

        // ── UI-OVERHAUL-1c-β: 确定按钮实现 ────────────────────────────────────

        /// <summary>
        /// 1c-β: 全局"确定"按钮 —
        ///   - 有 prepared 符文 → 先独立 commit（tap/recycle 真正执行，获得 mana/sch）
        ///   - 有战场己方单位 → 再遍历所有战场触发 combat / spell duel
        ///   - 两者皆无 → 提示忽略
        /// </summary>
        public async void OnConfirmClicked()
        {
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;
            if (_bfClickInFlight) return;

            bool hadPreparedRunes = HasPreparedRunes();
            bool hasBfUnits       = HasAnyPlayerUnitOnBattlefield();

            if (!hadPreparedRunes && !hasBfUnits)
            {
                TurnManager.BroadcastMessage_Static("[提示] 无可确定的操作");
                return;
            }

            if (hadPreparedRunes)
            {
                CommitPreparedRunes();
                TurnManager.BroadcastMessage_Static("[符文] 已提交准备的横置/回收操作");
            }

            if (!hasBfUnits)
            {
                RefreshUI();
                return;
            }

            _bfClickInFlight = true;
            try
            {
                await TriggerPendingCombatsAsync();
            }
            finally
            {
                _bfClickInFlight = false;
                if (!_gs.GameOver) RefreshUI();
            }
        }

        /// <summary>
        /// 对所有有我方单位的战场触发 combat / spell duel。供 OnConfirmClicked 和 OnEndTurnClicked 共用。
        /// </summary>
        private async System.Threading.Tasks.Task TriggerPendingCombatsAsync()
        {
            bool anyCombat = false;
            for (int bf = 0; bf < GameRules.BATTLEFIELD_COUNT; bf++)
            {
                if (_gs.BF[bf].PlayerUnits.Count == 0) continue;
                if (_gs.BF[bf].EnemyUnits.Count > 0)
                {
                    await FWTCG.Core.GameTiming.Delay(500);
                    UI.GameEventBus.FireDuelBanner();
                    await FWTCG.Core.GameTiming.Delay(2000);
                    UI.GameEventBus.FireSetBannerDelay(0.5f);
                    anyCombat = true;
                }
                _combatSys.CheckAndResolveCombat(bf, GameRules.OWNER_PLAYER, _gs, _scoreMgr);
                if (_gs.GameOver) return;
            }
            if (anyCombat)
            {
                await FWTCG.Core.GameTiming.Delay(550); // hit flash + death anim 余韵
            }
        }

        /// <summary>
        /// UI-OVERHAUL-1b: 符文点击改为"标记"（非立即执行）。
        ///   - 左键 (recycle=false) → toggle 到 "待横置" 集合（绿色呼吸灯）
        ///   - 右键 (recycle=true)  → toggle 到 "待回收" 集合（红色呼吸灯）
        ///   - 同一符文两种标记互斥：标记 A 时清除 B
        /// 真正的横置/回收在出牌成功时由 <see cref="CommitPreparedRunes"/> 执行。
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
            if (rune == null) return;

            // 已横置的符文不可再标记（也不能回收）
            if (rune.Tapped)
            {
                TurnManager.BroadcastMessage_Static("[提示] 该符文已横置，无法再操作");
                return;
            }

            if (recycle)
            {
                _preparedTapIdxs.Remove(runeIdx); // 互斥
                if (_preparedRecycleIdxs.Contains(runeIdx))
                {
                    _preparedRecycleIdxs.Remove(runeIdx);
                    TurnManager.BroadcastMessage_Static($"[取消标记] {rune.RuneType.ToChinese()} 符文不再准备回收");
                }
                else
                {
                    _preparedRecycleIdxs.Add(runeIdx);
                    TurnManager.BroadcastMessage_Static(
                        $"[标记回收] {rune.RuneType.ToChinese()} 符文（出牌时生效）");
                }
            }
            else
            {
                _preparedRecycleIdxs.Remove(runeIdx); // 互斥
                if (_preparedTapIdxs.Contains(runeIdx))
                {
                    _preparedTapIdxs.Remove(runeIdx);
                    TurnManager.BroadcastMessage_Static($"[取消标记] {rune.RuneType.ToChinese()} 符文不再准备横置");
                }
                else
                {
                    _preparedTapIdxs.Add(runeIdx);
                    TurnManager.BroadcastMessage_Static(
                        $"[标记横置] {rune.RuneType.ToChinese()} 符文（出牌时生效）");
                }
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
        /// UI-OVERHAUL-1b: "全部横置"按钮改为"全部标记为待横置"。
        /// 不再立即 tap，只是加到 _preparedTapIdxs（出牌时统一 commit）。
        /// </summary>
        public void OnTapAllRunesClicked()
        {
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;

            int marked = 0;
            for (int i = 0; i < _gs.PRunes.Count; i++)
            {
                var r = _gs.PRunes[i];
                if (r == null || r.Tapped) continue;
                if (_preparedTapIdxs.Add(i))
                {
                    _preparedRecycleIdxs.Remove(i); // 互斥
                    marked++;
                }
            }
            if (marked > 0)
                TurnManager.BroadcastMessage_Static($"[全部标记横置] 标记 {marked} 个符文（出牌时生效）");
            else
                TurnManager.BroadcastMessage_Static("[提示] 没有可标记的未横置符文");

            RefreshUI();
        }

        /// <summary>
        /// Skip the current reaction window, delegating to ReactiveWindowUI.
        /// </summary>
        public void OnSkipReactionClicked()
        {
            ReactionWindow?.SkipReaction();
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

        // ── UI-OVERHAUL-1b: Hover 自动高亮已禁用（玩家手动标记符文，auto plan 会误导）──

        private void OnHeroHoverEnter(UnitInstance unit) { /* 手动模式下不做任何 auto highlight */ }
        private void OnHeroHoverExit(UnitInstance unit)  { /* 同上 */ }
        private void OnCardHoverEnter(UnitInstance unit) { /* 同上 */ }
        private void OnCardHoverExit(UnitInstance unit)  { /* 同上 */ }

        // ── UI-OVERHAUL-1b: Hero / Hand 出牌入口 ─────────────────────────────
        //
        // 流程：先校验"玩家已准备的 tap/recycle" + 现有 mana/sch 是否满足 cost；
        //   - 不满足 → FloatingTipUI 飘屏列出缺口 + 弹回 + 清空准备标记
        //   - 满足   → CommitPreparedRunes() 真正 tap/recycle + 走原出牌流程

        private async System.Threading.Tasks.Task PlayHeroWithRuneConfirmAsync(UnitInstance hero)
        {
            if (_gs == null || _gs.GameOver || hero == null) return;
            if (!ValidateAndCommitPreparedFor(hero)) return;
            await TryPlayHeroAsync(hero);
        }

        // ── UI-OVERHAUL-1b: Hand 出牌入口（已去 AskPromptUI 弹窗 + RuneAutoConsume.Compute）

        private System.Threading.Tasks.Task PlayHandCardWithRuneConfirmAsync(UnitInstance unit)
        {
            if (_gs == null || _gs.GameOver || unit == null) return System.Threading.Tasks.Task.CompletedTask;
            if (!_gs.PHand.Contains(unit)) return System.Threading.Tasks.Task.CompletedTask;

            if (!ValidateAndCommitPreparedFor(unit)) return System.Threading.Tasks.Task.CompletedTask;

            // Re-validate once more before the final play
            if (_gs == null || _gs.GameOver) return System.Threading.Tasks.Task.CompletedTask;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return System.Threading.Tasks.Task.CompletedTask;
            if (!_gs.PHand.Contains(unit)) return System.Threading.Tasks.Task.CompletedTask;

            _selectedHandUnits.Remove(unit);
            TryPlayCard(unit);
            return System.Threading.Tasks.Task.CompletedTask;
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

        /// <summary>
        /// B8 full: 面朝下打出带【隐匿/待命】关键词的牌（Rule 23）。
        /// 流程：支付 1 任意符能 → 选己方控制且待命区未被占的战场 → 面朝下放该战场待命区。
        /// 放下这一回合不能翻开；下一位玩家回合开始后激活。
        /// </summary>
        public async Task TryPlayAsStandbyAsync(UnitInstance card)
        {
            if (_aiReactionWindowActive) { FireHintToast("AI 正在使用反应牌"); return; }
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION)
            {
                FireHintToast("只能在自己行动阶段面朝下放置");
                return;
            }
            if (!card.CardData.HasKeyword(CardKeyword.Standby))
            {
                FireHintToast("该牌没有【隐匿】关键词");
                return;
            }
            if (!_gs.PHand.Contains(card)) return;

            // Rule 23.1.b.1：需要 1 任意符能（符文池任意颜色都可）
            int totalSch = _gs.GetSch(GameRules.OWNER_PLAYER);
            if (totalSch < 1)
            {
                FireHintToast("需要至少 1 点任意符能才能面朝下放置");
                return;
            }

            // 找到所有己方控制且 PlayerStandby 为空的战场
            var legalBFs = new System.Collections.Generic.List<int>();
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                if (_gs.BF[i].Ctrl == GameRules.OWNER_PLAYER && _gs.BF[i].PlayerStandby == null)
                    legalBFs.Add(i);
            }
            if (legalBFs.Count == 0)
            {
                FireHintToast("没有合法战场（需己方控制且待命区未被占用）");
                return;
            }

            // 让玩家选战场（UI 简化：只有一个合法就直接用；多个则弹 AskPrompt 选择）
            int chosenBF = legalBFs[0];
            if (legalBFs.Count > 1 && UI.AskPromptUI.Instance != null)
            {
                try
                {
                    int pick = await UI.AskPromptUI.Instance.WaitForConfirm(
                        "面朝下放置",
                        $"选择战场（1 任意符能）",
                        $"战场 {legalBFs[0] + 1}",
                        $"战场 {legalBFs[1] + 1}") ? 0 : 1;
                    chosenBF = legalBFs[pick];
                }
                catch { return; }
                // 重验
                if (_gs.BF[chosenBF].Ctrl != GameRules.OWNER_PLAYER ||
                    _gs.BF[chosenBF].PlayerStandby != null ||
                    !_gs.PHand.Contains(card))
                {
                    ShowPlayError("[面朝下] 状态已变更，操作取消", card);
                    return;
                }
            }

            // 扣 1 任意符能（选颜色优先非主域符能，减少"卡死自己"）
            Data.RuneType spentType = Data.RuneType.Blazing;
            foreach (Data.RuneType rt in System.Enum.GetValues(typeof(Data.RuneType)))
            {
                if (_gs.GetSch(GameRules.OWNER_PLAYER, rt) > 0)
                {
                    _gs.SpendSch(GameRules.OWNER_PLAYER, rt, 1);
                    spentType = rt;
                    break;
                }
            }

            // 面朝下放置
            _gs.PHand.Remove(card);
            card.IsStandby = true;
            card.StandbyBFIndex = chosenBF;
            card.StandbyReadyToFlip = false; // 这回合不能翻
            _gs.BF[chosenBF].PlayerStandby = card;
            TurnManager.BroadcastMessage_Static(
                $"[待命] 面朝下放置到战场 {chosenBF + 1}，支付 1 {spentType.ToChinese()}符能");
            RefreshUI();
        }

        /// <summary>
        /// B8 full: 翻开打出待命牌（Rule 23）。0 费（无视基础费用）。
        /// 玩家可在任意时机（即便对手回合）触发，但必须 StandbyReadyToFlip=true。
        /// 翻开打出按卡牌原效果结算，但目标必须来自此牌所在战场。
        /// </summary>
        public async Task FlipAndPlayStandbyAsync(int bfId, string owner)
        {
            if (_gs == null || _gs.GameOver) return;
            if (bfId < 0 || bfId >= GameRules.BATTLEFIELD_COUNT) return;

            var bf = _gs.BF[bfId];
            UnitInstance card = owner == GameRules.OWNER_PLAYER ? bf.PlayerStandby : bf.EnemyStandby;
            if (card == null)
            {
                FireHintToast("该战场无面朝下牌");
                return;
            }
            if (!card.StandbyReadyToFlip)
            {
                FireHintToast("待命牌在放下这一回合不能翻开");
                return;
            }

            // 从待命区移除
            if (owner == GameRules.OWNER_PLAYER) bf.PlayerStandby = null;
            else                                 bf.EnemyStandby  = null;
            card.IsStandby = false;
            card.StandbyReadyToFlip = false;

            TurnManager.BroadcastMessage_Static($"[待命] 翻开 {card.UnitName}（0费打出）");

            // 根据卡类型走相应流程（费用设为 0，已在 IsStandby 时记录 BF 约束）
            if (card.CardData.IsEquipment)
            {
                // 装备：放到基地（0 费，跳过 cost 检查）
                _gs.GetBase(owner).Add(card);
                UI.GameEventBus.FireUnitEntered(card, owner); // DEV-32 A6 事件化
            }
            else if (card.CardData.IsSpell)
            {
                // 法术：立刻结算（目标来自 bfId）
                _gs.GetDiscard(owner).Add(card); // 法术打出后弃置
                UnitInstance t = null;
                if (card.CardData.SpellTargetType != SpellTargetType.None)
                {
                    // 从 bfId 选目标
                    var list = new System.Collections.Generic.List<UnitInstance>();
                    if (card.CardData.SpellTargetType == SpellTargetType.EnemyUnit ||
                        card.CardData.SpellTargetType == SpellTargetType.AnyUnit)
                    {
                        list.AddRange(owner == GameRules.OWNER_PLAYER ? bf.EnemyUnits : bf.PlayerUnits);
                    }
                    if (card.CardData.SpellTargetType == SpellTargetType.FriendlyUnit ||
                        card.CardData.SpellTargetType == SpellTargetType.AnyUnit)
                    {
                        list.AddRange(owner == GameRules.OWNER_PLAYER ? bf.PlayerUnits : bf.EnemyUnits);
                    }
                    list.RemoveAll(u => u.UntargetableBySpells);
                    if (list.Count == 0)
                    {
                        // Rule 23.1.d.1：无合法目标 → 无法翻开打出，已从待命区移除视为失败
                        TurnManager.BroadcastMessage_Static($"[待命] {card.UnitName} 无合法目标，打出失败");
                        RefreshUI();
                        return;
                    }
                    t = list[0]; // 简化：自动选第一个
                }
                _spellSys?.CastSpell(card, owner, t, _gs);
            }
            else
            {
                // 单位：放到基地（休眠）
                _gs.GetBase(owner).Add(card);
                card.Exhausted = true;
                UI.GameEventBus.FireUnitEntered(card, owner); // DEV-32 A6 事件化
            }
            RefreshUI();
            await System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// 军团（Legion, Rule 24）费用减免：
        ///   - 卡牌带 Inspire 关键词 + 本回合已打过其他牌 → 费用 -= LegionCostReduction（最低 0）。
        /// 各卡减免量由 CardData.LegionCostReduction 配置（noxus_recruit=2；未来其他 Legion 卡可定制）。
        /// </summary>
        private int ComputeLegionAdjustedCost(UnitInstance unit, GameState gs)
        {
            int baseCost = unit.CardData.Cost;
            if (unit.CardData.HasKeyword(CardKeyword.Inspire) &&
                unit.CardData.LegionCostReduction > 0 &&
                gs.CardsPlayedThisTurn >= 1)
            {
                return Mathf.Max(0, baseCost - unit.CardData.LegionCostReduction);
            }
            return baseCost;
        }

        private async Task TryPlayUnitAsync(UnitInstance unit)
        {
            // UI-OVERHAUL-1c-β: mana/sch/Haste 已在 ValidateAndCommitPreparedFor 完成扣费 + commit
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
            if (!_gs.PHand.Contains(unit)) return;

            bool useHaste = _lastCommitUsedHaste;
            _lastCommitUsedHaste = false;

            _gs.PHand.Remove(unit);
            _gs.PBase.Add(unit);

            bool rally = _gs.RallyCallActiveThisTurn.TryGetValue(GameRules.OWNER_PLAYER, out var rp) && rp;
            if (useHaste)
            {
                unit.Exhausted = false;
                UI.GameEventBus.FireUnitFloatText(unit, "急速！", UI.GameColors.BuffColor);
            }
            else if (rally)
            {
                unit.Exhausted = false;
                UI.GameEventBus.FireUnitFloatText(unit, "迎敌号令·活跃！", UI.GameColors.BuffColor);
            }
            else
            {
                unit.Exhausted = true;
            }

            if (unit.IsEphemeral) unit.SummonedOnRound = _gs.Round;
            unit.PlayedThisTurn = true;  // B10: 标记为"本回合打出"以支持 duel_stance 额外+1
            _gs.CardsPlayedThisTurn++;
            FireCardPlayed(unit, GameRules.OWNER_PLAYER);
            TurnManager.BroadcastMessage_Static(
                $"[打出] {unit.UnitName}（剩余法力 {_gs.PMana}）");

            // Trigger entry effects
            if (_entryEffects != null)
                UI.GameEventBus.FireUnitEntered(unit, GameRules.OWNER_PLAYER); // DEV-32 A6

            // DEV-26: Foresight prompt (player only — AI handled in EntryEffectSystem)
            if (unit.CardData.HasKeyword(CardKeyword.Foresight))
                await HandleForesightPromptAsync(GameRules.OWNER_PLAYER);
            // Kaisa 进化机制已废弃（原卡 OGN-247 无此效果）

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
            // UI-OVERHAUL-1c-β: mana/sch/Haste 已由 ValidateAndCommitPreparedFor 扣除
            if (_gs == null || _gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER || _gs.Phase != GameRules.PHASE_ACTION) return;
            if (_gs.PHero != hero) return;

            bool useHaste = _lastCommitUsedHaste;
            _lastCommitUsedHaste = false;

            _gs.PHero = null;
            _gs.PBase.Add(hero);

            bool rallyH = _gs.RallyCallActiveThisTurn.TryGetValue(GameRules.OWNER_PLAYER, out var rph) && rph;
            if (useHaste)
            {
                hero.Exhausted = false;
                UI.GameEventBus.FireUnitFloatText(hero, "急速！", UI.GameColors.BuffColor);
            }
            else if (rallyH)
            {
                hero.Exhausted = false;
                UI.GameEventBus.FireUnitFloatText(hero, "迎敌号令·活跃！", UI.GameColors.BuffColor);
            }
            else
            {
                hero.Exhausted = true;
            }

            if (hero.IsEphemeral) hero.SummonedOnRound = _gs.Round;
            hero.PlayedThisTurn = true;
            _gs.CardsPlayedThisTurn++;
            FireCardPlayed(hero, GameRules.OWNER_PLAYER);
            TurnManager.BroadcastMessage_Static($"[英雄出场] {hero.UnitName}（剩余法力 {_gs.PMana}）");

            if (_entryEffects != null)
                UI.GameEventBus.FireUnitEntered(hero, GameRules.OWNER_PLAYER); // DEV-32 A6

            if (hero.CardData.HasKeyword(CardKeyword.Foresight))
                await HandleForesightPromptAsync(GameRules.OWNER_PLAYER);

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
            UI.GameEventBus.FireUnitEntered(equip, GameRules.OWNER_PLAYER); // DEV-32 A6

            // Fly ghost to target unit, then refresh UI
            var tcs2 = new System.Threading.Tasks.TaskCompletionSource<bool>();
            _ui?.AnimateEquipFlyToUnit(mouseCanvasPos, target, () =>
            {
                UI.GameEventBus.FireUnitFloatText(target, $"附着：{equip.UnitName}", UI.GameColors.BuffColor);
                tcs2.TrySetResult(true);
            });
            if (_ui != null) await tcs2.Task;

            // 金色幽灵飞完后再发能量球飞到被装备单位（顺序，不并行）
            if (UI.EntryEffectVFX.Instance != null)
            {
                UI.EntryEffectVFX.Instance.PlayEquipOrb(
                    mouseCanvasPos, target,
                    $"+{bonus}战力", UI.GameColors.BuffColor);
                await FWTCG.Core.GameTiming.Delay(400);
            }

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

        /// <summary>
        /// B1/B2: 取消法术目标选择 / 法盾付不起时，退还所有已扣费用。
        /// 调用方保证 spell 此前已扣法力 + 主符能 + 次符能（如有）。
        /// </summary>
        private void RefundSpellCosts(UnitInstance spell)
        {
            _gs.PHand.Add(spell);
            _gs.PMana += GameRules.GetSpellEffectiveCost(spell, GameRules.OWNER_PLAYER, _gs);
            if (spell.CardData.RuneCost > 0)
                _gs.AddSch(GameRules.OWNER_PLAYER, spell.CardData.RuneType, spell.CardData.RuneCost);
            if (spell.CardData.HasSecondaryRune)
                _gs.AddSch(GameRules.OWNER_PLAYER, spell.CardData.SecondaryRuneType, spell.CardData.SecondaryRuneCost);
            _gs.CardsPlayedThisTurn--;
        }

        private async Task TryPlaySpellAsync(UnitInstance spell)
        {
            // B1: 法力检查（balance_resolve 条件减费由 GetSpellEffectiveCost 处理）
            int manaCost = GameRules.GetSpellEffectiveCost(spell, GameRules.OWNER_PLAYER, _gs);
            if (manaCost > _gs.PMana)
            {
                ShowPlayError($"[提示] 法力不足：需要 {manaCost}，当前 {_gs.PMana}", spell);
                return;
            }

            // B1: 主符能检查（用 TotalSch = 主池 + Kaisa legend 产出的法术专用池）
            if (spell.CardData.RuneCost > 0)
            {
                int haveSch = _gs.GetTotalSch(GameRules.OWNER_PLAYER, spell.CardData.RuneType);
                if (haveSch < spell.CardData.RuneCost)
                {
                    ShowPlayError($"[提示] 符能不足：需要 {spell.CardData.RuneCost} {spell.CardData.RuneType.ToColoredText()}，当前 {haveSch}", spell);
                    return;
                }
            }

            // B2: 次符能检查（双色法术，如 akasi_storm）
            if (spell.CardData.HasSecondaryRune)
            {
                int have2 = _gs.GetTotalSch(GameRules.OWNER_PLAYER, spell.CardData.SecondaryRuneType);
                if (have2 < spell.CardData.SecondaryRuneCost)
                {
                    ShowPlayError($"[提示] 符能不足：需要 {spell.CardData.SecondaryRuneCost} {spell.CardData.SecondaryRuneType.ToColoredText()}，当前 {have2}", spell);
                    return;
                }
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

            // B1+B2: 扣除法力 + 主符能 + 次符能（法术专用池优先消耗）
            _gs.PMana -= manaCost;
            if (spell.CardData.RuneCost > 0)
                _gs.SpendSchForSpell(GameRules.OWNER_PLAYER, spell.CardData.RuneType, spell.CardData.RuneCost);
            if (spell.CardData.HasSecondaryRune)
                _gs.SpendSchForSpell(GameRules.OWNER_PLAYER, spell.CardData.SecondaryRuneType, spell.CardData.SecondaryRuneCost);

            _gs.CardsPlayedThisTurn++;
            FireCardPlayed(spell, GameRules.OWNER_PLAYER);
            _gs.PHand.Remove(spell);

            if (spell.CardData.SpellTargetType == SpellTargetType.None)
            {
                // No target needed — show showcase immediately, then resolve with AI reaction window (DEV-15)
                if (_spellShowcase != null)
                    _ = _spellShowcase.ShowAsync(spell, GameRules.OWNER_PLAYER);
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
                    // C-8: exclude enemies marked UntargetableBySpells (sandshoal_deserter)
                    !(u.Owner == GameRules.OWNER_ENEMY && u.UntargetableBySpells) &&
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
                // Cancelled — refund mana + all runes and return spell to hand
                RefundSpellCosts(spell);
                TurnManager.BroadcastMessage_Static($"[法术] 取消 {spell.UnitName} 的目标选择，费用已退还");
                RefreshUI();
                return;
            }

            // Rule 721: SpellShield forces caster to pay 1 extra sch to target the unit
            if (!TryPaySpellShieldCost(GameRules.OWNER_PLAYER, target))
            {
                // Can't afford — treat as cancelled, refund
                RefundSpellCosts(spell);
                ShowPlayError($"[法盾] 符能不足：{target.UnitName} 拥有法盾，需要至少1点符能才能选为目标", spell);
                RefreshUI();
                return;
            }

            // Target confirmed — now play the spell showcase (fires in parallel with cast resolution)
            if (_spellShowcase != null)
                _ = _spellShowcase.ShowAsync(spell, GameRules.OWNER_PLAYER);

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

            await FWTCG.Core.GameTiming.Delay(GameRules.AI_ACTION_DELAY_MS);
            if (_gs.GameOver) { _aiReactionPending = false; return; }

            // DEV-27: enter SpellDuel_OpenLoop so Swift cards become legal (Rule 718)
            TurnStateMachine.TransitionTo(TurnStateMachine.State.SpellDuel_OpenLoop);
            bool negated = AiTryReact(spell);
            // DEV-27: return to player action phase after AI reaction resolves
            TurnStateMachine.TransitionTo(TurnStateMachine.State.Normal_OpenLoop);

            if (negated)
            {
                await FWTCG.Core.GameTiming.Delay(300); // brief pause so player reads the negation log
                TurnManager.BroadcastMessage_Static($"[法术] {spell.UnitName} 被无效化！");
            }
            else if (!_gs.GameOver)
            {
                // 等 showcase 完全播完（约 SpellShowcaseUI.TOTAL_DURATION 秒）再飞能量球。
                // 避免球在展示还没结束时就飞出去抢戏。
                // TOTAL_DURATION = FLY_IN + HOLD + DISSOLVE (~1.63s)，bot 10x 下 ≈0.16s。
                await FWTCG.Core.GameTiming.Delay((int)(UI.SpellShowcaseUI.TOTAL_DURATION * 1000f));

                // 展示完：飞能量球到 target（如果有）
                if (target != null && UI.EntryEffectVFX.Instance != null)
                {
                    UI.EntryEffectVFX.Instance.PlaySpellOrbs(
                        new System.Collections.Generic.List<UnitInstance> { target },
                        spell.UnitName,
                        UI.GameColors.SchColor);
                    // 让球飞行 ~0.4s 再结算，保证视觉"命中"
                    await FWTCG.Core.GameTiming.Delay(400);
                }

                if (_spellSys != null)
                {
                    // furnace_blast 位置选择：施法前让玩家选战场
                    if (spell.CardData.EffectId == "furnace_blast")
                        _gs.FurnaceBlastBfOverride = await PickFurnaceBlastPositionAsync();

                    // akasi_storm 预选 6 个目标（玩家逐次弹窗）
                    if (spell.CardData.EffectId == "akasi_storm")
                        await PrepareAkasiStormTargetsAsync();

                    _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, target, _gs);

                    // stardrop 第二段：玩家选新目标（可同可不同，可取消）
                    if (!_gs.GameOver && spell.CardData.EffectId == "stardrop")
                    {
                        await TryStardropSecondAsync(target);
                    }

                    // Echo 玩家路径：法术具 Echo 关键字 且 可付费 → 询问是否重复
                    if (!_gs.GameOver &&
                        spell.CardData.HasKeyword(CardKeyword.Echo) &&
                        GameRules.CanAffordEcho(spell, GameRules.OWNER_PLAYER, _gs))
                    {
                        await TryEchoPromptAsync(spell, GameRules.OWNER_PLAYER, target);
                    }
                }
                else
                    _gs.PDiscard.Add(spell);
            }

            // Wait for death animation to complete before RefreshUI rebuilds containers.
            // DeathRoutine = Phase A dissolve ~0.6s + Phase B ghost fly ~0.5s = ~1.1s total.
            // 550ms was too short — caused grey ghost cards when base units died mid-animation.
            await FWTCG.Core.GameTiming.Delay(1200);

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
            bool aiJaxActive = GameRules.IsJaxInPlay(GameRules.OWNER_ENEMY, _gs);
            foreach (var c in _gs.EHand)
            {
                bool aiAffordRune = c.CardData.RuneCost == 0 ||
                    _gs.GetTotalSch(GameRules.OWNER_ENEMY, c.CardData.RuneType) >= c.CardData.RuneCost;
                bool aiAffordRune2 = !c.CardData.HasSecondaryRune ||
                    _gs.GetTotalSch(GameRules.OWNER_ENEMY, c.CardData.SecondaryRuneType) >= c.CardData.SecondaryRuneCost;
                bool spellReactive = c.CardData.IsSpell &&
                    (c.CardData.HasKeyword(CardKeyword.Reactive) || c.CardData.HasKeyword(CardKeyword.Swift)) &&
                    GameRules.GetSpellEffectiveCost(c, GameRules.OWNER_ENEMY, _gs) <= _gs.EMana &&
                    aiAffordRune && aiAffordRune2;
                bool equipReactive = c.CardData.IsEquipment && aiJaxActive &&
                    c.CardData.Cost <= _gs.EMana &&
                    (c.CardData.RuneCost == 0 ||
                     _gs.GetSch(GameRules.OWNER_ENEMY, c.CardData.RuneType) >= c.CardData.RuneCost);
                if (spellReactive || equipReactive)
                {
                    reactives.Add(c);
                }
            }

            if (reactives.Count == 0) return false;

            UnitInstance chosen = SimpleAI.AiPickBestReactiveCard(reactives, playerSpell, _gs, GameRules.OWNER_ENEMY);
            if (chosen == null) return false;

            // Pay cost and apply reactive
            _gs.EMana -= GameRules.GetSpellEffectiveCost(chosen, GameRules.OWNER_ENEMY, _gs);
            TurnManager.ShowBanner_Static($"⚡ [AI] 反应！{chosen.UnitName}");
            // Full-screen showcase for AI reactive card (matches player reactive + spell visual)
            if (_spellShowcase != null)
                _ = _spellShowcase.ShowAsync(chosen, GameRules.OWNER_ENEMY);
            if (chosen.CardData.IsEquipment)
            {
                // jax 被动：AI 用装备作为反应牌（装备不走 GetSpellEffectiveCost，但需扣符文）
                if (chosen.CardData.RuneCost > 0)
                    _gs.SpendSch(GameRules.OWNER_ENEMY, chosen.CardData.RuneType, chosen.CardData.RuneCost);
                _gs.EHand.Remove(chosen);
                _gs.EBase.Add(chosen);
                chosen.Exhausted = chosen.CardData.HasKeyword(CardKeyword.Standby);
                _gs.CardsPlayedThisTurn++;
                TurnManager.BroadcastMessage_Static($"[AI·贾克斯] 装备反应 {chosen.UnitName} 入基地");
                return false;
            }
            // 反应法术符文成本扣除（ApplyReactive 不扣 RuneCost，此处显式处理）
            if (chosen.CardData.RuneCost > 0)
                _gs.SpendSchForSpell(GameRules.OWNER_ENEMY, chosen.CardData.RuneType, chosen.CardData.RuneCost);
            if (chosen.CardData.HasSecondaryRune)
                _gs.SpendSchForSpell(GameRules.OWNER_ENEMY, chosen.CardData.SecondaryRuneType, chosen.CardData.SecondaryRuneCost);
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
            if (!IsPlayerActionPhase() && !AskPromptUI.IsShowing)
            {
                _selectedBaseUnits.Clear();
                _selectedHandUnits.Clear();
                ClearPreparedRunes(); // UI-OVERHAUL-1b: 非行动阶段自动清空符文标记
            }
            // 统一规则：玩家主动标记 → isHint=false（tap=绿选中 / recycle=红回收）
            _ui.SetRuneHighlights(new List<int>(_preparedTapIdxs), new List<int>(_preparedRecycleIdxs), isHint: false);
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
        /// 检查玩家手中是否有可立即打出的反应/迅捷法术（用于按钮亮暗状态）。
        /// 条件：手牌中存在 IsSpell + (Reactive 或 Swift) 且经 RuneAutoConsume 可付费。
        /// </summary>
        public bool HasAffordableReactive()
        {
            if (_gs == null || _gs.GameOver) return false;
            if (_gs.PHand == null) return false;
            foreach (var c in _gs.PHand)
            {
                if (c == null || c.CardData == null) continue;
                bool spellReactive = c.CardData.IsSpell &&
                    (c.CardData.HasKeyword(CardKeyword.Reactive) || c.CardData.HasKeyword(CardKeyword.Swift));
                bool equipReactive = c.CardData.IsEquipment &&
                    GameRules.IsJaxInPlay(GameRules.OWNER_PLAYER, _gs);
                if (!spellReactive && !equipReactive) continue;
                var plan = RuneAutoConsume.Compute(c, _gs, GameRules.OWNER_PLAYER);
                if (plan.CanAfford) return true;
            }
            return false;
        }

        /// <summary>
        /// furnace_blast 位置选择：弹 AskPromptUI 让玩家选战场 0 / 战场 1。
        /// 返回 BF 索引；弹窗不可用或玩家不选则返回 -1（SpellSystem 兜底用 AI 启发式）。
        /// </summary>
        private async Task<int> PickFurnaceBlastPositionAsync()
        {
            if (UI.AskPromptUI.Instance == null) return -1;
            try
            {
                string bf0Name = _gs.BFNames != null && _gs.BFNames.Length > 0 && !string.IsNullOrEmpty(_gs.BFNames[0])
                    ? _gs.BFNames[0] : "战场1";
                string bf1Name = _gs.BFNames != null && _gs.BFNames.Length > 1 && !string.IsNullOrEmpty(_gs.BFNames[1])
                    ? _gs.BFNames[1] : "战场2";
                bool pickFirst = await UI.AskPromptUI.Instance.WaitForConfirm(
                    "风箱炎息 · 选位置",
                    "对哪个战场的单位造成伤害？",
                    bf0Name, bf1Name);
                return pickFirst ? 0 : 1;
            }
            catch (System.OperationCanceledException)
            {
                return -1;
            }
        }

        /// <summary>
        /// akasi_storm 预选 6 次目标（玩家逐次弹窗）。取消 / 无弹窗则留空，SpellSystem 兜底选最低 HP。
        /// </summary>
        private async Task PrepareAkasiStormTargetsAsync()
        {
            _gs.AkasiStormTargets.Clear();
            var popup = UI.SpellTargetPopup.Instance;
            if (popup == null) return;
            for (int i = 0; i < 6; i++)
            {
                TurnManager.ShowBanner_Static($"🌪 [艾卡西亚暴雨] 第 {i + 1}/6 次目标");
                UnitInstance pick = null;
                try
                {
                    pick = await popup.ShowAsync(SpellTargetType.EnemyUnit, _gs);
                }
                catch (System.OperationCanceledException) { pick = null; }
                _gs.AkasiStormTargets.Add(pick); // null 也占位，由 SpellSystem 兜底
            }
        }

        /// <summary>
        /// stardrop 第二段：玩家选新目标（卡面"可以选择不同的单位"）。取消则兜底打首目标（若仍存活）。
        /// </summary>
        private async Task TryStardropSecondAsync(UnitInstance firstTarget)
        {
            if (_spellSys == null) return;
            var popup = UI.SpellTargetPopup.Instance;
            UnitInstance second = null;
            if (popup != null)
            {
                TurnManager.ShowBanner_Static("✨ [星落·再次] 选择第二次目标");
                try
                {
                    second = await popup.ShowAsync(SpellTargetType.EnemyUnit, _gs);
                }
                catch (System.OperationCanceledException) { second = null; }
            }
            if (second == null || second.CurrentHp <= 0)
                second = (firstTarget != null && firstTarget.CurrentHp > 0) ? firstTarget : null;
            if (second == null) return;
            _spellSys.StardropSecondStrike(second, _gs);
            await FWTCG.Core.GameTiming.Delay(400);
            RefreshUI();
        }

        /// <summary>
        /// Echo 玩家路径：法术结算后询问是否支付 1 主符能重复效果。
        /// 若法术需目标（EnemyUnit / FriendlyUnit / AnyUnit），重新开 SpellTargetPopup 选新目标。
        /// </summary>
        private async Task TryEchoPromptAsync(UnitInstance spell, string owner, UnitInstance firstTarget)
        {
            if (UI.AskPromptUI.Instance == null || _spellSys == null) return;

            bool confirm;
            try
            {
                string runeLabel = spell.CardData.RuneType.ToChinese();
                confirm = await UI.AskPromptUI.Instance.WaitForConfirm(
                    "回响",
                    $"消耗 1 点{runeLabel}符能再次发动 {spell.UnitName}？",
                    "回响", "取消");
            }
            catch (System.OperationCanceledException)
            {
                return;
            }
            if (!confirm) return;

            // 再次确认可付（玩家权衡期间可能有其他效果影响符能池）
            if (!GameRules.CanAffordEcho(spell, owner, _gs)) return;
            GameRules.SpendEchoCost(spell, owner, _gs);

            // 选新目标（若法术需目标）
            UnitInstance echoTarget = firstTarget;
            if (spell.CardData.SpellTargetType != SpellTargetType.None)
            {
                var popup = UI.SpellTargetPopup.Instance;
                if (popup != null)
                {
                    try
                    {
                        echoTarget = await popup.ShowAsync(spell.CardData.SpellTargetType, _gs);
                    }
                    catch (System.OperationCanceledException)
                    {
                        echoTarget = firstTarget;
                    }
                    // 玩家取消 → 回退到首目标（若仍存活）或空
                    if (echoTarget == null || echoTarget.CurrentHp <= 0)
                        echoTarget = (firstTarget != null && firstTarget.CurrentHp > 0) ? firstTarget : null;
                }
            }

            TurnManager.ShowBanner_Static($"⚡ [回响] {spell.UnitName}");
            if (echoTarget != null && UI.EntryEffectVFX.Instance != null)
            {
                UI.EntryEffectVFX.Instance.PlaySpellOrbs(
                    new System.Collections.Generic.List<UnitInstance> { echoTarget },
                    spell.UnitName + "·回响",
                    UI.GameColors.SchColor);
                await FWTCG.Core.GameTiming.Delay(300);
            }
            _spellSys.EchoCast(spell, owner, echoTarget, _gs);
            await FWTCG.Core.GameTiming.Delay(500);
            RefreshUI();
        }

        /// <summary>
        /// Called when the player clicks the React button (any time, any turn).
        /// Collects affordable reactive cards from hand.
        /// If any exist → opens the reaction window (player must pick one, no cancel).
        /// If none → shows a message.
        /// </summary>
        private async void OnReactClicked()
        {
            if (_gs == null || _gs.GameOver) return;
            var window = ReactionWindow;
            if (window == null) return;

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
            bool jaxActive = GameRules.IsJaxInPlay(GameRules.OWNER_PLAYER, _gs);
            foreach (var c in _gs.PHand)
            {
                bool spellReactive = c.CardData.IsSpell &&
                    (c.CardData.HasKeyword(CardKeyword.Reactive) || c.CardData.HasKeyword(CardKeyword.Swift));
                bool equipReactive = c.CardData.IsEquipment && jaxActive;
                if (spellReactive || equipReactive)
                {
                    var affordPlan = RuneAutoConsume.Compute(c, _gs, GameRules.OWNER_PLAYER);
                    if (affordPlan.CanAfford)
                        reactives.Add(c);
                }
            }

            if (reactives.Count == 0)
            {
                // 允许玩家疯狂点击"抢触发"，但 toast 最多 2 秒一次，避免刷屏
                if (Time.time - _lastNoReactiveToastTime > NO_REACTIVE_TOAST_COOLDOWN)
                {
                    _lastNoReactiveToastTime = Time.time;
                    TurnManager.BroadcastMessage_Static(
                        $"[反应] 当前没有可打出的反应牌（手牌无反应法术或资源不足，当前法力：{_gs.PMana}）");
                }
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
                picked = await window.WaitForReaction(
                    reactives,
                    $"选择反应牌打出（当前法力：{_gs.PMana}）",
                    _gs,
                    onHoverEnter: u =>
                    {
                        var p = RuneAutoConsume.Compute(u, _gs, GameRules.OWNER_PLAYER);
                        if (p.NeedsOps) { _ui?.SetRuneHighlights(p.TapIndices, p.RecycleIndices, isHint: true); _ui?.Refresh(_gs); }
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
                    _ui?.SetRuneHighlights(reactPlan.TapIndices, reactPlan.RecycleIndices, isHint: true);
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

                // balance_resolve 条件减费也适用于反应/Swift 路径
                int pickedCost = GameRules.GetSpellEffectiveCost(picked, GameRules.OWNER_PLAYER, _gs);
                _gs.PMana -= pickedCost;
                TurnManager.BroadcastMessage_Static(
                    $"[反应] 打出 {picked.UnitName}（费用{pickedCost}），剩余法力 {_gs.PMana}");
                FireCardPlayed(picked, GameRules.OWNER_PLAYER); // triggers board flash
                if (_spellShowcase != null)
                    await _spellShowcase.ShowAsync(picked, GameRules.OWNER_PLAYER); // card art center reveal

                if (picked.CardData.IsEquipment)
                {
                    // jax 被动：装备作为反应牌打出 → 直接进入基地
                    // 装备符文扣除（ExecuteRunePlan 只在 NeedsOps 时跑，不保证扣主符能）
                    if (picked.CardData.RuneCost > 0)
                        _gs.SpendSch(GameRules.OWNER_PLAYER, picked.CardData.RuneType, picked.CardData.RuneCost);
                    _gs.PHand.Remove(picked);
                    _gs.PBase.Add(picked);
                    picked.Exhausted = picked.CardData.HasKeyword(CardKeyword.Standby);
                    _gs.CardsPlayedThisTurn++;
                    TurnManager.BroadcastMessage_Static(
                        $"[贾克斯·装备反应] {picked.UnitName} 部署到基地");
                }
                else
                {
                    // 反应法术符文成本扣除（ExecuteRunePlan 只补亏空，不扣 RuneCost 本身）
                    if (picked.CardData.RuneCost > 0)
                        _gs.SpendSchForSpell(GameRules.OWNER_PLAYER, picked.CardData.RuneType, picked.CardData.RuneCost);
                    if (picked.CardData.HasSecondaryRune)
                        _gs.SpendSchForSpell(GameRules.OWNER_PLAYER, picked.CardData.SecondaryRuneType, picked.CardData.SecondaryRuneCost);
                    // ApplyReactive handles hand→discard move internally
                    _reactiveSys?.ApplyReactive(picked, GameRules.OWNER_PLAYER, null, _gs);
                }
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
        /// 玩家点击传奇技能按钮 — 卡莎·虚空之女：横置→反应 — 获得 1 任意符能（仅法术）。
        /// 弹窗让玩家选 6 色符能之一。
        /// </summary>
        private void OnLegendSkillClicked()
        {
            _ = OnLegendSkillClickedAsync();
        }

        private async Task OnLegendSkillClickedAsync()
        {
            if (_gs == null || _gs.GameOver) return;
            if (_legendSys == null) return;

            var legend = _gs.GetLegend(GameRules.OWNER_PLAYER);
            if (legend == null || legend.Id != LegendSystem.KAISA_LEGEND_ID) return;
            if (legend.AbilityUsedThisTurn)
            {
                FireHintToast("虚空之女本回合已使用");
                return;
            }

            // 按卡面弹窗让玩家选 6 色之一（此处简化为 2 色：卡莎卡组主要用炽烈/灵光；
            //  如需完整 6 色可扩展 AskPromptUI 为多选。）
            Data.RuneType chosen;
            if (UI.AskPromptUI.Instance != null)
            {
                bool pickBlazing;
                try
                {
                    pickBlazing = await UI.AskPromptUI.Instance.WaitForConfirm(
                        "虚空之女",
                        "选择获得的符能颜色（仅能用于打出法术）",
                        "炽烈", "灵光");
                }
                catch { return; }
                chosen = pickBlazing ? Data.RuneType.Blazing : Data.RuneType.Radiant;
            }
            else
            {
                chosen = Data.RuneType.Blazing;
            }

            bool used = _legendSys.UseKaisaActive(GameRules.OWNER_PLAYER, _gs, chosen);
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
                // DEV-32 A3: 统一走 DamageRouter
                Systems.DamageRouter.Apply(u, dmg, _gs,
                    Systems.DamageRouter.DamageKind.Debug, "DEBUG");
                if (u.CurrentHp <= 0) baseDead.Add(u);
            }
            foreach (var u in baseDead)
            {
                FireUnitDied(u);
                baseList.Remove(u);
                _gs.GetDiscard(targetOwner).Add(u);
            }
            UI.GameEventBus.FireUnitsDied(baseDead, -1); // DEV-32 A6

            // ── Battlefield units ────────────────────────────────────────────
            foreach (var bf in _gs.BF)
            {
                var bfList = targetOwner == GameRules.OWNER_PLAYER ? bf.PlayerUnits : bf.EnemyUnits;
                var bfDead = new List<UnitInstance>();
                foreach (var u in new List<UnitInstance>(bfList))
                {
                    Systems.DamageRouter.Apply(u, dmg, _gs,
                        Systems.DamageRouter.DamageKind.Debug, "DEBUG");
                    if (u.CurrentHp <= 0) bfDead.Add(u);
                }
                foreach (var u in bfDead)
                {
                    FireUnitDied(u);
                    bfList.Remove(u);
                    _gs.GetDiscard(targetOwner).Add(u);
                }
                UI.GameEventBus.FireUnitsDied(bfDead, bf.Id); // DEV-32 A6
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
