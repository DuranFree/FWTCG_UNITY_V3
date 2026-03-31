# 功能清单 — FWTCG Unity 移植
> Phase 1 生成，每完成一项立即标记 ✅

---

## 一、核心游戏逻辑（engine.js + combat.js）

### 游戏状态
- ✅ G 全局状态对象（所有字段 1:1 移植为 C# GameState 类）— DEV-1
- ✅ 回合系统（awaken → start → summon → draw → action → end 六阶段）— DEV-1
- ✅ 回合倒计时（30秒，时间到自动结束玩家回合）— DEV-10
- ✅ 胜利判定（率先8分 / 平局）— DEV-1（规则633：纯得分制，无传奇HP胜负路线）
- ✅ 得分系统（据守+1 / 征服+1 / 燃尽惩罚+1）— DEV-1
- ❌ 最后1分受限规则 — 经用户确认此规则不存在，已从ScoreManager中删除 — DEV-1修正

### 战场系统
- ✅ 双战场区域（BF1 / BF2）— DEV-1
- ✅ 战场控制权（ctrl / conqDone）— DEV-1
- ❌ 战场单位槽位上限 — 经用户确认无上限，HasSlot始终返回true — DEV-1修正

### 回合流程
- ✅ 唤醒阶段（解除休眠、重置符文、清除眩晕）— DEV-1
- ✅ 开始阶段（据守得分判定 + 战场特殊能力触发）— DEV-1
- ✅ 召出阶段（从符文牌堆召出2张，后手首回合3张）— DEV-1
- ✅ 抽牌阶段（抽1张 + 燃尽检查 + 法力/符能清零）— DEV-1
- ✅ 行动阶段（玩家操作 / AI 操作）— DEV-1
- ✅ 结束阶段（眩晕清除 / 标记伤害清除 / 临时效果清除 / 时间扭曲检查）— DEV-1（基础清理）

### 单位状态
- ✅ atk = HP 规则（currentHp 和 currentAtk 始终同步）— DEV-1
- ✅ 增益指示物系统（buffToken，+1/+1，ResetEndOfTurn保留buff）— DEV-1字段+DEV-2/6/11使用
- ✅ 休眠（exhausted）/ 眩晕（stunned）状态 — DEV-1（基础字段）
- ✅ 临时战力加成（tb.atk，回合结束清零）— DEV-1（TempAtkBonus 字段已实现，ResetEndOfTurn 清零）
- ✅ 绝念触发系统（DeathwishSystem：alert_sentinel_die / wailing_poro_die）— DEV-2

### 战斗系统
- ✅ 单位移动（基地↔战场，多选→批量同时移动）— DEV-1 + 修正
- ✅ 战斗触发（移动造成争夺→自动触发法术对决，Rule 546）— DEV-1 规则修正
- ✅ 伤害分配（逐一分配，顺序吸收，未来加壁垒优先 Rule 626）— DEV-1 规则修正
- ✅ 强攻 / 坚守关键词（+1攻击力 / +1防御力，ComputeCombatPower）— DEV-6
- ❌ 溢出伤害检查（压制关键词击中传奇）— OUT OF SCOPE：传奇不可被摧毁（规则167.3），溢出伤害无处落点
- ✅ 战斗结果判定（双方全灭 / 攻方胜征服 / 守方胜 / 双方存活→攻方召回 Rule 627）— DEV-1 规则修正
- ✅ 战后清理（阵亡→废牌堆 + 全场所有单位HP重置 Rule 627.5）— DEV-1 规则修正
- ✅ 眩晕单位战力=0（不贡献伤害 Rule 743）— DEV-1 规则修正
- ✅ 征服得分条件（控制权须改变 + 每BF每回合一次 Rule 630-632）— DEV-1 规则修正
- ✅ 空战场征服（移动到空BF控制权改变→得征服分 Rule 546+630）— DEV-1 规则修正
- ✅ 最后一分规则（需征服2个BF才得最后1分，否则摸牌）— DEV-1 规则修正
- ✅ 召回行动（战场→基地，休眠，不算移动 Rule 616）— DEV-1 规则修正

### 法术对决系统（Spell Duel）
- ✅ 对决自动触发（移动造成争夺→即时战斗，替代手动按钮）— DEV-1 规则修正
- ✅ 反应/迅捷牌处理（AI法术触发玩家反应窗口，单轮响应）— DEV-4
- ✅ 反应窗口（ReactiveWindowUI，TaskCompletionSource 异步等待玩家选择）— DEV-4
- ✅ 对决结束 → 战斗结算（TriggerCombat + 回合结束兜底 ResolveAllBattlefields）— DEV-1

---

## 二、卡牌数据系统（cards.js → ScriptableObject）

### 数据结构
- ✅ CardData ScriptableObject（id/cardName/cost/atk/runeType/runeCost/description）— DEV-1（简化版，完整字段 DEV-2）
- ✅ 关键词枚举（Haste/Barrier/SpellShield/Inspire/Conquest/Deathwish/Reactive/StrongAtk/Roam/Foresight/Standby/Stun）— DEV-2
- ✅ 装备卡数据结构（附着机制：UnitInstance.AttachedEquipment/AttachedTo + TryPlayEquipment自动附着）— DEV-13
- ✅ 传奇卡数据结构（abilities数组：被动/触发/主动技能）— DEV-10（LegendInstance.DisplayData + kaisa_legend/yi_legend CardData）
- ✅ 英雄牌标记（hero: true，游戏开始时单独提取到英雄区，不进牌库）— DEV-10

### 卡组数据
- ✅ DEV-1 简化卡组（10张：卡莎×5 + 易大师×5，无特殊效果）— DEV-1
- ✅ 卡莎主卡组（19张唯一卡，含全部id/参数/关键词/effectId）— DEV-2
- ✅ 易大师主卡组（10张唯一卡，含单位+装备）— DEV-2
- ✅ 卡莎传奇卡（kaisa_legend，传奇区技能载体：虚空感知主动 + 进化被动）— DEV-10
- ✅ 易大师传奇卡（yi_legend，传奇区技能载体：独影剑鸣被动）— DEV-10

### 战场卡数据
- ✅ 19张战场卡数据（id/name/效果描述，GameRules.BF_DISPLAY_NAMES）— DEV-6
- ✅ 战场牌池分配（卡莎池3张 / 易大师池3张，GameRules.KAISA_BF_POOL/YI_BF_POOL）— DEV-6

### 符文系统
- ✅ 6种符文类型定义（blazing/radiant/verdant/crushing/chaos/order）— DEV-1
- ✅ 卡莎虚空符文牌堆（炽烈×7 + 灵光×5 = 12张）— DEV-1
- ✅ 易大师伊欧尼亚符文牌堆（翠意×6 + 摧破×6 = 12张）— DEV-1
- ✅ 符文横置获得法力（tap → G.pMana++）— DEV-1
- ✅ 符文回收获得符能（recycle → addSch）— DEV-11（右键回收已修复）
- ✅ 符能费用检查与扣除 — DEV-1
- [ ] 待确认符文操作队列（G.pendingRunes）
- ✅ 回合结束符能清零（resetSch）— DEV-1
- ✅ 符文区上限12张（MAX_RUNES_IN_PLAY=12，DoSummon 检查）— DEV-1 规则修正

---

## 三、法术效果系统（spell.js → SpellSystem.cs）

### 效果执行
- ✅ SpellSystem.cs 创建（applySpell 主入口，10张非反应法术）— DEV-3
- ✅ 费用检查（法力不足提示）— DEV-3
- ✅ 目标选择系统（SpellTargetType: None/EnemyUnit/FriendlyUnit/AnyUnit）— DEV-3
- ✅ 费用扣除（法力 + 从手牌移除 + 加入废牌堆）— DEV-3
- ✅ SpellTargetType.cs 枚举 — DEV-3
- ✅ CardData 扩展：_isSpell + _spellTargetType 字段 — DEV-3
- ✅ GameManager 法术目标选择流程（点击法术→选目标→结算）— DEV-3
- ✅ CardView 法术卡视觉区分（紫色背景 + "法"字标记）— DEV-3

### 卡莎卡组效果（21张法术 + 19张单位入场效果）
- ✅ swindle（反应，-1战力+抽牌，自动选第一个敌方单位）— DEV-4
- ✅ void_seek（4点伤害+抽牌）— DEV-3
- ✅ evolve_day（抽4张）— DEV-3
- ✅ retreat_rune（反应，召回战场单位+回收符文）— DEV-4
- ✅ furnace_blast（回响，1点伤害×3单位）— DEV-11
- ✅ guilty_pleasure（反应，弃牌造2点伤害）— DEV-4
- ✅ starburst（6点伤害×1目标，DEV-3简化）— DEV-3
- ✅ hex_ray（迅捷，3点伤害）— DEV-3
- ✅ time_warp（额外回合，extraTurnPending=true）— DEV-11
- ✅ stardrop（3点伤害×2次）— DEV-3
- ✅ smoke_bomb（反应，-4战力，自动选第一个敌方单位）— DEV-4
- ✅ divine_ray（回响+2炽烈，2点伤害×2次）— DEV-11
- ✅ akasi_storm（2点伤害×6次随机敌方）— DEV-3
- ✅ noxus_recruit 入场（鼓舞：下一个盟友+1战力）— DEV-11
- ✅ alert_sentinel 绝念（抽1张）— DEV-2
- ✅ yordel_instructor 入场（壁垒+抽牌）— DEV-2
- ✅ bad_poro 征服触发（征服时摸1张牌）— DEV-12
- ✅ rengar 入场（反应+强攻+1炽烈符能）— DEV-11
- ✅ kaisa_hero 入场（征服触发+1炽烈符能）— DEV-11
- ✅ darius 入场（本回合已出牌时+2战力）— DEV-2
- ✅ thousand_tail 入场（敌方单位战力-3，最低1）— DEV-2
- ✅ foresight_mech 预知（查看牌堆顶）— DEV-2（日志显示，无UI）

### 易大师卡组效果（22张法术 + 装备 + 单位入场）
- ✅ scoff（反应，无效化费用≤4法术）— DEV-4
- ✅ duel_stance（反应，+1/+1永久增益，DEV-4简化自动选目标）— DEV-4
- ✅ well_trained（反应，+2战力+抽牌，自动选第一个己方单位）— DEV-4
- ✅ wind_wall（反应，无效任意法术）— DEV-4
- ✅ rally_call（迅捷，单位活跃进场+抽牌）— DEV-3
- ✅ balance_resolve（抽牌+召出符文，条件费用-2推迟）— DEV-3
- ✅ flash_counter（反应，反制敌方法术）— DEV-4
- ✅ slam（回响，眩晕单位）— DEV-3
- ✅ strike_ask_later（+5战力，2摧破符能）— DEV-3
- ✅ yi_hero 入场（游走+急速+1摧破符能）— DEV-11
- ✅ jax 入场（法盾+入场效果）— DEV-2（日志显示）
- ✅ tiyana_warden 被动（阻止对手据守得分）— DEV-2
- ✅ wailing_poro 绝念（孤独阵亡时抽1张）— DEV-2
- ✅ zhonya（待命，死亡保护）— DEV-13（CardData已有Standby+Reactive关键词，待命机制基础实现）
- ✅ trinity_force（据守额外+1分，+2战力，1摧破符能）— DEV-11入场效果+DEV-13附着系统
- ✅ guardian_angel（死亡保护，+1战力，1翠意符能）— DEV-11入场效果+DEV-13附着系统
- ✅ dorans_blade（+2战力，1摧破符能）— DEV-11入场效果+DEV-13附着系统
- ✅ sandshoal_deserter（入场+法盾，法术无法选中）— DEV-11

---

## 四、UI 系统（ui.js + CSS → uGUI）

### 主界面布局
- ✅ 主战场布局（1920×1080基准，Scale With Screen Size，7×5网格）— DEV-9
- ✅ 双战场区域 UI（BF1 / BF2 + 控制标记）— DEV-9
- ✅ 玩家基地区域 UI — DEV-9
- ✅ 敌方基地区域 UI — DEV-9
- ✅ 手牌区域 UI（棋盘外全宽，敌方50px/玩家120px）— DEV-9
- ✅ 传奇区域 UI（玩家/敌方各一个，网格内定位，无HP，显示技能按钮）— DEV-9
- ✅ 英雄区域 UI（玩家/敌方各一个，英雄牌在此等待打出）— DEV-9
- ✅ 符文区域 UI（显示已召出符文）— DEV-9
- ✅ 得分显示 UI（得分轨道，9个圆圈0-8）— DEV-9
- ✅ 法力/符能显示 UI（信息条内）— DEV-9
- ✅ 回合/阶段显示 UI（行动面板内）— DEV-9
- ✅ 消息/日志区域 UI — DEV-9
- ✅ 结束回合按钮（行动面板内，含全部横置/取消/确认/跳过/结束）— DEV-9
- ✅ 弃牌堆 UI（双方各一个，显示计数）— DEV-9
- ✅ 放逐堆 UI（双方各一个，显示计数）— DEV-9
- ✅ 主牌堆计数显示 UI（双方各一个）— DEV-9
- ✅ 符文牌堆计数显示 UI（双方各一个）— DEV-9
- ✅ 战场控制标记 UI（绿=玩家/红=敌方）— DEV-9
- ✅ 信息条 UI（双方各一条：名字/法力/符文/牌堆）— DEV-9

### 卡牌显示
- [ ] 卡牌 Prefab（正面：图片/名称/费用/战力/关键词/文字）
- [ ] 卡牌背面 Prefab
- ✅ 传奇卡特殊显示（卡图+技能描述+等级标记，不显示atk/hp）— DEV-10
- [ ] 装备卡显示（附着在单位上）
- ✅ 卡牌状态视觉（休眠变暗 / 眩晕标记 / 增益指示物标记）— DEV-8
- ✅ 费用不足压暗提示（costInsufficient 手牌变暗）— DEV-8

### 卡牌交互
- ✅ 卡牌详情查看（右键弹出完整卡牌信息：图片/名称/费用/战力/关键词/效果文字/运行时状态）— DEV-8
- [ ] 悬停放大 + 发光效果
- ✅ 可出牌提示（发光边框 Shader：绿色动态粒子轨迹）— DEV-8
- ✅ 3D 倾斜效果（鼠标跟随 18°，CardTilt.cs）— DEV-8
- [ ] 拖拽出牌（dragAnim.js 移植：漩涡/传送门视觉效果）
- ✅ 点击出牌（OnUnitClicked → TryPlayCard，点击手牌直接打出）— DEV-1
- [ ] 目标选择高亮（合法目标 .targetable 绿色框 + 已选目标 .spell-targeted 红色框）
- [ ] 卡牌选中状态视觉（.selected 黄色高亮 + 轨道光环旋转）
- [ ] 悬停预判符文高亮（canPlayCard：法力+未横置符文>=费用时绿边提示）
- [ ] 拖拽释放时资源消耗提示（目标区域高亮 + 符文消耗预览）

### 战场交互
- ✅ 单位点击选中 / 取消选中 — DEV-1（OnUnitClicked 多选/单选切换）
- ✅ 战场点击目标（OnBattlefieldClicked 批量移动）— DEV-1
- ✅ 确认移动按钮（直接点击BF区域触发移动，无需额外确认）— DEV-1
- [ ] 战斗动画（单位冲向目标，伤害数字飘出）
- ✅ 战斗结算框 UI（CombatResultPanel：双方战力对比 + VS + 结果显示 + 3s自动关闭）— DEV-10
- [ ] 待命区域 UI（bf-1-standby / bf-2-standby，配合 zhonya 待命关键词）

### 特殊界面
- ✅ 掷硬币界面（显示先手结果 + 战场名 + OK按钮）— DEV-2（无动画，DEV-3添加）
- ✅ 战场随机选择（各方从牌池随机1个，在掷硬币界面显示）— DEV-2
- ✅ 梦想手牌调度界面（mulligan，选≤2张换牌，StartupFlowUI）— DEV-2
- ✅ 游戏结束界面（得分显示 + 全屏遮罩 + 消息文字）— DEV-9/DEV-12确认
- ✅ 横幅提示系统（ShowBanner 大横幅 + 2s自动隐藏协程）— DEV-9/DEV-12确认
- [ ] 对决界面（法术对决期间的特殊 UI）
- [ ] 吐司通知栈（toast-stack，右下角堆叠短期通知，自动消失）
- [ ] askPrompt 通用弹窗系统（目标选择弹窗 / 确认弹窗 / 卡片选择弹窗，Promise 异步）
- ✅ 弃牌堆点击浏览器（ShowDiscardViewer，网格展示所有弃牌，可点击查看详情）— DEV-10
- ✅ 放逐堆点击浏览器（ShowExileViewer，同上）— DEV-10

### 日志面板
- ✅ 日志折叠/展开按钮（LogToggleBtn，">" / "<" 切换）— DEV-10
- ✅ 日志折叠动画（协程 0.3s 宽度过渡 + SmoothStep）— DEV-10
- ✅ 日志折叠联动棋盘居中（折叠时 boardWrapperOuter 自动扩展）— DEV-10

### 符文交互
- [ ] 符文回收按钮（♻ 按钮在每张符文下方，点击回收）
- [ ] 装备激活能力按钮（activateEquipAbility，基地装备卡能力触发）

---

## 五、AI 系统（ai.js → AIController.cs）

- ✅ AI 回合启动（aiAction，700ms延迟）— DEV-7
- ✅ 局面评分（aiBoardScore：得分差×3 + 手牌差/2 + 控场×2 + 战力差/3）— DEV-7
- ✅ AI 出牌决策（法力+符能双重检查 / 法术优先级 / 智能目标选择）— DEV-7
- ✅ AI 移动多单位（循环移动所有非休眠基地单位）— DEV-1 规则修正
- ✅ AI 移动决策（基于局面评分：征服15 / 制胜+12 / 分差比较 / 绝境特殊逻辑）— DEV-7
- ✅ AI 符文操作（自动横置符文获取法力）— DEV-1
- ✅ AI 符文回收（回收低价值符文获取符能）— DEV-12
- ✅ AI 反应窗口处理（玩家施法时 AI 自动选择并打出反应牌，风墙>闪电反制>嘲讽>增益）— DEV-15
- ❌ AI 延迟链（_aiNextAction）— 新架构改用 async/await 循环，此机制不适用

---

## 六、传奇技能系统（legend.js → LegendSystem.cs）

### 卡莎传奇技能
- ✅ 主动技能：虚空感知（反应，休眠自身+1炽烈符能）— DEV-5
- ✅ 被动：进化条件检查（盟友4种关键词→升级+3/+3）— DEV-5
- ✅ 传奇升级动画（轻量版：OnLegendEvolved 事件 + GameUI 金色闪光协程，level: 1→2）— DEV-15
- ✅ 技能本回合使用限制（resetLegendAbilitiesForTurn）— DEV-5

### 易大师传奇技能
- ✅ 被动：独影剑鸣（该战场防守单位仅1名时+2战力）— DEV-5

### 通用传奇机制
- ✅ 传奇不占基地/战场槽位（独立传奇区，规则167.4）— DEV-5
- ✅ 英雄牌单独提取 + 英雄区域（游戏开始时从牌库分离，规则103.2.a）— DEV-10
- ✅ checkLegendPassives 战后触发（TriggerCombat 后 CheckLegendDeaths）— DEV-5
- ✅ triggerLegendEvent 事件系统（LegendSystem.OnLegendLog 静态事件）— DEV-5
- ✅ AI 传奇技能决策（卡莎虚空感知：手牌有炽烈法术且符能不足时触发）— DEV-7

---

## 七、音效系统（sound.js → AudioManager）

- ✅ 背景音乐（BGM 循环播放，AudioManager.PlayBGM）— DEV-14（框架+需音频资源）
- ✅ 出牌音效（AudioManager.PlayCardPlay）— DEV-14（框架+需音频资源）
- ✅ 法术施放音效（AudioManager.PlaySpellCast）— DEV-14（框架+需音频资源）
- ✅ 战斗冲击音效（AudioManager.PlayCombatHit）— DEV-14（框架+需音频资源）
- ✅ 单位死亡音效（AudioManager.PlayUnitDeath）— DEV-14（框架+需音频资源）
- ✅ 回合结束音效（AudioManager.PlayTurnEnd）— DEV-14（框架+需音频资源）
- ✅ 游戏结束音效（AudioManager.PlayGameOverWin/Lose）— DEV-14（框架+需音频资源）
- ✅ UI 点击音效（AudioManager.PlayUIClick）— DEV-14（框架+需音频资源）
- ✅ 音量控制（AudioManager.SetBGMVolume/SetSFXVolume）— DEV-14

---

## 八、粒子特效（particles.js → VFX）

- [ ] 伤害数字飘出（受击时显示伤害值）
- [ ] 单位阵亡特效
- [ ] 法术施放特效
- [ ] 征服/得分特效
- [ ] 对决横幅动画（showDuelBanner）

---

## 九、游戏启动流程（main.js → GameBootstrap.cs）

- ✅ 随机阵营分配（50%卡莎先手/易大师先手）— DEV-1
- ✅ 卡组初始化（含英雄卡单独提取）— DEV-1 BuildDeck+DealInitialHand + DEV-10 ExtractHero + DEV-14 SeedOpeningHand
- ✅ 软加权开局手牌（seedPlayerOpeningHand，67%触发，抽≤2费单位）— DEV-14
- ✅ 符文牌堆初始化 — DEV-1
- ✅ 传奇初始化（InitGame 创建 PLegend=卡莎 + ELegend=易大师）— DEV-5
- ✅ 初始手牌抽取（各4张）— DEV-1
- ✅ 掷硬币先手决定（StartupFlowUI 显示结果）— DEV-2
- ✅ 战场随机选择（各方从己方牌池抽1个，GameRules.PickBattlefield）— DEV-2
- ✅ 梦想手牌调度（StartupFlowUI，选≤2张换牌）— DEV-2

---

## 十、战场卡牌特殊能力（19张）

- ✅ altar_unity — 据守：召唤1/1新兵 — DEV-6
- ✅ aspirant_climb — 据守：支付1法力，基地单位+1战力 — DEV-6
- ✅ back_alley_bar — 被动：移动离开时+1战力 — DEV-6
- ✅ bandle_tree — 据守：场上≥3种特性+1法力 — DEV-6
- ✅ hirana — 征服：消耗增益指示物抽1牌 — DEV-6
- ✅ reaver_row — 征服：从废牌堆捞费用≤2单位 — DEV-6
- ✅ reckoner_arena — 被动：战力≥5自动获得强攻/坚守 — DEV-6
- ✅ dreaming_tree — 被动：每回合首次法术抽1牌 — DEV-6
- ✅ vile_throat_nest — 被动：此处单位禁止撤回基地 — DEV-6
- ✅ rockfall_path — 被动：禁止直接出牌到此战场 — DEV-6
- ✅ sunken_temple — 防守失败触发：支付2法力抽1牌 — DEV-6
- ✅ trifarian_warcamp — 入场触发：获得增益指示物 — DEV-6
- ✅ void_gate — 被动：法术伤害额外+1 — DEV-6
- ✅ zaun_undercity — 征服：弃1牌抽1牌 — DEV-6
- ✅ strength_obelisk — 据守：额外召出1张符文 — DEV-6
- ✅ star_peak — 据守：召出1枚休眠符文 — DEV-6
- ✅ thunder_rune — 征服：回收1张符文 — DEV-6
- ✅ ascending_stairs — 被动：据守/征服时额外+1分 — DEV-6
- ✅ forgotten_monument — 被动：第三回合前无据守分 — DEV-6

---

## 深度扫描发现（移植必须覆盖）

### 硬编码数值（已在代码中确认）
- ✅ WIN_SCORE = 8 — GameRules.WIN_SCORE（DEV-1已定义）
- ❌ 手牌上限 = 7 — 经用户确认无手牌上限，此规则不存在
- ❌ 战场槽位 = 2 — 经用户确认无槽位上限
- ✅ 强力判定 atk >= 5 — GameRules.STRONG_POWER_THRESHOLD（DEV-6已定义）
- ✅ 回合倒计时 = 30秒 — GameRules.TURN_TIMER_SECONDS（DEV-10实现）
- ✅ 后手首回合符文 = 3（之后每回合2）— GameRules.RUNES_FIRST_TURN_SECOND（DEV-1已定义）
- ✅ 初始手牌 = 4张 — GameRules.INITIAL_HAND_SIZE（DEV-1已定义）
- ✅ 燃尽惩罚 = +1分 — ScoreManager.AddScore BURNOUT（DEV-1实现）
- ✅ 软加权触发率 = 67% — DEV-14 SeedOpeningHand Random.value <= 0.67f
- ❌ 最后1分受限规则 — 不存在，已删除

### 关键移植陷阱
- ✅ atk = HP 同步规则（currentHp = currentAtk，dealDamage 是唯一入口）— UnitInstance构造+ResetEndOfTurn（DEV-1）
- [ ] 拖拽覆写：Unity 直接实现 dragAnim.js 逻辑，不参考 spell.js 中的 startDrag
- ✅ 异步交互锁（prompting 标记）→ async/await + TaskCompletionSource 实现 — DEV-4
- ✅ 反应窗口系统（reactionWindowOpen 冻结 AI 行动）— ReactiveWindowUI + WaitIfReactionActive（DEV-4）
- [ ] 符文操作待确认队列（pendingRunes，需维持中间状态）
