using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    public float speed = 0.5f;
    private Vector3 target;
    public Tilemap treeTileMap;
    public TileBase emptyTile; 
    private Vector3Int lastHoveredTile;
    private Color originalColor = Color.white;
    
    // Add this to track when a coroutine is controlling movement
    private bool isPerformingAction = false;

    void Start()
    {
        target = transform.position;
    }

    void Update()
    {
        // Mouse position handling
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPosition.z = 0;
        Vector3Int hoveredTilePosition = treeTileMap.WorldToCell(mouseWorldPosition);

        // Tree highlighting
        if (treeTileMap.HasTile(hoveredTilePosition)) 
        {
            HighlightTree(hoveredTilePosition);
            lastHoveredTile = hoveredTilePosition;
        }
        else
        {
            ResetTreeHighlight();
        }

        // Only process clicks if not already performing an action
        if (!isPerformingAction)
        {
            // Left click for movement
            if (Input.GetMouseButtonDown(0)) 
            {
                target = mouseWorldPosition;
            }
            // Right click for tree interaction
            else if (Input.GetMouseButtonDown(1)) 
            {
                if (treeTileMap.HasTile(hoveredTilePosition)) 
                { 
                    StartCoroutine(MoveAndChopTree(hoveredTilePosition)); 
                }
            }
            
            // Only move toward target if not performing an action
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        }
    }

    IEnumerator MoveAndChopTree(Vector3Int treeTilePosition)
    {
        // Set flag to prevent regular movement
        isPerformingAction = true;
        
        // Find closest valid tile
        Vector3Int closestValidTile = FindClosestWalkableTile(treeTilePosition);
        Vector3 validWorldPos = treeTileMap.GetCellCenterWorld(closestValidTile);

        // Move player to the tree before chopping
        while (Vector3.Distance(transform.position, validWorldPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, validWorldPos, speed * Time.deltaTime);
            yield return null;
        }

        yield return new WaitForSeconds(0.5f); // Delay for chopping effect
        
        // Remove the tree
        treeTileMap.SetTile(treeTilePosition, emptyTile);
        ResetTreeHighlight();
        
        // Update the target to be where the player currently is after chopping tree
        target = transform.position;

        // Release movement control
        isPerformingAction = false;
    }

    void HighlightTree(Vector3Int tilePosition)
    {
        // Add outline to highlight tree
        if (treeTileMap.HasTile(tilePosition))
        {
            treeTileMap.SetTileFlags(tilePosition, TileFlags.None); // Allows colour modification
            treeTileMap.SetColor(tilePosition, Color.yellow); // Change colour to yellow
        }
    }

    void ResetTreeHighlight()
    {
        if (treeTileMap.HasTile(lastHoveredTile))
        {
            treeTileMap.SetTileFlags(lastHoveredTile, TileFlags.None);
            treeTileMap.SetColor(lastHoveredTile, originalColor); // Reset to default
        }
    }

    Vector3Int FindClosestWalkableTile(Vector3Int treeTilePosition)
    {
        // Define 4 possible adjacent tiles
        Vector3Int[] directions = {
            new Vector3Int(1,0,0), // Right
            new Vector3Int(-1,0,0), // Left
            new Vector3Int(0,1,0), // Up
            new Vector3Int(0,-1,0) // Down
        };

        // Find the first walkable tile
        foreach (Vector3Int dir in directions)
        {
            Vector3Int adjacentTile = treeTilePosition + dir;
            if (!treeTileMap.HasTile(adjacentTile)) // Check if it's NOT a tree
            {
                return adjacentTile;
            }
        }

        return treeTilePosition; // Defauly to the tree tile if no valid space found
    }
}
