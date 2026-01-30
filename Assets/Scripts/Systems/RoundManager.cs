using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Combat;

namespace Crestforge.Systems
{
    public class RoundManager : MonoBehaviour
    {
        public static RoundManager Instance { get; private set; }

        [Header("Settings")]
        public float planningPhaseDuration = 30f;
        public float resultsPhaseDuration = 3f;

        [Header("State")]
        public bool isPaused;
        private Coroutine phaseCoroutine;

        // Store original positions before combat
        private Dictionary<string, Vector2Int> savedUnitPositions = new Dictionary<string, Vector2Int>();

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
            }
        }

        private void OnDestroy()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatEnd -= HandleCombatEnd;
            }
        }

        public void StartGame()
        {
            GameState.Instance.InitializeGame();
            SetPhase(GamePhase.CrestSelect);
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
            Debug.Log($"Round {state.round.currentRound}: +{income} gold (Total: {state.player.gold})");

            if (!state.shop.isLocked)
            {
                state.shop.ReturnToPool(state.unitPool);
                state.shop.Refresh(state.unitPool, state.player.level);
            }

            if (state.round.currentRound == 1)
            {
                state.GenerateItemSelection(GameConstants.Rounds.ITEMS_PER_SELECTION);
                SetPhase(GamePhase.ItemSelect);
                return;
            }
            else if (state.round.currentRound == GameConstants.Rounds.ITEM_ROUND_2 + 1)
            {
                state.GenerateItemSelection(GameConstants.Rounds.ITEMS_PER_SELECTION);
                SetPhase(GamePhase.ItemSelect);
                return;
            }
            else if (state.round.currentRound == GameConstants.Rounds.ITEM_ROUND_3 + 1)
            {
                state.GenerateItemSelection(GameConstants.Rounds.ITEMS_PER_SELECTION);
                SetPhase(GamePhase.ItemSelect);
                return;
            }

            SetPhase(GamePhase.Planning);
            OnRoundStarted?.Invoke(state.round.currentRound);

            if (phaseCoroutine != null) StopCoroutine(phaseCoroutine);
            phaseCoroutine = StartCoroutine(PlanningPhaseTimer());
        }

        private IEnumerator PlanningPhaseTimer()
        {
            float timer = planningPhaseDuration;
            
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
                StartCombatPhase();
            }
        }

        public void StartCombatPhase()
        {
            if (phaseCoroutine != null) StopCoroutine(phaseCoroutine);
            
            var state = GameState.Instance;

            // Save unit positions before combat
            SaveUnitPositions();

            SetPhase(GamePhase.Combat);

            var enemyBoard = EnemyWaveGenerator.Instance.GenerateWave(state.round.currentRound);
            state.enemyBoard = enemyBoard;

            var playerCrests = new List<CrestData>();
            if (state.minorCrests != null) playerCrests.AddRange(state.minorCrests);
            if (state.majorCrests != null) playerCrests.AddRange(state.majorCrests);

            CombatManager.Instance.StartCombat(
                state.playerBoard,
                enemyBoard,
                playerCrests,
                new List<CrestData>()
            );
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

            // Unit positions will be restored when planning phase starts

            if (!result.victory)
            {
                // In PvE Wave mode, lose 1 life per defeat. In other modes, use calculated damage.
                int damage = state.currentGameMode == GameMode.PvEWave ? 1 : result.damageToPlayer;
                state.player.health -= damage;
                state.player.lossStreak++;
                state.player.winStreak = 0;
                
                if (state.currentGameMode == GameMode.PvEWave)
                {
                    Debug.Log($"Defeat! Lost 1 life. Lives remaining: {state.player.health}");
                }
                else
                {
                    Debug.Log($"Defeat! Took {damage} damage. Health: {state.player.health}");
                }
            }
            else
            {
                state.player.winStreak++;
                state.player.lossStreak = 0;
                Debug.Log($"Victory! Win streak: {state.player.winStreak}");
            }

            OnRoundEnded?.Invoke(result);

            if (state.player.health <= 0)
            {
                SetPhase(GamePhase.GameOver);
                OnGameOver?.Invoke();
                Debug.Log("GAME OVER!");
                yield break;
            }

            if (state.round.currentRound >= GameConstants.Rounds.MAX_ROUNDS && result.victory)
            {
                SetPhase(GamePhase.GameOver);
                Debug.Log("VICTORY! You completed all rounds!");
                yield break;
            }

            yield return new WaitForSeconds(resultsPhaseDuration);

            state.round.currentRound++;

            ResetUnitHealth();

            StartPlanningPhase();
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
            
            if (GameState.Instance.round.currentRound == 1)
            {
                StartPlanningPhase();
            }
        }

        public void OnItemSelected(ItemData item)
        {
            GameState.Instance.SelectItem(item);
            SetPhase(GamePhase.Planning);
        }

        private void SetPhase(GamePhase phase)
        {
            GameState.Instance.round.phase = phase;
            OnPhaseChanged?.Invoke(phase);
            Debug.Log($"Phase changed to: {phase}");
        }

        public void TogglePause()
        {
            isPaused = !isPaused;
        }
    }
}