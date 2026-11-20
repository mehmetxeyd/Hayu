using UnityEngine;

/// <summary>
/// Spawns the swords for the current level. The LevelManager starts and stops waves;
/// swords enter from the right edge of the screen, and on later levels some drop in
/// diagonally from above or below. On the barrier level, golden swords to collect
/// are mixed in as well.
/// </summary>
public class SpawnObstacles : MonoBehaviour
{
    [Tooltip("Sword prefab (needs a trigger collider). If empty, one is built in code.")]
    public GameObject obstacle;
    [Tooltip("Seconds between golden swords on the barrier level.")]
    public float pickupInterval = 1.25f;
    public float pickupSpeed = 5f;
    [Tooltip("Keep swords this far from the top/bottom edge.")]
    public float edgeMargin = 0.8f;

    LevelManager.LevelSettings level;
    bool running;
    int spawned;
    float nextSword;
    float nextPickup;
    float lastY;

    /// <summary>True once every sword of a counted wave has been spawned.</summary>
    public bool Finished => level != null && !level.barrierLevel && spawned >= level.swordCount;

    public void StartWave(LevelManager.LevelSettings settings)
    {
        level = settings;
        spawned = 0;
        running = true;
        nextSword = Time.time + 0.3f;
        nextPickup = Time.time + 0.6f;
    }

    public void Stop() => running = false;

    void Update()
    {
        if (!running || level == null) return;

        if (Time.time >= nextSword && (level.barrierLevel || spawned < level.swordCount))
        {
            SpawnSword();
            spawned++;
            nextSword = Time.time + level.spawnInterval * Random.Range(0.75f, 1.25f);
        }

        if (level.barrierLevel && Time.time >= nextPickup)
        {
            SpawnPickup();
            nextPickup = Time.time + pickupInterval * Random.Range(0.8f, 1.2f);
        }
    }

    void SpawnSword()
    {
        Rect view = LevelManager.ViewRect;
        Vector3 playerPos = LevelManager.Instance != null ? LevelManager.Instance.PlayerPosition : Vector3.zero;
        Vector3 pos;
        Vector2 dir;
        float speed = level.swordSpeed;

        if (Random.value < level.diagonalChance)
        {
            // Diagonal: drop in from the top or bottom, aimed near the player.
            bool fromTop = Random.value < 0.5f;
            pos = new Vector3(
                Random.Range(view.center.x, view.xMax + 0.5f),
                fromTop ? view.yMax + 1f : view.yMin - 1f,
                0f);
            Vector3 target = new Vector3(
                playerPos.x + Random.Range(-1f, 1f),
                Mathf.Clamp(playerPos.y + Random.Range(-2f, 2f), view.yMin + 1f, view.yMax - 1f),
                0f);
            dir = target - pos;
            speed *= 0.85f;
        }
        else
        {
            // Straight: from the right edge. Avoid stacking at the same height twice in a row.
            float y = RandomY(view);
            if (Mathf.Abs(y - lastY) < 0.8f) y = RandomY(view);
            lastY = y;
            pos = new Vector3(view.xMax + 1.5f, y, 0f);
            dir = Vector2.left;
        }

        var go = obstacle != null ? Instantiate(obstacle, pos, Quaternion.identity) : BuildSword(pos);
        var sword = go.GetComponent<Obstacle>();
        if (sword == null) sword = go.AddComponent<Obstacle>();
        sword.Launch(dir, speed);
    }

    void SpawnPickup()
    {
        Rect view = LevelManager.ViewRect;
        SwordPickup.Create(new Vector3(view.xMax - 2f, RandomY(view), 0f), pickupSpeed);
    }

    float RandomY(Rect view) => Random.Range(view.yMin + edgeMargin, view.yMax - edgeMargin);

    static GameObject BuildSword(Vector3 pos)
    {
        var go = new GameObject("Sword");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.5f;
        var art = new GameObject("Art");
        art.transform.SetParent(go.transform, false);
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sprite = LevelManager.LoadSprite("sword");
        sr.sortingOrder = 5;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(3.5f, 0.9f);
        box.isTrigger = true;
        go.AddComponent<Rigidbody2D>();
        return go;
    }
}
