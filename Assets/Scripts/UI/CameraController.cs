using UnityEngine;
using Crestforge.Core;

namespace Crestforge.UI
{
    /// <summary>
    /// Sets up the camera to show the hex grid properly positioned
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Grid View Settings")]
        public float gridCameraSize = 3.5f;
        
        private Camera mainCamera;
        
        private void Awake()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = GetComponent<Camera>();
            }
        }

        private void Start()
        {
            SetupCamera();
        }

        private void Update()
        {
            // Adjust camera based on screen orientation
            if (Screen.height > Screen.width)
            {
                // Portrait mode - move camera down so battlefield appears higher on screen
                float gridCenterX = 1.5f;
                float gridCenterY = -0.3f; // Lower camera Y = battlefield appears higher
                mainCamera.transform.position = new Vector3(gridCenterX, gridCenterY, -10f);
                mainCamera.orthographicSize = 3.8f;
            }
            else
            {
                // Landscape mode
                float gridCenterX = 1.5f;
                float gridCenterY = 1.5f;
                mainCamera.transform.position = new Vector3(gridCenterX, gridCenterY, -10f);
                mainCamera.orthographicSize = gridCameraSize;
            }
        }

        private void SetupCamera()
        {
            if (mainCamera == null) return;
            mainCamera.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        }
    }
}