using System;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// Attached to a Card prefab. Displays unit data and handles click events.
    /// Exhausted units are shown with a grey tint.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _costText;
        [SerializeField] private Text _atkText;
        [SerializeField] private Text _descText;
        [SerializeField] private Image _cardBg;
        [SerializeField] private Image _artImage;
        [SerializeField] private Button _clickButton;

        private UnitInstance _unit;
        private bool _isPlayerCard;
        private Action<UnitInstance> _onClick;

        private static readonly Color ExhaustedColor    = new Color(0.4f, 0.4f, 0.4f, 1f);
        private static readonly Color NormalColor        = Color.white;
        private static readonly Color PlayerCardColor    = new Color(0.85f, 0.92f, 1f, 1f);
        private static readonly Color EnemyCardColor     = new Color(1f, 0.85f, 0.85f, 1f);
        private static readonly Color SpellPlayerColor   = new Color(0.95f, 0.85f, 1f, 1f); // purple tint for player spells
        private static readonly Color SpellEnemyColor    = new Color(1f, 0.8f, 0.9f, 1f);   // pink tint for enemy spells
        private static readonly Color SelectedColor      = new Color(0.4f, 1f, 0.4f, 1f);   // green highlight
        private static readonly Color FaceDownColor      = new Color(0.12f, 0.16f, 0.25f, 1f); // dark blue-grey back

        private bool _selected;
        private bool _faceDown;

        private void Awake()
        {
            if (_clickButton != null)
                _clickButton.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_clickButton != null)
                _clickButton.onClick.RemoveListener(HandleClick);
        }

        /// <summary>
        /// Configures this card view for a unit.
        /// </summary>
        /// <param name="unit">Unit to display</param>
        /// <param name="isPlayerCard">True if this belongs to the local player</param>
        /// <param name="onClick">Callback when the card is clicked (null to disable interaction)</param>
        public void Setup(UnitInstance unit, bool isPlayerCard, Action<UnitInstance> onClick)
        {
            _unit = unit;
            _isPlayerCard = isPlayerCard;
            _onClick = onClick;

            Refresh();

            // Only player cards are clickable
            if (_clickButton != null)
                _clickButton.interactable = isPlayerCard && onClick != null;
        }

        /// <summary>
        /// Shows the card as face-down (enemy hand). Hides all unit data.
        /// </summary>
        public void SetFaceDown(bool faceDown)
        {
            _faceDown = faceDown;
            RefreshFaceDown();
        }

        private void RefreshFaceDown()
        {
            bool hide = _faceDown;
            if (_nameText != null) _nameText.gameObject.SetActive(!hide);
            if (_costText != null) _costText.gameObject.SetActive(!hide);
            if (_atkText  != null) _atkText.gameObject.SetActive(!hide);
            if (_descText != null) _descText.gameObject.SetActive(!hide);
            if (_artImage != null) _artImage.enabled = !hide;
            if (_cardBg   != null) _cardBg.color = hide ? FaceDownColor : (_selected ? SelectedColor : (_unit != null && _unit.Exhausted ? ExhaustedColor : (_isPlayerCard ? PlayerCardColor : EnemyCardColor)));
            if (_clickButton != null) _clickButton.interactable = !hide;
        }

        /// <summary>
        /// Re-reads the unit state and updates all displayed values.
        /// Call this when unit state changes (e.g., after taking damage).
        /// </summary>
        public void Refresh()
        {
            if (_faceDown) { RefreshFaceDown(); return; }
            if (_unit == null) return;

            if (_nameText != null)
                _nameText.text = _unit.UnitName;

            if (_costText != null)
                _costText.text = _unit.CardData.Cost.ToString();

            if (_atkText != null)
            {
                if (_unit.CardData.IsSpell)
                {
                    // Spell cards show "法" instead of atk/hp
                    _atkText.text = "法";
                }
                else
                {
                    // Show current attack / hp. In FWTCG atk=HP so show both same value.
                    _atkText.text = $"{_unit.CurrentAtk}";
                    if (_unit.CurrentHp != _unit.CurrentAtk)
                        _atkText.text = $"{_unit.CurrentHp}/{_unit.CurrentAtk}";
                }
            }

            if (_descText != null)
                _descText.text = _unit.CardData.Description;

            if (_artImage != null && _unit.CardData.ArtSprite != null)
            {
                _artImage.sprite = _unit.CardData.ArtSprite;
                _artImage.enabled = true;
            }
            else if (_artImage != null)
            {
                _artImage.enabled = false;
            }

            // Background colour by owner + exhausted + selected state
            if (_cardBg != null)
            {
                Color baseColor;
                if (_unit.CardData.IsSpell)
                    baseColor = _isPlayerCard ? SpellPlayerColor : SpellEnemyColor;
                else
                    baseColor = _isPlayerCard ? PlayerCardColor : EnemyCardColor;

                if (_selected)
                    _cardBg.color = SelectedColor;
                else
                    _cardBg.color = _unit.Exhausted ? ExhaustedColor : baseColor;
            }
        }

        public UnitInstance Unit => _unit;

        /// <summary>
        /// Sets green highlight when unit is part of a multi-select batch.
        /// </summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            Refresh();
        }

        private void HandleClick()
        {
            if (_unit != null && _onClick != null)
                _onClick(_unit);
        }
    }
}
