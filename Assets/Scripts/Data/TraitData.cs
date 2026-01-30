using UnityEngine;
using System;

namespace Crestforge.Data
{
    /// <summary>
    /// Defines a trait (synergy) that units can have.
    /// Traits provide bonuses when you have enough units with the same trait.
    /// </summary>
    [CreateAssetMenu(fileName = "New Trait", menuName = "Crestforge/Trait")]
    public class TraitData : ScriptableObject
    {
        [Header("Basic Info")]
        public string traitId;
        public string traitName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Trait Type")]
        public TraitCategory category;

        [Header("Tier Thresholds")]
        [Tooltip("Number of units needed to activate each tier (e.g., 2, 4, 6)")]
        public int[] tierThresholds = { 2, 4, 6 };

        [Header("Tier Bonuses")]
        public TraitBonus[] tierBonuses;

        /// <summary>
        /// Get the active tier based on unit count (-1 if not active)
        /// </summary>
        public int GetActiveTier(int unitCount)
        {
            int activeTier = -1;
            for (int i = 0; i < tierThresholds.Length; i++)
            {
                if (unitCount >= tierThresholds[i])
                {
                    activeTier = i;
                }
            }
            return activeTier;
        }

        /// <summary>
        /// Get the bonus for a specific tier
        /// </summary>
        public TraitBonus GetBonusForTier(int tier)
        {
            if (tier >= 0 && tier < tierBonuses.Length)
            {
                return tierBonuses[tier];
            }
            return null;
        }

        /// <summary>
        /// Get a color representing this trait's category (for UI display)
        /// </summary>
        public Color GetTraitColor()
        {
            return category switch
            {
                TraitCategory.Origin => new Color(0.4f, 0.6f, 0.9f),  // Blue for origins
                TraitCategory.Class => new Color(0.9f, 0.6f, 0.3f),   // Orange for classes
                _ => Color.white
            };
        }

        /// <summary>
        /// Legacy property for compatibility - returns tierThresholds
        /// </summary>
        public int[] thresholds => tierThresholds;
    }

    public enum TraitCategory
    {
        Origin,
        Class
    }

    /// <summary>
    /// Defines the bonus granted at each trait tier
    /// </summary>
    [Serializable]
    public class TraitBonus
    {
        [Header("Stat Bonuses (applied to units with this trait)")]
        public int bonusHealth;
        public int bonusAttack;
        public int bonusArmor;
        public int bonusMagicResist;
        public float bonusAttackSpeed; // Percentage (0.2 = 20%)

        [Header("Stat Bonuses (applied to ALL friendly units)")]
        public int globalBonusHealth;
        public int globalBonusAttack;
        public int globalBonusArmor;
        public int globalBonusMagicResist;
        public float globalBonusAttackSpeed;

        [Header("Special Effects")]
        public TraitEffect specialEffect;
        public float effectValue1;
        public float effectValue2;
        public float effectValue3;

        [Header("Description")]
        [TextArea(1, 2)]
        public string bonusDescription;
    }

    /// <summary>
    /// Special effects that traits can grant
    /// </summary>
    public enum TraitEffect
    {
        None,
        
        // Combat Start Effects
        JumpToBackline,         // Assassins jump to enemy backline
        GainShield,             // Start with a shield (value1 = amount)
        
        // On Hit Effects
        Lifesteal,              // Heal for % of damage (value1 = percent)
        ApplyBurn,              // Deal fire damage over time (value1 = damage, value2 = duration)
        ApplyPoison,            // Deal poison damage over time
        ReduceArmor,            // Reduce target armor (value1 = amount, value2 = duration)
        ChainLightning,         // Attack chains to nearby enemies (value1 = damage %, value2 = targets)
        
        // On Damage Taken Effects
        Thorns,                 // Reflect damage (value1 = percent)
        GainMana,               // Bonus mana on hit (value1 = amount)
        
        // On Kill Effects
        HealOnKill,             // Heal when killing (value1 = amount)
        ManaOnKill,             // Gain mana on kill (value1 = amount)
        SummonOnKill,           // Summon unit on kill (value1 = summon id)
        
        // On Death Effects
        Resurrect,              // Come back to life (value1 = health %)
        ExplodeOnDeath,         // Deal damage on death (value1 = damage, value2 = radius)
        BuffAlliesOnDeath,      // Buff remaining allies (value1 = stat boost %)
        
        // Periodic Effects
        Regeneration,           // Heal over time (value1 = amount per second)
        ManaRegen,              // Gain mana over time
        
        // Ability Modifiers
        AbilityDamageBonus,     // Abilities deal more damage (value1 = percent)
        ReducedManaCost,        // Abilities cost less mana (value1 = percent)
        DoubleCast,             // Chance to cast ability twice (value1 = percent)
        
        // Unique Mechanics
        PackHunter,             // Bonus damage per nearby ally with same trait (value1 = % per ally)
        ExecuteThreshold,       // Instant kill below health % (value1 = threshold)
        DamageReductionLowHealth, // Take less damage when low (value1 = threshold, value2 = reduction %)
        BonusDamageHighHealth,  // Deal more damage to high health targets (value1 = threshold, value2 = bonus %)
    }
}