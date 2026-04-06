using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FWTCG.Core;
using FWTCG.FX;
using FWTCG.VFX;

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

        // VFX-7a: frame overlay (gold/silver border)
        [SerializeField] private Image _frameOverlay;
        // VFX-7k: card glow overlay (ally green / enemy red, sprite-based)
        [SerializeField] private Image _glowOverlay;

        // VFX-3: dissolve death material (KillDissolveFX.mat, wired by SceneBuilder)
        [SerializeField] private Material _killDissolveMat;
        private Material _clonedDissolveMat; // per-instance clone, destroyed in OnDestroy

        private UnitInstance _unit;

        private bool _isPlayerCard;
        private bool _isDiscardView;   // discard/exile viewer — hide runtime-state decorations
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
        private GameObject _deathGhost; // DEV-29: tracked so OnDestroy can clean up mid-flight ghost
        private CardGlow _cardGlow;

        // ── Stat glow: breathing background images when values are modified ──
        private Image     _atkGlowImg;
        private Image     _costGlowImg;
        private Image     _schGlowImg;
        private Coroutine _atkBreath;
        private Coroutine _costBreath;
        private Coroutine _schBreath;

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

        // ── DEV-28: Target highlight ─────────────────────────────────────────
        private Image     _targetBorder;
        private Coroutine _targetPulse;

        // ── DEV-28: Selected orbit light ─────────────────────────────────────
        private GameObject _orbitDot;
        private Coroutine  _orbitRoutine;

        // ── DEV-28: Hero aura ────────────────────────────────────────────────
        private Image     _heroAura;
        private Coroutine _heroAuraPulse;

        // ── DEV-28: Hand enter animation ─────────────────────────────────────
        private bool _enterAnimPlayed;
        private Coroutine _enterAnimCoroutine;

        // ── DEV-29: Card back overlay (geometric pattern) ────────────────────
        private GameObject _cardBackOverlay;
        private Image      _cardBackSpriteImg; // VFX-7g: sprite-based card back

        // ── DEV-30: Foil Sweep (V6) ──────────────────────────────────────────
        private Image     _shineOverlay;  // lazy-created full-size overlay with CardShine shader
        private Material  _shineMat;      // per-card clone destroyed in OnDestroy
        private Coroutine _foilSweep;

        // ── DEV-30: Playable Spark (V7) ──────────────────────────────────────
        private Coroutine _playableSpark;
        private bool      _lastPlayable;
        private readonly System.Collections.Generic.List<GameObject> _sparkDots =
            new System.Collections.Generic.List<GameObject>();

        // ── VFX-7k: Glow overlay smooth fade state ──────────────────────────
        private float _glowCurrentAlpha;
        private float _glowTargetAlpha;
        private Color _glowTargetColor = Color.clear;
        public const float GLOW_FADE_SPEED = 4f;

        // ── VFX-7o: Status particle FX (dynamic mount/unmount) ────────────────
        private GameObject _stunFX;           // ElectricFX for stunned
        private GameObject _sleepFX;          // Zzz for exhausted/sleeping

        // ── VFX-4: Battlefield visual details ────────────────────────────────
        private GameObject _idleFX;          // persistent rune-type particle (snapped)
        private GameObject _shieldFX;        // persistent Shield/Barrier FX
        private GameObject _shadowImage;     // offset shadow Image below card
        private bool       _idleFXSpawned;
        private bool       _bfVisualsApplied; // guard: only apply rotation/shadow once per placement

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
            if (_enterAnimCoroutine != null) StopCoroutine(_enterAnimCoroutine);
            _enterAnimCoroutine = null;
            if (_stunPulse   != null) StopCoroutine(_stunPulse);
            if (_shake       != null) StopCoroutine(_shake);   // DEV-26
            if (_flash       != null) StopCoroutine(_flash);   // DEV-26
            if (_death       != null) StopCoroutine(_death);   // DEV-26
            if (_deathGhost  != null) { Destroy(_deathGhost); _deathGhost = null; } // DEV-29
            if (_atkBreath   != null) StopCoroutine(_atkBreath);
            if (_costBreath  != null) StopCoroutine(_costBreath);
            if (_schBreath   != null) StopCoroutine(_schBreath);
            if (_liftFloat    != null) StopCoroutine(_liftFloat);
            if (_returnToRest != null) StopCoroutine(_returnToRest);
            if (_targetPulse  != null) StopCoroutine(_targetPulse);
            if (_orbitRoutine != null) StopCoroutine(_orbitRoutine);
            if (_heroAuraPulse != null) StopCoroutine(_heroAuraPulse);
            if (_orbitDot != null) Destroy(_orbitDot);
            // Stop all badge scale coroutines
            foreach (var co in _badgeScaleCos.Values)
                if (co != null) StopCoroutine(co);
            _badgeScaleCos.Clear();
            // H-3: destroy floating tooltip to prevent canvas leak when card is removed
            if (_statusTooltip != null) { Destroy(_statusTooltip); _statusTooltip = null; _currentStatusTip = null; }
            // VFX-3: clear dissolve material on all images before destroying clone (prevents dangling ref if coroutine was interrupted)
            if (_clonedDissolveMat != null)
            {
                foreach (var img in GetComponentsInChildren<Image>(true))
                    if (img != null) img.material = null;
                SafeDestroy(_clonedDissolveMat);
                _clonedDissolveMat = null;
            }
            // DEV-30 V6: destroy cloned shine material
            if (_foilSweep != null) StopCoroutine(_foilSweep);
            if (_shineMat  != null) { SafeDestroy(_shineMat); _shineMat = null; }
            // DEV-30 V7: stop sparks and destroy dot GOs
            if (_playableSpark != null) StopCoroutine(_playableSpark);
            foreach (var d in _sparkDots) if (d) SafeDestroy(d);
            _sparkDots.Clear();
            // VFX-7o: cleanup status FX
            if (_stunFX  != null) { SafeDestroy(_stunFX);  _stunFX = null; }
            if (_sleepFX != null) { SafeDestroy(_sleepFX); _sleepFX = null; }
            // VFX-4: cleanup idle FX, shield FX, and shadow
            if (_idleFX != null) { SafeDestroy(_idleFX); _idleFX = null; }
            if (_shieldFX != null) { SafeDestroy(_shieldFX); _shieldFX = null; }
            if (_shadowImage != null) { SafeDestroy(_shadowImage); _shadowImage = null; }
        }

        // ── VFX-7k: Smooth glow overlay fade ──────────────────────────────
        private void Update()
        {
            if (_glowOverlay == null) return;
            if (Mathf.Approximately(_glowCurrentAlpha, _glowTargetAlpha)) return;

            _glowCurrentAlpha = Mathf.MoveTowards(_glowCurrentAlpha, _glowTargetAlpha,
                GLOW_FADE_SPEED * Time.deltaTime);
            var c = _glowTargetColor;
            c.a = _glowCurrentAlpha;
            _glowOverlay.color = c;
            _glowOverlay.enabled = _glowCurrentAlpha > 0.01f;
        }

        /// <summary>VFX-7k: Set glow overlay target. Color: green for ally, red for enemy.</summary>
        public void SetGlowTarget(float alpha, Color color)
        {
            _glowTargetAlpha = alpha;
            _glowTargetColor = color;
        }

        /// <summary>VFX-7k: Show glow for hover/select (ally green or enemy red). Half intensity.</summary>
        public void ShowGlow()
        {
            if (_glowOverlay == null) return;
            var color = _isPlayerCard ? GameColors.PlayerGreen : GameColors.EnemyRed;
            SetGlowTarget(0.32f, color);
        }

        /// <summary>VFX-7k: Hide glow with smooth fade-out.</summary>
        public void HideGlow()
        {
            SetGlowTarget(0f, _glowTargetColor);
        }

        public void Setup(UnitInstance unit, bool isPlayerCard, Action<UnitInstance> onClick,
                          Action<UnitInstance> onRightClick  = null,
                          Action<UnitInstance> onHoverEnter  = null,
                          Action<UnitInstance> onHoverExit   = null,
                          bool isDiscardView = false,
                          bool playEnterAnim = false)
        {
            bool isNewUnit = _unit != unit;
            _unit = unit;
            _isPlayerCard = isPlayerCard;
            _isDiscardView = isDiscardView;
            _onClick = onClick;
            _onRightClick = onRightClick;
            _onHoverEnter = onHoverEnter;
            _onHoverExit  = onHoverExit;
            _costInsufficient = false;

            // DEV-29: when reusing this CardView for a new unit, clear hero aura and
            // reset enter-animation flag so the new unit gets a fresh start
            if (isNewUnit)
            {
                ClearHeroAura();
                _enterAnimPlayed = false;
            }

            Refresh();

            if (_clickButton != null)
                _clickButton.interactable = onClick != null;

            // DEV-28: Hero aura — disabled per VFX-7 (frame border replaces aura)
            // if (isNewUnit && unit != null && unit.CardData != null && unit.CardData.IsHero)
            //     StartHeroAura();

            // Enter animation — only for hand cards (playEnterAnim=true).
            // Base/BF cards appear immediately to avoid position/scale animation
            // fighting with HLG layout and causing delayed visibility.
            if (playEnterAnim && isNewUnit && !_enterAnimPlayed && gameObject.activeInHierarchy)
                _enterAnimCoroutine = StartCoroutine(EnterAnimRoutine());
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (_unit != null && !_faceDown)
            {
                ShowGlow(); // VFX-7k
                _onHoverEnter?.Invoke(_unit);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (_unit != null && !_faceDown)
            {
                if (!_selected) HideGlow(); // VFX-7k: keep glow if selected
                _onHoverExit?.Invoke(_unit);
            }
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
            if (_cardBg   != null)
            {
                if (hide)
                    _cardBg.enabled = false; // hide background entirely — card back sprite covers all
                else
                {
                    _cardBg.enabled = true;
                    _cardBg.color = _selected ? GameColors.CardSelected
                        : (_unit != null && _unit.Exhausted ? GameColors.CardExhausted
                        : (_isPlayerCard ? GameColors.CardPlayer : GameColors.CardEnemy));
                }
            }
            if (_clickButton != null) _clickButton.interactable = !hide;
            if (_stunnedOverlay != null) _stunnedOverlay.gameObject.SetActive(false);
            if (_buffTokenIcon != null) _buffTokenIcon.SetActive(false);

            // Hide badge circles (CostBadge, AtkBadge, SchCostBg) when face-down
            if (_costText != null && _costText.transform.parent != null
                && _costText.transform.parent != transform)
                _costText.transform.parent.gameObject.SetActive(!hide);
            if (_atkText != null && _atkText.transform.parent != null
                && _atkText.transform.parent != transform)
                _atkText.transform.parent.gameObject.SetActive(!hide);
            if (_schCostBg != null) _schCostBg.gameObject.SetActive(!hide);
            if (_schCostText != null) _schCostText.gameObject.SetActive(!hide);
            if (_frameOverlay != null) _frameOverlay.enabled = !hide;

            // DEV-29 + VFX-7g: show/hide card-back overlay (sprite or geometric)
            if (hide)
                EnsureCardBackOverlay();
            else
            {
                if (_cardBackOverlay != null) _cardBackOverlay.SetActive(false);
                if (_cardBackSpriteImg != null) _cardBackSpriteImg.gameObject.SetActive(false);
            }
        }

        // DEV-29 + VFX-7g: lazy-create card back overlay (sprite if available, else geometric)
        private void EnsureCardBackOverlay()
        {
            // VFX-7g: try sprite-based card back first
            var backSprite = CardBackManager.GetCardBackSprite();
            if (backSprite != null)
            {
                if (_cardBackSpriteImg == null)
                {
                    var go = new GameObject("CardBackSprite");
                    go.transform.SetParent(transform, false);
                    go.transform.SetAsLastSibling();
                    var rt = go.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    _cardBackSpriteImg = go.AddComponent<Image>();
                    _cardBackSpriteImg.raycastTarget = false;
                    _cardBackSpriteImg.preserveAspect = true;
                }
                _cardBackSpriteImg.sprite = backSprite;
                _cardBackSpriteImg.gameObject.SetActive(true);
                if (_cardBackOverlay != null) _cardBackOverlay.SetActive(false);
                return;
            }

            // Hide sprite back if switching away
            if (_cardBackSpriteImg != null) _cardBackSpriteImg.gameObject.SetActive(false);

            if (_cardBackOverlay != null)
            {
                _cardBackOverlay.SetActive(true);
                return;
            }

            _cardBackOverlay = new GameObject("CardBackOverlay");
            _cardBackOverlay.transform.SetParent(transform, false);
            _cardBackOverlay.transform.SetAsLastSibling();

            // Subtle highlight color: slightly lighter than CardFaceDown
            Color faceDown = GameColors.CardFaceDown;
            Color patternColor = new Color(
                Mathf.Min(1f, faceDown.r + 0.14f),
                Mathf.Min(1f, faceDown.g + 0.17f),
                Mathf.Min(1f, faceDown.b + 0.22f),
                0.85f);

            const float borderW = 2.5f; // border strip thickness in pixels
            const float inset    = 4f;   // inset from card edge

            // 4 border strips anchored to each edge (full-width/height stretch within card)
            // Top strip: anchored to top edge, spans full width (inset from sides)
            AddCardBackStrip(_cardBackOverlay.transform, "BackTop",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                offsetMin: new Vector2(inset, -(inset + borderW)), offsetMax: new Vector2(-inset, -inset), patternColor);
            // Bottom strip
            AddCardBackStrip(_cardBackOverlay.transform, "BackBottom",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                offsetMin: new Vector2(inset, inset), offsetMax: new Vector2(-inset, inset + borderW), patternColor);
            // Left strip: anchored to left edge, spans full height (inset from top/bottom)
            AddCardBackStrip(_cardBackOverlay.transform, "BackLeft",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 1f),
                offsetMin: new Vector2(inset, inset), offsetMax: new Vector2(inset + borderW, -inset), patternColor);
            // Right strip
            AddCardBackStrip(_cardBackOverlay.transform, "BackRight",
                anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 1f),
                offsetMin: new Vector2(-(inset + borderW), inset), offsetMax: new Vector2(-inset, -inset), patternColor);

            // Center diamond (45° rotated square)
            var diamondGO = new GameObject("BackDiamond");
            diamondGO.transform.SetParent(_cardBackOverlay.transform, false);
            var diamondRT = diamondGO.AddComponent<RectTransform>();
            diamondRT.anchorMin = diamondRT.anchorMax = new Vector2(0.5f, 0.5f);
            diamondRT.pivot = new Vector2(0.5f, 0.5f);
            diamondRT.sizeDelta = new Vector2(28f, 28f);
            diamondRT.anchoredPosition = Vector2.zero;
            diamondGO.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var diamondImg = diamondGO.AddComponent<Image>();
            diamondImg.color = new Color(patternColor.r, patternColor.g, patternColor.b, 0.60f);
            diamondImg.raycastTarget = false;
        }

        private static void AddCardBackStrip(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        public void Refresh()
        {
            if (_faceDown) { RefreshFaceDown(); return; }
            if (_unit == null) return;

            if (_nameText != null)
                _nameText.text = _unit.UnitName;

            if (_costText != null)
            {
                _costText.text = _unit.EffectiveCost.ToString();
                RefreshStatGlow(ref _costBreath, ref _costGlowImg,
                    (RectTransform)_costText.transform,
                    _unit.CostModifier != 0);
            }

            if (_atkText != null)
            {
                if (_unit.CardData.IsSpell)
                {
                    _atkText.text  = "法";
                    _atkText.color = Color.white;
                    StopStatGlow(ref _atkBreath, _atkGlowImg);
                }
                else
                {
                    // Effective ATK = CurrentAtk (includes equipment/buffs) + TempAtkBonus
                    int effAtk = _unit.CurrentAtk + _unit.TempAtkBonus;
                    _atkText.text = _unit.CurrentHp != _unit.CurrentAtk
                        ? $"{_unit.CurrentHp}/{effAtk}"
                        : $"{effAtk}";

                    // Glow when any modifier is active
                    bool modified = _unit.HasBuff || _unit.HasDebuff
                                 || _unit.AttachedEquipment != null
                                 || _unit.TempAtkBonus != 0;
                    RefreshAtkGlow(modified);
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

            // Stunned overlay + VFX-7o: stun particle FX
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
                // VFX-7o: mount/unmount ElectricFX for stun
                if (stunned && _stunFX == null)
                {
                    var pfb = Resources.Load<GameObject>("Prefabs/FX/ElectricFX");
                    if (pfb != null) _stunFX = FXTool.DoSnapFX(pfb, transform, Vector3.zero, 0f);
                }
                else if (!stunned && _stunFX != null)
                { Destroy(_stunFX); _stunFX = null; }
            }

            // VFX-7a: frame overlay — color by card type
            if (_frameOverlay != null)
            {
                bool isLegend = _unit.CardData.Id != null && _unit.CardData.Id.Contains("legend");
                bool isHero = _unit.CardData.IsHero;
                bool isSkill = _unit.CardData.IsSpell || _unit.CardData.IsEquipment;

                Sprite frameSpr;
                Color frameTint;
                if (isLegend)
                {
                    frameSpr = Resources.Load<Sprite>("UI/frame_gold");
                    frameTint = new Color(1f, 0.85f, 0.3f, 1f); // 金色
                }
                else if (isHero)
                {
                    frameSpr = Resources.Load<Sprite>("UI/frame_gold");
                    frameTint = new Color(0.7f, 0.4f, 0.9f, 1f); // 紫色
                }
                else if (isSkill)
                {
                    frameSpr = Resources.Load<Sprite>("UI/frame_gold");
                    frameTint = new Color(0.9f, 0.25f, 0.2f, 1f); // 红色
                }
                else
                {
                    frameSpr = Resources.Load<Sprite>("UI/frame_silver");
                    frameTint = new Color(1f, 0.82f, 0.86f, 1f); // 浅粉色
                }

                if (frameSpr != null)
                {
                    _frameOverlay.sprite = frameSpr;
                    _frameOverlay.color = frameTint;
                    _frameOverlay.enabled = true;
                }
                else
                    _frameOverlay.enabled = false;
            }

            // Exhausted overlay (gray dim) + VFX-7o: sleep particle FX
            if (_exhaustedOverlay != null)
            {
                bool exhausted = !_isDiscardView && _unit.Exhausted && !_unit.Stunned;
                _exhaustedOverlay.gameObject.SetActive(exhausted);
                if (exhausted && _sleepFX == null)
                {
                    var pfb = Resources.Load<GameObject>("Prefabs/FX/Zzz");
                    if (pfb != null) _sleepFX = FXTool.DoSnapFX(pfb, transform, new Vector3(15f, 25f, 0f), 0f);
                }
                else if (!exhausted && _sleepFX != null)
                { Destroy(_sleepFX); _sleepFX = null; }
            }

            // Glow border (playable = affordable + not exhausted for hand cards)
            bool playable = _isPlayerCard && !_unit.Exhausted && !_costInsufficient;
            if (_cardGlow != null)
                _cardGlow.SetPlayable(playable);

            // DEV-30 V7: playable spark — start/stop on state change
            if (playable && !_lastPlayable && gameObject.activeInHierarchy)
                _playableSpark ??= StartCoroutine(PlayableSparkRoutine());
            else if (!playable && _lastPlayable)
            {
                if (_playableSpark != null) { StopCoroutine(_playableSpark); _playableSpark = null; }
                foreach (var d in _sparkDots) if (d) Destroy(d);
                _sparkDots.Clear();
            }
            _lastPlayable = playable;

            // VFX-7l: equipment glow — subtle gold when unit has equipment attached
            if (_glowOverlay != null && _unit.AttachedEquipment != null
                && _glowTargetAlpha < 0.01f) // don't override hover/select glow
            {
                SetGlowTarget(0.35f, GameColors.Gold);
            }

            // Buff token indicator (legacy icon — kept for backward compat)
            if (_buffTokenIcon != null) _buffTokenIcon.SetActive(false);

            // Status badges: ▲ buff (left-bottom) and ▼ debuff (right-bottom) — hidden in discard viewer
            if (_isDiscardView) HideBadges();
            else RefreshStatusBadges();

            // Schematic (rune) cost display — uses Effective values for runtime changes
            if (_schCostText != null && _schCostBg != null)
            {
                int schCost           = _unit.EffectiveRuneCost;
                Data.RuneType schType = _unit.EffectiveRuneType;
                if (schCost > 0)
                {
                    _schCostText.gameObject.SetActive(true);
                    _schCostBg.gameObject.SetActive(true);
                    string rtShort = "";
                    switch (schType)
                    {
                        case Data.RuneType.Blazing:  rtShort = "炽"; break;
                        case Data.RuneType.Radiant:  rtShort = "灵"; break;
                        case Data.RuneType.Verdant:  rtShort = "翠"; break;
                        case Data.RuneType.Crushing: rtShort = "摧"; break;
                        default:                     rtShort = "符"; break;
                    }
                    _schCostText.text = $"{rtShort}×{schCost}";
                    _schCostBg.color  = GameColors.GetRuneColor(schType);

                    bool schModified = _unit.RuneCostModifier != 0 || _unit.RuneTypeOverride.HasValue;
                    RefreshStatGlow(ref _schBreath, ref _schGlowImg,
                        (RectTransform)_schCostBg.transform, schModified);
                }
                else
                {
                    _schCostText.gameObject.SetActive(false);
                    _schCostBg.gameObject.SetActive(false);
                    StopStatGlow(ref _schBreath, _schGlowImg);
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
                // DEV-28: start orbit light
                StartOrbit();
                ShowGlow(); // VFX-7k
            }
            else
            {
                _isLifted = false;
                if (_liftFloat != null) { StopCoroutine(_liftFloat); _liftFloat = null; }
                // Animate back to rest position instead of snapping
                if (_returnToRest != null) StopCoroutine(_returnToRest);
                _returnToRest = StartCoroutine(ReturnToRestRoutine(rt.anchoredPosition.y));
                // DEV-28: stop orbit light
                StopOrbit();
                HideGlow(); // VFX-7k
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

        // ── ATK glow helpers ─────────────────────────────────────────────────

        // ── Shared stat-glow helpers ─────────────────────────────────────────

        /// <summary>Start or stop a white breathing glow behind target. Lazily creates Image.</summary>
        private void RefreshStatGlow(ref Coroutine co, ref Image glowImg,
                                     RectTransform target, bool modified)
        {
            if (modified)
            {
                if (glowImg == null) glowImg = CreateGlowImgBehind(target);
                if (co == null)
                {
                    var captured = glowImg;
                    co = StartCoroutine(BreathGlowRoutine(() => captured));
                }
            }
            else
            {
                StopStatGlow(ref co, glowImg);
            }
        }

        private void StopStatGlow(ref Coroutine co, Image glowImg)
        {
            if (co != null) { StopCoroutine(co); co = null; }
            if (glowImg != null) glowImg.color = new Color(1f, 1f, 1f, 0f);
        }

        private Image CreateGlowImgBehind(RectTransform target)
        {
            if (target == null) return null;
            var go   = new GameObject("StatGlow");
            var glRT = go.AddComponent<RectTransform>();
            glRT.SetParent(target.parent, false);
            glRT.anchorMin        = target.anchorMin;
            glRT.anchorMax        = target.anchorMax;
            glRT.pivot            = target.pivot;
            glRT.anchoredPosition = target.anchoredPosition;
            glRT.sizeDelta        = target.sizeDelta + new Vector2(6f, 4f);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            go.transform.SetSiblingIndex(target.GetSiblingIndex());
            var img = go.AddComponent<Image>();
            img.color         = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            return img;
        }

        private IEnumerator BreathGlowRoutine(System.Func<Image> getImg)
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * 1.4f;
                float alpha = Mathf.Lerp(0.08f, 0.45f, (Mathf.Sin(t) + 1f) * 0.5f);
                var img = getImg();
                if (img != null) img.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
        }

        private void RefreshAtkGlow(bool modified)
        {
            if (_atkText == null) return;
            _atkText.color = Color.white;
            RefreshStatGlow(ref _atkBreath, ref _atkGlowImg,
                (RectTransform)_atkText.transform, modified);
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
        public void PlayDeathAnimation(Vector2? flyTarget = null, Canvas canvas = null)
        {
            if (_death != null) return;
            _death = StartCoroutine(DeathRoutine(flyTarget, canvas));
        }

        // ── Status badge logic (DEV-25) ───────────────────────────────────────

        private void RefreshStatusBadges()
        {
            if (_unit == null || _faceDown) { HideBadges(); return; }

            // Only show badges on in-play cards (base/BF), not hand cards
            bool inPlay = !_unit.CardData.IsSpell && !_unit.CardData.IsEquipment;
            if (!inPlay && _unit.AttachedTo == null) { HideBadges(); return; }

            // ▲ Buff (green) — left  [x=-28: above card, left slot]
            if (_unit.HasBuff)
            {
                if (_buffBadge == null)
                    _buffBadge = CreateStatusBadge("▲", GameColors.PlayerGreen,
                        new Vector2(-28f, 0f), () => ShowStatusTooltip(BadgeTip.Buff));
                _buffBadge.SetActive(true);
            }
            else if (_buffBadge != null) _buffBadge.SetActive(false);

            // ▲ Equipment (gold) — center  [x=0: above card, center slot] — DEV-30 F4: shows equip name
            if (_unit.AttachedEquipment != null)
            {
                if (_equipBadge == null)
                {
                    string eName = _unit.AttachedEquipment.CardData?.CardName ?? "装备";
                    string label = eName.Length > 4 ? eName.Substring(0, 4) : eName;
                    _equipBadge = CreateStatusBadge(label, GameColors.Gold,
                        new Vector2(0f, 0f), () => ShowStatusTooltip(BadgeTip.Equip));
                }
                _equipBadge.SetActive(true);
            }
            else if (_equipBadge != null) _equipBadge.SetActive(false);

            // ▼ Debuff (red) — right  [x=+28: above card, right slot]
            if (_unit.HasDebuff)
            {
                if (_debuffBadge == null)
                    _debuffBadge = CreateStatusBadge("▼", GameColors.EnemyRed,
                        new Vector2(28f, 0f), () => ShowStatusTooltip(BadgeTip.Debuff));
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

            // ── Container — narrow strip above the card top edge ─────────────
            // new GameObject() only has Transform; must AddComponent<RectTransform>
            // BEFORE SetParent so the cast is valid. Set anchor/pivot before size/position.
            var container = new GameObject("BadgeContainer_" + symbol);
            var cRT = container.AddComponent<RectTransform>();
            cRT.SetParent(transform, false);
            cRT.anchorMin        = new Vector2(0.5f, 1f);  // anchor: card top-center
            cRT.anchorMax        = new Vector2(0.5f, 1f);
            cRT.pivot            = new Vector2(0.5f, 0f);  // pivot: badge bottom → hangs above card
            cRT.sizeDelta        = new Vector2(20f, 16f);
            cRT.anchoredPosition = new Vector2(pos.x, 2f); // 2px gap above card top

            // ── Escape parent LayoutGroup ────────────────────────────────────
            container.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;

            // ── Canvas override: force badge to render above all sibling panels ─
            var badgeCanvas = container.AddComponent<UnityEngine.Canvas>();
            badgeCanvas.overrideSorting = true;
            badgeCanvas.sortingOrder    = 100;
            container.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // ── Glow layer (soft halo, renders before body) ────────────────────
            var glow   = new GameObject("Glow");
            var glowRT = glow.AddComponent<RectTransform>();
            glowRT.SetParent(container.transform, false);
            glowRT.anchorMin        = new Vector2(0.5f, 0.5f);
            glowRT.anchorMax        = new Vector2(0.5f, 0.5f);
            glowRT.pivot            = new Vector2(0.5f, 0.5f);
            glowRT.sizeDelta        = new Vector2(22f, 18f);
            glowRT.anchoredPosition = Vector2.zero;
            var glowImg = glow.AddComponent<Image>();
            Color gc    = badgeColor; gc.a = 0.20f;
            glowImg.color         = gc;
            glowImg.raycastTarget = false;

            // ── Body — dark glass base ─────────────────────────────────────────
            var body   = new GameObject("Body");
            var bodyRT = body.AddComponent<RectTransform>();
            bodyRT.SetParent(container.transform, false);
            bodyRT.anchorMin        = Vector2.zero;
            bodyRT.anchorMax        = Vector2.one;
            bodyRT.pivot            = new Vector2(0.5f, 0.5f);
            bodyRT.sizeDelta        = Vector2.zero;
            bodyRT.anchoredPosition = Vector2.zero;
            var bodyImg = body.AddComponent<Image>();
            bodyImg.color         = new Color(0.04f, 0.06f, 0.14f, 0.90f);
            bodyImg.raycastTarget = false;

            // ── Symbol text ────────────────────────────────────────────────────
            var symGO  = new GameObject("Symbol");
            var symRT  = symGO.AddComponent<RectTransform>();
            symRT.SetParent(body.transform, false);
            symRT.anchorMin        = Vector2.zero;
            symRT.anchorMax        = Vector2.one;
            symRT.pivot            = new Vector2(0.5f, 0.5f);
            symRT.sizeDelta        = Vector2.zero;
            symRT.anchoredPosition = Vector2.zero;
            var symTxt = symGO.AddComponent<Text>();
            symTxt.text           = symbol;
            symTxt.fontSize       = 11;
            symTxt.color          = badgeColor;
            symTxt.alignment      = TextAnchor.MiddleCenter;
            symTxt.font           = font;
            symTxt.raycastTarget  = false;

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
                    AddTooltipRow(go.transform, "▲ 装备", _unit.BuildEquipSummary(), GameColors.Gold);
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

        // ── DEV-28: Target highlight ─────────────────────────────────────────

        /// <summary>Highlight this card as a valid spell target (green pulsing border).</summary>
        // VFX-7j: smooth target highlight with MoveTowards fade
        public const float TARGET_FADE_SPEED = 2f;
        private float _targetAlpha;
        private float _targetAlphaGoal;

        public void SetTargeted(bool targeted)
        {
            _targetAlphaGoal = targeted ? 1f : 0f;
            if (targeted)
            {
                if (_targetBorder == null)
                    _targetBorder = CreateOverlayImage("TargetBorder",
                        new Color(0.29f, 0.87f, 0.50f, 0f), sizeDelta: Vector2.zero, asOutline: true);
                if (_targetPulse != null) StopCoroutine(_targetPulse);
                _targetPulse = StartCoroutine(TargetPulseRoutine());
            }
            else
            {
                // Don't stop pulse immediately — let it fade out
                if (_targetPulse != null) { StopCoroutine(_targetPulse); _targetPulse = null; }
                if (_targetFadeOut != null) StopCoroutine(_targetFadeOut);
                _targetFadeOut = StartCoroutine(TargetFadeOutRoutine());
            }
        }

        private Coroutine _targetFadeOut;

        private IEnumerator TargetFadeOutRoutine()
        {
            while (_targetAlpha > 0.01f && _targetBorder != null)
            {
                _targetAlpha = Mathf.MoveTowards(_targetAlpha, 0f, TARGET_FADE_SPEED * Time.deltaTime);
                var c = _targetBorder.color;
                _targetBorder.color = new Color(c.r, c.g, c.b, _targetAlpha);
                yield return null;
            }
            if (_targetBorder != null)
                _targetBorder.color = new Color(0.29f, 0.87f, 0.50f, 0f);
            _targetAlpha = 0f;
            _targetFadeOut = null;
        }

        private IEnumerator TargetPulseRoutine()
        {
            const float period = 1.2f;
            float t = 0f;
            var baseCol = new Color(0.29f, 0.87f, 0.50f, 1f);
            // VFX-7j: smooth fade-in
            while (_targetAlpha < 0.99f && _targetBorder != null)
            {
                _targetAlpha = Mathf.MoveTowards(_targetAlpha, 1f, TARGET_FADE_SPEED * Time.deltaTime);
                float pulseA = (Mathf.Sin(t * Mathf.PI * 2f / period) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(0.3f, 0.85f, pulseA) * _targetAlpha;
                _targetBorder.color = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
                t += Time.deltaTime;
                yield return null;
            }
            _targetAlpha = 1f;
            // Normal pulse
            while (_targetBorder != null)
            {
                float alpha = (Mathf.Sin(t * Mathf.PI * 2f / period) + 1f) * 0.5f;
                alpha = Mathf.Lerp(0.3f, 0.85f, alpha);
                _targetBorder.color = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ── DEV-28: Selected orbit light ─────────────────────────────────────

        private void StartOrbit()
        {
            if (_orbitDot == null)
            {
                _orbitDot = new GameObject("OrbitDot");
                _orbitDot.transform.SetParent(transform, false);
                var rt = _orbitDot.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(10f, 10f);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = _orbitDot.AddComponent<Image>();
                img.color = new Color(1f, 0.92f, 0.6f, 0.9f);
                img.raycastTarget = false;
                _orbitDot.transform.SetAsLastSibling();
            }
            _orbitDot.SetActive(true);
            if (_orbitRoutine != null) StopCoroutine(_orbitRoutine);
            _orbitRoutine = StartCoroutine(OrbitRoutine());
        }

        private void StopOrbit()
        {
            if (_orbitRoutine != null) { StopCoroutine(_orbitRoutine); _orbitRoutine = null; }
            if (_orbitDot != null) _orbitDot.SetActive(false);
        }

        private IEnumerator OrbitRoutine()
        {
            const float radius   = 60f;
            const float period   = 6f;   // seconds per full revolution
            float angle = 0f;
            var dotRT = _orbitDot != null ? _orbitDot.GetComponent<RectTransform>() : null;
            while (_orbitDot != null && _orbitDot.activeSelf)
            {
                if (dotRT != null)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    dotRT.anchoredPosition = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
                }
                angle = (angle + 360f / period * Time.deltaTime) % 360f;
                yield return null;
            }
        }

        // ── DEV-28: Hero aura ────────────────────────────────────────────────

        // DEV-29: stop and destroy hero aura when CardView is reused for a non-hero unit
        private void ClearHeroAura()
        {
            if (_heroAuraPulse != null) { StopCoroutine(_heroAuraPulse); _heroAuraPulse = null; }
            if (_heroAura != null)
            {
                // Use DestroyImmediate in EditMode (e.g. tests); Destroy at runtime
                if (Application.isPlaying) Destroy(_heroAura.gameObject);
                else DestroyImmediate(_heroAura.gameObject);
                _heroAura = null;
            }
        }

        private void StartHeroAura()
        {
            if (_heroAura == null)
                _heroAura = CreateOverlayImage("HeroAura",
                    new Color(1f, 0.85f, 0.2f, 0f), sizeDelta: new Vector2(8f, 8f), asOutline: false);
            if (_heroAuraPulse != null) StopCoroutine(_heroAuraPulse);
            _heroAuraPulse = StartCoroutine(HeroAuraPulseRoutine());
        }

        private IEnumerator HeroAuraPulseRoutine()
        {
            const float period = 4f;
            float t = 0f;
            while (_heroAura != null)
            {
                float alpha = (Mathf.Sin(t * Mathf.PI * 2f / period) + 1f) * 0.5f;
                alpha = Mathf.Lerp(0.25f, 0.60f, alpha);
                _heroAura.color = new Color(1f, 0.85f, 0.2f, alpha);
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ── DEV-28: Hand enter animation ─────────────────────────────────────

        /// <summary>
        /// Stops the enter animation if running and restores alpha/scale to final values.
        /// Called by DropAnimHost before it takes over alpha management to prevent races.
        /// </summary>
        public void CancelEnterAnim()
        {
            if (_enterAnimCoroutine != null)
            {
                StopCoroutine(_enterAnimCoroutine);
                _enterAnimCoroutine = null;
            }
            // Restore scale (EnterAnimRoutine no longer touches alpha)
            transform.localScale = Vector3.one;
            _enterAnimPlayed = true; // prevent re-start

            // Force parent HLG to recalculate — EnterAnimRoutine manually sets
            // anchoredPosition (Y-30 offset), so if cancelled mid-animation the
            // card is stuck below its correct layout position.
            var rt = transform as RectTransform;
            if (rt != null && rt.parent != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt.parent as RectTransform);
        }

        private IEnumerator EnterAnimRoutine()
        {
            // Mark in-progress to prevent duplicate starts; reset below if we exit early.
            _enterAnimPlayed = true;

            const float duration = 0.42f;
            var rt  = (RectTransform)transform;

            // IMPORTANT: Do NOT touch CanvasGroup.alpha here.
            // Alpha is managed exclusively by RefreshUnitList (sets 1) and DropAnimHost
            // (sets 0 during fly animation, then 1). Touching alpha in this coroutine
            // caused race conditions where cards got stuck invisible (alpha=0).
            // Only animate scale + position for the enter effect.
            transform.localScale = Vector3.one * 0.82f;

            // DEV-30 fix: wait one frame for LayoutGroup to calculate correct position
            // before reading anchoredPosition; otherwise the prefab default (0,0) is
            // captured and the animation fights the layout system, making cards appear
            // in wrong positions.
            yield return null;
            if (this == null || !gameObject.activeInHierarchy)
            {
                if (this != null)
                    transform.localScale = Vector3.one;
                _enterAnimPlayed = false;
                yield break;
            }
            Canvas.ForceUpdateCanvases();

            Vector2 startPos = rt.anchoredPosition + new Vector2(0f, -30f);
            Vector2 endPos   = rt.anchoredPosition;
            Vector3 startScale = Vector3.one * 0.82f;
            Vector3 endScale   = Vector3.one;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = t * (2f - t); // EaseOutQuad
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
                transform.localScale = Vector3.Lerp(startScale, endScale, ease);
                yield return null;
            }
            rt.anchoredPosition = endPos;
            transform.localScale = endScale;
            _enterAnimCoroutine = null;

            // DEV-30 V6: trigger foil sweep after card enters
            EnsureShineOverlay();
            if (_shineMat != null)
                _foilSweep = StartCoroutine(FoilSweepRoutine());
        }

        // ── DEV-30 V6: Foil Sweep ────────────────────────────────────────────

        // Safe destroy: uses DestroyImmediate in Edit Mode (tests), Destroy at runtime.
        private static void SafeDestroy(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) { DestroyImmediate(obj); return; }
#endif
            Destroy(obj);
        }

        /// <summary>Lazily creates a full-size ShineOverlay Image with CardShine shader (per-card material clone).</summary>
        private void EnsureShineOverlay()
        {
            if (_shineOverlay != null) return;
            var go = new GameObject("ShineOverlay");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _shineOverlay = go.AddComponent<Image>();
            _shineOverlay.raycastTarget = false;
            var shader = Shader.Find("UI/CardShine");
            if (shader == null) return;
            _shineMat = new Material(shader);
            _shineOverlay.material = _shineMat;
            _shineMat.SetFloat("_ShineIntensity", 0f);
        }

        /// <summary>V6: 0.8s diagonal foil sweep — animates ShineX/Y from bottom-left to top-right.</summary>
        private IEnumerator FoilSweepRoutine()
        {
            if (_shineMat == null) yield break;
            const float dur = 0.8f;
            float elapsed = 0f;
            _shineMat.SetFloat("_ShineIntensity", 0.7f);
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                _shineMat.SetFloat("_ShineX", Mathf.Lerp(-0.3f, 1.3f, t));
                _shineMat.SetFloat("_ShineY", Mathf.Lerp(1.3f, -0.3f, t));
                yield return null;
            }
            _shineMat.SetFloat("_ShineIntensity", 0f);
            _foilSweep = null;
        }

        // ── DEV-30 V7: Playable Spark ────────────────────────────────────────

        /// <summary>V7: spawns small white dots above card while it is playable (every 0.6s, 0.5s lifetime).</summary>
        private IEnumerator PlayableSparkRoutine()
        {
            var rt = (RectTransform)transform;
            while (true)
            {
                // Spawn one sparkle dot
                var dot = new GameObject("SparkDot");
                dot.transform.SetParent(transform.parent ?? transform, false);
                var drt = dot.AddComponent<RectTransform>();
                drt.sizeDelta = new Vector2(5f, 5f);
                // Must ignore layout so HorizontalLayoutGroup doesn't reflow sibling cards
                dot.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
                // Random X within card, Y just above top edge
                float halfW = rt.rect.width * 0.4f;
                drt.anchoredPosition = rt.anchoredPosition
                    + new Vector2(UnityEngine.Random.Range(-halfW, halfW), rt.rect.height * 0.5f + 4f);
                var img = dot.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = false;
                _sparkDots.Add(dot);

                // Animate: fade in/out + float up
                StartCoroutine(AnimateSparkDot(dot, img, drt));

                yield return new WaitForSeconds(0.6f);
            }
        }

        private IEnumerator AnimateSparkDot(GameObject dot, Image img, RectTransform drt)
        {
            if (dot == null) yield break;
            const float dur = 0.5f;
            Vector2 startPos = drt.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < dur && dot != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float alpha = t < 0.4f ? t / 0.4f : 1f - (t - 0.4f) / 0.6f; // up then down
                if (img != null) img.color = new Color(1f, 1f, 1f, alpha * 0.85f);
                if (drt != null) drt.anchoredPosition = startPos + new Vector2(0f, t * 18f);
                yield return null;
            }
            if (dot != null)
            {
                _sparkDots.Remove(dot);
                Destroy(dot);
            }
        }

        // ── DEV-28: Overlay image helper ─────────────────────────────────────

        /// <summary>
        /// Creates a full-size semi-transparent overlay Image child (used for target glow / hero aura).
        /// sizeDelta offsets expand the image beyond card bounds when positive.
        /// </summary>
        private Image CreateOverlayImage(string goName, Color color, Vector2 sizeDelta, bool asOutline)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            float expand = asOutline ? 4f : 0f;
            // sizeDelta.x/2 and sizeDelta.y/2 expand the overlay beyond card bounds on each side
            rt.offsetMin = new Vector2(-expand - sizeDelta.x * 0.5f, -expand - sizeDelta.y * 0.5f);
            rt.offsetMax = new Vector2( expand + sizeDelta.x * 0.5f,  expand + sizeDelta.y * 0.5f);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();
            return img;
        }

        // DEV-29: two-phase death:
        //   Phase A — quick shrink to 60% in place (0.3s, impact feel)
        //   Phase B — ghost flies to discard pile along bezier arc (0.5s)
        //   If no flyTarget, falls back to the original shrink-to-zero animation.
        private IEnumerator DeathRoutine(Vector2? flyTarget = null, Canvas canvas = null)
        {
            Vector3 startScale = transform.localScale;

            if (flyTarget.HasValue && canvas != null)
            {
                // ── Phase A: dissolve (VFX-3) or shrink+tint fallback ────────
                yield return StartCoroutine(DissolveOrFallbackRoutine(startScale));


                // ── Phase B: ghost flies to discard pile ─────────────────────
                const float phaseB = 0.50f;
                var rt = (RectTransform)transform;
                var canvasRT = canvas.GetComponent<RectTransform>();

                // Compute origin in canvas-local coords
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                Vector2 screenCenter = new Vector2(
                    (corners[0].x + corners[2].x) * 0.5f,
                    (corners[0].y + corners[2].y) * 0.5f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRT, screenCenter, canvas.worldCamera, out Vector2 origin);

                Vector2 dest = flyTarget.Value;
                // Bezier control point: midpoint raised by 60px for arc
                Vector2 ctrl = (origin + dest) * 0.5f + new Vector2(0f, 60f);

                // Create ghost — capture size before SetActive(false) so layout doesn't reset
                Vector2 capturedSize = rt.rect.size;
                var ghost = new GameObject("DeathFlyGhost");
                _deathGhost = ghost; // DEV-29: track for OnDestroy cleanup
                ghost.transform.SetParent(canvas.transform, false);
                var ghostRT = ghost.AddComponent<RectTransform>();
                ghostRT.sizeDelta = capturedSize * 0.6f; // matches Phase A end scale
                ghostRT.anchorMin = ghostRT.anchorMax = new Vector2(0.5f, 0.5f);
                ghostRT.pivot = new Vector2(0.5f, 0.5f);
                ghostRT.anchoredPosition = origin;
                ghost.transform.SetAsLastSibling();

                var srcImg = rt.GetComponent<Image>();
                if (srcImg != null)
                {
                    var ghostImg = ghost.AddComponent<Image>();
                    ghostImg.sprite = srcImg.sprite;
                    ghostImg.color  = srcImg.color;
                    ghostImg.raycastTarget = false;
                }
                var cg = ghost.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable   = false;

                // Hide the real card immediately
                gameObject.SetActive(false);

                float elapsed = 0f;
                while (elapsed < phaseB)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / phaseB);
                    float ease = t * (2f - t); // EaseOutQuad

                    // Quadratic bezier
                    float u = 1f - ease;
                    Vector2 pos = u * u * origin + 2f * u * ease * ctrl + ease * ease * dest;
                    ghostRT.anchoredPosition = pos;

                    // Scale down (use capturedSize — rt may be inactive and return zero)
                    float s = Mathf.Lerp(0.6f, 0.15f, ease);
                    ghostRT.sizeDelta = capturedSize * s;

                    // Fade out during last 40%
                    cg.alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);

                    yield return null;
                }

                _deathGhost = null;
                if (ghost != null) Destroy(ghost);
            }
            else
            {
                // ── No flyTarget: dissolve (VFX-3) or shrink-to-zero fallback ─
                yield return StartCoroutine(DissolveOrFallbackRoutine(startScale));
            }

            _death = null;
            // VFX-3: destroy card GO after animation completes
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        // ── VFX-3: Dissolve phase (or shrink+fade fallback if material unavailable) ──
        // Drives TweenMatFX noise_fade 0→1 on a cloned KillDissolveFX material assigned to
        // _cardBg; simultaneously fades all child images/texts. Falls back to the original
        // Phase-A shrink+red-tint (flyTarget path) or shrink+fade (no-flyTarget path).
        private IEnumerator DissolveOrFallbackRoutine(Vector3 startScale)
        {
            bool useDissolve = _killDissolveMat != null && _cardBg != null;

            if (useDissolve)
            {
                const float dissolveTime = 0.6f;
                var cloned = Instantiate(_killDissolveMat);
                _clonedDissolveMat = cloned;
                cloned.SetFloat("noise_fade", 0f);

                // Apply shared clone to ALL Image children so every layer dissolves in sync
                var images = GetComponentsInChildren<Image>(true);
                foreach (var img in images) img.material = cloned;

                // TweenMatFX drives noise_fade 0 → 1
                bool dissolveDone = false;
                var dissolveSeq = TweenMatFX.DissolveSequence(cloned, dissolveTime,
                    () => dissolveDone = true);

                // Texts still fade independently (Text doesn't support ShaderGraph materials)
                var texts  = GetComponentsInChildren<Text>(true);
                Color[] txtColors = new Color[texts.Length];
                for (int i = 0; i < texts.Length; i++) txtColors[i] = texts[i].color;

                float elapsed = 0f;
                const float timeout = dissolveTime + 0.25f; // safety
                while (!dissolveDone && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / dissolveTime);
                    float alpha = 1f - t;
                    for (int i = 0; i < texts.Length; i++)
                    {
                        var c = txtColors[i]; c.a = txtColors[i].a * alpha; texts[i].color = c;
                    }
                    yield return null;
                }

                // Kill dissolve tween if still alive
                if (dissolveSeq != null && dissolveSeq.IsActive()) dissolveSeq.Kill();

                // Cleanup: restore default material on all images
                foreach (var img in images)
                    if (img != null) img.material = null;
                if (_clonedDissolveMat != null) { SafeDestroy(_clonedDissolveMat); _clonedDissolveMat = null; }
            }
            else
            {
                // ── Fallback: shrink + red tint (0.3s) matching original Phase A ─
                const float duration = 0.30f;
                float elapsed = 0f;
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
                    float ease = t * (2f - t); // EaseOutQuad
                    transform.localScale = startScale * Mathf.Lerp(1f, 0.6f, ease);
                    float tint = Mathf.Sin(t * Mathf.PI) * 0.35f;
                    foreach (var img in images)
                    {
                        var c = img.color; c.r = Mathf.Min(1f, c.r + tint); img.color = c;
                    }
                    // Fade texts during fallback
                    float alpha = 1f - t;
                    for (int i = 0; i < texts.Length; i++)
                    {
                        var c = txtColors[i]; c.a = txtColors[i].a * alpha; texts[i].color = c;
                    }
                    yield return null;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // VFX-4: Battlefield visual details
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Call after a unit card is placed on the battlefield to apply visual details:
        /// micro-rotation, shadow layer, idle FX (delayed 1s).
        /// </summary>
        public void ApplyBattlefieldVisuals()
        {
            if (_unit?.CardData == null) return;

            // One-time setup (rotation + shadow) — only on first call per placement
            if (!_bfVisualsApplied)
            {
                _bfVisualsApplied = true;

                // Micro-rotation (±3° Z axis for natural feel)
                float angle = UnityEngine.Random.Range(-3f, 3f);
                transform.localRotation = Quaternion.Euler(0f, 0f, angle);

                // Shadow layer (offset Image below card)
                CreateShadow();
            }

            // These may change over time, refresh each call
            if (!_idleFXSpawned && gameObject.activeInHierarchy)
                StartCoroutine(SpawnIdleFXDelayed());

            RefreshShieldFX();
        }

        private void CreateShadow()
        {
            if (_shadowImage != null) return;
            var go = new GameObject("CardShadow");
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling(); // render below card content

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3f, -5f); // offset down-right for shadow
            rt.offsetMax = new Vector2(5f, -3f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.02f, 0.04f, 0.08f, 0.45f);
            img.raycastTarget = false;

            _shadowImage = go;

            // Fade in shadow after 0.4s delay
            if (gameObject.activeInHierarchy)
                StartCoroutine(FadeShadowIn(img));
        }

        private IEnumerator FadeShadowIn(Image shadowImg)
        {
            if (shadowImg == null) yield break;
            shadowImg.color = new Color(0f, 0f, 0f, 0f);
            yield return new WaitForSeconds(0.4f);

            const float fadeDur = 0.3f;
            float elapsed = 0f;
            while (elapsed < fadeDur)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(0f, 0.45f, elapsed / fadeDur);
                if (shadowImg != null)
                    shadowImg.color = new Color(0.02f, 0.04f, 0.08f, a);
                yield return null;
            }
        }

        private IEnumerator SpawnIdleFXDelayed()
        {
            _idleFXSpawned = true;
            yield return new WaitForSeconds(1f);

            if (_unit?.CardData == null || !gameObject.activeInHierarchy) yield break;

            string fxName = VFXResolver.GetIdleFXName(_unit.CardData.RuneType);
            if (fxName == null) yield break;

            var prefab = VFXResolver.GetPrefab(fxName);
            if (prefab == null) yield break;

            _idleFX = FXTool.DoSnapFX(prefab, transform, Vector3.zero, 0f); // 0 = no auto-destroy
        }

        /// <summary>Show/hide Shield FX based on unit keyword state.</summary>
        private void RefreshShieldFX()
        {
            if (_unit == null) return;
            bool needsShield = _unit.HasSpellShield || _unit.HasBarrier;
            if (needsShield && _shieldFX == null)
            {
                var prefab = VFXResolver.GetPrefab(VFXResolver.FX_SHIELD);
                if (prefab != null)
                    _shieldFX = FXTool.DoSnapFX(prefab, transform, Vector3.zero, 0f);
            }
            else if (!needsShield && _shieldFX != null)
            {
                SafeDestroy(_shieldFX);
                _shieldFX = null;
            }
        }

        /// <summary>Cleans up battlefield visuals when unit leaves the field.</summary>
        public void ClearBattlefieldVisuals()
        {
            if (_idleFX != null) { SafeDestroy(_idleFX); _idleFX = null; }
            if (_shieldFX != null) { SafeDestroy(_shieldFX); _shieldFX = null; }
            _idleFXSpawned = false;
            _bfVisualsApplied = false;
            if (_shadowImage != null) { SafeDestroy(_shadowImage); _shadowImage = null; }
            transform.localRotation = Quaternion.identity;
        }
    }
}
