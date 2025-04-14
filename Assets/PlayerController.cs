// PlayerController.cs
// Handles player movement using A*, interaction with obstacles (trees, stones), and hover highlighting

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap walkableTilemap;   // Tilemap defining walkable ground
    public Tilemap treeTileMap;       // Tilemap containing tree obstacle tiles
    public Tilemap stoneTileMap;      // Tilemap containing stone obstacle tiles

    [Header("Pathfinding")]
    public Pathfinder pathfinder;     // A* pathfinding system reference
    public LineRenderer pathLine;     // Optional path visualizer

    [Header("Movement")]
    public float speed = 2f;          // Speed at which the player moves

    private Queue<Vector3> pathQueue = new Queue<Vector3>();       // World-space movement targets
    private List<Vector3Int> currentPath = new List<Vector3Int>(); // Tilemap-space path tiles

    private Vector3Int? currentHighlightedTile = null;  // Currently hovered tile (for highlight reset)
    private Vector3Int pendingBreakTile;                // Tile marked for destruction
    private bool isBreakingObstacle = false;            // Flag to trigger break coroutine after path

    private void Start()
    {
        // Auto-assign pathfinder if not already linked
        if (pathfinder == null)
            pathfinder = FindObjectOfType<Pathfinder>();

        pathLine.positionCount = 0; // Clear line at start
    }

    private void Update()
    {
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector3Int hoveredCell = walkableTilemap.WorldToCell(mouseWorld);

        if (Input.GetKeyDown(KeyCode.T))
        {
            Vector3Int testTile = new Vector3Int(0, 0, 0); // Change to where you know a tree is
            Debug.Log($"Tree at (0,0,0)? {treeTileMap.HasTile(testTile)}");
        }

        HandleTileHighlighting(hoveredCell); // Update hover highlight

        if (Input.GetMouseButtonDown(0))
        {
            SetPath(hoveredCell); // Move to location
        }
        else if (Input.GetMouseButtonDown(1))
        {
            TryInitiateBreak(hoveredCell); // Interact with obstacle
        }

        MoveAlongPath(); // Perform movement
    }

    // Converts mouse screen position to world-space (z = 0)
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;
        return pos;
    }

    // Highlight hovered tile if valid
    private void HandleTileHighlighting(Vector3Int tile)
    {
        if (tile == currentHighlightedTile) return;

        // Unhighlight previous tile
        if (currentHighlightedTile.HasValue)
            ResetHighlight(currentHighlightedTile.Value);

        // Highlight if valid (walkable or interactable)
        if (treeTileMap.HasTile(tile) || stoneTileMap.HasTile(tile) || walkableTilemap.HasTile(tile))
        {
            HighlightTile(tile);
            currentHighlightedTile = tile;
        }
        else
        {
            currentHighlightedTile = null;
        }
    }

    // Apply highlight to applicable tilemaps
    private void HighlightTile(Vector3Int pos)
    {
        if (treeTileMap.HasTile(pos)) ApplyHighlight(treeTileMap, pos);
        if (stoneTileMap.HasTile(pos)) ApplyHighlight(stoneTileMap, pos);
        if (walkableTilemap.HasTile(pos)) ApplyHighlight(walkableTilemap, pos);
    }

    // Reset highlight on tile
    private void ResetHighlight(Vector3Int pos)
    {
        if (treeTileMap.HasTile(pos)) ResetTileColor(treeTileMap, pos);
        if (stoneTileMap.HasTile(pos)) ResetTileColor(stoneTileMap, pos);
        if (walkableTilemap.HasTile(pos)) ResetTileColor(walkableTilemap, pos);
    }

    // Set highlight color (translucent white)
    private void ApplyHighlight(Tilemap map, Vector3Int pos)
    {
        map.SetTileFlags(pos, TileFlags.None);
        map.SetColor(pos, new Color(1f, 1f, 1f, 0.5f));
    }

    // Reset tile color to original white
    private void ResetTileColor(Tilemap map, Vector3Int pos)
    {
        map.SetTileFlags(pos, TileFlags.None);
        map.SetColor(pos, Color.white);
    }

    // Handle right-click breaking initiation (tree or stone)
    private void TryInitiateBreak(Vector3Int target)
    {
        if (treeTileMap.HasTile(target) || stoneTileMap.HasTile(target))
        {
            Vector3Int playerPos = walkableTilemap.WorldToCell(transform.position);
            Vector3Int? adjacent = GetNearestWalkableAdjacent(target, playerPos);

            if (adjacent.HasValue)
            {
                SetPath(adjacent.Value); // Move adjacent
                pendingBreakTile = target;
                isBreakingObstacle = true;
            }
        }
    }

    // Finds best adjacent walkable tile for interaction
    private Vector3Int? GetNearestWalkableAdjacent(Vector3Int target, Vector3Int player)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>
        {
            target + Vector3Int.up,
            target + Vector3Int.down,
            target + Vector3Int.left,
            target + Vector3Int.right
        };

        Vector3Int? best = null;
        int bestDist = int.MaxValue;

        foreach (var cell in neighbors)
        {
            if (walkableTilemap.HasTile(cell) && !treeTileMap.HasTile(cell) && !stoneTileMap.HasTile(cell))
            {
                int dist = Mathf.Abs(cell.x - player.x) + Mathf.Abs(cell.y - player.y);
                if (dist < bestDist)
                {
                    best = cell;
                    bestDist = dist;
                }
            }
        }
        return best;
    }

    // Destroys obstacle after a short delay (can support animation later)
    private IEnumerator BreakObstacleAfterDelay(Vector3Int tile)
    {
        yield return new WaitForSeconds(0.5f);

        if (treeTileMap.HasTile(tile)) treeTileMap.SetTile(tile, null);
        if (stoneTileMap.HasTile(tile)) stoneTileMap.SetTile(tile, null);

        pathfinder.RegenerateGrid(); // Rebuild walkability grid
    }

    // Sets a new A* path to a given destination tile
    private void SetPath(Vector3Int destination)
    {
        Vector3Int start = walkableTilemap.WorldToCell(transform.position);
        List<Vector3Int> path = pathfinder.FindPath(start, destination);

        if (path == null || path.Count == 0)
        {
            return;
        }


        pathQueue.Clear();
        currentPath.Clear();
        currentPath = new List<Vector3Int>(path);

        pathLine.positionCount = path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 world = walkableTilemap.GetCellCenterWorld(path[i]);
            pathQueue.Enqueue(world);
            pathLine.SetPosition(i, world);
        }
    }

    // Smoothly moves the character along the current path
    private void MoveAlongPath()
    {
        if (pathQueue.Count == 0)
        {
            if (isBreakingObstacle)
            {
                StartCoroutine(BreakObstacleAfterDelay(pendingBreakTile));
                isBreakingObstacle = false;
            }

            // Reset path data
            currentPath.Clear();
            pathLine.positionCount = 0;
            return;
        }

        Vector3 target = pathQueue.Peek();
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.05f)
        {
            pathQueue.Dequeue();

            if (currentPath.Count > 0)
                currentPath.RemoveAt(0);

            UpdatePathLine();
        }
    }

    // Updates the drawn path after each move
    private void UpdatePathLine()
    {
        if (currentPath.Count == 0)
        {
            pathLine.positionCount = 0;
            return;
        }

        pathLine.positionCount = currentPath.Count;

        for (int i = 0; i < currentPath.Count; i++)
        {
            pathLine.SetPosition(i, walkableTilemap.GetCellCenterWorld(currentPath[i]));
        }
    }
}
