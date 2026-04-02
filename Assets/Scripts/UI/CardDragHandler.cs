using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FWTCG;
using FWTCG.Core;

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

        // Main drag ghost
        private GameObject _ghost;
        private Vector2    _dragOriginCanvasPos; // canvas-space origin of dragged card

        // Cluster: ghost copies of other selected cards
        private readonly List<GameObject> _clusterGhosts      = new List<GameObject>();
        private readonly List<CardView>   _clusterOrigViews   = new List<CardView>();
        private readonly List<Vector2>    _clusterGhostOrigins = new List<Vector2>(); // canvas-space spawn positions
        private Coroutine _clusterMoveCoroutine;
        private Coroutine _cancelReturnCoroutine;
        private Vector2   _dragScreenPos; // updated in OnDrag

        private CanvasGroup _selfCanvasGroup;
        private PortalVFX   _portalVFX;
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
            _portalVFX       = GetComponent<PortalVFX>();
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
            _dragPending       = false;
            BlockPointerEvents = false;
            if (_cancelReturnCoroutine != null) StopCoroutine(_cancelReturnCoroutine);
            RestoreCluster();
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }
            if (_portalVFX != null) _portalVFX.Hide();
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

            // Dim original card and add green overlay
            if (_selfCanvasGroup != null)
                _selfCanvasGroup.alpha = 0.45f;
            AddDragOriginOverlay();

            Vector2 currentScreenPos = (Vector2)Input.mousePosition;
            CreateGhost(currentScreenPos);

            if (_dragSource == DragSource.Base || _dragSource == DragSource.Hand || _dragSource == DragSource.Hero)
                GatherCluster();

            if (_ghost != null)
                _ghost.transform.SetAsLastSibling();

            if (_portalVFX != null)
            {
                Vector2 canvasPos = ScreenToCanvas(currentScreenPos);
                _portalVFX.Show(canvasPos);
            }
        }

        // ── IDragHandler ─────────────────────────────────────────────────────

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _isCancelling) return;

            Vector2 canvasPos = ScreenToCanvas(eventData.position);
            _dragScreenPos = eventData.position;

            if (_ghost != null)
            {
                var ghostRT = _ghost.GetComponent<RectTransform>();
                ghostRT.localPosition = new Vector3(canvasPos.x, canvasPos.y, 0f);
            }

            if (_portalVFX != null)
                _portalVFX.MoveTo(canvasPos);
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
            if (_portalVFX != null) _portalVFX.Hide();

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

            // ── Step 1: Haste prompt (ghosts stay frozen while dialog is open) ──
            bool needsHaste = draggedUnit != null && gm != null && gm.DragNeedsHasteChoice(draggedUnit);
            if (needsHaste && AskPromptUI.Instance != null)
            {
                var task = AskPromptUI.Instance.WaitForConfirm(
                    "急速",
                    $"额外支付 [1] 法力 + [1{draggedUnit.CardData.RuneType}] 符能，让 {draggedUnit.UnitName} 以活跃状态进场？",
                    "使用急速",
                    "取消打出");

                yield return new WaitUntil(() => task.IsCompleted || task.IsFaulted || task.IsCanceled);

                bool confirmed = task.IsCompleted && !task.IsFaulted && task.Result;
                if (!confirmed)
                {
                    // User cancelled — animate ghosts back to origin
                    _isCancelling = false; // let CancelReturnRoutine manage the flag
                    _isDragging   = false;
                    _isCancelling = true;
                    yield return CancelReturnRoutine();
                    FinishDragCancel();
                    yield break;
                }

                gm.SetDragHasteDecision(true);
            }

            // ── Step 2: Record ghost positions, cleanup, commit game logic ───────
            Vector2 mainGhostPos = _ghost != null
                ? (Vector2)_ghost.GetComponent<RectTransform>().localPosition
                : ScreenToCanvas(dropScreenPos);

            var fromPositions = new List<Vector2> { mainGhostPos };
            foreach (var g in _clusterGhosts)
                fromPositions.Add(g != null ? (Vector2)g.GetComponent<RectTransform>().localPosition : mainGhostPos);

            RemoveDragOriginOverlay();
            RestoreCluster();
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }

            // Capture drag source before HandleDrop (which may destroy 'this' via RefreshUI)
            DragSource capturedSource = _dragSource;

            HandleDrop(dropScreenPos);

            // ── Step 3: Animate on a temporary host so RefreshUI destroying 'this' can't cancel it ──
            SpawnDropAnimation(capturedSource, draggedUnit, clusterUnits, fromPositions);
            _isCancelling      = false;
            BlockPointerEvents = false;
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
        private void StartCancelDrag()
        {
            if (_isCancelling) return;
            _isCancelling = true;
            _isDragging   = false; // stops ClusterFollowRoutine

            if (_portalVFX != null) _portalVFX.Hide();

            _cancelReturnCoroutine = StartCoroutine(CancelReturnRoutine());
        }

        private IEnumerator CancelReturnRoutine()
        {
            const float duration = 0.42f;
            float elapsed = 0f;

            // Snapshot current positions at start of animation
            Vector2 mainStart = _ghost != null
                ? (Vector2)_ghost.GetComponent<RectTransform>().localPosition
                : _dragOriginCanvasPos;

            int clusterCount = _clusterGhosts.Count;
            var clusterStarts = new Vector2[clusterCount];
            for (int i = 0; i < clusterCount; i++)
            {
                var g = _clusterGhosts[i];
                clusterStarts[i] = g != null
                    ? (Vector2)g.GetComponent<RectTransform>().localPosition
                    : (i < _clusterGhostOrigins.Count ? _clusterGhostOrigins[i] : mainStart);
            }

            while (elapsed < duration + clusterCount * 0.05f)
            {
                elapsed += Time.deltaTime;

                // Animate main ghost
                if (_ghost != null)
                {
                    var gRT = _ghost.GetComponent<RectTransform>();
                    if (gRT != null)
                    {
                        float t     = Mathf.Clamp01(elapsed / duration);
                        float eased = Smoothstep(t);
                        gRT.localPosition = Vector2.Lerp(mainStart, _dragOriginCanvasPos, eased);
                    }
                }

                // Animate cluster ghosts with staggered delay
                for (int i = 0; i < clusterCount; i++)
                {
                    var g = _clusterGhosts[i];
                    if (g == null) continue;
                    var gRT = g.GetComponent<RectTransform>();
                    if (gRT == null) continue;

                    float delay     = (i + 1) * 0.05f;
                    float t         = Mathf.Clamp01((elapsed - delay) / duration);
                    float eased     = Smoothstep(t);
                    Vector2 origin  = i < _clusterGhostOrigins.Count ? _clusterGhostOrigins[i] : _dragOriginCanvasPos;
                    gRT.localPosition = Vector2.Lerp(clusterStarts[i], origin, eased);
                }

                yield return null;
            }

            FinishDragCancel();
        }

        private void FinishDragCancel()
        {
            _cancelReturnCoroutine = null;
            _isCancelling          = false;

            // Restore original card visibility WITHOUT touching selection state
            RemoveDragOriginOverlay();

            // Destroy main ghost
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }

            // Restore cluster originals (alpha only, no deselect)
            RestoreCluster();

            // Delay unblocking pointer events by 2 frames so the card fully settles
            // visually before hover/right-click events can fire again.
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
            var pvfx = _ghost.GetComponent<PortalVFX>();        if (pvfx != null) Destroy(pvfx);

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

                // Dim original in-place — visible but clearly "in motion"
                cv.SuspendLift();
                var origCG = cv.GetComponent<CanvasGroup>() ?? cv.gameObject.AddComponent<CanvasGroup>();
                origCG.alpha = 0.35f;
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
                var pvfx = ghost.GetComponent<PortalVFX>();        if (pvfx != null) Destroy(pvfx);

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

        private static CardView FindCardViewInScene(UnitInstance unit)
        {
            foreach (var cv in FindObjectsOfType<CardView>())
            {
                if (cv.Unit == unit) return cv;
            }
            return null;
        }

        // ── Drop land animation (independent host) ───────────────────────────

        /// <summary>
        /// Runs the landing animation on its own temporary GameObject so it is never
        /// killed when RefreshUI destroys the source CardView's GameObject.
        /// </summary>
        private sealed class DropAnimHost : MonoBehaviour
        {
            public void Run(DragSource dragSource, UnitInstance mainUnit,
                            List<UnitInstance> clusterUnits, List<Vector2> fromPositions)
            {
                StartCoroutine(AnimRoutine(dragSource, mainUnit, clusterUnits, fromPositions));
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

                const float phase1Dur   = 0.18f;
                const float phase2Dur   = 0.16f;
                const float hoverHeight = 70f;
                const float stagger     = 0.04f;

                var allUnits = new List<UnitInstance>();
                if (mainUnit != null) allUnits.Add(mainUnit);
                allUnits.AddRange(clusterUnits);

                var items       = new List<(CanvasGroup cardCG, RectTransform overlayRT, Vector2 from, Vector2 to)>();
                var overlayObjs = new List<GameObject>();

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

                    if (Vector2.Distance(fromPos, finalPos) < 5f) continue;
                    if (RootCanvas == null) continue;

                    var overlay = Instantiate(cv.gameObject, RootCanvas.transform);
                    overlay.name = "LandGhost";
                    var dh2  = overlay.GetComponent<CardDragHandler>(); if (dh2  != null) Destroy(dh2);
                    var btn2 = overlay.GetComponent<Button>();           if (btn2  != null) Destroy(btn2);
                    var pvfx = overlay.GetComponent<PortalVFX>();        if (pvfx  != null) Destroy(pvfx);

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

                    var cardCG = cv.GetComponent<CanvasGroup>() ?? cv.gameObject.AddComponent<CanvasGroup>();
                    cardCG.alpha = 0f;
                    items.Add((cardCG, oRT, fromPos, finalPos));
                }

                if (items.Count > 0)
                {
                    float totalDuration = phase1Dur + phase2Dur + (items.Count - 1) * stagger;
                    float elapsed       = 0f;
                    while (elapsed < totalDuration)
                    {
                        elapsed += Time.deltaTime;
                        for (int i = 0; i < items.Count; i++)
                        {
                            Vector2 hover = items[i].to + new Vector2(0f, hoverHeight);
                            AnimateDropCard(items[i].overlayRT, items[i].from, hover, items[i].to,
                                            phase1Dur, phase2Dur, elapsed, i * stagger);
                        }
                        yield return null;
                    }
                }

                foreach (var (cardCG, _, _, _) in items)
                    cardCG.alpha = 1f;
                foreach (var obj in overlayObjs)
                    if (obj != null) Destroy(obj);

                Destroy(gameObject); // clean up this temporary host
            }
        }

        private static void AnimateDropCard(RectTransform gRT, Vector2 start,
                                            Vector2 hover, Vector2 final,
                                            float phase1Dur, float phase2Dur,
                                            float elapsed, float delay)
        {
            if (gRT == null) return;
            float t1 = elapsed - delay;
            if (t1 <= 0f)
            {
                gRT.localPosition = start;
            }
            else if (t1 < phase1Dur)
            {
                // Phase 1: fly to hover — EaseOutQuad (decelerate on arrival)
                float t = EaseOutQuad(Mathf.Clamp01(t1 / phase1Dur));
                gRT.localPosition = Vector2.Lerp(start, hover, t);
            }
            else
            {
                // Phase 2: drop to final — EaseInQuad (accelerate like falling)
                float t = EaseInQuad(Mathf.Clamp01((t1 - phase1Dur) / phase2Dur));
                gRT.localPosition = Vector2.Lerp(hover, final, t);
            }
        }

        /// <summary>Quadratic ease-out: fast start, decelerates at end.</summary>
        private static float EaseOutQuad(float t) => t * (2f - t);

        /// <summary>Quadratic ease-in: starts slow, accelerates (falling feel).</summary>
        private static float EaseInQuad(float t) => t * t;

        /// <summary>Smoothstep: ease-in/ease-out curve, t in [0,1].</summary>
        private static float Smoothstep(float t) => t * t * (3f - 2f * t);

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
