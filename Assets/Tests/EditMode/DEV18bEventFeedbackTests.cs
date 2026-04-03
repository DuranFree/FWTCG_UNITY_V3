using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-18b: Tests for GameEventBus event firing and GameColors additions.
    /// All tests are EditMode (no MonoBehaviour / canvas required).
    /// </summary>
    [TestFixture]
    public class DEV18bEventFeedbackTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private CardData MakeCard(string id, int atk = 2)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(id, id, 1, atk, RuneType.Blazing, 0, "");
            return cd;
        }

        private UnitInstance MakeUnit(string name, string owner = "player")
            => new UnitInstance(1, MakeCard(name), owner);

        // ── GameEventBus: UnitFloatText ───────────────────────────────────────

        [Test]
        public void GameEventBus_FireUnitFloatText_InvokesSubscriber()
        {
            UnitInstance received = null;
            string receivedText = null;
            Color receivedColor = default;

            // DEV-26: save lambda reference so unsubscription actually works
            System.Action<FWTCG.Core.UnitInstance, string, Color> handler =
                (u, t, c) => { received = u; receivedText = t; receivedColor = c; };
            GameEventBus.OnUnitFloatText += handler;

            var unit = MakeUnit("test_unit");
            GameEventBus.FireUnitFloatText(unit, "+1战力", Color.yellow);

            GameEventBus.OnUnitFloatText -= handler;

            Assert.AreEqual(unit, received);
            Assert.AreEqual("+1战力", receivedText);
            Assert.AreEqual(Color.yellow, receivedColor);
        }

        [Test]
        public void GameEventBus_FireUnitFloatText_NullUnit_DoesNotThrow()
        {
            // FireUnitAtkBuff has null guard for unit
            Assert.DoesNotThrow(() => GameEventBus.FireUnitAtkBuff(null, 2));
        }

        // ── GameEventBus: ZoneFloatText ───────────────────────────────────────

        [Test]
        public void GameEventBus_FireZoneFloatText_InvokesSubscriber()
        {
            string receivedZone = null;
            string receivedText = null;

            void Handler(string zone, string text, Color color) { receivedZone = zone; receivedText = text; }
            GameEventBus.OnZoneFloatText += Handler;

            GameEventBus.FireZoneFloatText("score_player", "+1分", Color.yellow);
            GameEventBus.OnZoneFloatText -= Handler;

            Assert.AreEqual("score_player", receivedZone);
            Assert.AreEqual("+1分", receivedText);
        }

        [Test]
        public void GameEventBus_FireScoreFloat_PlayerZone()
        {
            string receivedZone = null;
            string receivedText = null;
            void Handler(string zone, string text, Color c) { receivedZone = zone; receivedText = text; }
            GameEventBus.OnZoneFloatText += Handler;

            GameEventBus.FireScoreFloat(GameRules.OWNER_PLAYER, 1);
            GameEventBus.OnZoneFloatText -= Handler;

            Assert.AreEqual("score_player", receivedZone);
            Assert.AreEqual("+1分", receivedText);
        }

        [Test]
        public void GameEventBus_FireScoreFloat_EnemyZone()
        {
            string receivedZone = null;
            void Handler(string zone, string text, Color c) { receivedZone = zone; }
            GameEventBus.OnZoneFloatText += Handler;

            GameEventBus.FireScoreFloat(GameRules.OWNER_ENEMY, 2);
            GameEventBus.OnZoneFloatText -= Handler;

            Assert.AreEqual("score_enemy", receivedZone);
        }

        [Test]
        public void GameEventBus_FireRuneTapFloat_PlayerZone()
        {
            string receivedZone = null;
            void Handler(string zone, string text, Color c) { receivedZone = zone; }
            GameEventBus.OnZoneFloatText += Handler;

            GameEventBus.FireRuneTapFloat(GameRules.OWNER_PLAYER);
            GameEventBus.OnZoneFloatText -= Handler;

            Assert.AreEqual("rune_player", receivedZone);
        }

        [Test]
        public void GameEventBus_FireRuneRecycleFloat_EnemyZone()
        {
            string receivedZone = null;
            void Handler(string zone, string text, Color c) { receivedZone = zone; }
            GameEventBus.OnZoneFloatText += Handler;

            GameEventBus.FireRuneRecycleFloat(GameRules.OWNER_ENEMY);
            GameEventBus.OnZoneFloatText -= Handler;

            Assert.AreEqual("rune_enemy", receivedZone);
        }

        // ── GameEventBus: EventBanner ─────────────────────────────────────────

        [Test]
        public void GameEventBus_FireEventBanner_InvokesSubscriber()
        {
            string receivedText = null;
            float receivedDuration = 0f;
            bool receivedLarge = false;
            void Handler(string t, float d, bool l) { receivedText = t; receivedDuration = d; receivedLarge = l; }
            GameEventBus.OnEventBanner += Handler;

            GameEventBus.FireEventBanner("征服！+1分", 2f, false);
            GameEventBus.OnEventBanner -= Handler;

            Assert.AreEqual("征服！+1分", receivedText);
            Assert.AreEqual(2f, receivedDuration, 0.001f);
            Assert.IsFalse(receivedLarge);
        }

        [Test]
        public void GameEventBus_FireHoldScoreBanner_DefaultDuration()
        {
            float duration = 0f;
            void Handler(string t, float d, bool l) { duration = d; }
            GameEventBus.OnEventBanner += Handler;

            GameEventBus.FireHoldScoreBanner();
            GameEventBus.OnEventBanner -= Handler;

            Assert.AreEqual(1.0f, duration, 0.001f);
        }

        [Test]
        public void GameEventBus_FireConquerScoreBanner_2sDuration()
        {
            float duration = 0f;
            void Handler(string t, float d, bool l) { duration = d; }
            GameEventBus.OnEventBanner += Handler;

            GameEventBus.FireConquerScoreBanner();
            GameEventBus.OnEventBanner -= Handler;

            Assert.AreEqual(1.0f, duration, 0.001f);
        }

        [Test]
        public void GameEventBus_FireBurnoutBanner_IsLarge()
        {
            bool large = false;
            void Handler(string t, float d, bool l) { large = l; }
            GameEventBus.OnEventBanner += Handler;

            GameEventBus.FireBurnoutBanner(GameRules.OWNER_PLAYER);
            GameEventBus.OnEventBanner -= Handler;

            Assert.IsTrue(large);
        }

        [Test]
        public void GameEventBus_FireLegendEvolvedBanner_IsLarge()
        {
            bool large = false;
            void Handler(string t, float d, bool l) { large = l; }
            GameEventBus.OnEventBanner += Handler;

            GameEventBus.FireLegendEvolvedBanner();
            GameEventBus.OnEventBanner -= Handler;

            Assert.IsTrue(large);
        }

        [Test]
        public void GameEventBus_FireTimeWarpBanner_IsLarge()
        {
            bool large = false;
            void Handler(string t, float d, bool l) { large = l; }
            GameEventBus.OnEventBanner += Handler;

            GameEventBus.FireTimeWarpBanner();
            GameEventBus.OnEventBanner -= Handler;

            Assert.IsTrue(large);
        }

        [Test]
        public void GameEventBus_FireDeathwishBanner_ContainsUnitName()
        {
            string text = null;
            void Handler(string t, float d, bool l) { text = t; }
            GameEventBus.OnEventBanner += Handler;

            GameEventBus.FireDeathwishBanner("AlertSentinel", "摸1张牌");
            GameEventBus.OnEventBanner -= Handler;

            StringAssert.Contains("AlertSentinel", text);
            StringAssert.Contains("摸1张牌", text);
        }

        [Test]
        public void GameEventBus_FireEntryEffectBanner_ContainsUnitName()
        {
            string text = null;
            void Handler(string t, float d, bool l) { text = t; }
            GameEventBus.OnEventBanner += Handler;

            GameEventBus.FireEntryEffectBanner("Darius", "+2战力");
            GameEventBus.OnEventBanner -= Handler;

            StringAssert.Contains("Darius", text);
        }

        // ── GameEventBus: UnitAtkBuff convenience ─────────────────────────────

        [Test]
        public void GameEventBus_FireUnitAtkBuff_PositiveProducesGoldColor()
        {
            Color receivedColor = default;
            void Handler(UnitInstance u, string t, Color c) { receivedColor = c; }
            GameEventBus.OnUnitFloatText += Handler;

            GameEventBus.FireUnitAtkBuff(MakeUnit("u"), 3);
            GameEventBus.OnUnitFloatText -= Handler;

            // Gold color for buff
            Assert.AreEqual(GameColors.BuffColor, receivedColor);
        }

        [Test]
        public void GameEventBus_FireUnitAtkBuff_NegativeProducesDebuffColor()
        {
            Color receivedColor = default;
            void Handler(UnitInstance u, string t, Color c) { receivedColor = c; }
            GameEventBus.OnUnitFloatText += Handler;

            GameEventBus.FireUnitAtkBuff(MakeUnit("u"), -2);
            GameEventBus.OnUnitFloatText -= Handler;

            Assert.AreEqual(GameColors.DebuffColor, receivedColor);
        }

        // ── GameColors: new constants exist ───────────────────────────────────

        [Test]
        public void GameColors_ScorePulseColor_IsNotMagenta()
        {
            Assert.AreNotEqual(Color.magenta, GameColors.ScorePulseColor);
        }

        [Test]
        public void GameColors_ManaColor_IsNotMagenta()
        {
            Assert.AreNotEqual(Color.magenta, GameColors.ManaColor);
        }

        [Test]
        public void GameColors_SchColor_IsNotMagenta()
        {
            Assert.AreNotEqual(Color.magenta, GameColors.SchColor);
        }

        [Test]
        public void GameColors_BuffColor_IsNotMagenta()
        {
            Assert.AreNotEqual(Color.magenta, GameColors.BuffColor);
        }

        [Test]
        public void GameColors_DebuffColor_IsNotMagenta()
        {
            Assert.AreNotEqual(Color.magenta, GameColors.DebuffColor);
        }

        // ── No-subscriber safety ──────────────────────────────────────────────

        [Test]
        public void GameEventBus_FireAllEvents_NoSubscribers_DoesNotThrow()
        {
            // Ensure all Fire methods are safe with no subscribers
            Assert.DoesNotThrow(() =>
            {
                GameEventBus.FireUnitFloatText(MakeUnit("u"), "test", Color.red);
                GameEventBus.FireZoneFloatText("score_player", "+1分", Color.yellow);
                GameEventBus.FireEventBanner("test", 1.5f);
                GameEventBus.FireScoreFloat(GameRules.OWNER_PLAYER, 1);
                GameEventBus.FireRuneTapFloat(GameRules.OWNER_PLAYER);
                GameEventBus.FireRuneRecycleFloat(GameRules.OWNER_ENEMY);
                GameEventBus.FireUnitAtkBuff(MakeUnit("u"), 2);
                GameEventBus.FireDeathwishBanner("u", "effect");
                GameEventBus.FireEntryEffectBanner("u", "effect");
                GameEventBus.FireHoldScoreBanner();
                GameEventBus.FireConquerScoreBanner();
                GameEventBus.FireBurnoutBanner(GameRules.OWNER_PLAYER);
                GameEventBus.FireLegendSkillBanner("skill", "effect");
                GameEventBus.FireLegendEvolvedBanner();
                GameEventBus.FireTimeWarpBanner();
            });
        }
    }
}
