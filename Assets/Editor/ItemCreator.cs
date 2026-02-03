using UnityEngine;
using UnityEditor;
using Crestforge.Core;
using Crestforge.Data;

namespace Crestforge.Editor
{
    public class ItemCreator : EditorWindow
    {
        [MenuItem("Crestforge/Create Placeholder Items")]
        public static void CreateItems()
        {
            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources/ScriptableObjects/Items"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources/ScriptableObjects"))
                {
                    AssetDatabase.CreateFolder("Assets/Resources", "ScriptableObjects");
                }
                AssetDatabase.CreateFolder("Assets/Resources/ScriptableObjects", "Items");
            }

            string basePath = "Assets/Resources/ScriptableObjects/Items";
            int created = 0;

            // Common Items (basic stat boosts)
            created += CreateItem(basePath, "IronSword", "Iron Sword",
                "A sturdy blade forged from iron.",
                ItemRarity.Common, bonusAttack: 10);

            created += CreateItem(basePath, "WoodenShield", "Wooden Shield",
                "A simple shield that provides basic protection.",
                ItemRarity.Common, bonusArmor: 10);

            created += CreateItem(basePath, "HealthPotion", "Health Potion",
                "A vial of restorative liquid.",
                ItemRarity.Common, bonusHealth: 100);

            created += CreateItem(basePath, "ManaGem", "Mana Gem",
                "A gem that pulses with arcane energy.",
                ItemRarity.Common, bonusMana: 15);

            created += CreateItem(basePath, "SwiftBoots", "Swift Boots",
                "Light footwear that increases agility.",
                ItemRarity.Common, bonusAttackSpeed: 0.15f);

            // Uncommon Items (better stats or simple effects)
            created += CreateItem(basePath, "SteelBlade", "Steel Blade",
                "A well-crafted sword of tempered steel.",
                ItemRarity.Uncommon, bonusAttack: 20);

            created += CreateItem(basePath, "ChainMail", "Chain Mail",
                "Interlocking metal rings provide solid defense.",
                ItemRarity.Uncommon, bonusArmor: 20, bonusHealth: 50);

            created += CreateItem(basePath, "VampireFang", "Vampire Fang",
                "Attacks heal for a portion of damage dealt.",
                ItemRarity.Uncommon, bonusAttack: 10,
                effect: ItemEffect.Lifesteal, effectValue1: 0.15f);

            created += CreateItem(basePath, "MagicCloak", "Magic Cloak",
                "A cloak woven with protective enchantments.",
                ItemRarity.Uncommon, bonusMagicResist: 25, bonusHealth: 50);

            created += CreateItem(basePath, "BerserkerGloves", "Berserker Gloves",
                "Gloves that fuel aggressive combat.",
                ItemRarity.Uncommon, bonusAttack: 15, bonusAttackSpeed: 0.2f);

            // Rare Items (significant stats and effects)
            created += CreateItem(basePath, "FlamingSword", "Flaming Sword",
                "A blade wreathed in magical fire. Burns enemies on hit.",
                ItemRarity.Rare, bonusAttack: 25,
                effect: ItemEffect.Burn, effectValue1: 20, effectValue2: 3);

            created += CreateItem(basePath, "GuardianShield", "Guardian Shield",
                "A legendary shield that reduces all damage taken.",
                ItemRarity.Rare, bonusArmor: 30, bonusHealth: 100,
                effect: ItemEffect.DamageReduction, effectValue1: 0.1f);

            created += CreateItem(basePath, "ArcaneStaff", "Arcane Staff",
                "Increases ability power significantly.",
                ItemRarity.Rare, bonusMana: 25,
                effect: ItemEffect.AbilityPower, effectValue1: 0.25f);

            created += CreateItem(basePath, "BloodThirster", "Bloodthirster",
                "A cursed blade that drains life from enemies.",
                ItemRarity.Rare, bonusAttack: 30,
                effect: ItemEffect.Lifesteal, effectValue1: 0.25f);

            created += CreateItem(basePath, "ThornMail", "Thornmail",
                "Armor covered in spikes that damage attackers.",
                ItemRarity.Rare, bonusArmor: 40,
                effect: ItemEffect.Thorns, effectValue1: 0.25f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Item Creator",
                $"Created {created} placeholder items in {basePath}", "OK");
        }

        private static int CreateItem(string basePath, string fileName, string itemName, string description,
            ItemRarity rarity, int bonusHealth = 0, int bonusAttack = 0, int bonusArmor = 0,
            int bonusMagicResist = 0, float bonusAttackSpeed = 0, int bonusMana = 0,
            ItemEffect effect = ItemEffect.None, float effectValue1 = 0, float effectValue2 = 0)
        {
            string path = $"{basePath}/{fileName}.asset";

            // Skip if already exists
            if (AssetDatabase.LoadAssetAtPath<ItemData>(path) != null)
            {
                Debug.Log($"[ItemCreator] Item already exists: {fileName}");
                return 0;
            }

            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = fileName.ToLower();
            item.itemName = itemName;
            item.description = description;
            item.rarity = rarity;
            item.bonusHealth = bonusHealth;
            item.bonusAttack = bonusAttack;
            item.bonusArmor = bonusArmor;
            item.bonusMagicResist = bonusMagicResist;
            item.bonusAttackSpeed = bonusAttackSpeed;
            item.bonusMana = bonusMana;
            item.effect = effect;
            item.effectValue1 = effectValue1;
            item.effectValue2 = effectValue2;

            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"[ItemCreator] Created item: {itemName}");
            return 1;
        }

        [MenuItem("Crestforge/Load Items Into GameState")]
        public static void LoadItemsIntoGameState()
        {
            // Find all items
            string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/Resources/ScriptableObjects/Items" });

            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Load Items", "No items found. Run 'Create Placeholder Items' first.", "OK");
                return;
            }

            ItemData[] items = new ItemData[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                items[i] = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            }

            // Find GameState prefab or scene instance
            // For now, just log what was found
            Debug.Log($"[ItemCreator] Found {items.Length} items. Add them to GameState.allItems in the Inspector.");

            foreach (var item in items)
            {
                Debug.Log($"  - {item.itemName} ({item.rarity})");
            }

            EditorUtility.DisplayDialog("Load Items",
                $"Found {items.Length} items.\n\nTo use them:\n1. Select GameState in the scene\n2. Drag items to the 'All Items' array", "OK");
        }
    }
}
