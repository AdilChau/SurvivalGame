// Handles A* pathfinding, avoiding obstacles like trees or stones

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Handles grid-based A* pathfinding, avoiding obstacle tiles
public class Pathfinder : MonoBehaviour
{
    public Tilemap walkableTilemap; // Main ground tilemap used for walkable tiles
    public Tilemap[] obstacleTilemaps; // Array of tilemaps representing obstacles (e.g., trees, stones)

    private Dictionary<Vector3Int, Node> grid = new Dictionary<Vector3Int, Node>(); // Stores the grid with walkability info

    private void Start()
    {
        GenerateGrid(); // Build initial walkability grid
    }

    // Scans the walkable tilemap and marks each tile as walkable or not based on obstacle presence
    private void GenerateGrid()
    {
        BoundsInt bounds = walkableTilemap.cellBounds;
        foreach (var pos in bounds.allPositionsWithin)
        {
            bool isWalkable = walkableTilemap.HasTile(pos) && !IsObstacle(pos);
            grid[pos] = new Node(pos, isWalkable);
        }
    }

    // Clears and rebuilds the grid, useful when obstacles are added/removed at runtime
    public void RegenerateGrid()
    {
        grid.Clear();
        GenerateGrid();
    }

    // Checks all obstacle tilemaps to determine if a tile is blocked
    private bool IsObstacle(Vector3Int position)
    {
        foreach (Tilemap obstacleTilemap in obstacleTilemaps)
        {
            if (obstacleTilemap.HasTile(position)) return true;
        }
        return false;
    }

    // Returns a path using A* algorithm from start to target, or null if no path found
    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int target)
    {
        if (!grid.ContainsKey(target) || !grid[target].isWalkable) return null;

        List<Node> openList = new List<Node> { grid[start] }; // List of nodes to evaluate
        HashSet<Node> closedList = new HashSet<Node>(); // List of nodes already evaluated

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

        return null; // Return null if path couldn't be found
    }

    // Traces the final path from target to start by walking backwards through parent nodes
    private List<Vector3Int> RetracePath(Node start, Node end)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        Node currentNode = end;

        while (currentNode != start)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }

        path.Reverse(); // Reverse so it starts from origin
        return path;
    }

    // Selects the node in the list with the lowest total cost (fCost)
    private Node GetLowestFCostNode(List<Node> nodes)
    {
        Node lowest = nodes[0];
        foreach (var node in nodes)
        {
            if (node.fCost < lowest.fCost) lowest = node;
        }
        return lowest;
    }

    // Heuristic cost estimate using diagonal-friendly max difference
    private int GetDistance(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return 10 * Mathf.Max(dx, dy); // Diagonal movement weight
    }

    // Returns all 8-connected neighbors (including diagonals)
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

// Node class used for pathfinding grid
public class Node
{
    public Vector3Int position; // Tile position on grid
    public bool isWalkable; // Whether the tile is walkable
    public int gCost, hCost, fCost; // Cost values used by A* algorithm
    public Node parent; // Parent node used for path retracing

    public Node(Vector3Int pos, bool walkable)
    {
        position = pos;
        isWalkable = walkable;
    }

    // fCost = gCost + hCost
    public void CalculateFCost()
    {
        fCost = gCost + hCost;
    }
}
