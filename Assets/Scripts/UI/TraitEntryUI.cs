using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Crestforge.Data;

namespace Crestforge.UI
{
    /// <summary>
    /// Component for trait icon entries in the trait panel - handles tap and hover detection
    /// </summary>
    public class TraitEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public TraitData trait;
        public int count;

        private bool isHovered = false;
        private Image backgroundImage;
        private Color originalColor;

        // Static reference to currently selected entry (for tap-to-toggle on mobile)
        private static TraitEntryUI currentlySelected;

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
            ApplyHighlight();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // On mobile, keep selected state even after pointer exit
            if (currentlySelected == this) return;

            isHovered = false;
            RestoreColor();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Toggle behavior for mobile: tap to select, tap again to deselect
            if (currentlySelected == this)
            {
                // Deselect
                currentlySelected = null;
                isHovered = false;
                RestoreColor();
            }
            else
            {
                // Deselect previous
                if (currentlySelected != null)
                {
                    currentlySelected.isHovered = false;
                    currentlySelected.RestoreColor();
                }

                // Select this one
                currentlySelected = this;
                isHovered = true;
                ApplyHighlight();
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

        /// <summary>
        /// Deselect any currently selected trait entry (called when tapping outside)
        /// </summary>
        public static void DeselectAll()
        {
            if (currentlySelected != null)
            {
                currentlySelected.isHovered = false;
                currentlySelected.RestoreColor();
                currentlySelected = null;
            }
        }

        private void ApplyHighlight()
        {
            if (backgroundImage != null)
            {
                Color highlightColor = originalColor;
                highlightColor.r = Mathf.Min(1f, highlightColor.r + 0.15f);
                highlightColor.g = Mathf.Min(1f, highlightColor.g + 0.15f);
                highlightColor.b = Mathf.Min(1f, highlightColor.b + 0.1f);
                backgroundImage.color = highlightColor;
            }
        }

        private void RestoreColor()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = originalColor;
            }
        }
    }
}
