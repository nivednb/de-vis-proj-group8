using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Auto-assigns process-flow materials and lightweight flow animation to the
/// existing pipe objects in the reduced reactor/pipe scene.
///
/// It uses the real process direction for power-to-methanol:
/// H2 / CO2 feed -> syngas compression -> methanol reactor -> condenser /
/// separator / distillation -> methanol storage, with recycle/amine return lines.
///
/// Attach this to an empty GameObject, then use the context menu:
/// "Configure Whole Plant Pipe Flow".
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class FlowNetworkAutoConfigurator : MonoBehaviour
{
    [Header("Setup")]
    public bool configureOnStart = false;
    public bool includeInactiveObjects = true;
    public float defaultFlowSpeed = 1.2f;
    [Range(0f, 1f)] public float pipeBaseAlpha = 0.18f;
    public float flowTiling = 8f;

    [Header("Route Name Prefixes")]
    [Tooltip("Electrolyzer output to hydrogen tank. Uses H2_pipe_1...")]
    public string h2ElectrolyzerPrefix = "H2_pipe_";
    [Tooltip("Hydrogen storage to compressor/mixing feed. Uses H2Storage_pipe_1...")]
    public string h2StoragePrefix = "H2Storage_pipe_";
    public string richAminePrefix = "RichAmine_pipe_";
    public string leanAminePrefix = "LeanAmine_pipe_";
    public string co2Prefix = "CO2_pipe_";
    public string recycleGasPrefix = "RecycleGas_pipe_";
    [Tooltip("Compressor/mixed feed to reactor. Syngas_pipe_5 passes through the heat exchanger before Syngas_pipe_6.")]
    public string syngasPrefix = "Syngas_pipe_";
    public string reactorEffluentPrefix = "ReactorEffluent_pipe_";
    [Tooltip("Condenser/flash vapour-liquid product route before liquid distillation feed.")]
    public string crudeMeohPrefix = "CrudeMeOH_pipe_";
    [Tooltip("Liquid crude methanol from flash separator toward distillation.")]
    public string liquidCrudeMeohPrefix = "Liq_CrudeMeOH_pipe_";
    public string methanolProductPrefix = "MethanolProduct_pipe_";

    [Header("Optional Reactor Visuals")]
    public bool autoAddCatalystVisualizer = true;
    public bool autoMakeReactorTransparent = true;
    [Range(0.05f, 0.8f)] public float reactorAlpha = 0.32f;

    private readonly Dictionary<FlowKind, Material> materialByKind = new Dictionary<FlowKind, Material>();

    private enum FlowKind
    {
        Hydrogen,
        RichAmine,
        LeanAmine,
        CarbonDioxide,
        RecycleGas,
        Syngas,
        SyngasAfterHeatExchanger,
        ReactorEffluent,
        CrudeMethanol,
        LiquidCrudeMethanol,
        MethanolProduct
    }

    private struct RouteSpec
    {
        public string label;
        public FlowKind kind;
        public bool reverse;
        public bool ghostSupply;
        public string prefix;

        public RouteSpec(string label, FlowKind kind, bool reverse, bool ghostSupply, string prefix)
        {
            this.label = label;
            this.kind = kind;
            this.reverse = reverse;
            this.ghostSupply = ghostSupply;
            this.prefix = prefix;
        }
    }

    private void Start()
    {
        if (configureOnStart && Application.isPlaying)
        {
            ConfigureWholePlantPipeFlow();
        }
    }

    [ContextMenu("Configure Whole Plant Pipe Flow")]
    public void ConfigureWholePlantPipeFlow()
    {
        materialByKind.Clear();

        Dictionary<string, GameObject> sceneObjects = BuildSceneObjectLookup();
        RouteSpec[] routes = BuildRouteSpecs();

        int assigned = 0;
        foreach (RouteSpec route in routes)
        {
            List<GameObject> pipeObjects = FindObjectsByPrefix(sceneObjects, route.prefix);

            if (pipeObjects.Count == 0)
            {
                Debug.LogWarning("Flow setup: no pipe objects found for prefix '" + route.prefix + "' (" + route.label + ")");
                continue;
            }

            foreach (GameObject pipeObject in pipeObjects)
            {
                Material material = GetMaterialForPipe(route.kind, pipeObject.name);
                MeshRenderer renderer = pipeObject.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    renderer = pipeObject.GetComponentInChildren<MeshRenderer>();
                }

                if (renderer == null)
                {
                    Debug.LogWarning("Flow setup: no MeshRenderer found on '" + pipeObject.name + "'");
                    continue;
                }

                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                PipeFlowAnimator animator = pipeObject.GetComponent<PipeFlowAnimator>();
                if (animator == null)
                {
                    animator = pipeObject.AddComponent<PipeFlowAnimator>();
                }

                animator.reverseDirection = route.reverse;
                animator.speedOverride = defaultFlowSpeed;
                animator.isFlowing = true;
                animator.isGhostSupply = route.ghostSupply;

                assigned++;
            }

            Debug.Log("Flow setup: " + route.label + " -> " + pipeObjects.Count + " objects using prefix " + route.prefix);
        }

        if (autoMakeReactorTransparent)
        {
            ConfigureReactorTransparency(sceneObjects);
        }

        if (autoAddCatalystVisualizer)
        {
            AddCatalystVisualizer(sceneObjects);
        }

        Debug.Log("Flow setup complete. Assigned flow animation/materials to " + assigned + " pipe objects.");
    }

    private Dictionary<string, GameObject> BuildSceneObjectLookup()
    {
        Dictionary<string, GameObject> lookup = new Dictionary<string, GameObject>();
        GameObject[] allObjects = includeInactiveObjects
            ? Resources.FindObjectsOfTypeAll<GameObject>()
            : FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
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

    private RouteSpec[] BuildRouteSpecs()
    {
        return new[]
        {
            new RouteSpec("Electrolyzer hydrogen output to H2 tank", FlowKind.Hydrogen, false, false, h2ElectrolyzerPrefix),
            new RouteSpec("H2 tank/storage output to compressor feed", FlowKind.Hydrogen, false, false, h2StoragePrefix),
            new RouteSpec("CO2-rich amine from absorber to desorber/regenerator", FlowKind.RichAmine, false, false, richAminePrefix),
            new RouteSpec("Lean amine return from desorber/regenerator to absorber", FlowKind.LeanAmine, true, false, leanAminePrefix),
            new RouteSpec("CO2 feed to compressor/mixing junction", FlowKind.CarbonDioxide, false, false, co2Prefix),
            new RouteSpec("Recycle gas from flash separator back to compressor feed", FlowKind.RecycleGas, true, false, recycleGasPrefix),
            new RouteSpec("Compressed H2/CO2 syngas feed to reactor via heat exchanger", FlowKind.Syngas, false, false, syngasPrefix),
            new RouteSpec("Hot methanol reactor effluent to condenser", FlowKind.ReactorEffluent, false, false, reactorEffluentPrefix),
            new RouteSpec("Condensed crude methanol / product mixture to flash separator", FlowKind.CrudeMethanol, false, false, crudeMeohPrefix),
            new RouteSpec("Liquid crude methanol from flash separator to distillation", FlowKind.LiquidCrudeMethanol, false, false, liquidCrudeMeohPrefix),
            new RouteSpec("Purified methanol product to storage tank", FlowKind.MethanolProduct, false, false, methanolProductPrefix)
        };
    }

    private List<GameObject> FindObjectsByPrefix(Dictionary<string, GameObject> sceneObjects, string prefix)
    {
        List<GameObject> matches = new List<GameObject>();

        foreach (KeyValuePair<string, GameObject> pair in sceneObjects)
        {
            if (pair.Value == null || !pair.Key.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(pair.Value);
        }

        matches.Sort((a, b) => ExtractPipeIndex(a.name).CompareTo(ExtractPipeIndex(b.name)));
        return matches;
    }

    private Material GetMaterialForPipe(FlowKind kind, string objectName)
    {
        // Engineering cue: Syngas_pipe_5 enters/approaches the heat exchanger;
        // Syngas_pipe_6 onward is the conditioned/heated reactor feed.
        if (kind == FlowKind.Syngas && ExtractPipeIndex(objectName) >= 6)
        {
            return GetOrCreateFlowMaterial(FlowKind.SyngasAfterHeatExchanger);
        }

        return GetOrCreateFlowMaterial(kind);
    }

    private int ExtractPipeIndex(string objectName)
    {
        int lastUnderscore = objectName.LastIndexOf('_');
        if (lastUnderscore < 0 || lastUnderscore >= objectName.Length - 1)
        {
            return 0;
        }

        string numberText = objectName.Substring(lastUnderscore + 1);
        return int.TryParse(numberText, out int index) ? index : 0;
    }

    private Material GetOrCreateFlowMaterial(FlowKind kind)
    {
        if (materialByKind.TryGetValue(kind, out Material existing))
        {
            return existing;
        }

        Color color = GetFlowColor(kind);
        Shader shader = Shader.Find("Custom/PipeFlow");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = "AutoFlow_" + kind;

        if (material.HasProperty("_FlowColor")) material.SetColor("_FlowColor", color);
        if (material.HasProperty("_FlowSpeed")) material.SetFloat("_FlowSpeed", defaultFlowSpeed);
        if (material.HasProperty("_Tiling")) material.SetFloat("_Tiling", flowTiling);
        if (material.HasProperty("_BaseAlpha")) material.SetFloat("_BaseAlpha", pipeBaseAlpha);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, pipeBaseAlpha));
        if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(color.r, color.g, color.b, pipeBaseAlpha));

        if (!material.HasProperty("_FlowColor"))
        {
            MakeTransparent(material, new Color(color.r, color.g, color.b, pipeBaseAlpha));
        }

        materialByKind[kind] = material;
        return material;
    }

    private Color GetFlowColor(FlowKind kind)
    {
        switch (kind)
        {
            case FlowKind.Hydrogen: return new Color(0.10f, 1.00f, 0.18f, 1f);
            case FlowKind.RichAmine: return new Color(0.00f, 0.90f, 0.78f, 1f);
            case FlowKind.LeanAmine: return new Color(0.00f, 1.00f, 0.42f, 1f);
            case FlowKind.CarbonDioxide: return new Color(0.86f, 0.92f, 1.00f, 1f);
            case FlowKind.RecycleGas: return new Color(0.58f, 0.28f, 1.00f, 1f);
            case FlowKind.Syngas: return new Color(1.00f, 0.82f, 0.08f, 1f);
            case FlowKind.SyngasAfterHeatExchanger: return new Color(1.00f, 0.72f, 0.02f, 1f);
            case FlowKind.ReactorEffluent: return new Color(1.00f, 0.45f, 0.05f, 1f);
            case FlowKind.CrudeMethanol: return new Color(0.35f, 0.72f, 1.00f, 1f);
            case FlowKind.LiquidCrudeMethanol: return new Color(0.20f, 0.92f, 1.00f, 1f);
            case FlowKind.MethanolProduct: return new Color(0.72f, 0.18f, 1.00f, 1f);
            default: return Color.white;
        }
    }

    private void ConfigureReactorTransparency(Dictionary<string, GameObject> sceneObjects)
    {
        foreach (GameObject obj in sceneObjects.Values)
        {
            if (obj == null || obj.name.ToLowerInvariant().Contains("pipe"))
            {
                continue;
            }

            string lowerName = obj.name.ToLowerInvariant();
            if (!lowerName.Contains("reactor"))
            {
                continue;
            }

            MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Material transparentMaterial = new Material(renderer.sharedMaterial);
                transparentMaterial.name = renderer.sharedMaterial != null ? renderer.sharedMaterial.name + "_Transparent" : "Reactor_Transparent";

                Color baseColor = Color.white;
                if (transparentMaterial.HasProperty("_BaseColor"))
                {
                    baseColor = transparentMaterial.GetColor("_BaseColor");
                }
                else if (transparentMaterial.HasProperty("_Color"))
                {
                    baseColor = transparentMaterial.GetColor("_Color");
                }

                baseColor.a = reactorAlpha;
                MakeTransparent(transparentMaterial, baseColor);
                renderer.sharedMaterial = transparentMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    private void AddCatalystVisualizer(Dictionary<string, GameObject> sceneObjects)
    {
        GameObject catalystTarget = null;

        foreach (GameObject obj in sceneObjects.Values)
        {
            if (obj == null)
            {
                continue;
            }

            string lowerName = obj.name.ToLowerInvariant();
            if (lowerName.Contains("catalyst") || lowerName.Contains("reactor bed"))
            {
                catalystTarget = obj;
                break;
            }
        }

        if (catalystTarget == null)
        {
            foreach (GameObject obj in sceneObjects.Values)
            {
                if (obj != null && obj.name.ToLowerInvariant().Contains("reactor"))
                {
                    catalystTarget = obj;
                    break;
                }
            }
        }

        if (catalystTarget == null)
        {
            Debug.LogWarning("Catalyst visualizer: no catalyst/reactor bed object found.");
            return;
        }

        CatalystBedConditionVisualizer visualizer = catalystTarget.GetComponent<CatalystBedConditionVisualizer>();
        if (visualizer == null)
        {
            visualizer = catalystTarget.AddComponent<CatalystBedConditionVisualizer>();
        }

        visualizer.AutoFindRenderer();
        visualizer.ApplyConditionColor();
    }

    private static void MakeTransparent(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
