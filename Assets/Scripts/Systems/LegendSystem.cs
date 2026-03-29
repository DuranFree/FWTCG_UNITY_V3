using System;
using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Systems
{
    /// <summary>
    /// Manages legend passives and active skills (DEV-5).
    ///
    /// Kaisa (虚空) legend:
    ///   Active — 虚空感知: exhaust self + gain 1 Blazing schematic energy (reactive, once/turn)
    ///   Passive — 进化: allies have ≥4 distinct keywords → evolve (+3/+3)
    ///
    /// Masteryi (伊欧尼亚) legend:
    ///   Passive — 独影剑鸣: lone defender on any BF gets +2 temp attack
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
                return new LegendInstance(KAISA_LEGEND_ID, "卡莎·传奇", 3, GameRules.LEGEND_HP, owner);
            return new LegendInstance(YI_LEGEND_ID, "易大师·传奇", 3, GameRules.LEGEND_HP, owner);
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

        // ── Kaisa active: 虚空感知 ─────────────────────────────────────────────

        /// <summary>
        /// Activate Kaisa's 虚空感知: exhaust legend + +1 Blazing schematic energy.
        /// Usable during any phase as a reaction (once per turn, while not exhausted).
        /// Returns true if successfully used.
        /// </summary>
        public bool UseKaisaActive(string owner, GameState gs)
        {
            LegendInstance legend = gs.GetLegend(owner);
            if (legend == null || legend.Id != KAISA_LEGEND_ID)
            {
                Log("[传奇] 当前阵营传奇不是卡莎");
                return false;
            }
            if (!legend.IsAlive)
            {
                Log("[传奇] 卡莎已阵亡，无法使用技能");
                return false;
            }
            if (legend.AbilityUsedThisTurn)
            {
                Log("[传奇] 虚空感知本回合已使用（每回合限一次）");
                return false;
            }

            legend.Exhausted = true;
            legend.AbilityUsedThisTurn = true;
            gs.AddSch(owner, RuneType.Blazing, 1);

            int blazing = gs.GetSch(owner, RuneType.Blazing);
            Log($"[传奇] 卡莎发动【虚空感知】— 进入休眠，获得1炽烈符能（当前{blazing}点）");
            TurnManager.ShowBanner_Static("✨ 虚空感知！");
            return true;
        }

        // ── Kaisa passive: 进化 ────────────────────────────────────────────────

        /// <summary>
        /// Check if Kaisa's evolution condition is met.
        /// Counts distinct keywords among all allied units in base + battlefields.
        /// If ≥ LEGEND_EVOLUTION_KEYWORDS distinct keywords and level == 1 → evolve.
        /// Call this after any friendly unit enters play.
        /// </summary>
        public void CheckKaisaEvolution(string kaisaOwner, GameState gs)
        {
            LegendInstance legend = gs.GetLegend(kaisaOwner);
            if (legend == null || legend.Id != KAISA_LEGEND_ID) return;
            if (legend.Level >= 2) return;  // already evolved
            if (!legend.IsAlive) return;

            int keywordMask = 0;
            CollectKeywords(gs.GetBase(kaisaOwner), ref keywordMask);
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                List<UnitInstance> bfUnits = kaisaOwner == GameRules.OWNER_PLAYER
                    ? gs.BF[i].PlayerUnits
                    : gs.BF[i].EnemyUnits;
                CollectKeywords(bfUnits, ref keywordMask);
            }

            int distinctCount = CountBits(keywordMask);
            if (distinctCount >= GameRules.LEGEND_EVOLUTION_KEYWORDS)
            {
                legend.Evolve(3, 3);
                Log($"[传奇] 卡莎进化！盟友拥有{distinctCount}种关键词 — 等级→2，+3战力/+3生命上限");
                TurnManager.ShowBanner_Static("⭐ 卡莎·进化！Lv.2");
            }
        }

        private static void CollectKeywords(List<UnitInstance> units, ref int mask)
        {
            foreach (UnitInstance u in units)
                if (u.CardData != null) mask |= (int)u.CardData.Keywords;
        }

        private static int CountBits(int n)
        {
            int count = 0;
            while (n != 0) { count += n & 1; n >>= 1; }
            return count;
        }

        // ── Masteryi passive: 独影剑鸣 ─────────────────────────────────────────

        /// <summary>
        /// Apply Masteryi's 独影剑鸣 before combat:
        /// if Yi's owner defends with exactly 1 unit, that unit gets +2 TempAtkBonus.
        /// TempAtkBonus is cleared automatically after combat by ResetAllUnits.
        /// </summary>
        public void TryApplyMasteryiPassive(int bfId, string attacker, GameState gs)
        {
            string defender = gs.Opponent(attacker);
            LegendInstance legend = gs.GetLegend(defender);
            if (legend == null || legend.Id != YI_LEGEND_ID || !legend.IsAlive) return;

            List<UnitInstance> defenderUnits = defender == GameRules.OWNER_PLAYER
                ? gs.BF[bfId].PlayerUnits
                : gs.BF[bfId].EnemyUnits;

            if (defenderUnits.Count == 1)
            {
                defenderUnits[0].TempAtkBonus += 2;
                Log($"[传奇] 易大师【独影剑鸣】— {defenderUnits[0].UnitName} 孤身作战，+2战力" +
                    $"（实际战力 {defenderUnits[0].EffectiveAtk()}）");
            }
        }

        // ── Legend death check ─────────────────────────────────────────────────

        /// <summary>
        /// Check if any legend's HP reached 0.
        /// Returns a game-over message, or null if all legends are alive.
        /// </summary>
        public string CheckLegendDeaths(GameState gs)
        {
            if (gs.PLegend != null && !gs.PLegend.IsAlive)
                return $"[传奇] {gs.PLegend.Name} 阵亡！AI 获胜！";
            if (gs.ELegend != null && !gs.ELegend.IsAlive)
                return $"[传奇] {gs.ELegend.Name} 阵亡！玩家 获胜！";
            return null;
        }

        // ── Logging ───────────────────────────────────────────────────────────

        private static void Log(string msg)
        {
            Debug.Log(msg);
            OnLegendLog?.Invoke(msg);
        }
    }
}
