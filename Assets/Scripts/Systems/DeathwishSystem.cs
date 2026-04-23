using System.Collections.Generic;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Systems
{
    /// <summary>
    /// Handles deathwish (绝念) effects when units die in combat.
    /// Called by CombatSystem after RemoveDeadUnits.
    /// </summary>
    public class DeathwishSystem : MonoBehaviour
    {
        public static event System.Action<string> OnDeathwishLog;

        // DEV-32 A6: 需要 GameState 引用才能从事件总线处理 OnUnitsDied（事件签名不带 gs）
        private GameState _gsRef;
        public void Inject(GameState gs) { _gsRef = gs; }

        private void OnEnable()
        {
            FWTCG.UI.GameEventBus.OnUnitsDied += OnUnitsDiedViaEvent;
        }

        private void OnDisable()
        {
            FWTCG.UI.GameEventBus.OnUnitsDied -= OnUnitsDiedViaEvent;
        }

        private void OnUnitsDiedViaEvent(List<UnitInstance> deadUnits, int bfId)
        {
            if (_gsRef == null) return;
            OnUnitsDied(deadUnits, bfId, _gsRef);
        }

        /// <summary>
        /// Trigger deathwish effects for a list of units that just died.
        /// bfId: battlefield where the unit died (-1 if from base/other).
        /// </summary>
        public void OnUnitsDied(List<UnitInstance> deadUnits, int bfId, GameState gs)
        {
            foreach (UnitInstance unit in deadUnits)
            {
                if (!unit.CardData.HasKeyword(CardKeyword.Deathwish)) continue;
                TriggerDeathwish(unit, bfId, gs);
            }
        }

        private void TriggerDeathwish(UnitInstance unit, int bfId, GameState gs)
        {
            string owner = unit.Owner;
            string effectId = unit.CardData.EffectId;

            switch (effectId)
            {
                case "alert_sentinel_die":
                    // 绝念：必然触发效果 — 播放 showcase
                    FWTCG.UI.SpellShowcaseUI.Instance?.ShowAsync(unit, owner);
                    DrawCard(owner, gs);
                    Log($"[绝念] {unit.UnitName} 阵亡 — 摸1张牌");
                    FWTCG.UI.GameEventBus.FireDeathwishBanner(unit.UnitName, "摸1张牌"); // DEV-18b
                    break;

                case "wailing_poro_die":
                    // Draw only if this unit was alone in that zone
                    bool isAlone = IsAloneInZone(unit, bfId, gs);
                    if (isAlone)
                    {
                        // 仅在效果真正触发时播放 showcase（孤独阵亡条件满足）
                        FWTCG.UI.SpellShowcaseUI.Instance?.ShowAsync(unit, owner);
                        DrawCard(owner, gs);
                        Log($"[绝念] {unit.UnitName} 孤独阵亡 — 摸1张牌");
                        FWTCG.UI.GameEventBus.FireDeathwishBanner(unit.UnitName, "孤独阵亡 — 摸1张牌"); // DEV-18b
                    }
                    else
                    {
                        Log($"[绝念] {unit.UnitName} 阵亡但非孤独 — 无效果");
                    }
                    break;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private bool IsAloneInZone(UnitInstance unit, int bfId, GameState gs)
        {
            string owner = unit.Owner;
            // Check if there were any other allies on the same BF
            // (At time of death, unit is already removed from the list,
            //  so 0 remaining allies = was alone)
            if (bfId < 0) return false;

            List<UnitInstance> bfUnits = owner == GameRules.OWNER_PLAYER
                ? gs.BF[bfId].PlayerUnits
                : gs.BF[bfId].EnemyUnits;

            return bfUnits.Count == 0;
        }

        private void DrawCard(string owner, GameState gs)
        {
            List<UnitInstance> deck = gs.GetDeck(owner);
            List<UnitInstance> hand = gs.GetHand(owner);
            List<UnitInstance> discard = gs.GetDiscard(owner);

            if (deck.Count == 0)
            {
                if (discard.Count == 0) return;
                deck.AddRange(discard);
                discard.Clear();
            }

            if (deck.Count > 0)
            {
                hand.Add(deck[0]);
                deck.RemoveAt(0);
            }
        }

        private void Log(string msg)
        {
            Debug.Log(msg);
            OnDeathwishLog?.Invoke(msg);
        }
    }
}
