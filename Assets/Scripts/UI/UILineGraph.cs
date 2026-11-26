using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a polyline through normalised points (0..1 on both axes) with round-ish dots, inside its rect.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UILineGraph : MaskableGraphic
{
    [SerializeField] List<float> values = new List<float>();
    public float thickness = 4f, dot = 12f;

    public void SetValues(List<float> v)
    {
        values = v ?? new List<float>();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int n = values.Count;
        if (n == 0) return;
        var r = rectTransform.rect;
        Vector2 P(int i) => new Vector2(r.xMin + (n == 1 ? 0.5f : (float)i / (n - 1)) * r.width, r.yMin + Mathf.Clamp01(values[i]) * r.height);
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 a = P(i), b = P(i + 1);
            Vector2 dir = (b - a).normalized;
            Vector2 nrm = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
            Quad(vh, a - nrm, a + nrm, b + nrm, b - nrm);
        }
        for (int i = 0; i < n; i++)
        {
            Vector2 c = P(i);
            float h = dot * 0.5f;
            // Octagon dot
            int start = vh.currentVertCount;
            vh.AddVert(c, color, Vector2.zero);
            for (int k = 0; k < 8; k++)
            {
                float ang = k * Mathf.PI / 4f;
                vh.AddVert(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * h, color, Vector2.zero);
            }
            for (int k = 0; k < 8; k++) vh.AddTriangle(start, start + 1 + k, start + 1 + (k + 1) % 8);
        }
    }

    void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int s = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(s, s + 1, s + 2);
        vh.AddTriangle(s, s + 2, s + 3);
    }
}
