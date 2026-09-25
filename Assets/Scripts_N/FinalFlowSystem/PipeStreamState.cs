using UnityEngine;

/// <summary>
/// Physical state of one process stream, evaluated from the live plant snapshot.
///
/// This is the single source of truth for "how much is actually flowing in this pipe".
/// The mass flows come straight out of <see cref="PlantProcessSimulator"/>'s balances, and
/// everything derived from them uses standard relations rather than invented numbers:
///
///   mass flow        m_dot = rho * A * v                 (so v = m_dot / (rho * A))
///   volumetric flow  Q     = m_dot / rho = A * v
///   gas density      rho   = P * M / (R * T)             ideal gas, R = 8.314462 J/(mol K)
///   pipe bore area   A     = pi * D^2 / 4
///
/// Mixture molar masses are computed from the component molar flows implied by the
/// stoichiometry the simulator itself uses (CO2 + 3 H2 -> CH3OH + H2O), not assumed.
///
/// Real gases at 70 bar deviate from ideal by a few percent (Z ~ 1.0-1.05 for these
/// H2-rich mixtures), and liquid densities use a linear thermal-expansion term. Both are
/// the normal educational treatment; this remains a teaching model, not a property package.
/// </summary>
public enum StreamPhase { Gas, Liquid, TwoPhase }

public readonly struct PipeStreamState
{
    public readonly PlantFlowKind Kind;
    public readonly string Name;
    public readonly StreamPhase Phase;
    /// <summary>Mass flow in kg/h — the quantity the probe reports.</summary>
    public readonly float MassFlowKgH;
    public readonly float TemperatureC;
    public readonly float PressureBar;
    public readonly float DensityKgM3;
    /// <summary>Mixture molar mass in g/mol; 0 for liquid streams.</summary>
    public readonly float MolarMassGMol;
    /// <summary>Nominal line bore in mm (see <see cref="InnerDiameterMm"/> table).</summary>
    public readonly float BoreMm;
    public readonly float VelocityMS;
    public readonly float VolumetricFlowM3H;
    /// <summary>Human-readable composition or loading note.</summary>
    public readonly string Composition;

    public PipeStreamState(PlantFlowKind kind, string name, StreamPhase phase, float massFlowKgH,
        float temperatureC, float pressureBar, float densityKgM3, float molarMassGMol,
        float boreMm, string composition)
    {
        Kind = kind;
        Name = name;
        Phase = phase;
        MassFlowKgH = Mathf.Max(0f, massFlowKgH);
        TemperatureC = temperatureC;
        PressureBar = pressureBar;
        DensityKgM3 = Mathf.Max(1e-4f, densityKgM3);
        MolarMassGMol = molarMassGMol;
        BoreMm = boreMm;
        Composition = composition;

        float area = Mathf.PI * boreMm * boreMm * 1e-6f / 4f;   // mm^2 -> m^2
        VolumetricFlowM3H = MassFlowKgH / DensityKgM3;
        VelocityMS = area > 1e-9f ? VolumetricFlowM3H / 3600f / area : 0f;
    }

    // ---- constants ---------------------------------------------------------

    private const float R = 8.314462f;              // J / (mol K)
    private const float MH2 = 2.016f;               // g/mol
    private const float MCO2 = 44.01f;
    private const float MCO = 28.01f;
    private const float MMeOH = 32.042f;
    private const float MH2O = 18.015f;
    /// <summary>Recycle gas is ~74% H2, 20% CO2, 6% CO/inerts by mole — 11.97 g/mol.</summary>
    private const float MRecycle = 0.74f * MH2 + 0.20f * MCO2 + 0.06f * MCO;
    /// <summary>kg CO2 carried per kg of 30 wt% MEA solution at a 0.25 mol/mol cyclic loading.</summary>
    private const float AmineCo2PerKg = 0.05405f;
    private const float MeaMassFraction = 0.30f;
    private const float MMea = 61.08f;

    /// <summary>
    /// Nominal schedule-40 bores, chosen so the design case lands inside normal process
    /// piping practice — roughly 15-30 m/s for gas lines and 1-3 m/s for liquid lines.
    /// </summary>
    public static float InnerDiameterMm(PlantFlowKind kind) => kind switch
    {
        PlantFlowKind.Hydrogen or PlantFlowKind.HydrogenFromStorage => 40.9f,   // DN40
        PlantFlowKind.CarbonDioxide => 26.6f,                                   // DN25
        PlantFlowKind.RichAmine or PlantFlowKind.LeanAmine => 62.7f,            // DN65
        PlantFlowKind.RecycleGas => 21.7f,                                      // DN20
        PlantFlowKind.MixedFeed or PlantFlowKind.SyngasCold or
            PlantFlowKind.SyngasHeated or PlantFlowKind.ReactorEffluent or
            PlantFlowKind.CrudeMethanolVapourLiquid => 52.5f,                   // DN50
        PlantFlowKind.LiquidCrudeMethanol => 26.6f,                             // DN25
        PlantFlowKind.MethanolProduct => 21.7f,                                 // DN20
        PlantFlowKind.Water => 26.6f,                                           // DN25
        _ => 52.5f
    };

    /// <summary>Nominal-bore label for the readout ("DN50").</summary>
    public static string NominalBoreLabel(PlantFlowKind kind)
    {
        float d = InnerDiameterMm(kind);
        if (d < 24f) return "DN20";
        if (d < 34f) return "DN25";
        if (d < 48f) return "DN40";
        if (d < 58f) return "DN50";
        return "DN65";
    }

    public static string DisplayName(PlantFlowKind kind) => kind switch
    {
        PlantFlowKind.Hydrogen => "Hydrogen from electrolyzer",
        PlantFlowKind.HydrogenFromStorage => "Hydrogen from buffer storage",
        PlantFlowKind.CarbonDioxide => "Captured CO2 to compression",
        PlantFlowKind.RichAmine => "Rich amine to regenerator",
        PlantFlowKind.LeanAmine => "Lean amine to absorber",
        PlantFlowKind.RecycleGas => "Recycle gas to mixing",
        PlantFlowKind.MixedFeed => "Mixed synthesis feed",
        PlantFlowKind.SyngasCold => "Compressed syngas (cold)",
        PlantFlowKind.SyngasHeated => "Heated syngas to reactor",
        PlantFlowKind.ReactorEffluent => "Reactor effluent",
        PlantFlowKind.CrudeMethanolVapourLiquid => "Condensed effluent to separator",
        PlantFlowKind.LiquidCrudeMethanol => "Crude methanol to distillation",
        PlantFlowKind.MethanolProduct => "Refined methanol product",
        PlantFlowKind.Water => "Demineralised water to electrolyzer",
        _ => kind.ToString()
    };

    // ---- property correlations ---------------------------------------------

    /// <summary>Ideal-gas density: rho = P M / (R T), with P in bar, T in Celsius.</summary>
    public static float GasDensity(float pressureBar, float temperatureC, float molarMassGMol)
    {
        float t = Mathf.Max(1f, temperatureC + 273.15f);
        return pressureBar * 1e5f * (molarMassGMol * 1e-3f) / (R * t);
    }

    private static float MethanolDensity(float t) => Mathf.Max(300f, 791.8f - 0.95f * (t - 20f));
    private static float WaterDensity(float t) => Mathf.Max(600f, 998.2f - 0.35f * (t - 20f));
    private static float LeanAmineDensity(float t) => Mathf.Max(700f, 1012f - 0.60f * (t - 20f));
    private static float RichAmineDensity(float t) => Mathf.Max(700f, 1062f - 0.60f * (t - 20f));

    // ---- stream evaluation --------------------------------------------------

    /// <summary>
    /// Evaluates the stream carried by a given pipe route from the current plant snapshot.
    /// Every mass flow here closes against the simulator's own balances.
    /// </summary>
    public static PipeStreamState Evaluate(PlantFlowKind kind, PlantProcessSimulator.ProcessSnapshot s)
    {
        float h2 = Mathf.Max(0f, s.h2InputKgH);
        float co2 = Mathf.Max(0f, s.co2CapturedKgH);
        float freshFeed = Mathf.Max(0f, s.syngasFeedKgH);          // fresh H2 + CO2 routed to the reactor
        float recycle = Mathf.Max(0f, s.recycleGasKgH);
        float meoh = Mathf.Max(0f, s.methanolProductionKgH);
        float water = meoh * (MH2O / MMeOH);                        // 1 mol water per mol methanol
        float crude = meoh + water;
        float reactorIn = freshFeed + recycle;                      // mass is conserved across the reactor
        float reactorP = Mathf.Max(1f, s.reactorPressureBar);
        float reactorT = s.reactorTemperatureC;
        float sepT = s.separatorTemperatureC;

        // Split the fresh feed back into its H2 and CO2 parts — the simulator throttles both
        // by the same reactor-feed factor, so their ratio is preserved.
        float freshTotal = Mathf.Max(1e-4f, h2 + co2);
        float h2Feed = freshFeed * (h2 / freshTotal);
        float co2Feed = freshFeed * (co2 / freshTotal);

        switch (kind)
        {
            case PlantFlowKind.Hydrogen:
            case PlantFlowKind.HydrogenFromStorage:
            {
                const float t = 30f, p = 30f;
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Gas, h2, t, p,
                    GasDensity(p, t, MH2), MH2, InnerDiameterMm(kind), "H2 100% (mol)");
            }

            case PlantFlowKind.CarbonDioxide:
            {
                const float t = 40f, p = 25f;
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Gas, co2, t, p,
                    GasDensity(p, t, MCO2), MCO2, InnerDiameterMm(kind), "CO2 100% (mol)");
            }

            case PlantFlowKind.LeanAmine:
            case PlantFlowKind.RichAmine:
            {
                // Circulation follows the CO2 actually absorbed at a 0.25 mol/mol cyclic
                // loading, shifted by the amine-flow setting: turning it up over-circulates
                // (lower loading per kg), turning it down under-circulates.
                float solvent = (co2 / AmineCo2PerKg) * Mathf.Clamp(s.amineFlowPercent / 65f, 0.25f, 2.2f);
                bool rich = kind == PlantFlowKind.RichAmine;
                float flow = rich ? solvent + co2 : solvent;
                float t = rich ? 50f : 45f;
                float p = rich ? 2.5f : 3.5f;
                float rho = rich ? RichAmineDensity(t) : LeanAmineDensity(t);
                float meaKmol = solvent * MeaMassFraction / MMea;
                float loading = meaKmol > 1e-6f ? (co2 / MCO2) / meaKmol : 0f;
                string note = rich
                    ? $"30% MEA, CO2 loading {loading:F2} mol/mol"
                    : "30% MEA, regenerated";
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Liquid, flow, t, p,
                    rho, 0f, InnerDiameterMm(kind), note);
            }

            case PlantFlowKind.RecycleGas:
            {
                float t = Mathf.Max(5f, sepT);
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Gas, recycle, t, reactorP,
                    GasDensity(reactorP, t, MRecycle), MRecycle, InnerDiameterMm(kind),
                    "H2 74% · CO2 20% · CO/inert 6% (mol)");
            }

            case PlantFlowKind.MixedFeed:
            case PlantFlowKind.SyngasCold:
            case PlantFlowKind.SyngasHeated:
            {
                float nH2 = h2Feed / MH2;
                float nCO2 = co2Feed / MCO2;
                float nRec = recycle / MRecycle;
                float nTotal = Mathf.Max(1e-6f, nH2 + nCO2 + nRec);
                float m = reactorIn / nTotal;
                float p = kind == PlantFlowKind.MixedFeed ? 30f : reactorP;
                float t = kind switch
                {
                    PlantFlowKind.MixedFeed => 45f,
                    PlantFlowKind.SyngasCold => 40f,
                    _ => Mathf.Max(40f, reactorT - 25f)
                };
                string note = $"H2 {nH2 / nTotal * 100f:F0}% · CO2 {nCO2 / nTotal * 100f:F0}% · recycle {nRec / nTotal * 100f:F0}% (mol)";
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Gas, reactorIn, t, p,
                    GasDensity(p, t, m), m, InnerDiameterMm(kind), note);
            }

            case PlantFlowKind.ReactorEffluent:
            case PlantFlowKind.CrudeMethanolVapourLiquid:
            {
                // CO2 + 3 H2 -> CH3OH + H2O consumes 4 mol and produces 2, so the effluent
                // has fewer moles than the feed at the same mass.
                float nMeOH = meoh / MMeOH;
                float nH2 = Mathf.Max(0f, h2Feed / MH2 - 3f * nMeOH);
                float nCO2 = Mathf.Max(0f, co2Feed / MCO2 - nMeOH);
                float nRec = recycle / MRecycle;
                float nTotal = Mathf.Max(1e-6f, nH2 + nCO2 + nRec + nMeOH + nMeOH);
                float m = reactorIn / nTotal;
                bool effluent = kind == PlantFlowKind.ReactorEffluent;
                float t = effluent ? reactorT : Mathf.Max(5f, sepT);
                float p = Mathf.Max(1f, reactorP - (effluent ? 2f : 3f));
                float meohFrac = nMeOH / nTotal * 100f;
                string note = $"CH3OH {meohFrac:F0}% · H2O {meohFrac:F0}% · unreacted gas {(100f - 2f * meohFrac):F0}% (mol)";
                if (effluent)
                {
                    return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Gas, reactorIn, t, p,
                        GasDensity(p, t, m), m, InnerDiameterMm(kind), note);
                }

                // After the condenser the line runs two-phase: condensed crude plus the gas
                // that stays uncondensed. Report the mixed-phase (slip-free) bulk density.
                float liquidRho = crude > 1e-4f
                    ? (meoh * MethanolDensity(t) + water * WaterDensity(t)) / crude
                    : MethanolDensity(t);
                float gasMass = Mathf.Max(0f, reactorIn - crude);
                float gasRho = GasDensity(p, t, m);
                float volume = crude / Mathf.Max(1e-4f, liquidRho) + gasMass / Mathf.Max(1e-4f, gasRho);
                float bulkRho = volume > 1e-9f ? reactorIn / volume : liquidRho;
                float liquidFraction = reactorIn > 1e-4f ? crude / reactorIn * 100f : 0f;
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.TwoPhase, reactorIn, t, p,
                    bulkRho, m, InnerDiameterMm(kind), $"{liquidFraction:F0}% condensed by mass");
            }

            case PlantFlowKind.LiquidCrudeMethanol:
            {
                float t = Mathf.Max(5f, sepT);
                float rho = crude > 1e-4f
                    ? (meoh * MethanolDensity(t) + water * WaterDensity(t)) / crude
                    : MethanolDensity(t);
                float meohWt = crude > 1e-4f ? meoh / crude * 100f : 0f;
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Liquid, crude, t, 3f,
                    rho, 0f, InnerDiameterMm(kind), $"CH3OH {meohWt:F0} wt% · balance water");
            }

            case PlantFlowKind.MethanolProduct:
            {
                const float t = 40f;
                float purity = Mathf.Clamp(s.methanolPurityPercent, 0f, 100f);
                float rho = (purity * MethanolDensity(t) + (100f - purity) * WaterDensity(t)) / 100f;
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Liquid, meoh, t, 2.5f,
                    rho, 0f, InnerDiameterMm(kind), $"CH3OH {purity:F2} wt%");
            }

            case PlantFlowKind.Water:
            {
                // Treated water from the RO skid, pumped to the electrolyzer feed.
                const float t = 20f, p = 4f;
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Liquid, s.waterFeedKgH, t, p,
                    WaterDensity(t), 0f, InnerDiameterMm(kind), "Demineralised H2O, conductivity < 1 µS/cm");
            }

            default:
                return new PipeStreamState(kind, DisplayName(kind), StreamPhase.Gas, 0f, 25f, 1f,
                    1.2f, 28.96f, InnerDiameterMm(kind), "—");
        }
    }

    /// <summary>Design mass flow for each route, used only to normalise the visual speed
    /// and density of the flow animation.</summary>
    public static float DesignMassFlowKgH(PlantFlowKind kind) => kind switch
    {
        PlantFlowKind.Hydrogen or PlantFlowKind.HydrogenFromStorage => 215f,
        PlantFlowKind.CarbonDioxide => 1208f,
        PlantFlowKind.RichAmine or PlantFlowKind.LeanAmine => 22350f,
        PlantFlowKind.RecycleGas => 450f,
        PlantFlowKind.MixedFeed or PlantFlowKind.SyngasCold or
            PlantFlowKind.SyngasHeated or PlantFlowKind.ReactorEffluent or
            PlantFlowKind.CrudeMethanolVapourLiquid => 2175f,
        PlantFlowKind.LiquidCrudeMethanol => 1953f,
        PlantFlowKind.MethanolProduct => 1250f,
        PlantFlowKind.Water => 1935f,
        _ => 1000f
    };
}
