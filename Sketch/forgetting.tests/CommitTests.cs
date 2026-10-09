using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sketch.Acting;

namespace Sketch.Forgetting.Tests;

// Every test here Distills before it reopens: releasing a journal and reopening it
// in the same process is exercised, but a Distill always runs on the instance that
// wrote the journal.
[TestClass]
public class CommitTests
{
    private const string Actor = "canvas";

    [TestMethod]
    public void Eliding_MarksThePairs_ButKeepsEveryRecordOnDisk()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        var session = ScriptedSession.Standard(sketch);
        var before = sketch.Census(temp.NewDirectory("census-full"));

        long pairs = sketch.ElideErasedStrokes();

        var logical = sketch.Census(temp.NewDirectory("census-logical"));
        Assert.AreEqual(session.Pairs, pairs);
        Assert.AreEqual(before.Records, logical.Records);
        Assert.AreEqual(session.Forgettable.Count, logical.ElisionMarks);
    }

    [TestMethod]
    public void CommitThenReopen_KeepsThePictureByteEqual_AndRemovesExactlyTheErasedPairs()
    {
        using var temp = new TempJournal();
        string picture;
        int strokes;
        JournalCensus full;
        int forgettable;
        using (var sketch = SketchActor.Open(Actor, temp.Root, 320, 200))
        {
            var session = ScriptedSession.Standard(sketch);
            forgettable = session.Forgettable.Count;
            picture = sketch.RenderedPicture();
            strokes = sketch.StrokeCount();
            full = sketch.Census(temp.NewDirectory("census-full"));

            sketch.ElideErasedStrokes();
            sketch.Distill();
        }

        using var reopened = SketchActor.Open(Actor, temp.Root, 320, 200);
        var physical = reopened.Census(temp.NewDirectory("census-physical"));

        Assert.AreEqual(picture, reopened.RenderedPicture());
        Assert.AreEqual(strokes, reopened.StrokeCount());
        Assert.AreEqual(full.Records - forgettable, physical.Records);
        Assert.AreEqual(full.Defines, physical.Defines, "Define rows are separate records and survive their invocations' elision.");
        Assert.AreEqual(full.HighestEntryId, physical.HighestEntryId);
    }

    [TestMethod]
    public void Distill_KeepsTheJournalsLastRecord_EvenWhenItIsAnElidedErase()
    {
        using var temp = new TempJournal();
        string picture;
        int forgettable;
        JournalCensus full;
        long lastEntry;
        using (var sketch = SketchActor.Open(Actor, temp.Root, 320, 200))
        {
            var session = new ScriptedSession(sketch);
            session.Survivor(1, 10, 10, 300, 10, "graphite");
            session.Hesitation(100);
            session.Survivor(2, 300, 10, 300, 190, "indigo");
            session.Hesitation(101);
            lastEntry = sketch.CurrentEntryId;
            forgettable = session.Forgettable.Count;
            picture = sketch.RenderedPicture();
            full = sketch.Census(temp.NewDirectory("census-full"));

            sketch.ElideErasedStrokes();
            sketch.Distill();
        }

        using var reopened = SketchActor.Open(Actor, temp.Root, 320, 200);
        var physical = reopened.Census(temp.NewDirectory("census-physical"));

        Assert.AreEqual(full.Records - forgettable + 1, physical.Records);
        CollectionAssert.Contains(physical.EntryIds.ToArray(), lastEntry);
        Assert.AreEqual(picture, reopened.RenderedPicture(), "The kept last record is still elided, so rehydration skips it.");
    }

    [TestMethod]
    public void AfterCommitAndReopen_TheNextCommandContinuesTheEntryIds_AndNoIdIsReused()
    {
        using var temp = new TempJournal();
        JournalCensus full;
        long head;
        using (var sketch = SketchActor.Open(Actor, temp.Root, 320, 200))
        {
            ScriptedSession.Standard(sketch);
            head = sketch.CurrentEntryId;
            full = sketch.Census(temp.NewDirectory("census-full"));
            sketch.ElideErasedStrokes();
            sketch.Distill();
        }

        using var reopened = SketchActor.Open(Actor, temp.Root, 320, 200);
        Assert.AreEqual(head, reopened.CurrentEntryId);

        Assert.IsTrue(reopened.Draw(4, 10, 190, 10, 10, "viridian"));

        Assert.AreEqual(head + 1, reopened.CurrentEntryId);
        var after = reopened.Census(temp.NewDirectory("census-after"));
        CollectionAssert.AllItemsAreUnique(after.EntryIds.ToArray());
        CollectionAssert.AreEqual(after.EntryIds.OrderBy(id => id).ToArray(), after.EntryIds.ToArray());
        CollectionAssert.DoesNotContain(full.EntryIds.ToArray(), reopened.CurrentEntryId);
    }

    [TestMethod]
    public void RunningTheRuleTwice_ElidesNothingMore()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        var session = ScriptedSession.Standard(sketch);

        Assert.AreEqual(session.Pairs, sketch.ElideErasedStrokes());
        var once = sketch.Census(temp.NewDirectory("census-once"));
        Assert.AreEqual(0, sketch.ElideErasedStrokes());
        var twice = sketch.Census(temp.NewDirectory("census-twice"));

        Assert.AreEqual(once.ElisionMarks, twice.ElisionMarks);
        Assert.AreEqual(once.Records, twice.Records);
    }

    [TestMethod]
    public void DistillingTwice_IsHarmless()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        ScriptedSession.Standard(sketch);
        string picture = sketch.RenderedPicture();
        sketch.ElideErasedStrokes();

        sketch.Distill();
        var once = sketch.Census(temp.NewDirectory("census-once"));
        sketch.Distill();
        var twice = sketch.Census(temp.NewDirectory("census-twice"));

        CollectionAssert.AreEqual(once.EntryIds.ToArray(), twice.EntryIds.ToArray());
        Assert.AreEqual(picture, sketch.RenderedPicture());
    }

    [TestMethod]
    public void EveryJournalState_RehydratesColdToTheSamePicture()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        ScriptedSession.Standard(sketch);
        string picture = sketch.RenderedPicture();

        string full = temp.NewDirectory("full");
        sketch.CopyJournalTo(full);
        sketch.ElideErasedStrokes();
        string logical = temp.NewDirectory("logical");
        sketch.CopyJournalTo(logical);
        sketch.Distill();
        string physical = temp.NewDirectory("physical");
        sketch.CopyJournalTo(physical);

        foreach (string state in new[] { full, logical, physical })
        {
            var (_, rendered) = SketchActor.MeasureColdStart(Actor, state);
            Assert.AreEqual(picture, rendered, state);
        }
    }

    [TestMethod]
    public void Distill_ShrinksTheRecordSegments()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        var session = new ScriptedSession(sketch);
        session.Survivor(1, 10, 10, 300, 10, "graphite");
        for (int id = 100; id < 150; id++) session.Hesitation(id);
        session.Survivor(2, 300, 10, 300, 190, "indigo");
        long before = sketch.Footprint().SegmentBytes;

        sketch.ElideErasedStrokes();
        sketch.Distill();

        Assert.IsTrue(sketch.Footprint().SegmentBytes < before);
    }
}
