using UnityEngine;

public class ModuleHoverDetector : MonoBehaviour
{
    private SharedInfoButton sharedInfoButton;
    private InfoPanelController infoPanelController;

    private void Start()
    {
        // Find SharedInfoButton in MaterialCanvas
        sharedInfoButton = FindObjectOfType<SharedInfoButton>();
        if (sharedInfoButton == null)
        {
            Debug.LogError($"SharedInfoButton not found for module: {gameObject.name}");
            return;
        }

        // Find the corresponding info panel for this module
        string panelName = DeterminePanelName(gameObject.name);
        Transform panelTransform = sharedInfoButton.transform.parent.parent.Find(panelName);
        
        if (panelTransform != null)
        {
            infoPanelController = panelTransform.GetComponent<InfoPanelController>();
            if (infoPanelController == null)
            {
                infoPanelController = panelTransform.gameObject.AddComponent<InfoPanelController>();
            }
        }
        else
        {
            Debug.LogWarning($"Info panel '{panelName}' not found for module: {gameObject.name}");
        }
    }

    private void OnMouseEnter()
    {
        if (sharedInfoButton != null && infoPanelController != null)
        {
            sharedInfoButton.ShowAtModule(gameObject, infoPanelController);
        }
    }

    private void OnMouseExit()
    {
        if (sharedInfoButton != null)
        {
            sharedInfoButton.Hide();
        }
    }

    private string DeterminePanelName(string moduleName)
    {
        // Map module names to panel names
        return moduleName switch
        {
            "compressor_block" => "CompressorInfoPanel",
            "distillation column" => "DistillatonInfoPanel",
            "condenser" => "CondenserInfoPanel",
            _ => moduleName + "InfoPanel"
        };
    }
}