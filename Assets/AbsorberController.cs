using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbsorberController : MonoBehaviour
{
    [Header("Sliders")]
    public Slider amineFlowSlider;
    public Slider regenTempSlider;

    [Header("Output Text Children")]
    public TMP_Text efficiencyOutput;
    public TMP_Text amineFactorOutput;
    public TMP_Text tempFactorOutput;

    [Header("Efficiency Color Bar")]
    public Image efficiencyBarImage;

    [Header("Warning")]
    public CanvasGroup absorberWarningBanner;
    public GameObject panelBackgroundObject;
    private Image panelBackground;

    [Header("Smooth Speed")]
    public float smoothSpeed = 5f;

    private readonly Color lowColor = new Color(0.89f, 0.29f, 0.29f);
    private readonly Color midColor = new Color(0.94f, 0.62f, 0.15f);
    private readonly Color highColor = new Color(0.11f, 0.62f, 0.46f);
    private readonly Color normalColor = new Color(0.96f, 0.96f, 0.96f);
    private readonly Color warningColor = new Color(1f, 0.92f, 0.92f);

    private const float F_MAX = 100f;
    private const float T_MIN = 80f;
    private const float BAR_WIDTH = 300f;

    private float targetEta;
    private float currentEta;
    private float targetAmineFactor;
    private float currentAmineFactor;
    private float targetTempFactor;
    private float currentTempFactor;

    void Start()
    {
        panelBackground = panelBackgroundObject.GetComponent<Image>();

        amineFlowSlider.minValue = 10f;
        amineFlowSlider.maxValue = 100f;
        amineFlowSlider.value = 55f;

        regenTempSlider.minValue = 80f;
        regenTempSlider.maxValue = 130f;
        regenTempSlider.value = 105f;

        amineFlowSlider.onValueChanged.AddListener(delegate { RecalculateTargets(); });
        regenTempSlider.onValueChanged.AddListener(delegate { RecalculateTargets(); });

        RecalculateTargets();

        currentEta = targetEta;
        currentAmineFactor = targetAmineFactor;
        currentTempFactor = targetTempFactor;
    }

    void RecalculateTargets()
    {
        float F = amineFlowSlider.value;
        float T = regenTempSlider.value;

        targetAmineFactor = Mathf.Pow(F / F_MAX, 0.6f);
        targetTempFactor = 1f - Mathf.Exp(-0.05f * (T - T_MIN));
        targetEta = Mathf.Clamp(targetAmineFactor * targetTempFactor * 100f, 0f, 100f);
    }

    void Update()
    {
        currentEta = Mathf.Lerp(currentEta, targetEta, Time.deltaTime * smoothSpeed);
        currentAmineFactor = Mathf.Lerp(currentAmineFactor, targetAmineFactor, Time.deltaTime * smoothSpeed);
        currentTempFactor = Mathf.Lerp(currentTempFactor, targetTempFactor, Time.deltaTime * smoothSpeed);

        efficiencyOutput.text = currentEta.ToString("F1") + "%";
        amineFactorOutput.text = currentAmineFactor.ToString("F3");
        tempFactorOutput.text = currentTempFactor.ToString("F3");

        float targetWidth = (currentEta / 100f) * BAR_WIDTH;
        RectTransform barRect = efficiencyBarImage.rectTransform;
        barRect.sizeDelta = new Vector2(
            Mathf.Lerp(barRect.sizeDelta.x, targetWidth, Time.deltaTime * smoothSpeed),
            barRect.sizeDelta.y
        );

        Color targetBarColor;
        if (currentEta < 60f)
            targetBarColor = lowColor;
        else if (currentEta < 85f)
            targetBarColor = midColor;
        else
            targetBarColor = highColor;

        efficiencyBarImage.color = Color.Lerp(efficiencyBarImage.color, targetBarColor, Time.deltaTime * smoothSpeed);

        float targetAlpha = currentEta < 60f ? 1f : 0f;
        absorberWarningBanner.alpha = Mathf.Lerp(absorberWarningBanner.alpha, targetAlpha, Time.deltaTime * smoothSpeed);

        Color targetBgColor = currentEta < 60f ? warningColor : normalColor;
        panelBackground.color = Color.Lerp(panelBackground.color, targetBgColor, Time.deltaTime * smoothSpeed);
    }
}