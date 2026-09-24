using UnityEngine;

/// <summary>
/// The single colour authority for process streams.
///
/// The legend panel and the pipe routes both read from here, so a pipe can never end up a
/// different colour from the legend row that claims to explain it. Every <see cref="PlantFlowKind"/>
/// maps onto exactly one legend row.
/// </summary>
public static class PlantStreamLegend
{
    public readonly struct Row
    {
        public readonly string Label;
        public readonly Color Color;
        /// <summary>Drawn as a dashed swatch — the recycle loop, which returns gas upstream.</summary>
        public readonly bool Dashed;

        public Row(string label, Color color, bool dashed)
        {
            Label = label;
            Color = color;
            Dashed = dashed;
        }
    }

    public static readonly Color WaterHydrogen = Hex("38BDF8");
    // Pink rather than a green/teal, so the amine loop can't be mistaken for refined methanol.
    public static readonly Color AmineCapturedCo2 = Hex("EC4899");
    public static readonly Color CompressedSyngas = Hex("F59E0B");
    public static readonly Color HotReactorEffluent = Hex("EF4444");
    public static readonly Color CrudeMethanol = Hex("A855F7");
    public static readonly Color RefinedMethanol = Hex("22C55E");

    public static readonly Row[] Rows =
    {
        new Row("Raw water / H₂ stream", WaterHydrogen, false),
        new Row("Amine solvent / captured CO₂", AmineCapturedCo2, false),
        new Row("Compressed syngas (3:1 H₂:CO₂)", CompressedSyngas, false),
        new Row("Hot reactor effluent", HotReactorEffluent, false),
        new Row("Crude methanol / water", CrudeMethanol, false),
        new Row("Refined methanol (>99.85%)", RefinedMethanol, false),
        // Recycle is unconverted synthesis gas returning upstream, so it keeps the syngas
        // colour and is distinguished by the dashed swatch rather than by a second hue.
        new Row("Gas recycle loop", CompressedSyngas, true),
    };

    /// <summary>The legend colour a given pipe route must be drawn in.</summary>
    public static Color ColorFor(PlantFlowKind kind) => kind switch
    {
        PlantFlowKind.Hydrogen or PlantFlowKind.HydrogenFromStorage => WaterHydrogen,
        PlantFlowKind.CarbonDioxide or PlantFlowKind.RichAmine or PlantFlowKind.LeanAmine => AmineCapturedCo2,
        PlantFlowKind.MixedFeed or PlantFlowKind.SyngasCold or PlantFlowKind.SyngasHeated or
            PlantFlowKind.RecycleGas => CompressedSyngas,
        PlantFlowKind.ReactorEffluent => HotReactorEffluent,
        PlantFlowKind.CrudeMethanolVapourLiquid or PlantFlowKind.LiquidCrudeMethanol => CrudeMethanol,
        PlantFlowKind.MethanolProduct => RefinedMethanol,
        _ => Color.white
    };

    private static Color Hex(string rgb) =>
        ColorUtility.TryParseHtmlString("#" + rgb, out Color color) ? color : Color.white;
}
