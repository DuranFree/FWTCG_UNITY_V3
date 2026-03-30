using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;
using FWTCG.UI;
using FWTCG;

namespace FWTCG.AI
{
    /// <summary>
    /// DEV-7 Strategic AI — upgraded from DEV-4 SimpleAI.
    ///
    /// Turn flow:
    ///   1. Tap all runes for mana
    ///   2. Compute reactive mana reserve (don't spend below lowest reactive card cost)
    ///   3. Play rally_call early (before summoning units)
    ///   4. Play balance_resolve early (card draw + rune gain)
    ///   5. Summon units (sorted by value, try to reserve mana for reactives)
    ///   6. Play non-reactive spells (priority-sorted, smart targeting)
    ///   7. Use legend active ability when worthwhile
    ///   8. Move units to battlefields (scoring-based, supports split strategy)
    ///   9. End turn
    ///
    /// Board scoring is used for:
    ///   - Deciding whether to fight a losing battle (only when behind)
    ///   - Evaluating the split-field strategy
    ///   - Masteryi lone-defender passive awareness
    /// </summary>
    public class SimpleAI : MonoBehaviour
    {
        // How long (ms) to pause after announcing a spell, giving the player
        // a window to click the React button before the spell resolves.
        private const int SPELL_REACTION_WINDOW_MS = 2000;

        // ── Public entry point ─────────────────────────────────────────────────

        public async Task TakeAction(
            GameState gs, TurnManager turnMgr,
            CombatSystem combat, ScoreManager score,
            EntryEffectSystem entryEffects = null,
            SpellSystem spellSys = null,
            ReactiveSystem reactiveSys = null,
            ReactiveWindowUI reactiveWindow = null,
            LegendSystem legendSys = null,
            BattlefieldSystem bfSys = null)
        {
            if (gs.GameOver) return;

            // ── 1. Tap all untapped runes ──────────────────────────────────────
            int manaGained = 0;
            foreach (RuneInstance r in gs.GetRunes(GameRules.OWNER_ENEMY))
            {
                if (!r.Tapped) { r.Tapped = true; manaGained++; }
            }
            gs.AddMana(GameRules.OWNER_ENEMY, manaGained);
            Log($"[AI] 横置 {manaGained} 张符文，法力 → {gs.EMana}");
            await Delay(GameRules.AI_ACTION_DELAY_MS);
            if (gs.GameOver) return;

            // ── 2. Compute reactive mana reserve ──────────────────────────────
            int reactiveReserve = AiMinReactiveCost(gs);

            // ── 3. Play rally_call before summoning ────────────────────────────
            if (spellSys != null && !gs.GameOver)
            {
                UnitInstance rally = FindAffordableSpell("rally_call", gs);
                if (rally != null && AiShouldPlaySpell(rally, gs))
                    await CastAISpell(rally, null, gs, spellSys, turnMgr);
                if (gs.GameOver) { turnMgr.EndTurn(); return; }
                await Delay(GameRules.AI_ACTION_DELAY_MS);
            }

            // ── 4. Play balance_resolve early ──────────────────────────────────
            if (spellSys != null && !gs.GameOver)
            {
                UnitInstance balance = FindAffordableSpell("balance_resolve", gs);
                if (balance != null)
                    await CastAISpell(balance, null, gs, spellSys, turnMgr);
                if (gs.GameOver) { turnMgr.EndTurn(); return; }
                await Delay(GameRules.AI_ACTION_DELAY_MS);
            }

            // ── 5. Summon units (sorted by value) ─────────────────────────────
            while (!gs.GameOver)
            {
                // Try to play within mana reserve; fall back to best unit if needed
                int available = reactiveReserve > 0
                    ? Mathf.Max(0, gs.EMana - reactiveReserve)
                    : gs.EMana;

                var candidates = gs.GetHand(GameRules.OWNER_ENEMY)
                    .Where(c => !c.CardData.IsSpell && !c.CardData.IsEquipment && CanAfford(c.CardData, gs))
                    .OrderByDescending(c => AiCardValue(c.CardData))
                    .ToList();

                if (candidates.Count == 0) break;

                UnitInstance toPlay = candidates.FirstOrDefault(c => c.CardData.Cost <= available)
                                   ?? candidates[0];

                gs.GetHand(GameRules.OWNER_ENEMY).Remove(toPlay);
                SpendCost(toPlay.CardData, gs);
                gs.EBase.Add(toPlay);
                // Units with Haste keyword enter active (not exhausted)
                toPlay.Exhausted = !toPlay.CardData.HasKeyword(CardKeyword.Haste);
                gs.CardsPlayedThisTurn++;
                Log($"[AI] 出 {toPlay.UnitName}（费用{toPlay.CardData.Cost}，战力{toPlay.CurrentAtk}），剩余法力 {gs.EMana}");
                entryEffects?.OnUnitEntered(toPlay, GameRules.OWNER_ENEMY, gs);

                await Delay(GameRules.AI_ACTION_DELAY_MS);
                if (gs.GameOver) return;
            }

            // ── 6. Play non-reactive spells (priority-sorted) ─────────────────
            if (spellSys != null)
            {
                bool castAny = true;
                while (castAny && !gs.GameOver)
                {
                    castAny = false;

                    UnitInstance sp = gs.GetHand(GameRules.OWNER_ENEMY)
                        .Where(c => c.CardData.IsSpell
                                 && !c.CardData.HasKeyword(CardKeyword.Reactive)
                                 && c.CardData.EffectId != "rally_call"      // already handled
                                 && c.CardData.EffectId != "balance_resolve" // already handled
                                 && CanAfford(c.CardData, gs)
                                 && AiShouldPlaySpell(c, gs))
                        .OrderByDescending(c => AiSpellPriority(c))
                        .FirstOrDefault();

                    if (sp == null) break;

                    UnitInstance target = AiChooseSpellTarget(sp, gs);

                    // Skip if spell requires a target but none is available
                    if (sp.CardData.SpellTargetType == SpellTargetType.EnemyUnit   && target == null) break;
                    if (sp.CardData.SpellTargetType == SpellTargetType.FriendlyUnit && target == null) break;

                    await CastAISpell(sp, target, gs, spellSys, turnMgr);
                    castAny = true;

                    await Delay(GameRules.AI_ACTION_DELAY_MS);
                    if (gs.GameOver) { turnMgr.EndTurn(); return; }
                }
            }

            if (gs.GameOver) { turnMgr.EndTurn(); return; }

            // ── 7. Legend active ability ───────────────────────────────────────
            if (legendSys != null && gs.ELegend != null)
                AiUseLegendAbility(gs, legendSys);

            // ── 8. Movement loop ──────────────────────────────────────────────
            // Called repeatedly so split-field strategy works naturally:
            // first call sends 1 unit to BF0, second call sends another to BF1.
            while (!gs.GameOver)
            {
                var active = gs.GetBase(GameRules.OWNER_ENEMY)
                    .Where(u => !u.Exhausted && !u.Stunned)
                    .ToList();
                if (active.Count == 0) break;

                var plan = AiDecideMovement(active, gs, bfSys);
                if (!plan.HasValue) break;

                var (movers, targetBF) = plan.Value;
                foreach (UnitInstance u in movers)
                {
                    Log($"[AI] 移动 {u.UnitName} → 战场{targetBF + 1}");
                    combat.MoveUnit(u, "base", targetBF, GameRules.OWNER_ENEMY, gs);
                }

                await Delay(GameRules.AI_ACTION_DELAY_MS);
                if (gs.GameOver) { turnMgr.EndTurn(); return; }

                combat.CheckAndResolveCombat(targetBF, GameRules.OWNER_ENEMY, gs, score);

                await Delay(GameRules.AI_ACTION_DELAY_MS);
                if (gs.GameOver) { turnMgr.EndTurn(); return; }
            }

            await Delay(GameRules.AI_ACTION_DELAY_MS);

            // ── 9. End turn ────────────────────────────────────────────────────
            Log("[AI] 结束回合");
            turnMgr.EndTurn();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Board Evaluation ──────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Global board score. Positive = AI leading, negative = AI behind.
        /// Weights: score diff × 3, hand advantage × 0.5, BF control × 2, unit power × 0.3.
        /// Public for testing.
        /// </summary>
        public static int AiBoardScore(GameState gs)
        {
            int scoreDiff = gs.EScore - gs.PScore;
            int handDiff  = gs.GetHand(GameRules.OWNER_ENEMY).Count
                          - gs.GetHand(GameRules.OWNER_PLAYER).Count;
            int bfControl = 0;
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                string ctrl = gs.BF[i].Ctrl;
                if      (ctrl == GameRules.OWNER_ENEMY)  bfControl++;
                else if (ctrl == GameRules.OWNER_PLAYER) bfControl--;
            }
            int myPow  = GetAllAIUnits(gs).Sum(u => u.EffectiveAtk());
            int oppPow = GetAllPlayerUnits(gs).Sum(u => u.EffectiveAtk());
            return scoreDiff * 3 + handDiff / 2 + bfControl * 2 + (myPow - oppPow) / 3;
        }

        /// <summary>
        /// Card value score for prioritizing which unit to summon.
        /// Uses attack-efficiency plus keyword bonuses. Public for testing.
        /// </summary>
        public static float AiCardValue(CardData card)
        {
            float atk  = Mathf.Max(card.Atk, 0);
            float cost = Mathf.Max(card.Cost, 1);
            float val  = (atk / cost) * 10f;
            if (card.HasKeyword(CardKeyword.Haste))     val += 4f; // active on entry
            if (card.HasKeyword(CardKeyword.Barrier))   val += 3f;
            if (card.HasKeyword(CardKeyword.StrongAtk)) val += 2f;
            if (card.HasKeyword(CardKeyword.Deathwish)) val += 2f;
            if (card.HasKeyword(CardKeyword.Conquest))  val += 1f;
            if (card.HasKeyword(CardKeyword.Inspire))   val += 1f;
            return val;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Reactive Mana Reservation ─────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the lowest mana cost among reactive cards in AI hand.
        /// AI tries to keep at least this much mana available.
        /// </summary>
        public static int AiMinReactiveCost(GameState gs)
        {
            var reactives = gs.GetHand(GameRules.OWNER_ENEMY)
                .Where(c => c.CardData.IsSpell && c.CardData.HasKeyword(CardKeyword.Reactive))
                .ToList();
            return reactives.Count == 0 ? 0 : reactives.Min(c => c.CardData.Cost);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Spell Decision Logic ──────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true if the AI should actively play this spell this turn.
        /// Reactive spells are always excluded (only played via React button).
        /// </summary>
        public static bool AiShouldPlaySpell(UnitInstance spell, GameState gs)
        {
            if (spell.CardData.HasKeyword(CardKeyword.Reactive)) return false;

            switch (spell.CardData.EffectId)
            {
                case "slam":
                    // Only useful if there's a non-stunned enemy to stun
                    return GetAllPlayerUnits(gs).Any(u => !u.Stunned);

                case "strike_ask_later":
                    // Only useful if we have a unit to buff
                    return GetAllAIUnits(gs).Count > 0;

                case "void_seek":
                case "hex_ray":
                case "stardrop":
                case "starburst":
                case "akasi_storm":
                    // Only useful if enemy has units to damage
                    return GetAllPlayerUnits(gs).Count > 0;

                case "rally_call":
                    // Only useful if we have affordable units in hand to play afterward
                    {
                        int cost = spell.CardData.Cost;
                        return gs.GetHand(GameRules.OWNER_ENEMY)
                            .Any(c => !c.CardData.IsSpell && !c.CardData.IsEquipment
                                   && c.CardData.Cost <= gs.EMana - cost);
                    }

                default:
                    return true;
            }
        }

        /// <summary>
        /// Returns a higher number for spells that should be played first.
        /// </summary>
        public static int AiSpellPriority(UnitInstance spell)
        {
            switch (spell.CardData.EffectId)
            {
                case "rally_call":       return 100;
                case "balance_resolve":  return 90;
                case "slam":             return 80;
                case "strike_ask_later": return 70;
                case "starburst":        return 65;
                case "void_seek":        return 60;
                case "stardrop":         return 55;
                case "akasi_storm":      return 50;
                case "hex_ray":          return 45;
                case "evolve_day":       return 40;
                default:                 return 30;
            }
        }

        /// <summary>
        /// Selects the best target for a spell based on the spell's effect.
        /// Returns null for untargeted spells (SpellTargetType.None).
        /// </summary>
        public static UnitInstance AiChooseSpellTarget(UnitInstance spell, GameState gs)
        {
            switch (spell.CardData.SpellTargetType)
            {
                case SpellTargetType.EnemyUnit:
                {
                    var enemies = GetAllPlayerUnits(gs);
                    if (enemies.Count == 0) return null;

                    if (spell.CardData.EffectId == "slam")
                    {
                        // Stun: prefer BF units (they fight), pick highest ATK, unstunned
                        UnitInstance bfTarget = GetPlayerBFUnits(gs)
                            .Where(u => !u.Stunned)
                            .OrderByDescending(u => u.EffectiveAtk())
                            .FirstOrDefault();
                        if (bfTarget != null) return bfTarget;
                        return enemies.Where(u => !u.Stunned)
                            .OrderByDescending(u => u.EffectiveAtk()).FirstOrDefault();
                    }

                    // Default damage spells: hit highest effective ATK enemy
                    return enemies.OrderByDescending(u => u.EffectiveAtk()).First();
                }

                case SpellTargetType.FriendlyUnit:
                {
                    var allies = GetAllAIUnits(gs);
                    if (allies.Count == 0) return null;

                    // Prefer BF units for buffs (they're in active combat)
                    UnitInstance bfAlly = GetAIBFUnits(gs)
                        .OrderByDescending(u => u.EffectiveAtk()).FirstOrDefault();
                    return bfAlly ?? allies.OrderByDescending(u => u.EffectiveAtk()).First();
                }

                default:
                    return null; // SpellTargetType.None
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Movement Decision ─────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Evaluates all possible move plans and returns the best one.
        /// Each call returns ONE plan (one BF target + a subset of movers),
        /// allowing the movement loop to iterate for split-field strategies.
        /// Returns null if no move is worthwhile.
        /// </summary>
        public static (List<UnitInstance> movers, int bfIndex)?
            AiDecideMovement(List<UnitInstance> active, GameState gs, BattlefieldSystem bfSys = null)
        {
            // Sort by effective attack (strongest leads the charge)
            var sorted = active.OrderByDescending(u => u.EffectiveAtk()).ToList();
            int boardAdv = AiBoardScore(gs);
            int myScore  = gs.EScore;
            int oppScore = gs.PScore;

            List<UnitInstance> bestMovers = null;
            int bestBF    = -1;
            int bestScore = int.MinValue;

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                // Respect rockfall_path: AI can't move directly from base to this BF
                if (bfSys != null && !bfSys.CanPlayDirectlyToBattlefield(i, gs)) continue;

                BattlefieldState bf = gs.BF[i];
                int myCount    = bf.EnemyUnits.Count;
                int theirCount = bf.PlayerUnits.Count;
                int myBFPow    = bf.EnemyUnits.Sum(u => u.EffectiveAtk());
                int theirBFPow = bf.PlayerUnits.Sum(u => u.EffectiveAtk());
                string bfCard  = gs.BFNames != null && gs.BFNames.Length > i
                               ? gs.BFNames[i] : "";

                for (int count = 1; count <= sorted.Count; count++)
                {
                    var movers   = sorted.Take(count).ToList();
                    int movePow  = movers.Sum(u => u.EffectiveAtk());
                    int myTotal  = movePow + myBFPow;
                    bool willWin = myTotal > theirBFPow;
                    int margin   = myTotal - theirBFPow;

                    int planScore = 0;

                    if (theirCount == 0)
                    {
                        if (bf.Ctrl != GameRules.OWNER_ENEMY)
                        {
                            // Uncontrolled empty BF → easy conquest
                            planScore = 15;
                            if (bfCard == "ascending_stairs") planScore += 5;
                            if (count > 1) planScore -= 2; // overkill penalty
                        }
                        else
                        {
                            // Own controlled empty BF → reinforcing (low value)
                            planScore = 2;
                        }
                    }
                    else
                    {
                        if (willWin)
                        {
                            planScore = 12 + margin;
                            if (bf.Ctrl != GameRules.OWNER_ENEMY) planScore += 3;
                            if (bfCard == "ascending_stairs") planScore += 5;
                        }
                        else if (margin == 0)
                        {
                            // Tie: may be worth it if we're losing in score
                            planScore = 1;
                            if (bf.Ctrl == GameRules.OWNER_PLAYER && myScore < oppScore) planScore += 5;
                        }
                        else
                        {
                            // Will lose combat → only gamble when desperate
                            planScore = -3;
                            if (boardAdv < -3 || myScore - oppScore <= -3) planScore += 6;
                            if (boardAdv > 5) planScore -= 3; // conserve when winning
                            if (oppScore >= GameRules.WIN_SCORE - 2 && bf.Ctrl == GameRules.OWNER_PLAYER)
                                planScore += 8; // must contest when opponent near victory
                        }
                    }

                    // Urgency: push when close to winning or when opponent is close to winning
                    if (myScore >= GameRules.WIN_SCORE - 2) planScore += 3;
                    if (oppScore >= GameRules.WIN_SCORE - 2 && bf.Ctrl == GameRules.OWNER_PLAYER
                        && theirCount > 0) planScore += 5;

                    // Masteryi passive: lone defender on contested BF gets +2 — slight preference
                    if (gs.ELegend?.Id == LegendSystem.YI_LEGEND_ID
                        && myCount + count == 1 && theirCount > 0) planScore += 2;

                    // BF card modifiers
                    if (bfCard == "trifarian_warcamp") planScore += 2;
                    if (bfCard == "forgotten_monument" && gs.Round < 3) planScore -= 2;

                    if (planScore > bestScore)
                    {
                        bestScore  = planScore;
                        bestMovers = movers;
                        bestBF     = i;
                    }
                }
            }

            // Split strategy: 2 uncontrolled empty BFs → send 1 unit to each (one per call)
            if (sorted.Count >= 2)
            {
                BattlefieldState bf0 = gs.BF[0];
                BattlefieldState bf1 = gs.BF[1];
                bool bf0Open = bfSys == null || bfSys.CanPlayDirectlyToBattlefield(0, gs);
                bool bf1Open = bfSys == null || bfSys.CanPlayDirectlyToBattlefield(1, gs);
                bool splitViable =
                    bf0Open && bf1Open &&
                    bf0.PlayerUnits.Count == 0 && bf1.PlayerUnits.Count == 0 &&
                    bf0.Ctrl != GameRules.OWNER_ENEMY && bf1.Ctrl != GameRules.OWNER_ENEMY &&
                    bf0.EnemyUnits.Count < 2 && bf1.EnemyUnits.Count < 2;

                if (splitViable && 25 > bestScore)
                {
                    // Send strongest unit to BF0; next movement iteration handles BF1
                    bestMovers = new List<UnitInstance> { sorted[0] };
                    bestBF     = 0;
                }
            }

            if (bestMovers != null && bestBF >= 0)
                return (bestMovers, bestBF);
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Legend Ability ────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        private static void AiUseLegendAbility(GameState gs, LegendSystem legendSys)
        {
            if (gs.ELegend == null || gs.ELegend.AbilityUsedThisTurn || gs.ELegend.Exhausted)
                return;

            if (gs.ELegend.Id == LegendSystem.KAISA_LEGEND_ID)
            {
                // Use 虚空感知 if hand contains a Blazing spell we can't afford yet
                bool needsBlazing = gs.GetHand(GameRules.OWNER_ENEMY)
                    .Any(c => c.CardData.IsSpell
                           && c.CardData.RuneType == RuneType.Blazing
                           && c.CardData.RuneCost > 0
                           && gs.GetSch(GameRules.OWNER_ENEMY, RuneType.Blazing)
                              < c.CardData.RuneCost);
                if (needsBlazing)
                    legendSys.UseKaisaActive(GameRules.OWNER_ENEMY, gs);
            }
            // Masteryi (Yi legend): passive only, handled automatically by CombatSystem
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Cost Helpers ──────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Returns true if the AI can afford both the mana and rune costs.</summary>
        private static bool CanAfford(CardData card, GameState gs)
        {
            if (card.Cost > gs.EMana) return false;
            if (card.RuneCost > 0
                && gs.GetSch(GameRules.OWNER_ENEMY, card.RuneType) < card.RuneCost)
                return false;
            return true;
        }

        private static void SpendCost(CardData card, GameState gs)
        {
            gs.EMana -= card.Cost;
            if (card.RuneCost > 0)
                gs.SpendSch(GameRules.OWNER_ENEMY, card.RuneType, card.RuneCost);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Spell Casting Helper ──────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        private async Task CastAISpell(UnitInstance spell, UnitInstance target,
                                       GameState gs, SpellSystem spellSys,
                                       TurnManager turnMgr)
        {
            SpendCost(spell.CardData, gs);
            gs.CardsPlayedThisTurn++;
            Log($"[AI] 发动法术 {spell.UnitName}（费用{spell.CardData.Cost}）　⚡ 可点击【反应】按钮响应！");

            // Give player a window to click the React button
            await Task.Delay(SPELL_REACTION_WINDOW_MS);
            await GameManager.WaitIfReactionActive();

            if (!gs.GameOver)
                spellSys.CastSpell(spell, GameRules.OWNER_ENEMY, target, gs);
        }

        private static UnitInstance FindAffordableSpell(string effectId, GameState gs)
        {
            return gs.GetHand(GameRules.OWNER_ENEMY).FirstOrDefault(
                c => c.CardData.IsSpell
                  && c.CardData.EffectId == effectId
                  && !c.CardData.HasKeyword(CardKeyword.Reactive)
                  && CanAfford(c.CardData, gs));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Unit Queries ──────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        private static List<UnitInstance> GetAllPlayerUnits(GameState gs)
        {
            var list = new List<UnitInstance>(gs.PBase);
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                list.AddRange(gs.BF[i].PlayerUnits);
            return list;
        }

        private static List<UnitInstance> GetAllAIUnits(GameState gs)
        {
            var list = new List<UnitInstance>(gs.EBase);
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                list.AddRange(gs.BF[i].EnemyUnits);
            return list;
        }

        private static List<UnitInstance> GetPlayerBFUnits(GameState gs)
        {
            var list = new List<UnitInstance>();
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                list.AddRange(gs.BF[i].PlayerUnits);
            return list;
        }

        private static List<UnitInstance> GetAIBFUnits(GameState gs)
        {
            var list = new List<UnitInstance>();
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                list.AddRange(gs.BF[i].EnemyUnits);
            return list;
        }

        // ── Logging/Delay helpers ─────────────────────────────────────────────
        private static void Log(string msg) => TurnManager.BroadcastMessage_Static(msg);
        private static Task Delay(int ms)   => Task.Delay(ms);
    }
}
