using UnityEngine;

/// <summary>
/// PlantPipeManager — creates cylinder-based pipes connecting PtMeOH plant modules.
/// Attach to an empty GameObject named "PipeManager" in the scene.
/// All pipe positions are approximate based on model origins + visual offsets.
/// Adjust individual pipe positions in Inspector after generation.
/// </summary>
public class PlantPipeManager : MonoBehaviour
{
    [Header("Pipe Settings")]
    public float pipeRadius = 0.15f;
    public Material h2PipeMaterial;         // Green — H2 lines
    public Material aminePipeMaterial;      // Orange — amine lines
    public Material gasPipeMaterial;        // Red — compressed gas lines
    public Material condensatePipeMaterial; // Blue — liquid/condensate lines
    public Material recyclePipeMaterial;    // Cyan — recycle loop

    [Header("Auto Generate")]
    public bool generateOnStart = false;

    void Start()
    {
        if (generateOnStart)
            GenerateAllPipes();
    }

    [ContextMenu("Generate All Pipes")]
    public void GenerateAllPipes()
    {
        // Clear existing pipes
        Transform existing = transform.Find("Pipes");
        if (existing != null)
            DestroyImmediate(existing.gameObject);

        GameObject pipesRoot = new GameObject("Pipes");
        pipesRoot.transform.SetParent(transform);

        // === PIPE CONNECTIONS ===
        // Format: CreatePipe(parent, name, startPos, endPos, material)
        // All Y positions are approximate — adjust visually after generation

        // 1. Electrolyzer → Absorber (H2, green)
        CreatePipe(pipesRoot.transform, "Pipe_Electrolyzer_to_Absorber",
            new Vector3(-57f, -3f, -18.52f),
            new Vector3(-48f, -3f, -18.52f),
            h2PipeMaterial);

        // 2. Absorber → Desorber (orange amine)
        CreatePipeWithElbow(pipesRoot.transform, "Pipe_Absorber_to_Desorber",
            new Vector3(-45f, -5f, -18.52f),
            new Vector3(-31f, -5f, -18.52f),
            -5.5f,
            aminePipeMaterial);

        // 3. Desorber → Compressor (red gas)
        CreatePipeWithElbow(pipesRoot.transform, "Pipe_Desorber_to_Compressor",
            new Vector3(-28f, 2f, -18.52f),
            new Vector3(-15f, -5f, -18.52f),
            2f,
            gasPipeMaterial);

        // 4. Compressor → Reactor (red gas)
        CreatePipeWithElbow(pipesRoot.transform, "Pipe_Compressor_to_Reactor",
            new Vector3(-9f, -5f, -18.52f),
            new Vector3(3f, 12f, -18.83f),
            3f,
            gasPipeMaterial);

        // 5. Reactor → Condenser (red gas, straight)
        CreatePipe(pipesRoot.transform, "Pipe_Reactor_to_Condenser",
            new Vector3(5f, 0.73f, -18.83f),
            new Vector3(22f, 0.73f, -18.97f),
            gasPipeMaterial);

        // 6. Condenser → Distillation (blue condensate)
        CreatePipeWithElbow(pipesRoot.transform, "Pipe_Condenser_to_Distillation",
            new Vector3(25f, -5f, -18.97f),
            new Vector3(36f, -3f, -19.15f),
            -5.5f,
            condensatePipeMaterial);

        Debug.Log("PlantPipeManager: All pipes generated. Adjust positions visually in Inspector.");
    }

    /// <summary>
    /// Creates a straight cylinder pipe between two world positions.
    /// </summary>
    void CreatePipe(Transform parent, string pipeName, Vector3 start, Vector3 end, Material mat)
    {
        GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipe.name = pipeName;
        pipe.transform.SetParent(parent);

        // Remove collider — not needed for visual pipes
        DestroyImmediate(pipe.GetComponent<CapsuleCollider>());

        // Position at midpoint
        pipe.transform.position = (start + end) / 2f;

        // Point toward end
        pipe.transform.up = (end - start).normalized;

        // Scale: radius on X/Z, length on Y
        float length = Vector3.Distance(start, end);
        pipe.transform.localScale = new Vector3(pipeRadius * 2f, length / 2f, pipeRadius * 2f);

        // Apply material
        if (mat != null)
            pipe.GetComponent<Renderer>().material = mat;
        else
            pipe.GetComponent<Renderer>().material.color = Color.gray;
    }

    /// <summary>
    /// Creates an L-shaped pipe with a horizontal segment and a vertical elbow.
    /// elbowY = Y position of the horizontal run.
    /// </summary>
    void CreatePipeWithElbow(Transform parent, string pipeName,
        Vector3 start, Vector3 end, float elbowY, Material mat)
    {
        // Vertical drop/rise from start to elbow height
        Vector3 elbowStart = new Vector3(start.x, elbowY, start.z);
        Vector3 elbowEnd   = new Vector3(end.x,   elbowY, end.z);

        // Segment 1: vertical from start down/up to elbow
        if (Mathf.Abs(start.y - elbowY) > 0.1f)
            CreatePipe(parent, pipeName + "_Vertical_A", start, elbowStart, mat);

        // Segment 2: horizontal run
        if (Vector3.Distance(elbowStart, elbowEnd) > 0.1f)
            CreatePipe(parent, pipeName + "_Horizontal", elbowStart, elbowEnd, mat);

        // Segment 3: vertical from elbow up/down to end
        if (Mathf.Abs(elbowY - end.y) > 0.1f)
            CreatePipe(parent, pipeName + "_Vertical_B", elbowEnd, end, mat);
    }
}
