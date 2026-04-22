using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;

namespace FWTCG.Tests
{
    /// <summary>
    /// DEV-6: BattlefieldSystem tests
    /// Covers all 6 active BF cards assigned in Kaisa/Yi pools plus key passive checks:
    ///   Kaisa pool: star_peak, void_gate, strength_obelisk
    ///   Yi pool:    thunder_rune, ascending_stairs, forgotten_monument
    /// Also covers: vile_throat_nest recall block, rockfall_path play block,
    ///              reckoner_arena StrongAtk/Guard, dreaming_tree draw,
    ///              void_gate spell damage bonus, ascending_stairs score bonus,
    ///              forgotten_monument hold block.
    /// </summary>
    public class DEV6BattlefieldTests
    {
        private GameState      _gs;
        private BattlefieldSystem _bfSys;

        // ── Helpers ───────────────────────────────────────────────────────────

        private CardData MakeCard(string id, string name, int cost = 1, int atk = 2,
                                  RuneType rune = RuneType.Blazing, int runeCost = 1,
                                  bool isSpell = false)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(id, name, cost, atk, rune, runeCost, "", CardKeyword.None, id,
                           false, 0, RuneType.Blazing, 0, isSpell);
            return cd;
        }

        private UnitInstance MakeUnit(string id, string name, int atk = 2,
                                      string owner = GameRules.OWNER_PLAYER,
                                      RuneType rune = RuneType.Blazing)
        {
            var cd = MakeCard(id, name, atk: atk, rune: rune);
            return _gs.MakeUnit(cd, owner);
        }

        private static int _runeUid = 1000;
        private RuneInstance MakeRune(RuneType type = RuneType.Blazing)
        {
            return new RuneInstance(_runeUid++, type);
        }

        private void SetBF(int idx, string cardId)
        {
            _gs.BFNames[idx] = cardId;
        }

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _gs = new GameState();
            _gs.Round = 2;
            _gs.Turn  = GameRules.OWNER_PLAYER;
            _gs.Phase = GameRules.PHASE_ACTION;
            _gs.SetMana(GameRules.OWNER_PLAYER, 5);
            _gs.SetMana(GameRules.OWNER_ENEMY,  5);

            _bfSys = new GameObject("BFSys").AddComponent<BattlefieldSystem>();

            // Default: no special BF
            SetBF(0, "none");
            SetBF(1, "none");
        }

        // ── forgotten_monument ────────────────────────────────────────────────

        [Test]
        public void ForgottenMonument_BlocksHoldBeforeRound2()
        {
            SetBF(0, "forgotten_monument");
            _gs.Round = 1; // before round 2

            bool blocked = _bfSys.ShouldBlockHoldScore(0, _gs);
            Assert.IsTrue(blocked, "Should block hold score before round 2");
        }

        [Test]
        public void ForgottenMonument_AllowsHoldAtRound2()
        {
            SetBF(0, "forgotten_monument");
            _gs.Round = 2; // round 2 is allowed

            bool blocked = _bfSys.ShouldBlockHoldScore(0, _gs);
            Assert.IsFalse(blocked, "Should NOT block hold at round >= 2");
        }

        [Test]
        public void ForgottenMonument_DoesNotAffectOtherBF()
        {
            SetBF(0, "forgotten_monument");
            SetBF(1, "none");
            _gs.Round = 0;

            bool blocked = _bfSys.ShouldBlockHoldScore(1, _gs);
            Assert.IsFalse(blocked, "Other BF should not be blocked");
        }

        // ── ascending_stairs ──────────────────────────────────────────────────
        // B-SCORE-1: 卡面 "使赢得游戏所需的分数 +1"。
        // 不再在得分时额外 +1；改为通过 EffectiveWinScore 提升胜利门槛。

        [Test]
        public void AscendingStairs_DoesNotGiveBonusOnHold()
        {
            SetBF(0, "ascending_stairs");
            int bonus = _bfSys.GetBonusScorePoints(0, GameRules.SCORE_TYPE_HOLD, _gs);
            Assert.AreEqual(0, bonus, "据守不再额外 +1 分");
        }

        [Test]
        public void AscendingStairs_DoesNotGiveBonusOnConquer()
        {
            SetBF(0, "ascending_stairs");
            int bonus = _bfSys.GetBonusScorePoints(0, GameRules.SCORE_TYPE_CONQUER, _gs);
            Assert.AreEqual(0, bonus, "征服不再额外 +1 分");
        }

        [Test]
        public void AscendingStairs_RaisesWinScoreBy1()
        {
            SetBF(0, "ascending_stairs");
            SetBF(1, "none");
            Assert.AreEqual(GameRules.WIN_SCORE + 1,
                BattlefieldSystem.EffectiveWinScore(_gs),
                "攀圣长阶 → 胜利门槛 +1");
        }

        [Test]
        public void AscendingStairs_NotPresent_KeepsDefaultWinScore()
        {
            SetBF(0, "none");
            SetBF(1, "none");
            Assert.AreEqual(GameRules.WIN_SCORE,
                BattlefieldSystem.EffectiveWinScore(_gs),
                "无攀圣长阶 → 胜利门槛不变");
        }

        [Test]
        public void GetBonusScorePoints_AscendingStairs_AlwaysZero()
        {
            SetBF(0, "ascending_stairs");
            Assert.AreEqual(0, _bfSys.GetBonusScorePoints(0, GameRules.SCORE_TYPE_HOLD, _gs));
            Assert.AreEqual(0, _bfSys.GetBonusScorePoints(0, GameRules.SCORE_TYPE_CONQUER, _gs));
            Assert.AreEqual(0, _bfSys.GetBonusScorePoints(0, GameRules.SCORE_TYPE_BURNOUT, _gs));
        }

        // ── strength_obelisk ──────────────────────────────────────────────────

        [Test]
        public void StrengthObelisk_HoldDrawsExtraRune()
        {
            SetBF(0, "strength_obelisk");

            var rune = MakeRune();
            _gs.GetRuneDeck(GameRules.OWNER_PLAYER).Add(rune);
            int before = _gs.GetRunes(GameRules.OWNER_PLAYER).Count;

            _bfSys.OnHoldPhaseEffects(0, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(before + 1, _gs.GetRunes(GameRules.OWNER_PLAYER).Count);
        }

        [Test]
        public void StrengthObelisk_HoldDoesNotExceedMaxRunes()
        {
            SetBF(0, "strength_obelisk");

            // Fill rune zone to max
            var runes = _gs.GetRunes(GameRules.OWNER_PLAYER);
            for (int i = 0; i < GameRules.MAX_RUNES_IN_PLAY; i++)
                runes.Add(MakeRune());
            _gs.GetRuneDeck(GameRules.OWNER_PLAYER).Add(MakeRune());

            _bfSys.OnHoldPhaseEffects(0, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(GameRules.MAX_RUNES_IN_PLAY, runes.Count, "Should not exceed max runes");
        }

        // ── star_peak ─────────────────────────────────────────────────────────

        [Test]
        public void StarPeak_HoldSummonsExhaustedRune()
        {
            SetBF(0, "star_peak");

            var rune = MakeRune();
            rune.Tapped = false;
            _gs.GetRuneDeck(GameRules.OWNER_PLAYER).Add(rune);

            _bfSys.OnHoldPhaseEffects(0, GameRules.OWNER_PLAYER, _gs);

            var runes = _gs.GetRunes(GameRules.OWNER_PLAYER);
            Assert.AreEqual(1, runes.Count);
            Assert.IsTrue(runes[0].Tapped, "Summoned rune should be tapped (exhausted)");
        }

        // ── thunder_rune ──────────────────────────────────────────────────────

        [Test]
        public void ThunderRune_ConquestRecyclesTappedRune()
        {
            SetBF(0, "thunder_rune");

            var rune = MakeRune();
            rune.Tapped = true;
            _gs.GetRunes(GameRules.OWNER_PLAYER).Add(rune);

            int deckBefore = _gs.GetRuneDeck(GameRules.OWNER_PLAYER).Count;

            _bfSys.OnConquest(0, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(0, _gs.GetRunes(GameRules.OWNER_PLAYER).Count,
                            "Tapped rune should be removed from play");
            Assert.AreEqual(deckBefore + 1, _gs.GetRuneDeck(GameRules.OWNER_PLAYER).Count,
                            "Rune should be at top of deck");
            Assert.IsFalse(_gs.GetRuneDeck(GameRules.OWNER_PLAYER)[0].Tapped,
                           "Recycled rune should be untapped");
        }

        [Test]
        public void ThunderRune_ConquestDoesNothingIfNoTappedRune()
        {
            SetBF(0, "thunder_rune");

            var rune = MakeRune();
            rune.Tapped = false;
            _gs.GetRunes(GameRules.OWNER_PLAYER).Add(rune);

            int deckBefore = _gs.GetRuneDeck(GameRules.OWNER_PLAYER).Count;
            _bfSys.OnConquest(0, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(1, _gs.GetRunes(GameRules.OWNER_PLAYER).Count,
                            "Untapped rune should not be recycled");
            Assert.AreEqual(deckBefore, _gs.GetRuneDeck(GameRules.OWNER_PLAYER).Count);
        }

        // ── void_gate ─────────────────────────────────────────────────────────

        [Test]
        public void VoidGate_AddsSpellDamageBonusForUnitOnBF()
        {
            SetBF(0, "void_gate");

            var unit = MakeUnit("u1", "Test", owner: GameRules.OWNER_ENEMY);
            _gs.BF[0].EnemyUnits.Add(unit);

            int bonus = _bfSys.GetSpellDamageBonus(unit, _gs);
            Assert.AreEqual(1, bonus);
        }

        [Test]
        public void VoidGate_NoBonusForUnitOnOtherBF()
        {
            SetBF(0, "void_gate");
            SetBF(1, "none");

            var unit = MakeUnit("u1", "Test", owner: GameRules.OWNER_ENEMY);
            _gs.BF[1].EnemyUnits.Add(unit); // BF1, not BF0

            int bonus = _bfSys.GetSpellDamageBonus(unit, _gs);
            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void VoidGate_NoBonusForUnitNotOnAnyBF()
        {
            SetBF(0, "void_gate");

            var unit = MakeUnit("u1", "Test", owner: GameRules.OWNER_ENEMY);
            _gs.GetBase(GameRules.OWNER_ENEMY).Add(unit);

            int bonus = _bfSys.GetSpellDamageBonus(unit, _gs);
            Assert.AreEqual(0, bonus);
        }

        // ── vile_throat_nest ──────────────────────────────────────────────────

        [Test]
        public void VileThroatNest_BlocksRecall()
        {
            SetBF(0, "vile_throat_nest");

            bool canRecall = _bfSys.CanRecallFromBattlefield(0, _gs);
            Assert.IsFalse(canRecall);
        }

        [Test]
        public void VileThroatNest_DoesNotBlockOtherBF()
        {
            SetBF(0, "vile_throat_nest");
            SetBF(1, "none");

            bool canRecall = _bfSys.CanRecallFromBattlefield(1, _gs);
            Assert.IsTrue(canRecall);
        }

        // ── rockfall_path ─────────────────────────────────────────────────────

        [Test]
        public void RockfallPath_BlocksDirectPlay()
        {
            SetBF(0, "rockfall_path");

            bool canPlay = _bfSys.CanPlayDirectlyToBattlefield(0, _gs);
            Assert.IsFalse(canPlay);
        }

        [Test]
        public void RockfallPath_DoesNotBlockOtherBF()
        {
            SetBF(0, "rockfall_path");
            SetBF(1, "none");

            bool canPlay = _bfSys.CanPlayDirectlyToBattlefield(1, _gs);
            Assert.IsTrue(canPlay);
        }

        // ── reckoner_arena ────────────────────────────────────────────────────

        [Test]
        public void ReckonerArena_GrantsStrongAtkToHighPowerAttacker()
        {
            SetBF(0, "reckoner_arena");

            var unit = MakeUnit("u1", "Bruiser", atk: 5); // power >= STRONG_POWER_THRESHOLD (5)
            _gs.BF[0].PlayerUnits.Add(unit);

            _bfSys.OnCombatStart(0, GameRules.OWNER_PLAYER, _gs);

            Assert.IsTrue(unit.HasStrongAtk, "High-power attacker should gain StrongAtk");
        }

        [Test]
        public void ReckonerArena_GrantsGuardToHighPowerDefender()
        {
            SetBF(0, "reckoner_arena");

            var unit = MakeUnit("u1", "Guard", atk: 5, owner: GameRules.OWNER_ENEMY);
            _gs.BF[0].EnemyUnits.Add(unit);

            // Player is attacker → enemy is defender
            _bfSys.OnCombatStart(0, GameRules.OWNER_PLAYER, _gs);

            Assert.IsTrue(unit.HasGuard, "High-power defender should gain Guard");
        }

        [Test]
        public void ReckonerArena_NoKeywordForLowPowerUnit()
        {
            SetBF(0, "reckoner_arena");

            var unit = MakeUnit("u1", "Weakling", atk: 2); // power < 5
            _gs.BF[0].PlayerUnits.Add(unit);

            _bfSys.OnCombatStart(0, GameRules.OWNER_PLAYER, _gs);

            Assert.IsFalse(unit.HasStrongAtk);
            Assert.IsFalse(unit.HasGuard);
        }

        // ── dreaming_tree ─────────────────────────────────────────────────────

        [Test]
        public void DreamingTree_DrawsCardWhenSpellTargetsFriendlyUnit()
        {
            SetBF(0, "dreaming_tree");

            var friendly = MakeUnit("u1", "Friend", owner: GameRules.OWNER_PLAYER);
            _gs.BF[0].PlayerUnits.Add(friendly);

            var deck = _gs.GetDeck(GameRules.OWNER_PLAYER);
            var card = MakeUnit("c1", "DrawMe");
            deck.Add(card);

            int handBefore = _gs.GetHand(GameRules.OWNER_PLAYER).Count;
            _bfSys.OnSpellTargetsFriendlyUnit(friendly, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(handBefore + 1, _gs.GetHand(GameRules.OWNER_PLAYER).Count);
        }

        [Test]
        public void DreamingTree_TriggersOnlyOncePerTurn()
        {
            SetBF(0, "dreaming_tree");

            var friendly = MakeUnit("u1", "Friend", owner: GameRules.OWNER_PLAYER);
            _gs.BF[0].PlayerUnits.Add(friendly);

            for (int i = 0; i < 3; i++)
                _gs.GetDeck(GameRules.OWNER_PLAYER).Add(MakeUnit($"c{i}", "Card"));

            // Trigger twice
            _bfSys.OnSpellTargetsFriendlyUnit(friendly, GameRules.OWNER_PLAYER, _gs);
            _bfSys.OnSpellTargetsFriendlyUnit(friendly, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(1, _gs.GetHand(GameRules.OWNER_PLAYER).Count,
                            "Should only draw once per turn");
        }

        [Test]
        public void DreamingTree_DoesNotTriggerForEnemyUnit()
        {
            SetBF(0, "dreaming_tree");

            var enemy = MakeUnit("u1", "Enemy", owner: GameRules.OWNER_ENEMY);
            _gs.BF[0].EnemyUnits.Add(enemy);

            _gs.GetDeck(GameRules.OWNER_PLAYER).Add(MakeUnit("c1", "Card"));

            // Player targets enemy unit — should not trigger dreaming_tree
            _bfSys.OnSpellTargetsFriendlyUnit(enemy, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(0, _gs.GetHand(GameRules.OWNER_PLAYER).Count);
        }
    }
}
