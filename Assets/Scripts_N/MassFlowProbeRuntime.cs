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

    private readonly List<Renderer> pipeRenderers = new List<Renderer>();
    private MaterialPropertyBlock probeBlock;
    private Renderer highlighted;
    private bool collidersReady;
    private bool active;

    private static readonly int HighlightId = Shader.PropertyToID("_Highlight");

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
            Cursor.SetCursor(GetCursorTexture(), new Vector2(15f, 15f), CursorMode.Auto);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
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

    /// <summary>Read-modify-write so the flow animator's own property-block values survive.</summary>
    private void ApplyHighlight(Renderer renderer, float amount)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(probeBlock);
        probeBlock.SetFloat(HighlightId, amount);
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
    /// A measuring crosshair drawn procedurally — a ringed reticle with a centre dot, in the
    /// dashboard accent colour with a dark outline so it stays readable over the plant, the
    /// sky and the dark UI alike.
    /// </summary>
    private Texture2D GetCursorTexture()
    {
        if (cursorTexture != null) return cursorTexture;

        const int size = 32;
        const float centre = 15.5f;
        cursorTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[size * size];
        Color ink = new Color(0.42f, 0.86f, 1f, 1f);
        Color edge = new Color(0f, 0.05f, 0.09f, 0.9f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - centre, dy = y - centre;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                // Ring, four tick marks poking out of it, and a centre dot.
                float ring = Mathf.Abs(r - 9f);
                bool onRing = ring <= 1.2f;
                bool onEdge = ring > 1.2f && ring <= 2.2f;
                bool tick = (Mathf.Abs(dx) <= 0.9f && r > 9f && r <= 14f) ||
                            (Mathf.Abs(dy) <= 0.9f && r > 9f && r <= 14f);
                bool tickEdge = (Mathf.Abs(dx) <= 1.9f && r > 9f && r <= 14.6f) ||
                                (Mathf.Abs(dy) <= 1.9f && r > 9f && r <= 14.6f);
                bool dot = r <= 1.8f;
                bool dotEdge = r > 1.8f && r <= 2.8f;

                Color c = Color.clear;
                if (onEdge || tickEdge || dotEdge) c = edge;
                if (onRing || tick || dot) c = ink;
                pixels[y * size + x] = c;
            }
        }

        cursorTexture.SetPixels(pixels);
        cursorTexture.Apply();
        return cursorTexture;
    }
}
