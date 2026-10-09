using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;

namespace Sketch.Forgetting;

/// <summary>Writes one run's evidence as a self-contained HTML page with the pictures inline.</summary>
internal static class Report
{
    internal static string Html(LabNumbers n, string ruleAsRegistered, IReadOnlyDictionary<string, string> svgs)
    {
        var html = new StringBuilder();
        html.Append("""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Forgetting on Purpose</title>
            <style>
              :root { --ink:#222; --muted:#666; --line:#ddd; --paper:#fff; --accent:#8a5a00; }
              body { margin:0; background:var(--paper); color:var(--ink); font:16px/1.5 system-ui, sans-serif; }
              main { max-width:1360px; margin:0 auto; padding:24px 16px 64px; }
              h1 { margin:0 0 4px; } h2 { margin:40px 0 8px; border-bottom:1px solid var(--line); padding-bottom:4px; }
              .question { font-size:1.2em; color:var(--accent); margin:0 0 16px; }
              .muted { color:var(--muted); }
              table { border-collapse:collapse; margin:8px 0; }
              th, td { border:1px solid var(--line); padding:4px 10px; text-align:right; }
              th:first-child, td:first-child { text-align:left; }
              .pair { display:flex; flex-wrap:wrap; gap:16px; }
              figure { margin:0; } figure svg { max-width:100%; height:auto; } figcaption { color:var(--muted); font-size:.9em; }
              pre { background:#f6f6f6; padding:8px 12px; overflow-x:auto; font-size:.85em; }
              .verdict { font-weight:600; }
            </style></head><body><main>
            <h1>Forgetting on Purpose</h1>
            <p class="question">The journal keeps everything. Can part of it be removed, with proof that nothing anyone can see has changed?</p>
            """);

        html.Append(Paragraph($"Run of {n.Run.DateUtc} (UTC): seed {n.Run.Seed}, {n.Run.Survivors} surviving strokes, {n.Run.Hesitations} hesitations (scale {n.Run.Scale}), {n.Run.Runs} timed cold starts per journal state. " +
            $"Engine commit {n.Engine.Commit}, {n.Engine.Build} build. The whole run took {Number(n.Run.TotalSeconds)} s.", "muted"));

        html.Append(Heading("1. The session"));
        html.Append(Paragraph($"A scripted session of {n.Session.Acts:N0} acts ({n.Session.Draws:N0} draws, {n.Session.Erases:N0} erases) on a FileSystem journal. " +
            $"Every hesitation was erased at most {n.Session.MaxPairDistanceEntries} entries after it was drawn. The canvas ends with {n.Session.StrokesOnCanvas:N0} strokes."));
        html.Append(Figure(svgs["picture"], $"The picture (SHA-256 of its printed text: {n.Session.PictureSha256[..16]}…)"));

        html.Append(Heading("2. The rule"));
        html.Append(Paragraph("One reaction pairs a Draw($id, …) with a later Erase($id) of the same id and elides the pair. As the engine registered it:"));
        html.Append("<pre>").Append(WebUtility.HtmlEncode(ruleAsRegistered.Trim())).Append("</pre>");

        html.Append(Heading("3. The preview"));
        html.Append(Paragraph($"On a shadow in skip-preview mode the rule would elide {n.Preview.WouldElide:N0} entries: {n.Preview.Pairs:N0} drawn-then-erased pairs. " +
            $"The primary's head, records, bytes and picture were compared before and after: {(n.Preview.PrimaryUntouched ? "unchanged" : "CHANGED")}. ({Number(n.Preview.Seconds)} s)"));

        html.Append(Heading("4. The proof"));
        html.Append(Paragraph($"Eliding the rule's entries, compared by the elision-impact diff over the picture and the stroke count: " +
            $"<span class=\"verdict\">{(n.Proof.PairsAreSafe ? "safe" : "UNSAFE")}</span>. " +
            $"Eliding every pair except the Draw of stroke {n.Proof.ResurrectedStroke} (its Erase elided, its Draw kept): " +
            $"<span class=\"verdict\">{(n.Proof.LoneEraseIsSafe ? "safe" : "unsafe")}</span>, changing {string.Join(" and ", n.Proof.LoneEraseChangedObservations)}.", encode: false));
        html.Append("<div class=\"pair\">");
        html.Append(Figure(svgs["unsafe-without"], "Without elision (the diff's WithoutElision text)"));
        html.Append(Figure(svgs["unsafe-with"], $"With that candidate set elided (WithElision): stroke {n.Proof.ResurrectedStroke}, highlighted, comes back"));
        html.Append("</div>");

        html.Append(Heading("5. The commit"));
        html.Append(Paragraph($"The rule ran on the primary ({n.Commit.PairsElided:N0} pairs, {Number(n.Commit.ElideSeconds)} s), then Distill ({Number(n.Commit.DistillSeconds)} s). " +
            $"After releasing the journal and reopening it from disk the picture was {(n.Commit.PictureByteEqualAfterReopen ? "byte-equal" : "DIFFERENT")} and the stroke count {(n.Commit.StrokeCountEqualAfterReopen ? "equal" : "DIFFERENT")}; " +
            $"the head stayed at {n.Commit.HeadAfterReopen:N0} and the next command journaled entry {n.Commit.NextEntryId:N0} ({(n.Commit.NextEntryIdIsNew && n.Commit.EntryIdsUniqueAndAscending ? "a new id; no id reused" : "AN ID WAS REUSED")})."));
        html.Append("<table><tr><th>Journal state</th><th>Records</th><th>Elision marks</th><th>Segment bytes</th><th>All bytes</th><th>Cold start, median</th><th>min – max</th></tr>");
        foreach (string state in new[] { "full", "logical", "physical" })
        {
            var journal = n.Journal[state];
            var timing = n.Rehydration.States[state];
            html.Append(CultureInfo.InvariantCulture,
                $"<tr><td>{state}</td><td>{journal.Records:N0}</td><td>{journal.ElisionMarks:N0}</td><td>{journal.SegmentBytes:N0}</td><td>{journal.TotalBytes:N0}</td><td>{timing.MedianMs:0.0} ms</td><td>{timing.MinMs:0.0} – {timing.MaxMs:0.0} ms</td></tr>");
        }

        html.Append("</table>");
        html.Append(Paragraph($"Cold start: a new actor opens a copy of the journal state and rehydrates it; median of {n.Run.Runs} starts per state, interleaved, after {n.Rehydration.WarmUpStarts} uncounted warm-up start. " +
            "Segment bytes are the record files (journal/journal_*.bin); all bytes include the index, elision marks, skip set, reaction registry and checkpoints, and meta.", "muted"));

        html.Append(Heading("6. What was given up"));
        html.Append(Paragraph($"Before Distill, a shadow synced to entry {n.Past.MidSessionEntry:N0} shows the canvas as it stood then: {n.Past.StrokesAtMid:N0} strokes, {n.Past.HesitationsAtMid.Count} of them hesitations not yet erased (highlighted). " +
            $"After Distill, a shadow synced to the same entry shows {n.Past.StrokesAtMidAfterDistill:N0} strokes and none of those hesitations. " +
            $"Asked of that moment before the commit, the diff had called the same forgetting <span class=\"verdict\">{(n.Past.ForgettingIsSafeForThePast ? "safe" : "unsafe")}</span> (it changes {string.Join(" and ", n.Past.ChangedObservationsAtMid)}), " +
            $"and the picture it predicted is {(n.Past.AfterDistillMatchesPrediction ? "byte-equal to" : "DIFFERENT from")} the one the shadow now replays. " +
            $"The journal held {n.Past.RecordsUpToMidBeforeDistill:N0} records up to that entry before Distill and {n.Past.RecordsUpToMidAfterDistill:N0} after it.", encode: false));
        html.Append("<div class=\"pair\">");
        html.Append(Figure(svgs["past-then"], "Before Distill: a shadow at that entry"));
        html.Append(Figure(svgs["past-after-distill"], "After Distill: a shadow at the same entry"));
        html.Append("</div>");

        html.Append(Heading("What this does not establish"));
        html.Append("<ul>");
        html.Append("<li>“Safe” is relative to the observations passed to the diff (the picture and the stroke count). A question outside that list can change; scene 6 shows one.</li>");
        html.Append("<li>The developer writes the rule. The engine does not work out what is safe to forget.</li>");
        html.Append("<li>It is not a personal-data erasure mechanism.</li>");
        html.Append("<li>The timings come from one machine and one session size. This is not a performance evaluation.</li>");
        html.Append("</ul></main></body></html>");
        return html.ToString();
    }

    private static string Heading(string text) => "<h2>" + WebUtility.HtmlEncode(text) + "</h2>";

    private static string Paragraph(string text, string? cssClass = null, bool encode = true) =>
        (cssClass is null ? "<p>" : $"<p class=\"{cssClass}\">") + (encode ? WebUtility.HtmlEncode(text) : text) + "</p>";

    private static string Figure(string svg, string caption) =>
        "<figure>" + svg + "<figcaption>" + WebUtility.HtmlEncode(caption) + "</figcaption></figure>";

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
