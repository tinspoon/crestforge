using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;

namespace Crestforge.Systems
{
    /// <summary>
    /// Generates enemy waves for PvE combat
    /// </summary>
    public class EnemyWaveGenerator : MonoBehaviour
    {
        public static EnemyWaveGenerator Instance { get; private set; }

        [Header("References")]
        public UnitData[] allUnits;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Generate enemy board for a given round
        /// </summary>
        public UnitInstance[,] GenerateWave(int round)
        {
            // Check for special PvE rounds
            var roundType = GetRoundType(round);
            if (roundType == RoundType.PvEIntro)
            {
                return GeneratePvEIntroWave();
            }
            else if (roundType == RoundType.PvELoot)
            {
                return GeneratePvELootWave(round);
            }
            else if (roundType == RoundType.PvEBoss)
            {
                return GeneratePvEBossWave(round);
            }

            // Standard wave generation
            var template = GetWaveTemplate(round);
            var board = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];
            var usedPositions = new HashSet<Vector2Int>();

            int remainingBudget = template.budget;
            int unitsPlaced = 0;

            // Get valid units for this wave
            var validUnits = GetValidUnits(template.maxCost);

            while (remainingBudget > 0 && unitsPlaced < template.maxUnits && validUnits.Count > 0)
            {
                // Filter to affordable units
                var affordable = validUnits.FindAll(u => u.cost <= remainingBudget);
                if (affordable.Count == 0) break;

                // Pick random unit
                var unitData = affordable[Random.Range(0, affordable.Count)];

                // Determine star level
                int starLevel = DetermineStarLevel(unitData.cost, remainingBudget, template.maxStars);

                // Calculate power cost
                int powerCost = CalculatePowerCost(unitData.cost, starLevel);
                if (powerCost > remainingBudget)
                {
                    starLevel = 1;
                    powerCost = unitData.cost;
                }

                // Find valid position
                var position = FindValidPosition(board, usedPositions, unitData);
                if (!position.HasValue) break;

                // Create and place unit
                var unit = UnitInstance.Create(unitData, starLevel);
                board[position.Value.x, position.Value.y] = unit;
                usedPositions.Add(position.Value);

                remainingBudget -= powerCost;
                unitsPlaced++;
            }

            Debug.Log($"Generated wave {round}: {unitsPlaced} units, {template.budget - remainingBudget} power used");
            return board;
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

        /// <summary>
        /// Generate PvE Intro wave (Round 1): 2 weak critters with loot
        /// </summary>
        private UnitInstance[,] GeneratePvEIntroWave()
        {
            var board = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];

            // Load PvE critter units
            var pveUnits = LoadPvECritters();

            if (pveUnits.Count < 2)
            {
                Debug.LogWarning("Not enough PvE critters! Run Crestforge > Generate PvE Critters first.");
                // Fallback to very weak placeholder units
                return GenerateFallbackIntroWave(board);
            }

            // Create first enemy (Stingray drops Crest Token)
            var stingray = pveUnits.Find(u => u.unitName == "Stingray") ?? pveUnits[0];
            var unit1 = UnitInstance.Create(stingray, 1);
            unit1.lootType = LootType.CrestToken;
            board[1, 1] = unit1;

            // Create second enemy (Cactus drops Item Anvil)
            var cactus = pveUnits.Find(u => u.unitName == "Cactus") ?? pveUnits[pveUnits.Count > 1 ? 1 : 0];
            var unit2 = UnitInstance.Create(cactus, 1);
            unit2.lootType = LootType.ItemAnvil;
            board[3, 1] = unit2;

            Debug.Log($"Generated PvE Intro wave: {stingray.unitName} (Crest Token) and {cactus.unitName} (Item Anvil)");
            return board;
        }

        private List<UnitData> LoadPvECritters()
        {
            var critters = new List<UnitData>();
            var loaded = Resources.LoadAll<UnitData>("ScriptableObjects/PvEUnits");
            if (loaded != null)
            {
                critters.AddRange(loaded);
            }
            return critters;
        }

        private UnitInstance[,] GenerateFallbackIntroWave(UnitInstance[,] board)
        {
            // If no PvE critters exist, use 1-cost units but make them weaker
            var oneCostUnits = GetValidUnits(1);
            if (oneCostUnits.Count == 0) return board;

            var unit1Data = oneCostUnits[Random.Range(0, oneCostUnits.Count)];
            var unit1 = UnitInstance.Create(unit1Data, 1);
            // Reduce stats for fallback
            unit1.currentStats.health = 80;
            unit1.currentStats.attack = 15;
            unit1.currentHealth = 80;
            unit1.lootType = LootType.CrestToken;
            board[1, 1] = unit1;

            var unit2Data = oneCostUnits[Random.Range(0, oneCostUnits.Count)];
            var unit2 = UnitInstance.Create(unit2Data, 1);
            unit2.currentStats.health = 80;
            unit2.currentStats.attack = 15;
            unit2.currentHealth = 80;
            unit2.lootType = LootType.ItemAnvil;
            board[3, 1] = unit2;

            Debug.Log("Using fallback intro wave with weakened units");
            return board;
        }

        /// <summary>
        /// Generate PvE Loot wave: PvE critters with item drops
        /// </summary>
        private UnitInstance[,] GeneratePvELootWave(int round)
        {
            var board = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];

            // Load PvE critter units
            var pveUnits = LoadPvECritters();

            if (pveUnits.Count == 0)
            {
                Debug.LogWarning("No PvE critters found for loot wave!");
                return board;
            }

            // Place 3-4 critters with scaling stats
            int critterCount = 3 + (round / 4);
            bool lootAssigned = false;

            for (int i = 0; i < critterCount && i < 6; i++)
            {
                var critterData = pveUnits[Random.Range(0, pveUnits.Count)];
                int starLevel = 1 + (round / 5); // Scale star level with round
                starLevel = Mathf.Min(starLevel, 2);

                var unit = UnitInstance.Create(critterData, starLevel);

                // First unit gets the item loot
                if (!lootAssigned)
                {
                    unit.lootType = LootType.ItemAnvil;
                    lootAssigned = true;
                }

                // Position critters
                int col = i % GameConstants.Grid.WIDTH;
                int row = i / GameConstants.Grid.WIDTH;
                if (board[col, row] == null)
                {
                    board[col, row] = unit;
                }
            }

            Debug.Log($"Generated PvE Loot wave {round}: {critterCount} critters with item drop");
            return board;
        }

        /// <summary>
        /// Generate PvE Boss wave: Strong PvE critter boss with item drop
        /// </summary>
        private UnitInstance[,] GeneratePvEBossWave(int round)
        {
            var board = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];

            // Load PvE critter units
            var pveUnits = LoadPvECritters();

            if (pveUnits.Count == 0)
            {
                Debug.LogWarning("No PvE critters found for boss wave!");
                return board;
            }

            // Create boss critter (3-star for difficulty)
            var bossData = pveUnits[Random.Range(0, pveUnits.Count)];
            var boss = UnitInstance.Create(bossData, 3);
            boss.lootType = LootType.ItemAnvil;
            board[2, 2] = boss;

            // Add critter minions (2-star)
            var minion1Data = pveUnits[Random.Range(0, pveUnits.Count)];
            var minion1 = UnitInstance.Create(minion1Data, 2);
            board[1, 1] = minion1;

            var minion2Data = pveUnits[Random.Range(0, pveUnits.Count)];
            var minion2 = UnitInstance.Create(minion2Data, 2);
            board[3, 1] = minion2;

            // Add extra minions for difficulty
            var minion3Data = pveUnits[Random.Range(0, pveUnits.Count)];
            var minion3 = UnitInstance.Create(minion3Data, 2);
            board[0, 0] = minion3;

            var minion4Data = pveUnits[Random.Range(0, pveUnits.Count)];
            var minion4 = UnitInstance.Create(minion4Data, 2);
            board[4, 0] = minion4;

            Debug.Log($"Generated PvE Boss wave: {bossData.unitName} (3-star boss with Item Anvil) + 4 minions");
            return board;
        }

        /// <summary>
        /// Get wave template for a round
        /// </summary>
        private WaveTemplate GetWaveTemplate(int round)
        {
            // Scale difficulty with round number
            return round switch
            {
                1 => new WaveTemplate { budget = 2, maxUnits = 2, maxCost = 1, maxStars = 1 },
                2 => new WaveTemplate { budget = 3, maxUnits = 3, maxCost = 1, maxStars = 1 },
                3 => new WaveTemplate { budget = 4, maxUnits = 3, maxCost = 2, maxStars = 1 },
                4 => new WaveTemplate { budget = 5, maxUnits = 4, maxCost = 2, maxStars = 2 },
                5 => new WaveTemplate { budget = 7, maxUnits = 4, maxCost = 2, maxStars = 2 },
                6 => new WaveTemplate { budget = 9, maxUnits = 5, maxCost = 3, maxStars = 2 },
                7 => new WaveTemplate { budget = 11, maxUnits = 5, maxCost = 3, maxStars = 2 },
                8 => new WaveTemplate { budget = 14, maxUnits = 6, maxCost = 3, maxStars = 2 },
                9 => new WaveTemplate { budget = 17, maxUnits = 6, maxCost = 4, maxStars = 2 },
                10 => new WaveTemplate { budget = 20, maxUnits = 6, maxCost = 4, maxStars = 3 },
                11 => new WaveTemplate { budget = 24, maxUnits = 6, maxCost = 4, maxStars = 3 },
                12 => new WaveTemplate { budget = 28, maxUnits = 6, maxCost = 4, maxStars = 3 },
                13 => new WaveTemplate { budget = 32, maxUnits = 6, maxCost = 4, maxStars = 3 },
                14 => new WaveTemplate { budget = 38, maxUnits = 6, maxCost = 4, maxStars = 3 },
                15 => new WaveTemplate { budget = 45, maxUnits = 6, maxCost = 4, maxStars = 4 },
                _ => new WaveTemplate 
                { 
                    budget = 45 + (round - 15) * 5, 
                    maxUnits = 6, 
                    maxCost = 4, 
                    maxStars = 4 
                }
            };
        }

        /// <summary>
        /// Get units valid for this wave's cost limit
        /// </summary>
        private List<UnitData> GetValidUnits(int maxCost)
        {
            var valid = new List<UnitData>();
            foreach (var unit in allUnits)
            {
                if (unit.cost <= maxCost)
                {
                    valid.Add(unit);
                }
            }
            return valid;
        }

        /// <summary>
        /// Determine star level for a unit
        /// </summary>
        private int DetermineStarLevel(int unitCost, int budget, int maxStars)
        {
            int starLevel = 1;
            
            for (int s = Mathf.Min(maxStars, 3); s >= 1; s--)
            {
                int power = CalculatePowerCost(unitCost, s);
                if (power <= budget)
                {
                    // Higher stars are less likely
                    float chance = 1f / Mathf.Pow(2, s - 1);
                    if (Random.value < chance || s == 1)
                    {
                        starLevel = s;
                        break;
                    }
                }
            }

            return starLevel;
        }

        /// <summary>
        /// Calculate power cost of a unit
        /// </summary>
        private int CalculatePowerCost(int unitCost, int starLevel)
        {
            int starMultiplier = (int)Mathf.Pow(2, starLevel - 1);
            return unitCost * starMultiplier;
        }

        /// <summary>
        /// Find a valid position for a unit
        /// </summary>
        private Vector2Int? FindValidPosition(UnitInstance[,] board, HashSet<Vector2Int> used, UnitData unitData)
        {
            // Determine preferred rows based on range
            int[] preferredRows;
            if (unitData.baseStats.range <= 1)
            {
                // Melee prefer front (row 0, 1)
                preferredRows = new int[] { 0, 1, 2, 3 };
            }
            else
            {
                // Ranged prefer back (row 3, 2)
                preferredRows = new int[] { 3, 2, 1, 0 };
            }

            // Shuffle columns
            var cols = new List<int>();
            for (int i = 0; i < GameConstants.Grid.WIDTH; i++)
                cols.Add(i);
            ShuffleList(cols);

            foreach (int row in preferredRows)
            {
                foreach (int col in cols)
                {
                    var pos = new Vector2Int(col, row);
                    if (!used.Contains(pos) && board[col, row] == null)
                    {
                        return pos;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Fisher-Yates shuffle
        /// </summary>
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

        private struct WaveTemplate
        {
            public int budget;
            public int maxUnits;
            public int maxCost;
            public int maxStars;
        }
    }
}
