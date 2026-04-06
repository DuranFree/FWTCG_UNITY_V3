# CLAUDE.md — 项目全局规则

## 六条执行铁律

1. **解除克制**：修 bug 时主动检查周围代码，发现问题一起修。
2. **编辑后验证**：每次编辑代码文件后，立即触发编译/类型检查，不需要用户提示。（优先用引擎 MCP，无 MCP 则用命令行）
3. **回复简洁直接**：不加开头废话，直接给结果。
4. **大改动先出计划**：改动超过 5 个文件先输出计划。
5. **不确定主动说**：不确定的地方直接说不确定，不编造。
6. **新功能必须有测试**：每个新功能实现完后，必须同时写对应测试，不等用户要求。

---

这些规则在每个会话自动生效，不可忽略。

**每次新会话开始时，立即执行：**
读取以下文件了解当前项目状态，然后告知用户当前进度：
- `./logs/dev-log.md`（只读最后 2 个 Phase 的记录）
- `./plans/known-bugs.md`
- `./plans/tech-debt.md`
- `./plans/phase-roadmap.md`

读取完成后，立即检查 git 状态：
- 执行 `git status` 检查是否有未提交的修改
- 执行 `git log origin/master..HEAD` 检查是否有未推送的 commit
- 有未提交修改 → 告知用户：`⚠️ 检测到上次 Phase 有未提交内容，正在补 commit 并推送...`，自动 commit（commit message 基于 dev-log 最后一条 Phase 记录），然后 push
- 有未推送 commit → 告知用户：`⚠️ 检测到上次 Phase 有未推送内容，正在补推...`，直接 push
- push 失败 → 告知用户：`⚠️ [版本控制] push 失败，请检查网络或远程仓库状态`
- 全部干净 → 静默继续，不打扰用户

以上 4 个文件构成最小上下文。除非任务需要，不进行代码文件的全量或无目的预读；在执行任务时按调用链逐步加载相关代码。

**开始新 Phase 前必须读取：**
- `./plans/feature-checklist.md`
- `./plans/visual-checklist.md`
- `./plans/assets-index.json`（如存在）— 了解当前可用美术资产，涉及美术资源的决策必须基于此文件
  - ⚠️ 禁止用 Read 工具直接读此文件（文件过大，Read 只能读到头部，会漏掉后半段资产）
  - 必须用 python/bash 脚本按关键词过滤查询，例如：`python3 -c "import json; data=json.load(open('plans/assets-index.json')); [print(a['name'],a['path']) for a in data['assets'] if 'FX' in a['path']]"`

检查 git status，如果以下文件出现在修改列表中，立即读取：
- `plans/assets-index.json`

收到任何涉及图片/贴图/美术/视觉资源的需求时，第一步必须查项目美术资源索引文件（查 memory 获取具体路径；memory 无记录 → 询问用户，回答后立即存入 memory），再开口。

---

## 每个 Phase 完成后必须执行（按顺序）

**⚠️ 以下 10 步是强制要求，不可跳过，不可省略任何一步，无论上下文多长都必须完整执行。**

**⚠️ 上下文压缩恢复规则：** 执行 10 步收尾期间，如果检测到压缩提示（`[Compaction occurred]`），立即重新读取 `CLAUDE.md`，从第 1 步重新执行完整的 10 步收尾，不得假设之前的步骤已完成。

1. **引擎场景验证** — 测试全绿后、代码审查前立即执行：
   - 验证前先用项目指定方式重建场景（查 memory 获取具体方式），重建完成后立即调用 `save_scene` 清除 dirty 状态，再执行验证
   - 若本 Phase 有任何视听改动（UI、特效、动画、音效、材质、场景结构等），必须用引擎 MCP 工具自动验证能检查的部分（组件挂载、字段连线、节点存在性、初始属性等）
   - 引擎 MCP 不可用时，改用读取场景文件或请用户描述，不得静默跳过
   - 验证未完成（任何原因）必须告知用户：`⚠️ [引擎场景验证未完成] 请手动确认视听效果`
   - 纯逻辑 Phase（无任何视听改动）可标注：`✅ [引擎场景验证] 本 Phase 无视听改动，跳过` 并继续

2. **代码审查** — 测试全绿后立即执行，commit 之前完成：
   - **第一步 — Claude 自身审查：** 对所有变动做完整审查，重点检查：
     - 边界条件与异常路径
     - 跨系统调用是否有副作用
     - 新增代码与现有架构是否一致
     - 安全性与性能隐患
   - **第二步 — Codex 深度审查（必触发）：** 运行 `/codex adversarial-review`，触发时高亮提示：
     > `🔍 [Codex 审查] 正在调用 adversarial-review，审查当前 Phase 变动，请稍候...`
     - 完成后输出：`✅ [Codex 审查完成]`
     - Codex 不可用 → 改用 Claude 自身从头完整审查，输出：`⚠️ [Codex 不可用，已切换 Claude 自身审查]`
   - 审查结果：High → 必须修复后才能继续；Medium / Low → 记入 `tech-debt.md`

3. **更新功能清单** — 标记已完成的功能项：
   `./plans/feature-checklist.md`

4. **更新视觉清单** — 标记已完成的视觉项：
   `./plans/visual-checklist.md`

5. **更新开发日志** — 追加本 Phase 的记录：
   `./logs/dev-log.md`
   - 写入前必须先读 `./plans/phase-roadmap.md`，用 roadmap 里的 Phase 编号，不得自行递增

6. **检查技术债** — 有新增就追加，已解决就删除：
   `./plans/tech-debt.md`

7. **检查已知 Bug** — 有新发现就追加，已修复就标记 ✅：
   `./plans/known-bugs.md`

8. **读取功能清单和视觉清单** — 确认下一步从哪里开始：
   读取 `./plans/feature-checklist.md` 和 `./plans/visual-checklist.md`
   不得依赖记忆，必须读取文件后才能继续。

9. **版本控制检查** — 测试全绿后检查 git 状态：
   - 有 git → 自动 commit 并 push 到远程仓库
   - 没有 git → 提醒用户：`⚠️ [版本控制] 未检测到 git 仓库`
   - push 失败 → 告知用户：`⚠️ [版本控制] push 失败，请检查网络或远程仓库状态`

10. **检查提示** — 完成以上全部 9 步后，向用户汇报本次 Phase 收尾已全部执行完毕，逐步列出各步骤状态，无论本 Phase 是否有新增完成项，都必须明确告知用户清单状态，不得静默跳过。

**⚠️ 第 6、7 步（tech-debt.md 和 known-bugs.md）是强制步骤，不得因为"没有变化"而跳过，必须读取并确认状态。**

---

## 功能对账规则

每个 Phase 完成后，回顾完整功能清单，检查：
- 是否有功能在清单里但没有分配到任何 Phase？
- 是否有功能"提过但没有明确落地"？

如果有，立即补充到对照表并告知用户：
> "⚠️ 发现以下功能还没有 Phase 归属：[列出功能]，请确认是加入后续 Phase 还是标注为 OUT OF SCOPE。"

---

## 测试规则

- 测试不可跳过，任何情况下都不能省略
- 每个 Phase 必须测试全绿才能继续下一个
- 逻辑测试每个循环后立即跑：`🟢 [逻辑测试]`
- 引擎测试每个 Phase 完成后跑：`🔵 [引擎测试]`
- **以上两类测试均指测试时机，不影响测试方式：编辑器开着时一律用 MCP run_tests，不得因为是"逻辑测试"就改用 batchmode**
- 如果测试框架未安装，自动安装，安装失败才用 TestRunner 替代
- **新增文件后，必须等待引擎完成资源导入/重新编译，再执行后续操作**
- **跑测试前，先用引擎 MCP 检查控制台是否有编译错误，有错误必须先修复再执行测试**
- **跑测试前，必须先用引擎 MCP 停止 Play Mode（无论当前是否在运行），排除干扰后再执行测试**
- **引擎编辑器开着时，必须用引擎 MCP 的 run_tests 工具跑测试，不得跳过直接用 batchmode**
- **MCP 跑测试全绿后，不再重复跑 batchmode；两者互斥，只跑其中一种**
- **PlayMode 测试或 MCP 场景验证前，必须先重建/确认场景已正确加载：**
  - 先调用 `save_scene` 清除当前 dirty 状态
  - 再查 memory 获取项目指定方式执行场景重建
  - memory 中无记录 → 询问用户"项目是否有场景重建方式"，用户回答后立即存入 memory，没有则跳过此步
- **MCP 全量测试超时时，优先分批跑（比 batchmode 快很多）：**
  - 按测试类/命名空间拆分多次 `run_tests(testFilter=...)`，每批控制在合理数量
  - 分批全部通过 = 等价于全量全绿
  - 分批也超时 → 再退回 batchmode
- **MCP run_tests 超时后禁止立即重试：** 超时不代表测试失败，只是 MCP 等不到结果。TestRunner 可能仍在后台执行，立即重试会导致请求排队堆积。超时后必须：
  1. 等待 30-60 秒，让 TestRunner 有时间跑完
  2. 用 `get_console_logs` 查看控制台输出，判断测试是否已完成（看有无测试结果日志）
  3. 已完成 → 根据日志判断结果，不需要重跑
  4. 未完成且无新输出（确认卡死）→ 调用 `recompile_scripts` 重置 TestRunner 队列，确认编译通过后再发下一次测试请求
- MCP run_tests 出错 / 超时 / 无响应时，**必须按以下顺序排查，不得直接杀编辑器**：
  1. 先尝试分批跑测试（见上条）
  2. 等待 30 秒后重试 MCP（可能正在编译）
  3. 仍无响应 → 再等待 30 秒后重试 MCP（最多等待 2 次共 1 分钟）
  4. 仍无响应 → 调用 `save_scene` 探测状态：
     - `save_scene` 报 "play mode" 错误 → 立刻停止 Play Mode（`execute_menu_item("Edit/Play")`），再重试 run_tests
     - `save_scene` 成功 → 说明不在 Play Mode，等待编译完成后重试 MCP
  5. 仍无响应 → 停止 Play Mode（如尚未停止）
  6. 重试 MCP
  7. 等待编译完成
  8. 再次重试 MCP
  - 以上全部完成后仍然失败 / 卡死 → 强制关闭引擎，切换 batchmode / headless，告知用户：`⚠️ 已强制关闭 [引擎名]，正在后台启动测试...`
    - Windows：`cmd.exe //C "taskkill /F /IM 进程名.exe"`，不可直接调用 taskkill
    - 关闭后必须用进程列表确认进程已退出（`tasklist` / `ps`），验证失败则重试，不得假设成功
- 引擎编辑器未运行时，才用 batchmode / headless 模式跑测试
- batchmode 测试跑完后，必须关闭引擎进程，不得残留
- **测试全绿后，MCP 场景验证：** 能用引擎 MCP 验证的先自动验证（组件引用、场景结构、位置、初始属性等），只把人眼才能判断的留给用户
  - MCP 跑测试路径 → 编辑器仍开着，直接执行 MCP 场景验证
  - batchmode 路径 → 编辑器已关闭，提示用户：`⚠️ 测试完成，请重新打开 [引擎名] 编辑器，打开后告诉我继续，将进行场景验证`，用户确认后执行 MCP 场景验证
- **交给用户 Play Mode 验证前，先用项目指定方式重建/刷新场景（查 memory 获取具体方式），完成后告知用户：`✅ 场景已重建，请在编辑器中进入 Play Mode 验证以下内容：`**
- **视听改动前置确认：** 收到任何视听相关的修改请求，必须先用引擎 MCP 验证问题确实存在，确认后才开始修改。MCP 无法验证时，改用读文件或请用户描述，不得直接跳到修改

- **每个功能必须包含边界条件测试**：无合法目标、空列表、零值、极限值等异常路径，不只测正常流程

## 交互测试规则

每个 Phase 完成后，针对本 Phase 新增的所有可交互元素，必须写 Play Mode 测试验证交互响应：
- 用代码模拟点击、触发等操作
- 验证对应的回调、事件、状态变化是否正确触发
- 测试必须全绿才能停下来等用户确认
- 测试写法必须通用，不依赖具体引擎 API，只验证行为结果

例如：点击某个按钮后，验证对应的事件是否被触发、状态是否正确改变，而不是验证按钮本身的存在。

---

## 禁止行为

- ❌ 跳过任何 Phase 的测试
- ❌ 不更新清单就继续下一个 Phase
- ❌ 依赖记忆而不读取文件
- ❌ 强行结束引擎进程，必须提前告知用户关闭引擎
- ❌ 有视听改动但跳过引擎场景验证（第 1 步）就进入代码审查

---

## 引擎 MCP 工具使用规则

需要查询场景结构、GameObject、组件属性时，优先判断是否有引擎 MCP 工具可用：
- 工具可用（如 `mcp__mcp-unity__*`）→ 直接调用，不读 .unity / .scene 文件
- 工具不可用 → 才读取文件或让用户描述

**注意：** 查顶层大节点（如 Canvas）数据量极大，应查具体子路径（如 `Canvas/Panel/Button`）。

**⚠️ MCP 修改场景后必须立即 save_scene（不可跳过）：** 任何通过 MCP 修改场景内容的操作（添加/删除/修改 GameObject、组件属性等）完成后，必须**立即**调用 `save_scene`，防止场景处于 dirty 状态，避免后续操作触发 Unity 保存弹窗导致 MCP 超时。**每次修改后都要执行，不得批量延后。**

---

## Roadmap 修改校验规则

任何时候新增、修改、追加 `plans/phase-roadmap.md` 的内容（包括追加新 Phase、调整顺序、修改 Phase 内容），必须先完整读取 `E:\claudeCode\DOC\skills\migration\04-plan.md`，逐条对照检查：

- 所有规划规则是否还在被遵守？
- Phase 顺序和依赖门是否正确？
- 有没有功能黑洞（功能未分配到任何 Phase）？
- 最后两个 Phase 是否固定为 Tech-Debt Cleanup + 架构优化？

**不得依赖记忆，必须读文件后才能改 roadmap。**

**新增 Phase 后必须同步清单（不可跳过）：**
roadmap 新增 Phase 后，立即把该 Phase 涉及的条目追加到对应清单，标记为 `[ ]`：
- 功能项 → `plans/feature-checklist.md`
- 视觉/特效/动画项 → `plans/visual-checklist.md`
- 没有视觉项的 Phase 不需要追加 visual-checklist，但必须明确说明跳过原因

---

## DOTween 动画规则（DOT-7 追加）

- 补间动画一律使用 DOTween，禁止手写协程插值（per-frame Lerp/MoveTowards/Sin 循环）
- 所有 tween 必须加 `.SetTarget(gameObject)` 以便 `DOTween.Kill(gameObject)` 统一清理
- OnDestroy 中必须 `DOTween.Kill(gameObject)` + 逐个 `KillSafe` 所有 tween 字段
- 流程控制协程（WaitForSeconds/WaitUntil/游戏逻辑等待）可保留，但内部动画仍用 DOTween
- 连续程序化模拟（每帧鼠标跟踪/粒子物理/贝塞尔曲线旋转）排除在外，不用 DOTween

---

## Unity 引擎常见坑

- `Awake()` 早于 `Start()`，跨组件初始化顺序依赖时要注意
- `OnDestroy()` 里必须取消事件订阅，否则内存泄漏
- `Time.deltaTime` 在 `FixedUpdate()` 里用 `Time.fixedDeltaTime`
- `Resources.Load()` 性能差，用 Addressables 或直接引用
- 不要在运行时用 `GameObject.Find()`，提前缓存引用
- `string` 拼接在热路径里用 `StringBuilder`
- Physics 操作放在 `FixedUpdate()`，输入检测放在 `Update()`
- UI 元素定位超出父容器边界时（pivot + offset 导致跑出 rect 范围），必须主动提示用户验证渲染层级是否遮挡

---

## 破坏性 Git 操作规则

执行任何破坏性 git 操作前（reset --hard、checkout .、restore .、clean -f、branch -D 等），必须：
1. 先运行 `git status` 检查是否有未提交修改
2. 如果有未提交修改，必须先 `git stash` 保存，或明确告知用户并等待确认，不得直接执行
3. 操作完成后提示用户是否需要 `git stash pop` 恢复


