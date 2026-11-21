using System.Collections;
using UnityEngine;

/// <summary>
/// A hostile sword. It flies in a straight line (horizontal or diagonal); touching
/// the player costs a life. It reports back to the LevelManager exactly once,
/// either when it leaves the screen (dodged) or when it hits.
/// </summary>
public class Obstacle : MonoBehaviour
{
    Rigidbody2D body;
    Vector2 velocity;
    bool resolved;
    bool shattering;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.freezeRotation = true;
        foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.sortingOrder = Mathf.Max(r.sortingOrder, 5);
    }

    /// <summary>Send the sword flying. The sword art points left, so it is rotated to face its direction.</summary>
    public void Launch(Vector2 direction, float speed)
    {
        direction.Normalize();
        velocity = direction * speed;
        body.linearVelocity = velocity;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 180f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        body.rotation = angle;
    }

    void Update()
    {
        if (resolved) return;
        Rect view = LevelManager.ViewRect;
        Vector2 p = transform.position;
        bool gone = p.x < view.xMin - 2.5f
                    || (velocity.y > 0f && p.y > view.yMax + 2.5f)
                    || (velocity.y < 0f && p.y < view.yMin - 2.5f);
        if (gone)
        {
            Resolve(true);
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (resolved || !other.CompareTag("Player")) return;
        var gm = LevelManager.Instance;
        if (gm != null && gm.PlayerHit())
            Shatter(false);
        // If the player was invulnerable, the sword simply passes through.
    }

    void Resolve(bool dodged)
    {
        if (resolved) return;
        resolved = true;
        if (LevelManager.Instance != null) LevelManager.Instance.OnSwordResolved(dodged);
    }

    /// <summary>Break the sword apart (hit the player, or knocked out by a thrown sword).</summary>
    public void Shatter(bool countAsDodged)
    {
        if (shattering) return;
        shattering = true;
        Resolve(countAsDodged);
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        body.linearVelocity = velocity * 0.15f;
        StartCoroutine(ShatterRoutine());
    }

    IEnumerator ShatterRoutine()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        Vector3 scale = transform.localScale;
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            float k = t / 0.2f;
            transform.localScale = scale * (1f + k * 0.6f);
            foreach (var r in renderers) r.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
