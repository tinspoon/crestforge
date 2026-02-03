using UnityEngine;

namespace Crestforge.Cosmetics
{
    /// <summary>
    /// Visual component for scenery placement slots
    /// Shows a subtle indicator where items can be placed
    /// </summary>
    public class ScenerySlotVisual : MonoBehaviour
    {
        public ScenerySlotData slotData;

        [Header("Visual Settings")]
        public Color emptyColor = new Color(0.3f, 0.25f, 0.2f, 0.3f);
        public Color highlightColor = new Color(0.5f, 0.8f, 0.4f, 0.6f);

        private Renderer circleRenderer;
        private bool isHighlighted;

        private void Awake()
        {
            var circle = transform.Find("SlotCircle");
            if (circle != null)
            {
                circleRenderer = circle.GetComponent<Renderer>();
            }
        }

        /// <summary>
        /// Highlight this slot (for edit mode)
        /// </summary>
        public void SetHighlight(bool highlight)
        {
            isHighlighted = highlight;
            if (circleRenderer != null)
            {
                circleRenderer.material.color = highlight ? highlightColor : emptyColor;
            }
        }

        /// <summary>
        /// Show/hide the slot marker
        /// </summary>
        public void SetVisible(bool visible)
        {
            var circle = transform.Find("SlotCircle");
            if (circle != null)
            {
                circle.gameObject.SetActive(visible);
            }
        }
    }
}
