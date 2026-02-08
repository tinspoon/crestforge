using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Crestforge.Networking;
using Crestforge.Systems;
using Crestforge.Data;

namespace Crestforge.UI
{
    /// <summary>
    /// UI for multiplayer lobby - create/join rooms and ready up.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        public static LobbyUI Instance { get; private set; }

        [Header("Settings")]
        public string defaultServerUrl = "ws://localhost:8080";

        // UI References (created dynamically)
        private Canvas lobbyCanvas;
        private GameObject connectPanel;
        private GameObject lobbyPanel;
        private GameObject roomPanel;

        private InputField serverUrlInput;
        private InputField playerNameInput;
        private InputField roomCodeInput;
        private Text statusText;
        private Text roomCodeText;
        private Text playersText;
        private Button connectButton;
        private Button createRoomButton;
        private Button joinRoomButton;
        private Button readyButton;
        private Button leaveButton;
        private Button startGameButton;
        private Button refreshRoomsButton;

        // Room list
        private Transform roomListContainer;
        private Text noRoomsText;
        private List<GameObject> roomListItems = new List<GameObject>();
        private float lastRoomListRequest = 0f;
        private const float ROOM_LIST_REFRESH_INTERVAL = 3f;

        private bool isInitialized;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            CreateUI();
            SubscribeToEvents();
            UpdateUI();

            isInitialized = true;
        }

        private void SubscribeToEvents()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;

            nm.OnConnected += HandleConnected;
            nm.OnDisconnected += HandleDisconnected;
            nm.OnError += HandleError;
            nm.OnRoomCreated += HandleRoomCreated;
            nm.OnRoomJoined += HandleRoomJoined;
            nm.OnRoomLeft += HandleRoomLeft;
            nm.OnPlayersUpdated += HandlePlayersUpdated;
            nm.OnGameStart += HandleGameStart;
            nm.OnRoomListReceived += HandleRoomListReceived;
        }

        private void UnsubscribeFromEvents()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;

            nm.OnConnected -= HandleConnected;
            nm.OnDisconnected -= HandleDisconnected;
            nm.OnError -= HandleError;
            nm.OnRoomCreated -= HandleRoomCreated;
            nm.OnRoomJoined -= HandleRoomJoined;
            nm.OnRoomLeft -= HandleRoomLeft;
            nm.OnPlayersUpdated -= HandlePlayersUpdated;
            nm.OnGameStart -= HandleGameStart;
            nm.OnRoomListReceived -= HandleRoomListReceived;
        }

        // ============================================
        // UI Creation
        // ============================================

        private void CreateUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("LobbyCanvas");
            canvasObj.transform.SetParent(transform);
            lobbyCanvas = canvasObj.AddComponent<Canvas>();
            lobbyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            lobbyCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Create panels
            CreateConnectPanel();
            CreateLobbyPanel();
            CreateRoomPanel();
        }

        private void CreateConnectPanel()
        {
            connectPanel = CreatePanel("ConnectPanel");

            // Title
            CreateText(connectPanel.transform, "CrestForge", 36, new Vector2(0, 200), FontStyle.Bold);
            CreateText(connectPanel.transform, "Multiplayer", 24, new Vector2(0, 150));

            // Server URL
            CreateText(connectPanel.transform, "Server:", 16, new Vector2(-100, 50));
            serverUrlInput = CreateInputField(connectPanel.transform, defaultServerUrl, new Vector2(50, 50), 250);

            // Player Name
            CreateText(connectPanel.transform, "Name:", 16, new Vector2(-100, -20));
            playerNameInput = CreateInputField(connectPanel.transform, "Player", new Vector2(50, -20), 200);

            // Connect Button
            connectButton = CreateButton(connectPanel.transform, "Connect", new Vector2(0, -100), OnConnectClicked);

            // Status
            statusText = CreateText(connectPanel.transform, "Not connected", 14, new Vector2(0, -170));
            statusText.color = Color.gray;
        }

        private void CreateLobbyPanel()
        {
            lobbyPanel = CreatePanel("LobbyPanel");
            lobbyPanel.SetActive(false);

            // Make lobby panel taller to fit room list
            var lobbyRT = lobbyPanel.GetComponent<RectTransform>();
            lobbyRT.sizeDelta = new Vector2(400, 600);

            // Title
            CreateText(lobbyPanel.transform, "Lobby", 28, new Vector2(0, 260), FontStyle.Bold);

            // Create Room
            createRoomButton = CreateButton(lobbyPanel.transform, "Create Room", new Vector2(0, 180), OnCreateRoomClicked);

            // Join Room
            CreateText(lobbyPanel.transform, "Room Code:", 16, new Vector2(-80, 110));
            roomCodeInput = CreateInputField(lobbyPanel.transform, "", new Vector2(50, 110), 100);
            roomCodeInput.characterLimit = 4;
            roomCodeInput.contentType = InputField.ContentType.Alphanumeric;

            joinRoomButton = CreateButton(lobbyPanel.transform, "Join", new Vector2(150, 110), OnJoinRoomClicked);
            var joinRT = joinRoomButton.GetComponent<RectTransform>();
            joinRT.sizeDelta = new Vector2(80, 40);

            // Available Rooms Section
            CreateText(lobbyPanel.transform, "Available Rooms:", 16, new Vector2(0, 50), FontStyle.Bold);

            // Room list container
            GameObject roomListObj = new GameObject("RoomListContainer");
            roomListObj.transform.SetParent(lobbyPanel.transform, false);
            RectTransform roomListRT = roomListObj.AddComponent<RectTransform>();
            roomListRT.anchoredPosition = new Vector2(0, -50);
            roomListRT.sizeDelta = new Vector2(350, 150);

            // Add vertical layout group for room list
            VerticalLayoutGroup vlg = roomListObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(5, 5, 5, 5);

            roomListContainer = roomListObj.transform;

            // No rooms text
            noRoomsText = CreateText(roomListContainer, "No rooms available", 14, Vector2.zero);
            noRoomsText.color = Color.gray;
            var noRoomsLE = noRoomsText.gameObject.AddComponent<LayoutElement>();
            noRoomsLE.preferredHeight = 30;

            // Refresh button
            refreshRoomsButton = CreateButton(lobbyPanel.transform, "Refresh", new Vector2(140, 50), OnRefreshRoomsClicked);
            var refreshRT = refreshRoomsButton.GetComponent<RectTransform>();
            refreshRT.sizeDelta = new Vector2(80, 30);

            // Disconnect
            CreateButton(lobbyPanel.transform, "Disconnect", new Vector2(0, -220), OnDisconnectClicked);
        }

        private void CreateRoomPanel()
        {
            roomPanel = CreatePanel("RoomPanel");
            roomPanel.SetActive(false);

            // Room Code
            roomCodeText = CreateText(roomPanel.transform, "Room: ----", 28, new Vector2(0, 200), FontStyle.Bold);

            // Players
            playersText = CreateText(roomPanel.transform, "Players:\n...", 18, new Vector2(0, 80));
            playersText.alignment = TextAnchor.UpperCenter;

            // Ready Button
            readyButton = CreateButton(roomPanel.transform, "Ready", new Vector2(-80, -80), OnReadyClicked);

            // Leave Button
            leaveButton = CreateButton(roomPanel.transform, "Leave", new Vector2(80, -80), OnLeaveClicked);

            // Start Game (host only)
            startGameButton = CreateButton(roomPanel.transform, "Start Game", new Vector2(0, -160), OnStartGameClicked);
            startGameButton.gameObject.SetActive(false);
        }

        private GameObject CreatePanel(string name)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(lobbyCanvas.transform, false);

            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 500);

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            return panel;
        }

        private Text CreateText(Transform parent, string text, int fontSize, Vector2 position, FontStyle style = FontStyle.Normal)
        {
            GameObject obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(350, 40);

            Text t = obj.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            return t;
        }

        private InputField CreateInputField(Transform parent, string placeholder, Vector2 position, float width)
        {
            GameObject obj = new GameObject("InputField");
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width, 35);

            Image bg = obj.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.25f);

            InputField input = obj.AddComponent<InputField>();

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10, 0);
            textRT.offsetMax = new Vector2(-10, 0);

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            input.textComponent = text;
            input.text = placeholder;

            return input;
        }

        private Button CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            GameObject obj = new GameObject("Button_" + text);
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(150, 45);

            Image bg = obj.AddComponent<Image>();
            bg.color = new Color(0.3f, 0.5f, 0.7f);

            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            Text t = textObj.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 18;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            return btn;
        }

        // ============================================
        // Button Handlers
        // ============================================

        private void OnConnectClicked()
        {
            Debug.Log("[LobbyUI] Connect button clicked");

            var nm = NetworkManager.Instance;
            if (nm == null)
            {
                Debug.Log("[LobbyUI] Creating NetworkManager");
                // Create NetworkManager if it doesn't exist
                GameObject nmObj = new GameObject("NetworkManager");
                nmObj.AddComponent<NetworkManager>();
                nm = NetworkManager.Instance;
                SubscribeToEvents();
            }

            string url = serverUrlInput.text;
            string playerName = playerNameInput.text;

            Debug.Log($"[LobbyUI] Connecting to: {url}");

            if (string.IsNullOrEmpty(playerName))
            {
                playerName = "Player_" + Random.Range(1000, 9999);
                playerNameInput.text = playerName;
            }

            nm.playerName = playerName;
            statusText.text = "Connecting...";
            statusText.color = Color.yellow;

            nm.Connect(url);
        }

        private void OnDisconnectClicked()
        {
            NetworkManager.Instance?.Disconnect();
            UpdateUI();
        }

        private void OnCreateRoomClicked()
        {
            NetworkManager.Instance?.SetPlayerName(playerNameInput.text);
            NetworkManager.Instance?.CreateRoom();
        }

        private void OnJoinRoomClicked()
        {
            string code = roomCodeInput.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("Enter a room code");
                return;
            }

            NetworkManager.Instance?.SetPlayerName(playerNameInput.text);
            NetworkManager.Instance?.JoinRoom(code);
        }

        private void OnReadyClicked()
        {
            var nm = NetworkManager.Instance;
            if (nm != null)
            {
                nm.SetReady(!nm.isReady);
                UpdateReadyButton();
            }
        }

        private void OnLeaveClicked()
        {
            NetworkManager.Instance?.LeaveRoom();
        }

        private void OnRefreshRoomsClicked()
        {
            RequestRoomList();
        }

        private void OnRoomItemClicked(string roomId)
        {
            Debug.Log($"[LobbyUI] Joining room: {roomId}");
            NetworkManager.Instance?.SetPlayerName(playerNameInput.text);
            NetworkManager.Instance?.JoinRoom(roomId);
        }

        private void OnStartGameClicked()
        {
            // In a full implementation, this would signal the server to start
            // For now, both players need to ready up
            Debug.Log("Start game requested");
        }

        // ============================================
        // Event Handlers
        // ============================================

        private void HandleConnected()
        {
            statusText.text = "Connected!";
            statusText.color = Color.green;
            NetworkManager.Instance?.SetPlayerName(playerNameInput.text);
            UpdateUI();

            // Request room list immediately when connected
            RequestRoomList();
        }

        private void HandleDisconnected()
        {
            statusText.text = "Disconnected";
            statusText.color = Color.red;
            UpdateUI();
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[LobbyUI] Connection error: {error}");
            statusText.text = $"Error: {error}";
            statusText.color = Color.red;
            UpdateUI(); // Refresh UI to show connect panel again
        }

        private void HandleRoomCreated(string roomId)
        {
            Debug.Log($"Room created: {roomId}");
            UpdateUI();
        }

        private void HandleRoomJoined(string roomId)
        {
            Debug.Log($"Joined room: {roomId}");
            UpdateUI();
        }

        private void HandleRoomLeft()
        {
            Debug.Log("Left room");
            UpdateUI();
        }

        private void HandlePlayersUpdated(List<PlayerInfo> players)
        {
            UpdatePlayersDisplay(players);
        }

        private void HandleRoomListReceived(List<RoomListItem> rooms)
        {
            UpdateRoomListDisplay(rooms);
        }

        private void HandleGameStart(int round)
        {
            Debug.Log($"[LobbyUI] HandleGameStart called! Round {round}");

            // Hide lobby UI
            Hide();
            Debug.Log("[LobbyUI] Lobby UI hidden");

            // Ensure ServerGameState exists for multiplayer
            if (ServerGameState.Instance == null)
            {
                Debug.Log("[LobbyUI] Creating ServerGameState");
                GameObject sgsObj = new GameObject("ServerGameState");
                sgsObj.AddComponent<ServerGameState>();
            }

            // Copy unit data reference to ServerGameState
            if (ServerGameState.Instance != null)
            {
                if (GameState.Instance != null && GameState.Instance.allUnits != null)
                {
                    ServerGameState.Instance.allUnits = GameState.Instance.allUnits;
                }
                else
                {
                    // Fallback: Load unit data from Resources
                    ServerGameState.Instance.allUnits = Resources.LoadAll<UnitData>("Units");
                    Debug.Log($"[LobbyUI] Loaded {ServerGameState.Instance.allUnits?.Length ?? 0} units from Resources");
                }
            }

            // Show game visuals
            if (Crestforge.Visuals.Game3DSetup.Instance != null)
            {
                Crestforge.Visuals.Game3DSetup.Instance.ShowGameVisuals();
            }

            // Show game UI
            GameUI gameUI = GameUI.Instance;
            if (gameUI == null)
            {
                Debug.Log("[LobbyUI] GameUI.Instance is null, searching...");
                gameUI = Object.FindAnyObjectByType<GameUI>(FindObjectsInactive.Include);
            }
            if (gameUI != null)
            {
                Debug.Log("[LobbyUI] Activating GameUI");
                gameUI.Show();
            }
            else
            {
                Debug.LogWarning("[LobbyUI] GameUI not found!");
            }

            // Note: In server-authoritative mode, we don't call RoundManager.StartGame()
            // The server controls the game state and sends updates
            Debug.Log("[LobbyUI] Multiplayer game started - server is authoritative");
        }

        // ============================================
        // UI Updates
        // ============================================

        private void UpdateUI()
        {
            var nm = NetworkManager.Instance;

            bool isConnected = nm != null && nm.IsConnected;
            bool isInRoom = nm != null && nm.IsInRoom;

            connectPanel.SetActive(!isConnected);
            lobbyPanel.SetActive(isConnected && !isInRoom);
            roomPanel.SetActive(isInRoom);

            if (isInRoom && nm != null)
            {
                roomCodeText.text = $"Room: {nm.currentRoomId}";
                UpdatePlayersDisplay(nm.playersInRoom);
                UpdateReadyButton();

                // Show start button for host (when all ready)
                bool allReady = nm.playersInRoom.TrueForAll(p => p.isReady);
                startGameButton.gameObject.SetActive(nm.isHost && nm.playersInRoom.Count >= 2 && allReady);
            }
        }

        private void UpdatePlayersDisplay(List<PlayerInfo> players)
        {
            var nm = NetworkManager.Instance;
            string text = "Players:\n";

            foreach (var player in players)
            {
                string readyMark = player.isReady ? " [Ready]" : "";
                string youMark = (nm != null && player.id == nm.clientId) ? " (You)" : "";
                text += $"\n{player.name}{youMark}{readyMark}";
            }

            if (playersText != null)
            {
                playersText.text = text;
            }
        }

        private void UpdateReadyButton()
        {
            var nm = NetworkManager.Instance;
            if (nm == null || readyButton == null) return;

            var btnText = readyButton.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = nm.isReady ? "Not Ready" : "Ready";
            }

            var btnImage = readyButton.GetComponent<Image>();
            if (btnImage != null)
            {
                btnImage.color = nm.isReady ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.3f, 0.5f, 0.7f);
            }
        }

        private void UpdateRoomListDisplay(List<RoomListItem> rooms)
        {
            // Clear existing room items
            foreach (var item in roomListItems)
            {
                if (item != null) Destroy(item);
            }
            roomListItems.Clear();

            // Show/hide "no rooms" text
            if (noRoomsText != null)
            {
                noRoomsText.gameObject.SetActive(rooms == null || rooms.Count == 0);
            }

            if (rooms == null || rooms.Count == 0) return;

            // Create room entries
            foreach (var room in rooms)
            {
                CreateRoomListItem(room);
            }
        }

        private void CreateRoomListItem(RoomListItem room)
        {
            if (roomListContainer == null) return;

            GameObject itemObj = new GameObject($"Room_{room.id}");
            itemObj.transform.SetParent(roomListContainer, false);

            RectTransform rt = itemObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(340, 40);

            Image bg = itemObj.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.3f, 0.4f, 0.9f);

            LayoutElement le = itemObj.AddComponent<LayoutElement>();
            le.preferredHeight = 40;

            // Room info text
            GameObject textObj = new GameObject("RoomText");
            textObj.transform.SetParent(itemObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10, 0);
            textRT.offsetMax = new Vector2(-60, 0);

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.text = $"{room.id} - {room.hostName} ({room.playerCount}/{room.maxPlayers})";

            // Join button
            GameObject joinBtnObj = new GameObject("JoinBtn");
            joinBtnObj.transform.SetParent(itemObj.transform, false);
            RectTransform joinBtnRT = joinBtnObj.AddComponent<RectTransform>();
            joinBtnRT.anchorMin = new Vector2(1, 0);
            joinBtnRT.anchorMax = new Vector2(1, 1);
            joinBtnRT.pivot = new Vector2(1, 0.5f);
            joinBtnRT.anchoredPosition = new Vector2(-5, 0);
            joinBtnRT.sizeDelta = new Vector2(50, 30);

            Image joinBtnBg = joinBtnObj.AddComponent<Image>();
            joinBtnBg.color = new Color(0.3f, 0.6f, 0.3f);

            Button joinBtn = joinBtnObj.AddComponent<Button>();
            joinBtn.targetGraphic = joinBtnBg;
            string roomId = room.id; // Capture for closure
            joinBtn.onClick.AddListener(() => OnRoomItemClicked(roomId));

            // Join button text
            GameObject joinTextObj = new GameObject("Text");
            joinTextObj.transform.SetParent(joinBtnObj.transform, false);
            RectTransform joinTextRT = joinTextObj.AddComponent<RectTransform>();
            joinTextRT.anchorMin = Vector2.zero;
            joinTextRT.anchorMax = Vector2.one;
            joinTextRT.offsetMin = Vector2.zero;
            joinTextRT.offsetMax = Vector2.zero;

            Text joinText = joinTextObj.AddComponent<Text>();
            joinText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            joinText.fontSize = 12;
            joinText.fontStyle = FontStyle.Bold;
            joinText.alignment = TextAnchor.MiddleCenter;
            joinText.color = Color.white;
            joinText.text = "Join";

            roomListItems.Add(itemObj);
        }

        private void RequestRoomList()
        {
            var nm = NetworkManager.Instance;
            if (nm != null && nm.IsConnected && !nm.IsInRoom)
            {
                nm.RequestRoomList();
                lastRoomListRequest = Time.time;
            }
        }

        private void Update()
        {
            // Periodically refresh room list when in lobby
            if (lobbyPanel != null && lobbyPanel.activeSelf)
            {
                if (Time.time - lastRoomListRequest > ROOM_LIST_REFRESH_INTERVAL)
                {
                    RequestRoomList();
                }
            }
        }

        // ============================================
        // Public Methods
        // ============================================

        public void Show()
        {
            if (!isInitialized) Initialize();
            lobbyCanvas.gameObject.SetActive(true);
            UpdateUI();
        }

        public void Hide()
        {
            if (lobbyCanvas != null)
            {
                lobbyCanvas.gameObject.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (lobbyCanvas != null && lobbyCanvas.gameObject.activeSelf)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }
    }
}
