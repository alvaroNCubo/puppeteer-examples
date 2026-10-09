using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sketch.Acting;

namespace Sketch.Forgetting.Tests;

[TestClass]
public class CanvasActorTests
{
    private const string Actor = "canvas";

    [TestMethod]
    public void ANewJournal_IsSeededWithAnEmptyCanvas()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);

        var picture = sketch.Picture();

        Assert.AreEqual(320, picture.Width);
        Assert.AreEqual(200, picture.Height);
        Assert.AreEqual(0, picture.Strokes.Count);
        Assert.AreEqual(0, sketch.StrokeCount());
    }

    [TestMethod]
    public void Draw_PutsAStrokeInThePicture_AndErase_TakesItOut()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);

        Assert.IsTrue(sketch.Draw(1, 10, 20, 30, 40, "indigo"));
        Assert.IsTrue(sketch.Draw(2, 0, 0, 320, 200, "ochre"));
        Assert.IsTrue(sketch.Erase(1));

        var picture = sketch.Picture();
        Assert.AreEqual(1, picture.Strokes.Count);
        Assert.AreEqual(new StrokeView(2, 0, 0, 320, 200, "ochre"), picture.Strokes[0]);
        Assert.AreEqual(1, sketch.StrokeCount());
        Assert.IsTrue(sketch.HasStroke(2));
        Assert.IsFalse(sketch.HasStroke(1));
    }

    [TestMethod]
    public void Draw_OfAnIdAlreadyOnTheCanvas_IsRefusedByTheCheck_AndJournalsNothing()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        sketch.Draw(1, 10, 20, 30, 40, "indigo");
        long head = sketch.CurrentEntryId;

        Assert.IsFalse(sketch.Draw(1, 50, 50, 60, 60, "ochre"));

        Assert.AreEqual(head, sketch.CurrentEntryId);
        Assert.AreEqual(new StrokeView(1, 10, 20, 30, 40, "indigo"), sketch.Picture().Strokes[0]);
    }

    [TestMethod]
    public void Erase_OfAStrokeNotOnTheCanvas_IsRefusedByTheCheck_AndJournalsNothing()
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        sketch.Draw(1, 10, 20, 30, 40, "indigo");
        long head = sketch.CurrentEntryId;

        Assert.IsFalse(sketch.Erase(9));

        Assert.AreEqual(head, sketch.CurrentEntryId);
        Assert.AreEqual(1, sketch.StrokeCount());
    }

    [TestMethod]
    public void Reopening_RehydratesTheSamePicture_AndJournalsNoSecondSeed()
    {
        using var temp = new TempJournal();
        string rendered;
        long head;
        using (var sketch = SketchActor.Open(Actor, temp.Root, 320, 200))
        {
            sketch.Draw(1, 10, 20, 30, 40, "indigo");
            sketch.Draw(2, 0, 0, 5, 5, "vermilion");
            rendered = sketch.RenderedPicture();
            head = sketch.CurrentEntryId;
        }

        using var reopened = SketchActor.Open(Actor, temp.Root, 999, 999);

        Assert.AreEqual(rendered, reopened.RenderedPicture());
        Assert.AreEqual(head, reopened.CurrentEntryId);
    }
}
