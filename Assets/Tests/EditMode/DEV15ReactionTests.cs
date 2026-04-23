using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.AI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-15: AI reactive card selection logic tests.
    /// Tests AiPickBestReactiveCard priority order and edge cases.
    /// </summary>
    public class DEV15ReactionTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static GameState MakeGs()
        {
            GameState.ResetUidCounter();
            return new GameState();
        }

        private static UnitInstance MakeReactive(string effectId, int cost = 1)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(effectId, effectId, cost, 0,
                RuneType.Blazing, 0, "reactive",
                CardKeyword.Reactive, effectId,
                isSpell: true, spellTargetType: SpellTargetType.None);
            return new UnitInstance(GameState.NextUid(), cd, GameRules.OWNER_ENEMY);
        }

        private static UnitInstance MakePlayerSpell(string effectId, int cost = 2)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(effectId, effectId, cost, 0,
                RuneType.Blazing, 0, "spell",
                CardKeyword.None, effectId,
                isSpell: true, spellTargetType: SpellTargetType.None);
            return new UnitInstance(GameState.NextUid(), cd, GameRules.OWNER_PLAYER);
        }

        private static UnitInstance MakeEnemyUnit()
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup("test_unit", "测试单位", 1, 2,
                RuneType.Blazing, 0, "unit");
            return new UnitInstance(GameState.NextUid(), cd, GameRules.OWNER_ENEMY);
        }

        // ── wind_wall always wins ────────────────────────────────────────────────

        [Test]
        public void AiPickBestReactive_WindWallAlwaysChosen()
        {
            var gs = MakeGs();
            var reactives = new List<UnitInstance>
            {
                MakeReactive("swindle"),
                MakeReactive("wind_wall"),
                MakeReactive("well_trained")
            };
            var trigger = MakePlayerSpell("void_seek", 3);

            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.AreEqual("wind_wall", chosen.CardData.EffectId);
        }

        // ── flash_counter vs player spell ───────────────────────────────────────

        [Test]
        public void AiPickBestReactive_FlashCounterVsPlayerSpell()
        {
            var gs = MakeGs();
            var reactives = new List<UnitInstance>
            {
                MakeReactive("flash_counter"),
                MakeReactive("well_trained")
            };
            var trigger = MakePlayerSpell("hex_ray", 2);

            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.AreEqual("flash_counter", chosen.CardData.EffectId);
        }

        // ── scoff negates cheap spell ────────────────────────────────────────────

        [Test]
        public void AiPickBestReactive_ScoffNegatesCheapSpell()
        {
            var gs = MakeGs();
            var reactives = new List<UnitInstance>
            {
                MakeReactive("scoff"),
                MakeReactive("well_trained")
            };
            var trigger = MakePlayerSpell("slam", 4); // cost 4 = negatable by scoff

            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.AreEqual("scoff", chosen.CardData.EffectId);
        }

        // ── scoff skips expensive spell ──────────────────────────────────────────

        [Test]
        public void AiPickBestReactive_ScoffSkipsExpensiveSpell()
        {
            var gs = MakeGs();
            var reactives = new List<UnitInstance>
            {
                MakeReactive("scoff")
            };
            var trigger = MakePlayerSpell("starburst", 5); // cost 5 > 4 → can't negate

            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.IsNull(chosen, "AI should pass when scoff can't negate the spell");
        }

        // ── well_trained when AI has allies ─────────────────────────────────────

        [Test]
        public void AiPickBestReactive_WellTrainedWithAllies()
        {
            var gs = MakeGs();
            gs.EBase.Add(MakeEnemyUnit());

            var reactives = new List<UnitInstance>
            {
                MakeReactive("well_trained")
            };
            var trigger = MakePlayerSpell("evolve_day", 3);

            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.AreEqual("well_trained", chosen.CardData.EffectId);
        }

        // ── well_trained returns null without allies ─────────────────────────────

        [Test]
        public void AiPickBestReactive_WellTrainedNoAlliesReturnsNull()
        {
            var gs = MakeGs(); // no AI units on board
            var reactives = new List<UnitInstance>
            {
                MakeReactive("well_trained")
            };
            var trigger = MakePlayerSpell("evolve_day", 3);

            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.IsNull(chosen, "AI should pass well_trained if no ally to buff");
        }

        // ── empty list returns null ─────────────────────────────────────────────

        [Test]
        public void AiPickBestReactive_EmptyListReturnsNull()
        {
            var gs        = MakeGs();
            var reactives = new List<UnitInstance>();
            var trigger   = MakePlayerSpell("hex_ray");

            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.IsNull(chosen);
        }

        // ── null list returns null ──────────────────────────────────────────────

        [Test]
        public void AiPickBestReactive_NullListReturnsNull()
        {
            var gs      = MakeGs();
            var trigger = MakePlayerSpell("hex_ray");

            var chosen = SimpleAI.AiPickBestReactiveCard(null, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.IsNull(chosen);
        }

        // ── wind_wall beats flash_counter in priority ─────────────────────────

        [Test]
        public void AiPickBestReactive_WindWallBeatsFlashCounter()
        {
            var gs = MakeGs();
            var reactives = new List<UnitInstance>
            {
                MakeReactive("flash_counter"),
                MakeReactive("wind_wall")
            };
            var trigger = MakePlayerSpell("void_seek");

            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.AreEqual("wind_wall", chosen.CardData.EffectId);
        }

        // ── null trigger doesn't crash ──────────────────────────────────────────

        [Test]
        public void AiPickBestReactive_NullTriggerNoCrash()
        {
            var gs = MakeGs();
            var reactives = new List<UnitInstance>
            {
                MakeReactive("wind_wall"),
                MakeReactive("scoff")
            };

            // wind_wall doesn't require a trigger — should return it without crashing
            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, null, gs, GameRules.OWNER_ENEMY);

            Assert.AreEqual("wind_wall", chosen.CardData.EffectId);
        }

        // ── duel_stance with allies ──────────────────────────────────────────────

        [Test]
        public void AiPickBestReactive_DuelStanceWithAllies()
        {
            var gs = MakeGs();
            gs.EBase.Add(MakeEnemyUnit());

            var reactives = new List<UnitInstance>
            {
                MakeReactive("duel_stance")
            };
            var trigger = MakePlayerSpell("evolve_day", 3);

            var chosen = SimpleAI.AiPickBestReactiveCard(reactives, trigger, gs, GameRules.OWNER_ENEMY);

            Assert.AreEqual("duel_stance", chosen.CardData.EffectId);
        }
    }
}
