using UnityEngine;

/// <summary>
/// Shared visual building blocks for the analytics graphs (LiveGraphRuntime,
/// CorrelationGraphRuntime) — the module color palette used for change markers/legends,
/// and a procedurally generated round-dot sprite (uGUI has no built-in circle) cached once
/// and reused everywhere so every graph's markers/legend look identical.
/// </summary>
public static class GraphVisualUtils
{
    // Every module that exposes sliders gets its own marker color, shared across all
    // graphs and shown in each graph's legend — so "which module was just adjusted" reads
    // at a glance regardless of which graph you're looking at. Keyed by the exact module
    // title InteractiveModulePanelRuntime passes into PlantProcessSimulator.CommitManualChange.
    public static readonly (string Module, Color Color)[] ModulePalette =
    {
        ("Electrolyzer", new Color(0.13f, 0.72f, 0.91f)),
        ("CO2 Absorber", new Color(0.13f, 0.79f, 0.59f)),
        ("Desorber / Regenerator", new Color(1f, 0.69f, 0.13f)),
        ("Compressor", new Color(0.66f, 0.33f, 0.97f)),
        ("Methanol Reactor", new Color(1f, 0.44f, 0.26f)),
        ("Condenser", new Color(0.31f, 0.76f, 0.97f)),
        ("Separator + Recycle", new Color(0.93f, 0.25f, 0.48f)),
        ("Distillation Column", new Color(0.61f, 0.80f, 0.40f)),
    };

    public static Color GetModuleColor(string module)
    {
        foreach (var entry in ModulePalette)
        {
            if (entry.Module == module) return entry.Color;
        }
        return new Color(0.86f, 0.82f, 0.95f, 1f);
    }

    /// <summary>
    /// Engineering unit for a control slider, keyed by the exact label
    /// InteractiveModulePanelRuntime.CreateControls gives it. Returns "" for dimensionless
    /// ratios. Used so every value shown in a graph tooltip / marker carries its unit.
    /// </summary>
    public static string GetParameterUnit(string parameterLabel)
    {
        switch (parameterLabel)
        {
            case "Temp":
            case "Regen temp":
            case "Cooling temp":
            case "Sep. temp":
            case "Reboiler temp":
                return "°C";
            case "Pressure":
            case "Outlet press.":
                return "bar";
            case "GHSV":
                return "1/h";
            case "Plant load":
            case "Power":
            case "Water feed":
            case "Amine flow":
            case "Flue gas":
            case "Steam flow":
            case "Cooling flow":
            case "Recycle ratio":
            case "Feed flow":
                return "%";
            default:
                return ""; // H2/CO2, Comp. ratio, Reflux ratio — dimensionless
        }
    }

    /// <summary>Formats a value with its unit ("82.3 bar"), or just the number when the unit
    /// is empty ("3.0").</summary>
    public static string FormatValue(float value, string unit, string numberFormat = "0.##")
    {
        return string.IsNullOrEmpty(unit) ? value.ToString(numberFormat) : $"{value.ToString(numberFormat)} {unit}";
    }

    private static Sprite cachedCircleSprite;

    public static Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null) return cachedCircleSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(radius - dist + 1f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        cachedCircleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return cachedCircleSprite;
    }
}
