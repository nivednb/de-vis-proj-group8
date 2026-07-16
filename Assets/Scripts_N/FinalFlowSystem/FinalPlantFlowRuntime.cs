using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Final project flow visualization runtime for the reduced plant scene.
///
/// It finds the manually renamed pipe meshes, applies semi-transparent
/// process-specific flow materials, adds dense animated flow bands, configures
/// reactor transparency, and creates an internal reactor flow/catalyst visual.
///
/// Manual adjustment:
/// - Edit route values in this component at runtime, or
/// - Add PipeFlowManualOverride to any pipe for one-off corrections.
/// </summary>
[DisallowMultipleComponent]
public class FinalPlantFlowRuntime : MonoBehaviour
{
    [Header("Automatic Setup")]
    public bool configureOnStart = true;
    public bool includeInactiveObjects = true;
    public bool logDetectedRoutes = true;

    [Header("Global Flow Scaling")]
    [Range(0.25f, 3f)] public float globalSpeedMultiplier = 1.0f;
    [Range(0.25f, 3f)] public float globalDensityMultiplier = 1.0f;
    [Range(0.05f, 1f)] public float globalPipeAlphaMultiplier = 1.0f;
    [Range(0.25f, 2.5f)] public float globalIntensityMultiplier = 1.0f;

    [Header("Reactor Visuals")]
    public bool configureTransparentReactor = true;
    [Range(0.05f, 0.75f)] public float reactorShellAlpha = 0.26f;
    public bool addInternalReactorSimulation = true;
    public bool animateCatalystCondition = true;

    [Header("Routes - editable for manual correction")]
    public List<PlantFlowRouteSettings> routes = new List<PlantFlowRouteSettings>();

    private readonly Dictionary<PlantFlowKind, Material> materialCache = new Dictionary<PlantFlowKind, Material>();

    private static readonly int FlowColorId = Shader.PropertyToID("_FlowColor");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int DensityId = Shader.PropertyToID("_Tiling");
    private static readonly int BaseAlphaId = Shader.PropertyToID("_BaseAlpha");
    private static readonly int FlowIntensityId = Shader.PropertyToID("_FlowIntensity");
    private static readonly int BandSharpnessId = Shader.PropertyToID("_BandSharpness");

    private void Reset()
    {
        routes = BuildDefaultRoutes();
    }

    private void Awake()
    {
        if (routes == null || routes.Count == 0)
        {
            routes = BuildDefaultRoutes();
        }
    }

    private void Start()
    {
        if (configureOnStart)
        {
            ConfigureFinalFlowSimulation();
        }
    }

    [ContextMenu("Configure Final Flow Simulation")]
    public void ConfigureFinalFlowSimulation()
    {
        materialCache.Clear();
        Dictionary<string, GameObject> sceneObjects = BuildSceneObjectLookup();
        int configuredPipes = 0;

        foreach (PlantFlowRouteSettings route in routes)
        {
            if (route == null || !route.enabled)
            {
                continue;
            }

            List<GameObject> pipes = FindRouteObjects(sceneObjects, route);
            if (pipes.Count == 0)
            {
                Debug.LogWarning("Final flow: no pipes detected for route '" + route.routeName + "'.");
                continue;
            }

            foreach (GameObject pipe in pipes)
            {
                ConfigurePipe(pipe, route);
                configuredPipes++;
            }

            if (logDetectedRoutes)
            {
                Debug.Log("Final flow route: " + route.routeName + " configured " + pipes.Count + " pipe meshes.");
            }
        }

        if (configureTransparentReactor)
        {
            ConfigureReactorTransparency(sceneObjects);
        }

        if (addInternalReactorSimulation)
        {
            ConfigureReactorInternalFlow(sceneObjects);
        }

        Debug.Log("Final MSc flow simulation configured " + configuredPipes + " pipes.");
    }

    private void ConfigurePipe(GameObject pipe, PlantFlowRouteSettings route)
    {
        MeshRenderer renderer = pipe.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = pipe.GetComponentInChildren<MeshRenderer>(true);
        }

        if (renderer == null)
        {
            Debug.LogWarning("Final flow: pipe '" + pipe.name + "' has no MeshRenderer.");
            return;
        }

        PipeFlowManualOverride manual = pipe.GetComponent<PipeFlowManualOverride>();
        PlantFlowKind flowKind = route.flowKind;
        Color color = route.flowColor;
        float speed = route.speed;
        float density = route.density;
        float pipeAlpha = route.pipeAlpha;
        float intensity = route.flowIntensity;
        float sharpness = route.bandSharpness;
        bool reverse = route.reverseDirection;
        bool flowing = true;

        if (manual != null && manual.useOverride)
        {
            if (manual.overrideFlowKind)
            {
                flowKind = manual.flowKind;
                color = ColorForKind(flowKind);
            }
            if (manual.overrideColor) color = manual.color;
            if (manual.overrideSpeed) speed = manual.speed;
            if (manual.overrideDensity) density = manual.density;
            if (manual.overridePipeAlpha) pipeAlpha = manual.pipeAlpha;
            if (manual.overrideFlowIntensity) intensity = manual.flowIntensity;
            if (manual.overrideDirection) reverse = manual.reverseDirection;
            if (manual.forceOff) flowing = false;
        }

        Material material = CreatePipeMaterial(flowKind, color, speed, density, pipeAlpha, intensity, sharpness);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        PipeFlowAnimator animator = pipe.GetComponent<PipeFlowAnimator>();
        if (animator == null)
        {
            animator = pipe.AddComponent<PipeFlowAnimator>();
        }

        animator.reverseDirection = reverse;
        animator.speedOverride = speed * globalSpeedMultiplier;
        animator.isFlowing = flowing;
        animator.isGhostSupply = false;
    }

    private Material CreatePipeMaterial(PlantFlowKind kind, Color color, float speed, float density, float alpha, float intensity, float sharpness)
    {
        string keyName = kind + "_" + ColorUtility.ToHtmlStringRGBA(color) + "_" + speed + "_" + density + "_" + alpha + "_" + intensity;
        int key = keyName.GetHashCode();

        // Cache by broad kind for normal routes; manual override creates customized material names.
        if (!materialCache.TryGetValue(kind, out Material material) || material == null || material.name.Contains("Manual"))
        {
            Shader shader = Shader.Find("Custom/PipeFlow");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            material = new Material(shader);
            material.name = "FinalFlow_" + kind;
            materialCache[kind] = material;
        }

        float finalDensity = density * globalDensityMultiplier;
        float finalAlpha = Mathf.Clamp01(alpha * globalPipeAlphaMultiplier);
        float finalIntensity = intensity * globalIntensityMultiplier;

        if (material.HasProperty(FlowColorId)) material.SetColor(FlowColorId, color);
        if (material.HasProperty(FlowSpeedId)) material.SetFloat(FlowSpeedId, speed * globalSpeedMultiplier);
        if (material.HasProperty(DensityId)) material.SetFloat(DensityId, finalDensity);
        if (material.HasProperty(BaseAlphaId)) material.SetFloat(BaseAlphaId, finalAlpha);
        if (material.HasProperty(FlowIntensityId)) material.SetFloat(FlowIntensityId, finalIntensity);
        if (material.HasProperty(BandSharpnessId)) material.SetFloat(BandSharpnessId, sharpness);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, finalAlpha));
        if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(color.r, color.g, color.b, finalAlpha));

        MakeTransparent(material, new Color(color.r, color.g, color.b, finalAlpha));
        return material;
    }

    private Dictionary<string, GameObject> BuildSceneObjectLookup()
    {
        Dictionary<string, GameObject> lookup = new Dictionary<string, GameObject>();
        GameObject[] objects = includeInactiveObjects
            ? Resources.FindObjectsOfTypeAll<GameObject>()
            : FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in objects)
        {
            if (obj == null || !obj.scene.IsValid())
            {
                continue;
            }

            if (!lookup.ContainsKey(obj.name))
            {
                lookup.Add(obj.name, obj);
            }
        }

        return lookup;
    }

    private List<GameObject> FindRouteObjects(Dictionary<string, GameObject> sceneObjects, PlantFlowRouteSettings route)
    {
        List<GameObject> matches = new List<GameObject>();
        if (route.namePrefixes == null)
        {
            return matches;
        }

        foreach (KeyValuePair<string, GameObject> pair in sceneObjects)
        {
            foreach (string prefix in route.namePrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix) &&
                    pair.Key.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(pair.Value);
                    break;
                }
            }
        }

        matches.Sort((a, b) => ExtractPipeIndex(a.name).CompareTo(ExtractPipeIndex(b.name)));
        return matches;
    }

    private static int ExtractPipeIndex(string objectName)
    {
        int lastUnderscore = objectName.LastIndexOf('_');
        if (lastUnderscore < 0 || lastUnderscore >= objectName.Length - 1)
        {
            return 0;
        }

        string numberText = objectName.Substring(lastUnderscore + 1);
        return int.TryParse(numberText, out int index) ? index : 0;
    }

    private void ConfigureReactorTransparency(Dictionary<string, GameObject> sceneObjects)
    {
        foreach (GameObject obj in sceneObjects.Values)
        {
            if (obj == null)
            {
                continue;
            }

            string lower = obj.name.ToLowerInvariant();
            if (!lower.Contains("reactor") || lower.Contains("panel") || lower.Contains("manager"))
            {
                continue;
            }

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                Color baseColor = new Color(0.82f, 0.93f, 1f, reactorShellAlpha);
                Material material = renderer.sharedMaterial != null
                    ? new Material(renderer.sharedMaterial)
                    : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

                material.name = "Final_Reactor_Transparent";
                MakeTransparent(material, baseColor);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    private void ConfigureReactorInternalFlow(Dictionary<string, GameObject> sceneObjects)
    {
        GameObject reactor = FindFirstObject(sceneObjects, "Reactor base model", "reactor base model", "reactor");
        if (reactor == null)
        {
            Debug.LogWarning("Final reactor flow: reactor object not found.");
            return;
        }

        ReactorInternalFlowRuntime internalFlow = reactor.GetComponent<ReactorInternalFlowRuntime>();
        if (internalFlow == null)
        {
            internalFlow = reactor.AddComponent<ReactorInternalFlowRuntime>();
        }

        internalFlow.animateCatalystCondition = animateCatalystCondition;
        internalFlow.ConfigureFromReactorBounds();
    }

    private static GameObject FindFirstObject(Dictionary<string, GameObject> sceneObjects, params string[] contains)
    {
        foreach (GameObject obj in sceneObjects.Values)
        {
            if (obj == null)
            {
                continue;
            }

            string lower = obj.name.ToLowerInvariant();
            foreach (string token in contains)
            {
                if (lower.Contains(token.ToLowerInvariant()))
                {
                    return obj;
                }
            }
        }

        return null;
    }

    private static Material MakeTransparent(Material material, Color color)
    {
        if (material == null)
        {
            return null;
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private static List<PlantFlowRouteSettings> BuildDefaultRoutes()
    {
        return new List<PlantFlowRouteSettings>
        {
            Route("Electrolyzer hydrogen production to H2 storage", PlantFlowKind.Hydrogen, new[] {"H2_pipe_"}, false, 2.0f, 24f, 0.20f, 1.30f, "Green H2 generated from water electrolysis."),
            Route("Hydrogen storage feed to mixing/compressor junction", PlantFlowKind.HydrogenFromStorage, new[] {"H2Storage_pipe_"}, false, 2.0f, 24f, 0.20f, 1.30f, "H2 moves away from storage toward compressor/mixing feed."),
            Route("CO2 feed into capture/compression side", PlantFlowKind.CarbonDioxide, new[] {"CO2_pipe_"}, false, 1.55f, 18f, 0.18f, 1.15f, "CO2 route feeding the syngas preparation path."),
            Route("Rich amine from absorber bottom to desorber/regenerator", PlantFlowKind.RichAmine, new[] {"RichAmine_pipe_"}, false, 1.35f, 16f, 0.22f, 1.05f, "CO2-loaded amine leaves absorber and goes to regeneration."),
            Route("Lean amine return from regenerator to absorber top", PlantFlowKind.LeanAmine, new[] {"LeanAmine_pipe_"}, true, 1.35f, 16f, 0.22f, 1.05f, "Regenerated lean amine returns back to absorber."),
            Route("Recycle gas from separator back to compressor feed", PlantFlowKind.RecycleGas, new[] {"RecycleGas_pipe_"}, true, 1.8f, 22f, 0.20f, 1.25f, "Unreacted gas recycle closes the process loop."),
            Route("Mixed H2/CO2/recycle feed before syngas conditioning", PlantFlowKind.MixedFeed, new[] {"MixedFeed_pipe_"}, false, 2.0f, 26f, 0.21f, 1.35f, "Mixed feed after junction/compressor region."),
            Route("Cold syngas toward heat exchanger", PlantFlowKind.SyngasCold, new[] {"Syngas_pipe_1", "Syngas_pipe_2", "Syngas_pipe_3", "Syngas_pipe_4", "Syngas_pipe_5"}, false, 2.15f, 26f, 0.22f, 1.35f, "Syngas before heat exchanger. Syngas_pipe_5 is the heat exchanger boundary."),
            Route("Heated syngas from heat exchanger into methanol reactor", PlantFlowKind.SyngasHeated, new[] {"Syngas_pipe_6", "Syngas_pipe_7", "Syngas_pipe_8"}, false, 2.25f, 28f, 0.23f, 1.45f, "Heated reactor feed after the heat exchanger."),
            Route("Hot reactor effluent to condenser", PlantFlowKind.ReactorEffluent, new[] {"ReactorEffluent_pipe_"}, false, 2.1f, 26f, 0.22f, 1.35f, "Methanol, water, and unreacted gas leaving reactor."),
            Route("Crude methanol vapour/liquid mixture to flash separator", PlantFlowKind.CrudeMethanolVapourLiquid, new[] {"CrudeMeOH_pipe_"}, false, 1.7f, 20f, 0.24f, 1.20f, "Condensed crude product stream."),
            Route("Liquid crude methanol to distillation", PlantFlowKind.LiquidCrudeMethanol, new[] {"Liq_CrudeMeOH_pipe_"}, false, 1.45f, 18f, 0.28f, 1.10f, "Liquid crude methanol feed to distillation."),
            Route("Purified methanol product to storage tank", PlantFlowKind.MethanolProduct, new[] {"MethanolProduct_pipe_"}, false, 1.35f, 18f, 0.30f, 1.15f, "Final methanol product sent to storage.")
        };
    }

    private static PlantFlowRouteSettings Route(string name, PlantFlowKind kind, string[] prefixes, bool reverse, float speed, float density, float alpha, float intensity, string note)
    {
        return new PlantFlowRouteSettings
        {
            routeName = name,
            namePrefixes = prefixes,
            flowKind = kind,
            reverseDirection = reverse,
            speed = speed,
            density = density,
            pipeAlpha = alpha,
            flowIntensity = intensity,
            bandSharpness = 0.62f,
            flowColor = ColorForKind(kind),
            note = note
        };
    }

    public static Color ColorForKind(PlantFlowKind kind)
    {
        switch (kind)
        {
            case PlantFlowKind.Hydrogen: return new Color(0.10f, 1.00f, 0.22f, 1f);
            case PlantFlowKind.HydrogenFromStorage: return new Color(0.22f, 1.00f, 0.36f, 1f);
            case PlantFlowKind.CarbonDioxide: return new Color(0.86f, 0.94f, 1.00f, 1f);
            case PlantFlowKind.RichAmine: return new Color(0.00f, 0.78f, 0.70f, 1f);
            case PlantFlowKind.LeanAmine: return new Color(0.00f, 1.00f, 0.48f, 1f);
            case PlantFlowKind.RecycleGas: return new Color(0.58f, 0.30f, 1.00f, 1f);
            case PlantFlowKind.MixedFeed: return new Color(0.95f, 1.00f, 0.16f, 1f);
            case PlantFlowKind.SyngasCold: return new Color(1.00f, 0.86f, 0.08f, 1f);
            case PlantFlowKind.SyngasHeated: return new Color(1.00f, 0.56f, 0.00f, 1f);
            case PlantFlowKind.ReactorEffluent: return new Color(1.00f, 0.34f, 0.04f, 1f);
            case PlantFlowKind.CrudeMethanolVapourLiquid: return new Color(0.35f, 0.70f, 1.00f, 1f);
            case PlantFlowKind.LiquidCrudeMethanol: return new Color(0.20f, 0.92f, 1.00f, 1f);
            case PlantFlowKind.MethanolProduct: return new Color(0.72f, 0.18f, 1.00f, 1f);
            default: return Color.white;
        }
    }
}
