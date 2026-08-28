using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Interactive module UI layer for the complete plant.
/// It adds compact eye buttons near process modules. Each opened panel contains
/// module-specific controls, and those controls feed PlantProcessSimulator so
/// flow visuals and production/storage values respond in real time.
///
/// Info buttons appear on hover over modules and disappear when the mouse leaves.
/// </summary>
[DisallowMultipleComponent]
public class InteractiveModulePanelRuntime : MonoBehaviour
{
    private const string RuntimeRootName = "Generated Interactive Module Panels";

    [SerializeField] private Vector2 buttonSize = new Vector2(28f, 28f);
    [SerializeField] private Vector2 panelSize = new Vector2(390f, 300f);
    [SerializeField] private Vector2 panelOffset = new Vector2(230f, -150f);

    private Canvas canvas;
    private Camera mainCamera;
    private Font font;
    private List<ModuleAnchor> allAnchors = new List<ModuleAnchor>();
    private readonly List<RegisteredSlider> registeredSliders = new List<RegisteredSlider>();

    private struct RegisteredSlider
    {
        public Slider Slider;
        public float DefaultValue;
        public Text ValueText;
        public Text LabelText;
        public int Decimals;
        public string Unit;
        public SliderCommitTracker Tracker;
    }

    // Exact module title + slider labels the OFAT timeline uses to lock the reactor to a
    // single varying parameter. Must match AddModule / CreateControls below.
    private const string ReactorModuleTitle = "Methanol Reactor";

    /// <summary>
    /// Fires PlantProcessSimulator.CommitManualChange exactly once per interaction — on
    /// pointer-up, comparing against the value the slider held before that interaction —
    /// instead of on every intermediate onValueChanged tick while dragging. This is what
    /// lets graphs show a single "changed from A to B" point per user action instead of a
    /// flood of points for one drag gesture.
    /// </summary>
    private sealed class SliderCommitTracker : MonoBehaviour, IPointerUpHandler
    {
        public Slider Slider;
        public string Module;
        public string Parameter;
        public float LastCommittedValue;

        public void OnPointerUp(PointerEventData eventData)
        {
            float current = Slider.value;
            if (Mathf.Approximately(current, LastCommittedValue)) return;
            PlantProcessSimulator.Instance?.CommitManualChange(Module, Parameter, LastCommittedValue, current);
            LastCommittedValue = current;
        }
    }
    private GraphicRaycaster graphicRaycaster;
    // The plant meshes ship without colliders, so hover cannot rely on Physics.Raycast out of
    // the box. We add MeshColliders to each module's renderers at runtime purely for hover
    // picking (not physics simulation) and map them back to their owning anchor here.
    private readonly Dictionary<Collider, ModuleAnchor> colliderToAnchor = new Dictionary<Collider, ModuleAnchor>();

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
        canvas.gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (mainCamera == null || canvas == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mousePos = mouse.position.ReadValue();

        // Exact geometric test against the analytics window's live rect (it's draggable) —
        // only the pixels it actually currently covers are excluded from hover; everywhere
        // else on screen stays interactive even while the window is open.
        if (IcodosDashboardRuntime.Instance != null && IcodosDashboardRuntime.Instance.IsPointerOverAnalyticsWindow(mousePos))
        {
            foreach (ModuleAnchor blockedAnchor in allAnchors)
            {
                if (blockedAnchor != null && blockedAnchor.ButtonRoot != null) blockedAnchor.ButtonRoot.SetActive(false);
            }
            return;
        }

        // Hover is decided by raycasting the cursor against the runtime-added MeshColliders
        // (see RegisterColliders) — this hits the module's actual geometry directly, so it
        // works correctly at any zoom level and for any module shape, unlike a screen-space
        // distance heuristic against a single anchor point.
        ModuleAnchor closestAnchor = null;
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, 5000f))
        {
            colliderToAnchor.TryGetValue(hit.collider, out closestAnchor);
        }

        foreach (ModuleAnchor anchor in allAnchors)
        {
            if (anchor == null || anchor.Target == null) continue;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(anchor.Target.position + anchor.WorldOffset);
            anchor.CachedScreen = screenPos;
            anchor.IsInFrustum = screenPos.z > 0f;
        }

        foreach (ModuleAnchor anchor in allAnchors)
        {
            if (anchor == null || anchor.Target == null) continue;

            // The 3D raycast alone loses hover the instant the cursor crosses in front of
            // something else (e.g. a pipe passing between the camera and the H2 tank / heat
            // exchanger), hiding the button right as the user moves toward it to click. Also
            // treat the cursor being over the button's own screen rect as hovered so it stays
            // up long enough to be clicked.
            bool isHovered = anchor == closestAnchor || IsNearButton(anchor.ButtonRect, mousePos);
            anchor.ButtonRoot.SetActive(anchor.IsInFrustum && isHovered);

            if (!anchor.IsInFrustum)
            {
                anchor.PanelRoot.SetActive(false);
                continue;
            }

            Vector3 screen = anchor.CachedScreen;
            anchor.ButtonRect.position = screen;
            Vector3 unclampedPanelPosition = screen + new Vector3(panelOffset.x, panelOffset.y, 0f);
            float halfWidth = panelSize.x * 0.5f;
            float halfHeight = panelSize.y * 0.5f;
            anchor.PanelRect.position = new Vector3(
                Mathf.Clamp(unclampedPanelPosition.x, halfWidth + 8f, Screen.width - halfWidth - 8f),
                Mathf.Clamp(unclampedPanelPosition.y, halfHeight + 8f, Screen.height - halfHeight - 8f),
                0f);

            if (anchor.PanelRoot.activeSelf && anchor.LiveText != null)
            {
                anchor.LiveText.text = BuildLiveText(anchor.ModuleId);
            }
        }
    }

    // Padded hit test around a button's screen rect. The plain RectangleContainsScreenPoint
    // check has zero margin, so a cursor moving fast toward a small (28px) button can land
    // a pixel outside it on the frame the mesh raycast has already lost the module (e.g. a
    // pipe occluding the H2 tank / heat exchanger from that exact angle), hiding the button
    // just before the click registers. A few pixels of padding gives that transition slack.
    private const float ButtonHoverPadding = 10f;

    private static bool IsNearButton(RectTransform buttonRect, Vector2 screenPoint)
    {
        if (buttonRect == null) return false;
        Vector3 center = buttonRect.position;
        Vector2 size = buttonRect.rect.size;
        float halfW = size.x * 0.5f + ButtonHoverPadding;
        float halfH = size.y * 0.5f + ButtonHoverPadding;
        return Mathf.Abs(screenPoint.x - center.x) <= halfW && Mathf.Abs(screenPoint.y - center.y) <= halfH;
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
        // HX_Shell lives inside the "Reactor base model" hierarchy (feed/effluent heat-exchanger
        // sub-assembly) — registered after "reactor" so RegisterColliders below reassigns its
        // specific mesh collider to this dedicated anchor, overriding the reactor's broader claim.
        AddModule("heatexchanger", "Feed/Effluent Heat Exchanger", new[] { "HX_Shell" }, new Vector3(0f, 1.4f, 0f));
        AddModule("condenser", "Condenser", new[] { "Condenser", "condenser" }, new Vector3(0f, 3.5f, 0f));
        AddModule("separator", "Separator + Recycle", new[] { "flash separator", "Separator" }, new Vector3(0f, 4.0f, 0f));
        AddModule("distillation", "Distillation Column", new[] { "distillation column", "Distillation" }, new Vector3(0f, 5.8f, 0f));
        AddModule("storage", "Methanol Storage", new[] { "methanol tank", "Methanol Storage" }, new Vector3(0f, 3.2f, 0f));
        AddModule("co2tank", "CO2 Buffer Tank", new[] { "co2 tank" }, new Vector3(0f, 2.6f, 0f));
        AddModule("h2tank", "H2 Buffer Tank", new[] { "h2 tank" }, new Vector3(0f, 2.6f, 0f));
    }

    [ContextMenu("Clear Interactive Module Panels")]
    public void Clear()
    {
        allAnchors.Clear();
        registeredSliders.Clear();
        if (canvas != null)
        {
            DestroyObject(canvas.gameObject);
            canvas = null;
        }
    }

    public void SetSelectionVisible(bool visible)
    {
        // Buttons now appear on hover, so this method is kept for compatibility but has no effect
        // The canvas remains active to allow hover detection
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Interactive Module Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 75;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1536f, 1024f);
        scaler.matchWidthOrHeight = 0.5f;
        graphicRaycaster = canvasObject.AddComponent<GraphicRaycaster>();
    }

    private void RemoveOldStaticEyeUi()
    {
        GameObject oldUi = GameObject.Find("Generated Module Eye UI");
        if (oldUi != null)
        {
            Destroy(oldUi);
        }

        Button[] legacyButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button legacyButton in legacyButtons)
        {
            if (legacyButton.name.EndsWith("InfoButton", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(legacyButton.name, "GearButton", StringComparison.OrdinalIgnoreCase))
            {
                legacyButton.gameObject.SetActive(false);
            }
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
        RegisterColliders(target, anchor);

        anchor.ButtonRoot = CreateEyeButton(anchorObject.transform, title, out anchor.ButtonRect, out Button eyeButton);
        anchor.PanelRoot = CreatePanel(anchorObject.transform, title, id, out anchor.PanelRect, out anchor.LiveText);
        anchor.PanelRoot.SetActive(false);

        eyeButton.onClick.AddListener(() => anchor.PanelRoot.SetActive(!anchor.PanelRoot.activeSelf));

        // Register anchor for hover detection
        allAnchors.Add(anchor);
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

        Text eye = CreateText("Eye Icon", root.transform, "i", 16, TextAnchor.MiddleCenter, Color.white);
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

        liveText = CreateText("Live Values", root.transform, "", 12, TextAnchor.UpperLeft, Color.white);
        liveText.rectTransform.anchorMin = new Vector2(0f, 1f);
        liveText.rectTransform.anchorMax = new Vector2(1f, 1f);
        liveText.rectTransform.offsetMin = new Vector2(14f, -118f);
        liveText.rectTransform.offsetMax = new Vector2(-14f, -46f);

        CreateControls(root.transform, id, title);
        return root;
    }

    private void CreateControls(Transform parent, string id, string moduleTitle)
    {
        switch (id)
        {
            case "electrolyzer":
                CreateSlider(parent, "Plant load", 0f, 100f, 100f, 0, "%", v => Simulator()?.SetTimelinePercent(v), -136f, moduleTitle);
                CreateSlider(parent, "Power", 0f, 100f, 75f, 0, "%", v => Simulator()?.SetElectrolyzerPower(v), -168f, moduleTitle);
                CreateSlider(parent, "Water feed", 0f, 130f, 100f, 0, "%", v => Simulator()?.SetWaterFeed(v), -200f, moduleTitle);
                break;
            case "absorber":
                CreateSlider(parent, "Amine flow", 10f, 100f, 65f, 0, "%", v => Simulator()?.SetAmineFlow(v), -136f, moduleTitle);
                CreateSlider(parent, "Flue gas", 0f, 130f, 100f, 0, "%", v => Simulator()?.SetFlueGasFlow(v), -168f, moduleTitle);
                break;
            case "desorber":
                CreateSlider(parent, "Steam flow", 0f, 100f, 70f, 0, "%", v => Simulator()?.SetRegeneratorSteam(v), -136f, moduleTitle);
                CreateSlider(parent, "Regen temp", 80f, 130f, 105f, 0, " C", v => Simulator()?.SetRegeneratorTemperature(v), -168f, moduleTitle);
                break;
            case "compressor":
                CreateSlider(parent, "Comp. ratio", 1f, 6f, 3f, 1, "", v => Simulator()?.SetCompressionRatio(v), -136f, moduleTitle);
                CreateSlider(parent, "Outlet press.", 40f, 100f, 70f, 0, " bar", v => Simulator()?.SetReactorPressure(v), -168f, moduleTitle);
                break;
            case "reactor":
                CreateSlider(parent, "Temp", 180f, 300f, 250f, 0, " C", v => Simulator()?.SetReactorTemperature(v), -126f, moduleTitle);
                CreateSlider(parent, "Pressure", 40f, 100f, 70f, 0, " bar", v => Simulator()?.SetReactorPressure(v), -158f, moduleTitle);
                CreateSlider(parent, "H2/CO2", 1f, 6f, 3f, 1, "", v => Simulator()?.SetH2Co2Ratio(v), -190f, moduleTitle);
                CreateSlider(parent, "GHSV", 1000f, 20000f, 8000f, 0, " h-1", v => Simulator()?.SetGHSV(v), -222f, moduleTitle);
                CreateSlider(parent, "Feed flow", 20f, 130f, 100f, 0, "%", v => Simulator()?.SetReactorFeedFlow(v), -254f, moduleTitle);
                break;
            case "condenser":
                CreateSlider(parent, "Cooling flow", 0f, 100f, 70f, 0, "%", v => Simulator()?.SetCoolingWaterFlow(v), -136f, moduleTitle);
                CreateSlider(parent, "Cooling temp", 5f, 45f, 24f, 0, " C", v => Simulator()?.SetCoolingWaterTemperature(v), -168f, moduleTitle);
                break;
            case "separator":
                CreateSlider(parent, "Recycle ratio", 0f, 100f, 65f, 0, "%", v => Simulator()?.SetRecycleRatio(v), -136f, moduleTitle);
                CreateSlider(parent, "Sep. temp", 20f, 65f, 34f, 0, " C", v => Simulator()?.SetSeparatorTemperature(v), -168f, moduleTitle);
                break;
            case "distillation":
                CreateSlider(parent, "Reflux ratio", 0.5f, 5f, 3.2f, 1, "", v => Simulator()?.SetRefluxRatio(v), -136f, moduleTitle);
                CreateSlider(parent, "Reboiler temp", 70f, 115f, 98f, 0, " C", v => Simulator()?.SetDistillationReboilerTemperature(v), -168f, moduleTitle);
                break;
            case "storage":
                Button reset = CreateWideButton(parent, "Reset stored methanol", new Vector2(0f, 50f), new Vector2(235f, 30f));
                reset.onClick.AddListener(() => Simulator()?.ResetStoredMethanol());
                break;
        }
    }

    private void CreateSlider(Transform parent, string label, float min, float max, float value, int decimals, string unit, Action<float> onChanged, float y, string moduleTitle)
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
        sliderRect.anchoredPosition = new Vector2(118f, y);
        sliderRect.sizeDelta = new Vector2(172f, 18f);

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
        valueText.rectTransform.anchoredPosition = new Vector2(296f, y);
        valueText.rectTransform.sizeDelta = new Vector2(78f, 22f);

        slider.onValueChanged.AddListener(v =>
        {
            valueText.text = Format(v, decimals, unit);
            onChanged?.Invoke(v);
        });

        onChanged?.Invoke(value);

        SliderCommitTracker tracker = sliderObject.AddComponent<SliderCommitTracker>();
        tracker.Slider = slider;
        tracker.Module = moduleTitle;
        tracker.Parameter = label;
        tracker.LastCommittedValue = value;

        registeredSliders.Add(new RegisteredSlider { Slider = slider, DefaultValue = value, ValueText = valueText, LabelText = labelText, Decimals = decimals, Unit = unit, Tracker = tracker });
    }

    /// <summary>
    /// Called by the OFAT timeline. While one reactor parameter is the active swept variable
    /// its slider stays usable and every other reactor slider is locked (non-interactive and
    /// dimmed) so the plant holds those constant. Pass null to release the lock.
    /// </summary>
    public void SetReactorVariableLock(string activeParameter)
    {
        foreach (RegisteredSlider entry in registeredSliders)
        {
            if (entry.Slider == null || entry.Tracker == null) continue;
            if (!string.Equals(entry.Tracker.Module, ReactorModuleTitle, StringComparison.OrdinalIgnoreCase)) continue;

            bool locked = activeParameter != null &&
                !string.Equals(entry.Tracker.Parameter, activeParameter, StringComparison.OrdinalIgnoreCase);
            entry.Slider.interactable = !locked;
            SetSliderDimmed(entry, locked);
        }
    }

    public void ClearReactorVariableLock() => SetReactorVariableLock(null);

    private static void SetSliderDimmed(RegisteredSlider entry, bool dim)
    {
        SetGraphicAlpha(entry.Slider.fillRect, dim ? 0.3f : 1f);
        SetGraphicAlpha(entry.Slider.handleRect, dim ? 0.35f : 1f);
        if (entry.ValueText != null) SetTextAlpha(entry.ValueText, dim ? 0.4f : 1f);
        if (entry.LabelText != null) SetTextAlpha(entry.LabelText, dim ? 0.4f : 0.95f);
    }

    private static void SetGraphicAlpha(RectTransform target, float alpha)
    {
        if (target == null) return;
        Image image = target.GetComponent<Image>();
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

    private static void SetTextAlpha(Text text, float alpha)
    {
        Color c = text.color;
        c.a = alpha;
        text.color = c;
    }

    /// <summary>
    /// Drives every module slider to the setpoint that maximises overall plant efficiency in
    /// the educational model: reactor at the yield peak (255 C), full pressure and recycle,
    /// stoichiometric feed ratio, minimum GHSV (max residence), and the whole separation
    /// train at maximum recovery. Moving the sliders re-runs the normal
    /// onValueChanged -> Simulator().SetXxx cascade, so KPIs / visuals follow.
    /// </summary>
    public void ApplyMaximumEfficiencyPreset()
    {
        ClearReactorVariableLock();

        SetSliderValue("Electrolyzer", "Plant load", 100f);
        SetSliderValue("Electrolyzer", "Power", 100f);
        SetSliderValue("Electrolyzer", "Water feed", 100f);
        SetSliderValue("CO2 Absorber", "Amine flow", 100f);
        SetSliderValue("CO2 Absorber", "Flue gas", 100f);
        SetSliderValue("Desorber / Regenerator", "Steam flow", 100f);
        SetSliderValue("Desorber / Regenerator", "Regen temp", 122f);
        SetSliderValue("Compressor", "Comp. ratio", 3f);
        SetSliderValue("Compressor", "Outlet press.", 100f);
        SetSliderValue("Methanol Reactor", "Temp", 255f);
        SetSliderValue("Methanol Reactor", "Pressure", 100f);
        SetSliderValue("Methanol Reactor", "H2/CO2", 3f);
        SetSliderValue("Methanol Reactor", "GHSV", 1000f);
        SetSliderValue("Methanol Reactor", "Feed flow", 100f);
        SetSliderValue("Condenser", "Cooling flow", 100f);
        SetSliderValue("Condenser", "Cooling temp", 5f);
        SetSliderValue("Separator + Recycle", "Recycle ratio", 100f);
        SetSliderValue("Separator + Recycle", "Sep. temp", 34f);
        SetSliderValue("Distillation Column", "Reflux ratio", 5f);
        SetSliderValue("Distillation Column", "Reboiler temp", 115f);
    }

    private void SetSliderValue(string module, string parameter, float value)
    {
        foreach (RegisteredSlider entry in registeredSliders)
        {
            if (entry.Slider == null || entry.Tracker == null) continue;
            if (!string.Equals(entry.Tracker.Module, module, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(entry.Tracker.Parameter, parameter, StringComparison.OrdinalIgnoreCase)) continue;
            float clamped = Mathf.Clamp(value, entry.Slider.minValue, entry.Slider.maxValue);
            entry.Slider.value = clamped;
            entry.Tracker.LastCommittedValue = clamped;
            return;
        }
    }

    /// <summary>
    /// Repositions every control back to its start-of-run default without re-triggering the
    /// onValueChanged -> Simulator().SetXxx cascade — PlantProcessSimulator.ResetSimulation()
    /// has already applied the equivalent data reset, so this only needs to catch the visual
    /// slider handles/labels up to match. Also closes any open module control panels so the
    /// scene reads as freshly started.
    /// </summary>
    public void ResetControlVisuals()
    {
        ClearReactorVariableLock();
        foreach (RegisteredSlider entry in registeredSliders)
        {
            if (entry.Slider == null) continue;
            entry.Slider.SetValueWithoutNotify(entry.DefaultValue);
            if (entry.ValueText != null) entry.ValueText.text = Format(entry.DefaultValue, entry.Decimals, entry.Unit);
            if (entry.Tracker != null) entry.Tracker.LastCommittedValue = entry.DefaultValue;
        }

        foreach (ModuleAnchor anchor in allAnchors)
        {
            if (anchor != null && anchor.PanelRoot != null) anchor.PanelRoot.SetActive(false);
        }
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
                return $"Status: {BuildReactorStatus(s)}\nYield: {s.reactorYieldPercent:F1}%  Methanol: {s.methanolProductionKgH:F0} kg/h\nTemp: {s.reactorTemperatureC:F0} C  Pressure: {s.reactorPressureBar:F0} bar\nRatio: {s.h2Co2Ratio:F1}  GHSV: {s.ghsv:F0} h-1";
            case "condenser":
                return $"Cooling water: {s.coolingWaterFlowPercent:F0}% at {s.coolingWaterTemperatureC:F0} C\nCondensation recovery: {s.condenserRecoveryPercent:F0}%\nRecovered liquid methanol: {s.methanolProductionKgH:F0} kg/h";
            case "heatexchanger":
                return $"Syngas feed: {s.syngasFeedKgH:F0} kg/h\nReactor effluent temp: {s.reactorTemperatureC:F0} C\nPreheats cold reactor feed against hot reactor effluent before the fixed bed, reducing external heating duty.";
            case "separator":
                return $"Recycle ratio: {s.recycleRatioPercent:F0}%\nRecycle gas: {s.recycleGasKgH:F0} kg/h\nSeparator temp: {s.separatorTemperatureC:F0} C\nMore recycle raises overall conversion and compressor load.";
            case "distillation":
                return $"Reflux ratio: {s.refluxRatio:F1}\nReboiler temp: {s.distillationReboilerTemperatureC:F0} C\nMethanol purity: {s.methanolPurityPercent:F2}%\nEnergy demand: {s.distillationEnergyPercent:F0}%";
            case "storage":
                return $"Stored methanol: {s.storedMethanolKg:F0} kg\nTank fill: {s.storageFillPercent:F0}%\nCurrent product rate: {s.methanolProductionKgH:F0} kg/h\nPurity: {s.methanolPurityPercent:F2}%";
            case "co2tank":
                return $"Captured CO2 inflow: {s.co2CapturedKgH:F0} kg/h\nCapture efficiency: {s.captureEfficiencyPercent:F0}%\nBuffers conditioned CO2 ahead of the synthesis gas mixing junction.";
            case "h2tank":
                return $"Hydrogen inflow: {s.h2InputKgH:F0} kg/h\nElectrolyzer power: {s.electrolyzerPowerPercent:F0}%\nDecouples variable electrolyzer output from steady synthesis-loop demand.";
            default:
                return $"H2: {s.h2InputKgH:F0} kg/h\nCO2: {s.co2CapturedKgH:F0} kg/h\nMeOH: {s.methanolProductionKgH:F0} kg/h";
        }
    }

    private PlantProcessSimulator Simulator()
    {
        return PlantProcessSimulator.Instance != null ? PlantProcessSimulator.Instance : FindFirstObjectByType<PlantProcessSimulator>();
    }

    private string BuildReactorStatus(PlantProcessSimulator.ProcessSnapshot s)
    {
        if (s.reactorTemperatureC > 270f) return "Catalyst sintering risk";
        if (s.reactorPressureBar < 60f && s.plantLoadPercent > 5f) return "Low conversion pressure";
        if (s.h2Co2Ratio < 2.5f) return "Hydrogen deficiency";
        if (s.ghsv > 9500f) return "Low residence time";
        return "Normal";
    }

    /// <summary>
    /// Adds a MeshCollider (hover picking only, not physics) to every renderer under target
    /// that doesn't already have one, and records which anchor each collider belongs to so
    /// LateUpdate's raycast can resolve "which module is the cursor over" directly against
    /// the actual mesh geometry.
    /// </summary>
    private void RegisterColliders(Transform target, ModuleAnchor anchor)
    {
        MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null) continue;

            GameObject go = meshFilter.gameObject;
            Collider existing = go.GetComponent<Collider>();
            if (existing is MeshCollider existingMeshCollider)
            {
                colliderToAnchor[existingMeshCollider] = anchor;
                continue;
            }
            if (existing != null) continue; // some other collider type already present; leave it alone

            MeshCollider meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            colliderToAnchor[meshCollider] = anchor;
        }
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
        public Vector3 CachedScreen;
        public bool IsInFrustum;
    }
}
