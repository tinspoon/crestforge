using UnityEngine;
using System.Collections.Generic;
using Crestforge.Cosmetics;

namespace Crestforge.Cosmetics
{
    [System.Serializable]
    public class ThemeObjectPlacement
    {
        public GameObject sceneObject;  // The actual object in the scene
        public GameObject prefab;       // The prefab reference (auto-detected or created)
        public string objectName;       // For display purposes
        public BattlefieldZone zone;
        public Vector3 positionOffset;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
        public bool isPrefabInstance;   // Whether sceneObject is already a prefab instance
    }

    /// <summary>
    /// Editor tool for visually designing battlefield themes.
    /// Creates preview geometry for hex grid and bench slots.
    /// </summary>
    public class ThemeEditorSetup : MonoBehaviour
    {
        [Header("Preview Settings")]
        [Tooltip("Show hex grid preview")]
        public bool showHexPreview = true;
        [Tooltip("Show bench slot preview")]
        public bool showBenchPreview = true;
        [Tooltip("Show zone markers")]
        public bool showZoneMarkers = true;

        [Header("Grid Settings")]
        public int gridWidth = 5;
        public int gridHeight = 8;  // 4 player rows + 4 enemy rows
        public float hexRadius = 0.5f;

        [Header("Colors")]
        public Color playerHexColor = new Color(0.2f, 0.5f, 0.8f, 0.3f);   // Blue - player side
        public Color enemyHexColor = new Color(0.8f, 0.3f, 0.2f, 0.3f);   // Red/orange - enemy side
        public Color benchSlotColor = new Color(0.4f, 0.4f, 0.2f, 0.5f);
        public Color zoneMarkerColor = new Color(1f, 1f, 0f, 0.3f);

        [Header("Theme to Edit")]
        [Tooltip("Drag an existing theme here to load it for editing")]
        public BattlefieldTheme themeToEdit;

        [Header("Theme Building")]
        [Tooltip("Drag prefabs here to include in the saved theme")]
        public List<ThemeObjectPlacement> placedObjects = new List<ThemeObjectPlacement>();

        // Preview objects
        private GameObject hexPreviewContainer;
        private GameObject benchPreviewContainer;
        private GameObject zonePreviewContainer;
        private GameObject loadedThemeContainer;
        private GameObject groundPreviewObject;

        public void GeneratePreview()
        {
            ClearPreview();

            if (showHexPreview) CreateHexPreview();
            if (showBenchPreview) CreateBenchPreview();
            if (showZoneMarkers) CreateZoneMarkers();
        }

        public void ClearPreview()
        {
            DestroyPreviewContainer(ref hexPreviewContainer);
            DestroyPreviewContainer(ref benchPreviewContainer);
            DestroyPreviewContainer(ref zonePreviewContainer);
        }

        private void DestroyPreviewContainer(ref GameObject container)
        {
            if (container != null)
            {
                if (Application.isPlaying)
                    Destroy(container);
                else
                    DestroyImmediate(container);
                container = null;
            }
        }

        public void PreviewMeadowTheme()
        {
            var manager = FindAnyObjectByType<BattlefieldManager>();
            if (manager == null)
            {
                GameObject obj = new GameObject("BattlefieldManager");
                manager = obj.AddComponent<BattlefieldManager>();
            }
            manager.ApplyDefaultTheme();
        }

        public void PreviewCastleTheme()
        {
            var manager = FindAnyObjectByType<BattlefieldManager>();
            if (manager == null)
            {
                GameObject obj = new GameObject("BattlefieldManager");
                manager = obj.AddComponent<BattlefieldManager>();
            }
            manager.ApplyCastleCourtyardTheme();
        }

        public void ClearTheme()
        {
            var manager = FindAnyObjectByType<BattlefieldManager>();
            if (manager != null)
            {
                manager.ClearEnvironment();
            }
        }

        /// <summary>
        /// Load an existing theme into the scene for editing
        /// </summary>
        public void LoadThemeForEditing()
        {
            if (themeToEdit == null)
            {
                Debug.LogWarning("[ThemeEditorSetup] No theme assigned to 'Theme To Edit' field");
                return;
            }

            // Clear any existing loaded theme objects
            ClearLoadedTheme();
            ClearTheme();

            // Create container for loaded objects
            loadedThemeContainer = new GameObject("LoadedThemeObjects");
            loadedThemeContainer.transform.SetParent(transform);
            loadedThemeContainer.transform.localPosition = Vector3.zero;

            // Clear current placements
            placedObjects.Clear();

            int loadedCount = 0;

            // Instantiate each placement from the theme
            foreach (var placement in themeToEdit.zonePlacements)
            {
                if (placement.prefab == null)
                {
                    Debug.LogWarning($"[ThemeEditorSetup] Skipping placement in zone {placement.zone} - no prefab");
                    continue;
                }

                // Simple: position offset IS the world position (relative to center)
                Vector3 worldPos = placement.positionOffset;

                // Instantiate the prefab
                GameObject instance;
#if UNITY_EDITOR
                instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(placement.prefab, loadedThemeContainer.transform);
#else
                instance = Instantiate(placement.prefab, loadedThemeContainer.transform);
#endif

                instance.transform.position = worldPos;
                instance.transform.rotation = Quaternion.Euler(placement.rotation);
                instance.transform.localScale = placement.scale;

                // Track this placement
                var newPlacement = new ThemeObjectPlacement
                {
                    sceneObject = instance,
                    prefab = placement.prefab,
                    objectName = placement.prefab.name,
                    zone = placement.zone,
                    positionOffset = placement.positionOffset,
                    rotation = placement.rotation,
                    scale = placement.scale,
                    isPrefabInstance = true
                };
                placedObjects.Add(newPlacement);
                loadedCount++;
            }

            // Spawn ground preview if theme has ground enabled
            SpawnGroundPreview();

            Debug.Log($"[ThemeEditorSetup] Loaded {loadedCount} objects from theme '{themeToEdit.themeName}'");
        }

        /// <summary>
        /// Spawn a ground preview based on the theme's ground settings
        /// </summary>
        public void SpawnGroundPreview()
        {
            // Clear existing ground preview
            if (groundPreviewObject != null)
            {
                if (Application.isPlaying)
                    Destroy(groundPreviewObject);
                else
                    DestroyImmediate(groundPreviewObject);
                groundPreviewObject = null;
            }

            // Check if we have a theme and ground is enabled
            if (themeToEdit == null || themeToEdit.skipGround)
                return;

            // Create ground plane
            if (themeToEdit.groundPrefab != null)
            {
                // Use the prefab
#if UNITY_EDITOR
                groundPreviewObject = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(themeToEdit.groundPrefab);
#else
                groundPreviewObject = Instantiate(themeToEdit.groundPrefab);
#endif
            }
            else
            {
                // Create procedural ground with theme color
                groundPreviewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                groundPreviewObject.name = "GroundPreview";
                groundPreviewObject.transform.localScale = new Vector3(
                    themeToEdit.groundSize.x,
                    0.1f,
                    themeToEdit.groundSize.y
                );

                // Remove collider
                var collider = groundPreviewObject.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                        Destroy(collider);
                    else
                        DestroyImmediate(collider);
                }

                // Apply color
                var renderer = groundPreviewObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat;
                    if (themeToEdit.groundMaterial != null)
                    {
                        mat = new Material(themeToEdit.groundMaterial);
                    }
                    else
                    {
                        // Create material with ground color
                        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                        if (shader == null || shader.name == "Hidden/InternalErrorShader")
                            shader = Shader.Find("Standard");
                        mat = new Material(shader);
                        mat.color = themeToEdit.groundColor;
                        if (mat.HasProperty("_BaseColor"))
                            mat.SetColor("_BaseColor", themeToEdit.groundColor);
                    }
                    renderer.material = mat;
                }
            }

            groundPreviewObject.transform.position = new Vector3(0, -0.05f, 0);
            Debug.Log($"[ThemeEditorSetup] Spawned ground preview with color {themeToEdit.groundColor}");
        }

        /// <summary>
        /// Clear all objects loaded from a theme
        /// </summary>
        public void ClearLoadedTheme()
        {
            if (loadedThemeContainer != null)
            {
                if (Application.isPlaying)
                    Destroy(loadedThemeContainer);
                else
                    DestroyImmediate(loadedThemeContainer);
                loadedThemeContainer = null;
            }

            if (groundPreviewObject != null)
            {
                if (Application.isPlaying)
                    Destroy(groundPreviewObject);
                else
                    DestroyImmediate(groundPreviewObject);
                groundPreviewObject = null;
            }

            placedObjects.Clear();
        }

        /// <summary>
        /// Scan the scene for objects placed in theme zones and populate placedObjects list
        /// </summary>
        public void ScanSceneObjects()
        {
            placedObjects.Clear();

            // Find all root objects that aren't part of the preview system
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (var obj in allObjects)
            {
                // Skip non-root objects unless they're in allowed containers
                if (obj.transform.parent != null)
                {
                    string parentName = obj.transform.parent.name;

                    // Allow objects that are children of these containers
                    bool isInAllowedContainer =
                        parentName == "LoadedThemeObjects" ||  // Loaded theme objects
                        parentName == "ThemeEditorSetup";      // Direct children of setup

                    // Skip if parent has a MeshRenderer (means this is a child of a prefab, not a root prefab)
                    if (obj.transform.parent.GetComponent<MeshRenderer>() != null) continue;

                    // Skip nested objects unless in allowed container
                    if (!isInAllowedContainer && obj.transform.parent.parent != null) continue;
                }

                // Skip preview objects, UI, cameras, lights, and system objects
                if (obj.name.StartsWith("Hex_") || obj.name.StartsWith("Zone_") || obj.name.StartsWith("BenchSlot")) continue;
                if (obj.name == "ThemeEditorSetup" || obj.name == "BattlefieldManager") continue;
                if (obj.name == "Main Camera" || obj.name == "Directional Light") continue;
                if (obj.name == "HexPreview" || obj.name == "BenchPreview" || obj.name == "ZonePreview") continue;
                if (obj.name == "LoadedThemeObjects") continue;  // Skip the theme loader container
                if (obj.name == "ReferenceGround" || obj.name == "BattlefieldEnvironment") continue;
                if (obj.name == "GroundPreview") continue;  // Skip ground preview object
                if (obj.name == "EventSystem" || obj.name == "Canvas") continue;
                if (obj.GetComponent<Camera>() != null || obj.GetComponent<Light>() != null) continue;
                if (obj.GetComponent<ThemeEditorSetup>() != null) continue;
                if (obj.GetComponent<BattlefieldManager>() != null) continue;

                // Skip if no visual component
                if (obj.GetComponent<MeshRenderer>() == null && obj.GetComponentInChildren<MeshRenderer>() == null) continue;

                // Simple: store position relative to center (0,0,0)
                var placement = new ThemeObjectPlacement
                {
                    sceneObject = obj,
                    prefab = null,  // Will be set by editor if it's a prefab instance
                    objectName = obj.name,
                    zone = BattlefieldZone.Ground,  // Always use Ground (center) as reference
                    positionOffset = obj.transform.position,  // World position = offset from center
                    rotation = obj.transform.eulerAngles,
                    scale = obj.transform.lossyScale,  // Use world scale, not local
                    isPrefabInstance = false  // Will be set by editor
                };

                placedObjects.Add(placement);
                Debug.Log($"Found object '{obj.name}' at position {obj.transform.position}");
            }

            Debug.Log($"Scanned {placedObjects.Count} objects.");
        }

        private BattlefieldZone FindClosestZone(Vector3 position)
        {
            BattlefieldZone closest = BattlefieldZone.Ground;
            float closestDist = float.MaxValue;

            foreach (BattlefieldZone zone in System.Enum.GetValues(typeof(BattlefieldZone)))
            {
                Vector3 zonePos = GetZonePosition(zone);
                float dist = Vector3.Distance(position, zonePos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = zone;
                }
            }

            return closest;
        }

        /// <summary>
        /// Create a new BattlefieldTheme asset from current placements
        /// </summary>
        public BattlefieldTheme CreateThemeFromPlacements(string themeName)
        {
            BattlefieldTheme theme = ScriptableObject.CreateInstance<BattlefieldTheme>();
            theme.themeId = themeName.ToLower().Replace(" ", "_");
            theme.themeName = themeName;
            theme.description = "Custom theme created in Theme Editor";

            // Convert placements to theme zone placements
            foreach (var placement in placedObjects)
            {
                if (placement.prefab == null)
                {
                    Debug.LogWarning($"Skipping placement in zone {placement.zone} - no prefab assigned");
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

        private void CreateHexPreview()
        {
            hexPreviewContainer = new GameObject("HexPreview");
            hexPreviewContainer.transform.SetParent(transform);
            hexPreviewContainer.transform.localPosition = Vector3.zero;

            // Pointy-top hex dimensions (matching HexBoard3D exactly)
            float hexWidth = hexRadius * 1.732f;  // sqrt(3) * radius - horizontal spacing
            float hexHeight = hexRadius * 2f;    // point to point height
            float rowSpacing = hexRadius * 1.5f;  // Vertical distance between row centers

            // Calculate grid offset to center it (matching HexBoard3D.GenerateBoard)
            // Account for hex offset pattern: odd rows are shifted by hexWidth/2
            float totalWidth = gridWidth * hexWidth;
            float totalHeight = (gridHeight - 1) * rowSpacing + hexHeight;
            float startX = -totalWidth / 2f + hexWidth / 4f;
            float startZ = -totalHeight / 2f + hexHeight / 2f;

            for (int row = 0; row < gridHeight; row++)
            {
                for (int col = 0; col < gridWidth; col++)
                {
                    // Odd ROWS are offset by half hex width (pointy-top offset coordinates)
                    float xOffset = (row % 2 == 1) ? hexWidth * 0.5f : 0;
                    float x = startX + col * hexWidth + xOffset;
                    float z = startZ + row * rowSpacing;

                    bool isPlayerSide = row < 4;
                    Color hexColor = isPlayerSide ? playerHexColor : enemyHexColor;

                    CreateHexTile(hexPreviewContainer.transform, new Vector3(x, 0.01f, z), hexColor, $"Hex_{col}_{row}");
                }
            }
        }

        private void CreateHexTile(Transform parent, Vector3 position, Color color, string name)
        {
            GameObject hex = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hex.name = name;
            hex.transform.SetParent(parent);
            hex.transform.position = position;
            hex.transform.localScale = new Vector3(hexRadius * 2f, 0.02f, hexRadius * 2f);

            if (Application.isPlaying)
                Destroy(hex.GetComponent<Collider>());
            else
                DestroyImmediate(hex.GetComponent<Collider>());

            var renderer = hex.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Standard"));

            mat.color = color;
            SetMaterialTransparent(mat);
            renderer.material = mat;
        }

        private void CreateBenchPreview()
        {
            benchPreviewContainer = new GameObject("BenchPreview");
            benchPreviewContainer.transform.SetParent(transform);
            benchPreviewContainer.transform.localPosition = Vector3.zero;

            int benchSize = 7;  // GameConstants.Player.BENCH_SIZE
            float slotSpacing = 0.8f;
            float totalBenchWidth = (benchSize - 1) * slotSpacing;
            float benchStartX = -totalBenchWidth / 2f;

            // Match HexBoard3D positioning exactly
            float hexHeight = hexRadius * 2f;
            float rowSpacing = hexRadius * 1.5f;
            float totalGridHeight = (gridHeight - 1) * rowSpacing + hexHeight;
            float row0Z = -totalGridHeight / 2f + hexHeight / 2f;  // Z position of row 0
            float benchZ = row0Z - 1.5f;  // Bench is 1.5 units behind row 0

            for (int i = 0; i < benchSize; i++)
            {
                float x = benchStartX + i * slotSpacing;
                CreateBenchSlot(benchPreviewContainer.transform, new Vector3(x, 0.01f, benchZ), $"BenchSlot_{i}");
            }
        }

        private void CreateBenchSlot(Transform parent, Vector3 position, string name)
        {
            GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slot.name = name;
            slot.transform.SetParent(parent);
            slot.transform.position = position;
            slot.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);

            if (Application.isPlaying)
                Destroy(slot.GetComponent<Collider>());
            else
                DestroyImmediate(slot.GetComponent<Collider>());

            var renderer = slot.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Standard"));

            mat.color = benchSlotColor;
            SetMaterialTransparent(mat);
            renderer.material = mat;
        }

        private void CreateZoneMarkers()
        {
            zonePreviewContainer = new GameObject("ZonePreview");
            zonePreviewContainer.transform.SetParent(transform);
            zonePreviewContainer.transform.localPosition = Vector3.zero;

            CreateZoneMarker(BattlefieldZone.BackLeft, "BackLeft");
            CreateZoneMarker(BattlefieldZone.BackRight, "BackRight");
            CreateZoneMarker(BattlefieldZone.BackCenter, "BackCenter");
            CreateZoneMarker(BattlefieldZone.SideLeft, "SideLeft");
            CreateZoneMarker(BattlefieldZone.SideRight, "SideRight");
            CreateZoneMarker(BattlefieldZone.FrontLeft, "FrontLeft");
            CreateZoneMarker(BattlefieldZone.FrontRight, "FrontRight");
            CreateZoneMarker(BattlefieldZone.FrontCenter, "FrontCenter");
            CreateZoneMarker(BattlefieldZone.BenchArea, "BenchArea");
        }

        private void CreateZoneMarker(BattlefieldZone zone, string name)
        {
            Vector3 position = GetZonePosition(zone);

            GameObject marker = new GameObject($"Zone_{name}");
            marker.transform.SetParent(zonePreviewContainer.transform);
            marker.transform.position = position;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Marker";
            sphere.transform.SetParent(marker.transform);
            sphere.transform.localPosition = new Vector3(0, 0.5f, 0);
            sphere.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            if (Application.isPlaying)
                Destroy(sphere.GetComponent<Collider>());
            else
                DestroyImmediate(sphere.GetComponent<Collider>());

            var renderer = sphere.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Standard"));

            mat.color = zoneMarkerColor;
            SetMaterialTransparent(mat);
            renderer.material = mat;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(marker.transform);
            labelObj.transform.localPosition = new Vector3(0, 1.2f, 0);

            var textMesh = labelObj.AddComponent<TextMesh>();
            textMesh.text = name;
            textMesh.fontSize = 24;
            textMesh.characterSize = 0.1f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.yellow;
        }

        public Vector3 GetZonePosition(BattlefieldZone zone)
        {
            // Match HexBoard3D positioning exactly
            float hexWidth = hexRadius * 1.732f;
            float hexHeight = hexRadius * 2f;
            float rowSpacing = hexRadius * 1.5f;
            float totalHeight = (gridHeight - 1) * rowSpacing + hexHeight;

            float row0Z = -totalHeight / 2f + hexHeight / 2f;      // Z position of row 0 (player front)
            float row7Z = row0Z + 7 * rowSpacing;                   // Z position of row 7 (enemy front)
            float halfWidth = (gridWidth * hexWidth) / 2f;

            float backZ = row0Z - 2f;     // Behind player side
            float frontZ = row7Z + 2f;    // In front of enemy side

            return zone switch
            {
                BattlefieldZone.Ground => Vector3.zero,
                BattlefieldZone.BackLeft => new Vector3(-halfWidth - 1f, 0, backZ),
                BattlefieldZone.BackRight => new Vector3(halfWidth + 1f, 0, backZ),
                BattlefieldZone.BackCenter => new Vector3(0, 0, backZ - 1f),
                BattlefieldZone.SideLeft => new Vector3(-halfWidth - 2f, 0, 0f),
                BattlefieldZone.SideRight => new Vector3(halfWidth + 2f, 0, 0f),
                BattlefieldZone.FrontLeft => new Vector3(-halfWidth - 1f, 0, frontZ),
                BattlefieldZone.FrontRight => new Vector3(halfWidth + 1f, 0, frontZ),
                BattlefieldZone.FrontCenter => new Vector3(0, 0, frontZ + 1f),
                BattlefieldZone.BenchArea => new Vector3(0, 0, row0Z - 1.5f),
                BattlefieldZone.Surrounding => Vector3.zero,
                _ => Vector3.zero
            };
        }

        private void SetMaterialTransparent(Material mat)
        {
            if (mat == null) return;

            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        private void OnDrawGizmos()
        {
            if (!showZoneMarkers) return;

            Gizmos.color = Color.yellow;

            foreach (BattlefieldZone zone in System.Enum.GetValues(typeof(BattlefieldZone)))
            {
                if (zone == BattlefieldZone.Ground || zone == BattlefieldZone.Surrounding) continue;

                Vector3 pos = transform.position + GetZonePosition(zone);
                Gizmos.DrawWireSphere(pos + Vector3.up * 0.5f, 0.3f);
            }
        }
    }
}
