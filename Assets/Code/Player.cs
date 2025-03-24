using UnityEngine;

public class Player : MonoBehaviour 
{
    public float playerSpeed = 5f;
    private Rigidbody2D player;
    private Vector2 playerDirection;

    void Start() // Start is called before the first frame update
    {
        player = GetComponent<Rigidbody2D>();
    }

    void Update() // Update is called once per frame
    {
        float directionY = Input.GetAxisRaw("Vertical");
        playerDirection = new Vector2(0, directionY).normalized; // Normalize the vector to prevent faster diagonal movement
    }

    void FixedUpdate() // FixedUpdate is called at a fixed interval
    {
        player.linearVelocity = new Vector2(0, playerDirection.y * playerSpeed);
    }
}
