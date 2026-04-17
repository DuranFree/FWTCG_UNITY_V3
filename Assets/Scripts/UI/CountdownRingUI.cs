using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FWTCG.UI
{
    /// <summary>
    /// 倒计时环 v5
    ///
    /// 核心修复：
    ///   ① 使用 material.SetColor("_HdrColor") 传递 HDR 颜色
    ///      → 绕过 Image.color Color32 截断，HDR 值完整到达 shader
    ///   ② EnergyGemUI shader 改为"亮度模式"着色
    ///      → 提取纹理亮度保留石头/宝石结构，_HdrColor 决定色相，不受原始纹理绿色影响
    ///   ③ ParticleSystem sortingOrder 高于 Canvas，保证粒子在 UI 之上渲染
    ///
    /// 颜色序列：灰（空槽）→ 绿 → 青 → 蓝 → 黄 → 橙 → 红
    ///            → 12/12 满载爆闪 → 慢慢暗淡回灰
    ///
    /// 暗灰底图：始终显示所有 12 个宝石槽轮廓（很暗的灰色），提示空槽位置
    /// </summary>
    public class CountdownRingUI : MonoBehaviour
    {
        [Header("Ring Base (stone frame)")]
        [SerializeField] public Image baseRing;

        // ── 常量 ─────────────────────────────────────────────────────────
        private const int   SEGMENTS      = 12;
        private const float BREATH_PERIOD = 1.4f;
        private const float RING_RADIUS   = 100f;

        // ── 调色板：每个宝石索引对应固定色（Bright 值，Dim = 0.4×）
        // 序列：暗绿 → 绿 → 青 → 暖青 → 黄绿 → 黄 → 琥珀 → 橙 → 橙红 → 红
        // HDR 最大分量控制在 2.7 以内，避免 Bloom 过曝（原来最大 5.0）
        // 序列：暗绿→绿→绿青→青→青蓝  →  黄→亮黄→暖黄  →  琥珀→橙→橙红→红
        // 规则：seg 5 起 R ≥ G，彻底避免青色之后出现"绿色复现"
        private static readonly Color[] k_PalBright = new Color[]
        {
            new Color(0.06f, 0.80f, 0.10f, 1f),  //  0 dark green
            new Color(0.04f, 1.05f, 0.22f, 1f),  //  1 green
            new Color(0.03f, 1.20f, 0.55f, 1f),  //  2 green-teal
            new Color(0.02f, 1.10f, 1.10f, 1f),  //  3 teal
            new Color(0.02f, 0.80f, 1.45f, 1f),  //  4 cyan（蓝主导）
            new Color(1.10f, 1.05f, 0.04f, 1f),  //  5 yellow（R≥G，直接跳黄，无绿复现）
            new Color(1.40f, 1.00f, 0.04f, 1f),  //  6 bright yellow
            new Color(1.65f, 0.75f, 0.03f, 1f),  //  7 warm yellow
            new Color(1.82f, 0.52f, 0.03f, 1f),  //  8 amber
            new Color(1.98f, 0.32f, 0.02f, 1f),  //  9 orange
            new Color(2.12f, 0.18f, 0.02f, 1f),  // 10 orange-red
            new Color(2.20f, 0.08f, 0.01f, 1f),  // 11 red
        };
        private static readonly Color[] k_PalDim;
        private static readonly Color[] k_PalBurst;
        private static readonly Color   k_DimBase  = new Color(0.30f, 0.38f, 0.62f, 1f);  // 偏蓝冷白，暗槽可见
        private static readonly Color   k_MegaBurst = new Color(9f, 7f, 4f, 1f);

        static CountdownRingUI()
        {
            int n = k_PalBright.Length;
            k_PalDim   = new Color[n];
            k_PalBurst = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Color b = k_PalBright[i];
                k_PalDim[i]   = new Color(b.r * 0.45f, b.g * 0.45f, b.b * 0.45f, 1f);
                k_PalBurst[i] = new Color(b.r * 4.5f,  b.g * 4.5f,  b.b * 4.5f,  1f);
            }
        }

        // ── 颜色渐进：每颗宝石从初始色匀速走向红色 ──────────────────────────
        // 所有宝石用相同时长到达红色，与出现顺序无关
        private const float COLOUR_PROGRESSION_DURATION = 28f;

        // ── 运行时状态 ────────────────────────────────────────────────────
        private float         _curProgress  = 0f;
        private bool          _isBreathing  = false;
        private float         _breathStart  = 0f;
        private bool          _megaFlashed  = false;
        private bool          _megaDimming  = false;  // MegaFlash 暗淡期间，屏蔽 OnSegHide
        private Coroutine     _megaFlashCo  = null;   // 追踪 MegaFlash 协程，ResetRing 时先停

        private Image[]       _segImages;   // 12 段宝石图像
        private Material[]    _segMats;     // 对应材质（Material.SetColor 走这里）
        private bool[]        _segActive;
        private Coroutine[]   _segAnims;
        private float[]       _segAppearTime; // 每颗宝石出现的 Time.time

        private Image[]       _dimImages;   // 12 个暗灰底图分段（每槽独立，宝石亮起时隐藏对应槽）
        private Material[]    _dimMats;     // 对应材质数组（new Material，需手动销毁）

        private Light[]       _gemLights;
        private Image[]       _gemGlows;    // per-gem glow (EnergyGemGlowUI additive, same wedge shape)
        private Material[]    _glowMats;    // glow 独立材质，通过 _HdrColor 控制亮度

        // ── 外圈光环 ─────────────────────────────────────────────────────────
        private Image         _ringArcImg;          // 光环（随宝石延展 + 持续旋转）
        private Material      _ringArcMat;          // 光环材质（new Material，需手动销毁）
        private Texture2D     _ringArcTex;          // 过程式环形渐变纹理
        private Coroutine     _ringArcAnim;          // 保留字段
        // 填充速度：30s 走满一圈（与倒计时总时长一致）
        private const float   RING_FILL_SPEED = 1f / 30f;
        private Color         _ringTargetColor = new Color(0.55f, 0.75f, 1.00f, 0.95f);

        // ── 光环扫描触发宝石点亮 ────────────────────────────────────────────
        // _intendedActiveSeg：progress 计算出来的"应该亮几颗"（上限）
        // _fillAmountTotal：圆环累计扫过的总量（单调增长，不受循环重置影响）
        // 宝石 i 的触发阈值 = (i+1)/SEGMENTS；当 _fillAmountTotal 跨过阈值且 i < _intendedActiveSeg 时，宝石亮起
        private int   _intendedActiveSeg = 0;
        private float _fillAmountTotal   = 0f;

        // 注：原独立 GemGlowBlocker 已移除，改由 GemRingArc 移到外圈石头框架位置兼任遮挡


        private ParticleSystem _psConv;     // 粒子汇聚
        private ParticleSystem _psConn;     // 宝石间连接
        private Sprite         _fillSprite;

        // ── 生命周期 ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (!Application.isPlaying) return;
            CleanRuntimeChildren();
            LoadFillSprite();
            BuildDimBase();       // 底层：暗灰宝石槽轮廓
            BuildSegImages();     // 宝石图像层
            BuildGemLights();
            BuildGemGlows();      // 光晕层：在 GemSeg 之上
            BuildRingArc();       // 外圈光环（最上层，位于外圈石头框架，天然挡住漏光）
            BuildParticleSystems();
            InitFills();
        }

        private void Update()
        {
            // 外圈光环：fillAmount 自动增长，走满后循环
            if (_ringArcImg != null && _ringArcImg.gameObject.activeSelf)
            {
                float inc = RING_FILL_SPEED * Time.deltaTime;
                _fillAmountTotal += inc;                           // 单调累计（不循环）
                float f = _ringArcImg.fillAmount + inc;
                if (f >= 1f) f -= 1f;
                _ringArcImg.fillAmount = f;
                _ringArcImg.color = Color.Lerp(_ringArcImg.color, _ringTargetColor, Time.deltaTime * 2.5f);

                // 扫描触发：光环累计走过 (i+1)/12 且 progress 允许 → 才点亮宝石 i
                for (int i = 0; i < _intendedActiveSeg && i < SEGMENTS; i++)
                {
                    if (_segActive[i]) continue;
                    float threshold = (i + 1f) / SEGMENTS;
                    if (_fillAmountTotal >= threshold) OnSegAppear(i);
                }
            }

            if (!_isBreathing) return;
            float now    = Time.time;
            float breath = BreathT(now);

            for (int i = 0; i < SEGMENTS; i++)
            {
                if (!_segActive[i] || _segMats[i] == null) continue;
                if (_segAnims[i] != null) continue;

                // 颜色渐进：宝石 i 从 i/11 走向 1，速度自适应
                // 保证当第 k 颗宝石出现时，前面所有宝石恰好同时到达 k/11
                float initialPos = GetPalettePos(i);
                float duration   = (SEGMENTS - 1 - i) * (30f / SEGMENTS); // 宝石越晚出现，变色越快
                float elapsed    = now - _segAppearTime[i];
                float currentPos = duration <= 0f ? 1f
                                 : Mathf.Lerp(initialPos, 1f, Mathf.Clamp01(elapsed / duration));

                // 叠加呼吸亮度（dim ↔ bright）
                Color dim    = SamplePalette(k_PalDim,    currentPos);
                Color bright = SamplePalette(k_PalBright, currentPos);
                _segMats[i].SetColor("_HdrColor", Color.Lerp(dim, bright, breath));
            }

            SyncGemLights(breath);
        }

        // ── Public API ────────────────────────────────────────────────────

        public void SetProgress(float progress)
        {
            _curProgress = progress = Mathf.Clamp01(progress);
            int activeSeg = Mathf.RoundToInt(progress * SEGMENTS);
            _intendedActiveSeg = activeSeg;  // 上限：光环扫到位才实际点亮

            // 只处理熄灭；"出现"延迟到 Update 里光环扫过阈值时触发
            for (int i = 0; i < SEGMENTS; i++)
            {
                if (i >= activeSeg && _segActive[i]) OnSegHide(i);
            }

            UpdateConnSparks(activeSeg);
            UpdateRingArc(activeSeg);

            // progress > 0 就启动光环（要先跑起来才能扫到宝石位置触发点亮）
            if (activeSeg > 0 && _ringArcImg != null && !_ringArcImg.gameObject.activeSelf)
                _ringArcImg.gameObject.SetActive(true);

            if (activeSeg > 0 && !_isBreathing) StartBreathing();
            if (activeSeg == 0) StopBreathing();

            // 倒计时真正到 0（progress=1）才爆闪；此时确保所有宝石都已点亮
            if (progress >= 1f && !_megaFlashed)
            {
                for (int i = 0; i < SEGMENTS; i++)
                    if (!_segActive[i]) OnSegAppear(i);  // 保险：防最后 1-2 颗未被光环扫到
                _megaFlashed = true;
                _megaFlashCo = StartCoroutine(MegaFlashSequence());
            }
        }

        public void ResetRing()
        {
            _curProgress = 0f;
            _megaFlashed = false;
            _intendedActiveSeg = 0;
            _fillAmountTotal   = 0f;

            if (_megaDimming)
            {
                // MegaFlash 暗淡进行中：只重置逻辑状态，不停协程，让视觉自然熄灭
                StopBreathing();
                if (_ringArcAnim != null) { StopCoroutine(_ringArcAnim); _ringArcAnim = null; }
                _ringTargetColor = new Color(0.55f, 0.75f, 1.00f, 0.95f);
                if (_ringArcImg != null)
                {
                    _ringArcImg.fillAmount = 0f;
                    _ringArcImg.color = _ringTargetColor;
                    _ringArcImg.gameObject.SetActive(false);  // 下次 SetProgress>0 才重新显示
                }
                if (_segImages != null)
                {
                    for (int i = 0; i < SEGMENTS; i++)
                    {
                        _segActive[i]     = false;
                        _segAppearTime[i] = 0f;
                        if (_segAnims[i] != null) { StopCoroutine(_segAnims[i]); _segAnims[i] = null; }
                        // 宝石 GameObject 留给协程自己隐藏，灯光立即关掉
                        if (_gemLights != null && i < _gemLights.Length && _gemLights[i] != null)
                            _gemLights[i].enabled = false;
                    }
                }
                return; // 不停 _megaFlashCo，让暗淡跑完
            }

            // 正常 reset（无 MegaFlash 进行）
            if (_megaFlashCo != null) { StopCoroutine(_megaFlashCo); _megaFlashCo = null; }
            _megaDimming = false;
            StopBreathing();
            if (_ringArcAnim != null) { StopCoroutine(_ringArcAnim); _ringArcAnim = null; }
            _ringTargetColor = new Color(0.55f, 0.75f, 1.00f, 0.95f);
            if (_ringArcImg != null)
            {
                _ringArcImg.rectTransform.localEulerAngles = Vector3.zero;
                _ringArcImg.color = _ringTargetColor;
                _ringArcImg.gameObject.SetActive(false);  // 下次 SetProgress>0 才重新显示
            }
            if (_segImages == null) return;
            for (int i = 0; i < SEGMENTS; i++)
            {
                _segActive[i]     = false;
                _segAppearTime[i] = 0f;
                if (_segAnims[i] != null) { StopCoroutine(_segAnims[i]); _segAnims[i] = null; }
                if (_segImages[i] != null) _segImages[i].gameObject.SetActive(false);
                if (_segMats[i]   != null) _segMats[i].SetColor("_HdrColor", k_DimBase);
                if (_gemGlows     != null && i < _gemGlows.Length  && _gemGlows[i]  != null)
                    _gemGlows[i].gameObject.SetActive(false);
                if (_gemLights    != null && i < _gemLights.Length && _gemLights[i] != null)
                    _gemLights[i].enabled = false;
                // 恢复所有暗灰槽
                if (_dimImages    != null && i < _dimImages.Length && _dimImages[i]  != null)
                    _dimImages[i].gameObject.SetActive(true);
            }
        }

        // ── 宝石出现 / 隐藏 ───────────────────────────────────────────────

        private void OnSegAppear(int idx)
        {
            if (_megaDimming) return;  // 暗淡期间不显示新宝石，避免新旧宝石重叠
            _segActive[idx] = true;
            _segAppearTime[idx] = Time.time;   // 记录出现时刻，用于颜色渐进
            var img = _segImages[idx];
            if (img == null) return;

            // 隐藏对应暗灰槽（彩色宝石完全覆盖它，避免叠影）
            if (_dimImages != null && idx < _dimImages.Length && _dimImages[idx] != null)
                _dimImages[idx].gameObject.SetActive(false);

            img.gameObject.SetActive(true);
            _segMats[idx].SetColor("_HdrColor", k_DimBase);

            // 同步激活光晕（_HdrColor 控制发光量，初始透明）
            if (_gemGlows != null && idx < _gemGlows.Length && _gemGlows[idx] != null)
            {
                _gemGlows[idx].gameObject.SetActive(true);
                SetGlowColor(idx, Color.black, 0f);  // _HdrColor=(0,0,0) → additive 无贡献
            }

            if (_gemLights != null && idx < _gemLights.Length)
                _gemLights[idx].enabled = true;

            EmitConvergeParticles(idx);

            if (_segAnims[idx] != null) StopCoroutine(_segAnims[idx]);
            _segAnims[idx] = StartCoroutine(SegAppearAnim(idx));
        }

        private void OnSegHide(int idx)
        {
            if (_megaDimming) return;  // MegaFlash 暗淡期间不立刻隐藏
            _segActive[idx] = false;
            if (_segAnims[idx] != null) { StopCoroutine(_segAnims[idx]); _segAnims[idx] = null; }
            if (_segImages[idx] != null) _segImages[idx].gameObject.SetActive(false);
            if (_gemGlows  != null && idx < _gemGlows.Length  && _gemGlows[idx]  != null)
                _gemGlows[idx].gameObject.SetActive(false);
            if (_gemLights != null && idx < _gemLights.Length && _gemLights[idx] != null)
                _gemLights[idx].enabled = false;
            // 恢复暗灰槽显示
            if (_dimImages != null && idx < _dimImages.Length && _dimImages[idx] != null)
                _dimImages[idx].gameObject.SetActive(true);
        }

        // ── 宝石出现动画：暗 → 爆发 → 稳定 ──────────────────────────────

        // 光晕 alpha 范围：dim=0.18，bright=0.45，burst=0.80
        private const float GLOW_ALPHA_DIM    = 0.10f;  // 呼吸低谷
        private const float GLOW_ALPHA_BRIGHT = 0.22f;  // 呼吸高峰
        private const float GLOW_ALPHA_BURST  = 0.28f;  // 出现闪光峰值（降低避免溢出到环外）

        private IEnumerator SegAppearAnim(int idx)
        {
            // 出现时颜色 = 当前所有宝石同步色（由索引决定初始位置，和前面宝石保持同步）
            float palPos = GetPalettePos(idx);
            Color  burst   = SamplePalette(k_PalBurst,  palPos);
            Color  bright  = SamplePalette(k_PalBright, palPos);
            var    mat     = _segMats[idx];
            var    glow    = (_gemGlows != null && idx < _gemGlows.Length) ? _gemGlows[idx] : null;
            Color  glowCol = GetNormalisedColor(palPos); // 单位亮度，alpha 单独控制

            // 阶段1：暗 → 爆发（0.07s，快速点亮）
            float e = 0f;
            while (e < 0.07f)
            {
                e += Time.deltaTime;
                float t0 = e / 0.07f;
                if (mat  != null) mat.SetColor("_HdrColor", Color.Lerp(k_DimBase, burst, t0));
                if (glow != null) SetGlowColor(idx, glowCol, Mathf.Lerp(0f, GLOW_ALPHA_BURST, t0));
                yield return null;
            }
            if (mat  != null) mat.SetColor("_HdrColor", burst);
            if (glow != null) SetGlowColor(idx, glowCol, GLOW_ALPHA_BURST);

            // 宝石灯同步爆发
            if (_gemLights != null && idx < _gemLights.Length && _gemLights[idx] != null)
            {
                var gl = _gemLights[idx];
                gl.color     = GetLightColor(palPos);
                gl.intensity = 7.5f;
                gl.range     = 13f;
                StartCoroutine(FadeLightBurst(gl, 7.5f, 1.5f, 13f, 8f, 0.5f));
            }

            // 保持 2 帧
            yield return null;
            yield return null;

            // 阶段2：爆发 → 稳定亮度（0.38s，OutCubic）
            e = 0f;
            while (e < 0.38f)
            {
                e += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(e / 0.38f), 3f);
                if (mat  != null) mat.SetColor("_HdrColor", Color.Lerp(burst, bright, t));
                if (glow != null) SetGlowColor(idx, glowCol, Mathf.Lerp(GLOW_ALPHA_BURST, GLOW_ALPHA_BRIGHT, t));
                yield return null;
            }
            if (mat  != null) mat.SetColor("_HdrColor", bright);
            if (glow != null) SetGlowColor(idx, glowCol, GLOW_ALPHA_BRIGHT);

            _segAnims[idx] = null;
        }

        // ── 满载爆闪 → 慢慢暗淡回灰 ─────────────────────────────────────

        private IEnumerator MegaFlashSequence()
        {
            _megaDimming = true;
            StopBreathing();

            // 全灯爆亮
            if (_gemLights != null)
                foreach (var gl in _gemLights)
                    if (gl != null && gl.enabled)
                    { gl.intensity = 10f; gl.range = 18f; gl.color = Color.white; }

            // 外圈光环：满载时高亮爆闪（fill 已经 = 1，直接设颜色）
            // fillAmount 由 Update 自主驱动，无需设目标
            _ringTargetColor = new Color(1f, 0.95f, 0.80f, 1.00f);

            // 所有段瞬间跳到 MegaBurst，光晕用红色（_HdrColor 控制发光量）
            Color redNorm = GetNormalisedColor(1f);
            float megaGlowAlpha = GLOW_ALPHA_BURST * 0.8f;  // 爆闪时进一步限制亮度
            for (int i = 0; i < SEGMENTS; i++)
            {
                if (_segMats[i] != null) _segMats[i].SetColor("_HdrColor", k_MegaBurst);
                SetGlowColor(i, redNorm, megaGlowAlpha);
            }

            // 保持 3 帧
            yield return null;
            yield return null;
            yield return null;

            // 红色 Bright（所有宝石此时都已走到红色，pos=1）
            Color redBright = SamplePalette(k_PalBright, 1f);

            // MegaBurst → 红色（0.25s）— 不回到各自初始色，全部统一到红
            float e = 0f;
            while (e < 0.25f)
            {
                e += Time.deltaTime;
                float t = Mathf.Clamp01(e / 0.25f);
                for (int i = 0; i < SEGMENTS; i++)
                {
                    if (_segMats[i] == null) continue;
                    _segMats[i].SetColor("_HdrColor", Color.Lerp(k_MegaBurst, redBright, t));
                }
                yield return null;
            }

            // 红色 → 完全透明（3.5s，缓慢熄灭，alpha 0→完全消失）
            // 同时 _HdrColor 保持红色亮度，只靠 alpha 淡出，避免变成灰白色
            e = 0f;
            while (e < 3.5f)
            {
                e += Time.deltaTime;
                float t = Mathf.Clamp01(e / 3.5f);
                // OutCubic：前段慢，后段快收尾
                float tEased = 1f - Mathf.Pow(1f - t, 3f);
                float alpha = 1f - tEased;
                float lightIntensity = Mathf.Lerp(1.5f, 0f, tEased);
                for (int i = 0; i < SEGMENTS; i++)
                {
                    if (_segImages[i] != null)
                        _segImages[i].color = new Color(1f, 1f, 1f, alpha);
                    SetGlowColor(i, redNorm, megaGlowAlpha * alpha);
                }
                if (_gemLights != null)
                    foreach (var gl in _gemLights)
                        if (gl != null && gl.enabled)
                        { gl.intensity = lightIntensity; gl.range = Mathf.Lerp(8f, 4f, tEased); }
                yield return null;
            }

            // 暗淡完成，正式隐藏所有宝石，清除标志
            _megaDimming = false;
            _megaFlashCo = null;
            for (int i = 0; i < SEGMENTS; i++)
            {
                // 重置 Image.color.a=1，下轮出现时不会透明
                if (_segImages[i] != null) _segImages[i].color = Color.white;
                if (_segMats[i]   != null) _segMats[i].SetColor("_HdrColor", k_DimBase);
                // 正式隐藏
                _segActive[i] = false;
                if (_segImages[i] != null) _segImages[i].gameObject.SetActive(false);
                if (_gemGlows != null && i < _gemGlows.Length && _gemGlows[i] != null)
                    _gemGlows[i].gameObject.SetActive(false);
                if (_gemLights != null && i < _gemLights.Length && _gemLights[i] != null)
                    _gemLights[i].enabled = false;
                // 宝石全部熄灭后，暗灰槽全部恢复显示
                if (_dimImages != null && i < _dimImages.Length && _dimImages[i] != null)
                    _dimImages[i].gameObject.SetActive(true);
            }
            _ringTargetColor = new Color(0.55f, 0.75f, 1.00f, 0.95f);
        }

        // ── 颜色辅助 ──────────────────────────────────────────────────────

        // 颜色由段索引固定决定，与时间/进度无关
        // seg 0 = 暗绿，seg 2 = 青，seg 4-5 = 蓝，seg 11 = 红
        private float GetPalettePos(int segIdx)
            => (float)segIdx / (SEGMENTS - 1);

        // 所有段共用同一呼吸 t 值 → 完全同步
        private float BreathT(float time)
            => (Mathf.Sin((time - _breathStart)
               * Mathf.PI * 2f / BREATH_PERIOD - Mathf.PI * 0.5f) + 1f) * 0.5f;

        private Color SampleLerp(float pos, float breathT)
        {
            Color dim    = SamplePalette(k_PalDim,    pos);
            Color bright = SamplePalette(k_PalBright, pos);
            return Color.Lerp(dim, bright, breathT);
        }

        private static Color SamplePalette(Color[] pal, float pos)
        {
            float f  = Mathf.Clamp01(pos) * (pal.Length - 1);
            int   i0 = Mathf.FloorToInt(f);
            int   i1 = Mathf.Min(i0 + 1, pal.Length - 1);
            Color c  = Color.Lerp(pal[i0], pal[i1], f - i0);
            c.a = 1f;
            return c;
        }

        private static Color GetLightColor(float pos)
        {
            Color c = SamplePalette(k_PalBright, pos);
            float m = Mathf.Max(c.r, c.g, c.b, 0.001f);
            return new Color(c.r / m, c.g / m, c.b / m, 1f);
        }

        /// <summary>将调色板颜色归一化到 [0,1] 范围（用于光晕 Image.color，alpha 单独控制）。</summary>
        private static Color GetNormalisedColor(float pos)
        {
            Color c = SamplePalette(k_PalBright, pos);
            float m = Mathf.Max(c.r, c.g, c.b, 0.001f);
            // 归一化但保留饱和度感：每个分量除以最大值
            return new Color(Mathf.Clamp01(c.r / m), Mathf.Clamp01(c.g / m), Mathf.Clamp01(c.b / m), 1f);
        }

        // ── Glow 颜色辅助 ──────────────────────────────────────────────────
        // brightness: 0=不发光, 1=正常亮度, >1=更亮
        private void SetGlowColor(int idx, Color normColor, float brightness)
        {
            if (_glowMats == null || idx >= _glowMats.Length || _glowMats[idx] == null) return;
            _glowMats[idx].SetColor("_HdrColor",
                new Color(normColor.r * brightness, normColor.g * brightness, normColor.b * brightness, 1f));
        }

        // ── 呼吸 ──────────────────────────────────────────────────────────

        private void StartBreathing() { _breathStart = Time.time; _isBreathing = true; }
        private void StopBreathing()  => _isBreathing = false;

        // ── 宝石灯同步 ─────────────────────────────────────────────────────

        private void SyncGemLights(float breathT)
        {
            float intensity = Mathf.Lerp(0.5f, 2.8f, breathT);
            float range     = Mathf.Lerp(6f, 10f, breathT);
            float glowAlpha = Mathf.Lerp(GLOW_ALPHA_DIM, GLOW_ALPHA_BRIGHT, breathT);

            for (int i = 0; i < SEGMENTS; i++)
            {
                if (!_segActive[i]) continue;
                if (_segAnims[i] != null) continue;

                // 颜色渐进：与 Update 主循环保持一致
                float initialPos2 = GetPalettePos(i);
                float duration2   = (SEGMENTS - 1 - i) * (30f / SEGMENTS);
                float elapsed2    = Time.time - _segAppearTime[i];
                float currentPos  = duration2 <= 0f ? 1f
                                  : Mathf.Lerp(initialPos2, 1f, Mathf.Clamp01(elapsed2 / duration2));

                if (_gemLights != null && i < _gemLights.Length && _gemLights[i] != null && _gemLights[i].enabled)
                {
                    _gemLights[i].color     = GetLightColor(currentPos);
                    _gemLights[i].intensity = intensity;
                    _gemLights[i].range     = range;
                }

                if (_gemGlows != null && i < _gemGlows.Length && _gemGlows[i] != null)
                {
                    Color gc = GetNormalisedColor(currentPos);
                    SetGlowColor(i, gc, glowAlpha);
                }
            }
        }

        // ── 粒子汇聚 ──────────────────────────────────────────────────────

        private void EmitConvergeParticles(int idx)
        {
            if (_psConv == null) return;
            float angleDeg = 90f - (idx + 0.5f) * (360f / SEGMENTS);
            float rad      = angleDeg * Mathf.Deg2Rad;
            Vector3 gemW   = GetGemWorldPos(rad);

            Color col = SamplePalette(k_PalBurst, GetPalettePos(idx));

            var particles = new ParticleSystem.Particle[18];
            for (int p = 0; p < particles.Length; p++)
            {
                float a    = Random.value * Mathf.PI * 2f;
                float dist = Random.Range(16f, 36f);
                Vector3 from = gemW + new Vector3(Mathf.Cos(a) * dist, Mathf.Sin(a) * dist, Random.Range(-1f, 1f));
                Vector3 vel  = (gemW - from).normalized * Random.Range(50f, 90f);
                float life   = dist / vel.magnitude;

                particles[p].position          = from;
                particles[p].velocity          = vel;
                particles[p].remainingLifetime = life;
                particles[p].startLifetime     = life + 0.01f;
                particles[p].startSize         = Random.Range(2.5f, 5f);
                particles[p].startColor        = col;
            }

            if (!_psConv.isPlaying) _psConv.Play();
            _psConv.SetParticles(particles, particles.Length);
        }

        // ── 粒子：宝石间连接火花 ───────────────────────────────────────────

        private void UpdateConnSparks(int activeSeg)
        {
            if (_psConn == null || activeSeg < 2) return;
            int newest = activeSeg - 1;
            int prev   = (newest - 1 + SEGMENTS) % SEGMENTS;

            float radN = (90f - (newest + 0.5f) * (360f / SEGMENTS)) * Mathf.Deg2Rad;
            float radP = (90f - (prev   + 0.5f) * (360f / SEGMENTS)) * Mathf.Deg2Rad;
            Vector3 mid = (GetGemWorldPos(radN) + GetGemWorldPos(radP)) * 0.5f;

            _psConn.transform.position = mid;
            var main = _psConn.main;
            float midPos = Mathf.Lerp(GetPalettePos(prev), GetPalettePos(newest), 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                SamplePalette(k_PalBright, midPos));
            if (!_psConn.isPlaying) _psConn.Play();
            _psConn.Emit(5);
        }

        /// <summary>
        /// 外圈光环：设目标 fill 和颜色，Update 里匀速驱动，旋转独立持续。
        /// </summary>
        /// <summary>光环只更新颜色目标，fillAmount 由 Update 自主驱动。</summary>
        private void UpdateRingArc(int activeSeg)
        {
            if (activeSeg == 0)
            {
                _ringTargetColor = new Color(0.55f, 0.75f, 1.00f, 0.95f);
                return;
            }
            float palPos = GetPalettePos(activeSeg - 1);
            Color gc     = GetNormalisedColor(palPos);
            float alpha  = Mathf.Lerp(0.95f, 1.00f, (activeSeg - 1) / (float)(SEGMENTS - 1));
            _ringTargetColor = new Color(gc.r, gc.g, gc.b, alpha);
        }

        private Vector3 GetGemWorldPos(float rad)
        {
            return transform.TransformPoint(new Vector3(
                Mathf.Cos(rad) * RING_RADIUS,
                Mathf.Sin(rad) * RING_RADIUS, 0f));
        }

        // ── Coroutines ────────────────────────────────────────────────────

        private IEnumerator FadeLightBurst(Light l,
            float fi, float ti, float fr, float tr, float dur)
        {
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(e / dur), 2f);
                if (l != null) { l.intensity = Mathf.Lerp(fi, ti, t); l.range = Mathf.Lerp(fr, tr, t); }
                yield return null;
            }
            if (l != null) { l.intensity = ti; l.range = tr; }
        }

        // ── 构建：暗灰底图（12 个独立分段，宝石亮起时对应槽隐藏）────────────

        private void BuildDimBase()
        {
            var shader = Shader.Find("FWTCG/EnergyGemUI");
            _dimImages = new Image[SEGMENTS];
            _dimMats   = new Material[SEGMENTS];

            for (int i = 0; i < SEGMENTS; i++)
            {
                var go = new GameObject($"GemDimBase_{i:00}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localEulerAngles = new Vector3(0f, 0f, -i * (360f / SEGMENTS));

                var img = go.GetComponent<Image>();
                img.sprite        = _fillSprite;
                img.type          = Image.Type.Filled;
                img.fillMethod    = Image.FillMethod.Radial360;
                img.fillOrigin    = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount    = 1f / SEGMENTS;
                img.raycastTarget = false;
                img.color         = Color.white;

                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.SetColor("_HdrColor",       new Color(0.15f, 0.19f, 0.31f, 1f));
                    mat.SetFloat("_LumMultiplier",  1.4f);
                    mat.SetFloat("_LumFloor",       0.05f);
                    mat.SetFloat("_DistortSpeed",   0f);
                    mat.SetFloat("_DistortStrength",0f);
                    img.material  = mat;
                    _dimMats[i]   = mat;
                }
                else
                {
                    img.color = new Color(0.20f, 0.20f, 0.25f, 0.40f);
                }

                go.SetActive(true);   // 初始全部显示
                _dimImages[i] = img;
            }
        }

        // ── 构建：12 段宝石图像 ────────────────────────────────────────────

        private void BuildSegImages()
        {
            _segImages     = new Image[SEGMENTS];
            _segMats       = new Material[SEGMENTS];
            _segActive     = new bool[SEGMENTS];
            _segAnims      = new Coroutine[SEGMENTS];
            _segAppearTime = new float[SEGMENTS];

            var shader = Shader.Find("FWTCG/EnergyGemUI");

            for (int i = 0; i < SEGMENTS; i++)
            {
                var go = new GameObject($"GemSeg_{i:00}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localEulerAngles = new Vector3(0f, 0f, -i * (360f / SEGMENTS));

                var img = go.GetComponent<Image>();
                img.sprite        = _fillSprite;
                img.type          = Image.Type.Filled;
                img.fillMethod    = Image.FillMethod.Radial360;
                img.fillOrigin    = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount    = 1f / SEGMENTS;
                img.raycastTarget = false;
                img.color         = Color.white;  // alpha 由 Image.color.a 控制，颜色由 _HdrColor 控制

                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.SetColor("_HdrColor",        k_DimBase);
                    mat.SetFloat("_DistortSpeed",    0.18f + Random.value * 0.12f);   // 0.18–0.30
                    mat.SetFloat("_DistortStrength", 0.028f + Random.value * 0.018f); // 0.028–0.046
                    mat.SetFloat("_DistortScale",    3.8f  + Random.value * 1.2f);    // 3.8–5.0
                    // 随机 noise 相位偏移：让每颗宝石有独立的扰动纹路
                    mat.SetVector("_NoiseOffset", new Vector4(
                        Random.Range(-8f, 8f), Random.Range(-8f, 8f), 0f, 0f));
                    img.material = mat;
                    _segMats[i]  = mat;
                }

                go.SetActive(false);
                _segImages[i] = img;
            }
        }

        // ── 构建：per-gem Point Lights ────────────────────────────────────

        private void BuildGemLights()
        {
            _gemLights = new Light[SEGMENTS];
            for (int i = 0; i < SEGMENTS; i++)
            {
                float ang = 90f - (i + 0.5f) * (360f / SEGMENTS);
                float rad = ang * Mathf.Deg2Rad;

                var go = new GameObject($"GemLight_{i:00}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    Mathf.Cos(rad) * RING_RADIUS,
                    Mathf.Sin(rad) * RING_RADIUS, -2f);

                var l        = go.AddComponent<Light>();
                l.type       = LightType.Point;
                l.color      = Color.white;
                l.intensity  = 0f;
                l.range      = 8f;
                l.shadows    = LightShadows.None;
                l.renderMode = LightRenderMode.Auto;
                l.enabled    = false;
                _gemLights[i] = l;
            }
        }

        // ── 构建：外圈光环（Radial Fill 随宝石延展，贴着宝石外缘，兼任漏光遮挡）──

        // 纹理坐标系：d = 2 × UV距离中心。UV 0.40 = 宝石外缘 = 石头框架内边
        // 贴着内：环内边紧贴 UV 0.40（宝石刚结束的地方），外边 UV 约 0.46
        private const float ARC_TEX_INNER_R  = 0.80f;   // UV 0.40 — 贴着宝石外缘
        private const float ARC_TEX_OUTER_R  = 0.925f;  // UV 0.4625 — 薄环外边
        // 内侧硬边宽度（绝对不透明，挡住漏光溢出）
        private const float ARC_TEX_HARD_W   = 0.04f;   // UV 0.02

        private void BuildRingArc()
        {
            // 内侧硬边纹理：完全不透明挡住漏光，外侧柔和过渡保持美观
            _ringArcTex = CreateRingTexture(256, ARC_TEX_INNER_R, ARC_TEX_OUTER_R, ARC_TEX_HARD_W);
            var sprite  = Sprite.Create(_ringArcTex,
                new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f), 100f);

            var go = new GameObject("GemRingArc", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            // 填满父容器：与 GemSeg / GemGlow 同 UV 坐标系
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _ringArcImg               = go.GetComponent<Image>();
            _ringArcImg.sprite        = sprite;
            _ringArcImg.type          = Image.Type.Filled;
            _ringArcImg.fillMethod    = Image.FillMethod.Radial360;
            _ringArcImg.fillOrigin    = (int)Image.Origin360.Top;
            _ringArcImg.fillClockwise = true;
            _ringArcImg.fillAmount    = 0f;
            _ringArcImg.raycastTarget = false;
            // 初始使用高 alpha 防半透明漏光
            _ringArcImg.color         = new Color(0.55f, 0.75f, 1.00f, 0.95f);
        }

        /// <summary>
        /// 生成环形纹理：内侧硬边（完全不透明挡漏光），外侧柔和过渡。
        /// d 坐标系：d = 2 × UV距离中心。
        ///   d < innerR              : alpha = 0 （宝石区透明，宝石正常可见）
        ///   innerR ≤ d < innerR+hardW : alpha = 1 （硬不透明，挡住宝石外缘漏光）
        ///   innerR+hardW ≤ d ≤ outerR : alpha 从 1 平滑过渡到 0 （外侧柔和）
        ///   d > outerR              : alpha = 0
        /// </summary>
        private static Texture2D CreateRingTexture(int size, float innerR, float outerR, float hardW)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            float half        = size * 0.5f;
            float hardEnd     = innerR + hardW;
            float fadeLen     = Mathf.Max(outerR - hardEnd, 0.0001f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);

                    float a;
                    if      (d < innerR)  a = 0f;
                    else if (d < hardEnd) a = 1f;
                    else if (d < outerR)
                    {
                        float t = 1f - (d - hardEnd) / fadeLen;
                        a = t * t * (3f - 2f * t);   // smoothstep
                    }
                    else a = 0f;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ── 构建：宝石光晕（与宝石同形状的扇形 Additive 层，天然封边不漏光）──────

        // Additive 材质（叠加混合：加亮底层）
        private Material _glowAdditiveMat;

        private void BuildGemGlows()
        {
            // 每颗宝石独立材质：EnergyGemGlowUI（Additive + gemMask），石头边框被 gemMask 裁掉，不漏光
            // 外边界裁切：shader 里 _OuterClipR 做径向 UV 裁切，不依赖 Unity Mask 组件
            // 精灵 UV 外圈实测约 0.449（459.6px / 1024px * 2）
            var glowShader = Shader.Find("FWTCG/EnergyGemGlowUI");
            if (glowShader == null) glowShader = Shader.Find("Sprites/Additive"); // 降级

            _gemGlows = new Image[SEGMENTS];
            _glowMats = new Material[SEGMENTS];
            for (int i = 0; i < SEGMENTS; i++)
            {
                var go = new GameObject($"GemGlow_{i:00}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localEulerAngles = new Vector3(0f, 0f, -i * (360f / SEGMENTS));

                var img = go.GetComponent<Image>();
                img.sprite        = _fillSprite;
                img.type          = Image.Type.Filled;
                img.fillMethod    = Image.FillMethod.Radial360;
                img.fillOrigin    = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount    = 1f / SEGMENTS;
                img.raycastTarget = false;
                img.color         = Color.white;  // shader 里用 _HdrColor 控色，Image.color 保持白

                // 独立材质：EnergyGemGlowUI（Additive + gemMask 裁边框 + 径向外裁切）
                var mat = new Material(glowShader);
                mat.SetColor("_HdrColor",        Color.clear);  // 初始透明
                mat.SetFloat("_LumMultiplier",   1.6f);
                mat.SetFloat("_LumFloor",        0f);
                mat.SetFloat("_DistortSpeed",    0f);   // glow 层不做扰动，保持简洁
                mat.SetFloat("_DistortStrength", 0f);
                mat.SetFloat("_MaskLow",         0.28f); // 提高阈值：槽缝过渡像素不发光（默认 0.12 太低）
                mat.SetFloat("_MaskHigh",        0.58f); // 相应提高上限，保留宝石晶体亮区发光
                mat.SetFloat("_OuterClipR",      0.445f);  // 外边界 UV 半径（实测 0.449，留 0.004 余量）
                mat.SetFloat("_OuterClipFeather",0.018f);  // 柔和过渡
                img.material  = mat;
                _glowMats[i]  = mat;

                go.SetActive(false);
                _gemGlows[i] = img;
            }

            _glowAdditiveMat = null;
        }

        // ── 构建：粒子系统 ────────────────────────────────────────────────

        private void BuildParticleSystems()
        {
            // 粒子系统暂停：URP particle shader 兼容性问题导致粉色大方块
            // 视觉效果由 per-gem Point Light + EnergyGemUI shader 提供，已足够
            _psConv = null;
            _psConn = null;
        }

        // ── 初始化 ────────────────────────────────────────────────────────

        private void InitFills()
        {
            // 光环初始（从 0 开始，随宝石延展）
            // _ringTargetFill 已移除
            _ringTargetColor = new Color(0.55f, 0.75f, 1.00f, 0.95f);
            if (_ringArcImg != null)
            {
                _ringArcImg.rectTransform.localEulerAngles = Vector3.zero;
                _ringArcImg.color = _ringTargetColor;
                _ringArcImg.gameObject.SetActive(false);  // 初始隐藏，等第一颗宝石出现才显示
            }

            if (_segImages == null) return;
            for (int i = 0; i < SEGMENTS; i++)
            {
                _segActive[i]     = false;
                _segAppearTime[i] = 0f;
                if (_segAnims[i] != null) { StopCoroutine(_segAnims[i]); _segAnims[i] = null; }
                if (_segImages[i] != null) _segImages[i].gameObject.SetActive(false);
                if (_segMats[i]   != null) _segMats[i].SetColor("_HdrColor", k_DimBase);
                if (_gemGlows     != null && i < _gemGlows.Length  && _gemGlows[i]  != null)
                    _gemGlows[i].gameObject.SetActive(false);
                if (_gemLights    != null && i < _gemLights.Length && _gemLights[i] != null)
                    _gemLights[i].enabled = false;
                // 暗灰槽初始全部显示
                if (_dimImages    != null && i < _dimImages.Length && _dimImages[i]  != null)
                    _dimImages[i].gameObject.SetActive(true);
            }
        }

        // ── 资源 ──────────────────────────────────────────────────────────

        private void LoadFillSprite()
        {
            _fillSprite = Resources.Load<Sprite>("UI/Generated/countdown_blue");
            if (_fillSprite == null)
                _fillSprite = Resources.Load<Sprite>("UI/Generated/countdown_empty");
        }

        /// <summary>用 countdown_ring（金色符文整圆）替换 baseRing 贴图，装饰感更强。</summary>
        private void UpgradeBaseRingSprite()
        {
            if (baseRing == null) return;
            var ringSprite = Resources.Load<Sprite>("UI/Generated/countdown_ring");
            if (ringSprite != null)
                baseRing.sprite = ringSprite;
        }

        private void CleanRuntimeChildren()
        {
            // 先释放旧材质（new Material() 不会随 GO 自动销毁）
            if (_ringArcMat      != null) { DestroyImmediate(_ringArcMat);      _ringArcMat      = null; }
            if (_glowAdditiveMat != null) { DestroyImmediate(_glowAdditiveMat); _glowAdditiveMat = null; }
            if (_ringArcTex      != null) { DestroyImmediate(_ringArcTex);      _ringArcTex      = null; }
            if (_segMats != null)
                for (int i = 0; i < _segMats.Length; i++)
                    if (_segMats[i] != null) { DestroyImmediate(_segMats[i]); _segMats[i] = null; }
            if (_dimMats != null)
                for (int i = 0; i < _dimMats.Length; i++)
                    if (_dimMats[i] != null) { DestroyImmediate(_dimMats[i]); _dimMats[i] = null; }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var name = transform.GetChild(i).name;
                if (name.StartsWith("GemSeg_")    || name.StartsWith("GemLight_") ||
                    name.StartsWith("GemDimBase")  || name.StartsWith("GemGlow_")   ||
                    name.StartsWith("GemGlowBlocker") ||
                    name.StartsWith("GemRingArc")  ||
                    name.StartsWith("GemConverge") || name.StartsWith("GemConn"))
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }
            // 清理场景根级的粒子系统残留
            var scenePS = new string[] { "GemConverge", "GemConn" };
            foreach (var n in scenePS)
            {
                var found = GameObject.Find(n);
                if (found != null) DestroyImmediate(found);
            }
        }

        private void OnDestroy()
        {
            if (_ringArcMat      != null) { Destroy(_ringArcMat);      _ringArcMat      = null; }
            if (_glowAdditiveMat != null) { Destroy(_glowAdditiveMat); _glowAdditiveMat = null; }
            if (_ringArcTex      != null) { Destroy(_ringArcTex);      _ringArcTex      = null; }
            if (_segMats != null)
                for (int i = 0; i < _segMats.Length; i++)
                    if (_segMats[i] != null) { Destroy(_segMats[i]); _segMats[i] = null; }
            if (_dimMats != null)
                for (int i = 0; i < _dimMats.Length; i++)
                    if (_dimMats[i] != null) { Destroy(_dimMats[i]); _dimMats[i] = null; }
        }
    }
}
