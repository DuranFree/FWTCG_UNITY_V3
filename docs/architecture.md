# FWTCG Unity — 架构文档

> 生成于 DEV-32（2026-04-03）。描述项目竣工状态的系统架构。

---

## 一、总体架构

```
┌─────────────────────────────────────────────────────────────┐
│                        UI 层                                │
│  GameUI · CardView · StartupFlowUI · SpellDuelUI            │
│  CardDragHandler · AskPromptUI · SpellTargetPopup           │
│  ReactiveWindowUI · SceneryUI · CombatAnimator · SpellVFX  │
└──────────────────────┬──────────────────────────────────────┘
                       │ 事件订阅 / 直接调用
┌──────────────────────▼──────────────────────────────────────┐
│                   GameManager（协调层）                      │
│  玩家输入路由 · 系统编排 · 异步流程（async/await）           │
└──┬────────┬────────┬────────┬────────┬───────────────────────┘
   │        │        │        │        │
   ▼        ▼        ▼        ▼        ▼
TurnMgr  Combat  Spell   Reactive  Score
System   System  System  System    Manager
   │
   ▼
TurnState
Machine
```

**核心数据流向：**
1. 玩家操作 → GameUI 回调 → GameManager
2. GameManager 修改 GameState → 调用各 System
3. System 触发 GameEventBus 事件 → UI 订阅更新

---

## 二、核心模块

### 2.1 数据层

| 类 | 职责 |
|----|------|
| `GameState` | 全局真实状态（手牌、基地、战场、法力、符能、得分） |
| `CardData` (ScriptableObject) | 卡牌静态数据（ID/名称/费用/关键词/效果ID） |
| `UnitInstance` | 运行时单位状态（HP/休眠/眩晕/增益/临时加成） |
| `RuneInstance` | 符文运行时状态（类型/横置状态） |
| `LegendInstance` | 传奇运行时状态（等级/技能冷却） |
| `BattlefieldState` | 战场运行时状态（控制权/单位列表） |

### 2.2 系统层

| 系统 | 职责 | 关键接口 |
|------|------|---------|
| `TurnManager` | 六阶段回合流程（唤醒→开始→召出→抽牌→行动→结束） | `RunTurn()` |
| `TurnStateMachine` | 四状态输入锁（Normal_ClosedLoop / Normal_OpenLoop / SpellDuel_ClosedLoop / SpellDuel_OpenLoop） | `static IsPlayerActionPhase` |
| `CombatSystem` | 战斗伤害分配、控制权更新、单位移除 | `TriggerCombat()` |
| `SpellSystem` | 法术效果执行、目标选择、费用扣除 | `ApplySpell()` |
| `ReactiveSystem` | 反应牌效果执行 | `ApplyReaction()` |
| `ScoreManager` | 据守/征服/燃尽得分，胜负判定 | `OnConquest()` |
| `EntryEffectSystem` | 单位入场触发效果 | `OnUnitEntered()` |
| `DeathwishSystem` | 单位死亡触发绝念效果 | `OnUnitsDied()` |
| `LegendSystem` | 传奇被动/触发/主动技能 | `CheckKaisaEvolution()` |
| `BattlefieldSystem` | 战场卡特殊效果（据守/征服/被动） | `OnStandbyTrigger()` |
| `RuneAutoConsume` | 自动符文消耗计划计算（单一规则源） | `Compute()` · `CanTap()` · `CanRecycle()` |
| `SimpleAI` | AI 决策（出牌/移动/符文/反应） | `TakeAction()` |

### 2.3 UI 层

| 组件 | 职责 |
|------|------|
| `GameUI` | 主战场 UI 刷新、区域引用、回调注册 |
| `CardView` | 卡牌视觉（状态/动画/拖拽/悬停） |
| `CardDragHandler` | 拖拽系统（手牌→基地/战场/法术区） |
| `GameEventBus` | 全局 UI 事件总线（static events） |
| `SpellVFX` | 法术/死亡/传奇粒子特效 |
| `CombatAnimator` | 战斗飞行动画、冲击波 |
| `SpellDuelUI` | 法术对决期间全屏叠加（倒计时/进度条） |
| `ReactiveWindowUI` | 反应窗口（TaskCompletionSource 异步等待） |
| `SceneBuilder` | Editor 工具：一键重建完整场景 |

---

## 三、事件系统

项目使用两套事件机制：

### 3.1 GameEventBus（UI 事件，static C# events）

```
OnCardPlayed(UnitInstance, string owner)
OnUnitDiedAtPos(UnitInstance, Vector2)
OnUnitDamaged(UnitInstance, int damage, string source)
OnDuelBanner(string text)
OnClearBanners()
OnConquestScored(string owner)
OnRuneTapFloat(string owner)
OnRuneRecycleFloat(string owner)
```

订阅者：SpellVFX · CombatAnimator · GameUI · SpellDuelUI · ReactiveWindowUI

### 3.2 系统级事件（各 System 内部 static events）

```
TurnManager.OnPhaseChanged(prev, next)
TurnManager.OnMessage(string)
ScoreManager.OnScoreChanged(owner, newScore)
ScoreManager.OnGameOver(winner)
LegendSystem.OnLegendEvolved(owner, level)
CombatSystem.OnCombatWillStart(bfId, attackers, defenders)
CombatSystem.OnCombatResult(CombatResult)
```

---

## 四、数据资产

### 4.1 卡牌资产（`Assets/Resources/Cards/`）

| 类别 | 数量 | 路径 |
|------|------|------|
| 卡莎单位卡 | 5 种 | `kaisa_*.asset` |
| 易大师单位卡 | 5 种 | `yi_*.asset` |
| 卡莎法术 | 10 种 | （含反应/装备） |
| 易大师法术 | 各种 | |
| 装备卡 | 4 种 | `dorans_blade` · `trinity_force` · `guardian_angel` · `zhonya` |
| 传奇卡 | 2 种 | `kaisa_legend` · `yi_legend` |
| 英雄卡 | 2 种 | `kaisa_hero` · `yi_hero` |
| 战场卡 | 19 种 | `Assets/Resources/Cards/BF/*.asset` |

### 4.2 战场卡（`Assets/Resources/Cards/BF/`）

19 张战场卡 CardData .asset 文件，仅作数据载体（名称/描述）。
运行时效果由 `BattlefieldSystem` 通过字符串 ID 匹配触发。

| ID | 中文名 | 触发类型 |
|----|--------|---------|
| `altar_unity` | 团结祭坛 | 据守 |
| `aspirant_climb` | 试炼者之阶 | 据守 |
| `back_alley_bar` | 暗巷酒吧 | 被动 |
| `bandle_tree` | 班德尔城神树 | 据守 |
| `dreaming_tree` | 梦幻树 | 被动 |
| `forgotten_monument` | 遗忘丰碑 | 被动 |
| `hirana` | 希拉娜修道院 | 征服 |
| `reckoner_arena` | 清算人竞技场 | 被动 |
| `reaver_row` | 掠夺者之街 | 征服（OUT OF SCOPE） |
| `rockfall_path` | 落岩之径 | 限制 |
| `star_peak` | 星尖峰 | 据守 |
| `strength_obelisk` | 力量方尖碑 | 据守 |
| `sunken_temple` | 沉没神庙 | 防守失败 |
| `thunder_rune` | 雷霆之纹 | 征服 |
| `trifarian_warcamp` | 崔法利战营 | 入场 |
| `vile_throat_nest` | 卑鄙之喉的巢穴 | 限制 |
| `void_gate` | 虚空之门 | 被动 |
| `zaun_undercity` | 祖安地沟 | 征服 |
| `ascending_stairs` | 攀圣长阶 | 被动 |

---

## 五、回合流程状态机

```
TurnStateMachine 四状态：

Normal_ClosedLoop ──── 玩家/AI 行动中（不可交互）
       │ 行动结束
Normal_OpenLoop ──────── 玩家可操作（出牌/移动/符文）
       │ 移动触发战斗
SpellDuel_ClosedLoop ─ 法术对决结算中
       │ 反应窗口开启
SpellDuel_OpenLoop ───── 玩家可打反应牌
       │ 反应结束/超时
Normal_ClosedLoop
```

TurnManager 六阶段：`Awaken → Start → Summon → Draw → Action → End`

---

## 六、已知架构摩擦点（供后续重构参考）

以下摩擦点在 DEV-32 架构探索中识别，未在本 Phase 实现（列入技术债）：

| 编号 | 摩擦点 | 影响 |
|------|--------|------|
| A1 | `GameState` 被 12+ 个系统直接读写，无访问封装 | 修改字段影响范围极广 |
| A2 | UI 无 ViewModel 层，依赖 `RefreshUI()` 手动同步 | 状态变化容易遗漏刷新 |
| A3 | 伤害/死亡逻辑分散在 CombatSystem/SpellSystem/ReactiveSystem | 三处重复，边界不清 |
| A4 | 反应窗口用 `static TaskCompletionSource`，跨越 GameManager/AI | 难以单元测试 |
| A5 | 三层状态管理并存（TurnStateMachine + TurnManager + GameManager flags） | 概念重叠，转换逻辑分散 |
| A6 | 入场/死亡触发链通过直接调用编排，非统一事件总线 | 新增系统需修改多处 |

---

## 七、测试覆盖概况

| 测试套件 | 测试数 | 覆盖范围 |
|---------|--------|---------|
| DEV1–DEV8 | ~180 | 核心规则、战斗、法术、符文 |
| DEV9–DEV14 | ~80 | UI 逻辑、AI、音效框架 |
| DEV15–DEV22 | ~120 | 动画系统、拖拽、交互 |
| DEV23–DEV32 | ~170 | 视觉系统、规则修正、架构行为 |
| **合计** | **550** | **EditMode 全绿** |

所有测试均为 EditMode（不依赖场景/MonoBehaviour），使用 NUnit 框架。
