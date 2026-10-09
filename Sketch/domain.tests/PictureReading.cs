using System.Linq;

namespace Sketch.Tests;

/// <summary>
/// Reads a canvas's picture into plain text lines, one stroke per line, bottom to top,
/// so two canvases can be compared the way an observer of the picture would compare them.
/// </summary>
internal static class PictureReading
{
    internal static string[] Lines(Canvas canvas) =>
        canvas.Picture()
            .Select(s => $"{s.Id}: ({s.From.X},{s.From.Y})-({s.To.X},{s.To.Y}) {s.Ink.Name}")
            .ToArray();

    internal static int[] Ids(Canvas canvas) =>
        canvas.Picture().Select(s => s.Id).ToArray();
}
