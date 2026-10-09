using System;
using Puppeteer.EventSourcing.Follower;

namespace Sketch.Acting;

/// <summary>
/// The one rule of forgetting, defined in exactly one place. It pairs a
/// <c>Draw($id, …)</c> with a later <c>Erase($id)</c> of the same id — the captured
/// <c>$id</c> correlates the two seeks by value — and elides the whole matched pair.
/// A stroke that was drawn and then erased contributes nothing to the picture, so
/// its two entries are what the journal can forget.
/// <para>
/// The same definition is registered on a shadow to preview and prove the elision,
/// and on the primary to commit it. The engine does not decide what is safe to
/// forget; this rule is that decision, and the diff is its check.
/// </para>
/// </summary>
public static class ForgettingRule
{
    /// <summary>The reaction's name on whichever actor it is registered.</summary>
    public const string Name = "ForgetErasedStrokes";

    // Reactions observe V2 Actions, so these match the parametrised Draw/Erase
    // commands SketchActor issues. Every seek of a multi-seek reaction must
    // declare a quantifier; the engine refuses to execute one that does not.
    private const string DrawnPattern = "[_:Canvas].Draw($id, _, _, _, _, _)";
    private const string ErasedPattern = "[_:Canvas].Erase($id)";

    /// <summary>Registers the rule on <paramref name="reactions"/>; nothing runs until they execute.</summary>
    internal static void DefineOn(Reactions reactions)
    {
        ArgumentNullException.ThrowIfNull(reactions);

        reactions.DefineReaction(Name)
            .Job().Company().WithSharedHydration()
            .Seek("Drawn")
                .OnMatch(DrawnPattern).One()
            .ThenSeek("Erased")
                .OnMatch(ErasedPattern).One()
            .Metadata.Elide();
    }
}
