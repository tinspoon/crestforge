using UnityEngine;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Assigns the UnitModelDatabase to UnitVisual3D at runtime.
    /// Add this component to a scene object (like GameManager) and assign the database.
    /// </summary>
    public class UnitModelConfig : MonoBehaviour
    {
        [Header("Model Database")]
        [Tooltip("Drag the UnitModelDatabase asset here")]
        public UnitModelDatabase modelDatabase;

        private void Awake()
        {
            if (modelDatabase != null)
            {
                UnitVisual3D.modelDatabase = modelDatabase;
                Debug.Log($"UnitModelDatabase assigned with {modelDatabase.unitModels.Count} entries");
            }
            else
            {
                Debug.LogWarning("UnitModelConfig: No model database assigned. Units will use procedural visuals.");
            }
        }

        private void OnValidate()
        {
            // Also assign in editor for preview purposes
            if (modelDatabase != null)
            {
                UnitVisual3D.modelDatabase = modelDatabase;
            }
        }
    }
}
