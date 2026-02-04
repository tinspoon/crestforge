using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Systems;
using Crestforge.Combat;
using Crestforge.Networking;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Simplified board visualizer for opponent boards in PvP mode.
    /// Display-only - no drag/drop interaction, just shows units and combat.
    /// Syncs with ServerGameState in multiplayer mode.
    /// </summary>
    public class OpponentBoardVisualizer : MonoBehaviour
    {
        [Header("References")]
        public HexBoard3D hexBoard;
        public OpponentData opponent;

        [Header("Settings")]
        public float unitYOffset = 0.15f;

        // Unit visuals tracking (for local/singleplayer mode)
        private Dictionary<UnitInstance, UnitVisual3D> ownerUnitVisuals = new Dictionary<UnitInstance, UnitVisual3D>();
        private Dictionary<UnitInstance, UnitVisual3D> enemyUnitVisuals = new Dictionary<UnitInstance, UnitVisual3D>();

        // Server unit visuals now managed by BoardVisualRegistry (via hexBoard.Registry)
        private string serverPlayerId; // The server player ID this board represents

        /// <summary>
        /// Get the visual registry for this board
        /// </summary>
        private BoardVisualRegistry Registry => hexBoard?.Registry;

        // Combat unit to visual mapping (for combat sync)
        private Dictionary<CombatUnit, UnitVisual3D> combatUnitVisuals = new Dictionary<CombatUnit, UnitVisual3D>();

        // Board label
        private GameObject labelObject;
        private TextMesh labelText;

        // Combat state
        private CombatSimulation currentSimulation;
        private bool isInCombat;
        private bool isConnectedToCombatManager;
        private bool isFlippedPerspective; // true = show from "away" team's perspective

        // "Units away" state - when this player's units are fighting on another board
        private bool unitsAway;
        private HexBoard3D awayFightBoard;
        private string awayFightHomePlayer;
        private GameObject awayIndicator;

        /// <summary>
        /// Whether this player is fighting "away" on another board
        /// </summary>
        public bool IsAway => unitsAway;

        /// <summary>
        /// The board where the fight is happening (if away)
        /// </summary>
        public HexBoard3D FightBoard => awayFightBoard;

        private void Start()
        {
            CreateBoardLabel();
        }

        private void OnEnable()
        {
        }

        // Debug logging throttle
        private float lastDebugLogTime = 0f;
        private const float DEBUG_LOG_INTERVAL = 5f;

        // Track combat state for multiplayer mode
        private bool wasInCombatPhase = false;

        private void Update()
        {
            // Check for multiplayer mode first
            var serverState = ServerGameState.Instance;
            if (serverState != null && serverState.IsInGame)
            {
                // Try to auto-detect server player ID if not set
                if (string.IsNullOrEmpty(serverPlayerId))
                {
                    TryAutoDetectServerPlayerId(serverState);
                }

                if (!string.IsNullOrEmpty(serverPlayerId))
                {
                    // Check for combat phase transitions (include "results" since victory pose is still playing)
                    bool isInCombatPhase = serverState.phase == "combat" || serverState.phase == "results";
                    if (isInCombatPhase && !wasInCombatPhase)
                    {
                        // Combat just started - hide board visuals (ServerCombatVisualizer will render combat)
                        HideBoardVisuals();
                    }
                    else if (!isInCombatPhase && wasInCombatPhase)
                    {
                        // Combat just ended - show board visuals
                        ShowBoardVisuals();
                    }
                    wasInCombatPhase = isInCombatPhase;

                    // Multiplayer mode - sync from server state (bench always, board only when not in combat)
                    SyncWithServerState(serverState);
                    return;
                }
                else if (Time.time - lastDebugLogTime > DEBUG_LOG_INTERVAL)
                {
                    lastDebugLogTime = Time.time;
                    Debug.LogWarning($"[OpponentBoardVisualizer] serverPlayerId not set for board {hexBoard?.boardLabel}. " +
                        $"AllBoards count: {HexBoard3D.AllBoards?.Count}, otherPlayers count: {serverState.otherPlayers?.Count}");
                }
            }
            else if (serverState != null && Time.time - lastDebugLogTime > DEBUG_LOG_INTERVAL)
            {
                // Log why we're not in multiplayer mode
                lastDebugLogTime = Time.time;
            }

            // Sync visuals with board state or combat state (local/singleplayer mode)
            if (opponent != null)
            {
                if (unitsAway)
                {
                    // Units are fighting on another board - don't show them here
                    return;
                }
                else if (isInCombat)
                {
                    if (isConnectedToCombatManager)
                    {
                        SyncWithCombatManager();
                    }
                    else if (currentSimulation != null)
                    {
                        SyncCombatVisuals();
                    }
                }
                else
                {
                    SyncBoardVisuals();
                }
            }
        }

        /// <summary>
        /// Try to auto-detect the server player ID based on board index
        /// </summary>
        private void TryAutoDetectServerPlayerId(ServerGameState serverState)
        {
            if (hexBoard == null)
            {
                Debug.LogWarning("[OpponentBoardVisualizer] TryAutoDetectServerPlayerId: hexBoard is null");
                return;
            }
            if (serverState.otherPlayers == null || serverState.otherPlayers.Count == 0)
            {
                // Only log occasionally to avoid spam
                if (Time.time - lastDebugLogTime > DEBUG_LOG_INTERVAL)
                {
                }
                return;
            }

            // Find this board's index in AllBoards
            int thisBoardIndex = -1;
            var allBoards = HexBoard3D.AllBoards;
            for (int i = 0; i < allBoards.Count; i++)
            {
                if (allBoards[i] == hexBoard)
                {
                    thisBoardIndex = i;
                    break;
                }
            }

            if (thisBoardIndex < 0)
            {
                Debug.LogWarning($"[OpponentBoardVisualizer] TryAutoDetectServerPlayerId: Board {hexBoard.boardLabel} not found in AllBoards");
                return;
            }

            // Skip if this is the local player's board
            if (thisBoardIndex == serverState.localBoardIndex)
            {
                // This is expected for the local player's board
                return;
            }

            // Find the player with this board index
            foreach (var player in serverState.otherPlayers)
            {
                if (player.boardIndex == thisBoardIndex)
                {
                    serverPlayerId = player.clientId;
                    return;
                }
            }

            // If we get here, no player matched this board index
            if (Time.time - lastDebugLogTime > DEBUG_LOG_INTERVAL)
            {
                string playerIndices = "";
                foreach (var p in serverState.otherPlayers)
                {
                    playerIndices += $"{p.name}(idx={p.boardIndex}), ";
                }
                Debug.LogWarning($"[OpponentBoardVisualizer] No player found for board index {thisBoardIndex}. Local board index: {serverState.localBoardIndex}. Other players: [{playerIndices}]");
            }
        }

        /// <summary>
        /// Set the server player ID this board represents (for multiplayer mode)
        /// </summary>
        public void SetServerPlayerId(string playerId)
        {
            serverPlayerId = playerId;

            // Trigger immediate sync if we have server state
            var serverState = ServerGameState.Instance;
            if (serverState != null && serverState.IsInGame && !string.IsNullOrEmpty(playerId))
            {
                SyncWithServerState(serverState);
            }
        }

        /// <summary>
        /// Get the server player ID (for debugging)
        /// </summary>
        public string ServerPlayerId => serverPlayerId;

        /// <summary>
        /// Initialize the visualizer with an opponent
        /// </summary>
        public void Initialize(OpponentData opponentData, HexBoard3D board)
        {
            opponent = opponentData;
            hexBoard = board;

            if (hexBoard != null)
            {
                hexBoard.ownerId = opponent.id;
                hexBoard.boardLabel = opponent.name;
                hexBoard.isPlayerBoard = false;
            }

            CreateBoardLabel();

            // Force initial sync to show existing units
            if (opponent != null && opponent.board != null)
            {
                SyncBoardVisuals();

                // Count units for debug
                int unitCount = 0;
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                    {
                        if (opponent.board[x, y] != null) unitCount++;
                    }
                }
            }
            else
            {
            }

            UpdateLabel();
        }

        /// <summary>
        /// Force refresh all visuals
        /// </summary>
        public void Refresh()
        {
            if (opponent != null)
            {
                SyncBoardVisuals();
                UpdateLabel();
            }
        }

        /// <summary>
        /// Create a floating label above the board
        /// </summary>
        private void CreateBoardLabel()
        {
            if (labelObject != null || opponent == null || hexBoard == null) return;

            labelObject = new GameObject("BoardLabel");
            labelObject.transform.SetParent(transform);

            // Position above the board center
            Vector3 boardCenter = hexBoard.BoardCenter;
            labelObject.transform.position = boardCenter + new Vector3(0, 2f, -3f);

            // Create TextMesh for the label
            labelText = labelObject.AddComponent<TextMesh>();
            labelText.text = opponent.name;
            labelText.fontSize = 48;
            labelText.characterSize = 0.1f;
            labelText.anchor = TextAnchor.MiddleCenter;
            labelText.alignment = TextAlignment.Center;
            labelText.color = Color.white;

            // Make it face the camera
            labelObject.AddComponent<LookAtCamera>();
        }

        /// <summary>
        /// Update the label with current health
        /// </summary>
        public void UpdateLabel()
        {
            if (labelText != null && opponent != null)
            {
                labelText.text = $"{opponent.name}\nHP: {opponent.health}/{opponent.maxHealth}";
                labelText.color = opponent.isEliminated ? Color.gray : Color.white;
            }
        }

        /// <summary>
        /// Connect to a combat simulation for this board
        /// </summary>
        /// <param name="simulation">The combat simulation</param>
        /// <param name="flipPerspective">If true, show from away team's perspective (flip positions)</param>
        public void ConnectToSimulation(CombatSimulation simulation, bool flipPerspective = false)
        {
            currentSimulation = simulation;
            isInCombat = true;
            isConnectedToCombatManager = false;
            isFlippedPerspective = flipPerspective;
            combatUnitVisuals.Clear();

            // Hide non-combat visuals to avoid duplicates
            HideBoardVisuals();

            // Subscribe to simulation events
            simulation.OnUnitMoved += HandleCombatUnitMoved;
            simulation.OnUnitDied += HandleCombatUnitDied;
            simulation.OnDamageDealt += HandleCombatDamage;
            simulation.OnCombatEnd += HandleCombatEnd;

            // Create initial combat unit visuals
            foreach (var combatUnit in simulation.allUnits)
            {
                CreateCombatUnitVisual(combatUnit);
            }

        }

        /// <summary>
        /// Connect to the main CombatManager to show the player's fight on this board.
        /// Used when this opponent is fighting the player.
        /// </summary>
        /// <param name="flipPerspective">If true, show from opponent's perspective (their units on bottom)</param>
        public void ConnectToCombatManager(bool flipPerspective = false)
        {
            if (CombatManager.Instance == null) return;

            isInCombat = true;
            isConnectedToCombatManager = true;
            isFlippedPerspective = flipPerspective;
            combatUnitVisuals.Clear();

            // Hide non-combat visuals
            HideBoardVisuals();

            // Subscribe to CombatManager events
            CombatManager.Instance.OnUnitDied += HandleCombatManagerUnitDied;
            CombatManager.Instance.OnDamageDealt += HandleCombatManagerDamage;
            CombatManager.Instance.OnCombatEnd += HandleCombatManagerEnd;

            // Create combat unit visuals for all units in CombatManager
            foreach (var combatUnit in CombatManager.Instance.allUnits)
            {
                CreateCombatUnitVisual(combatUnit);
            }

        }

        private void HandleCombatManagerUnitDied(CombatUnit unit)
        {
            HandleCombatUnitDied(unit);
        }

        private void HandleCombatManagerDamage(CombatUnit source, CombatUnit target, int damage)
        {
            HandleCombatDamage(source, target, damage);
        }

        private void HandleCombatManagerEnd(CombatResult result)
        {
            DisconnectFromCombatManager();
        }

        /// <summary>
        /// Sync visuals with CombatManager unit positions
        /// </summary>
        private void SyncWithCombatManager()
        {
            if (CombatManager.Instance == null || hexBoard == null) return;

            foreach (var combatUnit in CombatManager.Instance.allUnits)
            {
                if (combatUnit.isDead) continue;

                if (combatUnitVisuals.TryGetValue(combatUnit, out UnitVisual3D visual) && visual != null)
                {
                    if (!visual.IsMoving)
                    {
                        Vector2Int displayPos = GetDisplayPosition(combatUnit.position);
                        Vector3 worldPos = hexBoard.GetTileWorldPosition(displayPos.x, displayPos.y);
                        worldPos.y = unitYOffset;
                        visual.SetPosition(worldPos);
                    }

                    // Update health bar
                    visual.UpdateHealthBar(combatUnit.currentHealth, combatUnit.stats.health);
                }
            }
        }

        /// <summary>
        /// Disconnect from CombatManager
        /// </summary>
        public void DisconnectFromCombatManager()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnUnitDied -= HandleCombatManagerUnitDied;
                CombatManager.Instance.OnDamageDealt -= HandleCombatManagerDamage;
                CombatManager.Instance.OnCombatEnd -= HandleCombatManagerEnd;
            }

            isInCombat = false;
            isConnectedToCombatManager = false;

            // Clear combat visuals
            foreach (var kvp in combatUnitVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            combatUnitVisuals.Clear();

            // Restore board visuals
            ShowBoardVisuals();
        }

        /// <summary>
        /// Hide non-combat board visuals during combat
        /// Note: Bench visuals are NOT hidden - they should remain visible during combat
        /// </summary>
        private void HideBoardVisuals()
        {
            // Hide local/singleplayer visuals
            foreach (var kvp in ownerUnitVisuals)
            {
                if (kvp.Value != null) kvp.Value.gameObject.SetActive(false);
            }
            foreach (var kvp in enemyUnitVisuals)
            {
                if (kvp.Value != null) kvp.Value.gameObject.SetActive(false);
            }

            // Hide multiplayer server board visuals (but NOT bench visuals)
            // Now uses registry
            Registry?.SetBoardVisualsVisible(false);
            // Registry bench visuals remain visible during combat
        }

        /// <summary>
        /// Show non-combat board visuals after combat
        /// </summary>
        private void ShowBoardVisuals()
        {
            // Show local/singleplayer visuals
            foreach (var kvp in ownerUnitVisuals)
            {
                if (kvp.Value != null) kvp.Value.gameObject.SetActive(true);
            }
            foreach (var kvp in enemyUnitVisuals)
            {
                if (kvp.Value != null) kvp.Value.gameObject.SetActive(true);
            }

            // Show multiplayer server board visuals via registry
            Registry?.SetBoardVisualsVisible(true);
        }

        /// <summary>
        /// Disconnect from combat simulation
        /// </summary>
        public void DisconnectFromSimulation()
        {
            if (currentSimulation != null)
            {
                currentSimulation.OnUnitMoved -= HandleCombatUnitMoved;
                currentSimulation.OnUnitDied -= HandleCombatUnitDied;
                currentSimulation.OnDamageDealt -= HandleCombatDamage;
                currentSimulation.OnCombatEnd -= HandleCombatEnd;
            }

            currentSimulation = null;
            isInCombat = false;

            // Clear combat visuals
            foreach (var kvp in combatUnitVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            combatUnitVisuals.Clear();

            // Restore non-combat board visuals
            ShowBoardVisuals();
        }

        private void CreateCombatUnitVisual(CombatUnit combatUnit)
        {
            if (hexBoard == null || combatUnit == null) return;

            // Get position, flipping Y if needed
            Vector2Int displayPos = GetDisplayPosition(combatUnit.position);
            Vector3 worldPos = hexBoard.GetTileWorldPosition(displayPos.x, displayPos.y);
            worldPos.y = unitYOffset;

            // Determine if this unit is "enemy" from this board's perspective
            // If flipped, Team.Player becomes enemy and Team.Enemy becomes owner
            bool isEnemy = isFlippedPerspective ? (combatUnit.team == Team.Player) : (combatUnit.team == Team.Enemy);

            string teamLabel = isEnemy ? "Enemy" : "Owner";
            GameObject visualObj = new GameObject($"CombatUnit_{combatUnit.source.template.unitName}_{teamLabel}");
            visualObj.transform.SetParent(transform);

            UnitVisual3D visual = visualObj.AddComponent<UnitVisual3D>();
            visual.Initialize(combatUnit.source, isEnemy);
            visual.SetPosition(worldPos);

            combatUnitVisuals[combatUnit] = visual;
        }

        /// <summary>
        /// Get display position, flipping Y coordinate if viewing from flipped perspective
        /// </summary>
        private Vector2Int GetDisplayPosition(Vector2Int originalPos)
        {
            if (!isFlippedPerspective)
            {
                return originalPos;
            }

            // Flip Y position: bottom becomes top, top becomes bottom
            int flippedY = GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT - 1 - originalPos.y;
            return new Vector2Int(originalPos.x, flippedY);
        }

        private void HandleCombatUnitMoved(CombatUnit unit, Vector2Int newPos)
        {
            if (combatUnitVisuals.TryGetValue(unit, out UnitVisual3D visual) && visual != null && hexBoard != null)
            {
                Vector2Int displayPos = GetDisplayPosition(newPos);
                Vector3 worldPos = hexBoard.GetTileWorldPosition(displayPos.x, displayPos.y);
                worldPos.y = unitYOffset;
                visual.MoveTo(worldPos);
            }
        }

        private void HandleCombatUnitDied(CombatUnit unit)
        {
            if (combatUnitVisuals.TryGetValue(unit, out UnitVisual3D visual) && visual != null)
            {
                visual.PlayDeathEffect();
            }
        }

        private void HandleCombatDamage(CombatUnit source, CombatUnit target, int damage)
        {
            // Trigger attack/hit animations
            if (combatUnitVisuals.TryGetValue(source, out UnitVisual3D attackerVisual) && attackerVisual != null)
            {
                // Get target position for attack animation
                Vector3 targetPos = attackerVisual.transform.position;
                if (combatUnitVisuals.TryGetValue(target, out UnitVisual3D targetVisual) && targetVisual != null)
                {
                    targetPos = targetVisual.transform.position;
                }
                attackerVisual.PlayAttackAnimation(targetPos);
            }
            if (combatUnitVisuals.TryGetValue(target, out UnitVisual3D hitVisual) && hitVisual != null)
            {
                hitVisual.PlayHitEffect();
            }
        }

        private void HandleCombatEnd(CombatSimulation sim, CombatResult result)
        {
            DisconnectFromSimulation();
        }

        /// <summary>
        /// Sync visuals with combat unit positions during battle
        /// </summary>
        private void SyncCombatVisuals()
        {
            if (currentSimulation == null || hexBoard == null) return;

            foreach (var combatUnit in currentSimulation.allUnits)
            {
                if (combatUnit.isDead) continue;

                if (combatUnitVisuals.TryGetValue(combatUnit, out UnitVisual3D visual) && visual != null)
                {
                    if (!visual.IsMoving)
                    {
                        Vector2Int displayPos = GetDisplayPosition(combatUnit.position);
                        Vector3 worldPos = hexBoard.GetTileWorldPosition(displayPos.x, displayPos.y);
                        worldPos.y = unitYOffset;
                        visual.SetPosition(worldPos);
                    }

                    // Update health bar
                    visual.UpdateHealthBar(combatUnit.currentHealth, combatUnit.stats.health);
                }
            }
        }

        /// <summary>
        /// Sync unit visuals with ServerGameState (multiplayer mode)
        /// Always renders both board and bench units via BoardVisualRegistry
        /// </summary>
        private void SyncWithServerState(ServerGameState serverState)
        {
            if (hexBoard == null || Registry == null || string.IsNullOrEmpty(serverPlayerId)) return;

            // Find the player data for this board
            ServerPlayerData playerData = null;
            foreach (var player in serverState.otherPlayers)
            {
                if (player.clientId == serverPlayerId)
                {
                    playerData = player;
                    break;
                }
            }

            if (playerData == null)
            {
                // Only log occasionally to avoid spam
                if (Time.frameCount % 300 == 0)
                {
                    string otherIds = string.Join(", ", serverState.otherPlayers.ConvertAll(p => p.clientId));
                    Debug.LogWarning($"[OpponentBoardVisualizer] Could not find player data for serverPlayerId={serverPlayerId}. Available: [{otherIds}]");
                }
                return;
            }

            // Periodic diagnostic logging
            if (Time.frameCount % 600 == 0)
            {
                int boardUnitCount = playerData.boardUnits?.Count ?? 0;
                int benchCount = 0;
                if (playerData.bench != null)
                {
                    foreach (var b in playerData.bench)
                    {
                        if (b != null && !string.IsNullOrEmpty(b.instanceId)) benchCount++;
                    }
                }
            }

            HashSet<string> currentBoardIds = new HashSet<string>();
            HashSet<int> currentBenchSlots = new HashSet<int>();

            // Check if we're in combat/results phase - skip board unit sync during combat and victory pose
            // (ServerCombatVisualizer handles combat visualization including victory pose)
            bool isInCombatPhase = serverState.phase == "combat" || serverState.phase == "results";

            // Sync board units (only when not in combat)
            if (!isInCombatPhase && playerData.boardUnits != null)
            {
                foreach (var boardUnit in playerData.boardUnits)
                {
                    if (boardUnit == null || boardUnit.unit == null) continue;

                    string instanceId = boardUnit.unit.instanceId;
                    if (string.IsNullOrEmpty(instanceId)) continue;

                    currentBoardIds.Add(instanceId);

                    // Use registry to get or create visual
                    Registry.GetOrCreateBoardVisual(boardUnit.unit, boardUnit.x, boardUnit.y, false);
                }
            }
            else if (isInCombatPhase)
            {
                // During combat, keep track of existing board units but don't create/update visuals
                if (playerData.boardUnits != null)
                {
                    foreach (var boardUnit in playerData.boardUnits)
                    {
                        if (boardUnit?.unit != null && !string.IsNullOrEmpty(boardUnit.unit.instanceId))
                        {
                            currentBoardIds.Add(boardUnit.unit.instanceId);
                        }
                    }
                }
            }

            // Sync bench units
            if (playerData.bench != null)
            {
                for (int i = 0; i < playerData.bench.Length; i++)
                {
                    var unitData = playerData.bench[i];
                    if (unitData == null || string.IsNullOrEmpty(unitData.instanceId)) continue;

                    currentBenchSlots.Add(i);

                    // Use registry to get or create visual
                    Registry.GetOrCreateBenchVisual(unitData, i);
                }
            }

            // Cleanup removed board units via registry
            Registry.SyncBoardVisuals(currentBoardIds);

            // Cleanup removed bench units via registry
            Registry.SyncBenchVisuals(currentBenchSlots);
        }

        // CreateServerUnitVisual removed - now handled by BoardVisualRegistry

        /// <summary>
        /// Sync unit visuals with the opponent's board state
        /// </summary>
        private void SyncBoardVisuals()
        {
            if (opponent == null || opponent.board == null || hexBoard == null) return;

            // Track which units are still on the board
            HashSet<UnitInstance> currentOwnerUnits = new HashSet<UnitInstance>();
            HashSet<UnitInstance> currentEnemyUnits = new HashSet<UnitInstance>();

            // Sync owner's units (bottom half - the opponent's own units)
            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    var unit = opponent.board[x, y];
                    if (unit == null) continue;

                    currentOwnerUnits.Add(unit);
                    Vector3 worldPos = hexBoard.GetTileWorldPosition(x, y);
                    worldPos.y = unitYOffset;

                    if (ownerUnitVisuals.TryGetValue(unit, out UnitVisual3D visual) && visual != null)
                    {
                        // Update existing visual position
                        if (!visual.IsMoving)
                        {
                            visual.SetPosition(worldPos);
                        }
                    }
                    else
                    {
                        // Create new visual
                        visual = CreateUnitVisual(unit, worldPos, false);
                        ownerUnitVisuals[unit] = visual;
                    }
                }
            }

            // Clean up visuals for units no longer on board
            CleanupRemovedUnits(ownerUnitVisuals, currentOwnerUnits);
            CleanupRemovedUnits(enemyUnitVisuals, currentEnemyUnits);
        }

        /// <summary>
        /// Set the enemy units for this board (the opponent this owner is fighting)
        /// </summary>
        public void SetEnemyBoard(UnitInstance[,] enemyBoard)
        {
            if (hexBoard == null) return;

            HashSet<UnitInstance> currentEnemyUnits = new HashSet<UnitInstance>();

            if (enemyBoard != null)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                    {
                        var unit = enemyBoard[x, y];
                        if (unit == null) continue;

                        currentEnemyUnits.Add(unit);

                        // Mirror position to enemy side
                        int enemyY = GameConstants.Grid.HEIGHT + y;
                        Vector3 worldPos = hexBoard.GetTileWorldPosition(x, enemyY);
                        worldPos.y = unitYOffset;

                        if (enemyUnitVisuals.TryGetValue(unit, out UnitVisual3D visual) && visual != null)
                        {
                            if (!visual.IsMoving)
                            {
                                visual.SetPosition(worldPos);
                            }
                        }
                        else
                        {
                            visual = CreateUnitVisual(unit, worldPos, true);
                            enemyUnitVisuals[unit] = visual;
                        }
                    }
                }
            }

            CleanupRemovedUnits(enemyUnitVisuals, currentEnemyUnits);
        }

        /// <summary>
        /// Create a unit visual
        /// </summary>
        private UnitVisual3D CreateUnitVisual(UnitInstance unit, Vector3 position, bool isEnemy)
        {
            GameObject visualObj = new GameObject($"Unit_{unit.template.unitName}_{(isEnemy ? "Enemy" : "Owner")}");
            visualObj.transform.SetParent(transform);
            UnitVisual3D visual = visualObj.AddComponent<UnitVisual3D>();
            visual.Initialize(unit, isEnemy);
            visual.SetPosition(position);
            return visual;
        }

        /// <summary>
        /// Remove visuals for units that are no longer on the board
        /// </summary>
        private void CleanupRemovedUnits(Dictionary<UnitInstance, UnitVisual3D> visuals, HashSet<UnitInstance> currentUnits)
        {
            List<UnitInstance> toRemove = new List<UnitInstance>();

            foreach (var kvp in visuals)
            {
                if (!currentUnits.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var unit in toRemove)
            {
                if (visuals.TryGetValue(unit, out UnitVisual3D visual) && visual != null)
                {
                    Destroy(visual.gameObject);
                }
                visuals.Remove(unit);
            }
        }

        /// <summary>
        /// Show that this player's units are away fighting on another board.
        /// Hides unit visuals and shows an indicator pointing to the fight.
        /// </summary>
        /// <param name="fightBoard">The board where the fight is happening</param>
        /// <param name="homePlayerName">Name of the home player (optional)</param>
        public void ShowUnitsAway(HexBoard3D fightBoard, string homePlayerName = null)
        {
            unitsAway = true;
            awayFightBoard = fightBoard;
            awayFightHomePlayer = homePlayerName;

            // Hide all unit visuals
            foreach (var kvp in ownerUnitVisuals)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.gameObject.SetActive(false);
                }
            }

            // Create an indicator showing units are away
            if (awayIndicator == null && hexBoard != null)
            {
                awayIndicator = new GameObject("UnitsAwayIndicator");
                awayIndicator.transform.SetParent(transform);
                awayIndicator.transform.position = hexBoard.BoardCenter + new Vector3(0, 1f, 0);

                var textMesh = awayIndicator.AddComponent<TextMesh>();
                string displayText = string.IsNullOrEmpty(homePlayerName)
                    ? "Fighting..."
                    : $"Fighting on\n{homePlayerName}'s board";
                textMesh.text = displayText;
                textMesh.fontSize = 36;
                textMesh.characterSize = 0.1f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.color = new Color(1f, 0.8f, 0.3f);

                awayIndicator.AddComponent<LookAtCamera>();
            }

        }

        /// <summary>
        /// Clear the "units away" state, showing units back on this board.
        /// </summary>
        public void ClearUnitsAway()
        {
            unitsAway = false;
            awayFightBoard = null;

            // Show unit visuals again
            foreach (var kvp in ownerUnitVisuals)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.gameObject.SetActive(true);
                }
            }

            // Remove the away indicator
            if (awayIndicator != null)
            {
                Destroy(awayIndicator);
                awayIndicator = null;
            }

        }

        /// <summary>
        /// Clear all visuals
        /// </summary>
        public void ClearAllVisuals()
        {
            foreach (var kvp in ownerUnitVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            ownerUnitVisuals.Clear();

            foreach (var kvp in enemyUnitVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            enemyUnitVisuals.Clear();
        }

        private void OnDestroy()
        {
            DisconnectFromSimulation();
            ClearUnitsAway();
            ClearAllVisuals();
        }
    }

    /// <summary>
    /// Simple component to make an object always face the camera
    /// </summary>
    public class LookAtCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main != null)
            {
                transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                                 Camera.main.transform.rotation * Vector3.up);
            }
        }
    }
}
