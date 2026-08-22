using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Creates an audit-friendly snapshot of the educational plant mass balance.</summary>
public static class MassBalanceCsvExporter
{
    private static readonly CultureInfo CsvCulture = CultureInfo.InvariantCulture;

    public static string Export(PlantProcessSimulator simulator)
    {
        if (simulator == null) throw new ArgumentNullException(nameof(simulator));
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Power-to-Methanol Exports");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"PtMeOH_MassBalance_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllText(path, BuildCsv(simulator), new UTF8Encoding(true));
        return path;
    }

    public static string BuildCsv(PlantProcessSimulator simulator)
    {
        if (simulator == null) throw new ArgumentNullException(nameof(simulator));
        RecycleMassBalanceEngine b = simulator.MassBalance;
        if (b == null) throw new InvalidOperationException("Recycle mass-balance engine is unavailable.");
        b.UpdatePlantMassBalance();
        PlantProcessSimulator.ProcessSnapshot s = simulator.Current;
        StringBuilder csv = new StringBuilder(4096);
        csv.AppendLine("section,quantity,symbol,value,unit,basis_or_equation");
        Row(csv, "Metadata", "Export timestamp", "", DateTime.Now.ToString("O", CsvCulture), "ISO-8601", "Local computer time");
        Row(csv, "Basis", "Reaction", "", "CO2 + 3 H2 <-> CH3OH + H2O", "", "One-reaction educational steady-state model");
        Row(csv, "Basis", "Hydrogen molar mass", "M_H2", 2.01588f, "kg/kmol", "IUPAC-consistent value used by model");
        Row(csv, "Basis", "Carbon dioxide molar mass", "M_CO2", 44.00950f, "kg/kmol", "Value used by model");
        Row(csv, "Basis", "Methanol molar mass", "M_CH3OH", 32.04186f, "kg/kmol", "Value used by model");
        Row(csv, "Basis", "Water molar mass", "M_H2O", 18.01528f, "kg/kmol", "Value used by model");

        Row(csv, "Operating point", "Reactor temperature", "T", s.reactorTemperatureC, "degC", "User control");
        Row(csv, "Operating point", "Reactor pressure", "P", s.reactorPressureBar, "bar", "User control");
        Row(csv, "Operating point", "H2/CO2 molar ratio", "R", s.h2Co2Ratio, "mol/mol", "User control");
        Row(csv, "Operating point", "Gas hourly space velocity", "GHSV", s.ghsv, "1/h", "User control");
        Row(csv, "Operating point", "Single-pass CO2 conversion", "X_CO2,sp", b.singlePassCo2Conversion * 100f, "%", "Educational T/P/R/GHSV response correlation");
        Row(csv, "Operating point", "Gas recycle fraction", "f_recycle", b.recycleFraction * 100f, "%", "Separator gas split");

        Row(csv, "External input", "Fresh CO2", "m_CO2,fresh", b.freshCo2KgHr, "kg/h", "Captured CO2 entering synthesis boundary");
        Row(csv, "External input", "Fresh H2", "m_H2,fresh", b.freshH2KgHr, "kg/h", "Electrolytic H2 entering synthesis boundary");
        Row(csv, "Reactor inlet", "CO2 including recycle", "m_CO2,Rin", b.reactorFeedCo2KgHr, "kg/h", "Fresh + recycle");
        Row(csv, "Reactor inlet", "H2 including recycle", "m_H2,Rin", b.reactorFeedH2KgHr, "kg/h", "Fresh + recycle");
        Row(csv, "Reactor product", "Methanol before recovery", "m_CH3OH,rxn", b.methanolProductKgHr, "kg/h", "Reaction extent x M_CH3OH");
        Row(csv, "Reactor product", "Water", "m_H2O,rxn", b.waterProductKgHr, "kg/h", "Reaction extent x M_H2O");
        Row(csv, "Separator gas", "Unreacted CO2", "m_CO2,unreacted", b.unreactedCo2KgHr, "kg/h", "Before recycle/purge split");
        Row(csv, "Separator gas", "Unreacted H2", "m_H2,unreacted", b.unreactedH2KgHr, "kg/h", "Before recycle/purge split");
        Row(csv, "Internal recycle", "Recycle CO2", "m_CO2,recycle", b.recycleCo2KgHr, "kg/h", "Internal stream; excluded from external closure");
        Row(csv, "Internal recycle", "Recycle H2", "m_H2,recycle", b.recycleH2KgHr, "kg/h", "Internal stream; excluded from external closure");
        Row(csv, "External output", "Purge CO2", "m_CO2,purge", b.purgeCo2KgHr, "kg/h", "Unreacted external outlet");
        Row(csv, "External output", "Purge H2", "m_H2,purge", b.purgeH2KgHr, "kg/h", "Unreacted external outlet");
        Row(csv, "External closure", "Total external input", "m_in", b.freshCo2KgHr + b.freshH2KgHr, "kg/h", "Fresh CO2 + fresh H2");
        Row(csv, "External closure", "Total external output", "m_out", b.methanolProductKgHr + b.waterProductKgHr + b.purgeCo2KgHr + b.purgeH2KgHr, "kg/h", "Methanol + water + purge CO2 + purge H2");
        Row(csv, "External closure", "Mass-balance error", "m_in-m_out", b.externalMassBalanceErrorKgHr, "kg/h", "Should approach zero within floating-point tolerance");
        Row(csv, "External closure", "Absolute closure error", "epsilon", b.externalMassBalanceErrorPercent, "%", "abs(error)/input x 100");
        Row(csv, "External closure", "Solver converged", "", b.converged ? "TRUE" : "FALSE", "", $"Iterations: {b.convergenceIterations}");
        Row(csv, "External closure", "Limiting reactant", "", b.limitingReactant, "", "Determined from reactor inlet and conversion constraint");

        Row(csv, "Downstream recovery", "Refined methanol product", "m_CH3OH,refined", s.methanolProductionKgH, "kg/h", "After condenser/separator/distillation recovery factors");
        Row(csv, "Downstream recovery", "Methanol purity", "w_CH3OH", s.methanolPurityPercent, "%", "Educational separation correlation; capped at 99.85%");
        Row(csv, "Downstream recovery", "Condenser recovery", "eta_cond", s.condenserRecoveryPercent, "%", "Educational cooling response");
        Row(csv, "Storage", "Stored methanol", "m_stored", s.storedMethanolKg, "kg", "Accumulated simulated inventory");
        Row(csv, "Storage", "Tank fill", "", s.storageFillPercent, "%", "Relative to configured storage capacity");

        Row(csv, "Assumption", "Steady state synthesis loop", "", "TRUE", "", "No transient holdup inside reactor/separator/recycle calculation");
        Row(csv, "Assumption", "Products removed before recycle", "", "TRUE", "", "Methanol and water do not return in gas recycle");
        Row(csv, "Assumption", "Side reactions and inerts", "", "OMITTED", "", "No CO/shift chemistry, dissolved gases or inert accumulation");
        Row(csv, "Constraint", "Model status", "", "EDUCATIONAL", "", "Not a validated process simulator or plant design basis");
        return csv.ToString();
    }

    private static void Row(StringBuilder csv, string section, string quantity, string symbol,
        float value, string unit, string basis) =>
        Row(csv, section, quantity, symbol, value.ToString("0.######", CsvCulture), unit, basis);

    private static void Row(StringBuilder csv, string section, string quantity, string symbol,
        string value, string unit, string basis)
    {
        csv.Append(Escape(section)).Append(',').Append(Escape(quantity)).Append(',')
            .Append(Escape(symbol)).Append(',').Append(Escape(value)).Append(',')
            .Append(Escape(unit)).Append(',').Append(Escape(basis)).AppendLine();
    }

    private static string Escape(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
}
