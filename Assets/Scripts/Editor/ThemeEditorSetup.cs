#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Crestforge.Cosmetics;
using System.IO;

namespace Crestforge.Editor
{
    [CustomEditor(typeof(ThemeEditorSetup))]
    public class ThemeEditorSetupInspector : UnityEditor.Editor
    {
        private string newThemeName = "My Theme";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ThemeEditorSetup setup = (ThemeEditorSetup)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Preview Controls", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Preview", GUILayout.Height(30)))
            {
                setup.GeneratePreview();
            }

            if (GUILayout.Button("Clear Preview", GUILayout.Height(25)))
            {
                setup.ClearPreview();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Load Existing Theme", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Drag an existing theme asset to 'Theme To Edit' above, then click 'Load Theme' to edit it.",
                MessageType.Info
            );

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Theme", GUILayout.Height(30)))
            {
                if (setup.themeToEdit == null)
                {
                    EditorUtility.DisplayDialog("No Theme Selected",
                        "Please drag a BattlefieldTheme asset into the 'Theme To Edit' field first.",
                        "OK");
                }
                else
                {
                    setup.LoadThemeForEditing();
                    // Set the theme name for saving
                    newThemeName = setup.themeToEdit.themeName;
                }
            }
            if (GUILayout.Button("Clear Loaded", GUILayout.Height(30)))
            {
                setup.ClearLoadedTheme();
            }
            EditorGUILayout.EndHorizontal();

            // Show refresh ground button if a theme is loaded
            if (setup.themeToEdit != null)
            {
                if (GUILayout.Button("Refresh Ground Preview", GUILayout.Height(25)))
                {
                    setup.SpawnGroundPreview();
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Test Procedural Themes", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Meadow", GUILayout.Height(25)))
            {
                setup.PreviewMeadowTheme();
            }
            if (GUILayout.Button("Castle", GUILayout.Height(25)))
            {
                setup.PreviewCastleTheme();
            }
            if (GUILayout.Button("Clear", GUILayout.Height(25)))
            {
                setup.ClearTheme();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Save Theme", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Option A: Drag prefabs from Project into Scene, position them, save.\n" +
                "Option B: Load existing theme, modify positions, re-save.",
                MessageType.Info
            );

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Theme Name:", GUILayout.Width(85));
            newThemeName = EditorGUILayout.TextField(newThemeName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan & Save Theme", GUILayout.Height(35)))
            {
                ScanAndSaveTheme(setup, newThemeName);
            }

            // If we loaded a theme and have placements, offer quick re-save
            if (setup.themeToEdit != null && setup.placedObjects.Count > 0)
            {
                if (GUILayout.Button("Update Current Theme", GUILayout.Height(35)))
                {
                    UpdateLoadedTheme(setup);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Show detected objects
            if (setup.placedObjects != null && setup.placedObjects.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField($"Detected Objects ({setup.placedObjects.Count})", EditorStyles.boldLabel);

                foreach (var placement in setup.placedObjects)
                {
                    EditorGUILayout.BeginHorizontal();

                    string status = placement.prefab != null ? "✓" : (placement.isPrefabInstance ? "◐" : "○");
                    EditorGUILayout.LabelField(status, GUILayout.Width(20));
                    EditorGUILayout.LabelField(placement.objectName, GUILayout.Width(150));
                    EditorGUILayout.LabelField(placement.zone.ToString(), GUILayout.Width(100));

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.HelpBox(
                    "✓ = Prefab linked  ◐ = Prefab instance (will auto-link)  ○ = Will create prefab",
                    MessageType.None
                );
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Zone Guide", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• BackLeft/Right/Center - Behind player\n" +
                "• SideLeft/Right - Beside battlefield\n" +
                "• FrontLeft/Right/Center - Enemy side\n" +
                "• BenchArea - Behind bench slots",
                MessageType.None
            );
        }

        private void ScanAndSaveTheme(ThemeEditorSetup setup, string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a theme name", "OK");
                return;
            }

            // First scan the scene
            setup.ScanSceneObjects();

            if (setup.placedObjects == null || setup.placedObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("No Objects Found",
                    "No objects found in the scene to save.\n\n" +
                    "Drag prefabs from your Project window into the Scene view, " +
                    "position them around the zone markers, then try again.",
                    "OK");
                return;
            }

            // Detect prefab instances and get their source prefabs
            int prefabInstances = 0;
            int nonPrefabs = 0;

            foreach (var placement in setup.placedObjects)
            {
                if (placement.sceneObject == null) continue;

                // Check if this is a prefab instance
                GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(placement.sceneObject);
                if (prefabSource != null)
                {
                    placement.prefab = prefabSource;
                    placement.isPrefabInstance = true;
                    prefabInstances++;
                }
                else
                {
                    // Check if it's the root of a prefab instance
                    prefabSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(placement.sceneObject);
                    if (prefabSource != null)
                    {
                        placement.prefab = prefabSource;
                        placement.isPrefabInstance = true;
                        prefabInstances++;
                    }
                    else
                    {
                        placement.isPrefabInstance = false;
                        nonPrefabs++;
                    }
                }
            }

            // If there are non-prefab objects, ask about creating prefabs
            if (nonPrefabs > 0)
            {
                bool createPrefabs = EditorUtility.DisplayDialog("Create Prefabs?",
                    $"Found {nonPrefabs} object(s) that are not prefab instances.\n\n" +
                    "These need to be saved as prefabs to be included in the theme.\n\n" +
                    "Create prefabs for these objects?",
                    "Create Prefabs", "Skip These Objects");

                if (createPrefabs)
                {
                    CreatePrefabsForNonPrefabObjects(setup, themeName);
                }
            }

            // Ask where to save the theme
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Battlefield Theme",
                themeName.Replace(" ", "") + "Theme",
                "asset",
                "Choose where to save the theme asset",
                "Assets/Resources/ScriptableObjects/Themes"
            );

            if (string.IsNullOrEmpty(path))
            {
                return; // User cancelled
            }

            // Create the theme asset
            BattlefieldTheme theme = CreateThemeFromPlacements(setup, themeName);

            // Ensure directory exists
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(theme, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int savedCount = theme.zonePlacements.Count;
            EditorUtility.DisplayDialog("Theme Saved!",
                $"Theme '{themeName}' saved with {savedCount} object(s).\n\n" +
                $"Path: {path}\n\n" +
                "To use this theme:\n" +
                "1. Assign it to BattlefieldManager.currentTheme\n" +
                "2. Or load it at runtime with Resources.Load",
                "OK");

            Selection.activeObject = theme;
            EditorGUIUtility.PingObject(theme);
        }

        private void UpdateLoadedTheme(ThemeEditorSetup setup)
        {
            if (setup.themeToEdit == null)
            {
                EditorUtility.DisplayDialog("No Theme", "No theme is currently loaded for editing.", "OK");
                return;
            }

            // Check if we have any placements to update
            if (setup.placedObjects == null || setup.placedObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("No Objects",
                    "No objects are being tracked.\n\n" +
                    "This can happen if:\n" +
                    "- Scripts were recompiled after loading the theme\n" +
                    "- The theme wasn't loaded properly\n\n" +
                    "Try: Load the theme again, make changes, then update.",
                    "OK");
                return;
            }

            Debug.Log($"[ThemeEditor] Updating {setup.placedObjects.Count} placements...");

            // Update positions from current scene objects
            int validObjects = 0;
            int nullObjects = 0;
            foreach (var placement in setup.placedObjects)
            {
                if (placement.sceneObject != null)
                {
                    Vector3 oldOffset = placement.positionOffset;
                    // Simple: world position = offset from center
                    placement.positionOffset = placement.sceneObject.transform.position;
                    placement.rotation = placement.sceneObject.transform.eulerAngles;
                    placement.scale = placement.sceneObject.transform.lossyScale;
                    placement.zone = BattlefieldZone.Ground;  // Always use center

                    Debug.Log($"  [{placement.objectName}] position: {oldOffset} -> {placement.positionOffset}");
                    validObjects++;
                }
                else
                {
                    Debug.LogWarning($"  [{placement.objectName}] sceneObject is NULL - was it deleted?");
                    nullObjects++;
                }
            }

            if (validObjects == 0)
            {
                EditorUtility.DisplayDialog("No Valid Objects",
                    $"All {nullObjects} tracked objects have null scene references.\n\n" +
                    "This usually means scripts were recompiled.\n\n" +
                    "Please reload the theme and try again.",
                    "OK");
                return;
            }

            // Clear and rebuild the theme's placements
            setup.themeToEdit.zonePlacements.Clear();

            foreach (var placement in setup.placedObjects)
            {
                if (placement.prefab == null)
                {
                    Debug.LogWarning($"Skipping '{placement.objectName}' - no prefab available");
                    continue;
                }

                setup.themeToEdit.zonePlacements.Add(new ThemeZonePlacement
                {
                    zone = placement.zone,
                    prefab = placement.prefab,
                    positionOffset = placement.positionOffset,
                    rotation = placement.rotation,
                    scale = placement.scale
                });
            }

            // Mark the asset as dirty and save
            EditorUtility.SetDirty(setup.themeToEdit);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Force serialization to disk
            string assetPath = AssetDatabase.GetAssetPath(setup.themeToEdit);
            AssetDatabase.ImportAsset(assetPath);

            // Verify the save worked
            Debug.Log($"[ThemeEditor] Saved {setup.themeToEdit.zonePlacements.Count} placements to: {assetPath}");
            foreach (var p in setup.themeToEdit.zonePlacements)
            {
                Debug.Log($"  - {p.prefab?.name} at zone {p.zone}, offset {p.positionOffset}");
            }

            EditorUtility.DisplayDialog("Theme Updated!",
                $"Theme '{setup.themeToEdit.themeName}' updated.\n\n" +
                $"Updated: {validObjects} objects\n" +
                $"Skipped (null): {nullObjects} objects\n" +
                $"Total saved: {setup.themeToEdit.zonePlacements.Count} placements\n\n" +
                $"Saved to: {assetPath}",
                "OK");
        }

        private void CreatePrefabsForNonPrefabObjects(ThemeEditorSetup setup, string themeName)
        {
            // Create a folder for this theme's prefabs
            string prefabFolder = $"Assets/Prefabs/Themes/{themeName.Replace(" ", "")}";

            if (!Directory.Exists(prefabFolder))
            {
                Directory.CreateDirectory(prefabFolder);
                AssetDatabase.Refresh();
            }

            foreach (var placement in setup.placedObjects)
            {
                if (placement.sceneObject == null) continue;
                if (placement.isPrefabInstance) continue;  // Already has a prefab

                // Create a unique prefab name
                string prefabName = placement.sceneObject.name;
                string prefabPath = $"{prefabFolder}/{prefabName}.prefab";

                // Handle duplicates
                int counter = 1;
                while (File.Exists(prefabPath))
                {
                    prefabPath = $"{prefabFolder}/{prefabName}_{counter}.prefab";
                    counter++;
                }

                // Create the prefab
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(placement.sceneObject, prefabPath);
                if (prefab != null)
                {
                    placement.prefab = prefab;
                    placement.isPrefabInstance = true;
                    Debug.Log($"Created prefab: {prefabPath}");
                }
                else
                {
                    Debug.LogWarning($"Failed to create prefab for: {placement.sceneObject.name}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private BattlefieldTheme CreateThemeFromPlacements(ThemeEditorSetup setup, string themeName)
        {
            BattlefieldTheme theme = ScriptableObject.CreateInstance<BattlefieldTheme>();
            theme.themeId = themeName.ToLower().Replace(" ", "_");
            theme.themeName = themeName;
            theme.description = $"Custom theme created in Theme Editor";

            // Custom themes don't use procedural ground/bench by default
            theme.skipGround = true;
            theme.skipBench = true;

            foreach (var placement in setup.placedObjects)
            {
                if (placement.prefab == null)
                {
                    Debug.LogWarning($"Skipping '{placement.objectName}' - no prefab available");
                    continue;
                }

                theme.zonePlacements.Add(new ThemeZonePlacement
                {
                    zone = placement.zone,
                    prefab = placement.prefab,
                    positionOffset = placement.positionOffset,
                    rotation = placement.rotation,
                    scale = placement.scale
                });
            }

            return theme;
        }
    }
}
#endif
