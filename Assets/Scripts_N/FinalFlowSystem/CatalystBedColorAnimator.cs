using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Lightweight visible catalyst-state animation; no editor particle simulation.</summary>
[DisallowMultipleComponent]
public sealed class CatalystBedColorAnimator : MonoBehaviour
{
    [SerializeField] Color activeGreen = new Color(.12f, .88f, .35f, .22f);
    [SerializeField] Color convertingOrange = new Color(1f, .48f, .04f, .28f);
    [SerializeField] Color hotRed = new Color(1f, .08f, .02f, .26f);
    [SerializeField, Range(.05f, 2f)] float cycleSpeed = .32f;

    Renderer target;
    Material materialInstance;

    void Awake()
    {
        target = GetComponent<Renderer>();
        if (target == null) target = GetComponentInChildren<Renderer>(true);
        if (target == null) return;
        target.enabled = true;
        materialInstance = target.material;
        ConfigureTransparent(materialInstance);
    }

    void Update()
    {
        if (materialInstance == null) return;
        float phase = Mathf.Repeat(Time.time * cycleSpeed, 1f);
        Color color = phase < .55f
            ? Color.Lerp(activeGreen, convertingOrange, Mathf.SmoothStep(0f, 1f, phase / .55f))
            : Color.Lerp(convertingOrange, hotRed, Mathf.SmoothStep(0f, 1f, (phase - .55f) / .45f));
        SetColor(materialInstance, color);
        if (materialInstance.HasProperty("_EmissionColor"))
        {
            materialInstance.EnableKeyword("_EMISSION");
            materialInstance.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * .12f);
        }
    }

    static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    static void ConfigureTransparent(Material material)
    {
        if (material == null) return;
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
