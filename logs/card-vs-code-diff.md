# 牌面文字/数值 vs 代码逻辑 对账报告
> 生成于 2026-04-23。只做记录，不修改。
> 对比口径：`Assets/Resources/Cards/*.asset`（`_description` + 数值字段）vs 各 System 实际实现。
> 符能枚举（以防混淆）：0=Blazing炽烈 / 1=Radiant灵光 / 2=Verdant翠意 / 3=Crushing摧破 / 4=Chaos混沌 / 5=Order秩序

---

## 🔴 High（影响玩法结果）

### D-1 balance_resolve 费用减免数值错 2 vs 3
- 卡面：「…则此法术的费用减少**[3]**」
- 代码 [GameRules.cs:256-262](Assets/Scripts/Core/GameRules.cs:256)：`cost -= 2`
- 差 1 点法力；玩家/AI 实际支付比卡面多 1

### D-2 balance_resolve 召出的符文未休眠
- 卡面：「召出一枚**休眠**的符文」
- 代码 [SpellSystem.cs:462-478](Assets/Scripts/Systems/SpellSystem.cs:462) `SummonRune`：只 `runes.Add(r)`，无 `r.Tapped = true`
- 实际召出的是活跃符文，本回合可立即消耗——比卡面慷慨
- 对照组：`ReactiveSystem.SummonDormantRune`（retreat_rune 用）line 302-321 正确设了 `Tapped = true`

### D-3 foresight_mech 主效果完全缺失
- 卡面："**你的「机械」属性单位获得【预知】**。（当你打出我时，查看主牌堆顶的一张牌，你可以选择将其置底。）"
- 代码 [EntryEffectSystem.cs:136-143](Assets/Scripts/Systems/EntryEffectSystem.cs:136)：只打印牌库顶一张牌的 log
- 三处缺失：
  1. 无「机械」属性数据建模 → 持续被动无法生效
  2. 查看顶牌没有呈现给玩家（只 `Debug.Log`）
  3. 「可选择置底」分支未实现

---

## 🟡 Medium（偏离文本，玩法可察觉）

### D-4 balance_resolve 触发条件多出一分支
- 卡面：「如果**对手分数离获胜分数不超过 3**」→ 仅 `toWin ≤ 3`
- 代码 [GameRules.cs:261](Assets/Scripts/Core/GameRules.cs:261)：`if (oppScore <= 3 || toWin <= 3)`
- 多了 `oppScore ≤ 3` 路径；WIN_SCORE=5 时两条件等价，WIN_SCORE>6 时前几回合会非预期触发

### D-5 jax 被动文本位置不符
- 卡面：「让**你基地**的装备卡获得【反应】」
- 代码 [GameRules.cs:240-245](Assets/Scripts/Core/GameRules.cs:240) `CardActsReactive`：仅判 `IsEquipment && IsJaxInPlay`，与装备位置无关；实际语义是「**手牌**里的装备变反应」（注释在 line 216 也是这么写的：「你手牌中的装备获得【反应】」）
- 卡面与代码/注释三方不一致

### D-6 flash_counter 缺装备卡触发路径
- 卡面：「无效化一个敌方**法术或装备卡对目标的法术或技能效果**」
- 代码 [ReactiveSystem.cs:175-189](Assets/Scripts/Systems/ReactiveSystem.cs:175)：只判 `triggerSpell.Owner == enemy`
- 装备卡触发的技能反制链路不存在

### D-7 back_alley_bar / trifarian_warcamp 仅对玩家生效
- 卡面两处都是中性「己方单位…」，未限定玩家或 AI
- 代码 [BattlefieldSystem.cs:141,155](Assets/Scripts/Systems/BattlefieldSystem.cs:141)：`if (owner != GameRules.OWNER_PLAYER) return;`
- 注释 line 36 自述「matches JS behaviour」—— 是从 JS 版本保留的单边实现，AI 单位移动时拿不到这俩 BF 效果

### D-8 guilty_pleasure 玩家路径缺弃牌选择 UI
- 卡面：「弃置一张手牌」（通常可选）
- 代码 [SpellSystem.cs:550-576](Assets/Scripts/Systems/SpellSystem.cs:550)：自动挑「首张非法术，退化到首张」；AI 路径 ok，玩家无法选择哪张弃

### D-9 foresight_mech 顶牌置底选项缺失（D-3 子项）
- 卡面：「**你可以选择将其置底**」
- 代码只读取 `deck[0]` 打印，无分支处理「置底」

---

## 🟢 Low / Trivial

### D-10 aspirant_climb / trifarian_warcamp 的「增益指示物」是 +1/+1 而非 +1 战力
- aspirant_climb 卡面：「给基地的一名单位**+1战力**」
- trifarian_warcamp 卡面：「获得一个增益指示物**(+1战力)**」
- 代码 `ApplyBuffToken` [BattlefieldSystem.cs:463-468](Assets/Scripts/Systems/BattlefieldSystem.cs:463)：同时 `CurrentAtk++` 和 `CurrentHp++`
- 业界「增益指示物」一般是 +1/+1，但本卡面两处都只写「+1战力」，实际 HP 也涨

### D-11 wailing_poro 基地阵亡不触发
- 卡面：「当我被摧毁时，如果**此处**没有其他友方单位，则抽一张牌」
- 代码 [DeathwishSystem.cs:91](Assets/Scripts/Systems/DeathwishSystem.cs:91) `IsAloneInZone`：`if (bfId < 0) return false`
- 基地死（bfId=-1）直接判定"非孤独"；卡面未限定「战场」— 基地也是「此处」

### D-12 AkasiStorm 代码注释写反了符能
- 卡面数据：`Blazing/2 + sec Radiant/1`
- 代码 [SpellSystem.cs:111](Assets/Scripts/Systems/SpellSystem.cs:111) 注释：`"2 Radiant + 1 Blazing"` ← 写反
- 实现本身依然只走「循环 6 次 damage 2」的主逻辑，不读注释，实测无影响

### D-13 furnace_blast 卡面未限"敌方"
- 卡面：「对同一位置的**最多三名单位**各造成1点伤害」
- 代码 [SpellSystem.cs:398-439](Assets/Scripts/Systems/SpellSystem.cs:398) 只挑敌方战场
- 实战上几乎没人会自残，但卡面字面范围更宽

---

## ✅ 本次对比确认匹配的要点（抽样）

- darius：代码恰好在 `CardsPlayedThisTurn == 2` 那一下触发 +2/活跃，卡面「**第二张**牌时」一次性语义匹配
- time_warp：`ExtraTurnPending = true` + 入 exile，对应「再进行一个回合，然后放逐此牌」
- rengar 活跃进场：CombatSystem 通过 `WasInBaseAtTurnStart` 实现「若本回合开始前我在基地」条件 ✓
- duel_stance 额外 +1：`if (t.PlayedThisTurn)` 双栈 TempAtkBonus ✓
- stardrop 两段（自动/玩家选目标）、starburst 两目标 6 伤、akasi_storm 循环 6 次 2 伤 ✓
- zhonya / guardian_equip：战斗与法术两条致死路径都有保护替换 ✓
- scoff 费用门槛：`Cost ≤ 4 && RuneCost ≤ 3` ✓
- trinity_equip 据守 +1：ScoreManager.ComputeTrinityForceBonus 有实现 ✓
- tiyana 场上时禁止对手得分：ScoreManager 动态查 `IsTiyanaOnBattlefield()` ✓
- kaisa_legend / yi_legend：LegendSystem 两条被动均实现 ✓
- 所有反应法术（wind_wall/flash_counter/scoff/well_trained/duel_stance/retreat_rune/swindle/smoke_bomb）核心语义匹配（除 D-6 flash_counter 范围）
- BF 卡：altar_unity、bandle_tree、reckoner_arena、void_gate、dreaming_tree、sunken_temple、thunder_rune、vile_throat_nest、rockfall_path、ascending_stairs、forgotten_monument、star_peak、reaver_row、zaun_undercity、hirana、strength_obelisk 全部找到对应触发点

---

## 汇总

| 优先级 | 数量 | 条目 |
|--------|------|------|
| 🔴 High | 3 | D-1 D-2 D-3 |
| 🟡 Medium | 6 | D-4 D-5 D-6 D-7 D-8 D-9 |
| 🟢 Low/Trivial | 4 | D-10 D-11 D-12 D-13 |
| ✅ 对齐 | ~45 张卡核心逻辑 | — |

建议优先级：先补 D-1/D-2/D-3（玩法结果偏差），再处理 D-5/D-6（文字/代码注释/行为三方一致性），其余可归 tech-debt。

---

## 修复落地（2026-04-23）

| 条目 | 状态 | 修改点 |
|------|------|--------|
| D-1 | ✅ | [GameRules.cs:253](Assets/Scripts/Core/GameRules.cs:253) `cost -= 3` |
| D-2 | ✅ | [SpellSystem.cs:131,462](Assets/Scripts/Systems/SpellSystem.cs:131) `SummonRune(dormant: true)` |
| D-3 | ⏸ 归入 tech-debt | 需建模单位属性子类型系统 + 置底 UI，Phase 级任务 |
| D-4 | ✅ | GameRules 改用 `BattlefieldSystem.EffectiveWinScore(gs)`，只留 `toWin ≤ 3` |
| D-5 | ✅ | jax.asset / SceneBuilder.cs 卡面"基地的装备"→"手牌中的装备卡" |
| D-6 | ⏸ 归入 tech-debt | 当前卡池无可触发装备，保留现状 |
| D-7 | ✅ | [BattlefieldSystem.cs:139,153](Assets/Scripts/Systems/BattlefieldSystem.cs:139) 去 `owner != OWNER_PLAYER` 单边限制 |
| D-8 | ⏸ 归入 tech-debt | guilty_pleasure 玩家弃牌选择 UI 待扩展 |
| D-9 | ⏸ 归入 D-3 | foresight_mech 置底 UI 与 D-3 合并处理 |
| D-10 | ✅ | `ApplyAtkBuffToken` 只 +atk；HiranaConquest 对称只 -atk；UnitInstance 描述改为"+N 战力增益标记" |
| D-11 | ✅ | [DeathwishSystem.cs:85](Assets/Scripts/Systems/DeathwishSystem.cs:85) bfId<0 时查基地 |
| D-12 | ✅ | SpellSystem.cs AkasiStorm 注释改 "2 Blazing + 1 Radiant (dual)" |
| D-13 | ✅ | SpellSystem.cs FurnaceBlast 注释补"仅对敌方"理由 |

### 测试
- 新增 [CardFaceVsCodeTests.cs](Assets/Tests/EditMode/CardFaceVsCodeTests.cs)：11 项全绿
- 全套 EditMode 回归：**1179/1179 pass**

---

## 2026-04-23 复核（只记录，不修改）

口径：重新逐卡核对 42 张 `Assets/Resources/Cards/*.asset` 的 `_description/_cost/_runeCost/_atk/_keywords` vs 各 System / GameManager 实现。

### 已修复项回归验证（D-1 … D-13）
- D-1 [GameRules.cs:253-266](Assets/Scripts/Core/GameRules.cs:253) `cost -= 3` 就位 ✓
- D-2 [SpellSystem.cs:130](Assets/Scripts/Systems/SpellSystem.cs:130) `SummonRune(owner, gs, dormant: true)` ✓
- D-4 [GameRules.cs:260-263](Assets/Scripts/Core/GameRules.cs:260) 只留 `toWin <= 3` + `EffectiveWinScore(gs)` ✓
- D-5 [GameRules.cs:216](Assets/Scripts/Core/GameRules.cs:216) 注释 "手牌中的装备" + jax.asset `_description` 同步 ✓
- D-7 `BattlefieldSystem` 单边限制已去除（见 tech-debt 标记）✓
- D-10 `ApplyAtkBuffToken` 只 +atk ✓
- D-11 `DeathwishSystem.IsAloneInZone` bfId<0 查 Base ✓
- D-12 / D-13 注释已更正 ✓

### 新发现

#### 🟡 N-1 bad_poro 征服效果「硬币装备指示物」未实现（Medium）
- 卡面：「当我征服一处战场时，打出 **1 枚休眠的「硬币」装备指示物**。」
- 代码 [CombatSystem.cs:554-558](Assets/Scripts/Systems/CombatSystem.cs:554) `bad_poro_conquer`：直接 `DrawOneCardForConquest(attacker, gs, unit)`
- 注释自述「硬币装备指示物尚未建模，简化为摸一张牌作为占位。」
- 差异：卡面承诺的是「休眠装备指示物进场」（占基地槽位，后续可激活附着），实际给的是「手牌 +1」，资源性质与战术含义完全不同
- 归因：卡池缺「硬币」装备 token CardData（类似 trinity_force 等装备但无费用指示物形态）；需补建模 + 进场召出逻辑

#### 🟢 N-2 starburst 第二目标默认取敌方（Low）
- 卡面：「对**最多两名单位**各造成 6 点伤害。」—未限定敌/己
- 代码 [SpellSystem.cs:584-613](Assets/Scripts/Systems/SpellSystem.cs:584) `Starburst_DealToSecondTarget`：`string enemy = gs.Opponent(owner)`，候选只取对面阵营
- 与 D-13 furnace_blast 同性质，实战玩家不会打自己，影响极小；归 Low

#### ✅ 以下卡片本次复核匹配 ✓
- darius：`EntryEffectSystem.OnAnyCardPlayed` 订阅 + `CardsPlayedThisTurn == 2` 瞬间触发 + `_dariusBuffedThisTurn` 单回合一次 ✓
- rengar：`rengar_enter` 设 HasReactive+StrongAtkValue；`CombatSystem.cs:75-83` 基地→战场活跃移动 ✓
- noxus_recruit：`GameManager.ComputeLegionAdjustedCost` 检查 `Inspire + CardsPlayedThisTurn >= 1`，扣 `LegionCostReduction` ✓
- yordel_instructor：`Barrier` 关键字 + 入场摸牌 ✓
- sandshoal_deserter：`UntargetableBySpells = true`，四处（SpellTargetPopup/GameManager/SimpleAI/ReactiveSystem）都过滤 ✓
- scoff：费用 ≤4 且 RuneCost ≤3 ✓
- time_warp：`ExtraTurnPending + 放逐` ✓
- tiyana：位于战场时封锁得分 ✓
- wailing_poro：基地孤独也触发（D-11 修复保留）✓
- jax 被动：`CardActsReactive` 手牌装备在 jax 在场时视为反应 ✓
- guilty_pleasure：除玩家选卡 UI（D-8）外其余匹配 ✓
- akasi_storm / stardrop / furnace_blast / starburst：基础伤害+Echo+玩家 UI 选目标链路匹配 ✓

### 仍保留的历史 deferred
| 条目 | 状态 | 说明 |
|------|------|------|
| D-3 | ⏸ | foresight_mech「机械属性单位获得预知」+ 置底 UI 未实现 |
| D-6 | ⏸ | flash_counter 装备卡触发路径（当前卡池无可触发装备）|
| D-8 | ⏸ | guilty_pleasure 玩家手动选弃牌 UI |

### 汇总
| 优先级 | 新项 | 说明 |
|--------|------|------|
| 🟡 Medium | N-1 | bad_poro 硬币装备指示物占位摸牌 |
| 🟢 Low | N-2 | starburst 第二目标仅敌方（与 D-13 同性质）|
| ⏸ 保留 | D-3 / D-6 / D-8 | 功能级扩展，Phase 级任务 |

**结论：** 上次修复 10 项全部仍有效。新发现 1 Medium (N-1) + 1 Low (N-2)。其余 42 张卡核心玩法与卡面一致。

---

## 2026-04-23 复核修复

| 条目 | 状态 | 修改点 |
|------|------|--------|
| N-1 | ✅ | 新建 [coin_equip.asset](Assets/Resources/Cards/coin_equip.asset)（0费、装配[0]、+1战力）；[SceneBuilder.cs:3691](Assets/Scripts/Editor/SceneBuilder.cs:3691) 补 CD 条目；[CombatSystem.cs:554](Assets/Scripts/Systems/CombatSystem.cs:554) `bad_poro_conquer` 改调 `SummonCoinEquipment` — 加载 coin_equip 资源 → 新建 UnitInstance → Exhausted=true → 入基地 → FireUnitEntered |
| N-2 | ✅ | [SpellSystem.cs:581-583](Assets/Scripts/Systems/SpellSystem.cs:581) Starburst_DealToSecondTarget 加注释，说明「仅敌方」与 D-13 同逻辑一致 |

### 测试
- [CardFaceVsCodeTests](Assets/Tests/EditMode/CardFaceVsCodeTests.cs) 新增 2 项：
  - `BadPoro_CoinEquipAsset_Exists_AsDormantEquipmentToken` — 验证 coin_equip.asset 可加载、各字段匹配卡面
  - `BadPoro_ConquestSummonsDormantCoin_IntoBase` — 通过反射调 `SummonCoinEquipment`，验基地新增休眠 coin
- 全套 EditMode 回归：**1181/1181 pass**
