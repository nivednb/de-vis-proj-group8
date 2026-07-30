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
    private const string RuntimeRootName = "Generated ICODOS Plant Dashboard";

    private static readonly Color HeaderColor = new Color32(10, 24, 34, 248);
    private static readonly Color PanelColor = new Color32(15, 38, 51, 232);
    private static readonly Color PanelLightColor = new Color32(23, 54, 69, 236);
    private static readonly Color AccentColor = new Color32(20, 145, 205, 255);
    private static readonly Color MutedTextColor = new Color32(174, 195, 206, 255);
    private static readonly Color HealthyColor = new Color32(34, 197, 94, 255);

    private readonly List<Text> liveTexts = new List<Text>();
    private readonly List<Image> navigationImages = new List<Image>();
    private readonly List<int> navigationFocusIndices = new List<int>();
    private Canvas canvas;
    private Font font;
    private OrbitCameraController cameraController;
    private GameObject helpPanel;
    private GameObject legendPanel;
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

        string[] names = { "OVERVIEW", "ELECTROLYZER", "CARBON CAPTURE", "SYNTHESIS", "SEPARATION", "STORAGE" };
        int[] focus = { -1, 0, 2, 7, 9, 11 };
        float left = 0.31f;
        float width = 0.092f;
        for (int i = 0; i < names.Length; i++)
        {
            int target = focus[i];
            Button button = CreateButton(names[i], header, names[i], i == 0 ? AccentColor : HeaderColor, 11);
            Pin(button.GetComponent<RectTransform>(), new Vector2(left + width * i, 0f), new Vector2(left + width * (i + 1), 1f), Vector2.zero, Vector2.zero);
            button.onClick.AddListener(() => Focus(target));
            navigationImages.Add(button.GetComponent<Image>());
            navigationFocusIndices.Add(target);
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

        int currentFocus = cameraController != null ? cameraController.CurrentFocusIndex : -1;
        for (int i = 0; i < navigationImages.Count; i++)
        {
            if (navigationImages[i] != null)
                navigationImages[i].color = navigationFocusIndices[i] == currentFocus ? AccentColor : HeaderColor;
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
