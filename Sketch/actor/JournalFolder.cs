using System;
using System.IO;

namespace Sketch.Acting;

/// <summary>
/// The FileSystem journal of one actor on disk: the folder
/// <c>&lt;journal directory&gt;/&lt;actor name&gt;</c> the engine writes, with the record
/// segments under <c>journal/</c>. Measures it and copies it; never interprets its bytes.
/// </summary>
public static class JournalFolder
{
    /// <summary>The folder the engine keeps the actor's journal in.</summary>
    public static string Of(string journalDirectory, string actorName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorName);
        return Path.Combine(journalDirectory, actorName);
    }

    /// <summary>Bytes the actor's journal occupies: record segments alone, and every file of the folder.</summary>
    public static JournalFootprint Measure(string journalDirectory, string actorName)
    {
        string folder = Of(journalDirectory, actorName);
        if (!Directory.Exists(folder)) throw new ArgumentException($"No journal for '{actorName}' under '{journalDirectory}'.", nameof(journalDirectory));

        long segmentBytes = 0;
        long totalBytes = 0;
        int files = 0;
        string segments = Path.Combine(folder, "journal");
        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            long length = new FileInfo(file).Length;
            totalBytes += length;
            files++;
            if (file.StartsWith(segments + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                segmentBytes += length;
            }
        }

        return new JournalFootprint(segmentBytes, totalBytes, files);
    }

    /// <summary>
    /// Copies the actor's journal folder into <paramref name="destinationDirectory"/>, so an
    /// actor of the same name can be opened there. Works while the journal is open: every
    /// file is read with sharing that tolerates its writer, and an unsealed active segment
    /// is what the engine already recovers on open.
    /// </summary>
    public static void Copy(string journalDirectory, string actorName, string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        string source = Of(journalDirectory, actorName);
        string target = Of(destinationDirectory, actorName);
        if (Directory.Exists(target)) throw new ArgumentException($"'{target}' already holds a journal.", nameof(destinationDirectory));

        Directory.CreateDirectory(target);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            using var from = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var to = new FileStream(Path.Combine(target, Path.GetRelativePath(source, file)), FileMode.CreateNew, FileAccess.Write);
            from.CopyTo(to);
        }
    }
}
