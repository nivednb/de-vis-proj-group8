using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared mesh helpers for the analytics graphs (CorrelationGraphRuntime, LiveGraphRuntime).
///
/// The graphs used to draw each line as a row of individually rotated Image rects and each
/// shaded area as dozens of thin vertical Image bars, all destroyed and recreated on every
/// rebuild. That churned hundreds of GameObjects per sample tick / per scrollbar drag frame
/// (visible as jittery scrolling) and left visible gaps and overlap at the segment joints
/// (visible as jagged curves).
///
/// <see cref="UIGraphLine"/> and <see cref="UIGraphFill"/> replace both with a single
/// persistent CanvasRenderer each: one mesh, one draw call, rebuilt only when the data
/// actually changes, with rounded joints so the stroke reads as one smooth line.
/// </summary>
public static class GraphCurve
{
    /// <summary>Catmull-Rom spline through <paramref name="control"/>, <paramref name="subdivisions"/>
    /// samples per span, written into <paramref name="output"/> (cleared first).</summary>
    public static void CatmullRom(IReadOnlyList<Vector2> control, int subdivisions, List<Vector2> output)
    {
        output.Clear();
        if (control == null || control.Count == 0) return;
        if (control.Count < 3)
        {
            for (int i = 0; i < control.Count; i++) output.Add(control[i]);
            return;
        }

        subdivisions = Mathf.Max(1, subdivisions);
        output.Add(control[0]);
        for (int i = 0; i < control.Count - 1; i++)
        {
            Vector2 p0 = control[Mathf.Max(i - 1, 0)];
            Vector2 p1 = control[i];
            Vector2 p2 = control[i + 1];
            Vector2 p3 = control[Mathf.Min(i + 2, control.Count - 1)];
            for (int s = 1; s <= subdivisions; s++)
            {
                float t = s / (float)subdivisions;
                float t2 = t * t;
                float t3 = t2 * t;
                output.Add(0.5f * (2f * p1
                    + (-p0 + p2) * t
                    + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                    + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
    }
}

/// <summary>Single-draw-call polyline with rounded joints and end caps.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIGraphLine : MaskableGraphic
{
    [SerializeField] private float thickness = 2.4f;
    private readonly List<Vector2> points = new List<Vector2>();

    public float Thickness
    {
        get => thickness;
        set { thickness = value; SetVerticesDirty(); }
    }

    public void SetPoints(List<Vector2> pts)
    {
        points.Clear();
        if (pts != null) points.AddRange(pts);
        SetVerticesDirty();
    }

    public void ClearPoints()
    {
        if (points.Count == 0) return;
        points.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points.Count < 2) return;

        float half = Mathf.Max(0.4f, thickness * 0.5f);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 1e-8f) continue;
            dir.Normalize();
            Vector2 n = new Vector2(-dir.y, dir.x) * half;

            int idx = vh.currentVertCount;
            AddVert(vh, a - n);
            AddVert(vh, a + n);
            AddVert(vh, b + n);
            AddVert(vh, b - n);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx, idx + 2, idx + 3);
        }

        // Fill the wedge gaps only where the line actually bends, plus both end caps —
        // keeps the disc count negligible on the long smooth stretches.
        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector2 d0 = points[i] - points[i - 1];
            Vector2 d1 = points[i + 1] - points[i];
            if (d0.sqrMagnitude < 1e-8f || d1.sqrMagnitude < 1e-8f) continue;
            if (Vector2.Dot(d0.normalized, d1.normalized) < 0.9997f) AddDisc(vh, points[i], half);
        }
        AddDisc(vh, points[0], half);
        AddDisc(vh, points[points.Count - 1], half);
    }

    private void AddDisc(VertexHelper vh, Vector2 center, float radius)
    {
        const int segments = 10;
        int centerIdx = vh.currentVertCount;
        AddVert(vh, center);
        for (int s = 0; s <= segments; s++)
        {
            float ang = s / (float)segments * Mathf.PI * 2f;
            AddVert(vh, center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius);
            if (s > 0) vh.AddTriangle(centerIdx, centerIdx + s, centerIdx + s + 1);
        }
    }

    private void AddVert(VertexHelper vh, Vector2 pos)
    {
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = pos;
        vh.AddVert(v);
    }
}

/// <summary>Single-draw-call filled area between a curve and a horizontal baseline.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIGraphFill : MaskableGraphic
{
    private readonly List<Vector2> top = new List<Vector2>();
    private float baselineY;

    public void SetCurve(List<Vector2> topPoints, float baseline)
    {
        top.Clear();
        if (topPoints != null) top.AddRange(topPoints);
        baselineY = baseline;
        SetVerticesDirty();
    }

    public void ClearCurve()
    {
        if (top.Count == 0) return;
        top.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (top.Count < 2) return;

        for (int i = 0; i < top.Count - 1; i++)
        {
            Vector2 t0 = top[i];
            Vector2 t1 = top[i + 1];
            int idx = vh.currentVertCount;
            AddVert(vh, new Vector2(t0.x, baselineY));
            AddVert(vh, t0);
            AddVert(vh, t1);
            AddVert(vh, new Vector2(t1.x, baselineY));
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx, idx + 2, idx + 3);
        }
    }

    private void AddVert(VertexHelper vh, Vector2 pos)
    {
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = pos;
        vh.AddVert(v);
    }
}
