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
- [ ] 蓝色魔法光效色 rgba(60,140,255) / rgba(96,165,250)（漩涡/法术）
- ✅ 符文高亮预览色（蓝 rgb(64,140,255) = 待横置 / 红 rgb(255,64,64) = 待回收，悬停手牌时覆盖符文圆圈颜色）— DEV-20

---

## 二、字体系统

- [ ] Cinzel 字体导入（标题/UI标签，weight 400/600/700/900）
- [ ] Noto Sans SC 字体导入（卡牌文字/正文，weight 400/500/700）
- [ ] 字体大小层级定义（标题3rem / 按钮1rem / 卡牌名0.76rem / 卡牌文字0.36rem 等）

---

## 三、背景与环境

- ✅ 深海军蓝主背景（#010a13，带色调，非纯黑）— DEV-8
- ✅ 六边形网格纹理叠加（HexGrid.shader 手写 HLSL）— DEV-8
- ✅ 噪点纹理叠加（HexGrid.shader 程序化 hash 噪点）— DEV-8
- ✅ 分层渐变背景（中心青色光晕 + 暗角，HexGrid.shader）— DEV-8
- ✅ 边角暗角 Vignette（URP Post Processing + HexGrid.shader）— DEV-8
- ✅ URP Post Processing Volume 配置（Bloom + Color Adjustments + Vignette + Film Grain）— DEV-8

---

## 四、卡牌视觉

- [ ] 卡牌 Prefab 基础外观（边框/背景/图片层/文字层）
- [ ] 悬停放大 + 发光效果（DOTween scale + 发光边框 — DEV-9 scale 动画）
- [ ] 悬停上浮效果（margin-top -20px + z-index 提升，手牌卡片浮起）
- ✅ 3D 倾斜效果（鼠标跟随，18° 最大，CardTilt.cs）— DEV-8
- ✅ 全息光泽 Shine（鼠标位置驱动的径向渐变，CardShine.shader）— DEV-8
- ✅ 可出牌粒子边框（conic rotation 彗星效果，CardGlow.shader）— DEV-8
- [ ] 入场 Foil Sweep（0.8s 对角光扫，DOTween + Shader）
- [ ] 手牌卡片入场动画（hand-card-enter 0.42s，translateY+scale 淡入）
- [ ] 可出牌卡粒子特效（playable-particle 3s，上方闪烁光点）
- [ ] 卡牌选中轨道光环（selected-arcane-orbit 6s，小光环绕卡片旋转）
- [ ] 死亡飞行动画（playDeathFly，阵亡卡飞向弃牌堆消散）
- ✅ 费用不足变暗（整体压暗 ×0.6，CardView.SetCostInsufficient）— DEV-8
- ✅ 休眠状态（灰化 + CardExhausted 颜色）— DEV-8
- ✅ 眩晕状态（红色脉冲叠加层，StunPulseRoutine 协程）— DEV-8
- ✅ 增益指示物（+1/+1 金色图标，右上角叠加）— DEV-8
- [ ] 卡牌背面样式
- [ ] 英雄光环（hero-card-aura 4s 缓慢脉冲，英雄卡专属）

---

## 五、拖拽视觉系统

- [ ] 拖拽时卡牌跟随鼠标（Ghost 克隆）
- [ ] 漩涡/传送门特效（3 层同心圆，CW/CCW 旋转，发光）
- [ ] 漩涡螺旋粒子（10 个轨道粒子，蓝色发光）
- [ ] 漩涡淡入（0.28s cubic-bezier）/ 淡出（0.22s ease-in）
- [ ] 法术牌放置动画（飞向漩涡 0.4s → 停留发光 0.5s → 爆裂消散 0.4s）
- [ ] 爆裂粒子（16 个放射状粒子）
- [ ] 单位牌放置动画（飞行 0.35s → 悬浮弹跳 0.4s → 落地 0.18s）
- [ ] 落地涟漪（land ripple expand 0.55s）
- [ ] 落地震动（±2px，0.25s）
- [ ] 拖拽失败弹回（0.45s elastic overshoot 回到手牌）

---

## 六、战场视觉

- ✅ 战场区域环境呼吸动画（bf-ambient-breathe，5s）— DEV-18（BattlefieldGlow.AmbientBreatheLoop）
- ✅ 玩家控制战场绿色光晕（bf-ctrl-player-glow 3s 交替脉冲）— DEV-18（BattlefieldGlow.CtrlGlowLoop 绿色）
- ✅ 敌方控制战场红色光晕（bf-ctrl-enemy-glow 3s 交替脉冲）— DEV-18（BattlefieldGlow.CtrlGlowLoop 红色）
- ✅ 战场卡牌展示（战场特殊卡的图片显示）— DEV-18（GameUI.UpdateBFCardArt Resources.Load）
- [ ] 战斗触发特效（单位冲向对方 + 碰撞闪光）
- ✅ 战斗冲击波（playCombatShockwave，中央扩散金色光环）— DEV-18（CombatAnimator.PlayShockwave 0.45s scale 0→1.5）
- ✅ 征服/对决横幅动画（Duel Banner，FireDuelBanner → ShowBanner slide 动画）— DEV-19
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

- [ ] 标题文字动画（fadeInUp + ScaleIn + SlideFromLeft/Right）
- [ ] 六边形呼吸光效（hexBreath，50% 透明度脉冲）
- [ ] 标题中心光束脉冲（titleBeamPulse，无限循环）
- [ ] 按钮入场动画（titleBtnGlow，1s）
- [ ] 分层背景光效（conic/radial gradients + noise）
- [ ] 信息栏扫光（istrip-scan，8s）

---

## 十、掷硬币界面

- [ ] 硬币正面图片（xianshou.png → Sprite）
- [ ] 硬币背面图片（houshou.png → Sprite）
- [ ] 翻转动画（1800ms，正面/背面方向）
- [ ] 结果文字淡入

---

## 十一、梦想手牌界面

- [ ] 换牌选中状态（选中卡牌视觉变化）
- [ ] 确认按钮动画
- [ ] 界面进场/退场过渡

---

## 十二、过渡与切换效果

- [ ] 界面淡入淡出（0.3-0.5s，所有界面切换）
- [ ] 全屏 Banner 进场/退场
- [ ] 游戏结束界面淡入

---

## 十三、日志面板视觉

- [ ] 日志折叠/展开按钮动画（">" ↔ "<" 旋转过渡）
- [ ] 日志折叠联动棋盘居中（折叠时主区域自动扩展居中，展开时靠左）
- [ ] 日志条目进入闪烁（log-entry-flash 0.8s，背景金色闪烁后恢复）

---

## 十四、区域标识视觉

- ✅ 区域边框画线（所有游戏区域可见边框，Outline 组件 + 金色半透明）— DEV-10
- ✅ 区域名字标签（BASE/LEGEND/HERO/TRASH/EXILE/RUNES，9px 金色半透明）— DEV-10
- [ ] 符文回收按钮样式（♻ 按钮入场动画 rune-recycle-appear 0.22s）

---

## 十五、传奇/英雄特殊视觉

- ✅ 传奇升级视觉（金色闪烁4次协程，FlashLegendText，0.15s × 4 脉冲）— DEV-15
- ✅ 法术施放展示面板（SpellShowcaseUI：底部飞入+停留+上飞，卡名/效果/归属/卡图）— DEV-16
- [ ] 中央 SVG 旋转装饰（spin-slow 20s 顺时针 + spin-reverse 12s 逆时针）

---

## 深度扫描发现的视觉细节

- [ ] 玻璃态 UI（backdrop-filter blur 20px + saturate 1.2 → URP Full Screen Blur Pass）
- [ ] 装备标签样式（18px高，青/红边框，滑入动画 equipTabSlideIn 0.22s）
- [ ] 传奇槽位光晕（legend-arcane-ring，5s 内发光呼吸）
- [ ] 中央分隔线能量球（divider-energy，3.5s 下移动画）
- [ ] 角落宝石脉冲（corner-gem-pulse，4s L形括号闪烁）
- [ ] 中心符文旋转（sigil-rotate，30s + 20s 反向双层）
