# 已知 Bug 清单
> 发现时追加，修复后标记 ✅，不删除
> 格式：- [ ] <描述> — 发现于 Phase <number>

- [ ] TMP 文字不显示 — 首次打开 Unity 时 TMP Essential Resources 尚未导入，所有 TextMeshProUGUI 组件文字为空 — 发现于 Phase DEV-1（预期Bug，用户导入后自动修复）
- [ ] GameScene 中 UI 引用可能为 null — SceneBuilder 在 batch mode -nographics 下运行，TMP 组件 Awake 触发警告，部分 reference 可能未正确连接，需在 Editor 中验证 — 发现于 Phase DEV-1
