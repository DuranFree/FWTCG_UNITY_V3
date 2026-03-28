# 技术债清单
> 发现时追加，解决后删除
> 格式：- [ ] <描述> — 原因：<why deferred> — Phase <number>

- [ ] TMP Essential Resources 未导入 — 批处理模式无 GPU，TMP 无法弹出导入窗口，需用户在 Unity Editor 手动执行 Window → TextMeshPro → Import TMP Essential Resources — Phase DEV-1
- [ ] CardData ScriptableObject 字段简化 — DEV-1 仅包含 id/cardName/cost/atk/runeType/runeCost/description，完整字段（keywords/effect/description_full等）等 DEV-2 扩展 — Phase DEV-1
- [ ] UI 引用在 batch mode 下通过 GameObject.Find 连线，运行时若场景结构变化会失效 — Phase DEV-1
