using System;
using UnityEditor;
using UnityEngine;

/// <summary>Batch-compatible numerical regression checks for the recycle balance.</summary>
public static class RecycleMassBalanceValidation
{
    [MenuItem("Tools/Nived/Validate Recycle Mass Balance")]
    public static void Run()
    {
        GameObject testObject = new GameObject("Recycle balance validation");
        try
        {
            RecycleMassBalanceEngine engine = testObject.AddComponent<RecycleMassBalanceEngine>();

            engine.freshCo2KgHr = 1152f;
            engine.freshH2KgHr = 158f;
            engine.singlePassCo2Conversion = 0.20f;
            engine.recycleFraction = 0.95f;
            engine.UpdatePlantMassBalance();
            Require(engine.converged, "Default recycle case did not converge.");
            RequireNear(engine.overallCo2ConversionPercent, 83.33f, 0.08f, "Default overall CO2 conversion");
            RequireNear(engine.methanolProductKgHr, 698.8f, 1.0f, "Default methanol production");
            Require(engine.externalMassBalanceErrorPercent < 0.001f, "Default external mass balance does not close.");

            engine.freshCo2KgHr = 1152f;
            engine.freshH2KgHr = 80f;
            engine.singlePassCo2Conversion = 0.35f;
            engine.recycleFraction = 0.90f;
            engine.UpdatePlantMassBalance();
            Require(engine.converged, "Hydrogen-limited case did not converge.");
            Require(engine.limitingReactant == "H2", "Hydrogen-limited case was not identified.");
            Require(engine.unreactedH2KgHr >= -0.001f, "Hydrogen outlet became negative.");
            Require(engine.externalMassBalanceErrorPercent < 0.001f, "Hydrogen-limited mass balance does not close.");

            engine.freshCo2KgHr = 440.095f;
            engine.freshH2KgHr = 60.4764f;
            engine.singlePassCo2Conversion = 0.20f;
            engine.recycleFraction = 0f;
            engine.UpdatePlantMassBalance();
            RequireNear(engine.recycleStreamKgHr, 0f, 0.001f, "Zero-recycle stream");
            RequireNear(engine.overallCo2ConversionPercent, 20f, 0.01f, "Zero-recycle conversion");
            Require(engine.externalMassBalanceErrorPercent < 0.001f, "Zero-recycle mass balance does not close.");

            Debug.Log("RecycleMassBalanceValidation: PASS (default, H2-limited, and zero-recycle cases).");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireNear(float actual, float expected, float tolerance, string label)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }
}
