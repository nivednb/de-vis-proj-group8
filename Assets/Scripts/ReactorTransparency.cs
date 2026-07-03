using UnityEngine;

public class ReactorTransparency : MonoBehaviour
{
    public Material reactorMaterial;

    [Range(0f, 1f)]
    public float transparentAlpha = 0.25f;

    private Color originalColor;

    void Start()
    {
        if (reactorMaterial == null) return;
        originalColor = reactorMaterial.color;
    }

    public void SetTransparent()
    {
        if (reactorMaterial == null) return;

        Color c = reactorMaterial.color;
        c.a = transparentAlpha;
        reactorMaterial.color = c;
    }

    public void SetOpaque()
    {
        if (reactorMaterial == null) return;

        Color c = originalColor;
        c.a = 1f;
        reactorMaterial.color = c;
    }
}