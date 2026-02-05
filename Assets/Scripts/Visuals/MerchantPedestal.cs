using UnityEngine;
using Crestforge.Networking;

namespace Crestforge.Visuals
{
    /// <summary>
    /// A single clickable pedestal in the Mad Merchant area.
    /// Now displays paired rewards (two items per pedestal).
    /// </summary>
    public class MerchantPedestal : MonoBehaviour
    {
        [Header("Option Data")]
        public string optionId;
        public string pairType; // "unit_item", "crest_rerolls", "gold_item", etc.
        public MerchantRewardData rewardA;
        public MerchantRewardData rewardB;
        public bool isPicked;
        public string pickedByName;

        [Header("Visual Settings")]
        public float pedestalHeight = 0.15f;
        public float pedestalRadius = 0.5f;
        public float itemHeight = 0.5f;
        public float rotationSpeed = 30f;
        public float bobSpeed = 2f;
        public float bobAmount = 0.05f;
        public float itemSpacing = 0.3f; // Horizontal spacing between paired items

        [Header("Colors")]
        public Color availableColor = new Color(1f, 0.85f, 0.4f); // Gold
        public Color yourTurnColor = new Color(0.4f, 1f, 0.5f);   // Green
        public Color pickedColor = new Color(0.4f, 0.4f, 0.4f);   // Gray
        public Color crestColor = new Color(0.8f, 0.6f, 1f);      // Purple
        public Color goldColor = new Color(1f, 0.8f, 0.3f);       // Gold
        public Color unitColor = new Color(0.4f, 0.7f, 1f);       // Blue
        public Color itemColor = new Color(0.5f, 0.8f, 0.5f);     // Green
        public Color rerollColor = new Color(0.3f, 0.8f, 0.9f);   // Cyan

        // Visual components
        private GameObject baseObj;
        private GameObject itemDisplayA;
        private GameObject itemDisplayB;
        private Light glowLight;
        private TextMesh nameLabel;
        private TextMesh pickerLabel;

        // State
        private bool isMyTurn;
        private float bobTimer;
        private float itemBaseY;
        private MerchantArea3D merchantArea;
        private MeshRenderer itemRendererA;
        private MeshRenderer itemRendererB;

        /// <summary>
        /// Initialize the pedestal with paired option data
        /// </summary>
        public void Initialize(MerchantOptionData option, MerchantArea3D area)
        {
            optionId = option.optionId;
            pairType = option.pairType;
            rewardA = option.rewardA;
            rewardB = option.rewardB;
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
            // Create hexagonal base pedestal (slightly wider for paired items)
            baseObj = CreateHexPedestal();
            baseObj.transform.SetParent(transform);
            baseObj.transform.localPosition = Vector3.zero;

            // Create two floating item displays for the pair
            itemDisplayA = CreateRewardDisplay(option.rewardA);
            itemDisplayA.transform.SetParent(transform);
            itemDisplayA.transform.localPosition = new Vector3(-itemSpacing, pedestalHeight + itemHeight, 0);
            itemRendererA = itemDisplayA.GetComponent<MeshRenderer>();

            itemDisplayB = CreateRewardDisplay(option.rewardB);
            itemDisplayB.transform.SetParent(transform);
            itemDisplayB.transform.localPosition = new Vector3(itemSpacing, pedestalHeight + itemHeight, 0);
            itemRendererB = itemDisplayB.GetComponent<MeshRenderer>();

            itemBaseY = pedestalHeight + itemHeight;

            // Create glow light
            GameObject lightObj = new GameObject("GlowLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0, pedestalHeight + 0.3f, 0);
            glowLight = lightObj.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = GetDisplayColor();
            glowLight.intensity = 0.8f;
            glowLight.range = 2f;

            // Create name label showing both rewards
            string pairName = GetPairDisplayName(option);
            nameLabel = CreateWorldSpaceText(pairName, 0.025f);
            nameLabel.transform.SetParent(transform);
            nameLabel.transform.localPosition = new Vector3(0, pedestalHeight + itemHeight + 0.4f, 0);
            nameLabel.color = Color.white;

            // Create picker label (below pedestal, hidden initially)
            pickerLabel = CreateWorldSpaceText("", 0.025f);
            pickerLabel.transform.SetParent(transform);
            pickerLabel.transform.localPosition = new Vector3(0, -0.15f, 0);
            pickerLabel.color = new Color(0.7f, 0.7f, 0.7f);
            pickerLabel.gameObject.SetActive(false);

            // Add collider for clicking (wider for paired display)
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, pedestalHeight / 2 + itemHeight / 2, 0);
            collider.size = new Vector3(pedestalRadius * 2.5f, pedestalHeight + itemHeight * 1.5f, pedestalRadius * 2);
        }

        private string GetPairDisplayName(MerchantOptionData option)
        {
            string nameA = option.rewardA?.name ?? "?";
            string nameB = option.rewardB?.name ?? "?";
            return $"{nameA}\n+\n{nameB}";
        }

        private GameObject CreateRewardDisplay(MerchantRewardData reward)
        {
            if (reward == null)
            {
                return CreateDefaultOrb(Color.gray);
            }

            Color displayColor = GetRewardColor(reward);

            switch (reward.type)
            {
                case "gold":
                    return CreateGoldDisplay(displayColor);
                case "unit":
                    return CreateUnitDisplay(displayColor, reward.name);
                case "item":
                    return CreateItemOrb(displayColor);
                case "crest":
                    return CreateCrestOrb(displayColor);
                case "rerolls":
                    return CreateRerollOrb(displayColor);
                default:
                    return CreateDefaultOrb(displayColor);
            }
        }

        private Color GetRewardColor(MerchantRewardData reward)
        {
            if (reward == null) return Color.gray;

            switch (reward.type)
            {
                case "gold": return goldColor;
                case "unit": return unitColor;
                case "crest": return crestColor;
                case "rerolls": return rerollColor;
                case "item":
                    // Color by rarity
                    switch (reward.rarity)
                    {
                        case "common": return new Color(0.6f, 0.6f, 0.6f);
                        case "uncommon": return new Color(0.4f, 0.8f, 0.4f);
                        case "rare": return new Color(0.4f, 0.6f, 1f);
                        case "epic": return new Color(0.8f, 0.4f, 1f);
                        default: return itemColor;
                    }
                default: return Color.white;
            }
        }

        private GameObject CreateGoldDisplay(Color color)
        {
            var display = new GameObject("GoldPile");
            for (int i = 0; i < 2; i++)
            {
                var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coin.transform.SetParent(display.transform);
                coin.transform.localPosition = new Vector3((i - 0.5f) * 0.06f, i * 0.02f, 0);
                coin.transform.localScale = new Vector3(0.1f, 0.015f, 0.1f);
                Destroy(coin.GetComponent<Collider>());

                var coinRenderer = coin.GetComponent<MeshRenderer>();
                Material coinMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                coinMat.color = color;
                coinMat.SetColor("_EmissionColor", color * 0.3f);
                coinMat.EnableKeyword("_EMISSION");
                coinRenderer.material = coinMat;
            }

            // Main renderer for color changes
            var mainRenderer = display.AddComponent<MeshRenderer>();
            Material mainMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mainMat.color = color;
            mainRenderer.material = mainMat;

            return display;
        }

        private GameObject CreateUnitDisplay(Color color, string unitName)
        {
            // Unit - cube to differentiate from items
            var display = GameObject.CreatePrimitive(PrimitiveType.Cube);
            display.name = "UnitCube";
            display.transform.localScale = Vector3.one * 0.15f;
            Destroy(display.GetComponent<Collider>());

            var renderer = display.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetColor("_EmissionColor", color * 0.4f);
            mat.EnableKeyword("_EMISSION");
            renderer.material = mat;

            return display;
        }

        private GameObject CreateItemOrb(Color color)
        {
            var display = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            display.name = "ItemOrb";
            display.transform.localScale = Vector3.one * 0.15f;
            Destroy(display.GetComponent<Collider>());

            var renderer = display.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetColor("_EmissionColor", color * 0.4f);
            mat.EnableKeyword("_EMISSION");
            renderer.material = mat;

            return display;
        }

        private GameObject CreateCrestOrb(Color color)
        {
            // Crest - diamond/octahedron shape (scaled sphere with rotation)
            var display = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            display.name = "CrestOrb";
            display.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
            display.transform.localRotation = Quaternion.Euler(45, 0, 0);
            Destroy(display.GetComponent<Collider>());

            var renderer = display.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetColor("_EmissionColor", color * 0.5f);
            mat.EnableKeyword("_EMISSION");
            renderer.material = mat;

            return display;
        }

        private GameObject CreateRerollOrb(Color color)
        {
            // Reroll - spinning disc shape
            var display = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            display.name = "RerollDisc";
            display.transform.localScale = new Vector3(0.15f, 0.03f, 0.15f);
            Destroy(display.GetComponent<Collider>());

            var renderer = display.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetColor("_EmissionColor", color * 0.5f);
            mat.EnableKeyword("_EMISSION");
            renderer.material = mat;

            return display;
        }

        private GameObject CreateDefaultOrb(Color color)
        {
            var display = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            display.name = "DefaultOrb";
            display.transform.localScale = Vector3.one * 0.12f;
            Destroy(display.GetComponent<Collider>());

            var renderer = display.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.material = mat;

            return display;
        }

        private GameObject CreateHexPedestal()
        {
            // Create cylinder as pedestal base (slightly wider for paired items)
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "PedestalBase";
            pedestal.transform.localScale = new Vector3(pedestalRadius * 2.2f, pedestalHeight / 2, pedestalRadius * 2);
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

            // Bob up and down (offset between A and B for visual interest)
            bobTimer += Time.deltaTime * bobSpeed;
            float yOffsetA = Mathf.Sin(bobTimer) * bobAmount;
            float yOffsetB = Mathf.Sin(bobTimer + Mathf.PI * 0.5f) * bobAmount;

            // Rotate and bob item A
            if (itemDisplayA != null)
            {
                itemDisplayA.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
                itemDisplayA.transform.localPosition = new Vector3(-itemSpacing, itemBaseY + yOffsetA, 0);
            }

            // Rotate and bob item B
            if (itemDisplayB != null)
            {
                itemDisplayB.transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime); // Opposite direction
                itemDisplayB.transform.localPosition = new Vector3(itemSpacing, itemBaseY + yOffsetB, 0);
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

            // Update item materials emission
            if (itemRendererA != null && itemRendererA.material != null)
            {
                itemRendererA.material.SetColor("_EmissionColor", targetColor * 0.5f);
            }
            if (itemRendererB != null && itemRendererB.material != null)
            {
                itemRendererB.material.SetColor("_EmissionColor", targetColor * 0.5f);
            }
        }

        /// <summary>
        /// Mark this option as picked by a player
        /// </summary>
        public void SetPicked(string playerName)
        {
            isPicked = true;
            pickedByName = playerName;

            // Gray out both items
            if (itemRendererA != null && itemRendererA.material != null)
            {
                itemRendererA.material.color = pickedColor;
                itemRendererA.material.SetColor("_EmissionColor", Color.black);
            }
            if (itemRendererB != null && itemRendererB.material != null)
            {
                itemRendererB.material.color = pickedColor;
                itemRendererB.material.SetColor("_EmissionColor", Color.black);
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

            // Shrink items
            if (itemDisplayA != null)
            {
                itemDisplayA.transform.localScale *= 0.7f;
            }
            if (itemDisplayB != null)
            {
                itemDisplayB.transform.localScale *= 0.7f;
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
            if (itemDisplayA != null)
            {
                itemDisplayA.transform.localScale *= 1.1f;
            }
            if (itemDisplayB != null)
            {
                itemDisplayB.transform.localScale *= 1.1f;
            }
        }

        private void OnMouseExit()
        {
            if (isPicked) return;

            // Remove highlight
            if (itemDisplayA != null)
            {
                itemDisplayA.transform.localScale /= 1.1f;
            }
            if (itemDisplayB != null)
            {
                itemDisplayB.transform.localScale /= 1.1f;
            }
        }
    }
}
