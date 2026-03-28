using System.Collections.Generic;

namespace FWTCG.Core
{
    /// <summary>
    /// State for one battlefield zone.
    /// Each battlefield has no unit cap per side.
    /// </summary>
    public class BattlefieldState
    {
        /// <summary>Battlefield index: 0 or 1 (BF1 / BF2).</summary>
        public int Id { get; private set; }

        public List<UnitInstance> PlayerUnits { get; private set; }
        public List<UnitInstance> EnemyUnits { get; private set; }

        /// <summary>
        /// Current controller. null = contested/empty, "player" or "enemy".
        /// </summary>
        public string Ctrl { get; set; }

        /// <summary>
        /// Whether the conquest score for this battlefield has already been awarded
        /// this turn (prevents double-scoring on re-entry).
        /// </summary>
        public bool ConqDone { get; set; }

        public BattlefieldState(int id)
        {
            Id = id;
            PlayerUnits = new List<UnitInstance>();
            EnemyUnits = new List<UnitInstance>();
            Ctrl = null;
            ConqDone = false;
        }

        /// <summary>Returns total effective attack power for the given owner.</summary>
        public int TotalPower(string owner)
        {
            int total = 0;
            List<UnitInstance> units = owner == GameRules.OWNER_PLAYER ? PlayerUnits : EnemyUnits;
            foreach (UnitInstance u in units)
            {
                total += u.EffectiveAtk();
            }
            return total;
        }

        /// <summary>Returns true if this battlefield has at least one unit on either side.</summary>
        public bool IsContested()
        {
            return PlayerUnits.Count > 0 || EnemyUnits.Count > 0;
        }

        /// <summary>Returns true if the given owner has a unit here.</summary>
        public bool HasUnits(string owner)
        {
            return owner == GameRules.OWNER_PLAYER
                ? PlayerUnits.Count > 0
                : EnemyUnits.Count > 0;
        }

        /// <summary>Returns true if the given owner can still place units here (no cap).</summary>
        public bool HasSlot(string owner) => true;

        public override string ToString()
        {
            return $"BF{Id} ctrl:{Ctrl} P:{PlayerUnits.Count} E:{EnemyUnits.Count}";
        }
    }
}
