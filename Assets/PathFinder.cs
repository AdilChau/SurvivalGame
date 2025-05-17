// Pathfinder.cs
// Handles A* pathfinding using a tile-based grid system and dynamic obstacle checking via tilemaps.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Pathfinder : MonoBehaviour
{
    [Header("Tilemap References")]
    public Tilemap walkableTilemap;             // The tilemap that represents walkable ground
    public Tilemap[] obstacleTilemaps;          // Tilemaps that contain obstacles (e.g., trees, stones)

    // Internal pathfinding grid storing walkability and node data
    private Dictionary<Vector3Int, Node> grid;

    private void Start()
    {
        RegenerateGrid();  // Generate the grid at startup
        Debug.Log("Grid regenerated.");
    }

    /// <summary>
    /// Rebuilds the walkability grid by scanning the walkable tilemap and obstacle maps.
    /// Called at startup and whenever obstacles are changed (e.g., removed).
    /// </summary>
    public void RegenerateGrid()
    {
        grid = new Dictionary<Vector3Int, Node>();

        // Compute overall bounds across walkable and obstacle tilemaps
        BoundsInt bounds = GetExpandedTileBounds();

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            bool walkable = walkableTilemap.HasTile(pos) && !IsObstacle(pos);
            grid[pos] = new Node(pos, walkable);

            if (!walkable)
                Debug.Log($"Blocked tile at {pos} due to obstacle");
        }
    }

    /// <summary>
    /// Returns expanded tile bounds covering all relevant tilemaps.
    /// </summary>
    private BoundsInt GetExpandedTileBounds()
    {
        BoundsInt bounds = walkableTilemap.cellBounds;

        foreach (var tilemap in obstacleTilemaps)
        {
            BoundsInt obstacleBounds = tilemap.cellBounds;
            bounds.xMin = Mathf.Min(bounds.xMin, obstacleBounds.xMin);
            bounds.yMin = Mathf.Min(bounds.yMin, obstacleBounds.yMin);
            bounds.xMax = Mathf.Max(bounds.xMax, obstacleBounds.xMax);
            bounds.yMax = Mathf.Max(bounds.yMax, obstacleBounds.yMax);
        }

        bounds.xMin -= 2;
        bounds.yMin -= 2;
        bounds.xMax += 2;
        bounds.yMax += 2;

        return bounds;
    }

    /// <summary>
    /// Checks if a given tile position is obstructed by any obstacle tilemap.
    /// </summary>
    private bool IsObstacle(Vector3Int position)
    {
        foreach (var tilemap in obstacleTilemaps)
        {
            bool isTreeMap = tilemap.name.ToLower().Contains("tree");

            if (!tilemap.HasTile(position)) continue;

            if (isTreeMap)
            {
                Vector3Int above = position + Vector3Int.up;
                if (!tilemap.HasTile(above))
                    return true; // Tree trunk blocks; canopy doesn't
            }
            else
            {
                return true; // Non-tree obstacles block by default
            }
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        if (grid == null) return;

        Gizmos.color = Color.red;

        foreach (var node in grid.Values)
        {
            if (!node.isWalkable)
            {
                Vector3 world = walkableTilemap.GetCellCenterWorld(node.position);
                Gizmos.DrawWireCube(world, new Vector3(1f, 1f, 0f));
            }
        }
    }

    /// <summary>
    /// Finds a path using the A* algorithm from the start to the end tile.
    /// </summary>
    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int end)
    {
        if (!IsValidPosition(start) || !IsValidPosition(end)) return null;

        List<Node> openList = new List<Node> { grid[start] };
        HashSet<Node> closedSet = new HashSet<Node>();

        InitializePathfindingCosts();

        Node startNode = grid[start];
        Node endNode = grid[end];

        startNode.gCost = 0;
        startNode.hCost = GetDistance(start, end);
        startNode.CalculateFCost();

        while (openList.Count > 0)
        {
            Node current = GetLowestFCostNode(openList);

            if (current.position == end)
                return RetracePath(startNode, current);

            openList.Remove(current);
            closedSet.Add(current);

            foreach (Vector3Int neighborPos in GetNeighbors(current.position))
            {
                if (!grid.ContainsKey(neighborPos)) continue;

                Node neighbor = grid[neighborPos];
                if (!neighbor.isWalkable || closedSet.Contains(neighbor)) continue;

                int tentativeGCost = current.gCost + GetDistance(current.position, neighbor.position);
                if (tentativeGCost < neighbor.gCost)
                {
                    neighbor.gCost = tentativeGCost;
                    neighbor.hCost = GetDistance(neighbor.position, end);
                    neighbor.CalculateFCost();
                    neighbor.parent = current;

                    if (!openList.Contains(neighbor))
                        openList.Add(neighbor);
                }
            }
        }

        return null; // No valid path found
    }

    /// <summary>
    /// Validates whether the tile exists and is walkable.
    /// </summary>
    private bool IsValidPosition(Vector3Int position)
    {
        return grid.ContainsKey(position) && grid[position].isWalkable;
    }

    /// <summary>
    /// Resets pathfinding costs for all nodes in the grid.
    /// </summary>
    private void InitializePathfindingCosts()
    {
        foreach (var node in grid.Values)
        {
            node.gCost = int.MaxValue;
            node.hCost = 0;
            node.parent = null;
        }
    }

    /// <summary>
    /// Reconstructs the final path by backtracking from the end node to the start node.
    /// </summary>
    private List<Vector3Int> RetracePath(Node start, Node end)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        Node current = end;

        while (current != start)
        {
            path.Add(current.position);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Gets the node with the lowest fCost from a list of nodes.
    /// </summary>
    private Node GetLowestFCostNode(List<Node> nodes)
    {
        Node best = nodes[0];
        foreach (Node node in nodes)
        {
            if (node.fCost < best.fCost)
                best = node;
        }
        return best;
    }

    /// <summary>
    /// Heuristic cost estimate between two tiles (Chebyshev distance).
    /// </summary>
    private int GetDistance(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return 10 * Mathf.Max(dx, dy); // Diagonal-friendly
    }

    /// <summary>
    /// Returns 8-directional neighbors of a tile.
    /// </summary>
    private List<Vector3Int> GetNeighbors(Vector3Int pos)
    {
        return new List<Vector3Int>
        {
            pos + Vector3Int.right,
            pos + Vector3Int.left,
            pos + Vector3Int.up,
            pos + Vector3Int.down,
            pos + new Vector3Int(1, 1, 0),
            pos + new Vector3Int(1, -1, 0),
            pos + new Vector3Int(-1, 1, 0),
            pos + new Vector3Int(-1, -1, 0)
        };
    }

    /// <summary>
    /// Exposes the grid for debugging purposes.
    /// </summary>
    public Dictionary<Vector3Int, Node> DebugGetGrid() => grid;
}

/// <summary>
/// Node: Represents a single tile in the pathfinding grid.
/// </summary>
public class Node
{
    public Vector3Int position;  // Tile position on the grid
    public bool isWalkable;      // Can the player move here?
    public int gCost, hCost, fCost; // A* cost values
    public Node parent;          // Back-reference for path retracing

    public Node(Vector3Int pos, bool walkable)
    {
        position = pos;
        isWalkable = walkable;
    }

    public void CalculateFCost() => fCost = gCost + hCost;
}
