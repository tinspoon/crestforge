using UnityEngine;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Sets up and controls an isometric camera for the game board.
    /// Uses orthographic projection with a classic isometric angle.
    /// </summary>
    public class IsometricCameraSetup : MonoBehaviour
    {
        public static IsometricCameraSetup Instance { get; private set; }

        [Header("Camera Settings")]
        [Tooltip("Orthographic size (zoom level)")]
        public float orthoSize = 8f;
        
        [Tooltip("Camera rotation around Y axis (0 = behind player)")]
        public float rotationY = 0f;
        
        [Tooltip("Camera pitch angle")]
        public float rotationX = 45f;
        
        [Tooltip("Distance from board center")]
        public float distance = 15f;

        [Header("Board Focus")]
        [Tooltip("Center point the camera looks at")]
        public Vector3 focusPoint = new Vector3(0, 0, 0);

        [Header("Pan & Zoom")]
        public bool enablePan = true;
        public bool enableZoom = true;
        public float panSpeed = 0.5f;
        public float zoomSpeed = 2f;
        public float minOrthoSize = 4f;
        public float maxOrthoSize = 15f;

        // External control to temporarily disable input
        public bool inputBlocked = false;

        private Camera cam;
        private Vector3 lastMousePos;
        private bool isPanning;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = gameObject.AddComponent<Camera>();
            }

            SetupCamera();
        }

        private void Update()
        {
            HandleInput();
        }

        /// <summary>
        /// Configure the camera for isometric view
        /// </summary>
        public void SetupCamera()
        {
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);

            UpdateCameraPosition();
        }

        /// <summary>
        /// Update camera position based on rotation and distance settings
        /// </summary>
        public void UpdateCameraPosition()
        {
            // Calculate camera position based on spherical coordinates
            Quaternion rotation = Quaternion.Euler(rotationX, rotationY, 0);
            Vector3 offset = rotation * new Vector3(0, 0, -distance);
            
            transform.position = focusPoint + offset;
            transform.LookAt(focusPoint);
        }

        /// <summary>
        /// Center the camera on the board
        /// </summary>
        public void CenterOnBoard(Vector3 boardCenter, Vector3 boardSize)
        {
            // Focus on board center, shifted slightly forward to show whole board
            focusPoint = boardCenter + new Vector3(0, 0, boardSize.z * 0.2f);
            
            // Adjust ortho size to fit entire board with padding
            float maxDimension = Mathf.Max(boardSize.x, boardSize.z * 1.5f);
            orthoSize = maxDimension * 0.6f;
            orthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
            
            Debug.Log($"[IsometricCamera] CenterOnBoard: boardSize={boardSize}, focusPoint={focusPoint}, orthoSize={orthoSize}");
            
            cam.orthographicSize = orthoSize;
            UpdateCameraPosition();
        }

        private void HandleInput()
        {
            // Skip all input if blocked (e.g., during unit dragging)
            if (inputBlocked) return;

            // Zoom with scroll wheel
            if (enableZoom)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    orthoSize -= scroll * zoomSpeed;
                    orthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
                    cam.orthographicSize = orthoSize;
                }
            }

            // Pan with middle mouse or right mouse + drag
            if (enablePan)
            {
                if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1))
                {
                    isPanning = true;
                    lastMousePos = Input.mousePosition;
                }
                
                if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
                {
                    isPanning = false;
                }

                if (isPanning)
                {
                    Vector3 delta = Input.mousePosition - lastMousePos;
                    
                    // Convert screen delta to world movement
                    Vector3 right = transform.right;
                    Vector3 up = Vector3.Cross(right, Vector3.up).normalized;
                    
                    // Invert and scale movement
                    Vector3 movement = (-right * delta.x - up * delta.y) * panSpeed * orthoSize * 0.01f;
                    focusPoint += movement;
                    
                    UpdateCameraPosition();
                    lastMousePos = Input.mousePosition;
                }
            }

            // Touch pan (mobile)
            if (enablePan && Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved)
                {
                    Vector3 right = transform.right;
                    Vector3 up = Vector3.Cross(right, Vector3.up).normalized;
                    
                    Vector3 movement = (-right * touch.deltaPosition.x - up * touch.deltaPosition.y) * panSpeed * orthoSize * 0.005f;
                    focusPoint += movement;
                    
                    UpdateCameraPosition();
                }
            }

            // Pinch to zoom (mobile)
            if (enableZoom && Input.touchCount == 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                Vector2 t0Prev = t0.position - t0.deltaPosition;
                Vector2 t1Prev = t1.position - t1.deltaPosition;

                float prevDist = (t0Prev - t1Prev).magnitude;
                float currDist = (t0.position - t1.position).magnitude;

                float delta = prevDist - currDist;
                orthoSize += delta * zoomSpeed * 0.01f;
                orthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
                cam.orthographicSize = orthoSize;
            }
        }

        /// <summary>
        /// Rotate the camera view (for optional rotation controls)
        /// </summary>
        public void RotateView(float deltaY)
        {
            rotationY += deltaY;
            rotationY = rotationY % 360f;
            UpdateCameraPosition();
        }

        /// <summary>
        /// Reset camera to default position
        /// </summary>
        public void ResetView()
        {
            rotationY = 45f;
            rotationX = 30f;
            orthoSize = 6f;
            focusPoint = Vector3.zero;
            cam.orthographicSize = orthoSize;
            UpdateCameraPosition();
        }

        /// <summary>
        /// Convert screen position to world position on the ground plane (Y=0)
        /// </summary>
        public Vector3 ScreenToGroundPoint(Vector3 screenPos)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            
            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            
            return Vector3.zero;
        }
    }
}