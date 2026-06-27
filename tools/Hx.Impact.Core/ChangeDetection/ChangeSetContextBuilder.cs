using Hx.Impact.Core.Domain;
using Hx.Impact.Core.Graph;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;

namespace Hx.Impact.Core.ChangeDetection;

/// <summary>
/// Builds the rich <see cref="ChangeSetContext"/> for a base..head change set (FR-008/009/011) by reading the git
/// seam and parsing it with <see cref="ChangeSetParser"/> — the same parse the bare-path <see cref="ImpactChangeCollector"/>
/// uses, so the two never diverge (BL-3). Fail-closed: an unresolved merge-base yields a <c>RefsResolved=false</c>
/// context (the projection path is advisory; only the change-set-identity path throws — see <see cref="ImpactChangeCollector"/>).
/// <see cref="AffectedSourceProjects"/> is resolved only when a project graph is supplied (no graph re-walk, no
/// solution discovery coupling).
/// </summary>
public sealed class ChangeSetContextBuilder
{
    private readonly IGitChangeSource _source;

    public ChangeSetContextBuilder(IGitChangeSource? source = null) => _source = source ?? new GitChangeSource();

    /// <summary>Build the change-set context for a repository, discovering its single <c>.slnx</c> solution and the
    /// project graph so <see cref="ChangeSetContext.AffectedSourceProjects"/> is resolved (the production entry point
    /// for <c>hx doti review-context</c> / <c>hx impact plan --for change-context</c>). When no single solution is
    /// found the context is still valid with no affected projects.</summary>
    public ChangeSetContext BuildForRepo(string repositoryRoot, string baseRef, string headRef, bool includeWorkingTree = true)
    {
        string[] solutions = Directory.GetFiles(repositoryRoot, "*.slnx");
        ProjectGraph? graph = solutions.Length == 1
            ? new ProjectGraphBuilder().Build(repositoryRoot, Path.GetFileName(solutions[0]))
            : null;
        return Build(repositoryRoot, baseRef, headRef, includeWorkingTree, graph);
    }

    public ChangeSetContext Build(
        string repositoryRoot,
        string baseRef,
        string headRef,
        bool includeWorkingTree = true,
        ProjectGraph? graph = null)
    {
        GitChangeOutputs git = _source.Read(repositoryRoot, baseRef, headRef, includeWorkingTree);
        if (!git.MergeBaseResolved)
        {
            return new ChangeSetContext(
                JsonContractDefaults.SchemaVersion, baseRef, headRef, string.Empty,
                includeWorkingTree, RefsResolved: false, git.UnresolvedReason, [], []);
        }

        IReadOnlyList<ChangedFile> files = ChangeSetParser.Parse(git.DiffNameStatusZ, git.StatusPorcelainZ);
        IReadOnlyList<string> affectedProjects = graph is null
            ? []
            : AffectedTestPlanner.Resolve(graph, files.Select(f => f.Path).ToArray(), "Release").AffectedSourceProjects;
        return new ChangeSetContext(
            JsonContractDefaults.SchemaVersion, baseRef, headRef, git.BaseSha!,
            includeWorkingTree, RefsResolved: true, UnresolvedReason: null, files, affectedProjects);
    }
}
