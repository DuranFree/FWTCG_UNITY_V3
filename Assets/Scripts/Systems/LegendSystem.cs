using System;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Systems
{
    /// <summary>
    /// Manages legend abilities — 按贴图卡面原文：
    ///
    /// Kaisa 虚空之女（OGN-247）:
    ///   横置：反应 — 获得 1 任意符能，仅可用于打出法术。
    ///   （"进化"机制原卡不存在，已废弃。）
    ///
    /// Yi 无极剑圣（OGS-019，入门）:
    ///   被动 — 如果你只有一名友方单位防守一处战场，则该单位 +2。
    ///   （持续修饰符：实时计算，随条件自动生效/失效。）
    /// </summary>
    public class LegendSystem : MonoBehaviour
    {
        public static event Action<string> OnLegendLog;

        public const string KAISA_LEGEND_ID = "kaisa";
        public const string YI_LEGEND_ID    = "masteryi";

        // ── Legend initialization ─────────────────────────────────────────────

        public LegendInstance CreateLegend(string legendId, string owner)
        {
            if (legendId == KAISA_LEGEND_ID)
                return new LegendInstance(KAISA_LEGEND_ID, "卡莎·传奇", owner);
            return new LegendInstance(YI_LEGEND_ID, "易大师·传奇", owner);
        }

        // ── Per-turn reset (called in Awaken phase) ───────────────────────────

        /// <summary>Resets per-turn flags for the given owner's legend.</summary>
        public void ResetForTurn(string owner, GameState gs)
        {
            LegendInstance legend = gs.GetLegend(owner);
            if (legend == null) return;
            legend.Exhausted = false;
            legend.AbilityUsedThisTurn = false;
        }

        // ── Kaisa 主动: 横置→反应获得 1 任意符能，仅用于法术 ────────────────────

        /// <summary>
        /// 按卡面（OGN-247）：横置：反应 — 获得 1 任意符能，仅可用于打出法术。
        /// 使用方传入选择的符能类型 chosenType（玩家选 / AI 智选）。
        /// 符能加入 SpellOnlySch 专用池，不能支付单位/装备/技能费用。
        /// </summary>
        public bool UseKaisaActive(string owner, GameState gs, RuneType chosenType)
        {
            LegendInstance legend = gs.GetLegend(owner);
            if (legend == null || legend.Id != KAISA_LEGEND_ID)
            {
                Log("[传奇] 当前阵营传奇不是卡莎");
                return false;
            }
            if (legend.AbilityUsedThisTurn)
            {
                Log("[传奇] 本回合已使用（每回合限一次）");
                return false;
            }

            legend.Exhausted = true;
            legend.AbilityUsedThisTurn = true;
            gs.AddSpellOnlySch(owner, chosenType, 1);

            Log($"[传奇] 卡莎·虚空之女横置 — 获得 1 {chosenType.ToChinese()}符能（仅用于打出法术）");
            FWTCG.UI.GameEventBus.FireLegendSkillBanner("虚空之女",
                $"获得 1 {chosenType.ToChinese()}符能（法术专用）");
            FWTCG.UI.GameEventBus.FireLegendSkillFired(legend, owner);
            return true;
        }

        // ── Yi 被动: 独影剑鸣（持续修饰符） ────────────────────────────────────

        /// <summary>
        /// Yi 无极剑圣（OGS-019）被动 — 如果你只有一名友方单位防守一处战场，则该单位 +2。
        /// 持续修饰符：在查战力时动态加成。被 CombatSystem.ComputeCombatPower 调用。
        /// 返回 true 表示 unit 当前满足条件（防守 bfId 的唯一友方单位，且其主人拥有 Yi 传奇）。
        /// </summary>
        public static bool IsYiSoloDefender(UnitInstance unit, int bfId, string ownerOfUnit, GameState gs)
        {
            if (unit == null || gs == null) return false;
            if (bfId < 0 || bfId >= gs.BF.Length) return false;
            var legend = gs.GetLegend(ownerOfUnit);
            if (legend == null || legend.Id != YI_LEGEND_ID) return false;

            var defenderUnits = ownerOfUnit == GameRules.OWNER_PLAYER
                ? gs.BF[bfId].PlayerUnits : gs.BF[bfId].EnemyUnits;

            if (defenderUnits.Count != 1) return false;
            return defenderUnits[0] == unit;
        }

        // ── Logging ───────────────────────────────────────────────────────────

        private static void Log(string msg)
        {
            Debug.Log(msg);
            OnLegendLog?.Invoke(msg);
        }
    }
}
