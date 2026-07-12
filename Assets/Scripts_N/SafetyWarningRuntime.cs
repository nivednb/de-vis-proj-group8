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
        canvas.sortingOrder = 68;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        GameObject panel = new GameObject("Warning Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(18f, -18f);
        panelRect.sizeDelta = new Vector2(520f, 58f);
        panelImage = panel.AddComponent<Image>();
        panelImage.raycastTarget = false;
        panelImage.color = new Color(0.12f, 0.04f, 0.02f, 0.82f);

        GameObject textObject = new GameObject("Warning Text");
        textObject.transform.SetParent(panel.transform, false);
        warningText = textObject.AddComponent<Text>();
        warningText.font = font;
        warningText.fontSize = 13;
        warningText.fontStyle = FontStyle.Bold;
        warningText.alignment = TextAnchor.MiddleLeft;
        warningText.horizontalOverflow = HorizontalWrapMode.Wrap;
        warningText.verticalOverflow = VerticalWrapMode.Truncate;
        warningText.raycastTarget = false;
        warningText.color = Color.white;
        RectTransform textRect = warningText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 8f);
        textRect.offsetMax = new Vector2(-14f, -8f);

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

        AddElectrolyzerWarnings(s, cautions);
        AddAbsorberWarnings(s, cautions);
        AddDesorberWarnings(s, cautions);
        AddCompressorWarnings(s, alarms, cautions);
        AddReactorWarnings(s, alarms, cautions);
        AddCondenserWarnings(s, cautions);
        AddSeparatorRecycleWarnings(s, cautions);
        AddDistillationWarnings(s, cautions);
        AddStorageWarnings(s, alarms, cautions);

        bool hasAlarm = alarms.Count > 0;
        bool hasWarning = hasAlarm || cautions.Count > 0;
        panelRect.gameObject.SetActive(hasWarning);

        if (!hasWarning)
        {
            return;
        }

        panelImage.color = hasAlarm
            ? new Color(0.65f, 0.03f, 0.02f, 0.84f)
            : new Color(0.95f, 0.55f, 0.06f, 0.82f);

        List<string> active = hasAlarm ? alarms : cautions;
        string prefix = hasAlarm ? "CRITICAL" : "WARNING";
        warningText.text = $"{prefix}: {string.Join("  |  ", active.GetRange(0, Mathf.Min(active.Count, 2)))}";
    }

    private void AddElectrolyzerWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> cautions)
    {
        if (s.plantLoadPercent <= 5f) return;

        if (s.waterFeedPercent < 45f && s.electrolyzerPowerPercent > 65f)
            cautions.Add("ELECTROLYZER: water feed limits H2 production");

        if (s.electrolyzerPowerPercent > 92f)
            cautions.Add("ELECTROLYZER: high power increases H2/O2 generation");
    }

    private void AddAbsorberWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> cautions)
    {
        if (s.plantLoadPercent <= 5f) return;

        if (s.captureEfficiencyPercent < 65f)
            cautions.Add($"ABSORBER: CO2 capture efficiency low ({s.captureEfficiencyPercent:F0}%)");

        if (s.flueGasFlowPercent > 115f && s.amineFlowPercent < 70f)
            cautions.Add("ABSORBER: flue gas exceeds solvent capacity");
    }

    private void AddDesorberWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> cautions)
    {
        if (s.plantLoadPercent <= 5f) return;

        if (s.regeneratorSteamPercent < 35f || s.regeneratorTemperatureC < 92f)
            cautions.Add("DESORBER: weak regeneration leaves amine partially loaded");

        if (s.regeneratorTemperatureC >= 124f)
            cautions.Add($"DESORBER: high regeneration temperature ({s.regeneratorTemperatureC:F0} C)");
    }

    private void AddCompressorWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> alarms, List<string> cautions)
    {
        if (s.reactorPressureBar >= 98f)
            alarms.Add($"COMPRESSOR: overpressure risk ({s.reactorPressureBar:F0} bar)");
        else if (s.reactorPressureBar >= 90f)
            cautions.Add($"COMPRESSOR: high outlet pressure ({s.reactorPressureBar:F0} bar)");
    }

    private void AddReactorWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> alarms, List<string> cautions)
    {
        if (s.plantLoadPercent <= 5f) return;

        if (s.reactorTemperatureC >= 285f)
            alarms.Add($"REACTOR: temperature critical ({s.reactorTemperatureC:F0} C)");
        else if (s.reactorTemperatureC > 270f)
            cautions.Add($"REACTOR: catalyst sintering risk ({s.reactorTemperatureC:F0} C)");

        if (s.reactorTemperatureC <= 215f)
            cautions.Add($"REACTOR: low temperature limits reaction rate ({s.reactorTemperatureC:F0} C)");

        if (s.reactorPressureBar < 60f)
            cautions.Add($"REACTOR: low conversion pressure ({s.reactorPressureBar:F0} bar)");

        if (s.h2Co2Ratio < 2.5f)
            cautions.Add($"REACTOR: hydrogen deficiency (H2/CO2 {s.h2Co2Ratio:F1})");
        else if (s.h2Co2Ratio > 4.5f)
            cautions.Add($"REACTOR: excess hydrogen feed (H2/CO2 {s.h2Co2Ratio:F1})");

        if (s.ghsv > 9500f)
            cautions.Add($"REACTOR: low residence time (GHSV {s.ghsv:F0} h-1)");
    }

    private void AddCondenserWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> cautions)
    {
        if (s.plantLoadPercent <= 5f) return;

        if (s.coolingWaterTemperatureC >= 38f || s.coolingWaterFlowPercent <= 20f)
            cautions.Add("CONDENSER: cooling weak, methanol recovery may drop");

        if (s.condenserRecoveryPercent < 70f)
            cautions.Add($"CONDENSER: low condensation recovery ({s.condenserRecoveryPercent:F0}%)");
    }

    private void AddSeparatorRecycleWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> cautions)
    {
        if (s.plantLoadPercent <= 5f) return;

        if (s.recycleRatioPercent < 25f)
            cautions.Add("SEPARATOR: low recycle reduces overall conversion");
        else if (s.recycleRatioPercent > 90f)
            cautions.Add("RECYCLE LOOP: very high recycle increases compressor load");
    }

    private void AddDistillationWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> cautions)
    {
        if (s.plantLoadPercent <= 5f) return;

        if (s.methanolPurityPercent < 95f)
            cautions.Add($"DISTILLATION: methanol purity below target ({s.methanolPurityPercent:F1}%)");

        if (s.refluxRatio > 4.2f || s.distillationReboilerTemperatureC >= 108f)
            cautions.Add("DISTILLATION: high separation energy demand");
    }

    private void AddStorageWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> alarms, List<string> cautions)
    {
        if (s.storageFillPercent >= 95f)
            alarms.Add($"STORAGE: methanol tank nearly full ({s.storageFillPercent:F0}%)");
        else if (s.storageFillPercent >= 85f)
            cautions.Add($"STORAGE: methanol tank level high ({s.storageFillPercent:F0}%)");
    }
}
