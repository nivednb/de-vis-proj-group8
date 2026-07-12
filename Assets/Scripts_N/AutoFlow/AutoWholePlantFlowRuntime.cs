using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Drop this file anywhere under Assets. It auto-runs on Play.
/// Creates small moving flow dots along named pipe sections and makes pipes translucent.
/// Designed for the whole_plant_chaitanya scene.
/// </summary>
public class AutoWholePlantFlowRuntime : MonoBehaviour
{
    [Header("Global Flow Settings")]
    public float globalSpeed = 1.0f;
    public float particleSize = 0.16f;
    public float lineWidth = 0.075f;
    public float pipeAlpha = 0.28f;
    public bool makePipesTransparent = true;
    public bool showThinGuideLines = true;
    public bool scaleFlowWithProcessQuantities = true;

    private readonly List<RouteRuntime> routes = new List<RouteRuntime>();
    private readonly Dictionary<string, RouteRuntime> routeMap = new Dictionary<string, RouteRuntime>();
    private readonly Dictionary<string, Transform> nameMap = new Dictionary<string, Transform>();
    private readonly List<Material> runtimeMaterials = new List<Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<AutoWholePlantFlowRuntime>() != null) return;
        GameObject go = new GameObject("Generated Whole Plant Flow");
        go.AddComponent<AutoWholePlantFlowRuntime>();
    }

    private void Start()
    {
        BuildNameMap();
        if (makePipesTransparent) MakeNamedPipesTransparent();
        BuildWholePlantFlow();
        PositionCameraForDemo();
        Debug.Log($"AutoWholePlantFlowRuntime: built {routes.Count} named pipe flow routes.");
    }

    private void Update()
    {
        if (scaleFlowWithProcessQuantities && PlantProcessSimulator.Instance != null)
        {
            ApplyProcessQuantities(PlantProcessSimulator.Instance.Current);
        }

        float dt = Time.deltaTime * globalSpeed;
        foreach (var r in routes) r.Update(dt);
    }

    private void ApplyProcessQuantities(PlantProcessSimulator.ProcessSnapshot snapshot)
    {
        float h2 = Mathf.InverseLerp(0f, 215f, snapshot.h2InputKgH);
        float captured = Mathf.InverseLerp(0f, 1510f, snapshot.co2CapturedKgH);
        float syngas = Mathf.InverseLerp(0f, 1725f, snapshot.syngasFeedKgH);
        float methanol = Mathf.InverseLerp(0f, 1250f, snapshot.methanolProductionKgH);
        float recycle = Mathf.InverseLerp(0f, 450f, snapshot.recycleGasKgH);

        SetRouteIntensity("H2: electrolyzer to tank", h2);
        SetRouteIntensity("Rich amine: absorber to desorber", captured);
        SetRouteIntensity("Lean amine: desorber to absorber", captured);
        SetRouteIntensity("CO2 tank to compressor", captured);
        SetRouteIntensity("Flash recycle to compressor", recycle);
        SetRouteIntensity("Syngas: compressor to reactor feed", syngas);
        SetRouteIntensity("Hot product: reactor to condenser", methanol);
        SetRouteIntensity("Crude methanol: condenser to flash separator", methanol);
        SetRouteIntensity("Crude methanol: flash separator to distillation", methanol);
        SetRouteIntensity("Methanol product: distillation to tank", methanol);
        SetRouteIntensity("Purge / inerts", recycle * 0.45f);
    }

    private void SetRouteIntensity(string routeName, float intensity)
    {
        if (routeMap.TryGetValue(routeName, out RouteRuntime route))
        {
            route.SetIntensity(intensity);
        }
    }

    private void BuildNameMap()
    {
        nameMap.Clear();
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
        {
            if (!nameMap.ContainsKey(t.name)) nameMap.Add(t.name, t);
        }
    }

    private Transform FindObj(string objectName)
    {
        if (nameMap.TryGetValue(objectName, out Transform t)) return t;
        Debug.LogWarning("AutoFlow: missing object named: " + objectName);
        return null;
    }

    private void BuildWholePlantFlow()
    {
        // Colors follow your process diagram approximately.
        // Process material colors:
        // H2 = green, amine = teal, CO2 = pale grey, syngas = yellow,
        // hot reactor product = orange, methanol liquid = purple, recycle gas = violet.
        Color h2 = new Color(0.1f, 1.0f, 0.15f, 1f);
        Color richAmine = new Color(0.0f, 0.9f, 0.78f, 1f);
        Color leanAmine = new Color(0.0f, 1.0f, 0.42f, 1f);
        Color co2 = new Color(0.86f, 0.92f, 1.0f, 1f);
        Color syngas = new Color(1.0f, 0.82f, 0.08f, 1f);
        Color hotProduct = new Color(1.0f, 0.45f, 0.05f, 1f);
        Color methanol = new Color(0.72f, 0.18f, 1.0f, 1f);
        Color crudeMethanol = new Color(0.35f, 0.72f, 1.0f, 1f);
        Color recycle = new Color(0.58f, 0.28f, 1.0f, 1f);
        Color purge = new Color(1.0f, 0.2f, 0.95f, 1f);

        AddRoute("H2: electrolyzer to tank", h2, 28, 1.5f, new [] {
            "Cylinder", "pipe bend (2)", "Cylinder (20)", "pipe bend (3)", "Cylinder (19)", "pipe bend (4)", "Cylinder (30)", "pipe bend (21)"
        });

        AddRoute("Rich amine: absorber to desorber", richAmine, 26, 1.25f, new [] {
            "pipe bend (4)", "Cylinder (2)", "pipe bend (5)", "Cylinder (3)", "pipe bend (13)"
        });

        AddRoute("Lean amine: desorber to absorber", leanAmine, 26, 1.15f, new [] {
            "pipe bend (16)", "Cylinder (5)", "pipe bend (19)", "Cylinder (18)", "pipe bend (15)", "Cylinder (28)", "pipe bend (14)"
        });

        AddRoute("CO2 tank to compressor", co2, 32, 1.35f, new [] {
            "Cylinder (23)", "pipe bend (20)", "Cylinder (22)", "pipe bend (18)", "t junction", "Cylinder (24)", "pipe bend (17)", "Cylinder (4)", "pipe bend (1)", "Cylinder (26)"
        });

        AddRoute("Flash recycle to compressor", recycle, 34, 1.35f, new [] {
            "pipe bend (22)", "Cylinder (25)", "pipe bend (23)", "Cylinder (27)", "pipe bend (24)", "Cylinder (21)", "t junction", "Cylinder (17)", "pipe bend (28)", "Cylinder (8)", "pipe bend (12)", "Cylinder (16)"
        });

        AddRoute("Syngas: compressor to reactor feed", syngas, 36, 1.45f, new [] {
            "Cylinder (7)", "pipe bend (26)", "Cylinder (29)", "pipe bend (25)", "Cylinder (9)", "Cylinder (31)", "pipe bend (27)", "Cylinder (32)"
        });

        AddRoute("Hot product: reactor to condenser", hotProduct, 28, 1.3f, new [] {
            "pipe bend (8)", "Cylinder (33)", "pipe bend (7)", "Cylinder (6)", "pipe bend (6)", "Cylinder (10)"
        });

        AddRoute("Crude methanol: condenser to flash separator", crudeMethanol, 28, 1.25f, new [] {
            "Cylinder (11)", "pipe bend (9)", "Cylinder (12)", "pipe bend (10)", "Cylinder (13)"
        });

        AddRoute("Crude methanol: flash separator to distillation", crudeMethanol, 14, 0.95f, new [] {
            "Cylinder (15)"
        });

        AddRoute("Methanol product: distillation to tank", methanol, 24, 1.1f, new [] {
            "Cylinder (35)", "pipe bend (29)", "Cylinder (34)"
        });

        // Small visible purge marker if those objects exist later. Safe if missing.
        AddRoute("Purge / inerts", purge, 10, 0.7f, new [] { "purge", "vent", "Cube (5)" });
    }

    private void AddRoute(string routeName, Color color, int particleCount, float speed, string[] objectNames)
    {
        List<Vector3> points = new List<Vector3>();

        foreach (string n in objectNames)
        {
            Transform t = FindObj(n);
            if (t == null) continue;

            Vector3 p = GetObjectCenter(t);

            // avoid identical repeated points
            if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], p) > 0.02f)
                points.Add(p);
        }

        if (points.Count < 2)
        {
            Debug.LogWarning("AutoFlow: route skipped, not enough points: " + routeName);
            return;
        }

        // Create a small vertical offset so dots appear slightly inside/above pipe center and are visible.
        // Too much offset makes it look detached; keep very small.
        for (int i = 0; i < points.Count; i++) points[i] += Vector3.up * 0.03f;

        GameObject parent = new GameObject("Flow_" + routeName);
        parent.transform.SetParent(transform);

        Material mat = CreateUnlitMaterial("FlowMat_" + routeName, color);
        int amplifiedParticleCount = Mathf.CeilToInt(particleCount * 1.35f);
        RouteRuntime runtime = new RouteRuntime(parent.transform, points, mat, amplifiedParticleCount, speed, particleSize);
        routes.Add(runtime);
        routeMap[routeName] = runtime;

        if (showThinGuideLines) CreateGuideLine(parent.transform, routeName, points, color);
    }

    private Vector3 GetObjectCenter(Transform t)
    {
        Renderer r = t.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds.center;
        return t.position;
    }

    private Material CreateUnlitMaterial(string name, Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        Material m = new Material(shader);
        m.name = name;
        SetMaterialColor(m, c);
        runtimeMaterials.Add(m);
        return m;
    }

    private void SetMaterialColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 1.65f);
        }
    }

    private void CreateGuideLine(Transform parent, string routeName, List<Vector3> points, Color color)
    {
        GameObject go = new GameObject("ThinGuide_" + routeName);
        go.transform.SetParent(parent);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
        lr.widthMultiplier = lineWidth;
        lr.useWorldSpace = true;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;

        Color guideColor = color;
        guideColor.a = 0.82f;
        Material mat = CreateUnlitMaterial("GuideMat_" + routeName, guideColor);
        lr.material = mat;
        lr.startColor = guideColor;
        lr.endColor = guideColor;
    }

    private void MakeNamedPipesTransparent()
    {
        HashSet<string> pipeNames = new HashSet<string>();
        string[][] allRoutes = new []
        {
            new [] { "Cylinder", "pipe bend (2)", "Cylinder (20)", "pipe bend (3)", "Cylinder (19)", "pipe bend (4)", "Cylinder (30)", "pipe bend (21)" },
            new [] { "pipe bend (4)", "Cylinder (2)", "pipe bend (5)", "Cylinder (3)", "pipe bend (13)" },
            new [] { "pipe bend (16)", "Cylinder (5)", "pipe bend (19)", "Cylinder (18)", "pipe bend (15)", "Cylinder (28)", "pipe bend (14)" },
            new [] { "Cylinder (23)", "pipe bend (20)", "Cylinder (22)", "pipe bend (18)", "t junction", "Cylinder (24)", "pipe bend (17)", "Cylinder (4)", "pipe bend (1)", "Cylinder (26)" },
            new [] { "pipe bend (22)", "Cylinder (25)", "pipe bend (23)", "Cylinder (27)", "pipe bend (24)", "Cylinder (21)", "t junction", "Cylinder (17)", "pipe bend (28)", "Cylinder (8)", "pipe bend (12)", "Cylinder (16)" },
            new [] { "Cylinder (7)", "pipe bend (26)", "Cylinder (29)", "pipe bend (25)", "Cylinder (9)", "Cylinder (31)", "pipe bend (27)", "Cylinder (32)" },
            new [] { "pipe bend (8)", "Cylinder (33)", "pipe bend (7)", "Cylinder (6)", "pipe bend (6)", "Cylinder (10)" },
            new [] { "Cylinder (11)", "pipe bend (9)", "Cylinder (12)", "pipe bend (10)", "Cylinder (13)" },
            new [] { "Cylinder (15)" },
            new [] { "Cylinder (35)", "pipe bend (29)", "Cylinder (34)" }
        };

        foreach (var route in allRoutes)
            foreach (string n in route)
                pipeNames.Add(n);

        foreach (string n in pipeNames)
        {
            Transform t = FindObj(n);
            if (t == null) continue;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                MakeRendererTransparentGrey(r);
        }
    }

    private void MakeRendererTransparentGrey(Renderer r)
    {
        Material src = r.sharedMaterial;
        if (src == null) return;

        Material m = new Material(src);
        m.name = src.name + "_AutoTransparent";

        Color c = Color.white;
        if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
        else if (m.HasProperty("_Color")) c = m.GetColor("_Color");

        // keep pipes neutral grey, not magenta/red/green.
        c = Color.Lerp(c, new Color(0.75f, 0.85f, 0.9f, 1f), 0.55f);
        c.a = pipeAlpha;

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);

        // URP transparent setup
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        r.material = m;
        runtimeMaterials.Add(m);
    }

    private void PositionCameraForDemo()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null) return;

        // Front overview, orthographic, not back view.
        cam.transform.position = new Vector3(10f, 27f, 41f);
        cam.transform.rotation = Quaternion.Euler(30f, 180f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = 40f;
    }

    private class RouteRuntime
    {
        private readonly Transform parent;
        private readonly List<Vector3> points;
        private readonly List<Transform> particles = new List<Transform>();
        private readonly float[] distances;
        private readonly float[] segmentLengths;
        private readonly float totalLength;
        private readonly float speed;
        private readonly float baseParticleSize;
        private float intensity = 1f;

        public RouteRuntime(Transform parent, List<Vector3> points, Material mat, int particleCount, float speed, float particleSize)
        {
            this.parent = parent;
            this.points = points;
            this.speed = speed;
            this.baseParticleSize = particleSize;

            segmentLengths = new float[points.Count - 1];
            float len = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                segmentLengths[i] = Vector3.Distance(points[i], points[i + 1]);
                len += segmentLengths[i];
            }
            totalLength = Mathf.Max(len, 0.01f);
            distances = new float[particleCount];

            for (int i = 0; i < particleCount; i++)
            {
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "flow_dot_" + i.ToString("00");
                dot.transform.SetParent(parent);
                dot.transform.localScale = Vector3.one * particleSize;
                dot.GetComponent<Renderer>().material = mat;
                Collider col = dot.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                distances[i] = (totalLength / particleCount) * i;
                dot.transform.position = GetPositionAtDistance(distances[i]);
                particles.Add(dot.transform);
            }
        }

        public void Update(float dt)
        {
            float effectiveSpeed = Mathf.Lerp(0.15f, 1.75f, intensity) * speed;
            for (int i = 0; i < particles.Count; i++)
            {
                distances[i] += effectiveSpeed * dt;
                while (distances[i] > totalLength) distances[i] -= totalLength;
                particles[i].position = GetPositionAtDistance(distances[i]);
            }
        }

        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp01(value);
            float visibleFraction = Mathf.Lerp(0.42f, 1f, intensity);
            int visibleCount = Mathf.Max(1, Mathf.RoundToInt(particles.Count * visibleFraction));
            float scale = Mathf.Lerp(baseParticleSize * 0.9f, baseParticleSize * 2.15f, intensity);

            for (int i = 0; i < particles.Count; i++)
            {
                bool active = i < visibleCount;
                if (particles[i].gameObject.activeSelf != active)
                {
                    particles[i].gameObject.SetActive(active);
                }

                particles[i].localScale = Vector3.one * scale;
            }
        }

        private Vector3 GetPositionAtDistance(float d)
        {
            float remaining = d;
            for (int i = 0; i < segmentLengths.Length; i++)
            {
                if (remaining <= segmentLengths[i])
                {
                    float t = segmentLengths[i] <= 0.0001f ? 0f : remaining / segmentLengths[i];
                    return Vector3.Lerp(points[i], points[i + 1], t);
                }
                remaining -= segmentLengths[i];
            }
            return points[points.Count - 1];
        }
    }
}
