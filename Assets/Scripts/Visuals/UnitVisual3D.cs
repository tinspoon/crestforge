using UnityEngine;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Combat;

namespace Crestforge.Visuals
{
    /// <summary>
    /// 3D visual representation of a unit on the board.
    /// Uses custom 3D models when available, falls back to stylized primitives.
    /// </summary>
    public class UnitVisual3D : MonoBehaviour
    {
        [Header("References")]
        public UnitInstance unit;

        [Header("Model Database")]
        [Tooltip("Assign the UnitModelDatabase asset to use custom 3D models")]
        public static UnitModelDatabase modelDatabase;

        [Header("Visual Parts")]
        public GameObject bodyObj;
        public GameObject headObj;
        public GameObject baseObj;
        public GameObject customModelObj;
        public GameObject starContainer;
        public GameObject healthBarObj;
        public GameObject manaBarObj;

        [Header("Settings")]
        public float baseHeight = 0.8f;
        public float headSize = 0.25f;
        public float bodyRadius = 0.2f;

        // Track if using custom model
        private bool usingCustomModel = false;

        // Animation
        private UnitAnimator unitAnimator;

        // Materials
        private Material bodyMaterial;
        private Material headMaterial;
        private Material baseMaterial;

        // Original colors for reset after combat effects
        private Color originalBodyColor;
        private Color originalHeadColor;
        private bool hasStoredOriginalColors = false;
        private Coroutine flashCoroutine = null;

        // Health/Mana bar components
        private Transform healthFill;
        private Transform manaFill;

        // Item icons
        private GameObject itemIconContainer;
        private System.Collections.Generic.List<GameObject> itemIcons = new System.Collections.Generic.List<GameObject>();
        private int lastItemCount = -1;

        // Animation
        private Vector3 targetPosition;
        private Vector3 moveStartPosition;
        private float moveSpeed = 2.5f;
        private bool isMoving;
        public bool IsMoving => isMoving;
        private float moveStartTime;
        private float moveDuration = 0f; // If > 0, use lerping; otherwise use MoveTowards
        private const float MIN_WALK_ANIM_TIME = 0.3f; // Minimum time walk animation plays
        private float bobOffset;
        private float bobSpeed = 2f;
        private float bobAmount = 0.05f;

        // Combat animation
        private bool isAttacking;
        public bool IsAttacking => isAttacking;
        public bool IsPlayingHit => unitAnimator != null && unitAnimator.IsPlayingHit;
        private Vector3 attackTarget;
        private float attackTimer;
        private float attackDuration = 0.3f; // Base duration, dynamically adjusted based on attack speed

        // Multiplayer instance ID (set when syncing from server state)
        public string ServerInstanceId { get; set; }

        // Server item data for multiplayer
        private System.Collections.Generic.List<Crestforge.Networking.ServerItemData> serverItems;

        private void Awake()
        {
            // Initialize targetPosition to current position to prevent walking from origin
            // This is important because the GameObject should be positioned correctly before this component is added
            targetPosition = transform.position;
            isMoving = false;
        }

        /// <summary>
        /// Initialize the unit visual with data
        /// </summary>
        public void Initialize(UnitInstance unitInstance, bool isEnemy)
        {
            unit = unitInstance;
            this.isEnemy = isEnemy;

            // Get color palette based on origin for medieval theming
            ColorPalette palette = MedievalVisualConfig.GetPaletteForUnit(unitInstance.template);
            Color primaryColor = palette.GetBlendedPrimary(unitInstance.template.cost);
            Color secondaryColor = isEnemy ? MedievalVisualConfig.BoardColors.EnemyHighlight : MedievalVisualConfig.BoardColors.PlayerHighlight;

            // Determine archetype from traits
            archetype = UnitShapeFactory.GetArchetype(unitInstance.template);

            CreateVisuals(primaryColor, secondaryColor, isEnemy, unitInstance.template);
            UpdateStars(unitInstance.starLevel);

            // Set initial facing - face towards the camera uniformly
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 towardsCamera = -cam.transform.forward;
                towardsCamera.y = 0;

                // Fallback if camera is looking straight down
                if (towardsCamera.sqrMagnitude < 0.01f)
                {
                    towardsCamera = cam.transform.position - transform.position;
                    towardsCamera.y = 0;
                }

                if (towardsCamera.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(towardsCamera);
                }
            }
            else if (isEnemy)
            {
                // Fallback for enemies if no camera
                transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            bobOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        // Store archetype and enemy flag
        private UnitArchetype archetype = UnitArchetype.Default;
        public bool isEnemy { get; private set; }
        private GameObject shadowObj;

        /// <summary>
        /// When true, prevents automatic camera-facing rotation in Update
        /// </summary>
        public bool LockRotation { get; set; } = false;

        /// <summary>
        /// When true, freezes the unit at its current position (no movement)
        /// </summary>
        public bool FreezePosition { get; set; } = false;

        private void CreateVisuals(Color primaryColor, Color secondaryColor, bool isEnemy, UnitData template = null)
        {
            // === SHADOW ===
            shadowObj = UnitShapeFactory.CreateShadow(transform, 1f);

            // === BASE (flat cylinder showing team) - styled as stone/metal platform ===
            baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "Base";
            baseObj.transform.SetParent(transform);
            baseObj.transform.localPosition = new Vector3(0, 0.02f, 0);
            baseObj.transform.localScale = new Vector3(0.5f, 0.025f, 0.5f);
            Destroy(baseObj.GetComponent<Collider>());

            // Use toon material for base with team color accent
            baseMaterial = MedievalVisualConfig.CreateToonMaterial(
                isEnemy ? MedievalVisualConfig.BoardColors.EnemyTileLight : MedievalVisualConfig.BoardColors.PlayerTileLight,
                isEnemy ? MedievalVisualConfig.BoardColors.EnemyTileDark : MedievalVisualConfig.BoardColors.PlayerTileDark,
                0.2f
            );
            baseObj.GetComponent<Renderer>().material = baseMaterial;

            // === TRY CUSTOM MODEL FIRST ===
            string unitName = template != null ? template.unitName : "";
            if (modelDatabase != null && !string.IsNullOrEmpty(unitName))
            {
                customModelObj = modelDatabase.InstantiateModel(unitName, transform, isEnemy);
                if (customModelObj != null)
                {
                    usingCustomModel = true;
                    bodyObj = customModelObj;

                    // Get materials from custom model for effects
                    Renderer[] renderers = customModelObj.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        bodyMaterial = renderers[0].material;
                    }

                    // Apply team color tint to custom model (optional)
                    ApplyTeamColorToModel(customModelObj, primaryColor, isEnemy);

                    // Set up animator if model has one
                    SetupModelAnimator(customModelObj);

                    // Start with idle animation
                    if (unitAnimator != null)
                    {
                        unitAnimator.StopMoving(); // This plays idle
                    }
                }
            }

            // === FALLBACK: PROCEDURAL BODY ===
            if (!usingCustomModel)
            {
                bodyObj = UnitShapeFactory.CreateBody(archetype, transform, primaryColor, 1f, template);

                // Get body material from first renderer in body
                Renderer[] bodyRenderers = bodyObj.GetComponentsInChildren<Renderer>();
                if (bodyRenderers.Length > 0)
                {
                    bodyMaterial = bodyRenderers[0].material;
                }
                else
                {
                    // Fallback - create a simple material
                    bodyMaterial = new Material(Shader.Find("Standard"));
                    bodyMaterial.color = primaryColor;
                }

                // Store head material if there's a "Head" object
                Transform headTransform = bodyObj.transform.Find("Head");
                if (headTransform != null)
                {
                    headObj = headTransform.gameObject;
                    headMaterial = headObj.GetComponent<Renderer>()?.material;
                }
            }

            // === COLLIDER (for selection) ===
            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, baseHeight * 0.5f + 0.1f, 0);
            col.radius = bodyRadius * 1.5f;
            col.height = baseHeight + headSize;

            // === HEALTH BAR ===
            CreateHealthBar();

            // === MANA BAR ===
            CreateManaBar();

            // === STAR CONTAINER ===
            CreateStarContainer();

            // === ITEM ICONS ===
            CreateItemIconContainer();

            // Store original colors for reset after combat effects
            StoreOriginalColors();
        }

        /// <summary>
        /// Store original material colors for later reset
        /// </summary>
        private void StoreOriginalColors()
        {
            if (bodyMaterial != null)
            {
                originalBodyColor = GetMaterialColor(bodyMaterial);
            }
            if (headMaterial != null)
            {
                originalHeadColor = GetMaterialColor(headMaterial);
            }
            hasStoredOriginalColors = true;
        }

        /// <summary>
        /// Reset materials to original colors (call when combat ends)
        /// </summary>
        private void ResetToOriginalColors()
        {
            if (!hasStoredOriginalColors) return;

            // Stop any running flash coroutines
            StopAllCoroutines();

            if (bodyMaterial != null)
            {
                SetMaterialColor(bodyMaterial, originalBodyColor);
            }
            if (headMaterial != null)
            {
                SetMaterialColor(headMaterial, originalHeadColor);
            }
        }

        /// <summary>
        /// Set up animator component for custom models
        /// </summary>
        private void SetupModelAnimator(GameObject model)
        {
            // Get animation name overrides from database entry
            string unitName = unit?.template?.unitName ?? "";
            var entry = modelDatabase?.GetModelEntry(unitName);

            // Check for Animator (Mecanim) first - prefer this over Legacy
            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                // Only assign database controller if prefab doesn't already have one
                if (animator.runtimeAnimatorController == null && modelDatabase != null && modelDatabase.animatorController != null)
                {
                    animator.runtimeAnimatorController = modelDatabase.animatorController;
                }

                // Add UnitAnimator component to manage animations
                unitAnimator = model.AddComponent<UnitAnimator>();
                unitAnimator.animator = animator;

                // Apply custom animation names if specified
                if (entry != null)
                {
                    ApplyAnimationNames(unitAnimator, entry);
                }

                unitAnimator.Initialize();
                return;
            }

            // Fall back to Legacy Animation if no Animator found
            Animation legacyAnim = model.GetComponent<Animation>();
            if (legacyAnim == null)
            {
                legacyAnim = model.GetComponentInChildren<Animation>();
            }

            if (legacyAnim != null)
            {
                unitAnimator = model.AddComponent<UnitAnimator>();
                unitAnimator.legacyAnimation = legacyAnim;

                // Apply custom animation names if specified
                if (entry != null)
                {
                    ApplyAnimationNames(unitAnimator, entry);
                }

                unitAnimator.Initialize();
            }
        }

        /// <summary>
        /// Get the unit's current attack speed, accounting for combat buffs and global multiplier
        /// </summary>
        private float GetCurrentAttackSpeed()
        {
            float baseSpeed = 1f;

            // During combat, get from CombatUnit
            if (CombatManager.Instance != null && CombatManager.Instance.isInCombat)
            {
                foreach (var combatUnit in CombatManager.Instance.allUnits)
                {
                    if (combatUnit.source == unit)
                    {
                        baseSpeed = combatUnit.stats.attackSpeed;
                        break;
                    }
                }
            }
            // During planning, use UnitInstance stats
            else if (unit != null && unit.currentStats != null)
            {
                baseSpeed = unit.currentStats.attackSpeed;
            }
            // Fallback to base stats
            else if (unit != null && unit.template != null)
            {
                baseSpeed = unit.template.baseStats.attackSpeed;
            }

            // Apply global attack speed multiplier
            return baseSpeed * GameConstants.Combat.GLOBAL_ATTACK_SPEED_MULTIPLIER;
        }

        /// <summary>
        /// Apply custom animation clip names from database entry
        /// </summary>
        private void ApplyAnimationNames(UnitAnimator anim, UnitModelDatabase.UnitModelEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.idleClip))
                anim.idleClip = entry.idleClip;
            if (!string.IsNullOrEmpty(entry.walkClip))
                anim.walkClip = entry.walkClip;
            if (!string.IsNullOrEmpty(entry.attackClip))
                anim.attackClip = entry.attackClip;
            if (!string.IsNullOrEmpty(entry.hitClip))
                anim.hitClip = entry.hitClip;
            if (!string.IsNullOrEmpty(entry.deathClip))
                anim.deathClip = entry.deathClip;
            if (!string.IsNullOrEmpty(entry.victoryClip))
                anim.victoryClip = entry.victoryClip;

            // Apply animation speed
            if (entry.attackAnimSpeed > 0)
                anim.attackAnimSpeed = entry.attackAnimSpeed;
        }

        /// <summary>
        /// Apply team color tint to custom model materials
        /// </summary>
        private void ApplyTeamColorToModel(GameObject model, Color teamColor, bool isEnemy)
        {
            // Get all renderers
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();

            foreach (Renderer rend in renderers)
            {
                foreach (Material mat in rend.materials)
                {
                    // Check if material has a team color property (for models designed for this)
                    if (mat.HasProperty("_TeamColor"))
                    {
                        mat.SetColor("_TeamColor", teamColor);
                    }

                    // Optional: Add subtle rim light for team color
                    if (mat.HasProperty("_RimColor"))
                    {
                        Color rimColor = isEnemy ?
                            new Color(0.8f, 0.3f, 0.3f, 1f) :
                            new Color(0.3f, 0.5f, 0.8f, 1f);
                        mat.SetColor("_RimColor", rimColor);
                    }
                }
            }
        }

        private void CreateHealthBar()
        {
            healthBarObj = new GameObject("HealthBar");
            healthBarObj.transform.SetParent(transform);
            healthBarObj.transform.localPosition = new Vector3(0, baseHeight + headSize + 0.15f, 0);

            // Background (unlit to avoid shadows)
            GameObject bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgObj.name = "HealthBg";
            bgObj.transform.SetParent(healthBarObj.transform);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localScale = new Vector3(0.5f, 0.08f, 1f);
            Destroy(bgObj.GetComponent<Collider>());
            var bgMat = new Material(Shader.Find("Unlit/Color"));
            bgMat.color = new Color(0.2f, 0.2f, 0.2f);
            bgObj.GetComponent<Renderer>().material = bgMat;

            // Fill (unlit to avoid shadows)
            GameObject fillObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fillObj.name = "HealthFill";
            fillObj.transform.SetParent(healthBarObj.transform);
            fillObj.transform.localPosition = new Vector3(0, 0, -0.01f);
            fillObj.transform.localScale = new Vector3(0.48f, 0.06f, 1f);
            Destroy(fillObj.GetComponent<Collider>());
            var fillMat = new Material(Shader.Find("Unlit/Color"));
            fillMat.color = new Color(0.3f, 0.8f, 0.3f);
            fillObj.GetComponent<Renderer>().material = fillMat;
            
            healthFill = fillObj.transform;

            // Make health bar billboard (face camera)
            healthBarObj.AddComponent<BillboardUI>();
        }

        private void CreateManaBar()
        {
            manaBarObj = new GameObject("ManaBar");
            manaBarObj.transform.SetParent(transform);
            manaBarObj.transform.localPosition = new Vector3(0, baseHeight + headSize + 0.05f, 0);

            // Background (unlit to avoid shadows)
            GameObject bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgObj.name = "ManaBg";
            bgObj.transform.SetParent(manaBarObj.transform);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localScale = new Vector3(0.5f, 0.05f, 1f);
            Destroy(bgObj.GetComponent<Collider>());
            var manaBgMat = new Material(Shader.Find("Unlit/Color"));
            manaBgMat.color = new Color(0.1f, 0.1f, 0.2f);
            bgObj.GetComponent<Renderer>().material = manaBgMat;

            // Fill (unlit to avoid shadows)
            GameObject fillObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fillObj.name = "ManaFill";
            fillObj.transform.SetParent(manaBarObj.transform);
            fillObj.transform.localPosition = new Vector3(0, 0, -0.01f);
            fillObj.transform.localScale = new Vector3(0f, 0.03f, 1f); // Start empty
            Destroy(fillObj.GetComponent<Collider>());
            var manaFillMat = new Material(Shader.Find("Unlit/Color"));
            manaFillMat.color = new Color(0.3f, 0.5f, 1f);
            fillObj.GetComponent<Renderer>().material = manaFillMat;
            
            manaFill = fillObj.transform;

            manaBarObj.AddComponent<BillboardUI>();
        }

        private void CreateStarContainer()
        {
            starContainer = new GameObject("Stars");
            starContainer.transform.SetParent(transform);
            starContainer.transform.localPosition = new Vector3(0, 0.1f, 0.3f);
            starContainer.AddComponent<BillboardUI>();
        }

        private void CreateItemIconContainer()
        {
            itemIconContainer = new GameObject("ItemIcons");
            itemIconContainer.transform.SetParent(transform);
            // Position above health bar
            itemIconContainer.transform.localPosition = new Vector3(0, baseHeight + headSize + 0.28f, 0);
            itemIconContainer.AddComponent<BillboardUI>();
        }

        private void UpdateItemIcons()
        {
            if (itemIconContainer == null) return;

            // Determine current item count - check server items first (multiplayer), then unit items (single-player)
            int currentItemCount = 0;
            if (serverItems != null && serverItems.Count > 0)
            {
                currentItemCount = serverItems.Count;
            }
            else if (unit != null && unit.equippedItems != null)
            {
                currentItemCount = unit.equippedItems.Count;
            }

            // Only rebuild if item count changed
            if (currentItemCount == lastItemCount) return;
            lastItemCount = currentItemCount;

            // Clear existing icons
            foreach (var icon in itemIcons)
            {
                if (icon != null) Destroy(icon);
            }
            itemIcons.Clear();

            if (currentItemCount == 0) return;

            // Create item icons
            float iconSize = 0.12f;
            float spacing = 0.14f;
            float startX = -(currentItemCount - 1) * spacing * 0.5f;

            for (int i = 0; i < currentItemCount && i < Core.GameConstants.Items.MAX_PER_UNIT; i++)
            {
                Color itemColor;
                string itemName;

                // Get item data from server items or unit items
                if (serverItems != null && i < serverItems.Count)
                {
                    var serverItem = serverItems[i];
                    if (serverItem == null) continue;
                    itemName = serverItem.name ?? serverItem.itemId;
                    itemColor = new Color(1f, 0.8f, 0.4f);  // Gold for all items
                }
                else if (unit != null && unit.equippedItems != null && i < unit.equippedItems.Count)
                {
                    var item = unit.equippedItems[i];
                    if (item == null) continue;
                    itemName = item.itemName;
                    itemColor = GetItemRarityColor(item.rarity);
                }
                else
                {
                    continue;
                }

                GameObject iconObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                iconObj.name = $"Item_{itemName}";
                iconObj.transform.SetParent(itemIconContainer.transform);
                iconObj.transform.localPosition = new Vector3(startX + i * spacing, 0, -0.01f);
                iconObj.transform.localScale = new Vector3(iconSize, iconSize, 1f);
                iconObj.transform.localRotation = Quaternion.identity;
                Destroy(iconObj.GetComponent<Collider>());

                // Create material with item color
                var mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = itemColor;
                iconObj.GetComponent<Renderer>().material = mat;

                // Add a small border/outline effect by creating a slightly larger background quad
                GameObject borderObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                borderObj.name = $"ItemBorder_{itemName}";
                borderObj.transform.SetParent(itemIconContainer.transform);
                borderObj.transform.localPosition = new Vector3(startX + i * spacing, 0, 0.01f);
                borderObj.transform.localScale = new Vector3(iconSize + 0.03f, iconSize + 0.03f, 1f);
                borderObj.transform.localRotation = Quaternion.identity;
                Destroy(borderObj.GetComponent<Collider>());

                var borderMat = new Material(Shader.Find("Unlit/Color"));
                borderMat.color = new Color(0.1f, 0.1f, 0.1f);
                borderObj.GetComponent<Renderer>().material = borderMat;

                itemIcons.Add(iconObj);
                itemIcons.Add(borderObj);
            }
        }

        /// <summary>
        /// Set server items for multiplayer mode
        /// </summary>
        public void SetServerItems(System.Collections.Generic.List<Crestforge.Networking.ServerItemData> items)
        {
            serverItems = items;
            // Force refresh by resetting last count
            lastItemCount = -1;
            UpdateItemIcons();
        }

        private Color GetItemRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.7f, 0.7f, 0.7f),      // Gray
                ItemRarity.Uncommon => new Color(0.4f, 0.85f, 0.4f),   // Green
                ItemRarity.Rare => new Color(0.4f, 0.6f, 1f),          // Blue
                _ => Color.white
            };
        }

        /// <summary>
        /// Update star display based on unit level
        /// </summary>
        public void UpdateStars(int starLevel)
        {
            // Clear existing stars
            foreach (Transform child in starContainer.transform)
            {
                Destroy(child.gameObject);
            }

            // Create new stars
            float starSize = 0.08f;
            float spacing = 0.12f;
            float startX = -(starLevel - 1) * spacing * 0.5f;

            for (int i = 0; i < starLevel; i++)
            {
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Quad);
                star.name = $"Star_{i}";
                star.transform.SetParent(starContainer.transform);
                star.transform.localPosition = new Vector3(startX + i * spacing, 0, 0);
                star.transform.localScale = Vector3.one * starSize;
                star.transform.localRotation = Quaternion.Euler(0, 0, 45); // Diamond shape
                Destroy(star.GetComponent<Collider>());
                
                Material starMat = star.GetComponent<Renderer>().material;
                starMat.color = new Color(1f, 0.9f, 0.3f); // Gold
            }
        }

        /// <summary>
        /// Update health bar display
        /// </summary>
        public void UpdateHealthBar(float currentHealth, float maxHealth)
        {
            if (healthFill == null) return;

            float percent = Mathf.Clamp01(currentHealth / maxHealth);
            Vector3 scale = healthFill.localScale;
            scale.x = 0.48f * percent;
            healthFill.localScale = scale;

            // Shift position to fill from left
            Vector3 pos = healthFill.localPosition;
            pos.x = -0.24f * (1f - percent);
            healthFill.localPosition = pos;

            // Color based on health
            Color healthColor;
            if (percent > 0.5f)
                healthColor = Color.Lerp(Color.yellow, Color.green, (percent - 0.5f) * 2f);
            else
                healthColor = Color.Lerp(Color.red, Color.yellow, percent * 2f);
            
            healthFill.GetComponent<Renderer>().material.color = healthColor;
        }

        /// <summary>
        /// Update health bar using percentage (0-1)
        /// </summary>
        public void UpdateHealthBar(float percent)
        {
            if (healthFill == null) return;

            percent = Mathf.Clamp01(percent);
            Vector3 scale = healthFill.localScale;
            scale.x = 0.48f * percent;
            healthFill.localScale = scale;

            // Shift position to fill from left
            Vector3 pos = healthFill.localPosition;
            pos.x = -0.24f * (1f - percent);
            healthFill.localPosition = pos;

            // Color based on health
            Color healthColor;
            if (percent > 0.5f)
                healthColor = Color.Lerp(Color.yellow, Color.green, (percent - 0.5f) * 2f);
            else
                healthColor = Color.Lerp(Color.red, Color.yellow, percent * 2f);

            healthFill.GetComponent<Renderer>().material.color = healthColor;
        }

        /// <summary>
        /// Update mana bar display
        /// </summary>
        public void UpdateManaBar(float currentMana, float maxMana)
        {
            if (manaFill == null || maxMana <= 0) return;
            
            float percent = Mathf.Clamp01(currentMana / maxMana);
            Vector3 scale = manaFill.localScale;
            scale.x = 0.48f * percent;
            manaFill.localScale = scale;

            Vector3 pos = manaFill.localPosition;
            pos.x = -0.24f * (1f - percent);
            manaFill.localPosition = pos;
        }

        /// <summary>
        /// Move unit to a new position smoothly
        /// </summary>
        public void MoveTo(Vector3 worldPosition)
        {
            MoveTo(worldPosition, 0f); // Use speed-based movement by default
        }

        /// <summary>
        /// Move unit to a new position over a specific duration (for smooth server combat)
        /// </summary>
        public void MoveTo(Vector3 worldPosition, float duration)
        {
            moveStartPosition = transform.position;
            targetPosition = worldPosition;
            moveDuration = duration;
            moveStartTime = Time.time;

            // Only start walk animation if not already moving
            if (!isMoving)
            {
                isMoving = true;

                // Trigger walk animation
                if (unitAnimator != null)
                {
                    unitAnimator.StartMoving();
                }
            }
        }

        /// <summary>
        /// Teleport unit immediately to position
        /// </summary>
        public void SetPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            targetPosition = worldPosition;
            isMoving = false;
        }

        /// <summary>
        /// Immediately rotate to face the camera
        /// </summary>
        public void FaceCamera()
        {
            if (LockRotation) return;

            Camera cam = Camera.main;
            if (cam != null)
            {
                // Use the opposite of camera's forward direction (horizontal only)
                // This makes all units face the same direction rather than converging to a point
                Vector3 towardsCamera = -cam.transform.forward;
                towardsCamera.y = 0;

                // If camera is looking straight down, fall back to using camera position
                if (towardsCamera.sqrMagnitude < 0.01f)
                {
                    towardsCamera = cam.transform.position - transform.position;
                    towardsCamera.y = 0;
                }

                if (towardsCamera.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(towardsCamera);
                }
            }
        }

        /// <summary>
        /// Teleport unit immediately and face camera (used for swapping)
        /// </summary>
        public void SetPositionAndFaceCamera(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            targetPosition = worldPosition;
            isMoving = false;
            FaceCamera();
        }

        /// <summary>
        /// Play attack animation toward target
        /// </summary>
        public void PlayAttackAnimation(Vector3 targetPos)
        {
            // Use default attack speed from unit data
            PlayAttackAnimation(targetPos, 0f);
        }

        /// <summary>
        /// Play attack animation toward target with explicit attack speed
        /// </summary>
        /// <param name="targetPos">Position of the attack target</param>
        /// <param name="overrideAttackSpeed">Attack speed to use for animation timing (0 = auto-detect)</param>
        public void PlayAttackAnimation(Vector3 targetPos, float overrideAttackSpeed)
        {
            if (isAttacking) return;

            attackTarget = targetPos;
            attackStartPos = transform.position;
            isAttacking = true;
            attackTimer = 0f;

            // Use override speed if provided, otherwise auto-detect
            float attackSpeed = overrideAttackSpeed > 0 ? overrideAttackSpeed : GetCurrentAttackSpeed();

            // Calculate dynamic attack duration based on attack speed
            // Attack should complete within 50% of the attack interval to ensure
            // animations finish before the next attack event arrives
            if (attackSpeed > 0)
            {
                attackDuration = 0.5f / attackSpeed;
                // Clamp to reasonable range (0.1s to 1.0s)
                attackDuration = Mathf.Clamp(attackDuration, 0.1f, 1.0f);
            }
            else
            {
                attackDuration = 0.3f; // Default fallback
            }

            // Face the target immediately
            Vector3 lookDir = (targetPos - transform.position);
            lookDir.y = 0;
            if (lookDir.magnitude > 0.1f)
            {
                targetRotation = Quaternion.LookRotation(lookDir);
                transform.rotation = targetRotation; // Apply immediately for snappy attacks
            }

            // Trigger attack animation with dynamic speed
            if (unitAnimator != null)
            {
                unitAnimator.PlayAttack(attackSpeed);
            }
        }

        /// <summary>
        /// Play hit/damage effect
        /// </summary>
        public void PlayHitEffect()
        {
            // Cancel any running flash to prevent color drift
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                // Reset to original color before starting new flash
                if (hasStoredOriginalColors)
                {
                    if (bodyMaterial != null) SetMaterialColor(bodyMaterial, originalBodyColor);
                    if (headMaterial != null) SetMaterialColor(headMaterial, originalHeadColor);
                }
            }
            flashCoroutine = StartCoroutine(FlashColor(Color.red, 0.25f));

            // DISABLED: Shake effect conflicts with movement/attack animations
            // It captures position at start and resets to it, causing jitter
            // StartCoroutine(ShakeEffect(0.1f, 0.05f));

            // Skip hit animation if unit is already walking or attacking
            // This prevents jarring animation interruptions
            if (isMoving || isAttacking)
            {
                return;
            }

            // TESTING: Hit animations disabled to evaluate combat feel
            // Trigger hit animation
            // if (unitAnimator != null)
            // {
            //     unitAnimator.PlayHit();
            // }
        }

        /// <summary>
        /// Stop any in-progress attack animation
        /// </summary>
        public void StopAttackAnimation()
        {
            isAttacking = false;
            attackTimer = 0f;
        }

        /// <summary>
        /// Play ability cast effect
        /// </summary>
        public void PlayAbilityEffect()
        {
            // Cancel any running flash
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                if (hasStoredOriginalColors)
                {
                    if (bodyMaterial != null) SetMaterialColor(bodyMaterial, originalBodyColor);
                    if (headMaterial != null) SetMaterialColor(headMaterial, originalHeadColor);
                }
            }
            flashCoroutine = StartCoroutine(FlashColor(new Color(0.5f, 0.7f, 1f), 0.3f));
            StartCoroutine(ScalePop(1.2f, 0.3f));
        }

        /// <summary>
        /// Play death effect
        /// </summary>
        public void PlayDeathEffect()
        {
            // Trigger death animation
            if (unitAnimator != null)
            {
                unitAnimator.PlayDeath();
            }

            StartCoroutine(DeathAnimation());
        }

        private System.Collections.IEnumerator FlashColor(Color flashColor, float duration)
        {
            if (bodyMaterial == null) yield break;

            // Use stored original colors to prevent color drift from rapid hits
            Color origBody = hasStoredOriginalColors ? originalBodyColor : GetMaterialColor(bodyMaterial);
            Color origHead = hasStoredOriginalColors ? originalHeadColor : (headMaterial != null ? GetMaterialColor(headMaterial) : Color.white);

            // Instant flash to color
            SetMaterialColor(bodyMaterial, flashColor);
            if (headMaterial != null) SetMaterialColor(headMaterial, flashColor);

            // Quick fade back to original
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Use ease-out curve for snappy start, smooth end
                float easedT = 1f - (1f - t) * (1f - t);

                if (bodyMaterial != null)
                    SetMaterialColor(bodyMaterial, Color.Lerp(flashColor, origBody, easedT));
                if (headMaterial != null)
                    SetMaterialColor(headMaterial, Color.Lerp(flashColor, origHead, easedT));

                yield return null;
            }

            // Ensure we end at original color
            if (bodyMaterial != null) SetMaterialColor(bodyMaterial, origBody);
            if (headMaterial != null) SetMaterialColor(headMaterial, origHead);

            flashCoroutine = null;
        }

        private Color GetMaterialColor(Material mat)
        {
            if (mat.HasProperty("_MainColor"))
                return mat.GetColor("_MainColor");
            else if (mat.HasProperty("_Color"))
                return mat.color;
            return Color.white;
        }

        private void SetMaterialColor(Material mat, Color color)
        {
            if (mat.HasProperty("_MainColor"))
                mat.SetColor("_MainColor", color);
            else if (mat.HasProperty("_Color"))
                mat.color = color;
        }

        private System.Collections.IEnumerator ShakeEffect(float duration, float magnitude)
        {
            Vector3 originalPos = transform.position;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float z = Random.Range(-1f, 1f) * magnitude;
                transform.position = originalPos + new Vector3(x, 0, z);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            transform.position = originalPos;
        }

        private System.Collections.IEnumerator ScalePop(float targetScale, float duration)
        {
            Vector3 originalScale = transform.localScale;
            Vector3 popScale = originalScale * targetScale;
            float elapsed = 0f;
            
            // Scale up
            while (elapsed < duration * 0.3f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.3f);
                transform.localScale = Vector3.Lerp(originalScale, popScale, t);
                yield return null;
            }
            
            // Scale down
            elapsed = 0f;
            while (elapsed < duration * 0.7f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.7f);
                transform.localScale = Vector3.Lerp(popScale, originalScale, t);
                yield return null;
            }
            
            transform.localScale = originalScale;
        }

        private System.Collections.IEnumerator DeathAnimation()
        {
            float duration = 0.5f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 startPos = transform.position;

            // Get starting color using helper
            Color startBodyColor = bodyMaterial != null ? GetMaterialColor(bodyMaterial) : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Scale down and sink
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                transform.position = startPos + Vector3.down * t * 0.3f;

                // Fade materials - darken instead of alpha (toon shader doesn't support transparency)
                if (bodyMaterial != null)
                {
                    Color fadeColor = Color.Lerp(startBodyColor, Color.black, t);
                    SetMaterialColor(bodyMaterial, fadeColor);
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        // New fields needed
        private Vector3 attackStartPos;
        private Quaternion targetRotation;
        private bool wasInCombat = false;

        private void Update()
        {
            // Skip all movement if position is frozen (e.g., during victory pose)
            if (FreezePosition)
            {
                isMoving = false;
            }

            // Smooth movement
            if (isMoving)
            {
                // Use duration-based lerping for smooth server combat, or speed-based for planning phase
                if (moveDuration > 0)
                {
                    // Smooth lerp over duration
                    float elapsed = Time.time - moveStartTime;
                    float t = Mathf.Clamp01(elapsed / moveDuration);
                    // Use smooth step for even smoother interpolation
                    t = t * t * (3f - 2f * t);
                    transform.position = Vector3.Lerp(moveStartPosition, targetPosition, t);
                }
                else
                {
                    // Speed-based movement for planning phase
                    transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                }

                // Face movement direction
                Vector3 moveDir = targetPosition - transform.position;
                moveDir.y = 0;
                if (moveDir.magnitude > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
                }

                // Only stop moving after reaching destination AND minimum walk animation time has passed
                bool reachedDestination = Vector3.Distance(transform.position, targetPosition) < 0.01f;
                bool minWalkTimeElapsed = Time.time - moveStartTime >= MIN_WALK_ANIM_TIME;

                if (reachedDestination && minWalkTimeElapsed)
                {
                    transform.position = targetPosition;
                    isMoving = false;
                    moveDuration = 0f; // Reset for next movement

                    // Stop walk animation
                    if (unitAnimator != null)
                    {
                        unitAnimator.StopMoving();
                    }
                }
                else if (reachedDestination)
                {
                    // Reached destination but still playing walk animation - snap position but keep animating
                    transform.position = targetPosition;
                }
            }

            // Check if in combat (local or server or scout)
            bool inLocalCombat = CombatManager.Instance != null && CombatManager.Instance.isInCombat;
            bool inServerCombat = (Crestforge.Systems.ServerCombatVisualizer.Instance != null &&
                                   Crestforge.Systems.ServerCombatVisualizer.Instance.isPlaying) ||
                                  (Crestforge.UI.ScoutingUI.Instance != null &&
                                   Crestforge.UI.ScoutingUI.Instance.IsScoutCombatPlaying);
            bool inVictoryPose = (Crestforge.Systems.ServerCombatVisualizer.Instance != null &&
                                  Crestforge.Systems.ServerCombatVisualizer.Instance.isInVictoryPose) ||
                                 (Crestforge.UI.ScoutingUI.Instance != null &&
                                  Crestforge.UI.ScoutingUI.Instance.IsScoutCombatInVictoryPose);
            bool inCombat = inLocalCombat || inServerCombat;

            // Check if combat just ended - reset to idle animation and colors
            // But NOT if we're in victory pose (victory animation should keep playing)
            if (wasInCombat && !inCombat && !inVictoryPose)
            {
                // Combat just ended - reset all combat states and return to idle
                isAttacking = false;
                isMoving = false;
                if (unitAnimator != null)
                {
                    unitAnimator.ResetToIdle();
                }

                // Reset colors to remove any lingering damage tint
                ResetToOriginalColors();
            }
            wasInCombat = inCombat;

            // During planning phase, face towards the camera (so you can see their faces)
            // Skip if rotation is locked (e.g., bench units during away combat)
            if (!inCombat && !isMoving && !isAttacking && !LockRotation)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    // Use camera's forward direction so all units face uniformly
                    Vector3 towardsCamera = -cam.transform.forward;
                    towardsCamera.y = 0;

                    // Fallback if camera is looking straight down
                    if (towardsCamera.sqrMagnitude < 0.01f)
                    {
                        towardsCamera = cam.transform.position - transform.position;
                        towardsCamera.y = 0;
                    }

                    if (towardsCamera.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(towardsCamera);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
                    }
                }
            }

            // Idle bob animation - only for procedural models without animators
            if (!isMoving && !isAttacking && unitAnimator == null)
            {
                float bob = Mathf.Sin(Time.time * bobSpeed + bobOffset) * bobAmount;

                if (usingCustomModel && customModelObj != null)
                {
                    // For custom models without animator, bob the whole model
                    Vector3 modelPos = customModelObj.transform.localPosition;
                    var entry = modelDatabase?.GetModelEntry(unit?.template?.unitName ?? "");
                    float baseY = entry != null ? entry.yOffset : 0f;
                    modelPos.y = baseY + bob;
                    customModelObj.transform.localPosition = modelPos;
                }
                else
                {
                    // For procedural models, bob body and head separately
                    if (bodyObj != null)
                    {
                        Vector3 bodyPos = bodyObj.transform.localPosition;
                        bodyPos.y = baseHeight * 0.5f + 0.05f + bob;
                        bodyObj.transform.localPosition = bodyPos;
                    }
                    if (headObj != null)
                    {
                        Vector3 headPos = headObj.transform.localPosition;
                        headPos.y = baseHeight + headSize * 0.5f + bob;
                        headObj.transform.localPosition = headPos;
                    }
                }
            }

            // Attack animation
            if (isAttacking)
            {
                attackTimer += Time.deltaTime;
                float t = attackTimer / attackDuration;

                // Smoothly rotate toward target
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);

                // Only do procedural lunge if no animator
                if (unitAnimator == null)
                {
                    if (t < 0.4f)
                    {
                        // Lunge toward target
                        float lungeT = t / 0.4f;
                        Vector3 lungeTarget = Vector3.Lerp(attackStartPos, attackTarget, 0.3f);
                        transform.position = Vector3.Lerp(attackStartPos, lungeTarget, lungeT);
                    }
                    else if (t < 1f)
                    {
                        // Return to position
                        float returnT = (t - 0.4f) / 0.6f;
                        Vector3 lungeTarget = Vector3.Lerp(attackStartPos, attackTarget, 0.3f);
                        transform.position = Vector3.Lerp(lungeTarget, targetPosition, returnT);
                    }
                }

                if (t >= 1f)
                {
                    isAttacking = false;
                    transform.position = targetPosition;
                }
            }

            // Update unit stats display
            UpdateHealthDisplay();

            // Update item icons when items change
            UpdateItemIcons();
        }

        /// <summary>
        /// Update health bar based on combat or planning phase
        /// </summary>
        private void UpdateHealthDisplay()
        {
            if (unit == null) return;

            // During server combat, health is updated directly via UpdateHealthBar() calls from ServerCombatVisualizer
            // Skip automatic polling to prevent overwriting server-provided health values
            bool inServerCombat = Crestforge.Systems.ServerCombatVisualizer.Instance != null &&
                                  Crestforge.Systems.ServerCombatVisualizer.Instance.isPlaying;
            if (inServerCombat) return;

            float currentHealth = 0;
            float maxHealth = unit.template.baseStats.health * Mathf.Pow(1.8f, unit.starLevel - 1);

            // During local combat, get health from CombatUnit
            if (CombatManager.Instance != null && CombatManager.Instance.isInCombat)
            {
                foreach (var combatUnit in CombatManager.Instance.allUnits)
                {
                    if (combatUnit.source == unit)
                    {
                        currentHealth = combatUnit.currentHealth;
                        maxHealth = combatUnit.stats.health;
                        break;
                    }
                }
            }
            else if (unit.currentStats != null)
            {
                // Planning phase - use UnitInstance stats
                currentHealth = unit.currentStats.health;
            }

            UpdateHealthBar(currentHealth, maxHealth);
        }

        /// <summary>
        /// Get color based on unit cost tier
        /// </summary>
        private Color GetUnitColor(int cost)
        {
            return cost switch
            {
                1 => new Color(0.6f, 0.6f, 0.6f),   // Gray
                2 => new Color(0.4f, 0.7f, 0.4f),   // Green
                3 => new Color(0.4f, 0.5f, 0.9f),   // Blue
                4 => new Color(0.7f, 0.4f, 0.8f),   // Purple
                5 => new Color(1f, 0.8f, 0.3f),     // Gold
                _ => Color.white
            };
        }

        /// <summary>
        /// Set selection highlight
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (baseMaterial != null)
            {
                baseMaterial.color = selected ? 
                    new Color(0.4f, 0.8f, 0.4f) : 
                    (unit != null ? GetUnitColor(unit.template.cost) : Color.gray);
            }
        }
    }

}
