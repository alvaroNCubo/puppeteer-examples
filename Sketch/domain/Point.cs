namespace Sketch;

/// <summary>
/// A position on the canvas, in whole canvas units: <see cref="X"/> grows to the
/// right and <see cref="Y"/> grows downward from the top-left corner. A plain
/// carrier of its two coordinates; two points are the same only if they are the
/// same instance.
/// </summary>
internal sealed class Point
{
    /// <summary>Distance from the canvas's left edge.</summary>
    internal int X { get; }

    /// <summary>Distance from the canvas's top edge.</summary>
    internal int Y { get; }

    /// <summary>Names the position (<paramref name="x"/>, <paramref name="y"/>).</summary>
    internal Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}
