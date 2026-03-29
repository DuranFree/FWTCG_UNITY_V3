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

        // Phase transition delay in ms (used in async turn flow)
        public const int PHASE_DELAY_MS = 650;
        public const int AI_ACTION_DELAY_MS = 700;

        // Battlefield pools per faction (each player randomly picks 1 from their pool)
        // Format: display name only; special abilities are implemented in DEV-3+
        public static readonly string[] KAISA_BF_POOL =
        {
            "虚空之门", "星峰", "雷文符石"
        };

        public static readonly string[] YI_BF_POOL =
        {
            "磨难高阶", "力量碑文", "忘却纪念碑"
        };

        /// <summary>
        /// Picks a random battlefield name from a faction pool.
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
            // Kaisa deck (19 total)
            { "noxus_recruit",      2 },
            { "alert_sentinel",     3 },
            { "yordel_instructor",  3 },
            { "bad_poro",           2 },
            { "rengar",             2 },
            { "kaisa_hero",         1 },
            { "darius",             1 },
            { "thousand_tail",      3 },
            { "foresight_mech",     2 },
            // Yi deck (units: 11 + equipment: 8 = 19)
            { "yi_hero",            1 },
            { "jax",                2 },
            { "tiyana_warden",      2 },
            { "wailing_poro",       3 },
            { "sandshoal_deserter", 2 },
            { "zhonya",             1 },
            { "trinity_force",      2 },
            { "guardian_angel",     2 },
            { "dorans_blade",       3 },
            // ── Kaisa spells (+11 cards → 30 total) ──────────────────────────
            { "hex_ray",            3 },
            { "void_seek",          2 },
            { "stardrop",           2 },
            { "starburst",          2 },
            { "evolve_day",         1 },
            { "akasi_storm",        1 },
            // ── Yi spells (+7 cards → 26 total) ──────────────────────────────
            { "slam",               2 },
            { "rally_call",         2 },
            { "strike_ask_later",   2 },
            { "balance_resolve",    1 },
        };

        /// <summary>Returns how many copies of a card appear in its deck.</summary>
        public static int GetCardCopies(string cardId)
        {
            return CardCopies.TryGetValue(cardId, out int n) ? n : 1;
        }
    }
}
