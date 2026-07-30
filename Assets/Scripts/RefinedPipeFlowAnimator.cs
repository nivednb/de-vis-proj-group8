using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class RefinedPipeFlowAnimator : MonoBehaviour
{
    [Header("STREAM METRICS & PROPERTIES")]
    public string streamIdentifier = "H2_Stream"; // Matches stream name in ProcessSnapshot
    public float maxDesignFlowKgH = 250f;          // Flow rate corresponding to max speed
    public float baseFlowSpeed = 1.0f;             // Shader texture scroll multiplier
    
    [Header("SMOOTH TRANSITION SETTINGS")]
    public float lerpAccelerationSpeed = 8.0f;     // Smooth speed adjustment multiplier

    private Renderer pipeRenderer;
    private MaterialPropertyBlock propertyBlock;
    private float currentAnimatedOffset = 0f;
    private float currentTargetSpeed = 0f;
    private float smoothCurrentSpeed = 0f;

    // Shader Property IDs (Cached for GPU efficiency)
    private static readonly int FlowOffsetID = Shader.PropertyToID("_FlowOffset");
    private static readonly int FlowSpeedID = Shader.PropertyToID("_FlowSpeed");
    private static readonly int FlowOpacityID = Shader.PropertyToID("_FlowOpacity");

    void Awake()
    {
        pipeRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (PlantProcessSimulator.Instance == null) return;
        var snapshot = PlantProcessSimulator.Instance.LatestSnapshot;

        // 1. Fetch relevant mass flow rate (kg/h) based on stream tag
        float liveMassFlowKgH = GetLiveStreamFlow(snapshot, streamIdentifier);

        // 2. Calculate target visual speed normalized against max design capacity
        float normalizedFlow = Mathf.Clamp01(liveMassFlowKgH / maxDesignFlowKgH);
        currentTargetSpeed = normalizedFlow * baseFlowSpeed;

        // 3. Apply smooth Lerp acceleration/deceleration so speeds shift naturally
        smoothCurrentSpeed = Mathf.Lerp(smoothCurrentSpeed, currentTargetSpeed, Time.deltaTime * lerpAccelerationSpeed);

        // 4. Update texture scrolling offset
        currentAnimatedOffset += Time.deltaTime * smoothCurrentSpeed;
        if (currentAnimatedOffset > 1000f) currentAnimatedOffset -= 1000f; // Prevent floating-point overflow

        // 5. Pass updated properties cleanly to the GPU without material duplication
        pipeRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FlowOffsetID, currentAnimatedOffset);
        propertyBlock.SetFloat(FlowSpeedID, smoothCurrentSpeed);
        
        // Hide flow packets when flow rate becomes negligible
        float flowOpacity = (smoothCurrentSpeed < 0.01f) ? 0.0f : 1.0f;
        propertyBlock.SetFloat(FlowOpacityID, flowOpacity);

        pipeRenderer.SetPropertyBlock(propertyBlock);
    }

    private float GetLiveStreamFlow(PlantProcessSnapshot snapshot, string streamTag)
    {
        switch (streamTag)
        {
            case "H2_Stream":
                return snapshot.HydrogenProductionKgH;
            case "CO2_Stream":
                return snapshot.Co2CapturedKgH;
            case "Methanol_Stream":
                return snapshot.MethanolProductionKgH;
            case "Recycle_Stream":
                return snapshot.RecycleGasFlowKgH;
            default:
                return snapshot.HydrogenProductionKgH;
        }
    }
}