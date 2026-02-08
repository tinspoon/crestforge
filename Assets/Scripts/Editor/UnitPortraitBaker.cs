using UnityEngine;
using UnityEditor;
using System.IO;
using Crestforge.Visuals;

namespace Crestforge.Editor
{
    /// <summary>
    /// Editor tool to bake 3D unit model portraits to texture assets.
    /// Run via menu: Crestforge > Bake Unit Portraits
    /// </summary>
    public static class UnitPortraitBaker
    {
        // Settings
        private const int PORTRAIT_SIZE = 128;
        private const int PORTRAIT_LAYER = 31;
        private const float CAMERA_DISTANCE = 1.2f;
        private const float CAMERA_HEIGHT_OFFSET = 0.15f;
        private const float MODEL_SCALE = 1.5f;

        private const string OUTPUT_FOLDER = "Assets/Resources/UnitPortraits";

        [MenuItem("Crestforge/Bake Unit Portraits")]
        public static void BakeAllPortraits()
        {
            var database = LoadModelDatabase();
            if (database == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not find UnitModelDatabase asset in Resources folder.", "OK");
                return;
            }

            // Ensure output folder exists
            if (!Directory.Exists(OUTPUT_FOLDER))
            {
                Directory.CreateDirectory(OUTPUT_FOLDER);
                AssetDatabase.Refresh();
            }

            // Create render setup
            var renderSetup = CreateRenderSetup();
            int bakedCount = 0;
            int skippedCount = 0;

            try
            {
                EditorUtility.DisplayProgressBar("Baking Portraits", "Starting...", 0f);

                for (int i = 0; i < database.unitModels.Count; i++)
                {
                    var entry = database.unitModels[i];
                    if (string.IsNullOrEmpty(entry.unitName) || entry.modelPrefab == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    float progress = (float)i / database.unitModels.Count;
                    EditorUtility.DisplayProgressBar("Baking Portraits", $"Baking {entry.unitName}...", progress);

                    BakePortrait(entry, renderSetup);
                    bakedCount++;
                }

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Baked {bakedCount} portraits.\nSkipped {skippedCount} entries without models.\n\nPortraits saved to:\n{OUTPUT_FOLDER}",
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                CleanupRenderSetup(renderSetup);
            }
        }

        [MenuItem("Crestforge/Bake Single Portrait (Selected Prefab)")]
        public static void BakeSinglePortrait()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a unit model prefab in the Project window.", "OK");
                return;
            }

            // Check if it's a prefab
            if (PrefabUtility.GetPrefabAssetType(selected) == PrefabAssetType.NotAPrefab)
            {
                EditorUtility.DisplayDialog("Error", "Please select a prefab asset, not a scene object.", "OK");
                return;
            }

            var database = LoadModelDatabase();
            UnitModelDatabase.UnitModelEntry entry = null;

            // Find matching entry in database
            if (database != null)
            {
                foreach (var e in database.unitModels)
                {
                    if (e.modelPrefab == selected)
                    {
                        entry = e;
                        break;
                    }
                }
            }

            // Create a temporary entry if not found
            if (entry == null)
            {
                entry = new UnitModelDatabase.UnitModelEntry
                {
                    unitName = selected.name,
                    modelPrefab = selected,
                    scale = 1f,
                    yOffset = 0f,
                    rotationOffset = Vector3.zero
                };
            }

            // Ensure output folder exists
            if (!Directory.Exists(OUTPUT_FOLDER))
            {
                Directory.CreateDirectory(OUTPUT_FOLDER);
                AssetDatabase.Refresh();
            }

            var renderSetup = CreateRenderSetup();
            try
            {
                BakePortrait(entry, renderSetup);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete", $"Portrait saved to:\n{OUTPUT_FOLDER}/{entry.unitName}_portrait.png", "OK");
            }
            finally
            {
                CleanupRenderSetup(renderSetup);
            }
        }

        private static void BakePortrait(UnitModelDatabase.UnitModelEntry entry, RenderSetup setup)
        {
            // Instantiate model
            GameObject model = Object.Instantiate(entry.modelPrefab, setup.stage.transform);
            model.name = "BakeModel";
            model.transform.localPosition = new Vector3(0, entry.yOffset, 0);
            model.transform.localRotation = Quaternion.Euler(entry.rotationOffset);
            model.transform.localScale = Vector3.one * entry.scale * MODEL_SCALE;

            // Set layer recursively
            SetLayerRecursive(model, PORTRAIT_LAYER);

            // Disable animators
            foreach (var animator in model.GetComponentsInChildren<Animator>())
            {
                animator.enabled = false;
            }

            // Find head position
            Vector3 headPos = FindHeadPosition(model);

            // Position camera
            setup.camera.transform.position = headPos + new Vector3(0, CAMERA_HEIGHT_OFFSET, CAMERA_DISTANCE);
            setup.camera.transform.LookAt(headPos + Vector3.up * 0.1f);

            // Render
            setup.camera.targetTexture = setup.renderTexture;
            setup.camera.Render();
            setup.camera.targetTexture = null;

            // Read pixels
            RenderTexture.active = setup.renderTexture;
            Texture2D portrait = new Texture2D(PORTRAIT_SIZE, PORTRAIT_SIZE, TextureFormat.ARGB32, false);
            portrait.ReadPixels(new Rect(0, 0, PORTRAIT_SIZE, PORTRAIT_SIZE), 0, 0);
            portrait.Apply();
            RenderTexture.active = null;

            // Save to file
            string filename = SanitizeFilename(entry.unitName) + "_portrait.png";
            string path = Path.Combine(OUTPUT_FOLDER, filename);
            byte[] pngData = portrait.EncodeToPNG();
            File.WriteAllBytes(path, pngData);

            // Cleanup
            Object.DestroyImmediate(model);
            Object.DestroyImmediate(portrait);

            // Configure import settings for the saved texture
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.maxTextureSize = PORTRAIT_SIZE;
                importer.SaveAndReimport();
            }

            Debug.Log($"[PortraitBaker] Saved: {path}");
        }

        private static Vector3 FindHeadPosition(GameObject model)
        {
            // Try to find head bone
            string[] headBoneNames = { "Head", "head", "Bip001 Head", "mixamorig:Head" };
            foreach (string boneName in headBoneNames)
            {
                Transform headBone = FindChildRecursive(model.transform, boneName);
                if (headBone != null)
                    return headBone.position;
            }

            // Search for any transform with "head" in name
            foreach (var t in model.GetComponentsInChildren<Transform>())
            {
                if (t.name.ToLower().Contains("head"))
                    return t.position;
            }

            // Fall back to bounds estimation
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                float headY = bounds.center.y + bounds.extents.y * 0.6f;
                return new Vector3(bounds.center.x, headY, bounds.center.z);
            }

            return model.transform.position + Vector3.up * 0.5f;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static string SanitizeFilename(string name)
        {
            return name.ToLower().Replace(" ", "_").Replace("-", "_");
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private class RenderSetup
        {
            public GameObject stage;
            public Camera camera;
            public Light keyLight;
            public Light rimLight;
            public RenderTexture renderTexture;
        }

        private static RenderSetup CreateRenderSetup()
        {
            var setup = new RenderSetup();

            // Stage
            setup.stage = new GameObject("PortraitBakeStage");
            setup.stage.transform.position = new Vector3(1000, 1000, 1000);

            // Camera
            var camObj = new GameObject("BakeCamera");
            camObj.transform.SetParent(setup.stage.transform);
            setup.camera = camObj.AddComponent<Camera>();
            setup.camera.clearFlags = CameraClearFlags.SolidColor;
            setup.camera.backgroundColor = new Color(0.12f, 0.12f, 0.16f, 0f); // Match card background, transparent
            setup.camera.orthographic = false;
            setup.camera.fieldOfView = 30f;
            setup.camera.nearClipPlane = 0.1f;
            setup.camera.farClipPlane = 10f;
            setup.camera.cullingMask = 1 << PORTRAIT_LAYER;
            setup.camera.enabled = false;

            // Key light
            var keyObj = new GameObject("KeyLight");
            keyObj.transform.SetParent(setup.stage.transform);
            keyObj.transform.rotation = Quaternion.Euler(30f, -30f, 0f);
            setup.keyLight = keyObj.AddComponent<Light>();
            setup.keyLight.type = LightType.Directional;
            setup.keyLight.intensity = 1.2f;
            setup.keyLight.color = new Color(1f, 0.98f, 0.95f);
            setup.keyLight.cullingMask = 1 << PORTRAIT_LAYER;
            setup.keyLight.shadows = LightShadows.None;

            // Rim light
            var rimObj = new GameObject("RimLight");
            rimObj.transform.SetParent(setup.stage.transform);
            rimObj.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            setup.rimLight = rimObj.AddComponent<Light>();
            setup.rimLight.type = LightType.Directional;
            setup.rimLight.intensity = 0.6f;
            setup.rimLight.color = new Color(0.7f, 0.8f, 1f);
            setup.rimLight.cullingMask = 1 << PORTRAIT_LAYER;
            setup.rimLight.shadows = LightShadows.None;

            // Render texture
            setup.renderTexture = new RenderTexture(PORTRAIT_SIZE, PORTRAIT_SIZE, 24, RenderTextureFormat.ARGB32);
            setup.renderTexture.antiAliasing = 4;
            setup.renderTexture.Create();

            return setup;
        }

        private static void CleanupRenderSetup(RenderSetup setup)
        {
            if (setup.renderTexture != null)
            {
                setup.renderTexture.Release();
                Object.DestroyImmediate(setup.renderTexture);
            }
            if (setup.stage != null)
            {
                Object.DestroyImmediate(setup.stage);
            }
        }

        private static UnitModelDatabase LoadModelDatabase()
        {
            // Try Resources folder first
            var database = Resources.Load<UnitModelDatabase>("UnitModelDatabase");
            if (database != null)
                return database;

            // Search project
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
