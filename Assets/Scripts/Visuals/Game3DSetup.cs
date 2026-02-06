using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Combat;
using Crestforge.Systems;
using Crestforge.UI;
using Crestforge.Networking;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Sets up the 3D isometric visual system for Crestforge.
    /// Attach this to an empty GameObject in the scene to initialize all 3D components.
    /// </summary>
    public class Game3DSetup : MonoBehaviour
    {
        public static Game3DSetup Instance { get; private set; }

        [Header("Setup Options")]
        [Tooltip("Disable old 2D renderers automatically")]
        public bool disableOld2DSystem = true;
        
        [Tooltip("Auto-create lighting")]
        public bool createLighting = true;

        [Header("Visual Settings")]
        [Tooltip("Don't modify lighting - use scene's existing lighting settings")]
        public bool preserveSceneLighting = true;
        [Tooltip("Use skybox for ambient lighting instead of flat color (ignored if preserveSceneLighting is true)")]
        public bool useSkyboxAmbient = true;
        public Color ambientColor = new Color(0.3f, 0.3f, 0.35f);
        public Color directionalLightColor = new Color(1f, 0.95f, 0.9f);
        public float directionalLightIntensity = 1f;

        [Header("Multi-Board PvP")]
        [Tooltip("Enable 2x2 grid of boards (player + 3 opponents)")]
        public bool enableMultiBoard = true;
        [Tooltip("Spacing between boards in world units")]
        public float boardSpacing = 20f;

        [Header("Visibility")]
        [Tooltip("Hide game visuals until game starts (for lobby flow)")]
        public bool hideUntilGameStart = true;
        private bool gameVisualsVisible = false;

        // References to all boards
        private HexBoard3D playerBoard;
        private List<HexBoard3D> opponentBoards = new List<HexBoard3D>();
        private List<OpponentBoardVisualizer> opponentVisualizers = new List<OpponentBoardVisualizer>();

        // Multiplayer board management
        private List<HexBoard3D> allBoardsByIndex = new List<HexBoard3D>(); // Boards ordered by server index (0-3)
        private int localBoardIndex = -1; // Server-assigned board index for local player
        private bool isMultiplayerMode = false;

        /// <summary>
        /// Call this from anywhere to ensure 3D system is initialized
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInitialize()
        {
            // Check if Game3DSetup already exists
            if (FindAnyObjectByType<Game3DSetup>() != null) return;

            // Auto-create the setup object (even if HexBoard3D exists, we'll use it)
            GameObject setupObj = new GameObject("Game3DSetup");
            Game3DSetup setup = setupObj.AddComponent<Game3DSetup>();
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            SetupScene();
        }

        private void Start()
        {
            // Subscribe to ServerGameState events for multiplayer board positioning
            StartCoroutine(SubscribeToServerGameStateDelayed());
        }

        private System.Collections.IEnumerator SubscribeToServerGameStateDelayed()
        {
            // Wait for ServerGameState to initialize
            while (ServerGameState.Instance == null)
            {
                yield return null;
            }

            ServerGameState.Instance.OnBoardIndexAssigned += HandleBoardIndexAssigned;

            // Check if we already have a board index assigned (in case we missed the event)
            if (ServerGameState.Instance.IsInGame && localBoardIndex < 0)
            {
                HandleBoardIndexAssigned(ServerGameState.Instance.localBoardIndex);
            }
        }

        private void OnDestroy()
        {
            if (ServerGameState.Instance != null)
            {
                ServerGameState.Instance.OnBoardIndexAssigned -= HandleBoardIndexAssigned;
            }
        }

        /// <summary>
        /// Handle board index assignment from server for multiplayer mode.
        /// Points camera to the local player's assigned board.
        /// </summary>
        private void HandleBoardIndexAssigned(int boardIndex)
        {
            localBoardIndex = boardIndex;
            isMultiplayerMode = true;

            // Find the board for this player's index and focus camera on it
            StartCoroutine(FocusCameraOnAssignedBoardDelayed(boardIndex));
        }

        private System.Collections.IEnumerator FocusCameraOnAssignedBoardDelayed(int boardIndex)
        {
            // Wait a frame for boards to be ready
            yield return null;

            // Get all boards and find the one matching our index
            HexBoard3D targetBoard = null;

            // Check allBoardsByIndex first
            if (boardIndex >= 0 && boardIndex < allBoardsByIndex.Count)
            {
                targetBoard = allBoardsByIndex[boardIndex];
            }

            // If not found, try to build the list from existing boards
            if (targetBoard == null)
            {
                var allBoards = new List<HexBoard3D>();
                if (playerBoard != null) allBoards.Add(playerBoard);
                allBoards.AddRange(opponentBoards);

                // Store in allBoardsByIndex for future reference
                allBoardsByIndex.Clear();
                allBoardsByIndex.AddRange(allBoards);

                if (boardIndex >= 0 && boardIndex < allBoards.Count)
                {
                    targetBoard = allBoards[boardIndex];
                }
            }

            if (targetBoard != null)
            {
                // Mark this as the player's board
                playerBoard = targetBoard;
                targetBoard.isPlayerBoard = true;
                targetBoard.boardLabel = "Your Board";
                HexBoard3D.Instance = targetBoard;

                // Away bench visuals now handled by BoardVisualRegistry.TeleportToAwayPosition

                // Update BoardManager3D to use this board
                if (BoardManager3D.Instance != null)
                {
                    BoardManager3D.Instance.SetPlayerBoard(targetBoard);
                }

                // Focus camera on this board
                if (IsometricCameraSetup.Instance != null)
                {
                    IsometricCameraSetup.Instance.FocusOnBoard(targetBoard);
                }

                // Mark other boards as opponent boards and set up server player IDs
                foreach (var board in allBoardsByIndex)
                {
                    if (board != null && board != targetBoard)
                    {
                        board.isPlayerBoard = false;
                    }
                }

                // Set up server player IDs for opponent board visualizers (with retry)
                StartCoroutine(SetupOpponentBoardServerIdsDelayed());
            }
            else
            {
                Debug.LogWarning($"[Game3DSetup] Could not find board for index {boardIndex}");
            }
        }

        /// <summary>
        /// Set up server player IDs with retry mechanism
        /// </summary>
        private System.Collections.IEnumerator SetupOpponentBoardServerIdsDelayed()
        {
            // Wait for server state to be populated
            int maxAttempts = 30;
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                var serverState = ServerGameState.Instance;
                if (serverState != null && serverState.otherPlayers != null && serverState.otherPlayers.Count > 0)
                {
                    // Check if at least one player has a valid board index
                    bool hasValidIndex = false;
                    foreach (var player in serverState.otherPlayers)
                    {
                        if (player.boardIndex >= 0)
                        {
                            hasValidIndex = true;
                            break;
                        }
                    }

                    if (hasValidIndex)
                    {
                        SetupOpponentBoardServerIds();
                        yield break;
                    }
                }

                attempt++;
                yield return new WaitForSeconds(0.5f);
            }

            Debug.LogWarning("[Game3DSetup] Failed to set up opponent board server IDs after max attempts");
        }

        /// <summary>
        /// Set up server player IDs for all opponent board visualizers.
        /// Creates visualizers if they don't exist.
        /// </summary>
        private void SetupOpponentBoardServerIds()
        {
            var serverState = ServerGameState.Instance;
            if (serverState == null || serverState.otherPlayers == null)
            {
                Debug.LogWarning("[Game3DSetup] SetupOpponentBoardServerIds: No server state or other players");
                return;
            }

            foreach (var player in serverState.otherPlayers)
            {

                if (player.boardIndex >= 0 && player.boardIndex < allBoardsByIndex.Count)
                {
                    var board = allBoardsByIndex[player.boardIndex];
                    if (board != null && board != playerBoard) // Don't add visualizer to player's own board
                    {
                        var visualizer = board.GetComponent<OpponentBoardVisualizer>();
                        if (visualizer == null)
                        {
                            visualizer = board.GetComponentInChildren<OpponentBoardVisualizer>();
                        }

                        // Create visualizer if it doesn't exist
                        if (visualizer == null)
                        {
                            GameObject vizObj = new GameObject($"OpponentVisualizer_{player.name}");
                            vizObj.transform.SetParent(board.transform);
                            visualizer = vizObj.AddComponent<OpponentBoardVisualizer>();
                            visualizer.hexBoard = board;
                        }

                        visualizer.SetServerPlayerId(player.clientId);
                    }
                    else if (board == playerBoard)
                    {
                    }
                }
                else
                {
                    Debug.LogWarning($"[Game3DSetup] Invalid board index {player.boardIndex} for player {player.name}");
                }
            }
        }

        /// <summary>
        /// Initialize the 3D scene
        /// </summary>
        public void SetupScene()
        {

            // Disable old 2D renderers
            if (disableOld2DSystem)
            {
                DisableOld2DComponents();
            }

            // Setup camera
            SetupCamera();

            // Setup lighting
            if (createLighting)
            {
                SetupLighting();
            }

            // Create board(s) - either single board or 2x2 grid
            if (enableMultiBoard)
            {
                CreateMultiBoardLayout();
            }
            else
            {
                CreateHexBoard();
            }

            // Create board manager
            CreateBoardManager();

            // Create VFX, Audio, and other systems
            CreateVFXAndAudioSystems();

            // Create server combat visualizer for multiplayer
            CreateServerCombatVisualizer();

            // Create scenery manager for cosmetic decorations
            CreateSceneryManager();

            // Hide visuals initially if configured (for lobby flow)
            if (hideUntilGameStart)
            {
                HideGameVisuals();
            }

        }

        /// <summary>
        /// Hide all game visuals (boards, units, etc.) - used before game starts
        /// </summary>
        public void HideGameVisuals()
        {
            gameVisualsVisible = false;

            // Hide player board
            if (playerBoard != null)
            {
                playerBoard.gameObject.SetActive(false);
            }

            // Hide opponent boards
            foreach (var board in opponentBoards)
            {
                if (board != null) board.gameObject.SetActive(false);
            }

            // Hide BoardManager3D visuals
            if (BoardManager3D.Instance != null)
            {
                BoardManager3D.Instance.gameObject.SetActive(false);
            }

        }

        /// <summary>
        /// Show all game visuals - called when game starts
        /// </summary>
        public void ShowGameVisuals()
        {
            gameVisualsVisible = true;

            // Show player board
            if (playerBoard != null)
            {
                playerBoard.gameObject.SetActive(true);
            }

            // Show opponent boards (respecting enableMultiBoard setting)
            if (enableMultiBoard)
            {
                foreach (var board in opponentBoards)
                {
                    if (board != null) board.gameObject.SetActive(true);
                }
            }

            // Show BoardManager3D
            if (BoardManager3D.Instance != null)
            {
                BoardManager3D.Instance.gameObject.SetActive(true);
            }

        }

        /// <summary>
        /// Check if game visuals are currently visible
        /// </summary>
        public bool AreGameVisualsVisible => gameVisualsVisible;

        private void DisableOld2DComponents()
        {
            // Try to find and disable any old 2D rendering components
            // These are optional - the 3D system works independently
            
            // Look for any MonoBehaviour with these names to disable
            string[] componentsToDisable = new string[] {
                "HexGrid", "UnitRender", "DamageText", "FloatingText", 
                "CombatVisual", "BoardRender", "GridRender", "FloatingCombat",
                "CombatText", "DamagePopup", "TextPopup"
            };
            
            // Also look for GameObjects with these names to disable entirely
            string[] objectsToDisable = new string[] {
                "HexGrid", "UnitRenderer", "FloatingTextCanvas", "DamageCanvas",
                "CombatCanvas", "2DBoard", "BoardRenderer"
            };
            
            MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var component in allComponents)
            {
                if (component == null) continue;
                
                string typeName = component.GetType().Name;
                string nameSpace = component.GetType().Namespace ?? "";
                
                // Skip our own 3D components
                if (nameSpace.Contains("Visuals")) continue;
                
                foreach (string searchName in componentsToDisable)
                {
                    if (typeName.Contains(searchName))
                    {
                        component.enabled = false;
                        break;
                    }
                }
            }
            
            // Disable specific GameObjects
            foreach (string objName in objectsToDisable)
            {
                GameObject obj = GameObject.Find(objName);
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }

            // Disable old camera controller if it exists (but not our IsometricCameraSetup)
            if (Camera.main != null)
            {
                MonoBehaviour[] cameraComponents = Camera.main.GetComponents<MonoBehaviour>();
                foreach (var component in cameraComponents)
                {
                    if (component == null) continue;
                    
                    string typeName = component.GetType().Name;
                    if (typeName.Contains("CameraController") && !(component is IsometricCameraSetup))
                    {
                        component.enabled = false;
                    }
                }
            }
            
            // Find and disable any Canvas that might be rendering 2D board stuff
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in allCanvases)
            {
                if (canvas == null) continue;
                string canvasName = canvas.gameObject.name.ToLower();
                if (canvasName.Contains("board") || canvasName.Contains("combat") ||
                    canvasName.Contains("floating") || canvasName.Contains("damage"))
                {
                    canvas.gameObject.SetActive(false);
                }
            }
        }

        private void SetupCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
            }

            // Add isometric camera setup
            IsometricCameraSetup isoCam = mainCam.GetComponent<IsometricCameraSetup>();
            if (isoCam == null)
            {
                isoCam = mainCam.gameObject.AddComponent<IsometricCameraSetup>();
            }

            // Configure camera - view from behind player, zoomed out to see whole board
            isoCam.orthoSize = 8f;
            isoCam.rotationY = 0f;  // Straight ahead
            isoCam.rotationX = 45f; // Looking down at board
            isoCam.distance = 15f;
            isoCam.minOrthoSize = 4f;
            isoCam.maxOrthoSize = 15f;

            // Add audio listener if missing
            if (mainCam.GetComponent<AudioListener>() == null)
            {
                mainCam.gameObject.AddComponent<AudioListener>();
            }
        }

        private void SetupLighting()
        {
            // If preserving scene lighting, don't modify anything
            if (preserveSceneLighting)
            {
                return;
            }

            // Find or create directional light
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light dirLight = null;

            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    dirLight = light;
                    break;
                }
            }

            if (dirLight == null)
            {
                // Create directional light if none exists
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                dirLight.color = directionalLightColor;
                dirLight.intensity = directionalLightIntensity;
                dirLight.shadows = LightShadows.Soft;
            }

            // Configure ambient lighting based on settings
            if (useSkyboxAmbient)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                if (RenderSettings.skybox == null)
                {
                    Debug.LogWarning("[Game3DSetup] Skybox ambient enabled but no skybox material set. Using flat ambient as fallback.");
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = ambientColor;
                }
            }
            else
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = ambientColor;
            }

        }

        private void CreateHexBoard()
        {
            HexBoard3D existingBoard = FindAnyObjectByType<HexBoard3D>();
            if (existingBoard != null)
            {
                playerBoard = existingBoard;
                StartCoroutine(CreateSingleBoardBenchSlotsDelayed(existingBoard));
                return;
            }

            GameObject boardObj = new GameObject("HexBoard3D");
            HexBoard3D board = boardObj.AddComponent<HexBoard3D>();

            // Configure board visuals
            board.hexRadius = 0.45f;
            board.playerTileColor = new Color(0.25f, 0.4f, 0.55f);
            board.enemyTileColor = new Color(0.55f, 0.3f, 0.3f);
            board.isPlayerBoard = true;

            playerBoard = board;

            // Create bench slots after board initializes
            StartCoroutine(CreateSingleBoardBenchSlotsDelayed(board));
        }

        private System.Collections.IEnumerator CreateSingleBoardBenchSlotsDelayed(HexBoard3D board)
        {
            // Wait for board to generate tiles in Start()
            yield return null;
            yield return null;

            if (board != null)
            {
                CreateBenchSlotsForBoard(board);
            }
        }

        /// <summary>
        /// Create multiple boards for PvP mode (player + 3 opponents)
        /// Layout: 2x2 grid
        ///   [Opponent 1]  [Opponent 2]
        ///   [Player]      [Opponent 3]
        /// </summary>
        private void CreateMultiBoardLayout()
        {

            opponentBoards.Clear();
            opponentVisualizers.Clear();

            // Calculate positions for 2x2 grid
            Vector3[] positions = new Vector3[]
            {
                new Vector3(-boardSpacing / 2, 0, boardSpacing / 2),  // Top-left: Opponent 1
                new Vector3(boardSpacing / 2, 0, boardSpacing / 2),   // Top-right: Opponent 2
                new Vector3(-boardSpacing / 2, 0, -boardSpacing / 2), // Bottom-left: Player
                new Vector3(boardSpacing / 2, 0, -boardSpacing / 2),  // Bottom-right: Opponent 3
            };

            // Check for existing player board
            HexBoard3D existingBoard = FindAnyObjectByType<HexBoard3D>();

            // Create container for all boards
            GameObject boardsContainer = new GameObject("AllBoards");

            if (existingBoard != null)
            {
                // Use existing board as player board, move it to correct position
                playerBoard = existingBoard;
                playerBoard.transform.SetParent(boardsContainer.transform);
                playerBoard.transform.position = positions[2]; // Bottom-left: Player
                playerBoard.isPlayerBoard = true;
                playerBoard.boardLabel = "Player";

                // Ensure this is set as the Instance (Awake may have run before isPlayerBoard was set)
                HexBoard3D.Instance = playerBoard;

                if (!HexBoard3D.AllBoards.Contains(playerBoard))
                {
                    HexBoard3D.AllBoards.Add(playerBoard);
                }

                // Away bench visuals now handled by BoardVisualRegistry.TeleportToAwayPosition
            }
            else
            {
                // Create new player board
                GameObject playerObj = new GameObject("HexBoard_Player");
                playerObj.transform.SetParent(boardsContainer.transform);
                playerObj.transform.position = positions[2];
                playerBoard = playerObj.AddComponent<HexBoard3D>();
                playerBoard.hexRadius = 0.45f;
                playerBoard.playerTileColor = new Color(0.25f, 0.4f, 0.55f);
                playerBoard.enemyTileColor = new Color(0.55f, 0.3f, 0.3f);
                playerBoard.isPlayerBoard = true;
                playerBoard.boardLabel = "Player";

                // Away bench visuals now handled by BoardVisualRegistry.TeleportToAwayPosition
            }

            // Create 3 opponent boards
            string[] oppLabels = new string[] { "Opponent 1", "Opponent 2", "Opponent 3" };
            int[] posIndices = new int[] { 0, 1, 3 }; // Top-left, Top-right, Bottom-right

            for (int i = 0; i < 3; i++)
            {
                GameObject boardObj = new GameObject($"HexBoard_{oppLabels[i].Replace(" ", "")}");
                boardObj.transform.SetParent(boardsContainer.transform);
                boardObj.transform.position = positions[posIndices[i]];

                HexBoard3D board = boardObj.AddComponent<HexBoard3D>();

                // Copy settings from player board
                board.hexRadius = playerBoard.hexRadius;
                board.playerTileColor = playerBoard.playerTileColor;
                board.enemyTileColor = playerBoard.enemyTileColor;
                board.isPlayerBoard = false;
                board.boardLabel = oppLabels[i];

                // Away bench visuals now handled by BoardVisualRegistry.TeleportToAwayPosition

                // Add OpponentBoardVisualizer (renders opponent units from server state)
                GameObject vizObj = new GameObject($"OpponentVisualizer_{oppLabels[i]}");
                vizObj.transform.SetParent(board.transform);
                var visualizer = vizObj.AddComponent<OpponentBoardVisualizer>();
                visualizer.hexBoard = board;
                opponentVisualizers.Add(visualizer);

                opponentBoards.Add(board);
            }

            // Populate allBoardsByIndex for multiplayer board lookup
            // Order: 0=Top-left, 1=Top-right, 2=Bottom-left (default player), 3=Bottom-right
            allBoardsByIndex.Clear();
            allBoardsByIndex.Add(opponentBoards.Count > 0 ? opponentBoards[0] : null); // Index 0 = Opponent 1 (top-left)
            allBoardsByIndex.Add(opponentBoards.Count > 1 ? opponentBoards[1] : null); // Index 1 = Opponent 2 (top-right)
            allBoardsByIndex.Add(playerBoard);                                          // Index 2 = Player (bottom-left)
            allBoardsByIndex.Add(opponentBoards.Count > 2 ? opponentBoards[2] : null); // Index 3 = Opponent 3 (bottom-right)

            // Create bench slots for all boards after they've generated their tiles
            StartCoroutine(CreateAllBenchSlotsDelayed());

            // Connect opponent boards to OpponentManager after a frame
            // (OpponentManager needs to initialize first)
            StartCoroutine(ConnectOpponentBoardsDelayed());

            // Create MultiCombatOrchestrator for simultaneous battles
            CreateMultiCombatOrchestrator();

            // Focus camera on player board (default for non-multiplayer)
            StartCoroutine(FocusCameraOnPlayerBoardDelayed());

        }

        private System.Collections.IEnumerator FocusCameraOnPlayerBoardDelayed()
        {
            // Wait for boards to initialize
            yield return null;

            if (playerBoard != null)
            {
                // Update BoardManager3D to use the player's board
                if (BoardManager3D.Instance != null)
                {
                    BoardManager3D.Instance.SetPlayerBoard(playerBoard);
                }

                // Focus camera on player board
                if (IsometricCameraSetup.Instance != null)
                {
                    IsometricCameraSetup.Instance.FocusOnBoard(playerBoard);
                }
            }
        }

        /// <summary>
        /// Create bench slots for all boards after they've generated their tiles.
        /// This creates all 8 bench areas (4 home + 4 away) at startup.
        /// </summary>
        private System.Collections.IEnumerator CreateAllBenchSlotsDelayed()
        {
            // Wait for boards to generate their tiles in Start()
            yield return null;
            yield return null; // Extra frame for safety

            // Create bench slots for player board
            if (playerBoard != null)
            {
                CreateBenchSlotsForBoard(playerBoard);
            }

            // Create bench slots for opponent boards
            foreach (var board in opponentBoards)
            {
                if (board != null)
                {
                    CreateBenchSlotsForBoard(board);
                }
            }

        }

        /// <summary>
        /// Get board by server-assigned player index (for multiplayer)
        /// </summary>
        public HexBoard3D GetBoardByPlayerIndex(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < allBoardsByIndex.Count)
            {
                return allBoardsByIndex[playerIndex];
            }
            return null;
        }

        /// <summary>
        /// Check if we're in multiplayer mode
        /// </summary>
        public bool IsMultiplayerMode => isMultiplayerMode;

        /// <summary>
        /// Create the MultiCombatOrchestrator for coordinating all battles
        /// </summary>
        private void CreateMultiCombatOrchestrator()
        {
            if (MultiCombatOrchestrator.Instance != null) return;

            GameObject orchestratorObj = new GameObject("MultiCombatOrchestrator");
            orchestratorObj.AddComponent<MultiCombatOrchestrator>();
        }

        /// <summary>
        /// Check if multi-board mode is active
        /// </summary>
        public bool IsMultiBoardActive => enableMultiBoard && opponentBoards.Count > 0;

        /// <summary>
        /// Get opponent boards for scouting
        /// </summary>
        public List<HexBoard3D> GetOpponentBoards() => opponentBoards;

        /// <summary>
        /// Get the player's board
        /// </summary>
        public HexBoard3D GetPlayerBoard() => playerBoard ?? HexBoard3D.Instance;

        /// <summary>
        /// Get a board by its index (for combat visualization on specific boards)
        /// </summary>
        public HexBoard3D GetBoardByIndex(int boardIndex)
        {
            if (boardIndex >= 0 && boardIndex < allBoardsByIndex.Count)
            {
                return allBoardsByIndex[boardIndex];
            }
            return null;
        }

        /// <summary>
        /// Enable multi-board mode at runtime (call when PvP starts)
        /// </summary>
        public void EnableMultiBoard()
        {
            if (opponentBoards.Count > 0)
            {
                // Just show the boards
                foreach (var board in opponentBoards)
                {
                    if (board != null) board.gameObject.SetActive(true);
                }
                return;
            }

            enableMultiBoard = true;
            CreateMultiBoardLayout();
        }

        /// <summary>
        /// Disable multi-board mode at runtime (for multiplayer mode)
        /// </summary>
        public void DisableMultiBoard()
        {
            enableMultiBoard = false;

            // Hide opponent boards instead of destroying them
            foreach (var board in opponentBoards)
            {
                if (board != null) board.gameObject.SetActive(false);
            }

            foreach (var visualizer in opponentVisualizers)
            {
                if (visualizer != null) visualizer.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Connect opponent boards to OpponentManager after initialization
        /// </summary>
        private System.Collections.IEnumerator ConnectOpponentBoardsDelayed()
        {
            // Wait for game systems to initialize
            yield return null;
            yield return null;
            yield return null; // Extra frame for safety

            var opponentManager = OpponentManager.Instance;
            if (opponentManager == null)
            {
                Debug.LogWarning("[Game3DSetup] OpponentManager not found, cannot connect opponent boards");
                yield break;
            }

            // Wait for GameState to be ready with unit data
            var state = GameState.Instance;
            int waitAttempts = 0;
            while ((state == null || state.allUnits == null || state.allUnits.Length == 0) && waitAttempts < 10)
            {
                yield return null;
                state = GameState.Instance;
                waitAttempts++;
            }

            if (state == null || state.allUnits == null)
            {
                Debug.LogWarning("[Game3DSetup] GameState.allUnits not ready, opponent boards may be empty");
            }

            // Initialize opponents if not already done
            if (opponentManager.opponents.Count == 0)
            {
                opponentManager.InitializeOpponents();
            }

            // Connect each opponent to their board
            for (int i = 0; i < opponentBoards.Count && i < opponentManager.opponents.Count; i++)
            {
                var opponent = opponentManager.opponents[i];
                var board = opponentBoards[i];

                // Set board identity
                board.ownerId = opponent.id;
                board.boardLabel = opponent.name;

                // Find existing visualizer or create one
                var visualizer = board.GetComponentInChildren<OpponentBoardVisualizer>();
                if (visualizer == null)
                {
                    GameObject vizObj = new GameObject($"OpponentVisualizer_{opponent.name}");
                    vizObj.transform.SetParent(board.transform);
                    visualizer = vizObj.AddComponent<OpponentBoardVisualizer>();
                }

                visualizer.Initialize(opponent, board);

                // Store references
                opponent.hexBoard = board;
                opponent.boardVisualizer = visualizer;
                if (!opponentVisualizers.Contains(visualizer))
                {
                    opponentVisualizers.Add(visualizer);
                }
            }

            // Force refresh all visualizers after a frame to ensure units are shown
            yield return null;
            foreach (var visualizer in opponentVisualizers)
            {
                if (visualizer != null)
                {
                    visualizer.Refresh();
                }
            }
        }

        /// <summary>
        /// Create bench slot visuals for a board (both home and away benches).
        /// Home bench is behind row 0, away bench is behind row 7.
        /// </summary>
        private void CreateBenchSlotsForBoard(HexBoard3D board)
        {
            if (board == null)
            {
                Debug.LogWarning("[Game3DSetup] CreateBenchSlotsForBoard called with null board!");
                return;
            }

            int benchSize = GameConstants.Player.BENCH_SIZE;
            float slotSpacing = 0.8f;
            float totalWidth = (benchSize - 1) * slotSpacing;

            // Get board positions for calculating bench placement
            Vector3 firstRowPos = board.GetTileWorldPosition(0, 0);
            int lastRow = GameConstants.Grid.HEIGHT * 2 - 1; // Row 7 (0-indexed)
            Vector3 lastRowPos = board.GetTileWorldPosition(0, lastRow);

            // Calculate bench Z positions (behind row 0 and behind row 7)
            float homeBenchZ = firstRowPos.z - 1.5f;  // Behind row 0 (player's bench)
            float awayBenchZ = lastRowPos.z + 1.5f;   // Behind row 7 (away bench)

            // Calculate X start position (centered on board)
            float startX = board.transform.position.x - totalWidth / 2f;

            // Create container for bench slots - standalone (not parented to board)
            // This ensures bench slots are always visible even when boards are hidden
            GameObject benchContainer = new GameObject($"BenchSlots_{board.boardLabel}");
            benchContainer.transform.position = Vector3.zero;

            // Create home bench slots (behind row 0)
            GameObject homeBench = new GameObject("HomeBench");
            homeBench.transform.SetParent(benchContainer.transform);
            homeBench.transform.localPosition = Vector3.zero;

            for (int i = 0; i < benchSize; i++)
            {
                float x = startX + i * slotSpacing;
                Vector3 position = new Vector3(x, 0.15f, homeBenchZ);
                CreateBenchSlotVisual(homeBench.transform, position, $"HomeBenchSlot_{i}", board.playerTileColor);
            }

            // Create away bench slots (behind row 7)
            GameObject awayBench = new GameObject("AwayBench");
            awayBench.transform.SetParent(benchContainer.transform);
            awayBench.transform.localPosition = Vector3.zero;

            for (int i = 0; i < benchSize; i++)
            {
                float x = startX + i * slotSpacing;
                Vector3 position = new Vector3(x, 0.15f, awayBenchZ);
                CreateBenchSlotVisual(awayBench.transform, position, $"AwayBenchSlot_{i}", board.enemyTileColor);
            }

        }

        /// <summary>
        /// Create a single bench slot visual (a simple colored platform)
        /// </summary>
        private void CreateBenchSlotVisual(Transform parent, Vector3 position, string name, Color baseColor)
        {
            float slotSize = 0.7f;
            float slotHeight = 0.12f;

            GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slot.name = name;
            slot.transform.SetParent(parent);
            slot.transform.position = position;
            slot.transform.localScale = new Vector3(slotSize, slotHeight, slotSize);

            // Remove collider (we don't need collision for bench slots)
            Collider col = slot.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Create material - slightly muted version of the tile color
            MeshRenderer renderer = slot.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Use the exact tile color
                Color slotColor = baseColor;

                // Use URP/Lit shader if available
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader != null)
                {
                    Material mat = new Material(urpShader);
                    mat.SetColor("_BaseColor", slotColor);
                    mat.SetFloat("_Smoothness", 0.15f);
                    mat.SetFloat("_Metallic", 0f);
                    renderer.material = mat;
                }
                else
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = slotColor;
                    mat.SetFloat("_Glossiness", 0.15f);
                    renderer.material = mat;
                }
            }
        }

        private void CreateBoardManager()
        {
            BoardManager3D existingManager = FindAnyObjectByType<BoardManager3D>();
            if (existingManager == null)
            {
                GameObject managerObj = new GameObject("BoardManager3D");
                BoardManager3D manager = managerObj.AddComponent<BoardManager3D>();
            }

        }

        private void CreateServerCombatVisualizer()
        {
            ServerCombatVisualizer existing = FindAnyObjectByType<ServerCombatVisualizer>();
            if (existing != null)
            {
                return;
            }

            GameObject vizObj = new GameObject("ServerCombatVisualizer");
            vizObj.AddComponent<ServerCombatVisualizer>();
        }

        private void CreateVFXAndAudioSystems()
        {
            // Create VFX system
            VFXSystem existingVFX = FindAnyObjectByType<VFXSystem>();
            if (existingVFX == null)
            {
                GameObject vfxObj = new GameObject("VFXSystem");
                vfxObj.AddComponent<VFXSystem>();
            }

            // Create Projectile system
            ProjectileSystem existingProjectile = FindAnyObjectByType<ProjectileSystem>();
            if (existingProjectile == null)
            {
                GameObject projObj = new GameObject("ProjectileSystem");
                projObj.AddComponent<ProjectileSystem>();
            }

            // Create Audio system
            AudioManager existingAudio = FindAnyObjectByType<AudioManager>();
            if (existingAudio == null)
            {
                GameObject audioObj = new GameObject("AudioManager");
                audioObj.AddComponent<AudioManager>();
            }

            // Create OpponentManager for PvP mode
            OpponentManager existingOpponent = FindAnyObjectByType<OpponentManager>();
            if (existingOpponent == null)
            {
                GameObject opponentObj = new GameObject("OpponentManager");
                opponentObj.AddComponent<OpponentManager>();
            }

            // Create ScoutingUI for PvP mode
            ScoutingUI existingScouting = FindAnyObjectByType<ScoutingUI>();
            if (existingScouting == null)
            {
                GameObject scoutingObj = new GameObject("ScoutingUI");
                scoutingObj.AddComponent<ScoutingUI>();
            }

            // Create RoundProgressionUI for PvP mode
            RoundProgressionUI existingProgress = FindAnyObjectByType<RoundProgressionUI>();
            if (existingProgress == null)
            {
                GameObject progressObj = new GameObject("RoundProgressionUI");
                progressObj.AddComponent<RoundProgressionUI>();
            }

            // Create MadMerchantUI for merchant rounds
            MadMerchantUI existingMerchant = FindAnyObjectByType<MadMerchantUI>();
            if (existingMerchant == null)
            {
                GameObject merchantObj = new GameObject("MadMerchantUI");
                merchantObj.AddComponent<MadMerchantUI>();
            }

            // Create MerchantArea3D for 3D merchant rounds (hidden by default)
            MerchantArea3D existingMerchantArea = FindAnyObjectByType<MerchantArea3D>();
            if (existingMerchantArea == null)
            {
                GameObject merchantAreaObj = new GameObject("MerchantArea3D");
                // Position in front of boards (away from the main board area)
                merchantAreaObj.transform.position = new Vector3(0, 0, 25f);
                merchantAreaObj.AddComponent<MerchantArea3D>();
            }

            // Create MajorCrestOrb for major crest rounds (hidden by default)
            MajorCrestOrb existingCrestOrb = FindAnyObjectByType<MajorCrestOrb>();
            if (existingCrestOrb == null)
            {
                GameObject crestOrbObj = new GameObject("MajorCrestOrb");
                crestOrbObj.AddComponent<MajorCrestOrb>();
            }

            // Load UnitModelDatabase if not already assigned
            if (UnitVisual3D.modelDatabase == null)
            {
                var database = Resources.Load<UnitModelDatabase>("UnitModelDatabase");
                if (database != null)
                {
                    UnitVisual3D.modelDatabase = database;
                }
                else
                {
                    Debug.LogWarning("[Game3DSetup] UnitModelDatabase not found in Resources folder");
                }
            }
        }

        private void CreateSceneryManager()
        {
            Cosmetics.SceneryManager existingScenery = FindAnyObjectByType<Cosmetics.SceneryManager>();
            if (existingScenery == null)
            {
                GameObject sceneryObj = new GameObject("SceneryManager");
                sceneryObj.AddComponent<Cosmetics.SceneryManager>();
            }
        }

        /// <summary>
        /// Reset and recreate the 3D scene
        /// </summary>
        [ContextMenu("Rebuild 3D Scene")]
        public void RebuildScene()
        {
            // Destroy existing 3D components
            var board = FindAnyObjectByType<HexBoard3D>();
            if (board != null) DestroyImmediate(board.gameObject);

            var manager = FindAnyObjectByType<BoardManager3D>();
            if (manager != null) DestroyImmediate(manager.gameObject);

            var scenery = FindAnyObjectByType<Cosmetics.SceneryManager>();
            if (scenery != null) DestroyImmediate(scenery.gameObject);

            // Recreate
            SetupScene();
        }
    }
}