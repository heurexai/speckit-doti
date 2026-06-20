using Hx.Runner.Core.Io;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public enum StageFreshness
{
    Fresh,
    Stale,
}

/// <summary>Per-stage freshness verdict computed at read time (never stored).</summary>
public sealed record StageFreshnessResult(string Stage, StageFreshness Freshness, string? Reason);

/// <summary>
/// Re-derives whether a stamped stage is still fresh: (1) its recorded change-set identity must equal the
/// current one (the diff has not moved), and (2) its produced artifact (for doc stages) must re-hash to a
/// recorded value (the artifact has not changed). Any mismatch ⇒ stale. Pure given the repo + inputs.
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

    public StageFreshnessResult Evaluate(CycleStageProof proof, string feature, string currentIdentity)
    {
        if (!string.Equals(proof.ChangeSetId, currentIdentity, StringComparison.Ordinal))
        {
            return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                "change-set identity differs from the current diff (code changed since stamp)");
        }

        CycleStage stage = _stageModel.Find(proof.Stage);
        if (stage.Produces is { } pattern)
        {
            string artifactPath = ResolveProduces(pattern, feature);
            string full = Path.GetFullPath(Path.Combine(_repositoryRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
            string current = File.Exists(full) ? FileHashing.Sha256OfFile(full) : "absent";
            if (proof.ArtifactHashes.Count == 0 || !proof.ArtifactHashes.Contains(current))
            {
                return new StageFreshnessResult(proof.Stage, StageFreshness.Stale,
                    $"artifact '{artifactPath}' changed since stamp");
            }
        }

        return new StageFreshnessResult(proof.Stage, StageFreshness.Fresh, null);
    }

    public static string ResolveProduces(string producesPattern, string feature) =>
        producesPattern.Replace("{feature}", feature);
}
