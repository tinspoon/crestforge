using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Crestforge.Core;
using Crestforge.Data;

namespace Crestforge.UI
{
    /// <summary>
    /// Item slot displayed in unit tooltip - can be dragged to unequip
    /// </summary>
    public class TooltipItemSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        public Image background;
        public Image icon;
        public Image border;
        public Text nameText;

        private ItemData item;
        private UnitInstance ownerUnit;
        private int itemIndex;
        private bool isDragging;
        private RectTransform rectTransform;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Vector3 originalPosition;
        private Transform originalParent;

        private void Awake()
        {
            // Ensure we have references even if not set in Create
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
                // Get root canvas
                if (canvas != null)
                {
                    Canvas rootCanvas = canvas.rootCanvas;
                    if (rootCanvas != null)
                        canvas = rootCanvas;
                }
            }

            Debug.Log($"[TooltipItemSlot] Awake - canvas: {(canvas != null ? canvas.name : "NULL")}, " +
                      $"canvasGroup: {(canvasGroup != null)}, rectTransform: {(rectTransform != null)}");
        }

        public static TooltipItemSlot Create(Transform parent, Vector2 size, ItemData itemData, UnitInstance unit, int index)
        {
            GameObject slotObj = new GameObject($"TooltipItem_{itemData.itemName}");
            slotObj.transform.SetParent(parent, false);

            RectTransform rt = slotObj.AddComponent<RectTransform>();
            rt.sizeDelta = size;

            // Background - this is the raycast target for drag events
            Image bg = slotObj.AddComponent<Image>();
            Color rarityColor = GetRarityColor(itemData.rarity);
            bg.color = new Color(rarityColor.r * 0.3f, rarityColor.g * 0.3f, rarityColor.b * 0.3f, 0.9f);
            bg.raycastTarget = true;

            // Border - don't intercept raycasts
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(slotObj.transform, false);
            RectTransform borderRT = borderObj.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = Vector2.zero;
            borderRT.offsetMin = new Vector2(-2, -2);
            borderRT.offsetMax = new Vector2(2, 2);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = rarityColor;
            borderImg.raycastTarget = false;
            borderObj.transform.SetAsFirstSibling();

            // Icon - don't intercept raycasts
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.1f, 0.1f);
            iconRT.anchorMax = new Vector2(0.9f, 0.9f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.color = rarityColor;
            iconImg.raycastTarget = false;

            // Canvas group for drag transparency - ensure it's interactive
            CanvasGroup cg = slotObj.AddComponent<CanvasGroup>();
            cg.interactable = true;
            cg.blocksRaycasts = true;

            // Component
            TooltipItemSlot slot = slotObj.AddComponent<TooltipItemSlot>();
            slot.background = bg;
            slot.icon = iconImg;
            slot.border = borderImg;
            slot.item = itemData;
            slot.ownerUnit = unit;
            slot.itemIndex = index;
            slot.rectTransform = rt;
            slot.canvasGroup = cg;

            // Get the root canvas for drag reparenting
            Canvas rootCanvas = parent.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                // Get the root canvas (not a nested one)
                while (rootCanvas.transform.parent != null)
                {
                    Canvas parentCanvas = rootCanvas.transform.parent.GetComponentInParent<Canvas>();
                    if (parentCanvas == null) break;
                    rootCanvas = parentCanvas;
                }
            }
            slot.canvas = rootCanvas;

            return slot;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isDragging && item != null)
            {
                // Highlight the slot to show it's interactive
                if (background != null)
                {
                    Color c = background.color;
                    background.color = new Color(c.r + 0.2f, c.g + 0.2f, c.b + 0.2f, c.a);
                }
                Debug.Log($"[TooltipItemSlot] Hover enter: {item.itemName}");
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isDragging && item != null)
            {
                // Restore original color
                Color rarityColor = GetRarityColor(item.rarity);
                if (background != null)
                {
                    background.color = new Color(rarityColor.r * 0.3f, rarityColor.g * 0.3f, rarityColor.b * 0.3f, 0.9f);
                }
                Debug.Log($"[TooltipItemSlot] Hover exit: {item.itemName}");
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Required for drag to work - capture the pointer down event
            Debug.Log($"[TooltipItemSlot] OnPointerDown: {item?.itemName}");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Show item info in the tooltip when clicked
            if (item != null && !isDragging)
            {
                Debug.Log($"[TooltipItemSlot] OnPointerClick: Showing item info for {item.itemName}");
                GameUI.Instance?.ShowItemInfoTemporary(item);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (item == null || ownerUnit == null)
            {
                Debug.Log($"[TooltipItemSlot] OnBeginDrag cancelled - item: {item}, ownerUnit: {ownerUnit}");
                return;
            }

            Debug.Log($"[TooltipItemSlot] OnBeginDrag started for {item.itemName}");

            isDragging = true;
            originalPosition = rectTransform.position;
            originalParent = transform.parent;

            // Move to canvas root for rendering
            if (canvas != null)
            {
                transform.SetParent(canvas.transform);
                transform.SetAsLastSibling();
            }
            else
            {
                Debug.LogWarning("[TooltipItemSlot] Canvas is null, cannot reparent for drag");
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.8f;

            // Block camera
            if (Crestforge.Visuals.IsometricCameraSetup.Instance != null)
            {
                Crestforge.Visuals.IsometricCameraSetup.Instance.inputBlocked = true;
            }

            // Restore unit tooltip before dragging
            GameUI.Instance?.RestoreUnitTooltip();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            rectTransform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging)
            {
                Debug.Log("[TooltipItemSlot] OnEndDrag called but not dragging");
                return;
            }

            Debug.Log($"[TooltipItemSlot] OnEndDrag for {item?.itemName}");

            isDragging = false;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            // Unblock camera
            if (Crestforge.Visuals.IsometricCameraSetup.Instance != null)
            {
                Crestforge.Visuals.IsometricCameraSetup.Instance.inputBlocked = false;
            }

            // Any drag-and-release will unequip the item
            bool unequipped = false;

            // Try to unequip the item
            if (ownerUnit != null && item != null)
            {
                Debug.Log($"[TooltipItemSlot] Attempting to unequip item at index {itemIndex}");
                var removedItem = ownerUnit.UnequipItem(itemIndex);
                if (removedItem != null)
                {
                    Debug.Log($"[TooltipItemSlot] Successfully unequipped {removedItem.itemName}");
                    GameUI.Instance?.OnItemUnequipped(ownerUnit, removedItem);
                    unequipped = true;
                }
                else
                {
                    Debug.Log($"[TooltipItemSlot] UnequipItem returned null");
                }
            }

            if (!unequipped)
            {
                // Return to original position
                Debug.Log("[TooltipItemSlot] Returning to original position");
                transform.SetParent(originalParent);
                rectTransform.position = originalPosition;
            }
            else
            {
                // Destroy this slot since item was unequipped
                Debug.Log("[TooltipItemSlot] Destroying slot after unequip");
                Destroy(gameObject);
            }
        }

        private static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.6f, 0.6f, 0.6f),
                ItemRarity.Uncommon => new Color(0.3f, 0.8f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
                _ => Color.white
            };
        }
    }
}
