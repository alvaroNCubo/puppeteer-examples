using System;
using System.Collections.Generic;
using System.Linq;

namespace Sketch.Forgetting;

/// <summary>Repeated measurements of one duration, in milliseconds.</summary>
internal sealed record Timing(IReadOnlyList<double> SamplesMs)
{
    internal double MedianMs
    {
        get
        {
            var sorted = SamplesMs.OrderBy(sample => sample).ToArray();
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }
    }

    internal double MinMs => SamplesMs.Min();

    internal double MaxMs => SamplesMs.Max();

    internal static double Ms(TimeSpan elapsed) => Math.Round(elapsed.TotalMilliseconds, 1);
}
