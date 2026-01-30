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

        // Health/Mana bar components
        private Transform healthFill;
        private Transform manaFill;

        // Animation
        private Vector3 targetPosition;
        private float moveSpeed = 2.5f;
        private bool isMoving;
        private float moveStartTime;
        private const float MIN_WALK_ANIM_TIME = 0.3f; // Minimum time walk animation plays
        private float bobOffset;
        private float bobSpeed = 2f;
        private float bobAmount = 0.05f;

        // Combat animation
        private bool isAttacking;
        private Vector3 attackTarget;
        private float attackTimer;
        private float attackDuration = 0.3f;

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

            bobOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        // Store archetype and enemy flag
        private UnitArchetype archetype = UnitArchetype.Default;
        private bool isEnemy;
        private GameObject shadowObj;

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
            targetPosition = worldPosition;

            // Only start walk animation if not already moving
            if (!isMoving)
            {
                isMoving = true;
                moveStartTime = Time.time;

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
        /// Play attack animation toward target
        /// </summary>
        public void PlayAttackAnimation(Vector3 targetPos)
        {
            if (isAttacking) return;

            attackTarget = targetPos;
            attackStartPos = transform.position;
            isAttacking = true;
            attackTimer = 0f;

            // Face the target
            Vector3 lookDir = (targetPos - transform.position);
            lookDir.y = 0;
            if (lookDir.magnitude > 0.1f)
            {
                targetRotation = Quaternion.LookRotation(lookDir);
            }

            // Trigger attack animation with dynamic speed based on unit's attack speed
            if (unitAnimator != null)
            {
                float attackSpeed = GetCurrentAttackSpeed();
                unitAnimator.PlayAttack(attackSpeed);
            }
        }

        /// <summary>
        /// Play hit/damage effect
        /// </summary>
        public void PlayHitEffect()
        {
            StartCoroutine(FlashColor(Color.red, 0.15f));
            StartCoroutine(ShakeEffect(0.1f, 0.05f));

            // Trigger hit animation
            if (unitAnimator != null)
            {
                unitAnimator.PlayHit();
            }
        }

        /// <summary>
        /// Play ability cast effect
        /// </summary>
        public void PlayAbilityEffect()
        {
            StartCoroutine(FlashColor(new Color(0.5f, 0.7f, 1f), 0.3f));
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

            // Support both standard shader (color) and toon shader (_MainColor)
            Color origBody = GetMaterialColor(bodyMaterial);
            Color origHead = headMaterial != null ? GetMaterialColor(headMaterial) : Color.white;

            SetMaterialColor(bodyMaterial, flashColor);
            if (headMaterial != null) SetMaterialColor(headMaterial, flashColor);

            yield return new WaitForSeconds(duration);

            if (bodyMaterial != null) SetMaterialColor(bodyMaterial, origBody);
            if (headMaterial != null) SetMaterialColor(headMaterial, origHead);
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
            // Smooth movement
            if (isMoving)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                
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

            // Check if combat just ended - reset to idle animation
            bool inCombat = CombatManager.Instance != null && CombatManager.Instance.isInCombat;
            if (wasInCombat && !inCombat)
            {
                // Combat just ended - reset all combat states and return to idle
                isAttacking = false;
                isMoving = false;
                if (unitAnimator != null)
                {
                    unitAnimator.ResetToIdle();
                }
            }
            wasInCombat = inCombat;

            // During planning phase, face towards the camera (so you can see their faces)
            if (!inCombat && !isMoving && !isAttacking)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    // Face towards the camera (opposite of camera's forward direction)
                    Vector3 towardsCamera = -cam.transform.forward;
                    towardsCamera.y = 0;
                    if (towardsCamera.magnitude > 0.1f)
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
        }

        /// <summary>
        /// Update health bar based on combat or planning phase
        /// </summary>
        private void UpdateHealthDisplay()
        {
            if (unit == null) return;
            
            float currentHealth = 0;
            float maxHealth = unit.template.baseStats.health * Mathf.Pow(1.8f, unit.starLevel - 1);
            
            // During combat, get health from CombatUnit
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

    /// <summary>
    /// Makes a UI element always face the camera
    /// </summary>
    public class BillboardUI : MonoBehaviour
    {
        private Camera cam;

        private void Start()
        {
            cam = Camera.main;
        }

        private void LateUpdate()
        {
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }
    }
}
