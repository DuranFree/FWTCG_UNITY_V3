using FWTCG.Data;

namespace FWTCG.Core
{
    /// <summary>
    /// Runtime state of a legend card.
    /// Legends live in their own zone (rule 167.4), cannot be destroyed (rule 167.3),
    /// and cannot move. They are purely skill carriers.
    /// </summary>
    public class LegendInstance
    {
        public string Id    { get; }
        public string Name  { get; }
        public int    Level { get; set; }                // 1 or 2; Kaisa can evolve to Lv.2
        public bool   Exhausted          { get; set; }  // true after 虚空感知 is activated
        public bool   AbilityUsedThisTurn { get; set; }
        public string Owner { get; }

        /// <summary>Associated CardData for display (art, description). May be null.</summary>
        public CardData DisplayData { get; set; }

        public LegendInstance(string id, string name, string owner)
        {
            Id    = id;
            Name  = name;
            Owner = owner;
            Level = 1;
            Exhausted           = false;
            AbilityUsedThisTurn = false;
        }

        /// <summary>Mark this legend as evolved (Level → 2). Can only trigger once.</summary>
        public void Evolve()
        {
            Level = 2;
        }
    }
}
