using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PtMeOHReactorController : MonoBehaviour
{
    [Header("4-Variable Sliders")]
    public Slider tempSlider;      // Range: 200 to 300
    public Slider pressureSlider;  // Range: 50 to 100
    public Slider ratioSlider;     // Range: 2.5 to 5.0
    public Slider velocitySlider;  // Range: 5000 to 10000

    [Header("UI Outputs")]
    public TextMeshProUGUI yieldText;
    public TextMeshProUGUI statusText;
    public GameObject sinteringWarningPanel; // UI Overlay panel for high-temp risk

    [Header("Nicole's Live Material Balance UI")]
    [Tooltip("Total fixed input feed rate for simulation prototype in kg/h")]
    public float baseInputFeed = 100.0f; 
    public TextMeshProUGUI inputMassText;
    public TextMeshProUGUI outputMassText;
    
    [Header("Visual Balance Fill Bars (Assign UI Images with Filled Type)")]
    public Image inputFeedBar;      // Visual bar showing raw inputs entering
    public Image methanolProductBar; // Visual bar expanding with higher yield
    public Image waterByproductBar;  // Visual bar expanding with water creation
    public Image recycleGasBar;     // Visual bar shrinking as yield increases

    [Header("Simulation Settings")]
    [Tooltip("Scaling factor to normalize peak yield around 15-25%")]
    public float K = 21.5f; 

    [Header("3D Visual Assets")]
    public Renderer reactorMeshRenderer; // Reference to the 3D Blender model mesh

    void Update()
    {
        CalculateMethanolYield();
    }

    void CalculateMethanolYield()
    {
        // 1. Fetch live interface slider data
        float T = tempSlider.value;
        float P = pressureSlider.value;
        float R = ratioSlider.value;
        float V = velocitySlider.value;

        // 2. Compute components of the team's engineering formula
        float pressureFactor = P / 100f;
        float ratioFactor = R / 3f;
        float velocityFactor = 8000f / V;
        
        // Gaussian distribution exponent centered perfectly at 250°C
        float tempExponent = -Mathf.Pow((T - 250f) / 35f, 2f);
        float tempFactor = Mathf.Exp(tempExponent);

        // Calculate Final Single-Pass Yield (Y)
        float yield = K * pressureFactor * ratioFactor * tempFactor * velocityFactor;
        yield = Mathf.Clamp(yield, 0f, 100f);
        float yieldFraction = yield / 100f;

        // 3. Nicole's Comparative Input vs. Output Logic Prototype
        float totalInputMass = baseInputFeed;
        
        // Output splitting equations based on process flow sheets
        float methanolOutputMass = totalInputMass * yieldFraction; 
        float waterOutputMass = methanolOutputMass * 0.56f; // Stoichiometric mass ratio of H2O to MeOH (CO2 + 3H2 -> CH3OH + H2O)
        float unreactedRecycleMass = totalInputMass * (1f - yieldFraction);

        // Update Balance UI text fields dynamically
        if (inputMassText != null) inputMassText.text = $"Total Plant Input: {totalInputMass:F1} kg/h";
        if (outputMassText != null) outputMassText.text = $"Total Liquid Output: {methanolOutputMass:F1} kg/h MeOH";

        // Update visual Fill Amount metrics (Ranges from 0.0 empty to 1.0 full)
        if (inputFeedBar != null) inputFeedBar.fillAmount = 1.0f; // Inputs stay constant
        if (methanolProductBar != null) methanolProductBar.fillAmount = methanolOutputMass / totalInputMass;
        if (waterByproductBar != null) waterByproductBar.fillAmount = waterOutputMass / totalInputMass;
        if (recycleGasBar != null) recycleGasBar.fillAmount = unreactedRecycleMass / totalInputMass;

        // 4. Evaluate safety operational thresholds
        string currentStatus = "Operational Equilibrium Secure";
        bool isSintering = false;
        Color feedbackColor = Color.cyan;

        if (T > 270f) // Team critical cap
        {
            currentStatus = "CRITICAL ALARM: Catalyst Sintering Risk! Active Surface Area Collapsing.";
            isSintering = true;
            feedbackColor = new Color(1f, 0.15f, 0f); // Hotspot Orange-Red
        }
        else if (P < 60f)
        {
            currentStatus = "PERFORMANCE WARNING: Low Conversion Efficiency due to low pressure.";
            feedbackColor = Color.yellow;
        }
        else if (R < 2.5f)
        {
            currentStatus = "INPUT WARNING: Hydrogen deficiency; poor hydrocarbon conversion.";
            feedbackColor = Color.gray;
        }
        else if (V > 9500f)
        {
            currentStatus = "KINETIC WARNING: High space velocity; gas bypassing reaction matrix.";
            feedbackColor = Color.blue;
        }

        // 5. Send metrics out to UI Components
        yieldText.text = $"Est. Methanol Yield: {yield:F2}%";
        statusText.text = $"Status: {currentStatus}";
        
        if (sinteringWarningPanel != null)
        {
            sinteringWarningPanel.SetActive(isSintering);
        }

        // 6. Dynamic 3D shader color adjustments for visual feedback
        if (reactorMeshRenderer != null)
        {
            if (isSintering)
            {
                reactorMeshRenderer.material.EnableKeyword("_EMISSION");
                reactorMeshRenderer.material.SetColor("_EmissionColor", feedbackColor * 2.5f);
            }
            else
            {
                reactorMeshRenderer.material.DisableKeyword("_EMISSION");
            }
        }
    }
}