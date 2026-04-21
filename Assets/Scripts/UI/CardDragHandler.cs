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
    /// Drag-to-play card handler. UI-OVERHAUL-1a: 单选化 + 删除 cluster ghost。
    ///
    /// Attach to the same GameObject as CardView.
    /// Supports:
    ///   - Hand unit card: drag to base zone → play from hand
    ///   - Hand spell card: drag out of hand zone → trigger spell (shows target popup)
    ///   - Base unit: drag to BF zone → move to battlefield
    ///
    /// Cancel behaviour: right-click during drag 或 release on source zone → ghost 归位。
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
        // UI-OVERHAUL-1a: group 回调全部移除，只保留单元素回调
        public System.Action<UnitInstance>      OnDragToBase;
        public System.Action<UnitInstance>      OnSpellDragOut;
        public System.Action<UnitInstance>      OnDragHeroToBase;
        public System.Action<UnitInstance, int> OnDragToBF;

        // ── Internal state ───────────────────────────────────────────────────
        private CardView _cardView;
        private RectTransform _rt;
        private bool _isDragging;
        private bool _isCancelling;
        private bool _hiddenDuringDrag;   // true while original card is hidden (alpha=0)
        public bool IsHiddenDuringDrag => _hiddenDuringDrag;

        // Main drag ghost (单张拖拽时的半透明副本)
        private GameObject _ghost;
        private Vector2    _dragOriginCanvasPos; // legacy canvas-space
        private Vector3    _dragOriginWorldPos;  // world-space origin（SS-Camera 下正确的参考）

        private Sequence _cancelReturnSeq; // DOT-4: replaced coroutine
        private Vector2   _dragScreenPos;

        private CanvasGroup _selfCanvasGroup;
        private GameObject  _dragOriginOverlay;  // green tint at origin during drag

        private float _pointerDownTime = -1f;
        private const float DragDelay  = 0.1f;   // seconds before drag is recognized
        private bool  _dragPending;

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
            _dragRotation      = 0f;
            _prevDragPos       = ScreenToCanvas(Input.mousePosition);

            // Store origin of dragged card (world-space, SS-Camera 安全).
            if (RootCanvas != null && _rt != null)
            {
                _dragOriginWorldPos = _rt.position;
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

            // Hide original card AFTER ghost is created.
            _hiddenDuringDrag = true;
            if (_selfCanvasGroup != null)
                _selfCanvasGroup.alpha = 0f;

            if (_ghost != null)
                _ghost.transform.SetAsLastSibling();
        }

        // ── IDragHandler ─────────────────────────────────────────────────────

        // VFX-7e: drag rotation
        public const float DRAG_ROTATE_MAX = 10f;
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
            if (_dragPending) { _dragPending = false; return; }
            if (!_isDragging || _isCancelling) return;

            if (ShouldCancelDrop(eventData.position))
            {
                StartCancelDrag();
                return;
            }

            _isDragging = false;
            _isCancelling = true;
            _dragRotation = 0f;

            var draggedUnit = _cardView?.Unit;
            StartCoroutine(DropFlowRoutine(eventData.position, draggedUnit));
        }

        /// <summary>
        /// Orchestrates drop flow: record ghost position → trigger game logic → wait for prompts → land animation.
        /// </summary>
        private IEnumerator DropFlowRoutine(Vector2 dropScreenPos, UnitInstance draggedUnit)
        {
            var gm = GameManager.Instance;

            // ── Step 1: Record ghost position ─────────────────────────────────
            Vector2 ghostPos = _ghost != null
                ? (Vector2)_ghost.GetComponent<RectTransform>().localPosition
                : ScreenToCanvas(dropScreenPos);
            var fromPositions = new List<Vector2> { ghostPos };

            DragSource capturedSource = _dragSource;

            // ── Step 2: Trigger game logic ────────────────────────────────────
            HandleDrop(dropScreenPos);

            // ── Step 3: Wait for any confirm dialog to close ──────────────────
            bool hasPrompt = AskPromptUI.IsShowing || SpellTargetPopup.IsShowing;
            if (hasPrompt)
            {
                SetGhostVisible(false);
                yield return null;
                while (AskPromptUI.IsShowing || SpellTargetPopup.IsShowing)
                    yield return null;
            }

            // ── Step 4: Check if card was consumed or rejected ────────────────
            bool stillInSource = false;
            if (gm != null && draggedUnit != null)
            {
                stillInSource = capturedSource switch
                {
                    DragSource.Hand => gm.IsUnitInHand(draggedUnit),
                    DragSource.Hero => gm.IsUnitHero(draggedUnit),
                    _ => false,
                };
            }

            if (stillInSource && _ghost != null)
            {
                // Play failed or user cancelled — shake ghost then return to origin
                SetGhostVisible(true);
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
                _isCancelling = true;
                CancelReturnTween();
                while (_cancelReturnSeq != null && _cancelReturnSeq.IsActive())
                    yield return null;
                yield break;
            }

            // ── Step 5: Cleanup ghost (play succeeded) ────────────────────────
            RemoveDragOriginOverlay();
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }

            // ── Step 6: Animate on a temporary host ───────────────────────────
            SpawnDropAnimation(capturedSource, draggedUnit, fromPositions);
            _isCancelling      = false;
            BlockPointerEvents = false;
        }

        /// <summary>Show or hide the drag ghost without moving it.</summary>
        private void SetGhostVisible(bool visible)
        {
            if (_ghost != null)
            {
                var cg = _ghost.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = visible ? 0.72f : 0f;
            }
        }

        // ── Cancel drag ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when the drop position doesn't correspond to a valid target zone.
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
                    if (unit.CardData.IsSpell) return overHand;
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

        public const float CANCEL_RETURN_DURATION = 0.21f;

        private void StartCancelDrag()
        {
            if (_isCancelling) return;
            _isCancelling = true;
            _isDragging   = false;

            CancelReturnTween();
        }

        /// <summary>Cancel-return animation via DOTween Sequence.</summary>
        private void CancelReturnTween()
        {
            TweenHelper.KillSafe(ref _cancelReturnSeq);
            _cancelReturnSeq = DOTween.Sequence();

            if (_ghost != null)
            {
                var gT = _ghost.transform;
                if (gT != null)
                {
                    Vector3 target = _dragOriginWorldPos;
                    _cancelReturnSeq.Join(
                        DOTween.To(
                            () => gT.position,
                            v => gT.position = v,
                            target, CANCEL_RETURN_DURATION)
                        .SetEase(Ease.InOutQuad)
                        .SetTarget(gT));
                }
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

            // Clear all selections on cancel so cards return to neutral state
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
                            OnSpellDragOut?.Invoke(unit);
                    }
                    else if (overBase)
                    {
                        OnDragToBase?.Invoke(unit);
                    }
                    break;

                case DragSource.Hero:
                    if (!overHand)
                        OnDragHeroToBase?.Invoke(unit);
                    break;

                case DragSource.Base:
                    int bfIdx = overBf1 ? 0 : overBf2 ? 1 : -1;
                    if (bfIdx >= 0)
                        OnDragToBF?.Invoke(unit, bfIdx);
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
            img.color = new Color(0.1f, 0.85f, 0.25f, 0.4f);
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
        /// Starts landing animation on a temporary host GO so it survives
        /// RefreshUI potentially destroying this CardDragHandler's GameObject.
        /// </summary>
        private void SpawnDropAnimation(DragSource dragSource, UnitInstance mainUnit, List<Vector2> fromPositions)
        {
            if (RootCanvas == null) return;
            var hostGO = new GameObject("DropAnimHost");
            hostGO.transform.SetParent(RootCanvas.transform, false);
            var host = hostGO.AddComponent<DropAnimHost>();
            host.Run(dragSource, mainUnit, fromPositions);
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
            var chs = _ghost.GetComponent<CardHoverScale>(); if (chs != null) Destroy(chs);

            var cg = _ghost.GetComponent<CanvasGroup>() ?? _ghost.AddComponent<CanvasGroup>();
            cg.alpha          = 0.72f;
            cg.blocksRaycasts = false;
            cg.interactable   = false;

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

        private Vector2 ScreenToCanvas(Vector2 screenPos)
        {
            if (RootCanvas == null) return screenPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                RootCanvas.GetComponent<RectTransform>(), screenPos, RootCanvas.worldCamera, out var canvasPos);
            return canvasPos;
        }

        private static CardView FindCardViewInScene(UnitInstance unit)
            => GameUI.Instance != null ? GameUI.Instance.FindCardView(unit) : null;

        // ── Drop land animation (independent host) ───────────────────────────

        /// <summary>
        /// Runs landing animation on its own temporary GameObject so it is never
        /// killed when RefreshUI destroys the source CardView's GameObject.
        /// </summary>
        private sealed class DropAnimHost : MonoBehaviour
        {
            private readonly List<CanvasGroup> _managedCGs = new List<CanvasGroup>();
            private readonly List<CanvasGroup> _fadeInCGs  = new List<CanvasGroup>();
            private readonly List<GameObject> _overlayObjs = new List<GameObject>();
            private Sequence _masterSeq;

            public void Run(DragSource dragSource, UnitInstance mainUnit, List<Vector2> fromPositions)
            {
                StartCoroutine(AnimRoutine(dragSource, mainUnit, fromPositions));
            }

            private void OnDestroy()
            {
                TweenHelper.KillSafe(ref _masterSeq);
                foreach (var obj in _overlayObjs)
                    if (obj != null) Destroy(obj);
                _overlayObjs.Clear();
                foreach (var cg in _managedCGs)
                    if (cg != null) cg.alpha = 1f;
                _managedCGs.Clear();
                foreach (var cg in _fadeInCGs)
                    if (cg != null) { DOTween.Kill(cg.gameObject); cg.alpha = 1f; }
                _fadeInCGs.Clear();
            }

            private IEnumerator AnimRoutine(DragSource dragSource, UnitInstance mainUnit, List<Vector2> fromPositions)
            {
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
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();

                const float phase1Dur      = 0.18f;
                const float phase2Dur      = 0.16f;
                const float hoverHeight    = 70f;
                const float slingshotDur   = 0.05f;
                const float slingshotDist  = 18f;

                if (mainUnit == null) yield break;
                var cv = FindCardViewInScene(mainUnit);
                if (cv == null) yield break;

                var cvRT = cv.GetComponent<RectTransform>();
                if (cvRT == null) yield break;

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

                Vector2 fromPos = fromPositions != null && fromPositions.Count > 0 ? fromPositions[0] : finalPos;
                if (RootCanvas == null) yield break;

                cv.CancelEnterAnim();

                var cardCG = cv.GetComponent<CanvasGroup>() ?? cv.gameObject.AddComponent<CanvasGroup>();
                cardCG.alpha = 0f;
                _managedCGs.Add(cardCG);

                if (Vector2.Distance(fromPos, finalPos) < 5f)
                {
                    _fadeInCGs.Add(cardCG);
                    // fade in real card
                    cardCG.DOFade(1f, 0.15f).SetTarget(cardCG.gameObject);
                    yield break;
                }

                var overlay = Instantiate(cv.gameObject, RootCanvas.transform);
                overlay.name = "LandGhost";
                var dh2  = overlay.GetComponent<CardDragHandler>(); if (dh2  != null) Destroy(dh2);
                var btn2 = overlay.GetComponent<Button>();           if (btn2 != null) Destroy(btn2);

                var ocg = overlay.GetComponent<CanvasGroup>() ?? overlay.AddComponent<CanvasGroup>();
                ocg.alpha = 1f; ocg.blocksRaycasts = false; ocg.interactable = false;

                var oRT = overlay.GetComponent<RectTransform>();
                oRT.anchorMin  = oRT.anchorMax = new Vector2(0.5f, 0.5f);
                oRT.pivot      = new Vector2(0.5f, 0.5f);
                oRT.sizeDelta  = cvRT.rect.size;
                oRT.localScale = Vector3.one;
                oRT.localPosition = new Vector3(fromPos.x, fromPos.y, 0f);
                overlay.transform.SetAsLastSibling();
                _overlayObjs.Add(overlay);

                _masterSeq = DOTween.Sequence().SetTarget(this);
                Vector2 hover = finalPos + new Vector2(0f, hoverHeight);
                Vector2 travelDir = (hover - fromPos).normalized;
                Vector2 pullPos   = fromPos - travelDir * slingshotDist;

                _masterSeq.Append(
                    DOTween.To(() => (Vector2)oRT.localPosition,
                               v => oRT.localPosition = new Vector3(v.x, v.y, 0f),
                               pullPos, slingshotDur)
                    .SetEase(Ease.InQuad).SetTarget(oRT));
                _masterSeq.Append(
                    DOTween.To(() => (Vector2)oRT.localPosition,
                               v => oRT.localPosition = new Vector3(v.x, v.y, 0f),
                               hover, phase1Dur)
                    .SetEase(Ease.OutBack).SetTarget(oRT));
                _masterSeq.Append(
                    DOTween.To(() => (Vector2)oRT.localPosition,
                               v => oRT.localPosition = new Vector3(v.x, v.y, 0f),
                               finalPos, phase2Dur)
                    .SetEase(Ease.OutBounce).SetTarget(oRT));

                bool tweenDone = false;
                _masterSeq.OnComplete(() => tweenDone = true);
                _masterSeq.OnKill(() => tweenDone = true);
                while (!tweenDone) yield return null;

                // Reveal real card
                cardCG.alpha = 1f;
                if (overlay != null) Destroy(overlay);
                _overlayObjs.Remove(overlay);
                _managedCGs.Remove(cardCG);

                Destroy(gameObject);
            }
        }
    }
}
