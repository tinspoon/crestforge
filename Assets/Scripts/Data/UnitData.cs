using UnityEngine;
using Crestforge.Core;

namespace Crestforge.Data
{
    /// <summary>
    /// Defines a unit template. Create one of these for each unique unit type.
    /// </summary>
    [CreateAssetMenu(fileName = "New Unit", menuName = "Crestforge/Unit")]
    public class UnitData : ScriptableObject
    {
        [Header("Basic Info")]
        public string unitId;
        public string unitName;
        [TextArea(2, 4)]
        public string description;
        public int cost; // 1-4 gold

        [Header("Traits")]
        [Tooltip("Assign 2-3 traits to this unit")]
        public TraitData[] traits;

        [Header("Base Stats (1-Star)")]
        public UnitStats baseStats;

        [Header("Ability")]
        public AbilityData ability;

        [Header("Visuals")]
        public Sprite portrait;
        public Sprite[] spriteSheet; // Idle, Walk, Attack, Ability, Death frames
        public RuntimeAnimatorController animator;

        /// <summary>
        /// Calculate stats for a given star level
        /// </summary>
        public UnitStats GetStatsForStarLevel(int starLevel)
        {
            float multiplier = GameConstants.Units.STAR_MULTIPLIERS[starLevel];
            
            return new UnitStats
            {
                health = Mathf.RoundToInt(baseStats.health * multiplier),
                attack = Mathf.RoundToInt(baseStats.attack * multiplier),
                armor = baseStats.armor, // Armor doesn't scale
                magicResist = baseStats.magicResist, // MR doesn't scale
                attackSpeed = baseStats.attackSpeed,
                range = baseStats.range,
                moveSpeed = baseStats.moveSpeed,
                startingMana = baseStats.startingMana,
                maxMana = baseStats.maxMana
            };
        }

        /// <summary>
        /// Check if this unit has a specific trait
        /// </summary>
        public bool HasTrait(TraitData trait)
        {
            foreach (var t in traits)
            {
                if (t == trait) return true;
            }
            return false;
        }

        /// <summary>
        /// Check if this unit has a trait with the given ID
        /// </summary>
        public bool HasTraitId(string traitId)
        {
            foreach (var t in traits)
            {
                if (t.traitId == traitId) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Unit statistics
    /// </summary>
    [System.Serializable]
    public class UnitStats
    {
        [Header("Health & Defense")]
        public int health = 500;
        public int armor = 20;
        public int magicResist = 20;

        [Header("Offense")]
        public int attack = 50;
        public float attackSpeed = 0.8f; // Attacks per second
        public int range = 1; // 1 = melee, 2+ = ranged

        [Header("Movement")]
        public float moveSpeed = 2f; // Hexes per second

        [Header("Mana")]
        public int startingMana = 0;
        public int maxMana = 100;

        /// <summary>
        /// Create a copy of these stats
        /// </summary>
        public UnitStats Clone()
        {
            return new UnitStats
            {
                health = this.health,
                armor = this.armor,
                magicResist = this.magicResist,
                attack = this.attack,
                attackSpeed = this.attackSpeed,
                range = this.range,
                moveSpeed = this.moveSpeed,
                startingMana = this.startingMana,
                maxMana = this.maxMana
            };
        }

        /// <summary>
        /// Add another stat block to this one
        /// </summary>
        public void Add(UnitStats other)
        {
            health += other.health;
            armor += other.armor;
            magicResist += other.magicResist;
            attack += other.attack;
            attackSpeed += other.attackSpeed;
            startingMana += other.startingMana;
        }
    }

    /// <summary>
    /// Defines a unit's special ability
    /// </summary>
    [System.Serializable]
    public class AbilityData
    {
        public string abilityName;
        [TextArea(2, 4)]
        public string description;
        
        public AbilityType type;
        public DamageType damageType;
        
        [Header("Values (scaled by star level)")]
        public int baseDamage;
        public int baseHealing;
        public int baseShieldAmount;
        public float duration; // For buffs/debuffs
        public int radius; // For AoE abilities (in hexes)
        public int projectileCount; // For multi-hit abilities
        public float attackSpeedBonus; // For attack speed buffs (e.g., 0.5 = +50% AS)

        [Header("Targeting")]
        public AbilityTargeting targeting;

        [Header("Visuals")]
        public GameObject effectPrefab;
        public AudioClip soundEffect;
    }

    public enum AbilityType
    {
        Damage,         // Deals damage to target(s)
        Heal,           // Heals ally/allies
        Shield,         // Grants shield to ally/allies
        Buff,           // Buffs ally/allies
        Debuff,         // Debuffs enemy/enemies
        Summon,         // Creates additional units
        DamageAndHeal,  // Deals damage and heals caster (lifedrain)
        AreaDamage,     // Damages all enemies in area
        Movement        // Special movement (dash, teleport)
    }

    public enum AbilityTargeting
    {
        CurrentTarget,      // Uses current attack target
        LowestHealthEnemy,  // Targets lowest health enemy
        HighestHealthEnemy, // Targets highest health enemy
        LowestHealthAlly,   // Targets lowest health ally
        AllEnemies,         // Hits all enemies
        AllAllies,          // Affects all allies
        NearbyEnemies,      // Enemies within radius
        NearbyAllies,       // Allies within radius
        Self,               // Self-cast
        RandomEnemy,        // Random enemy target
        BacklineEnemy       // Furthest enemy (backline)
    }
}
