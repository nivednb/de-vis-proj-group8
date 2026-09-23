using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Executable submission evidence, independent analytical oracles and OFAT data.</summary>
public static class SubmissionValidation
{
    private static int checks;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly StringBuilder Results = new StringBuilder();
    public static string EvidenceDirectory => Path.GetFullPath("docs/evidence");

    [MenuItem("Tools/Power-to-Methanol/Validate Final Submission")]
    public static void Run()
    {
        checks = 0;
        Results.Clear();
        Directory.CreateDirectory(EvidenceDirectory);
        ReleaseValidation.Run();
        RecycleMassBalanceValidation.Run();
        ProfessorFeedbackValidation.Run();
        MassBalanceCsvValidation.Run();
        ValidateRecycle();
        ValidateProcess();
        File.WriteAllText(Path.Combine(EvidenceDirectory, "numerical-validation.txt"),
            "UTC: " + DateTime.UtcNow.ToString("O") + "\nUnity: " + Application.unityVersion +
            "\n" + Results + "\nPASS assertions=" + checks + "\n");
        Debug.Log("SUBMISSION_VALIDATION_PASS assertions=" + checks);
    }

    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Near(double actual, double expected, double tolerance, string message)
    {
        Require(!double.IsNaN(actual) && !double.IsInfinity(actual) &&
            Math.Abs(actual - expected) <= tolerance,
            message + ": expected " + expected.ToString("G12", Inv) + " actual " + actual.ToString("G12", Inv));
    }

    private static void ValidateRecycle()
    {
        var csv = new StringBuilder("fresh_co2_kg_h,fresh_h2_kg_h,single_pass,recycle,expected_meoh_kg_h,actual_meoh_kg_h,error_kg_h,closure_percent,converged\n");
        int cases = 0;
        foreach (float co2 in new[] { 0f, 44.0095f, 1152f })
        foreach (float h2 in new[] { 0f, 2.01588f, 80f, 158f })
        foreach (float x in new[] { 0f, 0.05f, 0.2f, 0.35f })
        foreach (float r in new[] { 0f, 0.65f, 0.95f, 0.99f, 0.999f })
        {
            // Independent closed-form external balance, not a second fixed-point loop:
            // extent = min[x F_CO2 / (1-r(1-x)), F_H2/3].
            double extent = Math.Min(x * (co2 / 44.00950) / (1d - r * (1d - x)), (h2 / 2.01588) / 3d);
            double expected = extent * 32.04186;
            var b = RecycleMassBalanceEngine.Calculate(co2, h2, x, r);
            double tolerance = Math.Max(0.002, expected * 2e-5);
            Require(b.Converged, "Recycle convergence at r=" + r + " x=" + x + " co2=" + co2 + " h2=" + h2);
            Near(b.MethanolProductKgHr, expected, tolerance, "Independent recycle oracle");
            Near(b.PurgeCo2KgHr / 44.00950 + extent, co2 / 44.00950, 0.0001, "Carbon atom balance");
            Near(b.PurgeH2KgHr / 2.01588 + 3 * extent, h2 / 2.01588, 0.0001, "Hydrogen balance");
            Require(b.ExternalMassBalanceErrorPercent < 0.001, "External mass closure r=" + r + " x=" + x + " co2=" + co2 + " h2=" + h2);
            Require(b.PurgeCo2KgHr >= 0 && b.PurgeH2KgHr >= 0, "Nonnegative purge");
            csv.AppendLine(string.Join(",", co2.ToString("R", Inv), h2.ToString("R", Inv), x.ToString("R", Inv), r.ToString("R", Inv), expected.ToString("G12", Inv), b.MethanolProductKgHr.ToString("G12", Inv), (b.MethanolProductKgHr-expected).ToString("G12", Inv), b.ExternalMassBalanceErrorPercent.ToString("G12", Inv), b.Converged));
            cases++;
        }
        File.WriteAllText(Path.Combine(EvidenceDirectory, "recycle-benchmark.csv"), csv.ToString());
        Results.AppendLine("Recycle: " + cases + " cases against independent analytical solution, carbon/hydrogen and external mass closure.");
    }

    private static void ValidateProcess()
    {
        var go = new GameObject("Submission numerical validation");
        try
        {
            var sim = go.AddComponent<PlantProcessSimulator>();
            // Edit-mode AddComponent does not invoke the ordinary runtime Awake lifecycle.
            // Match the existing batch validators' explicit setup before testing exports.
            typeof(PlantProcessSimulator).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(sim, null);
            sim.ResetSimulation();
            var baseline = sim.CurrentInputs;
            foreach (float water in new[] { 0f, 0.01f, 1f, 20f, 100f, 130f })
            foreach (float power in new[] { 0f, 25f, 75f, 100f })
            {
                var i = baseline; i.waterFeed = water; i.electrolyzerPower = power;
                var s = sim.Simulate(i);
                Require(s.h2InputKgH + s.oxygenByproductKgH <= s.waterFeedKgH + 0.001f, "Electrolyzer cannot create mass");
                Near(s.oxygenByproductKgH, s.h2InputKgH * (18.01528-2.01588)/2.01588, 0.001, "Oxygen stoichiometry");
                if (water == 0 || power == 0)
                {
                    Near(s.h2InputKgH, 0, 0.000001, "Zero water/power hydrogen regression");
                    Near(s.oxygenByproductKgH, 0, 0.000001, "Zero water/power oxygen regression");
                    Near(s.methanolProductionKgH, 0, 0.000001, "Zero reactant methanol regression");
                }
            }
            var off = baseline; off.storageInterlockLatched = true;
            Near(sim.Simulate(off).methanolProductionKgH, 0, 0.000001, "Storage shutdown");
            sim.SetRecycleRatio(25f);
            sim.SetReactorTemperature(240f);
            string exported = MassBalanceCsvExporter.BuildCsv(sim);
            Near(sim.MassBalance.recycleFraction, 0.25, 0.000001, "Export uses current recycle control, not engine defaults");
            Near(sim.MassBalance.singlePassCo2Conversion, 0.25, 0.000001, "Export uses current conversion");
            Near(sim.MassBalance.freshCo2KgHr + sim.MassBalance.freshH2KgHr,
                sim.MassBalance.methanolProductKgHr + sim.MassBalance.waterProductKgHr + sim.MassBalance.purgeCo2KgHr + sim.MassBalance.purgeH2KgHr,
                0.002, "Export external stream consistency");
            Require(exported.Contains("\"Gas recycle fraction\",\"f_recycle\",\"25\""), "Exported recycle row matches current control");
            File.WriteAllText(Path.Combine(EvidenceDirectory,"csv-regression.csv"),exported);
            File.WriteAllText(Path.Combine(EvidenceDirectory,"csv-regression-inputs.json"),JsonUtility.ToJson(sim.CurrentInputs,true));
            File.WriteAllText(Path.Combine(EvidenceDirectory,"csv-regression-snapshot.json"),JsonUtility.ToJson(sim.GetSteadyStateSnapshot(),true));
            Results.AppendLine("CSV: current 25% recycle and 240 C controls, updated balance basis and external closure checked.");
            sim.ResetSimulation();
            var stateBefore = JsonUtility.ToJson(sim.CurrentInputs);
            var csv = new StringBuilder("parameter,value,unit,single_pass_percent,methanol_kg_h,recovery_percent\n");
            for (int parameter=0; parameter<5; parameter++)
            for (int n=0; n<13; n++)
            {
                var i=baseline; string name, unit; float value;
                switch(parameter)
                {
                    case 0: name="temperature";unit="degC";value=200f+n*10f;i.temperature=value;break;
                    case 1: name="pressure";unit="bar";value=40f+n*5f;i.pressure=value;break;
                    case 2: name="ratio";unit="mol/mol";value=1.5f+n*0.25f;i.ratio=value;break;
                    case 3: name="feed";unit="percent";value=40f+n*5f;i.reactorFeedFlow=value;break;
                    default: name="recycle";unit="percent";value=n*8f;i.recycleRatio=value;break;
                }
                var s=sim.Simulate(i);
                Require(!float.IsNaN(s.methanolProductionKgH) && s.methanolProductionKgH>=0, "Finite OFAT output");
                csv.AppendLine(string.Join(",", name,value.ToString("R",Inv),unit,s.reactorYieldPercent.ToString("R",Inv),s.methanolProductionKgH.ToString("R",Inv),s.overallEfficiencyPercent.ToString("R",Inv)));
            }
            Require(stateBefore==JsonUtility.ToJson(sim.CurrentInputs), "Hypothetical sweeps must not mutate operating inputs");
            File.WriteAllText(Path.Combine(EvidenceDirectory,"sensitivity.csv"),csv.ToString());
            File.WriteAllText(Path.Combine(EvidenceDirectory,"nominal-inputs.json"),JsonUtility.ToJson(baseline,true));
            File.WriteAllText(Path.Combine(EvidenceDirectory,"nominal-output.json"),JsonUtility.ToJson(sim.Simulate(baseline),true));
            Results.AppendLine("Electrolyzer: 24 water/power cases including zero feed; oxygen stoichiometry; zero-feed downstream output.");
            Results.AppendLine("Process: storage shutdown, 65 OFAT points, and nonmutation of live inputs.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
