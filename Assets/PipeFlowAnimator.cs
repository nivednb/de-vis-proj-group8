using UnityEngine;

/// <summary>
/// Lightweight compatibility component used by the final flow runtime.
/// The visible final flow animation is handled mainly by FinalPlantFlowRuntime
/// and FlowVisualCorrectionRuntime. This component keeps the older API fields that
/// FinalPlantFlowRuntime expects, so the flow-only branch compiles cleanly.
/// </summary>
public class PipeFlowAnimator : MonoBehaviour
{
    [Header("Flow Metadata")]
    public PlantFlowKind flowKind = PlantFlowKind.MixedFeed;
    public Color flowColor = Color.white;

    [Header("Visual Tuning")]
    [Min(0f)] public float speed = 1f;

    // Compatibility fields expected by FinalPlantFlowRuntime.
    public float speedOverride = -1f;
    public bool isFlowing = true;
    public bool isGhostSupply = false;

    [Min(0f)] public float density = 1f;
    [Range(0f, 1f)] public float pipeAlpha = 0.35f;
    [Range(0f, 5f)] public float flowIntensity = 1f;
    public bool reverseDirection;
    public bool forceFlowOff;

    Renderer cachedRenderer;
    MaterialPropertyBlock propertyBlock;

    void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (cachedRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        float effectiveSpeed = speedOverride >= 0f ? speedOverride : speed;
        bool flowEnabled = isFlowing && !forceFlowOff;

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_FlowColor", flowColor);
        propertyBlock.SetFloat("_FlowSpeed", flowEnabled ? effectiveSpeed * (reverseDirection ? -1f : 1f) : 0f);
        propertyBlock.SetFloat("_FlowDensity", density);
        propertyBlock.SetFloat("_PipeAlpha", pipeAlpha);
        propertyBlock.SetFloat("_FlowIntensity", flowEnabled ? flowIntensity : 0f);
        propertyBlock.SetFloat("_IsGhostSupply", isGhostSupply ? 1f : 0f);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
