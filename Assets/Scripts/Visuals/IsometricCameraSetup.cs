using UnityEngine;
using UnityEngine.EventSystems;

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

        [Header("Responsive Sizing")]
        [Tooltip("Minimum world width that must be visible (bench width + padding)")]
        public float minVisibleWidth = 7f;
        [Tooltip("Minimum world height that must be visible (board + bench depth)")]
        public float minVisibleHeight = 10f;

        [Header("Pan & Zoom")]
        public bool enablePan = false;   // Disabled for now
        public bool enableZoom = true;   // Scroll wheel (PC) and pinch (mobile)
        public float panSpeed = 0.5f;
        public float zoomSpeed = 2f;
        public float minOrthoSize = 4f;
        public float maxOrthoSize = 14f; // Enough to see cosmetic slots on narrow screens

        [Header("Multi-Board Navigation")]
        [Tooltip("Speed of camera transition between boards")]
        public float boardTransitionSpeed = 5f;
        [Tooltip("Enable keyboard shortcuts to switch boards (1-4)")]
        public bool enableBoardSwitching = true;

        // External control to temporarily disable input
        public bool inputBlocked = false;

        // Board navigation state
        private HexBoard3D currentBoard;
        private HexBoard3D targetBoard;
        private Vector3 transitionStartPos;
        private Vector3 transitionTargetPos;
        private float transitionProgress = 1f;
        private bool isTransitioning = false;
        private bool isViewingFromOppositeSide = false;
        private float startRotationY = 0f;
        private float targetRotationY = 0f;
        private float rotationTransitionProgress = 1f;

        private Camera cam;
        private Vector3 lastMousePos;
        private bool isPanning;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private float baseOrthoSize; // Minimum size needed for gameplay visibility
        private bool userHasZoomed = false;

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

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            SetupCamera();
        }

        private void Update()
        {
            // Check for screen size changes (device rotation, window resize)
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
                AdjustForAspectRatio();
            }

            // Handle board transition
            if (isTransitioning)
            {
                transitionProgress += Time.deltaTime * boardTransitionSpeed;
                if (transitionProgress >= 1f)
                {
                    transitionProgress = 1f;
                    isTransitioning = false;
                    focusPoint = transitionTargetPos;
                    currentBoard = targetBoard;
                }
                else
                {
                    // Smooth ease-in-out interpolation
                    float t = transitionProgress;
                    float easedT = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                    focusPoint = Vector3.Lerp(transitionStartPos, transitionTargetPos, easedT);
                }
                UpdateCameraPosition();
            }

            // Handle rotation transition (for opposite side viewing)
            if (rotationTransitionProgress < 1f)
            {
                rotationTransitionProgress += Time.deltaTime * boardTransitionSpeed;
                if (rotationTransitionProgress >= 1f)
                {
                    rotationTransitionProgress = 1f;
                    rotationY = targetRotationY;
                }
                else
                {
                    float t = rotationTransitionProgress;
                    float easedT = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                    rotationY = Mathf.LerpAngle(startRotationY, targetRotationY, easedT);
                }
                UpdateCameraPosition();
            }

            HandleInput();
            HandleBoardSwitchInput();
        }

        /// <summary>
        /// Configure the camera for isometric view
        /// </summary>
        public void SetupCamera()
        {
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);

            AdjustForAspectRatio();
            UpdateCameraPosition();
        }

        /// <summary>
        /// Adjust camera ortho size to ensure minimum visible area fits on screen
        /// This ensures the bench is always fully visible regardless of aspect ratio
        /// Respects user zoom if they've zoomed out beyond the minimum
        /// </summary>
        public void AdjustForAspectRatio()
        {
            if (cam == null) return;

            float aspectRatio = (float)Screen.width / Screen.height;

            // For orthographic camera:
            // - Vertical visible height = 2 * orthoSize
            // - Horizontal visible width = 2 * orthoSize * aspectRatio

            // Account for camera angle - when tilted, we see more depth compressed
            float angleCompensation = 1f / Mathf.Cos(rotationX * Mathf.Deg2Rad);
            float effectiveMinHeight = minVisibleHeight * angleCompensation;

            // Calculate required orthoSize to fit width
            float orthoForWidth = minVisibleWidth / (2f * aspectRatio);

            // Calculate required orthoSize to fit height
            float orthoForHeight = effectiveMinHeight / 2f;

            // Use the larger value to ensure both fit
            float requiredOrtho = Mathf.Max(orthoForWidth, orthoForHeight);

            // Clamp to allowed range
            baseOrthoSize = Mathf.Clamp(requiredOrtho, minOrthoSize, maxOrthoSize);

            // If user hasn't manually zoomed, or if they're too zoomed in for gameplay,
            // adjust to the base size. Otherwise preserve their zoom level.
            if (!userHasZoomed || orthoSize < baseOrthoSize)
            {
                orthoSize = baseOrthoSize;
            }

            cam.orthographicSize = orthoSize;
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

            // Update min visible dimensions based on board size (with padding for bench)
            minVisibleWidth = Mathf.Max(minVisibleWidth, boardSize.x + 2f);
            minVisibleHeight = Mathf.Max(minVisibleHeight, boardSize.z + 4f); // Extra for bench behind

            // Use aspect-ratio-aware sizing
            AdjustForAspectRatio();
            UpdateCameraPosition();
        }

        private void HandleInput()
        {
            // Skip all input if blocked (e.g., during unit dragging)
            if (inputBlocked) return;

            // Skip input if pointer is over UI (prevents scroll affecting camera when scrolling dropdowns)
            bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // Zoom with scroll wheel (PC) - skip if over UI
            if (enableZoom && !isOverUI)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    userHasZoomed = true;
                    orthoSize -= scroll * zoomSpeed;
                    orthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
                    cam.orthographicSize = orthoSize;
                }
            }

            // Pan with middle mouse or right mouse + drag - don't start pan when over UI
            if (enablePan)
            {
                if ((Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1)) && !isOverUI)
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
                if (Mathf.Abs(delta) > 0.01f)
                {
                    userHasZoomed = true;
                    orthoSize += delta * zoomSpeed * 0.01f;
                    orthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
                    cam.orthographicSize = orthoSize;
                }
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
            userHasZoomed = false;
            focusPoint = Vector3.zero;
            AdjustForAspectRatio(); // Recalculate proper size
            UpdateCameraPosition();
        }

        /// <summary>
        /// Reset zoom to default gameplay level (e.g., when match starts)
        /// </summary>
        public void ResetZoom()
        {
            userHasZoomed = false;
            AdjustForAspectRatio();
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

        /// <summary>
        /// Handle keyboard input for switching between boards
        /// </summary>
        private void HandleBoardSwitchInput()
        {
            if (!enableBoardSwitching || inputBlocked) return;

            var allBoards = HexBoard3D.AllBoards;
            if (allBoards == null || allBoards.Count <= 1) return;

            // Number keys 1-4 to switch boards
            if (Input.GetKeyDown(KeyCode.Alpha1) && allBoards.Count > 0)
                FocusOnBoard(allBoards[0]);
            else if (Input.GetKeyDown(KeyCode.Alpha2) && allBoards.Count > 1)
                FocusOnBoard(allBoards[1]);
            else if (Input.GetKeyDown(KeyCode.Alpha3) && allBoards.Count > 2)
                FocusOnBoard(allBoards[2]);
            else if (Input.GetKeyDown(KeyCode.Alpha4) && allBoards.Count > 3)
                FocusOnBoard(allBoards[3]);

            // Tab to cycle through boards
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                CycleToNextBoard();
            }

            // Home/H to return to player's board
            if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.H))
            {
                FocusOnPlayerBoard();
            }
        }

        /// <summary>
        /// Focus camera on a specific board with smooth transition
        /// </summary>
        public void FocusOnBoard(HexBoard3D board)
        {
            FocusOnBoard(board, false);
        }

        /// <summary>
        /// Focus camera on a specific board with smooth transition
        /// </summary>
        /// <param name="board">The board to focus on</param>
        /// <param name="fromOppositeSide">If true, view from the far side (180 degree rotation)</param>
        public void FocusOnBoard(HexBoard3D board, bool fromOppositeSide)
        {
            if (board == null) return;

            targetBoard = board;
            transitionStartPos = focusPoint;
            // Offset toward enemy side (away from camera) for better view
            // Flip the offset direction when viewing from opposite side
            float zOffset = fromOppositeSide ? -2f : 2f;
            transitionTargetPos = board.BoardCenter + new Vector3(0, 0, zOffset);
            transitionProgress = 0f;
            isTransitioning = true;

            // Handle rotation for opposite side viewing
            isViewingFromOppositeSide = fromOppositeSide;
            startRotationY = rotationY;
            if (fromOppositeSide)
            {
                targetRotationY = 180f;
            }
            else
            {
                targetRotationY = 0f;
            }
            rotationTransitionProgress = 0f;

            Debug.Log($"[Camera] Transitioning to board: {board.boardLabel} (opposite side: {fromOppositeSide})");
        }

        /// <summary>
        /// Focus camera on a specific board from the opposite/far side
        /// </summary>
        public void FocusOnBoardFromOppositeSide(HexBoard3D board)
        {
            FocusOnBoard(board, true);
        }

        /// <summary>
        /// Focus on the player's board
        /// </summary>
        public void FocusOnPlayerBoard()
        {
            var playerBoard = HexBoard3D.Instance;
            if (playerBoard != null)
            {
                FocusOnBoard(playerBoard);
            }
        }

        /// <summary>
        /// Cycle to the next board in the list
        /// </summary>
        public void CycleToNextBoard()
        {
            var allBoards = HexBoard3D.AllBoards;
            if (allBoards == null || allBoards.Count <= 1) return;

            int currentIndex = currentBoard != null ? allBoards.IndexOf(currentBoard) : -1;
            int nextIndex = (currentIndex + 1) % allBoards.Count;
            FocusOnBoard(allBoards[nextIndex]);
        }

        /// <summary>
        /// Get the currently focused board
        /// </summary>
        public HexBoard3D GetCurrentBoard()
        {
            return currentBoard ?? HexBoard3D.Instance;
        }

        /// <summary>
        /// Check if camera is currently transitioning between boards
        /// </summary>
        public bool IsTransitioning => isTransitioning;

        /// <summary>
        /// Focus camera on the merchant area with smooth transition
        /// Uses a standard front-facing view (rotationY = 0)
        /// </summary>
        /// <param name="merchantCenter">Center position of the merchant area</param>
        public void FocusOnMerchantArea(Vector3 merchantCenter)
        {
            targetBoard = null; // Not focusing on a board
            transitionStartPos = focusPoint;
            transitionTargetPos = merchantCenter + new Vector3(0, 0, 1f); // Slight offset for better view
            transitionProgress = 0f;
            isTransitioning = true;

            // Standard front-facing view for merchant area
            startRotationY = rotationY;
            targetRotationY = 0f;
            rotationTransitionProgress = 0f;
            isViewingFromOppositeSide = false;

            Debug.Log($"[Camera] Transitioning to merchant area at {merchantCenter}");
        }
    }
}