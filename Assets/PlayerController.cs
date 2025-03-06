using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 0.5f;
    private Animator animator;
    private Vector2 movement;

    // Start is called once at the start
    void Start()
    {
        animator = GetComponent<Animator>(); // Get Animator component
    }

    // Update is called once per frame
    void Update()
    {
        // Get Input for Movement
        float moveX = Input.GetAxis("Horizontal");
        float moveY = Input.GetAxis("Vertical");

        // Store movement direction
        movement = new Vector2(moveX, moveY).normalized;

        // Update animation based on movement
        if (moveX == 0 && moveY == 0)
        {
            animator.Play("Idle"); // Force Idle when no keys are pressed
        }
        else
        {
        // Update the animation based on movement
        UpdateAnimation(moveX, moveY);
        }
    }

    void FixedUpdate()
    {
        // Move the Player
        transform.position += new Vector3((movement.x + movement.y), (movement.y - movement.x), 0) * speed * Time.deltaTime;
    }

    void UpdateAnimation(float moveX, float moveY) 
    {
        if (moveX == 0 && moveY == 0) 
        {
            animator.Play("Idle"); // Default animation when not moving
            return;
        }  

        if (moveY > 0) animator.Play("WalkUp");
        else if (moveY < 0) animator.Play("WalkDown");
        else if (moveX > 0) animator.Play("WalkRight");
        else if (moveX < 0) animator.Play("WalkLeft");
    }
}
