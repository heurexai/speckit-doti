using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    private TransitionReadiness ValidateTransitionReadiness(CycleState state, CycleStage current)
    {
        var reasons = new List<string>();
        CycleCheckReport check = Check(current.Id);
        if (!check.Passed)
        {
            reasons.AddRange(check.Prerequisites
                .Where(p => !p.Ok)
                .Select(p => $"prerequisite '{p.Stage}': {p.Status} ({p.Reason})"));
        }

        string identity = ChangeSetIdentity.Of(_repositoryRoot, state.BaseRef, "HEAD");
        PersistedGateProof? gateProof = null;
        if (RequiresGateProof(current))
        {
            gateProof = new GateProofStore(_repositoryRoot).Read();
            if (gateProof is null)
            {
                reasons.Add("no gate proof; run `gate run` first");
            }
            else if (gateProof.Proof.Outcome != StageOutcome.Pass)
            {
                reasons.Add($"gate proof is not passing (outcome {gateProof.Proof.Outcome})");
            }
            else if (!string.Equals(gateProof.ChangeSetId, identity, StringComparison.Ordinal))
            {
                reasons.Add("gate proof is stale (the diff changed since the gate ran); re-run `gate run`");
            }
            else
            {
                reasons.AddRange(GateProofValidator.ValidateAffectedTestProof(_repositoryRoot, gateProof));
            }
        }

        CommitScope scope = CommitScopeInspector.Inspect(_repositoryRoot);
        if (scope.HasUnstagedTrackedChanges)
        {
            reasons.Add("unstaged tracked changes present; stage or revert them for a deliberate transition scope");
        }

        if (scope.HasUntrackedChanges)
        {
            reasons.Add("untracked changes present; add, ignore, or remove them for a clean transition scope");
        }

        reasons.AddRange(ValidateDocStageScope(current, scope));
        return new TransitionReadiness(reasons, new CommitReadiness(reasons, identity, gateProof, scope));
    }

    private IReadOnlyList<string> ValidateDocStageScope(CycleStage current, CommitScope scope)
    {
        if (!string.Equals(current.Kind, "doc", StringComparison.OrdinalIgnoreCase)
            || current.Produces is not { } produces
            || scope.StagedPaths.Count == 0)
        {
            return [];
        }

        string expected = FreshnessEvaluator.ResolveProduces(produces, _store.Read()?.Feature ?? string.Empty)
            .Replace('\\', '/');
        return scope.StagedPaths
            .Where(path => !string.Equals(path, expected, StringComparison.Ordinal))
            .Select(path => $"staged path '{path}' is unrelated to doc stage artifact '{expected}'")
            .ToArray();
    }

    private static bool RequiresGateProof(CycleStage stage) =>
        string.Equals(stage.Kind, "diff", StringComparison.OrdinalIgnoreCase);

    private sealed record TransitionReadiness(
        IReadOnlyList<string> Reasons,
        CommitReadiness CommitReadiness);
}
