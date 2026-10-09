using System.Collections.Generic;
using System.Linq;
using Choreography.Theater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puppeteer;
using Puppeteer.EventSourcing.DB;
using Puppeteer.EventSourcing.Follower;
using Sketch.Acting;

namespace Sketch.Forgetting.Tests;

/// <summary>
/// The calls the README's "Start here" shows, with no facade in between: a
/// <see cref="PerformanceV2"/> hosts the canvas, a session of three strokes erases the
/// middle one, and the rule is previewed, proved, run and distilled on the instance that
/// wrote the journal. The block between the markers is the README's code as written.
/// </summary>
[TestClass]
public class StartHereTests
{
    private const string Actor = "canvas";

    private const string DrawCheck = "{ Check(canvas.HasStroke(@id) == false) WARNING 'stroke already on the canvas'; }";
    private const string DrawCommand = "canvas.Draw(@id, @fromX, @fromY, @toX, @toY, @ink);";
    private const string EraseCheck = "{ Check(canvas.HasStroke(@id) == true) WARNING 'no such stroke on the canvas'; }";
    private const string EraseCommand = "canvas.Erase(@id);";

    [TestMethod]
    public void TheStartHereCalls_ForgetTheErasedStroke_AndKeepThePicture()
    {
        using var temp = new TempJournal();
        var performance = new PerformanceV2(Actor, typeof(SketchDomain).Assembly)
            .ConfigureStorage(DatabaseType.FileSystem, $"path={temp.Root};maxFileSize=4194304")
            .Start();

        // Entries 1-2 seed the canvas, 3 defines Draw and 4 draws stroke 1, 5 draws
        // stroke 2, 6 defines Erase and 7 erases stroke 2, and 8 draws stroke 3.
        performance.Using("upgrade('seed') { canvas = Canvas(@width, @height); }")
            .WithParameters(p =>
            {
                p["width", typeof(int)] = 320;
                p["height", typeof(int)] = 200;
            })
            .PerformCommand();
        Draw(performance, 1, 10, 10, 300, 10, "graphite");
        Draw(performance, 2, 5, 5, 9, 9, "vermilion");
        Erase(performance, 2);
        Draw(performance, 3, 300, 10, 300, 190, "indigo");
        string picture = performance.Using(SketchActor.PictureQuery).PerformQuery();

        // ── The README's code, as written ──────────────────────────────────

        // The rule: a stroke drawn, then the same stroke erased. Elide both acts.
        static void DefineForgetting(Reactions reactions) =>
            reactions.DefineReaction("ForgetErasedStrokes")
                .Job().Company().WithSharedHydration()
                .Seek("Drawn")
                    .OnMatch("[_:Canvas].Draw($id, _, _, _, _, _)").One()
                .ThenSeek("Erased")
                    .OnMatch("[_:Canvas].Erase($id)").One()
                .Metadata.Elide();

        // 1. Preview: a shadow replays the journal into storage of its own and lists
        //    the entries the rule would elide. Nothing is elided anywhere.
        IReadOnlyList<long> wouldElide;
        using (Shadow preview = performance.Actor.Shadow(new ShadowConfig(
            "preview", DatabaseType.IN_MEMORY, "memory",
            configureReactions: actor => DefineForgetting(actor.Reactions))))
        {
            preview.EnableSkipPreview();
            preview.SyncUntil(performance.CurrentEntryId);
            preview.Reactions.Execute();
            wouldElide = preview.Reactions["ForgetErasedStrokes"].WouldSkip;
        }

        // 2. Proof: another shadow rehydrates twice, as the journal is and with those
        //    entries skipped, and compares the answers to the questions it is given.
        bool safe;
        using (Shadow proof = performance.Actor.Shadow(new ShadowConfig(
            "proof", DatabaseType.IN_MEMORY, "memory")))
        {
            proof.SyncUntil(performance.CurrentEntryId);
            safe = proof.ElisionImpactDiff(wouldElide.ToArray(),
                SketchActor.PictureQuery, SketchActor.StrokeCountQuery).IsSafe;
        }

        if (safe)
        {
            // 3. Elide: the rule runs on the primary and marks the pairs. Every record
            //    stays on disk; rehydration steps over the marked ones.
            DefineForgetting(performance.Actor.Reactions);
            performance.Actor.Reactions.Execute();

            // 4. Distill: the marked records leave the journal.
            performance.Distill();
        }

        // ── End of the README's code ───────────────────────────────────────

        CollectionAssert.AreEqual(new long[] { 5, 7 }, wouldElide.ToArray(), "the Draw and the Erase of stroke 2");
        Assert.IsTrue(safe);
        var census = SketchActor.CensusOf(Actor, temp.Root, temp.NewDirectory("census"));
        CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 6, 8 }, census.EntryIds.ToArray(), "the seed, the Define records and the strokes that stay");

        performance.Actor.GracefulExit();
        performance.Dispose();

        var (_, reopened) = SketchActor.MeasureColdStart(Actor, temp.Root);
        Assert.AreEqual(picture, reopened);
    }

    private static void Draw(PerformanceV2 performance, int id, int fromX, int fromY, int toX, int toY, string ink) =>
        performance.Using(DrawCheck, DrawCommand)
            .WithParameters(p =>
            {
                p["id", typeof(int)] = id;
                p["fromX", typeof(int)] = fromX;
                p["fromY", typeof(int)] = fromY;
                p["toX", typeof(int)] = toX;
                p["toY", typeof(int)] = toY;
                p["ink", typeof(string)] = ink;
            })
            .PerformCheckThenCommand();

    private static void Erase(PerformanceV2 performance, int id) =>
        performance.Using(EraseCheck, EraseCommand)
            .WithParameters(p => p["id", typeof(int)] = id)
            .PerformCheckThenCommand();
}
