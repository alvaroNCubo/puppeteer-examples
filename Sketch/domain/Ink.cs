namespace Sketch;

/// <summary>
/// One ink of the canvas's closed palette. Exactly these five exist, each as one
/// shared instance, so an ink is recognised by reference and an ink outside the
/// palette cannot be represented. Across the wire an ink travels as its
/// <see cref="Name"/> and comes back through <see cref="Named"/>.
/// </summary>
internal sealed class Ink
{
    /// <summary>A soft grey, the ink of construction and outline.</summary>
    internal static readonly Ink Graphite = new("graphite");

    /// <summary>A deep blue.</summary>
    internal static readonly Ink Indigo = new("indigo");

    /// <summary>A bright red.</summary>
    internal static readonly Ink Vermilion = new("vermilion");

    /// <summary>A warm earth yellow.</summary>
    internal static readonly Ink Ochre = new("ochre");

    /// <summary>A cool green.</summary>
    internal static readonly Ink Viridian = new("viridian");

    /// <summary>The ink's name in the palette, the form it takes on the wire.</summary>
    internal string Name { get; }

    private Ink(string name) => Name = name;

    /// <summary>
    /// Resolves a palette name to its shared ink. A name outside the palette is a
    /// caller bug, so it is refused rather than answered.
    /// </summary>
    internal static Ink Named(string name) => name switch
    {
        "graphite" => Graphite,
        "indigo" => Indigo,
        "vermilion" => Vermilion,
        "ochre" => Ochre,
        "viridian" => Viridian,
        _ => throw new SketchRuleException($"'{name}' is not an ink of the palette"),
    };
}
