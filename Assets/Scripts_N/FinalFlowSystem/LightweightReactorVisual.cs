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
        // Half-height of the catalyst bed, measured from the root at its centre. Every tracer
        // is defined by where it starts and ends *within* this span, and the lifetime is then
        // derived from that distance. The previous version derived lifetime from the full bed
        // height regardless of where a stream started, so streams that began part-way up flew
        // out through the top of the vessel.
        float half = bed.extents.y;
        Color h2 = PlantStreamLegend.WaterHydrogen;
        Color co2 = PlantStreamLegend.AmineCapturedCo2;
        Color syngas = PlantStreamLegend.CompressedSyngas;
        Color crude = PlantStreamLegend.CrudeMethanol;
        Color hot = PlantStreamLegend.HotReactorEffluent;

        // This model's feed nozzle enters the lower/side region and its product
        // nozzle is at the top, so it is represented as an upflow packed bed.
        CreateStream(root.transform, "H2 from side inlet", Fade(h2, .95f),
            new Vector2(-radius*.34f, 0f), radius*.34f, -half*.90f, -half*.20f, 22f, .064f, 1.05f);
        CreateStream(root.transform, "CO2 from side inlet", Fade(co2, .95f),
            new Vector2(radius*.34f, 0f), radius*.34f, -half*.90f, -half*.20f, 19f, .066f, 1.0f);
        CreateStream(root.transform, "Recycle from side inlet", Fade(syngas, .88f),
            new Vector2(0f, radius*.22f), radius*.30f, -half*.86f, -half*.24f, 10f, .061f, .96f);
        // Leaves room for the noise module's lateral wander (12% of the radius) so the
        // conversion tracers stay inside the catalyst bed, not just inside the shell.
        CreateStream(root.transform, "Catalyst conversion", Fade(hot, .92f),
            new Vector2(0f, 0f), radius*.96f, -half*.62f, half*.62f, 48f, .080f, .72f);
        CreateStream(root.transform, "Methanol vapour to top outlet", Fade(crude, .95f),
            new Vector2(-radius*.20f, 0f), radius*.44f, half*.16f, half*.90f, 18f, .073f, .90f);
        CreateStream(root.transform, "Water vapour to top outlet", Fade(h2, .92f),
            new Vector2(radius*.20f, 0f), radius*.44f, half*.16f, half*.90f, 14f, .069f, .86f);
        CreateStream(root.transform, "Unreacted gas to top outlet", Fade(syngas, .62f),
            new Vector2(0f, -radius*.18f), radius*.36f, half*.20f, half*.88f, 8f, .057f, .94f);
    }

    static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

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

    /// <summary>
    /// One tracer stream rising from <paramref name="startY"/> to <paramref name="endY"/>, both
    /// measured from the catalyst-bed centre. Lifetime is derived from that actual travel
    /// distance, which is what keeps every particle inside the vessel.
    /// </summary>
    static void CreateStream(Transform parent, string name, Color color, Vector2 lateralOffset,
        float radius, float startY, float endY, float rate, float size, float velocity)
    {
        float travel = Mathf.Abs(endY - startY);
        float emitterThickness = Mathf.Min(travel * .12f, Mathf.Max(.05f, travel * .12f));
        // The emitter has thickness, so a particle born at its top edge must still finish
        // inside the span: shorten the travel by half the emitter to stay bounded.
        float usableTravel = Mathf.Max(.05f, travel - emitterThickness * .5f);

        GameObject child = new(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = new Vector3(lateralOffset.x, startY, lateralOffset.y);
        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = Mathf.Max(.5f, usableTravel / Mathf.Abs(velocity));
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size*.72f, size*1.35f);
        main.startColor = color;
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius*2f, emitterThickness, radius*2f);
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
        // Proportional to the stream's own radius so the lateral wander never pushes tracers
        // through the vessel wall on this model's scale.
        noise.strength = Mathf.Max(.02f, radius * .12f);
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
