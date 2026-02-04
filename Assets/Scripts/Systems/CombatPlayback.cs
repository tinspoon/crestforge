using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Crestforge.Data;
using Crestforge.Networking;
using Crestforge.Visuals;

namespace Crestforge.Systems
{
    /// <summary>
    /// Handles playback of combat events on a specific board.
    /// Can be used for player's own combat or scouted combats.
    /// </summary>
    public class CombatPlayback
    {
        // Settings
        // Match server's 50ms tick rate for real-time playback
        public float tickDuration = 0.05f;
        // Movement animation duration (matches single-player MOVE_COOLDOWN)
        public float moveAnimationDuration = 0.35f;
        public float playbackSpeed = 1f;

        // State
        public bool IsPlaying { get; private set; }
        public bool IsInVictoryPose { get; private set; }
        public int CurrentTick { get; private set; }
        public int TotalTicks { get; private set; }
        public string CombatWinner { get; private set; }

        // References
        public HexBoard3D Board { get; private set; }
        private MonoBehaviour coroutineRunner;

        // Combat data
        private List<ServerCombatEvent> events = new List<ServerCombatEvent>();
        private int eventIndex;
        private string viewingTeam;
        private bool isHostViewing;

        // Combat units being visualized
        private Dictionary<string, CombatUnitVisual> combatUnits = new Dictionary<string, CombatUnitVisual>();

        // Original positions for reused visuals (so we can restore them after combat)
        private Dictionary<string, Vector3> reusedVisualOriginalPositions = new Dictionary<string, Vector3>();

        // Track units that died during combat (so we don't restore them)
        private HashSet<string> deadUnitIds = new HashSet<string>();

        // Coroutine reference for cleanup
        private Coroutine playbackCoroutine;

        // Events
        public event Action OnPlaybackStarted;
        public event Action OnPlaybackEnded;
        public event Action<string, int> OnUnitDamaged; // instanceId, damage
        public event Action<string> OnUnitDied; // instanceId

        /// <summary>
        /// Create a new combat playback instance
        /// </summary>
        /// <param name="runner">MonoBehaviour to run coroutines on</param>
        public CombatPlayback(MonoBehaviour runner)
        {
            coroutineRunner = runner;
        }

        /// <summary>
        /// Get all active combat unit visuals
        /// </summary>
        public Dictionary<string, CombatUnitVisual> CombatUnits => combatUnits;

        /// <summary>
        /// Find a CombatUnitVisual by its associated UnitVisual3D
        /// </summary>
        public CombatUnitVisual GetCombatUnitByVisual(UnitVisual3D visual)
        {
            if (visual == null) return null;

            foreach (var kvp in combatUnits)
            {
                if (kvp.Value != null && kvp.Value.BaseVisual == visual)
                {
                    return kvp.Value;
                }
                if (kvp.Value != null && kvp.Value.gameObject == visual.gameObject)
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Find a CombatUnitVisual by instance ID
        /// </summary>
        public CombatUnitVisual GetCombatUnitByInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            combatUnits.TryGetValue(instanceId, out var visual);
            return visual;
        }

        /// <summary>
        /// Start playback of combat events
        /// </summary>
        /// <param name="board">The hex board to visualize on</param>
        /// <param name="combatEvents">Combat events to play</param>
        /// <param name="team">Team we're viewing as (player1 or player2)</param>
        /// <param name="isHost">Whether viewing team is the host</param>
        /// <param name="startFromTick">If > 0, fast-forward to this tick</param>
        /// <param name="reuseExistingVisuals">If true, try to reuse existing BoardManager visuals</param>
        public void StartPlayback(HexBoard3D board, List<ServerCombatEvent> combatEvents, string team, bool isHost, int startFromTick = 0, bool reuseExistingVisuals = false)
        {
            if (board == null || combatEvents == null || combatEvents.Count == 0)
            {
                Debug.LogWarning("[CombatPlayback] Invalid parameters for playback");
                return;
            }

            Board = board;
            events = combatEvents;
            viewingTeam = team;
            isHostViewing = isHost;

            eventIndex = 0;
            CurrentTick = 0;
            TotalTicks = 0;
            CombatWinner = null;

            // Find total duration
            foreach (var evt in events)
            {
                if (evt.tick > TotalTicks) TotalTicks = evt.tick;
            }


            // If we need to fast-forward, process events up to that tick instantly
            if (startFromTick > 0)
            {
                FastForwardToTick(startFromTick, reuseExistingVisuals);
            }

            IsPlaying = true;
            OnPlaybackStarted?.Invoke();
            playbackCoroutine = coroutineRunner.StartCoroutine(PlaybackCoroutine(reuseExistingVisuals));
        }

        /// <summary>
        /// Stop playback and cleanup
        /// </summary>
        public void StopPlayback()
        {
            IsPlaying = false;
            IsInVictoryPose = false;
            CombatWinner = null;

            if (playbackCoroutine != null)
            {
                coroutineRunner.StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            CleanupCombatVisuals();
        }

        /// <summary>
        /// End victory pose and clean up (called when planning phase starts)
        /// </summary>
        public void EndVictoryPose()
        {
            if (!IsInVictoryPose) return;
            IsInVictoryPose = false;
            CleanupCombatVisuals();
        }

        /// <summary>
        /// Fast-forward to a specific tick by processing events instantly
        /// </summary>
        private void FastForwardToTick(int targetTick, bool reuseExistingVisuals)
        {

            while (eventIndex < events.Count && events[eventIndex].tick <= targetTick)
            {
                var evt = events[eventIndex];

                switch (evt.type)
                {
                    case "combatStart":
                        ProcessCombatStart(evt, reuseExistingVisuals);
                        break;
                    case "unitMove":
                        ProcessUnitMoveInstant(evt);
                        break;
                    case "unitDamage":
                        ProcessUnitDamageInstant(evt);
                        break;
                    case "unitDeath":
                        ProcessUnitDeathInstant(evt);
                        break;
                    // Skip unitAttack during fast-forward (just visual)
                }
                eventIndex++;
            }

            CurrentTick = targetTick;
        }

        private IEnumerator PlaybackCoroutine(bool reuseExistingVisuals)
        {
            while (IsPlaying && eventIndex < events.Count)
            {
                // Process all events at current tick
                bool hadEvents = false;
                while (eventIndex < events.Count && events[eventIndex].tick <= CurrentTick)
                {
                    ProcessEvent(events[eventIndex], reuseExistingVisuals);
                    eventIndex++;
                    hadEvents = true;
                }

                // Find the next tick that has events
                int nextEventTick = CurrentTick + 1;
                if (eventIndex < events.Count)
                {
                    nextEventTick = events[eventIndex].tick;
                }

                // Wait real-time: tick gap * tick duration (no compression, true real-time playback)
                int tickGap = nextEventTick - CurrentTick;
                float waitTime = tickGap * tickDuration / playbackSpeed;

                yield return new WaitForSeconds(waitTime);
                CurrentTick = nextEventTick;

                if (CurrentTick > TotalTicks + 10)
                {
                    break;
                }
            }

            IsPlaying = false;

            // Start victory pose
            StartVictoryPose();

            OnPlaybackEnded?.Invoke();
        }

        private void ProcessEvent(ServerCombatEvent evt, bool reuseExistingVisuals)
        {
            switch (evt.type)
            {
                case "combatStart":
                    ProcessCombatStart(evt, reuseExistingVisuals);
                    break;
                case "unitMove":
                    ProcessUnitMove(evt);
                    break;
                case "unitAttack":
                    ProcessUnitAttack(evt);
                    break;
                case "unitDamage":
                    ProcessUnitDamage(evt);
                    break;
                case "unitDeath":
                    ProcessUnitDeath(evt);
                    break;
                case "combatEnd":
                    ProcessCombatEnd(evt);
                    break;
            }
        }

        private void ProcessCombatStart(ServerCombatEvent evt, bool reuseExistingVisuals)
        {

            // Only hide board units if we're the host and reusing visuals
            if (isHostViewing && reuseExistingVisuals)
            {
                var boardManager = BoardManager3D.Instance;
                if (boardManager != null)
                {
                    boardManager.SetBoardUnitVisualsVisible(false);
                }
            }

            // Clear existing combat units
            CleanupCombatVisuals();

            // Create visuals for all units
            if (evt.units != null)
            {
                foreach (var unit in evt.units)
                {
                    CreateCombatUnitVisual(unit, reuseExistingVisuals);
                }
            }
        }

        private void CreateCombatUnitVisual(ServerCombatUnit unit, bool reuseExistingVisuals)
        {
            if (Board == null) return;


            var serverState = ServerGameState.Instance;
            var boardManager = BoardManager3D.Instance;

            // Calculate world position
            Vector3 worldPos = Board.GetTileWorldPosition(unit.x, unit.y);
            worldPos.y = 0.15f;

            CombatUnitVisual visual = null;

            // ALWAYS try to reuse existing visuals from the registry first
            UnitVisual3D existingVisual = null;

            // 1. Check the combat board's registry
            if (Board.Registry != null)
            {
                existingVisual = Board.Registry.GetVisualByInstanceId(unit.instanceId);
            }

            // 2. If not found and we're the host, check BoardManager3D
            if (existingVisual == null && isHostViewing && boardManager != null)
            {
                existingVisual = boardManager.GetUnitVisualByInstanceId(unit.instanceId);
            }

            // 3. If still not found, check all board registries (for away player units)
            if (existingVisual == null)
            {
                foreach (var board in HexBoard3D.AllBoards)
                {
                    if (board != null && board.Registry != null)
                    {
                        existingVisual = board.Registry.GetVisualByInstanceId(unit.instanceId);
                        if (existingVisual != null)
                        {
                            break;
                        }
                    }
                }
            }

            if (existingVisual != null)
            {

                // Save original position so we can restore it after combat
                reusedVisualOriginalPositions[unit.instanceId] = existingVisual.transform.position;

                // Re-enable the visual and move it to combat position
                existingVisual.gameObject.SetActive(true);
                existingVisual.SetPosition(worldPos);

                visual = existingVisual.gameObject.GetComponent<CombatUnitVisual>();
                if (visual == null)
                {
                    visual = existingVisual.gameObject.AddComponent<CombatUnitVisual>();
                }
                visual.Initialize(unit, existingVisual, isReusedVisual: true);
            }
            else
            {
                // No existing visual found - only create new for PvE enemies or units not on any board

                // Create a proper UnitVisual3D with model
                UnitVisual3D unitVisual3D = CreateUnitVisual3DForCombat(unit, worldPos);

                if (unitVisual3D != null)
                {
                    visual = unitVisual3D.gameObject.AddComponent<CombatUnitVisual>();
                    visual.Initialize(unit, unitVisual3D, isReusedVisual: false);
                }
                else
                {
                    // Fallback to capsule
                    Debug.LogWarning($"[CombatPlayback] Falling back to capsule for {unit.name}");
                    var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name = $"CombatUnit_{unit.name}_{unit.instanceId.Substring(0, 8)}";
                    go.transform.position = worldPos;
                    go.transform.localScale = Vector3.one * 0.5f;

                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        var mat = new Material(Shader.Find("Unlit/Color"));
                        mat.color = unit.playerId == "player1" ? Color.blue : Color.red;
                        renderer.material = mat;
                    }

                    visual = go.AddComponent<CombatUnitVisual>();
                    visual.Initialize(unit, null);
                }
            }

            if (visual != null)
            {
                combatUnits[unit.instanceId] = visual;
            }
        }

        private UnitVisual3D CreateUnitVisual3DForCombat(ServerCombatUnit unit, Vector3 worldPos)
        {
            var serverState = ServerGameState.Instance;
            if (serverState == null)
            {
                Debug.LogWarning("[CombatPlayback] ServerGameState not available");
                return null;
            }

            var template = serverState.GetUnitTemplate(unit.unitId);
            if (template == null)
            {
                // Try direct load by name (for PvE enemies)
                string capitalized = char.ToUpper(unit.unitId[0]) + unit.unitId.Substring(1);
                string[] searchPaths = new string[]
                {
                    $"ScriptableObjects/PvEUnits/{capitalized}",
                    $"ScriptableObjects/PvEUnits/{unit.unitId}",
                    $"ScriptableObjects/NewUnits/{capitalized}",
                    $"ScriptableObjects/Units/{capitalized}"
                };

                foreach (var path in searchPaths)
                {
                    template = Resources.Load<UnitData>(path);
                    if (template != null)
                    {
                        break;
                    }
                }

                if (template == null)
                {
                    Debug.LogError($"[CombatPlayback] Could not find template for '{unit.unitId}'");
                    return null;
                }
            }

            // Create temporary UnitInstance
            var tempUnit = UnitInstance.Create(template, 1);
            tempUnit.instanceId = unit.instanceId;

            // Create visual
            bool isEnemy = unit.playerId != viewingTeam;
            GameObject visualObj = new GameObject($"CombatUnit_{unit.name}");
            visualObj.transform.position = worldPos;

            var unitVisual = visualObj.AddComponent<UnitVisual3D>();
            unitVisual.Initialize(tempUnit, isEnemy);
            unitVisual.SetPosition(worldPos);
            unitVisual.ServerInstanceId = unit.instanceId;

            if (unit.items != null && unit.items.Count > 0)
            {
                unitVisual.SetServerItems(unit.items);
            }

            return unitVisual;
        }

        private void ProcessUnitMove(ServerCombatEvent evt)
        {
            if (!combatUnits.TryGetValue(evt.instanceId, out var visual)) return;
            if (visual == null || Board == null) return;

            int gridY = Mathf.Clamp(evt.y, 0, 7);
            int gridX = Mathf.Clamp(evt.x, 0, 6);

            Vector3 targetPos = Board.GetTileWorldPosition(gridX, gridY);
            targetPos.y = 0.15f;

            visual.MoveTo(targetPos, moveAnimationDuration / playbackSpeed);
        }

        private void ProcessUnitMoveInstant(ServerCombatEvent evt)
        {
            if (!combatUnits.TryGetValue(evt.instanceId, out var visual)) return;
            if (visual == null || Board == null) return;

            int gridY = Mathf.Clamp(evt.y, 0, 7);
            int gridX = Mathf.Clamp(evt.x, 0, 6);

            Vector3 targetPos = Board.GetTileWorldPosition(gridX, gridY);
            targetPos.y = 0.15f;

            // Set position instantly
            visual.transform.position = targetPos;
            if (visual.BaseVisual != null)
            {
                visual.BaseVisual.SetPosition(targetPos);
            }
        }

        private void ProcessUnitAttack(ServerCombatEvent evt)
        {
            if (!combatUnits.TryGetValue(evt.attackerId, out var attacker)) return;
            if (!combatUnits.TryGetValue(evt.targetId, out var target)) return;
            if (attacker == null || target == null) return;

            attacker.PlayAttackAnimation(target.transform.position);
        }

        private void ProcessUnitDamage(ServerCombatEvent evt)
        {
            if (!combatUnits.TryGetValue(evt.instanceId, out var visual)) return;
            if (visual == null) return;

            visual.TakeDamage(evt.damage, evt.currentHealth, evt.maxHealth);
            OnUnitDamaged?.Invoke(evt.instanceId, evt.damage);

            SpawnDamageNumber(visual.transform.position, evt.damage);
        }

        private void ProcessUnitDamageInstant(ServerCombatEvent evt)
        {
            if (!combatUnits.TryGetValue(evt.instanceId, out var visual)) return;
            if (visual == null) return;

            visual.SetHealthInstant(evt.currentHealth, evt.maxHealth);
        }

        private void ProcessUnitDeath(ServerCombatEvent evt)
        {
            if (!combatUnits.TryGetValue(evt.instanceId, out var visual)) return;
            if (visual == null) return;

            // Track that this unit died (so we don't restore it during cleanup)
            deadUnitIds.Add(evt.instanceId);

            visual.PlayDeathAnimation();
            OnUnitDied?.Invoke(evt.instanceId);

            // Spawn loot orb if the unit dropped loot
            if (!string.IsNullOrEmpty(evt.lootType) && evt.lootType != "none" && !string.IsNullOrEmpty(evt.lootId))
            {
                Vector3 lootPosition = visual.transform.position;
                if (evt.lootPosition != null)
                {
                    var hexBoard = Board ?? HexBoard3D.Instance;
                    if (hexBoard != null)
                    {
                        lootPosition = hexBoard.GetTileWorldPosition(evt.lootPosition.x, evt.lootPosition.y);
                    }
                }
                LootOrb.CreateMultiplayer(lootPosition, evt.lootType, evt.lootId);
            }

            combatUnits.Remove(evt.instanceId);

            if (visual.gameObject != null)
            {
                if (visual.IsReusedVisual)
                {
                    // Restore original position before hiding - this ensures the visual is back
                    // on its original board for proper registry cleanup
                    if (reusedVisualOriginalPositions.TryGetValue(evt.instanceId, out Vector3 originalPos))
                    {
                        if (visual.BaseVisual != null)
                        {
                            visual.BaseVisual.SetPosition(originalPos);
                        }
                        else
                        {
                            visual.transform.position = originalPos;
                        }
                        reusedVisualOriginalPositions.Remove(evt.instanceId);
                    }

                    // For reused visuals, just hide them - the registry will handle cleanup
                    // The unit is dead so it won't return to the board
                    visual.gameObject.SetActive(false);
                    // Remove the CombatUnitVisual component but keep UnitVisual3D
                    GameObject.Destroy(visual);
                }
                else
                {
                    // For newly created visuals, destroy the whole object
                    GameObject.Destroy(visual.gameObject, 0.5f);
                }
            }
        }

        private void ProcessUnitDeathInstant(ServerCombatEvent evt)
        {
            if (!combatUnits.TryGetValue(evt.instanceId, out var visual)) return;

            combatUnits.Remove(evt.instanceId);

            if (visual != null && visual.gameObject != null)
            {
                if (visual.IsReusedVisual)
                {
                    // For reused visuals, just hide them
                    visual.gameObject.SetActive(false);
                    GameObject.Destroy(visual);
                }
                else
                {
                    GameObject.Destroy(visual.gameObject);
                }
            }
        }

        private void ProcessCombatEnd(ServerCombatEvent evt)
        {
            CombatWinner = evt.winner;
        }

        private void SpawnDamageNumber(Vector3 position, int damage)
        {
            GameObject dmgObj = new GameObject("DamageNumber");
            dmgObj.transform.position = position + Vector3.up * 1f;

            TextMesh tm = dmgObj.AddComponent<TextMesh>();
            tm.text = damage.ToString();
            tm.fontSize = 48;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.red;
            tm.fontStyle = FontStyle.Bold;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (tm.font == null)
            {
                tm.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            dmgObj.AddComponent<BillboardUI>();
            var floater = dmgObj.AddComponent<FloatingText>();
            floater.lifetime = 0.8f;
            floater.floatSpeed = 1.5f;
        }

        private void StartVictoryPose()
        {
            IsInVictoryPose = true;

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[CombatPlayback] StartVictoryPose: Camera.main is null");
                return;
            }

            int playerUnitsRotated = 0;
            bool myTeamWon = CombatWinner == viewingTeam;

            foreach (var kvp in combatUnits)
            {
                var visual = kvp.Value;
                if (visual != null && visual.gameObject != null && visual.gameObject.activeInHierarchy)
                {
                    // Freeze position and lock rotation for reused visuals during victory pose
                    if (visual.IsReusedVisual && visual.BaseVisual != null)
                    {
                        visual.BaseVisual.SetPosition(visual.transform.position);
                        visual.BaseVisual.FreezePosition = true;
                        visual.BaseVisual.LockRotation = true;  // Prevent UnitVisual3D.Update from overriding rotation
                        visual.BaseVisual.StopAttackAnimation();  // Stop any in-progress attack animation
                    }

                    UnitAnimator unitAnimator = visual.GetComponentInChildren<UnitAnimator>();
                    bool isWinningTeam = visual.TeamId == CombatWinner;

                    if (isWinningTeam)
                    {
                        if (unitAnimator != null)
                        {
                            unitAnimator.PlayVictory();
                        }

                        // All winning units should face the camera for the viewer
                        // Use camera's view direction - units should face where the camera is looking FROM
                        // This works correctly regardless of camera rotation (including 180 degree for away player)
                        Vector3 towardsCamera = -cam.transform.forward;
                        towardsCamera.y = 0;
                        if (towardsCamera.sqrMagnitude < 0.01f)
                        {
                            towardsCamera = Vector3.back;
                        }
                        towardsCamera.Normalize();
                        Quaternion faceCamera = Quaternion.LookRotation(towardsCamera);

                        coroutineRunner.StartCoroutine(RotateToFaceCamera(visual.transform, faceCamera));
                        playerUnitsRotated++;
                    }
                    else
                    {
                        if (unitAnimator != null)
                        {
                            unitAnimator.ResetToIdle();
                        }
                    }
                }
            }

        }

        private IEnumerator RotateToFaceCamera(Transform unitTransform, Quaternion targetRotation)
        {
            if (unitTransform == null) yield break;

            float duration = 0.3f;
            float elapsed = 0f;
            Quaternion startRotation = unitTransform.rotation;

            while (elapsed < duration && unitTransform != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                unitTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            if (unitTransform != null)
            {
                unitTransform.rotation = targetRotation;
            }
        }

        private void CleanupCombatVisuals()
        {

            foreach (var kvp in combatUnits)
            {
                var instanceId = kvp.Key;
                var visual = kvp.Value;

                if (visual != null && visual.gameObject != null)
                {
                    bool unitDied = deadUnitIds.Contains(instanceId);

                    if (visual.IsReusedVisual)
                    {
                        // Only restore position and re-enable if the unit survived
                        if (!unitDied)
                        {
                            // Restore original position if we saved one
                            if (reusedVisualOriginalPositions.TryGetValue(instanceId, out Vector3 originalPos))
                            {
                                if (visual.BaseVisual != null)
                                {
                                    visual.BaseVisual.SetPosition(originalPos);
                                }
                                else
                                {
                                    visual.transform.position = originalPos;
                                }
                            }

                            // Reset animation and unfreeze position
                            if (visual.BaseVisual != null)
                            {
                                visual.BaseVisual.FreezePosition = false;
                            }
                            UnitAnimator unitAnimator = visual.GetComponentInChildren<UnitAnimator>();
                            if (unitAnimator != null)
                            {
                                unitAnimator.ResetToIdle();
                            }

                            // Re-enable the visual
                            visual.gameObject.SetActive(true);
                        }
                        else
                        {
                            // Unit died - ensure the visual is hidden
                            // (death animation coroutine might not have completed before we destroy the component)
                            visual.gameObject.SetActive(false);
                        }

                        GameObject.Destroy(visual); // Just the component
                    }
                    else
                    {
                        GameObject.Destroy(visual.gameObject); // The whole object
                    }
                }
            }
            combatUnits.Clear();
            reusedVisualOriginalPositions.Clear();
            deadUnitIds.Clear();
        }
    }

    /// <summary>
    /// Visual component for combat unit animations
    /// </summary>
    public class CombatUnitVisual : MonoBehaviour
    {
        public string InstanceId { get; private set; }
        public string TeamId { get; private set; }
        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public UnitVisual3D BaseVisual { get; private set; }
        public bool IsReusedVisual { get; private set; }
        public ServerCombatUnit CombatUnitData { get; private set; }

        private Vector3 moveTarget;
        private bool isMoving;
        private float moveTime;
        private float moveDuration;
        private Vector3 moveStart;

        public void Initialize(ServerCombatUnit unit, UnitVisual3D baseVisual, bool isReusedVisual = false)
        {
            InstanceId = unit.instanceId;
            TeamId = unit.playerId;
            CurrentHealth = unit.health;
            MaxHealth = unit.maxHealth;
            BaseVisual = baseVisual;
            IsReusedVisual = isReusedVisual;
            CombatUnitData = unit;
        }

        private void Update()
        {
            if (isMoving)
            {
                moveTime += Time.deltaTime;
                float t = Mathf.Clamp01(moveTime / moveDuration);
                transform.position = Vector3.Lerp(moveStart, moveTarget, t);

                if (t >= 1f)
                {
                    isMoving = false;
                }
            }
        }

        public void MoveTo(Vector3 position, float duration)
        {
            if (BaseVisual != null)
            {
                BaseVisual.MoveTo(position, duration);
                return;
            }

            moveStart = transform.position;
            moveTarget = position;
            moveDuration = duration;
            moveTime = 0f;
            isMoving = true;
        }

        private Coroutine attackCoroutine;
        private bool isAttacking;

        public void PlayAttackAnimation(Vector3 targetPosition)
        {
            if (BaseVisual != null)
            {
                // Pass attack speed from server combat data for proper animation timing
                float attackSpeed = CombatUnitData?.stats?.attackSpeed ?? 1f;
                BaseVisual.PlayAttackAnimation(targetPosition, attackSpeed);
                return;
            }

            // Prevent overlapping attack coroutines
            if (isAttacking) return;

            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
            }
            attackCoroutine = StartCoroutine(AttackCoroutine(targetPosition));
        }

        private IEnumerator AttackCoroutine(Vector3 targetPos)
        {
            isAttacking = true;
            Vector3 originalPos = transform.position;
            Vector3 lungeDir = (targetPos - originalPos).normalized;
            Vector3 lungePos = originalPos + lungeDir * 0.3f;

            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 10f;
                transform.position = Vector3.Lerp(originalPos, lungePos, t);
                yield return null;
            }

            t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 10f;
                transform.position = Vector3.Lerp(lungePos, originalPos, t);
                yield return null;
            }

            transform.position = originalPos;
            isAttacking = false;
            attackCoroutine = null;
        }

        public void TakeDamage(int damage, int newHealth, int maxHealth)
        {
            CurrentHealth = newHealth;
            MaxHealth = maxHealth;

            if (BaseVisual != null)
            {
                BaseVisual.PlayHitEffect();
                BaseVisual.UpdateHealthBar(newHealth, maxHealth);
            }
            else
            {
                StartCoroutine(DamageFlashCoroutine());
            }
        }

        public void SetHealthInstant(int newHealth, int maxHealth)
        {
            CurrentHealth = newHealth;
            MaxHealth = maxHealth;

            if (BaseVisual != null)
            {
                BaseVisual.UpdateHealthBar(newHealth, maxHealth);
            }
        }

        private IEnumerator DamageFlashCoroutine()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null) yield break;

            Color originalColor = renderer.material.color;
            renderer.material.color = Color.red;

            yield return new WaitForSeconds(0.1f);

            renderer.material.color = originalColor;
        }

        public void PlayDeathAnimation()
        {
            StartCoroutine(DeathCoroutine());
        }

        private IEnumerator DeathCoroutine()
        {
            Vector3 originalScale = transform.localScale;
            float t = 0;

            while (t < 1f)
            {
                t += Time.deltaTime * 2f;
                transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);

                var renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color c = renderer.material.color;
                    c.a = 1f - t;
                    renderer.material.color = c;
                }

                yield return null;
            }

            if (BaseVisual != null)
            {
                gameObject.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
