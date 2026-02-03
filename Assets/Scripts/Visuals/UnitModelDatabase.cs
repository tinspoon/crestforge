using UnityEngine;
using System.Collections.Generic;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Database mapping unit names to custom 3D model prefabs.
    /// Create via Assets > Create > Crestforge > Unit Model Database
    /// </summary>
    [CreateAssetMenu(fileName = "UnitModelDatabase", menuName = "Crestforge/Unit Model Database")]
    public class UnitModelDatabase : ScriptableObject
    {
        [System.Serializable]
        public class UnitModelEntry
        {
            [Tooltip("Unit name (must match UnitData.unitName exactly)")]
            public string unitName;

            [Tooltip("3D model prefab for this unit")]
            public GameObject modelPrefab;

            [Tooltip("Scale multiplier for the model (default 1)")]
            public float scale = 1f;

            [Tooltip("Y offset from ground (adjust if model floats or sinks)")]
            public float yOffset = 0f;

            [Tooltip("Rotation offset in degrees")]
            public Vector3 rotationOffset = Vector3.zero;

            [Header("Animation Clip Names (leave empty for defaults)")]
            [Tooltip("Idle animation clip name")]
            public string idleClip;

            [Tooltip("Walk/move animation clip name")]
            public string walkClip;

            [Tooltip("Attack animation clip name")]
            public string attackClip;

            [Tooltip("Hit/hurt animation clip name")]
            public string hitClip;

            [Tooltip("Death animation clip name")]
            public string deathClip;

            [Tooltip("Victory/celebration animation clip name")]
            public string victoryClip;

            [Header("Animation Speed")]
            [Tooltip("Speed multiplier for attack animation (1 = normal, 2 = twice as fast)")]
            [Range(0.5f, 4f)]
            public float attackAnimSpeed = 1f;
        }

        [Header("Unit Models")]
        [Tooltip("Map unit names to their 3D model prefabs")]
        public List<UnitModelEntry> unitModels = new List<UnitModelEntry>();

        [Header("Fallback Models")]
        [Tooltip("Default model for units without a specific model (optional)")]
        public GameObject defaultPlayerModel;

        [Tooltip("Default model for enemy units without a specific model (optional)")]
        public GameObject defaultEnemyModel;

        [Header("Animation")]
        [Tooltip("Animator Controller to use for all unit models")]
        public RuntimeAnimatorController animatorController;

        // Runtime lookup cache
        private Dictionary<string, UnitModelEntry> modelLookup;

        /// <summary>
        /// Get model entry for a unit by name
        /// </summary>
        public UnitModelEntry GetModelEntry(string unitName)
        {
            // Build cache if needed
            if (modelLookup == null)
            {
                modelLookup = new Dictionary<string, UnitModelEntry>();
                foreach (var entry in unitModels)
                {
                    if (!string.IsNullOrEmpty(entry.unitName) && entry.modelPrefab != null)
                    {
                        modelLookup[entry.unitName.ToLower()] = entry;
                    }
                }
            }

            if (modelLookup.TryGetValue(unitName.ToLower(), out UnitModelEntry found))
            {
                return found;
            }

            return null;
        }

        /// <summary>
        /// Check if a unit has a custom model assigned
        /// </summary>
        public bool HasCustomModel(string unitName)
        {
            return GetModelEntry(unitName) != null;
        }

        /// <summary>
        /// Instantiate a model for a unit
        /// </summary>
        public GameObject InstantiateModel(string unitName, Transform parent, bool isEnemy)
        {
            var entry = GetModelEntry(unitName);

            GameObject prefab = null;
            float scale = 1f;
            float yOffset = 0f;
            Vector3 rotationOffset = Vector3.zero;

            if (entry != null)
            {
                prefab = entry.modelPrefab;
                scale = entry.scale;
                yOffset = entry.yOffset;
                rotationOffset = entry.rotationOffset;
            }
            else
            {
                // Use fallback
                prefab = isEnemy ? defaultEnemyModel : defaultPlayerModel;
            }

            if (prefab == null)
            {
                return null; // No model available, will use procedural fallback
            }

            GameObject model = Instantiate(prefab, parent);
            model.name = "CustomModel";
            model.transform.localPosition = new Vector3(0, yOffset, 0);
            model.transform.localScale = Vector3.one * scale;
            model.transform.localRotation = Quaternion.Euler(rotationOffset);

            return model;
        }

        /// <summary>
        /// Clear the lookup cache (call after modifying the list at runtime)
        /// </summary>
        public void ClearCache()
        {
            modelLookup = null;
        }

        private void OnValidate()
        {
            // Clear cache when edited in inspector
            modelLookup = null;
        }
    }
}
