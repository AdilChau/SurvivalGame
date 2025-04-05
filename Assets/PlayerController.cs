// Controls player movement, tile highlighting, and path previewing + obstacle interaction

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    public float speed = 1f; // Player movement speed
    public Tilemap walkableTilemap; // Ground tilemap
    public Tilemap treeTileMap; // Tree obstacles
    public Tilemap stoneTileMap; // Stone obstacles
    public Pathfinder pathfinder; // Reference to the pathfinding system
    public LineRenderer pathLine; // Line preview renderer

    private Queue<Vector3> pathQueue = new Queue<Vector3>(); // Queue of target world positions
    private List<Vector3Int> currentPath = new List<Vector3Int>(); // List of tile positions in path

    private Vector3Int prevHoveredTile; // Previously hovered tile
    private Color originalColor = Color.white; // Default color for unhighlighted tile

    private Vector3Int pendingBreakTile; // Tile marked for breaking
    private bool isBreakingObstacle = false; // Whether we are in the process of breaking

    private void Start()
    {
        pathLine.positionCount = 0; // Clear path preview
        pathfinder = FindObjectOfType<Pathfinder>(); // Find the Pathfinder instance
    }

    private void Update()
    {
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector3Int hoveredTile = walkableTilemap.WorldToCell(mouseWorld);

        HandleTileHighlighting(hoveredTile); // Highlight the tile under mouse

        if (Input.GetMouseButtonDown(0)) // On left click, set path
        {
            SetPath(hoveredTile);
        }

        if (Input.GetMouseButtonDown(1)) // On right click, try to interact
        {
            TryInitiateBreak(hoveredTile);
        }

        MoveAlongPath(); // Move the player each frame
    }

    // Gets current mouse position in world space
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;
        return mousePosition;
    }

    // Highlights the currently hovered tile and resets the last
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

    // Highlights a tile by tinting it
    private void HighlightTile(Tilemap tileMap, Vector3Int tilePos)
    {
        tileMap.SetTileFlags(tilePos, TileFlags.None);
        tileMap.SetColor(tilePos, new Color(1f, 1f, 1f, 0.8f)); // Slightly faded
    }

    // Resets the tile color to original
    private void ResetTileHighlight(Tilemap tileMap, Vector3Int tilePos)
    {
        tileMap.SetTileFlags(tilePos, TileFlags.None);
        tileMap.SetColor(tilePos, originalColor);
    }

    // Attempts to break an obstacle by moving next to it first
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

    // Finds a walkable tile next to the target that is closest to player
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

    // Actually removes the obstacle after delay (for animation)
    private IEnumerator BreakObstacleAfterDelay(Vector3Int tile)
    {
        yield return new WaitForSeconds(0.5f); // Add delay for animation

        if (treeTileMap.HasTile(tile))
        {
            treeTileMap.SetTile(tile, null);
        }
        else if (stoneTileMap.HasTile(tile))
        {
            stoneTileMap.SetTile(tile, null);
        }

        // Update the pathfinding grid to mark this tile as walkable again
        pathfinder.RegenerateGrid();
    }

    // Sets a path from player to target using Pathfinder
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

    // Moves the player step-by-step toward next queued point
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

    // Updates the line renderer as segments are completed
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