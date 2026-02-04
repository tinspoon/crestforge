using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Systems;
using Crestforge.Data;
using Crestforge.Visuals;
using Crestforge.Networking;

namespace Crestforge.UI
{
    /// <summary>
    /// UI for scouting opponent boards during the planning phase.
    /// Provides buttons to switch camera view between boards.
    /// </summary>
    public class ScoutingUI : MonoBehaviour
    {
        public static ScoutingUI Instance { get; private set; }

        [Header("Settings")]
        public bool showDuringPlanning = true;
        public bool showDuringCombat = true;

        // UI Elements
        private Canvas scoutingCanvas;
        private RectTransform opponentButtonsPanel;
        private RectTransform backButtonPanel;
        private List<Button> opponentButtons = new List<Button>();
        private Button backButton;
        private Text viewingLabel;

        private bool isInitialized = false;
        private bool isViewingOpponent = false;
        private HexBoard3D currentViewedBoard;
        private GamePhase? lastPhase = null;
        private string lastMultiplayerPhase = "";

        // Server-authoritative multiplayer support
        private bool IsServerMultiplayer => ServerGameState.Instance != null && ServerGameState.Instance.IsInGame;
        private ServerGameState serverState => ServerGameState.Instance;

        // Track which server opponent board we're viewing
        private string viewedOpponentId;
        private Dictionary<string, HexBoard3D> serverOpponentBoards = new Dictionary<string, HexBoard3D>();

        // Refresh tracking for scouting
        private float scoutRefreshTimer = 0f;
        private const float SCOUT_REFRESH_INTERVAL = 0.5f;

        // Unit visuals are now handled by OpponentBoardVisualizer via BoardVisualRegistry
        // scoutUnitVisuals dictionary removed - no longer needed

        // Combat visualization tracking (now using unified system)
        private CombatPlayback scoutCombatPlayback;
        private bool isViewingCombat = false;

        /// <summary>
        /// Returns true if the scout combat is in victory pose
        /// </summary>
        public bool IsScoutCombatInVictoryPose => scoutCombatPlayback != null && scoutCombatPlayback.IsInVictoryPose;

        /// <summary>
        /// Returns true if the scout combat is playing
        /// </summary>
        public bool IsScoutCombatPlaying => scoutCombatPlayback != null && scoutCombatPlayback.IsPlaying;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            UpdateVisibility();

            // Periodically refresh scouting visuals when viewing an opponent
            if (isViewingOpponent && IsServerMultiplayer && !string.IsNullOrEmpty(viewedOpponentId))
            {
                scoutRefreshTimer += Time.deltaTime;
                if (scoutRefreshTimer >= SCOUT_REFRESH_INTERVAL)
                {
                    scoutRefreshTimer = 0f;
                    RefreshScoutingVisuals();
                }
            }
        }

        /// <summary>
        /// Refresh scouting state for the currently viewed opponent.
        /// Units are rendered by OpponentBoardVisualizer via BoardVisualRegistry - this just handles combat state transitions.
        /// </summary>
        private void RefreshScoutingVisuals()
        {
            if (!isViewingOpponent || string.IsNullOrEmpty(viewedOpponentId) || serverState == null) return;

            // Find the opponent data
            ServerPlayerData opponent = null;
            foreach (var op in serverState.otherPlayers)
            {
                if (op != null && op.clientId == viewedOpponentId)
                {
                    opponent = op;
                    break;
                }
            }

            if (opponent == null || currentViewedBoard == null) return;

            bool isCombat = serverState.phase == "combat";
            var mainVisualizer = ServerCombatVisualizer.Instance;
            bool mainCombatPlaying = mainVisualizer != null && mainVisualizer.isPlaying;

            // If in combat phase and main combat is still playing, start/continue combat visualization
            if (isCombat && mainCombatPlaying && !isViewingCombat)
            {
                viewingLabel.text = $"Watching: {opponent.name}'s fight";
                StartScoutCombatVisualization(opponent, currentViewedBoard);
                return;
            }

            // If combat visualization was running but main combat ended, transition to static view
            if (isViewingCombat && !mainCombatPlaying)
            {
                StopScoutCombatVisualization();
                viewingLabel.text = $"Scouting: {opponent.name}";
                // Units are rendered by OpponentBoardVisualizer via registry - no action needed
                return;
            }

            // During planning or results, OpponentBoardVisualizer handles unit rendering via registry
            // No action needed here - just maintain camera position
        }

        private void Initialize()
        {
            if (isInitialized) return;

            CreateUI();
            isInitialized = true;
        }

        private void CreateUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("ScoutingCanvas");
            canvasObj.transform.SetParent(transform);
            scoutingCanvas = canvasObj.AddComponent<Canvas>();
            scoutingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scoutingCanvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            CreateOpponentButtonsPanel();
            CreateBackButton();
        }

        private void CreateOpponentButtonsPanel()
        {
            GameObject panelObj = new GameObject("OpponentButtonsPanel");
            panelObj.transform.SetParent(scoutingCanvas.transform, false);
            opponentButtonsPanel = panelObj.AddComponent<RectTransform>();

            opponentButtonsPanel.anchorMin = new Vector2(1, 0.5f);
            opponentButtonsPanel.anchorMax = new Vector2(1, 0.5f);
            opponentButtonsPanel.pivot = new Vector2(1, 0.5f);
            opponentButtonsPanel.anchoredPosition = new Vector2(-10, 0);

            Image bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = panelObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(opponentButtonsPanel, false);
            Text headerText = headerObj.AddComponent<Text>();
            headerText.text = "SCOUT";
            headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerText.fontSize = 16;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = new Color(0.9f, 0.85f, 0.7f);
            LayoutElement headerLE = headerObj.AddComponent<LayoutElement>();
            headerLE.preferredWidth = 100;
            headerLE.preferredHeight = 25;
        }

        private void CreateBackButton()
        {
            GameObject panelObj = new GameObject("BackButtonPanel");
            panelObj.transform.SetParent(scoutingCanvas.transform, false);
            backButtonPanel = panelObj.AddComponent<RectTransform>();

            backButtonPanel.anchorMin = new Vector2(0.5f, 1);
            backButtonPanel.anchorMax = new Vector2(0.5f, 1);
            backButtonPanel.pivot = new Vector2(0.5f, 1);
            backButtonPanel.anchoredPosition = new Vector2(0, -60);
            backButtonPanel.sizeDelta = new Vector2(250, 80);

            Image bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            Outline outline = panelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.6f, 0.8f);
            outline.effectDistance = new Vector2(2, 2);

            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            GameObject labelObj = new GameObject("ViewingLabel");
            labelObj.transform.SetParent(backButtonPanel, false);
            viewingLabel = labelObj.AddComponent<Text>();
            viewingLabel.text = "Viewing: Opponent";
            viewingLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            viewingLabel.fontSize = 14;
            viewingLabel.alignment = TextAnchor.MiddleCenter;
            viewingLabel.color = new Color(0.8f, 0.8f, 0.9f);
            LayoutElement labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 20;

            GameObject btnObj = new GameObject("BackButton");
            btnObj.transform.SetParent(backButtonPanel, false);
            RectTransform btnRT = btnObj.AddComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(180, 40);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.3f, 0.5f, 0.7f);

            backButton = btnObj.AddComponent<Button>();
            backButton.targetGraphic = btnBg;
            backButton.onClick.AddListener(ReturnToPlayerBoard);

            LayoutElement btnLE = btnObj.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 180;
            btnLE.preferredHeight = 40;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            Text btnText = textObj.AddComponent<Text>();
            btnText.text = "← BACK TO MY BOARD";
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 14;
            btnText.fontStyle = FontStyle.Bold;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;

            backButtonPanel.gameObject.SetActive(false);
        }

        public void RefreshOpponentButtons()
        {
            if (!isInitialized) Initialize();

            foreach (var btn in opponentButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            opponentButtons.Clear();

            if (IsServerMultiplayer)
            {
                RefreshOpponentButtonsMultiplayer();
                return;
            }

            var state = GameState.Instance;
            if (state == null) return;

            if (state.currentGameMode == GameMode.Multiplayer)
            {
                var nm = NetworkManager.Instance;
                if (nm != null && nm.IsInGame)
                {
                    CreateHumanOpponentButton(nm);
                }
            }

            if (OpponentManager.Instance != null)
            {
                var opponents = OpponentManager.Instance.opponents;
                var opponentBoards = Game3DSetup.Instance?.GetOpponentBoards();

                if (opponentBoards != null)
                {
                    int boardOffset = state.currentGameMode == GameMode.Multiplayer ? 1 : 0;

                    for (int i = 0; i < opponents.Count; i++)
                    {
                        int boardIndex = i + boardOffset;
                        if (boardIndex < opponentBoards.Count && !opponents[i].isEliminated)
                        {
                            CreateOpponentButton(opponents[i], opponentBoards[boardIndex]);
                        }
                    }
                }
            }
        }

        private void RefreshOpponentButtonsMultiplayer()
        {
            if (serverState == null) return;

            foreach (var opponent in serverState.otherPlayers)
            {
                if (opponent == null || opponent.isEliminated) continue;

                // Use opponent's server-assigned board index to get the correct board
                HexBoard3D board = Game3DSetup.Instance?.GetBoardByIndex(opponent.boardIndex);
                if (board != null)
                {
                    serverOpponentBoards[opponent.clientId] = board;
                }

                CreateServerOpponentButton(opponent, board);
            }
        }

        private void CreateServerOpponentButton(ServerPlayerData opponent, HexBoard3D board)
        {
            GameObject btnObj = new GameObject($"Btn_{opponent.name}");
            btnObj.transform.SetParent(opponentButtonsPanel, false);

            Image btnBg = btnObj.AddComponent<Image>();
            if (opponent.isEliminated)
            {
                btnBg.color = new Color(0.3f, 0.2f, 0.2f, 0.7f);
            }
            else
            {
                btnBg.color = new Color(0.3f, 0.5f, 0.3f);
            }

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.interactable = !opponent.isEliminated;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 100;
            le.preferredHeight = 50;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(5, 5);
            textRT.offsetMax = new Vector2(-5, -5);

            Text btnText = textObj.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 12;
            btnText.alignment = TextAnchor.MiddleCenter;

            if (opponent.isEliminated)
            {
                btnText.text = $"{opponent.name}\n<color=#888>OUT</color>";
                btnText.color = new Color(0.5f, 0.5f, 0.5f);
            }
            else
            {
                bool isCombat = serverState.phase == "combat";
                if (isCombat)
                {
                    btnText.text = $"{opponent.name}\n<color=#ff8>FIGHTING</color>";
                }
                else
                {
                    btnText.text = $"{opponent.name}\n<color=#8f8>HP:{opponent.health}</color>";
                }
                btnText.color = Color.white;
            }

            var capturedOpponent = opponent;
            var capturedBoard = board;
            btn.onClick.AddListener(() => {
                AudioManager.Instance?.PlayUIClick();
                ViewServerOpponentBoard(capturedOpponent, capturedBoard);
            });

            opponentButtons.Add(btn);
        }

        /// <summary>
        /// View a server opponent's board.
        /// Units are rendered by OpponentBoardVisualizer via BoardVisualRegistry - scouting just moves the camera.
        /// </summary>
        private void ViewServerOpponentBoard(ServerPlayerData opponent, HexBoard3D board)
        {
            if (opponent == null) return;
            if (opponent.isEliminated) return;

            if (board == null)
            {
                // Try to get board by opponent's server-assigned board index
                board = Game3DSetup.Instance?.GetBoardByIndex(opponent.boardIndex);

                // Fallback to stored mapping
                if (board == null && serverOpponentBoards.TryGetValue(opponent.clientId, out var storedBoard))
                {
                    board = storedBoard;
                }

                // Last resort fallback
                if (board == null)
                {
                    board = Game3DSetup.Instance?.GetPlayerBoard() ?? HexBoard3D.Instance;
                }
            }

            if (board == null)
            {
                Debug.LogWarning("[ScoutingUI] No board available for scouting");
                return;
            }

            // If already viewing a different opponent, stop any combat visualization
            if (isViewingOpponent && viewedOpponentId != opponent.clientId)
            {
                StopScoutCombatVisualization();
                ClearBoardVisuals(currentViewedBoard);
            }

            isViewingOpponent = true;
            currentViewedBoard = board;
            viewedOpponentId = opponent.clientId;

            bool isCombat = serverState?.phase == "combat";

            if (isCombat)
            {
                viewingLabel.text = $"Watching: {opponent.name}'s fight";
                StartScoutCombatVisualization(opponent, board);
            }
            else
            {
                viewingLabel.text = $"Scouting: {opponent.name}";
                // Units are rendered by OpponentBoardVisualizer via registry - just switch camera
            }

            backButtonPanel.gameObject.SetActive(true);

            if (IsometricCameraSetup.Instance != null)
            {
                IsometricCameraSetup.Instance.FocusOnBoard(board);
            }
        }

        /// <summary>
        /// Start combat visualization for a scouted opponent using the unified CombatVisualizationManager
        /// </summary>
        private void StartScoutCombatVisualization(ServerPlayerData opponent, HexBoard3D board)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || nm.AllCombatEvents == null)
            {
                Debug.LogWarning("[ScoutingUI] Cannot start scout combat - no NetworkManager or AllCombatEvents");
                return;
            }

            var mainVisualizer = ServerCombatVisualizer.Instance;
            if (mainVisualizer == null || !mainVisualizer.isPlaying)
            {
                UpdateBoardWithServerData(board, opponent);
                return;
            }

            // Get the combat events for this opponent
            AllCombatEventsEntry combatEntry = null;
            if (!nm.AllCombatEvents.TryGetValue(opponent.clientId, out combatEntry))
            {
                Debug.LogWarning($"[ScoutingUI] No combat events found for {opponent.name}");
                UpdateBoardWithServerData(board, opponent);
                return;
            }

            if (combatEntry.events == null || combatEntry.events.Count == 0)
            {
                Debug.LogWarning($"[ScoutingUI] Combat events list is empty for {opponent.name}");
                UpdateBoardWithServerData(board, opponent);
                return;
            }

            // Check if the scouted opponent is fighting the local player (same combat, different perspective)
            string localPlayerId = serverState?.localPlayerId;
            bool isSameCombat = (combatEntry.hostPlayerId == localPlayerId || combatEntry.awayPlayerId == localPlayerId);

            if (isSameCombat)
            {
                // We're in the same combat - just switch camera to show opponent's perspective
                isViewingCombat = true;

                // Get the combat board
                HexBoard3D combatBoard = null;
                string hostPlayerId = serverState?.currentHostPlayerId;
                if (!string.IsNullOrEmpty(hostPlayerId))
                {
                    int hostBoardIndex = serverState.GetPlayerBoardIndex(hostPlayerId);
                    if (hostBoardIndex >= 0 && Game3DSetup.Instance != null)
                    {
                        combatBoard = Game3DSetup.Instance.GetBoardByIndex(hostBoardIndex);
                    }
                }
                if (combatBoard == null)
                {
                    combatBoard = Game3DSetup.Instance?.GetPlayerBoard() ?? HexBoard3D.Instance;
                }

                // Switch camera to opponent's perspective
                bool opponentIsHost = combatEntry.hostPlayerId == opponent.clientId;
                bool viewFromOpposite = !opponentIsHost;

                if (IsometricCameraSetup.Instance != null && combatBoard != null)
                {
                    IsometricCameraSetup.Instance.FocusOnBoard(combatBoard, viewFromOpposite);
                }

                return;
            }

            // Different combat - use the unified CombatVisualizationManager
            bool opponentIsHostInFight = combatEntry.hostPlayerId == opponent.clientId;
            string opponentTeam = opponentIsHostInFight ? "player1" : "player2";

            // Get current tick from main combat to sync playback
            int currentTick = 0;
            if (mainVisualizer.PlayerCombat != null && mainVisualizer.isPlaying)
            {
                currentTick = mainVisualizer.currentTick;
            }

            isViewingCombat = true;
            scoutCombatPlayback = mainVisualizer.PlayCombat(board, combatEntry.events, opponentTeam, opponentIsHostInFight, currentTick);
        }

        /// <summary>
        /// Stop scout combat visualization
        /// </summary>
        private void StopScoutCombatVisualization()
        {
            if (scoutCombatPlayback != null && currentViewedBoard != null)
            {
                var mainVisualizer = ServerCombatVisualizer.Instance;
                if (mainVisualizer != null)
                {
                    mainVisualizer.StopCombatOnBoard(currentViewedBoard);
                }
                scoutCombatPlayback = null;
            }
            isViewingCombat = false;
        }

        private void UpdateBoardWithServerData(HexBoard3D board, ServerPlayerData opponent)
        {
            // Units are now rendered by OpponentBoardVisualizer via BoardVisualRegistry
            // This method kept for combat mode compatibility but no longer creates visuals
            if (board == null || opponent == null) return;

            ClearBoardVisuals(board);
            // Unit rendering handled by OpponentBoardVisualizer
        }

        // SyncBoardWithServerData, SyncBenchUnitsOnly, CreateScoutUnitVisualTracked removed
        // Unit visuals are now handled by OpponentBoardVisualizer via BoardVisualRegistry

        private Dictionary<HexBoard3D, List<GameObject>> scoutVisuals = new Dictionary<HexBoard3D, List<GameObject>>();

        private void ClearBoardVisuals(HexBoard3D board)
        {
            if (board == null) return;

            if (scoutVisuals.TryGetValue(board, out List<GameObject> visuals))
            {
                foreach (var obj in visuals)
                {
                    if (obj != null) Destroy(obj);
                }
                visuals.Clear();
            }
        }

        private void CreateHumanOpponentButton(NetworkManager nm)
        {
            var opponent = nm.GetOpponent();
            if (opponent == null) return;

            GameObject btnObj = new GameObject($"Btn_{opponent.name}");
            btnObj.transform.SetParent(opponentButtonsPanel, false);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.3f, 0.5f, 0.3f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 100;
            le.preferredHeight = 50;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(5, 5);
            textRT.offsetMax = new Vector2(-5, -5);

            Text btnText = textObj.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 12;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;

            int health = nm.opponentHealth > 0 ? nm.opponentHealth : opponent.health;
            string displayName = !string.IsNullOrEmpty(nm.opponentName) ? nm.opponentName : opponent.name;
            btnText.text = $"[P] {displayName}\n<color=#8f8>HP:{health}</color>";

            btn.onClick.AddListener(() => {
                AudioManager.Instance?.PlayUIClick();
                ViewHumanOpponentBoard(nm);
            });

            opponentButtons.Add(btn);
        }

        private void ViewHumanOpponentBoard(NetworkManager nm)
        {
            if (nm == null || nm.opponentBoardData == null)
            {
                return;
            }

            var opponentBoards = Game3DSetup.Instance?.GetOpponentBoards();
            if (opponentBoards == null || opponentBoards.Count == 0) return;

            var board = opponentBoards[0];
            if (board == null) return;

            isViewingOpponent = true;
            currentViewedBoard = board;

            string displayName = !string.IsNullOrEmpty(nm.opponentName) ? nm.opponentName : "Opponent";
            viewingLabel.text = $"Scouting: {displayName}";

            UpdateBoardWithNetworkData(board, nm.opponentBoardData);

            backButtonPanel.gameObject.SetActive(true);

            if (IsometricCameraSetup.Instance != null)
            {
                IsometricCameraSetup.Instance.FocusOnBoard(board);
            }
        }

        private void UpdateBoardWithNetworkData(HexBoard3D board, BoardData data)
        {
            if (board == null || data == null) return;

            var nm = NetworkManager.Instance;
            if (nm == null) return;

            var unitBoard = nm.DeserializeBoard(data);
        }

        private void CreateOpponentButton(OpponentData opponent, HexBoard3D board)
        {
            GameObject btnObj = new GameObject($"Btn_{opponent.name}");
            btnObj.transform.SetParent(opponentButtonsPanel, false);

            Image btnBg = btnObj.AddComponent<Image>();
            if (opponent.isEliminated)
            {
                btnBg.color = new Color(0.3f, 0.2f, 0.2f, 0.7f);
            }
            else
            {
                btnBg.color = new Color(0.25f, 0.4f, 0.55f);
            }

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.interactable = !opponent.isEliminated;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 100;
            le.preferredHeight = 50;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(5, 5);
            textRT.offsetMax = new Vector2(-5, -5);

            Text btnText = textObj.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 12;
            btnText.alignment = TextAnchor.MiddleCenter;

            if (opponent.isEliminated)
            {
                btnText.text = $"{opponent.name}\n<color=#888>OUT</color>";
                btnText.color = new Color(0.5f, 0.5f, 0.5f);
            }
            else
            {
                var state = GameState.Instance;
                bool isDuringCombat = state != null && state.round.phase == GamePhase.Combat;

                if (isDuringCombat)
                {
                    bool isAway = opponent.boardVisualizer != null && opponent.boardVisualizer.IsAway;
                    if (isAway)
                    {
                        btnText.text = $"{opponent.name}\n<color=#ff8>AWAY</color>";
                    }
                    else
                    {
                        btnText.text = $"{opponent.name}\n<color=#ff8>FIGHTING</color>";
                    }
                    btnText.color = Color.white;
                }
                else
                {
                    btnText.text = $"{opponent.name}\n<color=#8f8>HP:{opponent.health}</color>";
                    btnText.color = Color.white;
                }
            }

            var capturedOpponent = opponent;
            var capturedBoard = board;
            btn.onClick.AddListener(() => {
                AudioManager.Instance?.PlayUIClick();
                ViewOpponentBoard(capturedOpponent, capturedBoard);
            });

            opponentButtons.Add(btn);
        }

        public void ViewOpponentBoard(OpponentData opponent, HexBoard3D board)
        {
            if (opponent == null || board == null) return;
            if (opponent.isEliminated) return;

            isViewingOpponent = true;
            currentViewedBoard = board;

            var state = GameState.Instance;
            bool isDuringCombat = state != null && state.round.phase == GamePhase.Combat;

            bool isAway = opponent.boardVisualizer != null && opponent.boardVisualizer.IsAway;
            HexBoard3D targetBoard = board;
            bool useOppositeSide = false;

            if (isDuringCombat)
            {
                if (isAway && opponent.boardVisualizer.FightBoard != null)
                {
                    targetBoard = opponent.boardVisualizer.FightBoard;
                    useOppositeSide = true;
                    viewingLabel.text = $"Watching: {opponent.name}'s fight";
                }
                else
                {
                    viewingLabel.text = $"Watching: {opponent.name}'s fight";
                }
            }
            else
            {
                viewingLabel.text = $"Scouting: {opponent.name}";
            }

            backButtonPanel.gameObject.SetActive(true);

            if (IsometricCameraSetup.Instance != null)
            {
                if (useOppositeSide)
                {
                    IsometricCameraSetup.Instance.FocusOnBoardFromOppositeSide(targetBoard);
                    // Set bench rendering to reversed for opposite-side viewing
                    if (targetBoard.Registry != null)
                    {
                        targetBoard.Registry.ViewFromOppositeSide = true;
                    }
                }
                else
                {
                    IsometricCameraSetup.Instance.FocusOnBoard(targetBoard);
                    // Reset bench rendering to normal
                    if (targetBoard.Registry != null)
                    {
                        targetBoard.Registry.ViewFromOppositeSide = false;
                    }
                }
            }
        }

        public void ReturnToPlayerBoard()
        {
            // Reset opposite-side viewing flag on the board we were viewing
            if (currentViewedBoard != null && currentViewedBoard.Registry != null)
            {
                currentViewedBoard.Registry.ViewFromOppositeSide = false;
            }

            isViewingOpponent = false;
            currentViewedBoard = null;
            viewedOpponentId = null;

            backButtonPanel.gameObject.SetActive(false);

            StopScoutCombatVisualization();

            if (IsServerMultiplayer)
            {
                // Clear any legacy scout visuals
                foreach (var kvp in scoutVisuals)
                {
                    foreach (var obj in kvp.Value)
                    {
                        if (obj != null) Destroy(obj);
                    }
                }
                scoutVisuals.Clear();
                // Unit visuals handled by OpponentBoardVisualizer via registry
            }

            if (IsometricCameraSetup.Instance != null)
            {
                // During combat, if we're the away player, return to the combat board (not our home board)
                var combatVisualizer = ServerCombatVisualizer.Instance;
                bool isInAwayCombat = combatVisualizer != null && combatVisualizer.isPlaying && serverState?.phase == "combat";

                if (isInAwayCombat)
                {
                    // Check if we're the away player (not the host)
                    string localPlayerId = serverState?.localPlayerId;
                    string hostPlayerId = serverState?.currentHostPlayerId;
                    bool isAwayPlayer = !string.IsNullOrEmpty(hostPlayerId) && localPlayerId != hostPlayerId;

                    if (isAwayPlayer)
                    {
                        // Return to the host's board where combat is happening
                        int hostBoardIndex = serverState.GetPlayerBoardIndex(hostPlayerId);
                        HexBoard3D combatBoard = null;
                        if (hostBoardIndex >= 0 && Game3DSetup.Instance != null)
                        {
                            combatBoard = Game3DSetup.Instance.GetBoardByIndex(hostBoardIndex);
                        }

                        if (combatBoard != null)
                        {
                            // View from away player's perspective (opposite side)
                            IsometricCameraSetup.Instance.FocusOnBoard(combatBoard, true);
                            // Ensure bench is rendered reversed for opposite-side viewing
                            if (combatBoard.Registry != null)
                            {
                                combatBoard.Registry.ViewFromOppositeSide = true;
                            }
                            AudioManager.Instance?.PlayUIClick();
                            return;
                        }
                    }
                }

                // Default: return to player's home board
                IsometricCameraSetup.Instance.FocusOnPlayerBoard();
            }

            AudioManager.Instance?.PlayUIClick();
        }

        private void UpdateVisibility()
        {
            if (scoutingCanvas == null) return;

            if (IsServerMultiplayer)
            {
                UpdateVisibilityMultiplayer();
                return;
            }

            var state = GameState.Instance;
            if (state == null)
            {
                scoutingCanvas.gameObject.SetActive(false);
                return;
            }

            if (state.currentGameMode != GameMode.PvP && state.currentGameMode != GameMode.Multiplayer)
            {
                scoutingCanvas.gameObject.SetActive(false);
                return;
            }

            if (!Game3DSetup.Instance?.IsMultiBoardActive ?? true)
            {
                scoutingCanvas.gameObject.SetActive(false);
                return;
            }

            bool shouldShow = false;
            var phase = state.round.phase;

            if (showDuringPlanning && phase == GamePhase.Planning)
            {
                shouldShow = true;
            }
            else if (showDuringCombat && phase == GamePhase.Combat)
            {
                shouldShow = true;
            }

            if (shouldShow && (opponentButtons.Count == 0 || phase != lastPhase))
            {
                RefreshOpponentButtons();
                lastPhase = phase;
            }

            scoutingCanvas.gameObject.SetActive(shouldShow);

            if (isViewingOpponent && phase == GamePhase.Results)
            {
                ReturnToPlayerBoard();
            }
        }

        private void UpdateVisibilityMultiplayer()
        {
            if (serverState == null)
            {
                scoutingCanvas.gameObject.SetActive(false);
                return;
            }

            bool hasOtherPlayers = serverState.otherPlayers != null && serverState.otherPlayers.Count > 0;

            string phase = serverState.phase;
            bool shouldShow = false;

            if (showDuringPlanning && phase == "planning")
            {
                shouldShow = hasOtherPlayers;
            }
            else if (showDuringCombat && phase == "combat")
            {
                shouldShow = hasOtherPlayers;
            }

            if (shouldShow && (opponentButtons.Count == 0 || phase != lastMultiplayerPhase))
            {
                RefreshOpponentButtons();
                lastMultiplayerPhase = phase;
            }

            scoutingCanvas.gameObject.SetActive(shouldShow);

            if (isViewingOpponent && phase == "results")
            {
                ReturnToPlayerBoard();
                foreach (var kvp in scoutVisuals)
                {
                    foreach (var obj in kvp.Value)
                    {
                        if (obj != null) Destroy(obj);
                    }
                }
                scoutVisuals.Clear();
            }
        }

        public bool IsViewingOpponent => isViewingOpponent;

        public HexBoard3D CurrentViewedBoard => currentViewedBoard;

        public void Show()
        {
            if (scoutingCanvas != null)
            {
                scoutingCanvas.gameObject.SetActive(true);
                RefreshOpponentButtons();
            }
        }

        public void Hide()
        {
            if (scoutingCanvas != null)
            {
                scoutingCanvas.gameObject.SetActive(false);
            }

            if (isViewingOpponent)
            {
                ReturnToPlayerBoard();
            }
        }

        public void Toggle()
        {
            if (scoutingCanvas != null && scoutingCanvas.gameObject.activeSelf)
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
