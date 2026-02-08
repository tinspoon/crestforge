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
        public int cost; // 1-5 gold

        [Header("Damage")]
        [Tooltip("Primary damage affinity (Physical for most autoattackers, elemental for casters)")]
        public DamageType damageAffinity = DamageType.Physical;
        [Tooltip("If true, autoattacks deal damageAffinity type instead of Physical")]
        public bool elementalAutoattacks;

        [Header("Traits")]
        [Tooltip("Assign 2 traits to this unit")]
        public TraitData[] traits;

        [Header("Blessed")]
        [Tooltip("Only set for Blessed units - which stat buff this unit provides")]
        public BlessedStatType blessedStatType;
        [Tooltip("Whether this unit is a Blessed unit with an always-active stat buff")]
        public bool isBlessed;

        [Header("Base Stats (1-Star)")]
        public UnitStats baseStats;

        [Header("Ability")]
        public AbilityData ability;

        [Header("Visuals")]
        public Sprite portrait;
        public Sprite[] spriteSheet;
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
                abilityPower = Mathf.RoundToInt(baseStats.abilityPower * multiplier),
                armor = baseStats.armor,
                magicResist = baseStats.magicResist,
                attackSpeed = baseStats.attackSpeed,
                range = baseStats.range,
                moveSpeed = baseStats.moveSpeed,
                startingMana = baseStats.startingMana,
                maxMana = baseStats.maxMana,
                critChance = baseStats.critChance,
                critDamage = baseStats.critDamage
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
        public int abilityPower = 0;
        public float attackSpeed = 0.8f; // Attacks per second
        public int range = 1; // 1 = melee, 2+ = ranged
        public float critChance = 0f;    // 0-1 (0.1 = 10%)
        public float critDamage = 1.5f;  // Crit multiplier (1.5 = 150%)

        [Header("Movement")]
        public float moveSpeed = 2f;

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
                abilityPower = this.abilityPower,
                attackSpeed = this.attackSpeed,
                range = this.range,
                moveSpeed = this.moveSpeed,
                startingMana = this.startingMana,
                maxMana = this.maxMana,
                critChance = this.critChance,
                critDamage = this.critDamage
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
            abilityPower += other.abilityPower;
            attackSpeed += other.attackSpeed;
            startingMana += other.startingMana;
            critChance += other.critChance;
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
        public float duration;
        public int radius;
        public int projectileCount;
        public float attackSpeedBonus;

        [Header("Status Effect")]
        public StatusEffectType appliesEffect = StatusEffectType.None;
        public float effectDuration;
        public int effectDamagePerSecond;     // For DoTs (Bleed/Burn)
        public float effectSlowPercent;        // For Frost

        [Header("Special")]
        public float selfHealPercent;          // % of damage dealt healed
        public bool teleportsToTarget;         // Backstab, Shadow Dive
        public bool piercesTargets;            // Line pierce
        public bool chainsOnKill;              // Chain to next target on kill
        public int chainDamage;                // Damage for chain
        public bool grantsPermStats;           // For on-kill permanent stat gains
        public int permStatAmount;             // Amount of permanent stats

        [Header("Targeting")]
        public AbilityTargeting targeting;

        [Header("Visuals")]
        public GameObject effectPrefab;
        public AudioClip soundEffect;
    }

    public enum AbilityType
    {
        Damage,
        Heal,
        Shield,
        Buff,
        Debuff,
        Summon,
        DamageAndHeal,
        AreaDamage,
        Movement,
        DamageAndBuff,      // Deals damage and buffs self/allies
        HealAndBuff,        // Heals and buffs
        TeamBuff            // Buffs all allies (Blacksmith)
    }

    public enum AbilityTargeting
    {
        CurrentTarget,
        LowestHealthEnemy,
        HighestHealthEnemy,
        LowestHealthAlly,
        AllEnemies,
        AllAllies,
        NearbyEnemies,
        NearbyAllies,
        Self,
        RandomEnemy,
        BacklineEnemy,
        FarthestEnemy,      // For charge abilities
        AdjacentEnemies,    // For cone/splash
        LineFromCaster      // For piercing shots
    }
}
