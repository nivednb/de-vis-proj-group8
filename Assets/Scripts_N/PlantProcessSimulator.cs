using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight process-state model for the Power-to-Methanol scene.
///
/// The values are intentionally educational/approximate rather than a rigorous
/// chemical simulation. The goal is to connect every interactive module to a
/// visible consequence: particle flow intensity, oxygen byproduct, CO2 capture,
/// reactor yield, recycle flow, distillation quality, and methanol stored in the tank.
/// </summary>
[DisallowMultipleComponent]
public class PlantProcessSimulator : MonoBehaviour
{
    public static PlantProcessSimulator Instance { get; private set; }

    [Serializable]
    public struct ProcessSnapshot
    {
        public float timelinePercent;
        public float plantLoadPercent;

        public float electrolyzerPowerPercent;
        public float waterFeedPercent;
        public float waterFeedKgH;
        public float h2InputKgH;
        public float oxygenByproductKgH;

        public float flueGasFlowPercent;
        public float amineFlowPercent;
        public float regeneratorSteamPercent;
        public float regeneratorTemperatureC;
        public float co2InputKgH;
        public float co2CapturedKgH;
        public float captureEfficiencyPercent;

        public float compressionRatio;
        public float reactorFeedFlowPercent;
        public float syngasFeedKgH;
        public float reactorYieldPercent;
        public float reactorTemperatureC;
        public float reactorPressureBar;
        public float h2Co2Ratio;
        public float ghsv;

        public float coolingWaterFlowPercent;
        public float coolingWaterTemperatureC;
        public float condenserRecoveryPercent;
        public float separatorTemperatureC;
        public float recycleRatioPercent;
        public float recycleGasKgH;

        public float refluxRatio;
        public float distillationReboilerTemperatureC;
        public float methanolPurityPercent;
        public float distillationEnergyPercent;

        public float methanolProductionKgH;
        public float storedMethanolKg;
        public float overallEfficiencyPercent;
        public float storageFillPercent;
    }

    [Header("Design Capacity")]
    [SerializeField] private float designH2InputKgH = 215f;
    [SerializeField] private float designCO2InputKgH = 1510f;
    [SerializeField] private float designWaterFeedKgH = 1935f;
    [SerializeField] private float designMethanolKgH = 1250f;
    [SerializeField] private float storageCapacityKg = 12000f;

    [Header("Runtime")]
    [SerializeField] private bool autoFindSceneSliders = true;
    [SerializeField] private bool accumulateStorageInPlayMode = true;
    [SerializeField] private float storageSimulationHoursPerSecond = 0.035f;
    [SerializeField] private float outputSmoothing = 4.5f;

    [Header("Optional UI Outputs")]
    [SerializeField] private TMP_Text methanolProductionLabel;
    [SerializeField] private TMP_Text storageLabel;
    [SerializeField] private TMP_Text oxygenByproductLabel;
    [SerializeField] private TMP_Text overallEfficiencyLabel;

    private Slider timelineSlider;
    private Slider temperatureSlider;
    private Slider pressureSlider;
    private Slider ratioSlider;
    private Slider ghsvSlider;
    private Slider amineFlowSlider;
    private Slider regenTempSlider;

    private float manualTimeline = 100f;
    private float manualElectrolyzerPower = 75f;
    private float manualWaterFeed = 100f;
    private float manualFlueGasFlow = 100f;
    private float manualAmineFlow = 65f;
    private float manualRegeneratorSteam = 70f;
    private float manualRegenTemp = 105f;
    private float manualCompressionRatio = 3f;
    private float manualTemperature = 250f;
    private float manualPressure = 70f;
    private float manualRatio = 3f;
    private float manualGhsv = 8000f;
    private float manualReactorFeedFlow = 100f;
    private float manualCoolingWaterFlow = 70f;
    private float manualCoolingWaterTemperature = 24f;
    private float manualSeparatorTemperature = 34f;
    private float manualRecycleRatio = 65f;
    private float manualRefluxRatio = 2.2f;
    private float manualDistillationReboilerTemp = 92f;

    private bool manualTimelineEnabled;
    private bool manualTemperatureEnabled;
    private bool manualPressureEnabled;
    private bool manualRatioEnabled;
    private bool manualGhsvEnabled;
    private bool manualAmineFlowEnabled;
    private bool manualRegenTempEnabled;

    private ProcessSnapshot current;
    private ProcessSnapshot target;
    private bool initialized;

    public ProcessSnapshot Current => current;
    public event Action<ProcessSnapshot> SnapshotUpdated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<PlantProcessSimulator>() != null)
        {
            return;
        }

        GameObject go = new GameObject("Generated Plant Process Simulator");
        go.AddComponent<PlantProcessSimulator>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (autoFindSceneSliders)
        {
            FindSceneControls();
        }

        target = CalculateSnapshot();
        current = target;
        initialized = true;
        Publish();
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        target = CalculateSnapshot();

        float lerp = 1f - Mathf.Exp(-outputSmoothing * Time.deltaTime);
        current.timelinePercent = Mathf.Lerp(current.timelinePercent, target.timelinePercent, lerp);
        current.plantLoadPercent = Mathf.Lerp(current.plantLoadPercent, target.plantLoadPercent, lerp);
        current.electrolyzerPowerPercent = Mathf.Lerp(current.electrolyzerPowerPercent, target.electrolyzerPowerPercent, lerp);
        current.waterFeedPercent = Mathf.Lerp(current.waterFeedPercent, target.waterFeedPercent, lerp);
        current.waterFeedKgH = Mathf.Lerp(current.waterFeedKgH, target.waterFeedKgH, lerp);
        current.h2InputKgH = Mathf.Lerp(current.h2InputKgH, target.h2InputKgH, lerp);
        current.oxygenByproductKgH = Mathf.Lerp(current.oxygenByproductKgH, target.oxygenByproductKgH, lerp);
        current.flueGasFlowPercent = Mathf.Lerp(current.flueGasFlowPercent, target.flueGasFlowPercent, lerp);
        current.amineFlowPercent = Mathf.Lerp(current.amineFlowPercent, target.amineFlowPercent, lerp);
        current.regeneratorSteamPercent = Mathf.Lerp(current.regeneratorSteamPercent, target.regeneratorSteamPercent, lerp);
        current.regeneratorTemperatureC = Mathf.Lerp(current.regeneratorTemperatureC, target.regeneratorTemperatureC, lerp);
        current.co2InputKgH = Mathf.Lerp(current.co2InputKgH, target.co2InputKgH, lerp);
        current.co2CapturedKgH = Mathf.Lerp(current.co2CapturedKgH, target.co2CapturedKgH, lerp);
        current.captureEfficiencyPercent = Mathf.Lerp(current.captureEfficiencyPercent, target.captureEfficiencyPercent, lerp);
        current.compressionRatio = Mathf.Lerp(current.compressionRatio, target.compressionRatio, lerp);
        current.reactorFeedFlowPercent = Mathf.Lerp(current.reactorFeedFlowPercent, target.reactorFeedFlowPercent, lerp);
        current.syngasFeedKgH = Mathf.Lerp(current.syngasFeedKgH, target.syngasFeedKgH, lerp);
        current.reactorYieldPercent = Mathf.Lerp(current.reactorYieldPercent, target.reactorYieldPercent, lerp);
        current.reactorTemperatureC = Mathf.Lerp(current.reactorTemperatureC, target.reactorTemperatureC, lerp);
        current.reactorPressureBar = Mathf.Lerp(current.reactorPressureBar, target.reactorPressureBar, lerp);
        current.h2Co2Ratio = Mathf.Lerp(current.h2Co2Ratio, target.h2Co2Ratio, lerp);
        current.ghsv = Mathf.Lerp(current.ghsv, target.ghsv, lerp);
        current.coolingWaterFlowPercent = Mathf.Lerp(current.coolingWaterFlowPercent, target.coolingWaterFlowPercent, lerp);
        current.coolingWaterTemperatureC = Mathf.Lerp(current.coolingWaterTemperatureC, target.coolingWaterTemperatureC, lerp);
        current.condenserRecoveryPercent = Mathf.Lerp(current.condenserRecoveryPercent, target.condenserRecoveryPercent, lerp);
        current.separatorTemperatureC = Mathf.Lerp(current.separatorTemperatureC, target.separatorTemperatureC, lerp);
        current.recycleRatioPercent = Mathf.Lerp(current.recycleRatioPercent, target.recycleRatioPercent, lerp);
        current.recycleGasKgH = Mathf.Lerp(current.recycleGasKgH, target.recycleGasKgH, lerp);
        current.refluxRatio = Mathf.Lerp(current.refluxRatio, target.refluxRatio, lerp);
        current.distillationReboilerTemperatureC = Mathf.Lerp(current.distillationReboilerTemperatureC, target.distillationReboilerTemperatureC, lerp);
        current.methanolPurityPercent = Mathf.Lerp(current.methanolPurityPercent, target.methanolPurityPercent, lerp);
        current.distillationEnergyPercent = Mathf.Lerp(current.distillationEnergyPercent, target.distillationEnergyPercent, lerp);
        current.methanolProductionKgH = Mathf.Lerp(current.methanolProductionKgH, target.methanolProductionKgH, lerp);
        current.overallEfficiencyPercent = Mathf.Lerp(current.overallEfficiencyPercent, target.overallEfficiencyPercent, lerp);

        if (accumulateStorageInPlayMode)
        {
            current.storedMethanolKg += current.methanolProductionKgH * storageSimulationHoursPerSecond * Time.deltaTime;
            current.storedMethanolKg = Mathf.Clamp(current.storedMethanolKg, 0f, storageCapacityKg);
        }
        else
        {
            current.storedMethanolKg = Mathf.Lerp(current.storedMethanolKg, target.storedMethanolKg, lerp);
        }

        current.storageFillPercent = StorageFill(current.storedMethanolKg);
        Publish();
    }

    public void ResetStoredMethanol()
    {
        current.storedMethanolKg = 0f;
        current.storageFillPercent = 0f;
        Publish();
    }

    public void SetTimelinePercent(float value)
    {
        manualTimelineEnabled = true;
        manualTimeline = Mathf.Clamp(value, 0f, 100f);
        if (timelineSlider != null) timelineSlider.SetValueWithoutNotify(manualTimeline);
    }

    public void SetElectrolyzerPower(float value) => manualElectrolyzerPower = Mathf.Clamp(value, 0f, 100f);
    public void SetWaterFeed(float value) => manualWaterFeed = Mathf.Clamp(value, 0f, 130f);
    public void SetFlueGasFlow(float value) => manualFlueGasFlow = Mathf.Clamp(value, 0f, 130f);
    public void SetRegeneratorSteam(float value) => manualRegeneratorSteam = Mathf.Clamp(value, 0f, 100f);
    public void SetCompressionRatio(float value) => manualCompressionRatio = Mathf.Clamp(value, 1f, 6f);
    public void SetReactorFeedFlow(float value) => manualReactorFeedFlow = Mathf.Clamp(value, 20f, 130f);
    public void SetCoolingWaterFlow(float value) => manualCoolingWaterFlow = Mathf.Clamp(value, 0f, 100f);
    public void SetCoolingWaterTemperature(float value) => manualCoolingWaterTemperature = Mathf.Clamp(value, 5f, 45f);
    public void SetSeparatorTemperature(float value) => manualSeparatorTemperature = Mathf.Clamp(value, 20f, 65f);
    public void SetRecycleRatio(float value) => manualRecycleRatio = Mathf.Clamp(value, 0f, 100f);
    public void SetRefluxRatio(float value) => manualRefluxRatio = Mathf.Clamp(value, 0.5f, 5f);
    public void SetDistillationReboilerTemperature(float value) => manualDistillationReboilerTemp = Mathf.Clamp(value, 70f, 115f);

    public void SetReactorTemperature(float value)
    {
        manualTemperatureEnabled = true;
        manualTemperature = Mathf.Clamp(value, 200f, 300f);
        if (temperatureSlider != null) temperatureSlider.SetValueWithoutNotify(manualTemperature);
    }

    public void SetReactorPressure(float value)
    {
        manualPressureEnabled = true;
        manualPressure = Mathf.Clamp(value, 40f, 100f);
        if (pressureSlider != null) pressureSlider.SetValueWithoutNotify(manualPressure);
    }

    public void SetH2Co2Ratio(float value)
    {
        manualRatioEnabled = true;
        manualRatio = Mathf.Clamp(value, 1f, 6f);
        if (ratioSlider != null) ratioSlider.SetValueWithoutNotify(manualRatio);
    }

    public void SetGHSV(float value)
    {
        manualGhsvEnabled = true;
        manualGhsv = Mathf.Clamp(value, 1000f, 20000f);
        if (ghsvSlider != null) ghsvSlider.SetValueWithoutNotify(manualGhsv);
    }

    public void SetAmineFlow(float value)
    {
        manualAmineFlowEnabled = true;
        manualAmineFlow = Mathf.Clamp(value, 10f, 100f);
        if (amineFlowSlider != null) amineFlowSlider.SetValueWithoutNotify(manualAmineFlow);
    }

    public void SetRegeneratorTemperature(float value)
    {
        manualRegenTempEnabled = true;
        manualRegenTemp = Mathf.Clamp(value, 80f, 130f);
        if (regenTempSlider != null) regenTempSlider.SetValueWithoutNotify(manualRegenTemp);
    }

    private void Publish()
    {
        UpdateOptionalLabels();
        SnapshotUpdated?.Invoke(current);
    }

    private ProcessSnapshot CalculateSnapshot()
    {
        float timeline = manualTimelineEnabled ? manualTimeline : GetSliderValue(timelineSlider, 100f);
        float plantRamp = CalculatePlantRamp(timeline);

        float temperature = manualTemperatureEnabled ? manualTemperature : GetSliderValue(temperatureSlider, 250f);
        float pressure = manualPressureEnabled ? manualPressure : GetSliderValue(pressureSlider, 70f);
        float ratio = manualRatioEnabled ? manualRatio : GetSliderValue(ratioSlider, 3f);
        float ghsv = manualGhsvEnabled ? manualGhsv : GetSliderValue(ghsvSlider, 8000f);
        float amineFlow = manualAmineFlowEnabled ? manualAmineFlow : GetSliderValue(amineFlowSlider, 65f);
        float regenTemp = manualRegenTempEnabled ? manualRegenTemp : GetSliderValue(regenTempSlider, 105f);

        float powerFactor = Mathf.Clamp01(manualElectrolyzerPower / 100f);
        float waterFactor = Mathf.Clamp01(manualWaterFeed / 100f);
        float electrolyzerFactor = Mathf.Min(powerFactor, Mathf.Lerp(0.15f, 1.1f, waterFactor));
        float h2Input = designH2InputKgH * plantRamp * electrolyzerFactor;
        float waterFeed = designWaterFeedKgH * plantRamp * waterFactor;
        float oxygen = h2Input * 8f;

        float flueGasFactor = Mathf.Clamp01(manualFlueGasFlow / 100f);
        float amineFactor = Mathf.Pow(Mathf.Clamp01(amineFlow / 100f), 0.55f);
        float regenTempFactor = Mathf.InverseLerp(82f, 118f, regenTemp);
        float steamFactor = Mathf.Pow(Mathf.Clamp01(manualRegeneratorSteam / 100f), 0.45f);
        float captureEfficiency = Mathf.Clamp01(0.18f + 0.46f * amineFactor + 0.22f * regenTempFactor + 0.14f * steamFactor);
        float co2Input = designCO2InputKgH * plantRamp * flueGasFactor;
        float co2Captured = co2Input * captureEfficiency;

        float reactorFeedFactor = Mathf.Clamp01(manualReactorFeedFlow / 100f);
        float syngasFeed = (h2Input + co2Captured) * reactorFeedFactor;

        float tempRateFactor = Mathf.InverseLerp(210f, 255f, temperature);
        float tempEquilibriumFactor = 1f - Mathf.Clamp01((temperature - 255f) / 55f) * 0.32f;
        float tempFactor = Mathf.Clamp01(tempRateFactor * tempEquilibriumFactor);
        float pressureFactor = Mathf.Clamp01(pressure / 100f);
        float ratioFactor = 1f - Mathf.Clamp01(Mathf.Abs(ratio - 3f) / 3f) * 0.42f;
        float residenceFactor = Mathf.Clamp(8000f / Mathf.Max(ghsv, 1f), 0.35f, 1.35f);
        float recycleBoost = Mathf.Lerp(0.86f, 1.18f, manualRecycleRatio / 100f);
        float reactorYield = Mathf.Clamp01(tempFactor * pressureFactor * ratioFactor * residenceFactor * recycleBoost);

        // Stoichiometric basis:
        // CO2 + 3H2 -> CH3OH + H2O
        // 6 kg H2 and 44 kg CO2 can form 32 kg methanol.
        float h2ToReactor = h2Input * reactorFeedFactor;
        float co2ToReactor = co2Captured * reactorFeedFactor;
        float methanolLimitedByH2 = h2ToReactor * (32f / 6f);
        float methanolLimitedByCO2 = co2ToReactor * (32f / 44f);
        float theoreticalMethanol = Mathf.Min(methanolLimitedByH2, methanolLimitedByCO2);

        float coolingFactor = Mathf.Clamp01((manualCoolingWaterFlow / 100f) * Mathf.InverseLerp(45f, 8f, manualCoolingWaterTemperature));
        float condenserRecovery = Mathf.Lerp(0.55f, 0.98f, coolingFactor);
        float separatorFactor = 1f - Mathf.Clamp01(Mathf.Abs(manualSeparatorTemperature - 34f) / 35f) * 0.16f;
        float distillationFactor = Mathf.Clamp01(0.72f + 0.11f * Mathf.InverseLerp(0.5f, 5f, manualRefluxRatio) + 0.17f * Mathf.InverseLerp(76f, 105f, manualDistillationReboilerTemp));
        float methanolPurity = Mathf.Clamp(90f + 7.2f * Mathf.InverseLerp(0.5f, 5f, manualRefluxRatio) + 2.4f * Mathf.InverseLerp(78f, 105f, manualDistillationReboilerTemp), 88f, 99.85f);
        float distillationEnergy = Mathf.Clamp(18f + manualRefluxRatio * 12f + Mathf.InverseLerp(70f, 115f, manualDistillationReboilerTemp) * 36f, 0f, 100f);

        float methanol = Mathf.Min(designMethanolKgH, theoreticalMethanol) * reactorYield * condenserRecovery * separatorFactor * distillationFactor;
        float unconverted = Mathf.Max(0f, syngasFeed - methanol);
        float recycle = unconverted * (manualRecycleRatio / 100f) * 0.36f;
        float efficiency = Mathf.Clamp01(methanol / Mathf.Max(theoreticalMethanol, 1f)) * 100f;

        ProcessSnapshot snapshot = new ProcessSnapshot
        {
            timelinePercent = timeline,
            plantLoadPercent = plantRamp * 100f,
            electrolyzerPowerPercent = manualElectrolyzerPower,
            waterFeedPercent = manualWaterFeed,
            waterFeedKgH = waterFeed,
            h2InputKgH = h2Input,
            oxygenByproductKgH = oxygen,
            flueGasFlowPercent = manualFlueGasFlow,
            amineFlowPercent = amineFlow,
            regeneratorSteamPercent = manualRegeneratorSteam,
            regeneratorTemperatureC = regenTemp,
            co2InputKgH = co2Input,
            co2CapturedKgH = co2Captured,
            captureEfficiencyPercent = captureEfficiency * 100f,
            compressionRatio = manualCompressionRatio,
            reactorFeedFlowPercent = manualReactorFeedFlow,
            syngasFeedKgH = syngasFeed,
            reactorYieldPercent = reactorYield * 100f,
            reactorTemperatureC = temperature,
            reactorPressureBar = pressure,
            h2Co2Ratio = ratio,
            ghsv = ghsv,
            coolingWaterFlowPercent = manualCoolingWaterFlow,
            coolingWaterTemperatureC = manualCoolingWaterTemperature,
            condenserRecoveryPercent = condenserRecovery * 100f,
            separatorTemperatureC = manualSeparatorTemperature,
            recycleRatioPercent = manualRecycleRatio,
            recycleGasKgH = recycle,
            refluxRatio = manualRefluxRatio,
            distillationReboilerTemperatureC = manualDistillationReboilerTemp,
            methanolPurityPercent = methanolPurity,
            distillationEnergyPercent = distillationEnergy,
            methanolProductionKgH = methanol,
            storedMethanolKg = current.storedMethanolKg,
            overallEfficiencyPercent = efficiency
        };

        snapshot.storageFillPercent = StorageFill(snapshot.storedMethanolKg);
        return snapshot;
    }

    private float CalculatePlantRamp(float timeline)
    {
        if (timeline <= 0f)
        {
            return 0f;
        }

        if (timeline < 15f)
        {
            return Mathf.Lerp(0f, 0.75f, timeline / 15f);
        }

        if (timeline < 40f)
        {
            return Mathf.Lerp(0.75f, 1f, (timeline - 15f) / 25f);
        }

        if (timeline < 75f)
        {
            return 1f;
        }

        return Mathf.Lerp(1f, 0.95f, (timeline - 75f) / 25f);
    }

    private float StorageFill(float storedKg)
    {
        return storageCapacityKg <= 0f ? 0f : Mathf.Clamp01(storedKg / storageCapacityKg) * 100f;
    }

    private float GetSliderValue(Slider slider, float fallback)
    {
        return slider != null ? slider.value : fallback;
    }

    private void FindSceneControls()
    {
        Slider[] sliders = FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Slider slider in sliders)
        {
            string n = slider.name.ToLowerInvariant();
            if (timelineSlider == null && n.Contains("timeline")) timelineSlider = slider;
            else if (temperatureSlider == null && (n.Contains("tempslider") || n.Contains("temperature"))) temperatureSlider = slider;
            else if (pressureSlider == null && n.Contains("pressure")) pressureSlider = slider;
            else if (ratioSlider == null && n.Contains("ratio")) ratioSlider = slider;
            else if (ghsvSlider == null && n.Contains("ghsv")) ghsvSlider = slider;
            else if (amineFlowSlider == null && n.Contains("amine")) amineFlowSlider = slider;
            else if (regenTempSlider == null && n.Contains("regen")) regenTempSlider = slider;
        }

        TMP_Text[] labels = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text label in labels)
        {
            string n = label.name.ToLowerInvariant();
            if (methanolProductionLabel == null && (n.Contains("meohvalue") || n.Contains("methanolproduction")))
                methanolProductionLabel = label;
            else if (oxygenByproductLabel == null && n.Contains("oxygen"))
                oxygenByproductLabel = label;
            else if (overallEfficiencyLabel == null && n.Contains("effvalue"))
                overallEfficiencyLabel = label;
            else if (storageLabel == null && n.Contains("storage"))
                storageLabel = label;
        }
    }

    private void UpdateOptionalLabels()
    {
        if (methanolProductionLabel != null)
            methanolProductionLabel.text = $"{current.methanolProductionKgH:F0} kg/h";

        if (storageLabel != null)
            storageLabel.text = $"{current.storedMethanolKg:F0} kg stored ({current.storageFillPercent:F0}%)";

        if (oxygenByproductLabel != null)
            oxygenByproductLabel.text = $"{current.oxygenByproductKgH:F0} kg/h O2";

        if (overallEfficiencyLabel != null)
            overallEfficiencyLabel.text = $"{current.overallEfficiencyPercent:F0}%";
    }
}
