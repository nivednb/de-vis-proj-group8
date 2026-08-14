using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A single self-sampling live strip-chart (ECG-style) rendered with plain uGUI elements
/// (no external charting package). X is always elapsed time — the trail continuously
/// scrolls forward as new samples arrive, exactly like a heart monitor, and NEVER moves
/// backward. Every sample since the last reset is kept (nothing is trimmed), so a
/// Scrollbar lets the user drag back through the full history; dragging back to the right
/// edge resumes live auto-follow. The visible window is drawn as a smooth Catmull-Rom
/// curve with round markers only at points where the user actively changed the relevant
/// slider, plus a matplotlib-style hover readout available at every recorded sample.
/// </summary>
public sealed class LiveGraphRuntime : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    private struct Sample
    {
        public float Y;
        public float X; // the correlated quantity (temp/pressure) shown in the hover tooltip only
        public float Secondary; // e.g. cumulative stored methanol — shown in tooltip only, not plotted
        public float Time;
        public string ChangeModule; // non-empty if this sample landed just after a manual slider change
        public string ChangeParameter;
        public float ChangeFromValue;
        public float ChangeToValue;
    }

    private const float SampleIntervalSeconds = 0.4f;
    private const float WindowSeconds = 30f;
    private const int CurveSubdivisions = 8;
    private const float HoverRadiusPixels = 16f;
    private const float DotGrowSeconds = 0.3f;
    private const float ChangeMarkerWindowSeconds = 1.2f;

    public string Title;
    public string XLabel; // e.g. "Reactor Temp" — shown in the hover tooltip, not as an axis
    public string YLabel;
    public string SecondaryLabel; // e.g. "Tank" — only shown in the hover tooltip when set
    public Func<PlantProcessSimulator.ProcessSnapshot, float> XSelector;
    public Func<PlantProcessSimulator.ProcessSnapshot, float> YSelector;
    public Func<PlantProcessSimulator.ProcessSnapshot, float> SecondarySelector;
    public Font LabelFont;
    public Color LineColor = new Color(0.08f, 0.62f, 0.9f, 1f);
    public Color PointColor = new Color(0.86f, 0.82f, 0.95f, 1f);
    public Color AxisColor = new Color(0.7f, 0.75f, 0.8f, 1f);

    // When true, the area between the curve and the Y=0 baseline is filled — used for the
    // methanol-output mode to visually read as "the tank filling up" alongside the line.
    public bool ShadeArea;
    public Color ShadeColor = new Color(0.08f, 0.62f, 0.9f, 0.18f);

    // Fixed Y domain (e.g. 0-100% or 0-design-output) so the scale never jumps as new
    // samples arrive — a point's height always reads as "% of the real operating range".
    public float YMin = 0f;
    public float YMax = 1f;

    private readonly List<Sample> samples = new List<Sample>();
    private readonly List<Vector2> pointScreenPositions = new List<Vector2>();
    private readonly List<Sample> visiblePointSamples = new List<Sample>();

    private Canvas ownerCanvas;
    private RectTransform plotArea;
    private RectTransform pointsLayer;
    private RectTransform linesLayer;
    private RectTransform shadeLayer;
    private Text xAxisMinLabel;
    private Text xAxisMaxLabel;
    private Text yAxisMinLabel;
    private Text yAxisMaxLabel;
    private Text yAxisNameLabel;
    private GameObject tooltip;
    private Text tooltipText;
    private RectTransform tooltipRect;
    private RectTransform selfRect;
    private RectTransform hoverDot;
    private RectTransform shadeHighlightBar;
    private Scrollbar scrollbar;
    private float nextSampleTime;
    private RectTransform newestDot;
    private float newestDotSpawnTime;
    private PlantProcessSimulator subscribedSimulator;

    // Drives the time axis instead of Time.unscaledTime directly — real time keeps
    // advancing while paused, but sampling doesn't, which left a gap on the timeline where
    // "now" had jumped ahead of the last recorded sample. This clock only advances while
    // the simulation is actually running, so the strip picks up exactly where it left off.
    private float clock;
    private bool isLive = true;
    private float viewportStart;
    private float lastOldestTime;
    private float lastMaxViewStart;

    public void Initialize(RectTransform container, Canvas canvas, Font font)
    {
        ownerCanvas = canvas;
        LabelFont = font;

        Image background = gameObject.GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.07f, 0.001f); // near-invisible but still a valid raycast target
        background.raycastTarget = true;

        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.SetParent(container, false);
        Stretch(root);
        selfRect = root;

        Text titleText = MakeText("Title", root, Title, 14, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        StretchWithOffset(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -30f), new Vector2(-14f, -6f));

        // Built as a normal horizontal label sized to the plot's height, then rotated 90°
        // about its own center so it reads bottom-to-top along the left edge — rotating
        // first and sizing to a narrow fixed width instead wraps the text letter-by-letter
        // because Text wrapping is computed from the unrotated rect width.
        yAxisNameLabel = MakeText("Y Axis Name", root, YLabel, 10, FontStyle.Normal, TextAnchor.MiddleCenter, AxisColor);
        yAxisNameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        yAxisNameLabel.rectTransform.anchorMin = yAxisNameLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        yAxisNameLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        yAxisNameLabel.rectTransform.anchoredPosition = new Vector2(12f, 12f);
        yAxisNameLabel.rectTransform.sizeDelta = new Vector2(220f, 16f);
        yAxisNameLabel.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

        Text xAxisNameLabel = MakeText("X Axis Name", root, "Time", 10, FontStyle.Normal, TextAnchor.MiddleCenter, AxisColor);
        StretchWithOffset(xAxisNameLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(58f, 4f), new Vector2(-8f, 34f));

        GameObject plotObject = new GameObject("Plot Area", typeof(RectTransform));
        plotArea = plotObject.GetComponent<RectTransform>();
        plotArea.SetParent(root, false);
        // Extra bottom margin (36px vs the usual 20px) leaves room for the scrollbar strip.
        StretchWithOffset(plotArea, Vector2.zero, Vector2.one, new Vector2(58f, 36f), new Vector2(-14f, -34f));

        // Clean L-shaped axis (matches the reference look) instead of a filled box + gridlines.
        RectTransform yAxisLine = MakePanel("Y Axis Line", plotArea, AxisColor);
        StretchWithOffset(yAxisLine, Vector2.zero, new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f));
        RectTransform xAxisLine = MakePanel("X Axis Line", plotArea, AxisColor);
        StretchWithOffset(xAxisLine, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f));

        shadeLayer = new GameObject("Shade", typeof(RectTransform)).GetComponent<RectTransform>();
        shadeLayer.SetParent(plotArea, false);
        Stretch(shadeLayer);

        linesLayer = new GameObject("Lines", typeof(RectTransform)).GetComponent<RectTransform>();
        linesLayer.SetParent(plotArea, false);
        Stretch(linesLayer);

        pointsLayer = new GameObject("Points", typeof(RectTransform)).GetComponent<RectTransform>();
        pointsLayer.SetParent(plotArea, false);
        Stretch(pointsLayer);

        // Built after plotArea (later sibling => draws on top of the curve) so the legend
        // box stays legible instead of the line passing through it.
        BuildLegend(root);

        yAxisMaxLabel = MakeText("Y Max", root, "", 10, FontStyle.Normal, TextAnchor.UpperRight, AxisColor);
        AnchorTopLeft(yAxisMaxLabel.rectTransform, new Vector2(0f, -20f), new Vector2(52f, 16f));
        yAxisMinLabel = MakeText("Y Min", root, "", 10, FontStyle.Normal, TextAnchor.LowerRight, AxisColor);
        AnchorBottomLeft(yAxisMinLabel.rectTransform, new Vector2(0f, 36f), new Vector2(52f, 16f));

        xAxisMinLabel = MakeText("X Min", root, "", 10, FontStyle.Normal, TextAnchor.LowerLeft, AxisColor);
        AnchorBottomLeft(xAxisMinLabel.rectTransform, new Vector2(58f, 20f), new Vector2(70f, 16f));
        xAxisMaxLabel = MakeText("X Max", root, "", 10, FontStyle.Normal, TextAnchor.LowerRight, AxisColor);
        AnchorBottomRight(xAxisMaxLabel.rectTransform, new Vector2(-14f, 20f), new Vector2(70f, 16f));

        BuildShadeHighlight();
        BuildHoverDot();
        BuildScrollbar(root);
        BuildTooltip(root);

        yAxisMinLabel.text = YMin.ToString("F0");
        yAxisMaxLabel.text = YMax.ToString("F0");

        nextSampleTime = 0f; // sample immediately on first Update
    }

    /// <summary>
    /// Compact boxed legend pinned to the top-right corner of the plot (not spread across
    /// the full width) so a marker's color can be read back to "which module was just
    /// adjusted" at a glance, shared by every graph.
    /// </summary>
    private void BuildLegend(RectTransform root)
    {
        const int columns = 2;
        int rows = Mathf.CeilToInt(GraphVisualUtils.ModulePalette.Length / (float)columns);
        const float rowHeight = 15f;
        const float boxWidth = 176f;
        float boxHeight = rows * rowHeight + 8f;

        RectTransform legend = MakePanel("Legend", root, new Color(0.02f, 0.05f, 0.07f, 0.88f));
        legend.anchorMin = legend.anchorMax = new Vector2(1f, 1f);
        legend.pivot = new Vector2(1f, 1f);
        legend.anchoredPosition = new Vector2(-14f, -34f);
        legend.sizeDelta = new Vector2(boxWidth, boxHeight);
        Outline legendOutline = legend.gameObject.AddComponent<Outline>();
        legendOutline.effectColor = new Color(1f, 1f, 1f, 0.1f);
        legendOutline.effectDistance = new Vector2(1f, -1f);

        for (int i = 0; i < GraphVisualUtils.ModulePalette.Length; i++)
        {
            int col = i % columns;
            int row = i / columns;

            RectTransform cell = new GameObject("Legend Cell", typeof(RectTransform)).GetComponent<RectTransform>();
            cell.SetParent(legend, false);
            cell.anchorMin = new Vector2(col / (float)columns, 1f);
            cell.anchorMax = new Vector2((col + 1) / (float)columns, 1f);
            cell.pivot = new Vector2(0f, 1f);
            cell.anchoredPosition = new Vector2(0f, -4f - row * rowHeight);
            cell.sizeDelta = new Vector2(0f, rowHeight);

            RectTransform dot = MakePanel("Swatch", cell, GraphVisualUtils.ModulePalette[i].Color);
            Image dotImage = dot.GetComponent<Image>();
            dotImage.sprite = GraphVisualUtils.GetCircleSprite();
            dotImage.type = Image.Type.Simple;
            dotImage.raycastTarget = false;
            dot.anchorMin = dot.anchorMax = new Vector2(0f, 0.5f);
            dot.pivot = new Vector2(0f, 0.5f);
            dot.sizeDelta = new Vector2(7f, 7f);
            dot.anchoredPosition = new Vector2(6f, 0f);

            Text label = MakeText("Label", cell, GraphVisualUtils.ModulePalette[i].Module, 8, FontStyle.Normal, TextAnchor.MiddleLeft, AxisColor);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(16f, 0f);
            label.rectTransform.offsetMax = new Vector2(-2f, 0f);
        }
    }

    /// <summary>
    /// A vertical bright band from the baseline up to the curve at whichever X the cursor
    /// is over, shown only for shaded graphs (see ShadeArea) — parented directly under
    /// plotArea (not shadeLayer, which RebuildVisible clears every tick) so it survives
    /// redraws and lets hovering anywhere across the filled area — not just the line — pick
    /// out that moment's cumulative value.
    /// </summary>
    private void BuildShadeHighlight()
    {
        shadeHighlightBar = MakePanel("Shade Highlight", plotArea, new Color(1f, 1f, 1f, 0.28f));
        shadeHighlightBar.GetComponent<Image>().raycastTarget = false;
        shadeHighlightBar.anchorMin = shadeHighlightBar.anchorMax = new Vector2(0.5f, 0.5f);
        shadeHighlightBar.pivot = new Vector2(0.5f, 0f);
        shadeHighlightBar.gameObject.SetActive(false);
    }

    /// <summary>
    /// A single persistent highlight marker (not touched by RebuildVisible's per-tick
    /// destroy/rebuild since it's parented directly under plotArea, a sibling of the
    /// lines/points layers rather than a child of them) that snaps to whichever sample is
    /// nearest the cursor. This is what makes hover work — and visually "highlight" —
    /// everywhere along the curve, not only at the sparse manual-change/newest markers.
    /// </summary>
    private void BuildHoverDot()
    {
        hoverDot = MakePanel("Hover Dot", plotArea, new Color(1f, 1f, 1f, 0.95f));
        Image img = hoverDot.GetComponent<Image>();
        img.sprite = GraphVisualUtils.GetCircleSprite();
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        hoverDot.anchorMin = hoverDot.anchorMax = new Vector2(0.5f, 0.5f);
        hoverDot.pivot = new Vector2(0.5f, 0.5f);
        hoverDot.sizeDelta = new Vector2(15f, 15f);
        Outline outline = hoverDot.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.42f, 0.86f, 1f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        hoverDot.gameObject.SetActive(false);
    }

    private void BuildScrollbar(RectTransform root)
    {
        RectTransform sbRect = MakePanel("Scrollbar", root, new Color(1f, 1f, 1f, 0.08f));
        StretchWithOffset(sbRect, Vector2.zero, new Vector2(1f, 0f), new Vector2(58f, 4f), new Vector2(-14f, 16f));

        RectTransform slidingArea = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
        slidingArea.SetParent(sbRect, false);
        Stretch(slidingArea);

        RectTransform handle = MakePanel("Handle", slidingArea, new Color(0.42f, 0.86f, 1f, 0.9f));
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = new Vector2(1f, 1f);
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;

        scrollbar = sbRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.size = 1f;
        scrollbar.value = 1f;
        scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
    }

    private void OnScrollbarChanged(float v)
    {
        bool wasLive = isLive;
        isLive = v >= 0.995f;
        if (!isLive)
        {
            viewportStart = Mathf.Lerp(lastOldestTime, lastMaxViewStart, v);
        }
        if (wasLive != isLive || !isLive) RebuildVisible();
    }

    private void Update()
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim != subscribedSimulator)
        {
            if (subscribedSimulator != null) subscribedSimulator.ResetRequested -= HandleReset;
            subscribedSimulator = sim;
            if (subscribedSimulator != null) subscribedSimulator.ResetRequested += HandleReset;
        }

        // Grows the newest dot in from a small pulse — cheap per-frame liveliness between
        // the (slower) sampling ticks, and flags "this is the newest point".
        if (newestDot != null)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - newestDotSpawnTime) / DotGrowSeconds);
            float scale = Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 1f, t));
            newestDot.localScale = new Vector3(scale, scale, 1f);
        }

        bool running = sim != null && sim.IsRunning;
        if (running) clock += Time.unscaledDeltaTime;

        if (clock < nextSampleTime) return;
        nextSampleTime = clock + SampleIntervalSeconds;

        if (!running || YSelector == null) return;

        PlantProcessSimulator.ProcessSnapshot snapshot = sim.Current;
        // Recency is checked in real (unscaled) time on both sides — CommitManualChange
        // stamps with Time.unscaledTime too — independently of `clock`, which only tracks
        // simulated run-time and is used purely for X-axis positioning.
        var lastChange = sim.LastManualChange;
        bool justChanged = Time.unscaledTime - lastChange.Time < ChangeMarkerWindowSeconds;
        Sample sample = new Sample
        {
            Y = YSelector(snapshot),
            X = XSelector != null ? XSelector(snapshot) : 0f,
            Secondary = SecondarySelector != null ? SecondarySelector(snapshot) : 0f,
            Time = clock,
            ChangeModule = justChanged ? lastChange.Module : null,
            ChangeParameter = justChanged ? lastChange.Parameter : null,
            ChangeFromValue = lastChange.FromValue,
            ChangeToValue = lastChange.ToValue
        };

        samples.Add(sample);
        RebuildVisible();
    }

    private void OnDestroy()
    {
        if (subscribedSimulator != null) subscribedSimulator.ResetRequested -= HandleReset;
    }

    /// <summary>Clears the trail back to empty so the strip starts fresh from t=0, matching
    /// the process having just restarted. This is the ONLY thing that removes points —
    /// otherwise every sample since the last reset is kept in `samples` (unbounded) and the
    /// scrollbar can always reach back to it; only the on-screen visible slice is rebuilt.</summary>
    private void HandleReset()
    {
        samples.Clear();
        isLive = true;
        viewportStart = 0f;
        clock = 0f;
        nextSampleTime = 0f;
        RebuildVisible();
    }

    private void RebuildVisible()
    {
        for (int i = linesLayer.childCount - 1; i >= 0; i--) Destroy(linesLayer.GetChild(i).gameObject);
        for (int i = pointsLayer.childCount - 1; i >= 0; i--) Destroy(pointsLayer.GetChild(i).gameObject);
        for (int i = shadeLayer.childCount - 1; i >= 0; i--) Destroy(shadeLayer.GetChild(i).gameObject);
        pointScreenPositions.Clear();
        visiblePointSamples.Clear();
        newestDot = null;

        if (samples.Count == 0)
        {
            xAxisMinLabel.text = $"-{WindowSeconds:F0}s";
            xAxisMaxLabel.text = "now";
            if (scrollbar != null) scrollbar.size = 1f;
            return;
        }

        float latestTime = samples[samples.Count - 1].Time;
        float oldestTime = samples[0].Time;
        float maxViewStart = Mathf.Max(oldestTime, latestTime - WindowSeconds);
        lastOldestTime = oldestTime;
        lastMaxViewStart = maxViewStart;

        if (isLive)
        {
            viewportStart = maxViewStart;
            if (scrollbar != null) scrollbar.SetValueWithoutNotify(1f);
        }
        else
        {
            viewportStart = Mathf.Clamp(viewportStart, oldestTime, maxViewStart);
        }

        if (scrollbar != null)
        {
            float totalSpan = Mathf.Max(latestTime - oldestTime, WindowSeconds);
            scrollbar.size = Mathf.Clamp01(WindowSeconds / totalSpan);
        }

        float viewportEnd = viewportStart + WindowSeconds;
        xAxisMinLabel.text = FormatRelative(viewportStart - latestTime);
        xAxisMaxLabel.text = FormatRelative(viewportEnd - latestTime);

        Rect plotRect = plotArea.rect;
        float slack = SampleIntervalSeconds * 2f;
        var controlPoints = new List<Vector2>();
        var controlSamples = new List<Sample>();
        for (int i = 0; i < samples.Count; i++)
        {
            Sample sample = samples[i];
            if (sample.Time < viewportStart - slack || sample.Time > viewportEnd + slack) continue;
            float nx = Mathf.InverseLerp(viewportStart, viewportEnd, sample.Time);
            float ny = Mathf.InverseLerp(YMin, YMax, sample.Y);
            Vector2 anchored = new Vector2(plotRect.xMin + nx * plotRect.width, plotRect.yMin + ny * plotRect.height);
            controlPoints.Add(anchored);
            controlSamples.Add(sample);
        }

        // Smooth Catmull-Rom curve through the control points instead of straight segments.
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Vector2 p0 = i > 0 ? controlPoints[i - 1] : controlPoints[i];
            Vector2 p1 = controlPoints[i];
            Vector2 p2 = controlPoints[i + 1];
            Vector2 p3 = i + 2 < controlPoints.Count ? controlPoints[i + 2] : p2;
            Vector2 previous = p1;
            for (int step = 1; step <= CurveSubdivisions; step++)
            {
                float t = step / (float)CurveSubdivisions;
                Vector2 point = CatmullRom(p0, p1, p2, p3, t);
                if (ShadeArea) CreateShadeBar(previous, point, plotRect.yMin);
                CreateLineSegment(previous, point, LineColor);
                previous = point;
            }
        }

        // Every sample in view is hoverable, but only manual-change samples (color-coded by
        // module — see GraphVisualUtils.ModulePalette/legend) and the newest sample get a visible round
        // marker — sparse dots, matching a real strip chart.
        int last = controlPoints.Count - 1;
        for (int i = 0; i <= last; i++)
        {
            pointScreenPositions.Add(controlPoints[i]);
            visiblePointSamples.Add(controlSamples[i]);

            bool isNewest = i == last;
            bool changed = !string.IsNullOrEmpty(controlSamples[i].ChangeModule);
            if (!changed && !isNewest) continue;

            // Kept small so a run of manual adjustments doesn't turn into an illegible
            // cluster — the hover dot (see BuildHoverDot) is what visually "grows" a point.
            float size = changed ? 7f : 8f;
            Color markerColor = changed ? GraphVisualUtils.GetModuleColor(controlSamples[i].ChangeModule) : PointColor;
            RectTransform dot = MakePanel("Point " + i, pointsLayer, markerColor);
            Image dotImage = dot.GetComponent<Image>();
            dotImage.sprite = GraphVisualUtils.GetCircleSprite();
            dotImage.type = Image.Type.Simple;
            dotImage.raycastTarget = false;
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(size, size);
            dot.anchoredPosition = controlPoints[i];

            if (isNewest)
            {
                newestDot = dot;
                newestDotSpawnTime = Time.unscaledTime;
                dot.localScale = new Vector3(0.25f, 0.25f, 1f);
            }
        }
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static string FormatRelative(float secondsFromNow)
    {
        return secondsFromNow >= -0.05f ? "now" : $"-{Mathf.Abs(secondsFromNow):F0}s";
    }

    private void CreateLineSegment(Vector2 from, Vector2 to, Color color)
    {
        RectTransform line = MakePanel("Segment", linesLayer, color);
        line.GetComponent<Image>().raycastTarget = false;
        // Anchored at the layer's center (0.5, 0.5) to match the coordinate space the point
        // positions are computed in (plotArea.rect, centered on its own pivot).
        line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
        line.pivot = new Vector2(0f, 0.5f);
        Vector2 delta = to - from;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        line.sizeDelta = new Vector2(length + 1f, 3f);
        line.anchoredPosition = from;
        line.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// One thin vertical bar from the Y=0 baseline up to the curve, for the segment between
    /// two adjacent (already-subdivided) curve points — approximates a filled area-under-
    /// curve using only axis-aligned rects (no custom mesh needed), reading as a smooth
    /// shaded fill at the subdivision density already used for the curve itself.
    /// </summary>
    private void CreateShadeBar(Vector2 from, Vector2 to, float baselineY)
    {
        RectTransform bar = MakePanel("Shade Bar", shadeLayer, ShadeColor);
        bar.GetComponent<Image>().raycastTarget = false;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
        bar.pivot = new Vector2(0.5f, 0f);
        float midX = (from.x + to.x) * 0.5f;
        float curveY = Mathf.Max(from.y, to.y);
        float width = Mathf.Abs(to.x - from.x) + 1f;
        float height = Mathf.Max(0f, curveY - baselineY);
        bar.sizeDelta = new Vector2(width, height);
        bar.anchoredPosition = new Vector2(midX, baselineY);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (pointScreenPositions.Count == 0 || plotArea == null)
        {
            HideTooltip();
            return;
        }

        Camera cam = ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? ownerCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(plotArea, eventData.position, cam, out Vector2 local))
        {
            HideTooltip();
            return;
        }

        int nearest = -1;
        Rect plotRect = plotArea.rect;
        if (ShadeArea && local.x >= plotRect.xMin && local.x <= plotRect.xMax)
        {
            // Shaded graphs respond to hovering anywhere across the filled area (not just
            // near the line itself) — nearest sample by X alone, ignoring vertical distance,
            // so the whole width of the fill is "hoverable" like the reference behaviour.
            float bestDx = float.MaxValue;
            for (int i = 0; i < pointScreenPositions.Count; i++)
            {
                float dx = Mathf.Abs(local.x - pointScreenPositions[i].x);
                if (dx < bestDx)
                {
                    bestDx = dx;
                    nearest = i;
                }
            }
        }
        else
        {
            float nearestDistance = HoverRadiusPixels;
            for (int i = 0; i < pointScreenPositions.Count; i++)
            {
                float d = Vector2.Distance(local, pointScreenPositions[i]);
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearest = i;
                }
            }
        }

        if (nearest < 0)
        {
            HideTooltip();
            return;
        }

        Sample s = visiblePointSamples[nearest];
        float secondsAgo = clock - s.Time;
        string xLine = !string.IsNullOrEmpty(XLabel) ? $"{XLabel}: {s.X:F1}\n" : "";
        string secondaryLine = !string.IsNullOrEmpty(SecondaryLabel) ? $"{SecondaryLabel}: {s.Secondary:F0}\n" : "";
        string changeLine = !string.IsNullOrEmpty(s.ChangeModule)
            ? $"\n{s.ChangeParameter} changed from {s.ChangeFromValue:F1} to {s.ChangeToValue:F1} in {s.ChangeModule}"
            : "";
        tooltipText.text = $"{xLine}{secondaryLine}{YLabel}: {s.Y:F1}\n{secondsAgo:F0}s ago" + changeLine;
        tooltip.SetActive(true);

        if (hoverDot != null)
        {
            hoverDot.anchoredPosition = pointScreenPositions[nearest];
            hoverDot.gameObject.SetActive(true);
        }

        if (shadeHighlightBar != null)
        {
            if (ShadeArea)
            {
                float curveY = pointScreenPositions[nearest].y;
                shadeHighlightBar.sizeDelta = new Vector2(10f, Mathf.Max(0f, curveY - plotRect.yMin));
                shadeHighlightBar.anchoredPosition = new Vector2(pointScreenPositions[nearest].x, plotRect.yMin);
                shadeHighlightBar.gameObject.SetActive(true);
            }
            else
            {
                shadeHighlightBar.gameObject.SetActive(false);
            }
        }

        // selfRect (this graph's root) has the default centered pivot, so its local-space
        // origin is at its own center, not its bottom-left corner. tooltipRect is anchored
        // at (0.5, 0.5) to match that same origin (see BuildTooltip).
        RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, eventData.position, cam, out Vector2 tooltipLocal);
        tooltipRect.anchoredPosition = tooltipLocal + new Vector2(14f, 14f);
        tooltip.transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.SetActive(false);
        if (hoverDot != null) hoverDot.gameObject.SetActive(false);
        if (shadeHighlightBar != null) shadeHighlightBar.gameObject.SetActive(false);
    }

    private void BuildTooltip(RectTransform root)
    {
        tooltip = new GameObject("Tooltip", typeof(RectTransform)).gameObject;
        tooltipRect = tooltip.GetComponent<RectTransform>();
        tooltipRect.SetParent(root, false);
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = Vector2.zero;
        tooltipRect.sizeDelta = new Vector2(200f, 112f);
        Image bg = tooltip.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.05f, 0.07f, 0.96f);
        bg.raycastTarget = false;
        Outline outline = tooltip.AddComponent<Outline>();
        outline.effectColor = new Color(0.42f, 0.86f, 1f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        tooltipText = MakeText("Tooltip Text", tooltipRect, "", 11, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        Stretch(tooltipText.rectTransform);
        tooltipText.rectTransform.offsetMin = new Vector2(8f, 4f);
        tooltipText.rectTransform.offsetMax = new Vector2(-8f, -4f);

        tooltip.SetActive(false);
    }

    private Text MakeText(string name, Transform parent, string value, int size, FontStyle style, TextAnchor anchor, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = LabelFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private RectTransform MakePanel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StretchWithOffset(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void AnchorTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AnchorBottomLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AnchorBottomRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
