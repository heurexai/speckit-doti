using Hx.Embedding;
using Hx.Impact.Core.ChangeDetection;
using Hx.Tooling.Contracts;

namespace Hx.Semantic;

/// <summary>
/// End-to-end advisory drift-candidate run, shared by <c>hx doti drift-candidates</c> (composed in the packed tool)
/// and the dev-only <c>Hx.Semantic.Cli</c>: build the change set (no own <c>git diff</c>), select the engine
/// (Qwen3 → BGE-M3 fallback, M-3) against the operator-resolved model root (FR-041: config wins, then
/// <c>HEUREX_LLM_ROOT</c>), and run the finder — reporting the active engine (FR-042). Throws
/// <see cref="SemanticException"/> when no engine can be loaded (no provisioned model); the CLI surfaces that as an
/// advisory skip, never a gate failure.
/// </summary>
public static class DriftCandidateRunner
{
    public static DriftCandidatesResult Run(string repositoryRoot, string baseRef, string? modelRoot, double? threshold)
    {
        string root = Path.GetFullPath(repositoryRoot);
        ChangeSetContext changeSet = new ChangeSetContextBuilder().BuildForRepo(root, baseRef, "HEAD");
        if (!changeSet.RefsResolved)
        {
            throw new SemanticException(changeSet.UnresolvedReason ?? "Could not resolve the change set.");
        }

        var selector = new EngineSelector((id, options) =>
            new SemanticEngineFactory(new ModelLocator(string.IsNullOrWhiteSpace(modelRoot) ? null : modelRoot)).Create(id, options));
        using EngineSelection engine = selector.Select();
        return new DriftCandidateService().Run(root, changeSet, engine, threshold);
    }
}
