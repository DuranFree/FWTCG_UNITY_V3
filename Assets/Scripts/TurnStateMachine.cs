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
        /// DEV-32 A5: 便利查询 — "正在结算中（closed loop）" — 不接受玩家输入期。
        /// </summary>
        public static bool IsResolving =>
            _current == State.SpellDuel_ClosedLoop || _current == State.Normal_ClosedLoop;

        /// <summary>
        /// Returns true when a spell card is legal to play given the current state.
        /// 按 Rule 18/25 修正后的权限判定：
        ///
        /// Normal_OpenLoop      : 任意法术合法（自己回合开环，基础权限）。
        /// SpellDuel_OpenLoop   : 只有反应 OR 迅捷（Rule 18.1.b / Rule 25）。
        /// SpellDuel_ClosedLoop : 只有反应插队（Rule 25.1.c 闭环）。
        /// Normal_ClosedLoop    : 只有反应（非本回合 / 结算中，Rule 25.1.c）。
        /// </summary>
        public static bool CanPlaySpell(CardData card)
        {
            if (card == null || !card.IsSpell) return false;

            bool hasReactive = card.HasKeyword(CardKeyword.Reactive);
            bool hasSwift    = card.HasKeyword(CardKeyword.Swift);

            switch (_current)
            {
                case State.Normal_OpenLoop:
                    // 自己回合开环：所有法术均可打（反应/迅捷属于额外权限，不是限制）
                    return true;
                case State.SpellDuel_OpenLoop:
                    // 对决开环：只有反应或迅捷（Rule 18.1.b）
                    return hasReactive || hasSwift;
                case State.SpellDuel_ClosedLoop:
                case State.Normal_ClosedLoop:
                    // 闭环：只有反应能插队结算链（Rule 25.1.c）
                    return hasReactive;
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
