using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    /// <summary>029: the resolved setup config for a host command, OR an early CLI failure (an invalid <c>--config</c>).</summary>
    internal sealed record SetupConfigResolution(ResolvedSetupConfig? Resolved, CliResult? Error);

    /// <summary>029 FR-009: render a fail-closed validation failure naming every offending field; no files are created (SC-006).</summary>
    internal static CliResult SetupConfigInvalid(CliMeta meta, string command, IReadOnlyList<SetupValidationError> errors)
    {
        IReadOnlyList<Diagnostic> diagnostics = errors.Count == 0
            ? [Diag.Of(ErrorCodes.Validation_SetupConfigInvalid)]
            : errors.Select(e => Diag.Of(ErrorCodes.Validation_SetupConfigInvalid, $"{e.Field}: {e.Message}", target: e.Field)).ToList();
        return CliResults.Fail(meta, command, ExitClass.Validation, diagnostics,
            "The supplied setup configuration is invalid; no files were created.");
    }

    internal static string[] ResolveAgents(ResolvedSetupConfig? resolved, string agentsCsv)
    {
        string value = resolved?.ValueOrDefault(SetupKeys.Agents) ?? agentsCsv;
        string[] agents = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return agents.Length > 0
            ? agents
            : agentsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    internal static string NonEmptyOr(string? primary, string fallback) =>
        string.IsNullOrWhiteSpace(primary) ? fallback : primary!;

    internal static string? NonEmptyOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    internal static IReadOnlyList<string>? AgentsOrNull(string agentsCsv)
    {
        if (string.IsNullOrWhiteSpace(agentsCsv))
        {
            return null;
        }

        string[] agents = agentsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Only treat as an explicit override when it differs from the default — so the default does not masquerade as a flag.
        return agents.Length > 0 && !string.Equals(string.Join(',', agents), SetupConfigDefaults.Agents, StringComparison.Ordinal)
            ? agents
            : null;
    }

    /// <summary>029 D9: filesystem containment of the output directory — reject a path that escapes the working tree
    /// via <c>..</c> traversal (the pure check plus a GetFullPath+StartsWith on the resolved absolute path).</summary>
    internal static bool IsContainedOutput(string output)
    {
        if (Path.IsPathRooted(output))
        {
            return true; // an explicit absolute --output is an operator choice (existing behavior), not a traversal escape.
        }

        if (!SetupConfigSchema.IsContainedRelativePath(output))
        {
            return false;
        }

        string baseFull = Path.GetFullPath(Directory.GetCurrentDirectory());
        string full = Path.GetFullPath(Path.Combine(baseFull, output));
        string root = baseFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
