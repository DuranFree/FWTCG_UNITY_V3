using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// 法术溶解 + 魔力粒子散发的共用工具。抽离自 SpellCastDemo，供 SpellShowcaseUI 等实际施法表演使用。
    /// </summary>
    public static class SpellDissolveFX
    {
        public const string SHADER_NAME = "FWTCG/UIDissolve";

        // Material property IDs (cached to avoid runtime string lookup churn)
        private static readonly int PropDissolveAmount  = Shader.PropertyToID("_DissolveAmount");
        private static readonly int PropEdgeWidth       = Shader.PropertyToID("_EdgeWidth");
        private static readonly int PropEdgeColor       = Shader.PropertyToID("_EdgeColor");
        private static readonly int PropEdgeGlow        = Shader.PropertyToID("_EdgeGlow");
        private static readonly int PropNoiseScale      = Shader.PropertyToID("_NoiseScale");
        private static readonly int PropDissolveDir     = Shader.PropertyToID("_DissolveDirection");

        // Shared spark sprite (radial gradient 32×32, HideAndDontSave)
        private static Sprite s_sparkSprite;

        /// <summary>
        /// 创建新的 dissolve 材质实例。返回 null 则 shader 未找到，调用方需要做飞出回退。
        /// </summary>
        public static Material CreateDissolveMaterial()
        {
            var shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                Debug.LogWarning("[SpellDissolveFX] Shader '" + SHADER_NAME + "' not found; dissolve disabled.");
                return null;
            }

            var mat = new Material(shader) { name = "SpellDissolveFX_Instance" };
            mat.SetFloat(PropDissolveAmount, 0f);
            mat.SetFloat(PropEdgeWidth, 0.09f);
            mat.SetColor(PropEdgeColor, new Color(1f, 0.85f, 0.35f, 1f));
            mat.SetFloat(PropEdgeGlow, 4f);
            mat.SetFloat(PropNoiseScale, 3.2f);
            mat.SetVector(PropDissolveDir, new Vector4(0f, 1f, 0.3f, 0f));
            return mat;
        }

        /// <summary>
        /// 为 dissolve 材质设置烧尽方向。fromPlayer=true 从下往上烧（玩家视角），false 从上往下烧（敌方视角）。
        /// </summary>
        public static void SetDirection(Material mat, bool fromPlayer)
        {
            if (mat == null) return;
            mat.SetVector(PropDissolveDir,
                fromPlayer ? new Vector4(0f, 1f, 0.3f, 0f) : new Vector4(0f, -1f, 0.3f, 0f));
        }

        /// <summary>
        /// 重置材质到未溶解状态。
        /// </summary>
        public static void ResetAmount(Material mat)
        {
            if (mat == null) return;
            mat.SetFloat(PropDissolveAmount, 0f);
        }

        /// <summary>
        /// 返回一个 DOTween 补间，把材质 _DissolveAmount 从当前值推到 endValue，通常 endValue=1.12 保证完全烧尽。
        /// </summary>
        public static Tweener TweenAmount(Material mat, float endValue, float duration, Ease ease = Ease.InOutSine)
        {
            if (mat == null) return null;
            return DOTween.To(
                () => mat.GetFloat(PropDissolveAmount),
                v => mat.SetFloat(PropDissolveAmount, v),
                endValue, duration).SetEase(ease);
        }

        /// <summary>
        /// 在指定父节点下爆发金色魔力粒子。父节点建议为法术卡片的 RectTransform 或其同级容器。
        /// 每颗粒子独立 tween：外散+上抬、缩小、淡出，完成后自销毁。
        /// </summary>
        public static void BurstSparks(RectTransform parent, int count, float areaWidth, float areaHeight)
        {
            if (parent == null || count <= 0) return;
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
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                var startPos = new Vector2(
                    Random.Range(-areaWidth * 0.42f, areaWidth * 0.42f),
                    Random.Range(-areaHeight * 0.48f, areaHeight * 0.28f));
                rt.anchoredPosition = startPos;
                float size = Random.Range(6f, 14f);
                rt.sizeDelta = new Vector2(size, size);

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
                        if (sg != null) UnityEngine.Object.Destroy(sg);
                    });
            }
        }

        private static Sprite GetSparkSprite()
        {
            if (s_sparkSprite != null) return s_sparkSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
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
