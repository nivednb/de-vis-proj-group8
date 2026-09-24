using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared export plumbing for the analytics graphs. A graph writes a CSV of its data
/// "until now" and a PNG of the panel, both timestamped, into a single folder next to the
/// project / build. Deliberately NOT wired into the live time-series graph (its trail is
/// continuous and scrolling — "until now" is not a meaningful snapshot there).
/// </summary>
public static class GraphExportUtil
{
    public static readonly string ExportDirectory = ResolveDirectory();

    private static string ResolveDirectory()
    {
        try
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Analytics Exports"));
        }
        catch
        {
            return Path.Combine(Application.persistentDataPath, "Analytics Exports");
        }
    }

    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "graph";
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    /// <summary>Quotes a value for a CSV cell if needed.</summary>
    public static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static string WriteText(string baseName, string extension, string contents)
    {
        try
        {
            Directory.CreateDirectory(ExportDirectory);
            string path = Path.Combine(ExportDirectory, baseName + "." + extension);
            File.WriteAllText(path, contents);
            return path;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GraphExport] Failed to write {baseName}.{extension}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Grabs the pixels under <paramref name="region"/> at end of frame and writes a PNG.
    /// <paramref name="hideDuringCapture"/> objects (e.g. the export button itself) are
    /// switched off for the single capture frame so they don't appear in the image.
    /// </summary>
    public static IEnumerator CaptureRegionPng(Canvas canvas, RectTransform region, string baseName,
        Action<string> onComplete, params GameObject[] hideDuringCapture)
    {
        if (hideDuringCapture != null)
            foreach (GameObject go in hideDuringCapture)
                if (go != null) go.SetActive(false);

        yield return new WaitForEndOfFrame();

        string path = null;
        Texture2D tex = null;
        try
        {
            Vector3[] corners = new Vector3[4];
            region.GetWorldCorners(corners);
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 c0 = cam != null ? RectTransformUtility.WorldToScreenPoint(cam, corners[0]) : (Vector2)corners[0];
            Vector2 c2 = cam != null ? RectTransformUtility.WorldToScreenPoint(cam, corners[2]) : (Vector2)corners[2];

            int x = Mathf.RoundToInt(Mathf.Min(c0.x, c2.x));
            int y = Mathf.RoundToInt(Mathf.Min(c0.y, c2.y));
            int w = Mathf.RoundToInt(Mathf.Abs(c2.x - c0.x));
            int h = Mathf.RoundToInt(Mathf.Abs(c2.y - c0.y));
            // A canvas drawn into a render texture (the separate analytics window) is read
            // from that texture; an overlay canvas from the back buffer.
            RenderTexture source = cam != null ? cam.targetTexture : null;
            int sourceWidth = source != null ? source.width : Screen.width;
            int sourceHeight = source != null ? source.height : Screen.height;
            x = Mathf.Clamp(x, 0, Mathf.Max(0, sourceWidth - 1));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, sourceHeight - 1));
            w = Mathf.Clamp(w, 1, sourceWidth - x);
            h = Mathf.Clamp(h, 1, sourceHeight - y);

            tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            if (source != null) RenderTexture.active = source;
            tex.ReadPixels(new Rect(x, y, w, h), 0, 0);
            RenderTexture.active = previous;
            tex.Apply();

            Directory.CreateDirectory(ExportDirectory);
            path = Path.Combine(ExportDirectory, baseName + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        catch (Exception e)
        {
            Debug.LogError($"[GraphExport] PNG capture failed: {e.Message}");
            path = null;
        }
        finally
        {
            if (tex != null) UnityEngine.Object.Destroy(tex);
            if (hideDuringCapture != null)
                foreach (GameObject go in hideDuringCapture)
                    if (go != null) go.SetActive(true);
        }

        onComplete?.Invoke(path);
    }

    public static void ShowToast(Canvas canvas, Font font, string message)
    {
        Debug.Log($"[GraphExport] {message}");
        if (canvas == null) return;

        bool failed = message.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      message.IndexOf("nothing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      message.IndexOf("no data", StringComparison.OrdinalIgnoreCase) >= 0;

        RectTransform rt = UITheme.Card("Export Toast", canvas.transform, 22f, UITheme.Ink, 26f, 10f, 0.3f, false);
        rt.GetComponent<UIRaycastTarget>().raycastTarget = false;
        Image icon = UITheme.IconImage("Icon", rt, failed ? UITheme.Icon.Info : UITheme.Icon.Check, 18f,
            failed ? UITheme.Hex("FCD34D") : UITheme.Hex("4ADE80"));
        UITheme.TopLeft(icon.rectTransform, 18f, 13f, 18f, 18f);
        Text text = UITheme.Label("Text", rt, message, 13f, UITheme.Weight.SemiBold, Color.white);
        UITheme.TopLeft(text.rectTransform, 46f, 0f, 1000f, 44f);
        float width = Mathf.Min(1100f, 46f + Mathf.Ceil(text.preferredWidth) + 22f);
        text.rectTransform.sizeDelta = new Vector2(width - 60f, 44f);
        UITheme.BottomCenter(rt, 0f, 92f, width, 44f);

        rt.gameObject.AddComponent<ExportToast>();
        rt.SetAsLastSibling();
    }

    private sealed class ExportToast : MonoBehaviour
    {
        private float life = 5f;
        private CanvasGroup group;

        private void Awake() => group = gameObject.AddComponent<CanvasGroup>();

        private void Update()
        {
            life -= Time.unscaledDeltaTime;
            if (group != null && life < 1.4f) group.alpha = Mathf.Clamp01(life / 1.4f);
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
