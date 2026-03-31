# 技术债清单
> 发现时追加，解决后删除
> 格式：- [ ] <描述> — 原因：<why deferred> — Phase <number>

- ✅ TMP Essential Resources 问题 — 已解决：全项目切换至 Legacy UnityEngine.UI.Text，TMP 彻底废弃 — Phase DEV-1
- ✅ CardData ScriptableObject 字段扩展 — DEV-2 已添加 keywords/effectId/isEquipment；DEV-3 已添加 isSpell/spellTargetType — Phase DEV-3
- ✅ 卡牌图片未导入 — DEV-2 已解决单位卡图片；DEV-3 已复制并导入10张法术卡图片 — Phase DEV-3
- [ ] StartupFlowUI 掷硬币无动画 — DEV-2 仅显示文字，动画效果（1800ms翻转）推迟到 DEV-4+ — Phase DEV-2
- [ ] UI 引用在 batch mode 下通过 GameObject.Find 连线，运行时若场景结构变化会失效 — Phase DEV-1
- [ ] balance_resolve 的"费用-2"条件效果未实现 — 需要手牌目标选择UI，推迟到 DEV-4 — Phase DEV-3
- ✅ 反应法术（swindle/retreat_rune/scoff等）未实现 — DEV-4 已实现 ReactiveSystem（9张反应牌全效果）+ ReactiveWindowUI（TaskCompletionSource异步窗口）— Phase DEV-4
- ✅ AI 不会使用法术 — DEV-4 已实现：SimpleAI 识别 IsSpell+非Reactive+自动选目标后施放 — Phase DEV-4
- ✅ DEV-1/DEV-2 交互测试补写（EditMode）— 已补完：DEV1InteractionTests 22项 + DEV2InteractionTests 17项，全绿 — Phase DEV-3
- ✅ 反应窗口双向对称 — DEV-15 已实现：玩家施法时 AI 自动选牌反应（CastPlayerSpellWithReactionAsync），AiPickBestReactiveCard 五级优先级 — Phase DEV-4→DEV-15
- [ ] 反应牌自动选目标（无目标选择UI）— DEV-4 简化，完整目标选择待 DEV-5+ — Phase DEV-4
- [ ] 反应按钮无倒计时（点击后玩家可无限等待）— DEV-4 简化，30秒倒计时+自动随机出牌待 DEV-11 — Phase DEV-4
- ✅ AI 传奇技能决策 — DEV-7 已实现：卡莎虚空感知（手牌有炽烈法术且符能不足时触发）— Phase DEV-7
- ✅ 传奇升级视觉 — DEV-15 已实现：LegendSystem.OnLegendEvolved 事件 + GameUI.FlashLegendText 金色闪烁协程 — Phase DEV-5→DEV-15
- [ ] BF效果目标选择无UI（aspirant_climb/star_peak等自动选择）— DEV-6 简化，完整目标选择UI待后续Phase — Phase DEV-6
- [ ] BF卡 SceneBuilder 尚未创建 CardData asset（19张BF卡仅有逻辑层，无.asset文件）— 运行时通过BFNames字符串ID触发，待资产Phase创建 — Phase DEV-6
- ✅ CardGlow.shader 材质已连线到 CardPrefab rootImg — DEV-8 hotfix
- ✅ CardShine overlay 材质已创建 ShineOverlay Image 子对象 — DEV-8 hotfix
- ✅ HexGrid.shader 材质已赋予 Background Image — DEV-8 hotfix
- ✅ 悬停放大动画 — CardHoverScale.cs 已实现（Lerp 1.08x scale，Update协程），SceneBuilder 已连线 — Phase DEV-8→DEV-9
- [ ] 入场 Foil Sweep 未实现（需对角光扫动画）— DEV-9 — Phase DEV-8
- [ ] kaisa_legend/yi_legend CardData 缺少卡图（需 tempPic 中找传奇卡图片，或用户提供）— Phase DEV-10
- ✅ 弃牌堆/放逐堆 Button onClick — 已通过 GameUI.SetPileClickCallback + WirePileButtons() 在运行时连线，实现正常 — Phase DEV-10
- [ ] DamagePopup 每次 new GameObject（GC churn）— 高频伤害时压力大，应改为对象池 — Phase DEV-17（Codex Medium/Low）
- [ ] GameUI.OnUnitDied/OnUnitDamaged 订阅在 Awake/OnDestroy，禁用组件时仍活跃 — 应改为 OnEnable/OnDisable — Phase DEV-17（Codex Medium）
- [ ] OnBattlefieldClicked async void 无结构化异常处理 — await 后异常无法传播 — Phase DEV-17（Codex Medium）
- [ ] CardView.OnDestroy 只停 _stunPulse，_shake/_flash/_death 靠 Unity 隐式停止 — Phase DEV-17（Codex Low）
- ✅ Ephemeral 单位打出时未设置 IsEphemeral/SummonedOnRound — 已修复：UnitInstance 构造函数从 CardData.HasKeyword(Ephemeral) 初始化 IsEphemeral；TryPlayUnit 打出瞬息单位时设置 SummonedOnRound=gs.Round — Phase DEV-18（Claude 审查 High → 已解决）
- [ ] CombatAnimator 并发冲击波未保护 — 同一 BF 快速连续战斗时第二个 PlayShockwave 会与第一个同时运行，第一个结束时 SetActive(false) 打断第二个动画；修复：OnCombatResult 先 StopCoroutine 再重启 — Phase DEV-18（Claude 审查 Medium）
- [ ] AI 出牌不触发 OnCardPlayed/BoardFlash — FireCardPlayed 只在玩家三条出牌路径调用，AI 出牌无棋盘闪烁；如需视觉对称须补充 AI 路径调用 — Phase DEV-18（Claude 审查 Medium，设计待确认）
- [ ] CtrlGlowLoop 无控制方时 alpha 非零 — NoGlow=(0,0,0,0) 但 Lerp(0.10,0.35,pulse) 始终>0，产生微弱黑色叠加；修复：_currentCtrl==null 时直接 alpha=0 — Phase DEV-18（Claude 审查 Low）
- [ ] Ephemeral 销毁未加入弃牌堆 — DestroyEphemeralUnits 只从列表移除，未调用 gs.GetDiscard(owner).Add(u)；需确认是否设计意图 — Phase DEV-18（Claude 审查 Low）
