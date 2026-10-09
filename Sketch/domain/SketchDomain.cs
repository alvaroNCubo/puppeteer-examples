namespace Sketch;

/// <summary>
/// Public anchor so a host can hand the domain assembly to the framework with
/// <c>typeof(SketchDomain).Assembly</c>. Every other type in this library is
/// <c>internal</c>; this empty type is the single seam a host needs.
/// </summary>
public sealed class SketchDomain
{
}
