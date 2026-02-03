using UnityEngine;
using UnityEngine.EventSystems;

namespace Crestforge.UI
{
    /// <summary>
    /// Drop zone for selling units - handles when units are dropped on the sell overlay
    /// </summary>
    public class SellDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private UnityEngine.UI.Image backgroundImage;
        private Color normalColor;
        private Color hoverColor;

        private void Awake()
        {
            backgroundImage = GetComponent<UnityEngine.UI.Image>();
            if (backgroundImage != null)
            {
                normalColor = backgroundImage.color;
                hoverColor = new Color(0.8f, 0.3f, 0.1f, 0.98f); // Brighter when hovering
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Highlight when dragging over
            if (backgroundImage != null && GameUI.Instance?.IsSellModeActive == true)
            {
                backgroundImage.color = hoverColor;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Return to normal color
            if (backgroundImage != null)
            {
                backgroundImage.color = normalColor;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            // This is called when a UI element is dropped on this zone
            // For bench unit drops, BoardManager3D handles it via EndBenchDrag
            // This handles UI-based drops if needed
            Debug.Log("[SellDropZone] OnDrop called");
        }
    }
}
