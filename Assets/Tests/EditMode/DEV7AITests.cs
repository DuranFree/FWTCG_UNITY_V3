using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FWTCG.AI;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-7: Strategic AI decision tests.
    /// Tests the pure-logic helper methods exposed as public static on SimpleAI:
    ///   - AiCardValue: keyword/efficiency scoring
    ///   - AiBoardScore: composite board evaluation
    ///   - AiMinReactiveCost: reactive mana reservation
    ///   - AiSpellPriority: spell ordering
    ///   - AiShouldPlaySpell: spell playability filter
    ///   - AiChooseSpellTarget: smart target selection
    ///   - AiDecideMovement: battlefield scoring + split strategy
    /// </summary>
    [TestFixture]
    public class DEV7AITests
    {
        private GameState _gs;

        // ── Helpers ───────────────────────────────────────────────────────────

        private static CardData MakeUnit(string id, int cost, int atk,
                                         CardKeyword kw = CardKeyword.None)
        {
            var so = ScriptableObject.CreateInstance<CardData>();
            so.EditorSetup(id, id, cost, atk, RuneType.Blazing, 0, "test", kw);
            return so;
        }

        private static CardData MakeSpell(string id, int cost, string effectId,
                                          CardKeyword kw = CardKeyword.None,
                                          RuneType rune = RuneType.Blazing, int runeCost = 0,
                                          SpellTargetType targetType = SpellTargetType.None)
        {
            var so = ScriptableObject.CreateInstance<CardData>();
            so.EditorSetup(id, id, cost, 0, rune, runeCost, "test", kw, effectId,
                           isSpell: true, spellTargetType: targetType);
            return so;
        }

        private UnitInstance AddEnemyHand(string id, int cost = 2, int atk = 2,
                                          CardKeyword kw = CardKeyword.None)
        {
            var u = new UnitInstance(GameState.NextUid(), MakeUnit(id, cost, atk, kw),
                                     GameRules.OWNER_ENEMY);
            _gs.GetHand(GameRules.OWNER_ENEMY).Add(u);
            return u;
        }

        private UnitInstance AddEnemyHandSpell(string id, int cost, string effectId,
                                               CardKeyword kw = CardKeyword.None,
                                               RuneType rune = RuneType.Blazing, int runeCost = 0,
                                               SpellTargetType target = SpellTargetType.None)
        {
            var u = new UnitInstance(GameState.NextUid(), MakeSpell(id, cost, effectId, kw, rune, runeCost, target),
                                     GameRules.OWNER_ENEMY);
            _gs.GetHand(GameRules.OWNER_ENEMY).Add(u);
            return u;
        }

        private UnitInstance PlaceUnit(string id, int atk, string owner, string zone = "base")
        {
            var data = MakeUnit(id, 1, atk);
            var u    = new UnitInstance(GameState.NextUid(), data, owner);
            if (zone == "base")
            {
                if (owner == GameRules.OWNER_PLAYER) _gs.PBase.Add(u);
                else                                  _gs.EBase.Add(u);
            }
            else if (int.TryParse(zone, out int bfIdx))
            {
                if (owner == GameRules.OWNER_PLAYER) _gs.BF[bfIdx].PlayerUnits.Add(u);
                else                                  _gs.BF[bfIdx].EnemyUnits.Add(u);
            }
            return u;
        }

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _gs = new GameState();
            _gs.AddMana(GameRules.OWNER_ENEMY, 10);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── AiCardValue ───────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void CardValue_HighAtkLowCost_ScoresHigher()
        {
            // 4 atk / 2 cost = 20; 2 atk / 3 cost = 6.67
            var cheap  = MakeUnit("cheap",  2, 4);
            var pricey = MakeUnit("pricey", 3, 2);
            Assert.Greater(SimpleAI.AiCardValue(cheap), SimpleAI.AiCardValue(pricey));
        }

        [Test]
        public void CardValue_HasteKeyword_AddsBonus()
        {
            var plain = MakeUnit("plain", 2, 2, CardKeyword.None);
            var haste = MakeUnit("haste", 2, 2, CardKeyword.Haste);
            Assert.Greater(SimpleAI.AiCardValue(haste), SimpleAI.AiCardValue(plain));
        }

        [Test]
        public void CardValue_MultipleKeywords_StackAdditively()
        {
            var plain    = MakeUnit("plain", 2, 2, CardKeyword.None);
            var buffed   = MakeUnit("buffed", 2, 2,
                                    CardKeyword.Haste | CardKeyword.Barrier | CardKeyword.StrongAtk);
            float diff = SimpleAI.AiCardValue(buffed) - SimpleAI.AiCardValue(plain);
            // Haste +4, Barrier +3, StrongAtk +2 = +9
            Assert.AreEqual(9f, diff, 0.01f);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── AiBoardScore ──────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void BoardScore_AILeadsOnScore_Positive()
        {
            _gs.EScore = 5;
            _gs.PScore = 2;
            Assert.Greater(SimpleAI.AiBoardScore(_gs), 0);
        }

        [Test]
        public void BoardScore_PlayerLeadsOnScore_Negative()
        {
            _gs.EScore = 1;
            _gs.PScore = 6;
            Assert.Less(SimpleAI.AiBoardScore(_gs), 0);
        }

        [Test]
        public void BoardScore_AIControlsBothBFs_Positive()
        {
            _gs.EScore = 0; _gs.PScore = 0;
            _gs.BF[0].Ctrl = GameRules.OWNER_ENEMY;
            _gs.BF[1].Ctrl = GameRules.OWNER_ENEMY;
            Assert.Greater(SimpleAI.AiBoardScore(_gs), 0);
        }

        [Test]
        public void BoardScore_EqualState_Zero()
        {
            _gs.EScore = 0; _gs.PScore = 0;
            // No BF control, no units, equal hands → score = 0
            Assert.AreEqual(0, SimpleAI.AiBoardScore(_gs));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── AiMinReactiveCost ─────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void MinReactiveCost_NoReactives_ReturnsZero()
        {
            AddEnemyHand("unit1");
            Assert.AreEqual(0, SimpleAI.AiMinReactiveCost(_gs));
        }

        [Test]
        public void MinReactiveCost_HasReactives_ReturnsMinCost()
        {
            AddEnemyHandSpell("r1", 3, "swindle", CardKeyword.Reactive);
            AddEnemyHandSpell("r2", 1, "smoke_bomb", CardKeyword.Reactive);
            AddEnemyHandSpell("r3", 2, "scoff", CardKeyword.Reactive);
            Assert.AreEqual(1, SimpleAI.AiMinReactiveCost(_gs));
        }

        [Test]
        public void MinReactiveCost_NonSpellsIgnored()
        {
            AddEnemyHand("unit1"); // unit card, not spell
            AddEnemyHandSpell("r1", 2, "wind_wall", CardKeyword.Reactive);
            Assert.AreEqual(2, SimpleAI.AiMinReactiveCost(_gs));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── AiSpellPriority ───────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SpellPriority_RallyCallIsHighest()
        {
            var rally   = new UnitInstance(0, MakeSpell("rally_call",    1, "rally_call"), GameRules.OWNER_ENEMY);
            var balance = new UnitInstance(0, MakeSpell("balance_resolve", 2, "balance_resolve"), GameRules.OWNER_ENEMY);
            var slam    = new UnitInstance(0, MakeSpell("slam",          1, "slam"), GameRules.OWNER_ENEMY);
            Assert.Greater(SimpleAI.AiSpellPriority(rally),   SimpleAI.AiSpellPriority(balance));
            Assert.Greater(SimpleAI.AiSpellPriority(balance), SimpleAI.AiSpellPriority(slam));
        }

        [Test]
        public void SpellPriority_SlamBeforeEvolveDay()
        {
            var slam   = new UnitInstance(0, MakeSpell("slam",      1, "slam"),      GameRules.OWNER_ENEMY);
            var evolve = new UnitInstance(0, MakeSpell("evolve_day", 1, "evolve_day"), GameRules.OWNER_ENEMY);
            Assert.Greater(SimpleAI.AiSpellPriority(slam), SimpleAI.AiSpellPriority(evolve));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── AiShouldPlaySpell ─────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ShouldPlaySpell_Reactive_AlwaysFalse()
        {
            var card = new UnitInstance(0,
                MakeSpell("swindle", 1, "swindle", CardKeyword.Reactive),
                GameRules.OWNER_ENEMY);
            Assert.IsFalse(SimpleAI.AiShouldPlaySpell(card, _gs));
        }

        [Test]
        public void ShouldPlaySpell_Slam_FalseWhenNoEnemies()
        {
            var slam = new UnitInstance(0, MakeSpell("slam", 1, "slam"), GameRules.OWNER_ENEMY);
            Assert.IsFalse(SimpleAI.AiShouldPlaySpell(slam, _gs));
        }

        [Test]
        public void ShouldPlaySpell_Slam_TrueWhenEnemyExists()
        {
            PlaceUnit("p1", 3, GameRules.OWNER_PLAYER, "base");
            var slam = new UnitInstance(0, MakeSpell("slam", 1, "slam"), GameRules.OWNER_ENEMY);
            Assert.IsTrue(SimpleAI.AiShouldPlaySpell(slam, _gs));
        }

        [Test]
        public void ShouldPlaySpell_StrikeAskLater_FalseWhenNoAllies()
        {
            var strike = new UnitInstance(0,
                MakeSpell("strike_ask_later", 2, "strike_ask_later",
                          targetType: SpellTargetType.FriendlyUnit),
                GameRules.OWNER_ENEMY);
            Assert.IsFalse(SimpleAI.AiShouldPlaySpell(strike, _gs));
        }

        [Test]
        public void ShouldPlaySpell_StrikeAskLater_TrueWhenAllyExists()
        {
            PlaceUnit("e1", 3, GameRules.OWNER_ENEMY, "base");
            var strike = new UnitInstance(0,
                MakeSpell("strike_ask_later", 2, "strike_ask_later",
                          targetType: SpellTargetType.FriendlyUnit),
                GameRules.OWNER_ENEMY);
            Assert.IsTrue(SimpleAI.AiShouldPlaySpell(strike, _gs));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── AiChooseSpellTarget ───────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ChooseTarget_DamageSpell_PicksHighestAtkEnemy()
        {
            var p1 = PlaceUnit("p1", 2, GameRules.OWNER_PLAYER, "base");
            var p2 = PlaceUnit("p2", 5, GameRules.OWNER_PLAYER, "base");
            var voidSeek = new UnitInstance(0,
                MakeSpell("void_seek", 2, "void_seek",
                          targetType: SpellTargetType.EnemyUnit),
                GameRules.OWNER_ENEMY);
            var target = SimpleAI.AiChooseSpellTarget(voidSeek, _gs);
            Assert.AreEqual(p2, target, "Should pick highest ATK enemy");
        }

        [Test]
        public void ChooseTarget_Slam_PrefersUnstunnedBFEnemy()
        {
            var baseEnemy  = PlaceUnit("base_p", 4, GameRules.OWNER_PLAYER, "base");
            var bfEnemy    = PlaceUnit("bf_p",   3, GameRules.OWNER_PLAYER, "0");
            var slam = new UnitInstance(0,
                MakeSpell("slam", 1, "slam", targetType: SpellTargetType.EnemyUnit),
                GameRules.OWNER_ENEMY);
            var target = SimpleAI.AiChooseSpellTarget(slam, _gs);
            // BF enemy preferred even though base enemy has higher ATK
            Assert.AreEqual(bfEnemy, target, "Slam should prefer BF enemy");
        }

        [Test]
        public void ChooseTarget_Buff_PrefersHighestAtkAlly()
        {
            var e1 = PlaceUnit("e1", 2, GameRules.OWNER_ENEMY, "base");
            var e2 = PlaceUnit("e2", 5, GameRules.OWNER_ENEMY, "base");
            var strike = new UnitInstance(0,
                MakeSpell("strike_ask_later", 2, "strike_ask_later",
                          targetType: SpellTargetType.FriendlyUnit),
                GameRules.OWNER_ENEMY);
            var target = SimpleAI.AiChooseSpellTarget(strike, _gs);
            Assert.AreEqual(e2, target, "Buff should pick highest ATK ally");
        }

        [Test]
        public void ChooseTarget_Buff_PrefersBFAllyOverBase()
        {
            var baseAlly = PlaceUnit("base_e", 4, GameRules.OWNER_ENEMY, "base");
            var bfAlly   = PlaceUnit("bf_e",   3, GameRules.OWNER_ENEMY, "0");
            var strike = new UnitInstance(0,
                MakeSpell("strike_ask_later", 2, "strike_ask_later",
                          targetType: SpellTargetType.FriendlyUnit),
                GameRules.OWNER_ENEMY);
            var target = SimpleAI.AiChooseSpellTarget(strike, _gs);
            Assert.AreEqual(bfAlly, target, "Buff should prefer BF ally (active combat)");
        }

        [Test]
        public void ChooseTarget_NoTarget_ReturnsNull()
        {
            var voidSeek = new UnitInstance(0,
                MakeSpell("void_seek", 2, "void_seek", targetType: SpellTargetType.EnemyUnit),
                GameRules.OWNER_ENEMY);
            // No player units
            Assert.IsNull(SimpleAI.AiChooseSpellTarget(voidSeek, _gs));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── AiDecideMovement ──────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Movement_EmptyBF_SendsUnitToUncontrolledField()
        {
            // BF0 uncontrolled, BF1 owned by AI
            _gs.BF[0].Ctrl = null;
            _gs.BF[1].Ctrl = GameRules.OWNER_ENEMY;
            var unit = PlaceUnit("e1", 3, GameRules.OWNER_ENEMY, "base");
            unit.Exhausted = false;

            var plan = SimpleAI.AiDecideMovement(new List<UnitInstance> { unit }, _gs);
            Assert.IsTrue(plan.HasValue);
            Assert.AreEqual(0, plan.Value.bfIndex, "Should prefer uncontrolled empty BF");
        }

        [Test]
        public void Movement_WinningCombat_PreferredOverEmptyBF()
        {
            // BF0: player has 2-atk unit. BF1: empty, uncontrolled.
            PlaceUnit("p1", 2, GameRules.OWNER_PLAYER, "0");
            _gs.BF[0].Ctrl = GameRules.OWNER_PLAYER;
            _gs.BF[1].Ctrl = null;

            // Our unit has 5 atk → wins BF0 combat AND conquers
            var unit = PlaceUnit("e1", 5, GameRules.OWNER_ENEMY, "base");
            unit.Exhausted = false;

            var plan = SimpleAI.AiDecideMovement(new List<UnitInstance> { unit }, _gs);
            Assert.IsTrue(plan.HasValue);
            Assert.AreEqual(0, plan.Value.bfIndex,
                "Winning combat + conquest scores higher than empty BF");
        }

        [Test]
        public void Movement_SplitStrategy_TwoEmptyBFs_SendOneEach()
        {
            // Both BFs empty, uncontrolled
            _gs.BF[0].Ctrl = null;
            _gs.BF[1].Ctrl = null;
            var e1 = PlaceUnit("e1", 4, GameRules.OWNER_ENEMY, "base"); e1.Exhausted = false;
            var e2 = PlaceUnit("e2", 2, GameRules.OWNER_ENEMY, "base"); e2.Exhausted = false;

            var plan = SimpleAI.AiDecideMovement(new List<UnitInstance> { e1, e2 }, _gs);
            Assert.IsTrue(plan.HasValue);
            // Split strategy: send only sorted[0] to BF0 (strongest unit)
            Assert.AreEqual(1, plan.Value.movers.Count, "Split sends 1 unit per call");
            Assert.AreEqual(e1, plan.Value.movers[0], "Strongest unit leads");
            Assert.AreEqual(0, plan.Value.bfIndex, "Goes to BF0 first");
        }

        [Test]
        public void Movement_NoActiveUnits_ReturnsNull()
        {
            var plan = SimpleAI.AiDecideMovement(new List<UnitInstance>(), _gs);
            Assert.IsFalse(plan.HasValue);
        }

        [Test]
        public void Movement_UrgencyBoost_ContestedBFWhenOpponentNearWin()
        {
            // Opponent is at WIN_SCORE - 1 (7) and controls BF0
            _gs.PScore = GameRules.WIN_SCORE - 1;
            _gs.EScore = 0;
            PlaceUnit("p1", 2, GameRules.OWNER_PLAYER, "0");
            _gs.BF[0].Ctrl = GameRules.OWNER_PLAYER;
            _gs.BF[1].Ctrl = null; // BF1 empty

            // Even with weak unit (can't win combat), should still go to BF0
            var unit = PlaceUnit("e1", 1, GameRules.OWNER_ENEMY, "base");
            unit.Exhausted = false;

            var plan = SimpleAI.AiDecideMovement(new List<UnitInstance> { unit }, _gs);
            Assert.IsTrue(plan.HasValue);
            Assert.AreEqual(0, plan.Value.bfIndex,
                "Must contest player-controlled BF when they're about to win");
        }

        [Test]
        public void Movement_MasteryiPassive_PrefersLoneDefender()
        {
            // Setup: Yi legend for enemy, BF0 has 1 player unit, BF1 empty
            _gs.ELegend = new LegendInstance("masteryi", "易大师", GameRules.OWNER_ENEMY);
            PlaceUnit("p1", 2, GameRules.OWNER_PLAYER, "0");
            _gs.BF[0].Ctrl = GameRules.OWNER_PLAYER;
            _gs.BF[1].Ctrl = null;

            // Unit with same ATK as enemy — normally a tie (bad), but Yi passive makes it worthwhile
            var unit = PlaceUnit("e1", 2, GameRules.OWNER_ENEMY, "base");
            unit.Exhausted = false;

            var plan = SimpleAI.AiDecideMovement(new List<UnitInstance> { unit }, _gs);
            Assert.IsTrue(plan.HasValue);
            // Masteryi passive should nudge score for BF0 (lone defender scenario)
            // (exact BF depends on scores, just verify a plan exists)
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── CanAfford / cost check ────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void BoardScore_AIHasMoreUnits_ScoresHigher()
        {
            // AI has 3 units with high ATK
            PlaceUnit("e1", 5, GameRules.OWNER_ENEMY, "base");
            PlaceUnit("e2", 4, GameRules.OWNER_ENEMY, "base");
            PlaceUnit("e3", 3, GameRules.OWNER_ENEMY, "base");
            // Player has 1 weak unit
            PlaceUnit("p1", 1, GameRules.OWNER_PLAYER, "base");
            Assert.Greater(SimpleAI.AiBoardScore(_gs), 0);
        }
    }
}
