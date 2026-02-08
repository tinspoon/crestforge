namespace Crestforge.Core
{
    /// <summary>
    /// Types of damage in combat.
    /// Physical is the default autoattack type. Elemental types are used by abilities and Attuned.
    /// </summary>
    public enum DamageType
    {
        Physical,   // Default autoattack damage, excluded from Attuned rolls
        Fire,       // Elemental - fire spells, burns
        Arcane,     // Elemental - magic missiles, arcane blasts
        Nature,     // Elemental - poison, thorns, healing
        Shadow      // Elemental - dark magic, critical strikes
    }

    /// <summary>
    /// Status effect types that can be applied in combat
    /// </summary>
    public enum StatusEffectType
    {
        None,
        Bleed,      // Physical DoT
        Frost,      // Movement + attack speed slow
        Burn,       // Fire DoT
        Poison,     // Nature DoT
        Stun,       // Cannot act
        Root,       // Cannot move
        Shield,     // Absorbs damage
        AttackSpeedBuff,
        AttackSpeedDebuff,
        DamageReduction,
        ArmorShred,
        HealOverTime
    }

    /// <summary>
    /// Per-game Attuned element roll (excludes Physical)
    /// </summary>
    public enum AttunedElement
    {
        Fire,
        Arcane,
        Nature,
        Shadow
    }

    /// <summary>
    /// Per-game Blessed bonus effect at 2/4 breakpoints
    /// </summary>
    public enum BlessedBonus
    {
        Life,       // Omnivamp for all allies
        Mana,       // Starting mana for all allies
        Unity,      // Bonus damage per ally on board
        Devotion,   // Blessed individual buffs doubled (2) / tripled (4)
        Aegis,      // Shield at combat start
        Vigor,      // HP regen per second
        Fortune     // Bonus gold per round
    }

    /// <summary>
    /// Per-game Warlord Physical enhancement
    /// </summary>
    public enum WarlordEnhancement
    {
        Bloodlust,  // Lifesteal on Physical damage
        Precision,  // Crit chance on Physical attacks
        Frenzy,     // Attack speed buff
        Brutality,  // Flat AD bonus
        Shatter     // Armor penetration
    }

    /// <summary>
    /// Individual Blessed stat buff type (each Blessed unit provides one)
    /// </summary>
    public enum BlessedStatType
    {
        AttackDamage,   // +5 AD (Cleric)
        Health,         // +100 HP (Dryad)
        AbilityPower,   // +5 AP (Mage)
        AttackSpeed,    // +10% AS (Druid)
        CritChance,     // +10% Crit (Naga Wizard)
        Armor,          // +10 Armor (Treeant)
        MagicResist     // +10 MR (Bishop Knight)
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
