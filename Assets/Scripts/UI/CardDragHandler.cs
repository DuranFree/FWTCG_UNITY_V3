using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FWTCG;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-22: Drag-to-play card handler.
    ///
    /// Attach to the same GameObject as CardView.
    /// Supports:
    ///   - Hand unit card: drag to base zone → play from hand
    ///   - Hand spell card: drag out of hand zone → trigger spell (shows target popup)
    ///   - Base unit: drag to BF zone → move to battlefield
    ///   - Multi-select: ghost copies of other selected cards follow the dragged card.
    ///
    /// Cancel behaviour (right-click OR release on source zone):
    ///   - Ghosts animate back to origin with ease-in/ease-out
    ///   - Selection highlight is preserved (no deselect on cancel)
    ///
    /// Static zone RTs are set once by GameUI on startup.
    /// Callbacks route back to GameManager via GameUI.
    /// </summary>
    public class CardDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        /// <summary>
        /// True while a drag or cancel-return animation is active.
        /// CardViews check this to suppress hover/click/right-click callbacks,
        /// so an accidental hover or right-click during cancel never fires.
        /// </summary>
        public static bool BlockPointerEvents { get; private set; }
        // ── Static zone refs (set by GameUI.SetupDragZones) ──────────────────
        public static RectTransform HandZoneRT;
        public static RectTransform BaseZoneRT;
        public static RectTransform Bf1ZoneRT;
        public static RectTransform Bf2ZoneRT;
        public static Canvas        RootCanvas;

        // ── Drag callbacks (set by GameUI per refresh cycle) ─────────────────
        public System.Action<UnitInstance>            OnDragToBase;
        public System.Action<List<UnitInstance>>    OnDragHandGroupToBase;
        public System.Action<UnitInstance>           OnSpellDragOut;
        public System.Action<List<UnitInstance>>    OnSpellGroupDragOut;
        public System.Action<UnitInstance>           OnDragHeroToBase;
        public System.Action<List<UnitInstance>, int> OnDragToBF;

        // ── Internal state ───────────────────────────────────────────────────
        private CardView _cardView;
        private RectTransform _rt;
        private bool _isDragging;
        private bool _isCancelling;
        private bool _hiddenDuringDrag;   // true while original card is hidden (alpha=0)
        public bool IsHiddenDuringDrag => _hiddenDuringDrag;

        // Main drag ghost
        private GameObject _ghost;
        private Vector2    _dragOriginCanvasPos; // canvas-space origin of dragged card

        // Cluster: ghost copies of other selected cards
        private readonly List<GameObject> _clusterGhosts      = new List<GameObject>();
        private readonly List<CardView>   _clusterOrigViews   = new List<CardView>();
        private readonly List<Vector2>    _clusterGhostOrigins = new List<Vector2>(); // canvas-space spawn positions
        private Coroutine _clusterMoveCoroutine;
        private Sequence  _cancelReturnSeq; // DOT-4: replaced coroutine
        private Vector2   _dragScreenPos; // updated in OnDrag

        private CanvasGroup _selfCanvasGroup;
        private GameObject  _dragOriginOverlay;  // green tint at origin during drag

        private float _pointerDownTime = -1f;
        private const float DragDelay  = 0.1f;   // seconds before drag is recognized
        private bool  _dragPending;               // OnBeginDrag fired but 0.1 s not yet elapsed

        private enum DragSource { Hand, Base, Hero, Other }
        private DragSource _dragSource;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _cardView        = GetComponent<CardView>();
            _rt              = GetComponent<RectTransform>();
            _selfCanvasGroup = GetComponent<CanvasGroup>();
            if (_selfCanvasGroup == null)
                _selfCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void Update()
        {
            // Right-click during drag = cancel drag, animate back to origin
            if (_isDragging && !_isCancelling && Input.GetMouseButtonDown(1))
                StartCancelDrag();

            // Commit a pending drag once 0.1 s has elapsed since pointer-down.
            // This prevents accidental drags from fast clicks.
            if (_dragPending && !_isDragging && !_isCancelling
                && _pointerDownTime >= 0f
                && Time.unscaledTime - _pointerDownTime >= DragDelay)
            {
                _dragPending = false;
                CommitDragStart();
            }
        }

        private void OnDestroy()
        {
            _isDragging        = false;
            _isCancelling      = false;
            _hiddenDuringDrag  = false;
            _dragPending       = false;
            BlockPointerEvents = false;
            TweenHelper.KillSafe(ref _cancelReturnSeq);
            RestoreCluster();
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }
        }

        // ── IPointerDownHandler ──────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDownTime = Time.unscaledTime;
        }

        // ── IBeginDragHandler ────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanStartDrag()) return;
            // Mark drag as pending; Update() will commit it after DragDelay seconds.
            // This prevents accidental drags from fast clicks where the mouse moves slightly.
            _dragPending   = true;
            _dragScreenPos = eventData.position;
        }

        /// <summary>
        /// Actually starts the drag (called from Update after DragDelay has elapsed).
        /// Uses the current mouse position so the ghost appears under the cursor.
        /// </summary>
        private void CommitDragStart()
        {
            if (!CanStartDrag()) { _dragPending = false; return; }

            _isDragging        = true;
            _isCancelling      = false;
            BlockPointerEvents = true;
            _dragSource        = DetectDragSource();
            _dragRotation      = 0f; // VFX-7e
            _prevDragPos       = ScreenToCanvas(Input.mousePosition); // VFX-7e

            // Store canvas-space origin of the dragged card (for cancel return animation).
            if (RootCanvas != null)
            {
                var originCorners = new Vector3[4];
                _rt.GetWorldCorners(originCorners);
                Vector2 cardScreenPos = new Vector2(
                    (originCorners[0].x + originCorners[2].x) * 0.5f,
                    (originCorners[0].y + originCorners[2].y) * 0.5f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    RootCanvas.GetComponent<RectTransform>(), cardScreenPos, RootCanvas.worldCamera, out _dragOriginCanvasPos);
            }

            Vector2 currentScreenPos = (Vector2)Input.mousePosition;
            CreateGhost(currentScreenPos);

            // Hide original card AFTER ghost is created (so ghost clones from full alpha).
            // Use both alpha=0 and a flag to prevent RefreshUnitList from resetting alpha.
            _hiddenDuringDrag = true;
            if (_selfCanvasGroup != null)
                _selfCanvasGroup.alpha = 0f;

            if (_dragSource == DragSource.Base || _dragSource == DragSource.Hand || _dragSource == DragSource.Hero)
                GatherCluster();

            if (_ghost != null)
                _ghost.transform.SetAsLastSibling();
        }

        // ── IDragHandler ─────────────────────────────────────────────────────

        // VFX-7e: drag rotation
        public const float DRAG_ROTATE_MAX = 10f;  // max ±degrees
        public const float DRAG_ROTATE_SPEED = 4f;
        private float _dragRotation;
        private Vector2 _prevDragPos;

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _isCancelling) return;

            Vector2 canvasPos = ScreenToCanvas(eventData.position);
            _dragScreenPos = eventData.position;

            if (_ghost != null)
            {
                var ghostRT = _ghost.GetComponent<RectTransform>();
                ghostRT.localPosition = new Vector3(canvasPos.x, canvasPos.y, 0f);

                // VFX-7e: tilt ghost based on horizontal movement direction
                float deltaX = canvasPos.x - _prevDragPos.x;
                float targetAngle = -Mathf.Clamp(deltaX * 2f, -DRAG_ROTATE_MAX, DRAG_ROTATE_MAX);
                _dragRotation = Mathf.Lerp(_dragRotation, targetAngle, Time.deltaTime * DRAG_ROTATE_SPEED);
                ghostRT.localRotation = Quaternion.Euler(0f, 0f, _dragRotation);
            }
            _prevDragPos = canvasPos;

        }

        // ── IEndDragHandler ──────────────────────────────────────────────────

        public void OnEndDrag(PointerEventData eventData)
        {
            // Released before 0.1 s delay elapsed — treat as a tap, not a drag
            if (_dragPending) { _dragPending = false; return; }
            if (!_isDragging || _isCancelling) return;

            if (ShouldCancelDrop(eventData.position))
            {
                StartCancelDrag();
                return;
            }

            _isDragging = false;
            _isCancelling = true;
            _dragRotation = 0f; // VFX-7e: reset rotation

            // Stop cluster follow — ghosts freeze in current positions
            if (_clusterMoveCoroutine != null)
            {
                StopCoroutine(_clusterMoveCoroutine);
                _clusterMoveCoroutine = null;
            }

            // Save unit refs before RestoreCluster clears _clusterOrigViews
            var draggedUnit  = _cardView?.Unit;
            var clusterUnits = new List<UnitInstance>();
            foreach (var cv in _clusterOrigViews)
                if (cv?.Unit != null) clusterUnits.Add(cv.Unit);

            StartCoroutine(DropFlowRoutine(eventData.position, draggedUnit, clusterUnits));
        }

        /// <summary>
        /// Orchestrates the full drop flow:
        ///   1. Show Haste prompt if needed (ghosts frozen while user decides).
        ///      - User cancels → animate ghosts back to origin (cancel play).
        ///      - User confirms → set pending Haste decision on GameManager.
        ///   2. Record ghost positions, cleanup, call HandleDrop.
        ///   3. Wait for layout, then animate each new CardView from ghost pos → hover → final.
        /// </summary>
        private IEnumerator DropFlowRoutine(
            Vector2 dropScreenPos,
            UnitInstance draggedUnit,
            List<UnitInstance> clusterUnits)
        {
            var gm = GameManager.Instance;

            // ── Step 1: Haste prompt — hide ghosts while dialog is open ──────────
            bool needsHaste = draggedUnit != null && gm != null && gm.DragNeedsHasteChoice(draggedUnit);
            if (needsHaste && AskPromptUI.Instance != null)
            {
                SetGhostsVisible(false); // hide mid-air ghosts while popup is open

                var task = AskPromptUI.Instance.WaitForConfirm(
                    "急速",
                    $"额外支付 [1] 法力 + [1{draggedUnit.CardData.RuneType.ToColoredText()}] 符能，让 {draggedUnit.UnitName} 以活跃状态进场？",
                    "使用急速",
                    "取消打出");

                yield return new WaitUntil(() => task.IsCompleted || task.IsFaulted || task.IsCanceled);

                bool confirmed = task.IsCompleted && !task.IsFaulted && task.Result;
                if (!confirmed)
                {
                    // Restore ghost so it's visible during the return animation
                    SetGhostsVisible(true);
                    _isCancelling = false;
                    _isDragging   = false;
                    _isCancelling = true;
                    CancelReturnTween();
                    // Wait for cancel tween to finish
                    while (_cancelReturnSeq != null && _cancelReturnSeq.IsActive())
                        yield return null;
                    yield break;
                }

                gm.SetDragHasteDecision(true);
                // Ghost stays hidden — it will be destroyed in Step 2 below
            }

            // ── Step 2: Record ghost positions (keep ghosts alive for now) ─────
            Vector2 mainGhostPos = _ghost != null
                ? (Vector2)_ghost.GetComponent<RectTransform>().localPosition
                : ScreenToCanvas(dropScreenPos);

            var fromPositions = new List<Vector2> { mainGhostPos };
            foreach (var g in _clusterGhosts)
                fromPositions.Add(g != null ? (Vector2)g.GetComponent<RectTransform>().localPosition : mainGhostPos);

            // Capture drag source before HandleDrop (which may destroy 'this' via RefreshUI)
            DragSource capturedSource = _dragSource;

            // ── Step 3: Trigger game logic ────────────────────────────────────────
            HandleDrop(dropScreenPos);

            // ── Step 3.5: Detect play failure — card still in source zone ─────────
            // If the card was NOT consumed by game logic (still in hand/hero),
            // shake the ghost at its current position FIRST, then animate return.
            bool playFailed = false;
            if (gm != null && draggedUnit != null)
            {
                playFailed = capturedSource switch
                {
                    DragSource.Hand => gm.IsUnitInHand(draggedUnit),
                    DragSource.Hero => gm.IsUnitHero(draggedUnit),
                    _ => false,
                };
            }

            if (playFailed && _ghost != null)
            {
                // Shake ghost at drop position, then return to origin
                var ghostRT = _ghost.GetComponent<RectTransform>();
                if (ghostRT != null)
                {
                    var shakeSeq = TweenHelper.ShakeUI(ghostRT, CardView.SHAKE_STRENGTH, CardView.SHAKE_DURATION, CardView.SHAKE_VIBRATO);
                    if (shakeSeq != null)
                    {
                        bool shakeDone = false;
                        shakeSeq.OnComplete(() => shakeDone = true);
                        shakeSeq.OnKill(() => shakeDone = true);
                        while (!shakeDone) yield return null;
                    }
                }
                // Now animate ghost back to origin
                _isCancelling = true;
                CancelReturnTween();
                while (_cancelReturnSeq != null && _cancelReturnSeq.IsActive())
                    yield return null;
                yield break;
            }

            // ── Step 4: Cleanup ghosts (play succeeded) ──────────────────────────
            RemoveDragOriginOverlay();
            RestoreCluster();
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }

            // ── Step 5: Wait for ALL prompts to be resolved ──────────────────────
            yield return null;
            while (AskPromptUI.IsShowing || SpellTargetPopup.IsShowing)
                yield return null;

            // ── Step 6: Animate on a temporary host ──────────────────────────────
            SpawnDropAnimation(capturedSource, draggedUnit, clusterUnits, fromPositions);
            _isCancelling      = false;
            BlockPointerEvents = false;
        }

        /// <summary>Show or hide all drag ghosts (main + cluster) without moving them.</summary>
        private void SetGhostsVisible(bool visible)
        {
            if (_ghost != null)
            {
                var cg = _ghost.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = visible ? 0.72f : 0f;
            }
            foreach (var g in _clusterGhosts)
            {
                if (g == null) continue;
                var cg = g.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = visible ? 1f : 0f;
            }
        }

        // ── Cancel drag ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when the drop position doesn't correspond to a valid target zone,
        /// meaning the drag should be cancelled and cards should animate back.
        /// </summary>
        private bool ShouldCancelDrop(Vector2 screenPos)
        {
            Camera cam      = RootCanvas?.worldCamera;
            bool overHand   = HandZoneRT != null && RectTransformUtility.RectangleContainsScreenPoint(HandZoneRT, screenPos, cam);
            bool overBase   = BaseZoneRT != null && RectTransformUtility.RectangleContainsScreenPoint(BaseZoneRT, screenPos, cam);
            bool overBf1    = Bf1ZoneRT  != null && RectTransformUtility.RectangleContainsScreenPoint(Bf1ZoneRT,  screenPos, cam);
            bool overBf2    = Bf2ZoneRT  != null && RectTransformUtility.RectangleContainsScreenPoint(Bf2ZoneRT,  screenPos, cam);

            switch (_dragSource)
            {
                case DragSource.Hand:
                {
                    var unit = _cardView?.Unit;
                    if (unit == null) return true;
                    // Spell: valid drop = outside hand zone
                    if (unit.CardData.IsSpell) return overHand;
                    // Unit: valid drop = base zone
                    return !overBase;
                }
                case DragSource.Hero:
                    return !overBase;
                case DragSource.Base:
                    return !overBf1 && !overBf2;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Begins cancel animation: stops follow coroutine, animates ghosts back to origins.
        /// Selection state is intentionally preserved (no deselect callbacks fired).
        /// </summary>
        public const float CANCEL_RETURN_DURATION = 0.21f;
        public const float CANCEL_STAGGER_DELAY  = 0.05f;

        private void StartCancelDrag()
        {
            if (_isCancelling) return;
            _isCancelling = true;
            _isDragging   = false; // stops ClusterFollowRoutine

            CancelReturnTween();
        }

        /// <summary>DOT-4: Cancel-return animation via DOTween Sequence (replaced coroutine).</summary>
        private void CancelReturnTween()
        {
            TweenHelper.KillSafe(ref _cancelReturnSeq);
            _cancelReturnSeq = DOTween.Sequence();

            // Main ghost returns to origin
            if (_ghost != null)
            {
                var gRT = _ghost.GetComponent<RectTransform>();
                if (gRT != null)
                {
                    _cancelReturnSeq.Join(
                        DOTween.To(
                            () => (Vector2)gRT.localPosition,
                            v => gRT.localPosition = new Vector3(v.x, v.y, 0f),
                            _dragOriginCanvasPos, CANCEL_RETURN_DURATION)
                        .SetEase(Ease.InOutQuad)
                        .SetTarget(gRT));
                }
            }

            // Cluster ghosts with staggered delays
            for (int i = 0; i < _clusterGhosts.Count; i++)
            {
                var g = _clusterGhosts[i];
                if (g == null) continue;
                var gRT = g.GetComponent<RectTransform>();
                if (gRT == null) continue;

                Vector2 origin = i < _clusterGhostOrigins.Count
                    ? _clusterGhostOrigins[i] : _dragOriginCanvasPos;
                float delay = (i + 1) * CANCEL_STAGGER_DELAY;

                _cancelReturnSeq.Insert(delay,
                    DOTween.To(
                        () => (Vector2)gRT.localPosition,
                        v => gRT.localPosition = new Vector3(v.x, v.y, 0f),
                        origin, CANCEL_RETURN_DURATION)
                    .SetEase(Ease.InOutQuad)
                    .SetTarget(gRT));
            }

            _cancelReturnSeq.OnComplete(() =>
            {
                _cancelReturnSeq = null;
                FinishDragCancel();
            });
        }

        private void FinishDragCancel()
        {
            _cancelReturnSeq = null;
            _isCancelling          = false;

            RemoveDragOriginOverlay();

            if (_ghost != null) { Destroy(_ghost); _ghost = null; }

            RestoreCluster();

            // Clear all hand/base selections on cancel so cards return to neutral state
            GameManager.Instance?.ClearAllSelections();

            StartCoroutine(UnblockEventsAfterCancel());
        }

        private IEnumerator UnblockEventsAfterCancel()
        {
            yield return null;
            yield return null;
            BlockPointerEvents = false;
        }

        // ── Drop resolution ──────────────────────────────────────────────────

        private void HandleDrop(Vector2 screenPos)
        {
            var unit = _cardView != null ? _cardView.Unit : null;
            if (unit == null) return;
            // Defense-in-depth: game state may have changed during async prompts between drag and drop
            if (GameManager.Instance == null) return;

            Camera cam = RootCanvas != null ? RootCanvas.worldCamera : null;

            bool overBf1  = Bf1ZoneRT  != null && RectTransformUtility.RectangleContainsScreenPoint(Bf1ZoneRT,  screenPos, cam);
            bool overBf2  = Bf2ZoneRT  != null && RectTransformUtility.RectangleContainsScreenPoint(Bf2ZoneRT,  screenPos, cam);
            bool overBase = BaseZoneRT != null && RectTransformUtility.RectangleContainsScreenPoint(BaseZoneRT, screenPos, cam);
            bool overHand = HandZoneRT != null && RectTransformUtility.RectangleContainsScreenPoint(HandZoneRT, screenPos, cam);

            switch (_dragSource)
            {
                case DragSource.Hand:
                    if (unit.CardData.IsSpell)
                    {
                        if (!overHand)
                        {
                            var gm2      = GameManager.Instance;
                            var sel2     = gm2 != null ? gm2.GetSelectedHandUnits() : null;
                            var spellGroup = new List<UnitInstance>();
                            if (sel2 != null)
                                foreach (var s in sel2) if (s.CardData.IsSpell) spellGroup.Add(s);
                            if (!spellGroup.Contains(unit)) spellGroup.Add(unit);

                            if (spellGroup.Count > 1)
                                OnSpellGroupDragOut?.Invoke(spellGroup);
                            else
                                OnSpellDragOut?.Invoke(unit);
                        }
                    }
                    else if (overBase)
                    {
                        var gm  = GameManager.Instance;
                        var sel = gm != null ? gm.GetSelectedHandUnits() : null;
                        if (sel != null && sel.Count > 0)
                        {
                            var group = new List<UnitInstance>(sel);
                            if (!group.Contains(unit)) group.Add(unit);
                            OnDragHandGroupToBase?.Invoke(group);
                        }
                        else
                            OnDragToBase?.Invoke(unit);
                    }
                    break;

                case DragSource.Hero:
                    if (!overHand)
                        OnDragHeroToBase?.Invoke(unit);
                    break;

                case DragSource.Base:
                    int bfIdx = overBf1 ? 0 : overBf2 ? 1 : -1;
                    if (bfIdx >= 0 && OnDragToBF != null)
                    {
                        var units = BuildDragGroup(unit);
                        OnDragToBF.Invoke(units, bfIdx);
                    }
                    break;
            }
        }

        // ── Drag origin overlay helpers ──────────────────────────────────────

        private void AddDragOriginOverlay()
        {
            if (_dragOriginOverlay != null) return;
            _dragOriginOverlay = new GameObject("DragOriginOverlay", typeof(Image), typeof(CanvasGroup));
            _dragOriginOverlay.transform.SetParent(transform, false);
            var img = _dragOriginOverlay.GetComponent<Image>();
            img.color = new Color(0.1f, 0.85f, 0.25f, 0.4f);   // semi-transparent green
            var rt = _dragOriginOverlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var cg = _dragOriginOverlay.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable   = false;
        }

        private void RemoveDragOriginOverlay()
        {
            if (_dragOriginOverlay != null) { Destroy(_dragOriginOverlay); _dragOriginOverlay = null; }
            _hiddenDuringDrag = false;
            if (_selfCanvasGroup != null) _selfCanvasGroup.alpha = 1f;
        }

        // ── Independent drop animation ────────────────────────────────────────

        /// <summary>
        /// Starts the landing animation on a temporary host GO so it survives
        /// RefreshUI potentially destroying this CardDragHandler's GameObject.
        /// </summary>
        private void SpawnDropAnimation(DragSource dragSource, UnitInstance mainUnit,
                                        List<UnitInstance> clusterUnits, List<Vector2> fromPositions)
        {
            if (RootCanvas == null) return;
            var hostGO = new GameObject("DropAnimHost");
            hostGO.transform.SetParent(RootCanvas.transform, false);
            var host = hostGO.AddComponent<DropAnimHost>();
            host.Run(dragSource, mainUnit, clusterUnits, fromPositions);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private bool CanStartDrag()
        {
            if (_cardView == null || _cardView.Unit == null) return false;
            var gm = GameManager.Instance;
            if (gm == null) return false;
            return gm.IsPlayerActionPhase();
        }

        private DragSource DetectDragSource()
        {
            var gm = GameManager.Instance;
            if (gm == null) return DragSource.Other;
            var unit = _cardView.Unit;
            if (gm.IsUnitInHand(unit))  return DragSource.Hand;
            if (gm.IsUnitInBase(unit))  return DragSource.Base;
            if (gm.IsUnitHero(unit))    return DragSource.Hero;
            return DragSource.Other;
        }

        private void CreateGhost(Vector2 screenPos)
        {
            if (RootCanvas == null) return;

            _ghost      = Instantiate(gameObject, RootCanvas.transform);
            _ghost.name = "DragGhost";

            var dh   = _ghost.GetComponent<CardDragHandler>(); if (dh   != null) Destroy(dh);
            var btn  = _ghost.GetComponent<Button>();           if (btn  != null) Destroy(btn);
            // Remove any CardHoverScale so the ghost never reacts to its own hover
            var chs = _ghost.GetComponent<CardHoverScale>(); if (chs != null) Destroy(chs);

            var cg = _ghost.GetComponent<CanvasGroup>() ?? _ghost.AddComponent<CanvasGroup>();
            cg.alpha          = 0.72f;
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            // Force ghost to render above all other cards (including CardHoverScale override canvases)
            var ghostCanvas = _ghost.GetComponent<Canvas>() ?? _ghost.AddComponent<Canvas>();
            ghostCanvas.overrideSorting = true;
            ghostCanvas.sortingOrder    = 9999;
            if (_ghost.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                _ghost.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            Vector2 originalSize = _rt.rect.size;
            var ghostRT = _ghost.GetComponent<RectTransform>();
            ghostRT.localScale = Vector3.one * 0.88f;
            ghostRT.anchorMin  = new Vector2(0.5f, 0.5f);
            ghostRT.anchorMax  = new Vector2(0.5f, 0.5f);
            ghostRT.pivot      = new Vector2(0.5f, 0.5f);
            ghostRT.sizeDelta  = originalSize;
            Vector2 localPos   = ScreenToCanvas(screenPos);
            ghostRT.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        }

        // Gather ghost copies of other selected cards so they follow this drag.
        // Originals stay in their HLG — only ghost copies move on the canvas root.
        private void GatherCluster()
        {
            _clusterGhosts.Clear();
            _clusterOrigViews.Clear();
            _clusterGhostOrigins.Clear();

            var gm = GameManager.Instance;
            if (gm == null) return;

            List<UnitInstance> selected = _dragSource == DragSource.Hand
                ? gm.GetSelectedHandUnits()
                : gm.GetSelectedBaseUnits();

            if (selected == null || selected.Count == 0) return;
            if (RootCanvas == null) return;

            var thisUnit = _cardView.Unit;
            Camera uiCam = RootCanvas.worldCamera;

            foreach (var unit in selected)
            {
                if (unit == thisUnit) continue;
                var cv = FindCardViewInScene(unit);
                if (cv == null) continue;
                var rt = cv.GetComponent<RectTransform>();
                if (rt == null) continue;

                // Hide original in-place — ghost replaces it visually
                cv.SuspendLift();
                var origCG = cv.GetComponent<CanvasGroup>() ?? cv.gameObject.AddComponent<CanvasGroup>();
                origCG.alpha = 0f;
                _clusterOrigViews.Add(cv);

                // Snapshot screen position before creating ghost.
                // For SS-Overlay, rt.position is already in screen pixel space.
                var cardCorners = new Vector3[4];
                rt.GetWorldCorners(cardCorners);
                Vector2 screenPos = new Vector2(
                    (cardCorners[0].x + cardCorners[2].x) * 0.5f,
                    (cardCorners[0].y + cardCorners[2].y) * 0.5f);
                Vector2 cardSize  = rt.rect.size;

                var ghost = Instantiate(cv.gameObject, RootCanvas.transform);
                ghost.name = "ClusterGhost";

                var dh2  = ghost.GetComponent<CardDragHandler>(); if (dh2  != null) Destroy(dh2);
                var btn2 = ghost.GetComponent<Button>();           if (btn2 != null) Destroy(btn2);

                var gcg = ghost.GetComponent<CanvasGroup>() ?? ghost.AddComponent<CanvasGroup>();
                gcg.alpha          = 1f;   // cluster ghosts are fully opaque — they represent the actual card
                gcg.blocksRaycasts = false;
                gcg.interactable   = false;

                var ghostRT = ghost.GetComponent<RectTransform>();
                ghostRT.anchorMin = new Vector2(0.5f, 0.5f);
                ghostRT.anchorMax = new Vector2(0.5f, 0.5f);
                ghostRT.sizeDelta = cardSize;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    RootCanvas.GetComponent<RectTransform>(), screenPos, uiCam, out Vector2 canvasPos);
                ghostRT.localPosition = new Vector3(canvasPos.x, canvasPos.y, 0f);

                _clusterGhosts.Add(ghost);
                _clusterGhostOrigins.Add(canvasPos); // save real origin for cancel animation

                // Start stacked on main card — they spread out naturally via lag as mouse moves
                ghostRT.localPosition = new Vector3(_dragOriginCanvasPos.x, _dragOriginCanvasPos.y, 0f);
            }

            if (_clusterGhosts.Count > 0)
                _clusterMoveCoroutine = StartCoroutine(ClusterFollowRoutine());
        }

        /// <summary>
        /// Each cluster ghost follows the mouse with a progressively slower lerp speed,
        /// creating a natural staggered trailing feel based on selection order.
        /// </summary>
        private IEnumerator ClusterFollowRoutine()
        {
            while (_isDragging)
            {
                Vector2 canvasTarget = ScreenToCanvas(_dragScreenPos);

                for (int i = 0; i < _clusterGhosts.Count; i++)
                {
                    var g = _clusterGhosts[i];
                    if (g == null) continue;
                    var gRT = g.GetComponent<RectTransform>();
                    if (gRT == null) continue;

                    // All ghosts aim for the same position as the main card —
                    // staggered lag speed creates the natural trailing chain without fan spread
                    float lerpSpeed = Mathf.Max(9f - i * 2f, 3f);
                    gRT.localPosition = Vector2.Lerp(gRT.localPosition, canvasTarget, Time.deltaTime * lerpSpeed);
                }

                // Z-order: slowest ghost at bottom, fastest just below main, main on top
                for (int i = _clusterGhosts.Count - 1; i >= 0; i--)
                    if (_clusterGhosts[i] != null) _clusterGhosts[i].transform.SetAsLastSibling();
                if (_ghost != null) _ghost.transform.SetAsLastSibling();

                yield return null;
            }
        }

        private void RestoreCluster()
        {
            if (_clusterMoveCoroutine != null)
            {
                StopCoroutine(_clusterMoveCoroutine);
                _clusterMoveCoroutine = null;
            }

            foreach (var g in _clusterGhosts)
                if (g != null) Destroy(g);
            _clusterGhosts.Clear();
            _clusterGhostOrigins.Clear();

            // Restore original cards: alpha + lift — do NOT touch selection state
            foreach (var cv in _clusterOrigViews)
            {
                if (cv == null) continue;
                var cg = cv.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
                cv.ResumeLift();
            }
            _clusterOrigViews.Clear();
        }

        private List<UnitInstance> BuildDragGroup(UnitInstance dragged)
        {
            var gm = GameManager.Instance;
            var selected = gm != null ? gm.GetSelectedBaseUnits() : null;
            if (selected != null && selected.Contains(dragged))
                return new List<UnitInstance>(selected);
            return new List<UnitInstance> { dragged };
        }

        // DEV-26: replaced FindObjectsOfType (O(n) scene scan) with GameUI.FindCardView lookup
        private static CardView FindCardViewInScene(UnitInstance unit)
            => GameUI.Instance != null ? GameUI.Instance.FindCardView(unit) : null;

        // ── Drop land animation (independent host) ───────────────────────────

        /// <summary>
        /// Runs the landing animation on its own temporary GameObject so it is never
        /// killed when RefreshUI destroys the source CardView's GameObject.
        /// </summary>
        private sealed class DropAnimHost : MonoBehaviour
        {
            // Safety net: all CanvasGroups whose alpha we set to 0.
            // OnDestroy guarantees they are restored to 1 even if the
            // coroutine is interrupted, the host is destroyed early,
            // or any other unexpected interruption occurs.
            private readonly List<CanvasGroup> _managedCGs = new List<CanvasGroup>();
            private readonly List<CanvasGroup> _fadeInCGs  = new List<CanvasGroup>();
            // DOT-4 Codex H-1: track overlay GOs + master sequence for OnDestroy cleanup
            private readonly List<GameObject> _overlayObjs = new List<GameObject>();
            private Sequence _masterSeq;

            public void Run(DragSource dragSource, UnitInstance mainUnit,
                            List<UnitInstance> clusterUnits, List<Vector2> fromPositions)
            {
                StartCoroutine(AnimRoutine(dragSource, mainUnit, clusterUnits, fromPositions));
            }

            private void OnDestroy()
            {
                // DOT-4 Codex H-1: kill master sequence to prevent callbacks on dead objects
                TweenHelper.KillSafe(ref _masterSeq);
                // Destroy overlay GOs that may still exist mid-animation
                foreach (var obj in _overlayObjs)
                    if (obj != null) Destroy(obj);
                _overlayObjs.Clear();
                // SAFETY NET: restore alpha=1 for any cards we hid.
                foreach (var cg in _managedCGs)
                    if (cg != null) cg.alpha = 1f;
                _managedCGs.Clear();
                // SAFETY NET: restore fade-in cards too
                foreach (var cg in _fadeInCGs)
                    if (cg != null) { DOTween.Kill(cg.gameObject); cg.alpha = 1f; }
                _fadeInCGs.Clear();
            }

            private IEnumerator AnimRoutine(DragSource dragSource, UnitInstance mainUnit,
                                            List<UnitInstance> clusterUnits, List<Vector2> fromPositions)
            {
                // Wait for the unit to leave its source zone (game-state driven).
                int waitFrames = 0;
                var gm = GameManager.Instance;
                while (waitFrames < 60)
                {
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    if (mainUnit == null || gm == null) break;

                    bool stillInSource = dragSource switch
                    {
                        DragSource.Hand => gm.IsUnitInHand(mainUnit),
                        DragSource.Hero => gm.IsUnitHero(mainUnit),
                        DragSource.Base => gm.IsUnitInBase(mainUnit),
                        _               => false,
                    };
                    if (!stillInSource) break;
                    waitFrames++;
                }
                // Two extra frames for HLG to finish repositioning children
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();

                const float phase1Dur      = 0.18f;
                const float phase2Dur      = 0.16f;
                const float hoverHeight    = 70f;
                const float stagger        = 0.04f;
                const float slingshotDur   = 0.05f; // DOT-8: pullback duration
                const float slingshotDist  = 18f;   // DOT-8: pullback distance px

                var allUnits = new List<UnitInstance>();
                if (mainUnit != null) allUnits.Add(mainUnit);
                allUnits.AddRange(clusterUnits);

                var overlayItems = new List<(RectTransform overlayRT, Vector2 from, Vector2 to)>();
                var overlayObjs  = new List<GameObject>();

                for (int i = 0; i < allUnits.Count; i++)
                {
                    var unit = allUnits[i];
                    if (unit == null) continue;
                    var cv = FindCardViewInScene(unit);
                    if (cv == null) continue;

                    var cvRT = cv.GetComponent<RectTransform>();
                    if (cvRT == null) continue;

                    // Get final canvas-space position using SS-Overlay safe corners
                    var corners = new Vector3[4];
                    cvRT.GetWorldCorners(corners);
                    Vector2 screenCenter = new Vector2(
                        (corners[0].x + corners[2].x) * 0.5f,
                        (corners[0].y + corners[2].y) * 0.5f);
                    Vector2 finalPos = Vector2.zero;
                    if (RootCanvas != null)
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            RootCanvas.GetComponent<RectTransform>(), screenCenter,
                            RootCanvas.worldCamera, out finalPos);

                    Vector2 fromPos = i < fromPositions.Count ? fromPositions[i] : fromPositions[0];

                    if (RootCanvas == null) continue;

                    // Cancel EnterAnimRoutine to prevent scale/position animation conflict.
                    cv.CancelEnterAnim();

                    // Hide the real card while the overlay flies in (or fades in).
                    // OnDestroy safety net guarantees alpha is restored even if
                    // this coroutine is interrupted for any reason.
                    var cardCG = cv.GetComponent<CanvasGroup>() ?? cv.gameObject.AddComponent<CanvasGroup>();
                    cardCG.alpha = 0f;
                    _managedCGs.Add(cardCG);

                    // If from and to are very close (e.g. after a confirm dialog where
                    // RefreshUI already placed the card at its final position), skip the
                    // fly-in overlay but still fade-in the real card to avoid a pop-in.
                    if (Vector2.Distance(fromPos, finalPos) < 5f)
                    {
                        _fadeInCGs.Add(cardCG);
                        continue;
                    }

                    var overlay = Instantiate(cv.gameObject, RootCanvas.transform);
                    overlay.name = "LandGhost";
                    var dh2  = overlay.GetComponent<CardDragHandler>(); if (dh2  != null) Destroy(dh2);
                    var btn2 = overlay.GetComponent<Button>();           if (btn2  != null) Destroy(btn2);

                    var ocg = overlay.GetComponent<CanvasGroup>() ?? overlay.AddComponent<CanvasGroup>();
                    ocg.alpha = 1f; ocg.blocksRaycasts = false; ocg.interactable = false;

                    var oRT = overlay.GetComponent<RectTransform>();
                    oRT.anchorMin  = oRT.anchorMax = new Vector2(0.5f, 0.5f);
                    oRT.pivot      = new Vector2(0.5f, 0.5f);
                    oRT.sizeDelta  = cvRT.rect.size;
                    oRT.localScale = Vector3.one;
                    oRT.localPosition = new Vector3(fromPos.x, fromPos.y, 0f);
                    overlay.transform.SetAsLastSibling();
                    overlayObjs.Add(overlay);

                    overlayItems.Add((oRT, fromPos, finalPos));
                }

                // DOT-4: Drop landing animation via DOTween Sequences
                // Store overlay refs for OnDestroy cleanup (Codex H-1)
                _overlayObjs.AddRange(overlayObjs);

                if (overlayItems.Count > 0)
                {
                    _masterSeq = DOTween.Sequence().SetTarget(this);
                    for (int i = 0; i < overlayItems.Count; i++)
                    {
                        var item = overlayItems[i];
                        Vector2 hover = item.to + new Vector2(0f, hoverHeight);
                        float delay = i * stagger;

                        var cardSeq = DOTween.Sequence();
                        // DOT-8: Phase 0 — slingshot pullback (opposite direction of travel)
                        Vector2 travelDir = (hover - item.from).normalized;
                        Vector2 pullPos   = item.from - travelDir * slingshotDist;
                        cardSeq.Append(
                            DOTween.To(() => (Vector2)item.overlayRT.localPosition,
                                       v => item.overlayRT.localPosition = new Vector3(v.x, v.y, 0f),
                                       pullPos, slingshotDur)
                            .SetEase(Ease.InQuad)
                            .SetTarget(item.overlayRT));
                        // Phase 1: fly to hover (OutBack gives spring feel)
                        cardSeq.Append(
                            DOTween.To(() => (Vector2)item.overlayRT.localPosition,
                                       v => item.overlayRT.localPosition = new Vector3(v.x, v.y, 0f),
                                       hover, phase1Dur)
                            .SetEase(Ease.OutBack)
                            .SetTarget(item.overlayRT));
                        // Phase 2: drop to final with bounce landing
                        cardSeq.Append(
                            DOTween.To(() => (Vector2)item.overlayRT.localPosition,
                                       v => item.overlayRT.localPosition = new Vector3(v.x, v.y, 0f),
                                       item.to, phase2Dur)
                            .SetEase(Ease.OutBounce)
                            .SetTarget(item.overlayRT));

                        _masterSeq.Insert(delay, cardSeq);
                    }

                    // DOT-4 Codex H-2: OnKill also sets flag to prevent coroutine hang
                    bool tweenDone = false;
                    _masterSeq.OnComplete(() => tweenDone = true);
                    _masterSeq.OnKill(() => tweenDone = true);
                    while (!tweenDone) yield return null;
                    _masterSeq = null;
                }

                // Restore alpha + cleanup (also done in OnDestroy as safety net)
                foreach (var cg in _managedCGs)
                    if (cg != null) cg.alpha = 1f;
                _managedCGs.Clear();

                // Fade-in cards that were too close for a fly-in overlay (e.g. after confirm dialog)
                const float fadeInDur = 0.25f;
                foreach (var cg in _fadeInCGs)
                {
                    if (cg != null)
                        DOTween.To(() => cg.alpha, a => { if (cg != null) cg.alpha = a; }, 1f, fadeInDur)
                            .SetEase(Ease.OutQuad)
                            .SetTarget(cg.gameObject);
                }
                _fadeInCGs.Clear();

                foreach (var obj in overlayObjs)
                    if (obj != null) Destroy(obj);
                _overlayObjs.Clear();

                Destroy(gameObject); // clean up this temporary host
            }
        }

        // ── Canvas coordinate helper ─────────────────────────────────────────

        private Vector2 ScreenToCanvas(Vector2 screenPos)
        {
            if (RootCanvas == null) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                RootCanvas.GetComponent<RectTransform>(),
                screenPos,
                RootCanvas.worldCamera,
                out Vector2 local);
            return local;
        }
    }
}
