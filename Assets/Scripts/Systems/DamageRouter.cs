using UnityEngine;
using FWTCG.Core;

namespace FWTCG.Systems
{
    /// <summary>
    /// DEV-32 A3: 统一伤害入口。
    ///
    /// **职责**：
    ///   - 扣 HP（支持战斗模式防透支；法术模式可透支）
    ///   - 应用战场加成（void_gate 等，仅法术）
    ///   - Fire <see cref="GameManager.FireUnitDamaged"/> 事件
    ///
    /// **非职责**（显式排除）：
    ///   - 死亡移除 — 各系统有差异化的保护链（Guardian/Zhonya）与容器处理（BF vs base），
    ///     继续由调用方处理；DamageRouter 不做死亡判定。
    ///   - SpellShield 抉择 — Rule 721 明确 SpellShield 是目标成本，在选目标时已付过，进入本函数时视为正常伤害。
    ///
    /// 调用方只关心"我要打 N 点伤害"；具体扣多少、要不要加成、要不要防透支由 flag 传达。
    /// 事件侧：本函数内唯一一次 FireUnitDamaged；调用方禁止再次 fire。
    /// </summary>
    public static class DamageRouter
    {
        /// <summary>伤害分类 — 控制 BF 加成适用与防透支策略。</summary>
        public enum DamageKind
        {
            /// <summary>战斗伤害：不加 BF 加成；防透支（最多扣到 0）。</summary>
            Combat,
            /// <summary>单点法术伤害：应用 BF 加成（void_gate）；允许透支（CurrentHp 可 < 0，不影响死亡判定）。</summary>
            Spell,
            /// <summary>范围法术伤害：不应用 BF 加成（Rule：BF 加成仅单点目标）；允许透支。</summary>
            AreaSpell,
            /// <summary>DEBUG 工具伤害：无加成，允许透支。</summary>
            Debug
        }

        /// <summary>
        /// 应用伤害到目标。返回实际扣除的 HP 量（受 respectHpFloor 影响）。
        /// target/amount 边界：target==null 或 amount<=0 时 no-op 返回 0。
        /// </summary>
        public static int Apply(UnitInstance target, int amount, GameState gs,
                                DamageKind kind, string sourceLabel,
                                BattlefieldSystem bfSys = null)
        {
            if (target == null || amount <= 0) return 0;

            int effective = amount;
            // BF 加成：仅单点法术
            if (kind == DamageKind.Spell && bfSys != null)
                effective += bfSys.GetSpellDamageBonus(target, gs);

            // 防透支：仅战斗（Rule 139.2 战斗伤害按剩余 HP 结算；溢出不计）
            int dealt = (kind == DamageKind.Combat)
                ? Mathf.Min(effective, Mathf.Max(0, target.CurrentHp))
                : effective;

            target.CurrentHp -= dealt;
            if (dealt > 0)
                GameManager.FireUnitDamaged(target, dealt, sourceLabel);

            return dealt;
        }
    }
}
