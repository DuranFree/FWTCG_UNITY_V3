using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// Hotfix-12: 单位入场效果视觉序列。
    ///   1. 等单位落地（0.4s）
    ///   2. caster 白闪 + 同色光环从卡周边扩散消失
    ///   3. 能量光球从 caster 飞向每个 target（直线 + scale yoyo）
    ///   4. 光球到达 target 时在目标位置弹出飘屏（标签 + 同色）
    /// targets==null/空 → 只在 caster 上方飘屏（用于"摸牌"等无目标效果）
    /// </summary>
    public class EntryEffectVFX : MonoBehaviour
    {
        public static EntryEffectVFX Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 入场效果总入口（非阻塞，协程）。
        /// </summary>
        public void Play(UnitInstance caster, List<UnitInstance> targets, string label, Color color)
        {
            if (caster == null || string.IsNullOrEmpty(label)) return;
            if (!gameObject.activeInHierarchy) return;
            StartCoroutine(PlayRoutine(caster, targets, label, color));
        }

        /// <summary>
        /// 法术 / 装备专用：从指定 canvas 位置（比如 showcase 中心、装备卡位置）
        /// 飞能量球到每个 target 的卡面位置。不播 caster flash / ring（那部分由
        /// showcase 或装备飞行动画自己完成了）。
        /// 必须在 showcase 完全播完后调用，避免与展示并行。
        /// </summary>
        public void PlayOrbsFromPos(Vector2 fromCanvasPos, List<UnitInstance> targets, string label, Color color)
        {
            if (targets == null || targets.Count == 0 || !gameObject.activeInHierarchy) return;
            Canvas canvas = GameUI.Instance?.RootCanvasRef;
            if (canvas == null) return;
            foreach (var t in targets)
            {
                if (t == null) continue;
                var tCV = GameUI.Instance?.FindCardView(t);
                if (tCV == null) continue;
                StartCoroutine(FlyOrbFromPos(fromCanvasPos, tCV, color, label, canvas));
            }
        }

        /// <summary>
        /// 屏幕中心飞球 —— 法术 showcase 播完后用。中心 = canvas 坐标 (0,0)。
        /// </summary>
        public void PlaySpellOrbs(List<UnitInstance> targets, string label, Color color)
            => PlayOrbsFromPos(Vector2.zero, targets, label, color);

        /// <summary>
        /// 装备生效后飞球：装备卡位置 → 被装备单位。
        /// </summary>
        public void PlayEquipOrb(Vector2 fromCanvasPos, UnitInstance targetUnit, string label, Color color)
        {
            if (targetUnit == null) return;
            PlayOrbsFromPos(fromCanvasPos,
                new List<UnitInstance> { targetUnit }, label, color);
        }

        private IEnumerator FlyOrbFromPos(Vector2 fromPos, CardView to, Color color, string label, Canvas canvas)
        {
            if (to == null || canvas == null) yield break;
            var orbGO = new GameObject("SpellOrb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            orbGO.transform.SetParent(canvas.transform, false);
            var rt = orbGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(28f, 28f);
            var img = orbGO.GetComponent<Image>();
            img.sprite = GetSoftGaussianSprite();
            img.color = color;
            img.raycastTarget = false;
            rt.anchoredPosition = fromPos;

            Vector2 toPos = GetCanvasPos(to, canvas);
            const float DUR = 0.38f;
            var seq = DOTween.Sequence().SetTarget(orbGO).LinkKillOnDestroy(orbGO);
            seq.Append(DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, toPos, DUR)
                .SetEase(Ease.InOutQuad));
            seq.Join(rt.DOScale(1.3f, DUR * 0.5f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.InOutSine));

            yield return seq.WaitForCompletion();

            FloatingTipUI.ShowSingle(canvas, label, color, toPos);
            if (orbGO != null) Destroy(orbGO);
        }

        private IEnumerator PlayRoutine(UnitInstance caster, List<UnitInstance> targets, string label, Color color)
        {
            // Step 1: 等单位入场动画落地
            yield return new WaitForSeconds(0.4f);

            var casterCV = GameUI.Instance?.FindCardView(caster);
            Canvas canvas = GameUI.Instance?.RootCanvasRef;
            if (casterCV == null || canvas == null) yield break;

            // Step 2: caster 闪烁 + 光环扩散
            FlashCard(casterCV);
            SpawnRingExpand(casterCV, color);

            yield return new WaitForSeconds(0.25f);

            // Step 3: 光球飞向每个 target（并发）+ 到达后飘屏
            bool anyTarget = false;
            if (targets != null)
            {
                foreach (var t in targets)
                {
                    if (t == null) continue;
                    var tCV = GameUI.Instance?.FindCardView(t);
                    if (tCV == null) continue;
                    anyTarget = true;
                    StartCoroutine(FlyOrb(casterCV, tCV, color, label, canvas));
                }
            }
            if (!anyTarget)
            {
                // 无 target：在 caster 上方飘屏
                Vector2 pos = GetCanvasPos(casterCV, canvas);
                FloatingTipUI.ShowSingle(canvas, label, color, pos);
            }
        }

        // ── VFX primitives ────────────────────────────────────────────────────

        private IEnumerator FlyOrb(CardView from, CardView to, Color color, string label, Canvas canvas)
        {
            if (from == null || to == null || canvas == null) yield break;
            var orbGO = new GameObject("EntryOrb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            orbGO.transform.SetParent(canvas.transform, false);
            var rt = orbGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(22f, 22f);
            var img = orbGO.GetComponent<Image>();
            img.sprite = GetSoftGaussianSprite();
            img.color = color;
            img.raycastTarget = false;

            Vector2 fromPos = GetCanvasPos(from, canvas);
            Vector2 toPos   = GetCanvasPos(to, canvas);
            rt.anchoredPosition = fromPos;

            const float DUR = 0.38f;
            var seq = DOTween.Sequence().SetTarget(orbGO).LinkKillOnDestroy(orbGO);
            seq.Append(DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, toPos, DUR)
                .SetEase(Ease.InOutQuad));
            // 路途中 scale 微跳动营造"能量脉冲"感
            seq.Join(rt.DOScale(1.3f, DUR * 0.5f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.InOutSine));

            yield return seq.WaitForCompletion();

            // 到达：飘屏 + 销毁光球
            FloatingTipUI.ShowSingle(canvas, label, color, toPos);
            if (orbGO != null) Destroy(orbGO);
        }

        private static void FlashCard(CardView cv)
        {
            if (cv == null) return;
            var img = cv.GetComponent<Image>();
            if (img == null) return;
            Color orig = img.color;
            // .SetTarget(cv.gameObject) 让 CardView.OnDestroy 的 DOTween.Kill(gameObject) 能清掉；
            // DOColor 目标是 Graphic，卡牌销毁时 DOTween SafeMode 捕获 warning，这里再加一层
            // 防护：tween 每帧检查 img 是否还在，不在就中止
            var seq = DOTween.Sequence().SetTarget(cv.gameObject);
            seq.Append(img.DOColor(new Color(1.4f, 1.4f, 1.4f, orig.a), 0.08f)
                .OnUpdate(() => { if (img == null) seq.Kill(); }));
            seq.Append(img.DOColor(orig, 0.22f)
                .OnUpdate(() => { if (img == null) seq.Kill(); }));
        }

        private static void SpawnRingExpand(CardView cv, Color color)
        {
            if (cv == null) return;
            // 找根 canvas 并把 ring 挂到 canvas 上（而非 cv），避免 cv 死亡时
            // ring 被父级销毁导致 tween 访问已销毁 Image（出现紫色尸体帧 + warning）
            var canvas = cv.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // 在 canvas 坐标系里取卡牌中心位置
            var cvRT = cv.GetComponent<RectTransform>();
            var canvasRT = canvas.GetComponent<RectTransform>();
            if (cvRT == null || canvasRT == null) return;
            var corners = new Vector3[4];
            cvRT.GetWorldCorners(corners);
            Vector2 screenCenter = new Vector2(
                (corners[0].x + corners[2].x) * 0.5f,
                (corners[0].y + corners[2].y) * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenCenter, canvas.worldCamera, out Vector2 canvasPos);

            var ring = new GameObject("EntryRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ring.transform.SetParent(canvas.transform, false);
            var rt = ring.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = cvRT.rect.size;
            rt.anchoredPosition = canvasPos;
            rt.localScale = Vector3.one;
            var img = ring.GetComponent<Image>();
            img.sprite = GetRingSprite();
            img.color = new Color(color.r, color.g, color.b, 0.85f);
            img.raycastTarget = false;
            ring.transform.SetAsLastSibling();

            var seq = DOTween.Sequence().SetTarget(ring).LinkKillOnDestroy(ring);
            seq.Append(rt.DOScale(2.2f, 0.55f).SetEase(Ease.OutCubic));
            seq.Join(img.DOFade(0f, 0.55f).SetEase(Ease.OutCubic));
            seq.OnComplete(() => { if (ring != null) Destroy(ring); });
        }

        private static Vector2 GetCanvasPos(CardView cv, Canvas canvas)
        {
            if (cv == null || canvas == null) return Vector2.zero;
            var corners = new Vector3[4];
            cv.GetComponent<RectTransform>().GetWorldCorners(corners);
            Vector2 screen = new Vector2((corners[0].x + corners[2].x) * 0.5f,
                                         (corners[0].y + corners[2].y) * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), screen, canvas.worldCamera, out Vector2 local);
            return local;
        }

        // ── Programmatic sprites (避免调 Resources.GetBuiltinResource) ────────

        private static Sprite _softSprite;
        private static Sprite GetSoftGaussianSprite()
        {
            if (_softSprite != null) return _softSprite;
            const int W = 64;
            var tex = new Texture2D(W, W, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            float cx = (W - 1) / 2f, cy = (W - 1) / 2f;
            float sigma = W * 0.28f;
            float twoSigSq = 2f * sigma * sigma;
            var px = new Color32[W * W];
            for (int y = 0; y < W; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = x - cx, dy = y - cy;
                float g = Mathf.Exp(-(dx * dx + dy * dy) / twoSigSq);
                px[y * W + x] = new Color32(255, 255, 255, (byte)(g * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            _softSprite = Sprite.Create(tex, new Rect(0, 0, W, W), new Vector2(0.5f, 0.5f), 100f);
            return _softSprite;
        }

        private static Sprite _ringSprite;
        private static Sprite GetRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int W = 128;
            var tex = new Texture2D(W, W, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            float cx = (W - 1) / 2f, cy = (W - 1) / 2f;
            float outerR = W * 0.48f;
            float innerR = W * 0.42f; // 环壁厚度 ≈ 6% × W（亚 px 抗锯齿）
            var px = new Color32[W * W];
            for (int y = 0; y < W; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a;
                if (d > outerR) a = Mathf.Clamp01(1f - (d - outerR));
                else if (d < innerR) a = Mathf.Clamp01(1f - (innerR - d));
                else a = 1f; // 环壁内
                px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, W, W), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }
    }
}
