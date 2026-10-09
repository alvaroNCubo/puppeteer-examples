using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sketch.Acting;
using Sketch.Rendering;

namespace Sketch.Forgetting;

/// <summary>
/// Runs the scenes in order against one primary journal and records what each measured.
/// Every claim a scene makes is checked here, at run time; a check that fails stops the
/// run before any evidence is written.
/// </summary>
internal sealed class Lab
{
    private const string ActorName = "canvas";
    private const int SvgWidth = 640;
    private static readonly string[] States = { "full", "logical", "physical" };

    private readonly LabOptions options;
    private readonly LabLog log = new();
    private readonly string output;
    private readonly string work;
    private readonly Dictionary<string, string> svgs = new();
    private int scratchSequence;

    internal Lab(LabOptions options)
    {
        this.options = options;
        output = Path.GetFullPath(options.Out);
        work = Path.Combine(output, "work");
    }

    internal void Run()
    {
        if (Directory.Exists(work) && Directory.EnumerateFileSystemEntries(work).Any())
        {
            throw new LabCheckFailed($"'{options.Out}' already holds a run; choose an empty --out");
        }

        Directory.CreateDirectory(work);
        var total = Stopwatch.StartNew();
        var engine = Engine.Describe();
        var session = Session.Generate(options.Seed, options.ScaledSurvivors, options.ScaledHesitations);

        log.Line("Forgetting on Purpose — lab run");
        log.Fact("date (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        log.Fact("seed / survivors / hesitations", $"{options.Seed} / {session.Survivors} / {session.Hesitations} (scale {options.Scale})");
        log.Fact("engine commit / build", $"{engine.Commit} / {engine.Build}");

        string primary = Path.Combine(work, "primary");
        var sketch = SketchActor.Open(ActorName, primary, Figure.Width, Figure.Height);

        // ── 1. The session ────────────────────────────────────────────────
        log.Scene("1. The session");
        var clock = Stopwatch.StartNew();
        var entryAfter = new long[session.Acts.Count];
        var drawnAt = new Dictionary<int, long>();
        var forgettable = new List<long>();
        long maxDistance = 0;
        for (int i = 0; i < session.Acts.Count; i++)
        {
            var act = session.Acts[i];
            bool journaled = act.IsDraw
                ? sketch.Draw(act.Id, act.FromX, act.FromY, act.ToX, act.ToY, act.Ink)
                : sketch.Erase(act.Id);
            Check(journaled, $"act {i} ({(act.IsDraw ? "draw" : "erase")} {act.Id}) was refused");
            entryAfter[i] = sketch.CurrentEntryId;

            if (act.IsDraw)
            {
                drawnAt[act.Id] = sketch.CurrentEntryId;
            }
            else
            {
                forgettable.Add(drawnAt[act.Id]);
                forgettable.Add(sketch.CurrentEntryId);
                maxDistance = Math.Max(maxDistance, sketch.CurrentEntryId - drawnAt[act.Id]);
            }
        }

        clock.Stop();
        forgettable.Sort();
        long head = sketch.CurrentEntryId;
        string picture = sketch.RenderedPicture();
        int strokes = sketch.StrokeCount();
        var fullCensus = sketch.Census(Scratch("census-full"));
        var full = State(sketch, fullCensus);
        Check(strokes == session.Survivors, $"{strokes} strokes on the canvas, expected the {session.Survivors} survivors");
        Check(maxDistance <= 1001, $"a hesitation was erased {maxDistance} entries after its draw, beyond the pairing window");
        string fullSnapshot = Snapshot(sketch, "full");
        svgs["picture"] = PictureSvg.Render(picture, SvgWidth, "The picture the session drew");

        var sessionNumbers = new SessionNumbers(
            session.Acts.Count,
            session.Acts.Count(act => act.IsDraw),
            session.Acts.Count(act => !act.IsDraw),
            head,
            maxDistance,
            strokes,
            Sha256(picture),
            Seconds(clock));
        log.Fact("acts (draws + erases)", $"{sessionNumbers.Acts} ({sessionNumbers.Draws} + {sessionNumbers.Erases})");
        log.Fact("journal head", head);
        log.Fact("records (script/define/invocation)", $"{full.Records} ({full.Scripts}/{full.Defines}/{full.Invocations})");
        log.Fact("bytes: segments / all files", $"{full.SegmentBytes:N0} / {full.TotalBytes:N0}");
        log.Fact("strokes on the canvas", strokes);
        log.Fact("longest draw→erase distance", $"{maxDistance} entries");
        log.Fact("picture SHA-256", sessionNumbers.PictureSha256);
        log.Fact("session time", $"{sessionNumbers.Seconds} s");

        // ── 2. The rule / 3. The preview ──────────────────────────────────
        var previewClock = Stopwatch.StartNew();
        var preview = sketch.Preview();
        previewClock.Stop();

        log.Scene("2. The rule");
        string rule = RuleAsRegistered(preview.RuleAsRegistered);
        foreach (string line in rule.Split('\n'))
        {
            log.Line("   | " + line);
        }

        log.Scene("3. The preview");
        var afterPreview = State(sketch);
        bool untouched = sketch.CurrentEntryId == head
            && afterPreview == full
            && sketch.RenderedPicture() == picture;
        Check(untouched, "the preview changed the primary");
        Check(preview.WouldElide.SequenceEqual(forgettable), "the preview does not list exactly the drawn-then-erased pairs");
        Check(preview.Pairs == session.Hesitations, $"the preview matched {preview.Pairs} pairs, expected {session.Hesitations}");
        var previewNumbers = new PreviewNumbers(preview.WouldElide.Count, preview.Pairs, untouched, Seconds(previewClock));
        log.Fact("entries the rule would elide", previewNumbers.WouldElide);
        log.Fact("pairs matched", previewNumbers.Pairs);
        log.Fact("primary head / records / bytes", $"{sketch.CurrentEntryId} / {afterPreview.Records} / {afterPreview.TotalBytes:N0} (unchanged)");
        log.Fact("preview time", $"{previewNumbers.Seconds} s");

        // ── 4. The proof ──────────────────────────────────────────────────
        log.Scene("4. The proof");
        var safeClock = Stopwatch.StartNew();
        var safe = sketch.Prove(preview.WouldElide);
        safeClock.Stop();
        Check(safe.IsSafe, "eliding the rule's pairs changed an observation");

        var comeback = session.MostVisibleHesitation();
        long comebackDraw = drawnAt[comeback.Id];
        var loneErase = preview.WouldElide.Where(entry => entry != comebackDraw).ToArray();
        var unsafeClock = Stopwatch.StartNew();
        var risky = sketch.Prove(loneErase);
        unsafeClock.Stop();
        Check(!risky.IsSafe, "eliding an Erase without its Draw changed nothing");
        var pictureChange = risky.Changes.SingleOrDefault(change => change.Observation == "picture");
        Check(pictureChange is not null, "the unsafe diff does not name the picture");
        var resurrected = PictureSvg.StrokeIdsOf(pictureChange!.WithElision).Except(PictureSvg.StrokeIdsOf(pictureChange.WithoutElision)).ToArray();
        Check(resurrected.SequenceEqual(new[] { comeback.Id }), "the unsafe diff did not bring back exactly the chosen stroke");
        Check(pictureChange.WithoutElision == picture, "the diff's unelided picture is not the primary's picture");
        svgs["unsafe-without"] = PictureSvg.Render(pictureChange.WithoutElision, SvgWidth, "Without elision");
        svgs["unsafe-with"] = PictureSvg.Render(pictureChange.WithElision, SvgWidth, $"With the Erase of stroke {comeback.Id} elided but not its Draw", new[] { comeback.Id });

        var proofNumbers = new ProofNumbers(
            safe.IsSafe,
            Seconds(safeClock),
            risky.IsSafe,
            risky.Changes.Select(change => change.Observation).ToArray(),
            comeback.Id,
            Seconds(unsafeClock));
        log.Fact("the rule's pairs", $"{(safe.IsSafe ? "SAFE" : "UNSAFE")} — {safe.Changes.Count} observations changed ({proofNumbers.SafeSeconds} s)");
        log.Fact($"all but the Draw of stroke {comeback.Id}", $"{(risky.IsSafe ? "SAFE" : "UNSAFE")} — changed: {string.Join(", ", proofNumbers.LoneEraseChangedObservations)} ({proofNumbers.UnsafeSeconds} s)");

        // ── 6, before the commit: the past as the journal still holds it ───
        var (midIndex, onCanvasAtMid) = session.MidSessionMoment();
        long mid = entryAfter[midIndex];
        var then = sketch.PictureAt(mid, Scratch("past-then"));
        Check(onCanvasAtMid.All(id => then.Strokes.Any(stroke => stroke.Id == id)), "the picture at mid-session lacks the hesitations on the canvas then");
        var pastProof = sketch.ProveAt(mid, preview.WouldElide.Where(entry => entry <= mid).ToArray());
        var pastPicture = pastProof.Changes.SingleOrDefault(change => change.Observation == "picture");
        Check(!pastProof.IsSafe && pastPicture is not null, "forgetting did not change the picture of the past");
        var forgottenPast = PictureSnapshot.Parse(pastPicture!.WithElision);
        Check(forgottenPast.Strokes.All(stroke => !onCanvasAtMid.Contains(stroke.Id)), "the forgotten past still shows a hesitation");
        svgs["past-then"] = PictureSvg.Render(then.Rendered, SvgWidth, $"The canvas right after entry {mid}, before Distill", onCanvasAtMid.ToArray());

        // ── 5. The commit ─────────────────────────────────────────────────
        log.Scene("5. The commit");
        var elideClock = Stopwatch.StartNew();
        long pairsElided = sketch.ElideErasedStrokes();
        elideClock.Stop();
        Check(pairsElided == session.Hesitations, $"the rule elided {pairsElided} pairs on the primary, expected {session.Hesitations}");
        var logical = State(sketch);
        Check(logical.Records == full.Records, "eliding removed records");
        Check(logical.ElisionMarks == forgettable.Count, $"{logical.ElisionMarks} elision marks, expected {forgettable.Count}");
        string logicalSnapshot = Snapshot(sketch, "logical");

        var distillClock = Stopwatch.StartNew();
        sketch.Distill();
        distillClock.Stop();
        var physicalCensus = sketch.Census(Scratch("census-physical"));
        var physical = State(sketch, physicalCensus);
        Check(physical.Records == full.Records - forgettable.Count, $"{physical.Records} records after Distill, expected {full.Records - forgettable.Count}");
        Check(physical.Defines == full.Defines, "Distill removed a Define record");
        Check(physicalCensus.HighestEntryId == head, "Distill removed the journal's last record");
        string physicalSnapshot = Snapshot(sketch, "physical");

        sketch.Dispose();
        sketch = SketchActor.Open(ActorName, primary, Figure.Width, Figure.Height);
        bool pictureEqual = string.Equals(sketch.RenderedPicture(), picture, StringComparison.Ordinal);
        bool countEqual = sketch.StrokeCount() == strokes;
        long headAfterReopen = sketch.CurrentEntryId;
        Check(pictureEqual, "the reopened picture differs from the session's picture");
        Check(countEqual, "the reopened stroke count differs");
        Check(headAfterReopen == head, "reopening moved the journal head");

        // ── 6, after the commit: the same moment, replayed from the distilled journal ──
        var nowThen = sketch.PictureAt(mid, Scratch("past-after"));
        Check(nowThen.Strokes.All(stroke => !onCanvasAtMid.Contains(stroke.Id)), "after Distill the picture of the past still shows a hesitation");
        bool predicted = string.Equals(nowThen.Rendered, pastPicture.WithElision, StringComparison.Ordinal);
        Check(predicted, "the past replayed after Distill differs from the picture the diff predicted");
        svgs["past-after-distill"] = PictureSvg.Render(nowThen.Rendered, SvgWidth, $"The canvas right after entry {mid}, after Distill");

        int signature = session.Acts.Max(act => act.Id) + 1;
        Check(sketch.Draw(signature, 1500, 1150, 1560, 1150, "graphite"), "the next command was refused");
        long next = sketch.CurrentEntryId;
        var afterNext = sketch.Census(Scratch("census-next"));
        bool ascending = afterNext.EntryIds.Zip(afterNext.EntryIds.Skip(1), (a, b) => a < b).All(ordered => ordered);
        bool isNew = !fullCensus.EntryIds.Contains(next);
        Check(next == head + 1, $"the next command journaled entry {next}, expected {head + 1}");
        Check(ascending && isNew, "an entry id was reused");
        sketch.Dispose();

        var commitNumbers = new CommitNumbers(pairsElided, Seconds(elideClock), Seconds(distillClock), pictureEqual, countEqual, head, headAfterReopen, next, ascending, isNew);
        log.Fact("pairs elided on the primary", $"{pairsElided} ({commitNumbers.ElideSeconds} s)");
        log.Fact("Distill", $"{commitNumbers.DistillSeconds} s");
        log.Fact("records full → logical → physical", $"{full.Records} → {logical.Records} → {physical.Records}");
        log.Fact("elision marks full → logical", $"{full.ElisionMarks} → {logical.ElisionMarks}");
        log.Fact("segment bytes full → logical → physical", $"{full.SegmentBytes:N0} → {logical.SegmentBytes:N0} → {physical.SegmentBytes:N0}");
        log.Fact("all bytes full → logical → physical", $"{full.TotalBytes:N0} → {logical.TotalBytes:N0} → {physical.TotalBytes:N0}");
        log.Fact("picture after reopen", pictureEqual ? "byte-equal" : "DIFFERENT");
        log.Fact("stroke count after reopen", countEqual ? $"{strokes} (equal)" : "DIFFERENT");
        log.Fact("head before / after reopen", $"{head} / {headAfterReopen}");
        log.Fact("next command's entry id", $"{next} (new, ids ascending: {ascending})");

        // ── cold rehydration of the three states ──────────────────────────
        var snapshots = new Dictionary<string, string> { ["full"] = fullSnapshot, ["logical"] = logicalSnapshot, ["physical"] = physicalSnapshot };
        var samples = States.ToDictionary(state => state, _ => new List<double>());
        ColdStart(physicalSnapshot, picture);
        for (int run = 0; run < options.Runs; run++)
        {
            foreach (string state in States)
            {
                samples[state].Add(ColdStart(snapshots[state], picture));
            }
        }

        var rehydration = new RehydrationNumbers(1, States.ToDictionary(
            state => state,
            state =>
            {
                var timing = new Timing(samples[state]);
                return new TimingNumbers(timing.MedianMs, timing.MinMs, timing.MaxMs, timing.SamplesMs);
            }));
        foreach (string state in States)
        {
            var timing = rehydration.States[state];
            log.Fact($"cold start, {state} (median of {options.Runs})", $"{timing.MedianMs:0.0} ms (min {timing.MinMs:0.0}, max {timing.MaxMs:0.0})");
        }

        // ── 6. What was given up ──────────────────────────────────────────
        log.Scene("6. What was given up");
        int upToMidBefore = fullCensus.EntryIds.Count(entry => entry <= mid);
        int upToMidAfter = physicalCensus.EntryIds.Count(entry => entry <= mid);
        var pastNumbers = new PastNumbers(
            mid,
            then.Strokes.Count,
            onCanvasAtMid,
            pastProof.IsSafe,
            pastProof.Changes.Select(change => change.Observation).ToArray(),
            forgottenPast.Strokes.Count,
            nowThen.Strokes.Count,
            predicted,
            upToMidBefore,
            upToMidAfter);
        log.Fact("mid-session entry", mid);
        log.Fact("strokes then, before Distill", $"{pastNumbers.StrokesAtMid}, of them hesitations: {string.Join(", ", onCanvasAtMid)}");
        log.Fact("forgetting, asked of that moment", $"{(pastProof.IsSafe ? "SAFE" : "UNSAFE")} — changed: {string.Join(", ", pastNumbers.ChangedObservationsAtMid)}");
        log.Fact("strokes then, as the diff predicted", pastNumbers.StrokesAtMidOnceForgotten);
        log.Fact("strokes then, after Distill", $"{pastNumbers.StrokesAtMidAfterDistill} (the predicted picture, byte-equal: {predicted})");
        log.Fact("records up to that entry: before / after Distill", $"{upToMidBefore} / {upToMidAfter}");

        total.Stop();
        var numbers = new LabNumbers(
            new RunFacts(DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), options.Seed, session.Survivors, session.Hesitations, options.Scale, options.Runs, Seconds(total)),
            engine,
            sessionNumbers,
            previewNumbers,
            proofNumbers,
            pastNumbers,
            commitNumbers,
            new Dictionary<string, JournalState> { ["full"] = full, ["logical"] = logical, ["physical"] = physical },
            rehydration);

        log.Line(string.Empty);
        log.Fact("total run time", $"{numbers.Run.TotalSeconds} s");
        WriteEvidence(numbers, rule);
    }

    // The engine's description of the registered rule, without its trailing list of the
    // last matches it saw (their bindings are data, not the rule).
    private static string RuleAsRegistered(string description) =>
        string.Join('\n', description.Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .TakeWhile(line => !line.StartsWith("lastMatches", StringComparison.Ordinal)));

    private double ColdStart(string snapshot, string expectedPicture)
    {
        string copy = Scratch("start");
        JournalFolder.Copy(snapshot, ActorName, copy);
        var (elapsed, rendered) = SketchActor.MeasureColdStart(ActorName, copy);
        Check(string.Equals(rendered, expectedPicture, StringComparison.Ordinal), "a cold start rehydrated a different picture");
        return Timing.Ms(elapsed);
    }

    private JournalState State(SketchActor sketch, JournalCensus? census = null)
    {
        census ??= sketch.Census(Scratch("census"));
        var footprint = sketch.Footprint();
        return new JournalState(census.Records, census.Scripts, census.Defines, census.Invocations, census.ElisionMarks, footprint.SegmentBytes, footprint.TotalBytes, footprint.Files);
    }

    private string Snapshot(SketchActor sketch, string name)
    {
        string directory = Path.Combine(work, "states", name);
        Directory.CreateDirectory(directory);
        sketch.CopyJournalTo(directory);
        return directory;
    }

    private string Scratch(string purpose)
    {
        string directory = Path.Combine(work, "scratch", $"{++scratchSequence:D3}-{purpose}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private void WriteEvidence(LabNumbers numbers, string ruleAsRegistered)
    {
        foreach (var (name, svg) in svgs)
        {
            File.WriteAllText(Path.Combine(output, name + ".svg"), svg, Encoding.UTF8);
        }

        var json = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(Path.Combine(output, "numbers.json"), JsonSerializer.Serialize(numbers, json), Encoding.UTF8);
        File.WriteAllText(Path.Combine(output, "report.html"), Report.Html(numbers, ruleAsRegistered, svgs), Encoding.UTF8);

        log.Line("   wrote report.html, numbers.json, run.log and " + string.Join(", ", svgs.Keys.Select(name => name + ".svg")));
        log.WriteTo(Path.Combine(output, "run.log"));
    }

    private static void Check(bool holds, string claim)
    {
        if (!holds) throw new LabCheckFailed(claim);
    }

    private static double Seconds(Stopwatch clock) => Math.Round(clock.Elapsed.TotalSeconds, 2);

    private static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
