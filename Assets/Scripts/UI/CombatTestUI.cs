using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using Crestforge.Networking;
using Crestforge.Systems;
using Crestforge.Data;
using Crestforge.Core;
using Crestforge.Visuals;
using Object = UnityEngine.Object;

namespace Crestforge.UI
{
    /// <summary>
    /// Data structure for a test unit configuration
    /// </summary>
    [Serializable]
    public class TestUnitConfig
    {
        public string unitId;
        public int starLevel = 1;
        public int boardX;
        public int boardY;
        public List<string> itemIds = new List<string>();
    }

    /// <summary>
    /// Data structure for a test team configuration
    /// </summary>
    [Serializable]
    public class TestTeamConfig
    {
        public List<TestUnitConfig> units = new List<TestUnitConfig>();
        public List<string> minorCrestIds = new List<string>();
        public string majorCrestId;
    }

    /// <summary>
    /// UI for combat testing - configure two teams and run combat simulations
    /// </summary>
    public class CombatTestUI : MonoBehaviour
    {
        public static CombatTestUI Instance { get; private set; }

        [Header("Settings")]
        public string defaultServerUrl = "ws://localhost:8080";

        // UI References
        private Canvas testCanvas;
        private GameObject configPanel;
        private GameObject combatPanel;
        private GameObject resultsPanel;

        // Team A (left side)
        private TestTeamConfig teamAConfig = new TestTeamConfig();
        private Dictionary<Vector2Int, TestUnitConfig> teamABoard = new Dictionary<Vector2Int, TestUnitConfig>();
        private List<Button> teamATileButtons = new List<Button>();
        private Dropdown[] teamAMinorCrestDropdowns = new Dropdown[3];
        private Dropdown teamAMajorCrestDropdown;

        // Team B (right side)
        private TestTeamConfig teamBConfig = new TestTeamConfig();
        private Dictionary<Vector2Int, TestUnitConfig> teamBBoard = new Dictionary<Vector2Int, TestUnitConfig>();
        private List<Button> teamBTileButtons = new List<Button>();
        private Dropdown[] teamBMinorCrestDropdowns = new Dropdown[3];
        private Dropdown teamBMajorCrestDropdown;

        // Current selection state
        private bool isTeamASelected = true;
        private Vector2Int? selectedTile = null;

        // Unit configuration panel
        private Dropdown unitTypeDropdown;
        private Dropdown starLevelDropdown;
        private Dropdown item1Dropdown;
        private Dropdown item2Dropdown;
        private Dropdown item3Dropdown;
        private Button addUnitButton;
        private Button removeUnitButton;
        private Text selectedTileText;

        // Status/buttons
        private Text statusText;
        private Button runCombatButton;
        private Button clearAllButton;
        private Button backButton;

        // Results
        private Text resultsText;
        private Button playAgainButton;
        private Button modifyButton;

        // Available units and items (loaded from server data)
        private List<string> availableUnits = new List<string>();
        private List<string> availableItems = new List<string>();
        private List<string> minorCrests = new List<string>();
        private List<string> majorCrests = new List<string>();

        private bool isInitialized;
        private bool isConnected;
        private Coroutine combatTimeoutCoroutine;
        private TestCombatResultMessage pendingResult;

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

            LoadAvailableData();
            CreateUI();
            SubscribeToEvents();

            isInitialized = true;
        }

        private void LoadAvailableData()
        {
            // Load unit IDs - these match the server's UnitTemplates
            availableUnits = new List<string>
            {
                // 1-cost
                "archer", "bat", "blueslime", "crawler", "greenspider",
                "littledemon", "mushroom", "ratassassin", "redslime", "starfish",
                // 2-cost
                "battlebee", "blacksmith", "chestmonster", "crabmonster", "evilplant",
                "fishman", "flowermonster", "golem", "salamander", "wormmonster",
                // 3-cost
                "beholder", "cleric", "cyclops", "icegolem", "lizardwarrior",
                "nagawizard", "specter", "werewolf",
                // 4-cost
                "bishopknight", "blackknight", "bonedragon", "eviloldmage",
                "fatdragon", "flyingdemon", "orcwithmace",
                // 5-cost
                "castlemonster", "demonking", "flameknight", "skeletonmage", "spikyshellturtle"
            };

            // Load item IDs
            availableItems = new List<string>
            {
                "", // None option
                "sword", "bow", "rod", "vest", "cloak", "belt", "tear", "glove",
                "bloodthirster", "rapidfire", "deathcap", "thornmail", "warmog",
                "infinity", "guardian_angel"
            };

            // Load crest IDs
            minorCrests = new List<string>
            {
                "", // None option
                "minor_might", "minor_vitality", "minor_swiftness",
                "minor_protection", "minor_warding", "minor_precision", "minor_sorcery"
            };

            majorCrests = new List<string>
            {
                "", // None option
                "major_bloodlust", "major_fortitude", "major_haste",
                "major_arcane", "major_iron"
            };
        }

        private void SubscribeToEvents()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;

            nm.OnConnected += HandleConnected;
            nm.OnDisconnected += HandleDisconnected;
            nm.OnError += HandleError;
        }

        private void UnsubscribeFromEvents()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;

            nm.OnConnected -= HandleConnected;
            nm.OnDisconnected -= HandleDisconnected;
            nm.OnError -= HandleError;
        }

        // ============================================
        // UI Creation
        // ============================================

        private void CreateUI()
        {
            // Ensure EventSystem exists
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[CombatTestUI] Created EventSystem");
            }

            // Create Canvas
            GameObject canvasObj = new GameObject("CombatTestCanvas");
            canvasObj.transform.SetParent(transform);
            testCanvas = canvasObj.AddComponent<Canvas>();
            testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            testCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Create panels
            CreateConfigPanel();
            CreateResultsPanel();
        }

        private void CreateConfigPanel()
        {
            configPanel = CreatePanel("ConfigPanel", new Vector2(1600, 900));

            // Title
            CreateText(configPanel.transform, "COMBAT TEST MODE", 36, new Vector2(0, 400), FontStyle.Bold);

            // Team A Panel (left)
            CreateTeamPanel(configPanel.transform, "Team A", new Vector2(-450, 50), true);

            // Team B Panel (right)
            CreateTeamPanel(configPanel.transform, "Team B", new Vector2(450, 50), false);

            // Unit Configuration Panel (center bottom)
            CreateUnitConfigPanel(configPanel.transform, new Vector2(0, -280));

            // Action buttons
            runCombatButton = CreateButton(configPanel.transform, "RUN COMBAT", new Vector2(-200, -400), OnRunCombatClicked);
            var runRT = runCombatButton.GetComponent<RectTransform>();
            runRT.sizeDelta = new Vector2(180, 50);
            runCombatButton.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.3f);

            clearAllButton = CreateButton(configPanel.transform, "Clear All", new Vector2(0, -400), OnClearAllClicked);
            var clearRT = clearAllButton.GetComponent<RectTransform>();
            clearRT.sizeDelta = new Vector2(120, 50);

            backButton = CreateButton(configPanel.transform, "Back", new Vector2(200, -400), OnBackClicked);
            var backRT = backButton.GetComponent<RectTransform>();
            backRT.sizeDelta = new Vector2(120, 50);
            backButton.GetComponent<Image>().color = new Color(0.5f, 0.3f, 0.3f);

            // Status text
            statusText = CreateText(configPanel.transform, "Configure teams and click RUN COMBAT", 16, new Vector2(0, -450));
            statusText.color = Color.gray;
        }

        private void CreateTeamPanel(Transform parent, string teamName, Vector2 position, bool isTeamA)
        {
            // Container
            GameObject container = new GameObject($"{teamName}Panel");
            container.transform.SetParent(parent, false);
            RectTransform containerRT = container.AddComponent<RectTransform>();
            containerRT.anchoredPosition = position;
            containerRT.sizeDelta = new Vector2(500, 600);

            Image containerBg = container.AddComponent<Image>();
            containerBg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

            // Team name header
            Text header = CreateText(container.transform, teamName, 24, new Vector2(0, 260), FontStyle.Bold);
            header.color = isTeamA ? new Color(0.4f, 0.6f, 0.9f) : new Color(0.9f, 0.4f, 0.4f);

            // Select team button
            Button selectBtn = CreateButton(container.transform, isTeamA ? "SELECTED" : "Select", new Vector2(0, 220), () => SelectTeam(isTeamA));
            var selectRT = selectBtn.GetComponent<RectTransform>();
            selectRT.sizeDelta = new Vector2(100, 30);
            if (isTeamA)
            {
                selectBtn.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.3f);
            }

            // Board grid (5 wide x 4 tall)
            CreateBoardGrid(container.transform, new Vector2(0, 30), isTeamA);

            // Minor crest dropdowns (3 slots)
            CreateText(container.transform, "Minor Crests:", 14, new Vector2(-120, -180));
            for (int i = 0; i < 3; i++)
            {
                float yPos = -180 - (i * 35);
                Dropdown minorDropdown = CreateDropdown(container.transform, minorCrests, new Vector2(80, yPos), 180);
                int slotIndex = i; // Capture for closure
                minorDropdown.onValueChanged.AddListener((val) => OnMinorCrestChanged(isTeamA, slotIndex, val));

                if (isTeamA)
                {
                    teamAMinorCrestDropdowns[i] = minorDropdown;
                }
                else
                {
                    teamBMinorCrestDropdowns[i] = minorDropdown;
                }
            }

            // Major crest dropdown
            CreateText(container.transform, "Major Crest:", 14, new Vector2(-120, -290));
            Dropdown majorDropdown = CreateDropdown(container.transform, majorCrests, new Vector2(80, -290), 180);
            majorDropdown.onValueChanged.AddListener((val) => OnMajorCrestChanged(isTeamA, val));

            if (isTeamA)
            {
                teamAMajorCrestDropdown = majorDropdown;
            }
            else
            {
                teamBMajorCrestDropdown = majorDropdown;
            }
        }

        private void CreateBoardGrid(Transform parent, Vector2 position, bool isTeamA)
        {
            GameObject gridContainer = new GameObject("BoardGrid");
            gridContainer.transform.SetParent(parent, false);
            RectTransform gridRT = gridContainer.AddComponent<RectTransform>();
            gridRT.anchoredPosition = position;
            gridRT.sizeDelta = new Vector2(400, 320);

            float cellWidth = 70f;
            float cellHeight = 70f;
            float totalWidth = GameConstants.Grid.WIDTH * cellWidth;
            float totalHeight = GameConstants.Grid.HEIGHT * cellHeight;
            float startX = -totalWidth / 2f + cellWidth / 2f;
            float startY = totalHeight / 2f - cellHeight / 2f;

            var tileButtons = isTeamA ? teamATileButtons : teamBTileButtons;
            tileButtons.Clear();

            for (int y = GameConstants.Grid.HEIGHT - 1; y >= 0; y--)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    float posX = startX + x * cellWidth;
                    float posY = startY - (GameConstants.Grid.HEIGHT - 1 - y) * cellHeight;

                    Button tileBtn = CreateTileButton(gridContainer.transform, new Vector2(posX, posY), x, y, isTeamA);
                    tileButtons.Add(tileBtn);
                }
            }
        }

        private Button CreateTileButton(Transform parent, Vector2 position, int x, int y, bool isTeamA)
        {
            GameObject btnObj = new GameObject($"Tile_{x}_{y}");
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(60, 60);

            Image bg = btnObj.AddComponent<Image>();
            Color baseColor = isTeamA ? new Color(0.2f, 0.3f, 0.5f) : new Color(0.5f, 0.25f, 0.25f);
            bg.color = baseColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;

            int capturedX = x;
            int capturedY = y;
            bool capturedIsTeamA = isTeamA;
            btn.onClick.AddListener(() => OnTileClicked(capturedX, capturedY, capturedIsTeamA));

            // Label text (shows unit if placed)
            Text label = CreateText(btnObj.transform, "", 10, Vector2.zero);
            label.raycastTarget = false;
            var labelRT = label.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(2, 2);
            labelRT.offsetMax = new Vector2(-2, -2);

            return btn;
        }

        private void CreateUnitConfigPanel(Transform parent, Vector2 position)
        {
            GameObject container = new GameObject("UnitConfigPanel");
            container.transform.SetParent(parent, false);
            RectTransform containerRT = container.AddComponent<RectTransform>();
            containerRT.anchoredPosition = position;
            containerRT.sizeDelta = new Vector2(800, 120);

            Image containerBg = container.AddComponent<Image>();
            containerBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            // Selected tile indicator
            selectedTileText = CreateText(container.transform, "Click a tile to select", 14, new Vector2(-300, 30));
            selectedTileText.alignment = TextAnchor.MiddleLeft;

            // Unit type dropdown
            CreateText(container.transform, "Unit:", 12, new Vector2(-280, -10));
            unitTypeDropdown = CreateDropdown(container.transform, availableUnits, new Vector2(-150, -10), 150);

            // Star level dropdown
            CreateText(container.transform, "Stars:", 12, new Vector2(-30, -10));
            starLevelDropdown = CreateDropdown(container.transform, new List<string> { "1", "2", "3" }, new Vector2(50, -10), 60);

            // Items
            CreateText(container.transform, "Items:", 12, new Vector2(100, -10));
            item1Dropdown = CreateDropdown(container.transform, availableItems, new Vector2(180, -10), 120);
            item2Dropdown = CreateDropdown(container.transform, availableItems, new Vector2(310, -10), 120);
            item3Dropdown = CreateDropdown(container.transform, availableItems, new Vector2(440, -10), 120);

            // Add/Remove buttons
            addUnitButton = CreateButton(container.transform, "Add Unit", new Vector2(-100, -50), OnAddUnitClicked);
            var addRT = addUnitButton.GetComponent<RectTransform>();
            addRT.sizeDelta = new Vector2(100, 35);
            addUnitButton.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.3f);

            removeUnitButton = CreateButton(container.transform, "Remove", new Vector2(50, -50), OnRemoveUnitClicked);
            var removeRT = removeUnitButton.GetComponent<RectTransform>();
            removeRT.sizeDelta = new Vector2(100, 35);
            removeUnitButton.GetComponent<Image>().color = new Color(0.5f, 0.3f, 0.3f);
        }

        private void CreateResultsPanel()
        {
            resultsPanel = CreatePanel("ResultsPanel", new Vector2(500, 400));
            resultsPanel.SetActive(false);

            CreateText(resultsPanel.transform, "COMBAT RESULTS", 28, new Vector2(0, 150), FontStyle.Bold);

            resultsText = CreateText(resultsPanel.transform, "...", 18, new Vector2(0, 50));
            resultsText.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 100);

            playAgainButton = CreateButton(resultsPanel.transform, "Run Again", new Vector2(-80, -100), OnPlayAgainClicked);
            playAgainButton.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.3f);

            modifyButton = CreateButton(resultsPanel.transform, "Modify Teams", new Vector2(80, -100), OnModifyClicked);
        }

        // ============================================
        // Helper UI Methods
        // ============================================

        private GameObject CreatePanel(string name, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(testCanvas.transform, false);

            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);

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
            t.raycastTarget = false; // Don't block clicks on elements behind

            return t;
        }

        private Button CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            GameObject obj = new GameObject("Button_" + text);
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(150, 45);

            Image bg = obj.AddComponent<Image>();
            bg.color = new Color(0.3f, 0.4f, 0.5f);

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
            t.fontSize = 16;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            return btn;
        }

        private Dropdown CreateDropdown(Transform parent, List<string> options, Vector2 position, float width)
        {
            // Load the default dropdown from Unity's built-in resources if available,
            // otherwise create a minimal working dropdown manually

            GameObject dropdownObj = new GameObject("Dropdown");
            dropdownObj.transform.SetParent(parent, false);

            RectTransform dropdownRT = dropdownObj.AddComponent<RectTransform>();
            dropdownRT.anchoredPosition = position;
            dropdownRT.sizeDelta = new Vector2(width, 30);

            Image dropdownBg = dropdownObj.AddComponent<Image>();
            dropdownBg.color = new Color(0.2f, 0.2f, 0.25f);
            dropdownBg.raycastTarget = true;

            Dropdown dropdown = dropdownObj.AddComponent<Dropdown>();
            dropdown.targetGraphic = dropdownBg;

            // Label (shows current selection)
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform labelRT = labelObj.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(5, 2);
            labelRT.offsetMax = new Vector2(-20, -2);

            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 11;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.raycastTarget = false;
            dropdown.captionText = labelText;

            // Arrow
            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform arrowRT = arrowObj.AddComponent<RectTransform>();
            arrowRT.anchorMin = new Vector2(1, 0.5f);
            arrowRT.anchorMax = new Vector2(1, 0.5f);
            arrowRT.pivot = new Vector2(1, 0.5f);
            arrowRT.anchoredPosition = new Vector2(-5, 0);
            arrowRT.sizeDelta = new Vector2(15, 15);

            Text arrowText = arrowObj.AddComponent<Text>();
            arrowText.text = "\u25BC";
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowText.fontSize = 10;
            arrowText.color = Color.white;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.raycastTarget = false;

            // Template
            GameObject templateObj = new GameObject("Template");
            templateObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform templateRT = templateObj.AddComponent<RectTransform>();
            templateRT.anchorMin = new Vector2(0, 0);
            templateRT.anchorMax = new Vector2(1, 0);
            templateRT.pivot = new Vector2(0.5f, 1);
            templateRT.anchoredPosition = Vector2.zero;
            templateRT.sizeDelta = new Vector2(0, 150);

            Image templateBg = templateObj.AddComponent<Image>();
            templateBg.color = new Color(0.15f, 0.15f, 0.2f);
            templateBg.raycastTarget = true;

            ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f; // Increase scroll speed for dropdown lists

            // Viewport
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(templateObj.transform, false);
            RectTransform viewportRT = viewportObj.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;

            Image viewportImage = viewportObj.AddComponent<Image>();
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;

            Mask viewportMask = viewportObj.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            scrollRect.viewport = viewportRT;

            // Content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRT = contentObj.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 28);

            scrollRect.content = contentRT;

            // Item (template for each option)
            GameObject itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform, false);
            RectTransform itemRT = itemObj.AddComponent<RectTransform>();
            itemRT.anchorMin = new Vector2(0, 0.5f);
            itemRT.anchorMax = new Vector2(1, 0.5f);
            itemRT.pivot = new Vector2(0.5f, 0.5f);
            itemRT.sizeDelta = new Vector2(0, 26);

            Toggle toggle = itemObj.AddComponent<Toggle>();

            // Item Background
            GameObject itemBgObj = new GameObject("Item Background");
            itemBgObj.transform.SetParent(itemObj.transform, false);
            RectTransform itemBgRT = itemBgObj.AddComponent<RectTransform>();
            itemBgRT.anchorMin = Vector2.zero;
            itemBgRT.anchorMax = Vector2.one;
            itemBgRT.offsetMin = Vector2.zero;
            itemBgRT.offsetMax = Vector2.zero;

            Image itemBgImage = itemBgObj.AddComponent<Image>();
            itemBgImage.color = new Color(0.25f, 0.25f, 0.3f);
            toggle.targetGraphic = itemBgImage;

            // Item Checkmark
            GameObject checkmarkObj = new GameObject("Item Checkmark");
            checkmarkObj.transform.SetParent(itemBgObj.transform, false);
            RectTransform checkmarkRT = checkmarkObj.AddComponent<RectTransform>();
            checkmarkRT.anchorMin = Vector2.zero;
            checkmarkRT.anchorMax = Vector2.one;
            checkmarkRT.offsetMin = Vector2.zero;
            checkmarkRT.offsetMax = Vector2.zero;

            Image checkmarkImage = checkmarkObj.AddComponent<Image>();
            checkmarkImage.color = new Color(0.4f, 0.6f, 0.8f, 0.5f);
            toggle.graphic = checkmarkImage;

            // Item Label
            GameObject itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(itemObj.transform, false);
            RectTransform itemLabelRT = itemLabelObj.AddComponent<RectTransform>();
            itemLabelRT.anchorMin = Vector2.zero;
            itemLabelRT.anchorMax = Vector2.one;
            itemLabelRT.offsetMin = new Vector2(5, 1);
            itemLabelRT.offsetMax = new Vector2(-5, -1);

            Text itemLabel = itemLabelObj.AddComponent<Text>();
            itemLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemLabel.fontSize = 11;
            itemLabel.color = Color.white;
            itemLabel.alignment = TextAnchor.MiddleLeft;
            itemLabel.raycastTarget = false;

            dropdown.itemText = itemLabel;
            dropdown.template = templateRT;

            templateObj.SetActive(false);

            // Set up color transitions for better visual feedback
            ColorBlock colors = dropdown.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.25f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.35f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.2f);
            colors.selectedColor = new Color(0.25f, 0.25f, 0.3f);
            colors.colorMultiplier = 1f;
            dropdown.colors = colors;

            // Add options BEFORE setting template inactive was already done above
            dropdown.ClearOptions();
            if (options != null && options.Count > 0)
            {
                dropdown.AddOptions(options);
                Debug.Log($"[CombatTestUI] Created dropdown with {options.Count} options, first: {options[0]}");
            }
            else
            {
                Debug.LogWarning("[CombatTestUI] Created dropdown with no options!");
            }

            // Force refresh
            dropdown.value = 0;
            dropdown.RefreshShownValue();

            // Ensure interactable
            dropdown.interactable = true;

            return dropdown;
        }

        // ============================================
        // Event Handlers
        // ============================================

        private void HandleConnected()
        {
            isConnected = true;
            statusText.text = "Connected to server";
            statusText.color = Color.green;
        }

        private void HandleDisconnected()
        {
            isConnected = false;
            statusText.text = "Disconnected from server";
            statusText.color = Color.red;
        }

        private void HandleError(string error)
        {
            statusText.text = $"Error: {error}";
            statusText.color = Color.red;
        }

        private void SelectTeam(bool selectTeamA)
        {
            isTeamASelected = selectTeamA;
            selectedTile = null;
            UpdateTeamSelection();
            UpdateSelectedTileUI();
        }

        private void UpdateTeamSelection()
        {
            // Update team A button
            var teamAPanel = configPanel.transform.Find("Team APanel");
            if (teamAPanel != null)
            {
                var selectBtn = teamAPanel.Find("Button_SELECTED") ?? teamAPanel.Find("Button_Select");
                if (selectBtn != null)
                {
                    var btnText = selectBtn.GetComponentInChildren<Text>();
                    var btnImg = selectBtn.GetComponent<Image>();
                    if (isTeamASelected)
                    {
                        btnText.text = "SELECTED";
                        btnImg.color = new Color(0.3f, 0.6f, 0.3f);
                    }
                    else
                    {
                        btnText.text = "Select";
                        btnImg.color = new Color(0.3f, 0.4f, 0.5f);
                    }
                }
            }

            // Update team B button
            var teamBPanel = configPanel.transform.Find("Team BPanel");
            if (teamBPanel != null)
            {
                var selectBtn = teamBPanel.Find("Button_SELECTED") ?? teamBPanel.Find("Button_Select");
                if (selectBtn != null)
                {
                    var btnText = selectBtn.GetComponentInChildren<Text>();
                    var btnImg = selectBtn.GetComponent<Image>();
                    if (!isTeamASelected)
                    {
                        btnText.text = "SELECTED";
                        btnImg.color = new Color(0.3f, 0.6f, 0.3f);
                    }
                    else
                    {
                        btnText.text = "Select";
                        btnImg.color = new Color(0.3f, 0.4f, 0.5f);
                    }
                }
            }
        }

        private void OnTileClicked(int x, int y, bool clickedTeamA)
        {
            // Auto-select team if clicking on a tile
            if (clickedTeamA != isTeamASelected)
            {
                SelectTeam(clickedTeamA);
            }

            selectedTile = new Vector2Int(x, y);
            UpdateSelectedTileUI();
            UpdateTileHighlights();
        }

        private void UpdateSelectedTileUI()
        {
            if (selectedTile.HasValue)
            {
                string teamName = isTeamASelected ? "Team A" : "Team B";
                var board = isTeamASelected ? teamABoard : teamBBoard;

                selectedTileText.text = $"{teamName} - Tile ({selectedTile.Value.x}, {selectedTile.Value.y})";

                // If there's a unit on this tile, populate the dropdowns
                if (board.TryGetValue(selectedTile.Value, out var unit))
                {
                    int unitIndex = availableUnits.IndexOf(unit.unitId);
                    if (unitIndex >= 0) unitTypeDropdown.value = unitIndex;
                    starLevelDropdown.value = unit.starLevel - 1;

                    // Items
                    item1Dropdown.value = unit.itemIds.Count > 0 ? availableItems.IndexOf(unit.itemIds[0]) : 0;
                    item2Dropdown.value = unit.itemIds.Count > 1 ? availableItems.IndexOf(unit.itemIds[1]) : 0;
                    item3Dropdown.value = unit.itemIds.Count > 2 ? availableItems.IndexOf(unit.itemIds[2]) : 0;

                    removeUnitButton.interactable = true;
                }
                else
                {
                    removeUnitButton.interactable = false;
                }
            }
            else
            {
                selectedTileText.text = "Click a tile to select";
                removeUnitButton.interactable = false;
            }
        }

        private void UpdateTileHighlights()
        {
            // Update Team A tiles
            for (int i = 0; i < teamATileButtons.Count; i++)
            {
                int x = i % GameConstants.Grid.WIDTH;
                int y = i / GameConstants.Grid.WIDTH;
                y = GameConstants.Grid.HEIGHT - 1 - y; // Flip Y

                var btn = teamATileButtons[i];
                var img = btn.GetComponent<Image>();

                Color baseColor = new Color(0.2f, 0.3f, 0.5f);
                bool hasUnit = teamABoard.ContainsKey(new Vector2Int(x, y));
                bool isSelected = isTeamASelected && selectedTile.HasValue && selectedTile.Value.x == x && selectedTile.Value.y == y;

                if (hasUnit)
                {
                    baseColor = new Color(0.3f, 0.5f, 0.7f);
                }
                if (isSelected)
                {
                    baseColor = new Color(0.5f, 0.7f, 0.3f);
                }

                img.color = baseColor;

                // Update label
                var label = btn.GetComponentInChildren<Text>();
                if (hasUnit)
                {
                    var unit = teamABoard[new Vector2Int(x, y)];
                    label.text = $"{unit.unitId}\n{new string('*', unit.starLevel)}";
                }
                else
                {
                    label.text = "";
                }
            }

            // Update Team B tiles
            for (int i = 0; i < teamBTileButtons.Count; i++)
            {
                int x = i % GameConstants.Grid.WIDTH;
                int y = i / GameConstants.Grid.WIDTH;
                y = GameConstants.Grid.HEIGHT - 1 - y; // Flip Y

                var btn = teamBTileButtons[i];
                var img = btn.GetComponent<Image>();

                Color baseColor = new Color(0.5f, 0.25f, 0.25f);
                bool hasUnit = teamBBoard.ContainsKey(new Vector2Int(x, y));
                bool isSelected = !isTeamASelected && selectedTile.HasValue && selectedTile.Value.x == x && selectedTile.Value.y == y;

                if (hasUnit)
                {
                    baseColor = new Color(0.7f, 0.4f, 0.4f);
                }
                if (isSelected)
                {
                    baseColor = new Color(0.5f, 0.7f, 0.3f);
                }

                img.color = baseColor;

                // Update label
                var label = btn.GetComponentInChildren<Text>();
                if (hasUnit)
                {
                    var unit = teamBBoard[new Vector2Int(x, y)];
                    label.text = $"{unit.unitId}\n{new string('*', unit.starLevel)}";
                }
                else
                {
                    label.text = "";
                }
            }
        }

        private void OnMinorCrestChanged(bool isTeamA, int slotIndex, int value)
        {
            var config = isTeamA ? teamAConfig : teamBConfig;
            string crestId = value >= 0 && value < minorCrests.Count ? minorCrests[value] : "";

            // Ensure the list has enough slots
            while (config.minorCrestIds.Count <= slotIndex)
            {
                config.minorCrestIds.Add("");
            }

            config.minorCrestIds[slotIndex] = crestId;

            // Remove empty trailing entries
            while (config.minorCrestIds.Count > 0 && string.IsNullOrEmpty(config.minorCrestIds[config.minorCrestIds.Count - 1]))
            {
                config.minorCrestIds.RemoveAt(config.minorCrestIds.Count - 1);
            }
        }

        private void OnMajorCrestChanged(bool isTeamA, int value)
        {
            var config = isTeamA ? teamAConfig : teamBConfig;
            string crestId = value >= 0 && value < majorCrests.Count ? majorCrests[value] : "";
            config.majorCrestId = crestId;
        }

        private void OnAddUnitClicked()
        {
            if (!selectedTile.HasValue)
            {
                statusText.text = "Select a tile first";
                statusText.color = Color.yellow;
                return;
            }

            var board = isTeamASelected ? teamABoard : teamBBoard;
            var pos = selectedTile.Value;

            string unitId = availableUnits[unitTypeDropdown.value];
            int starLevel = starLevelDropdown.value + 1;

            List<string> items = new List<string>();
            if (item1Dropdown.value > 0) items.Add(availableItems[item1Dropdown.value]);
            if (item2Dropdown.value > 0) items.Add(availableItems[item2Dropdown.value]);
            if (item3Dropdown.value > 0) items.Add(availableItems[item3Dropdown.value]);

            TestUnitConfig unit = new TestUnitConfig
            {
                unitId = unitId,
                starLevel = starLevel,
                boardX = pos.x,
                boardY = pos.y,
                itemIds = items
            };

            board[pos] = unit;

            statusText.text = $"Added {unitId} ({starLevel}*) at ({pos.x}, {pos.y})";
            statusText.color = Color.green;

            UpdateTileHighlights();
            UpdateSelectedTileUI();
        }

        private void OnRemoveUnitClicked()
        {
            if (!selectedTile.HasValue)
            {
                return;
            }

            var board = isTeamASelected ? teamABoard : teamBBoard;
            var pos = selectedTile.Value;

            if (board.ContainsKey(pos))
            {
                board.Remove(pos);
                statusText.text = $"Removed unit from ({pos.x}, {pos.y})";
                statusText.color = Color.yellow;
            }

            UpdateTileHighlights();
            UpdateSelectedTileUI();
        }

        private void OnRunCombatClicked()
        {
            // Build team configs from boards
            teamAConfig.units.Clear();
            foreach (var kvp in teamABoard)
            {
                teamAConfig.units.Add(kvp.Value);
            }

            teamBConfig.units.Clear();
            foreach (var kvp in teamBBoard)
            {
                teamBConfig.units.Add(kvp.Value);
            }

            // Validate
            if (teamAConfig.units.Count == 0)
            {
                statusText.text = "Team A needs at least one unit!";
                statusText.color = Color.red;
                return;
            }

            if (teamBConfig.units.Count == 0)
            {
                statusText.text = "Team B needs at least one unit!";
                statusText.color = Color.red;
                return;
            }

            // Connect to server if needed
            var nm = NetworkManager.Instance;
            if (nm == null)
            {
                GameObject nmObj = new GameObject("NetworkManager");
                nmObj.AddComponent<NetworkManager>();
                nm = NetworkManager.Instance;
                SubscribeToEvents();
            }

            if (!nm.IsConnected)
            {
                statusText.text = "Connecting to server...";
                statusText.color = Color.yellow;
                nm.Connect(defaultServerUrl);

                // Wait for connection then send
                StartCoroutine(WaitForConnectionAndSend());
                return;
            }

            SendTestCombatRequest();
        }

        private System.Collections.IEnumerator WaitForConnectionAndSend()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (!isConnected && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (isConnected)
            {
                SendTestCombatRequest();
            }
            else
            {
                statusText.text = "Failed to connect to server";
                statusText.color = Color.red;
            }
        }

        private void SendTestCombatRequest()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;

            statusText.text = "Running combat simulation...";
            statusText.color = Color.cyan;

            // Send the test combat message
            nm.SendTestCombat(teamAConfig, teamBConfig);
        }

        /// <summary>
        /// Called by NetworkManager when test combat result is received
        /// </summary>
        public void OnTestCombatResult(TestCombatResultMessage result)
        {
            if (result == null)
            {
                statusText.text = "No result received";
                statusText.color = Color.red;
                return;
            }

            // Start combat playback if we have events
            if (result.events != null && result.events.Count > 0)
            {
                StartCombatPlayback(result);
            }
            else
            {
                // Just show results
                ShowResults(result);
            }
        }

        private void StartCombatPlayback(TestCombatResultMessage result)
        {
            statusText.text = "Playing combat...";
            statusText.color = Color.cyan;

            // Hide config panel during combat
            configPanel.SetActive(false);

            // Ensure shop UI stays hidden during combat
            HideShopUI();

            // Ensure unit model database is loaded (needed for proper unit models)
            EnsureUnitModelDatabaseLoaded();

            // Show game visuals
            if (Game3DSetup.Instance != null)
            {
                Game3DSetup.Instance.ShowGameVisuals();
            }

            // Get the board for visualization - wait a frame for it to be ready
            StartCoroutine(StartCombatPlaybackDelayed(result));
        }

        private void EnsureUnitModelDatabaseLoaded()
        {
            // Load UnitModelDatabase if not already loaded
            if (UnitVisual3D.modelDatabase == null)
            {
                var database = Resources.Load<UnitModelDatabase>("UnitModelDatabase");
                if (database != null)
                {
                    UnitVisual3D.modelDatabase = database;
                    Debug.Log("[CombatTestUI] Loaded UnitModelDatabase");
                }
                else
                {
                    Debug.LogWarning("[CombatTestUI] UnitModelDatabase not found in Resources folder");
                }
            }

            // Load unit templates into a cache for test mode
            if (testUnitTemplates == null || testUnitTemplates.Count == 0)
            {
                LoadUnitTemplates();
            }
        }

        private Dictionary<string, UnitData> testUnitTemplates = new Dictionary<string, UnitData>();

        private void LoadUnitTemplates()
        {
            testUnitTemplates.Clear();

            // Load all unit templates from Resources
            var allUnits = Resources.LoadAll<UnitData>("ScriptableObjects/NewUnits");
            foreach (var unit in allUnits)
            {
                string key = unit.name.ToLower();
                if (!testUnitTemplates.ContainsKey(key))
                {
                    testUnitTemplates[key] = unit;
                }
            }

            Debug.Log($"[CombatTestUI] Loaded {testUnitTemplates.Count} unit templates");
        }

        /// <summary>
        /// Get unit template for test mode (used by CombatPlayback when ServerGameState is not available)
        /// </summary>
        public UnitData GetUnitTemplate(string unitId)
        {
            if (testUnitTemplates == null || testUnitTemplates.Count == 0)
            {
                LoadUnitTemplates();
            }

            string key = unitId.ToLower();
            if (testUnitTemplates.TryGetValue(key, out var template))
            {
                return template;
            }
            return null;
        }

        private System.Collections.IEnumerator StartCombatPlaybackDelayed(TestCombatResultMessage result)
        {
            // Wait for game visuals to initialize
            yield return null;
            yield return null;

            // Get the board for visualization
            HexBoard3D board = HexBoard3D.Instance;
            if (Game3DSetup.Instance != null)
            {
                board = Game3DSetup.Instance.GetPlayerBoard();
            }

            // If still no board, try to find any board
            if (board == null)
            {
                board = UnityEngine.Object.FindAnyObjectByType<HexBoard3D>();
            }

            if (board == null)
            {
                Debug.LogError("[CombatTestUI] No HexBoard3D found for combat playback!");
                ShowResults(result);
                yield break;
            }

            // Focus camera on the board
            if (IsometricCameraSetup.Instance != null)
            {
                IsometricCameraSetup.Instance.FocusOnBoard(board, false);
            }
            else
            {
                // Try to find camera setup
                var cameraSetup = UnityEngine.Object.FindAnyObjectByType<IsometricCameraSetup>();
                if (cameraSetup != null)
                {
                    cameraSetup.FocusOnBoard(board, false);
                }
            }

            // Wait another frame for camera to position
            yield return null;

            // Create or get ServerCombatVisualizer
            var visualizer = ServerCombatVisualizer.Instance;
            if (visualizer == null)
            {
                // Create one if it doesn't exist
                GameObject vizObj = new GameObject("ServerCombatVisualizer");
                visualizer = vizObj.AddComponent<ServerCombatVisualizer>();
            }

            // Store pending result for timeout fallback
            pendingResult = result;

            // Subscribe to playback end
            visualizer.OnCombatVisualizationEnded += () => OnCombatPlaybackEnded(result);

            // Start playback with test mode flag
            var playback = visualizer.PlayCombat(board, result.events, "player1", true, 0);

            if (playback == null)
            {
                Debug.LogError("[CombatTestUI] Failed to start combat playback!");
                ShowResults(result);
            }
            else
            {
                // Start a timeout coroutine in case combat visualization doesn't end properly
                // (e.g., simultaneous deaths causing no winner callback)
                float estimatedDuration = EstimateCombatDuration(result.events);
                combatTimeoutCoroutine = StartCoroutine(CombatTimeoutFallback(result, estimatedDuration + 5f));
            }
        }

        private float EstimateCombatDuration(List<ServerCombatEvent> events)
        {
            if (events == null || events.Count == 0) return 10f;

            // Find the maximum tick in the events
            int maxTick = 0;
            foreach (var evt in events)
            {
                if (evt.tick > maxTick) maxTick = evt.tick;
            }

            // Convert ticks to seconds (assuming 50ms per tick)
            return maxTick * 0.05f;
        }

        private System.Collections.IEnumerator CombatTimeoutFallback(TestCombatResultMessage result, float timeout)
        {
            yield return new WaitForSeconds(timeout);

            // If we get here, the combat visualization didn't end properly
            Debug.LogWarning("[CombatTestUI] Combat visualization timed out, showing results anyway");

            // Clean up
            if (ServerCombatVisualizer.Instance != null)
            {
                ServerCombatVisualizer.Instance.OnCombatVisualizationEnded -= () => OnCombatPlaybackEnded(result);
                ServerCombatVisualizer.Instance.StopPlayback();
            }

            combatTimeoutCoroutine = null;
            ShowResults(result);
        }

        private void OnCombatPlaybackEnded(TestCombatResultMessage result)
        {
            // Unsubscribe
            if (ServerCombatVisualizer.Instance != null)
            {
                ServerCombatVisualizer.Instance.OnCombatVisualizationEnded -= () => OnCombatPlaybackEnded(result);
            }

            // Cancel the timeout since we got the callback
            if (combatTimeoutCoroutine != null)
            {
                StopCoroutine(combatTimeoutCoroutine);
                combatTimeoutCoroutine = null;
            }

            // Wait a moment then show results
            StartCoroutine(ShowResultsAfterDelay(result, 2f));
        }

        private System.Collections.IEnumerator ShowResultsAfterDelay(TestCombatResultMessage result, float delay)
        {
            yield return new WaitForSeconds(delay);
            ShowResults(result);
        }

        private void ShowResults(TestCombatResultMessage result)
        {
            // Stop any ongoing combat
            if (ServerCombatVisualizer.Instance != null)
            {
                ServerCombatVisualizer.Instance.StopPlayback();
            }

            // Hide game visuals
            if (Game3DSetup.Instance != null)
            {
                Game3DSetup.Instance.HideGameVisuals();
            }

            configPanel.SetActive(false);
            resultsPanel.SetActive(true);

            // Handle draw/no winner case
            string winnerName;
            Color resultColor;
            if (string.IsNullOrEmpty(result.winner) || result.winner == "draw" || result.winner == "none")
            {
                winnerName = "Draw!";
                resultColor = Color.yellow;
            }
            else if (result.winner == "teamA")
            {
                winnerName = "Team A Wins!";
                resultColor = new Color(0.4f, 0.6f, 0.9f);
            }
            else
            {
                winnerName = "Team B Wins!";
                resultColor = new Color(0.9f, 0.4f, 0.4f);
            }

            resultsText.text = $"{winnerName}\n" +
                              $"Remaining Units: {result.remainingUnits}\n" +
                              $"Damage Dealt: {result.damage}";

            resultsText.color = resultColor;

            statusText.text = "Combat complete!";
            statusText.color = Color.green;

            pendingResult = null;
        }

        private void OnPlayAgainClicked()
        {
            // Clean up any combat visuals
            if (ServerCombatVisualizer.Instance != null)
            {
                ServerCombatVisualizer.Instance.StopPlayback();
            }

            // Hide game visuals (will be shown again when combat starts)
            if (Game3DSetup.Instance != null)
            {
                Game3DSetup.Instance.HideGameVisuals();
            }

            resultsPanel.SetActive(false);

            // Keep shop hidden
            HideShopUI();

            // Re-run with same teams
            SendTestCombatRequest();
        }

        private void OnModifyClicked()
        {
            // Clean up any combat visuals
            if (ServerCombatVisualizer.Instance != null)
            {
                ServerCombatVisualizer.Instance.StopPlayback();
            }

            // Hide game visuals
            if (Game3DSetup.Instance != null)
            {
                Game3DSetup.Instance.HideGameVisuals();
            }

            resultsPanel.SetActive(false);
            configPanel.SetActive(true);

            // Keep shop hidden
            HideShopUI();
        }

        private void OnClearAllClicked()
        {
            teamABoard.Clear();
            teamBBoard.Clear();
            teamAConfig = new TestTeamConfig();
            teamBConfig = new TestTeamConfig();

            // Reset crest dropdowns
            for (int i = 0; i < 3; i++)
            {
                if (teamAMinorCrestDropdowns[i] != null) teamAMinorCrestDropdowns[i].value = 0;
                if (teamBMinorCrestDropdowns[i] != null) teamBMinorCrestDropdowns[i].value = 0;
            }
            if (teamAMajorCrestDropdown != null) teamAMajorCrestDropdown.value = 0;
            if (teamBMajorCrestDropdown != null) teamBMajorCrestDropdown.value = 0;

            selectedTile = null;

            UpdateTileHighlights();
            UpdateSelectedTileUI();

            statusText.text = "All units cleared";
            statusText.color = Color.yellow;
        }

        private void OnBackClicked()
        {
            Hide();

            // Show main menu
            if (MainMenuUI.Instance != null)
            {
                MainMenuUI.Instance.Show();
            }
        }

        // ============================================
        // Public Methods
        // ============================================

        public void Show()
        {
            if (!isInitialized) Initialize();
            testCanvas.gameObject.SetActive(true);
            configPanel.SetActive(true);
            resultsPanel.SetActive(false);

            // Hide the shop UI during combat test mode
            HideShopUI();

            // Update UI
            UpdateTileHighlights();
            UpdateSelectedTileUI();
        }

        private void HideShopUI()
        {
            // Find and hide GameUI entirely during combat test mode
            var gameUI = GameUI.Instance;
            if (gameUI == null)
            {
                gameUI = Object.FindAnyObjectByType<GameUI>(FindObjectsInactive.Include);
            }

            if (gameUI != null)
            {
                gameUI.Hide();
                Debug.Log("[CombatTestUI] Hidden GameUI");
            }

            // Also try to find and hide any canvas named "GameCanvas"
            var allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in allCanvases)
            {
                if (canvas.name.Contains("Game") && !canvas.name.Contains("CombatTest"))
                {
                    canvas.enabled = false;
                    Debug.Log($"[CombatTestUI] Disabled canvas: {canvas.name}");
                }
            }
        }

        private void ShowShopUI()
        {
            // Restore GameUI when leaving combat test mode
            var gameUI = Object.FindAnyObjectByType<GameUI>(FindObjectsInactive.Include);
            if (gameUI != null)
            {
                gameUI.Show();
            }

            // Re-enable any game canvases we disabled
            var allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in allCanvases)
            {
                if (canvas.name.Contains("Game") && !canvas.name.Contains("CombatTest"))
                {
                    canvas.enabled = true;
                }
            }
        }

        public void Hide()
        {
            if (testCanvas != null)
            {
                testCanvas.gameObject.SetActive(false);
            }

            // Clean up any ongoing playback
            if (ServerCombatVisualizer.Instance != null)
            {
                ServerCombatVisualizer.Instance.StopPlayback();
            }

            // Hide game visuals
            if (Game3DSetup.Instance != null)
            {
                Game3DSetup.Instance.HideGameVisuals();
            }

            // Restore GameUI when leaving combat test mode
            ShowShopUI();
        }
    }
}
