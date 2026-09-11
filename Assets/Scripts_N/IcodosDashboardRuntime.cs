using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the ICODOS-inspired full-plant presentation at runtime.
/// The values are supplied by PlantProcessSimulator and remain educational estimates.
/// </summary>
[DisallowMultipleComponent]
public sealed class IcodosDashboardRuntime : MonoBehaviour
{
    private enum DashboardPage { Overview, Process, Equipment, Simulation, Analytics, FlowInspection }
    private enum AnalyticsTab { Stats, Visualise }

    private const string RuntimeRootName = "Generated ICODOS Plant Dashboard";

    private static readonly Color HeaderColor = new Color32(10, 24, 34, 248);
    private static readonly Color PanelColor = new Color32(15, 38, 51, 232);
    private static readonly Color PanelLightColor = new Color32(23, 54, 69, 236);
    private static readonly Color AccentColor = new Color32(20, 145, 205, 255);
    private static readonly Color MutedTextColor = new Color32(174, 195, 206, 255);
    private static readonly Color HealthyColor = new Color32(34, 197, 94, 255);

    private readonly List<Text> liveTexts = new List<Text>();
    private readonly List<Image> navigationImages = new List<Image>();
    private readonly List<DashboardPage> navigationPages = new List<DashboardPage>();
    private readonly List<Image> analyticsBars = new List<Image>();
    private Canvas canvas;
    private Font font;
    private OrbitCameraController cameraController;
    private GameObject helpPanel;
    private GameObject legendPanel;
    private GameObject plantStatusPanel;
    private GameObject kpiStrip;
    private GameObject processPanel;
    private GameObject equipmentPanel;
    private GameObject simulationPanel;
    private GameObject flowInspectionPanel;
    private GameObject analyticsWindow;
    private GameObject analyticsStatsTab;
    private GameObject analyticsVisualiseTab;
    private Button analyticsNavButton;
    private Button analyticsStatsTabButton;
    private Button analyticsVisualiseTabButton;
    private bool analyticsWindowOpen;
    private AnalyticsTab currentAnalyticsTab = AnalyticsTab.Stats;
    private readonly List<Button> runToggleButtons = new List<Button>();

    // VISUALISE tab: four sub-tabs (YIELD / EFFICIENCY correlation graphs, the OFAT
    // timeline, and the live strip chart).
    private enum VisualiseSubTab { Yield, Efficiency, Ofat, Live }
    private VisualiseSubTab currentSubTab = VisualiseSubTab.Yield;
    private readonly List<Button> subTabButtons = new List<Button>();
    private readonly CanvasGroup[] subTabGroups = new CanvasGroup[4];
    private readonly List<Button> yieldParamButtons = new List<Button>();
    private readonly List<CanvasGroup> yieldGraphGroups = new List<CanvasGroup>();
    private readonly List<Button> efficiencyParamButtons = new List<Button>();
    private readonly List<CanvasGroup> efficiencyGraphGroups = new List<CanvasGroup>();
    private int yieldParamIndex;
    private int efficiencyParamIndex;
    private OfatTimelineGraphRuntime ofatTimeline;
    private InteractiveModulePanelRuntime modulePanels;

    // Five reactor parameters shared by the YIELD and EFFICIENCY correlation sub-tabs.
    private static readonly string[] ReactorParamNames = { "Temp", "Pressure", "H2:CO2", "GHSV", "Feed" };
    // Exact reactor slider labels (InteractiveModulePanelRuntime.CreateControls) for the
    // variable-lock — selecting a parameter tab freezes every reactor slider but this one.
    private static readonly string[] ReactorParamSliderLabels = { "Temp", "Pressure", "H2/CO2", "GHSV", "Feed flow" };
    private static readonly string[] ReactorParamAxisLabels =
        { "Reactor Temp (C)", "Reactor Pressure (bar)", "H2 / CO2 Ratio", "GHSV (1/h)", "Reactor Feed Flow (%)" };
    private static readonly float[] ReactorParamMin = { 180f, 40f, 1f, 1000f, 20f };
    private static readonly float[] ReactorParamMax = { 300f, 100f, 6f, 20000f, 130f };
    private static readonly Func<PlantProcessSimulator.ProcessSnapshot, float>[] ReactorParamSelectors =
    {
        s => s.reactorTemperatureC,
        s => s.reactorPressureBar,
        s => s.h2Co2Ratio,
        s => s.ghsv,
        s => s.reactorFeedFlowPercent,
    };
    private LiveGraphRuntime liveProgressEfficiencyGraph;
    private LiveGraphRuntime liveProgressOutputGraph;
    private Button liveProgressEfficiencyButton;
    private Button liveProgressOutputButton;
    private Text processStepText;
    private Text processTitleText;
    private Text processBodyText;
    private Text processStreamsText;
    private Text equipmentSummaryText;
    private Text simulationSummaryText;
    private Text analyticsSummaryText;
    private Text analyticsInsightsText;
    private DashboardPage currentPage = DashboardPage.Overview;
    private int processStepIndex;
    private Text plantStatusText;
    private Text efficiencyText;
    private Text productionText;
    private Text utilizationText;
    private Text methanolTankText;
    private Text electrolyzerKpis;
    private Text captureKpis;
    private Text reactorKpis;
    private Text separationKpis;
    private float nextRefresh;

    public static IcodosDashboardRuntime Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<IcodosDashboardRuntime>() != null) return;
        new GameObject(RuntimeRootName).AddComponent<IcodosDashboardRuntime>();
    }

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>True once <see cref="Build"/> has produced the dashboard canvas.</summary>
    public bool IsBuilt => canvas != null;

    /// <summary>
    /// Opens one of the header sections by name ("overview", "process", "flow", "reactor",
    /// "simulation"). Exists so the guided tutorial can put the dashboard into the state a
    /// tour step is describing without duplicating any page logic of its own.
    /// </summary>
    public void TutorialShowPage(string pageId)
    {
        switch (pageId)
        {
            case "overview": SelectPage(DashboardPage.Overview); break;
            case "process": SelectPage(DashboardPage.Process); break;
            case "flow": SelectPage(DashboardPage.FlowInspection); break;
            case "reactor": SelectPage(DashboardPage.Equipment); break;
            case "simulation": SelectPage(DashboardPage.Simulation); break;
        }
    }

    /// <summary>
    /// Tutorial hook for the analytics window. A null or empty <paramref name="subTab"/> means
    /// the STATS tab; "yield", "efficiency", "ofat" or "live" select the VISUALISE tab and the
    /// matching sub-tab.
    /// </summary>
    public void TutorialSetAnalyticsView(bool open, string subTab)
    {
        if (!open)
        {
            CloseAnalyticsWindow();
            return;
        }

        OpenAnalyticsWindow();
        if (string.IsNullOrEmpty(subTab))
        {
            SetAnalyticsTab(AnalyticsTab.Stats);
            return;
        }

        SetAnalyticsTab(AnalyticsTab.Visualise);
        switch (subTab)
        {
            case "yield": SelectSubTab(VisualiseSubTab.Yield); break;
            case "efficiency": SelectSubTab(VisualiseSubTab.Efficiency); break;
            case "ofat": SelectSubTab(VisualiseSubTab.Ofat); break;
            case "live": SelectSubTab(VisualiseSubTab.Live); break;
        }
    }

    /// <summary>Returns the dashboard to its normal starting view — used when the tutorial
    /// finishes or is skipped part-way through an analytics step.</summary>
    public void TutorialRestoreDefaults()
    {
        SetPopupVisible(helpPanel, false);
        CloseAnalyticsWindow();
        SelectPage(DashboardPage.Overview);
    }

    /// <summary>
    /// True while the analytics window is open and the given screen point falls within its
    /// actual current rect (it's draggable, so this is computed live rather than cached) —
    /// the precise geometric test other systems (camera orbit, module hover buttons) use to
    /// block input only under the window, and nowhere else on screen.
    /// </summary>
    public bool IsPointerOverAnalyticsWindow(Vector2 screenPoint)
    {
        if (!analyticsWindowOpen || analyticsWindow == null) return false;
        RectTransform rect = analyticsWindow.GetComponent<RectTransform>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
    }

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        cameraController = FindFirstObjectByType<OrbitCameraController>();
        if (cameraController != null) cameraController.BackgroundClicked += OnBackgroundClicked;
        HideLegacyDashboard();
        Build();
        Refresh();
    }

    private void OnDestroy()
    {
        if (cameraController != null) cameraController.BackgroundClicked -= OnBackgroundClicked;
    }

    /// <summary>
    /// Clicking the 3D scene itself (not any UI) moves focus back to the main simulation
    /// view — closes whichever nav page (Process Map / Flow Lab / Reactor Lab / Simulation)
    /// is open, the way clicking outside a popover dismisses it elsewhere. Module control-
    /// slider panels and the analytics window are deliberately left alone.
    /// </summary>
    private void OnBackgroundClicked()
    {
        if (currentPage != DashboardPage.Overview) SelectPage(DashboardPage.Overview);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;
        Refresh();
    }

    private void HideLegacyDashboard()
    {
        Canvas[] existing = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas candidate in existing)
        {
            if (candidate.transform.IsChildOf(transform)) continue;
            if (candidate.name == "Canvas" && candidate.sortingOrder < 10)
            {
                candidate.enabled = false;
            }
        }
    }

    [ContextMenu("Build ICODOS Dashboard")]
    public void Build()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        analyticsBars.Clear();
        runToggleButtons.Clear();

        GameObject canvasObject = new GameObject("ICODOS Dashboard Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above InteractiveModulePanelRuntime's canvas (sortingOrder 75) so the dashboard
        // chrome and the analytics popup always render on top of the floating hover
        // info-buttons/panels instead of being covered by them.
        canvas.sortingOrder = 90;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1536f, 1024f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        BuildTitleBar(canvasObject.transform);
        BuildHeader(canvasObject.transform);
        BuildLegend(canvasObject.transform);
        BuildPlantStatus(canvasObject.transform);
        BuildKpiStrip(canvasObject.transform);
        BuildPagePanels(canvasObject.transform);
        BuildFooter(canvasObject.transform);
        BuildHelpPanel(canvasObject.transform);
        BuildAnalyticsWindow(canvasObject.transform);
        BuildEducationalBadge(canvasObject.transform);
    }

    private const float TitleBarHeight = 26f;

    /// <summary>
    /// Slim OS-style window chrome above the functional header — app name/icon on the
    /// left, a single close (X) button on the right. Replaces the old inline EXIT button
    /// so the whole app reads as one window with a real title bar, matching the analytics
    /// popup's own title bar.
    /// </summary>
    private void BuildTitleBar(Transform parent)
    {
        RectTransform titleBar = CreatePanel("App Title Bar", parent, new Color32(6, 15, 21, 255));
        Pin(titleBar, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -TitleBarHeight), Vector2.zero);

        Text label = CreateText("App Title", titleBar, "POWER-TO-METHANOL DIGITAL TWIN", 11, FontStyle.Bold, TextAnchor.MiddleLeft, MutedTextColor);
        Pin(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-40f, 0f));

        Button close = CreateButton("App Close", titleBar, "X", new Color32(6, 15, 21, 255), 12);
        Pin(close.GetComponent<RectTransform>(), new Vector2(1f, 0f), Vector2.one, new Vector2(-30f, 2f), new Vector2(-2f, -2f));
        close.onClick.AddListener(Quit);
    }

    private void BuildHeader(Transform parent)
    {
        RectTransform header = CreatePanel("Header", parent, HeaderColor);
        Pin(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -TitleBarHeight - 76f), new Vector2(0f, -TitleBarHeight));

        Text title = CreateText("Title", header, "POWER-TO-METHANOL DIGITAL TWIN", 21, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        Pin(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.31f, 1f), new Vector2(24f, 0f), new Vector2(-8f, 0f));

        Text subtitle = CreateText("Subtitle", header, "Interactive educational process visualization", 11, FontStyle.Normal, TextAnchor.LowerLeft, MutedTextColor);
        Pin(subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(0.31f, 0.48f), new Vector2(25f, 4f), new Vector2(-8f, 0f));

        string[] names = { "OVERVIEW", "PROCESS MAP", "FLOW LAB", "REACTOR LAB", "ANALYTICS", "SIMULATION" };
        DashboardPage[] pages =
        {
            DashboardPage.Overview, DashboardPage.Process, DashboardPage.FlowInspection,
            DashboardPage.Equipment, DashboardPage.Analytics, DashboardPage.Simulation
        };
        float left = 0.31f;
        float width = 0.092f;
        for (int i = 0; i < names.Length; i++)
        {
            DashboardPage page = pages[i];
            Button button = CreateButton(names[i], header, names[i], i == 0 ? AccentColor : HeaderColor, 11);
            Pin(button.GetComponent<RectTransform>(), new Vector2(left + width * i, 0f), new Vector2(left + width * (i + 1), 1f), Vector2.zero, Vector2.zero);
            if (page == DashboardPage.Analytics)
            {
                analyticsNavButton = button;
                button.onClick.AddListener(ToggleAnalyticsWindow);
            }
            else
            {
                button.onClick.AddListener(() => SelectPage(page));
            }
            navigationImages.Add(button.GetComponent<Image>());
            navigationPages.Add(page);
        }

        Button help = CreateButton("Help", header, "HELP", HeaderColor, 11);
        Pin(help.GetComponent<RectTransform>(), new Vector2(0.87f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        help.onClick.AddListener(() => SetPopupVisible(helpPanel, !helpPanel.activeSelf));
    }

    private void BuildLegend(Transform parent)
    {
        legendPanel = CreatePanel("Process Flow Legend", parent, PanelColor).gameObject;
        RectTransform panel = legendPanel.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(14f, -90f - TitleBarHeight);
        panel.sizeDelta = new Vector2(238f, 214f);

        AddPanelTitle(panel, "PROCESS FLOW");

        // Grouped, plain-language streams (the raw per-pipe kinds are collapsed here) plus a
        // dashed row for the recycle loop.
        string[] names =
        {
            "Raw Water / H2 Stream",
            "Amine Solvent / Captured CO2",
            "Compressed Syngas (3:1 H2:CO2)",
            "Hot Reactor Effluent",
            "Pure Refined Methanol (>99.85%)",
            "Gas Recycle Loop",
        };
        Color[] colors =
        {
            Hex("38BDF8"), Hex("10B981"), Hex("F59E0B"), Hex("EF4444"), Hex("22C55E"), Hex("F59E0B"),
        };

        for (int i = 0; i < names.Length; i++)
        {
            float y = -50f - i * 26f;
            bool dashed = i == names.Length - 1;
            if (dashed)
            {
                for (int d = 0; d < 4; d++)
                {
                    RectTransform dash = CreatePanel("Recycle Dash " + d, panel, colors[i]);
                    AnchorTopLeft(dash, new Vector2(14f + d * 10f, y), new Vector2(6f, 4f));
                }
            }
            else
            {
                RectTransform swatch = CreatePanel(names[i] + " Swatch", panel, colors[i]);
                AnchorTopLeft(swatch, new Vector2(14f, y), new Vector2(20f, 8f));
            }
            Text label = CreateText(names[i], panel, names[i], 11, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
            AnchorTopLeft(label.rectTransform, new Vector2(44f, y + 6f), new Vector2(188f, 20f));
        }
    }

    private void BuildPlantStatus(Transform parent)
    {
        RectTransform panel = CreatePanel("Plant Status", parent, PanelColor);
        plantStatusPanel = panel.gameObject;
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-16f, -94f - TitleBarHeight);
        panel.sizeDelta = new Vector2(340f, 210f);
        AddPanelTitle(panel, "PLANT STATUS");

        plantStatusText = CreateText("Status", panel, "● Normal operation", 12, FontStyle.Normal, TextAnchor.MiddleRight, HealthyColor);
        AnchorTopRight(plantStatusText.rectTransform, new Vector2(-16f, -16f), new Vector2(180f, 28f));
        efficiencyText = AddStatusRow(panel, "Plant efficiency", -54f);
        productionText = AddStatusRow(panel, "Methanol production", -79f);
        utilizationText = AddStatusRow(panel, "CO2 utilization", -104f);
        methanolTankText = AddStatusRow(panel, "Methanol tank", -129f);

        Button maxEfficiency = CreateButton("Set Maximum Efficiency", panel, "SET MAXIMUM EFFICIENCY", Hex("1E7A46"), 11);
        Pin(maxEfficiency.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 12f), new Vector2(-14f, 42f));
        maxEfficiency.onClick.AddListener(ApplyMaximumEfficiency);
    }

    /// <summary>
    /// One-click "best case": drives every module slider to the setpoint that maximises the
    /// plant's overall efficiency in the educational model (reactor at the yield peak, full
    /// pressure/recycle, and the separation train at maximum recovery).
    /// </summary>
    private void ApplyMaximumEfficiency()
    {
        InteractiveModulePanelRuntime panels = FindFirstObjectByType<InteractiveModulePanelRuntime>(FindObjectsInactive.Include);
        panels?.ApplyMaximumEfficiencyPreset();
        PlantProcessSimulator.Instance?.Play();
        Refresh();
    }

    private void BuildKpiStrip(Transform parent)
    {
        RectTransform strip = CreatePanel("Process KPI Strip", parent, PanelColor);
        kpiStrip = strip.gameObject;
        Pin(strip, new Vector2(0.01f, 0f), new Vector2(0.99f, 0f), new Vector2(0f, 66f), new Vector2(0f, 190f));
        string[] titles = { "ELECTROLYZER", "CO2 CAPTURE", "REACTOR", "SEPARATION" };
        Color[] colors = { Hex("22B7E8"), Hex("20C997"), Hex("FF7043"), Hex("A855F7") };
        Text[] targets = new Text[4];
        for (int i = 0; i < 4; i++)
        {
            RectTransform section = CreatePanel(titles[i], strip, i % 2 == 0 ? PanelColor : PanelLightColor);
            Pin(section, new Vector2(i * 0.25f, 0f), new Vector2((i + 1) * 0.25f, 1f), new Vector2(1f, 2f), new Vector2(-1f, -2f));
            Text title = CreateText("Title", section, titles[i], 13, FontStyle.Bold, TextAnchor.UpperLeft, colors[i]);
            Pin(title.rectTransform, new Vector2(0f, 0.66f), Vector2.one, new Vector2(16f, 0f), new Vector2(-8f, -9f));
            targets[i] = CreateText("Values", section, "Loading…", 12, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            Pin(targets[i].rectTransform, Vector2.zero, new Vector2(1f, 0.7f), new Vector2(16f, 8f), new Vector2(-8f, -2f));
        }

        electrolyzerKpis = targets[0];
        captureKpis = targets[1];
        reactorKpis = targets[2];
        separationKpis = targets[3];
        liveTexts.AddRange(targets);
    }

    private void BuildPagePanels(Transform parent)
    {
        processPanel = BuildContextPanel("Guided Process", parent, new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.47f), "GUIDED POWER-TO-METHANOL PROCESS");
        RectTransform process = processPanel.GetComponent<RectTransform>();
        processStepText = CreateText("Step", process, "", 12, FontStyle.Bold, TextAnchor.UpperLeft, AccentColor);
        Pin(processStepText.rectTransform, new Vector2(0f, 0.72f), new Vector2(0.25f, 0.90f), new Vector2(20f, 0f), Vector2.zero);
        processTitleText = CreateText("Process Title", process, "", 20, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        Pin(processTitleText.rectTransform, new Vector2(0.20f, 0.70f), new Vector2(0.70f, 0.92f), Vector2.zero, Vector2.zero);
        processBodyText = CreateText("Explanation", process, "", 13, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        Pin(processBodyText.rectTransform, new Vector2(0f, 0.18f), new Vector2(0.67f, 0.70f), new Vector2(20f, 4f), new Vector2(-10f, 0f));
        processStreamsText = CreateText("Streams", process, "", 12, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
        Pin(processStreamsText.rectTransform, new Vector2(0.68f, 0.18f), new Vector2(1f, 0.70f), new Vector2(8f, 4f), new Vector2(-18f, 0f));
        Button previous = CreateButton("Previous Step", process, "PREVIOUS STEP", HeaderColor, 11);
        Pin(previous.GetComponent<RectTransform>(), new Vector2(0.02f, 0.03f), new Vector2(0.20f, 0.18f), Vector2.zero, Vector2.zero);
        previous.onClick.AddListener(() => SetProcessStep(processStepIndex - 1));
        Button next = CreateButton("Next Step", process, "NEXT STEP", AccentColor, 11);
        Pin(next.GetComponent<RectTransform>(), new Vector2(0.80f, 0.03f), new Vector2(0.98f, 0.18f), Vector2.zero, Vector2.zero);
        next.onClick.AddListener(() => SetProcessStep(processStepIndex + 1));

        equipmentPanel = BuildContextPanel("Reactor Lab", parent, new Vector2(0.69f, 0.25f), new Vector2(0.985f, 0.72f), "REACTOR REACTION LAB");
        RectTransform equipment = equipmentPanel.GetComponent<RectTransform>();
        equipmentSummaryText = AddContextBody(equipment,
            "Inspect the real transparent fixed-bed reactor. Conditioned H2/CO2/recycle gas enters the side feed nozzle, crosses the catalyst volume, and the methanol/water-containing effluent leaves through the top outlet.\n\n" +
            "The catalyst colour indicates operating state; particle motion is an educational species-and-conversion visualization.");
        Button focusReactor = AddContextButton(equipment, "FOCUS REACTOR", 0.20f);
        focusReactor.onClick.AddListener(() => Focus(7));
        Button reactorControls = AddContextButton(equipment, "OPEN REACTOR CONTROLS", 0.08f);
        reactorControls.onClick.AddListener(() => ToggleModulePanels(true));

        simulationPanel = BuildContextPanel("Simulation", parent, new Vector2(0.69f, 0.25f), new Vector2(0.985f, 0.72f), "SIMULATION CONTROLS");
        RectTransform simulation = simulationPanel.GetComponent<RectTransform>();
        simulationSummaryText = AddContextBody(simulation,
            "The established plant controls remain the single source of truth. Slider changes update process calculations, KPIs, storage interlocks and stream animation together.");
        Button openControls = AddContextButton(simulation, "OPEN EXISTING PROCESS CONTROLS", 0.08f);
        openControls.onClick.AddListener(() => ToggleModulePanels(true));

        flowInspectionPanel = BuildContextPanel("Flow Lab", parent, new Vector2(0.69f, 0.21f), new Vector2(0.985f, 0.76f), "ENGINEERING PIPE-FLOW LAB");
        RectTransform flow = flowInspectionPanel.GetComponent<RectTransform>();
        Text flowBody = AddContextBody(flow,
            "Filter the actual plant routes by subsystem. Mixed synthesis gas retains separate H2, CO2 and recycle-species tracers. Direction, packet speed and occupancy remain driven by the calculated plant state.");
        Pin(flowBody.rectTransform, new Vector2(0f, 0.67f), new Vector2(1f, 0.88f), new Vector2(18f, 2f), new Vector2(-18f, 0f));
        AddFlowModeButton(flow, "ALL STREAMS", 0, 0.54f);
        AddFlowModeButton(flow, "FEED GASES", 1, 0.44f);
        AddFlowModeButton(flow, "CAPTURE LOOP", 2, 0.34f);
        AddFlowModeButton(flow, "SYNTHESIS LOOP", 3, 0.24f);
        AddFlowModeButton(flow, "PRODUCT PATH", 4, 0.14f);
        Button toggle = AddContextButton(flow, "SHOW / HIDE STREAM VISUALS", 0.03f);
        toggle.onClick.AddListener(ToggleStreams);

        SetProcessStep(0);
        SelectPage(DashboardPage.Overview);
    }

    private GameObject BuildContextPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string title)
    {
        RectTransform panel = CreatePanel(name, parent, new Color32(9, 29, 41, 246));
        Pin(panel, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        AddPanelTitle(panel, title);
        return panel.gameObject;
    }

    private Text AddContextBody(RectTransform panel, string value)
    {
        Text body = CreateText("Body", panel, value, 13, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        Pin(body.rectTransform, new Vector2(0f, 0.18f), new Vector2(1f, 0.88f), new Vector2(18f, 8f), new Vector2(-18f, 0f));
        return body;
    }

    private Button AddContextButton(RectTransform panel, string label, float bottom)
    {
        Button button = CreateButton(label, panel, label, AccentColor, 11);
        Pin(button.GetComponent<RectTransform>(), new Vector2(0.08f, bottom), new Vector2(0.92f, bottom + 0.10f), Vector2.zero, Vector2.zero);
        return button;
    }

    private void AddFlowModeButton(RectTransform panel, string label, int mode, float bottom)
    {
        Button button = CreateButton(label, panel, label, HeaderColor, 11);
        Pin(button.GetComponent<RectTransform>(), new Vector2(0.08f, bottom), new Vector2(0.92f, bottom + 0.085f), Vector2.zero, Vector2.zero);
        button.onClick.AddListener(() => SetFlowInspectionMode(mode));
    }

    private void AddAnalyticsBar(RectTransform panel, string label, Color color, float y,
        Func<PlantProcessSimulator.ProcessSnapshot, float> valueSelector)
    {
        Text text = CreateText(label, panel, label, 11, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
        Pin(text.rectTransform, new Vector2(0.06f, y), new Vector2(0.42f, y + 0.06f), Vector2.zero, Vector2.zero);
        RectTransform track = CreatePanel(label + " Track", panel, new Color32(48, 71, 82, 255));
        Pin(track, new Vector2(0.43f, y + 0.015f), new Vector2(0.94f, y + 0.045f), Vector2.zero, Vector2.zero);
        Image trackImage = track.GetComponent<Image>();
        RectTransform fill = CreatePanel(label + " Fill", track, color);
        Pin(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fill.GetComponent<Image>().raycastTarget = false;
        analyticsBars.Add(fill.GetComponent<Image>());

        // Value badge — hidden until hover, floats just above the track's right edge.
        RectTransform badge = CreatePanel(label + " Value Badge", panel, new Color(0.02f, 0.05f, 0.07f, 0.97f));
        badge.anchorMin = badge.anchorMax = new Vector2(0.94f, y + 0.045f);
        badge.pivot = new Vector2(1f, 0f);
        badge.anchoredPosition = new Vector2(0f, 6f);
        badge.sizeDelta = new Vector2(66f, 22f);
        Outline badgeOutline = badge.gameObject.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(1f, 1f, 1f, 0.18f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);
        Text valueLabel = CreateText(label + " Value", badge, "", 12, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Pin(valueLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        badge.gameObject.SetActive(false);

        AnalyticsBarHover hover = track.gameObject.AddComponent<AnalyticsBarHover>();
        hover.Track = trackImage;
        hover.BaseColor = trackImage.color;
        hover.HoverColor = Color.Lerp(trackImage.color, Color.white, 0.3f);
        hover.ValueBadge = badge.gameObject;
        hover.ValueLabel = valueLabel;
        hover.ValueSelector = valueSelector;
    }

    /// <summary>Brightens the bar's track and reveals a value badge (kept live-updated
    /// while hovered, so it reflects the current running value rather than a snapshot from
    /// the moment the cursor entered) on hover; reverts on exit.</summary>
    private sealed class AnalyticsBarHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Track;
        public Color BaseColor;
        public Color HoverColor;
        public GameObject ValueBadge;
        public Text ValueLabel;
        public Func<PlantProcessSimulator.ProcessSnapshot, float> ValueSelector;

        private bool hovering;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            if (Track != null) Track.color = HoverColor;
            if (ValueBadge != null) ValueBadge.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            if (Track != null) Track.color = BaseColor;
            if (ValueBadge != null) ValueBadge.SetActive(false);
        }

        private void Update()
        {
            if (!hovering || ValueLabel == null || ValueSelector == null) return;
            PlantProcessSimulator sim = PlantProcessSimulator.Instance;
            if (sim == null) return;
            ValueLabel.text = $"{ValueSelector(sim.Current):F1}%";
        }
    }

    private const int FooterButtonCount = 7;

    private void BuildFooter(Transform parent)
    {
        RectTransform footer = CreatePanel("Footer", parent, HeaderColor);
        Pin(footer, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 62f));
        Button runButton = AddFooterButton(footer, "PAUSE", 0, ToggleRunning);
        runToggleButtons.Add(runButton);
        Button resetButton = AddFooterButton(footer, "RESET", 1, ResetSimulation);
        resetButton.GetComponent<Image>().color = Hex("7A2A2A");
        AddFooterButton(footer, "VIEW INFORMATION", 2, () => SetPopupVisible(helpPanel, true));
        AddFooterButton(footer, "SHOW STREAMS", 3, ToggleStreams);
        AddFooterButton(footer, "PREVIOUS MODULE", 4, () => cameraController?.FocusPrevious());
        AddFooterButton(footer, "NEXT MODULE", 5, () => cameraController?.FocusNext());
        AddFooterButton(footer, "RESET VIEW", 6, () => cameraController?.FocusOverview());
    }

    private void BuildHelpPanel(Transform parent)
    {
        helpPanel = CreatePanel("Help Panel", parent, new Color32(9, 26, 36, 250)).gameObject;
        RectTransform panel = helpPanel.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(620f, 420f);
        AddPanelTitle(panel, "ABOUT THIS DIGITAL TWIN");
        Text body = CreateText("Body", panel,
            "Explore the Power-to-Methanol process from hydrogen production to methanol storage.\n\n" +
            "• New here? START TUTORIAL below runs a step-by-step tour of every section.\n" +
            "• Use the top navigation or module arrows to focus equipment.\n" +
            "• Select equipment to open educational controls and live values.\n" +
            "• Stream colours show qualitative material movement through the actual pipe routes.\n" +
            "• Arrow keys orbit; A/D pan; W/S zoom; Shift + arrows cycle modules.\n\n" +
            "Important: values and animations are simplified educational representations. " +
            "This application is not CFD, Aspen, industrial control software, or a validated process model.",
            16, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        Pin(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 62f), new Vector2(-28f, -64f));

        Button tutorial = CreateButton("Start Tutorial", panel, "START TUTORIAL", Hex("1E7A46"), 13);
        AnchorBottomLeft(tutorial.GetComponent<RectTransform>(), new Vector2(24f, 18f), new Vector2(200f, 36f));
        tutorial.onClick.AddListener(StartTutorial);

        Button close = CreateButton("Close", panel, "CLOSE", AccentColor, 13);
        AnchorBottomRight(close.GetComponent<RectTransform>(), new Vector2(-24f, 18f), new Vector2(120f, 36f));
        close.onClick.AddListener(() => SetPopupVisible(helpPanel, false));
        helpPanel.SetActive(false);
    }

    /// <summary>Closes the about box and hands over to the guided tour.</summary>
    private void StartTutorial()
    {
        SetPopupVisible(helpPanel, false);
        TutorialRuntime.Instance?.StartTutorial();
    }

    private void BuildAnalyticsWindow(Transform parent)
    {
        analyticsWindow = CreatePanel("Analytics Window", parent, new Color32(9, 29, 41, 250)).gameObject;
        RectTransform win = analyticsWindow.GetComponent<RectTransform>();
        win.anchorMin = win.anchorMax = new Vector2(0.5f, 0.5f);
        win.pivot = new Vector2(0.5f, 0.5f);
        win.sizeDelta = new Vector2(1180f, 780f);
        win.anchoredPosition = Vector2.zero;

        Outline outline = analyticsWindow.AddComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Title bar doubles as the drag handle, like a normal OS window.
        RectTransform titleBar = CreatePanel("Title Bar", win, HeaderColor);
        Pin(titleBar, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -40f), Vector2.zero);
        Text titleText = CreateText("Window Title", titleBar, "ANALYTICS & INSIGHTS", 14, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        Pin(titleText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-206f, 0f));

        WindowDragHandle drag = titleBar.gameObject.AddComponent<WindowDragHandle>();
        drag.target = win;
        drag.canvas = canvas;

        // Available regardless of which tab (Stats/Visualise) is active, so the window is
        // fully self-contained for controlling the run.
        Button windowRun = CreateButton("Window Run Toggle", titleBar, "PAUSE", AccentColor, 11);
        Pin(windowRun.GetComponent<RectTransform>(), new Vector2(1f, 0f), Vector2.one, new Vector2(-200f, 6f), new Vector2(-102f, -6f));
        windowRun.onClick.AddListener(ToggleRunning);
        runToggleButtons.Add(windowRun);

        Button windowReset = CreateButton("Window Reset", titleBar, "RESET", Hex("7A2A2A"), 11);
        Pin(windowReset.GetComponent<RectTransform>(), new Vector2(1f, 0f), Vector2.one, new Vector2(-98f, 6f), new Vector2(-46f, -6f));
        windowReset.onClick.AddListener(ResetSimulation);

        Button close = CreateButton("Close Window", titleBar, "X", HeaderColor, 14);
        Pin(close.GetComponent<RectTransform>(), new Vector2(1f, 0f), Vector2.one, new Vector2(-40f, 4f), new Vector2(-4f, -4f));
        close.onClick.AddListener(CloseAnalyticsWindow);

        // Tab bar
        RectTransform tabBar = CreatePanel("Tab Bar", win, PanelColor);
        Pin(tabBar, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -76f), new Vector2(0f, -40f));

        analyticsStatsTabButton = CreateButton("Stats Tab", tabBar, "STATS", AccentColor, 12);
        Pin(analyticsStatsTabButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f));
        analyticsStatsTabButton.onClick.AddListener(() => SetAnalyticsTab(AnalyticsTab.Stats));

        analyticsVisualiseTabButton = CreateButton("Visualise Tab", tabBar, "VISUALISE", HeaderColor, 12);
        Pin(analyticsVisualiseTabButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
        analyticsVisualiseTabButton.onClick.AddListener(() => SetAnalyticsTab(AnalyticsTab.Visualise));

        // Content area
        RectTransform content = CreatePanel("Content", win, new Color32(9, 29, 41, 0));
        Pin(content, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -76f));

        analyticsStatsTab = new GameObject("Stats Tab Content");
        analyticsStatsTab.transform.SetParent(content, false);
        RectTransform statsRect = analyticsStatsTab.AddComponent<RectTransform>();
        Pin(statsRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        analyticsSummaryText = CreateText("Analytics Summary", statsRect, "", 13, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        Pin(analyticsSummaryText.rectTransform, new Vector2(0f, 0.80f), Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, -10f));

        string[] metricNames = { "Overall efficiency", "CO2 capture", "Reactor yield", "Methanol purity", "Storage fill" };
        Color[] metricColors = { AccentColor, Hex("20C997"), Hex("FF7043"), Hex("A855F7"), Hex("EBFF33") };
        Func<PlantProcessSimulator.ProcessSnapshot, float>[] metricSelectors =
        {
            s => s.overallEfficiencyPercent,
            s => s.captureEfficiencyPercent,
            s => s.reactorYieldPercent,
            s => s.methanolPurityPercent,
            s => s.storageFillPercent
        };
        for (int i = 0; i < metricNames.Length; i++) AddAnalyticsBar(statsRect, metricNames[i], metricColors[i], 0.66f - i * 0.10f, metricSelectors[i]);

        analyticsInsightsText = CreateText("Insights", statsRect, "", 12, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
        Pin(analyticsInsightsText.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.15f), new Vector2(18f, 0f), new Vector2(-18f, 0f));

        analyticsVisualiseTab = new GameObject("Visualise Tab Content");
        analyticsVisualiseTab.transform.SetParent(content, false);
        RectTransform visRect = analyticsVisualiseTab.AddComponent<RectTransform>();
        Pin(visRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform subTabBar = CreatePanel("Visualise Sub Tabs", visRect, PanelColor);
        Pin(subTabBar, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -30f), Vector2.zero);
        string[] subTabNames = { "REACTOR YIELD", "EFFICIENCY", "OFAT TIMELINE", "LIVE PROGRESS" };
        subTabButtons.Clear();
        for (int i = 0; i < subTabNames.Length; i++)
        {
            int idx = i;
            Button b = CreateButton("SubTab " + i, subTabBar, subTabNames[i], i == 0 ? AccentColor : HeaderColor, 11);
            Pin(b.GetComponent<RectTransform>(), new Vector2(i * 0.25f, 0f), new Vector2((i + 1) * 0.25f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f));
            b.onClick.AddListener(() => SelectSubTab((VisualiseSubTab)idx));
            subTabButtons.Add(b);
        }

        RectTransform subContent = CreatePanel("Visualise Sub Content", visRect, PanelLightColor);
        Pin(subContent, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -30f));

        float designMethanol = PlantProcessSimulator.Instance != null ? PlantProcessSimulator.Instance.DesignMethanolKgH : 1250f;

        subTabGroups[0] = BuildCorrelationSubTab(subContent, "Reactor Yield", "Reactor Yield (%)",
            s => s.reactorYieldPercent, yieldParamButtons, yieldGraphGroups, true);
        subTabGroups[1] = BuildCorrelationSubTab(subContent, "Efficiency", "Overall Efficiency (%)",
            s => s.overallEfficiencyPercent, efficiencyParamButtons, efficiencyGraphGroups, false);
        subTabGroups[2] = BuildOfatTimelineSubTab(subContent);
        subTabGroups[3] = BuildLiveSubTab(subContent, designMethanol);

        SelectYieldParam(0);
        SelectEfficiencyParam(0);
        SelectSubTab(VisualiseSubTab.Yield);
        SetAnalyticsTab(AnalyticsTab.Stats);
        analyticsWindow.SetActive(false);
    }

    private CanvasGroup BuildCorrelationSubTab(RectTransform parent, string kind, string yLabel,
        Func<PlantProcessSimulator.ProcessSnapshot, float> ySelector,
        List<Button> paramButtons, List<CanvasGroup> graphGroups, bool isYield)
    {
        GameObject panel = new GameObject(kind + " Sub Panel", typeof(RectTransform));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        Pin(panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CanvasGroup group = AddHiddenCanvasGroup(panel);

        RectTransform paramRow = CreatePanel(kind + " Param Row", panelRect, PanelColor);
        Pin(paramRow, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -26f), Vector2.zero);
        Text lbl = CreateText(kind + " Param Label", paramRow, "PARAMETER:", 9, FontStyle.Bold, TextAnchor.MiddleLeft, MutedTextColor);
        Pin(lbl.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(78f, 0f));

        RectTransform graphHost = new GameObject("Graph Host", typeof(RectTransform)).GetComponent<RectTransform>();
        graphHost.SetParent(panelRect, false);
        Pin(graphHost, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -26f));

        float bw = (1f - 0.09f) / ReactorParamNames.Length;
        for (int p = 0; p < ReactorParamNames.Length; p++)
        {
            int pi = p;
            Button b = CreateButton(kind + " Param " + p, paramRow, ReactorParamNames[p], p == 0 ? AccentColor : HeaderColor, 9);
            Pin(b.GetComponent<RectTransform>(), new Vector2(0.09f + p * bw, 0f), new Vector2(0.09f + (p + 1) * bw, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f));
            if (isYield) b.onClick.AddListener(() => SelectYieldParam(pi));
            else b.onClick.AddListener(() => SelectEfficiencyParam(pi));
            paramButtons.Add(b);

            CanvasGroup cg = BuildCorrelationGraph(graphHost, $"{kind} vs {ReactorParamNames[p]}",
                ReactorParamAxisLabels[p], yLabel, ReactorParamSelectors[p], ySelector,
                ReactorParamMin[p], ReactorParamMax[p], 0f, 100f);
            graphGroups.Add(cg);
        }

        return group;
    }

    private CanvasGroup BuildOfatTimelineSubTab(RectTransform parent)
    {
        GameObject panel = new GameObject("OFAT Timeline Sub Panel", typeof(RectTransform));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        Pin(panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CanvasGroup group = AddHiddenCanvasGroup(panel);

        GameObject go = new GameObject("OFAT Timeline Graph", typeof(RectTransform));
        ofatTimeline = go.AddComponent<OfatTimelineGraphRuntime>();
        ofatTimeline.Initialize(panelRect, canvas, font);
        return group;
    }

    private CanvasGroup BuildLiveSubTab(RectTransform parent, float designMethanol)
    {
        GameObject panel = new GameObject("Live Sub Panel", typeof(RectTransform));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        Pin(panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CanvasGroup group = AddHiddenCanvasGroup(panel);

        BuildLiveProgressContent(panelRect, designMethanol);
        return group;
    }

    private void SelectSubTab(VisualiseSubTab tab)
    {
        currentSubTab = tab;
        for (int i = 0; i < subTabGroups.Length; i++)
        {
            if (subTabGroups[i] == null) continue;
            bool visible = (int)tab == i;
            subTabGroups[i].alpha = visible ? 1f : 0f;
            subTabGroups[i].interactable = visible;
            subTabGroups[i].blocksRaycasts = visible;
        }
        for (int i = 0; i < subTabButtons.Count; i++)
            if (subTabButtons[i] != null) subTabButtons[i].GetComponent<Image>().color = (int)tab == i ? AccentColor : HeaderColor;
        ApplyReactorLock();
    }

    private void SelectYieldParam(int index)
    {
        if (!SelectCorrelationParam(index, yieldParamButtons, yieldGraphGroups)) return;
        yieldParamIndex = index;
        if (currentSubTab == VisualiseSubTab.Yield) ApplyReactorLock();
    }

    private void SelectEfficiencyParam(int index)
    {
        if (!SelectCorrelationParam(index, efficiencyParamButtons, efficiencyGraphGroups)) return;
        efficiencyParamIndex = index;
        if (currentSubTab == VisualiseSubTab.Efficiency) ApplyReactorLock();
    }

    private bool SelectCorrelationParam(int index, List<Button> buttons, List<CanvasGroup> groups)
    {
        if (index < 0 || index >= groups.Count) return false;
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i] != null) buttons[i].GetComponent<Image>().color = i == index ? AccentColor : HeaderColor;
        for (int i = 0; i < groups.Count; i++)
        {
            bool visible = i == index;
            groups[i].alpha = visible ? 1f : 0f;
            groups[i].interactable = visible;
            groups[i].blocksRaycasts = visible;
        }
        return true;
    }

    private InteractiveModulePanelRuntime ModulePanels()
    {
        if (modulePanels == null) modulePanels = FindFirstObjectByType<InteractiveModulePanelRuntime>(FindObjectsInactive.Include);
        return modulePanels;
    }

    /// <summary>
    /// Single source of truth for the reactor variable-lock. The REACTOR YIELD / EFFICIENCY
    /// param tabs and the OFAT timeline all lock the reactor to a single moving slider while
    /// their sub-tab is on screen; everything else releases it.
    /// </summary>
    private void ApplyReactorLock()
    {
        bool visualiseOpen = analyticsWindowOpen && currentAnalyticsTab == AnalyticsTab.Visualise;
        bool ofatActive = visualiseOpen && currentSubTab == VisualiseSubTab.Ofat;

        if (ofatTimeline != null) ofatTimeline.SetSubTabVisible(ofatActive);

        InteractiveModulePanelRuntime p = ModulePanels();
        if (p == null || ofatActive) return; // OFAT owns the lock while its tab is up

        if (visualiseOpen && currentSubTab == VisualiseSubTab.Yield)
            p.SetReactorVariableLock(ReactorParamSliderLabels[yieldParamIndex]);
        else if (visualiseOpen && currentSubTab == VisualiseSubTab.Efficiency)
            p.SetReactorVariableLock(ReactorParamSliderLabels[efficiencyParamIndex]);
        else
            p.ClearReactorVariableLock();
    }

    private void ToggleRunning()
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim == null) return;
        if (sim.IsRunning) sim.Pause(); else sim.Play();
        Refresh();
    }

    private void ResetSimulation()
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        sim?.ResetSimulation();
        ModulePanels()?.ResetControlVisuals();
        ApplyReactorLock(); // ResetControlVisuals released the lock — re-assert it for the open tab
        Refresh();
    }

    private void ToggleAnalyticsWindow()
    {
        if (analyticsWindowOpen) CloseAnalyticsWindow();
        else OpenAnalyticsWindow();
    }

    private void OpenAnalyticsWindow()
    {
        analyticsWindowOpen = true;
        if (analyticsWindow != null)
        {
            analyticsWindow.transform.SetAsLastSibling();
            SetPopupVisible(analyticsWindow, true);
        }
        ApplyReactorLock();
        Refresh();
    }

    private void CloseAnalyticsWindow()
    {
        analyticsWindowOpen = false;
        SetPopupVisible(analyticsWindow, false);
        ApplyReactorLock();
        Refresh();
    }

    private readonly Dictionary<GameObject, Coroutine> popupAnimations = new Dictionary<GameObject, Coroutine>();

    /// <summary>Opens/closes a popup (analytics window, help panel) with a quick scale +
    /// fade tween instead of an instant SetActive toggle.</summary>
    private void SetPopupVisible(GameObject popup, bool visible)
    {
        if (popup == null) return;
        if (popupAnimations.TryGetValue(popup, out Coroutine running) && running != null) StopCoroutine(running);
        if (visible) popup.SetActive(true);
        popupAnimations[popup] = StartCoroutine(AnimatePopup(popup, visible));
    }

    private IEnumerator AnimatePopup(GameObject popup, bool opening)
    {
        RectTransform rect = popup.GetComponent<RectTransform>();
        CanvasGroup group = popup.GetComponent<CanvasGroup>();
        if (group == null) group = popup.AddComponent<CanvasGroup>();

        const float duration = 0.16f;
        Vector3 fromScale = opening ? Vector3.one * 0.92f : rect.localScale;
        Vector3 toScale = opening ? Vector3.one : Vector3.one * 0.92f;
        float fromAlpha = group.alpha;
        float toAlpha = opening ? 1f : 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, p);
            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, p);
            yield return null;
        }

        rect.localScale = toScale;
        group.alpha = toAlpha;
        if (!opening) popup.SetActive(false);
        popupAnimations.Remove(popup);
    }

    private void SetAnalyticsTab(AnalyticsTab tab)
    {
        currentAnalyticsTab = tab;
        if (analyticsStatsTab != null) analyticsStatsTab.SetActive(tab == AnalyticsTab.Stats);
        if (analyticsVisualiseTab != null) analyticsVisualiseTab.SetActive(tab == AnalyticsTab.Visualise);
        if (analyticsStatsTabButton != null) analyticsStatsTabButton.GetComponent<Image>().color = tab == AnalyticsTab.Stats ? AccentColor : HeaderColor;
        if (analyticsVisualiseTabButton != null) analyticsVisualiseTabButton.GetComponent<Image>().color = tab == AnalyticsTab.Visualise ? AccentColor : HeaderColor;
        // The reactor-slider lock only applies while a locking sub-tab is actually on screen.
        ApplyReactorLock();
    }

    private CanvasGroup BuildCorrelationGraph(RectTransform container, string title, string xLabel, string yLabel,
        Func<PlantProcessSimulator.ProcessSnapshot, float> xSelector,
        Func<PlantProcessSimulator.ProcessSnapshot, float> ySelector,
        float xMin, float xMax, float yMin, float yMax)
    {
        GameObject go = new GameObject(title + " Graph", typeof(RectTransform));
        CorrelationGraphRuntime graph = go.AddComponent<CorrelationGraphRuntime>();
        graph.Title = title;
        graph.XLabel = xLabel;
        graph.YLabel = yLabel;
        graph.XSelector = xSelector;
        graph.YSelector = ySelector;
        graph.XMin = xMin;
        graph.XMax = xMax;
        graph.YMin = yMin;
        graph.YMax = yMax;
        graph.Initialize(container, canvas, font);

        return AddHiddenCanvasGroup(go);
    }

    /// <summary>
    /// The "Live Progress" sub-tab: a small EFFICIENCY / METHANOL OUTPUT toggle row above two
    /// stacked LiveGraphRuntime instances (both keep sampling continuously in the background
    /// via their own CanvasGroup so switching never loses history). The output graph gets its
    /// area shaded and shows the tank's cumulative stored amount on hover.
    /// </summary>
    private void BuildLiveProgressContent(RectTransform container, float designMethanol)
    {
        RectTransform toggleRow = CreatePanel("Sub Toggle Row", container, PanelColor);
        Pin(toggleRow, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -28f), Vector2.zero);

        liveProgressEfficiencyButton = CreateButton("Efficiency Toggle", toggleRow, "EFFICIENCY", AccentColor, 11);
        Pin(liveProgressEfficiencyButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f));
        liveProgressEfficiencyButton.onClick.AddListener(() => SelectLiveProgressMode(true));

        liveProgressOutputButton = CreateButton("Output Toggle", toggleRow, "METHANOL OUTPUT", HeaderColor, 11);
        Pin(liveProgressOutputButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
        liveProgressOutputButton.onClick.AddListener(() => SelectLiveProgressMode(false));

        RectTransform graphHost = new GameObject("Graph Host", typeof(RectTransform)).GetComponent<RectTransform>();
        graphHost.SetParent(container, false);
        Pin(graphHost, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -28f));

        liveProgressEfficiencyGraph = BuildLiveGraphInstance(graphHost, "Overall Efficiency", "Reactor Temp", "°C", "Overall Efficiency (%)",
            s => s.reactorTemperatureC, s => s.overallEfficiencyPercent, 0f, 100f, false, null, "", null);

        liveProgressOutputGraph = BuildLiveGraphInstance(graphHost, "Methanol Output", "Reactor Temp", "°C", "Methanol Output (kg/h)",
            s => s.reactorTemperatureC, s => s.methanolProductionKgH, 0f, designMethanol, true, "Tank stored", "kg", s => s.storedMethanolKg);

        SelectLiveProgressMode(true);
    }

    private LiveGraphRuntime BuildLiveGraphInstance(RectTransform container, string title, string xLabel, string xUnit, string yLabel,
        Func<PlantProcessSimulator.ProcessSnapshot, float> xSelector,
        Func<PlantProcessSimulator.ProcessSnapshot, float> ySelector,
        float yMin, float yMax, bool shadeArea, string secondaryLabel, string secondaryUnit,
        Func<PlantProcessSimulator.ProcessSnapshot, float> secondarySelector)
    {
        GameObject go = new GameObject(title + " Graph", typeof(RectTransform));
        LiveGraphRuntime graph = go.AddComponent<LiveGraphRuntime>();
        graph.Title = title;
        graph.XLabel = xLabel;
        graph.XUnit = xUnit;
        graph.YLabel = yLabel;
        graph.XSelector = xSelector;
        graph.YSelector = ySelector;
        graph.YMin = yMin;
        graph.YMax = yMax;
        graph.ShadeArea = shadeArea;
        graph.SecondaryLabel = secondaryLabel;
        graph.SecondaryUnit = secondaryUnit;
        graph.SecondarySelector = secondarySelector;
        graph.Initialize(container, canvas, font);

        AddHiddenCanvasGroup(go);
        return graph;
    }

    private static CanvasGroup AddHiddenCanvasGroup(GameObject go)
    {
        CanvasGroup group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private static void SetCanvasGroupVisible(Component owner, bool visible)
    {
        if (owner == null) return;
        CanvasGroup group = owner.GetComponent<CanvasGroup>();
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private void SelectLiveProgressMode(bool efficiency)
    {
        SetCanvasGroupVisible(liveProgressEfficiencyGraph, efficiency);
        SetCanvasGroupVisible(liveProgressOutputGraph, !efficiency);
        if (liveProgressEfficiencyButton != null) liveProgressEfficiencyButton.GetComponent<Image>().color = efficiency ? AccentColor : HeaderColor;
        if (liveProgressOutputButton != null) liveProgressOutputButton.GetComponent<Image>().color = !efficiency ? AccentColor : HeaderColor;
    }

    /// <summary>Drag-to-move behaviour for the analytics window's title bar, like a normal OS window.</summary>
    private sealed class WindowDragHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public RectTransform target;
        public Canvas canvas;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (target != null) target.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            target.anchoredPosition += eventData.delta / scale;
        }
    }

    private void BuildEducationalBadge(Transform parent)
    {
        Text badge = CreateText("Educational Badge", parent, "EDUCATIONAL VISUALIZATION • SIMPLIFIED PROCESS VALUES", 10, FontStyle.Bold, TextAnchor.MiddleCenter, MutedTextColor);
        Pin(badge.rectTransform, new Vector2(0.33f, 1f), new Vector2(0.67f, 1f), new Vector2(0f, -98f - TitleBarHeight), new Vector2(0f, -76f - TitleBarHeight));
    }

    private void Refresh()
    {
        PlantProcessSimulator simulator = PlantProcessSimulator.Instance;
        if (simulator == null) return;
        PlantProcessSimulator.ProcessSnapshot s = simulator.Current;

        for (int i = 0; i < runToggleButtons.Count; i++)
        {
            Button button = runToggleButtons[i];
            if (button == null) continue;
            Image img = button.GetComponent<Image>();
            if (img != null) img.color = simulator.IsRunning ? Hex("1E7A46") : Hex("B37A18");
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = simulator.IsRunning ? "PAUSE" : "RESUME";
        }

        bool alarm = s.reactorTemperatureC >= 285f || s.reactorPressureBar >= 98f || s.storageFillPercent >= 95f;
        bool caution = !alarm && (s.reactorTemperatureC > 270f || s.captureEfficiencyPercent < 65f || s.methanolPurityPercent < 95f);
        if (!simulator.IsRunning)
        {
            plantStatusText.text = "● Paused";
            plantStatusText.color = Hex("B37A18");
        }
        else
        {
            plantStatusText.text = alarm ? "● Attention required" : caution ? "● Operating caution" : "● Normal operation";
            plantStatusText.color = alarm ? Hex("FF3B30") : caution ? Hex("FFB020") : HealthyColor;
        }
        efficiencyText.text = $"{s.overallEfficiencyPercent:F1}%";
        productionText.text = $"{s.methanolProductionKgH:F0} kg/h";
        // CO2 + 3H2 -> CH3OH + H2O. Each 32 kg of methanol represents
        // 44 kg of CO2 converted on the simplified stoichiometric basis.
        float co2ConvertedKgH = s.methanolProductionKgH * (44f / 32f);
        float co2UtilizationPercent = s.co2CapturedKgH > 0.01f
            ? Mathf.Clamp01(co2ConvertedKgH / s.co2CapturedKgH) * 100f
            : 0f;
        utilizationText.text = $"{co2UtilizationPercent:F1}%";

        float tankSecs = simulator.SecondsUntilStorageFull;
        string tankEta = s.storageFillPercent >= 99.9f ? "full"
            : float.IsInfinity(tankSecs) ? "—"
            : tankSecs >= 5940f ? "99:00+"
            : $"{(int)(tankSecs / 60f):00}:{(int)(tankSecs % 60f):00} left";
        methanolTankText.text = $"{s.storageFillPercent:F1}%  ({tankEta})";

        electrolyzerKpis.text = $"Power {s.electrolyzerPowerPercent:F0}%     H2 {s.h2InputKgH:F0} kg/h\nWater {s.waterFeedKgH:F0} kg/h     O2 {s.oxygenByproductKgH:F0} kg/h";
        captureKpis.text = $"Capture {s.captureEfficiencyPercent:F1}%     CO2 {s.co2CapturedKgH:F0} kg/h\nAmine {s.amineFlowPercent:F0}%     Regen {s.regeneratorTemperatureC:F0} °C";
        reactorKpis.text = $"Temp {s.reactorTemperatureC:F0} °C     Pressure {s.reactorPressureBar:F0} bar\nH2/CO2 {s.h2Co2Ratio:F1}     Yield {s.reactorYieldPercent:F1}%";
        separationKpis.text = $"Recycle {s.recycleRatioPercent:F0}%     Purity {s.methanolPurityPercent:F2}%\nStorage {s.storageFillPercent:F0}%     Product {s.methanolProductionKgH:F0} kg/h";

        for (int i = 0; i < navigationImages.Count; i++)
        {
            if (navigationImages[i] == null) continue;
            bool active = navigationPages[i] == DashboardPage.Analytics
                ? analyticsWindowOpen
                : navigationPages[i] == currentPage;
            navigationImages[i].color = active ? AccentColor : HeaderColor;
        }

        if (equipmentSummaryText != null)
            equipmentSummaryText.text =
                $"Selected process focus: Reactor R-201\n\nTemperature  {s.reactorTemperatureC:F0} °C\n" +
                $"Pressure  {s.reactorPressureBar:F0} bar\nH2/CO2 ratio  {s.h2Co2Ratio:F2}\n" +
                $"Yield  {s.reactorYieldPercent:F1}%\n\n" +
                "The fixed-bed synthesis reactor converts conditioned H2/CO2 syngas to methanol and water. " +
                "Use the selector to inspect every available module and its live controls.";

        if (simulationSummaryText != null)
            simulationSummaryText.text =
                $"Plant load  {s.plantLoadPercent:F0}%\nSyngas feed  {s.syngasFeedKgH:F0} kg/h\n" +
                $"Methanol output  {s.methanolProductionKgH:F0} kg/h\nStorage  {s.storageFillPercent:F1}%\n\n" +
                (s.storageInterlockActive
                    ? "Storage interlock is active: upstream production is constrained until capacity is restored."
                    : "Storage has available capacity. Existing module sliders remain active and calculation-linked.");

        if (analyticsSummaryText != null)
            analyticsSummaryText.text =
                $"Production {s.methanolProductionKgH:F0} kg/h   |   Captured CO2 {s.co2CapturedKgH:F0} kg/h\n" +
                $"Syngas {s.syngasFeedKgH:F0} kg/h   |   Recycle {s.recycleGasKgH:F0} kg/h";
        float[] metrics =
        {
            s.overallEfficiencyPercent, s.captureEfficiencyPercent, s.reactorYieldPercent,
            s.methanolPurityPercent, s.storageFillPercent
        };
        for (int i = 0; i < analyticsBars.Count && i < metrics.Length; i++)
        {
            RectTransform bar = analyticsBars[i].rectTransform;
            bar.anchorMax = new Vector2(Mathf.Clamp01(metrics[i] / 100f), 1f);
            bar.offsetMax = Vector2.zero;
        }
        if (analyticsInsightsText != null)
            analyticsInsightsText.text =
                (s.captureEfficiencyPercent < 80f ? "Insight: CO2 capture is limiting carbon utilization. " : "CO2 capture is operating in the preferred educational range. ") +
                (s.h2Co2Ratio < 2.8f || s.h2Co2Ratio > 3.2f ? "Adjust the synthesis feed ratio toward 3.0. " : "Synthesis feed ratio is near its target. ") +
                (s.storageFillPercent > 85f ? "Storage headroom is low; monitor the interlock." : "Storage headroom is adequate.");
    }

    private void SelectPage(DashboardPage page)
    {
        currentPage = page;
        if (processPanel != null) processPanel.SetActive(page == DashboardPage.Process);
        if (equipmentPanel != null) equipmentPanel.SetActive(page == DashboardPage.Equipment);
        if (simulationPanel != null) simulationPanel.SetActive(page == DashboardPage.Simulation);
        if (flowInspectionPanel != null) flowInspectionPanel.SetActive(page == DashboardPage.FlowInspection);
        if (legendPanel != null) legendPanel.SetActive(page == DashboardPage.Overview || page == DashboardPage.Process || page == DashboardPage.FlowInspection);
        if (plantStatusPanel != null) plantStatusPanel.SetActive(page == DashboardPage.Overview);
        if (kpiStrip != null) kpiStrip.SetActive(page == DashboardPage.Overview || page == DashboardPage.Process || page == DashboardPage.Simulation);
        if (page == DashboardPage.Overview) Focus(-1);
        else if (page == DashboardPage.FlowInspection) Focus(-1);
        else if (page == DashboardPage.Equipment) Focus(7);
        if (page != DashboardPage.FlowInspection) SetFlowInspectionMode(0);
        Refresh();
    }

    private void SetProcessStep(int index)
    {
        string[] titles =
        {
            "Renewable power & water", "Electrolyzer", "Hydrogen storage", "CO2 capture",
            "Gas mixing", "Feed heating", "Methanol reactor", "Cooling & flash separation",
            "Distillation", "Methanol storage"
        };
        string[] descriptions =
        {
            "Renewable electricity and treated water provide the material and energy basis for green hydrogen production.",
            "Water electrolysis produces hydrogen for synthesis and oxygen as a useful by-product.",
            "A hydrogen buffer decouples variable electrolyzer output from the steady synthesis-loop demand.",
            "Regenerable amine absorption separates and conditions carbon dioxide before compression.",
            "Fresh hydrogen, captured CO2 and recycled synthesis gas combine at the mixing junction.",
            "The mixed synthesis gas is brought toward reactor inlet temperature before entering the fixed bed.",
            "The conditioned H2/CO2 mixture passes through the catalyst bed and forms methanol and water.",
            "Reactor effluent is cooled so crude methanol and water condense while unreacted gas remains available for recycle.",
            "Distillation raises methanol purity by separating water and remaining light components.",
            "The product tank accumulates methanol and activates the capacity interlock near its safe operating limit."
        };
        string[] streams =
        {
            "Inputs\n- Renewable power\n- Treated water", "Streams\n- Water\n- Hydrogen\n- Oxygen",
            "Streams\n- Hydrogen", "Streams\n- Flue gas\n- Rich amine\n- Lean amine\n- Captured CO2",
            "Streams\n- Hydrogen\n- CO2\n- Recycle gas\n- Mixed syngas", "Streams\n- Mixed syngas\n- Heated syngas",
            "Streams\n- Hot syngas\n- Reactor effluent", "Streams\n- Reactor effluent\n- Crude methanol\n- Recycle gas",
            "Streams\n- Crude methanol\n- Water-rich bottoms\n- Methanol product", "Streams\n- Methanol product"
        };
        int[] focus = { -1, 0, 1, 2, 6, 6, 7, 9, 10, 11 };
        processStepIndex = Mathf.Clamp(index, 0, titles.Length - 1);
        if (processStepText != null) processStepText.text = $"STEP {processStepIndex + 1} OF {titles.Length}";
        if (processTitleText != null) processTitleText.text = titles[processStepIndex].ToUpperInvariant();
        if (processBodyText != null) processBodyText.text = descriptions[processStepIndex];
        if (processStreamsText != null) processStreamsText.text = streams[processStepIndex];
        Focus(focus[processStepIndex]);
    }

    private void Focus(int index)
    {
        if (cameraController == null) cameraController = FindFirstObjectByType<OrbitCameraController>();
        if (cameraController == null) return;
        if (index < 0) cameraController.FocusOverview();
        else cameraController.FocusModule(index);
    }

    private void ToggleStreams()
    {
        FinalPlantFlowRuntime flow = FindFirstObjectByType<FinalPlantFlowRuntime>(FindObjectsInactive.Include);
        if (flow != null) flow.ToggleVisuals();
    }

    private void SetFlowInspectionMode(int mode)
    {
        FinalPlantFlowRuntime flow = FindFirstObjectByType<FinalPlantFlowRuntime>(FindObjectsInactive.Include);
        if (flow != null) flow.SetInspectionMode(mode);
    }

    private void ToggleModulePanels(bool visible)
    {
        InteractiveModulePanelRuntime panels = FindFirstObjectByType<InteractiveModulePanelRuntime>(FindObjectsInactive.Include);
        if (panels != null) panels.SetSelectionVisible(visible);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Text AddStatusRow(RectTransform panel, string label, float y)
    {
        Text labelText = CreateText(label + " Label", panel, label, 12, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
        AnchorTopLeft(labelText.rectTransform, new Vector2(16f, y), new Vector2(150f, 24f));
        Text value = CreateText(label + " Value", panel, "—", 12, FontStyle.Bold, TextAnchor.MiddleRight, Color.white);
        AnchorTopRight(value.rectTransform, new Vector2(-16f, y), new Vector2(180f, 24f));
        return value;
    }

    private Button AddFooterButton(RectTransform footer, string label, int index, Action action)
    {
        Button button = CreateButton(label, footer, label, HeaderColor, 11);
        float width = 1f / FooterButtonCount;
        Pin(button.GetComponent<RectTransform>(), new Vector2(index * width, 0f), new Vector2((index + 1) * width, 1f), new Vector2(1f, 2f), new Vector2(-1f, -2f));
        button.onClick.AddListener(() => action());
        return button;
    }

    private void AddPanelTitle(RectTransform panel, string title)
    {
        Text text = CreateText("Panel Title", panel, title, 13, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        Pin(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -42f), new Vector2(-12f, -8f));
    }

    private RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        Image image = go.AddComponent<Image>();
        image.color = color;
        return rect;
    }

    private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Color color, int fontSize)
    {
        RectTransform rect = CreatePanel(name, parent, color);
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.selectedColor = AccentColor;
        button.colors = colors;
        Text text = CreateText("Label", rect, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Pin(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 4f), new Vector2(-5f, -4f));
        // Every button built through this one shared helper — nav, footer, tabs, window
        // controls, graph picker — automatically gets a smooth hover/press scale response
        // on top of Unity's built-in color tint, without touching each call site.
        rect.gameObject.AddComponent<UIHoverScale>();
        return button;
    }

    /// <summary>Smooth hover-grow / press-shrink feedback, framerate-independent via an
    /// exponential lerp toward the target scale so it never feels sudden or stuttery.</summary>
    private sealed class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float LerpSpeed = 14f;
        private RectTransform rect;
        private Vector3 baseScale;
        private float targetMultiplier = 1f;

        private void Awake()
        {
            rect = (RectTransform)transform;
            baseScale = rect.localScale;
        }

        private void Update()
        {
            if (rect == null) return;
            Vector3 target = baseScale * targetMultiplier;
            rect.localScale = Vector3.Lerp(rect.localScale, target, 1f - Mathf.Exp(-LerpSpeed * Time.unscaledDeltaTime));
        }

        public void OnPointerEnter(PointerEventData eventData) => targetMultiplier = 1.045f;
        public void OnPointerExit(PointerEventData eventData) => targetMultiplier = 1f;
        public void OnPointerDown(PointerEventData eventData) => targetMultiplier = 0.94f;
        public void OnPointerUp(PointerEventData eventData) => targetMultiplier = 1.045f;
    }

    private static Color Hex(string rgb)
    {
        return ColorUtility.TryParseHtmlString("#" + rgb, out Color color) ? color : Color.white;
    }

    private static void Pin(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void AnchorTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AnchorTopRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AnchorBottomRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AnchorBottomLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
