using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Crestforge.Data;
using Crestforge.Networking;

namespace Crestforge.UI
{
    /// <summary>
    /// Component for unit cards in shop/bench
    /// </summary>
    public class UnitCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Image background;
        public Image spriteImage;
        public Text costText;
        public Text nameText;
        public Button button;
        public int index;
        public bool isBenchSlot = false;

        private UnitInstance currentUnit;
        private bool isClicking = false;
        private float clickSuppressionTime = 0f;
        private bool isDragging = false;

        public UnitInstance GetUnit() => currentUnit;

        public void SetUnit(UnitInstance unit)
        {
            currentUnit = unit;

            if (unit == null || unit.template == null)
            {
                background.color = new Color(0.2f, 0.25f, 0.3f, 0.5f);
                spriteImage.enabled = false;
                spriteImage.sprite = null;
                costText.text = "";
                if (nameText != null) nameText.text = "";
                button.interactable = false;
                return;
            }

            var t = unit.template;

            // Set color based on cost
            background.color = GetCostColor(t.cost);

            // Set sprite
            Sprite unitSprite = UnitSpriteGenerator.GetSprite(t.unitId);
            if (unitSprite != null)
            {
                spriteImage.sprite = unitSprite;
                spriteImage.enabled = true;
            }
            else
            {
                spriteImage.enabled = false;
            }

            // Set text - bench shows only stars, shop shows cost + name
            if (isBenchSlot)
            {
                costText.text = new string('★', unit.starLevel);
            }
            else
            {
                costText.text = $"${t.cost} " + new string('★', unit.starLevel);
                if (nameText != null) nameText.text = t.unitName;
            }

            button.interactable = true;
        }

        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;

            Color c = background.color;
            c.a = interactable ? 1f : 0.5f;
            background.color = c;
        }

        /// <summary>
        /// Set unit from server shop data (multiplayer mode)
        /// </summary>
        public void SetServerUnit(ServerShopUnit serverUnit)
        {
            currentUnit = null; // No local unit instance

            if (serverUnit == null)
            {
                background.color = new Color(0.2f, 0.25f, 0.3f, 0.5f);
                spriteImage.enabled = false;
                spriteImage.sprite = null;
                costText.text = "";
                if (nameText != null) nameText.text = "";
                button.interactable = false;
                return;
            }

            // Set color based on cost
            background.color = GetCostColor(serverUnit.cost);

            // Set sprite
            Sprite unitSprite = UnitSpriteGenerator.GetSprite(serverUnit.unitId);
            if (unitSprite != null)
            {
                spriteImage.sprite = unitSprite;
                spriteImage.enabled = true;
            }
            else
            {
                spriteImage.enabled = false;
            }

            // Set text
            costText.text = $"${serverUnit.cost}";
            if (nameText != null) nameText.text = serverUnit.name;

            button.interactable = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Don't show tooltip if clicking or recently clicked
            if (isClicking || Time.time - clickSuppressionTime < 0.15f)
                return;

            Crestforge.Visuals.AudioManager.Instance?.PlayUIHover();

            if (currentUnit != null && GameUI.Instance != null)
            {
                GameUI.Instance.ShowTooltip(currentUnit);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (GameUI.Instance != null)
            {
                GameUI.Instance.HideTooltip();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isClicking = true;
            // Hide tooltip immediately when clicking
            if (GameUI.Instance != null)
            {
                GameUI.Instance.HideTooltip();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isClicking = false;
            clickSuppressionTime = Time.time;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Don't handle click if we were dragging
            if (isDragging) return;

            // Only pin tooltip on click for bench slots (which can have equipped items)
            if (isBenchSlot && currentUnit != null && GameUI.Instance != null)
            {
                // Pin the tooltip so user can interact with items
                GameUI.Instance.ShowTooltipPinned(currentUnit);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Only allow dragging from bench slots with a unit
            if (!isBenchSlot || currentUnit == null) return;

            isDragging = true;
            isClicking = false;

            // Force hide tooltip when starting drag (even if pinned)
            GameUI.Instance?.HideTooltipPinned();

            // Start the 3D drag via BoardManager
            var boardManager = Crestforge.Visuals.BoardManager3D.Instance;
            if (boardManager != null)
            {
                boardManager.StartBenchDrag(currentUnit, index);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            // Update the 3D drag position
            var boardManager = Crestforge.Visuals.BoardManager3D.Instance;
            if (boardManager != null)
            {
                boardManager.UpdateBenchDrag(eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;

            // End the 3D drag
            var boardManager = Crestforge.Visuals.BoardManager3D.Instance;
            if (boardManager != null)
            {
                boardManager.EndBenchDrag();
            }
        }

        private Color GetCostColor(int cost)
        {
            return cost switch
            {
                1 => new Color(0.45f, 0.45f, 0.5f),
                2 => new Color(0.25f, 0.5f, 0.25f),
                3 => new Color(0.25f, 0.4f, 0.7f),
                4 => new Color(0.55f, 0.25f, 0.55f),
                _ => new Color(0.3f, 0.3f, 0.35f)
            };
        }
    }
}
