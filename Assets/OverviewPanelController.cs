using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OverviewPanelController : MonoBehaviour
{
    [Header("Timeline Slider")]
    public Slider timelineSlider;

    [Header("Bar Images")]
    public Image h2Bar;
    public Image co2Bar;
    public Image meohBar;
    public Image effBar;

    [Header("Value Labels (TMP)")]
    public TMP_Text h2Label;
    public TMP_Text co2Label;
    public TMP_Text meohLabel;
    public TMP_Text effLabel;

    [Header("Steady-state design values")]
    public float h2Max   = 4.2f;
    public float co2Max  = 33.6f;
    public float meohMax = 16.0f;
    public float effMax  = 54f;

    [Header("Track full widths (pixels)")]
    public float h2TrackWidth   = 900f;
    public float co2TrackWidth  = 900f;
    public float meohTrackWidth = 900f;
    public float effTrackWidth  = 900f;

    [Header("Smooth Speed")]
    public float smoothSpeed = 5f;

    private float targetH2;
    private float targetCO2;
    private float targetMeOH;
    private float targetEff;

    void Start()
    {
        timelineSlider.minValue    = 0f;
        timelineSlider.maxValue    = 100f;
        timelineSlider.value       = 0f;
        timelineSlider.wholeNumbers = true;

        timelineSlider.onValueChanged.AddListener(OnTimelineChanged);

        targetH2   = 0f;
        targetCO2  = 0f;
        targetMeOH = 0f;
        targetEff  = 0f;
    }

    void OnTimelineChanged(float t)
    {
        float r = Ramp(t);
        targetH2   = h2Max   * r;
        targetCO2  = co2Max  * r;
        targetMeOH = meohMax * r;
        targetEff  = effMax  * r;
    }

    void Update()
    {
        SetBar(h2Bar,   h2TrackWidth,   ref targetH2,   co2Max,  h2Label,   "0.0", " kg/h");
        SetBar(co2Bar,  co2TrackWidth,  ref targetCO2,  co2Max,  co2Label,  "0.0", " kg/h");
        SetBar(meohBar, meohTrackWidth, ref targetMeOH, co2Max,  meohLabel, "0.0", " kg/h");
        SetBar(effBar,  effTrackWidth,  ref targetEff,  100f,    effLabel,  "0",   "%");
    }

    void SetBar(Image bar, float trackWidth, ref float target, float maxVal, TMP_Text label, string format, string unit)
    {
        if (bar == null) return;

        RectTransform rt = bar.rectTransform;
        float currentWidth = rt.sizeDelta.x;
        float targetWidth  = (target / maxVal) * trackWidth;

        float newWidth = Mathf.Lerp(currentWidth, targetWidth, Time.deltaTime * smoothSpeed);
        rt.sizeDelta = new Vector2(newWidth, rt.sizeDelta.y);

        if (label != null)
        {
            float displayVal = (newWidth / trackWidth) * maxVal;
            label.text = displayVal.ToString(format) + unit;
        }
    }

    float Ramp(float t)
    {
        if (t < 15f) return (t / 15f) * 0.75f;
        if (t < 40f) return 0.75f + ((t - 15f) / 25f) * 0.25f;
        if (t < 75f) return 1.0f;
        return 1.0f - ((t - 75f) / 25f) * 0.05f;
    }
}