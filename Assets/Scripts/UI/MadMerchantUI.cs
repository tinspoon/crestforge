using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Systems;
using Crestforge.Data;

namespace Crestforge.UI
{
    /// <summary>
    /// UI for the Mad Merchant carousel round.
    /// Players pick items/crests in order based on health (lowest first).
    /// </summary>
    public class MadMerchantUI : MonoBehaviour
    {
        public static MadMerchantUI Instance { get; private set; }

        [Header("References")]
        public Canvas merchantCanvas;
        public RectTransform mainPanel;

        [Header("UI Elements")]
        public Text titleText;
        public Text turnText;
        public Text pickHistoryText;
        public RectTransform optionsGrid;
        public Button skipButton;

        [Header("Settings")]
        public float opponentPickDelay = 1.5f;
        public int itemCount = 9;
        public int crestCount = 3;

        // Runtime state
        private List<MerchantOption> options = new List<MerchantOption>();
        private List<MerchantOptionUI> optionUIs = new List<MerchantOptionUI>();
        private List<PickerInfo> pickOrder = new List<PickerInfo>();
        private int currentPickerIndex = 0;
        private bool isPlayerTurn = false;
        private bool isActive = false;
        private List<string> pickHistory = new List<string>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            CreateUI();
            Hide();
        }

        private void CreateUI()
        {
            // Create Canvas
            if (merchantCanvas == null)
            {
                GameObject canvasObj = new GameObject("MerchantCanvas");
                canvasObj.transform.SetParent(transform);
                merchantCanvas = canvasObj.AddComponent<Canvas>();
                merchantCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                merchantCanvas.sortingOrder = 60;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Background overlay
            GameObject bgObj = CreatePanel("Background", merchantCanvas.transform);
            RectTransform bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bgObj.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            // Main panel
            GameObject panelObj = CreatePanel("MainPanel", merchantCanvas.transform);
            mainPanel = panelObj.GetComponent<RectTransform>();
            mainPanel.anchorMin = new Vector2(0.05f, 0.1f);
            mainPanel.anchorMax = new Vector2(0.95f, 0.9f);
            mainPanel.offsetMin = Vector2.zero;
            mainPanel.offsetMax = Vector2.zero;
            panelObj.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.08f, 0.98f);

            Outline outline = panelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.6f, 0.5f, 0.2f);
            outline.effectDistance = new Vector2(3, 3);

            VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 15;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Title
            titleText = CreateText("MAD MERCHANT", mainPanel, 400);
            titleText.fontSize = 36;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.85f, 0.3f);
            titleText.GetComponent<LayoutElement>().preferredHeight = 50;

            // Subtitle
            Text subtitle = CreateText("Choose your reward! Lowest health picks first.", mainPanel, 500);
            subtitle.fontSize = 18;
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.color = new Color(0.8f, 0.8f, 0.7f);
            subtitle.GetComponent<LayoutElement>().preferredHeight = 25;

            // Turn indicator
            turnText = CreateText("Waiting...", mainPanel, 400);
            turnText.fontSize = 24;
            turnText.fontStyle = FontStyle.Bold;
            turnText.alignment = TextAnchor.MiddleCenter;
            turnText.color = new Color(0.5f, 0.9f, 0.5f);
            turnText.GetComponent<LayoutElement>().preferredHeight = 35;

            // Options grid container
            GameObject gridObj = CreatePanel("OptionsGrid", mainPanel);
            optionsGrid = gridObj.GetComponent<RectTransform>();
            gridObj.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.5f);
            LayoutElement gridLE = gridObj.AddComponent<LayoutElement>();
            gridLE.preferredHeight = 350;
            gridLE.flexibleWidth = 1;

            GridLayoutGroup glg = gridObj.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(140, 100);
            glg.spacing = new Vector2(15, 15);
            glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;
            glg.padding = new RectOffset(20, 20, 20, 20);

            // Pick history
            Text historyLabel = CreateText("Recent Picks:", mainPanel, 300);
            historyLabel.fontSize = 16;
            historyLabel.fontStyle = FontStyle.Bold;
            historyLabel.alignment = TextAnchor.MiddleCenter;
            historyLabel.color = new Color(0.6f, 0.6f, 0.7f);
            historyLabel.GetComponent<LayoutElement>().preferredHeight = 20;

            pickHistoryText = CreateText("", mainPanel, 600);
            pickHistoryText.fontSize = 14;
            pickHistoryText.alignment = TextAnchor.MiddleCenter;
            pickHistoryText.color = new Color(0.7f, 0.7f, 0.8f);
            pickHistoryText.GetComponent<LayoutElement>().preferredHeight = 60;
        }

        /// <summary>
        /// Start the Mad Merchant round
        /// </summary>
        public void StartMerchantRound()
        {
            if (isActive) return;

            isActive = true;
            pickHistory.Clear();
            currentPickerIndex = 0;

            // Generate options
            GenerateOptions();

            // Create option UI elements
            CreateOptionUIs();

            // Determine pick order (lowest health first)
            DeterminePickOrder();

            // Show UI
            Show();

            // Start the picking process
            StartCoroutine(ProcessPicks());
        }

        private void GenerateOptions()
        {
            options.Clear();
            var state = GameState.Instance;

            // Generate items
            if (state.allItems != null && state.allItems.Length > 0)
            {
                var availableItems = new List<ItemData>(state.allItems);
                ShuffleList(availableItems);

                for (int i = 0; i < itemCount && i < availableItems.Count; i++)
                {
                    options.Add(new MerchantOption
                    {
                        optionType = MerchantOptionType.Item,
                        item = availableItems[i],
                        isPicked = false
                    });
                }
            }

            // Generate crest tokens (triggers crest selection when picked)
            for (int i = 0; i < crestCount; i++)
            {
                options.Add(new MerchantOption
                {
                    optionType = MerchantOptionType.CrestToken,
                    isPicked = false
                });
            }

            // Shuffle all options together
            ShuffleList(options);

            // Ensure we have exactly 12 options (pad with gold if needed)
            while (options.Count < GameConstants.MadMerchant.TOTAL_OPTIONS)
            {
                options.Add(new MerchantOption
                {
                    optionType = MerchantOptionType.Gold,
                    goldAmount = Random.Range(3, 8),
                    isPicked = false
                });
            }
        }

        private void CreateOptionUIs()
        {
            // Clear existing
            foreach (var ui in optionUIs)
            {
                if (ui != null && ui.gameObject != null)
                {
                    Destroy(ui.gameObject);
                }
            }
            optionUIs.Clear();

            // Create new option UIs
            for (int i = 0; i < options.Count; i++)
            {
                var optionUI = CreateOptionUI(options[i], i);
                optionUIs.Add(optionUI);
            }
        }

        private MerchantOptionUI CreateOptionUI(MerchantOption option, int index)
        {
            GameObject optionObj = CreatePanel($"Option_{index}", optionsGrid);
            Image bg = optionObj.GetComponent<Image>();
            bg.color = GetOptionColor(option);

            // Add outline
            Outline outline = optionObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.5f);
            outline.effectDistance = new Vector2(1, 1);

            VerticalLayoutGroup vlg = optionObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Type label
            string typeLabel = option.optionType switch
            {
                MerchantOptionType.Item => "ITEM",
                MerchantOptionType.Crest => "CREST",
                MerchantOptionType.CrestToken => "CREST",
                MerchantOptionType.Gold => "GOLD",
                _ => "???"
            };
            Text typeText = CreateText(typeLabel, optionObj.transform, 0);
            typeText.fontSize = 10;
            typeText.fontStyle = FontStyle.Bold;
            typeText.alignment = TextAnchor.MiddleCenter;
            typeText.color = new Color(0.7f, 0.7f, 0.8f);
            typeText.GetComponent<LayoutElement>().preferredHeight = 14;

            // Name
            string name = GetOptionName(option);
            Text nameText = CreateText(name, optionObj.transform, 0);
            nameText.fontSize = 14;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.GetComponent<LayoutElement>().preferredHeight = 40;

            // Description/bonus
            string desc = GetOptionDescription(option);
            Text descText = CreateText(desc, optionObj.transform, 0);
            descText.fontSize = 11;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.color = new Color(0.6f, 0.8f, 0.6f);
            descText.GetComponent<LayoutElement>().preferredHeight = 30;

            // Button
            Button btn = optionObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            int capturedIndex = index;
            btn.onClick.AddListener(() => OnOptionClicked(capturedIndex));

            // Create UI component
            MerchantOptionUI ui = optionObj.AddComponent<MerchantOptionUI>();
            ui.option = option;
            ui.index = index;
            ui.background = bg;
            ui.button = btn;
            ui.nameText = nameText;

            return ui;
        }

        private Color GetOptionColor(MerchantOption option)
        {
            return option.optionType switch
            {
                MerchantOptionType.Item => GetItemRarityColor(option.item),
                MerchantOptionType.Crest => new Color(0.5f, 0.35f, 0.6f),
                MerchantOptionType.CrestToken => new Color(0.6f, 0.4f, 0.7f),
                MerchantOptionType.Gold => new Color(0.6f, 0.5f, 0.2f),
                _ => new Color(0.3f, 0.3f, 0.3f)
            };
        }

        private Color GetItemRarityColor(ItemData item)
        {
            if (item == null) return new Color(0.3f, 0.3f, 0.35f);

            return item.rarity switch
            {
                ItemRarity.Common => new Color(0.35f, 0.35f, 0.4f),
                ItemRarity.Uncommon => new Color(0.25f, 0.45f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.4f, 0.6f),
                _ => new Color(0.3f, 0.3f, 0.35f)
            };
        }

        private string GetOptionName(MerchantOption option)
        {
            return option.optionType switch
            {
                MerchantOptionType.Item => option.item?.itemName ?? "Unknown Item",
                MerchantOptionType.Crest => option.crest?.crestName ?? "Unknown Crest",
                MerchantOptionType.CrestToken => "Minor Crest Token",
                MerchantOptionType.Gold => $"{option.goldAmount} Gold",
                _ => "???"
            };
        }

        private string GetOptionDescription(MerchantOption option)
        {
            switch (option.optionType)
            {
                case MerchantOptionType.Item:
                    if (option.item == null) return "";
                    var stats = new List<string>();
                    if (option.item.bonusHealth > 0) stats.Add($"+{option.item.bonusHealth} HP");
                    if (option.item.bonusAttack > 0) stats.Add($"+{option.item.bonusAttack} ATK");
                    if (option.item.bonusArmor > 0) stats.Add($"+{option.item.bonusArmor} ARM");
                    if (option.item.bonusAttackSpeed > 0) stats.Add($"+{option.item.bonusAttackSpeed:P0} AS");
                    return stats.Count > 0 ? string.Join(", ", stats) : option.item.rarity.ToString();

                case MerchantOptionType.Crest:
                    return option.crest?.description ?? "";

                case MerchantOptionType.CrestToken:
                    return "Choose 1 of 3 crests";

                case MerchantOptionType.Gold:
                    return "Instant gold!";

                default:
                    return "";
            }
        }

        private void DeterminePickOrder()
        {
            pickOrder.Clear();

            var state = GameState.Instance;

            // Add player
            pickOrder.Add(new PickerInfo
            {
                name = "You",
                health = state.player.health,
                isPlayer = true,
                opponent = null
            });

            // Add opponents (if in PvP mode)
            if (state.currentGameMode == GameMode.PvP && OpponentManager.Instance != null)
            {
                foreach (var opponent in OpponentManager.Instance.opponents)
                {
                    if (!opponent.isEliminated)
                    {
                        pickOrder.Add(new PickerInfo
                        {
                            name = opponent.name,
                            health = opponent.health,
                            isPlayer = false,
                            opponent = opponent
                        });
                    }
                }
            }

            // Sort by health (lowest first)
            pickOrder.Sort((a, b) => a.health.CompareTo(b.health));

            Debug.Log($"Mad Merchant pick order: {string.Join(", ", pickOrder.ConvertAll(p => $"{p.name}({p.health}HP)"))}");
        }

        private IEnumerator ProcessPicks()
        {
            yield return new WaitForSeconds(0.5f);

            while (currentPickerIndex < pickOrder.Count)
            {
                var picker = pickOrder[currentPickerIndex];
                isPlayerTurn = picker.isPlayer;

                // Update turn text
                if (picker.isPlayer)
                {
                    turnText.text = "YOUR TURN - Click to pick!";
                    turnText.color = new Color(0.5f, 1f, 0.5f);
                }
                else
                {
                    turnText.text = $"{picker.name}'s turn...";
                    turnText.color = new Color(1f, 0.8f, 0.5f);
                }

                if (picker.isPlayer)
                {
                    // Wait for player to pick
                    yield return new WaitUntil(() => !isPlayerTurn || !isActive);
                }
                else
                {
                    // AI picks after delay
                    yield return new WaitForSeconds(opponentPickDelay);

                    if (isActive)
                    {
                        int aiChoice = GetRandomAvailableOption();
                        if (aiChoice >= 0)
                        {
                            PickOption(aiChoice, picker);
                        }
                    }
                }

                currentPickerIndex++;
                yield return new WaitForSeconds(0.3f);
            }

            // All picks complete
            yield return new WaitForSeconds(1f);
            CompleteMerchantRound();
        }

        private int GetRandomAvailableOption()
        {
            var available = new List<int>();
            for (int i = 0; i < options.Count; i++)
            {
                if (!options[i].isPicked)
                {
                    available.Add(i);
                }
            }

            if (available.Count == 0) return -1;
            return available[Random.Range(0, available.Count)];
        }

        private void OnOptionClicked(int index)
        {
            if (!isActive || !isPlayerTurn) return;
            if (index < 0 || index >= options.Count) return;
            if (options[index].isPicked) return;

            var picker = pickOrder[currentPickerIndex];
            PickOption(index, picker);
            isPlayerTurn = false;
        }

        private void PickOption(int index, PickerInfo picker)
        {
            if (index < 0 || index >= options.Count) return;

            var option = options[index];
            option.isPicked = true;

            // Apply the reward
            if (picker.isPlayer)
            {
                ApplyRewardToPlayer(option);
            }
            // Opponents just "get" the reward (no actual effect for mock opponents)

            // Update UI
            if (index < optionUIs.Count)
            {
                var ui = optionUIs[index];
                ui.SetPicked(picker.name);
            }

            // Add to history
            string historyEntry = $"{picker.name} picked {GetOptionName(option)}";
            pickHistory.Add(historyEntry);
            UpdatePickHistory();

            Crestforge.Visuals.AudioManager.Instance?.PlayPurchase();

            Debug.Log($"Mad Merchant: {historyEntry}");
        }

        private void ApplyRewardToPlayer(MerchantOption option)
        {
            var state = GameState.Instance;

            switch (option.optionType)
            {
                case MerchantOptionType.Item:
                    if (option.item != null)
                    {
                        state.itemInventory.Add(option.item);
                        GameUI.Instance?.RefreshItemInventory();
                    }
                    break;

                case MerchantOptionType.Crest:
                    if (option.crest != null && option.crest.type == CrestType.Minor)
                    {
                        // Replace existing minor crest if at max slots
                        if (state.minorCrests.Count >= GameConstants.Crests.MINOR_SLOTS)
                        {
                            state.minorCrests.Clear();
                        }
                        state.minorCrests.Add(option.crest);
                    }
                    break;

                case MerchantOptionType.CrestToken:
                    // Create a crest token consumable item and add to inventory
                    var crestToken = ScriptableObject.CreateInstance<ItemData>();
                    crestToken.itemId = "crest_token";
                    crestToken.itemName = "Crest Token";
                    crestToken.description = "Use to select a Minor Crest for your team.";
                    crestToken.rarity = ItemRarity.Rare;
                    crestToken.effect = ItemEffect.ConsumableCrestToken;
                    state.itemInventory.Add(crestToken);
                    GameUI.Instance?.RefreshItemInventory();
                    break;

                case MerchantOptionType.Gold:
                    state.player.gold += option.goldAmount;
                    break;
            }
        }

        private void UpdatePickHistory()
        {
            // Show last 4 picks
            int startIndex = Mathf.Max(0, pickHistory.Count - 4);
            var recent = pickHistory.GetRange(startIndex, pickHistory.Count - startIndex);
            pickHistoryText.text = string.Join("\n", recent);
        }

        private void CompleteMerchantRound()
        {
            isActive = false;
            Hide();

            // Notify RoundManager to proceed
            RoundManager.Instance?.OnMerchantRoundComplete();
        }

        public void Show()
        {
            merchantCanvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (merchantCanvas != null)
            {
                merchantCanvas.gameObject.SetActive(false);
            }
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

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

    /// <summary>
    /// Types of options available in the Mad Merchant
    /// </summary>
    public enum MerchantOptionType
    {
        Item,
        Crest,          // Specific crest (for future use)
        CrestToken,     // Triggers crest selection
        Gold
    }

    /// <summary>
    /// A single option in the Mad Merchant carousel
    /// </summary>
    [System.Serializable]
    public class MerchantOption
    {
        public MerchantOptionType optionType;
        public ItemData item;
        public CrestData crest;
        public int goldAmount;
        public bool isPicked;
    }

    /// <summary>
    /// Info about a picker in the pick order
    /// </summary>
    public class PickerInfo
    {
        public string name;
        public int health;
        public bool isPlayer;
        public OpponentData opponent;
    }

    /// <summary>
    /// UI component for a single merchant option
    /// </summary>
    public class MerchantOptionUI : MonoBehaviour
    {
        public MerchantOption option;
        public int index;
        public Image background;
        public Button button;
        public Text nameText;

        public void SetPicked(string pickerName)
        {
            // Gray out and disable
            background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            button.interactable = false;

            // Show who picked it
            if (nameText != null)
            {
                nameText.text = $"[{pickerName}]";
                nameText.color = new Color(0.5f, 0.5f, 0.5f);
            }
        }
    }
}
