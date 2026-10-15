using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rank insignia, drawn once in code (no texture assets): a shield with a symbol that grows with the rank -
/// bar, one to three chevrons, chevrons with a crown bar, a forensics diamond, diamond and chevron, a star,
/// and a star in a ring for the top rank. White symbol on a dark shield, so an Image tinted with the rank
/// colour gives a coloured border and symbol on a dark face.
/// </summary>
public static class RankBadge
{
    const int Size = 128;
    static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
    static Sprite glow;

    public static Sprite For(int rankIndex)
    {
        rankIndex = Mathf.Max(0, rankIndex);
        if (cache.TryGetValue(rankIndex, out var s) && s != null) return s;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "RankBadge_" + rankIndex };
        var px = new Color32[Size * Size];
        Color face = new Color(0.07f, 0.10f, 0.17f, 1f);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // 3x3 supersampling for smooth edges
                float shield = 0f, bright = 0f;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        float u = ((x + (sx + 0.5f) / 3f) / Size) * 2f - 1f;
                        float v = ((y + (sy + 0.5f) / 3f) / Size) * 2f - 1f;
                        if (!InShield(u, v, 1f)) continue;
                        shield += 1f;
                        if (!InShield(u, v, 0.86f) || Symbol(rankIndex, u, v)) bright += 1f;
                    }
                shield /= 9f; bright /= 9f;
                if (shield <= 0f) { px[y * Size + x] = new Color32(255, 255, 255, 0); continue; }
                float b = shield > 0f ? bright / shield : 0f;
                Color c = Color.Lerp(face, Color.white, b);
                c.a = shield;
                px[y * Size + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        cache[rankIndex] = s;
        return s;
    }

    /// <summary>Soft radial glow for the promotion card.</summary>
    public static Sprite Glow
    {
        get
        {
            if (glow != null) return glow;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a * a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return glow;
        }
    }

    // Shield: flat top, straight sides, curved bottom to a point. scale < 1 gives the inner edge.
    static bool InShield(float u, float v, float scale)
    {
        u /= scale; v = (v + 0.05f) / scale - 0.05f;
        if (v > 0.86f || v < -0.96f) return false;
        float half = 0.78f;
        if (v >= -0.1f) return Mathf.Abs(u) <= half;
        float t = (v + 0.1f) / 0.86f;   // 0 at the shoulder, -1 at the point
        return Mathf.Abs(u) <= half * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
    }

    static bool Symbol(int rank, float u, float v)
    {
        switch (rank)
        {
            case 0: return Bar(u, v, 0.0f, 0.36f);
            case 1: return Chevron(u, v, 0.0f);
            case 2: return Chevron(u, v, 0.14f) || Chevron(u, v, -0.14f);
            case 3: return Chevron(u, v, 0.26f) || Chevron(u, v, 0.0f) || Chevron(u, v, -0.26f);
            case 4: return Bar(u, v, 0.5f, 0.42f) || Chevron(u, v, 0.18f) || Chevron(u, v, -0.06f) || Chevron(u, v, -0.30f);
            case 5: return Diamond(u, v, 0.02f, false);
            case 6: return Diamond(u, v, 0.16f, false) || Chevron(u, v, -0.38f);
            case 7: return Star(u, v, 0.04f, 0.46f);
            default: return Star(u, v, 0.04f, 0.36f) || Ring(u, v, 0.04f, 0.5f, 0.58f);
        }
    }

    static bool Bar(float u, float v, float c, float halfWidth) => Mathf.Abs(v - c) < 0.06f && Mathf.Abs(u) < halfWidth;

    static bool Chevron(float u, float v, float c)
    {
        float au = Mathf.Abs(u);
        if (au > 0.46f) return false;
        float line = c + 0.17f - 0.62f * au;
        return Mathf.Abs(v - line) < 0.075f;
    }

    static bool Diamond(float u, float v, float c, bool filled)
    {
        float d = Mathf.Abs(u) / 0.36f + Mathf.Abs(v - c) / 0.44f;
        return filled ? d <= 1f : d <= 1f && d >= 0.62f || (Mathf.Abs(u) < 0.05f && Mathf.Abs(v - c) < 0.12f);
    }

    static bool Ring(float u, float v, float c, float r0, float r1)
    {
        float r = Mathf.Sqrt(u * u + (v - c) * (v - c));
        return r >= r0 && r <= r1;
    }

    static bool Star(float u, float v, float c, float outer)
    {
        float inner = outer * 0.42f;
        // 10-vertex polygon, point up
        bool inside = false;
        Vector2 p = new Vector2(u, v - c);
        Vector2 prev = StarVertex(9, outer, inner);
        for (int i = 0; i < 10; i++)
        {
            Vector2 cur = StarVertex(i, outer, inner);
            if ((cur.y > p.y) != (prev.y > p.y) && p.x < (prev.x - cur.x) * (p.y - cur.y) / (prev.y - cur.y) + cur.x) inside = !inside;
            prev = cur;
        }
        return inside;
    }

    static Vector2 StarVertex(int i, float outer, float inner)
    {
        float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
        float r = i % 2 == 0 ? outer : inner;
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }
}
