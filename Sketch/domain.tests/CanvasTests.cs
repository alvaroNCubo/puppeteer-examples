using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sketch.Tests;

[TestClass]
public class CanvasTests
{
    private Canvas canvas = null!;

    [TestInitialize]
    public void OpenCanvas() => canvas = new Canvas(200, 100);

    [TestMethod]
    public void NewCanvas_IsEmpty()
    {
        Assert.AreEqual(0, canvas.StrokeCount);
        Assert.AreEqual(0, canvas.Picture().Count);
        Assert.AreEqual(200, canvas.Extent.Width);
        Assert.AreEqual(100, canvas.Extent.Height);
    }

    [DataTestMethod]
    [DataRow(0, 100)]
    [DataRow(200, 0)]
    [DataRow(-1, 100)]
    [DataRow(200, -5)]
    public void Canvas_WithoutPositiveSize_IsRefused(int width, int height) =>
        Assert.ThrowsException<SketchRuleException>(() => new Canvas(width, height));

    [TestMethod]
    public void Draw_PutsTheStrokeOnTheCanvas()
    {
        canvas.Draw(7, 10, 20, 30, 40, "indigo");

        Assert.AreEqual(1, canvas.StrokeCount);
        Assert.IsTrue(canvas.HasStroke(7));
        CollectionAssert.AreEqual(new[] { "7: (10,20)-(30,40) indigo" }, PictureReading.Lines(canvas));
    }

    [TestMethod]
    public void Draw_WithAnIdAlreadyOnTheCanvas_IsRefused_AndLeavesTheCanvasAsItWas()
    {
        canvas.Draw(1, 0, 0, 10, 10, "graphite");

        Assert.ThrowsException<SketchRuleException>(() => canvas.Draw(1, 50, 50, 60, 60, "ochre"));

        CollectionAssert.AreEqual(new[] { "1: (0,0)-(10,10) graphite" }, PictureReading.Lines(canvas));
    }

    [DataTestMethod]
    [DataRow(-1, 0, 10, 10)]
    [DataRow(0, -1, 10, 10)]
    [DataRow(0, 0, 201, 10)]
    [DataRow(0, 0, 10, 101)]
    [DataRow(250, 50, 10, 10)]
    public void Draw_WithAPointOffTheCanvas_IsRefused_AndLeavesTheCanvasEmpty(int fromX, int fromY, int toX, int toY)
    {
        Assert.ThrowsException<SketchRuleException>(() => canvas.Draw(1, fromX, fromY, toX, toY, "graphite"));

        Assert.AreEqual(0, canvas.StrokeCount);
        Assert.IsFalse(canvas.HasStroke(1));
    }

    [TestMethod]
    public void Draw_OnTheEdges_IsAccepted_BecauseTheEdgesBelongToTheCanvas()
    {
        canvas.Draw(1, 0, 0, 200, 100, "graphite");
        canvas.Draw(2, 200, 0, 0, 100, "graphite");

        Assert.AreEqual(2, canvas.StrokeCount);
    }

    [TestMethod]
    public void Draw_WithAnInkOutsideThePalette_IsRefused_AndLeavesTheCanvasEmpty()
    {
        Assert.ThrowsException<SketchRuleException>(() => canvas.Draw(1, 0, 0, 10, 10, "gold"));

        Assert.AreEqual(0, canvas.StrokeCount);
    }

    [TestMethod]
    public void Draw_ADot_IsAMark()
    {
        canvas.Draw(1, 5, 5, 5, 5, "vermilion");

        CollectionAssert.AreEqual(new[] { "1: (5,5)-(5,5) vermilion" }, PictureReading.Lines(canvas));
    }

    [TestMethod]
    public void Erase_LiftsTheStrokeOff()
    {
        canvas.Draw(1, 0, 0, 10, 10, "graphite");

        canvas.Erase(1);

        Assert.AreEqual(0, canvas.StrokeCount);
        Assert.IsFalse(canvas.HasStroke(1));
    }

    [TestMethod]
    public void Erase_OfAStrokeNotOnTheCanvas_IsRefused_AndLeavesTheCanvasAsItWas()
    {
        canvas.Draw(1, 0, 0, 10, 10, "graphite");

        Assert.ThrowsException<SketchRuleException>(() => canvas.Erase(2));

        CollectionAssert.AreEqual(new[] { 1 }, PictureReading.Ids(canvas));
    }

    [TestMethod]
    public void Erase_OfAnAlreadyErasedStroke_IsRefused()
    {
        canvas.Draw(1, 0, 0, 10, 10, "graphite");
        canvas.Erase(1);

        Assert.ThrowsException<SketchRuleException>(() => canvas.Erase(1));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    [DataRow(int.MaxValue)]
    public void HasStroke_AnswersAnyId(int id) =>
        Assert.IsFalse(canvas.HasStroke(id));
}
