using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Adds the reactor-specific visual story: transparent shell, visible catalyst
/// bed condition, incoming syngas particles, product/effluent particles, and a
/// subtle heat shimmer cue. Generated at runtime so the scene does not need
/// heavy particle prefabs.
/// </summary>
[DisallowMultipleComponent]
public class ReactorInternalFlowRuntime : MonoBehaviour
{
    [Header("Catalyst")]
    public bool animateCatalystCondition = true;
    [Range(0f, 1f)] public float catalystCondition01 = 0.45f;
    public float catalystAnimationSpeed = 0.12f;

    [Header("Particle Density")]
    [Range(5f, 180f)] public float syngasRate = 75f;
    [Range(5f, 180f)] public float productRate = 60f;
    [Range(0f, 80f)] public float heatShimmerRate = 25f;

    private GameObject catalystBed;
    private Material catalystMaterial;

    public void ConfigureFromReactorBounds()
    {
        Bounds bounds = CalculateBounds();
        CreateCatalystBed(bounds);
        CreateParticleStream("Internal Syngas Feed", bounds.center + new Vector3(0f, bounds.extents.y * 0.35f, 0f), new Color(1f, 0.82f, 0.05f, 0.85f), syngasRate, Vector3.down);
        CreateParticleStream("Internal Methanol Product Formation", bounds.center, new Color(0.72f, 0.18f, 1f, 0.85f), productRate, Vector3.down);
        CreateParticleStream("Internal Heat Release Shimmer", bounds.center + new Vector3(0f, bounds.extents.y * 0.05f, 0f), new Color(1f, 0.30f, 0.02f, 0.45f), heatShimmerRate, Vector3.up);
        ApplyCatalystColor();
    }

    private void Update()
    {
        if (animateCatalystCondition && Application.isPlaying)
        {
            catalystCondition01 = 0.35f + Mathf.PingPong(Time.time * catalystAnimationSpeed, 0.55f);
            ApplyCatalystColor();
        }
    }

    private Bounds CalculateBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one * 2f);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(transform.position, new Vector3(2f, 5f, 2f));
        }

        return bounds;
    }

    private void CreateCatalystBed(Bounds bounds)
    {
        Transform existing = transform.Find("Generated Transparent Catalyst Bed");
        catalystBed = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        catalystBed.name = "Generated Transparent Catalyst Bed";
        catalystBed.transform.SetParent(transform, true);
        catalystBed.transform.position = bounds.center + new Vector3(0f, -bounds.extents.y * 0.18f, 0f);
        catalystBed.transform.localScale = new Vector3(
            Mathf.Max(0.35f, bounds.extents.x * 0.72f),
            Mathf.Max(0.20f, bounds.extents.y * 0.22f),
            Mathf.Max(0.35f, bounds.extents.z * 0.72f));

        Renderer renderer = catalystBed.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        catalystMaterial = new Material(shader) { name = "Generated_Catalyst_Condition_Material" };
        renderer.sharedMaterial = catalystMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void ApplyCatalystColor()
    {
        if (catalystMaterial == null)
        {
            return;
        }

        Color healthy = new Color(0.15f, 0.95f, 0.32f, 0.78f);
        Color warning = new Color(1.00f, 0.62f, 0.05f, 0.82f);
        Color critical = new Color(1.00f, 0.08f, 0.04f, 0.90f);
        Color color = catalystCondition01 < 0.72f
            ? Color.Lerp(healthy, warning, Mathf.InverseLerp(0.35f, 0.72f, catalystCondition01))
            : Color.Lerp(warning, critical, Mathf.InverseLerp(0.72f, 1f, catalystCondition01));

        if (catalystMaterial.HasProperty("_BaseColor")) catalystMaterial.SetColor("_BaseColor", color);
        if (catalystMaterial.HasProperty("_Color")) catalystMaterial.SetColor("_Color", color);
        if (catalystMaterial.HasProperty("_EmissionColor"))
        {
            catalystMaterial.EnableKeyword("_EMISSION");
            catalystMaterial.SetColor("_EmissionColor", color * 0.45f);
        }
        MakeTransparent(catalystMaterial, color);
    }

    private void CreateParticleStream(string streamName, Vector3 position, Color color, float rate, Vector3 direction)
    {
        Transform existing = transform.Find(streamName);
        GameObject stream = existing != null ? existing.gameObject : new GameObject(streamName);
        stream.transform.SetParent(transform, true);
        stream.transform.position = position;

        ParticleSystem ps = stream.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            ps = stream.AddComponent<ParticleSystem>();
        }

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = 1.6f;
        main.startSpeed = 0.65f;
        main.startSize = 0.055f;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.7f, 0.15f, 0.7f);

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = direction.x * 0.5f;
        velocity.y = direction.y * 0.5f;
        velocity.z = direction.z * 0.5f;

        ParticleSystemRenderer renderer = stream.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateParticleMaterial(color);
    }

    private static Material CreateParticleMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Sprites/Default");
        Material material = new Material(shader) { name = "Generated_Reactor_Particle_Material" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private static void MakeTransparent(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
