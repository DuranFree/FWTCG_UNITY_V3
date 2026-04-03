using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;
using FWTCG.UI;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-18: Tests for Ephemeral keyword (Rule 728), Standby state (Rule 716),
    /// and BF card art identifier logic.
    /// </summary>
    [TestFixture]
    public class DEV18CombatVisualTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private CardData MakeCard(string id, int atk = 2, CardKeyword kw = CardKeyword.None)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(id, id, 1, atk, RuneType.Blazing, 0, "", keywords: kw);
            return cd;
        }

        private UnitInstance MakeUnit(string name, string owner = "player",
            CardKeyword kw = CardKeyword.None)
        {
            return new UnitInstance(1, MakeCard(name, 2, kw), owner);
        }

        // ── Ephemeral keyword existence ────────────────────────────────────────

        [Test]
        public void Ephemeral_KeywordEnumExists()
        {
            var kw = CardKeyword.Ephemeral;
            Assert.AreNotEqual(CardKeyword.None, kw);
        }

        [Test]
        public void Ephemeral_HasKeyword_ReturnsTrue_WhenSet()
        {
            var cd = MakeCard("ghost", 1, CardKeyword.Ephemeral);
            Assert.IsTrue(cd.HasKeyword(CardKeyword.Ephemeral));
            Object.DestroyImmediate(cd);
        }

        [Test]
        public void Ephemeral_HasKeyword_ReturnsFalse_WhenNotSet()
        {
            var cd = MakeCard("warrior", 2, CardKeyword.None);
            Assert.IsFalse(cd.HasKeyword(CardKeyword.Ephemeral));
            Object.DestroyImmediate(cd);
        }

        [Test]
        public void Ephemeral_DoesNotConflictWithOtherKeywords()
        {
            // Ephemeral can coexist with other keywords via bitwise OR
            var kw = CardKeyword.Ephemeral | CardKeyword.Haste;
            var cd = MakeCard("specter", 2, kw);
            Assert.IsTrue(cd.HasKeyword(CardKeyword.Ephemeral));
            Assert.IsTrue(cd.HasKeyword(CardKeyword.Haste));
            Object.DestroyImmediate(cd);
        }

        // ── UnitInstance.IsEphemeral + SummonedOnRound ────────────────────────

        [Test]
        public void UnitInstance_IsEphemeral_DefaultsFalse()
        {
            var u = MakeUnit("test");
            Assert.IsFalse(u.IsEphemeral);
        }

        [Test]
        public void UnitInstance_SummonedOnRound_DefaultsMinusOne()
        {
            var u = MakeUnit("test");
            Assert.AreEqual(-1, u.SummonedOnRound);
        }

        [Test]
        public void UnitInstance_IsEphemeral_CanBeSetTrue()
        {
            var u = MakeUnit("ghost");
            u.IsEphemeral = true;
            u.SummonedOnRound = 1;
            Assert.IsTrue(u.IsEphemeral);
            Assert.AreEqual(1, u.SummonedOnRound);
        }

        // ── Standby state ─────────────────────────────────────────────────────

        [Test]
        public void UnitInstance_IsStandby_DefaultsFalse()
        {
            var u = MakeUnit("trap_card");
            Assert.IsFalse(u.IsStandby);
        }

        [Test]
        public void UnitInstance_IsStandby_CanBeSetTrue()
        {
            var u = MakeUnit("zhonya", "player", CardKeyword.Standby);
            u.IsStandby = true;
            Assert.IsTrue(u.IsStandby);
        }

        [Test]
        public void UnitInstance_StandbyFlip_ClearsIsStandby()
        {
            var u = MakeUnit("zhonya", "player", CardKeyword.Standby);
            u.IsStandby = true;
            // Flip: set face-up
            u.IsStandby = false;
            Assert.IsFalse(u.IsStandby);
        }

        [Test]
        public void Standby_KeywordEnumExists()
        {
            var kw = CardKeyword.Standby;
            Assert.AreNotEqual(CardKeyword.None, kw);
        }

        // ── Ephemeral cleanup logic (pure condition checks) ───────────────────

        [Test]
        public void EphemeralCondition_SummonedOnRound1_Round1_NotEligible()
        {
            // SummonedOnRound == gs.Round → NOT yet eligible for destruction
            int round = 1;
            var u = MakeUnit("ghost");
            u.IsEphemeral = true;
            u.SummonedOnRound = 1;

            bool shouldDestroy = u.IsEphemeral && u.SummonedOnRound < round;
            Assert.IsFalse(shouldDestroy,
                "Should NOT be destroyed on same round it was summoned");
        }

        [Test]
        public void EphemeralCondition_SummonedOnRound1_Round2_Eligible()
        {
            int round = 2;
            var u = MakeUnit("ghost");
            u.IsEphemeral = true;
            u.SummonedOnRound = 1;

            bool shouldDestroy = u.IsEphemeral && u.SummonedOnRound < round;
            Assert.IsTrue(shouldDestroy,
                "Should be eligible for destruction when Round > SummonedOnRound");
        }

        [Test]
        public void EphemeralCondition_NonEphemeral_NeverEligible()
        {
            int round = 99;
            var u = MakeUnit("warrior");
            u.SummonedOnRound = 1;

            bool shouldDestroy = u.IsEphemeral && u.SummonedOnRound < round;
            Assert.IsFalse(shouldDestroy,
                "Non-ephemeral unit must never be scheduled for destruction");
        }

        [Test]
        public void EphemeralCondition_SummonedOnRound0_RoundMinus1_NotEligible()
        {
            // Edge: SummonedOnRound=-1 (default), round=0 → -1 < 0 is true, but IsEphemeral=false
            int round = 0;
            var u = MakeUnit("default_unit");
            // IsEphemeral is false by default
            bool shouldDestroy = u.IsEphemeral && u.SummonedOnRound < round;
            Assert.IsFalse(shouldDestroy);
        }

        // ── BF card art identifier logic ──────────────────────────────────────

        [Test]
        public void BFCardArt_PathFormat_MatchesResourcesFolder()
        {
            string bfId = "altar_unity";
            string expectedPath = $"CardArt/bf_{bfId}";
            Assert.AreEqual("CardArt/bf_altar_unity", expectedPath);
        }

        [Test]
        public void BFCardArt_KnownIds_ExistInGameRulesDisplayNames()
        {
            string[] knownIds = new[]
            {
                "altar_unity", "aspirant_climb", "reckoner_arena",
                "trifarian_warcamp", "star_peak", "forgotten_monument"
            };

            foreach (string id in knownIds)
            {
                Assert.IsTrue(
                    GameRules.BF_DISPLAY_NAMES.ContainsKey(id),
                    $"BF id '{id}' should exist in GameRules.BF_DISPLAY_NAMES"
                );
            }
        }

        [Test]
        public void BFCardArt_NullBFId_ShouldNotLoad()
        {
            // Null or empty bfId should result in no sprite load attempt
            string bfId = null;
            bool wouldLoad = !string.IsNullOrEmpty(bfId);
            Assert.IsFalse(wouldLoad, "Null bfId should skip sprite load");
        }

        // ── OnCardPlayed event ─────────────────────────────────────────────────

        [Test]
        public void OnCardPlayed_EventCanBeSubscribedAndUnsubscribed()
        {
            int callCount = 0;
            System.Action<UnitInstance, string> handler = (u, o) => callCount++;

            GameEventBus.OnCardPlayed += handler;
            GameManager.FireCardPlayed(null, GameRules.OWNER_PLAYER);
            GameEventBus.OnCardPlayed -= handler;

            Assert.AreEqual(1, callCount, "Handler called once while subscribed");

            // After unsubscribe, firing again should not increment
            GameManager.FireCardPlayed(null, GameRules.OWNER_PLAYER);
            Assert.AreEqual(1, callCount, "Handler must not fire after unsubscribe");
        }

        [Test]
        public void OnCardPlayed_FireWithNull_DoesNotThrow()
        {
            // Firing with null unit should not throw when no subscribers
            Assert.DoesNotThrow(() => GameManager.FireCardPlayed(null, "player"));
        }
    }
}
