using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// Controls the glow border material on a card.
    /// Clones the material on init to allow per-card control.
    /// Destroys the clone on OnDestroy to prevent leaks during GameUI.Refresh().
    /// </summary>
    public class CardGlow : MonoBehaviour
    {
        private Material _mat;
        private Image _targetImage;

        private static readonly int PropGlowColor     = Shader.PropertyToID("_GlowColor");
        private static readonly int PropGlowIntensity  = Shader.PropertyToID("_GlowIntensity");
        private static readonly int PropAnimSpeed      = Shader.PropertyToID("_AnimSpeed");

        public void Init(Image cardBg, Material baseMaterial)
        {
            if (baseMaterial == null) return;
            _targetImage = cardBg;
            _mat = new Material(baseMaterial);
            _mat.name = baseMaterial.name + " (Clone)";
            if (_targetImage != null)
                _targetImage.material = _mat;
            SetNormal();
        }

        private void OnDestroy()
        {
            if (_mat != null)
            {
                // Reset image material before destroying to avoid missing material
                if (_targetImage != null)
                    _targetImage.material = null;
                Destroy(_mat);
                _mat = null;
            }
        }

        public void SetPlayable(bool playable)
        {
            if (_mat == null) return;
            if (playable)
            {
                _mat.SetColor(PropGlowColor, GameColors.GlowPlayable);
                _mat.SetFloat(PropGlowIntensity, 1.2f);
                _mat.SetFloat(PropAnimSpeed, 1.5f);
            }
            else
            {
                SetNormal();
            }
        }

        public void SetHovered(bool hovered)
        {
            if (_mat == null) return;
            if (hovered)
            {
                _mat.SetColor(PropGlowColor, GameColors.GlowHover);
                _mat.SetFloat(PropGlowIntensity, 0.8f);
                _mat.SetFloat(PropAnimSpeed, 0.5f);
            }
        }

        public void SetNormal()
        {
            if (_mat == null) return;
            _mat.SetFloat(PropGlowIntensity, 0f);
        }
    }
}
