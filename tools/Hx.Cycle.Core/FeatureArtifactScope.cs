namespace Hx.Cycle.Core;

/// <summary>
/// The set of repo-relative artifact paths a feature OWNS — every stage's <c>produces</c> pattern resolved against
/// the feature slug, deduped and deterministically ordered. Single-responsibility, IO-free, testable. Used to
/// subtract a feature's footprint: the incoming feature's owned paths on a new-feature start so they do not read as
/// the prior feature's dirty tree (FR-038), and an earlier feature's owned paths for release-train drift (FR-037).
/// Exact-path membership (never a prefix) — a stray file under the same directory is NOT owned.
/// </summary>
public static class FeatureArtifactScope
{
    public static IReadOnlyList<string> OwnedPaths(StageModel stageModel, string feature)
    {
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (CycleStage stage in stageModel.Stages)
        {
            if (stage.Produces is { } pattern)
            {
                paths.Add(StageModel.ResolveProduces(pattern, feature).Replace('\\', '/'));
            }
        }

        return paths.ToArray();
    }
}
