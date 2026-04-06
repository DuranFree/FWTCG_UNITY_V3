using DG.Tweening;
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
        private Tween _pulseTween;

        private void Awake()
        {
            _outline = GetComponent<Outline>();
            if (_outline != null)
                _outline.effectColor = new Color(_outline.effectColor.r, _outline.effectColor.g,
                                                  _outline.effectColor.b, 0f); // start invisible
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // VFX-7m: only glow when button is interactable
            var btn = GetComponent<Button>();
            if (btn != null && !btn.interactable) return;
            if (_outline == null) return;

            TweenHelper.KillSafe(ref _pulseTween);

            var c = _outline.effectColor;
            _outline.effectColor = new Color(c.r, c.g, c.b, 0.4f);

            // DOTween.To driving Outline alpha 0.4↔1.0, ~2 Hz (period=0.5s)
            _pulseTween = DOTween.To(
                () => _outline.effectColor.a,
                a =>
                {
                    if (_outline != null)
                    {
                        var ec = _outline.effectColor;
                        _outline.effectColor = new Color(ec.r, ec.g, ec.b, a);
                    }
                },
                1f, 0.25f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetTarget(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TweenHelper.KillSafe(ref _pulseTween);
            if (_outline != null)
            {
                var c = _outline.effectColor;
                _outline.effectColor = new Color(c.r, c.g, c.b, 0f);
            }
        }

        private void OnDestroy()
        {
            TweenHelper.KillSafe(ref _pulseTween);
        }
    }
}
