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

        // Dissolve + sparks to match SpellShowcaseUI's visual feel
        private Image _cardBgImage;
        private Material _dissolveMat;
        private RectTransform _sparksRoot;

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
            if (_dissolveMat != null)
            {
                if (_cardBgImage != null && _cardBgImage.material == _dissolveMat)
                    _cardBgImage.material = null;
                Destroy(_dissolveMat);
                _dissolveMat = null;
            }
            if (Instance == this) Instance = null;
        }

        // ── Event handler ──────────────────────────────────────────────────────

        private void OnLegendSkillFired(LegendInstance legend, string owner)
        {
            if (legend == null) return;
            PopulateLegendInfo(legend, owner);
            // Direction matches SpellShowcaseUI convention: player burns bottom→top, opponent top→bottom
            bool isPlayer = owner == FWTCG.Core.GameRules.OWNER_PLAYER;
            SpellDissolveFX.SetDirection(_dissolveMat, isPlayer);
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
                // M-1: nested sub-sequence guarantees 0.4→1.08→1 correctly chains (Append, not Join+SetDelay)
                var scaleSeq = DOTween.Sequence().SetTarget(gameObject);
                scaleSeq.Append(_cardPanel.DOScale(1.08f, ZOOM_DUR * 0.75f).SetEase(Ease.OutBack));
                scaleSeq.Append(_cardPanel.DOScale(1f,    ZOOM_DUR * 0.25f).SetEase(Ease.InOutQuad));
                _showcaseSeq.Join(scaleSeq);
                if (cg != null)
                    _showcaseSeq.Join(cg.DOFade(1f, ZOOM_DUR * 0.5f).SetEase(Ease.OutQuad));
            }

            // Phase 2: hold
            _showcaseSeq.AppendInterval(HOLD_DUR);

            // Phase 3: magical dissolve + spark burst — bot 高速下跳过（KillAll 打断留残影 magenta）
            bool useDissolve = _dissolveMat != null && _cardBgImage != null
                               && FWTCG.Core.GameTiming.SpeedMultiplier <= 10f;
            if (useDissolve)
            {
                SpellDissolveFX.ResetAmount(_dissolveMat);
                // Legend skill treated as "player-side cast" direction by default; callers pass owner via PopulateLegendInfo.
                // Direction is set each time in OnLegendSkillFired before PlayShowcase runs.
                _showcaseSeq.AppendCallback(() =>
                {
                    if (_sparksRoot != null && _cardPanel != null)
                    {
                        // bot 高速运行时大幅减产，避免 tween 爆量
                        int n = FWTCG.Core.GameTiming.SpeedMultiplier > 10f ? 10 : 60;
                        SpellDissolveFX.BurstSparks(_sparksRoot, n, _cardPanel.sizeDelta.x, _cardPanel.sizeDelta.y);
                    }
                });
                _showcaseSeq.Append(SpellDissolveFX.TweenAmount(_dissolveMat, 1.12f, EXIT_DUR * 2.2f));
                if (_cardPanel != null)
                    _showcaseSeq.Join(_cardPanel.DOScale(1.04f, EXIT_DUR * 2.2f).SetEase(Ease.InOutSine));
                _showcaseSeq.InsertCallback(DARKEN_DUR + ZOOM_DUR + HOLD_DUR + EXIT_DUR * 0.9f, () =>
                {
                    if (_sparksRoot != null && _cardPanel != null)
                    {
                        int n = FWTCG.Core.GameTiming.SpeedMultiplier > 10f ? 5 : 30;
                        SpellDissolveFX.BurstSparks(_sparksRoot, n, _cardPanel.sizeDelta.x, _cardPanel.sizeDelta.y);
                    }
                });
                if (_darkOverlay != null)
                    _showcaseSeq.Append(_darkOverlay.DOFade(0f, EXIT_DUR).SetEase(Ease.InQuad));
                var cg = _cardPanel != null ? _cardPanel.GetComponent<CanvasGroup>() : null;
                if (cg != null)
                    _showcaseSeq.Join(cg.DOFade(0f, EXIT_DUR * 0.8f).SetEase(Ease.InQuad));
            }
            else
            {
                // Shader unavailable — preserve original scale-out fallback
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
            }

            _showcaseSeq.OnComplete(() =>
            {
                // Reset scale for next showcase
                if (_cardPanel != null) _cardPanel.localScale = Vector3.one;
                SetVisible(false);
                _showcaseSeq = null;
            });
        }

        /// <summary>
        /// 强制关闭 showcase。RestartGameInPlace / DOTween.KillAll 之后手动调，
        /// 避免 OnComplete 没 fire 导致面板永远 SetActive(true) 卡屏。
        /// </summary>
        public void ForceHide()
        {
            TweenHelper.KillSafe(ref _showcaseSeq);
            if (_cardPanel != null) _cardPanel.localScale = Vector3.one;
            if (_dissolveMat != null) SpellDissolveFX.ResetAmount(_dissolveMat);
            SetVisible(false);
            // 清空遗留 sparks
            if (_sparksRoot != null)
            {
                for (int i = _sparksRoot.childCount - 1; i >= 0; i--)
                    Destroy(_sparksRoot.GetChild(i).gameObject);
            }
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
                _cardBgImage = panelGO.AddComponent<Image>();
                _cardBgImage.color = new Color(0.12f, 0.10f, 0.20f, 0.95f);

                // Attach dissolve material (falls back to plain if shader missing)
                _dissolveMat = SpellDissolveFX.CreateDissolveMaterial();
                if (_dissolveMat != null)
                    _cardBgImage.material = _dissolveMat;

                // Sparks root — sibling of card panel for uncoupled transform
                var sparksGO = new GameObject("LegendSkillSparks");
                sparksGO.transform.SetParent(_rootCanvas.transform, false);
                _sparksRoot = sparksGO.AddComponent<RectTransform>();
                _sparksRoot.anchorMin = _sparksRoot.anchorMax = new Vector2(0.5f, 0.5f);
                _sparksRoot.pivot = new Vector2(0.5f, 0.5f);
                _sparksRoot.anchoredPosition = Vector2.zero;
                _sparksRoot.sizeDelta = _cardPanel.sizeDelta;
                _sparksRoot.SetAsLastSibling();

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
