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

    private void Start()
    {
        if (pathfinder == null)
            pathfinder = FindObjectOfType<Pathfinder>();

        if (pathLine != null)
            pathLine.positionCount = 0;
    }

    private void Update()
    {
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

    private Vector3Int GetClickedCell()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPos.z = 0f;
        Vector3Int cellPos = walkableTilemap.WorldToCell(mouseWorldPos);
        return cellPos;
    }

    private void HandleTileHighlighting(Vector3Int tile)
    {
        if (tile == currentHighlightedTile) return;

        if (currentHighlightedTile.HasValue)
            ResetHighlight(currentHighlightedTile.Value);

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

    private void HighlightTile(Vector3Int pos)
    {
        if (treeTileMap.HasTile(pos)) ApplyHighlight(treeTileMap, pos);
        if (stoneTileMap.HasTile(pos)) ApplyHighlight(stoneTileMap, pos);
        if (walkableTilemap.HasTile(pos)) ApplyHighlight(walkableTilemap, pos);
    }

    private void ResetHighlight(Vector3Int pos)
    {
        if (treeTileMap.HasTile(pos)) ResetTileColor(treeTileMap, pos);
        if (stoneTileMap.HasTile(pos)) ResetTileColor(stoneTileMap, pos);
        if (walkableTilemap.HasTile(pos)) ResetTileColor(walkableTilemap, pos);
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

    private void TryInitiateBreak(Vector3Int target)
    {
        if (treeTileMap.HasTile(target) || stoneTileMap.HasTile(target))
        {
            Vector3Int playerPos = walkableTilemap.WorldToCell(transform.position);
            Vector3Int? adjacent = GetNearestWalkableAdjacent(target, playerPos);

            if (adjacent.HasValue)
            {
                SetPath(adjacent.Value);
                pendingBreakTile = target;
                isBreakingObstacle = true;
            }
        }
    }

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

    private IEnumerator BreakObstacleAfterDelay(Vector3Int tile)
    {
        yield return new WaitForSeconds(0.5f);

        if (treeTileMap.HasTile(tile)) treeTileMap.SetTile(tile, null);
        if (stoneTileMap.HasTile(tile)) stoneTileMap.SetTile(tile, null);

        pathfinder.RegenerateGrid();
    }

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

        if (pathLine != null)
            pathLine.positionCount = path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 world = walkableTilemap.GetCellCenterWorld(path[i]);
            pathQueue.Enqueue(world);

            if (pathLine != null)
                pathLine.SetPosition(i, world);
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
            if (pathLine != null)
                pathLine.positionCount = 0;
            return;
        }

        Vector3 target = pathQueue.Peek();
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            transform.position = target; // Snap exactly
            pathQueue.Dequeue();

            if (currentPath.Count > 0)
                currentPath.RemoveAt(0);

            UpdatePathLine();
        }
    }

    private void UpdatePathLine()
    {
        if (pathLine == null) return;

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
