using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sketch.Tests;

[TestClass]
public class StackingTests
{
    [TestMethod]
    public void Picture_ListsStrokesBottomToTop_InTheOrderTheyWereDrawn()
    {
        var canvas = new Canvas(100, 100);
        canvas.Draw(30, 0, 0, 1, 1, "graphite");
        canvas.Draw(10, 0, 0, 1, 1, "indigo");
        canvas.Draw(20, 0, 0, 1, 1, "ochre");

        CollectionAssert.AreEqual(new[] { 30, 10, 20 }, PictureReading.Ids(canvas));
    }

    [TestMethod]
    public void Erase_OfAMiddleStroke_KeepsTheOthersInTheirOrder()
    {
        var canvas = new Canvas(100, 100);
        canvas.Draw(1, 0, 0, 1, 1, "graphite");
        canvas.Draw(2, 0, 0, 1, 1, "graphite");
        canvas.Draw(3, 0, 0, 1, 1, "graphite");
        canvas.Draw(4, 0, 0, 1, 1, "graphite");

        canvas.Erase(2);

        CollectionAssert.AreEqual(new[] { 1, 3, 4 }, PictureReading.Ids(canvas));
    }

    [TestMethod]
    public void RedrawingAnErasedId_LaysTheNewStrokeOnTop()
    {
        var canvas = new Canvas(100, 100);
        canvas.Draw(1, 0, 0, 1, 1, "graphite");
        canvas.Draw(2, 0, 0, 1, 1, "graphite");
        canvas.Erase(1);

        canvas.Draw(1, 5, 5, 6, 6, "vermilion");

        CollectionAssert.AreEqual(new[] { "2: (0,0)-(1,1) graphite", "1: (5,5)-(6,6) vermilion" },
            PictureReading.Lines(canvas));
    }

    [TestMethod]
    public void TheSameStrokes_ReachedByDifferentHistories_MakeTheSamePicture()
    {
        var direct = new Canvas(100, 100);
        direct.Draw(1, 10, 10, 90, 10, "graphite");
        direct.Draw(2, 90, 10, 90, 90, "indigo");
        direct.Draw(3, 90, 90, 10, 90, "ochre");
        direct.Draw(4, 10, 90, 10, 10, "viridian");

        var hesitant = new Canvas(100, 100);
        hesitant.Draw(1, 10, 10, 90, 10, "graphite");
        hesitant.Draw(101, 20, 20, 25, 25, "vermilion");
        hesitant.Draw(102, 30, 30, 35, 35, "vermilion");
        hesitant.Erase(101);
        hesitant.Draw(2, 90, 10, 90, 90, "indigo");
        hesitant.Draw(103, 40, 40, 45, 45, "graphite");
        hesitant.Erase(102);
        hesitant.Draw(3, 90, 90, 10, 90, "ochre");
        hesitant.Erase(103);
        hesitant.Draw(104, 50, 50, 55, 55, "indigo");
        hesitant.Draw(105, 60, 60, 65, 65, "indigo");
        hesitant.Erase(104);
        hesitant.Draw(4, 10, 90, 10, 10, "viridian");
        hesitant.Erase(105);

        CollectionAssert.AreEqual(PictureReading.Lines(direct), PictureReading.Lines(hesitant));
        Assert.AreEqual(direct.StrokeCount, hesitant.StrokeCount);
    }

    [TestMethod]
    public void ACanvasThatDrewAndErasedMany_BehavesLikeANewCanvas()
    {
        var weathered = new Canvas(100, 100);
        for (int id = 1; id <= 1000; id++)
        {
            weathered.Draw(id, id % 100, 0, id % 100, 100, "graphite");
            weathered.Erase(id);
        }

        var fresh = new Canvas(100, 100);
        weathered.Draw(5000, 1, 2, 3, 4, "ochre");
        fresh.Draw(5000, 1, 2, 3, 4, "ochre");

        CollectionAssert.AreEqual(PictureReading.Lines(fresh), PictureReading.Lines(weathered));
        Assert.AreEqual(fresh.StrokeCount, weathered.StrokeCount);
    }

    [TestMethod]
    public void StrokesMayCrossAndOverlap_NoStrokeConstrainsAnother()
    {
        var canvas = new Canvas(100, 100);
        canvas.Draw(1, 0, 0, 100, 100, "graphite");
        canvas.Draw(2, 0, 100, 100, 0, "indigo");
        canvas.Draw(3, 0, 0, 100, 100, "ochre");

        Assert.AreEqual(3, canvas.StrokeCount);
    }
}
