using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    private bool TryCompletedCycleCheck(
        CycleStage target,
        CycleState? state,
        CycleRecoveryReport recovery,
        out CycleCheckReport? report)
    {
        report = null;
        if (state is not null && TryCompletedClean(state, out CycleCompletionRecord? completion))
        {
            report = new CycleCheckReport(JsonContractDefaults.SchemaVersion, target.Id, false,
                [new StagePrereqResult("cycle", "completed", false,
                    $"previous cycle completed at {completion.CommitSha}; stamp specify to start a new cycle")],
                completion,
                recovery);
            return true;
        }

        if (state?.Completion is null)
        {
            return false;
        }

        report = new CycleCheckReport(JsonContractDefaults.SchemaVersion, target.Id, false,
            [new StagePrereqResult("cycle", "completed-with-new-changes", false,
                $"previous cycle completed at {state.Completion.CommitSha}; new edits require a new specify stamp")],
            state.Completion,
            recovery);
        return true;
    }

    private StagePrereqResult EvaluatePrerequisite(
        string prereqId,
        CycleState? state,
        FreshnessEvaluator evaluator,
        string identity) =>
        EvaluatePrerequisite(prereqId, state, evaluator, identity, []);

    private StagePrereqResult EvaluatePrerequisite(
        string prereqId,
        CycleState? state,
        FreshnessEvaluator evaluator,
        string identity,
        HashSet<string> evaluating)
    {
        if (!evaluating.Add(prereqId))
        {
            return new StagePrereqResult(prereqId, "invalid", false, "cyclic prerequisite graph");
        }

        CycleStageProof? proof = state?.Stages.FirstOrDefault(
            s => string.Equals(s.Stage, prereqId, StringComparison.OrdinalIgnoreCase));
        if (proof is null || state is null)
        {
            evaluating.Remove(prereqId);
            return new StagePrereqResult(prereqId, "missing", false, "not stamped");
        }

        StageFreshnessResult freshness = evaluator.Evaluate(
            proof,
            state.Feature,
            identity,
            RequiresChangeSetIdentity(prereqId));
        if (freshness.Freshness == StageFreshness.Stale)
        {
            evaluating.Remove(prereqId);
            return new StagePrereqResult(prereqId, "stale", false, freshness.Reason);
        }

        string? openMarker = OpenClarificationMarker(prereqId, state.Feature);
        if (openMarker is not null)
        {
            evaluating.Remove(prereqId);
            return new StagePrereqResult(prereqId, "invalid", false, openMarker);
        }

        CycleStage prereqStage = _stageModel.Find(prereqId);
        if (prereqStage.Prereqs.Count == 0)
        {
            evaluating.Remove(prereqId);
            return new StagePrereqResult(prereqId, "fresh", true, null);
        }

        foreach (string parentId in prereqStage.Prereqs)
        {
            StagePrereqResult parent = EvaluatePrerequisite(parentId, state, evaluator, identity, evaluating);
            if (!parent.Ok)
            {
                evaluating.Remove(prereqId);
                return new StagePrereqResult(prereqId, "stale", false,
                    $"prerequisite '{parent.Stage}' is {parent.Status}: {parent.Reason}");
            }
        }

        string? expectedHash = CycleStageProofHasher.HashPrerequisites(state, prereqStage.Prereqs);
        StagePrereqResult result = ValidatePrerequisiteHash(prereqId, proof, expectedHash);
        evaluating.Remove(prereqId);
        return result;
    }

    private static StagePrereqResult ValidatePrerequisiteHash(
        string prereqId,
        CycleStageProof proof,
        string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(proof.PrerequisiteProofHash))
        {
            return new StagePrereqResult(prereqId, "invalid", false,
                "missing prerequisite proof hash; re-stamp the stage with the current runner");
        }

        if (!string.Equals(expectedHash, proof.PrerequisiteProofHash, StringComparison.Ordinal))
        {
            return new StagePrereqResult(prereqId, "invalid", false,
                "prerequisite proof hash differs from the current prerequisite proofs");
        }

        return new StagePrereqResult(prereqId, "fresh", true, null);
    }

    // The transitive prerequisite closure of a stage, returned in workflow declaration order (deterministic).
    private IReadOnlyList<string> ResolveTransitivePrerequisites(CycleStage target)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>(target.Prereqs);
        while (stack.Count > 0)
        {
            string id = stack.Pop();
            if (!seen.Add(id))
            {
                continue;
            }

            foreach (string parent in _stageModel.Find(id).Prereqs)
            {
                stack.Push(parent);
            }
        }

        return _stageModel.Stages.Select(s => s.Id).Where(seen.Contains).ToList();
    }

    private bool RequiresChangeSetIdentity(string stageId)
    {
        CycleStage stage = _stageModel.Find(stageId);
        if (string.Equals(stage.Kind, "diff", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ResolveTransitivePrerequisites(stage)
            .Select(id => _stageModel.Find(id))
            .Any(prereq => string.Equals(prereq.Kind, "diff", StringComparison.OrdinalIgnoreCase));
    }

    // A doc stage's artifact must not still carry an open [NEEDS CLARIFICATION] marker (output discipline).
    private string? OpenClarificationMarker(string stageId, string feature)
    {
        CycleStage stage = _stageModel.Find(stageId);
        if (stage.Produces is not { } pattern)
        {
            return null;
        }

        string artifactPath = FreshnessEvaluator.ResolveProduces(pattern, feature);
        string full = Path.GetFullPath(Path.Combine(_repositoryRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(full))
        {
            return null; // absence is already a freshness/missing concern, not an output-validation one
        }

        // A real open marker carries a question - the form `[NEEDS CLARIFICATION: <q>]` (with a
        // colon). A bare, backticked `[NEEDS CLARIFICATION]` is a mention of the convention, so match
        // only the colon form to avoid false positives.
        return File.ReadAllText(full).Contains("[NEEDS CLARIFICATION:", StringComparison.Ordinal)
            ? $"artifact '{artifactPath}' has an open [NEEDS CLARIFICATION:] marker"
            : null;
    }
}
