using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    private CommitReadiness ValidateCommitReadiness(CycleState state)
    {
        var reasons = new List<string>();
        CycleCheckReport check = Check("commit");
        if (!check.Passed)
        {
            reasons.AddRange(check.Prerequisites
                .Where(p => !p.Ok)
                .Select(p => $"prerequisite '{p.Stage}': {p.Status} ({p.Reason})"));
        }

        string identity = ChangeSetIdentity.Of(_repositoryRoot, state.BaseRef, "HEAD");
        PersistedGateProof? gateProof = new GateProofStore(_repositoryRoot).Read();
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

        CommitScope scope = CommitScopeInspector.Inspect(_repositoryRoot);
        if (!scope.HasStaged)
        {
            reasons.Add("nothing staged to commit");
        }

        if (scope.HasUnstagedTrackedChanges)
        {
            reasons.Add("unstaged tracked changes present; stage or revert them for a deliberate scope");
        }

        if (scope.HasUntrackedChanges)
        {
            reasons.Add("untracked changes present; add, ignore, or remove them for a clean staged scope");
        }

        return new CommitReadiness(reasons, identity, gateProof, scope);
    }

}
