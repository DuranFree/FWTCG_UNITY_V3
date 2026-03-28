using NUnit.Framework;
using System.Collections.Generic;
using FWTCG.Core;
using FWTCG.Data;
using UnityEngine;

namespace FWTCG.Tests.EditMode
{
    /// <summary>
    /// EditMode unit tests for core game rules.
    /// Run via Unity Test Runner → EditMode tab.
    /// </summary>
    public class GameStateTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Creates a minimal CardData via ScriptableObject.CreateInstance.</summary>
        private CardData MakeCardData(string name, int cost, int atk, RuneType runeType = RuneType.Blazing)
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
#if UNITY_EDITOR
            data.EditorSetup(name.ToLower(), name, cost, atk, runeType, 0, "");
#endif
            // Force-set via reflection so tests work without UNITY_EDITOR define
            SetPrivate(data, "_id", name.ToLower());
            SetPrivate(data, "_cardName", name);
            SetPrivate(data, "_cost", cost);
            SetPrivate(data, "_atk", atk);
            SetPrivate(data, "_runeType", runeType);
            return data;
        }

        private void SetPrivate(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(obj, value);
        }

        private GameState MakeGameState()
        {
            GameState.ResetUidCounter();
            return new GameState();
        }

        // ── Tests: Constants ──────────────────────────────────────────────────

        [Test]
        public void WinScore_IsEight()
        {
            Assert.AreEqual(8, GameRules.WIN_SCORE,
                "WIN_SCORE must be 8 per game rules.");
        }

        [Test]
        public void HandSize_IsUnlimited()
        {
            // No hand size cap — players can hold any number of cards
            var gs = MakeGameState();
            CardData data = MakeCardData("Card", 1, 1);
            for (int i = 0; i < 20; i++)
                gs.PHand.Add(gs.MakeUnit(data, GameRules.OWNER_PLAYER));
            Assert.AreEqual(20, gs.PHand.Count, "Hand size should be unlimited");
        }

        [Test]
        public void BFSlots_AreUnlimited()
        {
            // No unit cap per battlefield side
            var gs = MakeGameState();
            BattlefieldState bf = gs.BF[0];
            Assert.IsTrue(bf.HasSlot(GameRules.OWNER_PLAYER),
                "HasSlot must always return true (no cap)");
        }

        [Test]
        public void InitialHandSize_IsFour()
        {
            Assert.AreEqual(4, GameRules.INITIAL_HAND_SIZE,
                "INITIAL_HAND_SIZE must be 4 per game rules.");
        }

        // ── Tests: atk = HP core rule ─────────────────────────────────────────

        [Test]
        public void UnitInstance_AtkEqualsHP_OnCreation()
        {
            CardData data = MakeCardData("TestUnit", 2, 3);
            var gs = MakeGameState();
            UnitInstance unit = gs.MakeUnit(data, GameRules.OWNER_PLAYER);

            Assert.AreEqual(3, unit.Atk, "Base atk should equal CardData.Atk");
            Assert.AreEqual(3, unit.CurrentAtk, "CurrentAtk should equal Atk on creation");
            Assert.AreEqual(3, unit.CurrentHp, "CurrentHp must equal Atk on creation (atk=HP rule)");
        }

        [Test]
        public void UnitInstance_DifferentAtkValues_AllCorrect()
        {
            var gs = MakeGameState();

            int[] atkValues = { 1, 2, 3, 4, 5 };
            foreach (int atk in atkValues)
            {
                CardData data = MakeCardData($"Unit{atk}", atk, atk);
                UnitInstance unit = gs.MakeUnit(data, GameRules.OWNER_PLAYER);

                Assert.AreEqual(atk, unit.CurrentHp,
                    $"Unit with atk={atk} must have currentHp={atk}");
                Assert.AreEqual(unit.CurrentAtk, unit.CurrentHp,
                    $"currentAtk must equal currentHp for atk={atk}");
            }
        }

        [Test]
        public void UnitInstance_ResetEndOfTurn_RestoresHP()
        {
            CardData data = MakeCardData("Fighter", 3, 4);
            var gs = MakeGameState();
            UnitInstance unit = gs.MakeUnit(data, GameRules.OWNER_PLAYER);

            // Simulate taking damage by lowering HP
            unit.CurrentHp = 1;
            unit.Stunned = true;

            unit.ResetEndOfTurn();

            Assert.AreEqual(unit.CurrentAtk, unit.CurrentHp,
                "ResetEndOfTurn must restore CurrentHp to CurrentAtk");
            Assert.IsFalse(unit.Stunned,
                "ResetEndOfTurn must clear Stunned");
        }

        [Test]
        public void UnitInstance_EffectiveAtk_NeverBelowOne()
        {
            CardData data = MakeCardData("WeakUnit", 1, 1);
            var gs = MakeGameState();
            UnitInstance unit = gs.MakeUnit(data, GameRules.OWNER_PLAYER);

            unit.CurrentAtk = -5; // Extreme debuff
            Assert.AreEqual(1, unit.EffectiveAtk(),
                "EffectiveAtk must return at least 1");
        }

        // ── Tests: Hand (no limit) ────────────────────────────────────────────

        [Test]
        public void Hand_CanHoldManyCards()
        {
            var gs = MakeGameState();
            CardData data = MakeCardData("Card", 1, 1);

            for (int i = 0; i < 15; i++)
                gs.PHand.Add(gs.MakeUnit(data, GameRules.OWNER_PLAYER));

            Assert.AreEqual(15, gs.PHand.Count,
                "Hand has no size cap — should hold 15 cards");
        }

        // ── Tests: Battlefield (no slot limit) ────────────────────────────────

        [Test]
        public void BattlefieldState_HasSlot_AlwaysTrue()
        {
            var gs = MakeGameState();
            CardData data = MakeCardData("Soldier", 2, 2);
            BattlefieldState bf = gs.BF[0];

            // Fill with many units — HasSlot should remain true
            for (int i = 0; i < 10; i++)
                bf.PlayerUnits.Add(gs.MakeUnit(data, GameRules.OWNER_PLAYER));

            Assert.IsTrue(bf.HasSlot(GameRules.OWNER_PLAYER),
                "HasSlot must always return true (no unit cap)");
            Assert.IsTrue(bf.HasSlot(GameRules.OWNER_ENEMY),
                "HasSlot must always return true for enemy");
        }

        // ── Tests: Score ──────────────────────────────────────────────────────

        [Test]
        public void GameState_AddScore_IncrementsCorrectly()
        {
            var gs = MakeGameState();

            gs.AddScore(GameRules.OWNER_PLAYER, 3);
            gs.AddScore(GameRules.OWNER_ENEMY, 2);

            Assert.AreEqual(3, gs.PScore, "Player score should be 3");
            Assert.AreEqual(2, gs.EScore, "Enemy score should be 2");
        }

        [Test]
        public void GameState_GetScore_ReturnsCorrectOwner()
        {
            var gs = MakeGameState();
            gs.PScore = 5;
            gs.EScore = 7;

            Assert.AreEqual(5, gs.GetScore(GameRules.OWNER_PLAYER));
            Assert.AreEqual(7, gs.GetScore(GameRules.OWNER_ENEMY));
        }

        // ── Tests: Schematic energy ───────────────────────────────────────────

        [Test]
        public void GameState_AddSch_IncreasesCorrectType()
        {
            var gs = MakeGameState();

            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 3);
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Radiant, 1);

            Assert.AreEqual(3, gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing));
            Assert.AreEqual(1, gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Radiant));
        }

        [Test]
        public void GameState_ResetSch_SetsAllToZero()
        {
            var gs = MakeGameState();

            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Blazing, 5);
            gs.AddSch(GameRules.OWNER_PLAYER, RuneType.Verdant, 3);
            gs.ResetSch(GameRules.OWNER_PLAYER);

            Assert.AreEqual(0, gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Blazing));
            Assert.AreEqual(0, gs.GetSch(GameRules.OWNER_PLAYER, RuneType.Verdant));
            Assert.AreEqual(0, gs.GetSch(GameRules.OWNER_PLAYER));
        }

        // ── Tests: Battlefield power calculation ──────────────────────────────

        [Test]
        public void BattlefieldState_TotalPower_SumsAllUnits()
        {
            var gs = MakeGameState();
            BattlefieldState bf = gs.BF[0];

            CardData data2 = MakeCardData("Unit2", 2, 2);
            CardData data3 = MakeCardData("Unit3", 3, 3);

            bf.PlayerUnits.Add(gs.MakeUnit(data2, GameRules.OWNER_PLAYER));
            bf.PlayerUnits.Add(gs.MakeUnit(data3, GameRules.OWNER_PLAYER));

            Assert.AreEqual(5, bf.TotalPower(GameRules.OWNER_PLAYER),
                "TotalPower should sum all unit effective attacks (2+3=5)");
        }

        [Test]
        public void BattlefieldState_TotalPower_EmptyIsZero()
        {
            var gs = MakeGameState();
            BattlefieldState bf = gs.BF[0];

            Assert.AreEqual(0, bf.TotalPower(GameRules.OWNER_PLAYER));
            Assert.AreEqual(0, bf.TotalPower(GameRules.OWNER_ENEMY));
        }

        // ── Tests: UID uniqueness ─────────────────────────────────────────────

        [Test]
        public void GameState_UIDs_AreUnique()
        {
            GameState.ResetUidCounter();
            var gs = MakeGameState();
            CardData data = MakeCardData("Unit", 1, 1);

            var seen = new HashSet<int>();
            for (int i = 0; i < 20; i++)
            {
                UnitInstance u = gs.MakeUnit(data, GameRules.OWNER_PLAYER);
                Assert.IsTrue(seen.Add(u.Uid), $"UID {u.Uid} was duplicated");
            }
        }

        // ── Tests: Rune deck composition ──────────────────────────────────────

        [Test]
        public void GameRules_RuneDeckCounts_MatchSpec()
        {
            Assert.AreEqual(7, GameRules.RUNE_DECK_BLAZING,
                "Kaisa rune deck: 7 Blazing");
            Assert.AreEqual(5, GameRules.RUNE_DECK_RADIANT,
                "Kaisa rune deck: 5 Radiant");
            Assert.AreEqual(6, GameRules.RUNE_DECK_VERDANT,
                "Yi rune deck: 6 Verdant");
            Assert.AreEqual(6, GameRules.RUNE_DECK_CRUSHING,
                "Yi rune deck: 6 Crushing");

            Assert.AreEqual(12,
                GameRules.RUNE_DECK_BLAZING + GameRules.RUNE_DECK_RADIANT,
                "Kaisa rune deck total should be 12");
            Assert.AreEqual(12,
                GameRules.RUNE_DECK_VERDANT + GameRules.RUNE_DECK_CRUSHING,
                "Yi rune deck total should be 12");
        }
    }
}
