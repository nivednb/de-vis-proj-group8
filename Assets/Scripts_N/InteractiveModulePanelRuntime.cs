using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive module UI layer for the complete plant.
/// It adds compact eye buttons near process modules. Each opened panel contains
/// module-specific controls, and those controls feed PlantProcessSimulator so
/// flow visuals and production/storage values respond in real time.
/// </summary>
[DisallowMultipleComponent]
public class InteractiveModulePanelRuntime : MonoBehaviour
{
    private const string RuntimeRootName = "Generated Interactive Module Panels";

    [SerializeField] private Vector2 buttonSize = new Vector2(44f, 32f);
    [SerializeField] private Vector2 panelSize = new Vector2(430f, 310f);
    [SerializeField] private Vector2 panelOffset = new Vector2(245f, -160f);

    private Canvas canvas;
    private Camera mainCamera;
    private Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<InteractiveModulePanelRuntime>() != null)
        {
            return;
        }

        GameObject go = new GameObject(RuntimeRootName);
        go.AddComponent<InteractiveModulePanelRuntime>();
    }

    private void Start()
    {
        RemoveOldStaticEyeUi();
        mainCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Build();
    }

    private void LateUpdate()
    {
        if (mainCamera == null || canvas == null) return;

        foreach (Transform child in canvas.transform)
        {
            ModuleAnchor anchor = child.GetComponent<ModuleAnchor>();
            if (anchor == null || anchor.Target == null) continue;

            Vector3 screen = mainCamera.WorldToScreenPoint(anchor.Target.position + anchor.WorldOffset);
            bool visible = screen.z > 0f;
            anchor.ButtonRoot.SetActive(visible);
            if (!visible)
            {
                anchor.PanelRoot.SetActive(false);
                continue;
            }

            anchor.ButtonRect.position = screen;
            anchor.PanelRect.position = screen + new Vector3(panelOffset.x, panelOffset.y, 0f);

            if (anchor.PanelRoot.activeSelf && anchor.LiveText != null)
            {
                anchor.LiveText.text = BuildLiveText(anchor.ModuleId);
            }
        }
    }

    [ContextMenu("Build Interactive Module Panels")]
    public void Build()
    {
        Clear();
        CreateCanvas();

        AddModule("electrolyzer", "Electrolyzer", new[] { "electrolyzer box", "Electrolyzer" }, new Vector3(0f, 3.2f, 0f));
        AddModule("absorber", "CO2 Absorber", new[] { "Absorber Column", "absorber column" }, new Vector3(0f, 5.8f, 0f));
        AddModule("desorber", "Desorber / Regenerator", new[] { "deabsorber column", "Desorber", "Regenerator" }, new Vector3(0f, 5.8f, 0f));
        AddModule("compressor", "Compressor", new[] { "compressor_block", "Compressor" }, new Vector3(0f, 3.2f, 0f));
        AddModule("reactor", "Methanol Reactor", new[] { "Reactor Bed", "Reactor base model", "reactor steel skirt" }, new Vector3(0f, 5.4f, 0f));
        AddModule("condenser", "Condenser", new[] { "Condenser", "condenser" }, new Vector3(0f, 3.5f, 0f));
        AddModule("separator", "Separator + Recycle", new[] { "flash separator", "Separator" }, new Vector3(0f, 4.0f, 0f));
        AddModule("distillation", "Distillation Column", new[] { "distillation column", "Distillation" }, new Vector3(0f, 5.8f, 0f));
        AddModule("storage", "Methanol Storage", new[] { "methanol tank", "Methanol Storage" }, new Vector3(0f, 3.2f, 0f));
    }

    [ContextMenu("Clear Interactive Module Panels")]
    public void Clear()
    {
        if (canvas != null)
        {
            DestroyObject(canvas.gameObject);
            canvas = null;
        }
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Interactive Module Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 75;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private void RemoveOldStaticEyeUi()
    {
        GameObject oldUi = GameObject.Find("Generated Module Eye UI");
        if (oldUi != null)
        {
            Destroy(oldUi);
        }
    }

    private void AddModule(string id, string title, string[] targetNames, Vector3 offset)
    {
        Transform target = FindTarget(targetNames);
        if (target == null)
        {
            Debug.LogWarning($"InteractiveModulePanelRuntime: target not found for {title}");
            return;
        }

        GameObject anchorObject = new GameObject(title + " Interactive Anchor");
        anchorObject.transform.SetParent(canvas.transform, false);
        ModuleAnchor anchor = anchorObject.AddComponent<ModuleAnchor>();
        anchor.ModuleId = id;
        anchor.Target = target;
        anchor.WorldOffset = offset;

        anchor.ButtonRoot = CreateEyeButton(anchorObject.transform, title, out anchor.ButtonRect, out Button eyeButton);
        anchor.PanelRoot = CreatePanel(anchorObject.transform, title, id, out anchor.PanelRect, out anchor.LiveText);
        anchor.PanelRoot.SetActive(false);

        eyeButton.onClick.AddListener(() => anchor.PanelRoot.SetActive(!anchor.PanelRoot.activeSelf));
    }

    private GameObject CreateEyeButton(Transform parent, string title, out RectTransform rect, out Button button)
    {
        GameObject root = new GameObject(title + " Eye Button");
        root.transform.SetParent(parent, false);
        rect = root.AddComponent<RectTransform>();
        rect.sizeDelta = buttonSize;

        Image image = root.AddComponent<Image>();
        image.color = new Color(0.04f, 0.1f, 0.14f, 0.92f);
        button = root.AddComponent<Button>();

        Text eye = CreateText("Eye Icon", root.transform, "VIEW", 12, TextAnchor.MiddleCenter, Color.white);
        eye.fontStyle = FontStyle.Bold;
        Stretch(eye.rectTransform);
        return root;
    }

    private GameObject CreatePanel(Transform parent, string title, string id, out RectTransform rect, out Text liveText)
    {
        GameObject root = new GameObject(title + " Interactive Panel");
        root.transform.SetParent(parent, false);
        rect = root.AddComponent<RectTransform>();
        rect.sizeDelta = panelSize;

        Image image = root.AddComponent<Image>();
        image.color = new Color(0.035f, 0.055f, 0.07f, 0.95f);

        Text titleText = CreateText("Title", root.transform, title, 17, TextAnchor.UpperLeft, new Color(0.42f, 0.86f, 1f, 1f));
        titleText.fontStyle = FontStyle.Bold;
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.offsetMin = new Vector2(14f, -42f);
        titleText.rectTransform.offsetMax = new Vector2(-44f, -10f);

        Button close = CreateSmallButton(root.transform, "X", new Vector2(-18f, -18f), new Vector2(30f, 30f));
        close.onClick.AddListener(() => root.SetActive(false));

        liveText = CreateText("Live Values", root.transform, "", 13, TextAnchor.UpperLeft, Color.white);
        liveText.rectTransform.anchorMin = new Vector2(0f, 0f);
        liveText.rectTransform.anchorMax = new Vector2(1f, 1f);
        liveText.rectTransform.offsetMin = new Vector2(14f, 112f);
        liveText.rectTransform.offsetMax = new Vector2(-14f, -52f);

        CreateControls(root.transform, id);
        return root;
    }

    private void CreateControls(Transform parent, string id)
    {
        switch (id)
        {
            case "electrolyzer":
                CreateSlider(parent, "Plant load", 0f, 100f, 100f, 0, "%", v => Simulator()?.SetTimelinePercent(v), -64f);
                CreateSlider(parent, "Power", 0f, 100f, 75f, 0, "%", v => Simulator()?.SetElectrolyzerPower(v), -99f);
                CreateSlider(parent, "Water feed", 0f, 130f, 100f, 0, "%", v => Simulator()?.SetWaterFeed(v), -134f);
                break;
            case "absorber":
                CreateSlider(parent, "Amine flow", 10f, 100f, 65f, 0, "%", v => Simulator()?.SetAmineFlow(v), -64f);
                CreateSlider(parent, "Flue gas", 0f, 130f, 100f, 0, "%", v => Simulator()?.SetFlueGasFlow(v), -99f);
                break;
            case "desorber":
                CreateSlider(parent, "Steam flow", 0f, 100f, 70f, 0, "%", v => Simulator()?.SetRegeneratorSteam(v), -64f);
                CreateSlider(parent, "Regen temp", 80f, 130f, 105f, 0, " C", v => Simulator()?.SetRegeneratorTemperature(v), -99f);
                break;
            case "compressor":
                CreateSlider(parent, "Comp. ratio", 1f, 6f, 3f, 1, "", v => Simulator()?.SetCompressionRatio(v), -64f);
                CreateSlider(parent, "Outlet press.", 40f, 100f, 70f, 0, " bar", v => Simulator()?.SetReactorPressure(v), -99f);
                break;
            case "reactor":
                CreateSlider(parent, "Temp", 200f, 300f, 250f, 0, " C", v => Simulator()?.SetReactorTemperature(v), -58f);
                CreateSlider(parent, "Pressure", 40f, 100f, 70f, 0, " bar", v => Simulator()?.SetReactorPressure(v), -91f);
                CreateSlider(parent, "H2/CO2", 1f, 6f, 3f, 1, "", v => Simulator()?.SetH2Co2Ratio(v), -124f);
                CreateSlider(parent, "GHSV", 1000f, 20000f, 8000f, 0, " h-1", v => Simulator()?.SetGHSV(v), -157f);
                CreateSlider(parent, "Feed flow", 20f, 130f, 100f, 0, "%", v => Simulator()?.SetReactorFeedFlow(v), -190f);
                break;
            case "condenser":
                CreateSlider(parent, "Cooling flow", 0f, 100f, 70f, 0, "%", v => Simulator()?.SetCoolingWaterFlow(v), -64f);
                CreateSlider(parent, "Cooling temp", 5f, 45f, 24f, 0, " C", v => Simulator()?.SetCoolingWaterTemperature(v), -99f);
                break;
            case "separator":
                CreateSlider(parent, "Recycle ratio", 0f, 100f, 65f, 0, "%", v => Simulator()?.SetRecycleRatio(v), -64f);
                CreateSlider(parent, "Sep. temp", 20f, 65f, 34f, 0, " C", v => Simulator()?.SetSeparatorTemperature(v), -99f);
                break;
            case "distillation":
                CreateSlider(parent, "Reflux ratio", 0.5f, 5f, 2.2f, 1, "", v => Simulator()?.SetRefluxRatio(v), -64f);
                CreateSlider(parent, "Reboiler temp", 70f, 115f, 92f, 0, " C", v => Simulator()?.SetDistillationReboilerTemperature(v), -99f);
                break;
            case "storage":
                Button reset = CreateWideButton(parent, "Reset stored methanol", new Vector2(0f, 50f), new Vector2(235f, 30f));
                reset.onClick.AddListener(() => Simulator()?.ResetStoredMethanol());
                break;
        }
    }

    private void CreateSlider(Transform parent, string label, float min, float max, float value, int decimals, string unit, Action<float> onChanged, float y)
    {
        Text labelText = CreateText(label + " Label", parent, label, 12, TextAnchor.MiddleLeft, new Color(0.75f, 0.88f, 0.95f, 1f));
        labelText.rectTransform.anchorMin = new Vector2(0f, 1f);
        labelText.rectTransform.anchorMax = new Vector2(0f, 1f);
        labelText.rectTransform.pivot = new Vector2(0f, 0.5f);
        labelText.rectTransform.anchoredPosition = new Vector2(16f, y);
        labelText.rectTransform.sizeDelta = new Vector2(100f, 22f);

        GameObject sliderObject = new GameObject(label + " Slider");
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(0f, 1f);
        sliderRect.pivot = new Vector2(0f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(122f, y);
        sliderRect.sizeDelta = new Vector2(190f, 18f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        Image background = SliderImage("Background", sliderObject.transform, new Color(0.12f, 0.18f, 0.23f, 1f), Vector2.zero, Vector2.one);
        Image fill = SliderImage("Fill", sliderObject.transform, new Color(0.08f, 0.62f, 0.9f, 1f), new Vector2(0f, 0.22f), new Vector2(1f, 0.78f));
        Image handle = SliderImage("Handle", sliderObject.transform, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        handle.rectTransform.sizeDelta = new Vector2(14f, 22f);

        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        _ = background;

        Text valueText = CreateText(label + " Value", parent, Format(value, decimals, unit), 12, TextAnchor.MiddleRight, Color.white);
        valueText.rectTransform.anchorMin = new Vector2(0f, 1f);
        valueText.rectTransform.anchorMax = new Vector2(0f, 1f);
        valueText.rectTransform.pivot = new Vector2(0f, 0.5f);
        valueText.rectTransform.anchoredPosition = new Vector2(322f, y);
        valueText.rectTransform.sizeDelta = new Vector2(92f, 22f);

        slider.onValueChanged.AddListener(v =>
        {
            valueText.text = Format(v, decimals, unit);
            onChanged?.Invoke(v);
        });

        onChanged?.Invoke(value);
    }

    private string BuildLiveText(string id)
    {
        PlantProcessSimulator.ProcessSnapshot s = Simulator() != null ? Simulator().Current : default;
        switch (id)
        {
            case "electrolyzer":
                return $"Water feed: {s.waterFeedKgH:F0} kg/h\nH2 production: {s.h2InputKgH:F0} kg/h\nO2 byproduct: {s.oxygenByproductKgH:F0} kg/h\nHigher power/water increases bubble and H2 pipe flow.";
            case "absorber":
                return $"Flue gas CO2: {s.co2InputKgH:F0} kg/h\nCaptured CO2: {s.co2CapturedKgH:F0} kg/h\nCapture efficiency: {s.captureEfficiencyPercent:F0}%\nAmine flow controls how much CO2 enters the process.";
            case "desorber":
                return $"Steam flow: {s.regeneratorSteamPercent:F0}%\nRegeneration temp: {s.regeneratorTemperatureC:F0} C\nCO2 released to compressor: {s.co2CapturedKgH:F0} kg/h\nLow heat leaves solvent partially loaded.";
            case "compressor":
                return $"Compression ratio: {s.compressionRatio:F1}\nOutlet pressure: {s.reactorPressureBar:F0} bar\nSyngas to reactor: {s.syngasFeedKgH:F0} kg/h\nHigh pressure improves conversion but implies higher power.";
            case "reactor":
                return $"Reactor yield: {s.reactorYieldPercent:F1}%\nMethanol formed: {s.methanolProductionKgH:F0} kg/h\nTemp: {s.reactorTemperatureC:F0} C  Pressure: {s.reactorPressureBar:F0} bar\nRatio: {s.h2Co2Ratio:F1}  GHSV: {s.ghsv:F0} h-1";
            case "condenser":
                return $"Cooling water: {s.coolingWaterFlowPercent:F0}% at {s.coolingWaterTemperatureC:F0} C\nCondensation recovery: {s.condenserRecoveryPercent:F0}%\nRecovered liquid methanol: {s.methanolProductionKgH:F0} kg/h";
            case "separator":
                return $"Recycle ratio: {s.recycleRatioPercent:F0}%\nRecycle gas: {s.recycleGasKgH:F0} kg/h\nSeparator temp: {s.separatorTemperatureC:F0} C\nMore recycle raises overall conversion and compressor load.";
            case "distillation":
                return $"Reflux ratio: {s.refluxRatio:F1}\nReboiler temp: {s.distillationReboilerTemperatureC:F0} C\nMethanol purity: {s.methanolPurityPercent:F2}%\nEnergy demand: {s.distillationEnergyPercent:F0}%";
            case "storage":
                return $"Stored methanol: {s.storedMethanolKg:F0} kg\nTank fill: {s.storageFillPercent:F0}%\nCurrent product rate: {s.methanolProductionKgH:F0} kg/h\nPurity: {s.methanolPurityPercent:F2}%";
            default:
                return $"H2: {s.h2InputKgH:F0} kg/h\nCO2: {s.co2CapturedKgH:F0} kg/h\nMeOH: {s.methanolProductionKgH:F0} kg/h";
        }
    }

    private PlantProcessSimulator Simulator()
    {
        return PlantProcessSimulator.Instance != null ? PlantProcessSimulator.Instance : FindFirstObjectByType<PlantProcessSimulator>();
    }

    private Transform FindTarget(string[] names)
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (string name in names)
        {
            foreach (Transform t in all)
            {
                if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
            }
        }

        foreach (string name in names)
        {
            string lower = name.ToLowerInvariant();
            foreach (Transform t in all)
            {
                if (t.name.ToLowerInvariant().Contains(lower)) return t;
            }
        }

        return null;
    }

    private Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Button CreateSmallButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject root = new GameObject("Close");
        root.transform.SetParent(parent, false);
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        root.AddComponent<Image>().color = new Color(0.85f, 0.18f, 0.18f, 0.9f);
        Button button = root.AddComponent<Button>();
        Text text = CreateText("Text", root.transform, label, 16, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private Button CreateWideButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject root = new GameObject(label);
        root.transform.SetParent(parent, false);
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        root.AddComponent<Image>().color = new Color(0.08f, 0.34f, 0.48f, 0.95f);
        Button button = root.AddComponent<Button>();
        Text text = CreateText("Text", root.transform, label, 13, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private Image SliderImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private string Format(float value, int decimals, string unit)
    {
        return decimals <= 0 ? $"{value:F0}{unit}" : $"{value:F1}{unit}";
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void DestroyObject(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private class ModuleAnchor : MonoBehaviour
    {
        public string ModuleId;
        public Transform Target;
        public Vector3 WorldOffset;
        public GameObject ButtonRoot;
        public RectTransform ButtonRect;
        public GameObject PanelRoot;
        public RectTransform PanelRect;
        public Text LiveText;
    }
}
