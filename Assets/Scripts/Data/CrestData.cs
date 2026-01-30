using UnityEngine;
using Crestforge.Core;

namespace Crestforge.Data
{
    /// <summary>
    /// Defines a crest (passive team-wide effect)
    /// </summary>
    [CreateAssetMenu(fileName = "New Crest", menuName = "Crestforge/Crest")]
    public class CrestData : ScriptableObject
    {
        [Header("Basic Info")]
        public string crestId;
        public string crestName;
        [TextArea(2, 4)]
        public string description;
        public CrestType type;
        public Sprite icon;

        [Header("Synergy Bonus")]
        [Tooltip("Optional: This crest works best with this trait")]
        public TraitData synergyTrait;

        [Header("Stat Bonuses (All Units)")]
        public int bonusHealth;
        public int bonusAttack;
        public int bonusArmor;
        public int bonusMagicResist;
        public float bonusAttackSpeed;
        public int bonusMana;

        [Header("Special Effect")]
        public CrestEffect effect;
        public float effectValue1;
        public float effectValue2;
        public float effectValue3;

        [Header("Conditional Bonuses")]
        [Tooltip("Only applies to units matching this condition")]
        public CrestCondition condition;
        public TraitData conditionalTrait;
        public int conditionalBonusHealth;
        public int conditionalBonusAttack;
        public float conditionalBonusAttackSpeed;
    }

    public enum CrestEffect
    {
        None,
        
        // Combat Start
        AllUnitsShield,         // All units gain shield (value1 = amount)
        
        // On-Hit (All Units)
        AllUnitsPoison,         // All attacks poison (value1 = damage, value2 = duration)
        AllUnitsBurn,           // All attacks burn (value1 = damage, value2 = duration)
        AllUnitsLifesteal,      // All units lifesteal (value1 = percent)
        
        // Conditional Combat
        LowHealthDamageBoost,   // Units below HP% deal more damage (value1 = threshold, value2 = bonus %)
        LowHealthDefenseBoost,  // Units below HP% take less damage
        HighHealthDamageBoost,  // Bonus damage to targets above HP%
        
        // On Death
        AllyDeathAttackSpeed,   // When ally dies, others gain attack speed (value1 = percent, value2 = duration)
        AllyDeathHeal,          // When ally dies, others heal (value1 = amount)
        
        // Ability Enhancement
        AllAbilityDamage,       // All abilities deal bonus damage (value1 = percent)
        AllManaCostReduction,   // All abilities cost less mana (value1 = percent)
        
        // Class/Origin Specific
        TraitAttackSpeed,       // Units with synergy trait gain attack speed (value1 = percent)
        TraitDamage,            // Units with synergy trait deal more damage
        TraitManaGain,          // Units with synergy trait gain more mana
    }

    public enum CrestCondition
    {
        None,
        HasTrait,           // Unit must have the specified trait
        IsMelee,            // Unit must be melee (range 1)
        IsRanged,           // Unit must be ranged (range > 1)
        LowHealth,          // Unit is below 50% HP
        HighMana,           // Unit has 50%+ mana
    }
}
