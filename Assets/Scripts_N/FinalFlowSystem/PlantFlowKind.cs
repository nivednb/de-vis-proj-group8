using UnityEngine;

/// <summary>
/// Shared flow classification used by the automatic pipe network and by
/// per-pipe manual overrides. Kept separate so Inspector dropdowns are stable.
/// Compatibility aliases are included so older runtime scripts still compile.
/// </summary>
public enum PlantFlowKind
{
    Hydrogen,
    H2 = Hydrogen,

    HydrogenFromStorage,

    CarbonDioxide,
    CO2 = CarbonDioxide,

    RichAmine,
    LeanAmine,
    RecycleGas,
    MixedFeed,

    SyngasCold,

    SyngasHeated,
    SyngasHot = SyngasHeated,
    HotSyngas = SyngasHeated,

    ReactorEffluent,

    CrudeMethanolVapourLiquid,
    CrudeMethanol = CrudeMethanolVapourLiquid,
    CrudeMeOH = CrudeMethanolVapourLiquid,

    LiquidCrudeMethanol,
    LiqCrudeMeOH = LiquidCrudeMethanol,

    MethanolProduct
}

[System.Serializable]
public class PlantFlowRouteSettings
{
    [Header("Detection")]
    public string routeName = "Route";
    public string[] namePrefixes = new string[0];
    public PlantFlowKind flowKind = PlantFlowKind.MixedFeed;

    [Header("Manual Tuning")]
    public bool enabled = true;
    public bool reverseDirection = false;
    [Min(0.05f)] public float speed = 1.8f;
    [Min(0.5f)] public float density = 18f;
    [Range(0f, 1f)] public float pipeAlpha = 0.20f;
    [Range(0f, 2f)] public float flowIntensity = 1.15f;
    [Range(0.1f, 1.5f)] public float bandSharpness = 0.62f;
    public Color flowColor = Color.white;

    [Header("Engineering Note")]
    [TextArea(2, 4)] public string note;
}
