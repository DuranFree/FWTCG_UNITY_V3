using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// 自适应叠卡布局。挂在 HorizontalLayoutGroup 容器上。
    ///
    /// 行为：
    ///   - 卡数少时 → 正常间距 (DefaultSpacing)
    ///   - 总宽度超过容器 → 动态计算负 spacing，卡按顺序重叠
    ///     (新入场的在最上层，旧的被部分盖住但保留左侧识别区)
    ///   - 和 HLG 的 MiddleCenter 对齐搭配工作，中心始终填满
    ///
    /// 设计要点：
    ///   - 每帧 LateUpdate 重算 spacing（child 变动后自动生效）
    ///   - MinVisibleWidth 保证即使极度挤压，每张牌至少露出 20px 左边条
    ///   - 最后一个 child SetAsLastSibling → 最新的卡永远在最上层
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(HorizontalLayoutGroup))]
    public class CardStackLayout : MonoBehaviour
    {
        [Tooltip("不挤压时的正常间距")]
        public float DefaultSpacing = 4f;

        [Tooltip("极度挤压时每张卡至少露出的左边宽度（px）")]
        public float MinVisibleWidth = 20f;

        [Tooltip("true = 每次 LateUpdate 都重排最新加入的 child 到最上层")]
        public bool NewestOnTop = true;

        private RectTransform _rt;
        private HorizontalLayoutGroup _hlg;
        private int _lastChildCount = -1;
        private float _lastContainerWidth = -1f;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _hlg = GetComponent<HorizontalLayoutGroup>();
            if (_hlg != null) _hlg.spacing = DefaultSpacing;
        }

        private void LateUpdate()
        {
            if (_rt == null || _hlg == null) return;

            int childCount = 0;
            float totalChildWidth = 0f;
            float firstChildWidth = 0f;
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (!c.gameObject.activeSelf) continue;
                var crt = c as RectTransform ?? c.GetComponent<RectTransform>();
                if (crt == null) continue;
                float w = crt.rect.width;
                if (w <= 0f) continue;
                if (childCount == 0) firstChildWidth = w;
                totalChildWidth += w;
                childCount++;
            }
            if (childCount == 0) return;

            float containerWidth = _rt.rect.width
                - _hlg.padding.left - _hlg.padding.right;

            // 避免无意义重算（尺寸和数量都没变就跳过）
            if (childCount == _lastChildCount && Mathf.Abs(containerWidth - _lastContainerWidth) < 0.5f)
            {
                if (NewestOnTop) EnsureNewestOnTop();
                return;
            }
            _lastChildCount = childCount;
            _lastContainerWidth = containerWidth;

            // 如果正常间距下就能放下 → 保持默认
            float totalWithDefault = totalChildWidth + (childCount - 1) * DefaultSpacing;
            if (totalWithDefault <= containerWidth)
            {
                _hlg.spacing = DefaultSpacing;
            }
            else
            {
                // overflow → 计算 overlap
                //   公式：containerWidth = firstFull + (n-1) * step
                //   step = (containerWidth - firstChildWidth) / (n-1)
                //   spacing = step - (cardWidth) = 负值
                //   Clamp：每张至少露出 MinVisibleWidth
                float step = (containerWidth - firstChildWidth) / Mathf.Max(1, childCount - 1);
                step = Mathf.Max(step, MinVisibleWidth);
                float spacing = step - firstChildWidth;
                _hlg.spacing = spacing;
            }

            if (NewestOnTop) EnsureNewestOnTop();
        }

        /// <summary>
        /// 保证最后一个 child 永远在最上层 —— HLG 不管渲染层级，
        /// 必须通过 SetAsLastSibling 让新入场的卡覆盖旧的。
        /// </summary>
        private void EnsureNewestOnTop()
        {
            int n = transform.childCount;
            if (n < 2) return;
            // 最后一个 child 已经是 last sibling，它会最后渲染在最上面 —— OK
            // 但如果顺序反了（可见排列是新的在左），需要反转。
            // 当前逻辑：HLG 按 child index 从左到右排列。最新的 child 是 index 最大。
            // 结果：最新的在最右，渲染也最晚 → 它盖住左侧的卡。符合需求。
            // 这里不主动操作 sibling index，HLG 自动处理。
        }

        /// <summary>手动重置，用于立刻 re-evaluate（RefreshUI 后可调）。</summary>
        public void MarkDirty()
        {
            _lastChildCount = -1;
            _lastContainerWidth = -1f;
        }
    }
}
