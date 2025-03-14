using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    public float speed = 2f; // Speed at which the player moves
    public Tilemap walkableTilemap; // Reference to the walkable tilemap
    public Tilemap treeTileMap; // Reference to the tree tilemap (obstacles)
    public Tilemap stoneTileMap; // Reference to the stone tilemap (obstacles)
    public Pathfinder pathfinder; // Reference to the A* pathfinding system
    public TileBase emptyTile; // Tile used to replace interacted objects

    private Queue<Vector3> pathQueue = new Queue<Vector3>(); // Stores movement path
    private Vector3Int lastHoveredTile; // Keeps track of last highlighted tile
    private Color originalColor = Color.white; // Default tile color
    private bool isPerformingAction = false; // Prevents movement during interaction

    void Start()
    {
        pathfinder = FindObjectOfType<Pathfinder>(); // Find and assign the Pathfinder script
    }

    void Update()
    {
        if (isPerformingAction) return; // Prevent input while performing an action

        Vector3 mouseWorldPosition = GetMouseWorldPosition(); // Get current mouse position
        Vector3Int hoveredTilePosition = treeTileMap.WorldToCell(mouseWorldPosition);
        Vector3Int hoveredStoneTilePosition = stoneTileMap.WorldToCell(mouseWorldPosition - stoneTileMap.transform.position);

        // Handle highlighting of interactable tiles
        HandleTileHighlighting(hoveredTilePosition, hoveredStoneTilePosition);

        // Handle input for movement and interaction
        HandleInput(mouseWorldPosition, hoveredTilePosition, hoveredStoneTilePosition);

        // Move the player along the queued path
        MoveAlongPath();
    }

    // Retrieves the current mouse position in world coordinates.
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;
        return mousePosition;
    }

    // Handles tile highlighting for both trees and stones based on the current hover position.
    private void HandleTileHighlighting(Vector3Int treePos, Vector3Int stonePos)
    {
        if (treeTileMap.HasTile(treePos))
        {
            HighlightTile(treeTileMap, treePos);
            lastHoveredTile = treePos;
        }
        else if (stoneTileMap.HasTile(stonePos))
        {
            HighlightTile(stoneTileMap, stonePos);
            lastHoveredTile = stonePos;
        }
        else
        {
            ResetTileHighlight(treeTileMap);
            ResetTileHighlight(stoneTileMap);
        }
    }

    // Processes user input for movement and interaction with objects.
    private void HandleInput(Vector3 mouseWorldPos, Vector3Int treePos, Vector3Int stonePos)
    {
        if (Input.GetMouseButtonDown(0)) // Left click to move
        {
            SetPath(mouseWorldPos);
        }
        else if (Input.GetMouseButtonDown(1)) // Right click to interact
        {
            if (treeTileMap.HasTile(treePos)) StartCoroutine(MoveAndInteract(treePos, treeTileMap));
            else if (stoneTileMap.HasTile(stonePos)) StartCoroutine(MoveAndInteract(stonePos, stoneTileMap));
        }
    }

    // Sets the movement path using A* pathfinding.
    private void SetPath(Vector3 worldPosition)
    {
        Vector3Int destination = walkableTilemap.WorldToCell(worldPosition);
        Vector3Int startPos = walkableTilemap.WorldToCell(transform.position);
        List<Vector3Int> path = pathfinder.FindPath(startPos, destination);

        if (path != null)
        {
            pathQueue.Clear();
            foreach (Vector3Int tile in path)
            {
                pathQueue.Enqueue(walkableTilemap.GetCellCenterWorld(tile));
            }
        }
    }

    // Moves the player along the path if there are tiles to move to.
    private void MoveAlongPath()
    {
        if (pathQueue.Count == 0) return;

        Vector3 targetPosition = pathQueue.Peek();
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            pathQueue.Dequeue();
        }
    }

    // Moves the player to the target tile and performs an interaction.
    private IEnumerator MoveAndInteract(Vector3Int tilePos, Tilemap tileMap)
    {
        isPerformingAction = true;

        // Find the closest walkable tile to interact from
        Vector3Int closestTile = FindClosestWalkableTile(tilePos, tileMap);
        Vector3 worldPos = tileMap.GetCellCenterWorld(closestTile);

        yield return MoveToTarget(worldPos);
        yield return new WaitForSeconds(0.5f); // Simulate interaction delay
        
        tileMap.SetTile(tilePos, emptyTile); // Remove the interacted tile
        ResetTileHighlight(tileMap);
        isPerformingAction = false;
    }

    // Moves the player to a specific target position over time.
    private IEnumerator MoveToTarget(Vector3 destination)
    {
        while (Vector3.Distance(transform.position, destination) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
            yield return null;
        }
    }

    // Highlights a specified tile by adjusting its color.
    private void HighlightTile(Tilemap tileMap, Vector3Int tilePos)
    {
        tileMap.SetTileFlags(tilePos, TileFlags.None);
        tileMap.SetColor(tilePos, new Color(0.9f, 0.9f, 0.9f, 0.9f));
    }

    // Resets the color of the last hovered tile to its original state.
    private void ResetTileHighlight(Tilemap tileMap)
    {
        if (tileMap.HasTile(lastHoveredTile))
        {
            tileMap.SetTileFlags(lastHoveredTile, TileFlags.None);
            tileMap.SetColor(lastHoveredTile, originalColor);
        }
    }

    // Finds the closest walkable tile adjacent to the specified tile.
    private Vector3Int FindClosestWalkableTile(Vector3Int tilePos, Tilemap tileMap)
    {
        Vector3Int[] directions = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down };
        
        foreach (Vector3Int dir in directions)
        {
            Vector3Int adjacentTile = tilePos + dir;
            if (!tileMap.HasTile(adjacentTile)) return adjacentTile;
        }
        
        return tilePos;
    }
}
