using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Networking;
using Crestforge.Systems;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Single source of truth for all unit visuals on a board.
    /// Each HexBoard3D has one BoardVisualRegistry that owns ALL unit visuals for that board.
    /// This eliminates duplicate visual creation across BoardManager3D, OpponentBoardVisualizer,
    /// ServerCombatVisualizer, CombatPlayback, and ScoutingUI.
    /// </summary>
    public class BoardVisualRegistry : MonoBehaviour
    {
        [Header("Settings")]
        public float unitYOffset = 0.15f;

        // The board this registry belongs to
        private HexBoard3D hexBoard;

        // Visual dictionaries - THE single source of truth
        private Dictionary<string, UnitVisual3D> boardVisuals = new Dictionary<string, UnitVisual3D>();
        private Dictionary<int, UnitVisual3D> benchVisuals = new Dictionary<int, UnitVisual3D>();

        // Away state tracking - when this board's units are teleported to fight on another board
        private bool isAway = false;
        private HexBoard3D awayTargetBoard;
        private Dictionary<string, Vector3> originalPositions = new Dictionary<string, Vector3>();
        private Dictionary<int, Vector3> originalBenchPositions = new Dictionary<int, Vector3>();

        // Whether the viewer is on the opposite side (camera at 180°), requiring reversed bench order
        private bool viewFromOppositeSide = false;

        // Pending merge tracking - don't revert star levels for these units until server confirms
        private HashSet<string> pendingMergeInstanceIds = new HashSet<string>();

        /// <summary>
        /// Whether this board's units have been teleported away for combat
        /// </summary>
        public bool IsAway => isAway;

        /// <summary>
        /// The board where units are currently fighting (if away)
        /// </summary>
        public HexBoard3D AwayTargetBoard => awayTargetBoard;

        /// <summary>
        /// Set to true when the viewer is on the opposite side of the board (camera at 180°).
        /// This reverses bench slot positions so they appear left-to-right from the camera,
        /// and rotates all units to face the opposite direction.
        /// </summary>
        public bool ViewFromOppositeSide
        {
            get => viewFromOppositeSide;
            set
            {
                if (viewFromOppositeSide != value)
                {
                    viewFromOppositeSide = value;
                    // Rotate all units to face the new camera direction
                    RotateAllUnitsForPerspective();
                }
            }
        }

        /// <summary>
        /// Rotate all board and bench units to face the camera
        /// </summary>
        private void RotateAllUnitsForPerspective()
        {
            // Rotate all board visuals to face camera
            foreach (var kvp in boardVisuals)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.FaceCamera();
                }
            }

            // Rotate all bench visuals to face camera
            foreach (var kvp in benchVisuals)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.FaceCamera();
                }
            }
        }

        private void Awake()
        {
            hexBoard = GetComponent<HexBoard3D>();
            if (hexBoard == null)
            {
                Debug.LogError("[BoardVisualRegistry] Must be attached to a HexBoard3D!");
            }
        }

        /// <summary>
        /// Get or create a visual for a board unit
        /// </summary>
        /// <param name="serverUnit">Server unit data</param>
        /// <param name="x">Board X coordinate</param>
        /// <param name="y">Board Y coordinate</param>
        /// <param name="isEnemy">Whether this is an enemy unit</param>
        /// <returns>The unit visual (existing or newly created)</returns>
        public UnitVisual3D GetOrCreateBoardVisual(ServerUnitData serverUnit, int x, int y, bool isEnemy)
        {
            if (serverUnit == null || string.IsNullOrEmpty(serverUnit.instanceId))
                return null;

            Vector3 worldPos = hexBoard.GetTileWorldPosition(x, y);
            worldPos.y = unitYOffset;

            // Check if visual already exists
            if (boardVisuals.TryGetValue(serverUnit.instanceId, out UnitVisual3D existing) && existing != null)
            {
                // Ensure visual is active (may have been deactivated during combat)
                if (!existing.gameObject.activeSelf)
                {
                    existing.gameObject.SetActive(true);
                }
                // Unfreeze position (may have been frozen during combat)
                existing.FreezePosition = false;

                // Update position if needed
                if (Vector3.Distance(existing.transform.position, worldPos) > 0.1f)
                {
                    existing.SetPositionAndFaceCamera(worldPos);
                }
                // Update stars if star level changed (e.g. after merge)
                // Skip if this is a pending merge and we would be downgrading (wait for server confirmation)
                if (existing.DisplayedStarLevel != serverUnit.starLevel)
                {
                    bool wouldDowngrade = serverUnit.starLevel < existing.DisplayedStarLevel;
                    if (!wouldDowngrade || !IsPendingMerge(serverUnit.instanceId))
                    {
                        existing.UpdateStars(serverUnit.starLevel);
                    }
                }
                // Update items
                existing.SetServerItems(serverUnit.items);
                return existing;
            }

            // Clean up stale entry if exists
            if (boardVisuals.ContainsKey(serverUnit.instanceId))
            {
                boardVisuals.Remove(serverUnit.instanceId);
            }

            // Create new visual
            UnitVisual3D visual = CreateUnitVisual(serverUnit, worldPos, isEnemy);
            if (visual != null)
            {
                boardVisuals[serverUnit.instanceId] = visual;

                // Ensure new units face the camera when viewing from opposite side
                if (viewFromOppositeSide)
                {
                    visual.FaceCamera();
                }
            }
            return visual;
        }

        /// <summary>
        /// Get or create a visual for a bench unit
        /// </summary>
        /// <param name="serverUnit">Server unit data</param>
        /// <param name="slotIndex">Bench slot index</param>
        /// <returns>The unit visual (existing or newly created)</returns>
        public UnitVisual3D GetOrCreateBenchVisual(ServerUnitData serverUnit, int slotIndex)
        {
            if (serverUnit == null || string.IsNullOrEmpty(serverUnit.instanceId))
                return null;

            // Use away position if we're currently fighting on another board
            Vector3 worldPos = isAway && awayTargetBoard != null
                ? GetAwayBenchSlotWorldPosition(awayTargetBoard, slotIndex)
                : GetBenchSlotWorldPosition(slotIndex);

            // Check if visual already exists for this slot
            if (benchVisuals.TryGetValue(slotIndex, out UnitVisual3D existing) && existing != null)
            {
                // Check if it's the same unit
                if (existing.ServerInstanceId == serverUnit.instanceId)
                {
                    // Ensure visual is active (may have been deactivated during combat)
                    if (!existing.gameObject.activeSelf)
                    {
                        existing.gameObject.SetActive(true);
                    }
                    // Unfreeze position (may have been frozen during combat)
                    existing.FreezePosition = false;

                    // Only update position if NOT away (away visuals are managed by combat)
                    // This prevents the sync from pulling visuals back to home during combat
                    if (!isAway && Vector3.Distance(existing.transform.position, worldPos) > 0.1f)
                    {
                        existing.SetPosition(worldPos);
                    }

                    // Always face camera when viewing from opposite side
                    // This handles initial sync, repositioning, and any other updates
                    if (viewFromOppositeSide)
                    {
                        existing.FaceCamera();
                    }

                    // Update stars if star level changed (e.g. after merge)
                    // Skip if this is a pending merge and we would be downgrading (wait for server confirmation)
                    if (existing.DisplayedStarLevel != serverUnit.starLevel)
                    {
                        bool wouldDowngrade = serverUnit.starLevel < existing.DisplayedStarLevel;
                        if (!wouldDowngrade || !IsPendingMerge(serverUnit.instanceId))
                        {
                            existing.UpdateStars(serverUnit.starLevel);
                        }
                    }
                    existing.SetServerItems(serverUnit.items);
                    return existing;
                }
                else
                {
                    // Different unit - check if it's a pending purchase being confirmed
                    bool isPendingVisual = existing.ServerInstanceId != null &&
                                           existing.ServerInstanceId.StartsWith("pending_");
                    bool isSameUnitType = existing.UnitId == serverUnit.unitId;

                    if (isPendingVisual && isSameUnitType)
                    {
                        // This is a pending purchase being confirmed - just update the instanceId
                        // Don't destroy/recreate to avoid flicker
                        existing.ServerInstanceId = serverUnit.instanceId;
                        existing.gameObject.name = $"BenchUnit_{serverUnit.name}_{slotIndex}";

                        // Update any other data that might have changed
                        if (existing.DisplayedStarLevel != serverUnit.starLevel)
                        {
                            existing.UpdateStars(serverUnit.starLevel);
                        }
                        existing.SetServerItems(serverUnit.items);
                        return existing;
                    }
                    else
                    {
                        // Actually a different unit - destroy old and create new
                        Destroy(existing.gameObject);
                        benchVisuals.Remove(slotIndex);
                    }
                }
            }

            // Create new visual at the appropriate position (home or away)
            UnitVisual3D visual = CreateUnitVisual(serverUnit, worldPos, false);
            if (visual != null)
            {
                visual.gameObject.name = $"BenchUnit_{serverUnit.name}_{slotIndex}";
                benchVisuals[slotIndex] = visual;

                // If we're away, also save the original position and lock rotation
                if (isAway)
                {
                    originalBenchPositions[slotIndex] = GetBenchSlotWorldPosition(slotIndex);
                    visual.LockRotation = true;
                }

                // Ensure new units face the camera when viewing from opposite side
                if (viewFromOppositeSide && !isAway)
                {
                    visual.FaceCamera();
                }
            }
            return visual;
        }

        /// <summary>
        /// Get an existing visual by instance ID (board or bench)
        /// </summary>
        public UnitVisual3D GetVisualByInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            // Check board visuals first
            if (boardVisuals.TryGetValue(instanceId, out UnitVisual3D boardVisual) && boardVisual != null)
            {
                return boardVisual;
            }

            // Check bench visuals
            foreach (var kvp in benchVisuals)
            {
                if (kvp.Value != null && kvp.Value.ServerInstanceId == instanceId)
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Get all board visuals (read-only)
        /// </summary>
        public IReadOnlyDictionary<string, UnitVisual3D> BoardVisuals => boardVisuals;

        /// <summary>
        /// Get all bench visuals (read-only)
        /// </summary>
        public IReadOnlyDictionary<int, UnitVisual3D> BenchVisuals => benchVisuals;

        /// <summary>
        /// Internal board visuals dictionary for drag operations (use with caution)
        /// </summary>
        internal Dictionary<string, UnitVisual3D> BoardVisualsInternal => boardVisuals;

        /// <summary>
        /// Internal bench visuals dictionary for drag operations (use with caution)
        /// </summary>
        internal Dictionary<int, UnitVisual3D> BenchVisualsInternal => benchVisuals;

        /// <summary>
        /// Mark a unit as having a pending optimistic merge.
        /// The star level won't be reverted by sync until server confirms.
        /// </summary>
        public void MarkPendingMerge(string instanceId)
        {
            if (!string.IsNullOrEmpty(instanceId))
                pendingMergeInstanceIds.Add(instanceId);
        }

        /// <summary>
        /// Clear all pending merges (called when server state is received)
        /// </summary>
        public void ClearPendingMerges()
        {
            pendingMergeInstanceIds.Clear();
        }

        /// <summary>
        /// Check if a unit has a pending merge (don't revert its star level)
        /// </summary>
        public bool IsPendingMerge(string instanceId)
        {
            return !string.IsNullOrEmpty(instanceId) && pendingMergeInstanceIds.Contains(instanceId);
        }

        /// <summary>
        /// Add or update a board visual (for drag operations)
        /// </summary>
        public void SetBoardVisual(string instanceId, UnitVisual3D visual)
        {
            if (string.IsNullOrEmpty(instanceId) || visual == null) return;
            boardVisuals[instanceId] = visual;
        }

        /// <summary>
        /// Add or update a bench visual (for drag operations)
        /// </summary>
        public void SetBenchVisual(int slotIndex, UnitVisual3D visual)
        {
            if (visual == null) return;
            benchVisuals[slotIndex] = visual;
        }

        /// <summary>
        /// Set visibility of all board visuals
        /// </summary>
        public void SetBoardVisualsVisible(bool visible)
        {
            // Don't hide visuals if this registry is away - they're being used on another board for combat
            if (!visible && isAway)
                return;

            foreach (var kvp in boardVisuals)
            {
                if (kvp.Value != null)
                {
                    // Skip hiding visuals that are being used for combat playback
                    if (!visible && kvp.Value.GetComponent<CombatUnitVisual>() != null)
                        continue;
                    kvp.Value.gameObject.SetActive(visible);
                    // When becoming visible again (e.g., after combat), face the camera
                    if (visible)
                    {
                        kvp.Value.FaceCamera();
                    }
                }
            }
        }

        /// <summary>
        /// Set visibility of all bench visuals
        /// </summary>
        public void SetBenchVisualsVisible(bool visible)
        {
            // Don't hide bench visuals if this registry is away - they're visible on the combat board
            if (!visible && isAway)
                return;

            foreach (var kvp in benchVisuals)
            {
                if (kvp.Value != null)
                {
                    // Skip hiding visuals that are being used for combat playback
                    if (!visible && kvp.Value.GetComponent<CombatUnitVisual>() != null)
                        continue;
                    kvp.Value.gameObject.SetActive(visible);
                    // When becoming visible again (e.g., after combat), face the camera
                    if (visible)
                    {
                        kvp.Value.FaceCamera();
                    }
                }
            }
        }

        /// <summary>
        /// Teleport all board and bench visuals to fight on another board (away combat)
        /// </summary>
        /// <param name="targetBoard">The board where combat will occur</param>
        public void TeleportToAwayPosition(HexBoard3D targetBoard)
        {
            if (targetBoard == null || isAway)
                return;

            isAway = true;
            awayTargetBoard = targetBoard;
            originalPositions.Clear();
            originalBenchPositions.Clear();

            // Rotation to face the away camera (180° from default)
            Quaternion flipRotation = Quaternion.Euler(0f, 180f, 0f);

            // Teleport board visuals
            foreach (var kvp in boardVisuals)
            {
                if (kvp.Value != null)
                {
                    // Save original position
                    originalPositions[kvp.Key] = kvp.Value.transform.position;
                    kvp.Value.LockRotation = true;
                    // Rotate to face the away camera
                    kvp.Value.transform.rotation = kvp.Value.transform.rotation * flipRotation;
                }
            }

            // Teleport bench visuals to away bench position
            foreach (var kvp in benchVisuals)
            {
                if (kvp.Value != null)
                {
                    // Save original position
                    originalBenchPositions[kvp.Key] = kvp.Value.transform.position;

                    // Move to away bench on target board
                    Vector3 awayBenchPos = GetAwayBenchSlotWorldPosition(targetBoard, kvp.Key);
                    kvp.Value.SetPosition(awayBenchPos);
                    kvp.Value.LockRotation = true;
                    // Rotate to face the away camera
                    kvp.Value.transform.rotation = kvp.Value.transform.rotation * flipRotation;
                }
            }
        }

        /// <summary>
        /// Return all teleported visuals back to their home board
        /// </summary>
        public void ReturnFromAway()
        {
            if (!isAway)
                return;

            // Return board visuals to original positions
            // Restore ALL visuals that have saved positions, regardless of active state
            // Dead unit visuals will be cleaned up by server state sync (SyncBoardVisuals)
            foreach (var kvp in boardVisuals)
            {
                if (kvp.Value != null)
                {
                    if (originalPositions.TryGetValue(kvp.Key, out Vector3 origPos))
                    {
                        kvp.Value.SetPosition(origPos);
                        kvp.Value.gameObject.SetActive(true);  // Reactivate in case it was hidden
                    }
                    kvp.Value.LockRotation = false;
                    kvp.Value.FreezePosition = false;
                    // Face the home camera
                    kvp.Value.FaceCamera();
                }
            }

            // Return bench visuals (bench units can't die in combat, so always restore)
            foreach (var kvp in benchVisuals)
            {
                if (kvp.Value != null)
                {
                    if (originalBenchPositions.TryGetValue(kvp.Key, out Vector3 origPos))
                    {
                        kvp.Value.SetPosition(origPos);
                    }
                    kvp.Value.LockRotation = false;
                    kvp.Value.FreezePosition = false;
                    kvp.Value.gameObject.SetActive(true);
                    // Face the home camera
                    kvp.Value.FaceCamera();
                }
            }

            isAway = false;
            awayTargetBoard = null;
            originalPositions.Clear();
            originalBenchPositions.Clear();
        }

        /// <summary>
        /// Remove a board visual by instance ID
        /// </summary>
        public void RemoveBoardVisual(string instanceId)
        {
            if (boardVisuals.TryGetValue(instanceId, out UnitVisual3D visual))
            {
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }
                boardVisuals.Remove(instanceId);
            }
        }

        /// <summary>
        /// Remove a bench visual by slot index
        /// </summary>
        public void RemoveBenchVisual(int slotIndex)
        {
            if (benchVisuals.TryGetValue(slotIndex, out UnitVisual3D visual))
            {
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }
                benchVisuals.Remove(slotIndex);
            }
        }

        /// <summary>
        /// Sync board visuals with server state - removes visuals for units no longer present
        /// </summary>
        /// <param name="currentInstanceIds">Set of instance IDs that should exist</param>
        public void SyncBoardVisuals(HashSet<string> currentInstanceIds)
        {
            List<string> toRemove = new List<string>();
            foreach (var kvp in boardVisuals)
            {
                if (!currentInstanceIds.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var instanceId in toRemove)
            {
                RemoveBoardVisual(instanceId);
            }
        }

        /// <summary>
        /// Sync bench visuals with server state - removes visuals for slots no longer occupied
        /// </summary>
        /// <param name="occupiedSlots">Set of slot indices that should have visuals</param>
        public void SyncBenchVisuals(HashSet<int> occupiedSlots)
        {
            List<int> toRemove = new List<int>();
            foreach (var kvp in benchVisuals)
            {
                if (!occupiedSlots.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var slot in toRemove)
            {
                RemoveBenchVisual(slot);
            }
        }

        /// <summary>
        /// Clear all visuals from this registry
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in boardVisuals)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            boardVisuals.Clear();

            foreach (var kvp in benchVisuals)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            benchVisuals.Clear();

            isAway = false;
            awayTargetBoard = null;
            originalPositions.Clear();
            originalBenchPositions.Clear();
        }

        /// <summary>
        /// Move a visual from board to bench
        /// </summary>
        public void MoveBoardToBench(string instanceId, int targetSlot)
        {
            if (!boardVisuals.TryGetValue(instanceId, out UnitVisual3D visual) || visual == null)
                return;

            // Remove from board tracking
            boardVisuals.Remove(instanceId);

            // Clear any existing bench visual at target slot
            RemoveBenchVisual(targetSlot);

            // Move and track as bench visual
            Vector3 benchPos = GetBenchSlotWorldPosition(targetSlot);
            visual.SetPositionAndFaceCamera(benchPos);
            benchVisuals[targetSlot] = visual;
        }

        /// <summary>
        /// Move a visual from bench to board
        /// </summary>
        public void MoveBenchToBoard(int slotIndex, string newInstanceId, int x, int y)
        {
            if (!benchVisuals.TryGetValue(slotIndex, out UnitVisual3D visual) || visual == null)
                return;

            // Remove from bench tracking
            benchVisuals.Remove(slotIndex);

            // Remove any existing board visual with same instance ID
            RemoveBoardVisual(newInstanceId);

            // Move and track as board visual
            Vector3 boardPos = hexBoard.GetTileWorldPosition(x, y);
            boardPos.y = unitYOffset;
            visual.SetPositionAndFaceCamera(boardPos);
            visual.ServerInstanceId = newInstanceId;
            boardVisuals[newInstanceId] = visual;
        }

        /// <summary>
        /// Swap two bench slots
        /// </summary>
        public void SwapBenchSlots(int slot1, int slot2)
        {
            benchVisuals.TryGetValue(slot1, out UnitVisual3D visual1);
            benchVisuals.TryGetValue(slot2, out UnitVisual3D visual2);

            // Use away positions if we're currently fighting on another board
            Vector3 pos1 = isAway && awayTargetBoard != null
                ? GetAwayBenchSlotWorldPosition(awayTargetBoard, slot1)
                : GetBenchSlotWorldPosition(slot1);
            Vector3 pos2 = isAway && awayTargetBoard != null
                ? GetAwayBenchSlotWorldPosition(awayTargetBoard, slot2)
                : GetBenchSlotWorldPosition(slot2);

            if (visual1 != null)
            {
                visual1.SetPositionAndFaceCamera(pos2);
                benchVisuals[slot2] = visual1;
            }
            else
            {
                benchVisuals.Remove(slot2);
            }

            if (visual2 != null)
            {
                visual2.SetPositionAndFaceCamera(pos1);
                benchVisuals[slot1] = visual2;
            }
            else
            {
                benchVisuals.Remove(slot1);
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// Create a UnitVisual3D from server data
        /// </summary>
        private UnitVisual3D CreateUnitVisual(ServerUnitData serverUnit, Vector3 worldPos, bool isEnemy)
        {
            var serverState = ServerGameState.Instance;
            if (serverState == null)
                return null;

            // Look up the unit template
            UnitData template = serverState.GetUnitTemplate(serverUnit.unitId);
            if (template == null)
            {
                Debug.LogWarning($"[BoardVisualRegistry] Could not find template for unit: {serverUnit.unitId}");
                return null;
            }

            // Create a temporary UnitInstance for visualization
            UnitInstance tempUnit = UnitInstance.Create(template, serverUnit.starLevel);

            // Create the visual
            GameObject visualObj = new GameObject($"Unit_{serverUnit.name}");
            visualObj.transform.SetParent(transform);
            visualObj.transform.position = worldPos;

            UnitVisual3D visual = visualObj.AddComponent<UnitVisual3D>();
            visual.Initialize(tempUnit, isEnemy);
            visual.SetPosition(worldPos);
            visual.ServerInstanceId = serverUnit.instanceId;
            visual.UnitId = serverUnit.unitId;
            visual.SetServerItems(serverUnit.items);

            // If viewing from opposite side, rotate unit to face that camera
            if (viewFromOppositeSide)
            {
                visual.transform.rotation = visual.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            }

            return visual;
        }

        /// <summary>
        /// Get world position for a bench slot (uses away position if currently away)
        /// </summary>
        public Vector3 GetBenchSlotWorldPosition(int index)
        {
            // Use away position if we're currently fighting on another board
            if (isAway && awayTargetBoard != null)
            {
                return GetAwayBenchSlotWorldPosition(awayTargetBoard, index);
            }

            if (hexBoard == null) return Vector3.zero;

            int benchSize = GameConstants.Player.BENCH_SIZE;
            float slotSpacing = 0.8f;
            float totalWidth = (benchSize - 1) * slotSpacing;

            // Reverse index if viewing from opposite side (camera at 180°)
            // This makes bench visually fill left-to-right from camera's perspective
            int visualIndex = viewFromOppositeSide ? (benchSize - 1 - index) : index;

            // Position behind the player's first row (negative Z from board center)
            Vector3 firstRowPos = hexBoard.GetTileWorldPosition(0, 0);
            float benchZ = firstRowPos.z - 1.5f;

            // Center horizontally on board
            float startX = hexBoard.transform.position.x - totalWidth / 2f;
            float x = startX + visualIndex * slotSpacing;

            return new Vector3(x, unitYOffset, benchZ);
        }

        /// <summary>
        /// Get world position for an away bench slot (behind row 7 on target board)
        /// Camera views from 180° when away, so reverse index for left-to-right visual order
        /// </summary>
        private Vector3 GetAwayBenchSlotWorldPosition(HexBoard3D targetBoard, int index)
        {
            if (targetBoard == null) return Vector3.zero;

            int benchSize = GameConstants.Player.BENCH_SIZE;
            float slotSpacing = 0.8f;
            float totalWidth = (benchSize - 1) * slotSpacing;

            // Reverse index since camera views from opposite side (180°)
            // This makes bench visually fill left-to-right from camera's perspective
            int visualIndex = benchSize - 1 - index;

            // Position behind the enemy's last row (positive Z from board center)
            int lastRow = GameConstants.Grid.HEIGHT * 2 - 1;
            Vector3 lastRowPos = targetBoard.GetTileWorldPosition(0, lastRow);
            float benchZ = lastRowPos.z + 1.5f;

            // Center horizontally on target board
            float startX = targetBoard.transform.position.x - totalWidth / 2f;
            float x = startX + visualIndex * slotSpacing;

            return new Vector3(x, unitYOffset, benchZ);
        }

        /// <summary>
        /// Find which bench slot a world position corresponds to
        /// </summary>
        public int GetBenchSlotAtWorldPosition(Vector3 worldPos)
        {
            int benchSize = GameConstants.Player.BENCH_SIZE;
            float snapRadius = 1.2f;
            float closestDist = float.MaxValue;
            int closestSlot = -1;

            for (int i = 0; i < benchSize; i++)
            {
                // Use away position if we're currently fighting on another board
                Vector3 slotPos = isAway && awayTargetBoard != null
                    ? GetAwayBenchSlotWorldPosition(awayTargetBoard, i)
                    : GetBenchSlotWorldPosition(i);
                float dist = Vector2.Distance(
                    new Vector2(worldPos.x, worldPos.z),
                    new Vector2(slotPos.x, slotPos.z)
                );
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestSlot = i;
                }
            }

            return closestDist < snapRadius ? closestSlot : -1;
        }

        /// <summary>
        /// Check if a world position is in the bench drop area
        /// </summary>
        public bool IsBenchDropArea(Vector3 worldPos)
        {
            int benchSize = GameConstants.Player.BENCH_SIZE;
            for (int i = 0; i < benchSize; i++)
            {
                // Use away position if we're currently fighting on another board
                Vector3 slotPos = isAway && awayTargetBoard != null
                    ? GetAwayBenchSlotWorldPosition(awayTargetBoard, i)
                    : GetBenchSlotWorldPosition(i);
                float dist = Vector2.Distance(
                    new Vector2(worldPos.x, worldPos.z),
                    new Vector2(slotPos.x, slotPos.z)
                );
                if (dist < 1.2f)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
