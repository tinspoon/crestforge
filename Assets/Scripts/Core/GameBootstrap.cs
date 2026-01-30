using UnityEngine;
using Crestforge.Systems;
using Crestforge.Combat;

namespace Crestforge.Core
{
    /// <summary>
    /// Main game entry point. Initializes all systems and starts the game.
    /// Attach this to a GameObject in your main scene.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Prefab References")]
        public GameObject gameStatePrefab;
        public GameObject roundManagerPrefab;
        public GameObject combatManagerPrefab;
        public GameObject enemyGeneratorPrefab;

        [Header("Auto Start")]
        public bool autoStartGame = true;

        private void Awake()
        {
            // Ensure all managers exist
            EnsureManagerExists<GameState>(gameStatePrefab, "GameState");
            EnsureManagerExists<RoundManager>(roundManagerPrefab, "RoundManager");
            EnsureManagerExists<CombatManager>(combatManagerPrefab, "CombatManager");
            EnsureManagerExists<EnemyWaveGenerator>(enemyGeneratorPrefab, "EnemyWaveGenerator");
        }

        private void Start()
        {
            if (autoStartGame)
            {
                StartGame();
            }
        }

        public void StartGame()
        {
            Debug.Log("=== CRESTFORGE ===");
            Debug.Log("Starting new game...");
            
            RoundManager.Instance.StartGame();
        }

        private void EnsureManagerExists<T>(GameObject prefab, string name) where T : MonoBehaviour
        {
            if (FindObjectOfType<T>() == null)
            {
                GameObject go;
                if (prefab != null)
                {
                    go = Instantiate(prefab);
                }
                else
                {
                    go = new GameObject(name);
                    go.AddComponent<T>();
                }
                go.name = name;
            }
        }
    }
}
