using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Soft depth for UI panels and buttons (3D soft realism look):
///   - a smooth vertical gradient on the element itself (lighter top, darker bottom), and
///   - optionally a soft drop shadow: a few enlarged, offset copies of the shape drawn behind it, fading outwards.
/// It only reshapes the element's own mesh (no extra draw calls, textures or layout changes), so rounded corners,
/// outlines and layouts stay exactly as they are. Shadows draw outside the rect and never affect layout.
/// Added by UIKit (PanelImage / Button / HudButton); can also be added to any Graphic by hand.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UISoftDepth : BaseMeshEffect
{
    [Tooltip("Colour multiplier at the top edge (above 1 = lighter).")]
    public float topTint = 1.07f;
    [Tooltip("Colour multiplier at the bottom edge (below 1 = darker).")]
    public float bottomTint = 0.93f;

    [Header("Soft shadow")]
    [Range(0, 6)] public int shadowLayers = 0;
    public Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
    [Tooltip("How far the outermost layer grows beyond the shape, in pixels.")]
    public float shadowSpread = 12f;
    public Vector2 shadowOffset = new Vector2(0f, -8f);

    [Header("Raised lip (buttons)")]
    [Tooltip("A crisp darker copy just below the shape, like the side of a raised key. 0 = none.")]
    public float lipDepth = 0f;
    public Color lipColor = new Color(0f, 0f, 0f, 0.45f);

    readonly List<UIVertex> verts = new List<UIVertex>();
    readonly List<UIVertex> output = new List<UIVertex>();

    public void Configure(float top, float bottom, int layers = 0, float spread = 12f, float offsetY = -8f, float alpha = 0.35f, float lip = 0f)
    {
        topTint = top; bottomTint = bottom;
        shadowLayers = layers; shadowSpread = spread; shadowOffset = new Vector2(0f, offsetY);
        shadowColor = new Color(0f, 0f, 0f, alpha);
        lipDepth = lip;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        verts.Clear();
        vh.GetUIVertexStream(verts);

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var v in verts)
        {
            if (v.position.x < minX) minX = v.position.x;
            if (v.position.x > maxX) maxX = v.position.x;
            if (v.position.y < minY) minY = v.position.y;
            if (v.position.y > maxY) maxY = v.position.y;
        }
        float h = Mathf.Max(1f, maxY - minY);

        // 1) Gradient on the element itself.
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            float t = (v.position.y - minY) / h;
            float k = Mathf.Lerp(bottomTint, topTint, t);
            Color c = v.color;
            v.color = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
            verts[i] = v;
        }

        if (shadowLayers <= 0 && lipDepth <= 0f)
        {
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
            return;
        }

        // 2) Shadow layers (outermost first, so the darker inner layers draw over them), then the lip, then the shape.
        output.Clear();
        Vector2 centre = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        float halfW = Mathf.Max(1f, (maxX - minX) * 0.5f), halfH = Mathf.Max(1f, h * 0.5f);
        for (int layer = shadowLayers; layer >= 1; layer--)
        {
            float k = layer / (float)shadowLayers;              // 1 = outermost
            float grow = shadowSpread * k;
            Vector2 offset = shadowOffset * (0.5f + 0.5f * k);
            float alpha = shadowColor.a * (1.15f - k) / shadowLayers * 2f;
            AppendCopy(centre, 1f + grow / halfW, 1f + grow / halfH, offset, shadowColor, alpha);
        }
        if (lipDepth > 0f) AppendCopy(centre, 1f, 1f, new Vector2(0f, -lipDepth), lipColor, lipColor.a);
        output.AddRange(verts);

        vh.Clear();
        vh.AddUIVertexTriangleStream(output);
    }

    void AppendCopy(Vector2 centre, float scaleX, float scaleY, Vector2 offset, Color color, float alpha)
    {
        foreach (var src in verts)
        {
            var v = src;
            var p = v.position;
            p.x = centre.x + (p.x - centre.x) * scaleX + offset.x;
            p.y = centre.y + (p.y - centre.y) * scaleY + offset.y;
            v.position = p;
            float a = Mathf.Clamp01(alpha) * (src.color.a / 255f);   // fades with the element's own alpha
            v.color = new Color(color.r, color.g, color.b, a);
            output.Add(v);
        }
    }
}
