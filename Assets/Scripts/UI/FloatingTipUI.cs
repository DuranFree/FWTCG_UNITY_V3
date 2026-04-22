using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Data;

namespace FWTCG.UI
{
    /// <summary>
    /// UI-OVERHAUL-1a: 条件不满足 / 单行提示用的飘屏组件。
    ///
    /// 从鼠标位置（或指定 canvas 坐标）向上飘约 60 px 后渐变消失。
    /// 多行支持：同一次 Show 可以传入多行，每行独立颜色；按顺序自上而下排列，整体同时飘 + 淡出。
    ///
    /// 颜色约定（在调用端决定）：
    ///   - 法力不足 → 白色（卡牌左上角法力同色）
    ///   - 符能不足 → RuneType 对应颜色
    ///   - 其他特殊触发（无单位等）→ 黄色
    ///
    /// 非单例——每次 Show 会 Instantiate 一个新的飘屏实例挂到 RootCanvas 下，播完自毁。
    /// </summary>
    public class FloatingTipUI : MonoBehaviour
    {
        public struct Line
        {
            public string Text;
            public Color  Color;
            public Line(string t, Color c) { Text = t; Color = c; }
        }

        // ── 运行时实例 ────────────────────────────────────────────────────────
        private RectTransform _rt;
        private CanvasGroup   _cg;
        private readonly List<Text> _texts = new List<Text>();
        private Sequence _seq;

        private const float FLOAT_DISTANCE = 60f;
        private const float FADE_IN_DUR    = 0.12f;
        private const float HOLD_DUR       = 0.9f;
        private const float FADE_OUT_DUR   = 0.35f;
        private const float LINE_HEIGHT    = 22f;
        private const float LINE_FONT      = 16f;

        /// <summary>
        /// 在指定 canvas 局部坐标位置显示多行飘屏。如果 canvasPos 为 null 则自动使用当前鼠标位置。
        /// </summary>
        public static FloatingTipUI Show(Canvas canvas, IList<Line> lines, Vector2? canvasPos = null)
        {
            if (canvas == null || lines == null || lines.Count == 0) return null;

            var go = new GameObject("FloatingTip", typeof(RectTransform), typeof(CanvasGroup), typeof(FloatingTipUI));
            go.transform.SetParent(canvas.transform, false);
            var tip = go.GetComponent<FloatingTipUI>();
            tip.Build(canvas, lines, canvasPos);
            return tip;
        }

        public static FloatingTipUI ShowSingle(Canvas canvas, string text, Color color, Vector2? canvasPos = null)
            => Show(canvas, new[] { new Line(text, color) }, canvasPos);

        private void Build(Canvas canvas, IList<Line> lines, Vector2? canvasPos)
        {
            _rt = GetComponent<RectTransform>();
            _cg = GetComponent<CanvasGroup>();
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot     = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = new Vector2(260f, LINE_HEIGHT * lines.Count + 8f);

            Vector2 startPos;
            if (canvasPos.HasValue)
            {
                startPos = canvasPos.Value;
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.GetComponent<RectTransform>(), Input.mousePosition, canvas.worldCamera, out startPos);
            }
            _rt.localPosition = new Vector3(startPos.x, startPos.y, 0f);
            _cg.alpha          = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable   = false;

            // 多行文字：每行独立 Text，Vertical 排布（从上到下）
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var lineGo = new GameObject($"Line{i}", typeof(RectTransform), typeof(Text));
                lineGo.transform.SetParent(transform, false);
                var lrt = lineGo.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 1f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.pivot     = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0f, -i * LINE_HEIGHT);
                lrt.sizeDelta = new Vector2(0f, LINE_HEIGHT);

                var txt = lineGo.GetComponent<Text>();
                txt.text       = line.Text;
                txt.color      = line.Color;
                txt.fontSize   = Mathf.RoundToInt(LINE_FONT);
                txt.alignment  = TextAnchor.MiddleCenter;
                // Hotfix-4: Arial.ttf 在 Unity 新版已移除，用 LegacyRuntime.ttf；try/catch 防崩
                Font font = null;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (font == null)
                {
                    try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                }
                if (font != null) txt.font = font;
                txt.raycastTarget = false;
                txt.supportRichText = true;
                // 轻描边效果：增加可读性
                var outline = lineGo.AddComponent<Outline>();
                outline.effectColor    = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(1f, -1f);
                _texts.Add(txt);
            }

            // 动画：向上飘 FLOAT_DISTANCE + 淡入 + 停留 + 淡出
            _seq = DOTween.Sequence().SetTarget(this).LinkKillOnDestroy(gameObject);
            _seq.Append(_cg.DOFade(1f, FADE_IN_DUR).SetEase(Ease.OutCubic));
            _seq.Join(_rt.DOAnchorPosY(_rt.anchoredPosition.y + FLOAT_DISTANCE * 0.35f, FADE_IN_DUR).SetEase(Ease.OutCubic));
            _seq.AppendInterval(HOLD_DUR);
            _seq.Append(_rt.DOAnchorPosY(_rt.anchoredPosition.y + FLOAT_DISTANCE, FADE_OUT_DUR).SetEase(Ease.InCubic));
            _seq.Join(_cg.DOFade(0f, FADE_OUT_DUR).SetEase(Ease.InCubic));
            _seq.OnComplete(() => { if (this != null) Destroy(gameObject); });
            _seq.OnKill(() =>    { if (this != null && gameObject != null) Destroy(gameObject); });
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _seq);
        }

        // ── 颜色 helper：常用类型 ──────────────────────────────────────────────

        /// <summary>法力不足（白色）。</summary>
        public static Line ManaShortLine(int shortBy)
            => new Line($"法力不足 {shortBy}", Color.white);

        /// <summary>符能不足（该符能本体色）。</summary>
        public static Line RuneShortLine(Data.RuneType type, int shortBy)
            => new Line($"{type.ToChinese()}符能不足 {shortBy}", GameColors.GetRuneColor(type));

        /// <summary>特殊情况（黄色）。</summary>
        public static Line WarnLine(string text)
            => new Line(text, GameColors.BannerYellow);
    }
}
