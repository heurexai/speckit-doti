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

    /// <summary>
    /// 022 (Bug#2 fix): the SINGLE source of the gate proof's base ref, used by BOTH the gate's affected-test planner
    /// (<c>GateRunner</c>) and the proof persistence (<c>GateProofStore.Persist</c>) so they can never diverge. The
    /// active cycle's <c>BaseRef</c> is authoritative — it is the per-stage base the transition validator
    /// (<see cref="GateProofValidator"/>) re-plans from; if the gate planned off a DIFFERENT base (e.g. <c>dev</c>)
    /// while the cycle base has advanced (rebase-to-head per transition), the proof's two base refs diverge and the
    /// diff/release transition rejects an otherwise-valid proof ("affected-test proof base ref does not match"). With
    /// no active cycle (standalone gate run) it falls back to the integration branch <c>dev</c>, else <c>HEAD</c> —
    /// always resolved to a concrete SHA so a stored symbolic ref cannot re-resolve to a different commit after a later
    /// transition advances HEAD.
    /// </summary>
    public static string ResolveProofBaseRef(string repositoryRoot) =>
        PickProofBaseRef(
            new CycleStateStore(repositoryRoot).Read()?.BaseRef,
            TryResolveSha(repositoryRoot, "dev"),
            TryResolveSha(repositoryRoot, "HEAD"));

    /// <summary>Pure priority pick (testable): cycle base wins, else dev, else HEAD, else the symbolic <c>"HEAD"</c>.</summary>
    public static string PickProofBaseRef(string? cycleBaseRef, string? devSha, string? headSha) =>
        Nonblank(cycleBaseRef) ?? Nonblank(devSha) ?? Nonblank(headSha) ?? "HEAD";

    private static string? Nonblank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TryResolveSha(string repositoryRoot, string reference)
    {
        ProcessRunResult result = ProcessRunner.Run(
            new ToolCommand("git", ["rev-parse", "--verify", "--quiet", reference], repositoryRoot));
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardOutput.Trim()
            : null;
    }
}
