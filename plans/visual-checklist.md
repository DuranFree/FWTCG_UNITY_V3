# 视觉清单 — FWTCG Unity 移植
> Phase 2 生成，每完成一项立即标记 ✅

---

## 一、颜色系统

- ✅ GameColors 静态类（所有颜色常量统一管理，不硬编码）— DEV-8
- ✅ 主色：金色 #c8aa6e / 亮金 #f0e6d2 / 暗金 #785a28 — DEV-8
- ✅ 副色：Hextech 青色 #0ac8b9 / 暗青 #005a82 — DEV-8
- ✅ 背景色：深海军蓝 #010a13 / 中调 #0a1428 — DEV-8
- ✅ 状态色：玩家绿 #4ade80 / 敌方红 #f87171 — DEV-8
- ✅ 符文颜色（6种：火橙 / 金黄 / 翠绿 / 红 / 紫 / 蓝）— DEV-8
- ✅ 蓝色魔法光效色 rgba(60,140,255)（GameColors.BlueSpell / BlueSpellDim）— DEV-23
- ✅ 符文高亮预览色（蓝 rgb(64,140,255) = 待横置 / 红 rgb(255,64,64) = 待回收，悬停手牌时覆盖符文圆圈颜色）— DEV-20

---

## 二、字体系统

- ❌ Cinzel 字体导入 — OUT OF SCOPE：不需要，使用现有字体
- ❌ Noto Sans SC 字体导入 — OUT OF SCOPE：不需要，使用现有字体
- ❌ 字体大小层级定义 — OUT OF SCOPE：不需要

---

## 三、背景与环境

- ✅ 深海军蓝主背景（#010a13，带色调，非纯黑）— DEV-8
- ✅ 六边形网格纹理叠加（HexGrid.shader 手写 HLSL）— DEV-8
- ✅ 噪点纹理叠加（HexGrid.shader 程序化 hash 噪点）— DEV-8
- ✅ 分层渐变背景（中心青色光晕 + 暗角，HexGrid.shader）— DEV-8
- ✅ 边角暗角 Vignette（URP Post Processing + HexGrid.shader）— DEV-8
- ✅ URP Post Processing Volume 配置（Bloom + Color Adjustments + Vignette + Film Grain）— DEV-8
- ✅ 中央旋转装饰（SpinOuter 20s CW / SpinInner 12s CCW，蓝/青色半透明圆盘）— DEV-23
- ✅ 中心符文旋转（SigilOuter 30s CW / SigilInner 20s CCW，金色半透明大圆盘）— DEV-23
- ✅ 中央分隔线能量球（DividerOrb 18px，3.5s 正弦 Y 振荡 ±20px）— DEV-23
- ✅ 角落宝石脉冲（4个角落 48px 金色图标，4s alpha 0.3↔0.9 循环）— DEV-23
- ✅ 传奇槽位光晕（LegendGlow 蓝色叠加层，5s 呼吸 alpha 0.15↔0.6，覆盖传奇区）— DEV-23

---

## 四、卡牌视觉

- ✅ 卡牌 Prefab 基础外观（边框/背景/图片层/文字层）— DEV-9（SceneBuilder.CreateCardPrefab）
- ✅ 悬停放大 + 发光效果（CardHoverScale.cs 1.08x scale + CardGlow.cs 发光边框）— DEV-9
- ❌ 悬停上浮效果 — OUT OF SCOPE：选中时上浮已足够，无需独立悬停上浮
- ✅ 3D 倾斜效果（鼠标跟随，18° 最大，CardTilt.cs）— DEV-8
- ✅ 全息光泽 Shine（鼠标位置驱动的径向渐变，CardShine.shader）— DEV-8
- ✅ 可出牌粒子边框（conic rotation 彗星效果，CardGlow.shader）— DEV-8
- ✅ 入场 Foil Sweep（0.8s 对角光扫，CardShine.shader + FoilSweepRoutine，EnsureShineOverlay）— DEV-30 V6
- ✅ 手牌卡片入场动画（hand-card-enter 0.42s，Y -30px 飞入 + scale 0.82→1 + alpha 0→1，EaseOutQuad）— DEV-28
- ✅ 可出牌卡粒子特效（playable-particle 3s，上方闪烁光点，PlayableSparkRoutine + _sparkDots）— DEV-30 V7
- ✅ 卡牌选中轨道光环（selected-arcane-orbit 6s，10px 金色光点半径 60px 旋转）— DEV-28
- ✅ 死亡飞行动画（Phase A 红闪缩小 0.3s + Phase B ghost 贝塞尔弧线飞向弃牌堆 0.5s，CardView.DeathRoutine）— DEV-29
- ✅ 费用不足变暗（整体压暗 ×0.6，CardView.SetCostInsufficient）— DEV-8
- ✅ 休眠状态（灰化 + CardExhausted 颜色）— DEV-8
- ✅ 眩晕状态（红色脉冲叠加层，StunPulseRoutine 协程）— DEV-8
- ✅ 增益指示物（+1/+1 金色图标，右上角叠加）— DEV-8
- ✅ Buff 状态徽章（▲绿色，卡面底部，右键查看详情）— DEV-22
- ✅ Debuff 状态徽章（▼红色，卡面底部，右键查看详情）— DEV-22
- ✅ 卡牌背面样式（几何纹样覆层：四边边框条 + 中央45°菱形，CardView.EnsureCardBackOverlay）— DEV-29
- ✅ 英雄光环（hero-card-aura 4s 缓慢脉冲，金色 alpha 0.25↔0.60，英雄卡专属）— DEV-28

---

## 五、拖拽视觉系统

- ✅ 拖拽时卡牌跟随鼠标（Ghost 克隆，alpha=0.72，scale=0.88）— DEV-22
- ✅ 漩涡/传送门特效（3 层同心圆，CW/CCW 旋转，蓝色半透明圆盘）— DEV-22
- ✅ 漩涡螺旋粒子（8 个轨道粒子，蓝色发光）— DEV-22
- ✅ 漩涡淡入（0.28s）/ 淡出（0.22s）— DEV-22
- ✅ 法术牌放置动画（EaseOutQuad 飞行 + EaseInQuad 落地，DropAnimHost）— DEV-22
- ❌ 爆裂粒子（16 个放射状粒子）— OUT OF SCOPE：用户确认不需要
- ✅ 单位牌放置动画（飞行 → 悬浮弹跳点 → 落地，DropAnimHost.AnimateDropCard）— DEV-22
- ❌ 落地涟漪（land ripple expand 0.55s）— OUT OF SCOPE：用户确认不需要
- ❌ 落地震动（±2px，0.25s）— OUT OF SCOPE：用户确认不需要
- ✅ 拖拽失败弹回（右键取消，CancelReturnRoutine ease-in/ease-out）— DEV-22

---

## 六、战场视觉

- ✅ 战场区域环境呼吸动画（bf-ambient-breathe，5s）— DEV-18（BattlefieldGlow.AmbientBreatheLoop）
- ✅ 玩家控制战场绿色光晕（bf-ctrl-player-glow 3s 交替脉冲）— DEV-18（BattlefieldGlow.CtrlGlowLoop 绿色）
- ✅ 敌方控制战场红色光晕（bf-ctrl-enemy-glow 3s 交替脉冲）— DEV-18（BattlefieldGlow.CtrlGlowLoop 红色）
- ✅ 战场卡牌展示（战场特殊卡的图片显示）— DEV-18（GameUI.UpdateBFCardArt Resources.Load）
- ✅ 战斗触发特效（单位冲向对方 ghost overlay 0.20s 飞出 + 0.15s 弹回，CombatAnimator.FlyAndReturnRoutine）— DEV-28
- ✅ 战斗冲击波（playCombatShockwave，中央扩散金色光环）— DEV-18（CombatAnimator.PlayShockwave 0.45s scale 0→1.5）
- ✅ 征服/对决横幅动画（Duel Banner，FireDuelBanner → ShowBanner slide 动画）— DEV-19
- ✅ 法术对决全屏叠加 UI（SpellDuelUI：青色4px边框脉冲 + 30s倒计时 + 进度条，自动跳过反应）— DEV-30 F2
- ✅ 出牌时环境光闪烁（board-event-fade 0.85s）— DEV-18（GameUI.BoardFlashRoutine OnCardPlayed 事件驱动）

---

## 七、粒子特效系统

- ✅ 背景漂浮粒子（55个，金/青/蓝/紫，上浮+正弦漂移，BG_COUNT=55）— DEV-21
- ✅ 符文字形漂浮（8个古北欧符文字符，金/青，旋转上浮，RUNE_COUNT=8）— DEV-21
- ✅ 萤火虫粒子（12个，正弦振荡运动，光晕，FIREFLY_COUNT=12）— DEV-21
- ✅ 鼠标拖尾（18点轨迹，TRAIL_LENGTH=18，DOT_MAX_SIZE=8，帧衰减）— DEV-21
- ✅ 点击涟漪（径向扩散圆环 20→80px 0.5s）— DEV-21
- ✅ 点击六边形闪光（6个金色向外扩散，0→38px）— DEV-21
- ✅ 底部云雾（4层，青色，水平漂移，MIST_COUNT=4）— DEV-21
- ✅ 粒子连线系统（LINE_RADIUS=90px内连线，青色，动态透明度，LINE_POOL=80）— DEV-21
- ✅ 伤害数字飘出（受击时弹出伤害数值，红色粗体+黑色描边，ease-out 上浮）— DEV-17
- ✅ 单位阵亡收缩特效（缩小+淡出 0.45s，加速二次方曲线）— DEV-17
- ✅ 通用飘字（FloatText 池化，上浮 60px+淡出 0.9s，支持颜色/大小）— DEV-18b
- ✅ 小横幅（EventBanner CanvasGroup + 滑入 0.2s + 停留 + 淡出 0.25s，队列防重叠）— DEV-18b
- ✅ 法术施放特效（SpellVFX：按RuneType着色16点径向爆裂；阵亡橙红爆裂12点；传奇进化3s火焰20粒）— DEV-21
- ✅ 征服得分粒子爆发（金色20点径向爆裂，SpellVFX.OnConquestScored，GameEventBus.OnConquestScored）— DEV-30 F1

---

## 八、得分与状态 UI 动画

- ✅ 得分圆圈（金色边框 + 颜色填充，9个圆圈0-8）— DEV-9
- ✅ 玩家得分脉冲（绿色，1.8s scale 1→1.15，TriggerScorePulse + ScorePulseRoutine）— DEV-19
- ✅ 敌方得分脉冲（红色，1.8s）— DEV-19
- ✅ 得分环扩散（score-ring-expand，scale 1→2.5，alpha→0，SpawnScoreRing + ScoreRingRoutine）— DEV-19
- ✅ 回合横幅动画（"回合 N · 你/AI的回合"，slide Y-40→0 0.28s + stay + slide out 0.22s）— DEV-19
- ✅ 法术对决横幅（"⚡ 法术对决！"，FireDuelBanner → OnDuelBannerHandler → ShowBanner）— DEV-19
- ✅ 阶段指示器脉冲（phase-pulse，0.4s scale 1→1.18→1，PhasePulseRoutine，阶段变化时触发）— DEV-19
- ✅ 按钮光流效果（btn-charge 1.5s，悬停激活，ButtonCharge.cs + RectMask2D，结束回合/反应/确认符文三个按钮）— DEV-19
- ✅ 结束按钮常驻脉冲（btn-end-magic-pulse 2s，有可操作时，EndTurnPulseRoutine alpha 1↔0.6）— DEV-19
- ✅ 反应按钮 ribbon 展开动画（ribbon-reveal scaleX 0→1 0.25s + react-ribbon-pulse 2s）— DEV-19

---

## 九、标题/开始界面视觉

- ✅ 标题文字动画（fadeInUp + scale + fade stagger，TitleTextEntranceRoutine，StartupFlowUI）— DEV-30 V1
- ✅ 六边形呼吸光效（hexBreath 3s alpha 脉冲，HexBreathLoop，StartupFlowUI）— DEV-30 V2
- ✅ 标题中心光束脉冲（titleBeamPulse 2s 无限循环，TitleBeamPulseLoop，StartupFlowUI）— DEV-30 V3
- ✅ 按钮入场动画（ButtonCharge 硬币面板 + 梦想手牌确认按钮，SceneBuilder）— DEV-30 V4/V8
- ✅ 分层背景光效（旋转渐变覆层，BgGradientRotateLoop，StartupFlowUI）— DEV-30 V5
- ✅ 信息栏扫光（ScanLight 蓝色水平扫光，8s 周期，StartupFlowUI.ScanLightLoop）— DEV-24

---

## 十、掷硬币界面

- ✅ 硬币正面图片（xianshou.png，StartupFlowUI.CoinSpinRoutine Resources.Load 接入，落地时显示正面）— DEV-24
- ✅ 硬币背面图片（houshou.png，StartupFlowUI.CoinSpinRoutine Resources.Load 接入，翻转时交替显示背面）— DEV-24
- ✅ 翻转动画（1800ms，5次 scaleX 0→1 翻转 + 落地弹跳，StartupFlowUI.CoinSpinRoutine）— DEV-24
- ✅ 结果文字淡入（0.4s FadeTextIn，coinResultText）— DEV-24

---

## 十一、梦想手牌界面

- ✅ 换牌选中状态（CardView.SetSelected → GameColors.CardSelected 金色背景，StartupFlowUI.UpdateMulliganUI 驱动）— DEV-22/DEV-24
- ✅ 确认按钮动画（梦想手牌确认按钮 ButtonCharge，SceneBuilder.CreateMulliganPanel）— DEV-30 V8
- ✅ 界面进场/退场过渡（CanvasGroup 0.4s 淡入 / 0.3s 淡出，MulliganFlowRoutine）— DEV-24

---

## 十二、过渡与切换效果

- ✅ 界面淡入淡出（0.3-0.5s，CoinFlip/Mulligan 面板均使用 CanvasGroup fade）— DEV-24
- ✅ 全屏 Banner 进场/退场（EventBanner slide 动画）— DEV-19
- ✅ 游戏结束界面淡入（0.5s FadeInPanelRoutine，GameUI.ShowGameOver）— DEV-24

---

## 十三、日志面板视觉

- ✅ 日志折叠/展开按钮动画（">" ↔ "<" 切换，0.3s 协程过渡）— DEV-10（GameUI.AnimateLogToggle）
- ✅ 日志折叠联动棋盘居中（折叠时 boardWrapperOuter 自动扩展，offsetMax.x 动画）— DEV-10（GameUI.AnimateLogToggle）
- ✅ 日志条目进入闪烁（log-entry-flash 0.8s，金色→白色，GameUI.LogEntryFlashRoutine）— DEV-24

---

## 十四、区域标识视觉

- ✅ 区域边框画线（所有游戏区域可见边框，Outline 组件 + 金色半透明）— DEV-10
- ✅ 区域名字标签（BASE/LEGEND/HERO/TRASH/EXILE/RUNES，9px 金色半透明）— DEV-10
- ❌ 符文回收按钮样式（♻ 按钮入场动画）— OUT OF SCOPE：已改为右键回收，无需独立按钮

---

## 十五、传奇/英雄特殊视觉

- ✅ 传奇升级视觉（金色闪烁4次协程，FlashLegendText，0.15s × 4 脉冲）— DEV-15
- ✅ 法术施放展示面板（SpellShowcaseUI：底部飞入+停留+上飞，卡名/效果/归属/卡图）— DEV-16
- ✅ 中央 SVG 旋转装饰（SpinOuter 20s CW / SpinInner 12s CCW，SceneryUI.cs）— DEV-23

---

## 深度扫描发现的视觉细节

- ✅ 玻璃态 UI（程序化噪点模拟磨砂玻璃，GlassPanelFX + GlassPanel.shader，应用于 CardDetailPopup / SpellShowcasePanel / AskPromptPanel）— DEV-25
- ✅ 装备标签样式（三徽章横排：▲绿色buff / ▲金色装备 / ▼红色debuff，卡牌底部悬浮，阴影+glow+悬停放大1.22×，右键弹出 tooltip）— DEV-25
- ✅ 传奇槽位光晕（LegendGlow 蓝色叠加层，5s 呼吸动画）— DEV-23（同三节）
- ✅ 中央分隔线能量球（DividerOrb 18px，3.5s 正弦振荡）— DEV-23（同三节）
- ✅ 角落宝石脉冲（CornerGem 4s alpha 脉冲）— DEV-23（同三节）
- ✅ 中心符文旋转（SigilOuter/Inner 双层旋转）— DEV-23（同三节）

---

## 十六、VFX 资产迁移视觉（TCG Engine → FWTCG，VFX-1 ~ VFX-8）

### VFX-1 — Shader 导入
- [ ] ShaderDissolve（溶解死亡效果，noise_fade 0→1 驱动）— VFX-1
- [ ] ShaderHolo（传奇/稀有卡全息光效）— VFX-1
- [ ] Grayscale（卡牌禁用/耗尽灰度态）— VFX-1

### VFX-2 — FX 粒子预制体
- [x] HitFX（命中爆炸粒子）— VFX-2
- [x] Flame（火焰粒子效果）— VFX-2
- [x] ElectricFX（闪电/眩晕粒子）— VFX-2
- [x] WaterFX（水元素粒子）— VFX-2
- [x] Leaf（植物/叶片粒子）— VFX-2
- [x] Shield（护盾/壁垒粒子）— VFX-2
- [x] Destroy/DestroyUI（死亡爆炸粒子）— VFX-2
- [x] Spawn/SpawnFire/SpawnForest/SpawnWater（召唤粒子）— VFX-2
- [x] Phoenix（凤凰死亡保护粒子）— VFX-2

### VFX-3 — 卡牌死亡溶解
- [ ] 单位阵亡时播放溶解动画（噪点溶解 0.6s，替代原缩放淡出）— VFX-3

### VFX-4 — VFXResolver 自动映射视觉
- [ ] 打出法术牌时自动触发对应元素 FX（RuneType 着色）— VFX-4
- [ ] 护盾/壁垒关键词单位显示 Shield prefab 常驻光效 — VFX-4
- [ ] 抽牌事件触发 Spawn 星光粒子 — VFX-4

### VFX-6 — 掷硬币翻转动画
- [ ] 硬币 scaleX 翻转动画（约 1.5s，正反面切换）— VFX-6
- [ ] 落定后金色粒子爆发 — VFX-6

### VFX-7 — UI 视觉迁移
- [ ] 卡牌金色边框叠加层（frame_gold.png，传奇/高稀有度）— VFX-7a
- [ ] 卡牌银色边框叠加层（frame_silver.png，普通稀有度）— VFX-7a
- [ ] 法力/符能离散图标条（IconBar，图标亮/暗替代纯文字）— VFX-7b
- [ ] 胜利屏光效 + 粒子爆发（win_glow + win_particles）— VFX-7c
- [ ] 失败屏灰色调 — VFX-7c
- [ ] 奖励数字滚动动画（MoveTowards 递增）— VFX-7c
- [ ] End Turn 按钮专用 sprite（button_endturn.png）— VFX-7d
- [ ] 手牌拖拽 ±10° 旋转（根据 deltaX 方向，Lerp 过渡）— VFX-7e
- [ ] 回合倒计时 <10s 脉冲（文字白→红 + 大小脉冲）— VFX-7f
- [ ] EventBanner 警告变体（红底白字 + EaseOutBack 弹入 + 1.5s 淡出）— VFX-7h
- [ ] 菜单背景替换为 bg_menu.png（替代 HexGrid shader 背景）— VFX-7i
