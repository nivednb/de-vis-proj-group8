using UnityEngine;

/// <summary>
/// Drop onto any pipe GameObject (straight Cylinder or pipe-bend prefab) that has a
/// MeshRenderer using the PipeFlow shader. Scrolls the texture along V to fake fluid
/// movement. One script per pipe object; speed/direction/color come from the assigned
/// Material instance so you only ever touch the Material, never this script, when
/// re-tagging a pipe to a different fluid.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class PipeFlowAnimator : MonoBehaviour
{
    [Tooltip("Reverses scroll direction. Use for recycle/return lines so flow reads correctly against the pipe's mesh orientation.")]
    public bool reverseDirection = false;

    [Tooltip("Optional override. If 0, uses the material's own _FlowSpeed.")]
    public float speedOverride = 0f;

    [Tooltip("Master on/off — flip this from a process-state script (e.g. when a unit is shut down).")]
    public bool isFlowing = true;

    [Header("Ghost Supply (unmodeled source line)")]
    [Tooltip("Marks this pipe as feeding from an unmodeled external source (e.g. CO2 ghost supplier). Dims alpha and widens dash spacing via the shared material's ghost properties, without needing a separate material asset.")]
    public bool isGhostSupply = false;

    private MaterialPropertyBlock _block;
    private MeshRenderer _renderer;
    private float _offset;
    private static readonly int FlowColorId = Shader.PropertyToID("_FlowColor");
    private static readonly int OffsetId = Shader.PropertyToID("_FlowOffset");
    private static readonly int GhostModeId = Shader.PropertyToID("_GhostMode");

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _block = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (!isFlowing) return;

        float baseSpeed = speedOverride > 0f
            ? speedOverride
            : (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty("_FlowSpeed")
                ? _renderer.sharedMaterial.GetFloat("_FlowSpeed")
                : 1f);

        float dir = reverseDirection ? -1f : 1f;
        _offset += baseSpeed * dir * Time.deltaTime;

        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(OffsetId, _offset);
        _block.SetFloat(GhostModeId, isGhostSupply ? 1f : 0f);
        _renderer.SetPropertyBlock(_block);
    }

    /// <summary>Flip ghost state at runtime — e.g. when a future sprint replaces the
    /// ghost supplier with a real modeled CO2 source unit, call SetGhostSupply(false).</summary>
    public void SetGhostSupply(bool ghost) => isGhostSupply = ghost;

    /// <summary>Call this if you ever swap a pipe's material at runtime (e.g. process-mode toggle).</summary>
    public void ResetOffset() => _offset = 0f;
}
