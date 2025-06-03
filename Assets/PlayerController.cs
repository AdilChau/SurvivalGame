using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Handles player input, movement, obstacle interaction, pathfinding, and visual feedback on a tilemap grid.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap walkableTilemap;
    public Tilemap treeTileMap;
    public Tilemap stoneTileMap;
    public Tilemap treeBlockerTileMap;

    [Header("Tile References")]
    public TileBase defaultGrassTile;

    [Header("Pathfinding")]
    public Pathfinder pathfinder;
    public LineRenderer pathLine;

    [Header("Movement")]
    public float speed = 1.5f;

    private Queue<Vector3> pathQueue = new();
    private List<Vector3Int> currentPath = new();
    private Vector3Int? currentHighlightedTile = null;
    private Vector3Int pendingBreakTile;
    private bool isBreakingObstacle = false;
    private List<Vector3Int> lastFadedTreeTiles = new();
    private Animator animator;
    private Vector3 lastPosition;

    private void Start()
    {
        // Animation setup
        animator = GetComponent<Animator>();
        lastPosition = transform.position;

        // Automatically assign pathfinder if not manually set
        pathfinder ??= FindObjectOfType<Pathfinder>();

        // Clear path line at start
        if (pathLine != null) pathLine.positionCount = 0;
    }

    private void Update()
    {
        Vector3Int hoveredCell = GetClickedCell();

        HandleTileHighlighting(hoveredCell);

        // Left click for movement
        if (Input.GetMouseButtonDown(0))
            SetPath(hoveredCell);
        // Right click to interact with obstacle
        else if (Input.GetMouseButtonDown(1))
            TryInitiateBreak(hoveredCell);

        MoveAlongPath();
        HandleIdleAnimation();
    }

    #region Input & Highlighting

    /// <summary>
    /// Converts current mouse position to corresponding tilemap cell.
    /// </summary>
    private Vector3Int GetClickedCell()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;
        return walkableTilemap.WorldToCell(mouseWorldPos);
    }

    /// <summary>
    /// Highlights relevant tiles under the cursor (tree, stone, or walkable).
    /// </summary>
    private void HandleTileHighlighting(Vector3Int tile)
    {
        if (tile == currentHighlightedTile) return;

        if (currentHighlightedTile.HasValue)
            ResetHighlight(currentHighlightedTile.Value);

        Vector3Int? root = FindTreeRootNear(tile);
        if (root.HasValue)
        {
            HighlightGroup(treeTileMap, root.Value);
            currentHighlightedTile = root.Value;
        }
        else if (stoneTileMap.HasTile(tile) || walkableTilemap.HasTile(tile))
        {
            HighlightTile(tile);
            currentHighlightedTile = tile;
        }
        else
        {
            currentHighlightedTile = null;
        }
    }

    /// <summary>
    /// Highlights a 3x3 group centered around the specified tile.
    /// </summary>
    private void HighlightGroup(Tilemap map, Vector3Int root)
    {
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int pos = root + new Vector3Int(x, y, 0);
                if (map.HasTile(pos)) ApplyHighlight(map, pos);
            }
    }

    /// <summary>
    /// Highlights a single tile if present in any relevant map.
    /// </summary>
    private void HighlightTile(Vector3Int pos)
    {
        if (treeTileMap.HasTile(pos)) ApplyHighlight(treeTileMap, pos);
        if (stoneTileMap.HasTile(pos)) ApplyHighlight(stoneTileMap, pos);
        if (walkableTilemap.HasTile(pos)) ApplyHighlight(walkableTilemap, pos);
    }

    /// <summary>
    /// Resets highlight of previously highlighted tiles.
    /// </summary>
    private void ResetHighlight(Vector3Int pos)
    {
        Vector3Int? root = FindTreeRootNear(pos);
        if (root.HasValue)
        {
            HighlightGroup(treeTileMap, root.Value);
        }
        else
        {
            if (stoneTileMap.HasTile(pos)) ResetTileColor(stoneTileMap, pos);
            if (walkableTilemap.HasTile(pos)) ResetTileColor(walkableTilemap, pos);
        }
    }

    private void ApplyHighlight(Tilemap map, Vector3Int pos)
    {
        map.SetTileFlags(pos, TileFlags.None);
        map.SetColor(pos, new Color(1f, 1f, 1f, 0.5f));
    }

    private void ResetTileColor(Tilemap map, Vector3Int pos)
    {
        map.SetTileFlags(pos, TileFlags.None);
        map.SetColor(pos, Color.white);
    }

    #endregion

    #region Breaking Obstacles

    /// <summary>
    /// Starts the process to break an obstacle tile (tree or stone).
    /// </summary>
    private void TryInitiateBreak(Vector3Int target)
    {
        Vector3Int? root = FindTreeRootNear(target);
        if (!root.HasValue && !stoneTileMap.HasTile(target)) return;

        Vector3Int playerPos = walkableTilemap.WorldToCell(transform.position);
        Vector3Int interactionTile = root ?? target;
        pendingBreakTile = interactionTile;

        Vector3Int? adjacent = GetNearestWalkableAdjacent(interactionTile, playerPos);
        if (adjacent.HasValue)
        {
            SetPath(adjacent.Value);
            isBreakingObstacle = true;
        }
    }

    /// <summary>
    /// Checks for tree root within a 3x3 area around the given cell.
    /// </summary>
    private Vector3Int? FindTreeRootNear(Vector3Int clickedCell)
    {
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int check = clickedCell + new Vector3Int(x, y, 0);
                if (treeTileMap.HasTile(check)) return check;
            }
        return null;
    }

    /// <summary>
    /// Gets the nearest adjacent walkable cell to the target.
    /// </summary>
    private Vector3Int? GetNearestWalkableAdjacent(Vector3Int target, Vector3Int player)
    {
        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        Vector3Int? best = null;
        int bestDist = int.MaxValue;

        foreach (var dir in directions)
        {
            Vector3Int cell = target + dir;
            if (walkableTilemap.HasTile(cell) &&
                !treeTileMap.HasTile(cell) &&
                !stoneTileMap.HasTile(cell))
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

    /// <summary>
    /// Coroutine that waits briefly, then removes obstacle tiles.
    /// </summary>
    private IEnumerator BreakObstacleAfterDelay(Vector3Int tile)
    {
        yield return new WaitForSeconds(0.5f);

        bool treeRemoved = false;

        if (treeTileMap.HasTile(tile))
        {
            treeTileMap.SetTile(tile, null);
            treeRemoved = true;
        }

        if (stoneTileMap.HasTile(tile))
        {
            stoneTileMap.SetTile(tile, null);
        }

        Vector3Int blockerOffset = new(-2, -2, 0);
        Vector3Int blockerPos = tile + blockerOffset;

        if (treeBlockerTileMap.HasTile(blockerPos))
        {
            treeBlockerTileMap.SetTile(blockerPos, null);
        }

        if (treeRemoved && !walkableTilemap.HasTile(tile) && defaultGrassTile != null)
        {
            walkableTilemap.SetTile(tile, defaultGrassTile);
        }

        pathfinder.RegenerateGrid();
    }

    #endregion

    #region Pathfinding & Movement

    /// <summary>
    /// Calculates and displays a path to the destination cell.
    /// </summary>
    private void SetPath(Vector3Int destination)
    {
        Vector3Int start = walkableTilemap.WorldToCell(transform.position);
        List<Vector3Int> path = pathfinder.FindPath(start, destination);

        if (path == null || path.Count == 0) return;

        pathQueue.Clear();
        currentPath = new List<Vector3Int>(path);

        if (pathLine != null)
        {
            pathLine.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 world = walkableTilemap.GetCellCenterWorld(path[i]);
                pathQueue.Enqueue(world);
                pathLine.SetPosition(i, world);
            }
        }
    }

    /// <summary>
    /// Moves the player along the current path if available.
    /// </summary>
    private void MoveAlongPath()
    {
        if (pathQueue.Count == 0)
        {
            if (isBreakingObstacle)
            {
                StartCoroutine(BreakObstacleAfterDelay(pendingBreakTile));
                isBreakingObstacle = false;
            }

            currentPath.Clear();
            if (pathLine != null) pathLine.positionCount = 0;

            HandleTreeTransparency();
            return;
        }

        Vector3 target = pathQueue.Peek();
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            transform.position = target;
            pathQueue.Dequeue();

            if (currentPath.Count > 0)
                currentPath.RemoveAt(0);

            UpdatePathLine();
        }
    }

    /// <summary>
    /// Handles the idle animation
    /// <summary>
    private void HandleIdleAnimation() 
    {
        bool isIdle = Vector3.Distance(transform.position, lastPosition) < 0.001f;
        animator.SetBool("IsIdle", isIdle);
        lastPosition = transform.position;
    }

    /// <summary>
    /// Updates the visual path line as the player moves.
    /// </summary>
    private void UpdatePathLine()
    {
        if (pathLine == null || currentPath.Count == 0)
        {
            if (pathLine != null) pathLine.positionCount = 0;
            return;
        }

        pathLine.positionCount = currentPath.Count;
        for (int i = 0; i < currentPath.Count; i++)
        {
            pathLine.SetPosition(i, walkableTilemap.GetCellCenterWorld(currentPath[i]));
        }
    }

    #endregion

    #region Visual Feedback

    /// <summary>
    /// Makes nearby tree tiles semi-transparent for better player visibility.
    /// </summary>
    private void HandleTreeTransparency()
    {
        foreach (var pos in lastFadedTreeTiles)
        {
            if (treeTileMap.HasTile(pos))
            {
                treeTileMap.SetTileFlags(pos, TileFlags.None);
                treeTileMap.SetColor(pos, Color.white);
            }
        }

        lastFadedTreeTiles.Clear();
        Vector3Int playerCell = treeTileMap.WorldToCell(transform.position);

        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 2; y++)
            {
                Vector3Int check = playerCell + new Vector3Int(x, y, 0);
                if (treeTileMap.HasTile(check))
                {
                    treeTileMap.SetTileFlags(check, TileFlags.None);
                    treeTileMap.SetColor(check, new Color(1f, 1f, 1f, 0.4f));
                    lastFadedTreeTiles.Add(check);
                }
            }
    }

    #endregion
}
