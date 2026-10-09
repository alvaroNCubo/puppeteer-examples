using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Choreography.Theater;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace Sketch.Acting;

/// <summary>
/// A typed facade over a <see cref="PerformanceV2"/> hosting the clean <c>Canvas</c>
/// domain on a FileSystem journal. Hosts talk only to these verbs and typed results;
/// every DSL string lives in this project. The canvas lives in actor state as the
/// root global <c>canvas</c>, seeded once when the journal is new, and each verb is a
/// parametrised check-then-command, so it journals as a V2 Action that reactions
/// observe and a refused check journals nothing.
/// <para>
/// Beyond drawing, the facade carries the three moves of forgetting: preview what the
/// <see cref="ForgettingRule"/> would elide (on a shadow), prove whether eliding a
/// candidate set changes any observation (the elision-impact diff), and commit (run the
/// rule on the primary, then Distill).
/// </para>
/// </summary>
public sealed class SketchActor : IDisposable
{
    private const string SeedCommand = "upgrade('seed') { canvas = Canvas(@width, @height); }";

    private const string DrawCheck = "{ Check(canvas.HasStroke(@id) == false) WARNING 'stroke already on the canvas'; }";
    private const string DrawCommand = "canvas.Draw(@id, @fromX, @fromY, @toX, @toY, @ink);";
    private const string EraseCheck = "{ Check(canvas.HasStroke(@id) == true) WARNING 'no such stroke on the canvas'; }";
    private const string EraseCommand = "canvas.Erase(@id);";

    private const string HasStrokeQuery = "{ print canvas.HasStroke(@id) present; }";

    /// <summary>
    /// The picture projection: the extent, then every stroke bottom to top with its points
    /// and ink. One constant serves the typed snapshot, the diff's observation and the view.
    /// It prints values; the formatter decides the text.
    /// </summary>
    public const string PictureQuery =
        "{ print canvas.Extent.Width width, canvas.Extent.Height height; " +
        "foreach (stroke in canvas.Picture()) { " +
        "print stroke.Id id, stroke.From.X x1, stroke.From.Y y1, stroke.To.X x2, stroke.To.Y y2, stroke.Ink.Name ink; } }";

    /// <summary>The second observation the diff asks: how many strokes are on the canvas.</summary>
    public const string StrokeCountQuery = "{ print canvas.StrokeCount strokes; }";

    // Registered on a census copy only, never on the primary: a registered
    // destination would put the primary's Distill behind the backup gate.
    private const string CensusDestination = "census";

    private readonly PerformanceV2 performance;
    private bool ruleDefinedOnPrimary;
    private int shadowSequence;
    private bool released;

    private SketchActor(string actorName, string journalDirectory, PerformanceV2 performance)
    {
        ActorName = actorName;
        JournalDirectory = journalDirectory;
        this.performance = performance;
    }

    /// <summary>
    /// Opens the canvas journaled under <paramref name="journalDirectory"/>. A new journal
    /// is seeded with an empty <paramref name="width"/> by <paramref name="height"/> canvas;
    /// an existing one rehydrates its canvas from disk and keeps its extent.
    /// </summary>
    public static SketchActor Open(string actorName, string journalDirectory, int width, int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(journalDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var performance = Start(actorName, journalDirectory);

        // Seed only a new journal. An upgrade re-issued on every open would skip its body
        // but still journal one more entry per open.
        if (performance.CurrentEntryId == 0)
        {
            performance.Using(SeedCommand)
                .WithParameters(p =>
                {
                    p["width", typeof(int)] = width;
                    p["height", typeof(int)] = height;
                })
                .PerformCommand();
        }

        return new SketchActor(actorName, journalDirectory, performance);
    }

    /// <summary>
    /// Opens the journal under <paramref name="journalDirectory"/> cold, times how long the
    /// engine takes to rehydrate it, and releases it. Returns the elapsed time and the
    /// picture the rehydrated canvas prints.
    /// </summary>
    public static (TimeSpan Elapsed, string RenderedPicture) MeasureColdStart(string actorName, string journalDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(journalDirectory);

        var clock = Stopwatch.StartNew();
        var performance = Start(actorName, journalDirectory);
        clock.Stop();
        try
        {
            return (clock.Elapsed, performance.Using(PictureQuery).PerformQuery());
        }
        finally
        {
            Release(performance);
        }
    }

    private static PerformanceV2 Start(string actorName, string journalDirectory) =>
        new PerformanceV2(actorName, typeof(SketchDomain).Assembly)
            .ConfigureStorage(DatabaseType.FileSystem, $"path={journalDirectory};maxFileSize=4194304")
            .Start();

    private static void Release(PerformanceV2 performance)
    {
        performance.Actor.GracefulExit();
        performance.Dispose();
    }

    /// <summary>The actor's name; with <see cref="JournalDirectory"/> it locates the journal.</summary>
    public string ActorName { get; }

    /// <summary>The directory the actor's FileSystem journal lives under.</summary>
    public string JournalDirectory { get; }

    /// <summary>The journal's head: the EntryId of the last record written.</summary>
    public long CurrentEntryId => performance.CurrentEntryId;

    // ── Verbs ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws stroke <paramref name="id"/>. Returns false, journaling nothing, when a stroke
    /// with that id is already on the canvas.
    /// </summary>
    public bool Draw(int id, int fromX, int fromY, int toX, int toY, string ink)
    {
        ArgumentNullException.ThrowIfNull(ink);

        return Journals(() => performance.Using(DrawCheck, DrawCommand)
            .WithParameters(p =>
            {
                p["id", typeof(int)] = id;
                p["fromX", typeof(int)] = fromX;
                p["fromY", typeof(int)] = fromY;
                p["toX", typeof(int)] = toX;
                p["toY", typeof(int)] = toY;
                p["ink", typeof(string)] = ink;
            })
            .PerformCheckThenCommand());
    }

    /// <summary>
    /// Erases stroke <paramref name="id"/>. Returns false, journaling nothing, when no stroke
    /// with that id is on the canvas.
    /// </summary>
    public bool Erase(int id) =>
        Journals(() => performance.Using(EraseCheck, EraseCommand)
            .WithParameters(p => p["id", typeof(int)] = id)
            .PerformCheckThenCommand());

    private bool Journals(Action perform)
    {
        long before = performance.CurrentEntryId;
        perform();
        return performance.CurrentEntryId > before;
    }

    // ── Reads ──────────────────────────────────────────────────────────────

    /// <summary>The picture as the projection prints it: the exact text the diff compares.</summary>
    public string RenderedPicture() => performance.Using(PictureQuery).PerformQuery();

    /// <summary>The picture as a typed value.</summary>
    public PictureSnapshot Picture() => PictureSnapshot.Parse(RenderedPicture());

    /// <summary>How many strokes are on the canvas.</summary>
    public int StrokeCount()
    {
        using var document = JsonDocument.Parse(performance.Using(StrokeCountQuery).PerformQuery());
        return document.RootElement.GetProperty("strokes").GetInt32();
    }

    /// <summary>Whether stroke <paramref name="id"/> is on the canvas.</summary>
    public bool HasStroke(int id)
    {
        string answer = performance.Using(HasStrokeQuery)
            .WithParameters(p => p["id", typeof(int)] = id)
            .PerformQuery();
        using var document = JsonDocument.Parse(answer);
        return document.RootElement.GetProperty("present").GetBoolean();
    }

    /// <summary>
    /// The picture as it stood right after entry <paramref name="entryId"/>: a shadow replays
    /// the primary's journal from genesis up to that entry and prints its picture. The shadow
    /// keeps its own journal under <paramref name="scratchDirectory"/>.
    /// </summary>
    public PictureSnapshot PictureAt(long entryId, string scratchDirectory)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(entryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchDirectory);

        // A FileSystem shadow keeps each copied record's EntryId, so it replays a journal
        // with the gaps Distill leaves; an in-memory shadow numbers the copies afresh.
        using Shadow shadow = performance.Actor.Shadow(new ShadowConfig(
            $"past-{++shadowSequence}",
            DatabaseType.FileSystem,
            $"path={scratchDirectory};maxFileSize=4194304"));
        shadow.SyncUntil(entryId);
        return PictureSnapshot.Parse(shadow.PerformQry(PictureQuery));
    }

    // ── Forgetting ─────────────────────────────────────────────────────────

    /// <summary>
    /// Scene "preview": registers the rule on a shadow in skip-preview mode, replays the
    /// primary's journal into it and runs the rule, capturing the entries it would elide
    /// without eliding anything anywhere. The primary is only read.
    /// </summary>
    public ForgettingPreview Preview()
    {
        using Shadow shadow = OpenShadow("preview", actor => ForgettingRule.DefineOn(actor.Reactions));
        shadow.EnableSkipPreview();
        shadow.SyncUntil(performance.CurrentEntryId);
        shadow.Reactions.Execute();

        var reaction = shadow.Reactions[ForgettingRule.Name];
        return new ForgettingPreview(
            reaction.WouldSkip.OrderBy(entryId => entryId).ToArray(),
            reaction.MatchCount,
            shadow.Actor.Introspection.ShowReaction(ForgettingRule.Name));
    }

    /// <summary>
    /// Scene "proof": on a shadow of the primary, rehydrates the journal twice — as it is,
    /// and with <paramref name="candidateEntryIds"/> skipped — and compares the picture and
    /// the stroke count between the two. Safe means neither observation changed.
    /// </summary>
    public ElisionProof Prove(IReadOnlyCollection<long> candidateEntryIds) =>
        ProveAt(performance.CurrentEntryId, candidateEntryIds);

    /// <summary>
    /// The same proof asked of the past: the shadow replays the journal only up to
    /// <paramref name="entryId"/>, so the observations compared are the picture and the
    /// stroke count as they stood right after that entry.
    /// </summary>
    public ElisionProof ProveAt(long entryId, IReadOnlyCollection<long> candidateEntryIds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(entryId);
        ArgumentNullException.ThrowIfNull(candidateEntryIds);

        using Shadow shadow = OpenShadow("proof");
        shadow.SyncUntil(entryId);
        var result = shadow.ElisionImpactDiff(candidateEntryIds.ToArray(), PictureQuery, StrokeCountQuery);

        var changes = result.Differences
            .Select(difference => new ObservationChange(
                ObservationName(difference.Observation), difference.WithoutElision, difference.WithElision))
            .ToArray();
        return new ElisionProof(result.IsSafe, changes);
    }

    private static string ObservationName(string query) => query switch
    {
        PictureQuery => "picture",
        StrokeCountQuery => "stroke count",
        _ => query,
    };

    /// <summary>
    /// Scene "commit", first half: runs the rule on the primary, which marks every matched
    /// pair as elided in its journal (a logical elision: the records stay on disk, and
    /// rehydration skips them). Returns how many pairs this run matched; a second run,
    /// resuming from the rule's checkpoint, matches none.
    /// </summary>
    public long ElideErasedStrokes()
    {
        if (!ruleDefinedOnPrimary)
        {
            ForgettingRule.DefineOn(performance.Actor.Reactions);
            ruleDefinedOnPrimary = true;
        }

        // A reaction's counters restart with every execution, so after it MatchCount
        // is this run's count.
        performance.Actor.Reactions.Execute();
        return performance.Actor.Reactions[ForgettingRule.Name].MatchCount;
    }

    /// <summary>
    /// Scene "commit", second half: physically removes the elided records from the journal.
    /// The engine keeps the journal's last record even when it is elided.
    /// </summary>
    public void Distill() => performance.Distill();

    /// <summary>
    /// Counts the journal record by record in one pass, from a copy made under
    /// <paramref name="scratchDirectory"/> and opened read-only for introspection, so the
    /// primary's journal is never written to.
    /// </summary>
    public JournalCensus Census(string scratchDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchDirectory);
        return CensusOf(ActorName, JournalDirectory, scratchDirectory);
    }

    /// <summary>Counts, in one pass, the journal of <paramref name="actorName"/> under <paramref name="journalDirectory"/>.</summary>
    public static JournalCensus CensusOf(string actorName, string journalDirectory, string scratchDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(journalDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchDirectory);

        JournalFolder.Copy(journalDirectory, actorName, scratchDirectory);

        // Opened for introspection the copy is not rehydrated, so its head reads 0 and a
        // destination registered now may read every record from genesis.
        var inspector = new ActorV2(actorName, typeof(SketchDomain).Assembly);
        inspector.ConfigureStorageForIntrospection(DatabaseType.FileSystem, $"path={scratchDirectory};maxFileSize=4194304");
        try
        {
            inspector.Materialization.Register(CensusDestination);
            IReadOnlyList<MaterializationRecord> records = inspector.Materialization.ReadRecordsAfter(CensusDestination, 0);
            long lowest = records.Count == 0 ? 0 : records.Min(record => record.EntryId);
            long highest = records.Count == 0 ? 0 : records.Max(record => record.EntryId);
            int marks = records.Count == 0
                ? 0
                : inspector.Materialization.ReadElidedRange(CensusDestination, 1, highest).Count;

            return new JournalCensus(
                records.Count,
                records.Count(record => record.Kind == MaterializationRecordKind.Script),
                records.Count(record => record.Kind == MaterializationRecordKind.Define),
                records.Count(record => record.Kind == MaterializationRecordKind.Invocation),
                marks,
                lowest,
                highest,
                records.Select(record => record.EntryId).ToArray());
        }
        finally
        {
            inspector.GracefulExit();
        }
    }

    /// <summary>Bytes the journal occupies on disk (see <see cref="JournalFootprint"/>).</summary>
    public JournalFootprint Footprint() => JournalFolder.Measure(JournalDirectory, ActorName);

    /// <summary>Copies the journal folder, as it is now, under <paramref name="destinationDirectory"/>.</summary>
    public void CopyJournalTo(string destinationDirectory) =>
        JournalFolder.Copy(JournalDirectory, ActorName, destinationDirectory);

    private Shadow OpenShadow(string purpose, Action<Actor>? configureReactions = null) =>
        performance.Actor.Shadow(new ShadowConfig(
            $"{purpose}-{++shadowSequence}",
            DatabaseType.IN_MEMORY,
            "memory",
            configureReactions: configureReactions));

    /// <summary>Releases the journal: the actor exits gracefully and the host is disposed.</summary>
    public void Dispose()
    {
        if (released) return;
        released = true;
        Release(performance);
    }
}
