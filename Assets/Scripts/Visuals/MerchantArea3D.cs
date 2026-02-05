using System.Collections.Generic;
using UnityEngine;
using Crestforge.Networking;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Manages the 3D Mad Merchant area where players pick items turn by turn.
    /// Creates a 4x3 grid of pedestals with items, handles camera transitions,
    /// and synchronizes picks across all clients.
    /// </summary>
    public class MerchantArea3D : MonoBehaviour
    {
        public static MerchantArea3D Instance { get; private set; }

        [Header("Layout Settings")]
        public int columns = 3; // 3 columns for 6 pairs (2 rows of 3)
        public int rows = 2;
        public float pedestalSpacing = 2.5f; // Increased spacing for paired items
        public Vector3 areaOffset = new Vector3(0, 0, 5f); // Position in front of boards

        [Header("Visual Settings")]
        public Color groundColor = new Color(0.25f, 0.22f, 0.2f);
        public float groundSize = 10f;

        // Runtime state
        private List<MerchantPedestal> pedestals = new List<MerchantPedestal>();
        private string currentPickerId;
        private bool isMyTurn;
        private GameObject groundPlane;
        private GameObject merchantTitle;
        private TextMesh turnIndicator;

        // Cached data
        private MerchantStartMessage merchantData;
        private string localPlayerId;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private bool eventsSubscribed = false;

        private void Start()
        {
            // Get local player ID (may be null on startup, will be set in Update)
            localPlayerId = NetworkManager.Instance?.clientId;

            // Subscribe to merchant events (will retry in Update if NetworkManager not ready)
            SubscribeToEvents();

            // Start hidden (no visuals created yet)
        }

        private void SubscribeToEvents()
        {
            if (NetworkManager.Instance == null)
            {
                // Will retry in Update() - this is normal on startup
                return;
            }

            if (eventsSubscribed)
            {
                return;
            }

            // Subscribe to events
            NetworkManager.Instance.OnMerchantStart += HandleMerchantStart;
            NetworkManager.Instance.OnMerchantTurnUpdate += HandleTurnUpdate;
            NetworkManager.Instance.OnMerchantPick += HandlePick;
            NetworkManager.Instance.OnMerchantEnd += HandleMerchantEnd;

            eventsSubscribed = true;
            Debug.Log("[MerchantArea3D] Subscribed to merchant events");
        }

        private void Update()
        {
            // If we haven't subscribed yet, keep trying
            if (!eventsSubscribed && NetworkManager.Instance != null)
            {
                localPlayerId = NetworkManager.Instance.clientId;
                SubscribeToEvents();
            }
        }

        private void OnDestroy()
        {
            Debug.Log("[MerchantArea3D] OnDestroy called");

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnMerchantStart -= HandleMerchantStart;
                NetworkManager.Instance.OnMerchantTurnUpdate -= HandleTurnUpdate;
                NetworkManager.Instance.OnMerchantPick -= HandlePick;
                NetworkManager.Instance.OnMerchantEnd -= HandleMerchantEnd;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Initialize the merchant area with data from server
        /// </summary>
        public void Initialize(MerchantStartMessage data)
        {
            merchantData = data;
            currentPickerId = data.currentPickerId;
            isMyTurn = !string.IsNullOrEmpty(localPlayerId) && (currentPickerId == localPlayerId);

            Debug.Log($"[MerchantArea3D] Initializing with {data.options?.Count ?? 0} options, isMyTurn={isMyTurn}, currentPickerId={currentPickerId}, localPlayerId={localPlayerId}");

            // Clear any existing pedestals
            ClearPedestals();

            // Create ground plane
            CreateGround();

            // Create title
            CreateTitle();

            // Create turn indicator
            CreateTurnIndicator(data.currentPickerName);

            // Create pedestals in 4x3 grid
            CreatePedestals(data.options);

            // Update highlight based on current picker
            UpdateHighlights();
        }

        private void CreateGround()
        {
            if (groundPlane != null)
            {
                Destroy(groundPlane);
            }

            groundPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            groundPlane.name = "MerchantGround";
            groundPlane.transform.SetParent(transform);
            groundPlane.transform.localPosition = new Vector3(0, -0.01f, 0);
            groundPlane.transform.localRotation = Quaternion.Euler(90, 0, 0);
            groundPlane.transform.localScale = new Vector3(groundSize, groundSize, 1);

            // Remove collider
            Destroy(groundPlane.GetComponent<Collider>());

            // Create dark ground material
            var renderer = groundPlane.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = groundColor;
            renderer.material = mat;
        }

        private void CreateTitle()
        {
            if (merchantTitle != null)
            {
                Destroy(merchantTitle);
            }

            merchantTitle = new GameObject("MerchantTitle");
            merchantTitle.transform.SetParent(transform);

            // Position above the pedestals
            float gridWidth = (columns - 1) * pedestalSpacing;
            merchantTitle.transform.localPosition = new Vector3(0, 2.5f, rows * pedestalSpacing / 2 + 0.5f);

            var titleText = merchantTitle.AddComponent<TextMesh>();
            titleText.text = "MAD MERCHANT";
            titleText.fontSize = 100;
            titleText.characterSize = 0.06f;
            titleText.anchor = TextAnchor.MiddleCenter;
            titleText.alignment = TextAlignment.Center;
            titleText.color = new Color(1f, 0.85f, 0.4f);

            // Billboard
            merchantTitle.AddComponent<BillboardUI>();
        }

        private void CreateTurnIndicator(string currentPickerName)
        {
            var indicatorObj = new GameObject("TurnIndicator");
            indicatorObj.transform.SetParent(transform);

            // Position below title
            indicatorObj.transform.localPosition = new Vector3(0, 2f, rows * pedestalSpacing / 2 + 0.5f);

            turnIndicator = indicatorObj.AddComponent<TextMesh>();
            turnIndicator.fontSize = 100;
            turnIndicator.characterSize = 0.04f;
            turnIndicator.anchor = TextAnchor.MiddleCenter;
            turnIndicator.alignment = TextAlignment.Center;

            UpdateTurnIndicatorText(currentPickerName);

            // Billboard
            indicatorObj.AddComponent<BillboardUI>();
        }

        private void UpdateTurnIndicatorText(string pickerName)
        {
            if (turnIndicator == null) return;

            if (isMyTurn)
            {
                turnIndicator.text = "YOUR TURN - Pick an item!";
                turnIndicator.color = new Color(0.4f, 1f, 0.5f);
            }
            else
            {
                turnIndicator.text = $"Waiting for {pickerName}...";
                turnIndicator.color = new Color(0.8f, 0.8f, 0.8f);
            }
        }

        private void CreatePedestals(List<MerchantOptionData> options)
        {
            if (options == null || options.Count == 0)
            {
                Debug.LogWarning("[MerchantArea3D] No options provided");
                return;
            }

            // Calculate grid start position (centered)
            float gridWidth = (columns - 1) * pedestalSpacing;
            float gridDepth = (rows - 1) * pedestalSpacing;
            float startX = -gridWidth / 2;
            float startZ = -gridDepth / 2;

            int optionIndex = 0;
            for (int row = 0; row < rows && optionIndex < options.Count; row++)
            {
                for (int col = 0; col < columns && optionIndex < options.Count; col++)
                {
                    var option = options[optionIndex];

                    // Create pedestal
                    var pedestalObj = new GameObject($"Pedestal_{option.optionId}");
                    pedestalObj.transform.SetParent(transform);
                    pedestalObj.transform.localPosition = new Vector3(
                        startX + col * pedestalSpacing,
                        0,
                        startZ + row * pedestalSpacing
                    );

                    var pedestal = pedestalObj.AddComponent<MerchantPedestal>();
                    pedestal.Initialize(option, this);
                    pedestals.Add(pedestal);

                    optionIndex++;
                }
            }

            Debug.Log($"[MerchantArea3D] Created {pedestals.Count} pedestals");
        }

        private void ClearPedestals()
        {
            foreach (var pedestal in pedestals)
            {
                if (pedestal != null)
                {
                    Destroy(pedestal.gameObject);
                }
            }
            pedestals.Clear();
        }

        /// <summary>
        /// Update which picker is current
        /// </summary>
        public void SetCurrentPicker(string pickerId, string pickerName)
        {
            currentPickerId = pickerId;
            isMyTurn = !string.IsNullOrEmpty(localPlayerId) && (pickerId == localPlayerId);

            UpdateTurnIndicatorText(pickerName);
            UpdateHighlights();

            Debug.Log($"[MerchantArea3D] Turn update: {pickerName}, isMyTurn={isMyTurn}, pickerId={pickerId}, localPlayerId={localPlayerId}");
        }

        /// <summary>
        /// Mark an option as picked
        /// </summary>
        public void MarkOptionPicked(string optionId, string pickerName)
        {
            var pedestal = pedestals.Find(p => p.optionId == optionId);
            if (pedestal != null)
            {
                pedestal.SetPicked(pickerName);
                Debug.Log($"[MerchantArea3D] Option {optionId} picked by {pickerName}");
            }
        }

        private void UpdateHighlights()
        {
            foreach (var pedestal in pedestals)
            {
                if (pedestal != null && !pedestal.isPicked)
                {
                    pedestal.SetHighlighted(isMyTurn);
                }
            }
        }

        /// <summary>
        /// Show the merchant area and transition camera
        /// </summary>
        public void Show()
        {
            // Transition camera to merchant area
            var camera = IsometricCameraSetup.Instance;
            if (camera != null)
            {
                camera.FocusOnMerchantArea(transform.position);
            }

            Debug.Log("[MerchantArea3D] Showing merchant area");
        }

        /// <summary>
        /// Hide the merchant area and return camera to player board
        /// </summary>
        public void Hide()
        {
            Debug.Log("[MerchantArea3D] Hide() called");

            // Return camera to player board
            var camera = IsometricCameraSetup.Instance;
            if (camera != null)
            {
                Debug.Log("[MerchantArea3D] Calling FocusOnPlayerBoard()");
                camera.FocusOnPlayerBoard();
            }
            else
            {
                Debug.LogWarning("[MerchantArea3D] IsometricCameraSetup.Instance is null!");
            }

            // Clear visual elements
            ClearPedestals();

            if (groundPlane != null)
            {
                Destroy(groundPlane);
                groundPlane = null;
            }

            if (merchantTitle != null)
            {
                Destroy(merchantTitle);
                merchantTitle = null;
            }

            if (turnIndicator != null)
            {
                Destroy(turnIndicator.gameObject);
                turnIndicator = null;
            }

            Debug.Log("[MerchantArea3D] Merchant area hidden, visual elements cleared");
        }

        // ============================================
        // Event Handlers
        // ============================================

        private void HandleMerchantStart(MerchantStartMessage data)
        {
            Debug.Log($"[MerchantArea3D] HandleMerchantStart - {data.options?.Count ?? 0} options, currentPickerId={data.currentPickerId}, localPlayerId={localPlayerId}");

            // Refresh local player ID in case it wasn't available at start
            if (string.IsNullOrEmpty(localPlayerId) && NetworkManager.Instance != null)
            {
                localPlayerId = NetworkManager.Instance.clientId;
                Debug.Log($"[MerchantArea3D] Refreshed localPlayerId to {localPlayerId}");
            }

            Initialize(data);
            Show();
        }

        private void HandleTurnUpdate(string pickerId, string pickerName)
        {
            SetCurrentPicker(pickerId, pickerName);
        }

        private void HandlePick(string optionId, string pickedById, string pickedByName)
        {
            MarkOptionPicked(optionId, pickedByName);
        }

        private void HandleMerchantEnd()
        {
            Debug.Log("[MerchantArea3D] HandleMerchantEnd - starting delayed hide");

            // Delay hiding slightly to show final state
            StartCoroutine(DelayedHide());
        }

        private System.Collections.IEnumerator DelayedHide()
        {
            Debug.Log("[MerchantArea3D] DelayedHide - waiting 1.5 seconds");
            yield return new WaitForSeconds(1.5f);
            Debug.Log("[MerchantArea3D] DelayedHide - calling Hide()");
            Hide();
        }
    }
}
