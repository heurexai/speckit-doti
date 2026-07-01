using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    /// <summary>
    /// 039 WI4/FR-032: finalize a cycle wedged at the <c>release</c> stage. The dev→main→CI publish path never calls
    /// <see cref="MarkReleaseTrainReleased"/>, so <c>ReleasedCycles</c> stays empty and the existing
    /// <c>IsCurrentFeatureReleased</c> recognition (which lets the next <c>specify</c> start) never fires — the exact
    /// 031 wedge. This moves the active feature into <c>ReleasedCycles</c> WITHOUT re-validating the train (it already
    /// shipped — a stale gate proof must not block finalizing a released cycle; mirrors the 0.18.5 post-tag no-op
    /// guard). Idempotent (a no-op once the feature is recognized released); fail-closed if the cycle is not at
    /// release-stage or the repo has no release tag (there is no shipped release to finalize).
    /// </summary>
    public CycleReleaseTrain FinalizeReleasedCycle()
    {
        CycleState? state = _store.Read();
        if (state is null || IsCurrentFeatureReleased(state))
        {
            return BuildReleaseTrain(state); // bug-only (self-maintains via tag), or already finalized — idempotent
        }

        if (!string.Equals(state.CurrentStage, "release", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot finalize-release: the cycle is at stage '{state.CurrentStage}', not 'release'. Only a cycle that "
                + "reached the release stage can be finalized.");
        }

        if (!HasReleaseTag())
        {
            throw new InvalidOperationException(
                "Cannot finalize-release: no release tag (v*) exists in the repository — there is no shipped release to "
                + "finalize. Run the release first.");
        }

        IReadOnlyList<CycleCompletionRecord> included = CompletionRecordsForRelease(state);
        _store.Write(state with
        {
            CompletedUnreleasedCycles = [],
            ReleasedCycles = (state.ReleasedCycles ?? []).Concat(included).ToArray(),
        });
        return BuildReleaseTrain(_store.Read());
    }

    /// <summary>True when a release-stage cycle is finalizable via <see cref="FinalizeReleasedCycle"/> — it is at the
    /// release stage, has an unfinalized (not-yet-released) active feature, and a release tag exists. The recovery
    /// planner surfaces the verb only in this state (FR-033: no dead-end, a coded path always forward).</summary>
    public bool CanFinalizeReleasedCycle()
    {
        CycleState? state = _store.Read();
        return state is not null
            && !IsCurrentFeatureReleased(state)
            && string.Equals(state.CurrentStage, "release", StringComparison.OrdinalIgnoreCase)
            && HasReleaseTag();
    }

    private bool HasReleaseTag() =>
        !string.IsNullOrWhiteSpace(
            ProcessRunner.Run(new ToolCommand("git", ["tag", "--list", "v[0-9]*"], _repositoryRoot))
                .StandardOutput.Trim());
}
