using UnityEngine;

/// <summary>Displays catalyst operating condition using calculated reactor state.</summary>
[DisallowMultipleComponent]
public sealed class CatalystBedColorAnimator : MonoBehaviour
{
    [SerializeField] Color idle = new(.72f, .60f, .24f, 1f);
    [SerializeField] Color active = new(.12f, .78f, .40f, 1f);
    [SerializeField] Color converting = new(1f, .48f, .04f, 1f);
    [SerializeField] Color overTemperature = new(1f, .08f, .02f, 1f);
    Renderer target;
    Material materialInstance;
    PlantProcessSimulator simulator;

    void Start()
    {
        target = GetComponent<Renderer>();
        if (target == null) target = GetComponentInChildren<Renderer>(true);
        if (target == null) return;
        target.enabled = true;
        materialInstance = target.material;
        simulator = PlantProcessSimulator.Instance;
        if (simulator != null)
        {
            simulator.SnapshotUpdated += Apply;
            Apply(simulator.Current);
        }
    }

    void OnDestroy()
    {
        if (simulator != null) simulator.SnapshotUpdated -= Apply;
        if (materialInstance != null) Destroy(materialInstance);
    }

    void Apply(PlantProcessSimulator.ProcessSnapshot snapshot)
    {
        if (materialInstance == null) return;
        float load = Mathf.Clamp01(snapshot.syngasFeedKgH / 1725f);
        float conversion = Mathf.Clamp01(snapshot.reactorYieldPercent / 100f);
        Color operating = Color.Lerp(active, converting, conversion);
        Color color = Color.Lerp(idle, operating, load);
        if (snapshot.reactorTemperatureC > 275f)
            color = Color.Lerp(color, overTemperature, Mathf.InverseLerp(275f, 300f, snapshot.reactorTemperatureC));
        if (materialInstance.HasProperty("_BaseColor")) materialInstance.SetColor("_BaseColor", color);
        if (materialInstance.HasProperty("_Color")) materialInstance.SetColor("_Color", color);
        if (materialInstance.HasProperty("_EmissionColor"))
        {
            materialInstance.EnableKeyword("_EMISSION");
            materialInstance.SetColor("_EmissionColor", color * (.04f + conversion * .08f));
        }
    }
}
