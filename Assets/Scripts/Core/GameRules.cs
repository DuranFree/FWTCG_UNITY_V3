namespace FWTCG.Core
{
    /// <summary>
    /// All game constants for FWTCG. Single source of truth.
    /// </summary>
    public static class GameRules
    {
        // Victory
        public const int WIN_SCORE = 8;

        // Hand
        public const int MAX_HAND_SIZE = 7;
        public const int INITIAL_HAND_SIZE = 4;

        // Battlefield
        public const int MAX_BF_UNITS = 2;
        public const int BATTLEFIELD_COUNT = 2;

        // Turn
        public const int TURN_TIMER_SECONDS = 30;

        // Rune deck composition
        public const int RUNE_DECK_BLAZING = 7;
        public const int RUNE_DECK_RADIANT = 5;
        public const int RUNE_DECK_VERDANT = 6;
        public const int RUNE_DECK_CRUSHING = 6;

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
    }
}
