using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime warning overlay for unsafe or inefficient operating conditions.
/// Auto-created on Play, so no scene edits are required.
/// </summary>
[DisallowMultipleComponent]
public class SafetyWarningRuntime : MonoBehaviour
{
    private const string RuntimeRootName = "Generated Safety Warning Overlay";

    [SerializeField] private float updateInterval = 0.25f;

    private Canvas canvas;
    private RectTransform panelRect;
    private Image panelImage;
    private Text warningText;
    private Font font;
    private float nextUpdateTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<SafetyWarningRuntime>() != null)
        {
            return;
        }

        GameObject go = new GameObject(RuntimeRootName);
        go.AddComponent<SafetyWarningRuntime>();
    }

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildOverlay();
    }

    private void Update()
    {
        if (Time.time < nextUpdateTime)
        {
            return;
        }

        nextUpdateTime = Time.time + updateInterval;
        RefreshWarnings();
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("Safety Warning Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Warning Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -18f);
        panelRect.sizeDelta = new Vector2(720f, 92f);
        panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.12f, 0.04f, 0.02f, 0.92f);

        GameObject textObject = new GameObject("Warning Text");
        textObject.transform.SetParent(panel.transform, false);
        warningText = textObject.AddComponent<Text>();
        warningText.font = font;
        warningText.fontSize = 18;
        warningText.fontStyle = FontStyle.Bold;
        warningText.alignment = TextAnchor.MiddleCenter;
        warningText.horizontalOverflow = HorizontalWrapMode.Wrap;
        warningText.verticalOverflow = VerticalWrapMode.Truncate;
        warningText.color = Color.white;
        RectTransform textRect = warningText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 10f);
        textRect.offsetMax = new Vector2(-18f, -10f);

        panel.SetActive(false);
    }

    private void RefreshWarnings()
    {
        if (panelRect == null || PlantProcessSimulator.Instance == null)
        {
            return;
        }

        PlantProcessSimulator.ProcessSnapshot s = PlantProcessSimulator.Instance.Current;
        List<string> alarms = new List<string>();
        List<string> cautions = new List<string>();

        if (s.reactorTemperatureC >= 285f)
            alarms.Add($"Reactor temperature critical: {s.reactorTemperatureC:F0} C");
        else if (s.reactorTemperatureC >= 270f)
            cautions.Add($"High reactor temperature: {s.reactorTemperatureC:F0} C");

        if (s.reactorTemperatureC <= 215f && s.plantLoadPercent > 5f)
            cautions.Add($"Low reactor temperature limits conversion: {s.reactorTemperatureC:F0} C");

        if (s.reactorPressureBar >= 95f)
            alarms.Add($"Reactor pressure critical: {s.reactorPressureBar:F0} bar");
        else if (s.reactorPressureBar >= 85f)
            cautions.Add($"High reactor pressure: {s.reactorPressureBar:F0} bar");
        else if (s.reactorPressureBar < 60f && s.plantLoadPercent > 5f)
            cautions.Add($"Low conversion pressure: {s.reactorPressureBar:F0} bar");

        if (s.h2Co2Ratio < 2.5f)
            cautions.Add($"Hydrogen deficiency: H2/CO2 {s.h2Co2Ratio:F1}");
        else if (s.h2Co2Ratio > 3.6f)
            cautions.Add($"H2/CO2 ratio off target: {s.h2Co2Ratio:F1}");

        if (s.ghsv > 9500f)
            cautions.Add($"High GHSV reduces residence time: {s.ghsv:F0} h-1");

        if (s.regeneratorTemperatureC >= 124f)
            cautions.Add($"Regenerator temperature high: {s.regeneratorTemperatureC:F0} C");

        if (s.coolingWaterTemperatureC >= 38f || s.coolingWaterFlowPercent <= 20f)
            cautions.Add("Condenser cooling is weak; methanol recovery may drop");

        if (s.distillationReboilerTemperatureC >= 108f)
            cautions.Add($"Distillation reboiler energy high: {s.distillationReboilerTemperatureC:F0} C");

        if (s.storageFillPercent >= 95f)
            alarms.Add($"Methanol tank nearly full: {s.storageFillPercent:F0}%");
        else if (s.storageFillPercent >= 85f)
            cautions.Add($"Methanol storage high: {s.storageFillPercent:F0}%");

        bool hasAlarm = alarms.Count > 0;
        bool hasWarning = hasAlarm || cautions.Count > 0;
        panelRect.gameObject.SetActive(hasWarning);

        if (!hasWarning)
        {
            return;
        }

        panelImage.color = hasAlarm
            ? new Color(0.65f, 0.03f, 0.02f, 0.94f)
            : new Color(0.95f, 0.55f, 0.06f, 0.94f);

        List<string> active = hasAlarm ? alarms : cautions;
        string prefix = hasAlarm ? "CRITICAL WARNING" : "PROCESS WARNING";
        warningText.text = $"{prefix}: {string.Join("  |  ", active.GetRange(0, Mathf.Min(active.Count, 3)))}";
    }
}
