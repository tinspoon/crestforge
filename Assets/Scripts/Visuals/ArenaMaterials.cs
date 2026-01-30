using UnityEngine;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Creates and provides materials for manual arena design with ProBuilder.
    /// Attach to a GameObject and click "Generate Materials" in the inspector.
    /// Materials will be saved to Assets/Materials/Arena/
    /// </summary>
    public class ArenaMaterials : MonoBehaviour
    {
        [Header("Generated Materials (Click Generate below)")]
        public Material grassLight;
        public Material grassDark;
        public Material stoneTan;
        public Material stoneDark;
        public Material woodBrown;
        public Material woodDark;
        public Material goldShiny;
        public Material bannerRed;
        public Material bannerBlue;
        public Material skyNight;
        public Material white;

        /// <summary>
        /// Call this to create all arena materials
        /// </summary>
        public void GenerateMaterials()
        {
            // Ensure folder exists
            #if UNITY_EDITOR
            string folderPath = "Assets/Materials/Arena";
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
            if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Materials", "Arena");
            #endif

            // Grass materials (bright, Merge Tactics style)
            grassLight = CreateAndSaveMaterial("Grass_Light",
                new Color(0.45f, 0.7f, 0.32f),
                new Color(0.35f, 0.55f, 0.25f));

            grassDark = CreateAndSaveMaterial("Grass_Dark",
                new Color(0.38f, 0.6f, 0.28f),
                new Color(0.28f, 0.48f, 0.2f));

            // Stone materials (tan castle walls)
            stoneTan = CreateAndSaveMaterial("Stone_Tan",
                new Color(0.78f, 0.7f, 0.55f),
                new Color(0.55f, 0.48f, 0.38f));

            stoneDark = CreateAndSaveMaterial("Stone_Dark",
                new Color(0.5f, 0.45f, 0.38f),
                new Color(0.35f, 0.3f, 0.25f));

            // Wood materials
            woodBrown = CreateAndSaveMaterial("Wood_Brown",
                new Color(0.6f, 0.45f, 0.28f),
                new Color(0.4f, 0.28f, 0.15f));

            woodDark = CreateAndSaveMaterial("Wood_Dark",
                new Color(0.4f, 0.3f, 0.2f),
                new Color(0.25f, 0.18f, 0.1f));

            // Gold material (shiny trim)
            goldShiny = CreateMetallicMaterial("Gold_Shiny",
                new Color(0.9f, 0.75f, 0.25f));

            // Banner materials
            bannerRed = CreateUnlitMaterial("Banner_Red",
                new Color(0.75f, 0.18f, 0.18f));

            bannerBlue = CreateUnlitMaterial("Banner_Blue",
                new Color(0.2f, 0.35f, 0.7f));

            // Sky material
            skyNight = CreateUnlitMaterial("Sky_Night",
                new Color(0.1f, 0.08f, 0.18f));

            // Utility
            white = CreateUnlitMaterial("White", Color.white);

            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log("Arena materials generated in Assets/Materials/Arena/");
            #endif
        }

        private Material CreateAndSaveMaterial(string name, Color mainColor, Color shadowColor)
        {
            Material mat;

            // Try to use toon shader
            Shader toonShader = Shader.Find("Crestforge/MedievalToon");
            if (toonShader != null)
            {
                mat = new Material(toonShader);
                mat.SetColor("_MainColor", mainColor);
                mat.SetColor("_ShadowColor", shadowColor);
                mat.SetFloat("_ShadowThreshold", 0.5f);
                mat.SetFloat("_ShadowSoftness", 0.05f);
                mat.SetColor("_RimColor", Color.Lerp(mainColor, Color.white, 0.3f));
                mat.SetFloat("_RimPower", 3f);
                mat.SetFloat("_RimIntensity", 0.3f);
                mat.SetFloat("_OutlineWidth", 0.005f);
                mat.SetColor("_OutlineColor", Color.Lerp(mainColor, Color.black, 0.6f));
            }
            else
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = mainColor;
                mat.SetFloat("_Glossiness", 0.3f);
            }

            mat.name = name;
            SaveMaterial(mat, name);
            return mat;
        }

        private Material CreateMetallicMaterial(string name, Color color)
        {
            Material mat;

            Shader toonShader = Shader.Find("Crestforge/MedievalToon");
            if (toonShader != null)
            {
                mat = new Material(toonShader);
                mat.SetColor("_MainColor", color);
                mat.SetColor("_ShadowColor", Color.Lerp(color, Color.black, 0.4f));
                mat.SetFloat("_SpecularIntensity", 0.6f);
                mat.SetFloat("_SpecularSize", 0.2f);
                mat.SetColor("_RimColor", Color.white);
                mat.SetFloat("_RimIntensity", 0.5f);
                mat.SetFloat("_OutlineWidth", 0.003f);
            }
            else
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                mat.SetFloat("_Glossiness", 0.8f);
                mat.SetFloat("_Metallic", 0.9f);
            }

            mat.name = name;
            SaveMaterial(mat, name);
            return mat;
        }

        private Material CreateUnlitMaterial(string name, Color color)
        {
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;
            mat.name = name;
            SaveMaterial(mat, name);
            return mat;
        }

        private void SaveMaterial(Material mat, string name)
        {
            #if UNITY_EDITOR
            string path = $"Assets/Materials/Arena/{name}.mat";

            // Check if material already exists
            Material existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Update existing material
                UnityEditor.EditorUtility.CopySerialized(mat, existing);
                UnityEditor.EditorUtility.SetDirty(existing);
            }
            else
            {
                // Create new asset
                UnityEditor.AssetDatabase.CreateAsset(mat, path);
            }
            #endif
        }
    }

    #if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(ArenaMaterials))]
    public class ArenaMaterialsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            UnityEditor.EditorGUILayout.Space(10);

            if (GUILayout.Button("Generate Materials", GUILayout.Height(30)))
            {
                ((ArenaMaterials)target).GenerateMaterials();
            }

            UnityEditor.EditorGUILayout.HelpBox(
                "Click 'Generate Materials' to create arena materials.\n\n" +
                "Materials will be saved to:\nAssets/Materials/Arena/\n\n" +
                "You can then drag these onto ProBuilder faces.",
                UnityEditor.MessageType.Info);
        }
    }
    #endif
}
