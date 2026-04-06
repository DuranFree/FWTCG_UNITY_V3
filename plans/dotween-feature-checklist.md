# DOTween 迁移功能清单

> 生成日期：2026-04-05
> 来源：FWTCG_UNITY_V3 深度扫描（22文件，~70个动画方法）
> 规则：补间动画一律替换为 DOTween，禁止手写协程插值

---

## 排除列表（不改，实时跟踪/程序化模拟）

- [x] SnapFX.cs — 每帧跟踪活动 Transform — 排除
- [x] MouseLineFX.cs — 12点每帧从源到鼠标重新计算 — 排除
- [x] AimTargetFX.cs — 鼠标跟踪+Sin脉冲 — 排除
- [x] ParticleManager.cs — 55粒子+8符文+12萤火虫+4雾层程序化模拟 — 排除
- [x] Projectile.cs — 自定义贝塞尔曲线+切线旋转 — 排除

---

## DOT-1 — DOTween 安装 + 工具类基础设施

- [x] DOTween 导入（官网 v1.2.825 zip → Assets/Plugins/DOTween）— DOT-1
- [x] DOTween Setup（Module .cs 移入 Scripts/DOTweenModules，ASMDEF 配 precompiledRef）— DOT-1
- [x] FWTCG.Runtime.asmdef 添加 DOTween.dll precompiledReference — DOT-1
- [x] FWTCG.Tests.EditMode.asmdef 添加 DOTween.dll precompiledReference — DOT-1
- [x] 新建 TweenHelper.cs（KillSafe/FadeCanvasGroup/FadeImage/PulseAlpha/ShakeUI/KillAllOn）— DOT-1
- [x] 新建 TweenMatFX.cs（DOFloat/DOColor/DissolveSequence，替代 AnimMatFX 接口）— DOT-1
- [x] 新建 DOTweenTestBase.cs（Init/CompleteAll/KillAll setup/teardown 基类）— DOT-1
- [x] 验证：transform.DOMove() + DOAnchorPos 编译通过，31 个测试全绿 — DOT-1

---

## DOT-2 — 8个小文件替换

### FloatText.cs
- [x] AnimateRoutine → DOAnchorPos + DOFade Sequence — DOT-2

### DamagePopup.cs
- [x] AnimateRoutine → DOAnchorPos + DOFade Sequence — DOT-2

### ButtonCharge.cs
- [x] SweepRoutine → DOAnchorPosX — DOT-2

### ButtonHoverGlow.cs
- [x] PulseRoutine → DOTween.To().SetLoops(-1, Yoyo) — DOT-2

### MouseTrail.cs
- [x] ClickEffectRoutine → DOScale + DOFade Sequence — DOT-2

### ToastUI.cs
- [x] ShowToast → DOFade (TweenHelper.FadeCanvasGroup) + coroutine stay — DOT-2

### ReactiveWindowUI.cs
- [x] CountdownRoutine → DOVirtual.Float — DOT-2

### AnimMatFX.cs
- [x] CardView.DissolveOrFallbackRoutine 替换为 TweenMatFX.DissolveSequence — DOT-2
- [x] VFX3DissolveTests 适配新 TweenMatFX API — DOT-2
- [x] AnimMatFX.cs 现已无引用（可在 DOT-7 收尾时删除）— DOT-2

---

## DOT-3 — 6个中等文件替换

### EventBanner.cs
- [x] AnimateIn → DOAnchorPos + DOFade — DOT-3
- [x] AnimateOut → DOFade — DOT-3
- [x] ClearFadeRoutine → DOFade — DOT-3
- [x] WarningRoutine → DOScale(EaseOutBack) + DOFade — DOT-3

### AskPromptUI.cs
- [x] ShowRoutine → DOScale(Ease.OutBack) — DOT-3
- [x] HideRoutine → DOScale(Ease.InBack) — DOT-3

### SpellDuelUI.cs
- [x] BorderPulseLoop → DOFade loop — DOT-3
- [x] CountdownRoutine → DOScaleX / DOFillAmount — DOT-3

### AudioTool.cs
- [x] FadeRoutine → DOVirtual.Float 驱动 AudioSource.volume — DOT-3
- [x] CrossFadeRoutine → DOVirtual.Float 双通道交叉 — DOT-3

### CardHoverScale.cs
- [x] Update Lerp → DOScale，拖拽时 Kill + snap — DOT-3

### PortalVFX.cs
- [x] FadeRoutine → DOFade — DOT-3
- [x] Update旋转保留（连续旋转不适合DOTween）— 排除

---

## DOT-4 — 3个游戏逻辑文件替换

### CombatAnimator.cs
- [ ] FlyAndReturnRoutine → Sequence 3段 DOAnchorPos — DOT-4
- [ ] PlayShockwave → DOScale + DOFade — DOT-4

### SpellVFX.cs
- [ ] BurstParticles → 批量 DOAnchorPos + DOFade — DOT-4
- [ ] LegendFlame → 批量 DOAnchorPos + DOFade — DOT-4
- [ ] ProjectileThenFXRoutine → Sequence — DOT-4
- [ ] DelayedCardPlayFX → Sequence — DOT-4

### CardDragHandler.cs
- [ ] CancelReturnRoutine → DOAnchorPos — DOT-4
- [ ] ClusterFollowRoutine → DOAnchorPos with SetDelay — DOT-4
- [ ] DropFlowRoutine → Sequence — DOT-4
- [ ] DropAnimHost.AnimRoutine → Sequence 两阶段 — DOT-4

---

## DOT-5 — 4个装饰文件替换

### SceneryUI.cs
- [ ] SpinLoop → DORotate SetLoops — DOT-5
- [ ] DividerOrbLoop → DOAnchorPosY Yoyo — DOT-5
- [ ] CornerGemLoop → DOFade loop — DOT-5
- [ ] LegendGlowLoop → DOFade loop — DOT-5

### BattlefieldGlow.cs
- [ ] AmbientBreatheLoop → DOFade Yoyo loop — DOT-5
- [ ] CtrlGlowLoop → DOFade Yoyo loop — DOT-5

### SpellShowcaseUI.cs
- [ ] ShowCoroutine → DOAnchorPos + DOFade Sequence — DOT-5
- [ ] ShowGroupCoroutine → 同上批量 — DOT-5

### StartupFlowUI.cs
- [ ] 硬币翻转 → DOScaleX 翻转序列 — DOT-5
- [ ] 爆发粒子 → DOAnchorPos + DOFade 批量 — DOT-5

---

## DOT-6 — GameUI.cs（13个方法）

- [ ] BannerSlideRoutine → DOAnchorPos — DOT-6
- [ ] RuneHighlightPulseRoutine → DOColor loop — DOT-6
- [ ] PhasePulseRoutine → DOFade + DOScale — DOT-6
- [ ] LogEntryFlashRoutine → DOColor — DOT-6
- [ ] GameOverEnhancedRoutine → 复杂 Sequence — DOT-6
- [ ] FadeInPanelRoutine → DOFade — DOT-6
- [ ] BoardFlashRoutine → DOFade — DOT-6
- [ ] TimerPulseRoutine → DOScale + DOColor loop — DOT-6
- [ ] ScorePulseRoutine → DOScale — DOT-6
- [ ] ScoreRingRoutine → DOFillAmount + DOScale — DOT-6
- [ ] EndTurnPulseRoutine → DOScale + DOFade loop — DOT-6
- [ ] ReactRibbonRevealRoutine → DOAnchorPos + DOFade — DOT-6
- [ ] EquipFlyRoutine → DOAnchorPos — DOT-6

---

## DOT-7 — CardView.cs（17个方法）

- [ ] LiftFloatRoutine → DOAnchorPosY Yoyo loop — DOT-7
- [ ] ReturnToRestRoutine → DOAnchorPosY — DOT-7
- [ ] BreathGlowRoutine → DOFade loop — DOT-7
- [ ] StunPulseRoutine → DOFade loop (高频) — DOT-7
- [ ] FlashRedRoutine → DOColor — DOT-7
- [ ] ShakeRoutine → DOShakeAnchorPos — DOT-7
- [ ] BadgeScaleRoutine → DOScale — DOT-7
- [ ] TargetFadeOutRoutine → DOFade — DOT-7
- [ ] TargetPulseRoutine → DOFade loop — DOT-7
- [ ] OrbitRoutine → DOLocalMove 圆形或保留 Update — DOT-7
- [ ] HeroAuraPulseRoutine → DOFade loop — DOT-7
- [ ] EnterAnimRoutine → DOAnchorPos + DOScale Sequence — DOT-7
- [ ] FoilSweepRoutine → material.DOFloat — DOT-7
- [ ] PlayableSparkRoutine → 批量 DOAnchorPosY + DOFade — DOT-7
- [ ] AnimateSparkDot → DOAnchorPosY + DOFade — DOT-7
- [ ] DeathRoutine → DOAnchorPos + DOScale + DOFade / TweenMatFX dissolve — DOT-7
- [ ] DissolveOrFallbackRoutine → TweenMatFX.DOFloat("noise_fade") — DOT-7
- [ ] OnDestroy 统一 Kill → DOTween.Kill(gameObject) — DOT-7
- [ ] 所有 tween 加 .SetTarget(gameObject) — DOT-7

---

## 收尾

- [ ] 项目 CLAUDE.md 追加规则：补间动画一律使用 DOTween，禁止手写协程插值 — DOT-7
- [ ] 清理残留：删除不再使用的 AnimMatFX.cs（确认无引用后）— DOT-7
- [ ] 全量 EditMode 测试通过 — DOT-7
- [ ] PlayMode 手动验证完整对局 — DOT-7
