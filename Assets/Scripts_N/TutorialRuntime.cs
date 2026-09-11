using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Step-by-step guided tour of the whole application.
///
/// It runs automatically the first time the app is launched and can be replayed at any time
/// from HELP -> START TUTORIAL. Each step dims the screen except the part of the UI being
/// explained, and drives the dashboard into the matching state first, so the tour points at
/// the real controls rather than at a description of them.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialRuntime : MonoBehaviour
{
    private const string RuntimeRootName = "Generated Tutorial Overlay";
    private const string CompletedKey = "PtmDigitalTwin.TutorialCompleted";

    private static readonly Color DimColor = new Color(0.012f, 0.035f, 0.05f, 0.78f);
    private static readonly Color CardColor = new Color32(9, 29, 41, 252);
    private static readonly Color HeaderColor = new Color32(10, 24, 34, 255);
    private static readonly Color AccentColor = new Color32(20, 145, 205, 255);
    private static readonly Color MutedTextColor = new Color32(174, 195, 206, 255);
    private static readonly Color SkipColor = new Color32(90, 42, 42, 255);

    private const float CardWidth = 600f;
    private const float CardHeight = 280f;
    private const float HighlightPadding = 8f;
    private const float FrameThickness = 2f;
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
    }

    private readonly List<Step> steps = new List<Step>();
    private readonly Vector3[] worldCorners = new Vector3[4];

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Font font;
    private RectTransform root;
    private readonly RectTransform[] dimPanels = new RectTransform[4];
    private readonly RectTransform[] frameEdges = new RectTransform[4];
    private GameObject frameRoot;
    private RectTransform card;
    private Text cardTitle;
    private Text cardBody;
    private Text cardCounter;
    private RectTransform progressFill;
    private Button previousButton;
    private Button nextButton;
    private Text nextButtonLabel;

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
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildSteps();
        BuildOverlay();
        if (PlayerPrefs.GetInt(CompletedKey, 0) == 0) StartCoroutine(AutoStartWhenReady());
    }

    /// <summary>The dashboard builds itself in its own Start(), so the first-launch tour waits
    /// for that canvas to exist before it tries to spotlight anything on it.</summary>
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
        PlayerPrefs.SetInt(CompletedKey, 1);
        PlayerPrefs.Save();
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

    private void Add(string title, string body, string targetName = null, Rect fallback = default, Action apply = null)
    {
        steps.Add(new Step { Title = title, Body = body, TargetName = targetName, ViewportFallback = fallback, Apply = apply });
    }

    /// <summary>Roughly the equipment row of the 3D plant, used for the steps that talk about
    /// the scene itself rather than about a specific widget. Deliberately short enough that
    /// the explanation card still fits below it instead of being pushed over the spotlight.</summary>
    private static readonly Rect PlantArea = new Rect(0.21f, 0.42f, 0.58f, 0.32f);

    private void BuildSteps()
    {
        Add("WELCOME TO THE POWER-TO-METHANOL DIGITAL TWIN",
            "This short tour walks through every part of the application: the 3D plant, the six sections in the top bar, the equipment controls, the analytics window and the footer tools.\n\n" +
            "Use NEXT and PREVIOUS to move through the tour, or SKIP TUTORIAL to jump straight in. You can reopen it at any time from HELP -> START TUTORIAL.\n\n" +
            "Keyboard: Enter or Space = next, Backspace = previous, Esc = skip.",
            apply: () => Dashboard(d => d.TutorialRestoreDefaults()));

        Add("THE APPLICATION WINDOW",
            "The slim bar at the very top is the application's window chrome. The X on its right closes the application. Everything below it is the digital twin itself.",
            "App Title Bar");

        Add("THE MAIN NAVIGATION",
            "Six sections live in the header: OVERVIEW, PROCESS MAP, FLOW LAB, REACTOR LAB, ANALYTICS and SIMULATION. Selecting one opens its panel and moves the camera to a matching view, and the highlighted button always shows where you are.\n\n" +
            "HELP on the far right opens the about box, which is also where you can replay this tour.",
            "Header");

        Add("OVERVIEW - THE HOME VIEW",
            "OVERVIEW is the landing view: the full plant in 3D with the live status, the KPI strip and the stream legend arranged around it.\n\n" +
            "From any other section, clicking empty space in the 3D scene brings you straight back here.",
            "OVERVIEW",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("PLANT STATUS",
            "The panel on the right summarises the whole plant: a status light (normal operation, operating caution, attention required or paused), overall efficiency, methanol production in kg/h, CO2 utilisation, and the product-tank level with an estimate of how long it has left before it is full.",
            "Plant Status",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("ONE-CLICK BEST CASE",
            "SET MAXIMUM EFFICIENCY drives every module slider to the setpoint combination that gives the highest overall efficiency in this educational model, and resumes the run.\n\n" +
            "It is the quickest way to see the plant at its best before you start experimenting with the controls yourself.",
            "Set Maximum Efficiency",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("LIVE KPI STRIP",
            "The strip across the bottom of the scene reports the four process stages live:\n\n" +
            "ELECTROLYZER - power, hydrogen, water and oxygen\n" +
            "CO2 CAPTURE - capture efficiency, captured CO2, amine flow, regenerator temperature\n" +
            "REACTOR - temperature, pressure, H2/CO2 ratio, yield\n" +
            "SEPARATION - recycle, purity, storage, product rate",
            "Process KPI Strip",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("STREAM LEGEND",
            "Every pipe in the plant is colour-coded by what it carries: water and hydrogen, amine and captured CO2, compressed syngas, hot reactor effluent, refined methanol, and the dashed gas recycle loop.\n\n" +
            "Packet speed and density inside the pipes follow the calculated flow rates, so the animation is driven by the process model rather than looping at a fixed speed.",
            "Process Flow Legend",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("THE 3D PLANT AND THE CAMERA",
            "The plant itself is fully navigable:\n\n" +
            "Arrow keys - orbit around the plant\n" +
            "A / D - pan left and right\n" +
            "W / S - zoom in and out\n" +
            "Shift + arrow keys - step through the modules one by one\n" +
            "Home - return to the full plant overview\n\n" +
            "You can also drag with the mouse to look around and scroll to zoom.",
            null, PlantArea,
            () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("EQUIPMENT INFO AND CONTROLS",
            "Hover over any piece of equipment and a small 'i' button appears on it. Click that button to open the module's own panel with live readings and its operating sliders.\n\n" +
            "Those sliders are the real inputs to the process model: moving one updates the KPIs, the warnings, the tank level and the pipe animation together.",
            null, PlantArea,
            () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("PROCESS MAP",
            "PROCESS MAP is a guided ten-step walkthrough, from renewable power and water all the way to methanol storage.\n\n" +
            "NEXT STEP and PREVIOUS STEP move through it, the panel explains each stage and lists the streams involved, and the camera flies to the matching equipment automatically.",
            "Guided Process",
            apply: () => Dashboard(d => d.TutorialShowPage("process")));

        Add("FLOW LAB",
            "FLOW LAB filters the pipe network by subsystem: ALL STREAMS, FEED GASES, CAPTURE LOOP, SYNTHESIS LOOP or PRODUCT PATH. Only the selected routes keep their moving packets, which makes a single loop easy to follow through the plant.\n\n" +
            "SHOW / HIDE STREAM VISUALS switches the animated packets off entirely so you can inspect the bare plant.",
            "Flow Lab",
            apply: () => Dashboard(d => d.TutorialShowPage("flow")));

        Add("REACTOR LAB",
            "REACTOR LAB focuses the transparent fixed-bed methanol reactor. FOCUS REACTOR re-frames the camera on it and OPEN REACTOR CONTROLS brings up its sliders.\n\n" +
            "The panel shows live temperature, pressure, H2/CO2 ratio and yield. Inside the reactor, the catalyst-bed colour tracks the operating state and the moving particles represent the species and the conversion.",
            "Reactor Lab",
            apply: () => Dashboard(d => d.TutorialShowPage("reactor")));

        Add("SIMULATION",
            "SIMULATION summarises plant load, syngas feed, methanol output and storage, and tells you when the storage interlock is throttling production upstream.\n\n" +
            "Use it alongside the module sliders - those remain the single source of truth for the calculation, and this panel reports what they add up to.",
            "Simulation",
            apply: () => Dashboard(d => d.TutorialShowPage("simulation")));

        Add("ANALYTICS - STATS",
            "ANALYTICS opens a window you can drag anywhere by its title bar. PAUSE and RESET sit in that title bar, so you can control the run without closing the window.\n\n" +
            "The STATS tab shows the five headline metrics as bars: overall efficiency, CO2 capture, reactor yield, methanol purity and storage fill. Hover any bar to read its exact live value.",
            "Analytics Window",
            apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, null)));

        Add("ANALYTICS - VISUALISE",
            "The VISUALISE tab holds four graph views:\n\n" +
            "REACTOR YIELD - yield against one reactor parameter\n" +
            "EFFICIENCY - overall efficiency against one reactor parameter\n" +
            "OFAT TIMELINE - a guided one-factor-at-a-time study\n" +
            "LIVE PROGRESS - strip charts of the run as it happens",
            "Visualise Sub Tabs",
            apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, "yield")));

        Add("CORRELATION GRAPHS AND THE VARIABLE LOCK",
            "REACTOR YIELD and EFFICIENCY plot the response against one reactor parameter at a time: Temp, Pressure, H2:CO2, GHSV or Feed.\n\n" +
            "Picking a parameter here locks every other reactor slider in place. That way the curve you build up is a clean one-factor-at-a-time scan instead of a tangle of several variables moving at once.",
            "Reactor Yield Param Row",
            apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, "yield")));

        Add("OFAT TIMELINE",
            "OFAT TIMELINE records a guided one-factor-at-a-time study against time. Pick the variable to vary under VARY ONE, choose the response under SHOW (YIELD, EFFICIENCY or METHANOL), then move that slider while the rest stay locked.\n\n" +
            "LINE ONLY and WITH POINTS change the plot style, PREV / NEXT / LIVE page through the recorded timeline, and EXPORT writes the series out to a file.",
            "OFAT Timeline Sub Panel",
            apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, "ofat")));

        Add("LIVE PROGRESS",
            "LIVE PROGRESS strip-charts the run as it happens. EFFICIENCY and METHANOL OUTPUT switch between the two charts, and both keep recording in the background so switching never loses history.\n\n" +
            "The output chart also reports the cumulative amount already in the tank when you hover it.",
            "Live Sub Panel",
            apply: () => Dashboard(d => d.TutorialSetAnalyticsView(true, "live")));

        Add("FOOTER TOOLS",
            "The footer is available from every section:\n\n" +
            "PAUSE / RESUME and RESET control the run\n" +
            "VIEW INFORMATION opens the about box\n" +
            "SHOW STREAMS toggles the pipe animation\n" +
            "PREVIOUS MODULE and NEXT MODULE step the camera through the equipment\n" +
            "RESET VIEW returns to the full plant overview",
            "Footer",
            apply: () => Dashboard(d =>
            {
                d.TutorialSetAnalyticsView(false, null);
                d.TutorialShowPage("overview");
            }));

        Add("SAFETY AND EFFICIENCY WARNINGS",
            "When an operating condition drifts out of the safe or sensible range - reactor temperature or pressure too high, storage nearly full, capture efficiency too low - a red warning band appears here listing the active alarms and cautions.\n\n" +
            "It clears itself as soon as conditions recover, so it is worth watching while you experiment with the sliders.",
            // No viewport fallback on purpose: when the plant is running cleanly the band is
            // hidden, and spotlighting the empty strip it would occupy reads as a mistake.
            "Warning Panel",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("HELP AND THIS TOUR",
            "HELP opens the about box, with a summary of the controls and the educational disclaimer.\n\n" +
            "START TUTORIAL in that box replays this tour whenever you want it, so nothing here is a one-time explanation.",
            "Help",
            apply: () => Dashboard(d => d.TutorialShowPage("overview")));

        Add("YOU ARE READY",
            "That is the whole application: the 3D plant and its camera, the six header sections, the equipment sliders, the analytics window and the footer tools.\n\n" +
            "One reminder before you start: every number and animation here is a simplified educational representation. This is not CFD, Aspen, industrial control software, or a validated process model.\n\n" +
            "Press FINISH to start exploring.");
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
        cardCounter.text = $"STEP {index + 1} OF {steps.Count}";
        progressFill.anchorMax = new Vector2((index + 1) / (float)steps.Count, 1f);
        progressFill.offsetMax = Vector2.zero;

        bool first = index == 0;
        previousButton.interactable = !first;
        previousButton.GetComponent<Image>().color = first ? new Color32(24, 44, 56, 255) : HeaderColor;
        bool last = index == steps.Count - 1;
        nextButtonLabel.text = last ? "FINISH" : "NEXT";

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
            SetLocalRect(frameEdges[0], Rect.MinMaxRect(spot.xMin - t, spot.yMax, spot.xMax + t, spot.yMax + t));
            SetLocalRect(frameEdges[1], Rect.MinMaxRect(spot.xMin - t, spot.yMin - t, spot.xMax + t, spot.yMin));
            SetLocalRect(frameEdges[2], Rect.MinMaxRect(spot.xMin - t, spot.yMin, spot.xMin, spot.yMax));
            SetLocalRect(frameEdges[3], Rect.MinMaxRect(spot.xMax, spot.yMin, spot.xMax + t, spot.yMax));
        }
        else
        {
            SetLocalRect(dimPanels[0], area);
            for (int i = 1; i < dimPanels.Length; i++) SetLocalRect(dimPanels[i], Rect.zero);
        }

        if (frameRoot.activeSelf != highlighted) frameRoot.SetActive(highlighted);
        PlaceCard(area, highlighted, spot);
    }

    private bool TryGetHighlight(Rect area, out Rect result)
    {
        Step step = steps[index];
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy &&
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

        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;
        float x = Mathf.Clamp(spot.center.x, area.xMin + halfW + CardMargin, area.xMax - halfW - CardMargin);

        float topIfBelow = spot.yMin - CardMargin;
        if (topIfBelow - size.y >= area.yMin + CardMargin)
        {
            card.anchoredPosition = new Vector2(x, topIfBelow - halfH);
            return;
        }

        float bottomIfAbove = spot.yMax + CardMargin;
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
        // Above the dashboard (90), the module panels (75) and the warning band (72) so the
        // tour always sits on top of whatever it is explaining.
        canvas.sortingOrder = 300;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1536f, 1024f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject rootObject = new GameObject("Tutorial Root");
        rootObject.transform.SetParent(canvasObject.transform, false);
        root = rootObject.AddComponent<RectTransform>();
        Pin(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        canvasGroup = rootObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // A full-screen, almost invisible blocker keeps the tour modal: the application
        // underneath cannot be clicked out from under the step that is describing it.
        RectTransform blocker = CreatePanel("Input Blocker", root, new Color(0f, 0f, 0f, 0.004f));
        Pin(blocker, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        for (int i = 0; i < dimPanels.Length; i++)
        {
            dimPanels[i] = CreatePanel("Dim " + i, root, DimColor);
            dimPanels[i].GetComponent<Image>().raycastTarget = true;
        }

        frameRoot = new GameObject("Highlight Frame", typeof(RectTransform));
        frameRoot.transform.SetParent(root, false);
        RectTransform frameRect = frameRoot.GetComponent<RectTransform>();
        Pin(frameRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        for (int i = 0; i < frameEdges.Length; i++)
        {
            frameEdges[i] = CreatePanel("Edge " + i, frameRect, AccentColor);
            frameEdges[i].GetComponent<Image>().raycastTarget = false;
        }

        BuildCard();
        rootObject.SetActive(false);
    }

    private void BuildCard()
    {
        card = CreatePanel("Tutorial Card", root, CardColor);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CardWidth, CardHeight);
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        cardTitle = CreateText("Card Title", card, "", 15, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        Pin(cardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -48f), new Vector2(-150f, -16f));

        cardCounter = CreateText("Card Counter", card, "", 11, FontStyle.Bold, TextAnchor.UpperRight, MutedTextColor);
        Pin(cardCounter.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-142f, -34f), new Vector2(-22f, -16f));

        RectTransform track = CreatePanel("Progress Track", card, new Color32(28, 54, 68, 255));
        Pin(track, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -58f), new Vector2(-22f, -54f));
        progressFill = CreatePanel("Progress Fill", track, AccentColor);
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        progressFill.GetComponent<Image>().raycastTarget = false;

        cardBody = CreateText("Card Body", card, "", 13, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        cardBody.verticalOverflow = VerticalWrapMode.Overflow;
        Pin(cardBody.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 62f), new Vector2(-22f, -66f));

        Button skip = CreateButton("Skip Tutorial", card, "SKIP TUTORIAL", SkipColor, 11);
        RectTransform skipRect = skip.GetComponent<RectTransform>();
        skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = Vector2.zero;
        skipRect.anchoredPosition = new Vector2(22f, 18f);
        skipRect.sizeDelta = new Vector2(160f, 34f);
        skip.onClick.AddListener(EndTutorial);

        nextButton = CreateButton("Next Step", card, "NEXT", AccentColor, 11);
        RectTransform nextRect = nextButton.GetComponent<RectTransform>();
        nextRect.anchorMin = nextRect.anchorMax = nextRect.pivot = new Vector2(1f, 0f);
        nextRect.anchoredPosition = new Vector2(-22f, 18f);
        nextRect.sizeDelta = new Vector2(132f, 34f);
        nextButton.onClick.AddListener(Next);
        nextButtonLabel = nextButton.GetComponentInChildren<Text>();

        previousButton = CreateButton("Previous Step", card, "PREVIOUS", HeaderColor, 11);
        RectTransform prevRect = previousButton.GetComponent<RectTransform>();
        prevRect.anchorMin = prevRect.anchorMax = prevRect.pivot = new Vector2(1f, 0f);
        prevRect.anchoredPosition = new Vector2(-162f, 18f);
        prevRect.sizeDelta = new Vector2(132f, 34f);
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

    // ---- small UI helpers ---------------------------------------------------

    private RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
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
        text.lineSpacing = 1.05f;
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
        colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        button.colors = colors;
        Text text = CreateText("Label", rect, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Pin(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 4f), new Vector2(-5f, -4f));
        return button;
    }

    private static void Pin(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
