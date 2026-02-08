using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Crestforge.Visuals;

namespace Crestforge.Editor
{
    /// <summary>
    /// Editor window for adjusting unit model scales with sliders and a live 3D preview.
    /// Opens a preview scene with hex tiles and all unit models laid out by cost tier.
    /// </summary>
    public class ModelScaleCalibrator : EditorWindow
    {
        private const float HEX_RADIUS = 0.45f;
        private static float HEX_WIDTH => HEX_RADIUS * 1.732f;
        private const float MAX_WIDTH_MULT = 2.0f;
        private const int MAX_COLS = 8;

        private static readonly Dictionary<int, float> HeightMultipliers = new Dictionary<int, float>
        {
            { 0, 1.0f }, { 1, 1.2f }, { 2, 1.35f }, { 3, 1.5f }, { 4, 1.65f }, { 5, 1.8f },
        };

        // Tier colors for hex tiles
        private static readonly Dictionary<int, Color> TierColors = new Dictionary<int, Color>
        {
            { 0, new Color(0.5f, 0.5f, 0.5f) },    // PvE - gray
            { 1, new Color(0.7f, 0.7f, 0.7f) },    // 1-cost - light gray
            { 2, new Color(0.4f, 0.7f, 0.4f) },    // 2-cost - green
            { 3, new Color(0.4f, 0.5f, 0.9f) },    // 3-cost - blue
            { 4, new Color(0.7f, 0.4f, 0.8f) },    // 4-cost - purple
            { 5, new Color(1f, 0.8f, 0.3f) },       // 5-cost - gold
        };

        private static readonly Dictionary<string, int> UnitCosts = new Dictionary<string, int>
        {
            { "Footman", 1 }, { "Archer", 1 }, { "Duelist", 1 }, { "Skeleton Warrior", 1 },
            { "Elf Ranger", 1 }, { "Bat", 1 }, { "Red Slime", 1 }, { "Mushroom", 1 },
            { "Crawler", 1 }, { "Little Demon", 1 }, { "Rat Assassin", 1 }, { "Cleric", 1 },
            { "Knight", 2 }, { "Mage", 2 }, { "Dryad", 2 }, { "Specter", 2 },
            { "Battle Bee", 2 }, { "Golem", 2 }, { "Salamander", 2 }, { "Fishman", 2 },
            { "Chest Monster", 2 }, { "Blacksmith", 2 }, { "Horseman", 2 },
            { "Druid", 3 }, { "Death Knight", 3 }, { "Death Rider", 3 }, { "Evil Mage", 3 },
            { "Orc", 3 }, { "Cyclops", 3 }, { "Werewolf", 3 }, { "Naga Wizard", 3 },
            { "Griffin", 4 }, { "Demon Hunter", 4 }, { "Champion", 4 }, { "Treeant", 4 },
            { "Black Knight", 4 }, { "Bishop Knight", 4 }, { "Flying Demon", 4 },
            { "Shadow Knight", 5 }, { "Lich", 5 }, { "Demon King", 5 }, { "Titan Drake", 5 },
            { "Flame Knight", 5 },
            { "Stingray", 0 }, { "Cactus", 0 },
        };

        private UnitModelDatabase database;
        private Dictionary<string, Vector3> measurements;
        private Vector2 scrollPos;
        private Dictionary<int, bool> costFoldouts = new Dictionary<int, bool>();
        private bool hasUnsavedChanges = false;

        // Preview scene
        private Scene previewScene;
        private bool previewOpen = false;
        private Dictionary<string, GameObject> previewModels = new Dictionary<string, GameObject>();
        private Dictionary<string, Vector3> previewPositions = new Dictionary<string, Vector3>();

        [MenuItem("Crestforge/Model Scale Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<ModelScaleCalibrator>("Model Scale Editor");
            window.minSize = new Vector2(550, 400);
        }

        private void OnEnable()
        {
            database = FindDatabase();
            for (int i = -1; i <= 5; i++)
                costFoldouts[i] = true;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            ClosePreview();
        }

        private void OnGUI()
        {
            if (database == null)
            {
                database = FindDatabase();
                if (database == null)
                {
                    EditorGUILayout.HelpBox("UnitModelDatabase not found! Run Map Models first.", MessageType.Error);
                    return;
                }
            }

            // Check if preview scene was closed externally
            if (previewOpen && !previewScene.IsValid())
            {
                previewOpen = false;
                previewModels.Clear();
                previewPositions.Clear();
            }

            float hexDiameter = HEX_RADIUS * 2f;

            // Header
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Model Scale Editor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Hex diameter: {hexDiameter:F2}    Hex width: {HEX_WIDTH:F2}", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            // Top buttons row
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Measure Bounds", GUILayout.Height(24)))
            {
                measurements = MeasureAllModels(database);
                Debug.Log($"[ScaleEditor] Measured {measurements.Count} models");
            }
            if (GUILayout.Button("Auto-Calibrate All", GUILayout.Height(24)))
            {
                if (measurements == null)
                    measurements = MeasureAllModels(database);
                AutoCalibrateAll();
                RefreshAllPreviewModels();
            }
            GUI.enabled = hasUnsavedChanges;
            if (GUILayout.Button("Save", GUILayout.Width(60), GUILayout.Height(24)))
            {
                SaveDatabase();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Preview button row
            EditorGUILayout.BeginHorizontal();
            if (!previewOpen)
            {
                if (GUILayout.Button("Open Preview Scene", GUILayout.Height(24)))
                    OpenPreview();
            }
            else
            {
                if (GUILayout.Button("Close Preview Scene", GUILayout.Height(24)))
                    ClosePreview();
                if (GUILayout.Button("Focus Camera", GUILayout.Width(100), GUILayout.Height(24)))
                    FocusPreviewCamera();
            }
            EditorGUILayout.EndHorizontal();

            if (hasUnsavedChanges)
            {
                EditorGUILayout.HelpBox("Unsaved changes", MessageType.Warning);
            }

            EditorGUILayout.Space(4);

            // Column headers
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16); // indent space
            EditorGUILayout.LabelField("Unit", EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Scale", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("", GUILayout.MinWidth(80)); // slider
            EditorGUILayout.LabelField("Y Off", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Final H", EditorStyles.miniLabel, GUILayout.Width(45));
            EditorGUILayout.LabelField("Raw H", EditorStyles.miniLabel, GUILayout.Width(40));
            EditorGUILayout.LabelField("Raw W", EditorStyles.miniLabel, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            // Scrollable unit list grouped by cost
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            var grouped = GroupByCost();
            string[] tierNames = { "PvE (0)", "1-Cost", "2-Cost", "3-Cost", "4-Cost", "5-Cost" };

            foreach (int cost in new[] { 0, 1, 2, 3, 4, 5, -1 })
            {
                if (!grouped.ContainsKey(cost)) continue;

                string tierLabel;
                if (cost >= 0 && cost < tierNames.Length)
                {
                    float targetH = hexDiameter * (HeightMultipliers.ContainsKey(cost) ? HeightMultipliers[cost] : 1.35f);
                    tierLabel = $"{tierNames[cost]}  ({grouped[cost].Count} units, target h={targetH:F2})";
                }
                else
                {
                    tierLabel = $"Unknown Cost ({grouped[cost].Count} units)";
                }

                costFoldouts[cost] = EditorGUILayout.Foldout(costFoldouts[cost], tierLabel, true, EditorStyles.foldoutHeader);
                if (!costFoldouts[cost]) continue;

                EditorGUI.indentLevel++;
                foreach (var entry in grouped[cost])
                    DrawUnitRow(entry, cost);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        private Dictionary<int, List<UnitModelDatabase.UnitModelEntry>> GroupByCost()
        {
            var grouped = new Dictionary<int, List<UnitModelDatabase.UnitModelEntry>>();
            foreach (var entry in database.unitModels)
            {
                int cost = UnitCosts.ContainsKey(entry.unitName) ? UnitCosts[entry.unitName] : -1;
                if (!grouped.ContainsKey(cost))
                    grouped[cost] = new List<UnitModelDatabase.UnitModelEntry>();
                grouped[cost].Add(entry);
            }
            return grouped;
        }

        private void DrawUnitRow(UnitModelDatabase.UnitModelEntry entry, int cost)
        {
            if (entry.modelPrefab == null)
            {
                EditorGUILayout.LabelField(entry.unitName, "NO PREFAB", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.BeginHorizontal();

            // Unit name (clickable to focus in preview)
            if (previewOpen && previewModels.ContainsKey(entry.unitName))
            {
                if (GUILayout.Button(entry.unitName, EditorStyles.linkLabel, GUILayout.Width(120)))
                {
                    Selection.activeGameObject = previewModels[entry.unitName];
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
            else
            {
                EditorGUILayout.LabelField(entry.unitName, GUILayout.Width(120));
            }

            // Scale field + slider
            EditorGUI.BeginChangeCheck();
            float newScale = EditorGUILayout.FloatField(entry.scale, GUILayout.Width(50));
            newScale = GUILayout.HorizontalSlider(newScale, 0.05f, 1.5f, GUILayout.MinWidth(80));

            if (EditorGUI.EndChangeCheck())
            {
                newScale = Mathf.Clamp(newScale, 0.01f, 5f);
                newScale = Mathf.Round(newScale * 100f) / 100f;
                if (newScale != entry.scale)
                {
                    Undo.RecordObject(database, "Change unit scale");
                    entry.scale = newScale;
                    hasUnsavedChanges = true;
                    UpdatePreviewModel(entry);
                }
            }

            // Y Offset field
            EditorGUI.BeginChangeCheck();
            float newYOffset = EditorGUILayout.FloatField(entry.yOffset, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck())
            {
                newYOffset = Mathf.Round(newYOffset * 100f) / 100f;
                if (newYOffset != entry.yOffset)
                {
                    Undo.RecordObject(database, "Change unit yOffset");
                    entry.yOffset = newYOffset;
                    hasUnsavedChanges = true;
                    UpdatePreviewModel(entry);
                }
            }

            // Measurement data columns
            if (measurements != null && measurements.TryGetValue(entry.unitName, out Vector3 rawSize) && rawSize.y > 0.001f)
            {
                float finalH = rawSize.y * entry.scale;
                float rawWidth = Mathf.Max(rawSize.x, rawSize.z);

                float hexDiameter = HEX_RADIUS * 2f;
                float targetH = hexDiameter * (HeightMultipliers.ContainsKey(cost) ? HeightMultipliers[cost] : 1.35f);
                float ratio = targetH > 0 ? finalH / targetH : 1f;
                Color labelColor = ratio > 0.8f && ratio < 1.2f ? new Color(0.3f, 0.8f, 0.3f) :
                                   ratio > 0.6f && ratio < 1.4f ? new Color(0.9f, 0.8f, 0.2f) :
                                   new Color(0.9f, 0.3f, 0.3f);
                var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = labelColor } };

                EditorGUILayout.LabelField($"{finalH:F2}", style, GUILayout.Width(45));
                EditorGUILayout.LabelField($"{rawSize.y:F1}", EditorStyles.miniLabel, GUILayout.Width(40));
                EditorGUILayout.LabelField($"{rawWidth:F1}", EditorStyles.miniLabel, GUILayout.Width(40));
            }
            else
            {
                EditorGUILayout.LabelField("--", EditorStyles.miniLabel, GUILayout.Width(45));
                EditorGUILayout.LabelField("--", EditorStyles.miniLabel, GUILayout.Width(40));
                EditorGUILayout.LabelField("--", EditorStyles.miniLabel, GUILayout.Width(40));
            }

            EditorGUILayout.EndHorizontal();
        }

        // ==================== PREVIEW SCENE ====================

        private void OpenPreview()
        {
            if (previewOpen) ClosePreview();

            previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            previewScene.name = "Model Preview";
            previewOpen = true;
            previewModels.Clear();
            previewPositions.Clear();

            // Directional light
            var lightObj = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(lightObj, previewScene);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.97f, 0.9f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ambient light boost
            var ambientObj = new GameObject("Fill Light");
            SceneManager.MoveGameObjectToScene(ambientObj, previewScene);
            var fill = ambientObj.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.4f;
            fill.color = new Color(0.7f, 0.8f, 1f);
            ambientObj.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            SceneManager.MoveGameObjectToScene(ground, previewScene);
            ground.transform.position = new Vector3(0, -0.01f, 0);
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (groundMat.shader.name == "Hidden/InternalErrorShader")
                groundMat = new Material(Shader.Find("Standard"));
            groundMat.color = new Color(0.25f, 0.28f, 0.25f);
            ground.GetComponent<Renderer>().material = groundMat;

            // Layout units by cost tier
            var grouped = GroupByCost();
            float hexSpacing = HEX_WIDTH + 0.2f;
            float rowHeight = HEX_RADIUS * 3.5f;
            float tierGap = 1.0f;
            float currentZ = 0;

            foreach (int cost in new[] { 5, 4, 3, 2, 1, 0 }) // High cost at back (far), low cost at front
            {
                if (!grouped.ContainsKey(cost)) continue;

                var entries = grouped[cost];
                Color tileColor = TierColors.ContainsKey(cost) ? TierColors[cost] : Color.gray;

                // Wrap into rows of MAX_COLS
                int rowCount = Mathf.CeilToInt((float)entries.Count / MAX_COLS);
                for (int row = 0; row < rowCount; row++)
                {
                    int startIdx = row * MAX_COLS;
                    int endIdx = Mathf.Min(startIdx + MAX_COLS, entries.Count);
                    int colsInRow = endIdx - startIdx;
                    float startX = -(colsInRow - 1) * hexSpacing * 0.5f;

                    for (int i = 0; i < colsInRow; i++)
                    {
                        var entry = entries[startIdx + i];
                        Vector3 hexCenter = new Vector3(startX + i * hexSpacing, 0, currentZ);

                        // Hex tile disc
                        CreateHexDisc(hexCenter, tileColor);

                        // Unit model
                        if (entry.modelPrefab != null)
                        {
                            var model = Object.Instantiate(entry.modelPrefab);
                            SceneManager.MoveGameObjectToScene(model, previewScene);
                            model.name = entry.unitName;
                            model.transform.position = hexCenter + new Vector3(0, entry.yOffset, 0);
                            model.transform.localScale = Vector3.one * entry.scale;
                            // Face towards camera (positive Z in scene view)
                            model.transform.rotation = Quaternion.Euler(0, 180f, 0);

                            // Strip all colliders so preview models don't intercept
                            // raycasts in the main scene (physics is global across scenes)
                            foreach (var col in model.GetComponentsInChildren<Collider>())
                                Object.DestroyImmediate(col);

                            previewModels[entry.unitName] = model;
                        }

                        previewPositions[entry.unitName] = hexCenter;
                    }

                    currentZ -= rowHeight;
                }

                currentZ -= tierGap;
            }

            FocusPreviewCamera();
        }

        private void ClosePreview()
        {
            if (previewOpen && previewScene.IsValid())
            {
                EditorSceneManager.CloseScene(previewScene, true);
            }
            previewOpen = false;
            previewModels.Clear();
            previewPositions.Clear();
        }

        private void CreateHexDisc(Vector3 center, Color color)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            SceneManager.MoveGameObjectToScene(disc, previewScene);
            disc.name = "HexTile";
            disc.transform.position = center;
            // Cylinder is 2 units tall, 1 unit diameter by default
            disc.transform.localScale = new Vector3(HEX_RADIUS * 2f, 0.02f, HEX_RADIUS * 2f);
            Object.DestroyImmediate(disc.GetComponent<Collider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            disc.GetComponent<Renderer>().material = mat;
        }

        private void UpdatePreviewModel(UnitModelDatabase.UnitModelEntry entry)
        {
            if (!previewOpen) return;
            if (!previewModels.TryGetValue(entry.unitName, out GameObject model)) return;
            if (model == null) return;

            model.transform.localScale = Vector3.one * entry.scale;
            if (previewPositions.TryGetValue(entry.unitName, out Vector3 hexCenter))
            {
                model.transform.position = hexCenter + new Vector3(0, entry.yOffset, 0);
            }

            // Force scene view to repaint for immediate visual feedback
            SceneView.RepaintAll();
        }

        private void RefreshAllPreviewModels()
        {
            if (!previewOpen) return;
            foreach (var entry in database.unitModels)
            {
                if (entry.modelPrefab == null) continue;
                UpdatePreviewModel(entry);
            }
        }

        private void FocusPreviewCamera()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;

            // Calculate center of all preview positions
            if (previewPositions.Count == 0) return;
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            foreach (var pos in previewPositions.Values)
            {
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
            }
            Vector3 center = (min + max) * 0.5f;
            float size = Mathf.Max(max.x - min.x, max.z - min.z) * 0.6f;

            sv.LookAt(center, Quaternion.Euler(45f, 0f, 0f), size);
            sv.Repaint();
        }

        /// <summary>
        /// Draw unit name labels above models in the scene view.
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (!previewOpen || previewPositions.Count == 0) return;

            Handles.BeginGUI();
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            foreach (var kvp in previewPositions)
            {
                string unitName = kvp.Key;
                Vector3 worldPos = kvp.Value + Vector3.up * 2.5f;
                Vector2 screenPos = HandleUtility.WorldToGUIPoint(worldPos);

                // Only draw if on screen
                if (screenPos.x < -100 || screenPos.x > sceneView.position.width + 100) continue;
                if (screenPos.y < -50 || screenPos.y > sceneView.position.height + 50) continue;

                // Get cost for color coding
                int cost = UnitCosts.ContainsKey(unitName) ? UnitCosts[unitName] : -1;
                Color labelCol = TierColors.ContainsKey(cost) ? TierColors[cost] : Color.gray;
                // Brighten for readability
                style.normal.textColor = Color.Lerp(labelCol, Color.white, 0.5f);

                Rect labelRect = new Rect(screenPos.x - 60, screenPos.y - 10, 120, 20);
                GUI.Label(labelRect, unitName, style);
            }

            Handles.EndGUI();
        }

        // ==================== AUTO-CALIBRATE ====================

        private void AutoCalibrateAll()
        {
            if (measurements == null) return;

            float hexDiameter = HEX_RADIUS * 2f;
            Undo.RecordObject(database, "Auto-calibrate all scales");

            int updated = 0;
            foreach (var entry in database.unitModels)
            {
                if (entry.modelPrefab == null) continue;
                if (!measurements.TryGetValue(entry.unitName, out Vector3 rawSize) || rawSize.y <= 0.001f) continue;

                float rawHeight = rawSize.y;
                float rawWidth = Mathf.Max(rawSize.x, rawSize.z);
                int cost = UnitCosts.ContainsKey(entry.unitName) ? UnitCosts[entry.unitName] : 2;

                float heightMult = HeightMultipliers.ContainsKey(cost) ? HeightMultipliers[cost] : 1.35f;
                float targetHeight = hexDiameter * heightMult;
                float heightScale = targetHeight / rawHeight;

                float maxWidth = HEX_WIDTH * MAX_WIDTH_MULT;
                float widthScale = maxWidth / rawWidth;

                float newScale = Mathf.Min(heightScale, widthScale);
                newScale = Mathf.Clamp(newScale, 0.05f, 5f);
                newScale = Mathf.Round(newScale * 100f) / 100f;

                entry.scale = newScale;
                updated++;
            }

            hasUnsavedChanges = true;
            Debug.Log($"[ScaleEditor] Auto-calibrated {updated} models");
        }

        private void SaveDatabase()
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            hasUnsavedChanges = false;
            Debug.Log("[ScaleEditor] Saved all changes");
        }

        // ==================== MEASUREMENT ====================

        private static Dictionary<string, Vector3> MeasureAllModels(UnitModelDatabase database)
        {
            var results = new Dictionary<string, Vector3>();
            var tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                foreach (var entry in database.unitModels)
                {
                    if (entry.modelPrefab == null) continue;

                    GameObject instance = Object.Instantiate(entry.modelPrefab);
                    SceneManager.MoveGameObjectToScene(instance, tempScene);
                    instance.transform.position = Vector3.zero;
                    instance.transform.localScale = Vector3.one;

                    Renderer[] allRenderers = instance.GetComponentsInChildren<Renderer>();
                    var meshRenderers = allRenderers.Where(r =>
                        r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();

                    if (meshRenderers.Length > 0)
                    {
                        Bounds bounds = meshRenderers[0].bounds;
                        for (int i = 1; i < meshRenderers.Length; i++)
                            bounds.Encapsulate(meshRenderers[i].bounds);
                        results[entry.unitName] = bounds.size;
                    }
                    else
                    {
                        results[entry.unitName] = Vector3.zero;
                    }

                    Object.DestroyImmediate(instance);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(tempScene, true);
            }

            return results;
        }

        private static UnitModelDatabase FindDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:UnitModelDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<UnitModelDatabase>(path);
            }
            return null;
        }
    }
}
