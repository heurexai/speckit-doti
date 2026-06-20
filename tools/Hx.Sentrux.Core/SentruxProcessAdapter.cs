using Hx.Runner.Core.Process;

namespace Hx.Sentrux.Core;

/// <summary>
/// Builds Sentrux process invocations via <see cref="ToolCommand"/>
/// (ArgumentList), always with <c>SENTRUX_SKIP_GRAMMAR_DOWNLOAD</c> set so gates
/// stay offline. Exact flags verified against the pinned fork release at vendor time.
/// </summary>
public static class SentruxProcessAdapter
{
    private static IReadOnlyDictionary<string, string> OfflineEnv =>
        new Dictionary<string, string> { ["SENTRUX_SKIP_GRAMMAR_DOWNLOAD"] = "1" };

    public static ToolCommand Check(string executablePath, string repositoryRoot) =>
        new(executablePath, ["check", repositoryRoot, "--include-untracked", "--json"], repositoryRoot, OfflineEnv);

    public static ToolCommand GateSave(string executablePath, string repositoryRoot) =>
        new(executablePath, ["gate", "--save", repositoryRoot], repositoryRoot, OfflineEnv);

    public static ToolCommand GateCompare(string executablePath, string repositoryRoot) =>
        new(executablePath, ["gate", repositoryRoot], repositoryRoot, OfflineEnv);
}
