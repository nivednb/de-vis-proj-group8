using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adds a focused local flow visualization around the reactor section.
/// The particle colours follow the project reference:
/// H2 = green, CO2 = grey, methanol = purple, water = blue.
/// </summary>
[DisallowMultipleComponent]
public class ReactorDetailFlowRuntime : MonoBehaviour
{
    private const bool LegacyAutoCreateEnabled = false;

    [SerializeField] private bool autoCreateOnStart = true;
    [SerializeField] private float inletOrbitRadius = 1.35f;
    [SerializeField] private float productOrbitRadius = 1.85f;
    [SerializeField] private int h2ParticleCount = 22;
    [SerializeField] private int co2ParticleCount = 18;
    [SerializeField] private int methanolParticleCount = 20;
    [SerializeField] private int waterParticleCount = 12;
    [SerializeField] private float particleSize = 0.12f;
    [SerializeField] private Vector3 reactorCenterOffset = new Vector3(0f, 1.8f, 0f);

    private Transform reactorTarget;
    private Material h2Material;
    private Material co2Material;
    private Material methanolMaterial;
    private Material waterMaterial;
    private Material catalystMaterial;
    private Material glowMaterial;
    private Transform catalystBed;
    private Transform temperatureGlow;

    private readonly List<ParticlePoint> h2Particles = new List<ParticlePoint>();
    private readonly List<ParticlePoint> co2Particles = new List<ParticlePoint>();
    private readonly List<ParticlePoint> methanolParticles = new List<ParticlePoint>();
    private readonly List<ParticlePoint> waterParticles = new List<ParticlePoint>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (!LegacyAutoCreateEnabled) return;
        if (FindFirstObjectByType<ReactorDetailFlowRuntime>() != null)
        {
            return;
        }

        GameObject go = new GameObject("Generated Reactor Detail Flow");
        go.AddComponent<ReactorDetailFlowRuntime>();
    }

    private void Start()
    {
        if (autoCreateOnStart)
        {
            Build();
        }
    }

    private void Update()
    {
        if (reactorTarget == null)
        {
            return;
        }

        PlantProcessSimulator.ProcessSnapshot snapshot = PlantProcessSimulator.Instance != null
            ? PlantProcessSimulator.Instance.Current
            : default;

        float h2Intensity = Mathf.InverseLerp(0f, 215f, snapshot.h2InputKgH);
        float co2Intensity = Mathf.InverseLerp(0f, 1510f, snapshot.co2CapturedKgH);
        float productIntensity = Mathf.InverseLerp(0f, 1250f, snapshot.methanolProductionKgH);
        float yield = Mathf.Clamp01(snapshot.reactorYieldPercent / 100f);
        Vector3 center = reactorTarget.position + reactorCenterOffset;

        UpdateTemperatureGlow(center, snapshot.reactorTemperatureC, yield);
        UpdateCatalystBed(center, yield);

        UpdateParticleSet(h2Particles, center + Vector3.down * 0.75f, inletOrbitRadius, h2Intensity, 1.25f + yield, ParticleMotion.BottomToMiddle);
        UpdateParticleSet(co2Particles, center + Vector3.down * 0.55f, inletOrbitRadius * 0.82f, co2Intensity, 1.05f + yield, ParticleMotion.BottomToMiddle);
        UpdateParticleSet(methanolParticles, center + Vector3.up * 0.55f, productOrbitRadius, productIntensity, 0.9f + yield * 1.4f, ParticleMotion.MiddleToTop);
        UpdateParticleSet(waterParticles, center + Vector3.up * 0.25f, productOrbitRadius * 0.72f, productIntensity * 0.75f, 0.7f + yield, ParticleMotion.MiddleToTop);
    }

    [ContextMenu("Build Reactor Detail Flow")]
    public void Build()
    {
        Clear();
        reactorTarget = FindReactorTarget();
        if (reactorTarget == null)
        {
            Debug.LogWarning("ReactorDetailFlowRuntime: reactor target not found.");
            return;
        }

        h2Material = CreateMaterial("Generated Reactor H2 Flow", new Color(0.1f, 1f, 0.35f, 1f));
        co2Material = CreateMaterial("Generated Reactor CO2 Flow", new Color(0.72f, 0.74f, 0.76f, 1f));
        methanolMaterial = CreateMaterial("Generated Reactor Methanol Flow", new Color(0.78f, 0.18f, 1f, 1f));
        waterMaterial = CreateMaterial("Generated Reactor Water Flow", new Color(0.1f, 0.58f, 1f, 1f));
        catalystMaterial = CreateMaterial("Generated Reactor Catalyst Bed", new Color(1f, 0.64f, 0.16f, 0.82f));
        glowMaterial = CreateMaterial("Generated Reactor Temperature Glow", new Color(1f, 0.84f, 0.2f, 0.18f));

        for (int i = 0; i < h2ParticleCount; i++)
            h2Particles.Add(CreateParticle("H2 feed particle", h2Material, i / (float)h2ParticleCount));

        for (int i = 0; i < co2ParticleCount; i++)
            co2Particles.Add(CreateParticle("CO2 feed particle", co2Material, i / (float)co2ParticleCount));

        for (int i = 0; i < methanolParticleCount; i++)
            methanolParticles.Add(CreateParticle("Methanol product particle", methanolMaterial, i / (float)methanolParticleCount));

        for (int i = 0; i < waterParticleCount; i++)
            waterParticles.Add(CreateParticle("Water product particle", waterMaterial, i / (float)waterParticleCount));

        catalystBed = CreateVisualCylinder("Generated Catalyst Bed", catalystMaterial, new Vector3(1.35f, 0.32f, 1.35f));
        temperatureGlow = CreateVisualCylinder("Generated Reactor Temperature Glow", glowMaterial, new Vector3(2.85f, 2.2f, 2.85f));
    }

    [ContextMenu("Clear Reactor Detail Flow")]
    public void Clear()
    {
        ClearParticles(h2Particles);
        ClearParticles(co2Particles);
        ClearParticles(methanolParticles);
        ClearParticles(waterParticles);

        if (catalystBed != null) DestroyObject(catalystBed.gameObject);
        if (temperatureGlow != null) DestroyObject(temperatureGlow.gameObject);
        catalystBed = null;
        temperatureGlow = null;
    }

    private void ClearParticles(List<ParticlePoint> particles)
    {
        foreach (ParticlePoint point in particles)
        {
            if (point.Transform != null) DestroyObject(point.Transform.gameObject);
        }

        particles.Clear();
    }

    private void UpdateParticleSet(List<ParticlePoint> particles, Vector3 center, float radius, float intensity, float speed, ParticleMotion motion)
    {
        int visible = Mathf.Max(1, Mathf.RoundToInt(particles.Count * Mathf.Lerp(0.18f, 1f, intensity)));
        float scale = Mathf.Lerp(particleSize * 0.55f, particleSize * 1.65f, intensity);

        for (int i = 0; i < particles.Count; i++)
        {
            ParticlePoint particle = particles[i];
            bool active = i < visible;
            if (particle.Transform.gameObject.activeSelf != active)
            {
                particle.Transform.gameObject.SetActive(active);
            }

            if (!active)
            {
                continue;
            }

            particle.Phase += Time.deltaTime * speed * Mathf.Lerp(0.35f, 2.1f, intensity);
            float progress = Mathf.Repeat(particle.Phase + particle.Offset01, 1f);
            float angle = progress * Mathf.PI * 2f;
            float y;

            if (motion == ParticleMotion.BottomToMiddle)
            {
                y = Mathf.Lerp(-1.25f, 0.15f, progress);
            }
            else
            {
                y = Mathf.Lerp(-0.15f, 1.35f, progress);
            }

            float wobble = Mathf.Sin(angle * 2.1f) * 0.18f;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * (radius + wobble), y, Mathf.Sin(angle) * (radius + wobble));
            particle.Transform.position = pos;
            particle.Transform.localScale = Vector3.one * scale;
        }
    }

    private void UpdateTemperatureGlow(Vector3 center, float temperature, float yield)
    {
        if (temperatureGlow == null || glowMaterial == null)
        {
            return;
        }

        temperatureGlow.position = center;
        Color glowColor;
        if (temperature < 230f)
        {
            glowColor = new Color(0.1f, 0.45f, 1f, 0.18f);
        }
        else if (temperature < 250f)
        {
            glowColor = new Color(1f, 0.88f, 0.18f, 0.22f);
        }
        else if (temperature < 275f)
        {
            glowColor = new Color(1f, 0.48f, 0.08f, 0.25f);
        }
        else
        {
            glowColor = new Color(1f, 0.05f, 0.02f, 0.32f);
        }

        SetMaterialColor(glowMaterial, glowColor);
        temperatureGlow.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.14f, yield);
    }

    private void UpdateCatalystBed(Vector3 center, float yield)
    {
        if (catalystBed == null)
        {
            return;
        }

        catalystBed.position = center;
        catalystBed.localScale = new Vector3(1.35f, Mathf.Lerp(0.26f, 0.4f, yield), 1.35f);
    }

    private ParticlePoint CreateParticle(string objectName, Material material, float offset)
    {
        GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        particle.name = objectName;
        particle.transform.SetParent(transform);
        particle.transform.localScale = Vector3.one * particleSize;
        Renderer renderer = particle.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = material;
        }

        Collider collider = particle.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyObject(collider);
        }

        return new ParticlePoint { Transform = particle.transform, Offset01 = offset, Phase = offset };
    }

    private Transform CreateVisualCylinder(string objectName, Material material, Vector3 localScale)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = objectName;
        visual.transform.SetParent(transform);
        visual.transform.localScale = localScale;
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = material;
        }

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyObject(collider);
        }

        return visual.transform;
    }

    private Transform FindReactorTarget()
    {
        string[] candidates =
        {
            "Reactor Bed",
            "Reactor base model",
            "Reactor base model 1",
            "reactor steel skirt"
        };

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (string candidate in candidates)
        {
            foreach (Transform t in all)
            {
                if (string.Equals(t.name, candidate, System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
        }

        foreach (string candidate in candidates)
        {
            string lower = candidate.ToLowerInvariant();
            foreach (Transform t in all)
            {
                if (t.name.ToLowerInvariant().Contains(lower))
                {
                    return t;
                }
            }
        }

        return null;
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader) { name = materialName };
        SetMaterialColor(material, color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color);
    }

    private void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private enum ParticleMotion
    {
        BottomToMiddle,
        MiddleToTop
    }

    private class ParticlePoint
    {
        public Transform Transform;
        public float Offset01;
        public float Phase;
    }
}
