using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Systems;
using Crestforge.Data;
using Crestforge.Combat;

namespace Crestforge.UI
{
    /// <summary>
    /// Main game UI - responsive design for desktop and mobile
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        [Header("References")]
        public Canvas mainCanvas;
        public Camera mainCamera;

        [Header("Panels")]
        public RectTransform topBar;
        public RectTransform bottomPanel;
        public RectTransform shopPanel;
        public RectTransform benchPanel;
        public RectTransform infoPanel;
        public RectTransform selectionPanel;

        [Header("Top Bar Elements")]
        public Text healthText;
        public Text goldText;
        public Text levelText;
        public Text roundText;
        public Text timerText;
        public Button fightButton;

        [Header("Shop Elements")]
        public RectTransform shopSlotContainer;
        public Button rerollButton;
        public Button buyXPButton;
        public Button lockButton;
        public Text rerollCostText;
        public Text xpCostText;
        public Text xpProgressText;

        [Header("Bench Elements")]
        public RectTransform benchSlotContainer;

        [Header("Trait Panel")]
        public RectTransform traitPanel;
        public RectTransform traitContent;

        [Header("Trait Tooltip")]
        public RectTransform traitTooltipPanel;
        public Text traitTooltipTitle;
        public Text traitTooltipDescription;
        public RectTransform traitTooltipTierContainer;
        public Text traitTooltipUnits;

        [Header("Prefabs")]
        public GameObject unitCardPrefab;
        public GameObject itemCardPrefab;
        public GameObject crestCardPrefab;

        [Header("Tooltip")]
        public RectTransform tooltipPanel;
        public Text tooltipTitle;
        public Text tooltipCost;
        public Text tooltipStats;
        public Text tooltipTraits;
        public Text tooltipAbility;
        public Image tooltipSprite;

        // Runtime
        private GameState state;
        private List<UnitCardUI> shopCards = new List<UnitCardUI>();
        private List<UnitCardUI> benchCards = new List<UnitCardUI>();
        private List<GameObject> traitEntries = new List<GameObject>();
        private List<TraitEntryUI> traitEntryComponents = new List<TraitEntryUI>();
        private System.Action pendingAction;
        private bool isPortrait;
        private GamePhase lastShownPhase = GamePhase.Planning;
        private bool selectionShown = false;
        private bool isInitialized = false;
        private bool isTooltipPinned = false;
        private TraitData hoveredTrait = null;
        private List<GameObject> traitTooltipTierEntries = new List<GameObject>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            Initialize();
            
            // Hide if main menu is present and active
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // Initialize when enabled if not already done
            if (!isInitialized)
            {
                Initialize();
            }
            
            // Reset selection state when re-enabled
            selectionShown = false;
            lastShownPhase = GamePhase.Planning;
        }

        private void Initialize()
        {
            if (isInitialized) return;
            
            state = GameState.Instance;
            
            if (mainCanvas == null)
                mainCanvas = GetComponentInChildren<Canvas>();
            if (mainCamera == null)
                mainCamera = Camera.main;

            CreateUI();
            UpdateLayout();
            isInitialized = true;
        }

        private void Update()
        {
            if (state == null)
            {
                state = GameState.Instance;
                if (state == null) return;
            }

            // Execute pending actions
            if (pendingAction != null)
            {
                var action = pendingAction;
                pendingAction = null;
                action.Invoke();
            }

            // Hide tooltip when clicking anywhere (except on unit cards, which handle their own clicks)
            if (Input.GetMouseButtonDown(0) && tooltipPanel != null && tooltipPanel.gameObject.activeSelf)
            {
                // Check if clicking on a UI element
                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                if (eventSystem != null)
                {
                    var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem)
                    {
                        position = Input.mousePosition
                    };
                    var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    eventSystem.RaycastAll(pointerData, results);

                    // Check if any hit object is a UnitCardUI
                    bool clickedOnUnitCard = false;
                    foreach (var result in results)
                    {
                        if (result.gameObject.GetComponentInParent<UnitCardUI>() != null)
                        {
                            clickedOnUnitCard = true;
                            break;
                        }
                    }

                    if (!clickedOnUnitCard)
                    {
                        isTooltipPinned = false;
                        HideTooltip();
                    }
                }
            }

            // Check for orientation change
            bool nowPortrait = Screen.height > Screen.width;
            if (nowPortrait != isPortrait)
            {
                isPortrait = nowPortrait;
                UpdateLayout();
            }

            // Update UI based on phase
            UpdateTopBar();
            UpdatePhaseUI();
            UpdateTooltip();
            UpdateTraitPanel();
            UpdateTraitTooltip();
        }

        private void CreateUI()
        {
            if (mainCanvas == null)
            {
                GameObject canvasObj = new GameObject("GameCanvas");
                mainCanvas = canvasObj.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Ensure EventSystem exists for UI interaction
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            CreateTopBar();
            CreateBottomPanel();
            CreateSelectionPanel();
            CreateTooltip();
            CreateTraitPanel();
            CreateTraitTooltip();
        }

        private void CreateTopBar()
        {
            // Top bar background
            GameObject topBarObj = CreatePanel("TopBar", mainCanvas.transform);
            topBar = topBarObj.GetComponent<RectTransform>();
            topBar.anchorMin = new Vector2(0, 1);
            topBar.anchorMax = new Vector2(1, 1);
            topBar.pivot = new Vector2(0.5f, 1);
            topBar.sizeDelta = new Vector2(0, 80);
            topBar.anchoredPosition = Vector2.zero;
            topBarObj.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            // Layout
            HorizontalLayoutGroup hlg = topBarObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15;
            hlg.padding = new RectOffset(15, 15, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;

            // Round
            roundText = CreateText("R1", topBar, 60);
            roundText.fontStyle = FontStyle.Bold;

            // Health
            healthText = CreateText("❤ 100", topBar, 80);
            healthText.color = new Color(1f, 0.4f, 0.4f);

            // Gold
            goldText = CreateText("💰 0", topBar, 80);
            goldText.color = new Color(1f, 0.85f, 0.3f);

            // Level
            levelText = CreateText("Lv 1", topBar, 60);
            levelText.color = new Color(0.5f, 0.8f, 1f);

            // Spacer
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(topBar);
            LayoutElement le = spacer.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;

            // Timer
            timerText = CreateText("30s", topBar, 60);
            timerText.alignment = TextAnchor.MiddleRight;

            // Fight button
            fightButton = CreateButton("⚔ FIGHT", topBar, 120, OnFightClicked);
            fightButton.GetComponent<Image>().color = new Color(0.3f, 0.7f, 0.3f);
        }

        private void CreateBottomPanel()
        {
            // Bench section - positioned just below battlefield (separate from shop)
            CreateBenchSection(mainCanvas.transform);
            
            // Shop section - at the bottom of screen
            CreateShopSection(mainCanvas.transform);
        }

        private void CreateShopSection(Transform parent)
        {
            // Shop panel at bottom of screen
            GameObject shopObj = CreatePanel("ShopPanel", parent);
            shopPanel = shopObj.GetComponent<RectTransform>();
            shopPanel.anchorMin = new Vector2(0, 0);
            shopPanel.anchorMax = new Vector2(1, 0);
            shopPanel.pivot = new Vector2(0.5f, 0);
            shopPanel.sizeDelta = new Vector2(0, 180);
            shopPanel.anchoredPosition = new Vector2(0, 30); // Safe area offset
            shopObj.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            
            // Also assign to bottomPanel for compatibility
            bottomPanel = shopPanel;

            VerticalLayoutGroup vlg = shopObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Shop header with buttons
            GameObject headerObj = new GameObject("ShopHeader");
            headerObj.transform.SetParent(shopPanel.transform);
            RectTransform headerRT = headerObj.AddComponent<RectTransform>();
            headerRT.sizeDelta = new Vector2(0, 36);
            LayoutElement headerLE = headerObj.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 36;
            
            HorizontalLayoutGroup headerHLG = headerObj.AddComponent<HorizontalLayoutGroup>();
            headerHLG.spacing = 10;
            headerHLG.childAlignment = TextAnchor.MiddleLeft;
            headerHLG.childControlWidth = false;
            headerHLG.childControlHeight = true;

            Text shopLabel = CreateText("SHOP", headerRT, 70);
            shopLabel.fontStyle = FontStyle.Bold;
            shopLabel.fontSize = 16;

            // Spacer
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(headerRT.transform);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

            rerollButton = CreateButton("🔄 $2", headerRT, 75, OnRerollClicked);
            buyXPButton = CreateButton("📈 $4", headerRT, 75, OnBuyXPClicked);
            lockButton = CreateButton("🔓", headerRT, 45, OnLockClicked);

            // Shop slots container
            GameObject slotsObj = new GameObject("ShopSlots");
            slotsObj.transform.SetParent(shopPanel.transform);
            shopSlotContainer = slotsObj.AddComponent<RectTransform>();
            shopSlotContainer.sizeDelta = new Vector2(0, 120);
            
            HorizontalLayoutGroup slotsHLG = slotsObj.AddComponent<HorizontalLayoutGroup>();
            slotsHLG.spacing = 6;
            slotsHLG.childAlignment = TextAnchor.MiddleCenter;
            slotsHLG.childControlWidth = false;
            slotsHLG.childControlHeight = true;
            slotsHLG.childForceExpandWidth = false;

            LayoutElement slotsLE = slotsObj.AddComponent<LayoutElement>();
            slotsLE.preferredHeight = 120;

            // Create shop card slots
            for (int i = 0; i < GameConstants.Economy.SHOP_SIZE; i++)
            {
                var card = CreateUnitCard(shopSlotContainer, i, true);
                shopCards.Add(card);
            }
        }

        private void CreateBenchSection(Transform parent)
        {
            // Bench panel - positioned above shop, just below battlefield
            GameObject benchObj = CreatePanel("BenchPanel", parent);
            benchPanel = benchObj.GetComponent<RectTransform>();
            benchPanel.anchorMin = new Vector2(0, 0);
            benchPanel.anchorMax = new Vector2(1, 0);
            benchPanel.pivot = new Vector2(0.5f, 0);
            benchPanel.sizeDelta = new Vector2(0, 90);
            benchPanel.anchoredPosition = new Vector2(0, 220); // Above the shop
            benchObj.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.18f, 0.85f);

            HorizontalLayoutGroup hlg = benchObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.padding = new RectOffset(10, 10, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            // Bench slots - square like hexes
            benchSlotContainer = benchPanel;
            
            // Create bench card slots - square style
            for (int i = 0; i < GameConstants.Player.BENCH_SIZE; i++)
            {
                var card = CreateBenchSlot(benchPanel, i);
                benchCards.Add(card);
            }
        }
        
        private UnitCardUI CreateBenchSlot(Transform parent, int index)
        {
            // Square slot matching hex tile size
            GameObject slotObj = CreatePanel($"BenchSlot_{index}", parent);
            RectTransform rt = slotObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(72, 72);
            
            Image bg = slotObj.GetComponent<Image>();
            bg.color = new Color(0.2f, 0.25f, 0.3f, 0.9f);

            LayoutElement le = slotObj.AddComponent<LayoutElement>();
            le.preferredWidth = 72;
            le.preferredHeight = 72;

            // Unit sprite (fills most of the slot)
            GameObject spriteObj = new GameObject("Sprite");
            spriteObj.transform.SetParent(slotObj.transform);
            RectTransform spriteRT = spriteObj.AddComponent<RectTransform>();
            spriteRT.anchorMin = new Vector2(0.5f, 0.5f);
            spriteRT.anchorMax = new Vector2(0.5f, 0.5f);
            spriteRT.sizeDelta = new Vector2(56, 56);
            spriteRT.anchoredPosition = new Vector2(0, 2);
            Image spriteImg = spriteObj.AddComponent<Image>();
            spriteImg.preserveAspect = true;

            // Star indicator at bottom
            GameObject costObj = new GameObject("Cost");
            costObj.transform.SetParent(slotObj.transform);
            RectTransform costRT = costObj.AddComponent<RectTransform>();
            costRT.anchorMin = new Vector2(0, 0);
            costRT.anchorMax = new Vector2(1, 0);
            costRT.sizeDelta = new Vector2(0, 16);
            costRT.anchoredPosition = new Vector2(0, 2);
            Text costText = costObj.AddComponent<Text>();
            costText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            costText.fontSize = 12;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(1f, 0.9f, 0.4f);

            // No name text for bench (too small)
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(slotObj.transform);
            RectTransform nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.sizeDelta = Vector2.zero;
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize = 1;
            nameText.text = "";

            // Button
            Button btn = slotObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            int capturedIndex = index;
            btn.onClick.AddListener(() => OnBenchCardClicked(capturedIndex));

            UnitCardUI card = slotObj.AddComponent<UnitCardUI>();
            card.background = bg;
            card.spriteImage = spriteImg;
            card.costText = costText;
            card.nameText = nameText;
            card.button = btn;
            card.index = index;
            card.isBenchSlot = true;

            return card;
        }

        private void CreateSelectionPanel()
        {
            GameObject selObj = CreatePanel("SelectionPanel", mainCanvas.transform);
            selectionPanel = selObj.GetComponent<RectTransform>();
            selectionPanel.anchorMin = new Vector2(0.1f, 0.3f);
            selectionPanel.anchorMax = new Vector2(0.9f, 0.7f);
            selectionPanel.offsetMin = Vector2.zero;
            selectionPanel.offsetMax = Vector2.zero;
            selObj.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.98f);

            VerticalLayoutGroup vlg = selObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 15;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            selectionPanel.gameObject.SetActive(false);
        }

        private void CreateTooltip()
        {
            GameObject tooltipObj = CreatePanel("Tooltip", mainCanvas.transform);
            tooltipPanel = tooltipObj.GetComponent<RectTransform>();
            
            // Position on middle-right of screen
            tooltipPanel.anchorMin = new Vector2(1, 0.5f);
            tooltipPanel.anchorMax = new Vector2(1, 0.5f);
            tooltipPanel.pivot = new Vector2(1, 0.5f);
            tooltipPanel.anchoredPosition = new Vector2(-15, 50);
            
            Image tooltipBg = tooltipObj.GetComponent<Image>();
            tooltipBg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

            // Add outline
            Outline outline = tooltipObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.5f);
            outline.effectDistance = new Vector2(2, 2);

            // Use ContentSizeFitter to auto-size based on content
            ContentSizeFitter csf = tooltipObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Layout group directly on tooltip
            VerticalLayoutGroup vlg = tooltipObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Header row (sprite + name/cost)
            GameObject headerRow = new GameObject("HeaderRow");
            headerRow.transform.SetParent(tooltipObj.transform, false);
            RectTransform headerRT = headerRow.AddComponent<RectTransform>();
            LayoutElement headerLE = headerRow.AddComponent<LayoutElement>();
            headerLE.minHeight = 60;
            headerLE.preferredHeight = 60;
            headerLE.preferredWidth = 250;
            HorizontalLayoutGroup headerHLG = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerHLG.spacing = 10;
            headerHLG.childAlignment = TextAnchor.MiddleLeft;
            headerHLG.childControlWidth = true;
            headerHLG.childControlHeight = true;
            headerHLG.childForceExpandWidth = false;
            headerHLG.childForceExpandHeight = true;

            // Unit sprite container
            GameObject spriteContainer = CreatePanel("SpriteContainer", headerRow.transform);
            spriteContainer.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
            LayoutElement spriteLE = spriteContainer.AddComponent<LayoutElement>();
            spriteLE.minWidth = 56;
            spriteLE.minHeight = 56;
            spriteLE.preferredWidth = 56;
            spriteLE.preferredHeight = 56;
            
            tooltipSprite = new GameObject("TooltipSprite").AddComponent<Image>();
            tooltipSprite.transform.SetParent(spriteContainer.transform, false);
            RectTransform spriteRT = tooltipSprite.GetComponent<RectTransform>();
            spriteRT.anchorMin = Vector2.zero;
            spriteRT.anchorMax = Vector2.one;
            spriteRT.offsetMin = new Vector2(4, 4);
            spriteRT.offsetMax = new Vector2(-4, -4);
            tooltipSprite.preserveAspect = true;

            // Name and cost column
            GameObject nameCol = new GameObject("NameCol");
            nameCol.transform.SetParent(headerRow.transform, false);
            RectTransform nameColRT = nameCol.AddComponent<RectTransform>();
            LayoutElement nameColLE = nameCol.AddComponent<LayoutElement>();
            nameColLE.flexibleWidth = 1;
            nameColLE.minWidth = 150;
            VerticalLayoutGroup nameVLG = nameCol.AddComponent<VerticalLayoutGroup>();
            nameVLG.spacing = 2;
            nameVLG.childControlWidth = true;
            nameVLG.childControlHeight = true;
            nameVLG.childForceExpandWidth = true;
            nameVLG.childForceExpandHeight = false;

            // Title
            tooltipTitle = CreateTooltipText("Unit Name", nameCol.transform, 18, FontStyle.Bold, Color.white, 24);

            // Cost
            tooltipCost = CreateTooltipText("$0 ★★★", nameCol.transform, 14, FontStyle.Normal, new Color(1f, 0.85f, 0.3f), 20);

            // Divider 1
            CreateTooltipDivider(tooltipObj.transform);

            // Stats label
            CreateTooltipText("STATS", tooltipObj.transform, 10, FontStyle.Bold, new Color(0.55f, 0.55f, 0.65f), 14);

            // Stats text
            tooltipStats = CreateTooltipText("HP: 0  ATK: 0", tooltipObj.transform, 12, FontStyle.Normal, Color.white, 48);

            // Divider 2
            CreateTooltipDivider(tooltipObj.transform);

            // Traits label
            CreateTooltipText("TRAITS", tooltipObj.transform, 10, FontStyle.Bold, new Color(0.55f, 0.55f, 0.65f), 14);

            // Traits text
            tooltipTraits = CreateTooltipText("Trait1, Trait2", tooltipObj.transform, 12, FontStyle.Normal, new Color(0.5f, 0.85f, 0.5f), 18);

            // Divider 3
            CreateTooltipDivider(tooltipObj.transform);

            // Ability label
            CreateTooltipText("ABILITY", tooltipObj.transform, 10, FontStyle.Bold, new Color(0.55f, 0.55f, 0.65f), 14);

            // Ability text
            tooltipAbility = CreateTooltipText("Ability Name", tooltipObj.transform, 12, FontStyle.Normal, new Color(0.7f, 0.7f, 0.95f), 40);

            tooltipPanel.gameObject.SetActive(false);
        }

        private void CreateTooltipDivider(Transform parent)
        {
            GameObject divider = CreatePanel("Divider", parent);
            divider.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.45f);
            LayoutElement le = divider.AddComponent<LayoutElement>();
            le.minHeight = 1;
            le.preferredHeight = 1;
        }

        private Text CreateTooltipText(string content, Transform parent, int fontSize, FontStyle style, Color color, float height)
        {
            GameObject obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            
            Text text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            
            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.preferredWidth = 250;
            
            return text;
        }

        private void CreateTraitPanel()
        {
            // Panel on left side of screen - simple design
            GameObject panelObj = CreatePanel("TraitPanel", mainCanvas.transform);
            traitPanel = panelObj.GetComponent<RectTransform>();
            
            // Position on left side, anchored to top-left below top bar
            traitPanel.anchorMin = new Vector2(0, 1);
            traitPanel.anchorMax = new Vector2(0, 1);
            traitPanel.pivot = new Vector2(0, 1);
            traitPanel.sizeDelta = new Vector2(120, 300);
            traitPanel.anchoredPosition = new Vector2(2, -85);
            
            Image panelBg = panelObj.GetComponent<Image>();
            panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // Add outline for polish
            Outline outline = panelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.25f, 0.35f);
            outline.effectDistance = new Vector2(1, 1);

            // Header
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(panelObj.transform, false);
            RectTransform headerRT = headerObj.AddComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(0, 24);
            headerRT.anchoredPosition = Vector2.zero;

            Image headerBg = headerObj.AddComponent<Image>();
            headerBg.color = new Color(0.15f, 0.12f, 0.08f, 1f);

            GameObject headerTextObj = new GameObject("HeaderText");
            headerTextObj.transform.SetParent(headerObj.transform, false);
            RectTransform headerTextRT = headerTextObj.AddComponent<RectTransform>();
            headerTextRT.anchorMin = Vector2.zero;
            headerTextRT.anchorMax = Vector2.one;
            headerTextRT.offsetMin = new Vector2(8, 0);
            headerTextRT.offsetMax = new Vector2(-8, 0);
            Text headerText = headerTextObj.AddComponent<Text>();
            headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            headerText.text = "TRAITS";
            headerText.fontSize = 12;
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = new Color(0.9f, 0.8f, 0.5f);
            headerText.alignment = TextAnchor.MiddleLeft;

            // Content container - simple vertical layout below header
            GameObject contentObj = new GameObject("TraitContent");
            contentObj.transform.SetParent(panelObj.transform, false);
            traitContent = contentObj.AddComponent<RectTransform>();
            traitContent.anchorMin = new Vector2(0, 0);
            traitContent.anchorMax = new Vector2(1, 1);
            traitContent.offsetMin = new Vector2(4, 4);
            traitContent.offsetMax = new Vector2(-4, -28);

            VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.UpperLeft;

            // Start hidden - will show when traits are present
            traitPanel.gameObject.SetActive(false);
        }

        private void CreateTraitTooltip()
        {
            // Trait tooltip panel - appears when hovering trait entries
            GameObject tooltipObj = CreatePanel("TraitTooltip", mainCanvas.transform);
            traitTooltipPanel = tooltipObj.GetComponent<RectTransform>();
            
            // Position to the right of trait panel
            traitTooltipPanel.anchorMin = new Vector2(0, 1);
            traitTooltipPanel.anchorMax = new Vector2(0, 1);
            traitTooltipPanel.pivot = new Vector2(0, 1);
            traitTooltipPanel.anchoredPosition = new Vector2(118, -85);
            
            Image tooltipBg = tooltipObj.GetComponent<Image>();
            tooltipBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // Outline
            Outline outline = tooltipObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.45f, 0.3f);
            outline.effectDistance = new Vector2(1, 1);

            // Content size fitter
            ContentSizeFitter csf = tooltipObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Vertical layout
            VerticalLayoutGroup vlg = tooltipObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 10, 10);
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title - larger
            traitTooltipTitle = CreateTooltipText("Trait Name", tooltipObj.transform, 20, FontStyle.Bold, new Color(1f, 0.9f, 0.5f), 28);
            traitTooltipTitle.GetComponent<LayoutElement>().preferredWidth = 240;

            // Description
            traitTooltipDescription = CreateTooltipText("Description", tooltipObj.transform, 14, FontStyle.Italic, new Color(0.75f, 0.75f, 0.8f), 24);

            // Divider
            CreateTooltipDivider(tooltipObj.transform);

            // Tier bonuses label
            CreateTooltipText("TIER BONUSES", tooltipObj.transform, 12, FontStyle.Bold, new Color(0.6f, 0.6f, 0.7f), 18);

            // Tier container
            GameObject tierContainer = new GameObject("TierContainer");
            tierContainer.transform.SetParent(tooltipObj.transform, false);
            traitTooltipTierContainer = tierContainer.AddComponent<RectTransform>();
            VerticalLayoutGroup tierVlg = tierContainer.AddComponent<VerticalLayoutGroup>();
            tierVlg.spacing = 4;
            tierVlg.childControlWidth = true;
            tierVlg.childControlHeight = true;
            tierVlg.childForceExpandWidth = true;
            LayoutElement tierLE = tierContainer.AddComponent<LayoutElement>();
            tierLE.preferredWidth = 240;

            // Divider
            CreateTooltipDivider(tooltipObj.transform);

            // Units with this trait label
            CreateTooltipText("UNITS ON BOARD", tooltipObj.transform, 12, FontStyle.Bold, new Color(0.6f, 0.6f, 0.7f), 18);

            // Units list
            traitTooltipUnits = CreateTooltipText("None", tooltipObj.transform, 14, FontStyle.Normal, new Color(0.6f, 0.85f, 0.6f), 24);

            traitTooltipPanel.gameObject.SetActive(false);
        }

        private int lastTraitHash = 0;

        private void UpdateTraitPanel()
        {
            if (state == null || traitPanel == null || traitContent == null) return;
            if (state.activeTraits == null) 
            {
                traitPanel.gameObject.SetActive(false);
                return;
            }

            // Check if there are any units on the board
            bool hasUnitsOnBoard = false;
            if (state.playerBoard != null)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH && !hasUnitsOnBoard; x++)
                {
                    for (int y = 0; y < GameConstants.Grid.HEIGHT && !hasUnitsOnBoard; y++)
                    {
                        if (state.playerBoard[x, y] != null)
                        {
                            hasUnitsOnBoard = true;
                        }
                    }
                }
            }

            // Hide panel if no units on board
            if (!hasUnitsOnBoard || state.activeTraits.Count == 0)
            {
                traitPanel.gameObject.SetActive(false);
                lastTraitHash = 0;
                return;
            }

            // Show panel when we have units with traits
            traitPanel.gameObject.SetActive(true);

            // Calculate hash of current traits to detect changes
            int currentHash = 0;
            foreach (var kvp in state.activeTraits)
            {
                if (kvp.Key != null)
                {
                    currentHash ^= kvp.Key.GetHashCode() * 31 + kvp.Value;
                }
            }

            // Only rebuild if traits changed
            if (currentHash == lastTraitHash && traitEntries.Count > 0) return;
            lastTraitHash = currentHash;

            // Clear existing entries
            foreach (var entry in traitEntries)
            {
                if (entry != null) Destroy(entry);
            }
            traitEntries.Clear();
            traitEntryComponents.Clear();

            // Sort traits: active traits first (by tier, descending), then inactive by count
            var sortedTraits = new List<KeyValuePair<TraitData, int>>(state.activeTraits);
            sortedTraits.Sort((a, b) => {
                int tierA = a.Key != null ? a.Key.GetActiveTier(a.Value) : -1;
                int tierB = b.Key != null ? b.Key.GetActiveTier(b.Value) : -1;
                if (tierA != tierB) return tierB.CompareTo(tierA); // Higher tier first
                return b.Value.CompareTo(a.Value); // Then by count
            });

            foreach (var kvp in sortedTraits)
            {
                if (kvp.Key == null) continue;
                CreateTraitEntry(kvp.Key, kvp.Value);
            }
        }

        private void CreateTraitEntry(TraitData trait, int count)
        {
            // Create entry container
            GameObject entryObj = new GameObject("TraitEntry_" + trait.traitName);
            entryObj.transform.SetParent(traitContent, false);
            traitEntries.Add(entryObj);
            
            RectTransform rt = entryObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 34);
            
            Image bg = entryObj.AddComponent<Image>();
            
            LayoutElement le = entryObj.AddComponent<LayoutElement>();
            le.preferredHeight = 34;
            le.minHeight = 34;

            int activeTier = trait.GetActiveTier(count);
            bool isActive = activeTier >= 0;
            Color categoryColor = trait.GetTraitColor();
            
            // Background color
            if (isActive)
            {
                float tierIntensity = 0.3f + (activeTier * 0.15f);
                bg.color = new Color(
                    Mathf.Lerp(0.2f, categoryColor.r * 0.4f, 0.4f) + tierIntensity * 0.1f,
                    Mathf.Lerp(0.18f, categoryColor.g * 0.35f, 0.4f) + tierIntensity * 0.08f,
                    Mathf.Lerp(0.12f, categoryColor.b * 0.3f, 0.4f),
                    1f
                );
            }
            else
            {
                bg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            }

            // Add button for hover
            Button entryBtn = entryObj.AddComponent<Button>();
            entryBtn.transition = Selectable.Transition.None;
            entryBtn.targetGraphic = bg;
            
            // Add TraitEntryUI for hover detection
            TraitEntryUI entryUI = entryObj.AddComponent<TraitEntryUI>();
            entryUI.trait = trait;
            entryUI.count = count;
            traitEntryComponents.Add(entryUI);

            // === ICON (colored square with tier number) ===
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(entryObj.transform, false);
            RectTransform iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0);
            iconRT.anchorMax = new Vector2(0, 1);
            iconRT.pivot = new Vector2(0, 0.5f);
            iconRT.offsetMin = new Vector2(4, 4);
            iconRT.offsetMax = new Vector2(30, -4);
            
            Image iconImg = iconObj.AddComponent<Image>();
            if (isActive)
            {
                iconImg.color = new Color(
                    Mathf.Min(1f, categoryColor.r * 1.4f),
                    Mathf.Min(1f, categoryColor.g * 1.4f),
                    Mathf.Min(1f, categoryColor.b * 1.4f),
                    1f);
            }
            else
            {
                iconImg.color = new Color(0.4f, 0.4f, 0.45f, 1f);
            }

            // Tier number text inside icon
            GameObject tierObj = new GameObject("Tier");
            tierObj.transform.SetParent(iconObj.transform, false);
            RectTransform tierRT = tierObj.AddComponent<RectTransform>();
            tierRT.anchorMin = Vector2.zero;
            tierRT.anchorMax = Vector2.one;
            tierRT.offsetMin = Vector2.zero;
            tierRT.offsetMax = Vector2.zero;
            
            Text tierText = tierObj.AddComponent<Text>();
            tierText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            tierText.text = isActive ? (activeTier + 1).ToString() : "-";
            tierText.fontSize = 14;
            tierText.fontStyle = FontStyle.Bold;
            tierText.alignment = TextAnchor.MiddleCenter;
            tierText.color = isActive ? Color.white : new Color(0.65f, 0.65f, 0.7f);

            // === TRAIT NAME ===
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(entryObj.transform, false);
            RectTransform nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.5f);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.pivot = new Vector2(0, 0.5f);
            nameRT.offsetMin = new Vector2(34, 0);
            nameRT.offsetMax = new Vector2(-4, -2);
            
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.text = trait.traitName;
            nameText.fontSize = 11;
            nameText.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = isActive ? new Color(1f, 0.95f, 0.7f) : new Color(0.7f, 0.7f, 0.75f);

            // === PROGRESS TEXT ===
            string progressStr = "";
            if (trait.tierThresholds != null && trait.tierThresholds.Length > 0)
            {
                int nextThreshold = -1;
                for (int i = 0; i < trait.tierThresholds.Length; i++)
                {
                    if (count < trait.tierThresholds[i])
                    {
                        nextThreshold = trait.tierThresholds[i];
                        break;
                    }
                }
                progressStr = nextThreshold > 0 ? $"{count}/{nextThreshold}" : $"{count} MAX";
            }

            GameObject progressObj = new GameObject("Progress");
            progressObj.transform.SetParent(entryObj.transform, false);
            RectTransform progressRT = progressObj.AddComponent<RectTransform>();
            progressRT.anchorMin = new Vector2(0, 0);
            progressRT.anchorMax = new Vector2(1, 0.5f);
            progressRT.pivot = new Vector2(0, 0.5f);
            progressRT.offsetMin = new Vector2(34, 2);
            progressRT.offsetMax = new Vector2(-4, 0);
            
            Text progressText = progressObj.AddComponent<Text>();
            progressText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            progressText.text = progressStr;
            progressText.fontSize = 10;
            progressText.alignment = TextAnchor.MiddleLeft;
            progressText.color = isActive ? new Color(0.9f, 0.8f, 0.4f) : new Color(0.55f, 0.55f, 0.6f);
        }

        private void UpdateTraitTooltip()
        {
            if (traitTooltipPanel == null) return;

            // Check if mouse is over a trait entry
            bool isOverTrait = false;
            TraitData currentTrait = null;
            int currentCount = 0;

            foreach (var entry in traitEntryComponents)
            {
                if (entry != null && entry.IsHovered())
                {
                    isOverTrait = true;
                    currentTrait = entry.trait;
                    currentCount = entry.count;
                    break;
                }
            }

            if (!isOverTrait || currentTrait == null)
            {
                traitTooltipPanel.gameObject.SetActive(false);
                hoveredTrait = null;
                return;
            }

            // Show and update tooltip
            traitTooltipPanel.gameObject.SetActive(true);
            
            // Only rebuild if trait changed
            if (hoveredTrait != currentTrait)
            {
                hoveredTrait = currentTrait;
                PopulateTraitTooltip(currentTrait, currentCount);
            }
        }

        private void PopulateTraitTooltip(TraitData trait, int count)
        {
            if (trait == null) return;

            int activeTier = trait.GetActiveTier(count);

            // Title with category
            string categoryLabel = trait.category == TraitCategory.Origin ? "Origin" : "Class";
            traitTooltipTitle.text = $"{trait.traitName}";
            traitTooltipTitle.color = trait.GetTraitColor();

            // Description
            traitTooltipDescription.text = !string.IsNullOrEmpty(trait.description) ? 
                $"\"{trait.description}\"" : $"({categoryLabel})";

            // Clear old tier entries
            foreach (var entry in traitTooltipTierEntries)
            {
                if (entry != null) Destroy(entry);
            }
            traitTooltipTierEntries.Clear();

            // Create tier bonus entries
            if (trait.tierBonuses != null && trait.thresholds != null)
            {
                for (int i = 0; i < trait.tierBonuses.Length && i < trait.thresholds.Length; i++)
                {
                    var bonus = trait.tierBonuses[i];
                    int threshold = trait.thresholds[i];
                    bool isReached = count >= threshold;
                    bool isCurrent = (i == activeTier);

                    GameObject tierEntry = new GameObject($"Tier_{i}");
                    tierEntry.transform.SetParent(traitTooltipTierContainer, false);
                    traitTooltipTierEntries.Add(tierEntry);

                    RectTransform entryRT = tierEntry.AddComponent<RectTransform>();
                    HorizontalLayoutGroup entryHlg = tierEntry.AddComponent<HorizontalLayoutGroup>();
                    entryHlg.spacing = 10;
                    entryHlg.childControlWidth = false;
                    entryHlg.childControlHeight = true;
                    entryHlg.childForceExpandWidth = false;
                    entryHlg.childAlignment = TextAnchor.MiddleLeft;
                    LayoutElement entryLE = tierEntry.AddComponent<LayoutElement>();
                    entryLE.preferredHeight = 28;

                    // Threshold indicator
                    Text thresholdText = new GameObject("Threshold").AddComponent<Text>();
                    thresholdText.transform.SetParent(tierEntry.transform, false);
                    thresholdText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    thresholdText.text = $"({threshold})";
                    thresholdText.fontSize = 16;
                    thresholdText.fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal;
                    thresholdText.alignment = TextAnchor.MiddleLeft;
                    LayoutElement threshLE = thresholdText.gameObject.AddComponent<LayoutElement>();
                    threshLE.preferredWidth = 42;

                    if (isReached)
                    {
                        thresholdText.color = isCurrent ? new Color(1f, 0.85f, 0.3f) : new Color(0.8f, 0.7f, 0.4f);
                    }
                    else
                    {
                        thresholdText.color = new Color(0.5f, 0.5f, 0.55f);
                    }

                    // Bonus description
                    Text bonusText = new GameObject("Bonus").AddComponent<Text>();
                    bonusText.transform.SetParent(tierEntry.transform, false);
                    bonusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    bonusText.text = bonus?.bonusDescription ?? "Bonus";
                    bonusText.fontSize = 14;
                    bonusText.alignment = TextAnchor.MiddleLeft;
                    LayoutElement bonusLE = bonusText.gameObject.AddComponent<LayoutElement>();
                    bonusLE.flexibleWidth = 1;
                    bonusLE.preferredWidth = 220;

                    if (isReached)
                    {
                        bonusText.color = isCurrent ? Color.white : new Color(0.85f, 0.85f, 0.8f);
                        bonusText.fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal;
                    }
                    else
                    {
                        bonusText.color = new Color(0.5f, 0.5f, 0.55f);
                    }
                }
            }

            // Units with this trait on board
            var boardUnits = state.GetBoardUnits();
            List<string> unitNames = new List<string>();
            foreach (var unit in boardUnits)
            {
                if (unit != null && unit.HasTrait(trait))
                {
                    string stars = new string('★', unit.starLevel);
                    unitNames.Add($"{unit.template.unitName} {stars}");
                }
            }

            if (unitNames.Count > 0)
            {
                traitTooltipUnits.text = string.Join(", ", unitNames);
                traitTooltipUnits.color = new Color(0.6f, 0.85f, 0.6f);
            }
            else
            {
                traitTooltipUnits.text = "None on board";
                traitTooltipUnits.color = new Color(0.5f, 0.5f, 0.55f);
            }
        }

        public void ShowTraitTooltip(TraitData trait, int count)
        {
            if (traitTooltipPanel == null || trait == null) return;
            
            hoveredTrait = trait;
            traitTooltipPanel.gameObject.SetActive(true);
            PopulateTraitTooltip(trait, count);
        }

        public void HideTraitTooltip()
        {
            if (traitTooltipPanel != null)
            {
                traitTooltipPanel.gameObject.SetActive(false);
            }
            hoveredTrait = null;
        }

        private UnitCardUI CreateUnitCard(Transform parent, int index, bool isShopCard)
        {
            GameObject cardObj = CreatePanel($"Card_{index}", parent);
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(65, 110);
            
            Image bg = cardObj.GetComponent<Image>();
            bg.color = new Color(0.25f, 0.25f, 0.3f);

            LayoutElement le = cardObj.AddComponent<LayoutElement>();
            le.preferredWidth = 65;
            le.preferredHeight = 110;

            // Unit sprite
            GameObject spriteObj = new GameObject("Sprite");
            spriteObj.transform.SetParent(cardObj.transform);
            RectTransform spriteRT = spriteObj.AddComponent<RectTransform>();
            spriteRT.anchorMin = new Vector2(0.5f, 0.5f);
            spriteRT.anchorMax = new Vector2(0.5f, 0.5f);
            spriteRT.sizeDelta = new Vector2(48, 48);
            spriteRT.anchoredPosition = new Vector2(0, 5);
            Image spriteImg = spriteObj.AddComponent<Image>();
            spriteImg.preserveAspect = true;

            // Cost/Stars text
            GameObject costObj = new GameObject("Cost");
            costObj.transform.SetParent(cardObj.transform);
            RectTransform costRT = costObj.AddComponent<RectTransform>();
            costRT.anchorMin = new Vector2(0, 0);
            costRT.anchorMax = new Vector2(1, 0);
            costRT.sizeDelta = new Vector2(0, 18);
            costRT.anchoredPosition = new Vector2(0, 3);
            Text costText = costObj.AddComponent<Text>();
            costText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            costText.fontSize = 12;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = Color.white;

            // Name text
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(cardObj.transform);
            RectTransform nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 1);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.sizeDelta = new Vector2(0, 18);
            nameRT.anchoredPosition = new Vector2(0, -2);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize = 11;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;

            // Button
            Button btn = cardObj.AddComponent<Button>();
            int capturedIndex = index;
            if (isShopCard)
            {
                btn.onClick.AddListener(() => OnShopCardClicked(capturedIndex));
            }
            else
            {
                btn.onClick.AddListener(() => OnBenchCardClicked(capturedIndex));
            }

            UnitCardUI card = cardObj.AddComponent<UnitCardUI>();
            card.background = bg;
            card.spriteImage = spriteImg;
            card.costText = costText;
            card.nameText = nameText;
            card.button = btn;
            card.index = index;

            return card;
        }

        // ========== UI Updates ==========

        private void UpdateTopBar()
        {
            if (state.player == null) return;

            roundText.text = $"R{state.round.currentRound}";
            
            // Show lives as hearts for PvE Wave mode, or health number for other modes
            if (state.currentGameMode == GameMode.PvEWave)
            {
                string hearts = new string('♥', state.player.health);
                string emptyHearts = new string('♡', state.player.maxHealth - state.player.health);
                healthText.text = hearts + emptyHearts;
            }
            else
            {
                healthText.text = $"❤ {state.player.health}";
            }
            
            goldText.text = $"💰 {state.player.gold}";
            levelText.text = $"Lv{state.player.level}";

            if (state.round.phase == GamePhase.Planning)
            {
                timerText.text = $"{state.round.phaseTimer:F0}s";
                fightButton.gameObject.SetActive(true);
            }
            else
            {
                timerText.text = state.round.phase.ToString();
                fightButton.gameObject.SetActive(false);
            }
        }

        private void UpdatePhaseUI()
        {
            GamePhase currentPhase = state.round.phase;
            
            // Only rebuild selection UI when phase changes
            bool phaseChanged = currentPhase != lastShownPhase;
            
            switch (currentPhase)
            {
                case GamePhase.CrestSelect:
                    if (phaseChanged || !selectionShown)
                    {
                        ShowCrestSelection();
                        selectionShown = true;
                    }
                    shopPanel.gameObject.SetActive(false);
                    benchPanel.gameObject.SetActive(false);
                    break;

                case GamePhase.ItemSelect:
                    if (phaseChanged || !selectionShown)
                    {
                        ShowItemSelection();
                        selectionShown = true;
                    }
                    shopPanel.gameObject.SetActive(false);
                    benchPanel.gameObject.SetActive(false);
                    break;

                case GamePhase.Planning:
                    selectionPanel.gameObject.SetActive(false);
                    selectionShown = false;
                    shopPanel.gameObject.SetActive(true);
                    benchPanel.gameObject.SetActive(true);
                    UpdateShop();
                    UpdateBench();
                    UpdateShopButtons();
                    break;

                case GamePhase.Combat:
                case GamePhase.Results:
                    selectionPanel.gameObject.SetActive(false);
                    selectionShown = false;
                    shopPanel.gameObject.SetActive(true);
                    benchPanel.gameObject.SetActive(true);
                    break;

                case GamePhase.GameOver:
                    if (phaseChanged || !selectionShown)
                    {
                        ShowGameOver();
                        selectionShown = true;
                    }
                    break;
            }
            
            lastShownPhase = currentPhase;
        }

        private void UpdateShop()
        {
            if (state.shop == null || state.shop.availableUnits == null) return;

            for (int i = 0; i < shopCards.Count; i++)
            {
                if (i < state.shop.availableUnits.Count)
                {
                    var unit = state.shop.availableUnits[i];
                    shopCards[i].SetUnit(unit);
                    
                    // Grey out if can't afford
                    if (unit != null && unit.template != null)
                    {
                        bool canAfford = state.player.gold >= unit.template.cost;
                        shopCards[i].SetInteractable(canAfford);
                    }
                }
                else
                {
                    shopCards[i].SetUnit(null);
                }
            }
        }

        private void UpdateBench()
        {
            for (int i = 0; i < benchCards.Count; i++)
            {
                if (state.bench != null && i < state.bench.Count)
                {
                    benchCards[i].SetUnit(state.bench[i]);
                    benchCards[i].SetInteractable(true);
                }
                else
                {
                    benchCards[i].SetUnit(null);
                }
            }
        }

        private void UpdateShopButtons()
        {
            bool canReroll = state.player.gold >= GameConstants.Economy.REROLL_COST;
            bool canBuyXP = state.player.gold >= GameConstants.Economy.XP_COST && 
                           state.player.level < GameConstants.Leveling.MAX_LEVEL;

            rerollButton.interactable = canReroll;
            buyXPButton.interactable = canBuyXP;

            string lockIcon = state.shop.isLocked ? "🔒" : "🔓";
            lockButton.GetComponentInChildren<Text>().text = lockIcon;
        }

        private void ShowCrestSelection()
        {
            selectionPanel.gameObject.SetActive(true);
            ClearSelectionPanel();

            Text title = CreateText("SELECT A CREST", selectionPanel, 0);
            title.fontSize = 28;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.GetComponent<LayoutElement>().preferredHeight = 40;

            if (state.pendingCrestSelection == null) return;

            foreach (var crest in state.pendingCrestSelection)
            {
                if (crest == null) continue;
                CreateSelectionCard(crest.crestName, crest.description, 
                    GetCrestColor(crest.type), () => OnCrestSelected(crest));
            }
        }

        private void ShowItemSelection()
        {
            selectionPanel.gameObject.SetActive(true);
            ClearSelectionPanel();

            Text title = CreateText("SELECT AN ITEM", selectionPanel, 0);
            title.fontSize = 28;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.GetComponent<LayoutElement>().preferredHeight = 40;

            if (state.pendingItemSelection == null) return;

            foreach (var item in state.pendingItemSelection)
            {
                if (item == null) continue;
                CreateSelectionCard(item.itemName, item.description,
                    GetRarityColor(item.rarity), () => OnItemSelected(item));
            }
        }

        private void ShowGameOver()
        {
            selectionPanel.gameObject.SetActive(true);
            ClearSelectionPanel();

            bool victory = state.player.health > 0;
            
            Text title = CreateText(victory ? "🏆 VICTORY! 🏆" : "💀 DEFEAT 💀", selectionPanel, 0);
            title.fontSize = 36;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = victory ? new Color(1f, 0.85f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
            title.GetComponent<LayoutElement>().preferredHeight = 50;

            Text info = CreateText($"Made it to Round {state.round.currentRound}", selectionPanel, 0);
            info.fontSize = 20;
            info.alignment = TextAnchor.MiddleCenter;
            info.GetComponent<LayoutElement>().preferredHeight = 30;

            CreateSelectionCard("Play Again", "Start a new game", 
                new Color(0.3f, 0.6f, 0.3f), OnPlayAgainClicked);
        }

        private void CreateSelectionCard(string title, string desc, Color color, System.Action onClick)
        {
            GameObject cardObj = CreatePanel("SelectionCard", selectionPanel);
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            Image img = cardObj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            
            LayoutElement le = cardObj.AddComponent<LayoutElement>();
            le.preferredHeight = 80;

            VerticalLayoutGroup vlg = cardObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(15, 15, 10, 10);
            vlg.spacing = 5;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            Text titleText = CreateText(title, rt, 0);
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.GetComponent<LayoutElement>().preferredHeight = 28;
            titleText.raycastTarget = false;

            Text descText = CreateText(desc, rt, 0);
            descText.fontSize = 14;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.raycastTarget = false;

            Button btn = cardObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => pendingAction = onClick);
        }

        private void ClearSelectionPanel()
        {
            foreach (Transform child in selectionPanel)
            {
                Destroy(child.gameObject);
            }
        }

        private void UpdateTooltip()
        {
            // Hide tooltip during non-planning phases
            if (state.round.phase != GamePhase.Planning)
            {
                tooltipPanel.gameObject.SetActive(false);
            }
        }

        public void ShowTooltip(UnitInstance unit)
        {
            if (unit == null || unit.template == null)
            {
                tooltipPanel.gameObject.SetActive(false);
                return;
            }

            tooltipPanel.gameObject.SetActive(true);

            var t = unit.template;
            
            // Set sprite
            if (tooltipSprite != null)
            {
                tooltipSprite.sprite = UnitSpriteGenerator.GetSprite(t.unitId);
                tooltipSprite.enabled = true;
            }
            
            // Set name with cost color
            tooltipTitle.text = t.unitName;
            tooltipTitle.color = GetCostColor(t.cost);

            // Set cost and stars
            tooltipCost.text = $"${t.cost}  " + new string('★', unit.starLevel);

            // Set stats
            string stats = "";
            if (unit.currentStats != null)
            {
                stats = $"HP: {unit.currentStats.health}    ATK: {unit.currentStats.attack}\n";
                stats += $"Armor: {unit.currentStats.armor}    Magic Resist: {unit.currentStats.magicResist}\n";
                stats += $"Attack Speed: {unit.currentStats.attackSpeed:F2}    Range: {unit.currentStats.range}";
            }
            else
            {
                stats = $"HP: {t.baseStats.health}    ATK: {t.baseStats.attack}\n";
                stats += $"Armor: {t.baseStats.armor}    Magic Resist: {t.baseStats.magicResist}\n";
                stats += $"Attack Speed: {t.baseStats.attackSpeed:F2}    Range: {t.baseStats.range}";
            }
            tooltipStats.text = stats;

            // Set traits
            string traits = "";
            if (t.traits != null)
            {
                foreach (var trait in t.traits)
                {
                    if (trait != null)
                    {
                        if (traits.Length > 0) traits += ", ";
                        traits += trait.traitName;
                    }
                }
            }
            tooltipTraits.text = traits.Length > 0 ? traits : "None";

            // Set ability
            if (t.ability != null)
            {
                string abilityText = t.ability.abilityName;
                if (!string.IsNullOrEmpty(t.ability.description))
                    abilityText += $"\n{t.ability.description}";
                abilityText += $"\nMana: {t.baseStats.maxMana}";
                if (t.ability.baseDamage > 0)
                    abilityText += $"  |  Damage: {t.ability.baseDamage}";
                if (t.ability.baseHealing > 0)
                    abilityText += $"  |  Heal: {t.ability.baseHealing}";
                tooltipAbility.text = abilityText;
            }
            else
            {
                tooltipAbility.text = "None";
            }
        }

        /// <summary>
        /// Show tooltip and keep it pinned (for battlefield clicks)
        /// </summary>
        public void ShowTooltipPinned(UnitInstance unit)
        {
            isTooltipPinned = true;
            ShowTooltip(unit);
        }

        /// <summary>
        /// Hide pinned tooltip (when clicking elsewhere on battlefield)
        /// </summary>
        public void HideTooltipPinned()
        {
            isTooltipPinned = false;
            tooltipPanel.gameObject.SetActive(false);
        }

        public void HideTooltip()
        {
            // Don't hide if tooltip is pinned
            if (isTooltipPinned) return;
            tooltipPanel.gameObject.SetActive(false);
        }

        // ========== Layout ==========

        private void UpdateLayout()
        {
            if (isPortrait)
            {
                // Portrait mode - bench above shop
                shopPanel.sizeDelta = new Vector2(0, 180);
                shopPanel.anchoredPosition = new Vector2(0, 30); // Shop at bottom
                benchPanel.sizeDelta = new Vector2(0, 90);
                benchPanel.anchoredPosition = new Vector2(0, 220); // Bench above shop
                
                // Trait panel - compact, doesn't overlap battlefield
                if (traitPanel != null)
                {
                    traitPanel.sizeDelta = new Vector2(110, 280);
                    traitPanel.anchoredPosition = new Vector2(2, -85);
                }
                
                // Trait tooltip - position to the right of trait panel
                if (traitTooltipPanel != null)
                {
                    traitTooltipPanel.anchoredPosition = new Vector2(118, -85);
                }
            }
            else
            {
                // Landscape mode
                shopPanel.sizeDelta = new Vector2(0, 160);
                shopPanel.anchoredPosition = new Vector2(0, 20);
                benchPanel.sizeDelta = new Vector2(0, 85);
                benchPanel.anchoredPosition = new Vector2(0, 190);
                
                // Trait panel - slightly larger for landscape but still compact
                if (traitPanel != null)
                {
                    traitPanel.sizeDelta = new Vector2(120, 320);
                    traitPanel.anchoredPosition = new Vector2(2, -85);
                }
                
                // Trait tooltip - position to the right of trait panel
                if (traitTooltipPanel != null)
                {
                    traitTooltipPanel.anchoredPosition = new Vector2(128, -85);
                }
            }
        }

        // ========== Click Handlers ==========

        private void OnShopCardClicked(int index)
        {
            // Play UI click sound immediately
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            
            pendingAction = () => {
                bool success = state.BuyUnit(index);
                if (success)
                {
                    Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
                }
            };
        }

        private void OnBenchCardClicked(int index)
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            
            if (state.bench != null && index < state.bench.Count)
            {
                var unit = state.bench[index];
                pendingAction = () => TryPlaceUnit(unit);
            }
        }

        private void OnRerollClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            
            pendingAction = () => {
                state.RerollShop();
                Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
            };
        }

        private void OnBuyXPClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            
            pendingAction = () => {
                state.BuyXP();
                Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
            };
        }

        private void OnLockClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            
            if (state.shop != null)
            {
                state.shop.isLocked = !state.shop.isLocked;
            }
        }

        private void OnFightClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            
            pendingAction = () => RoundManager.Instance.StartCombatPhase();
        }

        private void OnCrestSelected(CrestData crest)
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            Crestforge.Visuals.AudioManager.Instance?.PlayLevelUp();
            
            RoundManager.Instance.OnCrestSelected(crest);
        }

        private void OnItemSelected(ItemData item)
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            Crestforge.Visuals.AudioManager.Instance?.PlayLevelUp();
            
            RoundManager.Instance.OnItemSelected(item);
        }

        private void OnPlayAgainClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            
            RoundManager.Instance.StartGame();
        }

        private void TryPlaceUnit(UnitInstance unit)
        {
            if (state.playerBoard == null) return;
            
            for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    if (state.playerBoard[x, y] == null)
                    {
                        if (state.PlaceUnit(unit, x, y)) return;
                    }
                }
            }
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

        private Text CreateText(string content, Transform parent, float width)
        {
            GameObject obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            LayoutElement le = obj.AddComponent<LayoutElement>();
            if (width > 0)
            {
                le.preferredWidth = width;
            }
            Text text = obj.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private Button CreateButton(string label, Transform parent, float width, System.Action onClick)
        {
            GameObject obj = CreatePanel("Button", parent);
            RectTransform rt = obj.GetComponent<RectTransform>();
            
            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredWidth = width;

            Image img = obj.GetComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.4f);

            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => pendingAction = onClick);

            Text text = CreateText(label, obj.transform, 0);
            text.alignment = TextAnchor.MiddleCenter;
            RectTransform textRT = text.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            return btn;
        }

        private Color GetCostColor(int cost)
        {
            return cost switch
            {
                1 => new Color(0.6f, 0.6f, 0.6f),
                2 => new Color(0.3f, 0.7f, 0.3f),
                3 => new Color(0.3f, 0.5f, 0.9f),
                4 => new Color(0.7f, 0.3f, 0.7f),
                _ => Color.white
            };
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.5f, 0.5f, 0.5f),
                ItemRarity.Uncommon => new Color(0.3f, 0.6f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.4f, 0.8f),
                _ => Color.white
            };
        }

        private Color GetCrestColor(CrestType type)
        {
            return type switch
            {
                CrestType.Minor => new Color(0.4f, 0.5f, 0.6f),
                CrestType.Major => new Color(0.6f, 0.4f, 0.2f),
                _ => Color.white
            };
        }
    }

    /// <summary>
    /// Component for unit cards in shop/bench
    /// </summary>
    public class UnitCardUI : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        public Image background;
        public Image spriteImage;
        public Text costText;
        public Text nameText;
        public Button button;
        public int index;
        public bool isBenchSlot = false;

        private UnitInstance currentUnit;
        private bool isClicking = false;
        private float clickSuppressionTime = 0f;

        public UnitInstance GetUnit() => currentUnit;

        public void SetUnit(UnitInstance unit)
        {
            currentUnit = unit;

            if (unit == null || unit.template == null)
            {
                background.color = new Color(0.2f, 0.25f, 0.3f, 0.5f);
                spriteImage.enabled = false;
                costText.text = "";
                if (nameText != null) nameText.text = "";
                button.interactable = false;
                return;
            }

            var t = unit.template;
            
            // Set color based on cost
            background.color = GetCostColor(t.cost);
            
            // Set sprite
            spriteImage.enabled = true;
            Sprite unitSprite = UnitSpriteGenerator.GetSprite(t.unitId);
            spriteImage.sprite = unitSprite;

            // Set text - bench shows only stars, shop shows cost + name
            if (isBenchSlot)
            {
                costText.text = new string('★', unit.starLevel);
            }
            else
            {
                costText.text = $"${t.cost} " + new string('★', unit.starLevel);
                if (nameText != null) nameText.text = t.unitName;
            }
            
            button.interactable = true;
        }

        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
            
            Color c = background.color;
            c.a = interactable ? 1f : 0.5f;
            background.color = c;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Don't show tooltip if clicking or recently clicked
            if (isClicking || Time.time - clickSuppressionTime < 0.15f)
                return;

            Crestforge.Visuals.AudioManager.Instance?.PlayUIHover();

            if (currentUnit != null && GameUI.Instance != null)
            {
                GameUI.Instance.ShowTooltip(currentUnit);
            }
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (GameUI.Instance != null)
            {
                GameUI.Instance.HideTooltip();
            }
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            isClicking = true;
            // Hide tooltip immediately when clicking
            if (GameUI.Instance != null)
            {
                GameUI.Instance.HideTooltip();
            }
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            isClicking = false;
            clickSuppressionTime = Time.time;
        }

        private Color GetCostColor(int cost)
        {
            return cost switch
            {
                1 => new Color(0.45f, 0.45f, 0.5f),
                2 => new Color(0.25f, 0.5f, 0.25f),
                3 => new Color(0.25f, 0.4f, 0.7f),
                4 => new Color(0.55f, 0.25f, 0.55f),
                _ => new Color(0.3f, 0.3f, 0.35f)
            };
        }
    }

    /// <summary>
    /// Component for trait entries in the trait panel - handles hover detection
    /// </summary>
    public class TraitEntryUI : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public TraitData trait;
        public int count;
        
        private bool isHovered = false;
        private Image backgroundImage;
        private Color originalColor;

        private void Awake()
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                originalColor = backgroundImage.color;
            }
        }

        public bool IsHovered()
        {
            return isHovered;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            isHovered = true;
            Crestforge.Visuals.AudioManager.Instance?.PlayUIHover();
            
            // Highlight effect
            if (backgroundImage != null)
            {
                Color highlightColor = originalColor;
                highlightColor.r = Mathf.Min(1f, highlightColor.r + 0.1f);
                highlightColor.g = Mathf.Min(1f, highlightColor.g + 0.1f);
                highlightColor.b = Mathf.Min(1f, highlightColor.b + 0.05f);
                backgroundImage.color = highlightColor;
            }
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            isHovered = false;
            
            // Restore original color
            if (backgroundImage != null)
            {
                backgroundImage.color = originalColor;
            }
        }

        public void SetOriginalColor(Color color)
        {
            originalColor = color;
            if (backgroundImage != null && !isHovered)
            {
                backgroundImage.color = color;
            }
        }
    }
}