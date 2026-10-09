using System;

namespace Sketch.Forgetting;

/// <summary>
/// The "Forgetting on Purpose" lab: performs a scripted drawing session on a FileSystem
/// journal, previews, proves and commits the forgetting of every drawn-then-erased
/// stroke, and writes the evidence of each scene to <c>--out</c>.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        LabOptions options;
        try
        {
            options = LabOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(LabOptions.Usage);
            return 2;
        }

        try
        {
            new Lab(options).Run();
            return 0;
        }
        catch (LabCheckFailed failure)
        {
            Console.Error.WriteLine("CHECK FAILED: " + failure.Message);
            return 1;
        }
    }
}

/// <summary>A claim the lab verifies at run time did not hold; the run's evidence is not written.</summary>
internal sealed class LabCheckFailed : Exception
{
    internal LabCheckFailed(string message)
        : base(message)
    {
    }
}
