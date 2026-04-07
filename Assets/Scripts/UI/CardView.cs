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
        private Tween _stunPulse;
        private Tween _shake;
        private Tween _flash;
        private Sequence _deathSeq;
        private bool _deathStarted;
        private GameObject _deathGhost; // DEV-29: tracked so OnDestroy can clean up mid-flight ghost
        private CardGlow _cardGlow;

        // ── Stat glow: breathing background images when values are modified ──
        private Image     _atkGlowImg;
        private Image     _costGlowImg;
        private Image     _schGlowImg;
        private Tween _atkBreath;
        private Tween _costBreath;
        private Tween _schBreath;

        // ── Status badges (▲ buff / ▲ equip / ▼ debuff) — DEV-25 ────────────
        private GameObject _buffBadge;
        private GameObject _equipBadge;
        private GameObject _debuffBadge;
        private GameObject _statusTooltip;   // one-at-a-time tooltip panel
        private BadgeTip? _currentStatusTip; // tracks which badge opened the tooltip

        // Scale tweens per badge (Dictionary avoids ref-in-lambda issues)
        private readonly System.Collections.Generic.Dictionary<GameObject, Tween>
            _badgeScaleTweens = new System.Collections.Generic.Dictionary<GameObject, Tween>();

        private enum BadgeTip { Buff, Equip, Debuff }

        // ── Selection lift + float animation ────────────────────────────────
        private bool      _isLifted;
        private Tween     _liftFloat;
        private Tween     _returnToRest;            // smooth de-select return animation
        private float     _restAnchoredY;           // Y before lifting
        private const float LiftOffset       = 12f;   // px raised when selected
        private const float FloatAmplitude   = 4f;    // px peak of float wave
        private const float FloatPeriod      = 1.5f;  // seconds per full cycle
        private const float ReturnDuration   = 0.30f; // seconds for animated return

        // ── DEV-28: Target highlight ─────────────────────────────────────────
        private Image     _targetBorder;
        private Tween     _targetPulse;
        private Tween     _targetFadeOut;

        // ── DEV-28: Selected orbit light ─────────────────────────────────────
        private GameObject _orbitDot;
        private Tween      _orbitTween;

        // ── DEV-28: Hero aura ────────────────────────────────────────────────
        private Image     _heroAura;
        private Tween     _heroAuraPulse;

        // ── DEV-28: Hand enter animation ─────────────────────────────────────
        private bool _enterAnimPlayed;
        private Sequence _enterAnimSeq;
        private Coroutine _enterAnimSetup; // one-frame setup coroutine before DOTween anim

        // ── DEV-29: Card back overlay (geometric pattern) ────────────────────
        private GameObject _cardBackOverlay;
        private Image      _cardBackSpriteImg; // VFX-7g: sprite-based card back

        // ── DEV-30: Foil Sweep (V6) ──────────────────────────────────────────
        private Image     _shineOverlay;  // lazy-created full-size overlay with CardShine shader
        private Material  _shineMat;      // per-card clone destroyed in OnDestroy
        private Tween     _foilSweep;

        // ── DEV-30: Playable Spark (V7) ──────────────────────────────────────
        private Coroutine _playableSpark; // kept: periodic spawning loop (WaitForSeconds)
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

        // ── DOT-8: Hand spread (push neighbors via LayoutElement width) ───────
        private Tween _handSpreadTween;
        private UnityEngine.UI.LayoutElement _spreadLayoutEl;
        private float _naturalCardWidth = -1f;
        private const float HAND_SPREAD_EXTRA = 32f;  // extra px given to hovered card
        private const float HAND_SPREAD_DUR   = 0.12f;

        // ── DOT-8: 3D perspective tilt ────────────────────────────────────────
        private bool      _isTiltActive;
        private Vector3   _tiltTarget;
        private Vector3   _tiltCurrent;
        private Quaternion _preTiltRotation;
        private const float TILT_MAX   = 10f; // max degrees each axis
        private const float TILT_SPEED = 9f;  // lerp speed

        // ── DOT-8: Stat roll counter ──────────────────────────────────────────
        private Tween _statRollTween;
        private int   _displayedHp  = int.MinValue;
        private int   _displayedAtk = int.MinValue;
        private const float STAT_ROLL_DUR = 0.45f;

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
            // DOT-7: kill all tweens targeting this GameObject (catches any SetTarget(gameObject))
            DOTween.Kill(gameObject);

            if (_clickButton != null)
                _clickButton.onClick.RemoveListener(HandleClick);

            // DOT-7: KillSafe each tracked tween field
            TweenHelper.KillSafe(ref _stunPulse);
            TweenHelper.KillSafe(ref _shake);
            TweenHelper.KillSafe(ref _flash);
            TweenHelper.KillSafe(ref _atkBreath);
            TweenHelper.KillSafe(ref _costBreath);
            TweenHelper.KillSafe(ref _schBreath);
            TweenHelper.KillSafe(ref _liftFloat);
            TweenHelper.KillSafe(ref _returnToRest);
            TweenHelper.KillSafe(ref _targetPulse);
            TweenHelper.KillSafe(ref _targetFadeOut);
            TweenHelper.KillSafe(ref _orbitTween);
            TweenHelper.KillSafe(ref _heroAuraPulse);
            TweenHelper.KillSafe(ref _foilSweep);
            TweenHelper.KillSafe(ref _handSpreadTween);
            TweenHelper.KillSafe(ref _statRollTween);
            if (_enterAnimSeq != null && _enterAnimSeq.IsActive()) _enterAnimSeq.Kill();
            _enterAnimSeq = null;
            if (_deathSeq != null && _deathSeq.IsActive()) _deathSeq.Kill();
            _deathSeq = null;
            if (_enterAnimSetup != null) { StopCoroutine(_enterAnimSetup); _enterAnimSetup = null; }

            // Badge scale tweens
            foreach (var tw in _badgeScaleTweens.Values)
            {
                Tween t = tw;
                TweenHelper.KillSafe(ref t);
            }
            _badgeScaleTweens.Clear();

            // DEV-29: cleanup death ghost
            if (_deathGhost  != null) { Destroy(_deathGhost); _deathGhost = null; }
            if (_orbitDot != null) Destroy(_orbitDot);
            // H-3: destroy floating tooltip to prevent canvas leak when card is removed
            if (_statusTooltip != null) { Destroy(_statusTooltip); _statusTooltip = null; _currentStatusTip = null; }
            // VFX-3: clear dissolve material on all images before destroying clone
            if (_clonedDissolveMat != null)
            {
                foreach (var img in GetComponentsInChildren<Image>(true))
                    if (img != null) img.material = null;
                SafeDestroy(_clonedDissolveMat);
                _clonedDissolveMat = null;
            }
            // DEV-30 V6: destroy cloned shine material
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

        // ── VFX-7k + DOT-8: Update — glow fade + 3D tilt ────────────────────
        private void Update()
        {
            // VFX-7k: glow overlay smooth fade
            if (_glowOverlay != null && !Mathf.Approximately(_glowCurrentAlpha, _glowTargetAlpha))
            {
                _glowCurrentAlpha = Mathf.MoveTowards(_glowCurrentAlpha, _glowTargetAlpha,
                    GLOW_FADE_SPEED * Time.deltaTime);
                var gc = _glowTargetColor;
                gc.a = _glowCurrentAlpha;
                _glowOverlay.color = gc;
                _glowOverlay.enabled = _glowCurrentAlpha > 0.01f;
            }

            // DOT-8: 3D perspective tilt (continuous mouse tracking — excluded from DOTween per rules)
            if (_isTiltActive)
            {
                var rt = (RectTransform)transform;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                float w = corners[2].x - corners[0].x;
                float h = corners[2].y - corners[0].y;
                if (w > 1f && h > 1f)
                {
                    Vector2 center = new Vector2(
                        (corners[0].x + corners[2].x) * 0.5f,
                        (corners[0].y + corners[2].y) * 0.5f);
                    Vector2 delta  = (Vector2)Input.mousePosition - center;
                    float normX = Mathf.Clamp(delta.x / (w * 0.5f), -1f, 1f);
                    float normY = Mathf.Clamp(delta.y / (h * 0.5f), -1f, 1f);
                    _tiltTarget = new Vector3(-normY * TILT_MAX, normX * TILT_MAX, 0f);
                }
            }
            else
            {
                _tiltTarget = Vector3.zero;
            }

            if (_isTiltActive || _tiltCurrent.sqrMagnitude > 0.001f)
            {
                _tiltCurrent = Vector3.Lerp(_tiltCurrent, _tiltTarget, TILT_SPEED * Time.deltaTime);
                transform.localRotation = _preTiltRotation * Quaternion.Euler(_tiltCurrent);
                if (!_isTiltActive && _tiltCurrent.sqrMagnitude < 0.001f)
                    transform.localRotation = _preTiltRotation; // snap when fully returned
            }
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
                _enterAnimSetup = StartCoroutine(EnterAnimSetup());
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (_unit != null && !_faceDown)
            {
                ShowGlow(); // VFX-7k
                StartHandSpread();           // DOT-8: push neighbors apart
                _preTiltRotation = transform.localRotation;
                _isTiltActive    = true;     // DOT-8: enable 3D tilt
                _tiltCurrent     = Vector3.zero;
                _onHoverEnter?.Invoke(_unit);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (CardDragHandler.BlockPointerEvents) return;
            if (_unit != null && !_faceDown)
            {
                if (!_selected) HideGlow(); // VFX-7k: keep glow if selected
                StopHandSpread();           // DOT-8: restore spread
                _isTiltActive = false;      // DOT-8: tilt lerps back to zero in Update
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
                    int newHp  = _unit.CurrentHp;

                    // DOT-8: roll numbers from old value to new value when stats change
                    bool hpChanged  = newHp  != _displayedHp;
                    bool atkChanged = effAtk != _displayedAtk;
                    if ((hpChanged || atkChanged) && _displayedHp != int.MinValue
                        && gameObject.activeInHierarchy)
                    {
                        int fromHp  = _displayedHp;
                        int fromAtk = _displayedAtk;
                        _displayedHp  = newHp;
                        _displayedAtk = effAtk;
                        TweenHelper.KillSafe(ref _statRollTween);
                        _statRollTween = DOVirtual.Float(0f, 1f, STAT_ROLL_DUR, t =>
                        {
                            if (_atkText == null) return;
                            int dHp  = Mathf.RoundToInt(Mathf.Lerp(fromHp,  newHp,  t));
                            int dAtk = Mathf.RoundToInt(Mathf.Lerp(fromAtk, effAtk, t));
                            _atkText.text = dHp != dAtk ? $"{dHp}/{dAtk}" : $"{dAtk}";
                        }).SetEase(Ease.OutCubic).SetTarget(gameObject);
                    }
                    else
                    {
                        _displayedHp  = newHp;
                        _displayedAtk = effAtk;
                        _atkText.text = newHp != effAtk ? $"{newHp}/{effAtk}" : $"{effAtk}";
                    }

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
                if (stunned && (_stunPulse == null || !_stunPulse.IsActive()))
                    _stunPulse = CreateStunPulseTween();
                else if (!stunned)
                    TweenHelper.KillSafe(ref _stunPulse);
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
                bool wasReturning = _returnToRest != null && _returnToRest.IsActive();
                TweenHelper.KillSafe(ref _returnToRest);
                _isLifted = true;
                if (!wasReturning)
                    _restAnchoredY = rt.anchoredPosition.y;  // only save rest Y when truly at rest
                TweenHelper.KillSafe(ref _liftFloat);
                StartLiftFloat();
                // DEV-28: start orbit light
                StartOrbit();
                ShowGlow(); // VFX-7k
            }
            else
            {
                _isLifted = false;
                TweenHelper.KillSafe(ref _liftFloat);
                // Animate back to rest position instead of snapping
                TweenHelper.KillSafe(ref _returnToRest);
                StartReturnToRest(rt.anchoredPosition.y);
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
            TweenHelper.KillSafe(ref _liftFloat);
        }

        /// <summary>
        /// Resumes lift/float animation after cluster drag ends (if card is still selected).
        /// </summary>
        public void ResumeLift()
        {
            if (_selected && _isLifted && (_liftFloat == null || !_liftFloat.IsActive()))
                StartLiftFloat();
        }

        // ── DOT-8: Hand spread helpers ────────────────────────────────────────

        /// <summary>DOT-8: Expand preferred width → HLG pushes neighboring cards apart.</summary>
        private void StartHandSpread()
        {
            // Only spread hand cards (inside a HorizontalLayoutGroup)
            if (transform.parent == null) return;
            if (transform.parent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>() == null) return;

            if (_spreadLayoutEl == null)
                _spreadLayoutEl = GetComponent<UnityEngine.UI.LayoutElement>()
                               ?? gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

            if (_naturalCardWidth < 0f)
                _naturalCardWidth = ((RectTransform)transform).rect.width;

            float from = _spreadLayoutEl.preferredWidth < 0f
                ? _naturalCardWidth
                : _spreadLayoutEl.preferredWidth;
            float to   = _naturalCardWidth + HAND_SPREAD_EXTRA;

            TweenHelper.KillSafe(ref _handSpreadTween);
            _handSpreadTween = DOVirtual.Float(from, to, HAND_SPREAD_DUR, v =>
            {
                if (_spreadLayoutEl != null) _spreadLayoutEl.preferredWidth = v;
            }).SetEase(Ease.OutQuad).SetTarget(gameObject);
        }

        /// <summary>DOT-8: Restore preferred width to natural size.</summary>
        private void StopHandSpread()
        {
            if (_spreadLayoutEl == null) return;
            float from = _spreadLayoutEl.preferredWidth < 0f
                ? (_naturalCardWidth >= 0f ? _naturalCardWidth : 0f)
                : _spreadLayoutEl.preferredWidth;
            float to = _naturalCardWidth >= 0f ? _naturalCardWidth : from;

            TweenHelper.KillSafe(ref _handSpreadTween);
            _handSpreadTween = DOVirtual.Float(from, to, HAND_SPREAD_DUR, v =>
            {
                if (_spreadLayoutEl != null) _spreadLayoutEl.preferredWidth = v;
            }).SetEase(Ease.OutQuad).SetTarget(gameObject)
              .OnComplete(() => { if (_spreadLayoutEl != null) _spreadLayoutEl.preferredWidth = -1f; });
        }

        /// <summary>DOT-7: DOVirtual.Float sine loop driving anchoredPosition.y.</summary>
        private void StartLiftFloat()
        {
            var rt = (RectTransform)transform;
            _liftFloat = DOVirtual.Float(0f, FloatPeriod, FloatPeriod, v =>
            {
                if (rt == null) return;
                float floatY = Mathf.Sin(v * Mathf.PI * 2f / FloatPeriod) * FloatAmplitude;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                                  _restAnchoredY + LiftOffset + floatY);
            }).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetTarget(gameObject);
        }

        /// <summary>DOT-7: DOAnchorPosY smooth return to rest with OutQuad ease.</summary>
        private void StartReturnToRest(float startY)
        {
            var rt = (RectTransform)transform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, startY);
            _returnToRest = DOVirtual.Float(startY, _restAnchoredY, ReturnDuration, v =>
            {
                if (rt != null)
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, v);
            }).SetEase(Ease.OutQuad).SetTarget(gameObject)
              .OnComplete(() => _returnToRest = null);
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
        private void RefreshStatGlow(ref Tween tw, ref Image glowImg,
                                     RectTransform target, bool modified)
        {
            if (modified)
            {
                if (glowImg == null) glowImg = CreateGlowImgBehind(target);
                if (tw == null || !tw.IsActive())
                {
                    var captured = glowImg;
                    tw = CreateBreathGlowTween(captured);
                }
            }
            else
            {
                StopStatGlow(ref tw, glowImg);
            }
        }

        private void StopStatGlow(ref Tween tw, Image glowImg)
        {
            TweenHelper.KillSafe(ref tw);
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

        // DOT-7: sine-driven alpha breath on stat glow image
        public const float BREATH_GLOW_MIN = 0.08f;
        public const float BREATH_GLOW_MAX = 0.45f;
        public const float BREATH_GLOW_SPEED = 1.4f;

        private Tween CreateBreathGlowTween(Image img)
        {
            if (img == null) return null;
            // Full sine cycle = 2π / BREATH_GLOW_SPEED ≈ 4.49s
            float period = Mathf.PI * 2f / BREATH_GLOW_SPEED;
            return DOVirtual.Float(0f, Mathf.PI * 2f, period, v =>
            {
                if (img == null) return;
                float alpha = Mathf.Lerp(BREATH_GLOW_MIN, BREATH_GLOW_MAX, (Mathf.Sin(v) + 1f) * 0.5f);
                img.color = new Color(1f, 1f, 1f, alpha);
            }).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetTarget(gameObject);
        }

        private void RefreshAtkGlow(bool modified)
        {
            if (_atkText == null) return;
            _atkText.color = Color.white;
            RefreshStatGlow(ref _atkBreath, ref _atkGlowImg,
                (RectTransform)_atkText.transform, modified);
        }

        // DOT-7: stun overlay alpha pulse (2 Hz sine loop)
        public const float STUN_PULSE_SPEED = 2f;
        public const float STUN_PULSE_MIN = 0.15f;
        public const float STUN_PULSE_MAX = 0.45f;

        private Tween CreateStunPulseTween()
        {
            if (_stunnedOverlay == null) return null;
            float period = Mathf.PI * 2f / STUN_PULSE_SPEED;
            return DOVirtual.Float(0f, Mathf.PI * 2f, period, v =>
            {
                if (_stunnedOverlay == null) return;
                float alpha = Mathf.Lerp(STUN_PULSE_MIN, STUN_PULSE_MAX, (Mathf.Sin(v) + 1f) * 0.5f);
                var c = GameColors.StunnedOverlay;
                c.a = alpha;
                _stunnedOverlay.color = c;
            }).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetTarget(gameObject);
        }

        // DOT-7: flash red constants
        public const float FLASH_RED_HOLD = 0.12f;
        public const float FLASH_RED_FADE = 0.35f;

        /// <summary>Brief red flash: instant red, fade back over 0.35s.</summary>
        public void FlashRed()
        {
            TweenHelper.KillSafe(ref _flash);
            if (_cardBg == null) return;
            Color original = _cardBg.color;
            Color red = new Color(1f, 0.15f, 0.15f, original.a);
            _cardBg.color = red;
            _flash = _cardBg.DOColor(original, FLASH_RED_FADE)
                .SetDelay(FLASH_RED_HOLD)
                .SetEase(Ease.Linear)
                .SetTarget(gameObject)
                .OnComplete(() => _flash = null);
        }

        // DOT-7: shake constants
        public const float SHAKE_STRENGTH = 10f;
        public const float SHAKE_DURATION = 0.08f;
        public const int   SHAKE_VIBRATO = 12;

        /// <summary>Left-right shake: DOShakeAnchorPos ~0.28s.</summary>
        public void Shake()
        {
            TweenHelper.KillSafe(ref _shake);
            _shake = TweenHelper.ShakeUI((RectTransform)transform, SHAKE_STRENGTH, SHAKE_DURATION, SHAKE_VIBRATO);
            if (_shake != null)
                _shake.OnComplete(() => _shake = null);
        }

        // DOT-7: death animation constants
        public const float DEATH_PHASE_A_DISSOLVE = 0.6f;
        public const float DEATH_PHASE_A_FALLBACK = 0.30f;
        public const float DEATH_PHASE_B = 0.50f;
        public const float DEATH_GHOST_START_SCALE = 0.6f;
        public const float DEATH_GHOST_END_SCALE = 0.15f;

        /// <summary>
        /// Shrink + fade death animation over 0.45s. Called just before the unit is
        /// removed from game state. RefreshUI will destroy this GameObject shortly after.
        /// DEV-17.
        /// </summary>
        public void PlayDeathAnimation(Vector2? flyTarget = null, Canvas canvas = null)
        {
            if (_deathStarted) return;
            _deathStarted = true;
            StartCoroutine(DeathRoutine(flyTarget, canvas));
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
            if (_badgeScaleTweens.TryGetValue(badge, out var old) && old != null && old.IsActive())
                old.Kill();
            _badgeScaleTweens[badge] = badge.transform.DOScale(Vector3.one * target, duration)
                .SetEase(Ease.Linear).SetTarget(gameObject);
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

        // DOT-7: target pulse constants
        public const float TARGET_PULSE_PERIOD = 1.2f;
        public const float TARGET_PULSE_MIN = 0.3f;
        public const float TARGET_PULSE_MAX = 0.85f;

        public void SetTargeted(bool targeted)
        {
            _targetAlphaGoal = targeted ? 1f : 0f;
            if (targeted)
            {
                if (_targetBorder == null)
                    _targetBorder = CreateOverlayImage("TargetBorder",
                        new Color(0.29f, 0.87f, 0.50f, 0f), sizeDelta: Vector2.zero, asOutline: true);
                TweenHelper.KillSafe(ref _targetPulse);
                TweenHelper.KillSafe(ref _targetFadeOut);
                StartTargetPulse();
            }
            else
            {
                TweenHelper.KillSafe(ref _targetPulse);
                TweenHelper.KillSafe(ref _targetFadeOut);
                StartTargetFadeOut();
            }
        }

        private void StartTargetFadeOut()
        {
            if (_targetBorder == null) return;
            float fadeTime = Mathf.Max(0.05f, _targetAlpha / TARGET_FADE_SPEED);
            _targetFadeOut = DOVirtual.Float(_targetAlpha, 0f, fadeTime, v =>
            {
                _targetAlpha = v;
                if (_targetBorder != null)
                {
                    var c = _targetBorder.color;
                    _targetBorder.color = new Color(c.r, c.g, c.b, v);
                }
            }).SetEase(Ease.Linear).SetTarget(gameObject)
              .OnComplete(() => { _targetAlpha = 0f; _targetFadeOut = null; });
        }

        private void StartTargetPulse()
        {
            if (_targetBorder == null) return;
            var baseCol = new Color(0.29f, 0.87f, 0.50f, 1f);
            // Two-phase: fade-in envelope + steady pulse (combined in one tween)
            _targetPulse = DOVirtual.Float(0f, TARGET_PULSE_PERIOD, TARGET_PULSE_PERIOD, v =>
            {
                if (_targetBorder == null) return;
                // Envelope: ramp _targetAlpha to 1 over ~0.5s then hold
                if (_targetAlpha < 1f)
                    _targetAlpha = Mathf.MoveTowards(_targetAlpha, 1f, TARGET_FADE_SPEED * Time.deltaTime);
                float pulseA = (Mathf.Sin(v * Mathf.PI * 2f / TARGET_PULSE_PERIOD) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(TARGET_PULSE_MIN, TARGET_PULSE_MAX, pulseA) * _targetAlpha;
                _targetBorder.color = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
            }).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetTarget(gameObject);
        }

        // ── DEV-28: Selected orbit light ─────────────────────────────────────

        // DOT-7: orbit constants
        public const float ORBIT_RADIUS = 60f;
        public const float ORBIT_PERIOD = 6f;

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
            TweenHelper.KillSafe(ref _orbitTween);
            var dotRT = _orbitDot.GetComponent<RectTransform>();
            _orbitTween = DOVirtual.Float(0f, 360f, ORBIT_PERIOD, v =>
            {
                if (dotRT == null) return;
                float rad = v * Mathf.Deg2Rad;
                dotRT.anchoredPosition = new Vector2(Mathf.Cos(rad) * ORBIT_RADIUS, Mathf.Sin(rad) * ORBIT_RADIUS);
            }).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetTarget(gameObject);
        }

        private void StopOrbit()
        {
            TweenHelper.KillSafe(ref _orbitTween);
            if (_orbitDot != null) _orbitDot.SetActive(false);
        }

        // ── DEV-28: Hero aura ────────────────────────────────────────────────

        // DOT-7: hero aura pulse constants
        public const float HERO_AURA_PERIOD = 4f;
        public const float HERO_AURA_MIN = 0.25f;
        public const float HERO_AURA_MAX = 0.60f;

        // DEV-29: stop and destroy hero aura when CardView is reused for a non-hero unit
        private void ClearHeroAura()
        {
            TweenHelper.KillSafe(ref _heroAuraPulse);
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
            TweenHelper.KillSafe(ref _heroAuraPulse);
            _heroAuraPulse = DOVirtual.Float(0f, Mathf.PI * 2f, HERO_AURA_PERIOD, v =>
            {
                if (_heroAura == null) return;
                float alpha = Mathf.Lerp(HERO_AURA_MIN, HERO_AURA_MAX, (Mathf.Sin(v) + 1f) * 0.5f);
                _heroAura.color = new Color(1f, 0.85f, 0.2f, alpha);
            }).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetTarget(gameObject);
        }

        // ── DEV-28: Hand enter animation ─────────────────────────────────────

        // DOT-7: enter animation constants
        public const float ENTER_ANIM_DURATION = 0.42f;
        public const float ENTER_ANIM_START_SCALE = 0.82f;
        public const float ENTER_ANIM_Y_OFFSET = -30f;

        /// <summary>
        /// Stops the enter animation if running and restores alpha/scale to final values.
        /// Called by DropAnimHost before it takes over alpha management to prevent races.
        /// </summary>
        public void CancelEnterAnim()
        {
            if (_enterAnimSetup != null) { StopCoroutine(_enterAnimSetup); _enterAnimSetup = null; }
            if (_enterAnimSeq != null && _enterAnimSeq.IsActive()) _enterAnimSeq.Kill();
            _enterAnimSeq = null;
            // Restore scale (EnterAnimRoutine no longer touches alpha)
            transform.localScale = Vector3.one;
            _enterAnimPlayed = true; // prevent re-start

            // Force parent HLG to recalculate — the animation manually sets
            // anchoredPosition (Y-30 offset), so if cancelled mid-animation the
            // card is stuck below its correct layout position.
            var rt = transform as RectTransform;
            if (rt != null && rt.parent != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt.parent as RectTransform);
        }

        /// <summary>DOT-7: one-frame setup coroutine, then DOTween Sequence for the animation.</summary>
        private IEnumerator EnterAnimSetup()
        {
            // Mark in-progress to prevent duplicate starts; reset below if we exit early.
            _enterAnimPlayed = true;

            var rt  = (RectTransform)transform;

            // IMPORTANT: Do NOT touch CanvasGroup.alpha here.
            transform.localScale = Vector3.one * ENTER_ANIM_START_SCALE;

            // DEV-30 fix: wait one frame for LayoutGroup to calculate correct position
            yield return null;
            if (this == null || !gameObject.activeInHierarchy)
            {
                if (this != null)
                    transform.localScale = Vector3.one;
                _enterAnimPlayed = false;
                yield break;
            }
            Canvas.ForceUpdateCanvases();

            Vector2 startPos = rt.anchoredPosition + new Vector2(0f, ENTER_ANIM_Y_OFFSET);
            Vector2 endPos   = rt.anchoredPosition;

            rt.anchoredPosition = startPos;
            rt.localEulerAngles = new Vector3(0f, 0f, -4f); // slight tilt for drop-in feel
            _enterAnimSeq = DOTween.Sequence()
                .Append(rt.DOAnchorPos(endPos, ENTER_ANIM_DURATION).SetEase(Ease.OutCubic))
                .Join(transform.DOScale(Vector3.one, ENTER_ANIM_DURATION).SetEase(Ease.OutCubic))
                .Join(rt.DOLocalRotate(Vector3.zero, ENTER_ANIM_DURATION).SetEase(Ease.OutQuad))
                .SetTarget(gameObject)
                .OnComplete(() =>
                {
                    _enterAnimSeq = null;
                    _enterAnimSetup = null;
                    // DEV-30 V6: trigger foil sweep after card enters
                    EnsureShineOverlay();
                    if (_shineMat != null)
                        StartFoilSweep();
                });
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

        // DOT-7: foil sweep constants
        public const float FOIL_SWEEP_DURATION = 0.8f;

        /// <summary>V6: 0.8s diagonal foil sweep — animates ShineX/Y from bottom-left to top-right.</summary>
        private void StartFoilSweep()
        {
            if (_shineMat == null) return;
            TweenHelper.KillSafe(ref _foilSweep);
            _shineMat.SetFloat("_ShineIntensity", 0.7f);
            _foilSweep = DOVirtual.Float(0f, 1f, FOIL_SWEEP_DURATION, t =>
            {
                if (_shineMat == null) return;
                _shineMat.SetFloat("_ShineX", Mathf.Lerp(-0.3f, 1.3f, t));
                _shineMat.SetFloat("_ShineY", Mathf.Lerp(1.3f, -0.3f, t));
            }).SetEase(Ease.Linear).SetTarget(gameObject)
              .OnComplete(() =>
              {
                  if (_shineMat != null) _shineMat.SetFloat("_ShineIntensity", 0f);
                  _foilSweep = null;
              });
        }

        // ── DEV-30 V7: Playable Spark ────────────────────────────────────────

        // DOT-7: spark constants
        public const float SPARK_INTERVAL = 0.6f;
        public const float SPARK_DURATION = 0.5f;
        public const float SPARK_FLOAT_DIST = 18f;
        public const float SPARK_PEAK_ALPHA = 0.85f;

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

                // DOT-7: animate with DOTween Sequence
                AnimateSparkDot(dot, img, drt);

                yield return new WaitForSeconds(SPARK_INTERVAL);
            }
        }

        /// <summary>DOT-7: DOVirtual.Float drives position + alpha, OnComplete destroys dot.</summary>
        private void AnimateSparkDot(GameObject dot, Image img, RectTransform drt)
        {
            if (dot == null) return;
            Vector2 startPos = drt.anchoredPosition;
            DOVirtual.Float(0f, 1f, SPARK_DURATION, t =>
            {
                if (drt == null) return;
                float alpha = t < 0.4f ? t / 0.4f : 1f - (t - 0.4f) / 0.6f;
                if (img != null) img.color = new Color(1f, 1f, 1f, alpha * SPARK_PEAK_ALPHA);
                drt.anchoredPosition = startPos + new Vector2(0f, t * SPARK_FLOAT_DIST);
            }).SetEase(Ease.Linear).SetTarget(dot)
              .OnComplete(() =>
              {
                  if (dot != null) { _sparkDots.Remove(dot); Destroy(dot); }
              });
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
        //   Phase A — dissolve or shrink+tint fallback
        //   Phase B — ghost flies to discard pile along bezier arc
        //   If no flyTarget, falls back to the original shrink-to-zero animation.
        private IEnumerator DeathRoutine(Vector2? flyTarget = null, Canvas canvas = null)
        {
            Vector3 startScale = transform.localScale;

            // DOT-8: kill closeup — scale up 1.25x + red flash before dissolve
            bool punchDone = false;
            DOTween.Sequence()
                .Append(transform.DOScale(startScale * 1.25f, 0.10f).SetEase(Ease.OutBack))
                .AppendCallback(FlashRed)
                .AppendInterval(0.18f)
                .SetTarget(gameObject)
                .OnComplete(() => punchDone = true);
            while (!punchDone) yield return null;

            // Phase A: dissolve or fallback (still coroutine due to TweenMatFX await)
            yield return StartCoroutine(DissolveOrFallbackRoutine(transform.localScale));

            if (flyTarget.HasValue && canvas != null)
            {
                // ── Phase B: ghost flies to discard pile (DOTween) ───────────
                var rt = (RectTransform)transform;
                var canvasRT = canvas.GetComponent<RectTransform>();

                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                Vector2 screenCenter = new Vector2(
                    (corners[0].x + corners[2].x) * 0.5f,
                    (corners[0].y + corners[2].y) * 0.5f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRT, screenCenter, canvas.worldCamera, out Vector2 origin);

                Vector2 dest = flyTarget.Value;
                Vector2 ctrl = (origin + dest) * 0.5f + new Vector2(0f, 60f);

                Vector2 capturedSize = rt.rect.size;
                var ghost = new GameObject("DeathFlyGhost");
                _deathGhost = ghost;
                ghost.transform.SetParent(canvas.transform, false);
                var ghostRT = ghost.AddComponent<RectTransform>();
                ghostRT.sizeDelta = capturedSize * DEATH_GHOST_START_SCALE;
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

                gameObject.SetActive(false);

                // DOT-7 + enhance: punch on death start, then bezier fly
                bool phaseBDone = false;
                _deathSeq = DOTween.Sequence()
                    .Append(ghostRT.DOPunchScale(Vector3.one * 0.18f, 0.12f, 2, 0f))
                    .Append(DOVirtual.Float(0f, 1f, DEATH_PHASE_B, t =>
                    {
                        if (ghostRT == null) return;
                        float ease = t * (2f - t); // OutQuad
                        float u = 1f - ease;
                        Vector2 pos = u * u * origin + 2f * u * ease * ctrl + ease * ease * dest;
                        ghostRT.anchoredPosition = pos;
                        float s = Mathf.Lerp(DEATH_GHOST_START_SCALE, DEATH_GHOST_END_SCALE, ease);
                        ghostRT.sizeDelta = capturedSize * s;
                        cg.alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
                    }).SetEase(Ease.Linear))
                    .SetTarget(ghost)
                    .OnComplete(() => phaseBDone = true);

                while (!phaseBDone) yield return null;

                _deathGhost = null;
                if (ghost != null) Destroy(ghost);
            }

            _deathSeq = null;
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
                var cloned = Instantiate(_killDissolveMat);
                _clonedDissolveMat = cloned;
                cloned.SetFloat("noise_fade", 0f);

                var images = GetComponentsInChildren<Image>(true);
                foreach (var img in images) img.material = cloned;

                // TweenMatFX drives noise_fade 0 → 1
                bool dissolveDone = false;
                var dissolveSeq = TweenMatFX.DissolveSequence(cloned, DEATH_PHASE_A_DISSOLVE,
                    () => dissolveDone = true);

                // DOT-7: text fade via DOVirtual.Float parallel to dissolve
                var texts  = GetComponentsInChildren<Text>(true);
                Color[] txtColors = new Color[texts.Length];
                for (int i = 0; i < texts.Length; i++) txtColors[i] = texts[i].color;

                var textFade = DOVirtual.Float(1f, 0f, DEATH_PHASE_A_DISSOLVE, alpha =>
                {
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (texts[i] == null) continue;
                        var c = txtColors[i]; c.a = txtColors[i].a * alpha; texts[i].color = c;
                    }
                }).SetEase(Ease.Linear).SetTarget(gameObject);

                float timeout = DEATH_PHASE_A_DISSOLVE + 0.25f;
                float elapsed = 0f;
                while (!dissolveDone && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (dissolveSeq != null && dissolveSeq.IsActive()) dissolveSeq.Kill();
                if (textFade != null && textFade.IsActive()) textFade.Kill();

                foreach (var img in images)
                    if (img != null) img.material = null;
                if (_clonedDissolveMat != null) { SafeDestroy(_clonedDissolveMat); _clonedDissolveMat = null; }
            }
            else
            {
                // ── Fallback: shrink + red tint (0.3s) via DOVirtual.Float ─
                var images = GetComponentsInChildren<Image>(true);
                var texts  = GetComponentsInChildren<Text>(true);
                Color[] imgColors = new Color[images.Length];
                Color[] txtColors = new Color[texts.Length];
                for (int i = 0; i < images.Length; i++) imgColors[i] = images[i].color;
                for (int i = 0; i < texts.Length;  i++) txtColors[i] = texts[i].color;

                bool fallbackDone = false;
                DOVirtual.Float(0f, 1f, DEATH_PHASE_A_FALLBACK, t =>
                {
                    if (this == null) return;
                    float ease = t * (2f - t); // OutQuad
                    transform.localScale = startScale * Mathf.Lerp(1f, DEATH_GHOST_START_SCALE, ease);
                    float tint = Mathf.Sin(t * Mathf.PI) * 0.35f;
                    foreach (var img in images)
                    {
                        if (img == null) continue;
                        var c = img.color; c.r = Mathf.Min(1f, c.r + tint); img.color = c;
                    }
                    float alpha = 1f - t;
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (texts[i] == null) continue;
                        var c = txtColors[i]; c.a = txtColors[i].a * alpha; texts[i].color = c;
                    }
                }).SetEase(Ease.Linear).SetTarget(gameObject)
                  .OnComplete(() => fallbackDone = true);

                while (!fallbackDone) yield return null;
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

            // DOT-7: fade in shadow after 0.4s delay via DOTween
            if (gameObject.activeInHierarchy)
            {
                img.color = new Color(0f, 0f, 0f, 0f);
                img.DOColor(new Color(0.02f, 0.04f, 0.08f, 0.45f), 0.3f)
                   .SetDelay(0.4f).SetEase(Ease.Linear).SetTarget(gameObject);
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
