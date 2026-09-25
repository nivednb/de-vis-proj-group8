using UnityEngine;

[DisallowMultipleComponent]
public sealed class PipeFlowAnimator : MonoBehaviour
{
    /// <summary>
    /// PipeFlow.shader's noise lattices all repeat after 256 cells along the pipe, so the
    /// advected offset wraps at exactly that — the stream never visibly jumps or restarts.
    /// </summary>
    public const float NoisePeriod = 256f;

    public PlantFlowKind flowKind = PlantFlowKind.MixedFeed;
    public Color flowColor = Color.white;
    [Tooltip("Visual advection speed in metres per second.")]
    [Min(0f)] public float speed = 3f;
    [Tooltip("Noise cells per metre of pipe — the size of the eddies.")]
    [Min(.01f)] public float featureScale = .6f;
    [Range(0f, 1f)] public float pipeAlpha = .2f;
    [Range(0f, 3f)] public float flowIntensity = 1f;
    public bool isFlowing = true;
    public bool isGhostSupply;
    [HideInInspector] public bool useSharedProcessClock;

    [Header("Route geometry (world space)")]
    [Tooltip("A point on this segment's axis where the route distance equals flowStart.")]
    public Vector3 flowOrigin;
    [Tooltip("Direction the stream travels through this segment.")]
    public Vector3 flowDirection = Vector3.up;
    [Tooltip("Route distance in metres at flowOrigin, so consecutive segments continue one pattern.")]
    public float flowStart;
    bool hasRoute;

    static readonly int FlowColor = Shader.PropertyToID("_FlowColor"), Offset = Shader.PropertyToID("_FlowOffset"),
        Scale = Shader.PropertyToID("_FlowScale"), Alpha = Shader.PropertyToID("_BaseAlpha"),
        Intensity = Shader.PropertyToID("_FlowIntensity"), Ghost = Shader.PropertyToID("_GhostMode"),
        Liquid = Shader.PropertyToID("_IsLiquid"), TwoPhase = Shader.PropertyToID("_IsTwoPhase"),
        Dashed = Shader.PropertyToID("_Dashed"), OriginWS = Shader.PropertyToID("_FlowOriginWS"),
        DirWS = Shader.PropertyToID("_FlowDirWS");
    Renderer target;
    MaterialPropertyBlock block;
    float offset;

    void Awake() => Ensure();
    void OnEnable() => Apply();
    void OnValidate() => Apply();
    void Update()
    {
        Ensure();
        if (target == null || useSharedProcessClock) return;
        bool simulationRunning = PlantProcessSimulator.Instance == null || PlantProcessSimulator.Instance.IsRunning;
        if (isFlowing && simulationRunning)
            offset = Mathf.Repeat(offset + Time.deltaTime * speed * featureScale, NoisePeriod);
        PushOffset();
    }

    /// <summary>
    /// Drives this renderer from its route's shared transport offset (in noise cells), so
    /// every segment of one route advances as one continuous stream.
    /// </summary>
    public void SetSharedProcessOffset(float routeOffset)
    {
        useSharedProcessClock = true;
        offset = Mathf.Repeat(routeOffset, NoisePeriod);
        PushOffset();
    }

    /// <summary>Places this segment on its route: world origin, travel direction and the
    /// route distance at the origin.</summary>
    public void SetRoute(Vector3 origin, Vector3 direction, float startDistance)
    {
        flowOrigin = origin;
        flowDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up;
        flowStart = startDistance;
        hasRoute = true;
    }

    void PushOffset()
    {
        Ensure();
        if (target == null) return;
        target.GetPropertyBlock(block);
        block.SetFloat(Offset, offset);
        target.SetPropertyBlock(block);
    }

    public void Apply()
    {
        Ensure(); if (target == null) return;
        if (!hasRoute) DeriveRouteFromMesh();
        float liquid = 0f, twoPhase = 0f;
        switch (flowKind)
        {
            case PlantFlowKind.CrudeMethanolVapourLiquid: twoPhase = 1f; break;
            case PlantFlowKind.RichAmine:
            case PlantFlowKind.LeanAmine:
            case PlantFlowKind.LiquidCrudeMethanol:
            case PlantFlowKind.MethanolProduct:
            case PlantFlowKind.Water: liquid = 1f; break;
        }
        target.GetPropertyBlock(block);
        block.SetColor(FlowColor, flowColor);
        block.SetFloat(Scale, featureScale); block.SetFloat(Alpha, pipeAlpha);
        block.SetFloat(Intensity, isFlowing ? flowIntensity : 0f); block.SetFloat(Ghost, isGhostSupply ? 1f : 0f);
        block.SetFloat(Liquid, liquid); block.SetFloat(TwoPhase, twoPhase);
        block.SetFloat(Dashed, flowKind == PlantFlowKind.RecycleGas ? 1f : 0f);
        block.SetFloat(Offset, offset);
        block.SetVector(OriginWS, new Vector4(flowOrigin.x, flowOrigin.y, flowOrigin.z, flowStart));
        block.SetVector(DirWS, flowDirection);
        target.SetPropertyBlock(block);
    }

    /// <summary>Removes every per-renderer override so the renderer's own material draws
    /// exactly as authored.</summary>
    public void ClearOverrides()
    {
        Ensure();
        if (target != null) target.SetPropertyBlock(null);
    }

    /// <summary>Fallback for a standalone animator: run along the mesh's longest axis.</summary>
    void DeriveRouteFromMesh()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null) { flowOrigin = transform.position; flowDirection = transform.up; flowStart = 0f; return; }
        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;
        Vector3 axis = size.x >= size.y && size.x >= size.z ? Vector3.right : (size.y >= size.z ? Vector3.up : Vector3.forward);
        float major = Vector3.Dot(size, axis);
        flowOrigin = transform.TransformPoint(bounds.center - axis * major * .5f);
        flowDirection = transform.TransformDirection(axis).normalized;
        flowStart = 0f;
    }

    void Ensure() { if (target == null) target = GetComponent<Renderer>(); if (block == null) block = new MaterialPropertyBlock(); }
}
