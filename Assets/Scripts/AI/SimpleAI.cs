using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Systems;

namespace FWTCG.AI
{
    /// <summary>
    /// Minimal AI for DEV-1. Each turn it:
    ///  1. Taps all untapped runes for mana.
    ///  2. Plays the first affordable unit card from hand to base.
    ///  3. Moves the first non-exhausted base unit to a battlefield
    ///     (prefers a battlefield that has enemy units).
    ///  4. Calls EndTurn().
    /// </summary>
    public class SimpleAI : MonoBehaviour
    {
        public async Task TakeAction(GameState gs, TurnManager turnMgr,
                                     CombatSystem combat, ScoreManager score)
        {
            if (gs.GameOver) return;

            // ── Step 1: Tap all untapped runes for mana ────────────────────────
            List<RuneInstance> runes = gs.GetRunes(GameRules.OWNER_ENEMY);
            int manaGained = 0;
            foreach (RuneInstance r in runes)
            {
                if (!r.Tapped)
                {
                    r.Tapped = true;
                    manaGained++;
                }
            }
            gs.AddMana(GameRules.OWNER_ENEMY, manaGained);
            TurnManager.BroadcastMessage_Static($"[AI] 横置 {manaGained} 张符文，法力 → {gs.EMana}");

            await Task.Delay(GameRules.AI_ACTION_DELAY_MS);
            if (gs.GameOver) return;

            // ── Step 2: Play first affordable unit from hand ───────────────────
            List<UnitInstance> hand = gs.GetHand(GameRules.OWNER_ENEMY);
            UnitInstance toPlay = null;

            foreach (UnitInstance u in hand)
            {
                if (u.CardData.Cost <= gs.EMana)
                {
                    toPlay = u;
                    break;
                }
            }

            if (toPlay != null)
            {
                hand.Remove(toPlay);
                gs.EBase.Add(toPlay);
                gs.EMana -= toPlay.CardData.Cost;
                toPlay.Exhausted = true;
                gs.CardsPlayedThisTurn++;
                TurnManager.BroadcastMessage_Static(
                    $"[AI] 打出 {toPlay.UnitName}（费用{toPlay.CardData.Cost}），剩余法力 {gs.EMana}");
            }
            else
            {
                TurnManager.BroadcastMessage_Static("[AI] 无法打出任何单位（法力不足）");
            }

            await Task.Delay(GameRules.AI_ACTION_DELAY_MS);
            if (gs.GameOver) return;

            // ── Step 3: Move first non-exhausted base unit to a battlefield ────
            List<UnitInstance> eBase = gs.GetBase(GameRules.OWNER_ENEMY);
            UnitInstance toMove = null;

            foreach (UnitInstance u in eBase)
            {
                if (!u.Exhausted)
                {
                    toMove = u;
                    break;
                }
            }

            if (toMove != null)
            {
                int targetBF = ChooseBattlefield(gs);
                if (targetBF >= 0)
                {
                    TurnManager.BroadcastMessage_Static(
                        $"[AI] 移动 {toMove.UnitName} → 战场{targetBF + 1}");
                    combat.MoveUnit(toMove, "base", targetBF, GameRules.OWNER_ENEMY, gs, score);
                }
                else
                {
                    TurnManager.BroadcastMessage_Static("[AI] 所有战场槽位已满，无法移动单位");
                }
            }
            else
            {
                TurnManager.BroadcastMessage_Static("[AI] 基地无可移动的单位");
            }

            await Task.Delay(GameRules.AI_ACTION_DELAY_MS);

            // ── Step 4: End turn ───────────────────────────────────────────────
            TurnManager.BroadcastMessage_Static("[AI] 结束回合");
            turnMgr.EndTurn();
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Choose the best battlefield index for the AI to move to.
        /// Priority: battlefield with player units (to attack) that has an open slot.
        /// Fallback: any battlefield with an open slot.
        /// Returns -1 if no suitable battlefield found.
        /// </summary>
        private int ChooseBattlefield(GameState gs)
        {
            // First preference: a battlefield that has player units (can trigger combat)
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                BattlefieldState bf = gs.BF[i];
                if (bf.HasUnits(GameRules.OWNER_PLAYER) && bf.HasSlot(GameRules.OWNER_ENEMY))
                    return i;
            }

            // Second preference: an empty battlefield to claim territory
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                BattlefieldState bf = gs.BF[i];
                if (bf.HasSlot(GameRules.OWNER_ENEMY))
                    return i;
            }

            return -1;
        }
    }
}
