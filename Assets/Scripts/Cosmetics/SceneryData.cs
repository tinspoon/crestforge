using UnityEngine;

namespace Crestforge.Cosmetics
{
    /// <summary>
    /// Categories of scenery items
    /// </summary>
    public enum SceneryCategory
    {
        Flag,       // Customizable banners/flags
        Toy,        // Fun objects, mascots, toys
        Plant,      // Trees, flowers, bushes
        Statue,     // Trophies, monuments
        Creature,   // Pets, small animals
        Furniture   // Benches, crates, barrels
    }

    /// <summary>
    /// Rarity tiers for cosmetics
    /// </summary>
    public enum SceneryRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// How a cosmetic is unlocked
    /// </summary>
    public enum UnlockType
    {
        Default,        // Everyone has it
        Achievement,    // Unlock via achievement
        Progression,    // Unlock at certain level/wins
        Purchase,       // Buy with premium currency
        Event           // Limited time event
    }

    /// <summary>
    /// Types of slots where scenery can be placed
    /// </summary>
    public enum SlotType
    {
        Flag,           // Only flags allowed
        Toy,            // Toys and small objects
        Any             // Any category
    }

    /// <summary>
    /// Definition of a scenery item (cosmetic)
    /// </summary>
    [System.Serializable]
    public class SceneryItemData
    {
        public string itemId;
        public string itemName;
        public string description;
        public SceneryCategory category;
        public SceneryRarity rarity;
        public UnlockType unlockType;
        public string unlockRequirement; // Achievement ID, level required, etc.
        public int purchasePrice;        // If purchasable

        // Visual
        public GameObject prefab;        // Custom prefab if available
        public bool hasIdleAnimation;
        public bool reactsToCombat;      // Cheers, cowers, etc.

        public Color GetRarityColor()
        {
            return rarity switch
            {
                SceneryRarity.Common => new Color(0.6f, 0.6f, 0.6f),
                SceneryRarity.Rare => new Color(0.3f, 0.5f, 0.9f),
                SceneryRarity.Epic => new Color(0.7f, 0.3f, 0.9f),
                SceneryRarity.Legendary => new Color(1f, 0.8f, 0.2f),
                _ => Color.white
            };
        }
    }

    /// <summary>
    /// Player's customization for a flag
    /// </summary>
    [System.Serializable]
    public class FlagCustomization
    {
        public Color primaryColor = new Color(0.8f, 0.2f, 0.2f);   // Main banner color
        public Color secondaryColor = new Color(1f, 0.9f, 0.3f);  // Accent/trim color
        public int emblemIndex = 0;  // Index into emblem sprites

        public FlagCustomization() { }

        public FlagCustomization(Color primary, Color secondary, int emblem = 0)
        {
            primaryColor = primary;
            secondaryColor = secondary;
            emblemIndex = emblem;
        }
    }

    /// <summary>
    /// A placed scenery item instance
    /// </summary>
    [System.Serializable]
    public class PlacedScenery
    {
        public string slotId;           // Which slot it's in
        public string itemId;           // Which item is placed
        public FlagCustomization flagCustomization; // Only for flags

        public PlacedScenery() { }

        public PlacedScenery(string slot, string item)
        {
            slotId = slot;
            itemId = item;
        }
    }

    /// <summary>
    /// Definition of a placement slot
    /// </summary>
    [System.Serializable]
    public class ScenerySlotData
    {
        public string slotId;
        public string displayName;
        public SlotType slotType;
        public Vector3 position;
        public float rotationY;         // Facing direction
        public Vector3 scale = Vector3.one;
    }
}
