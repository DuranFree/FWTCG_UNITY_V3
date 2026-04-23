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
        public bool   Exhausted          { get; set; }  // true after 横置 active is used
        public bool   AbilityUsedThisTurn { get; set; }
        public string Owner { get; }

        /// <summary>[Deprecated] Kaisa "进化" 机制原卡不存在，已废弃；字段仅作向后兼容。</summary>
        public int Level { get; set; } = 1;

        /// <summary>Associated CardData for display (art, description). May be null.</summary>
        public CardData DisplayData { get; set; }

        public LegendInstance(string id, string name, string owner)
        {
            Id    = id;
            Name  = name;
            Owner = owner;
            Exhausted           = false;
            AbilityUsedThisTurn = false;
        }
    }
}
