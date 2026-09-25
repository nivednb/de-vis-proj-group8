using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Icon = UITheme.Icon;
using Kind = UITheme.ButtonKind;
using W = UITheme.Weight;

/// <summary>
/// Builds the full-plant dashboard at runtime in the "Daylight" design (see
/// <see cref="UITheme"/>): floating white cards over the 3D plant — brand, pill navigation,
/// stream legend, plant status, four process tiles and a bottom dock — plus one right-hand
/// card per section, the about box and the analytics window.
/// The values are supplied by PlantProcessSimulator and remain educational estimates.
///
/// GameObject names of the elements the guided tour spotlights ("Header", "OVERVIEW",
/// "Plant Status", "Footer", …) are part of the tour's contract and must not change.
/// </summary>
[DisallowMultipleComponent]
public sealed class IcodosDashboardRuntime : MonoBehaviour
{
    private enum DashboardPage { Overview, Process, Equipment, Simulation, Analytics, FlowInspection }
    private enum AnalyticsTab { Stats, Visualise }

    private const string RuntimeRootName = "Generated ICODOS Plant Dashboard";
    private const float PageCardWidth = 340f;

    private static readonly Color MetricEfficiency = UITheme.Accent;
    private static readonly Color MetricCapture = UITheme.Hex("0D9488");
    private static readonly Color MetricYield = UITheme.Hex("EA580C");
    private static readonly Color MetricPurity = UITheme.Hex("7C3AED");
    private static readonly Color MetricStorage = UITheme.Hex("0284C7");

    private readonly List<Button> navigationButtons = new List<Button>();
    private readonly List<DashboardPage> navigationPages = new List<DashboardPage>();
    private Canvas canvas;
    private Canvas helpCanvas;
    private Font font;
    private OrbitCameraController cameraController;
    private GameObject helpPanel;
    private RectTransform helpCard;
    private GameObject legendPanel;
    private GameObject plantStatusPanel;
    private GameObject kpiStrip;
    private GameObject processPanel;
    private GameObject equipmentPanel;
    private GameObject simulationPanel;
    private GameObject flowInspectionPanel;
    private GameObject analyticsWindow;
    // In the Windows player the analytics UI lives in its own native OS window; in the
    // Editor it stays an in-app popup on the main canvas.
    private ExternalAnalyticsWindow externalAnalytics;
    private Canvas analyticsCanvas;
    private Button flowNavButton;
    private Button showStreamsButton;
    private Button streamVisualsButton;
    // Flow Lab *mode* (animated pipes) is independent of its panel: closing the panel keeps
    // the flow running; only FLOW LAB / SHOW STREAMS switch the mode off again.
    private bool flowLabOn;
    private int flowInspectionMode;
    private readonly List<Button> flowModeButtons = new List<Button>();
    private GameObject analyticsStatsTab;
    private GameObject analyticsVisualiseTab;
    private Button analyticsNavButton;
    private Button analyticsStatsTabButton;
    private Button analyticsVisualiseTabButton;
    private bool analyticsWindowOpen;
    private AnalyticsTab currentAnalyticsTab = AnalyticsTab.Stats;
    private readonly List<Button> runToggleButtons = new List<Button>();
    private Button dockRunButton;

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
    private static readonly string[] ReactorParamNames = { "Temp", "Pressure", "H₂:CO₂", "GHSV", "Feed" };
    private static readonly string[] ReactorParamTitles = { "temperature", "pressure", "H₂/CO₂ ratio", "GHSV", "feed flow" };
    // Exact reactor slider labels (InteractiveModulePanelRuntime.CreateControls) for the
    // variable-lock — selecting a parameter tab freezes every reactor slider but this one.
    private static readonly string[] ReactorParamSliderLabels = { "Temp", "Pressure", "H2/CO2", "GHSV", "Feed flow" };
    private static readonly string[] ReactorParamAxisLabels =
        { "Reactor temperature (°C)", "Reactor pressure (bar)", "H₂/CO₂ ratio", "GHSV (1/h)", "Reactor feed flow (%)" };
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

    // Process map card
    private Text processStepText;
    private Text processTitleText;
    private Text processBodyText;
    private Text processStreamsLabel;
    private readonly List<Image> processSegments = new List<Image>();
    private readonly List<Text> processChipTexts = new List<Text>();
    private readonly List<Image> processChips = new List<Image>();
    private RectTransform processChipArea;

    // Reactor lab card
    private UIValueText reactorTempValue, reactorPressureValue, reactorRatioValue, reactorYieldValue;
    // Simulation card
    private UIValueText simLoadValue, simSyngasValue, simMethanolValue, simStorageValue;
    private Image simCallout;
    private Image simCalloutIcon;
    private Text simCalloutText;

    // Analytics
    private UIValueText[] throughputValues;
    private UIValueText[] metricValues;
    private UIProgressBar[] metricBars;
    private UIValueText[] kpiValues;
    private UIProgressBar[] kpiBars;
    private Text analyticsInsightsText;

    private DashboardPage currentPage = DashboardPage.Overview;
    private int processStepIndex;

    // Header / overview
    private Image runStateDot;
    private Text runStateText;
    private UIStatusChip plantStatusChip;
    private Image efficiencyRing;
    private Text efficiencyText;
    private UIValueText productionValue;
    private UIValueText utilizationValue;
    private Text tankValueText;
    private UIProgressBar tankBar;
    private Text tankCaption;
    private readonly UIValueText[][] tileValues = new UIValueText[4][];
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
        ModulePanels()?.CloseAllPanels();
        // Every tour stop except the Flow Lab ones shows the plant with its normal pipes.
        if (pageId != "flow") SetFlowLabMode(false);
        switch (pageId)
        {
            case "overview": SelectPage(DashboardPage.Overview); break;
            case "process": SelectPage(DashboardPage.Process); break;
            case "flow": SetFlowLabMode(true); break;
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
        SetPopupVisible(helpPanel, false, helpCard);
        ModulePanels()?.CloseAllPanels();
        // A separate analytics window belongs to the user; the tour never opens one, so it
        // must not close one either.
        if (!AnalyticsIsExternal) CloseAnalyticsWindow();
        SelectPage(DashboardPage.Overview);
    }

    /// <summary>True when ANALYTICS opens as its own native OS window (Windows player).</summary>
    public bool AnalyticsIsExternal => externalAnalytics != null;

    /// <summary>
    /// True while the analytics window is open and the given screen point falls within its
    /// actual current rect (it's draggable, so this is computed live rather than cached) —
    /// the precise geometric test other systems (camera orbit, module hover buttons) use to
    /// block input only under the window, and nowhere else on screen.
    /// </summary>
    public bool IsPointerOverAnalyticsWindow(Vector2 screenPoint)
    {
        // A separate OS window never overlaps the main window's own screen space.
        if (AnalyticsIsExternal || !analyticsWindowOpen || analyticsWindow == null) return false;
        RectTransform rect = analyticsWindow.GetComponent<RectTransform>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
    }

    private void Start()
    {
        font = UITheme.Font(W.Medium);
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
    /// view — closes whichever nav page (Process Map / Reactor Lab / Simulation) is open, the
    /// way clicking outside a popover dismisses it elsewhere. The Flow Lab panel is the
    /// exception: it closes with its own X (the flow mode itself only with FLOW LAB / SHOW
    /// STREAMS), so orbiting the plant with it open never snaps the camera back. Module
    /// control-slider panels and the analytics window are deliberately left alone.
    /// </summary>
    private void OnBackgroundClicked()
    {
        if (currentPage != DashboardPage.Overview && currentPage != DashboardPage.FlowInspection)
            SelectPage(DashboardPage.Overview);
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
        if (helpCanvas != null) Destroy(helpCanvas.gameObject);
        runToggleButtons.Clear();
        navigationButtons.Clear();
        navigationPages.Clear();
        flowModeButtons.Clear();

        GameObject canvasObject = new GameObject("ICODOS Dashboard Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the warning band (72); the module control drawers (95) sit above this so an
        // open drawer covers the right-hand cards it replaces.
        canvas.sortingOrder = 90;
        UITheme.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());
        canvasObject.AddComponent<GraphicRaycaster>();

        BuildHeader(canvasObject.transform);
        BuildLegend(canvasObject.transform);
        BuildPlantStatus(canvasObject.transform);
        BuildKpiStrip(canvasObject.transform);
        BuildPagePanels(canvasObject.transform);
        BuildFooter(canvasObject.transform);
        BuildAnalyticsWindow(canvasObject.transform);
        BuildHelpPanel();
    }

    // ---- small builders --------------------------------------------------------

    private static Text Txt(Transform parent, string name, string value, float size, W weight, Color color,
        float x, float y, float width, float height, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
    {
        Text t = UITheme.Label(name, parent, value, size, weight, color, anchor, wrap);
        UITheme.TopLeft(t.rectTransform, x, y, width, height);
        return t;
    }

    /// <summary>A labelled read-out tile (pale surface, small caption, large value).</summary>
    private static UIValueText ReadoutTile(Transform parent, string name, string caption, float x, float y, float width, float height, float valueSize = 18f)
    {
        Image tile = UITheme.Panel(name, parent, UITheme.Sunken2, 12f);
        UITheme.TopLeft(tile.rectTransform, x, y, width, height);
        UITheme.Border(tile.rectTransform, UITheme.Line, 12f);
        Txt(tile.transform, "Caption", caption, 12f, W.Bold, UITheme.Subtle, 12f, 9f, width - 20f, 16f);
        UIValueText value = UITheme.ValueText("Value", tile.transform, valueSize, 11.5f, UITheme.Ink, UITheme.Muted);
        UITheme.TopLeft((RectTransform)value.transform, 12f, 27f, width - 20f, valueSize * 1.35f);
        return value;
    }

    /// <summary>Right-hand section card with an icon badge, title and subtitle.</summary>
    private RectTransform PageCard(string name, Transform parent, float height, Icon icon, Color iconColor,
        string title, string subtitle, Action onClose = null, string closeName = null)
    {
        RectTransform card = UITheme.Card(name, parent, 16f);
        UITheme.TopRight(card, 16f, 92f, PageCardWidth, height);
        Image badge = UITheme.Badge("Badge", card, icon, iconColor, 40f, 12f, 22f);
        UITheme.TopLeft(badge.rectTransform, 18f, 18f, 40f, 40f);
        Txt(card, "Card Title", title, 17f, W.ExtraBold, UITheme.Ink, 70f, 17f, 200f, 22f);
        Txt(card, "Card Subtitle", subtitle, 12.5f, W.SemiBold, UITheme.Subtle, 70f, 39f, 220f, 18f);
        if (onClose != null)
        {
            Button close = UITheme.IconButton(closeName ?? "Close", card, Icon.Close, Kind.Secondary, 36f, 16f);
            UITheme.TopRight((RectTransform)close.transform, 14f, 20f, 36f, 36f);
            close.onClick.AddListener(() => onClose());
        }
        return card;
    }

    // ---- header ------------------------------------------------------------------

    private void BuildHeader(Transform parent)
    {
        // Invisible band that groups the top bar (the tour spotlights it as one element).
        RectTransform header = UITheme.NewRect("Header", parent);
        UITheme.TopBand(header, 0f, 0f, 0f, 88f);

        // Brand
        RectTransform brand = UITheme.Card("Brand", header, 14f);
        Image logo = UITheme.Panel("Logo", brand, UITheme.Accent, 10f);
        UITheme.TopLeft(logo.rectTransform, 11f, 11f, 34f, 34f);
        Image logoIcon = UITheme.IconImage("Logo Icon", logo.transform, Icon.Logo, 21f, Color.white);
        UITheme.Center(logoIcon.rectTransform, 21f, 21f);
        Text title = Txt(brand, "Title", "Power-to-Methanol Digital Twin", 15f, W.ExtraBold, UITheme.Ink, 57f, 9f, 300f, 20f);
        Txt(brand, "Subtitle", "Educational simulation · simplified values", 12f, W.Medium, UITheme.Muted, 57f, 29f, 300f, 17f);
        float brandWidth = 57f + Mathf.Max(title.preferredWidth, 250f) + 20f;
        UITheme.TopLeft(brand, 16f, 16f, brandWidth, 56f);

        // Pill navigation
        RectTransform navCard = UITheme.Card("Navigation", header, 24f);
        string[] names = { "OVERVIEW", "PROCESS MAP", "FLOW LAB", "REACTOR LAB", "ANALYTICS", "SIMULATION" };
        string[] labels = { "Overview", "Process map", "Flow lab", "Reactor lab", "Analytics", "Simulation" };
        DashboardPage[] pages =
        {
            DashboardPage.Overview, DashboardPage.Process, DashboardPage.FlowInspection,
            DashboardPage.Equipment, DashboardPage.Analytics, DashboardPage.Simulation
        };
        float x = 4f;
        for (int i = 0; i < names.Length; i++)
        {
            DashboardPage page = pages[i];
            Button button = UITheme.MakeButton(names[i], navCard, labels[i], Kind.Nav, 14f, null, 20f, false, 18f, W.Bold, 17f);
            float w = UITheme.PreferredWidth(button);
            UITheme.TopLeft((RectTransform)button.transform, x, 4f, w, 40f);
            x += w + 2f;
            if (page == DashboardPage.Analytics)
            {
                analyticsNavButton = button;
                button.onClick.AddListener(ToggleAnalyticsWindow);
            }
            else if (page == DashboardPage.FlowInspection)
            {
                flowNavButton = button;
                button.onClick.AddListener(() => { ModulePanels()?.CloseAllPanels(); ToggleFlowLab(); });
            }
            else
            {
                button.onClick.AddListener(() => { ModulePanels()?.CloseAllPanels(); SelectPage(page); });
            }
            navigationButtons.Add(button);
            navigationPages.Add(page);
        }
        UITheme.TopCenter(navCard, 0f, 20f, x + 2f, 48f);

        // Help (round card, right)
        RectTransform helpCardShell = UITheme.Card("Help Card", header, 22f);
        UITheme.TopRight(helpCardShell, 16f, 22f, 44f, 44f);
        Button help = UITheme.IconButton("Help", helpCardShell, Icon.Help, Kind.Ghost, 44f, 21f);
        UITheme.Fill((RectTransform)help.transform);
        help.onClick.AddListener(() => SetPopupVisible(helpPanel, !helpPanel.activeSelf, helpCard));

        // Run state capsule
        RectTransform runCard = UITheme.Card("Run State", header, 22f);
        runStateDot = UITheme.Dot("Dot", runCard, 8f, UITheme.Success);
        UITheme.TopLeft(runStateDot.rectTransform, 17f, 18f, 8f, 8f);
        runStateText = Txt(runCard, "Label", "Running", 13f, W.Bold, UITheme.SuccessInk, 33f, 12f, 80f, 20f);
        float runWidth = 33f + runStateText.preferredWidth + 18f;
        UITheme.TopRight(runCard, 16f + 44f + 8f, 22f, runWidth, 44f);
    }

    // ---- legend ------------------------------------------------------------------

    private void BuildLegend(Transform parent)
    {
        RectTransform panel = UITheme.Card("Process Flow Legend", parent, 14f);
        legendPanel = panel.gameObject;
        Text legendTitle = Txt(panel, "Panel Title", "Process streams", 14f, W.ExtraBold, UITheme.Ink, 18f, 14f, 220f, 20f);
        Txt(panel, "Caption", "Pipe colours in the 3D view", 12f, W.Medium, UITheme.Subtle, 18f, 34f, 220f, 17f);

        // Rows come from PlantStreamLegend, which is also what colours the pipes themselves —
        // so this panel and the plant can never disagree.
        PlantStreamLegend.Row[] rows = PlantStreamLegend.Rows;
        float maxLabel = 0f;
        for (int i = 0; i < rows.Length; i++)
        {
            PlantStreamLegend.Row row = rows[i];
            float y = 62f + i * 29f;
            if (row.Dashed)
            {
                for (int d = 0; d < 3; d++)
                {
                    Image dash = UITheme.Panel("Recycle Dash " + d, panel, row.Color, 3f);
                    UITheme.TopLeft(dash.rectTransform, 18f + d * 10f, y + 6f, 6f, 8f);
                }
            }
            else
            {
                Image swatch = UITheme.Panel(row.Label + " Swatch", panel, row.Color, 4f);
                UITheme.TopLeft(swatch.rectTransform, 18f, y + 6f, 26f, 8f);
            }
            Text label = Txt(panel, row.Label, row.Label, 13f, W.SemiBold, UITheme.Ink2, 56f, y, 220f, 20f);
            maxLabel = Mathf.Max(maxLabel, label.preferredWidth);
        }
        float height = 62f + (rows.Length - 1) * 29f + 20f + 16f;
        UITheme.TopLeft(panel, 16f, 92f, Mathf.Max(256f, 56f + maxLabel + 18f), height);
        // Minimisable down to its title row.
        UICollapsible.Attach(panel, height, 48f, 10f, 9f, legendTitle.transform);
    }

    // ---- plant status --------------------------------------------------------------

    private void BuildPlantStatus(Transform parent)
    {
        RectTransform panel = UITheme.Card("Plant Status", parent, 16f);
        plantStatusPanel = panel.gameObject;
        UITheme.TopRight(panel, 16f, 92f, 316f, 324f);
        Text statusTitle = Txt(panel, "Panel Title", "Plant status", 14f, W.ExtraBold, UITheme.Ink, 18f, 17f, 150f, 20f);
        plantStatusChip = UITheme.StatusChip("Status", panel, 26f, 12f);
        RectTransform chipRect = (RectTransform)plantStatusChip.transform;
        chipRect.anchorMin = chipRect.anchorMax = chipRect.pivot = new Vector2(1f, 1f);
        // Leaves room for the minimise button in the top-right corner.
        chipRect.anchoredPosition = new Vector2(-48f, -14f);
        plantStatusChip.Set(UIStatusChip.Kind.Success, "Normal operation");

        // Efficiency ring
        RectTransform ring = UITheme.NewRect("Efficiency Ring", panel);
        UITheme.TopLeft(ring, 18f, 52f, 104f, 104f);
        Image track = UITheme.Panel("Track", ring, UITheme.Line);
        track.sprite = UITheme.Ring();
        UITheme.Fill(track.rectTransform);
        efficiencyRing = UITheme.Panel("Fill", ring, UITheme.Accent);
        efficiencyRing.sprite = UITheme.Ring();
        efficiencyRing.type = Image.Type.Filled;
        efficiencyRing.fillMethod = Image.FillMethod.Radial360;
        efficiencyRing.fillOrigin = (int)Image.Origin360.Top;
        efficiencyRing.fillClockwise = true;
        efficiencyRing.fillAmount = 0f;
        UITheme.Fill(efficiencyRing.rectTransform);
        efficiencyText = Txt(ring, "Efficiency Value", "—", 20f, W.ExtraBold, UITheme.Ink, 0f, 34f, 104f, 26f, TextAnchor.MiddleCenter);
        Txt(ring, "Efficiency Caption", "efficiency", 11f, W.SemiBold, UITheme.Subtle, 0f, 58f, 104f, 16f, TextAnchor.MiddleCenter);

        Txt(panel, "Production Label", "Methanol production", 12f, W.SemiBold, UITheme.Subtle, 138f, 62f, 170f, 16f);
        productionValue = UITheme.ValueText("Production Value", panel, 19f, 12.5f, UITheme.Ink, UITheme.Muted);
        UITheme.TopLeft((RectTransform)productionValue.transform, 138f, 78f, 170f, 26f);
        Txt(panel, "Utilisation Label", "CO₂ utilisation", 12f, W.SemiBold, UITheme.Subtle, 138f, 112f, 170f, 16f);
        utilizationValue = UITheme.ValueText("Utilisation Value", panel, 19f, 12.5f, UITheme.Ink, UITheme.Muted);
        UITheme.TopLeft((RectTransform)utilizationValue.transform, 138f, 128f, 170f, 26f);

        // Methanol tank
        Image tank = UITheme.Panel("Methanol Tank", panel, UITheme.Sunken, 10f);
        UITheme.TopLeft(tank.rectTransform, 18f, 172f, 280f, 76f);
        Txt(tank.transform, "Label", "Methanol tank", 12.5f, W.Bold, UITheme.Ink2, 14f, 10f, 150f, 18f);
        tankValueText = UITheme.Label("Value", tank.transform, "—", 14f, W.ExtraBold, UITheme.Ink, TextAnchor.MiddleRight);
        UITheme.TopRight(tankValueText.rectTransform, 14f, 10f, 100f, 18f);
        tankBar = UITheme.ProgressBar("Bar", tank.transform, UITheme.Accent, 6f, UITheme.Line);
        UITheme.TopLeft((RectTransform)tankBar.transform, 14f, 36f, 252f, 6f);
        tankCaption = Txt(tank.transform, "Caption", "", 12f, W.SemiBold, UITheme.Muted, 14f, 49f, 252f, 17f);

        Button maxEfficiency = UITheme.MakeButton("Set Maximum Efficiency", panel, "Set maximum efficiency", Kind.Primary, 14f, Icon.Target, 10f);
        UITheme.TopLeft((RectTransform)maxEfficiency.transform, 18f, 262f, 280f, 44f);
        maxEfficiency.onClick.AddListener(ApplyMaximumEfficiency);

        // Minimisable down to its title row, keeping the live status badge visible.
        UICollapsible.Attach(panel, 324f, 54f, 12f, 12f, statusTitle.transform, chipRect);
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

    // ---- process tiles -------------------------------------------------------------

    private void BuildKpiStrip(Transform parent)
    {
        RectTransform strip = UITheme.NewRect("Process KPI Strip", parent);
        kpiStrip = strip.gameObject;
        UITheme.BottomBand(strip, 16f, 92f, 16f, 104f);

        string[] titles = { "Electrolyzer", "CO₂ capture", "Reactor", "Separation" };
        string[] moduleIds = { "electrolyzer", "absorber", "reactor", "separator" };
        Color[] colors = { UITheme.Hex("0EA5E9"), UITheme.Hex("10B981"), UITheme.Hex("F97316"), UITheme.Hex("A855F7") };
        string[][] columns =
        {
            new[] { "Power", "H₂", "Water", "O₂" },
            new[] { "Capture", "CO₂", "Amine", "Regen" },
            new[] { "Temp", "Pressure", "H₂/CO₂", "Yield" },
            new[] { "Recycle", "Purity", "Storage", "Product" },
        };

        for (int i = 0; i < 4; i++)
        {
            RectTransform tile = UITheme.Card(titles[i], strip, 14f);
            tile.anchorMin = new Vector2(i * 0.25f, 0f);
            tile.anchorMax = new Vector2((i + 1) * 0.25f, 1f);
            tile.offsetMin = new Vector2(i == 0 ? 0f : 6f, 0f);
            tile.offsetMax = new Vector2(i == 3 ? 0f : -6f, 0f);

            Image badge = UITheme.Panel("Badge", tile, UITheme.Tint(colors[i], 0.18f), 8f);
            UITheme.TopLeft(badge.rectTransform, 16f, 14f, 26f, 26f);
            Image square = UITheme.Panel("Swatch", badge.transform, colors[i], 3f);
            UITheme.Center(square.rectTransform, 10f, 10f);
            Txt(tile, "Title", titles[i], 14f, W.ExtraBold, UITheme.Ink, 52f, 16f, 180f, 22f);

            string moduleId = moduleIds[i];
            Button open = UITheme.MakeButton("Open " + titles[i], tile, "Open", Kind.Link, 12.5f, Icon.ChevronRight, 8f, true, 14f, W.ExtraBold, 8f);
            float ow = UITheme.PreferredWidth(open);
            UITheme.TopRight((RectTransform)open.transform, 10f, 12f, ow, 30f);
            open.onClick.AddListener(() => ModulePanels()?.OpenModule(moduleId, true));

            RectTransform valuesRow = UITheme.NewRect("Values", tile);
            valuesRow.anchorMin = new Vector2(0f, 1f);
            valuesRow.anchorMax = new Vector2(1f, 1f);
            valuesRow.pivot = new Vector2(0f, 1f);
            valuesRow.offsetMin = new Vector2(16f, -92f);
            valuesRow.offsetMax = new Vector2(-16f, -50f);
            tileValues[i] = new UIValueText[4];
            for (int c = 0; c < 4; c++)
            {
                RectTransform col = UITheme.NewRect(columns[i][c], valuesRow);
                col.anchorMin = new Vector2(c * 0.25f, 0f);
                col.anchorMax = new Vector2((c + 1) * 0.25f, 1f);
                col.offsetMin = Vector2.zero;
                col.offsetMax = new Vector2(-4f, 0f);
                Text caption = UITheme.Label("Caption", col, columns[i][c], 12f, W.SemiBold, UITheme.Subtle, TextAnchor.UpperLeft);
                UITheme.TopBand(caption.rectTransform, 0f, 0f, 0f, 16f);
                UIValueText value = UITheme.ValueText("Value", col, 15.5f, 11f, UITheme.Ink, UITheme.Muted);
                UITheme.TopBand((RectTransform)value.transform, 0f, 18f, 0f, 22f);
                tileValues[i][c] = value;
            }
        }
    }

    // ---- section cards ---------------------------------------------------------------

    private void BuildPagePanels(Transform parent)
    {
        BuildProcessCard(parent);
        BuildReactorCard(parent);
        BuildSimulationCard(parent);
        BuildFlowCard(parent);
        SetProcessStep(0);
        SelectPage(DashboardPage.Overview);
    }

    private void BuildProcessCard(Transform parent)
    {
        RectTransform card = PageCard("Guided Process", parent, 408f, Icon.Route, UITheme.Accent, "Process map", "Guided ten-step walkthrough");
        processPanel = card.gameObject;

        processStepText = Txt(card, "Step", "", 12.5f, W.ExtraBold, UITheme.Accent, 18f, 74f, 200f, 18f);
        processSegments.Clear();
        const int steps = 10;
        float segWidth = (304f - (steps - 1) * 4f) / steps;
        for (int i = 0; i < steps; i++)
        {
            Image seg = UITheme.Panel("Segment " + i, card, UITheme.Line, 2f);
            UITheme.TopLeft(seg.rectTransform, 18f + i * (segWidth + 4f), 98f, segWidth, 4f);
            processSegments.Add(seg);
        }

        processTitleText = Txt(card, "Process Title", "", 20f, W.ExtraBold, UITheme.Ink, 18f, 114f, 304f, 28f);
        processBodyText = Txt(card, "Explanation", "", 13.5f, W.Medium, UITheme.Ink2, 18f, 150f, 304f, 92f, TextAnchor.UpperLeft, true);
        processBodyText.lineSpacing = 1.12f;

        processStreamsLabel = Txt(card, "Streams Label", "Streams", 12f, W.ExtraBold, UITheme.Subtle, 18f, 250f, 200f, 16f);
        processChipArea = UITheme.NewRect("Streams", card);
        UITheme.TopLeft(processChipArea, 18f, 272f, 304f, 64f);

        Button previous = UITheme.MakeButton("Previous Step", card, "Previous", Kind.Secondary, 14f, Icon.ChevronLeft, 10f);
        UITheme.TopLeft((RectTransform)previous.transform, 18f, 346f, 147f, 44f);
        previous.onClick.AddListener(() => SetProcessStep(processStepIndex - 1));
        Button next = UITheme.MakeButton("Next Step", card, "Next step", Kind.Primary, 14f, Icon.ChevronRight, 10f, true);
        UITheme.TopLeft((RectTransform)next.transform, 175f, 346f, 147f, 44f);
        next.onClick.AddListener(() => SetProcessStep(processStepIndex + 1));
    }

    private void BuildReactorCard(Transform parent)
    {
        RectTransform card = PageCard("Reactor Lab", parent, 446f, Icon.Flask, UITheme.Hex("EA580C"), "Reactor lab", "R-201 · fixed-bed synthesis");
        equipmentPanel = card.gameObject;

        reactorTempValue = ReadoutTile(card, "Temperature", "Temperature", 18f, 76f, 147f, 60f);
        reactorPressureValue = ReadoutTile(card, "Pressure", "Pressure", 175f, 76f, 147f, 60f);
        reactorRatioValue = ReadoutTile(card, "Ratio", "H₂/CO₂ ratio", 18f, 146f, 147f, 60f);
        reactorYieldValue = ReadoutTile(card, "Yield", "Yield", 175f, 146f, 147f, 60f);

        Text body = Txt(card, "Body",
            "Inspect the transparent fixed-bed reactor. Conditioned H₂/CO₂/recycle gas enters the side feed nozzle, crosses the catalyst volume and leaves through the top outlet. " +
            "The catalyst colour shows the operating state; the moving particles visualise species and conversion.",
            13f, W.Medium, UITheme.Muted, 18f, 220f, 304f, 100f, TextAnchor.UpperLeft, true);
        body.lineSpacing = 1.12f;

        Button focusReactor = UITheme.MakeButton("FOCUS REACTOR", card, "Focus reactor", Kind.Primary, 14f, Icon.Focus, 10f);
        UITheme.TopLeft((RectTransform)focusReactor.transform, 18f, 330f, 304f, 44f);
        focusReactor.onClick.AddListener(() => Focus(7));
        Button reactorControls = UITheme.MakeButton("OPEN REACTOR CONTROLS", card, "Open reactor controls", Kind.Secondary, 14f, Icon.Sliders, 10f);
        UITheme.TopLeft((RectTransform)reactorControls.transform, 18f, 384f, 304f, 44f);
        reactorControls.onClick.AddListener(() => ModulePanels()?.OpenModule("reactor", false));
    }

    private void BuildSimulationCard(Transform parent)
    {
        RectTransform card = PageCard("Simulation", parent, 504f, Icon.Sliders, UITheme.Accent, "Simulation", "Plant-wide summary");
        simulationPanel = card.gameObject;

        simLoadValue = ReadoutTile(card, "Plant Load", "Plant load", 18f, 76f, 147f, 60f);
        simSyngasValue = ReadoutTile(card, "Syngas Feed", "Syngas feed", 175f, 76f, 147f, 60f);
        simMethanolValue = ReadoutTile(card, "Methanol Output", "Methanol output", 18f, 146f, 147f, 60f);
        simStorageValue = ReadoutTile(card, "Storage", "Storage", 175f, 146f, 147f, 60f);

        simCallout = UITheme.Panel("Interlock", card, UITheme.SuccessSoft, 10f);
        UITheme.TopLeft(simCallout.rectTransform, 18f, 220f, 304f, 58f);
        simCalloutIcon = UITheme.IconImage("Icon", simCallout.transform, Icon.Check, 18f, UITheme.SuccessInk);
        UITheme.TopLeft(simCalloutIcon.rectTransform, 12f, 11f, 18f, 18f);
        simCalloutText = Txt(simCallout.transform, "Text", "", 12.5f, W.SemiBold, UITheme.SuccessInk, 40f, 9f, 254f, 40f, TextAnchor.UpperLeft, true);

        Txt(card, "Modules Label", "Open module controls", 12f, W.ExtraBold, UITheme.Subtle, 18f, 294f, 250f, 16f);
        string[] labels = { "Electrolyzer", "CO₂ absorber", "Desorber", "Compressor", "Reactor", "Condenser", "Separator", "Distillation" };
        string[] ids = { "electrolyzer", "absorber", "desorber", "compressor", "reactor", "condenser", "separator", "distillation" };
        for (int i = 0; i < labels.Length; i++)
        {
            string id = ids[i];
            Button b = UITheme.MakeButton("Open " + id, card, labels[i], Kind.Secondary, 13f, null, 8f);
            UITheme.TopLeft((RectTransform)b.transform, 18f + (i % 2) * 157f, 316f + (i / 2) * 42f, 147f, 34f);
            b.onClick.AddListener(() => ModulePanels()?.OpenModule(id, true));
        }
    }

    private void BuildFlowCard(Transform parent)
    {
        RectTransform card = PageCard("Flow Lab", parent, 470f, Icon.Waves, UITheme.Accent, "Flow lab", "Animated pipe flow is on", CloseFlowLab, "Flow Lab Close");
        flowInspectionPanel = card.gameObject;

        Text body = Txt(card, "Body",
            "Pipes turn see-through and show what they carry in their legend colours. Gases move as eddies, liquids fill the bore and the recycle loop runs in dashes. Speed follows the calculated flow.",
            13f, W.Medium, UITheme.Muted, 18f, 74f, 304f, 80f, TextAnchor.UpperLeft, true);
        body.lineSpacing = 1.12f;

        Txt(card, "Filter Label", "Show streams for", 12f, W.ExtraBold, UITheme.Subtle, 18f, 160f, 250f, 16f);
        string[] modes = { "All streams", "Feed gases", "Capture loop", "Synthesis loop", "Product path" };
        for (int i = 0; i < modes.Length; i++)
        {
            int mode = i;
            Button b = UITheme.MakeButton(modes[i].ToUpperInvariant(), card, modes[i], Kind.Tab, 13.5f, Icon.Check, 10f, true, 16f, W.Bold);
            HorizontalLayoutGroup hl = b.GetComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft;
            // Push the check mark to the right edge.
            Text label = b.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            LayoutElement grow = label.gameObject.AddComponent<LayoutElement>();
            grow.flexibleWidth = 1f;
            hl.childForceExpandWidth = false;
            UITheme.TopLeft((RectTransform)b.transform, 18f, 182f + i * 44f, 304f, 38f);
            b.onClick.AddListener(() => SetFlowInspectionMode(mode));
            flowModeButtons.Add(b);
        }

        streamVisualsButton = UITheme.MakeButton("Stream Visuals", card, "Hide stream visuals", Kind.Secondary, 14f, Icon.Waves, 10f);
        UITheme.TopLeft((RectTransform)streamVisualsButton.transform, 18f, 412f, 304f, 40f);
        streamVisualsButton.onClick.AddListener(ToggleStreams);
    }

    // ---- dock ----------------------------------------------------------------------

    private Button massFlowToolButton;

    private void BuildFooter(Transform parent)
    {
        RectTransform dock = UITheme.Card("Footer", parent, 18f, UITheme.WithAlpha(Color.white, 0.97f), 36f, 14f, 0.2f);
        float x = 8f;

        dockRunButton = DockButton(dock, "Run Toggle", "Pause", Kind.Dark, Icon.Pause, ref x, ToggleRunning, false, "Resume");
        runToggleButtons.Add(dockRunButton);
        DockButton(dock, "Reset", "Reset", Kind.DangerGhost, Icon.Reset, ref x, ResetSimulation);
        DockDivider(dock, ref x);
        DockButton(dock, "View Information", "View information", Kind.Ghost, Icon.Info, ref x, () => SetPopupVisible(helpPanel, true, helpCard));
        showStreamsButton = DockButton(dock, "Show Streams", "Show streams", Kind.Ghost, Icon.Waves, ref x, () => { ModulePanels()?.CloseAllPanels(); ToggleFlowLab(); });
        massFlowToolButton = DockButton(dock, "Mass Flow Tool", "Mass flow tool", Kind.Ghost, Icon.Gauge, ref x, ToggleMassFlowTool);
        DockDivider(dock, ref x);
        DockButton(dock, "Previous Module", "Previous module", Kind.Ghost, Icon.ChevronLeft, ref x, () => cameraController?.FocusPrevious());
        DockButton(dock, "Next Module", "Next module", Kind.Ghost, Icon.ChevronRight, ref x, () => cameraController?.FocusNext(), true);
        DockDivider(dock, ref x);
        DockButton(dock, "Reset View", "Reset view", Kind.Ghost, Icon.Focus, ref x, () => cameraController?.FocusOverview());

        UITheme.BottomCenter(dock, 0f, 16f, x + 4f, 60f);
    }

    private Button DockButton(RectTransform dock, string name, string label, Kind kind, Icon icon, ref float x, Action action,
        bool trailing = false, string altLabel = null)
    {
        Button button = UITheme.MakeButton(name, dock, label, kind, 13.5f, icon, 12f, trailing, 18f, W.Bold, 14f);
        float w = UITheme.PreferredWidth(button);
        if (altLabel != null)
        {
            UITheme.SetLabel(button, altLabel);
            w = Mathf.Max(w, UITheme.PreferredWidth(button));
            UITheme.SetLabel(button, label);
        }
        UITheme.TopLeft((RectTransform)button.transform, x, 8f, w, 44f);
        x += w + 4f;
        button.onClick.AddListener(() => action());
        return button;
    }

    private static void DockDivider(RectTransform dock, ref float x)
    {
        Image line = UITheme.Panel("Divider", dock, UITheme.Line);
        UITheme.TopLeft(line.rectTransform, x + 3f, 18f, 1f, 24f);
        x += 10f;
    }

    /// <summary>Arms/disarms the pipe mass-flow probe; the button stays lit while it is armed
    /// so the changed cursor is never unexplained.</summary>
    private void ToggleMassFlowTool()
    {
        MassFlowProbeRuntime.Instance?.Toggle();
        Refresh();
    }

    // ---- about box -------------------------------------------------------------------

    private void BuildHelpPanel()
    {
        GameObject canvasObject = new GameObject("Help Canvas");
        canvasObject.transform.SetParent(transform, false);
        helpCanvas = canvasObject.AddComponent<Canvas>();
        helpCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the dashboard, the module drawers and the probe; below the tour (300).
        helpCanvas.sortingOrder = 200;
        UITheme.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform root = UITheme.NewRect("Help Panel", canvasObject.transform);
        UITheme.Fill(root);
        helpPanel = root.gameObject;
        Image scrim = UITheme.ScrimLayer("Scrim", root, UITheme.WithAlpha(UITheme.ShadowInk, 0.42f));
        Button scrimButton = scrim.gameObject.AddComponent<Button>();
        scrimButton.transition = Selectable.Transition.None;
        scrimButton.onClick.AddListener(() => SetPopupVisible(helpPanel, false, helpCard));

        const float width = 640f, height = 470f;
        RectTransform card = UITheme.Card("About Card", root, 18f, Color.white, 48f, 20f, 0.3f);
        UITheme.Center(card, width, height);
        helpCard = card;

        Image logo = UITheme.Panel("Logo", card, UITheme.Accent, 12f);
        UITheme.TopLeft(logo.rectTransform, 24f, 24f, 44f, 44f);
        Image logoIcon = UITheme.IconImage("Icon", logo.transform, Icon.Logo, 26f, Color.white);
        UITheme.Center(logoIcon.rectTransform, 26f, 26f);
        Txt(card, "Title", "About this digital twin", 19f, W.ExtraBold, UITheme.Ink, 82f, 25f, 400f, 24f);
        Txt(card, "Subtitle", "Power-to-Methanol · educational simulation", 12.5f, W.SemiBold, UITheme.Subtle, 82f, 49f, 400f, 18f);
        Button close = UITheme.IconButton("Close X", card, Icon.Close, Kind.Secondary, 36f, 16f);
        UITheme.TopRight((RectTransform)close.transform, 20f, 28f, 36f, 36f);
        close.onClick.AddListener(() => SetPopupVisible(helpPanel, false, helpCard));

        Txt(card, "Intro", "Explore the Power-to-Methanol process, from hydrogen production to methanol storage.",
            14f, W.SemiBold, UITheme.Ink, 24f, 90f, width - 48f, 20f, TextAnchor.UpperLeft, true);

        string[] bullets =
        {
            "New here? <b>Start tutorial</b> runs a step-by-step tour of every section.",
            "Use the top navigation, or the module arrows in the dock, to focus equipment.",
            "Hover a piece of equipment and click its label to open live values and controls.",
            "Pipe colours show qualitative material movement through the actual pipe routes.",
            "Mouse: drag to orbit, Shift + drag to pan, scroll to zoom. Keys: arrows orbit, A/D pan, W/S zoom, Shift + arrows cycle modules.",
        };
        float y = 124f;
        for (int i = 0; i < bullets.Length; i++)
        {
            Image dot = UITheme.Dot("Bullet " + i, card, 6f, UITheme.Accent);
            UITheme.TopLeft(dot.rectTransform, 26f, y + 7f, 6f, 6f);
            Text t = Txt(card, "Point " + i, bullets[i], 13.5f, W.Medium, UITheme.Ink2, 42f, y, width - 66f, 20f, TextAnchor.UpperLeft, true);
            float h = Mathf.Max(20f, t.preferredHeight);
            t.rectTransform.sizeDelta = new Vector2(width - 66f, h);
            y += h + 8f;
        }

        Image note = UITheme.Panel("Disclaimer", card, UITheme.WarningSoft, 12f);
        UITheme.TopLeft(note.rectTransform, 24f, y + 8f, width - 48f, 60f);
        Image alert = UITheme.IconImage("Icon", note.transform, Icon.Alert, 18f, UITheme.WarningInk);
        UITheme.TopLeft(alert.rectTransform, 14f, 13f, 18f, 18f);
        Txt(note.transform, "Text",
            "Values and animations are simplified educational representations. This is not CFD, Aspen, industrial control software, or a validated process model.",
            12.5f, W.SemiBold, UITheme.WarningInk, 42f, 10f, width - 48f - 58f, 42f, TextAnchor.UpperLeft, true);

        Button tutorial = UITheme.MakeButton("Start Tutorial", card, "Start tutorial", Kind.Primary, 14f, Icon.PlayCircle, 10f);
        float tw = UITheme.PreferredWidth(tutorial) + 8f;
        UITheme.BottomLeft((RectTransform)tutorial.transform, 24f, 22f, tw, 44f);
        tutorial.onClick.AddListener(StartTutorial);

        Button closeButton = UITheme.MakeButton("Close", card, "Close", Kind.Secondary, 14f, null, 10f);
        UITheme.BottomRight((RectTransform)closeButton.transform, 24f, 22f, 112f, 44f);
        closeButton.onClick.AddListener(() => SetPopupVisible(helpPanel, false, helpCard));

        float contentHeight = y + 8f + 60f + 22f + 44f + 22f;
        card.sizeDelta = new Vector2(width, contentHeight);
        helpPanel.SetActive(false);
    }

    /// <summary>Closes the about box and hands over to the guided tour.</summary>
    private void StartTutorial()
    {
        SetPopupVisible(helpPanel, false, helpCard);
        TutorialRuntime.Instance?.StartTutorial();
    }

    // ---- analytics window ----------------------------------------------------------------

    private void BuildAnalyticsWindow(Transform parent)
    {
        if (externalAnalytics != null)
        {
            externalAnalytics.Close();
            Destroy(externalAnalytics.gameObject);
            externalAnalytics = null;
        }
        analyticsCanvas = canvas;
        if (ExternalAnalyticsWindow.IsSupported)
        {
            externalAnalytics = ExternalAnalyticsWindow.Create(transform);
            externalAnalytics.Closed += OnExternalAnalyticsClosed;
            analyticsCanvas = externalAnalytics.Canvas;
            parent = externalAnalytics.Root;
        }
        bool external = externalAnalytics != null;

        RectTransform win;
        if (external)
        {
            // The OS window supplies the frame, title and close button; fill its client area.
            win = UITheme.NewRect("Analytics Window", parent);
            win.gameObject.AddComponent<UIRaycastTarget>();
            Image bg = UITheme.Panel("Surface", win, Color.white);
            UITheme.Fill(bg.rectTransform);
            UITheme.Fill(win);
        }
        else
        {
            win = UITheme.Card("Analytics Window", parent, 18f, Color.white, 48f, 20f, 0.3f);
            // Same top edge and height as the module control drawer, so the two sit side by side.
            UITheme.TopLeft(win, 16f, 92f, 1004f, 626f);
        }
        analyticsWindow = win.gameObject;

        // Title bar doubles as the drag handle, like a normal OS window.
        RectTransform titleBar = UITheme.NewRect("Title Bar", win);
        UITheme.TopBand(titleBar, 0f, 0f, 0f, 64f);
        titleBar.gameObject.AddComponent<UIRaycastTarget>();
        Text titleText = UITheme.Label("Window Title", titleBar, "Analytics & insights", 17f, W.ExtraBold, UITheme.Ink);
        UITheme.Fill(titleText.rectTransform, 24f, 0f, 400f, 0f);

        if (!external)
        {
            WindowDragHandle drag = titleBar.gameObject.AddComponent<WindowDragHandle>();
            drag.target = win;
            drag.canvas = canvas;
        }

        // Right-hand cluster, laid out from the right edge inwards.
        float right = 16f;
        if (!external)
        {
            Button close = UITheme.IconButton("Close Window", titleBar, Icon.Close, Kind.Secondary, 36f, 16f);
            UITheme.TopRight((RectTransform)close.transform, right, 14f, 36f, 36f);
            close.onClick.AddListener(CloseAnalyticsWindow);
            right += 36f + 8f;
        }

        Button windowReset = UITheme.MakeButton("Window Reset", titleBar, "Reset", Kind.DangerOutline, 13f, Icon.Reset, 10f, false, 15f, W.Bold, 12f);
        float rw = UITheme.PreferredWidth(windowReset);
        UITheme.TopRight((RectTransform)windowReset.transform, right, 14f, rw, 36f);
        windowReset.onClick.AddListener(ResetSimulation);
        right += rw + 8f;

        Button windowRun = UITheme.MakeButton("Window Run Toggle", titleBar, "Pause", Kind.Outline, 13f, Icon.Pause, 10f, false, 15f, W.Bold, 12f);
        UITheme.SetLabel(windowRun, "Resume");
        float runW = UITheme.PreferredWidth(windowRun);
        UITheme.SetLabel(windowRun, "Pause");
        UITheme.TopRight((RectTransform)windowRun.transform, right, 14f, runW, 36f);
        windowRun.onClick.AddListener(ToggleRunning);
        runToggleButtons.Add(windowRun);
        right += runW + 8f;

        Button exportBalance = UITheme.MakeButton("Export Mass Balance", titleBar, "Mass balance", Kind.Outline, 13f, Icon.Download, 10f, false, 15f, W.Bold, 12f);
        float ew = UITheme.PreferredWidth(exportBalance);
        UITheme.TopRight((RectTransform)exportBalance.transform, right, 14f, ew, 36f);
        exportBalance.onClick.AddListener(ExportMassBalanceCsv);
        right += ew + 12f;

        Image sep = UITheme.Panel("Divider", titleBar, UITheme.Line);
        UITheme.TopRight(sep.rectTransform, right, 20f, 1f, 24f);
        right += 13f;

        // Stats / Visualise segmented control
        Image segment = UITheme.Panel("Tab Bar", titleBar, UITheme.Sunken, 20f, true);
        analyticsStatsTabButton = UITheme.MakeButton("Stats Tab", segment.transform, "Stats", Kind.Segment, 13f, null, 16f, false, 16f, W.ExtraBold, 16f);
        analyticsVisualiseTabButton = UITheme.MakeButton("Visualise Tab", segment.transform, "Visualise", Kind.Segment, 13f, null, 16f, false, 16f, W.ExtraBold, 16f);
        float sw = UITheme.PreferredWidth(analyticsStatsTabButton);
        float vw = UITheme.PreferredWidth(analyticsVisualiseTabButton);
        UITheme.TopLeft((RectTransform)analyticsStatsTabButton.transform, 4f, 4f, sw, 32f);
        UITheme.TopLeft((RectTransform)analyticsVisualiseTabButton.transform, 6f + sw, 4f, vw, 32f);
        UITheme.TopRight(segment.rectTransform, right, 12f, sw + vw + 10f, 40f);
        analyticsStatsTabButton.onClick.AddListener(() => SetAnalyticsTab(AnalyticsTab.Stats));
        analyticsVisualiseTabButton.onClick.AddListener(() => SetAnalyticsTab(AnalyticsTab.Visualise));

        // Content area
        RectTransform content = UITheme.NewRect("Content", win);
        UITheme.Fill(content, 0f, 64f, 0f, 0f);

        BuildStatsTab(content);

        analyticsVisualiseTab = new GameObject("Visualise Tab Content", typeof(RectTransform));
        analyticsVisualiseTab.transform.SetParent(content, false);
        RectTransform visRect = (RectTransform)analyticsVisualiseTab.transform;
        UITheme.Fill(visRect);

        RectTransform subTabBar = UITheme.NewRect("Visualise Sub Tabs", visRect);
        UITheme.TopBand(subTabBar, 0f, 0f, 0f, 50f);
        Image subRule = UITheme.Panel("Rule", subTabBar, UITheme.Line);
        UITheme.BottomBand(subRule.rectTransform, 0f, 0f, 0f, 1f);
        string[] subTabNames = { "Reactor yield", "Efficiency", "OFAT timeline", "Live progress" };
        subTabButtons.Clear();
        float tx = 20f;
        for (int i = 0; i < subTabNames.Length; i++)
        {
            int idx = i;
            Button b = UITheme.MakeButton("SubTab " + i, subTabBar, subTabNames[i], Kind.Tab, 13.5f, null, 17f, false, 16f, W.ExtraBold, 15f);
            float w = UITheme.PreferredWidth(b);
            UITheme.TopLeft((RectTransform)b.transform, tx, 4f, w, 34f);
            tx += w + 6f;
            b.onClick.AddListener(() => SelectSubTab((VisualiseSubTab)idx));
            subTabButtons.Add(b);
        }

        // Live KPI strip above the graphs
        RectTransform kpiRow = UITheme.NewRect("Visualise KPI Row", visRect);
        UITheme.TopBand(kpiRow, 22f, 64f, 22f, 64f);
        string[] kpiNames = { "Overall efficiency", "CO₂ capture", "Reactor yield", "Methanol purity", "Storage fill" };
        Color[] kpiColors = { MetricEfficiency, MetricCapture, MetricYield, MetricPurity, MetricStorage };
        kpiValues = new UIValueText[5];
        kpiBars = new UIProgressBar[5];
        for (int i = 0; i < 5; i++)
        {
            Image tile = UITheme.Panel(kpiNames[i], kpiRow, UITheme.Sunken2, 12f);
            tile.rectTransform.anchorMin = new Vector2(i * 0.2f, 0f);
            tile.rectTransform.anchorMax = new Vector2((i + 1) * 0.2f, 1f);
            tile.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 5f, 0f);
            tile.rectTransform.offsetMax = new Vector2(i == 4 ? 0f : -5f, 0f);
            UITheme.Border(tile.rectTransform, UITheme.Line, 12f);
            Text caption = UITheme.Label("Caption", tile.transform, kpiNames[i], 11.5f, W.Bold, UITheme.Subtle);
            UITheme.TopBand(caption.rectTransform, 12f, 8f, 8f, 16f);
            kpiValues[i] = UITheme.ValueText("Value", tile.transform, 17f, 11.5f, UITheme.Ink, UITheme.Muted);
            UITheme.TopBand((RectTransform)kpiValues[i].transform, 12f, 25f, 8f, 22f);
            kpiBars[i] = UITheme.ProgressBar("Bar", tile.transform, kpiColors[i], 4f);
            UITheme.TopBand((RectTransform)kpiBars[i].transform, 12f, 51f, 12f, 4f);
        }

        RectTransform subContent = UITheme.NewRect("Visualise Sub Content", visRect);
        UITheme.Fill(subContent, 22f, 142f, 22f, 16f);

        float designMethanol = PlantProcessSimulator.Instance != null ? PlantProcessSimulator.Instance.DesignMethanolKgH : 1250f;

        subTabGroups[0] = BuildCorrelationSubTab(subContent, "Reactor Yield", "Reactor yield", "Reactor yield (%)",
            s => s.reactorYieldPercent, yieldParamButtons, yieldGraphGroups, true);
        subTabGroups[1] = BuildCorrelationSubTab(subContent, "Efficiency", "Overall efficiency", "Overall efficiency (%)",
            s => s.overallEfficiencyPercent, efficiencyParamButtons, efficiencyGraphGroups, false);
        subTabGroups[2] = BuildOfatTimelineSubTab(subContent);
        subTabGroups[3] = BuildLiveSubTab(subContent, designMethanol);

        SelectYieldParam(0);
        SelectEfficiencyParam(0);
        SelectSubTab(VisualiseSubTab.Yield);
        SetAnalyticsTab(AnalyticsTab.Stats);
        analyticsWindow.SetActive(false);
        if (external) externalAnalytics.AssignLayerRecursively();
    }

    private void BuildStatsTab(RectTransform content)
    {
        analyticsStatsTab = new GameObject("Stats Tab Content", typeof(RectTransform));
        analyticsStatsTab.transform.SetParent(content, false);
        RectTransform statsRect = (RectTransform)analyticsStatsTab.transform;
        UITheme.Fill(statsRect, 22f, 4f, 22f, 18f);

        Image rule = UITheme.Panel("Rule", content, UITheme.Line);
        UITheme.TopBand(rule.rectTransform, 0f, 0f, 0f, 1f);

        // Throughput tiles
        RectTransform throughput = UITheme.NewRect("Throughput", statsRect);
        UITheme.TopBand(throughput, 0f, 14f, 0f, 84f);
        string[] names = { "Methanol production", "Captured CO₂", "Syngas feed", "Recycle gas" };
        Icon[] icons = { Icon.Flask, Icon.Tank, Icon.Waves, Icon.Reset };
        Color[] colors = { MetricYield, MetricCapture, UITheme.Accent, MetricPurity };
        throughputValues = new UIValueText[4];
        for (int i = 0; i < 4; i++)
        {
            Image tile = UITheme.Panel(names[i], throughput, UITheme.Sunken2, 14f);
            tile.rectTransform.anchorMin = new Vector2(i * 0.25f, 0f);
            tile.rectTransform.anchorMax = new Vector2((i + 1) * 0.25f, 1f);
            tile.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 6f, 0f);
            tile.rectTransform.offsetMax = new Vector2(i == 3 ? 0f : -6f, 0f);
            UITheme.Border(tile.rectTransform, UITheme.Line, 14f);
            Image badge = UITheme.Badge("Badge", tile.transform, icons[i], colors[i], 30f, 9f, 17f);
            UITheme.TopLeft(badge.rectTransform, 14f, 14f, 30f, 30f);
            Text caption = UITheme.Label("Caption", tile.transform, names[i], 12.5f, W.Bold, UITheme.Subtle);
            UITheme.TopBand(caption.rectTransform, 54f, 20f, 8f, 18f);
            throughputValues[i] = UITheme.ValueText("Value", tile.transform, 24f, 13f, UITheme.Ink, UITheme.Muted);
            UITheme.TopBand((RectTransform)throughputValues[i].transform, 16f, 48f, 8f, 30f);
        }

        // Key figures
        RectTransform figures = UITheme.NewRect("Key Figures", statsRect);
        UITheme.TopBand(figures, 0f, 114f, 0f, 300f);
        Image figuresBg = UITheme.Panel("Surface", figures, Color.white, 14f);
        UITheme.Fill(figuresBg.rectTransform);
        UITheme.Border(figures, UITheme.Line, 14f);
        Text figTitle = UITheme.Label("Title", figures, "Key figures", 14f, W.ExtraBold, UITheme.Ink);
        UITheme.TopBand(figTitle.rectTransform, 20f, 16f, 20f, 20f);
        Text figCaption = UITheme.Label("Caption", figures, "Live from the running plant", 12f, W.SemiBold, UITheme.Subtle, TextAnchor.MiddleRight);
        UITheme.TopBand(figCaption.rectTransform, 20f, 16f, 20f, 20f);

        string[] metricNames = { "Overall efficiency", "CO₂ capture", "Reactor yield", "Methanol purity", "Storage fill" };
        Color[] metricColors = { MetricEfficiency, MetricCapture, MetricYield, MetricPurity, MetricStorage };
        metricValues = new UIValueText[5];
        metricBars = new UIProgressBar[5];
        for (int i = 0; i < metricNames.Length; i++)
        {
            float y = 52f + i * 48f;
            Image swatch = UITheme.Dot("Swatch", figures, 10f, metricColors[i]);
            UITheme.TopLeft(swatch.rectTransform, 20f, y + 5f, 10f, 10f);
            Text label = UITheme.Label(metricNames[i], figures, metricNames[i], 13.5f, W.SemiBold, UITheme.Ink2);
            UITheme.TopBand(label.rectTransform, 38f, y, 20f, 20f);
            metricValues[i] = UITheme.ValueText(metricNames[i] + " Value", figures, 15f, 11.5f, UITheme.Ink, UITheme.Muted, TextAnchor.LowerRight);
            RectTransform vr = (RectTransform)metricValues[i].transform;
            UITheme.TopBand(vr, 38f, y - 2f, 20f, 22f);
            metricBars[i] = UITheme.ProgressBar(metricNames[i] + " Bar", figures, metricColors[i], 8f);
            UITheme.TopBand((RectTransform)metricBars[i].transform, 20f, y + 25f, 20f, 8f);
        }

        // Insights
        Image insights = UITheme.Panel("Insights", statsRect, UITheme.AccentSoft, 14f);
        UITheme.TopBand(insights.rectTransform, 0f, 430f, 0f, 70f);
        Image bulb = UITheme.IconImage("Icon", insights.transform, Icon.Bulb, 20f, UITheme.Accent);
        UITheme.TopLeft(bulb.rectTransform, 18f, 16f, 20f, 20f);
        Text insightsTitle = UITheme.Label("Title", insights.transform, "Insights", 13f, W.ExtraBold, UITheme.AccentInk);
        UITheme.TopBand(insightsTitle.rectTransform, 48f, 12f, 18f, 18f);
        analyticsInsightsText = UITheme.Label("Insights Text", insights.transform, "", 13f, W.SemiBold, UITheme.AccentInk, TextAnchor.UpperLeft, true);
        UITheme.TopBand(analyticsInsightsText.rectTransform, 48f, 32f, 18f, 34f);
    }

    private CanvasGroup BuildCorrelationSubTab(RectTransform parent, string kind, string display, string yLabel,
        Func<PlantProcessSimulator.ProcessSnapshot, float> ySelector,
        List<Button> paramButtons, List<CanvasGroup> graphGroups, bool isYield)
    {
        GameObject panel = new GameObject(kind + " Sub Panel", typeof(RectTransform));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        UITheme.Fill(panelRect);
        CanvasGroup group = AddHiddenCanvasGroup(panel);

        RectTransform paramRow = UITheme.NewRect(kind + " Param Row", panelRect);
        UITheme.TopBand(paramRow, 0f, 0f, 0f, 32f);
        Text lbl = UITheme.Label(kind + " Param Label", paramRow, "Plot against", 12.5f, W.ExtraBold, UITheme.Subtle);
        UITheme.TopLeft(lbl.rectTransform, 0f, 0f, 90f, 32f);

        RectTransform graphHost = UITheme.NewRect("Graph Host", panelRect);
        UITheme.Fill(graphHost, 0f, 44f, 0f, 0f);

        float x = 92f;
        for (int p = 0; p < ReactorParamNames.Length; p++)
        {
            int pi = p;
            Button b = UITheme.MakeButton(kind + " Param " + p, paramRow, ReactorParamNames[p], Kind.Chip, 12.5f, null, 8f, false, 16f, W.Bold, 12f);
            float w = UITheme.PreferredWidth(b);
            UITheme.TopLeft((RectTransform)b.transform, x, 0f, w, 32f);
            x += w + 6f;
            if (isYield) b.onClick.AddListener(() => SelectYieldParam(pi));
            else b.onClick.AddListener(() => SelectEfficiencyParam(pi));
            paramButtons.Add(b);

            CanvasGroup cg = BuildCorrelationGraph(graphHost, $"{display} vs {ReactorParamTitles[p]}",
                ReactorParamAxisLabels[p], yLabel, ReactorParamSelectors[p], ySelector,
                ReactorParamMin[p], ReactorParamMax[p], 0f, 100f, p);
            graphGroups.Add(cg);
        }

        return group;
    }

    private CanvasGroup BuildOfatTimelineSubTab(RectTransform parent)
    {
        GameObject panel = new GameObject("OFAT Timeline Sub Panel", typeof(RectTransform));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        UITheme.Fill(panelRect);
        CanvasGroup group = AddHiddenCanvasGroup(panel);

        GameObject go = new GameObject("OFAT Timeline Graph", typeof(RectTransform));
        ofatTimeline = go.AddComponent<OfatTimelineGraphRuntime>();
        ofatTimeline.Initialize(panelRect, analyticsCanvas, font);
        return group;
    }

    private CanvasGroup BuildLiveSubTab(RectTransform parent, float designMethanol)
    {
        GameObject panel = new GameObject("Live Sub Panel", typeof(RectTransform));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        UITheme.Fill(panelRect);
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
            subTabButtons[i].Skin()?.SetActive((int)tab == i);
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
            buttons[i].Skin()?.SetActive(i == index);
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

    private void ExportMassBalanceCsv()
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        Canvas toastCanvas = analyticsCanvas != null ? analyticsCanvas : canvas;
        if (sim == null || sim.MassBalance == null)
        {
            GraphExportUtil.ShowToast(toastCanvas, font, "Mass balance: simulator not ready.");
            return;
        }

        string baseName = GraphExportUtil.Sanitize($"MassBalance_{DateTime.Now:yyyyMMdd_HHmmss}");
        string path = GraphExportUtil.WriteText(baseName, "csv", MassBalanceCsvExporter.BuildCsv(sim));
        GraphExportUtil.ShowToast(toastCanvas, font, path != null
            ? $"Exported {baseName}.csv to {GraphExportUtil.ExportDirectory}"
            : "Export failed — see console.");
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
        // A separate window can only be opened once; while it is open the nav button is
        // disabled and the window is closed from its own title bar.
        if (AnalyticsIsExternal)
        {
            if (!analyticsWindowOpen) OpenAnalyticsWindow();
            return;
        }
        if (analyticsWindowOpen) CloseAnalyticsWindow();
        else OpenAnalyticsWindow();
    }

    private void OpenAnalyticsWindow()
    {
        if (AnalyticsIsExternal)
        {
            if (externalAnalytics.IsOpen) return;
            analyticsWindow.SetActive(true);
            externalAnalytics.Open();
            analyticsWindowOpen = externalAnalytics.IsOpen;
            if (!analyticsWindowOpen) analyticsWindow.SetActive(false);
        }
        else
        {
            analyticsWindowOpen = true;
            if (analyticsWindow != null)
            {
                analyticsWindow.transform.SetAsLastSibling();
                SetPopupVisible(analyticsWindow, true);
            }
        }
        ApplyReactorLock();
        Refresh();
    }

    private void CloseAnalyticsWindow()
    {
        analyticsWindowOpen = false;
        if (AnalyticsIsExternal)
        {
            externalAnalytics.Close();
            if (analyticsWindow != null) analyticsWindow.SetActive(false);
        }
        else
        {
            SetPopupVisible(analyticsWindow, false);
        }
        ApplyReactorLock();
        Refresh();
    }

    /// <summary>The user closed the separate analytics window from its own title bar.</summary>
    private void OnExternalAnalyticsClosed()
    {
        analyticsWindowOpen = false;
        if (analyticsWindow != null) analyticsWindow.SetActive(false);
        ApplyReactorLock();
        Refresh();
    }

    private readonly Dictionary<GameObject, Coroutine> popupAnimations = new Dictionary<GameObject, Coroutine>();

    /// <summary>Opens/closes a popup (analytics window, about box) with a quick scale + fade
    /// tween. <paramref name="scaleTarget"/> is what scales (the card inside a full-screen
    /// modal); the popup root itself fades.</summary>
    private void SetPopupVisible(GameObject popup, bool visible, RectTransform scaleTarget = null)
    {
        if (popup == null) return;
        if (!visible && !popup.activeSelf) return;
        if (popupAnimations.TryGetValue(popup, out Coroutine running) && running != null) StopCoroutine(running);
        if (visible) popup.SetActive(true);
        popupAnimations[popup] = StartCoroutine(AnimatePopup(popup, visible, scaleTarget));
    }

    private IEnumerator AnimatePopup(GameObject popup, bool opening, RectTransform scaleTarget)
    {
        RectTransform rect = scaleTarget != null ? scaleTarget : popup.GetComponent<RectTransform>();
        CanvasGroup group = popup.GetComponent<CanvasGroup>();
        if (group == null) group = popup.AddComponent<CanvasGroup>();

        const float duration = 0.18f;
        Vector3 fromScale = opening ? Vector3.one * 0.96f : rect.localScale;
        Vector3 toScale = opening ? Vector3.one : Vector3.one * 0.96f;
        float fromAlpha = opening ? 0f : group.alpha;
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
        if (!opening)
        {
            popup.SetActive(false);
            rect.localScale = Vector3.one;
        }
        popupAnimations.Remove(popup);
    }

    private void SetAnalyticsTab(AnalyticsTab tab)
    {
        currentAnalyticsTab = tab;
        if (analyticsStatsTab != null) analyticsStatsTab.SetActive(tab == AnalyticsTab.Stats);
        if (analyticsVisualiseTab != null) analyticsVisualiseTab.SetActive(tab == AnalyticsTab.Visualise);
        analyticsStatsTabButton.Skin()?.SetActive(tab == AnalyticsTab.Stats);
        analyticsVisualiseTabButton.Skin()?.SetActive(tab == AnalyticsTab.Visualise);
        // The reactor-slider lock only applies while a locking sub-tab is actually on screen.
        ApplyReactorLock();
    }

    private CanvasGroup BuildCorrelationGraph(RectTransform container, string title, string xLabel, string yLabel,
        Func<PlantProcessSimulator.ProcessSnapshot, float> xSelector,
        Func<PlantProcessSimulator.ProcessSnapshot, float> ySelector,
        float xMin, float xMax, float yMin, float yMax, int reactorParam)
    {
        GameObject go = new GameObject(title + " Graph", typeof(RectTransform));
        CorrelationGraphRuntime graph = go.AddComponent<CorrelationGraphRuntime>();
        // Background model curves: constant temperature on every graph except the temperature
        // graph itself, where constant-temperature curves would be vertical lines, so it holds
        // pressure constant instead. ReactorParamNames order matches ReactorInput.
        graph.ShowTrendCurves = true;
        graph.XInput = (CorrelationGraphRuntime.ReactorInput)reactorParam;
        bool temperatureAxis = graph.XInput == CorrelationGraphRuntime.ReactorInput.Temperature;
        graph.FamilyInput = temperatureAxis ? CorrelationGraphRuntime.ReactorInput.Pressure : CorrelationGraphRuntime.ReactorInput.Temperature;
        graph.FamilyValues = temperatureAxis ? new[] { 40f, 55f, 70f, 85f, 100f } : new[] { 200f, 220f, 240f, 260f, 280f };
        graph.FamilyName = temperatureAxis ? "pressure" : "temperature";
        graph.FamilyUnit = temperatureAxis ? "bar" : "°C";
        graph.Title = title;
        graph.XLabel = xLabel;
        graph.YLabel = yLabel;
        graph.XSelector = xSelector;
        graph.YSelector = ySelector;
        graph.XMin = xMin;
        graph.XMax = xMax;
        graph.YMin = yMin;
        graph.YMax = yMax;
        graph.Initialize(container, analyticsCanvas, font);

        return AddHiddenCanvasGroup(go);
    }

    /// <summary>
    /// The "Live Progress" sub-tab: an Efficiency / Methanol output segmented control above two
    /// stacked LiveGraphRuntime instances (both keep sampling continuously in the background
    /// via their own CanvasGroup so switching never loses history). The output graph gets its
    /// area shaded and shows the tank's cumulative stored amount on hover.
    /// </summary>
    private void BuildLiveProgressContent(RectTransform container, float designMethanol)
    {
        Image toggleRow = UITheme.Panel("Sub Toggle Row", container, UITheme.Sunken, 18f, true);
        liveProgressEfficiencyButton = UITheme.MakeButton("Efficiency Toggle", toggleRow.transform, "Efficiency", Kind.Segment, 13f, null, 14f, false, 16f, W.ExtraBold, 16f);
        liveProgressOutputButton = UITheme.MakeButton("Output Toggle", toggleRow.transform, "Methanol output", Kind.Segment, 13f, null, 14f, false, 16f, W.ExtraBold, 16f);
        float ew = UITheme.PreferredWidth(liveProgressEfficiencyButton);
        float ow = UITheme.PreferredWidth(liveProgressOutputButton);
        UITheme.TopLeft((RectTransform)liveProgressEfficiencyButton.transform, 4f, 3f, ew, 28f);
        UITheme.TopLeft((RectTransform)liveProgressOutputButton.transform, 6f + ew, 3f, ow, 28f);
        UITheme.TopLeft(toggleRow.rectTransform, 0f, 0f, ew + ow + 10f, 34f);
        liveProgressEfficiencyButton.onClick.AddListener(() => SelectLiveProgressMode(true));
        liveProgressOutputButton.onClick.AddListener(() => SelectLiveProgressMode(false));

        RectTransform graphHost = UITheme.NewRect("Graph Host", container);
        UITheme.Fill(graphHost, 0f, 46f, 0f, 0f);

        liveProgressEfficiencyGraph = BuildLiveGraphInstance(graphHost, "Overall efficiency", "Reactor temp", "°C", "Overall efficiency (%)",
            s => s.reactorTemperatureC, s => s.overallEfficiencyPercent, 0f, 100f, false, null, "", null);

        liveProgressOutputGraph = BuildLiveGraphInstance(graphHost, "Methanol output", "Reactor temp", "°C", "Methanol output (kg/h)",
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
        graph.Initialize(container, analyticsCanvas, font);

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
        liveProgressEfficiencyButton.Skin()?.SetActive(efficiency);
        liveProgressOutputButton.Skin()?.SetActive(!efficiency);
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

    // ---- live values ---------------------------------------------------------------------

    private void Refresh()
    {
        PlantProcessSimulator simulator = PlantProcessSimulator.Instance;
        if (simulator == null || canvas == null) return;
        PlantProcessSimulator.ProcessSnapshot s = simulator.Current;
        bool running = simulator.IsRunning;

        // Run controls
        for (int i = 0; i < runToggleButtons.Count; i++)
        {
            Button button = runToggleButtons[i];
            if (button == null) continue;
            UITheme.SetLabel(button, running ? "Pause" : "Resume");
            UITheme.SetIcon(button, running ? Icon.Pause : Icon.Play);
        }
        dockRunButton.Skin()?.SetKind(running ? Kind.Dark : Kind.Primary);
        runStateText.text = running ? "Running" : "Paused";
        runStateText.color = running ? UITheme.SuccessInk : UITheme.WarningInk;
        runStateDot.color = running ? UITheme.Success : UITheme.Warning;

        if (analyticsNavButton != null)
            analyticsNavButton.interactable = !(AnalyticsIsExternal && analyticsWindowOpen);
        showStreamsButton.Skin()?.SetActive(flowLabOn);
        if (streamVisualsButton != null)
        {
            FinalPlantFlowRuntime flowRuntime = FinalPlantFlowRuntime.Instance;
            UITheme.SetLabel(streamVisualsButton, flowRuntime != null && !flowRuntime.VisualsEnabled ? "Show stream visuals" : "Hide stream visuals");
        }
        for (int i = 0; i < flowModeButtons.Count; i++)
        {
            bool on = i == flowInspectionMode;
            flowModeButtons[i].Skin()?.SetActive(on);
            Transform check = flowModeButtons[i].transform.Find("Icon");
            if (check != null && check.gameObject.activeSelf != on) check.gameObject.SetActive(on);
        }

        bool armed = MassFlowProbeRuntime.Instance != null && MassFlowProbeRuntime.Instance.IsActive;
        massFlowToolButton.Skin()?.SetActive(armed);

        // Plant status
        bool alarm = s.reactorTemperatureC >= 285f || s.reactorPressureBar >= 98f || s.storageFillPercent >= 95f;
        bool caution = !alarm && (s.reactorTemperatureC > 270f || s.captureEfficiencyPercent < 65f || s.methanolPurityPercent < 95f);
        if (!running) plantStatusChip.Set(UIStatusChip.Kind.Neutral, "Paused");
        else if (alarm) plantStatusChip.Set(UIStatusChip.Kind.Danger, "Attention required");
        else if (caution) plantStatusChip.Set(UIStatusChip.Kind.Warning, "Operating caution");
        else plantStatusChip.Set(UIStatusChip.Kind.Success, "Normal operation");

        efficiencyText.text = $"{s.overallEfficiencyPercent:F1}%";
        efficiencyRing.fillAmount = Mathf.Clamp01(s.overallEfficiencyPercent / 100f);
        productionValue.Set($"{s.methanolProductionKgH:F0}", "kg/h");
        // CO2 + 3H2 -> CH3OH + H2O. Each 32 kg of methanol represents
        // 44 kg of CO2 converted on the simplified stoichiometric basis.
        float co2ConvertedKgH = s.methanolProductionKgH * (44f / 32f);
        float co2UtilizationPercent = s.co2CapturedKgH > 0.01f
            ? Mathf.Clamp01(co2ConvertedKgH / s.co2CapturedKgH) * 100f
            : 0f;
        utilizationValue.Set($"{co2UtilizationPercent:F1}", "%");

        float tankSecs = simulator.SecondsUntilStorageFull;
        tankValueText.text = $"{s.storageFillPercent:F1} %";
        tankBar.Set(s.storageFillPercent / 100f);
        tankBar.SetColor(s.storageFillPercent >= 85f ? UITheme.Danger : UITheme.Accent);
        tankCaption.text = s.storageFillPercent >= 99.9f ? "Tank full — production is interlocked"
            : float.IsInfinity(tankSecs) ? "Not filling at the current rate"
            : tankSecs >= 5940f ? "Full in more than 99 min at the current rate"
            : $"Full in {(int)(tankSecs / 60f):00}:{(int)(tankSecs % 60f):00} min at the current rate";

        // Process tiles
        SetTile(0, $"{s.electrolyzerPowerPercent:F0}", "%", $"{s.h2InputKgH:F0}", "kg/h", $"{s.waterFeedKgH:F0}", "kg/h", $"{s.oxygenByproductKgH:F0}", "kg/h");
        SetTile(1, $"{s.captureEfficiencyPercent:F1}", "%", $"{s.co2CapturedKgH:F0}", "kg/h", $"{s.amineFlowPercent:F0}", "%", $"{s.regeneratorTemperatureC:F0}", "°C");
        SetTile(2, $"{s.reactorTemperatureC:F0}", "°C", $"{s.reactorPressureBar:F0}", "bar", $"{s.h2Co2Ratio:F1}", "", $"{s.reactorYieldPercent:F1}", "%");
        SetTile(3, $"{s.recycleRatioPercent:F0}", "%", $"{s.methanolPurityPercent:F1}", "%", $"{s.storageFillPercent:F0}", "%", $"{s.methanolProductionKgH:F0}", "kg/h");

        // Navigation
        for (int i = 0; i < navigationButtons.Count; i++)
        {
            UIButtonSkin skin = navigationButtons[i].Skin();
            if (skin == null) continue;
            DashboardPage page = navigationPages[i];
            if (page == DashboardPage.Analytics)
            {
                skin.SetKind(analyticsWindowOpen ? Kind.Tab : Kind.Nav);
                skin.SetActive(analyticsWindowOpen);
            }
            else if (page == DashboardPage.FlowInspection)
            {
                bool current = currentPage == DashboardPage.FlowInspection;
                skin.SetKind(current || !flowLabOn ? Kind.Nav : Kind.Tab);
                skin.SetActive(flowLabOn);
            }
            else
            {
                skin.SetKind(Kind.Nav);
                skin.SetActive(page == currentPage);
            }
        }

        // Section cards
        if (equipmentPanel != null && equipmentPanel.activeSelf)
        {
            reactorTempValue.Set($"{s.reactorTemperatureC:F0}", "°C");
            reactorPressureValue.Set($"{s.reactorPressureBar:F0}", "bar");
            reactorRatioValue.Set($"{s.h2Co2Ratio:F2}", "");
            reactorYieldValue.Set($"{s.reactorYieldPercent:F1}", "%");
        }
        if (simulationPanel != null && simulationPanel.activeSelf)
        {
            simLoadValue.Set($"{s.plantLoadPercent:F0}", "%");
            simSyngasValue.Set($"{s.syngasFeedKgH:F0}", "kg/h");
            simMethanolValue.Set($"{s.methanolProductionKgH:F0}", "kg/h");
            simStorageValue.Set($"{s.storageFillPercent:F1}", "%");
            bool interlock = s.storageInterlockActive;
            simCallout.color = interlock ? UITheme.WarningSoft : UITheme.SuccessSoft;
            simCalloutIcon.sprite = UITheme.IconSprite(interlock ? Icon.Alert : Icon.Check);
            simCalloutIcon.color = interlock ? UITheme.WarningInk : UITheme.SuccessInk;
            simCalloutText.color = interlock ? UITheme.WarningInk : UITheme.SuccessInk;
            simCalloutText.text = interlock
                ? "Storage interlock is active: upstream production is held back until capacity is restored."
                : "Storage has capacity. Module sliders stay active and drive every calculation.";
        }

        // Analytics
        if (analyticsWindowOpen && analyticsWindow != null)
        {
            throughputValues[0].Set($"{s.methanolProductionKgH:F0}", "kg/h");
            throughputValues[1].Set($"{s.co2CapturedKgH:F0}", "kg/h");
            throughputValues[2].Set($"{s.syngasFeedKgH:F0}", "kg/h");
            throughputValues[3].Set($"{s.recycleGasKgH:F0}", "kg/h");
            float[] metrics =
            {
                s.overallEfficiencyPercent, s.captureEfficiencyPercent, s.reactorYieldPercent,
                s.methanolPurityPercent, s.storageFillPercent
            };
            for (int i = 0; i < metrics.Length; i++)
            {
                metricValues[i].Set($"{metrics[i]:F1}", "%");
                metricBars[i].Set(metrics[i] / 100f);
                kpiValues[i].Set($"{metrics[i]:F1}", "%");
                kpiBars[i].Set(metrics[i] / 100f);
            }
            analyticsInsightsText.text = UITheme.Pretty(
                (s.captureEfficiencyPercent < 80f ? "CO2 capture is limiting carbon utilisation. " : "CO2 capture is in the preferred range. ") +
                (s.h2Co2Ratio < 2.8f || s.h2Co2Ratio > 3.2f ? "Move the synthesis feed ratio toward 3.0. " : "Synthesis feed ratio is near its target. ") +
                (s.storageFillPercent > 85f ? "Storage headroom is low; watch the interlock." : "Storage headroom is adequate."));
        }
    }

    private void SetTile(int tile, string v0, string u0, string v1, string u1, string v2, string u2, string v3, string u3)
    {
        UIValueText[] values = tileValues[tile];
        if (values == null) return;
        values[0].Set(v0, u0);
        values[1].Set(v1, u1);
        values[2].Set(v2, u2);
        values[3].Set(v3, u3);
    }

    private void SelectPage(DashboardPage page, bool moveCamera = true)
    {
        currentPage = page;
        if (processPanel != null) processPanel.SetActive(page == DashboardPage.Process);
        if (equipmentPanel != null) equipmentPanel.SetActive(page == DashboardPage.Equipment);
        if (simulationPanel != null) simulationPanel.SetActive(page == DashboardPage.Simulation);
        if (flowInspectionPanel != null) flowInspectionPanel.SetActive(page == DashboardPage.FlowInspection);
        if (legendPanel != null) legendPanel.SetActive(flowLabOn || page == DashboardPage.Overview || page == DashboardPage.Process || page == DashboardPage.FlowInspection);
        if (plantStatusPanel != null) plantStatusPanel.SetActive(page == DashboardPage.Overview);
        if (kpiStrip != null) kpiStrip.SetActive(page == DashboardPage.Overview || page == DashboardPage.Process || page == DashboardPage.Simulation);
        if (moveCamera)
        {
            if (page == DashboardPage.Overview || page == DashboardPage.FlowInspection) Focus(-1);
            else if (page == DashboardPage.Equipment) Focus(7);
        }
        Refresh();
    }

    /// <summary>
    /// FLOW LAB (and the footer's SHOW STREAMS): when the mode is off, switch it on and open its
    /// panel; when it is already on, switch it straight off without reopening the panel.
    /// </summary>
    private void ToggleFlowLab() => SetFlowLabMode(!flowLabOn);

    private void SetFlowLabMode(bool on)
    {
        flowLabOn = on;
        FinalPlantFlowRuntime flowRuntime = FinalPlantFlowRuntime.Instance != null
            ? FinalPlantFlowRuntime.Instance
            : FindFirstObjectByType<FinalPlantFlowRuntime>(FindObjectsInactive.Include);
        if (flowRuntime != null) flowRuntime.SetFlowLabActive(on);
        if (on)
        {
            SelectPage(DashboardPage.FlowInspection);
            return;
        }
        SetFlowInspectionMode(0);
        // Switching the flow off leaves the camera where the user put it.
        SelectPage(currentPage == DashboardPage.FlowInspection ? DashboardPage.Overview : currentPage, false);
    }

    /// <summary>The panel's X only hides the panel; the flow keeps running so the plant can be
    /// explored with it on.</summary>
    private void CloseFlowLab() => SelectPage(DashboardPage.Overview, false);

    private static readonly string[] ProcessTitles =
    {
        "Renewable power & water", "Electrolyzer", "Hydrogen storage", "CO₂ capture",
        "Gas mixing", "Feed heating", "Methanol reactor", "Cooling & flash separation",
        "Distillation", "Methanol storage"
    };

    private static readonly string[] ProcessDescriptions =
    {
        "Renewable electricity and treated water provide the material and energy basis for green hydrogen production.",
        "Water electrolysis produces hydrogen for synthesis and oxygen as a useful by-product.",
        "A hydrogen buffer decouples variable electrolyzer output from the steady synthesis-loop demand.",
        "Regenerable amine absorption separates and conditions carbon dioxide before compression.",
        "Fresh hydrogen, captured CO₂ and recycled synthesis gas combine at the mixing junction.",
        "The mixed synthesis gas is brought toward reactor inlet temperature before entering the fixed bed.",
        "The conditioned H₂/CO₂ mixture passes through the catalyst bed and forms methanol and water.",
        "Reactor effluent is cooled so crude methanol and water condense while unreacted gas remains available for recycle.",
        "Distillation raises methanol purity by separating water and remaining light components.",
        "The product tank accumulates methanol and activates the capacity interlock near its safe operating limit."
    };

    private static readonly string[][] ProcessStreams =
    {
        new[] { "Renewable power", "Treated water" },
        new[] { "Water", "Hydrogen", "Oxygen" },
        new[] { "Hydrogen" },
        new[] { "Flue gas", "Rich amine", "Lean amine", "Captured CO₂" },
        new[] { "Hydrogen", "CO₂", "Recycle gas", "Mixed syngas" },
        new[] { "Mixed syngas", "Heated syngas" },
        new[] { "Hot syngas", "Reactor effluent" },
        new[] { "Reactor effluent", "Crude methanol", "Recycle gas" },
        new[] { "Crude methanol", "Water-rich bottoms", "Methanol product" },
        new[] { "Methanol product" },
    };

    // Camera focus index per step (see OrbitCameraController.focusPoints): -1 = whole plant.
    private static readonly int[] ProcessFocus = { -1, 0, 3, 2, 6, 6, 7, 9, 10, 11 };

    private void SetProcessStep(int index)
    {
        processStepIndex = Mathf.Clamp(index, 0, ProcessTitles.Length - 1);
        if (processStepText != null) processStepText.text = $"Step {processStepIndex + 1} of {ProcessTitles.Length}";
        for (int i = 0; i < processSegments.Count; i++)
            processSegments[i].color = i <= processStepIndex ? UITheme.Accent : UITheme.Line;
        if (processTitleText != null) processTitleText.text = ProcessTitles[processStepIndex];
        if (processBodyText != null) processBodyText.text = ProcessDescriptions[processStepIndex];
        if (processStreamsLabel != null) processStreamsLabel.text = processStepIndex == 0 ? "Inputs" : "Streams";
        LayoutStreamChips(ProcessStreams[processStepIndex]);
        Focus(ProcessFocus[processStepIndex]);
    }

    /// <summary>Stream names as wrapped chips under the step description.</summary>
    private void LayoutStreamChips(string[] items)
    {
        if (processChipArea == null) return;
        while (processChips.Count < items.Length)
        {
            Image chip = UITheme.Panel("Chip " + processChips.Count, processChipArea, UITheme.Sunken, 13f);
            Text t = UITheme.Label("Label", chip.transform, "", 12.5f, W.Bold, UITheme.Ink2, TextAnchor.MiddleCenter);
            UITheme.Fill(t.rectTransform);
            processChips.Add(chip);
            processChipTexts.Add(t);
        }
        float x = 0f, y = 0f;
        const float gap = 6f, height = 28f, maxWidth = 304f;
        for (int i = 0; i < processChips.Count; i++)
        {
            bool used = i < items.Length;
            processChips[i].gameObject.SetActive(used);
            if (!used) continue;
            processChipTexts[i].text = items[i];
            float w = processChipTexts[i].preferredWidth + 24f;
            if (x > 0f && x + w > maxWidth)
            {
                x = 0f;
                y += height + gap;
            }
            UITheme.TopLeft(processChips[i].rectTransform, x, y, w, height);
            x += w + gap;
        }
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
        Refresh();
    }

    private void SetFlowInspectionMode(int mode)
    {
        flowInspectionMode = mode;
        FinalPlantFlowRuntime flow = FindFirstObjectByType<FinalPlantFlowRuntime>(FindObjectsInactive.Include);
        if (flow != null) flow.SetInspectionMode(mode);
        Refresh();
    }
}
