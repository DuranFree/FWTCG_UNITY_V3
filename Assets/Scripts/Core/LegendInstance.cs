namespace FWTCG.Core
{
    /// <summary>
    /// Runtime state of a legend card.
    /// Legends live in their own zone, independent of base/battlefield.
    /// HP is managed independently (not subject to the atk=HP rule).
    /// </summary>
    public class LegendInstance
    {
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; private set; }
        public int CurrentHp { get; set; }
        public int Atk { get; set; }
        public int Level { get; set; }                   // 1 or 2; Kaisa can evolve
        public bool Exhausted { get; set; }              // 虚空感知: true after active used
        public bool AbilityUsedThisTurn { get; set; }
        public string Owner { get; }

        public bool IsAlive => CurrentHp > 0;

        public LegendInstance(string id, string name, int atk, int maxHp, string owner)
        {
            Id = id;
            Name = name;
            Atk = atk;
            MaxHp = maxHp;
            CurrentHp = maxHp;
            Level = 1;
            Exhausted = false;
            AbilityUsedThisTurn = false;
            Owner = owner;
        }

        /// <summary>Apply damage. Returns true if the legend died.</summary>
        public bool TakeDamage(int amount)
        {
            CurrentHp -= amount;
            if (CurrentHp < 0) CurrentHp = 0;
            return CurrentHp <= 0;
        }

        /// <summary>Level up: increase Atk and HP by given bonuses. Sets Level = 2.</summary>
        public void Evolve(int atkBonus, int hpBonus)
        {
            Atk += atkBonus;
            MaxHp += hpBonus;
            CurrentHp += hpBonus;
            Level = 2;
        }
    }
}
