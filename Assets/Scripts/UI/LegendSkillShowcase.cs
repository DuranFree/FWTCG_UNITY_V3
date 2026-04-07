using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// DOT-8: Fullscreen legend skill close-up.
    /// When OnLegendSkillFired fires:
    ///   1. Darken overlay fades in (0 → 0.72, 0.15s)
    ///   2. Legend card panel scales from 0.4 → 1.08 → 1 and fades in (0.4s total)
    ///   3. Holds for 0.8s
    ///   4. Panel scales out 1 → 0 and overlay fades out (0.3s)
    ///
    /// Attach to a persistent UI Canvas node. SceneBuilder wires it in.
    /// All visual elements are created lazily in Awake so the component works
    /// with no pre-built scene objects.
    /// </summary>
    public class LegendSkillShowcase : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static LegendSkillShowcase Instance { get; private set; }

        // ── Timing ─────────────────────────────────────────────────────────────
        public const float DARKEN_DUR  = 0.15f;
        public const float ZOOM_DUR    = 0.40f;
        public const float HOLD_DUR    = 0.80f;
        public const float EXIT_DUR    = 0.30f;

        // ── Optional inspector refs (can be left null — created lazily) ─────────
        [SerializeField] private Canvas        _rootCanvas;
        [SerializeField] private Image         _darkOverlay;  // full-screen darken
        [SerializeField] private RectTransform _cardPanel;    // the zoomed card container
        [SerializeField] private Text          _legendNameText;
        [SerializeField] private Text          _skillLabel;

        // ── Runtime state ──────────────────────────────────────────────────────
        private Sequence _showcaseSeq;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GameEventBus.OnLegendSkillFired += OnLegendSkillFired;
            EnsureOverlayElements();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            GameEventBus.OnLegendSkillFired -= OnLegendSkillFired;
            TweenHelper.KillSafe(ref _showcaseSeq);
            if (Instance == this) Instance = null;
        }

        // ── Event handler ──────────────────────────────────────────────────────

        private void OnLegendSkillFired(LegendInstance legend, string owner)
        {
            if (legend == null) return;
            PopulateLegendInfo(legend, owner);
            PlayShowcase();
        }

        // ── Animation ─────────────────────────────────────────────────────────

        private void PlayShowcase()
        {
            TweenHelper.KillSafe(ref _showcaseSeq);
            SetVisible(true);

            // Reset starting states
            if (_darkOverlay != null)
            {
                var c = _darkOverlay.color; c.a = 0f; _darkOverlay.color = c;
            }
            if (_cardPanel != null)
            {
                _cardPanel.localScale = Vector3.one * 0.4f;
                var cg = _cardPanel.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }

            _showcaseSeq = DOTween.Sequence().SetTarget(gameObject);

            // Phase 1: fade in darken overlay + zoom card in simultaneously
            if (_darkOverlay != null)
                _showcaseSeq.Append(_darkOverlay.DOFade(0.72f, DARKEN_DUR).SetEase(Ease.OutQuad));
            else
                _showcaseSeq.AppendInterval(DARKEN_DUR);

            if (_cardPanel != null)
            {
                var cg = _cardPanel.GetComponent<CanvasGroup>();
                _showcaseSeq.Join(
                    _cardPanel.DOScale(1.08f, ZOOM_DUR * 0.75f).SetEase(Ease.OutBack));
                _showcaseSeq.Join(
                    _cardPanel.DOScale(1f, ZOOM_DUR * 0.25f).SetEase(Ease.InOutQuad).SetDelay(ZOOM_DUR * 0.75f));
                if (cg != null)
                    _showcaseSeq.Join(cg.DOFade(1f, ZOOM_DUR * 0.5f).SetEase(Ease.OutQuad));
            }

            // Phase 2: hold
            _showcaseSeq.AppendInterval(HOLD_DUR);

            // Phase 3: fade out overlay + scale card out
            if (_darkOverlay != null)
                _showcaseSeq.Append(_darkOverlay.DOFade(0f, EXIT_DUR).SetEase(Ease.InQuad));
            else
                _showcaseSeq.AppendInterval(EXIT_DUR);

            if (_cardPanel != null)
            {
                _showcaseSeq.Join(_cardPanel.DOScale(0f, EXIT_DUR).SetEase(Ease.InBack));
                var cg = _cardPanel.GetComponent<CanvasGroup>();
                if (cg != null)
                    _showcaseSeq.Join(cg.DOFade(0f, EXIT_DUR * 0.6f).SetEase(Ease.InQuad));
            }

            _showcaseSeq.OnComplete(() =>
            {
                SetVisible(false);
                _showcaseSeq = null;
            });
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void PopulateLegendInfo(LegendInstance legend, string owner)
        {
            string ownerLabel = owner == FWTCG.Core.GameRules.OWNER_PLAYER ? "玩家" : "对手";
            if (_legendNameText != null)
                _legendNameText.text = legend.Name ?? "传奇";
            if (_skillLabel != null)
                _skillLabel.text = $"[{ownerLabel}] 发动技能";
        }

        private void SetVisible(bool visible)
        {
            if (_darkOverlay != null)  _darkOverlay.gameObject.SetActive(visible);
            if (_cardPanel   != null)  _cardPanel.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Creates overlay elements lazily on the root canvas if they were not
        /// pre-assigned in the inspector.
        /// </summary>
        private void EnsureOverlayElements()
        {
            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>();
            if (_rootCanvas == null) return;

            var canvasRT = _rootCanvas.GetComponent<RectTransform>();

            // ── Dark overlay ──────────────────────────────────────────────────
            if (_darkOverlay == null)
            {
                var overlayGO = new GameObject("LegendDarken");
                overlayGO.transform.SetParent(_rootCanvas.transform, false);
                var oRT = overlayGO.AddComponent<RectTransform>();
                oRT.anchorMin = Vector2.zero; oRT.anchorMax = Vector2.one;
                oRT.offsetMin = oRT.offsetMax = Vector2.zero;
                _darkOverlay = overlayGO.AddComponent<Image>();
                _darkOverlay.color = new Color(0f, 0f, 0f, 0f);
                _darkOverlay.raycastTarget = true; // block clicks during showcase
                overlayGO.SetActive(false);
            }

            // ── Card panel ────────────────────────────────────────────────────
            if (_cardPanel == null)
            {
                var panelGO = new GameObject("LegendCardPanel");
                panelGO.transform.SetParent(_rootCanvas.transform, false);
                _cardPanel = panelGO.AddComponent<RectTransform>();
                _cardPanel.anchorMin = _cardPanel.anchorMax = new Vector2(0.5f, 0.5f);
                _cardPanel.pivot     = new Vector2(0.5f, 0.5f);
                _cardPanel.sizeDelta = new Vector2(200f, 280f);

                var cg = panelGO.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;

                // Background card body
                var bg = panelGO.AddComponent<Image>();
                bg.color = new Color(0.12f, 0.10f, 0.20f, 0.95f);

                // Gold border
                var borderGO = new GameObject("Border");
                borderGO.transform.SetParent(panelGO.transform, false);
                var bRT = borderGO.AddComponent<RectTransform>();
                bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
                bRT.offsetMin = new Vector2(-3, -3); bRT.offsetMax = new Vector2(3, 3);
                var bImg = borderGO.AddComponent<Image>();
                bImg.color = new Color(0.78f, 0.67f, 0.43f, 0.9f);
                borderGO.transform.SetSiblingIndex(0);

                // Legend name text
                var nameGO = new GameObject("LegendName");
                nameGO.transform.SetParent(panelGO.transform, false);
                var nRT = nameGO.AddComponent<RectTransform>();
                nRT.anchorMin = new Vector2(0f, 0.65f); nRT.anchorMax = Vector2.one;
                nRT.offsetMin = new Vector2(8, 4); nRT.offsetMax = new Vector2(-8, -4);
                _legendNameText = nameGO.AddComponent<Text>();
                _legendNameText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _legendNameText.fontSize  = 22;
                _legendNameText.fontStyle = FontStyle.Bold;
                _legendNameText.alignment = TextAnchor.MiddleCenter;
                _legendNameText.color     = new Color(1f, 0.92f, 0.55f, 1f);

                // Skill label text
                var skillGO = new GameObject("SkillLabel");
                skillGO.transform.SetParent(panelGO.transform, false);
                var sRT = skillGO.AddComponent<RectTransform>();
                sRT.anchorMin = Vector2.zero; sRT.anchorMax = new Vector2(1f, 0.35f);
                sRT.offsetMin = new Vector2(8, 4); sRT.offsetMax = new Vector2(-8, -4);
                _skillLabel = skillGO.AddComponent<Text>();
                _skillLabel.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _skillLabel.fontSize  = 16;
                _skillLabel.alignment = TextAnchor.MiddleCenter;
                _skillLabel.color     = new Color(0.85f, 0.85f, 0.95f, 1f);

                panelGO.SetActive(false);
            }
        }
    }
}
