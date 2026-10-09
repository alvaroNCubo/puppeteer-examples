using System;

namespace Sketch;

/// <summary>
/// The single exception the sketch domain throws: a caller asked for something the
/// canvas's rules forbid (a stroke id already on the canvas, a point off the canvas,
/// an ink outside the palette, erasing a stroke that is not there). A correct caller
/// asks the canvas first and never sees it.
/// </summary>
internal sealed class SketchRuleException : Exception
{
    /// <summary>Raises a rule violation with a message in the canvas's own words.</summary>
    internal SketchRuleException(string message)
        : base(message)
    {
    }
}
