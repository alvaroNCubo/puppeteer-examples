using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sketch.Acting;

namespace Sketch.Forgetting.Tests;

/// <summary>
/// How far apart a Draw and its Erase may be and still be paired by the rule. A Draw
/// still waiting for its Erase is a match in progress, and the engine prunes a match
/// that has not advanced for more than 1000 entries; measured from the Draw's entry,
/// the Erase must arrive within 1001 entries. What lies in between does not matter.
/// </summary>
[TestClass]
public class PairingWindowTests
{
    private const string Actor = "canvas";

    [DataTestMethod]
    [DataRow(2, false)]
    [DataRow(1000, false)]
    [DataRow(1001, false)]
    [DataRow(1001, true)]
    public void AnEraseWithin1001EntriesOfItsDraw_IsPaired(int distance, bool pairsBetween) =>
        Assert.IsTrue(IsPaired(distance, pairsBetween));

    [DataTestMethod]
    [DataRow(1002, false)]
    [DataRow(1002, true)]
    [DataRow(1500, false)]
    public void AnEraseFartherThan1001EntriesFromItsDraw_IsNotPaired(int distance, bool pairsBetween) =>
        Assert.IsFalse(IsPaired(distance, pairsBetween));

    // Draws stroke 1, fills the journal until the Erase of stroke 1 lands exactly
    // `distance` entries after its Draw — with surviving strokes, or with other
    // drawn-then-erased pairs — and asks the rule's preview.
    private static bool IsPaired(int distance, bool pairsBetween)
    {
        using var temp = new TempJournal();
        using var sketch = SketchActor.Open(Actor, temp.Root, 320, 200);
        var session = new ScriptedSession(sketch);
        session.Hesitation(90);                       // both Define rows are journaled first
        long drawn = session.Tentative(1);
        int filler = 1000;
        while (sketch.CurrentEntryId < drawn + distance - 1)
        {
            bool roomForAPair = sketch.CurrentEntryId + 2 <= drawn + distance - 1;
            if (pairsBetween && roomForAPair) session.Hesitation(filler++);
            else session.Survivor(filler++, 0, 0, 1, 1, "graphite");
        }

        long erased = session.EraseTentative(1);
        session.Survivor(filler, 0, 0, 1, 1, "graphite");
        Assert.AreEqual(drawn + distance, erased);

        var preview = sketch.Preview();
        bool paired = preview.WouldElide.Contains(drawn);
        Assert.AreEqual(paired, preview.WouldElide.Contains(erased), "a pair is elided whole or not at all");
        return paired;
    }
}
