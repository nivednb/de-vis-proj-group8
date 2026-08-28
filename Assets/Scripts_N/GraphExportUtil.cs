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
            x = Mathf.Clamp(x, 0, Mathf.Max(0, Screen.width - 1));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, Screen.height - 1));
            w = Mathf.Clamp(w, 1, Screen.width - x);
            h = Mathf.Clamp(h, 1, Screen.height - y);

            tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(x, y, w, h), 0, 0);
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

    private static readonly Color ToastBg = new Color(0.03f, 0.16f, 0.11f, 0.97f);
    private static readonly Color ToastBorder = new Color(0.20f, 0.90f, 0.52f, 0.85f);

    public static void ShowToast(Canvas canvas, Font font, string message)
    {
        Debug.Log($"[GraphExport] {message}");
        if (canvas == null) return;

        GameObject go = new GameObject("Export Toast", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 70f);
        rt.sizeDelta = new Vector2(860f, 32f);

        Image bg = go.AddComponent<Image>();
        bg.color = ToastBg;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = ToastBorder;
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(go.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = message;
        text.fontSize = 12;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform trt = textObject.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12f, 4f);
        trt.offsetMax = new Vector2(-12f, -4f);

        go.AddComponent<ExportToast>();
        go.transform.SetAsLastSibling();
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
