using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Systems;
using Crestforge.Data;
using Crestforge.Combat;
using Crestforge.Networking;

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
        [System.NonSerialized] public RectTransform benchPanel; // Bench UI disabled - using 3D bench
        public RectTransform infoPanel;
        public RectTransform selectionPanel;

        [Header("Top Bar Elements")]
        public Text healthText;
        public Text goldText;
        public Text levelText;
        public Text roundText;
        public Text timerText;
        public Button fightButton;
        public Button scoutButton;
        public Button progressButton;
        public Button myBoardButton;
        public Text opponentInfoText;

        [Header("Shop Elements")]
        public RectTransform shopSlotContainer;
        public Button rerollButton;
        public Button buyXPButton;
        public Button lockButton;
        public Text rerollCostText;
        public Text xpCostText;
        public Text xpProgressText;

        [Header("Bench Elements (Disabled - using 3D bench)")]
        [System.NonSerialized] public RectTransform benchSlotContainer;

        [Header("Item Inventory")]
        public RectTransform itemInventoryPanel;
        public RectTransform itemSlotContainer;

        [Header("Crest Display")]
        public RectTransform crestPanel;
        public CrestSlotUI majorCrestSlot;
        public CrestSlotUI[] minorCrestSlots = new CrestSlotUI[3];

        [Header("Game End Screen")]
        public RectTransform gameEndPanel;
        public Text gameEndTitle;
        public Text gameEndSubtitle;
        public Button exitToMenuButton;

        [Header("Trait Panel")]
        public RectTransform traitPanel;
        public RectTransform traitContent;

        [Header("Trait Tooltip")]
        public RectTransform traitTooltipPanel;
        public Text traitTooltipTitle;
        public Text traitTooltipDescription;
        public RectTransform traitTooltipTierContainer;
        public Text traitTooltipUnits;

        [Header("Crest Tooltip")]
        public RectTransform crestTooltipPanel;
        public Text crestTooltipTitle;
        public Text crestTooltipType;
        public Text crestTooltipDescription;
        public Text crestTooltipBonus;

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
        public RectTransform tooltipItemContainer;
        public Text tooltipItemsLabel;

        // Runtime
        private GameState state;
        private List<UnitCardUI> shopCards = new List<UnitCardUI>();
        private List<UnitCardUI> benchCards = new List<UnitCardUI>();
        private List<ItemSlotUI> itemSlots = new List<ItemSlotUI>();
        private List<GameObject> traitEntries = new List<GameObject>();
        private List<TraitEntryUI> traitEntryComponents = new List<TraitEntryUI>();
        private System.Action pendingAction;
        private bool isPortrait;
        private GamePhase lastShownPhase = GamePhase.Planning;
        private bool selectionShown = false;
        private bool isInitialized = false;
        private bool isTooltipPinned = false;
        private bool isCrestTooltipPinned = false;
        private TraitData hoveredTrait = null;
        private List<GameObject> traitTooltipTierEntries = new List<GameObject>();
        private UnitInstance tooltipUnit = null;
        private List<TooltipItemSlot> tooltipItemSlots = new List<TooltipItemSlot>();
        private bool showingTemporaryItemInfo = false;

        // Sell overlay
        private GameObject sellOverlay;
        private Text sellText;
        private UnitInstance unitBeingDragged;
        private bool isSellModeActive = false;

        // Multiplayer helper
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

            // Subscribe to server action results for multiplayer feedback
            if (ServerGameState.Instance != null)
            {
                ServerGameState.Instance.OnActionResult += HandleActionResult;
                ServerGameState.Instance.OnGameEnded += HandleGameEnded;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from server events
            if (ServerGameState.Instance != null)
            {
                ServerGameState.Instance.OnActionResult -= HandleActionResult;
                ServerGameState.Instance.OnGameEnded -= HandleGameEnded;
            }
        }

        private void HandleActionResult(string action, bool success)
        {
            if (!success)
            {
                // Show feedback for failed actions
                ShowActionFeedback($"Cannot {action}");
            }
        }

        private Coroutine feedbackCoroutine;
        private Text feedbackText;

        private void ShowActionFeedback(string message)
        {
            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }

            // Create feedback text if needed
            if (feedbackText == null && mainCanvas != null)
            {
                GameObject feedbackObj = new GameObject("ActionFeedback");
                feedbackObj.transform.SetParent(mainCanvas.transform, false);
                RectTransform rt = feedbackObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.7f);
                rt.anchorMax = new Vector2(0.5f, 0.7f);
                rt.sizeDelta = new Vector2(300, 40);

                feedbackText = feedbackObj.AddComponent<Text>();
                feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                feedbackText.fontSize = 18;
                feedbackText.alignment = TextAnchor.MiddleCenter;
                feedbackText.color = new Color(1f, 0.4f, 0.4f);

                Outline outline = feedbackObj.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1, 1);
            }

            if (feedbackText != null)
            {
                feedbackText.text = message;
                feedbackText.gameObject.SetActive(true);
                feedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay(2f));
            }
        }

        private System.Collections.IEnumerator HideFeedbackAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
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
            // In multiplayer mode, we can run without local GameState
            if (!IsMultiplayer)
            {
                if (state == null)
                {
                    state = GameState.Instance;
                    if (state == null) return;
                }
            }

            // Execute pending actions
            if (pendingAction != null)
            {
                var action = pendingAction;
                pendingAction = null;
                action.Invoke();
            }

            // Hide tooltip when clicking anywhere (except on unit cards, which handle their own clicks)
            bool itemTooltipActive = tooltipPanel != null && tooltipPanel.gameObject.activeSelf;
            bool crestTooltipActive = crestTooltipPanel != null && crestTooltipPanel.gameObject.activeSelf;
            if (Input.GetMouseButtonDown(0) && (itemTooltipActive || crestTooltipActive))
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

                    // Check if any hit object is a UnitCardUI, ItemSlotUI, CrestSlotUI, TooltipItemSlot, or the tooltip itself
                    bool clickedOnTooltipSource = false;
                    foreach (var result in results)
                    {
                        if (result.gameObject.GetComponentInParent<UnitCardUI>() != null ||
                            result.gameObject.GetComponentInParent<ItemSlotUI>() != null ||
                            result.gameObject.GetComponentInParent<CrestSlotUI>() != null ||
                            result.gameObject.GetComponentInParent<TooltipItemSlot>() != null ||
                            (tooltipPanel != null && result.gameObject.transform.IsChildOf(tooltipPanel.transform)))
                        {
                            clickedOnTooltipSource = true;
                            break;
                        }
                    }

                    if (!clickedOnTooltipSource)
                    {
                        isTooltipPinned = false;
                        isCrestTooltipPinned = false;
                        showingTemporaryItemInfo = false;
                        tooltipUnit = null;
                        HideTooltip();
                        HideCrestTooltip();
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
            UpdateCrestDisplay();
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
            CreateCrestPanel();
            CreateGameEndPanel();
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

            // Scout button (PvP mode only)
            scoutButton = CreateButton("👁 SCOUT", topBar, 100, OnScoutClicked);
            scoutButton.GetComponent<Image>().color = new Color(0.4f, 0.5f, 0.6f);
            scoutButton.gameObject.SetActive(false); // Hidden by default

            // Progress button (PvP mode only)
            progressButton = CreateButton("📊", topBar, 50, OnProgressClicked);
            progressButton.GetComponent<Image>().color = new Color(0.5f, 0.4f, 0.6f);
            progressButton.gameObject.SetActive(false); // Hidden by default

            // Fight button
            fightButton = CreateButton("⚔ FIGHT", topBar, 120, OnFightClicked);
            fightButton.GetComponent<Image>().color = new Color(0.3f, 0.7f, 0.3f);

            // My Board button (shown during combat when viewing opponent's board)
            myBoardButton = CreateButton("MY BOARD", topBar, 100, OnMyBoardClicked);
            myBoardButton.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f);
            myBoardButton.gameObject.SetActive(false);

            // Opponent info text (shown during PvP combat)
            opponentInfoText = CreateText("", topBar, 150);
            opponentInfoText.alignment = TextAnchor.MiddleRight;
            opponentInfoText.color = new Color(0.9f, 0.7f, 0.7f);
            opponentInfoText.gameObject.SetActive(false);
        }

        private void CreateBottomPanel()
        {
            // Bench section - now handled by 3D visuals in BoardManager3D
            // CreateBenchSection(mainCanvas.transform);

            // Item inventory - to the right of bench
            CreateItemInventory(mainCanvas.transform);

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

            // Create sell overlay as sibling (not child) so it's visible even when shop is hidden
            CreateSellOverlay(mainCanvas.transform);
        }

        private void CreateSellOverlay(Transform canvasParent)
        {
            // Sell overlay is a separate panel at the bottom (same position as shop)
            sellOverlay = new GameObject("SellOverlay");
            sellOverlay.transform.SetParent(canvasParent, false);

            RectTransform rt = sellOverlay.AddComponent<RectTransform>();
            // Same anchoring as shop panel - bottom of screen
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, 180);
            rt.anchoredPosition = new Vector2(0, 30);

            // Semi-transparent red/gold background
            Image bg = sellOverlay.AddComponent<Image>();
            bg.color = new Color(0.6f, 0.2f, 0.1f, 0.95f);
            bg.raycastTarget = true;

            // Add drop handler for selling
            SellDropZone dropZone = sellOverlay.AddComponent<SellDropZone>();

            // Sell text in center
            GameObject textObj = new GameObject("SellText");
            textObj.transform.SetParent(sellOverlay.transform, false);

            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            sellText = textObj.AddComponent<Text>();
            sellText.text = "Sell Unit for $0";
            sellText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sellText.fontSize = 28;
            sellText.fontStyle = FontStyle.Bold;
            sellText.alignment = TextAnchor.MiddleCenter;
            sellText.color = new Color(1f, 0.9f, 0.4f);

            // Add outline for visibility
            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(2, -2);

            sellOverlay.SetActive(false);
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
            costText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            costText.fontSize = 12;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(1f, 0.9f, 0.4f);

            // No name text for bench (too small)
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(slotObj.transform);
            RectTransform nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.sizeDelta = Vector2.zero;
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        private void CreateItemInventory(Transform parent)
        {
            // Item inventory panel - positioned to the right of bench
            GameObject itemObj = CreatePanel("ItemInventoryPanel", parent);
            itemInventoryPanel = itemObj.GetComponent<RectTransform>();
            itemInventoryPanel.anchorMin = new Vector2(1, 0);
            itemInventoryPanel.anchorMax = new Vector2(1, 0);
            itemInventoryPanel.pivot = new Vector2(1, 0);
            itemInventoryPanel.sizeDelta = new Vector2(200, 90);
            itemInventoryPanel.anchoredPosition = new Vector2(-10, 320); // Above the bench
            itemObj.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.18f, 0.85f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(itemObj.transform, false);
            RectTransform titleRT = titleObj.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.sizeDelta = new Vector2(0, 18);
            titleRT.anchoredPosition = Vector2.zero;
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Items";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 12;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.8f, 0.7f, 0.9f);

            // Item slot container
            GameObject containerObj = new GameObject("ItemSlotContainer");
            containerObj.transform.SetParent(itemObj.transform, false);
            itemSlotContainer = containerObj.AddComponent<RectTransform>();
            itemSlotContainer.anchorMin = new Vector2(0, 0);
            itemSlotContainer.anchorMax = new Vector2(1, 1);
            itemSlotContainer.offsetMin = new Vector2(5, 5);
            itemSlotContainer.offsetMax = new Vector2(-5, -20);

            HorizontalLayoutGroup hlg = containerObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(2, 2, 2, 2);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;

            // Create item slots (max 10 items)
            for (int i = 0; i < 10; i++)
            {
                var slot = ItemSlotUI.Create(itemSlotContainer, new Vector2(60, 60));
                slot.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;
                itemSlots.Add(slot);
            }
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

            // Items section (hidden when no items)
            CreateTooltipDivider(tooltipObj.transform);

            // Items label
            tooltipItemsLabel = CreateTooltipText("ITEMS", tooltipObj.transform, 10, FontStyle.Bold, new Color(0.55f, 0.55f, 0.65f), 14);

            // Items container - horizontal layout for item slots
            GameObject itemContainer = new GameObject("ItemContainer");
            itemContainer.transform.SetParent(tooltipObj.transform, false);
            tooltipItemContainer = itemContainer.AddComponent<RectTransform>();
            HorizontalLayoutGroup itemHLG = itemContainer.AddComponent<HorizontalLayoutGroup>();
            itemHLG.spacing = 6;
            itemHLG.childAlignment = TextAnchor.MiddleLeft;
            itemHLG.childControlWidth = false;
            itemHLG.childControlHeight = false;
            itemHLG.childForceExpandWidth = false;
            itemHLG.childForceExpandHeight = false;
            LayoutElement itemContainerLE = itemContainer.AddComponent<LayoutElement>();
            itemContainerLE.minHeight = 40;
            itemContainerLE.preferredHeight = 40;
            itemContainerLE.preferredWidth = 250;

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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        private void CreateCrestPanel()
        {
            // Crest panel - positioned on the left side, below traits panel
            GameObject crestObj = CreatePanel("CrestPanel", mainCanvas.transform);
            crestPanel = crestObj.GetComponent<RectTransform>();

            // Position on left side, below trait panel
            crestPanel.anchorMin = new Vector2(0, 1);
            crestPanel.anchorMax = new Vector2(0, 1);
            crestPanel.pivot = new Vector2(0, 1);
            crestPanel.anchoredPosition = new Vector2(10, -400);
            crestPanel.sizeDelta = new Vector2(105, 250); // Increased height for 1 major + 3 minor slots

            Image panelBg = crestObj.GetComponent<Image>();
            panelBg.color = new Color(0.12f, 0.12f, 0.18f, 0.9f);

            // Add vertical layout
            VerticalLayoutGroup vlg = crestObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 4;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            // Header
            Text header = CreateTooltipText("CRESTS", crestObj.transform, 10, FontStyle.Bold, new Color(0.7f, 0.6f, 0.9f), 14);
            header.alignment = TextAnchor.MiddleCenter;

            // Major crest slot (on top)
            majorCrestSlot = CrestSlotUI.Create(crestObj.transform, new Vector2(90, 50), "Major");

            // Minor crest slots (up to 3)
            for (int i = 0; i < 3; i++)
            {
                minorCrestSlots[i] = CrestSlotUI.Create(crestObj.transform, new Vector2(90, 50), "Minor");
                minorCrestSlots[i].SetCrest(null); // Hide initially
            }

            // Initialize with no crests - this will hide the slots
            majorCrestSlot.SetCrest(null);

            // Hide the entire panel initially (will show when a crest is acquired)
            crestPanel.gameObject.SetActive(false);

            // Create crest tooltip
            CreateCrestTooltip();
        }

        private void CreateCrestTooltip()
        {
            // Crest tooltip panel - appears when hovering crest slot
            GameObject tooltipObj = CreatePanel("CrestTooltip", mainCanvas.transform);
            crestTooltipPanel = tooltipObj.GetComponent<RectTransform>();

            // Position to the right of crest panel
            crestTooltipPanel.anchorMin = new Vector2(0, 1);
            crestTooltipPanel.anchorMax = new Vector2(0, 1);
            crestTooltipPanel.pivot = new Vector2(0, 1);
            crestTooltipPanel.anchoredPosition = new Vector2(120, -400);

            Image tooltipBg = tooltipObj.GetComponent<Image>();
            tooltipBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // Outline
            Outline outline = tooltipObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.6f, 0.5f, 0.8f);
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

            // Title
            crestTooltipTitle = CreateTooltipText("Crest Name", tooltipObj.transform, 18, FontStyle.Bold, new Color(0.8f, 0.6f, 1f), 26);
            crestTooltipTitle.GetComponent<LayoutElement>().preferredWidth = 220;

            // Type
            crestTooltipType = CreateTooltipText("Minor Crest", tooltipObj.transform, 12, FontStyle.Italic, new Color(0.6f, 0.6f, 0.7f), 18);

            // Divider
            CreateTooltipDivider(tooltipObj.transform);

            // Description
            crestTooltipDescription = CreateTooltipText("Description", tooltipObj.transform, 14, FontStyle.Normal, new Color(0.85f, 0.85f, 0.9f), 24);

            // Divider
            CreateTooltipDivider(tooltipObj.transform);

            // Bonus label
            CreateTooltipText("BONUS", tooltipObj.transform, 11, FontStyle.Bold, new Color(0.6f, 0.6f, 0.7f), 16);

            // Bonus text
            crestTooltipBonus = CreateTooltipText("Bonus effect", tooltipObj.transform, 13, FontStyle.Normal, new Color(0.7f, 0.9f, 0.7f), 20);

            crestTooltipPanel.gameObject.SetActive(false);
        }

        private void CreateGameEndPanel()
        {
            // Full-screen overlay for game end
            GameObject panelObj = CreatePanel("GameEndPanel", mainCanvas.transform);
            gameEndPanel = panelObj.GetComponent<RectTransform>();

            // Full screen
            gameEndPanel.anchorMin = Vector2.zero;
            gameEndPanel.anchorMax = Vector2.one;
            gameEndPanel.offsetMin = Vector2.zero;
            gameEndPanel.offsetMax = Vector2.zero;

            // Semi-transparent dark background
            Image bg = panelObj.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

            // Content container (centered)
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(gameEndPanel, false);
            RectTransform contentRT = contentObj.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0.5f, 0.5f);
            contentRT.anchorMax = new Vector2(0.5f, 0.5f);
            contentRT.pivot = new Vector2(0.5f, 0.5f);
            contentRT.sizeDelta = new Vector2(400, 300);

            // Background for content
            Image contentBg = contentObj.AddComponent<Image>();
            contentBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            // Outline
            Outline outline = contentObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.4f, 0.7f);
            outline.effectDistance = new Vector2(2, 2);

            // Vertical layout for content
            VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(30, 30, 40, 30);
            vlg.spacing = 20;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            // Title (Victory! / Defeat)
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(contentObj.transform, false);
            gameEndTitle = titleObj.AddComponent<Text>();
            gameEndTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gameEndTitle.fontSize = 48;
            gameEndTitle.fontStyle = FontStyle.Bold;
            gameEndTitle.alignment = TextAnchor.MiddleCenter;
            gameEndTitle.text = "VICTORY!";
            gameEndTitle.color = new Color(1f, 0.85f, 0.3f);
            LayoutElement titleLE = titleObj.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 60;

            // Subtitle (winner name)
            GameObject subtitleObj = new GameObject("Subtitle");
            subtitleObj.transform.SetParent(contentObj.transform, false);
            gameEndSubtitle = subtitleObj.AddComponent<Text>();
            gameEndSubtitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gameEndSubtitle.fontSize = 20;
            gameEndSubtitle.alignment = TextAnchor.MiddleCenter;
            gameEndSubtitle.text = "Winner: Player Name";
            gameEndSubtitle.color = new Color(0.8f, 0.8f, 0.9f);
            LayoutElement subtitleLE = subtitleObj.AddComponent<LayoutElement>();
            subtitleLE.preferredHeight = 30;

            // Spacer
            GameObject spacerObj = new GameObject("Spacer");
            spacerObj.transform.SetParent(contentObj.transform, false);
            spacerObj.AddComponent<RectTransform>();
            LayoutElement spacerLE = spacerObj.AddComponent<LayoutElement>();
            spacerLE.preferredHeight = 20;

            // Exit button
            GameObject buttonObj = new GameObject("ExitButton");
            buttonObj.transform.SetParent(contentObj.transform, false);
            Image buttonBg = buttonObj.AddComponent<Image>();
            buttonBg.color = new Color(0.3f, 0.25f, 0.5f);
            exitToMenuButton = buttonObj.AddComponent<Button>();
            exitToMenuButton.targetGraphic = buttonBg;

            // Button hover colors
            ColorBlock colors = exitToMenuButton.colors;
            colors.highlightedColor = new Color(0.4f, 0.35f, 0.6f);
            colors.pressedColor = new Color(0.25f, 0.2f, 0.4f);
            exitToMenuButton.colors = colors;

            LayoutElement buttonLE = buttonObj.AddComponent<LayoutElement>();
            buttonLE.preferredHeight = 50;
            buttonLE.preferredWidth = 200;

            // Button text
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            Text buttonText = buttonTextObj.AddComponent<Text>();
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 22;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.text = "Exit to Menu";
            buttonText.color = Color.white;
            RectTransform buttonTextRT = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRT.anchorMin = Vector2.zero;
            buttonTextRT.anchorMax = Vector2.one;
            buttonTextRT.offsetMin = Vector2.zero;
            buttonTextRT.offsetMax = Vector2.zero;

            // Button click handler
            exitToMenuButton.onClick.AddListener(OnExitToMenuClicked);

            // Hide panel initially
            gameEndPanel.gameObject.SetActive(false);
        }

        private void HandleGameEnded(string winnerId, string winnerName)
        {
            if (gameEndPanel == null) return;

            // Determine if local player won
            bool isWinner = false;
            if (serverState != null)
            {
                isWinner = winnerId == serverState.localPlayerId;
            }

            // Set title based on win/loss
            if (isWinner)
            {
                gameEndTitle.text = "VICTORY!";
                gameEndTitle.color = new Color(1f, 0.85f, 0.3f); // Gold
            }
            else
            {
                gameEndTitle.text = "DEFEAT";
                gameEndTitle.color = new Color(0.8f, 0.3f, 0.3f); // Red
            }

            // Set subtitle
            gameEndSubtitle.text = $"Winner: {winnerName}";

            // Show the panel
            gameEndPanel.gameObject.SetActive(true);
        }

        private void OnExitToMenuClicked()
        {
            // Leave the room
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.LeaveRoom();
            }

            // Reset server game state
            if (serverState != null)
            {
                serverState.ResetState();
            }

            // Hide game end panel
            if (gameEndPanel != null)
            {
                gameEndPanel.gameObject.SetActive(false);
            }

            // Hide game UI
            gameObject.SetActive(false);

            // Show main menu
            if (MainMenuUI.Instance != null)
            {
                MainMenuUI.Instance.Show();
            }
        }

        private int lastTraitHash = 0;

        private void UpdateTraitPanel()
        {
            if (traitPanel == null || traitContent == null) return;

            // Build trait list based on mode
            var traitsToDisplay = new List<KeyValuePair<TraitData, int>>();

            if (IsMultiplayer)
            {
                // Multiplayer: use server's active traits
                if (serverState == null || serverState.activeTraits == null || serverState.activeTraits.Count == 0)
                {
                    traitPanel.gameObject.SetActive(false);
                    lastTraitHash = 0;
                    return;
                }

                // Convert server trait entries to TraitData + count
                foreach (var serverTrait in serverState.activeTraits)
                {
                    if (string.IsNullOrEmpty(serverTrait.traitId)) continue;

                    // Look up TraitData by traitId
                    TraitData traitData = FindTraitDataById(serverTrait.traitId);
                    if (traitData != null)
                    {
                        traitsToDisplay.Add(new KeyValuePair<TraitData, int>(traitData, serverTrait.count));
                    }
                }
            }
            else
            {
                // Single player: use local state
                if (state == null || state.activeTraits == null)
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

                // Copy from local state
                traitsToDisplay = new List<KeyValuePair<TraitData, int>>(state.activeTraits);
            }

            // Hide if no traits
            if (traitsToDisplay.Count == 0)
            {
                traitPanel.gameObject.SetActive(false);
                lastTraitHash = 0;
                return;
            }

            // Show panel when we have units with traits
            traitPanel.gameObject.SetActive(true);

            // Calculate hash of current traits to detect changes
            int currentHash = 0;
            foreach (var kvp in traitsToDisplay)
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
            traitsToDisplay.Sort((a, b) => {
                int tierA = a.Key != null ? a.Key.GetActiveTier(a.Value) : -1;
                int tierB = b.Key != null ? b.Key.GetActiveTier(b.Value) : -1;
                if (tierA != tierB) return tierB.CompareTo(tierA); // Higher tier first
                return b.Value.CompareTo(a.Value); // Then by count
            });

            foreach (var kvp in traitsToDisplay)
            {
                if (kvp.Key == null) continue;
                CreateTraitEntry(kvp.Key, kvp.Value);
            }
        }

        // Cache for dynamically loaded traits
        private static Dictionary<string, TraitData> traitCache = new Dictionary<string, TraitData>();
        private static bool traitsLoaded = false;

        /// <summary>
        /// Find a TraitData ScriptableObject by its traitId
        /// </summary>
        private TraitData FindTraitDataById(string traitId)
        {
            // Check cache first
            if (traitCache.TryGetValue(traitId, out TraitData cached))
            {
                return cached;
            }

            // Load all traits from Resources if not done yet
            if (!traitsLoaded)
            {
                LoadAllTraits();
            }

            // Try cache again after loading
            if (traitCache.TryGetValue(traitId, out cached))
            {
                return cached;
            }

            // Fallback to state.allTraits
            if (state != null && state.allTraits != null)
            {
                foreach (var trait in state.allTraits)
                {
                    if (trait != null && trait.traitId == traitId)
                    {
                        traitCache[traitId] = trait;
                        return trait;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Load all TraitData from Resources folders
        /// </summary>
        private void LoadAllTraits()
        {
            traitsLoaded = true;

            // Load from NewTraits folder (priority)
            var newTraits = Resources.LoadAll<TraitData>("ScriptableObjects/NewTraits");
            foreach (var trait in newTraits)
            {
                if (trait != null && !string.IsNullOrEmpty(trait.traitId))
                {
                    traitCache[trait.traitId] = trait;
                }
            }

            // Also load from old Traits folder as fallback
            var oldTraits = Resources.LoadAll<TraitData>("ScriptableObjects/Traits");
            foreach (var trait in oldTraits)
            {
                if (trait != null && !string.IsNullOrEmpty(trait.traitId))
                {
                    // Don't overwrite if already loaded from NewTraits
                    if (!traitCache.ContainsKey(trait.traitId))
                    {
                        traitCache[trait.traitId] = trait;
                    }
                }
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
            tierText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

            // Title with type
            string typeLabel = trait.isUnique ? "Unique" : "Shared";
            traitTooltipTitle.text = $"{trait.traitName}";
            traitTooltipTitle.color = trait.GetTraitColor();

            // Description
            traitTooltipDescription.text = !string.IsNullOrEmpty(trait.description) ?
                $"\"{trait.description}\"" : $"({typeLabel})";

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
                    thresholdText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
                    bonusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            List<string> unitNames = new List<string>();

            // Try local GameState first
            if (state != null)
            {
                var boardUnits = state.GetBoardUnits();
                foreach (var unit in boardUnits)
                {
                    if (unit != null && unit.HasTrait(trait))
                    {
                        string stars = new string('★', unit.starLevel);
                        unitNames.Add($"{unit.template.unitName} {stars}");
                    }
                }
            }
            // Fall back to ServerGameState for multiplayer
            else if (serverState != null && serverState.board != null)
            {
                for (int x = 0; x < 7; x++)
                {
                    for (int y = 0; y < 4; y++)
                    {
                        var serverUnit = serverState.board[x, y];
                        if (serverUnit != null && serverUnit.traits != null)
                        {
                            foreach (var t in serverUnit.traits)
                            {
                                if (t == trait.traitId)
                                {
                                    string stars = new string('★', serverUnit.starLevel);
                                    unitNames.Add($"{serverUnit.name} {stars}");
                                    break;
                                }
                            }
                        }
                    }
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
            spriteObj.transform.SetParent(cardObj.transform, false);
            RectTransform spriteRT = spriteObj.AddComponent<RectTransform>();
            spriteRT.localScale = Vector3.one;
            spriteRT.anchorMin = new Vector2(0.5f, 0.5f);
            spriteRT.anchorMax = new Vector2(0.5f, 0.5f);
            spriteRT.pivot = new Vector2(0.5f, 0.5f);
            spriteRT.sizeDelta = new Vector2(48, 48);
            spriteRT.anchoredPosition = new Vector2(0, 5);
            Image spriteImg = spriteObj.AddComponent<Image>();
            spriteImg.preserveAspect = true;
            spriteImg.enabled = false; // Start disabled until a unit is set
            spriteImg.raycastTarget = false; // Don't block clicks

            // Cost/Stars text
            GameObject costObj = new GameObject("Cost");
            costObj.transform.SetParent(cardObj.transform, false);
            RectTransform costRT = costObj.AddComponent<RectTransform>();
            costRT.localScale = Vector3.one;
            costRT.anchorMin = new Vector2(0, 0);
            costRT.anchorMax = new Vector2(1, 0);
            costRT.pivot = new Vector2(0.5f, 0);
            costRT.sizeDelta = new Vector2(0, 18);
            costRT.anchoredPosition = new Vector2(0, 3);
            Text costText = costObj.AddComponent<Text>();
            costText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            costText.fontSize = 12;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = Color.white;

            // Name text
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(cardObj.transform, false);
            RectTransform nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.localScale = Vector3.one;
            nameRT.anchorMin = new Vector2(0, 1);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.pivot = new Vector2(0.5f, 1);
            nameRT.sizeDelta = new Vector2(0, 18);
            nameRT.anchoredPosition = new Vector2(0, -2);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            // isBenchSlot defaults to false, which is correct for shop cards

            return card;
        }

        // ========== UI Updates ==========

        private void UpdateTopBar()
        {
            // Multiplayer mode - read from ServerGameState
            if (IsMultiplayer)
            {
                UpdateTopBarMultiplayer();
                return;
            }

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

            bool isPvPMode = state.currentGameMode == GameMode.PvP;
            bool isPlanning = state.round.phase == GamePhase.Planning;
            bool isCombat = state.round.phase == GamePhase.Combat;

            if (isPlanning)
            {
                timerText.text = $"{state.round.phaseTimer:F0}s";
                fightButton.gameObject.SetActive(true);
            }
            else
            {
                timerText.text = state.round.phase.ToString();
                fightButton.gameObject.SetActive(false);
            }

            // PvP mode specific UI
            if (scoutButton != null)
            {
                scoutButton.gameObject.SetActive(isPvPMode && isPlanning);
            }
            if (progressButton != null)
            {
                progressButton.gameObject.SetActive(isPvPMode);
            }

            // Show opponent info during PvP combat
            if (opponentInfoText != null)
            {
                bool showOpponentInfo = isPvPMode && (isCombat || state.round.phase == GamePhase.Results);
                opponentInfoText.gameObject.SetActive(showOpponentInfo);

                if (showOpponentInfo && OpponentManager.Instance != null)
                {
                    var opponent = OpponentManager.Instance.currentOpponent;
                    if (opponent != null)
                    {
                        opponentInfoText.text = $"vs {opponent.name} (HP: {opponent.health})";
                    }
                }
            }
        }

        private void UpdateTopBarMultiplayer()
        {
            var ss = serverState;

            roundText.text = $"R{ss.round}";
            healthText.text = $"❤ {ss.health}";
            goldText.text = $"💰 {ss.gold}";
            levelText.text = $"Lv{ss.level}";

            bool isPlanning = ss.phase == "planning";
            bool isCombat = ss.phase == "combat";

            if (isPlanning)
            {
                timerText.text = $"{ss.phaseTimer:F0}s";
                fightButton.gameObject.SetActive(true);

                // In multiplayer, change button text based on ready state
                var btnText = fightButton.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    btnText.text = ss.isReady ? "✓ READY" : "⚔ READY";
                }
            }
            else
            {
                timerText.text = ss.phase;
                fightButton.gameObject.SetActive(false);
            }

            // Multiplayer always shows scout/progress buttons
            if (scoutButton != null)
            {
                scoutButton.gameObject.SetActive(isPlanning);
            }
            if (progressButton != null)
            {
                progressButton.gameObject.SetActive(true);
            }

            // Show opponent info during combat from matchups
            if (opponentInfoText != null)
            {
                bool showOpponentInfo = isCombat || ss.phase == "results";
                opponentInfoText.gameObject.SetActive(showOpponentInfo);

                if (showOpponentInfo && ss.matchups != null && ss.matchups.Count > 0)
                {
                    // Find the matchup involving local player
                    foreach (var matchup in ss.matchups)
                    {
                        if (matchup.player1 == ss.localPlayerId || matchup.player2 == ss.localPlayerId)
                        {
                            string opponentId = matchup.player1 == ss.localPlayerId ? matchup.player2 : matchup.player1;
                            var opponent = ss.GetOpponentData(opponentId);
                            if (opponent != null)
                            {
                                opponentInfoText.text = $"vs {opponent.name} (HP: {opponent.health})";
                            }
                            break;
                        }
                    }
                }
            }

            // Show "My Board" button during combat to allow returning to your board
            if (myBoardButton != null)
            {
                myBoardButton.gameObject.SetActive(isCombat);
            }
        }

        private void UpdatePhaseUI()
        {
            // Multiplayer mode - use server phase
            if (IsMultiplayer)
            {
                UpdatePhaseUIMultiplayer();
                return;
            }

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
                    benchPanel?.gameObject.SetActive(false);
                    break;

                case GamePhase.ItemSelect:
                    if (phaseChanged || !selectionShown)
                    {
                        ShowItemSelection();
                        selectionShown = true;
                    }
                    shopPanel.gameObject.SetActive(false);
                    benchPanel?.gameObject.SetActive(false);
                    break;

                case GamePhase.Planning:
                    selectionPanel.gameObject.SetActive(false);
                    selectionShown = false;
                    // Hide shop during PvE intro round (round 1) - player just uses their starter unit
                    bool isPvEIntro = RoundManager.Instance != null && RoundManager.Instance.IsPvEIntroRound();
                    shopPanel.gameObject.SetActive(!isPvEIntro);
                    benchPanel?.gameObject.SetActive(false); // Bench UI disabled - using 3D bench
                    itemInventoryPanel?.gameObject.SetActive(true);
                    if (!isPvEIntro)
                    {
                        UpdateShop();
                        UpdateShopButtons();
                    }
                    UpdateBench();
                    UpdateItemInventory();
                    break;

                case GamePhase.Combat:
                case GamePhase.Results:
                    selectionPanel.gameObject.SetActive(false);
                    selectionShown = false;
                    // Hide shop during PvE intro round
                    bool isPvEIntroCombat = RoundManager.Instance != null && RoundManager.Instance.IsPvEIntroRound();
                    shopPanel.gameObject.SetActive(!isPvEIntroCombat);
                    benchPanel?.gameObject.SetActive(false); // Bench UI disabled - using 3D bench
                    itemInventoryPanel?.gameObject.SetActive(true);
                    // Scouting UI now handles its own visibility during combat
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

        private string lastMultiplayerPhase = "";

        private bool multiplayerCrestSelectionShown = false;
        private bool multiplayerItemSelectionShown = false;
        private bool multiplayerCrestReplacementShown = false;

        private void UpdatePhaseUIMultiplayer()
        {
            var ss = serverState;
            string currentPhase = ss.phase;

            bool phaseChanged = currentPhase != lastMultiplayerPhase;

            // Check for pending selections (from consumables like crest_token or item_anvil)
            bool hasPendingCrestSelection = ss.pendingCrestSelection != null && ss.pendingCrestSelection.Count > 0;
            bool hasPendingItemSelection = ss.pendingItemSelection != null && ss.pendingItemSelection.Count > 0;
            // Check crestId to avoid false positives from empty deserialized objects
            bool hasPendingCrestReplacement = ss.pendingCrestReplacement != null
                && ss.pendingCrestReplacement.newCrest != null
                && !string.IsNullOrEmpty(ss.pendingCrestReplacement.newCrest.crestId);

            switch (currentPhase)
            {
                case "waiting":
                    // Waiting for game to start - hide shop
                    shopPanel.gameObject.SetActive(false);
                    benchPanel?.gameObject.SetActive(false);
                    selectionPanel.gameObject.SetActive(false);
                    multiplayerCrestSelectionShown = false;
                    multiplayerItemSelectionShown = false;
                    multiplayerCrestReplacementShown = false;
                    break;

                case "planning":
                    // Check if we have pending selections to show
                    if (hasPendingCrestSelection)
                    {
                        if (!multiplayerCrestSelectionShown)
                        {
                            ShowCrestSelectionMultiplayer();
                            multiplayerCrestSelectionShown = true;
                        }
                        shopPanel.gameObject.SetActive(false);
                    }
                    else if (hasPendingItemSelection)
                    {
                        if (!multiplayerItemSelectionShown)
                        {
                            ShowItemSelectionMultiplayer();
                            multiplayerItemSelectionShown = true;
                        }
                        shopPanel.gameObject.SetActive(false);
                    }
                    else if (hasPendingCrestReplacement)
                    {
                        if (!multiplayerCrestReplacementShown)
                        {
                            ShowCrestReplacementMultiplayer();
                            multiplayerCrestReplacementShown = true;
                        }
                        shopPanel.gameObject.SetActive(false);
                    }
                    else
                    {
                        // No pending selections - show normal planning UI
                        selectionPanel.gameObject.SetActive(false);
                        selectionShown = false;
                        multiplayerCrestSelectionShown = false;
                        multiplayerItemSelectionShown = false;
                        multiplayerCrestReplacementShown = false;
                        shopPanel.gameObject.SetActive(true);
                        UpdateShop();
                        UpdateShopButtons();
                    }
                    benchPanel?.gameObject.SetActive(false);
                    itemInventoryPanel?.gameObject.SetActive(true);
                    UpdateItemInventory();
                    break;

                case "combat":
                case "results":
                    // Check if we have pending selections to show (consumables can be used in any phase)
                    if (hasPendingCrestSelection)
                    {
                        if (!multiplayerCrestSelectionShown)
                        {
                            ShowCrestSelectionMultiplayer();
                            multiplayerCrestSelectionShown = true;
                        }
                        shopPanel.gameObject.SetActive(false);
                    }
                    else if (hasPendingItemSelection)
                    {
                        if (!multiplayerItemSelectionShown)
                        {
                            ShowItemSelectionMultiplayer();
                            multiplayerItemSelectionShown = true;
                        }
                        shopPanel.gameObject.SetActive(false);
                    }
                    else if (hasPendingCrestReplacement)
                    {
                        if (!multiplayerCrestReplacementShown)
                        {
                            ShowCrestReplacementMultiplayer();
                            multiplayerCrestReplacementShown = true;
                        }
                        shopPanel.gameObject.SetActive(false);
                    }
                    else
                    {
                        selectionPanel.gameObject.SetActive(false);
                        selectionShown = false;
                        multiplayerCrestSelectionShown = false;
                        multiplayerItemSelectionShown = false;
                        multiplayerCrestReplacementShown = false;
                        shopPanel.gameObject.SetActive(true);
                        // Refresh shop so players can buy during combat/results
                        UpdateShop();
                        UpdateShopButtons();
                    }
                    benchPanel?.gameObject.SetActive(false);
                    itemInventoryPanel?.gameObject.SetActive(true);
                    UpdateItemInventory();
                    // Scouting UI now handles its own visibility during combat
                    break;

                case "gameOver":
                    if (phaseChanged || !selectionShown)
                    {
                        ShowGameOverMultiplayer();
                        selectionShown = true;
                    }
                    break;
            }

            lastMultiplayerPhase = currentPhase;
        }

        private void ShowGameOverMultiplayer()
        {
            selectionPanel.gameObject.SetActive(true);
            ClearSelectionPanel();

            var ss = serverState;
            bool victory = ss.health > 0;

            Text title = CreateText(victory ? "VICTORY!" : "DEFEAT", selectionPanel, 0);
            title.fontSize = 36;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = victory ? new Color(1f, 0.85f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
            title.GetComponent<LayoutElement>().preferredHeight = 50;

            Text infoText = CreateText($"Round {ss.round}", selectionPanel, 0);
            infoText.fontSize = 20;
            infoText.alignment = TextAnchor.MiddleCenter;
            infoText.GetComponent<LayoutElement>().preferredHeight = 30;

            // Return to lobby button
            CreateSelectionCard("Return to Lobby", "Leave the game", new Color(0.5f, 0.5f, 0.6f), () => {
                NetworkManager.Instance?.LeaveRoom();
                if (LobbyUI.Instance != null)
                {
                    gameObject.SetActive(false);
                    LobbyUI.Instance.Show();
                }
            });
        }

        private void UpdateShop()
        {
            // Multiplayer mode - use server shop data
            if (IsMultiplayer)
            {
                UpdateShopMultiplayer();
                return;
            }

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

        private void UpdateShopMultiplayer()
        {
            var ss = serverState;
            if (ss.shop == null) return;

            for (int i = 0; i < shopCards.Count; i++)
            {
                if (i < ss.shop.Length && ss.shop[i] != null)
                {
                    var serverUnit = ss.shop[i];
                    shopCards[i].SetServerUnit(serverUnit);

                    // Grey out if can't afford
                    bool canAfford = ss.gold >= serverUnit.cost;
                    shopCards[i].SetInteractable(canAfford);
                }
                else
                {
                    shopCards[i].SetServerUnit(null);
                }
            }
        }

        private void UpdateBench()
        {
            // Bench UI is disabled - 3D bench is handled by BoardManager3D
            if (benchCards == null || benchCards.Count == 0) return;

            for (int i = 0; i < benchCards.Count; i++)
            {
                if (state.bench != null && i < state.bench.Length)
                {
                    benchCards[i].SetUnit(state.bench[i]); // May be null for empty slots
                    benchCards[i].SetInteractable(state.bench[i] != null);
                }
                else
                {
                    benchCards[i].SetUnit(null);
                }
            }
        }

        private void UpdateItemInventory()
        {
            if (itemSlots == null) return;

            // Multiplayer mode - use server state
            if (IsMultiplayer && serverState != null)
            {
                for (int i = 0; i < itemSlots.Count; i++)
                {
                    if (serverState.itemInventory != null && i < serverState.itemInventory.Count)
                    {
                        itemSlots[i].SetServerItem(serverState.itemInventory[i], i);
                    }
                    else
                    {
                        itemSlots[i].SetServerItem(null, -1);
                    }
                }
                return;
            }

            // Single-player mode
            if (state == null) return;

            for (int i = 0; i < itemSlots.Count; i++)
            {
                if (state.itemInventory != null && i < state.itemInventory.Count)
                {
                    itemSlots[i].SetItem(state.itemInventory[i]);
                }
                else
                {
                    itemSlots[i].SetItem(null);
                }
            }
        }

        public void RefreshItemInventory()
        {
            UpdateItemInventory();
        }

        public void RefreshBench()
        {
            UpdateBench();
        }

        /// <summary>
        /// Show sell mode overlay when dragging a unit
        /// </summary>
        public void ShowSellMode(UnitInstance unit)
        {
            if (unit == null || sellOverlay == null) return;

            unitBeingDragged = unit;
            isSellModeActive = true;

            int sellValue = unit.GetSellValue();
            sellText.text = $"Sell {unit.template.unitName} for ${sellValue}";

            // Hide shop and show sell overlay
            if (shopPanel != null)
            {
                shopPanel.gameObject.SetActive(false);
            }
            sellOverlay.SetActive(true);
        }

        /// <summary>
        /// Hide sell mode overlay
        /// </summary>
        public void HideSellMode()
        {
            isSellModeActive = false;
            unitBeingDragged = null;

            if (sellOverlay != null)
            {
                sellOverlay.SetActive(false);
            }

            // Restore shop panel visibility (respecting PvE intro state)
            if (shopPanel != null && state != null)
            {
                bool isPvEIntro = Crestforge.Systems.RoundManager.Instance?.IsPvEIntroRound() ?? false;
                shopPanel.gameObject.SetActive(!isPvEIntro);
            }
        }

        /// <summary>
        /// Try to sell the currently dragged unit
        /// </summary>
        public bool TrySellUnit()
        {
            if (unitBeingDragged == null || state == null) return false;

            state.SellUnit(unitBeingDragged);
            Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();

            // Refresh UI
            UpdateTopBar();
            RefreshBench();

            return true;
        }

        /// <summary>
        /// Check if sell mode is active
        /// </summary>
        public bool IsSellModeActive => isSellModeActive;

        /// <summary>
        /// Get the unit currently being dragged
        /// </summary>
        public UnitInstance UnitBeingDragged => unitBeingDragged;

        public void ShowItemTooltip(ItemData item, Vector3 position)
        {
            if (item == null || tooltipPanel == null) return;

            // Don't override pinned tooltip on hover
            if (isTooltipPinned) return;

            tooltipPanel.gameObject.SetActive(true);

            // Reset to default anchored position (middle-right, set in CreateTooltip)
            tooltipPanel.anchoredPosition = new Vector2(-15, 50);

            PopulateItemTooltip(item);
        }

        public void HideItemTooltip()
        {
            if (!isTooltipPinned && tooltipPanel != null)
            {
                tooltipPanel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Show item info temporarily while unit tooltip is pinned
        /// </summary>
        public void ShowItemInfoTemporary(ItemData item)
        {
            if (item == null || tooltipPanel == null) return;

            showingTemporaryItemInfo = true;
            PopulateItemTooltip(item);
        }

        /// <summary>
        /// Restore the unit tooltip after temporarily showing item info
        /// </summary>
        public void RestoreUnitTooltip()
        {
            if (!showingTemporaryItemInfo) return;
            showingTemporaryItemInfo = false;

            // Restore the unit tooltip if we have a unit pinned
            if (isTooltipPinned && tooltipUnit != null)
            {
                ShowTooltip(tooltipUnit);
            }
        }

        public void ShowItemTooltipPinned(ItemData item)
        {
            if (item == null || tooltipPanel == null) return;

            isTooltipPinned = true;
            tooltipPanel.gameObject.SetActive(true);

            // Reset to default anchored position (middle-right, set in CreateTooltip)
            tooltipPanel.anchoredPosition = new Vector2(-15, 50);

            PopulateItemTooltip(item);
        }

        public void ToggleItemTooltipPin(ItemData item)
        {
            if (item == null || tooltipPanel == null) return;

            if (isTooltipPinned && tooltipPanel.gameObject.activeSelf)
            {
                // Already pinned, unpin and hide
                isTooltipPinned = false;
                tooltipPanel.gameObject.SetActive(false);
            }
            else
            {
                // Pin the tooltip
                ShowItemTooltipPinned(item);
            }
        }

        private void PopulateItemTooltip(ItemData item)
        {
            if (tooltipTitle != null) tooltipTitle.text = item.itemName;
            if (tooltipCost != null) tooltipCost.text = item.rarity.ToString();
            if (tooltipStats != null) tooltipStats.text = GetItemStatsText(item);
            if (tooltipTraits != null) tooltipTraits.text = item.description;
            if (tooltipAbility != null) tooltipAbility.text = GetItemEffectText(item);
        }

        private string GetItemStatsText(ItemData item)
        {
            var stats = new System.Collections.Generic.List<string>();
            if (item.bonusHealth != 0) stats.Add($"+{item.bonusHealth} Health");
            if (item.bonusAttack != 0) stats.Add($"+{item.bonusAttack} Attack");
            if (item.bonusArmor != 0) stats.Add($"+{item.bonusArmor} Armor");
            if (item.bonusMagicResist != 0) stats.Add($"+{item.bonusMagicResist} Magic Resist");
            if (item.bonusAttackSpeed != 0) stats.Add($"+{(int)(item.bonusAttackSpeed * 100)}% Attack Speed");
            if (item.bonusMana != 0) stats.Add($"+{item.bonusMana} Mana");
            return string.Join("\n", stats);
        }

        private string GetItemEffectText(ItemData item)
        {
            if (item.effect == ItemEffect.None) return "";

            return item.effect switch
            {
                ItemEffect.Lifesteal => $"Lifesteal: Heal for {(int)(item.effectValue1 * 100)}% of damage dealt",
                ItemEffect.Burn => $"Burn: Deal {item.effectValue1} damage over {item.effectValue2}s",
                ItemEffect.CriticalStrike => $"Crit: {(int)(item.effectValue1 * 100)}% chance for {item.effectValue2}x damage",
                ItemEffect.Thorns => $"Thorns: Reflect {(int)(item.effectValue1 * 100)}% of damage taken",
                ItemEffect.DamageReduction => $"Reduce damage taken by {(int)(item.effectValue1 * 100)}%",
                ItemEffect.AbilityPower => $"Increase ability damage by {(int)(item.effectValue1 * 100)}%",
                ItemEffect.ManaOnHit => $"Gain {item.effectValue1} mana on hit",
                _ => ""
            };
        }

        // ===== Crest Tooltip Methods =====

        public void ShowCrestTooltip(CrestData crest)
        {
            if (crest == null || crestTooltipPanel == null) return;

            // Don't override pinned tooltip on hover
            if (isCrestTooltipPinned) return;

            crestTooltipPanel.gameObject.SetActive(true);
            PopulateCrestTooltip(crest);
        }

        public void HideCrestTooltip()
        {
            if (!isCrestTooltipPinned && crestTooltipPanel != null)
            {
                crestTooltipPanel.gameObject.SetActive(false);
            }
        }

        public void ShowCrestTooltipPinned(CrestData crest)
        {
            if (crest == null || crestTooltipPanel == null) return;

            isCrestTooltipPinned = true;
            crestTooltipPanel.gameObject.SetActive(true);
            PopulateCrestTooltip(crest);
        }

        public void UnpinCrestTooltip()
        {
            isCrestTooltipPinned = false;
            if (crestTooltipPanel != null)
            {
                crestTooltipPanel.gameObject.SetActive(false);
            }
        }

        private void PopulateCrestTooltip(CrestData crest)
        {
            if (crestTooltipTitle != null)
            {
                crestTooltipTitle.text = crest.crestName;
                crestTooltipTitle.color = crest.type == CrestType.Minor ?
                    new Color(0.8f, 0.6f, 1f) : new Color(1f, 0.8f, 0.3f);
            }

            if (crestTooltipType != null)
            {
                crestTooltipType.text = crest.type == CrestType.Minor ? "Minor Crest" : "Major Crest";
            }

            if (crestTooltipDescription != null)
            {
                crestTooltipDescription.text = crest.description;
            }

            if (crestTooltipBonus != null)
            {
                crestTooltipBonus.text = GetCrestBonusText(crest);
            }
        }

        private string GetCrestBonusText(CrestData crest)
        {
            // Generate bonus text based on crest effect
            string bonusText = crest.effect switch
            {
                CrestEffect.AllUnitsShield => $"All units gain {crest.effectValue1} shield at combat start",
                CrestEffect.AllUnitsPoison => $"All attacks poison for {crest.effectValue1} damage over {crest.effectValue2}s",
                CrestEffect.AllUnitsBurn => $"All attacks burn for {crest.effectValue1} damage over {crest.effectValue2}s",
                CrestEffect.AllUnitsLifesteal => $"All units lifesteal {(int)(crest.effectValue1 * 100)}%",
                CrestEffect.LowHealthDamageBoost => $"Units below {(int)(crest.effectValue1 * 100)}% HP deal {(int)(crest.effectValue2 * 100)}% more damage",
                CrestEffect.LowHealthDefenseBoost => $"Units below {(int)(crest.effectValue1 * 100)}% HP take {(int)(crest.effectValue2 * 100)}% less damage",
                CrestEffect.HighHealthDamageBoost => $"Bonus damage to targets above {(int)(crest.effectValue1 * 100)}% HP",
                CrestEffect.AllyDeathAttackSpeed => $"When ally dies, others gain {(int)(crest.effectValue1 * 100)}% attack speed for {crest.effectValue2}s",
                CrestEffect.AllyDeathHeal => $"When ally dies, others heal for {crest.effectValue1}",
                CrestEffect.AllAbilityDamage => $"All abilities deal {(int)(crest.effectValue1 * 100)}% more damage",
                CrestEffect.AllManaCostReduction => $"All abilities cost {(int)(crest.effectValue1 * 100)}% less mana",
                CrestEffect.TraitAttackSpeed => $"Units with {crest.synergyTrait?.traitName ?? "synergy trait"} gain {(int)(crest.effectValue1 * 100)}% attack speed",
                CrestEffect.TraitDamage => $"Units with {crest.synergyTrait?.traitName ?? "synergy trait"} deal {(int)(crest.effectValue1 * 100)}% more damage",
                CrestEffect.TraitManaGain => $"Units with {crest.synergyTrait?.traitName ?? "synergy trait"} gain {(int)(crest.effectValue1 * 100)}% more mana",
                _ => GetCrestStatBonusText(crest)
            };
            return bonusText;
        }

        private string GetCrestStatBonusText(CrestData crest)
        {
            // Build stat bonus text for crests with no special effect
            var bonuses = new System.Collections.Generic.List<string>();
            if (crest.bonusHealth != 0) bonuses.Add($"+{crest.bonusHealth} Health");
            if (crest.bonusAttack != 0) bonuses.Add($"+{crest.bonusAttack} Attack");
            if (crest.bonusArmor != 0) bonuses.Add($"+{crest.bonusArmor} Armor");
            if (crest.bonusMagicResist != 0) bonuses.Add($"+{crest.bonusMagicResist} Magic Resist");
            if (crest.bonusAttackSpeed != 0) bonuses.Add($"+{(int)(crest.bonusAttackSpeed * 100)}% Attack Speed");
            if (crest.bonusMana != 0) bonuses.Add($"+{crest.bonusMana} Mana");

            if (bonuses.Count > 0)
                return string.Join(", ", bonuses) + " to all units";
            return "Passive bonus active";
        }

        /// <summary>
        /// Show crest tooltip for server crest (multiplayer mode)
        /// </summary>
        public void ShowServerCrestTooltip(ServerCrestData serverCrest)
        {
            if (serverCrest == null || crestTooltipPanel == null) return;

            // Don't override pinned tooltip on hover
            if (isCrestTooltipPinned) return;

            crestTooltipPanel.gameObject.SetActive(true);
            PopulateServerCrestTooltip(serverCrest);
        }

        /// <summary>
        /// Show pinned crest tooltip for server crest (multiplayer mode)
        /// </summary>
        public void ShowServerCrestTooltipPinned(ServerCrestData serverCrest)
        {
            if (serverCrest == null || crestTooltipPanel == null) return;

            isCrestTooltipPinned = true;
            crestTooltipPanel.gameObject.SetActive(true);
            PopulateServerCrestTooltip(serverCrest);
        }

        private void PopulateServerCrestTooltip(ServerCrestData serverCrest)
        {
            bool isMinor = serverCrest.type == "minor";

            if (crestTooltipTitle != null)
            {
                crestTooltipTitle.text = serverCrest.name ?? serverCrest.crestId;
                crestTooltipTitle.color = isMinor ?
                    new Color(0.8f, 0.6f, 1f) : new Color(1f, 0.8f, 0.3f);
            }

            if (crestTooltipType != null)
            {
                crestTooltipType.text = isMinor ? "Minor Crest" : "Major Crest";
            }

            if (crestTooltipDescription != null)
            {
                crestTooltipDescription.text = serverCrest.description ?? "";
            }

            if (crestTooltipBonus != null)
            {
                crestTooltipBonus.text = GetServerCrestBonusText(serverCrest);
            }
        }

        private string GetServerCrestBonusText(ServerCrestData serverCrest)
        {
            // For minor crests, show the granted trait
            if (!string.IsNullOrEmpty(serverCrest.grantsTrait))
            {
                return $"Grants {serverCrest.grantsTrait} trait";
            }

            // For major crests, show stat bonuses
            if (serverCrest.teamBonus != null)
            {
                var bonuses = new System.Collections.Generic.List<string>();
                var b = serverCrest.teamBonus;
                if (b.health != 0) bonuses.Add($"+{b.health} Health");
                if (b.attack != 0) bonuses.Add($"+{b.attack} Attack");
                if (b.armor != 0) bonuses.Add($"+{b.armor} Armor");
                if (b.magicResist != 0) bonuses.Add($"+{b.magicResist} Magic Resist");
                if (b.attackSpeedPercent != 0) bonuses.Add($"+{(int)(b.attackSpeedPercent * 100)}% Attack Speed");
                if (b.abilityPower != 0) bonuses.Add($"+{b.abilityPower} Ability Power");
                if (b.lifesteal != 0) bonuses.Add($"+{b.lifesteal}% Lifesteal");

                if (bonuses.Count > 0)
                    return string.Join(", ", bonuses) + " to all units";
            }

            return "Passive bonus active";
        }

        public void UpdateCrestDisplay()
        {
            bool hasAnyCrest = false;

            // Multiplayer mode - use server crest data
            if (IsMultiplayer && serverState != null)
            {
                // Check major crest
                bool hasMajorCrest = serverState.majorCrest != null && !string.IsNullOrEmpty(serverState.majorCrest.crestId);
                if (majorCrestSlot != null)
                {
                    majorCrestSlot.SetServerCrest(hasMajorCrest ? serverState.majorCrest : null);
                    if (hasMajorCrest) hasAnyCrest = true;
                }

                // Update minor crest slots (up to 3)
                for (int i = 0; i < minorCrestSlots.Length; i++)
                {
                    if (minorCrestSlots[i] != null)
                    {
                        ServerCrestData minorCrest = (serverState.minorCrests != null && i < serverState.minorCrests.Count)
                            ? serverState.minorCrests[i]
                            : null;
                        bool hasValidCrest = minorCrest != null && !string.IsNullOrEmpty(minorCrest.crestId);
                        minorCrestSlots[i].SetServerCrest(hasValidCrest ? minorCrest : null);
                        if (hasValidCrest) hasAnyCrest = true;
                    }
                }

                // Hide entire crest panel if no crests are active
                if (crestPanel != null)
                {
                    crestPanel.gameObject.SetActive(hasAnyCrest);
                }
                return;
            }

            if (state == null) return;

            // Update major crest slot with the first major crest (if any)
            if (majorCrestSlot != null)
            {
                CrestData activeMajorCrest = (state.majorCrests != null && state.majorCrests.Count > 0)
                    ? state.majorCrests[0]
                    : null;
                majorCrestSlot.SetCrest(activeMajorCrest);
                if (activeMajorCrest != null) hasAnyCrest = true;
            }

            // Update minor crest slots (up to 3)
            for (int i = 0; i < minorCrestSlots.Length; i++)
            {
                if (minorCrestSlots[i] != null)
                {
                    CrestData activeMinorCrest = (state.minorCrests != null && i < state.minorCrests.Count)
                        ? state.minorCrests[i]
                        : null;
                    minorCrestSlots[i].SetCrest(activeMinorCrest);
                    if (activeMinorCrest != null) hasAnyCrest = true;
                }
            }

            // Hide entire crest panel if no crests are active
            if (crestPanel != null)
            {
                crestPanel.gameObject.SetActive(hasAnyCrest);
            }
        }

        private void UpdateShopButtons()
        {
            // Multiplayer mode
            if (IsMultiplayer)
            {
                var ss = serverState;
                // Can reroll if have free rerolls OR enough gold
                bool mpCanReroll = ss.freeRerolls > 0 || ss.gold >= 2; // REROLL_COST
                bool mpCanBuyXP = ss.gold >= 4 && ss.level < 9; // XP_COST, MAX_LEVEL

                rerollButton.interactable = mpCanReroll;
                buyXPButton.interactable = mpCanBuyXP;

                // Update reroll button text to show free rerolls
                var rerollText = rerollButton.GetComponentInChildren<Text>();
                if (rerollText != null)
                {
                    if (ss.freeRerolls > 0)
                    {
                        rerollText.text = $"🔄 0 ({ss.freeRerolls})";
                    }
                    else
                    {
                        rerollText.text = "🔄 $2";
                    }
                }

                string mpLockIcon = ss.shopLocked ? "🔒" : "🔓";
                lockButton.GetComponentInChildren<Text>().text = mpLockIcon;
                return;
            }

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

        /// <summary>
        /// Show crest selection panel for multiplayer mode (uses server data)
        /// </summary>
        private void ShowCrestSelectionMultiplayer()
        {
            selectionPanel.gameObject.SetActive(true);
            ClearSelectionPanel();

            Text title = CreateText("SELECT A CREST", selectionPanel, 0);
            title.fontSize = 28;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.GetComponent<LayoutElement>().preferredHeight = 40;

            var ss = serverState;
            if (ss.pendingCrestSelection == null || ss.pendingCrestSelection.Count == 0) return;

            for (int i = 0; i < ss.pendingCrestSelection.Count; i++)
            {
                var crest = ss.pendingCrestSelection[i];
                if (crest == null) continue;

                int choiceIndex = i;
                Color crestColor = crest.type == "minor"
                    ? new Color(0.4f, 0.5f, 0.6f)  // Minor crest color
                    : new Color(0.6f, 0.4f, 0.2f); // Major crest color

                string description = crest.description ?? "";
                if (!string.IsNullOrEmpty(crest.grantsTrait))
                {
                    description = $"+1 {crest.grantsTrait}\n{description}";
                }

                CreateSelectionCard(
                    crest.name ?? crest.crestId,
                    description,
                    crestColor,
                    () => OnCrestSelectedMultiplayer(choiceIndex)
                );
            }
        }

        /// <summary>
        /// Show crest replacement panel when player has max crests and tries to get a new one.
        /// Displays current 3 crests and lets player pick which to replace.
        /// </summary>
        private void ShowCrestReplacementMultiplayer()
        {
            selectionPanel.gameObject.SetActive(true);
            ClearSelectionPanel();

            var ss = serverState;
            if (ss.pendingCrestReplacement == null
                || ss.pendingCrestReplacement.newCrest == null
                || string.IsNullOrEmpty(ss.pendingCrestReplacement.newCrest.crestId))
            {
                Debug.LogWarning("[GameUI] ShowCrestReplacementMultiplayer called but pendingCrestReplacement is null or empty");
                return;
            }

            var newCrest = ss.pendingCrestReplacement.newCrest;

            // Title showing the new crest being added
            Text title = CreateText("REPLACE A CREST", selectionPanel, 0);
            title.fontSize = 28;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.GetComponent<LayoutElement>().preferredHeight = 40;

            // Subtitle showing the new crest
            Text subtitle = CreateText($"New: {newCrest.name ?? newCrest.crestId}", selectionPanel, 0);
            subtitle.fontSize = 20;
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.color = new Color(0.4f, 1f, 0.5f);  // Green for new crest
            subtitle.GetComponent<LayoutElement>().preferredHeight = 30;

            // Show description of new crest
            if (!string.IsNullOrEmpty(newCrest.description))
            {
                Text descText = CreateText(newCrest.description, selectionPanel, 0);
                descText.fontSize = 16;
                descText.alignment = TextAnchor.MiddleCenter;
                descText.color = new Color(0.8f, 0.8f, 0.8f);
                descText.GetComponent<LayoutElement>().preferredHeight = 25;
            }

            // Instructions
            Text instructions = CreateText("Choose which crest to replace:", selectionPanel, 0);
            instructions.fontSize = 18;
            instructions.alignment = TextAnchor.MiddleCenter;
            instructions.color = new Color(1f, 0.9f, 0.7f);
            instructions.GetComponent<LayoutElement>().preferredHeight = 30;

            // Display current 3 crests as options to replace
            if (ss.minorCrests == null || ss.minorCrests.Count == 0)
            {
                Debug.LogWarning("[GameUI] No minor crests to replace");
                return;
            }

            for (int i = 0; i < ss.minorCrests.Count; i++)
            {
                var crest = ss.minorCrests[i];
                if (crest == null) continue;

                int replaceIndex = i;
                Color crestColor = new Color(0.5f, 0.4f, 0.4f);  // Dimmer color for crests being replaced

                string rankText = crest.rank > 1 ? $" (Rank {crest.rank})" : "";
                string crestName = (crest.name ?? crest.crestId) + rankText;
                string description = crest.description ?? "";

                if (!string.IsNullOrEmpty(crest.grantsTrait))
                {
                    description = $"+1 {crest.grantsTrait}\n{description}";
                }

                CreateSelectionCard(
                    crestName,
                    description,
                    crestColor,
                    () => OnCrestReplacedMultiplayer(replaceIndex)
                );
            }
        }

        /// <summary>
        /// Show item selection panel for multiplayer mode (uses server data)
        /// </summary>
        private void ShowItemSelectionMultiplayer()
        {
            selectionPanel.gameObject.SetActive(true);
            ClearSelectionPanel();

            Text title = CreateText("SELECT AN ITEM", selectionPanel, 0);
            title.fontSize = 28;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.GetComponent<LayoutElement>().preferredHeight = 40;

            var ss = serverState;
            if (ss.pendingItemSelection == null || ss.pendingItemSelection.Count == 0)
            {
                Debug.LogWarning("[GameUI] pendingItemSelection is null or empty!");
                return;
            }

            for (int i = 0; i < ss.pendingItemSelection.Count; i++)
            {
                var item = ss.pendingItemSelection[i];
                if (item == null)
                {
                    Debug.LogWarning($"[GameUI] Item at index {i} is null");
                    continue;
                }

                int choiceIndex = i;
                Color itemColor = new Color(1f, 0.8f, 0.4f);  // Gold for all items

                CreateSelectionCard(
                    item.name ?? item.itemId,
                    item.description ?? "",
                    itemColor,
                    () => OnItemSelectedMultiplayer(choiceIndex)
                );
            }
        }

        private void OnCrestSelectedMultiplayer(int choiceIndex)
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            Crestforge.Visuals.AudioManager.Instance?.PlayLevelUp();

            serverState.SelectCrestChoice(choiceIndex);

            // Hide selection panel - it will be re-shown if there are more selections pending
            selectionPanel.gameObject.SetActive(false);
            multiplayerCrestSelectionShown = false;

            // Update the crest display UI
            UpdateCrestDisplay();
        }

        private void OnCrestReplacedMultiplayer(int replaceIndex)
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            Crestforge.Visuals.AudioManager.Instance?.PlayLevelUp();

            serverState.ReplaceCrest(replaceIndex);

            // Hide selection panel
            selectionPanel.gameObject.SetActive(false);
            multiplayerCrestReplacementShown = false;

            // Update the crest display UI
            UpdateCrestDisplay();
        }

        private void OnItemSelectedMultiplayer(int choiceIndex)
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            Crestforge.Visuals.AudioManager.Instance?.PlayLevelUp();

            serverState.SelectItemChoice(choiceIndex);

            // Hide selection panel - it will be re-shown if there are more selections pending
            selectionPanel.gameObject.SetActive(false);
            multiplayerItemSelectionShown = false;

            // Update inventory display
            UpdateItemInventory();
        }

        private void ShowGameOver()
        {
            selectionPanel.gameObject.SetActive(true);
            ClearSelectionPanel();

            bool victory = state.player.health > 0;
            bool isPvPMode = state.currentGameMode == GameMode.PvP;

            // Check for PvP victory condition (all opponents eliminated)
            if (isPvPMode && victory && OpponentManager.Instance != null)
            {
                var activeOpponents = OpponentManager.Instance.GetActiveOpponents();
                victory = activeOpponents.Count == 0;
            }

            Text title = CreateText(victory ? "🏆 VICTORY! 🏆" : "💀 DEFEAT 💀", selectionPanel, 0);
            title.fontSize = 36;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = victory ? new Color(1f, 0.85f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
            title.GetComponent<LayoutElement>().preferredHeight = 50;

            // PvP-specific info
            string infoText;
            if (isPvPMode)
            {
                if (victory)
                {
                    infoText = $"All opponents eliminated! (Round {state.round.currentRound})";
                }
                else
                {
                    int eliminatedCount = OpponentManager.Instance != null
                        ? 3 - OpponentManager.Instance.GetActiveOpponents().Count
                        : 0;
                    infoText = $"Eliminated {eliminatedCount} opponents (Round {state.round.currentRound})";
                }
            }
            else
            {
                infoText = $"Made it to Round {state.round.currentRound}";
            }

            Text info = CreateText(infoText, selectionPanel, 0);
            info.fontSize = 20;
            info.alignment = TextAnchor.MiddleCenter;
            info.GetComponent<LayoutElement>().preferredHeight = 30;

            CreateSelectionCard("Play Again", "Start a new game with same mode",
                new Color(0.3f, 0.6f, 0.3f), OnPlayAgainClicked);

            CreateSelectionCard("Main Menu", "Choose a different game mode",
                new Color(0.4f, 0.4f, 0.5f), OnReturnToMenuClicked);
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
            // Skip in multiplayer or if state not ready
            if (IsMultiplayer) return;
            if (state == null || state.round == null) return;

            // Hide tooltip during non-planning phases (unless pinned)
            if (state.round.phase != GamePhase.Planning && !isTooltipPinned)
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

            // Set items
            tooltipUnit = unit;
            PopulateTooltipItems(unit);
        }

        /// <summary>
        /// Show tooltip for a server unit (multiplayer mode)
        /// </summary>
        public void ShowTooltip(ServerUnitData serverUnit)
        {
            if (serverUnit == null)
            {
                tooltipPanel.gameObject.SetActive(false);
                return;
            }

            // Get the unit template from ServerGameState
            var serverState = ServerGameState.Instance;
            var template = serverState?.GetUnitTemplate(serverUnit.unitId);

            if (template == null)
            {
                Debug.LogWarning($"[GameUI] Could not find template for server unit: {serverUnit.unitId}");
                tooltipPanel.gameObject.SetActive(false);
                return;
            }

            tooltipPanel.gameObject.SetActive(true);

            // Set sprite
            if (tooltipSprite != null)
            {
                tooltipSprite.sprite = UnitSpriteGenerator.GetSprite(serverUnit.unitId);
                tooltipSprite.enabled = true;
            }

            // Set name with cost color
            tooltipTitle.text = serverUnit.name ?? template.unitName;
            tooltipTitle.color = GetCostColor(serverUnit.cost);

            // Set cost and stars
            tooltipCost.text = $"${serverUnit.cost}  " + new string('★', serverUnit.starLevel);

            // Set stats - use server stats if available, otherwise template base stats
            string stats = "";
            if (serverUnit.currentStats != null)
            {
                stats = $"HP: {serverUnit.currentStats.health}    ATK: {serverUnit.currentStats.attack}\n";
                stats += $"Armor: {serverUnit.currentStats.armor}    Magic Resist: {serverUnit.currentStats.magicResist}\n";
                stats += $"Attack Speed: {serverUnit.currentStats.attackSpeed:F2}    Range: {serverUnit.currentStats.range}";
            }
            else
            {
                stats = $"HP: {template.baseStats.health}    ATK: {template.baseStats.attack}\n";
                stats += $"Armor: {template.baseStats.armor}    Magic Resist: {template.baseStats.magicResist}\n";
                stats += $"Attack Speed: {template.baseStats.attackSpeed:F2}    Range: {template.baseStats.range}";
            }
            tooltipStats.text = stats;

            // Set traits
            string traits = "";
            if (serverUnit.traits != null)
            {
                traits = string.Join(", ", serverUnit.traits);
            }
            else if (template.traits != null)
            {
                foreach (var trait in template.traits)
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
            if (template.ability != null)
            {
                string abilityText = template.ability.abilityName;
                if (!string.IsNullOrEmpty(template.ability.description))
                    abilityText += $"\n{template.ability.description}";
                abilityText += $"\nMana: {template.baseStats.maxMana}";
                if (template.ability.baseDamage > 0)
                    abilityText += $"  |  Damage: {template.ability.baseDamage}";
                if (template.ability.baseHealing > 0)
                    abilityText += $"  |  Heal: {template.ability.baseHealing}";
                tooltipAbility.text = abilityText;
            }
            else
            {
                tooltipAbility.text = "None";
            }

            // Set items
            tooltipUnit = null; // Clear UnitInstance reference
            PopulateTooltipItems(serverUnit);
        }

        /// <summary>
        /// Show tooltip and keep it pinned for server units (multiplayer mode)
        /// </summary>
        public void ShowTooltipPinned(ServerUnitData serverUnit)
        {
            isTooltipPinned = true;
            ShowTooltip(serverUnit);
        }

        /// <summary>
        /// Show tooltip for combat units (during multiplayer combat)
        /// </summary>
        public void ShowTooltip(ServerCombatUnit combatUnit)
        {
            if (combatUnit == null)
            {
                tooltipPanel.gameObject.SetActive(false);
                return;
            }

            // Get the unit template from ServerGameState
            var serverState = ServerGameState.Instance;
            var template = serverState?.GetUnitTemplate(combatUnit.unitId);

            if (template == null)
            {
                Debug.LogWarning($"[GameUI] Could not find template for combat unit: {combatUnit.unitId}");
                tooltipPanel.gameObject.SetActive(false);
                return;
            }

            tooltipPanel.gameObject.SetActive(true);

            // Set sprite
            if (tooltipSprite != null)
            {
                tooltipSprite.sprite = UnitSpriteGenerator.GetSprite(combatUnit.unitId);
                tooltipSprite.enabled = true;
            }

            // Set name with cost color
            tooltipTitle.text = combatUnit.name ?? template.unitName;
            tooltipTitle.color = GetCostColor(template.cost);

            // Set cost (from template) and determine star level from stats
            int starLevel = 1;
            if (combatUnit.maxHealth > template.baseStats.health * 2.5f) starLevel = 3;
            else if (combatUnit.maxHealth > template.baseStats.health * 1.5f) starLevel = 2;
            tooltipCost.text = $"${template.cost}  " + new string('★', starLevel);

            // Set stats from combat unit
            string stats = "";
            if (combatUnit.stats != null)
            {
                stats = $"HP: {combatUnit.health}/{combatUnit.maxHealth}    ATK: {combatUnit.stats.attack}\n";
                stats += $"Armor: {combatUnit.stats.armor}    Magic Resist: {combatUnit.stats.magicResist}\n";
                stats += $"Attack Speed: {combatUnit.stats.attackSpeed:F2}    Range: {combatUnit.stats.range}";
            }
            else
            {
                stats = $"HP: {combatUnit.health}/{combatUnit.maxHealth}    ATK: {template.baseStats.attack}\n";
                stats += $"Armor: {template.baseStats.armor}    Magic Resist: {template.baseStats.magicResist}\n";
                stats += $"Attack Speed: {template.baseStats.attackSpeed:F2}    Range: {template.baseStats.range}";
            }
            tooltipStats.text = stats;

            // Set traits from template
            string traits = "";
            if (template.traits != null)
            {
                foreach (var trait in template.traits)
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
            if (template.ability != null)
            {
                string abilityText = template.ability.abilityName;
                if (!string.IsNullOrEmpty(template.ability.description))
                    abilityText += $"\n{template.ability.description}";
                abilityText += $"\nMana: {template.baseStats.maxMana}";
                if (template.ability.baseDamage > 0)
                    abilityText += $"  |  Damage: {template.ability.baseDamage}";
                if (template.ability.baseHealing > 0)
                    abilityText += $"  |  Heal: {template.ability.baseHealing}";
                tooltipAbility.text = abilityText;
            }
            else
            {
                tooltipAbility.text = "None";
            }

            // Set items
            tooltipUnit = null;
            PopulateTooltipItems(combatUnit);
        }

        /// <summary>
        /// Show tooltip and keep it pinned for combat units (during multiplayer combat)
        /// </summary>
        public void ShowTooltipPinned(ServerCombatUnit combatUnit)
        {
            isTooltipPinned = true;
            ShowTooltip(combatUnit);
        }

        /// <summary>
        /// Populate tooltip items for combat units (multiplayer mode - display only)
        /// </summary>
        private void PopulateTooltipItems(ServerCombatUnit combatUnit)
        {
            // Clear existing item slots
            foreach (var slot in tooltipItemSlots)
            {
                if (slot != null && slot.gameObject != null)
                    Destroy(slot.gameObject);
            }
            tooltipItemSlots.Clear();

            if (tooltipItemContainer != null)
            {
                foreach (Transform child in tooltipItemContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            bool hasItems = combatUnit.items != null && combatUnit.items.Count > 0;

            if (tooltipItemsLabel != null)
            {
                Transform dividerBeforeItems = tooltipItemsLabel.transform.parent.GetChild(
                    tooltipItemsLabel.transform.GetSiblingIndex() - 1);
                if (dividerBeforeItems != null)
                    dividerBeforeItems.gameObject.SetActive(hasItems);
                tooltipItemsLabel.gameObject.SetActive(hasItems);
            }
            if (tooltipItemContainer != null)
            {
                tooltipItemContainer.gameObject.SetActive(hasItems);
            }

            if (!hasItems) return;

            // Create display-only item slots for combat items (no unequip during combat)
            for (int i = 0; i < combatUnit.items.Count; i++)
            {
                var serverItem = combatUnit.items[i];
                if (serverItem == null) continue;

                // Use combat unit's instance ID; pass -1 for index to disable unequip
                ServerTooltipItemSlot.Create(tooltipItemContainer, new Vector2(36, 36), serverItem, combatUnit.instanceId, -1);
            }
        }

        private void PopulateTooltipItems(UnitInstance unit)
        {
            // Clear existing item slots - destroy all children of container
            foreach (var slot in tooltipItemSlots)
            {
                if (slot != null && slot.gameObject != null)
                    Destroy(slot.gameObject);
            }
            tooltipItemSlots.Clear();

            // Also destroy any orphaned children (like server item slots)
            if (tooltipItemContainer != null)
            {
                foreach (Transform child in tooltipItemContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            bool hasItems = unit.equippedItems != null && unit.equippedItems.Count > 0;

            // Show/hide items section based on whether unit has items
            if (tooltipItemsLabel != null)
            {
                // Find the divider before items label and hide it too
                Transform dividerBeforeItems = tooltipItemsLabel.transform.parent.GetChild(
                    tooltipItemsLabel.transform.GetSiblingIndex() - 1);
                if (dividerBeforeItems != null)
                    dividerBeforeItems.gameObject.SetActive(hasItems);
                tooltipItemsLabel.gameObject.SetActive(hasItems);
            }
            if (tooltipItemContainer != null)
            {
                tooltipItemContainer.gameObject.SetActive(hasItems);
            }

            if (!hasItems) return;

            // Create item slots
            for (int i = 0; i < unit.equippedItems.Count; i++)
            {
                var item = unit.equippedItems[i];
                if (item == null) continue;

                var slot = TooltipItemSlot.Create(tooltipItemContainer, new Vector2(36, 36), item, unit, i);
                tooltipItemSlots.Add(slot);
            }
        }

        /// <summary>
        /// Populate tooltip items for server units (multiplayer mode - display only)
        /// </summary>
        private void PopulateTooltipItems(ServerUnitData serverUnit)
        {
            // Clear existing item slots - destroy all children of container
            foreach (var slot in tooltipItemSlots)
            {
                if (slot != null && slot.gameObject != null)
                    Destroy(slot.gameObject);
            }
            tooltipItemSlots.Clear();

            // Also destroy any orphaned children (like server item slots)
            if (tooltipItemContainer != null)
            {
                foreach (Transform child in tooltipItemContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            bool hasItems = serverUnit.items != null && serverUnit.items.Count > 0;

            // Show/hide items section based on whether unit has items
            if (tooltipItemsLabel != null)
            {
                // Find the divider before items label and hide it too
                Transform dividerBeforeItems = tooltipItemsLabel.transform.parent.GetChild(
                    tooltipItemsLabel.transform.GetSiblingIndex() - 1);
                if (dividerBeforeItems != null)
                    dividerBeforeItems.gameObject.SetActive(hasItems);
                tooltipItemsLabel.gameObject.SetActive(hasItems);
            }
            if (tooltipItemContainer != null)
            {
                tooltipItemContainer.gameObject.SetActive(hasItems);
            }

            if (!hasItems) return;

            // Create draggable item slots for server items
            for (int i = 0; i < serverUnit.items.Count; i++)
            {
                var serverItem = serverUnit.items[i];
                if (serverItem == null) continue;
                ServerTooltipItemSlot.Create(tooltipItemContainer, new Vector2(36, 36), serverItem, serverUnit.instanceId, i);
            }
        }

        /// <summary>
        /// Called when an item is unequipped from the tooltip
        /// </summary>
        public void OnItemUnequipped(UnitInstance unit, ItemData item)
        {
            if (unit == null || item == null) return;

            // Add item back to inventory
            GameState.Instance?.itemInventory.Add(item);

            // Refresh displays
            RefreshItemInventory();
            RefreshBench();

            // Refresh tooltip if still showing this unit
            if (tooltipUnit == unit && tooltipPanel.gameObject.activeSelf)
            {
                PopulateTooltipItems(unit);
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
            showingTemporaryItemInfo = false;
            tooltipUnit = null;
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
                if (benchPanel != null)
                {
                    benchPanel.sizeDelta = new Vector2(0, 90);
                    benchPanel.anchoredPosition = new Vector2(0, 220); // Bench above shop
                }
                
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
                if (benchPanel != null)
                {
                    benchPanel.sizeDelta = new Vector2(0, 85);
                    benchPanel.anchoredPosition = new Vector2(0, 190);
                }
                
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

            // Multiplayer mode - send to server
            if (IsMultiplayer)
            {
                serverState.BuyUnit(index);
                Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
                return;
            }

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
            // Only play click sound - tooltip pinning is handled by UnitCardUI.OnPointerClick
            // Unit fielding should only happen via drag-and-drop to hexes
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
        }

        private void OnRerollClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            // Multiplayer mode - send to server
            if (IsMultiplayer)
            {
                serverState.Reroll();
                Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
                return;
            }

            pendingAction = () => {
                state.RerollShop();
                Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
            };
        }

        private void OnBuyXPClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            // Multiplayer mode - send to server
            if (IsMultiplayer)
            {
                serverState.BuyXP();
                Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
                return;
            }

            pendingAction = () => {
                state.BuyXP();
                Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
            };
        }

        private void OnLockClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            // Multiplayer mode - send to server
            if (IsMultiplayer)
            {
                serverState.ToggleShopLock();
                return;
            }

            if (state.shop != null)
            {
                state.shop.isLocked = !state.shop.isLocked;
            }
        }

        private void OnFightClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            // Multiplayer mode - toggle ready state
            if (IsMultiplayer)
            {
                serverState.SetReady(!serverState.isReady);
                return;
            }

            pendingAction = () => RoundManager.Instance.StartCombatPhase();
        }

        private void OnScoutClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            if (ScoutingUI.Instance != null)
            {
                ScoutingUI.Instance.Toggle();
            }
        }

        private void OnProgressClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            if (RoundProgressionUI.Instance != null)
            {
                RoundProgressionUI.Instance.Toggle();
            }
        }

        private void OnMyBoardClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            // Focus camera on player's own board
            if (Crestforge.Visuals.IsometricCameraSetup.Instance != null)
            {
                Crestforge.Visuals.IsometricCameraSetup.Instance.FocusOnPlayerBoard();
            }
        }

        private void OnCrestSelected(CrestData crest)
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();
            Crestforge.Visuals.AudioManager.Instance?.PlayLevelUp();

            RoundManager.Instance.OnCrestSelected(crest);

            // Update the crest display UI
            UpdateCrestDisplay();
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

            // Restart with same game mode
            RoundManager.Instance.StartGame(state.currentGameMode);
        }

        private void OnReturnToMenuClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            // Return to main menu to allow game mode selection
            if (MainMenuUI.Instance != null)
            {
                gameObject.SetActive(false);
                MainMenuUI.Instance.Show();
            }
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        /// <summary>
        /// Show item info temporarily for a server item (multiplayer mode)
        /// </summary>
        public void ShowServerItemInfoTemporary(ServerItemData serverItem)
        {
            if (serverItem == null || tooltipPanel == null) return;

            showingTemporaryItemInfo = true;
            PopulateServerItemTooltip(serverItem);
        }

        /// <summary>
        /// Populate tooltip with server item info
        /// </summary>
        private void PopulateServerItemTooltip(ServerItemData serverItem)
        {
            tooltipPanel.gameObject.SetActive(true);

            // Title - show item name with gold color
            tooltipTitle.text = serverItem.name ?? "Item";
            tooltipTitle.color = new Color(1f, 0.85f, 0.4f);  // Gold for items

            // Show "Item" label in cost field
            tooltipCost.text = "Item";
            tooltipCost.color = new Color(0.7f, 0.7f, 0.7f);

            // Description goes in traits field (cleaner look)
            tooltipTraits.text = serverItem.description ?? "";
            tooltipTraits.color = new Color(0.9f, 0.9f, 0.9f);

            // Stats from server item stats
            string stats = "";
            if (serverItem.stats != null)
            {
                var s = serverItem.stats;
                if (s.health > 0) stats += $"+{s.health} Health\n";
                if (s.attack > 0) stats += $"+{s.attack} Attack\n";
                if (s.armor > 0) stats += $"+{s.armor} Armor\n";
                if (s.magicResist > 0) stats += $"+{s.magicResist} Magic Resist\n";
                if (s.attackSpeedPercent > 0) stats += $"+{s.attackSpeedPercent:F0}% Attack Speed\n";
                if (s.mana > 0) stats += $"+{s.mana} Mana\n";
                if (s.range > 0) stats += $"+{s.range} Range\n";
                if (s.abilityPower > 0) stats += $"+{s.abilityPower} Ability Power\n";
                if (s.critChance > 0) stats += $"+{s.critChance}% Crit Chance\n";
                if (s.critDamagePercent > 0) stats += $"+{s.critDamagePercent}% Crit Damage\n";
            }
            tooltipStats.text = stats.Length > 0 ? stats.TrimEnd() : "";

            // Hide ability field for items (description already shown in traits)
            tooltipAbility.text = "";

            // Hide items section for item tooltip
            if (tooltipItemsLabel != null)
            {
                int siblingIndex = tooltipItemsLabel.transform.GetSiblingIndex();
                if (siblingIndex > 0)
                {
                    Transform dividerBeforeItems = tooltipItemsLabel.transform.parent.GetChild(siblingIndex - 1);
                    if (dividerBeforeItems != null)
                        dividerBeforeItems.gameObject.SetActive(false);
                }
                tooltipItemsLabel.gameObject.SetActive(false);
            }
            if (tooltipItemContainer != null)
            {
                tooltipItemContainer.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Simple hover trigger for server item tooltips in multiplayer mode
    /// </summary>
    public class ServerItemTooltipTrigger : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerClickHandler
    {
        public ServerItemData serverItem;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Could show a mini hover tooltip here
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Could hide mini hover tooltip here
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Show item info when clicked
            if (serverItem != null)
            {
                GameUI.Instance?.ShowServerItemInfoTemporary(serverItem);
            }
        }
    }
}