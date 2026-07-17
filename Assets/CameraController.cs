using UnityEngine;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    [System.Serializable]
    public class ModuleView
    {
        public string moduleName;
        public Vector3 cameraPosition;
        public Vector3 cameraRotation;
    }

    [Header("Module Views")]
    public ModuleView[] modules = new ModuleView[]
    {
        new ModuleView { moduleName = "Absorber",            cameraPosition = new Vector3(-29.7f,  0f,  12f), cameraRotation = new Vector3(15f, 180f, 0f) },
        new ModuleView { moduleName = "Condenser",           cameraPosition = new Vector3(-17.55f, 0f,  -4f), cameraRotation = new Vector3(15f, 180f, 0f) },
        new ModuleView { moduleName = "Reactor",             cameraPosition = new Vector3(-3f,     4f,  12f), cameraRotation = new Vector3(15f, 180f, 0f) },
        new ModuleView { moduleName = "Distillation Column", cameraPosition = new Vector3(8.63f,   4f,  -4f), cameraRotation = new Vector3(15f, 180f, 0f) },
        new ModuleView { moduleName = "Compressor",          cameraPosition = new Vector3(22.35f,  0f,  -4f), cameraRotation = new Vector3(15f, 180f, 0f) },
    };

    [Header("UI")]
    public Button nextButton;
    public Button prevButton;
    public Component moduleNameLabel;

    [Header("Smooth Speed")]
    public float smoothSpeed = 5f;

    private int currentIndex = 0;
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    void Start()
    {
        if (nextButton) nextButton.onClick.AddListener(NextModule);
        if (prevButton) prevButton.onClick.AddListener(PrevModule);

        targetPosition = transform.position;
        targetRotation = transform.rotation;

        GoToModule(0, instant: true);
    }

    void Update() { }

    public void GoToModule(int index, bool instant = false)
    {
        currentIndex = index;
        ModuleView m = modules[index];

        targetPosition = m.cameraPosition;
        targetRotation = Quaternion.Euler(m.cameraRotation);

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        SetModuleNameLabel(m.moduleName);
    }

    public void NextModule()
    {
        currentIndex = (currentIndex + 1) % modules.Length;
        GoToModule(currentIndex);
    }

    public void PrevModule()
    {
        currentIndex = (currentIndex - 1 + modules.Length) % modules.Length;
        GoToModule(currentIndex);
    }

    
    void SetModuleNameLabel(string value)
    {
        if (moduleNameLabel == null) return;

        System.Reflection.PropertyInfo textProperty = moduleNameLabel.GetType().GetProperty("text");
        if (textProperty != null && textProperty.CanWrite)
        {
            textProperty.SetValue(moduleNameLabel, value, null);
        }
    }
}

