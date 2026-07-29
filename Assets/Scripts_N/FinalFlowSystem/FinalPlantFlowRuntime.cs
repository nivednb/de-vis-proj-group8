using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lightweight bridge between calculated plant state and shader-driven pipe flow.
/// No particle GameObjects are spawned. Visual speed/density are derived from
/// calculated mass flow, so UI controls and displayed results share one authority.
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalPlantFlowRuntime : MonoBehaviour
{
    [SerializeField] bool visualsEnabled = true;
    [SerializeField, Range(.25f, 2f)] float globalSpeed = 1f;
    [SerializeField, Range(.25f, 2f)] float globalDensity = 1f;
    [SerializeField, Range(.25f, 2f)] float globalIntensity = 1f;

    readonly List<RouteBinding> bindings = new();
    readonly Dictionary<PlantFlowKind, Material> materials = new();
    PlantProcessSimulator simulator;
    float nextVisualUpdate;
    const float VisualUpdateInterval = .08f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindFirstObjectByType<FinalPlantFlowRuntime>() != null) return;
        new GameObject("Final Engineering Flow Runtime").AddComponent<FinalPlantFlowRuntime>();
    }

    void Start()
    {
        ConfigureRoutes();
        EnsureCatalystIndicator();
        simulator = PlantProcessSimulator.Instance != null
            ? PlantProcessSimulator.Instance
            : FindFirstObjectByType<PlantProcessSimulator>();
        if (simulator != null)
        {
            simulator.SnapshotUpdated += ApplySnapshot;
            ApplySnapshot(simulator.Current);
        }
    }

    static void EnsureCatalystIndicator()
    {
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject candidate in objects)
        {
            if (!candidate.name.Equals("Catalyst_Bed", StringComparison.OrdinalIgnoreCase)) continue;
            if (candidate.GetComponent<CatalystBedColorAnimator>() == null)
                candidate.AddComponent<CatalystBedColorAnimator>();
            return;
        }
    }

    void OnDestroy()
    {
        if (simulator != null) simulator.SnapshotUpdated -= ApplySnapshot;
        foreach (Material material in materials.Values)
            if (material != null) Destroy(material);
    }

    public void SetVisualsEnabled(bool enabled)
    {
        visualsEnabled = enabled;
        foreach (RouteBinding binding in bindings)
            if (binding.animator != null)
            {
                binding.animator.isFlowing = enabled && binding.normalizedFlow > .005f;
                binding.animator.Apply();
            }
    }

    public void ToggleVisuals() => SetVisualsEnabled(!visualsEnabled);

    void ConfigureRoutes()
    {
        bindings.Clear();
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var classifiedRenderers = new List<ClassifiedRenderer>();
        foreach (GameObject root in objects)
        {
            if (!TryDescribe(root.name, out RouteDefinition route)) continue;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is ParticleSystemRenderer) continue;
                ConfigureRenderer(renderer, route);
                classifiedRenderers.Add(new ClassifiedRenderer(renderer, route));
            }
        }
        ConfigureConnectedFittings(objects, classifiedRenderers);
        Debug.Log($"Final engineering flow configured {bindings.Count} pipe renderers.");
    }

    void ApplySnapshot(PlantProcessSimulator.ProcessSnapshot s)
    {
        if (Time.unscaledTime < nextVisualUpdate) return;
        nextVisualUpdate = Time.unscaledTime + VisualUpdateInterval;
        Vector3 feedFractions = FeedMolarFractions(s);
        foreach (RouteBinding binding in bindings)
        {
            float flow = NormalizedMassFlow(binding.route.kind, s);
            binding.normalizedFlow = flow;
            float visible = Mathf.Clamp(flow, 0f, 1.3f);
            float response = Mathf.Sqrt(visible);
            PipeFlowAnimator animator = binding.animator;
            animator.speed = binding.route.speed * globalSpeed * Mathf.Lerp(.35f, 1.35f, response);
            animator.density = binding.route.density * globalDensity * Mathf.Lerp(.55f, 1.3f, visible);
            animator.flowIntensity = binding.route.intensity * globalIntensity * Mathf.Lerp(.25f, 1.25f, response);
            animator.isFlowing = visualsEnabled && flow > .005f;
            animator.speciesFractions = SpeciesFractions(binding.route.kind, feedFractions);
            animator.Apply();
        }
    }

    void ConfigureRenderer(Renderer renderer, RouteDefinition route)
    {
        renderer.sharedMaterial = GetMaterial(route.kind, route.color);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        PipeFlowAnimator animator = renderer.GetComponent<PipeFlowAnimator>();
        if (animator == null) animator = renderer.gameObject.AddComponent<PipeFlowAnimator>();
        animator.flowKind = route.kind;
        animator.flowColor = route.color;
        animator.reverseDirection = route.reverse;
        animator.pipeAlpha = route.alpha;
        animator.Apply();
        bindings.Add(new RouteBinding(animator, route));
    }

    void ConfigureConnectedFittings(GameObject[] objects, List<ClassifiedRenderer> classified)
    {
        foreach (GameObject candidate in objects)
        {
            if (candidate.transform.parent == null ||
                !candidate.transform.parent.name.Equals("Pipings", StringComparison.OrdinalIgnoreCase) ||
                (!candidate.name.StartsWith("pipe bend", StringComparison.OrdinalIgnoreCase) &&
                 !candidate.name.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase)))
                continue;
            Renderer renderer = candidate.GetComponent<Renderer>();
            if (renderer == null || renderer.GetComponent<PipeFlowAnimator>() != null) continue;
            ClassifiedRenderer nearest = default;
            float nearestGap = float.PositiveInfinity;
            foreach (ClassifiedRenderer anchor in classified)
            {
                float gap = BoundsGapSquared(renderer.bounds, anchor.renderer.bounds);
                if (gap >= nearestGap) continue;
                nearest = anchor;
                nearestGap = gap;
            }
            // A fitting must physically meet, or nearly meet, a classified route.
            if (nearest.renderer != null && nearestGap < 1.5f)
                ConfigureRenderer(renderer, nearest.route);
        }
    }

    static float BoundsGapSquared(Bounds a, Bounds b)
    {
        Vector3 delta = new(
            Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x)),
            Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y)),
            Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z)));
        return delta.sqrMagnitude;
    }

    static Vector3 FeedMolarFractions(PlantProcessSimulator.ProcessSnapshot s)
    {
        float h2KmolH = Mathf.Max(0f, s.h2InputKgH) / 2.016f;
        float co2KmolH = Mathf.Max(0f, s.co2CapturedKgH) / 44.01f;
        float total = Mathf.Max(.0001f, h2KmolH + co2KmolH);
        return new Vector3(h2KmolH / total, co2KmolH / total, 0f);
    }

    static Vector3 SpeciesFractions(PlantFlowKind kind, Vector3 feed)
    {
        return kind switch
        {
            PlantFlowKind.MixedFeed => feed,
            // This project models direct CO2 hydrogenation, so the feed and
            // pre-reactor syngas contain distinct H2 and CO2 tracers only.
            PlantFlowKind.SyngasCold or PlantFlowKind.SyngasHeated => feed,
            PlantFlowKind.RecycleGas => new Vector3(.74f, .26f, 0f),
            PlantFlowKind.ReactorEffluent => new Vector3(.58f, .32f, .10f),
            PlantFlowKind.CrudeMethanolVapourLiquid => new Vector3(.64f, .36f, 0f),
            _ => new Vector3(1f, 0f, 0f)
        };
    }

    static float NormalizedMassFlow(PlantFlowKind kind, PlantProcessSimulator.ProcessSnapshot s)
    {
        const float designH2 = 215f, designCO2 = 1510f, designSyngas = 1725f;
        const float designMethanol = 1250f, designCrude = 1953.125f;
        float waterProduct = s.methanolProductionKgH * (18f / 32f);
        float crude = s.methanolProductionKgH + waterProduct;
        return kind switch
        {
            PlantFlowKind.Hydrogen or PlantFlowKind.HydrogenFromStorage => s.h2InputKgH / designH2,
            PlantFlowKind.CarbonDioxide => s.co2CapturedKgH / designCO2,
            PlantFlowKind.RichAmine or PlantFlowKind.LeanAmine =>
                (s.co2CapturedKgH / designCO2) * (s.amineFlowPercent / 65f),
            PlantFlowKind.RecycleGas => s.recycleGasKgH / 450f,
            PlantFlowKind.MixedFeed or PlantFlowKind.SyngasCold or PlantFlowKind.SyngasHeated =>
                s.syngasFeedKgH / designSyngas,
            PlantFlowKind.ReactorEffluent => (crude + s.recycleGasKgH) / (designCrude + 450f),
            PlantFlowKind.CrudeMethanolVapourLiquid or PlantFlowKind.LiquidCrudeMethanol =>
                crude / designCrude,
            PlantFlowKind.MethanolProduct => s.methanolProductionKgH / designMethanol,
            _ => 0f
        };
    }

    Material GetMaterial(PlantFlowKind kind, Color color)
    {
        if (materials.TryGetValue(kind, out Material existing)) return existing;
        Shader shader = Shader.Find("Custom/PipeFlow");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = new(shader) { name = $"EngineeringFlow_{kind}" };
        if (material.HasProperty("_FlowColor")) material.SetColor("_FlowColor", color);
        materials.Add(kind, material);
        return material;
    }

    static bool TryDescribe(string name, out RouteDefinition route)
    {
        route = default;
        if (Starts(name, "H2Storage_pipe_")) route = Def(PlantFlowKind.HydrogenFromStorage, C(.1f, 1f, .22f), 1.05f, 17f);
        else if (Starts(name, "H2_pipe_")) route = Def(PlantFlowKind.Hydrogen, C(.1f, 1f, .22f), 1.15f, 18f);
        else if (Starts(name, "CO2_pipe_")) route = Def(PlantFlowKind.CarbonDioxide, C(.86f, .94f, 1f), .82f, 15f);
        else if (Starts(name, "RichAmine_pipe_")) route = Def(PlantFlowKind.RichAmine, C(.04f, .72f, .42f), .62f, 12f);
        else if (Starts(name, "LeanAmine_pipe_")) route = Def(PlantFlowKind.LeanAmine, C(.05f, .92f, .52f), .68f, 13f);
        else if (Starts(name, "RecycleGas_pipe_")) route = Def(PlantFlowKind.RecycleGas, C(.48f, .82f, 1f), .9f, 16f, true);
        else if (Starts(name, "MixedFeed_pipe_")) route = Def(PlantFlowKind.MixedFeed, C(.48f, .88f, .68f), 1f, 18f);
        else if (Starts(name, "Syngas_pipe_"))
        {
            int segment = TrailingNumber(name);
            route = Def(segment >= 6 ? PlantFlowKind.SyngasHeated : PlantFlowKind.SyngasCold,
                segment >= 6 ? C(1f, .58f, .12f) : C(.54f, .92f, .76f), 1.12f, 19f);
        }
        else if (Starts(name, "ReactorEffluent_pipe_")) route = Def(PlantFlowKind.ReactorEffluent, C(1f, .42f, .12f), .88f, 17f);
        else if (Starts(name, "CrudeMeOH_pipe_")) route = Def(PlantFlowKind.CrudeMethanolVapourLiquid, C(.72f, .18f, 1f), .7f, 14f);
        else if (Starts(name, "Liq_CrudeMeOH_pipe_")) route = Def(PlantFlowKind.LiquidCrudeMethanol, C(.35f, .42f, 1f), .55f, 12f);
        else if (Starts(name, "MethanolProduct_pipe_")) route = Def(PlantFlowKind.MethanolProduct, C(.2f, .78f, 1f), .48f, 11f);
        else return false;
        return true;
    }

    static bool Starts(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    static Color C(float r, float g, float b) => new(r, g, b, 1f);
    static RouteDefinition Def(PlantFlowKind k, Color c, float s, float d, bool reverse = false) =>
        new(k, c, s, d, 1.05f, .2f, reverse);
    static int TrailingNumber(string value)
    {
        int underscore = value.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(value[(underscore + 1)..], out int n) ? n : 0;
    }

    sealed class RouteBinding
    {
        public readonly PipeFlowAnimator animator;
        public readonly RouteDefinition route;
        public float normalizedFlow;
        public RouteBinding(PipeFlowAnimator animator, RouteDefinition route)
        { this.animator = animator; this.route = route; }
    }

    readonly struct ClassifiedRenderer
    {
        public readonly Renderer renderer;
        public readonly RouteDefinition route;
        public ClassifiedRenderer(Renderer renderer, RouteDefinition route)
        { this.renderer = renderer; this.route = route; }
    }

    readonly struct RouteDefinition
    {
        public readonly PlantFlowKind kind;
        public readonly Color color;
        public readonly float speed, density, intensity, alpha;
        public readonly bool reverse;
        public RouteDefinition(PlantFlowKind kind, Color color, float speed, float density,
            float intensity, float alpha, bool reverse)
        {
            this.kind = kind; this.color = color; this.speed = speed; this.density = density;
            this.intensity = intensity; this.alpha = alpha; this.reverse = reverse;
        }
    }
}
