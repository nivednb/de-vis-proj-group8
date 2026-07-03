using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float panSpeed = 10f;
    public float zoomSpeed = 5f;
    public float rotateSpeed = 100f;

    void Update()
    {
        // WASD to pan
        float x = Input.GetAxis("Horizontal") * panSpeed * Time.deltaTime;
        float z = Input.GetAxis("Vertical") * panSpeed * Time.deltaTime;
        transform.Translate(x, 0, z, Space.World);

        // Scroll to zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        transform.Translate(0, 0, scroll * zoomSpeed, Space.Self);

        // Right click + drag to rotate
        if (Input.GetMouseButton(1))
        {
            float rotX = Input.GetAxis("Mouse X") * rotateSpeed * Time.deltaTime;
            float rotY = Input.GetAxis("Mouse Y") * rotateSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, rotX, Space.World);
            transform.Rotate(Vector3.right, -rotY, Space.Self);
        }
    }
}