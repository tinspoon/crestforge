#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Crestforge.Visuals;
using Crestforge.Data;
using System.IO;
using System.Collections.Generic;

namespace Crestforge.Editor
{
    /// <summary>
    /// Editor tools for setting up unit 3D models
    /// </summary>
    public class UnitModelSetup : EditorWindow
    {
        private UnitModelDatabase database;
        private Vector2 scrollPos;

        [MenuItem("Crestforge/Unit Models/Setup Window")]
        public static void ShowWindow()
        {
            GetWindow<UnitModelSetup>("Unit Model Setup");
        }

        [MenuItem("Crestforge/Unit Models/Create Model Database")]
        public static void CreateDatabase()
        {
            // Check if one already exists
            string[] existingGuids = AssetDatabase.FindAssets("t:UnitModelDatabase");
            if (existingGuids.Length > 0)
            {
                string existingPath = AssetDatabase.GUIDToAssetPath(existingGuids[0]);
                Debug.Log($"UnitModelDatabase already exists at: {existingPath}");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnitModelDatabase>(existingPath);
                return;
            }

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");

            // Create new database
            UnitModelDatabase database = ScriptableObject.CreateInstance<UnitModelDatabase>();
            AssetDatabase.CreateAsset(database, "Assets/Data/UnitModelDatabase.asset");
            AssetDatabase.SaveAssets();

            Debug.Log("Created UnitModelDatabase at Assets/Data/UnitModelDatabase.asset");
            Selection.activeObject = database;
        }

        [MenuItem("Crestforge/Unit Models/Create Model Folders")]
        public static void CreateModelFolders()
        {
            // Create folder structure for models
            string basePath = "Assets/Models";
            if (!AssetDatabase.IsValidFolder(basePath))
                AssetDatabase.CreateFolder("Assets", "Models");

            string unitsPath = basePath + "/Units";
            if (!AssetDatabase.IsValidFolder(unitsPath))
                AssetDatabase.CreateFolder(basePath, "Units");

            // Create subfolders by origin
            string[] origins = { "Human", "Undead", "Beast", "Elemental", "Demon", "Fey", "Shared" };
            foreach (string origin in origins)
            {
                string originPath = unitsPath + "/" + origin;
                if (!AssetDatabase.IsValidFolder(originPath))
                    AssetDatabase.CreateFolder(unitsPath, origin);
            }

            AssetDatabase.Refresh();
            Debug.Log("Created model folder structure at Assets/Models/Units/");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Unit Model Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Database reference
            database = (UnitModelDatabase)EditorGUILayout.ObjectField(
                "Model Database",
                database,
                typeof(UnitModelDatabase),
                false
            );

            if (database == null)
            {
                EditorGUILayout.HelpBox(
                    "No UnitModelDatabase assigned.\n\n" +
                    "Click 'Create Model Database' to create one, or drag an existing one here.",
                    MessageType.Warning
                );

                if (GUILayout.Button("Create Model Database"))
                {
                    CreateDatabase();
                    // Try to find the newly created database
                    string[] guids = AssetDatabase.FindAssets("t:UnitModelDatabase");
                    if (guids.Length > 0)
                    {
                        database = AssetDatabase.LoadAssetAtPath<UnitModelDatabase>(
                            AssetDatabase.GUIDToAssetPath(guids[0])
                        );
                    }
                }

                return;
            }

            EditorGUILayout.Space(10);

            // Quick actions
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Create Model Folders"))
            {
                CreateModelFolders();
            }

            if (GUILayout.Button("Populate Empty Entries for All Units"))
            {
                PopulateEmptyEntries();
            }

            if (GUILayout.Button("Auto-Assign Models from Folder"))
            {
                AutoAssignModels();
            }

            EditorGUILayout.Space(10);

            // Unit list with model status
            EditorGUILayout.LabelField("Unit Model Status", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Find all unit data
            string[] unitGuids = AssetDatabase.FindAssets("t:UnitData");
            int assigned = 0;
            int total = unitGuids.Length;

            foreach (string guid in unitGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(path);
                if (unit == null) continue;

                bool hasModel = database.HasCustomModel(unit.unitName);
                if (hasModel) assigned++;

                EditorGUILayout.BeginHorizontal();

                // Status icon
                GUIStyle iconStyle = new GUIStyle(EditorStyles.label);
                iconStyle.normal.textColor = hasModel ? Color.green : Color.gray;
                EditorGUILayout.LabelField(hasModel ? "\u2713" : "\u2717", iconStyle, GUILayout.Width(20));

                // Unit name
                EditorGUILayout.LabelField(unit.unitName, GUILayout.Width(150));

                // Model field (find in database)
                var entry = database.GetModelEntry(unit.unitName);
                GameObject currentModel = entry?.modelPrefab;

                GameObject newModel = (GameObject)EditorGUILayout.ObjectField(
                    currentModel,
                    typeof(GameObject),
                    false
                );

                if (newModel != currentModel)
                {
                    AssignModelToUnit(unit.unitName, newModel);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Models Assigned: {assigned} / {total}", EditorStyles.boldLabel);

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "How to add custom models:\n\n" +
                "1. Import FBX/model files to Assets/Models/Units/\n" +
                "2. Create a prefab from each model\n" +
                "3. Drag prefabs to the unit entries above\n" +
                "   OR use 'Auto-Assign' if prefab names match unit names\n\n" +
                "Recommended free models:\n" +
                "- Quaternius.com (free low-poly characters)\n" +
                "- Mixamo.com (free rigged characters)\n" +
                "- Meshy.ai (AI-generated 3D models)\n" +
                "- Synty POLYGON series (paid, high quality)",
                MessageType.Info
            );
        }

        private void PopulateEmptyEntries()
        {
            if (database == null) return;

            // Find all unit data
            string[] unitGuids = AssetDatabase.FindAssets("t:UnitData");

            Undo.RecordObject(database, "Populate Unit Model Entries");

            foreach (string guid in unitGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(path);
                if (unit == null) continue;

                // Check if entry already exists
                bool exists = false;
                foreach (var entry in database.unitModels)
                {
                    if (entry.unitName?.ToLower() == unit.unitName.ToLower())
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    database.unitModels.Add(new UnitModelDatabase.UnitModelEntry
                    {
                        unitName = unit.unitName,
                        modelPrefab = null,
                        scale = 1f,
                        yOffset = 0f
                    });
                }
            }

            database.ClearCache();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"Populated {unitGuids.Length} unit entries in database");
        }

        private void AutoAssignModels()
        {
            if (database == null) return;

            // Find all prefabs in Models/Units folder
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Models/Units" });

            Undo.RecordObject(database, "Auto-Assign Unit Models");

            int assignedCount = 0;
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                string prefabName = prefab.name.ToLower();

                // Try to match to a unit
                foreach (var entry in database.unitModels)
                {
                    if (entry.modelPrefab != null) continue; // Skip already assigned

                    string unitName = entry.unitName.ToLower().Replace(" ", "").Replace("_", "");

                    if (prefabName.Contains(unitName) || unitName.Contains(prefabName))
                    {
                        entry.modelPrefab = prefab;
                        assignedCount++;
                        Debug.Log($"Auto-assigned {prefab.name} to {entry.unitName}");
                        break;
                    }
                }
            }

            database.ClearCache();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"Auto-assigned {assignedCount} models");
        }

        private void AssignModelToUnit(string unitName, GameObject model)
        {
            if (database == null) return;

            Undo.RecordObject(database, "Assign Unit Model");

            // Find or create entry
            UnitModelDatabase.UnitModelEntry targetEntry = null;
            foreach (var entry in database.unitModels)
            {
                if (entry.unitName?.ToLower() == unitName.ToLower())
                {
                    targetEntry = entry;
                    break;
                }
            }

            if (targetEntry == null)
            {
                targetEntry = new UnitModelDatabase.UnitModelEntry
                {
                    unitName = unitName,
                    scale = 1f,
                    yOffset = 0f
                };
                database.unitModels.Add(targetEntry);
            }

            targetEntry.modelPrefab = model;
            database.ClearCache();
            EditorUtility.SetDirty(database);
        }
    }
}
#endif
