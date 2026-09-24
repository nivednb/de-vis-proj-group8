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
/// Guided OFAT experiment recorder.
///
/// Pick a reactor variable (Temperature, Pressure, H2/CO2, GHSV, Feed flow): its module
/// slider stays live and every OTHER reactor slider is locked constant (via
/// InteractiveModulePanelRuntime.SetReactorVariableLock). As you move the one live slider
/// the plant's response (yield / efficiency / methanol) is sampled over time and drawn as a
/// line whose colour is that variable's colour — so the trace is segmented by "which factor
/// was being varied". Each variable switch drops a timestamped marker.
///
/// Module-change dots are still shown (Points mode), exactly like the correlation graphs.
/// The time axis is paginated one minute per page so only ~120 samples are ever rendered.
/// </summary>
public sealed class OfatTimelineGraphRuntime : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    public enum Variable { Free = 0, Temperature = 1, Pressure = 2, H2CO2 = 3, GHSV = 4, FeedFlow = 5 }
    public enum Response { Yield = 0, Efficiency = 1, Methanol = 2 }
    public enum ViewMode { Line = 0, Points = 1 }

    private const float SampleInterval = 0.5f;
    private const float PageSeconds = 60f;
    private const float RebuildInterval = 0.2f;
    private const int MaxRecords = 20000;
    private const float HoverRadiusPixels = 16f;
    private const float LegendWidth = 172f;
    private const int TickCount = 5;

    private struct VarMeta
    {
        public string Label;
        public string SliderParam;
        public Color Color;
        public Func<PlantProcessSimulator.ProcessSnapshot, float> ReadValue;
    }
    private struct RespMeta { public string Label; public string Unit; public float Max; public Func<PlantProcessSimulator.ProcessSnapshot, float> Select; }
    private struct Sample { public float T; public float Y; public Variable Var; public float VarValue; }
    private struct Epoch { public float T; public Variable Var; public float VarValue; }
    private struct ChangePoint { public float T; public float Y; public string Module; public string Parameter; public float From; public float To; }

    private static readonly Dictionary<Variable, VarMeta> Vars = new Dictionary<Variable, VarMeta>
    {
        [Variable.Free]        = new VarMeta { Label = "Free",          SliderParam = null,        Color = UITheme.Hex("64748B"), ReadValue = null },
        [Variable.Temperature] = new VarMeta { Label = "Temperature",   SliderParam = "Temp",      Color = UITheme.Hex("EA580C"), ReadValue = s => s.reactorTemperatureC },
        [Variable.Pressure]    = new VarMeta { Label = "Pressure",      SliderParam = "Pressure",  Color = UITheme.Hex("2563EB"), ReadValue = s => s.reactorPressureBar },
        [Variable.H2CO2]       = new VarMeta { Label = "H₂/CO₂", SliderParam = "H2/CO2", Color = UITheme.Hex("059669"), ReadValue = s => s.h2Co2Ratio },
        [Variable.GHSV]        = new VarMeta { Label = "GHSV",          SliderParam = "GHSV",      Color = UITheme.Hex("7C3AED"), ReadValue = s => s.ghsv },
        [Variable.FeedFlow]    = new VarMeta { Label = "Feed flow",     SliderParam = "Feed flow", Color = UITheme.Hex("CA8A04"), ReadValue = s => s.reactorFeedFlowPercent },
    };

    private static readonly Variable[] VariableOrder = { Variable.Free, Variable.Temperature, Variable.Pressure, Variable.H2CO2, Variable.GHSV, Variable.FeedFlow };

    private static float ReadVarValue(Variable v, PlantProcessSimulator sim)
    {
        if (v == Variable.Free || sim == null || Vars[v].ReadValue == null) return 0f;
        return Vars[v].ReadValue(sim.Current);
    }

    private static string VarUnit(Variable v) => v == Variable.Free ? "" : GraphVisualUtils.GetParameterUnit(Vars[v].SliderParam);

    private RespMeta[] responses;

    private Canvas ownerCanvas;
    private Font labelFont;
    private InteractiveModulePanelRuntime panels;
    private PlantProcessSimulator subscribedSim;

    private Variable currentVar = Variable.Free;
    private Response currentResp = Response.Yield;
    private ViewMode currentMode = ViewMode.Points;
    private bool subTabVisible;

    private readonly List<Sample> samples = new List<Sample>();
    private readonly List<Epoch> epochs = new List<Epoch>();
    private readonly List<ChangePoint> changePoints = new List<ChangePoint>();

    private float clock;
    private float nextSampleTime;
    private int pageIndex;
    private bool followLive = true;
    private float nextRebuild;
    private bool dirty = true;

    private RectTransform selfRect;
    private RectTransform plotArea;
    private RectTransform linesLayer;
    private RectTransform epochLayer;
    private RectTransform pointsLayer;
    private readonly List<UIGraphLine> linePool = new List<UIGraphLine>();
    private readonly List<Vector2> segBuffer = new List<Vector2>();

    private Text titleText;
    private Text contextText;
    private Text pageLabel;
    private Text yAxisNameLabel;
    private Text[] yTicks;
    private Text[] xTicks;
    private Button[] varButtons;
    private Image[] varDots;
    private Button[] respButtons;
    private Button[] modeButtons;
    private Button prevPageButton;
    private Button nextPageButton;
    private Button liveButton;
    private Button exportButton;
    private GameObject moduleLegend;
    private RectTransform hoverDot;
    private UIGraphTooltip tooltip;

    private float viewYMax = 100f;
    private float cachedPageStart;
    private float cachedPageEnd;

    private int LatestPage => Mathf.Max(0, Mathf.FloorToInt(clock / PageSeconds));

    public void Initialize(RectTransform container, Canvas canvas, Font font)
    {
        ownerCanvas = canvas;
        labelFont = font;
        panels = FindFirstObjectByType<InteractiveModulePanelRuntime>();

        float designMethanol = PlantProcessSimulator.Instance != null ? PlantProcessSimulator.Instance.DesignMethanolKgH : 1250f;
        responses = new[]
        {
            new RespMeta { Label = "Reactor yield",      Unit = "%",    Max = 100f,           Select = s => s.reactorYieldPercent },
            new RespMeta { Label = "Overall efficiency", Unit = "%",    Max = 100f,           Select = s => s.overallEfficiencyPercent },
            new RespMeta { Label = "Methanol output",    Unit = "kg/h", Max = designMethanol, Select = s => s.methanolProductionKgH },
        };

        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.SetParent(container, false);
        UITheme.Fill(root);
        selfRect = root;
        gameObject.AddComponent<UIRaycastTarget>();

        BuildVariableRow(root);
        BuildResponseRow(root);

        titleText = UITheme.Label("Title", root, "", 15f, W.ExtraBold, UITheme.Ink);
        UITheme.TopBand(titleText.rectTransform, 0f, 84f, LegendWidth + 20f, 20f);
        contextText = UITheme.Label("Context", root, "", 12.5f, W.Medium, UITheme.Subtle);
        UITheme.TopBand(contextText.rectTransform, 0f, 106f, LegendWidth + 20f, 18f);

        GameObject plotObject = new GameObject("Plot Area", typeof(RectTransform));
        plotArea = plotObject.GetComponent<RectTransform>();
        plotArea.SetParent(root, false);
        UITheme.Fill(plotArea, 70f, 138f, LegendWidth + 28f, 64f);

        UIGraphKit.PlotBackground(plotArea);
        UIGraphKit.HorizontalGrid(plotArea, TickCount - 1);
        yTicks = UIGraphKit.YTicks(root, plotArea, TickCount, 12f);
        xTicks = UIGraphKit.XTicks(plotArea, 3, 8f);
        yAxisNameLabel = UIGraphKit.RotatedYTitle(root, plotArea, "");

        linesLayer = new GameObject("Lines", typeof(RectTransform)).GetComponent<RectTransform>();
        linesLayer.SetParent(plotArea, false);
        UITheme.Fill(linesLayer);
        epochLayer = new GameObject("Epochs", typeof(RectTransform)).GetComponent<RectTransform>();
        epochLayer.SetParent(plotArea, false);
        UITheme.Fill(epochLayer);
        pointsLayer = new GameObject("Points", typeof(RectTransform)).GetComponent<RectTransform>();
        pointsLayer.SetParent(plotArea, false);
        UITheme.Fill(pointsLayer);

        BuildPaginationRow(root);
        BuildLegends(root);
        BuildHoverDot();
        tooltip = UIGraphTooltip.Create(root);

        epochs.Add(new Epoch { T = 0f, Var = currentVar, VarValue = 0f });
        RecolorVarButtons();
        RecolorRespButtons();
        RecolorModeButtons();
        UpdateContext();
        dirty = true;
    }

    // ---- control rows -------------------------------------------------------

    private void BuildVariableRow(RectTransform root)
    {
        RectTransform row = UITheme.NewRect("Variable Row", root);
        UITheme.TopBand(row, 0f, 0f, 0f, 32f);
        Text lbl = UITheme.Label("Vary Label", row, "Vary one", 12.5f, W.ExtraBold, UITheme.Subtle);
        UITheme.TopLeft(lbl.rectTransform, 0f, 0f, 70f, 32f);

        varButtons = new Button[VariableOrder.Length];
        varDots = new Image[VariableOrder.Length];
        float x = 76f;
        for (int i = 0; i < VariableOrder.Length; i++)
        {
            Variable v = VariableOrder[i];
            Button b = UITheme.MakeButton("Var " + v, row, Vars[v].Label, Kind.Chip, 12.5f, null, 8f, false, 16f, W.Bold, 11f);
            // The chips double as the line-colour legend: each carries its variable's colour.
            Image dot = UITheme.Dot("Swatch", b.transform, 8f, Vars[v].Color);
            dot.transform.SetAsFirstSibling();
            LayoutElement le = dot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 8f;
            b.GetComponent<HorizontalLayoutGroup>().spacing = 7f;
            b.Skin().OverrideActive(Vars[v].Color, Color.white);
            float w = UITheme.PreferredWidth(b);
            UITheme.TopLeft((RectTransform)b.transform, x, 0f, w, 32f);
            x += w + 6f;
            b.onClick.AddListener(() => SetVariable(v));
            varButtons[i] = b;
            varDots[i] = dot;
        }

        exportButton = UITheme.MakeButton("Export", row, "Export", Kind.Outline, 13f, Icon.Download, 8f, false, 15f, W.Bold, 12f);
        float ew = UITheme.PreferredWidth(exportButton);
        UITheme.TopRight((RectTransform)exportButton.transform, 0f, 0f, ew, 32f);
        exportButton.onClick.AddListener(ExportNow);
    }

    private void BuildResponseRow(RectTransform root)
    {
        RectTransform row = UITheme.NewRect("Response Row", root);
        UITheme.TopBand(row, 0f, 40f, 0f, 32f);
        Text lbl = UITheme.Label("Show Label", row, "Show", 12.5f, W.ExtraBold, UITheme.Subtle);
        UITheme.TopLeft(lbl.rectTransform, 0f, 0f, 70f, 32f);

        string[] respNames = { "Yield", "Efficiency", "Methanol" };
        respButtons = new Button[respNames.Length];
        float x = 76f;
        for (int i = 0; i < respNames.Length; i++)
        {
            int idx = i;
            Button b = UITheme.MakeButton("Resp " + i, row, respNames[i], Kind.Chip, 12.5f, null, 8f, false, 16f, W.Bold, 12f);
            float w = UITheme.PreferredWidth(b);
            UITheme.TopLeft((RectTransform)b.transform, x, 0f, w, 32f);
            x += w + 6f;
            b.onClick.AddListener(() => SetResponse((Response)idx));
            respButtons[i] = b;
        }

        // Line only / With points segmented control
        Image segment = UITheme.Panel("Mode", row, UITheme.Sunken, 16f, true);
        string[] modeNames = { "Line only", "With points" };
        modeButtons = new Button[modeNames.Length];
        float sx = 3f;
        for (int i = 0; i < modeNames.Length; i++)
        {
            int idx = i;
            Button b = UITheme.MakeButton("Mode " + i, segment.transform, modeNames[i], Kind.Segment, 12.5f, null, 13f, false, 16f, W.ExtraBold, 12f);
            float w = UITheme.PreferredWidth(b);
            UITheme.TopLeft((RectTransform)b.transform, sx, 3f, w, 26f);
            sx += w + 2f;
            b.onClick.AddListener(() => SetMode((ViewMode)idx));
            modeButtons[i] = b;
        }
        UITheme.TopLeft(segment.rectTransform, x + 14f, 0f, sx + 1f, 32f);
    }

    private void BuildPaginationRow(RectTransform root)
    {
        RectTransform row = UITheme.NewRect("Pagination Row", root);
        row.anchorMin = Vector2.zero;
        row.anchorMax = new Vector2(1f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.offsetMin = new Vector2(70f, 0f);
        row.offsetMax = new Vector2(-(LegendWidth + 28f), 30f);

        prevPageButton = UITheme.MakeButton("Prev Page", row, "Previous", Kind.Secondary, 12.5f, Icon.ChevronLeft, 8f, false, 14f, W.Bold, 10f);
        float pw = UITheme.PreferredWidth(prevPageButton);
        UITheme.TopLeft((RectTransform)prevPageButton.transform, 0f, 0f, pw, 30f);
        prevPageButton.onClick.AddListener(PrevPage);

        liveButton = UITheme.MakeButton("Live", row, "Live", Kind.Chip, 12.5f, Icon.Dot, 8f, false, 12f, W.ExtraBold, 11f);
        liveButton.Skin().OverrideActive(UITheme.Accent, Color.white);
        float lw = UITheme.PreferredWidth(liveButton);
        UITheme.TopRight((RectTransform)liveButton.transform, 0f, 0f, lw, 30f);
        liveButton.onClick.AddListener(GoLive);

        nextPageButton = UITheme.MakeButton("Next Page", row, "Next", Kind.Secondary, 12.5f, Icon.ChevronRight, 8f, true, 14f, W.Bold, 10f);
        float nw = UITheme.PreferredWidth(nextPageButton);
        UITheme.TopRight((RectTransform)nextPageButton.transform, lw + 8f, 0f, nw, 30f);
        nextPageButton.onClick.AddListener(NextPage);

        pageLabel = UITheme.Label("Page Label", row, "", 12.5f, W.Bold, UITheme.Muted, TextAnchor.MiddleCenter);
        UITheme.Fill(pageLabel.rectTransform, pw + 8f, 0f, lw + nw + 16f, 0f);
    }

    private void BuildLegends(RectTransform root)
    {
        // The variable chips carry the line colours; the right column explains the points.
        RectTransform legend = UIGraphKit.ModuleLegend(root, "Colour = module changed", false, LegendWidth);
        UITheme.TopRight(legend, 0f, 138f, LegendWidth, legend.sizeDelta.y);
        moduleLegend = legend.gameObject;
    }

    // ---- state changes ----------------------------------------------------

    private void SetVariable(Variable v)
    {
        if (v == currentVar) return;
        currentVar = v;
        epochs.Add(new Epoch { T = clock, Var = v, VarValue = ReadVarValue(v, PlantProcessSimulator.Instance) });
        if (epochs.Count > MaxRecords) epochs.RemoveAt(0);
        ApplyLock();
        RecolorVarButtons();
        UpdateContext();
        dirty = true;
    }

    private void SetResponse(Response r)
    {
        if (r == currentResp) return;
        currentResp = r;
        RecolorRespButtons();
        UpdateContext();
        dirty = true;
    }

    private void SetMode(ViewMode m)
    {
        currentMode = m;
        RecolorModeButtons();
        dirty = true;
    }

    /// <summary>Called by the dashboard when the OFAT sub-tab is shown / hidden. The reactor
    /// slider lock is only in force while this sub-tab is actually visible.</summary>
    public void SetSubTabVisible(bool visible)
    {
        subTabVisible = visible;
        ApplyLock();
        if (visible) dirty = true;
    }

    private void ApplyLock()
    {
        if (panels == null) panels = FindFirstObjectByType<InteractiveModulePanelRuntime>();
        if (panels == null) return;
        if (subTabVisible && currentVar != Variable.Free)
            panels.SetReactorVariableLock(Vars[currentVar].SliderParam);
        else
            panels.ClearReactorVariableLock();
    }

    private void PrevPage()
    {
        pageIndex = Mathf.Max(0, pageIndex - 1);
        followLive = false;
        dirty = true;
    }

    private void NextPage()
    {
        pageIndex = Mathf.Min(LatestPage, pageIndex + 1);
        followLive = pageIndex >= LatestPage;
        dirty = true;
    }

    private void GoLive()
    {
        followLive = true;
        pageIndex = LatestPage;
        dirty = true;
    }

    // ---- lifecycle ------------------------------------------------------

    private void OnDisable()
    {
        if (panels != null) panels.ClearReactorVariableLock();
    }

    private void OnDestroy()
    {
        if (subscribedSim != null)
        {
            subscribedSim.ResetRequested -= HandleReset;
            subscribedSim.ManualChangeCommitted -= HandleChange;
        }
        if (panels != null) panels.ClearReactorVariableLock();
    }

    private void HandleReset()
    {
        samples.Clear();
        changePoints.Clear();
        epochs.Clear();
        clock = 0f;
        nextSampleTime = 0f;
        pageIndex = 0;
        followLive = true;
        currentVar = Variable.Free;
        epochs.Add(new Epoch { T = 0f, Var = currentVar, VarValue = 0f });
        ApplyLock();
        RecolorVarButtons();
        UpdateContext();
        dirty = true;
    }

    private void HandleChange(PlantProcessSimulator.ManualChangeInfo info)
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim == null) return;
        changePoints.Add(new ChangePoint
        {
            T = clock,
            Y = responses[(int)currentResp].Select(sim.Current),
            Module = info.Module,
            Parameter = info.Parameter,
            From = info.FromValue,
            To = info.ToValue,
        });
        if (changePoints.Count > MaxRecords) changePoints.RemoveAt(0);
        dirty = true;
    }

    private void Update()
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim != subscribedSim)
        {
            if (subscribedSim != null)
            {
                subscribedSim.ResetRequested -= HandleReset;
                subscribedSim.ManualChangeCommitted -= HandleChange;
            }
            subscribedSim = sim;
            if (subscribedSim != null)
            {
                subscribedSim.ResetRequested += HandleReset;
                subscribedSim.ManualChangeCommitted += HandleChange;
            }
        }

        bool running = sim != null && sim.IsRunning;
        if (running) clock += Time.unscaledDeltaTime;

        if (running && clock >= nextSampleTime)
        {
            nextSampleTime = clock + SampleInterval;
            samples.Add(new Sample
            {
                T = clock,
                Y = responses[(int)currentResp].Select(sim.Current),
                Var = currentVar,
                VarValue = ReadVarValue(currentVar, sim),
            });
            if (samples.Count > MaxRecords) samples.RemoveAt(0);
            dirty = true;
        }

        if (followLive) pageIndex = LatestPage;

        if (dirty || Time.unscaledTime >= nextRebuild)
        {
            nextRebuild = Time.unscaledTime + RebuildInterval;
            Rebuild();
        }
    }

    // ---- rendering ----------------------------------------------------

    private void Rebuild()
    {
        dirty = false;
        if (plotArea == null || responses == null) return;

        for (int i = pointsLayer.childCount - 1; i >= 0; i--) Destroy(pointsLayer.GetChild(i).gameObject);
        for (int i = epochLayer.childCount - 1; i >= 0; i--) Destroy(epochLayer.GetChild(i).gameObject);

        RespMeta rm = responses[(int)currentResp];
        int latest = LatestPage;
        pageIndex = Mathf.Clamp(pageIndex, 0, latest);
        float pageStart = pageIndex * PageSeconds;
        float pageEnd = pageStart + PageSeconds;
        cachedPageStart = pageStart;
        cachedPageEnd = pageEnd;
        float slack = SampleInterval * 2f;

        float dataMax = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            float ti = samples[i].T;
            if (ti < pageStart - slack || ti > pageEnd + slack) continue;
            if (samples[i].Y > dataMax) dataMax = samples[i].Y;
        }
        viewYMax = Mathf.Clamp(dataMax * 1.15f, rm.Max * 0.2f, rm.Max);
        if (viewYMax <= 1f) viewYMax = rm.Max;

        Rect r = plotArea.rect;

        // colour-segmented line: contiguous runs of equal Var
        int poolUsed = 0;
        segBuffer.Clear();
        Variable runVar = Variable.Free;
        bool haveRun = false;
        for (int i = 0; i < samples.Count; i++)
        {
            Sample s = samples[i];
            if (s.T < pageStart - slack) continue;
            if (s.T > pageEnd + slack) break;
            Vector2 p = new Vector2(MapX(s.T, r, pageStart, pageEnd), MapY(s.Y, r));
            if (!haveRun)
            {
                runVar = s.Var;
                haveRun = true;
                segBuffer.Add(p);
            }
            else if (s.Var == runVar)
            {
                segBuffer.Add(p);
            }
            else
            {
                segBuffer.Add(p);
                FlushRun(ref poolUsed, runVar);
                segBuffer.Clear();
                segBuffer.Add(p);
                runVar = s.Var;
            }
        }
        if (haveRun) FlushRun(ref poolUsed, runVar);
        for (int i = poolUsed; i < linePool.Count; i++) linePool[i].ClearPoints();

        // epoch markers
        foreach (Epoch e in epochs)
        {
            if (e.T < pageStart || e.T > pageEnd) continue;
            float x = MapX(e.T, r, pageStart, pageEnd);
            Image v = UITheme.Panel("Epoch Line", epochLayer, UITheme.WithAlpha(Vars[e.Var].Color, 0.45f));
            RectTransform vr = v.rectTransform;
            vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0.5f);
            vr.pivot = new Vector2(0.5f, 0f);
            vr.sizeDelta = new Vector2(1.5f, r.height);
            vr.anchoredPosition = new Vector2(x, r.yMin);

            string epochText = e.Var == Variable.Free
                ? $"Free · {FormatClock(e.T)}"
                : $"{Vars[e.Var].Label} = {GraphVisualUtils.FormatValue(e.VarValue, VarUnit(e.Var))} · {FormatClock(e.T)}";
            Text lab = UITheme.Label("Epoch Label", epochLayer, epochText, 11f, W.ExtraBold, Vars[e.Var].Color, TextAnchor.LowerLeft);
            RectTransform lr = lab.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0f, 0f);
            lr.anchoredPosition = new Vector2(x + 4f, r.yMin + r.height - 15f);
            lr.sizeDelta = new Vector2(180f, 14f);
        }

        // module change points
        if (currentMode == ViewMode.Points)
        {
            for (int i = 0; i < changePoints.Count; i++)
            {
                ChangePoint cp = changePoints[i];
                if (cp.T < pageStart || cp.T > pageEnd) continue;
                Vector2 p = new Vector2(MapX(cp.T, r, pageStart, pageEnd), MapY(cp.Y, r));
                RectTransform dot = UITheme.NewRect("Point", pointsLayer);
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(10f, 10f);
                dot.anchoredPosition = p;
                Image ring = UITheme.Dot("Ring", dot, 14f, Color.white);
                UITheme.Center(ring.rectTransform, 14f, 14f);
                Image fill = UITheme.Dot("Fill", dot, 10f, GraphVisualUtils.GetModuleColor(cp.Module));
                UITheme.Center(fill.rectTransform, 10f, 10f);
            }
        }

        titleText.text = $"OFAT timeline — {rm.Label.ToLowerInvariant()} over time";
        pageLabel.text = $"Page {pageIndex + 1} of {latest + 1}  ·  {FormatClock(pageStart)} – {FormatClock(pageEnd)}";
        xTicks[0].text = FormatClock(pageStart);
        xTicks[1].text = FormatClock(pageStart + PageSeconds * 0.5f);
        xTicks[2].text = FormatClock(pageEnd);
        for (int i = 0; i < TickCount; i++)
            yTicks[i].text = FormatNum(viewYMax * i / (TickCount - 1));
        yAxisNameLabel.text = $"{rm.Label} ({rm.Unit})";
        if (moduleLegend != null) moduleLegend.SetActive(currentMode == ViewMode.Points);
        liveButton.Skin()?.SetActive(followLive);
    }

    private void FlushRun(ref int poolUsed, Variable v)
    {
        if (segBuffer.Count < 2) return;
        UIGraphLine line = GetPoolLine(poolUsed);
        line.color = Vars[v].Color;
        line.SetPoints(segBuffer);
        poolUsed++;
    }

    private UIGraphLine GetPoolLine(int index)
    {
        while (linePool.Count <= index)
        {
            GameObject go = new GameObject("Seg " + linePool.Count, typeof(RectTransform));
            go.transform.SetParent(linesLayer, false);
            UITheme.Fill((RectTransform)go.transform);
            UIGraphLine line = go.AddComponent<UIGraphLine>();
            line.Thickness = 2.6f;
            line.raycastTarget = false;
            linePool.Add(line);
        }
        return linePool[index];
    }

    private static float MapX(float t, Rect r, float pageStart, float pageEnd)
        => r.xMin + Mathf.InverseLerp(pageStart, pageEnd, t) * r.width;

    private float MapY(float y, Rect r)
        => r.yMin + Mathf.Clamp01(Mathf.InverseLerp(0f, viewYMax, y)) * r.height;

    // ---- hover ----------------------------------------------------

    public void OnPointerMove(PointerEventData eventData)
    {
        if (plotArea == null || samples.Count == 0)
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

        Rect r = plotArea.rect;
        if (local.x < r.xMin || local.x > r.xMax || local.y < r.yMin - 8f || local.y > r.yMax + 8f)
        {
            HideTooltip();
            return;
        }
        float frac = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float t = Mathf.Lerp(cachedPageStart, cachedPageEnd, frac);

        int nearest = -1;
        float best = float.MaxValue;
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].T < cachedPageStart || samples[i].T > cachedPageEnd) continue;
            float d = Mathf.Abs(samples[i].T - t);
            if (d < best) { best = d; nearest = i; }
        }
        if (nearest < 0)
        {
            HideTooltip();
            return;
        }

        RespMeta rm = responses[(int)currentResp];
        Sample s = samples[nearest];
        string varyingLine = s.Var == Variable.Free
            ? "Varying: none (all reactor sliders free)"
            : $"Varying: {Vars[s.Var].Label} = {GraphVisualUtils.FormatValue(s.VarValue, VarUnit(s.Var))}";
        string text = $"<b>{rm.Label}: {GraphVisualUtils.FormatValue(s.Y, rm.Unit)}</b>\n<color=#94A3B8>t = {FormatClock(s.T)}</color>\n{varyingLine}";

        if (currentMode == ViewMode.Points)
        {
            int cpNearest = -1;
            float cpBest = HoverRadiusPixels;
            for (int i = 0; i < changePoints.Count; i++)
            {
                ChangePoint cp = changePoints[i];
                if (cp.T < cachedPageStart || cp.T > cachedPageEnd) continue;
                Vector2 sp = new Vector2(MapX(cp.T, r, cachedPageStart, cachedPageEnd), MapY(cp.Y, r));
                float d = Vector2.Distance(local, sp);
                if (d < cpBest) { cpBest = d; cpNearest = i; }
            }
            if (cpNearest >= 0)
            {
                ChangePoint cp = changePoints[cpNearest];
                string cu = GraphVisualUtils.GetParameterUnit(cp.Parameter);
                text += $"\n<color=#94A3B8>{UITheme.Pretty(cp.Parameter)} {GraphVisualUtils.FormatValue(cp.From, cu)} → {GraphVisualUtils.FormatValue(cp.To, cu)} · {UIGraphKit.ModuleDisplayName(cp.Module)}</color>";
            }
        }

        if (hoverDot != null)
        {
            hoverDot.anchoredPosition = new Vector2(MapX(s.T, r, cachedPageStart, cachedPageEnd), MapY(s.Y, r));
            hoverDot.gameObject.SetActive(true);
            hoverDot.SetAsLastSibling();
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, eventData.position, cam, out Vector2 tl);
        tooltip.Show(text, tl);
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.Hide();
        if (hoverDot != null) hoverDot.gameObject.SetActive(false);
    }

    // ---- export ----------------------------------------------------

    private void ExportNow()
    {
        if (samples.Count == 0 && changePoints.Count == 0)
        {
            GraphExportUtil.ShowToast(ownerCanvas, labelFont, "OFAT timeline: nothing recorded yet.");
            return;
        }
        RespMeta rm = responses[(int)currentResp];
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseName = GraphExportUtil.Sanitize($"OFAT_timeline_{rm.Label}_{stamp}");

        var sb = new StringBuilder();
        sb.AppendLine($"# OFAT timeline — {rm.Label} over time");
        sb.AppendLine($"# exported,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# response,{rm.Label} ({rm.Unit})");
        sb.AppendLine($"# sample_interval_s,{SampleInterval}");
        sb.AppendLine($"# page_seconds,{PageSeconds}");
        sb.AppendLine("#");
        sb.AppendLine("# [variable epochs] time_s,active_variable,variable_value,variable_unit");
        foreach (Epoch e in epochs)
            sb.AppendLine($"E,{e.T.ToString("0.##")},{Vars[e.Var].Label},{(e.Var == Variable.Free ? "" : e.VarValue.ToString("0.####"))},{VarUnit(e.Var)}");
        sb.AppendLine("#");
        sb.AppendLine($"# [samples] time_s,{rm.Label} ({rm.Unit}),active_variable,variable_value,variable_unit");
        foreach (Sample s in samples)
            sb.AppendLine($"S,{s.T.ToString("0.##")},{s.Y.ToString("0.####")},{Vars[s.Var].Label},{(s.Var == Variable.Free ? "" : s.VarValue.ToString("0.####"))},{VarUnit(s.Var)}");
        sb.AppendLine("#");
        sb.AppendLine("# [module change points] time_s,value,module,parameter,from,to,unit");
        foreach (ChangePoint c in changePoints)
            sb.AppendLine($"C,{c.T.ToString("0.##")},{c.Y.ToString("0.####")},{GraphExportUtil.Csv(c.Module)},{GraphExportUtil.Csv(c.Parameter)},{c.From.ToString("0.###")},{c.To.ToString("0.###")},{GraphVisualUtils.GetParameterUnit(c.Parameter)}");

        string csvPath = GraphExportUtil.WriteText(baseName, "csv", sb.ToString());
        StartCoroutine(GraphExportUtil.CaptureRegionPng(ownerCanvas, selfRect, baseName, pngPath =>
        {
            string msg = pngPath != null
                ? $"Exported {baseName}.csv + .png to {GraphExportUtil.ExportDirectory}"
                : (csvPath != null
                    ? $"Exported {baseName}.csv (PNG failed) to {GraphExportUtil.ExportDirectory}"
                    : "Export failed — see console.");
            GraphExportUtil.ShowToast(ownerCanvas, labelFont, msg);
        }, exportButton != null ? exportButton.gameObject : null));
    }

    // ---- misc -------------------------------------------------------

    private void UpdateContext()
    {
        RespMeta rm = responses != null ? responses[(int)currentResp] : default;
        string varLine = currentVar == Variable.Free
            ? "No variable selected — all reactor sliders are free. Pick one to start a controlled run."
            : $"Varying {Vars[currentVar].Label} only — the other four reactor sliders are locked.";
        contextText.text = $"{varLine}  ·  One page = {PageSeconds:0} s";
    }

    private void RecolorVarButtons()
    {
        for (int i = 0; i < varButtons.Length; i++)
        {
            if (varButtons[i] == null) continue;
            bool active = VariableOrder[i] == currentVar;
            varButtons[i].Skin()?.SetActive(active);
            if (varDots[i] != null) varDots[i].color = active ? Color.white : Vars[VariableOrder[i]].Color;
        }
    }

    private void RecolorRespButtons()
    {
        for (int i = 0; i < respButtons.Length; i++)
            respButtons[i].Skin()?.SetActive((int)currentResp == i);
    }

    private void RecolorModeButtons()
    {
        for (int i = 0; i < modeButtons.Length; i++)
            modeButtons[i].Skin()?.SetActive((int)currentMode == i);
    }

    private static string FormatClock(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private static string FormatNum(float v)
    {
        float a = Mathf.Abs(v);
        if (a >= 100f) return v.ToString("0");
        if (a >= 10f) return v.ToString("0.#");
        return v.ToString("0.##");
    }

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
        Image core = UITheme.Dot("Core", dot, 9f, UITheme.Ink);
        UITheme.Center(core.rectTransform, 9f, 9f);
        hoverDot = dot;
        hoverDot.gameObject.SetActive(false);
    }
}
