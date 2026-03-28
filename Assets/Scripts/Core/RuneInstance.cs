using FWTCG.Data;

namespace FWTCG.Core
{
    /// <summary>
    /// Runtime instance of a rune card.
    /// Tapped runes grant +1 mana. Recycled runes return to the rune deck
    /// and grant +1 of their corresponding schematic energy.
    /// </summary>
    public class RuneInstance
    {
        public int Uid { get; private set; }
        public RuneType RuneType { get; private set; }

        /// <summary>True when this rune has been tapped (used for mana).</summary>
        public bool Tapped { get; set; }

        public RuneInstance(int uid, RuneType runeType)
        {
            Uid = uid;
            RuneType = runeType;
            Tapped = false;
        }

        public override string ToString()
        {
            return $"Rune[{RuneType}] tapped:{Tapped}";
        }
    }
}
