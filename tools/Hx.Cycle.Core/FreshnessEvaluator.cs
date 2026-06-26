using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public enum StageFreshness
{
    Fresh,
    Stale,
    Completed,
}

/// <summary>Per-stage freshness verdict computed at read time (never stored).</summary>
public sealed record StageFreshnessResult(string Stage, StageFreshness Freshness, string? Reason);

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
                "change-set identity differs from the current diff (code changed since stamp)");
        }

        CycleStage stage = _stageModel.Find(proof.Stage);
        if (stage.Produces is { } pattern)
        {
            string artifactPath = ResolveProduces(pattern, feature);
            string full = Path.GetFullPath(Path.Combine(_repositoryRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
            string current = File.Exists(full) ? CanonicalArtifactHasher.CanonicalHashOfFile(full) : "absent";
            if (proof.ArtifactHashes.Count == 0 || !proof.ArtifactHashes.Contains(current))
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    $"artifact '{artifactPath}' changed since stamp");
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
                    "missing prerequisite artifact binding; re-stamp with the current runner");
            }

            if (!proof.PrerequisiteArtifactHashes.SequenceEqual(currentPrereqs, StringComparer.Ordinal))
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    "a prerequisite artifact changed since stamp; re-clarify/re-analyze the dependent");
            }
        }

        return new StageFreshnessResult(proof.Stage, StageFreshness.Fresh, null);
    }

    public static string ResolveProduces(string producesPattern, string feature) =>
        producesPattern.Replace("{feature}", feature);
}
