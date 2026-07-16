using UnityEngine;

/// <summary>
/// Lightweight compatibility component used by the final flow runtime.
/// The previous project had an older PipeFlowAnimator script; the flow-only cleanup
/// removed it, but FinalPlantFlowRuntime still uses this component as a per-pipe marker
/// for manual refresh/removal. This version intentionally keeps the behaviour minimal:
/// all visible flow animation is handled by FinalPlantFlowRuntime through materials.
/// </summary>
public class PipeFlowAnimator : MonoBehaviour
{
    [Header("Flow Metadata")]
    public PlantFlowKind flowKind = PlantFlowKind.MixedFeed;
    public Color flowColor = Color.white;

    [Header("Visual Tuning")]
    [Min(0f)] public float speed = 1f;
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

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_FlowColor", flowColor);
        propertyBlock.SetFloat("_FlowSpeed", forceFlowOff ? 0f : speed * (reverseDirection ? -1f : 1f));
        propertyBlock.SetFloat("_FlowDensity", density);
        propertyBlock.SetFloat("_PipeAlpha", pipeAlpha);
        propertyBlock.SetFloat("_FlowIntensity", forceFlowOff ? 0f : flowIntensity);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
