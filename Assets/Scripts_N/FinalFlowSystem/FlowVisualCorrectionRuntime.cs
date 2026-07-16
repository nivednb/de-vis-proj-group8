using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FlowVisualCorrectionRuntime : MonoBehaviour
{
    const string RuntimeName = "Flow Visual Correction Runtime";

    readonly Dictionary<PlantFlowKind, Color> colors = new Dictionary<PlantFlowKind, Color>
    {
        { PlantFlowKind.Hydrogen, new Color(0.20f, 1.00f, 0.25f, 1f) },
        { PlantFlowKind.CO2, new Color(0.70f, 0.92f, 1.00f, 1f) },
        { PlantFlowKind.RichAmine, new Color(0.00f, 0.85f, 0.72f, 1f) },
        { PlantFlowKind.LeanAmine, new Color(0.00f, 1.00f, 0.55f, 1f) },
        { PlantFlowKind.RecycleGas, new Color(0.78f, 0.34f, 1.00f, 1f) },
        { PlantFlowKind.MixedFeed, new Color(0.92f, 1.00f, 0.20f, 1f) },
        { PlantFlowKind.SyngasCold, new Color(1.00f, 0.86f, 0.10f, 1f) },
        { PlantFlowKind.SyngasHot, new Color(1.00f, 0.45f, 0.05f, 1f) },
        { PlantFlowKind.ReactorEffluent, new Color(1.00f, 0.18f, 0.06f, 1f) },
        { PlantFlowKind.CrudeMethanol, new Color(0.18f, 0.60f, 1.00f, 1f) },
        { PlantFlowKind.LiquidCrudeMethanol, new Color(0.00f, 0.90f, 1.00f, 1f) },
        { PlantFlowKind.MethanolProduct, new Color(0.72f, 0.30f, 1.00f, 1f) },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindObjectOfType<FlowVisualCorrectionRuntime>() != null) return;
        GameObject runtime = new GameObject(RuntimeName);
        DontDestroyOnLoad(runtime);
        runtime.AddComponent<FlowVisualCorrectionRuntime>();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.4f);
        ApplyCorrections();
        yield return new WaitForSeconds(0.8f);
        ApplyCorrections();
    }

    void ApplyCorrections()
    {
        HideGeneratedCatalystObjects();
        AnimateExistingCatalystBed();
        ImprovePipeVisuals();
    }

    void HideGeneratedCatalystObjects()
    {
        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            string n = obj.name.ToLowerInvariant();
            if (n.Contains("generated") && (n.Contains("catalyst") || n.Contains("bed")))
            {
                obj.SetActive(false);
            }
        }
    }

    void AnimateExistingCatalystBed()
    {
        Transform reactorRoot = FindSceneTransformContaining("reactor base model");
        if (reactorRoot == null) reactorRoot = FindSceneTransformContaining("reactor");
        if (reactorRoot == null) return;

        Renderer catalyst = FindNamedCatalystRenderer(reactorRoot);
        if (catalyst == null) catalyst = FindLikelyInternalCatalystRenderer(reactorRoot);
        if (catalyst == null) return;

        if (catalyst.GetComponent<CatalystBedConditionPulse>() == null)
        {
            catalyst.gameObject.AddComponent<CatalystBedConditionPulse>();
        }
    }

    Renderer FindNamedCatalystRenderer(Transform root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            string objectName = renderer.gameObject.name.ToLowerInvariant();
            string materialName = renderer.sharedMaterial != null ? renderer.sharedMaterial.name.ToLowerInvariant() : "";
            bool catalystName = objectName.Contains("catalyst") || objectName.Contains("bed") || materialName.Contains("catalyst");
            bool wrongPart = objectName.Contains("generated") || objectName.Contains("pipe") || objectName.Contains("nozzle") || objectName.Contains("flange") || objectName.Contains("skirt") || objectName.Contains("shell");
            if (catalystName && !wrongPart) return renderer;
        }
        return null;
    }

    Renderer FindLikelyInternalCatalystRenderer(Transform root)
    {
        Bounds reactorBounds = CalculateBounds(root);
        Renderer best = null;
        float bestScore = float.MaxValue;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            string n = renderer.gameObject.name.ToLowerInvariant();
            if (n.Contains("pipe") || n.Contains("nozzle") || n.Contains("flange") || n.Contains("skirt") || n.Contains("shell") || n.Contains("generated")) continue;
            float score = Vector3.Distance(renderer.bounds.center, reactorBounds.center);
            if (score < bestScore)
            {
                bestScore = score;
                best = renderer;
            }
        }
        return best;
    }

    void ImprovePipeVisuals()
    {
        Transform piping = FindSceneTransformContaining("piping");
        if (piping == null) return;

        foreach (Renderer renderer in piping.GetComponentsInChildren<Renderer>(true))
        {
            PlantFlowKind kind;
            if (!TryClassifyFlow(renderer.gameObject.name, out kind)) continue;
            Color color = colors.ContainsKey(kind) ? colors[kind] : Color.cyan;
            MakePipeSurfaceSmooth(renderer, color);
            EnsureMovingFlowBeads(renderer, color);
        }
    }

    void MakePipeSurfaceSmooth(Renderer renderer, Color flowColor)
    {
        Material material = renderer.material;
        Color surfaceColor = Color.Lerp(Color.white, flowColor, 0.22f);
        surfaceColor.a = 0.22f;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", surfaceColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", surfaceColor);
        if (material.HasProperty("_FlowIntensity")) material.SetFloat("_FlowIntensity", 0.18f);
        if (material.HasProperty("_FlowDensity")) material.SetFloat("_FlowDensity", 0.35f);
        if (material.HasProperty("_PipeAlpha")) material.SetFloat("_PipeAlpha", 0.22f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.82f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.02f);
        material.renderQueue = 3000;
    }

    void EnsureMovingFlowBeads(Renderer pipeRenderer, Color color)
    {
        if (pipeRenderer.transform.Find("Visible_Process_Flow") != null) return;
        GameObject root = new GameObject("Visible_Process_Flow");
        root.transform.SetParent(pipeRenderer.transform, false);

        Bounds bounds = pipeRenderer.bounds;
        Vector3 a;
        Vector3 b;
        GetApproximateAxis(bounds, out a, out b);

        LineRenderer line = root.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.startWidth = Mathf.Clamp(bounds.size.magnitude * 0.018f, 0.035f, 0.16f);
        line.endWidth = line.startWidth;
        line.numCapVertices = 6;
        line.startColor = color;
        line.endColor = color;
        line.material = FlowVisualCorrectionRuntimeMaterialCache.Get(color);

        FlowBeadPulse pulse = root.AddComponent<FlowBeadPulse>();
        pulse.Configure(a, b, color, Mathf.Clamp(bounds.size.magnitude * 0.18f, 0.75f, 2.4f));
    }

    static void GetApproximateAxis(Bounds b, out Vector3 a, out Vector3 c)
    {
        Vector3 axis = Vector3.right;
        if (b.size.y > b.size.x && b.size.y > b.size.z) axis = Vector3.up;
        else if (b.size.z > b.size.x && b.size.z > b.size.y) axis = Vector3.forward;

        float length = Mathf.Max(Vector3.Dot(b.size, Abs(axis)), 0.2f) * 0.45f;
        a = b.center - axis * length;
        c = b.center + axis * length;
    }

    static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

    static Transform FindSceneTransformContaining(string text)
    {
        string lower = text.ToLowerInvariant();
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.gameObject.scene.IsValid() && transform.name.ToLowerInvariant().Contains(lower)) return transform;
        }
        return null;
    }

    static Bounds CalculateBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static bool TryClassifyFlow(string objectName, out PlantFlowKind kind)
    {
        string n = objectName.ToLowerInvariant();
        if (n.StartsWith("h2storage") || n.StartsWith("h2_") || n.StartsWith("h2pipe") || n.StartsWith("h2_pipe")) { kind = PlantFlowKind.Hydrogen; return true; }
        if (n.StartsWith("co2")) { kind = PlantFlowKind.CO2; return true; }
        if (n.StartsWith("richamine")) { kind = PlantFlowKind.RichAmine; return true; }
        if (n.StartsWith("leanamine")) { kind = PlantFlowKind.LeanAmine; return true; }
        if (n.StartsWith("recyclegas")) { kind = PlantFlowKind.RecycleGas; return true; }
        if (n.StartsWith("mixedfeed")) { kind = PlantFlowKind.MixedFeed; return true; }
        if (n.StartsWith("syngas_pipe_6") || n.StartsWith("syngas_pipe_7") || n.StartsWith("syngas_pipe_8")) { kind = PlantFlowKind.SyngasHot; return true; }
        if (n.StartsWith("syngas")) { kind = PlantFlowKind.SyngasCold; return true; }
        if (n.StartsWith("reactoreffluent")) { kind = PlantFlowKind.ReactorEffluent; return true; }
        if (n.StartsWith("crudemeoh")) { kind = PlantFlowKind.CrudeMethanol; return true; }
        if (n.StartsWith("liq_crudemeoh")) { kind = PlantFlowKind.LiquidCrudeMethanol; return true; }
        if (n.StartsWith("methanolproduct")) { kind = PlantFlowKind.MethanolProduct; return true; }
        kind = PlantFlowKind.MixedFeed;
        return false;
    }
}

public sealed class CatalystBedConditionPulse : MonoBehaviour
{
    Renderer targetRenderer;
    Material materialInstance;

    void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            materialInstance = targetRenderer.material;
            if (materialInstance.HasProperty("_EmissionColor")) materialInstance.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        if (materialInstance == null) return;
        float pulse = (Mathf.Sin(Time.time * 1.8f) + 1f) * 0.5f;
        Color normal = new Color(0.05f, 0.75f, 0.38f, 1f);
        Color warning = new Color(1.00f, 0.48f, 0.02f, 1f);
        Color color = Color.Lerp(normal, warning, pulse);
        if (materialInstance.HasProperty("_BaseColor")) materialInstance.SetColor("_BaseColor", color);
        if (materialInstance.HasProperty("_Color")) materialInstance.SetColor("_Color", color);
        if (materialInstance.HasProperty("_EmissionColor")) materialInstance.SetColor("_EmissionColor", color * 0.35f);
    }
}

public sealed class FlowBeadPulse : MonoBehaviour
{
    readonly List<Transform> beads = new List<Transform>();
    Vector3 start;
    Vector3 end;
    float speed = 1f;

    public void Configure(Vector3 a, Vector3 b, Color color, float configuredSpeed)
    {
        start = a;
        end = b;
        speed = configuredSpeed;
        Material material = FlowVisualCorrectionRuntimeMaterialCache.Get(color);
        for (int i = 0; i < 5; i++)
        {
            GameObject bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bead.name = "Moving_Process_Flow_Bead";
            bead.transform.SetParent(transform, true);
            bead.transform.localScale = Vector3.one * 0.075f;
            Renderer renderer = bead.GetComponent<Renderer>();
            if (renderer != null) renderer.material = material;
            Collider collider = bead.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            beads.Add(bead.transform);
        }
    }

    void Update()
    {
        for (int i = 0; i < beads.Count; i++)
        {
            float t = Mathf.Repeat(Time.time * speed + i / (float)beads.Count, 1f);
            beads[i].position = Vector3.Lerp(start, end, t);
        }
    }
}

static class FlowVisualCorrectionRuntimeMaterialCache
{
    static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

    public static Material Get(Color color)
    {
        Material material;
        if (cache.TryGetValue(color, out material)) return material;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        material = new Material(shader);
        material.name = "Moving Process Flow Bead";
        material.color = color;
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        cache[color] = material;
        return material;
    }
}
