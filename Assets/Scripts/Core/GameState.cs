using System.Collections.Generic;
using FWTCG.Data;

namespace FWTCG.Core
{
    /// <summary>
    /// Complete mutable game state (equivalent to G object in the JS version).
    /// All game data lives here; systems read and write this object.
    /// </summary>
    public class GameState
    {
        // ── UID counter ────────────────────────────────────────────────────────
        private static int _uidCounter = 0;
        public static int NextUid() => ++_uidCounter;
        public static void ResetUidCounter() => _uidCounter = 0;

        // ── Scores ─────────────────────────────────────────────────────────────
        public int PScore { get; set; }
        public int EScore { get; set; }

        // ── Decks ──────────────────────────────────────────────────────────────
        public List<UnitInstance> PDeck { get; set; } = new List<UnitInstance>();
        public List<UnitInstance> EDeck { get; set; } = new List<UnitInstance>();

        // ── Hands ──────────────────────────────────────────────────────────────
        public List<UnitInstance> PHand { get; set; } = new List<UnitInstance>();
        public List<UnitInstance> EHand { get; set; } = new List<UnitInstance>();

        // ── Base (staging area before battlefield) ────────────────────────────
        public List<UnitInstance> PBase { get; set; } = new List<UnitInstance>();
        public List<UnitInstance> EBase { get; set; } = new List<UnitInstance>();

        // ── Discard piles ─────────────────────────────────────────────────────
        public List<UnitInstance> PDiscard { get; set; } = new List<UnitInstance>();
        public List<UnitInstance> EDiscard { get; set; } = new List<UnitInstance>();

        // ── Exile piles ──────────────────────────────────────────────────────
        public List<UnitInstance> PExile { get; set; } = new List<UnitInstance>();
        public List<UnitInstance> EExile { get; set; } = new List<UnitInstance>();

        // ── Runes in play ─────────────────────────────────────────────────────
        public List<RuneInstance> PRunes { get; set; } = new List<RuneInstance>();
        public List<RuneInstance> ERunes { get; set; } = new List<RuneInstance>();

        // ── Rune decks ────────────────────────────────────────────────────────
        public List<RuneInstance> PRuneDeck { get; set; } = new List<RuneInstance>();
        public List<RuneInstance> ERuneDeck { get; set; } = new List<RuneInstance>();

        // ── Battlefields ──────────────────────────────────────────────────────
        public BattlefieldState[] BF { get; private set; }

        // ── Mana ──────────────────────────────────────────────────────────────
        public int PMana { get; set; }
        public int EMana { get; set; }

        // ── Schematic energy (符能) per rune type ────────────────────────────
        public Dictionary<RuneType, int> PSch { get; set; } = new Dictionary<RuneType, int>();
        public Dictionary<RuneType, int> ESch { get; set; } = new Dictionary<RuneType, int>();

        // ── 法术专用符能池（Kaisa legend 产出）— 仅可支付法术符能费用，不能支付单位/装备/技能 ──
        public Dictionary<RuneType, int> PSpellOnlySch { get; set; } = new Dictionary<RuneType, int>();
        public Dictionary<RuneType, int> ESpellOnlySch { get; set; } = new Dictionary<RuneType, int>();

        // ── Turn tracking ─────────────────────────────────────────────────────
        public int Round { get; set; }
        public string Turn { get; set; }    // "player" or "enemy"
        public string Phase { get; set; }   // phase name constant

        /// <summary>Who went first this game.</summary>
        public string First { get; set; }

        public bool PFirstTurnDone { get; set; }
        public bool EFirstTurnDone { get; set; }

        // ── Game over ─────────────────────────────────────────────────────────
        public bool GameOver { get; set; }

        // ── Extra turn (time_warp) ─────────────────────────────────────────────
        public bool ExtraTurnPending { get; set; }

        // ── Per-turn tracking ─────────────────────────────────────────────────
        /// <summary>Battlefield IDs that already awarded a hold score this turn.</summary>
        public List<int> BFScoredThisTurn { get; set; } = new List<int>();

        /// <summary>Battlefield IDs that already awarded a conquest score this turn.</summary>
        public List<int> BFConqueredThisTurn { get; set; } = new List<int>();

        public int CardsPlayedThisTurn { get; set; }

        /// <summary>
        /// rally_call (迎敌号令) 持续效果：true 时，该玩家本回合打出的单位以活跃状态进场。
        /// 由 SpellSystem.RallyCall 设置；TurnManager.DoEndPhase 清零。
        /// </summary>
        public Dictionary<string, bool> RallyCallActiveThisTurn { get; set; } = new Dictionary<string, bool>();

        // ── Battlefield names (selected from faction pools at game start) ─────
        public string[] BFNames { get; set; } = new string[GameRules.BATTLEFIELD_COUNT];

        // ── Passive flags ──────────────────────────────────────────────────────
        // Tiyana 被动改为 ScoreManager.IsTiyanaOnAnyBattlefield() 动态查询，不再维护静态 flag。

        /// <summary>Whether 梦幻树 (dreaming_tree) draw has already triggered this turn.</summary>
        public bool DreamingTreeTriggeredThisTurn { get; set; }

        /// <summary>[Deprecated] Old Inspire mechanic placeholder. noxus_recruit now uses
        /// Legion cost reduction in GameManager, not an entry buff. Field kept for test back-compat.</summary>
        public bool InspireNextUnit { get; set; }

        // ── Hero zone ─────────────────────────────────────────────────────────
        /// <summary>Player's hero card (extracted from deck at game start, rule 103.2.a).</summary>
        public UnitInstance PHero { get; set; }

        /// <summary>Enemy's hero card (extracted from deck at game start).</summary>
        public UnitInstance EHero { get; set; }

        public UnitInstance GetHero(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PHero : EHero;

        public void SetHero(string owner, UnitInstance hero)
        {
            if (owner == GameRules.OWNER_PLAYER) PHero = hero;
            else EHero = hero;
        }

        // ── Legend zone ────────────────────────────────────────────────────────
        /// <summary>Player's legend (Kaisa). Initialized by GameManager at game start.</summary>
        public LegendInstance PLegend { get; set; }

        /// <summary>Enemy's legend (Masteryi). Initialized by GameManager at game start.</summary>
        public LegendInstance ELegend { get; set; }

        /// <summary>Returns the legend for the given owner.</summary>
        public LegendInstance GetLegend(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PLegend : ELegend;

        // ── Constructor ───────────────────────────────────────────────────────
        public GameState()
        {
            BF = new BattlefieldState[GameRules.BATTLEFIELD_COUNT];
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                BF[i] = new BattlefieldState(i);
            }

            // Initialise all rune-type entries to 0
            foreach (RuneType rt in System.Enum.GetValues(typeof(RuneType)))
            {
                PSch[rt] = 0;
                ESch[rt] = 0;
                PSpellOnlySch[rt] = 0;
                ESpellOnlySch[rt] = 0;
            }
        }

        // ── Schematic energy helpers ──────────────────────────────────────────

        public void AddSch(string owner, RuneType type, int n = 1)
        {
            Dictionary<RuneType, int> sch = owner == GameRules.OWNER_PLAYER ? PSch : ESch;
            if (!sch.ContainsKey(type)) sch[type] = 0;
            sch[type] += n;
        }

        public void SpendSch(string owner, RuneType type, int n = 1)
        {
            Dictionary<RuneType, int> sch = owner == GameRules.OWNER_PLAYER ? PSch : ESch;
            if (!sch.ContainsKey(type)) sch[type] = 0;
            sch[type] = UnityEngine.Mathf.Max(0, sch[type] - n);
        }

        public void ResetSch(string owner)
        {
            Dictionary<RuneType, int> sch = owner == GameRules.OWNER_PLAYER ? PSch : ESch;
            foreach (RuneType rt in System.Enum.GetValues(typeof(RuneType)))
            {
                sch[rt] = 0;
            }
        }

        // ── 法术专用符能池 API（Kaisa legend） ────────────────────────────────
        private Dictionary<RuneType, int> GetSpellOnlySch(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PSpellOnlySch : ESpellOnlySch;

        public int GetSpellOnlySch(string owner, RuneType type)
        {
            var pool = GetSpellOnlySch(owner);
            return pool.TryGetValue(type, out int v) ? v : 0;
        }

        public void AddSpellOnlySch(string owner, RuneType type, int n = 1)
        {
            var pool = GetSpellOnlySch(owner);
            if (!pool.ContainsKey(type)) pool[type] = 0;
            pool[type] += n;
        }

        public void SpendSpellOnlySch(string owner, RuneType type, int n)
        {
            var pool = GetSpellOnlySch(owner);
            if (!pool.ContainsKey(type)) pool[type] = 0;
            pool[type] = UnityEngine.Mathf.Max(0, pool[type] - n);
        }

        public void ResetSpellOnlySch(string owner)
        {
            var pool = GetSpellOnlySch(owner);
            foreach (RuneType rt in System.Enum.GetValues(typeof(RuneType)))
                pool[rt] = 0;
        }

        /// <summary>查一个玩家某色符能的总量（主池+法术专用池）— 仅用于查询，不决定支付。</summary>
        public int GetTotalSch(string owner, RuneType type) =>
            GetSch(owner, type) + GetSpellOnlySch(owner, type);

        /// <summary>
        /// 支付法术符能费用：优先消耗法术专用池（Kaisa legend 产出），不足再扣主池。
        /// 调用方保证 GetTotalSch(owner, type) >= n。
        /// </summary>
        public void SpendSchForSpell(string owner, RuneType type, int n)
        {
            int fromSpellOnly = UnityEngine.Mathf.Min(n, GetSpellOnlySch(owner, type));
            if (fromSpellOnly > 0)
            {
                SpendSpellOnlySch(owner, type, fromSpellOnly);
                n -= fromSpellOnly;
            }
            if (n > 0) SpendSch(owner, type, n);
        }

        /// <summary>
        /// Returns total schematic energy for an owner (all types summed)
        /// or the value for a specific type if type is provided.
        /// </summary>
        public int GetSch(string owner, RuneType? type = null)
        {
            Dictionary<RuneType, int> sch = owner == GameRules.OWNER_PLAYER ? PSch : ESch;
            if (type.HasValue)
            {
                return sch.ContainsKey(type.Value) ? sch[type.Value] : 0;
            }
            int total = 0;
            foreach (int v in sch.Values) total += v;
            return total;
        }

        // ── Unit factory ──────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new UnitInstance from CardData with a unique UID.
        /// Applies the atk=HP core rule during construction.
        /// </summary>
        public UnitInstance MakeUnit(CardData data, string owner)
        {
            return new UnitInstance(NextUid(), data, owner);
        }

        // ── Deck/hand helpers ─────────────────────────────────────────────────

        public List<UnitInstance> GetDeck(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PDeck : EDeck;

        public List<UnitInstance> GetHand(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PHand : EHand;

        public List<UnitInstance> GetBase(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PBase : EBase;

        public List<UnitInstance> GetDiscard(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PDiscard : EDiscard;

        public List<UnitInstance> GetExile(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PExile : EExile;

        public List<RuneInstance> GetRunes(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PRunes : ERunes;

        public List<RuneInstance> GetRuneDeck(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PRuneDeck : ERuneDeck;

        public int GetMana(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PMana : EMana;

        public void SetMana(string owner, int value)
        {
            if (owner == GameRules.OWNER_PLAYER) PMana = value;
            else EMana = value;
        }

        public void AddMana(string owner, int amount)
        {
            if (owner == GameRules.OWNER_PLAYER) PMana += amount;
            else EMana += amount;
        }

        public int GetScore(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PScore : EScore;

        public void AddScore(string owner, int amount)
        {
            if (owner == GameRules.OWNER_PLAYER) PScore += amount;
            else EScore += amount;
        }

        public bool IsFirstTurnDone(string owner) =>
            owner == GameRules.OWNER_PLAYER ? PFirstTurnDone : EFirstTurnDone;

        public void SetFirstTurnDone(string owner, bool value)
        {
            if (owner == GameRules.OWNER_PLAYER) PFirstTurnDone = value;
            else EFirstTurnDone = value;
        }

        public string Opponent(string owner) =>
            owner == GameRules.OWNER_PLAYER ? GameRules.OWNER_ENEMY : GameRules.OWNER_PLAYER;
    }
}
