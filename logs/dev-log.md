# 开发日志 — FWTCG Unity 移植

---

## DEV-18：战斗视觉 + 待命/瞬息机制 — 2026-03-31

**Status**: ✅ Completed
**Tests**: 309/309 🟢

**What was done**:

- **Ephemeral 关键词（Rule 728）** — `CardKeyword.cs` 新增 `Ephemeral = 1 << 14`；`CardDetailPopup.cs` 新增名称+描述条目；`UnitInstance.cs` 新增 `IsEphemeral`、`SummonedOnRound`（默认-1）、`IsStandby` 三个属性；`TurnManager.cs` 在 DoAwaken 阶段新增 `DestroyEphemeralUnits(gs)` 遍历基地+所有BF单位，`SummonedOnRound < gs.Round` 即销毁并 Fire `UnitDied` 事件。
- **战斗冲击波（Rule 战斗视觉）** — 新建 `CombatAnimator.cs`，订阅 `CombatSystem.OnCombatResult`，BF1/BF2 各生成一个 ShockwaveRing 子 Image，每次战斗结果触发 `PlayShockwave` 协程（scale 0→1.5，alpha 0.80→0，0.45s）。
- **战场环境光晕** — 新建 `BattlefieldGlow.cs`，AmbientBreatheLoop（5s，alpha 0.02~0.08）+ CtrlGlowLoop（3s，绿/红/透，alpha 0.10~0.35）；`SetControl(owner)` 由 `GameUI.UpdateBFGlows()` 每次 Refresh 调用。
- **出牌板闪烁** — `GameManager.cs` 新增 `OnCardPlayed` 静态事件，`TryPlayUnit/Equipment/SpellAsync` 均 Fire；`GameUI.cs` 订阅后触发 `BoardFlashRoutine`（0.85s，淡入0.3s+淡出0.55s，覆盖全棋盘 overlay）。
- **战场卡牌图片** — `GameUI.UpdateBFCardArt()` 从 `gs.BF1Id/BF2Id` 拼接 `Resources/CardArt/bf_{id}` 路径，`Resources.Load<Sprite>` 加载并赋给 SceneBuilder 创建的 Image。
- **待命区域 UI** — `SceneBuilder.CreateBattlefieldPanel` 新增 StandbyZone（18px 高，STANDBY 标签），以及 AmbientOverlay + CtrlGlowOverlay 两个全面板 Image，BattlefieldGlow 组件通过 SerializedObject wiring 与两个 overlay 关联。
- **SceneBuilder 扩展** — CreateBoardWrapper / WireGameUI 参数扩展支持 bf1Glow/bf2Glow/bf1CardArt/bf2CardArt/boardFlashOverlay；添加 CombatAnimator 组件并 wire BF panel RectTransforms。
- **测试** — 新建 `DEV18CombatVisualTests.cs`（18 tests）：Ephemeral 枚举/HasKeyword/UnitInstance 默认值/Standby flip/4条瞬息条件逻辑/BF art 路径格式/BF_DISPLAY_NAMES spot-check/OnCardPlayed 事件订阅反订阅。同步修复 `DEV8VisualTests.CardDetailPopup_15KeywordsExist`（14→15）。

**Files changed**: `CardKeyword.cs`、`CardDetailPopup.cs`、`UnitInstance.cs`、`TurnManager.cs`、`GameManager.cs`、`GameUI.cs`、`SceneBuilder.cs`（新建：`BattlefieldGlow.cs`、`CombatAnimator.cs`、`DEV18CombatVisualTests.cs`）

---

## DEV-25：规则对齐修复（16条对照表）— 2026-03-31

**Status**: ✅ Completed

**What was done**:

对照《符文战场》核心规则文档，逐条修复逻辑偏差，共修改 7 个文件：

- **#1 法盾语义（Rule 721）** — `SpellSystem.cs`：移除 DealDamage/Slam 中的伤害/眩晕吸收逻辑。`GameManager.cs`：新增 `TryPaySpellShieldCost()`，在玩家选择目标时（点击路径+弹窗路径）检查并扣除1点符能；无法支付则拦截选择。`SimpleAI.cs`：`AiChooseSpellTarget` 跳过符能不足时无法承担法盾费用的目标；`CastAISpell` 在施法时扣除对应符能。
- **#2 壁垒优先（Rule 727）** — `CombatSystem.cs`：`DistributeDamage` 通过 LINQ 将 `HasBarrier=true` 的单位排在前面，确保先于普通单位承受致命伤害。`UnitInstance.cs`：新增 `HasBarrier` 属性，构造时从 `CardData` 读取关键词初始化。
- **#7 抽牌阶段符文池清空（Rule 515.4.d）** — `TurnManager.cs`：`DoDraw` 所有三条退出路径（燃尽/无牌/正常）末尾均清空所有玩家的法力和符能池。
- **#8 回收放底部** — `GameManager.cs`：`OnRuneClicked` 回收时改为 `PRuneDeck.Add(rune)`（原为 `Insert(0, rune)`）。`SimpleAI.cs`：`AiRecycleRunes` 两处 `Insert(0, r)` 改为 `.Add(r)`。
- **#9 增益上限1（Rule 702）** — `UnitInstance.cs`：`BuffTokens` 改为手动属性，`set` 用 `Mathf.Clamp(value, 0, 1)` 确保最多1个增益指示物。
- **#15 急速费用（Rule 717）** — `SimpleAI.cs`：单位有 Haste 关键词时，AI 先判断是否有多余1法力+1C符能；有则额外扣除并进场为活跃状态（原为自动免费给活跃状态）。
- **#17 手牌调度回收（Rule 117.3）** — `StartupFlowUI.cs`：`PerformMulligan` 退还牌改为 `deck.Add(u)`（原为 `deck.Insert(Random.Range(...))`）。

**Tests**: 289/289 EditMode 全绿（3条旧测试同步更新为 Rule 721 正确语义）

---

## DEV-17：伤害数字飘出 + 单位死亡动画 — 2026-03-31

**Status**: ✅ Completed

**What was done**:
- DamagePopup.cs（NEW）：每次单位受伤时在 Canvas 根节点生成浮字，红色粗体+黑描边，ease-out 上浮 75px，0.85s 后自毁。
- CardView.cs：新增 `PlayDeathAnimation()`，缩小+淡出 0.45s（二次加速曲线），缓存各 Image/Text 原始颜色逐帧 alpha 渐出。
- GameManager.cs：新增 `OnUnitDied` 静态事件 + `FireUnitDied()`；`OnBattlefieldClicked` 改为 `async void`，两个战斗分支各加 550ms 延迟后再 RefreshUI；新增 `_bfClickInFlight` 输入锁防止 await 期间重叠点击，await 后重检 GameOver 再刷新。
- SpellSystem.cs：`RemoveDeadUnit` 在移除前先 `FireUnitDied`。
- CombatSystem.cs：`RemoveDeadUnits` 在移除前先 `FireUnitDied`。
- TurnManager.cs：`DoAction` 中 `ResolveAllBattlefields` 后加 `await Task.Delay(550)` 确保回合结束兜底战斗的死亡动画也能播完。
- GameUI.cs：订阅 `OnUnitDied` → `PlayDeathAnimation`；`OnSpellUnitDamaged` 新增 `SpawnDamagePopup`，通过 `RectTransformUtility` 正确转换坐标后生成 DamagePopup。

**Decisions made**:
- `OnUnitDied` 在单位从 List 移除前触发，确保 FindCardView 仍能找到 CardView。
- `_bfClickInFlight` + `try/finally` 保证锁无论是否抛异常都能释放。
- `ResolveAllBattlefields` 延迟放在 TurnManager async Task 层，无需额外 MonoBehaviour。

**Codex 审查**:
- 2 High 已修复：BF点击输入锁 + ResolveAllBattlefields 死亡延迟
- Medium/Low 记入 tech-debt.md

**Tests**: 289/289 EditMode 全绿

---

## DEV-16c：Bug 修复 — SpellShowcase 根治 + SpellTargetPopup + AI 出牌 + 受击特效 — 2026-03-31

**Status**: ✅ Completed

**What was done**:
- SpellShowcaseUI.cs：彻底修复 Lazy Awake 陷阱（Awake 调用 SetActive(false) 会在第一次激活时立刻再次设 inactive，使 StartCoroutine 失败）。改为始终保持 GameObject active，用 CanvasGroup alpha/blocksRaycasts 控制可见性。
- SpellTargetPopup.cs：同类修复，HidePanel() 使用 CanvasGroup，不再 SetActive。
- SimpleAI.cs：AiRecycleRunes 新增 `if (card.CardData.Cost > gs.EMana) continue;`，防止为本回合无法打出的卡浪费符能，修复 AI 永远不出单位牌的系统性 bug。
- SpellSystem.cs：新增 `OnUnitDamaged` 静态事件，DealDamage/AkasiStorm 触发，携带目标+伤害量+法术名。
- CardView.cs：FlashRed() + Shake() 受击动画（0.12s 红闪 + 0.35s 渐回 + 4次 ±8px 抖动）。
- GameUI.cs：订阅 OnUnitDamaged → FindCardView → FlashRed/Shake + GameManager.FireHintToast 飘屏。
- GameManager.cs：新增 FireHintToast() 静态帮助方法。

**Decisions made**:
- CanvasGroup 方案是 Unity 中 "始终 active" 面板的标准隐藏方式，彻底避免 Lazy Awake 问题。
- AiRecycleRunes 的修复条件为 `Cost > gs.EMana`（法力不足）而非 sch 判断，因为 sch 每回合清零，预储没有意义。

**Tests**: 289/289 EditMode 全绿

---

## DEV-16b：卡牌不可用飘屏提示 + 卡牌抖动 + 法术目标选择弹窗 — 2026-03-31

**Status**: ✅ Completed

**技术决策**:
- `GameManager.OnHintToast` / `OnCardPlayFailed` 静态事件：解耦提示触发与 UI 层
- `ShowPlayError(msg, card)` 统一入口：BroadcastMessage + toast + shake 三合一
- `CardView.Shake()` 协程：±10px 左右抖动 7步 0.28s，`RectTransform.anchoredPosition` 偏移
- `SpellTargetPopup`：Singleton MonoBehaviour，`ShowAsync(SpellTargetType, GameState)` → `Task<UnitInstance>`，分敌/己方两栏动态生成按钮
- `SpellShowcaseUI` coroutine 修复：`gameObject.SetActive(true)` 必须在 `StartCoroutine` 之前调用

**新功能**:
- 法术目标选择弹窗（SpellTargetPopup，敌/己方上下两栏，可取消）
- 无可用目标时飘屏提示（OnHintToast → ToastUI.Enqueue）
- 无可用目标时卡牌左右抖动（OnCardPlayFailed → GameUI.ShakeHandCard → CardView.Shake）
- Debug 按钮：+5 全符能（一键给所有6种符文各+5符能）

**Bug 修复**:
- `SpellShowcasePanel` inactive 时 StartCoroutine 报错 → 先 SetActive(true) 再协程
- `CardView` 重复 `Unit` 属性定义编译错误

**修改文件**:
- `Assets/Scripts/GameManager.cs`（+OnHintToast/OnCardPlayFailed 事件 + ShowPlayError + SpellTargetPopup 集成）
- `Assets/Scripts/UI/CardView.cs`（+Shake 方法 + 修复重复 Unit 属性）
- `Assets/Scripts/UI/GameUI.cs`（+OnCardPlayFailed 订阅 + ShakeHandCard）
- `Assets/Scripts/UI/ToastUI.cs`（+OnHintToast 订阅）
- `Assets/Scripts/UI/SpellShowcaseUI.cs`（coroutine inactive 修复）
- `Assets/Scripts/Editor/SceneBuilder.cs`（+SpellTargetPopup UI 构建）

**New files**:
- `Assets/Scripts/UI/SpellTargetPopup.cs`

**测试**: 289/289 全绿

---

## DEV-15：AI 反应对称 + 传奇升级动画 — 2026-03-31

**Status**: ✅ Completed

**技术决策**:
- AI 反应到玩家施法：`CastPlayerSpellWithReactionAsync`（async Task，fire-and-forget）给 AI 700ms 窗口，同步等待结算，期间 `_aiReactionPending` 锁定玩家输入
- `AiPickBestReactiveCard`：5 级明确优先级（风墙 > 闪电反制 > 嘲讽 > 精英训练 > 决斗姿态），每级独立 foreach 保证优先级不受列表顺序影响
- 传奇升级动画：`LegendSystem.OnLegendEvolved` 静态事件 → `GameUI.FlashLegendText` 金色闪烁协程（4 次 0.15s 脉冲）

**新功能**:
- `SimpleAI.AiPickBestReactiveCard`（public static，可测试）
- `GameManager.CastPlayerSpellWithReactionAsync`（玩家法术给 AI 反应窗口）
- `GameManager.AiTryReact`（AI 手牌筛选 + 费用扣除 + ApplyReactive 调用）
- `GameManager._aiReactionPending`（防止反应期间玩家重复操作）
- `LegendSystem.OnLegendEvolved`（`Action<string, int>`：owner + newLevel）
- `GameUI.OnLegendEvolved` + `FlashLegendText`（协程金色闪烁）

**修改文件**:
- `Assets/Scripts/AI/SimpleAI.cs`（+AiPickBestReactiveCard）
- `Assets/Scripts/GameManager.cs`（+_aiReactionPending + CastPlayerSpellWithReactionAsync + AiTryReact）
- `Assets/Scripts/Systems/LegendSystem.cs`（+OnLegendEvolved 事件）
- `Assets/Scripts/UI/GameUI.cs`（+OnLegendEvolved 订阅 + FlashLegendText 协程）
- `plans/feature-checklist.md`、`plans/visual-checklist.md`、`plans/tech-debt.md`

**New files**:
- `Assets/Tests/EditMode/DEV15ReactionTests.cs`（11 个测试）

**测试结果**: 274 全绿（11 新 + 263 现有），0 失败

---

## DEV-14：软加权手牌 + 音频框架 + 系统验证 — 2026-03-31

**Status**: ✅ Completed

**技术决策**:
- 软加权67%触发率：DealInitialHand内Random.value检测→SeedOpeningHand找≤2费非法术非装备单位
- AudioManager：单例模式，BGM循环+SFX OneShot，所有方法安全调用（clip=null时跳过）
- 增益指示物系统确认已完整实现（BuffTokens字段+ResetEndOfTurn保留）

**新功能**:
- GameManager.SeedOpeningHand（软加权67%开局手牌）
- AudioManager.cs（完整音频框架：BGM+9个SFX+音量控制）
- 确认标记：增益指示物系统、卡组初始化完整流程

**修改文件**:
- `Assets/Scripts/GameManager.cs`（+SeedOpeningHand+DealInitialHand改写）
- `Assets/Scripts/Audio/AudioManager.cs`（新建）
- `Assets/Tests/EditMode/DEV14SystemTests.cs`（新建，9个测试）

**测试结果**: 263 全绿（9 新 + 254 现有），0 失败

---

## DEV-13：装备附着系统 + 符能费用检查 + 常量验证 — 2026-03-31

**Status**: ✅ Completed

**技术决策**:
- 装备附着：UnitInstance.AttachedEquipment/AttachedTo 双向引用，TryPlayEquipment自动选最强非装备单位
- 符能费用：TryPlayUnit/TryPlayEquipment 都检查 RuneCost 并扣除对应符能
- 游戏常量：确认所有关键常量已在 GameRules.cs 中定义

**新功能**:
- UnitInstance: AttachedEquipment / AttachedTo 属性（装备附着双向引用）
- GameManager.TryPlayEquipment（自动附着到最强己方单位+ATK加成+费用检查）
- GameManager.TryPlayUnit 增加符能费用检查（RuneCost+RuneType）
- 确认标记：WIN_SCORE/INITIAL_HAND/TURN_TIMER/STRONG_POWER等常量

**修改文件**:
- `Assets/Scripts/Core/UnitInstance.cs`（+AttachedEquipment/AttachedTo）
- `Assets/Scripts/GameManager.cs`（+TryPlayEquipment+TryPlayUnit符能检查）
- `Assets/Tests/EditMode/DEV13EquipTests.cs`（新建，12个测试）

**测试结果**: 254 全绿（12 新 + 242 现有），0 失败

---

## DEV-12：AI符文回收 + 征服触发 + 系统确认 — 2026-03-31

**Status**: ✅ Completed

**技术决策**:
- AI符文回收：优先回收已横置同类型符文，不足时再牺牲未横置符文
- bad_poro征服触发：CombatSystem.CheckUnitConquestTriggers 在征服后遍历攻方所有Conquest关键词单位
- 游戏结束和横幅系统已在DEV-9存在，确认标记完成

**新功能**:
- SimpleAI: AiRecycleRunes（智能回收符文获取符能，优先tapped→untapped）
- CombatSystem: CheckUnitConquestTriggers（征服后触发bad_poro等Conquest效果）
- bad_poro征服效果：摸1张牌

**修改文件**:
- `Assets/Scripts/AI/SimpleAI.cs`（+AiRecycleRunes方法+step 1b调用）
- `Assets/Scripts/Systems/CombatSystem.cs`（+CheckUnitConquestTriggers方法）
- `Assets/Tests/EditMode/DEV12AITests.cs`（新建，8个测试）

**测试结果**: 242 全绿（8 新 + 234 现有），0 失败

---

## DEV-11：符文Bug修复 + 剩余法术效果 + 入场效果 — 2026-03-31

**Status**: ✅ Completed

**技术决策**:
- 符文右键回收Bug：EventTrigger从根节点移到RuneCircle节点，解决Button拦截右键问题
- 符文拉伸Bug：容器HorizontalLayoutGroup关闭childControlHeight/ForceExpand + LayoutElement固定46×46
- 额外回合：GameState.ExtraTurnPending + TurnManager.DoEndPhase检查
- 鼓舞系统：GameState.InspireNextUnit标记 + EntryEffectSystem在下一单位入场时自动+1

**新功能**:
- SpellSystem: furnace_blast（回响，3单位各1伤害）
- SpellSystem: time_warp（额外回合）
- SpellSystem: divine_ray（回响，2伤害×2次）
- EntryEffectSystem: noxus_recruit_enter（鼓舞下一盟友+1）
- EntryEffectSystem: rengar_enter（反应+强攻+1炽烈符能）
- EntryEffectSystem: kaisa_hero_conquer（征服+1炽烈符能）
- EntryEffectSystem: yi_hero_enter（游走+急速+1摧破符能）
- EntryEffectSystem: sandshoal_deserter_enter（法盾+法术无法选中）
- EntryEffectSystem: 装备入场效果（trinity_equip/guardian_equip/dorans_equip）
- UnitInstance: HasReactive / UntargetableBySpells 字段
- GameState: ExtraTurnPending / InspireNextUnit 字段
- TurnManager: 额外回合处理（ExtraTurnPending=true时不切换玩家）

**修改文件**:
- `Assets/Scripts/UI/GameUI.cs`（符文右键修复+LayoutElement）
- `Assets/Scripts/Editor/SceneBuilder.cs`（HLG修复+新法术CardData+effectId修正）
- `Assets/Scripts/Systems/SpellSystem.cs`（+3法术效果+FurnaceBlast方法）
- `Assets/Scripts/Systems/TurnManager.cs`（+额外回合处理）
- `Assets/Scripts/Systems/EntryEffectSystem.cs`（+7入场效果+鼓舞机制）
- `Assets/Scripts/Core/GameState.cs`（+ExtraTurnPending+InspireNextUnit）
- `Assets/Scripts/Core/UnitInstance.cs`（+HasReactive+UntargetableBySpells）
- `Assets/Tests/EditMode/DEV11SpellEntryTests.cs`（新建，14个测试）

**测试结果**: 234 全绿（14 新 + 220 现有），0 失败

---

## DEV-10：数据补全 + 区域视觉 + 日志系统 — 2026-03-30

**Status**: ✅ Completed

**技术决策**:
- 英雄卡：CardData 添加 `_isHero` 字段，游戏初始化时从牌库提取到 GameState.PHero/EHero
- 传奇卡图：LegendInstance 添加 `DisplayData` 关联 CardData，RefreshLegends 渲染卡图
- 日志折叠：协程 0.3s SmoothStep 动画，联动 boardWrapperOuter 的 offsetMax
- 弃牌堆浏览器：全屏半透明遮罩 + GridLayoutGroup 网格 + ScrollRect 滚动
- 回合倒计时：Image.fillAmount 径向填充 + 颜色变化(绿→黄→红) + 协程每秒递减
- 区域标签：Outline 组件边框 + 9px 金色半透明 Text

**新功能**:
- CardData `_isHero` 字段 + `IsHero` 属性
- GameState `PHero`/`EHero` + `GetHero(owner)` + `SetHero(owner, hero)`
- LegendInstance `DisplayData` 属性
- GameManager `ExtractHero()` — 从牌库提取英雄到英雄区
- GameUI `RefreshHeroZones()` — 英雄区渲染（有英雄显示 CardView，无英雄显示"已出场"）
- GameUI `RefreshLegendArt()` — 传奇区卡图叠加
- GameUI `ToggleLog()` + `AnimateLogToggle()` — 日志折叠/展开 + 棋盘联动动画
- GameUI `ShowDiscardViewer()` / `ShowExileViewer()` — 弃牌堆/放逐堆网格浏览器
- GameUI `StartTurnTimer()` / `ClearTurnTimer()` — 30秒回合倒计时
- SceneBuilder: ViewerPanel（全屏遮罩+网格+关闭按钮）
- SceneBuilder: TimerDisplay（圆形进度条+秒数文本）
- SceneBuilder: LogToggleBtn（折叠按钮"<"/">"）
- SceneBuilder: 区域边框（Outline）+ 名字标签（ZoneLabel Text）
- SceneBuilder: kaisa_hero/yi_hero 标记 isHero=true
- SceneBuilder: kaisa_legend/yi_legend CardData 资产
- 弃牌堆/放逐堆 UI 添加 Button 组件（可点击打开浏览器）

**修改文件**:
- `Assets/Scripts/Data/CardData.cs`（+_isHero 字段）
- `Assets/Scripts/Core/GameState.cs`（+PHero/EHero/GetHero/SetHero）
- `Assets/Scripts/Core/LegendInstance.cs`（+DisplayData 属性）
- `Assets/Scripts/GameManager.cs`（+ExtractHero/OnTimerExpired/OnPileClicked + timer调用）
- `Assets/Scripts/UI/GameUI.cs`（+30个字段 + 7个新方法）
- `Assets/Scripts/Editor/SceneBuilder.cs`（+ViewerPanel/TimerDisplay/LogToggle/ZoneLabels/Borders）
- `Assets/Tests/EditMode/DEV10DataTests.cs`（新建，17个测试）

**测试结果**: 220 全绿（17 新 + 203 现有），0 失败

---

## DEV-9：UI 布局补全 — 2026-03-30

**Status**: ✅ Completed

**技术决策**:
- 布局：anchor 定位模拟 7×5 网格（替代 CSS Grid），不用嵌套 LayoutGroup
- 手牌区移到棋盘外部（敌方50px/玩家120px），匹配原版布局
- 传奇区删除 HP 显示（传奇不可被摧毁，规则167.3）

**新功能**:
- 得分轨道（左=玩家 8→0，右=敌方 0→8，各9个圆圈）
- 弃牌堆 + 放逐堆（双方各一组，显示计数）
- 英雄区（双方各一个槽位，等待英雄牌出场）
- 主牌堆计数 + 符文牌堆计数（双方各一个）
- 战场控制标记（绿=玩家/红=敌方）
- 信息条（双方：名字/法力/符文计数/牌堆计数）
- 行动面板（全部横置/取消/确认符文/跳过响应/结束行动/反应）
- 传奇区重做（网格内定位 + 技能按钮 + 无HP）
- GameState.PExile/EExile 放��堆数据
- RefreshScoreTrack/RefreshPileCounts/RefreshBFControlBadges/RefreshInfoStrips/RefreshActionButtons

**修改文件**:
- `Assets/Scripts/Core/GameState.cs`（+PExile/EExile）
- `Assets/Scripts/UI/GameColors.cs`（+15 颜色常量）
- `Assets/Scripts/Editor/SceneBuilder.cs`（重写布局 + 8个新方法 + 连线扩展）
- `Assets/Scripts/UI/GameUI.cs`（+30字段 + 5刷新方法）
- `Assets/Scripts/GameManager.cs`（+TapAllRunes/SkipReaction 按钮回调）
- `Assets/Scripts/UI/ReactiveWindowUI.cs`（+SkipReaction 公开方法）
- `Assets/Tests/EditMode/DEV9LayoutTests.cs`（新建，25个测试）

**测试结果**: 203 全绿（25 新 + 178 现有），0 失败

---

## DEV-8：视觉升级 Part 1 — 2026-03-30

**Status**: ✅ Completed

**技术决策**:
- Shader：手写 .shader (HLSL)，不用 Shader Graph（batch mode 兼容）
- 动画：协程 + AnimationCurve，不用 DOTween（零依赖，DEV-9 再引入）
- 字体：保持 Legacy UI.Text + 系统字体（SimHei/微软雅黑）

**新功能**:
- `GameColors.cs`（新建）：静态颜色常量类，Gold/Teal/Background + 6种符文色 + 8种卡牌状态色 + 发光色 + UI面板色，Hex() 工具方法
- `CardDetailPopup.cs`（新建）：右键弹窗，显示完整卡牌信息（图片/名称/费用/ATK/关键词+描述/效果文字/运行时状态），Escape 或点击外部关闭
- `CardTilt.cs`（新建）：3D 鼠标跟随倾斜，MAX_TILT=18°, LERP_IN=0.12, LERP_OUT=0.08，IPointerEnterHandler/ExitHandler
- `CardGlow.cs`（新建）：发光边框材质控制器，克隆材质/OnDestroy销毁（防泄漏），SetPlayable/SetHovered/SetNormal
- `CardGlow.shader`（新建）：UI Unlit 发光边框，conic rotation 粒子轨迹动画，边缘 smoothstep 淡出
- `CardShine.shader`（新建）：UI 径向白色渐变叠加，_ShineX/_ShineY 控制光泽位置
- `HexGrid.shader`（新建）：六边形网格背景 + 程序化噪点 + 暗角 + 中心青色光晕
- URP Post Processing Volume：Bloom(1.2/0.8) + ColorAdjustments(0.1/10) + Vignette(0.3) + FilmGrain(0.1)
- `CardView.cs`：IPointerClickHandler 右键支持 + 眩晕红色脉冲叠加层 + 增益指示物金色图标 + 费用不足压暗

**修改文件**:
- `Assets/Scripts/UI/CardView.cs`（重写：IPointerClickHandler + 视觉状态 + GameColors 迁移）
- `Assets/Scripts/UI/GameUI.cs`（CardDetailPopup 引用 + 右键回调透传 + 费用不足 mana 参数）
- `Assets/Scripts/GameManager.cs`（CardDetailPopup 引用 + OnCardRightClicked 方法）
- `Assets/Scripts/Editor/SceneBuilder.cs`（CreateCardDetailPopup + SetupPostProcessing + Prefab 新增 StunnedOverlay/BuffTokenIcon/CardTilt + 连线）
- `Assets/Scripts/Editor/FWTCG.Editor.asmdef`（添加 URP Runtime + Core Runtime 引用）

**新建文件**:
- `Assets/Scripts/UI/GameColors.cs`
- `Assets/Scripts/UI/CardDetailPopup.cs`
- `Assets/Scripts/UI/CardTilt.cs`
- `Assets/Scripts/UI/CardGlow.cs`
- `Assets/Shaders/CardGlow.shader`
- `Assets/Shaders/CardShine.shader`
- `Assets/Shaders/HexGrid.shader`
- `Assets/Tests/EditMode/DEV8VisualTests.cs`
- `Assets/Settings/PostProcessProfile.asset`（SceneBuilder 生成）

**测试**:
- `DEV8VisualTests.cs`（新建）：14项视觉系统测试（GameColors 6项 + 关键词 2项 + 视觉状态 4项 + UnitInstance 2项）
- 🔵 [引擎测试] 178/178 全绿（164前序 + 14新增），编译无 error CS

---

## DEV-7：AI 策略升级 — 2026-03-30

**Status**: ✅ Completed

**新功能**:
- `SimpleAI.cs`（完全重写）：9步回合流程替代原5步；新增7个 public static 方法供测试直接调用：
  - `AiBoardScore`：局面评分 = 得分差×3 + 手牌差/2 + 控场×2 + 战力差/3
  - `AiCardValue`：卡牌价值 = 攻费比×10 + Haste(+4) + Barrier(+3) + StrongAtk(+2) + Deathwish(+2) + Conquest(+1) + Inspire(+1)
  - `AiMinReactiveCost`：最低反应法术费用（预留法力）
  - `AiShouldPlaySpell`：判断是否出法术（法力充足 + 有效目标 + 非预留）
  - `AiSpellPriority`：法术优先级排序（rally_call > balance_resolve > slam > ...）
  - `AiChooseSpellTarget`：智能目标选择（slam优先BF敌方最高ATK / buff优先BF己方最强单位）
  - `AiDecideMovement`：移动决策（征服15 / 制胜12+分差 / 平局1 / 劣势-3 + 绝境/双空场特殊处理）
  - `AiUseLegendAbility`：传奇技能决策（卡莎手牌有炽烈法术且符能不足时使用虚空感知）
- `CanAfford`：法力 + 符能双重检查（DEV-4 AI 仅检查法力）
- 移动循环：反复调用 AiDecideMovement 直到无可动单位（等同于 JS 递归 aiAction）
- 双空场分兵策略：2个空未控战场时，分别向BF0和BF1各派1支队
- `TurnManager.cs`：TakeAction 调用现在传递 `_legendSys` 和 `_bfSys`（已注入但之前未转发）

**设计决定**:
- BF卡ID通过 `gs.BFNames[i]` 获取（BattlefieldState无card字段）
- rockfall_path战场不纳入移动候选（无法直接出牌，AI也不移动到此）
- 移动评分基于局面评分差（移动后评分 - 移动前评分）

**测试**:
- `DEV7AITests.cs`（新建）：29项AI逻辑测试（AiCardValue×3 / AiBoardScore×5 / AiMinReactiveCost×3 / AiSpellPriority×2 / AiShouldPlaySpell×5 / AiChooseSpellTarget×5 / AiDecideMovement×6）
- 🔵 [引擎测试] 164/164 全绿（135前序 + 29新增），编译无 error CS

**Files modified**:
- `Assets/Scripts/AI/SimpleAI.cs`（完全重写）
- `Assets/Scripts/Systems/TurnManager.cs`（TakeAction 调用增加 legendSys/bfSys 参数）
- `Assets/Tests/EditMode/DEV7AITests.cs`（新建）

---

## DEV-6：战场特殊能力（19张）— 2026-03-30

**Status**: ✅ Completed

**新功能**:
- `CardKeyword.cs`：新增 `Guard`（坚守，bit 13）关键词
- `UnitInstance.cs`：新增运行时标志 `HasStrongAtk` / `HasGuard`，构造器自动从 CardData 关键词初始化
- `GameRules.cs`：
  - BF 牌池改为 ID 存储（`KAISA_BF_POOL` / `YI_BF_POOL`）
  - 新增 `BF_DISPLAY_NAMES` 字典（19张全部中文名）
  - 新增 `STRONG_POWER_THRESHOLD = 5` 常量
  - 新增 `GetBattlefieldDisplayName(id)` 辅助方法
- `GameState.cs`：新增 `DreamingTreeTriggeredThisTurn` 标志（每回合重置）
- `BattlefieldSystem.cs`（新建）：全部 19 张战场卡效果，触发点挂钩在各系统：
  - Score: `ShouldBlockHoldScore` / `GetBonusScorePoints`
  - Hold phase: `OnHoldPhaseEffects`（altar_unity / aspirant_climb / bandle_tree / strength_obelisk / star_peak）
  - Unit move: `OnUnitEnterBattlefield`（trifarian_warcamp）/ `OnUnitLeaveBattlefield`（back_alley_bar）
  - Combat: `OnCombatStart`（reckoner_arena）/ `OnConquest`（hirana/reaver_row/zaun_undercity/strength_obelisk/thunder_rune）/ `OnDefenseFailure`（sunken_temple）
  - Passive: `CanRecallFromBattlefield`（vile_throat_nest）/ `CanPlayDirectlyToBattlefield`（rockfall_path）
  - Spell: `GetSpellDamageBonus`（void_gate）/ `OnSpellTargetsFriendlyUnit`（dreaming_tree）
- `ScoreManager.cs`：注入 BattlefieldSystem，AddScore 集成 forgotten_monument 阻断 + ascending_stairs +1
- `TurnManager.cs`：Inject() 接收 BattlefieldSystem；DoAwaken 重置 DreamingTreeTriggeredThisTurn；DoStart 调用 OnHoldPhaseEffects
- `CombatSystem.cs`：注入 BattlefieldSystem；MoveUnit 调用进/出触发；TriggerCombat 集成 reckoner_arena / OnConquest / OnDefenseFailure；新增 `ComputeCombatPower(units, isAttacking)` 支持 StrongAtk/Guard；RecallUnit 检查 vile_throat_nest
- `SpellSystem.cs`：注入 BattlefieldSystem；DealDamage 集成 void_gate；CastSpell 集成 dreaming_tree
- `GameManager.cs`：注入 BattlefieldSystem；OnBattlefieldClicked 加入 rockfall_path 拦截；Inject() 透传给 TurnManager
- `GameUI.cs` / `StartupFlowUI.cs`：战场名显示改用 `GameRules.GetBattlefieldDisplayName()`
- `SceneBuilder.cs`：添加 BattlefieldSystem AddComponent；WireGameManager 函数签名 + 连线更新

**设计决定 (DEV-6 简化)**:
- aspirant_climb / star_peak / hirana / reaver_row / zaun_undercity / thunder_rune / sunken_temple：自动选择目标，不弹目标选择UI
- bandle_tree：以场上单位的 distinct RuneType 数量作为地区多样性代理
- back_alley_bar / trifarian_warcamp：仅对玩家方单位生效（与 JS 原版一致）

**测试**:
- `DEV6BattlefieldTests.cs`（新建）：25 项战场系统测试（6 张活跃 BF 卡 + 被动效果）
- 🟢 [逻辑测试] 135/135 全绿（含前序测试），编译无 error CS

**Files modified**:
- `Assets/Scripts/Data/CardKeyword.cs`
- `Assets/Scripts/Core/UnitInstance.cs`
- `Assets/Scripts/Core/GameRules.cs`
- `Assets/Scripts/Core/GameState.cs`
- `Assets/Scripts/Systems/BattlefieldSystem.cs`（新建）
- `Assets/Scripts/Systems/ScoreManager.cs`
- `Assets/Scripts/Systems/TurnManager.cs`
- `Assets/Scripts/Systems/CombatSystem.cs`
- `Assets/Scripts/Systems/SpellSystem.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/UI/GameUI.cs`
- `Assets/Scripts/UI/StartupFlowUI.cs`
- `Assets/Scripts/Editor/SceneBuilder.cs`
- `Assets/Tests/EditMode/DEV6BattlefieldTests.cs`（新建）

---

## DEV-5：传奇技能系统 — 2026-03-29

**Status**: ✅ Completed

**新功能**:
- `LegendInstance.cs`（新建）：传奇运行时状态（独立HP/ATK/Level/Exhausted/AbilityUsedThisTurn），TakeDamage + Evolve 方法
- `LegendSystem.cs`（新建）：全部传奇逻辑；CreateLegend 工厂、ResetForTurn、UseKaisaActive、CheckKaisaEvolution、TryApplyMasteryiPassive、CheckLegendDeaths；静态事件 OnLegendLog
- `GameRules.cs`：新增 LEGEND_HP=20、LEGEND_EVOLUTION_KEYWORDS=4 常量
- `GameState.cs`：新增 PLegend/ELegend 属性 + GetLegend(owner) 助手方法
- `TurnManager.cs`：Inject() 扩展支持 LegendSystem；DoAwaken 调用 ResetForTurn
- `CombatSystem.cs`：TriggerCombat 战斗前调用 TryApplyMasteryiPassive（TempAtkBonus +2 孤守）
- `GameManager.cs`：InitGame 创建 PLegend=卡莎/ELegend=易大师；TryPlayUnit 后检查卡莎进化；OnLegendSkillClicked 按钮回调
- `GameUI.cs`：RefreshLegends 显示传奇HP/等级/休眠状态 + 虚空感知按钮可用性
- `SceneBuilder.cs`：新增 CreatePlayerLegendPanel（底左，深紫+虚空感知按钮）+ CreateEnemyLegendPanel（顶左，深红）；WireGameUI/WireGameManager 连线

**Bug fix**:
- SceneBuilder.cs L791：CreateDebugButton 缺少 color 参数 → 修复为传入 violet 颜色，并移除多余的手动 img.color 设置

**设计决定**:
- 传奇HP独立于 atk=HP 规则，使用 LegendInstance 独立管理
- Masteryi 被动 TempAtkBonus 由 ResetAllUnits 自动清零，无需手动清理
- 卡莎进化仅在玩家出牌后检查（进化只针对己方盟友关键词）
- AI 传奇技能决策推迟至后续 Phase

**测试**:
- `DEV5LegendTests.cs`（新建）：20项传奇系统测试，全绿
- 🔵 [引擎测试] 119/119 全绿，编译无 error CS

**Files modified**:
- `Assets/Scripts/Core/LegendInstance.cs`（新建）
- `Assets/Scripts/Systems/LegendSystem.cs`（新建）
- `Assets/Tests/EditMode/DEV5LegendTests.cs`（新建）
- `Assets/Scripts/Core/GameRules.cs`
- `Assets/Scripts/Core/GameState.cs`
- `Assets/Scripts/Systems/TurnManager.cs`
- `Assets/Scripts/Systems/CombatSystem.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/UI/GameUI.cs`
- `Assets/Scripts/Editor/SceneBuilder.cs`

---

## DEV-4：反应系统 — 2026-03-29

**Status**: ✅ Completed（含收尾补丁）

**新功能**:
- `ReactiveSystem.cs`（新建）：9张反应法术效果（swindle/retreat_rune/guilty_pleasure/smoke_bomb + scoff/duel_stance/well_trained/wind_wall/flash_counter），自动选目标，支持无效化机制
- `ReactiveWindowUI.cs`：TaskCompletionSource 异步窗口，玩家点选反应牌（无通过按钮，必须选择一张）
- `SimpleAI.cs`：AI施法后广播消息 + 2000ms 等待窗口（替代原自动弹窗），玩家主动点击反应按钮响应
- `TurnManager.cs`：Inject()扩展支持 SpellSystem/ReactiveSystem/ReactiveWindowUI 注入
- `GameManager.cs`：新增 _reactBtn 字段 + OnReactClicked() 异步方法（过滤可用反应牌→选牌→施放）
- `SceneBuilder.cs`：BottomBar 新增橙色【反应】按钮，移除 ReactiveWindowPanel 的通过按钮，连线 _reactBtn
- `GameRules.cs`：新增9张反应卡副本数配置

**设计决定**:
- 反应按钮始终可点击（不做状态门控），点击时实时过滤可用牌
- 无法响应时显示提示消息，不弹窗
- 倒计时 + 自动随机出牌推迟至 DEV-11

**测试**:
- `DEV4InteractionTests.cs`（新建）：16项反应系统交互测试，全绿
- 🟢 总计 96/96 全绿，编译无 error CS

**Files modified**:
- `Assets/Scripts/AI/SimpleAI.cs`
- `Assets/Scripts/Systems/TurnManager.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/Core/GameRules.cs`
- `Assets/Scripts/Editor/SceneBuilder.cs`
- `Assets/Scripts/UI/ReactiveWindowUI.cs`
- `plans/feature-checklist.md`
- `plans/tech-debt.md`

**New files**:
- `Assets/Scripts/Systems/ReactiveSystem.cs`
- `Assets/Scripts/UI/ReactiveWindowUI.cs`
- `Assets/Tests/EditMode/DEV4InteractionTests.cs`

---

## DEV-3 补丁：Bug修复 + 交互测试 — 2026-03-29

**Status**: ✅ Completed

**Bug修复**:
- Debug面板只显示2个按钮 → `VerticalLayoutGroup.childControlHeight = true`
- 法术目标无法点击敌方单位 → 敌方 `RefreshUnitList` 传入 `_onUnitClicked`（而非null）
- 日志文字被右侧截断 → MessageText `horizontalOverflow = Wrap`

**交互测试补写（CLAUDE.md新规则）**:
- `SpellSystemTests.cs`：23项 DEV-3 法术行为测试
- `DEV1InteractionTests.cs`：22项 DEV-1 核心交互（移动/战斗/召回/得分/符文）
- `DEV2InteractionTests.cs`：17项 DEV-2 系统交互（入场效果/绝念/Tiyana/Mulligan）
- 🟢 总计 80/80 全绿

**Files modified**:
- `Assets/Scripts/Editor/SceneBuilder.cs`（Debug面板layout）
- `Assets/Scripts/UI/GameUI.cs`（敌方单位点击回调）

**New test files**:
- `Assets/Tests/EditMode/SpellSystemTests.cs`
- `Assets/Tests/EditMode/DEV1InteractionTests.cs`
- `Assets/Tests/EditMode/DEV2InteractionTests.cs`

---

## DEV-3: 法术系统（SpellSystem + 目标选择）— 2026-03-29

**Status**: ✅ Completed

**What was done**:
- 创建 `SpellTargetType.cs` 枚举（None/EnemyUnit/FriendlyUnit/AnyUnit）
- 扩展 `CardData.cs`：添加 `_isSpell` + `_spellTargetType` 字段及 `EditorSetup` 参数
- 创建 `SpellSystem.cs`：10张非反应法术完整实现
  - Kaisa: hex_ray, void_seek, stardrop, starburst, akasi_storm, evolve_day
  - Yi: rally_call, balance_resolve, slam, strike_ask_later
- 更新 `GameManager.cs`：法术目标选择状态机（_targetingSpell）
  - 无目标法术：立即结算
  - 有目标法术：点击法术→进入选目标状态→点击合法目标→结算
  - 结束回合时自动取消并退还法力
- 更新 `CardView.cs`：法术卡紫色背景 + 底部显示"法"字
- 更新 `GameRules.cs`：添加10张法术卡复制数量（卡莎+11张→30总, 易+7张→26总）
- 更新 `SceneBuilder.cs`：SpellSystem AddComponent、CDS()快捷函数、spell cards入组
- 复制10张法术卡图片到 Assets/Resources/CardArt/

**New files**:
- `Assets/Scripts/Data/SpellTargetType.cs`
- `Assets/Scripts/Systems/SpellSystem.cs`

**Files modified**:
- `Assets/Scripts/Data/CardData.cs`
- `Assets/Scripts/Core/GameRules.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/UI/CardView.cs`
- `Assets/Scripts/Editor/SceneBuilder.cs`

**Technical debt**: balance_resolve 的"费用-2"条件效果推迟到 DEV-4（需手牌目标UI）

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

---

## DEV-2: 卡牌数据完整 + 入场/绝念系统 + 启动流程 UI — 2026-03-29

**Status**: ✅ Completed

**What was done**:
- CardKeyword.cs 新增 [Flags] 枚举（12个关键词）
- CardData.cs 扩展字段：keywords, effectId, isEquipment, equipAtkBonus, equipRuneType, equipRuneCost
- UnitInstance.cs 新增运行时字段：BuffTokens, TempAtkBonus, HasSpellShield
- SceneBuilder.CreateAllCardData() 重写：19张卡莎卡 + 10张易大师卡（含装备）全量数据
- GameRules.GetCardCopies() + 完整 CardCopies 字典
- EntryEffectSystem.cs 新建（6种入场效果：yordel/darius/thousand_tail/foresight/jax/tiyana）
- DeathwishSystem.cs 新建（2种绝念：alert_sentinel_die/wailing_poro_die）
- GameState.cs 新增 TiyanasInPlay 字典 + BFNames 数组
- GameRules.cs 新增战场牌池（KAISA_BF_POOL/YI_BF_POOL）+ PickBattlefield()
- ScoreManager.cs：Tiyana 被动检查（阻止对手据守得分）
- CombatSystem.cs：绝念触发（RemoveDeadUnits 后调用 DeathwishSystem）+ Tiyana 死亡清除标志
- GameManager.cs：_entryEffects 注入、TryPlayCard 触发入场效果、RunWithStartup 协程
- SimpleAI.cs：打出卡后触发入场效果
- TurnManager.cs：Inject 接收 EntryEffectSystem，传给 AI.TakeAction
- StartupFlowUI.cs 新建：掷硬币面板 + Mulligan换牌面板（最多2张）
- SceneBuilder.cs：CreateCoinFlipPanel/CreateMulliganPanel + WireGameManager 全量连线

**Decisions made**:
- 掷硬币界面无动画（文字显示）：动画推迟到 DEV-3
- 战场选择纯随机：玩家无选择权，在掷硬币界面展示结果
- Mulligan 最多换2张：与 JS 原版一致
- 卡牌图片跳过：tempPic/cards/ 文件名无法映射到卡牌ID，需用户手动处理

**Technical debt**: StartupFlowUI无动画 / 卡牌图片未导入

**Problems encountered**:
- 卡牌图片文件名（hash/OGN-xxx.png）无法自动映射到卡牌ID，需要用户提供映射表

---

## DEV-16：法术展示动画 — 2026-03-31

**Status**: ✅ Completed

**What was done**:
- SpellShowcaseUI.cs 新建：全屏法术展示覆盖层（MonoBehaviour Singleton）
  - 底部飞入（0.4s SmoothStep）+ 停留（0.5s）+ 向上飞出（0.35s）
  - ShowAsync(UnitInstance, owner) 返回 Task，通过 TCS + Coroutine 桥接
  - 显示：归属标签（玩家绿/AI红）、卡名、效果描述、卡图（可选）
  - IsShowing 属性追踪动画状态，null-safe
- GameManager.cs：CastPlayerSpellWithReactionAsync 结算前 await SpellShowcaseUI.ShowAsync
- SimpleAI.cs：CastAISpell 结算前 await SpellShowcaseUI.Instance?.ShowAsync
- SceneBuilder.cs：CreateSpellShowcasePanel() 新建全屏覆盖层，SerializedObject 连线全部引用字段
- DEV16ShowcaseTests.cs：15个EditMode测试（常量/null安全/单例行为/CardData.IsSpell/OWNER常量）

**Decisions made**:
- 动画使用 unscaledDeltaTime（游戏暂停时仍可播放）
- 展示面板初始 SetActive(false)，动画结束后再关闭
- SceneBuilder 内联 SerializedObject 连线，无需 WireGameManager 传参修改接口

**Technical debt**: 无新增

**Problems encountered**:
- Unity batch mode 测试不执行：-quit 与 -runTests 冲突导致项目加载后立即退出
  解决：去除 -quit 和 -nographics 标志，与工作的 dev15 run 参数保持一致
- SpellShowcaseUI.cs 编译错误：CardData.Art 不存在 → 修正为 CardData.ArtSprite

**Tests**: 312/312 EditMode 全绿（含 DEV16ShowcaseTests 15项）
