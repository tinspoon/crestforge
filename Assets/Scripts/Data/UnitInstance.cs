using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;

namespace Crestforge.Data
{
    /// <summary>
    /// Type of loot dropped by PvE units
    /// </summary>
    public enum LootType
    {
        None,
        CrestToken,     // Drops minor crest selection
        ItemAnvil       // Drops item selection
    }

    /// <summary>
    /// Runtime instance of a unit. Created from UnitData template.
    /// Tracks current state, items, star level, etc.
    /// </summary>
    [System.Serializable]
    public class UnitInstance
    {
        // Identity
        public string instanceId;
        public UnitData template;
        public int starLevel = 1;

        // Current Stats (modified by items, traits, crests)
        public UnitStats currentStats;

        // Equipment
        public List<ItemData> equippedItems = new List<ItemData>();

        // Board Position
        public bool isOnBoard;
        public Vector2Int boardPosition;

        // Combat State
        public int currentHealth;
        public int currentMana;
        public int currentShield;
        public CombatState combatState;
        public UnitInstance currentTarget;
        public float attackCooldown;
        public List<StatusEffect> buffs = new List<StatusEffect>();
        public List<StatusEffect> debuffs = new List<StatusEffect>();

        // Flags
        public bool hasActedThisTick;
        public bool hasUsedRevive;

        // PvE Loot (for enemy units)
        public LootType lootType = LootType.None;

        /// <summary>
        /// Create a new unit instance from a template
        /// </summary>
        public static UnitInstance Create(UnitData template, int starLevel = 1)
        {
            var instance = new UnitInstance
            {
                instanceId = System.Guid.NewGuid().ToString(),
                template = template,
                starLevel = starLevel,
                currentStats = template.GetStatsForStarLevel(starLevel),
                isOnBoard = false,
                boardPosition = new Vector2Int(-1, -1),
                combatState = CombatState.Idle
            };

            instance.currentHealth = instance.currentStats.health;
            instance.currentMana = instance.currentStats.startingMana;
            instance.currentShield = 0;

            return instance;
        }

        /// <summary>
        /// Recalculate stats based on star level, items, and external bonuses
        /// </summary>
        public void RecalculateStats(UnitStats traitBonus = null, UnitStats crestBonus = null)
        {
            // Start with base stats for star level
            currentStats = template.GetStatsForStarLevel(starLevel);

            // Add item bonuses
            foreach (var item in equippedItems)
            {
                currentStats.health += item.bonusHealth;
                currentStats.attack += item.bonusAttack;
                currentStats.armor += item.bonusArmor;
                currentStats.magicResist += item.bonusMagicResist;
                currentStats.attackSpeed += item.bonusAttackSpeed;
                currentStats.startingMana += item.bonusMana;
            }

            // Add trait bonuses
            if (traitBonus != null)
            {
                currentStats.Add(traitBonus);
            }

            // Add crest bonuses
            if (crestBonus != null)
            {
                currentStats.Add(crestBonus);
            }

            // Update max health if needed
            if (currentHealth > currentStats.health)
            {
                currentHealth = currentStats.health;
            }
        }

        /// <summary>
        /// Check if unit can equip another item
        /// </summary>
        public bool CanEquipItem()
        {
            return equippedItems.Count < GameConstants.Items.MAX_PER_UNIT;
        }

        /// <summary>
        /// Equip an item to this unit
        /// </summary>
        public bool EquipItem(ItemData item)
        {
            if (!CanEquipItem()) return false;
            
            equippedItems.Add(item);
            RecalculateStats();
            return true;
        }

        /// <summary>
        /// Remove an item from this unit
        /// </summary>
        public ItemData UnequipItem(int index)
        {
            if (index < 0 || index >= equippedItems.Count) return null;
            
            var item = equippedItems[index];
            equippedItems.RemoveAt(index);
            RecalculateStats();
            return item;
        }

        /// <summary>
        /// Reset combat state for a new battle
        /// </summary>
        public void ResetForCombat()
        {
            currentHealth = currentStats.health;
            currentMana = currentStats.startingMana;
            currentShield = 0;
            combatState = CombatState.Idle;
            currentTarget = null;
            attackCooldown = 0;
            buffs.Clear();
            debuffs.Clear();
            hasActedThisTick = false;
            hasUsedRevive = false;
        }

        /// <summary>
        /// Check if this unit is dead
        /// </summary>
        public bool IsDead => combatState == CombatState.Dead || currentHealth <= 0;

        /// <summary>
        /// Check if this unit is alive
        /// </summary>
        public bool IsAlive => !IsDead;

        /// <summary>
        /// Get the sell value of this unit
        /// </summary>
        public int GetSellValue()
        {
            return template.cost * starLevel;
        }

        /// <summary>
        /// Get display name with star level
        /// </summary>
        public string GetDisplayName()
        {
            string stars = new string('★', starLevel);
            return $"{template.unitName} {stars}";
        }

        /// <summary>
        /// Check if unit has a specific trait
        /// </summary>
        public bool HasTrait(TraitData trait)
        {
            return template.HasTrait(trait);
        }

        /// <summary>
        /// Get health percentage (0-1)
        /// </summary>
        public float GetHealthPercent()
        {
            return (float)currentHealth / currentStats.health;
        }

        /// <summary>
        /// Get mana percentage (0-1)
        /// </summary>
        public float GetManaPercent()
        {
            return (float)currentMana / currentStats.maxMana;
        }
    }

    /// <summary>
    /// Represents a buff or debuff on a unit
    /// </summary>
    [System.Serializable]
    public class StatusEffect
    {
        public string effectId;
        public string effectName;
        public float duration;
        public float remainingDuration;

        // Stat modifiers
        public int healthMod;
        public int attackMod;
        public int armorMod;
        public int magicResistMod;
        public float attackSpeedMod;

        // DoT/HoT
        public int damagePerSecond;
        public int healPerSecond;
        public DamageType dotDamageType;

        // Special flags
        public bool isStun;
        public bool isSlow;
        public bool isRoot;

        public bool IsExpired => remainingDuration <= 0;

        public void Tick(float deltaTime)
        {
            remainingDuration -= deltaTime;
        }
    }
}
