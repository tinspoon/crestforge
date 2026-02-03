using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Visuals;

namespace Crestforge.Systems
{
    /// <summary>
    /// Data structure for an opponent in PvP mode
    /// </summary>
    [System.Serializable]
    public class OpponentData
    {
        public string id;
        public string name;
        public string personality;
        public int health;
        public int maxHealth;
        public int level;
        public int winStreak;
        public int lossStreak;
        public UnitInstance[,] board;
        public UnitInstance[] bench;
        public List<CrestData> crests;
        public UnitPreference preferredUnitType;
        public bool isEliminated;

        // Track match history for matchmaking
        public int lastFoughtRound;

        // Visual references for multi-board PvP
        [System.NonSerialized]
        public HexBoard3D hexBoard;
        [System.NonSerialized]
        public OpponentBoardVisualizer boardVisualizer;

        public OpponentData()
        {
            board = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];
            bench = new UnitInstance[GameConstants.Player.BENCH_SIZE];
            crests = new List<CrestData>();
        }
    }

    /// <summary>
    /// Unit type preference for AI opponents
    /// </summary>
    public enum UnitPreference
    {
        Warrior,    // Favors melee/tank units
        Mage,       // Favors spell caster units
        Ranger      // Favors ranged/attack speed units
    }

    /// <summary>
    /// Manages mock opponents and their boards in PvP mode
    /// </summary>
    public class OpponentManager : MonoBehaviour
    {
        public static OpponentManager Instance { get; private set; }

        [Header("State")]
        public List<OpponentData> opponents = new List<OpponentData>();
        public OpponentData currentOpponent;
        private int rotationIndex = 0;
        private int lastLeveledRound = 0; // Track to prevent double-leveling

        // Events
        public System.Action<OpponentData> OnOpponentEliminated;
        public System.Action OnAllOpponentsEliminated;

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
        /// Initialize opponents for a new PvP game
        /// </summary>
        /// <param name="count">Number of AI opponents (default 3 for PvP, 2 for multiplayer with 2 humans)</param>
        public void InitializeOpponents(int count = 3)
        {
            rotationIndex = 0;
            lastLeveledRound = 0;

            // Check if opponents already exist with visualizers (set up by Game3DSetup)
            bool hasExistingVisualizers = opponents.Count > 0 && opponents[0].boardVisualizer != null;

            if (hasExistingVisualizers)
            {
                // Opponents already set up with visualizers - just reset their state
                // Only reset up to the requested count
                int resetCount = Mathf.Min(opponents.Count, count);

                for (int i = 0; i < resetCount; i++)
                {
                    var opponent = opponents[i];
                    // Reset game state but keep visualizer references
                    opponent.health = 20;
                    opponent.maxHealth = 20;
                    opponent.level = 1;
                    opponent.winStreak = 0;
                    opponent.lossStreak = 0;
                    opponent.isEliminated = false;
                    opponent.lastFoughtRound = 0;

                    // Regenerate board
                    GenerateOpponentBoard(opponent);

                    // Refresh visualizer to show new board state
                    if (opponent.boardVisualizer != null)
                    {
                        opponent.boardVisualizer.Refresh();
                        opponent.boardVisualizer.UpdateLabel();
                    }
                }

                // Hide extra opponents if we have more than needed
                for (int i = count; i < opponents.Count; i++)
                {
                    opponents[i].isEliminated = true;
                    if (opponents[i].hexBoard != null)
                    {
                        opponents[i].hexBoard.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                // Fresh initialization
                opponents.Clear();

                // Create opponents based on count
                if (count >= 1)
                    opponents.Add(CreateOpponent("opponent_1", "Sir Bumble", "Bumbling knight", UnitPreference.Warrior));
                if (count >= 2)
                    opponents.Add(CreateOpponent("opponent_2", "Mystic Mira", "Mysterious mage", UnitPreference.Mage));
                if (count >= 3)
                    opponents.Add(CreateOpponent("opponent_3", "Rogue Rex", "Sneaky thief", UnitPreference.Ranger));

                // Generate initial boards for each opponent
                foreach (var opponent in opponents)
                {
                    GenerateOpponentBoard(opponent);
                }

            }
        }

        private OpponentData CreateOpponent(string id, string name, string personality, UnitPreference preference)
        {
            int startingHealth = 20; // Same as player in PvP mode

            return new OpponentData
            {
                id = id,
                name = name,
                personality = personality,
                health = startingHealth,
                maxHealth = startingHealth,
                level = 1,
                winStreak = 0,
                lossStreak = 0,
                preferredUnitType = preference,
                isEliminated = false,
                lastFoughtRound = 0
            };
        }

        /// <summary>
        /// Get the opponent for the current round
        /// </summary>
        public OpponentData GetOpponentForRound(int round)
        {
            if (opponents.Count == 0) return null;

            var activeOpponents = GetActiveOpponents();
            if (activeOpponents.Count == 0)
            {
                OnAllOpponentsEliminated?.Invoke();
                return null;
            }

            // Special handling for final rounds (13-14)
            if (round >= 13)
            {
                return GetLowestHealthOpponent(activeOpponents);
            }

            // Normal rotation - skip eliminated opponents
            int attempts = 0;
            while (attempts < opponents.Count)
            {
                var opponent = opponents[rotationIndex % opponents.Count];
                rotationIndex++;
                attempts++;

                if (!opponent.isEliminated)
                {
                    currentOpponent = opponent;
                    return opponent;
                }
            }

            return activeOpponents.Count > 0 ? activeOpponents[0] : null;
        }

        /// <summary>
        /// Get opponent with lowest health
        /// </summary>
        private OpponentData GetLowestHealthOpponent(List<OpponentData> activeOpponents)
        {
            if (activeOpponents.Count == 0) return null;

            OpponentData lowest = activeOpponents[0];
            foreach (var opponent in activeOpponents)
            {
                if (opponent.health < lowest.health)
                {
                    lowest = opponent;
                }
            }
            currentOpponent = lowest;
            return lowest;
        }

        /// <summary>
        /// Get list of opponents that are still in the game
        /// </summary>
        public List<OpponentData> GetActiveOpponents()
        {
            var active = new List<OpponentData>();
            foreach (var opponent in opponents)
            {
                if (!opponent.isEliminated)
                {
                    active.Add(opponent);
                }
            }
            return active;
        }

        /// <summary>
        /// Generate or update an opponent's board based on their level
        /// </summary>
        public void GenerateOpponentBoard(OpponentData opponent)
        {
            if (opponent == null) return;

            var state = GameState.Instance;
            if (state == null || state.allUnits == null) return;

            // Clear existing board
            opponent.board = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];

            // Get units that match opponent's preference
            var preferredUnits = GetPreferredUnits(opponent.preferredUnitType, state.allUnits);
            if (preferredUnits.Count == 0)
            {
                preferredUnits = new List<UnitData>(state.allUnits);
            }

            // Filter to units at or below what's available at opponent's level
            int maxCost = GetMaxCostForLevel(opponent.level);
            var affordableUnits = preferredUnits.FindAll(u => u != null && u.cost <= maxCost);
            if (affordableUnits.Count == 0)
            {
                affordableUnits = preferredUnits;
            }

            // Place units based on level (level = number of units)
            int unitsToPlace = Mathf.Min(opponent.level, GameConstants.Grid.WIDTH * GameConstants.Grid.HEIGHT);
            var usedPositions = new HashSet<Vector2Int>();

            for (int i = 0; i < unitsToPlace && affordableUnits.Count > 0; i++)
            {
                // Pick a random unit from available pool
                var unitData = affordableUnits[Random.Range(0, affordableUnits.Count)];

                // Check shared pool if available
                if (state.unitPool != null && state.unitPool.GetAvailable(unitData.unitId) <= 0)
                {
                    // Try to find another unit
                    var available = affordableUnits.FindAll(u => state.unitPool.GetAvailable(u.unitId) > 0);
                    if (available.Count > 0)
                    {
                        unitData = available[Random.Range(0, available.Count)];
                    }
                    else
                    {
                        continue; // Skip if no units available
                    }
                }

                // Create unit instance
                var unit = UnitInstance.Create(unitData, 1);

                // Find position based on unit type
                var position = FindPositionForUnit(opponent.board, usedPositions, unitData);
                if (position.HasValue)
                {
                    opponent.board[position.Value.x, position.Value.y] = unit;
                    unit.boardPosition = position.Value;
                    usedPositions.Add(position.Value);

                    // Take from shared pool
                    if (state.unitPool != null)
                    {
                        // Note: We don't actually modify the pool here since opponents
                        // are simulated - but in a full implementation we would
                    }
                }
            }

        }

        /// <summary>
        /// Get units that match an opponent's preferred type
        /// </summary>
        private List<UnitData> GetPreferredUnits(UnitPreference preference, UnitData[] allUnits)
        {
            var preferred = new List<UnitData>();

            foreach (var unit in allUnits)
            {
                if (unit == null) continue;

                bool matches = false;
                switch (preference)
                {
                    case UnitPreference.Warrior:
                        // Prefer melee units with high health
                        matches = unit.baseStats.range <= 1;
                        break;
                    case UnitPreference.Mage:
                        // Prefer ranged units or units with magic-like abilities
                        matches = unit.baseStats.range > 1 && unit.cost >= 2;
                        break;
                    case UnitPreference.Ranger:
                        // Prefer ranged units with high attack speed
                        matches = unit.baseStats.range > 1;
                        break;
                }

                if (matches)
                {
                    preferred.Add(unit);
                }
            }

            // If no matches found, return 1-cost units as fallback
            if (preferred.Count == 0)
            {
                foreach (var unit in allUnits)
                {
                    if (unit != null && unit.cost == 1)
                    {
                        preferred.Add(unit);
                    }
                }
            }

            return preferred;
        }

        /// <summary>
        /// Get max unit cost available at a given player level
        /// </summary>
        private int GetMaxCostForLevel(int level)
        {
            return level switch
            {
                1 => 1,
                2 => 2,
                3 => 2,
                4 => 3,
                5 => 3,
                _ => 4
            };
        }

        /// <summary>
        /// Find a good position for a unit on the board
        /// </summary>
        private Vector2Int? FindPositionForUnit(UnitInstance[,] board, HashSet<Vector2Int> used, UnitData unitData)
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

            // Shuffle columns for variety
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
        /// Apply damage to an opponent after they lose combat
        /// </summary>
        public void ApplyDamageToOpponent(OpponentData opponent, int damage)
        {
            if (opponent == null || opponent.isEliminated) return;

            opponent.health -= damage;
            opponent.lossStreak++;
            opponent.winStreak = 0;

            if (opponent.health <= 0)
            {
                EliminateOpponent(opponent);
            }
        }

        /// <summary>
        /// Record a win for an opponent
        /// </summary>
        public void RecordOpponentWin(OpponentData opponent)
        {
            if (opponent == null || opponent.isEliminated) return;

            opponent.winStreak++;
            opponent.lossStreak = 0;
        }

        /// <summary>
        /// Eliminate an opponent from the game
        /// </summary>
        private void EliminateOpponent(OpponentData opponent)
        {
            opponent.isEliminated = true;
            opponent.health = 0;

            OnOpponentEliminated?.Invoke(opponent);

            // Check if all opponents are eliminated
            if (GetActiveOpponents().Count == 0)
            {
                OnAllOpponentsEliminated?.Invoke();
            }
        }

        /// <summary>
        /// Level up all active opponents (called each round)
        /// </summary>
        public void LevelUpOpponents()
        {
            int currentRound = GameState.Instance?.round?.currentRound ?? 0;

            // Prevent double-leveling in the same round
            if (currentRound > 0 && currentRound == lastLeveledRound)
            {
                return;
            }
            lastLeveledRound = currentRound;

            foreach (var opponent in opponents)
            {
                if (!opponent.isEliminated && opponent.level < GameConstants.Leveling.MAX_LEVEL)
                {
                    opponent.level++;
                    GenerateOpponentBoard(opponent);

                    // Refresh visualizer to show new board state
                    if (opponent.boardVisualizer != null)
                    {
                        opponent.boardVisualizer.Refresh();
                        opponent.boardVisualizer.UpdateLabel();
                    }
                }
            }
        }

        /// <summary>
        /// Get the opponent board for combat.
        /// Uses the existing board state (same as shown during planning phase).
        /// </summary>
        public UnitInstance[,] GetOpponentBoardForCombat(OpponentData opponent)
        {
            if (opponent == null) return null;

            // Only generate if board doesn't exist yet (shouldn't happen normally)
            if (opponent.board == null)
            {
                Debug.LogWarning($"[OpponentManager] Board was null for {opponent.name}, generating on demand");
                GenerateOpponentBoard(opponent);
            }

            return opponent.board;
        }

        /// <summary>
        /// Calculate damage dealt by remaining units
        /// </summary>
        public int CalculateDamage(UnitInstance[,] board)
        {
            int damage = GameConstants.Player.LOSS_DAMAGE_BASE;
            int survivingUnits = 0;

            if (board != null)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                    {
                        var unit = board[x, y];
                        if (unit != null && unit.IsAlive)
                        {
                            survivingUnits++;
                        }
                    }
                }
            }

            damage += survivingUnits * GameConstants.Player.LOSS_DAMAGE_PER_UNIT;
            return damage;
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
    }
}
