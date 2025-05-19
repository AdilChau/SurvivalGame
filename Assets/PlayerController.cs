using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
    public float speed = 2f;

    private Queue<Vector3> pathQueue = new Queue<Vector3>();
    private List<Vector3Int> currentPath = new List<Vector3Int>();

    private Vector3Int? currentHighlightedTile = null;
    private Vector3Int pendingBreakTile;
    private bool isBreakingObstacle = false;

    private List<Vector3Int> lastFadedTreeTiles = new List<Vector3Int>();

    private void Start()
    {
        if (pathfinder == null)
            pathfinder = FindObjectOfType<Pathfinder>();

        if (pathLine != null)
            pathLine.positionCount = 0;

        // 🔍 DEBUG: Print all existing blocker tiles in the grid
        foreach (var pos in treeBlockerTileMap.cellBounds.allPositionsWithin)
        {
            if (treeBlockerTileMap.HasTile(pos))
            {
                Debug.Log($"[TREE BLOCKER] Blocker tile found at {pos}");
            }
        }

        foreach (var pos in treeBlockerTileMap.cellBounds.allPositionsWithin)
        {
            if (treeBlockerTileMap.HasTile(pos))
            {
                Debug.Log($"[TREE BLOCKER] Blocker tile found at {pos} | Z = {pos.z}");
            }
        }
    }

    private void Update()
    {
        Vector3Int hovered = walkableTilemap.WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        Debug.Log($"[HOVER] Mouse is over cell: {hovered}");
        
        Vector3Int hoveredCell = GetClickedCell();

        HandleTileHighlighting(hoveredCell);

        if (Input.GetMouseButtonDown(0))
        {
            SetPath(hoveredCell);
        }
        else if (Input.GetMouseButtonDown(1))
        {
            TryInitiateBreak(hoveredCell);
        }

        MoveAlongPath();
    }

    #region Input & Highlighting

    /// <summary>
    /// Converts mouse screen position to the corresponding cell on the tilemap.
    /// </summary>
    private Vector3Int GetClickedCell()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;
        return walkableTilemap.WorldToCell(mouseWorldPos);
    }

    /// <summary>
    /// Highlights hovered tiles or trees.
    /// </summary>
    private void HandleTileHighlighting(Vector3Int tile)
    {
        if (tile == currentHighlightedTile) return;

        if (currentHighlightedTile.HasValue)
            ResetHighlight(currentHighlightedTile.Value);

        Vector3Int? root = FindTreeRootNear(tile);
        if (root.HasValue)
        {
            HighlightEntireTree(root.Value);
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

    private void HighlightEntireTree(Vector3Int root)
    {
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int check = root + new Vector3Int(x, y, 0);
                if (treeTileMap.HasTile(check))
                    ApplyHighlight(treeTileMap, check);
            }
    }

    private void HighlightTile(Vector3Int pos)
    {
        if (treeTileMap.HasTile(pos)) ApplyHighlight(treeTileMap, pos);
        if (stoneTileMap.HasTile(pos)) ApplyHighlight(stoneTileMap, pos);
        if (walkableTilemap.HasTile(pos)) ApplyHighlight(walkableTilemap, pos);
    }

    private void ResetHighlight(Vector3Int pos)
    {
        Vector3Int? root = FindTreeRootNear(pos);
        if (root.HasValue)
        {
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                {
                    Vector3Int check = root.Value + new Vector3Int(x, y, 0);
                    if (treeTileMap.HasTile(check))
                        ResetTileColor(treeTileMap, check);
                }
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
    /// Initiates walking toward a tree/stone to remove it.
    /// </summary>
    private void TryInitiateBreak(Vector3Int target)
    {
        Vector3Int? root = FindTreeRootNear(target);

        if (root.HasValue || stoneTileMap.HasTile(target))
        {
            Vector3Int playerPos = walkableTilemap.WorldToCell(transform.position);
            Vector3Int interactionTile = root.HasValue ? root.Value : target;
            pendingBreakTile = interactionTile;
            Debug.Log($"[BREAK INIT] Targeting {interactionTile} as obstacle tile");

            Vector3Int? adjacent = GetNearestWalkableAdjacent(interactionTile, playerPos);

            if (adjacent.HasValue)
            {
                SetPath(adjacent.Value);
                pendingBreakTile = interactionTile;
                isBreakingObstacle = true;
            }
        }
    }

    /// <summary>
    /// Searches a 3x3 grid around the clicked tile for a tree root.
    /// </summary>
    private Vector3Int? FindTreeRootNear(Vector3Int clickedCell)
    {
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int check = clickedCell + new Vector3Int(x, y, 0);
                if (treeTileMap.HasTile(check))
                    return check;
            }
        return null;
    }

    /// <summary>
    /// Finds the nearest walkable adjacent tile to interact with the obstacle.
    /// </summary>
    private Vector3Int? GetNearestWalkableAdjacent(Vector3Int target, Vector3Int player)
    {
        List<Vector3Int> directions = new List<Vector3Int>
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
        };

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

    private IEnumerator BreakObstacleAfterDelay(Vector3Int tile)
    {
        Debug.Log($"[TRACE] BreakObstacleAfterDelay is running from: {this.GetType().Name}");
        Debug.Log($"[BREAK] Starting obstacle break at {tile}");

        yield return new WaitForSeconds(0.5f);

        bool treeRemoved = false;

        // Remove tree tile
        if (treeTileMap.HasTile(tile))
        {
            treeTileMap.SetTile(tile, null);
            treeRemoved = true;
            Debug.Log($"[BREAK] Tree removed at {tile}");
        }

        // Remove stone tile
        if (stoneTileMap.HasTile(tile))
        {
            stoneTileMap.SetTile(tile, null);
            Debug.Log($"[BREAK] Stone removed at {tile}");
        }

        // 🆕 DEBUG all blocker tile positions
        Debug.Log("[BLOCKER VERIFY] Checking all blocker tile positions...");
        foreach (var pos in treeBlockerTileMap.cellBounds.allPositionsWithin)
        {
            if (treeBlockerTileMap.HasTile(pos))
            {
                Debug.Log($"[BLOCKER VERIFY] Tile exists at {pos}");
            }
        }

        // 🔍 Check nearby for blocker tiles
        bool blockerRemoved = false;
        Debug.Log($"[BLOCKER REMOVAL] Scanning for blocker tile near {tile}");

        for (int x = -3; x <= 3; x++)
        {
            for (int y = -3; y <= 3; y++)
            {
                Vector3Int check = tile + new Vector3Int(x, y, 0);
                bool hasBlocker = treeBlockerTileMap.HasTile(check);
                Debug.Log($"[BLOCKER REMOVAL] Checking {check}... Has tile? {hasBlocker}");

                if (hasBlocker)
                {
                    treeBlockerTileMap.SetTile(check, null);
                    Debug.Log($"[BLOCKER REMOVAL] ✅ Removed blocker at {check}");
                    blockerRemoved = true;
                }
            }
        }

        if (!blockerRemoved)
        {
            Debug.Log($"[BLOCKER REMOVAL] ❌ No blocker tile found in 3x3 area around {tile}");
        }

        // Restore ground tile
        if (treeRemoved && !walkableTilemap.HasTile(tile) && defaultGrassTile != null)
        {
            walkableTilemap.SetTile(tile, defaultGrassTile);
            Debug.Log($"[GRASS RESTORE] Grass placed at {tile}");
        }

        // Refresh pathfinding
        pathfinder.RegenerateGrid();
    }




    #endregion

    #region Pathfinding & Movement

    private void SetPath(Vector3Int destination)
    {
        Vector3Int start = walkableTilemap.WorldToCell(transform.position);
        List<Vector3Int> path = pathfinder.FindPath(start, destination);

        if (path == null || path.Count == 0)
            return;

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
    /// Makes tree tiles near the player semi-transparent for visibility.
    /// </summary>
    private void HandleTreeTransparency()
    {
        foreach (var tilePos in lastFadedTreeTiles)
        {
            if (treeTileMap.HasTile(tilePos))
            {
                treeTileMap.SetTileFlags(tilePos, TileFlags.None);
                treeTileMap.SetColor(tilePos, Color.white);
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
