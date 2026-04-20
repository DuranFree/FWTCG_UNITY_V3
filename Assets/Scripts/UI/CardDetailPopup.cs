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

            if (_artImage != null && _panel != null)
            {
                // 1. art 先挂到 overlay (_panel) 下，脱离 DetailPanel 的父子关系
                _artImage.transform.SetParent(_panel.transform, false);

                // 2. 扫 overlay 所有直接子节点，除 art/BlurBG/Dim 外全部销毁（彻底消除底座）
                var overlayT = _panel.transform;
                for (int i = overlayT.childCount - 1; i >= 0; i--)
                {
                    var child = overlayT.GetChild(i);
                    if (child == _artImage.transform) continue;
                    string n = child.name;
                    if (n == "BlurBG" || n == "Dim") continue;
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }

                // 3. art 自由定位：屏幕中心 + 固定 500×720
                var artLE = _artImage.GetComponent<LayoutElement>()
                    ?? _artImage.gameObject.AddComponent<LayoutElement>();
                artLE.ignoreLayout = true;

                var artRT = _artImage.rectTransform;
                artRT.anchorMin = new Vector2(0.5f, 0.5f);
                artRT.anchorMax = new Vector2(0.5f, 0.5f);
                artRT.pivot     = new Vector2(0.5f, 0.5f);
                artRT.anchoredPosition = Vector2.zero;
                artRT.sizeDelta = new Vector2(500f, 720f);

                _artImage.preserveAspect = true;

                // 4. 在 art 背后插一层光晕 halo（半径渐变 + 浅蓝白色）
                CreateGlowBehindArt();

                _artImage.transform.SetAsLastSibling(); // art 渲染在 glow 之上
            }

            _simplified = true;
        }

        /// <summary>在 art 背后创建一个发光光晕 Image（运行时生成半径渐变 sprite）。</summary>
        private void CreateGlowBehindArt()
        {
            if (_artImage == null || _panel == null) return;
            // 已存在则跳过
            if (_panel.transform.Find("CardGlow") != null) return;

            var glowGO = new GameObject("CardGlow");
            glowGO.transform.SetParent(_panel.transform, false);
            var img = glowGO.AddComponent<Image>();
            img.sprite = GetOrCreateRadialGlowSprite();
            img.raycastTarget = false;
            img.color = new Color(0.75f, 0.85f, 1f, 0.85f); // 浅冷白蓝

            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            // 比卡图大 1.4 倍，营造扩散感
            var artSize = _artImage.rectTransform.sizeDelta;
            rt.sizeDelta = new Vector2(artSize.x * 1.5f, artSize.y * 1.35f);

            // 插在 art 前面（render 在 art 之下）
            int artIdx = _artImage.transform.GetSiblingIndex();
            glowGO.transform.SetSiblingIndex(artIdx);
        }

        // 运行时生成的半径渐变 sprite，所有 popup 实例共用一份
        private static Sprite s_glowSprite;
        private static Sprite GetOrCreateRadialGlowSprite()
        {
            if (s_glowSprite != null) return s_glowSprite;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.wrapMode = TextureWrapMode.Clamp;
            float c = (size - 1) * 0.5f;
            float maxD = c;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / maxD; // 0..1
                    // 平滑渐变：中心不透明(0.9)，边缘透明(0)，使用 smoothstep 形状
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // smoothstep
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            s_glowSprite = Sprite.Create(tex,
                new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            s_glowSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_glowSprite;
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

                // 1/4 尺寸降采样（更柔和，消除 banding）
                int w = Mathf.Max(64, Screen.width  / 4);
                int h = Mathf.Max(64, Screen.height / 4);
                var rtA = RenderTexture.GetTemporary(w, h, 0);
                var rtB = RenderTexture.GetTemporary(w, h, 0);

                // 第一次：原始截图 → rtA（水平）→ rtB（垂直）
                Graphics.Blit(screenTex, rtA, _blurMat, 0);
                Graphics.Blit(rtA, rtB, _blurMat, 1);

                // 追加 2 轮 ping-pong 迭代，每轮 H+V 一次，总共 3 轮
                for (int i = 0; i < 2; i++)
                {
                    Graphics.Blit(rtB, rtA, _blurMat, 0);
                    Graphics.Blit(rtA, rtB, _blurMat, 1);
                }

                RenderTexture.ReleaseTemporary(rtA);
                ReleaseBlurRT();
                _currentBlurRT = rtB;
                _blurBG.texture = rtB;
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
            // BlurSize 配合 3 轮迭代 + 1/4 降采样，3f 已是明显柔模糊效果，避免过强割裂
            _blurMat.SetFloat("_BlurSize", 3f);
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
