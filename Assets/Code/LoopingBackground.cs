using UnityEngine;

public class LoopingBackground : MonoBehaviour
{
    public float backgroundSpeed;
    //public Renderer backgroundRenderer;
    private Material backgroundMaterial;

    void Start()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            backgroundMaterial = meshRenderer.sharedMaterial;
        }
    }

    void Update()
    {
        //backgroundRenderer.material.mainTextureOffset += new Vector2(backgroundSpeed * Time.deltaTime, 0f);

        if (backgroundMaterial != null)
        {
            Vector2 offset = backgroundMaterial.mainTextureOffset;
            offset.x += backgroundSpeed * Time.deltaTime;
            backgroundMaterial.mainTextureOffset = offset;
        }
    }
}
