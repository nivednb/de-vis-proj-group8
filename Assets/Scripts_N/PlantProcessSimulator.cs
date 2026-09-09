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
        public float storageTimeRemainingSeconds;
        public bool storageInterlockActive;
    }

    /// <summary>
    /// The full set of control inputs consumed by <see cref="Simulate"/>. Bundling every
    /// manual value into one struct lets the OFAT sweep take the current operating point,
    /// perturb exactly one entry across a range, and re-evaluate the process with everything
    /// else held constant.
    /// </summary>
    [Serializable]
    public struct ProcessInputs
    {
        public float timeline;
        public bool storageInterlockLatched;
        public float storedMethanolKg;
        public float electrolyzerPower;
        public float waterFeed;
        public float flueGasFlow;
        public float amineFlow;
        public float regeneratorSteam;
        public float regenTemp;
        public float compressionRatio;
        public float temperature;
        public float pressure;
        public float ratio;
        public float ghsv;
        public float reactorFeedFlow;
        public float coolingWaterFlow;
        public float coolingWaterTemperature;
        public float separatorTemperature;
        public float recycleRatio;
        public float refluxRatio;
        public float distillationReboilerTemp;
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

    [Header("Storage Protection")]
    [Tooltip("A high-high level trip prevents further methanol production until storage is reset/unloaded.")]
    [SerializeField, Range(90f, 100f)] private float storageHighHighPercent = 99f;
    [SerializeField] private bool enableStorageHighHighTrip = true;

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

    // Matches CreateSlider's seed values in InteractiveModulePanelRuntime and what
    // ResetSimulation() returns to — the plant starts already at a typical operating point
    // (not powered off) so the scene reads as "running" the moment it loads.
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
    private float manualRefluxRatio = 3.2f;
    private float manualDistillationReboilerTemp = 98f;

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
    private bool storageInterlockLatched;
    private RecycleMassBalanceEngine recycleMassBalance;

    public ProcessSnapshot Current => current;
    public RecycleMassBalanceEngine MassBalance => recycleMassBalance;
    public event Action<ProcessSnapshot> SnapshotUpdated;

    /// <summary>The current operating point as a <see cref="ProcessInputs"/> — the baseline
    /// the OFAT sweep holds constant while it varies one axis.</summary>
    public ProcessInputs CurrentInputs => BuildCurrentInputs();

    public readonly struct ManualChangeInfo
    {
        public readonly string Module;
        public readonly string Parameter;
        public readonly float FromValue;
        public readonly float ToValue;
        public readonly float Time;

        public ManualChangeInfo(string module, string parameter, float fromValue, float toValue, float time)
        {
            Module = module;
            Parameter = parameter;
            FromValue = fromValue;
            ToValue = toValue;
            Time = time;
        }
    }

    // The most recent manual slider adjustment across every module, so external observers
    // (e.g. the live analytics graphs) can tell "the user just changed X from A to B in
    // module Y" apart from the plant's own smoothed drift toward its targets, without
    // separate per-control event wiring. Committed once per interaction (on slider release,
    // not every intermediate drag tick) by InteractiveModulePanelRuntime's SliderCommitTracker
    // — the only place that already knows the exact slider label, its owning module, and the
    // value it held before this interaction started.
    public ManualChangeInfo LastManualChange { get; private set; } = new ManualChangeInfo("", "", 0f, 0f, -1000f);

    /// <summary>Fired once per committed slider interaction (see LastManualChange) — the
    /// event other systems (e.g. the correlation graphs) should react to, rather than
    /// polling LastManualChange every frame.</summary>
    public event Action<ManualChangeInfo> ManualChangeCommitted;

    public void CommitManualChange(string module, string parameter, float fromValue, float toValue)
    {
        ManualChangeInfo info = new ManualChangeInfo(module, parameter, fromValue, toValue, Time.unscaledTime);
        LastManualChange = info;
        ManualChangeCommitted?.Invoke(info);
    }

    // Design-capacity accessors so external UI (live analytics graphs) can scale axes to
    // the plant's known maximums instead of re-deriving/duplicating these constants.
    public float DesignMethanolKgH => designMethanolKgH;
    public float StorageCapacityKg => storageCapacityKg;

    /// <summary>
    /// Real-world seconds until the methanol tank fills at the current production rate
    /// (accounts for the accelerated storage clock). PositiveInfinity when production has
    /// effectively stopped; 0 when the tank is already full.
    /// </summary>
    public float SecondsUntilStorageFull
    {
        get
        {
            float remaining = storageCapacityKg - current.storedMethanolKg;
            if (remaining <= 0f) return 0f;
            float rate = current.methanolProductionKgH;
            if (rate < 1f || storageSimulationHoursPerSecond <= 0f || !accumulateStorageInPlayMode)
                return float.PositiveInfinity;
            return remaining / rate / storageSimulationHoursPerSecond;
        }
    }

    public bool IsRunning { get; private set; } = true;

    /// <summary>Fired after ResetSimulation() has fully applied the fresh start-of-run state.</summary>
    public event Action ResetRequested;

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
        recycleMassBalance = GetComponent<RecycleMassBalanceEngine>();
        if (recycleMassBalance == null)
            recycleMassBalance = gameObject.AddComponent<RecycleMassBalanceEngine>();
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
        if (!initialized || !IsRunning)
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
        current.storageTimeRemainingSeconds = StorageTimeRemainingSeconds(
            current.storedMethanolKg,
            current.methanolProductionKgH);
        if (enableStorageHighHighTrip && current.storageFillPercent >= storageHighHighPercent)
        {
            storageInterlockLatched = true;
        }
        current.storageInterlockActive = storageInterlockLatched;
        Publish();
    }

    public void ResetStoredMethanol()
    {
        current.storedMethanolKg = 0f;
        current.storageFillPercent = 0f;
        current.storageTimeRemainingSeconds = StorageTimeRemainingSeconds(0f, current.methanolProductionKgH);
        storageInterlockLatched = false;
        current.storageInterlockActive = false;
        Publish();
    }

    public void Play() => IsRunning = true;
    public void Pause() => IsRunning = false;

    /// <summary>
    /// Returns every manual override and the storage level to their start-of-run defaults
    /// (the same values CreateSlider seeds each control with) and resumes the run, so the
    /// process reads exactly as if it had just started. UI that owns the actual slider
    /// handles (InteractiveModulePanelRuntime) should follow this with a call to reposition
    /// its own visuals via SetValueWithoutNotify — this method is the single source of truth
    /// for the underlying data and fires <see cref="ResetRequested"/> once it's settled.
    /// </summary>
    public void ResetSimulation()
    {
        manualTimeline = 100f;
        manualElectrolyzerPower = 75f;
        manualWaterFeed = 100f;
        manualFlueGasFlow = 100f;
        manualAmineFlow = 65f;
        manualRegeneratorSteam = 70f;
        manualRegenTemp = 105f;
        manualCompressionRatio = 3f;
        manualTemperature = 250f;
        manualPressure = 70f;
        manualRatio = 3f;
        manualGhsv = 8000f;
        manualReactorFeedFlow = 100f;
        manualCoolingWaterFlow = 70f;
        manualCoolingWaterTemperature = 24f;
        manualSeparatorTemperature = 34f;
        manualRecycleRatio = 65f;
        manualRefluxRatio = 3.2f;
        manualDistillationReboilerTemp = 98f;

        storageInterlockLatched = false;
        LastManualChange = new ManualChangeInfo("", "", 0f, 0f, -1000f);
        IsRunning = true;

        target = CalculateSnapshot();
        current = target;
        current.storedMethanolKg = 0f;
        current.storageFillPercent = 0f;
        current.storageInterlockActive = false;

        if (temperatureSlider != null) temperatureSlider.SetValueWithoutNotify(manualTemperature);
        if (pressureSlider != null) pressureSlider.SetValueWithoutNotify(manualPressure);
        if (timelineSlider != null) timelineSlider.SetValueWithoutNotify(manualTimeline);
        if (ratioSlider != null) ratioSlider.SetValueWithoutNotify(manualRatio);
        if (ghsvSlider != null) ghsvSlider.SetValueWithoutNotify(manualGhsv);
        if (amineFlowSlider != null) amineFlowSlider.SetValueWithoutNotify(manualAmineFlow);
        if (regenTempSlider != null) regenTempSlider.SetValueWithoutNotify(manualRegenTemp);

        Publish();
        ResetRequested?.Invoke();
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
    public void SetReactorFeedFlow(float value)
    {
        manualReactorFeedFlow = Mathf.Clamp(value, 20f, 130f);
        ApplyReactorControlChange();
    }
    public void SetCoolingWaterFlow(float value) => manualCoolingWaterFlow = Mathf.Clamp(value, 0f, 100f);
    public void SetCoolingWaterTemperature(float value) => manualCoolingWaterTemperature = Mathf.Clamp(value, 5f, 45f);
    public void SetSeparatorTemperature(float value) => manualSeparatorTemperature = Mathf.Clamp(value, 20f, 65f);
    public void SetRecycleRatio(float value) => manualRecycleRatio = Mathf.Clamp(value, 0f, 100f);
    public void SetRefluxRatio(float value) => manualRefluxRatio = Mathf.Clamp(value, 0.5f, 5f);
    public void SetDistillationReboilerTemperature(float value) => manualDistillationReboilerTemp = Mathf.Clamp(value, 70f, 115f);

    public void SetReactorTemperature(float value)
    {
        manualTemperatureEnabled = true;
        manualTemperature = Mathf.Clamp(value, 180f, 300f);
        if (temperatureSlider != null) temperatureSlider.SetValueWithoutNotify(manualTemperature);
        ApplyReactorControlChange();
    }

    public void SetReactorPressure(float value)
    {
        manualPressureEnabled = true;
        manualPressure = Mathf.Clamp(value, 40f, 100f);
        if (pressureSlider != null) pressureSlider.SetValueWithoutNotify(manualPressure);
        ApplyReactorControlChange();
    }

    public void SetH2Co2Ratio(float value)
    {
        manualRatioEnabled = true;
        manualRatio = Mathf.Clamp(value, 1f, 6f);
        if (ratioSlider != null) ratioSlider.SetValueWithoutNotify(manualRatio);
        ApplyReactorControlChange();
    }

    public void SetGHSV(float value)
    {
        manualGhsvEnabled = true;
        manualGhsv = Mathf.Clamp(value, 1000f, 20000f);
        if (ghsvSlider != null) ghsvSlider.SetValueWithoutNotify(manualGhsv);
        ApplyReactorControlChange();
    }

    /// <summary>
    /// Re-evaluates the reactor response immediately after a T/P/R/V UI event.
    /// Only the setter's own manual value changes; the other three operating
    /// variables retain their current values (ceteris paribus).
    /// </summary>
    private void ApplyReactorControlChange()
    {
        if (!initialized) return;
        target = CalculateSnapshot();
        current = target;
        Publish();
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

    public float GetControlValue(string label, float fallback)
    {
        switch (label)
        {
            case "Plant load": return manualTimelineEnabled ? manualTimeline : GetSliderValue(timelineSlider, fallback);
            case "Power": return manualElectrolyzerPower;
            case "Water feed": return manualWaterFeed;
            case "Amine flow": return manualAmineFlowEnabled ? manualAmineFlow : GetSliderValue(amineFlowSlider, fallback);
            case "Flue gas": return manualFlueGasFlow;
            case "Steam flow": return manualRegeneratorSteam;
            case "Regen temp": return manualRegenTempEnabled ? manualRegenTemp : GetSliderValue(regenTempSlider, fallback);
            case "Comp. ratio": return manualCompressionRatio;
            case "Outlet press.":
            case "Pressure": return manualPressureEnabled ? manualPressure : GetSliderValue(pressureSlider, fallback);
            case "Temp": return manualTemperatureEnabled ? manualTemperature : GetSliderValue(temperatureSlider, fallback);
            case "H2/CO2": return manualRatioEnabled ? manualRatio : GetSliderValue(ratioSlider, fallback);
            case "GHSV": return manualGhsvEnabled ? manualGhsv : GetSliderValue(ghsvSlider, fallback);
            case "Feed flow": return manualReactorFeedFlow;
            case "Cooling flow": return manualCoolingWaterFlow;
            case "Cooling temp": return manualCoolingWaterTemperature;
            case "Recycle ratio": return manualRecycleRatio;
            case "Sep. temp": return manualSeparatorTemperature;
            case "Reflux ratio": return manualRefluxRatio;
            case "Reboiler temp": return manualDistillationReboilerTemp;
            default: return fallback;
        }
    }

    public bool ApplyMaximumEfficiencyOperatingPoint()
    {
        // A full product tank is a hard safety constraint. Optimisation must never
        // silently clear or bypass the storage high-high interlock.
        if (storageInterlockLatched)
            return false;

        SetTimelinePercent(100f);
        SetElectrolyzerPower(100f);
        SetWaterFeed(100f);
        SetFlueGasFlow(100f);
        SetAmineFlow(100f);
        SetRegeneratorSteam(100f);
        SetRegeneratorTemperature(118f);
        SetCompressionRatio(3f);
        SetReactorFeedFlow(100f);
        SetReactorTemperature(255f);
        SetReactorPressure(70f);
        SetH2Co2Ratio(3f);
        SetGHSV(5900f);
        SetCoolingWaterFlow(100f);
        SetCoolingWaterTemperature(8f);
        SetSeparatorTemperature(34f);
        // A finite purge is required for a physically solvable steady-state loop.
        // 95% recycle retains high conversion while avoiding the singular 100% case.
        SetRecycleRatio(95f);
        SetRefluxRatio(5f);
        SetDistillationReboilerTemperature(105f);

        SyncVisibleControlSliders();
        current = CalculateSnapshot();
        initialized = true;
        Publish();
        return true;
    }

    private void SyncVisibleControlSliders()
    {
        const string suffix = " Slider";
        Slider[] sliders = FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Slider slider in sliders)
        {
            if (slider == null || !slider.name.EndsWith(suffix, StringComparison.Ordinal))
                continue;

            string label = slider.name.Substring(0, slider.name.Length - suffix.Length);
            float value = GetControlValue(label, float.NaN);
            if (!float.IsNaN(value))
                slider.value = value;
        }
    }

    private void Publish()
    {
        UpdateOptionalLabels();
        SnapshotUpdated?.Invoke(current);
    }

    private ProcessSnapshot CalculateSnapshot()
    {
        ProcessSnapshot snapshot = Simulate(BuildCurrentInputs(), out RecycleMassBalanceEngine.RecycleCalculationResult recycleCalculation);
        recycleMassBalance?.ApplyCalculatedResult(recycleCalculation);
        return snapshot;
    }

    private ProcessInputs BuildCurrentInputs()
    {
        return new ProcessInputs
        {
            timeline = manualTimelineEnabled ? manualTimeline : GetSliderValue(timelineSlider, 100f),
            storageInterlockLatched = storageInterlockLatched,
            storedMethanolKg = current.storedMethanolKg,
            electrolyzerPower = manualElectrolyzerPower,
            waterFeed = manualWaterFeed,
            flueGasFlow = manualFlueGasFlow,
            regeneratorSteam = manualRegeneratorSteam,
            compressionRatio = manualCompressionRatio,
            reactorFeedFlow = manualReactorFeedFlow,
            coolingWaterFlow = manualCoolingWaterFlow,
            coolingWaterTemperature = manualCoolingWaterTemperature,
            separatorTemperature = manualSeparatorTemperature,
            recycleRatio = manualRecycleRatio,
            refluxRatio = manualRefluxRatio,
            distillationReboilerTemp = manualDistillationReboilerTemp,
            temperature = manualTemperatureEnabled ? manualTemperature : GetSliderValue(temperatureSlider, 250f),
            pressure = manualPressureEnabled ? manualPressure : GetSliderValue(pressureSlider, 70f),
            ratio = manualRatioEnabled ? manualRatio : GetSliderValue(ratioSlider, 3f),
            ghsv = manualGhsvEnabled ? manualGhsv : GetSliderValue(ghsvSlider, 8000f),
            amineFlow = manualAmineFlowEnabled ? manualAmineFlow : GetSliderValue(amineFlowSlider, 65f),
            regenTemp = manualRegenTempEnabled ? manualRegenTemp : GetSliderValue(regenTempSlider, 105f),
        };
    }

    /// <summary>
    /// Pure process math: given a complete set of control inputs, returns the resulting
    /// snapshot. No side effects and reads no mutable simulator state, so the OFAT sweep can
    /// call it many times per frame with one input varied and everything else held constant.
    /// </summary>
    public ProcessSnapshot Simulate(ProcessInputs i)
    {
        return Simulate(i, out _);
    }

    private ProcessSnapshot Simulate(ProcessInputs i, out RecycleMassBalanceEngine.RecycleCalculationResult recycleCalculation)
    {
        float timeline = i.timeline;
        float plantRamp = i.storageInterlockLatched ? 0f : CalculatePlantRamp(timeline);

        float temperature = i.temperature;
        float pressure = i.pressure;
        float ratio = i.ratio;
        float ghsv = i.ghsv;
        float amineFlow = i.amineFlow;
        float regenTemp = i.regenTemp;

        float powerFactor = Mathf.Clamp01(i.electrolyzerPower / 100f);
        float waterFactor = Mathf.Clamp01(i.waterFeed / 100f);
        float electrolyzerFactor = Mathf.Min(powerFactor, Mathf.Lerp(0.15f, 1.1f, waterFactor));
        float h2Input = designH2InputKgH * plantRamp * electrolyzerFactor;
        float waterFeed = designWaterFeedKgH * plantRamp * waterFactor;
        float oxygen = h2Input * 8f;

        float flueGasFactor = Mathf.Clamp01(i.flueGasFlow / 100f);
        float amineFactor = Mathf.Pow(Mathf.Clamp01(amineFlow / 100f), 0.55f);
        float regenTempFactor = Mathf.InverseLerp(82f, 118f, regenTemp);
        float steamFactor = Mathf.Pow(Mathf.Clamp01(i.regeneratorSteam / 100f), 0.45f);
        float captureEfficiency = Mathf.Clamp01(0.18f + 0.46f * amineFactor + 0.22f * regenTempFactor + 0.14f * steamFactor);
        float co2Input = designCO2InputKgH * plantRamp * flueGasFactor;
        float co2Captured = co2Input * captureEfficiency;

        float reactorFeedFactor = Mathf.Clamp01(i.reactorFeedFlow / 100f);
        float syngasFeed = (h2Input + co2Captured) * reactorFeedFactor;

        float singlePassConversion = RecycleMassBalanceEngine.CalculateSinglePassConversion(
            temperature, pressure, ratio, ghsv);

        // Stoichiometric basis:
        // CO2 + 3H2 -> CH3OH + H2O
        // 6 kg H2 and 44 kg CO2 can form 32 kg methanol.
        float availableH2 = h2Input * reactorFeedFactor;
        float availableCo2 = co2Captured * reactorFeedFactor;
        float requestedRatio = Mathf.Max(0.1f, ratio);
        float h2RequiredForAvailableCo2 = availableCo2 / 44.0095f * requestedRatio * 2.01588f;
        float h2ToReactor = Mathf.Min(availableH2, h2RequiredForAvailableCo2);
        float co2ToReactor = Mathf.Min(availableCo2, h2ToReactor / 2.01588f / requestedRatio * 44.0095f);
        recycleCalculation = RecycleMassBalanceEngine.Calculate(
            co2ToReactor, h2ToReactor, singlePassConversion, i.recycleRatio / 100f);
        float theoreticalMethanol = Mathf.Min(
            h2ToReactor * (32.04186f / (3f * 2.01588f)),
            co2ToReactor * (32.04186f / 44.0095f));

        float coolingFactor = Mathf.Clamp01((i.coolingWaterFlow / 100f) * Mathf.InverseLerp(45f, 8f, i.coolingWaterTemperature));
        float condenserRecovery = Mathf.Lerp(0.55f, 0.98f, coolingFactor);
        float separatorFactor = 1f - Mathf.Clamp01(Mathf.Abs(i.separatorTemperature - 34f) / 35f) * 0.16f;
        float distillationFactor = Mathf.Clamp01(0.72f + 0.11f * Mathf.InverseLerp(0.5f, 5f, i.refluxRatio) + 0.17f * Mathf.InverseLerp(76f, 105f, i.distillationReboilerTemp));
        float methanolPurity = Mathf.Clamp(90f + 7.2f * Mathf.InverseLerp(0.5f, 5f, i.refluxRatio) + 2.4f * Mathf.InverseLerp(78f, 105f, i.distillationReboilerTemp), 88f, 99.85f);
        float distillationEnergy = Mathf.Clamp(18f + i.refluxRatio * 12f + Mathf.InverseLerp(70f, 115f, i.distillationReboilerTemp) * 36f, 0f, 100f);

        float reactorMethanol = recycleCalculation.Converged
            ? (float)recycleCalculation.MethanolProductKgHr
            : theoreticalMethanol * singlePassConversion;
        float methanol = Mathf.Min(designMethanolKgH, reactorMethanol) * condenserRecovery * separatorFactor * distillationFactor;
        float recycle = recycleCalculation.Converged ? (float)recycleCalculation.RecycleStreamKgHr : 0f;
        float efficiency = Mathf.Clamp01(methanol / Mathf.Max(theoreticalMethanol, 1f)) * 100f;

        ProcessSnapshot snapshot = new ProcessSnapshot
        {
            timelinePercent = timeline,
            plantLoadPercent = plantRamp * 100f,
            electrolyzerPowerPercent = i.electrolyzerPower,
            waterFeedPercent = i.waterFeed,
            waterFeedKgH = waterFeed,
            h2InputKgH = h2Input,
            oxygenByproductKgH = oxygen,
            flueGasFlowPercent = i.flueGasFlow,
            amineFlowPercent = amineFlow,
            regeneratorSteamPercent = i.regeneratorSteam,
            regeneratorTemperatureC = regenTemp,
            co2InputKgH = co2Input,
            co2CapturedKgH = co2Captured,
            captureEfficiencyPercent = captureEfficiency * 100f,
            compressionRatio = i.compressionRatio,
            reactorFeedFlowPercent = i.reactorFeedFlow,
            syngasFeedKgH = syngasFeed,
            reactorYieldPercent = singlePassConversion * 100f,
            reactorTemperatureC = temperature,
            reactorPressureBar = pressure,
            h2Co2Ratio = ratio,
            ghsv = ghsv,
            coolingWaterFlowPercent = i.coolingWaterFlow,
            coolingWaterTemperatureC = i.coolingWaterTemperature,
            condenserRecoveryPercent = condenserRecovery * 100f,
            separatorTemperatureC = i.separatorTemperature,
            recycleRatioPercent = i.recycleRatio,
            recycleGasKgH = recycle,
            refluxRatio = i.refluxRatio,
            distillationReboilerTemperatureC = i.distillationReboilerTemp,
            methanolPurityPercent = methanolPurity,
            distillationEnergyPercent = distillationEnergy,
            methanolProductionKgH = methanol,
            storedMethanolKg = i.storedMethanolKg,
            overallEfficiencyPercent = efficiency,
            storageInterlockActive = i.storageInterlockLatched
        };

        snapshot.storageFillPercent = StorageFill(snapshot.storedMethanolKg);
        snapshot.storageTimeRemainingSeconds = StorageTimeRemainingSeconds(
            snapshot.storedMethanolKg,
            snapshot.methanolProductionKgH);
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

    private float StorageTimeRemainingSeconds(float storedKg, float productionKgH)
    {
        float operationalFullKg = enableStorageHighHighTrip
            ? storageCapacityKg * storageHighHighPercent / 100f
            : storageCapacityKg;
        float remainingKg = Mathf.Max(0f, operationalFullKg - storedKg);
        float accumulationKgPerSecond = productionKgH * storageSimulationHoursPerSecond;

        if (remainingKg <= 0.01f) return 0f;
        return accumulationKgPerSecond > 0.0001f
            ? remainingKg / accumulationKgPerSecond
            : float.PositiveInfinity;
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
