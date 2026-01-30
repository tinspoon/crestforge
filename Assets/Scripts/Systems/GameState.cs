using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;

namespace Crestforge.Systems
{
    public enum GameMode
    {
        PvEWave,    // 3 lives
        PvP,        // 100 health (future)
        CoOp        // 100 health shared (future)
    }

    [System.Serializable]
    public class PlayerState
    {
        public int health;
        public int maxHealth;
        public int gold;
        public int level;
        public int xp;
        public int winStreak;
        public int lossStreak;

        public static PlayerState CreateDefault(GameMode mode = GameMode.PvEWave)
        {
            int startingHealth = mode switch
            {
                GameMode.PvEWave => 3,  // 3 lives for wave mode
                GameMode.PvP => 100,
                GameMode.CoOp => 100,
                _ => 3
            };

            return new PlayerState
            {
                health = startingHealth,
                maxHealth = startingHealth,
                gold = GameConstants.Player.STARTING_GOLD,
                level = GameConstants.Player.STARTING_LEVEL,
                xp = 0,
                winStreak = 0,
                lossStreak = 0
            };
        }

        public int GetMaxUnits()
        {
            return GameConstants.Leveling.UNITS_PER_LEVEL[level];
        }

        public int GetXPToNextLevel()
        {
            if (level >= GameConstants.Leveling.MAX_LEVEL) return 0;
            return GameConstants.Leveling.XP_REQUIRED[level + 1] - xp;
        }

        public bool CanLevelUp()
        {
            if (level >= GameConstants.Leveling.MAX_LEVEL) return false;
            return xp >= GameConstants.Leveling.XP_REQUIRED[level + 1];
        }
    }

    [System.Serializable]
    public class RoundState
    {
        public int currentRound;
        public GamePhase phase;
        public float phaseTimer;

        public static RoundState CreateDefault()
        {
            return new RoundState
            {
                currentRound = 1,
                phase = GamePhase.CrestSelect,
                phaseTimer = 0
            };
        }
    }

    [System.Serializable]
    public class ShopState
    {
        public List<UnitInstance> availableUnits = new List<UnitInstance>();
        public bool isLocked;

        public void Refresh(UnitPool pool, int playerLevel)
        {
            availableUnits.Clear();
            
            for (int i = 0; i < GameConstants.Economy.SHOP_SIZE; i++)
            {
                var unit = pool.RollUnit(playerLevel);
                if (unit != null)
                {
                    availableUnits.Add(unit);
                }
            }
        }

        public void ReturnToPool(UnitPool pool)
        {
            if (pool == null) return;
            
            foreach (var unit in availableUnits)
            {
                if (unit != null && unit.template != null)
                {
                    pool.ReturnUnit(unit.template, 1);
                }
            }
        }
    }

    [System.Serializable]
    public class UnitPool
    {
        private Dictionary<string, int> pool = new Dictionary<string, int>();
        private UnitData[] allUnits;

        public void Initialize(UnitData[] units)
        {
            if (units == null)
            {
                Debug.LogError("UnitPool.Initialize called with null units array!");
                return;
            }
            
            allUnits = units;
            pool.Clear();
            
            foreach (var unit in units)
            {
                if (unit != null && !string.IsNullOrEmpty(unit.unitId))
                {
                    pool[unit.unitId] = GameConstants.Units.POOL_SIZE;
                }
            }
        }

        public UnitInstance RollUnit(int playerLevel)
        {
            if (allUnits == null || allUnits.Length == 0) return null;
            
            int roll = Random.Range(0, 100);
            int cumulative = 0;
            int costTier = 1;

            for (int i = 0; i < 4; i++)
            {
                cumulative += GameConstants.ShopOdds.UNIT_ODDS[playerLevel, i];
                if (roll < cumulative)
                {
                    costTier = i + 1;
                    break;
                }
            }

            var available = new List<UnitData>();
            foreach (var unit in allUnits)
            {
                if (unit != null && unit.cost == costTier && GetAvailable(unit.unitId) > 0)
                {
                    available.Add(unit);
                }
            }

            if (available.Count == 0) return null;

            var selected = available[Random.Range(0, available.Count)];
            pool[selected.unitId]--;
            
            return UnitInstance.Create(selected);
        }

        public void ReturnUnit(UnitData unit, int count)
        {
            if (unit == null || string.IsNullOrEmpty(unit.unitId)) return;
            
            if (!pool.ContainsKey(unit.unitId))
            {
                pool[unit.unitId] = 0;
            }
            pool[unit.unitId] = Mathf.Min(GameConstants.Units.POOL_SIZE, pool[unit.unitId] + count);
        }

        public int GetAvailable(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return 0;
            return pool.ContainsKey(unitId) ? pool[unitId] : 0;
        }
    }

    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [Header("Game Mode")]
        public GameMode currentGameMode = GameMode.PvEWave;

        [Header("State")]
        public PlayerState player;
        public RoundState round;
        public ShopState shop;
        public UnitPool unitPool;

        [Header("Board")]
        public UnitInstance[,] playerBoard;
        public List<UnitInstance> bench = new List<UnitInstance>();

        [Header("Enemy")]
        public UnitInstance[,] enemyBoard;

        [Header("Crests & Items")]
        public List<CrestData> minorCrests = new List<CrestData>();
        public List<CrestData> majorCrests = new List<CrestData>();
        public List<ItemData> itemInventory = new List<ItemData>();
        public List<ItemData> pendingItemSelection = new List<ItemData>();
        public List<CrestData> pendingCrestSelection = new List<CrestData>();

        [Header("Trait Tracking")]
        public Dictionary<TraitData, int> activeTraits = new Dictionary<TraitData, int>();

        [Header("Data References")]
        public UnitData[] allUnits;
        public ItemData[] allItems;
        public CrestData[] allCrests;
        public TraitData[] allTraits;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeGame();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void InitializeGame(GameMode mode = GameMode.PvEWave)
        {
            currentGameMode = mode;
            player = PlayerState.CreateDefault(mode);
            round = RoundState.CreateDefault();
            shop = new ShopState();
            unitPool = new UnitPool();

            playerBoard = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];
            enemyBoard = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];
            bench.Clear();

            minorCrests.Clear();
            majorCrests.Clear();
            itemInventory.Clear();

            if (allUnits != null && allUnits.Length > 0)
            {
                unitPool.Initialize(allUnits);
            }
            else
            {
                Debug.LogWarning("GameState: allUnits is empty! Run Crestforge > Complete Setup");
            }

            if (allCrests != null && allCrests.Length > 0)
            {
                GenerateCrestSelection(CrestType.Minor, 3);
            }
            else
            {
                Debug.LogWarning("GameState: allCrests is empty! Run Crestforge > Complete Setup");
            }

            Debug.Log("Game initialized!");
        }

        public int CalculateIncome()
        {
            int income = GameConstants.Economy.BASE_GOLD_PER_TURN;
            int interest = Mathf.Min(player.gold / 5, GameConstants.Economy.MAX_INTEREST);
            income += interest;
            return income;
        }

        public bool BuyUnit(int shopIndex)
        {
            if (shop == null || shop.availableUnits == null) return false;
            if (shopIndex < 0 || shopIndex >= shop.availableUnits.Count) return false;
            
            var unit = shop.availableUnits[shopIndex];
            if (unit == null || unit.template == null) return false;
            if (player.gold < unit.template.cost) return false;
            if (bench.Count >= GameConstants.Player.BENCH_SIZE) return false;

            player.gold -= unit.template.cost;
            bench.Add(unit);
            shop.availableUnits[shopIndex] = null;

            CheckAndPerformMerge(unit);

            return true;
        }

        public void SellUnit(UnitInstance unit)
        {
            if (unit == null || unit.template == null) return;
            
            player.gold += unit.GetSellValue();
            
            int unitsToReturn = (int)Mathf.Pow(2, unit.starLevel - 1);
            unitPool.ReturnUnit(unit.template, unitsToReturn);

            RemoveUnit(unit);
            RecalculateTraits();
        }

        public bool RerollShop()
        {
            if (player.gold < GameConstants.Economy.REROLL_COST) return false;

            player.gold -= GameConstants.Economy.REROLL_COST;
            
            if (shop != null)
            {
                shop.ReturnToPool(unitPool);
                shop.Refresh(unitPool, player.level);
            }

            return true;
        }

        public bool BuyXP()
        {
            if (player.gold < GameConstants.Economy.XP_COST) return false;
            if (player.level >= GameConstants.Leveling.MAX_LEVEL) return false;

            player.gold -= GameConstants.Economy.XP_COST;
            player.xp += GameConstants.Economy.XP_PER_PURCHASE;

            while (player.CanLevelUp())
            {
                player.level++;
                Debug.Log($"Level up! Now level {player.level}");
            }

            return true;
        }

        public bool PlaceUnit(UnitInstance unit, int col, int row)
        {
            if (unit == null) return false;
            if (!IsValidBoardPosition(col, row)) return false;

            int boardCount = GetBoardUnitCount();
            bool isSwapping = playerBoard[col, row] != null;

            if (boardCount >= player.GetMaxUnits() && !isSwapping)
            {
                return false;
            }

            bench.Remove(unit);

            if (playerBoard[col, row] != null)
            {
                var swappedUnit = playerBoard[col, row];
                swappedUnit.isOnBoard = false;
                bench.Add(swappedUnit);
            }

            playerBoard[col, row] = unit;
            unit.isOnBoard = true;
            unit.boardPosition = new Vector2Int(col, row);

            RecalculateTraits();

            return true;
        }

        public bool ReturnToBench(UnitInstance unit)
        {
            if (unit == null) return false;
            if (!unit.isOnBoard) return false;
            if (bench.Count >= GameConstants.Player.BENCH_SIZE) return false;

            playerBoard[unit.boardPosition.x, unit.boardPosition.y] = null;
            unit.isOnBoard = false;
            bench.Add(unit);

            RecalculateTraits();

            return true;
        }

        private void CheckAndPerformMerge(UnitInstance newUnit)
        {
            if (newUnit == null || newUnit.template == null) return;
            
            var matches = new List<UnitInstance>();
            
            foreach (var unit in bench)
            {
                if (unit != null && unit != newUnit && 
                    unit.template == newUnit.template && 
                    unit.starLevel == newUnit.starLevel)
                {
                    matches.Add(unit);
                }
            }

            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    var unit = playerBoard[x, y];
                    if (unit != null && 
                        unit != newUnit && 
                        unit.template == newUnit.template && 
                        unit.starLevel == newUnit.starLevel)
                    {
                        matches.Add(unit);
                    }
                }
            }

            if (matches.Count >= 1)
            {
                var mergeTarget = matches[0];
                
                RemoveUnit(newUnit);
                
                mergeTarget.starLevel++;
                mergeTarget.RecalculateStats();
                mergeTarget.currentHealth = mergeTarget.currentStats.health;

                Debug.Log($"Merged into {mergeTarget.GetDisplayName()}!");

                if (mergeTarget.starLevel < GameConstants.Units.MAX_STAR_LEVEL)
                {
                    CheckAndPerformMerge(mergeTarget);
                }
            }
        }

        private void RemoveUnit(UnitInstance unit)
        {
            if (unit == null) return;
            
            if (unit.isOnBoard)
            {
                playerBoard[unit.boardPosition.x, unit.boardPosition.y] = null;
            }
            else
            {
                bench.Remove(unit);
            }
        }

        public int GetBoardUnitCount()
        {
            int count = 0;
            if (playerBoard == null) return 0;
            
            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    if (playerBoard[x, y] != null) count++;
                }
            }
            return count;
        }

        public List<UnitInstance> GetBoardUnits()
        {
            var units = new List<UnitInstance>();
            if (playerBoard == null) return units;
            
            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    if (playerBoard[x, y] != null)
                    {
                        units.Add(playerBoard[x, y]);
                    }
                }
            }
            return units;
        }

        private bool IsValidBoardPosition(int col, int row)
        {
            return col >= 0 && col < GameConstants.Grid.WIDTH &&
                   row >= 0 && row < GameConstants.Grid.HEIGHT;
        }

        public void RecalculateTraits()
        {
            activeTraits.Clear();

            var boardUnits = GetBoardUnits();
            
            foreach (var unit in boardUnits)
            {
                if (unit == null || unit.template == null || unit.template.traits == null) continue;
                
                foreach (var trait in unit.template.traits)
                {
                    if (trait == null) continue;
                    
                    if (!activeTraits.ContainsKey(trait))
                    {
                        activeTraits[trait] = 0;
                    }
                    activeTraits[trait]++;
                }
            }

            foreach (var unit in boardUnits)
            {
                if (unit == null) continue;
                var traitBonus = CalculateTraitBonuses(unit);
                unit.RecalculateStats(traitBonus);
                
                // Outside of combat, keep units at full health
                if (unit.currentStats != null)
                {
                    unit.currentHealth = unit.currentStats.health;
                }
            }
        }

        private UnitStats CalculateTraitBonuses(UnitInstance unit)
        {
            var bonus = new UnitStats();
            if (unit == null) return bonus;

            foreach (var traitPair in activeTraits)
            {
                var trait = traitPair.Key;
                if (trait == null) continue;
                
                int count = traitPair.Value;
                int tier = trait.GetActiveTier(count);

                if (tier < 0) continue;

                var traitBonus = trait.GetBonusForTier(tier);
                if (traitBonus == null) continue;

                bonus.health += traitBonus.globalBonusHealth;
                bonus.attack += traitBonus.globalBonusAttack;
                bonus.armor += traitBonus.globalBonusArmor;
                bonus.magicResist += traitBonus.globalBonusMagicResist;
                bonus.attackSpeed += traitBonus.globalBonusAttackSpeed;

                if (unit.HasTrait(trait))
                {
                    bonus.health += traitBonus.bonusHealth;
                    bonus.attack += traitBonus.bonusAttack;
                    bonus.armor += traitBonus.bonusArmor;
                    bonus.magicResist += traitBonus.bonusMagicResist;
                    bonus.attackSpeed += traitBonus.bonusAttackSpeed;
                }
            }

            return bonus;
        }

        public void GenerateCrestSelection(CrestType type, int count)
        {
            pendingCrestSelection.Clear();
            
            if (allCrests == null) return;
            
            var available = new List<CrestData>();
            foreach (var crest in allCrests)
            {
                if (crest != null && crest.type == type)
                {
                    available.Add(crest);
                }
            }

            for (int i = 0; i < Mathf.Min(count, available.Count); i++)
            {
                int randomIndex = Random.Range(i, available.Count);
                var temp = available[i];
                available[i] = available[randomIndex];
                available[randomIndex] = temp;
                
                pendingCrestSelection.Add(available[i]);
            }
        }

        public void SelectCrest(CrestData crest)
        {
            if (crest == null) return;
            
            if (crest.type == CrestType.Minor)
            {
                if (minorCrests.Count < GameConstants.Crests.MINOR_SLOTS)
                {
                    minorCrests.Add(crest);
                }
            }
            else
            {
                if (majorCrests.Count < GameConstants.Crests.MAJOR_SLOTS)
                {
                    majorCrests.Add(crest);
                }
            }

            pendingCrestSelection.Clear();
            round.phase = GamePhase.Planning;
        }

        public void GenerateItemSelection(int count)
        {
            pendingItemSelection.Clear();
            
            if (allItems == null) return;
            
            var weighted = new List<ItemData>();
            foreach (var item in allItems)
            {
                if (item == null) continue;
                int weight = item.GetRarityWeight();
                for (int i = 0; i < weight; i++)
                {
                    weighted.Add(item);
                }
            }

            var selected = new HashSet<ItemData>();
            while (selected.Count < count && weighted.Count > 0)
            {
                int index = Random.Range(0, weighted.Count);
                var item = weighted[index];
                selected.Add(item);
                weighted.RemoveAll(i => i == item);
            }

            pendingItemSelection.AddRange(selected);
        }

        public void SelectItem(ItemData item)
        {
            if (item == null) return;
            
            itemInventory.Add(item);
            pendingItemSelection.Clear();
            round.phase = GamePhase.Planning;
        }
    }
}