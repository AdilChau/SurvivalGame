// Controls player movement, tile highlighting, and path previewing

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    public float speed = 2f; // Player movement speed
    public Tilemap walkableTilemap; // Ground tilemap
    public Tilemap treeTileMap; // Tree obstacles
    public Tilemap stoneTileMap; // Stone obstacles
    public Pathfinder pathfinder; // Reference to the pathfinding system
    public LineRenderer pathLine; // Line preview renderer

    private Queue<Vector3> pathQueue = new Queue<Vector3>(); // Queue of target world positions
    private List<Vector3Int> currentPath = new List<Vector3Int>(); // List of tile positions in path

    private Vector3Int prevHoveredTile; // Previously hovered tile
    private Color originalColor = Color.white; // Default color for unhighlighted tile

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
            {
                ResetTileHighlight(walkableTilemap, prevHoveredTile);
            }

            if (walkableTilemap.HasTile(hoveredTile))
            {
                HighlightTile(walkableTilemap, hoveredTile);
            }

            prevHoveredTile = hoveredTile;
        }
    }

    // Highlights a tile by tinting it
    private void HighlightTile(Tilemap tileMap, Vector3Int tilePos)
    {
        tileMap.SetTileFlags(tilePos, TileFlags.None);
        tileMap.SetColor(tilePos, new Color(1f, 1f, 1f, 0.7f)); // Slightly faded
    }

    // Resets the tile color to original
    private void ResetTileHighlight(Tilemap tileMap, Vector3Int tilePos)
    {
        tileMap.SetTileFlags(tilePos, TileFlags.None);
        tileMap.SetColor(tilePos, originalColor);
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
        if (pathQueue.Count == 0) return;

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
