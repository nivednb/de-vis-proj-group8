using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class AnalyticsDashboardUI : MonoBehaviour
{
    [Header("UI NAVIGATION PANELS")]
    public GameObject analyticsDashboardPanel;

    [Header("WIDGET 1: YIELD TREND LINE")]
    public UILineRenderer yieldLineRenderer;
    private List<float> yieldHistory = new List<float>();
    private float sampleTimer = 0f;

    [Header("WIDGET 2: REACTOR FACTOR BARS (Filled Images)")]
    public Image tempFactorBar;
    public Image pressureFactorBar;
    public Image ratioFactorBar;
    public Image ghsvFactorBar;
    public TMP_Text tempFactorText;
    public TMP_Text pressureFactorText;
    public TMP_Text ratioFactorText;
    public TMP_Text ghsvFactorText;

    [Header("WIDGET 3: SAFETY GAUGE (Radial Fill)")]
    public Image temperatureRadialGauge;
    public TMP_Text temperatureGaugeText;

    void Update()
    {
        // Subscribe to central process Authority
        if (PlantProcessSimulator.Instance == null) return;
        var snapshot = PlantProcessSimulator.Instance.LatestSnapshot;

        UpdateReactionFactorBars(snapshot);
        UpdateSafetyRadialGauge(snapshot);
        RecordYieldTrendData(snapshot);
    }

    private void UpdateReactionFactorBars(var snapshot)
    {
        // Compute intermediate reaction factors
        float fP = Mathf.Clamp01(snapshot.ReactorPressure / 100f);
        float fT = Mathf.Exp(-Mathf.Pow((snapshot.ReactorTemperature - 245f), 2f) / (2f * Mathf.Pow(25f, 2f)));
        float fR = Mathf.Clamp01(1.0f - 0.05f * Mathf.Abs(snapshot.H2CO2Ratio - 3.0f));
        float fG = Mathf.Clamp01(1.0f - 0.1f * ((snapshot.GHSV - 1000f) / 19000f));

        // Update visual bar fill amounts
        if (pressureFactorBar) pressureFactorBar.fillAmount = fP;
        if (tempFactorBar) tempFactorBar.fillAmount = fT;
        if (ratioFactorBar) ratioFactorBar.fillAmount = fR;
        if (ghsvFactorBar) ghsvFactorBar.fillAmount = fG;

        // Update readout text
        if (pressureFactorText) pressureFactorText.text = $"F_P: {fP:F2}";
        if (tempFactorText) tempFactorText.text = $"F_T: {fT:F2}";
        if (ratioFactorText) ratioFactorText.text = $"F_R: {fR:F2}";
        if (ghsvFactorText) ghsvFactorText.text = $"F_G: {fG:F2}";
    }

    private void UpdateSafetyRadialGauge(var snapshot)
    {
        float currentTemp = snapshot.ReactorTemperature;
        
        if (temperatureRadialGauge)
        {
            temperatureRadialGauge.fillAmount = currentTemp / 300f;
            // Shift color based on safety limits
            if (currentTemp > 260f)
                temperatureRadialGauge.color = Color.red; // Overheating
            else if (currentTemp > 250f)
                temperatureRadialGauge.color = Color.yellow; // Warning
            else
                temperatureRadialGauge.color = Color.green; // Safe
        }

        if (temperatureGaugeText)
            temperatureGaugeText.text = $"{currentTemp:F0} °C";
    }

    private void RecordYieldTrendData(var snapshot)
    {
        sampleTimer += Time.deltaTime;
        if (sampleTimer >= 0.5f) // Sample every half second
        {
            sampleTimer = 0f;
            float currentYield = snapshot.MethanolYieldPercent;
            
            yieldHistory.Add(currentYield);
            if (yieldHistory.Count > 40) yieldHistory.RemoveAt(0); // Maintain last 40 points

            if (yieldLineRenderer != null)
            {
                yieldLineRenderer.UpdatePoints(yieldHistory);
            }
        }
    }

    // Call this method from the Analytics Tab Button OnClick() event
    public void ToggleAnalyticsPanel(bool isOpen)
    {
        if (analyticsDashboardPanel != null)
        {
            analyticsDashboardPanel.SetActive(isOpen);
        }
    }
}