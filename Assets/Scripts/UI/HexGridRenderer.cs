using UnityEngine;
using Crestforge.Core;
using Crestforge.Data;
using Crestforge.Systems;
using Crestforge.Combat;
using System.Collections.Generic;

namespace Crestforge.UI
{
    public class HexGridRenderer : MonoBehaviour
    {
        [Header("Grid Settings")]
        public float hexSize = 0.55f;
        public Vector2 gridOffset = new Vector2(0f, -1.5f);

        [Header("Colors")]
        public Color playerHexColor = new Color(0.2f, 0.4f, 0.6f, 0.7f);
        public Color enemyHexColor = new Color(0.6f, 0.2f, 0.2f, 0.7f);
        public Color highlightColor = new Color(1f, 1f, 0.5f, 0.9f);
        public Color validDropColor = new Color(0.3f, 0.8f, 0.3f, 0.9f);

        private GameState gameState;
        private CombatManager combatManager;
        private Dictionary<string, GameObject> unitObjects = new Dictionary<string, GameObject>();
        private SpriteRenderer[,] hexTiles;
        private bool isInitialized = false;
        
        private UnitInstance draggedUnit;
        private GameObject draggedObject;
        private Vector2Int? dragStartPosition;
        private bool isDraggingFromBoard;
        private Vector2Int hoveredHex = new Vector2Int(-1, -1);
        private Camera mainCamera;

        // Click vs drag detection
        private Vector3 clickStartPos;
        private UnitInstance clickedUnit;

        // Optimization
        private float lastUpdateTime;
        private const float UPDATE_INTERVAL = 0.1f;

        // Cached sprites
        private Sprite _hexSprite;
        private Sprite _rectSprite;

        private void Start()
        {
            gameState = GameState.Instance;
            combatManager = CombatManager.Instance;
            mainCamera = Camera.main;
            CreateHexGrid();
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized || gameState == null) return;

            if (gameState.round.phase == GamePhase.Planning)
            {
                HandleDragAndDrop();
            }

            // Throttle updates when not dragging
            if (draggedUnit == null && Time.time - lastUpdateTime < UPDATE_INTERVAL)
            {
                return;
            }
            lastUpdateTime = Time.time;

            // During Combat and Results, show combat positions
            if (gameState.round.phase == GamePhase.Combat || gameState.round.phase == GamePhase.Results)
            {
                if (combatManager != null && combatManager.allUnits != null && combatManager.allUnits.Count > 0)
                {
                    UpdateCombatUnits();
                }
                else
                {
                    UpdatePlanningUnits();
                }
            }
            else
            {
                UpdatePlanningUnits();
            }
        }

        private void CreateHexGrid()
        {
            hexTiles = new SpriteRenderer[GameConstants.Grid.WIDTH, GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT];
            Sprite hexSprite = GetHexSprite();

            for (int y = 0; y < GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT; y++)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    Vector3 worldPos = HexToWorldPosition(x, y);
                    GameObject hex = new GameObject($"Hex_{x}_{y}");
                    hex.transform.parent = transform;
                    hex.transform.position = worldPos;

                    SpriteRenderer sr = hex.AddComponent<SpriteRenderer>();
                    sr.sprite = hexSprite;
                    sr.sortingOrder = 0;

                    bool isPlayerSide = y < GameConstants.Grid.HEIGHT;
                    sr.color = isPlayerSide ? playerHexColor : enemyHexColor;
                    hexTiles[x, y] = sr;
                }
            }
        }

        private void HandleDragAndDrop()
        {
            if (hexTiles == null || mainCamera == null) return;
            
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            Vector2Int hexPos = WorldToHexPosition(mouseWorld);

            UpdateHexHighlight(hexPos);

            if (Input.GetMouseButtonDown(0))
            {
                clickStartPos = Input.mousePosition;
                clickedUnit = null;
                
                // Check player board for unit click
                if (IsValidPlayerPosition(hexPos))
                {
                    var unit = gameState.playerBoard[hexPos.x, hexPos.y];
                    if (unit != null)
                    {
                        clickedUnit = unit;
                        
                        // Start drag
                        draggedUnit = unit;
                        dragStartPosition = hexPos;
                        isDraggingFromBoard = true;
                        draggedObject = CreateUnitVisualObject(unit, Vector3.zero, true);
                        if (unitObjects.ContainsKey(unit.instanceId))
                            unitObjects[unit.instanceId].SetActive(false);
                        
                        // Hide tooltip while dragging
                        if (GameUI.Instance != null)
                        {
                            GameUI.Instance.HideTooltipPinned();
                        }
                    }
                }
                
                // Check enemy side for tooltip only (no drag)
                if (clickedUnit == null && hexPos.y >= GameConstants.Grid.HEIGHT && 
                    hexPos.y < GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT &&
                    hexPos.x >= 0 && hexPos.x < GameConstants.Grid.WIDTH)
                {
                    int enemyY = GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT - 1 - hexPos.y;
                    if (gameState.enemyBoard != null && enemyY >= 0 && enemyY < GameConstants.Grid.HEIGHT)
                    {
                        var unit = gameState.enemyBoard[hexPos.x, enemyY];
                        if (unit != null)
                        {
                            clickedUnit = unit;
                            // Show tooltip immediately for enemy units (can't drag them)
                            if (GameUI.Instance != null)
                            {
                                GameUI.Instance.ShowTooltipPinned(unit);
                            }
                        }
                    }
                }
                
                // Clicked on empty space - hide tooltip
                if (clickedUnit == null && GameUI.Instance != null)
                {
                    GameUI.Instance.HideTooltipPinned();
                }
            }

            if (draggedUnit != null && draggedObject != null)
            {
                draggedObject.transform.position = new Vector3(mouseWorld.x, mouseWorld.y, -2);
            }

            if (Input.GetMouseButtonUp(0))
            {
                // Check if this was a click on a player unit (not a drag)
                bool wasClick = Vector3.Distance(Input.mousePosition, clickStartPos) < 10f;
                
                if (wasClick && clickedUnit != null && isDraggingFromBoard && GameUI.Instance != null)
                {
                    // It was a click on a player unit, show tooltip
                    GameUI.Instance.ShowTooltipPinned(clickedUnit);
                }
                
                // Handle drag drop
                if (draggedUnit != null)
                {
                    if (IsValidPlayerPosition(hexPos))
                    {
                        if (isDraggingFromBoard && dragStartPosition.HasValue)
                        {
                            var from = dragStartPosition.Value;
                            var targetUnit = gameState.playerBoard[hexPos.x, hexPos.y];
                            gameState.playerBoard[from.x, from.y] = targetUnit;
                            gameState.playerBoard[hexPos.x, hexPos.y] = draggedUnit;
                            if (targetUnit != null) targetUnit.boardPosition = from;
                            draggedUnit.boardPosition = hexPos;
                            gameState.RecalculateTraits();
                        }
                    }
                    else if (isDraggingFromBoard)
                    {
                        gameState.ReturnToBench(draggedUnit);
                    }

                    if (draggedObject != null) Destroy(draggedObject);
                    if (isDraggingFromBoard && unitObjects.ContainsKey(draggedUnit.instanceId))
                        unitObjects[draggedUnit.instanceId].SetActive(true);

                    draggedObject = null;
                    draggedUnit = null;
                    dragStartPosition = null;
                    isDraggingFromBoard = false;
                    ResetHexHighlights();
                }
                
                clickedUnit = null;
            }
        }

        private void UpdateHexHighlight(Vector2Int hexPos)
        {
            if (hexTiles == null) return;
            
            if (hoveredHex.x >= 0 && hoveredHex.y >= 0 && 
                hoveredHex.x < GameConstants.Grid.WIDTH && 
                hoveredHex.y < GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT &&
                hexTiles[hoveredHex.x, hoveredHex.y] != null)
            {
                bool wasPlayerSide = hoveredHex.y < GameConstants.Grid.HEIGHT;
                hexTiles[hoveredHex.x, hoveredHex.y].color = wasPlayerSide ? playerHexColor : enemyHexColor;
            }

            hoveredHex = hexPos;

            if (IsValidPlayerPosition(hexPos) && hexTiles[hexPos.x, hexPos.y] != null)
            {
                hexTiles[hexPos.x, hexPos.y].color = draggedUnit != null ? validDropColor : highlightColor;
            }
        }

        private void ResetHexHighlights()
        {
            if (hexTiles == null) return;
            
            for (int y = 0; y < GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT; y++)
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    if (hexTiles[x, y] == null) continue;
                    bool isPlayerSide = y < GameConstants.Grid.HEIGHT;
                    hexTiles[x, y].color = isPlayerSide ? playerHexColor : enemyHexColor;
                }
            }
        }

        private bool IsValidPlayerPosition(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < GameConstants.Grid.WIDTH &&
                   pos.y >= 0 && pos.y < GameConstants.Grid.HEIGHT;
        }

        private Vector3 HexToWorldPosition(int x, int y)
        {
            float xOffset = (y % 2 == 1) ? hexSize * 0.5f : 0;
            float worldX = x * hexSize + xOffset + gridOffset.x;
            float worldY = y * hexSize * 0.866f + gridOffset.y;
            return new Vector3(worldX, worldY, 0);
        }

        private Vector2Int WorldToHexPosition(Vector3 worldPos)
        {
            float adjustedX = worldPos.x - gridOffset.x;
            float adjustedY = worldPos.y - gridOffset.y;
            int approxY = Mathf.RoundToInt(adjustedY / (hexSize * 0.866f));
            float xOffset = (approxY % 2 == 1) ? hexSize * 0.5f : 0;
            int approxX = Mathf.RoundToInt((adjustedX - xOffset) / hexSize);
            return new Vector2Int(approxX, approxY);
        }

        private void UpdatePlanningUnits()
        {
            ClearUnitObjects();

            if (gameState.playerBoard == null) return;

            // Always show player units
            for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
            {
                for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                {
                    var unit = gameState.playerBoard[x, y];
                    if (unit != null && unit != draggedUnit)
                    {
                        var obj = CreateUnitVisualObject(unit, HexToWorldPosition(x, y), true);
                        if (obj != null) unitObjects[unit.instanceId] = obj;
                    }
                }
            }

            // Don't show enemy units during Planning/Results - they only appear during Combat
            // (Enemy board contains old data from previous round until combat starts)
            if (false && gameState.enemyBoard != null) // Disabled - enemies only shown in UpdateCombatUnits
            {
                for (int x = 0; x < GameConstants.Grid.WIDTH; x++)
                {
                    for (int y = 0; y < GameConstants.Grid.HEIGHT; y++)
                    {
                        var unit = gameState.enemyBoard[x, y];
                        if (unit != null)
                        {
                            int displayY = GameConstants.Grid.TOTAL_BATTLEFIELD_HEIGHT - 1 - y;
                            var obj = CreateUnitVisualObject(unit, HexToWorldPosition(x, displayY), false);
                            if (obj != null) unitObjects[unit.instanceId] = obj;
                        }
                    }
                }
            }
        }

        private void UpdateCombatUnits()
        {
            ClearUnitObjects();

            if (combatManager == null || combatManager.allUnits == null) return;

            foreach (var combatUnit in combatManager.allUnits)
            {
                if (combatUnit != null && !combatUnit.isDead && combatUnit.source != null)
                {
                    Vector3 pos = HexToWorldPosition(combatUnit.position.x, combatUnit.position.y);
                    var obj = CreateCombatUnitVisual(combatUnit, pos);
                    if (obj != null) unitObjects[combatUnit.source.instanceId] = obj;
                }
            }
        }

        private GameObject CreateUnitVisualObject(UnitInstance unit, Vector3 position, bool isPlayer)
        {
            if (unit == null || unit.template == null) return null;
            
            position.z = -1;
            GameObject unitObj = new GameObject($"Unit_{unit.instanceId}");
            unitObj.transform.parent = transform;
            unitObj.transform.position = position;

            SpriteRenderer sr = unitObj.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSpriteGenerator.GetSprite(unit.template.unitId);
            sr.sortingOrder = 10;
            if (!isPlayer) sr.color = new Color(1f, 0.7f, 0.7f);

            float maxHealth = unit.currentStats != null ? unit.currentStats.health : 1;
            CreateHealthBar(unitObj, (float)unit.currentHealth / Mathf.Max(1, maxHealth), isPlayer);
            CreateStars(unitObj, unit.starLevel);

            return unitObj;
        }

        private GameObject CreateCombatUnitVisual(CombatUnit unit, Vector3 position)
        {
            if (unit == null || unit.source == null || unit.source.template == null) return null;
            
            position.z = -1;
            bool isPlayer = unit.team == Team.Player;
            
            GameObject unitObj = new GameObject($"Unit_{unit.source.instanceId}");
            unitObj.transform.parent = transform;
            unitObj.transform.position = position;

            SpriteRenderer sr = unitObj.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSpriteGenerator.GetSprite(unit.source.template.unitId);
            sr.sortingOrder = 10;
            if (!isPlayer) sr.color = new Color(1f, 0.7f, 0.7f);

            float maxHealth = unit.stats != null ? unit.stats.health : 1;
            CreateHealthBar(unitObj, (float)unit.currentHealth / Mathf.Max(1, maxHealth), isPlayer);
            CreateStars(unitObj, unit.source.starLevel);

            return unitObj;
        }

        private void CreateHealthBar(GameObject parent, float percent, bool isPlayer)
        {
            Sprite rect = GetRectSprite();
            
            GameObject bg = new GameObject("HPBg");
            bg.transform.parent = parent.transform;
            bg.transform.localPosition = new Vector3(0, -0.3f, 0);
            bg.transform.localScale = new Vector3(0.3f, 0.05f, 1);
            SpriteRenderer bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = rect;
            bgSr.color = Color.black;
            bgSr.sortingOrder = 11;

            GameObject fill = new GameObject("HPFill");
            fill.transform.parent = parent.transform;
            fill.transform.localPosition = new Vector3(-0.15f * (1 - percent), -0.3f, 0);
            fill.transform.localScale = new Vector3(0.28f * Mathf.Max(0.01f, percent), 0.04f, 1);
            SpriteRenderer fillSr = fill.AddComponent<SpriteRenderer>();
            fillSr.sprite = rect;
            fillSr.color = isPlayer ? Color.green : Color.red;
            fillSr.sortingOrder = 12;
        }

        private void CreateStars(GameObject parent, int starLevel)
        {
            GameObject stars = new GameObject("Stars");
            stars.transform.parent = parent.transform;
            stars.transform.localPosition = new Vector3(0, 0.35f, 0);
            TextMesh tm = stars.AddComponent<TextMesh>();
            tm.text = new string('*', starLevel);
            tm.fontSize = 24;
            tm.characterSize = 0.04f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.9f, 0.3f);
            stars.GetComponent<MeshRenderer>().sortingOrder = 13;
        }

        private Sprite GetHexSprite()
        {
            if (_hexSprite != null) return _hexSprite;
            
            int texSize = 32;
            Texture2D tex = new Texture2D(texSize, texSize);
            tex.filterMode = FilterMode.Point;
            Color[] pixels = new Color[texSize * texSize];

            Vector2 center = new Vector2(texSize / 2f, texSize / 2f);
            float radius = texSize / 2f - 2;

            for (int py = 0; py < texSize; py++)
            {
                for (int px = 0; px < texSize; px++)
                {
                    Vector2 point = new Vector2(px, py);
                    if (IsPointInHex(point, center, radius))
                    {
                        bool isEdge = !IsPointInHex(point, center, radius - 1.5f);
                        pixels[py * texSize + px] = isEdge ? Color.white : new Color(1, 1, 1, 0.6f);
                    }
                    else
                    {
                        pixels[py * texSize + px] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _hexSprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize / hexSize);
            return _hexSprite;
        }

        private Sprite GetRectSprite()
        {
            if (_rectSprite != null) return _rectSprite;
            
            Texture2D tex = new Texture2D(4, 4);
            tex.filterMode = FilterMode.Point;
            Color[] pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            _rectSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return _rectSprite;
        }

        private bool IsPointInHex(Vector2 point, Vector2 center, float radius)
        {
            float dx = Mathf.Abs(point.x - center.x);
            float dy = Mathf.Abs(point.y - center.y);
            float w = radius * 0.866f;
            float h = radius;
            if (dx > w || dy > h) return false;
            return w * h - w * dy - h * dx / 2f >= 0;
        }

        private void ClearUnitObjects()
        {
            foreach (var kvp in unitObjects)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            unitObjects.Clear();
        }
    }
}