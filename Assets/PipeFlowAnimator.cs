using UnityEngine;

/// <summary>
/// Animates process-flow bands directly on a pipe mesh using Custom/PipeFlow.
/// Keeping the animation on the renderer UVs guarantees that it follows bends
/// instead of drawing straight waypoint lines through open air.
/// </summary>
[DisallowMultipleComponent]
public class PipeFlowAnimator : MonoBehaviour
{
    [Header("Flow Metadata")]
    public PlantFlowKind flowKind = PlantFlowKind.MixedFeed;
    public Color flowColor = Color.white;

    [Header("Visual Tuning")]
    [Min(0f)] public float speed = 1f;
    public float speedOverride = -1f;
    public bool isFlowing = true;
    public bool isGhostSupply;
    [Min(0f)] public float density = 1f;
    [Range(0f, 1f)] public float pipeAlpha = 0.35f;
    [Range(0f, 5f)] public float flowIntensity = 1f;
    public bool reverseDirection;
    public bool forceFlowOff;

    static readonly int FlowColorId = Shader.PropertyToID("_FlowColor");
    static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    static readonly int FlowOffsetId = Shader.PropertyToID("_FlowOffset");
    static readonly int TilingId = Shader.PropertyToID("_Tiling");
    static readonly int BaseAlphaId = Shader.PropertyToID("_BaseAlpha");
    static readonly int FlowIntensityId = Shader.PropertyToID("_FlowIntensity");
    static readonly int GhostModeId = Shader.PropertyToID("_GhostMode");

    Renderer cachedRenderer;
    MaterialPropertyBlock propertyBlock;
    float flowOffset;

    void Awake()
    {
        EnsureRenderer();
    }

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    void Update()
    {
        if (cachedRenderer == null || propertyBlock == null)
        {
            EnsureRenderer();
        }

        if (cachedRenderer == null)
        {
            return;
        }

        bool flowEnabled = isFlowing && !forceFlowOff;
        if (flowEnabled)
        {
            float effectiveSpeed = speedOverride >= 0f ? speedOverride : speed;
            float direction = reverseDirection ? -1f : 1f;
            flowOffset = Mathf.Repeat(flowOffset + Time.deltaTime * effectiveSpeed * direction, 1f);
        }

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FlowOffsetId, flowOffset);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    public void Apply()
    {
        EnsureRenderer();
        if (cachedRenderer == null)
        {
            return;
        }

        float effectiveSpeed = speedOverride >= 0f ? speedOverride : speed;
        bool flowEnabled = isFlowing && !forceFlowOff;
        float signedSpeed = effectiveSpeed * (reverseDirection ? -1f : 1f);

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(FlowColorId, flowColor);
        propertyBlock.SetFloat(FlowSpeedId, flowEnabled ? signedSpeed : 0f);
        propertyBlock.SetFloat(FlowOffsetId, flowOffset);
        propertyBlock.SetFloat(TilingId, density);
        propertyBlock.SetFloat(BaseAlphaId, pipeAlpha);
        propertyBlock.SetFloat(FlowIntensityId, flowEnabled ? flowIntensity : 0f);
        propertyBlock.SetFloat(GhostModeId, isGhostSupply ? 1f : 0f);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    void EnsureRenderer()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }
}
