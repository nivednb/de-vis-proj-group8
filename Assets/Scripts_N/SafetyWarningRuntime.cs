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
    private Image panelBorder;
    private Image iconImage;
    private Text titleText;
    private Text warningText;
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
        canvas.sortingOrder = 72;
        UITheme.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());

        // A banner centred just below the navigation bar, in the gap between the stream
        // legend and the plant-status card.
        panelRect = UITheme.Card("Warning Panel", canvasObject.transform, 20f, UITheme.DangerSoft2, 22f, 8f, 0.18f, false);
        UITheme.TopCenter(panelRect, 0f, 80f, 560f, 40f);
        panelRect.GetComponent<UIRaycastTarget>().raycastTarget = false;
        panelImage = panelRect.Find("Surface").GetComponent<Image>();
        panelBorder = UITheme.Border(panelRect, UITheme.WithAlpha(UITheme.Danger, 0.35f), 20f);

        iconImage = UITheme.IconImage("Icon", panelRect, UITheme.Icon.Alert, 18f, UITheme.DangerInk);
        UITheme.TopLeft(iconImage.rectTransform, 16f, 11f, 18f, 18f);
        titleText = UITheme.Label("Warning Title", panelRect, "Critical", 13f, UITheme.Weight.ExtraBold, UITheme.DangerInk);
        UITheme.TopLeft(titleText.rectTransform, 42f, 0f, 80f, 40f);
        warningText = UITheme.Label("Warning Text", panelRect, "", 13f, UITheme.Weight.SemiBold, UITheme.DangerInk);
        UITheme.TopLeft(warningText.rectTransform, 120f, 0f, 600f, 40f);

        panelRect.gameObject.SetActive(false);
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

        Color ink = hasAlarm ? UITheme.DangerInk : UITheme.WarningInk;
        panelImage.color = hasAlarm ? UITheme.DangerSoft2 : UITheme.WarningSoft;
        panelBorder.color = UITheme.WithAlpha(hasAlarm ? UITheme.Danger : UITheme.Warning, 0.4f);
        iconImage.color = ink;
        titleText.color = ink;
        warningText.color = ink;
        titleText.text = hasAlarm ? "Critical" : "Warning";

        List<string> active = hasAlarm ? alarms : cautions;
        int shown = Mathf.Min(active.Count, 2);
        var parts = new List<string>(shown);
        for (int i = 0; i < shown; i++) parts.Add(Readable(active[i]));
        string more = active.Count > shown ? $"   +{active.Count - shown} more" : "";
        warningText.text = UITheme.Pretty(string.Join("   ·   ", parts)) + more;

        // Size the banner to its message; past the cap (the gap between the stream legend and
        // the right-hand cards) the message wraps onto more lines instead.
        const float maxWidth = 620f;
        float titleW = Mathf.Ceil(titleText.preferredWidth);
        float textLeft = 42f + titleW + 10f;
        float maxTextWidth = maxWidth - textLeft - 20f;
        warningText.horizontalOverflow = HorizontalWrapMode.Overflow;
        float oneLine = Mathf.Ceil(warningText.preferredWidth);
        float width;
        float height = 40f;
        if (oneLine <= maxTextWidth)
        {
            width = textLeft + oneLine + 20f;
            UITheme.TopLeft(warningText.rectTransform, textLeft, 0f, oneLine + 4f, 40f);
        }
        else
        {
            width = maxWidth;
            warningText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UITheme.TopLeft(warningText.rectTransform, textLeft, 11f, maxTextWidth, 20f);
            float textHeight = Mathf.Ceil(warningText.preferredHeight);
            warningText.rectTransform.sizeDelta = new Vector2(maxTextWidth, textHeight);
            warningText.alignment = TextAnchor.UpperLeft;
            height = textHeight + 22f;
        }
        if (oneLine <= maxTextWidth) warningText.alignment = TextAnchor.MiddleLeft;
        UITheme.TopLeft(titleText.rectTransform, 42f, 0f, titleW + 4f, 40f);
        panelRect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>"REACTOR: low temperature …" → "Reactor: low temperature …".</summary>
    private static string Readable(string message)
    {
        int colon = message.IndexOf(':');
        if (colon <= 0) return message;
        string head = message.Substring(0, colon).ToLowerInvariant();
        head = char.ToUpperInvariant(head[0]) + head.Substring(1);
        return head.Replace("co2", "CO2").Replace("h2", "H2") + message.Substring(colon);
    }

    private void AddElectrolyzerWarnings(PlantProcessSimulator.ProcessSnapshot s, List<string> cautions)
    {
        if (s.plantLoadPercent <= 5f) return;

        if (s.waterFeedPercent < 45f && s.electrolyzerPowerPercent > 65f)
            cautions.Add("ELECTROLYZER: water feed limits H2 production");

        if (s.electrolyzerPowerPercent > 92f && s.waterFeedPercent < 90f)
            cautions.Add("ELECTROLYZER: high power requires adequate water feed");
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
            cautions.Add($"DESORBER: high regeneration temperature ({s.regeneratorTemperatureC:F0} °C)");
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
            alarms.Add($"REACTOR: temperature critical ({s.reactorTemperatureC:F0} °C)");
        else if (s.reactorTemperatureC > 270f)
            cautions.Add($"REACTOR: catalyst sintering risk ({s.reactorTemperatureC:F0} °C)");

        if (s.reactorTemperatureC <= 215f)
            cautions.Add($"REACTOR: low temperature limits reaction rate ({s.reactorTemperatureC:F0} °C)");

        if (s.reactorPressureBar < 60f)
            cautions.Add($"REACTOR: low conversion pressure ({s.reactorPressureBar:F0} bar)");

        if (s.h2Co2Ratio < 2.5f)
            cautions.Add($"REACTOR: hydrogen deficiency (H2/CO2 {s.h2Co2Ratio:F1})");
        else if (s.h2Co2Ratio > 4.5f)
            cautions.Add($"REACTOR: excess hydrogen feed (H2/CO2 {s.h2Co2Ratio:F1})");

        if (s.ghsv > 9500f)
            cautions.Add($"REACTOR: low residence time (GHSV {s.ghsv:F0} 1/h)");
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
        else if (s.recycleRatioPercent > 97f)
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
        if (s.storageInterlockActive)
            alarms.Add($"STORAGE HIGH-HIGH: production interlock active ({s.storageFillPercent:F0}%). Reset/unload storage to restart");
        else if (s.storageFillPercent >= 95f)
            alarms.Add($"STORAGE: methanol tank nearly full ({s.storageFillPercent:F0}%)");
        else if (s.storageFillPercent >= 85f)
            cautions.Add($"STORAGE: methanol tank level high ({s.storageFillPercent:F0}%)");
    }
}
