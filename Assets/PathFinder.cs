// Handles A* pathfinding, avoiding obstacles like trees or stones

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Pathfinder : MonoBehaviour
{
    public Tilemap walkableTilemap; // Main ground tilemap
    public Tilemap[] obstacleTilemaps; // Tilemaps that represent obstacles

    private Dictionary<Vector3Int, Node> grid = new Dictionary<Vector3Int, Node>();

    private void Start()
    {
        GenerateGrid(); // Initialize grid at startup
    }

    // Build a dictionary of all walkable tiles
    private void GenerateGrid()
    {
        BoundsInt bounds = walkableTilemap.cellBounds;
        foreach (var pos in bounds.allPositionsWithin)
        {
            bool isWalkable = walkableTilemap.HasTile(pos) && !IsObstacle(pos);
            grid[pos] = new Node(pos, isWalkable);
        }
    }

    // Public method to regenerate the pathfinding grid (after tile changes)
    public void RegenerateGrid()
    {
        grid.Clear();          // Clear old data
        GenerateGrid();        // Rebuild grid from tilemaps
    }


    // Check if any obstacle tilemaps have a tile at this position
    private bool IsObstacle(Vector3Int position)
    {
        foreach (Tilemap obstacleTilemap in obstacleTilemaps)
        {
            if (obstacleTilemap.HasTile(position)) return true;
        }
        return false;
    }

    // A* pathfinding algorithm
    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int target)
    {
        if (!grid.ContainsKey(target) || !grid[target].isWalkable) return null;

        List<Node> openList = new List<Node> { grid[start] };
        HashSet<Node> closedList = new HashSet<Node>();

        foreach (Node node in grid.Values)
        {
            node.gCost = int.MaxValue;
            node.parent = null;
        }

        grid[start].gCost = 0;
        grid[start].hCost = GetDistance(start, target);
        grid[start].CalculateFCost();

        while (openList.Count > 0)
        {
            Node currentNode = GetLowestFCostNode(openList);

            if (currentNode.position == target)
                return RetracePath(grid[start], currentNode);

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            foreach (Vector3Int neighborPos in GetNeighbors(currentNode.position))
            {
                if (!grid.ContainsKey(neighborPos)) continue;

                Node neighbor = grid[neighborPos];
                if (!neighbor.isWalkable || closedList.Contains(neighbor)) continue;

                int tentativeGCost = currentNode.gCost + GetDistance(currentNode.position, neighbor.position);
                if (tentativeGCost < neighbor.gCost)
                {
                    neighbor.gCost = tentativeGCost;
                    neighbor.hCost = GetDistance(neighbor.position, target);
                    neighbor.CalculateFCost();
                    neighbor.parent = currentNode;

                    if (!openList.Contains(neighbor)) openList.Add(neighbor);
                }
            }
        }

        return null; // No valid path found
    }

    // Get path by walking back through parents
    private List<Vector3Int> RetracePath(Node start, Node end)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        Node currentNode = end;

        while (currentNode != start)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }

        path.Reverse();
        return path;
    }

    // Choose the node with the lowest F-cost (G + H)
    private Node GetLowestFCostNode(List<Node> nodes)
    {
        Node lowest = nodes[0];
        foreach (var node in nodes)
        {
            if (node.fCost < lowest.fCost) lowest = node;
        }
        return lowest;
    }

    // Use diagonal-friendly heuristic
    private int GetDistance(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return 10 * Mathf.Max(dx, dy);
    }

    // Get 8-directional neighboring tiles
    private List<Vector3Int> GetNeighbors(Vector3Int pos)
    {
        return new List<Vector3Int>
        {
            pos + new Vector3Int(1, 0, 0),
            pos + new Vector3Int(-1, 0, 0),
            pos + new Vector3Int(0, 1, 0),
            pos + new Vector3Int(0, -1, 0),
            pos + new Vector3Int(1, 1, 0),
            pos + new Vector3Int(-1, -1, 0),
            pos + new Vector3Int(-1, 1, 0),
            pos + new Vector3Int(1, -1, 0)
        };
    }
}

// Helper class for pathfinding
public class Node
{
    public Vector3Int position;
    public bool isWalkable;
    public int gCost, hCost, fCost;
    public Node parent;

    public Node(Vector3Int pos, bool walkable)
    {
        position = pos;
        isWalkable = walkable;
    }

    public void CalculateFCost()
    {
        fCost = gCost + hCost;
    }
}
