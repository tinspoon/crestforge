#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Crestforge.Cosmetics;

namespace Crestforge.Editor
{
    public static class ThemeEditorMenu
    {
        [MenuItem("Crestforge/Open Game Scene", false, 0)]
        public static void OpenGameScene()
        {
            string scenePath = "Assets/Scenes/SampleScene.unity";

            if (System.IO.File.Exists(scenePath))
            {
                // Check for unsaved changes
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Scene Not Found",
                    $"Could not find game scene at:\n{scenePath}",
                    "OK");
            }
        }

        [MenuItem("Crestforge/Open Theme Editor Scene", false, 1)]
        public static void OpenThemeEditorSceneMenu()
        {
            OpenOrCreateThemeEditorScene();
        }

        /// <summary>
        /// Configure ambient lighting and skybox to match SampleScene
        /// </summary>
        private static void SetupLightingToMatchGame()
        {
            // Ambient lighting settings from SampleScene
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 2.4f;
            RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);

            // Use default procedural skybox (same as SampleScene)
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");

            // Reflection settings
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;

            // Shadow settings
            RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.478f, 0.627f, 1f);

            Debug.Log("[ThemeEditorMenu] Applied SampleScene lighting settings");
        }

        public static void OpenOrCreateThemeEditorScene()
        {
            string scenePath = "Assets/Scenes/ThemeEditor.unity";

            // Check if scene exists
            if (System.IO.File.Exists(scenePath))
            {
                // Check for unsaved changes before switching
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                }
            }
            else
            {
                // Create new scene
                if (EditorUtility.DisplayDialog("Create Theme Editor Scene",
                    "Theme Editor scene doesn't exist. Create it now?", "Create", "Cancel"))
                {
                    CreateThemeEditorScene();
                }
            }
        }

        [MenuItem("Crestforge/Create Theme Editor Scene")]
        public static void CreateThemeEditorScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Set up the scene
            SetupThemeEditorScene();

            // Save scene
            string scenePath = "Assets/Scenes/ThemeEditor.unity";

            // Ensure directory exists
            string directory = System.IO.Path.GetDirectoryName(scenePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"Theme Editor scene created at: {scenePath}");
        }

        private static void SetupThemeEditorScene()
        {
            // Set up ambient lighting to match SampleScene
            SetupLightingToMatchGame();

            // Find and configure camera
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 12f, -10f);
                camera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = 8f;
                camera.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
            }

            // Find or create directional light matching SampleScene
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
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
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            // Match SampleScene directional light settings
            dirLight.transform.position = new Vector3(2.34f, 2.93f, -1.49f);
            dirLight.transform.rotation = Quaternion.Euler(63.378f, 148.387f, 61.809f);
            dirLight.color = new Color(1f, 0.95686275f, 0.8392157f);
            dirLight.intensity = 1f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.525f;

            // Create Theme Editor Setup object
            GameObject editorSetup = new GameObject("ThemeEditorSetup");
            var setup = editorSetup.AddComponent<ThemeEditorSetup>();

            // Auto-generate preview (with null check)
            if (setup != null)
            {
                // Defer to next editor update to ensure component is fully initialized
                EditorApplication.delayCall += () =>
                {
                    if (setup != null)
                    {
                        setup.GeneratePreview();
                    }
                };
            }

            // Create Battlefield Manager for theme testing
            GameObject battlefieldManager = new GameObject("BattlefieldManager");
            battlefieldManager.AddComponent<Crestforge.Cosmetics.BattlefieldManager>();

            // Create a ground reference plane
            GameObject groundPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundPlane.name = "ReferenceGround";
            groundPlane.transform.position = new Vector3(0, -0.1f, 0);
            groundPlane.transform.localScale = new Vector3(3f, 1f, 3f);
            Object.DestroyImmediate(groundPlane.GetComponent<Collider>());

            var renderer = groundPlane.GetComponent<Renderer>();
            Material groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (groundMat.shader.name == "Hidden/InternalErrorShader")
                groundMat = new Material(Shader.Find("Standard"));
            groundMat.color = new Color(0.15f, 0.15f, 0.15f);
            renderer.material = groundMat;

            // Select the editor setup
            Selection.activeGameObject = editorSetup;

            Debug.Log("Theme Editor Scene setup complete!\n" +
                     "- Use the ThemeEditorSetup inspector to generate previews\n" +
                     "- Place your environment objects around the zone markers\n" +
                     "- Test themes using the Preview buttons");
        }

        [MenuItem("Crestforge/Theme Editor/Preview Meadow Theme")]
        public static void PreviewMeadowTheme()
        {
            var manager = Object.FindAnyObjectByType<Crestforge.Cosmetics.BattlefieldManager>();
            if (manager == null)
            {
                GameObject obj = new GameObject("BattlefieldManager");
                manager = obj.AddComponent<Crestforge.Cosmetics.BattlefieldManager>();
            }
            manager.ApplyDefaultTheme();
        }

        [MenuItem("Crestforge/Theme Editor/Preview Castle Theme")]
        public static void PreviewCastleTheme()
        {
            var manager = Object.FindAnyObjectByType<Crestforge.Cosmetics.BattlefieldManager>();
            if (manager == null)
            {
                GameObject obj = new GameObject("BattlefieldManager");
                manager = obj.AddComponent<Crestforge.Cosmetics.BattlefieldManager>();
            }
            manager.ApplyCastleCourtyardTheme();
        }

        [MenuItem("Crestforge/Theme Editor/Clear Theme")]
        public static void ClearTheme()
        {
            var manager = Object.FindAnyObjectByType<Crestforge.Cosmetics.BattlefieldManager>();
            if (manager != null)
            {
                manager.ClearEnvironment();
            }
        }

        [MenuItem("Crestforge/Theme Editor/Apply Game Lighting")]
        public static void ApplyGameLighting()
        {
            SetupLightingToMatchGame();

            // Find or create directional light
            Light dirLight = null;
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    dirLight = light;
                    break;
                }
            }

            // Create directional light if none exists
            if (dirLight == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                Debug.Log("[ThemeEditorMenu] Created new directional light");
            }

            // Apply settings matching SampleScene
            dirLight.transform.position = new Vector3(2.34f, 2.93f, -1.49f);
            dirLight.transform.rotation = Quaternion.Euler(63.378f, 148.387f, 61.809f);
            dirLight.color = new Color(1f, 0.95686275f, 0.8392157f);
            dirLight.intensity = 1f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.525f;

            // Configure main camera to show skybox
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                Debug.Log("[ThemeEditorMenu] Set camera clear flags to Skybox");
            }

            Debug.Log("[ThemeEditorMenu] Applied game lighting settings:\n" +
                     $"  - Ambient Mode: Skybox, Intensity: 2.4\n" +
                     $"  - Skybox: Default-Skybox\n" +
                     $"  - Directional Light: color=(1, 0.957, 0.839), intensity=1, soft shadows");

            // Mark scene as dirty so it can be saved
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            // Force lighting update
            DynamicGI.UpdateEnvironment();
        }
    }
}
#endif
