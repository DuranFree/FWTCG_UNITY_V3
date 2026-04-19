using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.UI
{
    /// <summary>
    /// Fullscreen overlay showing an enlarged view of a card.
    /// Triggered by right-click on any visible card.
    /// Close by clicking the dimmed background or pressing Escape.
    ///
    /// The popup now displays ONLY the card sprite centered against a blurred backdrop.
    /// No info panel / chips / keyword badges (all legacy UI retained in scene is hidden at runtime).
    /// </summary>
    public class CardDetailPopup : MonoBehaviour
    {
        // ── Scene-wired fields (kept for backward-compat with SceneBuilder wiring) ──────
        [SerializeField] private GameObject _panel;
        [SerializeField] private Image _artImage;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _costText;
        [SerializeField] private Text _atkText;
        [SerializeField] private Text _keywordsText;
        [SerializeField] private Text _effectText;
        [SerializeField] private Text _stateText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private RawImage _blurBG;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _costChipText;
        [SerializeField] private Text _runeChipText;
        [SerializeField] private Text _typeChipText;
        [SerializeField] private GameObject _runeChip;
        [SerializeField] private GameObject _typeChip;
        [SerializeField] private RectTransform _keywordRow;
        [SerializeField] private Text _footerTypeText;

        private const float FADE_IN_DURATION  = 0.12f;
        private const float FADE_OUT_DURATION = 0.10f;

        private Material _blurMat;
        private RenderTexture _currentBlurRT;
        private Tween _fadeTween;
        private bool _simplified;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);
            if (_panel != null)
                _panel.SetActive(false);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Hide);
            TweenHelper.KillSafe(ref _fadeTween);
            if (_panel != null) DOTween.Kill(_panel);
            ReleaseBlurRT();
            if (_blurMat != null) Destroy(_blurMat);
        }

        private void Update()
        {
            if (_panel != null && _panel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        public bool IsVisible => _panel != null && _panel.activeSelf;

        public void Show(UnitInstance unit)
        {
            if (unit == null || _panel == null) return;
            ApplySpriteOnly(unit.CardData?.ArtSprite);
            BeginShow();
        }

        /// <summary>Show detail for a non-unit item (battlefield, legend, etc).</summary>
        public void ShowSimple(string cardName, string description, Sprite art = null)
        {
            if (_panel == null) return;
            ApplySpriteOnly(art);
            BeginShow();
        }

        public void Hide()
        {
            if (_panel == null) return;
            TweenHelper.KillSafe(ref _fadeTween);
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;

            bool panelVisible = _panel.activeSelf;
            if (_canvasGroup != null && gameObject.activeInHierarchy && panelVisible)
            {
                _fadeTween = _canvasGroup.DOFade(0f, FADE_OUT_DURATION)
                    .SetEase(Ease.InQuad)
                    .SetUpdate(true)
                    .SetTarget(_panel)
                    .OnComplete(() =>
                    {
                        if (_panel != null) _panel.SetActive(false);
                        ReleaseBlurRT();
                    });
            }
            else
            {
                _panel.SetActive(false);
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                ReleaseBlurRT();
            }
        }

        // ── Internal ────────────────────────────────────────────────────────────────────

        private void ApplySpriteOnly(Sprite art)
        {
            SimplifyToArtOnly();
            if (_artImage != null)
            {
                if (art != null) { _artImage.sprite = art; _artImage.enabled = true; }
                else _artImage.enabled = false;
            }
        }

        /// <summary>
        /// 首次 Show 时隐藏所有 legacy 信息面板子节点，让 popup 只展示卡图。
        /// 同时去掉 Panel 背景 / 金色外描边 / 纹理覆盖层。
        /// </summary>
        private void SimplifyToArtOnly()
        {
            if (_simplified) return;

            if (_nameText       != null) _nameText.text       = "";
            if (_costText       != null) _costText.text       = "";
            if (_atkText        != null) _atkText.text        = "";
            if (_keywordsText   != null) _keywordsText.text   = "";
            if (_effectText     != null) _effectText.text     = "";
            if (_stateText      != null) _stateText.text      = "";
            if (_costChipText   != null) _costChipText.text   = "";
            if (_runeChipText   != null) _runeChipText.text   = "";
            if (_typeChipText   != null) _typeChipText.text   = "";
            if (_footerTypeText != null) _footerTypeText.text = "";

            if (_runeChip   != null) _runeChip.SetActive(false);
            if (_typeChip   != null) _typeChip.SetActive(false);
            if (_keywordRow != null) _keywordRow.gameObject.SetActive(false);

            if (_artImage != null)
            {
                var infoColumn = _artImage.transform.parent != null
                    ? _artImage.transform.parent.Find("InfoColumn") : null;
                if (infoColumn != null) infoColumn.gameObject.SetActive(false);

                var panelRT = _artImage.transform.parent != null && _artImage.transform.parent.parent != null
                    ? _artImage.transform.parent.parent as RectTransform : null;
                if (panelRT != null)
                {
                    panelRT.sizeDelta = new Vector2(520f, 720f);
                    var panelImg = panelRT.GetComponent<Image>();
                    if (panelImg != null) panelImg.color = new Color(0, 0, 0, 0);
                    var panelOutline = panelRT.GetComponent<Outline>();
                    if (panelOutline != null) panelOutline.enabled = false;
                    var texChild = panelRT.Find("PanelTexture");
                    if (texChild != null) texChild.gameObject.SetActive(false);
                }

                var artLE = _artImage.GetComponent<LayoutElement>();
                if (artLE != null)
                {
                    artLE.preferredWidth = 500f;
                    artLE.minWidth       = 500f;
                    artLE.flexibleWidth  = 1f;
                    artLE.flexibleHeight = 1f;
                }
            }

            _simplified = true;
        }

        private void BeginShow()
        {
            if (!isActiveAndEnabled || _blurBG == null)
            {
                _panel.SetActive(true);
                return;
            }
            StopAllCoroutines();
            StartCoroutine(CaptureBlurAndShow());
        }

        private IEnumerator CaptureBlurAndShow()
        {
            _panel.SetActive(false);
            if (_blurBG != null) _blurBG.enabled = false;
            yield return new WaitForEndOfFrame();

            EnsureBlurMaterial();
            if (_blurMat != null && _blurBG != null)
            {
                var screenTex = ScreenCapture.CaptureScreenshotAsTexture();
                int w = Mathf.Max(64, Screen.width  / 2);
                int h = Mathf.Max(64, Screen.height / 2);
                var rt1 = RenderTexture.GetTemporary(w, h, 0);
                var rt2 = RenderTexture.GetTemporary(w, h, 0);
                Graphics.Blit(screenTex, rt1, _blurMat, 0);
                Graphics.Blit(rt1, rt2, _blurMat, 1);
                RenderTexture.ReleaseTemporary(rt1);
                ReleaseBlurRT();
                _currentBlurRT = rt2;
                _blurBG.texture = rt2;
                _blurBG.enabled = true;
                Destroy(screenTex);
            }

            TweenHelper.KillSafe(ref _fadeTween);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = true;
            }
            _panel.SetActive(true);
            if (_canvasGroup != null)
            {
                _fadeTween = _canvasGroup.DOFade(1f, FADE_IN_DURATION)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetTarget(_panel);
            }
        }

        private void EnsureBlurMaterial()
        {
            if (_blurMat != null) return;
            var sh = Shader.Find("FWTCG/UIBlur");
            if (sh == null)
            {
                Debug.LogWarning("[CardDetailPopup] Shader 'FWTCG/UIBlur' not found — blur disabled.");
                return;
            }
            _blurMat = new Material(sh) { hideFlags = HideFlags.DontSave };
            _blurMat.SetFloat("_BlurSize", 3.5f);
        }

        private void ReleaseBlurRT()
        {
            if (_currentBlurRT != null)
            {
                RenderTexture.ReleaseTemporary(_currentBlurRT);
                _currentBlurRT = null;
            }
            if (_blurBG != null) _blurBG.texture = null;
        }
    }
}
