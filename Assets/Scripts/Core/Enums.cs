namespace Crestforge.Core
{
    /// <summary>
    /// Types of damage in combat.
    /// Units deal one primary damage type.
    /// </summary>
    public enum DamageType
    {
        Physical,   // Standard weapon damage
        Elemental,  // Fire, Frost, Lightning, Nature
        Dark,       // Shadow, Void, Death
        Holy        // Light, Divine, Healing
    }

    /// <summary>
    /// Rarity tiers for items
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare
    }

    /// <summary>
    /// Crest types
    /// </summary>
    public enum CrestType
    {
        Minor,
        Major
    }

    /// <summary>
    /// Game phases
    /// </summary>
    public enum GamePhase
    {
        Planning,
        Combat,
        Results,
        ItemSelect,
        CrestSelect,
        GameOver
    }

    /// <summary>
    /// Combat unit states
    /// </summary>
    public enum CombatState
    {
        Idle,
        Moving,
        Attacking,
        CastingAbility,
        Stunned,
        Dead
    }
}
