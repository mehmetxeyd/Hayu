using UnityEngine;

/// <summary>A sword the player throws to the right on the barrier level.</summary>
public class ThrownSword : MonoBehaviour
{
    public float speed = 16f;
    bool spent;

    public static ThrownSword Create(Vector3 position)
    {
        var go = new GameObject("Thrown Sword");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.4f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelManager.LoadSprite("sword_pickup");
        sr.sortingOrder = 7;
        go.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // golden sword art points down; turn it to point right
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.6f, 3.2f);
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        var thrown = go.AddComponent<ThrownSword>();
        body.linearVelocity = Vector2.right * thrown.speed;
        return thrown;
    }

    void Update()
    {
        if (transform.position.x > LevelManager.ViewRect.xMax + 2f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (spent) return;

        var barrier = other.GetComponentInParent<Barrier>();
        if (barrier != null && !barrier.IsBroken)
        {
            spent = true;
            barrier.Hit();
            Destroy(gameObject);
            return;
        }

        var enemy = other.GetComponentInParent<Obstacle>();
        if (enemy != null)
        {
            // Thrown swords knock hostile swords out of the air.
            spent = true;
            enemy.Shatter(true);
            Destroy(gameObject);
        }
    }
}
