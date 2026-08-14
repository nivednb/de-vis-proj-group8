using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Low-cost reactor cutaway: transparent vessel wall plus bounded feed,
/// conversion and product tracers. It deliberately stays below 260 live
/// particles and exists only in Play mode.
/// </summary>
public sealed class LightweightReactorVisual : MonoBehaviour
{
    const string RootName = "Lightweight Reactor Cutaway";

    public static void Configure(GameObject owner)
    {
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Renderer shell = null, top = null, bottom = null, catalyst = null;
        foreach (GameObject obj in objects)
        {
            if (!obj.scene.IsValid()) continue;
            if (obj.name.Equals("Reactor_Shell", StringComparison.OrdinalIgnoreCase)) shell = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Cap_Top", StringComparison.OrdinalIgnoreCase)) top = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Cap_Bottom", StringComparison.OrdinalIgnoreCase)) bottom = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Catalyst_Bed", StringComparison.OrdinalIgnoreCase)) catalyst = obj.GetComponent<Renderer>();
        }
        if (shell == null || catalyst == null) return;
        MakeTransparent(shell, .16f);
        MakeTransparent(top, .18f);
        MakeTransparent(bottom, .18f);

        GameObject existing = GameObject.Find(RootName);
        if (existing != null) UnityEngine.Object.Destroy(existing);
        GameObject root = new(RootName);
        // Bounds below are already measured in world space. Keeping this root
        // outside the imported reactor hierarchy prevents its 3x model scale
        // from multiplying the particle volume and displacing it below the bed.
        root.transform.SetParent(null, false);
        root.transform.position = catalyst.bounds.center;

        Bounds bed = catalyst.bounds;
        float radius = Mathf.Min(bed.extents.x, bed.extents.z) * .92f;
        float height = bed.size.y * 1.08f;
        // This model's feed nozzle enters the lower/side region and its product
        // nozzle is at the top, so it is represented as an upflow packed bed.
        CreateStream(root.transform, "H2 from side inlet", new Color(.10f, 1f, .22f, .95f),
            new Vector3(-radius*.34f, -height*.49f, 0), radius*.34f, height*.30f, 22f, .064f, 1.05f);
        CreateStream(root.transform, "CO2 from side inlet", new Color(.20f, .86f, 1f, .95f),
            new Vector3(radius*.34f, -height*.49f, 0), radius*.34f, height*.30f, 19f, .066f, 1.0f);
        CreateStream(root.transform, "Recycle from side inlet", new Color(.72f, .28f, 1f, .88f),
            new Vector3(0, -height*.46f, radius*.22f), radius*.30f, height*.28f, 10f, .061f, .96f);
        CreateStream(root.transform, "Catalyst conversion", new Color(1f, .30f, .04f, .92f),
            new Vector3(0, -height*.03f, 0), radius*1.06f, height*.94f, 48f, .080f, .72f);
        CreateStream(root.transform, "Methanol vapour to top outlet", new Color(.70f, .20f, 1f, .95f),
            new Vector3(-radius*.20f, height*.33f, 0), radius*.44f, height*.38f, 18f, .073f, .90f);
        CreateStream(root.transform, "Water vapour to top outlet", new Color(.15f, .70f, 1f, .92f),
            new Vector3(radius*.20f, height*.33f, 0), radius*.44f, height*.38f, 14f, .069f, .86f);
        CreateStream(root.transform, "Unreacted gas to top outlet", new Color(.70f, .88f, 1f, .62f),
            new Vector3(0, height*.36f, -radius*.18f), radius*.36f, height*.34f, 8f, .057f, .94f);
    }

    static void MakeTransparent(Renderer renderer, float alpha)
    {
        if (renderer == null) return;
        Material source = renderer.sharedMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = source != null ? new Material(source) : new Material(shader);
        material.name = "ReactorCutaway_Transparent";
        Color color = new(.70f, .88f, 1f, alpha);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static void CreateStream(Transform parent, string name, Color color, Vector3 offset,
        float radius, float height, float rate, float size, float velocity)
    {
        GameObject child = new(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = offset;
        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = Mathf.Max(.5f, height / Mathf.Abs(velocity));
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size*.72f, size*1.35f);
        main.startColor = color;
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius*2f, Mathf.Max(.05f, height*.08f), radius*2f);
        var velocityModule = ps.velocityOverLifetime;
        velocityModule.enabled = true;
        velocityModule.space = ParticleSystemSimulationSpace.World;
        // All three axes must use the same curve mode or Unity emits a warning
        // every frame. Noise supplies the small lateral dispersion instead.
        velocityModule.x = 0f;
        velocityModule.y = velocity;
        velocityModule.z = 0f;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = .10f;
        noise.frequency = .45f;
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader != null)
        {
            Material material = new(particleShader) { name = $"ReactorTracer_{name}" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            renderer.sharedMaterial = material;
        }
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
    }
}
