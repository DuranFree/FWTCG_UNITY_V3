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
        private const float SEG_WAVE_OFF  = 0.07f;
        private const float RING_RADIUS   = 100f;

        // ── 调色板：灰→绿→青→蓝→黄→橙→红（HDR Bright 值，Dim = 0.45×）
        // index 0 = progress 0%，index 11 = progress 100%
        private static readonly Color[] k_PalBright = new Color[]
        {
            new Color(0.06f, 0.06f, 0.07f, 1f),  //  0 gray       (empty feel)
            new Color(0.18f, 0.08f, 0.22f, 1f),  //  1 deep-gray  (transition)
            new Color(0.30f, 1.80f, 0.40f, 1f),  //  2 green
            new Color(0.22f, 2.20f, 1.00f, 1f),  //  3 cyan-green
            new Color(0.18f, 1.40f, 2.80f, 1f),  //  4 cyan
            new Color(0.18f, 0.70f, 2.80f, 1f),  //  5 blue
            new Color(0.22f, 0.48f, 2.40f, 1f),  //  6 deep-blue
            new Color(1.80f, 1.60f, 0.18f, 1f),  //  7 yellow
            new Color(2.60f, 1.10f, 0.10f, 1f),  //  8 amber
            new Color(3.20f, 0.72f, 0.07f, 1f),  //  9 orange
            new Color(4.20f, 0.45f, 0.05f, 1f),  // 10 red-orange
            new Color(5.00f, 0.28f, 0.03f, 1f),  // 11 red
        };
        private static readonly Color[] k_PalDim;
        private static readonly Color[] k_PalBurst;
        private static readonly Color   k_DimBase  = new Color(0.04f, 0.04f, 0.05f, 1f);
        private static readonly Color   k_MegaBurst = new Color(12f, 10f, 6f, 1f);

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

        // ── 运行时状态 ────────────────────────────────────────────────────
        private float         _curProgress  = 0f;
        private bool          _isBreathing  = false;
        private float         _breathStart  = 0f;
        private bool          _megaFlashed  = false;

        private Image[]       _segImages;   // 12 段宝石图像
        private Material[]    _segMats;     // 对应材质（Material.SetColor 走这里）
        private bool[]        _segActive;
        private Coroutine[]   _segAnims;

        private Image         _dimBase;     // 暗灰底图（始终显示宝石槽轮廓，标准 Image 无自定义 shader）

        private Light[]       _gemLights;

        private ParticleSystem _psConv;     // 粒子汇聚
        private ParticleSystem _psConn;     // 宝石间连接
        private Sprite         _fillSprite;

        // ── 生命周期 ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (!Application.isPlaying) return;
            CleanRuntimeChildren();
            LoadFillSprite();
            BuildDimBase();       // 最先：底部暗灰宝石层
            BuildSegImages();
            BuildGemLights();
            BuildParticleSystems();
            InitFills();
        }

        private void Update()
        {
            if (!_isBreathing) return;
            float now = Time.time;

            for (int i = 0; i < SEGMENTS; i++)
            {
                if (!_segActive[i] || _segMats[i] == null) continue;
                if (_segAnims[i] != null) continue;

                float t = BreathT(now, i * SEG_WAVE_OFF);
                _segMats[i].SetColor("_HdrColor", SampleLerp(GetPalettePos(i), t));
            }

            SyncGemLights(BreathT(now, 0f));
        }

        // ── Public API ────────────────────────────────────────────────────

        public void SetProgress(float progress)
        {
            _curProgress = progress = Mathf.Clamp01(progress);
            int activeSeg = Mathf.RoundToInt(progress * SEGMENTS);

            for (int i = 0; i < SEGMENTS; i++)
            {
                bool should = i < activeSeg;
                if (should  && !_segActive[i]) OnSegAppear(i);
                if (!should && _segActive[i])  OnSegHide(i);
            }

            UpdateConnSparks(activeSeg);

            if (activeSeg > 0 && !_isBreathing) StartBreathing();
            if (activeSeg == 0) StopBreathing();

            // 12/12 满载时爆闪一次
            if (activeSeg == SEGMENTS && !_megaFlashed)
            {
                _megaFlashed = true;
                StartCoroutine(MegaFlashSequence());
            }
        }

        public void ResetRing()
        {
            _curProgress  = 0f;
            _megaFlashed  = false;
            StopBreathing();
            if (_segImages == null) return;
            for (int i = 0; i < SEGMENTS; i++)
            {
                _segActive[i] = false;
                if (_segAnims[i] != null) { StopCoroutine(_segAnims[i]); _segAnims[i] = null; }
                if (_segImages[i] != null) _segImages[i].gameObject.SetActive(false);
                if (_segMats[i]   != null) _segMats[i].SetColor("_HdrColor", k_DimBase);
                if (_gemLights    != null && i < _gemLights.Length && _gemLights[i] != null)
                    _gemLights[i].enabled = false;
            }
        }

        // ── 宝石出现 / 隐藏 ───────────────────────────────────────────────

        private void OnSegAppear(int idx)
        {
            _segActive[idx] = true;
            var img = _segImages[idx];
            if (img == null) return;

            img.gameObject.SetActive(true);
            _segMats[idx].SetColor("_HdrColor", k_DimBase);

            if (_gemLights != null && idx < _gemLights.Length)
                _gemLights[idx].enabled = true;

            EmitConvergeParticles(idx);

            if (_segAnims[idx] != null) StopCoroutine(_segAnims[idx]);
            _segAnims[idx] = StartCoroutine(SegAppearAnim(idx));
        }

        private void OnSegHide(int idx)
        {
            _segActive[idx] = false;
            if (_segAnims[idx] != null) { StopCoroutine(_segAnims[idx]); _segAnims[idx] = null; }
            if (_segImages[idx] != null) _segImages[idx].gameObject.SetActive(false);
            if (_gemLights != null && idx < _gemLights.Length && _gemLights[idx] != null)
                _gemLights[idx].enabled = false;
        }

        // ── 宝石出现动画：暗 → 爆发 → 稳定 ──────────────────────────────

        private IEnumerator SegAppearAnim(int idx)
        {
            float  palPos  = GetPalettePos(idx);
            Color  burst   = SamplePalette(k_PalBurst,  palPos);
            Color  bright  = SamplePalette(k_PalBright, palPos);
            var    mat     = _segMats[idx];

            // 阶段1：暗 → 爆发（0.07s，快速点亮）
            float e = 0f;
            while (e < 0.07f)
            {
                e += Time.deltaTime;
                if (mat != null) mat.SetColor("_HdrColor", Color.Lerp(k_DimBase, burst, e / 0.07f));
                yield return null;
            }
            if (mat != null) mat.SetColor("_HdrColor", burst);

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
                if (mat != null) mat.SetColor("_HdrColor", Color.Lerp(burst, bright, t));
                yield return null;
            }
            if (mat != null) mat.SetColor("_HdrColor", bright);

            _segAnims[idx] = null;
        }

        // ── 满载爆闪 → 慢慢暗淡回灰 ─────────────────────────────────────

        private IEnumerator MegaFlashSequence()
        {
            StopBreathing();

            // 全灯爆亮
            if (_gemLights != null)
                foreach (var gl in _gemLights)
                    if (gl != null && gl.enabled)
                    { gl.intensity = 10f; gl.range = 18f; gl.color = Color.white; }

            // 所有段瞬间跳到 MegaBurst
            for (int i = 0; i < SEGMENTS; i++)
                if (_segMats[i] != null) _segMats[i].SetColor("_HdrColor", k_MegaBurst);

            // 保持 3 帧
            yield return null;
            yield return null;
            yield return null;

            // 从 MegaBurst → 各自的 Bright（0.25s）
            float e = 0f;
            while (e < 0.25f)
            {
                e += Time.deltaTime;
                float t = Mathf.Clamp01(e / 0.25f);
                for (int i = 0; i < SEGMENTS; i++)
                {
                    if (_segMats[i] == null) continue;
                    Color bright = SamplePalette(k_PalBright, GetPalettePos(i));
                    _segMats[i].SetColor("_HdrColor", Color.Lerp(k_MegaBurst, bright, t));
                }
                yield return null;
            }

            // 然后慢慢暗淡到灰（1.5s）
            e = 0f;
            while (e < 1.5f)
            {
                e += Time.deltaTime;
                float t = Mathf.Clamp01(e / 1.5f);
                float lightIntensity = Mathf.Lerp(1.5f, 0f, t);
                for (int i = 0; i < SEGMENTS; i++)
                {
                    if (_segMats[i] == null) continue;
                    Color bright = SamplePalette(k_PalBright, GetPalettePos(i));
                    _segMats[i].SetColor("_HdrColor", Color.Lerp(bright, k_DimBase, t));
                }
                if (_gemLights != null)
                    foreach (var gl in _gemLights)
                        if (gl != null && gl.enabled)
                        { gl.intensity = lightIntensity; gl.range = Mathf.Lerp(8f, 4f, t); }
                yield return null;
            }

            // 全部归灰
            for (int i = 0; i < SEGMENTS; i++)
                if (_segMats[i] != null) _segMats[i].SetColor("_HdrColor", k_DimBase);
        }

        // ── 颜色辅助 ──────────────────────────────────────────────────────

        private float GetPalettePos(int segIdx)
        {
            float basePos   = _curProgress;
            float segOffset = ((float)segIdx / (SEGMENTS - 1) - 0.5f) * 0.18f;
            return Mathf.Clamp01(basePos + segOffset);
        }

        private float BreathT(float time, float phaseOff)
            => (Mathf.Sin((time - _breathStart + phaseOff)
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

        // ── 呼吸 ──────────────────────────────────────────────────────────

        private void StartBreathing() { _breathStart = Time.time; _isBreathing = true; }
        private void StopBreathing()  => _isBreathing = false;

        // ── 宝石灯同步 ─────────────────────────────────────────────────────

        private void SyncGemLights(float breathT)
        {
            if (_gemLights == null) return;
            float intensity = Mathf.Lerp(0.5f, 2.8f, breathT);
            float range     = Mathf.Lerp(6f, 10f, breathT);
            for (int i = 0; i < SEGMENTS; i++)
            {
                if (_gemLights[i] == null || !_gemLights[i].enabled) continue;
                _gemLights[i].color     = GetLightColor(GetPalettePos(i));
                _gemLights[i].intensity = intensity;
                _gemLights[i].range     = range;
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

        // ── 构建：暗灰底图（始终显示宝石槽）──────────────────────────────

        private void BuildDimBase()
        {
            var go = new GameObject("GemDimBase", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _dimBase = go.GetComponent<Image>();
            _dimBase.sprite        = _fillSprite;   // countdown_blue.png — 宝石槽轮廓
            _dimBase.type          = Image.Type.Simple;
            _dimBase.raycastTarget = false;
            // 标准 UI shader（不用 EnergyGemUI，避免 LumFloor 把透明区变实心）
            // 暗灰色 + 低透明度 → 只显示宝石槽轮廓，不遮挡石头底座
            _dimBase.color = new Color(0.22f, 0.22f, 0.28f, 0.45f);
        }

        // ── 构建：12 段宝石图像 ────────────────────────────────────────────

        private void BuildSegImages()
        {
            _segImages = new Image[SEGMENTS];
            _segMats   = new Material[SEGMENTS];
            _segActive = new bool[SEGMENTS];
            _segAnims  = new Coroutine[SEGMENTS];

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
                    mat.SetColor("_HdrColor",     k_DimBase);
                    mat.SetFloat("_DistortSpeed",    0.22f);
                    mat.SetFloat("_DistortStrength", 0.032f);
                    mat.SetFloat("_DistortScale",    4.2f);
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

        // ── 构建：粒子系统 ────────────────────────────────────────────────

        private void BuildParticleSystems()
        {
            _psConv = BuildPS("GemConverge", 200, sortOrder: 100);
            var main = _psConv.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            main.startSize     = new ParticleSystem.MinMaxCurve(2f, 5f);
            var em = _psConv.emission; em.enabled = false;
            var sh = _psConv.shape;    sh.enabled = false;
            SetFadeAlpha(_psConv);

            _psConn = BuildPS("GemConn", 64, sortOrder: 99);
            var main2 = _psConn.main;
            main2.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.40f);
            main2.startSpeed    = new ParticleSystem.MinMaxCurve(10f, 25f);
            main2.startSize     = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            var sh2 = _psConn.shape;
            sh2.enabled = true; sh2.shapeType = ParticleSystemShapeType.Circle; sh2.radius = 4f;
            SetFadeAlpha(_psConn);
        }

        private ParticleSystem BuildPS(string goName, int maxParts, int sortOrder)
        {
            // ⚠️ 放在 Canvas 父物体同级（场景根级），而非 Canvas 子节点
            // → 作为 World-space 对象，通过 sortingOrder 高于 Canvas 渲染在 UI 之上
            var go = new GameObject(goName);
            go.transform.SetParent(null, false);  // 场景根，非 Canvas 子

            var ps   = go.AddComponent<ParticleSystem>();
            var rend = go.GetComponent<ParticleSystemRenderer>();

            // 排序设置：高于 Canvas 默认 0，确保粒子在 UI 之上
            rend.sortingLayerName = "Default";
            rend.sortingOrder     = sortOrder;

            // 材质：按 URP / 内置管线优先级寻找可用 shader，找不到就用系统默认
            var psShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Particles/Simple Lit")
                        ?? Shader.Find("Particles/Standard Unlit")
                        ?? Shader.Find("Sprites/Default");
            if (psShader != null)
                rend.material = new Material(psShader);
            // 找不到任何 shader → 保留 rend 默认材质，不会出白方块

            var main = ps.main;
            main.loop            = false;
            main.playOnAwake     = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = maxParts;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private void SetFadeAlpha(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 0.9f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        // ── 初始化 ────────────────────────────────────────────────────────

        private void InitFills()
        {
            if (_segImages == null) return;
            for (int i = 0; i < SEGMENTS; i++)
            {
                _segActive[i] = false;
                if (_segAnims[i] != null) { StopCoroutine(_segAnims[i]); _segAnims[i] = null; }
                if (_segImages[i] != null) _segImages[i].gameObject.SetActive(false);
                if (_segMats[i]   != null) _segMats[i].SetColor("_HdrColor", k_DimBase);
                if (_gemLights    != null && i < _gemLights.Length && _gemLights[i] != null)
                    _gemLights[i].enabled = false;
            }
        }

        // ── 资源 ──────────────────────────────────────────────────────────

        private void LoadFillSprite()
        {
            _fillSprite = Resources.Load<Sprite>("UI/Generated/countdown_blue");
            if (_fillSprite == null)
                _fillSprite = Resources.Load<Sprite>("UI/Generated/countdown_empty");
        }

        private void CleanRuntimeChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var name = transform.GetChild(i).name;
                if (name.StartsWith("GemSeg_")    || name.StartsWith("GemLight_") ||
                    name.StartsWith("GemDimBase")  || name.StartsWith("GemConverge") ||
                    name.StartsWith("GemConn"))
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
    }
}
