using System.Collections.Generic;

namespace Sketch;

/// <summary>
/// The drawing surface and the aggregate root of the sketch: a fixed extent and the
/// strokes lying on it, stacked in the order they were drawn. Drawing lays a stroke on
/// top of all the others; erasing lifts one stroke off and leaves the rest exactly as
/// they lay. The picture is those strokes read from the bottom up, so it depends only on
/// which strokes are on the canvas and the order they were drawn in, never on strokes
/// that came and went.
/// </summary>
internal sealed class Canvas
{
    private readonly List<Stroke> stack = new();

    /// <summary>Opens a new, empty canvas of <paramref name="width"/> by <paramref name="height"/> units.</summary>
    internal Canvas(int width, int height)
    {
        Extent = new Extent(width, height);
    }

    /// <summary>The canvas's size, and what lies on it.</summary>
    internal Extent Extent { get; }

    /// <summary>How many strokes are on the canvas now.</summary>
    internal int StrokeCount => stack.Count;

    /// <summary>
    /// Lays stroke <paramref name="id"/> from (<paramref name="fromX"/>, <paramref name="fromY"/>)
    /// to (<paramref name="toX"/>, <paramref name="toY"/>) in the ink named <paramref name="ink"/>
    /// on top of every stroke already on the canvas. Refused, leaving the canvas as it was,
    /// when a stroke <paramref name="id"/> is already on the canvas, when either end point is
    /// off the canvas, or when the ink is not in the palette.
    /// </summary>
    internal void Draw(int id, int fromX, int fromY, int toX, int toY, string ink)
    {
        if (HasStroke(id)) throw new SketchRuleException($"stroke {id} is already on the canvas");

        var stroke = new Stroke(id, new Point(fromX, fromY), new Point(toX, toY), Ink.Named(ink));
        if (!Extent.Encloses(stroke))
        {
            throw new SketchRuleException(
                $"stroke {id} from ({fromX}, {fromY}) to ({toX}, {toY}) leaves the {Extent.Width} by {Extent.Height} canvas");
        }

        stack.Add(stroke);
    }

    /// <summary>
    /// Lifts stroke <paramref name="id"/> off the canvas; the strokes above and below it keep
    /// their order. Erasing a stroke that is not on the canvas is refused: ask
    /// <see cref="HasStroke"/> first.
    /// </summary>
    internal void Erase(int id)
    {
        int position = stack.FindIndex(stroke => stroke.Id == id);
        if (position < 0) throw new SketchRuleException($"stroke {id} is not on the canvas");
        stack.RemoveAt(position);
    }

    /// <summary>Whether a stroke <paramref name="id"/> is on the canvas now. Answers any id.</summary>
    internal bool HasStroke(int id) => stack.Exists(stroke => stroke.Id == id);

    /// <summary>
    /// The picture: every stroke on the canvas, from the bottom of the stack to the top,
    /// each later stroke lying over the earlier ones.
    /// </summary>
    internal IReadOnlyList<Stroke> Picture() => stack.ToArray();
}
