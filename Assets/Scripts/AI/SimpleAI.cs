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
    /// Strategic AI — owner-parameterized so it can drive both enemy and
    /// player-side bot turns. All decisions are symmetric.
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
    /// </summary>
    public class SimpleAI : MonoBehaviour
    {
        private const int SPELL_REACTION_WINDOW_MS = 2000;

        // ── Public entry point ─────────────────────────────────────────────────

        public async Task TakeAction(
            string owner,
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
            string tag = Tag(owner);

            // ── 1. Tap all untapped runes ──────────────────────────────────────
            int manaGained = 0;
            foreach (RuneInstance r in gs.GetRunes(owner))
            {
                if (!r.Tapped) { r.Tapped = true; manaGained++; }
            }
            gs.AddMana(owner, manaGained);
            Log($"{tag} 横置 {manaGained} 张符文，法力 → {gs.GetMana(owner)}");
            await Delay(GameRules.AI_ACTION_DELAY_MS);
            await GameManager.WaitIfReactionActive();
            if (gs.GameOver) return;

            // ── 1b. Recycle runes for schematic energy if needed ─────────────
            AiRecycleRunes(gs, owner);
            await Delay(GameRules.AI_ACTION_DELAY_MS);
            await GameManager.WaitIfReactionActive();
            if (gs.GameOver) return;

            // ── 2. Compute reactive mana reserve ──────────────────────────────
            int reactiveReserve = AiMinReactiveCost(gs, owner);

            // ── 2.5. Use legend active ability before spells/summons ──────────
            if (legendSys != null && gs.GetLegend(owner) != null)
                AiUseLegendAbility(gs, owner, legendSys);

            // ── 3. Play rally_call before summoning ────────────────────────────
            if (spellSys != null && !gs.GameOver)
            {
                UnitInstance rally = FindAffordableSpell("rally_call", gs, owner);
                if (rally != null && AiShouldPlaySpell(rally, gs, owner))
                    await CastAISpell(owner, rally, null, gs, spellSys, turnMgr);
                if (gs.GameOver) { turnMgr.EndTurn(); return; }
                await Delay(GameRules.AI_ACTION_DELAY_MS);
                await GameManager.WaitIfReactionActive();
            }

            // ── 4. Play balance_resolve early ──────────────────────────────────
            if (spellSys != null && !gs.GameOver)
            {
                UnitInstance balance = FindAffordableSpell("balance_resolve", gs, owner);
                if (balance != null)
                    await CastAISpell(owner, balance, null, gs, spellSys, turnMgr);
                if (gs.GameOver) { turnMgr.EndTurn(); return; }
                await Delay(GameRules.AI_ACTION_DELAY_MS);
                await GameManager.WaitIfReactionActive();
            }

            // ── 4.5. Play hero card if affordable ─────────────────────────────
            UnitInstance heroCard = gs.GetHero(owner);
            if (heroCard != null && !gs.GameOver && CanAfford(heroCard.CardData, gs, owner))
            {
                gs.SetHero(owner, null);
                SpendCost(heroCard.CardData, gs, owner);
                gs.GetBase(owner).Add(heroCard);

                bool useHaste = false;
                if (heroCard.CardData.HasKeyword(CardKeyword.Haste))
                {
                    bool hasExtraMana = gs.GetMana(owner) >= 1;
                    bool hasExtraSch  = gs.GetSch(owner, heroCard.CardData.RuneType) >= 1;
                    if (hasExtraMana && hasExtraSch)
                    {
                        gs.AddMana(owner, -1);
                        gs.SpendSch(owner, heroCard.CardData.RuneType, 1);
                        useHaste = true;
                        Log($"{tag} 急速！{heroCard.UnitName} 以活跃状态进场");
                        UI.GameEventBus.FireUnitFloatText(heroCard, "急速！", UI.GameColors.BuffColor);
                    }
                }
                bool rallyHero = gs.RallyCallActiveThisTurn.TryGetValue(owner, out var rh) && rh;
                heroCard.Exhausted = !useHaste && !rallyHero;
                if (rallyHero && !useHaste)
                    UI.GameEventBus.FireUnitFloatText(heroCard, "迎敌号令·活跃！", UI.GameColors.BuffColor);
                heroCard.PlayedThisTurn = true;
                gs.CardsPlayedThisTurn++;
                GameManager.FireCardPlayed(heroCard, owner); // 触发 OnCardPlayed（darius 监听需要）
                Log($"{tag} 英雄出场：{heroCard.UnitName}（费用{heroCard.CardData.Cost}），剩余法力 {gs.GetMana(owner)}");
                entryEffects?.OnUnitEntered(heroCard, owner, gs);
                // CheckKaisaEvolution 已废弃（原卡没有进化机制）

                await Delay(GameRules.AI_ACTION_DELAY_MS);
                await GameManager.WaitIfReactionActive();
                if (gs.GameOver) { turnMgr.EndTurn(); return; }
            }

            // ── 5. Summon units (sorted by value) ─────────────────────────────
            while (!gs.GameOver)
            {
                int available = reactiveReserve > 0
                    ? Mathf.Max(0, gs.GetMana(owner) - reactiveReserve)
                    : gs.GetMana(owner);

                var candidates = gs.GetHand(owner)
                    .Where(c => !c.CardData.IsSpell && !c.CardData.IsEquipment && CanAfford(c.CardData, gs, owner))
                    .OrderByDescending(c => AiCardValue(c.CardData))
                    .ToList();

                if (candidates.Count == 0) break;

                UnitInstance toPlay = candidates.FirstOrDefault(c => c.CardData.Cost <= available)
                                   ?? candidates[0];

                gs.GetHand(owner).Remove(toPlay);
                SpendCost(toPlay.CardData, gs, owner);
                gs.GetBase(owner).Add(toPlay);

                bool useHaste = false;
                if (toPlay.CardData.HasKeyword(CardKeyword.Haste))
                {
                    bool hasExtraMana = gs.GetMana(owner) >= 1;
                    bool hasExtraSch  = gs.GetSch(owner, toPlay.CardData.RuneType) >= 1;
                    if (hasExtraMana && hasExtraSch)
                    {
                        gs.AddMana(owner, -1);
                        gs.SpendSch(owner, toPlay.CardData.RuneType, 1);
                        useHaste = true;
                        Log($"{tag} 急速！支付额外1法力+1{toPlay.CardData.RuneType.ToChinese()}符能，{toPlay.UnitName}以活跃状态进场");
                        UI.GameEventBus.FireUnitFloatText(toPlay, "急速！", UI.GameColors.BuffColor);
                    }
                }
                bool rallyUnit = gs.RallyCallActiveThisTurn.TryGetValue(owner, out var ru) && ru;
                toPlay.Exhausted = !useHaste && !rallyUnit;
                if (rallyUnit && !useHaste)
                    UI.GameEventBus.FireUnitFloatText(toPlay, "迎敌号令·活跃！", UI.GameColors.BuffColor);
                toPlay.PlayedThisTurn = true;
                gs.CardsPlayedThisTurn++;
                GameManager.FireCardPlayed(toPlay, owner); // 触发 OnCardPlayed（darius 监听需要）
                Log($"{tag} 出 {toPlay.UnitName}（费用{toPlay.CardData.Cost}，战力{toPlay.CurrentAtk}），剩余法力 {gs.GetMana(owner)}");
                entryEffects?.OnUnitEntered(toPlay, owner, gs);
                // CheckKaisaEvolution 已废弃（原卡没有进化机制）

                await Delay(GameRules.AI_ACTION_DELAY_MS);
                await GameManager.WaitIfReactionActive();
                if (gs.GameOver) return;
            }

            // ── 6. Play non-reactive spells (priority-sorted) ─────────────────
            if (spellSys != null)
            {
                bool castAny = true;
                while (castAny && !gs.GameOver)
                {
                    castAny = false;

                    UnitInstance sp = gs.GetHand(owner)
                        .Where(c => c.CardData.IsSpell
                                 && !c.CardData.HasKeyword(CardKeyword.Reactive)
                                 && c.CardData.EffectId != "rally_call"
                                 && c.CardData.EffectId != "balance_resolve"
                                 && CanAfford(c, gs, owner)
                                 && AiShouldPlaySpell(c, gs, owner))
                        .OrderByDescending(c => AiSpellPriority(c))
                        .FirstOrDefault();

                    if (sp == null) break;

                    UnitInstance target = AiChooseSpellTarget(sp, gs, owner);

                    if (sp.CardData.SpellTargetType == SpellTargetType.EnemyUnit   && target == null) break;
                    if (sp.CardData.SpellTargetType == SpellTargetType.FriendlyUnit && target == null) break;

                    await CastAISpell(owner, sp, target, gs, spellSys, turnMgr);
                    castAny = true;

                    await Delay(GameRules.AI_ACTION_DELAY_MS);
                    await GameManager.WaitIfReactionActive();
                    if (gs.GameOver) { turnMgr.EndTurn(); return; }
                }
            }

            if (gs.GameOver) { turnMgr.EndTurn(); return; }
            await GameManager.WaitIfReactionActive();
            if (gs.GameOver) { turnMgr.EndTurn(); return; }

            // ── 7. Movement loop ──────────────────────────────────────────────
            while (!gs.GameOver)
            {
                await GameManager.WaitIfReactionActive();
                if (gs.GameOver) { turnMgr.EndTurn(); return; }

                var active = gs.GetBase(owner)
                    .Where(u => !u.Exhausted && !u.Stunned)
                    .ToList();
                if (active.Count == 0) break;

                var plan = AiDecideMovement(active, gs, owner, bfSys);
                if (!plan.HasValue) break;

                var (movers, targetBF) = plan.Value;
                foreach (UnitInstance u in movers)
                {
                    Log($"{tag} 移动 {u.UnitName} → 战场{targetBF + 1}");
                    combat.MoveUnit(u, "base", targetBF, owner, gs);
                }

                string opponent = gs.Opponent(owner);
                if (gs.BF[targetBF].GetUnits(opponent).Count > 0)
                {
                    await Delay(500);
                    UI.GameEventBus.FireDuelBanner();
                    await Delay(2000);
                    if (gs.GameOver) { turnMgr.EndTurn(); return; }
                    UI.GameEventBus.FireSetBannerDelay(0.5f);
                    combat.CheckAndResolveCombat(targetBF, owner, gs, score);
                    await Delay(500);
                }
                else
                {
                    await Delay(GameRules.AI_ACTION_DELAY_MS);
                    if (gs.GameOver) { turnMgr.EndTurn(); return; }
                    combat.CheckAndResolveCombat(targetBF, owner, gs, score);
                    await Delay(GameRules.AI_ACTION_DELAY_MS);
                }
                if (gs.GameOver) { turnMgr.EndTurn(); return; }
            }

            await Delay(GameRules.AI_ACTION_DELAY_MS);
            await GameManager.WaitIfReactionActive();

            // ── 9. End turn ────────────────────────────────────────────────────
            Log($"{tag} 结束回合");
            turnMgr.EndTurn();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Board Evaluation ──────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Global board score from the given owner's perspective.
        /// Positive = owner leading, negative = owner behind.
        /// </summary>
        public static int AiBoardScore(GameState gs, string owner)
        {
            string opp = gs.Opponent(owner);
            int scoreDiff = gs.GetScore(owner) - gs.GetScore(opp);
            int handDiff  = gs.GetHand(owner).Count - gs.GetHand(opp).Count;
            int bfControl = 0;
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                string ctrl = gs.BF[i].Ctrl;
                if      (ctrl == owner) bfControl++;
                else if (ctrl == opp)   bfControl--;
            }
            int myPow  = GetOwnUnits(gs, owner).Sum(u => u.EffectiveAtk());
            int oppPow = GetOpponentUnits(gs, owner).Sum(u => u.EffectiveAtk());
            return scoreDiff * 3 + handDiff / 2 + bfControl * 2 + (myPow - oppPow) / 3;
        }

        /// <summary>
        /// Card value score for prioritizing which unit to summon.
        /// Owner-agnostic (depends only on card data).
        /// </summary>
        public static float AiCardValue(CardData card)
        {
            float atk  = Mathf.Max(card.Atk, 0);
            float cost = Mathf.Max(card.Cost, 1);
            float val  = (atk / cost) * 10f;
            if (card.HasKeyword(CardKeyword.Haste))     val += 4f;
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
        /// Lowest mana cost among reactive cards in owner's hand.
        /// </summary>
        public static int AiMinReactiveCost(GameState gs, string owner)
        {
            var reactives = gs.GetHand(owner)
                .Where(c => c.CardData.IsSpell && c.CardData.HasKeyword(CardKeyword.Reactive))
                .ToList();
            return reactives.Count == 0 ? 0 : reactives.Min(c => c.CardData.Cost);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Spell Decision Logic ──────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// True if owner should actively play this spell this turn.
        /// </summary>
        public static bool AiShouldPlaySpell(UnitInstance spell, GameState gs, string owner)
        {
            if (spell.CardData.HasKeyword(CardKeyword.Reactive)) return false;

            switch (spell.CardData.EffectId)
            {
                case "slam":
                    return GetOpponentUnits(gs, owner).Any(u => !u.Stunned);

                case "strike_ask_later":
                    return GetOwnUnits(gs, owner).Count > 0;

                case "void_seek":
                case "hex_ray":
                case "stardrop":
                case "starburst":
                case "akasi_storm":
                    return GetOpponentUnits(gs, owner).Count > 0;

                case "rally_call":
                    {
                        int cost = spell.CardData.Cost;
                        return gs.GetHand(owner)
                            .Any(c => !c.CardData.IsSpell && !c.CardData.IsEquipment
                                   && c.CardData.Cost <= gs.GetMana(owner) - cost);
                    }

                default:
                    return true;
            }
        }

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
        /// Select best target for a spell based on owner's perspective.
        /// </summary>
        public static UnitInstance AiChooseSpellTarget(UnitInstance spell, GameState gs, string owner)
        {
            switch (spell.CardData.SpellTargetType)
            {
                case SpellTargetType.EnemyUnit:
                {
                    var enemies = GetOpponentUnits(gs, owner);
                    if (enemies.Count == 0) return null;

                    int mySch = gs.GetSch(owner);
                    var affordable = enemies
                        .Where(u => !u.UntargetableBySpells)
                        .Where(u => !u.HasSpellShield || mySch >= 1)
                        .ToList();
                    if (affordable.Count == 0) return null;

                    if (spell.CardData.EffectId == "slam")
                    {
                        UnitInstance bfTarget = GetOpponentBFUnits(gs, owner)
                            .Where(u => !u.UntargetableBySpells)
                            .Where(u => !u.Stunned && (!u.HasSpellShield || mySch >= 1))
                            .OrderByDescending(u => u.EffectiveAtk())
                            .FirstOrDefault();
                        if (bfTarget != null) return bfTarget;
                        return affordable.Where(u => !u.Stunned)
                            .OrderByDescending(u => u.EffectiveAtk()).FirstOrDefault();
                    }

                    return affordable.OrderByDescending(u => u.EffectiveAtk()).First();
                }

                case SpellTargetType.FriendlyUnit:
                {
                    var allies = GetOwnUnits(gs, owner);
                    if (allies.Count == 0) return null;

                    UnitInstance bfAlly = GetOwnBFUnits(gs, owner)
                        .OrderByDescending(u => u.EffectiveAtk()).FirstOrDefault();
                    return bfAlly ?? allies.OrderByDescending(u => u.EffectiveAtk()).First();
                }

                default:
                    return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Movement Decision ─────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Evaluates all possible move plans from owner's perspective.
        /// </summary>
        public static (List<UnitInstance> movers, int bfIndex)?
            AiDecideMovement(List<UnitInstance> active, GameState gs, string owner, BattlefieldSystem bfSys = null)
        {
            string opp = gs.Opponent(owner);
            var sorted = active.OrderByDescending(u => u.EffectiveAtk()).ToList();
            int boardAdv = AiBoardScore(gs, owner);
            int myScore  = gs.GetScore(owner);
            int oppScore = gs.GetScore(opp);

            List<UnitInstance> bestMovers = null;
            int bestBF    = -1;
            int bestScore = int.MinValue;

            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                if (bfSys != null && !bfSys.CanPlayDirectlyToBattlefield(i, gs)) continue;

                BattlefieldState bf = gs.BF[i];
                int myCount    = bf.GetUnits(owner).Count;
                int theirCount = bf.GetUnits(opp).Count;
                int myBFPow    = bf.GetUnits(owner).Sum(u => u.EffectiveAtk());
                int theirBFPow = bf.GetUnits(opp).Sum(u => u.EffectiveAtk());
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
                        if (bf.Ctrl != owner)
                        {
                            planScore = 15;
                            if (bfCard == "ascending_stairs") planScore += 5;
                            if (count > 1) planScore -= 2;
                        }
                        else
                        {
                            planScore = 2;
                        }
                    }
                    else
                    {
                        if (willWin)
                        {
                            planScore = 12 + margin;
                            if (bf.Ctrl != owner) planScore += 3;
                            if (bfCard == "ascending_stairs") planScore += 5;
                        }
                        else if (margin == 0)
                        {
                            planScore = 1;
                            if (bf.Ctrl == opp && myScore < oppScore) planScore += 5;
                        }
                        else
                        {
                            planScore = -3;
                            if (boardAdv < -3 || myScore - oppScore <= -3) planScore += 6;
                            if (boardAdv > 5) planScore -= 3;
                            if (oppScore >= GameRules.WIN_SCORE - 2 && bf.Ctrl == opp)
                                planScore += 8;
                        }
                    }

                    if (myScore >= GameRules.WIN_SCORE - 2) planScore += 3;
                    if (oppScore >= GameRules.WIN_SCORE - 2 && bf.Ctrl == opp
                        && theirCount > 0) planScore += 5;

                    // Masteryi passive: lone defender on contested BF gets +2
                    if (gs.GetLegend(owner)?.Id == LegendSystem.YI_LEGEND_ID
                        && myCount + count == 1 && theirCount > 0) planScore += 2;

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

            // Split strategy: 2 uncontrolled empty BFs → send 1 unit to each
            if (sorted.Count >= 2)
            {
                BattlefieldState bf0 = gs.BF[0];
                BattlefieldState bf1 = gs.BF[1];
                bool bf0Open = bfSys == null || bfSys.CanPlayDirectlyToBattlefield(0, gs);
                bool bf1Open = bfSys == null || bfSys.CanPlayDirectlyToBattlefield(1, gs);
                bool splitViable =
                    bf0Open && bf1Open &&
                    bf0.GetUnits(opp).Count == 0 && bf1.GetUnits(opp).Count == 0 &&
                    bf0.Ctrl != owner && bf1.Ctrl != owner &&
                    bf0.GetUnits(owner).Count < 2 && bf1.GetUnits(owner).Count < 2;

                if (splitViable && 25 > bestScore)
                {
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

        private static void AiUseLegendAbility(GameState gs, string owner, LegendSystem legendSys)
        {
            LegendInstance legend = gs.GetLegend(owner);
            if (legend == null || legend.AbilityUsedThisTurn || legend.Exhausted)
                return;

            if (legend.Id == LegendSystem.KAISA_LEGEND_ID)
            {
                // 选择对当前手牌最有用的颜色（法术专用符能，仅对法术卡有用）
                RuneType chosen = PickBestKaisaRuneColor(gs, owner);
                if (chosen != (RuneType)(-1))
                    legendSys.UseKaisaActive(owner, gs, chosen);
            }
        }

        /// <summary>AI 选择 Kaisa 传奇给予符能的颜色：看手牌中哪个法术最缺符能就选哪个。</summary>
        private static RuneType PickBestKaisaRuneColor(GameState gs, string owner)
        {
            RuneType best = (RuneType)(-1);
            int bestNeed = 0;
            foreach (var c in gs.GetHand(owner))
            {
                if (!c.CardData.IsSpell) continue;
                if (c.CardData.RuneCost > 0)
                {
                    int have = gs.GetTotalSch(owner, c.CardData.RuneType);
                    int need = c.CardData.RuneCost - have;
                    if (need > bestNeed) { bestNeed = need; best = c.CardData.RuneType; }
                }
                if (c.CardData.SecondaryRuneCost > 0)
                {
                    int have = gs.GetTotalSch(owner, c.CardData.SecondaryRuneType);
                    int need = c.CardData.SecondaryRuneCost - have;
                    if (need > bestNeed) { bestNeed = need; best = c.CardData.SecondaryRuneType; }
                }
            }
            return best;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Cost Helpers ──────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Spell-aware overload — applies conditional cost modifiers (e.g. balance_resolve).</summary>
        private static bool CanAfford(UnitInstance spell, GameState gs, string owner)
        {
            var card = spell.CardData;
            int manaCost = GameRules.GetSpellEffectiveCost(spell, owner, gs);
            if (manaCost > gs.GetMana(owner)) return false;
            int haveMain = card.RuneCost > 0 ? gs.GetSch(owner, card.RuneType) : 0;
            int haveTotal = card.IsSpell ? gs.GetTotalSch(owner, card.RuneType) : haveMain;
            if (card.RuneCost > 0 && haveTotal < card.RuneCost) return false;
            if (card.SecondaryRuneCost > 0)
            {
                int have2 = card.IsSpell
                    ? gs.GetTotalSch(owner, card.SecondaryRuneType)
                    : gs.GetSch(owner, card.SecondaryRuneType);
                if (have2 < card.SecondaryRuneCost) return false;
            }
            return true;
        }

        private static bool CanAfford(CardData card, GameState gs, string owner)
        {
            if (card.Cost > gs.GetMana(owner)) return false;
            // 法术可用 SpellOnlySch 池补齐；非法术只能用主池
            int haveMain = card.RuneCost > 0 ? gs.GetSch(owner, card.RuneType) : 0;
            int haveTotal = card.IsSpell ? gs.GetTotalSch(owner, card.RuneType) : haveMain;
            if (card.RuneCost > 0 && haveTotal < card.RuneCost) return false;
            if (card.SecondaryRuneCost > 0)
            {
                int have2 = card.IsSpell
                    ? gs.GetTotalSch(owner, card.SecondaryRuneType)
                    : gs.GetSch(owner, card.SecondaryRuneType);
                if (have2 < card.SecondaryRuneCost) return false;
            }
            return true;
        }

        /// <summary>Spell-aware overload — applies conditional cost modifiers (e.g. balance_resolve).</summary>
        private static void SpendCost(UnitInstance spell, GameState gs, string owner)
        {
            var card = spell.CardData;
            int manaCost = GameRules.GetSpellEffectiveCost(spell, owner, gs);
            gs.AddMana(owner, -manaCost);
            if (card.IsSpell)
            {
                if (card.RuneCost > 0)
                    gs.SpendSchForSpell(owner, card.RuneType, card.RuneCost);
                if (card.SecondaryRuneCost > 0)
                    gs.SpendSchForSpell(owner, card.SecondaryRuneType, card.SecondaryRuneCost);
            }
            else
            {
                if (card.RuneCost > 0)
                    gs.SpendSch(owner, card.RuneType, card.RuneCost);
                if (card.SecondaryRuneCost > 0)
                    gs.SpendSch(owner, card.SecondaryRuneType, card.SecondaryRuneCost);
            }
        }

        private static void SpendCost(CardData card, GameState gs, string owner)
        {
            gs.AddMana(owner, -card.Cost);
            if (card.IsSpell)
            {
                // 法术：优先消耗 SpellOnly 池
                if (card.RuneCost > 0)
                    gs.SpendSchForSpell(owner, card.RuneType, card.RuneCost);
                if (card.SecondaryRuneCost > 0)
                    gs.SpendSchForSpell(owner, card.SecondaryRuneType, card.SecondaryRuneCost);
            }
            else
            {
                // 非法术：只能消耗主池
                if (card.RuneCost > 0)
                    gs.SpendSch(owner, card.RuneType, card.RuneCost);
                if (card.SecondaryRuneCost > 0)
                    gs.SpendSch(owner, card.SecondaryRuneType, card.SecondaryRuneCost);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Spell Casting Helper ──────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        private async Task CastAISpell(string owner, UnitInstance spell, UnitInstance target,
                                       GameState gs, SpellSystem spellSys,
                                       TurnManager turnMgr)
        {
            string tag = Tag(owner);
            SpendCost(spell, gs, owner);
            gs.CardsPlayedThisTurn++;
            GameManager.FireCardPlayed(spell, owner); // 触发 OnCardPlayed（darius 监听需要，计 CardsPlayedThisTurn）
            if (target != null && target.HasSpellShield)
            {
                foreach (RuneType rt in System.Enum.GetValues(typeof(RuneType)))
                {
                    if (gs.GetSch(owner, rt) > 0)
                    { gs.SpendSch(owner, rt, 1); break; }
                }
            }
            Log($"{tag} 发动法术 {spell.UnitName}（费用{spell.CardData.Cost}）　⚡ 可点击【反应】按钮响应！");

            if (SpellShowcaseUI.Instance != null)
                _ = SpellShowcaseUI.Instance.ShowAsync(spell, owner);

            await Delay(SPELL_REACTION_WINDOW_MS);
            await GameManager.WaitIfReactionActive();

            if (!gs.GameOver)
            {
                await Delay((int)(SpellShowcaseUI.TOTAL_DURATION * 1000f));
                if (target != null && EntryEffectVFX.Instance != null)
                {
                    EntryEffectVFX.Instance.PlaySpellOrbs(
                        new System.Collections.Generic.List<UnitInstance> { target },
                        spell.UnitName,
                        GameColors.SchColor);
                    await Delay(400);
                }
                spellSys.CastSpell(spell, owner, target, gs);
                await Delay(550);

                // Echo: 支付 1 主符能后可重复效果（AI 自动：够就 echo）
                if (!gs.GameOver && spell.CardData.HasKeyword(CardKeyword.Echo)
                    && GameRules.CanAffordEcho(spell, owner, gs))
                {
                    GameRules.SpendEchoCost(spell, owner, gs);
                    UnitInstance echoTarget = AiChooseSpellTarget(spell, gs, owner);
                    // 特殊：spell 已离手进弃牌堆（或放逐），仅用 CardData 驱动再次 effect。
                    Log($"{tag} [回响] {spell.UnitName} 再次发动！");
                    TurnManager.ShowBanner_Static($"⚡ [AI·回响] {spell.UnitName}");
                    if (echoTarget != null && EntryEffectVFX.Instance != null)
                    {
                        EntryEffectVFX.Instance.PlaySpellOrbs(
                            new System.Collections.Generic.List<UnitInstance> { echoTarget },
                            spell.UnitName + "·回响",
                            GameColors.SchColor);
                        await Delay(300);
                    }
                    spellSys.EchoCast(spell, owner, echoTarget, gs);
                    await Delay(500);
                }
            }
        }

        private static UnitInstance FindAffordableSpell(string effectId, GameState gs, string owner)
        {
            return gs.GetHand(owner).FirstOrDefault(
                c => c.CardData.IsSpell
                  && c.CardData.EffectId == effectId
                  && !c.CardData.HasKeyword(CardKeyword.Reactive)
                  && CanAfford(c, gs, owner));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Unit Queries (owner-relative) ─────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        private static List<UnitInstance> GetOwnUnits(GameState gs, string owner)
        {
            var list = new List<UnitInstance>(gs.GetBase(owner));
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                list.AddRange(gs.BF[i].GetUnits(owner));
            return list;
        }

        private static List<UnitInstance> GetOpponentUnits(GameState gs, string owner)
        {
            string opp = gs.Opponent(owner);
            var list = new List<UnitInstance>(gs.GetBase(opp));
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                list.AddRange(gs.BF[i].GetUnits(opp));
            return list;
        }

        private static List<UnitInstance> GetOwnBFUnits(GameState gs, string owner)
        {
            var list = new List<UnitInstance>();
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                list.AddRange(gs.BF[i].GetUnits(owner));
            return list;
        }

        private static List<UnitInstance> GetOpponentBFUnits(GameState gs, string owner)
        {
            string opp = gs.Opponent(owner);
            var list = new List<UnitInstance>();
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
                list.AddRange(gs.BF[i].GetUnits(opp));
            return list;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── AI Rune Recycle ──────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Recycles runes to gain schematic energy when owner has spells/units
        /// needing sch but insufficient sch.
        /// </summary>
        private static void AiRecycleRunes(GameState gs, string owner)
        {
            string tag = Tag(owner);
            var hand = gs.GetHand(owner);
            var neededSch = new Dictionary<RuneType, int>();

            foreach (var card in hand)
            {
                if (card.CardData.RuneCost <= 0) continue;
                if (card.CardData.HasKeyword(CardKeyword.Reactive)) continue;
                if (card.CardData.Cost > gs.GetMana(owner)) continue;

                RuneType rt = card.CardData.RuneType;
                int have = gs.GetSch(owner, rt);
                int need = card.CardData.RuneCost - have;
                if (need > 0)
                {
                    if (!neededSch.ContainsKey(rt)) neededSch[rt] = 0;
                    neededSch[rt] = Mathf.Max(neededSch[rt], need);
                }
            }

            if (neededSch.Count == 0) return;

            var runes = gs.GetRunes(owner);

            foreach (var kv in neededSch)
            {
                RuneType targetType = kv.Key;
                int remaining = kv.Value;

                for (int i = runes.Count - 1; i >= 0 && remaining > 0; i--)
                {
                    if (runes[i].RuneType == targetType && runes[i].Tapped)
                    {
                        RuneInstance r = runes[i];
                        runes.RemoveAt(i);
                        gs.GetRuneDeck(owner).Add(r);
                        gs.AddSch(owner, r.RuneType, 1);
                        remaining--;
                        Log($"{tag}[回收] 回收已横置符文 {r.RuneType.ToChinese()}，+1{r.RuneType.ToChinese()}符能");
                    }
                }

                for (int i = runes.Count - 1; i >= 0 && remaining > 0; i--)
                {
                    if (runes[i].RuneType == targetType && !runes[i].Tapped)
                    {
                        RuneInstance r = runes[i];
                        runes.RemoveAt(i);
                        gs.GetRuneDeck(owner).Add(r);
                        gs.AddSch(owner, r.RuneType, 1);
                        remaining--;
                        Log($"{tag}[回收] 回收未横置符文 {r.RuneType.ToChinese()}（牺牲法力），+1{r.RuneType.ToChinese()}符能");
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── AI Reactive Card Selection ───────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Selects the best reactive card for owner to play in response to a trigger spell.
        /// Returns null if owner should pass.
        /// </summary>
        public static UnitInstance AiPickBestReactiveCard(
            List<UnitInstance> reactives, UnitInstance triggerSpell, GameState gs, string owner)
        {
            if (reactives == null || reactives.Count == 0) return null;

            // Priority 1: wind_wall — negates any spell unconditionally
            foreach (var r in reactives)
                if (r.CardData.EffectId == "wind_wall") return r;

            // Priority 2: flash_counter — negates an opponent spell
            string opp = gs.Opponent(owner);
            foreach (var r in reactives)
                if (r.CardData.EffectId == "flash_counter"
                    && triggerSpell != null
                    && triggerSpell.Owner == opp)
                    return r;

            // Priority 3: scoff — negates spells with cost ≤ 4
            foreach (var r in reactives)
                if (r.CardData.EffectId == "scoff"
                    && triggerSpell != null
                    && triggerSpell.CardData.Cost <= 4)
                    return r;

            // Priority 4: well_trained (needs ally)
            foreach (var r in reactives)
                if (r.CardData.EffectId == "well_trained" && GetOwnUnits(gs, owner).Count > 0)
                    return r;

            // Priority 5: duel_stance (needs ally)
            foreach (var r in reactives)
                if (r.CardData.EffectId == "duel_stance" && GetOwnUnits(gs, owner).Count > 0)
                    return r;

            return null;
        }

        // ── Logging/Delay helpers ─────────────────────────────────────────────
        private static string Tag(string owner)
            => owner == GameRules.OWNER_PLAYER ? "[玩家AI]" : "[AI]";

        private static void Log(string msg) => TurnManager.BroadcastMessage_Static(msg);

        /// <summary>
        /// Bot 模式下跳过所有 AI 延迟，实现超速对局。
        /// </summary>
        public static bool SkipDelays = false;

        private static Task Delay(int ms)
            => SkipDelays ? Task.CompletedTask : FWTCG.Core.GameTiming.Delay(ms);
    }
}
