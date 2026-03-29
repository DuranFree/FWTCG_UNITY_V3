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

---

## Phase 3: 移植计划 — 2026-03-28

**Status**: ✅ Completed

**What was done**:
- 生成 port-FWTCG.md（10个开发Phase，每个均为可玩Demo）
- 完成标准调整为"用户可实际操作"而非"测试通过"
- 更新 memory 第6条规则
- DEV-1 确认为10张简单单位卡（5套×2），最小可玩Loop

**Decisions made**:
- 每个Phase = 可玩Demo，从最小可玩开始逐步扩展
- DEV-1不再拆分，10张牌是最低可玩阈值
- 视觉升级推迟到DEV-8/9，前期功能优先

**Technical debt**: 无新增

**Problems encountered**: 无

---

## DEV-1: 最小可玩 Demo（10张简单单位卡）— 2026-03-28

**Status**: ✅ Completed

**What was done**:
- 创建 Unity 工程（2022.3.62f3c1 / URP）
- 完成所有核心 C# 脚本（16个）：
  - Data: RuneType, CardData (ScriptableObject)
  - Core: GameRules, UnitInstance, BattlefieldState, GameState
  - Systems: TurnManager (6阶段 async/await), CombatSystem, ScoreManager
  - AI: SimpleAI（横置符文→出牌→移动→结束回合）
  - UI: CardView, GameUI
  - GameManager（单例，全局入口）
  - Editor: SceneBuilder（一键建场景）
- 创建 asmdef：FWTCG.Runtime, FWTCG.Editor, FWTCG.Tests.EditMode
- 通过 19 个 NUnit EditMode 逻辑测试（全绿）
- 通过批处理编译：EXIT:0（无编译错误）
- 批处理运行 SceneBuilder 生成：
  - Assets/Scenes/GameScene.unity
  - Assets/Prefabs/CardPrefab.prefab
  - Assets/Prefabs/RunePrefab.prefab
  - Assets/Resources/Cards/（10 个 CardData.asset，5 Kaisa + 5 Yi）

**Decisions made**:
- DOTween 推迟到 DEV-8（视觉升级Phase），目前不引入
- TMP Essential Resources 须用户首次打开 Unity 时手动导入（批处理模式无法弹窗）
- SimpleAI：横置所有符文 → 出1张单位 → 移动1个单位 → 结束回合

**Technical debt**:
- TMP Essential Resources 未自动导入 — 批处理模式无 GPU，TMP 无法弹窗，需用户手动操作 — DEV-1

**Problems encountered**:
- DOTween 包未找到（移除解决）
- NUnit 缺失（添加 com.unity.test-framework 解决）
- Unity 已运行冲突（Stop-Process 强制关闭解决）
- SceneBuilder 中 TMPro 命名空间找不到（在 FWTCG.Editor.asmdef 加入 Unity.TextMeshPro 解决）
- batch mode -nographics 时 TMP 尝试弹窗失败（非致命，场景仍正常创建）
- executeMethod 方法名写错（BuildGameSceneFromCommandLine → BuildGameScene）

---

## DEV-1 规则修正 + 中文字体修复 — 2026-03-28

**Status**: ✅ Completed

**What was done**:
- 将全部 UI 文字从 TextMeshProUGUI 切换为 Legacy UnityEngine.UI.Text（彻底解决 TMP batch-mode 字体问题）
- SceneBuilder 增加 Main Camera（正交投影，#010a13 背景）
- SceneBuilder 增加 EventSystem + StandaloneInputModule（修复 UI 点击全无响应）
- LoadFont() 系统字体回退：尝试 simhei.ttf / simkai.ttf / msyh.ttc，失败则用 OS 字体
- 删除 FontSetup.cs / FontFallbackSetup.cs（TMP 方案遗留，已无用）
- 从 asmdef 移除 Unity.TextMeshPro 引用
- 删除错误规则：最后1分受限（ScoreManager 整块移除）
- 删除错误规则：战场每方2单位上限（HasSlot → 始终 true）
- 删除错误规则：手牌上限7（TurnManager draw 阶段 burn 逻辑移除）
- 删除 GameRules.MAX_HAND_SIZE 和 MAX_BF_UNITS 常量
- 更新 GameStateTests.cs：移除对已删除常量的引用，改为验证无上限行为

**Decisions made**:
- TMP 彻底废弃于此 Phase，全项目用 Legacy Text，后续 DEV-8 视觉升级时再评估是否引入
- 三条错误规则均经用户游戏测试后确认并修正

**Technical debt**: 无新增

**Problems encountered**:
- TMP m_AtlasTextures 在 batch -nographics 下始终损坏（根本原因：无 GPU 无法生成 atlas）
- EventSystem 缺失导致所有 Button 无响应（添加后立即修复）
- GameStateTests.cs 引用已删除常量造成编译错误（已修复）

---

## DEV-1 战斗系统重构：延迟战斗 + 法术对决按钮 — 2026-03-28

**Status**: ✅ Completed

**What was done**:
- CombatSystem.MoveUnit() 不再立即触发战斗，改为仅移动单位+设置控制权
- 新增 CombatSystem.ResolveAllBattlefields()：回合结束时兜底解决所有有争议战场
- TurnManager.DoAction() 末尾调用 ResolveAllBattlefields 作为安全网
- GameUI 新增 bf1DuelButton / bf2DuelButton：双方有单位时显示"⚔ 开始法术对决"
- GameManager.OnDuelClicked() 处理玩家手动触发战斗，战斗后可继续操作
- SceneBuilder 在战场面板中创建 DuelButton（金色，默认隐藏）

**Decisions made**:
- 玩家可自主选择何时发起法术对决，不强制在回合结束时自动解决
- 回合结束时 ResolveAllBattlefields 作为兜底（AI回合+玩家未手动对决的战场）

**Technical debt**: 无新增

**Problems encountered**: 无

---

## DEV-1 核心规则对齐（11项规则修正）— 2026-03-29

**Status**: ✅ Completed

**What was done**:
- 基于5列规则对比表，用户逐条确认16项规则选择，11项需代码改动
- #4 伤害逐一分配 / #5 眩晕=0 / #6 双方存活→攻方召回 / #10 全场HP重置
- #3 移动自动触发战斗 / #9 空BF征服 / #8 征服条件（控制权改变+每BF一次）
- #2 最后一分规则 / #11 符文12张上限 / #13 召回行动 / #14 AI多移动
- 移除手动对决按钮（GameUI/GameManager/SceneBuilder）

**Files changed**: CombatSystem.cs, ScoreManager.cs, TurnManager.cs, UnitInstance.cs, GameRules.cs, GameManager.cs, GameUI.cs, SimpleAI.cs, SceneBuilder.cs

**Tests**: 编译通过 + 18个EditMode测试全绿

---

## DEV-1 多单位同时上场修正 — 2026-03-29

**Status**: ✅ Completed（用户验证通过）

**What was done**:
- CombatSystem.MoveUnit 拆分：仅移动，不触发战斗
- 新增 CombatSystem.CheckAndResolveCombat：移动后检查战斗（批量移动完成后才调用）
- GameManager 支持多选基地单位（_selectedBaseUnits 列表）
- OnBattlefieldClicked 改为批量移动所有选中单位，再一次性触发战斗
- CardView 新增 SetSelected()：选中单位显示绿色高亮
- GameUI.Refresh 接受选中列表，传递给 CardView
- SimpleAI 批量移动所有可移动单位到同一战场，再统一结算

**Decisions made**:
- 玩家多选（绿色高亮）→ 点击战场 → 全部上场 → 自动战斗
- 不恢复手动对决按钮，改为批量移动后自动触发

**Technical debt**: 无新增

**Problems encountered**: 无
