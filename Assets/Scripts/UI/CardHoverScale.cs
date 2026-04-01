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
    /// </summary>
    public class CardHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_SCALE = 1.08f;
        private const float LERP_SPEED  = 10f;
        private const int   HOVER_SORT  = 100; // above hand zone (depth ~163)

        private Vector3 _targetScale = Vector3.one;
        private bool    _animating;

        // Nested Canvas for sorting override — added at runtime if missing
        private Canvas           _overrideCanvas;
        private GraphicRaycaster _raycaster;

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
            if (_overrideCanvas != null)
            {
                _overrideCanvas.overrideSorting = true;
                _overrideCanvas.sortingOrder    = HOVER_SORT;
            }
            _targetScale = new Vector3(HOVER_SCALE, HOVER_SCALE, 1f);
            _animating   = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_overrideCanvas != null)
                _overrideCanvas.overrideSorting = false;
            _targetScale = Vector3.one;
            _animating   = true;
        }

        private void Update()
        {
            if (!_animating) return;

            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale,
                Time.deltaTime * LERP_SPEED);

            if (Vector3.Distance(transform.localScale, _targetScale) < 0.001f)
            {
                transform.localScale = _targetScale;
                _animating = false;
            }
        }
    }
}
