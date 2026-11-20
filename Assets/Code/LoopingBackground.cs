using UnityEngine;

/// <summary>Scrolls the background quad's texture so it loops forever.</summary>
public class LoopingBackground : MonoBehaviour
{
    public float backgroundSpeed;
    private Material backgroundMaterial;

    void Awake()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        // Use a per-instance material so scrolling doesn't modify the material asset on disk.
        if (meshRenderer != null) backgroundMaterial = meshRenderer.material;
    }

    public void SetTexture(Texture2D texture)
    {
        if (backgroundMaterial == null || texture == null) return;
        texture.wrapMode = TextureWrapMode.Repeat;
        backgroundMaterial.mainTexture = texture;
        backgroundMaterial.mainTextureOffset = Vector2.zero;
    }

    void Update()
    {
        if (backgroundMaterial == null) return;
        Vector2 offset = backgroundMaterial.mainTextureOffset;
        offset.x = Mathf.Repeat(offset.x + backgroundSpeed * Time.deltaTime, 1f);
        backgroundMaterial.mainTextureOffset = offset;
    }
}
