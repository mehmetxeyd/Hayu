using System.Collections;
using UnityEngine;

/// <summary>The whole heart waiting behind the barrier. Reach it to win.</summary>
public class GoalHeart : MonoBehaviour
{
    SpriteRenderer sr;
    Vector3 target;
    Vector3 baseScale;
    bool celebrating;
    float time;

    public static GoalHeart Create(Sprite sprite, Vector3 from, Vector3 to)
    {
        var go = new GameObject("Goal Heart");
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 0.6f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 4;
        var circle = go.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 1.3f;
        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;

        var heart = go.AddComponent<GoalHeart>();
        heart.sr = sr;
        heart.target = to;
        heart.baseScale = go.transform.localScale;
        return heart;
    }

    void Update()
    {
        time += Time.deltaTime;
        Vector3 p = Vector3.Lerp(transform.position, target, Time.deltaTime * 1.5f);
        p.y = target.y + Mathf.Sin(time * 2f) * 0.2f;
        transform.position = p;

        float beat = celebrating ? 0.15f : 0.05f;
        float speed = celebrating ? 10f : 4f;
        transform.localScale = baseScale * (1f + Mathf.Abs(Mathf.Sin(time * speed)) * beat);
        float glow = 0.85f + Mathf.Sin(time * 3f) * 0.15f;
        sr.color = new Color(1f, glow, glow);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || GameManager.Instance == null) return;
        GameManager.Instance.OnHeartReached();
    }

    public void Celebrate()
    {
        celebrating = true;
        StartCoroutine(Grow());
    }

    IEnumerator Grow()
    {
        Vector3 start = baseScale;
        Vector3 end = baseScale * 1.4f;
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            baseScale = Vector3.Lerp(start, end, t);
            yield return null;
        }
    }
}
