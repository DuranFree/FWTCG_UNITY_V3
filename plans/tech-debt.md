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
- [ ] 反应窗口仅在 AI 施法时触发（玩家施法时 AI 不反应）— DEV-4 简化，完整轮流响应待 DEV-5+ — Phase DEV-4
- [ ] 反应牌自动选目标（无目标选择UI）— DEV-4 简化，完整目标选择待 DEV-5+ — Phase DEV-4
- [ ] 反应按钮无倒计时（点击后玩家可无限等待）— DEV-4 简化，30秒倒计时+自动随机出牌待 DEV-11 — Phase DEV-4
- ✅ AI 传奇技能决策 — DEV-7 已实现：卡莎虚空感知（手牌有炽烈法术且符能不足时触发）— Phase DEV-7
- [ ] 传奇升级无动画（Level 1→2 仅数值变化，无视觉表现）— DEV-5 简化，动画推迟至视觉Phase — Phase DEV-5
- [ ] BF效果目标选择无UI（aspirant_climb/star_peak等自动选择）— DEV-6 简化，完整目标选择UI待后续Phase — Phase DEV-6
- [ ] BF卡 SceneBuilder 尚未创建 CardData asset（19张BF卡仅有逻辑层，无.asset文件）— 运行时通过BFNames字符串ID触发，待资产Phase创建 — Phase DEV-6
- ✅ CardGlow.shader 材质已连线到 CardPrefab rootImg — DEV-8 hotfix
- ✅ CardShine overlay 材质已创建 ShineOverlay Image 子对象 — DEV-8 hotfix
- ✅ HexGrid.shader 材质已赋予 Background Image — DEV-8 hotfix
- [ ] 悬停放大动画未实现（需 DOTween 或协程 scale 动画）— DEV-9 — Phase DEV-8
- [ ] 入场 Foil Sweep 未实现（需对角光扫动画）— DEV-9 — Phase DEV-8
