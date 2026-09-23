using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lightweight bridge between calculated plant state and shader-driven pipe flow.
/// No particle GameObjects are spawned. Visual speed and brightness are derived from the
/// calculated mass flow, so UI controls and displayed results share one authority.
///
/// Each route (every "H2_pipe_*", "Syngas_pipe_*" ... family) is chained into one
/// continuous path: segments are ordered by their trailing number, oriented end-to-end from
/// their geometry, and given the route distance at which they start. All segments and the
/// elbows between them share one transport offset, so the stream runs through the whole
/// route as a single continuous flow instead of each piece looping its own copy.
///
/// The animated flow is only shown while the Flow Lab is open. Otherwise every pipe keeps
/// the opaque material it was authored with.
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalPlantFlowRuntime : MonoBehaviour
{
    public enum InspectionMode
    {
        All,
        FeedGases,
        CaptureLoop,
        SynthesisLoop,
        Product
    }

    [SerializeField] bool visualsEnabled = true;
    [SerializeField] InspectionMode inspectionMode = InspectionMode.All;
    [Tooltip("Base visual advection speed in metres per second at design flow.")]
    [SerializeField, Range(.25f, 20f)] float globalSpeed = 3.2f;
    [SerializeField, Range(.25f, 2f)] float globalDensity = 1f;
    [SerializeField, Range(.25f, 2f)] float globalIntensity = 1f;
    [Header("Continuous process transport")]
    [SerializeField, Min(.1f)] float startupStageDelay = .65f;
    [SerializeField, Min(.05f)] float stageFadeDuration = .55f;

    /// <summary>The shader's evolution lattices repeat after this many seconds.</summary>
    const float FlowTimePeriod = 2560f;
    static readonly int FlowTimeId = Shader.PropertyToID("_PipeFlowTime");

    readonly List<RouteBinding> bindings = new();
    readonly List<RouteChain> chains = new();
    readonly Dictionary<PlantFlowKind, Material> materials = new();
    PlantProcessSimulator simulator;
    PlantProcessSimulator.ProcessSnapshot currentSnapshot;
    float processClock;
    float flowTime;
    float nextVisualUpdate;
    bool flowLabActive;
    bool flowMaterialsApplied;
    const float VisualUpdateInterval = .08f;

    public static FinalPlantFlowRuntime Instance { get; private set; }

    /// <summary>True while the Flow Lab is open and the pipes show the animated stream.</summary>
    public bool FlowLabActive => flowLabActive;
    public bool VisualsEnabled => visualsEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindFirstObjectByType<FinalPlantFlowRuntime>() != null) return;
        new GameObject("Final Engineering Flow Runtime").AddComponent<FinalPlantFlowRuntime>();
    }

    void Awake() => Instance = this;

    void Start()
    {
        ConfigureRoutes();
        LightweightReactorVisual.Configure(gameObject);
        EnsureCatalystIndicator();
        simulator = PlantProcessSimulator.Instance != null
            ? PlantProcessSimulator.Instance
            : FindFirstObjectByType<PlantProcessSimulator>();
        if (simulator != null)
        {
            simulator.SnapshotUpdated += ApplySnapshot;
            ApplySnapshot(simulator.Current);
        }
        // The Flow Lab may have been opened before the routes existed; apply its state now.
        flowMaterialsApplied = false;
        RefreshMaterials();
    }

    void Update()
    {
        bool simulationRunning = simulator == null || simulator.IsRunning;
        if (simulationRunning)
        {
            processClock += Time.deltaTime;
            flowTime = Mathf.Repeat(flowTime + Time.deltaTime, FlowTimePeriod);
        }
        if (!flowMaterialsApplied) return;
        Shader.SetGlobalFloat(FlowTimeId, flowTime);

        // Each route advances its own offset by integrating its current speed. Integrating
        // (rather than multiplying elapsed time by speed) means a slider change alters how
        // fast the stream moves from now on without teleporting what is already in the pipe.
        if (simulationRunning)
            foreach (RouteChain chain in chains)
                chain.offset = Mathf.Repeat(chain.offset + Time.deltaTime * chain.speed * chain.featureScale,
                    PipeFlowAnimator.NoisePeriod);

        foreach (RouteBinding binding in bindings)
        {
            PipeFlowAnimator animator = binding.animator;
            if (animator == null) continue;

            float stageStart = ProcessStage(binding.route.kind) * startupStageDelay;
            float stageAvailability = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(stageStart, stageStart + stageFadeDuration, processClock));
            if (!ReactionPrerequisiteSatisfied(binding.route.kind, currentSnapshot))
                stageAvailability = 0f;

            binding.stageAvailability = stageAvailability;
            animator.flowIntensity = binding.targetIntensity * stageAvailability;
            bool active = simulationRunning && IsShown(binding);
            animator.isFlowing = active;
            animator.SetSharedProcessOffset(binding.chain.offset);

            if (Mathf.Abs(binding.lastAppliedAvailability - stageAvailability) > .02f ||
                active != binding.lastAppliedActive)
            {
                animator.Apply();
                binding.lastAppliedAvailability = stageAvailability;
                binding.lastAppliedActive = active;
            }
        }
    }

    bool IsShown(RouteBinding binding) =>
        IsIncludedInInspection(binding.route.kind) &&
        binding.normalizedFlow > .005f &&
        binding.stageAvailability > .001f;

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
        if (Instance == this) Instance = null;
        if (simulator != null) simulator.SnapshotUpdated -= ApplySnapshot;
        foreach (Material material in materials.Values)
            if (material != null) Destroy(material);
    }

    // ---- Flow Lab visibility ---------------------------------------------------

    /// <summary>
    /// Opens or closes the Flow Lab view of the pipes. Closed, every pipe draws with its
    /// original opaque material; open, the pipes turn into transparent sections showing the
    /// animated stream. Opening always starts with the stream visuals switched on.
    /// </summary>
    public void SetFlowLabActive(bool active)
    {
        if (flowLabActive == active) return;
        flowLabActive = active;
        if (active) visualsEnabled = true;
        RefreshMaterials();
    }

    public void SetVisualsEnabled(bool enabled)
    {
        visualsEnabled = enabled;
        RefreshMaterials();
    }

    public void ToggleVisuals() => SetVisualsEnabled(!visualsEnabled);

    public void SetInspectionMode(int mode)
    {
        inspectionMode = (InspectionMode)Mathf.Clamp(mode, 0, (int)InspectionMode.Product);
        if (!flowMaterialsApplied) return;
        foreach (RouteBinding binding in bindings)
            if (binding.animator != null)
            {
                binding.animator.isFlowing = IsShown(binding);
                binding.animator.Apply();
            }
    }

    /// <summary>Swaps every classified pipe between its authored material and the flow
    /// material, depending on whether the Flow Lab is showing the stream.</summary>
    void RefreshMaterials()
    {
        bool show = flowLabActive && visualsEnabled;
        if (show == flowMaterialsApplied) return;
        flowMaterialsApplied = show;
        foreach (RouteBinding binding in bindings)
        {
            Renderer renderer = binding.renderer;
            PipeFlowAnimator animator = binding.animator;
            if (renderer == null || animator == null) continue;
            if (show)
            {
                renderer.sharedMaterial = GetMaterial(binding.route.kind, binding.route.color);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                animator.isFlowing = IsShown(binding);
                animator.Apply();
                binding.lastAppliedActive = animator.isFlowing;
                binding.lastAppliedAvailability = binding.stageAvailability;
            }
            else
            {
                animator.ClearOverrides();
                renderer.sharedMaterials = binding.originalMaterials;
                renderer.shadowCastingMode = binding.originalShadowCasting;
                renderer.receiveShadows = binding.originalReceiveShadows;
            }
        }
    }

    // ---- route discovery ---------------------------------------------------------

    void ConfigureRoutes()
    {
        bindings.Clear();
        chains.Clear();
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var families = new Dictionary<string, List<Segment>>(StringComparer.OrdinalIgnoreCase);
        foreach (GameObject root in objects)
        {
            if (!TryDescribe(root.name, out RouteDefinition route, out string family)) continue;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is ParticleSystemRenderer) continue;
                if (!families.TryGetValue(family, out List<Segment> list))
                    families.Add(family, list = new List<Segment>());
                list.Add(new Segment(renderer, route, TrailingNumber(root.name)));
            }
        }

        var joints = new List<Joint>();
        foreach (KeyValuePair<string, List<Segment>> family in families)
            BuildChain(family.Key, family.Value, joints);
        ConfigureConnectedFittings(objects, joints);
        Debug.Log($"Final engineering flow configured {bindings.Count} pipe renderers on {chains.Count} continuous routes.");
    }

    /// <summary>
    /// Orders one route family's segments by number (the plant's naming follows the flow
    /// direction), orients each segment from the end nearest its predecessor to the end
    /// nearest its successor, and accumulates the route distance so consecutive segments
    /// continue one pattern.
    /// </summary>
    void BuildChain(string family, List<Segment> segments, List<Joint> joints)
    {
        segments.Sort((x, y) => x.number.CompareTo(y.number));
        var chain = new RouteChain(family, segments[0].route.speed, segments[0].route.density * globalDensity * .035f);
        chains.Add(chain);

        int count = segments.Count;
        var ends = new (Vector3 a, Vector3 b)[count];
        for (int i = 0; i < count; i++) ends[i] = SegmentEnds(segments[i].renderer);

        float distance = 0f;
        Vector3 previousExit = default, previousDirection = default;
        for (int i = 0; i < count; i++)
        {
            (Vector3 a, Vector3 b) = ends[i];
            Vector3 entry, exit;
            if (i + 1 < count)
            {
                // Exit through whichever end sits closer to the next segment.
                (Vector3 na, Vector3 nb) = ends[i + 1];
                float da = Mathf.Min(Vector3.Distance(a, na), Vector3.Distance(a, nb));
                float db = Mathf.Min(Vector3.Distance(b, na), Vector3.Distance(b, nb));
                (entry, exit) = db <= da ? (a, b) : (b, a);
            }
            else if (i > 0)
            {
                (entry, exit) = Vector3.Distance(a, previousExit) <= Vector3.Distance(b, previousExit) ? (a, b) : (b, a);
            }
            else
            {
                // A lone segment has nothing to orient against; fall back to the route's
                // declared direction.
                (entry, exit) = segments[i].route.reverse ? (a, b) : (b, a);
            }

            Vector3 direction = (exit - entry).normalized;
            if (i > 0)
            {
                float gap = Vector3.Distance(previousExit, entry);
                joints.Add(new Joint((previousExit + entry) * .5f, distance + gap * .5f,
                    (previousDirection + direction).normalized, segments[i].route, chain));
                distance += gap;
            }

            PipeFlowAnimator animator = ConfigureRenderer(segments[i].renderer, segments[i].route, chain);
            animator.SetRoute(entry, direction, distance);

            distance += Vector3.Distance(entry, exit);
            previousExit = exit;
            previousDirection = direction;
        }
    }

    /// <summary>World-space end points of a straight segment along its mesh's longest axis.</summary>
    static (Vector3 a, Vector3 b) SegmentEnds(Renderer renderer)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null)
        {
            Bounds wb = renderer.bounds;
            Vector3 ext = wb.extents;
            Vector3 axisW = ext.x >= ext.y && ext.x >= ext.z ? Vector3.right : (ext.y >= ext.z ? Vector3.up : Vector3.forward);
            float half = Vector3.Dot(ext, axisW);
            return (wb.center - axisW * half, wb.center + axisW * half);
        }
        Bounds bounds = mesh.bounds;
        Vector3 size = Vector3.Scale(bounds.size, renderer.transform.lossyScale);
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        Vector3 axis = size.x >= size.y && size.x >= size.z ? Vector3.right : (size.y >= size.z ? Vector3.up : Vector3.forward);
        float major = Vector3.Dot(bounds.size, axis) * .5f;
        Transform t = renderer.transform;
        return (t.TransformPoint(bounds.center - axis * major), t.TransformPoint(bounds.center + axis * major));
    }

    void ApplySnapshot(PlantProcessSimulator.ProcessSnapshot s)
    {
        currentSnapshot = s;
        if (Time.unscaledTime < nextVisualUpdate) return;
        nextVisualUpdate = Time.unscaledTime + VisualUpdateInterval;
        foreach (RouteBinding binding in bindings)
        {
            float flow = NormalizedMassFlow(binding.route.kind, s);
            binding.normalizedFlow = flow;
            float visible = Mathf.Clamp(flow, 0f, 1.3f);
            float response = Mathf.Sqrt(visible);
            PipeFlowAnimator animator = binding.animator;
            animator.speed = binding.route.speed * globalSpeed * Mathf.Lerp(.35f, 1.35f, response);
            binding.chain.speed = animator.speed;
            binding.targetIntensity = binding.route.intensity * globalIntensity *
                Mathf.Lerp(.25f, 1.25f, response);
            animator.flowIntensity = binding.targetIntensity * binding.stageAvailability;
            if (!flowMaterialsApplied) continue;
            animator.isFlowing = IsShown(binding);
            animator.Apply();
        }
    }

    static int ProcessStage(PlantFlowKind kind)
    {
        return kind switch
        {
            PlantFlowKind.Hydrogen or PlantFlowKind.HydrogenFromStorage or
                PlantFlowKind.CarbonDioxide or PlantFlowKind.RichAmine or
                PlantFlowKind.LeanAmine => 0,
            PlantFlowKind.MixedFeed => 1,
            PlantFlowKind.SyngasCold => 2,
            PlantFlowKind.SyngasHeated => 3,
            PlantFlowKind.ReactorEffluent => 4,
            PlantFlowKind.CrudeMethanolVapourLiquid => 5,
            PlantFlowKind.LiquidCrudeMethanol => 6,
            PlantFlowKind.MethanolProduct or PlantFlowKind.RecycleGas => 7,
            _ => 0
        };
    }

    static bool ReactionPrerequisiteSatisfied(PlantFlowKind kind,
        PlantProcessSimulator.ProcessSnapshot snapshot)
    {
        return kind switch
        {
            PlantFlowKind.MixedFeed or PlantFlowKind.SyngasCold or PlantFlowKind.SyngasHeated =>
                snapshot.h2InputKgH > .01f && snapshot.co2CapturedKgH > .01f,
            PlantFlowKind.ReactorEffluent =>
                snapshot.syngasFeedKgH > .01f && snapshot.reactorYieldPercent > .01f,
            PlantFlowKind.CrudeMethanolVapourLiquid or PlantFlowKind.LiquidCrudeMethanol or
                PlantFlowKind.MethanolProduct or PlantFlowKind.RecycleGas =>
                snapshot.methanolProductionKgH > .01f,
            _ => true
        };
    }

    bool IsIncludedInInspection(PlantFlowKind kind)
    {
        switch (inspectionMode)
        {
            case InspectionMode.FeedGases:
                return kind == PlantFlowKind.Hydrogen ||
                    kind == PlantFlowKind.HydrogenFromStorage ||
                    kind == PlantFlowKind.CarbonDioxide ||
                    kind == PlantFlowKind.MixedFeed;
            case InspectionMode.CaptureLoop:
                return kind == PlantFlowKind.RichAmine ||
                    kind == PlantFlowKind.LeanAmine ||
                    kind == PlantFlowKind.CarbonDioxide;
            case InspectionMode.SynthesisLoop:
                return kind == PlantFlowKind.MixedFeed ||
                    kind == PlantFlowKind.SyngasCold ||
                    kind == PlantFlowKind.SyngasHeated ||
                    kind == PlantFlowKind.RecycleGas ||
                    kind == PlantFlowKind.ReactorEffluent;
            case InspectionMode.Product:
                return kind == PlantFlowKind.ReactorEffluent ||
                    kind == PlantFlowKind.CrudeMethanolVapourLiquid ||
                    kind == PlantFlowKind.LiquidCrudeMethanol ||
                    kind == PlantFlowKind.MethanolProduct;
            default:
                return true;
        }
    }

    PipeFlowAnimator ConfigureRenderer(Renderer renderer, RouteDefinition route, RouteChain chain)
    {
        PipeFlowAnimator animator = renderer.GetComponent<PipeFlowAnimator>();
        if (animator == null) animator = renderer.gameObject.AddComponent<PipeFlowAnimator>();
        animator.flowKind = route.kind;
        animator.flowColor = route.color;
        animator.useSharedProcessClock = true;
        animator.pipeAlpha = route.alpha;
        animator.featureScale = chain.featureScale;
        animator.ClearOverrides();
        bindings.Add(new RouteBinding(renderer, animator, route, chain));
        return animator;
    }

    /// <summary>
    /// Elbows and loose cylinders under "Pipings" belong to whichever route they join. An
    /// elbow sitting on a joint between two chained segments takes that joint's position in
    /// the route, so the stream carries straight through the bend.
    /// </summary>
    void ConfigureConnectedFittings(GameObject[] objects, List<Joint> joints)
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

            Vector3 centre = renderer.bounds.center;
            Joint nearestJoint = null;
            float jointDistance = float.PositiveInfinity;
            foreach (Joint joint in joints)
            {
                float d = renderer.bounds.SqrDistance(joint.point);
                if (d >= jointDistance) continue;
                jointDistance = d;
                nearestJoint = joint;
            }
            if (nearestJoint != null && jointDistance < 1.5f)
            {
                PipeFlowAnimator animator = ConfigureRenderer(renderer, nearestJoint.route, nearestJoint.chain);
                animator.SetRoute(nearestJoint.point, nearestJoint.direction, nearestJoint.distance);
                    continue;
            }

            // Otherwise attach to the closest classified segment and continue its axis.
            RouteBinding nearest = null;
            float nearestGap = float.PositiveInfinity;
            foreach (RouteBinding anchor in bindings)
            {
                float gap = BoundsGapSquared(renderer.bounds, anchor.renderer.bounds);
                if (gap >= nearestGap) continue;
                nearest = anchor;
                nearestGap = gap;
            }
            // A fitting must physically meet, or nearly meet, a classified route.
            if (nearest == null || nearestGap >= 1.5f) continue;
            PipeFlowAnimator fitting = ConfigureRenderer(renderer, nearest.route, nearest.chain);
            PipeFlowAnimator anchorAnimator = nearest.animator;
            fitting.SetRoute(anchorAnimator.flowOrigin, anchorAnimator.flowDirection, anchorAnimator.flowStart);
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

    /// <summary>
    /// Visual scaling only. The mass flow itself comes from <see cref="PipeStreamState"/> —
    /// the same evaluation the mass-flow probe reports — so the animation speed and the
    /// number the probe shows can never disagree about which lines are busy.
    /// </summary>
    static float NormalizedMassFlow(PlantFlowKind kind, PlantProcessSimulator.ProcessSnapshot s)
    {
        float actual = PipeStreamState.Evaluate(kind, s).MassFlowKgH;
        return actual / Mathf.Max(1f, PipeStreamState.DesignMassFlowKgH(kind));
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

    static bool TryDescribe(string name, out RouteDefinition route, out string family)
    {
        route = default;
        family = null;
        if (Starts(name, "H2Storage_pipe_")) { route = Def(PlantFlowKind.HydrogenFromStorage, 1.05f, 17f); family = "H2Storage"; }
        else if (Starts(name, "H2_pipe_")) { route = Def(PlantFlowKind.Hydrogen, 1.15f, 18f); family = "H2"; }
        else if (Starts(name, "CO2_pipe_")) { route = Def(PlantFlowKind.CarbonDioxide, .82f, 15f); family = "CO2"; }
        else if (Starts(name, "RichAmine_pipe_")) { route = Def(PlantFlowKind.RichAmine, .62f, 12f); family = "RichAmine"; }
        else if (Starts(name, "LeanAmine_pipe_")) { route = Def(PlantFlowKind.LeanAmine, .68f, 13f); family = "LeanAmine"; }
        else if (Starts(name, "RecycleGas_pipe_")) { route = Def(PlantFlowKind.RecycleGas, .9f, 16f); family = "RecycleGas"; }
        else if (Starts(name, "MixedFeed_pipe_")) { route = Def(PlantFlowKind.MixedFeed, 1f, 18f); family = "MixedFeed"; }
        else if (Starts(name, "Syngas_pipe_"))
        {
            int segment = TrailingNumber(name);
            route = Def(segment >= 6 ? PlantFlowKind.SyngasHeated : PlantFlowKind.SyngasCold, 1.12f, 19f);
            family = "Syngas";
        }
        else if (Starts(name, "ReactorEffluent_pipe_")) { route = Def(PlantFlowKind.ReactorEffluent, .88f, 17f); family = "ReactorEffluent"; }
        else if (Starts(name, "CrudeMeOH_pipe_")) { route = Def(PlantFlowKind.CrudeMethanolVapourLiquid, .7f, 14f); family = "CrudeMeOH"; }
        else if (Starts(name, "Liq_CrudeMeOH_pipe_")) { route = Def(PlantFlowKind.LiquidCrudeMethanol, .55f, 12f); family = "LiqCrudeMeOH"; }
        else if (Starts(name, "MethanolProduct_pipe_")) { route = Def(PlantFlowKind.MethanolProduct, .48f, 11f); family = "MethanolProduct"; }
        else return false;
        return true;
    }

    static bool Starts(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    /// <summary>Colour always comes from <see cref="PlantStreamLegend"/>, never from a literal
    /// here — that is what keeps every pipe matching the legend row that explains it.</summary>
    static RouteDefinition Def(PlantFlowKind k, float s, float d, bool reverse = false) =>
        new(k, PlantStreamLegend.ColorFor(k), s, d, 1.55f, .24f, reverse);
    static int TrailingNumber(string value)
    {
        int underscore = value.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(value[(underscore + 1)..], out int n) ? n : 0;
    }

    /// <summary>One continuous route: every segment and elbow on it shares this offset.</summary>
    sealed class RouteChain
    {
        public readonly string family;
        public float speed;
        public readonly float featureScale;
        public float offset;
        public RouteChain(string family, float speed, float featureScale)
        { this.family = family; this.speed = speed; this.featureScale = Mathf.Max(.05f, featureScale); }
    }

    sealed class RouteBinding
    {
        public readonly Renderer renderer;
        public readonly PipeFlowAnimator animator;
        public readonly RouteDefinition route;
        public readonly RouteChain chain;
        public readonly Material[] originalMaterials;
        public readonly ShadowCastingMode originalShadowCasting;
        public readonly bool originalReceiveShadows;
        public float normalizedFlow;
        public float stageAvailability;
        public float targetIntensity;
        public float lastAppliedAvailability = -1f;
        public bool lastAppliedActive;
        public RouteBinding(Renderer renderer, PipeFlowAnimator animator, RouteDefinition route, RouteChain chain)
        {
            this.renderer = renderer; this.animator = animator; this.route = route; this.chain = chain;
            originalMaterials = renderer.sharedMaterials;
            originalShadowCasting = renderer.shadowCastingMode;
            originalReceiveShadows = renderer.receiveShadows;
        }
    }

    readonly struct Segment
    {
        public readonly Renderer renderer;
        public readonly RouteDefinition route;
        public readonly int number;
        public Segment(Renderer renderer, RouteDefinition route, int number)
        { this.renderer = renderer; this.route = route; this.number = number; }
    }

    sealed class Joint
    {
        public readonly Vector3 point;
        public readonly float distance;
        public readonly Vector3 direction;
        public readonly RouteDefinition route;
        public readonly RouteChain chain;
        public Joint(Vector3 point, float distance, Vector3 direction, RouteDefinition route, RouteChain chain)
        {
            this.point = point; this.distance = distance;
            this.direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up;
            this.route = route; this.chain = chain;
        }
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
