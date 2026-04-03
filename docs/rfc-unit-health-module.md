# RFC: Extract UnitHealth deep module — consolidate damage/death logic from 4 systems

> 创建为 GitHub Issue：https://github.com/DuranFree/FWTCG_UNITY_V3/issues/new
> 标题：`RFC: Extract UnitHealth deep module — consolidate damage/death logic from 4 systems`

---

## Problem

伤害分配和死亡处理逻辑分散在四个系统中，互相复制、行为不一致：

- **`CombatSystem`** — `DistributeDamage()` 实现 Barrier 优先排序 + 手动 HP 扣减，`RemoveDeadUnits()` 搜索所有列表并清理 `TiyanasInPlay` 标志，内联 8 层 Deathwish 链循环
- **`SpellSystem`** — `DealDamage()` + `RemoveDeadUnit()`，逻辑与 CombatSystem 相似但**漏掉了 `TiyanasInPlay` 清理**（已知 bug：Tiyana 被法术击杀时 passive 仍生效）
- **`ReactiveSystem`** — `RemoveDeadUnit()` 完全复制自 SpellSystem，第三份拷贝
- **`DeathwishSystem`** — `OnUnitsDied()` 触发效果，但不负责移除单位，也没有自己的链深度保护

结果：
1. 修改任何一处死亡逻辑需要同步改 3 个文件
2. SpellSystem 的多目标伤害（如 AkasiStorm）没有 Barrier 优先排序（CombatSystem 有）
3. 上述 `TiyanasInPlay` bug 是因为 `RemoveDeadUnit` 存在多份实现

---

## Proposed Interface

```csharp
public readonly struct DamageResult
{
    public readonly bool Died;
    public readonly IReadOnlyList<UnitInstance> Dead;   // 多目标用
    public readonly int DeathwishChainDepth;            // 调试用

    public static implicit operator bool(DamageResult r) => r.Died;
}

public class UnitHealth
{
    // 【最常用】单目标伤害 — SpellSystem / ReactiveSystem 调用路径
    // Barrier 自动处理，死亡自动移除，Deathwish 链自动触发
    public DamageResult Damage(UnitInstance target, int amount, GameState gs,
                               string sourceLabel = "法术",
                               int bfId = -1,
                               bool triggerDeathwish = true);

    // 【次常用】多目标，Barrier 优先分配总伤害 — CombatSystem 调用路径
    public DamageResult DamageAll(IReadOnlyList<UnitInstance> targets, int totalDamage,
                                  GameState gs,
                                  string sourceLabel = "战斗",
                                  int bfId = -1);

    // 【少用】对已死亡单位触发 Deathwish 链 — DeathwishSystem 委托调用
    public void TriggerDeathwishChain(IReadOnlyList<UnitInstance> dead,
                                      GameState gs, int bfId = -1);
}
```

**调用示例：**

```csharp
// SpellSystem — hex_ray：1 行
case "hex_ray":
    _health.Damage(target, 3, gs, "hex_ray");
    break;

// CombatSystem — 战斗结算：3 行
var defResult = _health.DamageAll(defenderUnits, attackerPower, gs, "战斗", bfId);
var atkResult = _health.DamageAll(attackerUnits, defenderPower, gs, "战斗", bfId);
_health.TriggerDeathwishChain(defResult.Dead.Concat(atkResult.Dead).ToList(), gs, bfId);

// ReactiveSystem — 与 SpellSystem 完全一致
_health.Damage(target, 2, gs, "罪恶乐趣");
```

**内部隐藏的复杂性：**
1. Barrier 关键词检测 + 首次致命伤害拦截
2. 死亡单位从所有区域列表中搜索移除（Base / BF[0] / BF[1] / Hand）
3. 加入 Discard，清理 `TiyanasInPlay` 等所有附属标志
4. `GameEventBus.FireUnitDied()` 在移除前触发（动画要求）
5. Deathwish 链深度上限 8，链内产生的新死亡自动递归处理

---

## Dependency Strategy

**In-process** — 纯内存计算，无 I/O，无 MonoBehaviour 依赖。

- `UnitHealth` 可直接 `new UnitHealth(deathwishSystem)` 实例化
- `DeathwishSystem` 作为构造器参数注入（保留独立的 effect switch 表）
- 测试中 `new GameState()` + `new UnitHealth(new DeathwishSystem())` 即可覆盖所有场景

---

## Testing Strategy

**新边界测试（在 `UnitHealth` 接口上直接断言）：**
- `Damage()` 目标 HP 归零 → `Died = true`，单位不在任何区域列表中
- `Damage()` Barrier 单位收致命伤害 → 存活，Barrier 关键词移除
- `DamageAll()` 多目标 Barrier 优先 → Barrier 单位最后死亡
- `Damage()` Deathwish 单位死亡 → 触发对应效果
- Deathwish 链深度超过 8 → `DeathwishChainDepth == 8`，无无限循环
- `TiyanasInPlay` bug 修复验证 → Tiyana 被 `Damage()` 打死后标志归零

**可删除的旧测试：**
- `SpellSystem` 中所有测试 `RemoveDeadUnit` 内部实现的测试
- `CombatSystem` 中单独测试 `DistributeDamage` LINQ 排序的测试
- `ReactiveSystem` 中复制的伤害移除测试

**不需要测试环境变更：** 全部 EditMode，无 MonoBehaviour，现有基础设施完全兼容。

---

## Implementation Recommendations

**模块应该拥有：**
- 所有对 `UnitInstance.CurrentHp` 的写操作（死亡语义）
- 所有从区域列表移除死亡单位的逻辑
- Deathwish 链的编排（深度计数、循环终止）
- `TiyanasInPlay`、`_barrierConsumed` 等附属标志的清理

**模块应该隐藏：**
- Barrier 检测和首次致命伤害的拦截算法
- 区域搜索（遍历 PBase / EBase / BF[0] / BF[1] 寻找单位）
- Deathwish 链的递归/迭代实现细节
- `GameEventBus.FireUnitDied()` 的调用时序

**模块应该暴露：**
- `Damage(target, amount, gs)` — 单目标，覆盖 95% 调用场景
- `DamageAll(targets, total, gs)` — 多目标 Barrier 分配，覆盖战斗系统
- `TriggerDeathwishChain(dead, gs)` — 纯效果触发，无伤害

**调用方迁移路径：**
1. 创建 `UnitHealth.cs`，内部移植 `CombatSystem.DistributeDamage` + `RemoveDeadUnits` + Deathwish 链
2. 在 `GameManager.Awake()` 中实例化 `UnitHealth` 并注入各系统
3. 依次替换各系统调用点（SpellSystem → ReactiveSystem → CombatSystem），每步跑测试
4. 替换完成后删除各系统中无调用者的私有方法
5. 补充边界测试，删除已被覆盖的旧测试

**扩展考量（未来预留）：**
- `bool triggerDeathwish = true` — 未来"压制 Deathwish"规则
- `bool exileOnDeath = false` — 未来流放效果
- void_gate 等修改器：先在 `Damage()` 内 if-check，规模到 5 张以上再提取修改器链
