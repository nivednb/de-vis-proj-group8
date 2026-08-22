using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Orbits the camera around a fixed pivot point on the surface of an imaginary sphere.
/// Arrow keys change azimuth/elevation, A/D pan laterally, and W/S zoom.
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
    [Tooltip("Close-up orthographic size used when a focus point has no explicit value.")]
    public float defaultFocusOrthoSize = 12f;
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
    [Tooltip("World-space lateral pan speed for A/D. Orthographic views scale this with zoom so movement stays readable.")]
    public float panSpeed = 24f;

    [Header("Mouse Controls")]
    [Tooltip("Mouse sensitivity for left-click drag rotation (degrees per pixel).")]
    public float mouseLookSensitivity = 0.15f;
    [Tooltip("Scroll wheel zoom multiplier (units per scroll tick).")]
    public float scrollZoomMultiplier = 10f;
    [Tooltip("Zoom range for scroll wheel.")]
    public float scrollMinDistance = 0.00001f;
    public float scrollMaxDistance = 250f;
    [Tooltip("Mouse sensitivity for Shift + drag pan (units per pixel).")]
    public float panMouseSensitivity = 2000.0f;
    [Tooltip("Smooth acceleration for pan dragging.")]
    public float panSmoothSpeed = 8f;

    [Header("Elevation clamp (degrees, avoids flipping over the poles)")]
    [Tooltip("Keep a degree or two short of 90 (e.g. 89) to avoid gimbal-flip at the exact pole.")]
    public float minElevation = -89f;
    public float maxElevation = 89f;

    [Header("Projection")]
    [Tooltip("True isometric/3D-scanner look: orthographic removes perspective foreshortening so the orbit reads as pure rotation around the object rather than depth-based movement.")]
    public bool useOrthographic = true;
    public float orthographicSize = 40f;
    [Tooltip("Automatically centers and frames the complete process model for the home view.")]
    public bool autoFrameWholePlant = true;
    [Range(1f, 2f)] public float wholePlantFramePadding = 1.2f;

    [Header("Starting angles (degrees)")]
    public float startAzimuth = 0f;
    public float startElevation = 20f;

    private float _azimuth;
    private float _elevation;
    private Camera _cam;

    // Mouse state
    private Vector2 _lastMousePos;
    private bool _leftMouseDragThisFrame;
    private Vector3 _panVelocity;

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

    public int CurrentFocusIndex => _focusIndex;

    // "Home" view — captured once at Start from the Inspector's start fields, and used
    // to snap exactly back to the original starting view whenever focus returns to -1
    // (Full Plant Overview), rather than just keeping whatever angle you'd orbited to.
    private float _homeAzimuth;
    private float _homeElevation;
    private float _homeDistance;
    private float _homeOrthoSize;

    /// <summary>Fired on a plain left-click that lands on the 3D scene rather than any UI.</summary>
    public event System.Action BackgroundClicked;

    void Start()
    {
        if (autoFrameWholePlant)
        {
            FrameWholePlantFromRenderers();
        }

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

    private void FrameWholePlantFromRenderers()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || ShouldIgnoreForWholePlantFrame(renderer)) continue;
            Bounds candidate = renderer.bounds;
            bool oversizedFloor = candidate.size.x > 120f && candidate.size.z > 80f && candidate.size.y < 2f;
            if (oversizedFloor || renderer.gameObject.name == "Plane") continue;
            if (!found) { bounds = candidate; found = true; }
            else bounds.Encapsulate(candidate);
        }

        if (!found) return;
        worldOrigin = bounds.center;
        float aspect = _cam != null ? _cam.aspect : (Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f);
        float halfHeightForWidth = bounds.extents.x / Mathf.Max(0.5f, aspect);
        orthographicSize = Mathf.Max(bounds.extents.y, halfHeightForWidth, bounds.extents.z) * wholePlantFramePadding;
        orthographicSize = Mathf.Max(orthographicSize, 20f);
        Debug.Log($"OrbitCameraController: framed plant at {worldOrigin}, ortho {orthographicSize:F1}, bounds {bounds.size}.");
    }

    private static bool ShouldIgnoreForWholePlantFrame(Renderer renderer)
    {
        Transform current = renderer.transform;
        while (current != null)
        {
            string value = current.name;
            if (value.Contains("Generated_Plant_Environment") || value.Contains("Generated Whole Plant Flow") ||
                value.Contains("Generated Interactive Module") || value.Contains("Generated Reactor Detail") ||
                value.Contains("FlowParticle") || value.Contains("ThinGuide_") || value.Contains("Canvas") ||
                value.Contains("Label") || value.Contains("TMP")) return true;
            current = current.parent;
        }
        return false;
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

        // A/D translate the view left/right without changing its viewing angle.
        // Moving both endpoints keeps subsequent focus interpolation internally consistent.
        float panInput = 0f;
        if (kb.dKey.isPressed) panInput += 1f;
        if (kb.aKey.isPressed) panInput -= 1f;
        if (!Mathf.Approximately(panInput, 0f))
        {
            Vector3 planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            if (planarRight.sqrMagnitude < 0.001f) planarRight = Vector3.right;

            float zoomScale = useOrthographic && _cam != null
                ? Mathf.Max(0.35f, _cam.orthographicSize / Mathf.Max(1f, _homeOrthoSize))
                : Mathf.Max(0.35f, distance / Mathf.Max(1f, _homeDistance));
            Vector3 panDelta = planarRight * (panInput * panSpeed * zoomScale * Time.deltaTime);
            _currentTarget += panDelta;
            _targetFrom += panDelta;
            _targetTo += panDelta;
        }

        // Zoom: W = zoom in, S = zoom out (also keeping +/- as a backup)
        float zoomDelta = 0f;
        if (kb.wKey.isPressed || kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed) zoomDelta -= zoomSpeed * Time.deltaTime;
        if (kb.sKey.isPressed || kb.minusKey.isPressed || kb.numpadMinusKey.isPressed) zoomDelta += zoomSpeed * Time.deltaTime;

        if (useOrthographic && _cam != null)
        {
            float minOrtho = _focusIndex >= 0 ? 0.1f : 0.1f;
            float maxOrtho = _focusIndex >= 0 ? Mathf.Max(18f, defaultFocusOrthoSize * 1.75f) : 52f;
            orthographicSize = Mathf.Clamp(orthographicSize + zoomDelta * 0.5f, minOrtho, maxOrtho);
            _cam.orthographicSize = orthographicSize;
        }
        else
        {
            distance = Mathf.Clamp(distance + zoomDelta, minDistance, maxDistance);
        }

        HandleMouseInput();
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

    Vector3 ScreenToWorldPoint(Vector2 screenPos, Vector3 targetPos)
    {
        if (_cam == null || !useOrthographic) return targetPos;

        Vector3 screenToWorld = _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        return new Vector3(screenToWorld.x, screenToWorld.y, targetPos.z);
    }

    void HandleMouseInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        var kb = Keyboard.current;
        bool shiftHeld = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);

        // Don't accept mouse input if cursor is over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Belt-and-suspenders exact check against the analytics window's live rect (it's
        // draggable) in case the EventSystem's UI hover check above doesn't cover it for
        // any reason — only the pixels the window actually currently covers block camera
        // input; everywhere else stays interactive even while it's open.
        if (IcodosDashboardRuntime.Instance != null && IcodosDashboardRuntime.Instance.IsPointerOverAnalyticsWindow(mouse.position.ReadValue()))
            return;

        // A plain click that lands on the 3D scene itself (not any UI) — lets listeners
        // (e.g. the dashboard) treat it as "focus moved to the main simulation view" and
        // dismiss whatever context panel/page is currently open, the way clicking outside
        // a popover closes it in most apps.
        if (mouse.leftButton.wasPressedThisFrame) BackgroundClicked?.Invoke();

        Vector2 mousePos = mouse.position.ReadValue();
        bool leftMouseDown = mouse.leftButton.isPressed;

        // Left-click drag for rotation or pan
        if (leftMouseDown)
        {
            if (!_leftMouseDragThisFrame)
            {
                // Mouse button just pressed this frame
                _lastMousePos = mousePos;
                _leftMouseDragThisFrame = true;
            }
            else
            {
                // Dragging continues
                Vector2 mouseDelta = mousePos - _lastMousePos;

                if (shiftHeld)
                {
                    // Shift + drag: pan camera with high responsiveness
                    Vector3 panDelta = new Vector3(-mouseDelta.x, -mouseDelta.y, 0f) * panMouseSensitivity;
                    _currentTarget += panDelta;
                    _targetFrom += panDelta;
                    _targetTo += panDelta;
                }
                else
                {
                    // Regular drag: orbit
                    _azimuth += mouseDelta.x * mouseLookSensitivity;
                    _elevation -= mouseDelta.y * mouseLookSensitivity;
                    _elevation = Mathf.Clamp(_elevation, minElevation, maxElevation);
                }

                _lastMousePos = mousePos;
            }
        }
        else
        {
            _leftMouseDragThisFrame = false;
        }

        // Apply smoothed pan velocity
        if (_panVelocity.sqrMagnitude > 0.001f)
        {
            _currentTarget += _panVelocity * Time.deltaTime;
            _targetFrom += _panVelocity * Time.deltaTime;
            _targetTo += _panVelocity * Time.deltaTime;

            _panVelocity = Vector3.Lerp(_panVelocity, Vector3.zero, panSmoothSpeed * Time.deltaTime);
        }

        // Scroll wheel zoom with zoom-to-mouse
        float scrollValue = mouse.scroll.ReadValue().y;
        if (!Mathf.Approximately(scrollValue, 0f))
        {
            float zoomDelta = scrollValue * scrollZoomMultiplier;

            if (useOrthographic && _cam != null)
            {
                // Calculate world position under mouse cursor BEFORE zoom
                Vector3 mouseWorldPosBefore = ScreenToWorldPoint(mousePos, _currentTarget);

                float minOrtho = _focusIndex >= 0 ? 0.1f : 0.1f;
                float maxOrtho = _focusIndex >= 0 ? Mathf.Max(18f, defaultFocusOrthoSize * 1.75f) : 52f;
                orthographicSize = Mathf.Clamp(orthographicSize - zoomDelta, minOrtho, maxOrtho);
                _cam.orthographicSize = orthographicSize;

                // Calculate world position under mouse cursor AFTER zoom
                Vector3 mouseWorldPosAfter = ScreenToWorldPoint(mousePos, _currentTarget);

                // Adjust camera target so the point under the cursor doesn't move
                Vector3 adjustment = mouseWorldPosBefore - mouseWorldPosAfter;
                _currentTarget += adjustment;
                _targetFrom += adjustment;
                _targetTo += adjustment;
            }
            else
            {
                distance = Mathf.Clamp(distance - zoomDelta, scrollMinDistance, scrollMaxDistance);
            }
        }
    }

    /// <summary>Hook this up to your "Next" button's OnClick() in the Inspector.</summary>
    public void FocusNext() => CycleFocus(+1);

    /// <summary>Hook this up to your "Previous" button's OnClick() in the Inspector.</summary>
    public void FocusPrevious() => CycleFocus(-1);

    /// <summary>Returns to the saved whole-plant view.</summary>
    public void FocusOverview()
    {
        if (_focusIndex == -1)
        {
            BeginFocusTransition(-1);
            return;
        }

        BeginFocusTransition(-1);
    }

    /// <summary>Focuses a configured module by its zero-based focus-point index.</summary>
    public void FocusModule(int index)
    {
        if (focusPoints == null || index < 0 || index >= focusPoints.Length)
        {
            Debug.LogWarning($"Cannot focus module {index}: focus point is not configured.");
            return;
        }

        BeginFocusTransition(index);
    }

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

        BeginFocusTransition(_focusIndex);
    }

    private void BeginFocusTransition(int focusIndex)
    {
        _focusIndex = focusIndex;

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
                ? focusOrthoSizes[_focusIndex] : defaultFocusOrthoSize;
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
