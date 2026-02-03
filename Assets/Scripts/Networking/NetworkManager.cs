using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Systems;

namespace Crestforge.Networking
{
    /// <summary>
    /// Connection state for the network manager
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        InRoom,
        InGame
    }

    /// <summary>
    /// Manages WebSocket connection to the game server.
    /// Handles message serialization, routing, and game state synchronization.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Server Settings")]
        [Tooltip("WebSocket server URL")]
        public string serverUrl = "ws://localhost:8080";

        [Header("State")]
        public ConnectionState connectionState = ConnectionState.Disconnected;
        public string clientId;
        public string playerName;
        public string currentRoomId;
        public bool isReady;
        public bool isHost;

        [Header("Room State")]
        public List<PlayerInfo> playersInRoom = new List<PlayerInfo>();
        public RoomInfo currentRoom;

        [Header("Opponent State (for scouting)")]
        public BoardData opponentBoardData;
        public int opponentHealth;
        public int opponentLevel;
        public string opponentName;

        // WebSocket
        private WebSocketClient webSocket;
        private Queue<string> messageQueue = new Queue<string>();
        private readonly object queueLock = new object();

        // Combat event batching
        private List<ServerCombatEvent> pendingCombatEvents = new List<ServerCombatEvent>();
        private int expectedTotalEvents = 0;
        private int currentRound = 0;

        // All combat events for scouting (keyed by playerId)
        private Dictionary<string, AllCombatEventsEntry> allCombatEventsData = new Dictionary<string, AllCombatEventsEntry>();
        public IReadOnlyDictionary<string, AllCombatEventsEntry> AllCombatEvents => allCombatEventsData;

        // Pending scout combat events for batching (keyed by playerId)
        private Dictionary<string, List<ServerCombatEvent>> pendingScoutEvents = new Dictionary<string, List<ServerCombatEvent>>();
        private Dictionary<string, ScoutCombatEventsMessage> pendingScoutMetadata = new Dictionary<string, ScoutCombatEventsMessage>();

        // Events - Connection
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        // Events - Lobby
        public event Action<string> OnRoomCreated;
        public event Action<string> OnRoomJoined;
        public event Action OnRoomLeft;
        public event Action<List<PlayerInfo>> OnPlayersUpdated;
        public event Action<List<RoomListItem>> OnRoomListReceived;

        // Events - Game Flow
        public event Action<int> OnGameStart;
        public event Action<int> OnRoundStart;
        public event Action<string, string> OnGameEnd; // winnerId, winnerName

        // Events - Server-Authoritative State
        public event Action<ServerGameStateData> OnGameStateReceived;
        public event Action<string, float, int> OnPhaseUpdate; // phase, timer, round
        public event Action<List<ServerMatchup>, Dictionary<string, ServerBoardState>> OnCombatStart;
        public event Action<List<ServerCombatEvent>> OnCombatEventsReceived; // Combat visualization events
        public event Action<List<ServerCombatResult>> OnCombatEnd;
        public event Action<string, bool, string> OnActionResult; // action, success, error

        // Events - Legacy (for compatibility)
        public event Action<string, BoardData> OnOpponentBoardUpdate;
        public event Action<string> OnPlayerEndedPlanning;
        public event Action<string, string> OnChatReceived;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Update()
        {
            // Process queued messages on main thread
            ProcessMessageQueue();

            // Update WebSocket (required for some implementations)
            webSocket?.DispatchMessages();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        // ============================================
        // Connection Management
        // ============================================

        /// <summary>
        /// Connect to the game server
        /// </summary>
        public void Connect()
        {
            Connect(serverUrl);
        }

        /// <summary>
        /// Connect to a specific server URL
        /// </summary>
        public void Connect(string url)
        {
            if (connectionState != ConnectionState.Disconnected)
            {
                Debug.LogWarning("[NetworkManager] Already connected or connecting");
                return;
            }

            serverUrl = url;
            connectionState = ConnectionState.Connecting;

            
            webSocket = new WebSocketClient();
            webSocket.OnOpen += HandleOpen;
            webSocket.OnMessage += HandleMessage;
            webSocket.OnClose += HandleClose;
            webSocket.OnError += HandleError;

            StartCoroutine(webSocket.Connect(url));
        }

        /// <summary>
        /// Disconnect from the server
        /// </summary>
        public void Disconnect()
        {
            if (webSocket != null)
            {
                webSocket.Close();
                webSocket = null;
            }

            connectionState = ConnectionState.Disconnected;
            clientId = null;
            currentRoomId = null;
            playersInRoom.Clear();
        }

        /// <summary>
        /// Check if connected to server
        /// </summary>
        public bool IsConnected => connectionState >= ConnectionState.Connected;

        /// <summary>
        /// Check if in a room
        /// </summary>
        public bool IsInRoom => connectionState >= ConnectionState.InRoom;

        /// <summary>
        /// Check if game is in progress
        /// </summary>
        public bool IsInGame => connectionState == ConnectionState.InGame;

        // ============================================
        // Lobby Actions
        // ============================================

        /// <summary>
        /// Set the player's display name
        /// </summary>
        public void SetPlayerName(string name)
        {
            playerName = name;
            Send(new SetNameMessage(name));
        }

        /// <summary>
        /// Create a new game room
        /// </summary>
        public void CreateRoom()
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[NetworkManager] Not connected to server");
                return;
            }

            Send(new CreateRoomMessage());
        }

        /// <summary>
        /// Join an existing room by code
        /// </summary>
        public void JoinRoom(string roomCode)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[NetworkManager] Not connected to server");
                return;
            }

            Send(new JoinRoomMessage(roomCode.ToUpper()));
        }

        /// <summary>
        /// Leave the current room
        /// </summary>
        public void LeaveRoom()
        {
            Send(new LeaveRoomMessage());
        }

        /// <summary>
        /// Request list of available rooms
        /// </summary>
        public void RequestRoomList()
        {
            Send(new ListRoomsMessage());
        }

        /// <summary>
        /// Toggle ready status
        /// </summary>
        public void SetReady(bool ready)
        {
            isReady = ready;
            Send(new ReadyMessage(ready));
        }

        // ============================================
        // Game Actions
        // ============================================

        /// <summary>
        /// Send current board state to server (for scouting)
        /// </summary>
        public void SendBoardUpdate()
        {
            var state = GameState.Instance;
            if (state == null) return;

            var boardData = SerializeBoard(state.playerBoard, state.bench);

            Send(new BoardUpdateMessage(
                boardData,
                state.player.health,
                state.player.gold,
                state.player.level
            ));
        }

        /// <summary>
        /// Signal that planning phase is complete
        /// </summary>
        public void EndPlanning()
        {
            Send(new EndPlanningMessage());
        }

        /// <summary>
        /// Send combat result to server
        /// </summary>
        public void SendCombatResult(bool victory, int remainingHealth, int damageTaken)
        {
            Send(new CombatResultMessage(
                victory ? "victory" : "defeat",
                remainingHealth,
                damageTaken
            ));
        }

        /// <summary>
        /// Send a chat message
        /// </summary>
        public void SendChat(string message)
        {
            Send(new SendChatMessage(message));
        }

        /// <summary>
        /// Send a game action to server (server-authoritative mode)
        /// </summary>
        public void SendGameAction(GameAction action)
        {
            if (!IsInGame)
            {
                Debug.LogWarning("[NetworkManager] Cannot send action - not in game");
                return;
            }

            string actionJson = JsonUtility.ToJson(action);
            string json = $"{{\"type\":\"action\",\"action\":{actionJson}}}";

            // Debug: Validate JSON before sending
                        
            // Validate the JSON is parseable
            try
            {
                // Attempt to verify JSON structure by parsing it back
                var test = JsonUtility.FromJson<GameAction>(actionJson);
                            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManager] JSON validation failed: {e.Message}");
            }

            webSocket?.Send(json);
        }

        // ============================================
        // Board Serialization
        // ============================================

        /// <summary>
        /// Serialize player board to network format
        /// </summary>
        private BoardData SerializeBoard(UnitInstance[,] board, UnitInstance[] bench)
        {
            var data = new BoardData
            {
                units = new List<UnitPlacement>(),
                bench = new List<UnitPlacement>()
            };

            // Serialize board units
            if (board != null)
            {
                for (int x = 0; x < board.GetLength(0); x++)
                {
                    for (int y = 0; y < board.GetLength(1); y++)
                    {
                        var unit = board[x, y];
                        if (unit != null && unit.template != null)
                        {
                            data.units.Add(new UnitPlacement(
                                x, y,
                                unit.template.unitId,
                                unit.starLevel,
                                unit.currentHealth,
                                unit.currentStats.health
                            ));
                        }
                    }
                }
            }

            // Serialize bench units
            if (bench != null)
            {
                for (int i = 0; i < bench.Length; i++)
                {
                    var unit = bench[i];
                    if (unit != null && unit.template != null)
                    {
                        data.bench.Add(new UnitPlacement(
                            i, 0,
                            unit.template.unitId,
                            unit.starLevel,
                            unit.currentHealth,
                            unit.currentStats.health
                        ));
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// Deserialize network board data to game format
        /// </summary>
        public UnitInstance[,] DeserializeBoard(BoardData data)
        {
            if (data == null || data.units == null)
                return new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];

            var board = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];
            var allUnits = GameState.Instance?.allUnits;

            if (allUnits == null)
            {
                Debug.LogWarning("[NetworkManager] Cannot deserialize board - allUnits not loaded");
                return board;
            }

            foreach (var placement in data.units)
            {
                // Find unit template by ID
                UnitData template = null;
                foreach (var unitData in allUnits)
                {
                    if (unitData != null && unitData.unitId == placement.unitId)
                    {
                        template = unitData;
                        break;
                    }
                }

                if (template != null && placement.x >= 0 && placement.x < board.GetLength(0)
                    && placement.y >= 0 && placement.y < board.GetLength(1))
                {
                    var unit = UnitInstance.Create(template, placement.stars);
                    if (placement.currentHealth > 0)
                    {
                        unit.currentHealth = placement.currentHealth;
                    }
                    board[placement.x, placement.y] = unit;
                }
            }

            return board;
        }

        // ============================================
        // Message Handling
        // ============================================

        private void Send(NetworkMessage message)
        {
            if (webSocket == null || !webSocket.IsConnected)
            {
                Debug.LogWarning("[NetworkManager] Cannot send - not connected");
                return;
            }

            string json = JsonUtility.ToJson(message);
                        webSocket.Send(json);
        }

        private void HandleOpen()
        {
                        connectionState = ConnectionState.Connected;

            // Queue for main thread
            lock (queueLock)
            {
                messageQueue.Enqueue("{\"type\":\"_internal_connected\"}");
            }
        }

        private void HandleMessage(string data)
        {
            // Log raw message receipt (on background thread)
            
            // Queue message for main thread processing
            lock (queueLock)
            {
                messageQueue.Enqueue(data);
            }
        }

        private void HandleClose()
        {
                        connectionState = ConnectionState.Disconnected;

            // Queue for main thread
            lock (queueLock)
            {
                messageQueue.Enqueue("{\"type\":\"_internal_disconnected\"}");
            }
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[NetworkManager] WebSocket error: {error}");

            lock (queueLock)
            {
                messageQueue.Enqueue($"{{\"type\":\"_internal_error\",\"message\":\"{error}\"}}");
            }
        }

        private void ProcessMessageQueue()
        {
            while (true)
            {
                string data;
                lock (queueLock)
                {
                    if (messageQueue.Count == 0) break;
                    data = messageQueue.Dequeue();
                }

                ProcessMessage(data);
            }
        }

        private void ProcessMessage(string data)
        {
            try
            {
                // Parse base message to get type
                var baseMsg = JsonUtility.FromJson<NetworkMessage>(data);
                
                switch (baseMsg.type)
                {
                    // Internal events
                    case "_internal_connected":
                        // OnConnected is now fired after 'welcome' message when we have clientId
                                                break;
                    case "_internal_disconnected":
                        OnDisconnected?.Invoke();
                        break;
                    case "_internal_error":
                        var errMsg = JsonUtility.FromJson<ErrorMessage>(data);
                        OnError?.Invoke(errMsg.message);
                        break;

                    // Server messages
                    case "welcome":
                        var welcome = JsonUtility.FromJson<WelcomeMessage>(data);
                        clientId = welcome.clientId;
                                                // Fire OnConnected now that we have our client ID
                        OnConnected?.Invoke();
                        break;

                    case "error":
                        var error = JsonUtility.FromJson<ErrorMessage>(data);
                        Debug.LogError($"[NetworkManager] Server error: {error.message}");
                        OnError?.Invoke(error.message);
                        break;

                    case "nameSet":
                                                break;

                    case "roomCreated":
                        var created = JsonUtility.FromJson<RoomCreatedMessage>(data);
                        currentRoomId = created.roomId;
                        currentRoom = created.room;
                        connectionState = ConnectionState.InRoom;
                        isHost = true;
                        isReady = false;
                                                OnRoomCreated?.Invoke(currentRoomId);
                        break;

                    case "roomJoined":
                        var joined = JsonUtility.FromJson<RoomJoinedMessage>(data);
                        currentRoomId = joined.roomId;
                        currentRoom = joined.room;
                        playersInRoom = joined.players ?? new List<PlayerInfo>();
                        connectionState = ConnectionState.InRoom;
                        isHost = false;
                        isReady = false;
                                                OnRoomJoined?.Invoke(currentRoomId);
                        OnPlayersUpdated?.Invoke(playersInRoom);
                        break;

                    case "leftRoom":
                        currentRoomId = null;
                        currentRoom = null;
                        playersInRoom.Clear();
                        connectionState = ConnectionState.Connected;
                        isReady = false;
                        OnRoomLeft?.Invoke();
                        break;

                    case "becameHost":
                        isHost = true;
                                                break;

                    case "playerJoined":
                        var pJoined = JsonUtility.FromJson<PlayerJoinedMessage>(data);
                        playersInRoom = pJoined.players ?? playersInRoom;
                        OnPlayersUpdated?.Invoke(playersInRoom);
                        break;

                    case "playerLeft":
                        var pLeft = JsonUtility.FromJson<PlayerLeftMessage>(data);
                        playersInRoom = pLeft.players ?? playersInRoom;
                        OnPlayersUpdated?.Invoke(playersInRoom);
                        break;

                    case "playerReady":
                        var pReady = JsonUtility.FromJson<PlayerReadyMessage>(data);
                        playersInRoom = pReady.players ?? playersInRoom;
                        OnPlayersUpdated?.Invoke(playersInRoom);
                        break;

                    case "roomList":
                        var roomList = JsonUtility.FromJson<RoomListMessage>(data);
                        OnRoomListReceived?.Invoke(roomList.rooms ?? new List<RoomListItem>());
                        break;

                    case "gameStart":
                        var gameStart = JsonUtility.FromJson<GameStartMessage>(data);
                        connectionState = ConnectionState.InGame;
                        playersInRoom = gameStart.players ?? playersInRoom;
                                                OnPlayersUpdated?.Invoke(playersInRoom);
                        OnGameStart?.Invoke(gameStart.round);
                        break;

                    case "roundStart":
                        var roundStart = JsonUtility.FromJson<RoundStartMessage>(data);
                        playersInRoom = roundStart.players ?? playersInRoom;
                        isReady = false;
                                                OnPlayersUpdated?.Invoke(playersInRoom);
                        OnRoundStart?.Invoke(roundStart.round);
                        break;

                    case "combatStart":
                                                try
                        {
                            var combatData = JsonUtility.FromJson<ServerCombatStartMessage>(data);
                            
                            currentRound = combatData.round;
                            expectedTotalEvents = combatData.totalEvents;

                            // Store which team we are in this combat for camera positioning
                            if (ServerGameState.Instance != null)
                            {
                                ServerGameState.Instance.currentCombatTeam = combatData.myTeam;
                            }

                            // Clear scout combat events at start of new combat
                            allCombatEventsData.Clear();

                            // Accumulate events from first batch
                            pendingCombatEvents.Clear();
                            if (combatData.combatEvents != null)
                            {
                                pendingCombatEvents.AddRange(combatData.combatEvents);
                            }

                            OnCombatStart?.Invoke(
                                combatData.matchups ?? new List<ServerMatchup>(),
                                new Dictionary<string, ServerBoardState>()
                            );

                            // If all events received in first batch, forward immediately
                            if (pendingCombatEvents.Count >= expectedTotalEvents || expectedTotalEvents == 0)
                            {
                                ForwardCombatEvents();
                            }
                            else
                            {
                                                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[NetworkManager] Failed to parse combat start: {ex.Message}\n{ex.StackTrace}");
                            OnCombatStart?.Invoke(new List<ServerMatchup>(), new Dictionary<string, ServerBoardState>());
                        }
                        break;

                    case "combatEventsBatch":
                        try
                        {
                            var batchData = JsonUtility.FromJson<ServerCombatEventsBatchMessage>(data);
                            
                            if (batchData.combatEvents != null)
                            {
                                pendingCombatEvents.AddRange(batchData.combatEvents);
                            }

                            // Forward events when we have all of them or this is the last batch
                            if (batchData.isLast || pendingCombatEvents.Count >= expectedTotalEvents)
                            {
                                ForwardCombatEvents();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[NetworkManager] Failed to parse combat events batch: {ex.Message}");
                        }
                        break;

                    case "scoutCombatEvents":
                        // Receive combat events for another player (for scouting during combat)
                        try
                        {
                            var scoutData = JsonUtility.FromJson<ScoutCombatEventsMessage>(data);
                            if (scoutData != null && !string.IsNullOrEmpty(scoutData.playerId))
                            {
                                // Check if this is a complete message or first batch
                                if (scoutData.isLast || scoutData.totalEvents <= (scoutData.events?.Count ?? 0))
                                {
                                    // Complete message - store directly
                                    var entry = new AllCombatEventsEntry
                                    {
                                        playerId = scoutData.playerId,
                                        hostPlayerId = scoutData.hostPlayerId,
                                        awayPlayerId = scoutData.awayPlayerId,
                                        events = scoutData.events ?? new List<ServerCombatEvent>()
                                    };
                                    allCombatEventsData[scoutData.playerId] = entry;
                                                                    }
                                else
                                {
                                    // First batch - store metadata and start accumulating
                                    pendingScoutMetadata[scoutData.playerId] = scoutData;
                                    pendingScoutEvents[scoutData.playerId] = new List<ServerCombatEvent>(scoutData.events ?? new List<ServerCombatEvent>());
                                                                    }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[NetworkManager] Failed to parse scoutCombatEvents: {ex.Message}");
                        }
                        break;

                    case "scoutCombatEventsBatch":
                        // Receive additional batched scout combat events
                        try
                        {
                            var batchData = JsonUtility.FromJson<ScoutCombatEventsBatchMessage>(data);
                            if (batchData != null && !string.IsNullOrEmpty(batchData.playerId))
                            {
                                // Add to pending events
                                if (pendingScoutEvents.TryGetValue(batchData.playerId, out var eventList))
                                {
                                    if (batchData.events != null)
                                    {
                                        eventList.AddRange(batchData.events);
                                    }
                                    
                                    // If this is the last batch, finalize
                                    if (batchData.isLast)
                                    {
                                        if (pendingScoutMetadata.TryGetValue(batchData.playerId, out var metadata))
                                        {
                                            var entry = new AllCombatEventsEntry
                                            {
                                                playerId = batchData.playerId,
                                                hostPlayerId = metadata.hostPlayerId,
                                                awayPlayerId = metadata.awayPlayerId,
                                                events = eventList
                                            };
                                            allCombatEventsData[batchData.playerId] = entry;
                                                                                    }
                                        pendingScoutEvents.Remove(batchData.playerId);
                                        pendingScoutMetadata.Remove(batchData.playerId);
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"[NetworkManager] Received scout batch for unknown player: {batchData.playerId}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[NetworkManager] Failed to parse scoutCombatEventsBatch: {ex.Message}");
                        }
                        break;

                    case "combatEnd":
                                                try
                        {
                            var combatEndData = JsonUtility.FromJson<ServerCombatEndMessage>(data);
                            OnCombatEnd?.Invoke(combatEndData.results ?? new List<ServerCombatResult>());
                        }
                        catch
                        {
                            OnCombatEnd?.Invoke(new List<ServerCombatResult>());
                        }
                        break;

                    case "gameState":
                                                try
                        {
                            var stateWrapper = JsonUtility.FromJson<GameStateWrapper>(data);
                            if (stateWrapper.state != null)
                            {
                                OnGameStateReceived?.Invoke(stateWrapper.state);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[NetworkManager] Failed to parse game state: {ex.Message}");
                        }
                        break;

                    case "phaseUpdate":
                        try
                        {
                            var phaseData = JsonUtility.FromJson<PhaseUpdateMessage>(data);
                            OnPhaseUpdate?.Invoke(phaseData.phase, phaseData.timer, phaseData.round);
                        }
                        catch { }
                        break;

                    case "actionResult":
                        try
                        {
                            var actionResult = JsonUtility.FromJson<ActionResultMessage>(data);
                            OnActionResult?.Invoke(actionResult.action, actionResult.success, actionResult.error);
                        }
                        catch { }
                        break;

                    case "gameEnd":
                        var gameEnd = JsonUtility.FromJson<GameEndMessage>(data);
                        connectionState = ConnectionState.InRoom;
                                                OnGameEnd?.Invoke(gameEnd.winnerId, gameEnd.winnerName);
                        break;

                    case "opponentBoardUpdate":
                        var boardUpdate = JsonUtility.FromJson<OpponentBoardUpdateMessage>(data);
                        // Store opponent's board data for scouting
                        opponentBoardData = boardUpdate.board;
                        opponentHealth = boardUpdate.health;
                        opponentLevel = boardUpdate.level;
                        opponentName = boardUpdate.playerName;
                                                OnOpponentBoardUpdate?.Invoke(boardUpdate.playerId, boardUpdate.board);
                        break;

                    case "playerEndedPlanning":
                        var endedPlanning = JsonUtility.FromJson<PlayerEndedPlanningMessage>(data);
                        OnPlayerEndedPlanning?.Invoke(endedPlanning.playerName);
                        break;

                    case "chat":
                        var chat = JsonUtility.FromJson<ChatMessage>(data);
                        OnChatReceived?.Invoke(chat.playerName, chat.message);
                        break;

                    default:
                        Debug.LogWarning($"[NetworkManager] Unknown message type: {baseMsg.type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Error processing message: {e.Message}\nData: {data}");
            }
        }

        // ============================================
        // Utility
        // ============================================

        /// <summary>
        /// Forward accumulated combat events to visualizer
        /// </summary>
        private void ForwardCombatEvents()
        {
            if (pendingCombatEvents.Count > 0)
            {
                int subscriberCount = OnCombatEventsReceived?.GetInvocationList()?.Length ?? 0;
                                try
                {
                    OnCombatEventsReceived?.Invoke(new List<ServerCombatEvent>(pendingCombatEvents));
                                    }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[NetworkManager] Exception invoking OnCombatEventsReceived: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                Debug.LogWarning($"[NetworkManager] No combat events to forward");
            }
            pendingCombatEvents.Clear();
        }

        /// <summary>
        /// Get info for the local player
        /// </summary>
        public PlayerInfo GetLocalPlayer()
        {
            return playersInRoom.Find(p => p.id == clientId);
        }

        /// <summary>
        /// Get info for the opponent player (in 2-player game)
        /// </summary>
        public PlayerInfo GetOpponent()
        {
            return playersInRoom.Find(p => p.id != clientId);
        }
    }
}
