using UnityEngine;

/// <summary>Sparse mesh-constrained particles layered over the continuous pipe shader.</summary>
[DisallowMultipleComponent]
public sealed class PipeFlowAccent : MonoBehaviour
{
    ParticleSystem particles;

    public void Configure(Renderer pipeRenderer, PlantFlowKind kind, Color color, float intensity)
    {
        if (pipeRenderer == null) return;
        Color speciesA = color;
        Color speciesB = color;
        if (kind == PlantFlowKind.MixedFeed || kind == PlantFlowKind.SyngasCold || kind == PlantFlowKind.SyngasHeated)
        {
            speciesA = new Color(0.20f, 1.00f, 0.28f, 1f);
            speciesB = new Color(0.72f, 0.94f, 1.00f, 1f);
        }
        else if (kind == PlantFlowKind.ReactorEffluent || kind == PlantFlowKind.CrudeMethanolVapourLiquid)
        {
            speciesA = new Color(0.68f, 0.24f, 1.00f, 1f);
            speciesB = new Color(0.15f, 0.70f, 1.00f, 1f);
        }
        else if (kind == PlantFlowKind.RecycleGas)
        {
            speciesA = new Color(0.20f, 1.00f, 0.28f, 1f);
            speciesB = new Color(0.68f, 0.24f, 1.00f, 1f);
        }
        Transform child = transform.Find("Mesh Flow Sparkles");
        GameObject go = child != null ? child.gameObject : new GameObject("Mesh Flow Sparkles");
        go.transform.SetParent(transform, false);
        particles = go.GetComponent<ParticleSystem>();
        if (particles == null) particles = go.AddComponent<ParticleSystem>();

        float diameter = Mathf.Min(pipeRenderer.bounds.size.x, Mathf.Min(pipeRenderer.bounds.size.y, pipeRenderer.bounds.size.z));
        float size = Mathf.Clamp(diameter * 0.13f, 0.028f, 0.085f);
        var main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.25f);
        main.startSpeed = 0f;
        main.startSize = size;
        main.startColor = new ParticleSystem.MinMaxGradient(speciesA, speciesB);
        main.maxParticles = 72;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Lerp(12f, 26f, Mathf.Clamp01(intensity));

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.MeshRenderer;
        shape.meshRenderer = pipeRenderer as MeshRenderer;
        shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;

        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(speciesA, 0f), new GradientColorKey(speciesB, 0.5f), new GradientColorKey(speciesA, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.25f), new GradientAlphaKey(0.65f, 0.72f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader) { name = "Runtime Pipe Sparkle" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        pr.material = material;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (Application.isPlaying) particles.Play();
    }
}
