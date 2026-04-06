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
        private const float HOVER_SCALE    = 1.08f;
        private const float TWEEN_DURATION = 0.1f;
        private const int   HOVER_SORT     = 100; // above hand zone (depth ~163)

        // Nested Canvas for sorting override — added at runtime if missing
        private Canvas           _overrideCanvas;
        private GraphicRaycaster _raycaster;

        // ── DOTween state ─────────────────────────────────────────────────────
        private Tween _scaleTween;

        private void Awake()
        {
            _overrideCanvas = GetComponent<Canvas>();
            if (_overrideCanvas == null)
            {
                _overrideCanvas = gameObject.AddComponent<Canvas>();
                _raycaster      = gameObject.AddComponent<GraphicRaycaster>();
            }
            _overrideCanvas.overrideSorting = false; // start inactive
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (Input.GetMouseButton(0)) return;
            if (_overrideCanvas != null)
            {
                _overrideCanvas.overrideSorting = true;
                _overrideCanvas.sortingOrder    = HOVER_SORT;
            }
            TweenHelper.KillSafe(ref _scaleTween);
            _scaleTween = transform.DOScale(HOVER_SCALE, TWEEN_DURATION)
                .SetEase(Ease.OutCubic)
                .SetTarget(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (Input.GetMouseButton(0)) return;
            if (_overrideCanvas != null)
                _overrideCanvas.overrideSorting = false;
            TweenHelper.KillSafe(ref _scaleTween);
            _scaleTween = transform.DOScale(1f, TWEEN_DURATION)
                .SetEase(Ease.OutCubic)
                .SetTarget(gameObject);
        }

        private void Update()
        {
            // During drag or left-button hold, all cards must be at normal scale instantly
            if (CardDragHandler.BlockPointerEvents || Input.GetMouseButton(0))
            {
                if (transform.localScale != Vector3.one)
                {
                    TweenHelper.KillSafe(ref _scaleTween);
                    transform.localScale = Vector3.one;
                    if (_overrideCanvas != null) _overrideCanvas.overrideSorting = false;
                }
                return;
            }
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _scaleTween);
        }
    }
}
