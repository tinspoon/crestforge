using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Crestforge.Visuals;

namespace Crestforge.Editor
{
    /// <summary>
    /// Maps 3D model prefabs to the UnitModelDatabase for all 43 game units + 2 PvE units.
    /// RTS Mini Legion models use auto-detect for animations.
    /// RPG Monster Wave models use explicit prefix-based animation clip names.
    /// </summary>
    public class ModelMapper : EditorWindow
    {
        private class ModelMapping
        {
            public string prefabPath;
            public float scale;
            public float yOffset;
            public string animPrefix; // For RPG Monster Wave models (null = auto-detect)

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
            VerifyPaths();

            var mapping = new Dictionary<string, ModelMapping>
            {
                // ==========================================
                // 1-COST UNITS
                // ==========================================

                // RTS Mini Legion Human
                { "Footman", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Human PBR/Prefabs/Footman_Standard_PBR.prefab", 0.8f) },
                // RTS Mini Legion Human
                { "Archer", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Human PBR/Prefabs/Archer_Standard_PBR.prefab", 0.8f) },
                // RTS Mini Legion Warband
                { "Grunt", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Warband PBR/Prefabs/Grunt Standard.prefab", 0.8f) },
                // RTS Mini Legion Undead
                { "Skeleton Warrior", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Undead PBR/Prefabs/Skeleton Warrior Standard.prefab", 0.8f) },
                // RTS Mini Legion Sentinel
                { "Elf Ranger", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Sentinel PBR/Prefabs/Elf Ranger StandardPBR.prefab", 0.8f) },
                // RPG Monster Wave PBR
                { "Bat", new ModelMapping(
                    "Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/BatPBR.prefab", 0.7f, 0.3f, "Bat") },
                // RPG Monster Wave PBR
                { "Red Slime", new ModelMapping(
                    "Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/SlimePBR.prefab", 0.8f, 0f, "Slime") },
                // RPGMonsterWave03PBR (Smile variant - auto-detect handles Smile suffix clips)
                { "Mushroom", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/MushroomSmilePBR.prefab", 0.8f) },
                // RPGMonsterWave4
                { "Crawler", new ModelMapping(
                    "Assets/RPGMonsterWave4/Prefab/Crawler.prefab", 0.8f, 0f, "Crawler") },
                // RPGMonsterWave4
                { "Little Demon", new ModelMapping(
                    "Assets/RPGMonsterWave4/Prefab/LittleDemon.prefab", 0.8f, 0f, "LittleDemon") },
                // RPGMonsterWave02PBR
                { "Rat Assassin", new ModelMapping(
                    "Assets/RPGMonsterWave02PBR/Prefabs/Character/RatAssassinPBRDefault.prefab", 0.85f, 0f, "RatAssassin") },
                // RPGTinyHeroWavePBR
                { "Cleric", new ModelMapping(
                    "Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC07.prefab", 0.85f) },

                // ==========================================
                // 2-COST UNITS
                // ==========================================

                // RTS Mini Legion Human
                { "Knight", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Human PBR/Prefabs/Knight_Standard_PBR.prefab", 0.9f) },
                // RTS Mini Legion Human
                { "Mage", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Human PBR/Prefabs/Mage_Standard_PBR.prefab", 0.9f) },
                // RTS Mini Legion Sentinel
                { "Dryad", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Sentinel PBR/Prefabs/Dryad StandardPBR.prefab", 0.9f) },
                // RTS Mini Legion Warband
                { "Head Hunter", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Warband PBR/Prefabs/HeadHunter Standard.prefab", 0.9f) },
                // RPGMonsterWave03PBR
                { "Battle Bee", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/BattleBeePBR.prefab", 0.85f, 0.2f, "BattleBee") },
                // RPG Monster Wave PBR
                { "Golem", new ModelMapping(
                    "Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/GolemPBR.prefab", 1.0f, 0f, "Golem") },
                // RPGMonsterWave03PBR
                { "Salamander", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/SalamanderPBR.prefab", 0.9f, 0f, "Salamander") },
                // RPGMonsterWave03PBR
                { "Fishman", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/FishmanPBR.prefab", 0.9f, 0f, "Fishman") },
                // RPGMonsterWave02PBR
                { "Chest Monster", new ModelMapping(
                    "Assets/RPGMonsterWave02PBR/Prefabs/Character/ChestMonsterPBRDefault.prefab", 0.9f, 0f, "ChestMonster") },
                // RPGTinyHeroWavePBR
                { "Blacksmith", new ModelMapping(
                    "Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC18.prefab", 0.95f) },
                // RTS Mini Legion Human
                { "Horseman", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Human PBR/Prefabs/Horseman_Standard_PBR.prefab", 0.9f) },

                // ==========================================
                // 3-COST UNITS
                // ==========================================

                // RTS Mini Legion Sentinel
                { "Druid", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Sentinel PBR/Prefabs/Druid StandardPBR.prefab", 1.0f) },
                // RTS Mini Legion Undead
                { "Death Knight", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Undead PBR/Prefabs/Death Knight Standard.prefab", 1.0f) },
                // RTS Mini Legion Undead
                { "Death Rider", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Undead PBR/Prefabs/Death Rider Standard.prefab", 1.0f) },
                // RTS Mini Legion Warband
                { "Warlock", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Warband PBR/Prefabs/Warlock Standard.prefab", 1.0f) },
                // RTS Mini Legion Warband
                { "Berserker", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Warband PBR/Prefabs/Berserker Standard.prefab", 1.0f) },
                // RPGMonsterWave03PBR
                { "Cyclops", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/CyclopsPBR.prefab", 1.1f, 0f, "Cyclops") },
                // RPGMonsterWave02PBR
                { "Werewolf", new ModelMapping(
                    "Assets/RPGMonsterWave02PBR/Prefabs/Character/WerewolfPBRDefault.prefab", 1.0f, 0f, "Werewolf") },
                // RPGMonsterWave03PBR
                { "Naga Wizard", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/NagaWizardPBR.prefab", 1.0f, 0f, "NagaWizard") },

                // ==========================================
                // 4-COST UNITS
                // ==========================================

                // RTS Mini Legion Human
                { "Griffin", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Human PBR/Prefabs/Griffin_Standard_PBR.prefab", 1.1f, 0.2f) },
                // RTS Mini Legion Sentinel
                { "Demon Hunter", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Sentinel PBR/Prefabs/Demon Hunter StandardPBR.prefab", 1.1f) },
                // RTS Mini Legion Warband
                { "Hog Rider", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Warband PBR/Prefabs/HogRider Standard.prefab", 1.1f) },
                // RTS Mini Legion Sentinel
                { "Treeant", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Sentinel PBR/Prefabs/Treeant StandardPBR.prefab", 1.2f) },
                // RPGMonsterWave02PBR
                { "Black Knight", new ModelMapping(
                    "Assets/RPGMonsterWave02PBR/Prefabs/Character/BlackKnightPBRDefault.prefab", 1.1f, 0f, "BlackKnight") },
                // RPGMonsterWave03PBR
                { "Bishop Knight", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/BishopKnightPBR.prefab", 1.1f, 0f, "BishopKnight") },
                // RPGMonsterWave02PBR
                { "Flying Demon", new ModelMapping(
                    "Assets/RPGMonsterWave02PBR/Prefabs/Character/FylingDemonPBRDefault.prefab", 1.1f, 0.3f, "FylingDemon") },

                // ==========================================
                // 5-COST UNITS
                // ==========================================

                // RTS Mini Legion Warband
                { "Drake", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Warband PBR/Prefabs/Drake Standard.prefab", 1.2f, 0.2f) },
                // RTS Mini Legion Undead
                { "Lich", new ModelMapping(
                    "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Undead PBR/Prefabs/Lich Standard.prefab", 1.15f) },
                // RPGMonsterWave03PBR
                { "Demon King", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/DemonKingPBR.prefab", 1.3f, 0f, "DemonKing") },
                // RPG Monster Wave PBR
                { "Fat Dragon", new ModelMapping(
                    "Assets/RPG Monster Wave PBR/Prefabs/PBRDefault/DragonPBR.prefab", 1.15f, 0f, "Dragon") },
                // RPGMonsterWave4
                { "Flame Knight", new ModelMapping(
                    "Assets/RPGMonsterWave4/Prefab/FlameKnight.prefab", 1.2f, 0f, "FlameKnight") },

                // ==========================================
                // PvE CRITTERS
                // ==========================================

                // RPGMonsterWave03PBR
                { "Stingray", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/StingRayPBR.prefab", 0.6f, 0.1f, "StingRay") },
                // RPGMonsterWave03PBR
                { "Cactus", new ModelMapping(
                    "Assets/RPGMonsterWave03PBR/Prefab/Characters/CactusPBR.prefab", 0.7f, 0f, "Cactus") },
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

                    // Set animation clip names for RPG Monster Wave models (prefix-based)
                    // RTS Mini Legion models leave clips empty for auto-detect
                    if (!string.IsNullOrEmpty(modelData.animPrefix))
                    {
                        string prefix = modelData.animPrefix;
                        entry.idleClip = $"{prefix}_IdleBattle";
                        entry.walkClip = $"{prefix}_MoveFWD";
                        entry.attackClip = $"{prefix}_Attack01";
                        entry.abilityClip = $"{prefix}_Attack02";
                        entry.hitClip = $"{prefix}_GetHit";
                        entry.deathClip = $"{prefix}_Die";
                    }

                    database.unitModels.Add(entry);
                    successCount++;
                    Debug.Log($"[ModelMapper] Added {unitName} -> {prefab.name} (scale: {modelData.scale}, anim: {(modelData.animPrefix ?? "auto-detect")})");
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
            string[] prefabFolders = new string[]
            {
                "Assets/RPG Monster Wave PBR/Prefabs",
                "Assets/RPGMonsterWave02PBR/Prefabs",
                "Assets/RPGMonsterWave03PBR/Prefab",
                "Assets/RPGMonsterWave4/Prefab",
                "Assets/RPGTinyHeroWavePBR/Prefab",
                "Assets/RPGTinyHeroWavePBR/Prefab/ModularCharacters",
                "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Human PBR/Prefabs",
                "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Sentinel PBR/Prefabs",
                "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Undead PBR/Prefabs",
                "Assets/RTS Mini Legion Fantasy PBR/Mini Legion Warband PBR/Prefabs"
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

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = System.IO.Path.GetFileName(path);
                    Debug.Log($"    {fileName}");
                }
            }

            Debug.Log($"[ModelMapper] ========== Total: {totalFound} prefabs ==========");
        }
    }
}
