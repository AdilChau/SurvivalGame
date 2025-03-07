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

    // Start is called once at the start
    void Start()
    {
        target = transform.position;
    }

    // Update is called once per frame
    void Update()
{
    if(Input.GetMouseButtonDown(0)) 
    {
        Vector3 movementTarget = Camera.main.ScreenToWorldPoint(Input.mousePosition); // For movement
        Vector3 treeCheckPoint = movementTarget; // Copy movement target

        treeCheckPoint.z = 0;  // Set Z=0 only for tree detection
        Vector3Int tilePosition = treeTileMap.WorldToCell(treeCheckPoint);

        Debug.Log("Clicked on tile position: " + tilePosition); 

        if (treeTileMap.HasTile(tilePosition)) 
        {
            Debug.Log("Tree found at: " + tilePosition);
            StartCoroutine(MoveAndChopTree(tilePosition));
            return;
        }
        else
        {
            Debug.Log("No tree found at: " + tilePosition);
        }

        // Use the original movementTarget (with correct Z value) for movement
        target = movementTarget;
        target.z = transform.position.z;
    }

    transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
}


    IEnumerator MoveAndChopTree(Vector3Int tilePosition)
    {
        Vector3 treeWorldPos = treeTileMap.GetCellCenterWorld(tilePosition);

        // Move player to the tree before chopping
        while (Vector3.Distance(transform.position, treeWorldPos) > 0.5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, treeWorldPos, speed * Time.deltaTime);
            yield return null;
        }

        // Remove the tree (or replace it with an empty tile)
        treeTileMap.SetTile(tilePosition, emptyTile);
    }
}
