using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The mass-flow probe: a footer tool that turns the cursor into a measuring crosshair and
/// reports what is actually flowing inside whichever pipe segment it is pointed at.
///
/// Hovering highlights just that segment and opens a readout next to the cursor, styled like
/// the analytics graph tooltips. Every number comes from <see cref="PipeStreamState"/>, which
/// evaluates the stream from the live simulator snapshot — so the readout tracks slider
/// changes immediately.
/// </summary>
[DisallowMultipleComponent]
public sealed class MassFlowProbeRuntime : MonoBehaviour
{
    private const string RuntimeRootName = "Generated Mass Flow Probe";

    /// <summary>
    /// Pipe probe colliders live on their own layer so the module hover system
    /// (<see cref="InteractiveModulePanelRuntime"/>) can exclude them and keep working while
    /// this tool is available.
    /// </summary>
    public const int PipeLayer = 30;
    public static int PipeLayerMask => 1 << PipeLayer;

    private static readonly Color AccentColor = new Color(0.42f, 0.86f, 1f);

    public static MassFlowProbeRuntime Instance { get; private set; }

    private Canvas canvas;
    private Font font;
    private Camera plantCamera;
    private GameObject readout;
    private RectTransform readoutRect;
    private Text headingText;
    private Text bodyText;
    private Texture2D cursorTexture;
    private Canvas cursorCanvas;
    private RectTransform cursorRect;
    private int cursorPixelSize;

    private readonly List<Renderer> pipeRenderers = new List<Renderer>();
    private MaterialPropertyBlock probeBlock;
    private Renderer highlighted;
    private bool collidersReady;
    private bool active;

    private static readonly int HighlightId = Shader.PropertyToID("_Highlight");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color HighlightTint = new Color(0.30f, 0.85f, 1f, 1f);

    public bool IsActive => active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<MassFlowProbeRuntime>() != null) return;
        new GameObject(RuntimeRootName).AddComponent<MassFlowProbeRuntime>();
    }

    private void Awake()
    {
        Instance = this;
        probeBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildReadout();
    }

    private void OnDestroy()
    {
        if (active) SetActive(false);
        if (cursorTexture != null) Destroy(cursorTexture);
    }

    // ---- activation ---------------------------------------------------------

    public void Toggle() => SetActive(!active);

    public void SetActive(bool value)
    {
        if (active == value) return;
        active = value;
        if (active)
        {
            EnsureColliders();
            EnsureCursorGraphic();
        }
        else
        {
            ShowCrosshair(false);
            ClearHighlight();
            if (readout != null) readout.SetActive(false);
        }
    }

    /// <summary>
    /// Gives every classified pipe renderer a mesh collider on the probe layer, once, the
    /// first time the tool is switched on. Done lazily so a session that never opens the tool
    /// never pays for it.
    /// </summary>
    private void EnsureColliders()
    {
        if (collidersReady) return;
        collidersReady = true;

        PipeFlowAnimator[] animators = FindObjectsByType<PipeFlowAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PipeFlowAnimator animator in animators)
        {
            Renderer renderer = animator.GetComponent<Renderer>();
            MeshFilter filter = animator.GetComponent<MeshFilter>();
            if (renderer == null || filter == null || filter.sharedMesh == null) continue;

            animator.gameObject.layer = PipeLayer;
            if (animator.GetComponent<Collider>() == null)
            {
                MeshCollider collider = animator.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }
            pipeRenderers.Add(renderer);
        }

        // The probe layer must stay visible — moving the pipes onto it would otherwise cull
        // them if the camera's mask happens not to include it.
        Camera cam = PlantCamera();
        if (cam != null) cam.cullingMask |= PipeLayerMask;
    }

    private Camera PlantCamera()
    {
        if (plantCamera == null) plantCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        return plantCamera;
    }

    // ---- hover --------------------------------------------------------------

    private void LateUpdate()
    {
        if (!active) return;
        Mouse mouse = Mouse.current;
        Camera cam = PlantCamera();
        if (mouse == null || cam == null) { ClearHighlight(); HideReadout(); return; }

        Vector2 screen = mouse.position.ReadValue();
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        // Over the dashboard the normal arrow comes back, so buttons still look clickable.
        bool inWindow = screen.x >= 0f && screen.y >= 0f && screen.x <= Screen.width && screen.y <= Screen.height;
        bool crosshair = !overUi && inWindow && Application.isFocused;
        ShowCrosshair(crosshair);
        if (crosshair) PlaceCrosshair(screen);
        if (overUi) { ClearHighlight(); HideReadout(); return; }

        Ray ray = cam.ScreenPointToRay(screen);
        if (!Physics.Raycast(ray, out RaycastHit hit, 5000f, PipeLayerMask))
        {
            ClearHighlight();
            HideReadout();
            return;
        }

        Renderer renderer = hit.collider.GetComponent<Renderer>();
        PipeFlowAnimator animator = hit.collider.GetComponent<PipeFlowAnimator>();
        if (renderer == null || animator == null) { ClearHighlight(); HideReadout(); return; }

        SetHighlight(renderer);
        ShowReadout(animator.flowKind, hit.collider.name, screen);
    }

    private void SetHighlight(Renderer renderer)
    {
        if (highlighted == renderer) return;
        ClearHighlight();
        highlighted = renderer;
        ApplyHighlight(renderer, 1f);
    }

    private void ClearHighlight()
    {
        if (highlighted == null) return;
        ApplyHighlight(highlighted, 0f);
        highlighted = null;
    }

    /// <summary>
    /// Read-modify-write so the flow animator's own property-block values survive. With the
    /// Flow Lab closed a pipe draws with its authored opaque material, which has no probe
    /// highlight of its own, so it is tinted through its base colour instead.
    /// </summary>
    private void ApplyHighlight(Renderer renderer, float amount)
    {
        if (renderer == null) return;
        Material material = renderer.sharedMaterial;
        renderer.GetPropertyBlock(probeBlock);
        if (material != null && material.HasProperty(HighlightId))
        {
            probeBlock.SetFloat(HighlightId, amount);
        }
        else if (material != null && material.HasProperty(BaseColorId))
        {
            Color authored = material.GetColor(BaseColorId);
            probeBlock.SetColor(BaseColorId, amount > 0f ? Color.Lerp(authored, HighlightTint, 0.55f) : authored);
        }
        renderer.SetPropertyBlock(probeBlock);
    }

    // ---- readout ------------------------------------------------------------

    private void HideReadout()
    {
        if (readout != null && readout.activeSelf) readout.SetActive(false);
    }

    private void ShowReadout(PlantFlowKind kind, string segmentName, Vector2 screen)
    {
        PlantProcessSimulator simulator = PlantProcessSimulator.Instance;
        if (simulator == null || readout == null) return;

        PipeStreamState stream = PipeStreamState.Evaluate(kind, simulator.Current);
        string phase = stream.Phase switch
        {
            StreamPhase.Liquid => "liquid",
            StreamPhase.TwoPhase => "two-phase",
            _ => "gas"
        };

        // A two-phase line has no single velocity: the gas outruns the liquid film. What is
        // reported is the homogeneous (no-slip) mixture value, and it says so.
        string basis = stream.Phase == StreamPhase.TwoPhase ? " (mixture)" : "";

        headingText.text = $"{stream.Name.ToUpperInvariant()}\n{segmentName}  ·  {phase}";
        bodyText.text =
            $"Mass flow      {stream.MassFlowKgH:N0} kg/h   ({stream.MassFlowKgH / 3600f:F3} kg/s)\n" +
            $"Volume flow    {stream.VolumetricFlowM3H:N1} m³/h\n" +
            $"Velocity       {stream.VelocityMS:F1} m/s{basis}\n" +
            $"Density        {stream.DensityKgM3:F1} kg/m³{basis}\n" +
            $"Conditions     {stream.TemperatureC:F0} °C   {stream.PressureBar:F0} bar\n" +
            (stream.MolarMassGMol > 0f ? $"Molar mass     {stream.MolarMassGMol:F1} g/mol\n" : "") +
            $"Line           {PipeStreamState.NominalBoreLabel(kind)}  ({stream.BoreMm:F1} mm bore)\n" +
            $"{stream.Composition}";

        readout.SetActive(true);
        readout.transform.SetAsLastSibling();

        // Follow the cursor, flipping side/edge so the panel never leaves the screen.
        RectTransform parent = (RectTransform)readoutRect.parent;
        Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out Vector2 local);

        LayoutRebuilder.ForceRebuildLayoutImmediate(readoutRect);
        float height = Mathf.Max(150f, bodyText.preferredHeight + 62f);
        readoutRect.sizeDelta = new Vector2(readoutRect.sizeDelta.x, height);

        Rect area = parent.rect;
        Vector2 size = readoutRect.sizeDelta;
        float x = local.x + 18f;
        if (x + size.x > area.xMax - 8f) x = local.x - 18f - size.x;
        float y = local.y + 18f;
        if (y + size.y > area.yMax - 8f) y = local.y - 18f - size.y;
        y = Mathf.Max(area.yMin + 8f, y);
        readoutRect.anchoredPosition = new Vector2(x, y);
    }

    private void BuildReadout()
    {
        GameObject canvasObject = new GameObject("Mass Flow Probe Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Below the tutorial (300) but above the dashboard chrome (90).
        canvas.sortingOrder = 140;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1536f, 1024f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject root = new GameObject("Probe Root", typeof(RectTransform));
        root.transform.SetParent(canvasObject.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        readout = new GameObject("Probe Readout", typeof(RectTransform));
        readout.transform.SetParent(rootRect, false);
        readoutRect = readout.GetComponent<RectTransform>();
        readoutRect.anchorMin = readoutRect.anchorMax = new Vector2(0.5f, 0.5f);
        readoutRect.pivot = Vector2.zero;
        readoutRect.sizeDelta = new Vector2(320f, 170f);
        Image background = readout.AddComponent<Image>();
        background.color = new Color(0.02f, 0.05f, 0.07f, 0.96f);
        background.raycastTarget = false;
        Outline outline = readout.AddComponent<Outline>();
        outline.effectColor = new Color(0.42f, 0.86f, 1f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        headingText = CreateText("Heading", readoutRect, 11, FontStyle.Bold, AccentColor);
        headingText.rectTransform.anchorMin = new Vector2(0f, 1f);
        headingText.rectTransform.anchorMax = new Vector2(1f, 1f);
        headingText.rectTransform.offsetMin = new Vector2(10f, -42f);
        headingText.rectTransform.offsetMax = new Vector2(-10f, -6f);

        bodyText = CreateText("Body", readoutRect, 11, FontStyle.Normal, Color.white);
        bodyText.rectTransform.anchorMin = Vector2.zero;
        bodyText.rectTransform.anchorMax = Vector2.one;
        bodyText.rectTransform.offsetMin = new Vector2(10f, 6f);
        bodyText.rectTransform.offsetMax = new Vector2(-10f, -46f);

        readout.SetActive(false);
    }

    private Text CreateText(string name, Transform parent, int size, FontStyle style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.UpperLeft;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.supportRichText = false;
        return text;
    }

    // ---- cursor -------------------------------------------------------------

    /// <summary>
    /// The crosshair is drawn by the app itself on a top-most canvas rather than handed to
    /// the OS as a hardware cursor: Windows rescales hardware cursors to its own cursor size,
    /// which is what made the old 32 px texture look blocky. Drawn here it is generated at the
    /// exact on-screen pixel size for the display's DPI and never resampled.
    /// </summary>
    private void EnsureCursorGraphic()
    {
        if (cursorCanvas == null)
        {
            GameObject canvasObject = new GameObject("Mass Flow Crosshair Canvas");
            canvasObject.transform.SetParent(transform, false);
            cursorCanvas = canvasObject.AddComponent<Canvas>();
            cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above everything, including the tutorial overlay (300).
            cursorCanvas.sortingOrder = 1000;

            GameObject cursorObject = new GameObject("Crosshair", typeof(RectTransform));
            cursorObject.transform.SetParent(canvasObject.transform, false);
            cursorRect = cursorObject.GetComponent<RectTransform>();
            cursorRect.anchorMin = cursorRect.anchorMax = Vector2.zero;
            cursorRect.pivot = new Vector2(0.5f, 0.5f);
            RawImage image = cursorObject.AddComponent<RawImage>();
            image.raycastTarget = false;
        }

        float dpiScale = Screen.dpi > 1f ? Screen.dpi / 96f : 1f;
        int pixels = Mathf.Clamp(Mathf.RoundToInt(44f * dpiScale), 36, 128);
        if (cursorTexture == null || cursorPixelSize != pixels)
        {
            if (cursorTexture != null) Destroy(cursorTexture);
            cursorPixelSize = pixels;
            cursorTexture = BuildCrosshairTexture(pixels);
        }
        cursorRect.GetComponent<RawImage>().texture = cursorTexture;
        // An overlay canvas without a scaler maps one canvas unit to one screen pixel.
        cursorRect.sizeDelta = new Vector2(pixels, pixels);
        cursorCanvas.gameObject.SetActive(false);
    }

    private void ShowCrosshair(bool visible)
    {
        if (cursorCanvas != null && cursorCanvas.gameObject.activeSelf != visible)
            cursorCanvas.gameObject.SetActive(visible);
        Cursor.visible = !visible;
    }

    private void PlaceCrosshair(Vector2 screen)
    {
        if (cursorRect == null) return;
        // Land every texel on exactly one screen pixel so the strokes stay crisp.
        float half = cursorPixelSize * 0.5f;
        cursorRect.anchoredPosition = new Vector2(Mathf.Round(screen.x - half) + half, Mathf.Round(screen.y - half) + half);
    }

    /// <summary>
    /// A precision reticle: four crosshair arms around an open centre, a thin ring and a
    /// centre dot. White strokes with a dark halo and a soft outer shadow stay legible over the
    /// pale sky, the grey plant and the dark dashboard alike. Every shape is an analytic
    /// signed distance supersampled 4x4 per pixel, so the edges stay smooth at any DPI.
    /// </summary>
    private static Texture2D BuildCrosshairTexture(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Mass Flow Crosshair"
        };
        float unit = size / 44f;            // geometry is designed on a 44 px grid
        float centre = size * 0.5f;
        float armWidth = 0.9f * unit;       // half-width of an arm stroke
        float gap = 5.5f * unit;            // open centre radius
        float armEnd = 20f * unit;
        float ringRadius = 11f * unit;
        float ringWidth = 0.75f * unit;
        float dotRadius = 1.6f * unit;
        float halo = 1.35f * unit;
        float shadow = 2.6f * unit;

        Color ink = Color.white;
        Color accent = new Color(0.22f, 0.83f, 1f, 1f);
        Color haloColor = new Color(0.02f, 0.06f, 0.10f, 1f);

        var pixels = new Color[size * size];
        const int ss = 4;
        const float samples = ss * ss;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float inkA = 0f, dotA = 0f, haloA = 0f, shadowA = 0f;
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float px = x + (sx + 0.5f) / ss - centre;
                        float py = y + (sy + 0.5f) / ss - centre;
                        float ax = Mathf.Abs(px), ay = Mathf.Abs(py);
                        float r = Mathf.Sqrt(px * px + py * py);

                        // Arms: one horizontal and one vertical bar, each cut back from the centre.
                        float hArm = Mathf.Max(ay - armWidth, Mathf.Max(gap - ax, ax - armEnd));
                        float vArm = Mathf.Max(ax - armWidth, Mathf.Max(gap - ay, ay - armEnd));
                        float ring = Mathf.Abs(r - ringRadius) - ringWidth;
                        float strokes = Mathf.Min(Mathf.Min(hArm, vArm), ring);
                        float dot = r - dotRadius;
                        float all = Mathf.Min(strokes, dot);

                        if (strokes <= 0f) inkA += 1f;
                        if (dot <= 0f) dotA += 1f;
                        if (all <= halo) haloA += 1f;
                        shadowA += Mathf.Clamp01(1f - Mathf.Max(0f, all - halo) / shadow);
                    }
                }
                inkA /= samples; dotA /= samples; haloA /= samples; shadowA /= samples;

                // Back to front: soft shadow, dark halo, white strokes, accent centre dot.
                Color c = new Color(haloColor.r, haloColor.g, haloColor.b, shadowA * shadowA * 0.35f);
                c = Over(c, haloColor, haloA * 0.9f);
                c = Over(c, ink, inkA);
                c = Over(c, accent, dotA);
                pixels[y * size + x] = c;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    /// <summary>Straight-alpha "source over destination".</summary>
    private static Color Over(Color dst, Color src, float srcA)
    {
        if (srcA <= 0f) return dst;
        float outA = srcA + dst.a * (1f - srcA);
        if (outA <= 1e-5f) return Color.clear;
        Color rgb = (src * srcA + dst * (dst.a * (1f - srcA))) / outA;
        return new Color(rgb.r, rgb.g, rgb.b, outA);
    }
}
