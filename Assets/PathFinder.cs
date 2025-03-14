using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Pathfinder : MonoBehaviour
{
    public Tilemap walkableTilemap; // The tilemap containing walkable tiles (e.g., ground)
    public Tilemap[] obstacleTilemaps; // Array of tilemaps that contain obstacles (e.g., trees, stones)

    private Dictionary<Vector3Int, Node> grid = new Dictionary<Vector3Int, Node>(); // Stores the pathfinding grid

    private void Start()
    {
        GenerateGrid(); // Build the grid when the game starts
    }

    // Generates the grid based on the tilemaps
    private void GenerateGrid()
    {
        BoundsInt bounds = walkableTilemap.cellBounds;

        foreach (var pos in bounds.allPositionsWithin)
        {
            bool isWalkable = walkableTilemap.HasTile(pos) && !IsObstacle(pos);
            grid[pos] = new Node(pos, isWalkable);
        }
    }

    // Checks if a given tile position is an obstacle
    private bool IsObstacle(Vector3Int position)
    {
        foreach (Tilemap obstacleTilemap in obstacleTilemaps)
        {
            if (obstacleTilemap.HasTile(position)) return true;
        }
        return false;
    }

    // Finds a path using the A* algorithm from the start position to the target position
    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int target)
    {
        // Ensure the target position is valid
        if (!grid.ContainsKey(target) || !grid[target].isWalkable) return null;

        List<Node> openList = new List<Node> { grid[start] }; // List of nodes to be evaluated
        HashSet<Node> closedList = new HashSet<Node>(); // Set of nodes already evaluated

        // Initialize the grid nodes
        foreach (Node node in grid.Values)
        {
            node.gCost = int.MaxValue;
            node.parent = null;
        }

        // Set the starting node costs
        grid[start].gCost = 0;
        grid[start].hCost = GetDistance(start, target);
        grid[start].CalculateFCost();

        while (openList.Count > 0)
        {
            Node currentNode = GetLowestFCostNode(openList);

            // If we reach the target, retrace the path
            if (currentNode.position == target)
            {
                return RetracePath(grid[start], currentNode);
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            // Check all neighbors of the current node
            foreach (Vector3Int neighborPos in GetNeighbors(currentNode.position))
            {
                if (!grid.ContainsKey(neighborPos) || closedList.Contains(grid[neighborPos])) continue;

                Node neighbor = grid[neighborPos];
                if (!neighbor.isWalkable) continue; // Ignore obstacles

                int tentativeGCost = currentNode.gCost + GetDistance(currentNode.position, neighbor.position);
                if (tentativeGCost < neighbor.gCost)
                {
                    // Update the neighbor's cost and set its parent
                    neighbor.gCost = tentativeGCost;
                    neighbor.hCost = GetDistance(neighbor.position, target);
                    neighbor.CalculateFCost();
                    neighbor.parent = currentNode;

                    // Add the neighbor to the open list if it's not already in it
                    if (!openList.Contains(neighbor)) openList.Add(neighbor);
                }
            }
        }
        return null; // No valid path found
    }

    // Retraces the path from the end node back to the start
    private List<Vector3Int> RetracePath(Node start, Node end)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        Node currentNode = end;

        while (currentNode != start)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }

        path.Reverse(); // Reverse the path to get the correct order
        return path;
    }

    // Finds the node with the lowest F cost in the open list
    private Node GetLowestFCostNode(List<Node> nodes)
    {
        Node lowest = nodes[0];
        foreach (var node in nodes)
        {
            if (node.fCost < lowest.fCost) lowest = node;
        }
        return lowest;
    }

    // Calculates the distance between two tile positions (Manhattan Distance for grid-based pathfinding)
    private int GetDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // Gets the valid neighboring tiles for a given tile position
    private List<Vector3Int> GetNeighbors(Vector3Int pos)
    {
        return new List<Vector3Int>
        {
            pos + new Vector3Int(1, 0, 0), // Right
            pos + new Vector3Int(-1, 0, 0), // Left
            pos + new Vector3Int(0, 1, 0), // Up
            pos + new Vector3Int(0, -1, 0) // Down
        };
    }
}

// Node class used for A* pathfinding
public class Node
{
    public Vector3Int position; // Position in the tilemap grid
    public bool isWalkable; // Whether the tile is walkable or an obstacle
    public int gCost, hCost, fCost; // Pathfinding costs
    public Node parent; // Parent node for path retracing

    public Node(Vector3Int pos, bool walkable)
    {
        position = pos;
        isWalkable = walkable;
    }

    // Calculates the total F cost (G cost + H cost)
    public void CalculateFCost()
    {
        fCost = gCost + hCost;
    }
}
