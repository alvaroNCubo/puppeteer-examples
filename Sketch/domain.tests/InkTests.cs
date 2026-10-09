using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sketch.Tests;

[TestClass]
public class InkTests
{
    [DataTestMethod]
    [DataRow("graphite")]
    [DataRow("indigo")]
    [DataRow("vermilion")]
    [DataRow("ochre")]
    [DataRow("viridian")]
    public void EveryPaletteName_ResolvesToOneSharedInk(string name)
    {
        Ink first = Ink.Named(name);

        Assert.AreSame(first, Ink.Named(name));
        Assert.AreEqual(name, first.Name);
    }

    [TestMethod]
    public void EachPaletteInk_IsTheInkItsNameResolvesTo()
    {
        Assert.AreSame(Ink.Graphite, Ink.Named("graphite"));
        Assert.AreSame(Ink.Indigo, Ink.Named("indigo"));
        Assert.AreSame(Ink.Vermilion, Ink.Named("vermilion"));
        Assert.AreSame(Ink.Ochre, Ink.Named("ochre"));
        Assert.AreSame(Ink.Viridian, Ink.Named("viridian"));
    }

    [DataTestMethod]
    [DataRow("gold")]
    [DataRow("Graphite")]
    [DataRow("")]
    [DataRow(" ")]
    public void ANameOutsideThePalette_IsRefused(string name) =>
        Assert.ThrowsException<SketchRuleException>(() => Ink.Named(name));

    [TestMethod]
    public void NoName_IsRefused() =>
        Assert.ThrowsException<SketchRuleException>(() => Ink.Named(null!));
}
