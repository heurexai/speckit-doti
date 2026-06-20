namespace Hx.Runner.Core.Git;

public enum ChangeKind
{
    Added,
    Modified,
    Renamed,
    Copied,
    Deleted,
    Other
}

/// <summary>A changed path discovered from the Git index or a ref range, with a repo-relative <c>/</c> path.</summary>
public sealed record ChangedFile(string Path, ChangeKind Kind);
