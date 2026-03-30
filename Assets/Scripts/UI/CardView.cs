using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// Attached to a Card prefab. Displays unit data and handles click events.
    /// Left-click: gameplay action (play/select/target).
    /// Right-click: show card detail popup.
    /// DEV-8: visual states (stunned overlay, buff token, cost dimming).
    /// </summary>
    public class CardView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _costText;
        [SerializeField] private Text _atkText;
        [SerializeField] private Text _descText;
        [SerializeField] private Image _cardBg;
        [SerializeField] private Image _artImage;
        [SerializeField] private Button _clickButton;

        // DEV-8: visual state overlays
        [SerializeField] private Image _stunnedOverlay;
        [SerializeField] private GameObject _buffTokenIcon;
        [SerializeField] private Text _buffTokenText;

        private UnitInstance _unit;
        private bool _isPlayerCard;
        private Action<UnitInstance> _onClick;
        private Action<UnitInstance> _onRightClick;

        private bool _selected;
        private bool _faceDown;
        private bool _costInsufficient;
        private Coroutine _stunPulse;
        private CardGlow _cardGlow;

        private void Awake()
        {
            if (_clickButton != null)
                _clickButton.onClick.AddListener(HandleClick);

            // Init glow controller — uses the material already assigned to _cardBg by SceneBuilder
            _cardGlow = GetComponent<CardGlow>();
            if (_cardGlow != null && _cardBg != null && _cardBg.material != null
                && _cardBg.material != Canvas.GetDefaultCanvasMaterial())
            {
                _cardGlow.Init(_cardBg, _cardBg.material);
            }
        }

        private void OnDestroy()
        {
            if (_clickButton != null)
                _clickButton.onClick.RemoveListener(HandleClick);
            if (_stunPulse != null)
                StopCoroutine(_stunPulse);
        }

        public void Setup(UnitInstance unit, bool isPlayerCard, Action<UnitInstance> onClick,
                          Action<UnitInstance> onRightClick = null)
        {
            _unit = unit;
            _isPlayerCard = isPlayerCard;
            _onClick = onClick;
            _onRightClick = onRightClick;
            _costInsufficient = false;

            Refresh();

            if (_clickButton != null)
                _clickButton.interactable = onClick != null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (_unit != null && _onRightClick != null && !_faceDown)
                    _onRightClick(_unit);
            }
        }

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
            if (_cardBg   != null) _cardBg.color = hide ? GameColors.CardFaceDown
                : (_selected ? GameColors.CardSelected
                : (_unit != null && _unit.Exhausted ? GameColors.CardExhausted
                : (_isPlayerCard ? GameColors.CardPlayer : GameColors.CardEnemy)));
            if (_clickButton != null) _clickButton.interactable = !hide;
            if (_stunnedOverlay != null) _stunnedOverlay.gameObject.SetActive(false);
            if (_buffTokenIcon != null) _buffTokenIcon.SetActive(false);
        }

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
                    _atkText.text = "法";
                }
                else
                {
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

            // Background color
            if (_cardBg != null)
            {
                Color baseColor;
                if (_unit.CardData.IsSpell)
                    baseColor = _isPlayerCard ? GameColors.CardSpellPlayer : GameColors.CardSpellEnemy;
                else
                    baseColor = _isPlayerCard ? GameColors.CardPlayer : GameColors.CardEnemy;

                if (_selected)
                    baseColor = GameColors.CardSelected;
                else if (_unit.Exhausted)
                    baseColor = GameColors.CardExhausted;

                // Cost insufficient dimming
                if (_costInsufficient)
                    baseColor *= GameColors.CostDimFactor;

                _cardBg.color = baseColor;
            }

            // Stunned overlay
            if (_stunnedOverlay != null)
            {
                bool stunned = _unit.Stunned;
                _stunnedOverlay.gameObject.SetActive(stunned);
                if (stunned && _stunPulse == null)
                    _stunPulse = StartCoroutine(StunPulseRoutine());
                else if (!stunned && _stunPulse != null)
                {
                    StopCoroutine(_stunPulse);
                    _stunPulse = null;
                }
            }

            // Glow border (playable = affordable + not exhausted for hand cards)
            if (_cardGlow != null)
            {
                if (_isPlayerCard && !_unit.Exhausted && !_costInsufficient)
                    _cardGlow.SetPlayable(true);
                else
                    _cardGlow.SetPlayable(false);
            }

            // Buff token indicator
            if (_buffTokenIcon != null)
            {
                bool hasBuff = _unit.BuffTokens > 0;
                _buffTokenIcon.SetActive(hasBuff);
                if (hasBuff && _buffTokenText != null)
                    _buffTokenText.text = $"+{_unit.BuffTokens}";
            }
        }

        /// <summary>
        /// Mark card as too expensive (dims the card).
        /// Called by GameUI for hand cards when mana is insufficient.
        /// </summary>
        public void SetCostInsufficient(bool insufficient)
        {
            _costInsufficient = insufficient;
            Refresh();
        }

        public UnitInstance Unit => _unit;

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

        private IEnumerator StunPulseRoutine()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * 2f; // 2 Hz pulse
                float alpha = Mathf.Lerp(0.15f, 0.45f, (Mathf.Sin(t) + 1f) * 0.5f);
                if (_stunnedOverlay != null)
                {
                    var c = GameColors.StunnedOverlay;
                    c.a = alpha;
                    _stunnedOverlay.color = c;
                }
                yield return null;
            }
        }
    }
}
