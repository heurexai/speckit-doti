using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    private static bool TryParseAgents(string csv, out List<DotiAgentTarget> agents, out string? error)
    {
        if (!DotiAgentTarget.TryParseCsv(csv, out IReadOnlyList<DotiAgentTarget> parsed, out error))
        {
            agents = [];
            return false;
        }

        agents = parsed.ToList();
        return true;
    }

    private static string? FindDotiSource(string start)
    {
        DirectoryInfo? dir = new(Path.GetFullPath(start));
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".doti", "core", "skills.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// 031 T002 (FR-001/002, D1): resolve the payload SOURCE the global-tool reconcile reads, defaulting to the
    /// running tool's bundled payload (beside the executable) and falling back to a working-directory dev walk only
    /// for a genuine in-repo source. On success returns the source root + its origin
    /// (<see cref="DotiSourceOrigin.Bundled"/> / <see cref="DotiSourceOrigin.DevCwd"/>); when BOTH are null the
    /// reconcile FAILS CLOSED (never silently stamps from a non-payload source) and <paramref name="failure"/> carries
    /// the coded <see cref="ErrorCodes.Validation_DotiPayloadSourceUnresolved"/> result.
    /// </summary>
    private static bool TryResolveDotiPayloadSource(
        CliMeta meta, string command, out string source, out string origin, out CliResult? failure)
    {
        string? bundled = BundledPayloadResolver.Resolve();
        if (bundled is not null)
        {
            source = bundled;
            origin = DotiSourceOrigin.Bundled;
            failure = null;
            return true;
        }

        string? dev = FindDotiSource(Directory.GetCurrentDirectory());
        if (dev is not null)
        {
            source = dev;
            origin = DotiSourceOrigin.DevCwd;
            failure = null;
            return true;
        }

        source = string.Empty;
        origin = DotiSourceOrigin.Unresolved;
        failure = CliResults.Fail(meta, command, ExitClass.Validation,
            [Diag.Of(ErrorCodes.Validation_DotiPayloadSourceUnresolved,
                "No version-stamped Doti payload source could be resolved: neither the running tool's bundled payload "
                + "(beside the executable) nor a `.doti/core/skills.json` ancestor of the current directory exists.")],
            "Doti reconcile could not resolve a payload source.",
            new { resolvedSource = (object?)null },
            nextActions:
            [
                new CliNextAction(
                    "Run from an installed hx",
                    "Install hx as a .NET global tool (its payload ships beside the executable), or run from inside a Doti source checkout."),
            ]);
        return false;
    }
}

/// <summary>031 (D5/FR-011): the origin of the resolved Doti payload source, reported in the result envelope.</summary>
public static class DotiSourceOrigin
{
    public const string Bundled = "bundled";   // the running tool's payload beside the executable (the global-tool default)
    public const string DevCwd = "dev-cwd";     // a `.doti/core/skills.json` ancestor of the current directory (dev/in-repo)
    public const string Unresolved = "unresolved"; // neither resolved — the reconcile fails closed
}
