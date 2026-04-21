using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.Demo
{
    /// <summary>
    /// 法术牌施放 + 魔力溶解 demo。进 Play Mode 后按键盘 1 触发。
    /// - 自动在 AfterSceneLoad 时注入一个 DontDestroyOnLoad 的 GameObject
    /// - 使用 FWTCG/UIDissolve shader 做溶解，Image 自绘粒子做火星散
    /// - 不依赖游戏状态，独立播放一次完整序列
    /// </summary>
    public class SpellCastDemo : MonoBehaviour
    {
        [Header("Timing (seconds)")]
        [SerializeField] private float _flyInDuration = 0.35f;
        [SerializeField] private float _holdDuration = 0.6f;
        [SerializeField] private float _dissolveDuration = 0.73f;
        [SerializeField] private float _fadeOutDuration = 0.23f;

        [Header("Look")]
        [SerializeField] private float _cardWidth = 380f;
        [SerializeField] private float _cardHeight = 540f;
        [SerializeField] private Color _edgeColor = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private float _edgeGlow = 4f;
        [SerializeField] private float _edgeWidth = 0.09f;
        [SerializeField] private float _noiseScale = 3.2f;
        [SerializeField] private int _sparkCount = 70;

        [Header("Input")]
        [SerializeField] private KeyCode _triggerKey = KeyCode.Alpha1;

        [Header("Flight")]
        [SerializeField] private float _flyOffset = 420f;

        // Toggles each press: true = from bottom (player), false = from top (opponent)
        private bool _nextFromPlayer = true;

        private Canvas _canvas;
        private RectTransform _overlay;
        private CanvasGroup _overlayCg;
        private Image _dimImage;
        private Image _cardImage;
        private Material _dissolveMat;
        private RectTransform _sparksRoot;
        private Sequence _playSeq;
        private bool _playing;

        private static Sprite s_sparkSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInject()
        {
            if (FindObjectOfType<SpellCastDemo>() != null) return;
            var go = new GameObject("SpellCastDemo");
            go.AddComponent<SpellCastDemo>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            BuildOverlay();
            HideInstant();
        }

        private void Update()
        {
            if (Input.GetKeyDown(_triggerKey) && !_playing)
            {
                Play();
            }
        }

        private void OnDestroy()
        {
            if (_playSeq != null && _playSeq.IsActive()) _playSeq.Kill();
            if (_dissolveMat != null) Destroy(_dissolveMat);
            DOTween.Kill(gameObject);
        }

        private void BuildOverlay()
        {
            // Dedicated canvas with high sort order so it renders above everything
            var canvasGO = new GameObject("SpellCastDemoCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // No GraphicRaycaster — we don't receive input, to avoid blocking game UI

            var overlayGO = new GameObject("Overlay");
            _overlay = overlayGO.AddComponent<RectTransform>();
            _overlay.SetParent(_canvas.transform, false);
            _overlay.anchorMin = Vector2.zero;
            _overlay.anchorMax = Vector2.one;
            _overlay.offsetMin = Vector2.zero;
            _overlay.offsetMax = Vector2.zero;
            _overlayCg = overlayGO.AddComponent<CanvasGroup>();
            _overlayCg.blocksRaycasts = false;
            _overlayCg.interactable = false;

            // Dim layer
            var dimGO = new GameObject("Dim");
            _dimImage = dimGO.AddComponent<Image>();
            _dimImage.color = new Color(0f, 0f, 0f, 0.65f);
            _dimImage.raycastTarget = false;
            var dimRT = _dimImage.rectTransform;
            dimRT.SetParent(_overlay, false);
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero;
            dimRT.offsetMax = Vector2.zero;

            // Card image (spell art)
            var cardGO = new GameObject("SpellCard");
            _cardImage = cardGO.AddComponent<Image>();
            _cardImage.raycastTarget = false;
            var sprite = Resources.Load<Sprite>("CardArt/divine_ray");
            if (sprite != null)
            {
                _cardImage.sprite = sprite;
                _cardImage.preserveAspect = true;
            }
            else
            {
                _cardImage.color = new Color(0.25f, 0.35f, 0.6f, 1f);
            }

            var shader = Shader.Find("FWTCG/UIDissolve");
            if (shader != null)
            {
                _dissolveMat = new Material(shader) { name = "SpellCastDemo_Dissolve" };
                _dissolveMat.SetFloat("_DissolveAmount", 0f);
                _dissolveMat.SetFloat("_EdgeWidth", _edgeWidth);
                _dissolveMat.SetColor("_EdgeColor", _edgeColor);
                _dissolveMat.SetFloat("_EdgeGlow", _edgeGlow);
                _dissolveMat.SetFloat("_NoiseScale", _noiseScale);
                _dissolveMat.SetVector("_DissolveDirection", new Vector4(0f, 1f, 0.3f, 0f));
                _cardImage.material = _dissolveMat;
            }
            else
            {
                Debug.LogError("[SpellCastDemo] Shader 'FWTCG/UIDissolve' not found. Make sure UIDissolve.shader is imported.");
            }

            var cardRT = _cardImage.rectTransform;
            cardRT.SetParent(_overlay, false);
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = Vector2.zero;
            cardRT.sizeDelta = new Vector2(_cardWidth, _cardHeight);

            // Sparks root (sits above card so particles layer on top of the dissolving art)
            var sparksGO = new GameObject("Sparks");
            _sparksRoot = sparksGO.AddComponent<RectTransform>();
            _sparksRoot.SetParent(_overlay, false);
            _sparksRoot.anchorMin = _sparksRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _sparksRoot.anchoredPosition = Vector2.zero;
            _sparksRoot.sizeDelta = new Vector2(_cardWidth, _cardHeight);
        }

        private void HideInstant()
        {
            _overlayCg.alpha = 0f;
            _overlay.gameObject.SetActive(false);
        }

        private void Play()
        {
            if (_dissolveMat == null) return;
            _playing = true;
            bool fromPlayer = _nextFromPlayer;
            _nextFromPlayer = !_nextFromPlayer;

            _overlay.gameObject.SetActive(true);
            _overlayCg.alpha = 0f;
            _dissolveMat.SetFloat("_DissolveAmount", 0f);
            // Player sweep bottom→top (0,1); opponent sweep top→bottom (0,-1). Blend weight keeps a bit of noise character.
            _dissolveMat.SetVector("_DissolveDirection",
                fromPlayer ? new Vector4(0f, 1f, 0.3f, 0f) : new Vector4(0f, -1f, 0.3f, 0f));

            var cardRT = _cardImage.rectTransform;
            float startY = fromPlayer ? -_flyOffset : _flyOffset;
            cardRT.anchoredPosition = new Vector2(0f, startY);
            cardRT.localScale = Vector3.one * 0.6f;

            if (_playSeq != null && _playSeq.IsActive()) _playSeq.Kill();
            _playSeq = DOTween.Sequence().SetTarget(gameObject);

            // 1) Fly-in: overlay fades in, card scales up to slight overshoot
            _playSeq.Append(_overlayCg.DOFade(1f, _flyInDuration).SetEase(Ease.OutQuad));
            _playSeq.Join(cardRT.DOScale(1.08f, _flyInDuration).SetEase(Ease.OutBack, 1.3f));
            _playSeq.Join(cardRT.DOAnchorPos(Vector2.zero, _flyInDuration).SetEase(Ease.OutQuad));

            // 2) Settle
            _playSeq.Append(cardRT.DOScale(1.0f, 0.12f).SetEase(Ease.OutQuad));

            // 3) Hold
            _playSeq.AppendInterval(_holdDuration);

            // 4) Dissolve + spark burst + slight expand
            _playSeq.AppendCallback(() => BurstSparks(_sparkCount));
            _playSeq.Append(DOTween.To(
                () => _dissolveMat.GetFloat("_DissolveAmount"),
                v => _dissolveMat.SetFloat("_DissolveAmount", v),
                1.12f,
                _dissolveDuration).SetEase(Ease.InOutSine));
            _playSeq.Join(cardRT.DOScale(1.05f, _dissolveDuration).SetEase(Ease.InOutSine));

            // Staggered secondary bursts while dissolving
            _playSeq.InsertCallback(_flyInDuration + 0.12f + _holdDuration + _dissolveDuration * 0.35f,
                () => BurstSparks(_sparkCount / 2));

            // 5) Fade overlay out
            _playSeq.Append(_overlayCg.DOFade(0f, _fadeOutDuration).SetEase(Ease.InQuad));
            _playSeq.OnComplete(() =>
            {
                _overlay.gameObject.SetActive(false);
                _playing = false;
                _playSeq = null;
            });
        }

        private void BurstSparks(int count)
        {
            if (_sparksRoot == null) return;
            var sprite = GetSparkSprite();

            for (int i = 0; i < count; i++)
            {
                var sg = new GameObject("spark");
                var img = sg.AddComponent<Image>();
                img.raycastTarget = false;
                img.sprite = sprite;
                img.color = Color.Lerp(
                    new Color(1f, 0.95f, 0.55f, 1f),
                    new Color(1f, 0.55f, 0.15f, 1f),
                    Random.value);
                var rt = img.rectTransform;
                rt.SetParent(_sparksRoot, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                // Seed near the card's lower 2/3 area (simulating dissolve origin)
                var startPos = new Vector2(
                    Random.Range(-_cardWidth * 0.42f, _cardWidth * 0.42f),
                    Random.Range(-_cardHeight * 0.48f, _cardHeight * 0.28f));
                rt.anchoredPosition = startPos;
                float size = Random.Range(6f, 14f);
                rt.sizeDelta = new Vector2(size, size);

                // Velocity: outward radial + upward bias
                var radial = startPos.sqrMagnitude > 1f ? startPos.normalized : Random.insideUnitCircle.normalized;
                var target = startPos
                    + radial * Random.Range(90f, 220f)
                    + Vector2.up * Random.Range(60f, 180f)
                    + new Vector2(Random.Range(-40f, 40f), 0f);

                float dur = Random.Range(0.75f, 1.25f);
                rt.DOAnchorPos(target, dur).SetEase(Ease.OutCubic).SetTarget(sg);
                rt.DOScale(0.1f, dur).SetEase(Ease.InQuad).SetTarget(sg);
                img.DOFade(0f, dur).SetEase(Ease.InQuad).SetTarget(sg)
                    .OnComplete(() =>
                    {
                        if (sg != null) Destroy(sg);
                    });
            }
        }

        private static Sprite GetSparkSprite()
        {
            if (s_sparkSprite != null) return s_sparkSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            var center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a; // soft falloff
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply(false, true);
            s_sparkSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            s_sparkSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_sparkSprite;
        }
    }
}
