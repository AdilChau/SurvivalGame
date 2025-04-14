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
        // Generate the grid at startup
        RegenerateGrid();
        Debug.Log("Grid regenerated.");

    }

    /// <summary>
    /// Rebuilds the walkability grid by scanning the walkable tilemap and obstacle maps.
    /// Called at startup and whenever obstacles are changed (e.g., removed).
    /// </summary>
    public void RegenerateGrid()
    {
        grid = new Dictionary<Vector3Int, Node>();

        // Get combined bounds of walkable and obstacle tilemaps
        BoundsInt bounds = walkableTilemap.cellBounds;
        foreach (var tilemap in obstacleTilemaps)
        {
            bounds.xMin = Mathf.Min(bounds.xMin, tilemap.cellBounds.xMin);
            bounds.yMin = Mathf.Min(bounds.yMin, tilemap.cellBounds.yMin);
            bounds.xMax = Mathf.Max(bounds.xMax, tilemap.cellBounds.xMax);
            bounds.yMax = Mathf.Max(bounds.yMax, tilemap.cellBounds.yMax);
        }

        bounds.xMin -= 2;
        bounds.yMin -= 2;
        bounds.xMax += 2;
        bounds.yMax += 2;

        foreach (var pos in bounds.allPositionsWithin)
        {
            bool walkable = walkableTilemap.HasTile(pos) && !IsObstacle(pos);
            grid[pos] = new Node(pos, walkable);

            if (!walkable)
            {
                Debug.Log($"Blocked tile at {pos} due to obstacle");
            }
        }
    }


    private bool IsObstacle(Vector3Int position)
    {
        foreach (var tilemap in obstacleTilemaps)
        {
            if (tilemap.HasTile(position))
            {
                Debug.Log($"Obstacle found at {position} on {tilemap.name}");
                return true;
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
        // Abort if start or end are not valid walkable tiles
        if (!grid.ContainsKey(end) || !grid[end].isWalkable) return null;
        if (!grid.ContainsKey(start) || !grid[start].isWalkable) return null;

        List<Node> openList = new List<Node> { grid[start] }; // Nodes to be evaluated
        HashSet<Node> closedSet = new HashSet<Node>();        // Nodes already evaluated

        // Reset cost values
        foreach (var node in grid.Values)
        {
            node.gCost = int.MaxValue;
            node.hCost = 0;
            node.parent = null;
        }

        // Initialize start node
        Node startNode = grid[start];
        startNode.gCost = 0;
        startNode.hCost = GetDistance(start, end);
        startNode.CalculateFCost();

        while (openList.Count > 0)
        {
            // Select node with lowest fCost
            Node current = GetLowestFCostNode(openList);

            // Path complete
            if (current.position == end)
                return RetracePath(startNode, current);

            openList.Remove(current);
            closedSet.Add(current);

            // Check valid neighbors
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

        // No valid path found
        return null;
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

        path.Reverse(); // Reverse to get path from start to end
        return path;
    }

    /// <summary>
    /// Gets the node with the lowest fCost from a list of nodes.
    /// </summary>
    private Node GetLowestFCostNode(List<Node> nodes)
    {
        Node lowest = nodes[0];
        foreach (var node in nodes)
        {
            if (node.fCost < lowest.fCost)
                lowest = node;
        }
        return lowest;
    }

    /// <summary>
    /// Heuristic cost estimate between two tiles (Chebyshev distance).
    /// </summary>
    private int GetDistance(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return 10 * Mathf.Max(dx, dy); // Diagonal-friendly distance
    }

    /// <summary>
    /// Returns 4-directional neighbors of a tile (no diagonals).
    /// </summary>
    private List<Vector3Int> GetNeighbors(Vector3Int pos)
    {
        return new List<Vector3Int>
        {
            pos + Vector3Int.right,
            pos + Vector3Int.left,
            pos + Vector3Int.up,
            pos + Vector3Int.down
        };
    }
}

//
// Node: Represents a single tile in the pathfinding grid.
//
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

    // fCost is total estimated cost: gCost (so far) + hCost (estimate to goal)
    public void CalculateFCost() => fCost = gCost + hCost;
}
