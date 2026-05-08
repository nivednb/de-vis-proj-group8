using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReactorController : MonoBehaviour
{
    [Header("Sliders")]
    public Slider temperatureSlider;
    public Slider pressureSlider;
    public Slider flowRateSlider;
    public Slider ratioSlider;

    [Header("Value Labels")]
    public TextMeshProUGUI temperatureText;
    public TextMeshProUGUI pressureText;
    public TextMeshProUGUI flowRateText;
    public TextMeshProUGUI ratioText;

    [Header("Output")]
    public TextMeshProUGUI outputText;

    void Start()
    {
        temperatureSlider.minValue = 200f;
        temperatureSlider.maxValue = 300f;
        temperatureSlider.value = 250f;

        pressureSlider.minValue = 50f;
        pressureSlider.maxValue = 100f;
        pressureSlider.value = 75f;

        // Space velocity
        flowRateSlider.minValue = 5000f;
        flowRateSlider.maxValue = 10000f;
        flowRateSlider.value = 8000f;

        // H2/CO2 ratio
        ratioSlider.minValue = 2.5f;
        ratioSlider.maxValue = 5f;
        ratioSlider.value = 3f;

        UpdateUI();
    }

    public void OnSliderChanged()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        float T = temperatureSlider.value;
        float P = pressureSlider.value;
        float F = flowRateSlider.value;  // Space Velocity
        float R = ratioSlider.value;     // H2/CO2 Ratio

        temperatureText.text = $"Temperature: {T:F0} C";
        pressureText.text = $"Pressure: {P:F0} bar";
        flowRateText.text = $"Space Velocity: {F:F0} h-1";
        ratioText.text = $"H2/CO2 Ratio: {R:F1}";

        // Real formula from CEE doc
        // Y = K * (P/100) * (R/3) * exp(-((T-250)/35)^2) * (8000/V)
        float K = 10f; // scaling constant
        float exponent = -Mathf.Pow((T - 250f) / 35f, 2);
        float yield = K * (P / 100f) * (R / 3f) * Mathf.Exp(exponent) * (8000f / F);

        // Warning thresholds
        string warning = "";
        if (T > 270f) warning += "WARNING: Catalyst sintering risk!\n";
        if (P < 60f) warning += "WARNING: Low conversion efficiency!\n";
        if (R < 2.5f) warning += "WARNING: Hydrogen deficiency!\n";
        if (F > 9500f) warning += "WARNING: Low residence time!\n";

        if (warning == "")
            outputText.text = $"Est. Methanol Yield: {yield:F2} mol/s";
        else
            outputText.text = warning + $"Yield: {yield:F2} mol/s";
    }
}