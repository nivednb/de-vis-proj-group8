using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Orbits the camera around a fixed pivot point on the surface of an imaginary sphere.
/// Arrow keys (or WASD) change azimuth/elevation; the camera always looks at the pivot.
/// Attach directly to the Main Camera. Set 'pivot' to an empty GameObject placed at the
/// visual center of the plant (e.g. near the Reactor, roughly X=5 in your current layout)
/// so the whole setup stays framed while you rotate around it.
/// </summary>
public class OrbitCameraController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Empty GameObject at the center of the plant. If left empty, uses worldOrigin below instead.")]
    public Transform pivot;
    [Tooltip("Used only if 'pivot' is not assigned.")]
    public Vector3 worldOrigin = new Vector3(0f, 0f, -20f);

    [Header("Module Focus Points (Shift + Left/Right to cycle)")]
    [Tooltip("Index 0 should usually be the overall plant pivot (whole-setup view). Add one Transform per module you want to jump to: Electrolyzer, Absorber, Desorber, Compressor, Reactor, Condenser, Flash Separator, Distillation.")]
    public Transform[] focusPoints;
    [Tooltip("Optional: orbit distance to use for each entry in focusPoints (same length/order). Leave empty to keep the current distance when switching focus.")]
    public float[] focusDistances;
    [Tooltip("Optional: orthographic size (visual zoom) to use for each entry, same length/order as focusPoints. Only matters when useOrthographic is true — 'distance' alone does NOT visually zoom in orthographic mode, this field is what actually controls zoom level per module.")]
    public float[] focusOrthoSizes;
    [Tooltip("Optional: vertical offset (in Unity units) added to each focusPoints entry, since most module meshes have their pivot at the base/floor rather than their visual center. Same length/order as focusPoints. E.g. 8 for a tall column, 2 for a short tank.")]
    public float[] focusHeightOffsets;
    [Tooltip("Optional: name shown on-screen for each module (same length/order as focusPoints). Hook up moduleNameText below to display it.")]
    public string[] focusNames;
    [Tooltip("Assign a TextMeshProUGUI element here to show the current module's name on screen. Leave empty to skip.")]
    public TextMeshProUGUI moduleNameText;
    [Tooltip("Text shown when focus index is -1 (whole-plant view).")]
    public string wholePlantLabel = "Full Plant Overview";
    [Tooltip("How long the camera takes to glide to a newly selected focus point, in seconds.")]
    public float focusTransitionTime = 0.8f;

    [Header("Sphere radius (distance from pivot)")]
    public float distance = 80f;
    public float minDistance = 20f;
    public float maxDistance = 200f;

    [Header("Rotation speed")]
    public float azimuthSpeed = 60f;   // degrees per second, left/right arrows
    public float elevationSpeed = 45f; // degrees per second, up/down arrows
    public float zoomSpeed = 40f;      // units per second, for +/- or scroll

    [Header("Elevation clamp (degrees, avoids flipping over the poles)")]
    [Tooltip("Keep a degree or two short of 90 (e.g. 89) to avoid gimbal-flip at the exact pole.")]
    public float minElevation = -89f;
    public float maxElevation = 89f;

    [Header("Projection")]
    [Tooltip("True isometric/3D-scanner look: orthographic removes perspective foreshortening so the orbit reads as pure rotation around the object rather than depth-based movement.")]
    public bool useOrthographic = true;
    public float orthographicSize = 40f;

    [Header("Starting angles (degrees)")]
    public float startAzimuth = 0f;
    public float startElevation = 20f;

    private float _azimuth;
    private float _elevation;
    private Camera _cam;

    // Smoothed focus target — lets the camera glide between modules instead of snapping
    private Vector3 _currentTarget;
    private Vector3 _targetFrom;
    private Vector3 _targetTo;
    private float _targetBlend = 1f; // 1 = fully arrived at _targetTo
    private float _distanceFrom;
    private float _distanceTo;
    private float _azFrom;
    private float _azTo;
    private float _elFrom;
    private float _elTo;
    private float _orthoFrom;
    private float _orthoTo;
    private int _focusIndex = -1; // -1 = using 'pivot'/worldOrigin (whole-plant view)

    // "Home" view — captured once at Start from the Inspector's start fields, and used
    // to snap exactly back to the original starting view whenever focus returns to -1
    // (Full Plant Overview), rather than just keeping whatever angle you'd orbited to.
    private float _homeAzimuth;
    private float _homeElevation;
    private float _homeDistance;
    private float _homeOrthoSize;

    void Start()
    {
        _azimuth = startAzimuth;
        _elevation = startElevation;
        _cam = GetComponent<Camera>();
        if (_cam != null)
        {
            _cam.orthographic = useOrthographic;
            if (useOrthographic) _cam.orthographicSize = orthographicSize;
        }

        // Capture the Inspector's starting values as "home" — CycleFocus uses these
        // whenever focus returns to -1 (Full Plant Overview) so it's an exact return,
        // not just "wherever you happened to be orbiting."
        _homeAzimuth = startAzimuth;
        _homeElevation = startElevation;
        _homeDistance = distance;
        _homeOrthoSize = orthographicSize;

        Vector3 initialTarget = pivot != null ? pivot.position : worldOrigin;
        _currentTarget = initialTarget;
        _targetFrom = initialTarget;
        _targetTo = initialTarget;
        _distanceFrom = distance;
        _distanceTo = distance;
        _azFrom = _azimuth; _azTo = _azimuth;
        _elFrom = _elevation; _elTo = _elevation;
        _orthoFrom = orthographicSize; _orthoTo = orthographicSize;
        _targetBlend = 1f;

        UpdateModuleNameLabel();
        UpdateCameraPosition();
    }

    /// <summary>
    /// Right-click the component header in the Inspector and choose this to capture
    /// whatever view you've manually framed (by hand-dragging the camera in the Scene
    /// view, or wherever Play has it parked) as the new starting/home view. Works in
    /// both Edit mode and Play mode — it just reads the camera's current transform and
    /// orthographic size relative to the pivot, and writes the equivalent
    /// azimuth/elevation/distance back into the Inspector fields above.
    /// </summary>
    [ContextMenu("Capture Current View As Home/Start")]
    public void CaptureCurrentViewAsHome()
    {
        Vector3 target = pivot != null ? pivot.position : worldOrigin;
        Vector3 rel = transform.position - target;
        float d = rel.magnitude;
        if (d < 0.001f)
        {
            Debug.LogWarning("Camera is sitting exactly on the pivot — move it first, then capture.");
            return;
        }

        float el = Mathf.Asin(Mathf.Clamp(rel.y / d, -1f, 1f)) * Mathf.Rad2Deg;
        float az = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;

        startAzimuth = az;
        startElevation = el;
        distance = d;

        var cam = _cam != null ? _cam : GetComponent<Camera>();
        if (cam != null) orthographicSize = cam.orthographicSize;

        Debug.Log($"Captured home view — Azimuth: {az:F2}, Elevation: {el:F2}, Distance: {d:F2}, OrthoSize: {orthographicSize:F2}. These are now saved in the Inspector's Starting Angles / Sphere Radius / Projection fields.");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return; // no keyboard device detected this frame

        bool shiftHeld = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

        // Shift + Left/Right: cycle focus between modules. Uses wasPressedThisFrame so
        // holding Shift+Right doesn't rapid-fire through every module in one second.
        if (shiftHeld && focusPoints != null && focusPoints.Length > 0)
        {
            if (kb.rightArrowKey.wasPressedThisFrame) CycleFocus(+1);
            if (kb.leftArrowKey.wasPressedThisFrame) CycleFocus(-1);
        }

        // Advance the smooth glide toward whichever focus point was last selected
        if (_targetBlend < 1f)
        {
            _targetBlend = Mathf.Min(1f, _targetBlend + Time.deltaTime / Mathf.Max(0.01f, focusTransitionTime));
            float t = Mathf.SmoothStep(0f, 1f, _targetBlend);
            _currentTarget = Vector3.Lerp(_targetFrom, _targetTo, t);
            distance = Mathf.Lerp(_distanceFrom, _distanceTo, t);
            _azimuth = Mathf.LerpAngle(_azFrom, _azTo, t);
            _elevation = Mathf.Lerp(_elFrom, _elTo, t);
            orthographicSize = Mathf.Lerp(_orthoFrom, _orthoTo, t);
            if (useOrthographic && _cam != null) _cam.orthographicSize = orthographicSize;

            UpdateCameraPosition();
            return; // skip manual input entirely while gliding — avoids fighting the lerp
        }

        _currentTarget = _targetTo;

        // Only orbit with plain arrow keys (no Shift) — Shift+arrow is reserved for focus switching above
        float horizontal = 0f;
        float vertical = 0f;
        if (!shiftHeld)
        {
            if (kb.rightArrowKey.isPressed) horizontal += 1f;
            if (kb.leftArrowKey.isPressed) horizontal -= 1f;
            if (kb.upArrowKey.isPressed) vertical += 1f;
            if (kb.downArrowKey.isPressed) vertical -= 1f;
        }

        _azimuth += horizontal * azimuthSpeed * Time.deltaTime;
        _elevation -= vertical * elevationSpeed * Time.deltaTime; // Up arrow = look from higher up
        _elevation = Mathf.Clamp(_elevation, minElevation, maxElevation);

        // Zoom: W = zoom in, S = zoom out (also keeping +/- as a backup)
        float zoomDelta = 0f;
        if (kb.wKey.isPressed || kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed) zoomDelta -= zoomSpeed * Time.deltaTime;
        if (kb.sKey.isPressed || kb.minusKey.isPressed || kb.numpadMinusKey.isPressed) zoomDelta += zoomSpeed * Time.deltaTime;

        if (useOrthographic && _cam != null)
        {
            orthographicSize = Mathf.Clamp(orthographicSize + zoomDelta * 0.5f, 5f, 150f);
            _cam.orthographicSize = orthographicSize;
        }
        else
        {
            distance = Mathf.Clamp(distance + zoomDelta, minDistance, maxDistance);
        }

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        float azRad = _azimuth * Mathf.Deg2Rad;
        float elRad = _elevation * Mathf.Deg2Rad;

        // Standard spherical -> Cartesian conversion around the current (possibly blending) target
        float x = distance * Mathf.Cos(elRad) * Mathf.Sin(azRad);
        float y = distance * Mathf.Sin(elRad);
        float z = distance * Mathf.Cos(elRad) * Mathf.Cos(azRad);

        transform.position = _currentTarget + new Vector3(x, y, z);
        transform.LookAt(_currentTarget);
    }

    /// <summary>Hook this up to your "Next" button's OnClick() in the Inspector.</summary>
    public void FocusNext() => CycleFocus(+1);

    /// <summary>Hook this up to your "Previous" button's OnClick() in the Inspector.</summary>
    public void FocusPrevious() => CycleFocus(-1);

    /// <summary>
    /// Cycles forward (+1) or backward (-1) through focusPoints. Index -1 is reserved for
    /// the original whole-plant pivot/worldOrigin so Shift+Left from module 0 returns you
    /// to the full-setup view rather than wrapping straight to the last module.
    /// </summary>
    void CycleFocus(int direction)
    {
        int count = focusPoints.Length;
        // Sequence is: -1 (whole plant) -> 0 -> 1 -> ... -> count-1 -> wraps back to -1
        _focusIndex += direction;
        if (_focusIndex >= count) _focusIndex = -1;
        if (_focusIndex < -1) _focusIndex = count - 1;

        Vector3 newTarget;
        float newDistance;
        float newAz;
        float newEl;
        float newOrtho;

        if (_focusIndex == -1)
        {
            // Exact return to the captured home/starting view
            newTarget = pivot != null ? pivot.position : worldOrigin;
            newDistance = _homeDistance;
            newAz = _homeAzimuth;
            newEl = _homeElevation;
            newOrtho = _homeOrthoSize;
        }
        else
        {
            Transform t = focusPoints[_focusIndex];
            if (t == null) return; // skip empty slots safely
            newTarget = t.position;
            if (focusHeightOffsets != null && _focusIndex < focusHeightOffsets.Length)
            {
                newTarget += Vector3.up * focusHeightOffsets[_focusIndex];
            }
            // Keep current orbit angle when jumping between modules — only position/zoom change
            newAz = _azimuth;
            newEl = _elevation;
            newDistance = (focusDistances != null && _focusIndex < focusDistances.Length && focusDistances[_focusIndex] > 0f)
                ? focusDistances[_focusIndex] : distance;
            newOrtho = (focusOrthoSizes != null && _focusIndex < focusOrthoSizes.Length && focusOrthoSizes[_focusIndex] > 0f)
                ? focusOrthoSizes[_focusIndex] : orthographicSize;
        }

        UpdateModuleNameLabel();

        _targetFrom = _currentTarget;
        _targetTo = newTarget;
        _distanceFrom = distance;
        _distanceTo = newDistance;
        _azFrom = _azimuth;
        _azTo = newAz;
        _elFrom = _elevation;
        _elTo = newEl;
        _orthoFrom = orthographicSize;
        _orthoTo = newOrtho;
        _targetBlend = 0f;
    }

    void UpdateModuleNameLabel()
    {
        if (moduleNameText == null) return;

        if (_focusIndex == -1)
        {
            moduleNameText.text = wholePlantLabel;
        }
        else if (focusNames != null && _focusIndex < focusNames.Length && !string.IsNullOrEmpty(focusNames[_focusIndex]))
        {
            moduleNameText.text = focusNames[_focusIndex];
        }
        else if (focusPoints[_focusIndex] != null)
        {
            moduleNameText.text = focusPoints[_focusIndex].name; // fallback: use the GameObject's own name
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 target = Application.isPlaying ? _currentTarget : (pivot != null ? pivot.position : worldOrigin);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target, distance);
        Gizmos.DrawLine(transform.position, target);

        if (focusPoints != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < focusPoints.Length; i++)
            {
                if (focusPoints[i] == null) continue;
                Vector3 fp = focusPoints[i].position;
                if (focusHeightOffsets != null && i < focusHeightOffsets.Length)
                {
                    fp += Vector3.up * focusHeightOffsets[i];
                }
                Gizmos.DrawWireSphere(fp, 2f);
            }
        }
    }
#endif
}