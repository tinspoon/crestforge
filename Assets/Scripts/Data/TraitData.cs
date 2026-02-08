using UnityEngine;
using System;
using Crestforge.Core;

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
        public bool isUnique;  // If true, only one unit has this trait (always active at 1)
        public bool isAlwaysActive; // For Blessed: individual buffs always on, tier bonuses at 2/4

        [Header("Tier Thresholds")]
        [Tooltip("Number of units needed to activate each tier (e.g., 2, 4)")]
        public int[] tierThresholds = { 2, 4 };

        [Header("Tier Bonuses")]
        public TraitBonus[] tierBonuses;

        /// <summary>
        /// Get the active tier based on unit count (-1 if not active)
        /// </summary>
        public int GetActiveTier(int unitCount)
        {
            if (isUnique && unitCount >= 1) return 0;

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
        /// Get a color representing this trait type (for UI display)
        /// </summary>
        public Color GetTraitColor()
        {
            return isUnique
                ? new Color(0.9f, 0.7f, 0.2f)  // Gold for unique traits
                : new Color(0.4f, 0.7f, 0.9f); // Blue for shared traits
        }

        /// <summary>
        /// Legacy property for compatibility - returns tierThresholds
        /// </summary>
        public int[] thresholds => tierThresholds;
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
        public float bonusAttackSpeed;

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

        // === Vanguard ===
        VanguardTankiness,      // bonusHealth + bonusArmor at tier 2

        // === Legion ===
        LegionAdjDamage,        // Adjacent Legion units gain AD% (v1=%), tier 2 also grants Armor to adj (v2=armor)

        // === Wild ===
        WildRampingAS,          // +v1% AS per attack, max v2% (ramping, not flat)

        // === Shadow ===
        ShadowCritStealth,      // +v1% crit, first attack +v2% dmg, untargetable v3 seconds

        // === Ironclad ===
        IroncladDR,             // v1% damage reduction

        // === Cleave ===
        CleaveAdjSplash,        // v1% splash to adjacent enemies

        // === Cavalry ===
        CavalryCharge,          // +v1 move speed, charge stun v2 seconds, tier 2: +v3% charge dmg

        // === Dragon ===
        DragonFirePower,        // +v1% dmg, v2% Fire resist, tier 2: attacks apply Burn v3 dps

        // === Volatile ===
        VolatileDeathExplosion, // On death deal v1 Fire dmg to adjacent

        // === Nature ===
        NatureAdjHeal,          // Adjacent allies heal v1 HP/s, tier 2: +v2% heal effectiveness

        // === Attuned ===
        AttunedConvert,         // Convert ability damage to Attuned element, +v1% global same-type dmg

        // === Blessed ===
        BlessedPerGame,         // Per-game bonus effect at 2/4 (individual buffs always active)

        // === Warlord ===
        WarlordPhysical,        // Per-game Physical enhancement (v1 = magnitude)

        // === Forged ===
        ForgedStacking,         // +v1 AD/AP +v2 HP per round to Forged; tier 2: ALL units get v3 AD/AP/HP, Forged double

        // === Scavenger ===
        ScavengerReward,        // Guaranteed unit after round (tier 1: 1-2 cost, tier 2: 3-4 cost + 1g)

        // === Unique: Treasure ===
        TreasureOnWin,          // Random loot on win: gold, reroll, unit, or crest token

        // === Unique: Crestmaker ===
        CrestmakerCraft,        // Minor crest token every N rounds

        // Legacy effects kept for compatibility
        Lifesteal,
        ApplyBurn,
        Thorns,
        HealOnKill,
        Resurrect,
        ExplodeOnDeath,
        Regeneration,
        AbilityDamageBonus,
        GainShield
    }
}
