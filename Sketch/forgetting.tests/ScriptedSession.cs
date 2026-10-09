using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sketch.Acting;

namespace Sketch.Forgetting.Tests;

/// <summary>
/// Drives a canvas with survivors (strokes that stay) and hesitations (strokes drawn and
/// erased again), recording the entry id each act journaled, so a test states its
/// expectations in terms of the acts it performed rather than a guessed journal layout.
/// </summary>
internal sealed class ScriptedSession
{
    private readonly SketchActor sketch;
    private readonly List<long> forgettable = new();
    private readonly Dictionary<int, long> drawnAt = new();

    internal ScriptedSession(SketchActor sketch) => this.sketch = sketch;

    /// <summary>Entry ids of every hesitation's Draw and Erase, ascending: what the rule should elide.</summary>
    internal IReadOnlyList<long> Forgettable => forgettable;

    /// <summary>How many drawn-then-erased pairs the session performed.</summary>
    internal int Pairs => forgettable.Count / 2;

    /// <summary>Draws a stroke that stays; returns the entry id it journaled.</summary>
    internal long Survivor(int id, int fromX, int fromY, int toX, int toY, string ink)
    {
        Assert.IsTrue(sketch.Draw(id, fromX, fromY, toX, toY, ink), $"survivor {id} was refused");
        drawnAt[id] = sketch.CurrentEntryId;
        return sketch.CurrentEntryId;
    }

    /// <summary>Draws a short stroke that will be erased later; returns the entry id it journaled.</summary>
    internal long Tentative(int id)
    {
        Assert.IsTrue(sketch.Draw(id, 5, 5, 9, 9, "vermilion"), $"tentative stroke {id} was refused");
        drawnAt[id] = sketch.CurrentEntryId;
        return sketch.CurrentEntryId;
    }

    /// <summary>Erases a tentative stroke; the pair joins <see cref="Forgettable"/>. Returns the Erase's entry id.</summary>
    internal long EraseTentative(int id)
    {
        Assert.IsTrue(sketch.Erase(id), $"erasing {id} was refused");
        forgettable.Add(drawnAt[id]);
        forgettable.Add(sketch.CurrentEntryId);
        forgettable.Sort();
        return sketch.CurrentEntryId;
    }

    /// <summary>Draws and at once erases a tentative stroke.</summary>
    internal void Hesitation(int id)
    {
        Tentative(id);
        EraseTentative(id);
    }

    /// <summary>
    /// The small session most tests share: survivors 1, 2, 3 in three inks, with three
    /// hesitations between them (one of them nested inside another), ending on a survivor.
    /// </summary>
    internal static ScriptedSession Standard(SketchActor sketch)
    {
        var session = new ScriptedSession(sketch);
        session.Survivor(1, 10, 10, 300, 10, "graphite");
        session.Hesitation(100);
        session.Survivor(2, 300, 10, 300, 190, "indigo");
        session.Tentative(101);
        session.Tentative(102);
        session.EraseTentative(102);
        session.EraseTentative(101);
        session.Survivor(3, 300, 190, 10, 190, "ochre");
        return session;
    }
}
