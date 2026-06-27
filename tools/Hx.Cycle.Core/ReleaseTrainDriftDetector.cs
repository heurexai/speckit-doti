using Hx.Impact.Core.ChangeDetection;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>
/// FR-037/SC-019: cross-feature release-train drift. For each EARLIER completed-unreleased feature, intersect its
/// owned footprint (<see cref="FeatureArtifactScope.OwnedPaths"/>) with what a LATER feature changed over the
/// pairwise commit range — a non-empty intersection means the later feature changed behaviour the earlier feature
/// owns/documents, so the train is internally inconsistent. Deterministic commit-range analysis, not heuristics.
/// H-1: the pairwise diff EXCLUDES the working tree, so in-flight uncommitted edits are never attributed to the
/// later feature (a false positive). M-9: the historical-diff collector is injected so the detector is unit-testable.
/// </summary>
public sealed class ReleaseTrainDriftDetector
{
    private readonly Func<string, string, string, IReadOnlyList<string>> _historicalChangedPaths;

    public ReleaseTrainDriftDetector(Func<string, string, string, IReadOnlyList<string>>? historicalChangedPaths = null) =>
        _historicalChangedPaths = historicalChangedPaths ?? DefaultHistoricalChangedPaths;

    public IReadOnlyList<ReleaseTrainDriftFinding> Detect(
        string repositoryRoot, StageModel stageModel, IReadOnlyList<CycleReleaseTrainFeature> features)
    {
        var findings = new List<ReleaseTrainDriftFinding>();
        for (int i = 0; i < features.Count; i++)
        {
            CycleReleaseTrainFeature earlier = features[i];
            var earlierOwned = new HashSet<string>(
                FeatureArtifactScope.OwnedPaths(stageModel, earlier.Feature), StringComparer.OrdinalIgnoreCase);

            for (int j = i + 1; j < features.Count; j++)
            {
                CycleReleaseTrainFeature later = features[j];
                if (string.IsNullOrWhiteSpace(earlier.CommitSha) || string.IsNullOrWhiteSpace(later.CommitSha))
                {
                    continue;
                }

                List<string> conflicts = _historicalChangedPaths(repositoryRoot, earlier.CommitSha, later.CommitSha)
                    .Where(earlierOwned.Contains)
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToList();
                if (conflicts.Count > 0)
                {
                    findings.Add(new ReleaseTrainDriftFinding(
                        earlier.Feature, later.Feature, conflicts,
                        $"feature '{later.Feature}' changed {conflicts.Count} path(s) owned/documented by the earlier release-train feature '{earlier.Feature}'"));
                }
            }
        }

        return findings;
    }

    // The working-tree-EXCLUDING historical diff for the pairwise range (H-1). Fail-closed: an unresolvable range
    // (the merge-base does not resolve) yields no paths rather than a misleading set.
    private static IReadOnlyList<string> DefaultHistoricalChangedPaths(string repositoryRoot, string earlierSha, string laterSha)
    {
        ChangeSetContext context = new ChangeSetContextBuilder()
            .Build(repositoryRoot, earlierSha, laterSha, includeWorkingTree: false);
        return context.RefsResolved ? context.Files.Select(f => f.Path).ToArray() : [];
    }
}
