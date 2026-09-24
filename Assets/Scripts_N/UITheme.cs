using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The "Daylight" design system every runtime UI in the application is built from: one
/// palette, one typeface (Manrope, four weights), rounded surfaces with soft shadows, a
/// stroke-icon set and a single button behaviour. Sizes are authored in the 1440 x 810
/// reference space the design mockups use (<see cref="ConfigureScaler"/>), so a value here
/// reads the same as the value in the mockup.
///
/// Every sprite is generated procedurally from an analytic signed distance (anti-aliased,
/// mip-mapped), so nothing depends on imported UI art.
/// </summary>
public static class UITheme
{
    // ---- palette -------------------------------------------------------------

    public static readonly Color Ink = Hex("0F172A");
    public static readonly Color Ink2 = Hex("334155");
    public static readonly Color Muted = Hex("475569");
    public static readonly Color Subtle = Hex("64748B");
    public static readonly Color Faint = Hex("94A3B8");
    public static readonly Color Line = Hex("E2E8F0");
    public static readonly Color LineStrong = Hex("CBD5E1");
    public static readonly Color Surface = Color.white;
    public static readonly Color Glass = new Color(1f, 1f, 1f, 0.96f);
    public static readonly Color Sunken = Hex("F1F5F9");
    public static readonly Color Sunken2 = Hex("F8FAFC");
    public static readonly Color Accent = Hex("1D4ED8");
    public static readonly Color AccentHover = Hex("1E40AF");
    public static readonly Color AccentPressed = Hex("1E3A8A");
    public static readonly Color AccentSoft = Hex("EFF6FF");
    public static readonly Color AccentSoft2 = Hex("DBEAFE");
    public static readonly Color AccentInk = Hex("1E3A8A");
    public static readonly Color Success = Hex("16A34A");
    public static readonly Color SuccessInk = Hex("166534");
    public static readonly Color SuccessSoft = Hex("DCFCE7");
    public static readonly Color Warning = Hex("D97706");
    public static readonly Color WarningInk = Hex("92400E");
    public static readonly Color WarningSoft = Hex("FEF3C7");
    public static readonly Color Danger = Hex("DC2626");
    public static readonly Color DangerInk = Hex("B91C1C");
    public static readonly Color DangerSoft = Hex("FEF2F2");
    public static readonly Color DangerSoft2 = Hex("FEE2E2");
    public static readonly Color Scrim = new Color(0.06f, 0.09f, 0.16f, 0.5f);
    public static readonly Color ShadowInk = new Color(0.06f, 0.09f, 0.16f, 1f);

    /// <summary>Reference resolution of every runtime canvas — the mockups' frame size.</summary>
    public static readonly Vector2 ReferenceResolution = new Vector2(1440f, 810f);

    public static Color Hex(string rgb)
    {
        return ColorUtility.TryParseHtmlString("#" + rgb, out Color color) ? color : Color.magenta;
    }

    public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    /// <summary>A pale wash of a colour, as used behind icon badges.</summary>
    public static Color Tint(Color c, float amount = 0.14f) => Color.Lerp(Color.white, new Color(c.r, c.g, c.b, 1f), amount);

    /// <summary>Chemical formulas with proper subscripts for display ("H2/CO2" → "H₂/CO₂").
    /// Internal keys (slider labels, CSV headers) keep the plain ASCII form.</summary>
    public static string Pretty(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("CO2", "CO₂").Replace("H2O", "H₂O").Replace("H2", "H₂").Replace("O2", "O₂");
    }

    public static void ConfigureScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
    }

    // ---- fonts ---------------------------------------------------------------

    public enum Weight { Medium, SemiBold, Bold, ExtraBold }

    private static readonly Font[] fonts = new Font[4];

    public static Font Font(Weight weight)
    {
        int i = (int)weight;
        if (fonts[i] != null) return fonts[i];
        string file = weight switch
        {
            Weight.Medium => "Fonts/Manrope-Medium",
            Weight.SemiBold => "Fonts/Manrope-SemiBold",
            Weight.Bold => "Fonts/Manrope-Bold",
            _ => "Fonts/Manrope-ExtraBold",
        };
        fonts[i] = Resources.Load<Font>(file);
        if (fonts[i] == null) fonts[i] = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return fonts[i];
    }

    // ---- procedural sprites --------------------------------------------------

    private const float TexelsPerUnit = 2f; // sprites are generated at 2x for crisp edges

    private static readonly Dictionary<int, Sprite> roundedCache = new Dictionary<int, Sprite>();
    private static readonly Dictionary<long, Sprite> outlineCache = new Dictionary<long, Sprite>();
    private static readonly Dictionary<long, Sprite> shadowCache = new Dictionary<long, Sprite>();
    private static readonly Dictionary<Icon, Sprite> iconCache = new Dictionary<Icon, Sprite>();
    private static Sprite circleSprite;
    private static Sprite knobSprite;
    private static Sprite ringSprite;

    /// <summary>A 9-sliced rounded rectangle with the given corner radius (UI units). A radius
    /// larger than half the element's height gives a capsule.</summary>
    public static Sprite Rounded(float radius)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(radius), 1, 40);
        if (roundedCache.TryGetValue(r, out Sprite cached) && cached != null) return cached;
        int R = Mathf.RoundToInt(r * TexelsPerUnit);
        int size = R * 2 + 4;
        Texture2D tex = NewTexture(size, size, "Rounded " + r);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = RoundedRectSdf(x + 0.5f - half, y + 0.5f - half, half, half, R);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * TexelsPerUnit,
            0, SpriteMeshType.FullRect, new Vector4(R + 1, R + 1, R + 1, R + 1));
        roundedCache[r] = sprite;
        return sprite;
    }

    /// <summary>A 9-sliced rounded-rectangle outline (border only).</summary>
    public static Sprite RoundedOutline(float radius, float thickness = 1f)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(radius), 1, 40);
        int t10 = Mathf.RoundToInt(thickness * 10f);
        long key = r * 1000L + t10;
        if (outlineCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        int R = Mathf.RoundToInt(r * TexelsPerUnit);
        float T = thickness * TexelsPerUnit;
        int size = R * 2 + 4;
        Texture2D tex = NewTexture(size, size, "Outline " + r);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = RoundedRectSdf(x + 0.5f - half, y + 0.5f - half, half, half, R);
            float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(0.5f + d + T);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * TexelsPerUnit,
            0, SpriteMeshType.FullRect, new Vector4(R + 1, R + 1, R + 1, R + 1));
        outlineCache[key] = sprite;
        return sprite;
    }

    /// <summary>A soft drop-shadow for a rounded rectangle; the image it goes on is the card
    /// rect grown by <paramref name="blur"/> on every side.</summary>
    public static Sprite Shadow(float radius, float blur)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(radius), 0, 40);
        int b = Mathf.Clamp(Mathf.RoundToInt(blur), 2, 64);
        long key = r * 1000L + b;
        if (shadowCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        int size = (r + b) * 2 + 4;
        Texture2D tex = NewTexture(size, size, "Shadow " + r + "/" + b, false);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        float inner = half - b;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = RoundedRectSdf(x + 0.5f - half, y + 0.5f - half, inner, inner, r);
            // Gaussian-like fall-off outside the card's edge.
            float t = Mathf.Clamp01((d + b * 0.15f) / b);
            float a = Mathf.Exp(-t * t * 4.2f) * (1f - t);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        int border = r + b + 1;
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        shadowCache[key] = sprite;
        return sprite;
    }

    public static Sprite Circle()
    {
        if (circleSprite != null) return circleSprite;
        const int size = 64;
        Texture2D tex = NewTexture(size, size, "Circle");
        var px = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) - (c - 1f);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * TexelsPerUnit);
        return circleSprite;
    }

    /// <summary>Donut used for progress rings (drawn with Image.Type.Filled, radial).</summary>
    public static Sprite Ring()
    {
        if (ringSprite != null) return ringSprite;
        const int size = 256;
        Texture2D tex = NewTexture(size, size, "Ring");
        var px = new Color32[size * size];
        float c = size * 0.5f;
        float outer = c - 2f;
        float width = size * (10f / 104f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float r = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
            float d = Mathf.Abs(r - (outer - width * 0.5f)) - width * 0.5f;
            px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * TexelsPerUnit);
        return ringSprite;
    }

    /// <summary>Slider thumb: white disc, accent ring and a soft shadow, baked in colour.</summary>
    public static Sprite Knob()
    {
        if (knobSprite != null) return knobSprite;
        const int size = 64; // 24 units at 2.67 texels/unit
        Texture2D tex = NewTexture(size, size, "Knob");
        var px = new Color[size * size];
        float c = size * 0.5f;
        float discR = size * (8.5f / 24f);
        float ringW = size * (2.2f / 24f);
        Color accent = Accent;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float rs = Mathf.Sqrt(dx * dx + (dy + size * 0.04f) * (dy + size * 0.04f));
            float shadowA = Mathf.Clamp01(1f - (rs - discR) / (size * 0.14f)) * 0.28f;
            float discA = Mathf.Clamp01(discR - r + 0.5f);
            float ringA = discA * Mathf.Clamp01(r - (discR - ringW) + 0.5f);
            Color col = new Color(ShadowInk.r, ShadowInk.g, ShadowInk.b, shadowA * shadowA * 2.5f);
            col = Over(col, Color.white, discA);
            col = Over(col, accent, ringA);
            px[y * size + x] = col;
        }
        tex.SetPixels(px);
        tex.Apply(true, true);
        knobSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * (size / 24f));
        return knobSprite;
    }

    private static Color Over(Color dst, Color src, float a)
    {
        if (a <= 0f) return dst;
        float outA = a + dst.a * (1f - a);
        if (outA <= 1e-5f) return Color.clear;
        Color rgb = (src * a + dst * (dst.a * (1f - a))) / outA;
        return new Color(rgb.r, rgb.g, rgb.b, outA);
    }

    private static Texture2D NewTexture(int w, int h, string name, bool mips = true)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, mips)
        {
            name = "UITheme " + name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
    }

    /// <summary>Signed distance to a rounded rectangle centred on the origin.</summary>
    private static float RoundedRectSdf(float px, float py, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(px) - (hx - r);
        float qy = Mathf.Abs(py) - (hy - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // ---- icons ---------------------------------------------------------------

    public enum Icon
    {
        Logo, Pause, Play, Reset, Info, Waves, Gauge, ChevronLeft, ChevronRight, ChevronDown,
        Focus, Help, Close, Download, Target, Flask, Bulb, Check, Alert, External, Chart,
        Grid, Route, Sliders, Bolt, Column, Tank, Snow, Split, Swap, PlayCircle, Clock, Dot
    }

    /// <summary>A stroke icon on a 24-unit grid (like the mockups' inline SVGs), rendered once
    /// to a mip-mapped white-on-transparent sprite so an Image can tint it.</summary>
    public static Sprite IconSprite(Icon icon)
    {
        if (iconCache.TryGetValue(icon, out Sprite cached) && cached != null) return cached;
        List<Vector2[]> paths = IconPaths(icon, out float strokeWidth);
        const int size = 72;
        float s = size / 24f;
        Texture2D tex = NewTexture(size, size, "Icon " + icon);
        var px = new Color32[size * size];
        float half = strokeWidth * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Texture rows grow upwards; icon coordinates grow downwards like SVG.
            Vector2 p = new Vector2((x + 0.5f) / s, (size - (y + 0.5f)) / s);
            float d = float.MaxValue;
            foreach (Vector2[] path in paths)
            {
                if (path.Length == 1) { d = Mathf.Min(d, Vector2.Distance(p, path[0]) - 0.35f); continue; }
                for (int i = 0; i < path.Length - 1; i++) d = Mathf.Min(d, SegmentDistance(p, path[i], path[i + 1]));
            }
            float a = Mathf.Clamp01((half - d) * s + 0.5f);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * s);
        iconCache[icon] = sprite;
        return sprite;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len = ab.sqrMagnitude;
        float t = len < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
        return Vector2.Distance(p, a + ab * t);
    }

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    private static Vector2[] Arc(float cx, float cy, float r, float fromDeg, float toDeg, int steps = 40)
    {
        var pts = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
            pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
        }
        return pts;
    }

    private static Vector2[] CircleP(float cx, float cy, float r) => Arc(cx, cy, r, 0f, 360f, 48);

    private static Vector2[] Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int steps = 16)
    {
        var pts = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1f - t;
            pts[i] = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
        return pts;
    }

    private static Vector2[] Concat(params Vector2[][] parts)
    {
        var list = new List<Vector2>();
        foreach (Vector2[] p in parts) list.AddRange(p);
        return list.ToArray();
    }

    private static List<Vector2[]> IconPaths(Icon icon, out float stroke)
    {
        stroke = 2f;
        var p = new List<Vector2[]>();
        switch (icon)
        {
            case Icon.Logo:
                stroke = 1.9f;
                p.Add(new[] { V(12, 2.5f), V(20.2f, 7.25f), V(20.2f, 16.75f), V(12, 21.5f), V(3.8f, 16.75f), V(3.8f, 7.25f), V(12, 2.5f) });
                p.Add(CircleP(12, 12, 3.2f));
                break;
            case Icon.Pause:
                stroke = 2.4f;
                p.Add(new[] { V(9, 5), V(9, 19) });
                p.Add(new[] { V(15, 5), V(15, 19) });
                break;
            case Icon.Play:
                p.Add(new[] { V(7.5f, 5), V(18.5f, 12), V(7.5f, 19), V(7.5f, 5) });
                break;
            case Icon.Reset:
                p.Add(Arc(12, 12, 8.5f, 180f, -134f));
                p.Add(new[] { V(3.5f, 4), V(3.5f, 9), V(8.5f, 9) });
                break;
            case Icon.Info:
                stroke = 1.9f;
                p.Add(CircleP(12, 12, 9));
                p.Add(new[] { V(12, 11), V(12, 16) });
                p.Add(new[] { V(12, 8) });
                break;
            case Icon.Waves:
                p.Add(Concat(Bezier(V(3, 9), V(6, 7), V(9, 11), V(12, 9)), Bezier(V(12, 9), V(15, 7), V(18, 11), V(21, 9))));
                p.Add(Concat(Bezier(V(3, 15), V(6, 13), V(9, 17), V(12, 15)), Bezier(V(12, 15), V(15, 13), V(18, 17), V(21, 15))));
                break;
            case Icon.Gauge:
                p.Add(Arc(12, 17, 8, 180f, 360f));
                p.Add(new[] { V(12, 17), V(16, 12) });
                break;
            case Icon.ChevronLeft:
                stroke = 2.2f;
                p.Add(new[] { V(15, 6), V(9, 12), V(15, 18) });
                break;
            case Icon.ChevronRight:
                stroke = 2.2f;
                p.Add(new[] { V(9, 6), V(15, 12), V(9, 18) });
                break;
            case Icon.ChevronDown:
                stroke = 2.2f;
                p.Add(new[] { V(6, 9), V(12, 15), V(18, 9) });
                break;
            case Icon.Focus:
                p.Add(new[] { V(4, 9), V(4, 4), V(9, 4) });
                p.Add(new[] { V(20, 9), V(20, 4), V(15, 4) });
                p.Add(new[] { V(4, 15), V(4, 20), V(9, 20) });
                p.Add(new[] { V(20, 15), V(20, 20), V(15, 20) });
                p.Add(CircleP(12, 12, 2.5f));
                break;
            case Icon.Help:
                stroke = 1.9f;
                p.Add(CircleP(12, 12, 9));
                p.Add(new[] { V(9.6f, 9.4f), V(10.1f, 8.3f), V(11.1f, 7.6f), V(12.4f, 7.5f), V(13.6f, 8.0f), V(14.3f, 9.0f), V(14.3f, 10.1f), V(13.6f, 11.0f), V(12.6f, 11.7f), V(12.1f, 12.5f), V(12.0f, 13.7f) });
                p.Add(new[] { V(12, 17) });
                break;
            case Icon.Close:
                stroke = 2.2f;
                p.Add(new[] { V(6, 6), V(18, 18) });
                p.Add(new[] { V(18, 6), V(6, 18) });
                break;
            case Icon.Download:
                p.Add(new[] { V(12, 4), V(12, 15) });
                p.Add(new[] { V(7, 10), V(12, 15), V(17, 10) });
                p.Add(new[] { V(5, 20), V(19, 20) });
                break;
            case Icon.Target:
                stroke = 1.9f;
                p.Add(CircleP(12, 12, 8));
                p.Add(CircleP(12, 12, 3.5f));
                p.Add(new[] { V(12, 2), V(12, 5) });
                p.Add(new[] { V(12, 19), V(12, 22) });
                p.Add(new[] { V(2, 12), V(5, 12) });
                p.Add(new[] { V(19, 12), V(22, 12) });
                break;
            case Icon.Flask:
                stroke = 1.8f;
                p.Add(new[] { V(9, 3), V(15, 3) });
                p.Add(new[] { V(10, 3), V(10, 9), V(5, 18), V(5.2f, 19.8f), V(6.7f, 21), V(17.3f, 21), V(18.8f, 19.8f), V(19, 18), V(14, 9), V(14, 3) });
                p.Add(new[] { V(7.5f, 15), V(16.5f, 15) });
                break;
            case Icon.Bulb:
                stroke = 1.9f;
                p.Add(new[] { V(9, 18), V(15, 18) });
                p.Add(new[] { V(10, 21), V(14, 21) });
                p.Add(Concat(Arc(12, 9, 6, 125.5f, 414.5f), new[] { V(14.8f, 14.6f), V(14.5f, 16), V(9.5f, 16), V(9.2f, 14.6f), V(8.5f, 13.9f) }));
                break;
            case Icon.Check:
                stroke = 2.4f;
                p.Add(new[] { V(5, 12.5f), V(9.5f, 17), V(19, 7.5f) });
                break;
            case Icon.Alert:
                stroke = 1.9f;
                p.Add(new[] { V(12, 3.5f), V(21.5f, 20), V(2.5f, 20), V(12, 3.5f) });
                p.Add(new[] { V(12, 9.5f), V(12, 14) });
                p.Add(new[] { V(12, 17) });
                break;
            case Icon.External:
                stroke = 1.9f;
                p.Add(new[] { V(14, 4), V(20, 4), V(20, 10) });
                p.Add(new[] { V(20, 4), V(12, 12) });
                p.Add(new[] { V(18, 14), V(18, 20), V(4, 20), V(4, 6), V(10, 6) });
                break;
            case Icon.Chart:
                p.Add(new[] { V(4, 20), V(4, 11) });
                p.Add(new[] { V(10, 20), V(10, 5) });
                p.Add(new[] { V(16, 20), V(16, 14) });
                p.Add(new[] { V(21, 20), V(3, 20) });
                break;
            case Icon.Grid:
                stroke = 1.8f;
                p.Add(new[] { V(4, 4), V(11, 4), V(11, 11), V(4, 11), V(4, 4) });
                p.Add(new[] { V(13, 4), V(20, 4), V(20, 11), V(13, 11), V(13, 4) });
                p.Add(new[] { V(4, 13), V(11, 13), V(11, 20), V(4, 20), V(4, 13) });
                p.Add(new[] { V(13, 13), V(20, 13), V(20, 20), V(13, 20), V(13, 13) });
                break;
            case Icon.Route:
                stroke = 1.8f;
                p.Add(CircleP(6, 6, 2.5f));
                p.Add(CircleP(18, 18, 2.5f));
                p.Add(Concat(new[] { V(8.5f, 6), V(15, 6) }, Arc(15, 9.5f, 3.5f, -90f, 90f, 12), new[] { V(9, 13), V(9, 13) }, Arc(9, 16.5f, 3.5f, -90f, -270f, 12), new[] { V(15.5f, 20) }));
                break;
            case Icon.Sliders:
                p.Add(new[] { V(4, 6), V(13, 6) }); p.Add(new[] { V(17, 6), V(20, 6) });
                p.Add(new[] { V(4, 12), V(7, 12) }); p.Add(new[] { V(11, 12), V(20, 12) });
                p.Add(new[] { V(4, 18), V(15, 18) }); p.Add(new[] { V(19, 18), V(20, 18) });
                p.Add(new[] { V(15, 4), V(15, 8) }); p.Add(new[] { V(9, 10), V(9, 14) }); p.Add(new[] { V(17, 16), V(17, 20) });
                break;
            case Icon.Bolt:
                stroke = 1.8f;
                p.Add(new[] { V(13, 2.5f), V(4.5f, 13.5f), V(11.5f, 13.5f), V(10.5f, 21.5f), V(19.5f, 10.5f), V(12.5f, 10.5f), V(13, 2.5f) });
                break;
            case Icon.Column:
                stroke = 1.8f;
                p.Add(Concat(Arc(12, 6, 4, 180f, 360f, 16), new[] { V(16, 19), V(8, 19), V(8, 6) }));
                p.Add(new[] { V(8, 10), V(16, 10) });
                p.Add(new[] { V(8, 14), V(16, 14) });
                p.Add(new[] { V(5, 21), V(19, 21) });
                break;
            case Icon.Tank:
                stroke = 1.8f;
                p.Add(ArcEllipse(12, 6.5f, 7, 2.5f, 0f, 360f));
                p.Add(new[] { V(5, 6.5f), V(5, 17.5f) });
                p.Add(new[] { V(19, 6.5f), V(19, 17.5f) });
                p.Add(ArcEllipse(12, 17.5f, 7, 2.5f, 0f, 180f));
                break;
            case Icon.Snow:
                p.Add(new[] { V(12, 3), V(12, 21) });
                p.Add(new[] { V(4.2f, 7.5f), V(19.8f, 16.5f) });
                p.Add(new[] { V(4.2f, 16.5f), V(19.8f, 7.5f) });
                break;
            case Icon.Split:
                p.Add(new[] { V(12, 21), V(12, 12) });
                p.Add(new[] { V(12, 12), V(6, 5) });
                p.Add(new[] { V(12, 12), V(18, 5) });
                p.Add(new[] { V(6, 9), V(6, 5), V(10, 5) });
                p.Add(new[] { V(18, 9), V(18, 5), V(14, 5) });
                break;
            case Icon.Swap:
                p.Add(new[] { V(4, 8), V(19, 8) });
                p.Add(new[] { V(15.5f, 4.5f), V(19, 8), V(15.5f, 11.5f) });
                p.Add(new[] { V(20, 16), V(5, 16) });
                p.Add(new[] { V(8.5f, 12.5f), V(5, 16), V(8.5f, 19.5f) });
                break;
            case Icon.PlayCircle:
                stroke = 1.9f;
                p.Add(CircleP(12, 12, 9));
                p.Add(new[] { V(10, 8.5f), V(15.5f, 12), V(10, 15.5f), V(10, 8.5f) });
                break;
            case Icon.Clock:
                stroke = 1.9f;
                p.Add(CircleP(12, 12, 9));
                p.Add(new[] { V(12, 7), V(12, 12), V(15.5f, 14) });
                break;
            case Icon.Dot:
                stroke = 8f;
                p.Add(new[] { V(12, 12) });
                break;
        }
        return p;
    }

    private static Vector2[] ArcEllipse(float cx, float cy, float rx, float ry, float fromDeg, float toDeg, int steps = 40)
    {
        var pts = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
            pts[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
        }
        return pts;
    }

    // ---- layout helpers (CSS-like: offsets measured from the parent's edges, y downwards) --

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void TopLeft(RectTransform rt, float left, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, -top);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void TopRight(RectTransform rt, float right, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-right, -top);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void TopCenter(RectTransform rt, float offsetX, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(offsetX, -top);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void BottomLeft(RectTransform rt, float left, float bottom, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(left, bottom);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void BottomRight(RectTransform rt, float right, float bottom, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-right, bottom);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void BottomCenter(RectTransform rt, float offsetX, float bottom, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(offsetX, bottom);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void Center(RectTransform rt, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Stretches to the parent with insets from each edge.</summary>
    public static void Fill(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Full-width band at the top: insets left/right, <paramref name="top"/> down, fixed height.</summary>
    public static void TopBand(RectTransform rt, float left, float top, float right, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }

    public static void BottomBand(RectTransform rt, float left, float bottom, float right, float height)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, bottom + height);
    }

    // ---- primitives ----------------------------------------------------------

    /// <summary>An Image with a rounded sprite (radius 0 = square corners).</summary>
    public static Image Panel(string name, Transform parent, Color color, float radius = 0f, bool raycast = false)
    {
        RectTransform rt = NewRect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        Style(image, color, radius);
        image.raycastTarget = raycast;
        return image;
    }

    public static void Style(Image image, Color color, float radius)
    {
        image.color = color;
        if (radius > 0.01f)
        {
            image.sprite = Rounded(radius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }
        else
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
        }
    }

    /// <summary>Adds a 1-unit rounded border drawn over the element.</summary>
    public static Image Border(RectTransform target, Color color, float radius, float thickness = 1f)
    {
        Image border = Panel("Border", target, color);
        border.sprite = RoundedOutline(radius, thickness);
        border.type = Image.Type.Sliced;
        border.raycastTarget = false;
        Fill(border.rectTransform);
        return border;
    }

    /// <summary>
    /// A floating card: shadow, then the rounded surface, then whatever the caller adds. The
    /// card object itself only receives raycasts (so it blocks clicks to the scene) and draws
    /// nothing, which is what lets the shadow render behind the surface.
    /// </summary>
    public static RectTransform Card(string name, Transform parent, float radius = 14f, Color? fill = null,
        float shadowBlur = 26f, float shadowOffset = 8f, float shadowAlpha = 0.16f, bool border = true)
    {
        RectTransform card = NewRect(name, parent);
        card.gameObject.AddComponent<UIRaycastTarget>();
        if (shadowBlur > 0f) AddShadow(card, radius, shadowBlur, shadowOffset, shadowAlpha);
        Image surface = Panel("Surface", card, fill ?? Glass, radius);
        Fill(surface.rectTransform);
        if (border) Border(card, WithAlpha(Ink, 0.07f), radius);
        return card;
    }

    public static Image AddShadow(RectTransform card, float radius, float blur, float offset, float alpha)
    {
        Image shadow = Panel("Shadow", card, WithAlpha(ShadowInk, alpha));
        shadow.sprite = Shadow(radius, blur);
        shadow.type = Image.Type.Sliced;
        shadow.raycastTarget = false;
        Fill(shadow.rectTransform, -blur, -blur + offset, -blur, -blur - offset);
        shadow.transform.SetAsFirstSibling();
        return shadow;
    }

    public static Text Label(string name, Transform parent, string value, float size, Weight weight, Color color,
        TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
    {
        RectTransform rt = NewRect(name, parent);
        Text text = rt.gameObject.AddComponent<Text>();
        text.font = Font(weight);
        text.fontSize = Mathf.RoundToInt(size);
        text.fontStyle = FontStyle.Normal;
        text.text = value;
        text.color = color;
        text.alignment = anchor;
        text.lineSpacing = 1.05f;
        text.supportRichText = true;
        text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public static Image IconImage(string name, Transform parent, Icon icon, float size, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(size, size);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = IconSprite(icon);
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    public static Image Dot(string name, Transform parent, float size, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(size, size);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = Circle();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Thin 1-unit divider line.</summary>
    public static Image Divider(string name, Transform parent, bool vertical, Color? color = null)
    {
        Image line = Panel(name, parent, color ?? Line);
        line.raycastTarget = false;
        if (vertical)
        {
            LayoutElement le = line.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 1f;
            le.minWidth = 1f;
            le.preferredHeight = 24f;
        }
        return line;
    }

    /// <summary>Tooltip-style dark bubble used by the graphs and the probe.</summary>
    public static Image TooltipSurface(RectTransform rt)
    {
        AddShadow(rt, 10f, 18f, 6f, 0.25f);
        Image bg = Panel("Surface", rt, Ink, 10f);
        Fill(bg.rectTransform);
        return bg;
    }

    // ---- buttons ---------------------------------------------------------------

    public enum ButtonKind { Primary, Dark, Secondary, Ghost, Outline, DangerGhost, DangerSoft, DangerOutline, Nav, Tab, Chip, Segment, White, Link }

    /// <summary>
    /// A themed button: rounded fill, optional leading/trailing icon and a label, laid out by a
    /// horizontal layout group so the caller can ask for its natural width. Hover, press,
    /// active (selected) and disabled states are handled by <see cref="UIButtonSkin"/>.
    /// </summary>
    public static Button MakeButton(string name, Transform parent, string label, ButtonKind kind, float fontSize = 13.5f,
        Icon? icon = null, float radius = 10f, bool trailingIcon = false, float iconSize = 18f, Weight weight = Weight.Bold,
        float padX = 14f)
    {
        RectTransform rt = NewRect(name, parent);
        Image bg = rt.gameObject.AddComponent<Image>();
        Style(bg, Color.white, radius);
        Button button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = bg;
        // AddComponent already ran the default colour-tint transition (disabled grey, if the
        // button was created under a hidden CanvasGroup); clear that leftover tint — the skin
        // drives all state colours from here on.
        bg.canvasRenderer.SetColor(Color.white);
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        HorizontalLayoutGroup layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = string.IsNullOrEmpty(label) ? 0f : 7f;
        layout.padding = new RectOffset(Mathf.RoundToInt(padX), Mathf.RoundToInt(padX), 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var content = new List<Graphic>();
        Image iconImage = null;
        if (icon.HasValue && !trailingIcon) iconImage = AddButtonIcon(rt, icon.Value, iconSize, content);
        Text text = null;
        if (!string.IsNullOrEmpty(label))
        {
            text = Label("Label", rt, label, fontSize, weight, Ink2, TextAnchor.MiddleCenter);
            text.rectTransform.sizeDelta = new Vector2(0f, fontSize * 1.6f);
            content.Add(text);
        }
        if (icon.HasValue && trailingIcon) iconImage = AddButtonIcon(rt, icon.Value, iconSize, content);

        UIButtonSkin skin = rt.gameObject.AddComponent<UIButtonSkin>();
        skin.Init(button, bg, content, kind, radius);
        return button;
    }

    private static Image AddButtonIcon(RectTransform parent, Icon icon, float size, List<Graphic> content)
    {
        Image image = IconImage("Icon", parent, icon, size, Ink2);
        LayoutElement le = image.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.minWidth = size;
        le.preferredHeight = size;
        content.Add(image);
        return image;
    }

    /// <summary>The button's natural width (padding + icon + label).</summary>
    /// <remarks>Measured directly rather than through LayoutUtility, which reports 0 for an
    /// inactive hierarchy — the separate analytics window is built while hidden.</remarks>
    public static float PreferredWidth(Button button)
    {
        RectTransform rt = (RectTransform)button.transform;
        HorizontalLayoutGroup layout = rt.GetComponent<HorizontalLayoutGroup>();
        float width = layout != null ? layout.padding.left + layout.padding.right : 0f;
        int count = 0;
        for (int i = 0; i < rt.childCount; i++)
        {
            Transform child = rt.GetChild(i);
            if (!child.gameObject.activeSelf) continue;
            LayoutElement le = child.GetComponent<LayoutElement>();
            if (le != null && le.ignoreLayout) continue;
            float w;
            if (child.TryGetComponent(out Text text)) w = text.preferredWidth;
            else if (le != null && le.preferredWidth > 0f) w = le.preferredWidth;
            else w = ((RectTransform)child).sizeDelta.x;
            width += w;
            count++;
        }
        if (layout != null && count > 1) width += layout.spacing * (count - 1);
        return Mathf.Ceil(width);
    }

    /// <summary>Round icon-only button (close, help).</summary>
    public static Button IconButton(string name, Transform parent, Icon icon, ButtonKind kind, float size = 36f, float iconSize = 16f)
    {
        Button b = MakeButton(name, parent, null, kind, 13f, icon, size * 0.5f, false, iconSize, Weight.Bold, 0f);
        ((RectTransform)b.transform).sizeDelta = new Vector2(size, size);
        return b;
    }

    public static UIButtonSkin Skin(this Button button) => button != null ? button.GetComponent<UIButtonSkin>() : null;

    public static void SetLabel(Button button, string label)
    {
        if (button == null) return;
        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null) text.text = label;
    }

    public static void SetIcon(Button button, Icon icon)
    {
        if (button == null) return;
        Transform t = button.transform.Find("Icon");
        if (t != null && t.TryGetComponent(out Image image)) image.sprite = IconSprite(icon);
    }

    // ---- composite widgets -----------------------------------------------------

    /// <summary>Capsule status indicator: coloured dot + label on a soft tint.</summary>
    public static UIStatusChip StatusChip(string name, Transform parent, float height = 26f, float fontSize = 12f)
    {
        RectTransform rt = NewRect(name, parent);
        Image bg = rt.gameObject.AddComponent<Image>();
        Style(bg, SuccessSoft, height * 0.5f);
        bg.raycastTarget = false;
        HorizontalLayoutGroup layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 6f;
        layout.padding = new RectOffset(Mathf.RoundToInt(height * 0.4f), Mathf.RoundToInt(height * 0.45f), 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        ContentSizeFitter fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rt.sizeDelta = new Vector2(100f, height);

        Image dot = Dot("Dot", rt, Mathf.Round(height * 0.26f), Success);
        LayoutElement le = dot.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = dot.rectTransform.sizeDelta.x;
        Text text = Label("Label", rt, "", fontSize, Weight.Bold, SuccessInk, TextAnchor.MiddleLeft);
        text.rectTransform.sizeDelta = new Vector2(0f, height);

        UIStatusChip chip = rt.gameObject.AddComponent<UIStatusChip>();
        chip.Background = bg;
        chip.DotImage = dot;
        chip.Text = text;
        return chip;
    }

    /// <summary>A number with a smaller, lighter unit beside it ("358 kg/h").</summary>
    public static UIValueText ValueText(string name, Transform parent, float valueSize, float unitSize, Color valueColor, Color unitColor,
        TextAnchor alignment = TextAnchor.LowerLeft, Weight valueWeight = Weight.ExtraBold)
    {
        RectTransform rt = NewRect(name, parent);
        HorizontalLayoutGroup layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = alignment;
        layout.spacing = Mathf.Round(unitSize * 0.3f);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        Text value = Label("Value", rt, "—", valueSize, valueWeight, valueColor, TextAnchor.LowerLeft);
        Text unit = Label("Unit", rt, "", unitSize, Weight.SemiBold, unitColor, TextAnchor.LowerLeft);
        // Nudge the smaller unit down onto the value's baseline.
        LayoutElement le = unit.gameObject.AddComponent<LayoutElement>();
        le.minHeight = unitSize * 1.2f;
        UIValueText vt = rt.gameObject.AddComponent<UIValueText>();
        vt.Value = value;
        vt.Unit = unit;
        return vt;
    }

    /// <summary>Rounded track with a fill bar; <see cref="UIProgressBar.Set"/> takes 0..1.</summary>
    public static UIProgressBar ProgressBar(string name, Transform parent, Color fill, float height = 6f, Color? track = null)
    {
        Image trackImage = Panel(name, parent, track ?? Line, height * 0.5f);
        Image fillImage = Panel("Fill", trackImage.transform, fill, height * 0.5f);
        RectTransform fr = fillImage.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = new Vector2(0f, 1f);
        fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = Vector2.zero;
        fr.offsetMax = Vector2.zero;
        UIProgressBar bar = trackImage.gameObject.AddComponent<UIProgressBar>();
        bar.FillImage = fillImage;
        return bar;
    }

    /// <summary>An icon on a pale rounded square in the given colour (module badges).</summary>
    public static Image Badge(string name, Transform parent, Icon icon, Color color, float size = 34f, float radius = 10f, float iconSize = 20f)
    {
        Image bg = Panel(name, parent, Tint(color, 0.16f), radius);
        bg.rectTransform.sizeDelta = new Vector2(size, size);
        Image i = IconImage("Icon", bg.transform, icon, iconSize, Color.Lerp(color, Ink, 0.12f));
        Center(i.rectTransform, iconSize, iconSize);
        return bg;
    }

    /// <summary>
    /// Themed horizontal slider: rounded track, accent fill and a white knob. The GameObject
    /// is named <paramref name="name"/> so existing lookups by name keep working.
    /// </summary>
    public static Slider MakeSlider(string name, Transform parent, float min, float max, float value, Color? accent = null)
    {
        RectTransform rt = NewRect(name, parent);
        Slider slider = rt.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        Navigation nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;

        Image track = Panel("Background", rt, Line, 3f, true);
        track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(0f, 6f);

        RectTransform fillArea = NewRect("Fill Area", rt);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 6f);
        Image fill = Panel("Fill", fillArea, accent ?? Accent, 3f);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        RectTransform handleArea = NewRect("Handle Slide Area", rt);
        Fill(handleArea, 9f, 0f, 9f, 0f);
        Image handle = Panel("Handle", handleArea, Color.white, 0f, true);
        handle.sprite = Knob();
        handle.type = Image.Type.Simple;
        handle.rectTransform.sizeDelta = new Vector2(22f, 22f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        handle.canvasRenderer.SetColor(Color.white);
        track.canvasRenderer.SetColor(Color.white);
        return slider;
    }

    /// <summary>Full-screen click-blocking scrim for modals.</summary>
    public static Image ScrimLayer(string name, Transform parent, Color? color = null)
    {
        Image scrim = Panel(name, parent, color ?? Scrim, 0f, true);
        Fill(scrim.rectTransform);
        return scrim;
    }
}

/// <summary>
/// Makes a top-anchored card minimisable: a chevron button in its header folds the card down
/// to just the header (everything else is hidden) and back. The card keeps its top edge.
/// </summary>
public sealed class UICollapsible : MonoBehaviour
{
    private RectTransform card;
    private float expandedHeight;
    private float collapsedHeight;
    private readonly HashSet<Transform> keep = new HashSet<Transform>();
    private RectTransform chevron;
    private bool collapsed;
    private float currentHeight;

    public bool Collapsed => collapsed;

    /// <summary>Adds the toggle to <paramref name="target"/>. <paramref name="headerElements"/>
    /// stay visible when folded (the card's shadow, surface and border always do).</summary>
    public static UICollapsible Attach(RectTransform target, float expanded, float collapsedTo, float right, float top,
        params Transform[] headerElements)
    {
        UICollapsible c = target.gameObject.AddComponent<UICollapsible>();
        c.card = target;
        c.expandedHeight = expanded;
        c.collapsedHeight = collapsedTo;
        c.currentHeight = expanded;
        foreach (Transform t in headerElements) if (t != null) c.keep.Add(t);
        foreach (string n in new[] { "Shadow", "Surface", "Border" })
        {
            Transform t = target.Find(n);
            if (t != null) c.keep.Add(t);
        }

        Button toggle = UITheme.IconButton("Minimise", target, UITheme.Icon.ChevronDown, UITheme.ButtonKind.Ghost, 30f, 16f);
        UITheme.TopRight((RectTransform)toggle.transform, right, top, 30f, 30f);
        c.keep.Add(toggle.transform);
        c.chevron = (RectTransform)toggle.transform.Find("Icon");
        c.chevron.localEulerAngles = new Vector3(0f, 0f, 180f); // pointing up = "fold away"
        toggle.onClick.AddListener(c.Toggle);
        return c;
    }

    public void Toggle() => SetCollapsed(!collapsed);

    public void SetCollapsed(bool value)
    {
        collapsed = value;
        for (int i = 0; i < card.childCount; i++)
        {
            Transform child = card.GetChild(i);
            if (!keep.Contains(child)) child.gameObject.SetActive(!collapsed);
        }
        if (chevron != null) chevron.localEulerAngles = new Vector3(0f, 0f, collapsed ? 0f : 180f);
    }

    private void Update()
    {
        float target = collapsed ? collapsedHeight : expandedHeight;
        if (Mathf.Abs(currentHeight - target) < 0.5f)
        {
            if (currentHeight == target) return;
            currentHeight = target;
        }
        else
        {
            currentHeight = Mathf.Lerp(currentHeight, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }
        card.sizeDelta = new Vector2(card.sizeDelta.x, currentHeight);
    }
}

/// <summary>Raycast-only graphic: blocks clicks without drawing anything.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIRaycastTarget : Graphic
{
    protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
}

/// <summary>Colour states for a themed button; eases between them.</summary>
public sealed class UIButtonSkin : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private struct Palette
    {
        public Color Fill, Ink, HoverFill, HoverInk, PressFill, ActiveFill, ActiveInk, ActiveHoverFill, BorderColor;
    }

    private Button button;
    private Image background;
    private Image border;
    private readonly List<Graphic> content = new List<Graphic>();
    private Palette palette;
    private UITheme.ButtonKind kind;
    private float radius;
    private bool hovered, pressed, active;
    private bool initialised;
    private Color currentFill, currentInk, targetFill, targetInk;

    public bool Active => active;
    public UITheme.ButtonKind Kind => kind;

    public void Init(Button b, Image bg, List<Graphic> graphics, UITheme.ButtonKind k, float cornerRadius)
    {
        button = b;
        background = bg;
        radius = cornerRadius;
        content.Clear();
        content.AddRange(graphics);
        SetKind(k);
        initialised = true;
        Snap();
    }

    public void SetKind(UITheme.ButtonKind k)
    {
        kind = k;
        palette = PaletteFor(k);
        if (palette.BorderColor.a > 0f)
        {
            if (border == null) border = UITheme.Border((RectTransform)transform, palette.BorderColor, radius);
            border.color = palette.BorderColor;
            border.gameObject.SetActive(true);
            border.GetComponent<LayoutElement>();
            if (border.GetComponent<LayoutElement>() == null) border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }
        else if (border != null) border.gameObject.SetActive(false);
        if (initialised) Snap();
    }

    public void SetActive(bool value)
    {
        if (active == value) return;
        active = value;
    }

    /// <summary>Uses a specific colour for the selected state (e.g. a variable's own colour).</summary>
    public void OverrideActive(Color fill, Color ink)
    {
        palette.ActiveFill = fill;
        palette.ActiveHoverFill = Color.Lerp(fill, Color.black, 0.1f);
        palette.ActiveInk = ink;
    }

    private static Palette PaletteFor(UITheme.ButtonKind k)
    {
        Color clear = new Color(1f, 1f, 1f, 0f);
        switch (k)
        {
            case UITheme.ButtonKind.Primary:
                return new Palette { Fill = UITheme.Accent, Ink = Color.white, HoverFill = UITheme.AccentHover, HoverInk = Color.white, PressFill = UITheme.AccentPressed, ActiveFill = UITheme.AccentHover, ActiveInk = Color.white, ActiveHoverFill = UITheme.AccentPressed };
            case UITheme.ButtonKind.Dark:
                return new Palette { Fill = UITheme.Ink, Ink = Color.white, HoverFill = UITheme.Hex("1E293B"), HoverInk = Color.white, PressFill = UITheme.Hex("020617"), ActiveFill = UITheme.Ink, ActiveInk = Color.white, ActiveHoverFill = UITheme.Hex("1E293B") };
            case UITheme.ButtonKind.Secondary:
                return new Palette { Fill = UITheme.Sunken, Ink = UITheme.Ink2, HoverFill = UITheme.Line, HoverInk = UITheme.Ink, PressFill = UITheme.LineStrong, ActiveFill = UITheme.AccentSoft2, ActiveInk = UITheme.Accent, ActiveHoverFill = UITheme.AccentSoft2 };
            case UITheme.ButtonKind.Outline:
                return new Palette { Fill = Color.white, Ink = UITheme.Ink2, HoverFill = UITheme.Sunken2, HoverInk = UITheme.Ink, PressFill = UITheme.Sunken, ActiveFill = UITheme.AccentSoft, ActiveInk = UITheme.Accent, ActiveHoverFill = UITheme.AccentSoft2, BorderColor = UITheme.LineStrong };
            case UITheme.ButtonKind.DangerGhost:
                return new Palette { Fill = clear, Ink = UITheme.DangerInk, HoverFill = UITheme.DangerSoft, HoverInk = UITheme.DangerInk, PressFill = UITheme.DangerSoft2, ActiveFill = UITheme.DangerSoft, ActiveInk = UITheme.DangerInk, ActiveHoverFill = UITheme.DangerSoft2 };
            case UITheme.ButtonKind.DangerSoft:
                return new Palette { Fill = UITheme.DangerSoft, Ink = UITheme.DangerInk, HoverFill = UITheme.DangerSoft2, HoverInk = UITheme.DangerInk, PressFill = UITheme.Hex("FECACA"), ActiveFill = UITheme.DangerSoft2, ActiveInk = UITheme.DangerInk, ActiveHoverFill = UITheme.DangerSoft2 };
            case UITheme.ButtonKind.DangerOutline:
                return new Palette { Fill = Color.white, Ink = UITheme.DangerInk, HoverFill = UITheme.DangerSoft, HoverInk = UITheme.DangerInk, PressFill = UITheme.DangerSoft2, ActiveFill = UITheme.DangerSoft, ActiveInk = UITheme.DangerInk, ActiveHoverFill = UITheme.DangerSoft2, BorderColor = UITheme.Hex("FECACA") };
            case UITheme.ButtonKind.Nav:
                return new Palette { Fill = clear, Ink = UITheme.Ink2, HoverFill = UITheme.Sunken, HoverInk = UITheme.Ink, PressFill = UITheme.Line, ActiveFill = UITheme.Accent, ActiveInk = Color.white, ActiveHoverFill = UITheme.AccentHover };
            case UITheme.ButtonKind.Tab:
                return new Palette { Fill = clear, Ink = UITheme.Muted, HoverFill = UITheme.Sunken, HoverInk = UITheme.Ink, PressFill = UITheme.Line, ActiveFill = UITheme.AccentSoft, ActiveInk = UITheme.Accent, ActiveHoverFill = UITheme.AccentSoft2 };
            case UITheme.ButtonKind.Chip:
                return new Palette { Fill = UITheme.Sunken, Ink = UITheme.Ink2, HoverFill = UITheme.Line, HoverInk = UITheme.Ink, PressFill = UITheme.LineStrong, ActiveFill = UITheme.Ink, ActiveInk = Color.white, ActiveHoverFill = UITheme.Hex("1E293B") };
            case UITheme.ButtonKind.Segment:
                return new Palette { Fill = clear, Ink = UITheme.Muted, HoverFill = UITheme.WithAlpha(Color.white, 0.6f), HoverInk = UITheme.Ink, PressFill = UITheme.WithAlpha(Color.white, 0.8f), ActiveFill = Color.white, ActiveInk = UITheme.Ink, ActiveHoverFill = Color.white };
            case UITheme.ButtonKind.Link:
                return new Palette { Fill = clear, Ink = UITheme.Accent, HoverFill = UITheme.AccentSoft, HoverInk = UITheme.AccentHover, PressFill = UITheme.AccentSoft2, ActiveFill = UITheme.AccentSoft, ActiveInk = UITheme.Accent, ActiveHoverFill = UITheme.AccentSoft2 };
            case UITheme.ButtonKind.White:
                return new Palette { Fill = UITheme.Glass, Ink = UITheme.Ink2, HoverFill = UITheme.Sunken2, HoverInk = UITheme.Ink, PressFill = UITheme.Sunken, ActiveFill = UITheme.AccentSoft, ActiveInk = UITheme.Accent, ActiveHoverFill = UITheme.AccentSoft2 };
            default: // Ghost
                return new Palette { Fill = clear, Ink = UITheme.Ink2, HoverFill = UITheme.Sunken, HoverInk = UITheme.Ink, PressFill = UITheme.Line, ActiveFill = UITheme.AccentSoft, ActiveInk = UITheme.Accent, ActiveHoverFill = UITheme.AccentSoft2 };
        }
    }

    private void ComputeTarget()
    {
        bool interactable = button == null || button.IsInteractable();
        if (active)
        {
            targetFill = hovered && interactable ? palette.ActiveHoverFill : palette.ActiveFill;
            targetInk = palette.ActiveInk;
        }
        else if (pressed && interactable)
        {
            targetFill = palette.PressFill;
            targetInk = palette.HoverInk;
        }
        else if (hovered && interactable)
        {
            targetFill = palette.HoverFill;
            targetInk = palette.HoverInk;
        }
        else
        {
            targetFill = palette.Fill;
            targetInk = palette.Ink;
        }
        if (!interactable)
        {
            targetFill.a *= 0.5f;
            targetInk.a *= 0.45f;
        }
    }

    private void Snap()
    {
        ComputeTarget();
        currentFill = targetFill;
        currentInk = targetInk;
        Apply();
    }

    private void Apply()
    {
        if (background != null) background.color = currentFill;
        foreach (Graphic g in content) if (g != null) g.color = currentInk;
    }

    private void OnEnable()
    {
        hovered = false;
        pressed = false;
        if (initialised) Snap();
    }

    private void Update()
    {
        if (!initialised) return;
        ComputeTarget();
        if (currentFill == targetFill && currentInk == targetInk) return;
        float k = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
        currentFill = Color.Lerp(currentFill, targetFill, k);
        currentInk = Color.Lerp(currentInk, targetInk, k);
        if (Mathf.Abs(currentFill.r - targetFill.r) + Mathf.Abs(currentFill.g - targetFill.g) + Mathf.Abs(currentFill.b - targetFill.b) + Mathf.Abs(currentFill.a - targetFill.a) < 0.004f) currentFill = targetFill;
        if (Mathf.Abs(currentInk.r - targetInk.r) + Mathf.Abs(currentInk.g - targetInk.g) + Mathf.Abs(currentInk.b - targetInk.b) + Mathf.Abs(currentInk.a - targetInk.a) < 0.004f) currentInk = targetInk;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
}

public sealed class UIStatusChip : MonoBehaviour
{
    public enum Kind { Success, Warning, Danger, Neutral, Info }

    public Image Background;
    public Image DotImage;
    public Text Text;
    private Kind current = (Kind)(-1);
    private string currentText;

    public void Set(Kind kind, string label)
    {
        if (kind == current && label == currentText) return;
        current = kind;
        currentText = label;
        Color soft, dot, ink;
        switch (kind)
        {
            case Kind.Warning: soft = UITheme.WarningSoft; dot = UITheme.Warning; ink = UITheme.WarningInk; break;
            case Kind.Danger: soft = UITheme.DangerSoft2; dot = UITheme.Danger; ink = UITheme.DangerInk; break;
            case Kind.Neutral: soft = UITheme.Sunken; dot = UITheme.Subtle; ink = UITheme.Ink2; break;
            case Kind.Info: soft = UITheme.AccentSoft; dot = UITheme.Accent; ink = UITheme.AccentInk; break;
            default: soft = UITheme.SuccessSoft; dot = UITheme.Success; ink = UITheme.SuccessInk; break;
        }
        if (Background != null) Background.color = soft;
        if (DotImage != null) DotImage.color = dot;
        if (Text != null)
        {
            Text.color = ink;
            Text.text = label;
        }
    }
}

public sealed class UIValueText : MonoBehaviour
{
    public Text Value;
    public Text Unit;

    public void Set(string value, string unit)
    {
        if (Value != null && Value.text != value) Value.text = value;
        if (Unit != null)
        {
            string u = unit ?? "";
            if (Unit.text != u) Unit.text = u;
            if (Unit.gameObject.activeSelf != (u.Length > 0)) Unit.gameObject.SetActive(u.Length > 0);
        }
    }
}

public sealed class UIProgressBar : MonoBehaviour
{
    public Image FillImage;
    private float value = -1f;

    public void Set(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        if (Mathf.Abs(fraction - value) < 0.0005f || FillImage == null) return;
        value = fraction;
        RectTransform r = FillImage.rectTransform;
        r.anchorMax = new Vector2(fraction, 1f);
        r.offsetMax = Vector2.zero;
        // A sliver narrower than the rounded caps would render as a blob; hide it instead.
        FillImage.enabled = fraction > 0.004f;
    }

    public void SetColor(Color c)
    {
        if (FillImage != null) FillImage.color = c;
    }
}
