using System.Collections.Generic;
using UnityEngine;
using Crestforge.Networking;

namespace Crestforge.Visuals
{
    /// <summary>
    /// A large glowing orb that appears during Major Crest rounds.
    /// When clicked, displays 3 crest options for the player to choose from.
    /// </summary>
    public class MajorCrestOrb : MonoBehaviour
    {
        public static MajorCrestOrb Instance { get; private set; }

        [Header("Orb Settings")]
        public float orbSize = 1.2f;
        public float dropSpeed = 3f;
        public float bounceHeight = 0.15f;
        public float bounceSpeed = 1.5f;
        public float rotationSpeed = 20f;
        public Color orbColor = new Color(0.9f, 0.7f, 1f); // Light purple

        [Header("Selection Panel")]
        public float panelWidth = 6f;
        public float panelHeight = 2.5f;
        public float optionSpacing = 2f;

        // Runtime state
        private bool isDropping = true;
        private bool isCollected = false;
        private bool showingOptions = false;
        private float bounceTimer = 0f;
        private float baseY;
        private Vector3 targetPosition;

        // Visual components
        private MeshRenderer orbRenderer;
        private Light orbLight;
        private GameObject selectionPanel;
        private List<MajorCrestOptionData> crestOptions;
        private List<CrestOptionButton> optionButtons = new List<CrestOptionButton>();

        private bool eventsSubscribed = false;
        private bool isHidden = true;

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

        private void Start()
        {
            // Subscribe to events (will retry in Update if NetworkManager not ready)
            SubscribeToEvents();

            // Start hidden (no visuals yet)
            isHidden = true;
        }

        private void SubscribeToEvents()
        {
            if (NetworkManager.Instance == null)
            {
                // Will retry in Update() - this is normal on startup
                return;
            }

            if (eventsSubscribed) return;

            NetworkManager.Instance.OnMajorCrestStart += HandleMajorCrestStart;
            NetworkManager.Instance.OnMajorCrestEnd += HandleMajorCrestEnd;

            eventsSubscribed = true;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnMajorCrestStart -= HandleMajorCrestStart;
                NetworkManager.Instance.OnMajorCrestEnd -= HandleMajorCrestEnd;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // Keep trying to subscribe if we haven't yet
            if (!eventsSubscribed && NetworkManager.Instance != null)
            {
                SubscribeToEvents();
            }

            // Skip updates if hidden
            if (isHidden || isCollected || showingOptions) return;

            if (isDropping)
            {
                // Drop to target position
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, dropSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
                {
                    isDropping = false;
                    baseY = transform.position.y;
                }
            }
            else
            {
                // Bounce animation
                bounceTimer += Time.deltaTime * bounceSpeed;
                float yOffset = Mathf.Sin(bounceTimer) * bounceHeight;
                transform.position = new Vector3(transform.position.x, baseY + yOffset, transform.position.z);

                // Rotate slowly
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }

            // Pulse the light
            if (orbLight != null)
            {
                orbLight.intensity = 1.5f + Mathf.Sin(Time.time * 2f) * 0.5f;
            }
        }

        /// <summary>
        /// Initialize and show the orb with crest options
        /// </summary>
        public void Show(List<MajorCrestOptionData> options)
        {
            Debug.Log($"[MajorCrestOrb] Show() called with {options?.Count ?? 0} options");

            crestOptions = options;
            isDropping = true;
            isCollected = false;
            showingOptions = false;
            bounceTimer = 0f;
            isHidden = false;

            // Position above player's board center
            var playerBoard = HexBoard3D.Instance;
            Vector3 boardCenter = playerBoard != null ? playerBoard.transform.position : Vector3.zero;
            targetPosition = boardCenter + Vector3.up * 1.5f;
            transform.position = targetPosition + Vector3.up * 5f; // Start high

            // Create orb visuals
            CreateOrbVisuals();

            // Move camera to focus on orb area
            var camera = IsometricCameraSetup.Instance;
            if (camera != null)
            {
                camera.FocusOnPlayerBoard();
            }

            Debug.Log($"[MajorCrestOrb] Orb visible at position {transform.position}");
        }

        /// <summary>
        /// Hide the orb and clean up
        /// </summary>
        public void Hide()
        {
            Debug.Log("[MajorCrestOrb] Hide() called");
            isHidden = true;

            // Clear option buttons
            foreach (var button in optionButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }
            optionButtons.Clear();

            // Clear selection panel
            if (selectionPanel != null)
            {
                Destroy(selectionPanel);
                selectionPanel = null;
            }

            // Clear all child objects (orb visuals)
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            // Remove collider
            var collider = GetComponent<SphereCollider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            orbRenderer = null;
            orbLight = null;

            Debug.Log("[MajorCrestOrb] Hidden and cleaned up");
        }

        private void CreateOrbVisuals()
        {
            // Clear existing children
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            // Create orb sphere
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "CrestOrb";
            sphere.transform.SetParent(transform);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * orbSize;

            // Remove collider from child sphere - we'll add one to parent
            Destroy(sphere.GetComponent<SphereCollider>());

            // Add collider to parent for OnMouseDown to work
            var existingCollider = GetComponent<SphereCollider>();
            if (existingCollider == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = orbSize * 0.5f;
                collider.isTrigger = false;
            }

            // Create glowing material
            orbRenderer = sphere.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = orbColor;
            mat.SetColor("_EmissionColor", orbColor * 2f);
            mat.EnableKeyword("_EMISSION");
            orbRenderer.material = mat;

            // Add point light
            GameObject lightObj = new GameObject("OrbLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            orbLight = lightObj.AddComponent<Light>();
            orbLight.type = LightType.Point;
            orbLight.color = orbColor;
            orbLight.intensity = 2f;
            orbLight.range = 4f;

            // Add label above orb
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(transform);
            labelObj.transform.localPosition = Vector3.up * (orbSize * 0.5f + 0.5f);
            var label = labelObj.AddComponent<TextMesh>();
            label.text = "MAJOR CREST";
            label.fontSize = 100;
            label.characterSize = 0.04f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(1f, 0.9f, 0.5f);
            labelObj.AddComponent<BillboardUI>();
        }

        private void OnMouseDown()
        {
            Debug.Log($"[MajorCrestOrb] OnMouseDown! isHidden={isHidden}, isCollected={isCollected}, isDropping={isDropping}, showingOptions={showingOptions}");

            if (isHidden || isCollected || isDropping) return;

            Debug.Log("[MajorCrestOrb] Clicked - showing options!");

            if (!showingOptions)
            {
                // First click - show options
                ShowCrestOptions();
            }
        }

        private void ShowCrestOptions()
        {
            if (crestOptions == null || crestOptions.Count == 0)
            {
                Debug.LogWarning("[MajorCrestOrb] No crest options to show");
                return;
            }

            showingOptions = true;
            isCollected = true;

            // Scale down the orb
            transform.localScale = Vector3.one * 0.5f;

            // Create selection panel above the orb
            selectionPanel = new GameObject("SelectionPanel");
            selectionPanel.transform.SetParent(transform);
            selectionPanel.transform.localPosition = Vector3.up * 2f;

            // Create option buttons
            float startX = -optionSpacing * (crestOptions.Count - 1) / 2f;
            for (int i = 0; i < crestOptions.Count; i++)
            {
                var option = crestOptions[i];
                var button = CreateOptionButton(option, i);
                button.transform.SetParent(selectionPanel.transform);
                button.transform.localPosition = new Vector3(startX + i * optionSpacing, 0, 0);
                optionButtons.Add(button);
            }

            // Add instruction text
            var instructionObj = new GameObject("Instruction");
            instructionObj.transform.SetParent(selectionPanel.transform);
            instructionObj.transform.localPosition = Vector3.up * 1.5f;
            var instruction = instructionObj.AddComponent<TextMesh>();
            instruction.text = "Choose Your Major Crest";
            instruction.fontSize = 100;
            instruction.characterSize = 0.05f;
            instruction.anchor = TextAnchor.MiddleCenter;
            instruction.alignment = TextAlignment.Center;
            instruction.color = new Color(1f, 0.9f, 0.6f);
            instructionObj.AddComponent<BillboardUI>();

            Debug.Log($"[MajorCrestOrb] Showing {crestOptions.Count} crest options");
        }

        private CrestOptionButton CreateOptionButton(MajorCrestOptionData option, int index)
        {
            var buttonObj = new GameObject($"CrestOption_{option.crestId}");

            // Add the button component
            var button = buttonObj.AddComponent<CrestOptionButton>();
            button.Initialize(option, this);

            return button;
        }

        /// <summary>
        /// Called when player selects a crest option
        /// </summary>
        public void OnCrestSelected(string crestId)
        {
            Debug.Log($"[MajorCrestOrb] Player selected crest: {crestId}");

            // Send selection to server
            NetworkManager.Instance?.SendMajorCrestSelect(crestId);

            // Visual feedback - disable other options and highlight selected
            foreach (var button in optionButtons)
            {
                if (button.crestId != crestId)
                {
                    button.SetDisabled();
                }
                else
                {
                    button.SetSelected();
                }
            }

            // Hide after a short delay to show the selection feedback
            StartCoroutine(HideAfterSelection());
        }

        private System.Collections.IEnumerator HideAfterSelection()
        {
            yield return new WaitForSeconds(0.3f);

            // Hide the orb and options - round will end when all players select
            Debug.Log("[MajorCrestOrb] Hiding after selection (waiting for other players if multiplayer)");
            Hide();
        }

        // Event handlers
        private void HandleMajorCrestStart(MajorCrestStartMessage data)
        {
            Debug.Log($"[MajorCrestOrb] Major crest round started with {data.options?.Count ?? 0} options");
            Show(data.options);
        }

        private void HandleMajorCrestEnd()
        {
            Debug.Log("[MajorCrestOrb] Major crest round ended");
            StartCoroutine(DelayedHide());
        }

        private System.Collections.IEnumerator DelayedHide()
        {
            yield return new WaitForSeconds(1.5f);
            Hide();
        }
    }

    /// <summary>
    /// A clickable button for selecting a crest option
    /// </summary>
    public class CrestOptionButton : MonoBehaviour
    {
        public string crestId;
        public bool isSelected = false;
        public bool isDisabled = false;

        private MajorCrestOrb parentOrb;
        private MeshRenderer backgroundRenderer;
        private TextMesh nameText;
        private TextMesh descText;
        private Light glowLight;

        public void Initialize(MajorCrestOptionData option, MajorCrestOrb orb)
        {
            crestId = option.crestId;
            parentOrb = orb;

            // Create background panel
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            bg.transform.SetParent(transform);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = new Vector3(1.8f, 1.2f, 1f);
            bg.transform.localRotation = Quaternion.identity;

            backgroundRenderer = bg.GetComponent<MeshRenderer>();
            Material bgMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bgMat.color = new Color(0.2f, 0.15f, 0.3f, 0.9f);
            backgroundRenderer.material = bgMat;

            // Add collider for clicking
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.8f, 1.2f, 0.2f);

            // Create name text
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(transform);
            nameObj.transform.localPosition = new Vector3(0, 0.35f, -0.1f);
            nameText = nameObj.AddComponent<TextMesh>();
            nameText.text = option.name;
            nameText.fontSize = 100;
            nameText.characterSize = 0.03f;
            nameText.anchor = TextAnchor.MiddleCenter;
            nameText.alignment = TextAlignment.Center;
            nameText.color = new Color(1f, 0.85f, 0.4f);

            // Create description text
            var descObj = new GameObject("Description");
            descObj.transform.SetParent(transform);
            descObj.transform.localPosition = new Vector3(0, -0.1f, -0.1f);
            descText = descObj.AddComponent<TextMesh>();
            descText.text = WrapText(option.description, 25);
            descText.fontSize = 100;
            descText.characterSize = 0.02f;
            descText.anchor = TextAnchor.MiddleCenter;
            descText.alignment = TextAlignment.Center;
            descText.color = Color.white;

            // Add glow light
            var lightObj = new GameObject("Glow");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0, 0, -0.2f);
            glowLight = lightObj.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(0.8f, 0.6f, 1f);
            glowLight.intensity = 0.5f;
            glowLight.range = 1.5f;

            // Billboard the whole thing
            gameObject.AddComponent<BillboardUI>();
        }

        private string WrapText(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
                return text;

            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";

            foreach (var word in words)
            {
                if (currentLine.Length + word.Length + 1 <= maxChars)
                {
                    currentLine += (currentLine.Length > 0 ? " " : "") + word;
                }
                else
                {
                    if (currentLine.Length > 0)
                        lines.Add(currentLine);
                    currentLine = word;
                }
            }
            if (currentLine.Length > 0)
                lines.Add(currentLine);

            return string.Join("\n", lines);
        }

        private void Update()
        {
            if (isDisabled || isSelected) return;

            // Pulse glow
            if (glowLight != null)
            {
                glowLight.intensity = 0.5f + Mathf.Sin(Time.time * 3f) * 0.2f;
            }
        }

        private void OnMouseDown()
        {
            if (isDisabled || isSelected) return;
            parentOrb?.OnCrestSelected(crestId);
        }

        private void OnMouseEnter()
        {
            if (isDisabled || isSelected) return;

            // Highlight on hover
            if (backgroundRenderer != null)
            {
                backgroundRenderer.material.color = new Color(0.3f, 0.25f, 0.5f);
            }
            if (glowLight != null)
            {
                glowLight.intensity = 1f;
            }
            transform.localScale = Vector3.one * 1.1f;
        }

        private void OnMouseExit()
        {
            if (isDisabled || isSelected) return;

            // Remove highlight
            if (backgroundRenderer != null)
            {
                backgroundRenderer.material.color = new Color(0.2f, 0.15f, 0.3f);
            }
            if (glowLight != null)
            {
                glowLight.intensity = 0.5f;
            }
            transform.localScale = Vector3.one;
        }

        public void SetSelected()
        {
            isSelected = true;
            if (backgroundRenderer != null)
            {
                backgroundRenderer.material.color = new Color(0.3f, 0.5f, 0.3f); // Green
            }
            if (glowLight != null)
            {
                glowLight.color = new Color(0.4f, 1f, 0.5f);
                glowLight.intensity = 1.5f;
            }
            if (nameText != null)
            {
                nameText.color = new Color(0.5f, 1f, 0.6f);
            }
        }

        public void SetDisabled()
        {
            isDisabled = true;
            if (backgroundRenderer != null)
            {
                backgroundRenderer.material.color = new Color(0.15f, 0.15f, 0.15f);
            }
            if (glowLight != null)
            {
                glowLight.intensity = 0.1f;
            }
            if (nameText != null)
            {
                nameText.color = new Color(0.4f, 0.4f, 0.4f);
            }
            if (descText != null)
            {
                descText.color = new Color(0.4f, 0.4f, 0.4f);
            }
            transform.localScale = Vector3.one * 0.9f;
        }
    }
}
