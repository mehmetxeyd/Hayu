using System.Collections;
using UnityEngine;

/// <summary>The wall on level 3. It cracks as thrown swords hit it, then shatters.</summary>
public class Barrier : MonoBehaviour
{
    Sprite[] sprites;
    SpriteRenderer sr;
    Vector3 restPosition;
    int maxHealth;
    float flash;
    float shake;

    public int Health { get; private set; }
    public bool IsBroken => Health <= 0;

    public static Barrier Create(Sprite[] sprites, int health, Vector3 position, float viewHeight)
    {
        var go = new GameObject("Barrier");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprites[0];
        sr.sortingOrder = 8;
        float spriteHeight = sprites[0] != null ? sprites[0].bounds.size.y : 10f;
        float s = (viewHeight + 0.4f) / spriteHeight;
        go.transform.localScale = new Vector3(s, s, 1f);
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;

        var barrier = go.AddComponent<Barrier>();
        barrier.sprites = sprites;
        barrier.sr = sr;
        barrier.maxHealth = barrier.Health = health;
        barrier.restPosition = position;
        go.transform.position = position + new Vector3(6f, 0f, 0f);
        barrier.StartCoroutine(barrier.SlideIn());
        return barrier;
    }

    IEnumerator SlideIn()
    {
        Vector3 from = transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(from, restPosition, k);
            yield return null;
        }
        transform.position = restPosition;
    }

    public void Hit()
    {
        if (IsBroken) return;
        Health--;
        flash = 1f;
        shake = 0.18f;

        float lost = 1f - Health / (float)maxHealth;
        int index = Mathf.Clamp(Mathf.FloorToInt(lost * sprites.Length), 0, sprites.Length - 1);
        if (sprites[index] != null) sr.sprite = sprites[index];

        if (IsBroken) StartCoroutine(Shatter());
    }

    void Update()
    {
        if (IsBroken) return;
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 5f);
        shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 0.8f);
        sr.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.7f), flash);
        if (shake > 0f) transform.position = restPosition + (Vector3)(Random.insideUnitCircle * shake);
    }

    IEnumerator Shatter()
    {
        GetComponent<Collider2D>().enabled = false;
        transform.position = restPosition;
        if (LevelManager.Instance != null) LevelManager.Instance.OnBarrierBroken(restPosition);

        Vector3 scale = transform.localScale;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            float k = t / 0.8f;
            transform.position = restPosition + new Vector3(Random.Range(-0.1f, 0.1f), -k * k * 6f, 0f);
            transform.localScale = new Vector3(scale.x * (1f + k * 0.5f), scale.y, 1f);
            sr.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
