using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Hosts the analytics UI in its own native Windows window — a real top-level window with
/// its own taskbar button that can be moved, resized, minimised, maximised and closed like
/// any other application window, while still running inside (and live-bound to) the main
/// application process.
///
/// How it works:
///   * The analytics UI lives on a Screen Space - Camera canvas drawn by a dedicated camera
///     into a RenderTexture sized to the window's client area.
///   * Each frame that texture is read back asynchronously and blitted into the native
///     window with GDI.
///   * Mouse messages from the native window are queued by its window procedure and replayed
///     into the canvas through <see cref="ExternalPointerDispatcher"/>, so buttons, hover
///     tooltips, drags and the scroll wheel behave exactly as they did in the embedded popup.
///
/// Only the Windows standalone player uses this; in the Editor the analytics popup stays
/// embedded in the main window (<see cref="IsSupported"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class ExternalAnalyticsWindow : MonoBehaviour
{
    /// <summary>Layer the off-screen analytics canvas and its camera live on. The plant
    /// cameras have it removed from their culling masks.</summary>
    public const int CanvasLayer = 31;

    private const string WindowClassName = "PtMDigitalTwinAnalyticsWindow";
    private const string WindowTitle = "Analytics & Insights - Power-to-Methanol Digital Twin";
    private static readonly Vector2 ReferenceSize = new Vector2(1180f, 780f);
    private static readonly Color BackgroundColor = new Color32(9, 29, 41, 255);

    /// <summary>True in the Windows standalone player, where native windows can be created.</summary>
    public static bool IsSupported
    {
        get
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>Raised when the user closes the native window (title-bar X, Alt+F4, taskbar).</summary>
    public event Action Closed;

    public Canvas Canvas { get; private set; }
    public RectTransform Root { get; private set; }
    public bool IsOpen => hwnd != IntPtr.Zero;

    private Camera renderCamera;
    private RenderTexture target;
    private ExternalPointerDispatcher pointer;
    private IntPtr hwnd;
    private bool readbackPending;
    private byte[] frame;
    private int frameWidth, frameHeight;
    private bool swizzleToBgra;
    private int clientWidth, clientHeight;
    private bool minimized;
    private bool closeRequested;
    private bool trackingLeave;
    private bool pointerInside;

    private static ExternalAnalyticsWindow current;
    private static WndProcDelegate wndProcDelegate;
    private static bool classRegistered;
    private readonly Queue<PointerMessage> messages = new Queue<PointerMessage>();

    private struct PointerMessage
    {
        public uint Msg;
        public int X, Y;
        public float Wheel;
    }

    // ---- construction --------------------------------------------------------

    /// <summary>Creates the host plus its off-screen camera and canvas. The native window
    /// itself is only created by <see cref="Open"/>.</summary>
    public static ExternalAnalyticsWindow Create(Transform parent)
    {
        GameObject go = new GameObject("External Analytics Window Host");
        go.transform.SetParent(parent, false);
        ExternalAnalyticsWindow host = go.AddComponent<ExternalAnalyticsWindow>();
        host.Build();
        return host;
    }

    private void Build()
    {
        // Far below the plant, so nothing else can ever be in front of this camera.
        GameObject cameraObject = new GameObject("Analytics Window Camera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.position = new Vector3(0f, -20000f, 0f);
        cameraObject.layer = CanvasLayer;
        renderCamera = cameraObject.AddComponent<Camera>();
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = BackgroundColor;
        renderCamera.cullingMask = 1 << CanvasLayer;
        renderCamera.orthographic = true;
        renderCamera.nearClipPlane = 0.1f;
        renderCamera.farClipPlane = 10f;
        renderCamera.depth = -100f;
        renderCamera.allowHDR = false;
        renderCamera.allowMSAA = false;
        UniversalAdditionalCameraData urp = renderCamera.GetUniversalAdditionalCameraData();
        if (urp != null)
        {
            urp.renderPostProcessing = false;
            urp.antialiasing = AntialiasingMode.None;
            urp.renderShadows = false;
            urp.requiresDepthTexture = false;
            urp.requiresColorTexture = false;
        }
        renderCamera.enabled = false;

        GameObject canvasObject = new GameObject("Analytics Window Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.layer = CanvasLayer;
        Canvas = canvasObject.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceCamera;
        Canvas.worldCamera = renderCamera;
        Canvas.planeDistance = 1f;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceSize;
        scaler.matchWidthOrHeight = 0.5f;
        ExternalWindowRaycaster raycaster = canvasObject.AddComponent<ExternalWindowRaycaster>();

        GameObject rootObject = new GameObject("Analytics Window Root", typeof(RectTransform));
        rootObject.layer = CanvasLayer;
        rootObject.transform.SetParent(canvasObject.transform, false);
        Root = rootObject.GetComponent<RectTransform>();
        Root.anchorMin = Vector2.zero;
        Root.anchorMax = Vector2.one;
        Root.offsetMin = Vector2.zero;
        Root.offsetMax = Vector2.zero;

        pointer = new ExternalPointerDispatcher(raycaster);
        ResizeTarget(Mathf.RoundToInt(ReferenceSize.x), Mathf.RoundToInt(ReferenceSize.y));
        canvasObject.SetActive(false);
    }

    /// <summary>Keeps every plant camera from drawing the analytics layer.</summary>
    private void ExcludeLayerFromOtherCameras()
    {
        foreach (Camera cam in Camera.allCameras)
            if (cam != renderCamera) cam.cullingMask &= ~(1 << CanvasLayer);
    }

    /// <summary>Puts every object under the analytics canvas on the analytics layer.</summary>
    public void AssignLayerRecursively()
    {
        if (Canvas != null) SetLayer(Canvas.transform);
    }

    private static void SetLayer(Transform t)
    {
        t.gameObject.layer = CanvasLayer;
        for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i));
    }

    // ---- open / close --------------------------------------------------------

    public void Open()
    {
        if (!IsSupported || IsOpen) return;
        ExcludeLayerFromOtherCameras();
        AssignLayerRecursively();
        Canvas.gameObject.SetActive(true);
        renderCamera.enabled = true;
        closeRequested = false;
        CreateNativeWindow();
    }

    /// <summary>Closes the native window programmatically. Does not raise <see cref="Closed"/>.</summary>
    public void Close()
    {
        DestroyNativeWindow();
        pointer.Reset();
        if (renderCamera != null) renderCamera.enabled = false;
        if (Canvas != null) Canvas.gameObject.SetActive(false);
    }

    private void OnApplicationQuit() => DestroyNativeWindow();

    private void OnDestroy()
    {
        DestroyNativeWindow();
        if (target != null)
        {
            target.Release();
            Destroy(target);
        }
        if (current == this) current = null;
    }

    // ---- per frame -------------------------------------------------------------

    private void Update()
    {
        if (!IsOpen) return;

        // Unity already dispatches messages for every window on its thread; draining our own
        // queue here as well guarantees the window never looks "not responding".
        PumpMessages();

        if (closeRequested)
        {
            Close();
            Closed?.Invoke();
            return;
        }

        if (!minimized && clientWidth > 0 && clientHeight > 0 &&
            (clientWidth != target.width || clientHeight != target.height) && !readbackPending)
            ResizeTarget(clientWidth, clientHeight);

        ProcessPointerMessages();
    }

    private void LateUpdate()
    {
        if (!IsOpen || minimized || readbackPending || target == null) return;
        readbackPending = true;
        int width = target.width, height = target.height;
        AsyncGPUReadback.Request(target, 0, request => OnReadback(request, width, height));
    }

    private void OnReadback(AsyncGPUReadbackRequest request, int width, int height)
    {
        readbackPending = false;
        if (request.hasError || !IsOpen) return;
        int bytes = width * height * 4;
        var data = request.GetData<byte>();
        if (data.Length < bytes) return;
        if (frame == null || frame.Length != bytes) frame = new byte[bytes];
        Unity.Collections.NativeArray<byte>.Copy(data, 0, frame, 0, bytes);
        if (swizzleToBgra)
            for (int i = 0; i < bytes; i += 4)
            {
                byte r = frame[i];
                frame[i] = frame[i + 2];
                frame[i + 2] = r;
            }
        frameWidth = width;
        frameHeight = height;
        PresentFrame(IntPtr.Zero);
    }

    private void ResizeTarget(int width, int height)
    {
        width = Mathf.Clamp(width, 64, 8192);
        height = Mathf.Clamp(height, 64, 8192);
        if (target != null)
        {
            if (renderCamera != null) renderCamera.targetTexture = null;
            target.Release();
            Destroy(target);
        }

        // BGRA sRGB lets GDI take the bytes as-is; fall back to RGBA plus a CPU swizzle.
        GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB;
        swizzleToBgra = false;
        if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render))
        {
            format = GraphicsFormat.R8G8B8A8_SRGB;
            swizzleToBgra = true;
        }
        var descriptor = new RenderTextureDescriptor(width, height, format, 24) { msaaSamples = 1 };
        target = new RenderTexture(descriptor) { name = "Analytics Window Target" };
        target.Create();
        renderCamera.targetTexture = target;
    }

    // ---- pointer input ---------------------------------------------------------

    private void ProcessPointerMessages()
    {
        while (messages.Count > 0)
        {
            PointerMessage m = messages.Dequeue();
            if (m.Msg == WM_MOUSELEAVE)
            {
                pointerInside = false;
                pointer.Leave();
                continue;
            }

            Vector2 position = ClientToTarget(m.X, m.Y);
            pointerInside = true;
            switch (m.Msg)
            {
                case WM_MOUSEMOVE: pointer.Move(position); break;
                case WM_LBUTTONDOWN: pointer.Press(position, PointerEventData.InputButton.Left); break;
                case WM_LBUTTONUP: pointer.Release(position, PointerEventData.InputButton.Left); break;
                case WM_RBUTTONDOWN: pointer.Press(position, PointerEventData.InputButton.Right); break;
                case WM_RBUTTONUP: pointer.Release(position, PointerEventData.InputButton.Right); break;
                case WM_MOUSEWHEEL: pointer.Scroll(position, m.Wheel); break;
            }
        }
        // Keep hover state fresh while content animates under a still pointer.
        if (pointerInside) pointer.Refresh();
    }

    /// <summary>Native client coordinates (top-left origin) to render-target pixels
    /// (bottom-left origin, as Unity's event system expects).</summary>
    private Vector2 ClientToTarget(int x, int y)
    {
        float sx = clientWidth > 0 ? target.width / (float)clientWidth : 1f;
        float sy = clientHeight > 0 ? target.height / (float)clientHeight : 1f;
        return new Vector2((x + 0.5f) * sx, (clientHeight - y - 0.5f) * sy);
    }

    // ---- native window -------------------------------------------------------

    private void CreateNativeWindow()
    {
        current = this;
        IntPtr instance = GetModuleHandle(null);
        if (!classRegistered)
        {
            wndProcDelegate = WindowProc;
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
                hInstance = instance,
                hCursor = LoadCursor(IntPtr.Zero, IDC_ARROW),
                lpszClassName = WindowClassName
            };
            IntPtr mainWindow = FindMainWindow();
            if (mainWindow != IntPtr.Zero)
            {
                // Reuse the application's own icon for the title bar and taskbar button.
                wc.hIcon = SendMessage(mainWindow, WM_GETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
                wc.hIconSm = SendMessage(mainWindow, WM_GETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
                if (wc.hIcon == IntPtr.Zero) wc.hIcon = GetClassLongPtr(mainWindow, GCLP_HICON);
                if (wc.hIconSm == IntPtr.Zero) wc.hIconSm = GetClassLongPtr(mainWindow, GCLP_HICONSM);
            }
            if (RegisterClassEx(ref wc) == 0)
            {
                Debug.LogError($"[Analytics window] RegisterClassEx failed ({Marshal.GetLastWin32Error()}).");
                return;
            }
            classRegistered = true;
        }

        // Size the client area to the analytics reference layout at the monitor's DPI, and
        // centre it on the main window's monitor without exceeding the work area.
        IntPtr main = FindMainWindow();
        uint dpi = main != IntPtr.Zero ? GetDpiForWindow(main) : 96u;
        if (dpi == 0) dpi = 96;
        float scale = dpi / 96f;
        RECT work = WorkAreaFor(main);
        int workW = work.Right - work.Left, workH = work.Bottom - work.Top;
        int clientW = Mathf.Min(Mathf.RoundToInt(ReferenceSize.x * scale), Mathf.RoundToInt(workW * 0.92f));
        int clientH = Mathf.Min(Mathf.RoundToInt(ReferenceSize.y * scale), Mathf.RoundToInt(workH * 0.88f));
        var rect = new RECT { Left = 0, Top = 0, Right = clientW, Bottom = clientH };
        AdjustWindowRectEx(ref rect, WS_OVERLAPPEDWINDOW, false, WS_EX_APPWINDOW);
        int outerW = rect.Right - rect.Left, outerH = rect.Bottom - rect.Top;
        int x = work.Left + Mathf.Max(0, (workW - outerW) / 2);
        int y = work.Top + Mathf.Max(0, (workH - outerH) / 2);

        hwnd = CreateWindowEx(WS_EX_APPWINDOW, WindowClassName, WindowTitle, WS_OVERLAPPEDWINDOW,
            x, y, outerW, outerH, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
        {
            Debug.LogError($"[Analytics window] CreateWindowEx failed ({Marshal.GetLastWin32Error()}).");
            return;
        }
        GetClientRect(hwnd, out RECT client);
        clientWidth = client.Right - client.Left;
        clientHeight = client.Bottom - client.Top;
        minimized = false;
        ResizeTarget(clientWidth, clientHeight);
        ShowWindow(hwnd, SW_SHOWNORMAL);
        UpdateWindow(hwnd);
        SetForegroundWindow(hwnd);
    }

    private void DestroyNativeWindow()
    {
        if (hwnd == IntPtr.Zero) return;
        IntPtr handle = hwnd;
        hwnd = IntPtr.Zero;
        trackingLeave = false;
        pointerInside = false;
        messages.Clear();
        DestroyWindow(handle);
    }

    private void PumpMessages()
    {
        while (hwnd != IntPtr.Zero && PeekMessage(out MSG msg, hwnd, 0, 0, PM_REMOVE))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    /// <summary>Draws the latest frame, stretched to the client area. Called after every
    /// readback, and from WM_PAINT (with that message's DC) when Windows asks for a repaint.</summary>
    private void PresentFrame(IntPtr paintDc)
    {
        if (hwnd == IntPtr.Zero || frame == null || frameWidth <= 0 || frameHeight <= 0) return;
        IntPtr dc = paintDc != IntPtr.Zero ? paintDc : GetDC(hwnd);
        if (dc == IntPtr.Zero) return;
        try
        {
            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth = frameWidth,
                // Positive height = bottom-up rows, which is how Unity lays out the readback.
                biHeight = frameHeight,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB
            };
            GetClientRect(hwnd, out RECT client);
            int w = client.Right - client.Left, h = client.Bottom - client.Top;
            SetStretchBltMode(dc, HALFTONE);
            StretchDIBits(dc, 0, 0, w, h, 0, 0, frameWidth, frameHeight, frame, ref header, DIB_RGB_COLORS, SRCCOPY);
        }
        finally
        {
            if (paintDc == IntPtr.Zero) ReleaseDC(hwnd, dc);
        }
    }

    [AOT.MonoPInvokeCallback(typeof(WndProcDelegate))]
    private static IntPtr WindowProc(IntPtr window, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            ExternalAnalyticsWindow self = current;
            if (self != null && window == self.hwnd)
            {
                IntPtr? handled = self.HandleMessage(window, msg, wParam, lParam);
                if (handled.HasValue) return handled.Value;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        return DefWindowProc(window, msg, wParam, lParam);
    }

    private IntPtr? HandleMessage(IntPtr window, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_CLOSE:
                closeRequested = true;
                return IntPtr.Zero;
            case WM_ERASEBKGND:
                return new IntPtr(1);
            case WM_PAINT:
            {
                IntPtr dc = BeginPaint(window, out PAINTSTRUCT ps);
                PresentFrame(dc);
                EndPaint(window, ref ps);
                return IntPtr.Zero;
            }
            case WM_SIZE:
            {
                int type = wParam.ToInt32();
                minimized = type == SIZE_MINIMIZED;
                if (!minimized)
                {
                    long l = lParam.ToInt64();
                    clientWidth = (int)(l & 0xFFFF);
                    clientHeight = (int)((l >> 16) & 0xFFFF);
                }
                return IntPtr.Zero;
            }
            case WM_GETMINMAXINFO:
            {
                MINMAXINFO info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                info.ptMinTrackSize.X = 820;
                info.ptMinTrackSize.Y = 560;
                Marshal.StructureToPtr(info, lParam, false);
                return IntPtr.Zero;
            }
            case WM_DPICHANGED:
            {
                RECT suggested = Marshal.PtrToStructure<RECT>(lParam);
                SetWindowPos(window, IntPtr.Zero, suggested.Left, suggested.Top,
                    suggested.Right - suggested.Left, suggested.Bottom - suggested.Top,
                    SWP_NOZORDER | SWP_NOACTIVATE);
                return IntPtr.Zero;
            }
            case WM_MOUSEMOVE:
                if (!trackingLeave)
                {
                    var tme = new TRACKMOUSEEVENT
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(TRACKMOUSEEVENT)),
                        dwFlags = TME_LEAVE,
                        hwndTrack = window
                    };
                    trackingLeave = TrackMouseEvent(ref tme);
                }
                Enqueue(msg, lParam, 0f);
                return IntPtr.Zero;
            case WM_LBUTTONDOWN:
            case WM_RBUTTONDOWN:
                SetCapture(window);
                Enqueue(msg, lParam, 0f);
                return IntPtr.Zero;
            case WM_LBUTTONUP:
            case WM_RBUTTONUP:
                ReleaseCapture();
                Enqueue(msg, lParam, 0f);
                return IntPtr.Zero;
            case WM_MOUSEWHEEL:
            {
                // Wheel messages carry screen coordinates; convert to client space.
                long l = lParam.ToInt64();
                var p = new POINT { X = (short)(l & 0xFFFF), Y = (short)((l >> 16) & 0xFFFF) };
                ScreenToClient(window, ref p);
                short delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                messages.Enqueue(new PointerMessage { Msg = msg, X = p.X, Y = p.Y, Wheel = delta / 120f });
                return IntPtr.Zero;
            }
            case WM_MOUSELEAVE:
                trackingLeave = false;
                messages.Enqueue(new PointerMessage { Msg = msg });
                return IntPtr.Zero;
        }
        return null;
    }

    private void Enqueue(uint msg, IntPtr lParam, float wheel)
    {
        long l = lParam.ToInt64();
        messages.Enqueue(new PointerMessage
        {
            Msg = msg,
            X = (short)(l & 0xFFFF),
            Y = (short)((l >> 16) & 0xFFFF),
            Wheel = wheel
        });
    }

    // ---- helpers ---------------------------------------------------------------

    private static IntPtr FindMainWindow()
    {
        uint pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != pid || !IsWindowVisible(window)) return true;
            var name = new System.Text.StringBuilder(64);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != "UnityWndClass") return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static RECT WorkAreaFor(IntPtr window)
    {
        IntPtr monitor = MonitorFromWindow(window, MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info)) return info.rcWork;
        return new RECT { Left = 0, Top = 0, Right = Display.main.systemWidth, Bottom = Display.main.systemHeight };
    }

    // ---- Win32 -------------------------------------------------------------------

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const uint WS_EX_APPWINDOW = 0x00040000;
    private const uint CS_HREDRAW = 0x0002, CS_VREDRAW = 0x0001;
    private const int IDC_ARROW = 32512;
    private const int GCLP_HICON = -14, GCLP_HICONSM = -34;
    private const uint WM_GETICON = 0x007F;
    private const int ICON_SMALL = 0, ICON_BIG = 1;
    private const int SW_SHOWNORMAL = 1;
    private const uint PM_REMOVE = 0x0001;
    private const uint WM_SIZE = 0x0005, WM_PAINT = 0x000F, WM_CLOSE = 0x0010, WM_ERASEBKGND = 0x0014,
        WM_GETMINMAXINFO = 0x0024, WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202,
        WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_MOUSEWHEEL = 0x020A, WM_MOUSELEAVE = 0x02A3,
        WM_DPICHANGED = 0x02E0;
    private const int SIZE_MINIMIZED = 1;
    private const uint TME_LEAVE = 0x00000002;
    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
    private const uint BI_RGB = 0, DIB_RGB_COLORS = 0, SRCCOPY = 0x00CC0020;
    private const int HALFTONE = 4;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TRACKMOUSEEVENT
    {
        public uint cbSize;
        public uint dwFlags;
        public IntPtr hwndTrack;
        public uint dwHoverTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool AdjustWindowRectEx(ref RECT rect, uint style, bool menu, uint exStyle);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT tme);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr instance, int cursor);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder name, int max);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr dc, int mode);
    [DllImport("gdi32.dll")]
    private static extern int StretchDIBits(IntPtr dc, int xDest, int yDest, int destWidth, int destHeight,
        int xSrc, int ySrc, int srcWidth, int srcHeight, byte[] bits, ref BITMAPINFOHEADER info, uint usage, uint rop);
}

/// <summary>
/// The analytics canvas' raycaster. It takes no part in the main window's input — the
/// shared EventSystem would otherwise hit-test the off-screen canvas with the main
/// window's mouse position — and only answers the queries made by
/// <see cref="ExternalPointerDispatcher"/> on behalf of the native analytics window.
/// </summary>
public sealed class ExternalWindowRaycaster : GraphicRaycaster
{
    private static readonly List<Graphic> Sorted = new List<Graphic>();
    private bool active;
    private Canvas ownCanvas;

    public void RaycastForWindow(PointerEventData eventData, List<RaycastResult> results)
    {
        active = true;
        try { Raycast(eventData, results); }
        finally { active = false; }
    }

    public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
    {
        if (!active) return;
        if (ownCanvas == null) ownCanvas = GetComponent<Canvas>();
        if (ownCanvas == null) return;
        Camera cam = eventCamera;
        Vector2 position = eventData.position;

        Sorted.Clear();
        IList<Graphic> graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(ownCanvas);
        for (int i = 0; i < graphics.Count; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || !graphic.raycastTarget || graphic.canvasRenderer.cull || graphic.depth == -1) continue;
            if (!graphic.isActiveAndEnabled) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, position, cam, graphic.raycastPadding)) continue;
            if (!graphic.Raycast(position, cam)) continue;
            Sorted.Add(graphic);
        }
        Sorted.Sort((a, b) => b.depth.CompareTo(a.depth));

        for (int i = 0; i < Sorted.Count; i++)
        {
            Graphic graphic = Sorted[i];
            resultAppendList.Add(new RaycastResult
            {
                gameObject = graphic.gameObject,
                module = this,
                distance = 0f,
                screenPosition = position,
                displayIndex = 0,
                index = resultAppendList.Count,
                depth = graphic.depth,
                sortingLayer = ownCanvas.sortingLayerID,
                sortingOrder = ownCanvas.sortingOrder,
                worldPosition = cam != null ? cam.ScreenToWorldPoint(new Vector3(position.x, position.y, ownCanvas.planeDistance)) : Vector3.zero,
                worldNormal = -Vector3.forward
            });
        }
        Sorted.Clear();
    }
}

/// <summary>
/// Replays one mouse pointer's events into a single canvas, following the same enter/exit,
/// press/click and drag rules as Unity's own pointer input modules, so every existing UI
/// handler on that canvas works unchanged.
/// </summary>
public sealed class ExternalPointerDispatcher
{
    private readonly ExternalWindowRaycaster raycaster;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private PointerEventData data;
    private Vector2 lastPosition;
    private bool hasPosition;

    public ExternalPointerDispatcher(ExternalWindowRaycaster raycaster)
    {
        this.raycaster = raycaster;
    }

    private PointerEventData Data
    {
        get
        {
            if (data == null && EventSystem.current != null)
                data = new PointerEventData(EventSystem.current) { pointerId = 7001 };
            return data;
        }
    }

    public void Move(Vector2 position) => UpdatePosition(position, true);

    /// <summary>Re-evaluates what is under a stationary pointer.</summary>
    public void Refresh()
    {
        if (hasPosition) UpdatePosition(lastPosition, false);
    }

    private void UpdatePosition(Vector2 position, bool moved)
    {
        PointerEventData ped = Data;
        if (ped == null) return;
        ped.delta = hasPosition ? position - lastPosition : Vector2.zero;
        ped.position = position;
        lastPosition = position;
        hasPosition = true;
        RaycastResult hit = CastAt(position);
        ped.pointerCurrentRaycast = hit;
        HandleEnterExit(ped, hit.gameObject);

        if (moved)
        {
            for (int i = 0; i < ped.hovered.Count; i++)
                if (ped.hovered[i] != null)
                    ExecuteEvents.Execute(ped.hovered[i], ped, ExecuteEvents.pointerMoveHandler);
            ProcessDrag(ped);
        }
    }

    public void Press(Vector2 position, PointerEventData.InputButton button)
    {
        UpdatePosition(position, true);
        PointerEventData ped = Data;
        if (ped == null) return;
        ped.button = button;
        ped.eligibleForClick = true;
        ped.delta = Vector2.zero;
        ped.dragging = false;
        ped.useDragThreshold = true;
        ped.pressPosition = position;
        ped.pointerPressRaycast = ped.pointerCurrentRaycast;

        GameObject over = ped.pointerCurrentRaycast.gameObject;
        EventSystem system = EventSystem.current;
        if (system != null && ExecuteEvents.GetEventHandler<ISelectHandler>(over) != system.currentSelectedGameObject)
            system.SetSelectedGameObject(null, ped);

        GameObject pressed = ExecuteEvents.ExecuteHierarchy(over, ped, ExecuteEvents.pointerDownHandler);
        GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(over);
        if (pressed == null) pressed = clickHandler;

        float time = Time.unscaledTime;
        if (pressed == ped.lastPress && time - ped.clickTime < 0.3f) ped.clickCount++;
        else ped.clickCount = 1;
        ped.clickTime = time;

        ped.pointerPress = pressed;
        ped.rawPointerPress = over;
        ped.pointerClick = clickHandler;
        ped.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(over);
        if (ped.pointerDrag != null)
            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.initializePotentialDrag);
    }

    public void Release(Vector2 position, PointerEventData.InputButton button)
    {
        UpdatePosition(position, true);
        PointerEventData ped = Data;
        if (ped == null) return;
        ped.button = button;
        GameObject over = ped.pointerCurrentRaycast.gameObject;

        ExecuteEvents.Execute(ped.pointerPress, ped, ExecuteEvents.pointerUpHandler);
        GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(over);
        if (ped.pointerClick != null && ped.pointerClick == clickHandler && ped.eligibleForClick)
            ExecuteEvents.Execute(ped.pointerClick, ped, ExecuteEvents.pointerClickHandler);
        else if (ped.dragging)
            ExecuteEvents.ExecuteHierarchy(over, ped, ExecuteEvents.dropHandler);

        if (ped.pointerDrag != null && ped.dragging)
            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.endDragHandler);

        ped.eligibleForClick = false;
        ped.pointerPress = null;
        ped.rawPointerPress = null;
        ped.pointerClick = null;
        ped.dragging = false;
        ped.pointerDrag = null;
    }

    public void Scroll(Vector2 position, float wheel)
    {
        UpdatePosition(position, true);
        PointerEventData ped = Data;
        if (ped == null) return;
        ped.scrollDelta = new Vector2(0f, wheel);
        ExecuteEvents.ExecuteHierarchy(ped.pointerCurrentRaycast.gameObject, ped, ExecuteEvents.scrollHandler);
        ped.scrollDelta = Vector2.zero;
    }

    /// <summary>The pointer left the window: everything it was over gets an exit.</summary>
    public void Leave()
    {
        PointerEventData ped = Data;
        hasPosition = false;
        if (ped == null) return;
        ped.pointerCurrentRaycast = default;
        HandleEnterExit(ped, null);
    }

    public void Reset()
    {
        Leave();
        if (data == null) return;
        data.pointerPress = null;
        data.pointerDrag = null;
        data.dragging = false;
        data.eligibleForClick = false;
    }

    private void ProcessDrag(PointerEventData ped)
    {
        if (ped.pointerDrag == null) return;
        if (!ped.dragging)
        {
            float threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10;
            if (!ped.useDragThreshold || (ped.pressPosition - ped.position).sqrMagnitude >= threshold * threshold)
            {
                ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.beginDragHandler);
                ped.dragging = true;
            }
        }
        if (!ped.dragging) return;
        if (ped.pointerPress != ped.pointerDrag)
        {
            ExecuteEvents.Execute(ped.pointerPress, ped, ExecuteEvents.pointerUpHandler);
            ped.eligibleForClick = false;
            ped.pointerPress = null;
            ped.rawPointerPress = null;
        }
        ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.dragHandler);
    }

    private RaycastResult CastAt(Vector2 position)
    {
        hits.Clear();
        PointerEventData ped = Data;
        if (ped == null || raycaster == null) return default;
        raycaster.RaycastForWindow(ped, hits);
        return hits.Count > 0 ? hits[0] : default;
    }

    private static void HandleEnterExit(PointerEventData ped, GameObject newEnter)
    {
        if (newEnter == null || ped.pointerEnter == null)
        {
            for (int i = 0; i < ped.hovered.Count; i++)
                if (ped.hovered[i] != null)
                    ExecuteEvents.Execute(ped.hovered[i], ped, ExecuteEvents.pointerExitHandler);
            ped.hovered.Clear();
            if (newEnter == null)
            {
                ped.pointerEnter = null;
                return;
            }
        }

        if (ped.pointerEnter == newEnter) return;

        GameObject common = CommonRoot(ped.pointerEnter, newEnter);
        if (ped.pointerEnter != null)
        {
            Transform t = ped.pointerEnter.transform;
            while (t != null)
            {
                if (common != null && common.transform == t) break;
                ExecuteEvents.Execute(t.gameObject, ped, ExecuteEvents.pointerExitHandler);
                ped.hovered.Remove(t.gameObject);
                t = t.parent;
            }
        }

        ped.pointerEnter = newEnter;
        Transform e = newEnter.transform;
        while (e != null && e.gameObject != common)
        {
            ExecuteEvents.Execute(e.gameObject, ped, ExecuteEvents.pointerEnterHandler);
            ped.hovered.Add(e.gameObject);
            e = e.parent;
        }
    }

    private static GameObject CommonRoot(GameObject a, GameObject b)
    {
        if (a == null || b == null) return null;
        Transform ta = a.transform;
        while (ta != null)
        {
            Transform tb = b.transform;
            while (tb != null)
            {
                if (ta == tb) return ta.gameObject;
                tb = tb.parent;
            }
            ta = ta.parent;
        }
        return null;
    }
}
