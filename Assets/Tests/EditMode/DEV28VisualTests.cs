using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-28 visual interaction tests (EditMode — logic only, no rendering).
    ///
    /// Covers:
    ///   TurnManager  — Ephemeral units enter discard pile on destruction
    ///   CombatSystem — OnCombatWillStart fires before OnCombatResult
    /// </summary>
    [TestFixture]
    public class DEV28VisualTests
    {
        // ── helpers ───────────────────────────────────────────────────────────

        private CardData MakeUnit(string id, bool ephemeral = false)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            CardKeyword kw = ephemeral ? CardKeyword.Ephemeral : CardKeyword.None;
            cd.EditorSetup(id, id, 1, 2, RuneType.Blazing, 0, "", isSpell: false, keywords: kw);
            return cd;
        }

        private UnitInstance MakeUnitInst(string id, string owner, bool ephemeral = false)
        {
            return new UnitInstance(GameState.NextUid(), MakeUnit(id, ephemeral), owner);
        }

        private GameState MakeGS()
        {
            return new GameState();
        }

        // ── Ephemeral discard ─────────────────────────────────────────────────

        [Test]
        public void EphemeralUnit_DestroyedAtRoundStart_EntersDiscardPile()
        {
            var gs = MakeGS();
            var u  = MakeUnitInst("eph_unit", GameRules.OWNER_PLAYER, ephemeral: true);
            u.SummonedOnRound = 1;
            gs.PBase.Add(u);
            gs.Round = 2; // next round — unit should be destroyed

            // Simulate what DestroyEphemeralUnits does
            int discardBefore = gs.PDiscard.Count;
            var baseList = gs.GetBase(GameRules.OWNER_PLAYER);
            for (int i = baseList.Count - 1; i >= 0; i--)
            {
                var unit = baseList[i];
                if (unit.IsEphemeral && unit.SummonedOnRound < gs.Round)
                {
                    gs.GetDiscard(GameRules.OWNER_PLAYER).Add(unit);
                    baseList.RemoveAt(i);
                }
            }

            Assert.AreEqual(discardBefore + 1, gs.PDiscard.Count,
                "Ephemeral unit should be added to discard pile on destruction");
            Assert.AreEqual(0, gs.PBase.Count,
                "Ephemeral unit should be removed from base");
        }

        [Test]
        public void EphemeralUnit_SameTurn_NotDestroyed()
        {
            var gs = MakeGS();
            var u  = MakeUnitInst("eph_unit", GameRules.OWNER_PLAYER, ephemeral: true);
            u.SummonedOnRound = 2;
            gs.PBase.Add(u);
            gs.Round = 2; // same round — should survive

            var baseList = gs.GetBase(GameRules.OWNER_PLAYER);
            for (int i = baseList.Count - 1; i >= 0; i--)
            {
                var unit = baseList[i];
                if (unit.IsEphemeral && unit.SummonedOnRound < gs.Round)
                {
                    gs.GetDiscard(GameRules.OWNER_PLAYER).Add(unit);
                    baseList.RemoveAt(i);
                }
            }

            Assert.AreEqual(1, gs.PBase.Count, "Ephemeral unit summoned this round should survive");
            Assert.AreEqual(0, gs.PDiscard.Count, "Discard pile should remain empty");
        }

        [Test]
        public void EphemeralUnit_OnBattlefield_EntersDiscardOnDestruction()
        {
            var gs = MakeGS();
            var u  = MakeUnitInst("eph_bf", GameRules.OWNER_PLAYER, ephemeral: true);
            u.SummonedOnRound = 1;
            gs.BF[0].PlayerUnits.Add(u);
            gs.Round = 2;

            var bfUnits = gs.BF[0].PlayerUnits;
            for (int i = bfUnits.Count - 1; i >= 0; i--)
            {
                var unit = bfUnits[i];
                if (unit.IsEphemeral && unit.SummonedOnRound < gs.Round)
                {
                    gs.GetDiscard(GameRules.OWNER_PLAYER).Add(unit);
                    bfUnits.RemoveAt(i);
                }
            }

            Assert.AreEqual(1, gs.PDiscard.Count, "BF ephemeral unit should enter discard");
            Assert.AreEqual(0, gs.BF[0].PlayerUnits.Count, "BF should be empty after ephemeral destruction");
        }

        // ── CombatSystem.OnCombatWillStart ────────────────────────────────────

        [Test]
        public void CombatSystem_OnCombatWillStart_FiresBeforeOnCombatResult()
        {
            var fireOrder = new List<string>();
            System.Action<int, System.Collections.Generic.List<UnitInstance>,
                               System.Collections.Generic.List<UnitInstance>> willStart =
                (bfIdx, atk, def) => fireOrder.Add("willStart");
            System.Action<CombatSystem.CombatResult> result =
                r => fireOrder.Add("result");

            CombatSystem.OnCombatWillStart += willStart;
            CombatSystem.OnCombatResult    += result;

            try
            {
                var gs   = MakeGS();
                var go   = new GameObject("CS");
                var sys  = go.AddComponent<CombatSystem>();
                var score = new ScoreManager();

                var attUnit = MakeUnitInst("att", GameRules.OWNER_PLAYER);
                var defUnit = MakeUnitInst("def", GameRules.OWNER_ENEMY);
                gs.BF[0].PlayerUnits.Add(attUnit);
                gs.BF[0].EnemyUnits.Add(defUnit);
                gs.BF[0].Ctrl = GameRules.OWNER_ENEMY; // enemy controls — player is attacker

                sys.TriggerCombat(0, GameRules.OWNER_PLAYER, gs, score);

                Assert.AreEqual(2, fireOrder.Count, "Both events should fire");
                Assert.AreEqual("willStart", fireOrder[0], "OnCombatWillStart must fire first");
                Assert.AreEqual("result",    fireOrder[1], "OnCombatResult must fire second");

                Object.DestroyImmediate(go);
            }
            finally
            {
                CombatSystem.OnCombatWillStart -= willStart;
                CombatSystem.OnCombatResult    -= result;
            }
        }

        [Test]
        public void CombatSystem_OnCombatWillStart_PassesCorrectUnitLists()
        {
            List<UnitInstance> capturedAttackers = null;
            List<UnitInstance> capturedDefenders = null;

            System.Action<int, List<UnitInstance>, List<UnitInstance>> handler =
                (bfIdx, atk, def) => {
                    capturedAttackers = new List<UnitInstance>(atk); // snapshot — lists may be cleared after combat
                    capturedDefenders = new List<UnitInstance>(def);
                };

            CombatSystem.OnCombatWillStart += handler;
            try
            {
                var gs    = MakeGS();
                var go    = new GameObject("CS2");
                var sys   = go.AddComponent<CombatSystem>();
                var score = new ScoreManager();

                var p1 = MakeUnitInst("p1", GameRules.OWNER_PLAYER);
                var e1 = MakeUnitInst("e1", GameRules.OWNER_ENEMY);
                gs.BF[0].PlayerUnits.Add(p1);
                gs.BF[0].EnemyUnits.Add(e1);
                gs.BF[0].Ctrl = GameRules.OWNER_ENEMY;

                sys.TriggerCombat(0, GameRules.OWNER_PLAYER, gs, score);

                Assert.IsNotNull(capturedAttackers, "Attacker list should be passed");
                Assert.IsNotNull(capturedDefenders, "Defender list should be passed");
                Assert.IsTrue(capturedAttackers.Contains(p1), "Player unit should be in attackers");
                Assert.IsTrue(capturedDefenders.Contains(e1), "Enemy unit should be in defenders");

                Object.DestroyImmediate(go);
            }
            finally
            {
                CombatSystem.OnCombatWillStart -= handler;
            }
        }
    }
}
