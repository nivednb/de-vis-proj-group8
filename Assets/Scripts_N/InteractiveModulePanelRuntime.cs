using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Icon = UITheme.Icon;
using Kind = UITheme.ButtonKind;
using W = UITheme.Weight;

/// <summary>
/// Interactive module UI layer for the complete plant.
///
/// Hovering a piece of equipment shows a small labelled pill on it; clicking that pill opens
/// the module's control drawer on the right-hand side of the screen (live read-outs plus the
/// module's operating sliders). Those controls feed PlantProcessSimulator so flow visuals and
/// production/storage values respond in real time. Only one drawer is open at a time, and it
/// stays put while the camera moves.
/// </summary>
[DisallowMultipleComponent]
public class InteractiveModulePanelRuntime : MonoBehaviour
{
    private const string RuntimeRootName = "Generated Interactive Module Panels";
    private const float DrawerWidth = 388f;
    private const float DrawerInner = DrawerWidth - 40f;
    private const float DrawerTop = 92f;
    // Clears the bottom module-card strip (IcodosDashboardRuntime.BuildKpiStrip: 92 + 104) plus a gap.
    private const float DrawerBottom = 208f;
    private const float DrawerHeaderHeight = 76f;
    private const float DrawerFooterHeight = 85f;

    private Canvas canvas;
    private Camera mainCamera;
    private ReactorReactionCard reactionCard;
    private ModuleAnchor reactorAnchor;
    private List<ModuleAnchor> allAnchors = new List<ModuleAnchor>();
    private readonly List<RegisteredSlider> registeredSliders = new List<RegisteredSlider>();

    private struct RegisteredSlider
    {
        public Slider Slider;
        public float DefaultValue;
        public Text ValueText;
        public Image ValueChip;
        public Text LabelText;
        public int Decimals;
        public string Unit;
        public SliderCommitTracker Tracker;
    }

    private struct Readout
    {
        public string Label;
        public string Unit;
        public Func<PlantProcessSimulator.ProcessSnapshot, string> Value;
        public Readout(string label, string unit, Func<PlantProcessSimulator.ProcessSnapshot, string> value)
        {
            Label = label;
            Unit = unit;
            Value = value;
        }
    }

    private sealed class ModuleInfo
    {
        public string Subtitle;
        public Icon Icon;
        public Color Color;
        public string Description;
        public Readout[] Readouts;
        public int FocusIndex;
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
        Build();
        canvas.gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (mainCamera == null || canvas == null) return;

        foreach (ModuleAnchor anchor in allAnchors)
        {
            if (anchor != null && anchor.PanelRoot != null && anchor.PanelRoot.activeSelf) UpdateReadouts(anchor);
        }

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
            reactionCard?.Track(null);
            return;
        }

        // Hover is decided by raycasting the cursor against the runtime-added MeshColliders
        // (see RegisterColliders) — this hits the module's actual geometry directly, so it
        // works correctly at any zoom level and for any module shape, unlike a screen-space
        // distance heuristic against a single anchor point.
        ModuleAnchor closestAnchor = null;
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        // Pipe probe colliders (added by MassFlowProbeRuntime on their own layer) are excluded:
        // a pipe crossing in front of a module must not swallow that module's hover.
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!overUi && Physics.Raycast(ray, out RaycastHit hit, 5000f, ~MassFlowProbeRuntime.PipeLayerMask))
        {
            colliderToAnchor.TryGetValue(hit.collider, out closestAnchor);
        }

        foreach (ModuleAnchor anchor in allAnchors)
        {
            if (anchor == null || anchor.Target == null) continue;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(anchor.Target.position + anchor.WorldOffset);
            anchor.CachedScreen = screenPos;
            anchor.IsInFrustum = screenPos.z > 0f;

            // The 3D raycast alone loses hover the instant the cursor crosses in front of
            // something else (e.g. a pipe passing between the camera and the H2 tank / heat
            // exchanger), hiding the button right as the user moves toward it to click. Also
            // treat the cursor being over the button's own screen rect as hovered so it stays
            // up long enough to be clicked.
            bool isHovered = anchor == closestAnchor || (anchor.ButtonRoot.activeSelf && IsNearButton(anchor.ButtonRect, mousePos));
            anchor.ButtonRoot.SetActive(anchor.IsInFrustum && isHovered);
            if (anchor.IsInFrustum) anchor.ButtonRect.position = new Vector3(screenPos.x, screenPos.y, 0f);
        }

        // Hovering the reactor also explains the chemistry happening inside it.
        bool reactorHovered = reactorAnchor != null && reactorAnchor.ButtonRoot.activeSelf;
        reactionCard?.Track(reactorHovered ? reactorAnchor.ButtonRect : null);
    }

    // Padded hit test around a button's screen rect, so a cursor moving toward the pill does
    // not lose it on the frame the mesh raycast has already lost the module (e.g. a pipe
    // occluding the H2 tank / heat exchanger from that exact angle).
    private const float ButtonHoverPadding = 10f;

    private static readonly Vector3[] corners = new Vector3[4];

    private static bool IsNearButton(RectTransform buttonRect, Vector2 screenPoint)
    {
        if (buttonRect == null) return false;
        buttonRect.GetWorldCorners(corners);
        return screenPoint.x >= corners[0].x - ButtonHoverPadding && screenPoint.x <= corners[2].x + ButtonHoverPadding &&
               screenPoint.y >= corners[0].y - ButtonHoverPadding && screenPoint.y <= corners[2].y + ButtonHoverPadding;
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

        reactorAnchor = allAnchors.Find(a => a != null && a.ModuleId == "reactor");
        reactionCard = reactorAnchor != null ? ReactorReactionCard.Create(canvas, Info("reactor").Color) : null;
    }

    [ContextMenu("Clear Interactive Module Panels")]
    public void Clear()
    {
        allAnchors.Clear();
        registeredSliders.Clear();
        reactorAnchor = null;
        reactionCard = null;
        if (canvas != null)
        {
            DestroyObject(canvas.gameObject);
            canvas = null;
        }
    }

    public void SetSelectionVisible(bool visible)
    {
        // Buttons appear on hover, so this method is kept for compatibility but has no effect.
    }

    /// <summary>True while any module's control drawer is open.</summary>
    public bool AnyPanelOpen
    {
        get
        {
            foreach (ModuleAnchor a in allAnchors) if (a != null && a.PanelRoot != null && a.PanelRoot.activeSelf) return true;
            return false;
        }
    }

    /// <summary>Opens one module's control drawer (closing any other), optionally flying the
    /// camera to that module.</summary>
    public void OpenModule(string id, bool focusCamera)
    {
        foreach (ModuleAnchor a in allAnchors)
        {
            if (a == null || a.PanelRoot == null) continue;
            bool open = a.ModuleId == id;
            a.PanelRoot.SetActive(open);
            if (open)
            {
                UpdateReadouts(a);
                if (focusCamera) FocusCamera(a.ModuleId);
            }
        }
    }

    public void CloseAllPanels()
    {
        foreach (ModuleAnchor a in allAnchors)
            if (a != null && a.PanelRoot != null) a.PanelRoot.SetActive(false);
    }

    private void TogglePanel(ModuleAnchor anchor)
    {
        if (anchor.PanelRoot.activeSelf) anchor.PanelRoot.SetActive(false);
        else OpenModule(anchor.ModuleId, false);
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Interactive Module Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the dashboard (90) so an open drawer covers the right-hand cards; below the
        // about box (200) and the tour (300).
        canvas.sortingOrder = 95;
        UITheme.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());
        canvasObject.AddComponent<GraphicRaycaster>();
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

        GameObject anchorObject = new GameObject(title + " Interactive Anchor", typeof(RectTransform));
        anchorObject.transform.SetParent(canvas.transform, false);
        UITheme.Fill((RectTransform)anchorObject.transform);
        ModuleAnchor anchor = anchorObject.AddComponent<ModuleAnchor>();
        anchor.ModuleId = id;
        anchor.Title = title;
        anchor.Target = target;
        anchor.WorldOffset = offset;
        RegisterColliders(target, anchor);

        ModuleInfo info = Info(id);
        anchor.Info = info;
        anchor.ButtonRoot = CreateHoverPill(anchorObject.transform, title, info, out anchor.ButtonRect, out Button pillButton);
        anchor.PanelRoot = CreateDrawer(anchorObject.transform, title, id, info, anchor);
        anchor.PanelRoot.SetActive(false);
        anchor.ButtonRoot.SetActive(false);

        pillButton.onClick.AddListener(() => TogglePanel(anchor));

        // Register anchor for hover detection
        allAnchors.Add(anchor);
    }

    // ---- hover pill ------------------------------------------------------------

    private GameObject CreateHoverPill(Transform parent, string title, ModuleInfo info, out RectTransform rect, out Button button)
    {
        RectTransform card = UITheme.Card(title + " Eye Button", parent, 17f, UITheme.Glass, 18f, 6f, 0.22f);
        card.anchorMin = card.anchorMax = Vector2.zero;
        card.pivot = new Vector2(0.5f, 0.5f);

        button = UITheme.MakeButton("Button", card, null, Kind.Ghost, 13f, null, 17f, false, 16f, W.Bold, 0f);
        UITheme.Fill((RectTransform)button.transform);
        HorizontalLayoutGroup layout = button.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 12, 0, 0);
        layout.spacing = 8f;

        Image iconBg = UITheme.Panel("Info", button.transform, UITheme.Accent, 12f);
        iconBg.rectTransform.sizeDelta = new Vector2(24f, 24f);
        LayoutElement le = iconBg.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 24f;
        Image i = UITheme.IconImage("Icon", iconBg.transform, Icon.Info, 16f, Color.white);
        UITheme.Center(i.rectTransform, 16f, 16f);

        Text label = UITheme.Label("Label", button.transform, UITheme.Pretty(title), 13f, W.ExtraBold, UITheme.Ink, TextAnchor.MiddleLeft);
        label.rectTransform.sizeDelta = new Vector2(0f, 20f);
        Image chevron = UITheme.IconImage("Chevron", button.transform, Icon.ChevronRight, 14f, UITheme.Subtle);
        LayoutElement ce = chevron.gameObject.AddComponent<LayoutElement>();
        ce.preferredWidth = 14f;

        float width = UITheme.PreferredWidth(button);
        card.sizeDelta = new Vector2(width, 34f);
        rect = card;
        return card.gameObject;
    }

    // ---- drawer ------------------------------------------------------------------

    private GameObject CreateDrawer(Transform parent, string title, string id, ModuleInfo info, ModuleAnchor anchor)
    {
        RectTransform drawer = UITheme.Card(title + " Interactive Panel", parent, 18f, Color.white, 48f, 20f, 0.28f);

        Image badge = UITheme.Badge("Badge", drawer, info.Icon, info.Color, 42f, 12f, 22f);
        UITheme.TopLeft(badge.rectTransform, 20f, 18f, 42f, 42f);
        Text titleText = UITheme.Label("Title", drawer, UITheme.Pretty(title), 17f, W.ExtraBold, UITheme.Ink);
        UITheme.TopLeft(titleText.rectTransform, 74f, 18f, 250f, 22f);
        Text subtitle = UITheme.Label("Subtitle", drawer, info.Subtitle, 12.5f, W.SemiBold, UITheme.Subtle);
        UITheme.TopLeft(subtitle.rectTransform, 74f, 40f, 250f, 18f);
        if (id == "reactor")
        {
            anchor.StatusChip = UITheme.StatusChip("Status", drawer, 22f, 11.5f);
            RectTransform chipRect = (RectTransform)anchor.StatusChip.transform;
            chipRect.anchorMin = chipRect.anchorMax = chipRect.pivot = new Vector2(0f, 1f);
            chipRect.anchoredPosition = new Vector2(74f + subtitle.preferredWidth + 8f, -38f);
            anchor.StatusChip.Set(UIStatusChip.Kind.Success, "Normal");
        }

        Button close = UITheme.IconButton("Close", drawer, Icon.Close, Kind.Secondary, 36f, 16f);
        UITheme.TopRight((RectTransform)close.transform, 18f, 21f, 36f, 36f);
        GameObject root = drawer.gameObject;
        close.onClick.AddListener(() => root.SetActive(false));

        // Everything between the header and the footer buttons scrolls, so the sheet can stop
        // above the bottom module cards on any screen height.
        RectTransform body = CreateScrollBody(drawer);

        float y = 0f;
        // Read-out tiles
        int n = info.Readouts.Length;
        anchor.ReadoutValues = new UIValueText[n];
        if (n > 0)
        {
            float tileW = (DrawerInner - (n - 1) * 8f) / n;
            for (int r = 0; r < n; r++)
            {
                Image tile = UITheme.Panel(info.Readouts[r].Label, body, UITheme.Sunken2, 12f);
                UITheme.TopLeft(tile.rectTransform, 20f + r * (tileW + 8f), y, tileW, 58f);
                UITheme.Border(tile.rectTransform, UITheme.Line, 12f);
                Text cap = UITheme.Label("Caption", tile.transform, info.Readouts[r].Label, 11.5f, W.Bold, UITheme.Subtle);
                UITheme.TopBand(cap.rectTransform, 11f, 8f, 6f, 16f);
                UIValueText v = UITheme.ValueText("Value", tile.transform, n >= 4 ? 16f : 18f, 11f, UITheme.Ink, UITheme.Muted);
                UITheme.TopBand((RectTransform)v.transform, 11f, 26f, 6f, 24f);
                anchor.ReadoutValues[r] = v;
            }
            y += 58f + 14f;
        }

        // Description
        Text desc = UITheme.Label("Description", body, info.Description, 12.5f, W.Medium, UITheme.Muted, TextAnchor.UpperLeft, true);
        desc.lineSpacing = 1.1f;
        UITheme.TopLeft(desc.rectTransform, 20f, y, DrawerInner, 40f);
        float descH = Mathf.Ceil(desc.preferredHeight) + 2f;
        desc.rectTransform.sizeDelta = new Vector2(DrawerInner, descH);
        y += descH + 16f;

        // Sliders
        int before = registeredSliders.Count;
        float slidersTop = y + 34f;
        float afterSliders = CreateControls(body, id, title, slidersTop);
        bool hasSliders = registeredSliders.Count > before;
        if (hasSliders)
        {
            Text section = UITheme.Label("Section", body, "Operating conditions", 14f, W.ExtraBold, UITheme.Ink);
            UITheme.TopLeft(section.rectTransform, 20f, y, 200f, 20f);
            Text hint = UITheme.Label("Hint", body, "Each release adds a graph point", 12f, W.SemiBold, UITheme.Subtle, TextAnchor.MiddleRight);
            UITheme.TopRight(hint.rectTransform, 20f, y, 200f, 20f);
            y = afterSliders + 18f;
        }
        else if (id == "storage")
        {
            Button reset = UITheme.MakeButton("Reset stored methanol", body, "Reset stored methanol", Kind.Secondary, 14f, Icon.Reset, 10f);
            UITheme.TopLeft((RectTransform)reset.transform, 20f, y, DrawerInner, 42f);
            reset.onClick.AddListener(() => Simulator()?.ResetStoredMethanol());
            y += 42f + 14f;
        }

        // Footer actions, pinned to the bottom of the sheet.
        string moduleId = id;
        Image rule = UITheme.Panel("Footer Rule", drawer, UITheme.Line);
        UITheme.BottomBand(rule.rectTransform, 0f, 84f, 0f, 1f);
        if (hasSliders)
        {
            float bw = (DrawerInner - 10f) / 2f;
            Button focus = UITheme.MakeButton("Focus Camera", drawer, "Focus camera", Kind.Primary, 14f, Icon.Focus, 10f);
            UITheme.BottomLeft((RectTransform)focus.transform, 20f, 20f, bw, 44f);
            focus.onClick.AddListener(() => FocusCamera(moduleId));
            Button resetModule = UITheme.MakeButton("Reset Module", drawer, "Reset module", Kind.DangerSoft, 14f, Icon.Reset, 10f);
            UITheme.BottomLeft((RectTransform)resetModule.transform, 30f + bw, 20f, bw, 44f);
            resetModule.onClick.AddListener(() => ResetModule(title));
        }
        else
        {
            Button focus = UITheme.MakeButton("Focus Camera", drawer, "Focus camera", Kind.Primary, 14f, Icon.Focus, 10f);
            UITheme.BottomLeft((RectTransform)focus.transform, 20f, 20f, DrawerInner, 44f);
            focus.onClick.AddListener(() => FocusCamera(moduleId));
        }
        body.sizeDelta = new Vector2(0f, y + 8f);

        // Side sheet from below the header down to just above the bottom module cards.
        drawer.anchorMin = new Vector2(1f, 0f);
        drawer.anchorMax = Vector2.one;
        drawer.pivot = new Vector2(1f, 1f);
        drawer.offsetMin = new Vector2(-16f - DrawerWidth, DrawerBottom);
        drawer.offsetMax = new Vector2(-16f, -DrawerTop);
        return root;
    }

    /// <summary>Scrollable region between the drawer header and its footer buttons. Returns the
    /// content rect; its children are laid out top-down and its height is set by the caller.</summary>
    private static RectTransform CreateScrollBody(RectTransform drawer)
    {
        RectTransform viewport = UITheme.NewRect("Scroll Viewport", drawer);
        UITheme.Fill(viewport, 0f, DrawerHeaderHeight, 0f, DrawerFooterHeight);
        viewport.gameObject.AddComponent<UIRaycastTarget>();
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = UITheme.NewRect("Scroll Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;

        Image track = UITheme.Panel("Scrollbar", drawer, UITheme.Sunken, 3f, true);
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = Vector2.one;
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.offsetMin = new Vector2(-10f, DrawerFooterHeight + 6f);
        trackRect.offsetMax = new Vector2(-5f, -DrawerHeaderHeight - 2f);

        RectTransform slidingArea = UITheme.NewRect("Sliding Area", trackRect);
        UITheme.Fill(slidingArea);
        Image handle = UITheme.Panel("Handle", slidingArea, UITheme.LineStrong, 3f, true);
        UITheme.Fill(handle.rectTransform);

        Scrollbar scrollbar = trackRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }

    /// <summary>Builds the module's sliders from <paramref name="top"/> down; returns the y
    /// below the last one.</summary>
    private float CreateControls(Transform parent, string id, string moduleTitle, float top)
    {
        float y = top;
        void S(string label, float min, float max, float value, int decimals, string unit, Action<float> onChanged)
        {
            CreateSlider(parent, label, min, max, value, decimals, unit, onChanged, y, moduleTitle);
            y += 56f;
        }

        switch (id)
        {
            case "electrolyzer":
                S("Plant load", 0f, 100f, 100f, 0, "%", v => Simulator()?.SetTimelinePercent(v));
                S("Power", 0f, 100f, 75f, 0, "%", v => Simulator()?.SetElectrolyzerPower(v));
                S("Water feed", 0f, 130f, 100f, 0, "%", v => Simulator()?.SetWaterFeed(v));
                break;
            case "absorber":
                S("Amine flow", 10f, 100f, 65f, 0, "%", v => Simulator()?.SetAmineFlow(v));
                S("Flue gas", 0f, 130f, 100f, 0, "%", v => Simulator()?.SetFlueGasFlow(v));
                break;
            case "desorber":
                S("Steam flow", 0f, 100f, 70f, 0, "%", v => Simulator()?.SetRegeneratorSteam(v));
                S("Regen temp", 80f, 130f, 105f, 0, "°C", v => Simulator()?.SetRegeneratorTemperature(v));
                break;
            case "compressor":
                S("Comp. ratio", 1f, 6f, 3f, 1, "", v => Simulator()?.SetCompressionRatio(v));
                S("Outlet press.", 40f, 100f, 70f, 0, "bar", v => Simulator()?.SetReactorPressure(v));
                break;
            case "reactor":
                S("Temp", 180f, 300f, 250f, 0, "°C", v => Simulator()?.SetReactorTemperature(v));
                S("Pressure", 40f, 100f, 70f, 0, "bar", v => Simulator()?.SetReactorPressure(v));
                S("H2/CO2", 1f, 6f, 3f, 1, "", v => Simulator()?.SetH2Co2Ratio(v));
                S("GHSV", 1000f, 20000f, 8000f, 0, "1/h", v => Simulator()?.SetGHSV(v));
                S("Feed flow", 20f, 130f, 100f, 0, "%", v => Simulator()?.SetReactorFeedFlow(v));
                break;
            case "condenser":
                S("Cooling flow", 0f, 100f, 70f, 0, "%", v => Simulator()?.SetCoolingWaterFlow(v));
                S("Cooling temp", 5f, 45f, 24f, 0, "°C", v => Simulator()?.SetCoolingWaterTemperature(v));
                break;
            case "separator":
                S("Recycle ratio", 0f, 100f, 65f, 0, "%", v => Simulator()?.SetRecycleRatio(v));
                S("Sep. temp", 20f, 65f, 34f, 0, "°C", v => Simulator()?.SetSeparatorTemperature(v));
                break;
            case "distillation":
                S("Reflux ratio", 0.5f, 5f, 3.2f, 1, "", v => Simulator()?.SetRefluxRatio(v));
                S("Reboiler temp", 70f, 115f, 98f, 0, "°C", v => Simulator()?.SetDistillationReboilerTemperature(v));
                break;
        }
        return y - 12f;
    }

    private static readonly Dictionary<string, string> SliderDisplayNames = new Dictionary<string, string>
    {
        { "Temp", "Temperature" }, { "H2/CO2", "H₂/CO₂ ratio" }, { "Comp. ratio", "Compression ratio" },
        { "Outlet press.", "Outlet pressure" }, { "Regen temp", "Regeneration temp" }, { "Sep. temp", "Separator temp" },
    };

    private void CreateSlider(Transform parent, string label, float min, float max, float value, int decimals, string unit, Action<float> onChanged, float y, string moduleTitle)
    {
        PlantProcessSimulator simulator = Simulator();
        if (simulator != null)
            value = simulator.GetControlValue(label, value);

        string display = SliderDisplayNames.TryGetValue(label, out string pretty) ? pretty : label;
        Text labelText = UITheme.Label(label + " Label", parent, display, 13.5f, W.Bold, UITheme.Ink2);
        UITheme.TopLeft(labelText.rectTransform, 20f, y, 200f, 22f);

        Image chip = UITheme.Panel(label + " Value Chip", parent, UITheme.AccentSoft, 6f);
        Text valueText = UITheme.Label(label + " Value", chip.transform, Format(value, decimals, unit), 13f, W.ExtraBold, UITheme.Accent, TextAnchor.MiddleCenter);
        UITheme.Fill(valueText.rectTransform);
        float chipW = Mathf.Max(ChipWidth(valueText, Format(max, decimals, unit)), ChipWidth(valueText, Format(min, decimals, unit)));
        UITheme.TopRight(chip.rectTransform, 20f, y, chipW, 22f);
        valueText.text = Format(value, decimals, unit);

        Text minText = UITheme.Label(label + " Min", parent, Format(min, decimals == 0 ? 0 : 1, ""), 11.5f, W.SemiBold, UITheme.Subtle);
        UITheme.TopLeft(minText.rectTransform, 20f, y + 26f, 40f, 20f);
        Text maxText = UITheme.Label(label + " Max", parent, Format(max, decimals == 0 ? 0 : 1, ""), 11.5f, W.SemiBold, UITheme.Subtle, TextAnchor.MiddleRight);
        UITheme.TopRight(maxText.rectTransform, 20f, y + 26f, 44f, 20f);

        Slider slider = UITheme.MakeSlider(label + " Slider", parent, min, max, value);
        UITheme.TopLeft((RectTransform)slider.transform, 60f, y + 25f, DrawerInner - 40f - 44f - 8f, 22f);

        slider.onValueChanged.AddListener(v =>
        {
            valueText.text = Format(v, decimals, unit);
            onChanged?.Invoke(v);
        });

        onChanged?.Invoke(value);

        SliderCommitTracker tracker = slider.gameObject.AddComponent<SliderCommitTracker>();
        tracker.Slider = slider;
        tracker.Module = moduleTitle;
        tracker.Parameter = label;
        tracker.LastCommittedValue = value;

        registeredSliders.Add(new RegisteredSlider { Slider = slider, DefaultValue = value, ValueText = valueText, ValueChip = chip, LabelText = labelText, Decimals = decimals, Unit = unit, Tracker = tracker });
    }

    private static float ChipWidth(Text sample, string text)
    {
        string old = sample.text;
        sample.text = text;
        float w = sample.preferredWidth + 18f;
        sample.text = old;
        return Mathf.Max(52f, w);
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
        SetGraphicAlpha(entry.Slider.handleRect, dim ? 0.4f : 1f);
        if (entry.ValueText != null) SetTextAlpha(entry.ValueText, dim ? 0.45f : 1f);
        if (entry.ValueChip != null) entry.ValueChip.color = dim ? UITheme.Sunken : UITheme.AccentSoft;
        if (entry.ValueText != null) entry.ValueText.color = UITheme.WithAlpha(dim ? UITheme.Subtle : UITheme.Accent, 1f);
        if (entry.LabelText != null) SetTextAlpha(entry.LabelText, dim ? 0.45f : 1f);
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

    /// <summary>Puts one module's sliders back to their start-of-run values (each change is
    /// recorded like a manual one, so the graphs show it).</summary>
    private void ResetModule(string moduleTitle)
    {
        foreach (RegisteredSlider entry in registeredSliders)
        {
            if (entry.Slider == null || entry.Tracker == null || !entry.Slider.interactable) continue;
            if (!string.Equals(entry.Tracker.Module, moduleTitle, StringComparison.OrdinalIgnoreCase)) continue;
            float before = entry.Slider.value;
            if (Mathf.Approximately(before, entry.DefaultValue)) continue;
            entry.Slider.value = entry.DefaultValue;
            PlantProcessSimulator.Instance?.CommitManualChange(entry.Tracker.Module, entry.Tracker.Parameter, before, entry.DefaultValue);
            entry.Tracker.LastCommittedValue = entry.DefaultValue;
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

        CloseAllPanels();
    }

    private void FocusCamera(string id)
    {
        ModuleInfo info = Info(id);
        if (info.FocusIndex < 0) return;
        OrbitCameraController cam = FindFirstObjectByType<OrbitCameraController>();
        if (cam != null) cam.FocusModule(info.FocusIndex);
    }

    // ---- module content ------------------------------------------------------------

    private static string F0(float v) => v.ToString("F0");
    private static string F1(float v) => v.ToString("F1");

    private static ModuleInfo Info(string id)
    {
        switch (id)
        {
            case "electrolyzer":
                return new ModuleInfo
                {
                    Subtitle = "Hydrogen production", Icon = Icon.Bolt, Color = UITheme.Hex("0EA5E9"), FocusIndex = 0,
                    Description = "Water electrolysis. Higher power and water feed increase hydrogen output and the H₂ pipe flow.",
                    Readouts = new[]
                    {
                        new Readout("Water", "kg/h", s => F0(s.waterFeedKgH)),
                        new Readout("H₂", "kg/h", s => F0(s.h2InputKgH)),
                        new Readout("O₂", "kg/h", s => F0(s.oxygenByproductKgH)),
                        new Readout("Power", "%", s => F0(s.electrolyzerPowerPercent)),
                    }
                };
            case "absorber":
                return new ModuleInfo
                {
                    Subtitle = "Amine CO₂ capture", Icon = Icon.Column, Color = UITheme.Hex("10B981"), FocusIndex = 2,
                    Description = "Amine flow controls how much of the flue-gas CO₂ is captured and enters the process.",
                    Readouts = new[]
                    {
                        new Readout("Flue-gas CO₂", "kg/h", s => F0(s.co2InputKgH)),
                        new Readout("Captured", "kg/h", s => F0(s.co2CapturedKgH)),
                        new Readout("Efficiency", "%", s => F0(s.captureEfficiencyPercent)),
                    }
                };
            case "desorber":
                return new ModuleInfo
                {
                    Subtitle = "Solvent regeneration", Icon = Icon.Column, Color = UITheme.Hex("F59E0B"), FocusIndex = 4,
                    Description = "Steam heat strips CO₂ from the rich amine. Low heat leaves the solvent partially loaded.",
                    Readouts = new[]
                    {
                        new Readout("Steam flow", "%", s => F0(s.regeneratorSteamPercent)),
                        new Readout("Regen temp", "°C", s => F0(s.regeneratorTemperatureC)),
                        new Readout("CO₂ released", "kg/h", s => F0(s.co2CapturedKgH)),
                    }
                };
            case "compressor":
                return new ModuleInfo
                {
                    Subtitle = "Syngas compression", Icon = Icon.Gauge, Color = UITheme.Hex("8B5CF6"), FocusIndex = 5,
                    Description = "Higher pressure improves conversion in the reactor but needs more compressor power.",
                    Readouts = new[]
                    {
                        new Readout("Ratio", "", s => F1(s.compressionRatio)),
                        new Readout("Outlet", "bar", s => F0(s.reactorPressureBar)),
                        new Readout("Syngas", "kg/h", s => F0(s.syngasFeedKgH)),
                    }
                };
            case "reactor":
                return new ModuleInfo
                {
                    // Short, so the live status chip fits beside it.
                    Subtitle = "R-201", Icon = Icon.Flask, Color = UITheme.Hex("EA580C"), FocusIndex = 7,
                    Description = "Fixed-bed synthesis: conditioned H₂/CO₂ syngas is converted to methanol and water over the catalyst bed. In this model, single-pass conversion peaks near 240 °C.",
                    Readouts = new[]
                    {
                        new Readout("Yield", "%", s => F1(s.reactorYieldPercent)),
                        new Readout("Methanol", "kg/h", s => F0(s.methanolProductionKgH)),
                        new Readout("Temp", "°C", s => F0(s.reactorTemperatureC)),
                        new Readout("Pressure", "bar", s => F0(s.reactorPressureBar)),
                    }
                };
            case "heatexchanger":
                return new ModuleInfo
                {
                    Subtitle = "Feed/effluent heat recovery", Icon = Icon.Swap, Color = UITheme.Hex("F97316"), FocusIndex = 6,
                    Description = "Preheats the cold reactor feed against the hot reactor effluent before the fixed bed, reducing external heating duty.",
                    Readouts = new[]
                    {
                        new Readout("Syngas feed", "kg/h", s => F0(s.syngasFeedKgH)),
                        new Readout("Effluent temp", "°C", s => F0(s.reactorTemperatureC)),
                    }
                };
            case "condenser":
                return new ModuleInfo
                {
                    Subtitle = "Effluent cooling", Icon = Icon.Snow, Color = UITheme.Hex("38BDF8"), FocusIndex = 8,
                    Description = "Cooling water condenses crude methanol and water out of the reactor effluent.",
                    Readouts = new[]
                    {
                        new Readout("Cooling", "%", s => F0(s.coolingWaterFlowPercent)),
                        new Readout("Water temp", "°C", s => F0(s.coolingWaterTemperatureC)),
                        new Readout("Recovery", "%", s => F0(s.condenserRecoveryPercent)),
                        new Readout("Liquid", "kg/h", s => F0(s.methanolProductionKgH)),
                    }
                };
            case "separator":
                return new ModuleInfo
                {
                    Subtitle = "Flash separation + recycle", Icon = Icon.Split, Color = UITheme.Hex("E11D48"), FocusIndex = 9,
                    Description = "More recycle raises overall conversion, and also the compressor load.",
                    Readouts = new[]
                    {
                        new Readout("Recycle", "%", s => F0(s.recycleRatioPercent)),
                        new Readout("Recycle gas", "kg/h", s => F0(s.recycleGasKgH)),
                        new Readout("Temp", "°C", s => F0(s.separatorTemperatureC)),
                    }
                };
            case "distillation":
                return new ModuleInfo
                {
                    Subtitle = "Methanol purification", Icon = Icon.Column, Color = UITheme.Hex("65A30D"), FocusIndex = 10,
                    Description = "Reflux and reboiler heat set the product purity and the separation energy demand.",
                    Readouts = new[]
                    {
                        new Readout("Reflux", "", s => F1(s.refluxRatio)),
                        new Readout("Reboiler", "°C", s => F0(s.distillationReboilerTemperatureC)),
                        new Readout("Purity", "%", s => s.methanolPurityPercent.ToString("F2")),
                        new Readout("Energy", "%", s => F0(s.distillationEnergyPercent)),
                    }
                };
            case "storage":
                return new ModuleInfo
                {
                    Subtitle = "Product storage", Icon = Icon.Tank, Color = UITheme.Hex("16A34A"), FocusIndex = 11,
                    Description = "Refined methanol accumulates here. Near its limit the tank's capacity interlock holds back production upstream.",
                    Readouts = new[]
                    {
                        new Readout("Stored", "kg", s => F0(s.storedMethanolKg)),
                        new Readout("Fill", "%", s => F0(s.storageFillPercent)),
                        new Readout("Rate", "kg/h", s => F0(s.methanolProductionKgH)),
                        new Readout("Purity", "%", s => s.methanolPurityPercent.ToString("F2")),
                    }
                };
            case "co2tank":
                return new ModuleInfo
                {
                    Subtitle = "CO₂ buffer", Icon = Icon.Tank, Color = UITheme.Hex("10B981"), FocusIndex = 1,
                    Description = "Buffers conditioned CO₂ ahead of the synthesis-gas mixing junction.",
                    Readouts = new[]
                    {
                        new Readout("CO₂ inflow", "kg/h", s => F0(s.co2CapturedKgH)),
                        new Readout("Capture", "%", s => F0(s.captureEfficiencyPercent)),
                    }
                };
            case "h2tank":
                return new ModuleInfo
                {
                    Subtitle = "H₂ buffer", Icon = Icon.Tank, Color = UITheme.Hex("0EA5E9"), FocusIndex = 3,
                    Description = "Decouples variable electrolyzer output from the steady synthesis-loop demand.",
                    Readouts = new[]
                    {
                        new Readout("H₂ inflow", "kg/h", s => F0(s.h2InputKgH)),
                        new Readout("Electrolyzer", "%", s => F0(s.electrolyzerPowerPercent)),
                    }
                };
            default:
                return new ModuleInfo
                {
                    Subtitle = "Process module", Icon = Icon.Info, Color = UITheme.Accent, FocusIndex = -1,
                    Description = "",
                    Readouts = new[]
                    {
                        new Readout("H₂", "kg/h", s => F0(s.h2InputKgH)),
                        new Readout("CO₂", "kg/h", s => F0(s.co2CapturedKgH)),
                        new Readout("Methanol", "kg/h", s => F0(s.methanolProductionKgH)),
                    }
                };
        }
    }

    private void UpdateReadouts(ModuleAnchor anchor)
    {
        PlantProcessSimulator sim = Simulator();
        if (sim == null || anchor.Info == null || anchor.ReadoutValues == null) return;
        PlantProcessSimulator.ProcessSnapshot s = sim.Current;
        for (int i = 0; i < anchor.ReadoutValues.Length; i++)
        {
            Readout r = anchor.Info.Readouts[i];
            anchor.ReadoutValues[i].Set(r.Value(s), r.Unit);
        }
        if (anchor.StatusChip != null)
        {
            string status = BuildReactorStatus(s);
            anchor.StatusChip.Set(status == "Normal" ? UIStatusChip.Kind.Success : UIStatusChip.Kind.Warning, status);
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

    private static string Format(float value, int decimals, string unit)
    {
        string number = decimals <= 0 ? value.ToString("F0") : value.ToString("F1");
        return string.IsNullOrEmpty(unit) ? number : $"{number} {unit}";
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
        public string Title;
        public Transform Target;
        public Vector3 WorldOffset;
        public GameObject ButtonRoot;
        public RectTransform ButtonRect;
        public GameObject PanelRoot;
        public ModuleInfo Info;
        public UIValueText[] ReadoutValues;
        public UIStatusChip StatusChip;
        public Vector3 CachedScreen;
        public bool IsInFrustum;
    }
}
