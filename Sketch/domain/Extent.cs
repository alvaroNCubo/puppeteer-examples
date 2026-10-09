namespace Sketch;

/// <summary>
/// The size of the canvas, <see cref="Width"/> by <see cref="Height"/> whole canvas
/// units, and the single authority on what lies on it. The surface runs from 0 to
/// its width across and from 0 to its height down, edges included, so both corner
/// points (0, 0) and (width, height) are on the canvas.
/// </summary>
internal sealed class Extent
{
    /// <summary>How many units the canvas spans from its left edge to its right edge.</summary>
    internal int Width { get; }

    /// <summary>How many units the canvas spans from its top edge to its bottom edge.</summary>
    internal int Height { get; }

    /// <summary>Opens an extent of the given size; both sides must be positive.</summary>
    internal Extent(int width, int height)
    {
        if (width <= 0) throw new SketchRuleException($"a canvas must be wider than 0 units (got {width})");
        if (height <= 0) throw new SketchRuleException($"a canvas must be taller than 0 units (got {height})");
        Width = width;
        Height = height;
    }

    /// <summary>Whether <paramref name="point"/> lies on the canvas, edges included.</summary>
    internal bool Contains(Point point) =>
        point.X >= 0 && point.X <= Width && point.Y >= 0 && point.Y <= Height;

    /// <summary>
    /// Whether the whole of <paramref name="stroke"/> lies on the canvas. The canvas is a
    /// rectangle, and a straight mark lies inside a rectangle exactly when both of its
    /// end points do.
    /// </summary>
    internal bool Encloses(Stroke stroke) =>
        Contains(stroke.From) && Contains(stroke.To);
}
