#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Crestforge.Core;
using Crestforge.Systems;
using Crestforge.Combat;
using Crestforge.Data;
using Crestforge.UI;
using System.Collections.Generic;
using System.IO;

namespace Crestforge.Editor
{
    /// <summary>
    /// Complete setup - generates all data AND sets up the scene with proper linking
    /// </summary>
    public class CrestforgeSetup : EditorWindow
    {
        [MenuItem("Crestforge/Complete Setup (Generate + Scene)")]
        public static void CompleteSetup()
        {
            Debug.Log("=== CRESTFORGE COMPLETE SETUP ===");
            
            // Step 1: Generate all data
            GenerateAllTraits();
            GenerateAllUnits();
            GenerateAllItems();
            GenerateAllCrests();
            GeneratePvECritters();

            // Refresh asset database
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Step 2: Setup scene with direct asset references
            SetupSceneWithDirectLinks();
            
            Debug.Log("=== COMPLETE SETUP FINISHED ===");
            Debug.Log("Press PLAY to start the game!");
            Debug.Log("Remember to SAVE your scene (Ctrl+S)!");
        }

        [MenuItem("Crestforge/Setup Scene (One Click!)")]
        public static void SetupSceneOnly()
        {
            SetupSceneWithDirectLinks();
        }

        private static void SetupSceneWithDirectLinks()
        {
            Debug.Log("--- Setting up scene ---");

            // 1. GameState
            var gameStateGO = CreateOrFind<GameState>("GameState");
            var gameState = gameStateGO.GetComponent<GameState>();
            
            // Load assets directly using AssetDatabase (more reliable than Resources.LoadAll)
            // Use NewUnits and NewTraits folders for the updated 40-unit roster
            gameState.allUnits = LoadAssetsFromFolder<UnitData>("Assets/Resources/ScriptableObjects/NewUnits");
            gameState.allItems = LoadAssetsFromFolder<ItemData>("Assets/Resources/ScriptableObjects/Items");
            gameState.allCrests = LoadAssetsFromFolder<CrestData>("Assets/Resources/ScriptableObjects/Crests");
            gameState.allTraits = LoadAssetsFromFolder<TraitData>("Assets/Resources/ScriptableObjects/NewTraits");
            
            Debug.Log($"GameState linked: {gameState.allUnits.Length} units, {gameState.allItems.Length} items, {gameState.allCrests.Length} crests, {gameState.allTraits.Length} traits");

            // 2. RoundManager
            CreateOrFind<RoundManager>("RoundManager");

            // 3. CombatManager
            CreateOrFind<CombatManager>("CombatManager");

            // 4. EnemyWaveGenerator
            var enemyGenGO = CreateOrFind<EnemyWaveGenerator>("EnemyWaveGenerator");
            var enemyGen = enemyGenGO.GetComponent<EnemyWaveGenerator>();
            enemyGen.allUnits = gameState.allUnits;

            // 5. GameBootstrap - disable auto-start, menu will start the game
            var bootstrapGO = CreateOrFind<GameBootstrap>("GameBootstrap");
            bootstrapGO.GetComponent<GameBootstrap>().autoStartGame = false;

            // 6. DebugUI - disable old UI
            var debugUI = Object.FindFirstObjectByType<DebugUI>();
            if (debugUI != null)
            {
                debugUI.gameObject.SetActive(false);
            }

            // 7. GameUI (new proper UI) - will hide itself when menu is active
            CreateOrFind<GameUI>("GameUI");

            // 8. MainMenuUI (home screen)
            CreateOrFind<MainMenuUI>("MainMenu");

            // 9. HexGridRenderer
            CreateOrFind<HexGridRenderer>("HexGrid");

            // 10. CombatVisualizer (UI effects)
            CreateOrFind<Crestforge.UI.CombatVisualizer>("CombatVisualizer");

            // 11. ServerCombatVisualizer (multiplayer combat playback)
            CreateOrFind<Crestforge.Systems.ServerCombatVisualizer>("ServerCombatVisualizer");

            // 11. Camera Controller
            var mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.GetComponent<CameraController>() == null)
            {
                mainCamera.gameObject.AddComponent<CameraController>();
            }

            // 12. VFX System
            CreateOrFind<Crestforge.Visuals.VFXSystem>("VFXSystem");

            // 13. Arena Materials helper (for ProBuilder workflow) - optional, create manually if needed
            // CreateOrFind<Crestforge.Visuals.ArenaMaterials>("ArenaMaterials");

            // Note: ArenaScenery removed - use ProBuilder for manual scene design

            // Mark scene dirty
            EditorUtility.SetDirty(gameState);
            EditorUtility.SetDirty(enemyGen);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
            );

            Debug.Log("Scene setup complete!");
        }

        private static T[] LoadAssetsFromFolder<T>(string folderPath) where T : Object
        {
            var assets = new List<T>();
            
            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"Folder not found: {folderPath}");
                return assets.ToArray();
            }

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets.ToArray();
        }

        private static GameObject CreateOrFind<T>(string name) where T : MonoBehaviour
        {
            var existing = Object.FindFirstObjectByType<T>();
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = new GameObject(name);
            go.AddComponent<T>();
            return go;
        }

        // =============================================
        // DATA GENERATION
        // =============================================

        private static void GenerateAllTraits()
        {
            string path = "Assets/Resources/ScriptableObjects/Traits";
            EnsureDirectoryExists(path);
            
            // Clear existing
            DeleteAllAssetsInFolder(path);

            // Origins
            CreateTrait(path, "Human", false, new int[] { 2, 4, 6 },
                "Strength in unity",
                new TraitBonus[] {
                    new TraitBonus { bonusHealth = 25, bonusAttack = 5, bonusDescription = "+5% stats per Human" },
                    new TraitBonus { bonusHealth = 50, bonusAttack = 10, bonusDescription = "+10% stats per Human" },
                    new TraitBonus { bonusHealth = 75, bonusAttack = 15, bonusArmor = 10, bonusDescription = "+15% stats, +10 Armor" }
                });

            CreateTrait(path, "Undead", false, new int[] { 2, 4, 6 },
                "Death is only the beginning",
                new TraitBonus[] {
                    new TraitBonus { specialEffect = TraitEffect.Lifesteal, effectValue1 = 0.15f, bonusDescription = "15% Lifesteal" },
                    new TraitBonus { specialEffect = TraitEffect.Lifesteal, effectValue1 = 0.25f, bonusDescription = "25% Lifesteal" },
                    new TraitBonus { specialEffect = TraitEffect.Lifesteal, effectValue1 = 0.35f, bonusDescription = "35% Lifesteal + revive" }
                });

            CreateTrait(path, "Beast", false, new int[] { 2, 4, 6 },
                "The pack hunts as one",
                new TraitBonus[] {
                    new TraitBonus { bonusAttackSpeed = 0.15f, bonusDescription = "+15% Attack Speed" },
                    new TraitBonus { bonusAttackSpeed = 0.30f, bonusDescription = "+30% Attack Speed" },
                    new TraitBonus { bonusAttackSpeed = 0.50f, bonusDescription = "+50% AS, double strike" }
                });

            CreateTrait(path, "Elemental", false, new int[] { 2, 4 },
                "Primordial forces unleashed",
                new TraitBonus[] {
                    new TraitBonus { bonusAttack = 20, bonusDescription = "+20 magic damage" },
                    new TraitBonus { bonusAttack = 40, bonusDescription = "+40 magic damage, -20 MR" }
                });

            CreateTrait(path, "Demon", false, new int[] { 2, 4 },
                "Power demands sacrifice",
                new TraitBonus[] {
                    new TraitBonus { bonusAttack = 30, bonusDescription = "+30% damage" },
                    new TraitBonus { bonusAttack = 50, bonusDescription = "+50% damage, heal on kill" }
                });

            CreateTrait(path, "Fey", false, new int[] { 2, 4 },
                "Now you see me...",
                new TraitBonus[] {
                    new TraitBonus { bonusMagicResist = 20, bonusDescription = "20% dodge, +20 MR" },
                    new TraitBonus { bonusMagicResist = 40, bonusDescription = "35% dodge, +40 MR" }
                });

            // Classes
            CreateTrait(path, "Warrior", false, new int[] { 2, 4, 6 },
                "First into battle",
                new TraitBonus[] {
                    new TraitBonus { bonusArmor = 25, bonusDescription = "+25 Armor" },
                    new TraitBonus { bonusArmor = 50, bonusDescription = "+50 Armor, cleave" },
                    new TraitBonus { bonusArmor = 75, bonusDescription = "+75 Armor, 50% cleave" }
                });

            CreateTrait(path, "Ranger", false, new int[] { 2, 4 },
                "Death from afar",
                new TraitBonus[] {
                    new TraitBonus { bonusAttack = 20, bonusDescription = "+20 Attack, armor pen" },
                    new TraitBonus { bonusAttack = 40, bonusAttackSpeed = 0.2f, bonusDescription = "+40 Attack, 40% pen" }
                });

            CreateTrait(path, "Mage", false, new int[] { 2, 4, 6 },
                "Knowledge is power",
                new TraitBonus[] {
                    new TraitBonus { specialEffect = TraitEffect.AbilityDamageBonus, effectValue1 = 0.2f, bonusDescription = "+20% ability damage" },
                    new TraitBonus { specialEffect = TraitEffect.AbilityDamageBonus, effectValue1 = 0.4f, bonusDescription = "+40% ability damage" },
                    new TraitBonus { specialEffect = TraitEffect.AbilityDamageBonus, effectValue1 = 0.7f, bonusDescription = "+70% ability damage" }
                });

            CreateTrait(path, "Tank", false, new int[] { 2, 4 },
                "Unbreakable",
                new TraitBonus[] {
                    new TraitBonus { bonusHealth = 300, bonusDescription = "+300 HP, 10% DR" },
                    new TraitBonus { bonusHealth = 600, bonusDescription = "+600 HP, 20% DR" }
                });

            CreateTrait(path, "Assassin", false, new int[] { 2, 4 },
                "Strike from shadows",
                new TraitBonus[] {
                    new TraitBonus { specialEffect = TraitEffect.JumpToBackline, bonusDescription = "Jump backline, +25% crit" },
                    new TraitBonus { specialEffect = TraitEffect.JumpToBackline, bonusDescription = "Jump, +75% crit damage" }
                });

            CreateTrait(path, "Support", false, new int[] { 2, 4 },
                "Together we stand",
                new TraitBonus[] {
                    new TraitBonus { bonusDescription = "Heal lowest ally 50 HP/3s" },
                    new TraitBonus { globalBonusAttackSpeed = 0.15f, bonusDescription = "Heal all, +15% team AS" }
                });

            CreateTrait(path, "Berserker", false, new int[] { 2 },
                "Pain fuels rage",
                new TraitBonus[] {
                    new TraitBonus { bonusDescription = "+1% AS per 1% missing HP" }
                });

            CreateTrait(path, "Summoner", false, new int[] { 2, 4 },
                "Rise, my minions",
                new TraitBonus[] {
                    new TraitBonus { bonusDescription = "Summon 1-star copy" },
                    new TraitBonus { bonusDescription = "Summon 2-star copy" }
                });

            Debug.Log("Generated 14 traits");
        }

        private static void CreateTrait(string path, string name, bool isUnique, int[] tiers, string desc, TraitBonus[] bonuses)
        {
            var trait = ScriptableObject.CreateInstance<TraitData>();
            trait.traitId = name.ToLower().Replace(" ", "_");
            trait.traitName = name;
            trait.isUnique = isUnique;
            trait.description = desc;
            trait.tierThresholds = tiers;
            trait.tierBonuses = bonuses;
            AssetDatabase.CreateAsset(trait, $"{path}/{name}.asset");
        }

        private static void GenerateAllUnits()
        {
            string path = "Assets/Resources/ScriptableObjects/Units";
            EnsureDirectoryExists(path);
            DeleteAllAssetsInFolder(path);

            // Load traits
            var traits = new Dictionary<string, TraitData>();
            foreach (var t in LoadAssetsFromFolder<TraitData>("Assets/Resources/ScriptableObjects/Traits"))
            {
                traits[t.traitName] = t;
            }

            // Cost 1 Units (8)
            CreateUnit(path, "Footman", 1, new[] { "Human", "Warrior" }, traits,
                new UnitStats { health = 550, attack = 50, armor = 25, magicResist = 20, attackSpeed = 0.7f, range = 1, maxMana = 60 },
                new AbilityData { abilityName = "Shield Bash", baseDamage = 100, type = AbilityType.Damage, damageType = DamageType.Physical });

            CreateUnit(path, "Archer", 1, new[] { "Human", "Ranger" }, traits,
                new UnitStats { health = 400, attack = 55, armor = 15, magicResist = 20, attackSpeed = 0.8f, range = 4, maxMana = 50 },
                new AbilityData { abilityName = "Volley", baseDamage = 120, type = AbilityType.AreaDamage, damageType = DamageType.Physical, radius = 1 });

            CreateUnit(path, "Skeleton", 1, new[] { "Undead", "Warrior" }, traits,
                new UnitStats { health = 450, attack = 45, armor = 10, magicResist = 10, attackSpeed = 0.9f, range = 1, maxMana = 80 },
                new AbilityData { abilityName = "Bone Shield", baseShieldAmount = 200, type = AbilityType.Shield, targeting = AbilityTargeting.Self });

            CreateUnit(path, "Wolf", 1, new[] { "Beast", "Assassin" }, traits,
                new UnitStats { health = 380, attack = 60, armor = 10, magicResist = 15, attackSpeed = 1.0f, range = 1, maxMana = 40 },
                new AbilityData { abilityName = "Savage Bite", baseDamage = 150, type = AbilityType.Damage, damageType = DamageType.Physical });

            CreateUnit(path, "Imp", 1, new[] { "Demon", "Mage" }, traits,
                new UnitStats { health = 350, attack = 40, armor = 10, magicResist = 25, attackSpeed = 0.75f, range = 3, startingMana = 20, maxMana = 60 },
                new AbilityData { abilityName = "Firebolt", baseDamage = 180, type = AbilityType.Damage, damageType = DamageType.Elemental });

            CreateUnit(path, "Sprite", 1, new[] { "Fey", "Support" }, traits,
                new UnitStats { health = 320, attack = 35, armor = 10, magicResist = 30, attackSpeed = 0.8f, range = 3, startingMana = 30, maxMana = 70 },
                new AbilityData { abilityName = "Healing Light", baseHealing = 200, type = AbilityType.Heal, targeting = AbilityTargeting.LowestHealthAlly });

            CreateUnit(path, "Golem", 1, new[] { "Elemental", "Tank" }, traits,
                new UnitStats { health = 650, attack = 40, armor = 35, magicResist = 25, attackSpeed = 0.5f, range = 1, maxMana = 100 },
                new AbilityData { abilityName = "Stone Skin", baseShieldAmount = 300, type = AbilityType.Shield, targeting = AbilityTargeting.Self });

            CreateUnit(path, "Rat", 1, new[] { "Beast", "Berserker" }, traits,
                new UnitStats { health = 300, attack = 55, armor = 5, magicResist = 5, attackSpeed = 1.1f, range = 1, maxMana = 50 },
                new AbilityData { abilityName = "Frenzy", description = "+50% Attack Speed for 4s", type = AbilityType.Buff, targeting = AbilityTargeting.Self, attackSpeedBonus = 0.5f, duration = 4f });

            // Cost 2 Units (8)
            CreateUnit(path, "Knight", 2, new[] { "Human", "Tank" }, traits,
                new UnitStats { health = 750, attack = 55, armor = 45, magicResist = 30, attackSpeed = 0.6f, range = 1, maxMana = 70 },
                new AbilityData { abilityName = "Taunt", type = AbilityType.Debuff, duration = 2f });

            CreateUnit(path, "Crossbowman", 2, new[] { "Human", "Ranger" }, traits,
                new UnitStats { health = 450, attack = 75, armor = 20, magicResist = 20, attackSpeed = 0.6f, range = 4, maxMana = 60 },
                new AbilityData { abilityName = "Piercing Shot", baseDamage = 200, type = AbilityType.Damage, damageType = DamageType.Physical });

            CreateUnit(path, "Ghoul", 2, new[] { "Undead", "Assassin" }, traits,
                new UnitStats { health = 500, attack = 65, armor = 20, magicResist = 20, attackSpeed = 0.85f, range = 1, maxMana = 50 },
                new AbilityData { abilityName = "Life Drain", baseDamage = 150, type = AbilityType.DamageAndHeal, damageType = DamageType.Dark });

            CreateUnit(path, "Druid", 2, new[] { "Beast", "Support", "Summoner" }, traits,
                new UnitStats { health = 480, attack = 45, armor = 20, magicResist = 35, attackSpeed = 0.7f, range = 3, startingMana = 20, maxMana = 80 },
                new AbilityData { abilityName = "Nature's Call", baseHealing = 150, type = AbilityType.Heal, targeting = AbilityTargeting.LowestHealthAlly });

            CreateUnit(path, "FireMage", 2, new[] { "Elemental", "Mage" }, traits,
                new UnitStats { health = 420, attack = 50, armor = 15, magicResist = 40, attackSpeed = 0.65f, range = 4, startingMana = 25, maxMana = 70 },
                new AbilityData { abilityName = "Flame Wave", baseDamage = 220, type = AbilityType.AreaDamage, damageType = DamageType.Elemental, radius = 1 });

            CreateUnit(path, "Shadow", 2, new[] { "Undead", "Assassin" }, traits,
                new UnitStats { health = 420, attack = 70, armor = 15, magicResist = 25, attackSpeed = 0.9f, range = 1, maxMana = 45 },
                new AbilityData { abilityName = "Shadow Strike", baseDamage = 250, type = AbilityType.Damage, damageType = DamageType.Dark, targeting = AbilityTargeting.BacklineEnemy });

            CreateUnit(path, "Satyr", 2, new[] { "Fey", "Berserker" }, traits,
                new UnitStats { health = 550, attack = 70, armor = 20, magicResist = 25, attackSpeed = 0.8f, range = 1, maxMana = 60 },
                new AbilityData { abilityName = "Wild Charge", baseDamage = 180, type = AbilityType.Damage, damageType = DamageType.Physical });

            CreateUnit(path, "HoundMaster", 2, new[] { "Human", "Beast", "Summoner" }, traits,
                new UnitStats { health = 500, attack = 50, armor = 25, magicResist = 20, attackSpeed = 0.7f, range = 2, maxMana = 80 },
                new AbilityData { abilityName = "Release Hounds", type = AbilityType.Summon });

            // Cost 3 Units (8)
            CreateUnit(path, "Paladin", 3, new[] { "Human", "Tank", "Support" }, traits,
                new UnitStats { health = 850, attack = 60, armor = 50, magicResist = 45, attackSpeed = 0.55f, range = 1, maxMana = 90 },
                new AbilityData { abilityName = "Divine Shield", baseShieldAmount = 400, type = AbilityType.Shield, targeting = AbilityTargeting.LowestHealthAlly });

            CreateUnit(path, "Warden", 3, new[] { "Human", "Warrior", "Tank" }, traits,
                new UnitStats { health = 900, attack = 70, armor = 55, magicResist = 35, attackSpeed = 0.6f, range = 1, maxMana = 70 },
                new AbilityData { abilityName = "Shockwave", baseDamage = 200, type = AbilityType.AreaDamage, damageType = DamageType.Physical, radius = 2 });

            CreateUnit(path, "Necromancer", 3, new[] { "Undead", "Mage", "Summoner" }, traits,
                new UnitStats { health = 500, attack = 55, armor = 15, magicResist = 45, attackSpeed = 0.6f, range = 4, startingMana = 30, maxMana = 90 },
                new AbilityData { abilityName = "Raise Dead", type = AbilityType.Summon });

            CreateUnit(path, "AlphaWolf", 3, new[] { "Beast", "Warrior" }, traits,
                new UnitStats { health = 700, attack = 85, armor = 30, magicResist = 25, attackSpeed = 0.85f, range = 1, maxMana = 60 },
                new AbilityData { abilityName = "Pack Howl", type = AbilityType.Buff, targeting = AbilityTargeting.AllAllies, duration = 4f });

            CreateUnit(path, "StormElemental", 3, new[] { "Elemental", "Mage" }, traits,
                new UnitStats { health = 520, attack = 60, armor = 20, magicResist = 50, attackSpeed = 0.7f, range = 3, startingMana = 25, maxMana = 80 },
                new AbilityData { abilityName = "Chain Lightning", baseDamage = 180, type = AbilityType.AreaDamage, damageType = DamageType.Elemental, projectileCount = 3 });

            CreateUnit(path, "Succubus", 3, new[] { "Demon", "Assassin" }, traits,
                new UnitStats { health = 550, attack = 90, armor = 20, magicResist = 30, attackSpeed = 0.9f, range = 1, maxMana = 55 },
                new AbilityData { abilityName = "Soul Kiss", baseDamage = 300, type = AbilityType.DamageAndHeal, damageType = DamageType.Dark });

            CreateUnit(path, "Enchantress", 3, new[] { "Fey", "Mage", "Support" }, traits,
                new UnitStats { health = 480, attack = 50, armor = 15, magicResist = 55, attackSpeed = 0.65f, range = 4, startingMana = 35, maxMana = 85 },
                new AbilityData { abilityName = "Charm", type = AbilityType.Debuff, duration = 2f, targeting = AbilityTargeting.HighestHealthEnemy });

            CreateUnit(path, "Marksman", 3, new[] { "Human", "Ranger" }, traits,
                new UnitStats { health = 480, attack = 95, armor = 20, magicResist = 20, attackSpeed = 0.75f, range = 5, maxMana = 65 },
                new AbilityData { abilityName = "Headshot", baseDamage = 400, type = AbilityType.Damage, damageType = DamageType.Physical, targeting = AbilityTargeting.LowestHealthEnemy });

            // Cost 4 Units (8)
            CreateUnit(path, "Champion", 4, new[] { "Human", "Warrior", "Berserker" }, traits,
                new UnitStats { health = 950, attack = 100, armor = 50, magicResist = 40, attackSpeed = 0.7f, range = 1, maxMana = 80 },
                new AbilityData { abilityName = "Bladestorm", baseDamage = 450, type = AbilityType.AreaDamage, damageType = DamageType.Physical, radius = 1 });

            CreateUnit(path, "DeathKnight", 4, new[] { "Undead", "Tank", "Warrior" }, traits,
                new UnitStats { health = 1100, attack = 85, armor = 55, magicResist = 45, attackSpeed = 0.6f, range = 1, maxMana = 90 },
                new AbilityData { abilityName = "Death Coil", baseDamage = 300, baseHealing = 300, type = AbilityType.DamageAndHeal, damageType = DamageType.Dark });

            CreateUnit(path, "Phoenix", 4, new[] { "Elemental", "Support" }, traits,
                new UnitStats { health = 600, attack = 70, armor = 25, magicResist = 55, attackSpeed = 0.75f, range = 3, startingMana = 30, maxMana = 100 },
                new AbilityData { abilityName = "Rebirth", baseHealing = 500, type = AbilityType.Heal, targeting = AbilityTargeting.AllAllies });

            CreateUnit(path, "DemonLord", 4, new[] { "Demon", "Tank", "Berserker" }, traits,
                new UnitStats { health = 1000, attack = 95, armor = 45, magicResist = 40, attackSpeed = 0.65f, range = 1, maxMana = 85 },
                new AbilityData { abilityName = "Hellfire", baseDamage = 350, type = AbilityType.AreaDamage, damageType = DamageType.Elemental, radius = 2 });

            CreateUnit(path, "Archdruid", 4, new[] { "Beast", "Fey", "Summoner" }, traits,
                new UnitStats { health = 700, attack = 65, armor = 30, magicResist = 50, attackSpeed = 0.6f, range = 3, startingMana = 40, maxMana = 100 },
                new AbilityData { abilityName = "Nature's Wrath", baseHealing = 250, type = AbilityType.Heal, targeting = AbilityTargeting.AllAllies });

            CreateUnit(path, "Archmage", 4, new[] { "Human", "Mage" }, traits,
                new UnitStats { health = 550, attack = 60, armor = 20, magicResist = 60, attackSpeed = 0.6f, range = 4, startingMana = 40, maxMana = 100 },
                new AbilityData { abilityName = "Meteor", baseDamage = 600, type = AbilityType.AreaDamage, damageType = DamageType.Elemental, radius = 2 });

            CreateUnit(path, "Lich", 4, new[] { "Undead", "Mage", "Summoner" }, traits,
                new UnitStats { health = 580, attack = 65, armor = 15, magicResist = 55, attackSpeed = 0.55f, range = 4, startingMana = 35, maxMana = 95 },
                new AbilityData { abilityName = "Death Nova", baseDamage = 400, type = AbilityType.AreaDamage, damageType = DamageType.Dark, radius = 2 });

            CreateUnit(path, "Dragon", 4, new[] { "Beast", "Elemental", "Warrior" }, traits,
                new UnitStats { health = 1200, attack = 110, armor = 45, magicResist = 45, attackSpeed = 0.5f, range = 2, maxMana = 100 },
                new AbilityData { abilityName = "Dragon Breath", baseDamage = 500, type = AbilityType.AreaDamage, damageType = DamageType.Elemental, radius = 2 });

            Debug.Log("Generated 32 units");
        }

        private static void CreateUnit(string path, string name, int cost, string[] traitNames, Dictionary<string, TraitData> traits, UnitStats stats, AbilityData ability)
        {
            var unit = ScriptableObject.CreateInstance<UnitData>();
            unit.unitId = name.ToLower();
            unit.unitName = name;
            unit.cost = cost;
            unit.baseStats = stats;
            unit.ability = ability;

            var unitTraits = new List<TraitData>();
            foreach (var traitName in traitNames)
            {
                if (traits.ContainsKey(traitName))
                    unitTraits.Add(traits[traitName]);
            }
            unit.traits = unitTraits.ToArray();

            AssetDatabase.CreateAsset(unit, $"{path}/{name}.asset");
        }

        private static void GenerateAllItems()
        {
            string path = "Assets/Resources/ScriptableObjects/Items";
            EnsureDirectoryExists(path);
            DeleteAllAssetsInFolder(path);

            // Common
            CreateItem(path, "IronSword", "Iron Sword", ItemRarity.Common, "+15 Attack", 15, 0, 0, 0, 0, 0, ItemEffect.None, 0, 0);
            CreateItem(path, "LeatherArmor", "Leather Armor", ItemRarity.Common, "+20 Armor", 0, 0, 20, 0, 0, 0, ItemEffect.None, 0, 0);
            CreateItem(path, "HealthCharm", "Health Charm", ItemRarity.Common, "+150 Health", 0, 150, 0, 0, 0, 0, ItemEffect.None, 0, 0);
            CreateItem(path, "QuickenBoots", "Quicken Boots", ItemRarity.Common, "+15% Attack Speed", 0, 0, 0, 0, 0.15f, 0, ItemEffect.None, 0, 0);
            CreateItem(path, "ManaGem", "Mana Gem", ItemRarity.Common, "+20 Starting Mana", 0, 0, 0, 0, 0, 20, ItemEffect.None, 0, 0);
            CreateItem(path, "MagicCloak", "Magic Cloak", ItemRarity.Common, "+25 Magic Resist", 0, 0, 0, 25, 0, 0, ItemEffect.None, 0, 0);

            // Uncommon
            CreateItem(path, "FrostBlade", "Frost Blade", ItemRarity.Uncommon, "Attacks slow by 20%", 10, 0, 0, 0, 0, 0, ItemEffect.Slow, 0.2f, 2f);
            CreateItem(path, "VampiricDagger", "Vampiric Dagger", ItemRarity.Uncommon, "15% Lifesteal", 10, 0, 0, 0, 0, 0, ItemEffect.Lifesteal, 0.15f, 0);
            CreateItem(path, "Thornmail", "Thornmail", ItemRarity.Uncommon, "Reflect 25% damage", 0, 0, 30, 0, 0, 0, ItemEffect.Thorns, 0.25f, 0);
            CreateItem(path, "Spellshield", "Spellshield", ItemRarity.Uncommon, "Block first ability", 0, 0, 0, 20, 0, 0, ItemEffect.SpellShield, 1, 0);
            CreateItem(path, "BerserkerAxe", "Berserker Axe", ItemRarity.Uncommon, "+30% AS, -100 HP", 0, -100, 0, 0, 0.3f, 0, ItemEffect.None, 0, 0);
            CreateItem(path, "ArcaneStaff", "Arcane Staff", ItemRarity.Uncommon, "+20% Ability Damage", 0, 0, 0, 0, 0, 15, ItemEffect.AbilityPower, 0.2f, 0);

            // Rare
            CreateItem(path, "BlazingSword", "Blazing Sword", ItemRarity.Rare, "Burn for 50 over 3s", 20, 0, 0, 0, 0, 0, ItemEffect.Burn, 50, 3);
            CreateItem(path, "GuardianAngel", "Guardian Angel", ItemRarity.Rare, "Revive with 400 HP", 0, 0, 25, 25, 0, 0, ItemEffect.Revive, 400, 0);
            CreateItem(path, "InfinityEdge", "Infinity Edge", ItemRarity.Rare, "25% crit, 2x damage", 25, 0, 0, 0, 0, 0, ItemEffect.CriticalStrike, 0.25f, 2f);
            CreateItem(path, "Deathcap", "Deathcap", ItemRarity.Rare, "+40% Ability Damage", 0, 0, 0, 0, 0, 30, ItemEffect.AbilityPower, 0.4f, 0);

            Debug.Log("Generated 16 items");
        }

        private static void CreateItem(string path, string id, string name, ItemRarity rarity, string desc, int atk, int hp, int armor, int mr, float atkSpd, int mana, ItemEffect effect, float v1, float v2)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id.ToLower();
            item.itemName = name;
            item.description = desc;
            item.rarity = rarity;
            item.bonusAttack = atk;
            item.bonusHealth = hp;
            item.bonusArmor = armor;
            item.bonusMagicResist = mr;
            item.bonusAttackSpeed = atkSpd;
            item.bonusMana = mana;
            item.effect = effect;
            item.effectValue1 = v1;
            item.effectValue2 = v2;
            AssetDatabase.CreateAsset(item, $"{path}/{id}.asset");
        }

        private static void GenerateAllCrests()
        {
            string path = "Assets/Resources/ScriptableObjects/Crests";
            EnsureDirectoryExists(path);
            DeleteAllAssetsInFolder(path);

            // Minor Crests
            CreateCrest(path, "PoisonTipped", "Poison-Tipped", CrestType.Minor, "Attacks apply 20 poison over 3s", 0, 0, 0, 0, 0, 0, CrestEffect.AllUnitsPoison, 20, 3, 0);
            CreateCrest(path, "EmberTouch", "Ember Touch", CrestType.Minor, "Attacks deal +10 fire damage", 0, 10, 0, 0, 0, 0, CrestEffect.AllUnitsBurn, 10, 1, 0);
            CreateCrest(path, "IronWill", "Iron Will", CrestType.Minor, "All units +15 Armor", 0, 0, 15, 0, 0, 0, CrestEffect.None, 0, 0, 0);
            CreateCrest(path, "MysticBarrier", "Mystic Barrier", CrestType.Minor, "All units +15 Magic Resist", 0, 0, 0, 15, 0, 0, CrestEffect.None, 0, 0, 0);
            CreateCrest(path, "ShieldBearer", "Shield Bearer", CrestType.Minor, "Start with 75 HP shield", 0, 0, 0, 0, 0, 0, CrestEffect.AllUnitsShield, 75, 0, 0);
            CreateCrest(path, "SwiftStrike", "Swift Strike", CrestType.Minor, "All units +10% Attack Speed", 0, 0, 0, 0, 0.1f, 0, CrestEffect.None, 0, 0, 0);
            CreateCrest(path, "MightyCrest", "Mighty Crest", CrestType.Minor, "All units +10 Attack", 0, 10, 0, 0, 0, 0, CrestEffect.None, 0, 0, 0);
            CreateCrest(path, "ManaFlow", "Mana Flow", CrestType.Minor, "All units +15 Starting Mana", 0, 0, 0, 0, 0, 15, CrestEffect.None, 0, 0, 0);
            CreateCrest(path, "Vitality", "Vitality", CrestType.Minor, "All units +100 Health", 100, 0, 0, 0, 0, 0, CrestEffect.None, 0, 0, 0);

            // Major Crests
            CreateCrest(path, "Inferno", "Inferno", CrestType.Major, "Abilities +25% fire damage, apply burn", 0, 0, 0, 0, 0, 0, CrestEffect.AllAbilityDamage, 0.25f, 40, 2);
            CreateCrest(path, "DeathsEmbrace", "Death's Embrace", CrestType.Major, "Ally death grants 15% AS", 0, 0, 0, 0, 0, 0, CrestEffect.AllyDeathAttackSpeed, 0.15f, 4, 0);
            CreateCrest(path, "FortressStance", "Fortress Stance", CrestType.Major, "+30 Armor/MR, -15% AS", 0, 0, 30, 30, -0.15f, 0, CrestEffect.None, 0, 0, 0);
            CreateCrest(path, "BerserkerRage", "Berserker Rage", CrestType.Major, "Below 50% HP: +40% AS, +25% dmg", 0, 0, 0, 0, 0, 0, CrestEffect.LowHealthDamageBoost, 0.5f, 0.4f, 0.25f);
            CreateCrest(path, "ArcaneOverflow", "Arcane Overflow", CrestType.Major, "Mages +50% mana gain, -15% cost", 0, 0, 0, 0, 0, 0, CrestEffect.TraitManaGain, 0.5f, 0.15f, 0);
            CreateCrest(path, "LifeBond", "Life Bond", CrestType.Major, "Support abilities heal all for 50", 0, 0, 0, 0, 0, 0, CrestEffect.AllyDeathHeal, 50, 0, 0);

            Debug.Log("Generated 15 crests");
        }

        [MenuItem("Crestforge/Generate PvE Critters")]
        public static void GeneratePvECritters()
        {
            string path = "Assets/Resources/ScriptableObjects/PvEUnits";
            EnsureDirectoryExists(path);

            // Create very weak critter units for PvE intro
            CreatePvEUnit(path, "Stingray", "Stingray", 80, 15, 0, 0, 0.6f, 1);
            CreatePvEUnit(path, "Cactus", "Cactus", 100, 10, 5, 0, 0.5f, 1);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated 2 PvE critter units in " + path);
        }

        private static void CreatePvEUnit(string path, string id, string name, int health, int attack, int armor, int mr, float atkSpd, int range)
        {
            var unit = ScriptableObject.CreateInstance<UnitData>();
            unit.unitId = id.ToLower();
            unit.unitName = name;
            unit.cost = 0; // PvE units have no cost
            unit.baseStats = new UnitStats
            {
                health = health,
                attack = attack,
                armor = armor,
                magicResist = mr,
                attackSpeed = atkSpd,
                range = range,
                maxMana = 100,
                startingMana = 0
            };
            unit.traits = new TraitData[0];
            unit.ability = new AbilityData { abilityName = "None", type = AbilityType.Damage };
            AssetDatabase.CreateAsset(unit, $"{path}/{id}.asset");
        }

        private static void CreateCrest(string path, string id, string name, CrestType type, string desc, int hp, int atk, int armor, int mr, float atkSpd, int mana, CrestEffect effect, float v1, float v2, float v3)
        {
            var crest = ScriptableObject.CreateInstance<CrestData>();
            crest.crestId = id.ToLower();
            crest.crestName = name;
            crest.description = desc;
            crest.type = type;
            crest.bonusHealth = hp;
            crest.bonusAttack = atk;
            crest.bonusArmor = armor;
            crest.bonusMagicResist = mr;
            crest.bonusAttackSpeed = atkSpd;
            crest.bonusMana = mana;
            crest.effect = effect;
            crest.effectValue1 = v1;
            crest.effectValue2 = v2;
            crest.effectValue3 = v3;
            AssetDatabase.CreateAsset(crest, $"{path}/{id}.asset");
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private static void DeleteAllAssetsInFolder(string path)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.asset");
                foreach (var file in files)
                {
                    AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                }
            }
        }
    }
}
#endif