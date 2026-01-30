using UnityEngine;
using System.Collections.Generic;
using Crestforge.Core;

namespace Crestforge.Hex
{
    /// <summary>
    /// Hex coordinate using axial system (q, r)
    /// Supports conversion to/from offset, cube, and world coordinates
    /// </summary>
    [System.Serializable]
    public struct HexCoord
    {
        public int q; // Column
        public int r; // Row

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        // Cube coordinate conversion (x + y + z = 0)
        public int X => q;
        public int Z => r;
        public int Y => -q - r;

        /// <summary>
        /// Create from offset coordinates (what we use for board array)
        /// Using odd-q vertical layout
        /// </summary>
        public static HexCoord FromOffset(int col, int row)
        {
            int q = col;
            int r = row - (col - (col & 1)) / 2;
            return new HexCoord(q, r);
        }

        /// <summary>
        /// Create from offset using Vector2Int
        /// </summary>
        public static HexCoord FromOffset(Vector2Int offset)
        {
            return FromOffset(offset.x, offset.y);
        }

        /// <summary>
        /// Convert to offset coordinates for board array access
        /// </summary>
        public Vector2Int ToOffset()
        {
            int col = q;
            int row = r + (q - (q & 1)) / 2;
            return new Vector2Int(col, row);
        }

        /// <summary>
        /// Get world position for this hex
        /// </summary>
        public Vector3 ToWorldPosition(float hexSize = 1f)
        {
            float x = hexSize * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
            float y = hexSize * (3f / 2f * r);
            return new Vector3(x, y, 0);
        }

        /// <summary>
        /// Create from world position
        /// </summary>
        public static HexCoord FromWorldPosition(Vector3 worldPos, float hexSize = 1f)
        {
            float q = (Mathf.Sqrt(3f) / 3f * worldPos.x - 1f / 3f * worldPos.y) / hexSize;
            float r = (2f / 3f * worldPos.y) / hexSize;
            return Round(q, r);
        }

        /// <summary>
        /// Round fractional hex coordinates to nearest hex
        /// </summary>
        public static HexCoord Round(float q, float r)
        {
            float x = q;
            float z = r;
            float y = -x - z;

            int rx = Mathf.RoundToInt(x);
            int ry = Mathf.RoundToInt(y);
            int rz = Mathf.RoundToInt(z);

            float xDiff = Mathf.Abs(rx - x);
            float yDiff = Mathf.Abs(ry - y);
            float zDiff = Mathf.Abs(rz - z);

            if (xDiff > yDiff && xDiff > zDiff)
            {
                rx = -ry - rz;
            }
            else if (yDiff > zDiff)
            {
                ry = -rx - rz;
            }
            else
            {
                rz = -rx - ry;
            }

            return new HexCoord(rx, rz);
        }

        /// <summary>
        /// Calculate distance between two hexes
        /// </summary>
        public int DistanceTo(HexCoord other)
        {
            return (Mathf.Abs(X - other.X) + Mathf.Abs(Y - other.Y) + Mathf.Abs(Z - other.Z)) / 2;
        }

        /// <summary>
        /// Get all neighboring hex coordinates
        /// </summary>
        public HexCoord[] GetNeighbors()
        {
            return new HexCoord[]
            {
                new HexCoord(q + 1, r),     // East
                new HexCoord(q + 1, r - 1), // Northeast
                new HexCoord(q, r - 1),     // Northwest
                new HexCoord(q - 1, r),     // West
                new HexCoord(q - 1, r + 1), // Southwest
                new HexCoord(q, r + 1)      // Southeast
            };
        }

        /// <summary>
        /// Get all hexes within a certain range
        /// </summary>
        public List<HexCoord> GetHexesInRange(int range)
        {
            var results = new List<HexCoord>();
            
            for (int x = -range; x <= range; x++)
            {
                for (int y = Mathf.Max(-range, -x - range); y <= Mathf.Min(range, -x + range); y++)
                {
                    int z = -x - y;
                    results.Add(new HexCoord(q + x, r + z));
                }
            }
            
            return results;
        }

        public override bool Equals(object obj)
        {
            if (obj is HexCoord other)
            {
                return q == other.q && r == other.r;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return q.GetHashCode() ^ (r.GetHashCode() << 16);
        }

        public static bool operator ==(HexCoord a, HexCoord b) => a.q == b.q && a.r == b.r;
        public static bool operator !=(HexCoord a, HexCoord b) => !(a == b);

        public override string ToString() => $"Hex({q}, {r})";
    }

    /// <summary>
    /// Hex grid utility functions
    /// </summary>
    public static class HexUtils
    {
        /// <summary>
        /// Check if offset coordinates are within grid bounds
        /// </summary>
        public static bool IsValidPosition(int col, int row, int width, int height)
        {
            return col >= 0 && col < width && row >= 0 && row < height;
        }

        /// <summary>
        /// Check if offset coordinates are within grid bounds
        /// </summary>
        public static bool IsValidPosition(Vector2Int pos, int width, int height)
        {
            return IsValidPosition(pos.x, pos.y, width, height);
        }

        /// <summary>
        /// Get all valid neighbors for a position within grid bounds
        /// </summary>
        public static List<Vector2Int> GetValidNeighbors(Vector2Int pos, int width, int height)
        {
            var hex = HexCoord.FromOffset(pos);
            var neighbors = hex.GetNeighbors();
            var valid = new List<Vector2Int>();

            foreach (var neighbor in neighbors)
            {
                var offset = neighbor.ToOffset();
                if (IsValidPosition(offset, width, height))
                {
                    valid.Add(offset);
                }
            }

            return valid;
        }

        /// <summary>
        /// Calculate hex distance between two offset positions
        /// </summary>
        public static int Distance(Vector2Int a, Vector2Int b)
        {
            var hexA = HexCoord.FromOffset(a);
            var hexB = HexCoord.FromOffset(b);
            return hexA.DistanceTo(hexB);
        }

        /// <summary>
        /// Simple A* pathfinding on hex grid
        /// Returns list of positions from start to end (excluding start)
        /// </summary>
        public static List<Vector2Int> FindPath(
            Vector2Int start, 
            Vector2Int end, 
            HashSet<Vector2Int> blockedPositions,
            int gridWidth,
            int gridHeight)
        {
            var openSet = new List<PathNode>();
            var closedSet = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

            openSet.Add(new PathNode(start, 0, Distance(start, end)));

            while (openSet.Count > 0)
            {
                // Get node with lowest F score
                openSet.Sort((a, b) => a.F.CompareTo(b.F));
                var current = openSet[0];
                openSet.RemoveAt(0);

                if (current.Position == end)
                {
                    // Reconstruct path
                    return ReconstructPath(cameFrom, end);
                }

                closedSet.Add(current.Position);

                foreach (var neighbor in GetValidNeighbors(current.Position, gridWidth, gridHeight))
                {
                    if (closedSet.Contains(neighbor)) continue;
                    if (blockedPositions.Contains(neighbor) && neighbor != end) continue;

                    int tentativeG = current.G + 1;

                    var existingNode = openSet.Find(n => n.Position == neighbor);
                    if (existingNode == null)
                    {
                        cameFrom[neighbor] = current.Position;
                        openSet.Add(new PathNode(neighbor, tentativeG, Distance(neighbor, end)));
                    }
                    else if (tentativeG < existingNode.G)
                    {
                        cameFrom[neighbor] = current.Position;
                        existingNode.G = tentativeG;
                    }
                }
            }

            // No path found
            return new List<Vector2Int>();
        }

        private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int end)
        {
            var path = new List<Vector2Int>();
            var current = end;

            while (cameFrom.ContainsKey(current))
            {
                path.Add(current);
                current = cameFrom[current];
            }

            path.Reverse();
            return path;
        }

        private class PathNode
        {
            public Vector2Int Position;
            public int G; // Cost from start
            public int H; // Heuristic (estimated cost to end)
            public int F => G + H;

            public PathNode(Vector2Int pos, int g, int h)
            {
                Position = pos;
                G = g;
                H = h;
            }
        }
    }
}
