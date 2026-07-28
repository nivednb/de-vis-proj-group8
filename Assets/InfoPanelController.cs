using UnityEngine;
using UnityEngine.UI;

public class InfoPanelController : MonoBehaviour
{
    private Button closeButton;
    private CanvasGroup canvasGroup;

    private void Start()
    {
        // Find close button - expected naming: {Module}InfoClose
        Transform closeButtonTransform = transform.Find(gameObject.name.Replace("Panel", "Close"));
        if (closeButtonTransform == null)
        {
            // Fallback: search children for any button
            closeButtonTransform = transform.Find(transform.GetChild(2).name);
        }

        if (closeButtonTransform != null)
        {
            closeButton = closeButtonTransform.GetComponent<Button>();
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HidePanel);
            }
        }

        // Add CanvasGroup for smooth fade/alpha control (optional but nice)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void ShowPanel()
    {
        gameObject.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    public void HidePanel()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        gameObject.SetActive(false);
        
        // Also hide the shared info button
        SharedInfoButton sharedButton = FindObjectOfType<SharedInfoButton>();
        if (sharedButton != null)
        {
            sharedButton.Hide();
        }
    }
}