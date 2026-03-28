using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
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

        // ── Game state ────────────────────────────────────────────────────────
        private GameState _gs;

        // ── Interaction state ─────────────────────────────────────────────────
        private UnitInstance _selectedUnit;
        private string _selectedUnitLoc; // "hand", "base", "0", "1"

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
        }

        private void OnEnable()
        {
            TurnManager.OnMessage += HandleMessage;
            TurnManager.OnPhaseChanged += HandlePhaseChanged;
            CombatSystem.OnCombatLog += HandleMessage;
            ScoreManager.OnGameOver += HandleGameOver;
            ScoreManager.OnScoreChanged += HandleMessage;
        }

        private void OnDisable()
        {
            TurnManager.OnMessage -= HandleMessage;
            TurnManager.OnPhaseChanged -= HandlePhaseChanged;
            CombatSystem.OnCombatLog -= HandleMessage;
            ScoreManager.OnGameOver -= HandleGameOver;
            ScoreManager.OnScoreChanged -= HandleMessage;
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
            StartCoroutine(GameLoop());
        }

        // ── Initialisation ────────────────────────────────────────────────────

        private void InitGame()
        {
            GameState.ResetUidCounter();
            _gs = new GameState();

            // Inject dependencies into systems
            _turnMgr.Inject(_gs, _scoreMgr, _combatSys, _ai);

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

            string firstLabel = _gs.First == GameRules.OWNER_PLAYER ? "玩家" : "AI";
            TurnManager.BroadcastMessage_Static($"[开局] {firstLabel} 先手。发牌完成。");

            RefreshUI();
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
        /// First click selects the unit; second click on a battlefield moves it.
        /// </summary>
        public void OnUnitClicked(UnitInstance unit)
        {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;

            if (_selectedUnit == unit)
            {
                // Deselect
                _selectedUnit = null;
                _selectedUnitLoc = null;
                TurnManager.BroadcastMessage_Static($"[选择] 取消选择 {unit.UnitName}");
                return;
            }

            _selectedUnit = unit;

            // Determine where this unit currently is
            if (_gs.PHand.Contains(unit))
            {
                // Unit is in hand — try to play it (add to base)
                TryPlayCard(unit);
                return;
            }

            if (_gs.PBase.Contains(unit))
            {
                _selectedUnitLoc = "base";
                TurnManager.BroadcastMessage_Static($"[选择] {unit.UnitName}（基地） — 点击战场移动");
                return;
            }

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                if (_gs.BF[i].PlayerUnits.Contains(unit))
                {
                    _selectedUnitLoc = i.ToString();
                    TurnManager.BroadcastMessage_Static($"[选择] {unit.UnitName}（战场{i + 1}） — 点击目标战场移动");
                    return;
                }
            }

            RefreshUI();
        }

        /// <summary>
        /// Called when the player clicks a battlefield zone.
        /// If a unit is selected, moves it to that battlefield.
        /// </summary>
        public void OnBattlefieldClicked(int bfId)
        {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;

            if (_selectedUnit == null)
            {
                TurnManager.BroadcastMessage_Static("[提示] 请先选择一个单位");
                return;
            }

            if (_selectedUnit.Exhausted)
            {
                TurnManager.BroadcastMessage_Static($"[提示] {_selectedUnit.UnitName} 已休眠，本回合无法移动");
                _selectedUnit = null;
                _selectedUnitLoc = null;
                return;
            }

            string fromLoc = _selectedUnitLoc ?? "base";
            _combatSys.MoveUnit(_selectedUnit, fromLoc, bfId, GameRules.OWNER_PLAYER, _gs, _scoreMgr);

            _selectedUnit = null;
            _selectedUnitLoc = null;
            RefreshUI();
        }

        /// <summary>
        /// Called when the player clicks "End Turn" button.
        /// </summary>
        public void OnEndTurnClicked()
        {
            if (_gs.GameOver) return;
            if (_gs.Turn != GameRules.OWNER_PLAYER) return;
            if (_gs.Phase != GameRules.PHASE_ACTION) return;

            _selectedUnit = null;
            _selectedUnitLoc = null;
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
            _selectedUnit = null;
            _selectedUnitLoc = "base";
            RefreshUI();
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

            // Add 2 copies of each card (10-card deck, 5 unique)
            foreach (CardData data in cardDatas)
            {
                if (data == null) continue;
                deck.Add(_gs.MakeUnit(data, owner));
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
                _ui.Refresh(_gs);
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
    }
}
