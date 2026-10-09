using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sketch.Rendering;

/// <summary>
/// Draws the canvas's printed picture projection as SVG: the extent as the drawing
/// area, then every stroke in the order printed (bottom to top), one colour per ink.
/// The input is the projection's rendered JSON text — the same text a query returns
/// and the elision diff compares — so either side of a diff can be drawn as it is.
/// </summary>
public static class PictureSvg
{
    private static readonly IReadOnlyDictionary<string, string> InkColours = new Dictionary<string, string>
    {
        ["graphite"] = "#4a4a4a",
        ["indigo"] = "#3b4cc0",
        ["vermilion"] = "#e34234",
        ["ochre"] = "#cc7722",
        ["viridian"] = "#40826d",
    };

    private const string UnknownInkColour = "#999999";
    private const string HighlightColour = "#ffcc00";

    /// <summary>The colour the view gives an ink name; an ink it does not know is drawn grey.</summary>
    public static string ColourOf(string ink)
    {
        ArgumentNullException.ThrowIfNull(ink);
        return InkColours.TryGetValue(ink, out var colour) ? colour : UnknownInkColour;
    }

    /// <summary>
    /// Renders <paramref name="renderedPicture"/> as a standalone SVG document
    /// <paramref name="pixelWidth"/> pixels wide. Strokes whose ids are in
    /// <paramref name="highlightIds"/> get a halo beneath them, so a reader can find them.
    /// </summary>
    public static string Render(string renderedPicture, int pixelWidth, string title, IReadOnlyCollection<int>? highlightIds = null)
    {
        ArgumentNullException.ThrowIfNull(renderedPicture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentNullException.ThrowIfNull(title);

        var picture = ParsedPicture.From(renderedPicture);
        var highlights = new HashSet<int>(highlightIds ?? Array.Empty<int>());
        int pixelHeight = (int)Math.Round(pixelWidth * (double)picture.Height / picture.Width);
        double strokeWidth = Math.Max(picture.Width, picture.Height) / 400.0;

        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {picture.Width} {picture.Height}\" width=\"{pixelWidth}\" height=\"{pixelHeight}\" role=\"img\">");
        svg.Append("<title>").Append(Escape(title)).Append("</title>");
        svg.Append(CultureInfo.InvariantCulture,
            $"<rect x=\"0\" y=\"0\" width=\"{picture.Width}\" height=\"{picture.Height}\" fill=\"#fbf8f1\" stroke=\"#d8d2c4\" stroke-width=\"{Format(strokeWidth)}\"/>");

        foreach (var stroke in picture.Strokes)
        {
            if (highlights.Contains(stroke.Id))
            {
                svg.Append(Line(stroke, HighlightColour, strokeWidth * 9, 0.85));
            }
        }

        foreach (var stroke in picture.Strokes)
        {
            svg.Append(Line(stroke, ColourOf(stroke.Ink), strokeWidth * 2, 1.0));
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>How many strokes the rendered picture lists.</summary>
    public static int StrokeCountOf(string renderedPicture)
    {
        ArgumentNullException.ThrowIfNull(renderedPicture);
        return ParsedPicture.From(renderedPicture).Strokes.Count;
    }

    /// <summary>The ids of the strokes the rendered picture lists, bottom to top.</summary>
    public static IReadOnlyList<int> StrokeIdsOf(string renderedPicture)
    {
        ArgumentNullException.ThrowIfNull(renderedPicture);
        var ids = new List<int>();
        foreach (var stroke in ParsedPicture.From(renderedPicture).Strokes)
        {
            ids.Add(stroke.Id);
        }

        return ids;
    }

    private static string Line(Stroke stroke, string colour, double width, double opacity) =>
        string.Create(CultureInfo.InvariantCulture,
            $"<line x1=\"{stroke.X1}\" y1=\"{stroke.Y1}\" x2=\"{stroke.X2}\" y2=\"{stroke.Y2}\" stroke=\"{colour}\" stroke-width=\"{Format(width)}\" stroke-linecap=\"round\" stroke-opacity=\"{Format(opacity)}\"/>");

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private sealed record Stroke(int Id, int X1, int Y1, int X2, int Y2, string Ink);

    private sealed record ParsedPicture(int Width, int Height, IReadOnlyList<Stroke> Strokes)
    {
        internal static ParsedPicture From(string rendered)
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rendered) ? "{}" : rendered);
            var root = document.RootElement;
            var strokes = new List<Stroke>();

            // The projection prints its foreach as an array keyed by the loop variable;
            // a canvas without strokes prints no array at all.
            if (root.TryGetProperty("stroke", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in array.EnumerateArray())
                {
                    strokes.Add(new Stroke(
                        element.GetProperty("id").GetInt32(),
                        element.GetProperty("x1").GetInt32(),
                        element.GetProperty("y1").GetInt32(),
                        element.GetProperty("x2").GetInt32(),
                        element.GetProperty("y2").GetInt32(),
                        element.GetProperty("ink").GetString() ?? string.Empty));
                }
            }

            int width = root.TryGetProperty("width", out var w) ? w.GetInt32() : 1;
            int height = root.TryGetProperty("height", out var h) ? h.GetInt32() : 1;
            return new ParsedPicture(Math.Max(width, 1), Math.Max(height, 1), strokes);
        }
    }
}
