using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Engineering-quality runtime reactor visualisation for URP.
/// Shows a continuous multi-layered process domain inside the transparent reactor:
/// - Layer 1: Reactant feed (H2, CO2, CO) inlet distribution and spread
/// - Layer 2: Catalyst flow (porous axial gas flow through catalyst bed)
/// - Layer 3: Chemical conversion (progressive fading of feed, amber catalytic glints)
/// - Layer 4: Products (Methanol and Water appearing and merging into outlet nozzle)
/// - Layer 5: Exothermic heat (subtle warm volumetric glow, no flames)
/// </summary>
[DisallowMultipleComponent]
public class ReactorInternalFlowRuntime : MonoBehaviour
{
    [Header("Process Fractions (0-1)")]
    [Range(0f, 1f)] public float H2Fraction = 0.55f;
    [Range(0f, 1f)] public float CO2Fraction = 0.30f;
    [Range(0f, 1f)] public float COFraction = 0.15f;
    [Range(0f, 1f)] public float MethanolFraction = 0.65f;
    [Range(0f, 1f)] public float WaterFraction = 0.35f;

    [Header("Reactor Controls")]
    [Range(10f, 300f)] public float FeedRate = 120f;
    [Range(0f, 1f)] public float Conversion = 0.75f;
    [Range(0f, 1f)] public float Temperature = 0.60f; // Exothermic heat intensity
    [Range(0f, 2f)] public float Turbulence = 0.45f;
    [Range(0.02f, 0.3f)] public float ParticleSize = 0.08f;

    [Header("Catalyst Bed Condition")]
    public bool animateCatalystCondition = true;
    [Range(0f, 1f)] public float catalystCondition01 = 0.55f;
    [Range(0.02f, 2f)] public float catalystAnimationSpeed = 0.35f;

    [Header("Measured Bounds (Output)")]
    public Vector3 ReactorBoundsCenter;
    public Vector3 ReactorBoundsSize;
    public Vector3 CatalystBoundsCenter;
    public Vector3 CatalystBoundsSize;
    public Vector3 FlowDirection = Vector3.down;

    private Renderer catalystRenderer;
    private Material catalystMaterial;
    private Bounds cachedBounds;

    private ParticleSystem feedSystem;
    private ParticleSystem glintSystem;
    private ParticleSystem productSystem;
    private ParticleSystem heatSystem;

    public void ConfigureFromReactorBounds()
    {
        cachedBounds = CalculateVisibleReactorBounds();

        ReactorBoundsCenter = cachedBounds.center;
        ReactorBoundsSize = cachedBounds.size;

        catalystRenderer = FindExistingCatalystRenderer();
        if (catalystRenderer != null)
        {
            CatalystBoundsCenter = catalystRenderer.bounds.center;
            CatalystBoundsSize = catalystRenderer.bounds.size;
        }
        else
        {
            CatalystBoundsCenter = cachedBounds.center;
            CatalystBoundsSize = new Vector3(cachedBounds.size.x * 0.88f, cachedBounds.size.y * 0.75f, cachedBounds.size.z * 0.88f);
        }

        // Derive Flow Direction from real nozzles
        GameObject feedNozzle = GameObject.Find("Nozzle_Feed_Inlet");
        GameObject productNozzle = GameObject.Find("Nozzle_Product_Outlet");
        if (feedNozzle != null && productNozzle != null)
        {
            FlowDirection = (productNozzle.transform.position - feedNozzle.transform.position).normalized;
        }
        else
        {
            FlowDirection = Vector3.down;
        }

        MakeReactorShellTransparent();
        PrepareCatalystBed();
        CreateInternalStreams();
        ApplyCatalystColor();
    }

    void Update()
    {
        if (animateCatalystCondition && Application.isPlaying)
        {
            catalystCondition01 = 0.38f + Mathf.PingPong(Time.time * catalystAnimationSpeed, 0.52f);
            ApplyCatalystColor();
        }

        UpdateParticleSystems();
    }

    void OnValidate()
    {
        // Keep fractions normalized
        float totalFeed = H2Fraction + CO2Fraction + COFraction;
        if (totalFeed > 0.001f)
        {
            H2Fraction /= totalFeed;
            CO2Fraction /= totalFeed;
            COFraction /= totalFeed;
        }

        float totalProduct = MethanolFraction + WaterFraction;
        if (totalProduct > 0.001f)
        {
            MethanolFraction /= totalProduct;
            WaterFraction /= totalProduct;
        }

        UpdateParticleSystems();
    }

    Bounds CalculateVisibleReactorBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r != null && !(r is ParticleSystemRenderer) &&
                r.gameObject.name.Equals("Reactor_Shell", System.StringComparison.OrdinalIgnoreCase))
            {
                return r.bounds;
            }
        }

        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, new Vector3(3f, 9f, 3f));
        foreach (Renderer r in renderers)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("flow") || n.Contains("particle") || n.Contains("generated")) continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return bounds;
    }

    void MakeReactorShellTransparent()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer) continue;

            string n = r.gameObject.name.ToLowerInvariant();
            bool isVesselShell = n == "reactor_shell" || n == "cap_top" || n == "cap_bottom";
            if (!isVesselShell) continue;

            Material material = r.material;
            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            color.a = 0.22f; // Slightly more transparent for engineering clarity
            SetMaterialColor(material, color);
            MakeTransparent(material, color);
        }
    }

    void PrepareCatalystBed()
    {
        catalystRenderer = FindExistingCatalystRenderer();
        if (catalystRenderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        catalystMaterial = new Material(shader) { name = "Runtime_Catalyst_Reaction_Material" };
        catalystRenderer.material = catalystMaterial;
        catalystRenderer.shadowCastingMode = ShadowCastingMode.Off;
        catalystRenderer.receiveShadows = false;
    }

    Renderer FindExistingCatalystRenderer()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer) continue;

            string objectName = r.gameObject.name.ToLowerInvariant();
            string materialName = r.sharedMaterial != null ? r.sharedMaterial.name.ToLowerInvariant() : "";

            bool likelyCatalyst = objectName.Contains("catalyst") || objectName.Contains("bed") || materialName.Contains("catalyst") || materialName.Contains("bed");
            bool wrongPart = objectName.Contains("pipe") || objectName.Contains("nozzle") || objectName.Contains("flange") || objectName.Contains("skirt") || objectName.Contains("shell");

            if (likelyCatalyst && !wrongPart) return r;
        }
        return null;
    }

    void CreateInternalStreams()
    {
        // Clean up any legacy or duplicate children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.name.Contains("Reaction") || child.name.Contains("Feed") || child.name.Contains("Glint") || child.name.Contains("Product") || child.name.Contains("Heat"))
            {
                DestroyImmediate(child);
            }
        }

        Vector3 topNozzlePos = CatalystBoundsCenter + Vector3.up * CatalystBoundsSize.y * 0.5f;
        Vector3 bottomNozzlePos = CatalystBoundsCenter - Vector3.up * CatalystBoundsSize.y * 0.5f;

        // Find exact nozzle positions if available
        GameObject feedNozzle = GameObject.Find("Nozzle_Feed_Inlet");
        GameObject productNozzle = GameObject.Find("Nozzle_Product_Outlet");
        if (feedNozzle != null) topNozzlePos = feedNozzle.transform.position;
        if (productNozzle != null) bottomNozzlePos = productNozzle.transform.position;

        // 1. Layer 1: Reactant Feed Particles
        GameObject feedGo = new GameObject("Reactor_Feed_Particles");
        feedGo.transform.SetParent(transform, true);
        feedGo.transform.position = topNozzlePos;
        feedSystem = feedGo.AddComponent<ParticleSystem>();
        SetupFeedSystem(feedSystem);

        // 2. Layer 3: Catalyst Active Glints
        GameObject glintGo = new GameObject("Reactor_Active_Glints");
        glintGo.transform.SetParent(transform, true);
        glintGo.transform.position = CatalystBoundsCenter;
        glintSystem = glintGo.AddComponent<ParticleSystem>();
        SetupGlintSystem(glintSystem);

        // 3. Layer 4: Products
        GameObject productGo = new GameObject("Reactor_Product_Particles");
        productGo.transform.SetParent(transform, true);
        productGo.transform.position = CatalystBoundsCenter;
        productSystem = productGo.AddComponent<ParticleSystem>();
        SetupProductSystem(productSystem, bottomNozzlePos);

        // 4. Layer 5: Heat Glow
        GameObject heatGo = new GameObject("Reactor_Heat_Glow");
        heatGo.transform.SetParent(transform, true);
        heatGo.transform.position = CatalystBoundsCenter - Vector3.up * CatalystBoundsSize.y * 0.15f; // Center heat lower-middle
        heatSystem = heatGo.AddComponent<ParticleSystem>();
        SetupHeatSystem(heatSystem);

        UpdateParticleSystems();

        if (Application.isPlaying)
        {
            feedSystem.Play();
            glintSystem.Play();
            productSystem.Play();
            heatSystem.Play();
        }
    }

    void SetupFeedSystem(ParticleSystem ps)
    {
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 3.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(ParticleSize * 0.4f, ParticleSize * 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(CatalystBoundsSize.x * 0.15f, 0.2f);
        shape.rotation = new Vector3(90f, 0f, 0f); // Pointing down

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = Turbulence * 0.12f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.22f;
        noise.damping = true;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;

        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.material = CreateParticleMaterial(Color.white);
        pr.shadowCastingMode = ShadowCastingMode.Off;
    }

    void SetupGlintSystem(ParticleSystem ps)
    {
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(ParticleSize * 0.18f, ParticleSize * 0.35f);
        main.startColor = new Color(1.00f, 0.65f, 0.15f, 0.85f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(CatalystBoundsSize.x * 0.85f, CatalystBoundsSize.y * 0.90f, CatalystBoundsSize.z * 0.85f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = Turbulence * 0.25f;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1.00f, 0.65f, 0.15f), 0f), new GradientColorKey(new Color(1.00f, 0.85f, 0.30f), 0.5f), new GradientColorKey(new Color(1.00f, 0.50f, 0.10f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.25f), new GradientAlphaKey(0.85f, 0.75f), new GradientAlphaKey(0f, 1f) }
        );
        fade.color = grad;

        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.material = CreateParticleMaterial(new Color(1f, 0.7f, 0.2f, 1f));
        pr.shadowCastingMode = ShadowCastingMode.Off;
    }

    void SetupProductSystem(ParticleSystem ps, Vector3 bottomNozzle)
    {
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(ParticleSize * 0.5f, ParticleSize * 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(CatalystBoundsSize.x * 0.88f, CatalystBoundsSize.y * 0.92f, CatalystBoundsSize.z * 0.88f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = Turbulence * 0.15f;
        noise.frequency = 0.40f;
        noise.scrollSpeed = 0.20f;
        noise.damping = true;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;

        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.material = CreateParticleMaterial(Color.white);
        pr.shadowCastingMode = ShadowCastingMode.Off;
    }

    void SetupHeatSystem(ParticleSystem ps)
    {
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.0f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(CatalystBoundsSize.x * 0.55f, CatalystBoundsSize.x * 0.82f);
        main.startColor = new Color(1.00f, 0.45f, 0.05f, 0.02f); // Extremely soft
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(CatalystBoundsSize.x * 0.75f, CatalystBoundsSize.y * 0.50f, CatalystBoundsSize.z * 0.75f); // Focus heat in lower-middle

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = Turbulence * 0.1f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.08f;
        noise.damping = true;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1.00f, 0.45f, 0.05f), 0f), new GradientColorKey(new Color(1.00f, 0.45f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.022f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        fade.color = grad;

        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.material = CreateParticleMaterial(new Color(1f, 0.5f, 0.1f, 0.02f));
        pr.shadowCastingMode = ShadowCastingMode.Off;
    }

    void UpdateParticleSystems()
    {
        if (feedSystem == null || glintSystem == null || productSystem == null || heatSystem == null) return;

        // Apply global size adjustments
        var fMain = feedSystem.main;
        fMain.startSize = new ParticleSystem.MinMaxCurve(ParticleSize * 0.4f, ParticleSize * 0.8f);

        var gMain = glintSystem.main;
        gMain.startSize = new ParticleSystem.MinMaxCurve(ParticleSize * 0.18f, ParticleSize * 0.35f);

        var pMain = productSystem.main;
        pMain.startSize = new ParticleSystem.MinMaxCurve(ParticleSize * 0.5f, ParticleSize * 0.9f);

        var hMain = heatSystem.main;
        hMain.startSize = new ParticleSystem.MinMaxCurve(CatalystBoundsSize.x * 0.55f, CatalystBoundsSize.x * 0.82f);

        // Apply velocities based on FlowDirection
        float speed = 1.35f; // default velocity
        Vector3 mainVelocity = FlowDirection * speed;

        var fVel = feedSystem.velocityOverLifetime;
        fVel.x = mainVelocity.x;
        fVel.y = mainVelocity.y;
        fVel.z = mainVelocity.z;

        var gVel = glintSystem.velocityOverLifetime;
        gVel.x = mainVelocity.x * 0.12f; // glints drift very slowly
        gVel.y = mainVelocity.y * 0.12f;
        gVel.z = mainVelocity.z * 0.12f;

        var pVel = productSystem.velocityOverLifetime;
        pVel.x = mainVelocity.x;
        pVel.y = mainVelocity.y;
        pVel.z = mainVelocity.z;

        var hVel = heatSystem.velocityOverLifetime;
        hVel.x = mainVelocity.x * 0.05f;
        hVel.y = mainVelocity.y * 0.05f;
        hVel.z = mainVelocity.z * 0.05f;

        // Apply noises
        var fNoise = feedSystem.noise;
        fNoise.strength = Turbulence * 0.12f;

        var gNoise = glintSystem.noise;
        gNoise.strength = Turbulence * 0.25f;

        var pNoise = productSystem.noise;
        pNoise.strength = Turbulence * 0.15f;

        var hNoise = heatSystem.noise;
        hNoise.strength = Turbulence * 0.08f;

        // Apply emission rates based on controls
        var fEm = feedSystem.emission;
        fEm.rateOverTime = FeedRate * (1f - Conversion * 0.52f); // Reactants decrease with conversion

        var gEm = glintSystem.emission;
        gEm.rateOverTime = FeedRate * Conversion * 0.38f; // Glints scale with reaction rate

        var pEm = productSystem.emission;
        pEm.rateOverTime = FeedRate * Conversion * 1.15f; // Products scale with feed rate and conversion

        var hEm = heatSystem.emission;
        hEm.rateOverTime = FeedRate * Temperature * 0.12f; // Heat glow scales with temperature

        // Update Multi-Species colors dynamically using discrete key transitions
        // Layer 1 Reactant Feed colors
        Color colH2 = new Color(0.10f, 1.00f, 0.22f, 1f); // soft green
        Color colCO2 = new Color(0.86f, 0.94f, 1.00f, 1f); // pale cyan/white-blue
        Color colCO = new Color(1.00f, 0.82f, 0.15f, 1f); // warm yellow

        Gradient feedGrad = new Gradient();
        float h2Bound = Mathf.Clamp01(H2Fraction);
        float co2Bound = Mathf.Clamp01(H2Fraction + CO2Fraction);

        feedGrad.SetKeys(
            new[] {
                new GradientColorKey(colH2, 0f),
                new GradientColorKey(colH2, Mathf.Max(0.01f, h2Bound - 0.02f)),
                new GradientColorKey(colCO2, Mathf.Min(0.99f, h2Bound + 0.02f)),
                new GradientColorKey(colCO2, Mathf.Max(0.01f, co2Bound - 0.02f)),
                new GradientColorKey(colCO, Mathf.Min(0.99f, co2Bound + 0.02f)),
                new GradientColorKey(colCO, 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        fMain.startColor = feedGrad;

        // Feed Color Over Lifetime (fades as reactants are converted)
        var fColOver = feedSystem.colorOverLifetime;
        Gradient feedLifeGrad = new Gradient();
        feedLifeGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.10f),
                new GradientAlphaKey(0.85f * (1f - Conversion * 0.85f), 0.65f), // Fades significantly if conversion is high
                new GradientAlphaKey(0f, 1f)
            }
        );
        fColOver.color = feedLifeGrad;

        // Layer 4 Product colors
        Color colMethanol = new Color(0.72f, 0.18f, 1.00f, 1f); // violet/magenta
        Color colWater = new Color(0.15f, 0.70f, 1.00f, 1f); // blue/cyan

        Gradient productGrad = new Gradient();
        float meohBound = Mathf.Clamp01(MethanolFraction);
        productGrad.SetKeys(
            new[] {
                new GradientColorKey(colMethanol, 0f),
                new GradientColorKey(colMethanol, Mathf.Max(0.01f, meohBound - 0.02f)),
                new GradientColorKey(colWater, Mathf.Min(0.99f, meohBound + 0.02f)),
                new GradientColorKey(colWater, 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        pMain.startColor = productGrad;

        // Product Color Over Lifetime (starts transparent at top of bed, becomes dense at bottom)
        var pColOver = productSystem.colorOverLifetime;
        Gradient productLifeGrad = new Gradient();
        productLifeGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] {
                new GradientAlphaKey(0f, 0f), // Starts transparent (conversion progressive)
                new GradientAlphaKey(0.20f * Conversion, 0.35f),
                new GradientAlphaKey(0.85f * Conversion, 0.80f), // Dominate toward the bottom
                new GradientAlphaKey(0f, 1f)
            }
        );
        pColOver.color = productLifeGrad;

        // Adjust Heat Glow alpha based on Temperature
        var hColOver = heatSystem.colorOverLifetime;
        Gradient heatLifeGrad = new Gradient();
        float targetAlpha = Mathf.Lerp(0.005f, 0.038f, Temperature);
        heatLifeGrad.SetKeys(
            new[] { new GradientColorKey(new Color(1.00f, 0.45f, 0.05f), 0f), new GradientColorKey(new Color(1.00f, 0.45f, 0.05f), 1f) },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(targetAlpha, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        hColOver.color = heatLifeGrad;
    }

    void ApplyCatalystColor()
    {
        if (catalystMaterial == null) return;

        Color healthy = new Color(0.14f, 0.90f, 0.35f, 0.10f);
        Color active = new Color(1.00f, 0.56f, 0.04f, 0.12f);
        Color hot = new Color(1.00f, 0.06f, 0.02f, 0.15f);

        Color color = catalystCondition01 < 0.70f
            ? Color.Lerp(healthy, active, Mathf.InverseLerp(0.35f, 0.70f, catalystCondition01))
            : Color.Lerp(active, hot, Mathf.InverseLerp(0.70f, 1f, catalystCondition01));

        color.a = 0.12f; // Keep it faint so internal visuals remain perfectly clear
        SetMaterialColor(catalystMaterial, color);

        if (catalystMaterial.HasProperty("_EmissionColor"))
        {
            catalystMaterial.EnableKeyword("_EMISSION");
            Color emission = new Color(color.r, color.g, color.b, 1f) * 0.08f;
            catalystMaterial.SetColor("_EmissionColor", emission);
        }

        MakeTransparent(catalystMaterial, color);
    }

    static Material CreateParticleMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Sprites/Default");
        Material material = new Material(shader) { name = "Runtime_Reactor_Flow_Particle_Material" };
        SetMaterialColor(material, color);
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.color = color;
    }

    static void MakeTransparent(Material material, Color color)
    {
        if (material == null) return;
        SetMaterialColor(material, color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
