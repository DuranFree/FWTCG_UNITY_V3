# 功能清单 — FWTCG Unity 移植
> Phase 1 生成，每完成一项立即标记 ✅

---

## 一、核心游戏逻辑（engine.js + combat.js）

### 游戏状态
- ✅ G 全局状态对象（所有字段 1:1 移植为 C# GameState 类）— DEV-1
- ✅ 回合系统（awaken → start → summon → draw → action → end 六阶段）— DEV-1
- [ ] 回合倒计时（30秒，时间到自动结束玩家回合）
- ✅ 胜利判定（率先8分 / 传奇HP归零 / 平局）— DEV-1（基础分数判定）
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
- [ ] 增益指示物系统（buffToken，+1/+1，不可叠加）
- ✅ 休眠（exhausted）/ 眩晕（stunned）状态 — DEV-1（基础字段）
- [ ] 临时战力加成（tb.atk，回合结束清零）
- ✅ 绝念触发系统（DeathwishSystem：alert_sentinel_die / wailing_poro_die）— DEV-2

### 战斗系统
- ✅ 单位移动（基地↔战场，多选→批量同时移动）— DEV-1 + 修正
- ✅ 战斗触发（移动造成争夺→自动触发法术对决，Rule 546）— DEV-1 规则修正
- ✅ 伤害分配（逐一分配，顺序吸收，未来加壁垒优先 Rule 626）— DEV-1 规则修正
- [ ] 强攻 / 坚守关键词（+1攻击力 / +1防御力，可配置）
- [ ] 溢出伤害检查（压制关键词击中传奇）
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
- [ ] 装备卡数据结构（附着机制）
- [ ] 传奇卡数据结构（独立HP、技能系统）

### 卡组数据
- ✅ DEV-1 简化卡组（10张：卡莎×5 + 易大师×5，无特殊效果）— DEV-1
- ✅ 卡莎主卡组（19张唯一卡，含全部id/参数/关键词/effectId）— DEV-2
- ✅ 易大师主卡组（10张唯一卡，含单位+装备）— DEV-2
- [ ] 卡莎传奇卡（kaisa，cost:5/atk:5/hp:14）
- [ ] 易大师传奇卡（masteryi，cost:5/atk:5/hp:12）

### 战场卡数据
- [ ] 19张战场卡数据（id/name/效果描述）
- [ ] 战场牌池分配（卡莎池3张 / 易大师池3张）

### 符文系统
- ✅ 6种符文类型定义（blazing/radiant/verdant/crushing/chaos/order）— DEV-1
- ✅ 卡莎虚空符文牌堆（炽烈×7 + 灵光×5 = 12张）— DEV-1
- ✅ 易大师伊欧尼亚符文牌堆（翠意×6 + 摧破×6 = 12张）— DEV-1
- ✅ 符文横置获得法力（tap → G.pMana++）— DEV-1
- [ ] 符文回收获得符能（recycle → addSch）
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
- [ ] furnace_blast（回响，1点伤害×3单位）
- ✅ guilty_pleasure（反应，弃牌造2点伤害）— DEV-4
- ✅ starburst（6点伤害×1目标，DEV-3简化）— DEV-3
- ✅ hex_ray（迅捷，3点伤害）— DEV-3
- [ ] time_warp（额外回合，extraTurnPending=true）
- ✅ stardrop（3点伤害×2次）— DEV-3
- ✅ smoke_bomb（反应，-4战力，自动选第一个敌方单位）— DEV-4
- [ ] divine_ray（回响+2炽烈，2点伤害×2次）
- ✅ akasi_storm（2点伤害×6次随机敌方）— DEV-3
- [ ] noxus_recruit 入场（鼓舞：下一个盟友+1战力）
- ✅ alert_sentinel 绝念（抽1张）— DEV-2
- ✅ yordel_instructor 入场（壁垒+抽牌）— DEV-2
- [ ] bad_poro 征服触发
- [ ] rengar 入场（反应+强攻+1炽烈符能）
- [ ] kaisa_hero 入场（征服触发+1炽烈符能）
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
- [ ] yi_hero 入场（游走+急速+1摧破符能）
- ✅ jax 入场（法盾+入场效果）— DEV-2（日志显示）
- ✅ tiyana_warden 被动（阻止对手据守得分）— DEV-2
- ✅ wailing_poro 绝念（孤独阵亡时抽1张）— DEV-2
- [ ] zhonya（待命，死亡保护）
- [ ] trinity_force（据守额外+1分，+2战力，1摧破符能）
- [ ] guardian_angel（死亡保护，+1战力，1翠意符能）
- [ ] dorans_blade（+2战力，1摧破符能）
- [ ] sandshoal_deserter（入场+法盾，法术无法选中）

---

## 四、UI 系统（ui.js + CSS → uGUI）

### 主界面布局
- [ ] 主战场布局（1920×1080基准，Scale With Screen Size，支持4K）
- [ ] 双战场区域 UI（BF1 / BF2）
- [ ] 玩家基地区域 UI
- [ ] 敌方基地区域 UI
- [ ] 手牌区域 UI（最多7张）
- [ ] 传奇区域 UI（玩家/敌方各一个）
- [ ] 符文区域 UI（显示已召出符文）
- [ ] 得分显示 UI（当前分数/8）
- [ ] 法力/符能显示 UI
- [ ] 回合/阶段显示 UI
- [ ] 消息/日志区域 UI
- [ ] 结束回合按钮

### 卡牌显示
- [ ] 卡牌 Prefab（正面：图片/名称/费用/战力/关键词/文字）
- [ ] 卡牌背面 Prefab
- [ ] 传奇卡特殊显示（HP 独立显示）
- [ ] 装备卡显示（附着在单位上）
- [ ] 卡牌状态视觉（休眠变暗 / 眩晕标记 / 增益指示物标记）
- [ ] 费用不足红色高亮（selFailUid）

### 卡牌交互
- [ ] 悬停放大 + 发光效果
- [ ] 可出牌提示（轻微上移 + 发光边框）
- [ ] 3D 倾斜效果（拖拽时 3D rotation，原 3d-tilt.js）
- [ ] 拖拽出牌（dragAnim.js 移植：漩涡/传送门视觉效果）
- [ ] 点击出牌（备选交互方式）
- [ ] 目标选择高亮（合法目标闪烁/高亮）

### 战场交互
- [ ] 单位点击选中 / 取消选中
- [ ] 战场点击目标（触发 pendingMove）
- [ ] 确认移动按钮
- [ ] 战斗动画（单位冲向目标，伤害数字飘出）

### 特殊界面
- ✅ 掷硬币界面（显示先手结果 + 战场名 + OK按钮）— DEV-2（无动画，DEV-3添加）
- ✅ 战场随机选择（各方从牌池随机1个，在掷硬币界面显示）— DEV-2
- ✅ 梦想手牌调度界面（mulligan，选≤2张换牌，StartupFlowUI）— DEV-2
- [ ] 游戏结束界面（得分显示 + 再来一局按钮）
- [ ] 横幅提示（showBanner：回合开始/得分事件）
- [ ] 对决界面（法术对决期间的特殊 UI）

---

## 五、AI 系统（ai.js → AIController.cs）

- [ ] AI 回合启动（aiAction，700ms延迟）
- [ ] 局面评分（aiBoardScore）
- [ ] AI 出牌决策（aiPlayCard：法力/符能/目标选择）
- ✅ AI 移动多单位（循环移动所有非休眠基地单位）— DEV-1 规则修正
- [ ] AI 符文操作（自动横置/回收）
- [ ] AI 反应窗口处理（对决期间AI响应迅捷/反应牌）
- [ ] AI 延迟链（_aiNextAction，等待玩家响应后继续）

---

## 六、传奇技能系统（legend.js → LegendSystem.cs）

### 卡莎传奇技能
- [ ] 主动技能：虚空感知（反应，休眠自身+1符能）
- [ ] 被动：进化条件检查（盟友4种关键词→升级+3/+3）
- [ ] 传奇升级动画（level: 1→2，+3/+3）
- [ ] 技能本回合使用限制（resetLegendAbilitiesForTurn）

### 易大师传奇技能
- [ ] 被动：孤注一掷（防守单位仅1名时+2战力）
- [ ] 传奇HP独立管理（不受 atk=HP 规则约束）

### 通用传奇机制
- [ ] 传奇HP归零游戏结束
- [ ] 传奇不占基地槽位（独立英雄区）
- [ ] checkLegendPassives 战后触发
- [ ] triggerLegendEvent 事件系统

---

## 七、粒子特效（particles.js → VFX）

- [ ] 伤害数字飘出（受击时显示伤害值）
- [ ] 单位阵亡特效
- [ ] 法术施放特效
- [ ] 征服/得分特效
- [ ] 对决横幅动画（showDuelBanner）

---

## 八、游戏启动流程（main.js → GameBootstrap.cs）

- ✅ 随机阵营分配（50%卡莎先手/易大师先手）— DEV-1
- [ ] 卡组初始化（含英雄卡单独提取）
- [ ] 软加权开局手牌（seedPlayerOpeningHand，67%触发，抽≤2费单位）
- ✅ 符文牌堆初始化 — DEV-1
- [ ] 传奇初始化
- ✅ 初始手牌抽取（各4张）— DEV-1
- ✅ 掷硬币先手决定（StartupFlowUI 显示结果）— DEV-2
- ✅ 战场随机选择（各方从己方牌池抽1个，GameRules.PickBattlefield）— DEV-2
- ✅ 梦想手牌调度（StartupFlowUI，选≤2张换牌）— DEV-2

---

## 九、战场卡牌特殊能力（19张）

- [ ] altar_unity — 据守：召唤1/1新兵
- [ ] aspirant_climb — 据守：支付1法力，基地单位+1战力
- [ ] back_alley_bar — 被动：移动离开时+1战力
- [ ] bandle_tree — 据守：场上≥3种特性+1法力
- [ ] hirana — 征服：消耗增益指示物抽1牌
- [ ] reaver_row — 征服：从废牌堆捞费用≤2单位
- [ ] reckoner_arena — 被动：战力≥5自动获得强攻/坚守
- [ ] dreaming_tree — 被动：每回合首次法术抽1牌
- [ ] vile_throat_nest — 被动：此处单位禁止撤回基地
- [ ] rockfall_path — 被动：禁止直接出牌到此战场
- [ ] sunken_temple — 防守失败触发：支付2法力抽1牌
- [ ] trifarian_warcamp — 入场触发：获得增益指示物
- [ ] void_gate — 被动：法术伤害额外+1
- [ ] zaun_undercity — 征服：弃1牌抽1牌
- [ ] strength_obelisk — 据守：额外召出1张符文
- [ ] star_peak — 据守：召出1枚休眠符文（玩家选择）
- [ ] thunder_rune — 征服：回收1张符文
- [ ] ascending_stairs — 被动：据守时额外+1分
- [ ] forgotten_monument — 被动：第三回合前无据守分

---

## 深度扫描发现（移植必须覆盖）

### 硬编码数值（已在代码中确认）
- [ ] WIN_SCORE = 8
- ❌ 手牌上限 = 7 — 经用户确认无手牌上限，此规则不存在
- ❌ 战场槽位 = 2 — 经用户确认无槽位上限
- [ ] 强力判定 atk >= 5
- [ ] 回合倒计时 = 30秒
- [ ] 后手首回合符文 = 3（之后每回合2）
- [ ] 初始手牌 = 4张
- [ ] 燃尽惩罚 = +1分
- [ ] 软加权触发率 = 67%
- ❌ 最后1分受限规则 — 不存在，已删除

### 关键移植陷阱
- [ ] atk = HP 同步规则（currentHp = currentAtk，dealDamage 是唯一入口）
- [ ] 拖拽覆写：Unity 直接实现 dragAnim.js 逻辑，不参考 spell.js 中的 startDrag
- [ ] 异步交互锁（prompting 标记）→ Unity 协程/async 实现
- [ ] 反应窗口系统（reactionWindowOpen 冻结 AI 行动）
- [ ] 符文操作待确认队列（pendingRunes，需维持中间状态）
