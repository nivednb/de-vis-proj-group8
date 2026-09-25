using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Reactor cutaway: a translucent vessel and catalyst bed with rising feed bubbles.
///
/// Feed bubbles (H2, CO2 and recycle gas) enter at the bottom, rise through the catalyst bed
/// and leave through the top outlet. A share of them equal to the live single-pass conversion
/// reacts inside the bed and turns into crude methanol / water. Every bubble is placed
/// analytically each frame from its own spawn radius, wobble and height, so it can never
/// leave the catalyst-bed radius or the vessel height. Exists only in Play mode.
/// </summary>
public sealed class LightweightReactorVisual : MonoBehaviour
{
    const string RootName = "Lightweight Reactor Cutaway";
    const int MaxBubbles = 150;
    const float MinSize = .11f;
    const float MaxSize = .2f;
    const float MaxWobble = .1f;
    // Converted bubbles briefly swell as they react, so the containment margin allows for it.
    const float ReactionSwell = 1.3f;

    // Render order: bed, then the glass shell, then the bubbles so the glass never veils them.
    const int BedQueue = 3000;
    const int ShellQueue = 3010;
    const int BubbleQueue = 3020;

    struct Bubble
    {
        public bool Alive;
        public float Progress;      // 0 at the inlet, 1 at the outlet
        public float Speed;         // progress per second at nominal flow
        public float Radius;        // distance from the axis at spawn
        public float Angle;
        public float WobbleAmp;
        public float WobbleFreq;
        public float Phase;
        public float Size;
        public Color Feed;
        public bool Reacts;
        public float ReactAt;       // progress at which it reacts
    }

    readonly Bubble[] bubbles = new Bubble[MaxBubbles];
    ParticleSystem.Particle[] particles;
    ParticleSystem system;
    Vector3 axis;
    float yStart, yEnd, bedStart, bedEnd, spawnRadius;
    float clock;

    Color h2, co2, syngas, product, reacting;

    public static void Configure(GameObject owner)
    {
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Renderer shell = null, top = null, bottom = null, catalyst = null;
        foreach (GameObject obj in objects)
        {
            if (!obj.scene.IsValid()) continue;
            if (obj.name.Equals("Reactor_Shell", StringComparison.OrdinalIgnoreCase)) shell = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Cap_Top", StringComparison.OrdinalIgnoreCase)) top = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Cap_Bottom", StringComparison.OrdinalIgnoreCase)) bottom = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Catalyst_Bed", StringComparison.OrdinalIgnoreCase)) catalyst = obj.GetComponent<Renderer>();
        }
        if (shell == null || catalyst == null) return;
        MakeTransparent(shell, .14f, ShellQueue, .55f);
        MakeTransparent(top, .16f, ShellQueue, .55f);
        MakeTransparent(bottom, .16f, ShellQueue, .55f);
        // Translucent packed bed; CatalystBedColorAnimator keeps this alpha while it recolours it.
        MakeTransparent(catalyst, .5f, BedQueue, .15f);

        GameObject existing = GameObject.Find(RootName);
        if (existing != null) Destroy(existing);
        // Kept outside the imported reactor hierarchy so its 3x model scale does not apply.
        GameObject root = new(RootName);
        root.AddComponent<LightweightReactorVisual>().Build(shell.bounds, catalyst.bounds);
    }

    void Build(Bounds shellBounds, Bounds bedBounds)
    {
        axis = new Vector3(bedBounds.center.x, 0f, bedBounds.center.z);
        float bedRadius = Mathf.Min(bedBounds.extents.x, bedBounds.extents.z);
        // Largest spawn radius for which spawn radius + wobble + swollen half-size stays at 94%
        // of the bed radius: the containment guarantee for every bubble.
        spawnRadius = Mathf.Max(.05f, bedRadius * .94f - MaxWobble - MaxSize * ReactionSwell * .5f);

        // Straight cylindrical part of the shell only, never into the domed caps.
        float margin = MaxSize * ReactionSwell;
        yStart = shellBounds.min.y + margin;
        yEnd = shellBounds.max.y - margin;
        bedStart = Mathf.InverseLerp(yStart, yEnd, bedBounds.min.y);
        bedEnd = Mathf.InverseLerp(yStart, yEnd, bedBounds.max.y);
        transform.position = new Vector3(axis.x, (yStart + yEnd) * .5f, axis.z);

        h2 = Fade(PlantStreamLegend.WaterHydrogen, 1f);
        co2 = Fade(PlantStreamLegend.AmineCapturedCo2, 1f);
        syngas = Fade(PlantStreamLegend.CompressedSyngas, 1f);
        product = Fade(PlantStreamLegend.CrudeMethanol, 1f);
        reacting = Fade(Color.Lerp(PlantStreamLegend.HotReactorEffluent, Color.white, .35f), 1f);

        system = CreateRenderer();
        particles = new ParticleSystem.Particle[MaxBubbles];

        // Start with the vessel already populated rather than a wave rising from the inlet.
        float conversion = CurrentConversion();
        for (int i = 0; i < MaxBubbles; i++)
        {
            Spawn(ref bubbles[i], conversion);
            bubbles[i].Progress = UnityEngine.Random.value;
        }
    }

    void LateUpdate()
    {
        if (system == null) return;
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        bool running = sim == null || sim.IsRunning;
        float flow = 1f, density = 1f, conversion = CurrentConversion();
        if (sim != null)
        {
            PlantProcessSimulator.ProcessSnapshot s = sim.Current;
            float feed = Mathf.Clamp01(s.syngasFeedKgH / 1725f);
            density = feed < .01f ? 0f : Mathf.Lerp(.3f, 1f, feed);
            float ghsv = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1000f, s.ghsv) / 8000f), .6f, 1.6f);
            flow = Mathf.Lerp(.45f, 1f, Mathf.Clamp01(s.reactorFeedFlowPercent / 100f)) * ghsv;
        }

        float dt = running ? Time.deltaTime : 0f;
        clock += dt;
        int activeTarget = Mathf.RoundToInt(MaxBubbles * density);
        int count = 0;
        for (int i = 0; i < MaxBubbles; i++)
        {
            ref Bubble b = ref bubbles[i];
            if (!b.Alive)
            {
                // Stagger respawns so bubbles keep arriving evenly instead of in waves.
                if (running && i < activeTarget && UnityEngine.Random.value < dt * 1.5f) Spawn(ref b, conversion);
                if (!b.Alive) continue;
            }

            b.Progress += b.Speed * flow * dt;
            if (b.Progress >= 1f)
            {
                b.Alive = false;
                continue;
            }
            particles[count++] = Place(b);
        }
        system.SetParticles(particles, count);
    }

    void Spawn(ref Bubble b, float conversion)
    {
        b.Alive = true;
        b.Progress = 0f;
        b.Speed = UnityEngine.Random.Range(.075f, .11f);
        b.Radius = spawnRadius * Mathf.Sqrt(UnityEngine.Random.value);  // uniform over the disc
        b.Angle = UnityEngine.Random.value * Mathf.PI * 2f;
        b.WobbleAmp = UnityEngine.Random.Range(.03f, MaxWobble);
        b.WobbleFreq = UnityEngine.Random.Range(1.2f, 2.4f);
        b.Phase = UnityEngine.Random.value * Mathf.PI * 2f;
        b.Size = UnityEngine.Random.Range(MinSize, MaxSize);

        // Fresh feed is H2:CO2 = 3:1 by mole; recycle gas is shown as its own colour.
        float pick = UnityEngine.Random.value;
        b.Feed = pick < .15f ? syngas : pick < .79f ? h2 : co2;
        b.Reacts = UnityEngine.Random.value < conversion;
        b.ReactAt = Mathf.Lerp(bedStart, bedEnd, UnityEngine.Random.Range(.08f, .92f));
    }

    ParticleSystem.Particle Place(in Bubble b)
    {
        float p = b.Progress;
        float t = clock * b.WobbleFreq + b.Phase;

        // Past the bed the flow gathers toward the top outlet on the axis.
        float gather = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bedEnd, 1f, p));
        float radius = b.Radius * Mathf.Lerp(1f, .35f, gather);
        float angle = b.Angle + t * .15f;
        Vector3 offset = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        offset += new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t * .83f)) * b.WobbleAmp;

        float y = Mathf.Lerp(yStart, yEnd, p);
        Color color = b.Feed;
        float size = b.Size * (.9f + .1f * Mathf.Sin(t * 1.7f));
        if (b.Reacts)
        {
            // Reaction: a short bright swell at ReactAt, then the product colour.
            float k = Mathf.InverseLerp(b.ReactAt - .015f, b.ReactAt + .05f, p);
            if (k > 0f)
            {
                float flash = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                color = Color.Lerp(Color.Lerp(b.Feed, product, k), reacting, flash * .8f);
                size *= 1f + (ReactionSwell - 1f) * flash;
            }
        }

        // Fade in at the inlet and out at the outlet so nothing pops.
        color.a *= Mathf.SmoothStep(0f, 1f, p / .06f) * Mathf.SmoothStep(0f, 1f, (1f - p) / .08f);

        return new ParticleSystem.Particle
        {
            position = axis + offset + Vector3.up * y,
            startSize = size,
            startColor = color,
            startLifetime = 1000f,
            remainingLifetime = 1000f
        };
    }

    float CurrentConversion()
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        return sim != null ? Mathf.Clamp01(sim.Current.reactorYieldPercent / 100f) : .25f;
    }

    ParticleSystem CreateRenderer()
    {
        GameObject child = new("Reactor Bubbles");
        child.transform.SetParent(transform, false);
        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = MaxBubbles;
        main.startSpeed = 0f;
        main.startLifetime = 1000f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sharedMaterial = CreateBubbleMaterial();
        ps.Play();
        return ps;
    }

    static Material CreateBubbleMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        Material material = new(shader) { name = "ReactorBubble" };
        Texture2D texture = CreateBubbleTexture(64);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = BubbleQueue;
        return material;
    }

    /// <summary>Soft sphere: translucent body, brighter rim and a small specular highlight.</summary>
    static Texture2D CreateBubbleTexture(int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "ReactorBubble",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[size * size];
        float half = size * .5f;
        Vector2 highlight = new(-.32f, .34f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 uv = new((x + .5f - half) / half, (y + .5f - half) / half);
            float r = uv.magnitude;
            float edge = Mathf.Clamp01((1f - r) * half * .5f);          // anti-aliased outline
            float body = Mathf.Lerp(.86f, 1f, Mathf.SmoothStep(.55f, .98f, r));
            float spec = Mathf.Clamp01(1f - (uv - highlight).magnitude / .28f);
            spec *= spec;
            float shade = Mathf.Lerp(.82f, 1f, Mathf.Clamp01(.5f + .5f * uv.y));
            Color c = new(shade, shade, shade, body * edge);
            c = Color.Lerp(c, new Color(1f, 1f, 1f, edge), spec * .85f);
            pixels[y * size + x] = c;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

    static void MakeTransparent(Renderer renderer, float alpha, int queue, float smoothness)
    {
        if (renderer == null) return;
        Material source = renderer.sharedMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = source != null ? new Material(source) : new Material(shader);
        material.name = "ReactorCutaway_Transparent";
        Color color = new(.70f, .88f, 1f, alpha);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        // The imported materials are tagged Opaque; left that way URP still treats the glass as
        // a solid surface in some passes and it renders as a milky white column.
        material.SetOverrideTag("RenderType", "Transparent");
        // URP keeps full-strength reflections on transparent surfaces by default, which turns
        // the stacked shell and bed into a milky white film over the interior.
        if (material.HasProperty("_BlendModePreserveSpecular")) material.SetFloat("_BlendModePreserveSpecular", 0f);
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = queue;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
