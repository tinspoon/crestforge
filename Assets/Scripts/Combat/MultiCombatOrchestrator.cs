using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Systems;
using Crestforge.Visuals;

namespace Crestforge.Combat
{
    /// <summary>
    /// Represents a single matchup between two players.
    /// Combat happens on the home player's board.
    /// </summary>
    public class CombatMatchup
    {
        public string homePlayerId;
        public string awayPlayerId;
        public HexBoard3D homeBoard;
        public OpponentBoardVisualizer homeVisualizer;
        public CombatSimulation simulation;
        public bool isPlayerInvolved;
        public bool playerIsHome;
    }

    /// <summary>
    /// Orchestrates combat matchups in multi-board PvP mode.
    /// Each matchup has one "home" board where combat actually happens.
    /// The "away" player's units travel to the home board for the fight.
    /// </summary>
    public class MultiCombatOrchestrator : MonoBehaviour
    {
        public static MultiCombatOrchestrator Instance { get; private set; }

        [Header("State")]
        public bool isMultiCombatActive;
        public List<CombatMatchup> activeMatchups = new List<CombatMatchup>();

        // Events
        public System.Action<CombatMatchup, CombatResult> OnMatchupEnded;
        public System.Action OnAllCombatsEnded;

        private int completedMatchups;
        private int totalMatchups;
        private bool subscribedToCombatManager;

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

        private void Start()
        {
            // Subscribe to player combat end
            SubscribeToCombatManager();
        }

        private void SubscribeToCombatManager()
        {
            if (subscribedToCombatManager) return;

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatEnd += HandlePlayerCombatEnd;
                subscribedToCombatManager = true;
            }
            else
            {
                Debug.LogWarning("[MultiCombat] CombatManager not ready, will retry subscription");
                StartCoroutine(RetrySubscription());
            }
        }

        private IEnumerator RetrySubscription()
        {
            yield return null;
            yield return null;
            if (!subscribedToCombatManager && CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatEnd += HandlePlayerCombatEnd;
                subscribedToCombatManager = true;
            }
        }

        private void OnDestroy()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatEnd -= HandlePlayerCombatEnd;
            }
        }

        private void Update()
        {
            if (!isMultiCombatActive) return;

            // Update all non-player combat simulations
            float deltaTime = Time.deltaTime;
            foreach (var matchup in activeMatchups)
            {
                if (matchup.simulation != null && matchup.simulation.isInCombat)
                {
                    matchup.simulation.Update(deltaTime);
                }
            }
        }

        /// <summary>
        /// Start multi-board combat for all players.
        /// Creates matchups where combat happens on one board per pair.
        /// </summary>
        public void StartMultiCombat()
        {
            if (GameState.Instance.currentGameMode != GameMode.PvP)
            {
                return;
            }
            if (OpponentManager.Instance == null)
            {
                Debug.LogWarning("[MultiCombat] OpponentManager.Instance is null");
                return;
            }

            var opponents = OpponentManager.Instance.GetActiveOpponents();

            if (opponents.Count == 0) return;

            isMultiCombatActive = true;
            completedMatchups = 0;
            activeMatchups.Clear();

            // Get the player's current opponent (already determined by RoundManager)
            var playerOpponent = OpponentManager.Instance.currentOpponent;

            // Player's opponent is "away" - their board shows indicator, fight is on player's board
            if (playerOpponent != null)
            {
                if (playerOpponent.boardVisualizer != null)
                {
                    var playerBoard = HexBoard3D.Instance;
                    playerOpponent.boardVisualizer.ShowUnitsAway(playerBoard, "Player");
                }
            }

            // Create matchups for AI vs AI (opponents not fighting the player)
            var aiOpponents = new List<OpponentData>();
            foreach (var opp in opponents)
            {
                if (opp != playerOpponent)
                {
                    aiOpponents.Add(opp);
                }
            }

            // Pair up remaining AI opponents
            for (int i = 0; i < aiOpponents.Count - 1; i += 2)
            {
                var homeOpp = aiOpponents[i];
                var awayOpp = aiOpponents[i + 1];
                CreateAIMatchup(homeOpp, awayOpp);
            }

            totalMatchups = activeMatchups.Count + 1; // +1 for player's fight
        }

        /// <summary>
        /// Start multi-board PvE combat - each player fights their own PvE wave on their own board.
        /// </summary>
        public void StartMultiCombatPvE(int round)
        {
            if (GameState.Instance.currentGameMode != GameMode.PvP)
            {
                return;
            }
            if (OpponentManager.Instance == null)
            {
                Debug.LogWarning("[MultiCombat] OpponentManager.Instance is null");
                return;
            }

            var opponents = OpponentManager.Instance.GetActiveOpponents();
            if (opponents.Count == 0) return;

            isMultiCombatActive = true;
            completedMatchups = 0;
            activeMatchups.Clear();

            // Each opponent fights their own PvE wave on their own board
            foreach (var opponent in opponents)
            {
                if (opponent.isEliminated) continue;
                CreatePvEMatchup(opponent, round);
            }

            totalMatchups = activeMatchups.Count + 1; // +1 for player's fight
        }

        /// <summary>
        /// Create a PvE matchup for an AI opponent fighting a PvE wave on their own board.
        /// </summary>
        private void CreatePvEMatchup(OpponentData opponent, int round)
        {
            if (opponent.boardVisualizer == null)
            {
                Debug.LogWarning($"[MultiCombat] {opponent.name} has no boardVisualizer for PvE!");
                return;
            }

            // Generate opponent's board if needed
            if (opponent.board == null)
            {
                OpponentManager.Instance.GenerateOpponentBoard(opponent);
            }

            // Generate PvE wave for this opponent
            var enemyWave = EnemyWaveGenerator.Instance.GenerateWave(round);

            var matchup = new CombatMatchup
            {
                homePlayerId = opponent.id,
                awayPlayerId = "pve_wave",
                homeBoard = opponent.hexBoard,
                homeVisualizer = opponent.boardVisualizer,
                isPlayerInvolved = false,
                playerIsHome = false
            };

            // Create simulation for this PvE matchup
            var simulation = new CombatSimulation(opponent.id, $"{opponent.name} vs PvE");
            simulation.OnCombatEnd += (sim, result) => HandlePvEMatchupEnd(matchup, opponent, result);

            // Start combat: opponent units on bottom, PvE wave on top
            simulation.StartCombat(
                opponent.board,
                enemyWave,
                opponent.crests ?? new List<CrestData>(),
                new List<CrestData>() // PvE has no crests
            );

            matchup.simulation = simulation;
            activeMatchups.Add(matchup);

            // Show combat on opponent's board
            opponent.boardVisualizer.ConnectToSimulation(simulation, false);
        }

        /// <summary>
        /// Handle end of a PvE matchup for an AI opponent.
        /// </summary>
        private void HandlePvEMatchupEnd(CombatMatchup matchup, OpponentData opponent, CombatResult result)
        {
            completedMatchups++;

            if (opponent != null)
            {
                if (!result.victory)
                {
                    // Opponent lost to PvE - take damage based on surviving enemies
                    int damage = 2 + result.enemySurvivors;
                    OpponentManager.Instance.ApplyDamageToOpponent(opponent, damage);
                }

                // Disconnect visualizer
                if (opponent.boardVisualizer != null)
                {
                    opponent.boardVisualizer.DisconnectFromSimulation();
                }
            }

            OnMatchupEnded?.Invoke(matchup, result);

            CheckAllCombatsEnded();
        }

        /// <summary>
        /// Create an AI vs AI matchup. Combat happens on homeOpp's board.
        /// </summary>
        private void CreateAIMatchup(OpponentData homeOpp, OpponentData awayOpp)
        {
            // Generate boards if needed
            if (homeOpp.board == null)
            {
                OpponentManager.Instance.GenerateOpponentBoard(homeOpp);
            }
            if (awayOpp.board == null)
            {
                OpponentManager.Instance.GenerateOpponentBoard(awayOpp);
            }

            var matchup = new CombatMatchup
            {
                homePlayerId = homeOpp.id,
                awayPlayerId = awayOpp.id,
                homeBoard = homeOpp.hexBoard,
                homeVisualizer = homeOpp.boardVisualizer,
                isPlayerInvolved = false,
                playerIsHome = false
            };

            // Create simulation for this matchup
            var simulation = new CombatSimulation(homeOpp.id, $"{homeOpp.name} vs {awayOpp.name}");
            simulation.OnCombatEnd += (sim, result) => HandleMatchupEnd(matchup, result);

            // Start combat: home units on bottom, away units on top (enemy side)
            simulation.StartCombat(
                homeOpp.board,
                awayOpp.board,
                homeOpp.crests ?? new List<CrestData>(),
                awayOpp.crests ?? new List<CrestData>()
            );

            matchup.simulation = simulation;
            activeMatchups.Add(matchup);

            // Home board shows the actual combat
            if (homeOpp.boardVisualizer != null)
            {
                homeOpp.boardVisualizer.ConnectToSimulation(simulation, false);
            }
            else
            {
                Debug.LogWarning($"[MultiCombat] {homeOpp.name} has no boardVisualizer - combat won't be visible!");
            }

            // Away board shows "fighting on home board" indicator
            if (awayOpp.boardVisualizer != null)
            {
                awayOpp.boardVisualizer.ShowUnitsAway(homeOpp.hexBoard, homeOpp.name);
            }
            else
            {
                Debug.LogWarning($"[MultiCombat] {awayOpp.name} has no boardVisualizer!");
            }
        }

        private int CountBoardUnits(UnitInstance[,] board)
        {
            if (board == null) return 0;
            int count = 0;
            for (int x = 0; x < board.GetLength(0); x++)
            {
                for (int y = 0; y < board.GetLength(1); y++)
                {
                    if (board[x, y] != null) count++;
                }
            }
            return count;
        }

        private void HandlePlayerCombatEnd(CombatResult result)
        {
            if (!isMultiCombatActive) return;

            // Disconnect player's opponent from CombatManager visualization
            var playerOpponent = OpponentManager.Instance?.currentOpponent;
            if (playerOpponent != null && playerOpponent.boardVisualizer != null)
            {
                playerOpponent.boardVisualizer.DisconnectFromCombatManager();
            }

            completedMatchups++;

            CheckAllCombatsEnded();
        }

        private void HandleMatchupEnd(CombatMatchup matchup, CombatResult result)
        {
            completedMatchups++;

            // Find the opponents involved
            var homeOpp = OpponentManager.Instance.opponents.Find(o => o.id == matchup.homePlayerId);
            var awayOpp = OpponentManager.Instance.opponents.Find(o => o.id == matchup.awayPlayerId);

            if (homeOpp != null && awayOpp != null)
            {
                // Home won means away takes damage, and vice versa
                if (result.victory)
                {
                    // Home won - away takes damage
                    int damage = 2 + result.playerSurvivors; // playerSurvivors = home's surviving units
                    OpponentManager.Instance.ApplyDamageToOpponent(awayOpp, damage);
                    OpponentManager.Instance.RecordOpponentWin(homeOpp);
                }
                else
                {
                    // Away won - home takes damage
                    int damage = 2 + result.enemySurvivors; // enemySurvivors = away's surviving units
                    OpponentManager.Instance.ApplyDamageToOpponent(homeOpp, damage);
                    OpponentManager.Instance.RecordOpponentWin(awayOpp);
                }
            }

            // Disconnect visualizers
            if (homeOpp?.boardVisualizer != null)
            {
                homeOpp.boardVisualizer.DisconnectFromSimulation();
            }
            if (awayOpp?.boardVisualizer != null)
            {
                awayOpp.boardVisualizer.ClearUnitsAway();
            }

            OnMatchupEnded?.Invoke(matchup, result);

            CheckAllCombatsEnded();
        }

        private void CheckAllCombatsEnded()
        {
            if (completedMatchups >= totalMatchups)
            {
                isMultiCombatActive = false;
                OnAllCombatsEnded?.Invoke();
            }
        }

        /// <summary>
        /// Force end all combats (e.g., when exiting to menu)
        /// </summary>
        public void ForceEndAllCombats()
        {
            foreach (var matchup in activeMatchups)
            {
                if (matchup.simulation != null)
                {
                    matchup.simulation.ForceEndCombat();
                }
            }
        }

        /// <summary>
        /// Get the matchup involving a specific opponent
        /// </summary>
        public CombatMatchup GetMatchupForOpponent(string opponentId)
        {
            return activeMatchups.Find(m =>
                m.homePlayerId == opponentId || m.awayPlayerId == opponentId);
        }

        /// <summary>
        /// Check if multi-board PvP mode is enabled
        /// </summary>
        public bool IsMultiBoardEnabled()
        {
            var game3DSetup = Game3DSetup.Instance;
            return game3DSetup != null && game3DSetup.enableMultiBoard;
        }

        /// <summary>
        /// Get the board where a specific opponent's fight is happening
        /// </summary>
        public HexBoard3D GetFightBoard(string opponentId)
        {
            var matchup = GetMatchupForOpponent(opponentId);
            return matchup?.homeBoard;
        }
    }
}
