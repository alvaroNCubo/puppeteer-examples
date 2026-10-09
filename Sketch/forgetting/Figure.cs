using System;
using System.Collections.Generic;

namespace Sketch.Forgetting;

/// <summary>
/// The picture the scripted session draws: a flower on a 1600 by 1200 canvas, made of
/// parametric curves in five inks. Each part is a curve sampled into straight strokes;
/// the more survivors the session has, the finer the sampling, and the picture stays
/// the same drawing.
/// </summary>
internal static class Figure
{
    internal const int Width = 1600;
    internal const int Height = 1200;

    private static readonly (double X, double Y) Bloom = (800, 420);

    /// <summary>A named part of the drawing: its ink, its share of the strokes, and its curve over t in [0, 1].</summary>
    internal sealed record Part(string Name, string Ink, double Share, Func<double, (double X, double Y)> Curve);

    internal static readonly IReadOnlyList<Part> Parts = new[]
    {
        new Part("ground", "graphite", 0.08, t => (80 + 1440 * t, 1090 + 10 * Math.Sin(2 * Math.PI * 6 * t))),
        new Part("stem", "viridian", 0.08, t => Bezier(t, (800, 1090), (700, 820), (800, 560))),
        new Part("left leaf", "viridian", 0.08, t => Leaf(t, (760, 900), (520, 760), 70)),
        new Part("right leaf", "viridian", 0.08, t => Leaf(t, (790, 760), (1060, 640), 60)),
        new Part("petals", "vermilion", 0.34, t => Rose(t, 250, 4)),
        new Part("heart", "ochre", 0.18, t => Spiral(t, 6, 64, 5)),
        new Part("cloud", "indigo", 0.16, t => Cloud(t)),
    };

    /// <summary>
    /// The survivors: <paramref name="count"/> strokes that together draw the figure, part
    /// by part, each part sampled into its share of the strokes.
    /// </summary>
    internal static IReadOnlyList<(string Part, string Ink, int FromX, int FromY, int ToX, int ToY)> Strokes(int count)
    {
        var strokes = new List<(string, string, int, int, int, int)>(count);
        int assigned = 0;
        for (int p = 0; p < Parts.Count; p++)
        {
            var part = Parts[p];
            int segments = p == Parts.Count - 1 ? count - assigned : (int)Math.Round(part.Share * count);
            segments = Math.Max(0, Math.Min(segments, count - assigned));
            assigned += segments;
            for (int i = 0; i < segments; i++)
            {
                var from = part.Curve((double)i / segments);
                var to = part.Curve((double)(i + 1) / segments);
                strokes.Add((part.Name, part.Ink, Clamp(from.X, Width), Clamp(from.Y, Height), Clamp(to.X, Width), Clamp(to.Y, Height)));
            }
        }

        return strokes;
    }

    internal static int Clamp(double value, int limit) => (int)Math.Round(Math.Clamp(value, 0, limit));

    private static (double X, double Y) Bezier(double t, (double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2)
    {
        double u = 1 - t;
        return (u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X, u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y);
    }

    // A lens from base to tip and back: out along one side, home along the other.
    private static (double X, double Y) Leaf(double t, (double X, double Y) root, (double X, double Y) tip, double bulge)
    {
        double along = t < 0.5 ? 2 * t : 2 - 2 * t;
        double side = t < 0.5 ? 1 : -1;
        double dx = tip.X - root.X;
        double dy = tip.Y - root.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        double nx = -dy / length;
        double ny = dx / length;
        double offset = side * bulge * Math.Sin(Math.PI * along);
        return (root.X + dx * along + nx * offset, root.Y + dy * along + ny * offset);
    }

    // r = radius * cos(k * theta) over a full turn: 2k petals for an even k.
    private static (double X, double Y) Rose(double t, double radius, int k)
    {
        double theta = 2 * Math.PI * t;
        double r = radius * Math.Cos(k * theta);
        return (Bloom.X + r * Math.Cos(theta), Bloom.Y + r * Math.Sin(theta));
    }

    private static (double X, double Y) Spiral(double t, double inner, double outer, int turns)
    {
        double theta = 2 * Math.PI * turns * t;
        double r = inner + outer * t;
        return (Bloom.X + r * Math.Cos(theta), Bloom.Y + r * Math.Sin(theta));
    }

    private static (double X, double Y) Cloud(double t)
    {
        double theta = 2 * Math.PI * t;
        return (1290 + 170 * Math.Cos(theta) + 45 * Math.Cos(7 * theta), 230 + 100 * Math.Sin(theta) + 45 * Math.Sin(7 * theta));
    }
}
