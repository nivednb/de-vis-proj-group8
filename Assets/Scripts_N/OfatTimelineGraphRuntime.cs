using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    private struct SweepPoint { public float X; public float YieldPercent; }

    private static readonly Dictionary<Variable, VarMeta> Vars = new Dictionary<Variable, VarMeta>
    {
        [Variable.Free]        = new VarMeta { Label = "Free",        SliderParam = null,        Color = new Color(0.55f, 0.60f, 0.66f), ReadValue = null },
        [Variable.Temperature] = new VarMeta { Label = "Temperature", SliderParam = "Temp",      Color = new Color(1.00f, 0.42f, 0.30f), ReadValue = s => s.reactorTemperatureC },
        [Variable.Pressure]    = new VarMeta { Label = "Pressure",    SliderParam = "Pressure",  Color = new Color(0.30f, 0.62f, 1.00f), ReadValue = s => s.reactorPressureBar },
        [Variable.H2CO2]       = new VarMeta { Label = "H2 / CO2",    SliderParam = "H2/CO2",    Color = new Color(0.20f, 0.82f, 0.52f), ReadValue = s => s.h2Co2Ratio },
        [Variable.GHSV]        = new VarMeta { Label = "GHSV",        SliderParam = "GHSV",      Color = new Color(0.72f, 0.46f, 1.00f), ReadValue = s => s.ghsv },
        [Variable.FeedFlow]    = new VarMeta { Label = "Feed Flow",   SliderParam = "Feed flow", Color = new Color(0.96f, 0.78f, 0.20f), ReadValue = s => s.reactorFeedFlowPercent },
    };

    private static float ReadVarValue(Variable v, PlantProcessSimulator sim)
    {
        if (v == Variable.Free || sim == null || Vars[v].ReadValue == null) return 0f;
        return Vars[v].ReadValue(sim.Current);
    }

    private static string VarUnit(Variable v) => v == Variable.Free ? "" : GraphVisualUtils.GetParameterUnit(Vars[v].SliderParam);

    private static readonly Color BtnIdle = new Color(0.10f, 0.24f, 0.32f, 1f);
    private static readonly Color BtnActive = new Color(0.10f, 0.55f, 0.75f, 1f);
    private static readonly Color AxisColor = new Color(0.7f, 0.75f, 0.8f, 1f);
    private static readonly Color AxisNameColor = new Color(0.86f, 0.92f, 0.98f, 1f);

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
    private readonly List<SweepPoint> temperatureSweepPoints = new List<SweepPoint>();
    private readonly List<SweepPoint> pressureSweepPoints = new List<SweepPoint>();
    private readonly List<SweepPoint> ratioSweepPoints = new List<SweepPoint>();
    private readonly List<SweepPoint> ghsvSweepPoints = new List<SweepPoint>();
    private readonly List<SweepPoint> feedSweepPoints = new List<SweepPoint>();
    private bool showingTemperatureSweep;
    private bool showingPressureSweep;
    private bool showingRatioSweep;
    private bool showingGhsvSweep;
    private bool showingFeedSweep;
    private float sweepCurrentTemperature;
    private float sweepCurrentYield;

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
    private Text xAxisNameLabel;
    private Text yMinLabel;
    private Text yMaxLabel;
    private Text xMinLabel;
    private Text xMaxLabel;
    private Button[] varButtons;
    private Button[] respButtons;
    private Button[] modeButtons;
    private Button prevPageButton;
    private Button nextPageButton;
    private Button liveButton;
    private Button exportButton;
    private GameObject moduleLegend;
    private RectTransform hoverDot;
    private GameObject tooltip;
    private Text tooltipText;
    private RectTransform tooltipRect;

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
            new RespMeta { Label = "Reactor Yield",   Unit = "%",    Max = 100f,           Select = s => s.reactorYieldPercent },
            new RespMeta { Label = "Overall Efficiency", Unit = "%",  Max = 100f,           Select = s => s.overallEfficiencyPercent },
            new RespMeta { Label = "Methanol Output", Unit = "kg/h", Max = designMethanol,  Select = s => s.methanolProductionKgH },
        };

        Image bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.035f, 0.055f, 0.07f, 0.001f);
        bg.raycastTarget = true;

        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.SetParent(container, false);
        Stretch(root);
        selfRect = root;

        titleText = MakeText("Title", root, "", 14, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        StretchWithOffset(titleText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -24f), new Vector2(-190f, -4f));

        contextText = MakeText("Context", root, "", 10, FontStyle.Normal, TextAnchor.UpperLeft, AxisColor);
        contextText.horizontalOverflow = HorizontalWrapMode.Overflow;
        StretchWithOffset(contextText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -42f), new Vector2(-14f, -26f));

        BuildVariableRow(root);
        BuildResponseRow(root);

        GameObject plotObject = new GameObject("Plot Area", typeof(RectTransform));
        plotArea = plotObject.GetComponent<RectTransform>();
        plotArea.SetParent(root, false);
        StretchWithOffset(plotArea, Vector2.zero, Vector2.one, new Vector2(62f, 34f), new Vector2(-16f, -104f));

        RectTransform yAxisLine = MakePanel("Y Axis Line", plotArea, AxisColor);
        StretchWithOffset(yAxisLine, Vector2.zero, new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f));
        RectTransform xAxisLine = MakePanel("X Axis Line", plotArea, AxisColor);
        StretchWithOffset(xAxisLine, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f));

        linesLayer = new GameObject("Lines", typeof(RectTransform)).GetComponent<RectTransform>();
        linesLayer.SetParent(plotArea, false);
        Stretch(linesLayer);
        epochLayer = new GameObject("Epochs", typeof(RectTransform)).GetComponent<RectTransform>();
        epochLayer.SetParent(plotArea, false);
        Stretch(epochLayer);
        pointsLayer = new GameObject("Points", typeof(RectTransform)).GetComponent<RectTransform>();
        pointsLayer.SetParent(plotArea, false);
        Stretch(pointsLayer);

        xAxisNameLabel = MakeText("X Axis Name", root, "X:  Time (mm:ss, one page = 1 min)", 11, FontStyle.Bold, TextAnchor.MiddleCenter, AxisNameColor);
        StretchWithOffset(xAxisNameLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(62f, 16f), new Vector2(-16f, 32f));

        yAxisNameLabel = MakeText("Y Axis Name", root, "", 11, FontStyle.Bold, TextAnchor.MiddleCenter, AxisNameColor);
        yAxisNameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        yAxisNameLabel.rectTransform.anchorMin = yAxisNameLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        yAxisNameLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        yAxisNameLabel.rectTransform.anchoredPosition = new Vector2(12f, -20f);
        yAxisNameLabel.rectTransform.sizeDelta = new Vector2(260f, 16f);
        yAxisNameLabel.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

        yMaxLabel = MakeText("Y Max", root, "", 10, FontStyle.Normal, TextAnchor.UpperRight, AxisColor);
        AnchorTopLeft(yMaxLabel.rectTransform, new Vector2(0f, -104f), new Vector2(56f, 16f));
        yMinLabel = MakeText("Y Min", root, "0", 10, FontStyle.Normal, TextAnchor.LowerRight, AxisColor);
        AnchorBottomLeft(yMinLabel.rectTransform, new Vector2(0f, 34f), new Vector2(56f, 16f));
        xMinLabel = MakeText("X Min", root, "00:00", 10, FontStyle.Normal, TextAnchor.LowerLeft, AxisColor);
        AnchorBottomLeft(xMinLabel.rectTransform, new Vector2(62f, 18f), new Vector2(90f, 16f));
        xMaxLabel = MakeText("X Max", root, "01:00", 10, FontStyle.Normal, TextAnchor.LowerRight, AxisColor);
        AnchorBottomRight(xMaxLabel.rectTransform, new Vector2(-16f, 18f), new Vector2(90f, 16f));

        BuildPaginationRow(root);
        BuildLegends(root);
        BuildHoverDot();
        BuildTooltip(root);

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
        RectTransform row = MakePanel("Variable Row", root, new Color(0.06f, 0.15f, 0.20f, 1f));
        Pin(row, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -70f), new Vector2(0f, -44f));
        Text lbl = MakeText("Vary Label", row, "VARY ONE:", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AxisColor);
        Pin(lbl.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(74f, 0f));

        Variable[] order = { Variable.Free, Variable.Temperature, Variable.Pressure, Variable.H2CO2, Variable.GHSV, Variable.FeedFlow };
        varButtons = new Button[order.Length];
        float w = (1f - 0.065f) / order.Length;
        for (int i = 0; i < order.Length; i++)
        {
            Variable v = order[i];
            Button b = MakeButton("Var " + v, row, Vars[v].Label.ToUpperInvariant(), BtnIdle, 9);
            Pin(b.GetComponent<RectTransform>(), new Vector2(0.065f + i * w, 0f), new Vector2(0.065f + (i + 1) * w, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f));
            b.onClick.AddListener(() => SetVariable(v));
            varButtons[i] = b;
        }
    }

    private void BuildResponseRow(RectTransform root)
    {
        RectTransform row = MakePanel("Response Row", root, new Color(0.06f, 0.15f, 0.20f, 1f));
        Pin(row, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -98f), new Vector2(0f, -72f));
        Text lbl = MakeText("Show Label", row, "SHOW:", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AxisColor);
        Pin(lbl.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(50f, 0f));

        string[] respNames = { "YIELD", "EFFICIENCY", "METHANOL" };
        respButtons = new Button[respNames.Length];
        for (int i = 0; i < respNames.Length; i++)
        {
            int idx = i;
            Button b = MakeButton("Resp " + i, row, respNames[i], BtnIdle, 9);
            float x0 = 0.06f + i * 0.15f;
            Pin(b.GetComponent<RectTransform>(), new Vector2(x0, 0f), new Vector2(x0 + 0.145f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f));
            b.onClick.AddListener(() => SetResponse((Response)idx));
            respButtons[i] = b;
        }

        string[] modeNames = { "LINE ONLY", "WITH POINTS" };
        modeButtons = new Button[modeNames.Length];
        for (int i = 0; i < modeNames.Length; i++)
        {
            int idx = i;
            Button b = MakeButton("Mode " + i, row, modeNames[i], BtnIdle, 9);
            float x0 = 0.54f + i * 0.15f;
            Pin(b.GetComponent<RectTransform>(), new Vector2(x0, 0f), new Vector2(x0 + 0.145f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f));
            b.onClick.AddListener(() => SetMode((ViewMode)idx));
            modeButtons[i] = b;
        }

        exportButton = MakeButton("Export", row, "EXPORT", new Color(0.10f, 0.34f, 0.48f, 1f), 9);
        Pin(exportButton.GetComponent<RectTransform>(), new Vector2(0.87f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-2f, -1f));
        exportButton.onClick.AddListener(ExportNow);
    }

    private void BuildPaginationRow(RectTransform root)
    {
        RectTransform row = MakePanel("Pagination Row", root, new Color(0.06f, 0.15f, 0.20f, 1f));
        Pin(row, Vector2.zero, new Vector2(1f, 0f), new Vector2(62f, 3f), new Vector2(-16f, 29f));

        prevPageButton = MakeButton("Prev Page", row, "◀  PREV", BtnIdle, 9);
        Pin(prevPageButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.16f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f));
        prevPageButton.onClick.AddListener(PrevPage);

        pageLabel = MakeText("Page Label", row, "", 10, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Pin(pageLabel.rectTransform, new Vector2(0.16f, 0f), new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);

        nextPageButton = MakeButton("Next Page", row, "NEXT  ▶", BtnIdle, 9);
        Pin(nextPageButton.GetComponent<RectTransform>(), new Vector2(0.72f, 0f), new Vector2(0.86f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f));
        nextPageButton.onClick.AddListener(NextPage);

        liveButton = MakeButton("Live", row, "● LIVE", BtnActive, 9);
        Pin(liveButton.GetComponent<RectTransform>(), new Vector2(0.86f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f));
        liveButton.onClick.AddListener(GoLive);
    }

    private void BuildLegends(RectTransform root)
    {
        // Variable / line legend — always shown.
        RectTransform varLegend = MakePanel("Variable Legend", root, new Color(0.02f, 0.05f, 0.07f, 0.9f));
        varLegend.anchorMin = varLegend.anchorMax = new Vector2(1f, 1f);
        varLegend.pivot = new Vector2(1f, 1f);
        varLegend.anchoredPosition = new Vector2(-18f, -106f);
        varLegend.sizeDelta = new Vector2(150f, 98f);
        Outline vo = varLegend.gameObject.AddComponent<Outline>();
        vo.effectColor = new Color(1f, 1f, 1f, 0.1f);
        Text vt = MakeText("Var Legend Title", varLegend, "LINE = varying", 8, FontStyle.Bold, TextAnchor.UpperLeft, AxisColor);
        Pin(vt.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(6f, -14f), new Vector2(-4f, -2f));
        Variable[] order = { Variable.Free, Variable.Temperature, Variable.Pressure, Variable.H2CO2, Variable.GHSV, Variable.FeedFlow };
        for (int i = 0; i < order.Length; i++)
        {
            float y = -16f - i * 13f;
            RectTransform sw = MakePanel("sw", varLegend, Vars[order[i]].Color);
            sw.anchorMin = sw.anchorMax = new Vector2(0f, 1f);
            sw.pivot = new Vector2(0f, 1f);
            sw.anchoredPosition = new Vector2(7f, y);
            sw.sizeDelta = new Vector2(14f, 4f);
            Text t = MakeText("t", varLegend, Vars[order[i]].Label, 8, FontStyle.Normal, TextAnchor.MiddleLeft, AxisColor);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(26f, y + 6f);
            t.rectTransform.sizeDelta = new Vector2(118f, 12f);
        }

        // Module / point legend — only shown in "with points" mode.
        moduleLegend = MakePanel("Module Legend", root, new Color(0.02f, 0.05f, 0.07f, 0.9f)).gameObject;
        RectTransform ml = moduleLegend.GetComponent<RectTransform>();
        ml.anchorMin = ml.anchorMax = new Vector2(1f, 1f);
        ml.pivot = new Vector2(1f, 1f);
        ml.anchoredPosition = new Vector2(-18f, -210f);
        int rows = Mathf.CeilToInt(GraphVisualUtils.ModulePalette.Length / 2f);
        ml.sizeDelta = new Vector2(190f, rows * 13f + 16f);
        Outline mo = moduleLegend.AddComponent<Outline>();
        mo.effectColor = new Color(1f, 1f, 1f, 0.1f);
        Text mt = MakeText("Mod Legend Title", ml, "POINTS = module change", 8, FontStyle.Bold, TextAnchor.UpperLeft, AxisColor);
        Pin(mt.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(6f, -14f), new Vector2(-4f, -2f));
        for (int i = 0; i < GraphVisualUtils.ModulePalette.Length; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = 7f + col * 92f;
            float y = -16f - row * 13f;
            RectTransform sw = MakePanel("sw", ml, GraphVisualUtils.ModulePalette[i].Color);
            Image si = sw.GetComponent<Image>();
            si.sprite = GraphVisualUtils.GetCircleSprite();
            si.type = Image.Type.Simple;
            sw.anchorMin = sw.anchorMax = new Vector2(0f, 1f);
            sw.pivot = new Vector2(0f, 1f);
            sw.anchoredPosition = new Vector2(x, y);
            sw.sizeDelta = new Vector2(7f, 7f);
            Text t = MakeText("t", ml, GraphVisualUtils.ModulePalette[i].Module, 7, FontStyle.Normal, TextAnchor.MiddleLeft, AxisColor);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(x + 10f, y + 6f);
            t.rectTransform.sizeDelta = new Vector2(80f, 12f);
        }
    }

    // ---- state changes ----------------------------------------------------

    private void SetVariable(Variable v)
    {
        if (v == currentVar)
        {
            if (v == Variable.Temperature && !showingTemperatureSweep)
            {
                showingTemperatureSweep = true;
                ClearAutomaticSweepModes();
                showingTemperatureSweep = true;
                currentResp = Response.Yield;
                GenerateTemperatureYieldSweep();
                RecolorRespButtons();
                dirty = true;
            }
            else if (v == Variable.Pressure && !showingPressureSweep)
            {
                ClearAutomaticSweepModes();
                showingPressureSweep = true;
                currentResp = Response.Yield;
                GeneratePressureYieldSweep();
                RecolorRespButtons();
                dirty = true;
            }
            else if (v == Variable.H2CO2 && !showingRatioSweep)
            {
                ClearAutomaticSweepModes();
                showingRatioSweep = true;
                currentResp = Response.Yield;
                GenerateRatioYieldSweep();
                RecolorRespButtons();
                dirty = true;
            }
            else if (v == Variable.GHSV && !showingGhsvSweep)
            {
                ClearAutomaticSweepModes();
                showingGhsvSweep = true;
                currentResp = Response.Yield;
                GenerateGhsvYieldSweep();
                RecolorRespButtons();
                dirty = true;
            }
            else if (v == Variable.FeedFlow && !showingFeedSweep)
            {
                ClearAutomaticSweepModes();
                showingFeedSweep = true;
                currentResp = Response.Yield;
                GenerateFeedYieldSweep();
                RecolorRespButtons();
                dirty = true;
            }
            return;
        }
        currentVar = v;
        ClearAutomaticSweepModes();
        showingTemperatureSweep = v == Variable.Temperature;
        showingPressureSweep = v == Variable.Pressure;
        showingRatioSweep = v == Variable.H2CO2;
        showingGhsvSweep = v == Variable.GHSV;
        showingFeedSweep = v == Variable.FeedFlow;
        if (showingTemperatureSweep)
        {
            currentResp = Response.Yield;
            GenerateTemperatureYieldSweep();
        }
        else if (showingPressureSweep)
        {
            currentResp = Response.Yield;
            GeneratePressureYieldSweep();
        }
        else if (showingRatioSweep)
        {
            currentResp = Response.Yield;
            GenerateRatioYieldSweep();
        }
        else if (showingGhsvSweep)
        {
            currentResp = Response.Yield;
            GenerateGhsvYieldSweep();
        }
        else if (showingFeedSweep)
        {
            currentResp = Response.Yield;
            GenerateFeedYieldSweep();
        }
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
        ClearAutomaticSweepModes();
        RecolorRespButtons();
        UpdateContext();
        dirty = true;
    }

    /// <summary>Captures one live baseline, then evaluates independent copies with only
    /// temperature changed. Simulate is pure, so neither plant controls nor recycle state
    /// can be altered by this educational OFAT calculation.</summary>
    private void GenerateTemperatureYieldSweep()
    {
        temperatureSweepPoints.Clear();
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim == null) return;

        PlantProcessSimulator.ProcessInputs baseline = sim.CurrentInputs;
        sweepCurrentTemperature = baseline.temperature;
        sweepCurrentYield = sim.Current.reactorYieldPercent;
        for (int temperature = 180; temperature <= 300; temperature += 10)
        {
            PlantProcessSimulator.ProcessInputs hypothetical = baseline;
            hypothetical.temperature = temperature;
            PlantProcessSimulator.ProcessSnapshot result = sim.Simulate(hypothetical);
            temperatureSweepPoints.Add(new SweepPoint
            {
                X = temperature,
                YieldPercent = result.reactorYieldPercent
            });
        }
    }

    private void GeneratePressureYieldSweep()
    {
        pressureSweepPoints.Clear();
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim == null) return;

        PlantProcessSimulator.ProcessInputs baseline = sim.CurrentInputs;
        sweepCurrentTemperature = baseline.pressure;
        sweepCurrentYield = sim.Current.reactorYieldPercent;
        for (int pressure = 40; pressure <= 100; pressure += 5)
        {
            PlantProcessSimulator.ProcessInputs hypothetical = baseline;
            hypothetical.pressure = pressure;
            PlantProcessSimulator.ProcessSnapshot result = sim.Simulate(hypothetical);
            pressureSweepPoints.Add(new SweepPoint
            {
                X = pressure,
                YieldPercent = result.reactorYieldPercent
            });
        }
    }

    private void GenerateRatioYieldSweep()
    {
        GenerateYieldSweep(ratioSweepPoints, 1f, 6f, 12, (ref PlantProcessSimulator.ProcessInputs inputs, float value) => inputs.ratio = value, out sweepCurrentTemperature);
    }

    private void GenerateGhsvYieldSweep()
    {
        GenerateYieldSweep(ghsvSweepPoints, 1000f, 20000f, 12, (ref PlantProcessSimulator.ProcessInputs inputs, float value) => inputs.ghsv = value, out sweepCurrentTemperature);
    }

    private void GenerateFeedYieldSweep()
    {
        GenerateYieldSweep(feedSweepPoints, 20f, 130f, 12, (ref PlantProcessSimulator.ProcessInputs inputs, float value) => inputs.reactorFeedFlow = value, out sweepCurrentTemperature);
    }

    private delegate void SetSweepInput(ref PlantProcessSimulator.ProcessInputs inputs, float value);

    private void GenerateYieldSweep(List<SweepPoint> destination, float min, float max, int intervals,
        SetSweepInput setValue, out float currentValue)
    {
        destination.Clear();
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        currentValue = 0f;
        if (sim == null) return;
        PlantProcessSimulator.ProcessInputs baseline = sim.CurrentInputs;
        for (int index = 0; index <= intervals; index++)
        {
            float value = Mathf.Lerp(min, max, index / (float)intervals);
            PlantProcessSimulator.ProcessInputs hypothetical = baseline;
            setValue(ref hypothetical, value);
            PlantProcessSimulator.ProcessSnapshot result = sim.Simulate(hypothetical);
            destination.Add(new SweepPoint { X = value, YieldPercent = result.reactorYieldPercent });
        }
        sweepCurrentYield = sim.Current.reactorYieldPercent;
        currentValue = ReadVarValue(currentVar, sim);
    }

    private void ClearAutomaticSweepModes()
    {
        showingTemperatureSweep = false;
        showingPressureSweep = false;
        showingRatioSweep = false;
        showingGhsvSweep = false;
        showingFeedSweep = false;
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
        showingTemperatureSweep = false;
        showingPressureSweep = false;
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

        if (showingTemperatureSweep)
        {
            RebuildAutomaticYieldSweep(temperatureSweepPoints, 180f, 300f, "Temperature", "°C", "temperature");
            return;
        }
        if (showingPressureSweep)
        {
            RebuildAutomaticYieldSweep(pressureSweepPoints, 40f, 100f, "Pressure", "bar", "pressure");
            return;
        }
        if (showingRatioSweep)
        {
            RebuildAutomaticYieldSweep(ratioSweepPoints, 1f, 6f, "H2/CO2 Ratio", "", "H2/CO2 ratio");
            return;
        }
        if (showingGhsvSweep)
        {
            RebuildAutomaticYieldSweep(ghsvSweepPoints, 1000f, 20000f, "GHSV", "1/h", "GHSV");
            return;
        }
        if (showingFeedSweep)
        {
            RebuildAutomaticYieldSweep(feedSweepPoints, 20f, 130f, "Feed Flow", "%", "feed");
            return;
        }

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
            RectTransform v = MakePanel("Epoch Line", epochLayer, new Color(1f, 1f, 1f, 0.22f));
            v.GetComponent<Image>().raycastTarget = false;
            v.anchorMin = v.anchorMax = new Vector2(0.5f, 0.5f);
            v.pivot = new Vector2(0.5f, 0f);
            v.sizeDelta = new Vector2(1.5f, r.height);
            v.anchoredPosition = new Vector2(x, r.yMin);

            string epochText = e.Var == Variable.Free
                ? $"Free · {FormatClock(e.T)}"
                : $"{Vars[e.Var].Label} = {GraphVisualUtils.FormatValue(e.VarValue, VarUnit(e.Var))} · {FormatClock(e.T)}";
            Text lab = MakeText("Epoch Label", epochLayer, epochText, 8, FontStyle.Bold, TextAnchor.LowerLeft, Vars[e.Var].Color);
            lab.horizontalOverflow = HorizontalWrapMode.Overflow;
            lab.rectTransform.anchorMin = lab.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            lab.rectTransform.pivot = new Vector2(0f, 0f);
            lab.rectTransform.anchoredPosition = new Vector2(x + 3f, r.yMin + r.height - 11f);
            lab.rectTransform.sizeDelta = new Vector2(140f, 12f);
        }

        // module change points
        if (currentMode == ViewMode.Points)
        {
            for (int i = 0; i < changePoints.Count; i++)
            {
                ChangePoint cp = changePoints[i];
                if (cp.T < pageStart || cp.T > pageEnd) continue;
                Vector2 p = new Vector2(MapX(cp.T, r, pageStart, pageEnd), MapY(cp.Y, r));
                RectTransform dot = MakePanel("Point", pointsLayer, GraphVisualUtils.GetModuleColor(cp.Module));
                Image di = dot.GetComponent<Image>();
                di.sprite = GraphVisualUtils.GetCircleSprite();
                di.type = Image.Type.Simple;
                di.raycastTarget = false;
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(6f, 6f);
                dot.anchoredPosition = p;
                Outline ol = dot.gameObject.AddComponent<Outline>();
                ol.effectColor = new Color(1f, 1f, 1f, 0.5f);
                ol.effectDistance = new Vector2(0.5f, -0.5f);
            }
        }

        titleText.text = $"OFAT Timeline — {rm.Label} over time";
        pageLabel.text = $"Page {pageIndex + 1} / {latest + 1}    {FormatClock(pageStart)} – {FormatClock(pageEnd)}" + (pageIndex >= latest ? "   (live)" : "");
        xMinLabel.text = FormatClock(pageStart);
        xMaxLabel.text = FormatClock(pageEnd);
        yMaxLabel.text = FormatNum(viewYMax);
        yMinLabel.text = "0";
        yAxisNameLabel.text = $"Y:  {rm.Label} ({rm.Unit})";
        if (xAxisNameLabel != null) xAxisNameLabel.text = "X:  Time (mm:ss, one page = 1 min)";
        if (moduleLegend != null) moduleLegend.SetActive(currentMode == ViewMode.Points);
        if (prevPageButton != null) prevPageButton.interactable = true;
        if (nextPageButton != null) nextPageButton.interactable = true;
        if (liveButton != null) liveButton.GetComponent<Image>().color = followLive ? BtnActive : BtnIdle;
    }

    private void RebuildAutomaticYieldSweep(List<SweepPoint> points, float xMin, float xMax,
        string parameter, string unit, string lowerParameter)
    {
        for (int i = 0; i < linePool.Count; i++) linePool[i].ClearPoints();
        if (points.Count == 0) return;

        Rect r = plotArea.rect;
        float dataMax = 0f;
        for (int i = 0; i < points.Count; i++)
            dataMax = Mathf.Max(dataMax, points[i].YieldPercent);
        viewYMax = Mathf.Clamp(dataMax * 1.15f, 20f, 100f);

        segBuffer.Clear();
        for (int i = 0; i < points.Count; i++)
        {
            SweepPoint point = points[i];
            segBuffer.Add(new Vector2(
                r.xMin + Mathf.InverseLerp(xMin, xMax, point.X) * r.width,
                MapY(point.YieldPercent, r)));
        }
        if (segBuffer.Count >= 2)
        {
            UIGraphLine line = GetPoolLine(0);
            line.color = Vars[Variable.Temperature].Color;
            line.SetPoints(segBuffer);
        }

        for (int i = 0; i < points.Count; i++)
        {
            SweepPoint point = points[i];
            RectTransform dot = MakePanel(parameter + " Sweep Point", pointsLayer, Vars[currentVar].Color);
            Image image = dot.GetComponent<Image>();
            image.sprite = GraphVisualUtils.GetCircleSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(6f, 6f);
            dot.anchoredPosition = segBuffer[i];
        }

        RectTransform currentDot = MakePanel("Current Temperature Point", pointsLayer, Color.white);
        Image currentImage = currentDot.GetComponent<Image>();
        currentImage.sprite = GraphVisualUtils.GetCircleSprite();
        currentImage.type = Image.Type.Simple;
        currentImage.raycastTarget = false;
        currentDot.anchorMin = currentDot.anchorMax = new Vector2(0.5f, 0.5f);
        currentDot.pivot = new Vector2(0.5f, 0.5f);
        currentDot.sizeDelta = new Vector2(11f, 11f);
        currentDot.anchoredPosition = new Vector2(
            r.xMin + Mathf.InverseLerp(xMin, xMax, sweepCurrentTemperature) * r.width,
            MapY(sweepCurrentYield, r));
        Outline highlight = currentDot.gameObject.AddComponent<Outline>();
        highlight.effectColor = Vars[currentVar].Color;
        highlight.effectDistance = new Vector2(1.5f, -1.5f);

        titleText.text = $"How does {lowerParameter} affect reactor yield?";
        contextText.text = currentVar == Variable.FeedFlow
            ? "Feed flow is varied while other operating conditions are held constant. In the current educational model, feed flow primarily changes throughput rather than calculated reactor yield, so the yield response remains approximately constant."
            : $"{parameter} vs Reactor Yield   |   Only {lowerParameter} changes; other operating conditions remain constant.   |   Each point is calculated using the educational process model.";
        pageLabel.text = "AUTOMATIC OFAT SWEEP   •   13 calculated points";
        xMinLabel.text = string.IsNullOrEmpty(unit) ? xMin.ToString("0.##") : $"{xMin:F0} {unit}";
        xMaxLabel.text = string.IsNullOrEmpty(unit) ? xMax.ToString("0.##") : $"{xMax:F0} {unit}";
        yMaxLabel.text = FormatNum(viewYMax);
        yMinLabel.text = "0";
        yAxisNameLabel.text = "Y:  Reactor Yield (%)";
        if (xAxisNameLabel != null) xAxisNameLabel.text = string.IsNullOrEmpty(unit)
            ? $"X:  {parameter}"
            : $"X:  {parameter} ({unit})";
        if (moduleLegend != null) moduleLegend.SetActive(false);
        if (prevPageButton != null) prevPageButton.interactable = false;
        if (nextPageButton != null) nextPageButton.interactable = false;
        if (liveButton != null) liveButton.GetComponent<Image>().color = BtnIdle;
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
            Stretch(go.GetComponent<RectTransform>());
            UIGraphLine line = go.AddComponent<UIGraphLine>();
            line.Thickness = 2.4f;
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
            ? "varying: none (all reactor sliders free)"
            : $"varying: {Vars[s.Var].Label} = {GraphVisualUtils.FormatValue(s.VarValue, VarUnit(s.Var))}";
        string text = $"t = {FormatClock(s.T)}\n{rm.Label}: {GraphVisualUtils.FormatValue(s.Y, rm.Unit)}\n{varyingLine}";

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
                text += $"\n—\n{cp.Parameter} {GraphVisualUtils.FormatValue(cp.From, cu)} → {GraphVisualUtils.FormatValue(cp.To, cu)} in {cp.Module}";
            }
        }

        tooltipText.text = text;
        tooltip.SetActive(true);
        if (hoverDot != null)
        {
            hoverDot.anchoredPosition = new Vector2(MapX(s.T, r, cachedPageStart, cachedPageEnd), MapY(s.Y, r));
            hoverDot.gameObject.SetActive(true);
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, eventData.position, cam, out Vector2 tl);
        tooltipRect.anchoredPosition = tl + new Vector2(14f, 14f);
        tooltip.transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.SetActive(false);
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
                ? $"Exported  {baseName}.csv + .png   →   {GraphExportUtil.ExportDirectory}"
                : (csvPath != null
                    ? $"Exported  {baseName}.csv  (PNG failed)   →   {GraphExportUtil.ExportDirectory}"
                    : "Export failed — see console.");
            GraphExportUtil.ShowToast(ownerCanvas, labelFont, msg);
        }, exportButton != null ? exportButton.gameObject : null));
    }

    // ---- misc -------------------------------------------------------

    private void UpdateContext()
    {
        RespMeta rm = responses != null ? responses[(int)currentResp] : default;
        string varLine = currentVar == Variable.Free
            ? "No variable selected — all reactor sliders free. Pick one to start a controlled OFAT run."
            : $"Varying {Vars[currentVar].Label} only — the other 4 reactor sliders are locked constant.";
        contextText.text = $"{varLine}   |   Y = {rm.Label} ({rm.Unit})   |   1 page = {PageSeconds:0} s";
    }

    private void RecolorVarButtons()
    {
        Variable[] order = { Variable.Free, Variable.Temperature, Variable.Pressure, Variable.H2CO2, Variable.GHSV, Variable.FeedFlow };
        for (int i = 0; i < varButtons.Length; i++)
        {
            if (varButtons[i] == null) continue;
            bool active = order[i] == currentVar;
            varButtons[i].GetComponent<Image>().color = active
                ? (order[i] == Variable.Free ? BtnActive : Color.Lerp(Vars[order[i]].Color, Color.black, 0.15f))
                : BtnIdle;
        }
    }

    private void RecolorRespButtons()
    {
        for (int i = 0; i < respButtons.Length; i++)
            if (respButtons[i] != null) respButtons[i].GetComponent<Image>().color = (int)currentResp == i ? BtnActive : BtnIdle;
    }

    private void RecolorModeButtons()
    {
        for (int i = 0; i < modeButtons.Length; i++)
            if (modeButtons[i] != null) modeButtons[i].GetComponent<Image>().color = (int)currentMode == i ? BtnActive : BtnIdle;
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
        hoverDot = MakePanel("Hover Dot", plotArea, new Color(1f, 1f, 1f, 0.95f));
        Image img = hoverDot.GetComponent<Image>();
        img.sprite = GraphVisualUtils.GetCircleSprite();
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        hoverDot.anchorMin = hoverDot.anchorMax = new Vector2(0.5f, 0.5f);
        hoverDot.pivot = new Vector2(0.5f, 0.5f);
        hoverDot.sizeDelta = new Vector2(13f, 13f);
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
        tooltipRect.sizeDelta = new Vector2(230f, 72f);
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
        text.font = labelFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private Button MakeButton(string name, Transform parent, string label, Color color, int fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        Button button = go.AddComponent<Button>();
        Text text = MakeText("Label", go.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private RectTransform MakePanel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
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

    private static void Pin(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        => StretchWithOffset(rect, anchorMin, anchorMax, offsetMin, offsetMax);

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
