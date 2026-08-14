using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Optional release-QA hook. Pass -ptmeoh-capture followed by a PNG path to
/// capture the fully rendered player frame after startup.
/// </summary>
public sealed class RuntimeValidationCapture : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TryCreate()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string environmentPath = System.Environment.GetEnvironmentVariable("PTMEOH_CAPTURE_PATH");
        int environmentFocus = -1;
        int.TryParse(System.Environment.GetEnvironmentVariable("PTMEOH_CAPTURE_FOCUS"), out environmentFocus);
        int focusIndex = -1;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-ptmeoh-focus" && int.TryParse(args[i + 1], out int parsed))
            {
                focusIndex = parsed;
            }
        }

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "-ptmeoh-capture") continue;
            RuntimeValidationCapture capture =
                new GameObject("Runtime Validation Capture").AddComponent<RuntimeValidationCapture>();
            capture.StartCoroutine(capture.Capture(args[i + 1], focusIndex));
            return;
        }

        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            RuntimeValidationCapture capture =
                new GameObject("Runtime Validation Capture").AddComponent<RuntimeValidationCapture>();
            capture.StartCoroutine(capture.Capture(environmentPath, environmentFocus));
        }
    }

    private IEnumerator Capture(string path, int focusIndex)
    {
        yield return null;
        if (focusIndex >= 0)
        {
            OrbitCameraController cameraController = FindFirstObjectByType<OrbitCameraController>();
            cameraController?.FocusModule(focusIndex);
        }

        yield return new WaitForSecondsRealtime(4f);
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        ScreenCapture.CaptureScreenshot(fullPath);
        Debug.Log($"Runtime validation screenshot requested: {fullPath}");
        yield return new WaitForSecondsRealtime(1f);
        Destroy(gameObject);
    }
}
