#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Crestforge.Editor
{
    /// <summary>
    /// Fixes pink materials by converting them to Standard shader.
    /// Use when assets were saved with URP but you're using Built-in pipeline.
    /// </summary>
    public class FixPinkMaterials : EditorWindow
    {
        [MenuItem("Crestforge/Fix Pink Materials")]
        public static void FixMaterials()
        {
            string folderPath = "Assets/EmaceArt";

            if (!Directory.Exists(folderPath))
            {
                EditorUtility.DisplayDialog("Folder Not Found",
                    "EmaceArt folder not found. Select materials manually.", "OK");
                return;
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });

            if (materialGuids.Length == 0)
            {
                EditorUtility.DisplayDialog("No Materials",
                    "No materials found in EmaceArt folder.", "OK");
                return;
            }

            Shader standardShader = Shader.Find("Standard");
            if (standardShader == null)
            {
                EditorUtility.DisplayDialog("Shader Not Found",
                    "Standard shader not found!", "OK");
                return;
            }

            int fixedCount = 0;

            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat != null && mat.shader != standardShader)
                {
                    // Store texture before changing shader
                    Texture mainTex = null;
                    Color color = Color.white;

                    if (mat.HasProperty("_MainTex"))
                        mainTex = mat.GetTexture("_MainTex");
                    else if (mat.HasProperty("_BaseMap"))
                        mainTex = mat.GetTexture("_BaseMap");

                    if (mat.HasProperty("_Color"))
                        color = mat.GetColor("_Color");
                    else if (mat.HasProperty("_BaseColor"))
                        color = mat.GetColor("_BaseColor");

                    // Change shader
                    mat.shader = standardShader;

                    // Restore texture and color
                    if (mainTex != null)
                        mat.SetTexture("_MainTex", mainTex);
                    mat.SetColor("_Color", color);

                    // Set to non-shiny by default
                    mat.SetFloat("_Glossiness", 0f);
                    mat.SetFloat("_Metallic", 0f);

                    EditorUtility.SetDirty(mat);
                    fixedCount++;
                }
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Materials Fixed",
                $"Fixed {fixedCount} materials in EmaceArt folder.\n\nChanged shader to Standard.", "OK");

            Debug.Log($"[FixPinkMaterials] Fixed {fixedCount} materials");
        }
    }
}
#endif
