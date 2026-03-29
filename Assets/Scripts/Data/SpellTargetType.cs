namespace FWTCG.Data
{
    /// <summary>
    /// Defines what a spell card can target when played.
    /// </summary>
    public enum SpellTargetType
    {
        None,          // No target — spell resolves immediately
        EnemyUnit,     // Must target an enemy unit (base or battlefield)
        FriendlyUnit,  // Must target a friendly unit (base or battlefield)
        AnyUnit,       // Can target any unit from either side
    }
}
