using UnityEngine;
using Crestforge.Core;

namespace Crestforge.Data
{
    /// <summary>
    /// Defines an equippable item
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "Crestforge/Item")]
    public class ItemData : ScriptableObject
    {
        [Header("Basic Info")]
        public string itemId;
        public string itemName;
        [TextArea(2, 4)]
        public string description;
        public ItemRarity rarity;
        public Sprite icon;

        [Header("Stat Bonuses")]
        public int bonusHealth;
        public int bonusAttack;
        public int bonusArmor;
        public int bonusMagicResist;
        public float bonusAttackSpeed;
        public int bonusMana;

        [Header("Special Effect")]
        public ItemEffect effect;
        public float effectValue1;
        public float effectValue2;

        /// <summary>
        /// Get the weighted rarity value for random selection
        /// </summary>
        public int GetRarityWeight()
        {
            switch (rarity)
            {
                case ItemRarity.Common: return 50;
                case ItemRarity.Uncommon: return 35;
                case ItemRarity.Rare: return 15;
                default: return 50;
            }
        }
    }

    public enum ItemEffect
    {
        None,
        
        // On-Hit Effects
        Lifesteal,          // Heal for % of damage dealt (value1 = percent)
        Burn,               // Apply fire DoT (value1 = damage, value2 = duration)
        Slow,               // Reduce target attack speed (value1 = percent, value2 = duration)
        ArmorShred,         // Reduce target armor (value1 = amount, value2 = duration)
        CriticalStrike,     // Chance to crit (value1 = chance, value2 = multiplier)
        
        // Defensive Effects
        Thorns,             // Reflect damage (value1 = percent)
        SpellShield,        // Block first ability (value1 = charges)
        DamageReduction,    // Reduce damage taken (value1 = percent)
        
        // On-Death Effects
        Revive,             // Resurrect with HP (value1 = health amount)
        
        // Ability Effects  
        AbilityPower,       // Increase ability damage (value1 = percent)
        ManaOnHit,          // Gain extra mana on hit (value1 = amount)
    }
}
