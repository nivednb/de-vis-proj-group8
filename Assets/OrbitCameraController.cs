using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCameraController : MonoBehaviour
{
    [Header("Camera Orbit")]
    [SerializeField] private float orbitSpeed = 2f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 250f;
    [SerializeField] private float minVertical = -80f;
    [SerializeField] private float maxVertical = 80f;

    [Header("Pan")]
    [SerializeField] private float panSensitivity = 0.002f;
    [SerializeField] private float panSmoothSpeed = 8f;

    [Header("Startup")]
    [SerializeField] private Vector3 homePosition = Vector3.zero;
    [SerializeField] private float startHorizontal = 225f;
    [SerializeField] private float startVertical = 25f;
    [SerializeField] private float startDistance = 90f;
    [SerializeField] private Transform[] focusPoints = new Transform[12];

    private Camera mainCamera;
    private float horizontal;
    private float vertical;
    private float distance;
    private Vector3 focusTarget;
    private Vector3 panTarget;
    private Vector2 lastMouse;
    private bool isDragging;

    private void OnEnable()
    {
        mainCamera = Camera.main;
        horizontal = startHorizontal;
        vertical = startVertical;
        distance = startDistance;
        focusTarget = homePosition;
        panTarget = homePosition;
    }

    private void Update()
    {
        HandleInput();
        UpdateCamera();
    }

    private void HandleInput()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // LEFT CLICK DRAG - Rotate or Pan
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isDragging = true;
            lastMouse = mousePos;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector2 delta = mousePos - lastMouse;

            if (Keyboard.current.shiftKey.isPressed)
            {
                // Pan mode
                Vector3 right = transform.right;
                Vector3 up = transform.up;
                panTarget -= right * delta.x * panSensitivity;
                panTarget -= up * delta.y * panSensitivity;
            }
            else
            {
                // Rotate mode
                horizontal -= delta.x * orbitSpeed * 0.1f;
                vertical -= delta.y * orbitSpeed * 0.1f;
                vertical = Mathf.Clamp(vertical, minVertical, maxVertical);
            }

            lastMouse = mousePos;
        }

        // SCROLL - Zoom
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -= scroll * zoomSpeed * 0.2f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    private void UpdateCamera()
    {
        // Smooth pan
        focusTarget = Vector3.Lerp(focusTarget, panTarget, Time.deltaTime * panSmoothSpeed);

        // Calculate position
        float hRad = horizontal * Mathf.Deg2Rad;
        float vRad = vertical * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            distance * Mathf.Cos(hRad) * Mathf.Cos(vRad),
            distance * Mathf.Sin(vRad),
            distance * Mathf.Sin(hRad) * Mathf.Cos(vRad)
        );

        transform.position = focusTarget + offset;
        transform.LookAt(focusTarget + Vector3.up * 0.5f);
    }

    public void FocusModule(int index)
    {
        if (index >= 0 && index < focusPoints.Length && focusPoints[index] != null)
        {
            panTarget = focusPoints[index].position;
        }
    }

    public void FocusHome()
    {
        panTarget = homePosition;
        horizontal = startHorizontal;
        vertical = startVertical;
        distance = startDistance;
    }
}