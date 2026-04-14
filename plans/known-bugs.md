# 已知 Bug 清单
> 发现时追加，修复后标记 ✅，不删除
> 格式：- [ ] <描述> — 发现于 Phase <number>

- ✅ TMP 文字不显示 — 已修复：全项目切换至 Legacy Text，不再依赖 TMP — 发现于 Phase DEV-1
- ✅ GameScene UI 无响应 — 已修复：SceneBuilder 添加 EventSystem + StandaloneInputModule — 发现于 Phase DEV-1
- ✅ 卡在7分无法获胜 — 已修复：删除错误的最后1分受限规则（规则不存在）— 发现于 Phase DEV-1
- ✅ 战场每方只能放2张单位 — 已修复：删除 MAX_BF_UNITS 限制（规则不存在）— 发现于 Phase DEV-1
- ✅ 手牌莫名丢弃 — 已修复：删除 MAX_HAND_SIZE = 7 限制（规则不存在）— 发现于 Phase DEV-1
- ✅ GameStateTests.cs 编译错误 — 已修复：移除对已删除常量 MAX_HAND_SIZE/MAX_BF_UNITS 的引用 — 发现于 Phase DEV-1
- ✅ Debug面板只显示2个按钮 — 已修复：VerticalLayoutGroup.childControlHeight 改为 true 使 preferredHeight 生效 — 发现于 DEV-3
- ✅ 法术目标无法点击 — 已修复：敌方单位 RefreshUnitList 改为传入 _onUnitClicked 而非 null — 发现于 DEV-3
- ✅ 日志文字被右边裁切 — 已修复：MessageText 的 horizontalOverflow 改为 Wrap — 发现于 DEV-3
- ✅ 符文牌右键回收无反应 — 已修复：EventTrigger从root移到RuneCircle（Button在circle上拦截事件）— 发现于 DEV-11
- ✅ 符文牌被上下拉伸 — 已修复：HLG关闭childControlHeight + LayoutElement固定46×46 — 发现于 DEV-11
- ✅ SpellShowcasePanel coroutine inactive 报错（Lazy Awake 陷阱）— 彻底修复：Awake 改用 CanvasGroup Hide()，不再 SetActive(false)，面板始终保持 active — 发现于 DEV-16b，根治于 DEV-16c
- ✅ SpellTargetPopup coroutine inactive 同类问题 — 已修复：同上 CanvasGroup 方案 — 发现于 DEV-16c
- ✅ CardView 重复 Unit 属性编译错误 — 已修复：移除行39的重复定义，保留行299原有属性 — 发现于 DEV-16b
- ✅ AI 不出单位牌（符能被提前耗尽）— 已修复：AiRecycleRunes 跳过本回合法力不足的卡牌，防止无意义的 sch 预消耗 — 发现于 DEV-16c
- ✅ EffectiveAtk 最小值为 1 而非 0 — 已修复：Mathf.Max(1,…) 改为 Mathf.Max(0,…)，符合 Rule 139.2 — 发现于规则对比，修复于 DEV-25b patch
- ✅ 游走关键词检查缺失 — 已修复：OnBattlefieldClicked 移动前加 HasKeyword(Roam) 检查，无游走单位无法跨战场 — 发现于规则对比，修复于 DEV-25b patch
- ✅ 装备弹窗出现时卡牌提前弹回 — 已修复：SpellTargetPopup.IsShowing 静态标志，DropFlowRoutine 等待弹窗关闭后再执行弹回动画 — 发现于 DEV-22
- ✅ 装备飞行动画落点错误 — 已修复：RectTransformToCanvasLocal() 统一转换所有位置到 canvas-root 局部坐标系 — 发现于 DEV-22
- ✅ RefreshUI 覆盖 HideEquipCardInBase 的 alpha 设置 — 已修复：ActivateEquipmentAsync 先 RefreshUI 后 HideEquipCardInBase — 发现于 DEV-22
- ✅ 弹窗回合结束未自动关闭 — 已修复：SpellTargetPopup/AskPromptUI 订阅 GameEventBus.OnClearBanners，ReactiveWindowUI 随机出牌 — 发现于 DEV-22
- ✅ 符文类型显示英文 — 已修复：RuneTypeExtensions.ToChinese/ToColoredText()，所有用户提示改用彩色中文 — 发现于 DEV-22
- ✅ EquipFlyRoutine onDone 挂起（GameUI 销毁）— 已修复：_pendingEquipOnDone 字段，OnDestroy 调用回调解锁 tcs2 — 发现于 DEV-22 Codex H-1
- ✅ _statusTooltip 泄漏（CardView 销毁）— 已修复：CardView.OnDestroy 销毁 _statusTooltip — 发现于 DEV-22 Codex H-3
- ✅ Mulligan 面板中 PlayableSparkRoutine 报 "Coroutine couldn't be started because the game object is inactive" — 已修复：CardView.Refresh 加 `gameObject.activeInHierarchy` 守卫，Mulligan 面板隐藏时不启动协程 — 发现于 DEV-31
- ✅ WaitForReaction await 无 OperationCanceledException 保护，ReactiveWindowUI 被禁用时 _reactionWindowActive 永久卡死 — 已修复：GameManager try/catch OperationCanceledException — 发现于 DEV-31 Codex HIGH
- ✅ EnemyRunes/EnemyBase/PlayerBase/PlayerRunes 显示黄色底色 — 已修复：各区域缺少 Image 背景组件，已补加深海军蓝半透明 Image — 发现于 DEV-30c
- ✅ SpellShowcasePanel / SpellTargetPopup 初始可见（CanvasGroup alpha=1）— 已修复：SceneBuilder 创建时设 alpha=0，不再一帧闪现 — 发现于 DEV-30c
