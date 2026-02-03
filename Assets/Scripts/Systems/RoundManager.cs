using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Combat;
using Crestforge.Visuals;
using Crestforge.Networking;

namespace Crestforge.Systems
{
    public class RoundManager : MonoBehaviour
    {
        public static RoundManager Instance { get; private set; }

        [Header("Settings")]
        public float planningPhaseDuration = 30f;
        public float pveIntroPlanningDuration = 5f;  // Shorter planning for PvE intro round
        public float resultsPhaseDuration = 3f;

        [Header("State")]
        public bool isPaused;
        private Coroutine phaseCoroutine;

        // Store original positions before combat
        private Dictionary<string, Vector2Int> savedUnitPositions = new Dictionary<string, Vector2Int>();

        // Multiplayer state
        private UnitInstance[,] multiplayerOpponentBoard;
        private int multiplayerOpponentIndex = 0; // 0 = human opponent, 1+ = AI opponents

        // Events
        public System.Action<GamePhase> OnPhaseChanged;
        public System.Action<int> OnRoundStarted;
        public System.Action<CombatResult> OnRoundEnded;
        public System.Action OnGameOver;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Enable running in background for multiplayer combat synchronization
                Application.runInBackground = true;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatEnd += HandleCombatEnd;
                CombatManager.Instance.OnUnitDied += HandleUnitDied;
            }

            // Subscribe to network events for multiplayer
            SubscribeToNetworkEvents();
        }

        private void SubscribeToNetworkEvents()
        {
            var nm = NetworkManager.Instance;
            if (nm != null)
            {
                nm.OnCombatStart += HandleNetworkCombatStart;
                nm.OnRoundStart += HandleNetworkRoundStart;
            }
            else
            {
                // NetworkManager might not exist yet, try again later
                StartCoroutine(RetryNetworkSubscription());
            }
        }

        private IEnumerator RetryNetworkSubscription()
        {
            yield return new WaitForSeconds(0.5f);
            var nm = NetworkManager.Instance;
            if (nm != null)
            {
                nm.OnCombatStart += HandleNetworkCombatStart;
                nm.OnRoundStart += HandleNetworkRoundStart;
            }
        }

        private void OnDestroy()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatEnd -= HandleCombatEnd;
                CombatManager.Instance.OnUnitDied -= HandleUnitDied;
            }

            var nm = NetworkManager.Instance;
            if (nm != null)
            {
                nm.OnCombatStart -= HandleNetworkCombatStart;
                nm.OnRoundStart -= HandleNetworkRoundStart;
            }
        }

        /// <summary>
        /// Handle combat start from network (server-authoritative multiplayer mode)
        /// </summary>
        private void HandleNetworkCombatStart(List<ServerMatchup> matchups, Dictionary<string, ServerBoardState> boards)
        {
            // In server-authoritative mode, ServerGameState handles the state
            // RoundManager just needs to update phase for legacy code compatibility
            if (GameState.Instance?.currentGameMode != GameMode.Multiplayer)
            {
                return;
            }

            var nm = NetworkManager.Instance;
            if (nm == null)
            {
                Debug.LogError("[RoundManager] NetworkManager is null in HandleNetworkCombatStart");
                return;
            }

            // For server-authoritative mode, the server handles combat simulation
            // CombatVisualizer will receive and play back combat events
            // We just update local state to reflect combat phase
            
            // Update game state phase if needed for UI purposes
            if (GameState.Instance != null && GameState.Instance.round != null)
            {
                GameState.Instance.round.phase = GamePhase.Combat;
            }
        }

        /// <summary>
        /// Handle round start from network (multiplayer mode)
        /// </summary>
        private void HandleNetworkRoundStart(int round)
        {
            if (GameState.Instance?.currentGameMode != GameMode.Multiplayer) return;

            multiplayerOpponentBoard = null; // Clear opponent board for new round
        }

        private void HandleUnitDied(CombatUnit unit)
        {
            // Loot orb spawning is now handled by BoardManager3D
            // This handler is kept for any future logic needs
        }

        public void StartGame(GameMode mode = GameMode.PvEWave)
        {
            // Show game visuals now that game is starting
            if (Crestforge.Visuals.Game3DSetup.Instance != null)
            {
                Crestforge.Visuals.Game3DSetup.Instance.ShowGameVisuals();
            }

            GameState.Instance.InitializeGame(mode);

            // Initialize opponents for PvP mode (not for Multiplayer - that uses real players)
            if (mode == GameMode.PvP && OpponentManager.Instance != null)
            {
                OpponentManager.Instance.InitializeOpponents();
            }

            // For Multiplayer mode, keep multi-board layout but initialize 2 AI opponents
            // (2 human players + 2 AI opponents = 4 total)
            if (mode == GameMode.Multiplayer)
            {
                multiplayerOpponentIndex = 0; // Start with human opponent
                multiplayerOpponentBoard = null;
                
                if (OpponentManager.Instance != null)
                {
                    OpponentManager.Instance.InitializeOpponents(2); // Only 2 AI opponents for multiplayer
                }

                // Re-subscribe to network events in case we missed them
                SubscribeToNetworkEvents();
            }

            // Go straight to planning - loot will drop after PvE rounds
            StartPlanningPhase();
        }

        public void StartPlanningPhase()
        {
            var state = GameState.Instance;

            // Restore unit positions from previous combat (if any)
            if (savedUnitPositions.Count > 0)
            {
                RestoreUnitPositions();
            }

            int income = state.CalculateIncome();
            state.player.gold += income;

            if (!state.shop.isLocked)
            {
                state.shop.ReturnToPool(state.unitPool);
                state.shop.Refresh(state.unitPool, state.player.level);
            }

            SetPhase(GamePhase.Planning);
            OnRoundStarted?.Invoke(state.round.currentRound);

            if (phaseCoroutine != null) StopCoroutine(phaseCoroutine);
            phaseCoroutine = StartCoroutine(PlanningPhaseTimer());
        }

        private IEnumerator PlanningPhaseTimer()
        {
            // Use shorter timer for special rounds
            var roundType = GetCurrentRoundType();
            float timer;

            if (roundType == RoundType.PvEIntro)
            {
                timer = pveIntroPlanningDuration;
            }
            else if (roundType == RoundType.MadMerchant || roundType == RoundType.MajorCrest)
            {
                // Short planning before special non-combat rounds
                timer = 10f;
            }
            else
            {
                timer = planningPhaseDuration;
            }

            while (timer > 0 && GameState.Instance.round.phase == GamePhase.Planning)
            {
                if (!isPaused)
                {
                    timer -= Time.deltaTime;
                    GameState.Instance.round.phaseTimer = timer;
                }
                yield return null;
            }

            if (GameState.Instance.round.phase == GamePhase.Planning)
            {
                // In multiplayer mode, handle differently based on opponent type
                if (GameState.Instance.currentGameMode == GameMode.Multiplayer)
                {
                    var nm = NetworkManager.Instance;

                    // Always send board update for scouting
                    if (nm != null && nm.IsInGame)
                    {
                        nm.SendBoardUpdate();
                    }

                    // Check if this is a PvP round
                    bool isPvPRound = IsPvPRound();

                    if (isPvPRound && multiplayerOpponentIndex == 0)
                    {
                        // Fighting human opponent - wait for server sync
                        if (nm != null && nm.IsInGame)
                        {
                            nm.EndPlanning();
                                                        // Don't start combat directly - wait for HandleNetworkCombatStart
                        }
                        else
                        {
                            Debug.LogWarning("[RoundManager] NetworkManager not available, starting combat directly");
                            StartCombatPhase();
                        }
                    }
                    else if (isPvPRound)
                    {
                        // Fighting AI opponent - start combat directly
                        StartCombatPhase();
                    }
                    else
                    {
                        // PvE round - start combat directly
                        StartCombatPhase();
                    }
                }
                else
                {
                    // Single-player modes: start combat directly
                    StartCombatPhase();
                }
            }
        }

        public void StartCombatPhase()
        {
            if (phaseCoroutine != null) StopCoroutine(phaseCoroutine);

            // Check for special non-combat rounds
            var roundType = GetCurrentRoundType();

            if (roundType == RoundType.MadMerchant)
            {
                StartMadMerchantRound();
                return;
            }

            if (roundType == RoundType.MajorCrest)
            {
                StartMajorCrestRound();
                return;
            }

            // Cancel any in-progress bench drags before combat starts
            Crestforge.Visuals.BoardManager3D.Instance?.CancelBenchDrag();

            var state = GameState.Instance;

            // Save unit positions before combat
            SaveUnitPositions();

            SetPhase(GamePhase.Combat);

            UnitInstance[,] enemyBoard;
            List<CrestData> enemyCrests = new List<CrestData>();

            // Check if this is a PvP round in PvP mode
            bool isPvPRoundInPvP = IsPvPRound() && state.currentGameMode == GameMode.PvP;
            bool isPvPRoundInMultiplayer = IsPvPRound() && state.currentGameMode == GameMode.Multiplayer;

            // Check for Multiplayer mode
            if (state.currentGameMode == GameMode.Multiplayer && isPvPRoundInMultiplayer)
            {
                if (multiplayerOpponentIndex == 0)
                {
                    // Fighting human opponent - use board from network
                    var nm = Crestforge.Networking.NetworkManager.Instance;
                    if (nm != null && multiplayerOpponentBoard != null)
                    {
                        enemyBoard = multiplayerOpponentBoard;
                    }
                    else
                    {
                        // No opponent board received yet - use empty board
                        enemyBoard = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];
                        Debug.LogWarning("Multiplayer: No opponent board received from human!");
                    }
                }
                else
                {
                    // Fighting AI opponent - use OpponentManager
                    if (OpponentManager.Instance != null)
                    {
                        int aiIndex = multiplayerOpponentIndex - 1; // Convert to 0-based AI index
                        var opponents = OpponentManager.Instance.GetActiveOpponents();
                        if (aiIndex < opponents.Count)
                        {
                            var aiOpponent = opponents[aiIndex];
                            enemyBoard = OpponentManager.Instance.GetOpponentBoardForCombat(aiOpponent);
                            enemyCrests = aiOpponent.crests ?? new List<CrestData>();
                            OpponentManager.Instance.currentOpponent = aiOpponent;
                        }
                        else
                        {
                            enemyBoard = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];
                            Debug.LogWarning("Multiplayer: AI opponent index out of range!");
                        }
                    }
                    else
                    {
                        enemyBoard = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];
                    }
                }

                // Rotate to next opponent for next PvP round
                int totalOpponents = 1 + (OpponentManager.Instance?.GetActiveOpponents().Count ?? 0);
                multiplayerOpponentIndex = (multiplayerOpponentIndex + 1) % totalOpponents;
            }
            else if (isPvPRoundInPvP && OpponentManager.Instance != null)
            {
                // Get opponent for this round
                var opponent = OpponentManager.Instance.GetOpponentForRound(state.round.currentRound);
                if (opponent != null)
                {
                    enemyBoard = OpponentManager.Instance.GetOpponentBoardForCombat(opponent);
                    enemyCrests = opponent.crests ?? new List<CrestData>();
                }
                else
                {
                    // All opponents eliminated - use empty board
                    enemyBoard = new UnitInstance[GameConstants.Grid.WIDTH, GameConstants.Grid.HEIGHT];
                }
            }
            else
            {
                // PvE round or not in PvP mode - use wave generator
                enemyBoard = EnemyWaveGenerator.Instance.GenerateWave(state.round.currentRound);
            }

            state.enemyBoard = enemyBoard;

            var playerCrests = new List<CrestData>();
            if (state.minorCrests != null) playerCrests.AddRange(state.minorCrests);
            if (state.majorCrests != null) playerCrests.AddRange(state.majorCrests);

            CombatManager.Instance.StartCombat(
                state.playerBoard,
                enemyBoard,
                playerCrests,
                enemyCrests
            );

            // Start multi-board combat if enabled (for both PvP and PvE rounds in PvP mode)
            if (state.currentGameMode == GameMode.PvP && Game3DSetup.Instance != null && Game3DSetup.Instance.enableMultiBoard)
            {
                if (MultiCombatOrchestrator.Instance != null)
                {
                    if (isPvPRoundInPvP)
                    {
                        MultiCombatOrchestrator.Instance.StartMultiCombat();
                    }
                    else
                    {
                        // PvE round - each opponent fights their own PvE wave
                        MultiCombatOrchestrator.Instance.StartMultiCombatPvE(state.round.currentRound);
                    }
                }
            }
        }

        /// <summary>
        /// Check if current round is a PvP round (based on round type)
        /// </summary>
        public bool IsPvPRound()
        {
            return GetCurrentRoundType() == RoundType.PvP;
        }

        /// <summary>
        /// Start the Mad Merchant carousel round
        /// </summary>
        private void StartMadMerchantRound()
        {
            // Hide shop during merchant round
            if (Crestforge.UI.GameUI.Instance != null)
            {
                // The shop will be hidden by the merchant UI overlay
            }

            // Start the merchant UI
            if (Crestforge.UI.MadMerchantUI.Instance != null)
            {
                Crestforge.UI.MadMerchantUI.Instance.StartMerchantRound();
            }
            else
            {
                Debug.LogWarning("MadMerchantUI not found! Skipping merchant round.");
                OnMerchantRoundComplete();
            }
        }

        /// <summary>
        /// Called when the Mad Merchant round is complete
        /// </summary>
        public void OnMerchantRoundComplete()
        {
            var state = GameState.Instance;
            state.round.currentRound++;

            // Level up opponents in PvP mode
            if (state.currentGameMode == GameMode.PvP && OpponentManager.Instance != null)
            {
                OpponentManager.Instance.LevelUpOpponents();
            }

            StartPlanningPhase();
        }

        /// <summary>
        /// Start the Major Crest selection round
        /// </summary>
        private void StartMajorCrestRound()
        {
            var state = GameState.Instance;

            // Generate major crest options
            state.GenerateCrestSelection(CrestType.Major, GameConstants.Crests.CREST_CHOICES);

            // Show crest selection UI
            SetPhase(GamePhase.CrestSelect);
        }

        /// <summary>
        /// Save all unit positions before combat starts
        /// </summary>
        private void SaveUnitPositions()
        {
            savedUnitPositions.Clear();
            var state = GameState.Instance;

            if (state.playerBoard == null) return;

            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    var unit = state.playerBoard[x, y];
                    if (unit != null)
                    {
                        savedUnitPositions[unit.instanceId] = new Vector2Int(x, y);
                    }
                }
            }
        }

        /// <summary>
        /// Restore all unit positions after combat ends
        /// </summary>
        private void RestoreUnitPositions()
        {
            var state = GameState.Instance;

            if (state.playerBoard == null) return;

            // Clear the board first
            var unitsToRestore = new List<UnitInstance>();
            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    var unit = state.playerBoard[x, y];
                    if (unit != null)
                    {
                        unitsToRestore.Add(unit);
                        state.playerBoard[x, y] = null;
                    }
                }
            }

            // Restore units to their saved positions
            foreach (var unit in unitsToRestore)
            {
                if (savedUnitPositions.TryGetValue(unit.instanceId, out Vector2Int savedPos))
                {
                    state.playerBoard[savedPos.x, savedPos.y] = unit;
                    unit.boardPosition = savedPos;
                }
            }

            savedUnitPositions.Clear();
        }

        private void HandleCombatEnd(CombatResult result)
        {
            StartCoroutine(HandleCombatEndCoroutine(result));
        }

        private IEnumerator HandleCombatEndCoroutine(CombatResult result)
        {
            SetPhase(GamePhase.Results);

            var state = GameState.Instance;
            bool isPvPMode = state.currentGameMode == GameMode.PvP;
            bool isMultiplayer = state.currentGameMode == GameMode.Multiplayer;
            bool wasPvPRound = IsPvPRound() && isPvPMode;

            // Send combat result to server in multiplayer mode
            if (isMultiplayer)
            {
                var nm = NetworkManager.Instance;
                if (nm != null)
                {
                    nm.SendCombatResult(result.victory, state.player.health, result.damageToPlayer);
                }
            }

            // Unit positions will be restored when planning phase starts

            if (!result.victory)
            {
                // In PvE Wave mode, lose 1 life per defeat. In other modes, use calculated damage.
                int damage = state.currentGameMode == GameMode.PvEWave ? 1 : result.damageToPlayer;
                state.player.health -= damage;
                state.player.lossStreak++;
                state.player.winStreak = 0;

                // In PvP mode, record opponent win
                if (wasPvPRound && OpponentManager.Instance != null && OpponentManager.Instance.currentOpponent != null)
                {
                    OpponentManager.Instance.RecordOpponentWin(OpponentManager.Instance.currentOpponent);
                }
            }
            else
            {
                state.player.winStreak++;
                state.player.lossStreak = 0;

                // In PvP mode, apply damage to opponent
                if (wasPvPRound && OpponentManager.Instance != null && OpponentManager.Instance.currentOpponent != null)
                {
                    int damageToOpponent = OpponentManager.Instance.CalculateDamage(state.playerBoard);
                    OpponentManager.Instance.ApplyDamageToOpponent(OpponentManager.Instance.currentOpponent, damageToOpponent);
                }
            }

            OnRoundEnded?.Invoke(result);

            if (state.player.health <= 0)
            {
                SetPhase(GamePhase.GameOver);
                OnGameOver?.Invoke();
                yield break;
            }

            // Check for PvP victory (all opponents eliminated)
            if (isPvPMode && OpponentManager.Instance != null)
            {
                var activeOpponents = OpponentManager.Instance.GetActiveOpponents();
                if (activeOpponents.Count == 0)
                {
                    SetPhase(GamePhase.GameOver);
                    yield break;
                }
            }

            if (state.round.currentRound >= GameConstants.Rounds.MAX_ROUNDS && result.victory)
            {
                SetPhase(GamePhase.GameOver);
                yield break;
            }

            yield return new WaitForSeconds(resultsPhaseDuration);

            // Loot is now collected via clickable orbs that become consumable items
            // Selection UIs are triggered when consumables are used from inventory

            state.round.currentRound++;

            // Level up opponents each round in PvP mode
            if (isPvPMode && OpponentManager.Instance != null)
            {
                OpponentManager.Instance.LevelUpOpponents();
            }

            ResetUnitHealth();

            StartPlanningPhase();
        }

        private RoundType GetCurrentRoundType()
        {
            int roundIndex = GameState.Instance.round.currentRound - 1;
            if (roundIndex >= 0 && roundIndex < GameConstants.Rounds.ROUND_TYPES.Length)
            {
                return GameConstants.Rounds.ROUND_TYPES[roundIndex];
            }
            return RoundType.PvP;
        }

        /// <summary>
        /// Check if current round is the PvE intro round (round 1)
        /// </summary>
        public bool IsPvEIntroRound()
        {
            return GetCurrentRoundType() == RoundType.PvEIntro;
        }

        private void ResetUnitHealth()
        {
            var state = GameState.Instance;

            if (state.playerBoard != null)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                    {
                        var unit = state.playerBoard[x, y];
                        if (unit != null && unit.currentStats != null)
                        {
                            unit.currentHealth = unit.currentStats.health;
                            unit.currentMana = unit.currentStats.startingMana;
                            unit.currentShield = 0;
                        }
                    }
                }
            }

            if (state.bench != null)
            {
                foreach (var unit in state.bench)
                {
                    if (unit != null && unit.currentStats != null)
                    {
                        unit.currentHealth = unit.currentStats.health;
                        unit.currentMana = unit.currentStats.startingMana;
                        unit.currentShield = 0;
                    }
                }
            }
        }

        public void OnCrestSelected(CrestData crest)
        {
            GameState.Instance.SelectCrest(crest);

            // Check if this was a Major Crest round (no combat follows)
            if (GetCurrentRoundType() == RoundType.MajorCrest)
            {
                // Advance to next round
                var state = GameState.Instance;
                state.round.currentRound++;

                // Level up opponents in PvP mode
                if (state.currentGameMode == GameMode.PvP && OpponentManager.Instance != null)
                {
                    OpponentManager.Instance.LevelUpOpponents();
                }

                StartPlanningPhase();
            }
            else
            {
                // Return to planning phase (consumables are used during planning)
                SetPhase(GamePhase.Planning);
            }
        }

        public void OnItemSelected(ItemData item)
        {
            GameState.Instance.SelectItem(item);
            // Return to planning phase (consumables are used during planning)
            SetPhase(GamePhase.Planning);
        }

        private void SetPhase(GamePhase phase)
        {
            GameState.Instance.round.phase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        public void TogglePause()
        {
            isPaused = !isPaused;
        }
    }
}