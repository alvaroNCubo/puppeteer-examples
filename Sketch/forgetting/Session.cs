using System;
using System.Collections.Generic;
using System.Linq;

namespace Sketch.Forgetting;

/// <summary>One act of the scripted session: a Draw (with its geometry) or an Erase of a stroke id.</summary>
internal sealed record Act(bool IsDraw, int Id, int FromX, int FromY, int ToX, int ToY, string Ink, bool IsHesitation);

/// <summary>
/// The scripted drawing session, generated from a seed. The host owns every choice the
/// canvas receives: stroke ids (increasing, never reused), geometry, inks, and when each
/// hesitation is erased.
/// <para>
/// Survivors draw <see cref="Figure"/> part by part. Hesitations are short strokes drawn
/// near where the pen is working and erased a few acts later — never more than
/// <see cref="MaxEraseDelay"/> acts later, far inside the rule's pairing window of 1001
/// entries. The session ends on a survivor, with every hesitation already erased.
/// </para>
/// </summary>
internal sealed class Session
{
    /// <summary>The most acts a hesitation stays on the canvas before it is erased.</summary>
    internal const int MaxEraseDelay = 12;

    private static readonly string[] Palette = { "graphite", "indigo", "vermilion", "ochre", "viridian" };

    private Session(IReadOnlyList<Act> acts, int survivors, int hesitations)
    {
        Acts = acts;
        Survivors = survivors;
        Hesitations = hesitations;
    }

    internal IReadOnlyList<Act> Acts { get; }

    internal int Survivors { get; }

    internal int Hesitations { get; }

    /// <summary>Generates the session for <paramref name="seed"/>; the same seed always yields the same acts.</summary>
    internal static Session Generate(int seed, int survivors, int hesitations)
    {
        if (survivors < 1) throw new ArgumentOutOfRangeException(nameof(survivors), "a session draws at least one surviving stroke");
        if (hesitations < 0) throw new ArgumentOutOfRangeException(nameof(hesitations));

        var random = new Random(seed);
        var figure = Figure.Strokes(survivors);

        // Each hesitation happens just before one survivor, chosen at random.
        var hesitationsBefore = new int[survivors];
        for (int h = 0; h < hesitations; h++)
        {
            hesitationsBefore[random.Next(survivors)]++;
        }

        var acts = new List<Act>(survivors + 2 * hesitations);
        var pending = new List<(int Id, int DueAt)>();
        int nextId = 1;

        void EraseWhatIsDue(bool everything)
        {
            while (true)
            {
                int index = pending.FindIndex(p => everything || p.DueAt <= acts.Count);
                if (index < 0) return;
                var due = pending[index];
                pending.RemoveAt(index);
                acts.Add(new Act(false, due.Id, 0, 0, 0, 0, string.Empty, true));
            }
        }

        for (int s = 0; s < survivors; s++)
        {
            var stroke = figure[s];
            for (int h = 0; h < hesitationsBefore[s]; h++)
            {
                int fromX = Figure.Clamp(stroke.FromX + random.Next(-24, 25), Figure.Width);
                int fromY = Figure.Clamp(stroke.FromY + random.Next(-24, 25), Figure.Height);
                double angle = random.NextDouble() * 2 * Math.PI;
                double length = 10 + random.NextDouble() * 35;
                int toX = Figure.Clamp(fromX + length * Math.Cos(angle), Figure.Width);
                int toY = Figure.Clamp(fromY + length * Math.Sin(angle), Figure.Height);
                string ink = random.NextDouble() < 0.75 ? stroke.Ink : Palette[random.Next(Palette.Length)];

                int id = nextId++;
                acts.Add(new Act(true, id, fromX, fromY, toX, toY, ink, true));
                pending.Add((id, acts.Count + random.Next(1, MaxEraseDelay + 1)));
                EraseWhatIsDue(everything: false);
            }

            // The last stroke of the figure is the session's last act: nothing is erased after it.
            if (s == survivors - 1) EraseWhatIsDue(everything: true);

            acts.Add(new Act(true, nextId++, stroke.FromX, stroke.FromY, stroke.ToX, stroke.ToY, stroke.Ink, false));
            if (s < survivors - 1) EraseWhatIsDue(everything: false);
        }

        return new Session(acts, survivors, hesitations);
    }

    /// <summary>The hesitation whose stroke is longest: the one most visible when it comes back.</summary>
    internal Act MostVisibleHesitation() =>
        Acts.Where(act => act.IsDraw && act.IsHesitation)
            .OrderByDescending(act => (act.ToX - act.FromX) * (act.ToX - act.FromX) + (act.ToY - act.FromY) * (act.ToY - act.FromY))
            .ThenBy(act => act.Id)
            .First();

    /// <summary>
    /// The act near the middle of the session (between 45% and 55% of it) after which the
    /// most hesitations are on the canvas at once; ties go to the earliest. Returns its index
    /// and the ids of those hesitations.
    /// </summary>
    internal (int ActIndex, IReadOnlyList<int> HesitationsOnCanvas) MidSessionMoment()
    {
        var onCanvas = new List<int>();
        int bestIndex = -1;
        List<int> best = new();
        int from = (int)(Acts.Count * 0.45);
        int to = (int)(Acts.Count * 0.55);
        for (int i = 0; i < Acts.Count; i++)
        {
            var act = Acts[i];
            if (act.IsHesitation)
            {
                if (act.IsDraw) onCanvas.Add(act.Id);
                else onCanvas.Remove(act.Id);
            }

            if (i >= from && i <= to && onCanvas.Count > best.Count)
            {
                bestIndex = i;
                best = new List<int>(onCanvas);
            }
        }

        if (bestIndex < 0) bestIndex = Acts.Count / 2;
        return (bestIndex, best);
    }
}
