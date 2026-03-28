using System;
using UnityEngine;
using FWTCG.Core;

namespace FWTCG.Systems
{
    /// <summary>
    /// Handles all scoring logic including the last-point restriction rule.
    ///
    /// Last-point rule: A player can only earn their 8th (WIN_SCORE) point via
    /// a "conquer" score type AND they must have conquered ALL battlefields
    /// during the current turn. Otherwise the point is denied and they draw 1
    /// card instead.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static event Action<string> OnGameOver;
        public static event Action<string> OnScoreChanged;

        /// <summary>
        /// Attempts to add points to a player. Applies the last-point restriction.
        /// Returns true if the point(s) were actually awarded.
        /// </summary>
        /// <param name="who">Owner string ("player" or "enemy")</param>
        /// <param name="pts">Points to add</param>
        /// <param name="type">Score type: "hold", "conquer", or "burnout"</param>
        /// <param name="bfId">Battlefield ID (null for non-battlefield scores)</param>
        /// <param name="gs">Current game state</param>
        public bool AddScore(string who, int pts, string type, int? bfId, GameState gs)
        {
            if (gs.GameOver) return false;

            int currentScore = gs.GetScore(who);

            // Check if this would reach or exceed WIN_SCORE
            if (currentScore + pts >= GameRules.WIN_SCORE)
            {
                // Last-point restriction: only "conquer" can award the final point,
                // and the player must have conquered ALL battlefields this turn.
                if (type != GameRules.SCORE_TYPE_CONQUER)
                {
                    // Not a conquer — draw a card instead
                    DrawCardInstead(who, gs);
                    TurnManager.BroadcastMessage_Static(
                        $"[最后1分限制] {DisplayName(who)} 的 {type} 得分被拒绝，改为抽1张牌");
                    return false;
                }

                // It is a conquer — check if ALL battlefields have been conquered this turn
                if (!AllBattlefieldsConqueredThisTurn(who, gs))
                {
                    // Not all battlefields conquered — deny and draw instead
                    DrawCardInstead(who, gs);
                    TurnManager.BroadcastMessage_Static(
                        $"[最后1分限制] {DisplayName(who)} 未征服所有战场，得分被拒绝，改为抽1张牌");
                    return false;
                }
            }

            // Award the score
            gs.AddScore(who, pts);

            // Track battlefield scoring
            if (bfId.HasValue)
            {
                if (type == GameRules.SCORE_TYPE_HOLD && !gs.BFScoredThisTurn.Contains(bfId.Value))
                {
                    gs.BFScoredThisTurn.Add(bfId.Value);
                }
                if (type == GameRules.SCORE_TYPE_CONQUER && !gs.BFConqueredThisTurn.Contains(bfId.Value))
                {
                    gs.BFConqueredThisTurn.Add(bfId.Value);
                }
            }

            string msg = $"[得分] {DisplayName(who)} +{pts} ({type}) → {gs.GetScore(who)}/{GameRules.WIN_SCORE}";
            TurnManager.BroadcastMessage_Static(msg);
            OnScoreChanged?.Invoke(msg);

            CheckWin(gs);
            return true;
        }

        /// <summary>
        /// Checks if WIN_SCORE has been reached and fires OnGameOver if so.
        /// </summary>
        public void CheckWin(GameState gs)
        {
            if (gs.GameOver) return;

            string winner = null;
            if (gs.PScore >= GameRules.WIN_SCORE) winner = GameRules.OWNER_PLAYER;
            else if (gs.EScore >= GameRules.WIN_SCORE) winner = GameRules.OWNER_ENEMY;

            if (winner != null)
            {
                gs.GameOver = true;
                string msg = winner == GameRules.OWNER_PLAYER
                    ? $"玩家获胜！({gs.PScore}/{GameRules.WIN_SCORE})"
                    : $"AI获胜！({gs.EScore}/{GameRules.WIN_SCORE})";

                OnGameOver?.Invoke(msg);
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the player has conquered every battlefield this turn
        /// (i.e., all bf IDs appear in BFConqueredThisTurn).
        /// </summary>
        private bool AllBattlefieldsConqueredThisTurn(string who, GameState gs)
        {
            // The conquering player must have entries for ALL battlefields
            // Count how many BFs the 'who' player controls after conquering
            int conqueredCount = 0;
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                if (gs.BFConqueredThisTurn.Contains(i))
                {
                    conqueredCount++;
                }
            }
            return conqueredCount >= GameRules.BATTLEFIELD_COUNT;
        }

        /// <summary>
        /// Draws one card for a player (used as compensation for denied last-point).
        /// </summary>
        private void DrawCardInstead(string who, GameState gs)
        {
            var deck = gs.GetDeck(who);
            var hand = gs.GetHand(who);

            if (deck.Count == 0)
            {
                // Deck empty — no card to draw
                TurnManager.BroadcastMessage_Static($"[抽牌] {DisplayName(who)} 牌库为空，无法抽牌");
                return;
            }

            if (hand.Count >= GameRules.MAX_HAND_SIZE)
            {
                TurnManager.BroadcastMessage_Static($"[抽牌] {DisplayName(who)} 手牌已满，抽牌被废弃");
                deck.RemoveAt(0); // Burn the card
                return;
            }

            UnitInstance drawn = deck[0];
            deck.RemoveAt(0);
            hand.Add(drawn);
            TurnManager.BroadcastMessage_Static($"[抽牌] {DisplayName(who)} 抽到 {drawn.UnitName}（最后1分补偿）");
        }

        private string DisplayName(string owner) =>
            owner == GameRules.OWNER_PLAYER ? "玩家" : "AI";
    }
}
