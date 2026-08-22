using System;
using UnityEngine;

/// <summary>
/// Educational steady-state recycle/purge balance for:
/// CO2 + 3 H2 -> CH3OH + H2O.
///
/// Assumptions: perfect removal of methanol/water before gas recycle,
/// identical H2/CO2 recycle split, no inerts or side reactions, and steady state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RecycleMassBalanceEngine : MonoBehaviour
{
    // kg/kmol. These values close the displayed reaction mass balance.
    private const double M_H2 = 2.01588;
    private const double M_CO2 = 44.00950;
    private const double M_MEOH = 32.04186;
    private const double M_H2O = 18.01528;
    private const int MaxIterations = 1024;
    private const double ConvergenceToleranceKmolHr = 1e-9;

    [Header("Fresh Inputs (kg/h)")]
    [Min(0f)] public float freshCo2KgHr = 1152f;
    [Min(0f)] public float freshH2KgHr = 158f;

    [Header("Reactor & Separator Parameters")]
    [Tooltip("Fraction of reactor-inlet CO2 converted in one pass.")]
    [Range(0.05f, 0.35f)] public float singlePassCo2Conversion = 0.20f;
    [Tooltip("Fraction of unreacted separator gas returned to the reactor; the remainder is purged.")]
    [Range(0.80f, 0.99f)] public float recycleFraction = 0.95f;

    [Header("Reactor Inlet (kg/h)")]
    public float reactorFeedCo2KgHr;
    public float reactorFeedH2KgHr;
    public float totalReactorFeedKgHr;
    public float reactorH2Co2MolarRatio;

    [Header("Products (kg/h)")]
    public float methanolProductKgHr;
    public float waterProductKgHr;

    [Header("Unreacted Separator Gas (kg/h)")]
    public float unreactedCo2KgHr;
    public float unreactedH2KgHr;

    [Header("Recycle Streams (kg/h)")]
    public float recycleCo2KgHr;
    public float recycleH2KgHr;
    public float recycleStreamKgHr;

    [Header("Purge Streams (kg/h)")]
    public float purgeCo2KgHr;
    public float purgeH2KgHr;
    public float purgeStreamKgHr;

    [Header("Performance & Validation")]
    [Range(0f, 100f)] public float overallCo2ConversionPercent;
    [Range(0f, 100f)] public float overallH2ConversionPercent;
    public float externalMassBalanceErrorKgHr;
    public float externalMassBalanceErrorPercent;
    public bool converged;
    public int convergenceIterations;
    public string limitingReactant;

    public event Action BalanceUpdated;

    private void Start()
    {
        UpdatePlantMassBalance();
    }

    /// <summary>Recalculates the steady-state recycle loop without frame-by-frame iteration.</summary>
    [ContextMenu("Update Plant Mass Balance")]
    public void UpdatePlantMassBalance()
    {
        double freshCo2 = Math.Max(0d, freshCo2KgHr) / M_CO2;
        double freshH2 = Math.Max(0d, freshH2KgHr) / M_H2;
        double conversion = Math.Clamp(singlePassCo2Conversion, 0d, 0.999d);
        double recycle = Math.Clamp(recycleFraction, 0d, 0.999d);

        double recycleCo2 = 0d;
        double recycleH2 = 0d;
        converged = false;
        convergenceIterations = 0;

        // A fixed-point solution is used instead of one recycle multiplier because
        // it remains valid when either CO2 or H2 becomes the limiting reactant.
        for (int iteration = 1; iteration <= MaxIterations; iteration++)
        {
            double feedCo2 = freshCo2 + recycleCo2;
            double feedH2 = freshH2 + recycleH2;
            double extent = ReactionExtent(feedCo2, feedH2, conversion);
            double nextRecycleCo2 = Math.Max(0d, feedCo2 - extent) * recycle;
            double nextRecycleH2 = Math.Max(0d, feedH2 - 3d * extent) * recycle;

            double change = Math.Max(Math.Abs(nextRecycleCo2 - recycleCo2), Math.Abs(nextRecycleH2 - recycleH2));
            recycleCo2 = nextRecycleCo2;
            recycleH2 = nextRecycleH2;
            convergenceIterations = iteration;

            if (change <= ConvergenceToleranceKmolHr)
            {
                converged = true;
                break;
            }
        }

        double reactorCo2 = freshCo2 + recycleCo2;
        double reactorH2 = freshH2 + recycleH2;
        double reacted = ReactionExtent(reactorCo2, reactorH2, conversion);
        double unreactedCo2 = Math.Max(0d, reactorCo2 - reacted);
        double unreactedH2 = Math.Max(0d, reactorH2 - 3d * reacted);

        // Recalculate the split from the final separator outlet so reported
        // recycle and purge streams are mutually consistent.
        recycleCo2 = unreactedCo2 * recycle;
        recycleH2 = unreactedH2 * recycle;
        double purgeCo2 = unreactedCo2 * (1d - recycle);
        double purgeH2 = unreactedH2 * (1d - recycle);

        reactorFeedCo2KgHr = ToFloat(reactorCo2 * M_CO2);
        reactorFeedH2KgHr = ToFloat(reactorH2 * M_H2);
        totalReactorFeedKgHr = reactorFeedCo2KgHr + reactorFeedH2KgHr;
        reactorH2Co2MolarRatio = reactorCo2 > 1e-12 ? ToFloat(reactorH2 / reactorCo2) : 0f;

        methanolProductKgHr = ToFloat(reacted * M_MEOH);
        waterProductKgHr = ToFloat(reacted * M_H2O);
        unreactedCo2KgHr = ToFloat(unreactedCo2 * M_CO2);
        unreactedH2KgHr = ToFloat(unreactedH2 * M_H2);

        recycleCo2KgHr = ToFloat(recycleCo2 * M_CO2);
        recycleH2KgHr = ToFloat(recycleH2 * M_H2);
        recycleStreamKgHr = recycleCo2KgHr + recycleH2KgHr;
        purgeCo2KgHr = ToFloat(purgeCo2 * M_CO2);
        purgeH2KgHr = ToFloat(purgeH2 * M_H2);
        purgeStreamKgHr = purgeCo2KgHr + purgeH2KgHr;

        overallCo2ConversionPercent = freshCo2 > 1e-12 ? ToFloat(Math.Clamp(reacted / freshCo2, 0d, 1d) * 100d) : 0f;
        overallH2ConversionPercent = freshH2 > 1e-12 ? ToFloat(Math.Clamp(3d * reacted / freshH2, 0d, 1d) * 100d) : 0f;

        double possibleByCo2 = reactorCo2 * conversion;
        double possibleByH2 = reactorH2 / 3d;
        limitingReactant = Math.Abs(possibleByCo2 - possibleByH2) <= 1e-7
            ? "Stoichiometric"
            : possibleByCo2 < possibleByH2 ? "CO2 (conversion-limited)" : "H2";

        double externalInput = freshCo2KgHr + freshH2KgHr;
        double externalOutput = methanolProductKgHr + waterProductKgHr + purgeCo2KgHr + purgeH2KgHr;
        externalMassBalanceErrorKgHr = ToFloat(externalInput - externalOutput);
        externalMassBalanceErrorPercent = externalInput > 1e-9
            ? ToFloat(Math.Abs(externalMassBalanceErrorKgHr) / externalInput * 100d)
            : 0f;

        BalanceUpdated?.Invoke();
    }

    public void SetFreshFeeds(float co2KgHr, float h2KgHr)
    {
        freshCo2KgHr = Mathf.Max(0f, co2KgHr);
        freshH2KgHr = Mathf.Max(0f, h2KgHr);
        UpdatePlantMassBalance();
    }

    /// <summary>Applies a complete operating point and solves it once.</summary>
    public void ConfigureAndCalculate(float co2KgHr, float h2KgHr, float conversion, float recycle)
    {
        freshCo2KgHr = Mathf.Max(0f, co2KgHr);
        freshH2KgHr = Mathf.Max(0f, h2KgHr);
        singlePassCo2Conversion = Mathf.Clamp(conversion, 0f, 0.999f);
        recycleFraction = Mathf.Clamp(recycle, 0f, 0.999f);
        UpdatePlantMassBalance();
    }

    /// <summary>
    /// Splits a total fresh-feed mass flow using the requested H2/CO2 molar ratio.
    /// Suitable for direct calls from paired flow and ratio UI controls.
    /// </summary>
    public void SetFeedFromTotalFlowAndRatio(float totalFeedKgHr, float molarRatioH2Co2)
    {
        double totalFeed = Math.Max(0d, totalFeedKgHr);
        double ratio = Math.Max(0.1d, molarRatioH2Co2);
        double hydrogenToCo2MassRatio = ratio * M_H2 / M_CO2;
        double co2KgHr = totalFeed / (1d + hydrogenToCo2MassRatio);

        freshCo2KgHr = ToFloat(co2KgHr);
        freshH2KgHr = ToFloat(totalFeed - co2KgHr);
        UpdatePlantMassBalance();
    }

    /// <summary>
    /// Updates the educational single-pass conversion correlation from reactor controls.
    /// The ratio penalty peaks at the stoichiometric H2/CO2 value of 3.0.
    /// </summary>
    public void SetReactorOperatingConditions(float tempCelsius, float pressureBar, float molarRatio, float ghsv)
    {
        singlePassCo2Conversion = CalculateSinglePassConversion(tempCelsius, pressureBar, molarRatio, ghsv);
        UpdatePlantMassBalance();
    }

    public static float CalculateSinglePassConversion(float tempCelsius, float pressureBar, float molarRatio, float ghsv)
    {
        float tempKelvin = tempCelsius + 273.15f;
        const float optimumTemperatureKelvin = 513.15f;
        float temperatureFactor = Mathf.Exp(-0.0005f * Mathf.Pow(tempKelvin - optimumTemperatureKelvin, 2f));
        float pressureFactor = Mathf.Pow(Mathf.Max(1f, pressureBar) / 70f, 0.35f);
        float velocityFactor = Mathf.Pow(8000f / Mathf.Max(1000f, ghsv), 0.2f);
        float ratioFactor = 1f - Mathf.Clamp01(Mathf.Abs(molarRatio - 3f) / 3f) * 0.42f;
        return Mathf.Clamp(0.25f * temperatureFactor * pressureFactor * velocityFactor * ratioFactor, 0.05f, 0.35f);
    }

    public bool IsHotspotAlarmActive(float tempCelsius) => tempCelsius > 260f;

    public void SetSinglePassCo2Conversion(float value)
    {
        singlePassCo2Conversion = Mathf.Clamp(value, 0f, 0.999f);
        UpdatePlantMassBalance();
    }

    public void SetRecycleFraction(float value)
    {
        recycleFraction = Mathf.Clamp(value, 0f, 0.999f);
        UpdatePlantMassBalance();
    }

    private static double ReactionExtent(double reactorCo2KmolHr, double reactorH2KmolHr, double conversion)
    {
        double possibleByCo2Conversion = reactorCo2KmolHr * conversion;
        double possibleByHydrogen = reactorH2KmolHr / 3d;
        return Math.Max(0d, Math.Min(possibleByCo2Conversion, possibleByHydrogen));
    }

    private static float ToFloat(double value)
    {
        return (float)value;
    }

    private void OnValidate()
    {
        UpdatePlantMassBalance();
    }
}
