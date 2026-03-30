using UnityEngine;
using UnityEngine.EventSystems;

namespace FWTCG.UI
{
    /// <summary>
    /// Subtle hover scale effect for cards. Scales up slightly on mouse enter,
    /// returns to normal on mouse exit. Lightweight replacement for CardTilt.
    /// </summary>
    public class CardHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_SCALE = 1.08f;
        private const float LERP_SPEED = 10f;

        private Vector3 _targetScale = Vector3.one;
        private bool _animating;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _targetScale = new Vector3(HOVER_SCALE, HOVER_SCALE, 1f);
            _animating = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetScale = Vector3.one;
            _animating = true;
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
