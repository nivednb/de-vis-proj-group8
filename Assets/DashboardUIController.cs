using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
// DashboardUIController.cs
// Attach to the same GameObject as DashboardUI, OR call from any other script.
// Handles: nav selection, tab switching, quick action buttons, live data push.
// ─────────────────────────────────────────────────────────────────────────────

public class DashboardUIController : MonoBehaviour
{
    [Header("Hook up your camera controller here")]
    public MonoBehaviour orbitCameraController;

    // Maps nav index → module index in OrbitCameraController.focusTargets[]
    // Adjust to match your focusTargets array order
    static readonly int[] CamIndex = {
        -1, -1,   // Dashboard, Analytics
         0,  1, 2, // Electrolyzer, CO2 Absorber, Desorber
         3,  4, 5, // Compressor, Reactor, Heat Exchanger
         6,  7, 8  // Flash Separator, Distillation, Storage Tanks
    };

    static readonly string[] NavLabels = {
        "Dashboard", "Analytics",
        "Electrolyzer", "CO\u2082 Absorber", "Desorber",
        "Compressor", "Reactor", "Heat Exchanger",
        "Flash Separator", "Distillation", "Storage Tanks"
    };

    readonly Color NAVY    = new Color(0.039f, 0.208f, 0.569f);
    readonly Color SURFACE = new Color(0.953f, 0.961f, 0.976f);
    readonly Color BORDER  = new Color(0.863f, 0.882f, 0.922f);
    readonly Color TSEC    = new Color(0.353f, 0.392f, 0.502f);
    readonly Color ABLUE   = new Color(0.255f, 0.565f, 0.973f);
    readonly Color TPRI    = new Color(0.102f, 0.137f, 0.251f);
    readonly Color NTXT    = new Color(0.180f, 0.341f, 0.698f);
    readonly Color GREEN   = new Color(0.059f, 0.612f, 0.290f);

    static Color WA(Color c, float a) => new Color(c.r, c.g, c.b, a);

    bool _simRunning = false;

    void Start()
    {
        WireQuickActions();
    }

    // ── Called by DashboardUI nav buttons directly via onClick ──────────────
    // (DashboardUI already wires these; this just provides the public methods
    //  in case you want to call them from other scripts too.)

    public void SelectNav(int idx)
    {
        // Update main title
        var titleObj = GameObject.Find("MainTitle");
        if (titleObj != null && idx < NavLabels.Length)
        {
            var tmp = titleObj.GetComponent<TextMeshProUGUI>();
            if (tmp) tmp.text = "3D plant view \u2014 " + NavLabels[idx];
        }

        // Focus camera
        if (orbitCameraController != null && idx < CamIndex.Length && CamIndex[idx] >= 0)
        {
            var method = orbitCameraController.GetType().GetMethod("FocusModule");
            if (method != null)
                method.Invoke(orbitCameraController, new object[] { CamIndex[idx] });
        }
    }

    // ── Quick action buttons ─────────────────────────────────────────────────
    void WireQuickActions()
    {
        Wire("QA_Start",  OnStart);
        Wire("QA_Pause",  OnPause);
        Wire("QA_Reset",  OnReset);
        Wire("QA_Export", OnExport);
    }

    void Wire(string goName, UnityEngine.Events.UnityAction action)
    {
        var go = GameObject.Find(goName);
        if (go == null) return;
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    void OnStart()
    {
        _simRunning = true;
        Debug.Log("[Dashboard] Simulation started.");
        UpdateChipText("Simulation running");
        // TODO: hook into your sim manager
    }

    void OnPause()
    {
        _simRunning = false;
        Debug.Log("[Dashboard] Simulation paused.");
        UpdateChipText("Simulation paused");
    }

    void OnReset()
    {
        _simRunning = false;
        Debug.Log("[Dashboard] Simulation reset.");
        UpdateChipText("Simulation stopped");
    }

    void OnExport()
    {
        Debug.Log("[Dashboard] Export triggered.");
        // TODO: hook into your screenshot/export logic
    }

    void UpdateChipText(string text)
    {
        var chip = GameObject.Find("Chip");
        if (chip == null) return;
        var tmp = chip.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp) tmp.text = text;
    }

    // ── Live data push (call these from ReactorController, AbsorberController etc.) ──

    // Call from ReactorController every frame (or on value change)
    public void UpdateLiveConditions(float tempC, float pressureBar,
                                     float ratio, float ghsv)
    {
        SetMetric("MC_Temperature",      $"{tempC:F0} \u00b0C");
        SetMetric("MC_Pressure",         $"{pressureBar:F0} bar");
        SetMetric("MC_H\u2082/CO\u2082 ratio", $"{ratio:F1} mol/mol");
        SetMetric("MC_GHSV",             $"{ghsv:F0} h\u207b\u00b9");
    }

    // Call from AbsorberController / OverviewPanelController
    public void UpdateOperatingTargets(float yieldPct, float co2Pct,
                                       float elecPct, float meohPct)
    {
        SetProgressBar("OFill_Yield",    yieldPct);
        SetProgressBar("OFill_CO2",      co2Pct);
        SetProgressBar("OFill_Elec",     elecPct);
        SetProgressBar("OFill_MeOH",     meohPct);
        SetPctText("OPct_Yield", yieldPct);
        SetPctText("OPct_CO2",   co2Pct);
        SetPctText("OPct_Elec",  elecPct);
        SetPctText("OPct_MeOH",  meohPct);
    }

    // Call from OverviewPanelController.UpdateMassFlows()
    public void UpdateStreamFlows(float h2, float co2, float meoh,
                                  float recycle, float steam, float cw)
    {
        SetStreamVal("SV_H2",      $"{h2:F1} kg/h");
        SetStreamVal("SV_CO2",     $"{co2:F1} kg/h");
        SetStreamVal("SV_MeOH",    $"{meoh:F1} kg/h");
        SetStreamVal("SV_Recycle", $"{recycle:F1} kg/h");
        SetStreamVal("SV_Steam",   $"{steam:F1} kg/h");
        SetStreamVal("SV_CW",      $"{cw:F1} kg/h");
    }

    // ── Alert system ──────────────────────────────────────────────────────────
    public void SetAlert(string message, bool isWarning = false)
    {
        var banner = GameObject.Find("AlertBanner");
        if (banner == null) return;
        var texts = banner.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0) texts[0].text = message;
        var img = banner.GetComponent<Image>();
        if (img) img.color = isWarning
            ? new Color(1f, 0.95f, 0.88f)   // amber bg
            : new Color(0.882f, 0.957f, 0.914f); // green bg
    }

    public void ClearAlert()
    {
        SetAlert("All systems normal", false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void SetMetric(string cardName, string value)
    {
        var card = GameObject.Find(cardName);
        if (card == null) return;
        var tmps = card.GetComponentsInChildren<TextMeshProUGUI>();
        // Second TMP child is the value (first is the label)
        if (tmps.Length >= 2) tmps[1].text = value;
    }

    void SetProgressBar(string barName, float pct)
    {
        var bar = GameObject.Find(barName);
        if (bar == null) return;
        var rt = bar.GetComponent<RectTransform>();
        if (rt == null) return;
        float maxW = 70f;
        rt.sizeDelta = new Vector2(Mathf.Round(maxW * Mathf.Clamp01(pct / 100f)), rt.sizeDelta.y);
    }

    void SetPctText(string objName, float pct)
    {
        var go = GameObject.Find(objName);
        if (go == null) return;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp) tmp.text = $"{pct:F0}%";
    }

    void SetStreamVal(string objName, string value)
    {
        var go = GameObject.Find(objName);
        if (go == null) return;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp) tmp.text = value;
    }
}