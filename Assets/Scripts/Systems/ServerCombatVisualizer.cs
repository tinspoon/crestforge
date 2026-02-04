using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Crestforge.Data;
using Crestforge.Networking;
using Crestforge.Visuals;

namespace Crestforge.Systems
{
    /// <summary>
    /// Manages combat visualization across all boards.
    /// Supports the player's own combat and scouted combats on different boards.
    /// </summary>
    public class ServerCombatVisualizer : MonoBehaviour
    {
        public static ServerCombatVisualizer Instance { get; private set; }

        [Header("Settings")]
        public float tickDuration = 0.05f; // Match server's 50ms tick rate for real-time playback
        public float playbackSpeed = 1f;
        public bool autoStartPlayback = true;

        [Header("Visual References")]
        public Transform combatBoardParent;

        [Header("State - Player's Combat")]
        public bool isPlaying;
        public int currentTick;
        public int totalTicks;
        public bool isInVictoryPose;

        // Player's primary combat playback
        private CombatPlayback playerCombat;

        // All active combat playbacks (board -> playback)
        private Dictionary<HexBoard3D, CombatPlayback> activeCombats = new Dictionary<HexBoard3D, CombatPlayback>();

        // Combat data for player's combat
        private List<ServerCombatEvent> events = new List<ServerCombatEvent>();
        private string myTeam;
        private string opponentTeam;
        private string combatWinner;
        private bool isHostPlayer;
        private HexBoard3D combatBoard;

        // Away bench visuals are now managed by BoardVisualRegistry via TeleportToAwayPosition
        // No longer needed: benchUnits dictionary, benchContainer, benchSlotPositions

        // Reference to player's board when they're fighting away
        private HexBoard3D playerBoardWhenAway;

        // Away player bench visuals created by the HOST (since the away player's visuals don't exist on host's machine)
        private List<UnitVisual3D> awayPlayerBenchVisuals = new List<UnitVisual3D>();

        // Events
        public event Action OnCombatVisualizationStarted;
        public event Action OnCombatVisualizationEnded;
        public event Action<string, int> OnUnitDamaged;
        public event Action<string> OnUnitDied;

        private bool isSubscribed = false;
        private bool isPhaseSubscribed = false;

        /// <summary>
        /// Get the player's combat playback for syncing
        /// </summary>
        public CombatPlayback PlayerCombat => playerCombat;

        /// <summary>
        /// Returns true if combat visualization is currently playing
        /// </summary>
        public bool IsPlayingCombat => isPlaying && playerCombat != null && playerCombat.CombatUnits.Count > 0;

        /// <summary>
        /// Get the world positions of all bench slots (computed from registry or combatBoard)
        /// </summary>
        public List<Vector3> GetBenchSlotPositions()
        {
            var positions = new List<Vector3>();
            if (combatBoard == null) return positions;

            // Get bench slot positions from the away bench area (behind row 7)
            int benchSize = Crestforge.Core.GameConstants.Player.BENCH_SIZE;
            float slotSpacing = 0.9f;
            float totalWidth = (benchSize - 1) * slotSpacing;
            float startX = combatBoard.transform.position.x - totalWidth / 2f;

            Vector3 row7Pos = combatBoard.GetTileWorldPosition(3, 7);
            float benchZ = row7Pos.z + 1.5f;
            float benchY = 0.15f;

            for (int i = 0; i < benchSize; i++)
            {
                float x = startX + i * slotSpacing;
                positions.Add(new Vector3(x, benchY, benchZ));
            }
            return positions;
        }

        /// <summary>
        /// Find a CombatUnitVisual by its associated UnitVisual3D
        /// </summary>
        public CombatUnitVisual GetCombatUnitByVisual(UnitVisual3D visual)
        {
            // Check player's combat first
            if (playerCombat != null)
            {
                var result = playerCombat.GetCombatUnitByVisual(visual);
                if (result != null) return result;
            }

            // Check other active combats
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value != playerCombat)
                {
                    var result = kvp.Value.GetCombatUnitByVisual(visual);
                    if (result != null) return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Find a CombatUnitVisual by instance ID
        /// </summary>
        public CombatUnitVisual GetCombatUnitByInstanceId(string instanceId)
        {
            // Check player's combat first
            if (playerCombat != null)
            {
                var result = playerCombat.GetCombatUnitByInstanceId(instanceId);
                if (result != null) return result;
            }

            // Check other active combats
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value != playerCombat)
                {
                    var result = kvp.Value.GetCombatUnitByInstanceId(instanceId);
                    if (result != null) return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Find a bench unit by its UnitVisual3D (now uses registry)
        /// </summary>
        public ServerUnitData GetBenchUnitByVisual(UnitVisual3D visual)
        {
            if (visual == null) return null;

            // If we're away, check the player's board registry
            if (playerBoardWhenAway != null && playerBoardWhenAway.Registry != null)
            {
                foreach (var kvp in playerBoardWhenAway.Registry.BenchVisuals)
                {
                    if (kvp.Value == visual)
                    {
                        // Get the bench unit data from server state
                        var serverState = ServerGameState.Instance;
                        if (serverState?.bench != null && kvp.Key < serverState.bench.Length)
                        {
                            return serverState.bench[kvp.Key];
                        }
                    }
                }
            }
            return null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            SubscribeToEvents();
        }

        private void Start()
        {
            if (!isSubscribed)
            {
                SubscribeToEvents();
            }
        }

        private void Update()
        {
            if (!isSubscribed || !isPhaseSubscribed)
            {
                SubscribeToEvents();
            }

            // Sync state from player's combat playback
            if (playerCombat != null)
            {
                isPlaying = playerCombat.IsPlaying;
                currentTick = playerCombat.CurrentTick;
                totalTicks = playerCombat.TotalTicks;
                isInVictoryPose = playerCombat.IsInVictoryPose;
                combatWinner = playerCombat.CombatWinner;
            }

            // If we're the host and combat is playing, sync away player bench visuals
            // This handles the case where the away player buys units during combat
            if (isPlaying && isHostPlayer && combatBoard != null)
            {
                SyncAwayPlayerBenchVisuals();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            if (!isSubscribed)
            {
                var nm = NetworkManager.Instance;
                if (nm != null)
                {
                    nm.OnCombatEventsReceived += HandleCombatEvents;
                    isSubscribed = true;
                }
            }

            if (!isPhaseSubscribed)
            {
                var serverState = ServerGameState.Instance;
                if (serverState != null)
                {
                    serverState.OnPhaseChanged += HandlePhaseChanged;
                    isPhaseSubscribed = true;
                }
            }
        }

        private void UnsubscribeFromEvents()
        {
            var nm = NetworkManager.Instance;
            if (nm != null)
            {
                nm.OnCombatEventsReceived -= HandleCombatEvents;
            }

            if (isPhaseSubscribed)
            {
                var serverState = ServerGameState.Instance;
                if (serverState != null)
                {
                    serverState.OnPhaseChanged -= HandlePhaseChanged;
                }
                isPhaseSubscribed = false;
            }
        }

        private void HandlePhaseChanged(string newPhase)
        {

            if (newPhase == "planning" && isInVictoryPose)
            {
                EndVictoryPose();
            }
        }

        private void HandleCombatEvents(List<ServerCombatEvent> combatEvents)
        {

            if (combatEvents == null || combatEvents.Count == 0)
            {
                Debug.LogWarning("[CombatVisualizationManager] No combat events received");
                return;
            }

            var serverState = ServerGameState.Instance;
            myTeam = serverState?.currentCombatTeam ?? "player1";
            opponentTeam = myTeam == "player1" ? "player2" : "player1";

            string localPlayerId = serverState?.localPlayerId;
            string hostPlayerId = serverState?.currentHostPlayerId;
            isHostPlayer = string.IsNullOrEmpty(hostPlayerId) || (localPlayerId == hostPlayerId);


            events = combatEvents;

            if (autoStartPlayback)
            {
                StartPlayback();
            }
        }

        /// <summary>
        /// Start playing combat visualization for the player's combat
        /// </summary>
        public void StartPlayback()
        {
            if (events.Count == 0)
            {
                Debug.LogWarning("[CombatVisualizationManager] No events to play");
                return;
            }

            var cameraSetup = IsometricCameraSetup.Instance;
            var serverState = ServerGameState.Instance;

            // Determine which board to use
            if (isHostPlayer)
            {
                combatBoard = HexBoard3D.Instance;
                if (Game3DSetup.Instance != null)
                {
                    var setupBoard = Game3DSetup.Instance.GetPlayerBoard();
                    if (setupBoard != null)
                    {
                        combatBoard = setupBoard;
                    }
                }
            }
            else
            {
                string hostPlayerId = serverState?.currentHostPlayerId;
                int hostBoardIndex = serverState?.GetPlayerBoardIndex(hostPlayerId) ?? -1;

                if (hostBoardIndex >= 0 && Game3DSetup.Instance != null)
                {
                    combatBoard = Game3DSetup.Instance.GetBoardByIndex(hostBoardIndex);
                }

                if (combatBoard == null)
                {
                    combatBoard = HexBoard3D.Instance;
                    Debug.LogWarning("[CombatVisualizationManager] Could not find host board, using local board");
                }
            }

            // Set camera position
            bool isScoutingOpponent = Crestforge.UI.ScoutingUI.Instance != null && Crestforge.UI.ScoutingUI.Instance.IsViewingOpponent;
            if (cameraSetup != null && combatBoard != null && !isScoutingOpponent)
            {
                bool viewFromOpposite = (myTeam == "player2");
                cameraSetup.FocusOnBoard(combatBoard, viewFromOpposite);
            }

            // For away player: teleport visuals to the combat board BEFORE playback starts
            // This must happen first so original positions are saved correctly
            if (!isHostPlayer)
            {
                // Get player's board
                playerBoardWhenAway = null;
                if (Game3DSetup.Instance != null)
                {
                    playerBoardWhenAway = Game3DSetup.Instance.GetPlayerBoard();
                }
                if (playerBoardWhenAway == null)
                {
                    playerBoardWhenAway = HexBoard3D.Instance;
                }

                // Teleport visuals to the away position on combat board
                if (playerBoardWhenAway != null && playerBoardWhenAway.Registry != null && combatBoard != null)
                {
                    playerBoardWhenAway.Registry.TeleportToAwayPosition(combatBoard);

                    // Set the combat board (host's board) to render benches reversed
                    // since we're viewing from the opposite side (camera at 180°)
                    if (combatBoard.Registry != null)
                    {
                        combatBoard.Registry.ViewFromOppositeSide = true;
                    }
                }
            }
            else
            {
                // We are the HOST - create bench visuals for the AWAY player on our combat board
                // The away player's bench visuals don't exist on our machine, so we need to create them
                CreateAwayPlayerBenchVisuals(serverState, combatBoard);
            }

            // Create player's combat playback
            playerCombat = new CombatPlayback(this);
            playerCombat.tickDuration = tickDuration;
            playerCombat.playbackSpeed = playbackSpeed;

            // Subscribe to events
            playerCombat.OnPlaybackStarted += () => OnCombatVisualizationStarted?.Invoke();
            playerCombat.OnPlaybackEnded += () => OnCombatVisualizationEnded?.Invoke();
            playerCombat.OnUnitDamaged += (id, dmg) => OnUnitDamaged?.Invoke(id, dmg);
            playerCombat.OnUnitDied += (id) => OnUnitDied?.Invoke(id);

            // Track in active combats
            activeCombats[combatBoard] = playerCombat;

            // Start playback - for away player, also reuse existing visuals since we teleported them
            playerCombat.StartPlayback(combatBoard, events, myTeam, isHostPlayer, 0, reuseExistingVisuals: true);

            isPlaying = true;
        }

        /// <summary>
        /// Play combat visualization on a specific board (for scouting)
        /// </summary>
        /// <param name="board">The board to visualize on</param>
        /// <param name="combatEvents">The combat events to play</param>
        /// <param name="team">The team to view as</param>
        /// <param name="isHost">Whether the viewed team is the host</param>
        /// <param name="startTick">Tick to start from (for sync)</param>
        public CombatPlayback PlayCombat(HexBoard3D board, List<ServerCombatEvent> combatEvents, string team, bool isHost, int startTick = 0)
        {
            if (board == null || combatEvents == null || combatEvents.Count == 0)
            {
                Debug.LogWarning("[CombatVisualizationManager] Invalid parameters for PlayCombat");
                return null;
            }

            // Stop existing combat on this board if any
            if (activeCombats.TryGetValue(board, out var existing) && existing != playerCombat)
            {
                existing.StopPlayback();
                activeCombats.Remove(board);
            }

            // Create new playback
            var playback = new CombatPlayback(this);
            playback.tickDuration = tickDuration;
            playback.playbackSpeed = playbackSpeed;

            activeCombats[board] = playback;

            playback.StartPlayback(board, combatEvents, team, isHost, startTick, reuseExistingVisuals: false);


            return playback;
        }

        /// <summary>
        /// Stop combat playback on a specific board
        /// </summary>
        public void StopCombatOnBoard(HexBoard3D board)
        {
            if (activeCombats.TryGetValue(board, out var playback))
            {
                if (playback != playerCombat)
                {
                    playback.StopPlayback();
                    activeCombats.Remove(board);
                }
            }
        }

        /// <summary>
        /// Stop all combat visualization playback
        /// </summary>
        public void StopPlayback()
        {
            isPlaying = false;
            isInVictoryPose = false;
            combatWinner = null;

            // Stop all combats
            foreach (var kvp in activeCombats)
            {
                kvp.Value.StopPlayback();
            }
            activeCombats.Clear();
            playerCombat = null;

            StopAllCoroutines();
            CleanupAfterCombat();
        }

        /// <summary>
        /// End victory pose and clean up
        /// </summary>
        public void EndVictoryPose()
        {

            if (!isInVictoryPose) return;

            isInVictoryPose = false;

            // End victory pose on all combats
            foreach (var kvp in activeCombats)
            {
                kvp.Value.EndVictoryPose();
            }

            CleanupAfterCombat();
        }

        private void CleanupAfterCombat()
        {

            // Clear active combats
            activeCombats.Clear();
            playerCombat = null;

            // Clear away player bench visuals (if we were the host)
            ClearAwayPlayerBenchVisuals();
            cachedAwayPlayerId = null;

            // Return visuals from away position (if we were away)
            if (playerBoardWhenAway != null && playerBoardWhenAway.Registry != null && playerBoardWhenAway.Registry.IsAway)
            {
                playerBoardWhenAway.Registry.ReturnFromAway();
            }
            playerBoardWhenAway = null;

            // Reset opposite-side viewing flag on combat board
            if (combatBoard != null && combatBoard.Registry != null)
            {
                combatBoard.Registry.ViewFromOppositeSide = false;
            }

            // Restore board units if we're the host
            var boardManager = BoardManager3D.Instance;
            if (isHostPlayer && boardManager != null)
            {
                boardManager.SetBoardUnitVisualsVisible(true);
            }

            // Reset camera to player's board
            bool isScoutingOpponent = Crestforge.UI.ScoutingUI.Instance != null && Crestforge.UI.ScoutingUI.Instance.IsViewingOpponent;
            if (!isScoutingOpponent)
            {
                var cameraSetup = IsometricCameraSetup.Instance;
                HexBoard3D playerBoard = null;
                if (Game3DSetup.Instance != null)
                {
                    playerBoard = Game3DSetup.Instance.GetPlayerBoard();
                }
                if (playerBoard == null)
                {
                    playerBoard = HexBoard3D.Instance;
                }

                if (cameraSetup != null && playerBoard != null)
                {
                    cameraSetup.FocusOnBoard(playerBoard, false);
                }
            }
        }

        // CreateBenchVisuals, CreateBenchSlotBackground, ClearBenchVisuals removed
        // Away bench visuals are now managed by BoardVisualRegistry.TeleportToAwayPosition

        /// <summary>
        /// Create bench visuals for the away player on the host's combat board.
        /// Called only when we are the HOST player, since the away player's visuals don't exist on our machine.
        /// </summary>
        private void CreateAwayPlayerBenchVisuals(ServerGameState serverState, HexBoard3D board)
        {
            if (serverState == null || board == null) return;

            // Find the away player (the player we're fighting who isn't the host)
            // We need to look at the matchup to find who we're fighting
            string awayPlayerId = null;
            string localPlayerId = serverState.localPlayerId;
            string hostPlayerId = serverState.currentHostPlayerId;

            // If we're the host, find our opponent from the matchups
            foreach (var matchup in serverState.matchups)
            {
                if (matchup.hostPlayerId == localPlayerId)
                {
                    // We're hosting - find the away player
                    if (matchup.player1 == localPlayerId)
                    {
                        awayPlayerId = matchup.player2;
                    }
                    else if (matchup.player2 == localPlayerId)
                    {
                        awayPlayerId = matchup.player1;
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(awayPlayerId))
            {
                return;
            }

            // Get the away player's bench data
            ServerPlayerData awayPlayer = null;
            foreach (var player in serverState.otherPlayers)
            {
                if (player.clientId == awayPlayerId)
                {
                    awayPlayer = player;
                    break;
                }
            }

            if (awayPlayer == null || awayPlayer.bench == null)
            {
                return;
            }

            // Clear any existing away player bench visuals
            ClearAwayPlayerBenchVisuals();

            // Calculate bench positions (behind row 7 on combat board)
            int benchSize = Crestforge.Core.GameConstants.Player.BENCH_SIZE;
            float slotSpacing = 0.8f;
            float totalWidth = (benchSize - 1) * slotSpacing;
            float startX = board.transform.position.x - totalWidth / 2f;

            int lastRow = Crestforge.Core.GameConstants.Grid.HEIGHT * 2 - 1;
            Vector3 lastRowPos = board.GetTileWorldPosition(0, lastRow);
            float benchZ = lastRowPos.z + 1.5f;
            float benchY = 0.15f;

            int createdCount = 0;
            for (int i = 0; i < awayPlayer.bench.Length && i < benchSize; i++)
            {
                var benchUnit = awayPlayer.bench[i];
                if (benchUnit == null || string.IsNullOrEmpty(benchUnit.instanceId)) continue;

                float x = startX + i * slotSpacing;
                Vector3 benchPos = new Vector3(x, benchY, benchZ);

                // Create visual for this bench unit
                UnitVisual3D visual = CreateBenchUnitVisualForAway(benchUnit, benchPos, i);
                if (visual != null)
                {
                    awayPlayerBenchVisuals.Add(visual);
                    createdCount++;
                }
            }

        }

        /// <summary>
        /// Create a single bench visual for the away player
        /// </summary>
        private UnitVisual3D CreateBenchUnitVisualForAway(ServerUnitData serverUnit, Vector3 position, int slotIndex)
        {
            var serverState = ServerGameState.Instance;
            if (serverState == null) return null;

            UnitData template = serverState.GetUnitTemplate(serverUnit.unitId);
            if (template == null)
            {
                Debug.LogWarning($"[CombatVisualizationManager] Could not find template for bench unit: {serverUnit.unitId}");
                return null;
            }

            UnitInstance tempUnit = UnitInstance.Create(template, serverUnit.starLevel);

            GameObject visualObj = new GameObject($"AwayBenchUnit_{serverUnit.name}_{slotIndex}");
            visualObj.transform.position = position;

            UnitVisual3D visual = visualObj.AddComponent<UnitVisual3D>();
            visual.Initialize(tempUnit, false);
            visual.SetPosition(position);
            visual.ServerInstanceId = serverUnit.instanceId;
            visual.SetServerItems(serverUnit.items);

            return visual;
        }

        /// <summary>
        /// Clear away player bench visuals created by the host
        /// </summary>
        private void ClearAwayPlayerBenchVisuals()
        {
            foreach (var visual in awayPlayerBenchVisuals)
            {
                if (visual != null && visual.gameObject != null)
                {
                    Destroy(visual.gameObject);
                }
            }
            awayPlayerBenchVisuals.Clear();
        }

        // Cache for away player sync
        private string cachedAwayPlayerId;

        /// <summary>
        /// Sync away player bench visuals - called during combat to update when away player buys/moves units
        /// </summary>
        private void SyncAwayPlayerBenchVisuals()
        {
            var serverState = ServerGameState.Instance;
            if (serverState == null || combatBoard == null) return;

            // Find the away player ID (cache it for efficiency)
            if (string.IsNullOrEmpty(cachedAwayPlayerId))
            {
                string localPlayerId = serverState.localPlayerId;
                foreach (var matchup in serverState.matchups)
                {
                    if (matchup.hostPlayerId == localPlayerId)
                    {
                        cachedAwayPlayerId = matchup.player1 == localPlayerId ? matchup.player2 : matchup.player1;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(cachedAwayPlayerId)) return;

            // Get the away player's bench data
            ServerPlayerData awayPlayer = null;
            foreach (var player in serverState.otherPlayers)
            {
                if (player.clientId == cachedAwayPlayerId)
                {
                    awayPlayer = player;
                    break;
                }
            }

            if (awayPlayer == null || awayPlayer.bench == null) return;

            // Calculate bench positions
            int benchSize = Crestforge.Core.GameConstants.Player.BENCH_SIZE;
            float slotSpacing = 0.8f;
            float totalWidth = (benchSize - 1) * slotSpacing;
            float startX = combatBoard.transform.position.x - totalWidth / 2f;

            int lastRow = Crestforge.Core.GameConstants.Grid.HEIGHT * 2 - 1;
            Vector3 lastRowPos = combatBoard.GetTileWorldPosition(0, lastRow);
            float benchZ = lastRowPos.z + 1.5f;
            float benchY = 0.15f;

            // Build a set of existing visual instance IDs
            HashSet<string> existingIds = new HashSet<string>();
            foreach (var visual in awayPlayerBenchVisuals)
            {
                if (visual != null && !string.IsNullOrEmpty(visual.ServerInstanceId))
                {
                    existingIds.Add(visual.ServerInstanceId);
                }
            }

            // Check for new bench units
            for (int i = 0; i < awayPlayer.bench.Length && i < benchSize; i++)
            {
                var benchUnit = awayPlayer.bench[i];
                if (benchUnit == null || string.IsNullOrEmpty(benchUnit.instanceId)) continue;

                // Skip if we already have a visual for this unit
                if (existingIds.Contains(benchUnit.instanceId)) continue;

                // Create a new visual for this unit
                float x = startX + i * slotSpacing;
                Vector3 benchPos = new Vector3(x, benchY, benchZ);

                UnitVisual3D visual = CreateBenchUnitVisualForAway(benchUnit, benchPos, i);
                if (visual != null)
                {
                    awayPlayerBenchVisuals.Add(visual);
                }
            }

            // Update positions for existing visuals (in case of bench swaps)
            HashSet<string> currentBenchIds = new HashSet<string>();
            for (int i = 0; i < awayPlayer.bench.Length && i < benchSize; i++)
            {
                var benchUnit = awayPlayer.bench[i];
                if (benchUnit != null && !string.IsNullOrEmpty(benchUnit.instanceId))
                {
                    currentBenchIds.Add(benchUnit.instanceId);

                    // Find the visual and update its position
                    foreach (var visual in awayPlayerBenchVisuals)
                    {
                        if (visual != null && visual.ServerInstanceId == benchUnit.instanceId)
                        {
                            float x = startX + i * slotSpacing;
                            Vector3 benchPos = new Vector3(x, benchY, benchZ);
                            if (Vector3.Distance(visual.transform.position, benchPos) > 0.1f)
                            {
                                visual.SetPosition(benchPos);
                            }
                            break;
                        }
                    }
                }
            }

            // Remove visuals for units no longer on bench (sold)
            for (int i = awayPlayerBenchVisuals.Count - 1; i >= 0; i--)
            {
                var visual = awayPlayerBenchVisuals[i];
                if (visual != null && !currentBenchIds.Contains(visual.ServerInstanceId))
                {
                    Destroy(visual.gameObject);
                    awayPlayerBenchVisuals.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Swap the visual positions of two bench units (uses registry)
        /// </summary>
        public void SwapBenchUnitPositions(int fromSlot, int toSlot)
        {
            if (playerBoardWhenAway != null && playerBoardWhenAway.Registry != null)
            {
                playerBoardWhenAway.Registry.SwapBenchSlots(fromSlot, toSlot);
            }
        }

        /// <summary>
        /// Clean up all combat visuals
        /// </summary>
        public void ClearCombatVisuals()
        {
            CleanupAfterCombat();
        }
    }

    // BenchUnitVisual class removed - bench visuals now managed by BoardVisualRegistry

    /// <summary>
    /// Handles drag interaction for combat bench units
    /// </summary>
    public class CombatBenchDragHandler : MonoBehaviour
    {
        public string instanceId;
        public int slotIndex;
        public Vector3 originalPosition;

        private bool isDragging;
        private Camera mainCamera;
        private Plane dragPlane;

        private void Start()
        {
            mainCamera = Camera.main;
            originalPosition = transform.position;
        }

        private void OnMouseDown()
        {
            if (mainCamera == null) return;

            isDragging = true;
            dragPlane = new Plane(Vector3.up, transform.position);

            if (IsometricCameraSetup.Instance != null)
            {
                IsometricCameraSetup.Instance.inputBlocked = true;
            }
        }

        private void OnMouseDrag()
        {
            if (!isDragging || mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            float distance;
            if (dragPlane.Raycast(ray, out distance))
            {
                Vector3 point = ray.GetPoint(distance);
                transform.position = new Vector3(point.x, originalPosition.y, point.z);
            }
        }

        private void OnMouseUp()
        {
            if (!isDragging) return;

            isDragging = false;

            if (IsometricCameraSetup.Instance != null)
            {
                IsometricCameraSetup.Instance.inputBlocked = false;
            }

            int targetSlot = FindClosestBenchSlot();

            if (targetSlot >= 0 && targetSlot != slotIndex)
            {
                var serverState = Networking.ServerGameState.Instance;
                if (serverState != null)
                {
                    serverState.MoveBenchUnit(instanceId, targetSlot);
                }

                var visualizer = ServerCombatVisualizer.Instance;
                if (visualizer != null)
                {
                    visualizer.SwapBenchUnitPositions(slotIndex, targetSlot);
                }
            }
            else
            {
                transform.position = originalPosition;
            }
        }

        private int FindClosestBenchSlot()
        {
            var visualizer = ServerCombatVisualizer.Instance;
            if (visualizer == null) return -1;

            float closestDist = float.MaxValue;
            int closestSlot = -1;

            var slotPositions = visualizer.GetBenchSlotPositions();
            for (int i = 0; i < slotPositions.Count; i++)
            {
                float dist = Vector3.Distance(transform.position, slotPositions[i]);
                if (dist < closestDist && dist < 1.0f)
                {
                    closestDist = dist;
                    closestSlot = i;
                }
            }

            return closestSlot;
        }
    }
}
