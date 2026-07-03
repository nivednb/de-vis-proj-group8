using UnityEngine;
using UnityEngine.UI;

public class EyePanelToggle : MonoBehaviour
{
    [Header("Panel to Show/Hide")]
    public GameObject targetPanel;

    [Header("Eye Button Image")]
    public Image eyeButtonImage;

    [Header("Icons")]
    public Sprite eyeOpenIcon;
    public Sprite eyeClosedIcon;

    [Header("Start State")]
    public bool panelOpenAtStart = false;

    private bool isOpen;

    void Start()
    {
        isOpen = panelOpenAtStart;
        ApplyState();
    }

    public void TogglePanel()
    {
        isOpen = !isOpen;
        ApplyState();
    }

    void ApplyState()
    {
        if (targetPanel != null)
            targetPanel.SetActive(isOpen);

        if (eyeButtonImage != null)
            eyeButtonImage.sprite = isOpen ? eyeOpenIcon : eyeClosedIcon;
    }
}