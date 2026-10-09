using System;
using System.Globalization;

namespace Sketch.Forgetting;

/// <summary>The lab's command line.</summary>
internal sealed record LabOptions(string Out, int Seed, int Survivors, int Hesitations, int Scale, int Runs)
{
    internal const string Usage =
        "usage: Forgetting --out <dir> [--seed 42] [--survivors 500] [--hesitations 1500] [--scale 1] [--runs 5]";

    /// <summary>Survivors after scaling.</summary>
    internal int ScaledSurvivors => Survivors * Scale;

    /// <summary>Hesitations after scaling.</summary>
    internal int ScaledHesitations => Hesitations * Scale;

    internal static LabOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? output = null;
        int seed = 42, survivors = 500, hesitations = 1500, scale = 1, runs = 5;
        for (int i = 0; i < args.Length; i++)
        {
            string name = args[i];
            if (i + 1 >= args.Length) throw new ArgumentException($"{name} needs a value");
            string value = args[++i];
            switch (name)
            {
                case "--out": output = value; break;
                case "--seed": seed = Number(name, value, allowZero: true); break;
                case "--survivors": survivors = Number(name, value, allowZero: false); break;
                case "--hesitations": hesitations = Number(name, value, allowZero: true); break;
                case "--scale": scale = Number(name, value, allowZero: false); break;
                case "--runs": runs = Number(name, value, allowZero: false); break;
                default: throw new ArgumentException($"unknown option {name}");
            }
        }

        if (string.IsNullOrWhiteSpace(output)) throw new ArgumentException("--out is required");
        return new LabOptions(output, seed, survivors, hesitations, scale, runs);
    }

    private static int Number(string name, string value, bool allowZero)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < 0 || (!allowZero && number == 0))
        {
            throw new ArgumentException($"{name} needs a {(allowZero ? "non-negative" : "positive")} whole number, got '{value}'");
        }

        return number;
    }
}
