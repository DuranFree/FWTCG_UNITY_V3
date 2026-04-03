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

        // ── Status badges (▲ buff / ▲ equip / ▼ debuff) — DEV-25 ────────────
        private GameObject _buffBadge;
        private GameObject _equipBadge;
        private GameObject _debuffBadge;
        private GameObject _statusTooltip;   // one-at-a-time tooltip panel
        private BadgeTip? _currentStatusTip; // tracks which badge opened the tooltip

        // Scale coroutines per badge (Dictionary avoids ref-in-lambda issues)
        private readonly System.Collections.Generic.Dictionary<GameObject, Coroutine>
            _badgeScaleCos = new System.Collections.Generic.Dictionary<GameObject, Coroutine>();

        private enum BadgeTip { Buff, Equip, Debuff }

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
            // Stop all badge scale coroutines
            foreach (var co in _badgeScaleCos.Values)
                if (co != null) StopCoroutine(co);
            _badgeScaleCos.Clear();
            // H-3: destroy floating tooltip to prevent canvas leak when card is removed
            if (_statusTooltip != null) { Destroy(_statusTooltip); _statusTooltip = null; _currentStatusTip = null; }
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
                if (Input.GetMouseButton(0)) return;
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

            // Buff token indicator (legacy icon — kept for backward compat)
            if (_buffTokenIcon != null) _buffTokenIcon.SetActive(false);

            // Status badges: ▲ buff (left-bottom) and ▼ debuff (right-bottom)
            RefreshStatusBadges();

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
            if (CardDragHandler.BlockPointerEvents) return;
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

        // ── Status badge logic (DEV-25) ───────────────────────────────────────

        private void RefreshStatusBadges()
        {
            if (_unit == null || _faceDown) { HideBadges(); return; }

            // Only show badges on in-play cards (base/BF), not hand cards
            bool inPlay = !_unit.CardData.IsSpell && !_unit.CardData.IsEquipment;
            if (!inPlay && _unit.AttachedTo == null) { HideBadges(); return; }

            // ▲ Buff (green) — left
            if (_unit.HasBuff)
            {
                if (_buffBadge == null)
                    _buffBadge = CreateStatusBadge("▲", GameColors.PlayerGreen,
                        new Vector2(-22f, -2f), () => ShowStatusTooltip(BadgeTip.Buff));
                _buffBadge.SetActive(true);
            }
            else if (_buffBadge != null) _buffBadge.SetActive(false);

            // ▲ Equipment (gold) — center
            if (_unit.AttachedEquipment != null)
            {
                if (_equipBadge == null)
                    _equipBadge = CreateStatusBadge("▲", GameColors.GoldLight,
                        new Vector2(0f, -2f), () => ShowStatusTooltip(BadgeTip.Equip));
                _equipBadge.SetActive(true);
            }
            else if (_equipBadge != null) _equipBadge.SetActive(false);

            // ▼ Debuff (red) — right
            if (_unit.HasDebuff)
            {
                if (_debuffBadge == null)
                    _debuffBadge = CreateStatusBadge("▼", GameColors.EnemyRed,
                        new Vector2(22f, -2f), () => ShowStatusTooltip(BadgeTip.Debuff));
                _debuffBadge.SetActive(true);
            }
            else if (_debuffBadge != null) _debuffBadge.SetActive(false);
        }

        private void HideBadges()
        {
            if (_buffBadge   != null) _buffBadge.SetActive(false);
            if (_equipBadge  != null) _equipBadge.SetActive(false);
            if (_debuffBadge != null) _debuffBadge.SetActive(false);
        }

        /// <summary>
        /// Creates a floating status badge that hangs below the card.
        /// Layout: Glow (behind) + Body (dark glass bg + outline) + Symbol text.
        /// Hover: scale 1→1.22, right-click: show tooltip.
        /// </summary>
        private GameObject CreateStatusBadge(string symbol, Color badgeColor,
                                             Vector2 pos, System.Action onRightClick)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // ── Container — positioned at card bottom edge (inside card bounds) ─
            // Note: badges use pivot-bottom + positive y so they stay within the card's
            // RectTransform rect and aren't occluded by sibling panels (e.g. PlayerHandZone).
            var container = new GameObject("BadgeContainer_" + symbol);
            container.transform.SetParent(transform, false);
            var cRT             = container.AddComponent<RectTransform>();
            cRT.sizeDelta       = new Vector2(22f, 20f);
            cRT.anchorMin       = new Vector2(0.5f, 0f);
            cRT.anchorMax       = new Vector2(0.5f, 0f);
            cRT.pivot           = new Vector2(0.5f, 0f);   // bottom pivot → sits at card bottom
            cRT.anchoredPosition = new Vector2(pos.x, 2f); // 2px above card bottom, inside bounds

            // ── Glow layer (soft halo, renders before body) ────────────────────
            var glow    = new GameObject("Glow");
            glow.transform.SetParent(container.transform, false);
            var glowRT  = glow.AddComponent<RectTransform>();
            glowRT.anchorMin = glowRT.anchorMax = new Vector2(0.5f, 0.5f);
            glowRT.pivot     = new Vector2(0.5f, 0.5f);
            glowRT.sizeDelta = new Vector2(32f, 30f);      // slightly bigger than body
            var glowImg       = glow.AddComponent<Image>();
            Color gc          = badgeColor; gc.a = 0.20f;
            glowImg.color     = gc;
            glowImg.raycastTarget = false;
            var glowShadow    = glow.AddComponent<Shadow>();
            glowShadow.effectColor    = new Color(0f, 0f, 0f, 0.45f);
            glowShadow.effectDistance = new Vector2(0f, -4f);

            // ── Body — dark glass base ─────────────────────────────────────────
            var body    = new GameObject("Body");
            body.transform.SetParent(container.transform, false);
            var bodyRT  = body.AddComponent<RectTransform>();
            bodyRT.anchorMin = Vector2.zero;
            bodyRT.anchorMax = Vector2.one;
            bodyRT.offsetMin = bodyRT.offsetMax = Vector2.zero;
            var bodyImg = body.AddComponent<Image>();
            bodyImg.color = new Color(0.04f, 0.06f, 0.14f, 0.90f);
            bodyImg.raycastTarget = false;
            // Drop shadow for floating depth
            var bodyShadow    = body.AddComponent<Shadow>();
            bodyShadow.effectColor    = new Color(0f, 0f, 0f, 0.85f);
            bodyShadow.effectDistance = new Vector2(2f, -3f);
            // Colored outline (badge color at 70%)
            var bodyOutline   = body.AddComponent<Outline>();
            bodyOutline.effectColor    = new Color(badgeColor.r, badgeColor.g, badgeColor.b, 0.70f);
            bodyOutline.effectDistance = new Vector2(1f, -1f);

            // ── Symbol text ────────────────────────────────────────────────────
            var symGO  = new GameObject("Symbol");
            symGO.transform.SetParent(body.transform, false);
            var symRT  = symGO.AddComponent<RectTransform>();
            symRT.anchorMin = Vector2.zero;
            symRT.anchorMax = Vector2.one;
            symRT.offsetMin = symRT.offsetMax = Vector2.zero;
            var symTxt = symGO.AddComponent<Text>();
            symTxt.text           = symbol;
            symTxt.fontSize       = 11;
            symTxt.color          = badgeColor;
            symTxt.alignment      = TextAnchor.MiddleCenter;
            symTxt.font           = font;
            symTxt.raycastTarget  = false;
            var symShadow         = symGO.AddComponent<Shadow>();
            symShadow.effectColor    = new Color(0f, 0f, 0f, 0.90f);
            symShadow.effectDistance = new Vector2(1f, -1f);

            // ── Event trigger: right-click + hover scale ───────────────────────
            var trig = container.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var clickE = new UnityEngine.EventSystems.EventTrigger.Entry();
            clickE.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
            clickE.callback.AddListener(data =>
            {
                var ped = (UnityEngine.EventSystems.PointerEventData)data;
                if (ped.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                    onRightClick?.Invoke();
            });
            trig.triggers.Add(clickE);

            var enterE = new UnityEngine.EventSystems.EventTrigger.Entry();
            enterE.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            enterE.callback.AddListener(_ =>
            {
                if (container == null) return;
                container.transform.SetAsLastSibling();
                ScaleBadge(container, 1.22f, 0.12f);
            });
            trig.triggers.Add(enterE);

            var exitE = new UnityEngine.EventSystems.EventTrigger.Entry();
            exitE.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            exitE.callback.AddListener(_ =>
            {
                if (container == null) return;
                ScaleBadge(container, 1.0f, 0.10f);
            });
            trig.triggers.Add(exitE);

            return container;
        }

        private void ScaleBadge(GameObject badge, float target, float duration)
        {
            if (badge == null) return;
            if (_badgeScaleCos.TryGetValue(badge, out var old) && old != null)
                StopCoroutine(old);
            _badgeScaleCos[badge] = StartCoroutine(BadgeScaleRoutine(badge.transform, target, duration));
        }

        private IEnumerator BadgeScaleRoutine(Transform t, float target, float duration)
        {
            if (t == null) yield break;
            float start   = t.localScale.x;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float s = Mathf.Lerp(start, target, elapsed / duration);
                if (t != null) t.localScale = Vector3.one * s;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one * target;
        }

        private void ShowStatusTooltip(BadgeTip tip)
        {
            if (_unit == null) return;

            // Toggle off if same badge clicked again; switch content if different badge
            if (_statusTooltip != null)
            {
                Destroy(_statusTooltip);
                _statusTooltip = null;
                var prev = _currentStatusTip;
                _currentStatusTip = null;
                if (prev == tip) return;   // same badge → close only
                // different badge → fall through to open new tooltip
            }

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null) return;

            var go = new GameObject("StatusTooltip");
            go.transform.SetParent(rootCanvas.transform, false);
            _statusTooltip = go;
            _currentStatusTip = tip;

            var rt       = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(210f, 0f);  // height auto via ContentSizeFitter

            var bg   = go.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.08f, 0.18f, 0.96f);
            var bgOut = go.AddComponent<Outline>();
            bgOut.effectColor    = new Color(0.04f, 0.78f, 0.73f, 0.5f);
            bgOut.effectDistance = new Vector2(1f, -1f);

            // Position above the card (badges hang below, tooltip pops above)
            var cardRT = (RectTransform)transform;
            Vector2 cardPos;
            UnityEngine.RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(),
                UnityEngine.RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, cardRT.position),
                rootCanvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
                out cardPos);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = cardPos + new Vector2(0f, cardRT.rect.height * 0.5f + 10f);

            var vlg = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.padding               = new UnityEngine.RectOffset(8, 8, 6, 6);
            vlg.spacing               = 4f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            go.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            switch (tip)
            {
                case BadgeTip.Buff:
                    AddTooltipRow(go.transform, "▲ 强化", _unit.BuildBuffSummary(), GameColors.PlayerGreen);
                    break;
                case BadgeTip.Equip:
                    AddTooltipRow(go.transform, "▲ 装备", _unit.BuildEquipSummary(), GameColors.GoldLight);
                    break;
                case BadgeTip.Debuff:
                    AddTooltipRow(go.transform, "▼ 削弱", _unit.BuildDebuffSummary(), GameColors.EnemyRed);
                    break;
            }

            StartCoroutine(AutoDismissTooltip());
        }

        private void AddTooltipRow(Transform parent, string header, string body, Color headerColor)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Header
            var hgo  = new GameObject("H");
            hgo.transform.SetParent(parent, false);
            hgo.AddComponent<UnityEngine.UI.LayoutElement>().minHeight = 16f;
            var htxt = hgo.AddComponent<Text>();
            htxt.text      = header;
            htxt.font      = font;
            htxt.fontSize  = 12;
            htxt.fontStyle = FontStyle.Bold;
            htxt.color     = headerColor;
            htxt.raycastTarget = false;

            // Body
            var bgo  = new GameObject("B");
            bgo.transform.SetParent(parent, false);
            var ble = bgo.AddComponent<UnityEngine.UI.LayoutElement>();
            ble.minHeight = 14f;
            var btxt = bgo.AddComponent<Text>();
            btxt.text             = body;
            btxt.font             = font;
            btxt.fontSize         = 11;
            btxt.color            = new Color(0.82f, 0.82f, 0.82f, 1f);
            btxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            btxt.verticalOverflow   = VerticalWrapMode.Overflow;
            btxt.raycastTarget      = false;
        }

        private IEnumerator AutoDismissTooltip()
        {
            // Wait one frame so the click that opened it doesn't immediately close it
            yield return null;
            while (_statusTooltip != null)
            {
                if (UnityEngine.Input.GetMouseButtonDown(0) ||
                    UnityEngine.Input.GetMouseButtonDown(1))
                {
                    if (_statusTooltip != null) { Destroy(_statusTooltip); _statusTooltip = null; _currentStatusTip = null; }
                    yield break;
                }
                yield return null;
            }
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
