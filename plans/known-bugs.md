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
