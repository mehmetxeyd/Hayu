using UnityEngine;

/// <summary>
/// Layered background where each layer scrolls at its own speed: the far sky is
/// still and the nearest clouds move fastest, which gives a sense of depth.
/// Each layer is repeated side by side so it loops forever.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Tooltip("Scales every layer's speed (0 freezes the background).")]
    public float speedMultiplier = 1f;

    Transform[] layers;
    float[] speeds;
    float[] offsets;
    float layerWidth;

    /// <param name="sprites">Layers ordered back (far) to front (near).</param>
    /// <param name="speeds">World units per second for each layer.</param>
    public static ParallaxBackground Create(Sprite[] sprites, float[] speeds, Vector3 center, float viewHeight)
    {
        var go = new GameObject("Parallax Background");
        go.transform.position = new Vector3(center.x, center.y, 10f);
        var p = go.AddComponent<ParallaxBackground>();

        Vector2 size = sprites[0].bounds.size;
        float scale = viewHeight / size.y;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        p.layerWidth = size.x; // in the parent's (unscaled) space

        p.layers = new Transform[sprites.Length];
        p.speeds = new float[sprites.Length];
        p.offsets = new float[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            var layer = new GameObject("Layer " + i).transform;
            layer.SetParent(go.transform, false);
            // Four copies side by side cover even very wide screens while scrolling.
            for (int c = -1; c <= 2; c++)
            {
                var tile = new GameObject("Tile").AddComponent<SpriteRenderer>();
                tile.transform.SetParent(layer, false);
                tile.transform.localPosition = new Vector3(c * size.x, 0f, 0f);
                tile.sprite = sprites[i];
                tile.sortingOrder = -100 + i;
            }
            p.layers[i] = layer;
            p.speeds[i] = i < speeds.Length ? speeds[i] : speeds[speeds.Length - 1];
        }
        return p;
    }

    void Update()
    {
        float scale = transform.localScale.x;
        for (int i = 0; i < layers.Length; i++)
        {
            // speeds are in world units; convert to the parent's local units
            offsets[i] = Mathf.Repeat(offsets[i] + speeds[i] * speedMultiplier * Time.deltaTime / scale, layerWidth);
            layers[i].localPosition = new Vector3(-offsets[i], 0f, 0f);
        }
    }
}
