using UnityEngine;
using FWTCG.Data;

namespace FWTCG.Core
{
    /// <summary>
    /// Runtime instance of a unit card on the battlefield or in hand.
    /// atk = HP is the core rule: currentHp is always initialised to atk.
    /// </summary>
    public class UnitInstance
    {
        public int Uid { get; private set; }
        public CardData CardData { get; private set; }
        public string UnitName { get; private set; }

        /// <summary>Base attack/HP value (does not change during combat).</summary>
        public int Atk { get; private set; }

        /// <summary>Current effective attack (may include buffs/debuffs + temp bonuses).</summary>
        public int CurrentAtk { get; set; }

        /// <summary>Current HP. Equals CurrentAtk at start of each turn (atk=HP rule).</summary>
        public int CurrentHp { get; set; }

        public bool Exhausted { get; set; }
        public bool Stunned { get; set; }

        /// <summary>
        /// Buff tokens on this unit. Rule 702: a unit can hold at most 1 buff token.
        /// Setting this value will be clamped to [0, 1].
        /// </summary>
        private int _buffTokens;
        public int BuffTokens
        {
            get => _buffTokens;
            set => _buffTokens = Mathf.Clamp(value, 0, 1);
        }

        /// <summary>Temporary attack bonus (e.g. Darius entry effect). Cleared end of turn.</summary>
        public int TempAtkBonus { get; set; }

        /// <summary>
        /// B10: 追踪本单位是否是"这一回合打出的"（duel_stance 额外+1 用）。
        /// 由 GameManager/AI 打出时设为 true；由 TurnManager.DoEndPhase 清零。
        /// </summary>
        public bool PlayedThisTurn { get; set; }

        /// <summary>
        /// B11: 追踪本单位"在本回合开始前就在基地"（rengar 可活跃移动用）。
        /// 由 TurnManager.DoAwaken 基于本回合开始时在基地的单位设置；移动离基或回合切换后清零。
        /// </summary>
        public bool WasInBaseAtTurnStart { get; set; }

        /// <summary>Whether this unit currently has a spell shield charge.</summary>
        public bool HasSpellShield { get; set; }

        /// <summary>Whether this unit has Barrier (壁垒): must absorb lethal damage first in combat.</summary>
        public bool HasBarrier { get; set; }

        /// <summary>Whether this unit has StrongAtk (强攻): +StrongAtkValue power when attacking.</summary>
        public bool HasStrongAtk { get; set; }

        /// <summary>强攻值（Rule 19.1.b）。默认 1；rengar 等特定卡为 2。</summary>
        public int StrongAtkValue { get; set; } = 1;

        /// <summary>Whether this unit has Guard (坚守): +GuardValue power when defending.</summary>
        public bool HasGuard { get; set; }

        /// <summary>坚守值（Rule 26.1.b）。默认 1。</summary>
        public int GuardValue { get; set; } = 1;

        /// <summary>Whether this unit has Reactive keyword (can be played in reaction window).</summary>
        public bool HasReactive { get; set; }

        /// <summary>Whether this unit cannot be targeted by spells.</summary>
        public bool UntargetableBySpells { get; set; }

        /// <summary>
        /// Ephemeral units (Rule 728) are destroyed at the start of the next turn.
        /// Set SummonedOnRound to the current gs.Round when creating an ephemeral unit.
        /// </summary>
        public bool IsEphemeral { get; set; }
        public int SummonedOnRound { get; set; } = -1;

        /// <summary>
        /// Standby units (Rule 716) are deployed face-down at 0 cost.
        /// While IsStandby=true the unit does not participate in combat and
        /// cannot be targeted. The owner may flip it face-up as a 0-cost action.
        /// </summary>
        public bool IsStandby { get; set; }

        /// <summary>Equipment attached to this unit (null if none).</summary>
        public UnitInstance AttachedEquipment { get; set; }

        /// <summary>Unit this equipment is attached to (null if this is not equipment or unattached).</summary>
        public UnitInstance AttachedTo { get; set; }

        // ── Cost / rune modifiers (set by card effects, persist until removed) ─
        /// <summary>Flat modifier applied to mana cost. Negative = cost reduction.</summary>
        public int CostModifier { get; set; }

        /// <summary>Flat modifier applied to rune (sch) cost. Negative = cost reduction.</summary>
        public int RuneCostModifier { get; set; }

        /// <summary>Override rune type requirement. Null = use CardData.RuneType.</summary>
        public Data.RuneType? RuneTypeOverride { get; set; }

        /// <summary>Effective mana cost after modifiers.</summary>
        public int EffectiveCost => Mathf.Max(0, CardData.Cost + CostModifier);

        /// <summary>Effective rune cost after modifiers.</summary>
        public int EffectiveRuneCost => Mathf.Max(0, CardData.RuneCost + RuneCostModifier);

        /// <summary>Effective rune type (overridden or base).</summary>
        public Data.RuneType EffectiveRuneType => RuneTypeOverride ?? CardData.RuneType;

        /// <summary>"player" or "enemy"</summary>
        public string Owner { get; private set; }

        public UnitInstance(int uid, CardData data, string owner)
        {
            Uid = uid;
            CardData = data;
            UnitName = data.CardName;
            Atk = data.Atk;
            CurrentAtk = data.Atk;
            CurrentHp = data.Atk;   // atk = HP core rule
            Exhausted = false;
            Stunned = false;
            _buffTokens = 0;
            TempAtkBonus = 0;
            HasSpellShield = data.HasKeyword(CardKeyword.SpellShield);
            HasBarrier = data.HasKeyword(CardKeyword.Barrier);
            HasStrongAtk = data.HasKeyword(CardKeyword.StrongAtk);
            HasGuard = data.HasKeyword(CardKeyword.Guard);
            IsEphemeral = data.HasKeyword(CardKeyword.Ephemeral);
            Owner = owner;
        }

        /// <summary>
        /// Effective attack used during combat.
        /// Stunned units contribute 0 power. Otherwise minimum 0 (Rule 139.2: power &lt; 0 treated as 0).
        /// Includes TempAtkBonus (e.g. Darius entry, StrongAtk).
        /// </summary>
        public int EffectiveAtk()
        {
            if (Stunned) return 0;
            return Mathf.Max(0, CurrentAtk + TempAtkBonus);
        }

        /// <summary>
        /// Called at end of turn: restore HP, clear stun, clear temp bonuses.
        /// </summary>
        public void ResetEndOfTurn()
        {
            // Re-apply buff tokens to base atk before resetting HP
            int baseWithBuffs = Atk + BuffTokens;
            if (CurrentAtk < baseWithBuffs) CurrentAtk = baseWithBuffs;
            CurrentHp = CurrentAtk;
            Stunned = false;
            TempAtkBonus = 0;
        }

        // ── Status badge helpers ──────────────────────────────────────────────

        /// <summary>True if this unit has any positive status (buff token, temp bonus, external stat raise).
        /// Equipment is intentionally excluded — it has its own dedicated badge (DEV-25).</summary>
        public bool HasBuff =>
            BuffTokens > 0 ||
            TempAtkBonus > 0 ||
            (CurrentAtk > Atk + BuffTokens + (AttachedEquipment?.CardData.EquipAtkBonus ?? 0)); // external buff raised base

        /// <summary>True if this unit has any negative status (damaged, stunned, weakened).</summary>
        public bool HasDebuff =>
            Stunned ||
            CurrentHp < CurrentAtk ||       // took damage this turn
            CurrentAtk < Atk + BuffTokens;  // stat was reduced below buffed base

        /// <summary>Build a human-readable list of all active buffs (excludes equipment — shown by equip badge).</summary>
        public string BuildBuffSummary()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (BuffTokens > 0)
                parts.Add($"+{BuffTokens}/+{BuffTokens} 强化标记");
            if (TempAtkBonus > 0)
                parts.Add($"+{TempAtkBonus} 临时战力加成");
            // Exclude equipment bonus from external-buff calculation so it doesn't double-appear
            int equipBonus   = AttachedEquipment?.CardData.EquipAtkBonus ?? 0;
            int externalBuff = CurrentAtk - Atk - BuffTokens - equipBonus;
            if (externalBuff > 0)
                parts.Add($"+{externalBuff} 战力加成");
            return parts.Count > 0 ? string.Join("\n", parts) : "无";
        }

        /// <summary>Build a human-readable summary of attached equipment for the equip badge tooltip.</summary>
        public string BuildEquipSummary()
        {
            if (AttachedEquipment == null) return "无";
            var eq    = AttachedEquipment;
            var lines = new System.Collections.Generic.List<string>();
            lines.Add(eq.UnitName);
            if (eq.CardData.EquipAtkBonus > 0)
                lines.Add($"+{eq.CardData.EquipAtkBonus} 战力");
            if (!string.IsNullOrEmpty(eq.CardData.Description))
                lines.Add(eq.CardData.Description);
            return string.Join("\n", lines);
        }

        /// <summary>Build a human-readable list of all active debuffs.</summary>
        public string BuildDebuffSummary()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (Stunned)
                parts.Add("眩晕（本回合战力归零）");
            if (CurrentHp < CurrentAtk)
                parts.Add($"已受伤 HP {CurrentHp}/{CurrentAtk}");
            int loss = (Atk + BuffTokens) - CurrentAtk;
            if (loss > 0)
                parts.Add($"-{loss} 战力削弱");
            return parts.Count > 0 ? string.Join("\n", parts) : "无";
        }

        public override string ToString()
        {
            return $"{UnitName}({Owner}) ATK:{CurrentAtk} HP:{CurrentHp} EX:{Exhausted}";
        }
    }
}
