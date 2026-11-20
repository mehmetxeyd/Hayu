using System.Collections;
using UnityEngine;

/// <summary>A golden sword on the barrier level. Touch it to add one sword to throw.</summary>
public class SwordPickup : MonoBehaviour
{
    float speed;
    float baseY;
    float phase;
    bool taken;
    SpriteRenderer sr;

    public static SwordPickup Create(Vector3 position, float speed)
    {
        var go = new GameObject("Golden Sword");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.32f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelManager.LoadSprite("sword_pickup");
        sr.sortingOrder = 6;
        var circle = go.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 1.9f; // ~0.6 world units at this scale
        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var pickup = go.AddComponent<SwordPickup>();
        pickup.speed = speed;
        pickup.baseY = position.y;
        pickup.phase = Random.value * 10f;
        pickup.sr = sr;
        return pickup;
    }

    void Update()
    {
        if (taken) return;
        phase += Time.deltaTime;
        Vector3 p = transform.position;
        p.x -= speed * Time.deltaTime;
        p.y = baseY + Mathf.Sin(phase * 3f) * 0.25f;
        transform.position = p;
        transform.rotation = Quaternion.Euler(0f, 0f, -35f + Mathf.Sin(phase * 4f) * 12f);
        float glow = 0.85f + Mathf.Sin(phase * 8f) * 0.15f;
        sr.color = new Color(1f, glow, glow * 0.8f + 0.2f);

        if (p.x < LevelManager.ViewRect.xMin - 1.5f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (taken || !other.CompareTag("Player")) return;
        taken = true;
        if (LevelManager.Instance != null) LevelManager.Instance.AddAmmo();
        StartCoroutine(Collect());
    }

    IEnumerator Collect()
    {
        Vector3 scale = transform.localScale;
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            float k = t / 0.2f;
            transform.localScale = scale * (1f + k);
            transform.position += Vector3.up * Time.deltaTime * 3f;
            sr.color = new Color(1f, 1f, 0.7f, 1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
