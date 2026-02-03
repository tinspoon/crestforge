using System;
using System.Collections.Generic;
using UnityEngine;
using Crestforge.Core;
using Crestforge.Data;

namespace Crestforge.Networking
{
    /// <summary>
    /// Client-side representation of server game state.
    /// This replaces local GameState for multiplayer - all state comes from server.
    /// </summary>
    public class ServerGameState : MonoBehaviour
    {
        public static ServerGameState Instance { get; private set; }

        // Connection state
        public bool IsConnected => NetworkManager.Instance?.IsConnected ?? false;
        public bool IsInGame => NetworkManager.Instance?.IsInGame ?? false;

        // Game state from server
        [Header("Game State")]
        public string roomId;
        public string phase = "waiting"; // waiting, planning, combat, results, gameOver
        public int round = 1;
        public float phaseTimer = 0;

        // Local player state
        [Header("Local Player")]
        public string localPlayerId;
        public string localPlayerName;
        public int localBoardIndex; // Assigned board position (0-3)
        public string currentCombatTeam; // "player1" or "player2" for current combat
        public string currentHostPlayerId; // The player whose board hosts the combat
        public int gold;
        public int health;
        public int maxHealth;
        public int level;
        public int xp;
        public int xpToNext;
        public int maxUnits;
        public int winStreak;
        public int lossStreak;
        public bool isReady;
        public bool shopLocked;

        // Items, Crests, and Traits
        [Header("Items & Crests")]
        public List<ServerItemData> itemInventory = new List<ServerItemData>();
        public ServerCrestData minorCrest;
        public ServerCrestData majorCrest;
        public List<ServerActiveTraitEntry> activeTraits = new List<ServerActiveTraitEntry>();

        // Pending selections (from consumables)
        [Header("Pending Selections")]
        public List<ServerCrestData> pendingCrestSelection = new List<ServerCrestData>();
        public List<ServerItemData> pendingItemSelection = new List<ServerItemData>();

        // Board and bench - stored as serializable data
        [Header("Board")]
        public ServerUnitData[,] board;
        public ServerUnitData[] bench;
        public ServerShopUnit[] shop;

        // Other players
        [Header("Other Players")]
        public List<ServerPlayerData> otherPlayers = new List<ServerPlayerData>();

        // Combat state
        [Header("Combat")]
        public List<ServerMatchup> matchups = new List<ServerMatchup>();
        public List<ServerCombatResult> combatResults = new List<ServerCombatResult>();

        // Events
        public event Action OnStateUpdated;
        public event Action OnShopUpdated;
        public event Action OnBoardUpdated;
        public event Action<string> OnPhaseChanged;
        public event Action<int> OnRoundStarted;
        public event Action OnCombatStarted;
        public event Action OnCombatEnded;
        public event Action<string, string> OnGameEnded; // winnerId, winnerName
        public event Action<string, bool> OnActionResult; // action, success
        public event Action<int> OnBoardIndexAssigned; // boardIndex - fired when local player's board position is known
        public event Action OnSelectionAvailable; // fired when pendingCrestSelection or pendingItemSelection is populated

        // Unit data reference (for looking up unit templates)
        public UnitData[] allUnits;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeArrays();
                LoadUnitData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LoadUnitData()
        {
            // Load all unit data from Resources for template lookups
            var unitsList = new System.Collections.Generic.List<UnitData>();

            var newUnits = Resources.LoadAll<UnitData>("ScriptableObjects/NewUnits");
            if (newUnits != null && newUnits.Length > 0)
            {
                unitsList.AddRange(newUnits);
                            }

            // Also load from Units folder
            var units = Resources.LoadAll<UnitData>("ScriptableObjects/Units");
            if (units != null && units.Length > 0)
            {
                unitsList.AddRange(units);
                            }

            // Also load PvE units
            var pveUnits = Resources.LoadAll<UnitData>("ScriptableObjects/PvEUnits");
            if (pveUnits != null && pveUnits.Length > 0)
            {
                unitsList.AddRange(pveUnits);
            }
            else
            {
                Debug.LogWarning($"[ServerGameState] No PvE units found in ScriptableObjects/PvEUnits");
            }

            allUnits = unitsList.ToArray();
        }

        private void Start()
        {
            SubscribeToNetworkEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        private void Update()
        {
            // Client-side timer countdown during planning phase
            if (phase == "planning" && phaseTimer > 0)
            {
                phaseTimer -= Time.deltaTime;
                if (phaseTimer < 0) phaseTimer = 0;
            }
        }

        private void InitializeArrays()
        {
            board = new ServerUnitData[7, 4];
            bench = new ServerUnitData[GameConstants.Player.BENCH_SIZE];
            shop = new ServerShopUnit[5];
        }

        private void SubscribeToNetworkEvents()
        {
            var nm = NetworkManager.Instance;
            if (nm == null)
            {
                // Try again later
                Invoke(nameof(SubscribeToNetworkEvents), 0.5f);
                return;
            }

            
            nm.OnConnected += HandleConnected;
            nm.OnDisconnected += HandleDisconnected;
            nm.OnGameStateReceived += HandleGameState;
            nm.OnPhaseUpdate += HandlePhaseUpdate;
            nm.OnGameStart += HandleGameStart;
            nm.OnRoundStart += HandleRoundStart;
            nm.OnCombatStart += HandleCombatStart;
            nm.OnCombatEnd += HandleCombatEnd;
            nm.OnGameEnd += HandleGameEnd;
            nm.OnActionResult += HandleActionResult;

            // If already connected, grab the client ID now
            if (nm.IsConnected && !string.IsNullOrEmpty(nm.clientId))
            {
                                localPlayerId = nm.clientId;
                localPlayerName = nm.playerName;
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;

            nm.OnConnected -= HandleConnected;
            nm.OnDisconnected -= HandleDisconnected;
            nm.OnGameStateReceived -= HandleGameState;
            nm.OnPhaseUpdate -= HandlePhaseUpdate;
            nm.OnGameStart -= HandleGameStart;
            nm.OnRoundStart -= HandleRoundStart;
            nm.OnCombatStart -= HandleCombatStart;
            nm.OnCombatEnd -= HandleCombatEnd;
            nm.OnGameEnd -= HandleGameEnd;
            nm.OnActionResult -= HandleActionResult;
        }

        // ============================================
        // Network Event Handlers
        // ============================================

        private void HandleConnected()
        {
            var nm = NetworkManager.Instance;
            if (nm != null)
            {
                localPlayerId = nm.clientId;
                localPlayerName = nm.playerName;
                            }
            else
            {
                Debug.LogWarning("[ServerGameState] HandleConnected called but NetworkManager.Instance is null!");
            }
        }

        private void HandleDisconnected()
        {
            // Reset state
            phase = "waiting";
            round = 1;
            localBoardIndex = 0;
            boardIndexReceived = false;
            InitializeArrays();
            otherPlayers.Clear();
        }

        private void HandleGameState(ServerGameStateData state)
        {
            if (state == null)
            {
                Debug.LogWarning("[ServerGameState] Received null game state!");
                return;
            }

            
            // Update game info
            roomId = state.roomId;
            phase = state.phase;
            round = state.round;
            phaseTimer = state.phaseTimer;

            // Find local player data
            otherPlayers.Clear();
            bool foundLocalPlayer = false;

            if (state.players == null || state.players.Count == 0)
            {
                Debug.LogWarning("[ServerGameState] No players in state!");
                return;
            }

            
            foreach (var playerData in state.players)
            {
                int boardUnitCount = playerData.boardUnits?.Count ?? 0;
                int benchCount = 0;
                if (playerData.bench != null)
                {
                    foreach (var b in playerData.bench)
                    {
                        if (b != null && !string.IsNullOrEmpty(b.instanceId)) benchCount++;
                    }
                }
                
                if (playerData.clientId == localPlayerId)
                {
                    // Update local player state
                                        UpdateLocalPlayerState(playerData);
                    foundLocalPlayer = true;
                }
                else
                {
                    // Store other player data
                    otherPlayers.Add(playerData);
                }
            }

            if (!foundLocalPlayer)
            {
                Debug.LogWarning($"[ServerGameState] Local player '{localPlayerId}' not found in state!");
            }

            // Update matchups if present
            if (state.matchups != null)
            {
                matchups = state.matchups;
            }

            OnStateUpdated?.Invoke();
        }

        private bool boardIndexReceived = false;

        private void UpdateLocalPlayerState(ServerPlayerData data)
        {
            int previousBoardIndex = localBoardIndex;
            localBoardIndex = data.boardIndex;

            // Fire event if board index was just assigned (first time we receive it)
            if (!boardIndexReceived)
            {
                boardIndexReceived = true;
                                OnBoardIndexAssigned?.Invoke(localBoardIndex);
            }
            else if (previousBoardIndex != localBoardIndex)
            {
                // Board index changed (shouldn't normally happen)
                                OnBoardIndexAssigned?.Invoke(localBoardIndex);
            }

            gold = data.gold;
            health = data.health;
            maxHealth = data.maxHealth;
            level = data.level;
            xp = data.xp;
            xpToNext = data.xpToNext;
            maxUnits = data.maxUnits;
            winStreak = data.winStreak;
            lossStreak = data.lossStreak;
            isReady = data.isReady;
            shopLocked = data.shopLocked;

            
            // Update board from flat list with coordinates
            // First clear the board
            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    board[x, y] = null;
                }
            }

            // Then populate from the flat list
            if (data.boardUnits != null && data.boardUnits.Count > 0)
            {
                                foreach (var boardUnit in data.boardUnits)
                {
                    if (boardUnit != null && boardUnit.unit != null &&
                        boardUnit.x >= 0 && boardUnit.x < 7 &&
                        boardUnit.y >= 0 && boardUnit.y < 4)
                    {
                                                board[boardUnit.x, boardUnit.y] = boardUnit.unit;
                    }
                }
                OnBoardUpdated?.Invoke();
            }

            // Update bench
            if (data.bench != null)
            {
                for (int i = 0; i < GameConstants.Player.BENCH_SIZE && i < data.bench.Length; i++)
                {
                    bench[i] = data.bench[i];
                }
            }

            // Update shop
            if (data.shop != null)
            {
                for (int i = 0; i < 5 && i < data.shop.Length; i++)
                {
                    shop[i] = data.shop[i];
                }
                OnShopUpdated?.Invoke();
            }

            // Update items
            if (data.itemInventory != null)
            {
                itemInventory = data.itemInventory;
            }

            // Update crests
            minorCrest = data.minorCrest;
            majorCrest = data.majorCrest;

            // Update active traits
            if (data.activeTraits != null)
            {
                activeTraits = data.activeTraits;
            }

            // Update pending selections
            bool hadCrestSelection = pendingCrestSelection != null && pendingCrestSelection.Count > 0;
            bool hadItemSelection = pendingItemSelection != null && pendingItemSelection.Count > 0;

            if (data.pendingCrestSelection != null)
            {
                pendingCrestSelection = data.pendingCrestSelection;
            }
            else
            {
                pendingCrestSelection = new List<ServerCrestData>();
            }

            if (data.pendingItemSelection != null)
            {
                pendingItemSelection = data.pendingItemSelection;
            }
            else
            {
                pendingItemSelection = new List<ServerItemData>();
            }

            // Fire event if selections became available
            bool hasCrestSelection = pendingCrestSelection.Count > 0;
            bool hasItemSelection = pendingItemSelection.Count > 0;
            if ((!hadCrestSelection && hasCrestSelection) || (!hadItemSelection && hasItemSelection))
            {
                OnSelectionAvailable?.Invoke();
            }
        }

        private void HandlePhaseUpdate(string newPhase, float timer, int newRound)
        {
            string oldPhase = phase;
            phase = newPhase;
            phaseTimer = timer;
            round = newRound;

            
            if (oldPhase != newPhase)
            {
                OnPhaseChanged?.Invoke(newPhase);
            }
        }

        private void HandleGameStart(int startRound)
        {
                        string oldPhase = phase;
            round = startRound;
            phase = "planning";

            // Fire phase changed event
            if (oldPhase != "planning")
            {
                OnPhaseChanged?.Invoke("planning");
            }
        }

        private void HandleRoundStart(int newRound)
        {
                        string oldPhase = phase;
            round = newRound;
            phase = "planning";

            // Fire phase changed event so victory pose ends
            if (oldPhase != "planning")
            {
                OnPhaseChanged?.Invoke("planning");
            }

            OnRoundStarted?.Invoke(newRound);
        }

        private void HandleCombatStart(List<ServerMatchup> newMatchups, Dictionary<string, ServerBoardState> boards)
        {
                        phase = "combat";
            matchups = newMatchups ?? new List<ServerMatchup>();

            // Find our matchup and store the host player ID
            currentHostPlayerId = null;
            foreach (var matchup in matchups)
            {
                if (matchup.player1 == localPlayerId || matchup.player2 == localPlayerId)
                {
                    currentHostPlayerId = matchup.hostPlayerId;
                                        break;
                }
            }

            OnCombatStarted?.Invoke();
        }

        private void HandleCombatEnd(List<ServerCombatResult> results)
        {
                        phase = "results";
            combatResults = results ?? new List<ServerCombatResult>();
            OnCombatEnded?.Invoke();
        }

        private void HandleGameEnd(string winnerId, string winnerName)
        {
                        phase = "gameOver";
            OnGameEnded?.Invoke(winnerId, winnerName);
        }

        private void HandleActionResult(string action, bool success, string error)
        {
            if (!success)
            {
                Debug.LogWarning($"[ServerGameState] Action '{action}' failed: {error}");
            }
            OnActionResult?.Invoke(action, success);
        }

        // ============================================
        // Actions (send to server)
        // ============================================

        public void BuyUnit(int shopIndex)
        {
            SendAction(new GameAction { type = "buyUnit", shopIndex = shopIndex });
        }

        public void SellUnit(string instanceId)
        {
            SendAction(new GameAction { type = "sellUnit", instanceId = instanceId });
        }

        public void PlaceUnit(string instanceId, int x, int y)
        {
            SendAction(new GameAction { type = "placeUnit", instanceId = instanceId, x = x, y = y });
        }

        public void BenchUnit(string instanceId, int targetSlot = -1)
        {
            SendAction(new GameAction { type = "benchUnit", instanceId = instanceId, targetSlot = targetSlot });
        }

        public void MoveBenchUnit(string instanceId, int targetSlot)
        {
            SendAction(new GameAction { type = "moveBenchUnit", instanceId = instanceId, targetSlot = targetSlot });
        }

        public void Reroll()
        {
            SendAction(new GameAction { type = "reroll" });
        }

        public void BuyXP()
        {
            SendAction(new GameAction { type = "buyXP" });
        }

        public void ToggleShopLock()
        {
            SendAction(new GameAction { type = "toggleShopLock" });
        }

        public void SetReady(bool ready)
        {
            SendAction(new GameAction { type = "ready", ready = ready });
        }

        // ============================================
        // Item & Crest Actions
        // ============================================

        public void EquipItem(int itemIndex, string instanceId)
        {
            SendAction(new GameAction { type = "equipItem", itemIndex = itemIndex, instanceId = instanceId });
        }

        public void UnequipItem(string instanceId, int itemSlot)
        {
            SendAction(new GameAction { type = "unequipItem", instanceId = instanceId, itemSlot = itemSlot });
        }

        public void CombineItems(int itemIndex1, int itemIndex2)
        {
            SendAction(new GameAction { type = "combineItems", itemIndex1 = itemIndex1, itemIndex2 = itemIndex2 });
        }

        public void SelectMinorCrest(string crestId)
        {
            SendAction(new GameAction { type = "selectMinorCrest", crestId = crestId });
        }

        public void SelectMajorCrest(string crestId)
        {
            SendAction(new GameAction { type = "selectMajorCrest", crestId = crestId });
        }

        // Consumable Actions
        public void UseConsumable(int itemIndex)
        {
            SendAction(new GameAction { type = "useConsumable", itemIndex = itemIndex });
        }

        public void SelectCrestChoice(int choiceIndex)
        {
            SendAction(new GameAction { type = "selectCrestChoice", choiceIndex = choiceIndex });
        }

        public void SelectItemChoice(int choiceIndex)
        {
            SendAction(new GameAction { type = "selectItemChoice", choiceIndex = choiceIndex });
        }

        // ============================================
        // Loot Actions
        // ============================================

        public void CollectLoot(string lootId)
        {
            SendAction(new GameAction { type = "collectLoot", lootId = lootId });
        }

        private void SendAction(GameAction action)
        {
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsInGame)
            {
                Debug.LogWarning("[ServerGameState] Cannot send action - not in game");
                return;
            }

            nm.SendGameAction(action);
        }

        // ============================================
        // Utility Methods
        // ============================================

        public int GetBoardUnitCount()
        {
            int count = 0;
            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    if (board[x, y] != null) count++;
                }
            }
            return count;
        }

        public int GetBenchUnitCount()
        {
            int count = 0;
            for (int i = 0; i < bench.Length; i++)
            {
                if (bench[i] != null) count++;
            }
            return count;
        }

        public ServerUnitData GetUnitByInstanceId(string instanceId)
        {
            // Check board
            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    if (board[x, y]?.instanceId == instanceId)
                        return board[x, y];
                }
            }

            // Check bench
            for (int i = 0; i < bench.Length; i++)
            {
                if (bench[i]?.instanceId == instanceId)
                    return bench[i];
            }

            return null;
        }

        public UnitData GetUnitTemplate(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;

            if (allUnits == null)
            {
                Debug.LogWarning($"[ServerGameState] GetUnitTemplate: allUnits is null! Attempting reload...");
                LoadUnitData();
                if (allUnits == null) return null;
            }

            foreach (var unit in allUnits)
            {
                if (unit != null && unit.unitId == unitId)
                    return unit;
            }

            // Try direct load by name (handles case where unit wasn't loaded at startup)
            // This catches PvE units that might have been missed
            string capitalized = char.ToUpper(unitId[0]) + unitId.Substring(1);
            string[] searchPaths = new string[]
            {
                $"ScriptableObjects/PvEUnits/{capitalized}", // Capitalized name
                $"ScriptableObjects/PvEUnits/{unitId}", // lowercase name
                $"ScriptableObjects/NewUnits/{capitalized}",
                $"ScriptableObjects/Units/{capitalized}"
            };

            foreach (var path in searchPaths)
            {
                var loaded = Resources.Load<UnitData>(path);
                if (loaded != null)
                {
                                        // Add to our cache for future lookups
                    var newList = new System.Collections.Generic.List<UnitData>(allUnits);
                    newList.Add(loaded);
                    allUnits = newList.ToArray();
                    return loaded;
                }
            }

            // Debug: log what units ARE available if we didn't find the requested one
            Debug.LogWarning($"[ServerGameState] GetUnitTemplate: unitId '{unitId}' not found after direct load attempts. Available units ({allUnits.Length}): {string.Join(", ", System.Linq.Enumerable.Take(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(allUnits, u => u != null), u => u.unitId), 10))}...");

            return null;
        }

        public bool IsLocalPlayerTurn()
        {
            return phase == "planning";
        }

        public ServerPlayerData GetOpponentData(string opponentId)
        {
            return otherPlayers.Find(p => p.clientId == opponentId);
        }

        /// <summary>
        /// Get a player's board index by their ID (for combat visualization)
        /// </summary>
        public int GetPlayerBoardIndex(string playerId)
        {
            if (playerId == localPlayerId)
            {
                return localBoardIndex;
            }
            var opponent = otherPlayers.Find(p => p.clientId == playerId);
            return opponent?.boardIndex ?? -1;
        }

        /// <summary>
        /// Get the current combat opponent (the player we're fighting against)
        /// Returns null if not in combat or no opponent found
        /// </summary>
        public ServerPlayerData GetCurrentCombatOpponent()
        {
            if (phase != "combat" || matchups == null) return null;

            foreach (var matchup in matchups)
            {
                if (matchup.player1 == localPlayerId)
                {
                    return otherPlayers.Find(p => p.clientId == matchup.player2);
                }
                else if (matchup.player2 == localPlayerId)
                {
                    return otherPlayers.Find(p => p.clientId == matchup.player1);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns true if the local player is hosting the current combat (fighting on their board)
        /// </summary>
        public bool IsLocalPlayerCombatHost()
        {
            return phase == "combat" && currentHostPlayerId == localPlayerId;
        }
    }

    // ============================================
    // Data Structures for Server Communication
    // ============================================

    [Serializable]
    public class ServerGameStateData
    {
        public string roomId;
        public string hostId;
        public string state;
        public string phase;
        public int round;
        public float phaseTimer;
        public List<ServerPlayerData> players;
        public List<ServerMatchup> matchups;
    }

    [Serializable]
    public class ServerPlayerData
    {
        public string clientId;
        public string name;
        public int boardIndex;
        public int gold;
        public int health;
        public int maxHealth;
        public int level;
        public int xp;
        public int xpToNext;
        public int maxUnits;
        public int winStreak;
        public int lossStreak;
        public bool isReady;
        public bool shopLocked;
        public bool isEliminated;
        public List<ServerBoardUnit> boardUnits; // Flat list with coordinates (replaces 2D array)
        public ServerUnitData[] bench;   // 9 slots
        public ServerShopUnit[] shop;    // 5 slots
        // Items, Crests, and Traits
        public List<ServerItemData> itemInventory;
        public ServerCrestData minorCrest;
        public ServerCrestData majorCrest;
        public List<ServerActiveTraitEntry> activeTraits;
        // Pending selections (from consumables like crest_token or item_anvil)
        public List<ServerCrestData> pendingCrestSelection;
        public List<ServerItemData> pendingItemSelection;
    }

    [Serializable]
    public class ServerBoardUnit
    {
        public int x;
        public int y;
        public ServerUnitData unit;
    }

    [Serializable]
    public class ServerUnitData
    {
        public string instanceId;
        public string unitId;
        public string name;
        public int cost;
        public string[] traits;
        public int starLevel;
        public ServerUnitStats currentStats;
        public int currentHealth;
        public int currentMana;
        // Items equipped on this unit
        public List<ServerItemData> items;
    }

    [Serializable]
    public class ServerUnitStats
    {
        public int health;
        public int attack;
        public int armor;
        public int magicResist;
        public float attackSpeed;
        public int range;
        public int mana;
    }

    [Serializable]
    public class ServerShopUnit
    {
        public string unitId;
        public string name;
        public int cost;
        public string[] traits;
    }

    [Serializable]
    public class ServerMatchup
    {
        public string player1;
        public string player2;
        public bool isGhost;
        public string hostPlayerId; // Combat happens on this player's board
    }

    [Serializable]
    public class ServerCombatResult
    {
        public string player1;
        public string player2;
        public string winnerId;
        public string loserId;
        public int damage;
    }

    [Serializable]
    public class ServerBoardState
    {
        public string playerId;
        public string playerName;
        public ServerUnitData[][] board;
        public ServerUnitData[] bench;
    }

    [Serializable]
    public class GameAction
    {
        public string type;
        public int shopIndex;
        public string instanceId;
        public int x;
        public int y;
        public bool ready;
        // Bench actions
        public int targetSlot;
        // Item/Crest actions
        public int itemIndex;
        public int itemIndex1;
        public int itemIndex2;
        public int itemSlot;
        public string crestId;
        // Loot actions
        public string lootId;
        // Selection actions
        public int choiceIndex;
    }

    [Serializable]
    public class ServerItemData
    {
        public string itemId;
        public string name;
        public string description;
        public bool isCombined;
        public ServerItemStats stats;
    }

    [Serializable]
    public class ServerItemStats
    {
        public int attack;
        public int health;
        public int armor;
        public int magicResist;
        public int mana;
        public int range;
        public float attackSpeedPercent;
        public int abilityPower;
        public int critChance;
        public int critDamagePercent;
    }

    [Serializable]
    public class ServerCrestData
    {
        public string crestId;
        public string name;
        public string description;
        public string type; // "minor" or "major"
        public string grantsTrait; // For minor crests
        public ServerCrestBonus teamBonus; // For major crests
    }

    [Serializable]
    public class ServerCrestBonus
    {
        public int health;
        public int attack;
        public int armor;
        public int magicResist;
        public float attackSpeedPercent;
        public int abilityPower;
        public int lifesteal;
    }

    [Serializable]
    public class ServerActiveTraitData
    {
        public int count;
        public int tierCount;
        public ServerTraitBonus bonus;
    }

    [Serializable]
    public class ServerActiveTraitEntry
    {
        public string traitId;
        public int count;
        public int tierCount;
        public ServerTraitBonus bonus;
    }

    [Serializable]
    public class ServerTraitBonus
    {
        public int healthPercent;
        public int attackPercent;
        public int attackSpeedPercent;
        public int armor;
        public int magicResist;
        public int allStatsPercent;
        public int critChance;
        public int critDamagePercent;
        public int dodgeChance;
        public int magicDamagePercent;
        public int lifesteal;
    }
}
