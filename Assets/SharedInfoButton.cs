using UnityEngine;
using UnityEngine.UI;

public class SharedInfoButton : MonoBehaviour
{
    private RectTransform buttonRect;
    private Button buttonComponent;
    private Canvas canvas;
    private GameObject currentHoveredModule;
    private InfoPanelController currentPanelController;

    private void Start()
    {
        buttonRect = GetComponent<RectTransform>();
        buttonComponent = GetComponent<Button>();
        canvas = GetComponentInParent<Canvas>();
        
        // Wire up the button click event
        if (buttonComponent != null)
        {
            buttonComponent.onClick.AddListener(OnButtonClicked);
        }
        
        gameObject.SetActive(false);
    }

    public void ShowAtModule(GameObject module, InfoPanelController panelController)
    {
        currentHoveredModule = module;
        currentPanelController = panelController;
        
        // Position button above/near the module
        Vector3 moduleScreenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, module.transform.position);
        buttonRect.position = new Vector3(moduleScreenPos.x, moduleScreenPos.y + 60f, 0);
        
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        currentHoveredModule = null;
        currentPanelController = null;
    }

    public void OnButtonClicked()
    {
        if (currentPanelController != null)
        {
            currentPanelController.ShowPanel();
        }
    }
}