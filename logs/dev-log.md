# 开发日志 — FWTCG Unity 移植

---

## Phase 1: 需求挖掘 + 深度扫描 — 2026-03-28

**Status**: ✅ Completed

**What was done**:
- 完成 Grill Me 7个决策点确认（Unity新工程/uGUI/倒计时/音频暂跳/4K分辨率/双卡组/优先级顺序）
- 完成三项深度扫描（硬编码数值 / 数据配置 / 交互流程链）
- 生成完整功能清单（9大模块，100+项）
- 创建 deep-scan-results.md / feature-checklist.md / tech-debt.md / known-bugs.md

**Decisions made**:
- UI 系统：uGUI + DOTween（原版有 3D 倾斜效果，uGUI 最适配）
- 分辨率：1920×1080 基准，Scale With Screen Size，支持 4K
- 音频：暂时跳过，后续再处理
- 开发顺序：engine → cards → spell → ui → ai → legend → particles
- 两套英雄卡组全部移植（卡莎虚空 + 易大师伊欧尼亚）
- 回合倒计时 30 秒保留

**Technical debt**: 无

**Problems encountered**: 无

---

## Phase 2: 视觉分析 + 视觉升级指南 — 2026-03-28

**Status**: ✅ Completed

**What was done**:
- 读取全部 5 个 CSS 文件（16,000+ 行）及 particles.js / 3d-tilt.js / dragAnim.js 视觉部分
- 完成视觉元素分类（A直接移植3项 / B原版Hack需升级8项 / C原版缺失2项）
- 生成 visual-checklist.md（12大类，60+项）
- 生成 visual-upgrade-FWTCG.md（效果逐一对照，含实施顺序和资源清单）

**Decisions made**:
- URP Post Processing（Bloom/Vignette/Color Grade/Film Grain）全部启用
- 3D 倾斜从 CSS perspective hack 升级为真实 RectTransform 3D 旋转
- 玻璃态简化方案：半透明深色 Panel 替代真模糊（性能优先）
- DOTween 统一替代全部 60+ CSS @keyframes 动画
- Shader Graph 处理发光边框 / 彗星边框 / 背景纹理

**Technical debt**: 无新增

**Problems encountered**: 无
