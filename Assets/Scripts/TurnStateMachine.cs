using System;
using FWTCG.Data;

namespace FWTCG
{
    /// <summary>
    /// 4-state turn state machine (DEV-27).
    ///
    /// Tracks which actions are legal at any given moment, enabling
    /// correct implementation of Rule 718 (Swift timing) and Rule 725
    /// (Reactive closed-loop permission).
    ///
    /// States:
    ///   Normal_ClosedLoop    — Default; AI turn / between phases / resolving.
    ///   Normal_OpenLoop      — Player's Action phase; normal cards are playable.
    ///   SpellDuel_OpenLoop   — Reaction window open; Reactive + Swift spells are legal.
    ///   SpellDuel_ClosedLoop — Spell duel resolving; no inputs accepted.
    /// </summary>
    public static class TurnStateMachine
    {
        public enum State
        {
            Normal_ClosedLoop,       // Default/inactive — no player action allowed
            Normal_OpenLoop,         // Player Action phase — normal play
            SpellDuel_OpenLoop,      // Reaction window — Reactive + Swift spells (Rule 718)
            SpellDuel_ClosedLoop,    // Resolving — no inputs
        }

        private static State _current = State.Normal_ClosedLoop;

        /// <summary>Current state of the turn state machine.</summary>
        public static State Current => _current;

        /// <summary>
        /// Fired when the state changes: (previousState, newState).
        /// Subscribers can use this to update UI (e.g. highlight React button).
        /// </summary>
        public static event Action<State, State> OnStateChanged;

        /// <summary>
        /// Transition to the given state. No-op if already in that state.
        /// </summary>
        public static void TransitionTo(State next)
        {
            if (_current == next) return;
            State prev = _current;
            _current = next;
            OnStateChanged?.Invoke(prev, next);
            UnityEngine.Debug.Log($"[TurnState] {prev} → {next}");
        }

        // ── Convenience queries ──────────────────────────────────────────────

        /// <summary>True when the player can take normal actions (play cards, move units).</summary>
        public static bool IsPlayerActionPhase => _current == State.Normal_OpenLoop;

        /// <summary>
        /// True when the spell duel reaction window is open.
        /// During this state both Reactive and Swift spells are playable (Rule 718).
        /// </summary>
        public static bool IsSpellDuelOpen => _current == State.SpellDuel_OpenLoop;

        /// <summary>
        /// Returns true when a spell card is legal to play given the current state.
        ///
        /// Normal_OpenLoop     : non-Reactive, non-Swift spells only.
        /// SpellDuel_OpenLoop  : Reactive OR Swift spells only (Rule 718).
        /// All other states    : no spells allowed.
        /// </summary>
        public static bool CanPlaySpell(CardData card)
        {
            if (card == null || !card.IsSpell) return false;

            bool hasReactive = card.HasKeyword(CardKeyword.Reactive);
            bool hasSwift    = card.HasKeyword(CardKeyword.Swift);

            switch (_current)
            {
                case State.Normal_OpenLoop:
                    // Normal play: only non-reactive, non-swift spells
                    return !hasReactive && !hasSwift;
                case State.SpellDuel_OpenLoop:
                    // Duel window: Reactive OR Swift (Rule 718)
                    return hasReactive || hasSwift;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Reset to the default closed state. Call on game load / scene reload.
        /// </summary>
        public static void Reset() => TransitionTo(State.Normal_ClosedLoop);
    }
}
