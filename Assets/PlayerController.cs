using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    public float speed = 0.5f;
    public Tilemap treeTileMap;
    public Tilemap stoneTileMap;
    public TileBase emptyTile;
    private Vector3 target;
    private Vector3Int lastHoveredTile;
    private Color originalColor = Color.white;
    private bool isPerformingAction = false;

    void Start()
    {
        target = transform.position; // Initialize target position to the player's start position
    }

    void Update()
    {
        if (isPerformingAction) return; // Prevent input while performing an action

        Vector3 mouseWorldPosition = GetMouseWorldPosition();
        Vector3Int hoveredTilePosition = treeTileMap.WorldToCell(mouseWorldPosition);
        Vector3Int hoveredStoneTilePosition = stoneTileMap.WorldToCell(mouseWorldPosition - stoneTileMap.transform.position);

        HandleTileHighlighting(hoveredTilePosition, hoveredStoneTilePosition);
        HandleInput(mouseWorldPosition, hoveredTilePosition, hoveredStoneTilePosition);
        MovePlayer();
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
            target = mouseWorldPos;
        }
        else if (Input.GetMouseButtonDown(1)) // Right click to interact
        {
            if (treeTileMap.HasTile(treePos)) StartCoroutine(MoveAndInteract(treePos, treeTileMap));
            else if (stoneTileMap.HasTile(stonePos)) StartCoroutine(MoveAndInteract(stonePos, stoneTileMap));
        }
    }

    // Moves the player towards the target position.
    private void MovePlayer()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }


    // Moves the player to the target tile and performs an interaction.
    private IEnumerator MoveAndInteract(Vector3Int tilePos, Tilemap tileMap)
    {
        isPerformingAction = true;
        Vector3Int closestTile = FindClosestWalkableTile(tilePos, tileMap);
        Vector3 worldPos = tileMap.GetCellCenterWorld(closestTile);

        yield return MoveToTarget(worldPos);
        yield return new WaitForSeconds(0.5f); // Simulate interaction delay
        
        tileMap.SetTile(tilePos, emptyTile); // Remove the interacted tile
        ResetTileHighlight(tileMap);
        target = transform.position;
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