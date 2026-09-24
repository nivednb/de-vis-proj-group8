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
/// </summary>
public sealed class CorrelationGraphRuntime : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    private struct Point
    {
        public float X;
        public float Y;
        public string Module; // empty for the seed point (no change caused it)
        public string Parameter;
        public float FromValue;
        public float ToValue;
    }

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
        Text subtitle = UITheme.Label("Subtitle", root, "Each point is one committed change, numbered in the order it was made.", 12.5f, W.Medium, UITheme.Subtle);
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
        points.Add(new Point { X = XSelector(snapshot), Y = YSelector(snapshot), Module = null });
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
            ToValue = info.ToValue
        });
        Redraw();
    }

    /// <summary>Clears back to a single fresh seed point matching the just-reset state —
    /// the graph restarts exactly like a brand-new session.</summary>
    private void HandleReset()
    {
        points.Clear();
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim != null) AddSeedPoint(sim);
        else Redraw();
    }

    private Vector2 ToAnchored(Point p)
    {
        Rect plotRect = plotArea.rect;
        float nx = Mathf.InverseLerp(viewXMin, viewXMax, p.X);
        float ny = Mathf.InverseLerp(viewYMin, viewYMax, p.Y);
        return new Vector2(plotRect.xMin + nx * plotRect.width, plotRect.yMin + ny * plotRect.height);
    }

    private void Redraw()
    {
        if (pointsLayer == null) return;
        lastPlotWidth = plotArea.rect.width;
        for (int i = pointsLayer.childCount - 1; i >= 0; i--) Destroy(pointsLayer.GetChild(i).gameObject);
        pointScreenPositions.Clear();
        newestDot = null;

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
