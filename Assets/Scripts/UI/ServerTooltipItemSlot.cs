using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Crestforge.Networking;

namespace Crestforge.UI
{
    /// <summary>
    /// Item slot displayed in unit tooltip for multiplayer mode - can be dragged to unequip
    /// </summary>
    public class ServerTooltipItemSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        public Image background;
        public Image icon;
        public Image border;

        private ServerItemData serverItem;
        private string unitInstanceId;
        private int itemSlot;
        private bool isDragging;
        private RectTransform rectTransform;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Vector3 originalPosition;
        private Transform originalParent;

        private static readonly Color ItemColor = new Color(1f, 0.8f, 0.4f);  // Gold for all items

        private void Awake()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    Canvas rootCanvas = canvas.rootCanvas;
                    if (rootCanvas != null)
                        canvas = rootCanvas;
                }
            }
        }

        public static ServerTooltipItemSlot Create(Transform parent, Vector2 size, ServerItemData itemData, string instanceId, int slot)
        {
            GameObject slotObj = new GameObject($"ServerTooltipItem_{itemData.name}");
            slotObj.transform.SetParent(parent, false);

            RectTransform rt = slotObj.AddComponent<RectTransform>();
            rt.sizeDelta = size;

            // Background - this is the raycast target for drag events
            Image bg = slotObj.AddComponent<Image>();
            bg.color = new Color(ItemColor.r * 0.3f, ItemColor.g * 0.3f, ItemColor.b * 0.3f, 0.9f);
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
            borderImg.color = ItemColor;
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
            iconImg.color = ItemColor;
            iconImg.raycastTarget = false;

            // Canvas group for drag transparency
            CanvasGroup cg = slotObj.AddComponent<CanvasGroup>();
            cg.interactable = true;
            cg.blocksRaycasts = true;

            // Component
            ServerTooltipItemSlot component = slotObj.AddComponent<ServerTooltipItemSlot>();
            component.background = bg;
            component.icon = iconImg;
            component.border = borderImg;
            component.serverItem = itemData;
            component.unitInstanceId = instanceId;
            component.itemSlot = slot;
            component.rectTransform = rt;
            component.canvasGroup = cg;

            // Get the root canvas for drag reparenting
            Canvas rootCanvas = parent.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                while (rootCanvas.transform.parent != null)
                {
                    Canvas parentCanvas = rootCanvas.transform.parent.GetComponentInParent<Canvas>();
                    if (parentCanvas == null) break;
                    rootCanvas = parentCanvas;
                }
            }
            component.canvas = rootCanvas;

            return component;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isDragging && serverItem != null)
            {
                if (background != null)
                {
                    Color c = background.color;
                    background.color = new Color(c.r + 0.2f, c.g + 0.2f, c.b + 0.2f, c.a);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isDragging && serverItem != null)
            {
                if (background != null)
                {
                    background.color = new Color(ItemColor.r * 0.3f, ItemColor.g * 0.3f, ItemColor.b * 0.3f, 0.9f);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Required for drag to work
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (serverItem != null && !isDragging)
            {
                GameUI.Instance?.ShowServerItemInfoTemporary(serverItem);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (serverItem == null || string.IsNullOrEmpty(unitInstanceId))
            {
                return;
            }

            Debug.Log($"[ServerTooltipItemSlot] OnBeginDrag started for {serverItem.name}");

            isDragging = true;
            originalPosition = rectTransform.position;
            originalParent = transform.parent;

            if (canvas != null)
            {
                transform.SetParent(canvas.transform);
                transform.SetAsLastSibling();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.8f;

            if (Crestforge.Visuals.IsometricCameraSetup.Instance != null)
            {
                Crestforge.Visuals.IsometricCameraSetup.Instance.inputBlocked = true;
            }

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
                return;
            }

            Debug.Log($"[ServerTooltipItemSlot] OnEndDrag for {serverItem?.name}");

            isDragging = false;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            if (Crestforge.Visuals.IsometricCameraSetup.Instance != null)
            {
                Crestforge.Visuals.IsometricCameraSetup.Instance.inputBlocked = false;
            }

            // Send unequip action to server
            if (!string.IsNullOrEmpty(unitInstanceId) && serverItem != null)
            {
                Debug.Log($"[ServerTooltipItemSlot] Unequipping item {serverItem.itemId} from unit {unitInstanceId} slot {itemSlot}");
                ServerGameState.Instance?.UnequipItem(unitInstanceId, itemSlot);

                // Destroy this slot - the server will update the state
                Destroy(gameObject);
            }
            else
            {
                // Return to original position if we can't unequip
                transform.SetParent(originalParent);
                rectTransform.position = originalPosition;
            }
        }
    }
}
