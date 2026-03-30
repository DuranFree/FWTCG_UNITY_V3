using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// 3D tilt effect for cards. Tilts toward mouse position on hover.
    /// Also drives the holographic shine overlay if assigned.
    /// </summary>
    public class CardTilt : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float MAX_TILT = 30f;  // higher than original 18° to compensate for no perspective
        private const float LERP_IN  = 0.18f;
        private const float LERP_OUT = 0.12f;
        private const float EPSILON  = 0.01f;

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private Camera _canvasCamera;
        private bool _hovered;
        private float _currentTiltX;
        private float _currentTiltY;
        private float _targetTiltX;
        private float _targetTiltY;

        // Optional shine overlay (controlled via material properties)
        private Material _shineMat;

        private static readonly int PropShineX     = Shader.PropertyToID("_ShineX");
        private static readonly int PropShineY     = Shader.PropertyToID("_ShineY");
        private static readonly int PropShineIntensity = Shader.PropertyToID("_ShineIntensity");

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _canvasCamera = _canvas.worldCamera;
        }

        /// <summary>
        /// Assign the shine overlay material (cloned per card).
        /// </summary>
        public void SetShineMaterial(Material mat)
        {
            _shineMat = mat;
            if (_shineMat != null)
                _shineMat.SetFloat(PropShineIntensity, 0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _targetTiltX = 0f;
            _targetTiltY = 0f;
        }

        private void Update()
        {
            if (_rectTransform == null) return;

            if (_hovered)
            {
                // Get mouse position relative to card center (-1..1)
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectTransform, Input.mousePosition, _canvasCamera, out localPoint))
                {
                    Vector2 size = _rectTransform.rect.size;
                    if (size.x > 0 && size.y > 0)
                    {
                        float nx = Mathf.Clamp(localPoint.x / (size.x * 0.5f), -1f, 1f);
                        float ny = Mathf.Clamp(localPoint.y / (size.y * 0.5f), -1f, 1f);

                        // Tilt: card edge nearest to mouse rises toward viewer
                        _targetTiltY = -nx * MAX_TILT;
                        _targetTiltX = ny * MAX_TILT;

                        // Update shine position (0-1 UV)
                        if (_shineMat != null)
                        {
                            _shineMat.SetFloat(PropShineX, (nx + 1f) * 0.5f);
                            _shineMat.SetFloat(PropShineY, (ny + 1f) * 0.5f);
                            _shineMat.SetFloat(PropShineIntensity, 1f);
                        }
                    }
                }
            }
            else if (_shineMat != null)
            {
                _shineMat.SetFloat(PropShineIntensity, 0f);
            }

            // Lerp current toward target
            float lerpSpeed = _hovered ? LERP_IN : LERP_OUT;
            float t = 1f - Mathf.Pow(1f - lerpSpeed, Time.deltaTime * 60f);
            _currentTiltX = Mathf.Lerp(_currentTiltX, _targetTiltX, t);
            _currentTiltY = Mathf.Lerp(_currentTiltY, _targetTiltY, t);

            // Snap to zero when close
            if (!_hovered && Mathf.Abs(_currentTiltX) < EPSILON && Mathf.Abs(_currentTiltY) < EPSILON)
            {
                _currentTiltX = 0f;
                _currentTiltY = 0f;
            }

            _rectTransform.localEulerAngles = new Vector3(_currentTiltX, _currentTiltY, 0f);
        }

        private void OnDestroy()
        {
            // Cleanup cloned shine material
            if (_shineMat != null)
            {
                Destroy(_shineMat);
                _shineMat = null;
            }
        }
    }
}
