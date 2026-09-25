using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Icon = UITheme.Icon;
using Kind = UITheme.ButtonKind;
using W = UITheme.Weight;

/// <summary>
/// A static entity-vs-entity graph — X is the changing quantity (e.g. reactor temperature),
/// Y is the dependent quantity (e.g. efficiency), NOT time. It starts with a single seed
/// point at the plant's current state, then gets exactly one new point appended per
/// committed manual slider change (any module, not just the one this graph nominally
/// correlates), each connected to the previous point by a straight line. The result grows
/// and reshapes as the user makes more changes, rather than continuously scrolling — this
/// is a record of "what happened as a result of each change", not a live trend line.
///
/// Every point is drawn as a numbered bubble in the colour of the module whose change
/// produced it, so the order of the changes can be read straight off the plot.
///
/// Behind the points, faint model curves show the response at a few constant values of a
/// second reactor condition (temperature, or pressure on the temperature graph) with every
/// other condition at its live value. Clicking a point draws a dotted curve through it at
/// that point's own conditions, showing where the trend goes if only the X quantity changes.
/// </summary>
public sealed class CorrelationGraphRuntime : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
{
    /// <summary>The reactor operating conditions a graph can plot against or hold constant.</summary>
    public enum ReactorInput { Temperature, Pressure, Ratio, Ghsv, FeedFlow }

    private struct Point
    {
        public float X;
        public float Y;
        public string Module; // empty for the seed point (no change caused it)
        public string Parameter;
        public float FromValue;
        public float ToValue;
        public bool HasInputs;
        public PlantProcessSimulator.ProcessInputs Inputs; // operating point when recorded
    }

    private const int TrendSamples = 48;
    private const float TrendRefreshSeconds = 0.5f;

    private const float HoverRadiusPixels = 16f;
    private const float DotGrowSeconds = 0.3f;
    private const float BubbleSize = 20f;
    private const int TickCount = 5;
    private const float LegendWidth = 172f;

    public string Title;
    public string XLabel;
    public string YLabel;
    public Func<PlantProcessSimulator.ProcessSnapshot, float> XSelector;
    public Func<PlantProcessSimulator.ProcessSnapshot, float> YSelector;
    public Font LabelFont;
    public Color LineColor = UITheme.WithAlpha(UITheme.Accent, 0.75f);
    public Color SeedPointColor = UITheme.Muted;

    // Fixed domain on both axes — the actual slider range for X, the real ceiling for Y —
    // used as the floor for the auto-fitted view window (see ComputeViewRange).
    public float XMin = 0f;
    public float XMax = 1f;
    public float YMin = 0f;
    public float YMax = 1f;

    // Model curves. XInput is the condition on the X axis; FamilyInput is held at each of
    // FamilyValues for the faint background curves.
    public bool ShowTrendCurves;
    public ReactorInput XInput;
    public ReactorInput FamilyInput;
    public float[] FamilyValues;
    public string FamilyName = "temperature";
    public string FamilyUnit = "°C";

    private readonly List<Point> points = new List<Point>();
    private readonly List<Vector2> pointScreenPositions = new List<Vector2>();

    private Canvas ownerCanvas;
    private RectTransform selfRect;
    private RectTransform plotArea;
    private RectTransform pointsLayer;
    private Text[] xTicks;
    private Text[] yTicks;
    private RectTransform newestDot;
    private float newestDotSpawnTime;
    private UIGraphTooltip tooltip;
    private RectTransform hoverRing;
    private PlantProcessSimulator subscribedSimulator;
    private UIGraphLine lineGraphic;
    private Button exportButton;
    private bool layoutDirty;

    private RectTransform trendLayer;
    private readonly List<UIGraphLine> familyLines = new List<UIGraphLine>();
    private readonly List<Text> familyLabels = new List<Text>();
    private UIGraphLine tracedLine;
    private Text tracedLabel;
    private RectTransform selectionRing;
    private int selectedIndex = -1;
    private bool trendDirty = true;
    private float nextTrendCheck;
    private PlantProcessSimulator.ProcessInputs trendBasis;
    private bool hasTrendBasis;
    // Y extent of the model curves over the full X range; the view fits these as well as
    // the recorded points so the curves show where the trend goes.
    private bool hasCurveRange;
    private float curveYMin;
    private float curveYMax;
    private readonly List<CanvasGroup> parentGroups = new List<CanvasGroup>();

    // The axes auto-fit to the data (plus padding) rather than always spanning the full
    // physical slider range — see ComputeViewRange.
    private float viewXMin;
    private float viewXMax;
    private float viewYMin;
    private float viewYMax;

    public void Initialize(RectTransform container, Canvas canvas, Font font)
    {
        ownerCanvas = canvas;
        LabelFont = font;
        viewXMin = XMin;
        viewXMax = XMax;
        viewYMin = YMin;
        viewYMax = YMax;

        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.SetParent(container, false);
        UITheme.Fill(root);
        selfRect = root;
        // Invisible raycast target so hovering anywhere over the graph drives the tooltip.
        gameObject.AddComponent<UIRaycastTarget>();

        Text titleText = UITheme.Label("Title", root, Title, 15f, W.ExtraBold, UITheme.Ink);
        UITheme.TopBand(titleText.rectTransform, 0f, 0f, 120f, 20f);
        string subtitleText = ShowTrendCurves
            ? $"Numbered points are your changes. Faint curves hold {FamilyName} constant; click a point to trace its own curve."
            : "Each point is one committed change, numbered in the order it was made.";
        Text subtitle = UITheme.Label("Subtitle", root, subtitleText, 12.5f, W.Medium, UITheme.Subtle);
        UITheme.TopBand(subtitle.rectTransform, 0f, 22f, 120f, 18f);

        exportButton = UITheme.MakeButton("Export Button", root, "Export", Kind.Outline, 13f, Icon.Download, 8f, false, 15f, W.Bold, 12f);
        float ew = UITheme.PreferredWidth(exportButton);
        UITheme.TopRight((RectTransform)exportButton.transform, 0f, 4f, ew, 32f);
        exportButton.onClick.AddListener(ExportNow);

        RectTransform legend = UIGraphKit.ModuleLegend(root, "Colour = module changed", true, LegendWidth);
        UITheme.TopRight(legend, 0f, 56f, LegendWidth, legend.sizeDelta.y);

        GameObject plotObject = new GameObject("Plot Area", typeof(RectTransform));
        plotArea = plotObject.GetComponent<RectTransform>();
        plotArea.SetParent(root, false);
        UITheme.Fill(plotArea, 70f, 62f, LegendWidth + 28f, 46f);

        UIGraphKit.PlotBackground(plotArea);
        UIGraphKit.HorizontalGrid(plotArea, TickCount - 1);
        // Model curves sit under the recorded line and are clipped to the plot, since they
        // span the whole visible X range and can leave the auto-fitted Y window.
        trendLayer = UITheme.NewRect("Trend Curves", plotArea);
        UITheme.Fill(trendLayer);
        trendLayer.gameObject.AddComponent<RectMask2D>();

        yTicks = UIGraphKit.YTicks(root, plotArea, TickCount, 12f);
        xTicks = UIGraphKit.XTicks(plotArea, TickCount, 8f);
        UIGraphKit.RotatedYTitle(root, plotArea, YLabel);
        Text xTitle = UITheme.Label("X Axis Name", root, XLabel, 12f, W.Bold, UITheme.Muted, TextAnchor.LowerRight);
        UITheme.Fill(xTitle.rectTransform, 70f, 0f, LegendWidth + 28f, 0f);
        xTitle.rectTransform.anchorMax = new Vector2(1f, 0f);
        xTitle.rectTransform.offsetMax = new Vector2(-(LegendWidth + 28f), 18f);

        GameObject lineObject = new GameObject("Line Mesh", typeof(RectTransform));
        lineObject.transform.SetParent(plotArea, false);
        UITheme.Fill((RectTransform)lineObject.transform);
        lineGraphic = lineObject.AddComponent<UIGraphLine>();
        lineGraphic.color = LineColor;
        lineGraphic.Thickness = 2.4f;
        lineGraphic.raycastTarget = false;

        pointsLayer = new GameObject("Points", typeof(RectTransform)).GetComponent<RectTransform>();
        pointsLayer.SetParent(plotArea, false);
        UITheme.Fill(pointsLayer);

        BuildHoverRing();
        BuildSelectionRing();
        tooltip = UIGraphTooltip.Create(root);
        UpdateAxisLabels();
    }

    /// <summary>Writes the recorded points "until now" to a timestamped CSV (with the graph's
    /// axis metadata as header comments) plus a PNG of the panel, into a shared folder.</summary>
    private void ExportNow()
    {
        if (points.Count == 0)
        {
            GraphExportUtil.ShowToast(ownerCanvas, LabelFont, $"{Title}: no data recorded yet — change a slider first.");
            return;
        }

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseName = GraphExportUtil.Sanitize($"{Title}_{stamp}");

        var sb = new StringBuilder();
        sb.AppendLine($"# {Title}");
        sb.AppendLine($"# exported,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# x_axis,{GraphExportUtil.Csv(XLabel)}");
        sb.AppendLine($"# y_axis,{GraphExportUtil.Csv(YLabel)}");
        sb.AppendLine($"# x_view_range,{viewXMin.ToString("0.###")},{viewXMax.ToString("0.###")}");
        sb.AppendLine($"# y_view_range,{viewYMin.ToString("0.###")},{viewYMax.ToString("0.###")}");
        sb.AppendLine($"index,x [{UnitFromLabel(XLabel)}],y [{UnitFromLabel(YLabel)}],change_module,change_parameter,from_value,to_value,change_unit");
        for (int i = 0; i < points.Count; i++)
        {
            Point p = points[i];
            bool seed = string.IsNullOrEmpty(p.Module);
            sb.AppendLine(string.Join(",",
                (i + 1).ToString(),
                p.X.ToString("0.#####"),
                p.Y.ToString("0.#####"),
                GraphExportUtil.Csv(p.Module),
                GraphExportUtil.Csv(p.Parameter),
                seed ? "" : p.FromValue.ToString("0.#####"),
                seed ? "" : p.ToValue.ToString("0.#####"),
                seed ? "" : GraphVisualUtils.GetParameterUnit(p.Parameter)));
        }

        string csvPath = GraphExportUtil.WriteText(baseName, "csv", sb.ToString());
        StartCoroutine(GraphExportUtil.CaptureRegionPng(ownerCanvas, selfRect, baseName, pngPath =>
        {
            string msg = pngPath != null
                ? $"Exported {baseName}.csv + .png to {GraphExportUtil.ExportDirectory}"
                : (csvPath != null
                    ? $"Exported {baseName}.csv (PNG failed) to {GraphExportUtil.ExportDirectory}"
                    : "Export failed — see console.");
            GraphExportUtil.ShowToast(ownerCanvas, LabelFont, msg);
        }, exportButton != null ? exportButton.gameObject : null));
    }

    private void Update()
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim != subscribedSimulator)
        {
            if (subscribedSimulator != null)
            {
                subscribedSimulator.ResetRequested -= HandleReset;
                subscribedSimulator.ManualChangeCommitted -= HandleManualChange;
            }
            subscribedSimulator = sim;
            if (subscribedSimulator != null)
            {
                subscribedSimulator.ResetRequested += HandleReset;
                subscribedSimulator.ManualChangeCommitted += HandleManualChange;
            }
            // Seed the graph with the plant's current state the first time a simulator is
            // available, so there's always a starting point before any change happens.
            if (sim != null && points.Count == 0) AddSeedPoint(sim);
        }

        // The plot rect only has its real size once the (initially hidden) window has been
        // laid out; redraw once it changes so points land in the right place.
        if (layoutDirty || (plotArea != null && Mathf.Abs(plotArea.rect.width - lastPlotWidth) > 0.5f))
        {
            layoutDirty = false;
            Redraw();
        }

        if (ShowTrendCurves && IsShown() && Time.unscaledTime >= nextTrendCheck)
        {
            nextTrendCheck = Time.unscaledTime + TrendRefreshSeconds;
            if (!trendDirty && sim != null && (!hasTrendBasis || !SameOperatingPoint(sim.CurrentInputs, trendBasis)))
                trendDirty = true;
            if (trendDirty) RebuildTrendCurves();
        }

        if (newestDot != null)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - newestDotSpawnTime) / DotGrowSeconds);
            float scale = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, t));
            newestDot.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private float lastPlotWidth = -1f;

    private void OnDestroy()
    {
        if (subscribedSimulator != null)
        {
            subscribedSimulator.ResetRequested -= HandleReset;
            subscribedSimulator.ManualChangeCommitted -= HandleManualChange;
        }
    }

    private void AddSeedPoint(PlantProcessSimulator sim)
    {
        if (XSelector == null || YSelector == null) return;
        PlantProcessSimulator.ProcessSnapshot snapshot = sim.Current;
        points.Add(new Point { X = XSelector(snapshot), Y = YSelector(snapshot), Module = null,
            HasInputs = true, Inputs = sim.CurrentInputs });
        Redraw();
    }

    private void HandleManualChange(PlantProcessSimulator.ManualChangeInfo info)
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim == null || XSelector == null || YSelector == null) return;
        PlantProcessSimulator.ProcessSnapshot snapshot = sim.Current;
        points.Add(new Point
        {
            X = XSelector(snapshot),
            Y = YSelector(snapshot),
            Module = info.Module,
            Parameter = info.Parameter,
            FromValue = info.FromValue,
            ToValue = info.ToValue,
            HasInputs = true,
            Inputs = sim.CurrentInputs
        });
        Redraw();
    }

    /// <summary>Clears back to a single fresh seed point matching the just-reset state —
    /// the graph restarts exactly like a brand-new session.</summary>
    private void HandleReset()
    {
        points.Clear();
        selectedIndex = -1;
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim != null) AddSeedPoint(sim);
        else Redraw();
    }

    private Vector2 ToAnchored(Point p) => ToAnchored(p.X, p.Y);

    // Unclamped, so model curves extend past the window and are cut by the plot mask.
    private Vector2 ToAnchored(float x, float y)
    {
        Rect plotRect = plotArea.rect;
        float nx = (x - viewXMin) / Mathf.Max(1e-6f, viewXMax - viewXMin);
        float ny = (y - viewYMin) / Mathf.Max(1e-6f, viewYMax - viewYMin);
        return new Vector2(plotRect.xMin + nx * plotRect.width, plotRect.yMin + ny * plotRect.height);
    }

    private void Redraw()
    {
        if (pointsLayer == null) return;
        lastPlotWidth = plotArea.rect.width;
        for (int i = pointsLayer.childCount - 1; i >= 0; i--) Destroy(pointsLayer.GetChild(i).gameObject);
        pointScreenPositions.Clear();
        newestDot = null;

        trendDirty = true;
        nextTrendCheck = 0f;
        if (selectedIndex >= points.Count) selectedIndex = -1;
        if (selectionRing != null) selectionRing.gameObject.SetActive(false);

        if (points.Count == 0)
        {
            if (lineGraphic != null) lineGraphic.ClearPoints();
            return;
        }

        ComputeViewRange();
        UpdateAxisLabels();

        var anchored = new List<Vector2>(points.Count);
        foreach (Point p in points) anchored.Add(ToAnchored(p));

        if (lineGraphic != null) lineGraphic.SetPoints(anchored);

        int last = anchored.Count - 1;
        for (int i = 0; i <= last; i++)
        {
            pointScreenPositions.Add(anchored[i]);

            bool isSeed = string.IsNullOrEmpty(points[i].Module);
            bool isNewest = i == last;
            Color color = isSeed ? SeedPointColor : GraphVisualUtils.GetModuleColor(points[i].Module);

            RectTransform bubble = UITheme.NewRect("Point " + (i + 1), pointsLayer);
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.pivot = new Vector2(0.5f, 0.5f);
            bubble.sizeDelta = new Vector2(BubbleSize, BubbleSize);
            bubble.anchoredPosition = anchored[i];

            if (isNewest && !isSeed)
            {
                Image halo = UITheme.Dot("Halo", bubble, BubbleSize + 14f, UITheme.WithAlpha(color, 0.2f));
                UITheme.Center(halo.rectTransform, BubbleSize + 14f, BubbleSize + 14f);
            }
            // White ring keeps overlapping bubbles apart.
            Image ring = UITheme.Dot("Ring", bubble, BubbleSize + 4f, Color.white);
            UITheme.Center(ring.rectTransform, BubbleSize + 4f, BubbleSize + 4f);
            Image fill = UITheme.Dot("Fill", bubble, BubbleSize, color);
            UITheme.Center(fill.rectTransform, BubbleSize, BubbleSize);
            // Sequence number, so the order the points were recorded in can be read straight
            // off the plot even when later points land left of earlier ones.
            Text number = UITheme.Label("Number", bubble, (i + 1).ToString(), i + 1 >= 100 ? 9f : 11f, W.ExtraBold, Color.white, TextAnchor.MiddleCenter);
            UITheme.Center(number.rectTransform, BubbleSize + 8f, BubbleSize);

            if (i == selectedIndex && selectionRing != null)
            {
                selectionRing.anchoredPosition = anchored[i];
                selectionRing.gameObject.SetActive(true);
            }

            if (isNewest)
            {
                newestDot = bubble;
                newestDotSpawnTime = Time.unscaledTime;
                bubble.localScale = new Vector3(0.3f, 0.3f, 1f);
            }
        }
    }

    /// <summary>
    /// Auto-fits both axes to the data (plus padding) rather than always spanning the full
    /// physical slider range. A handful of changes only a few units apart were previously
    /// squashed into one corner of a window that spanned the entire operating range.
    /// </summary>
    private void ComputeViewRange()
    {
        float fullX = Mathf.Max(1e-4f, XMax - XMin);
        float fullY = Mathf.Max(1e-4f, YMax - YMin);

        if (points.Count == 0)
        {
            viewXMin = XMin;
            viewXMax = XMax;
            viewYMin = YMin;
            viewYMax = YMax;
            return;
        }

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (Point p in points)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        if (ShowTrendCurves)
        {
            // The curves need the whole operating range for context, so X spans the slider
            // range and Y fits both the points and the curves.
            if (hasCurveRange)
            {
                minY = Mathf.Min(minY, curveYMin);
                maxY = Mathf.Max(maxY, curveYMax);
            }
            float padY = Mathf.Max(maxY - minY, fullY * 0.12f) * 0.08f;
            viewXMin = XMin;
            viewXMax = XMax;
            viewYMin = Mathf.Max(YMin, minY - padY);
            viewYMax = Mathf.Min(YMax, maxY + padY);
            if (viewYMax - viewYMin < fullY * 0.12f)
            {
                float mid = (viewYMin + viewYMax) * 0.5f;
                viewYMin = mid - fullY * 0.06f;
                viewYMax = mid + fullY * 0.06f;
            }
            return;
        }

        // Floor on the window size so a single point (or near-identical points) still gets
        // real context instead of an infinitely zoomed axis.
        float spanX = Mathf.Max(maxX - minX, fullX * 0.12f);
        float spanY = Mathf.Max(maxY - minY, fullY * 0.12f);
        float cx = (minX + maxX) * 0.5f;
        float cy = (minY + maxY) * 0.5f;
        float halfX = spanX * 0.62f; // ~24% padding each side
        float halfY = spanY * 0.62f;

        viewXMin = cx - halfX;
        viewXMax = cx + halfX;
        viewYMin = cy - halfY;
        viewYMax = cy + halfY;
    }

    private void UpdateAxisLabels()
    {
        if (xTicks == null || yTicks == null) return;
        for (int i = 0; i < TickCount; i++)
        {
            float f = i / (float)(TickCount - 1);
            xTicks[i].text = UIGraphKit.FormatTick(Mathf.Lerp(viewXMin, viewXMax, f));
            yTicks[i].text = UIGraphKit.FormatTick(Mathf.Lerp(viewYMin, viewYMax, f));
        }
    }

    // XLabel / YLabel arrive as "Reactor pressure (bar)" — split the unit out so tooltip
    // values can read "82.3 bar" rather than "Reactor pressure (bar): 82.3".
    private static string UnitFromLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return "";
        int open = label.LastIndexOf('(');
        int close = label.LastIndexOf(')');
        return open >= 0 && close > open ? label.Substring(open + 1, close - open - 1).Trim() : "";
    }

    private static string LabelWithoutUnit(string label)
    {
        if (string.IsNullOrEmpty(label)) return "";
        int open = label.LastIndexOf('(');
        return open > 0 ? label.Substring(0, open).Trim() : label.Trim();
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

        // Latest point wins where bubbles overlap — it is drawn on top.
        int nearest = -1;
        float nearestDistance = HoverRadiusPixels;
        for (int i = pointScreenPositions.Count - 1; i >= 0; i--)
        {
            float d = Vector2.Distance(local, pointScreenPositions[i]);
            if (d < nearestDistance - 0.01f)
            {
                nearestDistance = d;
                nearest = i;
            }
        }

        if (nearest < 0)
        {
            HideTooltip();
            return;
        }

        Point p = points[nearest];
        string xu = UnitFromLabel(XLabel);
        string yu = UnitFromLabel(YLabel);
        string cu = GraphVisualUtils.GetParameterUnit(p.Parameter);
        string changeLine = !string.IsNullOrEmpty(p.Module)
            ? $"\n<color=#94A3B8>{UITheme.Pretty(p.Parameter)}: {GraphVisualUtils.FormatValue(p.FromValue, cu)} → {GraphVisualUtils.FormatValue(p.ToValue, cu)} · {UIGraphKit.ModuleDisplayName(p.Module)}</color>"
            : "\n<color=#94A3B8>Starting point</color>";
        string content = $"<b>Point {nearest + 1}</b>\n{LabelWithoutUnit(XLabel)}: {GraphVisualUtils.FormatValue(p.X, xu, "0.#")}\n{LabelWithoutUnit(YLabel)}: {GraphVisualUtils.FormatValue(p.Y, yu, "0.#")}" + changeLine;
        if (ShowTrendCurves && p.HasInputs)
            content += nearest == selectedIndex
                ? $"\n<color=#94A3B8>Click again to hide its {FamilyName} curve</color>"
                : $"\n<color=#94A3B8>Click to trace its constant-{FamilyName} curve</color>";

        if (hoverRing != null)
        {
            hoverRing.anchoredPosition = pointScreenPositions[nearest];
            hoverRing.gameObject.SetActive(true);
            hoverRing.SetSiblingIndex(pointsLayer.GetSiblingIndex());
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, eventData.position, cam, out Vector2 tooltipLocal);
        tooltip.Show(content, tooltipLocal);
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!ShowTrendCurves || plotArea == null) return;
        Camera cam = ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? ownerCanvas.worldCamera : null;
        int hit = -1;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(plotArea, eventData.position, cam, out Vector2 local))
        {
            float best = HoverRadiusPixels;
            for (int i = pointScreenPositions.Count - 1; i >= 0; i--)
            {
                float d = Vector2.Distance(local, pointScreenPositions[i]);
                if (d < best - 0.01f)
                {
                    best = d;
                    hit = i;
                }
            }
        }

        // Clicking the traced point again, or empty plot space, clears the trace.
        selectedIndex = hit >= 0 && hit != selectedIndex && points[hit].HasInputs ? hit : -1;
        if (selectionRing != null)
        {
            selectionRing.gameObject.SetActive(selectedIndex >= 0);
            if (selectedIndex >= 0) selectionRing.anchoredPosition = pointScreenPositions[selectedIndex];
        }
        HideTooltip();
        RebuildTrendCurves();
    }

    // ---- model curves ---------------------------------------------------------------

    /// <summary>
    /// Recomputes the faint constant-value family and, if a point is selected, the dotted
    /// curve through it. Each curve evaluates the pure process model across the visible X
    /// range with only the X condition varying, so nothing here touches the live plant.
    /// </summary>
    private void RebuildTrendCurves()
    {
        trendDirty = false;
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (!ShowTrendCurves || trendLayer == null || sim == null || YSelector == null || points.Count == 0)
        {
            HideTrendCurves();
            return;
        }

        trendBasis = sim.CurrentInputs;
        hasTrendBasis = true;

        // Sample in data space first: the view range depends on where the curves go.
        var family = new List<List<Vector2>>();
        var familyText = new List<string>();
        int count = FamilyValues != null ? FamilyValues.Length : 0;
        for (int k = 0; k < count; k++)
        {
            PlantProcessSimulator.ProcessInputs inputs = trendBasis;
            SetInput(ref inputs, FamilyInput, FamilyValues[k]);
            List<Vector2> curve = SampleCurve(sim, inputs);
            // Curves that coincide (the conversion model is symmetric about its optimum
            // temperature) are drawn once with a combined label rather than stacked.
            int same = family.FindIndex(c => SameCurve(c, curve));
            if (same >= 0)
            {
                familyText[same] = familyText[same].Replace(" " + FamilyUnit, "") + " / " + FormatFamily(FamilyValues[k]);
                continue;
            }
            family.Add(curve);
            familyText.Add(FormatFamily(FamilyValues[k]));
        }

        List<Vector2> traced = null;
        Point tracedPoint = default;
        if (selectedIndex >= 0 && selectedIndex < points.Count && points[selectedIndex].HasInputs)
        {
            tracedPoint = points[selectedIndex];
            traced = SampleCurve(sim, tracedPoint.Inputs);
        }

        float yLo = float.MaxValue, yHi = float.MinValue;
        foreach (List<Vector2> c in family)
            foreach (Vector2 v in c) { yLo = Mathf.Min(yLo, v.y); yHi = Mathf.Max(yHi, v.y); }
        if (traced != null)
            foreach (Vector2 v in traced) { yLo = Mathf.Min(yLo, v.y); yHi = Mathf.Max(yHi, v.y); }
        if (yLo <= yHi)
        {
            float tolerance = Mathf.Max(1e-3f, (YMax - YMin) * 0.002f);
            bool changed = !hasCurveRange || Mathf.Abs(yLo - curveYMin) > tolerance || Mathf.Abs(yHi - curveYMax) > tolerance;
            hasCurveRange = true;
            curveYMin = yLo;
            curveYMax = yHi;
            if (changed)
            {
                Redraw();
                trendDirty = false;
            }
        }

        Color faint = UITheme.WithAlpha(UITheme.Faint, 0.7f);
        var anchoredFamily = new List<List<Vector2>>(family.Count);
        foreach (List<Vector2> c in family) anchoredFamily.Add(ToAnchoredCurve(c));
        int labelAt = WidestSpreadIndex(anchoredFamily);
        for (int k = 0; k < family.Count; k++)
        {
            List<Vector2> anchored = anchoredFamily[k];
            UIGraphLine line = FamilyLine(k);
            line.color = faint;
            line.SetPoints(anchored);
            line.gameObject.SetActive(true);
            if (labelAt >= 0) PlaceLabel(familyLabels[k], anchored[labelAt] + new Vector2(-4f, 3f), false, familyText[k], UITheme.Subtle);
            else PlaceCurveLabel(familyLabels[k], anchored, familyText[k], UITheme.Subtle);
        }
        for (int k = family.Count; k < familyLines.Count; k++)
        {
            familyLines[k].gameObject.SetActive(false);
            familyLabels[k].gameObject.SetActive(false);
        }

        if (traced != null)
        {
            Color color = string.IsNullOrEmpty(tracedPoint.Module) ? UITheme.Accent : GraphVisualUtils.GetModuleColor(tracedPoint.Module);
            List<Vector2> anchored = ToAnchoredCurve(traced);
            UIGraphLine line = TracedLine();
            line.color = color;
            line.SetPoints(anchored);
            line.gameObject.SetActive(true);
            // Beside its own point: the family labels sit at the curve peaks.
            PlaceLabel(TracedLabel(), ToAnchored(tracedPoint) + new Vector2(14f, -24f), true,
                $"Point {selectedIndex + 1} at {FormatFamily(GetInput(tracedPoint.Inputs, FamilyInput))}", color);
        }
        else
        {
            if (tracedLine != null) tracedLine.gameObject.SetActive(false);
            if (tracedLabel != null) tracedLabel.gameObject.SetActive(false);
        }
        SeparateLabels();
    }

    /// <summary>Sample index where the family curves are furthest apart while all of them
    /// are inside the plot, so their labels line up in one readable column; -1 if none.</summary>
    private int WidestSpreadIndex(List<List<Vector2>> curves)
    {
        if (curves.Count < 2) return -1;
        Rect r = plotArea.rect;
        int best = -1;
        float bestSpread = -1f;
        for (int j = 0; j < TrendSamples; j++)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            bool inside = true;
            foreach (List<Vector2> c in curves)
            {
                if (!r.Contains(c[j])) { inside = false; break; }
                lo = Mathf.Min(lo, c[j].y);
                hi = Mathf.Max(hi, c[j].y);
            }
            // Prefer the right-hand side on ties, where the labels read as line endings.
            if (inside && hi - lo >= bestSpread - 0.5f)
            {
                bestSpread = hi - lo;
                best = j;
            }
        }
        return best;
    }

    /// <summary>Nudges overlapping curve labels apart vertically; curves that run close
    /// together (e.g. neighbouring pressures near the temperature optimum) would otherwise
    /// print their labels on top of each other.</summary>
    private void SeparateLabels()
    {
        const float gap = 13f;
        var active = new List<RectTransform>();
        foreach (Text label in familyLabels)
            if (label.gameObject.activeSelf) active.Add(label.rectTransform);
        if (tracedLabel != null && tracedLabel.gameObject.activeSelf) active.Add(tracedLabel.rectTransform);
        active.Sort((a, b) => a.anchoredPosition.y.CompareTo(b.anchoredPosition.y));

        Rect r = plotArea.rect;
        float top = r.yMax - 16f;
        float bottom = r.yMin + 2f;
        // Upward pass spreads the stack; if that runs into the top edge, a downward pass
        // shifts the crowded labels back down instead of piling them on the edge.
        for (int i = 1; i < active.Count; i++)
            for (int j = 0; j < i; j++)
                if (Overlaps(active[j], active[i], gap))
                    SetY(active[i], active[j].anchoredPosition.y + gap);
        for (int i = active.Count - 1; i >= 0; i--)
        {
            float limit = top;
            for (int j = i + 1; j < active.Count; j++)
                if (XOverlap(active[i], active[j])) limit = Mathf.Min(limit, active[j].anchoredPosition.y - gap);
            SetY(active[i], Mathf.Max(bottom, Mathf.Min(active[i].anchoredPosition.y, limit)));
        }
    }

    // Labels pivot at their bottom-right corner, so each spans [x - width, x].
    private static bool XOverlap(RectTransform a, RectTransform b) =>
        a.anchoredPosition.x - a.sizeDelta.x < b.anchoredPosition.x &&
        b.anchoredPosition.x - b.sizeDelta.x < a.anchoredPosition.x;

    private static bool Overlaps(RectTransform lower, RectTransform upper, float gap) =>
        XOverlap(lower, upper) && upper.anchoredPosition.y - lower.anchoredPosition.y < gap;

    private static void SetY(RectTransform rt, float y) =>
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);

    /// <summary>Response across the full X range with only the X condition varying.</summary>
    private List<Vector2> SampleCurve(PlantProcessSimulator sim, PlantProcessSimulator.ProcessInputs inputs)
    {
        var curve = new List<Vector2>(TrendSamples);
        for (int j = 0; j < TrendSamples; j++)
        {
            float x = Mathf.Lerp(XMin, XMax, j / (float)(TrendSamples - 1));
            SetInput(ref inputs, XInput, x);
            curve.Add(new Vector2(x, YSelector(sim.Simulate(inputs))));
        }
        return curve;
    }

    private List<Vector2> ToAnchoredCurve(List<Vector2> data)
    {
        var anchored = new List<Vector2>(data.Count);
        foreach (Vector2 v in data) anchored.Add(ToAnchored(v.x, v.y));
        return anchored;
    }

    private bool SameCurve(List<Vector2> a, List<Vector2> b)
    {
        float tolerance = Mathf.Max(1e-3f, (YMax - YMin) * 0.001f);
        for (int i = 0; i < a.Count; i++)
            if (Mathf.Abs(a[i].y - b[i].y) > tolerance) return false;
        return true;
    }

    /// <summary>
    /// Labels a curve at its highest point inside the plot. Curves are furthest apart
    /// there (peaks for the temperature bell, the high end for monotonic responses), while
    /// they tend to converge at the ends, where labels would stack.
    /// </summary>
    private void PlaceCurveLabel(Text label, List<Vector2> curve, string text, Color color)
    {
        Rect r = plotArea.rect;
        int at = -1;
        for (int i = 0; i < curve.Count; i++)
        {
            if (!r.Contains(curve[i])) continue;
            if (at < 0 || curve[i].y >= curve[at].y - 0.5f) at = i;
        }
        if (at < 0)
        {
            label.gameObject.SetActive(false);
            return;
        }
        PlaceLabel(label, curve[at] + new Vector2(-4f, 3f), false, text, color);
    }

    /// <summary>Positions a label inside the plot; <paramref name="leftAligned"/> puts the
    /// text to the right of <paramref name="at"/>, otherwise it ends there.</summary>
    private void PlaceLabel(Text label, Vector2 at, bool leftAligned, string text, Color color)
    {
        Rect r = plotArea.rect;
        label.text = text;
        label.color = color;
        label.gameObject.SetActive(true);
        RectTransform lr = label.rectTransform;
        lr.sizeDelta = new Vector2(Mathf.Ceil(label.preferredWidth) + 2f, 14f);
        float right = leftAligned ? at.x + lr.sizeDelta.x : at.x;
        float x = Mathf.Clamp(right, r.xMin + lr.sizeDelta.x, r.xMax - 2f);
        float y = Mathf.Clamp(at.y, r.yMin + 2f, r.yMax - 16f);
        lr.anchoredPosition = new Vector2(x, y);
    }

    private void HideTrendCurves()
    {
        foreach (UIGraphLine line in familyLines) line.gameObject.SetActive(false);
        foreach (Text label in familyLabels) label.gameObject.SetActive(false);
        if (tracedLine != null) tracedLine.gameObject.SetActive(false);
        if (tracedLabel != null) tracedLabel.gameObject.SetActive(false);
    }

    private UIGraphLine FamilyLine(int k)
    {
        while (familyLines.Count <= k)
        {
            familyLines.Add(NewTrendLine("Constant " + FamilyName + " " + familyLines.Count, 1.5f));
            familyLabels.Add(NewTrendLabel("Constant " + FamilyName + " Label " + familyLabels.Count, W.SemiBold));
        }
        return familyLines[k];
    }

    private UIGraphLine TracedLine()
    {
        if (tracedLine == null)
        {
            tracedLine = NewTrendLine("Traced Curve", 2.4f);
            tracedLine.DashLength = 7f;
            tracedLine.GapLength = 5f;
        }
        return tracedLine;
    }

    private Text TracedLabel()
    {
        if (tracedLabel == null) tracedLabel = NewTrendLabel("Traced Curve Label", W.ExtraBold);
        return tracedLabel;
    }

    private UIGraphLine NewTrendLine(string name, float thickness)
    {
        RectTransform rt = UITheme.NewRect(name, trendLayer);
        UITheme.Fill(rt);
        UIGraphLine line = rt.gameObject.AddComponent<UIGraphLine>();
        line.Thickness = thickness;
        line.raycastTarget = false;
        return line;
    }

    private Text NewTrendLabel(string name, W weight)
    {
        Text label = UITheme.Label(name, trendLayer, "", 10.5f, weight, UITheme.Subtle, TextAnchor.LowerRight);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.pivot = new Vector2(1f, 0f);
        return label;
    }

    private string FormatFamily(float value) =>
        FamilyInput == ReactorInput.Ratio ? value.ToString("0.#") : GraphVisualUtils.FormatValue(value, FamilyUnit, "0");

    private bool IsShown()
    {
        if (!isActiveAndEnabled) return false;
        GetComponentsInParent(false, parentGroups);
        foreach (CanvasGroup group in parentGroups)
            if (group.alpha < 0.01f) return false;
        return true;
    }

    /// <summary>Same operating point for the curves, ignoring the tank inventory, which
    /// changes every frame but does not affect yield or efficiency.</summary>
    private static bool SameOperatingPoint(PlantProcessSimulator.ProcessInputs a, PlantProcessSimulator.ProcessInputs b)
    {
        a.storedMethanolKg = 0f;
        b.storedMethanolKg = 0f;
        return a.Equals(b);
    }

    public static float GetInput(PlantProcessSimulator.ProcessInputs inputs, ReactorInput which)
    {
        switch (which)
        {
            case ReactorInput.Temperature: return inputs.temperature;
            case ReactorInput.Pressure: return inputs.pressure;
            case ReactorInput.Ratio: return inputs.ratio;
            case ReactorInput.Ghsv: return inputs.ghsv;
            default: return inputs.reactorFeedFlow;
        }
    }

    public static void SetInput(ref PlantProcessSimulator.ProcessInputs inputs, ReactorInput which, float value)
    {
        switch (which)
        {
            case ReactorInput.Temperature: inputs.temperature = value; break;
            case ReactorInput.Pressure: inputs.pressure = value; break;
            case ReactorInput.Ratio: inputs.ratio = value; break;
            case ReactorInput.Ghsv: inputs.ghsv = value; break;
            default: inputs.reactorFeedFlow = value; break;
        }
    }

    private void BuildSelectionRing()
    {
        Image ring = UITheme.Dot("Selection Ring", plotArea, 36f, UITheme.WithAlpha(UITheme.Accent, 0.35f));
        selectionRing = ring.rectTransform;
        selectionRing.anchorMin = selectionRing.anchorMax = new Vector2(0.5f, 0.5f);
        selectionRing.pivot = new Vector2(0.5f, 0.5f);
        selectionRing.SetSiblingIndex(pointsLayer.GetSiblingIndex());
        selectionRing.gameObject.SetActive(false);
    }

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.Hide();
        if (hoverRing != null) hoverRing.gameObject.SetActive(false);
    }

    private void BuildHoverRing()
    {
        Image ring = UITheme.Dot("Hover Ring", plotArea, 34f, UITheme.WithAlpha(UITheme.Accent, 0.22f));
        hoverRing = ring.rectTransform;
        hoverRing.anchorMin = hoverRing.anchorMax = new Vector2(0.5f, 0.5f);
        hoverRing.pivot = new Vector2(0.5f, 0.5f);
        hoverRing.gameObject.SetActive(false);
    }
}
