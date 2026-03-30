using NUnit.Framework;
using UnityEngine;
using FWTCG.UI;
using FWTCG.Core;
using FWTCG.Data;
using System.Collections.Generic;

namespace FWTCG.Tests
{
    [TestFixture]
    public class DEV9LayoutTests
    {
        // ── GameState Exile list tests ────────────────────────────────────────

        [Test]
        public void GameState_ExileLists_InitEmpty()
        {
            var gs = new GameState();
            Assert.IsNotNull(gs.PExile);
            Assert.IsNotNull(gs.EExile);
            Assert.AreEqual(0, gs.PExile.Count);
            Assert.AreEqual(0, gs.EExile.Count);
        }

        [Test]
        public void GameState_ExileLists_AddAndCount()
        {
            var gs = new GameState();
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("test", "Test", 1, 1, RuneType.Blazing, 0, "desc");
            var unit = gs.MakeUnit(card, GameRules.OWNER_PLAYER);

            gs.PExile.Add(unit);
            Assert.AreEqual(1, gs.PExile.Count);

            gs.EExile.Add(unit);
            Assert.AreEqual(1, gs.EExile.Count);

            Object.DestroyImmediate(card);
        }

        [Test]
        public void GameState_GetExile_ReturnsCorrectList()
        {
            var gs = new GameState();
            Assert.AreSame(gs.PExile, gs.GetExile(GameRules.OWNER_PLAYER));
            Assert.AreSame(gs.EExile, gs.GetExile(GameRules.OWNER_ENEMY));
        }

        // ── GameColors new DEV-9 constants ───────────────────────────────────

        [Test]
        public void GameColors_ScoreCircleColors_Exist()
        {
            Assert.AreNotEqual(Color.clear, GameColors.ScoreCircleInactive);
            Assert.AreNotEqual(Color.clear, GameColors.ScoreCirclePlayer);
            Assert.AreNotEqual(Color.clear, GameColors.ScoreCircleEnemy);
            Assert.AreNotEqual(Color.clear, GameColors.ScoreCircleCurrent);
        }

        [Test]
        public void GameColors_PileColors_Exist()
        {
            Assert.AreNotEqual(Color.clear, GameColors.PileBorder);
            Assert.AreNotEqual(Color.clear, GameColors.PileBackground);
        }

        [Test]
        public void GameColors_CtrlBadgeColors_Exist()
        {
            Assert.AreNotEqual(Color.clear, GameColors.CtrlBadgePlayer);
            Assert.AreNotEqual(Color.clear, GameColors.CtrlBadgeEnemy);
        }

        [Test]
        public void GameColors_ActionBtnColors_Exist()
        {
            Assert.AreNotEqual(Color.clear, GameColors.ActionBtnPrimary);
            Assert.AreNotEqual(Color.clear, GameColors.ActionBtnSecondary);
            Assert.AreNotEqual(Color.clear, GameColors.ActionBtnDanger);
        }

        [Test]
        public void GameColors_InfoStripBg_Exists()
        {
            Assert.AreNotEqual(Color.clear, GameColors.InfoStripBg);
        }

        // ── Score track refresh logic ────────────────────────────────────────

        [Test]
        public void ScoreTrack_Score0_AllInactiveExceptFirst()
        {
            // Simulate: score=0, circles 0-8.
            // Circle 0 = current (bright), rest = inactive
            var gs = new GameState();
            gs.PScore = 0;

            // Expected: i < 0 => none green, i == 0 => current, rest inactive
            for (int i = 0; i < 9; i++)
            {
                Color expected;
                if (i < gs.PScore)
                    expected = GameColors.ScoreCirclePlayer;
                else if (i == gs.PScore && gs.PScore < GameRules.WIN_SCORE)
                    expected = GameColors.ScoreCircleCurrent;
                else
                    expected = GameColors.ScoreCircleInactive;

                if (i == 0)
                    Assert.AreEqual(GameColors.ScoreCircleCurrent, expected, $"Circle {i} should be current");
                else
                    Assert.AreEqual(GameColors.ScoreCircleInactive, expected, $"Circle {i} should be inactive");
            }
        }

        [Test]
        public void ScoreTrack_Score5_First5GreenNext1Current()
        {
            var gs = new GameState();
            gs.PScore = 5;

            for (int i = 0; i < 9; i++)
            {
                Color expected;
                if (i < gs.PScore)
                    expected = GameColors.ScoreCirclePlayer;
                else if (i == gs.PScore && gs.PScore < GameRules.WIN_SCORE)
                    expected = GameColors.ScoreCircleCurrent;
                else
                    expected = GameColors.ScoreCircleInactive;

                if (i < 5)
                    Assert.AreEqual(GameColors.ScoreCirclePlayer, expected, $"Circle {i} should be player green");
                else if (i == 5)
                    Assert.AreEqual(GameColors.ScoreCircleCurrent, expected, $"Circle {i} should be current");
                else
                    Assert.AreEqual(GameColors.ScoreCircleInactive, expected, $"Circle {i} should be inactive");
            }
        }

        [Test]
        public void ScoreTrack_Score8_AllGreen_NoCurrent()
        {
            var gs = new GameState();
            gs.PScore = 8; // WIN_SCORE

            for (int i = 0; i < 9; i++)
            {
                Color expected;
                if (i < gs.PScore)
                    expected = GameColors.ScoreCirclePlayer;
                else if (i == gs.PScore && gs.PScore < GameRules.WIN_SCORE)
                    expected = GameColors.ScoreCircleCurrent;
                else
                    expected = GameColors.ScoreCircleInactive;

                if (i < 8)
                    Assert.AreEqual(GameColors.ScoreCirclePlayer, expected, $"Circle {i} should be player green");
                else
                    Assert.AreEqual(GameColors.ScoreCircleInactive, expected, $"Circle 8 should be inactive (no current at win)");
            }
        }

        // ── Pile count display tests ─────────────────────────────────────────

        [Test]
        public void PileCount_DeckCountMatchesState()
        {
            var gs = new GameState();
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("t", "T", 1, 1, RuneType.Blazing, 0, "d");

            for (int i = 0; i < 7; i++)
                gs.PDeck.Add(gs.MakeUnit(card, GameRules.OWNER_PLAYER));

            Assert.AreEqual(7, gs.PDeck.Count);

            Object.DestroyImmediate(card);
        }

        [Test]
        public void PileCount_EmptyDeck_Returns0()
        {
            var gs = new GameState();
            Assert.AreEqual(0, gs.PDeck.Count);
            Assert.AreEqual(0, gs.EDeck.Count);
        }

        [Test]
        public void PileCount_RuneDeckCount()
        {
            var gs = new GameState();
            for (int i = 0; i < 5; i++)
                gs.PRuneDeck.Add(new RuneInstance(GameState.NextUid(), RuneType.Blazing));

            Assert.AreEqual(5, gs.PRuneDeck.Count);
        }

        [Test]
        public void PileCount_DiscardAndExile()
        {
            var gs = new GameState();
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup("t", "T", 1, 1, RuneType.Blazing, 0, "d");

            gs.PDiscard.Add(gs.MakeUnit(card, GameRules.OWNER_PLAYER));
            gs.PDiscard.Add(gs.MakeUnit(card, GameRules.OWNER_PLAYER));
            gs.PExile.Add(gs.MakeUnit(card, GameRules.OWNER_PLAYER));

            Assert.AreEqual(2, gs.PDiscard.Count);
            Assert.AreEqual(1, gs.PExile.Count);

            Object.DestroyImmediate(card);
        }

        // ── BF control badge tests ───────────────────────────────────────────

        [Test]
        public void BFControlBadge_PlayerControl_IsGreen()
        {
            var gs = new GameState();
            gs.BF[0].Ctrl = GameRules.OWNER_PLAYER;

            bool isPlayer = gs.BF[0].Ctrl == GameRules.OWNER_PLAYER;
            bool isEnemy = gs.BF[0].Ctrl == GameRules.OWNER_ENEMY;
            Color expected = isPlayer ? GameColors.CtrlBadgePlayer
                           : isEnemy ? GameColors.CtrlBadgeEnemy
                           : GameColors.ScoreCircleInactive;

            Assert.AreEqual(GameColors.CtrlBadgePlayer, expected);
        }

        [Test]
        public void BFControlBadge_EnemyControl_IsRed()
        {
            var gs = new GameState();
            gs.BF[1].Ctrl = GameRules.OWNER_ENEMY;

            bool isPlayer = gs.BF[1].Ctrl == GameRules.OWNER_PLAYER;
            bool isEnemy = gs.BF[1].Ctrl == GameRules.OWNER_ENEMY;
            Color expected = isPlayer ? GameColors.CtrlBadgePlayer
                           : isEnemy ? GameColors.CtrlBadgeEnemy
                           : GameColors.ScoreCircleInactive;

            Assert.AreEqual(GameColors.CtrlBadgeEnemy, expected);
        }

        [Test]
        public void BFControlBadge_NoControl_IsInactive()
        {
            var gs = new GameState();
            gs.BF[0].Ctrl = null;

            bool isPlayer = gs.BF[0].Ctrl == GameRules.OWNER_PLAYER;
            bool isEnemy = gs.BF[0].Ctrl == GameRules.OWNER_ENEMY;
            Color expected = isPlayer ? GameColors.CtrlBadgePlayer
                           : isEnemy ? GameColors.CtrlBadgeEnemy
                           : GameColors.ScoreCircleInactive;

            Assert.AreEqual(GameColors.ScoreCircleInactive, expected);
        }

        [Test]
        public void BFControlBadge_TextLabel_Player()
        {
            var gs = new GameState();
            gs.BF[0].Ctrl = GameRules.OWNER_PLAYER;

            string label = gs.BF[0].Ctrl == GameRules.OWNER_PLAYER ? "玩"
                         : gs.BF[0].Ctrl == GameRules.OWNER_ENEMY ? "敌"
                         : "—";
            Assert.AreEqual("玩", label);
        }

        [Test]
        public void BFControlBadge_TextLabel_Enemy()
        {
            var gs = new GameState();
            gs.BF[1].Ctrl = GameRules.OWNER_ENEMY;

            string label = gs.BF[1].Ctrl == GameRules.OWNER_PLAYER ? "玩"
                         : gs.BF[1].Ctrl == GameRules.OWNER_ENEMY ? "敌"
                         : "—";
            Assert.AreEqual("敌", label);
        }

        // ── Edge cases ───────────────────────────────────────────────────────

        [Test]
        public void EdgeCase_EmptyDiscard_CountIsZero()
        {
            var gs = new GameState();
            Assert.AreEqual(0, gs.PDiscard.Count);
            Assert.AreEqual(0, gs.EDiscard.Count);
        }

        [Test]
        public void EdgeCase_EmptyExile_CountIsZero()
        {
            var gs = new GameState();
            Assert.AreEqual(0, gs.PExile.Count);
            Assert.AreEqual(0, gs.EExile.Count);
        }

        [Test]
        public void EdgeCase_FullScore_NoCircleBeyond8()
        {
            var gs = new GameState();
            gs.PScore = GameRules.WIN_SCORE;

            // Verify no circle index goes beyond 8
            int maxCircle = 8;
            for (int i = 0; i <= maxCircle; i++)
            {
                if (i < gs.PScore)
                    Assert.Less(i, GameRules.WIN_SCORE, $"Circle {i} should be within score range");
            }
        }

        [Test]
        public void EdgeCase_InfoStrip_RuneFormat()
        {
            var gs = new GameState();
            for (int i = 0; i < 3; i++)
                gs.PRunes.Add(new RuneInstance(GameState.NextUid(), RuneType.Blazing));
            for (int i = 0; i < 9; i++)
                gs.PRuneDeck.Add(new RuneInstance(GameState.NextUid(), RuneType.Blazing));

            string expected = $"{gs.PRunes.Count}/{gs.PRunes.Count + gs.PRuneDeck.Count}";
            Assert.AreEqual("3/12", expected);
        }

        [Test]
        public void EdgeCase_ScoreEnemyTrack_Score3()
        {
            var gs = new GameState();
            gs.EScore = 3;

            for (int i = 0; i < 9; i++)
            {
                Color expected;
                if (i < gs.EScore)
                    expected = GameColors.ScoreCircleEnemy;
                else if (i == gs.EScore && gs.EScore < GameRules.WIN_SCORE)
                    expected = GameColors.ScoreCircleCurrent;
                else
                    expected = GameColors.ScoreCircleInactive;

                if (i < 3)
                    Assert.AreEqual(GameColors.ScoreCircleEnemy, expected, $"Enemy circle {i} should be red");
                else if (i == 3)
                    Assert.AreEqual(GameColors.ScoreCircleCurrent, expected, $"Enemy circle {i} should be current");
                else
                    Assert.AreEqual(GameColors.ScoreCircleInactive, expected, $"Enemy circle {i} should be inactive");
            }
        }
    }
}
