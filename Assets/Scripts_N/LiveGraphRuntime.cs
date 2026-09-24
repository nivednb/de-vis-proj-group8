using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using W = UITheme.Weight;

/// <summary>
/// A single self-sampling live strip-chart (ECG-style) rendered with plain uGUI elements
/// (no external charting package). X is always elapsed time — the trail continuously
/// scrolls forward as new samples arrive, exactly like a heart monitor, and NEVER moves
/// backward. Every sample since the last reset is kept (nothing is trimmed), so a
/// Scrollbar lets the user drag back through the full history; dragging back to the right
/// edge resumes live auto-follow. The visible window is drawn as a smooth Catmull-Rom
/// curve with round markers only at points where the user actively changed the relevant
/// slider, plus a hover readout available at every recorded sample.
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
    private const int CurveSubdivisions = 14;
    private const float HoverRadiusPixels = 16f;
    private const float DotGrowSeconds = 0.3f;
    private const float ChangeMarkerWindowSeconds = 1.2f;
    private const int TickCount = 5;
    private const float LegendWidth = 172f;

    public string Title;
    public string XLabel; // e.g. "Reactor temp" — shown in the hover tooltip, not as an axis
    public string XUnit = "";
    public string YLabel; // carries its own unit in parens, e.g. "Overall efficiency (%)"
    public string SecondaryLabel; // e.g. "Tank" — only shown in the hover tooltip when set
    public string SecondaryUnit = "";
    public Func<PlantProcessSimulator.ProcessSnapshot, float> XSelector;
    public Func<PlantProcessSimulator.ProcessSnapshot, float> YSelector;
    public Func<PlantProcessSimulator.ProcessSnapshot, float> SecondarySelector;
    public Font LabelFont;
    public Color LineColor = UITheme.Accent;
    public Color PointColor = UITheme.Accent;

    // When true, the area between the curve and the Y=0 baseline is filled — used for the
    // methanol-output mode to visually read as "the tank filling up" alongside the line.
    public bool ShadeArea;
    public Color ShadeColor = UITheme.WithAlpha(UITheme.Accent, 0.12f);

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
    private Text[] xTicks;
    private Text[] yTicks;
    private UIGraphTooltip tooltip;
    private RectTransform selfRect;
    private RectTransform hoverDot;
    private RectTransform shadeHighlightBar;
    private Scrollbar scrollbar;
    private float nextSampleTime;
    private RectTransform newestDot;
    private float newestDotSpawnTime;
    private PlantProcessSimulator subscribedSimulator;
    private UIGraphLine lineGraphic;
    private UIGraphFill fillGraphic;
    private readonly List<Vector2> curveBuffer = new List<Vector2>();
    private float lastPlotWidth = -1f;

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

        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.SetParent(container, false);
        UITheme.Fill(root);
        selfRect = root;
        gameObject.AddComponent<UIRaycastTarget>();

        Text titleText = UITheme.Label("Title", root, Title, 15f, W.ExtraBold, UITheme.Ink);
        UITheme.TopBand(titleText.rectTransform, 0f, 0f, LegendWidth + 20f, 20f);
        Text subtitle = UITheme.Label("Subtitle", root, "Last 30 s of the run. Drag the bar below the chart to look back; markers show slider changes.", 12.5f, W.Medium, UITheme.Subtle);
        UITheme.TopBand(subtitle.rectTransform, 0f, 22f, LegendWidth + 20f, 18f);

        RectTransform legend = UIGraphKit.ModuleLegend(root, "Colour = module changed", false, LegendWidth);
        UITheme.TopRight(legend, 0f, 56f, LegendWidth, legend.sizeDelta.y);

        GameObject plotObject = new GameObject("Plot Area", typeof(RectTransform));
        plotArea = plotObject.GetComponent<RectTransform>();
        plotArea.SetParent(root, false);
        // Extra bottom margin leaves room for the tick labels and the history scrollbar.
        UITheme.Fill(plotArea, 70f, 62f, LegendWidth + 28f, 58f);

        UIGraphKit.PlotBackground(plotArea);
        UIGraphKit.HorizontalGrid(plotArea, TickCount - 1);
        yTicks = UIGraphKit.YTicks(root, plotArea, TickCount, 12f);
        xTicks = UIGraphKit.XTicks(plotArea, 2, 8f);
        UIGraphKit.RotatedYTitle(root, plotArea, YLabel);

        GameObject fillObject = new GameObject("Fill Mesh", typeof(RectTransform));
        fillObject.transform.SetParent(plotArea, false);
        UITheme.Fill((RectTransform)fillObject.transform);
        fillGraphic = fillObject.AddComponent<UIGraphFill>();
        fillGraphic.color = ShadeColor;
        fillGraphic.raycastTarget = false;

        GameObject lineObject = new GameObject("Line Mesh", typeof(RectTransform));
        lineObject.transform.SetParent(plotArea, false);
        UITheme.Fill((RectTransform)lineObject.transform);
        lineGraphic = lineObject.AddComponent<UIGraphLine>();
        lineGraphic.color = LineColor;
        lineGraphic.Thickness = 2.6f;
        lineGraphic.raycastTarget = false;

        BuildShadeHighlight();

        pointsLayer = new GameObject("Points", typeof(RectTransform)).GetComponent<RectTransform>();
        pointsLayer.SetParent(plotArea, false);
        UITheme.Fill(pointsLayer);

        BuildHoverDot();
        BuildScrollbar(root);
        tooltip = UIGraphTooltip.Create(root);

        for (int i = 0; i < TickCount; i++)
            yTicks[i].text = UIGraphKit.FormatTick(Mathf.Lerp(YMin, YMax, i / (float)(TickCount - 1)));

        nextSampleTime = 0f; // sample immediately on first Update
    }

    /// <summary>
    /// A soft vertical band from the baseline up to the curve at whichever X the cursor is
    /// over, shown only for shaded graphs (see ShadeArea) — parented directly under plotArea
    /// so it survives redraws and lets hovering anywhere across the filled area pick out that
    /// moment's cumulative value.
    /// </summary>
    private void BuildShadeHighlight()
    {
        Image bar = UITheme.Panel("Shade Highlight", plotArea, UITheme.WithAlpha(UITheme.Accent, 0.16f), 3f);
        shadeHighlightBar = bar.rectTransform;
        shadeHighlightBar.anchorMin = shadeHighlightBar.anchorMax = new Vector2(0.5f, 0.5f);
        shadeHighlightBar.pivot = new Vector2(0.5f, 0f);
        shadeHighlightBar.gameObject.SetActive(false);
    }

    /// <summary>
    /// A single persistent highlight marker that snaps to whichever sample is nearest the
    /// cursor. This is what makes hover work — and visually "highlight" — everywhere along
    /// the curve, not only at the sparse manual-change/newest markers.
    /// </summary>
    private void BuildHoverDot()
    {
        RectTransform dot = UITheme.NewRect("Hover Dot", plotArea);
        dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
        dot.pivot = new Vector2(0.5f, 0.5f);
        dot.sizeDelta = new Vector2(16f, 16f);
        Image halo = UITheme.Dot("Halo", dot, 28f, UITheme.WithAlpha(UITheme.Accent, 0.2f));
        UITheme.Center(halo.rectTransform, 28f, 28f);
        Image ring = UITheme.Dot("Ring", dot, 14f, Color.white);
        UITheme.Center(ring.rectTransform, 14f, 14f);
        Image core = UITheme.Dot("Core", dot, 9f, UITheme.Accent);
        UITheme.Center(core.rectTransform, 9f, 9f);
        hoverDot = dot;
        hoverDot.gameObject.SetActive(false);
    }

    private void BuildScrollbar(RectTransform root)
    {
        Image track = UITheme.Panel("Scrollbar", root, UITheme.Sunken, 4f, true);
        RectTransform sbRect = track.rectTransform;
        sbRect.anchorMin = Vector2.zero;
        sbRect.anchorMax = new Vector2(1f, 0f);
        sbRect.pivot = new Vector2(0.5f, 0f);
        sbRect.offsetMin = new Vector2(70f, 4f);
        sbRect.offsetMax = new Vector2(-(LegendWidth + 28f), 12f);

        RectTransform slidingArea = UITheme.NewRect("Sliding Area", sbRect);
        UITheme.Fill(slidingArea);

        Image handle = UITheme.Panel("Handle", slidingArea, UITheme.LineStrong, 4f, true);
        UITheme.Fill(handle.rectTransform);

        scrollbar = sbRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = scrollbar.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.8f, 0.85f, 0.95f, 1f);
        colors.pressedColor = new Color(0.6f, 0.7f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        scrollbar.colors = colors;
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.size = 1f;
        scrollbar.value = 1f;
        scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
        handle.canvasRenderer.SetColor(Color.white);
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

        if (plotArea != null && Mathf.Abs(plotArea.rect.width - lastPlotWidth) > 0.5f) RebuildVisible();

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
        if (plotArea == null) return;
        lastPlotWidth = plotArea.rect.width;
        // Only the sparse marker dots are GameObjects; the line and fill are persistent
        // meshes updated in place further down.
        for (int i = pointsLayer.childCount - 1; i >= 0; i--) Destroy(pointsLayer.GetChild(i).gameObject);
        pointScreenPositions.Clear();
        visiblePointSamples.Clear();
        newestDot = null;

        if (samples.Count == 0)
        {
            xTicks[0].text = $"-{WindowSeconds:F0} s";
            xTicks[1].text = "now";
            if (scrollbar != null) scrollbar.size = 1f;
            if (lineGraphic != null) lineGraphic.ClearPoints();
            if (fillGraphic != null) fillGraphic.ClearCurve();
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
        xTicks[0].text = FormatRelative(viewportStart - latestTime);
        xTicks[1].text = FormatRelative(viewportEnd - latestTime);

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

        // One smooth Catmull-Rom curve fed into a single persistent line mesh (and, for the
        // shaded modes, a single fill mesh) — updated in place rather than rebuilt from
        // GameObjects, which is what keeps scrolling smooth.
        GraphCurve.CatmullRom(controlPoints, CurveSubdivisions, curveBuffer);
        if (lineGraphic != null) lineGraphic.SetPoints(curveBuffer);
        if (fillGraphic != null)
        {
            if (ShadeArea && curveBuffer.Count >= 2) fillGraphic.SetCurve(curveBuffer, plotRect.yMin);
            else fillGraphic.ClearCurve();
        }

        // Every sample in view is hoverable, but only manual-change samples (colour-coded by
        // module) and the newest sample get a visible round marker.
        int last = controlPoints.Count - 1;
        for (int i = 0; i <= last; i++)
        {
            pointScreenPositions.Add(controlPoints[i]);
            visiblePointSamples.Add(controlSamples[i]);

            bool isNewest = i == last;
            bool changed = !string.IsNullOrEmpty(controlSamples[i].ChangeModule);
            if (!changed && !isNewest) continue;

            float size = changed ? 11f : 9f;
            Color markerColor = changed ? GraphVisualUtils.GetModuleColor(controlSamples[i].ChangeModule) : PointColor;
            RectTransform dot = UITheme.NewRect("Point " + i, pointsLayer);
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(size, size);
            dot.anchoredPosition = controlPoints[i];
            Image ring = UITheme.Dot("Ring", dot, size + 4f, Color.white);
            UITheme.Center(ring.rectTransform, size + 4f, size + 4f);
            Image fill = UITheme.Dot("Fill", dot, size, markerColor);
            UITheme.Center(fill.rectTransform, size, size);

            if (isNewest)
            {
                newestDot = dot;
                newestDotSpawnTime = Time.unscaledTime;
                dot.localScale = new Vector3(0.25f, 0.25f, 1f);
            }
        }
    }

    private static string FormatRelative(float secondsFromNow)
    {
        return secondsFromNow >= -0.05f ? "now" : $"-{Mathf.Abs(secondsFromNow):F0} s";
    }

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
        Rect plotRect = plotArea.rect;
        if (ShadeArea && local.x >= plotRect.xMin && local.x <= plotRect.xMax)
        {
            // Shaded graphs respond to hovering anywhere across the filled area (not just
            // near the line itself) — nearest sample by X alone.
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
        string yUnit = UnitFromLabel(YLabel);
        string yName = LabelWithoutUnit(YLabel);
        string xLine = !string.IsNullOrEmpty(XLabel) ? $"\n{XLabel}: {GraphVisualUtils.FormatValue(s.X, XUnit, "0.#")}" : "";
        string secondaryLine = !string.IsNullOrEmpty(SecondaryLabel) ? $"\n{SecondaryLabel}: {GraphVisualUtils.FormatValue(s.Secondary, SecondaryUnit, "0")}" : "";
        string cu = GraphVisualUtils.GetParameterUnit(s.ChangeParameter);
        string changeLine = !string.IsNullOrEmpty(s.ChangeModule)
            ? $"\n<color=#94A3B8>{UITheme.Pretty(s.ChangeParameter)}: {GraphVisualUtils.FormatValue(s.ChangeFromValue, cu)} → {GraphVisualUtils.FormatValue(s.ChangeToValue, cu)} · {UIGraphKit.ModuleDisplayName(s.ChangeModule)}</color>"
            : "";
        string content = $"<b>{yName}: {GraphVisualUtils.FormatValue(s.Y, yUnit, "0.#")}</b>{xLine}{secondaryLine}\n<color=#94A3B8>{secondsAgo:F0} s ago</color>" + changeLine;

        if (hoverDot != null)
        {
            hoverDot.anchoredPosition = pointScreenPositions[nearest];
            hoverDot.gameObject.SetActive(true);
            hoverDot.SetAsLastSibling();
        }

        if (shadeHighlightBar != null)
        {
            if (ShadeArea)
            {
                float curveY = pointScreenPositions[nearest].y;
                shadeHighlightBar.sizeDelta = new Vector2(8f, Mathf.Max(0f, curveY - plotRect.yMin));
                shadeHighlightBar.anchoredPosition = new Vector2(pointScreenPositions[nearest].x, plotRect.yMin);
                shadeHighlightBar.gameObject.SetActive(true);
            }
            else
            {
                shadeHighlightBar.gameObject.SetActive(false);
            }
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, eventData.position, cam, out Vector2 tooltipLocal);
        tooltip.Show(content, tooltipLocal);
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.Hide();
        if (hoverDot != null) hoverDot.gameObject.SetActive(false);
        if (shadeHighlightBar != null) shadeHighlightBar.gameObject.SetActive(false);
    }
}
