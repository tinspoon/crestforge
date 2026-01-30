using UnityEngine;
using Crestforge.Core;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Sets up the 3D isometric visual system for Crestforge.
    /// Attach this to an empty GameObject in the scene to initialize all 3D components.
    /// </summary>
    public class Game3DSetup : MonoBehaviour
    {
        [Header("Setup Options")]
        [Tooltip("Disable old 2D renderers automatically")]
        public bool disableOld2DSystem = true;
        
        [Tooltip("Auto-create lighting")]
        public bool createLighting = true;

        [Header("Visual Settings")]
        public Color ambientColor = new Color(0.3f, 0.3f, 0.35f);
        public Color directionalLightColor = new Color(1f, 0.95f, 0.9f);
        public float directionalLightIntensity = 1f;

        /// <summary>
        /// Call this from anywhere to ensure 3D system is initialized
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInitialize()
        {
            // Check if already exists
            if (FindObjectOfType<Game3DSetup>() != null) return;
            if (FindObjectOfType<HexBoard3D>() != null) return;
            
            // Auto-create the setup object
            GameObject setupObj = new GameObject("Game3DSetup");
            Game3DSetup setup = setupObj.AddComponent<Game3DSetup>();
            Debug.Log("[Game3DSetup] Auto-initialized 3D system");
        }

        private void Awake()
        {
            SetupScene();
        }

        /// <summary>
        /// Initialize the 3D scene
        /// </summary>
        public void SetupScene()
        {
            Debug.Log("[Game3DSetup] Initializing 3D visual system...");

            // Disable old 2D renderers
            if (disableOld2DSystem)
            {
                DisableOld2DComponents();
            }

            // Setup camera
            SetupCamera();

            // Setup lighting
            if (createLighting)
            {
                SetupLighting();
            }

            // Create board
            CreateHexBoard();

            // Create board manager
            CreateBoardManager();

            Debug.Log("[Game3DSetup] 3D visual system initialized!");
        }

        private void DisableOld2DComponents()
        {
            // Try to find and disable any old 2D rendering components
            // These are optional - the 3D system works independently
            
            // Look for any MonoBehaviour with these names to disable
            string[] componentsToDisable = new string[] {
                "HexGrid", "UnitRender", "DamageText", "FloatingText", 
                "CombatVisual", "BoardRender", "GridRender", "FloatingCombat",
                "CombatText", "DamagePopup", "TextPopup"
            };
            
            // Also look for GameObjects with these names to disable entirely
            string[] objectsToDisable = new string[] {
                "HexGrid", "UnitRenderer", "FloatingTextCanvas", "DamageCanvas",
                "CombatCanvas", "2DBoard", "BoardRenderer"
            };
            
            MonoBehaviour[] allComponents = FindObjectsOfType<MonoBehaviour>();
            foreach (var component in allComponents)
            {
                if (component == null) continue;
                
                string typeName = component.GetType().Name;
                string nameSpace = component.GetType().Namespace ?? "";
                
                // Skip our own 3D components
                if (nameSpace.Contains("Visuals")) continue;
                
                foreach (string searchName in componentsToDisable)
                {
                    if (typeName.Contains(searchName))
                    {
                        Debug.Log($"[Game3DSetup] Disabling old component: {typeName}");
                        component.enabled = false;
                        break;
                    }
                }
            }
            
            // Disable specific GameObjects
            foreach (string objName in objectsToDisable)
            {
                GameObject obj = GameObject.Find(objName);
                if (obj != null)
                {
                    Debug.Log($"[Game3DSetup] Disabling old GameObject: {objName}");
                    obj.SetActive(false);
                }
            }

            // Disable old camera controller if it exists (but not our IsometricCameraSetup)
            if (Camera.main != null)
            {
                MonoBehaviour[] cameraComponents = Camera.main.GetComponents<MonoBehaviour>();
                foreach (var component in cameraComponents)
                {
                    if (component == null) continue;
                    
                    string typeName = component.GetType().Name;
                    if (typeName.Contains("CameraController") && !(component is IsometricCameraSetup))
                    {
                        Debug.Log($"[Game3DSetup] Disabling old camera component: {typeName}");
                        component.enabled = false;
                    }
                }
            }
            
            // Find and disable any Canvas that might be rendering 2D board stuff
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            foreach (var canvas in allCanvases)
            {
                if (canvas == null) continue;
                string canvasName = canvas.gameObject.name.ToLower();
                if (canvasName.Contains("board") || canvasName.Contains("combat") || 
                    canvasName.Contains("floating") || canvasName.Contains("damage"))
                {
                    Debug.Log($"[Game3DSetup] Disabling canvas: {canvas.gameObject.name}");
                    canvas.gameObject.SetActive(false);
                }
            }
        }

        private void SetupCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
            }

            // Add isometric camera setup
            IsometricCameraSetup isoCam = mainCam.GetComponent<IsometricCameraSetup>();
            if (isoCam == null)
            {
                isoCam = mainCam.gameObject.AddComponent<IsometricCameraSetup>();
            }

            // Configure camera - view from behind player, zoomed out to see whole board
            isoCam.orthoSize = 8f;
            isoCam.rotationY = 0f;  // Straight ahead
            isoCam.rotationX = 45f; // Looking down at board
            isoCam.distance = 15f;
            isoCam.minOrthoSize = 4f;
            isoCam.maxOrthoSize = 15f;

            // Add audio listener if missing
            if (mainCam.GetComponent<AudioListener>() == null)
            {
                mainCam.gameObject.AddComponent<AudioListener>();
            }

            Debug.Log("[Game3DSetup] Camera configured");
        }

        private void SetupLighting()
        {
            // Find or create directional light
            Light[] lights = FindObjectsOfType<Light>();
            Light dirLight = null;
            
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    dirLight = light;
                    break;
                }
            }

            if (dirLight == null)
            {
                // Only create and configure lighting if none exists
                // This preserves scene-configured lighting settings
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                dirLight.color = directionalLightColor;
                dirLight.intensity = directionalLightIntensity;

                // Only set ambient lighting when creating from scratch
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = ambientColor;
            }

            // Always ensure shadows are enabled
            dirLight.shadows = LightShadows.Soft;

            Debug.Log("[Game3DSetup] Lighting configured");
        }

        private void CreateHexBoard()
        {
            HexBoard3D existingBoard = FindObjectOfType<HexBoard3D>();
            if (existingBoard != null)
            {
                Debug.Log("[Game3DSetup] HexBoard3D already exists");
                return;
            }

            GameObject boardObj = new GameObject("HexBoard3D");
            HexBoard3D board = boardObj.AddComponent<HexBoard3D>();
            
            // Configure board visuals
            board.hexRadius = 0.45f;
            board.hexHeight = 0.12f;
            board.hexSpacing = 0.08f;
            board.playerTileColor = new Color(0.25f, 0.4f, 0.55f);
            board.enemyTileColor = new Color(0.55f, 0.3f, 0.3f);

            Debug.Log("[Game3DSetup] HexBoard3D created");
        }

        private void CreateBoardManager()
        {
            BoardManager3D existingManager = FindObjectOfType<BoardManager3D>();
            if (existingManager != null)
            {
                Debug.Log("[Game3DSetup] BoardManager3D already exists");
            }
            else
            {
                GameObject managerObj = new GameObject("BoardManager3D");
                BoardManager3D manager = managerObj.AddComponent<BoardManager3D>();
                Debug.Log("[Game3DSetup] BoardManager3D created");
            }

            // Create VFX system
            VFXSystem existingVFX = FindObjectOfType<VFXSystem>();
            if (existingVFX == null)
            {
                GameObject vfxObj = new GameObject("VFXSystem");
                vfxObj.AddComponent<VFXSystem>();
                Debug.Log("[Game3DSetup] VFXSystem created");
            }

            // Create Projectile system
            ProjectileSystem existingProjectile = FindObjectOfType<ProjectileSystem>();
            if (existingProjectile == null)
            {
                GameObject projObj = new GameObject("ProjectileSystem");
                projObj.AddComponent<ProjectileSystem>();
                Debug.Log("[Game3DSetup] ProjectileSystem created");
            }

            // Create Audio system
            AudioManager existingAudio = FindObjectOfType<AudioManager>();
            if (existingAudio == null)
            {
                GameObject audioObj = new GameObject("AudioManager");
                audioObj.AddComponent<AudioManager>();
                Debug.Log("[Game3DSetup] AudioManager created");
            }
        }

        /// <summary>
        /// Reset and recreate the 3D scene
        /// </summary>
        [ContextMenu("Rebuild 3D Scene")]
        public void RebuildScene()
        {
            // Destroy existing 3D components
            var board = FindObjectOfType<HexBoard3D>();
            if (board != null) DestroyImmediate(board.gameObject);

            var manager = FindObjectOfType<BoardManager3D>();
            if (manager != null) DestroyImmediate(manager.gameObject);

            // Recreate
            SetupScene();
        }
    }
}