using UnityEngine;
using System.Collections.Generic;

namespace Crestforge.Cosmetics
{
    /// <summary>
    /// Manages scenery placement around the battlefield
    /// </summary>
    public class SceneryManager : MonoBehaviour
    {
        public static SceneryManager Instance { get; private set; }

        // Cached shader for material creation (URP or Standard fallback)
        private static Shader _cachedShader;

        [Header("Slot Definitions")]
        public List<ScenerySlotData> slots = new List<ScenerySlotData>();

        [Header("Available Items")]
        public List<SceneryItemData> availableItems = new List<SceneryItemData>();

        [Header("Player's Placed Items")]
        public List<PlacedScenery> placedScenery = new List<PlacedScenery>();

        // Runtime spawned objects
        private Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>();
        private Dictionary<string, ScenerySlotVisual> slotVisuals = new Dictionary<string, ScenerySlotVisual>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Get the correct shader for materials (URP with Standard fallback)
        /// </summary>
        private static Shader GetShader()
        {
            if (_cachedShader != null) return _cachedShader;

            // Try URP shader first
            _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_cachedShader == null || _cachedShader.name == "Hidden/InternalErrorShader")
            {
                // Fall back to Standard
                _cachedShader = Shader.Find("Standard");
            }
            return _cachedShader;
        }

        /// <summary>
        /// Create a new material with the correct shader
        /// </summary>
        private static Material CreateMaterial(Color color)
        {
            Material mat = new Material(GetShader());
            mat.color = color;
            return mat;
        }

        private void Start()
        {
            InitializeDefaultSlots();
            InitializeDefaultItems();
            SpawnAllScenery();
        }

        /// <summary>
        /// Set up the default placement slots around the player's side
        /// </summary>
        private void InitializeDefaultSlots()
        {
            if (slots.Count > 0) return; // Already configured

            // Get board reference for positioning
            var hexBoard = Crestforge.Visuals.HexBoard3D.Instance;
            float boardHalfWidth = 3.5f;  // Approximate
            float benchZ = -3f;           // Behind bench

            // Back-left corner (flag slot)
            slots.Add(new ScenerySlotData
            {
                slotId = "back_left",
                displayName = "Back Left Flag",
                slotType = SlotType.Flag,
                position = new Vector3(-boardHalfWidth - 1f, 0, benchZ - 1f),
                rotationY = 45f
            });

            // Back-right corner (flag slot)
            slots.Add(new ScenerySlotData
            {
                slotId = "back_right",
                displayName = "Back Right Flag",
                slotType = SlotType.Flag,
                position = new Vector3(boardHalfWidth + 1f, 0, benchZ - 1f),
                rotationY = -45f
            });

            // Side-left (toy slot) - beside the battlefield
            slots.Add(new ScenerySlotData
            {
                slotId = "side_left",
                displayName = "Left Decoration",
                slotType = SlotType.Toy,
                position = new Vector3(-boardHalfWidth - 1.5f, 0, 0f),
                rotationY = 90f
            });

            // Side-right (toy slot) - beside the battlefield
            slots.Add(new ScenerySlotData
            {
                slotId = "side_right",
                displayName = "Right Decoration",
                slotType = SlotType.Toy,
                position = new Vector3(boardHalfWidth + 1.5f, 0, 0f),
                rotationY = -90f
            });
        }

        /// <summary>
        /// Set up default available items
        /// </summary>
        private void InitializeDefaultItems()
        {
            if (availableItems.Count > 0) return;

            // Default flag (everyone has this)
            availableItems.Add(new SceneryItemData
            {
                itemId = "flag_basic",
                itemName = "Basic Banner",
                description = "A simple banner to show your colors",
                category = SceneryCategory.Flag,
                rarity = SceneryRarity.Common,
                unlockType = UnlockType.Default,
                hasIdleAnimation = true,
                reactsToCombat = true
            });

            // Basic toy
            availableItems.Add(new SceneryItemData
            {
                itemId = "toy_mushroom",
                itemName = "Lucky Mushroom",
                description = "A cheerful mushroom decoration",
                category = SceneryCategory.Toy,
                rarity = SceneryRarity.Common,
                unlockType = UnlockType.Default,
                hasIdleAnimation = false,
                reactsToCombat = false
            });

            // Rare toy
            availableItems.Add(new SceneryItemData
            {
                itemId = "toy_crystal",
                itemName = "Mystic Crystal",
                description = "A glowing crystal that pulses with energy",
                category = SceneryCategory.Toy,
                rarity = SceneryRarity.Rare,
                unlockType = UnlockType.Progression,
                unlockRequirement = "win_10_games",
                hasIdleAnimation = true,
                reactsToCombat = true
            });

            // Set default placements if none exist
            if (placedScenery.Count == 0)
            {
                // Place default flags in flag slots
                placedScenery.Add(new PlacedScenery
                {
                    slotId = "back_left",
                    itemId = "flag_basic",
                    flagCustomization = new FlagCustomization(
                        new Color(0.8f, 0.15f, 0.15f),  // Red
                        new Color(1f, 0.85f, 0.2f),     // Gold
                        0
                    )
                });

                placedScenery.Add(new PlacedScenery
                {
                    slotId = "back_right",
                    itemId = "flag_basic",
                    flagCustomization = new FlagCustomization(
                        new Color(0.15f, 0.15f, 0.8f),  // Blue
                        new Color(0.9f, 0.9f, 0.95f),   // Silver
                        0
                    )
                });
            }
        }

        /// <summary>
        /// Spawn all placed scenery objects
        /// </summary>
        public void SpawnAllScenery()
        {
            // Clear existing
            foreach (var obj in spawnedObjects.Values)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedObjects.Clear();

            // Spawn slot markers and items
            foreach (var slot in slots)
            {
                // Create slot visual marker
                CreateSlotVisual(slot);

                // Find and spawn placed item
                var placed = placedScenery.Find(p => p.slotId == slot.slotId);
                if (placed != null)
                {
                    SpawnSceneryItem(slot, placed);
                }
            }
        }

        /// <summary>
        /// Create a visual marker for an empty slot (subtle ground indicator)
        /// </summary>
        private void CreateSlotVisual(ScenerySlotData slot)
        {
            GameObject marker = new GameObject($"SlotMarker_{slot.slotId}");
            marker.transform.SetParent(transform);
            marker.transform.position = slot.position;

            // Add a subtle ground circle
            GameObject circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            circle.name = "SlotCircle";
            circle.transform.SetParent(marker.transform);
            circle.transform.localPosition = new Vector3(0, 0.01f, 0);
            circle.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f);
            Destroy(circle.GetComponent<Collider>());

            var renderer = circle.GetComponent<Renderer>();
            renderer.material = CreateMaterial(new Color(0.3f, 0.25f, 0.2f, 0.5f));

            var slotVisual = marker.AddComponent<ScenerySlotVisual>();
            slotVisual.slotData = slot;
            slotVisuals[slot.slotId] = slotVisual;
        }

        /// <summary>
        /// Spawn a scenery item in its slot
        /// </summary>
        private void SpawnSceneryItem(ScenerySlotData slot, PlacedScenery placed)
        {
            var itemData = availableItems.Find(i => i.itemId == placed.itemId);
            if (itemData == null) return;

            GameObject obj;

            // Use custom prefab or generate based on category
            if (itemData.prefab != null)
            {
                obj = Instantiate(itemData.prefab);
            }
            else
            {
                obj = GenerateSceneryObject(itemData, placed);
            }

            obj.name = $"Scenery_{slot.slotId}_{itemData.itemId}";
            obj.transform.SetParent(transform);
            obj.transform.position = slot.position;
            obj.transform.rotation = Quaternion.Euler(0, slot.rotationY, 0);
            obj.transform.localScale = slot.scale;

            // Add behavior component
            var behavior = obj.AddComponent<SceneryBehavior>();
            behavior.Initialize(itemData, placed);

            spawnedObjects[slot.slotId] = obj;
        }

        /// <summary>
        /// Generate a scenery object procedurally based on item type
        /// </summary>
        private GameObject GenerateSceneryObject(SceneryItemData itemData, PlacedScenery placed)
        {
            GameObject obj = new GameObject(itemData.itemName);

            switch (itemData.category)
            {
                case SceneryCategory.Flag:
                    GenerateFlag(obj, placed.flagCustomization ?? new FlagCustomization());
                    break;
                case SceneryCategory.Toy:
                    GenerateToy(obj, itemData);
                    break;
                default:
                    GenerateDefaultObject(obj, itemData);
                    break;
            }

            return obj;
        }

        /// <summary>
        /// Generate a flag with pole and banner
        /// </summary>
        private void GenerateFlag(GameObject parent, FlagCustomization customization)
        {
            // Flag pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(parent.transform);
            pole.transform.localPosition = new Vector3(0, 0.75f, 0);
            pole.transform.localScale = new Vector3(0.05f, 0.75f, 0.05f);
            Destroy(pole.GetComponent<Collider>());

            pole.GetComponent<Renderer>().material = CreateMaterial(new Color(0.4f, 0.3f, 0.2f)); // Wood brown

            // Pole top ornament
            GameObject ornament = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ornament.name = "Ornament";
            ornament.transform.SetParent(parent.transform);
            ornament.transform.localPosition = new Vector3(0, 1.55f, 0);
            ornament.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            Destroy(ornament.GetComponent<Collider>());

            ornament.GetComponent<Renderer>().material = CreateMaterial(customization.secondaryColor);

            // Banner (using a cube scaled flat, could be replaced with cloth mesh later)
            GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "Banner";
            banner.transform.SetParent(parent.transform);
            banner.transform.localPosition = new Vector3(0.25f, 1.2f, 0);
            banner.transform.localScale = new Vector3(0.5f, 0.6f, 0.03f);
            Destroy(banner.GetComponent<Collider>());

            banner.GetComponent<Renderer>().material = CreateMaterial(customization.primaryColor);

            // Banner trim (top edge)
            GameObject trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.name = "Trim";
            trim.transform.SetParent(parent.transform);
            trim.transform.localPosition = new Vector3(0.25f, 1.48f, 0);
            trim.transform.localScale = new Vector3(0.52f, 0.05f, 0.04f);
            Destroy(trim.GetComponent<Collider>());

            trim.GetComponent<Renderer>().material = CreateMaterial(customization.secondaryColor);

            // Add flag animation component
            var flagAnim = parent.AddComponent<FlagAnimation>();
            flagAnim.banner = banner.transform;
        }

        /// <summary>
        /// Generate a toy decoration
        /// </summary>
        private void GenerateToy(GameObject parent, SceneryItemData itemData)
        {
            // Simple toy - could be expanded based on itemId
            if (itemData.itemId == "toy_mushroom")
            {
                // Mushroom cap
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = "Cap";
                cap.transform.SetParent(parent.transform);
                cap.transform.localPosition = new Vector3(0, 0.3f, 0);
                cap.transform.localScale = new Vector3(0.4f, 0.25f, 0.4f);
                Destroy(cap.GetComponent<Collider>());
                cap.GetComponent<Renderer>().material = CreateMaterial(new Color(0.9f, 0.2f, 0.2f));

                // Stem
                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = "Stem";
                stem.transform.SetParent(parent.transform);
                stem.transform.localPosition = new Vector3(0, 0.12f, 0);
                stem.transform.localScale = new Vector3(0.15f, 0.12f, 0.15f);
                Destroy(stem.GetComponent<Collider>());
                stem.GetComponent<Renderer>().material = CreateMaterial(new Color(0.95f, 0.92f, 0.85f));

                // Spots on cap
                for (int i = 0; i < 4; i++)
                {
                    GameObject spot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    spot.name = $"Spot_{i}";
                    spot.transform.SetParent(cap.transform);
                    float angle = i * 90f * Mathf.Deg2Rad;
                    spot.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.3f, 0.2f, Mathf.Sin(angle) * 0.3f);
                    spot.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    Destroy(spot.GetComponent<Collider>());
                    spot.GetComponent<Renderer>().material = CreateMaterial(Color.white);
                }
            }
            else if (itemData.itemId == "toy_crystal")
            {
                // Crystal formation
                GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crystal.name = "Crystal";
                crystal.transform.SetParent(parent.transform);
                crystal.transform.localPosition = new Vector3(0, 0.3f, 0);
                crystal.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
                crystal.transform.localRotation = Quaternion.Euler(0, 45f, 10f);
                Destroy(crystal.GetComponent<Collider>());

                var crystalMat = CreateMaterial(new Color(0.5f, 0.8f, 1f, 0.8f));
                crystal.GetComponent<Renderer>().material = crystalMat;
                // Enable emission (works for both URP and Standard)
                crystalMat.EnableKeyword("_EMISSION");
                crystalMat.SetColor("_EmissionColor", new Color(0.3f, 0.5f, 0.7f));

                // Smaller crystals
                for (int i = 0; i < 2; i++)
                {
                    GameObject small = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    small.name = $"SmallCrystal_{i}";
                    small.transform.SetParent(parent.transform);
                    float xOff = (i == 0) ? -0.12f : 0.1f;
                    small.transform.localPosition = new Vector3(xOff, 0.15f, 0.05f);
                    small.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
                    small.transform.localRotation = Quaternion.Euler(5f, 30f + i * 60f, 15f);
                    Destroy(small.GetComponent<Collider>());
                    // Share the crystal material for consistent emission
                    small.GetComponent<Renderer>().sharedMaterial = crystalMat;
                }
            }
            else
            {
                GenerateDefaultObject(parent, itemData);
            }
        }

        /// <summary>
        /// Generate a default placeholder object
        /// </summary>
        private void GenerateDefaultObject(GameObject parent, SceneryItemData itemData)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Placeholder";
            obj.transform.SetParent(parent.transform);
            obj.transform.localPosition = new Vector3(0, 0.25f, 0);
            obj.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            Destroy(obj.GetComponent<Collider>());

            obj.GetComponent<Renderer>().material = CreateMaterial(itemData.GetRarityColor());
        }

        /// <summary>
        /// Place an item in a slot
        /// </summary>
        public void PlaceItem(string slotId, string itemId, FlagCustomization flagCustomization = null)
        {
            // Remove existing placement
            placedScenery.RemoveAll(p => p.slotId == slotId);

            // Add new placement
            var placed = new PlacedScenery
            {
                slotId = slotId,
                itemId = itemId,
                flagCustomization = flagCustomization
            };
            placedScenery.Add(placed);

            // Respawn the slot
            var slot = slots.Find(s => s.slotId == slotId);
            if (slot != null)
            {
                // Remove old object
                if (spawnedObjects.TryGetValue(slotId, out var oldObj) && oldObj != null)
                {
                    Destroy(oldObj);
                }

                SpawnSceneryItem(slot, placed);
            }
        }

        /// <summary>
        /// Clear a slot
        /// </summary>
        public void ClearSlot(string slotId)
        {
            placedScenery.RemoveAll(p => p.slotId == slotId);

            if (spawnedObjects.TryGetValue(slotId, out var obj) && obj != null)
            {
                Destroy(obj);
            }
            spawnedObjects.Remove(slotId);
        }

        /// <summary>
        /// Get item data by ID
        /// </summary>
        public SceneryItemData GetItemData(string itemId)
        {
            return availableItems.Find(i => i.itemId == itemId);
        }

        /// <summary>
        /// Notify all scenery of a combat event (for reactions)
        /// </summary>
        public void OnCombatEvent(CombatEventType eventType, Vector3 position)
        {
            foreach (var obj in spawnedObjects.Values)
            {
                if (obj == null) continue;
                var behavior = obj.GetComponent<SceneryBehavior>();
                if (behavior != null && behavior.itemData.reactsToCombat)
                {
                    behavior.OnCombatEvent(eventType, position);
                }
            }
        }
    }

    public enum CombatEventType
    {
        UnitKilled,
        UnitDamaged,
        AbilityCast,
        Victory,
        Defeat
    }
}
