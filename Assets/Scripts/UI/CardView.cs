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
    public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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

        // DEV-10: schematic cost display
        [SerializeField] private Text _schCostText;
        [SerializeField] private Image _schCostBg;

        // DEV-10: exhausted overlay (gray dimming)
        [SerializeField] private Image _exhaustedOverlay;

        private UnitInstance _unit;

        private bool _isPlayerCard;
        private Action<UnitInstance> _onClick;
        private Action<UnitInstance> _onRightClick;
        private Action<UnitInstance> _onHoverEnter;
        private Action<UnitInstance> _onHoverExit;

        private bool _selected;
        private bool _faceDown;
        private bool _costInsufficient;
        private Coroutine _stunPulse;
        private Coroutine _shake;
        private Coroutine _flash;
        private Coroutine _death;
        private CardGlow _cardGlow;

        // ── Selection lift + float animation ────────────────────────────────
        private bool      _isLifted;
        private Coroutine _liftFloat;
        private Coroutine _returnToRest;            // smooth de-select return animation
        private float     _restAnchoredY;           // Y before lifting
        private const float LiftOffset       = 12f;   // px raised when selected
        private const float FloatAmplitude   = 4f;    // px peak of float wave
        private const float FloatPeriod      = 1.5f;  // seconds per full cycle
        private const float ReturnDuration   = 0.30f; // seconds for animated return

        private void Awake()
        {
            // Auto-wire SerializeField refs by child name if Inspector connections were lost
            if (_nameText == null)         _nameText         = FindDeepText("CardName");
            if (_costText == null)         _costText         = FindDeepText("CostText");
            if (_atkText == null)          _atkText          = FindDeepText("AtkText");
            if (_descText == null)         _descText         = FindDeepText("DescText");
            if (_artImage == null)         _artImage         = FindDeepImage("ArtImage");
            if (_cardBg == null)           _cardBg           = GetComponent<Image>();
            if (_clickButton == null)      _clickButton      = GetComponent<Button>();
            if (_stunnedOverlay == null)   _stunnedOverlay   = FindDeepImage("StunnedOverlay");
            if (_buffTokenIcon == null)    { var t = FindDeep("BuffTokenIcon"); if (t) _buffTokenIcon = t.gameObject; }
            if (_buffTokenText == null)    _buffTokenText    = FindDeepText("BuffText");
            if (_schCostText == null)      _schCostText      = FindDeepText("SchCostText");
            if (_schCostBg == null)        _schCostBg        = FindDeepImage("SchCostBg");
            if (_exhaustedOverlay == null) _exhaustedOverlay = FindDeepImage("ExhaustedOverlay");

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

        // ── Auto-wire helpers ─────────────────────────────────────────────────

        private Transform FindDeep(string childName)
        {
            return FindDeepIn(transform, childName);
        }

        private static Transform FindDeepIn(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindDeepIn(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        private Text FindDeepText(string childName)
        {
            var t = FindDeep(childName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private Image FindDeepImage(string childName)
        {
            var t = FindDeep(childName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private void OnDestroy()
        {
            if (_clickButton != null)
                _clickButton.onClick.RemoveListener(HandleClick);
            if (_stunPulse    != null) StopCoroutine(_stunPulse);
            if (_liftFloat    != null) StopCoroutine(_liftFloat);
            if (_returnToRest != null) StopCoroutine(_returnToRest);
        }

        public void Setup(UnitInstance unit, bool isPlayerCard, Action<UnitInstance> onClick,
                          Action<UnitInstance> onRightClick  = null,
                          Action<UnitInstance> onHoverEnter  = null,
                          Action<UnitInstance> onHoverExit   = null)
        {
            _unit = unit;
            _isPlayerCard = isPlayerCard;
            _onClick = onClick;
            _onRightClick = onRightClick;
            _onHoverEnter = onHoverEnter;
            _onHoverExit  = onHoverExit;
            _costInsufficient = false;

            Refresh();

            if (_clickButton != null)
                _clickButton.interactable = onClick != null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (_unit != null && _onHoverEnter != null && !_faceDown)
                _onHoverEnter(_unit);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (_unit != null && _onHoverExit != null && !_faceDown)
                _onHoverExit(_unit);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
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

            // Exhausted overlay (gray dim)
            if (_exhaustedOverlay != null)
                _exhaustedOverlay.gameObject.SetActive(_unit.Exhausted && !_unit.Stunned);

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

            // Schematic (rune) cost display
            if (_schCostText != null && _schCostBg != null)
            {
                int schCost = _unit.CardData.RuneCost;
                if (schCost > 0)
                {
                    _schCostText.gameObject.SetActive(true);
                    _schCostBg.gameObject.SetActive(true);
                    string rtShort = "";
                    switch (_unit.CardData.RuneType)
                    {
                        case Data.RuneType.Blazing: rtShort = "炽"; break;
                        case Data.RuneType.Radiant: rtShort = "灵"; break;
                        case Data.RuneType.Verdant: rtShort = "翠"; break;
                        case Data.RuneType.Crushing: rtShort = "摧"; break;
                        default: rtShort = "符"; break;
                    }
                    _schCostText.text = $"{rtShort}×{schCost}";
                    _schCostBg.color = GameColors.GetRuneColor(_unit.CardData.RuneType);
                }
                else
                {
                    _schCostText.gameObject.SetActive(false);
                    _schCostBg.gameObject.SetActive(false);
                }
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
            bool stateChanged = _selected != selected;
            _selected = selected;
            Refresh();

            // Only touch animation when selection state actually changes;
            // avoids flickering when Refresh() calls SetSelected with the same value.
            if (!stateChanged) return;

            var rt = (RectTransform)transform;
            if (selected)
            {
                // If a return animation was in-progress, the card hasn't reached rest yet —
                // _restAnchoredY is still the correct original rest Y, so don't overwrite it.
                bool wasReturning = _returnToRest != null;
                if (_returnToRest != null) { StopCoroutine(_returnToRest); _returnToRest = null; }
                _isLifted = true;
                if (!wasReturning)
                    _restAnchoredY = rt.anchoredPosition.y;  // only save rest Y when truly at rest
                if (_liftFloat != null) StopCoroutine(_liftFloat);
                _liftFloat = StartCoroutine(LiftFloatRoutine());
            }
            else
            {
                _isLifted = false;
                if (_liftFloat != null) { StopCoroutine(_liftFloat); _liftFloat = null; }
                // Animate back to rest position instead of snapping
                if (_returnToRest != null) StopCoroutine(_returnToRest);
                _returnToRest = StartCoroutine(ReturnToRestRoutine(rt.anchoredPosition.y));
            }
        }

        /// <summary>
        /// Pauses the lift/float animation while this card is being cluster-dragged.
        /// Does NOT change _selected or _isLifted so ResumeLift can restart correctly.
        /// </summary>
        public void SuspendLift()
        {
            if (_liftFloat != null) { StopCoroutine(_liftFloat); _liftFloat = null; }
        }

        /// <summary>
        /// Resumes lift/float animation after cluster drag ends (if card is still selected).
        /// </summary>
        public void ResumeLift()
        {
            if (_selected && _isLifted && _liftFloat == null)
                _liftFloat = StartCoroutine(LiftFloatRoutine());
        }

        private IEnumerator LiftFloatRoutine()
        {
            var rt = (RectTransform)transform;
            float t = 0f;
            while (_isLifted)
            {
                float floatY = Mathf.Sin(t * Mathf.PI * 2f / FloatPeriod) * FloatAmplitude;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                                  _restAnchoredY + LiftOffset + floatY);
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Smoothly returns the card from its current lifted Y back to _restAnchoredY.
        /// Called when the card is deselected (e.g. turn ends) so it eases down visually.
        /// </summary>
        private IEnumerator ReturnToRestRoutine(float startY)
        {
            var rt = (RectTransform)transform;
            float elapsed = 0f;
            while (elapsed < ReturnDuration)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / ReturnDuration);
                float eased = t * (2f - t);   // ease-out quad — fast start, soft landing
                rt.anchoredPosition = new Vector2(
                    rt.anchoredPosition.x,
                    Mathf.Lerp(startY, _restAnchoredY, eased));
                yield return null;
            }
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, _restAnchoredY);
            _returnToRest = null;
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

        /// <summary>Brief red flash: instant red, fade back over 0.35s.</summary>
        public void FlashRed()
        {
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(FlashRedRoutine());
        }

        private IEnumerator FlashRedRoutine()
        {
            if (_cardBg == null) yield break;
            Color original = _cardBg.color;
            Color red = new Color(1f, 0.15f, 0.15f, original.a);
            _cardBg.color = red;
            yield return new WaitForSeconds(0.12f);
            float t = 0f;
            while (t < 0.35f)
            {
                _cardBg.color = Color.Lerp(red, original, t / 0.35f);
                t += Time.deltaTime;
                yield return null;
            }
            _cardBg.color = original;
            _flash = null;
        }

        /// <summary>Left-right shake: 4 oscillations of ±10px over ~0.3s.</summary>
        public void Shake()
        {
            if (_shake != null) StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 origin = rt.anchoredPosition;
            float[] offsets = { 10f, -10f, 8f, -8f, 5f, -5f, 0f };
            float step = 0.04f;
            foreach (float dx in offsets)
            {
                rt.anchoredPosition = new Vector2(origin.x + dx, origin.y);
                yield return new WaitForSeconds(step);
            }
            rt.anchoredPosition = origin;
            _shake = null;
        }

        /// <summary>
        /// Shrink + fade death animation over 0.45s. Called just before the unit is
        /// removed from game state. RefreshUI will destroy this GameObject shortly after.
        /// DEV-17.
        /// </summary>
        public void PlayDeathAnimation()
        {
            if (_death != null) return;
            _death = StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            const float duration = 0.45f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            var images = GetComponentsInChildren<Image>(true);
            var texts  = GetComponentsInChildren<Text>(true);
            Color[] imgColors = new Color[images.Length];
            Color[] txtColors = new Color[texts.Length];
            for (int i = 0; i < images.Length; i++) imgColors[i] = images[i].color;
            for (int i = 0; i < texts.Length;  i++) txtColors[i] = texts[i].color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Accelerate scale down (quadratic)
                float scale = Mathf.Lerp(1f, 0f, t * t);
                transform.localScale = startScale * scale;

                float alpha = 1f - t;
                for (int i = 0; i < images.Length; i++)
                {
                    var c = imgColors[i]; c.a *= alpha; images[i].color = c;
                }
                for (int i = 0; i < texts.Length; i++)
                {
                    var c = txtColors[i]; c.a *= alpha; texts[i].color = c;
                }
                yield return null;
            }
            _death = null;
        }
    }
}
