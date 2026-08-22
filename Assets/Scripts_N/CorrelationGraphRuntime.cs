using System;
using System.Collections.Generic;
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
    private Text emptyHint;
    private RectTransform tooltipRect;
    private RectTransform hoverDot;
    private PlantProcessSimulator subscribedSimulator;

    public void Initialize(RectTransform container, Canvas canvas, Font font)
    {
        ownerCanvas = canvas;
        LabelFont = font;

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
        StretchWithOffset(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -30f), new Vector2(-14f, -6f));

        Text yAxisNameLabel = MakeText("Y Axis Name", root, YLabel, 10, FontStyle.Normal, TextAnchor.MiddleCenter, AxisColor);
        yAxisNameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        yAxisNameLabel.rectTransform.anchorMin = yAxisNameLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        yAxisNameLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        yAxisNameLabel.rectTransform.anchoredPosition = new Vector2(12f, 8f);
        yAxisNameLabel.rectTransform.sizeDelta = new Vector2(220f, 16f);
        yAxisNameLabel.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

        Text xAxisNameLabel = MakeText("X Axis Name", root, XLabel, 10, FontStyle.Normal, TextAnchor.MiddleCenter, AxisColor);
        StretchWithOffset(xAxisNameLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(58f, 4f), new Vector2(-8f, 18f));

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

        pointsLayer = new GameObject("Points", typeof(RectTransform)).GetComponent<RectTransform>();
        pointsLayer.SetParent(plotArea, false);
        Stretch(pointsLayer);

        emptyHint = MakeText("Empty Guidance", plotArea,
            "STARTING OPERATING POINT\nChange one process control to add a comparison point.",
            12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.65f, 0.75f, 0.80f, 0.85f));
        StretchWithOffset(emptyHint.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(80f, 80f), new Vector2(-80f, -80f));

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
        float nx = Mathf.InverseLerp(XMin, XMax, p.X);
        float ny = Mathf.InverseLerp(YMin, YMax, p.Y);
        return new Vector2(plotRect.xMin + nx * plotRect.width, plotRect.yMin + ny * plotRect.height);
    }

    private void Redraw()
    {
        if (emptyHint != null) emptyHint.gameObject.SetActive(points.Count <= 1);
        for (int i = linesLayer.childCount - 1; i >= 0; i--) Destroy(linesLayer.GetChild(i).gameObject);
        for (int i = pointsLayer.childCount - 1; i >= 0; i--) Destroy(pointsLayer.GetChild(i).gameObject);
        pointScreenPositions.Clear();
        newestDot = null;

        if (points.Count == 0) return;

        var anchored = new List<Vector2>(points.Count);
        foreach (Point p in points) anchored.Add(ToAnchored(p));

        for (int i = 1; i < anchored.Count; i++)
        {
            CreateLineSegment(anchored[i - 1], anchored[i], LineColor);
        }

        int last = anchored.Count - 1;
        for (int i = 0; i <= last; i++)
        {
            pointScreenPositions.Add(anchored[i]);

            bool isSeed = string.IsNullOrEmpty(points[i].Module);
            bool isNewest = i == last;
            Color color = isSeed ? SeedPointColor : GraphVisualUtils.GetModuleColor(points[i].Module);
            float size = isSeed ? 8f : 9f;

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
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(1f, -1f);
            }

            if (isNewest)
            {
                newestDot = dot;
                newestDotSpawnTime = Time.unscaledTime;
                dot.localScale = new Vector3(0.3f, 0.3f, 1f);
            }
        }
    }

    private void CreateLineSegment(Vector2 from, Vector2 to, Color color)
    {
        RectTransform line = MakePanel("Segment", linesLayer, color);
        line.GetComponent<Image>().raycastTarget = false;
        line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
        line.pivot = new Vector2(0f, 0.5f);
        Vector2 delta = to - from;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        line.sizeDelta = new Vector2(length + 1f, 3f);
        line.anchoredPosition = from;
        line.localRotation = Quaternion.Euler(0f, 0f, angle);
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
        string changeLine = !string.IsNullOrEmpty(p.Module)
            ? $"\n{p.Parameter} changed from {p.FromValue:F1} to {p.ToValue:F1} in {p.Module}"
            : "\n(starting point)";
        tooltipText.text = $"{XLabel}: {p.X:F1}\n{YLabel}: {p.Y:F1}" + changeLine;
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
