using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Native Unity implementation of the group Figma dashboard.
/// The application remains fully offline/buildable while preserving the shared
/// navigation, live-conditions rail, operating controls, and educational intent.
/// </summary>
[DisallowMultipleComponent]
public sealed class IcodosDashboardRuntime : MonoBehaviour
{
    private const string RuntimeRootName = "Generated Group Figma Dashboard";

    private static readonly Color Burgundy = Hex("520B22");
    private static readonly Color DeepBlue = Hex("0A3591");
    private static readonly Color CanvasBlue = Hex("EAEFF6");
    private static readonly Color Surface = Hex("F7F9FC");
    private static readonly Color SurfaceAlt = Hex("E2E7F0");
    private static readonly Color Ink = Hex("1A2340");
    private static readonly Color Muted = Hex("5A6480");
    private static readonly Color Accent = Hex("168ED0");
    private static readonly Color Healthy = Hex("1EB56A");
    private static readonly Color Warning = Hex("E59B20");
    private static readonly Color Alarm = Hex("D43A45");

    private sealed class ModuleInfo
    {
        public readonly string id;
        public readonly string label;
        public readonly int focusIndex;
        public readonly string purpose;
        public readonly string inputs;
        public readonly string outputs;
        public readonly string principle;
        public readonly string note;

        public ModuleInfo(string id, string label, int focusIndex, string purpose, string inputs,
            string outputs, string principle, string note)
        {
            this.id = id;
            this.label = label;
            this.focusIndex = focusIndex;
            this.purpose = purpose;
            this.inputs = inputs;
            this.outputs = outputs;
            this.principle = principle;
            this.note = note;
        }
    }

    private readonly List<ModuleInfo> modules = new List<ModuleInfo>
    {
        new ModuleInfo("dashboard", "Dashboard", -1,
            "Provides a complete-plant view of the Power-to-Methanol process.",
            "Electricity, water and a CO2-containing gas source.",
            "Methanol product, oxygen by-product, water and small purge streams.",
            "Renewable electricity produces hydrogen. Captured CO2 and H2 are compressed, reacted, cooled, separated, purified and stored.",
            "This is an educational dynamic model, not CFD or safety-certified control software."),
        new ModuleInfo("analytics", "Analytics", -1,
            "Explains how operating inputs affect conversion, efficiency, purity and storage.",
            "All operator set-points and calculated process states.",
            "Mass-flow estimates, efficiencies, warnings and trends.",
            "The simplified model preserves key stoichiometric and qualitative process relationships.",
            "Use trends and warnings for learning; do not use these values for plant design."),
        new ModuleInfo("electrolyzer", "Electrolyzer", 0,
            "Splits demineralized water to create renewable hydrogen for methanol synthesis.",
            "Electric power and water.",
            "Hydrogen to synthesis; oxygen as a useful by-product.",
            "Overall reaction: 2 H2O -> 2 H2 + O2. Production follows available electrical power and water feed.",
            "Hydrogen is flammable. Industrial systems require gas detection, ventilation and pressure protection."),
        new ModuleInfo("absorber", "CO2 Absorber", 2,
            "Removes CO2 from the incoming gas using a circulating amine solvent.",
            "CO2-containing gas and lean amine.",
            "Treated gas and CO2-rich amine.",
            "Counter-current contacting transfers CO2 from the gas phase into the liquid solvent.",
            "Capture falls when solvent circulation is too low for the gas load."),
        new ModuleInfo("desorber", "Amine Desorber", 3,
            "Regenerates rich amine and releases concentrated CO2 for synthesis.",
            "CO2-rich amine and reboiler steam/heat.",
            "Concentrated CO2 and regenerated lean amine.",
            "Heating reverses chemical absorption; stripped CO2 leaves overhead while lean solvent returns to the absorber.",
            "Regeneration is energy intensive; excessive temperature can degrade solvent."),
        new ModuleInfo("compressor", "Syngas Compressor", 6,
            "Raises the H2/CO2 mixture to reactor pressure.",
            "Mixed H2, CO2 and recycle gas.",
            "Compressed synthesis gas.",
            "Staged compression with cooling reduces required work and limits discharge temperature.",
            "Pressure ratio and feed rate are constrained to a simplified safe teaching range."),
        new ModuleInfo("reactor", "Methanol Reactor", 7,
            "Converts compressed H2 and CO2 over a fixed catalyst bed.",
            "Hydrogen, carbon dioxide and recycle gas.",
            "Methanol vapour, water vapour and unreacted recycle gas.",
            "Main reaction: CO2 + 3 H2 -> CH3OH + H2O. The exothermic catalyst-bed reaction is coupled with the RWGS side reaction CO2 + H2 -> CO + H2O.",
            "Temperature, pressure, H2/CO2 ratio and space velocity jointly control conversion. Heat removal is essential."),
        new ModuleInfo("heat_exchanger", "Heat Exchanger", 8,
            "Recovers heat and cools reactor effluent so methanol and water can condense.",
            "Hot reactor effluent and cooling water.",
            "Cooled two-phase process stream and warmer cooling water.",
            "Heat passes through tube walls without mixing process and utility fluids.",
            "Cooling temperature and flow determine condenser recovery."),
        new ModuleInfo("flash", "Flash Separator", 9,
            "Separates condensed crude methanol/water from unreacted gas.",
            "Cooled two-phase reactor effluent.",
            "Crude liquid methanol/water, recycle gas and purge gas.",
            "Vapour-liquid equilibrium and gravity split the phases at the separator pressure and temperature.",
            "Recycle improves overall conversion; a purge prevents inert accumulation."),
        new ModuleInfo("distillation", "Distillation", 10,
            "Purifies crude methanol by separating methanol from water and heavier impurities.",
            "Crude methanol-water liquid.",
            "Purified methanol product and aqueous bottoms.",
            "Repeated vapour-liquid contacting enriches the more volatile methanol toward the product end.",
            "Reflux improves purity but increases reboiler and condenser duty."),
        new ModuleInfo("storage", "Methanol Storage", 11,
            "Receives purified methanol and tracks inventory.",
            "On-spec methanol product.",
            "Stored methanol for dispatch.",
            "Inventory integrates production over time. A high-high level interlock stops further simulated production.",
            "Reset/unload clears inventory and the latched storage trip."),
        new ModuleInfo("information", "Information", -1,
            "A guided introduction for people who are new to Power-to-Methanol.",
            "Start with Dashboard, then follow modules from Electrolyzer to Storage.",
            "Understanding of each unit operation, stream and operating variable.",
            "Open LEARN for purpose and chemistry, LIVE DATA for current state, and CONTROLS to run safe what-if experiments.",
            "Stream colours identify species qualitatively; mixed-gas pipes contain multiple species tracers.")
    };

    private readonly List<Image> navigationBackgrounds = new List<Image>();
    private Canvas canvas;
    private Font font;
    private OrbitCameraController cameraController;
    private PlantProcessSimulator simulator;
    private ModuleInfo selected;
    private Text selectedTitle;
    private Text selectedSubtitle;
    private Text learnText;
    private Text liveText;
    private Text alertText;
    private Text statusText;
    private Text statusValues;
    private GameObject learnRoot;
    private GameObject liveRoot;
    private GameObject controlsRoot;
    private GameObject centerOverlay;
    private Text centerOverlayTitle;
    private Text centerOverlayBody;
    private GameObject gridOverlay;
    private Image learnTab;
    private Image liveTab;
    private Image controlsTab;
    private int detailTab;
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<IcodosDashboardRuntime>() == null)
            new GameObject(RuntimeRootName).AddComponent<IcodosDashboardRuntime>();
    }

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        cameraController = FindFirstObjectByType<OrbitCameraController>();
        simulator = PlantProcessSimulator.Instance;
        HideLegacyDashboard();
        Build();
        SelectModule(modules[0]);
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
        foreach (Canvas candidate in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!candidate.transform.IsChildOf(transform) && candidate.name == "Canvas" && candidate.sortingOrder < 10)
                candidate.enabled = false;
        }
    }

    [ContextMenu("Build Group Figma Dashboard")]
    public void Build()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        navigationBackgrounds.Clear();

        GameObject canvasObject = new GameObject("Group Figma Dashboard Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440f, 1024f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        BuildHeader(canvasObject.transform);
        BuildSidebar(canvasObject.transform);
        BuildRightRail(canvasObject.transform);
        BuildViewTabs(canvasObject.transform);
        BuildBottomToolbar(canvasObject.transform);
        BuildCenterOverlay(canvasObject.transform);
        BuildGridOverlay(canvasObject.transform);
    }

    private void BuildHeader(Transform parent)
    {
        RectTransform header = CreatePanel("Figma Top Bar", parent, Burgundy);
        Pin(header, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -56f), Vector2.zero);

        Text title = CreateText("Project Title", header, "Power-to-Methanol Digital Twin", 22, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        Pin(title.rectTransform, Vector2.zero, new Vector2(0.62f, 1f), new Vector2(24f, 0f), Vector2.zero);
        Text group = CreateText("Group", header, "GROUP 8  |  OVGU  |  MASTER'S PROJECT", 11, FontStyle.Bold, TextAnchor.MiddleRight, Hex("F0C6D4"));
        Pin(group.rectTransform, new Vector2(0.62f, 0f), new Vector2(0.91f, 1f), Vector2.zero, new Vector2(-14f, 0f));
        Button exit = CreateButton("Exit", header, "EXIT", Hex("3A0718"), 12);
        Pin(exit.GetComponent<RectTransform>(), new Vector2(0.91f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        exit.onClick.AddListener(Quit);
    }

    private void BuildSidebar(Transform parent)
    {
        RectTransform side = CreatePanel("Figma Module Navigation", parent, DeepBlue);
        Pin(side, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 48f), new Vector2(220f, -56f));

        Text heading = CreateText("Navigation Heading", side, "PROCESS MODULES", 12, FontStyle.Bold, TextAnchor.MiddleLeft, Hex("AFC5F8"));
        AnchorTopLeft(heading.rectTransform, new Vector2(18f, -18f), new Vector2(184f, 24f));

        float y = -54f;
        for (int i = 0; i < modules.Count; i++)
        {
            ModuleInfo module = modules[i];
            Button button = CreateButton(module.id, side, module.label, i == 0 ? Accent : DeepBlue, 13);
            RectTransform rect = button.GetComponent<RectTransform>();
            AnchorTopLeft(rect, new Vector2(10f, y), new Vector2(200f, 42f));
            Text label = rect.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(18f, 2f);
            button.onClick.AddListener(() => SelectModule(module));
            navigationBackgrounds.Add(button.GetComponent<Image>());
            y -= 44f;
        }
    }

    private void BuildRightRail(Transform parent)
    {
        RectTransform rail = CreatePanel("Figma Inspector Rail", parent, Surface);
        Pin(rail, new Vector2(1f, 0f), Vector2.one, new Vector2(-260f, 48f), new Vector2(0f, -56f));
        AddBorder(rail, new Vector2(0f, 0f), new Vector2(3f, 1f), Hex("C9D1DF"));

        selectedTitle = CreateText("Selected Module", rail, "Dashboard", 20, FontStyle.Bold, TextAnchor.UpperLeft, Ink);
        AnchorTopLeft(selectedTitle.rectTransform, new Vector2(16f, -18f), new Vector2(228f, 32f));
        selectedSubtitle = CreateText("Selected Subtitle", rail, "Complete plant", 11, FontStyle.Normal, TextAnchor.UpperLeft, Muted);
        AnchorTopLeft(selectedSubtitle.rectTransform, new Vector2(16f, -50f), new Vector2(228f, 28f));

        learnTab = AddDetailTab(rail, "LEARN", 0, 0f);
        liveTab = AddDetailTab(rail, "LIVE DATA", 1, 1f / 3f);
        controlsTab = AddDetailTab(rail, "CONTROLS", 2, 2f / 3f);

        learnRoot = CreateEmpty("Learn View", rail);
        Pin(learnRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(14f, 172f), new Vector2(-14f, -126f));
        learnText = CreateText("Learn Content", learnRoot.transform, "", 13, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
        Pin(learnText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        liveRoot = CreateEmpty("Live Data View", rail);
        Pin(liveRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(14f, 172f), new Vector2(-14f, -126f));
        liveText = CreateText("Live Content", liveRoot.transform, "", 14, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
        Pin(liveText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        controlsRoot = CreateEmpty("Controls View", rail);
        Pin(controlsRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(12f, 130f), new Vector2(-12f, -126f));

        RectTransform status = CreatePanel("Plant Status", rail, CanvasBlue);
        Pin(status, Vector2.zero, new Vector2(1f, 0f), new Vector2(12f, 14f), new Vector2(-12f, 116f));
        statusText = CreateText("Status", status, "NORMAL OPERATION", 12, FontStyle.Bold, TextAnchor.UpperLeft, Healthy);
        Pin(statusText.rectTransform, new Vector2(0f, 0.62f), Vector2.one, new Vector2(12f, 0f), new Vector2(-8f, -8f));
        statusValues = CreateText("Status Values", status, "", 11, FontStyle.Normal, TextAnchor.UpperLeft, Muted);
        Pin(statusValues.rectTransform, Vector2.zero, new Vector2(1f, 0.64f), new Vector2(12f, 8f), new Vector2(-8f, 0f));
        alertText = CreateText("Alert", rail, "", 11, FontStyle.Bold, TextAnchor.UpperLeft, Alarm);
        AnchorTopLeft(alertText.rectTransform, new Vector2(16f, -132f), new Vector2(228f, 34f));

        ShowDetailTab(0);
    }

    private Image AddDetailTab(RectTransform parent, string label, int tab, float x)
    {
        Button button = CreateButton(label, parent, label, tab == 0 ? Accent : SurfaceAlt, 11);
        RectTransform rect = button.GetComponent<RectTransform>();
        Pin(rect, new Vector2(x, 1f), new Vector2(x + 1f / 3f, 1f), new Vector2(1f, -120f), new Vector2(-1f, -82f));
        Text text = rect.GetComponentInChildren<Text>();
        text.color = tab == 0 ? Color.white : Ink;
        button.onClick.AddListener(() => ShowDetailTab(tab));
        return button.GetComponent<Image>();
    }

    private void BuildViewTabs(Transform parent)
    {
        RectTransform tabs = CreatePanel("Viewport Mode Tabs", parent, Surface);
        Pin(tabs, new Vector2(0f, 1f), Vector2.one, new Vector2(220f, -100f), new Vector2(-260f, -56f));
        string[] names = { "3D VIEW", "GUIDED FLOW", "PROCESS DATA" };
        for (int i = 0; i < names.Length; i++)
        {
            int mode = i;
            Button button = CreateButton(names[i], tabs, names[i], i == 0 ? Accent : Surface, 12);
            Pin(button.GetComponent<RectTransform>(), new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f),
                new Vector2(1f, 1f), new Vector2(-1f, -1f));
            button.GetComponentInChildren<Text>().color = i == 0 ? Color.white : Ink;
            button.onClick.AddListener(() => ShowCenterMode(mode));
        }
    }

    private void BuildBottomToolbar(Transform parent)
    {
        RectTransform toolbar = CreatePanel("Viewport Toolbar", parent, Surface);
        Pin(toolbar, Vector2.zero, new Vector2(1f, 0f), new Vector2(220f, 0f), new Vector2(-260f, 48f));
        AddToolbarButton(toolbar, "RESET VIEW", 0, () => Focus(-1));
        AddToolbarButton(toolbar, "FOCUS MODULE", 1, () => Focus(selected != null ? selected.focusIndex : -1));
        AddToolbarButton(toolbar, "GRID", 2, () => gridOverlay.SetActive(!gridOverlay.activeSelf));
        AddToolbarButton(toolbar, "PIPE FLOW", 3, ToggleStreams);
    }

    private void AddToolbarButton(RectTransform toolbar, string label, int index, Action action)
    {
        Button button = CreateButton(label, toolbar, label, Surface, 11);
        Pin(button.GetComponent<RectTransform>(), new Vector2(index * 0.25f, 0f), new Vector2((index + 1) * 0.25f, 1f),
            new Vector2(1f, 1f), new Vector2(-1f, -1f));
        button.GetComponentInChildren<Text>().color = Ink;
        button.onClick.AddListener(() => action());
    }

    private void BuildCenterOverlay(Transform parent)
    {
        centerOverlay = CreatePanel("Educational Center Overlay", parent, new Color32(247, 249, 252, 244)).gameObject;
        RectTransform panel = centerOverlay.GetComponent<RectTransform>();
        Pin(panel, Vector2.zero, Vector2.one, new Vector2(246f, 78f), new Vector2(-286f, -126f));
        centerOverlayTitle = CreateText("Title", panel, "", 26, FontStyle.Bold, TextAnchor.UpperLeft, Ink);
        Pin(centerOverlayTitle.rectTransform, new Vector2(0f, 0.88f), Vector2.one, new Vector2(28f, 0f), new Vector2(-28f, -20f));
        centerOverlayBody = CreateText("Body", panel, "", 17, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
        Pin(centerOverlayBody.rectTransform, Vector2.zero, new Vector2(1f, 0.88f), new Vector2(28f, 28f), new Vector2(-28f, -10f));
        centerOverlay.SetActive(false);
    }

    private void BuildGridOverlay(Transform parent)
    {
        gridOverlay = CreateEmpty("Viewport Grid", parent);
        RectTransform root = gridOverlay.GetComponent<RectTransform>();
        Pin(root, Vector2.zero, Vector2.one, new Vector2(220f, 48f), new Vector2(-260f, -100f));
        for (int i = 1; i < 12; i++)
        {
            RectTransform line = CreatePanel("Vertical " + i, root, new Color32(30, 69, 120, 24));
            Pin(line, new Vector2(i / 12f, 0f), new Vector2(i / 12f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f));
        }
        for (int i = 1; i < 8; i++)
        {
            RectTransform line = CreatePanel("Horizontal " + i, root, new Color32(30, 69, 120, 24));
            Pin(line, new Vector2(0f, i / 8f), new Vector2(1f, i / 8f), new Vector2(0f, -1f), new Vector2(0f, 1f));
        }
        gridOverlay.SetActive(false);
    }

    private void SelectModule(ModuleInfo module)
    {
        selected = module;
        selectedTitle.text = module.label;
        selectedSubtitle.text = module.focusIndex >= 0 ? "EQUIPMENT " + (module.focusIndex + 1) : "PLANT LEARNING VIEW";
        learnText.text =
            "<b>WHAT IT DOES</b>\n" + module.purpose + "\n\n" +
            "<b>INPUTS</b>\n" + module.inputs + "\n\n" +
            "<b>OUTPUTS</b>\n" + module.outputs + "\n\n" +
            "<b>HOW IT WORKS</b>\n" + module.principle + "\n\n" +
            "<b>ENGINEERING NOTE</b>\n" + module.note;
        learnText.supportRichText = true;

        for (int i = 0; i < modules.Count && i < navigationBackgrounds.Count; i++)
            navigationBackgrounds[i].color = modules[i] == module ? Accent : DeepBlue;

        RebuildControls();
        RefreshLiveText();
        if (module.focusIndex >= 0) Focus(module.focusIndex);
    }

    private void ShowDetailTab(int tab)
    {
        detailTab = tab;
        learnRoot.SetActive(tab == 0);
        liveRoot.SetActive(tab == 1);
        controlsRoot.SetActive(tab == 2);
        SetTabColor(learnTab, tab == 0);
        SetTabColor(liveTab, tab == 1);
        SetTabColor(controlsTab, tab == 2);
        if (tab == 2) RebuildControls();
    }

    private void SetTabColor(Image image, bool active)
    {
        image.color = active ? Accent : SurfaceAlt;
        Text label = image.GetComponentInChildren<Text>();
        if (label != null) label.color = active ? Color.white : Ink;
    }

    private void ShowCenterMode(int mode)
    {
        if (mode == 0)
        {
            centerOverlay.SetActive(false);
            return;
        }

        centerOverlay.SetActive(true);
        if (mode == 1)
        {
            centerOverlayTitle.text = "Guided Power-to-Methanol Flow";
            centerOverlayBody.text =
                "1  WATER + RENEWABLE POWER\n   Electrolysis produces hydrogen and oxygen.\n\n" +
                "2  CO2 CAPTURE\n   Amine absorbs CO2; the desorber regenerates solvent and releases concentrated CO2.\n\n" +
                "3  MIXING + COMPRESSION\n   H2, CO2 and recycle gas form synthesis gas near H2/CO2 = 3.\n\n" +
                "4  CATALYTIC SYNTHESIS\n   CO2 + 3 H2 -> CH3OH + H2O in an exothermic fixed-bed reactor.\n\n" +
                "5  COOLING + SEPARATION\n   Methanol/water condense; unreacted gas is recycled with a small purge.\n\n" +
                "6  PURIFICATION + STORAGE\n   Distillation purifies methanol before protected storage.";
        }
        else
        {
            centerOverlayTitle.text = "Current Process Data";
            centerOverlayBody.text = BuildPlantDataText();
        }
    }

    private void RebuildControls()
    {
        if (controlsRoot == null) return;
        for (int i = controlsRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(controlsRoot.transform.GetChild(i).gameObject);
        if (simulator == null) simulator = PlantProcessSimulator.Instance;
        if (simulator == null || selected == null) return;

        PlantProcessSimulator.ProcessSnapshot s = simulator.Current;
        float y = -8f;
        switch (selected.id)
        {
            case "dashboard":
            case "analytics":
                AddSlider(controlsRoot.transform, "Plant timeline / load", 0f, 100f, s.timelinePercent, "%", simulator.SetTimelinePercent, ref y);
                break;
            case "electrolyzer":
                AddSlider(controlsRoot.transform, "Electrical power", 0f, 100f, s.electrolyzerPowerPercent, "%", simulator.SetElectrolyzerPower, ref y);
                AddSlider(controlsRoot.transform, "Water feed", 0f, 130f, s.waterFeedPercent, "%", simulator.SetWaterFeed, ref y);
                break;
            case "absorber":
                AddSlider(controlsRoot.transform, "Flue-gas flow", 0f, 130f, s.flueGasFlowPercent, "%", simulator.SetFlueGasFlow, ref y);
                AddSlider(controlsRoot.transform, "Amine circulation", 20f, 130f, s.amineFlowPercent, "%", simulator.SetAmineFlow, ref y);
                break;
            case "desorber":
                AddSlider(controlsRoot.transform, "Regenerator steam", 0f, 100f, s.regeneratorSteamPercent, "%", simulator.SetRegeneratorSteam, ref y);
                AddSlider(controlsRoot.transform, "Regenerator temperature", 80f, 140f, s.regeneratorTemperatureC, " C", simulator.SetRegeneratorTemperature, ref y);
                break;
            case "compressor":
                AddSlider(controlsRoot.transform, "Compression ratio", 1f, 6f, s.compressionRatio, " x", simulator.SetCompressionRatio, ref y);
                AddSlider(controlsRoot.transform, "Reactor feed", 20f, 130f, s.reactorFeedFlowPercent, "%", simulator.SetReactorFeedFlow, ref y);
                break;
            case "reactor":
                AddSlider(controlsRoot.transform, "Temperature", 200f, 300f, s.reactorTemperatureC, " C", simulator.SetReactorTemperature, ref y);
                AddSlider(controlsRoot.transform, "Pressure", 35f, 110f, s.reactorPressureBar, " bar", simulator.SetReactorPressure, ref y);
                AddSlider(controlsRoot.transform, "H2 / CO2 ratio", 2f, 4f, s.h2Co2Ratio, "", simulator.SetH2Co2Ratio, ref y);
                AddSlider(controlsRoot.transform, "Space velocity (GHSV)", 2000f, 20000f, s.ghsv, " h-1", simulator.SetGHSV, ref y);
                break;
            case "heat_exchanger":
                AddSlider(controlsRoot.transform, "Cooling-water flow", 0f, 100f, s.coolingWaterFlowPercent, "%", simulator.SetCoolingWaterFlow, ref y);
                AddSlider(controlsRoot.transform, "Cooling-water temperature", 5f, 45f, s.coolingWaterTemperatureC, " C", simulator.SetCoolingWaterTemperature, ref y);
                break;
            case "flash":
                AddSlider(controlsRoot.transform, "Separator temperature", 20f, 65f, s.separatorTemperatureC, " C", simulator.SetSeparatorTemperature, ref y);
                AddSlider(controlsRoot.transform, "Recycle ratio", 0f, 100f, s.recycleRatioPercent, "%", simulator.SetRecycleRatio, ref y);
                break;
            case "distillation":
                AddSlider(controlsRoot.transform, "Reflux ratio", 0.5f, 5f, s.refluxRatio, "", simulator.SetRefluxRatio, ref y);
                AddSlider(controlsRoot.transform, "Reboiler temperature", 70f, 115f, s.distillationReboilerTemperatureC, " C", simulator.SetDistillationReboilerTemperature, ref y);
                break;
            case "storage":
                Text inventory = CreateText("Inventory", controlsRoot.transform,
                    $"Storage: {s.storageFillPercent:F1}%\nInventory: {s.storedMethanolKg:F0} kg\n\n" +
                    "Unloading the tank also clears a latched high-high production interlock.",
                    14, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
                AnchorTopLeft(inventory.rectTransform, new Vector2(4f, y), new Vector2(220f, 110f));
                y -= 126f;
                Button reset = CreateButton("Unload Storage", controlsRoot.transform, "UNLOAD / RESET STORAGE", Burgundy, 12);
                AnchorTopLeft(reset.GetComponent<RectTransform>(), new Vector2(4f, y), new Vector2(220f, 42f));
                reset.onClick.AddListener(simulator.ResetStoredMethanol);
                break;
            default:
                Text hint = CreateText("Hint", controlsRoot.transform,
                    "This page is informational. Select a process module to expose its safe teaching controls.",
                    14, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
                AnchorTopLeft(hint.rectTransform, new Vector2(4f, y), new Vector2(220f, 100f));
                break;
        }
    }

    private void AddSlider(Transform parent, string label, float min, float max, float value, string unit,
        UnityAction<float> callback, ref float y)
    {
        Text name = CreateText(label, parent, label, 12, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
        AnchorTopLeft(name.rectTransform, new Vector2(4f, y), new Vector2(150f, 22f));
        Text readout = CreateText(label + " Value", parent, FormatValue(value, unit), 12, FontStyle.Bold, TextAnchor.MiddleRight, DeepBlue);
        AnchorTopLeft(readout.rectTransform, new Vector2(154f, y), new Vector2(70f, 22f));
        y -= 28f;

        RectTransform background = CreatePanel(label + " Track", parent, Hex("CCD5E4"));
        AnchorTopLeft(background, new Vector2(4f, y), new Vector2(220f, 8f));
        RectTransform fillArea = CreateEmpty(label + " Fill Area", background).GetComponent<RectTransform>();
        Pin(fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform fill = CreatePanel(label + " Fill", fillArea, Accent);
        Pin(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform handleArea = CreateEmpty(label + " Handle Area", background).GetComponent<RectTransform>();
        Pin(handleArea, Vector2.zero, Vector2.one, new Vector2(-7f, 0f), new Vector2(7f, 0f));
        RectTransform handle = CreatePanel(label + " Handle", handleArea, Burgundy);
        handle.sizeDelta = new Vector2(16f, 16f);

        Slider slider = background.gameObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
        slider.onValueChanged.AddListener(v =>
        {
            readout.text = FormatValue(v, unit);
            callback(v);
        });
        y -= 42f;
    }

    private static string FormatValue(float value, string unit)
    {
        string number = Mathf.Abs(value) >= 100f ? value.ToString("F0") : value.ToString("F1");
        return number + unit;
    }

    private void Refresh()
    {
        if (simulator == null) simulator = PlantProcessSimulator.Instance;
        if (simulator == null) return;
        PlantProcessSimulator.ProcessSnapshot s = simulator.Current;

        bool alarm = s.storageInterlockActive || s.reactorTemperatureC >= 285f || s.reactorPressureBar >= 98f;
        bool caution = !alarm && (s.storageFillPercent >= 85f || s.captureEfficiencyPercent < 65f || s.methanolPurityPercent < 95f);
        statusText.text = alarm ? "PROCESS INTERLOCK / ALARM" : caution ? "OPERATING CAUTION" : "NORMAL OPERATION";
        statusText.color = alarm ? Alarm : caution ? Warning : Healthy;
        statusValues.text =
            $"Efficiency  {s.overallEfficiencyPercent:F1}%\n" +
            $"Methanol   {s.methanolProductionKgH:F0} kg/h\n" +
            $"Storage    {s.storageFillPercent:F1}%";
        alertText.text = s.storageInterlockActive
            ? "Storage high-high trip active. Open Storage > Controls to unload/reset."
            : caution ? "Review the highlighted live conditions before increasing load." : "";
        if (detailTab == 1) RefreshLiveText();
        if (centerOverlay != null && centerOverlay.activeSelf && centerOverlayTitle.text == "Current Process Data")
            centerOverlayBody.text = BuildPlantDataText();
    }

    private void RefreshLiveText()
    {
        if (liveText == null || selected == null || simulator == null) return;
        PlantProcessSimulator.ProcessSnapshot s = simulator.Current;
        switch (selected.id)
        {
            case "electrolyzer":
                liveText.text = $"Power\n{s.electrolyzerPowerPercent:F1}%\n\nHydrogen\n{s.h2InputKgH:F0} kg/h\n\nWater feed\n{s.waterFeedKgH:F0} kg/h\n\nOxygen by-product\n{s.oxygenByproductKgH:F0} kg/h";
                break;
            case "absorber":
                liveText.text = $"Gas flow\n{s.flueGasFlowPercent:F1}%\n\nAmine flow\n{s.amineFlowPercent:F1}%\n\nCO2 captured\n{s.co2CapturedKgH:F0} kg/h\n\nCapture efficiency\n{s.captureEfficiencyPercent:F1}%";
                break;
            case "desorber":
                liveText.text = $"Steam duty\n{s.regeneratorSteamPercent:F1}%\n\nRegenerator temperature\n{s.regeneratorTemperatureC:F1} C\n\nCO2 product\n{s.co2CapturedKgH:F0} kg/h";
                break;
            case "compressor":
                liveText.text = $"Compression ratio\n{s.compressionRatio:F2} x\n\nFeed flow\n{s.reactorFeedFlowPercent:F1}%\n\nSyngas feed\n{s.syngasFeedKgH:F0} kg/h";
                break;
            case "reactor":
                liveText.text = $"Temperature\n{s.reactorTemperatureC:F1} C\n\nPressure\n{s.reactorPressureBar:F1} bar\n\nH2 / CO2\n{s.h2Co2Ratio:F2}\n\nGHSV\n{s.ghsv:F0} h-1\n\nSingle-pass yield\n{s.reactorYieldPercent:F1}%";
                break;
            case "heat_exchanger":
                liveText.text = $"Cooling-water flow\n{s.coolingWaterFlowPercent:F1}%\n\nCooling-water inlet\n{s.coolingWaterTemperatureC:F1} C\n\nCondenser recovery\n{s.condenserRecoveryPercent:F1}%";
                break;
            case "flash":
                liveText.text = $"Separator temperature\n{s.separatorTemperatureC:F1} C\n\nRecycle ratio\n{s.recycleRatioPercent:F1}%\n\nRecycle gas\n{s.recycleGasKgH:F0} kg/h";
                break;
            case "distillation":
                liveText.text = $"Reflux ratio\n{s.refluxRatio:F2}\n\nReboiler temperature\n{s.distillationReboilerTemperatureC:F1} C\n\nMethanol purity\n{s.methanolPurityPercent:F2}%";
                break;
            case "storage":
                liveText.text = $"Methanol production\n{s.methanolProductionKgH:F0} kg/h\n\nStored methanol\n{s.storedMethanolKg:F0} kg\n\nTank level\n{s.storageFillPercent:F1}%\n\nInterlock\n{(s.storageInterlockActive ? "TRIPPED" : "Ready")}";
                break;
            default:
                liveText.text = BuildPlantDataText();
                break;
        }
    }

    private string BuildPlantDataText()
    {
        if (simulator == null) return "Process model is initializing.";
        PlantProcessSimulator.ProcessSnapshot s = simulator.Current;
        float co2Converted = s.methanolProductionKgH * (44f / 32f);
        float utilization = s.co2CapturedKgH > 0.01f ? Mathf.Clamp01(co2Converted / s.co2CapturedKgH) * 100f : 0f;
        return
            $"PLANT LOAD                 {s.plantLoadPercent:F1}%\n\n" +
            $"HYDROGEN                  {s.h2InputKgH:F0} kg/h\n" +
            $"CAPTURED CO2              {s.co2CapturedKgH:F0} kg/h\n" +
            $"CO2 UTILIZATION           {utilization:F1}%\n\n" +
            $"REACTOR T / P             {s.reactorTemperatureC:F0} C / {s.reactorPressureBar:F0} bar\n" +
            $"H2 / CO2 RATIO            {s.h2Co2Ratio:F2}\n" +
            $"REACTOR YIELD             {s.reactorYieldPercent:F1}%\n\n" +
            $"METHANOL PRODUCT          {s.methanolProductionKgH:F0} kg/h\n" +
            $"PRODUCT PURITY            {s.methanolPurityPercent:F2}%\n" +
            $"STORAGE LEVEL             {s.storageFillPercent:F1}%\n" +
            $"OVERALL EFFICIENCY        {s.overallEfficiencyPercent:F1}%";
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

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void AddBorder(RectTransform parent, Vector2 anchor, Vector2 size, Color color)
    {
        RectTransform border = CreatePanel("Border", parent, color);
        border.anchorMin = new Vector2(anchor.x, 0f);
        border.anchorMax = new Vector2(anchor.x, 1f);
        border.pivot = new Vector2(anchor.x, 0.5f);
        border.sizeDelta = size;
    }

    private static GameObject CreateEmpty(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = CreateEmpty(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go.GetComponent<RectTransform>();
    }

    private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, TextAnchor alignment, Color color)
    {
        GameObject go = CreateEmpty(name, parent);
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
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.14f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
        colors.selectedColor = Accent;
        button.colors = colors;
        Text text = CreateText("Label", rect, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Pin(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 3f), new Vector2(-6f, -3f));
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
}
