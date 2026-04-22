using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// Subtle hover scale effect for cards. Scales up slightly on mouse enter,
    /// returns to normal on mouse exit.
    /// On hover enter, enables a nested Canvas with overrideSorting so the element
    /// renders above ALL other Canvas children (including PlayerHandZone).
    ///
    /// DOT-3: Update Lerp → DOScale tween; drag snap kills tween instantly.
    /// </summary>
    public class CardHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_SCALE    = 1.18f;  // 放大明显一点
        private const float TWEEN_DURATION = 0.18f;
        private const float HAND_LIFT_Y    = 40f;    // 手牌悬停时额外向上位移（像抽出来）
        private const int   HOVER_SORT     = 100; // above hand zone (depth ~163)

        // Nested Canvas for sorting override — added at runtime if missing
        private Canvas           _overrideCanvas;
        private GraphicRaycaster _raycaster;
        private CardView         _cardView;

        // ── DOTween state ─────────────────────────────────────────────────────
        private Tween _scaleTween;
        private Tween _liftTween;
        private RectTransform _rt;
        private bool  _isLifted;             // 手牌悬停抬起状态
        private float _liftCurrentOffset;    // 当前实际抬起的 y 偏移（tween 驱动）
        private bool  _liftHookSubscribed;   // 是否已订阅 willRenderCanvases

        private void Awake()
        {
            _overrideCanvas = GetComponent<Canvas>();
            if (_overrideCanvas == null)
            {
                _overrideCanvas = gameObject.AddComponent<Canvas>();
                _raycaster      = gameObject.AddComponent<GraphicRaycaster>();
            }
            _overrideCanvas.overrideSorting = false; // start inactive
            _cardView = GetComponent<CardView>();
            _rt       = (RectTransform)transform;
        }

        private bool IsSelected => _cardView != null && _cardView.IsSelected;

        private bool IsHandCard()
        {
            // 只对真正的玩家手牌区生效（PlayerHandZone），其它带 HLG 的容器（Mulligan/AskPrompt/Reactive）不触发抬起+扩散
            return transform.parent != null && transform.parent.name == "PlayerHandZone";
        }

        private void ApplyHoverVisuals()
        {
            if (_overrideCanvas != null)
            {
                _overrideCanvas.overrideSorting = true;
                _overrideCanvas.sortingOrder    = HOVER_SORT;
            }
            TweenHelper.KillSafe(ref _scaleTween);
            _scaleTween = transform.DOScale(HOVER_SCALE, TWEEN_DURATION)
                .SetEase(Ease.OutCubic)
                .SetTarget(gameObject);

            // 手牌：抬起（抽出效果）
            if (IsHandCard())
            {
                _isLifted = true;
                if (!_liftHookSubscribed)
                {
                    Canvas.willRenderCanvases += EnforceLift;
                    _liftHookSubscribed = true;
                }
                TweenHelper.KillSafe(ref _liftTween);
                _liftTween = DOTween.To(() => _liftCurrentOffset,
                                        v => _liftCurrentOffset = v,
                                        HAND_LIFT_Y, TWEEN_DURATION)
                    .SetEase(Ease.OutCubic)
                    .SetTarget(gameObject);
            }
        }

        /// <summary>
        /// 在 HLG 完成布局后（Canvas.willRenderCanvases 阶段）把抬起偏移加到 anchoredPosition.y 上。
        /// 这样不跟 HLG 互斥：HLG 设定 x + baseY，我们在最后一刻把 Y 加上 lift offset。
        /// </summary>
        private void EnforceLift()
        {
            if (_rt == null) return;
            if (!_isLifted && Mathf.Approximately(_liftCurrentOffset, 0f))
            {
                // 已完全落回且不需要抬起 → 取消订阅减少每帧开销
                if (_liftHookSubscribed)
                {
                    Canvas.willRenderCanvases -= EnforceLift;
                    _liftHookSubscribed = false;
                }
                return;
            }
            var p = _rt.anchoredPosition;
            p.y += _liftCurrentOffset;
            _rt.anchoredPosition = p;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (Input.GetMouseButton(0)) return;
            ApplyHoverVisuals();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (Input.GetMouseButton(0)) return;
            // 选中状态：保持放大 + 高层级，直到再次点击取消
            if (IsSelected) return;
            if (_overrideCanvas != null)
                _overrideCanvas.overrideSorting = false;
            TweenHelper.KillSafe(ref _scaleTween);
            _scaleTween = transform.DOScale(1f, TWEEN_DURATION)
                .SetEase(Ease.OutCubic)
                .SetTarget(gameObject);

            // 手牌：抬起偏移动画式归零（保持订阅，直到归零后 EnforceLift 自动退订）
            if (_isLifted)
            {
                _isLifted = false;
                TweenHelper.KillSafe(ref _liftTween);
                _liftTween = DOTween.To(() => _liftCurrentOffset,
                                        v => _liftCurrentOffset = v,
                                        0f, TWEEN_DURATION)
                    .SetEase(Ease.OutCubic)
                    .SetTarget(gameObject);
            }
        }

        private void Update()
        {
            // 拖拽期间 / 左键按住时强制缩回原尺寸（仅未选中才生效）
            if ((CardDragHandler.BlockPointerEvents || Input.GetMouseButton(0)) && !IsSelected)
            {
                if (transform.localScale != Vector3.one)
                {
                    TweenHelper.KillSafe(ref _scaleTween);
                    transform.localScale = Vector3.one;
                    if (_overrideCanvas != null) _overrideCanvas.overrideSorting = false;
                }
                return;
            }

            // 选中但当前未放大（例如刚被 SetSelected 调用）→ 驱动到放大
            if (IsSelected && !CardDragHandler.BlockPointerEvents
                && Mathf.Abs(transform.localScale.x - HOVER_SCALE) > 0.001f
                && (_scaleTween == null || !_scaleTween.IsActive()))
            {
                ApplyHoverVisuals();
            }

            // Hotfix-8: 取消选中后若鼠标不在本卡上 → 归零 scale + lift
            // （OnPointerExit 已在 IsSelected=true 时被 return 跳过，需要此分支补做）
            if (!IsSelected && !CardDragHandler.BlockPointerEvents && !Input.GetMouseButton(0)
                && (transform.localScale.x > 1.01f || _isLifted || _liftCurrentOffset > 0.1f))
            {
                Camera cam = _overrideCanvas != null ? _overrideCanvas.worldCamera : null;
                bool mouseOver = _rt != null
                    && RectTransformUtility.RectangleContainsScreenPoint(_rt, Input.mousePosition, cam);
                if (!mouseOver)
                {
                    if (_overrideCanvas != null) _overrideCanvas.overrideSorting = false;
                    TweenHelper.KillSafe(ref _scaleTween);
                    _scaleTween = transform.DOScale(1f, TWEEN_DURATION)
                        .SetEase(Ease.OutCubic).SetTarget(gameObject);
                    if (_isLifted || _liftCurrentOffset > 0.1f)
                    {
                        _isLifted = false;
                        TweenHelper.KillSafe(ref _liftTween);
                        _liftTween = DOTween.To(() => _liftCurrentOffset,
                                                v => _liftCurrentOffset = v,
                                                0f, TWEEN_DURATION)
                            .SetEase(Ease.OutCubic).SetTarget(gameObject);
                    }
                }
            }
        }

        /// <summary>Force-exit hover state immediately (used when popup steals focus).</summary>
        public void ForceUnhover()
        {
            TweenHelper.KillSafe(ref _scaleTween);
            TweenHelper.KillSafe(ref _liftTween);
            transform.localScale = Vector3.one;
            _liftCurrentOffset = 0f;
            _isLifted = false;
            if (_overrideCanvas != null) _overrideCanvas.overrideSorting = false;
            if (_liftHookSubscribed)
            {
                Canvas.willRenderCanvases -= EnforceLift;
                _liftHookSubscribed = false;
            }
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _scaleTween);
            TweenHelper.KillSafe(ref _liftTween);
            if (_liftHookSubscribed)
            {
                Canvas.willRenderCanvases -= EnforceLift;
                _liftHookSubscribed = false;
            }
        }
    }
}
