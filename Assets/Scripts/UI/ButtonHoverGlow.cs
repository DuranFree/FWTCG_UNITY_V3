using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// Pulses a golden Outline glow on the attached Button when the pointer hovers over it.
    /// Requires an Outline component on the same GameObject (added by SceneBuilder).
    /// </summary>
    [RequireComponent(typeof(Outline))]
    public class ButtonHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Outline _outline;
        private Coroutine _pulseCoroutine;

        private void Awake()
        {
            _outline = GetComponent<Outline>();
            if (_outline != null)
                _outline.effectColor = new Color(_outline.effectColor.r, _outline.effectColor.g,
                                                  _outline.effectColor.b, 0f); // start invisible
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseRoutine());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
            if (_outline != null)
            {
                var c = _outline.effectColor;
                _outline.effectColor = new Color(c.r, c.g, c.b, 0f);
            }
        }

        private System.Collections.IEnumerator PulseRoutine()
        {
            while (true)
            {
                float alpha = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f; // 0→1→0, ~2 Hz
                if (_outline != null)
                {
                    var c = _outline.effectColor;
                    _outline.effectColor = new Color(c.r, c.g, c.b, Mathf.Lerp(0.4f, 1f, alpha));
                }
                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        }
    }
}
