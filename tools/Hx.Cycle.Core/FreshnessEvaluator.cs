using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public enum StageFreshness
{
    Fresh,
    Stale,
    Completed,
}

/// <summary>
/// The category of a stale verdict (FR-005/006/007) — the machine-readable "why" the restamp-safety classifier
/// (1a) consumes, so refresh can never disagree with this evaluator: <see cref="ChangeSetDiffers"/> (code moved),
/// <see cref="OwnArtifactChanged"/> (the stage's own artifact content changed), <see cref="NotProduced"/> (its
/// produced artifact is absent), <see cref="MissingArtifactBinding"/> (the produced artifact is present but the
/// proof has no bound hash — the produces-binding migration, a safe re-stamp), <see cref="MissingBinding"/> (a
/// runner/schema bump left the prerequisite-artifact binding null — also a safe re-stamp), and
/// <see cref="PrereqArtifactChanged"/> (an upstream artifact's content changed — a real input change).
/// </summary>
public enum StaleReason
{
    ChangeSetDiffers,
    OwnArtifactChanged,
    PrereqArtifactChanged,
    MissingArtifactBinding,
    MissingBinding,
    NotProduced,
}

/// <summary>Per-stage freshness verdict computed at read time (never stored). <see cref="StaleReason"/> is the
/// machine-readable category when <see cref="Freshness"/> is <see cref="StageFreshness.Stale"/>; null otherwise.</summary>
public sealed record StageFreshnessResult(string Stage, StageFreshness Freshness, string? Reason, StaleReason? StaleReason = null);

/// <summary>
/// Re-derives whether a stamped stage is still fresh. Code-bound stages must keep the same change-set
/// identity; doc/review stages that happen before implementation are bound by their artifact and
/// prerequisite hashes so normal implementation edits do not stale the entire pre-workflow.
/// </summary>
public sealed class FreshnessEvaluator
{
    private readonly string _repositoryRoot;
    private readonly StageModel _stageModel;

    public FreshnessEvaluator(string repositoryRoot, StageModel stageModel)
    {
        _repositoryRoot = repositoryRoot;
        _stageModel = stageModel;
    }

    public StageFreshnessResult Evaluate(
        CycleStageProof proof,
        string feature,
        string currentIdentity,
        bool requireChangeSetIdentity = true)
    {
        if (requireChangeSetIdentity
            && !string.Equals(proof.ChangeSetId, currentIdentity, StringComparison.Ordinal))
        {
            return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                "change-set identity differs from the current diff (code changed since stamp)",
                StaleReason.ChangeSetDiffers);
        }

        CycleStage stage = _stageModel.Find(proof.Stage);
        if (stage.Produces is { } pattern)
        {
            string artifactPath = StageModel.ResolveProduces(pattern, feature);
            string full = Path.GetFullPath(Path.Combine(_repositoryRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
            bool present = File.Exists(full);
            if (!present)
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    $"artifact '{artifactPath}' is absent (never produced)", StaleReason.NotProduced);
            }

            if (proof.ArtifactHashes.Count == 0)
            {
                // Present but never bound: the produces-binding migration (a stage stamped before it had a
                // produces pattern). Re-stamping safely binds the current content — not a content change.
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    $"artifact '{artifactPath}' is present but unbound; re-stamp with the current runner",
                    StaleReason.MissingArtifactBinding);
            }

            string current = CanonicalArtifactHasher.CanonicalHashOfFile(full);
            if (!proof.ArtifactHashes.Contains(current))
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    $"artifact '{artifactPath}' changed since stamp", StaleReason.OwnArtifactChanged);
            }
        }

        // Living-Spec (FR-027): a stage also binds the canonical CONTENT of its transitive prerequisite
        // artifacts, so editing the spec stales a dependent plan/tasks even though its own file is
        // untouched — forcing re-clarify/re-analyze. Re-stamping an upstream stage WITHOUT a content change
        // does NOT stale it (the binding is to content, not the upstream proof), removing the re-stamp cascade.
        IReadOnlyList<string> currentPrereqs =
            CanonicalArtifactHasher.PrerequisiteArtifactHashes(_repositoryRoot, _stageModel, proof.Stage, feature);
        if (currentPrereqs.Count > 0)
        {
            if (proof.PrerequisiteArtifactHashes is null)
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    "missing prerequisite artifact binding; re-stamp with the current runner",
                    StaleReason.MissingBinding);
            }

            if (!proof.PrerequisiteArtifactHashes.SequenceEqual(currentPrereqs, StringComparer.Ordinal))
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    "a prerequisite artifact changed since stamp; re-clarify/re-analyze the dependent",
                    StaleReason.PrereqArtifactChanged);
            }
        }

        return new StageFreshnessResult(proof.Stage, StageFreshness.Fresh, null);
    }
}
