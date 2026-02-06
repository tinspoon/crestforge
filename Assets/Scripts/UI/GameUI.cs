using UnityEngine;
using UnityEngine.UI;
using System.Collections;
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
        private TraitData hoveredTrait = null;
        private GameObject traitTooltipBlocker = null;
        private List<GameObject> traitTooltipTierEntries = new List<GameObject>();
        private List<GameObject> crestIconEntries = new List<GameObject>();
        private GameObject crestTooltipBlocker = null;
        private bool crestTooltipShowing = false;
        private GameObject activeCrestIcon = null;
        private UnitInstance tooltipUnit = null;
        private List<TooltipItemSlot> tooltipItemSlots = new List<TooltipItemSlot>();
        private bool showingTemporaryItemInfo = false;

        // Sell overlay
        private GameObject sellOverlay;
        private Text sellText;
        private UnitInstance unitBeingDragged;
        private bool isSellModeActive = false;

        // Unit detail panel (mobile bottom sheet)
        private RectTransform unitDetailPanel;
        private Image unitDetailSprite;
        private Text unitDetailName;
        private Text unitDetailCost;
        private Text unitDetailStats;
        private Text unitDetailTraits;
        private Text unitDetailAbility;
        private bool unitDetailVisible = false;
        private int detailPanelShopIndex = -1;

        // Multiplayer helper
        private bool IsMultiplayer => ServerGameState.Instance != null && ServerGameState.Instance.IsInGame;
        private ServerGameState serverState => ServerGameState.Instance;

        // Optimistic updates - track pending changes before server confirms
        private int optimisticGoldDelta = 0;
        private int optimisticFreeRerollDelta = 0;
        private HashSet<int> pendingShopPurchases = new HashSet<int>(); // Shop slots with pending purchases

        // Reroll animation state
        private bool waitingForRerollResponse = false;
        private Coroutine rerollAnimationCoroutine;

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
                ServerGameState.Instance.OnStateUpdated += ClearOptimisticGold;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from server events
            if (ServerGameState.Instance != null)
            {
                ServerGameState.Instance.OnActionResult -= HandleActionResult;
                ServerGameState.Instance.OnGameEnded -= HandleGameEnded;
                ServerGameState.Instance.OnStateUpdated -= ClearOptimisticGold;
            }
        }

        private void ClearOptimisticGold()
        {
            optimisticGoldDelta = 0;
            optimisticFreeRerollDelta = 0;
            pendingShopPurchases.Clear();
        }

        /// <summary>
        /// Apply optimistic gold change for selling a unit (before server confirms)
        /// </summary>
        public void ApplyOptimisticSellGold(int sellValue)
        {
            optimisticGoldDelta += sellValue;
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
            bool detailPanelActive = unitDetailVisible && unitDetailPanel != null && unitDetailPanel.gameObject.activeSelf;
            if (Input.GetMouseButtonDown(0) && (itemTooltipActive || crestTooltipActive || detailPanelActive))
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

                    // Check if any hit object is a UnitCardUI, ItemSlotUI, CrestSlotUI, TooltipItemSlot, or the tooltip/detail panel itself
                    bool clickedOnTooltipSource = false;
                    bool clickedOnDetailPanel = false;
                    foreach (var result in results)
                    {
                        if (result.gameObject.GetComponentInParent<UnitCardUI>() != null ||
                            result.gameObject.GetComponentInParent<ItemSlotUI>() != null ||
                            result.gameObject.GetComponentInParent<CrestSlotUI>() != null ||
                            result.gameObject.GetComponentInParent<TooltipItemSlot>() != null ||
                            (tooltipPanel != null && result.gameObject.transform.IsChildOf(tooltipPanel.transform)))
                        {
                            clickedOnTooltipSource = true;
                        }
                        if (unitDetailPanel != null && result.gameObject.transform.IsChildOf(unitDetailPanel.transform))
                        {
                            clickedOnDetailPanel = true;
                        }
                    }

                    if (!clickedOnTooltipSource)
                    {
                        isTooltipPinned = false;

                        showingTemporaryItemInfo = false;
                        tooltipUnit = null;
                        HideTooltip();
                        HideCrestTooltip();
                    }

                    // Dismiss detail panel when clicking outside it (but not on shop cards)
                    if (detailPanelActive && !clickedOnDetailPanel && !clickedOnTooltipSource)
                    {
                        HideUnitDetailPanel();
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
                mainCanvas.sortingOrder = 100;
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
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
            CreateUnitDetailPanel();
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

        /// <summary>
        /// Offset the top bar downward to make room for other UI (e.g., scout panel).
        /// </summary>
        public void SetTopBarOffset(float yOffset)
        {
            if (topBar != null)
            {
                topBar.anchoredPosition = new Vector2(0, -yOffset);
            }
            // Reposition crest panel below top bar (80px) + 5px gap
            if (crestPanel != null)
            {
                float crestY = -(yOffset + 80f + 5f);
                crestPanel.anchoredPosition = new Vector2(4, crestY);
            }
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
            // Shop panel at bottom of screen - mobile optimized layout
            // Layout: [Reroll] [Cards] [XP] in a single row
            // No background - transparent panel
            GameObject shopObj = new GameObject("ShopPanel");
            shopObj.transform.SetParent(parent, false);
            shopPanel = shopObj.AddComponent<RectTransform>();
            shopPanel.anchorMin = new Vector2(0, 0);
            shopPanel.anchorMax = new Vector2(1, 0);
            shopPanel.pivot = new Vector2(0.5f, 0);
            shopPanel.sizeDelta = new Vector2(0, 240); // Height for cards
            shopPanel.anchoredPosition = new Vector2(0, 120); // Margin from bottom
            // No Image component = transparent background

            // Also assign to bottomPanel for compatibility
            bottomPanel = shopPanel;

            // Calculate dimensions based on screen width
            float screenWidth = Screen.width;
            float buttonSize = 105f; // Square buttons
            float sidePadding = 8f;
            float cardSpacing = 24f; // Larger gap between cards

            // Available width for cards = screen - (2 buttons) - (2 side padding)
            float cardsAreaWidth = screenWidth - (buttonSize * 2) - (sidePadding * 2);
            float cardWidth = (cardsAreaWidth - (cardSpacing * 3)) / 4f;
            cardWidth = Mathf.Clamp(cardWidth, 90f, 140f); // Bigger cards
            float cardHeight = 200f; // Taller cards

            // Left button - Buy XP (swapped)
            buyXPButton = CreateShopSideButton("+4 XP\n$4", shopPanel, buttonSize, buttonSize, OnBuyXPClicked);
            RectTransform buyXPRT = buyXPButton.GetComponent<RectTransform>();
            buyXPRT.anchorMin = new Vector2(0, 0f);
            buyXPRT.anchorMax = new Vector2(0, 0f);
            buyXPRT.pivot = new Vector2(0, 0f);
            buyXPRT.anchoredPosition = new Vector2(sidePadding, 20);

            // Right button - Reroll (swapped)
            rerollButton = CreateShopSideButton("Reroll\n$2", shopPanel, buttonSize, buttonSize, OnRerollClicked);
            RectTransform rerollRT = rerollButton.GetComponent<RectTransform>();
            rerollRT.anchorMin = new Vector2(1, 0f);
            rerollRT.anchorMax = new Vector2(1, 0f);
            rerollRT.pivot = new Vector2(1, 0f);
            rerollRT.anchoredPosition = new Vector2(-sidePadding, 20);

            // Shop slots container - centered horizontally, anchored to bottom
            GameObject slotsObj = new GameObject("ShopSlots");
            slotsObj.transform.SetParent(shopPanel.transform, false);
            shopSlotContainer = slotsObj.AddComponent<RectTransform>();
            // Anchor to bottom-center of panel
            shopSlotContainer.anchorMin = new Vector2(0.5f, 0f);
            shopSlotContainer.anchorMax = new Vector2(0.5f, 0f);
            shopSlotContainer.pivot = new Vector2(0.5f, 0f);
            shopSlotContainer.anchoredPosition = new Vector2(0, 20); // 20px up from panel bottom

            HorizontalLayoutGroup slotsHLG = slotsObj.AddComponent<HorizontalLayoutGroup>();
            slotsHLG.spacing = cardSpacing;
            slotsHLG.childAlignment = TextAnchor.MiddleCenter;
            slotsHLG.childControlWidth = false;
            slotsHLG.childControlHeight = false;
            slotsHLG.childForceExpandWidth = false;
            slotsHLG.childForceExpandHeight = false;

            // Content size fitter to size container to its children
            ContentSizeFitter slotsFitter = slotsObj.AddComponent<ContentSizeFitter>();
            slotsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            slotsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create shop card slots (4 cards) with calculated dimensions
            for (int i = 0; i < GameConstants.Economy.SHOP_SIZE; i++)
            {
                var card = CreateUnitCard(shopSlotContainer, i, true, cardWidth, cardHeight);
                shopCards.Add(card);
            }

            // Lock button - small toggle in corner of reroll button
            CreateLockToggle(rerollButton.transform);

            // Hide shop initially - will be shown when game starts
            shopPanel.gameObject.SetActive(false);

            // Create sell overlay as child of shop panel (fills same area)
            CreateSellOverlay(shopPanel.transform);
        }

        private Button CreateShopSideButton(string text, Transform parent, float width, float height, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject($"Btn_{text.Replace("\n", "")}");
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);

            Image bg = btnObj.AddComponent<Image>();
            bg.sprite = CreateRoundedRectSprite(8);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.2f, 0.2f, 0.25f, 0.95f);

            // Border
            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(4, 4);
            textRT.offsetMax = new Vector2(-4, -4);

            Text btnText = textObj.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.text = text;
            btnText.fontSize = 14;
            btnText.fontStyle = FontStyle.Bold;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);

            // Color transitions
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.25f, 0.95f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.35f, 0.95f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            colors.selectedColor = new Color(0.25f, 0.25f, 0.3f, 0.95f);
            btn.colors = colors;

            return btn;
        }

        private void CreateLockToggle(Transform parent)
        {
            // Small lock toggle button in top-right corner of reroll button
            GameObject lockObj = new GameObject("LockToggle");
            lockObj.transform.SetParent(parent, false);
            RectTransform rt = lockObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(28, 28);
            rt.anchoredPosition = new Vector2(4, 4);

            Image bg = lockObj.AddComponent<Image>();
            bg.color = new Color(0.3f, 0.3f, 0.35f, 0.9f);

            // Lock icon text
            GameObject textObj = new GameObject("Icon");
            textObj.transform.SetParent(lockObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            Text iconText = textObj.AddComponent<Text>();
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.text = "🔓";
            iconText.fontSize = 14;
            iconText.alignment = TextAnchor.MiddleCenter;

            lockButton = lockObj.AddComponent<Button>();
            lockButton.targetGraphic = bg;
            lockButton.onClick.AddListener(OnLockClicked);
        }

        private Button CreateShopActionButton(string text, Transform parent, float width, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = CreatePanel($"Btn_{text}", parent);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 44);

            Image bg = btnObj.GetComponent<Image>();
            bg.color = new Color(0.25f, 0.25f, 0.32f);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 44;
            le.minHeight = 44; // Touch target minimum

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            Text btnText = textObj.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.text = text;
            btnText.fontSize = 14;
            btnText.fontStyle = FontStyle.Bold;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);

            // Color transition for feedback
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.25f, 0.25f, 0.32f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.42f);
            colors.pressedColor = new Color(0.2f, 0.2f, 0.25f);
            colors.selectedColor = new Color(0.3f, 0.3f, 0.38f);
            btn.colors = colors;

            return btn;
        }

        private void CreateSellOverlay(Transform canvasParent)
        {
            // Sell overlay - extends from top of shop panel to bottom of screen
            sellOverlay = new GameObject("SellOverlay");
            sellOverlay.transform.SetParent(mainCanvas.transform, false);

            RectTransform rt = sellOverlay.AddComponent<RectTransform>();
            // Anchor to bottom, full width
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            // Height = shop panel height + shop panel offset from bottom
            float sellHeight = shopPanel.sizeDelta.y + shopPanel.anchoredPosition.y;
            rt.sizeDelta = new Vector2(0, sellHeight);
            rt.anchoredPosition = Vector2.zero; // Sits at bottom of screen

            // Semi-transparent dark red background
            Image bg = sellOverlay.AddComponent<Image>();
            bg.color = new Color(0.5f, 0.15f, 0.1f, 0.92f);
            bg.raycastTarget = true;

            // Add drop handler for selling
            SellDropZone dropZone = sellOverlay.AddComponent<SellDropZone>();

            // Top border line (gold accent)
            GameObject topBorder = new GameObject("TopBorder");
            topBorder.transform.SetParent(sellOverlay.transform, false);
            RectTransform topBorderRT = topBorder.AddComponent<RectTransform>();
            topBorderRT.anchorMin = new Vector2(0, 1);
            topBorderRT.anchorMax = new Vector2(1, 1);
            topBorderRT.pivot = new Vector2(0.5f, 1);
            topBorderRT.sizeDelta = new Vector2(0, 4);
            topBorderRT.anchoredPosition = Vector2.zero;
            Image topBorderImg = topBorder.AddComponent<Image>();
            topBorderImg.color = new Color(1f, 0.75f, 0.3f, 1f);
            topBorderImg.raycastTarget = false;

            // Inner border frame
            GameObject borderFrame = new GameObject("BorderFrame");
            borderFrame.transform.SetParent(sellOverlay.transform, false);
            RectTransform borderRT = borderFrame.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(12, 12);
            borderRT.offsetMax = new Vector2(-12, -12);
            Image borderImg = borderFrame.AddComponent<Image>();
            borderImg.color = Color.clear;
            borderImg.raycastTarget = false;
            Outline borderOutline = borderFrame.AddComponent<Outline>();
            borderOutline.effectColor = new Color(1f, 0.7f, 0.3f, 0.6f);
            borderOutline.effectDistance = new Vector2(2, -2);

            // Sell text - centered in the overlay
            GameObject textObj = new GameObject("SellText");
            textObj.transform.SetParent(sellOverlay.transform, false);

            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(20, 20);
            textRT.offsetMax = new Vector2(-20, -20);

            sellText = textObj.AddComponent<Text>();
            sellText.text = "SELL UNIT\n$0";
            sellText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sellText.fontSize = 32;
            sellText.fontStyle = FontStyle.Bold;
            sellText.alignment = TextAnchor.MiddleCenter;
            sellText.color = new Color(1f, 0.9f, 0.4f);

            // Add shadow for depth
            Shadow shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(3, -3);

            // Add outline for visibility
            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.1f, 0.05f, 1f);
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
            vlg.padding = new RectOffset(24, 24, 24, 24);
            vlg.spacing = 12;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Header row (sprite + name/cost)
            GameObject headerRow = new GameObject("HeaderRow");
            headerRow.transform.SetParent(tooltipObj.transform, false);
            RectTransform headerRT = headerRow.AddComponent<RectTransform>();
            LayoutElement headerLE = headerRow.AddComponent<LayoutElement>();
            headerLE.minHeight = 120;
            headerLE.preferredHeight = 120;
            headerLE.preferredWidth = 500;
            HorizontalLayoutGroup headerHLG = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerHLG.spacing = 20;
            headerHLG.childAlignment = TextAnchor.MiddleLeft;
            headerHLG.childControlWidth = true;
            headerHLG.childControlHeight = true;
            headerHLG.childForceExpandWidth = false;
            headerHLG.childForceExpandHeight = true;

            // Unit sprite container
            GameObject spriteContainer = CreatePanel("SpriteContainer", headerRow.transform);
            spriteContainer.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
            LayoutElement spriteLE = spriteContainer.AddComponent<LayoutElement>();
            spriteLE.minWidth = 112;
            spriteLE.minHeight = 112;
            spriteLE.preferredWidth = 112;
            spriteLE.preferredHeight = 112;

            tooltipSprite = new GameObject("TooltipSprite").AddComponent<Image>();
            tooltipSprite.transform.SetParent(spriteContainer.transform, false);
            RectTransform spriteRT = tooltipSprite.GetComponent<RectTransform>();
            spriteRT.anchorMin = Vector2.zero;
            spriteRT.anchorMax = Vector2.one;
            spriteRT.offsetMin = new Vector2(8, 8);
            spriteRT.offsetMax = new Vector2(-8, -8);
            tooltipSprite.preserveAspect = true;

            // Name and cost column
            GameObject nameCol = new GameObject("NameCol");
            nameCol.transform.SetParent(headerRow.transform, false);
            RectTransform nameColRT = nameCol.AddComponent<RectTransform>();
            LayoutElement nameColLE = nameCol.AddComponent<LayoutElement>();
            nameColLE.flexibleWidth = 1;
            nameColLE.minWidth = 300;
            VerticalLayoutGroup nameVLG = nameCol.AddComponent<VerticalLayoutGroup>();
            nameVLG.spacing = 4;
            nameVLG.childControlWidth = true;
            nameVLG.childControlHeight = true;
            nameVLG.childForceExpandWidth = true;
            nameVLG.childForceExpandHeight = false;

            // Title
            tooltipTitle = CreateTooltipText("Unit Name", nameCol.transform, 36, FontStyle.Bold, Color.white, 48);

            // Cost
            tooltipCost = CreateTooltipText("$0 ★★★", nameCol.transform, 28, FontStyle.Normal, new Color(1f, 0.85f, 0.3f), 40);

            // Divider 1
            CreateTooltipDivider(tooltipObj.transform);

            // Stats label
            CreateTooltipText("STATS", tooltipObj.transform, 20, FontStyle.Bold, new Color(0.55f, 0.55f, 0.65f), 28);

            // Stats text
            tooltipStats = CreateTooltipText("HP: 0  ATK: 0", tooltipObj.transform, 24, FontStyle.Normal, Color.white, 96);

            // Divider 2
            CreateTooltipDivider(tooltipObj.transform);

            // Traits label
            CreateTooltipText("TRAITS", tooltipObj.transform, 20, FontStyle.Bold, new Color(0.55f, 0.55f, 0.65f), 28);

            // Traits text
            tooltipTraits = CreateTooltipText("Trait1, Trait2", tooltipObj.transform, 24, FontStyle.Normal, new Color(0.5f, 0.85f, 0.5f), 36);

            // Divider 3
            CreateTooltipDivider(tooltipObj.transform);

            // Ability label
            CreateTooltipText("ABILITY", tooltipObj.transform, 20, FontStyle.Bold, new Color(0.55f, 0.55f, 0.65f), 28);

            // Ability text
            tooltipAbility = CreateTooltipText("Ability Name", tooltipObj.transform, 24, FontStyle.Normal, new Color(0.7f, 0.7f, 0.95f), 80);

            // Items section (hidden when no items)
            CreateTooltipDivider(tooltipObj.transform);

            // Items label
            tooltipItemsLabel = CreateTooltipText("ITEMS", tooltipObj.transform, 20, FontStyle.Bold, new Color(0.55f, 0.55f, 0.65f), 28);

            // Items container - horizontal layout for item slots
            GameObject itemContainer = new GameObject("ItemContainer");
            itemContainer.transform.SetParent(tooltipObj.transform, false);
            tooltipItemContainer = itemContainer.AddComponent<RectTransform>();
            HorizontalLayoutGroup itemHLG = itemContainer.AddComponent<HorizontalLayoutGroup>();
            itemHLG.spacing = 12;
            itemHLG.childAlignment = TextAnchor.MiddleLeft;
            itemHLG.childControlWidth = false;
            itemHLG.childControlHeight = false;
            itemHLG.childForceExpandWidth = false;
            itemHLG.childForceExpandHeight = false;
            LayoutElement itemContainerLE = itemContainer.AddComponent<LayoutElement>();
            itemContainerLE.minHeight = 80;
            itemContainerLE.preferredHeight = 80;
            itemContainerLE.preferredWidth = 500;

            tooltipPanel.gameObject.SetActive(false);
        }

        private void CreateTooltipDivider(Transform parent)
        {
            GameObject divider = CreatePanel("Divider", parent);
            divider.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.45f);
            LayoutElement le = divider.AddComponent<LayoutElement>();
            le.minHeight = 2;
            le.preferredHeight = 2;
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
            le.preferredWidth = 500;

            return text;
        }

        private void CreateTraitPanel()
        {
            // Minimal container on left side - no background, no title
            // Just a vertical column of trait icons that grows upward from above the shop
            GameObject panelObj = new GameObject("TraitPanel");
            panelObj.transform.SetParent(mainCanvas.transform, false);
            traitPanel = panelObj.AddComponent<RectTransform>();

            // Anchor to bottom-left, positioned just above the shop panel
            traitPanel.anchorMin = new Vector2(0, 0);
            traitPanel.anchorMax = new Vector2(0, 0);
            traitPanel.pivot = new Vector2(0, 0);
            traitPanel.sizeDelta = new Vector2(104, 0); // Width for icons, height auto-sized
            traitPanel.anchoredPosition = new Vector2(4, 365); // Above shop (shop top ~360)

            // Content size fitter so height grows with entries
            ContentSizeFitter csf = panelObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Vertical layout - entries stack upward (LowerLeft alignment + pivot at bottom)
            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.LowerCenter;

            // traitContent points to the same object (entries added directly)
            traitContent = traitPanel;

            // Start hidden - will show when traits are present
            traitPanel.gameObject.SetActive(false);
        }

        private void CreateTraitTooltip()
        {
            // Trait tooltip panel - appears when tapping trait icons
            GameObject tooltipObj = CreatePanel("TraitTooltip", mainCanvas.transform);
            traitTooltipPanel = tooltipObj.GetComponent<RectTransform>();

            // Position to the right of the trait icon column
            traitTooltipPanel.anchorMin = new Vector2(0, 0);
            traitTooltipPanel.anchorMax = new Vector2(0, 0);
            traitTooltipPanel.pivot = new Vector2(0, 0);
            traitTooltipPanel.anchoredPosition = new Vector2(64, 365);
            
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
            vlg.padding = new RectOffset(24, 24, 20, 20);
            vlg.spacing = 12;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title - larger
            traitTooltipTitle = CreateTooltipText("Trait Name", tooltipObj.transform, 40, FontStyle.Bold, new Color(1f, 0.9f, 0.5f), 56);
            traitTooltipTitle.GetComponent<LayoutElement>().preferredWidth = 480;

            // Description
            traitTooltipDescription = CreateTooltipText("Description", tooltipObj.transform, 28, FontStyle.Italic, new Color(0.75f, 0.75f, 0.8f), 48);

            // Divider
            CreateTooltipDivider(tooltipObj.transform);

            // Tier bonuses label
            CreateTooltipText("TIER BONUSES", tooltipObj.transform, 24, FontStyle.Bold, new Color(0.6f, 0.6f, 0.7f), 36);

            // Tier container
            GameObject tierContainer = new GameObject("TierContainer");
            tierContainer.transform.SetParent(tooltipObj.transform, false);
            traitTooltipTierContainer = tierContainer.AddComponent<RectTransform>();
            VerticalLayoutGroup tierVlg = tierContainer.AddComponent<VerticalLayoutGroup>();
            tierVlg.spacing = 8;
            tierVlg.childControlWidth = true;
            tierVlg.childControlHeight = true;
            tierVlg.childForceExpandWidth = true;
            LayoutElement tierLE = tierContainer.AddComponent<LayoutElement>();
            tierLE.preferredWidth = 480;

            // Divider
            CreateTooltipDivider(tooltipObj.transform);

            // Units with this trait label
            CreateTooltipText("UNITS ON BOARD", tooltipObj.transform, 24, FontStyle.Bold, new Color(0.6f, 0.6f, 0.7f), 36);

            // Units list
            traitTooltipUnits = CreateTooltipText("None", tooltipObj.transform, 28, FontStyle.Normal, new Color(0.6f, 0.85f, 0.6f), 48);

            traitTooltipPanel.gameObject.SetActive(false);

            // Full-screen invisible blocker to catch taps outside the tooltip
            traitTooltipBlocker = new GameObject("TraitTooltipBlocker");
            traitTooltipBlocker.transform.SetParent(mainCanvas.transform, false);
            RectTransform blockerRT = traitTooltipBlocker.AddComponent<RectTransform>();
            blockerRT.anchorMin = Vector2.zero;
            blockerRT.anchorMax = Vector2.one;
            blockerRT.offsetMin = Vector2.zero;
            blockerRT.offsetMax = Vector2.zero;

            Image blockerImg = traitTooltipBlocker.AddComponent<Image>();
            blockerImg.color = Color.clear; // Invisible
            blockerImg.raycastTarget = true;

            Button blockerBtn = traitTooltipBlocker.AddComponent<Button>();
            blockerBtn.transition = Selectable.Transition.None;
            blockerBtn.onClick.AddListener(() => {
                TraitEntryUI.DeselectAll();
                HideTraitTooltip();
            });

            traitTooltipBlocker.SetActive(false);
        }

        private void CreateCrestPanel()
        {
            // Minimal container on top-left - no background, no title
            // Just a vertical column of crest icons that grows downward
            GameObject crestObj = new GameObject("CrestPanel");
            crestObj.transform.SetParent(mainCanvas.transform, false);
            crestPanel = crestObj.AddComponent<RectTransform>();

            // Anchor to top-left, grows downward
            crestPanel.anchorMin = new Vector2(0, 1);
            crestPanel.anchorMax = new Vector2(0, 1);
            crestPanel.pivot = new Vector2(0, 1);
            crestPanel.sizeDelta = new Vector2(104, 0); // Width for icons, height auto-sized
            crestPanel.anchoredPosition = new Vector2(4, -85); // Below top bar (80px) + 5px gap

            // Content size fitter so height grows with entries
            ContentSizeFitter csf = crestObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Vertical layout - icons stack downward
            VerticalLayoutGroup vlg = crestObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.UpperCenter;

            // Hide initially - will show when crests are present
            crestPanel.gameObject.SetActive(false);

            // Create crest tooltip
            CreateCrestTooltip();
        }

        private void CreateCrestTooltip()
        {
            // Crest tooltip panel - appears when tapping crest icons
            GameObject tooltipObj = CreatePanel("CrestTooltip", mainCanvas.transform);
            crestTooltipPanel = tooltipObj.GetComponent<RectTransform>();

            // Default position (will be set dynamically when shown)
            crestTooltipPanel.anchorMin = new Vector2(0, 1);
            crestTooltipPanel.anchorMax = new Vector2(0, 1);
            crestTooltipPanel.pivot = new Vector2(0, 0.5f);
            crestTooltipPanel.anchoredPosition = new Vector2(112, -85);

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
            vlg.padding = new RectOffset(24, 24, 20, 20);
            vlg.spacing = 12;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title
            crestTooltipTitle = CreateTooltipText("Crest Name", tooltipObj.transform, 36, FontStyle.Bold, new Color(0.8f, 0.6f, 1f), 52);
            crestTooltipTitle.GetComponent<LayoutElement>().preferredWidth = 440;

            // Type
            crestTooltipType = CreateTooltipText("Minor Crest", tooltipObj.transform, 24, FontStyle.Italic, new Color(0.6f, 0.6f, 0.7f), 36);

            // Divider
            CreateTooltipDivider(tooltipObj.transform);

            // Description
            crestTooltipDescription = CreateTooltipText("Description", tooltipObj.transform, 28, FontStyle.Normal, new Color(0.85f, 0.85f, 0.9f), 48);

            // Divider
            CreateTooltipDivider(tooltipObj.transform);

            // Bonus label
            CreateTooltipText("BONUS", tooltipObj.transform, 22, FontStyle.Bold, new Color(0.6f, 0.6f, 0.7f), 32);

            // Bonus text
            crestTooltipBonus = CreateTooltipText("Bonus effect", tooltipObj.transform, 26, FontStyle.Normal, new Color(0.7f, 0.9f, 0.7f), 40);

            crestTooltipPanel.gameObject.SetActive(false);

            // Full-screen invisible blocker to catch taps outside the tooltip
            crestTooltipBlocker = new GameObject("CrestTooltipBlocker");
            crestTooltipBlocker.transform.SetParent(mainCanvas.transform, false);
            RectTransform blockerRT = crestTooltipBlocker.AddComponent<RectTransform>();
            blockerRT.anchorMin = Vector2.zero;
            blockerRT.anchorMax = Vector2.one;
            blockerRT.offsetMin = Vector2.zero;
            blockerRT.offsetMax = Vector2.zero;

            Image blockerImg = crestTooltipBlocker.AddComponent<Image>();
            blockerImg.color = Color.clear;
            blockerImg.raycastTarget = true;

            Button blockerBtn = crestTooltipBlocker.AddComponent<Button>();
            blockerBtn.transition = Selectable.Transition.None;
            blockerBtn.onClick.AddListener(() => {
                DismissCrestTooltip();
            });

            crestTooltipBlocker.SetActive(false);
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

        private void CreateUnitDetailPanel()
        {
            // Full-width bottom sheet for unit details (mobile-friendly)
            GameObject panelObj = CreatePanel("UnitDetailPanel", mainCanvas.transform);
            unitDetailPanel = panelObj.GetComponent<RectTransform>();

            // Anchor to bottom, full width
            unitDetailPanel.anchorMin = new Vector2(0, 0);
            unitDetailPanel.anchorMax = new Vector2(1, 0);
            unitDetailPanel.pivot = new Vector2(0.5f, 0);
            unitDetailPanel.sizeDelta = new Vector2(0, 280);
            unitDetailPanel.anchoredPosition = new Vector2(0, 0);

            // Dark semi-transparent background
            Image panelBg = panelObj.GetComponent<Image>();
            panelBg.color = new Color(0.1f, 0.1f, 0.15f, 0.98f);

            // Add outline
            Outline outline = panelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.5f);
            outline.effectDistance = new Vector2(0, 2);

            // Main content layout
            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 16);
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Header row (sprite + name/cost)
            GameObject headerRow = new GameObject("HeaderRow");
            headerRow.transform.SetParent(panelObj.transform, false);
            RectTransform headerRT = headerRow.AddComponent<RectTransform>();
            LayoutElement headerLE = headerRow.AddComponent<LayoutElement>();
            headerLE.minHeight = 70;
            headerLE.preferredHeight = 70;
            HorizontalLayoutGroup headerHLG = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerHLG.spacing = 12;
            headerHLG.childAlignment = TextAnchor.MiddleLeft;
            headerHLG.childControlWidth = true;
            headerHLG.childControlHeight = true;
            headerHLG.childForceExpandWidth = false;
            headerHLG.childForceExpandHeight = true;

            // Unit sprite container
            GameObject spriteContainer = CreatePanel("SpriteContainer", headerRow.transform);
            spriteContainer.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
            LayoutElement spriteLE = spriteContainer.AddComponent<LayoutElement>();
            spriteLE.minWidth = 64;
            spriteLE.minHeight = 64;
            spriteLE.preferredWidth = 64;
            spriteLE.preferredHeight = 64;

            unitDetailSprite = new GameObject("DetailSprite").AddComponent<Image>();
            unitDetailSprite.transform.SetParent(spriteContainer.transform, false);
            RectTransform spriteRT = unitDetailSprite.GetComponent<RectTransform>();
            spriteRT.anchorMin = Vector2.zero;
            spriteRT.anchorMax = Vector2.one;
            spriteRT.offsetMin = new Vector2(4, 4);
            spriteRT.offsetMax = new Vector2(-4, -4);
            unitDetailSprite.preserveAspect = true;

            // Name and cost column
            GameObject nameCol = new GameObject("NameCol");
            nameCol.transform.SetParent(headerRow.transform, false);
            RectTransform nameColRT = nameCol.AddComponent<RectTransform>();
            LayoutElement nameColLE = nameCol.AddComponent<LayoutElement>();
            nameColLE.flexibleWidth = 1;
            nameColLE.minWidth = 150;
            VerticalLayoutGroup nameVLG = nameCol.AddComponent<VerticalLayoutGroup>();
            nameVLG.spacing = 4;
            nameVLG.childControlWidth = true;
            nameVLG.childControlHeight = true;
            nameVLG.childForceExpandWidth = true;
            nameVLG.childForceExpandHeight = false;

            // Name
            unitDetailName = CreateDetailText("Unit Name", nameCol.transform, 20, FontStyle.Bold, Color.white, 28);

            // Cost and stars
            unitDetailCost = CreateDetailText("$0 ★★★", nameCol.transform, 16, FontStyle.Normal, new Color(1f, 0.85f, 0.3f), 22);

            // Divider 1
            CreateDetailDivider(panelObj.transform);

            // Stats row
            GameObject statsRow = new GameObject("StatsRow");
            statsRow.transform.SetParent(panelObj.transform, false);
            LayoutElement statsLE = statsRow.AddComponent<LayoutElement>();
            statsLE.minHeight = 24;
            statsLE.preferredHeight = 24;
            HorizontalLayoutGroup statsHLG = statsRow.AddComponent<HorizontalLayoutGroup>();
            statsHLG.spacing = 20;
            statsHLG.childAlignment = TextAnchor.MiddleLeft;
            statsHLG.childControlWidth = false;
            statsHLG.childControlHeight = true;
            statsHLG.childForceExpandWidth = false;

            unitDetailStats = CreateDetailText("HP: 0  ATK: 0  DEF: 0  SPD: 0.0", statsRow.transform, 14, FontStyle.Normal, Color.white, 24);
            unitDetailStats.GetComponent<LayoutElement>().flexibleWidth = 1;

            // Divider 2
            CreateDetailDivider(panelObj.transform);

            // Traits row
            unitDetailTraits = CreateDetailText("Traits: None", panelObj.transform, 14, FontStyle.Normal, new Color(0.5f, 0.85f, 0.5f), 22);

            // Divider 3
            CreateDetailDivider(panelObj.transform);

            // Ability section
            GameObject abilityLabel = new GameObject("AbilityLabel");
            abilityLabel.transform.SetParent(panelObj.transform, false);
            Text abilityLabelText = abilityLabel.AddComponent<Text>();
            abilityLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            abilityLabelText.text = "ABILITY";
            abilityLabelText.fontSize = 11;
            abilityLabelText.fontStyle = FontStyle.Bold;
            abilityLabelText.color = new Color(0.55f, 0.55f, 0.65f);
            LayoutElement labelLE = abilityLabel.AddComponent<LayoutElement>();
            labelLE.minHeight = 16;
            labelLE.preferredHeight = 16;

            unitDetailAbility = CreateDetailText("Ability Name\nDescription", panelObj.transform, 13, FontStyle.Normal, new Color(0.7f, 0.7f, 0.95f), 50);

            // Dismiss hint at bottom
            Text dismissHint = CreateDetailText("Tap anywhere to dismiss", panelObj.transform, 11, FontStyle.Italic, new Color(0.5f, 0.5f, 0.55f), 20);
            dismissHint.alignment = TextAnchor.MiddleCenter;

            // Add click handler to dismiss panel
            Button dismissBtn = panelObj.AddComponent<Button>();
            dismissBtn.transition = Selectable.Transition.None;
            dismissBtn.onClick.AddListener(HideUnitDetailPanel);

            // Start hidden
            unitDetailPanel.gameObject.SetActive(false);
        }

        private Text CreateDetailText(string content, Transform parent, int fontSize, FontStyle style, Color color, float height)
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

            return text;
        }

        private void CreateDetailDivider(Transform parent)
        {
            GameObject divider = CreatePanel("Divider", parent);
            divider.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f);
            LayoutElement le = divider.AddComponent<LayoutElement>();
            le.minHeight = 1;
            le.preferredHeight = 1;
        }

        /// <summary>
        /// Show unit detail panel for a server shop unit (multiplayer mode)
        /// </summary>
        public void ShowUnitDetailPanel(ServerShopUnit serverUnit, int shopIndex)
        {
            if (serverUnit == null || unitDetailPanel == null) return;

            detailPanelShopIndex = shopIndex;

            // Get the unit template for additional info
            var template = serverState?.GetUnitTemplate(serverUnit.unitId);

            // Set sprite - prefer 3D portrait
            if (unitDetailSprite != null)
            {
                unitDetailSprite.sprite = UnitPortraitGenerator.GetPortrait(serverUnit.unitId, serverUnit.name);
                unitDetailSprite.enabled = true;
            }

            // Set name with cost color
            unitDetailName.text = serverUnit.name;
            unitDetailName.color = GetCostColor(serverUnit.cost);

            // Set cost
            unitDetailCost.text = $"${serverUnit.cost}";

            // Set stats from template
            if (template != null)
            {
                var stats = template.baseStats;
                unitDetailStats.text = $"HP: {stats.health}   ATK: {stats.attack}   DEF: {stats.armor}   SPD: {stats.attackSpeed:F1}";

                // Set traits
                if (serverUnit.traits != null && serverUnit.traits.Length > 0)
                {
                    unitDetailTraits.text = string.Join("  •  ", serverUnit.traits);
                }
                else if (template.traits != null)
                {
                    var traitNames = new List<string>();
                    foreach (var trait in template.traits)
                    {
                        if (trait != null) traitNames.Add(trait.traitName);
                    }
                    unitDetailTraits.text = traitNames.Count > 0 ? string.Join("  •  ", traitNames) : "None";
                }
                else
                {
                    unitDetailTraits.text = "None";
                }

                // Set ability
                if (template.ability != null)
                {
                    string abilityText = template.ability.abilityName;
                    if (!string.IsNullOrEmpty(template.ability.description))
                        abilityText += $"\n{template.ability.description}";
                    if (template.ability.baseDamage > 0)
                        abilityText += $"\nDamage: {template.ability.baseDamage}";
                    if (template.ability.baseHealing > 0)
                        abilityText += $"\nHealing: {template.ability.baseHealing}";
                    unitDetailAbility.text = abilityText;
                }
                else
                {
                    unitDetailAbility.text = "None";
                }
            }
            else
            {
                unitDetailStats.text = "Stats unavailable";
                unitDetailTraits.text = serverUnit.traits != null ? string.Join("  •  ", serverUnit.traits) : "None";
                unitDetailAbility.text = "Ability info unavailable";
            }

            // Show panel
            unitDetailPanel.gameObject.SetActive(true);
            unitDetailVisible = true;
        }

        /// <summary>
        /// Show unit detail panel for a local unit instance
        /// </summary>
        public void ShowUnitDetailPanel(UnitInstance unit, int shopIndex)
        {
            if (unit == null || unit.template == null || unitDetailPanel == null) return;

            detailPanelShopIndex = shopIndex;
            var t = unit.template;

            // Set sprite - prefer 3D portrait
            if (unitDetailSprite != null)
            {
                unitDetailSprite.sprite = UnitPortraitGenerator.GetPortrait(t.unitId, t.unitName);
                unitDetailSprite.enabled = true;
            }

            // Set name with cost color
            unitDetailName.text = t.unitName;
            unitDetailName.color = GetCostColor(t.cost);

            // Set cost and stars
            unitDetailCost.text = $"${t.cost}  " + new string('★', unit.starLevel);

            // Set stats
            var stats = unit.currentStats ?? t.baseStats;
            unitDetailStats.text = $"HP: {stats.health}   ATK: {stats.attack}   DEF: {stats.armor}   SPD: {stats.attackSpeed:F1}";

            // Set traits
            if (t.traits != null && t.traits.Length > 0)
            {
                var traitNames = new List<string>();
                foreach (var trait in t.traits)
                {
                    if (trait != null) traitNames.Add(trait.traitName);
                }
                unitDetailTraits.text = traitNames.Count > 0 ? string.Join("  •  ", traitNames) : "None";
            }
            else
            {
                unitDetailTraits.text = "None";
            }

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
                unitDetailAbility.text = abilityText;
            }
            else
            {
                unitDetailAbility.text = "None";
            }

            // Show panel
            unitDetailPanel.gameObject.SetActive(true);
            unitDetailVisible = true;
        }

        /// <summary>
        /// Hide the unit detail panel
        /// </summary>
        public void HideUnitDetailPanel()
        {
            if (unitDetailPanel != null)
            {
                unitDetailPanel.gameObject.SetActive(false);
            }
            unitDetailVisible = false;
            detailPanelShopIndex = -1;
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

            // Sort traits: inactive first (top of column), active last (bottom, closest to shop)
            // Within active: highest tier at the very bottom
            traitsToDisplay.Sort((a, b) => {
                int tierA = a.Key != null ? a.Key.GetActiveTier(a.Value) : -1;
                int tierB = b.Key != null ? b.Key.GetActiveTier(b.Value) : -1;
                bool activeA = tierA >= 0;
                bool activeB = tierB >= 0;
                if (activeA != activeB) return activeA.CompareTo(activeB); // Inactive first (top), active last (bottom)
                if (activeA && activeB) return tierA.CompareTo(tierB); // Lower tier higher up, highest tier at bottom
                return b.Value.CompareTo(a.Value); // Inactive: higher count first
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
            int activeTier = trait.GetActiveTier(count);
            bool isActive = activeTier >= 0;
            Color traitColor = GetTraitPanelColor(trait.traitId);

            // Entry is just a circle icon, same style as shop card trait icons
            float iconSize = 96f; // Large, easy to tap on mobile

            GameObject entryObj = new GameObject("TraitEntry_" + trait.traitName);
            entryObj.transform.SetParent(traitContent, false);
            traitEntries.Add(entryObj);

            RectTransform rt = entryObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            LayoutElement le = entryObj.AddComponent<LayoutElement>();
            le.preferredHeight = iconSize;
            le.preferredWidth = iconSize;
            le.minHeight = iconSize;

            // Circle background - same procedural circle as shop card icons
            Image bg = entryObj.AddComponent<Image>();
            bg.sprite = GetTraitPanelIconSprite();
            if (isActive)
            {
                bg.color = traitColor;
            }
            else
            {
                // Dim inactive traits
                bg.color = new Color(traitColor.r * 0.35f, traitColor.g * 0.35f, traitColor.b * 0.35f, 0.7f);
            }

            // Button for tap detection
            Button entryBtn = entryObj.AddComponent<Button>();
            entryBtn.transition = Selectable.Transition.None;
            entryBtn.targetGraphic = bg;

            // TraitEntryUI for hover/tap detection
            TraitEntryUI entryUI = entryObj.AddComponent<TraitEntryUI>();
            entryUI.trait = trait;
            entryUI.count = count;
            traitEntryComponents.Add(entryUI);

            // Abbreviation text (first 2 chars, same as shop cards)
            string abbrev = trait.traitName.Length >= 2
                ? trait.traitName.Substring(0, 2).ToUpper()
                : trait.traitName.ToUpper();

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(entryObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            Text label = textObj.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = abbrev;
            label.fontSize = 32;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = isActive ? Color.white : new Color(0.6f, 0.6f, 0.65f);
            label.raycastTarget = false;

            // Shadow for readability
            Shadow shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(1, -1);

            // Progress text below abbreviation (small count indicator)
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
                progressStr = nextThreshold > 0 ? $"{count}/{nextThreshold}" : $"{count}";
            }

            if (!string.IsNullOrEmpty(progressStr))
            {
                GameObject progressObj = new GameObject("Progress");
                progressObj.transform.SetParent(entryObj.transform, false);
                RectTransform progressRT = progressObj.AddComponent<RectTransform>();
                progressRT.anchorMin = new Vector2(0, 0);
                progressRT.anchorMax = new Vector2(1, 0.3f);
                progressRT.offsetMin = Vector2.zero;
                progressRT.offsetMax = Vector2.zero;

                Text progressText = progressObj.AddComponent<Text>();
                progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                progressText.text = progressStr;
                progressText.fontSize = 20;
                progressText.fontStyle = FontStyle.Bold;
                progressText.alignment = TextAnchor.MiddleCenter;
                progressText.color = isActive ? new Color(1f, 0.95f, 0.7f) : new Color(0.5f, 0.5f, 0.55f);
                progressText.raycastTarget = false;

                Shadow progressShadow = progressObj.AddComponent<Shadow>();
                progressShadow.effectColor = new Color(0, 0, 0, 0.8f);
                progressShadow.effectDistance = new Vector2(1, -1);
            }
        }

        // Trait color mapping for the panel icons (same as UnitCardUI)
        private static Color GetTraitPanelColor(string traitId)
        {
            if (string.IsNullOrEmpty(traitId)) return Color.gray;
            string id = traitId.ToLower();
            return id switch
            {
                "attuned"      => new Color(0.4f, 0.7f, 0.9f),
                "forged"       => new Color(0.85f, 0.5f, 0.2f),
                "scavenger"    => new Color(0.6f, 0.5f, 0.3f),
                "invigorating" => new Color(0.3f, 0.8f, 0.4f),
                "reflective"   => new Color(0.7f, 0.7f, 0.85f),
                "mitigation"   => new Color(0.5f, 0.5f, 0.7f),
                "bruiser"      => new Color(0.8f, 0.3f, 0.3f),
                "overkill"     => new Color(0.9f, 0.2f, 0.2f),
                "gigamega"     => new Color(0.6f, 0.3f, 0.8f),
                "firstblood"   => new Color(0.9f, 0.3f, 0.4f),
                "momentum"     => new Color(0.2f, 0.7f, 0.7f),
                "cleave"       => new Color(0.7f, 0.4f, 0.2f),
                "fury"         => new Color(0.9f, 0.4f, 0.1f),
                "volatile"     => new Color(0.8f, 0.8f, 0.2f),
                "treasure"     => new Color(0.95f, 0.75f, 0.25f),
                "crestmaker"   => new Color(0.3f, 0.5f, 0.9f),
                _              => new Color(0.5f, 0.5f, 0.55f)
            };
        }

        // Cached circle sprite for trait panel icons
        private static Sprite _traitPanelIconSprite;
        private static Sprite GetTraitPanelIconSprite()
        {
            if (_traitPanelIconSprite != null) return _traitPanelIconSprite;

            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            float center = size / 2f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius)
                    {
                        float edge = Mathf.Clamp01((radius - dist) * 2f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, edge);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _traitPanelIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _traitPanelIconSprite;
        }

        private void UpdateTraitTooltip()
        {
            if (traitTooltipPanel == null) return;

            // Check if mouse/touch is over a trait entry
            bool isOverTrait = false;
            TraitData currentTrait = null;
            int currentCount = 0;
            RectTransform hoveredEntryRT = null;

            foreach (var entry in traitEntryComponents)
            {
                if (entry != null && entry.IsHovered())
                {
                    isOverTrait = true;
                    currentTrait = entry.trait;
                    currentCount = entry.count;
                    hoveredEntryRT = entry.GetComponent<RectTransform>();
                    break;
                }
            }

            if (!isOverTrait || currentTrait == null)
            {
                traitTooltipPanel.gameObject.SetActive(false);
                if (traitTooltipBlocker != null) traitTooltipBlocker.SetActive(false);
                hoveredTrait = null;
                return;
            }

            // Show blocker behind tooltip (catches taps outside to dismiss)
            if (traitTooltipBlocker != null)
            {
                traitTooltipBlocker.SetActive(true);
                // Ensure blocker is behind trait panel and tooltip but in front of other UI
                traitTooltipBlocker.transform.SetSiblingIndex(traitPanel.transform.GetSiblingIndex());
                traitPanel.transform.SetAsLastSibling();
                traitTooltipPanel.transform.SetAsLastSibling();
            }

            // Show and update tooltip
            traitTooltipPanel.gameObject.SetActive(true);

            // Position tooltip to the right of the hovered icon
            if (hoveredEntryRT != null)
            {
                Vector3[] corners = new Vector3[4];
                hoveredEntryRT.GetWorldCorners(corners);
                // corners[0] = bottom-left, corners[2] = top-right (in screen pixels for overlay canvas)
                float iconRight = corners[2].x;
                float iconCenterY = (corners[0].y + corners[2].y) * 0.5f;

                // Convert screen-pixel position to canvas local coordinates
                RectTransform canvasRT = mainCanvas.GetComponent<RectTransform>();
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRT, new Vector2(iconRight + 8, iconCenterY),
                    null, out localPos);

                // localPos is relative to canvas pivot (center). Convert to bottom-left for anchors at (0,0).
                Vector2 anchoredPos = localPos + new Vector2(
                    canvasRT.rect.width * canvasRT.pivot.x,
                    canvasRT.rect.height * canvasRT.pivot.y);

                traitTooltipPanel.anchorMin = new Vector2(0, 0);
                traitTooltipPanel.anchorMax = new Vector2(0, 0);
                traitTooltipPanel.pivot = new Vector2(0, 0.5f);
                traitTooltipPanel.anchoredPosition = anchoredPos;
            }

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
                    entryHlg.spacing = 20;
                    entryHlg.childControlWidth = false;
                    entryHlg.childControlHeight = true;
                    entryHlg.childForceExpandWidth = false;
                    entryHlg.childAlignment = TextAnchor.MiddleLeft;
                    LayoutElement entryLE = tierEntry.AddComponent<LayoutElement>();
                    entryLE.preferredHeight = 56;

                    // Threshold indicator
                    Text thresholdText = new GameObject("Threshold").AddComponent<Text>();
                    thresholdText.transform.SetParent(tierEntry.transform, false);
                    thresholdText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    thresholdText.text = $"({threshold})";
                    thresholdText.fontSize = 32;
                    thresholdText.fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal;
                    thresholdText.alignment = TextAnchor.MiddleLeft;
                    LayoutElement threshLE = thresholdText.gameObject.AddComponent<LayoutElement>();
                    threshLE.preferredWidth = 84;

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
                    bonusText.fontSize = 28;
                    bonusText.alignment = TextAnchor.MiddleLeft;
                    LayoutElement bonusLE = bonusText.gameObject.AddComponent<LayoutElement>();
                    bonusLE.flexibleWidth = 1;
                    bonusLE.preferredWidth = 440;

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
            if (traitTooltipBlocker != null)
            {
                traitTooltipBlocker.SetActive(false);
            }
            hoveredTrait = null;
        }

        private UnitCardUI CreateUnitCard(Transform parent, int index, bool isShopCard, float cardWidth = 0, float cardHeight = 0)
        {
            // Use provided dimensions or calculate defaults
            if (cardWidth <= 0 || cardHeight <= 0)
            {
                // Default calculation for non-shop cards (bench, etc.)
                cardWidth = 80f;
                cardHeight = 100f;
            }

            GameObject cardObj = new GameObject($"Card_{index}");
            cardObj.transform.SetParent(parent, false);
            RectTransform rt = cardObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(cardWidth, cardHeight);

            LayoutElement le = cardObj.AddComponent<LayoutElement>();
            le.preferredWidth = cardWidth;
            le.preferredHeight = cardHeight;

            // Background with rounded corners - use gradient image
            Image bg = cardObj.AddComponent<Image>();
            bg.sprite = CreateRoundedRectSprite(10); // 10px corner radius (more rounded for border)
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.22f, 0.22f, 0.28f); // Neutral base color

            // Black border outline
            Outline borderOutline = cardObj.AddComponent<Outline>();
            borderOutline.effectColor = Color.black;
            borderOutline.effectDistance = new Vector2(2, -2);

            // Second outline for thicker border effect
            Outline borderOutline2 = cardObj.AddComponent<Outline>();
            borderOutline2.effectColor = Color.black;
            borderOutline2.effectDistance = new Vector2(-2, 2);

            // Bevel shine overlay - diagonal gradient bright at top-left, fading to bottom-right
            // Creates a 3D raised look like light hitting a beveled edge
            GameObject shineObj = new GameObject("BevelShine");
            shineObj.transform.SetParent(cardObj.transform, false);
            RectTransform shineRT = shineObj.AddComponent<RectTransform>();
            shineRT.anchorMin = Vector2.zero;
            shineRT.anchorMax = Vector2.one;
            shineRT.offsetMin = new Vector2(2, 2);
            shineRT.offsetMax = new Vector2(-2, -2);
            Image shineImg = shineObj.AddComponent<Image>();
            shineImg.sprite = CreateBevelShineSprite(8);
            shineImg.type = Image.Type.Sliced;
            shineImg.color = Color.white;
            shineImg.raycastTarget = false;

            // Gradient overlay for rarity color (top = rarity, bottom = neutral)
            GameObject gradientObj = new GameObject("GradientOverlay");
            gradientObj.transform.SetParent(cardObj.transform, false);
            RectTransform gradientRT = gradientObj.AddComponent<RectTransform>();
            gradientRT.anchorMin = Vector2.zero;
            gradientRT.anchorMax = Vector2.one;
            gradientRT.offsetMin = new Vector2(3, 3);
            gradientRT.offsetMax = new Vector2(-3, -3);
            Image gradientImg = gradientObj.AddComponent<Image>();
            gradientImg.sprite = CreateVerticalGradientSprite();
            gradientImg.type = Image.Type.Sliced;
            gradientImg.color = new Color(0.5f, 0.5f, 0.55f, 0.6f); // Default gray, semi-transparent
            gradientImg.raycastTarget = false;

            // Unit sprite - anchored to top area, leaving room for trait icons at bottom
            GameObject spriteObj = new GameObject("Sprite");
            spriteObj.transform.SetParent(cardObj.transform, false);
            RectTransform spriteRT = spriteObj.AddComponent<RectTransform>();
            // Anchor to top-center
            spriteRT.anchorMin = new Vector2(0.5f, 1f);
            spriteRT.anchorMax = new Vector2(0.5f, 1f);
            spriteRT.pivot = new Vector2(0.5f, 1f);
            float spriteSize = Mathf.Min(cardWidth - 12, cardHeight - 50); // Leave room for traits
            spriteRT.sizeDelta = new Vector2(spriteSize, spriteSize);
            spriteRT.anchoredPosition = new Vector2(0, -6); // 6px from top
            Image spriteImg = spriteObj.AddComponent<Image>();
            spriteImg.preserveAspect = true;
            spriteImg.enabled = false;
            spriteImg.raycastTarget = false;

            // Cost badge overlapping top-left corner (sits on top of the border)
            GameObject costBadge = new GameObject("CostBadge");
            costBadge.transform.SetParent(cardObj.transform, false);
            RectTransform costBadgeRT = costBadge.AddComponent<RectTransform>();
            costBadgeRT.anchorMin = new Vector2(0, 1);
            costBadgeRT.anchorMax = new Vector2(0, 1);
            costBadgeRT.pivot = new Vector2(0.5f, 0.5f);
            costBadgeRT.sizeDelta = new Vector2(28, 28);
            costBadgeRT.anchoredPosition = new Vector2(2, 2); // Straddle the corner

            // Coin background
            Image coinBg = costBadge.AddComponent<Image>();
            coinBg.sprite = CreateCoinSprite();
            coinBg.color = new Color(1f, 0.85f, 0.3f); // Gold color
            coinBg.raycastTarget = false;

            // Cost number on coin
            GameObject costNumObj = new GameObject("CostNum");
            costNumObj.transform.SetParent(costBadge.transform, false);
            RectTransform costNumRT = costNumObj.AddComponent<RectTransform>();
            costNumRT.anchorMin = Vector2.zero;
            costNumRT.anchorMax = Vector2.one;
            costNumRT.offsetMin = Vector2.zero;
            costNumRT.offsetMax = Vector2.zero;
            Text costText = costNumObj.AddComponent<Text>();
            costText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            costText.fontSize = 14;
            costText.fontStyle = FontStyle.Bold;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(0.3f, 0.2f, 0.1f); // Dark brown for contrast

            // Outline on cost text for readability
            Outline costOutline = costNumObj.AddComponent<Outline>();
            costOutline.effectColor = new Color(1f, 0.95f, 0.7f);
            costOutline.effectDistance = new Vector2(1, -1);

            // Star indicator at bottom
            GameObject starsObj = new GameObject("Stars");
            starsObj.transform.SetParent(cardObj.transform, false);
            RectTransform starsRT = starsObj.AddComponent<RectTransform>();
            starsRT.anchorMin = new Vector2(0.5f, 0);
            starsRT.anchorMax = new Vector2(0.5f, 0);
            starsRT.pivot = new Vector2(0.5f, 0);
            starsRT.sizeDelta = new Vector2(cardWidth, 24);
            starsRT.anchoredPosition = new Vector2(0, 6);
            Text starsText = starsObj.AddComponent<Text>();
            starsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            starsText.fontSize = 16;
            starsText.alignment = TextAnchor.MiddleCenter;
            starsText.color = new Color(1f, 0.9f, 0.4f); // Gold stars

            // No name text - removed per request

            // Trait icons container - fills gap between sprite bottom and card bottom
            // Sprite bottom is at: cardHeight - 6 (top pad) - spriteSize from top of card
            float spriteBottom = cardHeight - 6f - spriteSize; // distance from card bottom
            float traitAreaHeight = spriteBottom; // from card bottom to sprite bottom
            GameObject traitContainer = new GameObject("TraitIcons");
            traitContainer.transform.SetParent(cardObj.transform, false);
            RectTransform traitContainerRT = traitContainer.AddComponent<RectTransform>();
            // Stretch full width, positioned to fill gap below sprite
            traitContainerRT.anchorMin = new Vector2(0, 0);
            traitContainerRT.anchorMax = new Vector2(1, 0);
            traitContainerRT.pivot = new Vector2(0.5f, 0);
            traitContainerRT.sizeDelta = new Vector2(0, traitAreaHeight);
            traitContainerRT.anchoredPosition = new Vector2(0, 0);

            // No layout group - icons will be positioned manually by UnitCardUI
            // for equidistant spacing from each other and edges

            // Button - don't use onClick for shop cards (handled by UnitCardUI for tap vs long-press)
            Button btn = cardObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            int capturedIndex = index;
            if (!isShopCard)
            {
                btn.onClick.AddListener(() => OnBenchCardClicked(capturedIndex));
            }

            UnitCardUI card = cardObj.AddComponent<UnitCardUI>();
            card.background = bg;
            card.spriteImage = spriteImg;
            card.costText = costText;
            card.starsText = starsText;
            card.gradientOverlay = gradientImg;
            card.nameText = null; // No name text
            card.traitIconContainer = traitContainerRT;
            card.button = btn;
            card.index = index;
            card.isShopCard = isShopCard;

            return card;
        }

        // Cached sprites for card UI
        private static Sprite cachedRoundedRectSprite;
        private static Sprite cachedGradientSprite;
        private static Sprite cachedCoinSprite;

        private Sprite CreateRoundedRectSprite(int cornerRadius)
        {
            if (cachedRoundedRectSprite != null)
                return cachedRoundedRectSprite;

            int size = 32;
            int r = Mathf.Min(cornerRadius, size / 2);
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Check if in corner regions
                    bool inCorner = false;
                    float dist = 0;

                    // Bottom-left corner
                    if (x < r && y < r)
                    {
                        dist = Vector2.Distance(new Vector2(x, y), new Vector2(r, r));
                        inCorner = true;
                    }
                    // Bottom-right corner
                    else if (x >= size - r && y < r)
                    {
                        dist = Vector2.Distance(new Vector2(x, y), new Vector2(size - r - 1, r));
                        inCorner = true;
                    }
                    // Top-left corner
                    else if (x < r && y >= size - r)
                    {
                        dist = Vector2.Distance(new Vector2(x, y), new Vector2(r, size - r - 1));
                        inCorner = true;
                    }
                    // Top-right corner
                    else if (x >= size - r && y >= size - r)
                    {
                        dist = Vector2.Distance(new Vector2(x, y), new Vector2(size - r - 1, size - r - 1));
                        inCorner = true;
                    }

                    if (inCorner && dist > r)
                        pixels[y * size + x] = Color.clear;
                    else
                        pixels[y * size + x] = Color.white;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // Create sliced sprite with proper border
            cachedRoundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100,
                0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));

            return cachedRoundedRectSprite;
        }

        private static Sprite cachedBevelShineSprite;

        /// <summary>
        /// Creates a rounded rect with a diagonal gradient shine effect.
        /// Bright white border at top-left edges, fading to transparent at bottom-right.
        /// The center is transparent so only the border ring shows.
        /// </summary>
        private Sprite CreateBevelShineSprite(int cornerRadius)
        {
            if (cachedBevelShineSprite != null)
                return cachedBevelShineSprite;

            int size = 64; // Higher res for smoother gradient
            int r = Mathf.Min(cornerRadius, size / 2); // Corner radius in texture pixels
            int borderWidth = 6; // Width of the shine border in texture pixels

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Rounded rect mask - check if pixel is inside the rounded rect
                    bool inside = true;
                    float cornerDist = 0;

                    // Bottom-left
                    if (x < r && y < r)
                    {
                        cornerDist = Vector2.Distance(new Vector2(x, y), new Vector2(r, r));
                        if (cornerDist > r) inside = false;
                    }
                    // Bottom-right
                    else if (x >= size - r && y < r)
                    {
                        cornerDist = Vector2.Distance(new Vector2(x, y), new Vector2(size - r - 1, r));
                        if (cornerDist > r) inside = false;
                    }
                    // Top-left
                    else if (x < r && y >= size - r)
                    {
                        cornerDist = Vector2.Distance(new Vector2(x, y), new Vector2(r, size - r - 1));
                        if (cornerDist > r) inside = false;
                    }
                    // Top-right
                    else if (x >= size - r && y >= size - r)
                    {
                        cornerDist = Vector2.Distance(new Vector2(x, y), new Vector2(size - r - 1, size - r - 1));
                        if (cornerDist > r) inside = false;
                    }

                    if (!inside)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    // Calculate distance from edge (how deep inside the border we are)
                    float edgeDist = Mathf.Min(
                        Mathf.Min(x, size - 1 - x),
                        Mathf.Min(y, size - 1 - y)
                    );

                    // In corner regions, use distance from corner arc
                    if (x < r && y < r)
                        edgeDist = Mathf.Min(edgeDist, r - Vector2.Distance(new Vector2(x, y), new Vector2(r, r)));
                    else if (x >= size - r && y < r)
                        edgeDist = Mathf.Min(edgeDist, r - Vector2.Distance(new Vector2(x, y), new Vector2(size - r - 1, r)));
                    else if (x < r && y >= size - r)
                        edgeDist = Mathf.Min(edgeDist, r - Vector2.Distance(new Vector2(x, y), new Vector2(r, size - r - 1)));
                    else if (x >= size - r && y >= size - r)
                        edgeDist = Mathf.Min(edgeDist, r - Vector2.Distance(new Vector2(x, y), new Vector2(size - r - 1, size - r - 1)));

                    // Border falloff - only visible within borderWidth pixels of edge
                    float borderAlpha = 1f - Mathf.Clamp01(edgeDist / borderWidth);

                    // Diagonal gradient - bright at top-left (high x inverted + high y), dim at bottom-right
                    float nx = 1f - (float)x / (size - 1); // 1 at left, 0 at right
                    float ny = (float)y / (size - 1);       // 1 at top, 0 at bottom
                    float shine = (nx + ny) * 0.5f;         // Average: brightest at top-left
                    shine = Mathf.Pow(shine, 0.7f);          // Boost the bright end slightly

                    float finalAlpha = borderAlpha * shine * 0.5f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, finalAlpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // Use 9-slice borders so corners maintain shape when stretched on rectangular cards
            int border = r + borderWidth;
            cachedBevelShineSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100,
                0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            return cachedBevelShineSprite;
        }

        private Sprite CreateVerticalGradientSprite()
        {
            if (cachedGradientSprite != null)
                return cachedGradientSprite;

            int width = 4;
            int height = 32;
            Texture2D tex = new Texture2D(width, height);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                // Gradient from full opacity at top to transparent at bottom
                Color c = new Color(1f, 1f, 1f, 1f - t * 0.7f);
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            cachedGradientSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100,
                0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));

            return cachedGradientSprite;
        }

        private Sprite CreateCoinSprite()
        {
            if (cachedCoinSprite != null)
                return cachedCoinSprite;

            int size = 24;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        // Inner coin - slight 3D effect
                        float shade = 1f - (dist / radius) * 0.2f;
                        pixels[y * size + x] = new Color(shade, shade, shade, 1f);
                    }
                    else if (dist <= radius + 1)
                    {
                        // Anti-aliased edge
                        float alpha = 1f - (dist - radius);
                        pixels[y * size + x] = new Color(0.8f, 0.8f, 0.8f, alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            cachedCoinSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);

            return cachedCoinSprite;
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

            // Scout button no longer needed - scout panel is always visible at top
            if (scoutButton != null)
            {
                scoutButton.gameObject.SetActive(false);
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
            // Include optimistic gold changes (pending purchases/sales before server confirms)
            int displayGold = ss.gold + optimisticGoldDelta;
            goldText.text = $"💰 {displayGold}";
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

            // Scout button no longer needed - scout panel is always visible at top
            if (scoutButton != null)
            {
                scoutButton.gameObject.SetActive(false);
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

            // Check if this is a reroll response (we were waiting for new shop data)
            bool isRerollResponse = waitingForRerollResponse;
            if (isRerollResponse)
            {
                waitingForRerollResponse = false;
            }

            for (int i = 0; i < shopCards.Count; i++)
            {
                // Skip slots with pending purchases (server hasn't confirmed yet)
                if (pendingShopPurchases.Contains(i))
                {
                    // Check if server has confirmed (slot is now null)
                    if (i < ss.shop.Length && ss.shop[i] == null)
                    {
                        pendingShopPurchases.Remove(i); // Purchase confirmed
                    }
                    continue; // Don't restore the card
                }

                if (i < ss.shop.Length && ss.shop[i] != null
                    && !string.IsNullOrEmpty(ss.shop[i].unitId))
                {
                    var serverUnit = ss.shop[i];
                    shopCards[i].SetServerUnit(serverUnit);

                    // Grey out if can't afford (including optimistic gold changes)
                    int effectiveGold = ss.gold + optimisticGoldDelta;
                    bool canAfford = effectiveGold >= serverUnit.cost;
                    shopCards[i].SetInteractable(canAfford);
                }
                else
                {
                    shopCards[i].SetServerUnit(null);
                }
            }

            // Start grow animation if this was a reroll response
            if (isRerollResponse)
            {
                StartRerollGrowAnimation();
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
            sellText.text = $"SELL {unit.template.unitName.ToUpper()}\n<size=40>${sellValue}</size>";

            // Show sell overlay
            if (shopPanel != null)
            {
                shopPanel.gameObject.SetActive(true);
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
            if (crestTooltipShowing) return;

            crestTooltipPanel.gameObject.SetActive(true);
            PopulateCrestTooltip(crest);
        }

        public void HideCrestTooltip()
        {
            DismissCrestTooltip();
        }

        public void ShowCrestTooltipPinned(CrestData crest)
        {
            if (crest == null || crestTooltipPanel == null) return;
            crestTooltipShowing = true;

            crestTooltipPanel.gameObject.SetActive(true);
            PopulateCrestTooltip(crest);
        }

        public void UnpinCrestTooltip()
        {
            DismissCrestTooltip();
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
            if (crestTooltipShowing) return;

            crestTooltipPanel.gameObject.SetActive(true);
            PopulateServerCrestTooltip(serverCrest);
        }

        public void ShowServerCrestTooltipPinned(ServerCrestData serverCrest)
        {
            if (serverCrest == null || crestTooltipPanel == null) return;
            crestTooltipShowing = true;

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
            if (crestPanel == null) return;

            // Collect active crests
            var activeCrestList = new List<(Sprite icon, CrestData crestData, ServerCrestData serverCrestData, bool isMinor, string displayName)>();

            if (IsMultiplayer && serverState != null)
            {
                // Major crest
                if (serverState.majorCrest != null && !string.IsNullOrEmpty(serverState.majorCrest.crestId))
                {
                    activeCrestList.Add((
                        CrestIcons.GetMajorCrestIcon(),
                        null,
                        serverState.majorCrest,
                        false,
                        serverState.majorCrest.name ?? serverState.majorCrest.crestId
                    ));
                }

                // Minor crests
                if (serverState.minorCrests != null)
                {
                    foreach (var minorCrest in serverState.minorCrests)
                    {
                        if (minorCrest != null && !string.IsNullOrEmpty(minorCrest.crestId))
                        {
                            activeCrestList.Add((
                                CrestIcons.GetMinorCrestIcon(),
                                null,
                                minorCrest,
                                true,
                                minorCrest.name ?? minorCrest.crestId
                            ));
                        }
                    }
                }
            }
            else if (state != null)
            {
                // Major crests
                if (state.majorCrests != null)
                {
                    foreach (var crest in state.majorCrests)
                    {
                        if (crest != null)
                        {
                            activeCrestList.Add((
                                CrestIcons.GetMajorCrestIcon(),
                                crest,
                                null,
                                false,
                                crest.crestName
                            ));
                        }
                    }
                }

                // Minor crests
                if (state.minorCrests != null)
                {
                    foreach (var crest in state.minorCrests)
                    {
                        if (crest != null)
                        {
                            activeCrestList.Add((
                                CrestIcons.GetMinorCrestIcon(),
                                crest,
                                null,
                                true,
                                crest.crestName
                            ));
                        }
                    }
                }
            }

            // Hide if no crests
            if (activeCrestList.Count == 0)
            {
                crestPanel.gameObject.SetActive(false);
                return;
            }

            // Check if display changed (simple count + name hash)
            int newHash = activeCrestList.Count;
            foreach (var c in activeCrestList)
                newHash = newHash * 31 + (c.displayName?.GetHashCode() ?? 0);

            if (newHash == lastCrestHash && crestIconEntries.Count > 0)
            {
                crestPanel.gameObject.SetActive(true);
                return;
            }
            lastCrestHash = newHash;

            // Clear old entries
            foreach (var entry in crestIconEntries)
            {
                if (entry != null) Destroy(entry);
            }
            crestIconEntries.Clear();

            // Create icon entries
            foreach (var crestInfo in activeCrestList)
            {
                CreateCrestIconEntry(crestInfo.icon, crestInfo.crestData, crestInfo.serverCrestData, crestInfo.isMinor);
            }

            crestPanel.gameObject.SetActive(true);
        }

        private int lastCrestHash = 0;

        private void CreateCrestIconEntry(Sprite icon, CrestData crestData, ServerCrestData serverCrestData, bool isMinor)
        {
            float iconSize = 96f;

            GameObject entryObj = new GameObject("CrestIcon");
            entryObj.transform.SetParent(crestPanel, false);
            crestIconEntries.Add(entryObj);

            RectTransform rt = entryObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            LayoutElement le = entryObj.AddComponent<LayoutElement>();
            le.preferredHeight = iconSize;
            le.preferredWidth = iconSize;
            le.minHeight = iconSize;

            // Shield icon
            Image img = entryObj.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = true;

            // Button for tap
            Button btn = entryObj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            // Capture references for click handler
            CrestData cd = crestData;
            ServerCrestData scd = serverCrestData;
            GameObject thisEntry = entryObj;

            btn.onClick.AddListener(() => {
                OnCrestIconClicked(thisEntry, cd, scd);
            });
        }

        private void OnCrestIconClicked(GameObject iconObj, CrestData crestData, ServerCrestData serverCrestData)
        {
            // Toggle: if this icon's tooltip is already showing, dismiss it
            if (crestTooltipShowing && activeCrestIcon == iconObj)
            {
                DismissCrestTooltip();
                return;
            }

            // Show tooltip for this crest

            crestTooltipShowing = true;
            activeCrestIcon = iconObj;

            if (crestData != null)
            {
                crestTooltipPanel.gameObject.SetActive(true);
                PopulateCrestTooltip(crestData);
            }
            else if (serverCrestData != null)
            {
                crestTooltipPanel.gameObject.SetActive(true);
                PopulateServerCrestTooltip(serverCrestData);
            }

            // Position tooltip to the right of the tapped icon
            RectTransform iconRT = iconObj.GetComponent<RectTransform>();
            if (iconRT != null)
            {
                Vector3[] corners = new Vector3[4];
                iconRT.GetWorldCorners(corners);
                float iconRight = corners[2].x;
                float iconCenterY = (corners[0].y + corners[2].y) * 0.5f;

                RectTransform canvasRT = mainCanvas.GetComponent<RectTransform>();
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRT, new Vector2(iconRight + 8, iconCenterY),
                    null, out localPos);

                // Convert pivot-relative to top-left anchor relative
                Vector2 anchoredPos = localPos + new Vector2(
                    canvasRT.rect.width * canvasRT.pivot.x,
                    -(canvasRT.rect.height * (1f - canvasRT.pivot.y)));

                crestTooltipPanel.anchorMin = new Vector2(0, 1);
                crestTooltipPanel.anchorMax = new Vector2(0, 1);
                crestTooltipPanel.pivot = new Vector2(0, 0.5f);
                crestTooltipPanel.anchoredPosition = anchoredPos;
            }

            // Show blocker
            if (crestTooltipBlocker != null)
            {
                crestTooltipBlocker.SetActive(true);
                crestTooltipBlocker.transform.SetSiblingIndex(crestPanel.transform.GetSiblingIndex());
                crestPanel.transform.SetAsLastSibling();
                crestTooltipPanel.transform.SetAsLastSibling();
            }
        }

        private void DismissCrestTooltip()
        {
            crestTooltipShowing = false;
            activeCrestIcon = null;


            if (crestTooltipPanel != null)
            {
                crestTooltipPanel.gameObject.SetActive(false);
            }
            if (crestTooltipBlocker != null)
            {
                crestTooltipBlocker.SetActive(false);
            }
        }

        private void UpdateShopButtons()
        {
            // Multiplayer mode
            if (IsMultiplayer)
            {
                var ss = serverState;
                // Use optimistic values for button interactability
                int effectiveGold = ss.gold + optimisticGoldDelta;
                int effectiveFreeRerolls = ss.freeRerolls + optimisticFreeRerollDelta;

                // Can reroll if have free rerolls OR enough gold
                bool mpCanReroll = effectiveFreeRerolls > 0 || effectiveGold >= 2; // REROLL_COST
                bool mpCanBuyXP = effectiveGold >= 4 && ss.level < 9; // XP_COST, MAX_LEVEL

                rerollButton.interactable = mpCanReroll;
                buyXPButton.interactable = mpCanBuyXP;

                // Update reroll button text to show free rerolls (including optimistic changes)
                var rerollText = rerollButton.GetComponentInChildren<Text>();
                if (rerollText != null)
                {
                    if (effectiveFreeRerolls > 0)
                    {
                        rerollText.text = $"Reroll ({effectiveFreeRerolls})";
                    }
                    else
                    {
                        rerollText.text = "Reroll $2";
                    }
                }

                string mpLockText = ss.shopLocked ? "Locked" : "Lock";
                lockButton.GetComponentInChildren<Text>().text = mpLockText;
                return;
            }

            bool canReroll = state.player.gold >= GameConstants.Economy.REROLL_COST;
            bool canBuyXP = state.player.gold >= GameConstants.Economy.XP_COST &&
                           state.player.level < GameConstants.Leveling.MAX_LEVEL;

            rerollButton.interactable = canReroll;
            buyXPButton.interactable = canBuyXP;

            string lockText = state.shop.isLocked ? "Locked" : "Lock";
            lockButton.GetComponentInChildren<Text>().text = lockText;
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

            // Set sprite - prefer 3D portrait
            if (tooltipSprite != null)
            {
                tooltipSprite.sprite = UnitPortraitGenerator.GetPortrait(t.unitId, t.unitName);
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

            // Set sprite - prefer 3D portrait
            if (tooltipSprite != null)
            {
                tooltipSprite.sprite = UnitPortraitGenerator.GetPortrait(serverUnit.unitId, serverUnit.name ?? template.unitName);
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
        /// Show tooltip for a shop unit (multiplayer mode)
        /// </summary>
        public void ShowTooltip(ServerShopUnit shopUnit)
        {
            if (shopUnit == null)
            {
                tooltipPanel.gameObject.SetActive(false);
                return;
            }

            // Get the unit template from ServerGameState
            var serverState = ServerGameState.Instance;
            var template = serverState?.GetUnitTemplate(shopUnit.unitId);

            if (template == null)
            {
                Debug.LogWarning($"[GameUI] Could not find template for shop unit: {shopUnit.unitId}");
                tooltipPanel.gameObject.SetActive(false);
                return;
            }

            tooltipPanel.gameObject.SetActive(true);

            // Set sprite - prefer 3D portrait
            if (tooltipSprite != null)
            {
                tooltipSprite.sprite = UnitPortraitGenerator.GetPortrait(shopUnit.unitId, shopUnit.name ?? template.unitName);
                tooltipSprite.enabled = true;
            }

            // Set name with cost color
            tooltipTitle.text = shopUnit.name ?? template.unitName;
            tooltipTitle.color = GetCostColor(shopUnit.cost);

            // Set cost (shop units are always 1-star)
            tooltipCost.text = $"${shopUnit.cost}  ★";

            // Set stats from template base stats
            string stats = $"HP: {template.baseStats.health}    ATK: {template.baseStats.attack}\n";
            stats += $"Armor: {template.baseStats.armor}    Magic Resist: {template.baseStats.magicResist}\n";
            stats += $"Attack Speed: {template.baseStats.attackSpeed:F2}    Range: {template.baseStats.range}";
            tooltipStats.text = stats;

            // Set traits
            string traits = "";
            if (shopUnit.traits != null && shopUnit.traits.Length > 0)
            {
                traits = string.Join(", ", shopUnit.traits);
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
                tooltipAbility.text = $"<b>{template.ability.abilityName}</b>\n{template.ability.description}";
            }
            else
            {
                tooltipAbility.text = "No ability";
            }

            // Clear items (shop units don't have items)
            tooltipUnit = null;
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
                tooltipItemContainer.gameObject.SetActive(false);
            }
            if (tooltipItemsLabel != null)
            {
                Transform dividerBeforeItems = tooltipItemsLabel.transform.parent.GetChild(
                    tooltipItemsLabel.transform.GetSiblingIndex() - 1);
                if (dividerBeforeItems != null)
                    dividerBeforeItems.gameObject.SetActive(false);
                tooltipItemsLabel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Show tooltip and keep it pinned for shop units (multiplayer mode)
        /// </summary>
        public void ShowTooltipPinned(ServerShopUnit shopUnit)
        {
            isTooltipPinned = true;
            ShowTooltip(shopUnit);
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

            // Set sprite - prefer 3D portrait
            if (tooltipSprite != null)
            {
                tooltipSprite.sprite = UnitPortraitGenerator.GetPortrait(combatUnit.unitId, combatUnit.name ?? template.unitName);
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
                ServerTooltipItemSlot.Create(tooltipItemContainer, new Vector2(72, 72), serverItem, combatUnit.instanceId, -1);
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

                var slot = TooltipItemSlot.Create(tooltipItemContainer, new Vector2(72, 72), item, unit, i);
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
                ServerTooltipItemSlot.Create(tooltipItemContainer, new Vector2(72, 72), serverItem, serverUnit.instanceId, i);
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
                
                // Trait icon column - positioned above shop, height auto-sized
                if (traitPanel != null)
                {
                    traitPanel.sizeDelta = new Vector2(104, 0);
                    traitPanel.anchoredPosition = new Vector2(4, 365);
                }

                // Trait tooltip - position is set dynamically in UpdateTraitTooltip
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
                
                // Trait icon column - positioned above shop, height auto-sized
                if (traitPanel != null)
                {
                    traitPanel.sizeDelta = new Vector2(104, 0);
                    traitPanel.anchoredPosition = new Vector2(4, 190);
                }

                // Trait tooltip - position is set dynamically in UpdateTraitTooltip
            }
        }

        // ========== Click Handlers ==========

        public void OnShopCardClicked(int index)
        {
            // Play UI click sound immediately
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            // Multiplayer mode - send to server
            if (IsMultiplayer)
            {
                // Check if we can afford it and have bench space (basic validation)
                var shopUnit = serverState.shop != null && index < serverState.shop.Length ? serverState.shop[index] : null;
                if (shopUnit != null && serverState.gold + optimisticGoldDelta >= shopUnit.cost)
                {
                    // Apply optimistic gold change immediately
                    optimisticGoldDelta -= shopUnit.cost;

                    // Create optimistic visual immediately (before server confirms)
                    if (Crestforge.Visuals.BoardManager3D.Instance != null)
                    {
                        int slot = Crestforge.Visuals.BoardManager3D.Instance.CreateOptimisticPurchaseVisual(shopUnit);
                        Debug.Log($"[GameUI] CreateOptimisticPurchaseVisual returned slot: {slot}");
                    }
                    else
                    {
                        Debug.Log("[GameUI] BoardManager3D.Instance is null!");
                    }

                    // Clear the shop card immediately for instant feedback
                    if (index < shopCards.Count && shopCards[index] != null)
                    {
                        shopCards[index].SetServerUnit(null);
                        pendingShopPurchases.Add(index); // Protect from sync restoring it
                    }
                }

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
                // Apply optimistic changes immediately
                int effectiveFreeRerolls = serverState.freeRerolls + optimisticFreeRerollDelta;
                if (effectiveFreeRerolls > 0)
                {
                    // Use a free reroll
                    optimisticFreeRerollDelta--;
                }
                else
                {
                    // Pay gold for reroll
                    int rerollCost = 2; // GameConstants.Economy.REROLL_COST
                    if (serverState.gold + optimisticGoldDelta >= rerollCost)
                    {
                        optimisticGoldDelta -= rerollCost;
                    }
                }

                // Start shrink animation and mark waiting for response
                StartRerollShrinkAnimation();
                waitingForRerollResponse = true;

                serverState.Reroll();
                Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
                return;
            }

            pendingAction = () => {
                state.RerollShop();
                Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();
            };
        }

        /// <summary>
        /// Start the shrink animation for all shop cards during reroll
        /// </summary>
        private void StartRerollShrinkAnimation()
        {
            if (rerollAnimationCoroutine != null)
            {
                StopCoroutine(rerollAnimationCoroutine);
            }
            rerollAnimationCoroutine = StartCoroutine(AnimateShopCardsShrink());
        }

        /// <summary>
        /// Start the grow animation for all shop cards after reroll response
        /// </summary>
        private void StartRerollGrowAnimation()
        {
            if (rerollAnimationCoroutine != null)
            {
                StopCoroutine(rerollAnimationCoroutine);
            }
            rerollAnimationCoroutine = StartCoroutine(AnimateShopCardsGrow());
        }

        private IEnumerator AnimateShopCardsShrink()
        {
            float duration = 0.15f;
            float elapsed = 0f;

            // Store original scales and animate to minimum
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = Mathf.Lerp(1f, 0.1f, t);

                foreach (var card in shopCards)
                {
                    if (card != null)
                    {
                        card.transform.localScale = Vector3.one * scale;
                    }
                }

                yield return null;
            }

            // Ensure final scale is set
            foreach (var card in shopCards)
            {
                if (card != null)
                {
                    card.transform.localScale = Vector3.one * 0.1f;
                }
            }
        }

        private IEnumerator AnimateShopCardsGrow()
        {
            float duration = 0.15f;
            float elapsed = 0f;

            // Get starting scale (might be mid-shrink)
            float startScale = shopCards.Count > 0 && shopCards[0] != null
                ? shopCards[0].transform.localScale.x
                : 0.1f;

            // Animate to full size with slight overshoot for juicy feel
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Ease out with slight overshoot
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(startScale, 1.05f, eased);

                foreach (var card in shopCards)
                {
                    if (card != null)
                    {
                        card.transform.localScale = Vector3.one * scale;
                    }
                }

                yield return null;
            }

            // Settle back to exactly 1.0
            float settleDuration = 0.05f;
            elapsed = 0f;
            while (elapsed < settleDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / settleDuration;
                float scale = Mathf.Lerp(1.05f, 1f, t);

                foreach (var card in shopCards)
                {
                    if (card != null)
                    {
                        card.transform.localScale = Vector3.one * scale;
                    }
                }

                yield return null;
            }

            // Ensure final scale
            foreach (var card in shopCards)
            {
                if (card != null)
                {
                    card.transform.localScale = Vector3.one;
                }
            }
        }

        private void OnBuyXPClicked()
        {
            Crestforge.Visuals.AudioManager.Instance?.PlayUIClick();

            // Multiplayer mode - send to server
            if (IsMultiplayer)
            {
                // Apply optimistic gold change immediately
                int xpCost = 4; // GameConstants.Economy.XP_COST
                if (serverState.gold + optimisticGoldDelta >= xpCost)
                {
                    optimisticGoldDelta -= xpCost;
                }

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