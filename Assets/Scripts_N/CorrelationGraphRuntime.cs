using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A static entity-vs-entity graph — X is the changing quantity (e.g. reactor temperature),
/// Y is the dependent quantity (e.g. efficiency), NOT time. It starts with a single seed
/// point at the plant's current state, then gets exactly one new point appended per
/// committed manual slider change (any module, not just the one this graph nominally
/// correlates), each connected to the previous point by a straight line. The result grows
/// and reshapes as the user makes more changes, rather than continuously scrolling — this
/// is a record of "what happened as a result of each change", not a live trend line.
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

    public string Title;
    public string XLabel;
    public string YLabel;
    public Func<PlantProcessSimulator.ProcessSnapshot, float> XSelector;
    public Func<PlantProcessSimulator.ProcessSnapshot, float> YSelector;
    public Font LabelFont;
    public Color LineColor = new Color(0.08f, 0.62f, 0.9f, 1f);
    public Color SeedPointColor = new Color(0.86f, 0.82f, 0.95f, 1f);
    public Color AxisColor = new Color(0.7f, 0.75f, 0.8f, 1f);
    public Color AxisNameColor = new Color(0.86f, 0.92f, 0.98f, 1f);

    // Fixed domain on both axes — the actual slider range for X, the real ceiling for Y —
    // so the scale never jumps as points are added; a point's position always reads as
    // "% of the real operating range".
    public float XMin = 0f;
    public float XMax = 1f;
    public float YMin = 0f;
    public float YMax = 1f;

    private readonly List<Point> points = new List<Point>();
    private readonly List<Vector2> pointScreenPositions = new List<Vector2>();

    private Canvas ownerCanvas;
    private RectTransform selfRect;
    private RectTransform plotArea;
    private RectTransform linesLayer;
    private RectTransform pointsLayer;
    private Text xAxisMinLabel;
    private Text xAxisMaxLabel;
    private Text yAxisMinLabel;
    private Text yAxisMaxLabel;
    private RectTransform newestDot;
    private float newestDotSpawnTime;
    private GameObject tooltip;
    private Text tooltipText;
    private RectTransform tooltipRect;
    private RectTransform hoverDot;
    private PlantProcessSimulator subscribedSimulator;
    private UIGraphLine lineGraphic;
    private Button exportButton;

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

        Image background = gameObject.GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.07f, 0.001f);
        background.raycastTarget = true;

        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.SetParent(container, false);
        Stretch(root);
        selfRect = root;

        Text titleText = MakeText("Title", root, Title, 14, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        StretchWithOffset(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -30f), new Vector2(-88f, -6f));

        Text yAxisNameLabel = MakeText("Y Axis Name", root, "Y:  " + YLabel, 11, FontStyle.Bold, TextAnchor.MiddleCenter, AxisNameColor);
        yAxisNameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        yAxisNameLabel.rectTransform.anchorMin = yAxisNameLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        yAxisNameLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        yAxisNameLabel.rectTransform.anchoredPosition = new Vector2(11f, 8f);
        yAxisNameLabel.rectTransform.sizeDelta = new Vector2(240f, 16f);
        yAxisNameLabel.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

        Text xAxisNameLabel = MakeText("X Axis Name", root, "X:  " + XLabel, 11, FontStyle.Bold, TextAnchor.MiddleCenter, AxisNameColor);
        StretchWithOffset(xAxisNameLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(58f, 2f), new Vector2(-8f, 18f));

        GameObject plotObject = new GameObject("Plot Area", typeof(RectTransform));
        plotArea = plotObject.GetComponent<RectTransform>();
        plotArea.SetParent(root, false);
        StretchWithOffset(plotArea, Vector2.zero, Vector2.one, new Vector2(58f, 20f), new Vector2(-14f, -34f));

        RectTransform yAxisLine = MakePanel("Y Axis Line", plotArea, AxisColor);
        StretchWithOffset(yAxisLine, Vector2.zero, new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f));
        RectTransform xAxisLine = MakePanel("X Axis Line", plotArea, AxisColor);
        StretchWithOffset(xAxisLine, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f));

        linesLayer = new GameObject("Lines", typeof(RectTransform)).GetComponent<RectTransform>();
        linesLayer.SetParent(plotArea, false);
        Stretch(linesLayer);

        GameObject lineObject = new GameObject("Line Mesh", typeof(RectTransform));
        lineObject.transform.SetParent(linesLayer, false);
        Stretch(lineObject.GetComponent<RectTransform>());
        lineGraphic = lineObject.AddComponent<UIGraphLine>();
        lineGraphic.color = LineColor;
        lineGraphic.Thickness = 2.4f;
        lineGraphic.raycastTarget = false;

        pointsLayer = new GameObject("Points", typeof(RectTransform)).GetComponent<RectTransform>();
        pointsLayer.SetParent(plotArea, false);
        Stretch(pointsLayer);

        BuildLegend(root);

        yAxisMaxLabel = MakeText("Y Max", root, YMax.ToString("F0"), 10, FontStyle.Normal, TextAnchor.UpperRight, AxisColor);
        AnchorTopLeft(yAxisMaxLabel.rectTransform, new Vector2(0f, -20f), new Vector2(52f, 16f));
        yAxisMinLabel = MakeText("Y Min", root, YMin.ToString("F0"), 10, FontStyle.Normal, TextAnchor.LowerRight, AxisColor);
        AnchorBottomLeft(yAxisMinLabel.rectTransform, new Vector2(0f, 20f), new Vector2(52f, 16f));

        xAxisMinLabel = MakeText("X Min", root, XMin.ToString("F0"), 10, FontStyle.Normal, TextAnchor.LowerLeft, AxisColor);
        AnchorBottomLeft(xAxisMinLabel.rectTransform, new Vector2(58f, 4f), new Vector2(70f, 16f));
        xAxisMaxLabel = MakeText("X Max", root, XMax.ToString("F0"), 10, FontStyle.Normal, TextAnchor.LowerRight, AxisColor);
        AnchorBottomRight(xAxisMaxLabel.rectTransform, new Vector2(-14f, 4f), new Vector2(70f, 16f));

        BuildHoverDot();
        BuildTooltip(root);
        BuildExportButton(root);
    }

    private void BuildExportButton(RectTransform root)
    {
        GameObject go = new GameObject("Export Button", typeof(RectTransform));
        go.transform.SetParent(root, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-12f, -6f);
        rect.sizeDelta = new Vector2(68f, 20f);
        go.AddComponent<Image>().color = new Color(0.10f, 0.34f, 0.48f, 1f);
        exportButton = go.AddComponent<Button>();
        Text label = MakeText("Label", go.transform, "EXPORT", 10, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Stretch(label.rectTransform);
        exportButton.onClick.AddListener(ExportNow);
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
                i.ToString(),
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
                ? $"Exported  {baseName}.csv + .png   →   {GraphExportUtil.ExportDirectory}"
                : (csvPath != null
                    ? $"Exported  {baseName}.csv  (PNG failed)   →   {GraphExportUtil.ExportDirectory}"
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

        if (newestDot != null)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - newestDotSpawnTime) / DotGrowSeconds);
            float scale = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, t));
            newestDot.localScale = new Vector3(scale, scale, 1f);
        }
    }

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
            // Small at rest so dense clusters stay legible — the hover dot (see BuildHoverDot)
            // is what visibly "pops" a point when the cursor is near it.
            float size = isSeed ? 4.5f : 5.5f;

            RectTransform dot = MakePanel("Point " + i, pointsLayer, color);
            Image dotImage = dot.GetComponent<Image>();
            dotImage.sprite = GraphVisualUtils.GetCircleSprite();
            dotImage.type = Image.Type.Simple;
            dotImage.raycastTarget = false;
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(size, size);
            dot.anchoredPosition = anchored[i];
            if (!isSeed)
            {
                Outline outline = dot.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 1f, 1f, 0.55f);
                outline.effectDistance = new Vector2(0.5f, -0.5f);
            }

            if (isNewest)
            {
                newestDot = dot;
                newestDotSpawnTime = Time.unscaledTime;
                dot.localScale = new Vector3(0.3f, 0.3f, 1f);
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
        if (xAxisMinLabel != null) xAxisMinLabel.text = FormatAxis(viewXMin);
        if (xAxisMaxLabel != null) xAxisMaxLabel.text = FormatAxis(viewXMax);
        if (yAxisMinLabel != null) yAxisMinLabel.text = FormatAxis(viewYMin);
        if (yAxisMaxLabel != null) yAxisMaxLabel.text = FormatAxis(viewYMax);
    }

    private static string FormatAxis(float v)
    {
        float a = Mathf.Abs(v);
        if (a >= 100f) return v.ToString("F0");
        if (a >= 10f) return v.ToString("F1");
        return v.ToString("F2");
    }

    // XLabel / YLabel arrive as "Reactor Pressure (bar)" — split the unit out so tooltip
    // values can read "82.3 bar" rather than "Reactor Pressure (bar): 82.3".
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

        int nearest = -1;
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
            ? $"\n{p.Parameter}: {GraphVisualUtils.FormatValue(p.FromValue, cu)} → {GraphVisualUtils.FormatValue(p.ToValue, cu)}  ({p.Module})"
            : "\n(starting point)";
        tooltipText.text = $"{LabelWithoutUnit(XLabel)}: {GraphVisualUtils.FormatValue(p.X, xu, "0.#")}\n{LabelWithoutUnit(YLabel)}: {GraphVisualUtils.FormatValue(p.Y, yu, "0.#")}" + changeLine;
        tooltip.SetActive(true);

        if (hoverDot != null)
        {
            hoverDot.anchoredPosition = pointScreenPositions[nearest];
            hoverDot.gameObject.SetActive(true);
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, eventData.position, cam, out Vector2 tooltipLocal);
        tooltipRect.anchoredPosition = tooltipLocal + new Vector2(14f, 14f);
        tooltip.transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.SetActive(false);
        if (hoverDot != null) hoverDot.gameObject.SetActive(false);
    }

    private void BuildHoverDot()
    {
        hoverDot = MakePanel("Hover Dot", plotArea, new Color(1f, 1f, 1f, 0.95f));
        Image img = hoverDot.GetComponent<Image>();
        img.sprite = GraphVisualUtils.GetCircleSprite();
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        hoverDot.anchorMin = hoverDot.anchorMax = new Vector2(0.5f, 0.5f);
        hoverDot.pivot = new Vector2(0.5f, 0.5f);
        hoverDot.sizeDelta = new Vector2(16f, 16f);
        Outline outline = hoverDot.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.42f, 0.86f, 1f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        hoverDot.gameObject.SetActive(false);
    }

    private void BuildTooltip(RectTransform root)
    {
        tooltip = new GameObject("Tooltip", typeof(RectTransform)).gameObject;
        tooltipRect = tooltip.GetComponent<RectTransform>();
        tooltipRect.SetParent(root, false);
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = Vector2.zero;
        tooltipRect.sizeDelta = new Vector2(200f, 92f);
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

    /// <summary>Compact boxed legend pinned to the top-right corner — identical styling to
    /// LiveGraphRuntime's, sharing the same module color palette.</summary>
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
