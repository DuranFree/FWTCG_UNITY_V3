# 开发日志 — FWTCG Unity 移植

---

## VFX-6：掷硬币金色粒子爆发 + 音效触发点 — 2026-04-04

**Status**: ✅ Completed
**Tests**: 706/706 EditMode 全绿 🟢

### 实现内容

**StartupFlowUI.cs（编辑）**：
- CoinBurstParticles 协程：硬币落定后 20 个金色粒子径向爆发，0.6s ease-out-quad
- 粒子参数：大小 8→2px，半径 0→130px，alpha 渐出
- LayoutElement.ignoreLayout 防止 VerticalLayoutGroup 干扰
- _burstParticles 追踪列表 + OnDestroy 清理（防中断泄漏）
- 音效触发点：_coinFlipStartClip（翻转开始）+ _coinFlipLandClip（落定），AudioTool CH_UI
- 两个 AudioClip 字段默认 null，用户提供音效后赋值即可

**SceneBuilder.cs（编辑）**：
- 音效字段注释占位（值为 null，待音效资产提供）

**测试（9 个新测试）**：
- VFX6CoinFlipTests：爆发常量、总时间、音效字段序列化、null 安全、DEV-24 常量不变

**Codex 审查**：1 HIGH 已修复（粒子中断泄漏）、3 MEDIUM 已修复/确认（LayoutGroup / 音效 null 设计预期 / Image 缓存）

**Decisions made**：
- 不新建 CoinFlipFX.cs 独立组件：DEV-24 已将动画内联在 CoinSpinRoutine，仅使用一次，提取属过度工程
- 翻转动画（scaleX 翻转 + 正反面切换 + 弹跳）已由 DEV-24 完成，VFX-6 仅增量实现粒子爆发 + 音效

**Technical debt**: 无新增

---

## VFX-5：音频框架升级（通道制 AudioTool）— 2026-04-04

**Status**: ✅ Completed
**Tests**: 697/697 EditMode 全绿 🟢

### 实现内容

**AudioTool.cs（新建）**：
- 通道制音频核心，11 个独立��道（bgm/ui/card_spawn/attack/death/spell/ambient/score/legend/duel/system）
- ��通道独立 AudioSource、优先��、音量控制
- 优先级系统：同通道内高优先级（>=）打断低优先级，跨通道互不干扰
- 6 级优先级：AMBIENT(10) < UI(20) < CARD(40) < COMBAT(60) < SPELL(80) < SYSTEM(100)
- FadeIn/FadeOut/CrossFade 协程（使用 unscaledDeltaTime，不受暂停影响）
- MasterVolume × BaseVolume 双层音量控制
- 单例模式 + OnDestroy 清理

**AudioManager.cs（重写）**：
- 9 个原始 SFX 方法签名完整保留（PlayCardPlay/PlaySpellCast/PlayCombatHit 等）
- 内部委托 AudioTool 通道：card_spawn/spell/attack/death/system/ui/score
- BGM 方法新增 FadeBGMIn/FadeBGMOut
- 防御性 fallback：AudioTool 不可用时创建自有 AudioSource 播放

**ButtonAudio.cs（新建）**：
- RequireComponent<Button>，Awake 自动订阅 onClick → PlayUIClick
- 可选 override AudioClip，OnDestroy 取消订阅

**SceneBuilder**：
- 新增 AudioTool GO（含 AudioTool + AudioManager 组件），在 GameManager 之前创建

**测试（30 个新测试）**：
- VFX5AudioToolTests：通道创建/Play/Priority/Stop/Volume/Fade/Singleton
- VFX5AudioManagerCompatTests：9 个 SFX 方法兼容性 + BGM + Volume
- VFX5ButtonAudioTests：RequireComponent/AddComponent/Field 验证
- VFX5AudioManagerFallbackTests：无 AudioTool 时 fallback 路径

**附带修复**：
- VFX-4 flaky 测试 ApplyBattlefieldVisuals_AppliesMicroRotation：±1° 范围扩大到 ±3°（Random.Range + 欧拉角浮点精度）

**Decisions made**：
- AudioTool 与 AudioManager 共存：AudioTool 是底层引擎，AudioManager 是高层门面
- 通道用 string 而非 enum：便于扩展，无需修改 AudioTool 代码
- PlayOneShot 独立于 Play：OneShot 允许重叠，Play 受优先级管控
- 实际音频文件需用户另行提供（两个工程均无 .wav/.mp3/.ogg）

**Technical debt**: FadeRoutine 外部 StopAllCoroutines 打断时 FadeRoutine 引用不���空（MEDIUM，当前无此路径）

---

## VFX-4：VFXResolver 自动映射 + 游戏事件集成 — 2026-04-04

**Status**: ✅ Completed
**Tests**: 617/617 EditMode 全绿 🟢

### 实现内容

**VFXResolver.cs（新建）**：
- 静态类，两层 FX 解析：effectId 精确映射 → RuneType 元素兜底
- FXConfig 结构体：prefab/delay/repeat/interval/tint/scale/duration
- 38 个 effectId → FX 组合硬编码映射（含多段/延迟/着色/缩放）
- 死亡 FX 独立映射表（deathwish 卡有专属死亡特效）
- RuneType → spawn_fx / idle_fx 自动映射（6 种元素各有对应粒子）
- Resources.Load 缓存机制，null 也缓存避免重复查找

**SpellVFX 集成**：
- OnCardPlayed：VFXResolver.Resolve → SpawnResolvedFX 协程（含延迟/重复/着色）
- OnUnitDiedAtPos：VFXResolver.ResolveDeathFX → SpawnResolvedFX
- 原有径向爆裂粒子保留作为环境层叠加

**CardView 战场视觉（4d）**：
- ApplyBattlefieldVisuals()：微旋转 ±1° + 阴影层 + idle FX + Shield 常驻
- CreateShadow()：偏移半透明 Image，0.4s 延迟 + 0.3s 淡入
- SpawnIdleFXDelayed()：1s 延迟后按 RuneType 生成 SnapFX 跟随粒子
- RefreshShieldFX()：SpellShield/Barrier → Shield prefab 常驻，状态解除时销毁
- HP/ATK 受击变黄：_lastKnownHp 追踪，HP 下降 → Color.yellow，恢复 → white
- ClearBattlefieldVisuals()：离场时清理所有战场视觉

**GameUI 集成**：
- RefreshBattlefields() 后自动调用 ApplyBFVisuals() 应用战场视觉

**FX Prefab 迁移**：
- 23 个 FX prefab 从 Assets/Prefabs/FX/ 移至 Assets/Resources/Prefabs/FX/，支持 Resources.Load

**推迟项**：
- 抽牌 FX（无 OnDrawCard 事件，需侵入多个 System）
- spawn_fx / death_fx CardData 覆盖（需新增字段，待有传奇卡专属特效需求时）

**Decisions made**：
- FX prefab 用 Resources.Load 而非 Inspector 引用，避免 SceneBuilder 膨胀
- idle FX 用 SnapFX 跟随而非挂载到 CardView 子节点，粒子在世界空间渲染
- 阴影用 UI Image 而非 SpriteRenderer，保持 Screen Space Overlay 一致性

**Technical debt**: 无新增

---

## VFX-2：FX 粒子预制体 & 材质 — 2026-04-04

**Status**: ✅ Completed
**Tests**: 550/550 EditMode 全绿 🟢

### 实现内容

**FX Sprites（32 张）**：
- 全部从 TCG Engine `TcgEngine/Sprites/FX/` 迁移到 `Assets/Sprites/FX/`
- 含 .meta 文件（GUID 保留），材质引用自动解析

**FX 材质（27 个）**：
- 全部从 `TcgEngine/Materials/FX/` 迁移到 `Assets/Materials/FX/`
- shader 引用：URP `ParticlesUnlit.shader`（GUID `0406db5a`）和 `Lit.shader`（GUID `933532a4`），均在项目 URP 14.0.12 中存在

**FX Prefabs — 优先（14 个）**：
- HitFX / Flame / ElectricFX / WaterFX / Leaf / Shield / Destroy / DestroyUI
- Spawn / SpawnFire / SpawnForest / SpawnWater / Phoenix / RayGlow

**FX Prefabs — 次要（9 个）**：
- Poisoned / Silenced / Immune / RootFX / Shell / PotionFX / DamageFX / CastFX / Zzz

**Decisions made**：
- 所有资产带 .meta 一并复制，GUID 完整保留，prefab → material → sprite 引用链自动生效
- 未修改任何 .cs 文件，零代码变更

**Technical debt**: 无新增

---

## VFX-1：Shader & 工具类导入 — 2026-04-04

**Status**: ✅ Completed
**Tests**: 编译 0 错误 0 警告 🟢

### 实现内容

**Shader 资产迁移（来自 TCG Engine URP 14.0.12）**：
- `ShaderDissolve.shadergraph` → Assets/Shaders/（含 .meta，GUID 保留）
- `ShaderHolo.shadergraph` → Assets/Shaders/（含 .meta）
- `Grayscale.shader` → Assets/Shaders/（含 .meta）

**材质迁移**：
- `KillDissolveFX.mat` → Assets/Materials/（引用 ShaderDissolve，GUID 完整）
- `GrayscaleUI.mat` → Assets/Materials/（引用 Grayscale.shader）

**工具类脚本（FWTCG.FX 命名空间）**：
- `AnimMatFX.cs`：材质属性插值驱动，修复 4 个 HIGH（GetFloat(null) 崩溃、HasProperty 守卫、最终值精度、stale 状态）
- `SnapFX.cs`：FX 附着 Transform 追踪组件
- `FXTool.cs`：静态 DoFX / DoSnapFX 工厂（移除 TcgEngine GameBoard 依赖，暴露 duration 参数，LOW null 守卫）

**Codex 审查**：4 HIGH 全部修复（GetFloat null 崩溃、HasProperty 守卫、动画最终帧精度、DoSnapFX duration 锁定）；3 MEDIUM 已修复或记入 tech-debt；2 LOW 修复。

**Decisions made**：
- FXTool 改为 `static class`（原版继承 MonoBehaviour 但只用静态方法，纯静态更合理）
- ShaderGraph .meta 一并复制保留 GUID，确保材质引用正确
- DoProjectileFX 留空（VFX-8 实现）

---

## DEV-32：架构优化 — 2026-04-03

**Status**: ✅ Completed
**Tests**: 550/550 🟢（EditMode 全绿，新增 23 个测试）

### 实现内容

**Medium 技术债修复（4项）**：
- `CardView.EnterAnimRoutine`：移动 `_enterAnimPlayed=true` 进协程（修竞态）；加首帧 alpha/scale 初始化（修首帧闪烁）；失败路径重置标志（加重试路径）
- `GameUI.RefreshScoreTrack`：保存 prevPScore/prevEScore 后再更新缓存，多分得分时对每个新亮圆圈均触发脉冲
- `RuneAutoConsume`：新增 `CanTap()` / `CanRecycle()` 静态方法作为单一规则源，Compute() 和 GameManager.OnRuneClicked 均统一使用
- `SpellVFX`：用 `_ownedParticles` HashSet 显式追踪粒子 GO，OnDestroy 精确清理（不再误删其他 _vfxLayer 子节点）

**测试缺口填补（23 个新测试）**：
- `DEV32BehaviorTests.cs`：RuneAutoConsume.CanTap/CanRecycle 边界条件、Haste 关键词检测与可负担性前置条件、CardDragHandler CanStartDrag=false 保护、OnDestroy 清理、回调空安全、GameState 资源算术

**BF 卡资产创建（19 张）**：
- 在 `Assets/Resources/Cards/BF/` 创建 19 张战场卡 CardData .asset 文件
- 数据层现完整，运行时通过 BattlefieldSystem 字符串 ID 匹配触发效果

**架构文档**：
- `docs/architecture.md`：总体架构图、模块职责表、事件系统说明、回合状态机、已知摩擦点列表、测试覆盖概况

**Codex 深度审查**：无 HIGH 级问题。

**Decisions made**：
- 架构摩擦点（GameState 8向耦合、无 ViewModel、伤害分散等）记录进架构文档和技术债，不在本 Phase 重构
- BF 卡资产使用 Python 直写 YAML，确保 GUID 唯一性

**Technical debt**：
- 架构文档中列出 6 项摩擦点（A1-A6）供未来重构参考，均为 Low/Medium 优先级

---

## DEV-31：Tech-Debt Cleanup — 2026-04-03

**Status**: ✅ Completed

**What was done**:

### 批次 1 — SpellDuelUI
- 添加单例守卫：Awake 加 `if (Instance != null && Instance != this) { Destroy(this); return; }` 防双重订阅
- _rootCanvas 在 Awake 缓存，移除每次 ShowDuelOverlay 调用 FindObjectsOfType<Canvas>()（O(n) 分配）

### 批次 2 — ReactiveWindowUI
- WaitForReaction 开头加 `_tcs?.TrySetCanceled()` 防止旧 Task 永久挂起（重入场景）
- OnDisable 加 `_tcs?.TrySetCanceled()` 防止组件禁用后悬挂 Task

### 批次 3 — 已确认已解决项
- ToastUI 去重 null/empty 守卫：已在 DEV-26 修复，确认无需处理
- EventBanner 去重忽略参数：已重构为 batch 系统，确认无问题
- GameUI.ShowCombatResult 无句柄：_crHideRoutine 已存在并正确使用，确认无问题
- SceneryUI.DividerOrbLoop 基准位置：DEV-26 已修复，仅缓存 baseY，确认正确
- AskPromptUI._cardViewPrefab null：SceneBuilder 已连线，优雅降级属设计预期，WONTFIX

### 批次 4 — CardDragHandler
- PortalVFX.EnsureBuilt 改用 GetComponentInParent<Canvas>() + rootCanvas 解析，移除 FindObjectOfType
- HandleDrop 开头加 GameManager.Instance null 检查（深度防御）

### 批次 5 — 配置/LOW 项
- GlassPanelFX Shader.Find：属项目配置项，标注打包前添加 Always Included Shaders
- 其余 LOW 项评估为实际风险极低，保留在 tech-debt.md

### 批次 6 — Low 项代码改动 + Codex HIGH 修复
- CombatAnimator.OnDestroy：补注释说明 Remove-before-Destroy 的单线程协程安全原因
- CreateOverlayImage sizeDelta 修复：offsetMin/offsetMax 加入 sizeDelta*0.5，HeroAura 现在正确扩展 4px/边
- CardView.Refresh PlayableSparkRoutine：加 `gameObject.activeInHierarchy` 守卫，防非活跃 GO 启动协程（Mulligan 面板场景）
- GameManager.cs：WaitForReaction await 加 try/catch OperationCanceledException，防 _reactionWindowActive 卡死
- tech-debt.md：标记 6 项已在 batches 1-4 修复，2 项设计待确认，1 项 WONTFIX

**Decisions made**:
- SpellDuelUI.OnClearBanners + ReactiveWindowUI.AutoPlayRandom 同步触发：设计待确认，保留
- AI 出牌不触发 OnCardPlayed：设计待确认，保留
- GlassPanelFX 配置项：待打包前处理

**Technical debt**:
- 仍有 ~15 项 Low/Medium 项保留（DamagePopup 对象池、BF卡 asset 创建、反应牌目标选择 UI 等），属功能后续 Phase 或配置项

**Problems encountered**:
- Codex 审查发现 HIGH：WaitForReaction await 无 catch，TrySetCanceled 导致 _reactionWindowActive 卡死 → 已修复

**Tests**: 527/527 EditMode 全绿

---

## DEV-30：视觉 + 功能收尾（最后功能 Phase）— 2026-04-03

**Status**: ✅ Completed
**Tests**: 527/527 🟢（MCP EditMode 全绿，新增 21 个测试）

### 实现内容

**V1 — 标题文字动画（StartupFlowUI.cs）**：
- `TitleTextEntranceRoutine`：对标题子 Text 逐个 fadeInUp + scale 0.85→1，0.35s stagger 0.1s

**V2 — 六边形呼吸光效（StartupFlowUI.cs）**：
- `HexBreathLoop`：`_titleHexOverlay` alpha 0.3↔0.6，3s 正弦循环

**V3 — 标题中心光束脉冲（StartupFlowUI.cs）**：
- `TitleBeamPulseLoop`：`_titleBeam` alpha 0→0.5→0，2s 周期

**V4/V8 — 按钮入场动画（SceneBuilder.cs）**：
- 硬币面板「继续」按钮 + 梦想手牌确认按钮均添加 `ButtonCharge` 组件

**V5 — 分层背景光效（StartupFlowUI.cs）**：
- `BgGradientRotateLoop`：`_bgGradientOverlay` 持续慢速旋转，Hextech 青色极低透明度

**V6 — 入场 Foil Sweep（CardView.cs）**：
- `EnsureShineOverlay`：懒创建 "ShineOverlay" Image 子节点，克隆 CardShine.shader 材质
- `FoilSweepRoutine`：0.8s 对角扫光（左下→右上），`_shineMat` 在 OnDestroy 销毁

**V7 — 可出牌卡粒子特效（CardView.cs）**：
- `PlayableSparkRoutine`：每 0.6s 生成 6px 白点，Y 上浮 20px，0.5s 生命周期
- `SetPlayable(true)` 启动，`SetPlayable(false)` 停止并清理 `_sparkDots`

**F1 — 征服得分粒子爆发（SpellVFX.cs + GameEventBus.cs）**：
- `GameEventBus.OnConquestScored` + `FireConquestScored(owner)` 新增
- `SpellVFX.OnConquestScored`：金色 20 点径向爆裂，获取得分区 canvas 坐标

**F2 — 法术对决全屏叠加 UI（SpellDuelUI.cs + ReactiveWindowUI.cs）**：
- `SpellDuelUI`（新建）：订阅 `OnDuelBanner/OnClearBanners`，程序化创建边框脉冲（4×4px 青色）+ 30s 倒计时 + 进度条；超时调用 `ReactiveWindowUI.Instance?.AutoSkipReaction()`
- `ReactiveWindowUI`：新增 `Instance` 单例 + `AutoSkipReaction()` → `SkipReaction()`
- `GameManager.Awake`：`if (_spellDuelUI == null) AddComponent<SpellDuelUI>()`

**F4 — 装备卡显示（CardView.cs）**：
- `RefreshBuffBadges()` 中 `AttachedEquipment != null` 时显示 `_equipBadge`，文字设为装备卡名

**技术修复（跨 Phase 泛化）**：
- SpellVFX 所有事件处理器添加 `if (!this) return;` 守卫（OnCardPlayed / OnUnitDiedAtPos / OnLegendEvolved / OnConquestScored）
- SpellDuelUI ShowDuelOverlay/HideDuelOverlay/StopRoutines 添加 `if (!this) return;` 守卫
- 修复全局测试跨类污染（stale 订阅调用已销毁对象的 isActiveAndEnabled 属性导致 MissingReferenceException）

**DEV30Tests.cs（新建）**：
- 21 个 EditMode 测试，覆盖 F1/F2/F4/V6/V7

### 代码审查
- Claude 自身审查：已确认无立即需修复的 High 问题
- 🔍 Codex adversarial-review：High×3（HIGH-1 为设计预期；HIGH-2/HIGH-3 为潜在风险，已记入 tech-debt）+ Medium×5 + Low×2（均记入 tech-debt）

**追加修复（收尾阶段）**：
- CardView.Setup：入场动画添加 `gameObject.activeInHierarchy` 守卫，防止隐藏面板中启动协程
- CardView.EnterAnimRoutine：开头 `yield return null` + `Canvas.ForceUpdateCanvases()`，等 LayoutGroup 计算完位置再读 anchoredPosition
- CardView.PlayableSparkRoutine：spark dot 添加 `LayoutElement.ignoreLayout = true`，防止 HorizontalLayoutGroup 重排手牌

### 代码审查（收尾阶段）
- Claude 自身审查：三处修复正确，无 High 问题
- 🔍 Codex adversarial-review：Medium×3（EnterAnimRoutine 无重试路径/一帧闪烁/竞态），均已记入 tech-debt

### 场景验证
- ✅ 场景已重建（FWTCG/Build Game Scene），SpellDuelUI 由 GameManager.Awake 程序化添加
- ✅ MCP 验证：GameManager 27 组件连线正确，SpellShowcasePanel/ReactButton 存在且有 ButtonCharge
- ⚠️ [引擎场景验证] 标题动效 / Foil Sweep / 粒子特效为 Play Mode 视觉效果，需用户手动 Play Mode 确认

---

## DEV-29：死亡飞行 + 卡背样式 + DEV-28 技术债修复 — 2026-04-03

**Status**: ✅ Completed
**Tests**: 506/506 🟢（MCP EditMode 全绿，新增 17 个测试）

### 实现内容

**T1 — ShowTargetHighlights 移除手牌容器（GameUI.cs）**：
- `ShowTargetHighlights()` 和 `ClearTargetHighlights()` 的容器数组中移除 `_playerHandContainer`，防止手牌卡被高亮为可选目标

**T2 — CardView HeroAura 清除（CardView.cs）**：
- 新增 `ClearHeroAura()`：停止 `_heroAuraPulse` 协程 + 销毁 `_heroAura` GO（运行时用 Destroy，EditMode 用 DestroyImmediate）
- `Setup()` 中 `isNewUnit` 时调用 `ClearHeroAura()` 并重置 `_enterAnimPlayed = false`

**T3 — try/finally 保护高亮清理（GameManager.cs）**：
- 法术目标选择和装备目标选择路径均用 try/finally 包裹，确保 `ClearTargetHighlights()` 总被调用

**T4 — CombatFlyGhost 追踪（CombatAnimator.cs）**：
- 新增 `_activeGhosts` List，`FlyAndReturnRoutine` 创建 ghost 后入队，完成后移除
- `OnDestroy` 遍历销毁残留 ghost（运行时 Destroy，EditMode DestroyImmediate）

**F1 — 死亡飞行动画（CardView.cs + GameUI.cs）**：
- `DeathRoutine` 改为两阶段：Phase A (0.3s) 缩小到 60% + 红色闪烁；Phase B (0.5s) 创建 ghost，沿贝塞尔弧线飞向弃牌堆，scale 0.6→0.15 + alpha 渐出
- `_deathGhost` 字段追踪飞行 ghost，`OnDestroy` 安全清理
- `capturedSize` 在 `SetActive(false)` 前缓存，防止 layout 重建后 rect.size 归零
- `GameUI.OnUnitDiedHandler` 计算弃牌堆 canvas-local 坐标传入 `PlayDeathAnimation`；合并两处 `GetRootCanvas()` 调用消除重复

**F2 — 卡牌背面几何纹样（CardView.cs）**：
- 新增 `_cardBackOverlay` 字段 + `EnsureCardBackOverlay()`：懒创建，四边各 3px 边框条（Image）+ 中央 28×28px 菱形（45° 旋转，GameColors.CardBack 浅色），全部 raycastTarget=false
- `RefreshFaceDown(true)` 激活 overlay；`RefreshFaceDown(false)` 隐藏（不销毁，可复用）

**DEV29Tests.cs（新建）**：
- 17 个 EditMode 测试，覆盖 T1-T4 和 F1-F2 全部修复
- 修复 DEV28VisualTests.cs 预存编译错误（`gs.InitGame` → `new GameState()`）

### 代码审查
- Claude 自身审查：发现2 High（ghost泄漏 + size归零），已全部修复
- 🔍 Codex adversarial-review：High×2（已修复）+ Medium×2（已修复）+ Low×4（记入 tech-debt）

### 场景验证
- ✅ CombatAnimator._bf1Panel/bf2Panel 连线正确
- ✅ GameUI._playerDiscardCountText/_enemyDiscardCountText 连线正确（死亡飞行目标）

---

## DEV-28：卡牌交互视觉补全 — 2026-04-03

**Status**: ✅ Completed
**Tests**: 489/489 🟢（MCP EditMode 全绿，新增 5 个测试）

### 实现内容

**TurnManager.cs（修改）**：
- `DestroyEphemeralUnits()` 两处（base + battlefield 循环）各加 `gs.GetDiscard(owner).Add(u)`，修复 Tech-Debt DEV-18：瞬息单位销毁后进入弃牌堆

**CombatSystem.cs（修改）**：
- 新增静态事件 `OnCombatWillStart: Action<int, List<UnitInstance>, List<UnitInstance>>`
- `TriggerCombat()` 在伤害计算前触发事件，供 CombatAnimator 飞行 VFX 使用

**CardView.cs（修改）**：
- `SetTargeted(bool)` + `TargetPulseRoutine()`：绿色 #4ade80 脉冲边框，alpha 0.3↔0.85，1.2s 周期
- `StartOrbit()` / `StopOrbit()` / `OrbitRoutine()`：10px 金色光点半径 60px 旋转，6s 周期；选中时启动，取消选中时停止
- `StartHeroAura()` / `HeroAuraPulseRoutine()`：金色叠加层 alpha 0.25↔0.60 呼吸，4s 周期；英雄卡首次 Setup 时启动
- `EnterAnimRoutine()`：Y -30px 飞入 + scale 0.82→1 + alpha 0→1，0.42s EaseOutQuad；每个新卡牌实例首次绑定时播放一次
- `CreateOverlayImage()` 辅助方法：全尺寸 Image 子节点，支持可选 expand 偏移

**GameUI.cs（修改）**：
- `ShowTargetHighlights(Func<UnitInstance, bool> filter)`：遍历所有单位容器调用 `cv.SetTargeted(filter(cv.Unit))`
- `ClearTargetHighlights()`：全部调 `cv.SetTargeted(false)`

**GameManager.cs（修改）**：
- 法术出牌前后：`_ui?.ShowTargetHighlights(...)` / `_ui?.ClearTargetHighlights()`，按 SpellTargetType 过滤敌/己/任意单位
- 装备出牌前后：同上，过滤无附件的友方非法术单位

**CombatAnimator.cs（修改）**：
- 订阅 `CombatSystem.OnCombatWillStart`
- `AnimateFlyGroup()` 每侧最多 2 个单位，找到对应 CardView 后启动协程
- `FlyAndReturnRoutine()`：创建 ghost overlay，复制卡图，EaseOutQuad 向敌侧飞出 28px 0.20s，EaseInQuad 弹回 0.15s，销毁 ghost

**Assets/Tests/EditMode/DEV28VisualTests.cs（新建）**：
- 3 个 Ephemeral 弃牌堆测试（base 销毁、同回合存活、战场销毁）
- 2 个 OnCombatWillStart 测试（事件顺序、攻守列表内容正确）

### 代码审查
- Claude 自身审查：无 High，2 Low 记入 tech-debt
- Codex adversarial-review：无 High，4 Medium + 2 Low 记入 tech-debt

### 场景验证
- ✅ CombatAnimator 组件已验证：_bf1Panel → BF1Panel，_bf2Panel → BF2Panel，事件订阅已在 Awake/OnDestroy 中对称注册

---

## DEV-27：Architecture Improvement — 2026-04-03

**Status**: ✅ Completed
**Tests**: 489/489 🟢（MCP EditMode 全绿，新增 22 个测试）

### 实现内容（P1 + P2，跳过 P3 GameManager 拆分）

**TurnStateMachine.cs（新建）**：
- 4态静态类：Normal_ClosedLoop / Normal_OpenLoop / SpellDuel_OpenLoop / SpellDuel_ClosedLoop
- TransitionTo（重复态无操作）、OnStateChanged 事件、IsPlayerActionPhase / IsSpellDuelOpen 便利查询
- CanPlaySpell(CardData)：Rule 718 实现（Normal 普通法术 / SpellDuel Reactive+Swift）
- Reset()：InitGame 调用，防止静态字段跨场景污染

**TurnManager.cs（修改）**：
- DoAction() 玩家行动阶段前后加 Normal_OpenLoop / Normal_ClosedLoop 转换

**GameManager.cs（修改）**：
- InitGame() 加 TurnStateMachine.Reset()（High 修复）
- CastPlayerSpellWithReactionAsync / BeginPlayerReactionWindow 加 SpellDuel_OpenLoop 转换
- AiTryReact + 玩家反应窗口两处 Reactive 筛选加 Swift（Rule 718 DEV-27 TODO）
- 移除5个 static 事件声明，改为转发方法到 GameEventBus（向后兼容）

**CombatSystem.cs（修改）**：
- 绝念结算改为多轮循环（maxChainDepth=8），每轮调用 OnUnitsDied 后检测新阵亡
- 新增 FindNewlyDead 私有辅助方法

**GameEventBus.cs（修改）**：
- 新增5个迁移事件：OnCardPlayFailed / OnUnitDamaged / OnUnitDied / OnCardPlayed / OnHintToast
- 各自配套 Fire* 静态方法

**GameUI.cs（修改）**：
- 新增 singleton Instance 属性（CardDragHandler 使用）
- OnDestroy 清除 Instance（防止跨场景悬空引用）
- 事件订阅迁移：GameManager.* → GameEventBus.*

**SpellVFX.cs / ToastUI.cs（修改）**：事件订阅迁移

**DEV18CombatVisualTests.cs（修改）**：OnCardPlayed 测试改用 GameEventBus

**DEV27ArchitectureTests.cs（新建）**：22 个测试覆盖 TurnStateMachine 全态转换、Rule 718、GameEventBus 5事件、CombatSystem 绝念链

### 技术决策
- P3（GameManager 拆分）跳过：用户确认，收益不足以抵消改动风险
- Unity asset 导入缺 .meta 导致 TurnStateMachine 首次编译不可见，Assets/Refresh 解决
- TurnStateMachine.Reset() 由 InitGame() 调用而非 Awake()，确保每局游戏重置

### 技术债（新增）
- 无新增（已有 Medium 项未变化）

---

## DEV-26：Tech-Debt Cleanup — 2026-04-03

**Status**: ✅ Completed
**Tests**: 466/466 🟢（MCP EditMode 全绿）

### 实现内容（13 个文件）

**GameUI.cs**：OnUnitDamaged / OnUnitDied 事件订阅从 Awake/OnDestroy 改为 OnEnable/OnDisable；FindCardView 改为 public

**GameManager.cs**：
- OnBattlefieldClicked async void 外层加 try/catch，catch 中补 RefreshUI()
- 新增 HandleForesightPromptAsync(owner)：异步弹出"置底/保留"对话，await AskPromptUI.WaitForConfirm
- TryPlayUnitAsync 在 OnUnitEntered 后调用 HandleForesightPromptAsync（Foresight 关键词触发）
- TryPlayHeroAsync 补同样的 Foresight 调用（HIGH 修复）

**CombatSystem.cs**：CombatResult struct 新增 int BFIndex 字段，TriggerCombat 填入 bfId

**CombatAnimator.cs**：OnCombatResult 改用 result.BFIndex 替代 Contains("1") 字符串解析（HIGH 修复）；新增 _sw1Routine/_sw2Routine 句柄，避免并发动画累积

**DEV18bEventFeedbackTests.cs**：lambda 取消订阅改为命名变量，修复 unsubscription 泄漏

**ToastUI.cs**：Enqueue 加 string.IsNullOrEmpty 守卫

**SpellVFX.cs**：OnDestroy 清理 _vfxLayer 所有子节点

**CardDragHandler.cs**：FindObjectsOfType 替换为 GameUI.Instance.FindCardView（O(n) → O(1)）

**SceneryUI.cs**：DividerOrbLoop 只缓存 baseY，消除每帧 anchoredPosition 读取赋值污染 X

**StartupFlowUI.cs**：OnDisable 停止 _scanLightRoutine；FadeIn/FadeOut 内循环加 null 守卫

**CardView.cs**：OnDestroy 显式 StopCoroutine _shake / _flash / _death

**BattlefieldGlow.cs**：CtrlGlowLoop 无控制方时强制 alpha=0，修复黑色叠层可见 artifact

**FloatText.cs**：_pool.Clear() 在重建前清除跨场景 stale 引用；GetFromPool 加 null 守卫

**EntryEffectSystem.cs**：foresight_mech_enter 注释更新（玩家 prompt 已移至 GameManager）

### 技术决策
- Foresight 异步 UI 放在 GameManager（HandleForesightPromptAsync）而非 EntryEffectSystem，避免 EntryEffectSystem 引入 async 依赖
- BFIndex 加入 CombatResult 而非用显示名称字符串解析，彻底去除脆弱 Contains("1") 判断
- FindCardView 改 public 而非新增 wrapper，最小改动面

### 技术债（新增）
- 无新增

---

## DEV-25：玻璃态 UI + 三徽章布局 + 装备标签 — 2026-04-03

**Status**: ✅ Completed（音频部分跳过，无音频文件）
**Tests**: 466/466 🟢（MCP EditMode 全绿，新增 15 个测试）

### 实现内容

**Assets/Shaders/GlassPanel.shader（新建）**：
- 程序化磨砂玻璃效果（ScreenSpaceOverlay 无法使用 `_CameraOpaqueTexture`，改用双频 value noise 模拟）
- 两倍频叠加 noise frost + 顶部 highlight stripe + 底部 vignette + 边框正弦脉冲（1.2Hz）
- CGPROGRAM，Blend SrcAlpha OneMinusSrcAlpha，ZTest Always

**GlassPanelFX.cs（新建）**：
- `[RequireComponent(typeof(Image))]`，Awake → `Shader.Find("FWTCG/GlassPanel")` → `new Material(shader)` with `HideFlags.DontSave`
- `OnDestroy` 清理 Material 防泄漏
- `SetBorderColor(Color)` / `SetTintAlpha(float)` 运行时 setter
- M-2 修复：shader 找不到时输出 `Debug.LogWarning`，提示加入 Always Included Shaders

**UnitInstance.cs（修改）**：
- `HasBuff`：移除 `AttachedEquipment != null` 条件，装备不再触发 buff 徽章
- `BuildBuffSummary()`：去掉装备段，保留 `equipBonus` 减法防止 double-count
- 新增 `BuildEquipSummary()`：返回装备名 + 战力加成 + 描述，无装备返回"无"

**CardView.cs（修改）**：
- 三徽章横排：▲绿 buff（-22, -2）/ ▲金 equip（0, -2）/ ▼红 debuff（+22, -2），anchor bottom-center，pivot top
- `CreateStatusBadge()`：Container → Glow（32×30，20% alpha + shadow）+ Body（暗色 + drop shadow + outline）+ Symbol（文字 + shadow）+ EventTrigger（右键 tooltip / Enter 放大 / Exit 复位）
- `ScaleBadge()`：`Dictionary<GameObject, Coroutine>` 管理协程句柄，避免 ref-in-lambda
- `ShowStatusTooltip(BadgeTip)`：新增 `_currentStatusTip` 字段；同徽章右键 toggle-off，不同徽章右键关旧开新（M-1 修复）

**SceneBuilder.cs（修改）**：
- SchCostBg 位置：从 `(0.05-0.55, 0.02-0.12)` 移到 `(0-0.25, 0.70-0.84)`（符纹费用移到法力费用下方）
- CardDetailPopup DetailPanel / SpellShowcasePanel CardPanel / AskPromptPanel DialogBox 各加 `GlassPanelFX` 组件

**DEV25VisualTests.cs（新建，15 个测试）**：
- GlassPanelFX 组件挂载 / SetBorderColor / SetTintAlpha / Destroy
- HasBuff 无 buff / 有 buff / 装备不触发 buff
- BuildBuffSummary 不含装备、BuildEquipSummary 空 / 含装备名
- Shader 常量范围（borderWidth / noiseScale / noiseStr）
- 徽章位置对称性 / Y 值在卡牌下方

### 技术决策
- ScreenSpaceOverlay Canvas 无法采样屏幕背景纹理，使用程序化 noise 模拟磨砂玻璃视觉
- 跨徽章 tooltip 切换使用 `BadgeTip?` null 字段记录当前打开的徽章类型

---

## DEV-24：开始流程 + 过渡动画 — 2026-04-03

**Status**: ✅ Completed
**Tests**: 451/451 🟢（MCP EditMode 全绿）

### 实现内容

**StartupFlowUI.cs（重写视觉层）**：
- `CanvasGroup` 面板管理：`HidePanel`/`EnsureCG`，Awake 初始化；OnDestroy 清理 TCS 防泄漏
- 掷硬币翻转动画：`CoinSpinRoutine` 5次 scaleX 翻转（每次 0.13s × 2）+ 落地弹跳 0.3s = 总时长 ~1.8s；`ScaleCoinX` 加 mid-frame null guard
- 结果文字淡入：`FadeTextIn` 0.4s
- 扫光背景循环：`ScanLightLoop` 蓝色水平条，8s 周期
- MulliganPanel 进场/退场：CanvasGroup 0.4s 淡入 / 0.3s 淡出
- TCS 追踪：`_activeCoinTcs` / `_activeMulliganTcs`，OnDestroy resolve 防永久挂起
- 按钮双击防护：onClick 触发时立即 disable + RemoveAllListeners

**GameUI.cs（2处改动）**：
- `ShowGameOver`：改为 CanvasGroup `FadeInPanelRoutine` 0.5s 淡入；mid-frame null guard
- `ShowMessage`：追加 `LogEntryFlashRoutine`（金色 #f2d266 → 原色，0.8s，null guard）

**SceneBuilder.cs（扩展 CoinFlipPanel）**：
- CoinFlipPanel 新增子节点：CoinTitle（金色 44pt 标题）/ CoinContainer（160×160）/ CoinCircle Image（金色圆）/ CoinFaceText（先/后/?）/ CoinResultText（透明，flip 后淡入）/ ScanLight Image（ignoreLayout，水平扫光条）
- CoinFlipPanel / MulliganPanel / GameOverPanel 均添加 CanvasGroup
- WireGameManager 连线新字段：_coinCircleImage / _coinResultText / _scanLightImage

**新增测试**：
- `DEV24StartupTests.cs`：20 个 EditMode 测试（常量验证、字段存在性、null 安全、双击防护时序）

### Codex 审查修复

- **H-1 已修复**：TCS 追踪 + OnDestroy resolve，防止协程提前退出导致 Task 永久挂起
- **H-2 已修复**：按钮 onClick 触发时立即 disable + RemoveAllListeners，防止 Mulligan 双击损坏手牌
- **H-3 已修复**：ScaleCoinX / CoinSpinRoutine 所有 yield 后加 null check，防 MissingReference
- **H-4（Medium 改为已修复）**：FadeInPanelRoutine mid-frame null guard

### 技术债新增

- ScanLightLoop 协程无 OnDisable 停止路径（OnDestroy 已覆盖，低风险）— DEV-24（Codex Medium）
- FadeIn/FadeOut 协程无 mid-frame CanvasGroup null guard（销毁场景时低概率抛出）— DEV-24（Codex Medium）

---

## DEV-23：字体 + 视觉细节打磨 — 2026-04-02

**Status**: ✅ Completed
**Tests**: 431/431 🟢（MCP EditMode 全绿）

### 实现内容

**字体**：沿用 simhei.ttf（黑体），跳过 Cinzel / Noto Sans SC 导入（无字体文件）

**GameColors.cs（修改）**：
- 新增 `BlueSpell = rgba(60,140,255,1)` 和 `BlueSpellDim = rgba(60,140,255,0.35)`

**SceneryUI.cs（新建）**：
- `SpinLoop`：CW/CCW 无限旋转协程，`Mathf.Repeat` 防浮点漂移，null guard 防目标销毁
- `DividerOrbLoop`：3.5s 正弦 Y 振荡 ±20px
- `CornerGemLoop`：4s alpha 脉冲 0.3↔0.9，4个角落同步
- `LegendGlowLoop`：5s 呼吸发光 0.15↔0.6，静态方法防 this 引用，null guard

**SceneBuilder.cs（修改）**：
- 新增 `DecorLayer`（fullscreen，layerZ=ParticleBGLayer+1，raycastTarget=false）
  - SpinOuter 180px / SpinInner 120px（蓝/青，中心锚）
  - SigilOuter 280px / SigilInner 190px（金/金亮，中心锚）
  - DividerOrb 18px（蓝，中心锚）
  - CornerGem0-3 48px（金，四角锚，±16px 偏移）
  - LegendGlow 覆盖传奇区（pLegendZone / eLegendZone）
- 新增 `CreateDecorDisc` / `CreateLegendGlowOverlay` helper
- 全部颜色改用 GameColors 常量（L-01 修复）

**新增测试**：
- `DEV23SceneryTests.cs`：19 个 EditMode 测试（常量验证、alpha 范围、AddComponent、字段连线）

### Codex 审查修复

- **M-02 已修复**：LegendGlow 从 heroContainer → pLegendZone/eLegendZone（传奇槽位，非英雄槽）
- **M-01 已修复**：SpinLoop / LegendGlowLoop 协程加 null guard，目标销毁时安全退出
- **L-02 已修复**：SpinLoop 用 Mathf.Repeat 替代累加，消除浮点漂移风险
- **L-01 已修复**：SceneBuilder 中硬编码颜色全部改用 GameColors.BlueSpell

### 技术债新增

- M-03：DividerOrb 基准位置只采样一次，分辨率变化时振荡中心偏移（风险低，游戏内无动态分辨率切换）

---

## DEV-22：拖拽出牌系统（含漩涡视觉） — 2026-04-02

**Status**: ✅ Completed
**Tests**: 412/412 🟢（batchmode EditMode 全绿）

### 实现内容

**新增文件**：
- `CardDragHandler.cs`：IBeginDragHandler/IDragHandler/IEndDragHandler，ghost 克隆跟随，集群动画，drop 区域检测
- `PortalVFX.cs`：3 层同心旋转圆盘 + 8 轨道粒子，淡入 0.28s / 淡出 0.22s，纯 UGUI 实现

**修改文件**：
- `GameManager.cs`：新增 IsPlayerActionPhase、IsUnitInHand、IsUnitInBase、GetSelectedBaseUnits、OnDragCardToBase、OnSpellDraggedOut、OnDragUnitsToBF
- `GameUI.cs`：SetDragCallbacks、SetupDragZones，RefreshUnitList 中为玩家牌线路回调，OnDestroy 清空静态字段
- `SceneBuilder.cs`：CardPrefab 追加 CardDragHandler + PortalVFX 组件

**新增测试**：
- `DEV22DragTests.cs`：14 个 EditMode 测试（PortalVFX 常量、拖拽回调分配、zone 静态引用、null 安全）

### Codex 审查修复

- **H-1**：GameUI.OnDestroy 清空 CardDragHandler 5 个静态字段，防止场景重载后持有 destroyed 引用
- **H-2**：CardDragHandler.OnDestroy 恢复 cluster 位置 + 销毁 ghost，防止 mid-drag 刷新时 ghost 泄漏
- **H-3**：ClusterFollowRoutine 内 ghostRT 获取后再次检查 null，消除同帧 NRE
- **H-4**：CreateGhost 中 Destroy(PortalVFX)，防止 ghost 销毁时破坏原始 VFX 子对象
- **M-3**：删除 PortalVFX 中 dead `cut` GameObject 代码

### 技术债记录

- M-1: PortalVFX.EnsureBuilt fallback canvas 查找方式（GetComponentInParent 更安全）
- M-2: HandleDrop 未在 drop 时重验证游戏状态（GameManager 回调内部已验证，深度防御待补）
- M-4: GatherCluster 中 FindObjectsOfType 多次调用，待 GameUI 维护 UnitInstance→CardView 查找表

## DEV-22 补丁：装备 UX + Buff 徽章 + 弹窗超时 — 2026-04-02

**Status**: ✅ Completed
**Tests**: 编译全绿（1 个预存警告，与本次无关）

### 实现内容

**装备牌双阶段流程**：
- 手牌→基地（TryDeployEquipmentToBase：扣费/加入 PBase/按 Standby 关键词决定是否休眠）
- 基地→附着（ActivateEquipmentAsync：RefreshUI → HideEquipCardInBase → SpellTargetPopup 选目标 → 飞行动画）
- 装备卡隐藏（alpha=0）等待选择完成，确认飞向目标单位，取消飞回基地原位
- 坐标系修正：RectTransformToCanvasLocal() 将任意 RectTransform 中心转换为 canvas-root 局部坐标

**符文彩色文字**：
- RuneTypeExtensions.ToChinese() / ToShort() / ToColoredText() 集中管理
- 所有用户可见提示（法力不足/符能不足/拖拽 toast）统一改用 ToColoredText()
- ToastUI / AskPromptUI 的 Text.supportRichText = true

**等待弹窗超时自动取消**：
- SpellTargetPopup：OnEnable/OnDisable 订阅 GameEventBus.OnClearBanners → CancelSelection
- AskPromptUI：OnEnable/OnDisable 订阅 → OnCancelClicked
- SpellTargetPopup.IsShowing 静态标志：DropFlowRoutine 等待弹窗消失再执行弹回动画，防止装备牌提前弹回
- ReactiveWindowUI：OnClearBanners → AutoPlayRandom（反应牌不可取消，随机出牌）

**Buff/Debuff 状态徽章**：
- UnitInstance.HasBuff / HasDebuff 计算属性
- UnitInstance.BuildBuffSummary() / BuildDebuffSummary()
- CardView：底部 ▲（绿）/ ▼（红）18×18 徽章，右键弹出浮动详情面板
- 详情面板：VLG 排列，点击任意鼠标键关闭

**Codex HIGH 修复**（第二轮审查）：
- H-1：EquipFlyRoutine 设置 _pendingEquipOnDone 字段，GameUI.OnDestroy 调用，防止 tcs2 永久挂起
- H-2：ReactiveWindowUI.AutoPlayRandom 增加 _gs.PHand.Contains(chosen) 守卫，状态变化时降级为 SkipReaction
- H-3：CardView.OnDestroy 销毁 _statusTooltip，防止 canvas root 泄漏

### 技术债新增

无新增（H-1/H-2/H-3 已全部修复）

---

## DEV-25b：规则对齐补丁（迅捷/急速/注释） — 2026-04-01

**Status**: ✅ Completed
**Tests**: 385/385 🟢（MCP EditMode 全绿）

### Codex 审查修复（2026-04-01）

**HIGH #1 已修复 — TryPlayUnitAsync 事后重验证**:
- await Haste 弹窗后新增完整状态重验证（H-5）
- 检查：GameOver / 回合/阶段 / hand membership / PMana / Sch
- useHaste 资源再验证：若 await 期间资源被花掉，静默降级为休眠进场
- 参考模式：PlayHandCardWithRuneConfirmAsync 的 H-4 验证块

**HIGH #2 设计确认 — Swift 时机延迟**:
- Swift 时机（法术对决期间可打出）需要 4 状态回合状态机
- 已在两处 spell-duel 筛选处加 `// DEV-27 TODO` 注释
- 记入 tech-debt，归入 DEV-27

**LOW — 测试覆盖**:
- 记入 tech-debt，待 DEV-27 一起补

### 实现内容

**CardKeyword.cs**:
- 新增 `Swift = 1 << 15`（迅捷，Rule 718：法术对决期间可打出）
- 修正 Haste/Barrier/SpellShield/Inspire/StrongAtk 注释，对齐规则

**SceneBuilder.cs**:
- 6 张法术卡关键词从 `Haste` → `Swift`（hex_ray, void_seek, rally_call, balance_resolve, slam, strike_ask_later）
- 卡面文字从"急速"→"迅捷"

**GameManager.cs**:
- `TryPlayUnit` → `TryPlayUnitAsync`，新增 Haste 付费弹窗
- Rule 717：有 Haste 的单位可选额外支付 [1] mana + [1C] sch 以活跃状态进场
- 通过 AskPromptUI.WaitForConfirm 弹窗让玩家选择

**EntryEffectSystem.cs**:
- `yi_hero_enter` 删除 `unit.Exhausted = false` — 急速付费统一交给出牌流程处理

**CardDetailPopup.cs**:
- 新增 Swift 名称/描述
- 修正 Haste/Barrier/SpellShield/Inspire 描述文案

**Roadmap 整理**:
- 迅捷时机限制、反应闭环权限、待命机制（#3）、结算链（#11/#13）统一归入 DEV-27
- 瞬息 #4 标记为已完成
- DEV-27 新增"回合状态机（4态）"为核心架构目标

---

## DEV-21：粒子特效系统 — 2026-04-01

**Status**: ✅ Completed
**Tests**: 385/385 🟢（MCP EditMode 全绿）

### 实现内容

**ParticleManager.cs（新建）**:
- BG_COUNT=55 背景粒子（金/青/蓝/紫，上浮+正弦漂移）
- RUNE_COUNT=8 古北欧符文字形漂浮（金/青，旋转上浮）
- FIREFLY_COUNT=12 萤火虫粒子（正弦振荡，双色光晕）
- MIST_COUNT=4 底部云雾层（青色，水平漂移）
- LINE_POOL=80 连线粒子池（LINE_RADIUS=90f，旋转 Image 条，pivot=(0,0.5)）
- Update() 驱动全部5类粒子；所有常量 `public const` 供测试验证

**MouseTrail.cs（新建）**:
- TRAIL_LENGTH=18 点拖尾（DOT_MAX_SIZE=8f，TRAIL_HEAD_ALPHA=0.65f）
- RectTransformUtility.ScreenPointToLocalPointInRectangle 鼠标→Canvas坐标转换
- ClickEffectRoutine：涟漪环（20→80px，0.5s）+ 6个金色六边形点外扩（0→38px）

**SpellVFX.cs（新建）**:
- Awake/OnDestroy 订阅/退订：OnCardPlayed / OnUnitDiedAtPos / OnLegendEvolved
- BurstParticles(origin, color, count)：16点径向爆裂，ease-out quad，0.6s，按RuneType着色
- LegendFlame(origin)：20粒子，3s，橙→黄渐变，正弦横向抖动，超高度自动重生
- GetCardBurstColor(UnitInstance)：static，RuneType→Color 映射，null-safe
- 所有 handler 含 `isActiveAndEnabled` + `_vfxLayer != null` 卫语句

**GameEventBus.cs（修改）**:
- 新增 `OnUnitDiedAtPos`（Action\<UnitInstance, Vector2\>）+ `FireUnitDiedAtPos`

**GameUI.cs（修改）**:
- `OnUnitDiedHandler`：死亡单位位置转换 Canvas 本地坐标，调用 FireUnitDiedAtPos
- Camera 统一使用 `rootCanvas.worldCamera`（Codex MEDIUM-3 修复）

**SceneBuilder.cs（修改）**:
- 创建 ParticleBGLayer（Canvas 后、Background 前）、ParticleFGLayer（前景最顶层）
- GameManager GO 添加 ParticleManager / MouseTrail / SpellVFX 组件并连线所有 SerializedField

**DEV21ParticleTests.cs（新建）**:
- 22 项 EditMode 测试：常量值验证 / GetCardBurstColor 全RuneType / OnUnitDiedAtPos 事件 / Subscribe/Unsubscribe / LINE_RADIUS 边界

**Codex 审查修复**:
- MEDIUM-2：SpellVFX 所有 handler 添加 `isActiveAndEnabled` 卫语句（防订阅窗口 race）
- MEDIUM-3：GameUI OnUnitDiedHandler camera 由 null 改为 `rootCanvas.worldCamera`

**Files changed**: `ParticleManager.cs`（新建）、`MouseTrail.cs`（新建）、`SpellVFX.cs`（新建）、`GameEventBus.cs`、`GameUI.cs`、`SceneBuilder.cs`、`DEV21ParticleTests.cs`（新建，22条测试）

---

## DEV-20：符文自动消耗系统 — 2026-04-01

**Status**: ✅ Completed
**Tests**: 363/363 🟢

### 实现内容

**核心逻辑**:
- **RuneAutoConsume.cs（新建）** — `Plan` struct + `Compute()` 静态方法：计算打出某张牌需要自动横置/回收哪些符文（Pass 1 先回收匹配类型满足符能差额，Pass 2 横置剩余符文满足法力差额，CanAfford 标记是否可负担）
- `BuildConfirmText(unit)` — 生成确认对话框文案

**悬停手牌 → 符文高亮**:
- **CardView.cs** — 新增 `IPointerEnterHandler`/`IPointerExitHandler` + `onHoverEnter`/`onHoverExit` 回调参数
- **GameUI.cs** — `SetRuneHighlights(tapIndices, recycleIndices)` / `ClearRuneHighlights()`；`RefreshRuneZone` 中对高亮索引覆盖颜色（蓝=横置，红=回收）；`SetCallbacks` 新增 hover 参数；`RefreshHands` 传递 hover 回调给手牌 CardView
- **GameManager.cs** — `OnCardHoverEnter`/`OnCardHoverExit`：悬停时计算 Plan 并调用 `SetRuneHighlights`，离开时 `ClearRuneHighlights`

**点击出牌确认流程**:
- `PlayHandCardWithRuneConfirmAsync` — 替代 `TryPlayCard` 直接调用：若 Plan.NeedsOps 则显示 AskPromptUI 确认弹窗，await 后重新验证 turn/phase/hand/CanAfford，然后执行 `ExecuteRunePlan`
- `ExecuteRunePlan` — 降序回收（含 `_gs.GetRuneDeck(owner).Add(r)` 归还牌堆），升序横置（offset 补偿已移除项偏移）

**反应按钮符文感知**:
- `OnReactClicked` — 过滤改为 `RuneAutoConsume.Compute().CanAfford`（含横置/回收后可负担）
- 选牌后若需消耗符文，展示符文高亮 + 确认弹窗，确认后 `ExecuteRunePlan`，再打出反应牌
- **ReactiveWindowUI.cs** — `WaitForReaction` 接受可选 `onHoverEnter`/`onHoverExit` 参数，传递给每张卡的 `cv.Setup`；新增 `OnDestroy` TCS 清理

**Codex HIGH 修复**:
- H-1: `ExecuteRunePlan` 回收后将符文 `Add` 到 RuneDeck（之前丢失）
- H-2: `OnRuneClicked` recycle 分支新增 `rune.Tapped` 卫语句（横置与回收互斥）
- H-3: `OnReactClicked` 新增重入守卫（`_reactionWindowActive` 检查）；`ReactiveWindowUI.OnDestroy` 调用 `TrySetCanceled`
- H-4: `PlayHandCardWithRuneConfirmAsync` 每次 await 后重新验证 turn/phase/hand/CanAfford

**Files changed**: `RuneAutoConsume.cs`（新建）、`CardView.cs`、`GameUI.cs`、`GameManager.cs`、`ReactiveWindowUI.cs`、`DEV20RuneAutoConsumeTests.cs`（新建，13条测试）

---

## DEV-19：UI 系统补全（补丁）— 2026-04-01

**Status**: ✅ Completed (patch)
**Tests**: 350/350 🟢

**Notification system fix + Codex HIGH fixes**:

- **ToastUI.cs + EventBanner.cs** — 通知系统三项修复：(1) 相同消息触发时重置驻留计时器，不重新入队（dedup+extend），(2) 回合切换时立即清除所有队列和当前显示（订阅 `GameEventBus.OnClearBanners`），(3) 动画速度加倍（ToastUI: fade-in 0.15s/stay 0.8s/fade-out 0.2s；EventBanner: anim-in 0.1s/anim-out 0.12s）
- **GameManager.cs** — AWAKEN 阶段 Fire `GameEventBus.FireClearBanners()` 清除上一回合所有提示
- **GameUI.cs** — `RefreshRoundPhase` 检测阶段变化，触发 `PhasePulseRoutine`（scale 1→1.18→1，0.4s）
- **ButtonCharge.cs（新建）** — 悬停激活光流效果（1.5s sweep，RectMask2D 裁切），SceneBuilder 为 EndTurn/React/ConfirmRunes 三按钮自动添加
- **SceneBuilder.cs** — 完整连线 AskPromptUI 面板（`CreateAskPromptPanel` + `AddButtonCharge` helper）
- **AskPromptUI.cs（Codex HIGH 修复）** — (H-1) `OnDestroy` 改用 `TrySetCanceled()`，防止 teardown 与用户决策混淆；(H-2) 空列表早返回路径补调 `Hide()`，防止旧面板残留
- **GameManager.cs（Codex HIGH 修复）** — (H-3) 新增 `OnDestroy`，场景卸载时取消静态 `_reactionTcs`/`_aiReactionTcs`，防止 awaiter 跨场景挂起

**Files changed**: `ToastUI.cs`、`EventBanner.cs`、`AskPromptUI.cs`、`GameManager.cs`、`ButtonCharge.cs`（新建）、`GameUI.cs`、`SceneBuilder.cs`

---

## DEV-19：UI 系统补全 — 2026-04-01

**Status**: ✅ Completed
**Tests**: 332/332 🟢

**What was done**:

- **AskPromptUI.cs（新建）** — 通用异步弹窗，支持卡片选择（`WaitForCardChoice`）和确认对话框（`WaitForConfirm`）两种模式，TaskCompletionSource 异步等待，Singleton MonoBehaviour。修复：重入弹窗先 cancel 旧 TCS；OnDestroy 解析所有挂起 Task，防止 awaiter 永久挂起。
- **ScoreManager.cs** — 新增 `OnScoreAdded` 静态事件（owner, newScore），得分成功后 Fire，供 GameUI 触发得分脉冲。
- **GameEventBus.cs** — 新增 `OnDuelBanner` 事件 + `FireDuelBanner()`，法术对决进入反应窗口时触发。
- **GameUI.cs** — DEV-19 动画系统：
  - 得分脉冲（`TriggerScorePulse` → `ScorePulseRoutine` scale 1→1.15→1，1.8s）
  - 得分环扩散（`SpawnScoreRing` → `ScoreRingRoutine` scale 1→2.5 + alpha→0，自动 Destroy）
  - 横幅 slide 动画（`BannerSlideRoutine`：Y -40→0 淡入 0.28s，停留 1.8s，Y +20 淡出 0.22s）
  - 结束按钮常驻脉冲（`EndTurnPulseRoutine`：alpha 1↔0.6，2s 周期，UpdateEndTurnPulse 控制启停）
  - 反应按钮 ribbon 展开（`PlayReactRibbonReveal`：scaleX 0→1 0.25s + 1 脉冲周期）
  - `OnDuelBannerHandler` 订阅 GameEventBus.OnDuelBanner，显示"⚡ 法术对决！"
- **GameManager.cs** — `HandlePhaseChanged` 在 AWAKEN 阶段 ShowBanner 显示"回合 N · 玩家/AI的回合"；ACTION 阶段触发 `PlayReactRibbonReveal`；`CastPlayerSpellWithReactionAsync` Fire `FireDuelBanner()`。
- **SceneBuilder.cs** — 新增 `CreateAskPromptPanel`（全屏遮罩 + 标题/消息/卡片容器/确认取消按钮），AddComponent `AskPromptUI`，完整 SerializedObject 连线，`_askPromptUI` 连线到 GameUI。
- **测试** — 新建 `DEV19UITests.cs`（18 tests）：AskPromptUI 单例/异步行为/重入取消、ScoreManager.OnScoreAdded 触发/跳过、GameEventBus.OnDuelBanner 触发、得分脉冲索引、回合横幅文本、react ribbon 条件、结束键脉冲条件。

**Files changed**: `AskPromptUI.cs`（新建）、`DEV19UITests.cs`（新建）；`ScoreManager.cs`、`GameEventBus.cs`、`GameUI.cs`、`GameManager.cs`、`SceneBuilder.cs`（修改）

---

## DEV-18b：全局事件反馈系统 — 2026-03-31

**Status**: ✅ Completed
**Tests**: 332/332 🟢

**What was done**:

- **GameEventBus.cs（新建）** — 静态事件总线，集中管理所有视觉反馈事件。提供 `OnUnitFloatText`、`OnZoneFloatText`、`OnEventBanner` 三类事件 + 16 个 convenience Fire 方法（ScoreFloat / RuneTap / RuneRecycle / UnitAtkBuff / 各类 banner）。
- **FloatText.cs（新建）** — 池化飘字组件（默认池 12 个）。上浮 60px + ease-out + 淡出，0.9s 自动回池。支持颜色和 large 参数（24/32px 字号）。
- **EventBanner.cs（新建）** — 小横幅组件，CanvasGroup 控制可见性，Queue 防重叠（先进先出）。每条 banner 滑入 0.2s + 停留 + 淡出 0.25s；large=true 时字号 26/金色，普通 20/暖白。
- **GameColors.cs** — 新增 `ScorePulseColor`、`ManaColor`、`SchColor`、`BuffColor`、`DebuffColor` 5 个颜色常量。
- **GameUI.cs** — 新增 4 个 SerializedField zone RT（`_playerScoreZoneRT` 等），Awake/OnDestroy 订阅 GameEventBus，新增 `OnUnitFloatTextHandler`、`OnZoneFloatTextHandler`、`GetZoneRT`、`GetRootCanvas` 4 个处理方法。
- **ScoreManager.cs** — AddScore 成功后 Fire `GameEventBus.FireScoreFloat + FireHoldScoreBanner / FireConquerScoreBanner`。
- **TurnManager.cs** — DoDraw 燃尽路径 Fire `GameEventBus.FireBurnoutBanner`。
- **SpellSystem.cs** — time_warp 分支 Fire `GameEventBus.FireTimeWarpBanner`。
- **DeathwishSystem.cs** — alert_sentinel_die / wailing_poro_die 触发时 Fire `GameEventBus.FireDeathwishBanner`。
- **EntryEffectSystem.cs** — yordel/darius/thousand_tail/rengar/yi_hero 入场 Fire `FireEntryEffectBanner`；darius +2战力 / thousand_tail 逐单位 / inspire 触发时 Fire `FireUnitAtkBuff`。
- **LegendSystem.cs** — 卡莎虚空感知 Fire `FireLegendSkillBanner`；进化 Fire `FireLegendEvolvedBanner`；易大师独影剑鸣 Fire `FireLegendSkillBanner + FireUnitAtkBuff`。
- **GameManager.cs** — OnRuneClicked 横置/回收分支分别 Fire `FireRuneTapFloat / FireRuneRecycleFloat`。
- **SceneBuilder.cs** — 新增 `CreateEventBannerPanel`，在 Canvas 根节点创建 EventBanner GameObject 并连线字段；WireGameUI 补充 4 个 zone RT 连线。
- **测试** — 新建 `DEV18bEventFeedbackTests.cs`（23 tests）：所有 Fire 方法均有订阅回调验证、null 安全测试、颜色正确性检查、无订阅者不抛异常。

**Files changed**: `GameEventBus.cs`、`FloatText.cs`、`EventBanner.cs`（新建）；`GameColors.cs`、`GameUI.cs`、`ScoreManager.cs`、`TurnManager.cs`、`SpellSystem.cs`、`DeathwishSystem.cs`、`EntryEffectSystem.cs`、`LegendSystem.cs`、`GameManager.cs`、`SceneBuilder.cs`（修改）

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

---

## VFX-3：卡牌死亡溶解效果 — 2026-04-04

**Status**: ✅ Completed

**What was done**:
- CardView.cs 新增 `_killDissolveMat`（SerializedField，SceneBuilder 连线）+ `_clonedDissolveMat` 运行时克隆追踪
- `DeathRoutine` Phase A 提取为 `DissolveOrFallbackRoutine`（协程）：
  - 有材质时：克隆 KillDissolveFX.mat → `_cardBg.material = cloned` → `AnimMatFX.SetFloat("noise_fade", 1f, 0.6s)` 驱动溶解 + 子元素同步 alpha 淡出
  - 无材质时：原缩放+红色闪烁（0.3s）fallback
  - 溶解/fallback 完成后 Phase B（ghost 贝塞尔弧线飞向弃牌堆）保持不变
- 动画结束后 `Destroy(gameObject)` 清理卡牌 GO
- `OnDestroy` 新增 `_cardBg.material = null` + `SafeDestroy(_clonedDissolveMat)` 防止协程中断时悬空引用（Codex HIGH-1 修复）
- SceneBuilder.CreateCardPrefab() 连线 KillDissolveFX.mat → `_killDissolveMat`
- VFX3DissolveTests.cs 新建（10 项测试：AnimMatFX API、材质属性、CardView 字段、fallback 安全）

**Decisions made**:
- dissolve 路径只对 `_cardBg` 应用着色器，其余子元素同步 alpha 淡出（非 CanvasGroup，避免遮挡着色器效果）
- `dissolveDone` bool + 手动 while 循环代替 `WaitUntil`，配合 timeout 兜底防止协程永久挂起

**Technical debt**:
- MEDIUM: AnimMatFX.Create 复用组件存在竞态隐患（当前路径安全）
- MEDIUM: fallback 路径红色叠加是累积而非从原始值插值（低帧率轻微视觉偏差）
- LOW: dissolve 路径 Phase B ghost 大小仍为 0.6x（dissolve 不缩放卡牌，轻微不一致）

**Problems encountered**:
- `float elapsed` 变量提取后 Phase B 丢失声明 → 补加 `float elapsed = 0f`
- 测试用 `SendMessage("Update")` 触发 Unity `ShouldRunBehaviour()` 断言 → 改为直接测试 API 调用

**Tests**: 492/492 EditMode 全绿（含 VFX3DissolveTests 10项）
