using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small chart kit for the classroom portal, in the UIKit look (navy cards, cyan data, Inter).
/// Every bar and point carries its value as text, so nothing is shown by colour or length alone.
/// Charts take values from 0 to 1 (fractions) plus the text to print next to them.
/// </summary>
public static class UICharts
{
    public struct Item
    {
        public string label;
        public float value;     // 0..1
        public string valueText;
        public Color? color;
        public Item(string label, float value, string valueText, Color? color = null)
        {
            this.label = label; this.value = Mathf.Clamp01(value); this.valueText = valueText; this.color = color;
        }
    }

    static readonly Color Track = new Color(1f, 1f, 1f, 0.08f);
    static readonly Color Muted = new Color32(0x94, 0xA3, 0xB8, 255);

    /// <summary>A titled card (rounded raised panel) that charts and tables sit in. Returns the card's content column.</summary>
    public static RectTransform Card(Transform parent, string title, string subtitle = null, float height = -1f)
    {
        var card = UIKit.Rect("Card_" + title, parent);
        var le = card.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f; le.preferredWidth = 0f;   // cards side by side share a row equally
        UIKit.AddImage(card, UIKit.PanelRaised, rounded: true).raycastTarget = false;
        var v = UIKit.Vertical(card, 24, 10);
        v.padding.top = 18;
        if (height > 0f) UIKit.Size(card, -1, height);
        var head = UIKit.Header(card, title, 20f);
        head.characterSpacing = 3f;
        if (!string.IsNullOrEmpty(subtitle)) UIKit.Text(card, "Subtitle", subtitle, 18f, Muted);
        return card;
    }

    public static TextMeshProUGUI Empty(Transform parent, string text)
    {
        var t = UIKit.Text(parent, "Empty", text, 21f, Muted, TextAlignmentOptions.MidlineLeft);
        UIKit.Size(t, -1, 60);
        return t;
    }

    /// <summary>Vertical bars with the value above each bar and the label below (levels, days...).</summary>
    public static RectTransform VerticalBars(Transform parent, IList<Item> items, float height = 240f, Color? color = null)
    {
        var area = UIKit.Rect("Bars", parent);
        UIKit.Size(area, -1, height);
        // Guides at 50% and 100%
        foreach (float g in new[] { 0.5f, 1f })
        {
            var line = UIKit.Rect("Guide" + g, area);
            line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0);
            line.pivot = new Vector2(0.5f, 0.5f);
            float y = 34f + (height - 34f - 30f) * g;
            line.anchoredPosition = new Vector2(0, y);
            line.sizeDelta = new Vector2(0, 1.5f);
            UIKit.AddImage(line, new Color(1, 1, 1, 0.07f)).raycastTarget = false;
        }
        var row = UIKit.Rect("Columns", area);
        UIKit.Stretch(row);
        var h = UIKit.Horizontal(row, 14);
        h.childForceExpandHeight = true;
        foreach (var it in items)
        {
            var col = UIKit.Rect("Bar_" + it.label, row);
            var label = UIKit.Text(col, "Label", it.label, 17f, UIKit.TextSecondary, TextAlignmentOptions.Bottom, bold: true);
            label.rectTransform.anchorMin = new Vector2(0, 0); label.rectTransform.anchorMax = new Vector2(1, 0);
            label.rectTransform.pivot = new Vector2(0.5f, 0); label.rectTransform.sizeDelta = new Vector2(0, 30);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true; label.fontSizeMin = 13f; label.fontSizeMax = 17f;

            var track = UIKit.Rect("Track", col);
            track.anchorMin = new Vector2(0.18f, 0); track.anchorMax = new Vector2(0.82f, 1);
            track.offsetMin = new Vector2(0, 34); track.offsetMax = new Vector2(0, -30);
            UIKit.AddImage(track, Track, rounded: true).raycastTarget = false;
            var fill = UIKit.Rect("Fill", track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(1, Mathf.Max(0.012f, it.value));
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            UIKit.AddImage(fill, it.color ?? color ?? UIKit.Accent, rounded: true).raycastTarget = false;

            var value = UIKit.Text(col, "Value", it.valueText, 19f, UIKit.TextPrimary, TextAlignmentOptions.Bottom, bold: true);
            value.rectTransform.anchorMin = new Vector2(0, 0); value.rectTransform.anchorMax = new Vector2(1, 0);
            value.rectTransform.pivot = new Vector2(0.5f, 0);
            value.rectTransform.sizeDelta = new Vector2(0, 26);
            value.rectTransform.anchoredPosition = new Vector2(0, 34 + (height - 34 - 30) * it.value + 2);
            value.textWrappingMode = TextWrappingModes.NoWrap;
        }
        return area;
    }

    /// <summary>Horizontal bars: label on the left, bar in the middle, value on the right (students, topics...).</summary>
    public static RectTransform HorizontalBars(Transform parent, IList<Item> items, float labelWidth = 300f, float rowHeight = 38f, Color? color = null)
    {
        var list = UIKit.Rect("HBars", parent);
        UIKit.Vertical(list, 0, 8);
        foreach (var it in items)
        {
            var row = UIKit.Rect("Row_" + it.label, list);
            UIKit.Size(row, -1, rowHeight);
            var label = UIKit.Text(row, "Label", it.label, 19f, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = new Vector2(0, 0); label.rectTransform.anchorMax = new Vector2(0, 1);
            label.rectTransform.pivot = new Vector2(0, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(labelWidth - 12f, 0);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;

            var track = UIKit.Rect("Track", row);
            track.anchorMin = new Vector2(0, 0.5f); track.anchorMax = new Vector2(1, 0.5f);
            track.offsetMin = new Vector2(labelWidth, -9); track.offsetMax = new Vector2(-150, 9);
            UIKit.AddImage(track, Track, rounded: true).raycastTarget = false;
            var fill = UIKit.Rect("Fill", track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Max(0.01f, it.value), 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            UIKit.AddImage(fill, it.color ?? color ?? UIKit.Accent, rounded: true).raycastTarget = false;

            var value = UIKit.Text(row, "Value", it.valueText, 19f, UIKit.TextPrimary, TextAlignmentOptions.MidlineRight, bold: true);
            value.rectTransform.anchorMin = new Vector2(1, 0); value.rectTransform.anchorMax = new Vector2(1, 1);
            value.rectTransform.pivot = new Vector2(1, 0.5f);
            value.rectTransform.sizeDelta = new Vector2(140, 0);
            value.textWrappingMode = TextWrappingModes.NoWrap;
        }
        return list;
    }

    /// <summary>Line chart of values over time, with a dot, value and date label at each point.</summary>
    public static RectTransform LineChart(Transform parent, IList<Item> points, float height = 240f, Color? color = null)
    {
        var area = UIKit.Rect("Line", parent);
        UIKit.Size(area, -1, height);
        var plot = UIKit.Rect("Plot", area);
        UIKit.Stretch(plot);
        plot.offsetMin = new Vector2(20, 34); plot.offsetMax = new Vector2(-20, -30);
        var bg = UIKit.Rect("Guides", plot);
        UIKit.Stretch(bg);
        foreach (float g in new[] { 0f, 0.5f, 1f })
        {
            var line = UIKit.Rect("Guide" + g, bg);
            line.anchorMin = new Vector2(0, g); line.anchorMax = new Vector2(1, g);
            line.sizeDelta = new Vector2(0, 1.5f);
            UIKit.AddImage(line, new Color(1, 1, 1, g == 0f ? 0.15f : 0.07f)).raycastTarget = false;
        }
        var graph = UIKit.Rect("Graph", plot);
        UIKit.Stretch(graph);
        graph.gameObject.AddComponent<CanvasRenderer>();
        var g2 = graph.gameObject.AddComponent<UILineGraph>();
        g2.color = color ?? UIKit.Accent;
        g2.raycastTarget = false;
        var values = new List<float>();
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            values.Add(points[i].value);
            float x = n == 1 ? 0.5f : (float)i / (n - 1);
            var dotLabel = UIKit.Text(plot, "Value" + i, points[i].valueText, 17f, UIKit.TextPrimary, TextAlignmentOptions.Bottom, bold: true);
            var r = dotLabel.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(x, points[i].value);
            r.pivot = new Vector2(0.5f, 0);
            r.anchoredPosition = new Vector2(0, 8);
            r.sizeDelta = new Vector2(90, 24);
            dotLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var date = UIKit.Text(plot, "Label" + i, points[i].label, 16f, UIKit.TextSecondary, TextAlignmentOptions.Top);
            var d = date.rectTransform;
            d.anchorMin = d.anchorMax = new Vector2(x, 0);
            d.pivot = new Vector2(0.5f, 1);
            d.anchoredPosition = new Vector2(0, -6);
            d.sizeDelta = new Vector2(90, 24);
            date.textWrappingMode = TextWrappingModes.NoWrap;
        }
        g2.SetValues(values);
        return area;
    }

    /// <summary>Big number with a caption, for dashboard summaries.</summary>
    public static RectTransform StatTile(Transform parent, string value, string label, Color? valueColor = null, string hint = null)
    {
        var tile = UIKit.Rect("Stat_" + label, parent);
        var le = tile.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f; le.preferredWidth = 0f;
        UIKit.AddImage(tile, UIKit.PanelRaised, rounded: true).raycastTarget = false;
        var o = tile.gameObject.AddComponent<Outline>();
        o.effectColor = UIKit.Border; o.effectDistance = new Vector2(1.5f, -1.5f);
        var v = UIKit.Text(tile, "Value", value, 40f, valueColor ?? UIKit.TextPrimary, TextAlignmentOptions.Top, bold: true);
        v.rectTransform.anchorMin = new Vector2(0, 1); v.rectTransform.anchorMax = new Vector2(1, 1);
        v.rectTransform.offsetMin = new Vector2(8, -66); v.rectTransform.offsetMax = new Vector2(-8, -12);
        v.textWrappingMode = TextWrappingModes.NoWrap; v.enableAutoSizing = true; v.fontSizeMin = 24f; v.fontSizeMax = 40f;
        var l = UIKit.Text(tile, "Label", label.ToUpperInvariant(), 16f, Muted, TextAlignmentOptions.Top, bold: true);
        l.characterSpacing = 2f;
        l.rectTransform.anchorMin = new Vector2(0, 1); l.rectTransform.anchorMax = new Vector2(1, 1);
        l.rectTransform.offsetMin = new Vector2(8, -96); l.rectTransform.offsetMax = new Vector2(-8, -70);
        l.textWrappingMode = TextWrappingModes.NoWrap; l.enableAutoSizing = true; l.fontSizeMin = 12f; l.fontSizeMax = 16f;
        if (!string.IsNullOrEmpty(hint))
        {
            var h = UIKit.Text(tile, "Hint", hint, 15f, Muted, TextAlignmentOptions.Top);
            h.rectTransform.anchorMin = new Vector2(0, 1); h.rectTransform.anchorMax = new Vector2(1, 1);
            h.rectTransform.offsetMin = new Vector2(8, -120); h.rectTransform.offsetMax = new Vector2(-8, -96);
            h.textWrappingMode = TextWrappingModes.NoWrap; h.overflowMode = TextOverflowModes.Ellipsis;
        }
        return tile;
    }

    public static string Percent(float v) => v < 0f ? "-" : Mathf.RoundToInt(v * 100f) + "%";

    /// <summary>Green / amber / red for a score fraction, always shown next to the number itself.</summary>
    public static Color ScoreColor(float v) => v < 0f ? Muted : v >= 0.75f ? UIKit.Safe : v >= 0.5f ? UIKit.Warning : UIKit.Danger;
}
