using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Sketch.Forgetting;

/// <summary>
/// The lab's short log: every line goes to the console and into <c>run.log</c>. Lines
/// name files relative to the output folder, never by absolute path.
/// </summary>
internal sealed class LabLog
{
    private readonly List<string> lines = new();

    internal void Scene(string title)
    {
        Line(string.Empty);
        Line("== " + title);
    }

    internal void Line(string text)
    {
        lines.Add(text);
        Console.WriteLine(text);
    }

    internal void Fact(string name, object value) =>
        Line(string.Create(CultureInfo.InvariantCulture, $"   {name,-34} {value}"));

    internal void WriteTo(string path) => File.WriteAllLines(path, lines);
}
