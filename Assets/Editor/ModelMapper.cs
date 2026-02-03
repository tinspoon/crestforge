using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Crestforge.Visuals;

namespace Crestforge.Editor
{
    /// <summary>
    /// Maps 3D model prefabs to the UnitModelDatabase
    /// </summary>
    public class ModelMapper : EditorWindow
    {
        // Model mapping data with scale adjustments and animation names
        private class ModelMapping
        {
            public string prefabPath;
            public float scale;
            public float yOffset;
            public string animPrefix; // Prefix for animation clips (e.g., "StarFish" for "StarFish_IdleBattle")

            public ModelMapping(string path, float s = 1f, float y = 0f, string animPrefix = null)
            {
                prefabPath = path;
                scale = s;
                yOffset = y;
                this.animPrefix = animPrefix;
            }
        }

        [MenuItem("Crestforge/Map Models to Units")]
        public static void MapModels()
        {
            // First verify paths exist
            VerifyPaths();

            // Define the mapping from unit name to prefab path with scale adjustments and animation prefixes
            // Paths verified from actual asset pack folder structure
            var mapping = new Dictionary<string, ModelMapping>
            {
                // TIER 1 (1-Cost) - smaller units, scale down
                { "Blue Slime", new ModelMapping("Assets/RPGMonsterWave4/Prefab/BlueSlime.prefab", 0.8f, 0f, "BlueSlime") },
                { "Red Slime", new ModelMapping("Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/SlimePBR.prefab", 0.8f, 0f, "Slime") },
                { "Bat", new ModelMapping("Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/BatPBR.prefab", 0.7f, 0.3f, "Bat") },
                { "Mushroom", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/MushroomSmilePBR.prefab", 0.8f, 0f, "Mushroom") },
                { "Crawler", new ModelMapping("Assets/RPGMonsterWave4/Prefab/Crawler.prefab", 0.8f, 0f, "Crawler") },
                { "Little Demon", new ModelMapping("Assets/RPGMonsterWave4/Prefab/LittleDemon.prefab", 0.8f, 0f, "LittleDemon") },
                { "Green Spider", new ModelMapping("Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/SpiderPBR.prefab", 0.8f, 0f, "Spider") },
                { "Starfish", new ModelMapping("Assets/RPGMonsterWave4/Prefab/StarFish.prefab", 0.7f, 0f, "StarFish") },
                { "Rat Assassin", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/RatAssassinPBRDefault.prefab", 0.85f, 0f, "RatAssassin") },
                { "Archer", new ModelMapping("Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC11.prefab", 0.9f, 0f, null) },

                // TIER 2 (2-Cost) - slightly larger
                { "Fishman", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/FishmanPBR.prefab", 0.9f, 0f, "Fishman") },
                { "Crab Monster", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/CrabMonsterPBRDefault.prefab", 0.85f, 0f, "CrabMonster") },
                { "Salamander", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/SalamanderPBR.prefab", 0.9f, 0f, "Salamander") },
                { "Flower Monster", new ModelMapping("Assets/RPGMonsterWave4/Prefab/FlowerMonster.prefab", 0.9f, 0f, "FlowerMonster") },
                { "Evil Plant", new ModelMapping("Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/MonsterPlantPBR.prefab", 0.9f, 0f, "MonsterPlant") },
                { "Worm Monster", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/WormMonsterPBRDefault.prefab", 0.85f, 0f, "WormMonster") },
                { "Battle Bee", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/BattleBeePBR.prefab", 0.85f, 0.2f, "BattleBee") },
                { "Golem", new ModelMapping("Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/GolemPBR.prefab", 1.0f, 0f, "Golem") },
                { "Chest Monster", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/ChestMonsterPBRDefault.prefab", 0.9f, 0f, "ChestMonster") },
                { "Blacksmith", new ModelMapping("Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC18.prefab", 0.95f, 0f, null) },

                // TIER 3 (3-Cost) - medium units
                { "Lizard Warrior", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/LizardWarriorPBRDefault.prefab", 1.0f, 0f, "LizardWarrior") },
                { "Werewolf", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/WerewolfPBRDefault.prefab", 1.0f, 0f, "Werewolf") },
                { "Ice Golem", new ModelMapping("Assets/RPGMonsterWave4/Prefab/IceGolem.prefab", 1.1f, 0f, "IceGolem") },
                { "Cyclops", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/CyclopsPBR.prefab", 1.1f, 0f, "Cyclops") },
                { "Specter", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/SpecterPBRDefault.prefab", 1.0f, 0.2f, "Specter") },
                { "Naga Wizard", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/NagaWizardPBR.prefab", 1.0f, 0f, "NagaWizard") },
                { "Beholder", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/BeholderPBRDefault.prefab", 1.0f, 0.3f, "Beholder") },
                { "Cleric", new ModelMapping("Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC07.prefab", 1.0f, 0f, null) },

                // TIER 4 (4-Cost) - larger units
                { "Black Knight", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/BlackKnightPBRDefault.prefab", 1.1f, 0f, "BlackKnight") },
                { "Bishop Knight", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/BishopKnightPBR.prefab", 1.1f, 0f, "BishopKnight") },
                { "Bone Dragon", new ModelMapping("Assets/RPGMonsterWave4/Prefab/BoneDragon.prefab", 1.2f, 0.1f, "BoneDragon") },
                { "Flying Demon", new ModelMapping("Assets/RPGMonsterWave02PBR/Prefabs/Character/FylingDemonPBRDefault.prefab", 1.1f, 0.3f, "FylingDemon") },
                { "Fat Dragon", new ModelMapping("Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/DragonPBR.prefab", 1.15f, 0f, "Dragon") },
                { "Evil Old Mage", new ModelMapping("Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/EvilMagePBR.prefab", 1.0f, 0f, "EvilMage") },
                { "Orc with Mace", new ModelMapping("Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/OrcPBR.prefab", 1.1f, 0f, "Orc") },

                // TIER 5 (5-Cost) - boss-sized units
                { "Demon King", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/DemonKingPBR.prefab", 1.3f, 0f, "DemonKing") },
                { "Castle Monster", new ModelMapping("Assets/RPGMonsterWave4/Prefab/CastleMonster.prefab", 1.4f, 0f, "CastleMonster") },
                { "Spiky Shell Turtle", new ModelMapping("Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/TurtleShellPBR.prefab", 1.2f, 0f, "TurtleShell") },
                { "Flame Knight", new ModelMapping("Assets/RPGMonsterWave4/Prefab/FlameKnight.prefab", 1.2f, 0f, "FlameKnight") },
                { "Skeleton Mage", new ModelMapping("Assets/RPGMonsterWave4/Prefab/SkeletonMage.prefab", 1.15f, 0f, "SkeletonMage") },

                // PvE Critters (weak enemies for intro rounds)
                { "Stingray", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/StingRayPBR.prefab", 0.6f, 0.1f, "StingRay") },
                { "Cactus", new ModelMapping("Assets/RPGMonsterWave03PBR/Prefab/Characters/CactusPBR.prefab", 0.7f, 0f, "Cactus") },
            };

            // Find or create the UnitModelDatabase
            UnitModelDatabase database = FindOrCreateDatabase();
            if (database == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not create UnitModelDatabase!", "OK");
                return;
            }

            // Clear existing entries
            database.unitModels.Clear();

            int successCount = 0;
            int failCount = 0;
            List<string> missing = new List<string>();

            foreach (var kvp in mapping)
            {
                string unitName = kvp.Key;
                ModelMapping modelData = kvp.Value;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelData.prefabPath);

                if (prefab != null)
                {
                    var entry = new UnitModelDatabase.UnitModelEntry
                    {
                        unitName = unitName,
                        modelPrefab = prefab,
                        scale = modelData.scale,
                        yOffset = modelData.yOffset,
                        rotationOffset = Vector3.zero,
                        attackAnimSpeed = 1f
                    };

                    // Set animation clip names based on the animation prefix
                    if (!string.IsNullOrEmpty(modelData.animPrefix))
                    {
                        string prefix = modelData.animPrefix;
                        entry.idleClip = $"{prefix}_IdleBattle";
                        entry.walkClip = $"{prefix}_MoveFWD";
                        entry.attackClip = $"{prefix}_Attack01";
                        entry.hitClip = $"{prefix}_GetHit";
                        entry.deathClip = $"{prefix}_Die";
                    }

                    database.unitModels.Add(entry);
                    successCount++;
                    Debug.Log($"[ModelMapper] Added {unitName} -> {prefab.name} (scale: {modelData.scale}, anim: {modelData.animPrefix})");
                }
                else
                {
                    missing.Add($"{unitName}: {modelData.prefabPath}");
                    failCount++;
                    Debug.LogWarning($"[ModelMapper] Missing prefab: {modelData.prefabPath}");
                }
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message = $"Model mapping complete!\n\nSuccess: {successCount}\nFailed: {failCount}";
            if (missing.Count > 0)
            {
                message += "\n\nMissing prefabs (check Console for paths):\n" + string.Join("\n", missing.GetRange(0, Mathf.Min(5, missing.Count)));
                if (missing.Count > 5) message += $"\n... and {missing.Count - 5} more";
            }

            EditorUtility.DisplayDialog("Model Mapper", message, "OK");
            Debug.Log($"[ModelMapper] Complete - Success: {successCount}, Failed: {failCount}");
        }

        private static UnitModelDatabase FindOrCreateDatabase()
        {
            // Try to find existing database
            string[] guids = AssetDatabase.FindAssets("t:UnitModelDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<UnitModelDatabase>(path);
            }

            // Create new database
            UnitModelDatabase database = ScriptableObject.CreateInstance<UnitModelDatabase>();

            // Ensure Resources folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            AssetDatabase.CreateAsset(database, "Assets/Resources/UnitModelDatabase.asset");
            Debug.Log("[ModelMapper] Created new UnitModelDatabase at Assets/Resources/UnitModelDatabase.asset");

            return database;
        }

        [MenuItem("Crestforge/Verify Model Paths")]
        public static void VerifyPaths()
        {
            // Quick check which prefab paths exist
            string[] prefabFolders = new string[]
            {
                "Assets/RPG Monster Wave PBR/Prefabs",
                "Assets/RPGMonsterWave02PBR/Prefabs",
                "Assets/RPGMonsterWave03PBR/Prefabs",
                "Assets/RPGMonsterWave4/Prefabs",
                "Assets/RPGTinyHeroWavePBR/Prefab",
                "Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters"
            };

            Debug.Log("[ModelMapper] ========== Verifying prefab locations ==========");

            int totalFound = 0;
            foreach (string folder in prefabFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"[ModelMapper] Folder not found: {folder}");
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                totalFound += guids.Length;
                Debug.Log($"[ModelMapper] {folder}: {guids.Length} prefabs");

                // List all prefabs with PBRDefault in name
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = System.IO.Path.GetFileName(path);
                    if (fileName.Contains("PBRDefault"))
                    {
                        Debug.Log($"    {fileName}");
                    }
                }
            }

            Debug.Log($"[ModelMapper] ========== Total: {totalFound} prefabs ==========");
        }

        [MenuItem("Crestforge/List All PBRDefault Prefabs")]
        public static void ListAllPBRPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("PBRDefault t:Prefab");
            Debug.Log($"[ModelMapper] Found {guids.Length} PBRDefault prefabs:");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Debug.Log($"  {path}");
            }
        }
    }
}
