namespace Sketch;

/// <summary>
/// One straight mark on the canvas, from one point to another, in one ink. A stroke
/// is known by the <see cref="Id"/> its author gave it; the canvas never invents one.
/// </summary>
internal sealed class Stroke
{
    /// <summary>The name the author gave this stroke, unique among the strokes on the canvas.</summary>
    internal int Id { get; }

    /// <summary>Where the mark starts.</summary>
    internal Point From { get; }

    /// <summary>Where the mark ends. It may coincide with <see cref="From"/>: a dot.</summary>
    internal Point To { get; }

    /// <summary>The ink the mark is made in.</summary>
    internal Ink Ink { get; }

    /// <summary>Makes the mark <paramref name="id"/> from <paramref name="from"/> to <paramref name="to"/>.</summary>
    internal Stroke(int id, Point from, Point to, Ink ink)
    {
        if (from is null) throw new SketchRuleException($"stroke {id} needs a point to start from");
        if (to is null) throw new SketchRuleException($"stroke {id} needs a point to end at");
        if (ink is null) throw new SketchRuleException($"stroke {id} needs an ink");
        Id = id;
        From = from;
        To = to;
        Ink = ink;
    }
}
