using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Combat;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Generates and manages hexagonal tiles for the game board.
    /// Creates flat 2D pointy-top hexes with outlines and proper mesh colliders.
    /// Hexes touch at edges with no gaps (offset coordinate system).
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
        public float hexHeight = 0.01f;  // Very thin - essentially 2D
        public int playerRows = 4;  // Player gets 4 rows

        // Hex grid spacing constants (for pointy-top hexes with offset rows)
        // Width (horizontal spacing) = sqrt(3) * radius
        // Height (point to point) = 2 * radius
        // Row spacing = 1.5 * radius (hexes interlock vertically)
        private float HexWidth => hexRadius * 1.732f;  // sqrt(3) * radius - horizontal spacing
        private float HexHeight => hexRadius * 2f;     // 2 * radius - point to point height
        private float RowSpacing => hexRadius * 1.5f;  // Vertical distance between row centers

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
                    GameConstants.Grid.WIDTH * HexWidth,
                    0,
                    (totalRows - 1) * RowSpacing + HexHeight  // Account for proper row spacing
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
            float totalWidth = width * HexWidth;
            float totalHeight = (totalRows - 1) * RowSpacing + HexHeight;
            Vector3 offset = new Vector3(-totalWidth / 2f + HexWidth / 2f, 0, -totalHeight / 2f + HexHeight / 2f);

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

            // Draw unique hex edges (avoiding duplicates where hexes share edges)
            DrawBoardOutlines();
        }

        /// <summary>
        /// Convert hex grid coordinates to world position
        /// For pointy-top hexes with offset rows: horizontal spacing = sqrt(3)*radius, vertical spacing = 1.5*radius
        /// </summary>
        public Vector3 HexToWorldPosition(int x, int y)
        {
            // Odd rows are offset by half the hex width (offset coordinate system)
            float xOffset = (y % 2 == 1) ? (HexWidth / 2f) : 0;
            float worldX = x * HexWidth + xOffset;
            float worldZ = y * RowSpacing;  // 1.5 * radius for interlocking hexes
            // Raise tiles slightly above ground to prevent Z-fighting
            return new Vector3(worldX, 0.02f, worldZ);
        }

        /// <summary>
        /// Get world position for a grid coordinate (including board offset)
        /// </summary>
        public Vector3 GetTileWorldPosition(int x, int y)
        {
            // Total board is HEIGHT * 2 (player + enemy sides)
            int totalRows = GameConstants.Grid.HEIGHT * 2;
            float totalWidth = GameConstants.Grid.WIDTH * HexWidth;
            float totalHeight = (totalRows - 1) * RowSpacing + HexHeight;
            Vector3 offset = new Vector3(-totalWidth / 2f + HexWidth / 2f, 0, -totalHeight / 2f + HexHeight / 2f);

            // Add board's world position to get correct world coordinates
            return transform.position + HexToWorldPosition(x, y) + offset;
        }

        /// <summary>
        /// Create a single flat 2D hex tile with outline
        /// </summary>
        private GameObject CreateHexTile(Vector3 position, Color color, string name)
        {
            GameObject tile = new GameObject(name);
            tile.transform.SetParent(transform);
            tile.transform.localPosition = position;  // Use localPosition so tiles are relative to board

            // Create flat hex mesh (2D - just the top face)
            MeshFilter mf = tile.AddComponent<MeshFilter>();
            MeshRenderer mr = tile.AddComponent<MeshRenderer>();
            MeshCollider mc = tile.AddComponent<MeshCollider>();

            Mesh mesh = CreateFlatHexMesh();
            mf.mesh = mesh;
            mc.sharedMesh = mesh;  // Collider matches visual exactly

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
                    mat.SetColor("_EdgeColor", Color.Lerp(variedColor, MedievalVisualConfig.BoardColors.HexOutline, 0.7f));
                    mat.SetColor("_OutlineColor", MedievalVisualConfig.BoardColors.HexOutline);
                    mat.SetFloat("_OutlineWidth", 0f);     // Disabled - using LineRenderer outlines instead
                    mat.SetFloat("_EdgeWidth", 0.15f);     // Wider edge gradient
                    mat.SetFloat("_Brightness", 1.0f);
                }
                else
                {
                    // Fallback to URP/Lit material with edge darkening effect
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpShader != null)
                    {
                        // Darken edges by using a darker base color
                        Color edgeColor = Color.Lerp(variedColor, Color.black, 0.3f);
                        mat = new Material(urpShader);
                        mat.SetColor("_BaseColor", edgeColor);
                        mat.SetFloat("_Smoothness", 0.1f);
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

            // Note: Outlines are drawn globally in DrawBoardOutlines() to avoid duplicate edges

            return tile;
        }

        /// <summary>
        /// Draw all hex outlines, avoiding duplicate edges where hexes share borders
        /// </summary>
        private void DrawBoardOutlines()
        {
            // Container for all outline segments
            GameObject outlinesContainer = new GameObject("BoardOutlines");
            outlinesContainer.transform.SetParent(transform, false);

            // Track which edges have been drawn using a string key of the two endpoint positions
            HashSet<string> drawnEdges = new HashSet<string>();

            int width = GameConstants.Grid.WIDTH;
            int totalRows = GameConstants.Grid.HEIGHT * 2;

            // Calculate board offset (same as in GenerateBoard)
            float totalWidth = width * HexWidth;
            float totalHeight = (totalRows - 1) * RowSpacing + HexHeight;
            Vector3 offset = new Vector3(-totalWidth / 2f + HexWidth / 2f, 0, -totalHeight / 2f + HexHeight / 2f);

            float outlineY = hexHeight + 0.005f;
            Color outlineColor = new Color(0.2f, 0.15f, 0.1f, 0.35f);  // Dark brown, more transparent

            // Go through all hexes and draw their edges
            for (int y = 0; y < totalRows; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 hexCenter = HexToWorldPosition(x, y) + offset;

                    // Get the 6 corners of this hex
                    Vector3[] corners = new Vector3[6];
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = 60f * i - 30f;
                        float rad = angle * Mathf.Deg2Rad;
                        corners[i] = hexCenter + new Vector3(
                            hexRadius * Mathf.Cos(rad),
                            outlineY,
                            hexRadius * Mathf.Sin(rad)
                        );
                    }

                    // Draw each of the 6 edges if not already drawn
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 p1 = corners[i];
                        Vector3 p2 = corners[(i + 1) % 6];

                        // Create a key for this edge (order-independent)
                        string edgeKey = GetEdgeKey(p1, p2);

                        if (!drawnEdges.Contains(edgeKey))
                        {
                            drawnEdges.Add(edgeKey);
                            CreateEdgeLine(outlinesContainer.transform, p1, p2, outlineColor);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create a string key for an edge that is the same regardless of point order
        /// </summary>
        private string GetEdgeKey(Vector3 p1, Vector3 p2)
        {
            // Round to 2 decimal places to handle floating point precision
            string key1 = $"{p1.x:F2},{p1.z:F2}";
            string key2 = $"{p2.x:F2},{p2.z:F2}";

            // Order-independent key
            return string.Compare(key1, key2) < 0 ? $"{key1}-{key2}" : $"{key2}-{key1}";
        }

        /// <summary>
        /// Create a single line segment for an edge
        /// </summary>
        private void CreateEdgeLine(Transform parent, Vector3 p1, Vector3 p2, Color color)
        {
            GameObject lineObj = new GameObject("Edge");
            lineObj.transform.SetParent(parent, false);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, p1);
            lr.SetPosition(1, p2);

            lr.startWidth = 0.03f;
            lr.endWidth = 0.03f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.sortingOrder = 1;
        }

        /// <summary>
        /// Generate a flat hexagonal mesh (2D - horizontal face for pointy-top hex)
        /// </summary>
        private Mesh CreateFlatHexMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "FlatHexTile";

            Vector3[] vertices = new Vector3[7];  // 6 corners + center
            int[] triangles = new int[18];        // 6 triangles * 3 vertices
            Vector3[] normals = new Vector3[7];
            Vector2[] uvs = new Vector2[7];

            // Center vertex
            vertices[0] = new Vector3(0, 0, 0);
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            // 6 corner vertices (pointy-top hex: start at -30 degrees, point at top)
            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i - 30f;
                float rad = angle * Mathf.Deg2Rad;
                float x = hexRadius * Mathf.Cos(rad);
                float z = hexRadius * Mathf.Sin(rad);

                vertices[i + 1] = new Vector3(x, 0, z);
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(0.5f + x / hexRadius * 0.5f, 0.5f + z / hexRadius * 0.5f);
            }

            // 6 triangles from center to each edge
            for (int i = 0; i < 6; i++)
            {
                triangles[i * 3] = 0;                    // Center
                triangles[i * 3 + 1] = i + 1;            // Current corner
                triangles[i * 3 + 2] = (i % 6) + 2 > 6 ? 1 : (i % 6) + 2;  // Next corner (wrap)
            }

            // Fix the last triangle wrapping
            triangles[15] = 0;
            triangles[16] = 6;
            triangles[17] = 1;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Generate a hexagonal prism mesh (kept for compatibility, but not used for tiles)
        /// </summary>
        private Mesh CreateHexMesh(float radiusScale = 1f)
        {
            // Just return flat mesh now
            return CreateFlatHexMesh();
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
        /// Find the closest tile to a world position (more reliable than raycasting).
        /// Uses 2D distance in the X-Z plane.
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

        /// <summary>
        /// Find the closest tile to a world position, always returns a result if on or near the board.
        /// This is more reliable than raycast-based detection for hex grids.
        /// </summary>
        public bool TryGetClosestTileCoordUnlimited(Vector3 worldPos, out Vector2Int coord, out bool isEnemy)
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

            if (closestTile != null)
            {
                coord = tileToCoord[closestTile];
                isEnemy = tileIsEnemy[closestTile];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the maximum distance from any point to the nearest hex center.
        /// For a hex grid, this is approximately the hex radius.
        /// </summary>
        public float MaxDistanceToHexCenter => hexRadius;

        public Vector3 BoardCenter => boardCenter;
        public float TileRadius => hexRadius;
        public float TileHeight => hexHeight;
    }

}