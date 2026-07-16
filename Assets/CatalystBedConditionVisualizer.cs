using UnityEngine;

/// <summary>
/// Visual state model for the methanol reactor catalyst bed.
///
/// This intentionally avoids the old slider UI. A script, animation, timeline,
/// or inspector value can drive condition01:
/// 0.0 = cold/inactive, 0.5 = healthy operating condition, 0.75 = hot spot risk,
/// 1.0 = severe overheating / sintering risk.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CatalystBedConditionVisualizer : MonoBehaviour
{
    [Header("References")]
    public Renderer catalystRenderer;

    [Header("Condition")]
    [Range(0f, 1f)] public float condition01 = 0.5f;
    public bool animateDemoCondition = false;
    public float demoSpeed = 0.18f;

    [Header("Colours")]
    public Color coldInactive = new Color(0.15f, 0.45f, 1.00f, 1f);
    public Color healthy = new Color(0.18f, 0.95f, 0.35f, 1f);
    public Color hotspotRisk = new Color(1.00f, 0.62f, 0.05f, 1f);
    public Color sinteringRisk = new Color(1.00f, 0.08f, 0.04f, 1f);

    private Material runtimeMaterial;

    private void OnEnable()
    {
        AutoFindRenderer();
        EnsureRuntimeMaterial();
        ApplyConditionColor();
    }

    private void OnValidate()
    {
        AutoFindRenderer();
        EnsureRuntimeMaterial();
        ApplyConditionColor();
    }

    private void Update()
    {
        if (animateDemoCondition && Application.isPlaying)
        {
            condition01 = Mathf.PingPong(Time.time * demoSpeed, 1f);
        }

        ApplyConditionColor();
    }

    [ContextMenu("Auto Find Renderer")]
    public void AutoFindRenderer()
    {
        if (catalystRenderer != null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            catalystRenderer = renderers[0];
        }
    }

    [ContextMenu("Apply Condition Color")]
    public void ApplyConditionColor()
    {
        if (catalystRenderer == null)
        {
            return;
        }

        EnsureRuntimeMaterial();

        Color colour;
        if (condition01 < 0.5f)
        {
            colour = Color.Lerp(coldInactive, healthy, condition01 / 0.5f);
        }
        else if (condition01 < 0.78f)
        {
            colour = Color.Lerp(healthy, hotspotRisk, (condition01 - 0.5f) / 0.28f);
        }
        else
        {
            colour = Color.Lerp(hotspotRisk, sinteringRisk, (condition01 - 0.78f) / 0.22f);
        }

        SetMaterialColour(runtimeMaterial, colour);
    }

    private void EnsureRuntimeMaterial()
    {
        if (catalystRenderer == null)
        {
            return;
        }

        if (runtimeMaterial != null)
        {
            return;
        }

        runtimeMaterial = catalystRenderer.sharedMaterial != null
            ? new Material(catalystRenderer.sharedMaterial)
            : new Material(Shader.Find("Standard"));

        runtimeMaterial.name = "CatalystBed_ConditionMaterial";
        catalystRenderer.sharedMaterial = runtimeMaterial;
    }

    private static void SetMaterialColour(Material material, Color colour)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", colour);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", colour);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour * 0.55f);
        }
    }
}
