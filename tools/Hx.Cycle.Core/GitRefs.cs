using Hx.Runner.Core.Process;

namespace Hx.Cycle.Core;

/// <summary>Shared git ref helpers for the cycle: the base ref the change-set identity is computed
/// against (the integration branch <c>dev</c> if it resolves, else <c>HEAD</c>) and the current HEAD sha.</summary>
public static class GitRefs
{
    public static string ResolveBaseRef(string repositoryRoot)
    {
        ProcessRunResult result = ProcessRunner.Run(
            new ToolCommand("git", ["rev-parse", "--verify", "--quiet", "dev"], repositoryRoot));
        return result.ExitCode == 0 ? "dev" : "HEAD";
    }

    public static string? TryHeadSha(string repositoryRoot)
    {
        ProcessRunResult result = ProcessRunner.Run(new ToolCommand("git", ["rev-parse", "HEAD"], repositoryRoot));
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardOutput.Trim()
            : null;
    }
}
