using UnityEngine;

public class ReactorPanelToggle : MonoBehaviour
{
    public GameObject reactorPanel;
    public GameObject gearButton;

    void Start()
    {
        reactorPanel.SetActive(false);
        gearButton.SetActive(true);
    }

    public void OpenPanel()
    {
        reactorPanel.SetActive(true);
        gearButton.SetActive(false);
    }

    public void ClosePanel()
    {
        reactorPanel.SetActive(false);
        gearButton.SetActive(true);
    }
}