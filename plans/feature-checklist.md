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
- ✅ 最后1分受限规则（得第8分须本回合主动征服所有战场）— DEV-1

### 战场系统
- ✅ 双战场区域（BF1 / BF2）— DEV-1
- ✅ 战场控制权（ctrl / conqDone）— DEV-1
- ✅ 战场单位槽位上限（每方最多2名单位）— DEV-1

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
- [ ] 绝念触发系统（deathwish：voidling / void_sentinel / alert_sentinel / wailing_poro）

### 战斗系统
- ✅ 单位移动（基地↔战场，含多单位同时移动）— DEV-1
- ✅ 战斗触发（移动到有敌方单位的战场时）— DEV-1
- ✅ 伤害分配（优先壁垒单位）— DEV-1（基础伤害）
- [ ] 强攻 / 坚守关键词（+1攻击力 / +1防御力，可配置）
- [ ] 溢出伤害检查（压制关键词击中传奇）
- ✅ 战斗结果判定（双方全灭 / 进攻方存活征服 / 防守方存活 / 双方存活）— DEV-1
- ✅ 战后清理（cleanDead + 战场控制权更新）— DEV-1

### 法术对决系统（Spell Duel）
- [ ] 对决启动（移动到有敌方单位战场时）
- [ ] 反应/迅捷牌处理（对决期间轮流响应）
- [ ] 反应窗口（reactionWindowOpen，冻结AI等待玩家响应）
- [ ] 对决结束 → 战斗结算

---

## 二、卡牌数据系统（cards.js → ScriptableObject）

### 数据结构
- ✅ CardData ScriptableObject（id/cardName/cost/atk/runeType/runeCost/description）— DEV-1（简化版，完整字段 DEV-2）
- [ ] 关键词枚举（迅捷/迅捷攻击/急速/反应/回响/壁垒/法盾/强攻/坚守/游走/鼓舞/征服等）
- [ ] 装备卡数据结构（附着机制）
- [ ] 传奇卡数据结构（独立HP、技能系统）

### 卡组数据
- ✅ DEV-1 简化卡组（10张：卡莎×5 + 易大师×5，无特殊效果）— DEV-1
- [ ] 卡莎主卡组（40张，含所有id和参数）
- [ ] 易大师主卡组（30张，含所有id和参数）
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

---

## 三、法术效果系统（spell.js → SpellSystem.cs）

### 效果执行
- [ ] applySpell 主入口（法术/入场效果分发）
- [ ] 费用检查（getCannotPlayReasons：法力/符能/锁定/目标）
- [ ] 目标选择系统（getSpellTargets：全场/单体/友方/敌方等）
- [ ] 费用扣除（法力 + 符能 + 从手牌移除 + 加入废牌堆）

### 卡莎卡组效果（21张法术 + 19张单位入场效果）
- [ ] swindle（反应，-1战力+抽牌）
- [ ] void_seek（4点伤害+抽牌）
- [ ] evolve_day（抽4张）
- [ ] retreat_rune（回收单位+符文）
- [ ] furnace_blast（回响，1点伤害×3单位）
- [ ] guilty_pleasure（反应，弃牌造伤）
- [ ] starburst（6点伤害×2目标）
- [ ] hex_ray（迅捷，3点伤害）
- [ ] time_warp（额外回合，extraTurnPending=true）
- [ ] stardrop（3点伤害×2次）
- [ ] smoke_bomb（反应，-4战力）
- [ ] divine_ray（回响+2炽烈，2点伤害×2次）
- [ ] akasi_storm（2点伤害×6次）
- [ ] noxus_recruit 入场（鼓舞：下一个盟友+1战力）
- [ ] alert_sentinel 绝念（抽1张）
- [ ] yordel_instructor 入场（壁垒+抽牌）
- [ ] bad_poro 征服触发
- [ ] rengar 入场（反应+强攻+1炽烈符能）
- [ ] kaisa_hero 入场（征服触发+1炽烈符能）
- [ ] darius 入场（本回合已出牌时+2战力）
- [ ] thousand_tail 入场（敌方单位战力-3，最低1）
- [ ] foresight_mech 预知（抽牌前查看牌堆顶，可回底）

### 易大师卡组效果（22张法术 + 装备 + 单位入场）
- [ ] scoff（反应，无效化费用≤4法术）
- [ ] duel_stance（反应，+1战力，单独防守额外+1）
- [ ] well_trained（反应，+2战力+抽牌）
- [ ] wind_wall（反应，无效任意法术）
- [ ] rally_call（迅捷，单位活跃进场+抽牌，pRallyActive）
- [ ] balance_resolve（对手距胜利≤3时费用-2+抽牌+符文）
- [ ] flash_counter（反应，反制敌方法术）
- [ ] slam（回响，眩晕单位）
- [ ] strike_ask_later（+5战力，2摧破符能）
- [ ] yi_hero 入场（游走+急速+1摧破符能）
- [ ] jax 入场（法盾+1翠意符能）
- [ ] tiyana_warden 被动（阻止对手据守/征服得分）
- [ ] wailing_poro 绝念（孤独阵亡时抽1张）
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
- [ ] 掷硬币界面（硬币动画1800ms，正/反面）
- [ ] 战场选择界面（showBFSelect）
- [ ] 梦想手牌调度界面（mulligan，选≤2张换牌）
- [ ] 游戏结束界面（得分显示 + 再来一局按钮）
- [ ] 横幅提示（showBanner：回合开始/得分事件）
- [ ] 对决界面（法术对决期间的特殊 UI）

---

## 五、AI 系统（ai.js → AIController.cs）

- [ ] AI 回合启动（aiAction，700ms延迟）
- [ ] 局面评分（aiBoardScore）
- [ ] AI 出牌决策（aiPlayCard：法力/符能/目标选择）
- [ ] AI 移动单位决策（aiMoveUnit）
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

- [ ] 随机阵营分配（50%卡莎先手/易大师先手）
- [ ] 卡组初始化（含英雄卡单独提取）
- [ ] 软加权开局手牌（seedPlayerOpeningHand，67%触发，抽≤2费单位）
- [ ] 符文牌堆初始化
- [ ] 传奇初始化
- [ ] 初始手牌抽取（各4张）
- [ ] 掷硬币先手决定
- [ ] 战场随机选择（各方从己方牌池抽1个）
- [ ] 梦想手牌调度（换牌系统）

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
- [ ] 手牌上限 = 7
- [ ] 战场槽位 = 2
- [ ] 强力判定 atk >= 5
- [ ] 回合倒计时 = 30秒
- [ ] 后手首回合符文 = 3（之后每回合2）
- [ ] 初始手牌 = 4张
- [ ] 燃尽惩罚 = +1分
- [ ] 软加权触发率 = 67%
- [ ] 最后1分受限规则

### 关键移植陷阱
- [ ] atk = HP 同步规则（currentHp = currentAtk，dealDamage 是唯一入口）
- [ ] 拖拽覆写：Unity 直接实现 dragAnim.js 逻辑，不参考 spell.js 中的 startDrag
- [ ] 异步交互锁（prompting 标记）→ Unity 协程/async 实现
- [ ] 反应窗口系统（reactionWindowOpen 冻结 AI 行动）
- [ ] 符文操作待确认队列（pendingRunes，需维持中间状态）
