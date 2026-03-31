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

        /// <summary>Whether this unit currently has a spell shield charge.</summary>
        public bool HasSpellShield { get; set; }

        /// <summary>Whether this unit has Barrier (壁垒): must absorb lethal damage first in combat.</summary>
        public bool HasBarrier { get; set; }

        /// <summary>Whether this unit has StrongAtk (强攻): +1 power when attacking.</summary>
        public bool HasStrongAtk { get; set; }

        /// <summary>Whether this unit has Guard (坚守): +1 power when defending.</summary>
        public bool HasGuard { get; set; }

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
        /// Stunned units contribute 0 power. Otherwise minimum 1.
        /// Includes TempAtkBonus (e.g. Darius entry, StrongAtk).
        /// </summary>
        public int EffectiveAtk()
        {
            if (Stunned) return 0;
            return Mathf.Max(1, CurrentAtk + TempAtkBonus);
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

        public override string ToString()
        {
            return $"{UnitName}({Owner}) ATK:{CurrentAtk} HP:{CurrentHp} EX:{Exhausted}";
        }
    }
}
