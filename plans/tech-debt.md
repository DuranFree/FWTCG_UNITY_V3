# 技术债清单
> 发现时追加，解决后删除
> 格式：- [ ] <描述> — 原因：<why deferred> — Phase <number>

- ✅ TMP Essential Resources 问题 — 已解决：全项目切换至 Legacy UnityEngine.UI.Text，TMP 彻底废弃 — Phase DEV-1
- [ ] CardData ScriptableObject 字段简化 — DEV-1 仅包含 id/cardName/cost/atk/runeType/runeCost/description，完整字段（keywords/effect/description_full等）等 DEV-2 扩展 — Phase DEV-1
- [ ] UI 引用在 batch mode 下通过 GameObject.Find 连线，运行时若场景结构变化会失效 — Phase DEV-1
