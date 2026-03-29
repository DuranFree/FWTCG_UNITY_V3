using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;
using FWTCG.UI;

namespace FWTCG.AI
{
    /// <summary>
    /// Simple AI for DEV-1/DEV-4. Each turn it:
    ///  1. Taps all untapped runes for mana.
    ///  2. Plays the first affordable non-spell unit card from hand to base.
    ///  3. (DEV-4) Plays all affordable non-reactive spell cards (with auto-targeting).
    ///     Each AI spell opens a reaction window for the player to respond.
    ///  4. Moves all non-exhausted base units to a battlefield (batch move).
    ///  5. Calls EndTurn().
    /// </summary>
    public class SimpleAI : MonoBehaviour
    {
        public async Task TakeAction(GameState gs, TurnManager turnMgr,
                                     CombatSystem combat, ScoreManager score,
                                     EntryEffectSystem entryEffects = null,
                                     SpellSystem spellSys = null,
                                     ReactiveSystem reactiveSys = null,
                                     ReactiveWindowUI reactiveWindow = null)
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

            // ── Step 2: Play first affordable non-spell unit from hand ─────────
            {
                List<UnitInstance> hand = gs.GetHand(GameRules.OWNER_ENEMY);
                UnitInstance toPlay = null;

                foreach (UnitInstance u in hand)
                {
                    if (!u.CardData.IsSpell && u.CardData.Cost <= gs.EMana)
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

                    entryEffects?.OnUnitEntered(toPlay, GameRules.OWNER_ENEMY, gs);
                }
                else
                {
                    TurnManager.BroadcastMessage_Static("[AI] 无法打出任何单位（法力不足或无单位牌）");
                }
            }

            await Task.Delay(GameRules.AI_ACTION_DELAY_MS);
            if (gs.GameOver) return;

            // ── Step 3 (DEV-4): Play affordable non-reactive spells ────────────
            if (spellSys != null)
            {
                bool foundSpell = true;
                while (foundSpell && !gs.GameOver)
                {
                    foundSpell = false;
                    var hand = gs.GetHand(GameRules.OWNER_ENEMY);

                    foreach (UnitInstance card in new List<UnitInstance>(hand))
                    {
                        // Only non-reactive spells that are affordable
                        if (!card.CardData.IsSpell) continue;
                        if (card.CardData.HasKeyword(CardKeyword.Reactive)) continue;
                        if (card.CardData.Cost > gs.EMana) continue;

                        // Auto-select target based on SpellTargetType
                        UnitInstance target = null;
                        if (card.CardData.SpellTargetType == SpellTargetType.EnemyUnit)
                        {
                            // "Enemy" from AI's perspective = player units
                            target = GetFirstPlayerUnit(gs);
                            if (target == null) continue; // no target available, skip
                        }
                        else if (card.CardData.SpellTargetType == SpellTargetType.FriendlyUnit)
                        {
                            // "Friendly" from AI's perspective = AI units
                            target = GetFirstAIUnit(gs);
                            if (target == null) continue;
                        }
                        // SpellTargetType.None: target stays null

                        // Deduct mana
                        gs.EMana -= card.CardData.Cost;
                        gs.CardsPlayedThisTurn++;
                        TurnManager.BroadcastMessage_Static(
                            $"[AI] 发动法术 {card.UnitName}（费用{card.CardData.Cost}），剩余法力 {gs.EMana}");

                        // Open reaction window for player if applicable
                        bool negated = false;
                        if (reactiveWindow != null && reactiveWindow != null)
                        {
                            var playerReactives = GetReactiveCards(gs.GetHand(GameRules.OWNER_PLAYER));
                            if (playerReactives.Count > 0)
                            {
                                var reaction = await reactiveWindow.WaitForReaction(
                                    playerReactives, card.UnitName, gs);

                                if (reaction != null && reactiveSys != null)
                                {
                                    negated = reactiveSys.ApplyReactive(
                                        reaction, GameRules.OWNER_PLAYER, card, gs);
                                }
                            }
                        }

                        if (!negated)
                        {
                            // CastSpell handles hand→discard move internally
                            spellSys.CastSpell(card, GameRules.OWNER_ENEMY, target, gs);
                        }
                        else
                        {
                            // Negated: move spell directly to discard without effect
                            gs.GetHand(GameRules.OWNER_ENEMY).Remove(card);
                            gs.GetDiscard(GameRules.OWNER_ENEMY).Add(card);
                            TurnManager.BroadcastMessage_Static(
                                $"[AI] {card.UnitName} 被玩家反应无效化，丢入废牌堆");
                        }

                        foundSpell = true;
                        await Task.Delay(GameRules.AI_ACTION_DELAY_MS);
                        if (gs.GameOver) { turnMgr.EndTurn(); return; }
                        break; // Restart the while loop to re-evaluate hand state
                    }
                }
            }

            if (gs.GameOver) { turnMgr.EndTurn(); return; }

            // ── Step 4: Batch move ALL non-exhausted base units to one BF ─────
            int targetBF = ChooseBattlefield(gs);
            if (targetBF >= 0)
            {
                // Collect all movable units first (avoid modifying list while iterating)
                List<UnitInstance> toMoveList = new List<UnitInstance>();
                foreach (UnitInstance u in gs.GetBase(GameRules.OWNER_ENEMY))
                {
                    if (!u.Exhausted) toMoveList.Add(u);
                }

                if (toMoveList.Count > 0)
                {
                    foreach (UnitInstance u in toMoveList)
                    {
                        TurnManager.BroadcastMessage_Static(
                            $"[AI] 移动 {u.UnitName} → 战场{targetBF + 1}");
                        combat.MoveUnit(u, "base", targetBF, GameRules.OWNER_ENEMY, gs);
                    }

                    await Task.Delay(GameRules.AI_ACTION_DELAY_MS);
                    if (gs.GameOver) { turnMgr.EndTurn(); return; }

                    // After all units moved, resolve combat on that BF
                    combat.CheckAndResolveCombat(targetBF, GameRules.OWNER_ENEMY, gs, score);
                }
                else
                {
                    TurnManager.BroadcastMessage_Static("[AI] 基地无可移动的单位");
                }
            }
            else
            {
                TurnManager.BroadcastMessage_Static("[AI] 无可用战场");
            }

            await Task.Delay(GameRules.AI_ACTION_DELAY_MS);

            // ── Step 5: End turn ───────────────────────────────────────────────
            TurnManager.BroadcastMessage_Static("[AI] 结束回合");
            turnMgr.EndTurn();
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns player units for AI auto-targeting ("enemy" from AI's perspective).
        /// Priority: base, then BF0, BF1.
        /// </summary>
        private static UnitInstance GetFirstPlayerUnit(GameState gs)
        {
            if (gs.PBase.Count > 0) return gs.PBase[0];
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                if (gs.BF[i].PlayerUnits.Count > 0) return gs.BF[i].PlayerUnits[0];
            return null;
        }

        /// <summary>
        /// Returns AI units for AI auto-targeting ("friendly" from AI's perspective).
        /// Priority: base, then BF0, BF1.
        /// </summary>
        private static UnitInstance GetFirstAIUnit(GameState gs)
        {
            if (gs.EBase.Count > 0) return gs.EBase[0];
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                if (gs.BF[i].EnemyUnits.Count > 0) return gs.BF[i].EnemyUnits[0];
            return null;
        }

        /// <summary>
        /// Collects all reactive spells from a hand.
        /// </summary>
        private static List<UnitInstance> GetReactiveCards(List<UnitInstance> hand)
        {
            var result = new List<UnitInstance>();
            foreach (var c in hand)
            {
                if (c.CardData.IsSpell && c.CardData.HasKeyword(CardKeyword.Reactive))
                    result.Add(c);
            }
            return result;
        }

        /// <summary>
        /// Choose the best battlefield index for the AI to move to.
        /// Priority: battlefield with player units (to attack).
        /// Fallback: any battlefield.
        /// Returns -1 if no suitable battlefield found.
        /// </summary>
        private int ChooseBattlefield(GameState gs)
        {
            // First preference: battlefield with player units (triggers combat)
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                BattlefieldState bf = gs.BF[i];
                if (bf.HasUnits(GameRules.OWNER_PLAYER) && bf.HasSlot(GameRules.OWNER_ENEMY))
                    return i;
            }

            // Second preference: empty battlefield to claim territory
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
