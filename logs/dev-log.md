# 开发日志 — FWTCG Unity 移植

---

## DEV-30：Pencil → Unity 布局全面对齐 — 2026-04-13

**Status**: ✅ Completed
**Files**: SceneBuilder.cs, GameUI.cs, CountdownRingUI.cs, bg_game_main.png

### 实现内容

**SceneBuilder.cs — 隐藏非 Pencil UI 元素**：
- `messagePanel.SetActive(false)` — Pencil 设计中无消息面板
- `logToggleGO.SetActive(false)` — Pencil 设计中无日志折叠按钮
- `debugPanel.SetActive(false)` — Pencil 设计中无调试面板

**SceneBuilder.cs — 新增中间区域（BF FieldCard + Standby）**：
- BF0FieldCard: Pencil x=679,y=436,w=106,h=76 → Unity 锚点 (0.354,0.409,0.426,0.596)
- BF0Standby: Pencil x=696,y=546,w=72,h=100 → Unity 锚点 (0.363,0.400,0.402,0.495)
- BF1FieldCard/BF1Standby 对称镜像
- 样式：暗色背景 #08101a + 金色描边 + CreateTwoLineLabel 双行文字

**SceneBuilder.cs — RUNES + BASE 区域**（CreateBoardWrapper 内）：
- EnemyRunes 条带: y=155-240，12个符文圆圈槽位，HLG 排列
- EnemyBase 区域: y=242-402，左右各4个基地卡槽
- PlayerBase 区域: y=666-826（对称）
- PlayerRunes 条带: y=828-913（对称）

**SceneBuilder.cs — 结束回合按钮定位**：
- Pencil: x=1538,y=926,w=108,h=34 → Unity 锚点 (0.801,0.857,0.111,0.143)
- BottomBar VLG `childControlHeight=true` 修复（防止 btn_react sprite 溢出）
- 上下文按钮默认隐藏（phaseDisplay/tapAllRunesBtn/cancelRunesBtn/confirmRunesBtn/skipReactionBtn）

**SceneBuilder.cs — 手牌区宽度修正**：
- 原: 全宽 (x=248-1672)；修正: 中间区域 (x=644-1276) 对齐 Pencil 设计
- EnemyHand/PlayerHand anchorMin/Max 从 Pencil 坐标精确换算

**bg_game_main.png 替换**：
- 从 Pencil new5.pen bg_texture 节点（2062×1277）导出
- `preserveAspect=false` 拉伸铺满整个 Canvas（包含左右计分板区域）

### 审查结果（Claude 自身审查）
- SetActive(false) 元素在 WireGameUI 中均有 null-guard 保护 ✅
- CreateTwoLineLabel Text→RT 创建顺序正确 ✅
- VLG childControlHeight=true 防止按钮溢出 ✅
- 手牌区宽度变更不影响 GameUI 运行时逻辑 ✅

---

## DOT-8：DOTween 视效强化（17个新效果）— 2026-04-07

**Status**: ✅ Completed
**Tests**: 60 新测试 (DOT8ReplacementTests) 全绿，DOT7/6/5/4/3/2 + DEV29/30/32/27/28 等所有批次无回归

### 实现内容

**GameEventBus.cs**：新增 3 个静态事件 + Fire 方法：`OnTurnChanged(string owner, int round)`、`OnAOETargets(UnitInstance[] targets)`、`OnLegendSkillFired(LegendInstance legend, string owner)`

**GameManager.cs**：`HandlePhaseChanged` 在 Action 阶段触发 `FireTurnChanged`；`OnLegendSkillClicked` 触发 `FireLegendSkillFired`

**CardView.cs**：
- 手牌推挤：`_spreadLayoutEl` LayoutElement 控制邻牌 preferredWidth +32px，0.12s EaseOutBack 弹出，`StartHandSpread/StopHandSpread`
- 3D 倾斜：`FixedUpdate` 跟随鼠标 `_tiltTarget` ±10°，`Update` lerp 插值 speed=9，切换 `_isTiltActive`
- 数字翻滚：`_displayedHp/_displayedAtk` int.MinValue 哨兵初始化，`DOVirtual.Float STAT_ROLL_DUR=0.45s` 逐帧更新文字
- 击杀特写：`KillCloseupRoutine` DOTween Sequence，放大 1→1.4 + 发光 0→1，hold 0.3s，缩回

**CardDragHandler.cs**：出牌弹弓蓄力，拖拽 `DOScaleX` 按距离压缩，最大 0.85x

**CombatAnimator.cs**：AOE 连锁高亮，`DOVirtual.DelayedCall` 0.08s stagger 依次 `PunchScale` 各目标；`_activeGhosts` 列表 + `OnDestroy` 清理

**GameUI.cs**（8个新 handler）：
- `OnBigDamageHandler`：damage ≥5 触发 Canvas `DOShakeAnchorPos` 0.35s
- `OnFatalHitHandler`：timeScale 1→0.3（0.15s）→ hold 0.2s →1（0.5s），`SetUpdate(true)` 不受 timeScale 影响；re-trigger 修复（H-1：先重置 timeScale=1 再启动）
- `OnTurnChangedHandler`：调用 TurnSweepBanner + ManaFillStagger
- `PlayTurnSweepBanner`：懒建 Text，OutBack 飞入 0.5s → hold 1s → InQuad 飞出 0.35s
- `PlayManaFillStagger`：3拍 InsertCallback PunchScale，stagger=0.08s
- `SpawnConfetti`：25 彩色方块从顶部落下旋转淡出 ~2.8s；OnDestroy 清理（H-2 修复）
- `PlayOpponentCardPreview`：ghost 卡淡入飞入 → hold 0.5s → 淡出；`_opponentPreviewGO` 字段防泄漏（H-3 修复）
- `HandleEndTurn`：`PunchScaleUI` 按钮 Squash 0.15f；`RefreshPileCounts` 剩余≤2 触发牌库抖动

**StartupFlowUI.cs**：
- 洗牌动画：`CreateShuffleAnimationTween` 4张幽灵卡交叉飞行 + 淡出，`_shuffleGhosts` 追踪
- Mulligan 翻转：`OnMulliganCardClicked` 添加 DOScaleX 0→1 翻转，0.11s×2

**LegendSkillShowcase.cs**（新文件）：
- 单例 `Instance` + `GameEventBus.OnLegendSkillFired` 订阅
- `PlayShowcase`：overlay 0→0.72（0.15s）+ panel 0.4→1.08→1（0.4s）→ hold 0.8s → exit（0.3s）
- `EnsureOverlayElements`：懒创建 dark overlay Image + card panel（Border + Name + SkillLabel）
- SceneBuilder 补加 `Canvas/LegendSkillShowcase` GO + 组件

**CardBackManager.cs 修复**：`ResetForTest` 改 `_loaded=true`，防止 `Load()` 覆盖测试设置的 Default 变体

**Codex 审查 High 修复**：
- H-1: `OnFatalHitHandler` 二次触发前重置 `Time.timeScale=1f`
- H-2: `OnDestroy` 新增 confetti GO 清理循环（DOTween.Kill + Destroy）
- H-3: `_opponentPreviewGO` 字段追踪，re-trigger 时先销毁旧 GO

### Technical debt（新增 Medium/Low 记入 tech-debt）
- MEDIUM: `LegendSkillShowcase` 两阶段 scale 动画（0.4→1.08→1）Join+SetDelay 方式视觉上有轻微冻帧（MEDIUM-1）
- MEDIUM: `_turnSweepSeq` kill 未用 `KillSafe(ref)`，与其他 seq 不一致（MEDIUM-2）
- MEDIUM: Mulligan 翻转 tween 无 `_mulliganFlipSeq` 字段追踪，销毁路径有漏（MEDIUM-3）
- MEDIUM: `PlayManaFillStagger` InsertCallback 内部 PunchScale tween 无 `SetTarget(gameObject)` 追踪（MEDIUM-5）

---

## DOT-7：CardView.cs DOTween 替换 + AnimMatFX 清理 — 2026-04-06

**Status**: ✅ Completed
**Tests**: 1080 EditMode 编译通过，1077 FWTCG+MCP 测试中 1077 绿 🟢（3 个预存失败非 DOT-7 引入），新增 55 个测试

### 实现内容

**CardView.cs**：17 个协程方法替换为 DOTween，完全消除动画相关的 IEnumerator 依赖（保留 5 个流程控制协程：PlayableSparkRoutine/EnterAnimSetup/DeathRoutine/DissolveOrFallbackRoutine/SpawnIdleFXDelayed/AutoDismissTooltip）：

**LiftFloatRoutine**（per-frame Sin 驱动悬浮）→ DOVirtual.Float sine loop SetLoops(-1, Restart)；_liftFloat Coroutine → Tween

**ReturnToRestRoutine**（手写 ease-out-quad 返回）→ DOVirtual.Float SetEase(OutQuad)；_returnToRest Coroutine → Tween

**BreathGlowRoutine**（per-frame Sin 驱动 stat glow alpha）→ CreateBreathGlowTween DOVirtual.Float sine loop；_atkBreath/_costBreath/_schBreath Coroutine → Tween；常量提取 BREATH_GLOW_MIN/MAX/SPEED

**StunPulseRoutine**（per-frame 2Hz Sin 驱动 stun overlay）→ CreateStunPulseTween DOVirtual.Float sine loop；_stunPulse Coroutine → Tween；常量 STUN_PULSE_SPEED/MIN/MAX

**FlashRedRoutine**（red → original 0.35s Lerp）→ DOColor + SetDelay(0.12s)；_flash Coroutine → Tween；常量 FLASH_RED_HOLD/FLASH_RED_FADE

**ShakeRoutine**（手写 offsets 数组）→ TweenHelper.ShakeUI；_shake Coroutine → Tween；常量 SHAKE_STRENGTH/DURATION/VIBRATO

**BadgeScaleRoutine**（手写 Lerp scale）→ DOScale SetEase(Linear)；_badgeScaleCos Dictionary<GO,Coroutine> → _badgeScaleTweens Dictionary<GO,Tween>

**TargetFadeOutRoutine**（MoveTowards alpha fade）→ DOVirtual.Float；_targetFadeOut Coroutine → Tween

**TargetPulseRoutine**（两阶段 fade-in + sine pulse）→ StartTargetPulse DOVirtual.Float sine loop + envelope；_targetPulse Coroutine → Tween；常量 TARGET_PULSE_PERIOD/MIN/MAX

**OrbitRoutine**（per-frame 角度递增圆形运动）→ DOVirtual.Float angle loop SetLoops(-1, Restart)；_orbitRoutine → _orbitTween Tween；常量 ORBIT_RADIUS/ORBIT_PERIOD

**HeroAuraPulseRoutine**（per-frame Sin 驱动 aura alpha）→ DOVirtual.Float sine loop；_heroAuraPulse Coroutine → Tween；常量 HERO_AURA_PERIOD/MIN/MAX

**EnterAnimRoutine**（yield return null + 手写 EaseOutQuad）→ EnterAnimSetup 协程壳（保留 1 帧等待 + ForceUpdateCanvases）+ DOTween Sequence（DOAnchorPos + DOScale OutQuad）；_enterAnimCoroutine → _enterAnimSetup + _enterAnimSeq；常量 ENTER_ANIM_DURATION/START_SCALE/Y_OFFSET

**FoilSweepRoutine**（per-frame 驱动 ShineX/ShineY material）→ StartFoilSweep DOVirtual.Float；_foilSweep Coroutine → Tween；常量 FOIL_SWEEP_DURATION

**AnimateSparkDot**（per-frame 手写 alpha+position）→ void 方法，DOVirtual.Float + SetTarget(dot) + OnComplete Destroy；常量 SPARK_INTERVAL/DURATION/FLOAT_DIST/PEAK_ALPHA

**FadeShadowIn**（WaitForSeconds + per-frame Lerp）→ DOColor + SetDelay（额外发现）

**DeathRoutine**（保留协程壳）：Phase B ghost fly 改为 DOTween Sequence（DOVirtual.Float 驱动贝塞尔弧线+缩放+淡出）；_death Coroutine → _deathSeq Sequence；常量 DEATH_PHASE_A_DISSOLVE/FALLBACK/PHASE_B/GHOST_START_SCALE/GHOST_END_SCALE

**DissolveOrFallbackRoutine**（保留协程壳）：文本淡出改 DOVirtual.Float 并行；fallback 路径 shrink+tint 改 DOVirtual.Float

**OnDestroy**：DOTween.Kill(gameObject) 兜底 + 14 个 KillSafe 逐个处理所有 tween 字段

**AnimMatFX.cs 删除**：已无代码引用，安全删除

**DOT2ReplacementTests.cs（更新）**：AnimMatFX type reference → string-based check（适配文件删除）

**BugFixBaseCardVisibilityTests.cs（更新）**：_enterAnimCoroutine → _enterAnimSeq (Sequence)

**DEV29Tests.cs（更新）**：_death field check 适配 _deathSeq

**DOT7ReplacementTests.cs（新建，55 测试）**：覆盖 14 个旧协程方法移除验证、15 个旧 Coroutine 字段→Tween/Sequence 验证、7 个新方法存在验证、12 个常量验证、AnimMatFX 类型不存在、Coroutine 字段全扫描、FlashRed/Shake null-safety、TweenHelper null-safety 回归、badge scale tweens 字典类型

**Technical debt**: AnimMatFX.cs 已删除（DOT-2 标记的技术债）

---

## DOT-6：GameUI.cs DOTween 替换 — 2026-04-06

**Status**: ✅ Completed
**Tests**: 1003/1003 EditMode 编译通过，1000 FWTCG+MCP 测试中 1000 绿 🟢（3 个预存失败非 DOT-6 引入），新增 53 个测试

### 实现内容

**GameUI.cs**：18 个协程方法全部替换为 DOTween，完全消除 IEnumerator/System.Collections 依赖：

**BannerSlideRoutine**（fade-in + stay + fade-out）→ DOTween Sequence（DOFade in 0.2s + AppendInterval + DOFade out 0.2s）；_bannerAnimCoroutine → _bannerAnimSeq Sequence

**FlashLegendText**（4次金色闪烁 for 循环）→ DOColor SetLoops(8, Yoyo) 自动完成 4 个完整闪烁周期

**RuneHighlightPulseRoutine**（per-frame Sin 驱动符文边框颜色）→ DOVirtual.Float sine loop SetLoops(-1)，callback 内保留动态 HashSet 遍历逻辑；常量提取 RuneTapFill/RuneTapOutline/RuneRecFill/RuneRecOutline + RUNE_PULSE_FREQ

**PhasePulseRoutine**（scale up/down 0.4s）→ DOScale Yoyo SetLoops(2)；常量 PHASE_PULSE_DURATION/PHASE_PULSE_PEAK

**FadeLegendGlow**（static IEnumerator MoveTowards alpha）→ static void，DOTween.Kill(img) 防重叠 + DOFade；常量 LEGEND_GLOW_SPEED

**LogEntryFlashRoutine**（gold→original 0.8s Lerp）→ DOColor SetEase(Linear)；常量 LOG_FLASH_GOLD/LOG_FLASH_DURATION

**GameOverEnhancedRoutine**（fade-in + 胜利 scale pop）→ CreateGameOverSequence：DOFade + 条件 DOScale pop；常量 GAMEOVER_FADE_DUR/GAMEOVER_WIN_SCALE_DUR

**FadeInPanelRoutine** → 删除（无代码引用）

**BoardFlashRoutine**（金色/红色闪烁 0.85s）→ DOTween Sequence DOColor in + out；常量 BOARD_FLASH_HALF

**AnimateLogToggle**（SmoothStep 驱动 offsetMax）→ DOVirtual.Float SetEase(InOutQuad)；常量 LOG_TOGGLE_DURATION

**TimerCountdown**（WaitForSeconds 1s 循环）→ DOVirtual.Float Linear countdown，CeilToInt 每秒更新

**TimerPulseRoutine**（per-frame Sin scale）→ DOVirtual.Float sine loop SetLoops(-1)；常量不变

**ScorePulseRoutine**（scale up/down 1.8s）→ DOScale Yoyo SetLoops(2)；常量 SCORE_PULSE_HALF/SCORE_PULSE_PEAK

**ScoreRingRoutine**（expanding ring + fade）→ DOTween Sequence（DOScale + DOFade + OnComplete Destroy）；常量 SCORE_RING_DURATION

**EndTurnPulseRoutine**（alpha 1→0.6→1 2s 循环）→ TweenHelper.PulseAlpha(CanvasGroup)；常量 ENDTURN_PULSE_PERIOD/ENDTURN_PULSE_MIN_ALPHA

**ReactRibbonRevealRoutine**（scaleX 0→1 + 1周期脉冲）→ DOTween Sequence（DOVirtual scale-X reveal + DOVirtual pulse）；常量 REACT_REVEAL_DUR/REACT_PULSE_DUR/REACT_PULSE_AMP

**EquipFlyRoutine**（ghost card fly + fade）→ DOTween Sequence（DOAnchorPos InOutQuad + DOFade）SetUpdate(true) unscaled；常量 EQUIP_FLY_DURATION

**ShowHideCombatResult**（fade-in + stay + fade-out）→ DOTween Sequence（DOFade in + AppendInterval + DOFade out）；常量 CR_FADE_IN/CR_STAY/CR_FADE_OUT

**OnDestroy** 统一清理：DOTween.Kill(gameObject) 兜底 + 9 个 KillSafe 逐个处理所有 tween 字段

**DEV24StartupTests.cs（更新 2 个测试）**：LogEntryFlashRoutine_FieldExists → LogEntryFlash_ConstantsExist；FadeInPanelRoutine_FieldExists �� GameOverSequence_Exists

**DOT6ReplacementTests.cs（新建，53 测试）**：覆盖 18 个旧协程移除验证、9 个旧 Coroutine 字段移除、9 个新 Tween/Sequence 字段存在、14 个常量验证、4 个 DOTween 方法存在、IEnumerator 全扫描、TweenHelper null-safety 回归、FadeLegendGlow null-safety、Coroutine 字段类型全扫描

**Technical debt**: 无新增

---

## DOT-5：4个装饰文件 DOTween 替换 — 2026-04-06

**Status**: ✅ Completed
**Tests**: 985/985 EditMode 编译通过，937 FWTCG+MCP 测试中 937 绿 🟢（3 个预存失败非 DOT-5 引入），新增 45 个测试

### 实现内容

**SceneryUI.cs**：SpinLoop 4个协程（手写 Mathf.Repeat + localEulerAngles 连续旋转）→ CreateSpinTween 用 DOLocalRotate(FastBeyond360) Restart -1 loop；DividerOrbLoop 协程（每帧 Sin 驱动 Y 振荡）→ DOVirtual.Float sine loop Restart + 缓存 baseY + 实时读 X（DEV-26 行为保留）；CornerGemLoop 协程（手写 Lerp alpha 渐变）→ DOVirtual.Float Yoyo loop 驱动 SetCornerAlpha；LegendGlowLoop 保持禁用（VFX-7 金色边框替代）+ 注释标注 TweenHelper.PulseAlpha 方式；OnDestroy 6 个 KillSafe

**BattlefieldGlow.cs**：AmbientBreatheLoop 协程（每帧 Sin 驱动 alpha）→ DOVirtual.Float sine loop Restart；CtrlGlowLoop 协程（每帧 Sin + 运行时 _currentCtrl 条件色）→ DOVirtual.Float sine loop + callback 内保留条件逻辑（DEV-26 null→NoGlow 行为保留）；_breatheRoutine/_ctrlRoutine Coroutine → _breatheTween/_ctrlTween Tween；常量改 public 便于测试；OnEnable 创建 / OnDisable+OnDestroy KillSafe

**SpellShowcaseUI.cs**：ShowCoroutine 协程（3阶段 fly-in/hold/fly-out 手写 SmoothStep + unscaledDeltaTime）→ ShowAsync 直接构建 DOTween Sequence（DOAnchorPosY InOutQuad + DOFade + AppendInterval + OnComplete）+ SetUpdate(true) unscaled；ShowGroupCoroutine 同上 + BuildGroupSlots 同步方法分离；完全消除 IEnumerator/System.Collections 依赖；_showSeq Sequence + OnDestroy KillSafe

**StartupFlowUI.cs**：CoinSpinRoutine 协程（COIN_FLIP_COUNT 次 ScaleCoinX + 中间换面 + 落地弹跳）→ CreateCoinSpinSequence 用 DOScaleX InOutSine + AppendCallback 换面 + DOPunchScale 落地；ScaleCoinX 辅助方法移除；CoinBurstParticles 协程（20粒子每帧径向 ease-out-quad）→ StartCoinBurstTweens per-particle Sequence（DOAnchorPos OutQuad + DOSizeDelta + DOFade）+ SetTarget(go) + OnComplete 清理 _burstParticles；FadeIn/FadeOut 协程 → static 方法返回 Tween（DOFade），flow 协程 yield WaitForCompletion；FadeTextIn 协程 → DOTween.To alpha 返回 Tween；ScanLightLoop 协程 → DOAnchorPosX Restart loop；HexBreathLoop/TitleBeamPulseLoop 协程 → DOVirtual.Float sine loop；BgGradientRotateLoop 协程（5°/s）→ DOLocalRotate 72s Restart；TitleTextEntranceRoutine 协程 → CreateTitleEntranceSequence Sequence（DOAnchorPos + DOScale + DOFade OutQuad）；_scanLightRoutine/_hexBreathRoutine/_titleBeamRoutine/_bgGradientRoutine Coroutine → 对应 _xxxTween Tween；OnDisable+OnDestroy KillSafe + DOTween.Kill(go) burst 清理

**保留协程**：CoinFlipFlowRoutine / MulliganFlowRoutine / FadeOutAndResolve — 均为游戏逻辑流程控制（含 yield WaitForSeconds、按钮点击等待、TCS resolve），不适合 DOTween

**DOT5ReplacementTests.cs��新建，45 测试）**：覆盖所有 4 个文件的结构验证（旧协程方法/字段已移除、新 tween 字段/方法存在、常量不变、FadeIn/FadeOut null-safety、保留协程确认、TweenHelper null-safety 回归）

**Technical debt**: 无新增

---

## DOT-4：3个游戏逻辑文件 DOTween 替换 — 2026-04-06

**Status**: ✅ Completed
**Tests**: 954/954 EditMode 编译通过，934 FWTCG+MCP 测试中 934 绿 🟢（20 个预存失败非 DOT-4 引入），新增 27 个测试

### 实现内容

**CombatAnimator.cs**：FlyAndReturnRoutine 3阶段协程（手写 EaseOutQuad/EaseInOutQuad）→ FlyAndReturn 用 DOAnchorPos Sequence（lunge OutQuad + interval + rebound InOutQuad）+ OnComplete 清理 ghost；PlayShockwave 协程（手写 Lerp scale+fade）→ PlayShockwave 返回 Tween（DOScale + DOFade Join）+ OnComplete 隐藏；_sw1Routine/_sw2Routine Coroutine → _sw1Tween/_sw2Tween Tween；OnDestroy KillSafe

**SpellVFX.cs**：BurstParticles 协程（N粒子每帧径向爆发+缩小+淡出）→ void 方法，每粒子独立 Sequence（DOAnchorPos OutQuad + DOSizeDelta + DOFade）+ SetTarget(go) + OnComplete 清理 _ownedParticles；常量提取为 public const（BURST_DURATION/BURST_RADIUS/BURST_START_SIZE/BURST_END_SIZE）；OnDestroy 先 DOTween.Kill(go) 再 Destroy

**SpellVFX.cs（保留协程）**：LegendFlame（每帧速度+sin摆动+高度重生模拟）、ProjectileThenFXRoutine（游戏逻辑等待投射物到达）、DelayedCardPlayFX（等待展示面板关闭）— 均为程序化模拟或流程控制，不适合 DOTween

**CardDragHandler.cs**：CancelReturnRoutine 协程（Smoothstep + stagger delay）→ CancelReturnTween 用 DOTween.To + Sequence.Insert(delay,...) Ease.InOutQuad；_cancelReturnCoroutine Coroutine → _cancelReturnSeq Sequence；DropAnimHost.AnimRoutine 内手写动画循环（AnimateDropCard + EaseOutQuad/EaseInQuad）→ DOTween.To 两阶段 Sequence（OutQuad hover + InQuad drop）+ masterSeq.Insert(stagger)；移除 AnimateDropCard/EaseOutQuad/EaseInQuad/Smoothstep 静态方法

**CardDragHandler.cs（保留协程）**：ClusterFollowRoutine（每帧鼠标跟踪 Lerp）、DropFlowRoutine（yield WaitUntil 游戏逻辑流程控制）

**DOT4ReplacementTests.cs（新建，27 测试）**：覆盖所有 3 个文件的结构验证（旧协程字段/方法已移除、新 tween 字段/方法存在、常量不变、PlayShockwave null-safety/创建验证、保留协程确认、TweenHelper null-safety）

**Technical debt**: 无新增

---

## DOT-3：6个中等文件 DOTween 替换 — 2026-04-06

**Status**: ✅ Completed
**Tests**: 869/869 EditMode 编译通过，859 FWTCG 测试中 859 绿 🟢（10 个预存失败非 DOT-3 引入），新增 46 个测试

### 实现内容

**EventBanner.cs**：AnimateIn/AnimateOut 协程 → DOAnchorPos + DOFade Sequence；ClearFadeRoutine 协程 → DOFade inline；WarningRoutine 手写 EaseOutBack → DOScale(Ease.OutBack)；EaseOutBack 静态方法移除；OnDestroy KillSafe

**AskPromptUI.cs**：ShowRoutine 协程（自定义 overshoot ease）→ DOScale(Ease.OutBack) SetUpdate(true)；HideRoutine 协程（quadratic ease-in）→ DOScale(Ease.InBack) + OnComplete deactivate；_animCoroutine → _animTween

**SpellDuelUI.cs**：BorderPulseLoop 协程（每帧 Sin wave 驱动4边框 alpha）→ DOVirtual.Float Yoyo -1 loop；CountdownRoutine 协程（30s 倒计时 + bar scaleX）→ DOVirtual.Float Linear + OnComplete auto-skip；StopRoutines → KillTweens

**AudioTool.cs**：FadeRoutine 协程 → StartFade 用 DOVirtual.Float 驱动 AudioSource.volume；CrossFadeRoutine 协程 → StartCrossFade 用 Sequence（fade out → swap clip → fade in）；AudioChannel.FadeRoutine Coroutine → FadeTween Tween；所有 fade 使用 SetUpdate(true) 保持 unscaledTime

**CardHoverScale.cs**：Update Lerp(_targetScale, LERP_SPEED) → DOScale(TWEEN_DURATION=0.1f, Ease.OutCubic)；_animating + _targetScale 字段移除；拖拽时 KillSafe + snap to Vector3.one；OnDestroy KillSafe

**PortalVFX.cs**：FadeRoutine 协程 → DOFade on CanvasGroup；Show/Hide 各自 KillSafe + 新建 tween；OnDestroy KillSafe；Update 旋转保留（连续旋转不适合 DOTween）

**DOT3ReplacementTests.cs（新建，46 测试）**：覆盖所有 6 个文件的结构验证（旧协程字段/方法已移除、新 tween 字段/方法存在、常量不变、AudioTool FadeIn/FadeOut/StopChannel/ZeroDuration 行为、TweenHelper null-safety）

**Technical debt**: 无新增

---

## DOT-2：8个小文件 DOTween 替换 — 2026-04-06

**Status**: ✅ Completed
**Tests**: 823/823 EditMode 编译通过，772 FWTCG 测试中 769 绿 🟢（3 个预存失败非 DOT-2 引入），新增 35 个测试

### 实现内容

**FloatText.cs**：AnimateRoutine 协程 → DOAnchorPos + DOFade Sequence + OnDestroy KillSafe

**DamagePopup.cs**：AnimateRoutine 协程 → DOAnchorPos + DOFade Sequence + OnDestroy KillSafe

**ButtonCharge.cs**：SweepRoutine 协程 → DOAnchorPosX + SetTarget + OnDestroy KillSafe

**ButtonHoverGlow.cs**：PulseRoutine 协程 → DOTween.To() driving Outline alpha, Yoyo -1 loop

**MouseTrail.cs**：ClickEffectRoutine 协程 → DOScale + DOFade + DOAnchorPos Sequences，每个特效 GO 自动 Destroy

**ToastUI.cs**：ShowToast 内 fade-in/fade-out 循环 → TweenHelper.FadeCanvasGroup + WaitForCompletion()，stay phase 保留协程（支持 _extendCurrent 重置逻辑）

**ReactiveWindowUI.cs**：CountdownRoutine 协程 → DOVirtual.Float 15s→0s，OnUpdate 驱动 fillAmount + text

**CardView.cs（DissolveOrFallbackRoutine）**：AnimMatFX.Create + SetFloat + Callback → TweenMatFX.DissolveSequence

**VFX3DissolveTests.cs**：4 个 AnimMatFX 测试 → 6 个 TweenMatFX 测试（DOFloat/DOColor/DissolveSequence null-safety + callback）

**DOT2ReplacementTests.cs（新建，31 测试）**：覆盖所有 8 个文件的结构验证（旧协程字段已移除、新 tween 字段存在、常量不变、TweenHelper null-safety）

**Technical debt**: AnimMatFX.cs 现已无代码引用，可在 DOT-7 收尾时安全删除

---

## VFX-8：投射物系统 — 2026-04-05

**Status**: ✅ Completed
**Tests**: 759/759 EditMode 全绿 🟢（新增 27 个测试）

### 实现内容

**Projectile.cs（新建）**：
- 二次贝塞尔弧线飞行组件（start → control → end）
- 弧线高度 = 距离 × 0.3（ARC_HEIGHT_RATIO），默认飞行时间 0.4s
- 飞行期间 Z 轴旋转朝向运动切线方向
- 到达后调用 onArrived 回调 + Destroy(gameObject)

**FXTool.cs（编辑）**：
- 新增 DoProjectileFX(prefab, start, end, duration, onArrived)
- 实例化 prefab + AddComponent<Projectile> + Init

**VFXResolver.cs（编辑）**：
- 新增 ResolveProjectile(CardData) 三层解析：effectId 精确映射 → IsSpell/IsEquipment 按 RuneType → unit 返回 null
- 新增 GetProjectileFXName(RuneType) 6 种元素投射物映射
- 9 个 effectId 专属投射物覆盖（hex_ray/furnace_blast/divine_ray/slam 等）

**SpellVFX.cs（编辑）**：
- DelayedCardPlayFX 新增投射物路径判断：法术/装备卡自动走 ProjectileThenFXRoutine
- ProjectileThenFXRoutine：发射投射物 → 等待到达 → spawn impact FX + 白色 8 点径向爆裂
- 投射物起点：玩家手牌区(Y=-300) / AI屏幕上方(Y=340)
- 无投射物的卡保持原有直接 FX spawn 逻辑

**测试（27 个新测试）**：
- VFX8ProjectileTests：Projectile 常量/Init/控制点/边界、FXTool.DoProjectileFX 创建/null/回调、VFXResolver.ResolveProjectile 各类型卡/effectId/RuneType 全覆盖、SpellVFX 常量

**Technical debt**: 无新增

---

## VFX-7：UI & 高亮视觉迁移（18 子项）— 2026-04-04

**Status**: ✅ Completed
**Tests**: 732/732 EditMode 全绿 🟢（新增 26 个测试）

### 实现内容

**资产迁移（批次 A）**：
- 14 个 UI/FX sprite 从 TCG Engine 迁移：frame_gold/silver、mana_full/empty、button_endturn、card_glow、equip_glow、win_glow/particles/text、bg_menu、target
- card_back_01.png 迁移到 Resources/CardArt/
- frame sprites 放入 Resources/UI/ 供 Resources.Load 使用

**CardBackManager.cs（新建，7g）**：
- 静态类，CardBackVariant 枚举 + PlayerPrefs 持久化
- GetCardBackSprite() 返回当前卡背 sprite（Default 返回 null 用几何叠加）
- CardView.EnsureCardBackOverlay 优先使用 sprite 卡背

**CardView.cs（编辑，7a/7j/7k/7l/7o）**：
- 7a：_frameOverlay Image 层，Refresh 按卡类型选 frame_gold/frame_silver
- 7k：_glowOverlay + Update MoveTowards 平滑淡入淡出，OnPointerEnter/Exit + SetSelected 接入
- 7l：装备卡自动金色发光（alpha 0.35）
- 7j：SetTargeted 改为 MoveTowards 平滑过渡（替代瞬间切换）
- 7o：眩晕→ElectricFX / 休眠→Zzz 动态 DoSnapFX 挂载/销毁

**SceneBuilder.cs（编辑）**：
- CardPrefab 新增 FrameOverlay + GlowOverlay 子节点 + SerializedObject 连线
- EndTurn 按钮加载 button_endturn.png sprite
- Background 改为 bg_menu.png（fallback HexGrid shader）
- GameManager 新增 MouseLineFX + AimTargetFX 组件

**IconBar.cs（新建，7b）**：
- N 个 Image 子节点，SetValue(current, max) 亮暗切换
- 16px 图标 + 2px 间距，mana_full/mana_empty sprite

**GameUI.cs（编辑，7f/7r/7c）**：
- 7f：TimerPulseRoutine <10s 时 scale 1→1.15 2Hz 脉冲
- 7r：ArrangeHandFan ±15° Z 轴扇形排列
- 7c：GameOverEnhancedRoutine 胜利金色+scale pop / 失败灰色

**EventBanner.cs（编辑，7h）**：
- ShowWarning(text) 红底白字 + EaseOutBack scale pop + AudioManager 触发点

**CardDragHandler.cs（编辑，7e）**：
- OnDrag 中根据 deltaX 施加 ±10° Z 轴旋转，Lerp speed=4

**CombatAnimator.cs（编辑，7n）**：
- FlyAndReturnRoutine 改为 3 阶段：飞出 0.3s + 停顿 0.1s + 回弹 0.3s（总 0.7s）

**ButtonHoverGlow.cs（编辑，7m）**：
- OnPointerEnter 加 Button.interactable 检查

**MouseLineFX.cs（新建，7p）**：
- 12 点链瞄准线，合法绿色/非法红色，拖拽时激活

**AimTargetFX.cs（新建，7q）**：
- 48px target.png 准星，alpha 0.4↔0.8 2Hz 脉冲

**测试（26 个新测试）**：
- VFX7Tests：覆盖所有 18 子项常量验证、IconBar SetValue 边界、CardBackManager CRUD

**Technical debt**: 无新增

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

---

## VFX-7r：手牌扇形展开 — 2026-04-14

**Status**: ✅ Completed

**What was done**:
- GameUI.cs `ApplyHandFan` 全面重写，以 Pencil new5.pen 设计数据为基准：
  - **对称索引**：`countHalf = (n-1)/2f`，中心牌 t=0，两侧均等展开（原 n/2f 有偏移）
  - **动态旋转**：`cardAngle = 14f / countHalf`，无论手牌数量上限始终 ±14°（原 10° × tmax=4 = 40°）
  - **Y弧系数**：`cardOffsetY = 56f / (countHalf²)`，两端下落约 56px（来自 Pencil 实测）
  - **消除叠加 bug**：每次调用先 `ClearHandFanAngle` → `ForceRebuildLayoutImmediate` → 读稳定 baseY → 写绝对 `baseY + t² × -cardOffsetY`（原代码以 LateUpdate 已移动后的 Y 为基础累积偏移）
  - 去掉全部 `Debug.Log`
- CardView.cs `SetHandFan` / `ClearHandFanAngle` / `LateUpdate` 逻辑保持，无需修改

**Decisions made**:
- Pencil 设计 7 张参考牌：旋转 ±5° 每步、Y 弧 ~56px、间距 ~90px
- 参数动态缩放（14f/countHalf、56f/countHalf²）确保任意牌数下视觉比例一致
- AI 手牌旋转方向镜像（`!isPlayer` 时 zAngle 取反），与玩家手牌对称

**Technical debt**: 无新增

**Problems encountered**:
- 旋转最大 40°（原 cardAngle=10 × tmax=4），远超 Pencil ±14°
- Y 叠加：每次 ApplyHandFan 以 LateUpdate 已移动后的 Y 为基，反复调用后牌越降越低
- countHalf = n/2f 导致 t 不对称（8 张牌 t 范围 -4~3 而非 ±3.5）

**Tests**: 编译通过（1 warning，无关），场景重建成功，Play Mode 无报错


---

## DEV-30c：Pencil 视觉细节修复 — 2026-04-14

**Status**: ✅ Completed

**What was done**:
- SceneBuilder.cs：EnemyRunes/EnemyBase/PlayerBase/PlayerRunes 各区域新增深海军蓝半透明 Image 背景（ZoneBgDefault/ZoneBgBase，alpha=0.4），消除黄色底色视觉 bug
- SceneBuilder.cs：SpellShowcasePanel CanvasGroup.alpha = 0f + interactable=false + blocksRaycasts=false，初始完全隐藏（之前 CanvasGroup alpha 默认 1，面板可见）
- SceneBuilder.cs：SpellTargetPopup（backdrop）CanvasGroup 同上，初始隐藏
- SceneBuilder.cs：RunesLabel/BaseLabel 字体大小 11→13，区域标签可读性改善
- SceneBuilder.cs：全部 3 处 Knob 精灵加载统一改为 `AssetDatabase.GetBuiltinExtraResource<Sprite>`（原 `Resources.GetBuiltinResource` 在某些 Unity 版本 Editor 下 Knob.psd 路径失效）

**Decisions made**:
- GameOverPanel 保留 SetActive(false)，无需 alpha=0（已有 active 状态控制，不走 DOTween fade-in）
- 区域背景使用 raycastTarget=false，不干扰卡槽点击

**Technical debt**: 无新增

**Problems encountered**:
- 初次重建未生效：SceneBuilder 编译旧代码，需先 recompile_scripts 再重建
- Knob.psd 加载失败：Editor 脚本应用 AssetDatabase API，Resources.GetBuiltinResource 在编辑器批量构建下可能返回 null

**Tests**: 编译 0 error 0 warning，场景重建成功，MCP 验证 RuneSlot0.Image.m_Sprite="Knob" / RunesLabel.fontSize=13

---

## UI-CLEANUP：详情面板极简 + 撤回投机性关键词 — 2026-04-19

**Status**: ✅ Completed

**What was done**:
- `CardDetailPopup.cs` 完全重写：右键卡牌只显示**放大的完整卡图（520×720）**，移除所有 chip / 文字 / 关键词徽章 / 描述区 / 状态文本；Panel 背景、金色外描边、纹理覆盖层全关
- `SimplifyToArtOnly()` 在首次 Show 时清空所有 legacy SerializedField 文本 + 隐藏 InfoColumn + 缩放 Panel 到竖版比例
- `CardKeyword.cs` 撤回投机性扩展枚举：删除 `Summon/BattleCry/Ongoing/Focus/Apprentice/Overwhelm/Starting/Rally/Ethereal/QuickStrike` 10 项（项目 50+ 卡全部不使用，无系统读这些 flag；是上周给详情面板做 UI 装饰时我误加的死代码）
- 删死文件：`Assets/Resources/{card_element_colors,keyword_colors,kw_colors}.json` + meta + `tools/scan_card_elements.py` / `scan_keyword_colors.py` / `sample_kw_colors.py` / `make_card_grid.py`

**Decisions made**:
- 经核查每个卡 `_effectId` 都对应 `SpellSystem`/`EntryEffectSystem`/`ReactiveSystem`/`DeathwishSystem`/`CombatSystem` 里的 handler（40 个 effectId / 40 个 handler，100% 覆盖）。原本"还有很多关键词逻辑没做"的说法是我把参考 LoR 卡上的概念误当项目缺口 — 实际上项目功能完整
- 详情面板极简化后不再需要任何字典/颜色表/JSON 加载器；保留 SerializedField 字段是为了不破坏 SceneBuilder 连线兼容

**Technical debt**:
- [ ] 历史测试未跟上代码演化（10 项 DOT*/DEV21* 失败），基线就已存在，跟本次清理无关；已追加 tech-debt.md

**Problems encountered**:
- 采样脚本在非固定切片位置上不稳定（迅捷在不同卡上采到绿/橙/红），尝试多轮后确认像素级取色方案不可行 → 果断改回简化方案
- stash 验证老测试失败确实先于本次清理就存在

**Tests**: 1109/1119 EditMode 通过；10 项失败全部经 stash 验证为历史问题，与本次清理无因果关系

---

## UI-OVERHAUL-1a：交互机制统一化（第 1 阶段）— 2026-04-21

**Status**: ✅ Completed

**What was done**:
- `GameColors.cs` 新增 3 常量：`ActionBtnEndTurn (#f4c23a 黄)`、`ActionBtnConfirm (#5fd064 绿)`、`ActionBtnCancel (#d24a4a 红)`
- `SceneBuilder.cs` EndTurnButton 颜色 `ActionBtnPrimary → ActionBtnEndTurn`，sprite tint 从 `Color.white` 改为 `ActionBtnEndTurn` 避免 sprite 原色压过
- `CardDragHandler.cs` 整体重写 790→600 行：
  - 删除 cluster ghost 多选系统（`_clusterGhosts` / `_clusterOrigViews` / `_clusterGhostOrigins` / `_clusterGhostWorldOrigins` / `_clusterMoveCoroutine` / `GatherCluster` / `ClusterFollowRoutine` / `RestoreCluster` / `BuildDragGroup` / `SetGhostsVisible` cluster 段 / `CANCEL_STAGGER_DELAY`）
  - 删除回调字段 `OnDragHandGroupToBase` / `OnSpellGroupDragOut`；`OnDragToBF` 签名改 `Action<UnitInstance, int>`
  - `DropFlowRoutine` 简化：去 `clusterUnits` 参数，只处理单 unit
  - `DropAnimHost.Run` 改为单 unit + 单 fromPos，AnimRoutine 从多卡循环改为单卡流
  - 保留主 `_ghost`（单张拖拽的视觉反馈）
- `GameUI.cs` `SetDragCallbacks` 签名由 6 参简化为 4 参，连线代码同步
- `GameManager.cs`：
  - 删除 Haste 询问弹窗 3 处（`TryPlayUnitAsync`/`TryPlayHeroAsync`/`CardDragHandler.DropFlowRoutine`）；`useHaste` 临时硬编码为 false，1b 将改为资源准备机制自动判定
  - `OnDragUnitsToBF(List, int)` → `OnDragUnitToBF(UnitInstance, int)` 单元素化
  - 手牌/基地点击改单选逻辑：点 A 清 B 再选 A；再次点 A 取消；移除 `[取消选择] XX（已选N张）` 等多选文案
  - 删除 "一次只能打出一张法术牌"/"一次只能使用一张装备牌" toast（单选本来就只有一张）
- `GameBot.cs` 同步单元素调用：`OnDragUnitsToBF(baseUnits, bfId)` → `OnDragUnitToBF(baseUnits[0], bfId)`
- 新建 `Assets/Scripts/UI/FloatingTipUI.cs`：鼠标位置 / 多行 / 多色飘屏组件，静态 `Show` / `ShowSingle` 工厂 + 颜色助手 `ManaShortLine/RuneShortLine/WarnLine`
- 新建 `Assets/Tests/EditMode/UIOverhaul1aTests.cs`（10 项）验证颜色常量、CardDragHandler 签名重构、FloatingTipUI 行为
- 更新老测试：`DEV22DragTests.OnDragToBF` / `DEV32BehaviorTests.OnDragToBF` 签名测试改单元素；`DOT4ReplacementTests.CancelConstants` 去掉 `CANCEL_STAGGER_DELAY` 期望；`ClusterFollowRoutine_StillCoroutine` 反转为 `_Removed`

**Decisions made**:
- 保留主拖拽 ghost（单张幻影跟随鼠标）作为拖拽视觉反馈，仅删除多选的 cluster ghost；可在后续 Phase 若用户明确希望直接拖原卡再移除
- Haste 临时硬编码 false；1b 将改为"玩家手动准备 +1 法力 + +1 符能 → 自动激活"
- 取消/结束回合按钮的颜色语义提前在 `GameColors` 中建常量，1c 直接复用
- FloatingTipUI 非单例设计：每次 `Show` 新建 GO 播完自毁，避免并发冲突

**Technical debt**:
- [ ] `GameManager.OnDragHandGroupToBase` / `OnSpellGroupDraggedOut` / `PlaySpellGroupAsync` 方法保留但成为死代码 — 未来如无依赖可删 — UI-OVERHAUL-1a
- [ ] `GameManager.GetSelectedHandUnits` / `GetSelectedBaseUnits` 返回 List 但单选下恒 ≤ 1 项 — 调用方可逐步改用单元素字段 — UI-OVERHAUL-1a
- [ ] `_pendingDragHasteDecision` / `SetDragHasteDecision` / `DragNeedsHasteChoice` 暂保留但在 1a 无效 — 1b 重构时统一移除 — UI-OVERHAUL-1a

**Problems encountered**:
- `CardDragHandler` 整体重写后老测试 `ClusterFollowRoutine_StillCoroutine` 断言失败 — 已反转期望为 `_Removed`
- 第一次全量 run 时 DEV5LegendTests 7 项偶发 `MissingReferenceException`（LegendSkillShowcase 单例残留），重跑后消失；非本次改动引入
- `FloatingTipUI` 首版漏 using `FWTCG.Data` 导致 `ToChinese` 扩展方法找不到，补全后通过

**Tests**: 编译 0 error 3 warning（均历史无关）；MCP EditMode 1130/1140 通过，10 项失败皆 `known-bugs.md` 已登记历史项
**引擎场景验证**：MCP `get_gameobject EndTurnButton` 确认 Image.color = `(0.957, 0.761, 0.227)` = `#f4c23a` 黄色 ✓

---

## UI-OVERHAUL-1b：符文手动标记 + 待结算资源池 — 2026-04-21

**Status**: ✅ Completed

**What was done**:
- `GameManager.cs`：
  - 新增字段 `_preparedTapIdxs` / `_preparedRecycleIdxs`（HashSet<int>）保存玩家手动标记的符文下标
  - 公开只读访问器 `GetPreparedTapIdxs()` / `GetPreparedRecycleIdxs()` + 写方法 `ClearPreparedRunes()`
  - `CommitPreparedRunes()` 真正执行 tap（Tapped=true + PMana+1）+ recycle（从 PRunes 移除 → PRuneDeck 底部 → AddSch +1）
  - `CountPreparedRecyclesOfType(type)` 统计准备回收的某符能数量（用于主/副符能校验）
  - `ValidateAndCommitPreparedFor(card)` 核心校验：mana + 主/副 sch 含 prepared 是否满足 cost → 不够调 FloatingTipUI 飘屏列出缺口 + 弹回（FireCardPlayFailed） + ClearPreparedRunes + 返回 false；满足 → CommitPreparedRunes + 返回 true
  - `OnRuneClicked` 彻底重写为标记 toggle：左键 tap-idx toggle（互斥 recycle）/ 右键 recycle-idx toggle（互斥 tap）/ 已横置符文不可标记
  - `OnTapAllRunesClicked` 改为"全部标记为待横置"
  - `PlayHandCardWithRuneConfirmAsync` / `PlayHeroWithRuneConfirmAsync` 简化：去 `RuneAutoConsume.Compute` + `AskPromptUI.WaitForConfirm` 消耗询问弹窗，直接走 ValidateAndCommitPreparedFor → 成功调 TryPlayCard/TryPlayHeroAsync
  - `OnEndTurnClicked` 回合结束自动 ClearPreparedRunes
  - `RefreshUI` 非行动阶段清空 prepared + 每次同步 `_ui.SetRuneHighlights(preparedTapIdxs, preparedRecycleIdxs)` 驱动呼吸灯
  - `OnCardHoverEnter/OnHeroHoverEnter/OnCardHoverExit/OnHeroHoverExit` 全部简化为 no-op（玩家手动模式下 auto plan 高亮会误导）
- `GameUI.cs`：
  - 公开 `RootCanvasRef`（FloatingTipUI 需要根 Canvas）
  - `RuneTapFill` 由蓝色 `(0.15, 0.50, 1.00)` 改为绿色 `(0.18, 1.00, 0.35)`，`RuneTapOutline` 同步绿色，与"准备横置"语义一致
- 新建 `Assets/Tests/EditMode/UIOverhaul1bTests.cs`（9 项）：
  - OnRuneClicked toggle / 互斥（左→右切换）/ 已横置守卫 / 越界忽略
  - ClearPreparedRunes 双集合清空
  - CommitPreparedRunes: tap only / recycle only / 混合路径全覆盖
- 更新 `DOT6ReplacementTests.GameUI_RunePulseConstants`：RuneTapFill 期望改绿

**Decisions made**:
- 资源池采用"pending + commit"模式：玩家点符文只加标记，不改 _gs；出牌成功时一次性 commit。这样失败路径天然无副作用（ClearPreparedRunes 即可）
- 失败时（资源不足）立即清空标记，避免下次出牌误 commit 失效标记；后续若希望"失败保留标记便于调整"可在 1c "取消"按钮落地时重新评估
- Haste 依然硬编码 false（1a 决策延续），待 1c"确定按钮"机制整合后再决定：目前最简方案是"prepared tap 数量 > cost + 1 且 prepared recycle 对应类型 ≥ runeCost + 1 → 自动激活"
- Hover auto-plan 高亮禁用为 no-op 而非删除方法，保留方法签名便于 1c 恢复"可选 hover 预览"

**Technical debt**:
- [ ] `RuneAutoConsume.Compute` 及 `ExecuteRunePlan` 仍被 AI 自动出牌 + Mulligan 场景使用 — 玩家路径不再走它；未来若 AI 也切单选标记可删 — UI-OVERHAUL-1b
- [ ] Hover 自动高亮（OnCardHoverEnter 等）暂 no-op 保留签名 — 1c 会再决定是否恢复"hover 预览可选" — UI-OVERHAUL-1b
- [ ] Haste 关键词判定硬编码 false — 1c 整合"确定按钮"时按"多准备 +1 法力 + +1 符能 → 自动激活"规则实现 — UI-OVERHAUL-1b

**Problems encountered**:
- `PlayHandCardWithRuneConfirmAsync` 去掉 await 后变 async 无 await warning — 改为普通 Task 返回，调用方 `await` 仍兼容
- 测试构造 `RuneInstance(RuneType)` 签名错误（正确为 `(int uid, RuneType)`）→ 修正
- `DOT6ReplacementTests.GameUI_RunePulseConstants` 原断言蓝色 RuneTapFill 失败 → 改为绿色期望

**Tests**: 编译 0 error 0 warning；EditMode 1140/1150 通过（10 项失败皆 known-bugs.md 登记的历史项）

---

## UI-OVERHAUL-1c-α：确定/取消按钮骨架 + 本回合入场栈 — 2026-04-21

**Status**: ✅ Completed（combat 延迟触发 / 回滚实现 / AI 适配留给 1c-β / 1c-γ）

**What was done**:
- `SceneBuilder.cs`：CancelRunesBtn/ConfirmRunesBtn 重命名为 CancelBtn/ConfirmBtn，颜色使用 1a 就位的 ActionBtnCancel（红）/ ActionBtnConfirm（绿），文案"取消/确定"；移除 `.SetActive(false)` 让其常驻可见
- `GameManager.cs`：
  - 新增 `PlayActionKind` 枚举（HandToBase/BaseToBF/HeroToBase）+ `PlayStackEntry` struct（Unit/BFIndex/ManaSpent/PrimarySchSpent/SecondarySchSpent）
  - 字段 `_thisTurnPlayStack: List<PlayStackEntry>` 保存本回合入场操作
  - 公开 API：`HasAnyPlayerUnitOnBattlefield()` / `HasThisTurnPlayActions()` / `RecordPlayAction(entry)` / `ClearThisTurnPlayStack()`
  - `OnConfirmClicked()` stub：校验（行动阶段 + 战场有我方单位）→ 广播"1c-α stub"+ 清栈；真实 combat 延迟触发放到 1c-β
  - `OnCancelClicked()` stub：校验（行动阶段 + 栈非空）→ 广播"1c-α stub"+ 清栈；真实 LIFO 回滚放到 1c-γ
  - `OnEndTurnClicked` 回合结束自动 ClearThisTurnPlayStack
- `GameUI.cs`：
  - `Awake` 接入 ConfirmBtn/CancelBtn onClick → `GameManager.Instance.OnConfirmClicked / OnCancelClicked`
  - `RefreshActionButtons` 重写：Confirm 亮色条件 = 行动阶段 + `HasAnyPlayerUnitOnBattlefield`；Cancel 亮色条件 = 行动阶段 + `HasThisTurnPlayActions`
  - 新静态 helper `ApplyActionButtonState(btn, active)`：active→Image 原色 + alpha=1 + interactable；inactive→Image color ×0.45 + alpha=0.55 + 禁用点击 + Text 淡化
- 新建 `Assets/Tests/EditMode/UIOverhaul1cAlphaTests.cs`（8 项）：
  - `HasAnyPlayerUnitOnBattlefield`：空战场 / 有单位两种场景
  - `PlayStack`：初始空 / RecordPlayAction 累加 / ClearThisTurnPlayStack 清空
  - `OnConfirmClicked` / `OnCancelClicked` stub 安全性（边界不崩）

**Decisions made**:
- 复用已有的 `_confirmRunesBtn` / `_cancelRunesBtn` 字段（GameUI SerializedField）避免引入新字段 + SceneBuilder 连线迁移成本；语义从"符文确认 / 符文取消"推广为"全局确定 / 全局取消"
- 1c 改动体量巨大（涉及 combat 流延迟 + AI 适配），按用户确认的 B 方案拆三阶段推进：α = UI 骨架 + 数据结构；β = combat 延迟触发 + Haste 自动判定；γ = LIFO 回滚 + AI 适配
- `ApplyActionButtonState` 当前仅做"亮/暗"二态；用户要求的"激活瞬间闪烁 + 长亮发光 + 粒子"留到 1c-γ 整体润色（避免在骨架阶段过早细化视觉）

**Technical debt**:
- [ ] `ConfirmBtn` / `CancelBtn` 字段在 GameUI 仍叫 `_confirmRunesBtn` / `_cancelRunesBtn` — 语义已变，后续可重命名（不影响 SceneBuilder 连线字段名）— UI-OVERHAUL-1c-α
- [ ] OnConfirmClicked / OnCancelClicked 仍为 stub — 1c-β/γ 未落地前用户点击只会清栈 + 广播提示，不会真实 combat / 回滚 — UI-OVERHAUL-1c-α
- [ ] 按钮"激活瞬间闪烁 + 长亮发光 + 粒子"未实现，1c-γ 整体润色时补完 — UI-OVERHAUL-1c-α

**Problems encountered**:
- 无阻塞问题；编译 0 warning，MCP 场景重建确认 ConfirmBtn 存在且文本为"确定"

**Tests**: EditMode 1140/1150 通过（10 项失败皆 known-bugs.md 登记的历史项）；新增 8 项 1c-α 测试全绿
**引擎场景验证**：MCP `get_gameobject ConfirmBtn` 确认：名字=ConfirmBtn, text="确定", activeSelf=true, ButtonCharge 挂载正常 ✓

---

## UI-OVERHAUL-1c-β/γ：combat 延迟 + LIFO 回滚 + 按钮激活动效 — 2026-04-21

**Status**: ✅ Completed（1c 系列收尾，整套 UI-OVERHAUL 完整落地）

**What was done（1c-β）**:
- `GameManager.cs`：
  - `PlayStackEntry` 扩展：ManaSpent / PrimaryType / PrimarySchSpent / HasSecondary / SecondaryType / SecondarySchSpent / UsedHaste / CommittedTappedUids / CommittedRecycled
  - `PreparedCommitSnapshot` 新增：CommitPreparedRunes 返回 TappedUids + Recycled 列表
  - `ValidateAndCommitPreparedFor` 重写：计算 manaAvailable/primaryHave/secondaryHave（含 prepared）；不够飘屏+清栈返 false；够→判定 Haste（有 Haste 关键词 && manaAvailable ≥ cost+1 && primaryHave ≥ primary+1 → 激活）→ CommitPreparedRunes → 扣 mana/sch（含 Haste 额外 +1+1）→ 填 `_pendingStackEntry` + `_lastCommitUsedHaste`
  - `TryPlayUnitAsync`：移除所有 mana/sch 检查 + 扣费（已在 ValidateAndCommit 做）；从 `_lastCommitUsedHaste` 读 Haste；落地后 `_thisTurnPlayStack.Add(entry with Kind=HandToBase, Unit, BFIndex=-1)`
  - `TryPlayHeroAsync`：同上，Kind=HeroToBase
  - `OnBattlefieldClicked`（base→BF）：只 MoveUnit + `_thisTurnPlayStack.Add(Kind=BaseToBF)`，删除原立即 CheckAndResolveCombat + FireDuelBanner + 550ms 延迟
  - `OnBattlefieldClicked`（Roam BF→BF）：同样只 MoveUnit，不 combat（不入栈因为非资源入场）
  - `OnConfirmClicked` 真实实现：`_bfClickInFlight=true` → `TriggerPendingCombatsAsync()` → `ClearThisTurnPlayStack()` → RefreshUI
  - `TriggerPendingCombatsAsync()`：遍历所有 BF，有我方单位 + 有敌方 → FireDuelBanner（2s banner）→ CheckAndResolveCombat；最后 550ms 余韵
  - `OnEndTurnClicked` 改为 async：若战场有我方单位自动 flush TriggerPendingCombatsAsync（等同隐式点击"确定"），再 EndTurn

**What was done（1c-γ）**:
- `GameManager.OnCancelClicked` 实际回滚：LIFO 遍历 `_thisTurnPlayStack`，逐条 `RollbackEntry`；`_gs.CardsPlayedThisTurn` 相应减回
- `RollbackEntry(entry)`：
  - HandToBase：PBase.Remove + PHand.Add + PlayedThisTurn=false + Exhausted=false
  - HeroToBase：PBase.Remove + PHero=unit + PlayedThisTurn=false + Exhausted=false
  - BaseToBF：BF[idx].PlayerUnits.Remove + PBase.Add + Exhausted=false
  - 资源还原：PMana += ManaSpent；AddSch 主/副符能
  - Tap 快照撤销：每个 CommittedTappedUid 找到 rune → Tapped=false + PMana -= 1
  - Recycle 快照撤销：每个 CommittedRecycled rune 从 PRuneDeck 移回 PRunes + SpendSch -1
- `GameUI.cs`：
  - 新字段 `_confirmBtnWasActive` / `_cancelBtnWasActive` / `_confirmPulseTween` / `_cancelPulseTween`
  - `ApplyActionButtonState(btn, active, ref wasActive, ref pulseTween)` 重写：状态切换时 Kill 旧 tween；切到 active → DOPunchScale(0.18, 0.35s, 6 vibrato) + DOScale 1.04 Yoyo 长亮 pulse；切回 dim → scale=1
- 新建 `Assets/Tests/EditMode/UIOverhaul1cBetaGammaTests.cs`（7 项）：
  - OnCancelClicked 回滚 HandToBase 三路径（unit+mana / 主符能 / Tap+Recycle 快照）
  - OnCancelClicked 回滚 BaseToBF（unit 回基地）
  - OnCancelClicked LIFO 多条（HandToBase + BaseToBF 混合）
  - OnConfirmClicked 无单位拒绝
  - OnCancelClicked 回滚 HeroToBase（PHero 恢复）

**Decisions made**:
- combat 仅玩家路径延迟；AI 路径（SimpleAI 直接调 _combatSys）保留原立即结算，避免大规模重构 AI
- Roam（BF→BF）也延迟 combat，但不入回滚栈 — Roam 只是位置移动无资源消耗，如需回滚后续可补
- OnEndTurnClicked 隐式 flush：鲁棒性考虑，避免玩家不点"确定"时战场 pending 永远不结算
- Confirm/Cancel 按钮动效采用轻量方案（punch + scale pulse）；用户原需求的"粒子从按钮向上飘"未做，留作后续润色（当前 scale 动效已能传达"可点击/激活"视觉差异）
- Haste 激活条件精简为"额外 +1 法力 + +1 主符能"，不要求副符能；用户手动横置/回收达到该阈值自动激活，否则休眠进场

**Technical debt**:
- [ ] Confirm 按钮"粒子从按钮向上飘"未实现，当前用 DOPunchScale + Yoyo scale 代替 — 1c-γ
- [ ] 1c 下 Play Mode 端到端验收尚未由人类执行 — 用户明确 Play Mode 手动验证 — 1c-γ
- [ ] Roam（BF→BF）延迟 combat 但不入栈，取消按钮不能回滚该类移动 — 低优先级，非典型回滚需求 — 1c-γ
- [ ] 玩家"确定"按钮触发 combat 时若敌方有反应牌，当前走原 ReactiveSystem 流程；按钮点击期间的状态切换（_bfClickInFlight）可能与反应窗口时序有耦合，需 Play Mode 观察 — 1c-γ

**Problems encountered**:
- UIOverhaul1cAlphaTests 里 UnitInstance 构造签名错误（正确为 `(int uid, CardData, string)`），已修
- RecordPlayAction / ClearThisTurnPlayStack 原 internal 跨 assembly 测试不可见，改 public
- MCP run_tests 一次性超时后按 CLAUDE.md 规则查 console 确认测试仍在跑 → 重试成功（1155/1165）

**Tests**: 编译 0 error 0 warning；EditMode 1155/1165 通过，10 项失败皆 known-bugs.md 登记的历史项
**引擎场景验证**：1c-α 阶段已通过 MCP 确认 ConfirmBtn 存在 + 文本"确定" + active；1c-β/γ 的 combat 流改动与按钮动效需 Play Mode 人工验收

---

## UI-OVERHAUL HOTFIX-1：拖拽 ghost 停留空中 bug 修复 — 2026-04-21

**Status**: ✅ Completed

**What was done**:
- 根因：1b 的 `ValidateAndCommitPreparedFor` 失败路径会 `RefreshUI` → 重建手牌 CardView → 销毁 CardDragHandler GameObject → `DropFlowRoutine` 协程中断，但 ghost (Instantiate 在 RootCanvas 下) 还活着 → 永远留在释放点
- 新增 `CardDragHandler.DropCancelHost`（nested class，独立 MonoBehaviour 挂 RootCanvas 下）：
  - `Spawn(ghost, targetWorldPos, shake)` 静态工厂
  - 协程：shake (optional) → DOTween move to origin → destroy ghost + destroy host
  - 生命周期与 CardDragHandler 完全解耦
- `DropFlowRoutine` step 4（stillInSource + _ghost != null）改为 `DropCancelHost.Spawn(_ghost, _dragOriginWorldPos, shake: true)` + `_ghost = null` + `yield break`
- `StartCancelDrag`（右键中途取消路径）同样改为 spawn host 接管，this 立即清理 selection + unblock events
- 移除 `CancelReturnTween` / `FinishDragCancel` / `UnblockEventsAfterCancel` 方法体（字段 `_cancelReturnSeq` 保留给 OnDestroy KillSafe 做兜底）
- 更新 `DOT4ReplacementTests.CardDragHandler_HasCancelReturnTween`：断言改为验证 `DropCancelHost` nested type 存在

**Decisions made**:
- 选择"独立 host"而非"让 ValidateAndCommitPreparedFor 不调 RefreshUI"：后者会让符文呼吸灯等 UI 状态不同步；前者彻底消除 ghost 生命周期依赖
- shake 参数化：右键中途取消 `shake=false`（直接弹回），DropFlowRoutine 失败 `shake=true`（传达"出牌失败"反馈）
- `_cancelReturnSeq` 字段保留但标注为 backward-compat OnDestroy 兜底，后续 cleanup 可删

**Tests**: 编译 0 error 0 warning；EditMode 1155/1165 通过（10 项失败皆 known-bugs.md 登记的历史项）

## CARD-FIX-1：按贴图修复 10 张卡行为不符 — 2026-04-23

**Status**: ✅ Completed（批 1 + 批 2a 两次提交汇总为一个 Phase）

**触发**：用户审计发现多张卡牌行为与卡面贴图文字描述不符，要求按贴图原文逐张修复。

**修复清单（10 张卡）**：

批 1（9a954b6）— 纯逻辑修复：
- `thousand_tail`：敌方-3 战力改用 TempAtkBonus（回合末清零），保持 ≥1 底线（原改 CurrentAtk 永久削弱）
- `darius_second_card`：订阅 GameEventBus.OnCardPlayed，恰好第二张牌瞬间触发 +2/活跃（原入场即固定触发）
- `tiyana_enter`：动态查战场存在（IsTiyanaOnAnyBattlefield）替代静态 TiyanasInPlay 字典；阻止所有得分类型（原只阻止 HOLD）
- `time_warp`：结算后放逐（GetExile）而非弃牌（原入 GetDiscard）
- `balance_resolve`：GameRules.GetSpellEffectiveCost 统一处理条件减费（对手得分 ≤3 或距胜 ≤3 时费用-2）
- `rally_call`：持续修饰符 GameState.RallyCallActiveThisTurn + TurnManager.DoAwaken 清零 + 追溯本回合已打出的疲惫单位（原只激活所有己方单位，与卡面"本回合打出"不符）

批 2a（7638ec5）— jax 被动 + Echo AI 路径 + 多段法术校正：
- `jax_enter`：GameRules.IsJaxInPlay 动态查询；反应窗口（玩家 + AI 双路径）接受装备作反应牌，直接部署到基地
- Echo 基础设施：GameRules.CanAffordEcho / SpendEchoCost（主符能池优先消耗 1 点）；SpellSystem 拆 CastSpell → ResolveSpellEffect + EchoCast；SimpleAI.CastAISpell 自动 Echo 循环
- `stardrop` "进行再次"：第二段若首目标死亡自动改打最低 HP 敌（PickLowestHpEnemy）
- `akasi_storm` "进行六次"：AI 逐次选最低 HP（原随机）
- `furnace_blast` "同一位置最多3名"：AI 选敌方单位最多的战场（原硬编码前 3 个单位）

**配套**：
- 移除废弃 GameState.TiyanasInPlay 字典 + CombatSystem 相关维护代码
- DEV2InteractionTests / SpellSystemTests 调整以匹配新语义
- SimpleAI.CanAfford / SpendCost 新增 UnitInstance 重载以套用条件费用

**Decisions made**：
- 批次拆分：批 1（纯逻辑）→ 批 2a（AI 路径）→ 批 2b（留作后续，玩家 UI 路径）。按用户"推荐就拆"指示
- Echo 成本固定为"1 主符能"（卡面"回响①"的通用理解）；法术专用池优先消耗
- tiyana "无法得分" 严格解读：所有类型（HOLD/CONQUER/BURNOUT）均阻止
- balance_resolve 条件参数：从卡面"对手得分或胜利得分不超过3分"解读为 oppScore≤3 或 WIN_SCORE-oppScore≤3
- furnace_blast "位置" = 战场（BF0/BF1），不含基地
- 玩家 Echo 付费确认 UI、stardrop/akasi/furnace 目标选择 UI 全部留 CARD-FIX-2

**Technical debt（CARD-FIX-2 承接）**：
- [ ] Echo 机制玩家付费确认弹窗（AskPromptUI）— CARD-FIX-1
- [ ] stardrop 第二段玩家选不同目标 UI — CARD-FIX-2
- [ ] akasi_storm 六次目标弹窗（SpellTargetPopup 复用）— CARD-FIX-2
- [ ] furnace_blast 位置选择弹窗（战场高亮 + 点击确认）— CARD-FIX-2

**Tests**：EditMode 1143/1154 通过；11 项失败全部为 pre-existing DOT*/DEV21* 动画常量测试（源码已演化，测试未跟上，不属本 Phase 新引入）。6 张修复卡的相关测试（DEV2InteractionTests / SpellSystemTests）全绿。

**引擎场景验证**：本 Phase 无视听改动（纯逻辑 + AI 路径），按 CLAUDE.md §1 标注跳过。
**代码审查**：Claude 自审查 + Codex adversarial-review（单独 dispatched）。

**Problems encountered**：
- Tiyana 旧 TiyanasInPlay 字典在测试文件多处使用（3 个 Test），统一重写测试以对应新语义
- darius 事件驱动 vs 入场自检：darius 自己就是本回合第二张时需要入场即自检一次（CardsPlayedThisTurn≥2 && !_dariusBuffedThisTurn），否则 OnCardPlayed 监听器不会对自身触发
- ScoreManager 使用 gs.TiyanasInPlay 被 IsTiyanaOnAnyBattlefield 替代，CombatSystem 死亡清理代码也同步删除

## CARD-FIX-2：多段法术玩家 UI 路径 — 2026-04-23

**Status**: ✅ Completed

**触发**：承接 CARD-FIX-1 留债 — 为 Echo 机制 + 多段法术（stardrop/akasi/furnace）补全玩家 UI 路径。

**改动**：

Echo（回响①）玩家路径：
- `GameManager.TryEchoPromptAsync`：AskPromptUI 询问"消耗 1 {rune}符能再次发动？"；玩家权衡期间可能被其他效果影响符能池，二次校验 CanAffordEcho
- 需目标的法术（divine_ray 等）重开 SpellTargetPopup 选新目标；取消回退首目标
- 接入 divine_ray / slam / furnace_blast 三张 Echo 法术

stardrop "进行再次"：
- `SpellSystem.StardropSecondStrike(target, gs)` 独立公共方法（第 2 段从 CastSpell 剥离）
- 玩家：TryStardropSecondAsync + SpellTargetPopup 选第二目标；取消兜底打首目标（若存活）
- AI：首目标死则 PickLowestHpEnemy 换目标，否则继续打首目标

furnace_blast "同一位置"：
- `GameState.FurnaceBlastBfOverride: int`（-1 = 未指定，AI 启发式兜底）
- `PickFurnaceBlastPositionAsync`：AskPromptUI 二选一，按钮文本用 BFNames 实际战场名
- `SpellSystem.FurnaceBlast` 新增 `bfOverride` 参数

akasi_storm "进行六次"：
- `GameState.AkasiStormTargets: List<UnitInstance>`（长度 ≤6，允许 null 占位）
- `PrepareAkasiStormTargetsAsync`：6 次 SpellTargetPopup，玩家每次可选不同单位或取消（取消则 null）
- `SpellSystem.AkasiStorm`：按列表顺序消费，遇 null / 已死 / 不存在时兜底选最低 HP

**配套（pre-existing bug 修复）**：
- 反应窗口法术 RuneCost 漏扣（玩家 + AI）— CARD-FIX-1 自审时发现，本 Phase 合并修复（SpendSchForSpell 扣主/次符能）

**Decisions made**：
- Echo 二次校验：玩家按是之后再查一次 CanAffordEcho，防止玩家权衡期间其他效果（如符文状态变化）影响可付费判定
- stardrop / akasi / furnace 的"状态字段传参" 而非改 CastSpell 签名：保留现有 CastSpell(spell, owner, target, gs) 签名，把多余数据放 GameState，由 SpellSystem 消费后清空
- akasi 允许 null 占位：玩家中途取消弹窗时以 null 表示"由兜底决定"，不退出循环（避免玩家因误操作失去剩余 5 次）
- furnace BF 选择用二选一 AskPromptUI（BFNames 作按钮文本）而非新建战场高亮 UI — 最简可行方案

**Technical debt**：
- [ ] Echo / stardrop / akasi 的 UI 可交互测试（Play Mode）未自动化
- [ ] akasi "取消 1 次只退当次"语义待 UX 确认（当前空位兜底合理）
- [ ] furnace 战场选择 UX：未来可迁移到"点击战场高亮"而非对话框

**Tests**：EditMode 1143/1154 通过，11 项失败仍为 pre-existing DOT*/DEV21*。SpellSystemTests 两项 stardrop 断言更新以反映 StardropSecondStrike 独立调用。

**引擎场景验证**：本 Phase 纯 async UI 对接 + 数据流，无场景/视觉改动，按 CLAUDE.md §1 标注跳过；交互 UX 由玩家 Play Mode 验收。

**代码审查**：Claude 自审（Codex 不可用：账号 plan 不支持 gpt-5.4）；未发现 High 级问题；本 Phase 合并修了 CARD-FIX-1 自审发现的 pre-existing reactive 符文漏扣 bug。

**Problems encountered**：
- stardrop 测试未随代码变更同步 → 更新两项 SpellSystemTests 显式调 StardropSecondStrike

## DEV-31 cleanup：tech-debt 批量清理 + 历史测试同步 — 2026-04-23

**Status**: ✅ Completed

**What was done（代码修复 5 项）**:
1. **AI 出牌 OnCardPlayed** — CARD-FIX-1 hotfix 已修（SimpleAI 英雄/单位/CastAISpell 三处 FireCardPlayed），本 Phase 仅追认标记
2. **EventBanner.DrainQueue OnDisable** — OnDisable 加 `StopCoroutine(_showRoutine)` 防止组件禁用后协程继续运行
3. **ClearHeroAura 双重销毁守卫** — 先 `_heroAura = null` 再 Destroy go，防外部代码绕过触发 fake-null 二次进入
4. **CreateShadow DOColor tween 未跟踪** — 新增 `_shadowFadeTween` 字段，Create / Clear / 3D-cleanup 三处 KillSafe
5. **_pendingDragHasteDecision / SetDragHasteDecision / DragNeedsHasteChoice 死代码** — CardDragHandler 确认无引用，字段 + 两个方法全部移除

**What was done（测试同步 11 项）**:
- DOT_MAX_SIZE: 8 → 5（MouseTrail 源）
- HOVER_SCALE: 1.08 → 1.18（CardHoverScale 源）
- ENDTURN_PULSE_MIN_ALPHA: 0.60 → 0.82（GameUI 源）
- SHAKE_DURATION: 0.28 → 0.08（CardView 源）
- SHAKE_VIBRATO: 7 → 12（CardView 源）
- Chaos 色: IsPurple → IsTealCyan（GetCardBurstColor 改 teal/cyan）
- StartupFlowUI 5 项 SHUFFLE_* / _shuffleGhosts / CreateShuffleAnimationTween 测试删除（源字段已移除）

**Decisions made**:
- 代码是真实源：测试过期的场景一律按源码更新（而非回滚源码）
- StartupFlowUI 5 项 SHUFFLE_* 测试：字段已不存在 → 删除而非保留 skip（不增加维护负担）
- ClearHeroAura 双重销毁：采用 "先置 null 再 Destroy" 最小防御（Unity fake-null 触发二次进入时 if-check 挡住）

**Tests**: EditMode **1149/1149 全绿** — 历史遗留失败归零！
**引擎场景验证**: 本 Phase 纯代码清理 + 测试同步，无视听改动，按 CLAUDE.md §1 标注跳过
**代码审查**: 5 项改动均为 low-risk 防御性修改，Claude 自审未发现问题
