using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReactorUI : MonoBehaviour
{
    public MethanolReactor reactor;

    [Header("Sliders")]
    public Slider temperatureSlider;
    public Slider pressureSlider;
    public Slider ghsvSlider;
    public Slider ratioSlider;

    [Header("Texts")]
    public TMP_Text temperatureText;
    public TMP_Text pressureText;
    public TMP_Text ghsvText;
    public TMP_Text ratioText;
    public TMP_Text yieldText;
    public TMP_Text methanolText;
    public TMP_Text statusText;

    [Header("Warning")]
    public GameObject warningBanner;

    void Start()
    {
        temperatureSlider.minValue = 200;
        temperatureSlider.maxValue = 300;
        temperatureSlider.value = reactor.temperature;

        pressureSlider.minValue = 20;
        pressureSlider.maxValue = 100;
        pressureSlider.value = reactor.pressure;

        ghsvSlider.minValue = 1000;
        ghsvSlider.maxValue = 20000;
        ghsvSlider.value = reactor.ghsv;

        ratioSlider.minValue = 1;
        ratioSlider.maxValue = 5;
        ratioSlider.value = reactor.ratio;
    }

    void Update()
    {
        reactor.temperature = temperatureSlider.value;
        reactor.pressure = pressureSlider.value;
        reactor.ghsv = ghsvSlider.value;
        reactor.ratio = ratioSlider.value;

        temperatureText.text = "Temperature: " + reactor.temperature.ToString("F0") + " °C";
        pressureText.text = "Pressure: " + reactor.pressure.ToString("F0") + " bar";
        ghsvText.text = "GHSV: " + reactor.ghsv.ToString("F0") + " h⁻¹";
        ratioText.text = "H₂/CO₂ Ratio: " + reactor.ratio.ToString("F1");

        yieldText.text = "Yield: " + reactor.yieldPercent.ToString("F1") + " %";
        methanolText.text = "Methanol: " + reactor.methanolKgPerHour.ToString("F1") + " kg/h";

        if (reactor.hotspotWarning)
        {
            statusText.text = "Status: Hotspot Risk";
            warningBanner.SetActive(true);
        }
        else
        {
            statusText.text = "Status: Normal";
            warningBanner.SetActive(false);
        }
    }
}