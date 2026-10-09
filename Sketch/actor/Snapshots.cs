using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Sketch.Acting;

/// <summary>One stroke of a picture, as the picture projection printed it.</summary>
public sealed record StrokeView(int Id, int FromX, int FromY, int ToX, int ToY, string Ink);

/// <summary>
/// The canvas's picture as a typed value: its extent and its strokes bottom to top,
/// together with the exact <see cref="Rendered"/> text the picture projection printed
/// (the text the elision diff compares and the view draws).
/// </summary>
public sealed record PictureSnapshot(int Width, int Height, IReadOnlyList<StrokeView> Strokes, string Rendered)
{
    /// <summary>Parses the text the picture projection prints.</summary>
    public static PictureSnapshot Parse(string rendered)
    {
        ArgumentNullException.ThrowIfNull(rendered);

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rendered) ? "{}" : rendered);
        var root = document.RootElement;
        var strokes = new List<StrokeView>();

        // A foreach-print renders as an array keyed by the loop variable; an empty
        // loop prints nothing, so a canvas with no strokes has no "stroke" key.
        if (root.TryGetProperty("stroke", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in array.EnumerateArray())
            {
                strokes.Add(new StrokeView(
                    element.GetProperty("id").GetInt32(),
                    element.GetProperty("x1").GetInt32(),
                    element.GetProperty("y1").GetInt32(),
                    element.GetProperty("x2").GetInt32(),
                    element.GetProperty("y2").GetInt32(),
                    element.GetProperty("ink").GetString() ?? string.Empty));
            }
        }

        return new PictureSnapshot(
            root.GetProperty("width").GetInt32(),
            root.GetProperty("height").GetInt32(),
            strokes,
            rendered);
    }
}

/// <summary>What the rule would elide, found on a shadow without touching the primary.</summary>
/// <param name="WouldElide">The entry ids the rule would mark, ascending.</param>
/// <param name="Pairs">How many drawn-then-erased pairs the rule matched.</param>
/// <param name="RuleAsRegistered">The engine's own description of the registered rule.</param>
public sealed record ForgettingPreview(IReadOnlyList<long> WouldElide, long Pairs, string RuleAsRegistered);

/// <summary>One observation whose answer differs once a candidate set is elided.</summary>
/// <param name="Observation">Which question changed: <c>picture</c> or <c>stroke count</c>.</param>
/// <param name="WithoutElision">The answer rehydrating the journal as it is.</param>
/// <param name="WithElision">The answer rehydrating it with the candidate entries skipped.</param>
public sealed record ObservationChange(string Observation, string WithoutElision, string WithElision);

/// <summary>The elision-impact verdict for a candidate set, relative to the observations asked.</summary>
public sealed record ElisionProof(bool IsSafe, IReadOnlyList<ObservationChange> Changes);

/// <summary>
/// What a journal holds, counted record by record in one pass: every record still on
/// disk by kind, and how many of them carry an elision mark.
/// </summary>
public sealed record JournalCensus(
    int Records,
    int Scripts,
    int Defines,
    int Invocations,
    int ElisionMarks,
    long LowestEntryId,
    long HighestEntryId,
    IReadOnlyList<long> EntryIds);

/// <summary>
/// Bytes a journal occupies on disk: the record segments alone
/// (<c>journal/journal_*.bin</c>) and every file under the actor's folder
/// (segments, index, elision marks, skip set, reaction registry and checkpoints, meta).
/// </summary>
public sealed record JournalFootprint(long SegmentBytes, long TotalBytes, int Files);
