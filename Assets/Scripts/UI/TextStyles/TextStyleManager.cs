using UnityEngine;
using TMPro;

namespace Crestforge.UI
{
    /// <summary>
    /// Global manager for text styles. Provides easy access to style presets.
    /// </summary>
    public class TextStyleManager : MonoBehaviour
    {
        public static TextStyleManager Instance { get; private set; }

        [SerializeField]
        private TextStylePresets presets;

        public static TextStylePresets Presets => Instance?.presets;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // Try to load presets from Resources if not assigned
                if (presets == null)
                {
                    presets = Resources.Load<TextStylePresets>("TextStylePresets");
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Apply a named style to a TMP_Text component
        /// </summary>
        public static void ApplyStyle(TMP_Text text, string styleName)
        {
            if (Presets == null)
            {
                Debug.LogWarning("[TextStyleManager] No presets loaded");
                return;
            }

            TextStyle style = Presets.GetStyle(styleName);
            style?.ApplyTo(text);
        }

        /// <summary>
        /// Apply a TextStyle directly to a TMP_Text component
        /// </summary>
        public static void ApplyStyle(TMP_Text text, TextStyle style)
        {
            style?.ApplyTo(text);
        }

        /// <summary>
        /// Create styled text dynamically
        /// </summary>
        public static TMP_Text CreateText(Transform parent, string styleName, string content = "")
        {
            GameObject textObj = new GameObject("StyledText");
            textObj.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = content;

            ApplyStyle(text, styleName);

            return text;
        }
    }

    /// <summary>
    /// Component to automatically apply a text style to a TMP_Text.
    /// Add this to any TextMeshPro object in the scene.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]
    public class StyledText : MonoBehaviour
    {
        [Tooltip("Style to apply. Can be a TextStyle asset or use preset name.")]
        public TextStyle style;

        [Tooltip("Or use a preset style by name")]
        public string presetStyleName;

        [Tooltip("Apply style on start and when values change")]
        public bool autoApply = true;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            if (autoApply)
            {
                ApplyStyle();
            }
        }

        private void OnValidate()
        {
            if (autoApply && _text == null)
                _text = GetComponent<TMP_Text>();

            if (autoApply && _text != null)
            {
                ApplyStyle();
            }
        }

        public void ApplyStyle()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();

            if (style != null)
            {
                style.ApplyTo(_text);
            }
            else if (!string.IsNullOrEmpty(presetStyleName) && TextStyleManager.Presets != null)
            {
                TextStyleManager.ApplyStyle(_text, presetStyleName);
            }
        }

        /// <summary>
        /// Set text content while maintaining style
        /// </summary>
        public void SetText(string content)
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();

            _text.text = content;
        }
    }
}
