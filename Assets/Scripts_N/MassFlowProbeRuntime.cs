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

    private const float ReadoutWidth = 344f;

    private Canvas canvas;
    private Camera plantCamera;
    private GameObject readout;
    private RectTransform readoutRect;
    private Image swatch;
    private Text headingText;
    private Text subText;
    private Text labelsText;
    private Text bodyText;
    private Text compositionText;
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

        headingText.text = UITheme.Pretty(stream.Name);
        subText.text = $"{segmentName}  ·  {phase}";
        swatch.color = PlantStreamLegend.ColorFor(kind);
        labelsText.text =
            "Mass flow\nVolume flow\nVelocity\nDensity\nConditions\n" +
            (stream.MolarMassGMol > 0f ? "Molar mass\n" : "") +
            "Line";
        bodyText.text =
            $"{stream.MassFlowKgH:N0} kg/h  ({stream.MassFlowKgH / 3600f:F3} kg/s)\n" +
            $"{stream.VolumetricFlowM3H:N1} m³/h\n" +
            $"{stream.VelocityMS:F1} m/s{basis}\n" +
            $"{stream.DensityKgM3:F1} kg/m³{basis}\n" +
            $"{stream.TemperatureC:F0} °C  ·  {stream.PressureBar:F0} bar\n" +
            (stream.MolarMassGMol > 0f ? $"{stream.MolarMassGMol:F1} g/mol\n" : "") +
            $"{PipeStreamState.NominalBoreLabel(kind)}  ({stream.BoreMm:F1} mm bore)";
        compositionText.text = UITheme.Pretty(stream.Composition);

        readout.SetActive(true);
        readout.transform.SetAsLastSibling();

        // Follow the cursor, flipping side/edge so the panel never leaves the screen.
        RectTransform parent = (RectTransform)readoutRect.parent;
        Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out Vector2 local);

        float rowsHeight = Mathf.Ceil(Mathf.Max(labelsText.preferredHeight, bodyText.preferredHeight));
        labelsText.rectTransform.sizeDelta = new Vector2(labelsText.rectTransform.sizeDelta.x, rowsHeight);
        bodyText.rectTransform.sizeDelta = new Vector2(bodyText.rectTransform.sizeDelta.x, rowsHeight);
        float compTop = 68f + rowsHeight + 10f;
        UITheme.TopLeft(compositionText.rectTransform, 16f, compTop, ReadoutWidth - 32f, 20f);
        float compHeight = string.IsNullOrEmpty(compositionText.text) ? 0f : Mathf.Ceil(compositionText.preferredHeight);
        float height = compTop + compHeight + 14f;
        readoutRect.sizeDelta = new Vector2(ReadoutWidth, height);

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
        UITheme.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());

        RectTransform rootRect = UITheme.NewRect("Probe Root", canvasObject.transform);
        UITheme.Fill(rootRect);

        readoutRect = UITheme.Card("Probe Readout", rootRect, 14f, Color.white, 30f, 10f, 0.22f);
        readout = readoutRect.gameObject;
        readoutRect.GetComponent<UIRaycastTarget>().raycastTarget = false;
        readoutRect.anchorMin = readoutRect.anchorMax = new Vector2(0.5f, 0.5f);
        readoutRect.pivot = Vector2.zero;
        readoutRect.sizeDelta = new Vector2(ReadoutWidth, 200f);

        swatch = UITheme.Dot("Swatch", readoutRect, 10f, UITheme.Accent);
        UITheme.TopLeft(swatch.rectTransform, 16f, 20f, 10f, 10f);
        headingText = UITheme.Label("Heading", readoutRect, "", 14f, UITheme.Weight.ExtraBold, UITheme.Ink);
        UITheme.TopLeft(headingText.rectTransform, 34f, 13f, ReadoutWidth - 50f, 22f);
        subText = UITheme.Label("Segment", readoutRect, "", 12f, UITheme.Weight.SemiBold, UITheme.Subtle);
        UITheme.TopLeft(subText.rectTransform, 34f, 34f, ReadoutWidth - 50f, 17f);
        Image rule = UITheme.Panel("Rule", readoutRect, UITheme.Line);
        UITheme.TopBand(rule.rectTransform, 16f, 58f, 16f, 1f);

        labelsText = UITheme.Label("Labels", readoutRect, "", 12.5f, UITheme.Weight.SemiBold, UITheme.Subtle, TextAnchor.UpperLeft);
        labelsText.lineSpacing = 1.3f;
        UITheme.TopLeft(labelsText.rectTransform, 16f, 68f, 104f, 140f);
        bodyText = UITheme.Label("Values", readoutRect, "", 12.5f, UITheme.Weight.Bold, UITheme.Ink, TextAnchor.UpperLeft);
        bodyText.lineSpacing = 1.3f;
        UITheme.TopLeft(bodyText.rectTransform, 122f, 68f, ReadoutWidth - 138f, 140f);
        compositionText = UITheme.Label("Composition", readoutRect, "", 12f, UITheme.Weight.Medium, UITheme.Muted, TextAnchor.UpperLeft, true);

        readout.SetActive(false);
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
