namespace FWTCG.Core
{
    /// <summary>
    /// All game constants for FWTCG. Single source of truth.
    /// </summary>
    public static class GameRules
    {
        // Victory
        public const int WIN_SCORE = 8;

        // Hand — no hand size cap
        public const int INITIAL_HAND_SIZE = 4;

        // Battlefield — no unit cap per side
        public const int BATTLEFIELD_COUNT = 2;

        // Turn
        public const int TURN_TIMER_SECONDS = 30;

        // Rune deck composition
        public const int RUNE_DECK_BLAZING = 7;
        public const int RUNE_DECK_RADIANT = 5;
        public const int RUNE_DECK_VERDANT = 6;
        public const int RUNE_DECK_CRUSHING = 6;

        // Rune zone cap
        public const int MAX_RUNES_IN_PLAY = 12;

        // Rune draw per turn
        public const int RUNES_PER_TURN = 2;
        public const int RUNES_FIRST_TURN_SECOND = 3; // second player draws 3 on their first turn

        // Phase names
        public const string PHASE_AWAKEN = "awaken";
        public const string PHASE_START = "start";
        public const string PHASE_SUMMON = "summon";
        public const string PHASE_DRAW = "draw";
        public const string PHASE_ACTION = "action";
        public const string PHASE_END = "end";

        // Owners
        public const string OWNER_PLAYER = "player";
        public const string OWNER_ENEMY = "enemy";

        // Score types (used for logging / last-point rule)
        public const string SCORE_TYPE_HOLD = "hold";
        public const string SCORE_TYPE_CONQUER = "conquer";
        public const string SCORE_TYPE_BURNOUT = "burnout";

        // Legend
        public const int LEGEND_EVOLUTION_KEYWORDS = 4; // distinct keywords needed for Kaisa to evolve

        // Phase transition delay in ms (used in async turn flow)
        public const int PHASE_DELAY_MS = 650;
        public const int AI_ACTION_DELAY_MS = 700;

        // Strong power threshold for reckoner_arena keyword grant
        public const int STRONG_POWER_THRESHOLD = 5;

        // Battlefield pools per faction (each player randomly picks 1 from their pool)
        // Format: card ID (matches BattlefieldSystem effect switch cases)
        public static readonly string[] KAISA_BF_POOL =
        {
            "star_peak", "void_gate", "strength_obelisk"
        };

        public static readonly string[] YI_BF_POOL =
        {
            "thunder_rune", "ascending_stairs", "forgotten_monument"
        };

        // Display names for all 19 battlefield cards
        public static readonly System.Collections.Generic.Dictionary<string, string> BF_DISPLAY_NAMES =
            new System.Collections.Generic.Dictionary<string, string>
        {
            { "altar_unity",        "团结祭坛" },
            { "aspirant_climb",     "试炼者之阶" },
            { "back_alley_bar",     "暗巷酒吧" },
            { "bandle_tree",        "班德尔城神树" },
            { "hirana",             "希拉娜修道院" },
            { "reaver_row",         "掠夺者之街" },
            { "reckoner_arena",     "清算人竞技场" },
            { "dreaming_tree",      "梦幻树" },
            { "vile_throat_nest",   "卑鄙之喉的巢穴" },
            { "rockfall_path",      "落岩之径" },
            { "sunken_temple",      "沉没神庙" },
            { "trifarian_warcamp",  "崔法利战营" },
            { "void_gate",          "虚空之门" },
            { "zaun_undercity",     "祖安地沟" },
            { "strength_obelisk",   "力量方尖碑" },
            { "star_peak",          "星尖峰" },
            { "thunder_rune",       "雷霆之纹" },
            { "ascending_stairs",   "攀圣长阶" },
            { "forgotten_monument", "遗忘丰碑" },
        };

        public static readonly System.Collections.Generic.Dictionary<string, string> BF_DESCRIPTIONS =
            new System.Collections.Generic.Dictionary<string, string>
        {
            { "altar_unity",        "【据守】在基地召唤1/1新兵" },
            { "aspirant_climb",     "【据守】支付1法力，基地单位+1战力" },
            { "back_alley_bar",     "【被动】移动离开时+1战力" },
            { "bandle_tree",        "【据守】场上≥3种特性+1法力" },
            { "hirana",             "【征服】消耗增益指示物抽1牌" },
            { "reaver_row",         "【征服】从废牌堆捞费用≤2单位" },
            { "reckoner_arena",     "【被动】战力≥5自动获得强攻/坚守" },
            { "dreaming_tree",      "【被动】每回合首次法术抽1牌" },
            { "vile_throat_nest",   "【限制】此处单位禁止撤回基地" },
            { "rockfall_path",      "【限制】禁止直接出牌到此战场" },
            { "sunken_temple",      "【防守失败】支付2法力抽1牌" },
            { "trifarian_warcamp",  "【入场】获得增益指示物" },
            { "void_gate",          "【被动】法术伤害额外+1" },
            { "zaun_undercity",     "【征服】弃1牌抽1牌" },
            { "strength_obelisk",   "【据守】额外召出1张符文" },
            { "star_peak",          "【据守】召出1枚休眠符文" },
            { "thunder_rune",       "【征服】回收1张符文" },
            { "ascending_stairs",   "【被动】使赢得游戏所需的分数+1" },
            { "forgotten_monument", "【被动】第三回合前无据守分" },
        };

        /// <summary>Returns the Chinese display name for a battlefield card ID.</summary>
        public static string GetBattlefieldDisplayName(string id)
        {
            return BF_DISPLAY_NAMES.TryGetValue(id, out string name) ? name : id;
        }

        public static string GetBattlefieldDescription(string id)
        {
            return BF_DESCRIPTIONS.TryGetValue(id, out string desc) ? desc : "";
        }

        /// <summary>
        /// Picks a random battlefield card ID from a faction pool.
        /// </summary>
        public static string PickBattlefield(string[] pool)
        {
            int idx = UnityEngine.Random.Range(0, pool.Length);
            return pool[idx];
        }

        // Card copy counts per id (BuildDeck uses this)
        private static readonly System.Collections.Generic.Dictionary<string, int> CardCopies =
            new System.Collections.Generic.Dictionary<string, int>
        {
            // ── Kaisa deck: 19 units + 21 spells = 40 ────────────────────────
            { "noxus_recruit",      2 },
            { "alert_sentinel",     3 },
            { "yordel_instructor",  3 },
            { "bad_poro",           2 },
            { "rengar",             2 },
            { "kaisa_hero",         1 },
            { "darius",             1 },
            { "thousand_tail",      3 },
            { "foresight_mech",     2 },
            { "swindle",            3 },
            { "void_seek",          1 },
            { "evolve_day",         1 },
            { "retreat_rune",       2 },
            { "furnace_blast",      2 },
            { "guilty_pleasure",    1 },
            { "starburst",          1 },
            { "hex_ray",            2 },
            { "time_warp",          2 },
            { "stardrop",           3 },
            { "smoke_bomb",         1 },
            { "divine_ray",         1 },
            { "akasi_storm",        1 },
            // ── Yi deck: 18 units/equipment + 22 spells = 40 ─────────────────
            { "yi_hero",            1 },
            { "jax",                2 },
            { "tiyana_warden",      2 },
            { "wailing_poro",       3 },
            { "sandshoal_deserter", 2 },
            { "zhonya",             1 },
            { "trinity_force",      2 },
            { "guardian_angel",     2 },
            { "dorans_blade",       3 },
            { "slam",               2 },
            { "rally_call",         3 },
            { "strike_ask_later",   2 },
            { "balance_resolve",    3 },
            { "scoff",              3 },
            { "duel_stance",        2 },
            { "well_trained",       3 },
            { "wind_wall",          2 },
            { "flash_counter",      2 },
        };

        /// <summary>Returns how many copies of a card appear in its deck.</summary>
        public static int GetCardCopies(string cardId)
        {
            return CardCopies.TryGetValue(cardId, out int n) ? n : 1;
        }

        /// <summary>
        /// 返回法术在当前语境下的实际费用（考虑卡面条件性减费）。
        /// balance_resolve 卡面："如果对手得分或胜利得分不超过3分，则此法术的费用将减少2。"
        /// 解读：对手当前得分 ≤3，或对手距离胜利（WIN_SCORE - oppScore）≤3 时，自身费用 -2。
        /// </summary>
        public static int GetSpellEffectiveCost(FWTCG.Core.UnitInstance spell, string caster, FWTCG.Core.GameState gs)
        {
            int cost = spell.CardData.Cost;
            if (spell.CardData.EffectId == "balance_resolve" && gs != null)
            {
                string opp = gs.Opponent(caster);
                int oppScore = gs.GetScore(opp);
                int toWin = WIN_SCORE - oppScore;
                if (oppScore <= 3 || toWin <= 3)
                    cost -= 2;
            }
            return cost < 0 ? 0 : cost;
        }
    }
}
