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

        /// <summary>Current effective attack (may include buffs/debuffs).</summary>
        public int CurrentAtk { get; set; }

        /// <summary>Current HP. Equals CurrentAtk at start of each turn (atk=HP rule).</summary>
        public int CurrentHp { get; set; }

        public bool Exhausted { get; set; }
        public bool Stunned { get; set; }

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
            Owner = owner;
        }

        /// <summary>
        /// Effective attack used during combat. Never drops below 1.
        /// </summary>
        public int EffectiveAtk()
        {
            return Mathf.Max(1, CurrentAtk);
        }

        /// <summary>
        /// Called at end of turn: restore HP to current attack value, clear stun.
        /// This implements the "marked damage reset" rule.
        /// </summary>
        public void ResetEndOfTurn()
        {
            CurrentHp = CurrentAtk;
            Stunned = false;
        }

        public override string ToString()
        {
            return $"{UnitName}({Owner}) ATK:{CurrentAtk} HP:{CurrentHp} EX:{Exhausted}";
        }
    }
}
