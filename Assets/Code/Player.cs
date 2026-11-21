using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    public float playerSpeed = 5f;
    [Tooltip("Seconds between throws on the barrier level.")]
    public float throwCooldown = 0.25f;

    Rigidbody2D body;
    Collider2D col;
    SpriteRenderer[] renderers;
    Vector2 input;
    Vector3 startLocalPosition;
    bool freeMovement;
    bool controllable = true;
    float invulnerableUntil;
    float nextThrow;

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in renderers) r.sortingOrder = Mathf.Max(r.sortingOrder, 10);
        startLocalPosition = transform.localPosition;
    }

    void Update()
    {
        float x = freeMovement ? Input.GetAxisRaw("Horizontal") : 0f;
        float y = Input.GetAxisRaw("Vertical");
        input = controllable ? Vector2.ClampMagnitude(new Vector2(x, y), 1f) : Vector2.zero;

        // Blink while invulnerable
        if (controllable)
        {
            bool visible = !IsInvulnerable || Mathf.Repeat(Time.time * 12f, 1f) > 0.4f;
            foreach (var r in renderers) r.enabled = visible;
        }

        var gm = LevelManager.Instance;
        bool throwPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0);
        if (controllable && throwPressed && gm != null && gm.CanShoot && Time.time >= nextThrow && gm.TryUseAmmo())
        {
            nextThrow = Time.time + throwCooldown;
            ThrownSword.Create(transform.position + new Vector3(0.9f, 0f, 0f));
        }
    }

    void FixedUpdate()
    {
        if (!body.simulated) return;
        body.linearVelocity = input * playerSpeed;

        // Keep the player on screen.
        Rect view = LevelManager.ViewRect;
        Vector2 ext = col != null ? (Vector2)col.bounds.extents : new Vector2(0.5f, 0.5f);
        Vector2 p = body.position;
        Vector2 clamped = new Vector2(
            Mathf.Clamp(p.x, view.xMin + ext.x, view.xMax - ext.x),
            Mathf.Clamp(p.y, view.yMin + ext.y, view.yMax - ext.y));
        if (clamped != p) body.position = clamped;
    }

    public void ResetForLevel()
    {
        StopAllCoroutines();
        transform.localPosition = startLocalPosition;
        transform.localScale = Vector3.one;
        body.position = transform.position;
        body.linearVelocity = Vector2.zero;
        freeMovement = false;
        controllable = true;
        invulnerableUntil = 0f;
        body.simulated = true;
        foreach (var r in renderers)
        {
            r.enabled = true;
            r.color = Color.white;
        }
    }

    public void SetControllable(bool value)
    {
        controllable = value;
        if (!value)
        {
            input = Vector2.zero;
            foreach (var r in renderers) r.enabled = true;
        }
    }

    public void SetFreeMovement(bool value) => freeMovement = value;

    public void StartInvulnerability(float seconds) => invulnerableUntil = Time.time + seconds;

    public void Die()
    {
        SetControllable(false);
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        StartCoroutine(DieRoutine());
    }

    IEnumerator DieRoutine()
    {
        Vector3 start = transform.position;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            float k = t / 1.2f;
            transform.position = start + new Vector3(0f, -k * k * 2.5f, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
            foreach (var r in renderers) r.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        foreach (var r in renderers) r.enabled = false;
        transform.localRotation = Quaternion.identity;
    }
}
