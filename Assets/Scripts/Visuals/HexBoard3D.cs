using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Combat;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Generates and manages 3D hexagonal tiles for the game board.
    /// Creates extruded hex prisms with proper materials and hover effects.
    /// Supports multiple instances for PvP mode.
    /// </summary>
    public class HexBoard3D : MonoBehaviour
    {
        // Primary instance (player's board) - for backwards compatibility
        public static HexBoard3D Instance { get; set; }

        // All board instances for multi-board PvP
        public static List<HexBoard3D> AllBoards { get; private set; } = new List<HexBoard3D>();

        [Header("Board Settings")]
        public float hexRadius = 0.5f;
        public float hexHeight = 0.15f;
        public float hexSpacing = 0.05f;
        public int playerRows = 4;  // Player gets 4 rows

        [Header("Board Identity")]
        [Tooltip("Is this the main player's board?")]
        public bool isPlayerBoard = true;
        [Tooltip("Owner ID for opponent boards")]
        public string ownerId = "";
        [Tooltip("Display name for this board")]
        public string boardLabel = "Player";

        [Header("Visual Settings")]
        public Color playerTileColor;
        public Color enemyTileColor;
        public Color highlightColor;
        public Color hoverColor;
        public bool useMedievalTheme = true;

        [Header("Materials")]
        public Material tileMaterial;

        // Runtime
        private GameObject[,] playerTiles;
        private GameObject[,] enemyTiles;
        private Dictionary<GameObject, Vector2Int> tileToCoord = new Dictionary<GameObject, Vector2Int>();
        private Dictionary<GameObject, bool> tileIsEnemy = new Dictionary<GameObject, bool>();
        private GameObject hoveredTile;
        private GameObject selectedTile;
        private Vector3 boardCenter;

        // Visual registry - single source of truth for all unit visuals on this board
        private BoardVisualRegistry _registry;

        /// <summary>
        /// The visual registry that owns all unit visuals for this board.
        /// Single source of truth - use this instead of creating visuals directly.
        /// </summary>
        public BoardVisualRegistry Registry
        {
            get
            {
                if (_registry == null)
                {
                    _registry = GetComponent<BoardVisualRegistry>();
                    if (_registry == null)
                    {
                        _registry = gameObject.AddComponent<BoardVisualRegistry>();
                    }
                }
                return _registry;
            }
        }

        private void Awake()
        {
            // Register this board
            if (!AllBoards.Contains(this))
            {
                AllBoards.Add(this);
            }

            // Set as primary instance if it's the player board and no instance exists
            if (isPlayerBoard && Instance == null)
            {
                Instance = this;
            }

            // Create visual registry for this board
            if (_registry == null)
            {
                _registry = GetComponent<BoardVisualRegistry>();
                if (_registry == null)
                {
                    _registry = gameObject.AddComponent<BoardVisualRegistry>();
                }
            }

            // Note: Bench slots (visual platforms) are created by Game3DSetup at startup.
        }

        private void OnDestroy()
        {
            AllBoards.Remove(this);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (tileMaterial == null)
            {
                CreateDefaultMaterial();
            }

            GenerateBoard();

            // Only center camera on player's board
            if (isPlayerBoard && IsometricCameraSetup.Instance != null)
            {
                // Total board is HEIGHT * 2 (player + enemy sides)
                int totalRows = GameConstants.Grid.HEIGHT * 2;
                Vector3 boardSize = new Vector3(
                    GameConstants.Grid.WIDTH * (hexRadius * 2 + hexSpacing),
                    0,
                    totalRows * (hexRadius * 1.732f + hexSpacing)
                );
                IsometricCameraSetup.Instance.CenterOnBoard(boardCenter, boardSize);
            }
        }

        private void CreateDefaultMaterial()
        {
            // Try grass shader first for Merge Tactics style
            Shader grassShader = Shader.Find("Crestforge/GrassHex");
            if (grassShader != null && useMedievalTheme)
            {
                tileMaterial = new Material(grassShader);
            }
            else
            {
                // Use URP/Lit shader
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader != null)
                {
                    tileMaterial = new Material(urpShader);
                    tileMaterial.SetFloat("_Smoothness", 0.2f);
                    tileMaterial.SetFloat("_Metallic", 0.05f);
                }
                else
                {
                    tileMaterial = new Material(Shader.Find("Standard"));
                    tileMaterial.SetFloat("_Glossiness", 0.2f);
                    tileMaterial.SetFloat("_Metallic", 0.05f);
                }
            }

            // Apply grass colors for Merge Tactics style
            if (useMedievalTheme)
            {
                playerTileColor = MedievalVisualConfig.BoardColors.PlayerTileBase;
                enemyTileColor = MedievalVisualConfig.BoardColors.EnemyTileBase;
                highlightColor = MedievalVisualConfig.BoardColors.PlayerHighlight;
                hoverColor = Color.Lerp(playerTileColor, Color.white, 0.1f);
            }
        }

        /// <summary>
        /// Generate the full game board with player and enemy sides
        /// </summary>
        public void GenerateBoard()
        {
            ClearBoard();

            int width = GameConstants.Grid.WIDTH;
            // HEIGHT is per-side, so total rows = HEIGHT * 2 (player + enemy)
            int rowsPerSide = GameConstants.Grid.HEIGHT;
            int totalRows = rowsPerSide * 2;
            playerRows = rowsPerSide;  // Update playerRows to match

            playerTiles = new GameObject[width, playerRows];
            enemyTiles = new GameObject[width, rowsPerSide];

            // Calculate total board size for centering
            float totalWidth = width * (hexRadius * 2 + hexSpacing);
            float totalHeight = totalRows * (hexRadius * 1.732f + hexSpacing);
            Vector3 offset = new Vector3(-totalWidth / 2f + hexRadius, 0, -totalHeight / 2f + hexRadius);

            // Generate player tiles (bottom rows)
            for (int y = 0; y < playerRows; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 pos = HexToWorldPosition(x, y) + offset;
                    GameObject tile = CreateHexTile(pos, playerTileColor, $"PlayerTile_{x}_{y}");
                    playerTiles[x, y] = tile;
                    tileToCoord[tile] = new Vector2Int(x, y);
                    tileIsEnemy[tile] = false;
                }
            }

            // Generate enemy tiles (top rows)
            int enemyRowCount = rowsPerSide;

            for (int y = playerRows; y < totalRows; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 pos = HexToWorldPosition(x, y) + offset;
                    GameObject tile = CreateHexTile(pos, enemyTileColor, $"EnemyTile_{x}_{y}");
                    enemyTiles[x, y - playerRows] = tile;
                    tileToCoord[tile] = new Vector2Int(x, y);
                    tileIsEnemy[tile] = true;
                }
            }

            // Calculate board center (relative to board transform)
            boardCenter = transform.position;
        }

        /// <summary>
        /// Convert hex grid coordinates to world position
        /// </summary>
        public Vector3 HexToWorldPosition(int x, int y)
        {
            float xOffset = (y % 2 == 1) ? (hexRadius + hexSpacing / 2f) : 0;
            float worldX = x * (hexRadius * 2 + hexSpacing) + xOffset;
            float worldZ = y * (hexRadius * 1.732f + hexSpacing);
            // Raise tiles above ground to prevent Z-fighting
            return new Vector3(worldX, 0.05f, worldZ);
        }

        /// <summary>
        /// Get world position for a grid coordinate (including board offset)
        /// </summary>
        public Vector3 GetTileWorldPosition(int x, int y)
        {
            // Total board is HEIGHT * 2 (player + enemy sides)
            int totalRows = GameConstants.Grid.HEIGHT * 2;
            float totalWidth = GameConstants.Grid.WIDTH * (hexRadius * 2 + hexSpacing);
            float totalHeight = totalRows * (hexRadius * 1.732f + hexSpacing);
            Vector3 offset = new Vector3(-totalWidth / 2f + hexRadius, 0, -totalHeight / 2f + hexRadius);

            // Add board's world position to get correct world coordinates
            return transform.position + HexToWorldPosition(x, y) + offset;
        }

        /// <summary>
        /// Create a single 3D hex tile with grass styling (Merge Tactics style)
        /// </summary>
        private GameObject CreateHexTile(Vector3 position, Color color, string name)
        {
            GameObject tile = new GameObject(name);
            tile.transform.SetParent(transform);
            tile.transform.localPosition = position;  // Use localPosition so tiles are relative to board

            // Create hex mesh
            MeshFilter mf = tile.AddComponent<MeshFilter>();
            MeshRenderer mr = tile.AddComponent<MeshRenderer>();
            MeshCollider mc = tile.AddComponent<MeshCollider>();

            Mesh mesh = CreateHexMesh();
            mf.mesh = mesh;
            mc.sharedMesh = mesh;

            // Add slight color variation for natural grass look
            float variation = Random.Range(-0.02f, 0.02f);
            Color variedColor = new Color(
                Mathf.Clamp01(color.r + variation),
                Mathf.Clamp01(color.g + variation * 1.5f), // More variation in green
                Mathf.Clamp01(color.b + variation * 0.5f)
            );

            // Create grass material
            Material mat;
            if (useMedievalTheme)
            {
                Shader grassShader = Shader.Find("Crestforge/GrassHex");
                if (grassShader != null)
                {
                    mat = new Material(grassShader);
                    mat.SetColor("_MainColor", variedColor);
                    mat.SetColor("_EdgeColor", Color.Lerp(variedColor, MedievalVisualConfig.BoardColors.HexOutline, 0.5f));
                    mat.SetColor("_OutlineColor", MedievalVisualConfig.BoardColors.HexOutline);
                    mat.SetFloat("_OutlineWidth", 0.03f);
                    mat.SetFloat("_EdgeWidth", 0.12f);
                    mat.SetFloat("_Brightness", 1.0f);
                }
                else
                {
                    // Fallback to URP/Lit material with grass-like appearance
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpShader != null)
                    {
                        mat = new Material(urpShader);
                        mat.SetColor("_BaseColor", variedColor);
                        mat.SetFloat("_Smoothness", 0.1f); // Low smoothness for matte grass look
                        mat.SetFloat("_Metallic", 0f);
                    }
                    else
                    {
                        Color shadowColor = Color.Lerp(variedColor, Color.black, 0.3f);
                        mat = MedievalVisualConfig.CreateToonMaterial(variedColor, shadowColor, 0.15f);
                    }
                }
            }
            else
            {
                mat = new Material(tileMaterial);
                // Set color for both URP and Standard shaders
                mat.SetColor("_BaseColor", variedColor);
                mat.color = variedColor;
            }
            mr.material = mat;

            // Add HexTile component for interaction
            HexTile3D hexTile = tile.AddComponent<HexTile3D>();
            hexTile.baseColor = variedColor;
            hexTile.useMedievalTheme = useMedievalTheme;

            return tile;
        }

        /// <summary>
        /// Generate a hexagonal prism mesh
        /// </summary>
        private Mesh CreateHexMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "HexTile";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector3> normals = new List<Vector3>();

            // Generate hex vertices (6 corners + center for top and bottom)
            Vector3[] topCorners = new Vector3[6];
            Vector3[] bottomCorners = new Vector3[6];

            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i - 30f; // Start at -30 for flat-top hex
                float rad = angle * Mathf.Deg2Rad;
                float x = hexRadius * Mathf.Cos(rad);
                float z = hexRadius * Mathf.Sin(rad);
                
                topCorners[i] = new Vector3(x, hexHeight, z);
                bottomCorners[i] = new Vector3(x, 0, z);
            }

            Vector3 topCenter = new Vector3(0, hexHeight, 0);
            Vector3 bottomCenter = new Vector3(0, 0, 0);

            // === TOP FACE ===
            int topCenterIdx = vertices.Count;
            vertices.Add(topCenter);
            normals.Add(Vector3.up);
            
            for (int i = 0; i < 6; i++)
            {
                vertices.Add(topCorners[i]);
                normals.Add(Vector3.up);
            }

            for (int i = 0; i < 6; i++)
            {
                triangles.Add(topCenterIdx);
                triangles.Add(topCenterIdx + 1 + i);
                triangles.Add(topCenterIdx + 1 + (i + 1) % 6);
            }

            // === BOTTOM FACE ===
            int bottomCenterIdx = vertices.Count;
            vertices.Add(bottomCenter);
            normals.Add(Vector3.down);
            
            for (int i = 0; i < 6; i++)
            {
                vertices.Add(bottomCorners[i]);
                normals.Add(Vector3.down);
            }

            for (int i = 0; i < 6; i++)
            {
                triangles.Add(bottomCenterIdx);
                triangles.Add(bottomCenterIdx + 1 + (i + 1) % 6);
                triangles.Add(bottomCenterIdx + 1 + i);
            }

            // === SIDE FACES ===
            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                
                // Calculate side normal
                Vector3 edge = topCorners[next] - topCorners[i];
                Vector3 sideNormal = Vector3.Cross(Vector3.up, edge).normalized;

                int baseIdx = vertices.Count;
                
                // Add 4 vertices for this side quad
                vertices.Add(topCorners[i]);
                vertices.Add(topCorners[next]);
                vertices.Add(bottomCorners[next]);
                vertices.Add(bottomCorners[i]);
                
                normals.Add(sideNormal);
                normals.Add(sideNormal);
                normals.Add(sideNormal);
                normals.Add(sideNormal);

                // Two triangles for the quad
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);
                
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 3);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.normals = normals.ToArray();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Clear all tiles
        /// </summary>
        public void ClearBoard()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            
            tileToCoord.Clear();
            tileIsEnemy.Clear();
            playerTiles = null;
            enemyTiles = null;
        }

        /// <summary>
        /// Highlight a tile (for valid placement)
        /// </summary>
        public void HighlightTile(int x, int y, bool isValid)
        {
            GameObject tile = GetTile(x, y);
            if (tile != null)
            {
                HexTile3D hexTile = tile.GetComponent<HexTile3D>();
                if (hexTile != null)
                {
                    hexTile.SetHighlight(isValid ? highlightColor : Color.red);
                }
            }
        }

        /// <summary>
        /// Clear all highlights
        /// </summary>
        public void ClearHighlights()
        {
            if (playerTiles != null)
            {
                foreach (var tile in playerTiles)
                {
                    if (tile != null)
                    {
                        tile.GetComponent<HexTile3D>()?.ClearHighlight();
                    }
                }
            }
            
            if (enemyTiles != null)
            {
                foreach (var tile in enemyTiles)
                {
                    if (tile != null)
                    {
                        tile.GetComponent<HexTile3D>()?.ClearHighlight();
                    }
                }
            }
        }

        /// <summary>
        /// Get tile at grid position
        /// </summary>
        public GameObject GetTile(int x, int y)
        {
            if (y < playerRows)
            {
                if (playerTiles != null && x >= 0 && x < playerTiles.GetLength(0) && y >= 0 && y < playerTiles.GetLength(1))
                {
                    return playerTiles[x, y];
                }
            }
            else
            {
                int enemyY = y - playerRows;
                if (enemyTiles != null && x >= 0 && x < enemyTiles.GetLength(0) && enemyY >= 0 && enemyY < enemyTiles.GetLength(1))
                {
                    return enemyTiles[x, enemyY];
                }
            }
            
            return null;
        }

        /// <summary>
        /// Get grid coordinates from a tile object
        /// </summary>
        public bool TryGetTileCoord(GameObject tile, out Vector2Int coord, out bool isEnemy)
        {
            if (tileToCoord.TryGetValue(tile, out coord))
            {
                isEnemy = tileIsEnemy[tile];
                return true;
            }
            
            coord = Vector2Int.zero;
            isEnemy = false;
            return false;
        }

        /// <summary>
        /// Raycast to find tile under screen position
        /// </summary>
        public GameObject GetTileAtScreenPosition(Vector3 screenPos)
        {
            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (tileToCoord.ContainsKey(hit.collider.gameObject))
                {
                    return hit.collider.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Find the closest tile to a world position (more reliable than raycasting)
        /// </summary>
        public bool TryGetClosestTileCoord(Vector3 worldPos, float maxDistance, out Vector2Int coord, out bool isEnemy)
        {
            coord = Vector2Int.zero;
            isEnemy = false;

            float closestDist = float.MaxValue;
            GameObject closestTile = null;

            foreach (var kvp in tileToCoord)
            {
                GameObject tile = kvp.Key;
                if (tile == null) continue;

                float dist = Vector2.Distance(
                    new Vector2(worldPos.x, worldPos.z),
                    new Vector2(tile.transform.position.x, tile.transform.position.z)
                );

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestTile = tile;
                }
            }

            if (closestTile != null && closestDist < maxDistance)
            {
                coord = tileToCoord[closestTile];
                isEnemy = tileIsEnemy[closestTile];
                return true;
            }

            return false;
        }

        public Vector3 BoardCenter => boardCenter;
        public float TileRadius => hexRadius;
        public float TileHeight => hexHeight;
    }

}