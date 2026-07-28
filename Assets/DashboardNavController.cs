using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class DashboardNavController : MonoBehaviour
{
    public OrbitCameraController orbitCameraController;
    public TextMeshProUGUI mainTitleText;
    public GameObject[] navItems = new GameObject[12];

    string[] navLabels = {
        "Electrolyzer", "CO2 Tank", "Absorber Column", "H2 Tank",
        "Desorber Column", "Compressor", "Heat Exchanger", "Reactor Bed",
        "Condenser", "Flash Separator", "Distillation Column", "Methanol Storage"
    };

    int[] cameraFocusIndex = {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
    };

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
            CheckNavClick();
    }

    void CheckNavClick()
    {
        for (int i = 0; i < navItems.Length; i++)
        {
            if (navItems[i] == null) continue;
            var rt = navItems[i].GetComponent<RectTransform>();
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, Mouse.current.position.ReadValue()))
            {
                OnNavClick(i);
                return;
            }
        }
    }

    void OnNavClick(int navIndex)
    {
        if (mainTitleText != null && navIndex < navLabels.Length)
            mainTitleText.text = "3D plant view – " + navLabels[navIndex];

        if (orbitCameraController != null && navIndex < cameraFocusIndex.Length)
        {
            int focusIdx = cameraFocusIndex[navIndex];
            if (focusIdx >= 0)
            {
                orbitCameraController.FocusModule(focusIdx);
                Debug.Log("Clicked: " + navLabels[navIndex]);
            }
        }
        
        if (orbitCameraController != null)
        {
            Debug.Log("Calling FocusModule(" + cameraFocusIndex[navIndex] + ")");
            orbitCameraController.FocusModule(cameraFocusIndex[navIndex]);
        }
        else
        {
            Debug.LogError("orbitCameraController is NULL!");
        }

    }
}