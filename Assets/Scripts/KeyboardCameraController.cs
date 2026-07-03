using UnityEngine;

public class KeyboardCameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float fastSpeedMultiplier = 3f;

    [Header("Rotation")]
    public float lookSpeed = 80f;

    [Header("Zoom")]
    public float zoomSpeed = 8f;
    public float minFov = 15f;
    public float maxFov = 70f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        MoveCamera();
        RotateCamera();
        ZoomCamera();
    }

    void MoveCamera()
    {
        float speed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
            speed *= fastSpeedMultiplier;

        float horizontal = Input.GetAxis("Horizontal"); // A/D or Left/Right
        float vertical = Input.GetAxis("Vertical");     // W/S or Up/Down

        Vector3 move =
            transform.right * horizontal +
            transform.forward * vertical;

        if (Input.GetKey(KeyCode.E))
            move += transform.up;

        if (Input.GetKey(KeyCode.Q))
            move -= transform.up;

        transform.position += move * speed * Time.deltaTime;
    }

    void RotateCamera()
    {
        if (Input.GetMouseButton(1)) // hold right mouse button
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed * Time.deltaTime;

            transform.eulerAngles += new Vector3(-mouseY, mouseX, 0f);
        }
    }

    void ZoomCamera()
    {
        if (cam == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f)
        {
            cam.fieldOfView -= scroll * zoomSpeed;
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minFov, maxFov);
        }
    }
}