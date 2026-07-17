using UnityEngine;

public class MethanolReactor : MonoBehaviour
{
    [Header("Inputs")]
    public float temperature = 240f;   // °C
    public float pressure = 60f;       // bar
    public float ghsv = 8000f;         // h^-1
    public float ratio = 3f;           // H2/CO2

    [Header("Particle Systems")]
    public ParticleSystem h2Particles;
    public ParticleSystem co2Particles;
    public ParticleSystem methanolParticles;
    public ParticleSystem waterParticles;

    [Header("Outputs")]
    public float yieldPercent;
    public float methanolKgPerHour;
    public bool hotspotWarning;

    [Header("Visual")]
    public Renderer reactorRenderer;

    void Update()
    {
        CalculateReactor();
        UpdateVisuals();
        UpdateParticleSpeed();
        UpdateParticleDensity();
        UpdateGasRatio();
        UpdateReactionProgress();
    }

    void CalculateReactor()
    {
        float pressureFactor = Mathf.InverseLerp(20f, 100f, pressure);
        float tempFactor = Mathf.Exp(-Mathf.Pow((temperature - 240f) / 35f, 2f));
        float ratioFactor = Mathf.Clamp01(ratio / 3f);
        float ghsvFactor = Mathf.Clamp01(1f - ((ghsv - 1000f) / 19000f) * 0.6f);

        yieldPercent = 100f * pressureFactor * tempFactor * ratioFactor * ghsvFactor;
        yieldPercent = Mathf.Clamp(yieldPercent, 0f, 100f);

        methanolKgPerHour = yieldPercent * 2.5f;
        hotspotWarning = temperature > 260f;
    }

    void UpdateVisuals()
    {
        if (reactorRenderer == null) return;

        float t = Mathf.InverseLerp(200f, 300f, temperature);

        Color cold = new Color(0.2f, 0.5f, 1f);
        Color normal = new Color(0.2f, 1f, 0.4f);
        Color hot = new Color(1f, 0.25f, 0.1f);

        Color finalColor = t < 0.5f
            ? Color.Lerp(cold, normal, t * 2f)
            : Color.Lerp(normal, hot, (t - 0.5f) * 2f);

        reactorRenderer.material.color = finalColor;
    }

    void UpdateParticleSpeed()
    {
        float speed = Mathf.Lerp(0.2f, 1.5f,
            Mathf.InverseLerp(200f, 300f, temperature));

        SetSpeed(h2Particles, speed);
        SetSpeed(co2Particles, speed);
        SetSpeed(methanolParticles, speed);
        SetSpeed(waterParticles, speed);
    }

    void SetSpeed(ParticleSystem ps, float speed)
    {
        if (ps == null) return;

        var main = ps.main;
        main.simulationSpeed = speed;
    }

    void UpdateParticleDensity()
    {
        float pressure01 = Mathf.InverseLerp(20f, 100f, pressure);

        SetEmission(h2Particles, Mathf.Lerp(20f, 90f, pressure01));
        SetEmission(co2Particles, Mathf.Lerp(10f, 60f, pressure01));
        SetEmission(methanolParticles, Mathf.Lerp(5f, 50f, yieldPercent / 100f));
        SetEmission(waterParticles, Mathf.Lerp(3f, 35f, yieldPercent / 100f));
    }

    void SetEmission(ParticleSystem ps, float rate)
    {
        if (ps == null) return;

        var emission = ps.emission;
        emission.rateOverTime = rate;
    }
    void UpdateGasRatio()
    {
        float ratio01 = Mathf.InverseLerp(1f, 5f, ratio);

        // Hydrogen increases with ratio
        SetEmission(h2Particles,
            Mathf.Lerp(25f, 120f, ratio01));

        // CO2 decreases with ratio
        SetEmission(co2Particles,
            Mathf.Lerp(80f, 15f, ratio01));
    }
    void UpdateReactionProgress()
    {
        float y = yieldPercent / 100f;

        // Reactants decrease
        SetEmission(h2Particles,
            Mathf.Lerp(90f, 20f, y));

        SetEmission(co2Particles,
            Mathf.Lerp(70f, 10f, y));

        // Products increase
        SetEmission(methanolParticles,
            Mathf.Lerp(5f, 80f, y));

        SetEmission(waterParticles,
            Mathf.Lerp(5f, 60f, y));
    }
}