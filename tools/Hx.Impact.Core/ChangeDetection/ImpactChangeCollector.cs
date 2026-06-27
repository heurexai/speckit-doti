namespace Hx.Impact.Core.ChangeDetection;

/// <summary>
/// Collects the changed repo-relative paths for a change set: the committed diff
/// <c>merge-base(base,head)..head</c> UNION the working tree (staged + unstaged + untracked). A thin adapter over
/// <see cref="ChangeSetParser"/> (so its path set is byte-identical to <see cref="ChangeSetContextBuilder"/> — BL-3);
/// all paths are '/'-normalized. Fails closed (throws) if the merge-base cannot be resolved — the change-set
/// identity must never be computed from a misleading set.
/// </summary>
public sealed class ImpactChangeCollector
{
    private readonly IGitChangeSource _source;

    public ImpactChangeCollector(IGitChangeSource? source = null) => _source = source ?? new GitChangeSource();

    public IReadOnlyList<string> Collect(string repositoryRoot, string baseRef, string headRef)
    {
        GitChangeOutputs git = _source.Read(repositoryRoot, baseRef, headRef, includeWorkingTree: true);
        if (!git.MergeBaseResolved)
        {
            throw new InvalidOperationException(
                git.UnresolvedReason ?? $"Could not resolve merge-base for '{baseRef}'..'{headRef}'.");
        }

        return ChangeSetParser.Parse(git.DiffNameStatusZ, git.StatusPorcelainZ)
            .Select(file => file.Path)
            .ToArray();
    }
}
