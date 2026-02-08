using UnityEngine;
using TMPro;

namespace Crestforge.UI
{
    /// <summary>
    /// Defines a reusable text style for TextMeshPro
    /// </summary>
    [CreateAssetMenu(fileName = "NewTextStyle", menuName = "Crestforge/UI/Text Style")]
    public class TextStyle : ScriptableObject
    {
        [Header("Font")]
        public TMP_FontAsset font;
        public FontStyles fontStyle = FontStyles.Normal;

        [Header("Size")]
        public float fontSize = 24f;
        public bool autoSize = false;
        [ShowIf("autoSize")]
        public float fontSizeMin = 12f;
        [ShowIf("autoSize")]
        public float fontSizeMax = 72f;

        [Header("Color")]
        public Color color = Color.white;
        public bool useGradient = false;
        public VertexGradient gradient = new VertexGradient(Color.white);

        [Header("Spacing")]
        public float characterSpacing = 0f;
        public float wordSpacing = 0f;
        public float lineSpacing = 0f;
        public float paragraphSpacing = 0f;

        [Header("Outline")]
        public bool useOutline = false;
        public Color outlineColor = Color.black;
        [Range(0f, 1f)]
        public float outlineWidth = 0.1f;

        [Header("Shadow/Underlay")]
        public bool useShadow = false;
        public Color shadowColor = new Color(0, 0, 0, 0.5f);
        public Vector2 shadowOffset = new Vector2(1f, -1f);
        [Range(0f, 1f)]
        public float shadowDilate = 0f;
        [Range(0f, 1f)]
        public float shadowSoftness = 0.2f;

        [Header("Alignment")]
        public TextAlignmentOptions alignment = TextAlignmentOptions.Center;

        /// <summary>
        /// Apply this style to a TextMeshPro component
        /// </summary>
        public void ApplyTo(TMP_Text text)
        {
            if (text == null) return;

            // Font
            if (font != null)
                text.font = font;
            text.fontStyle = fontStyle;

            // Size
            text.enableAutoSizing = autoSize;
            if (autoSize)
            {
                text.fontSizeMin = fontSizeMin;
                text.fontSizeMax = fontSizeMax;
            }
            else
            {
                text.fontSize = fontSize;
            }

            // Color
            if (useGradient)
            {
                text.enableVertexGradient = true;
                text.colorGradient = gradient;
            }
            else
            {
                text.enableVertexGradient = false;
                text.color = color;
            }

            // Spacing
            text.characterSpacing = characterSpacing;
            text.wordSpacing = wordSpacing;
            text.lineSpacing = lineSpacing;
            text.paragraphSpacing = paragraphSpacing;

            // Alignment
            text.alignment = alignment;

            // Outline and Shadow require material property changes
            ApplyMaterialEffects(text);
        }

        private void ApplyMaterialEffects(TMP_Text text)
        {
            // Create instance of material to avoid modifying shared material
            if (text.fontSharedMaterial == null) return;

            Material mat = text.fontMaterial; // This creates an instance

            // Outline
            if (useOutline)
            {
                mat.EnableKeyword("OUTLINE_ON");
                mat.SetColor("_OutlineColor", outlineColor);
                mat.SetFloat("_OutlineWidth", outlineWidth);
            }
            else
            {
                mat.DisableKeyword("OUTLINE_ON");
                mat.SetFloat("_OutlineWidth", 0f);
            }

            // Shadow (Underlay in TMP)
            if (useShadow)
            {
                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor("_UnderlayColor", shadowColor);
                mat.SetFloat("_UnderlayOffsetX", shadowOffset.x);
                mat.SetFloat("_UnderlayOffsetY", shadowOffset.y);
                mat.SetFloat("_UnderlayDilate", shadowDilate);
                mat.SetFloat("_UnderlaySoftness", shadowSoftness);
            }
            else
            {
                mat.DisableKeyword("UNDERLAY_ON");
            }
        }
    }

    /// <summary>
    /// Attribute to show fields conditionally (for editor display)
    /// </summary>
    public class ShowIfAttribute : PropertyAttribute
    {
        public string conditionField;
        public ShowIfAttribute(string conditionField)
        {
            this.conditionField = conditionField;
        }
    }
}
