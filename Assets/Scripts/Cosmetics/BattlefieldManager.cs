using UnityEngine;
using System.Collections.Generic;
using Crestforge.Visuals;

namespace Crestforge.Cosmetics
{
    /// <summary>
    /// Manages battlefield visuals and theme swapping.
    /// Each player's board can have its own theme.
    /// </summary>
    public class BattlefieldManager : MonoBehaviour
    {
        public static BattlefieldManager Instance { get; private set; }

        [Header("Theme Selection")]
        [Tooltip("All available themes loaded from Resources.")]
        public List<BattlefieldTheme> availableThemes = new List<BattlefieldTheme>();

        [Tooltip("Index of the currently selected theme.")]
        public int currentThemeIndex = -1;

        [Header("Current Theme")]
        [Tooltip("The active battlefield theme. Can be swapped at runtime.")]
        public BattlefieldTheme currentTheme;

        [Header("Zone Positions")]
        [Tooltip("Base positions for each zone, relative to board center.")]
        public BattlefieldZonePositions zonePositions;

        [Header("References")]
        public HexBoard3D hexBoard;
        public Transform environmentContainer;

        // Spawned objects tracking
        private GameObject groundObject;
        private GameObject benchObject;
        private GameObject ambientParticles;
        private AudioSource ambientAudioSource;
        private List<GameObject> spawnedEnvironment = new List<GameObject>();

        // Cached material (more reliable than caching shader)
        private static Material _baseMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Clear cached material on domain reload / play mode start
            _baseMaterial = null;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (environmentContainer == null)
            {
                environmentContainer = new GameObject("BattlefieldEnvironment").transform;
                environmentContainer.SetParent(transform);
            }

            LoadAllThemes();
        }

        private void Start()
        {
            // If no theme set, use CastleCourtyard theme from Resources as default
            if (currentTheme == null)
            {
                // Try to find "CastleCourtyard" theme (exact match first, then fallback)
                var castleTheme = availableThemes.Find(t => t.themeName == "CastleCourtyard");
                if (castleTheme == null)
                {
                    castleTheme = availableThemes.Find(t => t.themeName.Contains("CastleCourtyard"));
                }

                if (castleTheme != null)
                {
                    ApplyTheme(castleTheme);
                }
                else if (availableThemes.Count > 0)
                {
                    ApplyTheme(availableThemes[0]);
                }
                else
                {
                    ApplyDefaultTheme();
                }
            }
            else
            {
                ApplyTheme(currentTheme);
            }
        }

        /// <summary>
        /// Load all BattlefieldTheme assets from Resources/ScriptableObjects/Themes/
        /// </summary>
        public void LoadAllThemes()
        {
            availableThemes.Clear();

            // Load all themes from Resources folder
            BattlefieldTheme[] themes = Resources.LoadAll<BattlefieldTheme>("ScriptableObjects/Themes");

            foreach (var theme in themes)
            {
                availableThemes.Add(theme);
                Debug.Log($"[BattlefieldManager] Loaded theme: {theme.themeName}");
            }

            Debug.Log($"[BattlefieldManager] Loaded {availableThemes.Count} theme(s) from Resources");

            // Find index of current theme if one is set
            if (currentTheme != null)
            {
                currentThemeIndex = availableThemes.IndexOf(currentTheme);
            }
        }

        private void Update()
        {
            // Debug keybinds for theme switching
            // F1 = Procedural Meadow
            // F2 = Procedural Castle
            // F3 = Clear environment
            // F4 = Previous custom theme
            // F5 = Next custom theme
            if (Input.GetKeyDown(KeyCode.F1))
            {
                currentThemeIndex = -1;
                ApplyDefaultTheme();
                Debug.Log("[BattlefieldManager] Switched to Procedural Meadow (F1)");
            }
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                currentThemeIndex = -1;
                ApplyCastleCourtyardTheme();
                Debug.Log("[BattlefieldManager] Switched to Procedural Castle (F2)");
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                ClearEnvironment();
                Debug.Log("[BattlefieldManager] Cleared all environment (F3)");
            }
            else if (Input.GetKeyDown(KeyCode.F4))
            {
                ApplyPreviousTheme();
            }
            else if (Input.GetKeyDown(KeyCode.F5))
            {
                ApplyNextTheme();
            }
        }

        /// <summary>
        /// Apply the next theme in the list
        /// </summary>
        public void ApplyNextTheme()
        {
            if (availableThemes.Count == 0)
            {
                Debug.LogWarning("[BattlefieldManager] No custom themes available. Add themes to Resources/ScriptableObjects/Themes/");
                return;
            }

            currentThemeIndex++;
            if (currentThemeIndex >= availableThemes.Count)
            {
                currentThemeIndex = 0;
            }

            ApplyThemeByIndex(currentThemeIndex);
        }

        /// <summary>
        /// Apply the previous theme in the list
        /// </summary>
        public void ApplyPreviousTheme()
        {
            if (availableThemes.Count == 0)
            {
                Debug.LogWarning("[BattlefieldManager] No custom themes available. Add themes to Resources/ScriptableObjects/Themes/");
                return;
            }

            currentThemeIndex--;
            if (currentThemeIndex < 0)
            {
                currentThemeIndex = availableThemes.Count - 1;
            }

            ApplyThemeByIndex(currentThemeIndex);
        }

        /// <summary>
        /// Apply a theme by its index in the available themes list
        /// </summary>
        public void ApplyThemeByIndex(int index)
        {
            if (index < 0 || index >= availableThemes.Count)
            {
                Debug.LogWarning($"[BattlefieldManager] Invalid theme index: {index}");
                return;
            }

            currentThemeIndex = index;
            BattlefieldTheme theme = availableThemes[index];
            ApplyTheme(theme);
            Debug.Log($"[BattlefieldManager] Applied theme: {theme.themeName} ({index + 1}/{availableThemes.Count}) (F4/F5 to cycle)");
        }

        /// <summary>
        /// Apply a theme by name
        /// </summary>
        public void ApplyThemeByName(string themeName)
        {
            for (int i = 0; i < availableThemes.Count; i++)
            {
                if (availableThemes[i].themeName == themeName || availableThemes[i].themeId == themeName)
                {
                    ApplyThemeByIndex(i);
                    return;
                }
            }

            Debug.LogWarning($"[BattlefieldManager] Theme not found: {themeName}");
        }

        /// <summary>
        /// Get a list of all available theme names
        /// </summary>
        public List<string> GetThemeNames()
        {
            List<string> names = new List<string>();
            foreach (var theme in availableThemes)
            {
                names.Add(theme.themeName);
            }
            return names;
        }

        /// <summary>
        /// Apply a battlefield theme, replacing all visuals
        /// </summary>
        public void ApplyTheme(BattlefieldTheme theme)
        {
            if (theme == null) return;

            currentTheme = theme;
            ClearEnvironment();

            // Apply theme to all boards in multi-board mode
            if (HexBoard3D.AllBoards != null && HexBoard3D.AllBoards.Count > 0)
            {
                foreach (var board in HexBoard3D.AllBoards)
                {
                    if (board != null)
                    {
                        ApplyThemeToBoard(theme, board.transform.position);
                    }
                }
            }
            else
            {
                // Fallback to single board
                if (hexBoard == null)
                    hexBoard = HexBoard3D.Instance;

                Vector3 boardCenter = hexBoard != null ? hexBoard.transform.position : Vector3.zero;
                ApplyThemeToBoard(theme, boardCenter);
            }

            // Audio is global, only set up once
            SetupAmbientAudio(theme);

            Debug.Log($"[BattlefieldManager] Applied theme: {theme.themeName}");
        }

        /// <summary>
        /// Apply theme visuals to a specific board position
        /// </summary>
        private void ApplyThemeToBoard(BattlefieldTheme theme, Vector3 boardCenter)
        {
            SpawnGround(theme, boardCenter);
            SpawnBench(theme, boardCenter);
            SpawnZonePlacements(theme, boardCenter);
            SpawnAmbientEffects(theme, boardCenter);
        }

        /// <summary>
        /// Apply the default procedural meadow theme
        /// </summary>
        public void ApplyDefaultTheme()
        {
            ClearEnvironment();

            if (HexBoard3D.AllBoards != null && HexBoard3D.AllBoards.Count > 0)
            {
                foreach (var board in HexBoard3D.AllBoards)
                {
                    if (board != null)
                    {
                        CreateProceduralMeadow(board.transform.position);
                    }
                }
            }
            else
            {
                Vector3 boardCenter = hexBoard != null ? hexBoard.transform.position : Vector3.zero;
                CreateProceduralMeadow(boardCenter);
            }

            Debug.Log("[BattlefieldManager] Applied default meadow theme");
        }

        /// <summary>
        /// Clear all spawned environment objects
        /// </summary>
        public void ClearEnvironment()
        {
            // Clear tracked references
            if (groundObject != null) SafeDestroy(groundObject);
            if (benchObject != null) SafeDestroy(benchObject);
            if (ambientParticles != null) SafeDestroy(ambientParticles);
            if (ambientAudioSource != null) SafeDestroy(ambientAudioSource.gameObject);

            foreach (var obj in spawnedEnvironment)
            {
                if (obj != null) SafeDestroy(obj);
            }
            spawnedEnvironment.Clear();

            // Also destroy ALL children of environmentContainer to catch any untracked objects
            if (environmentContainer != null)
            {
                // Iterate backwards to safely destroy while iterating
                for (int i = environmentContainer.childCount - 1; i >= 0; i--)
                {
                    SafeDestroy(environmentContainer.GetChild(i).gameObject);
                }
            }

            // Reset references
            groundObject = null;
            benchObject = null;
            ambientParticles = null;
            ambientAudioSource = null;
        }

        private void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        #region Spawning Methods

        private void SpawnGround(BattlefieldTheme theme, Vector3 boardCenter)
        {
            // Skip ground if theme specifies
            if (theme.skipGround) return;

            GameObject ground;
            if (theme.groundPrefab != null)
            {
                ground = Instantiate(theme.groundPrefab, environmentContainer);
                ground.name = "Ground";
                ground.transform.position = boardCenter + new Vector3(0, -0.05f, 0);
            }
            else
            {
                ground = CreateProceduralGround(theme.groundColor, theme.groundSize, theme.groundMaterial);
                ground.transform.position = boardCenter + new Vector3(0, -0.05f, 0);
            }

            // Track in list for proper cleanup in multi-board mode
            spawnedEnvironment.Add(ground);
            groundObject = ground;  // Keep reference for backwards compatibility
        }

        private void SpawnBench(BattlefieldTheme theme, Vector3 boardCenter)
        {
            // Skip bench if theme specifies
            if (theme.skipBench) return;

            Vector3 benchPos = GetZonePosition(BattlefieldZone.BenchArea, boardCenter);

            GameObject bench;
            if (theme.benchPrefab != null)
            {
                bench = Instantiate(theme.benchPrefab, environmentContainer);
                bench.name = "BenchVisual";
                bench.transform.position = benchPos + theme.benchOffset;
                bench.transform.localScale = Vector3.one * theme.benchScale;
            }
            else
            {
                bench = CreateProceduralBench();
                bench.transform.SetParent(environmentContainer);
                bench.transform.position = benchPos + theme.benchOffset;
            }

            // Track in list for proper cleanup in multi-board mode
            spawnedEnvironment.Add(bench);
            benchObject = bench;  // Keep reference for backwards compatibility
        }

        private void SpawnZonePlacements(BattlefieldTheme theme, Vector3 boardCenter)
        {
            foreach (var placement in theme.zonePlacements)
            {
                Vector3 zonePos = GetZonePosition(placement.zone, boardCenter);
                Vector3 finalPos = zonePos + placement.positionOffset;

                if (placement.positionVariance > 0)
                {
                    finalPos += new Vector3(
                        Random.Range(-placement.positionVariance, placement.positionVariance),
                        0,
                        Random.Range(-placement.positionVariance, placement.positionVariance)
                    );
                }

                Vector3 finalRotation = placement.rotation;
                if (placement.rotationVariance > 0)
                {
                    finalRotation.y += Random.Range(-placement.rotationVariance, placement.rotationVariance);
                }

                Vector3 finalScale = placement.scale;
                if (placement.scaleVariance > 0)
                {
                    float variance = Random.Range(-placement.scaleVariance, placement.scaleVariance);
                    finalScale *= (1f + variance);
                }

                GameObject obj;
                if (placement.prefab != null)
                {
                    obj = Instantiate(placement.prefab, environmentContainer);
                }
                else if (placement.objectType != ProceduralObjectType.None)
                {
                    obj = CreateProceduralObject(placement.objectType);
                    obj.transform.SetParent(environmentContainer);
                }
                else
                {
                    continue;
                }

                obj.name = $"Env_{placement.zone}_{placement.objectType}";
                obj.transform.position = finalPos;
                obj.transform.rotation = Quaternion.Euler(finalRotation);
                obj.transform.localScale = finalScale;

                spawnedEnvironment.Add(obj);
            }
        }

        private void SpawnAmbientEffects(BattlefieldTheme theme, Vector3 boardCenter)
        {
            if (theme.ambientParticlesPrefab != null)
            {
                ambientParticles = Instantiate(theme.ambientParticlesPrefab, environmentContainer);
                ambientParticles.name = "AmbientParticles";
                ambientParticles.transform.position = boardCenter + theme.ambientParticlesOffset;
            }
        }

        private void SetupAmbientAudio(BattlefieldTheme theme)
        {
            if (theme.ambientAudio != null)
            {
                GameObject audioObj = new GameObject("AmbientAudio");
                audioObj.transform.SetParent(environmentContainer);
                ambientAudioSource = audioObj.AddComponent<AudioSource>();
                ambientAudioSource.clip = theme.ambientAudio;
                ambientAudioSource.loop = true;
                ambientAudioSource.volume = theme.ambientVolume;
                ambientAudioSource.spatialBlend = 0f;
                ambientAudioSource.Play();
            }
        }

        #endregion

        #region Zone Positions

        public Vector3 GetZonePosition(BattlefieldZone zone, Vector3 boardCenter)
        {
            float halfWidth = 4f;
            float backZ = -4f;
            float frontZ = 6f;

            return zone switch
            {
                BattlefieldZone.Ground => boardCenter,
                BattlefieldZone.BackLeft => boardCenter + new Vector3(-halfWidth - 1f, 0, backZ),
                BattlefieldZone.BackRight => boardCenter + new Vector3(halfWidth + 1f, 0, backZ),
                BattlefieldZone.BackCenter => boardCenter + new Vector3(0, 0, backZ - 1f),
                BattlefieldZone.SideLeft => boardCenter + new Vector3(-halfWidth - 2f, 0, 1f),
                BattlefieldZone.SideRight => boardCenter + new Vector3(halfWidth + 2f, 0, 1f),
                BattlefieldZone.FrontLeft => boardCenter + new Vector3(-halfWidth - 1f, 0, frontZ),
                BattlefieldZone.FrontRight => boardCenter + new Vector3(halfWidth + 1f, 0, frontZ),
                BattlefieldZone.FrontCenter => boardCenter + new Vector3(0, 0, frontZ + 1f),
                BattlefieldZone.BenchArea => boardCenter + new Vector3(0, 0, backZ + 0.5f),
                BattlefieldZone.Surrounding => boardCenter,
                _ => boardCenter
            };
        }

        #endregion

        #region Procedural Generation

        private static Material GetBaseMaterial()
        {
            if (_baseMaterial != null)
                return _baseMaterial;

            // Create a primitive to get Unity's default working material for current render pipeline
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _baseMaterial = new Material(temp.GetComponent<Renderer>().sharedMaterial);

            if (Application.isPlaying)
                Destroy(temp);
            else
                DestroyImmediate(temp);

            Debug.Log($"[BattlefieldManager] Base material shader: {_baseMaterial.shader.name}");
            return _baseMaterial;
        }

        private Material CreateMaterial(Color color)
        {
            Material mat = new Material(GetBaseMaterial());

            // Set color using multiple property names to cover different shaders
            mat.color = color;

            // URP uses _BaseColor
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            // Standard uses _Color (already set via mat.color, but explicit is safer)
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            return mat;
        }

        private Material CreateEmissiveMaterial(Color baseColor, Color emissionColor)
        {
            Material mat = CreateMaterial(baseColor);

            // Enable emission - try both Standard and URP keywords
            mat.EnableKeyword("_EMISSION");
            mat.EnableKeyword("_EMISSIVE_COLOR_MAP");

            // Standard shader emission
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emissionColor);

            // URP emission
            if (mat.HasProperty("_EmissiveColor"))
                mat.SetColor("_EmissiveColor", emissionColor);

            return mat;
        }

        private GameObject CreateProceduralGround(Color color, Vector2 size, Material overrideMaterial = null)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(environmentContainer);
            ground.transform.localScale = new Vector3(size.x, 0.1f, size.y);
            SafeDestroy(ground.GetComponent<Collider>());

            var renderer = ground.GetComponent<Renderer>();
            renderer.material = overrideMaterial != null ? overrideMaterial : CreateMaterial(color);

            return ground;
        }

        private GameObject CreateProceduralBench()
        {
            GameObject bench = new GameObject("ProceduralBench");
            Color woodColor = new Color(0.45f, 0.3f, 0.15f);

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Top";
            top.transform.SetParent(bench.transform);
            top.transform.localPosition = new Vector3(0, 0.35f, 0);
            top.transform.localScale = new Vector3(3f, 0.08f, 0.5f);
            SafeDestroy(top.GetComponent<Collider>());
            top.GetComponent<Renderer>().material = CreateMaterial(woodColor);

            for (int i = 0; i < 2; i++)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Leg_{i}";
                leg.transform.SetParent(bench.transform);
                float xPos = (i == 0) ? -1.2f : 1.2f;
                leg.transform.localPosition = new Vector3(xPos, 0.15f, 0);
                leg.transform.localScale = new Vector3(0.1f, 0.3f, 0.4f);
                SafeDestroy(leg.GetComponent<Collider>());
                leg.GetComponent<Renderer>().material = CreateMaterial(woodColor * 0.8f);
            }

            return bench;
        }

        private GameObject CreateProceduralObject(ProceduralObjectType objectType)
        {
            return objectType switch
            {
                ProceduralObjectType.Tree => CreateProceduralTree(),
                ProceduralObjectType.Bush => CreateProceduralBush(),
                ProceduralObjectType.Rock => CreateProceduralRock(),
                ProceduralObjectType.Flower => CreateProceduralFlower(),
                ProceduralObjectType.Fence => CreateProceduralFence(),
                ProceduralObjectType.Torch => CreateProceduralTorch(),
                ProceduralObjectType.Pillar => CreateProceduralPillar(),
                ProceduralObjectType.Crate => CreateProceduralCrate(),
                ProceduralObjectType.Barrel => CreateProceduralBarrel(),
                _ => new GameObject("Unknown")
            };
        }

        private GameObject CreateProceduralTree()
        {
            GameObject tree = new GameObject("Tree");

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform);
            trunk.transform.localPosition = new Vector3(0, 0.6f, 0);
            trunk.transform.localScale = new Vector3(0.2f, 0.6f, 0.2f);
            SafeDestroy(trunk.GetComponent<Collider>());
            trunk.GetComponent<Renderer>().material = CreateMaterial(new Color(0.4f, 0.25f, 0.1f));

            Color leafColor = new Color(0.2f, 0.5f, 0.15f);
            Vector3[] foliagePositions = {
                new Vector3(0, 1.5f, 0),
                new Vector3(0.2f, 1.3f, 0.1f),
                new Vector3(-0.15f, 1.35f, -0.1f),
                new Vector3(0, 1.2f, 0.15f)
            };

            for (int i = 0; i < foliagePositions.Length; i++)
            {
                GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                foliage.name = $"Foliage_{i}";
                foliage.transform.SetParent(tree.transform);
                foliage.transform.localPosition = foliagePositions[i];
                float scale = 0.5f + Random.Range(-0.1f, 0.15f);
                foliage.transform.localScale = new Vector3(scale, scale * 0.8f, scale);
                SafeDestroy(foliage.GetComponent<Collider>());
                foliage.GetComponent<Renderer>().material = CreateMaterial(leafColor * Random.Range(0.9f, 1.1f));
            }

            return tree;
        }

        private GameObject CreateProceduralBush()
        {
            GameObject bush = new GameObject("Bush");
            Color bushColor = new Color(0.25f, 0.45f, 0.15f);

            for (int i = 0; i < 3; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Bush_{i}";
                sphere.transform.SetParent(bush.transform);
                float angle = i * 120f * Mathf.Deg2Rad;
                sphere.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.15f, 0.2f, Mathf.Sin(angle) * 0.15f);
                float scale = 0.3f + Random.Range(-0.05f, 0.1f);
                sphere.transform.localScale = new Vector3(scale, scale * 0.7f, scale);
                SafeDestroy(sphere.GetComponent<Collider>());
                sphere.GetComponent<Renderer>().material = CreateMaterial(bushColor * Random.Range(0.9f, 1.1f));
            }

            return bush;
        }

        private GameObject CreateProceduralRock()
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.localScale = new Vector3(
                0.4f + Random.Range(-0.1f, 0.2f),
                0.25f + Random.Range(-0.05f, 0.1f),
                0.35f + Random.Range(-0.1f, 0.15f)
            );
            rock.transform.localPosition = new Vector3(0, rock.transform.localScale.y * 0.4f, 0);
            SafeDestroy(rock.GetComponent<Collider>());

            Color rockColor = new Color(0.45f, 0.42f, 0.4f) * Random.Range(0.8f, 1.2f);
            rock.GetComponent<Renderer>().material = CreateMaterial(rockColor);

            return rock;
        }

        private GameObject CreateProceduralFlower()
        {
            GameObject flower = new GameObject("Flower");

            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(flower.transform);
            stem.transform.localPosition = new Vector3(0, 0.1f, 0);
            stem.transform.localScale = new Vector3(0.02f, 0.1f, 0.02f);
            SafeDestroy(stem.GetComponent<Collider>());
            stem.GetComponent<Renderer>().material = CreateMaterial(new Color(0.2f, 0.4f, 0.1f));

            Color[] petalColors = {
                new Color(1f, 0.3f, 0.3f),
                new Color(1f, 0.9f, 0.3f),
                new Color(0.9f, 0.4f, 0.9f),
                new Color(0.3f, 0.6f, 1f)
            };
            Color petalColor = petalColors[Random.Range(0, petalColors.Length)];

            GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            petal.name = "Bloom";
            petal.transform.SetParent(flower.transform);
            petal.transform.localPosition = new Vector3(0, 0.22f, 0);
            petal.transform.localScale = new Vector3(0.08f, 0.05f, 0.08f);
            SafeDestroy(petal.GetComponent<Collider>());
            petal.GetComponent<Renderer>().material = CreateMaterial(petalColor);

            return flower;
        }

        private GameObject CreateProceduralFence()
        {
            GameObject fence = new GameObject("Fence");
            Color woodColor = new Color(0.5f, 0.35f, 0.2f);

            for (int i = 0; i < 3; i++)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = $"Post_{i}";
                post.transform.SetParent(fence.transform);
                post.transform.localPosition = new Vector3((i - 1) * 0.5f, 0.25f, 0);
                post.transform.localScale = new Vector3(0.06f, 0.5f, 0.06f);
                SafeDestroy(post.GetComponent<Collider>());
                post.GetComponent<Renderer>().material = CreateMaterial(woodColor);
            }

            for (int i = 0; i < 2; i++)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"Rail_{i}";
                rail.transform.SetParent(fence.transform);
                rail.transform.localPosition = new Vector3(0, 0.15f + i * 0.2f, 0);
                rail.transform.localScale = new Vector3(1f, 0.04f, 0.04f);
                SafeDestroy(rail.GetComponent<Collider>());
                rail.GetComponent<Renderer>().material = CreateMaterial(woodColor * 0.9f);
            }

            return fence;
        }

        private GameObject CreateProceduralTorch()
        {
            GameObject torch = new GameObject("Torch");

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(torch.transform);
            pole.transform.localPosition = new Vector3(0, 0.4f, 0);
            pole.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);
            SafeDestroy(pole.GetComponent<Collider>());
            pole.GetComponent<Renderer>().material = CreateMaterial(new Color(0.35f, 0.25f, 0.15f));

            GameObject holder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            holder.name = "Holder";
            holder.transform.SetParent(torch.transform);
            holder.transform.localPosition = new Vector3(0, 0.8f, 0);
            holder.transform.localScale = new Vector3(0.1f, 0.05f, 0.1f);
            SafeDestroy(holder.GetComponent<Collider>());
            holder.GetComponent<Renderer>().material = CreateMaterial(new Color(0.3f, 0.3f, 0.3f));

            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Flame";
            flame.transform.SetParent(torch.transform);
            flame.transform.localPosition = new Vector3(0, 0.9f, 0);
            flame.transform.localScale = new Vector3(0.08f, 0.12f, 0.08f);
            SafeDestroy(flame.GetComponent<Collider>());

            Material flameMat = CreateEmissiveMaterial(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.5f, 0.1f) * 2f);
            flame.GetComponent<Renderer>().material = flameMat;

            GameObject lightObj = new GameObject("TorchLight");
            lightObj.transform.SetParent(torch.transform);
            lightObj.transform.localPosition = new Vector3(0, 0.9f, 0);
            Light torchLight = lightObj.AddComponent<Light>();
            torchLight.type = LightType.Point;
            torchLight.color = new Color(1f, 0.7f, 0.4f);
            torchLight.intensity = 0.8f;
            torchLight.range = 3f;

            return torch;
        }

        private GameObject CreateProceduralPillar()
        {
            GameObject pillar = new GameObject("Pillar");
            Color stoneColor = new Color(0.6f, 0.58f, 0.55f);

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Column";
            column.transform.SetParent(pillar.transform);
            column.transform.localPosition = new Vector3(0, 0.6f, 0);
            column.transform.localScale = new Vector3(0.25f, 0.6f, 0.25f);
            SafeDestroy(column.GetComponent<Collider>());
            column.GetComponent<Renderer>().material = CreateMaterial(stoneColor);

            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "Base";
            baseObj.transform.SetParent(pillar.transform);
            baseObj.transform.localPosition = new Vector3(0, 0.05f, 0);
            baseObj.transform.localScale = new Vector3(0.35f, 0.05f, 0.35f);
            SafeDestroy(baseObj.GetComponent<Collider>());
            baseObj.GetComponent<Renderer>().material = CreateMaterial(stoneColor * 0.9f);

            GameObject capital = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            capital.name = "Capital";
            capital.transform.SetParent(pillar.transform);
            capital.transform.localPosition = new Vector3(0, 1.2f, 0);
            capital.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);
            SafeDestroy(capital.GetComponent<Collider>());
            capital.GetComponent<Renderer>().material = CreateMaterial(stoneColor * 0.95f);

            return pillar;
        }

        private GameObject CreateProceduralCrate()
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Crate";
            crate.transform.localScale = new Vector3(0.4f, 0.35f, 0.4f);
            crate.transform.localPosition = new Vector3(0, 0.175f, 0);
            SafeDestroy(crate.GetComponent<Collider>());
            crate.GetComponent<Renderer>().material = CreateMaterial(new Color(0.55f, 0.4f, 0.25f));
            return crate;
        }

        private GameObject CreateProceduralBarrel()
        {
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            barrel.transform.localScale = new Vector3(0.3f, 0.35f, 0.3f);
            barrel.transform.localPosition = new Vector3(0, 0.35f, 0);
            SafeDestroy(barrel.GetComponent<Collider>());
            barrel.GetComponent<Renderer>().material = CreateMaterial(new Color(0.5f, 0.35f, 0.2f));
            return barrel;
        }

        /// <summary>
        /// Create a full procedural meadow environment for a single board
        /// </summary>
        private void CreateProceduralMeadow(Vector3 boardCenter)
        {
            var ground = CreateProceduralGround(new Color(0.35f, 0.5f, 0.2f), new Vector2(14f, 12f));
            ground.transform.position = boardCenter + new Vector3(0, -0.05f, 0);
            spawnedEnvironment.Add(ground);

            var bench = CreateProceduralBench();
            bench.transform.SetParent(environmentContainer);
            bench.transform.position = GetZonePosition(BattlefieldZone.BenchArea, boardCenter);
            spawnedEnvironment.Add(bench);

            var tree1 = CreateProceduralTree();
            tree1.transform.SetParent(environmentContainer);
            tree1.transform.position = GetZonePosition(BattlefieldZone.BackLeft, boardCenter);
            spawnedEnvironment.Add(tree1);

            var tree2 = CreateProceduralTree();
            tree2.transform.SetParent(environmentContainer);
            tree2.transform.position = GetZonePosition(BattlefieldZone.BackRight, boardCenter);
            spawnedEnvironment.Add(tree2);

            var bush1 = CreateProceduralBush();
            bush1.transform.SetParent(environmentContainer);
            bush1.transform.position = GetZonePosition(BattlefieldZone.SideLeft, boardCenter) + new Vector3(0, 0, -1f);
            spawnedEnvironment.Add(bush1);

            var bush2 = CreateProceduralBush();
            bush2.transform.SetParent(environmentContainer);
            bush2.transform.position = GetZonePosition(BattlefieldZone.SideRight, boardCenter) + new Vector3(0, 0, 1f);
            spawnedEnvironment.Add(bush2);

            var rock1 = CreateProceduralRock();
            rock1.transform.SetParent(environmentContainer);
            rock1.transform.position = GetZonePosition(BattlefieldZone.SideLeft, boardCenter) + new Vector3(0.5f, 0, 1f);
            spawnedEnvironment.Add(rock1);

            for (int i = 0; i < 8; i++)
            {
                var flower = CreateProceduralFlower();
                flower.transform.SetParent(environmentContainer);
                float angle = i * 45f * Mathf.Deg2Rad;
                float radius = 5f + Random.Range(-1f, 1f);
                flower.transform.position = boardCenter + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                flower.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
                spawnedEnvironment.Add(flower);
            }

            var rock2 = CreateProceduralRock();
            rock2.transform.SetParent(environmentContainer);
            rock2.transform.position = GetZonePosition(BattlefieldZone.FrontLeft, boardCenter) + new Vector3(-0.5f, 0, 0);
            rock2.transform.localScale *= 0.7f;
            spawnedEnvironment.Add(rock2);
        }

        /// <summary>
        /// Create a castle courtyard procedural environment for a single board
        /// </summary>
        private void CreateProceduralCastleCourtyardForBoard(Vector3 boardCenter)
        {
            var ground = CreateProceduralGround(new Color(0.45f, 0.42f, 0.38f), new Vector2(14f, 12f));
            ground.transform.position = boardCenter + new Vector3(0, -0.05f, 0);
            spawnedEnvironment.Add(ground);

            var bench = CreateProceduralStoneBench();
            bench.transform.SetParent(environmentContainer);
            bench.transform.position = GetZonePosition(BattlefieldZone.BenchArea, boardCenter);
            spawnedEnvironment.Add(bench);

            var pillar1 = CreateProceduralPillar();
            pillar1.transform.SetParent(environmentContainer);
            pillar1.transform.position = GetZonePosition(BattlefieldZone.BackLeft, boardCenter);
            pillar1.transform.localScale = Vector3.one * 1.2f;
            spawnedEnvironment.Add(pillar1);

            var torch1 = CreateProceduralTorch();
            torch1.transform.SetParent(environmentContainer);
            torch1.transform.position = GetZonePosition(BattlefieldZone.BackLeft, boardCenter) + new Vector3(0.5f, 0, 0.3f);
            spawnedEnvironment.Add(torch1);

            var pillar2 = CreateProceduralPillar();
            pillar2.transform.SetParent(environmentContainer);
            pillar2.transform.position = GetZonePosition(BattlefieldZone.BackRight, boardCenter);
            pillar2.transform.localScale = Vector3.one * 1.2f;
            spawnedEnvironment.Add(pillar2);

            var torch2 = CreateProceduralTorch();
            torch2.transform.SetParent(environmentContainer);
            torch2.transform.position = GetZonePosition(BattlefieldZone.BackRight, boardCenter) + new Vector3(-0.5f, 0, 0.3f);
            spawnedEnvironment.Add(torch2);

            var wall = CreateProceduralCastleWall();
            wall.transform.SetParent(environmentContainer);
            wall.transform.position = GetZonePosition(BattlefieldZone.BackCenter, boardCenter) + new Vector3(0, 0, -0.5f);
            spawnedEnvironment.Add(wall);

            var sidePillar1 = CreateProceduralPillar();
            sidePillar1.transform.SetParent(environmentContainer);
            sidePillar1.transform.position = GetZonePosition(BattlefieldZone.SideLeft, boardCenter) + new Vector3(0, 0, -1.5f);
            spawnedEnvironment.Add(sidePillar1);

            var sidePillar2 = CreateProceduralPillar();
            sidePillar2.transform.SetParent(environmentContainer);
            sidePillar2.transform.position = GetZonePosition(BattlefieldZone.SideLeft, boardCenter) + new Vector3(0, 0, 1.5f);
            spawnedEnvironment.Add(sidePillar2);

            var sidePillar3 = CreateProceduralPillar();
            sidePillar3.transform.SetParent(environmentContainer);
            sidePillar3.transform.position = GetZonePosition(BattlefieldZone.SideRight, boardCenter) + new Vector3(0, 0, -1.5f);
            spawnedEnvironment.Add(sidePillar3);

            var sidePillar4 = CreateProceduralPillar();
            sidePillar4.transform.SetParent(environmentContainer);
            sidePillar4.transform.position = GetZonePosition(BattlefieldZone.SideRight, boardCenter) + new Vector3(0, 0, 1.5f);
            spawnedEnvironment.Add(sidePillar4);

            var crate1 = CreateProceduralCrate();
            crate1.transform.SetParent(environmentContainer);
            crate1.transform.position = GetZonePosition(BattlefieldZone.BackLeft, boardCenter) + new Vector3(1.2f, 0, -0.5f);
            crate1.transform.rotation = Quaternion.Euler(0, 15f, 0);
            spawnedEnvironment.Add(crate1);

            var crate2 = CreateProceduralCrate();
            crate2.transform.SetParent(environmentContainer);
            crate2.transform.position = GetZonePosition(BattlefieldZone.BackLeft, boardCenter) + new Vector3(1.5f, 0, 0.1f);
            crate2.transform.rotation = Quaternion.Euler(0, -10f, 0);
            crate2.transform.localScale *= 0.85f;
            spawnedEnvironment.Add(crate2);

            var barrel1 = CreateProceduralBarrel();
            barrel1.transform.SetParent(environmentContainer);
            barrel1.transform.position = GetZonePosition(BattlefieldZone.BackRight, boardCenter) + new Vector3(-1.3f, 0, -0.3f);
            spawnedEnvironment.Add(barrel1);

            var barrel2 = CreateProceduralBarrel();
            barrel2.transform.SetParent(environmentContainer);
            barrel2.transform.position = GetZonePosition(BattlefieldZone.BackRight, boardCenter) + new Vector3(-1.0f, 0, 0.3f);
            barrel2.transform.localScale *= 0.9f;
            spawnedEnvironment.Add(barrel2);

            var frontTorch1 = CreateProceduralTorch();
            frontTorch1.transform.SetParent(environmentContainer);
            frontTorch1.transform.position = GetZonePosition(BattlefieldZone.FrontLeft, boardCenter) + new Vector3(0.5f, 0, -0.5f);
            spawnedEnvironment.Add(frontTorch1);

            var frontTorch2 = CreateProceduralTorch();
            frontTorch2.transform.SetParent(environmentContainer);
            frontTorch2.transform.position = GetZonePosition(BattlefieldZone.FrontRight, boardCenter) + new Vector3(-0.5f, 0, -0.5f);
            spawnedEnvironment.Add(frontTorch2);

            var flag1 = CreateProceduralBanner();
            flag1.transform.SetParent(environmentContainer);
            flag1.transform.position = GetZonePosition(BattlefieldZone.SideLeft, boardCenter) + new Vector3(0.3f, 0, 0);
            spawnedEnvironment.Add(flag1);

            var flag2 = CreateProceduralBanner();
            flag2.transform.SetParent(environmentContainer);
            flag2.transform.position = GetZonePosition(BattlefieldZone.SideRight, boardCenter) + new Vector3(-0.3f, 0, 0);
            spawnedEnvironment.Add(flag2);
        }

        private GameObject CreateProceduralStoneBench()
        {
            GameObject bench = new GameObject("StoneBench");
            Color stoneColor = new Color(0.55f, 0.52f, 0.48f);
            Color stoneDark = stoneColor * 0.85f;

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Top";
            top.transform.SetParent(bench.transform);
            top.transform.localPosition = new Vector3(0, 0.4f, 0);
            top.transform.localScale = new Vector3(3.2f, 0.12f, 0.6f);
            SafeDestroy(top.GetComponent<Collider>());
            top.GetComponent<Renderer>().material = CreateMaterial(stoneColor);

            for (int i = 0; i < 2; i++)
            {
                GameObject support = GameObject.CreatePrimitive(PrimitiveType.Cube);
                support.name = $"Support_{i}";
                support.transform.SetParent(bench.transform);
                float xPos = (i == 0) ? -1.1f : 1.1f;
                support.transform.localPosition = new Vector3(xPos, 0.17f, 0);
                support.transform.localScale = new Vector3(0.35f, 0.34f, 0.5f);
                SafeDestroy(support.GetComponent<Collider>());
                support.GetComponent<Renderer>().material = CreateMaterial(stoneDark);
            }

            return bench;
        }

        private GameObject CreateProceduralCastleWall()
        {
            GameObject wall = new GameObject("CastleWall");
            Color stoneColor = new Color(0.5f, 0.48f, 0.44f);
            Color stoneDark = stoneColor * 0.85f;

            GameObject mainWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mainWall.name = "MainWall";
            mainWall.transform.SetParent(wall.transform);
            mainWall.transform.localPosition = new Vector3(0, 0.8f, 0);
            mainWall.transform.localScale = new Vector3(4f, 1.6f, 0.4f);
            SafeDestroy(mainWall.GetComponent<Collider>());
            mainWall.GetComponent<Renderer>().material = CreateMaterial(stoneColor);

            float merlonSpacing = 0.7f;
            int merlonCount = 5;
            for (int i = 0; i < merlonCount; i++)
            {
                GameObject merlon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                merlon.name = $"Merlon_{i}";
                merlon.transform.SetParent(wall.transform);
                float xPos = (i - (merlonCount - 1) / 2f) * merlonSpacing;
                merlon.transform.localPosition = new Vector3(xPos, 1.75f, 0);
                merlon.transform.localScale = new Vector3(0.35f, 0.3f, 0.45f);
                SafeDestroy(merlon.GetComponent<Collider>());
                merlon.GetComponent<Renderer>().material = CreateMaterial(stoneDark);
            }

            GameObject archBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            archBase.name = "ArchBase";
            archBase.transform.SetParent(wall.transform);
            archBase.transform.localPosition = new Vector3(0, 0.15f, 0.1f);
            archBase.transform.localScale = new Vector3(1.2f, 0.3f, 0.25f);
            SafeDestroy(archBase.GetComponent<Collider>());
            archBase.GetComponent<Renderer>().material = CreateMaterial(stoneDark);

            return wall;
        }

        private GameObject CreateProceduralBanner()
        {
            GameObject banner = new GameObject("Banner");

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(banner.transform);
            pole.transform.localPosition = new Vector3(0, 1f, 0);
            pole.transform.localScale = new Vector3(0.06f, 1f, 0.06f);
            SafeDestroy(pole.GetComponent<Collider>());
            pole.GetComponent<Renderer>().material = CreateMaterial(new Color(0.35f, 0.25f, 0.15f));

            GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.name = "Cloth";
            cloth.transform.SetParent(banner.transform);
            cloth.transform.localPosition = new Vector3(0.2f, 1.5f, 0);
            cloth.transform.localScale = new Vector3(0.5f, 0.7f, 0.02f);
            cloth.transform.rotation = Quaternion.Euler(0, 0, -5f);
            SafeDestroy(cloth.GetComponent<Collider>());

            Color[] bannerColors = {
                new Color(0.7f, 0.15f, 0.15f),
                new Color(0.15f, 0.25f, 0.6f),
                new Color(0.6f, 0.5f, 0.1f),
                new Color(0.2f, 0.5f, 0.2f)
            };
            Color bannerColor = bannerColors[Random.Range(0, bannerColors.Length)];
            cloth.GetComponent<Renderer>().material = CreateMaterial(bannerColor);

            GameObject ornament = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ornament.name = "Ornament";
            ornament.transform.SetParent(banner.transform);
            ornament.transform.localPosition = new Vector3(0, 2.05f, 0);
            ornament.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            SafeDestroy(ornament.GetComponent<Collider>());
            ornament.GetComponent<Renderer>().material = CreateMaterial(new Color(0.75f, 0.65f, 0.2f));

            return banner;
        }

        /// <summary>
        /// Apply castle courtyard theme
        /// </summary>
        public void ApplyCastleCourtyardTheme()
        {
            ClearEnvironment();

            if (HexBoard3D.AllBoards != null && HexBoard3D.AllBoards.Count > 0)
            {
                foreach (var board in HexBoard3D.AllBoards)
                {
                    if (board != null)
                    {
                        CreateProceduralCastleCourtyardForBoard(board.transform.position);
                    }
                }
            }
            else
            {
                Vector3 boardCenter = hexBoard != null ? hexBoard.transform.position : Vector3.zero;
                CreateProceduralCastleCourtyardForBoard(boardCenter);
            }

            Debug.Log("[BattlefieldManager] Applied castle courtyard theme");
        }

        #endregion
    }

    [System.Serializable]
    public class BattlefieldZonePositions
    {
        public Vector3 backLeft = new Vector3(-5f, 0, -4f);
        public Vector3 backRight = new Vector3(5f, 0, -4f);
        public Vector3 backCenter = new Vector3(0, 0, -5f);
        public Vector3 sideLeft = new Vector3(-6f, 0, 1f);
        public Vector3 sideRight = new Vector3(6f, 0, 1f);
        public Vector3 frontLeft = new Vector3(-5f, 0, 6f);
        public Vector3 frontRight = new Vector3(5f, 0, 6f);
        public Vector3 frontCenter = new Vector3(0, 0, 7f);
        public Vector3 benchArea = new Vector3(0, 0, -3.5f);
    }
}
