using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Handles player movement, pathfinding interaction, and tile highlighting in a grid-based isometric environment
public class PlayerController : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap walkableTilemap;      // Tilemap for walkable ground
    public Tilemap treeTileMap;          // Tilemap for tree obstacles (trunks only)
    public Tilemap stoneTileMap;         // Tilemap for stone obstacles
    public Tilemap treeVisualTileMap;


    [Header("Pathfinding")]
    public Pathfinder pathfinder;        // Reference to the Pathfinder system
    public LineRenderer pathLine;        // Line renderer to visualize path

    [Header("Movement")]
    public float speed = 1f;             // Movement speed of the player

    private Queue<Vector3> pathQueue = new Queue<Vector3>();      // Queue of target positions in world space
    private List<Vector3Int> currentPath = new List<Vector3Int>(); // List of tile positions in the path

    private Vector3Int prevHoveredTile;                          // Previously highlighted tile
    private Color originalColor = Color.white;                   // Default tile color

    private Vector3Int pendingBreakTile;                         // Tile selected for interaction
    private bool isBreakingObstacle = false;                     // Whether the player is trying to break an obstacle

    private void Start()
    {
        pathLine.positionCount = 0;                              // Clear any previous line path
        pathfinder = FindObjectOfType<Pathfinder>();            // Auto-assign pathfinder if not set
    }

    private void Update()
    {
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector3Int hoveredTile = walkableTilemap.WorldToCell(mouseWorld);

        HandleTileHighlighting(hoveredTile);                    // Update tile highlighting as mouse moves

        if (Input.GetMouseButtonDown(0))                        // Left click to move
        {
            SetPath(hoveredTile);
        }

        if (Input.GetMouseButtonDown(1))                        // Right click to interact with obstacle
        {
            TryInitiateBreak(hoveredTile);
        }

        MoveAlongPath();                                       // Move along current path if any
    }

    // Converts screen mouse position to world position
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;
        return mousePosition;
    }

    // Highlights the currently hovered tile and unhighlights the previously hovered one
    private void HandleTileHighlighting(Vector3Int hoveredTile)
    {
        if (hoveredTile != prevHoveredTile)
        {
            if (walkableTilemap.HasTile(prevHoveredTile))
                ResetTileHighlight(walkableTilemap, prevHoveredTile);

            if (treeTileMap.HasTile(prevHoveredTile))
                ResetTileHighlight(treeTileMap, prevHoveredTile);

            if (stoneTileMap.HasTile(prevHoveredTile))
                ResetTileHighlight(stoneTileMap, prevHoveredTile);

            if (walkableTilemap.HasTile(hoveredTile))
                HighlightTile(walkableTilemap, hoveredTile);

            if (treeTileMap.HasTile(hoveredTile))
                HighlightTile(treeTileMap, hoveredTile);

            if (stoneTileMap.HasTile(hoveredTile))
                HighlightTile(stoneTileMap, hoveredTile);

            prevHoveredTile = hoveredTile;
        }
    }

    // Applies a semi-transparent highlight to a tile
    private void HighlightTile(Tilemap tileMap, Vector3Int tilePos)
    {
        tileMap.SetTileFlags(tilePos, TileFlags.None);
        tileMap.SetColor(tilePos, new Color(1f, 1f, 1f, 0.8f));
    }

    // Resets the tile color back to default
    private void ResetTileHighlight(Tilemap tileMap, Vector3Int tilePos)
    {
        tileMap.SetTileFlags(tilePos, TileFlags.None);
        tileMap.SetColor(tilePos, originalColor);
    }

    // Attempts to move next to an obstacle and prepare to break it
    private void TryInitiateBreak(Vector3Int obstacleTile)
    {
        if (treeTileMap.HasTile(obstacleTile) || stoneTileMap.HasTile(obstacleTile))
        {
            Vector3Int playerTile = walkableTilemap.WorldToCell(transform.position);
            Vector3Int? targetAdjacent = GetNearestWalkableAdjacent(obstacleTile, playerTile);

            if (targetAdjacent.HasValue)
            {
                SetPath(targetAdjacent.Value);
                pendingBreakTile = obstacleTile;
                isBreakingObstacle = true;
            }
        }
    }

    // Finds a walkable tile next to the obstacle that is closest to the player
    private Vector3Int? GetNearestWalkableAdjacent(Vector3Int obstacle, Vector3Int player)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>
        {
            obstacle + Vector3Int.up,
            obstacle + Vector3Int.down,
            obstacle + Vector3Int.left,
            obstacle + Vector3Int.right
        };

        Vector3Int? best = null;
        int bestDist = int.MaxValue;

        foreach (var tile in neighbors)
        {
            if (walkableTilemap.HasTile(tile) && !treeTileMap.HasTile(tile) && !stoneTileMap.HasTile(tile))
            {
                int dist = Mathf.Abs(tile.x - player.x) + Mathf.Abs(tile.y - player.y);
                if (dist < bestDist)
                {
                    best = tile;
                    bestDist = dist;
                }
            }
        }

        return best;
    }

    // Coroutine to delay the removal of an obstacle (for future animation)
    private IEnumerator BreakObstacleAfterDelay(Vector3Int tile)
    {
        yield return new WaitForSeconds(0.5f); // Delay for animation timing

        if (treeTileMap.HasTile(tile))
        {
            // We assume this tile is the bottom-left (trunk) of the tree
            Vector3Int[] treeParts = new Vector3Int[]
            {
                tile,                             // Bottom Left (trunk)
                tile + new Vector3Int(1, 0, 0),   // Bottom Right
                tile + new Vector3Int(0, 1, 0),   // Top Left
                tile + new Vector3Int(1, 1, 0)    // Top Right
            };

            foreach (var pos in treeParts)
            {
                treeTileMap.SetTile(pos, null);
                treeVisualTileMap.SetTile(pos, null); // Visual canopy
            }
        }
        else if (stoneTileMap.HasTile(tile))
        {
            stoneTileMap.SetTile(tile, null);
        }

        // Rebuild the pathfinding grid after removing an obstacle
        pathfinder.RegenerateGrid();
    }

    // Finds a path and enqueues movement positions
    private void SetPath(Vector3Int destination)
    {
        Vector3Int start = walkableTilemap.WorldToCell(transform.position);
        List<Vector3Int> path = pathfinder.FindPath(start, destination);

        if (path == null) return; // No path found

        pathQueue.Clear();
        currentPath = path;
        pathLine.positionCount = path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 worldPos = walkableTilemap.GetCellCenterWorld(path[i]);
            pathQueue.Enqueue(worldPos);
            pathLine.SetPosition(i, worldPos);
        }
    }

    // Moves the player toward the next point in the queue
    private void MoveAlongPath()
    {
        if (pathQueue.Count == 0)
        {
            if (isBreakingObstacle)
            {
                StartCoroutine(BreakObstacleAfterDelay(pendingBreakTile));
                isBreakingObstacle = false;
            }
            return;
        }

        Vector3 target = pathQueue.Peek();
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.05f)
        {
            pathQueue.Dequeue();
            UpdatePathLine();
        }
    }

    // Updates the visual path line as the player walks
    private void UpdatePathLine()
    {
        if (currentPath.Count > 0)
        {
            currentPath.RemoveAt(0);

            if (currentPath.Count == 0)
            {
                pathLine.positionCount = 0;
            }
            else
            {
                pathLine.positionCount = currentPath.Count;
                for (int i = 0; i < currentPath.Count; i++)
                {
                    pathLine.SetPosition(i, walkableTilemap.GetCellCenterWorld(currentPath[i]));
                }
            }
        }
    }
}