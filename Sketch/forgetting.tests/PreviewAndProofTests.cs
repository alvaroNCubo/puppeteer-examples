using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sketch.Acting;

namespace Sketch.Forgetting.Tests;

[TestClass]
public class PreviewAndProofTests
{
    private const string Actor = "canvas";

    [TestMethod]
    public void Census_CountsEveryRecordOfTheJournal_ByKind()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        ScriptedSession.Standard(sketch);

        var census = sketch.Census(temp.NewDirectory("census"));

        // Seed Define + Invocation, then Draw's and Erase's Define rows, then one
        // invocation per act: 3 survivors + 3 hesitation pairs.
        Assert.AreEqual(3, census.Defines);
        Assert.AreEqual(0, census.Scripts);
        Assert.AreEqual(1 + 3 + 6, census.Invocations);
        Assert.AreEqual(13, census.Records);
        CollectionAssert.AreEqual(Enumerable.Range(1, 13).Select(i => (long)i).ToArray(), census.EntryIds.ToArray());
        Assert.AreEqual(0, census.ElisionMarks);
    }

    [TestMethod]
    public void Preview_ListsExactlyTheErasedPairs_AndLeavesThePrimaryUntouched()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        var session = ScriptedSession.Standard(sketch);
        long head = sketch.CurrentEntryId;
        string picture = sketch.RenderedPicture();
        var footprint = sketch.Footprint();
        var census = sketch.Census(temp.NewDirectory("census-before"));

        var preview = sketch.Preview();

        CollectionAssert.AreEqual(session.Forgettable.ToArray(), preview.WouldElide.ToArray());
        Assert.AreEqual(session.Pairs, preview.Pairs);
        Assert.AreEqual(head, sketch.CurrentEntryId);
        Assert.AreEqual(picture, sketch.RenderedPicture());
        Assert.AreEqual(footprint, sketch.Footprint());
        var after = sketch.Census(temp.NewDirectory("census-after"));
        Assert.AreEqual(census.Records, after.Records);
        Assert.AreEqual(0, after.ElisionMarks);
    }

    [TestMethod]
    public void Proof_OfTheRulesPairs_IsSafe_ForThePictureAndTheStrokeCount()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        ScriptedSession.Standard(sketch);

        var proof = sketch.Prove(sketch.Preview().WouldElide);

        Assert.IsTrue(proof.IsSafe);
        Assert.AreEqual(0, proof.Changes.Count);
    }

    [TestMethod]
    public void Proof_OfAnEraseWithoutItsDraw_IsUnsafe_AndNamesThePicture()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        var session = new ScriptedSession(sketch);
        session.Survivor(1, 10, 10, 300, 10, "graphite");
        session.Tentative(100);
        long erase = session.EraseTentative(100);
        session.Survivor(2, 300, 10, 300, 190, "indigo");

        var proof = sketch.Prove(new[] { erase });

        Assert.IsFalse(proof.IsSafe);
        var picture = proof.Changes.Single(change => change.Observation == "picture");
        CollectionAssert.AreEqual(new[] { 1, 2 }, PictureSnapshot.Parse(picture.WithoutElision).Strokes.Select(s => s.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 100, 2 }, PictureSnapshot.Parse(picture.WithElision).Strokes.Select(s => s.Id).ToArray());
        var count = proof.Changes.Single(change => change.Observation == "stroke count");
        Assert.AreEqual("{\"strokes\":2}", count.WithoutElision);
        Assert.AreEqual("{\"strokes\":3}", count.WithElision);
    }

    [TestMethod]
    public void Proof_WithoutElision_ReproducesThePrimarysPictureByteForByte()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        var session = ScriptedSession.Standard(sketch);

        var proof = sketch.Prove(new[] { session.Forgettable[^1] });

        var picture = proof.Changes.Single(change => change.Observation == "picture");
        Assert.AreEqual(sketch.RenderedPicture(), picture.WithoutElision);
    }
}
