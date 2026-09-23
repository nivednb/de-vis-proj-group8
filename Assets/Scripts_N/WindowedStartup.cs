using UnityEngine;

/// <summary>
/// Makes the desktop player always start as a normal, resizable application window (with the
/// system title bar, minimise / maximise / close and free positioning) even when an earlier
/// build stored a full-screen preference in the registry.
/// </summary>
public static class WindowedStartup
{
    private const int PreferredWidth = 1600;
    private const int PreferredHeight = 900;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_STANDALONE && !UNITY_EDITOR
        // Analytics runs in a second window, and the simulation must keep running while that
        // window has focus.
        Application.runInBackground = true;
        if (Screen.fullScreenMode == FullScreenMode.Windowed) return;

        // Leave room for the taskbar and the window frame on small displays.
        int width = Mathf.Min(PreferredWidth, Mathf.RoundToInt(Display.main.systemWidth * 0.9f));
        int height = Mathf.Min(PreferredHeight, Mathf.RoundToInt(Display.main.systemHeight * 0.85f));
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
#endif
    }
}
