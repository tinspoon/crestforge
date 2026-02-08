using UnityEngine;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Networking;
using Crestforge.Systems;
using Crestforge.UI;

namespace Crestforge.Visuals
{
    /// <summary>
    /// A clickable loot orb that drops from PvE enemies.
    /// Clicking it adds a consumable item to the player's inventory.
    /// </summary>
    public class LootOrb : MonoBehaviour
    {
        [Header("Settings")]
        public LootType lootType;
        public float dropSpeed = 5f;
        public float bounceHeight = 0.3f;
        public float bounceSpeed = 2f;
        public float glowIntensity = 1.5f;

        [Header("Colors")]
        public Color crestTokenColor = new Color(0.8f, 0.6f, 1f);   // Purple for crest
        public Color itemAnvilColor = new Color(1f, 0.8f, 0.3f);    // Gold for item
        public Color mixedLootColor = new Color(0.3f, 1f, 0.8f);    // Cyan/teal for mystery loot
        public Color largeMixedLootColor = new Color(1f, 0.5f, 0.2f); // Orange for boss loot

        // Runtime
        private Vector3 targetPosition;
        private bool isDropping = true;
        private bool isCollected = false;
        private float bounceTimer = 0f;
        private float baseY;
        private MeshRenderer meshRenderer;
        private Light orbLight;

        // Multiplayer support
        private bool isMultiplayer = false;
        private string lootId;

        public static LootOrb Create(Vector3 spawnPosition, LootType type)
        {
            GameObject orbObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orbObj.name = $"LootOrb_{type}";
            orbObj.transform.position = spawnPosition + Vector3.up * 0.5f;
            orbObj.transform.localScale = Vector3.one * 0.5f;

            // Add collider for clicking
            var collider = orbObj.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            // Add LootOrb component
            var orb = orbObj.AddComponent<LootOrb>();
            orb.lootType = type;
            orb.targetPosition = spawnPosition + Vector3.up * 0.2f;

            return orb;
        }

        /// <summary>
        /// Create a loot orb for multiplayer mode (sends CollectLoot action to server)
        /// </summary>
        public static LootOrb CreateMultiplayer(Vector3 spawnPosition, string lootTypeStr, string lootId)
        {
            // Parse loot type from string
            LootType type = LootType.None;
            if (lootTypeStr == "crest_token")
                type = LootType.CrestToken;
            else if (lootTypeStr == "item_anvil")
                type = LootType.ItemAnvil;
            else if (lootTypeStr == "mixed_loot")
                type = LootType.MixedLoot;
            else if (lootTypeStr == "large_mixed_loot")
                type = LootType.LargeMixedLoot;

            if (type == LootType.None)
            {
                Debug.LogWarning($"[LootOrb] Unknown loot type: {lootTypeStr}");
                return null;
            }

            var orb = Create(spawnPosition, type);
            orb.isMultiplayer = true;
            orb.lootId = lootId;
            return orb;
        }

        private void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();

            // Set color based on loot type
            Color orbColor = lootType switch
            {
                LootType.CrestToken => crestTokenColor,
                LootType.MixedLoot => mixedLootColor,
                LootType.LargeMixedLoot => largeMixedLootColor,
                _ => itemAnvilColor
            };

            // Create emissive material
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = orbColor;
            mat.SetColor("_EmissionColor", orbColor * glowIntensity);
            mat.EnableKeyword("_EMISSION");
            meshRenderer.material = mat;

            // Add point light for glow effect
            GameObject lightObj = new GameObject("OrbLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            orbLight = lightObj.AddComponent<Light>();
            orbLight.type = LightType.Point;
            orbLight.color = orbColor;
            orbLight.intensity = 0.5f;
            orbLight.range = 1f;

            baseY = targetPosition.y;
        }

        private void Update()
        {
            if (isCollected) return;

            if (isDropping)
            {
                // Drop to target position
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, dropSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
                {
                    isDropping = false;
                }
            }
            else
            {
                // Bounce animation
                bounceTimer += Time.deltaTime * bounceSpeed;
                float yOffset = Mathf.Sin(bounceTimer) * bounceHeight;
                transform.position = new Vector3(transform.position.x, baseY + yOffset, transform.position.z);

                // Rotate slowly
                transform.Rotate(Vector3.up, 45f * Time.deltaTime);
            }

            // Pulse the light
            if (orbLight != null)
            {
                orbLight.intensity = 0.5f + Mathf.Sin(Time.time * 3f) * 0.2f;
            }
        }

        private void OnMouseDown()
        {
            if (isCollected) return;
            Collect();
        }

        public void Collect()
        {
            if (isCollected) return;
            isCollected = true;

            // Multiplayer mode - send action to server
            if (isMultiplayer)
            {
                var serverState = Crestforge.Networking.ServerGameState.Instance;
                if (serverState != null && !string.IsNullOrEmpty(lootId))
                {
                    Debug.Log($"[LootOrb] Collecting multiplayer loot: {lootId}");
                    serverState.CollectLoot(lootId);
                }

                // Play collect effect and destroy (server will add to inventory)
                StartCoroutine(CollectAnimation());
                return;
            }

            // Single-player mode - add consumable to inventory directly
            var state = GameState.Instance;
            if (state != null)
            {
                ItemData consumable = CreateConsumableItem();
                if (consumable != null && state.itemInventory.Count < GameConstants.Items.MAX_INVENTORY)
                {
                    state.itemInventory.Add(consumable);
                    Debug.Log($"Collected {consumable.itemName}! Use it from your inventory.");

                    // Refresh UI
                    if (GameUI.Instance != null)
                    {
                        GameUI.Instance.RefreshItemInventory();
                    }
                }
                else
                {
                    Debug.LogWarning("Inventory full! Could not collect loot.");
                }
            }

            // Play collect effect and destroy
            StartCoroutine(CollectAnimation());
        }

        private ItemData CreateConsumableItem()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();

            if (lootType == LootType.CrestToken)
            {
                item.itemId = "crest_token";
                item.itemName = "Crest Token";
                item.description = "Use to select a Minor Crest for your team.";
                item.rarity = ItemRarity.Rare;
                item.effect = ItemEffect.ConsumableCrestToken;
            }
            else if (lootType == LootType.ItemAnvil)
            {
                item.itemId = "item_anvil";
                item.itemName = "Item Anvil";
                item.description = "Use to forge a new item for your units.";
                item.rarity = ItemRarity.Rare;
                item.effect = ItemEffect.ConsumableItemAnvil;
            }

            return item;
        }

        private System.Collections.IEnumerator CollectAnimation()
        {
            // Quick scale up and fade
            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Scale up
                transform.localScale = startScale * (1f + t * 0.5f);

                // Move up slightly
                transform.position += Vector3.up * Time.deltaTime * 2f;

                // Fade light
                if (orbLight != null)
                {
                    orbLight.intensity = 0.5f * (1f - t);
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
