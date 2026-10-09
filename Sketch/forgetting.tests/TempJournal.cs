using System;
using System.IO;

namespace Sketch.Forgetting.Tests;

/// <summary>
/// A fresh directory under the system temp folder for one test's journals, removed when
/// the test is done. Removal is best effort: an engine that still holds a released
/// journal file open leaves the folder behind, which changes no test's outcome.
/// </summary>
internal sealed class TempJournal : IDisposable
{
    internal TempJournal()
    {
        Root = Path.Combine(Path.GetTempPath(), "sketch-forgetting-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    internal string Root { get; }

    /// <summary>A new, empty sub-directory, for a journal copy or a census scratch area.</summary>
    internal string NewDirectory(string purpose)
    {
        string directory = Path.Combine(Root, purpose + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
