using System;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class MassBalanceCsvValidation
{
    [MenuItem("Tools/Nived/Validate Mass Balance CSV")]
    public static void Run()
    {
        GameObject testObject = new GameObject("Mass balance CSV validation");
        try
        {
            PlantProcessSimulator simulator = testObject.AddComponent<PlantProcessSimulator>();
            MethodInfo awake = typeof(PlantProcessSimulator).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(simulator, null);
            if (!simulator.ApplyMaximumEfficiencyOperatingPoint())
                throw new InvalidOperationException("Maximum-efficiency operating point was not applied.");

            string csv = MassBalanceCsvExporter.BuildCsv(simulator);
            Require(csv.Contains("External closure"), "External closure section missing.");
            Require(csv.Contains("Mass-balance error"), "Closure error row missing.");
            Require(csv.Contains("M_CH3OH"), "Molar-mass basis missing.");
            Require(csv.Contains("Downstream recovery"), "Recovery boundary missing.");
            Require(csv.Contains("Assumption"), "Assumptions missing.");

            RecycleMassBalanceEngine balance = simulator.MassBalance;
            Require(balance.converged, "Recycle calculation did not converge.");
            Require(balance.externalMassBalanceErrorPercent < 0.001f,
                "External mass-balance closure exceeds 0.001%: " +
                balance.externalMassBalanceErrorPercent.ToString("G9", CultureInfo.InvariantCulture));

            Debug.Log($"MASS_BALANCE_CSV_VALIDATION_OK closure={balance.externalMassBalanceErrorPercent:G6}% " +
                $"rows={csv.Split('\n').Length - 1} maxEfficiency={simulator.Current.overallEfficiencyPercent:F2}%");
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
}
