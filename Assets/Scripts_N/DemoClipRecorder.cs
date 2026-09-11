using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Development utility: captures a numbered PNG sequence of the running application so a short
/// demo clip can be encoded from it afterwards. A companion to RuntimeValidationCapture, which
/// only grabs a single frame.
///
/// Dormant unless <see cref="Begin"/> is called, so it costs a shipped build nothing.
/// </summary>
public sealed class DemoClipRecorder : MonoBehaviour
{
    public static bool IsRecording { get; private set; }
    public static int FramesWritten { get; private set; }
    /// <summary>Wall-clock seconds the capture actually spanned — encode at
    /// FramesWritten / ElapsedSeconds to play the clip back at real-time speed.</summary>
    public static float ElapsedSeconds { get; private set; }

    public static void Begin(string directory, int frameCount)
    {
        if (IsRecording) return;
        GameObject host = new GameObject("Demo Clip Recorder");
        DemoClipRecorder recorder = host.AddComponent<DemoClipRecorder>();
        recorder.StartCoroutine(recorder.Capture(directory, frameCount));
    }

    private IEnumerator Capture(string directory, int frameCount)
    {
        Directory.CreateDirectory(directory);
        foreach (string stale in Directory.GetFiles(directory, "frame_*.png")) File.Delete(stale);

        IsRecording = true;
        FramesWritten = 0;
        ElapsedSeconds = 0f;
        float start = Time.realtimeSinceStartup;

        for (int i = 0; i < frameCount; i++)
        {
            // ScreenCapture.CaptureScreenshot rather than WaitForEndOfFrame +
            // CaptureScreenshotAsTexture: the end-of-frame signal never fires while the
            // editor's Game view is unfocused, which silently captures nothing.
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"frame_{i:0000}.png"));
            FramesWritten = i + 1;
            ElapsedSeconds = Time.realtimeSinceStartup - start;
            // Give the request a frame to land before queueing the next one.
            yield return null;
            yield return null;
        }

        IsRecording = false;
        Destroy(gameObject);
    }
}
