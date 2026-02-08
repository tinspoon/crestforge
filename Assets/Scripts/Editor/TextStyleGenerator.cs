#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using Crestforge.UI;
using System.IO;

namespace Crestforge.Editor
{
    /// <summary>
    /// Editor utility to generate default text styles for the game
    /// </summary>
    public class TextStyleGenerator : EditorWindow
    {
        private TMP_FontAsset primaryFont;

        [MenuItem("Crestforge/UI/Generate Text Styles")]
        public static void ShowWindow()
        {
            GetWindow<TextStyleGenerator>("Text Style Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Text Style Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This will create a set of mobile-game styled TextStyle assets.\n\n" +
                "1. Select a font (or leave empty for TMP default)\n" +
                "2. Click 'Generate All Styles'\n" +
                "3. Styles will be created in Resources/TextStyles/",
                MessageType.Info);

            EditorGUILayout.Space(10);

            primaryFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                "Primary Font", primaryFont, typeof(TMP_FontAsset), false);

            EditorGUILayout.Space(20);

            if (GUILayout.Button("Generate All Styles", GUILayout.Height(40)))
            {
                GenerateAllStyles();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Generate Presets Asset Only", GUILayout.Height(30)))
            {
                GeneratePresetsAsset();
            }
        }

        private void GenerateAllStyles()
        {
            // Ensure directories exist
            string stylesPath = "Assets/Resources/TextStyles";
            if (!Directory.Exists(stylesPath))
            {
                Directory.CreateDirectory(stylesPath);
            }

            // Mobile game color palette
            Color goldColor = new Color(1f, 0.84f, 0f);           // Gold
            Color damageColor = new Color(1f, 0.3f, 0.2f);        // Red
            Color healColor = new Color(0.3f, 1f, 0.4f);          // Green
            Color critColor = new Color(1f, 0.5f, 0f);            // Orange
            Color xpColor = new Color(0.6f, 0.4f, 1f);            // Purple
            Color buttonColor = Color.white;
            Color shadowColor = new Color(0, 0, 0, 0.7f);

            // Title - Big, bold, with outline
            CreateStyle(stylesPath, "Title", style => {
                style.font = primaryFont;
                style.fontSize = 48f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = new Color(0.1f, 0.1f, 0.2f);
                style.outlineWidth = 0.2f;
                style.useShadow = true;
                style.shadowColor = shadowColor;
                style.shadowOffset = new Vector2(2f, -2f);
                style.alignment = TextAlignmentOptions.Center;
            });

            // Header - Medium bold
            CreateStyle(stylesPath, "Header", style => {
                style.font = primaryFont;
                style.fontSize = 32f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = new Color(0, 0, 0, 0.8f);
                style.outlineWidth = 0.15f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Body - Clean readable text
            CreateStyle(stylesPath, "Body", style => {
                style.font = primaryFont;
                style.fontSize = 20f;
                style.fontStyle = FontStyles.Normal;
                style.color = Color.white;
                style.alignment = TextAlignmentOptions.Left;
            });

            // Button - Bold with slight shadow
            CreateStyle(stylesPath, "Button", style => {
                style.font = primaryFont;
                style.fontSize = 24f;
                style.fontStyle = FontStyles.Bold;
                style.color = buttonColor;
                style.useShadow = true;
                style.shadowColor = shadowColor;
                style.shadowOffset = new Vector2(1f, -1f);
                style.shadowSoftness = 0.3f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Button Small
            CreateStyle(stylesPath, "ButtonSmall", style => {
                style.font = primaryFont;
                style.fontSize = 18f;
                style.fontStyle = FontStyles.Bold;
                style.color = buttonColor;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Damage Number - Red with strong outline
            CreateStyle(stylesPath, "DamageNumber", style => {
                style.font = primaryFont;
                style.fontSize = 36f;
                style.fontStyle = FontStyles.Bold;
                style.color = damageColor;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.25f;
                style.useShadow = true;
                style.shadowColor = shadowColor;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Heal Number - Green
            CreateStyle(stylesPath, "HealNumber", style => {
                style.font = primaryFont;
                style.fontSize = 32f;
                style.fontStyle = FontStyles.Bold;
                style.color = healColor;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.2f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Critical Hit - Large orange
            CreateStyle(stylesPath, "CriticalHit", style => {
                style.font = primaryFont;
                style.fontSize = 44f;
                style.fontStyle = FontStyles.Bold;
                style.color = critColor;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.3f;
                style.useShadow = true;
                style.shadowColor = shadowColor;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Unit Name
            CreateStyle(stylesPath, "UnitName", style => {
                style.font = primaryFont;
                style.fontSize = 16f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.15f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Health Bar text
            CreateStyle(stylesPath, "HealthBar", style => {
                style.font = primaryFont;
                style.fontSize = 14f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.2f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Gold
            CreateStyle(stylesPath, "Gold", style => {
                style.font = primaryFont;
                style.fontSize = 22f;
                style.fontStyle = FontStyles.Bold;
                style.color = goldColor;
                style.useOutline = true;
                style.outlineColor = new Color(0.4f, 0.3f, 0f);
                style.outlineWidth = 0.1f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // XP
            CreateStyle(stylesPath, "XP", style => {
                style.font = primaryFont;
                style.fontSize = 20f;
                style.fontStyle = FontStyles.Bold;
                style.color = xpColor;
                style.useOutline = true;
                style.outlineColor = new Color(0.2f, 0.1f, 0.4f);
                style.outlineWidth = 0.1f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Level
            CreateStyle(stylesPath, "Level", style => {
                style.font = primaryFont;
                style.fontSize = 18f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.15f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Card Name
            CreateStyle(stylesPath, "CardName", style => {
                style.font = primaryFont;
                style.fontSize = 16f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.12f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Card Cost
            CreateStyle(stylesPath, "CardCost", style => {
                style.font = primaryFont;
                style.fontSize = 24f;
                style.fontStyle = FontStyles.Bold;
                style.color = goldColor;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.2f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Card Stats
            CreateStyle(stylesPath, "CardStats", style => {
                style.font = primaryFont;
                style.fontSize = 14f;
                style.fontStyle = FontStyles.Normal;
                style.color = Color.white;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Trait Name
            CreateStyle(stylesPath, "TraitName", style => {
                style.font = primaryFont;
                style.fontSize = 14f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.alignment = TextAlignmentOptions.Left;
            });

            // Timer
            CreateStyle(stylesPath, "Timer", style => {
                style.font = primaryFont;
                style.fontSize = 28f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.15f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Round Number
            CreateStyle(stylesPath, "RoundNumber", style => {
                style.font = primaryFont;
                style.fontSize = 24f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.12f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Player Name
            CreateStyle(stylesPath, "PlayerName", style => {
                style.font = primaryFont;
                style.fontSize = 18f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.1f;
                style.alignment = TextAlignmentOptions.Center;
            });

            // Notification
            CreateStyle(stylesPath, "Notification", style => {
                style.font = primaryFont;
                style.fontSize = 22f;
                style.fontStyle = FontStyles.Bold;
                style.color = Color.white;
                style.useOutline = true;
                style.outlineColor = Color.black;
                style.outlineWidth = 0.15f;
                style.useShadow = true;
                style.shadowColor = shadowColor;
                style.alignment = TextAlignmentOptions.Center;
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GeneratePresetsAsset();

            EditorUtility.DisplayDialog("Success",
                "Text styles generated in:\nAssets/Resources/TextStyles/\n\n" +
                "A TextStylePresets asset was also created.\n" +
                "Add TextStyleManager to your scene to use the styles.",
                "OK");
        }

        private void CreateStyle(string path, string name, System.Action<TextStyle> configure)
        {
            TextStyle style = ScriptableObject.CreateInstance<TextStyle>();
            configure(style);

            string assetPath = $"{path}/{name}.asset";
            AssetDatabase.CreateAsset(style, assetPath);
            Debug.Log($"Created text style: {assetPath}");
        }

        private void GeneratePresetsAsset()
        {
            string presetsPath = "Assets/Resources/TextStylePresets.asset";
            string stylesPath = "Assets/Resources/TextStyles";

            TextStylePresets presets = ScriptableObject.CreateInstance<TextStylePresets>();
            presets.primaryFont = primaryFont;

            // Load all created styles
            presets.title = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/Title.asset");
            presets.header = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/Header.asset");
            presets.body = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/Body.asset");
            presets.button = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/Button.asset");
            presets.buttonSmall = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/ButtonSmall.asset");
            presets.damageNumber = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/DamageNumber.asset");
            presets.healNumber = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/HealNumber.asset");
            presets.criticalHit = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/CriticalHit.asset");
            presets.unitName = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/UnitName.asset");
            presets.healthBar = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/HealthBar.asset");
            presets.gold = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/Gold.asset");
            presets.xp = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/XP.asset");
            presets.level = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/Level.asset");
            presets.cardName = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/CardName.asset");
            presets.cardCost = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/CardCost.asset");
            presets.cardStats = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/CardStats.asset");
            presets.traitName = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/TraitName.asset");
            presets.timer = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/Timer.asset");
            presets.roundNumber = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/RoundNumber.asset");
            presets.playerName = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/PlayerName.asset");
            presets.notification = AssetDatabase.LoadAssetAtPath<TextStyle>($"{stylesPath}/Notification.asset");

            AssetDatabase.CreateAsset(presets, presetsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created presets: {presetsPath}");
        }
    }
}
#endif
