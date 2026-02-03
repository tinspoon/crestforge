using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Systems;
using Crestforge.Networking;

namespace Crestforge.UI
{
    /// <summary>
    /// UI showing round progression and matchups in PvP mode
    /// </summary>
    public class RoundProgressionUI : MonoBehaviour
    {
        public static RoundProgressionUI Instance { get; private set; }

        [Header("References")]
        public Canvas progressCanvas;
        public RectTransform mainPanel;

        [Header("UI Elements")]
        public RectTransform timelineContainer;
        public RectTransform healthBarsContainer;
        public Text currentMatchupText;
        public Button closeButton;

        // Runtime
        private List<Image> roundIndicators = new List<Image>();
        private List<RectTransform> healthBars = new List<RectTransform>();
        private List<Text> healthTexts = new List<Text>();
        private List<Text> nameTexts = new List<Text>();
        private bool isInitialized = false;

        // Multiplayer support
        private bool IsMultiplayer => ServerGameState.Instance != null && ServerGameState.Instance.IsInGame;
        private ServerGameState serverState => ServerGameState.Instance;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            Initialize();
            Hide();
        }

        private void Update()
        {
            if (progressCanvas != null && progressCanvas.gameObject.activeSelf)
            {
                UpdateDisplay();
            }
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
            if (progressCanvas == null)
            {
                GameObject canvasObj = new GameObject("ProgressCanvas");
                canvasObj.transform.SetParent(transform);
                progressCanvas = canvasObj.AddComponent<Canvas>();
                progressCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                progressCanvas.sortingOrder = 45;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Main panel - right side panel
            GameObject panelObj = CreatePanel("MainPanel", progressCanvas.transform);
            mainPanel = panelObj.GetComponent<RectTransform>();
            mainPanel.anchorMin = new Vector2(0.6f, 0.15f);
            mainPanel.anchorMax = new Vector2(0.98f, 0.85f);
            mainPanel.offsetMin = Vector2.zero;
            mainPanel.offsetMax = Vector2.zero;
            panelObj.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            Outline outline = panelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.5f);
            outline.effectDistance = new Vector2(2, 2);

            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.padding = new RectOffset(15, 15, 15, 15);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Header
            Text header = CreateText("MATCH PROGRESS", mainPanel, 300);
            header.fontSize = 22;
            header.fontStyle = FontStyle.Bold;
            header.alignment = TextAnchor.MiddleCenter;
            header.color = new Color(0.9f, 0.85f, 0.7f);
            header.GetComponent<LayoutElement>().preferredHeight = 30;

            // Current matchup text
            currentMatchupText = CreateText("Round 1: PvE Intro", mainPanel, 300);
            currentMatchupText.fontSize = 16;
            currentMatchupText.alignment = TextAnchor.MiddleCenter;
            currentMatchupText.color = new Color(0.8f, 0.9f, 1f);
            currentMatchupText.GetComponent<LayoutElement>().preferredHeight = 25;

            // Round timeline section header
            Text timelineHeader = CreateText("ROUNDS", mainPanel, 200);
            timelineHeader.fontSize = 14;
            timelineHeader.fontStyle = FontStyle.Bold;
            timelineHeader.alignment = TextAnchor.MiddleCenter;
            timelineHeader.color = new Color(0.6f, 0.6f, 0.7f);
            timelineHeader.GetComponent<LayoutElement>().preferredHeight = 20;

            // Timeline container
            GameObject timelineObj = new GameObject("Timeline");
            timelineObj.transform.SetParent(mainPanel);
            timelineContainer = timelineObj.AddComponent<RectTransform>();
            LayoutElement timelineLE = timelineObj.AddComponent<LayoutElement>();
            timelineLE.preferredHeight = 80;

            // Create grid for round indicators
            GridLayoutGroup glg = timelineObj.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(38, 38);
            glg.spacing = new Vector2(4, 4);
            glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 7;

            // Create round indicators (14 rounds)
            for (int i = 1; i <= GameConstants.Rounds.TOTAL_ROUNDS; i++)
            {
                CreateRoundIndicator(i);
            }

            // Health bars section header
            Text healthHeader = CreateText("STANDINGS", mainPanel, 200);
            healthHeader.fontSize = 14;
            healthHeader.fontStyle = FontStyle.Bold;
            healthHeader.alignment = TextAnchor.MiddleCenter;
            healthHeader.color = new Color(0.6f, 0.6f, 0.7f);
            healthHeader.GetComponent<LayoutElement>().preferredHeight = 20;

            // Health bars container
            GameObject healthObj = new GameObject("HealthBars");
            healthObj.transform.SetParent(mainPanel);
            healthBarsContainer = healthObj.AddComponent<RectTransform>();
            VerticalLayoutGroup healthVLG = healthObj.AddComponent<VerticalLayoutGroup>();
            healthVLG.spacing = 8;
            healthVLG.childAlignment = TextAnchor.UpperCenter;
            healthVLG.childControlWidth = true;
            healthVLG.childControlHeight = false;
            LayoutElement healthLE = healthObj.AddComponent<LayoutElement>();
            healthLE.preferredHeight = 200;

            // Player health bar
            CreateHealthBar("You", healthBarsContainer, new Color(0.3f, 0.6f, 0.9f));

            // Opponent health bars (will be populated when game starts)
            CreateHealthBar("Sir Bumble", healthBarsContainer, new Color(0.8f, 0.4f, 0.4f));
            CreateHealthBar("Mystic Mira", healthBarsContainer, new Color(0.6f, 0.4f, 0.8f));
            CreateHealthBar("Rogue Rex", healthBarsContainer, new Color(0.4f, 0.7f, 0.4f));

            // Close button
            GameObject closeBtnObj = CreatePanel("CloseButton", mainPanel);
            closeButton = closeBtnObj.AddComponent<Button>();
            Image closeBg = closeBtnObj.GetComponent<Image>();
            closeBg.color = new Color(0.35f, 0.35f, 0.4f);
            closeButton.targetGraphic = closeBg;
            closeButton.onClick.AddListener(Hide);
            LayoutElement closeLE = closeBtnObj.AddComponent<LayoutElement>();
            closeLE.preferredWidth = 120;
            closeLE.preferredHeight = 35;

            Text closeText = CreateText("CLOSE", closeBtnObj.transform, 0);
            closeText.fontSize = 14;
            closeText.fontStyle = FontStyle.Bold;
            closeText.alignment = TextAnchor.MiddleCenter;
            RectTransform closeTextRT = closeText.GetComponent<RectTransform>();
            closeTextRT.anchorMin = Vector2.zero;
            closeTextRT.anchorMax = Vector2.one;
            closeTextRT.offsetMin = Vector2.zero;
            closeTextRT.offsetMax = Vector2.zero;
        }

        private void CreateRoundIndicator(int roundNumber)
        {
            GameObject indicatorObj = CreatePanel($"Round_{roundNumber}", timelineContainer);
            Image indicator = indicatorObj.GetComponent<Image>();

            // Get round type and set color
            RoundType roundType = GetRoundType(roundNumber);
            indicator.color = GetRoundTypeColor(roundType, false);

            roundIndicators.Add(indicator);

            // Round number text
            Text roundText = CreateText(roundNumber.ToString(), indicatorObj.transform, 0);
            roundText.fontSize = 14;
            roundText.fontStyle = FontStyle.Bold;
            roundText.alignment = TextAnchor.MiddleCenter;
            roundText.color = Color.white;
            RectTransform textRT = roundText.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            // Add outline for current round highlighting
            Outline outlineComp = indicatorObj.AddComponent<Outline>();
            outlineComp.effectColor = Color.clear;
            outlineComp.effectDistance = new Vector2(2, 2);
        }

        private void CreateHealthBar(string name, Transform parent, Color barColor)
        {
            GameObject rowObj = new GameObject(name + "_HealthBar");
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRT = rowObj.AddComponent<RectTransform>();
            LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 40;

            // Name label
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(rowObj.transform, false);
            RectTransform nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0);
            nameRT.anchorMax = new Vector2(0.35f, 1);
            nameRT.offsetMin = Vector2.zero;
            nameRT.offsetMax = Vector2.zero;
            Text nameText = nameObj.AddComponent<Text>();
            nameText.text = name;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 14;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;

            // Health bar background
            GameObject barBgObj = CreatePanel("BarBg", rowObj.transform);
            RectTransform barBgRT = barBgObj.GetComponent<RectTransform>();
            barBgRT.anchorMin = new Vector2(0.37f, 0.2f);
            barBgRT.anchorMax = new Vector2(0.85f, 0.8f);
            barBgRT.offsetMin = Vector2.zero;
            barBgRT.offsetMax = Vector2.zero;
            barBgObj.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

            // Health bar fill
            GameObject barFillObj = CreatePanel("BarFill", barBgObj.transform);
            RectTransform barFillRT = barFillObj.GetComponent<RectTransform>();
            barFillRT.anchorMin = Vector2.zero;
            barFillRT.anchorMax = Vector2.one;
            barFillRT.offsetMin = Vector2.zero;
            barFillRT.offsetMax = Vector2.zero;
            barFillObj.GetComponent<Image>().color = barColor;
            healthBars.Add(barFillRT);

            // Health text
            GameObject healthTextObj = new GameObject("HealthText");
            healthTextObj.transform.SetParent(rowObj.transform, false);
            RectTransform healthTextRT = healthTextObj.AddComponent<RectTransform>();
            healthTextRT.anchorMin = new Vector2(0.87f, 0);
            healthTextRT.anchorMax = new Vector2(1f, 1);
            healthTextRT.offsetMin = Vector2.zero;
            healthTextRT.offsetMax = Vector2.zero;
            Text healthText = healthTextObj.AddComponent<Text>();
            healthText.text = "20";
            healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            healthText.fontSize = 14;
            healthText.color = Color.white;
            healthText.alignment = TextAnchor.MiddleRight;
            healthTexts.Add(healthText);
        }

        private RoundType GetRoundType(int round)
        {
            int roundIndex = round - 1;
            if (roundIndex >= 0 && roundIndex < GameConstants.Rounds.ROUND_TYPES.Length)
            {
                return GameConstants.Rounds.ROUND_TYPES[roundIndex];
            }
            return RoundType.PvP;
        }

        private Color GetRoundTypeColor(RoundType type, bool isCurrent)
        {
            Color baseColor = type switch
            {
                RoundType.PvP => new Color(0.7f, 0.3f, 0.3f),          // Red for PvP
                RoundType.PvEIntro => new Color(0.3f, 0.6f, 0.3f),     // Green for PvE Intro
                RoundType.PvELoot => new Color(0.3f, 0.5f, 0.3f),      // Darker green for PvE Loot
                RoundType.PvEBoss => new Color(0.6f, 0.4f, 0.2f),      // Orange for Boss
                RoundType.MadMerchant => new Color(0.8f, 0.7f, 0.2f),  // Yellow for Merchant
                RoundType.MajorCrest => new Color(0.5f, 0.3f, 0.7f),   // Purple for Crest
                _ => new Color(0.4f, 0.4f, 0.4f)
            };

            if (isCurrent)
            {
                return baseColor * 1.3f;
            }
            return baseColor;
        }

        public void Show()
        {
            if (!isInitialized) Initialize();
            UpdateDisplay();
            progressCanvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (progressCanvas != null)
            {
                progressCanvas.gameObject.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (progressCanvas != null && progressCanvas.gameObject.activeSelf)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        private void UpdateDisplay()
        {
            // Multiplayer mode
            if (IsMultiplayer)
            {
                UpdateDisplayMultiplayer();
                return;
            }

            var state = GameState.Instance;
            if (state == null) return;

            int currentRound = state.round.currentRound;

            // Update round indicators
            for (int i = 0; i < roundIndicators.Count; i++)
            {
                int roundNum = i + 1;
                RoundType roundType = GetRoundType(roundNum);
                bool isCurrent = roundNum == currentRound;
                bool isPast = roundNum < currentRound;

                Image indicator = roundIndicators[i];
                Outline outline = indicator.GetComponent<Outline>();

                if (isPast)
                {
                    // Darken past rounds
                    indicator.color = GetRoundTypeColor(roundType, false) * 0.5f;
                    outline.effectColor = Color.clear;
                }
                else if (isCurrent)
                {
                    indicator.color = GetRoundTypeColor(roundType, true);
                    outline.effectColor = Color.yellow;
                }
                else
                {
                    indicator.color = GetRoundTypeColor(roundType, false);
                    outline.effectColor = Color.clear;
                }
            }

            // Update current matchup text
            RoundType currentType = GetRoundType(currentRound);
            string matchupText = GetMatchupText(currentRound, currentType);
            currentMatchupText.text = matchupText;

            // Update health bars
            UpdateHealthBars();
        }

        private void UpdateDisplayMultiplayer()
        {
            if (serverState == null) return;

            int currentRound = serverState.round;

            // Update round indicators (in multiplayer, all rounds are PvP)
            for (int i = 0; i < roundIndicators.Count; i++)
            {
                int roundNum = i + 1;
                bool isCurrent = roundNum == currentRound;
                bool isPast = roundNum < currentRound;

                Image indicator = roundIndicators[i];
                Outline outline = indicator.GetComponent<Outline>();

                if (isPast)
                {
                    indicator.color = new Color(0.2f, 0.4f, 0.2f); // Green for completed
                    outline.effectColor = Color.clear;
                }
                else if (isCurrent)
                {
                    indicator.color = new Color(0.3f, 0.6f, 0.3f);
                    outline.effectColor = Color.yellow;
                }
                else
                {
                    indicator.color = new Color(0.3f, 0.3f, 0.4f);
                    outline.effectColor = Color.clear;
                }
            }

            // Update current matchup text
            string matchupText = GetMatchupTextMultiplayer(currentRound);
            currentMatchupText.text = matchupText;

            // Update health bars
            UpdateHealthBarsMultiplayer();
        }

        private string GetMatchupTextMultiplayer(int round)
        {
            string opponent = "";

            // Find current matchup from server state
            if (serverState.matchups != null)
            {
                foreach (var matchup in serverState.matchups)
                {
                    if (matchup.player1 == serverState.localPlayerId || matchup.player2 == serverState.localPlayerId)
                    {
                        string opponentId = matchup.player1 == serverState.localPlayerId ? matchup.player2 : matchup.player1;
                        var opponentData = serverState.GetOpponentData(opponentId);
                        if (opponentData != null)
                        {
                            opponent = $" vs {opponentData.name}";
                        }
                        break;
                    }
                }
            }

            return $"Round {round}: PvP{opponent}";
        }

        private void UpdateHealthBarsMultiplayer()
        {
            if (serverState == null) return;

            // Index 0: Local Player
            if (healthBars.Count > 0 && healthTexts.Count > 0)
            {
                float playerHealthPercent = (float)serverState.health / serverState.maxHealth;
                healthBars[0].anchorMax = new Vector2(Mathf.Clamp01(playerHealthPercent), 1);
                healthTexts[0].text = serverState.health.ToString();

                // Update name if we have name texts
                if (nameTexts.Count > 0 && nameTexts[0] != null)
                {
                    nameTexts[0].text = serverState.localPlayerName ?? "You";
                }
            }

            // Indices 1+: Other players from server
            int barIndex = 1;
            foreach (var opponent in serverState.otherPlayers)
            {
                if (barIndex >= healthBars.Count) break;

                float healthPercent = (float)opponent.health / opponent.maxHealth;
                healthBars[barIndex].anchorMax = new Vector2(Mathf.Clamp01(healthPercent), 1);
                healthTexts[barIndex].text = opponent.isEliminated ? "OUT" : opponent.health.ToString();

                // Update name
                if (barIndex < nameTexts.Count && nameTexts[barIndex] != null)
                {
                    nameTexts[barIndex].text = opponent.name;
                }

                // Dim eliminated opponents
                if (opponent.isEliminated)
                {
                    healthBars[barIndex].GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
                }
                else
                {
                    healthBars[barIndex].GetComponent<Image>().color = new Color(0.8f, 0.3f, 0.3f);
                }

                barIndex++;
            }
        }

        private string GetMatchupText(int round, RoundType type)
        {
            string typeStr = type switch
            {
                RoundType.PvP => "PvP",
                RoundType.PvEIntro => "PvE Intro",
                RoundType.PvELoot => "PvE Loot",
                RoundType.PvEBoss => "PvE Boss",
                RoundType.MadMerchant => "Mad Merchant",
                RoundType.MajorCrest => "Major Crest",
                _ => "Unknown"
            };

            string opponent = "";
            if (type == RoundType.PvP && OpponentManager.Instance != null)
            {
                var currentOpp = OpponentManager.Instance.currentOpponent;
                if (currentOpp != null)
                {
                    opponent = $" vs {currentOpp.name}";
                }
            }

            return $"Round {round}: {typeStr}{opponent}";
        }

        private void UpdateHealthBars()
        {
            var state = GameState.Instance;
            if (state == null) return;

            // Index 0: Player
            if (healthBars.Count > 0 && healthTexts.Count > 0)
            {
                float playerHealthPercent = (float)state.player.health / state.player.maxHealth;
                healthBars[0].anchorMax = new Vector2(Mathf.Clamp01(playerHealthPercent), 1);
                healthTexts[0].text = state.player.health.ToString();
            }

            // Indices 1-3: Opponents
            if (OpponentManager.Instance != null)
            {
                for (int i = 0; i < OpponentManager.Instance.opponents.Count && i + 1 < healthBars.Count; i++)
                {
                    var opponent = OpponentManager.Instance.opponents[i];
                    int barIndex = i + 1;

                    float healthPercent = (float)opponent.health / opponent.maxHealth;
                    healthBars[barIndex].anchorMax = new Vector2(Mathf.Clamp01(healthPercent), 1);
                    healthTexts[barIndex].text = opponent.isEliminated ? "OUT" : opponent.health.ToString();

                    // Dim eliminated opponents
                    if (opponent.isEliminated)
                    {
                        healthBars[barIndex].GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
                    }
                }
            }
        }

        // Helper methods
        private GameObject CreatePanel(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            obj.AddComponent<Image>();
            return obj;
        }

        private Text CreateText(string content, Transform parent, float width)
        {
            GameObject obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            LayoutElement le = obj.AddComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = width;
            Text text = obj.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }
    }
}
