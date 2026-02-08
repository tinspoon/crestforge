using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using Crestforge.Core;
using Crestforge.Systems;

namespace Crestforge.UI
{
    /// <summary>
    /// Main Menu / Home Screen UI
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        [Header("References")]
        public Canvas menuCanvas;

        [Header("Panels")]
        public RectTransform mainPanel;
        public RectTransform gameModePanel;
        public RectTransform settingsPanel;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Ensure TextStyleManager exists
            if (TextStyleManager.Instance == null)
            {
                GameObject styleManagerObj = new GameObject("TextStyleManager");
                styleManagerObj.AddComponent<TextStyleManager>();
            }

            CreateMenuUI();
            ShowMainMenu();
        }

        private void CreateMenuUI()
        {
            // Create Canvas
            if (menuCanvas == null)
            {
                GameObject canvasObj = new GameObject("MenuCanvas");
                canvasObj.transform.SetParent(transform);
                menuCanvas = canvasObj.AddComponent<Canvas>();
                menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                menuCanvas.sortingOrder = 100; // Above game UI
                
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
                
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Ensure EventSystem exists
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            CreateMainPanel();
            CreateGameModePanel();
            CreateSettingsPanel();
        }

        private void CreateMainPanel()
        {
            // Main menu panel - full screen
            GameObject panelObj = CreatePanel("MainPanel", menuCanvas.transform);
            mainPanel = panelObj.GetComponent<RectTransform>();
            mainPanel.anchorMin = Vector2.zero;
            mainPanel.anchorMax = Vector2.one;
            mainPanel.offsetMin = Vector2.zero;
            mainPanel.offsetMax = Vector2.zero;

            // Load background image
            Image bgImage = panelObj.GetComponent<Image>();
            Sprite bgSprite = Resources.Load<Sprite>("UI/crestForgeHome_v1");
            if (bgSprite != null)
            {
                bgImage.sprite = bgSprite;
                bgImage.type = Image.Type.Simple;
                bgImage.preserveAspect = false;
                bgImage.color = Color.white;
            }
            else
            {
                bgImage.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            }

            // Vertical layout for content
            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20;
            vlg.padding = new RectOffset(40, 40, 80, 40);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;

            // Spacer before title (pushes it down)
            GameObject spacerTop = new GameObject("SpacerTop");
            spacerTop.transform.SetParent(mainPanel);
            spacerTop.AddComponent<RectTransform>();
            spacerTop.AddComponent<LayoutElement>().preferredHeight = 80;

            // Title - Large and stylized with Titan One font
            TextMeshProUGUI title = CreateText("CrestForge", mainPanel, 900, null);
            title.fontSize = 140;
            title.alignment = TextAlignmentOptions.Center;
            title.GetComponent<LayoutElement>().preferredHeight = 200;

            // Load Titan One font
            TMP_FontAsset titanFont = Resources.Load<TMP_FontAsset>("TextStyles/TitanOne-Regular SDF");
            if (titanFont == null)
            {
                // Try alternative paths
                titanFont = Resources.Load<TMP_FontAsset>("Fonts/TitanOne-Regular SDF");
            }
            if (titanFont == null)
            {
                // Try loading from TextStylePresets
                var presets = Resources.Load<TextStylePresets>("TextStylePresets");
                if (presets != null && presets.primaryFont != null)
                {
                    titanFont = presets.primaryFont;
                }
            }
            if (titanFont != null)
            {
                title.font = titanFont;
            }

            // Apply gradient color (gold to orange)
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(
                new Color(1f, 0.95f, 0.6f),      // Top left - bright gold
                new Color(1f, 0.85f, 0.4f),      // Top right - gold
                new Color(1f, 0.6f, 0.2f),       // Bottom left - orange
                new Color(0.9f, 0.5f, 0.1f)      // Bottom right - deep orange
            );

            // Apply outline via material
            Material titleMat = title.fontMaterial;
            titleMat.EnableKeyword("OUTLINE_ON");
            titleMat.SetColor("_OutlineColor", new Color(0.15f, 0.08f, 0f, 1f));
            titleMat.SetFloat("_OutlineWidth", 0.2f);

            // Shadow using underlay - crisp drop shadow
            titleMat.EnableKeyword("UNDERLAY_ON");
            titleMat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.7f));
            titleMat.SetFloat("_UnderlayOffsetX", 1f);
            titleMat.SetFloat("_UnderlayOffsetY", -1f);
            titleMat.SetFloat("_UnderlayDilate", 0f);
            titleMat.SetFloat("_UnderlaySoftness", 0f);

            // Spacer
            GameObject spacer1 = new GameObject("Spacer");
            spacer1.transform.SetParent(mainPanel);
            spacer1.AddComponent<RectTransform>();
            spacer1.AddComponent<LayoutElement>().preferredHeight = 80;

            // Play button (large, prominent with glow effect)
            CreateStylizedButton("PLAY", mainPanel, 400, 100, OnPlayClicked,
                new Color(0.15f, 0.55f, 0.25f), new Color(0.2f, 0.7f, 0.35f), 42);

            // Spacer
            GameObject spacer2 = new GameObject("Spacer2");
            spacer2.transform.SetParent(mainPanel);
            spacer2.AddComponent<RectTransform>();
            spacer2.AddComponent<LayoutElement>().preferredHeight = 25;

            // Settings button
            CreateStylizedButton("SETTINGS", mainPanel, 340, 80, OnSettingsClicked,
                new Color(0.25f, 0.28f, 0.35f), new Color(0.35f, 0.4f, 0.5f), 32);

            // Spacer
            GameObject spacer3 = new GameObject("Spacer3");
            spacer3.transform.SetParent(mainPanel);
            spacer3.AddComponent<RectTransform>();
            spacer3.AddComponent<LayoutElement>().preferredHeight = 20;

            // Quit button
            CreateStylizedButton("QUIT", mainPanel, 340, 80, OnQuitClicked,
                new Color(0.5f, 0.2f, 0.2f), new Color(0.65f, 0.3f, 0.3f), 32);

            // Flexible spacer
            GameObject spacerFlex = new GameObject("SpacerFlex");
            spacerFlex.transform.SetParent(mainPanel);
            spacerFlex.AddComponent<RectTransform>();
            spacerFlex.AddComponent<LayoutElement>().flexibleHeight = 1;
            
            // Version text
            TextMeshProUGUI version = CreateText("v0.1 Alpha", mainPanel, 200, "body");
            version.fontSize = 16;
            version.alignment = TextAlignmentOptions.Center;
            version.color = new Color(0.4f, 0.4f, 0.5f);
            version.GetComponent<LayoutElement>().preferredHeight = 30;
        }

        private void CreateGameModePanel()
        {
            // Game mode selection panel
            GameObject panelObj = CreatePanel("GameModePanel", menuCanvas.transform);
            gameModePanel = panelObj.GetComponent<RectTransform>();
            gameModePanel.anchorMin = Vector2.zero;
            gameModePanel.anchorMax = Vector2.one;
            gameModePanel.offsetMin = Vector2.zero;
            gameModePanel.offsetMax = Vector2.zero;
            panelObj.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 1f);

            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 25;
            vlg.padding = new RectOffset(40, 40, 60, 40);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;

            // Header
            TextMeshProUGUI header = CreateText("SELECT GAME MODE", gameModePanel, 400, "header");
            header.alignment = TextAlignmentOptions.Center;
            header.GetComponent<LayoutElement>().preferredHeight = 60;

            // Spacer
            GameObject spacer1 = new GameObject("Spacer");
            spacer1.transform.SetParent(gameModePanel);
            spacer1.AddComponent<RectTransform>();
            spacer1.AddComponent<LayoutElement>().preferredHeight = 40;

            // Multiplayer Mode
            CreateGameModeCard(
                "MULTIPLAYER",
                "Battle against real players online! " +
                "Create or join a room to fight head-to-head!",
                true,
                OnMultiplayerClicked
            );

            // Combat Test Mode
            CreateGameModeCard(
                "COMBAT TEST",
                "Configure and test combat scenarios. " +
                "Set up units, items, and crests for both teams and watch them fight!",
                true,
                OnCombatTestClicked
            );

            // Flexible spacer
            GameObject spacer2 = new GameObject("Spacer2");
            spacer2.transform.SetParent(gameModePanel);
            spacer2.AddComponent<RectTransform>();
            spacer2.AddComponent<LayoutElement>().flexibleHeight = 1;

            // Back button
            CreateStylizedButton("BACK", gameModePanel, 260, 65, OnBackToMainClicked,
                new Color(0.25f, 0.28f, 0.35f), new Color(0.4f, 0.45f, 0.55f), 28);

            gameModePanel.gameObject.SetActive(false);
        }

        private void CreateGameModeCard(string title, string description, bool available, System.Action onClick)
        {
            GameObject cardObj = CreatePanel("GameModeCard", gameModePanel);
            RectTransform cardRT = cardObj.GetComponent<RectTransform>();
            Image cardBg = cardObj.GetComponent<Image>();
            
            if (available)
            {
                cardBg.color = new Color(0.15f, 0.2f, 0.25f, 1f);
            }
            else
            {
                cardBg.color = new Color(0.12f, 0.12f, 0.15f, 0.7f);
            }

            LayoutElement cardLE = cardObj.AddComponent<LayoutElement>();
            cardLE.preferredWidth = 500;
            cardLE.preferredHeight = 140;

            VerticalLayoutGroup cardVLG = cardObj.AddComponent<VerticalLayoutGroup>();
            cardVLG.spacing = 8;
            cardVLG.padding = new RectOffset(20, 20, 15, 15);
            cardVLG.childAlignment = TextAnchor.UpperLeft;
            cardVLG.childControlWidth = true;
            cardVLG.childControlHeight = false;

            // Title row
            GameObject titleRow = new GameObject("TitleRow");
            titleRow.transform.SetParent(cardObj.transform);
            RectTransform titleRowRT = titleRow.AddComponent<RectTransform>();
            HorizontalLayoutGroup titleHLG = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleHLG.childAlignment = TextAnchor.MiddleLeft;
            titleHLG.childControlWidth = false;
            titleHLG.childControlHeight = true;
            titleRow.AddComponent<LayoutElement>().preferredHeight = 35;

            TextMeshProUGUI titleText = CreateText(title, titleRowRT, 300, "cardName");
            titleText.fontSize = 24;
            titleText.color = available ? Color.white : new Color(0.5f, 0.5f, 0.5f);

            if (!available)
            {
                TextMeshProUGUI comingSoon = CreateText("COMING SOON", titleRowRT, 150, "body");
                comingSoon.fontSize = 14;
                comingSoon.fontStyle = FontStyles.Italic;
                comingSoon.alignment = TextAlignmentOptions.MidlineRight;
                comingSoon.color = new Color(0.6f, 0.5f, 0.3f);
            }

            // Description
            TextMeshProUGUI descText = CreateText(description, cardObj.transform, 0, "body");
            descText.fontSize = 16;
            descText.color = available ? new Color(0.75f, 0.75f, 0.8f) : new Color(0.4f, 0.4f, 0.45f);
            descText.GetComponent<LayoutElement>().preferredHeight = 60;

            // Make clickable if available
            if (available && onClick != null)
            {
                Button btn = cardObj.AddComponent<Button>();
                btn.targetGraphic = cardBg;
                cardBg.raycastTarget = true;
                btn.onClick.AddListener(() => {
                    Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
                    onClick();
                });

                // Hover effect
                ColorBlock colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
                colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
                btn.colors = colors;
            }
        }

        private void CreateSettingsPanel()
        {
            // Settings panel
            GameObject panelObj = CreatePanel("SettingsPanel", menuCanvas.transform);
            settingsPanel = panelObj.GetComponent<RectTransform>();
            settingsPanel.anchorMin = Vector2.zero;
            settingsPanel.anchorMax = Vector2.one;
            settingsPanel.offsetMin = Vector2.zero;
            settingsPanel.offsetMax = Vector2.zero;
            panelObj.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 1f);

            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 25;
            vlg.padding = new RectOffset(40, 40, 60, 40);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;

            // Header
            TextMeshProUGUI settingsHeader = CreateText("SETTINGS", settingsPanel, 300, "header");
            settingsHeader.alignment = TextAlignmentOptions.Center;
            settingsHeader.GetComponent<LayoutElement>().preferredHeight = 60;

            // Spacer
            GameObject spacer1 = new GameObject("Spacer");
            spacer1.transform.SetParent(settingsPanel);
            spacer1.AddComponent<RectTransform>();
            spacer1.AddComponent<LayoutElement>().preferredHeight = 40;

            // Sound toggle
            CreateSettingsToggle("Sound Effects", settingsPanel, true);
            
            // Music toggle
            CreateSettingsToggle("Music", settingsPanel, true);

            // Combat Speed
            CreateSettingsOption("Combat Speed", settingsPanel, new string[] { "1x", "1.5x", "2x" }, 0);

            // Auto-battle toggle
            CreateSettingsToggle("Auto-Battle", settingsPanel, false);

            // Flexible spacer
            GameObject spacer2 = new GameObject("Spacer2");
            spacer2.transform.SetParent(settingsPanel);
            spacer2.AddComponent<RectTransform>();
            spacer2.AddComponent<LayoutElement>().flexibleHeight = 1;

            // Back button
            CreateStylizedButton("BACK", settingsPanel, 260, 65, OnBackToMainClicked,
                new Color(0.25f, 0.28f, 0.35f), new Color(0.4f, 0.45f, 0.55f), 28);

            settingsPanel.gameObject.SetActive(false);
        }

        private void CreateSettingsToggle(string label, Transform parent, bool defaultValue)
        {
            GameObject rowObj = CreatePanel("SettingsRow", parent);
            RectTransform rowRT = rowObj.GetComponent<RectTransform>();
            rowObj.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
            
            LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredWidth = 400;
            rowLE.preferredHeight = 60;

            HorizontalLayoutGroup hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.padding = new RectOffset(20, 20, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            TextMeshProUGUI labelText = CreateText(label, rowRT, 250, "body");
            labelText.fontSize = 20;
            labelText.color = Color.white;

            // Toggle button
            GameObject toggleObj = CreatePanel("Toggle", rowRT);
            RectTransform toggleRT = toggleObj.GetComponent<RectTransform>();
            toggleRT.sizeDelta = new Vector2(80, 36);
            Image toggleBg = toggleObj.GetComponent<Image>();
            toggleBg.color = defaultValue ? new Color(0.2f, 0.5f, 0.3f) : new Color(0.3f, 0.3f, 0.35f);

            LayoutElement toggleLE = toggleObj.AddComponent<LayoutElement>();
            toggleLE.preferredWidth = 80;
            toggleLE.preferredHeight = 36;

            TextMeshProUGUI toggleText = CreateText(defaultValue ? "ON" : "OFF", toggleObj.transform, 0, "buttonSmall");
            toggleText.fontSize = 16;
            toggleText.alignment = TextAlignmentOptions.Center;
            RectTransform toggleTextRT = toggleText.GetComponent<RectTransform>();
            toggleTextRT.anchorMin = Vector2.zero;
            toggleTextRT.anchorMax = Vector2.one;
            toggleTextRT.offsetMin = Vector2.zero;
            toggleTextRT.offsetMax = Vector2.zero;

            // Make toggle clickable
            Button btn = toggleObj.AddComponent<Button>();
            btn.targetGraphic = toggleBg;
            bool isOn = defaultValue;
            btn.onClick.AddListener(() => {
                Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
                isOn = !isOn;
                toggleBg.color = isOn ? new Color(0.2f, 0.5f, 0.3f) : new Color(0.3f, 0.3f, 0.35f);
                toggleText.text = isOn ? "ON" : "OFF";
            });
        }

        private void CreateSettingsOption(string label, Transform parent, string[] options, int defaultIndex)
        {
            GameObject rowObj = CreatePanel("SettingsRow", parent);
            RectTransform rowRT = rowObj.GetComponent<RectTransform>();
            rowObj.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
            
            LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredWidth = 400;
            rowLE.preferredHeight = 60;

            HorizontalLayoutGroup hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.padding = new RectOffset(20, 20, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            TextMeshProUGUI labelText = CreateText(label, rowRT, 160, "body");
            labelText.fontSize = 20;
            labelText.color = Color.white;

            // Store references for highlighting
            List<Image> optionBgs = new List<Image>();
            int currentIndex = defaultIndex;

            // Option buttons
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                GameObject optObj = CreatePanel($"Option_{i}", rowRT);
                Image optBg = optObj.GetComponent<Image>();
                optBg.color = (i == defaultIndex) ? new Color(0.3f, 0.5f, 0.6f) : new Color(0.25f, 0.25f, 0.3f);
                optionBgs.Add(optBg);

                LayoutElement optLE = optObj.AddComponent<LayoutElement>();
                optLE.preferredWidth = 55;
                optLE.preferredHeight = 36;

                TextMeshProUGUI optText = CreateText(options[i], optObj.transform, 0, "buttonSmall");
                optText.fontSize = 16;
                optText.alignment = TextAlignmentOptions.Center;
                RectTransform optTextRT = optText.GetComponent<RectTransform>();
                optTextRT.anchorMin = Vector2.zero;
                optTextRT.anchorMax = Vector2.one;
                optTextRT.offsetMin = Vector2.zero;
                optTextRT.offsetMax = Vector2.zero;

                Button btn = optObj.AddComponent<Button>();
                btn.targetGraphic = optBg;
                btn.onClick.AddListener(() => {
                    Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
                    currentIndex = index;
                    for (int j = 0; j < optionBgs.Count; j++)
                    {
                        optionBgs[j].color = (j == currentIndex) ?
                            new Color(0.3f, 0.5f, 0.6f) : new Color(0.25f, 0.25f, 0.3f);
                    }
                });
            }
        }

        // ========== Button Handlers ==========

        private void OnPlayClicked()
        {
            ShowGameModePanel();
        }

        private void OnSettingsClicked()
        {
            ShowSettingsPanel();
        }

        private void OnQuitClicked()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        private void OnBackToMainClicked()
        {
            ShowMainMenu();
        }

        private void OnMultiplayerClicked()
        {
            // Hide menu
            Hide();

            // Show lobby UI
            LobbyUI lobbyUI = LobbyUI.Instance;
            if (lobbyUI == null)
            {
                // Create LobbyUI if it doesn't exist
                GameObject lobbyObj = new GameObject("LobbyUI");
                lobbyUI = lobbyObj.AddComponent<LobbyUI>();
            }
            lobbyUI.Show();
        }

        private void OnCombatTestClicked()
        {
            // Hide menu
            Hide();

            // Show combat test UI
            CombatTestUI combatTestUI = CombatTestUI.Instance;
            if (combatTestUI == null)
            {
                // Create CombatTestUI if it doesn't exist
                GameObject testObj = new GameObject("CombatTestUI");
                combatTestUI = testObj.AddComponent<CombatTestUI>();
            }
            combatTestUI.Show();
        }

        // ========== Panel Management ==========

        public void ShowMainMenu()
        {
            mainPanel.gameObject.SetActive(true);
            gameModePanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
            menuCanvas.gameObject.SetActive(true);
        }

        private void ShowGameModePanel()
        {
            mainPanel.gameObject.SetActive(false);
            gameModePanel.gameObject.SetActive(true);
            settingsPanel.gameObject.SetActive(false);
        }

        private void ShowSettingsPanel()
        {
            mainPanel.gameObject.SetActive(false);
            gameModePanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
        }

        public void Show()
        {
            menuCanvas.gameObject.SetActive(true);
            ShowMainMenu();
        }

        public void Hide()
        {
            menuCanvas.gameObject.SetActive(false);
        }

        // ========== Helpers ==========

        private GameObject CreatePanel(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            obj.AddComponent<Image>();
            return obj;
        }

        private TextMeshProUGUI CreateText(string content, Transform parent, float width, string styleName = null)
        {
            GameObject obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            LayoutElement le = obj.AddComponent<LayoutElement>();
            if (width > 0)
            {
                le.preferredWidth = width;
            }
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            // Apply style if specified
            if (!string.IsNullOrEmpty(styleName))
            {
                TextStyleManager.ApplyStyle(text, styleName);
            }

            return text;
        }

        private Button CreateMenuButton(string label, Transform parent, float width, float height,
            System.Action onClick, Color bgColor, int fontSize)
        {
            GameObject btnObj = CreatePanel("Button", parent);
            RectTransform btnRT = btnObj.GetComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(width, height);

            Image btnBg = btnObj.GetComponent<Image>();
            btnBg.color = bgColor;
            btnBg.raycastTarget = true;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            // Button text with TMP
            string styleName = fontSize >= 28 ? "button" : "buttonSmall";
            TextMeshProUGUI btnText = CreateText(label, btnObj.transform, 0, styleName);
            btnText.fontSize = fontSize;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.raycastTarget = false;
            RectTransform textRT = btnText.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            if (onClick != null)
            {
                btn.onClick.AddListener(() => {
                    Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
                    onClick();
                });
            }

            // Hover colors
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            btn.colors = colors;

            return btn;
        }

        private Button CreateStylizedButton(string label, Transform parent, float width, float height,
            System.Action onClick, Color bgColor, Color highlightColor, int fontSize)
        {
            // Create button container with rounded appearance
            GameObject btnObj = CreatePanel("StylizedButton", parent);
            RectTransform btnRT = btnObj.GetComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(width, height);

            Image btnBg = btnObj.GetComponent<Image>();
            btnBg.color = bgColor;
            btnBg.raycastTarget = true;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            // Add outline/border effect using a child image
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(btnObj.transform, false);
            RectTransform borderRT = borderObj.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-3, -3);
            borderRT.offsetMax = new Vector2(3, 3);
            borderRT.SetAsFirstSibling();
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.6f);
            borderImg.raycastTarget = false;

            // Inner highlight gradient
            GameObject innerGlow = new GameObject("InnerGlow");
            innerGlow.transform.SetParent(btnObj.transform, false);
            RectTransform glowRT = innerGlow.AddComponent<RectTransform>();
            glowRT.anchorMin = new Vector2(0, 0.5f);
            glowRT.anchorMax = new Vector2(1, 1);
            glowRT.offsetMin = new Vector2(4, 0);
            glowRT.offsetMax = new Vector2(-4, -4);
            Image glowImg = innerGlow.AddComponent<Image>();
            glowImg.color = new Color(1f, 1f, 1f, 0.15f);
            glowImg.raycastTarget = false;

            // Button text with TMP styling
            TextMeshProUGUI btnText = CreateText(label, btnObj.transform, 0, null);
            btnText.fontSize = fontSize;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            // Load Titan One font for buttons
            TMP_FontAsset titanFont = Resources.Load<TMP_FontAsset>("TextStyles/TitanOne-Regular SDF");
            if (titanFont == null)
            {
                titanFont = Resources.Load<TMP_FontAsset>("Fonts/TitanOne-Regular SDF");
            }
            if (titanFont == null)
            {
                var presets = Resources.Load<TextStylePresets>("TextStylePresets");
                if (presets != null && presets.primaryFont != null)
                {
                    titanFont = presets.primaryFont;
                }
            }
            if (titanFont != null)
            {
                btnText.font = titanFont;
            }
            btnText.raycastTarget = false;

            // Add outline to text
            Material textMat = btnText.fontMaterial;
            textMat.EnableKeyword("OUTLINE_ON");
            textMat.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0.5f));
            textMat.SetFloat("_OutlineWidth", 0.1f);

            RectTransform textRT = btnText.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            if (onClick != null)
            {
                btn.onClick.AddListener(() => {
                    Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
                    onClick();
                });
            }

            // Enhanced hover colors
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.1f);
            btn.colors = colors;

            return btn;
        }
    }
}