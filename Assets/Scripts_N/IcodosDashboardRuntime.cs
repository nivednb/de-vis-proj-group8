using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the ICODOS-inspired full-plant presentation at runtime.
/// The values are supplied by PlantProcessSimulator and remain educational estimates.
/// </summary>
[DisallowMultipleComponent]
public sealed class IcodosDashboardRuntime : MonoBehaviour
{
    private enum DashboardPage { Overview, Process, Equipment, Simulation, Analytics, FlowInspection }

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
    private GameObject analyticsPanel;
    private GameObject flowInspectionPanel;
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
    private Text electrolyzerKpis;
    private Text captureKpis;
    private Text reactorKpis;
    private Text separationKpis;
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<IcodosDashboardRuntime>() != null) return;
        new GameObject(RuntimeRootName).AddComponent<IcodosDashboardRuntime>();
    }

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        cameraController = FindFirstObjectByType<OrbitCameraController>();
        HideLegacyDashboard();
        Build();
        Refresh();
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

        GameObject canvasObject = new GameObject("ICODOS Dashboard Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1536f, 1024f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        BuildHeader(canvasObject.transform);
        BuildLegend(canvasObject.transform);
        BuildPlantStatus(canvasObject.transform);
        BuildKpiStrip(canvasObject.transform);
        BuildPagePanels(canvasObject.transform);
        BuildFooter(canvasObject.transform);
        BuildHelpPanel(canvasObject.transform);
        BuildEducationalBadge(canvasObject.transform);
    }

    private void BuildHeader(Transform parent)
    {
        RectTransform header = CreatePanel("Header", parent, HeaderColor);
        Pin(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -76f), Vector2.zero);

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
            button.onClick.AddListener(() => SelectPage(page));
            navigationImages.Add(button.GetComponent<Image>());
            navigationPages.Add(page);
        }

        Button help = CreateButton("Help", header, "HELP", HeaderColor, 11);
        Pin(help.GetComponent<RectTransform>(), new Vector2(0.87f, 0f), new Vector2(0.935f, 1f), Vector2.zero, Vector2.zero);
        help.onClick.AddListener(() => helpPanel.SetActive(!helpPanel.activeSelf));

        Button exit = CreateButton("Exit", header, "EXIT", HeaderColor, 11);
        Pin(exit.GetComponent<RectTransform>(), new Vector2(0.935f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        exit.onClick.AddListener(Quit);
    }

    private void BuildLegend(Transform parent)
    {
        legendPanel = CreatePanel("Process Flow Legend", parent, PanelColor).gameObject;
        RectTransform panel = legendPanel.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(14f, -90f);
        panel.sizeDelta = new Vector2(178f, 288f);

        AddPanelTitle(panel, "PROCESS FLOW");
        string[] names = { "Hydrogen", "CO2", "Rich amine", "Lean amine", "Mixed syngas", "Hot syngas", "Reactor effluent", "Crude methanol", "Methanol product" };
        Color[] colors =
        {
            Hex("33FF40"), Hex("B3EBFF"), Hex("00D9B8"), Hex("00FF8C"), Hex("EBFF33"),
            Hex("FF730D"), Hex("FF2E0F"), Hex("2E99FF"), Hex("B84DFF")
        };

        for (int i = 0; i < names.Length; i++)
        {
            float y = -52f - i * 24f;
            RectTransform swatch = CreatePanel(names[i] + " Swatch", panel, colors[i]);
            AnchorTopLeft(swatch, new Vector2(17f, y), new Vector2(18f, 8f));
            Text label = CreateText(names[i], panel, names[i], 12, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
            AnchorTopLeft(label.rectTransform, new Vector2(45f, y + 5f), new Vector2(122f, 20f));
        }
    }

    private void BuildPlantStatus(Transform parent)
    {
        RectTransform panel = CreatePanel("Plant Status", parent, PanelColor);
        plantStatusPanel = panel.gameObject;
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-16f, -94f);
        panel.sizeDelta = new Vector2(340f, 142f);
        AddPanelTitle(panel, "PLANT STATUS");

        plantStatusText = CreateText("Status", panel, "● Normal operation", 12, FontStyle.Normal, TextAnchor.MiddleRight, HealthyColor);
        AnchorTopRight(plantStatusText.rectTransform, new Vector2(-16f, -16f), new Vector2(180f, 28f));
        efficiencyText = AddStatusRow(panel, "Plant efficiency", -58f);
        productionText = AddStatusRow(panel, "Methanol production", -85f);
        utilizationText = AddStatusRow(panel, "CO2 utilization", -112f);
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

        analyticsPanel = BuildContextPanel("Analytics", parent, new Vector2(0.65f, 0.20f), new Vector2(0.985f, 0.76f), "ANALYTICS & INSIGHTS");
        RectTransform analytics = analyticsPanel.GetComponent<RectTransform>();
        analyticsSummaryText = CreateText("Analytics Summary", analytics, "", 13, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        Pin(analyticsSummaryText.rectTransform, new Vector2(0f, 0.64f), new Vector2(1f, 0.89f), new Vector2(18f, 0f), new Vector2(-18f, 0f));
        string[] metricNames = { "Overall efficiency", "CO2 capture", "Reactor yield", "Methanol purity", "Storage fill" };
        Color[] metricColors = { AccentColor, Hex("20C997"), Hex("FF7043"), Hex("A855F7"), Hex("EBFF33") };
        for (int i = 0; i < metricNames.Length; i++) AddAnalyticsBar(analytics, metricNames[i], metricColors[i], 0.58f - i * 0.085f);
        analyticsInsightsText = CreateText("Insights", analytics, "", 12, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
        Pin(analyticsInsightsText.rectTransform, new Vector2(0f, 0.04f), new Vector2(1f, 0.22f), new Vector2(18f, 0f), new Vector2(-18f, 0f));

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

    private void AddAnalyticsBar(RectTransform panel, string label, Color color, float y)
    {
        Text text = CreateText(label, panel, label, 11, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
        Pin(text.rectTransform, new Vector2(0.06f, y), new Vector2(0.42f, y + 0.06f), Vector2.zero, Vector2.zero);
        RectTransform track = CreatePanel(label + " Track", panel, new Color32(48, 71, 82, 255));
        Pin(track, new Vector2(0.43f, y + 0.015f), new Vector2(0.94f, y + 0.045f), Vector2.zero, Vector2.zero);
        RectTransform fill = CreatePanel(label + " Fill", track, color);
        Pin(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        analyticsBars.Add(fill.GetComponent<Image>());
    }

    private void BuildFooter(Transform parent)
    {
        RectTransform footer = CreatePanel("Footer", parent, HeaderColor);
        Pin(footer, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 62f));
        AddFooterButton(footer, "SELECT EQUIPMENT", 0, () => ToggleModulePanels(true));
        AddFooterButton(footer, "VIEW INFORMATION", 1, () => helpPanel.SetActive(true));
        AddFooterButton(footer, "SHOW STREAMS", 2, ToggleStreams);
        AddFooterButton(footer, "PREVIOUS MODULE", 3, () => cameraController?.FocusPrevious());
        AddFooterButton(footer, "NEXT MODULE", 4, () => cameraController?.FocusNext());
        AddFooterButton(footer, "RESET VIEW", 5, () => cameraController?.FocusOverview());
    }

    private void BuildHelpPanel(Transform parent)
    {
        helpPanel = CreatePanel("Help Panel", parent, new Color32(9, 26, 36, 250)).gameObject;
        RectTransform panel = helpPanel.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(620f, 390f);
        AddPanelTitle(panel, "ABOUT THIS DIGITAL TWIN");
        Text body = CreateText("Body", panel,
            "Explore the Power-to-Methanol process from hydrogen production to methanol storage.\n\n" +
            "• Use the top navigation or module arrows to focus equipment.\n" +
            "• Select equipment to open educational controls and live values.\n" +
            "• Stream colours show qualitative material movement through the actual pipe routes.\n" +
            "• Arrow keys orbit; A/D pan; W/S zoom; Shift + arrows cycle modules.\n\n" +
            "Important: values and animations are simplified educational representations. " +
            "This application is not CFD, Aspen, industrial control software, or a validated process model.",
            16, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        Pin(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 56f), new Vector2(-28f, -64f));
        Button close = CreateButton("Close", panel, "CLOSE", AccentColor, 13);
        AnchorBottomRight(close.GetComponent<RectTransform>(), new Vector2(-24f, 18f), new Vector2(120f, 36f));
        close.onClick.AddListener(() => helpPanel.SetActive(false));
        helpPanel.SetActive(false);
    }

    private void BuildEducationalBadge(Transform parent)
    {
        Text badge = CreateText("Educational Badge", parent, "EDUCATIONAL VISUALIZATION • SIMPLIFIED PROCESS VALUES", 10, FontStyle.Bold, TextAnchor.MiddleCenter, MutedTextColor);
        Pin(badge.rectTransform, new Vector2(0.33f, 1f), new Vector2(0.67f, 1f), new Vector2(0f, -98f), new Vector2(0f, -76f));
    }

    private void Refresh()
    {
        PlantProcessSimulator simulator = PlantProcessSimulator.Instance;
        if (simulator == null) return;
        PlantProcessSimulator.ProcessSnapshot s = simulator.Current;

        bool alarm = s.reactorTemperatureC >= 285f || s.reactorPressureBar >= 98f || s.storageFillPercent >= 95f;
        bool caution = !alarm && (s.reactorTemperatureC > 270f || s.captureEfficiencyPercent < 65f || s.methanolPurityPercent < 95f);
        plantStatusText.text = alarm ? "● Attention required" : caution ? "● Operating caution" : "● Normal operation";
        plantStatusText.color = alarm ? Hex("FF3B30") : caution ? Hex("FFB020") : HealthyColor;
        efficiencyText.text = $"{s.overallEfficiencyPercent:F1}%";
        productionText.text = $"{s.methanolProductionKgH:F0} kg/h";
        // CO2 + 3H2 -> CH3OH + H2O. Each 32 kg of methanol represents
        // 44 kg of CO2 converted on the simplified stoichiometric basis.
        float co2ConvertedKgH = s.methanolProductionKgH * (44f / 32f);
        float co2UtilizationPercent = s.co2CapturedKgH > 0.01f
            ? Mathf.Clamp01(co2ConvertedKgH / s.co2CapturedKgH) * 100f
            : 0f;
        utilizationText.text = $"{co2UtilizationPercent:F1}%";

        electrolyzerKpis.text = $"Power {s.electrolyzerPowerPercent:F0}%     H2 {s.h2InputKgH:F0} kg/h\nWater {s.waterFeedKgH:F0} kg/h     O2 {s.oxygenByproductKgH:F0} kg/h";
        captureKpis.text = $"Capture {s.captureEfficiencyPercent:F1}%     CO2 {s.co2CapturedKgH:F0} kg/h\nAmine {s.amineFlowPercent:F0}%     Regen {s.regeneratorTemperatureC:F0} °C";
        reactorKpis.text = $"Temp {s.reactorTemperatureC:F0} °C     Pressure {s.reactorPressureBar:F0} bar\nH2/CO2 {s.h2Co2Ratio:F1}     Yield {s.reactorYieldPercent:F1}%";
        separationKpis.text = $"Recycle {s.recycleRatioPercent:F0}%     Purity {s.methanolPurityPercent:F2}%\nStorage {s.storageFillPercent:F0}%     Product {s.methanolProductionKgH:F0} kg/h";

        for (int i = 0; i < navigationImages.Count; i++)
        {
            if (navigationImages[i] != null)
                navigationImages[i].color = navigationPages[i] == currentPage ? AccentColor : HeaderColor;
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
        if (analyticsPanel != null) analyticsPanel.SetActive(page == DashboardPage.Analytics);
        if (flowInspectionPanel != null) flowInspectionPanel.SetActive(page == DashboardPage.FlowInspection);
        if (legendPanel != null) legendPanel.SetActive(page == DashboardPage.Overview || page == DashboardPage.Process || page == DashboardPage.FlowInspection);
        if (plantStatusPanel != null) plantStatusPanel.SetActive(page == DashboardPage.Overview || page == DashboardPage.Analytics);
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
        AnchorTopLeft(labelText.rectTransform, new Vector2(16f, y), new Vector2(190f, 24f));
        Text value = CreateText(label + " Value", panel, "—", 12, FontStyle.Bold, TextAnchor.MiddleRight, Color.white);
        AnchorTopRight(value.rectTransform, new Vector2(-16f, y), new Vector2(120f, 24f));
        return value;
    }

    private void AddFooterButton(RectTransform footer, string label, int index, Action action)
    {
        Button button = CreateButton(label, footer, label, HeaderColor, 11);
        float width = 1f / 6f;
        Pin(button.GetComponent<RectTransform>(), new Vector2(index * width, 0f), new Vector2((index + 1) * width, 1f), new Vector2(1f, 2f), new Vector2(-1f, -2f));
        button.onClick.AddListener(() => action());
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
        return button;
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
}
