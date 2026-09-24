using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Icon = UITheme.Icon;
using Kind = UITheme.ButtonKind;
using W = UITheme.Weight;

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

    public static WelcomeScreenRuntime Instance { get; private set; }

    /// <summary>True from launch until START has been pressed.</summary>
    public static bool IsShowing => Instance != null && Instance.showing;

    private bool showing = true;
    private GameObject canvasObject;
    private CanvasGroup canvasGroup;
    private RectTransform card;
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
            float pulse = 1f + 0.02f * Mathf.Sin(Time.unscaledTime * 3f);
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
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            canvasGroup.alpha = 1f - p;
            if (card != null) card.localScale = Vector3.one * Mathf.Lerp(1f, 0.97f, p);
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
        UITheme.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());
        canvasObject.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();

        // Full-screen backdrop: blocks every click to the dashboard and the 3D scene, while
        // still letting the plant show softly through.
        Image backdrop = UITheme.ScrimLayer("Backdrop", canvasObject.transform, new Color(0.93f, 0.95f, 0.97f, 0.72f));

        const float width = 620f;
        card = UITheme.Card("Welcome Card", backdrop.transform, 22f, Color.white, 56f, 22f, 0.28f);
        UITheme.Center(card, width, 486f);

        Image logo = UITheme.Panel("Logo", card, UITheme.Accent, 16f);
        UITheme.TopCenter(logo.rectTransform, 0f, 40f, 60f, 60f);
        Image logoIcon = UITheme.IconImage("Icon", logo.transform, Icon.Logo, 36f, Color.white);
        UITheme.Center(logoIcon.rectTransform, 36f, 36f);

        Text eyebrow = UITheme.Label("Eyebrow", card, "Interactive process simulation", 13f, W.ExtraBold, UITheme.Accent, TextAnchor.MiddleCenter);
        UITheme.TopBand(eyebrow.rectTransform, 30f, 118f, 30f, 18f);

        Text title = UITheme.Label("Title", card, "Power-to-Methanol Digital Twin", 32f, W.ExtraBold, UITheme.Ink, TextAnchor.MiddleCenter);
        UITheme.TopBand(title.rectTransform, 30f, 142f, 30f, 42f);

        Text subtitle = UITheme.Label("Subtitle", card,
            "Follow renewable electricity, water and captured CO₂ through electrolysis, synthesis and distillation to refined methanol — and see how every setting shapes the result.",
            15f, W.Medium, UITheme.Muted, TextAnchor.UpperCenter, true);
        subtitle.lineSpacing = 1.15f;
        UITheme.TopBand(subtitle.rectTransform, 64f, 196f, 64f, 48f);

        // What the application offers, as three small chips.
        string[] features = { "Live process model", "Animated flow lab", "Analytics & export" };
        Icon[] icons = { Icon.Sliders, Icon.Waves, Icon.Chart };
        RectTransform chipRow = UITheme.NewRect("Features", card);
        UITheme.TopBand(chipRow, 0f, 284f, 0f, 32f);
        float total = 0f;
        var chips = new RectTransform[features.Length];
        for (int i = 0; i < features.Length; i++)
        {
            Image chip = UITheme.Panel(features[i], chipRow, UITheme.Sunken, 16f);
            Image icon = UITheme.IconImage("Icon", chip.transform, icons[i], 15f, UITheme.Accent);
            UITheme.TopLeft(icon.rectTransform, 12f, 8.5f, 15f, 15f);
            Text label = UITheme.Label("Label", chip.transform, features[i], 12.5f, W.Bold, UITheme.Ink2);
            UITheme.TopLeft(label.rectTransform, 33f, 0f, 200f, 32f);
            float w = 33f + label.preferredWidth + 14f;
            chip.rectTransform.sizeDelta = new Vector2(w, 32f);
            chips[i] = chip.rectTransform;
            total += w;
        }
        total += (features.Length - 1) * 8f;
        float x = -total * 0.5f;
        foreach (RectTransform chip in chips)
        {
            chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 1f);
            chip.pivot = new Vector2(0f, 1f);
            chip.anchoredPosition = new Vector2(x, 0f);
            x += chip.sizeDelta.x + 8f;
        }

        Button start = UITheme.MakeButton("Start Button", card, "Start", Kind.Primary, 17f, Icon.Play, 14f, false, 18f, W.ExtraBold, 20f);
        startButtonRect = (RectTransform)start.transform;
        UITheme.TopCenter(startButtonRect, 0f, 348f, 220f, 54f);
        start.onClick.AddListener(Begin);

        Text hint = UITheme.Label("Hint", card, "Press Start or Enter to begin — a short guided tour opens first.",
            12.5f, W.SemiBold, UITheme.Subtle, TextAnchor.MiddleCenter);
        UITheme.TopBand(hint.rectTransform, 30f, 420f, 30f, 18f);
    }
}
