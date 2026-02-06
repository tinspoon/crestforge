using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Crestforge.Core;
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
        public Text nameText; // Optional, may be null for shop cards
        public Text starsText; // Separate star display
        public Image gradientOverlay; // Rarity gradient overlay
        public RectTransform traitIconContainer; // Container for trait icons at bottom
        public Button button;
        public int index;
        public bool isBenchSlot = false;
        public bool isShopCard = false;

        private UnitInstance currentUnit;
        private ServerShopUnit currentServerUnit;
        private bool isClicking = false;
        private float clickSuppressionTime = 0f;
        private bool isDragging = false;

        // Long-press detection for mobile
        private const float LONG_PRESS_DURATION = 0.4f;
        private float pointerDownTime;
        private bool isLongPress = false;
        private Coroutine longPressCoroutine;

        public UnitInstance GetUnit() => currentUnit;
        public ServerShopUnit GetServerUnit() => currentServerUnit;

        public void SetUnit(UnitInstance unit)
        {
            currentUnit = unit;
            currentServerUnit = null;

            if (unit == null || unit.template == null)
            {
                background.color = new Color(0.2f, 0.25f, 0.3f, 0.5f);
                spriteImage.enabled = false;
                spriteImage.sprite = null;
                costText.text = "";
                if (starsText != null) starsText.text = "";
                if (nameText != null) nameText.text = "";
                if (gradientOverlay != null) gradientOverlay.color = new Color(0.5f, 0.5f, 0.55f, 0f);
                ClearTraitIcons();
                button.interactable = false;
                return;
            }

            var t = unit.template;

            // Set background and gradient overlay based on rarity
            if (isShopCard)
            {
                background.color = new Color(0.22f, 0.22f, 0.28f);
                UpdateRarityGradient(t.cost);
            }
            else
            {
                background.color = GetCostColor(t.cost);
            }

            // Set sprite - prefer 3D portrait, fall back to pixel art
            Sprite unitSprite = UnitPortraitGenerator.GetPortrait(t.unitId, t.unitName);
            if (unitSprite != null)
            {
                spriteImage.sprite = unitSprite;
                spriteImage.enabled = true;
            }
            else
            {
                spriteImage.enabled = false;
            }

            // Set cost (just the number for coin badge)
            if (isShopCard)
            {
                costText.text = t.cost.ToString();
                if (starsText != null)
                {
                    starsText.text = unit.starLevel > 1 ? new string('★', unit.starLevel) : "";
                }
            }
            else if (isBenchSlot)
            {
                costText.text = new string('★', unit.starLevel);
            }
            else
            {
                costText.text = $"${t.cost}";
                if (nameText != null) nameText.text = t.unitName;
            }

            // Update trait icons
            if (traitIconContainer != null && t.traits != null)
            {
                PopulateTraitIcons(t.traits);
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
            currentServerUnit = serverUnit;

            if (serverUnit == null)
            {
                background.color = new Color(0.2f, 0.25f, 0.3f, 0.5f);
                spriteImage.enabled = false;
                spriteImage.sprite = null;
                costText.text = "";
                if (starsText != null) starsText.text = "";
                if (nameText != null) nameText.text = "";
                if (gradientOverlay != null) gradientOverlay.color = new Color(0.5f, 0.5f, 0.55f, 0f);
                ClearTraitIcons();
                button.interactable = false;
                return;
            }

            // Set background color
            background.color = new Color(0.22f, 0.22f, 0.28f);

            // Update rarity gradient
            UpdateRarityGradient(serverUnit.cost);

            // Set sprite - prefer 3D portrait, fall back to pixel art
            Sprite unitSprite = UnitPortraitGenerator.GetPortrait(serverUnit.unitId, serverUnit.name);
            if (unitSprite != null)
            {
                spriteImage.sprite = unitSprite;
                spriteImage.enabled = true;
            }
            else
            {
                spriteImage.enabled = false;
            }

            // Set cost (just number for coin badge)
            costText.text = serverUnit.cost.ToString();
            if (starsText != null) starsText.text = ""; // Server units are 1-star by default

            // Update trait icons from server trait IDs
            if (traitIconContainer != null && serverUnit.traits != null)
            {
                PopulateTraitIconsFromIds(serverUnit.traits);
            }

            button.interactable = true;
        }

        private void UpdateRarityGradient(int cost)
        {
            if (gradientOverlay == null) return;

            Color rarityColor = GetRarityColor(cost);
            // Apply with semi-transparency for gradient effect
            gradientOverlay.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.7f);
        }

        private Color GetRarityColor(int cost)
        {
            return cost switch
            {
                1 => new Color(1f, 1f, 1f),              // White
                2 => new Color(0.35f, 0.65f, 0.35f),   // Green
                3 => new Color(0.35f, 0.55f, 0.85f),   // Blue
                4 => new Color(0.65f, 0.35f, 0.65f),   // Purple
                5 => new Color(0.95f, 0.75f, 0.25f),   // Gold
                _ => new Color(0.3f, 0.3f, 0.35f)
            };
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // On mobile/touch, hover doesn't apply - skip tooltip on hover for shop cards
            if (isShopCard) return;

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
            // Skip for shop cards (using tap/long-press instead)
            if (isShopCard) return;

            if (GameUI.Instance != null)
            {
                GameUI.Instance.HideTooltip();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isClicking = true;
            isLongPress = false;
            pointerDownTime = Time.time;

            // Hide tooltip immediately when clicking
            if (GameUI.Instance != null)
            {
                GameUI.Instance.HideTooltip();
            }

            // Start long-press detection for shop cards
            if (isShopCard && (currentUnit != null || currentServerUnit != null))
            {
                if (longPressCoroutine != null)
                {
                    StopCoroutine(longPressCoroutine);
                }
                longPressCoroutine = StartCoroutine(DetectLongPress());
            }
        }

        private IEnumerator DetectLongPress()
        {
            yield return new WaitForSeconds(LONG_PRESS_DURATION);

            // Still holding and not dragging? It's a long press - show tooltip
            if (isClicking && !isDragging)
            {
                isLongPress = true;
                Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

                // Haptic feedback on mobile
#if UNITY_IOS || UNITY_ANDROID
                Handheld.Vibrate();
#endif

                // Show unit tooltip (pinned so it stays visible)
                if (GameUI.Instance != null)
                {
                    if (currentServerUnit != null)
                    {
                        GameUI.Instance.ShowTooltipPinned(currentServerUnit);
                    }
                    else if (currentUnit != null)
                    {
                        GameUI.Instance.ShowTooltipPinned(currentUnit);
                    }
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Cancel long-press detection
            if (longPressCoroutine != null)
            {
                StopCoroutine(longPressCoroutine);
                longPressCoroutine = null;
            }

            // Handle quick tap on shop card = buy unit
            if (isShopCard && !isLongPress && !isDragging && isClicking)
            {
                float pressDuration = Time.time - pointerDownTime;
                if (pressDuration < LONG_PRESS_DURATION)
                {
                    // Quick tap - buy the unit
                    if (GameUI.Instance != null)
                    {
                        GameUI.Instance.OnShopCardClicked(index);
                    }
                }
            }

            isClicking = false;
            isLongPress = false;
            clickSuppressionTime = Time.time;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Don't handle click if we were dragging
            if (isDragging) return;

            // Shop card clicks handled in OnPointerUp for tap-to-buy
            if (isShopCard) return;

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

        private void ClearTraitIcons()
        {
            if (traitIconContainer == null) return;
            // Hide the container immediately
            traitIconContainer.gameObject.SetActive(false);
            // Destroy all child icons
            for (int i = traitIconContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(traitIconContainer.GetChild(i).gameObject);
            }
        }

        private void PopulateTraitIcons(TraitData[] traits)
        {
            ClearTraitIcons();
            if (traits == null || traits.Length == 0) return;

            // Collect valid traits
            var validTraits = new List<(string name, Color color)>();
            foreach (var trait in traits)
            {
                if (trait == null) continue;
                validTraits.Add((trait.traitName ?? trait.traitId, GetTraitColor(trait.traitId)));
            }

            LayoutTraitIcons(validTraits);
        }

        private void PopulateTraitIconsFromIds(string[] traitIds)
        {
            ClearTraitIcons();
            if (traitIds == null || traitIds.Length == 0) return;

            var validTraits = new List<(string name, Color color)>();
            foreach (var traitId in traitIds)
            {
                if (string.IsNullOrEmpty(traitId)) continue;
                validTraits.Add((FormatTraitName(traitId), GetTraitColor(traitId)));
            }

            LayoutTraitIcons(validTraits);
        }

        private void LayoutTraitIcons(List<(string name, Color color)> traits)
        {
            if (traits.Count == 0 || traitIconContainer == null) return;

            float containerWidth = traitIconContainer.rect.width;
            float containerHeight = traitIconContainer.rect.height;
            float iconSize = 36f;
            int count = traits.Count;

            // If container width isn't ready yet, use parent card width as fallback
            if (containerWidth <= 0 && traitIconContainer.parent is RectTransform parentRT)
                containerWidth = parentRT.rect.width;

            // Equidistant spacing: divide container into (count+1) equal gaps
            float totalIconWidth = count * iconSize;
            float totalSpacing = containerWidth - totalIconWidth;
            float gap = totalSpacing / (count + 1);

            for (int i = 0; i < count; i++)
            {
                var icon = CreateTraitIcon(traits[i].name, traits[i].color);
                RectTransform rt = icon.GetComponent<RectTransform>();
                // Position each icon: gap + i * (iconSize + gap) from left edge
                float x = gap + i * (iconSize + gap) + iconSize * 0.5f;
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(0, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(x, 0);
            }

            traitIconContainer.gameObject.SetActive(true);
        }

        private GameObject CreateTraitIcon(string traitName, Color color)
        {
            float iconSize = 36f;

            GameObject iconObj = new GameObject($"Trait_{traitName}");
            iconObj.transform.SetParent(traitIconContainer, false);
            RectTransform rt = iconObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            // Colored circle background
            Image bg = iconObj.AddComponent<Image>();
            bg.sprite = GetTraitIconSprite();
            bg.color = color;
            bg.raycastTarget = false;

            // Abbreviation text (first 2 chars)
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(iconObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            Text label = textObj.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = traitName.Length >= 2 ? traitName.Substring(0, 2).ToUpper() : traitName.ToUpper();
            label.fontSize = 14;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            // Shadow for readability
            Shadow shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(1, -1);

            return iconObj;
        }

        private static string FormatTraitName(string traitId)
        {
            if (string.IsNullOrEmpty(traitId)) return "";
            // Capitalize first letter
            return char.ToUpper(traitId[0]) + traitId.Substring(1);
        }

        // Trait color mapping
        private static Color GetTraitColor(string traitId)
        {
            if (string.IsNullOrEmpty(traitId)) return Color.gray;

            string id = traitId.ToLower();
            return id switch
            {
                "attuned"      => new Color(0.4f, 0.7f, 0.9f),  // Cyan
                "forged"       => new Color(0.85f, 0.5f, 0.2f),  // Orange
                "scavenger"    => new Color(0.6f, 0.5f, 0.3f),   // Brown
                "invigorating" => new Color(0.3f, 0.8f, 0.4f),   // Green
                "reflective"   => new Color(0.7f, 0.7f, 0.85f),  // Silver
                "mitigation"   => new Color(0.5f, 0.5f, 0.7f),   // Steel blue
                "bruiser"      => new Color(0.8f, 0.3f, 0.3f),   // Red
                "overkill"     => new Color(0.9f, 0.2f, 0.2f),   // Dark red
                "gigamega"     => new Color(0.6f, 0.3f, 0.8f),   // Purple
                "firstblood"   => new Color(0.9f, 0.3f, 0.4f),   // Crimson
                "momentum"     => new Color(0.2f, 0.7f, 0.7f),   // Teal
                "cleave"       => new Color(0.7f, 0.4f, 0.2f),   // Copper
                "fury"         => new Color(0.9f, 0.4f, 0.1f),   // Flame
                "volatile"     => new Color(0.8f, 0.8f, 0.2f),   // Yellow
                "treasure"     => new Color(0.95f, 0.75f, 0.25f), // Gold
                "crestmaker"   => new Color(0.3f, 0.5f, 0.9f),   // Royal blue
                _              => new Color(0.5f, 0.5f, 0.55f)
            };
        }

        // Cached circle sprite for trait icons
        private static Sprite _traitIconSprite;
        private static Sprite GetTraitIconSprite()
        {
            if (_traitIconSprite != null) return _traitIconSprite;

            int size = 32;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            float center = size / 2f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius)
                    {
                        float edge = Mathf.Clamp01((radius - dist) * 2f); // AA
                        pixels[y * size + x] = new Color(1f, 1f, 1f, edge);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _traitIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _traitIconSprite;
        }
    }
}
