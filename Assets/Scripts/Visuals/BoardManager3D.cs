using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Crestforge.Core;
using Crestforge.Systems;
using Crestforge.Data;
using Crestforge.Combat;
using Crestforge.Networking;
using Crestforge.UI;
using Crestforge.Utilities;

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

        // Drag & drop (selection is now managed by GameUI)
        private UnitVisual3D draggedUnit;
        private Vector3 dragStartPos;
        private Vector2Int dragStartCoord;
        private bool isDragging;
        private bool isPendingDrag; // Waiting to see if this becomes a drag or tap
        private bool isDraggingFromBench;
        private bool pointerDownWasValid; // True when OnPointerDownMultiplayer processed a non-UI-blocked down
        private int benchDragIndex;
        private GameObject dragPlaceholder;
        private Vector3 dragStartMousePos;
        private const float TAP_THRESHOLD = 10f; // pixels

        // Hover
        private GameObject hoveredTile;
        private UnitVisual3D hoveredUnit;

        // Haptic feedback tracking during drag
        private Vector2Int lastHapticHexCoord = new Vector2Int(-999, -999);
        private int lastHapticBenchSlot = -999;

        // Bench visuals (slots are created by Game3DSetup)
        private Dictionary<int, UnitVisual3D> benchVisuals = new Dictionary<int, UnitVisual3D>();

        // Multiplayer support
        private bool IsMultiplayer => ServerGameState.Instance != null && ServerGameState.Instance.IsInGame;
        private ServerGameState serverState => ServerGameState.Instance;
        private HashSet<string> recentlyMovedUnits = new HashSet<string>(); // Units moved by drag, skip sync until server confirms
        private HashSet<string> recentlySoldUnits = new HashSet<string>(); // Units sold, skip sync until server confirms
        private HashSet<int> recentlyVacatedBenchSlots = new HashSet<int>(); // Bench slots that had units moved out, skip sync
        private HashSet<Vector2Int> recentlyVacatedBoardPositions = new HashSet<Vector2Int>(); // Board positions that had units moved out
        private HashSet<int> pendingPurchaseSlots = new HashSet<int>(); // Bench slots with optimistic purchase visuals

        /// <summary>
        /// Get the visual registry for the player's board
        /// </summary>
        private BoardVisualRegistry Registry => hexBoard?.Registry;

        // Dictionary accessors that delegate to registry - for drag/drop operations
        // These allow existing drag code to continue working while using the registry as backing store
        private Dictionary<string, UnitVisual3D> serverUnitVisuals =>
            Registry?.BoardVisualsInternal ?? _fallbackServerUnitVisuals;
        private Dictionary<int, UnitVisual3D> serverBenchVisuals =>
            Registry?.BenchVisualsInternal ?? _fallbackServerBenchVisuals;

        // Fallback dictionaries if registry not available yet
        private Dictionary<string, UnitVisual3D> _fallbackServerUnitVisuals = new Dictionary<string, UnitVisual3D>();
        private Dictionary<int, UnitVisual3D> _fallbackServerBenchVisuals = new Dictionary<int, UnitVisual3D>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            state = GameState.Instance;

            // Subscribe to server state updates to clear recently moved units
            if (ServerGameState.Instance != null)
            {
                ServerGameState.Instance.OnStateUpdated += OnServerStateUpdated;
                ServerGameState.Instance.OnBoardUpdated += OnServerStateUpdated;
            }

            // For multiplayer, DON'T set hexBoard here - wait for SetPlayerBoard to be called
            // This prevents using the wrong board when there are multiple boards
            if (!IsMultiplayer)
            {
                // Single player: Find or create board - prefer the player's board (HexBoard3D.Instance)
                if (hexBoard == null)
                {
                    // Use HexBoard3D.Instance which should be the player's board
                    hexBoard = HexBoard3D.Instance;

                    // Fallback to finding any board if Instance not set yet
                    if (hexBoard == null)
                    {
                        hexBoard = FindAnyObjectByType<HexBoard3D>();
                    }

                    // Create new board as last resort
                    if (hexBoard == null)
                    {
                        GameObject boardObj = new GameObject("HexBoard3D");
                        hexBoard = boardObj.AddComponent<HexBoard3D>();
                    }
                }
            }

            // Find or create camera
            if (cameraSetup == null)
            {
                cameraSetup = FindAnyObjectByType<IsometricCameraSetup>();
                if (cameraSetup == null && Camera.main != null)
                {
                    cameraSetup = Camera.main.gameObject.AddComponent<IsometricCameraSetup>();
                }
            }

            // Create drag placeholder
            CreateDragPlaceholder();

            // Note: Bench slots are created by Game3DSetup for all boards

            // Subscribe to combat events (if CombatManager exists)
            if (CombatManager.Instance != null && !subscribedToCombat)
            {
                SubscribeToCombatEvents();
                subscribedToCombat = true;
            }
        }

        /// <summary>
        /// Update the board reference to the player's board.
        /// Called by Game3DSetup after multi-board layout is created.
        /// </summary>
        public void SetPlayerBoard(HexBoard3D board)
        {
            if (board != null)
            {
                hexBoard = board;

                // Clear existing unit visuals so they get recreated at the correct position
                // This prevents units from walking from old positions to new positions
                if (IsMultiplayer)
                {
                    ClearMultiplayerVisuals();
                }

                // Refresh bench positions to align with the new board
                RefreshBenchPositions();

            }
            else
            {
                Debug.LogWarning("[BoardManager3D] SetPlayerBoard called with null board!");
            }
        }

        /// <summary>
        /// Called when server state is updated - clear recently moved units so sync can proceed
        /// </summary>
        private void OnServerStateUpdated()
        {
            recentlyMovedUnits.Clear();
            recentlySoldUnits.Clear();
            recentlyVacatedBenchSlots.Clear();
            recentlyVacatedBoardPositions.Clear();
            pendingPurchaseSlots.Clear();
            Registry?.ClearPendingMerges();
        }

        /// <summary>
        /// Create an optimistic visual for a purchased unit before server confirms.
        /// Returns -2 if unit was merged, -1 if failed, or bench slot index if new visual created.
        /// </summary>
        public int CreateOptimisticPurchaseVisual(ServerShopUnit shopUnit)
        {
            if (shopUnit == null || string.IsNullOrEmpty(shopUnit.unitId))
            {
                Debug.Log($"[OptimisticPurchase] Failed: shopUnit null or no unitId");
                return -1;
            }
            if (Registry == null)
            {
                Debug.Log($"[OptimisticPurchase] Failed: Registry is null");
                return -1;
            }
            if (serverState == null)
            {
                Debug.Log($"[OptimisticPurchase] Failed: serverState is null");
                return -1;
            }

            // Check for optimistic merge first
            bool isPlanning = serverState.phase == "planning";
            if (TryOptimisticMerge(shopUnit.unitId, 1, isPlanning))
            {
                Debug.Log($"[OptimisticPurchase] Optimistic merge for {shopUnit.name}");
                return -2; // Special value indicating merge happened
            }

            // Find first empty bench slot
            int targetSlot = -1;
            for (int i = 0; i < GameConstants.Player.BENCH_SIZE; i++)
            {
                bool serverEmpty = serverState.bench[i] == null;
                bool visualEmpty = !serverBenchVisuals.ContainsKey(i);
                if (serverEmpty && visualEmpty)
                {
                    targetSlot = i;
                    break;
                }
            }

            if (targetSlot < 0)
            {
                // Debug: show what's in each bench slot
                for (int i = 0; i < GameConstants.Player.BENCH_SIZE; i++)
                {
                    var benchUnit = serverState.bench[i];
                    bool hasVisual = serverBenchVisuals.ContainsKey(i);
                    if (benchUnit != null)
                    {
                        Debug.Log($"[OptimisticPurchase] Slot {i}: unitId={benchUnit.unitId ?? "null"}, instanceId={benchUnit.instanceId ?? "null"}, hasVisual={hasVisual}");
                    }
                    else
                    {
                        Debug.Log($"[OptimisticPurchase] Slot {i}: null, hasVisual={hasVisual}");
                    }
                }
                Debug.Log($"[OptimisticPurchase] Failed: No empty bench slot found. Bench size: {GameConstants.Player.BENCH_SIZE}, serverBenchVisuals count: {serverBenchVisuals.Count}");
                return -1;
            }

            Debug.Log($"[OptimisticPurchase] Creating visual for {shopUnit.name} at slot {targetSlot}");

            // Create temporary ServerUnitData for the visual
            var tempUnitData = new ServerUnitData
            {
                unitId = shopUnit.unitId,
                name = shopUnit.name,
                instanceId = $"pending_{System.Guid.NewGuid()}", // Temporary ID
                starLevel = 1,
                items = new System.Collections.Generic.List<ServerItemData>()
            };

            // Create the visual immediately
            UnitVisual3D visual = Registry.GetOrCreateBenchVisual(tempUnitData, targetSlot);
            if (visual != null)
            {
                Debug.Log($"[OptimisticPurchase] Visual created successfully at slot {targetSlot}");
                // Mark this slot as having a pending purchase (protect from sync)
                pendingPurchaseSlots.Add(targetSlot);
            }
            else
            {
                Debug.Log($"[OptimisticPurchase] Registry.GetOrCreateBenchVisual returned null");
            }

            return targetSlot;
        }

        /// <summary>
        /// Try to perform an optimistic merge for a unit being purchased.
        /// Returns true if merge happened, false otherwise.
        /// </summary>
        private bool TryOptimisticMerge(string unitId, int starLevel, bool includeBoard)
        {
            if (starLevel >= GameConstants.Units.MAX_STAR_LEVEL)
                return false;

            // Find a matching unit on bench
            UnitVisual3D matchVisual = null;
            string matchInstanceId = null;
            int matchBenchSlot = -1;

            // Check bench first
            for (int i = 0; i < GameConstants.Player.BENCH_SIZE; i++)
            {
                var benchUnit = serverState.bench[i];
                if (benchUnit != null && benchUnit.unitId == unitId && benchUnit.starLevel == starLevel)
                {
                    // Found match on bench, get its visual
                    if (serverBenchVisuals.TryGetValue(i, out UnitVisual3D visual) && visual != null)
                    {
                        matchVisual = visual;
                        matchInstanceId = benchUnit.instanceId;
                        matchBenchSlot = i;
                        break;
                    }
                }
            }

            // Check board during planning phase if no bench match
            if (matchVisual == null && includeBoard && serverState.board != null)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                    {
                        var boardUnit = serverState.board[x, y];
                        if (boardUnit != null && boardUnit.unitId == unitId && boardUnit.starLevel == starLevel)
                        {
                            // Found match on board, get its visual
                            if (Registry.BoardVisuals.TryGetValue(boardUnit.instanceId, out UnitVisual3D visual) && visual != null)
                            {
                                matchVisual = visual;
                                matchInstanceId = boardUnit.instanceId;
                                break;
                            }
                        }
                    }
                    if (matchVisual != null) break;
                }
            }

            if (matchVisual == null)
                return false;

            // Found a match - upgrade it!
            int newStarLevel = starLevel + 1;
            matchVisual.UpdateStars(newStarLevel);
            Registry?.MarkPendingMerge(matchInstanceId);
            Debug.Log($"[OptimisticMerge] Upgraded {unitId} from {starLevel} to {newStarLevel} star (instanceId: {matchInstanceId})");

            // Check for chain merge (e.g., buying a 1-star that creates a 2-star that merges with another 2-star)
            TryOptimisticMerge(unitId, newStarLevel, includeBoard);

            return true;
        }

        /// <summary>
        /// Clear all multiplayer unit visuals so they can be recreated at correct positions
        /// </summary>
        private void ClearMultiplayerVisuals()
        {
            if (Registry != null)
            {
                Registry.ClearAll();
            }
        }

        /// <summary>
        /// Refresh bench slot positions - bench slots are managed by Game3DSetup
        /// </summary>
        public void RefreshBenchPositions()
        {
            // Bench slots are created and managed by Game3DSetup
            // This method is kept for compatibility but does nothing
        }

        private void OnDestroy()
        {
            UnsubscribeFromCombatEvents();

            // Unsubscribe from server state events
            if (ServerGameState.Instance != null)
            {
                ServerGameState.Instance.OnStateUpdated -= OnServerStateUpdated;
                ServerGameState.Instance.OnBoardUpdated -= OnServerStateUpdated;
            }
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
                        // Play attack animation for ranged units too
                        attackerVis.PlayAttackAnimation(targetVis.transform.position);

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

                    // Spawn loot orb if this is a PvE enemy with loot
                    if (unit.team == Team.Enemy && unit.source.lootType != LootType.None)
                    {
                        LootOrb.Create(visual.transform.position, unit.source.lootType);
                    }

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
            // Multiplayer mode - sync from server state
            if (IsMultiplayer)
            {
                // Don't sync BOARD positions during combat - let ServerCombatVisualizer control unit positions
                // But ALWAYS sync bench visuals so bought units appear immediately
                var combatVisualizer = Crestforge.Systems.ServerCombatVisualizer.Instance;
                bool inServerCombat = combatVisualizer != null && combatVisualizer.isPlaying;
                bool inVictoryPose = combatVisualizer != null && combatVisualizer.isInVictoryPose;

                // Skip board sync during combat AND during victory pose (results phase)
                if (!inServerCombat && !inVictoryPose)
                {
                    SyncBoardVisualsMultiplayer(); // This also calls SyncBenchVisualsMultiplayer
                }
                else
                {
                    // During combat or victory pose, still sync bench visuals for purchased units
                    SyncBenchVisualsMultiplayer();
                }

                HandleInputMultiplayer();
                return;
            }

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

            // Sync bench visuals (units sitting on bench slots)
            SyncBenchVisuals();

            // Periodically clean up orphaned visuals (not every frame)
            orphanCleanupTimer += Time.deltaTime;
            if (orphanCleanupTimer >= ORPHAN_CLEANUP_INTERVAL)
            {
                orphanCleanupTimer = 0f;
                CleanupOrphanedVisuals();
            }
        }

        // Orphan cleanup runs periodically, not every frame
        private const float ORPHAN_CLEANUP_INTERVAL = 1.0f;
        private float orphanCleanupTimer = 0f;

        /// <summary>
        /// Find and destroy any UnitVisual3D that isn't tracked in unitVisuals
        /// </summary>
        private void CleanupOrphanedVisuals()
        {
            // Skip during drag to avoid destroying the drag visual
            if (isDragging) return;

            // Get all tracked visuals for quick lookup
            HashSet<UnitVisual3D> trackedVisuals = new HashSet<UnitVisual3D>();
            foreach (var kvp in unitVisuals)
            {
                if (kvp.Value != null)
                {
                    trackedVisuals.Add(kvp.Value);
                }
            }

            // Also include bench visuals in tracked set
            foreach (var kvp in benchVisuals)
            {
                if (kvp.Value != null)
                {
                    trackedVisuals.Add(kvp.Value);
                }
            }

            // Find all UnitVisual3D components that are children of this manager
            UnitVisual3D[] allVisuals = GetComponentsInChildren<UnitVisual3D>();
            foreach (var visual in allVisuals)
            {
                if (visual != null && !trackedVisuals.Contains(visual))
                {
                    // This visual isn't tracked - destroy it
                    Destroy(visual.gameObject);
                }
            }
        }

        // ============================================
        // MULTIPLAYER BOARD SYNC
        // ============================================

        /// <summary>
        /// Sync board visuals from server state (multiplayer mode)
        /// </summary>
        private void SyncBoardVisualsMultiplayer()
        {
            if (serverState == null) return;

            // Wait for the player's board to be assigned before creating visuals
            // This prevents creating units at wrong positions
            if (hexBoard == null || Registry == null)
            {
                // Don't fallback to HexBoard3D.Instance - wait for SetPlayerBoard to be called
                // This ensures we use the correct board for this player
                return;
            }

            // Also verify the board is the player's board
            if (!hexBoard.isPlayerBoard)
            {
                Debug.LogWarning("[BoardManager3D] hexBoard is not marked as player board, waiting for correct assignment");
                return;
            }

            HashSet<string> currentUnitIds = new HashSet<string>();

            // Sync board units
            if (serverState.board != null)
            {
                int boardUnitsFound = 0;
                for (int x = 0; x < 7; x++)
                {
                    for (int y = 0; y < 4; y++)
                    {
                        ServerUnitData serverUnit = serverState.board[x, y];
                        if (serverUnit != null && !string.IsNullOrEmpty(serverUnit.unitId))
                        {
                            boardUnitsFound++;
                            currentUnitIds.Add(serverUnit.instanceId);

                            // Skip if this unit is being dragged, was recently moved, or was recently sold
                            if ((isDragging || isPendingDrag) && draggedServerUnitInstanceId == serverUnit.instanceId)
                            {
                                continue;
                            }
                            if (recentlyMovedUnits.Contains(serverUnit.instanceId) || recentlySoldUnits.Contains(serverUnit.instanceId))
                            {
                                continue;
                            }
                            // Skip if this position was recently vacated (unit dragged away but server hasn't confirmed)
                            if (recentlyVacatedBoardPositions.Contains(new Vector2Int(x, y)))
                            {
                                continue;
                            }

                            // Use registry to get or create visual
                            Registry.GetOrCreateBoardVisual(serverUnit, x, y, false);
                        }
                    }
                }

            }

            // Also mark units as present if they were recently moved
            // (prevents deletion of visuals moved locally before server confirms)
            foreach (var kvp in serverUnitVisuals)
            {
                if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Key) &&
                    recentlyMovedUnits.Contains(kvp.Key))
                {
                    currentUnitIds.Add(kvp.Key);
                }
            }

            // Remove visuals for units no longer on board (via registry)
            Registry.SyncBoardVisuals(currentUnitIds);

            // Sync bench visuals
            SyncBenchVisualsMultiplayer();
        }

        // SyncServerUnitVisual and CreateUnitInstanceFromServerData removed - now handled by BoardVisualRegistry

        /// <summary>
        /// Sync bench visuals from server state
        /// </summary>
        private void SyncBenchVisualsMultiplayer()
        {
            if (serverState == null || serverState.bench == null || Registry == null) return;

            HashSet<int> occupiedSlots = new HashSet<int>();

            // Create/update visuals for bench units
            for (int i = 0; i < serverState.bench.Length && i < GameConstants.Player.BENCH_SIZE; i++)
            {
                ServerUnitData serverUnit = serverState.bench[i];
                if (serverUnit != null && !string.IsNullOrEmpty(serverUnit.unitId))
                {
                    occupiedSlots.Add(i);

                    // Skip if we're currently dragging this bench unit
                    if (isDragging && isDraggingFromBench && benchDragIndex == i)
                    {
                        continue;
                    }

                    // Also skip if this unit is being dragged by instanceId
                    if ((isDragging || isPendingDrag) && draggedServerUnitInstanceId == serverUnit.instanceId)
                    {
                        continue;
                    }

                    // Skip if this unit was recently moved or sold
                    if (recentlyMovedUnits.Contains(serverUnit.instanceId) || recentlySoldUnits.Contains(serverUnit.instanceId))
                    {
                        continue;
                    }
                    // Skip if this bench slot was recently vacated (unit dragged away but server hasn't confirmed)
                    if (recentlyVacatedBenchSlots.Contains(i))
                    {
                        continue;
                    }

                    // Use registry to get or create visual
                    Registry.GetOrCreateBenchVisual(serverUnit, i);
                }
            }

            // Also mark slots as occupied if they contain a recently-moved unit
            // (prevents deletion of visuals moved locally before server confirms)
            foreach (var kvp in serverBenchVisuals)
            {
                if (kvp.Value != null && kvp.Value.ServerInstanceId != null &&
                    recentlyMovedUnits.Contains(kvp.Value.ServerInstanceId))
                {
                    occupiedSlots.Add(kvp.Key);
                }
            }

            // Also mark slots with pending purchases as occupied
            // (prevents deletion of optimistic visuals before server confirms)
            foreach (int slot in pendingPurchaseSlots)
            {
                occupiedSlots.Add(slot);
            }

            // Remove visuals for empty bench slots (via registry)
            Registry.SyncBenchVisuals(occupiedSlots);
        }

        // Track last phase for detecting phase changes
        private string lastServerPhase = "planning";

        /// <summary>
        /// Handle input in multiplayer mode
        /// </summary>
        private void HandleInputMultiplayer()
        {
            if (serverState == null) return;

            // Detect phase changes and cancel any in-progress drag
            if (serverState.phase != lastServerPhase)
            {
                if (isDragging || isPendingDrag)
                {
                    CancelDragMultiplayer();
                }
                lastServerPhase = serverState.phase;
            }

            Vector3 mousePos = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            // Hover detection (simplified for now)
            UpdateHover(ray);

            // Mouse/touch input for drag & drop
            // Always allow completing/cancelling an in-progress drag
            // Starting new drags: bench units always allowed, board units only during planning
            if (Input.GetMouseButtonDown(0))
            {
                OnPointerDownMultiplayer(ray);
            }
            else if (Input.GetMouseButton(0) && (isDragging || isPendingDrag))
            {
                OnPointerDrag(ray);
            }

            // Process Up independently (not else-if) because the Device Simulator
            // can fire GetMouseButtonDown and GetMouseButtonUp on the same frame for taps.
            if (Input.GetMouseButtonUp(0))
            {
                OnPointerUpMultiplayer(ray);
            }
            // Fallback: detect button release when GetMouseButtonUp was missed.
            // The Device Simulator can drop GetMouseButtonUp events entirely for some taps.
            // Input.GetMouseButton(0) returns the current held state — if it's false and we
            // still have unresolved state from a PointerDown, the Up event was lost.
            else if (!Input.GetMouseButton(0) && (pointerDownWasValid || isPendingDrag))
            {
                OnPointerUpMultiplayer(ray);
            }
        }

        /// <summary>
        /// Cancel any in-progress drag and clean up state.
        /// Public wrapper for external callers (e.g., combat visualizer).
        /// </summary>
        public void CancelDrag()
        {
            if (isDragging || isPendingDrag)
            {
                CancelDragMultiplayer();
            }
        }

        /// <summary>
        /// Cancel any in-progress drag and clean up state
        /// </summary>
        private void CancelDragMultiplayer()
        {
            if (draggedUnit != null)
            {
                // Return to original position (don't destroy - server sync might not run during combat)
                draggedUnit.SetPosition(dragStartPos);

                // Re-enable collider in case it was disabled
                Collider col = draggedUnit.GetComponent<Collider>();
                if (col != null) col.enabled = true;

                // Restore tracking
                if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                {
                    if (isDraggingFromBench && benchDragIndex >= 0)
                    {
                        serverBenchVisuals[benchDragIndex] = draggedUnit;
                        // Clear the vacated slot tracking since visual is returning
                        recentlyVacatedBenchSlots.Remove(benchDragIndex);
                    }
                    else
                    {
                        serverUnitVisuals[draggedServerUnitInstanceId] = draggedUnit;
                        // Clear the vacated position tracking since visual is returning
                        if (dragStartCoord.x >= 0 && dragStartCoord.y >= 0)
                        {
                            recentlyVacatedBoardPositions.Remove(dragStartCoord);
                        }
                    }
                }
            }

            // Reset all drag state
            isDragging = false;
            isPendingDrag = false;
            isDraggingFromBench = false;
            draggedUnit = null;
            draggedServerUnitInstanceId = null;
            benchDragIndex = -1;

            if (dragPlaceholder != null)
            {
                dragPlaceholder.SetActive(false);
            }

            // Clear tile highlights
            hexBoard?.ClearHighlights();

            // Unblock camera and hide sell mode
            if (cameraSetup != null)
            {
                cameraSetup.inputBlocked = false;
            }
            GameUI.Instance?.HideSellMode();
        }

        /// <summary>
        /// Cancel any in-progress drag and clean up state (local mode)
        /// </summary>
        private void CancelDragLocal()
        {
            if (draggedUnit != null)
            {
                // Return to original position
                draggedUnit.SetPosition(dragStartPos);

                // If dragging from bench, restore bench visual tracking
                if (isDraggingFromBench && benchDragIndex >= 0)
                {
                    benchVisuals[benchDragIndex] = draggedUnit;
                }
            }

            // Reset all drag state
            isDragging = false;
            isPendingDrag = false;
            isDraggingFromBench = false;
            draggedUnit = null;
            benchDragIndex = -1;

            if (dragPlaceholder != null)
            {
                dragPlaceholder.SetActive(false);
            }

            // Clear tile highlights
            hexBoard?.ClearHighlights();

            // Unblock camera and hide sell mode
            if (cameraSetup != null)
            {
                cameraSetup.inputBlocked = false;
            }
            GameUI.Instance?.HideSellMode();
        }

        private ServerUnitData draggedServerUnit;
        private string draggedServerUnitInstanceId;

        /// <summary>
        /// Check if the pointer is over any UI element using explicit raycast.
        /// More reliable than IsPointerOverGameObject() which can return stale results
        /// in the Device Simulator and with simulated touch input.
        /// </summary>
        private bool IsPointerOverUI(out List<UnityEngine.EventSystems.RaycastResult> uiHits)
        {
            uiHits = new List<UnityEngine.EventSystems.RaycastResult>();
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return false;

            var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };
            eventSystem.RaycastAll(pointerData, uiHits);
            return uiHits.Count > 0;
        }

        private void OnPointerDownMultiplayer(Ray ray)
        {
            // Resolve stale pending drag as a tap. The Device Simulator can lose
            // GetMouseButtonUp events for some taps, leaving isPendingDrag set
            // with no way to resolve it. When the next PointerDown arrives,
            // treat the previous pending drag as a completed tap.
            if (isPendingDrag && !isDragging && draggedUnit != null)
            {
                SelectUnitMultiplayer(draggedUnit);
                isPendingDrag = false;
                isDraggingFromBench = false;
                draggedUnit = null;
                draggedServerUnitInstanceId = null;
                benchDragIndex = -1;
            }

            // Don't process 3D clicks when pointer is over UI (prevents grabbing bench units behind UI panels)
            if (IsPointerOverUI(out var uiHits))
            {
                pointerDownWasValid = false;
                return;
            }

            pointerDownWasValid = true;
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                // Check if we hit a unit visual
                UnitVisual3D hitUnit = hit.collider.GetComponentInParent<UnitVisual3D>();
                if (hitUnit != null)
                {
                    // Check bench visuals first - bench dragging is ALWAYS allowed
                    foreach (var kvp in serverBenchVisuals)
                    {
                        if (kvp.Value == hitUnit)
                        {
                            draggedServerUnitInstanceId = serverState.bench[kvp.Key]?.instanceId;
                            draggedUnit = hitUnit;
                            isPendingDrag = true;
                            isDraggingFromBench = true;
                            benchDragIndex = kvp.Key;
                            dragStartMousePos = Input.mousePosition;
                            dragStartPos = hitUnit.transform.position;
                            dragStartCoord = new Vector2Int(-1, -1); // Invalid coord for bench units
                            return;
                        }
                    }

                    // Board units - only allow dragging during planning phase
                    if (serverState.phase != "planning") return;

                    // Find the server unit this visual represents
                    foreach (var kvp in serverUnitVisuals)
                    {
                        if (kvp.Value == hitUnit)
                        {
                            draggedServerUnitInstanceId = kvp.Key;
                            draggedUnit = hitUnit;
                            isPendingDrag = true;
                            isDraggingFromBench = false;
                            dragStartMousePos = Input.mousePosition;
                            dragStartPos = hitUnit.transform.position;

                            // Find grid position of this unit for dragStartCoord
                            dragStartCoord = FindServerUnitGridPosition(kvp.Key);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find the grid position of a server unit by its instance ID
        /// </summary>
        private Vector2Int FindServerUnitGridPosition(string instanceId)
        {
            if (serverState == null || serverState.board == null) return new Vector2Int(-1, -1);

            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    if (serverState.board[x, y]?.instanceId == instanceId)
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
            return new Vector2Int(-1, -1);
        }

        private void OnPointerUpMultiplayer(Ray ray)
        {
            if (!isDragging && !isPendingDrag)
            {
                // Only process the simple-click path if we had a valid (non-UI-blocked) PointerDown.
                // Prevents phantom PointerUp events (common in Device Simulator after drag-equip)
                // from triggering unwanted 3D raycasts.
                if (!pointerDownWasValid)
                {
                    return;
                }
                pointerDownWasValid = false;

                // Don't process 3D clicks if clicking on UI (let UI handle its own clicks)
                if (IsPointerOverUI(out var uiResults))
                {
                    return;
                }

                // Check for click on unit to show tooltip (no pending drag started)
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 100f))
                {
                    UnitVisual3D hitUnit = hit.collider.GetComponentInParent<UnitVisual3D>();
                    if (hitUnit != null)
                    {
                        SelectUnitMultiplayer(hitUnit);
                    }
                    else
                    {
                        // Clicked on something that's not a unit - delegate to GameUI to clear selection
                        Crestforge.UI.GameUI.Instance?.HandleUnitClicked(null);
                    }
                }
                return;
            }

            // Handle pending drag that never activated (tap/click on unit)
            pointerDownWasValid = false;
            if (isPendingDrag && !isDragging && draggedUnit != null)
            {
                isPendingDrag = false;
                isDraggingFromBench = false;

                // This was a tap, not a drag - show tooltip for this unit
                SelectUnitMultiplayer(draggedUnit);
                draggedUnit = null;
                draggedServerUnitInstanceId = null;
                benchDragIndex = -1;
                return;
            }

            isPendingDrag = false;

            bool isPlanning = serverState.phase == "planning";
            bool sentServerAction = false; // Track if we sent an action that changes unit location

            if (isDragging && draggedUnit != null && !string.IsNullOrEmpty(draggedServerUnitInstanceId))
            {
                // Temporarily disable the dragged unit's collider so raycast hits the tile underneath
                Collider draggedCollider = draggedUnit.GetComponent<Collider>();
                if (draggedCollider != null) draggedCollider.enabled = false;

                // Check if dropped on UI sell zone
                // Bench units can be sold any time, board units only during planning
                bool canSell = IsOverSellZone() && (isDraggingFromBench || isPlanning);
                if (canSell)
                {
                    if (draggedCollider != null) draggedCollider.enabled = true;

                    // Track as sold to prevent flicker from sync
                    recentlySoldUnits.Add(draggedServerUnitInstanceId);

                    // Apply optimistic gold change (before server confirms)
                    if (draggedUnit != null && draggedUnit.unit != null)
                    {
                        int sellValue = draggedUnit.unit.GetSellValue();
                        GameUI.Instance?.ApplyOptimisticSellGold(sellValue);
                    }

                    // Remove from tracking and destroy visual immediately
                    if (isDraggingFromBench && benchDragIndex >= 0)
                    {
                        serverBenchVisuals.Remove(benchDragIndex);
                    }
                    else
                    {
                        serverUnitVisuals.Remove(draggedServerUnitInstanceId);
                    }

                    if (draggedUnit != null)
                    {
                        Destroy(draggedUnit.gameObject);
                        draggedUnit = null;
                    }

                    serverState.SellUnit(draggedServerUnitInstanceId);
                    AudioManager.Instance?.PlayPurchase();
                    sentServerAction = true;
                }
                else
                {
                    // Find drop target
                    RaycastHit hit;
                    bool hasHit = Physics.Raycast(ray, out hit, 100f);

                    // Get mouse world position for bench detection (more reliable than raycast hit point)
                    Vector3 mouseWorldPos = GetWorldPositionFromMouse();

                    // For bench unit dragging, handle bench-to-bench AND bench-to-board
                    if (isDraggingFromBench)
                    {
                        // Check if we hit a bench unit directly
                        int targetBenchSlot = -1;
                        if (hasHit)
                        {
                            UnitVisual3D hitBenchUnit = hit.collider.GetComponentInParent<UnitVisual3D>();
                            if (hitBenchUnit != null && hitBenchUnit != draggedUnit)
                            {
                                foreach (var kvp in serverBenchVisuals)
                                {
                                    if (kvp.Value == hitBenchUnit)
                                    {
                                        targetBenchSlot = kvp.Key;
                                        break;
                                    }
                                }
                            }
                        }

                        // Check if mouse is over bench area (use mouse position, not raycast hit)
                        bool isBenchArea = IsBenchDropArea(mouseWorldPos);
                        if (targetBenchSlot < 0 && isBenchArea)
                        {
                            targetBenchSlot = GetBenchSlotAtWorldPosition(mouseWorldPos);
                        }

                        // Bench-to-bench move if we found a valid bench target slot
                        if (targetBenchSlot >= 0 && targetBenchSlot != benchDragIndex)
                        {
                            // Move visuals directly for immediate feedback (both during planning and combat)
                            Vector3 targetPos = GetBenchSlotWorldPosition(targetBenchSlot);
                            targetPos.y = unitYOffset;
                            Vector3 sourcePos = GetBenchSlotWorldPosition(benchDragIndex);
                            sourcePos.y = unitYOffset;

                            // If there's a unit in target slot, swap positions
                            if (serverBenchVisuals.TryGetValue(targetBenchSlot, out UnitVisual3D targetVisual) && targetVisual != null)
                            {
                                targetVisual.SetPositionAndFaceCamera(sourcePos);
                                serverBenchVisuals[benchDragIndex] = targetVisual;

                                // Mark swapped unit as recently moved too
                                if (!string.IsNullOrEmpty(targetVisual.ServerInstanceId))
                                {
                                    recentlyMovedUnits.Add(targetVisual.ServerInstanceId);
                                }
                            }
                            else
                            {
                                serverBenchVisuals.Remove(benchDragIndex);
                            }

                            // Move dragged unit to target
                            draggedUnit.SetPositionAndFaceCamera(targetPos);
                            serverBenchVisuals[targetBenchSlot] = draggedUnit;

                            // Re-enable collider before nulling
                            if (draggedCollider != null) draggedCollider.enabled = true;

                            // Send server action but DON'T destroy visuals (we moved them directly)
                            serverState.MoveBenchUnit(draggedServerUnitInstanceId, targetBenchSlot);

                            // Mark as recently moved to prevent sync from moving it back
                            if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                            {
                                recentlyMovedUnits.Add(draggedServerUnitInstanceId);
                            }

                            // Null out draggedUnit so the restoration code doesn't overwrite our changes
                            draggedUnit = null;
                        }
                        else if (!isBenchArea && isPlanning)
                        {
                            // Not on bench area - check for hex tile placement (bench-to-board)
                            Vector2Int coord = new Vector2Int(-1, -1);
                            bool isEnemy = false;
                            bool foundTile = false;

                            // Try direct tile detection via raycast
                            if (hasHit && hexBoard != null && hexBoard.TryGetTileCoord(hit.collider.gameObject, out coord, out isEnemy))
                            {
                                foundTile = true;
                            }
                            // Fallback: find closest tile to drop position (more reliable for flat 2D hexes)
                            if (!foundTile && hexBoard != null)
                            {
                                Vector3 dropPos = draggedUnit.transform.position;
                                if (hexBoard.TryGetClosestTileCoord(dropPos, hexBoard.TileRadius * 2f, out coord, out isEnemy))
                                {
                                    foundTile = true;
                                }
                            }
                            // If we hit a board unit, find the tile it's on (for swapping)
                            if (!foundTile && hasHit && hexBoard != null)
                            {
                                UnitVisual3D hitUnit = hit.collider.GetComponentInParent<UnitVisual3D>();
                                if (hitUnit != null && hitUnit != draggedUnit)
                                {
                                    // Check if it's a board unit (not bench)
                                    foreach (var kvp in serverUnitVisuals)
                                    {
                                        if (kvp.Value == hitUnit)
                                        {
                                            coord = FindServerUnitGridPosition(kvp.Key);
                                            if (coord.x >= 0 && coord.y >= 0)
                                            {
                                                isEnemy = coord.y >= 4;
                                                foundTile = true;
                                            }
                                            break;
                                        }
                                    }
                                }
                            }

                            if (foundTile && !isEnemy && coord.y < 4)
                            {
                                // Check if there's a unit at target (for swapping)
                                string targetInstanceId = null;
                                if (serverState.board[coord.x, coord.y] != null)
                                {
                                    targetInstanceId = serverState.board[coord.x, coord.y].instanceId;
                                }

                                // If placing to empty tile, check if board is full (can't exceed level)
                                bool isSwap = !string.IsNullOrEmpty(targetInstanceId);
                                if (!isSwap && serverState.GetBoardUnitCount() >= serverState.level)
                                {
                                    // Board is full - return to bench
                                    draggedUnit.SetPositionAndFaceCamera(dragStartPos);
                                    serverBenchVisuals[benchDragIndex] = draggedUnit;
                                    if (draggedCollider != null) draggedCollider.enabled = true;
                                    draggedUnit = null;
                                }
                                else
                                {
                                    // Move visual directly for immediate feedback (no flicker)
                                    Vector3 targetPos = hexBoard.GetTileWorldPosition(coord.x, coord.y);
                                    targetPos.y = unitYOffset;

                                    if (isSwap && serverUnitVisuals.TryGetValue(targetInstanceId, out UnitVisual3D targetVisual) && targetVisual != null)
                                    {
                                        // Swap: move target board unit to bench position
                                        Vector3 benchPos = GetBenchSlotWorldPosition(benchDragIndex);
                                        benchPos.y = unitYOffset;
                                        targetVisual.SetPositionAndFaceCamera(benchPos);
                                        serverBenchVisuals[benchDragIndex] = targetVisual;
                                        serverUnitVisuals.Remove(targetInstanceId);

                                        // Mark swapped unit as recently moved too
                                        if (!string.IsNullOrEmpty(targetVisual.ServerInstanceId))
                                        {
                                            recentlyMovedUnits.Add(targetVisual.ServerInstanceId);
                                        }
                                        // Mark the board position as vacated (swapped unit moved out)
                                        recentlyVacatedBoardPositions.Add(coord);
                                    }
                                    else
                                    {
                                        // No swap needed, just clear bench slot
                                        serverBenchVisuals.Remove(benchDragIndex);
                                    }

                                    // Move dragged unit to board
                                    draggedUnit.SetPositionAndFaceCamera(targetPos);
                                    serverUnitVisuals[draggedServerUnitInstanceId] = draggedUnit;

                                    // Re-enable collider
                                    if (draggedCollider != null) draggedCollider.enabled = true;

                                    // Send place action to server
                                    serverState.PlaceUnit(draggedServerUnitInstanceId, coord.x, coord.y);

                                    // Mark as recently moved to prevent sync from moving it back
                                    if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                                    {
                                        recentlyMovedUnits.Add(draggedServerUnitInstanceId);
                                    }

                                    // Null out draggedUnit so it doesn't get destroyed
                                    draggedUnit = null;
                                }
                            }
                            else
                            {
                                // Invalid placement - return to original bench position
                                draggedUnit.SetPositionAndFaceCamera(dragStartPos);
                                serverBenchVisuals[benchDragIndex] = draggedUnit;
                                if (draggedCollider != null) draggedCollider.enabled = true;
                                draggedUnit = null;
                            }
                        }
                        else if (isBenchArea && targetBenchSlot == benchDragIndex)
                        {
                            // Dropped on same bench slot - return to original
                            draggedUnit.SetPositionAndFaceCamera(dragStartPos);
                            serverBenchVisuals[benchDragIndex] = draggedUnit;
                            if (draggedCollider != null) draggedCollider.enabled = true;
                            draggedUnit = null;
                        }
                        else
                        {
                            // No valid drop target - return to original bench position
                            draggedUnit.SetPositionAndFaceCamera(dragStartPos);
                            serverBenchVisuals[benchDragIndex] = draggedUnit;
                            if (draggedCollider != null) draggedCollider.enabled = true;
                            draggedUnit = null;
                        }
                    }
                    else if (hasHit)
                    {
                        // Board unit dragging - check hex tiles first, then bench
                        Vector2Int coord = new Vector2Int(-1, -1);
                        bool isEnemy = false;
                        bool foundTile = false;

                        // First try direct tile detection via raycast
                        if (isPlanning && hexBoard != null && hexBoard.TryGetTileCoord(hit.collider.gameObject, out coord, out isEnemy))
                        {
                            foundTile = true;
                        }
                        // Fallback: find closest tile to drop position (more reliable for flat 2D hexes)
                        if (!foundTile && isPlanning && hexBoard != null)
                        {
                            Vector3 dropPos = draggedUnit.transform.position;
                            if (hexBoard.TryGetClosestTileCoord(dropPos, hexBoard.TileRadius * 2f, out coord, out isEnemy))
                            {
                                foundTile = true;
                            }
                        }
                        // If we hit a unit, find the tile it's on (for swapping)
                        if (!foundTile && isPlanning && hexBoard != null)
                        {
                            UnitVisual3D hitUnit = hit.collider.GetComponentInParent<UnitVisual3D>();
                            if (hitUnit != null && hitUnit != draggedUnit)
                            {
                                // Find this unit's grid position
                                foreach (var kvp in serverUnitVisuals)
                                {
                                    if (kvp.Value == hitUnit)
                                    {
                                        coord = FindServerUnitGridPosition(kvp.Key);
                                        if (coord.x >= 0 && coord.y >= 0)
                                        {
                                            isEnemy = coord.y >= 4;
                                            foundTile = true;
                                        }
                                        break;
                                    }
                                }
                            }
                        }

                        if (foundTile)
                        {
                            if (!isEnemy && coord.y < 4) // Player rows only
                            {
                                // Move visual directly for immediate feedback (no flicker)
                                Vector3 targetPos = hexBoard.GetTileWorldPosition(coord.x, coord.y);
                                targetPos.y = unitYOffset;

                                // If there's a unit at target position, swap them
                                string targetInstanceId = null;
                                if (serverState.board[coord.x, coord.y] != null)
                                {
                                    targetInstanceId = serverState.board[coord.x, coord.y].instanceId;
                                }

                                if (!string.IsNullOrEmpty(targetInstanceId) && serverUnitVisuals.TryGetValue(targetInstanceId, out UnitVisual3D targetVisual) && targetVisual != null)
                                {
                                    // Swap: move target unit to drag start position
                                    targetVisual.SetPositionAndFaceCamera(dragStartPos);

                                    // Mark swapped unit as recently moved too
                                    recentlyMovedUnits.Add(targetInstanceId);
                                }

                                // Move dragged unit to target
                                draggedUnit.SetPositionAndFaceCamera(targetPos);

                                // Update tracking - remove from old position tracking
                                if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                                {
                                    serverUnitVisuals[draggedServerUnitInstanceId] = draggedUnit;
                                }

                                // Re-enable collider
                                if (draggedCollider != null) draggedCollider.enabled = true;

                                // Send place action to server
                                serverState.PlaceUnit(draggedServerUnitInstanceId, coord.x, coord.y);

                                // Mark as recently moved to prevent sync from moving it back
                                if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                                {
                                    recentlyMovedUnits.Add(draggedServerUnitInstanceId);
                                }

                                // Null out draggedUnit so it doesn't get destroyed
                                draggedUnit = null;
                            }
                            else
                            {
                                // Return to original position (enemy row)
                                draggedUnit.SetPosition(dragStartPos);
                            }
                        }
                        else
                        {
                            // Check if we hit a bench unit directly
                            int targetSlot = -1;
                            UnitVisual3D hitBenchUnit = hit.collider.GetComponentInParent<UnitVisual3D>();
                            if (hitBenchUnit != null && hitBenchUnit != draggedUnit)
                            {
                                foreach (var kvp in serverBenchVisuals)
                                {
                                    if (kvp.Value == hitBenchUnit)
                                    {
                                        targetSlot = kvp.Key;
                                        break;
                                    }
                                }
                            }

                            // Check if dropped on bench area
                            bool isBenchAreaHit = IsBenchDropArea(hit.point);
                            bool isBenchAreaMouse = IsBenchDropArea(mouseWorldPos);
                            bool droppedOnBench = targetSlot >= 0 || isBenchAreaHit || isBenchAreaMouse;

                            if (droppedOnBench && isPlanning)
                            {
                                // If we didn't hit a bench unit directly, get slot from position
                                if (targetSlot < 0)
                                {
                                    targetSlot = GetBenchSlotAtWorldPosition(mouseWorldPos);
                                    if (targetSlot < 0)
                                    {
                                        targetSlot = GetBenchSlotAtWorldPosition(hit.point);
                                    }
                                }

                                // Board-to-bench move: only during planning
                                if (targetSlot >= 0)
                                {
                                    // Move visual directly for immediate feedback (no flicker)
                                    Vector3 benchPos = GetBenchSlotWorldPosition(targetSlot);
                                    benchPos.y = unitYOffset;

                                    // If target slot has a unit, swap to board position
                                    if (serverBenchVisuals.TryGetValue(targetSlot, out UnitVisual3D targetBenchVisual) && targetBenchVisual != null)
                                    {
                                        // Move bench unit to the board position we're vacating
                                        targetBenchVisual.SetPositionAndFaceCamera(dragStartPos);
                                        serverUnitVisuals[targetBenchVisual.ServerInstanceId] = targetBenchVisual;
                                        serverBenchVisuals.Remove(targetSlot);

                                        // Mark swapped unit as recently moved too
                                        if (!string.IsNullOrEmpty(targetBenchVisual.ServerInstanceId))
                                        {
                                            recentlyMovedUnits.Add(targetBenchVisual.ServerInstanceId);
                                        }
                                        // Mark the bench slot as vacated (swapped unit moved out)
                                        recentlyVacatedBenchSlots.Add(targetSlot);
                                    }

                                    // Move dragged board unit to bench
                                    draggedUnit.SetPositionAndFaceCamera(benchPos);
                                    serverBenchVisuals[targetSlot] = draggedUnit;

                                    // Remove from board tracking
                                    if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                                    {
                                        serverUnitVisuals.Remove(draggedServerUnitInstanceId);
                                    }

                                    // Re-enable collider
                                    if (draggedCollider != null) draggedCollider.enabled = true;

                                    // Send server action
                                    serverState.BenchUnit(draggedServerUnitInstanceId, targetSlot);

                                    // Mark as recently moved to prevent sync from moving it back
                                    if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                                    {
                                        recentlyMovedUnits.Add(draggedServerUnitInstanceId);
                                    }

                                    // Null out draggedUnit so it doesn't get destroyed
                                    draggedUnit = null;
                                }
                                else
                                {
                                    // Couldn't determine bench slot - return to original
                                    draggedUnit.SetPosition(dragStartPos);
                                }
                            }
                            else
                            {
                                // Return to original position
                                draggedUnit.SetPosition(dragStartPos);
                            }
                        }
                    }
                    else
                    {
                        // No raycast hit - try fallback tile detection first, then check bench
                        bool foundTileNoHit = false;
                        Vector2Int coordNoHit = new Vector2Int(-1, -1);
                        bool isEnemyNoHit = false;

                        // Fallback: find closest tile to drop position (for flat 2D hexes)
                        if (isPlanning && hexBoard != null)
                        {
                            Vector3 dropPos = draggedUnit.transform.position;
                            if (hexBoard.TryGetClosestTileCoord(dropPos, hexBoard.TileRadius * 2f, out coordNoHit, out isEnemyNoHit))
                            {
                                foundTileNoHit = !isEnemyNoHit && coordNoHit.y < 4;
                            }
                        }

                        if (foundTileNoHit)
                        {
                            // Found a valid player tile via fallback detection
                            Vector3 targetPos = hexBoard.GetTileWorldPosition(coordNoHit.x, coordNoHit.y);
                            targetPos.y = unitYOffset;

                            // Check for swap
                            string targetInstanceId = null;
                            if (serverState.board[coordNoHit.x, coordNoHit.y] != null)
                            {
                                targetInstanceId = serverState.board[coordNoHit.x, coordNoHit.y].instanceId;
                            }

                            if (!string.IsNullOrEmpty(targetInstanceId) && serverUnitVisuals.TryGetValue(targetInstanceId, out UnitVisual3D targetVisual) && targetVisual != null)
                            {
                                // Swap: move target unit to drag start position
                                targetVisual.SetPositionAndFaceCamera(dragStartPos);
                                recentlyMovedUnits.Add(targetInstanceId);
                            }

                            // Move dragged unit to target
                            draggedUnit.SetPositionAndFaceCamera(targetPos);

                            // Update tracking
                            if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                            {
                                serverUnitVisuals[draggedServerUnitInstanceId] = draggedUnit;
                            }

                            // Re-enable collider
                            if (draggedCollider != null) draggedCollider.enabled = true;

                            // Send place action to server
                            serverState.PlaceUnit(draggedServerUnitInstanceId, coordNoHit.x, coordNoHit.y);

                            // Mark as recently moved
                            if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                            {
                                recentlyMovedUnits.Add(draggedServerUnitInstanceId);
                            }

                            draggedUnit = null;
                        }
                        // Check if we're over the bench area (for board-to-bench)
                        else if (IsBenchDropArea(mouseWorldPos) && isPlanning)
                        {
                            int targetSlot = GetBenchSlotAtWorldPosition(mouseWorldPos);
                            if (targetSlot >= 0)
                            {
                                // Move visual directly for immediate feedback (no flicker)
                                Vector3 benchPos = GetBenchSlotWorldPosition(targetSlot);
                                benchPos.y = unitYOffset;

                                // If target slot has a unit, swap to board position
                                if (serverBenchVisuals.TryGetValue(targetSlot, out UnitVisual3D targetBenchVisual) && targetBenchVisual != null)
                                {
                                    // Move bench unit to the board position we're vacating
                                    targetBenchVisual.SetPositionAndFaceCamera(dragStartPos);
                                    serverUnitVisuals[targetBenchVisual.ServerInstanceId] = targetBenchVisual;
                                    serverBenchVisuals.Remove(targetSlot);

                                    // Mark swapped unit as recently moved too
                                    if (!string.IsNullOrEmpty(targetBenchVisual.ServerInstanceId))
                                    {
                                        recentlyMovedUnits.Add(targetBenchVisual.ServerInstanceId);
                                    }
                                    // Mark the bench slot as vacated (swapped unit moved out)
                                    recentlyVacatedBenchSlots.Add(targetSlot);
                                }

                                // Move dragged board unit to bench
                                draggedUnit.SetPositionAndFaceCamera(benchPos);
                                serverBenchVisuals[targetSlot] = draggedUnit;

                                // Remove from board tracking
                                if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                                {
                                    serverUnitVisuals.Remove(draggedServerUnitInstanceId);
                                }

                                // Re-enable collider
                                Collider col = draggedUnit.GetComponent<Collider>();
                                if (col != null) col.enabled = true;

                                // Send server action
                                serverState.BenchUnit(draggedServerUnitInstanceId, targetSlot);

                                // Mark as recently moved to prevent sync from moving it back
                                if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                                {
                                    recentlyMovedUnits.Add(draggedServerUnitInstanceId);
                                }

                                // Null out draggedUnit so it doesn't get destroyed
                                draggedUnit = null;
                            }
                            else
                            {
                                // Couldn't determine bench slot - return to original
                                draggedUnit.SetPosition(dragStartPos);
                            }
                        }
                        else
                        {
                            // No valid drop target - return to original position
                            draggedUnit.SetPosition(dragStartPos);
                        }
                    }

                    // Re-enable collider
                    if (draggedCollider != null) draggedCollider.enabled = true;
                }
            }

            // Only destroy the visual if we sent a server action that changes unit location
            // Server sync will recreate it at the correct position
            if (sentServerAction && draggedUnit != null)
            {
                // Remove from tracking dictionaries if present
                string instanceIdToRemove = draggedServerUnitInstanceId;
                if (!string.IsNullOrEmpty(instanceIdToRemove) && serverUnitVisuals.ContainsKey(instanceIdToRemove))
                {
                    serverUnitVisuals.Remove(instanceIdToRemove);
                }

                Destroy(draggedUnit.gameObject);
            }
            else if (draggedUnit != null)
            {
                // Re-enable collider
                Collider col = draggedUnit.GetComponent<Collider>();
                if (col != null) col.enabled = true;

                // Restore visual to tracking (it was removed during drag start)
                if (isDraggingFromBench && benchDragIndex >= 0)
                {
                    serverBenchVisuals[benchDragIndex] = draggedUnit;
                }
                else if (!string.IsNullOrEmpty(draggedServerUnitInstanceId))
                {
                    serverUnitVisuals[draggedServerUnitInstanceId] = draggedUnit;
                }
            }

            // Reset drag state
            isDragging = false;
            isDraggingFromBench = false;
            draggedUnit = null;
            draggedServerUnitInstanceId = null;

            if (dragPlaceholder != null)
            {
                dragPlaceholder.SetActive(false);
            }

            // Clear tile highlights
            hexBoard?.ClearHighlights();

            // Unblock camera and hide sell mode
            if (cameraSetup != null)
            {
                cameraSetup.inputBlocked = false;
            }
            GameUI.Instance?.HideSellMode();

            // Clear hover highlights
            hexBoard?.ClearHighlights();
        }

        private bool IsOverSellZone()
        {
            // Check if pointer is off the bottom of the screen (mobile drag-off-screen to sell)
            if (Input.mousePosition.y < 0)
            {
                return true;
            }

            // Check if mouse is over the UI sell overlay
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return false;

            var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };

            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                if (result.gameObject.GetComponent<SellDropZone>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBenchDropArea(Vector3 worldPoint)
        {
            // Delegate to registry if available
            if (Registry != null)
            {
                return Registry.IsBenchDropArea(worldPoint);
            }

            // Fallback check
            if (hexBoard == null) return false;

            int benchSize = GameConstants.Player.BENCH_SIZE;
            for (int i = 0; i < benchSize; i++)
            {
                Vector3 slotPos = GetBenchSlotWorldPosition(i);
                float dist = Vector2.Distance(
                    new Vector2(worldPoint.x, worldPoint.z),
                    new Vector2(slotPos.x, slotPos.z)
                );
                if (dist < 1.2f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Sync 3D visuals for units on the bench
        /// </summary>
        private void SyncBenchVisuals()
        {
            if (state == null) return;

            // Trigger bench initialization if needed
            _ = state.GetBenchUnitCount();

            if (state.bench == null || state.bench.Length == 0)
            {
                return;
            }

            // Track which bench slots have units
            HashSet<int> occupiedSlots = new HashSet<int>();

            // Create/update visuals for bench units
            for (int i = 0; i < state.bench.Length; i++)
            {
                UnitInstance unit = state.bench[i];
                if (unit == null) continue;

                occupiedSlots.Add(i);
                Vector3 slotPos = GetBenchSlotWorldPosition(i);
                slotPos.y = unitYOffset;

                // Check if we already have a visual for this slot
                if (benchVisuals.TryGetValue(i, out UnitVisual3D visual) && visual != null)
                {
                    // Check if it's the same unit
                    if (visual.unit == unit)
                    {
                        // Same unit - update position if needed (shouldn't move much)
                        if (Vector3.Distance(visual.transform.position, slotPos) > 0.1f)
                        {
                            // Don't move if we're dragging this unit
                            if (!(isDragging && isDraggingFromBench && benchDragIndex == i))
                            {
                                visual.SetPosition(slotPos);
                            }
                        }
                    }
                    else
                    {
                        // Different unit in this slot - destroy old, create new
                        Destroy(visual.gameObject);
                        benchVisuals[i] = CreateBenchUnitVisual(unit, slotPos, i);
                    }
                }
                else
                {
                    // No visual for this slot - create one
                    // But skip if we're currently dragging from this slot
                    if (isDragging && isDraggingFromBench && benchDragIndex == i)
                    {
                        continue;
                    }
                    benchVisuals[i] = CreateBenchUnitVisual(unit, slotPos, i);
                }
            }

            // Remove visuals for empty bench slots
            List<int> slotsToRemove = new List<int>();
            foreach (var kvp in benchVisuals)
            {
                if (!occupiedSlots.Contains(kvp.Key))
                {
                    slotsToRemove.Add(kvp.Key);
                }
            }
            foreach (int slot in slotsToRemove)
            {
                if (benchVisuals.TryGetValue(slot, out UnitVisual3D visual) && visual != null)
                {
                    Destroy(visual.gameObject);
                }
                benchVisuals.Remove(slot);
            }
        }

        /// <summary>
        /// Create a visual for a unit on the bench
        /// </summary>
        private UnitVisual3D CreateBenchUnitVisual(UnitInstance unit, Vector3 position, int slotIndex)
        {
            GameObject visualObj = new GameObject($"BenchUnit_{unit.template.unitName}_{slotIndex}");
            visualObj.transform.SetParent(transform);
            visualObj.transform.position = position; // Set position BEFORE adding component to prevent walking from origin
            UnitVisual3D visual = visualObj.AddComponent<UnitVisual3D>();
            visual.Initialize(unit, false);
            visual.SetPosition(position); // Also call SetPosition to set targetPosition correctly
            return visual;
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
                // Clean up any stale entry
                if (unitVisuals.ContainsKey(unit))
                {
                    unitVisuals.Remove(unit);
                }

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

        // Track last phase for detecting phase changes (local mode)
        private GamePhase lastLocalPhase = GamePhase.Planning;

        /// <summary>
        /// Handle mouse/touch input for unit interaction
        /// </summary>
        private void HandleInput()
        {
            // Detect phase changes and cancel any in-progress drag
            if (state.round.phase != lastLocalPhase)
            {
                if (isDragging || isPendingDrag)
                {
                    CancelDragLocal();
                }
                lastLocalPhase = state.round.phase;
            }

            Vector3 mousePos = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            // Hover detection
            UpdateHover(ray);

            // Mouse/touch input
            if (Input.GetMouseButtonDown(0))
            {
                OnPointerDown(ray);
            }
            else if (Input.GetMouseButton(0) && (isDragging || isPendingDrag))
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

                if (unitVis != null && unitVis.unit != null && !unitVis.isEnemy)
                {
                    // Check if this is a bench unit
                    int benchIndex = GetBenchIndexForVisual(unitVis);
                    if (benchIndex >= 0)
                    {
                        // Bench rearranging is ALWAYS allowed
                        isPendingDrag = true;
                        isDraggingFromBench = true;
                        benchDragIndex = benchIndex;
                        draggedUnit = unitVis;
                        dragStartPos = unitVis.transform.position;
                        dragStartMousePos = Input.mousePosition;
                    }
                    else
                    {
                        // Set up pending board drag (visual drag starts when mouse moves)
                        if (state.round.phase != GamePhase.Planning) return;

                        draggedUnit = unitVis;
                        dragStartPos = unitVis.transform.position;
                        dragStartMousePos = Input.mousePosition;
                        benchDragIndex = -1;

                        // Find current grid position
                        if (TryGetUnitGridPosition(unitVis.unit, out int x, out int y))
                        {
                            dragStartCoord = new Vector2Int(x, y);
                            isPendingDrag = true;
                            isDraggingFromBench = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Activate the visual drag state (called when mouse moves past threshold)
        /// </summary>
        private void ActivateDrag()
        {
            if (!isPendingDrag || draggedUnit == null) return;

            isPendingDrag = false;
            isDragging = true;

            // Initialize haptic tracking to current position so pickup doesn't trigger feedback
            // Only trigger when moving to a NEW slot/hex
            if (isDraggingFromBench)
            {
                lastHapticBenchSlot = benchDragIndex;
                lastHapticHexCoord = new Vector2Int(-999, -999);
            }
            else
            {
                lastHapticHexCoord = dragStartCoord;
                lastHapticBenchSlot = -999;
            }

            // Block camera input
            if (cameraSetup != null)
            {
                cameraSetup.inputBlocked = true;
            }

            // Show sell mode in UI
            if (draggedUnit.unit != null)
            {
                GameUI.Instance?.ShowSellMode(draggedUnit.unit);
            }

            if (isDraggingFromBench)
            {
                // Remove from bench visuals tracking while dragging
                if (IsMultiplayer)
                {
                    serverBenchVisuals.Remove(benchDragIndex);
                    // Mark bench slot as vacated to prevent sync from creating a new visual there
                    recentlyVacatedBenchSlots.Add(benchDragIndex);
                }
                else
                {
                    benchVisuals.Remove(benchDragIndex);
                }
            }
            else
            {
                // Show placeholder at original position for board units
                if (dragPlaceholder != null)
                {
                    dragPlaceholder.transform.position = dragStartPos;
                    dragPlaceholder.SetActive(true);
                }
                // Mark board position as vacated (for multiplayer)
                if (IsMultiplayer && dragStartCoord.x >= 0 && dragStartCoord.y >= 0)
                {
                    recentlyVacatedBoardPositions.Add(dragStartCoord);
                }
            }

            // Highlight valid placement tiles
            HighlightValidTiles();
        }

        /// <summary>
        /// Get the bench slot index for a visual, or -1 if not a bench unit
        /// </summary>
        private int GetBenchIndexForVisual(UnitVisual3D visual)
        {
            foreach (var kvp in benchVisuals)
            {
                if (kvp.Value == visual)
                {
                    return kvp.Key;
                }
            }
            return -1;
        }

        private void OnPointerDrag(Ray ray)
        {
            // Check if pending drag should become active drag
            if (isPendingDrag && draggedUnit != null)
            {
                float dragDistance = Vector3.Distance(Input.mousePosition, dragStartMousePos);
                if (dragDistance >= TAP_THRESHOLD)
                {
                    ActivateDrag();
                }
                else
                {
                    // Not yet past threshold, don't move the unit
                    return;
                }
            }

            if (!isDragging || draggedUnit == null) return;

            // Get ground position
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, unitYOffset, 0));
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPos = ray.GetPoint(distance);
                // Use SetPosition to update both position and targetPosition
                // This prevents the visual from trying to animate back if destroyed with delay
                draggedUnit.SetPosition(worldPos);

                // Check for bench slot hover and trigger haptic
                if (IsBenchDropArea(worldPos))
                {
                    int benchSlot = GetBenchSlotAtWorldPosition(worldPos);
                    if (benchSlot >= 0 && benchSlot != lastHapticBenchSlot)
                    {
                        lastHapticBenchSlot = benchSlot;
                        // Don't reset lastHapticHexCoord here - causes infinite loops
                        // when position flickers between bench area and hex area boundaries
                        HapticFeedback.LightTap();
                    }
                }
            }

            // Update hover preview
            UpdateHoverPreview();
        }

        private void OnPointerUp(Ray ray)
        {
            // Handle pending drag that never activated (tap/click)
            if (isPendingDrag && draggedUnit != null)
            {
                // This was a tap, not a drag - treat as unit selection
                isPendingDrag = false;
                isDraggingFromBench = false;

                SelectUnit(draggedUnit);
                draggedUnit = null;
                return;
            }

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

            // Handle bench drags separately
            if (isDraggingFromBench)
            {
                EndBenchDrag();
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

            if (draggedUnit == null)
            {
                Crestforge.UI.GameUI.Instance?.HideSellMode();
                return;
            }

            // Check if dropped on sell zone first
            if (IsPointerOverSellZone())
            {
                // Sell the unit from the board
                UnitInstance unit = draggedUnit.unit;

                // Remove from board
                state.playerBoard[dragStartCoord.x, dragStartCoord.y] = null;

                // Remove visual from tracking
                if (unitVisuals.ContainsKey(unit))
                {
                    unitVisuals.Remove(unit);
                }

                // Destroy visual
                Destroy(draggedUnit.gameObject);

                // Sell and get gold
                Crestforge.UI.GameUI.Instance?.TrySellUnit();

                draggedUnit = null;
                Crestforge.UI.GameUI.Instance?.HideSellMode();
                return;
            }

            // Temporarily disable the dragged unit's collider so raycast hits the tile
            Collider draggedCollider = draggedUnit.GetComponent<Collider>();
            if (draggedCollider != null) draggedCollider.enabled = false;

            // Find target tile
            GameObject targetTile = hexBoard.GetTileAtScreenPosition(Input.mousePosition);

            // Check if over a bench slot
            Vector3 worldPos = GetWorldPositionFromMouse();
            int targetBenchSlot = GetBenchSlotAtWorldPosition(worldPos);

            // Re-enable collider
            if (draggedCollider != null) draggedCollider.enabled = true;

            // Check if dropped on a bench slot (board unit -> bench)
            if (targetBenchSlot >= 0 && targetBenchSlot < state.bench.Length)
            {
                UnitInstance boardUnit = draggedUnit.unit;

                // Check if there's a unit in that bench slot
                UnitInstance benchUnit = state.bench[targetBenchSlot];
                if (benchUnit != null)
                {
                    // Swap board unit with bench unit
                    // Get bench visual BEFORE modifying tracking
                    benchVisuals.TryGetValue(targetBenchSlot, out UnitVisual3D benchVisual);

                    // Move board unit to bench
                    state.playerBoard[dragStartCoord.x, dragStartCoord.y] = null;
                    boardUnit.isOnBoard = false;
                    state.bench[targetBenchSlot] = boardUnit;

                    // Move bench unit to board
                    state.playerBoard[dragStartCoord.x, dragStartCoord.y] = benchUnit;
                    benchUnit.isOnBoard = true;
                    benchUnit.boardPosition = dragStartCoord;

                    // Update board unit visual (board -> bench)
                    Vector3 benchPos = GetBenchSlotWorldPosition(targetBenchSlot);
                    benchPos.y = unitYOffset;
                    draggedUnit.SetPosition(benchPos);
                    unitVisuals.Remove(boardUnit);
                    benchVisuals[targetBenchSlot] = draggedUnit;

                    // Update bench unit visual (bench -> board)
                    if (benchVisual != null && benchVisual != draggedUnit)
                    {
                        Vector3 boardPos = hexBoard.GetTileWorldPosition(dragStartCoord.x, dragStartCoord.y);
                        boardPos.y = unitYOffset;
                        benchVisual.SetPosition(boardPos);
                        unitVisuals[benchUnit] = benchVisual;
                    }

                    // Refresh UI and recalculate traits
                    state.RecalculateTraits();
                    Crestforge.UI.GameUI.Instance?.RefreshBench();
                }
                else
                {
                    // Empty bench slot - move board unit to bench at this slot
                    UnitInstance unit = draggedUnit.unit;

                    // Remove from board
                    state.playerBoard[dragStartCoord.x, dragStartCoord.y] = null;
                    unit.isOnBoard = false;

                    // Place in target bench slot
                    state.bench[targetBenchSlot] = unit;

                    // Update visual
                    Vector3 benchPos = GetBenchSlotWorldPosition(targetBenchSlot);
                    benchPos.y = unitYOffset;
                    draggedUnit.SetPosition(benchPos);
                    unitVisuals.Remove(unit);
                    benchVisuals[targetBenchSlot] = draggedUnit;

                    // Refresh UI and recalculate traits
                    state.RecalculateTraits();
                    Crestforge.UI.GameUI.Instance?.RefreshBench();
                }
            }
            else
            {
                // Try raycast first, then fall back to closest tile detection
                Vector2Int coord;
                bool isEnemy;
                bool foundTile = false;

                if (targetTile != null && hexBoard.TryGetTileCoord(targetTile, out coord, out isEnemy))
                {
                    foundTile = true;
                }
                else
                {
                    // Fallback: find closest tile to drop position (use 2x radius to ensure no gaps)
                    foundTile = hexBoard.TryGetClosestTileCoord(worldPos, hexBoard.TileRadius * 2f, out coord, out isEnemy);
                }

                if (foundTile)
                {
                    // Only allow placement in player rows
                    if (coord.y < hexBoard.playerRows)
                    {
                        // Check if there's a unit to swap with
                        UnitInstance swappedUnit = state.playerBoard[coord.x, coord.y];
                        UnitVisual3D swappedVisual = null;
                        if (swappedUnit != null)
                        {
                            unitVisuals.TryGetValue(swappedUnit, out swappedVisual);
                        }

                        // Try to place unit at new position
                        bool success = TryMoveUnit(draggedUnit.unit, dragStartCoord.x, dragStartCoord.y, coord.x, coord.y);

                        if (success)
                        {
                            // Teleport dragged unit visual to new position
                            Vector3 newPos = hexBoard.GetTileWorldPosition(coord.x, coord.y);
                            newPos.y = unitYOffset;
                            draggedUnit.SetPosition(newPos);

                            // If we swapped, teleport the other unit's visual to the old position
                            if (swappedVisual != null)
                            {
                                Vector3 oldPos = hexBoard.GetTileWorldPosition(dragStartCoord.x, dragStartCoord.y);
                                oldPos.y = unitYOffset;
                                swappedVisual.SetPosition(oldPos);
                            }
                        }
                        else
                        {
                            // Return to original position
                            draggedUnit.MoveTo(dragStartPos);
                        }
                    }
                    else
                    {
                        // Dropped on enemy side - return to original position
                        draggedUnit.MoveTo(dragStartPos);
                    }
                }
                else
                {
                    // Dropped outside board - return to original position
                    draggedUnit.MoveTo(dragStartPos);
                }
            }

            draggedUnit = null;
            Crestforge.UI.GameUI.Instance?.HideSellMode();

            // Trigger orphan cleanup on next sync
            orphanCleanupTimer = ORPHAN_CLEANUP_INTERVAL;
        }

        private void SelectUnit(UnitVisual3D unitVis)
        {
            // Delegate to GameUI for centralized selection state (works for both single and multiplayer)
            Crestforge.UI.GameUI.Instance?.HandleUnitClicked(unitVis);
        }

        /// <summary>
        /// Select a unit in multiplayer mode - delegates to GameUI for centralized state management
        /// </summary>
        private void SelectUnitMultiplayer(UnitVisual3D unitVis)
        {
            // Delegate all selection/tooltip logic to GameUI (single source of truth)
            Crestforge.UI.GameUI.Instance?.HandleUnitClicked(unitVis);
        }

        /// <summary>
        /// Find ServerUnitData for a given visual (called by GameUI for centralized selection)
        /// </summary>
        public ServerUnitData FindServerUnitByVisual(UnitVisual3D unitVis)
        {
            if (unitVis == null) return null;

            // Check board visuals
            foreach (var kvp in serverUnitVisuals)
            {
                if (kvp.Value == unitVis)
                {
                    return FindServerUnitByInstanceId(kvp.Key);
                }
            }

            // Check bench visuals
            if (serverState?.bench != null)
            {
                foreach (var kvp in serverBenchVisuals)
                {
                    if (kvp.Value == unitVis && kvp.Key >= 0 && kvp.Key < serverState.bench.Length)
                    {
                        return serverState.bench[kvp.Key];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find a ServerUnitData by its instance ID in the board
        /// </summary>
        private ServerUnitData FindServerUnitByInstanceId(string instanceId)
        {
            if (serverState == null || serverState.board == null || string.IsNullOrEmpty(instanceId))
                return null;

            // Check board
            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    var unit = serverState.board[x, y];
                    if (unit != null && unit.instanceId == instanceId)
                    {
                        return unit;
                    }
                }
            }

            // Check bench
            if (serverState.bench != null)
            {
                foreach (var unit in serverState.bench)
                {
                    if (unit != null && unit.instanceId == instanceId)
                    {
                        return unit;
                    }
                }
            }

            return null;
        }

        private void HighlightValidTiles()
        {
            if (hexBoard == null)
            {
                Debug.LogWarning("[BoardManager3D] hexBoard is null in HighlightValidTiles");
                return;
            }

            int playerRows = hexBoard.playerRows;

            // In multiplayer mode, use serverState board
            if (IsMultiplayer && serverState != null)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < playerRows; y++)
                    {
                        bool isEmpty = serverState.board[x, y] == null;
                        bool isStart = (x == dragStartCoord.x && y == dragStartCoord.y);

                        if (isEmpty || isStart)
                        {
                            hexBoard.HighlightTile(x, y, true);
                        }
                    }
                }
                return;
            }

            // Single player mode
            if (state == null || state.playerBoard == null) return;

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
                unit.boardPosition = new Vector2Int(toX, toY);
                return true;
            }
            else
            {
                // Swap units
                state.playerBoard[fromX, fromY] = targetUnit;
                state.playerBoard[toX, toY] = unit;
                targetUnit.boardPosition = new Vector2Int(fromX, fromY);
                unit.boardPosition = new Vector2Int(toX, toY);
                return true;
            }
        }

        /// <summary>
        /// Return a unit from the board to the bench
        /// </summary>
        private void ReturnUnitToBench(UnitVisual3D unitVisual)
        {
            if (unitVisual == null || unitVisual.unit == null) return;

            UnitInstance unit = unitVisual.unit;

            // Try to return to bench via game state
            bool success = state.ReturnToBench(unit);

            if (success)
            {
                // Remove visual from board tracking
                if (unitVisuals.ContainsKey(unit))
                {
                    unitVisuals.Remove(unit);
                }

                // Find the bench slot index for this unit
                int benchIndex = state.FindBenchIndex(unit);
                if (benchIndex >= 0)
                {
                    // Move visual to bench slot position
                    Vector3 benchPos = GetBenchSlotWorldPosition(benchIndex);
                    benchPos.y = unitYOffset;
                    unitVisual.SetPosition(benchPos);
                    benchVisuals[benchIndex] = unitVisual;
                }
                else
                {
                    // Couldn't find in bench - destroy as fallback
                    Destroy(unitVisual.gameObject);
                }
            }
            else
            {
                // Bench is full, return to original position on board
                unitVisual.MoveTo(dragStartPos);
            }
        }

        private void CreateDragPlaceholder()
        {
            dragPlaceholder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dragPlaceholder.name = "DragPlaceholder";
            dragPlaceholder.transform.SetParent(transform);
            dragPlaceholder.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
            Destroy(dragPlaceholder.GetComponent<Collider>());

            // Create a glowing green material for valid placement preview
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
            }
            mat.color = new Color(0.2f, 0.9f, 0.3f, 0.7f);
            mat.SetFloat("_Smoothness", 0.8f);
            // Enable transparency
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0); // Alpha
            mat.renderQueue = 3000;
            dragPlaceholder.GetComponent<Renderer>().material = mat;

            dragPlaceholder.SetActive(false);
        }

        /// <summary>
        /// Get world position for an away bench slot index (far side of board, behind row 7)
        /// </summary>
        public Vector3 GetAwayBenchSlotWorldPosition(int index)
        {
            if (hexBoard == null) return Vector3.zero;

            int benchSize = GameConstants.Player.BENCH_SIZE;
            float slotSpacing = 0.8f; // Space between bench slots
            float totalWidth = (benchSize - 1) * slotSpacing;

            // Position behind the enemy's last row (positive Z from board center)
            // Get the position of the last enemy row (row 7) to align with
            int lastRow = GameConstants.Grid.HEIGHT * 2 - 1; // Row 7 (0-indexed)
            Vector3 lastRowPos = hexBoard.GetTileWorldPosition(0, lastRow);

            // Away bench is positioned behind (positive Z) the last enemy row
            float benchZ = lastRowPos.z + 1.5f; // 1.5 units behind last row

            // Center the bench horizontally relative to board center
            Vector3 boardCenter = hexBoard.BoardCenter;
            float startX = boardCenter.x - totalWidth / 2f;
            float x = startX + index * slotSpacing;

            return new Vector3(x, 0.025f, benchZ); // Slightly above ground
        }

        /// <summary>
        /// Get world position for a bench slot index
        /// </summary>
        public Vector3 GetBenchSlotWorldPosition(int index)
        {
            // Delegate to registry if available
            if (Registry != null)
            {
                return Registry.GetBenchSlotWorldPosition(index);
            }

            // Fallback calculation
            if (hexBoard == null) return Vector3.zero;

            int benchSize = GameConstants.Player.BENCH_SIZE;
            float slotSpacing = 0.8f;
            float totalWidth = (benchSize - 1) * slotSpacing;

            Vector3 firstRowPos = hexBoard.GetTileWorldPosition(0, 0);
            float benchZ = firstRowPos.z - 1.5f;
            float startX = hexBoard.transform.position.x - totalWidth / 2f;
            float x = startX + index * slotSpacing;

            return new Vector3(x, unitYOffset, benchZ);
        }

        /// <summary>
        /// Get the bench slot index at a world position, or -1 if not over a bench slot
        /// </summary>
        private int GetBenchSlotAtWorldPosition(Vector3 worldPos)
        {
            // Delegate to registry if available
            if (Registry != null)
            {
                return Registry.GetBenchSlotAtWorldPosition(worldPos);
            }

            // Fallback calculation
            if (hexBoard == null) return -1;

            int benchSize = GameConstants.Player.BENCH_SIZE;
            float snapRadius = 1.2f;
            float closestDist = float.MaxValue;
            int closestSlot = -1;

            for (int i = 0; i < benchSize; i++)
            {
                Vector3 slotPos = GetBenchSlotWorldPosition(i);
                float dist = Vector2.Distance(
                    new Vector2(worldPos.x, worldPos.z),
                    new Vector2(slotPos.x, slotPos.z)
                );
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestSlot = i;
                }
            }

            return closestDist < snapRadius ? closestSlot : -1;
        }

        /// <summary>
        /// Update the hover preview to show where unit will be placed
        /// </summary>
        private void UpdateHoverPreview()
        {
            if (!isDragging || draggedUnit == null)
            {
                if (dragPlaceholder != null)
                    dragPlaceholder.SetActive(false);
                lastHapticHexCoord = new Vector2Int(-999, -999);
                return;
            }

            if (hexBoard == null)
            {
                if (dragPlaceholder != null)
                    dragPlaceholder.SetActive(false);
                lastHapticHexCoord = new Vector2Int(-999, -999);
                return;
            }

            // Temporarily disable ALL unit colliders so raycast can hit hex tiles underneath
            // This includes the dragged unit AND any fielded units on the board
            var disabledColliders = new System.Collections.Generic.List<Collider>();

            // Disable dragged unit's collider
            Collider draggedCollider = draggedUnit.GetComponent<Collider>();
            if (draggedCollider != null && draggedCollider.enabled)
            {
                draggedCollider.enabled = false;
                disabledColliders.Add(draggedCollider);
            }

            // Disable all board unit colliders
            if (Registry != null)
            {
                foreach (var kvp in Registry.BoardVisuals)
                {
                    if (kvp.Value != null && kvp.Value != draggedUnit)
                    {
                        Collider col = kvp.Value.GetComponent<Collider>();
                        if (col != null && col.enabled)
                        {
                            col.enabled = false;
                            disabledColliders.Add(col);
                        }
                    }
                }
            }

            // Find hex under cursor
            GameObject targetTile = hexBoard.GetTileAtScreenPosition(Input.mousePosition);

            // Re-enable all the colliders we disabled
            foreach (var col in disabledColliders)
            {
                if (col != null) col.enabled = true;
            }

            // Try raycast first, then fall back to closest tile by position
            Vector2Int coord = new Vector2Int(-1, -1);
            bool isEnemy = false;
            bool foundTile = false;

            if (targetTile != null && hexBoard.TryGetTileCoord(targetTile, out coord, out isEnemy))
            {
                foundTile = true;
            }
            else
            {
                // Fallback: find closest tile to dragged unit's current position
                // Use a generous distance (2x radius) to ensure no gaps between hexes
                Vector3 dragPos = draggedUnit.transform.position;
                if (hexBoard.TryGetClosestTileCoord(dragPos, hexBoard.TileRadius * 2f, out coord, out isEnemy))
                {
                    foundTile = true;
                }
            }

            if (foundTile)
            {
                // Only interact with valid player tiles
                if (coord.y < hexBoard.playerRows)
                {
                    // Haptic feedback when hovering a new player hex (even occupied - swap is allowed)
                    if (coord != lastHapticHexCoord)
                    {
                        lastHapticHexCoord = coord;
                        lastHapticBenchSlot = -999;
                        HapticFeedback.LightTap();
                    }

                    // Check if tile is empty (or is our original position for board drags)
                    bool isValidPlacement = false;
                    bool tileOccupied = false;

                    // Use appropriate state based on game mode
                    if (IsMultiplayer && serverState != null)
                    {
                        tileOccupied = serverState.board[coord.x, coord.y] != null;
                    }
                    else if (state != null)
                    {
                        tileOccupied = state.playerBoard[coord.x, coord.y] != null;
                    }

                    if (isDraggingFromBench)
                    {
                        // For bench drags, swapping with occupied tiles is allowed
                        isValidPlacement = true;
                    }
                    else
                    {
                        // Board drag - valid if empty or same tile (swap with another board unit)
                        isValidPlacement = true;
                    }

                    if (isValidPlacement && dragPlaceholder != null)
                    {
                        Vector3 previewPos = hexBoard.GetTileWorldPosition(coord.x, coord.y);
                        previewPos.y = 0.1f;
                        dragPlaceholder.transform.position = previewPos;
                        dragPlaceholder.SetActive(true);
                    }
                    // Found a valid player tile - don't reset haptic coord even if occupied
                    return;
                }
            }

            // Hide preview if not over valid tile (off the board or on enemy side)
            if (dragPlaceholder != null)
                dragPlaceholder.SetActive(false);
            // Don't reset lastHapticHexCoord here - it causes infinite haptic loops
            // when tile detection flickers near tile edges. The coord is properly
            // initialized in ActivateDrag() when a new drag starts.
        }

        /// <summary>
        /// Start dragging a unit from the bench
        /// </summary>
        public void StartBenchDrag(UnitInstance unit, int benchIndex)
        {
            if (unit == null || isDragging) return;
            // Bench rearranging is ALWAYS allowed

            isDragging = true;
            isDraggingFromBench = true;
            benchDragIndex = benchIndex;

            // Use the existing bench visual if available
            if (benchVisuals.TryGetValue(benchIndex, out UnitVisual3D existingVisual) && existingVisual != null)
            {
                draggedUnit = existingVisual;
                dragStartPos = existingVisual.transform.position;
                // Remove from bench visuals tracking while dragging
                benchVisuals.Remove(benchIndex);
            }
            else
            {
                // Fallback: Create temporary visual at cursor position
                Vector3 startPos = GetWorldPositionFromMouse();
                startPos.y = unitYOffset;

                GameObject visualObj = new GameObject($"Unit_{unit.template.unitName}_BenchDrag");
                visualObj.transform.SetParent(transform);
                UnitVisual3D visual = visualObj.AddComponent<UnitVisual3D>();
                visual.Initialize(unit, false);
                visual.SetPosition(startPos);

                draggedUnit = visual;
                dragStartPos = startPos;
            }

            // Block camera input
            if (cameraSetup != null)
            {
                cameraSetup.inputBlocked = true;
            }

            // Show sell mode in UI
            Crestforge.UI.GameUI.Instance?.ShowSellMode(unit);

            // Highlight valid placement tiles
            HighlightValidTiles();
        }

        /// <summary>
        /// Update bench drag position (called from UnitCardUI)
        /// </summary>
        public void UpdateBenchDrag(Vector2 screenPos)
        {
            if (!isDragging || !isDraggingFromBench || draggedUnit == null) return;

            Vector3 worldPos = GetWorldPositionFromMouse();
            worldPos.y = unitYOffset;
            // Use SetPosition to update both position and targetPosition
            // This prevents the visual from trying to animate back if destroyed with delay
            draggedUnit.SetPosition(worldPos);

            // Update hover preview
            UpdateHoverPreview();
        }

        /// <summary>
        /// End bench drag (called from UnitCardUI)
        /// </summary>
        public void EndBenchDrag()
        {
            if (!isDragging || !isDraggingFromBench)
            {
                isDragging = false;
                isDraggingFromBench = false;
                Crestforge.UI.GameUI.Instance?.HideSellMode();
                return;
            }

            isDragging = false;
            isDraggingFromBench = false;
            hexBoard.ClearHighlights();
            dragPlaceholder.SetActive(false);

            // Unblock camera input
            if (cameraSetup != null)
            {
                cameraSetup.inputBlocked = false;
            }

            if (draggedUnit == null)
            {
                Crestforge.UI.GameUI.Instance?.HideSellMode();
                return;
            }

            // Get the unit before destroying the visual
            UnitInstance unit = draggedUnit.unit;

            // Temporarily disable the dragged unit's collider so raycast hits the tile
            Collider draggedCollider = draggedUnit.GetComponent<Collider>();
            if (draggedCollider != null) draggedCollider.enabled = false;

            // Find target tile
            GameObject targetTile = hexBoard.GetTileAtScreenPosition(Input.mousePosition);

            // Check if over a bench slot
            Vector3 worldPos = GetWorldPositionFromMouse();
            int targetBenchSlot = GetBenchSlotAtWorldPosition(worldPos);

            // Re-enable collider
            if (draggedCollider != null) draggedCollider.enabled = true;

            bool placed = false;
            bool sold = false;

            // Check if dropped on sell zone (UI raycast)
            if (IsPointerOverSellZone())
            {
                // Sell the unit
                if (benchDragIndex >= 0 && benchDragIndex < state.bench.Length)
                {
                    state.bench[benchDragIndex] = null;
                }
                Crestforge.UI.GameUI.Instance?.TrySellUnit();
                sold = true;
            }
            // Check if dropped on another bench slot (swap or move)
            else if (targetBenchSlot >= 0 && targetBenchSlot < state.bench.Length && targetBenchSlot != benchDragIndex)
            {
                UnitInstance otherUnit = state.bench[targetBenchSlot];
                if (otherUnit != null && benchDragIndex >= 0 && benchDragIndex < state.bench.Length)
                {
                    // Swap with existing bench unit
                    state.bench[targetBenchSlot] = unit;
                    state.bench[benchDragIndex] = otherUnit;

                    // Get the other unit's visual BEFORE overwriting
                    benchVisuals.TryGetValue(targetBenchSlot, out UnitVisual3D otherVisual);

                    // Update dragged unit visual (move to target slot)
                    Vector3 newPos = GetBenchSlotWorldPosition(targetBenchSlot);
                    newPos.y = unitYOffset;
                    draggedUnit.SetPosition(newPos);
                    benchVisuals[targetBenchSlot] = draggedUnit;

                    // Move the other unit's visual to the original slot
                    if (otherVisual != null && otherVisual != draggedUnit)
                    {
                        Vector3 otherPos = GetBenchSlotWorldPosition(benchDragIndex);
                        otherPos.y = unitYOffset;
                        otherVisual.SetPosition(otherPos);
                        benchVisuals[benchDragIndex] = otherVisual;
                    }

                    draggedUnit = null;
                    placed = true;
                }
                else if (benchDragIndex >= 0 && benchDragIndex < state.bench.Length)
                {
                    // Move to empty bench slot
                    // Clear old slot, set new slot
                    state.bench[benchDragIndex] = null;
                    state.bench[targetBenchSlot] = unit;

                    // Update visual position
                    Vector3 newPos = GetBenchSlotWorldPosition(targetBenchSlot);
                    newPos.y = unitYOffset;
                    draggedUnit.SetPosition(newPos);
                    benchVisuals[targetBenchSlot] = draggedUnit;
                    benchVisuals.Remove(benchDragIndex);

                    draggedUnit = null;
                    placed = true;
                }

                // Refresh to ensure visuals are correct
                Crestforge.UI.GameUI.Instance?.RefreshBench();
            }
            else
            {
                // Try raycast first, then fall back to closest tile detection
                Vector2Int coord;
                bool isEnemy;
                bool foundTile = false;

                if (targetTile != null && hexBoard.TryGetTileCoord(targetTile, out coord, out isEnemy))
                {
                    foundTile = true;
                }
                else
                {
                    // Fallback: find closest tile to drop position (use 2x radius to ensure no gaps)
                    foundTile = hexBoard.TryGetClosestTileCoord(worldPos, hexBoard.TileRadius * 2f, out coord, out isEnemy);
                }

                if (foundTile)
                {
                    // Don't allow bench-to-board during combat
                    if (state.round.phase == GamePhase.Combat)
                    {
                        // Return to original bench slot
                    }
                    // Only allow placement in player rows during planning
                    else if (coord.y < hexBoard.playerRows && benchDragIndex >= 0 && benchDragIndex < state.bench.Length)
                    {
                        UnitInstance boardUnit = state.playerBoard[coord.x, coord.y];

                        if (boardUnit == null)
                        {
                            // Empty tile - place bench unit on board
                            state.bench[benchDragIndex] = null;
                            state.playerBoard[coord.x, coord.y] = unit;

                            // Set unit state for proper tracking
                            unit.isOnBoard = true;
                            unit.boardPosition = new UnityEngine.Vector2Int(coord.x, coord.y);

                            // Update the visual position
                            Vector3 newPos = hexBoard.GetTileWorldPosition(coord.x, coord.y);
                            newPos.y = unitYOffset;
                            draggedUnit.SetPosition(newPos);

                            // Add to tracking - keep visual, don't destroy
                            unitVisuals[unit] = draggedUnit;
                            draggedUnit = null;
                            placed = true;

                            // Refresh UI and recalculate traits
                            state.RecalculateTraits();
                            Crestforge.UI.GameUI.Instance?.RefreshBench();
                        }
                        else
                        {
                            // Occupied tile - swap bench unit with board unit
                            // Place bench unit on board
                            state.playerBoard[coord.x, coord.y] = unit;
                            unit.isOnBoard = true;
                            unit.boardPosition = new UnityEngine.Vector2Int(coord.x, coord.y);

                            // Move board unit to the bench slot where dragged unit was
                            boardUnit.isOnBoard = false;
                            state.bench[benchDragIndex] = boardUnit;

                            // Update dragged unit visual (bench -> board)
                            Vector3 boardPos = hexBoard.GetTileWorldPosition(coord.x, coord.y);
                            boardPos.y = unitYOffset;
                            draggedUnit.SetPosition(boardPos);
                            unitVisuals[unit] = draggedUnit;

                            // Update board unit visual (board -> bench)
                            if (unitVisuals.TryGetValue(boardUnit, out UnitVisual3D boardVisual) && boardVisual != null)
                            {
                                unitVisuals.Remove(boardUnit);
                                Vector3 benchPos = GetBenchSlotWorldPosition(benchDragIndex);
                                benchPos.y = unitYOffset;
                                boardVisual.SetPosition(benchPos);
                                benchVisuals[benchDragIndex] = boardVisual;
                            }

                            draggedUnit = null;
                            placed = true;

                            // Refresh UI and recalculate traits
                            state.RecalculateTraits();
                            Crestforge.UI.GameUI.Instance?.RefreshBench();
                        }
                    }
                }
            }

            // Handle the visual based on outcome
            if (draggedUnit != null)
            {
                if (sold)
                {
                    // Sold - destroy the visual
                    Destroy(draggedUnit.gameObject);
                }
                else if (!placed)
                {
                    // Invalid drop - return visual to bench
                    Vector3 benchPos = GetBenchSlotWorldPosition(benchDragIndex);
                    benchPos.y = unitYOffset;
                    draggedUnit.SetPosition(benchPos);
                    benchVisuals[benchDragIndex] = draggedUnit;
                }
            }

            draggedUnit = null;

            // Hide sell mode
            Crestforge.UI.GameUI.Instance?.HideSellMode();

            // Trigger orphan cleanup on next sync to catch any stray visuals
            orphanCleanupTimer = ORPHAN_CLEANUP_INTERVAL;
        }

        /// <summary>
        /// Check if pointer is over the sell zone UI or off the bottom of screen
        /// </summary>
        private bool IsPointerOverSellZone()
        {
            // Check if pointer is off the bottom of the screen (mobile drag-off-screen to sell)
            if (Input.mousePosition.y < 0)
            {
                return true;
            }

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return false;

            var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };

            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                if (result.gameObject.GetComponent<Crestforge.UI.SellDropZone>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if currently dragging from bench
        /// </summary>
        public bool IsDraggingFromBench => isDragging && isDraggingFromBench;

        /// <summary>
        /// Cancel any in-progress bench drag and return unit to bench
        /// Called when combat starts to prevent stuck units
        /// </summary>
        public void CancelBenchDrag()
        {
            if (!isDragging) return;

            // Clean up drag state
            isDragging = false;
            isDraggingFromBench = false;
            hexBoard?.ClearHighlights();
            if (dragPlaceholder != null) dragPlaceholder.SetActive(false);

            // Unblock camera input
            if (cameraSetup != null)
            {
                cameraSetup.inputBlocked = false;
            }

            // Return the dragged unit to its bench position
            if (draggedUnit != null && benchDragIndex >= 0)
            {
                Vector3 benchPos = GetBenchSlotWorldPosition(benchDragIndex);
                benchPos.y = unitYOffset;
                draggedUnit.SetPosition(benchPos);
                benchVisuals[benchDragIndex] = draggedUnit;
            }

            draggedUnit = null;

            // Hide sell mode
            Crestforge.UI.GameUI.Instance?.HideSellMode();
        }

        private Vector3 GetWorldPositionFromMouse()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, unitYOffset, 0));
            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            return Vector3.zero;
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
        /// Get unit visual by server instance ID (for multiplayer combat visualization)
        /// </summary>
        public UnitVisual3D GetUnitVisualByInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;

            // Delegate to registry
            if (Registry != null)
            {
                return Registry.GetVisualByInstanceId(instanceId);
            }

            return null;
        }

        /// <summary>
        /// Show or hide board unit visuals only (used during combat to prevent duplication)
        /// Bench units are NOT affected - they should remain visible during combat
        /// </summary>
        public void SetBoardUnitVisualsVisible(bool visible)
        {
            if (Registry != null)
            {
                Registry.SetBoardVisualsVisible(visible);
            }
        }

        /// <summary>
        /// Show or hide all unit visuals including bench (legacy method)
        /// </summary>
        public void SetAllUnitVisualsVisible(bool visible)
        {
            if (Registry != null)
            {
                Registry.SetBoardVisualsVisible(visible);
                Registry.SetBenchVisualsVisible(visible);
            }
        }

        /// <summary>
        /// Convert grid coordinates to world position (for combat visualization)
        /// </summary>
        public Vector3 GridToWorld(int x, int y)
        {
            if (hexBoard == null) return Vector3.zero;
            Vector3 pos = hexBoard.GetTileWorldPosition(x, y);
            pos.y = unitYOffset;
            return pos;
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
}