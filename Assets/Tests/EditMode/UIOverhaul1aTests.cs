using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.UI;
using FWTCG.Data;

namespace FWTCG.Tests
{
    /// <summary>
    /// UI-OVERHAUL-1a tests: 新颜色常量、CardDragHandler 回调签名、FloatingTipUI 行为。
    /// EditMode only — 不依赖场景/GameState。
    /// </summary>
    [TestFixture]
    public class UIOverhaul1aTests
    {
        // ── 新增颜色常量 ─────────────────────────────────────────────────────
        [Test]
        public void GameColors_ActionBtnEndTurn_IsYellow()
        {
            var c = GameColors.ActionBtnEndTurn;
            Assert.Greater(c.r, 0.8f, "黄色 R 应 > 0.8");
            Assert.Greater(c.g, 0.6f, "黄色 G 应 > 0.6");
            Assert.Less(c.b, 0.5f,    "黄色 B 应 < 0.5");
        }

        [Test]
        public void GameColors_ActionBtnConfirm_IsGreen()
        {
            var c = GameColors.ActionBtnConfirm;
            Assert.Less(c.r, 0.6f,    "绿色 R 应 < 0.6");
            Assert.Greater(c.g, 0.7f, "绿色 G 应 > 0.7");
        }

        [Test]
        public void GameColors_ActionBtnCancel_IsRed()
        {
            var c = GameColors.ActionBtnCancel;
            Assert.Greater(c.r, 0.7f, "红色 R 应 > 0.7");
            Assert.Less(c.b, 0.5f,    "红色 B 应 < 0.5");
        }

        // ── CardDragHandler 回调签名（单元素化）────────────────────────────
        [Test]
        public void CardDragHandler_OnDragToBF_AcceptsSingleUnit()
        {
            var go = new GameObject("SingleBFTest", typeof(Image), typeof(Button), typeof(CardView));
            var dh = go.AddComponent<CardDragHandler>();
            bool fired = false;
            int  bf    = -1;
            dh.OnDragToBF = (unit, bfId) => { fired = true; bf = bfId; };
            dh.OnDragToBF.Invoke(null, 1);
            Assert.IsTrue(fired);
            Assert.AreEqual(1, bf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CardDragHandler_HasNoGroupCallbacks()
        {
            // cluster/group 回调字段应已删除
            var t = typeof(CardDragHandler);
            Assert.IsNull(t.GetField("OnDragHandGroupToBase"), "OnDragHandGroupToBase 应已移除");
            Assert.IsNull(t.GetField("OnSpellGroupDragOut"),   "OnSpellGroupDragOut 应已移除");
        }

        [Test]
        public void CardDragHandler_HasNoClusterFields()
        {
            var t = typeof(CardDragHandler);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            Assert.IsNull(t.GetField("_clusterGhosts", flags),       "_clusterGhosts 应已移除");
            Assert.IsNull(t.GetField("_clusterOrigViews", flags),    "_clusterOrigViews 应已移除");
            Assert.IsNull(t.GetField("_clusterMoveCoroutine", flags),"_clusterMoveCoroutine 应已移除");
        }

        // ── FloatingTipUI 颜色 helper ───────────────────────────────────────
        [Test]
        public void FloatingTipUI_ManaShortLine_IsWhite()
        {
            var line = FloatingTipUI.ManaShortLine(2);
            Assert.AreEqual(Color.white, line.Color);
            StringAssert.Contains("法力", line.Text);
            StringAssert.Contains("2", line.Text);
        }

        [Test]
        public void FloatingTipUI_RuneShortLine_UsesRuneColor()
        {
            var line = FloatingTipUI.RuneShortLine(RuneType.Blazing, 1);
            Assert.AreEqual(GameColors.RuneBlazing, line.Color);
            StringAssert.Contains("炽烈", line.Text);
        }

        [Test]
        public void FloatingTipUI_WarnLine_IsYellow()
        {
            var line = FloatingTipUI.WarnLine("场上无单位");
            Assert.AreEqual(GameColors.BannerYellow, line.Color);
        }

        // ── FloatingTipUI.Show 空输入守卫 ──────────────────────────────────
        [Test]
        public void FloatingTipUI_Show_NullCanvas_ReturnsNull()
        {
            var result = FloatingTipUI.Show(null, new[] { new FloatingTipUI.Line("x", Color.white) });
            Assert.IsNull(result);
        }

        [Test]
        public void FloatingTipUI_Show_EmptyLines_ReturnsNull()
        {
            var canvasGo = new GameObject("CanvasForTest", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            var result = FloatingTipUI.Show(canvas, new FloatingTipUI.Line[0]);
            Assert.IsNull(result);
            Object.DestroyImmediate(canvasGo);
        }
    }
}
