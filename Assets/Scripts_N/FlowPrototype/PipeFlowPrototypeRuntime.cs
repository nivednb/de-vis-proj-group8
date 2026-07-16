using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight standalone prototype for realistic process-flow visualization.
///
/// Usage:
/// 1. Create an empty Unity scene.
/// 2. Add an empty GameObject.
/// 3. Attach this script.
/// 4. Press Play.
///
/// This intentionally does not load the full plant model. It generates a simple
/// pipe-only layout with transparent pipe shells and animated internal flow
/// pulses so the flow style can be polished separately and integrated later.
/// </summary>
[DisallowMultipleComponent]
public class PipeFlowPrototypeRuntime : MonoBehaviour
{
    [Header("Prototype Controls")]
    [Range(0f, 1.5f)] public float globalLoad = 1f;
    [Range(0.15f, 4f)] public float globalSpeed = 1.35f;
    [Range(0.03f, 0.3f)] public float pipeRadius = 0.13f;
    [Range(0.03f, 0.22f)] public float pulseSize = 0.12f;
    [Range(0.12f, 0.8f)] public float pulseSpacing = 0.42f;
    public bool showTransparentPipeShells = true;
    public bool showLegend = true;
    public bool rebuildOnStart = true;

    private readonly List<AnimatedRoute> activeRoutes = new List<AnimatedRoute>();
    private readonly Dictionary<string, Vector3> modulePositions = new Dictionary<string, Vector3>();
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private Transform generatedRoot;
    private Font uiFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateInStandalonePrototypeProject()
    {
        if (!Application.dataPath.Replace("\\", "/").Contains("PipeFlowPrototypeProject"))
        {
            return;
        }

        if (FindFirstObjectByType<PipeFlowPrototypeRuntime>() != null)
        {
            return;
        }

        GameObject prototypeObject = new GameObject("Auto Pipe Flow Prototype Runtime");
        prototypeObject.AddComponent<PipeFlowPrototypeRuntime>();
    }

    private void Start()
    {
        if (rebuildOnStart)
        {
            BuildPrototype();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime * globalSpeed;
        foreach (AnimatedRoute route in activeRoutes)
        {
            route.Update(dt, globalLoad);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            BuildPrototype();
        }

        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus))
        {
            globalLoad = Mathf.Clamp(globalLoad + Time.deltaTime * 0.55f, 0f, 1.5f);
        }

        if (Input.GetKey(KeyCode.Minus))
        {
            globalLoad = Mathf.Clamp(globalLoad - Time.deltaTime * 0.55f, 0f, 1.5f);
        }
    }

    [ContextMenu("Build Pipe Flow Prototype")]
    public void BuildPrototype()
    {
        ClearPrototype();
        CreateModuleLayout();

        generatedRoot = new GameObject("Generated Pipe Flow Prototype").transform;
        generatedRoot.SetParent(transform, false);

        BuildGroundPlane();
        BuildModuleMarkers();
        BuildProcessRoutes();
        BuildCameraAndLighting();

        if (showLegend)
        {
            BuildLegendUi();
        }
    }

    [ContextMenu("Clear Pipe Flow Prototype")]
    public void ClearPrototype()
    {
        activeRoutes.Clear();

        if (generatedRoot != null)
        {
            DestroyObject(generatedRoot.gameObject);
            generatedRoot = null;
        }

        Transform existing = transform.Find("Generated Pipe Flow Prototype");
        if (existing != null)
        {
            DestroyObject(existing.gameObject);
        }

        foreach (Material material in runtimeMaterials)
        {
            if (material != null)
            {
                DestroyObject(material);
            }
        }

        runtimeMaterials.Clear();
    }

    private void CreateModuleLayout()
    {
        modulePositions.Clear();
        modulePositions["Electrolyzer"] = new Vector3(-15f, 0f, -4.0f);
        modulePositions["H2 Tank"] = new Vector3(-9.8f, 0f, -4.0f);
        modulePositions["Absorber"] = new Vector3(-9.5f, 0f, 2.8f);
        modulePositions["Desorber"] = new Vector3(-3.8f, 0f, 2.8f);
        modulePositions["CO2 Tank"] = new Vector3(-1.4f, 0f, -4.4f);
        modulePositions["Compressor"] = new Vector3(3.5f, 0f, -2.4f);
        modulePositions["Reactor"] = new Vector3(9.0f, 0f, 0.9f);
        modulePositions["Condenser"] = new Vector3(14.0f, 0f, 0.9f);
        modulePositions["Flash Separator"] = new Vector3(17.0f, 0f, -2.2f);
        modulePositions["Distillation"] = new Vector3(20.5f, 0f, 1.8f);
        modulePositions["Methanol Tank"] = new Vector3(24.5f, 0f, -3.6f);
    }

    private void BuildGroundPlane()
    {
        Material ground = CreateLitMaterial("Prototype Ground", new Color(0.08f, 0.10f, 0.11f, 1f));
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Prototype Dark Floor";
        floor.transform.SetParent(generatedRoot);
        floor.transform.position = new Vector3(4f, -0.08f, -0.8f);
        floor.transform.localScale = new Vector3(46f, 0.08f, 17f);
        floor.GetComponent<Renderer>().sharedMaterial = ground;
    }

    private void BuildModuleMarkers()
    {
        Material moduleMaterial = CreateLitMaterial("Prototype Equipment Dark", new Color(0.18f, 0.21f, 0.24f, 1f));
        Material accentMaterial = CreateLitMaterial("Prototype Equipment Accent", new Color(0.95f, 0.65f, 0.10f, 1f));

        foreach (KeyValuePair<string, Vector3> module in modulePositions)
        {
            bool tower = module.Key.Contains("Absorber") || module.Key.Contains("Desorber") ||
                         module.Key.Contains("Reactor") || module.Key.Contains("Distillation");

            GameObject marker = tower
                ? GameObject.CreatePrimitive(PrimitiveType.Cylinder)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            marker.name = module.Key + " marker";
            marker.transform.SetParent(generatedRoot);
            marker.transform.position = module.Value + Vector3.up * (tower ? 1.15f : 0.35f);
            marker.transform.localScale = tower ? new Vector3(1.05f, 1.15f, 1.05f) : new Vector3(1.8f, 0.7f, 1.25f);
            marker.GetComponent<Renderer>().sharedMaterial = moduleMaterial;

            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = module.Key + " label plate";
            band.transform.SetParent(generatedRoot);
            band.transform.position = module.Value + new Vector3(0f, tower ? 2.55f : 1.1f, -0.72f);
            band.transform.localScale = new Vector3(2.2f, 0.28f, 0.08f);
            band.GetComponent<Renderer>().sharedMaterial = accentMaterial;

            CreateWorldLabel(module.Key, module.Value + new Vector3(0f, tower ? 3.05f : 1.5f, -0.86f));
        }
    }

    private void BuildProcessRoutes()
    {
        Color h2 = new Color(0.10f, 1.00f, 0.18f, 1f);
        Color richAmine = new Color(0.00f, 0.90f, 0.78f, 1f);
        Color leanAmine = new Color(0.00f, 1.00f, 0.42f, 1f);
        Color co2 = new Color(0.86f, 0.92f, 1.00f, 1f);
        Color recycle = new Color(0.58f, 0.28f, 1.00f, 1f);
        Color syngas = new Color(1.00f, 0.82f, 0.08f, 1f);
        Color hotProduct = new Color(1.00f, 0.45f, 0.05f, 1f);
        Color crudeMethanol = new Color(0.35f, 0.72f, 1.00f, 1f);
        Color methanol = new Color(0.72f, 0.18f, 1.00f, 1f);

        AddRoute("H2 to tank", h2, 1.15f, "Electrolyzer", "H2 Tank", new[]
        {
            "Cylinder", "pipe bend (2)", "Cylinder (20)", "pipe bend (3)", "Cylinder (19)", "pipe bend (4)", "Cylinder (30)", "pipe bend (21)"
        });

        AddRoute("Rich amine to desorber", richAmine, 0.95f, "Absorber", "Desorber", new[]
        {
            "pipe bend (4)", "Cylinder (2)", "pipe bend (5)", "Cylinder (3)", "pipe bend (13)"
        });

        AddRoute("Lean amine return", leanAmine, 0.9f, "Desorber", "Absorber", new[]
        {
            "pipe bend (16)", "Cylinder (5)", "pipe bend (19)", "Cylinder (18)", "pipe bend (15)", "Cylinder (28)", "pipe bend (14)"
        }, 1.25f);

        AddRoute("CO2 to compressor", co2, 1.0f, "CO2 Tank", "Compressor", new[]
        {
            "Cylinder (23)", "pipe bend (20)", "Cylinder (22)", "pipe bend (18)", "t junction", "Cylinder (24)", "pipe bend (17)", "Cylinder (4)", "pipe bend (1)", "Cylinder (26)"
        });

        AddRoute("Recycle to compressor", recycle, 0.65f, "Flash Separator", "Compressor", new[]
        {
            "pipe bend (22)", "Cylinder (25)", "pipe bend (23)", "Cylinder (27)", "pipe bend (24)", "Cylinder (21)", "t junction", "Cylinder (17)", "pipe bend (28)", "Cylinder (8)", "pipe bend (12)", "Cylinder (16)"
        }, 1.15f);

        AddRoute("Syngas to reactor", syngas, 1.15f, "Compressor", "Reactor", new[]
        {
            "Cylinder (7)", "pipe bend (26)", "Cylinder (29)", "pipe bend (25)", "Cylinder (9)", "Cylinder (31)", "pipe bend (27)", "Cylinder (32)"
        });

        AddRoute("Hot product to condenser", hotProduct, 0.9f, "Reactor", "Condenser", new[]
        {
            "pipe bend (8)", "Cylinder (33)", "pipe bend (7)", "Cylinder (6)", "pipe bend (6)", "Cylinder (10)"
        });

        AddRoute("Crude methanol to flash", crudeMethanol, 0.85f, "Condenser", "Flash Separator", new[]
        {
            "Cylinder (11)", "pipe bend (9)", "Cylinder (12)", "pipe bend (10)", "Cylinder (13)"
        });

        AddRoute("Crude methanol to distillation", crudeMethanol, 0.8f, "Flash Separator", "Distillation", new[]
        {
            "Cylinder (15)"
        });

        AddRoute("Methanol product to tank", methanol, 0.8f, "Distillation", "Methanol Tank", new[]
        {
            "Cylinder (35)", "pipe bend (29)", "Cylinder (34)"
        });
    }

    private void AddRoute(string routeName, Color color, float routeLoad, string fromModule, string toModule, string[] sourcePipeSequence, float zOffset = 0f)
    {
        Vector3 start = modulePositions[fromModule] + new Vector3(0f, 1.0f, zOffset);
        Vector3 end = modulePositions[toModule] + new Vector3(0f, 1.0f, zOffset);
        float midX = (start.x + end.x) * 0.5f;

        List<Vector3> points = new List<Vector3>
        {
            start,
            new Vector3(midX, start.y, start.z),
            new Vector3(midX, end.y, end.z),
            end
        };

        Material pipeMaterial = CreateTransparentMaterial(routeName + " pipe shell", new Color(0.82f, 0.90f, 0.95f, showTransparentPipeShells ? 0.22f : 0.04f));
        Material flowMaterial = CreateFlowMaterial(routeName + " flow", color);
        Material coreMaterial = CreateTransparentMaterial(routeName + " soft core", new Color(color.r, color.g, color.b, 0.28f));

        GameObject routeRoot = new GameObject("Route - " + routeName);
        routeRoot.transform.SetParent(generatedRoot);

        for (int i = 0; i < points.Count - 1; i++)
        {
            CreateCylinderBetween(routeName + " transparent pipe", routeRoot.transform, points[i], points[i + 1], pipeRadius, pipeMaterial);
            CreateCylinderBetween(routeName + " inner fluid core", routeRoot.transform, points[i], points[i + 1], pipeRadius * 0.46f, coreMaterial);
        }

        AnimatedRoute route = new AnimatedRoute(routeName, routeRoot.transform, points, flowMaterial, pulseSize, pulseSpacing, routeLoad, sourcePipeSequence);
        activeRoutes.Add(route);
    }

    private void CreateCylinderBetween(string objectName, Transform parent, Vector3 a, Vector3 b, float radius, Material material)
    {
        Vector3 midpoint = (a + b) * 0.5f;
        Vector3 direction = b - a;
        float length = direction.magnitude;

        if (length <= 0.001f)
        {
            return;
        }

        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = objectName;
        cylinder.transform.SetParent(parent);
        cylinder.transform.position = midpoint;
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        cylinder.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        cylinder.GetComponent<Renderer>().sharedMaterial = material;

        Collider collider = cylinder.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyObject(collider);
        }
    }

    private void BuildCameraAndLighting()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Prototype Camera");
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.transform.position = new Vector3(5f, 22f, -23f);
        camera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 15f;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.055f);

        Light existingLight = FindFirstObjectByType<Light>();
        if (existingLight == null)
        {
            GameObject lightObject = new GameObject("Prototype Directional Light");
            existingLight = lightObject.AddComponent<Light>();
        }

        existingLight.type = LightType.Directional;
        existingLight.transform.rotation = Quaternion.Euler(45f, -35f, 20f);
        existingLight.intensity = 1.15f;
    }

    private void BuildLegendUi()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        GameObject canvasObject = new GameObject("Pipe Flow Prototype UI");
        canvasObject.transform.SetParent(generatedRoot);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        GameObject panel = new GameObject("Legend Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(16f, -16f);
        panelRect.sizeDelta = new Vector2(430f, 310f);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.02f, 0.03f, 0.04f, 0.82f);

        CreateUiText(panel.transform, "Pipe Flow Prototype\n+ / - changes plant load   |   R rebuilds", new Vector2(18f, -18f), 16, FontStyle.Bold);

        string legend =
            "H2: green\n" +
            "CO2: pale blue-white\n" +
            "Rich amine: teal\n" +
            "Lean amine: green-teal\n" +
            "Recycle gas: violet\n" +
            "Syngas: yellow\n" +
            "Hot reactor product: orange\n" +
            "Crude methanol: cyan\n" +
            "Final methanol: purple";

        CreateUiText(panel.transform, legend, new Vector2(18f, -76f), 14, FontStyle.Normal);
    }

    private void CreateUiText(Transform parent, string text, Vector2 anchoredPosition, int fontSize, FontStyle style)
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);
        Text uiText = textObject.AddComponent<Text>();
        uiText.font = uiFont;
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.fontStyle = style;
        uiText.color = Color.white;
        uiText.alignment = TextAnchor.UpperLeft;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = uiText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(390f, 230f);
    }

    private void CreateWorldLabel(string text, Vector3 position)
    {
        GameObject labelObject = new GameObject(text + " world label");
        labelObject.transform.SetParent(generatedRoot);
        labelObject.transform.position = position;
        labelObject.transform.rotation = Quaternion.Euler(65f, 0f, 0f);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 54;
        label.characterSize = 0.055f;
        label.color = Color.white;
    }

    private Material CreateLitMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        SetMaterialColor(material, color);
        runtimeMaterials.Add(material);
        return material;
    }

    private Material CreateTransparentMaterial(string materialName, Color color)
    {
        Material material = CreateLitMaterial(materialName, color);
        SetTransparent(material, color);
        return material;
    }

    private Material CreateFlowMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = materialName;
        SetMaterialColor(material, color);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.8f);
        }

        runtimeMaterials.Add(material);
        return material;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private void SetTransparent(Material material, Color color)
    {
        color.a = Mathf.Clamp01(color.a);
        SetMaterialColor(material, color);

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private class AnimatedRoute
    {
        private readonly string routeName;
        private readonly List<Vector3> points;
        private readonly List<Transform> pulses = new List<Transform>();
        private readonly float[] distances;
        private readonly float[] segmentLengths;
        private readonly float totalLength;
        private readonly float basePulseSize;
        private readonly float routeLoad;
        private readonly string[] sourcePipeSequence;

        public AnimatedRoute(string routeName, Transform parent, List<Vector3> points, Material pulseMaterial, float pulseSize, float pulseSpacing, float routeLoad, string[] sourcePipeSequence)
        {
            this.routeName = routeName;
            this.points = points;
            this.basePulseSize = pulseSize;
            this.routeLoad = routeLoad;
            this.sourcePipeSequence = sourcePipeSequence;

            segmentLengths = new float[points.Count - 1];
            float length = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                segmentLengths[i] = Vector3.Distance(points[i], points[i + 1]);
                length += segmentLengths[i];
            }

            totalLength = Mathf.Max(length, 0.01f);
            int pulseCount = Mathf.Clamp(Mathf.CeilToInt(totalLength / Mathf.Max(0.12f, pulseSpacing)), 4, 80);
            distances = new float[pulseCount];

            for (int i = 0; i < pulseCount; i++)
            {
                GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pulse.name = routeName + " moving pulse " + i.ToString("00");
                pulse.transform.SetParent(parent);
                pulse.transform.localScale = Vector3.one * pulseSize;
                pulse.GetComponent<Renderer>().sharedMaterial = pulseMaterial;

                Collider collider = pulse.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                distances[i] = (totalLength / pulseCount) * i;
                pulse.transform.position = GetPositionAtDistance(distances[i]);
                pulses.Add(pulse.transform);
            }
        }

        public void Update(float dt, float globalLoad)
        {
            float intensity = Mathf.Clamp01(globalLoad * routeLoad);
            float speed = Mathf.Lerp(0.35f, 2.75f, intensity);
            float visibleFraction = Mathf.Lerp(0.25f, 1f, intensity);
            int visibleCount = Mathf.Max(1, Mathf.RoundToInt(pulses.Count * visibleFraction));
            float scale = Mathf.Lerp(basePulseSize * 0.65f, basePulseSize * 1.9f, intensity);

            for (int i = 0; i < pulses.Count; i++)
            {
                distances[i] += dt * speed;
                while (distances[i] > totalLength)
                {
                    distances[i] -= totalLength;
                }

                Transform pulse = pulses[i];
                pulse.position = GetPositionAtDistance(distances[i]);
                pulse.localScale = Vector3.one * scale;

                bool shouldShow = i < visibleCount;
                if (pulse.gameObject.activeSelf != shouldShow)
                {
                    pulse.gameObject.SetActive(shouldShow);
                }
            }
        }

        private Vector3 GetPositionAtDistance(float distance)
        {
            float remaining = distance;
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

        public override string ToString()
        {
            return routeName + " (" + string.Join(" -> ", sourcePipeSequence) + ")";
        }
    }
}
