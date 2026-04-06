using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Systems;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-18: Plays a shockwave effect on the relevant battlefield panel
    /// whenever CombatSystem.OnCombatResult fires.
    ///
    /// The shockwave is a radial Image (circle sprite) that scales from 0 → 1.5
    /// while fading from opaque → transparent over 0.45s.
    ///
    /// Assign _bf1Panel and _bf2Panel to the two BF root GameObjects.
    /// The shockwave Image is a child created at runtime so nothing needs to be
    /// pre-wired in the scene — SceneBuilder calls Init() after building.
    /// </summary>
    public class CombatAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform _bf1Panel;
        [SerializeField] private RectTransform _bf2Panel;

        // Shockwave parameters
        private const float SHOCKWAVE_DURATION  = 0.45f;
        private const float SHOCKWAVE_END_SCALE = 1.5f;
        private static readonly Color SHOCKWAVE_COLOR_GOLD = new Color(0.78f, 0.67f, 0.43f, 0.80f);
        private static readonly Color SHOCKWAVE_COLOR_CYAN = new Color(0.04f, 0.78f, 0.72f, 0.50f);

        // Runtime shockwave images (one per BF, created in Start)
        private Image _shockwave1;
        private Image _shockwave2;

        // DOT-4: tween handles (replaced DEV-26 coroutine handles)
        private Tween _sw1Tween;
        private Tween _sw2Tween;

        // DEV-29: track active fly ghosts so they can be cleaned up in OnDestroy
        private readonly List<GameObject> _activeGhosts = new List<GameObject>();

        // VFX-7n: 3-phase flight animation constants (replaces DEV-28 2-phase)
        public const float FLY_DURATION   = 0.30f;  // phase 1: lunge toward enemy
        public const float PAUSE_DURATION = 0.10f;  // phase 2: impact hold
        public const float BACK_DURATION  = 0.30f;  // phase 3: rebound
        public const float FLY_OFFSET     = 40f;    // px toward enemy side (was 28)

        private void Awake()
        {
            CombatSystem.OnCombatResult    += OnCombatResult;
            CombatSystem.OnCombatWillStart += OnCombatWillStart;
        }

        private void OnDestroy()
        {
            CombatSystem.OnCombatResult    -= OnCombatResult;
            CombatSystem.OnCombatWillStart -= OnCombatWillStart;
            TweenHelper.KillSafe(ref _sw1Tween);
            TweenHelper.KillSafe(ref _sw2Tween);
            // DEV-29: destroy any fly ghosts that are mid-animation when the component is torn down
            foreach (var ghost in _activeGhosts)
            {
                if (ghost == null) continue;
                DOTween.Kill(ghost); // DOT-4: kill any tweens targeting this ghost
                // Use DestroyImmediate in EditMode (e.g. tests); Destroy at runtime
                if (Application.isPlaying) Destroy(ghost);
                else DestroyImmediate(ghost);
            }
            _activeGhosts.Clear();
        }

        private void Start()
        {
            _shockwave1 = CreateShockwave(_bf1Panel);
            _shockwave2 = CreateShockwave(_bf2Panel);
        }

        /// <summary>Called from SceneBuilder after panels are assigned.</summary>
        public void Init(RectTransform bf1, RectTransform bf2)
        {
            _bf1Panel = bf1;
            _bf2Panel = bf2;
        }

        // ── Private ───────────────────────────────────────────────────────────

        // DEV-28: flight VFX — ghost overlays lunge toward enemy then snap back
        private void OnCombatWillStart(int bfIdx, List<UnitInstance> attackers, List<UnitInstance> defenders)
        {
            if (GameUI.Instance == null) return;
            var canvas = GameUI.Instance.GetComponentInParent<Canvas>()
                      ?? GameUI.Instance.GetComponent<Canvas>();
            if (canvas == null) return;

            // Animate up to 2 units per side (avoid flooding the screen)
            AnimateFlyGroup(attackers, defenders, canvas, flyRight: true);
            AnimateFlyGroup(defenders, attackers, canvas, flyRight: false);
        }

        private void AnimateFlyGroup(List<UnitInstance> units, List<UnitInstance> enemies,
                                     Canvas canvas, bool flyRight)
        {
            int count = Mathf.Min(units.Count, 2);
            for (int i = 0; i < count; i++)
            {
                var cv = GameUI.Instance?.FindCardView(units[i]);
                if (cv == null) continue;
                FlyAndReturn(cv.GetComponent<RectTransform>(), canvas, flyRight);
            }
        }

        /// <summary>DOT-4: 3-phase flight animation using DOTween Sequence (replaced coroutine).</summary>
        private void FlyAndReturn(RectTransform rt, Canvas canvas, bool flyRight)
        {
            if (rt == null) return;

            // Create ghost overlay so we don't disturb the real card's layout
            var host = new GameObject("CombatFlyGhost");
            _activeGhosts.Add(host); // DEV-29: track for OnDestroy cleanup
            host.transform.SetParent(canvas.transform, false);
            var ghostRT = host.AddComponent<RectTransform>();
            ghostRT.sizeDelta  = rt.rect.size;
            ghostRT.anchorMin  = ghostRT.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRT.pivot      = new Vector2(0.5f, 0.5f);

            // Copy world position to canvas-local
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 screenCenter = new Vector2(
                (corners[0].x + corners[2].x) * 0.5f,
                (corners[0].y + corners[2].y) * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), screenCenter,
                canvas.worldCamera, out Vector2 origin);
            ghostRT.anchoredPosition = origin;
            host.transform.SetAsLastSibling();

            // Copy visuals from real card
            var srcImg = rt.GetComponent<Image>();
            if (srcImg != null)
            {
                var img = host.AddComponent<Image>();
                img.sprite = srcImg.sprite;
                img.color  = srcImg.color;
                img.raycastTarget = false;
            }
            var cg = host.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            // VFX-7n / DOT-4: 3-phase flight animation via DOTween Sequence
            float dir = flyRight ? 1f : -1f;
            Vector2 target = origin + new Vector2(dir * FLY_OFFSET, 0f);

            var seq = DOTween.Sequence().SetTarget(host);
            // Phase 1: lunge toward enemy
            seq.Append(ghostRT.DOAnchorPos(target, FLY_DURATION).SetEase(Ease.OutQuad));
            // Phase 2: impact hold
            seq.AppendInterval(PAUSE_DURATION);
            // Phase 3: rebound
            seq.Append(ghostRT.DOAnchorPos(origin, BACK_DURATION).SetEase(Ease.InOutQuad));
            // Cleanup
            seq.OnComplete(() =>
            {
                _activeGhosts.Remove(host);
                Destroy(host);
            });
        }

        private void OnCombatResult(CombatSystem.CombatResult result)
        {
            int bfIdx = result.BFIndex; // DEV-26: use explicit index instead of string parsing
            if (bfIdx == 0)
            {
                TweenHelper.KillSafe(ref _sw1Tween);
                if (_shockwave1 != null) _sw1Tween = PlayShockwave(_shockwave1);
            }
            else
            {
                TweenHelper.KillSafe(ref _sw2Tween);
                if (_shockwave2 != null) _sw2Tween = PlayShockwave(_shockwave2);
            }
        }

        /// <summary>DOT-4: Shockwave scale+fade via DOTween Sequence (replaced coroutine).</summary>
        private Tween PlayShockwave(Image img)
        {
            if (img == null) return null;

            RectTransform rt = img.rectTransform;
            rt.localScale = Vector3.zero;
            img.color = SHOCKWAVE_COLOR_GOLD;
            img.gameObject.SetActive(true);

            var seq = DOTween.Sequence().SetTarget(img);
            seq.Append(rt.DOScale(SHOCKWAVE_END_SCALE, SHOCKWAVE_DURATION).SetEase(Ease.Linear));
            seq.Join(img.DOFade(0f, SHOCKWAVE_DURATION).SetEase(Ease.Linear));
            seq.OnComplete(() =>
            {
                img.gameObject.SetActive(false);
                rt.localScale = Vector3.zero;
                if (img == _shockwave1) _sw1Tween = null;
                else if (img == _shockwave2) _sw2Tween = null;
            });
            return seq;
        }

        private static Image CreateShockwave(RectTransform parent)
        {
            if (parent == null) return null;

            var go = new GameObject("ShockwaveRing");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 120f);

            var img = go.AddComponent<Image>();
            // Use built-in circle sprite or fall back to white quad
            Sprite circle = Resources.Load<Sprite>("UI/circle");
            if (circle != null) img.sprite = circle;
            img.color = new Color(0.78f, 0.67f, 0.43f, 0f);
            img.raycastTarget = false;

            go.SetActive(false);
            return img;
        }
    }
}
