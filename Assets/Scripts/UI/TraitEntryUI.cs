using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Crestforge.Data;

namespace Crestforge.UI
{
    /// <summary>
    /// Component for trait entries in the trait panel - handles hover detection
    /// </summary>
    public class TraitEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TraitData trait;
        public int count;

        private bool isHovered = false;
        private Image backgroundImage;
        private Color originalColor;

        private void Awake()
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                originalColor = backgroundImage.color;
            }
        }

        public bool IsHovered()
        {
            return isHovered;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            Crestforge.Visuals.AudioManager.Instance?.PlayUIHover();

            // Highlight effect
            if (backgroundImage != null)
            {
                Color highlightColor = originalColor;
                highlightColor.r = Mathf.Min(1f, highlightColor.r + 0.1f);
                highlightColor.g = Mathf.Min(1f, highlightColor.g + 0.1f);
                highlightColor.b = Mathf.Min(1f, highlightColor.b + 0.05f);
                backgroundImage.color = highlightColor;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;

            // Restore original color
            if (backgroundImage != null)
            {
                backgroundImage.color = originalColor;
            }
        }

        public void SetOriginalColor(Color color)
        {
            originalColor = color;
            if (backgroundImage != null && !isHovered)
            {
                backgroundImage.color = color;
            }
        }
    }
}
