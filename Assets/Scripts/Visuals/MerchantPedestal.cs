using UnityEngine;
using Crestforge.Networking;

namespace Crestforge.Visuals
{
    /// <summary>
    /// A single clickable pedestal in the Mad Merchant area.
    /// Displays an item, crest token, or gold pile that players can pick.
    /// </summary>
    public class MerchantPedestal : MonoBehaviour
    {
        [Header("Option Data")]
        public string optionId;
        public string optionType; // "item", "crest_token", "gold"
        public bool isPicked;
        public string pickedByName;

        [Header("Visual Settings")]
        public float pedestalHeight = 0.15f;
        public float pedestalRadius = 0.4f;
        public float itemHeight = 0.5f;
        public float rotationSpeed = 30f;
        public float bobSpeed = 2f;
        public float bobAmount = 0.05f;

        [Header("Colors")]
        public Color availableColor = new Color(1f, 0.85f, 0.4f); // Gold
        public Color yourTurnColor = new Color(0.4f, 1f, 0.5f);   // Green
        public Color pickedColor = new Color(0.4f, 0.4f, 0.4f);   // Gray
        public Color crestTokenColor = new Color(0.8f, 0.6f, 1f); // Purple
        public Color goldColor = new Color(1f, 0.8f, 0.3f);       // Gold

        // Visual components
        private GameObject baseObj;
        private GameObject itemDisplay;
        private Light glowLight;
        private TextMesh nameLabel;
        private TextMesh pickerLabel;

        // State
        private bool isMyTurn;
        private float bobTimer;
        private float itemBaseY;
        private MerchantArea3D merchantArea;
        private MeshRenderer itemRenderer;

        /// <summary>
        /// Initialize the pedestal with option data
        /// </summary>
        public void Initialize(MerchantOptionData option, MerchantArea3D area)
        {
            optionId = option.optionId;
            optionType = option.optionType;
            isPicked = option.isPicked;
            pickedByName = option.pickedByName;
            merchantArea = area;

            CreateVisuals(option);

            if (isPicked)
            {
                SetPicked(pickedByName);
            }
        }

        private void CreateVisuals(MerchantOptionData option)
        {
            // Create hexagonal base pedestal
            baseObj = CreateHexPedestal();
            baseObj.transform.SetParent(transform);
            baseObj.transform.localPosition = Vector3.zero;

            // Create floating item display based on type
            itemDisplay = CreateItemDisplay(option);
            itemDisplay.transform.SetParent(transform);
            itemDisplay.transform.localPosition = new Vector3(0, pedestalHeight + itemHeight, 0);
            itemBaseY = pedestalHeight + itemHeight;
            itemRenderer = itemDisplay.GetComponent<MeshRenderer>();

            // Create glow light
            GameObject lightObj = new GameObject("GlowLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0, pedestalHeight + 0.3f, 0);
            glowLight = lightObj.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = GetDisplayColor();
            glowLight.intensity = 0.8f;
            glowLight.range = 1.5f;

            // Create name label (above item) - smaller text
            nameLabel = CreateWorldSpaceText(option.name, 0.03f);
            nameLabel.transform.SetParent(transform);
            nameLabel.transform.localPosition = new Vector3(0, pedestalHeight + itemHeight + 0.35f, 0);
            nameLabel.color = Color.white;

            // Create picker label (below pedestal, hidden initially)
            pickerLabel = CreateWorldSpaceText("", 0.025f);
            pickerLabel.transform.SetParent(transform);
            pickerLabel.transform.localPosition = new Vector3(0, -0.15f, 0);
            pickerLabel.color = new Color(0.7f, 0.7f, 0.7f);
            pickerLabel.gameObject.SetActive(false);

            // Add collider for clicking
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, pedestalHeight / 2 + itemHeight / 2, 0);
            collider.size = new Vector3(pedestalRadius * 2, pedestalHeight + itemHeight * 1.5f, pedestalRadius * 2);
        }

        private GameObject CreateHexPedestal()
        {
            // Create cylinder as pedestal base
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "PedestalBase";
            pedestal.transform.localScale = new Vector3(pedestalRadius * 2, pedestalHeight / 2, pedestalRadius * 2);
            pedestal.transform.localPosition = new Vector3(0, pedestalHeight / 2, 0);

            // Remove default collider (we add our own)
            Destroy(pedestal.GetComponent<Collider>());

            // Create stone-like material
            var renderer = pedestal.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.45f, 0.42f, 0.38f); // Stone gray
            renderer.material = mat;

            return pedestal;
        }

        private GameObject CreateItemDisplay(MerchantOptionData option)
        {
            GameObject display;
            Color displayColor = GetItemColor(option);

            if (option.optionType == "gold")
            {
                // Gold pile - stack of small cylinders
                display = new GameObject("GoldPile");
                for (int i = 0; i < 3; i++)
                {
                    var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    coin.transform.SetParent(display.transform);
                    coin.transform.localPosition = new Vector3(
                        (i - 1) * 0.08f,
                        i * 0.03f,
                        (i % 2) * 0.04f
                    );
                    coin.transform.localScale = new Vector3(0.15f, 0.02f, 0.15f);
                    Destroy(coin.GetComponent<Collider>());

                    var coinRenderer = coin.GetComponent<MeshRenderer>();
                    Material coinMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    coinMat.color = goldColor;
                    coinMat.SetColor("_EmissionColor", goldColor * 0.3f);
                    coinMat.EnableKeyword("_EMISSION");
                    coinRenderer.material = coinMat;
                }

                // Add a sphere renderer component to the parent for color changes
                var mainSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mainSphere.transform.SetParent(display.transform);
                mainSphere.transform.localPosition = Vector3.up * 0.1f;
                mainSphere.transform.localScale = Vector3.one * 0.2f;
                Destroy(mainSphere.GetComponent<Collider>());
                var mainRenderer = mainSphere.GetComponent<MeshRenderer>();
                Material mainMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mainMat.color = goldColor;
                mainMat.SetColor("_EmissionColor", goldColor * 0.5f);
                mainMat.EnableKeyword("_EMISSION");
                mainRenderer.material = mainMat;

                display.AddComponent<MeshFilter>();
                var meshRenderer = display.AddComponent<MeshRenderer>();
                meshRenderer.material = mainMat;
            }
            else
            {
                // Item or crest token - floating sphere/orb
                display = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                display.name = option.optionType == "crest_token" ? "CrestOrb" : "ItemOrb";
                display.transform.localScale = Vector3.one * 0.25f;
                Destroy(display.GetComponent<Collider>());

                var renderer = display.GetComponent<MeshRenderer>();
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = displayColor;
                mat.SetColor("_EmissionColor", displayColor * 0.5f);
                mat.EnableKeyword("_EMISSION");
                renderer.material = mat;
            }

            return display;
        }

        private Color GetItemColor(MerchantOptionData option)
        {
            if (option.optionType == "crest_token")
                return crestTokenColor;
            if (option.optionType == "gold")
                return goldColor;

            // Items - color by rarity
            switch (option.rarity)
            {
                case "common": return new Color(0.6f, 0.6f, 0.6f);
                case "uncommon": return new Color(0.4f, 0.8f, 0.4f);
                case "rare": return new Color(0.4f, 0.6f, 1f);
                case "epic": return new Color(0.8f, 0.4f, 1f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        private Color GetDisplayColor()
        {
            if (isPicked) return pickedColor;
            if (isMyTurn) return yourTurnColor;
            return availableColor;
        }

        private TextMesh CreateWorldSpaceText(string text, float size)
        {
            var textObj = new GameObject("Label");
            var textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.fontSize = 100;
            textMesh.characterSize = size;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;

            // Make it face the camera (billboard) - add BillboardUI component if available
            var billboard = textObj.AddComponent<BillboardUI>();

            return textMesh;
        }

        private void Update()
        {
            if (isPicked) return;

            // Rotate item
            if (itemDisplay != null)
            {
                itemDisplay.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

                // Bob up and down
                bobTimer += Time.deltaTime * bobSpeed;
                float yOffset = Mathf.Sin(bobTimer) * bobAmount;
                itemDisplay.transform.localPosition = new Vector3(0, itemBaseY + yOffset, 0);
            }

            // Pulse light
            if (glowLight != null)
            {
                float pulse = 0.7f + Mathf.Sin(Time.time * 3f) * 0.3f;
                glowLight.intensity = isMyTurn ? pulse * 1.2f : pulse * 0.8f;
            }
        }

        /// <summary>
        /// Set whether it's the local player's turn to pick
        /// </summary>
        public void SetHighlighted(bool myTurn)
        {
            isMyTurn = myTurn;

            if (isPicked) return;

            Color targetColor = myTurn ? yourTurnColor : availableColor;

            // Update glow light
            if (glowLight != null)
            {
                glowLight.color = targetColor;
            }

            // Update item material emission
            if (itemRenderer != null && itemRenderer.material != null)
            {
                itemRenderer.material.SetColor("_EmissionColor", targetColor * 0.5f);
            }
        }

        /// <summary>
        /// Mark this option as picked by a player
        /// </summary>
        public void SetPicked(string playerName)
        {
            isPicked = true;
            pickedByName = playerName;

            // Gray out the item
            if (itemRenderer != null && itemRenderer.material != null)
            {
                itemRenderer.material.color = pickedColor;
                itemRenderer.material.SetColor("_EmissionColor", Color.black);
            }

            // Dim the light
            if (glowLight != null)
            {
                glowLight.color = pickedColor;
                glowLight.intensity = 0.2f;
            }

            // Show picker label
            if (pickerLabel != null)
            {
                pickerLabel.text = playerName;
                pickerLabel.gameObject.SetActive(true);
            }

            // Dim name label
            if (nameLabel != null)
            {
                nameLabel.color = new Color(0.5f, 0.5f, 0.5f);
            }

            // Stop rotation
            if (itemDisplay != null)
            {
                itemDisplay.transform.localScale *= 0.7f;
            }
        }

        private void OnMouseDown()
        {
            Debug.Log($"[MerchantPedestal] OnMouseDown called for {optionId}, isPicked={isPicked}, isMyTurn={isMyTurn}");

            if (isPicked)
            {
                Debug.Log("[MerchantPedestal] Already picked");
                return;
            }
            if (!isMyTurn)
            {
                Debug.Log("[MerchantPedestal] Not your turn to pick");
                return;
            }

            // Send pick to server
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SendMerchantPick(optionId);
                Debug.Log($"[MerchantPedestal] Sent pick for option: {optionId}");
            }
            else
            {
                Debug.LogError("[MerchantPedestal] NetworkManager.Instance is null!");
            }
        }

        private void OnMouseEnter()
        {
            if (isPicked) return;

            // Highlight effect on hover
            if (itemDisplay != null)
            {
                itemDisplay.transform.localScale *= 1.1f;
            }
        }

        private void OnMouseExit()
        {
            if (isPicked) return;

            // Remove highlight
            if (itemDisplay != null)
            {
                itemDisplay.transform.localScale /= 1.1f;
            }
        }
    }
}
