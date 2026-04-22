using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-30 F2: Spell Duel overlay UI.
    ///
    /// Subscribes to GameEventBus.OnDuelBanner → ShowDuelOverlay().
    /// Subscribes to GameEventBus.OnClearBanners → HideDuelOverlay().
    ///
    /// When shown:
    ///   - 4px pulse border on all screen edges (spell blue)
    ///   - Top-center panel: countdown label + "⚡ 法术对决" text
    ///   - Horizontal progress bar below the panel (scaleX 1→0 over 30s)
    ///   - After 30s: auto-calls ReactiveWindowUI.Instance.AutoSkipReaction()
    ///
    /// All created objects are Destroyed on HideDuelOverlay.
    ///
    /// DOT-3: BorderPulseLoop/CountdownRoutine coroutines → DOTween.
    /// </summary>
    public class SpellDuelUI : MonoBehaviour
    {
        public static SpellDuelUI Instance { get; private set; }

        /// <summary>True while the duel overlay is visible. Used in tests.</summary>
        public bool IsShowing { get; private set; }

        private const float DUEL_TIMEOUT = 30f;

        private Canvas     _rootCanvas;       // cached in Awake, avoids FindObjectsOfType each call
        private GameObject _overlay;
        private Image[]    _borders;          // [0]=Top [1]=Bottom [2]=Left [3]=Right
        private Text       _countdownText;
        private Image      _countdownBar;

        // ── DOTween state ─────────────────────────────────────────────────────
        private Tween _borderPulseTween;
        private Tween _countdownTween;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Duplicate instance guard: if another SpellDuelUI already exists, destroy self
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _rootCanvas = FindRootCanvas();
            GameEventBus.OnDuelBanner   += ShowDuelOverlay;
            GameEventBus.OnClearBanners += HideDuelOverlay;
        }

        private void OnDestroy()
        {
            KillTweens();
            if (Instance == this) Instance = null;
            GameEventBus.OnDuelBanner   -= ShowDuelOverlay;
            GameEventBus.OnClearBanners -= HideDuelOverlay;
            DestroyOverlay();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void ShowDuelOverlay()
        {
            if (!this) return; // guard: destroyed object may still hold a delegate reference
            if (IsShowing) return;
            IsShowing = true;

            // SpellDuel 和 SpellShowcase 是互斥的全屏 overlay — 开 duel 时
            // 强制关掉 showcase，避免同时显示导致输入锁死 / 视觉叠加
            SpellShowcaseUI.Instance?.ForceHide();

            Canvas root = _rootCanvas != null ? _rootCanvas : FindRootCanvas();
            if (root == null) return;

            BuildOverlay(root);
            StartBorderPulse();
            StartCountdown();
        }

        public void HideDuelOverlay()
        {
            if (!this) return; // guard: destroyed object may still hold a delegate reference
            if (!IsShowing) return;
            IsShowing = false;
            KillTweens();
            DestroyOverlay();
        }

        // ── Build overlay ─────────────────────────────────────────────────────

        private void BuildOverlay(Canvas root)
        {
            _overlay = new GameObject("SpellDuelOverlay");
            _overlay.transform.SetParent(root.transform, false);

            var overlayRT = _overlay.AddComponent<RectTransform>();
            overlayRT.anchorMin        = Vector2.zero;
            overlayRT.anchorMax        = Vector2.one;
            overlayRT.sizeDelta        = Vector2.zero;
            overlayRT.anchoredPosition = Vector2.zero;

            var bg = _overlay.AddComponent<Image>();
            bg.color        = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = false;

            BuildBorders();
            BuildCountdownPanel();
        }

        private void BuildBorders()
        {
            // 4px borders along each edge, spell-blue pulse color
            var color = new Color(0.10f, 0.55f, 1.00f, 0.7f);

            //                      anchorMin             anchorMax         sizeDelta
            var defs = new (Vector2 aMin, Vector2 aMax, Vector2 sz, string name)[]
            {
                (new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 4f), "Border_Top"),
                (new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), "Border_Bot"),
                (new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(4f, 0f), "Border_Left"),
                (new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), "Border_Right"),
            };

            _borders = new Image[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var go = new GameObject(defs[i].name);
                go.transform.SetParent(_overlay.transform, false);

                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin        = defs[i].aMin;
                rt.anchorMax        = defs[i].aMax;
                rt.sizeDelta        = defs[i].sz;
                rt.anchoredPosition = Vector2.zero;

                var img = go.AddComponent<Image>();
                img.color        = color;
                img.raycastTarget = false;
                _borders[i] = img;
            }
        }

        private void BuildCountdownPanel()
        {
            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ── Header panel ─────────────────────────────────────────────────
            var panel = new GameObject("DuelCountdownPanel");
            panel.transform.SetParent(_overlay.transform, false);

            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin        = new Vector2(0.5f, 1f);
            panelRT.anchorMax        = new Vector2(0.5f, 1f);
            panelRT.sizeDelta        = new Vector2(200f, 34f);
            panelRT.anchoredPosition = new Vector2(0f, -18f);

            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.04f, 0.10f, 0.22f, 0.88f);
            panelBg.raycastTarget = false;

            // ── Countdown text (left half of panel) ──────────────────────────
            var ctGO = new GameObject("CountdownNum");
            ctGO.transform.SetParent(panel.transform, false);
            var ctRT = ctGO.AddComponent<RectTransform>();
            ctRT.anchorMin        = new Vector2(0.0f, 0f);
            ctRT.anchorMax        = new Vector2(0.3f, 1f);
            ctRT.sizeDelta        = Vector2.zero;
            ctRT.anchoredPosition = Vector2.zero;
            _countdownText = ctGO.AddComponent<Text>();
            _countdownText.text      = Mathf.CeilToInt(DUEL_TIMEOUT).ToString();
            _countdownText.alignment = TextAnchor.MiddleCenter;
            _countdownText.color     = new Color(1f, 0.85f, 0.25f, 1f);
            _countdownText.fontSize  = 20;
            _countdownText.font      = fallback;
            _countdownText.raycastTarget = false;

            // ── Label text (right portion of panel) ──────────────────────────
            var labelGO = new GameObject("DuelLabel");
            labelGO.transform.SetParent(panel.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin        = new Vector2(0.3f, 0f);
            labelRT.anchorMax        = new Vector2(1.0f, 1f);
            labelRT.sizeDelta        = Vector2.zero;
            labelRT.anchoredPosition = Vector2.zero;
            var labelTxt = labelGO.AddComponent<Text>();
            labelTxt.text      = "⚡ 法术对决";
            labelTxt.alignment = TextAnchor.MiddleCenter;
            labelTxt.color     = new Color(0.55f, 0.85f, 1f, 1f);
            labelTxt.fontSize  = 14;
            labelTxt.font      = fallback;
            labelTxt.raycastTarget = false;

            // ── Progress bar background ───────────────────────────────────────
            var barBgGO = new GameObject("DuelBarBg");
            barBgGO.transform.SetParent(_overlay.transform, false);
            var barBgRT = barBgGO.AddComponent<RectTransform>();
            barBgRT.anchorMin        = new Vector2(0.30f, 1f);
            barBgRT.anchorMax        = new Vector2(0.70f, 1f);
            barBgRT.sizeDelta        = new Vector2(0f, 6f);
            barBgRT.anchoredPosition = new Vector2(0f, -56f);
            barBgGO.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.28f, 0.85f);

            // ── Progress bar fill (pivot=left so localScale.x shrinks rightward) ──
            var barFillGO = new GameObject("DuelBarFill");
            barFillGO.transform.SetParent(barBgGO.transform, false);
            var barFillRT = barFillGO.AddComponent<RectTransform>();
            barFillRT.anchorMin        = Vector2.zero;
            barFillRT.anchorMax        = Vector2.one;
            barFillRT.sizeDelta        = Vector2.zero;
            barFillRT.anchoredPosition = Vector2.zero;
            barFillRT.pivot            = new Vector2(0f, 0.5f);
            _countdownBar = barFillGO.AddComponent<Image>();
            _countdownBar.color        = new Color(0.25f, 0.65f, 1f, 0.90f);
            _countdownBar.raycastTarget = false;
        }

        // ── DOTween animations ────────────────────────────────────────────────

        private void StartBorderPulse()
        {
            // Pulse alpha 0.4↔0.9 with period 1.5s (sine wave), unscaled time
            _borderPulseTween = DOVirtual.Float(0.4f, 0.9f, 0.75f, alpha =>
            {
                if (_borders == null) return;
                for (int i = 0; i < _borders.Length; i++)
                    if (_borders[i] != null)
                    {
                        var c = _borders[i].color; c.a = alpha;
                        _borders[i].color = c;
                    }
            })
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(gameObject);
        }

        private void StartCountdown()
        {
            // Count down from DUEL_TIMEOUT to 0 over DUEL_TIMEOUT seconds, unscaled time
            _countdownTween = DOVirtual.Float(DUEL_TIMEOUT, 0f, DUEL_TIMEOUT, remaining =>
            {
                float frac = Mathf.Clamp01(remaining / DUEL_TIMEOUT);
                if (_countdownText != null)
                    _countdownText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
                if (_countdownBar != null)
                    _countdownBar.rectTransform.localScale = new Vector3(frac, 1f, 1f);
            })
            .SetEase(Ease.Linear)
            .SetTarget(gameObject)
            .OnComplete(() =>
            {
                _countdownTween = null;
                HideDuelOverlay();
                ReactiveWindowUI.Instance?.AutoSkipReaction();
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void KillTweens()
        {
            TweenHelper.KillSafe(ref _borderPulseTween);
            TweenHelper.KillSafe(ref _countdownTween);
        }

        private void DestroyOverlay()
        {
            _borders       = null;
            _countdownText = null;
            _countdownBar  = null;
            if (_overlay != null) { SafeDestroy(_overlay); _overlay = null; }
        }

        // Safe destroy: uses DestroyImmediate in Edit Mode (tests), Destroy at runtime.
        private static void SafeDestroy(Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) { DestroyImmediate(obj); return; }
#endif
            Destroy(obj);
        }

        private static Canvas FindRootCanvas()
        {
            Canvas best = null;
            int bestOrder = int.MinValue;
            foreach (var c in FindObjectsOfType<Canvas>())
            {
                if (c.isRootCanvas && c.sortingOrder >= bestOrder)
                {
                    best = c;
                    bestOrder = c.sortingOrder;
                }
            }
            return best;
        }
    }
}
