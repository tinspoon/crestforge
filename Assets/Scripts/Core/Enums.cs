namespace Crestforge.Core
{
    /// <summary>
    /// Types of damage in combat
    /// </summary>
    public enum DamageType
    {
        Physical,
        Magic,
        True,
        Poison,
        Fire
    }

    /// <summary>
    /// Unit classes (roles) - each unit has 1-2 of these
    /// </summary>
    public enum UnitClass
    {
        None = 0,
        Warrior,    // Frontline damage dealers
        Ranger,     // Backline physical damage
        Mage,       // Backline magic damage
        Tank,       // High durability, crowd control
        Assassin,   // High burst, targets backline
        Support,    // Healing and buffs
        Berserker,  // High risk, high reward damage
        Summoner    // Creates additional units
    }

    /// <summary>
    /// Unit origins (factions) - each unit has 1-2 of these
    /// </summary>
    public enum UnitOrigin
    {
        None = 0,
        Human,      // Versatile, teamwork bonuses
        Undead,     // Lifesteal, resurrection
        Beast,      // Attack speed, pack bonuses
        Elemental,  // Magic damage, spell effects
        Demon,      // High damage, health costs
        Fey         // Evasion, magic resistance
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
