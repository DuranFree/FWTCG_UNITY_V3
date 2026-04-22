using System;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;

namespace FWTCG.Systems
{
    /// <summary>
    /// Handles all scoring logic including the last-point rule.
    ///
    /// Last-point rule (#2): To earn the winning point via conquest,
    /// the player must have conquered ALL battlefields (2) in the current turn.
    /// Conquering only 1 battlefield denies the point and awards 1 card draw instead.
    /// Hold and burnout scores bypass this restriction.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static event Action<string> OnGameOver;
        public static event Action<string> OnScoreChanged;
        /// <summary>Fired after a score is successfully added. (owner, newScore) — DEV-19 score pulse.</summary>
        public static event Action<string, int> OnScoreAdded;

        [SerializeField] private BattlefieldSystem _bfSys;

        /// <summary>
        /// Attempts to add points to a player. Applies the last-point restriction.
        /// Returns true if the point(s) were actually awarded.
        /// </summary>
        public bool AddScore(string who, int pts, string type, int? bfId, GameState gs)
        {
            if (gs.GameOver) return false;

            // forgotten_monument: block hold score before round 2
            if (type == GameRules.SCORE_TYPE_HOLD && bfId.HasValue && _bfSys != null)
            {
                if (_bfSys.ShouldBlockHoldScore(bfId.Value, gs))
                    return false;
            }

            // ascending_stairs: +1 bonus on hold or conquer from this BF
            if (bfId.HasValue && _bfSys != null)
                pts += _bfSys.GetBonusScorePoints(bfId.Value, type, gs);

            // C-7 trinity_force: attached unit adds +1 when it scores a hold
            if (type == GameRules.SCORE_TYPE_HOLD && bfId.HasValue)
            {
                pts += ComputeTrinityForceBonus(who, bfId.Value, gs);
            }

            // Tiyana passive: opponent can't gain hold score while Tiyana is in play
            if (type == GameRules.SCORE_TYPE_HOLD)
            {
                string opponent = gs.Opponent(who);
                if (gs.TiyanasInPlay.TryGetValue(opponent, out bool active) && active)
                {
                    TurnManager.BroadcastMessage_Static(
                        $"[蒂亚娜] {DisplayName(who)} 的据守分被阻止（对手有蒂亚娜守卫在场）");
                    return false;
                }
            }

            // #2: Last-point rule for conquest scoring — 胜利门槛支持攀圣长阶 +1（B-SCORE-1）
            int winScore = BattlefieldSystem.EffectiveWinScore(gs);
            if (type == GameRules.SCORE_TYPE_CONQUER)
            {
                int currentScore = gs.GetScore(who);
                if (currentScore + pts >= winScore)
                {
                    // Check if ALL battlefields have been conquered this turn
                    HashSet<int> conqueredBFs = new HashSet<int>(gs.BFConqueredThisTurn);
                    if (bfId.HasValue) conqueredBFs.Add(bfId.Value);

                    if (conqueredBFs.Count < GameRules.BATTLEFIELD_COUNT)
                    {
                        // Deny winning point, draw 1 card instead
                        TurnManager.BroadcastMessage_Static(
                            $"[最后一分] {DisplayName(who)} 未征服所有战场，得分无效，改为抽1张牌");

                        // Still track that this BF was conquered
                        if (bfId.HasValue && !gs.BFConqueredThisTurn.Contains(bfId.Value))
                            gs.BFConqueredThisTurn.Add(bfId.Value);

                        DrawCardAsReward(who, gs);
                        return false;
                    }
                    // All BFs conquered — allow winning point through
                }
            }

            // Award the score
            gs.AddScore(who, pts);
            OnScoreAdded?.Invoke(who, gs.GetScore(who)); // DEV-19

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

            string msg = $"[得分] {DisplayName(who)} +{pts} ({type}) → {gs.GetScore(who)}/{winScore}";
            TurnManager.BroadcastMessage_Static(msg);
            OnScoreChanged?.Invoke(msg);

            // DEV-18b: fire score float text only.
            // Conquer banner removed (result already shown in CombatResultPanel).
            // Hold banner kept — fires at end-of-turn, not during combat, so no overlap.
            FWTCG.UI.GameEventBus.FireScoreFloat(who, pts);
            if (type == GameRules.SCORE_TYPE_HOLD)
                FWTCG.UI.GameEventBus.FireHoldScoreBanner();
            if (type == GameRules.SCORE_TYPE_CONQUER)
                FWTCG.UI.GameEventBus.FireConquestScored(who); // DEV-30 F1: conquest VFX

            CheckWin(gs);
            return true;
        }

        /// <summary>
        /// Checks if WIN_SCORE has been reached and fires OnGameOver if so.
        /// </summary>
        public void CheckWin(GameState gs)
        {
            if (gs.GameOver) return;

            // B-SCORE-1: 攀圣长阶 → 胜利分数门槛 +1
            int winScore = BattlefieldSystem.EffectiveWinScore(gs);

            string winner = null;
            if (gs.PScore >= winScore) winner = GameRules.OWNER_PLAYER;
            else if (gs.EScore >= winScore) winner = GameRules.OWNER_ENEMY;

            if (winner != null)
            {
                gs.GameOver = true;
                string msg = winner == GameRules.OWNER_PLAYER
                    ? $"玩家获胜！({gs.PScore}/{winScore})"
                    : $"AI获胜！({gs.EScore}/{winScore})";

                OnGameOver?.Invoke(msg);
            }
        }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Draws 1 card for the last-point denial reward.
        /// If deck is empty, performs burnout (shuffle discard → draw, opponent +1).
        /// </summary>
        private void DrawCardAsReward(string who, GameState gs)
        {
            List<UnitInstance> deck = gs.GetDeck(who);
            List<UnitInstance> hand = gs.GetHand(who);
            List<UnitInstance> discard = gs.GetDiscard(who);

            if (deck.Count == 0 && discard.Count > 0)
            {
                // Burnout: shuffle discard into deck
                ShuffleDiscard(deck, discard);
                string opponent = gs.Opponent(who);
                TurnManager.BroadcastMessage_Static($"[燃尽] {DisplayName(who)} 牌库耗尽，洗牌！对手 +1分");
                AddScore(opponent, 1, GameRules.SCORE_TYPE_BURNOUT, null, gs);
            }

            if (deck.Count > 0)
            {
                UnitInstance drawn = deck[0];
                deck.RemoveAt(0);
                hand.Add(drawn);
                TurnManager.BroadcastMessage_Static($"[奖励] {DisplayName(who)} 抽到 {drawn.UnitName}");
            }
        }

        private void ShuffleDiscard(List<UnitInstance> deck, List<UnitInstance> discard)
        {
            for (int i = discard.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                UnitInstance temp = discard[i];
                discard[i] = discard[j];
                discard[j] = temp;
            }
            deck.AddRange(discard);
            discard.Clear();
        }

        private string DisplayName(string owner) =>
            owner == GameRules.OWNER_PLAYER ? "玩家" : "AI";

        /// <summary>
        /// C-7: 三相之力 — "当我据守一处战场时，获得的分数+1"
        /// 每一个在 bfId 上的友方单位附着 trinity_force 装备，+1 分。
        /// </summary>
        private int ComputeTrinityForceBonus(string owner, int bfId, GameState gs)
        {
            var units = owner == GameRules.OWNER_PLAYER
                ? gs.BF[bfId].PlayerUnits
                : gs.BF[bfId].EnemyUnits;
            int bonus = 0;
            foreach (var u in units)
            {
                if (u.AttachedEquipment != null &&
                    u.AttachedEquipment.CardData.EffectId == "trinity_equip")
                {
                    bonus += 1;
                    TurnManager.BroadcastMessage_Static(
                        $"[三相之力] {u.UnitName} 据守额外 +1 分");
                }
            }
            return bonus;
        }
    }
}
