using Hx.Runner.Core.Process;

namespace Hx.Impact.Core.ChangeDetection;

/// <summary>The raw git output a change set is built from: the resolved base SHA and the diff/status NUL streams.
/// <see cref="MergeBaseResolved"/> is false (with <see cref="UnresolvedReason"/>) when the merge-base cannot be
/// resolved — the seam reports the failure rather than throwing, so each caller decides (fail-closed identity vs
/// a <c>RefsResolved=false</c> projection).</summary>
public sealed record GitChangeOutputs(
    bool MergeBaseResolved,
    string? BaseSha,
    string? UnresolvedReason,
    string DiffNameStatusZ,
    string StatusPorcelainZ);

/// <summary>The git seam for change-set collection — injected so the builder/collector are unit-testable
/// without a real repository.</summary>
public interface IGitChangeSource
{
    GitChangeOutputs Read(string repositoryRoot, string baseRef, string headRef, bool includeWorkingTree);
}

/// <summary>The production seam: shells <c>git</c> for the merge-base, the committed diff, and (optionally) the
/// working-tree status. When <paramref name="includeWorkingTree"/> is false the status union is skipped, giving a
/// pure historical <c>base..head</c> diff (the release-train pairwise comparison, FR-037/H-1).</summary>
public sealed class GitChangeSource : IGitChangeSource
{
    public GitChangeOutputs Read(string repositoryRoot, string baseRef, string headRef, bool includeWorkingTree)
    {
        ProcessRunResult mergeBase = Git(repositoryRoot, "merge-base", baseRef, headRef);
        if (mergeBase.ExitCode != 0 || string.IsNullOrWhiteSpace(mergeBase.StandardOutput))
        {
            string detail = string.IsNullOrWhiteSpace(mergeBase.StandardError)
                ? $"exit {mergeBase.ExitCode}"
                : mergeBase.StandardError.Trim();
            return new GitChangeOutputs(
                false, null, $"Could not resolve merge-base for '{baseRef}'..'{headRef}': {detail}", "", "");
        }

        string baseSha = mergeBase.StandardOutput.Trim();
        string diff = Git(repositoryRoot, "diff", "--name-status", "-M", "-z", baseSha, headRef).StandardOutput;
        string status = includeWorkingTree
            ? Git(repositoryRoot, "status", "--porcelain=v1", "-z", "--untracked-files=all").StandardOutput
            : string.Empty;
        return new GitChangeOutputs(true, baseSha, null, diff, status);
    }

    private static ProcessRunResult Git(string repositoryRoot, params string[] arguments) =>
        ProcessRunner.Run(new ToolCommand("git", arguments, repositoryRoot));
}
