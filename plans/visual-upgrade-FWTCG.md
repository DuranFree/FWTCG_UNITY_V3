# Visual Upgrade Guide: FWTCG
> Source platform: Web JS (HTML/CSS/Canvas)
> Target platform: Unity 2022.3 / URP / uGUI
> Generated from: styles.css, battle-enhance.css, title-enhance.css, 3d-enhance.css, equipment.css, particles.js, 3d-tilt.js, dragAnim.js

---

## Summary

FWTCG 原版是一个高品质的 Hextech 主题卡牌游戏界面，以深海军蓝+金色+青色为调色板，风格参考英雄联盟暗黑奇幻美学。移植到 Unity URP 后，CSS 的模拟发光效果将被 URP Bloom 替代，3D 倾斜从 CSS perspective hack 升级为真实 3D 旋转，JS Canvas 粒子升级为 GPU 加速粒子系统，视觉品质整体提升。

---

## Color Palette

| 变量名 | Hex / RGBA | 用途 |
|--------|-----------|------|
| Gold | #c8aa6e | 主要强调色：边框、装饰、UI |
| GoldLight | #f0e6d2 | 文字高亮、发光强调 |
| GoldDark | #785a28 | 暗金，次要强调 |
| GoldMuted | #463714 | 低调调子 |
| Teal | #0ac8b9 | Hextech 青色，副色 |
| TealDim | #005a82 | 暗青 |
| TealGlow | rgba(10,200,185,0.35) | 发光/透明变体 |
| Background | #010a13 | 主背景（深海军蓝，含色调）|
| BackgroundMid | #0a1428 | 中调背景 |
| BackgroundPanel | rgba(1,10,19,0.88) | 半透明面板 |
| EnemyRed | #e84057 / #f87171 | 敌方/警告红 |
| PlayerGreen | #40e88a / #4ade80 | 玩家/正向绿 |
| TextMuted | #a09b8c | 默认文字（米色）|
| TextDim | #5b5a56 | 弱化文字 |
| MagicBlue | rgba(60,140,255,1) | 漩涡/法术蓝 |
| MagicBlueLight | rgba(96,165,250,1) | 战场卡发光蓝 |
| RuneFire | rgba(255,120,40,0.7) | 炽烈符文橙 |
| RuneGold | rgba(240,210,120,0.75) | 灵光符文金 |
| RuneGreen | rgba(80,210,120,0.7) | 翠意符文绿 |
| RuneRed | rgba(220,60,80,0.7) | 摧破符文红 |
| RunePurple | rgba(160,80,220,0.7) | 混沌符文紫 |
| RuneBlue | rgba(80,180,240,0.7) | 序理符文蓝 |

**Unity 实现：** 全部定义在 `GameColors.cs` 静态类，HDR 颜色用于 Bloom 触发（亮度 > 1.0）

---

## Layer Architecture（渲染层级，从底到顶）

```
0  背景渐变（Camera 背景色 + Skybox 关闭）
1  六边形网格纹理（Quad + Shader Graph）
2  噪点纹理（Quad + Shader Graph fBM noise）
3  分层光晕（多个 Quad + 透明材质，径向渐变）
4  战场区域（Game Object 层）
5  单位/卡牌 Sprite（SpriteRenderer / Image）
6  装备标签（UI Canvas World Space）
7  全息光泽（Card Shader Graph ::before 等效）
8  粒子效果（Particle System，Sorting Layer: Particles）
9  拖拽 Ghost 卡（Overlay Canvas，最高优先级）
10 全屏 UI（Screen Space Overlay Canvas）
11 后处理（URP Post Processing Volume）
```

---

## Effect-by-Effect Breakdown

---

### 1. 卡牌发光边框 / Glow
**Original implementation**: CSS `box-shadow` 多层叠加，最多 8 层
**Original limitation**: B — box-shadow 是 2D 假发光，不支持 HDR，不与场景交互
**Target platform solution**: URP Bloom（Post Processing Volume）+ Shader Graph `_EmissionColor`（HDR）
**Implementation notes**:
- Bloom Intensity: 0.4，Threshold: 0.9
- 卡牌发光材质：`_EmissionColor = GoldGlow × HDR 强度`
- 悬停时提升 `_EmissionStr` 参数（DOTween float tween）
**Priority**: High

---

### 2. 3D 倾斜效果
**Original implementation**: CSS `perspective(800px) rotateX(var(--tilt-x)) rotateY(var(--tilt-y))`，JS 计算鼠标偏移
**Original limitation**: B — CSS perspective hack，锯齿，不是真 3D
**Target platform solution**: RectTransform.localRotation（真实 3D 旋转）+ uGUI Canvas 开启 `Pixel Perfect: Off`
**Implementation notes**:
- MAX_TILT = 18°，LERP_IN = 0.12，LERP_OUT = 0.08
- `Update()` 中读取 `Input.mousePosition`，计算卡牌内偏移比例
- `Quaternion.Lerp` 到目标旋转，不用 DOTween（需要每帧更新）
- 全息光泽：Shader Graph 中用鼠标 UV 偏移驱动 radial gradient overlay
**Priority**: High

---

### 3. 背景漂浮粒子（55个）
**Original implementation**: JS Canvas 手写，requestAnimationFrame 循环
**Original limitation**: B — CPU 单线程 Canvas 绘制，性能有限
**Target platform solution**: Unity Particle System（GPU Instanced）
**Implementation notes**:
- 颜色：7色随机（Gold/Teal/LightBlue/Purple/TealLight/Green/WarmGold）
- 大小：0.004-0.025f（世界空间），12% 为大粒子
- 速度：上浮 0.004-0.028，水平 ±0.0035
- 寿命：0.25-0.95s
- 发光：小粒子 4x Additive Blend，大粒子 6x
- Sorting Layer: Background Particles
**Priority**: Medium

---

### 4. 背景六边形网格 + 噪点纹理
**Original implementation**: CSS `repeating-linear-gradient` + SVG `feTurbulence` 噪点
**Original limitation**: C — CSS 无法做真实程序化纹理，静态效果
**Target platform solution**: Shader Graph（程序化）
**Implementation notes**:
- Hexagon Grid：Shader Graph Hexagon Tiling 节点（或自定义 UV 分割）
- Noise：Shader Graph Simple Noise / Gradient Noise 节点
- 整体透明度极低（hexagon: 0.04，noise: 3.5%），放在背景 Quad 上
- 背景 Quad 使用 Unlit Transparent Shader Graph
**Priority**: Medium

---

### 5. 漩涡/传送门拖拽特效
**Original implementation**: 3层 CSS 同心圆 div + `@keyframes` 旋转 + CSS 粒子
**Original limitation**: B — DOM 创建/销毁消耗大，CSS 限制表现上限
**Target platform solution**: Prefab（3个旋转 Ring Image + 10个轨道粒子 Particle System）+ DOTween
**Implementation notes**:
- Ring1: 70px，蓝边实线，DOTween Rotate 360° 2.4s infinite
- Ring2: 50px，蓝边虚线，DOTween Rotate -360° 1.8s infinite（反向）
- Ring3: 34px，蓝边实线，DOTween Rotate 360° 1.3s + scale pulse
- 轨道粒子：10个，Particle System circular orbit，蓝色发光，1.6-2.4s
- 淡入：DOTween alpha 0→1，0.28s，Ease.OutBack
- 淡出：DOTween alpha 1→0，0.22s，Ease.InQuad
**Priority**: High

---

### 6. CSS 动画 → DOTween
**Original implementation**: `@keyframes` + CSS `transition`，60+ 个动画定义
**Original limitation**: B — CSS 动画不支持打断、回调、序列组合
**Target platform solution**: DOTween（所有 UI 动画统一使用）
**Implementation notes**:
- 卡牌出牌飞行：0.4s，Ease.OutBack（cubic-bezier 0.34,1.56,0.64,1 等效）
- 单位落地弹跳：0.4s，Ease.OutElastic
- 界面进场：0.3-0.5s，Ease.OutQuart
- 得分脉冲：1.8s，Ease.InOutSine（scale 1→1.12→1）
- 所有动画必须支持 `.SetId()` 用于打断
**Priority**: High

---

### 7. 玻璃态 UI（Glassmorphism）
**Original implementation**: CSS `backdrop-filter: blur(20px) saturate(1.2)`
**Original limitation**: B — backdrop-filter 支持有限，WebKit 前缀，性能差
**Target platform solution**: URP Full Screen Pass Renderer Feature（模糊 Pass）或 RenderTexture 截图模糊
**Implementation notes**:
- 仅用于主面板背景和弹窗（非每帧全屏效果）
- 推荐：Camera Stacking + Blur Render Feature（仅模糊底层相机输出）
- 简化方案：半透明深色 Image（#010a13 at 88% alpha）代替真模糊，视效接近
**Priority**: Low（简化方案优先）

---

### 8. Conic Gradient 可出牌边框彗星
**Original implementation**: CSS `@property --playable-angle` + `conic-gradient` 彗星轨道动画
**Original limitation**: C — CSS @property 只在 Chrome 支持，无法直接移植
**Target platform solution**: Shader Graph（UV 旋转 + 彗星渐变遮罩）
**Implementation notes**:
- Shader Graph：UV 坐标转极坐标 → `_Time` 驱动角度 → 彗星渐变
- 颜色：绿色 rgba(74,222,128) 尾部 → rgba(200,255,218) 白色头部
- 速度：普通 3s/圈，可出牌悬停 1.8s/圈（DOTween 修改 Shader float 参数）
- 仅在卡牌可出牌时激活材质
**Priority**: Medium

---

### 9. 后处理效果（原版缺失的）
**Original implementation**: C — 原版完全没有后处理
**Original limitation**: C — 浏览器无法做游戏级后处理
**Target platform solution**: URP Post Processing Volume（全新添加）
**Implementation notes**:
- Bloom: Intensity 0.5，Threshold 0.9，Scatter 0.7（金色/青色发光效果最佳体现）
- Color Adjustments: Saturation +10，Contrast +5（强化 Hextech 配色）
- Vignette: Intensity 0.3，Smoothness 0.4（增加沉浸感，不超过 0.4）
- Film Grain: Intensity 0.15，Luminance Contribution 0.8（胶片质感，极低强度）
**Priority**: High（最高性价比升级）

---

### 10. 粒子连线（Constellation）
**Original implementation**: JS Canvas `lineTo`，90px 内连线
**Original limitation**: B — Canvas 每帧全量重绘，CPU 消耗
**Target platform solution**: LineRenderer（动态更新）或 VFX Graph Trail
**Implementation notes**:
- 检测距离 90px（世界空间换算），动态透明度
- 颜色：Teal rgba(10,200,185)
- 线宽：0.5px 等效
- 推荐：Custom MonoBehaviour 管理，每帧更新 LineRenderer positions
**Priority**: Low

---

### 11. 字体
**Original implementation**: Google Fonts（Cinzel + Noto Sans SC），@font-face
**Original limitation**: A — 直接可用
**Target platform solution**: 将字体 .ttf 文件导入 Unity，创建 TMP_FontAsset
**Implementation notes**:
- Cinzel: 用于 TextMeshPro 标题/UI 标签
- Noto Sans SC: 用于卡牌描述/正文（支持中文字符集）
- 必须在 TextMeshPro 中生成完整中文字符集 Atlas
**Priority**: High（中文显示必须）

---

### 12. 符文发光色彩
**Original implementation**: CSS variables 动态设置每个符文的颜色
**Original limitation**: A — 可直接移植为 Shader 参数
**Target platform solution**: Shader Graph `_RuneColor`（Material Property Block per instance）
**Implementation notes**:
- 6 种符文各有一套：主色 + 发光色（HDR 版）
- 用 `MaterialPropertyBlock` 按实例设置颜色，不创建多份 Material
**Priority**: Medium

---

## Implementation Order（实施顺序）

1. **字体系统** — 中文显示是最基础的依赖
2. **GameColors.cs** — 所有颜色常量，其他一切依赖它
3. **URP Post Processing Volume** — 最高性价比，立即提升整体质感
4. **卡牌基础 Shader（发光 + 费用压暗）** — 核心视觉
5. **3D 倾斜系统** — 核心交互视觉
6. **DOTween 动画层** — 所有 UI 过渡
7. **漩涡/传送门拖拽特效** — 核心交互特效
8. **背景粒子系统** — 氛围
9. **可出牌彗星边框 Shader** — 游戏体验
10. **背景 Shader（六边形 + 噪点）** — 环境细节
11. **连线系统** — 可选，最低优先级

---

## Assets Required

| 资源 | 类型 | 来源 | 备注 |
|------|------|------|------|
| Cinzel 字体 | .ttf | Google Fonts | 需生成 TMP FontAsset |
| Noto Sans SC | .ttf | Google Fonts | 需生成含中文的 TMP FontAsset |
| 卡莎卡组图片（34张）| .png | tempPic/cards/ksha/ | 直接导入 Sprites |
| 易大师卡组图片（24张）| .png | tempPic/cards/jiansheng/ | 直接导入 Sprites |
| 卡牌背面 | .png | tempPic/cards/card_back/ | 直接导入 |
| 掷币正面 | .png | tempPic/coins/xianshou.png | 直接导入 |
| 掷币背面 | .png | tempPic/coins/houshou.png | 直接导入 |
| 战场底图 | .png | tempPic/playmat/ | 直接导入 |

---

## Known Gaps（无法完美还原的效果）

| 效果 | 问题 | 推荐方案 |
|------|------|---------|
| 玻璃态模糊 | Unity URP 实时模糊性能消耗大 | 半透明深色 Panel 替代（88% alpha #010a13）|
| SVG feTurbulence 噪点 | Unity 无 SVG 滤镜 | Shader Graph Simple Noise 近似（视效 95% 接近）|
| CSS 粒子连线 | DOM 实时检测，性能模式不同 | LineRenderer 近似，精度略降 |
