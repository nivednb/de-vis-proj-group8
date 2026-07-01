using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReactorController : MonoBehaviour
{
    [Header("Sliders")]
    public Slider temperatureSlider;
    public Slider pressureSlider;
    public Slider ratioSlider;
    public Slider ghsvSlider;

    [Header("Slider Value Labels")]
    public TextMeshProUGUI tempLabel;
    public TextMeshProUGUI pressureLabel;
    public TextMeshProUGUI ratioLabel;
    public TextMeshProUGUI ghsvLabel;

    [Header("LED Input Field Displays")]
    public TMP_InputField yieldIndexDisplay;
    public TMP_InputField tempFactorDisplay;
    public TMP_InputField pressureFactorDisplay;
    public TMP_InputField ratioFactorDisplay;

    [Header("LED Output TMP Labels (Text Area > Text children)")]
    public TextMeshProUGUI yieldOutput;
    public TextMeshProUGUI tempFactorOutput;
    public TextMeshProUGUI pressureFactorOutput;
    public TextMeshProUGUI ratioFactorOutput;

    [Header("Hotspot Warning")]
    public GameObject warningBanner;
    public Image panelBackground;
    public Color normalColor = Color.white;
    public Color warningColor = new Color(0.99f, 0.92f, 0.92f);

    [Header("Status")]
    public TextMeshProUGUI statusOutput;

    private const float K = 1.0f;
    private const float P_MAX = 100f;
    private const float HOTSPOT_THRESHOLD = 260f;

    private Color ledGreen = new Color(0f, 0.902f, 0.463f);
    private Color ledRed   = new Color(1f, 0.322f, 0.322f);

    void Start()
    {
        temperatureSlider.minValue = 200f;
        temperatureSlider.maxValue = 300f;
        temperatureSlider.value    = 250f;

        pressureSlider.minValue = 40f;
        pressureSlider.maxValue = 100f;
        pressureSlider.value    = 70f;

        ratioSlider.minValue = 1f;
        ratioSlider.maxValue = 6f;
        ratioSlider.value    = 3f;

        ghsvSlider.minValue = 1000f;
        ghsvSlider.maxValue = 20000f;
        ghsvSlider.value    = 8000f;

        if (yieldIndexDisplay)        yieldIndexDisplay.interactable        = false;
        if (tempFactorDisplay)        tempFactorDisplay.interactable        = false;
        if (pressureFactorDisplay)    pressureFactorDisplay.interactable    = false;
        if (ratioFactorDisplay)       ratioFactorDisplay.interactable       = false;

        temperatureSlider.onValueChanged.AddListener(_ => UpdateOutput());
        pressureSlider.onValueChanged.AddListener(_    => UpdateOutput());
        ratioSlider.onValueChanged.AddListener(_       => UpdateOutput());
        ghsvSlider.onValueChanged.AddListener(_        => UpdateOutput());

        UpdateOutput();
    }

    void UpdateOutput()
    {
        float T = temperatureSlider.value;
        float P = pressureSlider.value;
        float R = ratioSlider.value;
        float V = ghsvSlider.value;

        if (tempLabel)     tempLabel.text     = T.ToString("F0") + " °C";
        if (pressureLabel) pressureLabel.text = P.ToString("F0") + " bar";
        if (ratioLabel)    ratioLabel.text    = R.ToString("F1");
        if (ghsvLabel)     ghsvLabel.text     = V.ToString("F0") + " h\u207B\u00B9";

        float tempFactor     = Mathf.Exp(-Mathf.Pow((T - 250f) / 35f, 2));
        float pressureFactor = P / P_MAX;
        float ratioFactor    = R / 3f;
        float ghsvFactor     = 8000f / V;
        float Y              = K * pressureFactor * ratioFactor * tempFactor * ghsvFactor;

        bool hotspot = T > HOTSPOT_THRESHOLD;
        Color ledColor = hotspot ? ledRed : ledGreen;

        if (yieldIndexDisplay)
        {
            yieldIndexDisplay.text = Y.ToString("F4");
            yieldIndexDisplay.textComponent.color = ledColor;
        }
        if (tempFactorDisplay)
        {
            tempFactorDisplay.text = tempFactor.ToString("F4");
            tempFactorDisplay.textComponent.color = ledColor;
        }
        if (pressureFactorDisplay)
        {
            pressureFactorDisplay.text = pressureFactor.ToString("F3");
            pressureFactorDisplay.textComponent.color = ledColor;
        }
        if (ratioFactorDisplay)
        {
            ratioFactorDisplay.text = ratioFactor.ToString("F3");
            ratioFactorDisplay.textComponent.color = ledColor;
        }

        if (yieldOutput)          yieldOutput.text          = Y.ToString("F4");
        if (tempFactorOutput)     tempFactorOutput.text     = tempFactor.ToString("F4");
        if (pressureFactorOutput) pressureFactorOutput.text = pressureFactor.ToString("F3");
        if (ratioFactorOutput)    ratioFactorOutput.text    = ratioFactor.ToString("F3");

        if (warningBanner)   warningBanner.SetActive(hotspot);
        if (panelBackground) panelBackground.color = hotspot ? warningColor : normalColor;

        if (statusOutput)
        {
            statusOutput.text  = hotspot ? "Hotspot risk" : "Normal";
            statusOutput.color = hotspot ? Color.red : ledGreen;
        }
    }
}
