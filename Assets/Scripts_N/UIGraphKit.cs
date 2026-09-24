using UnityEngine;
using UnityEngine.UI;
using W = UITheme.Weight;

/// <summary>
/// Shared Daylight styling for the analytics graphs: the dark hover tooltip, the module
/// colour legend, the light plot surface with gridlines and tick labels.
/// </summary>
public static class UIGraphKit
{
    public static string ModuleDisplayName(string module) => UITheme.Pretty(module);

    /// <summary>A vertical "colour = module" legend; returns its rect (height already set).</summary>
    public static RectTransform ModuleLegend(RectTransform parent, string title, bool includeSeed, float width)
    {
        RectTransform legend = UITheme.NewRect("Legend", parent);
        Text t = UITheme.Label("Title", legend, title, 12f, W.ExtraBold, UITheme.Ink, TextAnchor.MiddleLeft, true);
        UITheme.TopLeft(t.rectTransform, 0f, 0f, width, 18f);
        float y = 26f;
        if (includeSeed)
        {
            AddLegendRow(legend, "Starting point", UITheme.Muted, y, width);
            y += 21f;
        }
        foreach (var entry in GraphVisualUtils.ModulePalette)
        {
            AddLegendRow(legend, ModuleDisplayName(entry.Module), entry.Color, y, width);
            y += 21f;
        }
        legend.sizeDelta = new Vector2(width, y);
        return legend;
    }

    private static void AddLegendRow(RectTransform legend, string label, Color color, float y, float width)
    {
        Image dot = UITheme.Dot("Swatch", legend, 10f, color);
        UITheme.TopLeft(dot.rectTransform, 0f, y + 4f, 10f, 10f);
        Text t = UITheme.Label(label, legend, label, 12f, W.SemiBold, UITheme.Muted);
        UITheme.TopLeft(t.rectTransform, 18f, y, width - 18f, 18f);
    }

    /// <summary>Rounded pale plot background placed as the first child of the plot area.</summary>
    public static Image PlotBackground(RectTransform plotArea)
    {
        Image bg = UITheme.Panel("Plot Background", plotArea, UITheme.Sunken2, 8f);
        UITheme.Fill(bg.rectTransform, -6f, -6f, -6f, -2f);
        bg.transform.SetAsFirstSibling();
        return bg;
    }

    /// <summary>Horizontal gridlines at evenly spaced fractions of the plot height.</summary>
    public static void HorizontalGrid(RectTransform plotArea, int divisions, bool baseline = true)
    {
        for (int i = 0; i <= divisions; i++)
        {
            float f = i / (float)divisions;
            bool isBase = i == 0 && baseline;
            Image line = UITheme.Panel(isBase ? "Baseline" : "Grid " + i, plotArea, isBase ? UITheme.LineStrong : UITheme.Line);
            RectTransform r = line.rectTransform;
            r.anchorMin = new Vector2(0f, f);
            r.anchorMax = new Vector2(1f, f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(0f, isBase ? 1.5f : 1f);
            r.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>Tick labels along the left of the plot (evenly spaced, bottom to top).</summary>
    public static Text[] YTicks(RectTransform root, RectTransform plotArea, int count, float gutterRight)
    {
        var ticks = new Text[count];
        for (int i = 0; i < count; i++)
        {
            Text t = UITheme.Label("Y Tick " + i, plotArea, "", 11.5f, W.SemiBold, UITheme.Subtle, TextAnchor.MiddleRight);
            RectTransform r = t.rectTransform;
            float f = count == 1 ? 0f : i / (float)(count - 1);
            r.anchorMin = r.anchorMax = new Vector2(0f, f);
            r.pivot = new Vector2(1f, 0.5f);
            r.sizeDelta = new Vector2(48f, 16f);
            r.anchoredPosition = new Vector2(-gutterRight, 0f);
            ticks[i] = t;
        }
        return ticks;
    }

    /// <summary>Tick labels under the plot (evenly spaced, left to right).</summary>
    public static Text[] XTicks(RectTransform plotArea, int count, float below)
    {
        var ticks = new Text[count];
        for (int i = 0; i < count; i++)
        {
            float f = count == 1 ? 0f : i / (float)(count - 1);
            TextAnchor anchor = i == 0 ? TextAnchor.UpperLeft : i == count - 1 ? TextAnchor.UpperRight : TextAnchor.UpperCenter;
            Text t = UITheme.Label("X Tick " + i, plotArea, "", 11.5f, W.SemiBold, UITheme.Subtle, anchor);
            RectTransform r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(f, 0f);
            r.pivot = new Vector2(i == 0 ? 0f : i == count - 1 ? 1f : 0.5f, 1f);
            r.sizeDelta = new Vector2(80f, 16f);
            r.anchoredPosition = new Vector2(0f, -below);
            ticks[i] = t;
        }
        return ticks;
    }

    /// <summary>Axis title rotated along the left edge of <paramref name="root"/>.</summary>
    public static Text RotatedYTitle(RectTransform root, RectTransform plotArea, string text)
    {
        Text t = UITheme.Label("Y Axis Name", root, text, 12f, W.Bold, UITheme.Muted, TextAnchor.MiddleCenter);
        RectTransform r = t.rectTransform;
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        // Rotated 90°, so the rect's width runs vertically: match the plot's vertical extent.
        r.localEulerAngles = new Vector3(0f, 0f, 90f);
        r.sizeDelta = new Vector2(16f, 0f);
        r.anchoredPosition = new Vector2(8f, 0f);
        return t;
    }

    public static string FormatTick(float v)
    {
        float a = Mathf.Abs(v);
        if (a >= 1000f) return v.ToString("N0");
        if (a >= 100f) return v.ToString("F0");
        if (a >= 10f) return v.ToString("0.#");
        return v.ToString("0.##");
    }
}

/// <summary>Dark hover bubble that sizes itself to its text and stays inside its graph.</summary>
public sealed class UIGraphTooltip : MonoBehaviour
{
    private RectTransform rect;
    private RectTransform root;
    private Text text;

    public static UIGraphTooltip Create(RectTransform root)
    {
        RectTransform rt = UITheme.NewRect("Tooltip", root);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = Vector2.zero;
        UITheme.TooltipSurface(rt);
        Text t = UITheme.Label("Tooltip Text", rt, "", 12.5f, W.SemiBold, Color.white, TextAnchor.UpperLeft);
        t.lineSpacing = 1.12f;
        UITheme.Fill(t.rectTransform, 12f, 9f, 12f, 9f);
        UIGraphTooltip tip = rt.gameObject.AddComponent<UIGraphTooltip>();
        tip.rect = rt;
        tip.root = root;
        tip.text = t;
        rt.gameObject.SetActive(false);
        return tip;
    }

    /// <summary>Shows the bubble near <paramref name="localPoint"/> (in the root's local space).</summary>
    public void Show(string content, Vector2 localPoint)
    {
        text.text = content;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        float w = Mathf.Ceil(text.preferredWidth) + 24f;
        float h = Mathf.Ceil(text.preferredHeight) + 18f;
        rect.sizeDelta = new Vector2(w, h);
        Rect r = root.rect;
        Vector2 centre = r.center;
        float x = localPoint.x + 16f;
        if (x + w > r.xMax - 4f) x = localPoint.x - 16f - w;
        float y = localPoint.y + 16f;
        if (y + h > r.yMax - 4f) y = localPoint.y - 16f - h;
        x = Mathf.Max(r.xMin + 4f, x);
        y = Mathf.Max(r.yMin + 4f, y);
        // Anchored at the root's centre, so convert from pivot-relative to centre-relative.
        rect.anchoredPosition = new Vector2(x, y) - centre;
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
