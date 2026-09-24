using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Icon = UITheme.Icon;
using Kind = UITheme.ButtonKind;
using W = UITheme.Weight;

/// <summary>
/// Step-by-step guided tour of the whole application.
///
/// It opens every time the app is launched (straight after the welcome screen's START, see
/// <see cref="WelcomeScreenRuntime"/>) and can be replayed at any time
/// from HELP -> START TUTORIAL. Each step dims the screen except the part of the UI being
/// explained, and drives the dashboard into the matching state first, so the tour points at
/// the real controls rather than at a description of them.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialRuntime : MonoBehaviour
{
    private const string RuntimeRootName = "Generated Tutorial Overlay";

    private static readonly Color DimColor = new Color(0.06f, 0.09f, 0.16f, 0.62f);

    private const float CardWidth = 580f;
    private const float CardMinHeight = 220f;
    private const float HighlightPadding = 8f;
    private const float FrameThickness = 2.5f;
    private const float CardMargin = 22f;

    public static TutorialRuntime Instance { get; private set; }

    /// <summary>A single tour stop.</summary>
    private sealed class Step
    {
        public string Title;
        public string Body;
        /// <summary>GameObject name of the UI element to spotlight; null for a full-screen step.</summary>
        public string TargetName;
        /// <summary>Viewport-fraction rectangle used when <see cref="TargetName"/> resolves to
        /// nothing (or to a hidden object). Zero-sized means "no highlight".</summary>
        public Rect ViewportFallback;
        /// <summary>Puts the application into the state this step talks about. Must be complete
        /// rather than incremental, so stepping backwards restores the right view too.</summary>
        public Action Apply;
        /// <summary>Spotlight <see cref="TargetName"/> even while it is hidden — for pointing at
        /// where something *will* appear. Only sensible together with an arrow and a caption,
        /// otherwise an empty spotlit strip just reads as a bug.</summary>
        public bool SpotlightWhenHidden;
        /// <summary>Draw an arrow from the card to the spotlight.</summary>
        public bool ArrowToTarget;
        /// <summary>Short caption rendered beside the arrow's tip.</summary>
        public string ArrowLabel;
        /// <summary>Let the pointer through to the 3D scene inside the spotlight, so the step
        /// can be tried out while it is being explained. The dim panels still cover — and so
        /// still block — every piece of UI outside the spotlight.</summary>
        public bool AllowInteraction;
    }

    private const float ArrowHeadLength = 18f;
    private const float ArrowRunLength = 78f;

    private readonly List<Step> steps = new List<Step>();
    private readonly List<Vector2> arrowPoints = new List<Vector2>();
    private readonly Vector3[] worldCorners = new Vector3[4];

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform root;
    private readonly RectTransform[] dimPanels = new RectTransform[4];
    private RectTransform frame;
    private Image blockerImage;
    private GameObject arrowRoot;
    private UIGraphLine arrowShaft;
    private TutorialArrowHead arrowHead;
    private RectTransform arrowPill;
    private Text arrowLabel;
    private RectTransform card;
    private Text cardTitle;
    private Text cardBody;
    private Text cardCounter;
    private UIProgressBar progress;
    private Button previousButton;
    private Button nextButton;

    private RectTransform currentTarget;
    // Steps run their Apply() before the target is looked up, but a panel can still take a
    // frame or two to appear. Retry for a short while, then stop searching every frame.
    private int targetRetries;
    private int index;
    private bool running;
    private Coroutine fade;

    public bool IsRunning => running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<TutorialRuntime>() != null) return;
        new GameObject(RuntimeRootName).AddComponent<TutorialRuntime>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        BuildSteps(ExternalAnalyticsWindow.IsSupported);
        BuildOverlay();
        // With the welcome screen up, its START button opens the tour instead.
        if (!WelcomeScreenRuntime.IsShowing) StartCoroutine(AutoStartWhenReady());
    }

    /// <summary>The tour opens on every launch. The dashboard builds itself in its own
    /// Start(), so the tour waits for that canvas to exist before it tries to spotlight
    /// anything on it.</summary>
    private IEnumerator AutoStartWhenReady()
    {
        float deadline = Time.unscaledTime + 8f;
        while (Time.unscaledTime < deadline)
        {
            IcodosDashboardRuntime dashboard = IcodosDashboardRuntime.Instance;
            if (dashboard != null && dashboard.IsBuilt) break;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.35f);
        StartTutorial();
    }

    // ---- public entry points ------------------------------------------------

    /// <summary>Opens the tour at its first step. Safe to call while it is already running.</summary>
    public void StartTutorial()
    {
        if (root == null) return;
        running = true;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeOverlay(true));
        ShowStep(0);
    }

    /// <summary>Ends the tour and returns the application to its normal starting view.</summary>
    public void EndTutorial()
    {
        if (!running) return;
        running = false;
        currentTarget = null;
        IcodosDashboardRuntime.Instance?.TutorialRestoreDefaults();
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeOverlay(false));
    }

    // ---- step content -------------------------------------------------------

    private static void Dashboard(Action<IcodosDashboardRuntime> action)
    {
        IcodosDashboardRuntime dashboard = IcodosDashboardRuntime.Instance;
        if (dashboard != null) action(dashboard);
    }

    private void Add(string title, string body, string targetName = null, Rect fallback = default, Action apply = null,
        bool spotlightWhenHidden = false, bool arrowToTarget = false, string arrowLabel = null, bool allowInteraction = false)
    {
        steps.Add(new Step
        {
            Title = title,
            Body = body,
            TargetName = targetName,
            ViewportFallback = fallback,
            Apply = apply,
            SpotlightWhenHidden = spotlightWhenHidden,
            ArrowToTarget = arrowToTarget,
            ArrowLabel = arrowLabel,
            AllowInteraction = allowInteraction,
        });
    }

    /// <summary>Roughly the equipment row of the 3D plant, used for the steps that talk about
    /// the scene itself rather than about a specific widget. Deliberately short enough that
    /// the explanation card still fits below it instead of being pushed over the spotlight.</summary>
    private static readonly Rect PlantArea = new Rect(0.21f, 0.42f, 0.58f, 0.32f);

    /// <summary>A UI label named in the step text, drawn in the accent colour.</summary>
    private static string UI(string label) => $"<color=#1D4ED8>{label}</color>";

    private void BuildSteps(bool separateAnalyticsWindow)
    {
        Add("Welcome to the Power-to-Methanol Digital Twin",
            "This short tour walks through every part of the application: the 3D plant, the six sections in the navigation bar, the Flow lab, the equipment controls, the analytics window and the dock of tools along the bottom.\n\n" +
            $"It opens every time the application starts. Use {UI("Next")} and {UI("Previous")} to move through it, or {UI("Skip tutorial")} to jump straight in. You can replay it at any time from the {UI("?")} button → {UI("Start tutorial")}.\n\n" +
            "Keyboard: Enter or Space = next, Backspace = previous, Esc = skip.",
            apply: () => Dashboard(d => d.TutorialRestoreDefaults()));

        Add("The main navigation",
            $"Six sections live in the navigation bar: {UI("Overview")}, {UI("Process map")}, {UI("Flow lab")}, {UI("Reactor lab")}, {UI("Analytics")} and {UI("Simulation")}. Selecting one opens its card and moves the camera to a matching view; the highlighted pill always shows where you are.\n\n" +
            $"The round {UI("?")} button on the far right opens the about box, which is also where you can replay this tour. Next to it, the status capsule shows whether the simulation is running or paused.",
            "Header");

        Add("Overview — the home view",
            $"{UI("Overview")} is the landing view: the full plant in 3D, with the plant status, the four process tiles and the stream legend arranged around it.\n\n" +
            "From any other section, clicking empty space in the 3D scene brings you straight back here.",
            "OVERVIEW",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("Plant status",
            "The card on the right summarises the whole plant: a status badge (normal operation, operating caution, attention required or paused), the overall-efficiency ring, methanol production in kg/h, CO₂ utilisation, and the product-tank level with an estimate of how long it has left before it is full.",
            "Plant Status",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("One-click best case",
            $"{UI("Set maximum efficiency")} drives every module slider to the setpoint combination that gives the highest overall efficiency in this educational model, and resumes the run.\n\n" +
            "It is the quickest way to see the plant at its best before you start experimenting with the controls yourself.",
            "Set Maximum Efficiency",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("Live process tiles",
            "The four tiles along the bottom report the process stages live:\n\n" +
            $"{UI("Electrolyzer")} — power, hydrogen, water and oxygen\n" +
            $"{UI("CO₂ capture")} — capture efficiency, captured CO₂, amine flow, regenerator temperature\n" +
            $"{UI("Reactor")} — temperature, pressure, H₂/CO₂ ratio, yield\n" +
            $"{UI("Separation")} — recycle, purity, storage, product rate\n\n" +
            $"{UI("Open")} on a tile opens that module's controls.",
            "Process KPI Strip",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("Stream legend",
            "The legend lists what the pipes carry and the colour each stream is drawn in: water and hydrogen, amine and captured CO₂, compressed syngas, hot reactor effluent, crude methanol, refined methanol, and the dashed gas recycle loop.\n\n" +
            $"Those colours come alive in the {UI("Flow lab")}, covered in a moment: open it and every pipe shows its stream moving in exactly these legend colours.",
            "Process Flow Legend",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("The 3D plant and the camera",
            "The plant itself is fully navigable with the mouse:\n\n" +
            "Hold the left button and drag — turn the view in any direction.\n" +
            "Hold Shift, then drag — pan the view horizontally or vertically.\n" +
            "Scroll the wheel — zoom in and out, towards whatever the cursor is pointing at.\n\n" +
            "Try it now: the highlighted area is live while this step is open.",
            null, PlantArea,
            () => Dashboard(d => d.TutorialShowPage("overview")),
            allowInteraction: true);

        Add("Equipment controls",
            $"Hover over any piece of equipment and a small label with an {UI("i")} appears on it. Click the label to open that module's control drawer on the right: live read-outs, its operating sliders, {UI("Focus camera")} and {UI("Reset module")}.\n\n" +
            "Those sliders are the real inputs to the process model: moving one updates the tiles, the warnings, the tank level and the pipe animation together.",
            null, PlantArea,
            () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("Process map",
            $"{UI("Process map")} is a guided ten-step walkthrough, from renewable power and water all the way to methanol storage.\n\n" +
            $"{UI("Next step")} and {UI("Previous")} move through it; the card explains each stage and lists the streams involved, and the camera flies to the matching equipment automatically.",
            "Guided Process",
            apply: () => Dashboard(d => d.TutorialShowPage("process")));

        Add("Flow lab — switching the flow on",
            $"{UI("Flow lab")} is an on/off switch for the animated process flow. While it is on, the pipes turn see-through and show the stream inside them moving from source to destination; while it is off, the pipes are the normal solid plant pipework.\n\n" +
            $"Click {UI("Flow lab")} (or {UI("Show streams")} in the dock) to switch it on; its card opens with it. The flow stays on until you click {UI("Flow lab")} again. It is on right now for this part of the tour.",
            "FLOW LAB",
            apply: () => Dashboard(d => d.TutorialShowPage("flow")),
            arrowToTarget: true, arrowLabel: "Click to turn the flow on or off");

        Add("Flow lab — reading the streams",
            "Every stream is drawn in its legend colour. Gases — hydrogen, CO₂, syngas and the hot reactor effluent — move as turbulent eddies. Liquids — the amine loop and methanol — fill the bore and flow more slowly. Crude methanol shows liquid along the bottom with vapour above it, and the recycle loop runs in dashes, just like its legend swatch.\n\n" +
            "Speed and brightness follow the calculated mass flow, so moving a slider visibly changes the stream. The plant is live here: drag to orbit and scroll to zoom.",
            null, PlantArea,
            () => Dashboard(d => d.TutorialShowPage("flow")),
            allowInteraction: true);

        Add("Flow lab — filters and closing",
            $"The card filters the network by subsystem: {UI("All streams")}, {UI("Feed gases")}, {UI("Capture loop")}, {UI("Synthesis loop")} or {UI("Product path")}. Only the selected routes keep flowing, which makes one loop easy to follow through the plant.\n\n" +
            $"The close button in the card's corner only closes the card — the flow keeps running, so you can orbit and zoom around the plant with it on. {UI("Hide stream visuals")} pauses the effect without leaving the lab.",
            "Flow Lab",
            apply: () => Dashboard(d => d.TutorialShowPage("flow")));

        Add("Reactor lab",
            $"{UI("Reactor lab")} focuses the transparent fixed-bed methanol reactor. {UI("Focus reactor")} re-frames the camera on it and {UI("Open reactor controls")} opens its control drawer.\n\n" +
            "The card shows live temperature, pressure, H₂/CO₂ ratio and yield. Inside the reactor, the catalyst-bed colour tracks the operating state and the moving particles represent the species and the conversion.",
            "Reactor Lab",
            apply: () => Dashboard(d => d.TutorialShowPage("reactor")));

        Add("Simulation",
            $"{UI("Simulation")} summarises plant load, syngas feed, methanol output and storage, and tells you when the storage interlock is holding production back.\n\n" +
            "Its buttons open any module's control drawer and fly the camera to it — those sliders remain the single source of truth for the calculation.",
            "Simulation",
            apply: () => Dashboard(d => d.TutorialShowPage("simulation")));

        if (separateAnalyticsWindow)
        {
            Add("Analytics — its own window",
                $"{UI("Analytics")} opens in a separate window of its own, next to this one. Like any other application window you can move it, resize it, minimise or maximise it, or put it on a second monitor — and it keeps showing this plant's live data the whole time.\n\n" +
                "Only one analytics window can be open at a time: while it is open this button is greyed out, and it becomes clickable again as soon as you close the window.",
                "ANALYTICS",
                apply: () => Dashboard(d => d.TutorialShowPage("overview")),
                arrowToTarget: true, arrowLabel: "Opens a separate window");

            Add("Inside the analytics window",
                $"{UI("Pause")} and {UI("Reset")} sit in the window's own title bar, so you can control the run from there.\n\n" +
                $"{UI("Stats")} shows throughput and the five headline metrics with their live values. {UI("Visualise")} holds four graph views: {UI("Reactor yield")} and {UI("Efficiency")} plot the response against one reactor parameter (picking a parameter locks the other reactor sliders for a clean one-factor-at-a-time scan), {UI("OFAT timeline")} records a guided study against time, and {UI("Live progress")} strip-charts the run as it happens. Every graph can {UI("Export")} its data.",
                "ANALYTICS",
                apply: () => Dashboard(d => d.TutorialShowPage("overview")));
        }
        else
        {
            Add("Analytics — stats",
                $"{UI("Analytics")} opens a window you can drag anywhere by its title bar. {UI("Pause")} and {UI("Reset")} sit in that title bar, so you can control the run without closing the window.\n\n" +
                $"The {UI("Stats")} tab shows throughput and the five headline metrics — overall efficiency, CO₂ capture, reactor yield, methanol purity and storage fill — each with its live value, plus a short set of insights.",
                "Analytics Window",
                apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, null)));

            Add("Analytics — visualise",
                $"The {UI("Visualise")} tab holds four graph views:\n\n" +
                $"{UI("Reactor yield")} — yield against one reactor parameter\n" +
                $"{UI("Efficiency")} — overall efficiency against one reactor parameter\n" +
                $"{UI("OFAT timeline")} — a guided one-factor-at-a-time study\n" +
                $"{UI("Live progress")} — strip charts of the run as it happens",
                "Visualise Sub Tabs",
                apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, "yield")));

            Add("Correlation graphs and the variable lock",
                $"{UI("Reactor yield")} and {UI("Efficiency")} plot the response against one reactor parameter at a time: {UI("Temp")}, {UI("Pressure")}, {UI("H₂:CO₂")}, {UI("GHSV")} or {UI("Feed")}.\n\n" +
                "Every point is numbered in the order it was recorded and coloured by the module you changed. Picking a parameter locks every other reactor slider, so the curve you build is a clean one-factor-at-a-time scan.",
                "Reactor Yield Param Row",
                apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, "yield")));

            Add("OFAT timeline",
                $"{UI("OFAT timeline")} records a guided one-factor-at-a-time study against time. Pick the variable under {UI("Vary one")}, choose the response under {UI("Show")}, then move that slider while the rest stay locked.\n\n" +
                $"{UI("Line only")} and {UI("With points")} change the plot style, {UI("Previous")}, {UI("Next")} and {UI("Live")} page through the recorded timeline, and {UI("Export")} writes the series to a file.",
                "OFAT Timeline Sub Panel",
                apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, "ofat")));

            Add("Live progress",
                $"{UI("Live progress")} strip-charts the run as it happens. {UI("Efficiency")} and {UI("Methanol output")} switch between the two charts, and both keep recording in the background so switching never loses history.\n\n" +
                "The output chart also reports the cumulative amount already in the tank when you hover it.",
                "Live Sub Panel",
                apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, "live")));
        }

        Add("The dock",
            "The dock along the bottom is available from every section:\n\n" +
            $"{UI("Pause")} / {UI("Resume")} and {UI("Reset")} control the run\n" +
            $"{UI("View information")} opens the about box\n" +
            $"{UI("Show streams")} switches the Flow lab's animated pipe flow on and off\n" +
            $"{UI("Mass flow tool")} turns the cursor into a crosshair — point it at any pipe to read its mass flow, velocity and conditions\n" +
            $"{UI("Previous module")} and {UI("Next module")} step the camera through the equipment\n" +
            $"{UI("Reset view")} returns to the full plant overview",
            "Footer",
            apply: () => Dashboard(d =>
            {
                if (!d.AnalyticsIsExternal) d.TutorialSetAnalyticsView(false, null);
                d.TutorialShowPage("overview");
            }));

        Add("Safety and efficiency warnings",
            "The arrow points at the space just below the navigation bar. That is where a red or amber warning banner appears when an operating condition drifts out of the safe or sensible range — reactor temperature or pressure too high, storage nearly full, capture efficiency too low — listing the active alarms and cautions.\n\n" +
            "Nothing is showing there right now because the plant is running cleanly. The banner clears itself as soon as conditions recover, so it is worth watching while you experiment with the sliders.",
            "Warning Panel",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")),
            spotlightWhenHidden: true, arrowToTarget: true, arrowLabel: "Warnings appear here");

        Add("Help and this tour",
            $"The round {UI("?")} button opens the about box, with a summary of the controls and the educational disclaimer.\n\n" +
            $"{UI("Start tutorial")} in that box replays this tour whenever you want it, so nothing here is a one-time explanation.",
            "Help",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("You are ready",
            "That is the whole application: the 3D plant and its camera, the six sections, the Flow lab, the equipment controls, the analytics window and the dock.\n\n" +
            "One reminder before you start: every number and animation here is a simplified educational representation. This is not CFD, Aspen, industrial control software, or a validated process model.\n\n" +
            $"Press {UI("Finish")} to start exploring.");
    }

    // ---- navigation ---------------------------------------------------------

    private void ShowStep(int target)
    {
        if (steps.Count == 0) return;
        index = Mathf.Clamp(target, 0, steps.Count - 1);
        Step step = steps[index];

        step.Apply?.Invoke();
        currentTarget = string.IsNullOrEmpty(step.TargetName) ? null : FindRect(step.TargetName);
        targetRetries = currentTarget == null ? 30 : 0;

        cardTitle.text = step.Title;
        cardBody.text = step.Body;
        cardCounter.text = $"Step {index + 1} of {steps.Count}";
        progress.Set((index + 1) / (float)steps.Count);

        // The card grows with its text.
        float bodyHeight = Mathf.Ceil(cardBody.preferredHeight);
        card.sizeDelta = new Vector2(CardWidth, Mathf.Max(CardMinHeight, 82f + bodyHeight + 86f));

        bool first = index == 0;
        previousButton.interactable = !first;
        bool last = index == steps.Count - 1;
        UITheme.SetLabel(nextButton, last ? "Finish" : "Next");
        UITheme.SetIcon(nextButton, last ? Icon.Check : Icon.ChevronRight);

        // Steps that invite the user to try something drop the full-screen blocker; the dim
        // panels still cover every pixel outside the spotlight, so only the scene is reachable.
        blockerImage.raycastTarget = !step.AllowInteraction;

        UpdateLayout();
    }

    private void Next()
    {
        if (index >= steps.Count - 1) EndTutorial();
        else ShowStep(index + 1);
    }

    private void Previous() => ShowStep(index - 1);

    private void Update()
    {
        if (!running) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) EndTutorial();
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) Next();
        else if (keyboard.backspaceKey.wasPressedThisFrame) Previous();
    }

    // Layout runs every frame rather than only on step changes: the analytics window opens
    // with a scale tween and can be dragged, the camera keeps moving, and the window can be
    // resized - the spotlight has to track all of that.
    private void LateUpdate()
    {
        if (!running) return;
        if (currentTarget == null && targetRetries > 0)
        {
            targetRetries--;
            currentTarget = FindRect(steps[index].TargetName);
            if (currentTarget != null) targetRetries = 0;
        }
        UpdateLayout();
    }

    // ---- spotlight geometry -------------------------------------------------

    private void UpdateLayout()
    {
        if (root == null) return;
        Rect area = root.rect;
        bool highlighted = TryGetHighlight(area, out Rect spot);

        if (highlighted)
        {
            SetLocalRect(dimPanels[0], Rect.MinMaxRect(area.xMin, spot.yMax, area.xMax, area.yMax));
            SetLocalRect(dimPanels[1], Rect.MinMaxRect(area.xMin, area.yMin, area.xMax, spot.yMin));
            SetLocalRect(dimPanels[2], Rect.MinMaxRect(area.xMin, spot.yMin, spot.xMin, spot.yMax));
            SetLocalRect(dimPanels[3], Rect.MinMaxRect(spot.xMax, spot.yMin, area.xMax, spot.yMax));
            float t = FrameThickness;
            SetLocalRect(frame, Rect.MinMaxRect(spot.xMin - t, spot.yMin - t, spot.xMax + t, spot.yMax + t));
        }
        else
        {
            SetLocalRect(dimPanels[0], area);
            for (int i = 1; i < dimPanels.Length; i++) SetLocalRect(dimPanels[i], Rect.zero);
        }

        if (frame.gameObject.activeSelf != highlighted) frame.gameObject.SetActive(highlighted);
        PlaceCard(area, highlighted, spot);
        UpdateArrow(area, highlighted, spot);
    }

    /// <summary>Runs the annotation arrow from the card's near edge to the spotlight, with a
    /// single elbow when the two are not vertically aligned.</summary>
    private void UpdateArrow(Rect area, bool highlighted, Rect spot)
    {
        Step step = steps[index];
        bool show = highlighted && step.ArrowToTarget;
        if (arrowRoot.activeSelf != show) arrowRoot.SetActive(show);
        if (!show) return;

        Rect cardRect = new Rect((Vector2)card.anchoredPosition - card.sizeDelta * 0.5f, card.sizeDelta);
        bool cardBelow = cardRect.center.y <= spot.center.y;
        float startX = Mathf.Clamp(spot.center.x, cardRect.xMin + 30f, cardRect.xMax - 30f);
        Vector2 start = new Vector2(startX, cardBelow ? cardRect.yMax + 8f : cardRect.yMin - 8f);
        Vector2 tip = new Vector2(
            Mathf.Clamp(spot.center.x, spot.xMin + 20f, spot.xMax - 20f),
            cardBelow ? spot.yMin - 9f : spot.yMax + 9f);

        arrowPoints.Clear();
        arrowPoints.Add(start);
        if (Mathf.Abs(start.x - tip.x) > 24f)
        {
            float midY = (start.y + tip.y) * 0.5f;
            arrowPoints.Add(new Vector2(start.x, midY));
            arrowPoints.Add(new Vector2(tip.x, midY));
        }
        Vector2 previous = arrowPoints[arrowPoints.Count - 1];
        Vector2 headDirection = (tip - previous).sqrMagnitude > 1e-4f ? (tip - previous).normalized : Vector2.up;
        arrowPoints.Add(tip - headDirection * (ArrowHeadLength * 0.85f));

        arrowShaft.SetPoints(arrowPoints);
        arrowHead.SetArrow(tip, headDirection, ArrowHeadLength);

        bool hasLabel = !string.IsNullOrEmpty(step.ArrowLabel);
        if (arrowPill.gameObject.activeSelf != hasLabel) arrowPill.gameObject.SetActive(hasLabel);
        if (!hasLabel) return;
        arrowLabel.text = step.ArrowLabel;
        float labelWidth = Mathf.Ceil(arrowLabel.preferredWidth) + 28f;
        // Beside the middle of the shaft, in the gap the arrow run opened up.
        Vector2 mid = (start + tip) * 0.5f;
        float labelX = Mathf.Min(mid.x + 14f, area.xMax - labelWidth - 8f);
        SetLocalRect(arrowPill, new Rect(labelX, mid.y - 15f, labelWidth, 30f));
    }

    private bool TryGetHighlight(Rect area, out Rect result)
    {
        Step step = steps[index];
        if (currentTarget != null && (step.SpotlightWhenHidden || currentTarget.gameObject.activeInHierarchy) &&
            TryGetTargetRect(currentTarget, out result))
        {
            result = Clamp(Expand(result, HighlightPadding), area);
            if (result.width >= 4f && result.height >= 4f) return true;
        }

        Rect fallback = step.ViewportFallback;
        if (fallback.width > 0f && fallback.height > 0f)
        {
            result = new Rect(
                area.xMin + fallback.xMin * area.width,
                area.yMin + fallback.yMin * area.height,
                fallback.width * area.width,
                fallback.height * area.height);
            result = Clamp(result, area);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Converts a RectTransform on any screen-space canvas into this overlay's own
    /// local coordinates, so the spotlight lines up regardless of which canvas owns it.</summary>
    private bool TryGetTargetRect(RectTransform target, out Rect result)
    {
        result = default;
        target.GetWorldCorners(worldCorners);
        Vector2 minScreen = RectTransformUtility.WorldToScreenPoint(null, worldCorners[0]);
        Vector2 maxScreen = RectTransformUtility.WorldToScreenPoint(null, worldCorners[2]);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, minScreen, null, out Vector2 min)) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, maxScreen, null, out Vector2 max)) return false;
        result = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        return true;
    }

    /// <summary>Parks the explanation card next to the spotlight without ever covering it -
    /// below by preference, above when there is no room below, beside it when there is
    /// neither.</summary>
    private void PlaceCard(Rect area, bool highlighted, Rect spot)
    {
        Vector2 size = card.sizeDelta;
        if (!highlighted)
        {
            card.anchoredPosition = new Vector2(area.center.x, area.center.y);
            return;
        }

        // Arrow steps stand the card further off so the arrow has a run long enough to read as
        // an arrow, and so its caption has somewhere to sit.
        float gap = CardMargin + (steps[index].ArrowToTarget ? ArrowRunLength : 0f);

        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;
        float x = Mathf.Clamp(spot.center.x, area.xMin + halfW + CardMargin, area.xMax - halfW - CardMargin);

        float topIfBelow = spot.yMin - gap;
        if (topIfBelow - size.y >= area.yMin + CardMargin)
        {
            card.anchoredPosition = new Vector2(x, topIfBelow - halfH);
            return;
        }

        float bottomIfAbove = spot.yMax + gap;
        if (bottomIfAbove + size.y <= area.yMax - CardMargin)
        {
            card.anchoredPosition = new Vector2(x, bottomIfAbove + halfH);
            return;
        }

        float y = Mathf.Clamp(spot.center.y, area.yMin + halfH + CardMargin, area.yMax - halfH - CardMargin);
        bool toTheRight = area.xMax - spot.xMax >= spot.xMin - area.xMin;
        float sideX = toTheRight
            ? Mathf.Min(spot.xMax + CardMargin + halfW, area.xMax - halfW - CardMargin)
            : Mathf.Max(spot.xMin - CardMargin - halfW, area.xMin + halfW + CardMargin);
        card.anchoredPosition = new Vector2(sideX, y);
    }

    private static Rect Expand(Rect rect, float amount)
    {
        return Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);
    }

    private static Rect Clamp(Rect rect, Rect bounds)
    {
        float xMin = Mathf.Clamp(rect.xMin, bounds.xMin, bounds.xMax);
        float xMax = Mathf.Clamp(rect.xMax, bounds.xMin, bounds.xMax);
        float yMin = Mathf.Clamp(rect.yMin, bounds.yMin, bounds.yMax);
        float yMax = Mathf.Clamp(rect.yMax, bounds.yMin, bounds.yMax);
        return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
    }

    private static void SetLocalRect(RectTransform rect, Rect value)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = value.center;
        rect.sizeDelta = new Vector2(Mathf.Max(0f, value.width), Mathf.Max(0f, value.height));
    }

    /// <summary>Finds a UI element by GameObject name across every screen-space canvas in the
    /// scene except this overlay's own, so steps can point at the dashboard, the module
    /// panels or the warning band alike.</summary>
    private RectTransform FindRect(string name)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas candidate in canvases)
        {
            if (candidate == null || candidate == canvas) continue;
            if (candidate.transform.IsChildOf(transform)) continue;
            // Only the main window's overlay canvases can be spotlit; the separate analytics
            // window renders off-screen.
            if (candidate.isRootCanvas && candidate.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            RectTransform found = FindRecursive(candidate.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static RectTransform FindRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.Ordinal) && child is RectTransform rect) return rect;
            RectTransform found = FindRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // ---- overlay construction -----------------------------------------------

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("Tutorial Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the dashboard (90), the module drawers (95), the probe (140) and the about box
        // (200) so the tour always sits on top of whatever it is explaining.
        canvas.sortingOrder = 300;
        UITheme.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());
        canvasObject.AddComponent<GraphicRaycaster>();

        root = UITheme.NewRect("Tutorial Root", canvasObject.transform);
        UITheme.Fill(root);
        canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // A full-screen, almost invisible blocker keeps the tour modal: the application
        // underneath cannot be clicked out from under the step that is describing it.
        blockerImage = UITheme.Panel("Input Blocker", root, new Color(0f, 0f, 0f, 0.004f), 0f, true);
        UITheme.Fill(blockerImage.rectTransform);

        for (int i = 0; i < dimPanels.Length; i++)
            dimPanels[i] = UITheme.Panel("Dim " + i, root, DimColor, 0f, true).rectTransform;

        Image frameImage = UITheme.Panel("Highlight Frame", root, UITheme.Hex("3B82F6"));
        frameImage.sprite = UITheme.RoundedOutline(10f, FrameThickness);
        frameImage.type = Image.Type.Sliced;
        frame = frameImage.rectTransform;

        BuildArrow();
        BuildCard();
        root.gameObject.SetActive(false);
    }

    private void BuildArrow()
    {
        arrowRoot = new GameObject("Annotation Arrow", typeof(RectTransform));
        arrowRoot.transform.SetParent(root, false);
        RectTransform arrowRect = arrowRoot.GetComponent<RectTransform>();
        UITheme.Fill(arrowRect);

        // Shaft and head live in their own full-screen rects so their vertex coordinates are
        // the same root-local space the spotlight geometry is computed in.
        GameObject shaft = new GameObject("Arrow Shaft", typeof(RectTransform));
        shaft.transform.SetParent(arrowRect, false);
        UITheme.Fill(shaft.GetComponent<RectTransform>());
        arrowShaft = shaft.AddComponent<UIGraphLine>();
        arrowShaft.Thickness = 3.2f;
        arrowShaft.color = UITheme.Hex("60A5FA");
        arrowShaft.raycastTarget = false;

        GameObject head = new GameObject("Arrow Head", typeof(RectTransform));
        head.transform.SetParent(arrowRect, false);
        UITheme.Fill(head.GetComponent<RectTransform>());
        arrowHead = head.AddComponent<TutorialArrowHead>();
        arrowHead.color = UITheme.Hex("60A5FA");
        arrowHead.raycastTarget = false;

        // Caption as a white pill so it stays readable over the dimmed scene.
        Image pill = UITheme.Panel("Arrow Label", arrowRect, Color.white, 15f);
        arrowPill = pill.rectTransform;
        arrowLabel = UITheme.Label("Text", pill.transform, "", 13f, W.ExtraBold, UITheme.Accent, TextAnchor.MiddleCenter);
        UITheme.Fill(arrowLabel.rectTransform);

        arrowRoot.SetActive(false);
    }

    private void BuildCard()
    {
        card = UITheme.Card("Tutorial Card", root, 18f, Color.white, 48f, 18f, 0.35f);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CardWidth, 300f);

        Image logo = UITheme.Panel("Logo", card, UITheme.Accent, 9f);
        UITheme.TopLeft(logo.rectTransform, 24f, 22f, 30f, 30f);
        Image logoIcon = UITheme.IconImage("Icon", logo.transform, Icon.Logo, 18f, Color.white);
        UITheme.Center(logoIcon.rectTransform, 18f, 18f);

        cardCounter = UITheme.Label("Card Counter", card, "", 12f, W.ExtraBold, UITheme.Accent);
        UITheme.TopLeft(cardCounter.rectTransform, 64f, 20f, 200f, 16f);
        cardTitle = UITheme.Label("Card Title", card, "", 17f, W.ExtraBold, UITheme.Ink);
        UITheme.TopBand(cardTitle.rectTransform, 64f, 36f, 24f, 22f);

        progress = UITheme.ProgressBar("Progress", card, UITheme.Accent, 4f);
        UITheme.TopBand((RectTransform)progress.transform, 24f, 68f, 24f, 4f);

        cardBody = UITheme.Label("Card Body", card, "", 13.5f, W.Medium, UITheme.Ink2, TextAnchor.UpperLeft, true);
        cardBody.lineSpacing = 1.15f;
        UITheme.TopBand(cardBody.rectTransform, 24f, 86f, 24f, 160f);

        Button skip = UITheme.MakeButton("Skip Tutorial", card, "Skip tutorial", Kind.Ghost, 13.5f, null, 10f);
        float sw = UITheme.PreferredWidth(skip);
        UITheme.BottomLeft((RectTransform)skip.transform, 16f, 20f, sw, 42f);
        skip.onClick.AddListener(EndTutorial);

        nextButton = UITheme.MakeButton("Next Step", card, "Next", Kind.Primary, 14f, Icon.ChevronRight, 10f, true);
        UITheme.BottomRight((RectTransform)nextButton.transform, 24f, 20f, 124f, 42f);
        nextButton.onClick.AddListener(Next);

        previousButton = UITheme.MakeButton("Previous Step", card, "Previous", Kind.Secondary, 14f, Icon.ChevronLeft, 10f);
        UITheme.BottomRight((RectTransform)previousButton.transform, 158f, 20f, 124f, 42f);
        previousButton.onClick.AddListener(Previous);
    }

    private IEnumerator FadeOverlay(bool visible)
    {
        const float duration = 0.2f;
        if (visible) root.gameObject.SetActive(true);
        float from = canvasGroup.alpha;
        float to = visible ? 1f : 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
        if (!visible) root.gameObject.SetActive(false);
        fade = null;
    }
}

/// <summary>
/// Solid triangle used as the tutorial annotation arrow's head; the shaft reuses the project's
/// existing single-draw-call <see cref="UIGraphLine"/>.
///
/// Top-level rather than nested inside <see cref="TutorialRuntime"/> on purpose: Unity does not
/// reliably support MonoBehaviour-derived types declared as nested classes — AddComponent
/// succeeds but the graphic never issues its mesh.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialArrowHead : MaskableGraphic
{
    private Vector2 tip;
    private Vector2 direction = Vector2.up;
    private float size = 18f;

    public void SetArrow(Vector2 tipPoint, Vector2 dir, float headSize)
    {
        tip = tipPoint;
        direction = dir.sqrMagnitude < 1e-6f ? Vector2.up : dir.normalized;
        size = headSize;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 back = tip - direction * size;
        Vector2 side = new Vector2(-direction.y, direction.x) * (size * 0.52f);
        int idx = vh.currentVertCount;
        AddVert(vh, tip);
        AddVert(vh, back + side);
        AddVert(vh, back - side);
        vh.AddTriangle(idx, idx + 1, idx + 2);
    }

    private void AddVert(VertexHelper vh, Vector2 position)
    {
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = position;
        vh.AddVert(v);
    }
}
