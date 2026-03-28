# Port Plan: FWTCG

> Original codebase: E:\claudeCode\FWTCG_V3d_V9
> Target framework: Unity 2022.3.62f3c1 / URP / C# / uGUI
> Asset path: E:\claudeCode\FWTCG_V3d_V9\tempPic
> Visual Upgrade Guide: ./plans/visual-upgrade-FWTCG.md

## Architectural Decisions

- **Intentional behavior changes**: 无 — 纯 1:1 移植
- **Known bugs to fix**: 无
- **Asset strategy**: 图片直接导入，字体需生成 TMP FontAsset
- **Visual strategy**: 逻辑1:1移植，视觉按 Visual Upgrade Guide 用 Unity 原生重建
- **UI system**: uGUI + DOTween
- **完成标准**: 每个 Phase 完成 = 用户可以实际打开 Unity 操作/玩，不以测试通过为标准

---

## DEV-1: 最小可玩 Demo

**目标**: 能完整玩一局——有卡可出、有战场可打、有胜负

**Original files**: engine.js, combat.js, ai.js, cards.js（仅基础单位部分）, ui.js（仅基础布局）

**包含内容**:
- Unity 工程初始化（URP / DOTween / TextMeshPro）
- 项目文件结构（命名空间 FWTCG.Core / FWTCG.UI 等）
- GameState.cs — G 对象全部字段的 C# 等价
- 卡牌数据：每套各 5 张简单单位卡（无特殊效果）
- 符文系统：横置获得法力 / 回收获得符能
- 回合六阶段（唤醒→开始→召出→抽牌→行动→结束）
- 单位移动（基地↔战场，含多单位同时移动）
- 战斗结算（基础伤害 / 强攻 / 坚守 / 征服）
- 据守/征服得分 + 胜利判定（8分）
- 简单 AI（随机合法动作）
- 基础 UI：战场 / 手牌 / 得分 / 符文 / 结束回合按钮 / 消息栏
- 游戏开始：随机先手，直接发4张牌，跳过硬币/换牌动画

**Visual handling**: 功能优先，简洁 uGUI，无 Shader/粒子，卡牌用纯色矩形+文字代替图片

**用户可以做什么**:
- ✅ 打开 Unity 运行游戏
- ✅ 横置符文获得法力
- ✅ 出单位牌到基地
- ✅ 点击单位 → 点击战场移动
- ✅ 看到战斗结果（谁赢了战场）
- ✅ 看到得分增加
- ✅ AI 回合自动进行
- ✅ 游戏有胜负结果

---

## DEV-2: 完整卡组数据 + 单位入场效果

**目标**: 两套完整卡组都能用，所有单位的入场效果都能触发

**Original files**: cards.js（全部）, spell.js（入场效果部分）, engine.js（绝念系统）

**包含内容**:
- 卡莎卡组全部 19 张单位 + ScriptableObject
- 易大师卡组全部 8 张单位 + ScriptableObject
- 所有卡组图片导入（tempPic/cards/ksha + jiansheng）
- 所有单位入场效果（onSummon triggers）
- 绝念系统（deathwish：4种）
- 增益指示物系统（buffToken）
- 掷硬币界面（硬币动画）
- 梦想手牌换牌（选≤2张换牌）
- 两套传奇卡（卡莎/易大师，仅数值，技能下个Phase）
- 符文牌堆完整（炽烈×7+灵光×5 / 翠意×6+摧破×6）
- 战场选择（从各自牌池随机选1个）

**Visual handling**: 卡牌图片替换色块，硬币用图片，其余 UI 不变

**用户可以做什么**:
- ✅ 看到真实卡牌图片
- ✅ 掷硬币决定先手
- ✅ 梦想手牌换牌
- ✅ 战场随机选择生效
- ✅ 单位入场效果触发（如 darius 进化加成、yordel_instructor 抽牌）
- ✅ 绝念效果触发（如 alert_sentinel 阵亡抽牌）

---

## DEV-3: 法术牌 + 基础法术效果

**目标**: 能打出法术牌，伤害/强化/抽牌类效果正常

**Original files**: spell.js（基础效果部分）, hint.js

**包含内容**:
- 费用检查系统（法力 + 符能双费用）
- 目标选择 UI（合法目标高亮）
- 卡莎法术（13张）：伤害 / 强化 / 抽牌类
- 易大师法术（9张，无反应类）：强化 / 眩晕 / 抽牌类
- 易大师装备系统（8张）
- 迅捷/回响关键词
- 手牌上限 7 张限制

**Visual handling**: 法术效果数字弹出提示（TextMeshPro），无粒子特效

**用户可以做什么**:
- ✅ 横置符文 → 获得法力 + 符能
- ✅ 出法术牌选目标（高亮可选单位）
- ✅ 法术效果生效（伤害扣血 / 单位获得战力 / 抽牌）
- ✅ 装备附着到单位，战力加成生效
- ✅ 回响牌二次触发
- ✅ 费用不足无法出牌（红色提示）

---

## DEV-4: 反应系统 + 法术对决

**目标**: 反应牌能在对方出牌时响应，法术对决流程完整

**Original files**: spell.js（对决部分）, engine.js（reactionWindow）

**包含内容**:
- 法术对决系统（startSpellDuel → 轮流响应 → 战斗结算）
- 反应窗口（reactionWindowOpen 冻结 AI）
- 反应关键词（玩家可在 AI 行动间插入）
- 卡莎反应牌：swindle / retreat_rune / guilty_pleasure / smoke_bomb
- 易大师反应牌：scoff / duel_stance / well_trained / wind_wall / flash_counter / duel_stance
- 异步交互锁（prompting 机制）
- askPrompt 对话框系统

**用户可以做什么**:
- ✅ AI 出牌时弹出反应窗口
- ✅ 可选择打出反应牌或跳过
- ✅ 法术对决轮流进行，双方均可响应
- ✅ 无效化/反制法术生效
- ✅ 移动单位到有敌方的战场 → 触发法术对决

---

## DEV-5: 传奇技能

**目标**: 卡莎和易大师的技能可以使用，进化条件可以触发

**Original files**: legend.js

**包含内容**:
- 卡莎主动技能：虚空感知（反应，休眠自身+1符能）
- 卡莎被动：盟友4种关键词→升级+3/+3
- 易大师被动：防守单位仅1名时+2战力
- 传奇HP独立管理（不受 atk=HP 规则约束）
- 传奇阵亡→游戏结束
- 技能本回合使用限制（resetLegendAbilitiesForTurn）
- 传奇升级动画（level 1→2 视觉变化）

**用户可以做什么**:
- ✅ 点击传奇使用主动技能
- ✅ 满足条件时卡莎自动升级（+3/+3 提示）
- ✅ 易大师防守时战力加成触发
- ✅ 传奇被打死→游戏立刻结束

---

## DEV-6: 战场特殊能力（19张）

**目标**: 所有战场卡的特殊效果在游戏中生效

**Original files**: engine.js（doStart/doSummon战场部分）, combat.js（战场触发部分）

**包含内容**:
- 全部 19 张战场卡特殊效果（据守触发 / 征服触发 / 被动效果）
- 战场图片导入（playmat资源）
- 攀圣长阶：据守+1分
- 遗忘丰碑：第三回合前无据守分
- 清算人竞技场：战力≥5自动强攻/坚守
- 虚空之门：法术伤害+1
- 其余 15 张全部实现
- 最后1分受限规则完整实现

**用户可以做什么**:
- ✅ 战场卡片可见，名称和效果可查看
- ✅ 据守时战场特效触发（如团结祭坛召唤新兵）
- ✅ 征服时战场特效触发（如希拉娜抽牌）
- ✅ 被动战场限制生效（如落岩之径无法直接出牌）

---

## DEV-7: AI 策略升级

**目标**: AI 打起来有真实挑战，能做出策略性决策

**Original files**: ai.js（完整）

**包含内容**:
- 局面评分系统（aiBoardScore）
- AI 出牌优先级（法术 / 单位 / 装备）
- AI 移动策略（进攻 / 防守 / 撤退）
- AI 符文操作优化
- AI 反应窗口决策（对决期间是否响应）
- AI 延迟链（_aiNextAction，保留操作间隔）
- 燃尽惩罚处理

**用户可以做什么**:
- ✅ AI 会主动推进战场控制
- ✅ AI 会在对决中打出反应牌
- ✅ AI 会合理使用符能
- ✅ 游戏有真实挑战感

---

## DEV-8: 视觉升级 Part 1（核心 Shader + 后处理）

**目标**: 游戏看起来有 Hextech 主题质感

**Visual Upgrade Guide 对应项**: 颜色系统 / URP后处理 / 卡牌发光 / 3D倾斜

**包含内容**:
- GameColors.cs（所有颜色常量）
- URP Post Processing Volume（Bloom / Color Adjustments / Vignette / Film Grain）
- 卡牌发光边框 Shader Graph（悬停 + 可出牌状态）
- 3D 倾斜系统（鼠标跟随，18°，LERP）
- 全息光泽 Shader（hover shine effect）
- 费用不足压暗效果
- 休眠/眩晕状态视觉
- 字体系统（Cinzel + Noto Sans SC TMP FontAsset）
- 背景深海军蓝 + 六边形网格 Shader

**用户可以做什么**:
- ✅ 游戏整体呈现 Hextech 金色/青色/深海军蓝风格
- ✅ 鼠标悬停卡牌时有 3D 倾斜效果
- ✅ 可出牌的卡牌有发光提示
- ✅ 场景有 Bloom 后处理光晕感
- ✅ 中文字体显示清晰

---

## DEV-9: 视觉升级 Part 2（动画 + 拖拽 + 粒子）

**目标**: 所有交互动画流畅，拖拽有特效，背景有氛围

**Visual Upgrade Guide 对应项**: 拖拽视觉 / 粒子系统 / DOTween动画 / 得分动画

**包含内容**:
- 拖拽系统（DragSystem）
- 漩涡/传送门 Prefab（3层旋转圆环 + 10粒子轨道）
- 法术牌放置动画（飞行→停留→爆裂 3段）
- 单位落地动画（飞行→弹跳→落地→涟漪）
- 背景漂浮粒子（55个，GPU Instanced Particle System）
- 鼠标拖尾 + 点击涟漪 + 六边形闪光
- DOTween 全局动画（卡牌进场 / 界面切换 / 得分脉冲）
- 得分圆圈动画 + 横幅动画
- 回合倒计时 UI（30秒进度条）

**用户可以做什么**:
- ✅ 拖拽卡牌到目标区域出牌
- ✅ 拖到有效区域出现漩涡特效
- ✅ 法术牌爆裂消散动画
- ✅ 单位落地有涟漪震动
- ✅ 背景有漂浮粒子氛围
- ✅ 得分时有缩放 + 光圈动画

---

## DEV-10: 完整游戏流程收尾

**目标**: 从打开游戏到游戏结束全流程无断点，可发布

**Original files**: main.js（完整）, titleEnhance.js

**包含内容**:
- 标题界面（完整视觉：文字动画 / 光效 / 背景）
- 掷硬币界面（完整动画 1800ms，硬币翻转）
- 战场选择界面（可视化选择）
- 梦想手牌换牌界面（选牌 UI 完整）
- 游戏结束界面（分数 + 再来一局）
- 软加权开局手牌（seedPlayerOpeningHand）
- 回合倒计时到0自动结束
- 时间扭曲额外回合（extraTurnPending）
- 全局音频占位（预留接口，不实现）
- 完整端到端游戏测试

**用户可以做什么**:
- ✅ 从标题界面开始完整游戏流程
- ✅ 看到硬币翻转动画确定先手
- ✅ 换牌后开始游戏
- ✅ 游戏结束后可再来一局
- ✅ 整个游戏体验流畅无卡死

---

## 依赖关系图

```
DEV-1（最小可玩）
  └─ DEV-2（完整卡组 + 图片）
       └─ DEV-3（法术牌）
            └─ DEV-4（反应/对决）
                 ├─ DEV-5（传奇技能）
                 ├─ DEV-6（战场能力）
                 └─ DEV-7（AI升级）
  DEV-8（视觉 Part 1）← 可与 DEV-5/6/7 并行
  DEV-9（视觉 Part 2）← 依赖 DEV-8
  DEV-10（流程收尾）← 依赖全部
```
