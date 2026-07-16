using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FlowVisualCorrectionRuntime : MonoBehaviour
{
    const string RuntimeName = "Flow Visual Correction Runtime";
    const string RouteRootName = "Waypoint Process Flow Routes";

    readonly Dictionary<PlantFlowKind, Color> colors = new Dictionary<PlantFlowKind, Color>
    {
        { PlantFlowKind.Hydrogen, new Color(0.20f, 1.00f, 0.25f, 1f) },
        { PlantFlowKind.HydrogenFromStorage, new Color(0.22f, 1.00f, 0.36f, 1f) },
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
        if (FindObjectOfType<FlowVisualCorrectionRuntime>() != null)
        {
            return;
        }

        GameObject runtime = new GameObject(RuntimeName);
        DontDestroyOnLoad(runtime);
        runtime.AddComponent<FlowVisualCorrectionRuntime>();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.35f);
        ApplyCorrections();
        yield return new WaitForSeconds(0.85f);
        ApplyCorrections();
    }

    void ApplyCorrections()
    {
        HideOldGeneratedCatalystObjects();
        AnimateExistingCatalystBed();
        RemoveLegacyWaypointFlowRoutes();
    }

    void HideOldGeneratedCatalystObjects()
    {
        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            string n = obj.name.ToLowerInvariant();
            bool oldGeneratedCatalyst = n.Contains("generated") && (n.Contains("catalyst") || n.Contains("bed"));
            bool activeRuntimeVisual = n.Contains("runtime reactor") || n.Contains("reactor input") || n.Contains("reactor output") || n.Contains("reactor conversion") || n.Contains("reactor exothermic");

            if (oldGeneratedCatalyst && !activeRuntimeVisual)
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

        // ReactorInternalFlowRuntime already owns the catalyst material and pulse.
        // A second controller would overwrite its transparency every frame.
        if (reactorRoot.GetComponent<ReactorInternalFlowRuntime>() != null) return;

        Renderer catalyst = FindNamedCatalystRenderer(reactorRoot);

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

    void RemoveLegacyWaypointFlowRoutes()
    {
        Transform piping = FindSceneTransformContaining("piping");
        if (piping == null) return;

        Transform oldRoot = piping.Find(RouteRootName);
        if (oldRoot != null)
        {
            Destroy(oldRoot.gameObject);
        }
    }

    void ImprovePipeSurfaces()
    {
        Transform piping = FindSceneTransformContaining("piping");
        if (piping == null) return;

        foreach (Renderer renderer in piping.GetComponentsInChildren<Renderer>(true))
        {
            PlantFlowKind kind;
            if (!TryClassifyFlow(renderer.gameObject.name, out kind)) continue;
            Color color = GetColor(kind);
            MakePipeSurfaceSmooth(renderer, color);
            RemoveOldPerPipeFlow(renderer.transform);
        }
    }

    void MakePipeSurfaceSmooth(Renderer renderer, Color flowColor)
    {
        Material material = renderer.material;
        Color surfaceColor = Color.Lerp(Color.white, flowColor, 0.20f);
        surfaceColor.a = 0.24f;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", surfaceColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", surfaceColor);
        if (material.HasProperty("_FlowIntensity")) material.SetFloat("_FlowIntensity", 0.10f);
        if (material.HasProperty("_FlowDensity")) material.SetFloat("_FlowDensity", 0.18f);
        if (material.HasProperty("_PipeAlpha")) material.SetFloat("_PipeAlpha", 0.24f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.84f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.01f);
        material.renderQueue = 3000;
    }

    void RemoveOldPerPipeFlow(Transform pipe)
    {
        Transform old = pipe.Find("Visible_Process_Flow");
        if (old != null)
        {
            Destroy(old.gameObject);
        }
    }

    void BuildWaypointFlowRoutes()
    {
        Transform piping = FindSceneTransformContaining("piping");
        if (piping == null) return;

        Transform oldRoot = piping.Find(RouteRootName);
        if (oldRoot != null)
        {
            Destroy(oldRoot.gameObject);
        }

        GameObject routeRoot = new GameObject(RouteRootName);
        routeRoot.transform.SetParent(piping, false);

        List<Renderer> pipeRenderers = new List<Renderer>();
        foreach (Renderer renderer in piping.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.transform.IsChildOf(routeRoot.transform)) continue;
            PlantFlowKind ignored;
            if (TryClassifyFlow(renderer.gameObject.name, out ignored))
            {
                pipeRenderers.Add(renderer);
            }
        }

        CreateRoute(routeRoot.transform, "H2 Electrolyzer to Storage", pipeRenderers, PlantFlowKind.Hydrogen, new[] { "H2_pipe_" }, false, 2.25f, 11, 0.11f);
        CreateRoute(routeRoot.transform, "H2 Storage to Feed Junction", pipeRenderers, PlantFlowKind.HydrogenFromStorage, new[] { "H2Storage_pipe_" }, false, 2.20f, 10, 0.11f);
        CreateRoute(routeRoot.transform, "CO2 Feed and Compression", pipeRenderers, PlantFlowKind.CO2, new[] { "CO2_pipe_" }, false, 1.85f, 9, 0.10f);
        CreateRoute(routeRoot.transform, "Rich Amine Absorber to Regenerator", pipeRenderers, PlantFlowKind.RichAmine, new[] { "RichAmine_pipe_" }, false, 1.60f, 8, 0.095f);
        CreateRoute(routeRoot.transform, "Lean Amine Return", pipeRenderers, PlantFlowKind.LeanAmine, new[] { "LeanAmine_pipe_" }, true, 1.60f, 8, 0.095f);
        CreateRoute(routeRoot.transform, "Recycle Gas Return", pipeRenderers, PlantFlowKind.RecycleGas, new[] { "RecycleGas_pipe_" }, true, 2.05f, 9, 0.10f);
        CreateRoute(routeRoot.transform, "Mixed Feed", pipeRenderers, PlantFlowKind.MixedFeed, new[] { "MixedFeed_pipe_" }, false, 2.15f, 9, 0.105f);
        CreateRoute(routeRoot.transform, "Cold Syngas to Heat Exchanger", pipeRenderers, PlantFlowKind.SyngasCold, new[] { "Syngas_pipe_1", "Syngas_pipe_2", "Syngas_pipe_3", "Syngas_pipe_4", "Syngas_pipe_5" }, false, 2.30f, 11, 0.11f);
        CreateRoute(routeRoot.transform, "Heated Syngas to Reactor", pipeRenderers, PlantFlowKind.SyngasHot, new[] { "Syngas_pipe_6", "Syngas_pipe_7", "Syngas_pipe_8" }, false, 2.35f, 8, 0.12f);
        CreateRoute(routeRoot.transform, "Reactor Effluent to Condenser", pipeRenderers, PlantFlowKind.ReactorEffluent, new[] { "ReactorEffluent_pipe_" }, false, 2.10f, 9, 0.11f);
        CreateRoute(routeRoot.transform, "Crude Methanol to Flash Separator", pipeRenderers, PlantFlowKind.CrudeMethanol, new[] { "CrudeMeOH_pipe_" }, false, 1.85f, 8, 0.105f);
        CreateRoute(routeRoot.transform, "Liquid Crude Methanol to Distillation", pipeRenderers, PlantFlowKind.LiquidCrudeMethanol, new[] { "Liq_CrudeMeOH_pipe_" }, false, 1.65f, 7, 0.10f);
        CreateRoute(routeRoot.transform, "Methanol Product to Storage", pipeRenderers, PlantFlowKind.MethanolProduct, new[] { "MethanolProduct_pipe_" }, false, 1.55f, 7, 0.105f);
    }

    void CreateRoute(Transform root, string routeName, List<Renderer> allPipes, PlantFlowKind kind, string[] prefixes, bool reverse, float speed, int beadCount, float beadSize)
    {
        List<Renderer> route = new List<Renderer>();
        foreach (Renderer renderer in allPipes)
        {
            if (NameMatchesAnyPrefix(renderer.gameObject.name, prefixes))
            {
                route.Add(renderer);
            }
        }

        route.Sort((a, b) => ExtractRouteIndex(a.gameObject.name).CompareTo(ExtractRouteIndex(b.gameObject.name)));
        if (reverse)
        {
            route.Reverse();
        }

        List<Vector3> points = BuildWaypointPoints(route);
        if (points.Count < 2)
        {
            return;
        }

        Color color = GetColor(kind);
        GameObject routeObject = new GameObject("Waypoint_Flow_" + SanitizeName(routeName));
        routeObject.transform.SetParent(root, true);

        LineRenderer line = routeObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        line.startWidth = beadSize * 0.38f;
        line.endWidth = beadSize * 0.38f;
        line.numCapVertices = 6;
        line.numCornerVertices = 6;
        Color lineColor = color;
        lineColor.a = 0.42f;
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.material = FlowVisualCorrectionRuntimeMaterialCache.Get(lineColor);

        WaypointFlowPulse pulse = routeObject.AddComponent<WaypointFlowPulse>();
        pulse.Configure(points.ToArray(), color, speed, beadCount, beadSize);
    }

    static List<Vector3> BuildWaypointPoints(List<Renderer> route)
    {
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < route.Count; i++)
        {
            Renderer renderer = route[i];
            Bounds b = renderer.bounds;
            Vector3 center = b.center;

            if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], center) > 0.05f)
            {
                points.Add(center);
            }
        }
        return points;
    }

    static bool NameMatchesAnyPrefix(string objectName, string[] prefixes)
    {
        string lower = objectName.ToLowerInvariant();
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (lower.StartsWith(prefixes[i].ToLowerInvariant()))
            {
                return true;
            }
        }
        return false;
    }

    static int ExtractRouteIndex(string objectName)
    {
        int lastUnderscore = objectName.LastIndexOf('_');
        int start = lastUnderscore >= 0 ? lastUnderscore + 1 : 0;
        int value;
        if (int.TryParse(objectName.Substring(start), out value))
        {
            return value;
        }

        int best = 0;
        int multiplier = 1;
        bool found = false;
        for (int i = objectName.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(objectName[i]))
            {
                best += (objectName[i] - '0') * multiplier;
                multiplier *= 10;
                found = true;
            }
            else if (found)
            {
                break;
            }
        }
        return found ? best : 0;
    }

    Color GetColor(PlantFlowKind kind)
    {
        Color color;
        return colors.TryGetValue(kind, out color) ? color : Color.cyan;
    }

    static string SanitizeName(string name)
    {
        return name.Replace(' ', '_').Replace('/', '_');
    }

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
        if (n.StartsWith("h2storage")) { kind = PlantFlowKind.HydrogenFromStorage; return true; }
        if (n.StartsWith("h2_") || n.StartsWith("h2pipe") || n.StartsWith("h2_pipe")) { kind = PlantFlowKind.Hydrogen; return true; }
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
        Color normal = new Color(0.08f, 0.76f, 0.38f, 1f);
        Color warning = new Color(1.00f, 0.45f, 0.02f, 1f);
        Color color = Color.Lerp(normal, warning, pulse);
        if (materialInstance.HasProperty("_BaseColor")) materialInstance.SetColor("_BaseColor", color);
        if (materialInstance.HasProperty("_Color")) materialInstance.SetColor("_Color", color);
        if (materialInstance.HasProperty("_EmissionColor")) materialInstance.SetColor("_EmissionColor", color * 0.55f);
    }
}

public sealed class WaypointFlowPulse : MonoBehaviour
{
    [Header("UI Adjustable Flow")]
    public bool isFlowing = true;
    [Min(0f)] public float routeSpeed = 1f;
    [Range(1, 64)] public int beadCount = 8;
    [Range(0.03f, 0.35f)] public float beadSize = 0.10f;

    readonly List<Transform> beads = new List<Transform>();
    Vector3[] points = new Vector3[0];
    float[] cumulativeDistances = new float[0];
    float totalDistance = 1f;
    Material beadMaterial;

    public void Configure(Vector3[] configuredPoints, Color color, float configuredSpeed, int configuredBeadCount, float configuredBeadSize)
    {
        points = configuredPoints;
        routeSpeed = configuredSpeed;
        beadCount = Mathf.Clamp(configuredBeadCount, 1, 64);
        beadSize = configuredBeadSize;
        beadMaterial = FlowVisualCorrectionRuntimeMaterialCache.Get(color);
        BuildDistanceTable();
        RebuildBeads();
    }

    public void SetSpeed(float newSpeed)
    {
        routeSpeed = Mathf.Max(0f, newSpeed);
    }

    public void SetDensity(float normalizedDensity)
    {
        beadCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, 40f, Mathf.Clamp01(normalizedDensity))), 1, 64);
        RebuildBeads();
    }

    public void SetFlowEnabled(bool enabled)
    {
        isFlowing = enabled;
        for (int i = 0; i < beads.Count; i++)
        {
            if (beads[i] != null)
            {
                beads[i].gameObject.SetActive(enabled);
            }
        }
    }

    void RebuildBeads()
    {
        for (int i = beads.Count - 1; i >= 0; i--)
        {
            if (beads[i] != null)
            {
                Destroy(beads[i].gameObject);
            }
        }
        beads.Clear();

        int count = Mathf.Clamp(beadCount, 1, 64);
        for (int i = 0; i < count; i++)
        {
            GameObject bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bead.name = "Waypoint_Process_Flow_Bead";
            bead.transform.SetParent(transform, true);
            bead.transform.localScale = Vector3.one * beadSize;
            Renderer renderer = bead.GetComponent<Renderer>();
            if (renderer != null) renderer.material = beadMaterial;
            Collider collider = bead.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            bead.SetActive(isFlowing);
            beads.Add(bead.transform);
        }
    }

    void BuildDistanceTable()
    {
        cumulativeDistances = new float[points.Length];
        totalDistance = 0f;
        for (int i = 1; i < points.Length; i++)
        {
            totalDistance += Vector3.Distance(points[i - 1], points[i]);
            cumulativeDistances[i] = totalDistance;
        }
        totalDistance = Mathf.Max(totalDistance, 0.01f);
    }

    void Update()
    {
        if (!isFlowing || points == null || points.Length < 2)
        {
            return;
        }

        for (int i = 0; i < beads.Count; i++)
        {
            if (beads[i] == null)
            {
                continue;
            }

            float distance = Mathf.Repeat(Time.time * routeSpeed + (totalDistance * i / Mathf.Max(beads.Count, 1)), totalDistance);
            beads[i].position = EvaluateAtDistance(distance);
        }
    }

    Vector3 EvaluateAtDistance(float distance)
    {
        for (int i = 1; i < cumulativeDistances.Length; i++)
        {
            if (distance <= cumulativeDistances[i])
            {
                float previous = cumulativeDistances[i - 1];
                float segmentLength = Mathf.Max(cumulativeDistances[i] - previous, 0.001f);
                float t = (distance - previous) / segmentLength;
                return Vector3.Lerp(points[i - 1], points[i], t);
            }
        }
        return points[points.Length - 1];
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
        material.name = "Waypoint Process Flow Material";
        material.color = color;
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        cache[color] = material;
        return material;
    }
}
