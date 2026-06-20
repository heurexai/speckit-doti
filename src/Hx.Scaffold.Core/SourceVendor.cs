namespace Hx.Scaffold.Core;

/// <summary>
/// Copies the runner + impact tooling SOURCE projects into a generated repo so it runs the identical
/// <c>dotnet run --project tools/Hx.Runner.Cli</c> workflow as the scaffold (vendoring
/// form = source). This list MUST equal the full forward project-reference closure of the vendored CLIs;
/// <c>SourceVendorClosureTests</c> verifies that against the real project graph and fails closed on drift
/// (it caught a missing <c>Hx.Gate.Core</c> reference). bin/obj are excluded.
/// </summary>
public static class SourceVendor
{
    public static readonly IReadOnlyList<string> Projects =
    [
        "Hx.Tooling.Contracts",
        "Hx.Cli.Kernel",
        "Hx.Doti.Core",
        "Hx.Runner.Core",
        "Hx.Sentrux.Core",
        "Hx.Gate.Core",
        "Hx.Version.Core",
        "Hx.Security.Core",
        "Hx.Cycle.Core",
        "Hx.Impact.Core",
        "Hx.Runner.Cli",
        "Hx.Impact.Cli",
    ];

    public static void Vendor(string sourceRepoRoot, string targetRepoRoot)
    {
        foreach (string project in Projects)
        {
            string from = Path.Combine(sourceRepoRoot, "tools", project);
            string to = Path.Combine(targetRepoRoot, "tools", project);
            if (!Directory.Exists(from))
            {
                throw new DirectoryNotFoundException($"Vendored source project is missing: tools/{project}");
            }

            DirectoryCopy.Copy(from, to, DirectoryCopy.ExcludeBuildArtifacts);
        }
    }
}
