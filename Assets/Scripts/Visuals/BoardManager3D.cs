using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Crestforge.Core;
using Crestforge.Systems;
using Crestforge.Data;
using Crestforge.Combat;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Manages the 3D board visuals, unit placement, and interaction.
    /// Bridges between GameState and 3D visual representations.
    /// </summary>
    public class BoardManager3D : MonoBehaviour
    {
        public static BoardManager3D Instance { get; private set; }

        [Header("Prefabs")]
        public GameObject unitVisualPrefab;

        [Header("References")]
        public HexBoard3D hexBoard;
        public IsometricCameraSetup cameraSetup;

        [Header("Settings")]
        public float unitYOffset = 0.15f;

        // Runtime
        private Dictionary<UnitInstance, UnitVisual3D> unitVisuals = new Dictionary<UnitInstance, UnitVisual3D>();
        private GameState state;
        
        // Drag & drop
        private UnitVisual3D selectedUnit;
        private UnitVisual3D draggedUnit;
        private Vector3 dragStartPos;
        private Vector2Int dragStartCoord;
        private bool isDragging;
        private GameObject dragPlaceholder;

        // Hover
        private GameObject hoveredTile;
        private UnitVisual3D hoveredUnit;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            state = GameState.Instance;
            
            // Find or create board
            if (hexBoard == null)
            {
                hexBoard = FindObjectOfType<HexBoard3D>();
                if (hexBoard == null)
                {
                    GameObject boardObj = new GameObject("HexBoard3D");
                    hexBoard = boardObj.AddComponent<HexBoard3D>();
                }
            }

            // Find or create camera
            if (cameraSetup == null)
            {
                cameraSetup = FindObjectOfType<IsometricCameraSetup>();
                if (cameraSetup == null && Camera.main != null)
                {
                    cameraSetup = Camera.main.gameObject.AddComponent<IsometricCameraSetup>();
                }
            }

            // Create drag placeholder
            CreateDragPlaceholder();

            // Subscribe to combat events (if CombatManager exists)
            if (CombatManager.Instance != null && !subscribedToCombat)
            {
                SubscribeToCombatEvents();
                subscribedToCombat = true;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromCombatEvents();
        }

        private void SubscribeToCombatEvents()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnDamageDealt += HandleDamageDealt;
                CombatManager.Instance.OnUnitDied += HandleUnitDied;
                CombatManager.Instance.OnAbilityCast += HandleAbilityCast;
            }
        }

        private void UnsubscribeFromCombatEvents()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnDamageDealt -= HandleDamageDealt;
                CombatManager.Instance.OnUnitDied -= HandleUnitDied;
                CombatManager.Instance.OnAbilityCast -= HandleAbilityCast;
            }
        }

        private void HandleDamageDealt(CombatUnit attacker, CombatUnit target, int damage)
        {
            // Show attack animation
            if (IsUnitTracked(attacker.source) && IsUnitTracked(target.source))
            {
                UnitVisual3D attackerVis = GetUnitVisual(attacker.source);
                UnitVisual3D targetVis = GetUnitVisual(target.source);
                
                if (attackerVis != null && targetVis != null)
                {
                    // Check if ranged attack
                    bool isRanged = attacker.stats.range > 1;
                    
                    if (isRanged && ProjectileSystem.Instance != null)
                    {
                        // Fire projectile for ranged attacks
                        ProjectileType projType = GetProjectileType(attacker.source.template);
                        ProjectileSystem.Instance.FireProjectile(
                            attackerVis, 
                            targetVis, 
                            projType,
                            () => {
                                // On hit callback
                                if (targetVis != null)
                                {
                                    targetVis.PlayHitEffect();
                                    PlayDamage(target.source, damage);
                                    
                                    // Spawn hit VFX
                                    VFXSystem.Instance?.SpawnEffect(
                                        GetHitVFXType(projType), 
                                        targetVis.transform.position + Vector3.up * 0.3f,
                                        0.5f
                                    );
                                }
                            }
                        );
                    }
                    else
                    {
                        // Melee attack animation
                        attackerVis.PlayAttackAnimation(targetVis.transform.position);
                        
                        // Show damage after a small delay (when attack lands)
                        StartCoroutine(DelayedHitEffect(target, targetVis, damage, 0.15f));
                    }
                }
            }
        }

        private System.Collections.IEnumerator DelayedHitEffect(CombatUnit target, UnitVisual3D targetVis, int damage, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (targetVis != null)
            {
                targetVis.PlayHitEffect();
                PlayDamage(target.source, damage);
                
                // Melee hit VFX
                VFXSystem.Instance?.SpawnEffect(VFXType.Slash, targetVis.transform.position + Vector3.up * 0.3f, 0.6f);
            }
        }

        private ProjectileType GetProjectileType(UnitData template)
        {
            if (template == null || template.traits == null)
                return ProjectileType.Arrow;

            foreach (var trait in template.traits)
            {
                if (trait == null) continue;
                string traitName = trait.traitName.ToLower();
                
                if (traitName.Contains("fire") || traitName.Contains("flame") || traitName.Contains("inferno"))
                    return ProjectileType.Fireball;
                if (traitName.Contains("ice") || traitName.Contains("frost") || traitName.Contains("cold"))
                    return ProjectileType.IceShard;
                if (traitName.Contains("lightning") || traitName.Contains("storm") || traitName.Contains("electric"))
                    return ProjectileType.Lightning;
                if (traitName.Contains("holy") || traitName.Contains("light") || traitName.Contains("divine"))
                    return ProjectileType.Holy;
                if (traitName.Contains("shadow") || traitName.Contains("dark") || traitName.Contains("void"))
                    return ProjectileType.Shadow;
                if (traitName.Contains("mage") || traitName.Contains("wizard") || traitName.Contains("arcane"))
                    return ProjectileType.Fireball;
            }
            
            return ProjectileType.Arrow;
        }

        private VFXType GetHitVFXType(ProjectileType projType)
        {
            return projType switch
            {
                ProjectileType.Fireball => VFXType.FireExplosion,
                ProjectileType.IceShard => VFXType.IceShatter,
                ProjectileType.Lightning => VFXType.LightningStrike,
                _ => VFXType.Impact
            };
        }

        private void HandleUnitDied(CombatUnit unit)
        {
            if (IsUnitTracked(unit.source))
            {
                UnitVisual3D visual = GetUnitVisual(unit.source);
                if (visual != null)
                {
                    // Spawn death VFX
                    VFXSystem.Instance?.SpawnEffect(VFXType.Death, visual.transform.position, 1f);
                    
                    visual.PlayDeathEffect();
                }
                unitVisuals.Remove(unit.source);
            }
        }

        private void HandleAbilityCast(CombatUnit caster)
        {
            if (IsUnitTracked(caster.source))
            {
                UnitVisual3D visual = GetUnitVisual(caster.source);
                if (visual != null)
                {
                    visual.PlayAbilityEffect();
                    
                    // Show ability name
                    if (caster.source.template.ability != null)
                    {
                        ShowAbilityText(caster.source, caster.source.template.ability.abilityName);
                        
                        // Spawn ability-specific VFX
                        VFXType abilityVFX = GetAbilityVFX(caster.source.template);
                        VFXSystem.Instance?.SpawnEffectOnUnit(abilityVFX, visual, 1.5f);
                    }
                }
            }
        }

        private VFXType GetAbilityVFX(UnitData template)
        {
            if (template == null || template.traits == null)
                return VFXType.Buff;

            foreach (var trait in template.traits)
            {
                if (trait == null) continue;
                string traitName = trait.traitName.ToLower();
                
                if (traitName.Contains("healer") || traitName.Contains("support") || traitName.Contains("priest"))
                    return VFXType.Heal;
                if (traitName.Contains("fire") || traitName.Contains("flame"))
                    return VFXType.FireExplosion;
                if (traitName.Contains("ice") || traitName.Contains("frost"))
                    return VFXType.IceShatter;
                if (traitName.Contains("lightning") || traitName.Contains("storm"))
                    return VFXType.LightningStrike;
                if (traitName.Contains("guardian") || traitName.Contains("tank"))
                    return VFXType.Shield;
            }
            
            return VFXType.Buff;
        }

        private bool IsUnitTracked(UnitInstance unit)
        {
            return unitVisuals.ContainsKey(unit);
        }

        // Track if we've subscribed to combat events
        private bool subscribedToCombat = false;

        private void Update()
        {
            if (state == null)
            {
                state = GameState.Instance;
                if (state == null) return;
            }

            // Late subscribe to combat events if needed
            if (!subscribedToCombat && CombatManager.Instance != null)
            {
                SubscribeToCombatEvents();
                subscribedToCombat = true;
            }

            // Sync visuals with game state
            SyncBoardVisuals();
            
            // Handle input
            HandleInput();
        }

        // Track last known combat positions for Results phase
        private Dictionary<UnitInstance, Vector2Int> lastCombatPositions = new Dictionary<UnitInstance, Vector2Int>();
        private GamePhase lastPhase = GamePhase.Planning;

        /// <summary>
        /// Synchronize 3D visuals with current game state
        /// </summary>
        private void SyncBoardVisuals()
        {
            if (state.playerBoard == null || hexBoard == null) return;

            HashSet<UnitInstance> currentUnits = new HashSet<UnitInstance>();
            int playerRows = hexBoard.playerRows;

            // Detect phase transition
            if (state.round.phase != lastPhase)
            {
                // Transitioning from Combat to Results - save positions
                if (lastPhase == GamePhase.Combat && state.round.phase == GamePhase.Results)
                {
                    SaveCombatPositions();
                }
                // Transitioning to Planning - clear saved positions
                else if (state.round.phase == GamePhase.Planning)
                {
                    lastCombatPositions.Clear();
                }
                lastPhase = state.round.phase;
            }

            // During combat, sync from CombatManager's unit list with their combat positions
            if (state.round.phase == GamePhase.Combat && CombatManager.Instance != null && CombatManager.Instance.isInCombat)
            {
                foreach (var combatUnit in CombatManager.Instance.allUnits)
                {
                    if (combatUnit.isDead) continue;
                    
                    UnitInstance unit = combatUnit.source;
                    if (unit != null)
                    {
                        currentUnits.Add(unit);
                        bool isEnemy = combatUnit.team == Team.Enemy;
                        
                        // Use combat position
                        int x = combatUnit.position.x;
                        int y = combatUnit.position.y;
                        
                        // Save position for Results phase
                        lastCombatPositions[unit] = new Vector2Int(x, y);
                        
                        SyncUnitVisual(unit, x, y, isEnemy);
                    }
                }
            }
            else if (state.round.phase == GamePhase.Results)
            {
                // During Results phase, show surviving player units at their last combat positions
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < playerRows; y++)
                    {
                        if (x < state.playerBoard.GetLength(0) && y < state.playerBoard.GetLength(1))
                        {
                            UnitInstance unit = state.playerBoard[x, y];
                            if (unit != null)
                            {
                                currentUnits.Add(unit);
                                
                                // Use last combat position if available, otherwise board position
                                if (lastCombatPositions.TryGetValue(unit, out Vector2Int combatPos))
                                {
                                    SyncUnitVisual(unit, combatPos.x, combatPos.y, false);
                                }
                                else
                                {
                                    SyncUnitVisual(unit, x, y, false);
                                }
                            }
                        }
                    }
                }
                // Don't show any enemies during Results
            }
            else
            {
                // Planning phase - sync from board arrays
                // Player board
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < playerRows; y++)
                    {
                        if (x < state.playerBoard.GetLength(0) && y < state.playerBoard.GetLength(1))
                        {
                            UnitInstance unit = state.playerBoard[x, y];
                            if (unit != null)
                            {
                                currentUnits.Add(unit);
                                SyncUnitVisual(unit, x, y, false);
                            }
                        }
                    }
                }

                // Enemy board - only show if we have new enemies for this round
                // Check if CombatManager has been reset (not in combat)
                bool showEnemies = state.enemyBoard != null && 
                                   (CombatManager.Instance == null || !CombatManager.Instance.isInCombat);
                
                if (showEnemies && state.enemyBoard != null)
                {
                    // Only show enemies if they haven't been defeated yet
                    // Check if any combat has happened this round
                    bool combatOccurred = CombatManager.Instance != null && 
                                          CombatManager.Instance.allUnits != null && 
                                          CombatManager.Instance.allUnits.Count > 0;
                    
                    if (!combatOccurred)
                    {
                        int enemyRows = GameConstants.Grid.HEIGHT;
                        for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                        {
                            for (int y = 0; y < enemyRows; y++)
                            {
                                if (x < state.enemyBoard.GetLength(0) && y < state.enemyBoard.GetLength(1))
                                {
                                    UnitInstance unit = state.enemyBoard[x, y];
                                    if (unit != null)
                                    {
                                        currentUnits.Add(unit);
                                        int boardY = y + playerRows;
                                        SyncUnitVisual(unit, x, boardY, true);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Remove visuals for dead/removed units
            List<UnitInstance> toRemove = new List<UnitInstance>();
            foreach (var kvp in unitVisuals)
            {
                if (!currentUnits.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var unit in toRemove)
            {
                if (unitVisuals.TryGetValue(unit, out UnitVisual3D visual))
                {
                    if (visual != null)
                    {
                        Destroy(visual.gameObject);
                    }
                    unitVisuals.Remove(unit);
                }
            }
        }

        /// <summary>
        /// Save combat positions when transitioning to Results
        /// </summary>
        private void SaveCombatPositions()
        {
            if (CombatManager.Instance == null) return;
            
            lastCombatPositions.Clear();
            foreach (var combatUnit in CombatManager.Instance.allUnits)
            {
                if (!combatUnit.isDead && combatUnit.source != null)
                {
                    lastCombatPositions[combatUnit.source] = combatUnit.position;
                }
            }
        }

        /// <summary>
        /// Get world position for a unit based on its combat position
        /// </summary>
        private Vector3 GetUnitWorldPosition(UnitInstance unit)
        {
            // Try to get position from unit's gridX/gridY if available
            int x = 0, y = 0;
            
            // Use reflection or direct field access to get grid position
            var unitType = unit.GetType();
            var gridXField = unitType.GetField("gridX") ?? unitType.GetField("x") ?? unitType.GetField("posX");
            var gridYField = unitType.GetField("gridY") ?? unitType.GetField("y") ?? unitType.GetField("posY");
            
            if (gridXField != null && gridYField != null)
            {
                x = (int)gridXField.GetValue(unit);
                y = (int)gridYField.GetValue(unit);
            }
            else
            {
                // Try properties
                var gridXProp = unitType.GetProperty("gridX") ?? unitType.GetProperty("x") ?? unitType.GetProperty("posX");
                var gridYProp = unitType.GetProperty("gridY") ?? unitType.GetProperty("y") ?? unitType.GetProperty("posY");
                
                if (gridXProp != null && gridYProp != null)
                {
                    x = (int)gridXProp.GetValue(unit);
                    y = (int)gridYProp.GetValue(unit);
                }
                else
                {
                    // Fallback - search boards for unit position
                    if (TryFindUnitOnBoards(unit, out x, out y))
                    {
                        // Found position
                    }
                }
            }
            
            Vector3 worldPos = hexBoard.GetTileWorldPosition(x, y);
            worldPos.y = unitYOffset;
            return worldPos;
        }

        /// <summary>
        /// Find unit position by searching the boards
        /// </summary>
        private bool TryFindUnitOnBoards(UnitInstance unit, out int outX, out int outY)
        {
            int playerRows = hexBoard != null ? hexBoard.playerRows : 4;
            
            // Search player board
            if (state.playerBoard != null)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < playerRows; y++)
                    {
                        if (x < state.playerBoard.GetLength(0) && y < state.playerBoard.GetLength(1))
                        {
                            if (state.playerBoard[x, y] == unit)
                            {
                                outX = x;
                                outY = y;
                                return true;
                            }
                        }
                    }
                }
            }
            
            // Search enemy board
            if (state.enemyBoard != null)
            {
                // HEIGHT is per-side
                int enemyRows = GameConstants.Grid.HEIGHT;
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < enemyRows; y++)
                    {
                        if (x < state.enemyBoard.GetLength(0) && y < state.enemyBoard.GetLength(1))
                        {
                            if (state.enemyBoard[x, y] == unit)
                            {
                                outX = x;
                                outY = y + playerRows;
                                return true;
                            }
                        }
                    }
                }
            }
            
            outX = 0;
            outY = 0;
            return false;
        }

        /// <summary>
        /// Sync unit visual at a specific world position
        /// </summary>
        private void SyncUnitVisualAtPosition(UnitInstance unit, Vector3 worldPos, bool isEnemy)
        {
            if (!unitVisuals.TryGetValue(unit, out UnitVisual3D visual) || visual == null)
            {
                // Create new visual
                GameObject visualObj = new GameObject($"Unit_{unit.template.unitName}");
                visualObj.transform.SetParent(transform);
                visual = visualObj.AddComponent<UnitVisual3D>();
                visual.Initialize(unit, isEnemy);
                visual.SetPosition(worldPos);
                unitVisuals[unit] = visual;
            }
            else
            {
                // Update position smoothly
                if (isDragging && visual == draggedUnit)
                {
                    // Don't move dragged unit
                }
                else if (Vector3.Distance(visual.transform.position, worldPos) > 0.05f)
                {
                    visual.MoveTo(worldPos);
                }
            }
        }

        /// <summary>
        /// Create or update visual for a unit
        /// </summary>
        private void SyncUnitVisual(UnitInstance unit, int x, int y, bool isEnemy)
        {
            Vector3 worldPos = hexBoard.GetTileWorldPosition(x, y);
            worldPos.y = unitYOffset;

            if (!unitVisuals.TryGetValue(unit, out UnitVisual3D visual) || visual == null)
            {
                // Create new visual
                GameObject visualObj = new GameObject($"Unit_{unit.template.unitName}");
                visualObj.transform.SetParent(transform);
                visual = visualObj.AddComponent<UnitVisual3D>();
                visual.Initialize(unit, isEnemy);
                visual.SetPosition(worldPos);
                unitVisuals[unit] = visual;
            }
            else
            {
                // Update position if needed (smooth move)
                if (Vector3.Distance(visual.transform.position, worldPos) > 0.1f)
                {
                    if (isDragging && visual == draggedUnit)
                    {
                        // Don't move dragged unit
                    }
                    else
                    {
                        visual.MoveTo(worldPos);
                    }
                }
            }
        }

        /// <summary>
        /// Handle mouse/touch input for unit interaction
        /// </summary>
        private void HandleInput()
        {
            // Only allow interaction during planning phase
            if (state.round.phase != GamePhase.Planning) return;

            Vector3 mousePos = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            // Hover detection
            UpdateHover(ray);

            // Mouse/touch input
            if (Input.GetMouseButtonDown(0))
            {
                OnPointerDown(ray);
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                OnPointerDrag(ray);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                OnPointerUp(ray);
            }

            // Touch input
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                Ray touchRay = Camera.main.ScreenPointToRay(touch.position);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        OnPointerDown(touchRay);
                        break;
                    case TouchPhase.Moved:
                        if (isDragging) OnPointerDrag(touchRay);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        OnPointerUp(touchRay);
                        break;
                }
            }
        }

        private void UpdateHover(Ray ray)
        {
            // Check for unit hover
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                UnitVisual3D unitVis = hit.collider.GetComponent<UnitVisual3D>();
                if (unitVis != null && unitVis != hoveredUnit)
                {
                    if (hoveredUnit != null)
                        hoveredUnit.SetSelected(false);
                    
                    hoveredUnit = unitVis;
                    // Could show tooltip here
                }

                // Check for tile hover
                HexTile3D tile = hit.collider.GetComponent<HexTile3D>();
                if (tile != null && tile.gameObject != hoveredTile)
                {
                    if (hoveredTile != null)
                        hoveredTile.GetComponent<HexTile3D>()?.SetHover(false);
                    
                    hoveredTile = tile.gameObject;
                    tile.SetHover(true);
                }
            }
            else
            {
                if (hoveredUnit != null)
                {
                    hoveredUnit.SetSelected(false);
                    hoveredUnit = null;
                }
                if (hoveredTile != null)
                {
                    hoveredTile.GetComponent<HexTile3D>()?.SetHover(false);
                    hoveredTile = null;
                }
            }
        }

        private void OnPointerDown(Ray ray)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                UnitVisual3D unitVis = hit.collider.GetComponent<UnitVisual3D>();
                if (unitVis != null && unitVis.unit != null)
                {
                    // Start dragging
                    draggedUnit = unitVis;
                    dragStartPos = unitVis.transform.position;
                    
                    // Find current grid position
                    if (TryGetUnitGridPosition(unitVis.unit, out int x, out int y))
                    {
                        dragStartCoord = new Vector2Int(x, y);
                        isDragging = true;
                        
                        // Block camera input while dragging
                        if (cameraSetup != null)
                        {
                            cameraSetup.inputBlocked = true;
                        }
                        
                        // Show placeholder at original position
                        dragPlaceholder.transform.position = dragStartPos;
                        dragPlaceholder.SetActive(true);
                        
                        // Highlight valid placement tiles
                        HighlightValidTiles();
                    }
                }
            }
        }

        private void OnPointerDrag(Ray ray)
        {
            if (!isDragging || draggedUnit == null) return;

            // Get ground position
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, unitYOffset, 0));
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPos = ray.GetPoint(distance);
                draggedUnit.transform.position = worldPos;
            }
        }

        private void OnPointerUp(Ray ray)
        {
            if (!isDragging)
            {
                // Check for unit selection (tap without drag)
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    UnitVisual3D unitVis = hit.collider.GetComponent<UnitVisual3D>();
                    if (unitVis != null)
                    {
                        SelectUnit(unitVis);
                    }
                }
                return;
            }

            isDragging = false;
            dragPlaceholder.SetActive(false);
            hexBoard.ClearHighlights();
            
            // Unblock camera input
            if (cameraSetup != null)
            {
                cameraSetup.inputBlocked = false;
            }

            if (draggedUnit == null) return;

            // Temporarily disable the dragged unit's collider so raycast hits the tile
            Collider draggedCollider = draggedUnit.GetComponent<Collider>();
            if (draggedCollider != null) draggedCollider.enabled = false;

            // Find target tile
            GameObject targetTile = hexBoard.GetTileAtScreenPosition(Input.mousePosition);
            
            // Re-enable collider
            if (draggedCollider != null) draggedCollider.enabled = true;
            
            Debug.Log($"[BoardManager3D] OnPointerUp: targetTile={targetTile?.name ?? "null"}");
            
            if (targetTile != null && hexBoard.TryGetTileCoord(targetTile, out Vector2Int coord, out bool isEnemy))
            {
                Debug.Log($"[BoardManager3D] Target coord: ({coord.x},{coord.y}), isEnemy={isEnemy}, playerRows={hexBoard.playerRows}");
                
                // Only allow placement in player rows
                if (coord.y < hexBoard.playerRows)
                {
                    // Try to place unit at new position
                    bool success = TryMoveUnit(draggedUnit.unit, dragStartCoord.x, dragStartCoord.y, coord.x, coord.y);
                    
                    Debug.Log($"[BoardManager3D] TryMoveUnit success={success}");
                    
                    if (success)
                    {
                        // Move visual to new position
                        Vector3 newPos = hexBoard.GetTileWorldPosition(coord.x, coord.y);
                        newPos.y = unitYOffset;
                        draggedUnit.SetPosition(newPos); // Use SetPosition for immediate placement
                        Debug.Log($"[BoardManager3D] Set unit position to {newPos}");
                    }
                    else
                    {
                        // Return to original position
                        draggedUnit.MoveTo(dragStartPos);
                    }
                }
                else
                {
                    Debug.Log($"[BoardManager3D] Invalid placement - enemy row");
                    // Invalid placement - return to original
                    draggedUnit.MoveTo(dragStartPos);
                }
            }
            else
            {
                Debug.Log($"[BoardManager3D] No valid tile found");
                // No valid tile - return to original
                draggedUnit.MoveTo(dragStartPos);
            }

            draggedUnit = null;
        }

        private void SelectUnit(UnitVisual3D unitVis)
        {
            // Deselect previous
            if (selectedUnit != null)
            {
                selectedUnit.SetSelected(false);
            }

            // Select new
            if (selectedUnit == unitVis)
            {
                selectedUnit = null;
            }
            else
            {
                selectedUnit = unitVis;
                selectedUnit.SetSelected(true);
                
                // Show unit tooltip/info
                if (Crestforge.UI.GameUI.Instance != null)
                {
                    Crestforge.UI.GameUI.Instance.ShowTooltipPinned(unitVis.unit);
                }
            }
        }

        private void HighlightValidTiles()
        {
            int playerRows = hexBoard.playerRows;
            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < playerRows; y++)
                {
                    if (x < state.playerBoard.GetLength(0) && y < state.playerBoard.GetLength(1))
                    {
                        bool isEmpty = state.playerBoard[x, y] == null;
                        bool isStart = (x == dragStartCoord.x && y == dragStartCoord.y);
                        
                        if (isEmpty || isStart)
                        {
                            hexBoard.HighlightTile(x, y, true);
                        }
                    }
                }
            }
        }

        private bool TryGetUnitGridPosition(UnitInstance unit, out int outX, out int outY)
        {
            int playerRows = hexBoard != null ? hexBoard.playerRows : 4;
            
            if (state.playerBoard != null)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < playerRows; y++)
                    {
                        if (x < state.playerBoard.GetLength(0) && y < state.playerBoard.GetLength(1))
                        {
                            if (state.playerBoard[x, y] == unit)
                            {
                                outX = x;
                                outY = y;
                                return true;
                            }
                        }
                    }
                }
            }

            outX = 0;
            outY = 0;
            return false;
        }

        private bool TryMoveUnit(UnitInstance unit, int fromX, int fromY, int toX, int toY)
        {
            if (fromX == toX && fromY == toY) return true; // Same position

            // Bounds check
            if (toX < 0 || toX >= state.playerBoard.GetLength(0) || 
                toY < 0 || toY >= state.playerBoard.GetLength(1))
            {
                Debug.LogWarning($"[BoardManager3D] TryMoveUnit: target ({toX},{toY}) out of bounds");
                return false;
            }

            // Check if target is empty
            UnitInstance targetUnit = state.playerBoard[toX, toY];
            
            if (targetUnit == null)
            {
                // Move to empty tile
                state.playerBoard[fromX, fromY] = null;
                state.playerBoard[toX, toY] = unit;
                Debug.Log($"[BoardManager3D] Moved unit from ({fromX},{fromY}) to ({toX},{toY})");
                return true;
            }
            else
            {
                // Swap units
                state.playerBoard[fromX, fromY] = targetUnit;
                state.playerBoard[toX, toY] = unit;
                Debug.Log($"[BoardManager3D] Swapped units at ({fromX},{fromY}) and ({toX},{toY})");
                return true;
            }
        }

        private void CreateDragPlaceholder()
        {
            dragPlaceholder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dragPlaceholder.name = "DragPlaceholder";
            dragPlaceholder.transform.SetParent(transform);
            dragPlaceholder.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
            Destroy(dragPlaceholder.GetComponent<Collider>());
            
            Material mat = dragPlaceholder.GetComponent<Renderer>().material;
            mat.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            
            dragPlaceholder.SetActive(false);
        }

        /// <summary>
        /// Get the visual for a specific unit
        /// </summary>
        public UnitVisual3D GetUnitVisual(UnitInstance unit)
        {
            unitVisuals.TryGetValue(unit, out UnitVisual3D visual);
            return visual;
        }

        /// <summary>
        /// Play attack animation from attacker to target
        /// </summary>
        public void PlayAttack(UnitInstance attacker, UnitInstance target)
        {
            if (unitVisuals.TryGetValue(attacker, out UnitVisual3D attackerVis) &&
                unitVisuals.TryGetValue(target, out UnitVisual3D targetVis))
            {
                attackerVis.PlayAttackAnimation(targetVis.transform.position);
            }
        }

        /// <summary>
        /// Play damage effect on a unit
        /// </summary>
        public void PlayDamage(UnitInstance unit, int damage)
        {
            if (unitVisuals.TryGetValue(unit, out UnitVisual3D visual))
            {
                visual.PlayHitEffect();
                SpawnDamageNumber(visual.transform.position, damage, Color.red);
            }
        }

        /// <summary>
        /// Play healing effect on a unit
        /// </summary>
        public void PlayHealing(UnitInstance unit, int amount)
        {
            if (unitVisuals.TryGetValue(unit, out UnitVisual3D visual))
            {
                SpawnDamageNumber(visual.transform.position, amount, Color.green);
            }
        }

        /// <summary>
        /// Show ability name on a unit
        /// </summary>
        public void ShowAbilityText(UnitInstance unit, string abilityName)
        {
            if (unitVisuals.TryGetValue(unit, out UnitVisual3D visual))
            {
                SpawnAbilityText(visual.transform.position, abilityName);
            }
        }

        private void SpawnDamageNumber(Vector3 position, int value, Color color)
        {
            GameObject dmgObj = new GameObject("DamageNumber");
            dmgObj.transform.position = position + Vector3.up * 1f;
            
            // Create a TextMesh for 3D text
            TextMesh textMesh = dmgObj.AddComponent<TextMesh>();
            textMesh.text = value.ToString();
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.1f;
            textMesh.color = color;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.fontStyle = FontStyle.Bold;
            
            // Add MeshRenderer settings
            MeshRenderer mr = dmgObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = 100;
            }
            
            dmgObj.AddComponent<BillboardUI>();
            dmgObj.AddComponent<FloatingText>();
        }

        private void SpawnAbilityText(Vector3 position, string text)
        {
            GameObject textObj = new GameObject("AbilityText");
            textObj.transform.position = position + Vector3.up * 1.2f;
            
            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.fontSize = 36;
            textMesh.characterSize = 0.08f;
            textMesh.color = new Color(0.5f, 0.8f, 1f);
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.fontStyle = FontStyle.Bold;
            
            textObj.AddComponent<BillboardUI>();
            
            FloatingText ft = textObj.AddComponent<FloatingText>();
            ft.lifetime = 1.5f;
            ft.floatSpeed = 0.5f;
        }
    }

    /// <summary>
    /// Simple floating text animation
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        public float floatSpeed = 1f;
        public float lifetime = 1f;
        
        private float elapsed;
        private Vector3 startPos;
        private TextMesh textMesh;

        private void Start()
        {
            startPos = transform.position;
            textMesh = GetComponent<TextMesh>();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            
            // Float upward
            transform.position = startPos + Vector3.up * elapsed * floatSpeed;
            
            // Fade out
            if (textMesh != null)
            {
                Color c = textMesh.color;
                c.a = 1f - (elapsed / lifetime);
                textMesh.color = c;
            }
            
            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}