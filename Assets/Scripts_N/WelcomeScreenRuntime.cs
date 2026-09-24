using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Title screen shown once when the application launches.
///
/// It covers the whole window and holds the process simulator paused until START is pressed;
/// START then resumes the simulation and opens the guided tutorial. While it is up it also
/// sits above the tutorial overlay, which waits for <see cref="IsShowing"/> to clear before
/// auto-starting (see <see cref="TutorialRuntime"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class WelcomeScreenRuntime : MonoBehaviour
{
    private const string RuntimeRootName = "Generated Welcome Screen";

    private static readonly Color BackdropColor = new Color(0.012f, 0.035f, 0.05f, 0.94f);
    private static readonly Color CardColor = new Color32(9, 29, 41, 252);
    private static readonly Color AccentColor = new Color32(20, 145, 205, 255);
    private static readonly Color MutedTextColor = new Color32(174, 195, 206, 255);

    public static WelcomeScreenRuntime Instance { get; private set; }

    /// <summary>True from launch until START has been pressed.</summary>
    public static bool IsShowing => Instance != null && Instance.showing;

    private bool showing = true;
    private Font font;
    private GameObject canvasObject;
    private CanvasGroup canvasGroup;
    private RectTransform startButtonRect;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<WelcomeScreenRuntime>() != null) return;
        new GameObject(RuntimeRootName).AddComponent<WelcomeScreenRuntime>();
    }

    private void Awake()
    {
        Instance = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Build();
    }

    private void Start()
    {
        StartCoroutine(HoldSimulatorPaused());
    }

    /// <summary>The simulator may come up a frame or two after this screen, so keep it paused
    /// for as long as the screen is showing rather than pausing it once.</summary>
    private IEnumerator HoldSimulatorPaused()
    {
        while (showing)
        {
            PlantProcessSimulator sim = PlantProcessSimulator.Instance;
            if (sim != null && sim.IsRunning) sim.Pause();
            yield return null;
        }
    }

    private void Update()
    {
        if (!showing) return;

        // Gentle pulse on the START button so it reads as the one thing to do here.
        if (startButtonRect != null)
        {
            float pulse = 1f + 0.035f * Mathf.Sin(Time.unscaledTime * 3f);
            startButtonRect.localScale = new Vector3(pulse, pulse, 1f);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                                 keyboard.spaceKey.wasPressedThisFrame))
            Begin();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Dismisses the screen, starts the simulation and hands over to the tutorial.</summary>
    public void Begin()
    {
        if (!showing) return;
        showing = false;
        PlantProcessSimulator.Instance?.Play();
        StartCoroutine(FadeOutAndStartTutorial());
    }

    private IEnumerator FadeOutAndStartTutorial()
    {
        const float duration = 0.35f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / duration);
            yield return null;
        }
        Destroy(canvasObject);

        // The dashboard builds itself in its own Start(); the tutorial spotlights it, so wait
        // for it just as the tutorial's own auto-start does.
        float deadline = Time.unscaledTime + 8f;
        while (Time.unscaledTime < deadline)
        {
            IcodosDashboardRuntime dashboard = IcodosDashboardRuntime.Instance;
            if (dashboard != null && dashboard.IsBuilt) break;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.2f);
        TutorialRuntime.Instance?.StartTutorial();
    }

    // ---- construction -------------------------------------------------------

    private void Build()
    {
        canvasObject = new GameObject("Welcome Canvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the tutorial (300) and everything under it, below only the probe cursor (1000).
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1536f, 1024f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();

        // Full-screen backdrop: blocks every click to the dashboard and the 3D scene, while
        // still letting the plant show faintly through.
        RectTransform backdrop = CreatePanel("Backdrop", canvasObject.transform, BackdropColor);
        Pin(backdrop, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform card = CreatePanel("Welcome Card", backdrop, CardColor);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(720f, 420f);
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        RectTransform accentBar = CreatePanel("Accent Bar", card, AccentColor);
        Pin(accentBar, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -5f), Vector2.zero);

        Text eyebrow = CreateText("Eyebrow", card, "INTERACTIVE PROCESS SIMULATION", 13, FontStyle.Bold, TextAnchor.MiddleCenter, AccentColor);
        Pin(eyebrow.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(30f, -72f), new Vector2(-30f, -44f));

        Text title = CreateText("Title", card, "POWER-TO-METHANOL\nDIGITAL TWIN", 38, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        title.lineSpacing = 1f;
        Pin(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(30f, -186f), new Vector2(-30f, -78f));

        Text subtitle = CreateText("Subtitle", card,
            "Follow renewable electricity, water and captured CO2 through electrolysis, synthesis and distillation to refined methanol - and see how every setting shapes the result.",
            15, FontStyle.Normal, TextAnchor.UpperCenter, MutedTextColor);
        Pin(subtitle.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(70f, -262f), new Vector2(-70f, -200f));

        Button start = CreateButton("Start Button", card, "START", AccentColor, 20);
        startButtonRect = start.GetComponent<RectTransform>();
        startButtonRect.anchorMin = startButtonRect.anchorMax = startButtonRect.pivot = new Vector2(0.5f, 0f);
        startButtonRect.anchoredPosition = new Vector2(0f, 70f);
        startButtonRect.sizeDelta = new Vector2(240f, 56f);
        start.onClick.AddListener(Begin);

        Text hint = CreateText("Hint", card, "Press START (or Enter) to begin - a short guided tour opens first.",
            12, FontStyle.Italic, TextAnchor.MiddleCenter, MutedTextColor);
        Pin(hint.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 26f), new Vector2(-30f, 50f));
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
        text.lineSpacing = 1.1f;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
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
        button.colors = colors;
        Text text = CreateText("Label", rect, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Pin(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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
