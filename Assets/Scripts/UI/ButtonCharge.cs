using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-19: Hover-activated light-sweep effect for buttons (btn-charge).
    ///
    /// On pointer enter: a white semi-transparent Image sweeps left → right over
    /// the button in 1.5s. RectMask2D on the button clips the sweep so it never
    /// overflows.
    ///
    /// Usage: AddComponent to any Button GameObject. Requires a RectMask2D on the
    /// same GameObject (added by SceneBuilder). The sweep Image child is created
    /// automatically in Awake.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonCharge : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private RectTransform _rt;
        private Image         _sweep;
        private RectTransform _sweepRT;
        private Tween         _sweepTween;

        private const float DURATION = 1.5f;
        private const float SWEEP_WIDTH_FACTOR = 0.45f; // sweep image width = button width × factor

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();

            // Create sweep child image
            var sweepGO = new GameObject("ChargeSwep");
            sweepGO.transform.SetParent(transform, false);

            _sweep = sweepGO.AddComponent<Image>();
            _sweep.color = new Color(1f, 1f, 1f, 0.22f);
            _sweep.raycastTarget = false;

            _sweepRT = sweepGO.GetComponent<RectTransform>();
            _sweepRT.anchorMin = new Vector2(0f, 0f);
            _sweepRT.anchorMax = new Vector2(0f, 1f);
            _sweepRT.pivot     = new Vector2(0f, 0.5f);
            _sweepRT.offsetMin = Vector2.zero;
            _sweepRT.offsetMax = Vector2.zero;
            _sweepRT.sizeDelta = new Vector2(_rt.rect.width * SWEEP_WIDTH_FACTOR, 0f);

            // Start hidden (off the left edge)
            _sweepRT.anchoredPosition = new Vector2(-_rt.rect.width, 0f);
        }

        public void OnPointerEnter(PointerEventData _)
        {
            if (_sweep == null) return;

            TweenHelper.KillSafe(ref _sweepTween);

            float btnWidth   = _rt.rect.width;
            float sweepWidth = btnWidth * SWEEP_WIDTH_FACTOR;

            // Recalculate width in case button was resized
            _sweepRT.sizeDelta = new Vector2(sweepWidth, 0f);

            float startX = -sweepWidth;
            float endX   = btnWidth + sweepWidth;

            _sweepRT.anchoredPosition = new Vector2(startX, 0f);
            _sweepTween = _sweepRT.DOAnchorPosX(endX, DURATION)
                .SetEase(Ease.Linear)
                .SetTarget(gameObject)
                .OnComplete(() =>
                {
                    _sweepRT.anchoredPosition = new Vector2(-btnWidth, 0f);
                    _sweepTween = null;
                });
        }

        public void OnPointerExit(PointerEventData _)
        {
            // Let the current sweep finish naturally; nothing to cancel
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _sweepTween);
        }
    }
}
