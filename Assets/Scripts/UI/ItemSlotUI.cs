using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Networking;
using Crestforge.Systems;

namespace Crestforge.UI
{
    /// <summary>
    /// UI slot for displaying and interacting with items
    /// </summary>
    public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [Header("References")]
        public Image backgroundImage;
        public Image iconImage;
        public Image rarityBorder;
        public Text nameText;

        [Header("Colors")]
        public Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        public Color filledColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        public Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 0.9f);

        // Runtime
        private ItemData item;
        private ServerItemData serverItem;
        private int itemIndex = -1; // Index in inventory for server actions
        private bool isDragging;
        private RectTransform rectTransform;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Vector3 originalPosition;
        private Transform originalParent;
        private int originalSiblingIndex;

        // Static drag tracking
        public static ItemSlotUI DraggedItem { get; private set; }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void Initialize(ItemData itemData = null)
        {
            SetItem(itemData);
        }

        public void SetItem(ItemData itemData)
        {
            item = itemData;

            if (item != null)
            {
                // Check if this is a consumable
                bool isConsumable = item.effect.IsConsumable();
                Color itemColor = isConsumable ? GetConsumableColor(item.effect) : GetRarityColor(item.rarity);

                if (iconImage != null)
                {
                    // Use custom icons for consumables
                    if (isConsumable)
                    {
                        iconImage.sprite = GetConsumableSprite(item.effect);
                        iconImage.enabled = true;
                        iconImage.color = Color.white;
                    }
                    else
                    {
                        iconImage.sprite = item.icon;
                        iconImage.enabled = item.icon != null;

                        // If no icon, show colored square
                        if (item.icon == null)
                        {
                            iconImage.enabled = true;
                            iconImage.color = itemColor;
                        }
                        else
                        {
                            iconImage.color = Color.white;
                        }
                    }
                }

                if (nameText != null)
                {
                    nameText.text = item.itemName;
                    nameText.color = itemColor;
                }

                if (rarityBorder != null)
                {
                    rarityBorder.color = itemColor;
                    // Make consumable borders glow more
                    if (isConsumable)
                    {
                        rarityBorder.color = new Color(itemColor.r, itemColor.g, itemColor.b, 1f);
                    }
                }

                if (backgroundImage != null)
                {
                    backgroundImage.color = isConsumable ?
                        new Color(itemColor.r * 0.3f, itemColor.g * 0.3f, itemColor.b * 0.3f, 0.9f) :
                        filledColor;
                }
            }
            else
            {
                if (iconImage != null)
                {
                    iconImage.enabled = false;
                }

                if (nameText != null)
                {
                    nameText.text = "";
                }

                if (rarityBorder != null)
                {
                    rarityBorder.color = Color.clear;
                }

                if (backgroundImage != null)
                {
                    backgroundImage.color = emptyColor;
                }
            }
        }

        public ItemData GetItem()
        {
            return item;
        }

        /// <summary>
        /// Set item from server data (multiplayer mode)
        /// </summary>
        public void SetServerItem(ServerItemData serverItemData, int index = -1)
        {
            serverItem = serverItemData;
            itemIndex = index;

            if (serverItemData == null)
            {
                SetItem(null);
                serverItem = null;
                itemIndex = -1;
                return;
            }

            // Check if this is a consumable based on itemId
            bool isConsumable = serverItemData.itemId == "crest_token" || serverItemData.itemId == "item_anvil";
            Color itemColor;

            if (isConsumable)
            {
                // Use consumable colors
                if (serverItemData.itemId == "crest_token")
                {
                    itemColor = new Color(0.6f, 0.4f, 1f); // Purple
                }
                else
                {
                    itemColor = new Color(1f, 0.7f, 0.2f); // Gold/orange
                }
            }
            else
            {
                // Gold color for all items
                itemColor = new Color(1f, 0.8f, 0.4f);
            }

            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.color = itemColor;
                iconImage.sprite = null;
            }

            if (nameText != null)
            {
                nameText.text = serverItemData.name ?? serverItemData.itemId;
                nameText.color = itemColor;
            }

            if (rarityBorder != null)
            {
                rarityBorder.color = itemColor;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = isConsumable ?
                    new Color(itemColor.r * 0.3f, itemColor.g * 0.3f, itemColor.b * 0.3f, 0.9f) :
                    filledColor;
            }

            // Clear single-player item reference
            item = null;
        }

        public ServerItemData GetServerItem()
        {
            return serverItem;
        }

        public int GetItemIndex()
        {
            return itemIndex;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging) return;

            bool hasItem = item != null || serverItem != null;
            if (hasItem)
            {
                if (backgroundImage != null)
                {
                    backgroundImage.color = hoverColor;
                }

                // Show tooltip - prefer single-player item, fall back to server item
                if (item != null)
                {
                    GameUI.Instance?.ShowItemTooltip(item, Vector3.zero);
                }
                else if (serverItem != null)
                {
                    GameUI.Instance?.ShowServerItemInfoTemporary(serverItem);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isDragging)
            {
                bool hasItem = item != null || serverItem != null;
                if (backgroundImage != null)
                {
                    backgroundImage.color = hasItem ? filledColor : emptyColor;
                }

                GameUI.Instance?.HideItemTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Handle single-player items
            if (item != null)
            {
                // Left-click
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    // Check if item is a consumable
                    if (item.effect.IsConsumable())
                    {
                        UseConsumable();
                    }
                    else
                    {
                        // Pin/unpin tooltip for regular items
                        GameUI.Instance?.ToggleItemTooltipPin(item);
                    }
                }
                return;
            }

            // Handle server items (multiplayer)
            if (serverItem != null)
            {
                // Left-click
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    // Check if item is a consumable
                    bool isConsumable = serverItem.itemId == "crest_token" || serverItem.itemId == "item_anvil";
                    if (isConsumable)
                    {
                        UseConsumableMultiplayer();
                    }
                    else
                    {
                        // Show tooltip for regular items
                        GameUI.Instance?.ShowServerItemInfoTemporary(serverItem);
                    }
                }
            }
        }

        private void UseConsumable()
        {
            if (item == null || !item.effect.IsConsumable()) return;

            var state = GameState.Instance;
            if (state == null) return;

            // Remove from inventory
            state.itemInventory.Remove(item);

            // Trigger appropriate selection based on consumable type
            if (item.effect == ItemEffect.ConsumableCrestToken)
            {
                state.GenerateCrestSelection(CrestType.Minor, GameConstants.Crests.CREST_CHOICES);
                state.round.phase = GamePhase.CrestSelect;
                RoundManager.Instance?.OnPhaseChanged?.Invoke(GamePhase.CrestSelect);
                Debug.Log("Using Crest Token - opening crest selection!");
            }
            else if (item.effect == ItemEffect.ConsumableItemAnvil)
            {
                state.GenerateItemSelection(GameConstants.Rounds.ITEMS_PER_SELECTION);
                state.round.phase = GamePhase.ItemSelect;
                RoundManager.Instance?.OnPhaseChanged?.Invoke(GamePhase.ItemSelect);
                Debug.Log("Using Item Anvil - opening item selection!");
            }

            // Refresh UI
            GameUI.Instance?.RefreshItemInventory();
            GameUI.Instance?.HideItemTooltip();
        }

        /// <summary>
        /// Use a consumable item in multiplayer mode
        /// </summary>
        private void UseConsumableMultiplayer()
        {
            if (serverItem == null || itemIndex < 0) return;

            bool isConsumable = serverItem.itemId == "crest_token" || serverItem.itemId == "item_anvil";
            if (!isConsumable) return;

            var serverState = ServerGameState.Instance;
            if (serverState == null || !serverState.IsInGame) return;

            Debug.Log($"[ItemSlotUI] Using consumable {serverItem.itemId} at index {itemIndex}, phase={serverState.phase}");
            serverState.UseConsumable(itemIndex);
            Debug.Log($"[ItemSlotUI] UseConsumable call completed");

            // Hide tooltip
            GameUI.Instance?.HideItemTooltip();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Allow dragging if we have either single-player item or server item
            if (item == null && serverItem == null) return;

            isDragging = true;
            DraggedItem = this;
            originalPosition = rectTransform.position;
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();

            // Move to top of hierarchy for rendering
            transform.SetParent(canvas.transform);
            transform.SetAsLastSibling();

            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.8f;

            // Block camera input during drag
            if (Crestforge.Visuals.IsometricCameraSetup.Instance != null)
            {
                Crestforge.Visuals.IsometricCameraSetup.Instance.inputBlocked = true;
            }

            GameUI.Instance?.HideItemTooltip();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            rectTransform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;
            DraggedItem = null;

            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            // Unblock camera input
            if (Crestforge.Visuals.IsometricCameraSetup.Instance != null)
            {
                Crestforge.Visuals.IsometricCameraSetup.Instance.inputBlocked = false;
            }

            // Check if dropped on a unit
            bool equipped = false;
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            // First check UI elements (bench units)
            foreach (var result in results)
            {
                // Check for unit card (bench unit)
                var unitCard = result.gameObject.GetComponent<UnitCardUI>();
                if (unitCard != null && unitCard.GetUnit() != null)
                {
                    equipped = TryEquipToUnit(unitCard.GetUnit());
                    break;
                }
            }

            // If not dropped on UI, try 3D board units
            if (!equipped)
            {
                equipped = TryEquipToBoardUnit(eventData.position);
            }

            // Return to original parent and let layout group handle positioning
            transform.SetParent(originalParent);
            transform.SetSiblingIndex(originalSiblingIndex);
        }

        private bool TryEquipToBoardUnit(Vector2 screenPosition)
        {
            // Raycast into the 3D scene to find board units
            Camera cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;

                var unitVisual = hit.collider.GetComponentInParent<Crestforge.Visuals.UnitVisual3D>();

                // Check multiplayer mode first
                var serverState = ServerGameState.Instance;
                if (serverState != null && serverState.IsInGame)
                {
                    string instanceId = null;

                    // During combat, check if this is a combat unit visual (our own units fighting)
                    var combatVisualizer = Crestforge.Systems.ServerCombatVisualizer.Instance;
                    if (combatVisualizer != null && combatVisualizer.IsPlayingCombat && unitVisual != null)
                    {
                        // Get combat unit data to find instanceId
                        var combatUnit = combatVisualizer.GetCombatUnitByVisual(unitVisual);
                        if (combatUnit != null && combatUnit.CombatUnitData != null)
                        {
                            // Only allow equipping to our own units (not enemies)
                            if (combatUnit.TeamId == "player1" || combatUnit.CombatUnitData.playerId == serverState.localPlayerId)
                            {
                                instanceId = combatUnit.InstanceId;
                                Debug.Log($"[ItemSlotUI] Found combat unit for item equip: {instanceId}");
                            }
                        }
                    }

                    // If not a combat unit, check regular board unit
                    if (string.IsNullOrEmpty(instanceId) && unitVisual != null && !unitVisual.isEnemy)
                    {
                        instanceId = unitVisual.ServerInstanceId;
                    }

                    // Also check bench units during combat
                    if (string.IsNullOrEmpty(instanceId) && unitVisual != null)
                    {
                        // Bench units should still be equippable during combat
                        var benchUnit = combatVisualizer?.GetBenchUnitByVisual(unitVisual);
                        if (benchUnit != null)
                        {
                            instanceId = benchUnit.instanceId;
                            Debug.Log($"[ItemSlotUI] Found bench unit for item equip: {instanceId}");
                        }
                    }

                    if (!string.IsNullOrEmpty(instanceId) && itemIndex >= 0)
                    {
                        serverState.EquipItem(itemIndex, instanceId);
                        Debug.Log($"[ItemSlotUI] Equipped item {itemIndex} to unit {instanceId}");
                        return true;
                    }
                }
                else if (unitVisual != null && !unitVisual.isEnemy && unitVisual.unit != null && unitVisual.unit.template != null)
                {
                    // Single-player mode
                    return TryEquipToUnit(unitVisual.unit);
                }
            }

            return false;
        }

        private bool TryEquipToUnit(UnitInstance unit)
        {
            if (unit == null || unit.template == null || item == null) return false;

            // Store item info before any operations that might change this.item
            string itemName = item.itemName;
            string unitName = unit.template?.unitName ?? "Unknown";
            ItemData itemToEquip = item;

            if (unit.EquipItem(itemToEquip))
            {
                // Remove from inventory
                GameState.Instance?.itemInventory.Remove(itemToEquip);

                // Refresh UI (this may change this.item, so we use stored values)
                GameUI.Instance?.RefreshItemInventory();
                GameUI.Instance?.RefreshBench();

                Debug.Log($"Equipped {itemName} to {unitName}");
                return true;
            }
            else
            {
                Debug.Log($"Cannot equip {itemName} - unit has max items");
                return false;
            }
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.6f, 0.6f, 0.6f),
                ItemRarity.Uncommon => new Color(0.3f, 0.8f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
                _ => Color.white
            };
        }

        private Color GetConsumableColor(ItemEffect effect)
        {
            return effect switch
            {
                ItemEffect.ConsumableCrestToken => new Color(0.8f, 0.5f, 1f),   // Purple for crest
                ItemEffect.ConsumableItemAnvil => new Color(1f, 0.8f, 0.3f),    // Gold for item anvil
                _ => Color.white
            };
        }

        private Sprite GetConsumableSprite(ItemEffect effect)
        {
            return effect switch
            {
                ItemEffect.ConsumableCrestToken => ConsumableIcons.GetCrestTokenIcon(),
                ItemEffect.ConsumableItemAnvil => ConsumableIcons.GetItemAnvilIcon(),
                _ => null
            };
        }

        /// <summary>
        /// Create an item slot UI element
        /// </summary>
        public static ItemSlotUI Create(Transform parent, Vector2 size)
        {
            GameObject slotObj = new GameObject("ItemSlot");
            slotObj.transform.SetParent(parent, false);

            RectTransform rt = slotObj.AddComponent<RectTransform>();
            rt.sizeDelta = size;

            // Background
            Image bg = slotObj.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            // Rarity border
            GameObject borderObj = new GameObject("RarityBorder");
            borderObj.transform.SetParent(slotObj.transform, false);
            RectTransform borderRT = borderObj.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = Vector2.zero;
            borderRT.offsetMin = new Vector2(-2, -2);
            borderRT.offsetMax = new Vector2(2, 2);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = Color.clear;
            borderObj.transform.SetAsFirstSibling();

            // Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.1f, 0.25f);
            iconRT.anchorMax = new Vector2(0.9f, 0.95f);
            iconRT.sizeDelta = Vector2.zero;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.enabled = false;

            // Name text
            GameObject textObj = new GameObject("Name");
            textObj.transform.SetParent(slotObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0, 0);
            textRT.anchorMax = new Vector2(1, 0.25f);
            textRT.sizeDelta = Vector2.zero;
            textRT.offsetMin = new Vector2(2, 2);
            textRT.offsetMax = new Vector2(-2, 0);
            Text nameText = textObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 10;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;

            // Add component
            ItemSlotUI slot = slotObj.AddComponent<ItemSlotUI>();
            slot.backgroundImage = bg;
            slot.iconImage = iconImg;
            slot.rarityBorder = borderImg;
            slot.nameText = nameText;

            return slot;
        }
    }
}
