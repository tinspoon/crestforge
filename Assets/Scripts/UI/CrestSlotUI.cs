using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Systems;
using Crestforge.Networking;

namespace Crestforge.UI
{
    /// <summary>
    /// UI slot for displaying an active crest
    /// </summary>
    public class CrestSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("References")]
        public Image backgroundImage;
        public Image iconImage;
        public Image borderImage;
        public Text nameText;
        public Text typeText;

        [Header("Colors")]
        public Color emptyColor = new Color(0.2f, 0.2f, 0.25f, 0.6f);
        public Color minorCrestColor = new Color(0.6f, 0.4f, 0.8f);    // Purple
        public Color majorCrestColor = new Color(1f, 0.7f, 0.2f);      // Gold

        // Runtime
        private CrestData crest;
        private ServerCrestData serverCrest;
        private bool isTooltipPinned = false;

        public void SetCrest(CrestData crestData)
        {
            crest = crestData;

            if (crest != null)
            {
                Color crestColor = crest.type == CrestType.Minor ? minorCrestColor : majorCrestColor;

                if (iconImage != null)
                {
                    iconImage.sprite = GetCrestIcon(crest.type);
                    iconImage.enabled = true;
                    iconImage.color = Color.white;
                }

                if (nameText != null)
                {
                    nameText.text = crest.crestName;
                    nameText.color = crestColor;
                }

                if (typeText != null)
                {
                    typeText.text = crest.type == CrestType.Minor ? "Minor" : "Major";
                    typeText.color = new Color(crestColor.r * 0.8f, crestColor.g * 0.8f, crestColor.b * 0.8f);
                }

                if (borderImage != null)
                {
                    borderImage.color = crestColor;
                }

                if (backgroundImage != null)
                {
                    backgroundImage.color = new Color(crestColor.r * 0.2f, crestColor.g * 0.2f, crestColor.b * 0.2f, 0.85f);
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
                    nameText.text = "No Crest";
                    nameText.color = new Color(0.5f, 0.5f, 0.5f);
                }

                if (typeText != null)
                {
                    typeText.text = "";
                }

                if (borderImage != null)
                {
                    borderImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                }

                if (backgroundImage != null)
                {
                    backgroundImage.color = emptyColor;
                }
            }
        }

        public CrestData GetCrest()
        {
            return crest;
        }

        /// <summary>
        /// Set crest from server data (multiplayer mode)
        /// </summary>
        public void SetServerCrest(ServerCrestData serverCrestData)
        {
            serverCrest = serverCrestData;
            crest = null; // Clear single-player crest

            if (serverCrestData != null)
            {
                bool isMinor = serverCrestData.type == "minor";
                Color crestColor = isMinor ? minorCrestColor : majorCrestColor;

                if (iconImage != null)
                {
                    iconImage.sprite = isMinor ? CrestIcons.GetMinorCrestIcon() : CrestIcons.GetMajorCrestIcon();
                    iconImage.enabled = true;
                    iconImage.color = Color.white;
                }

                if (nameText != null)
                {
                    nameText.text = serverCrestData.name ?? serverCrestData.crestId;
                    nameText.color = crestColor;
                }

                if (typeText != null)
                {
                    typeText.text = isMinor ? "Minor" : "Major";
                    typeText.color = new Color(crestColor.r * 0.8f, crestColor.g * 0.8f, crestColor.b * 0.8f);
                }

                if (borderImage != null)
                {
                    borderImage.color = crestColor;
                }

                if (backgroundImage != null)
                {
                    backgroundImage.color = new Color(crestColor.r * 0.2f, crestColor.g * 0.2f, crestColor.b * 0.2f, 0.85f);
                }
            }
            else
            {
                // No crest
                if (iconImage != null)
                {
                    iconImage.enabled = false;
                }

                if (nameText != null)
                {
                    nameText.text = "No Crest";
                    nameText.color = new Color(0.5f, 0.5f, 0.5f);
                }

                if (typeText != null)
                {
                    typeText.text = "";
                }

                if (borderImage != null)
                {
                    borderImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                }

                if (backgroundImage != null)
                {
                    backgroundImage.color = emptyColor;
                }
            }
        }

        public ServerCrestData GetServerCrest()
        {
            return serverCrest;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (crest != null)
            {
                GameUI.Instance?.ShowCrestTooltip(crest);
            }
            else if (serverCrest != null)
            {
                GameUI.Instance?.ShowServerCrestTooltip(serverCrest);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isTooltipPinned)
            {
                GameUI.Instance?.HideCrestTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            bool hasCrest = crest != null || serverCrest != null;
            if (!hasCrest) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Toggle tooltip pin
                isTooltipPinned = !isTooltipPinned;
                if (isTooltipPinned)
                {
                    if (crest != null)
                    {
                        GameUI.Instance?.ShowCrestTooltipPinned(crest);
                    }
                    else if (serverCrest != null)
                    {
                        GameUI.Instance?.ShowServerCrestTooltipPinned(serverCrest);
                    }
                }
                else
                {
                    GameUI.Instance?.HideCrestTooltip();
                }
            }
        }

        private Sprite GetCrestIcon(CrestType type)
        {
            return type == CrestType.Minor ?
                CrestIcons.GetMinorCrestIcon() :
                CrestIcons.GetMajorCrestIcon();
        }

        /// <summary>
        /// Create a crest slot UI element
        /// </summary>
        public static CrestSlotUI Create(Transform parent, Vector2 size, string label = null)
        {
            GameObject slotObj = new GameObject("CrestSlot");
            slotObj.transform.SetParent(parent, false);

            RectTransform rt = slotObj.AddComponent<RectTransform>();
            rt.sizeDelta = size;

            // Background
            Image bg = slotObj.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.25f, 0.6f);

            // Border
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(slotObj.transform, false);
            RectTransform borderRT = borderObj.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = Vector2.zero;
            borderRT.offsetMin = new Vector2(-2, -2);
            borderRT.offsetMax = new Vector2(2, 2);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            borderObj.transform.SetAsFirstSibling();

            // Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.05f, 0.3f);
            iconRT.anchorMax = new Vector2(0.35f, 0.95f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.enabled = false;
            iconImg.preserveAspect = true;

            // Name text
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(slotObj.transform, false);
            RectTransform nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.38f, 0.5f);
            nameRT.anchorMax = new Vector2(0.98f, 0.95f);
            nameRT.offsetMin = Vector2.zero;
            nameRT.offsetMax = Vector2.zero;
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 11;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = Color.white;
            nameText.text = "No Crest";

            // Type text
            GameObject typeObj = new GameObject("Type");
            typeObj.transform.SetParent(slotObj.transform, false);
            RectTransform typeRT = typeObj.AddComponent<RectTransform>();
            typeRT.anchorMin = new Vector2(0.38f, 0.05f);
            typeRT.anchorMax = new Vector2(0.98f, 0.5f);
            typeRT.offsetMin = Vector2.zero;
            typeRT.offsetMax = Vector2.zero;
            Text typeText = typeObj.AddComponent<Text>();
            typeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            typeText.fontSize = 9;
            typeText.alignment = TextAnchor.MiddleLeft;
            typeText.color = new Color(0.6f, 0.6f, 0.6f);

            // Label (optional, e.g., "Minor Crest")
            if (!string.IsNullOrEmpty(label))
            {
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(slotObj.transform, false);
                RectTransform labelRT = labelObj.AddComponent<RectTransform>();
                labelRT.anchorMin = new Vector2(0, 1);
                labelRT.anchorMax = new Vector2(1, 1);
                labelRT.pivot = new Vector2(0.5f, 0);
                labelRT.sizeDelta = new Vector2(0, 14);
                labelRT.anchoredPosition = new Vector2(0, 2);
                Text labelText = labelObj.AddComponent<Text>();
                labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelText.fontSize = 9;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.color = new Color(0.7f, 0.7f, 0.7f);
                labelText.text = label;
            }

            // Add component
            CrestSlotUI slot = slotObj.AddComponent<CrestSlotUI>();
            slot.backgroundImage = bg;
            slot.iconImage = iconImg;
            slot.borderImage = borderImg;
            slot.nameText = nameText;
            slot.typeText = typeText;

            return slot;
        }
    }
}
