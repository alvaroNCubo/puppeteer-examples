using System.Linq;
using System.Reflection;

namespace Sketch.Forgetting;

/// <summary>The engine a run measured: the commit it was built from and its build configuration.</summary>
internal sealed record Engine(string Commit, string Build)
{
    internal static Engine Describe() => new(
        typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "EngineCommit")?.Value is { Length: > 0 } commit ? commit : "unknown",
        typeof(Puppeteer.Actor).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "unknown");
}
