namespace Hx.Tooling.Contracts;

/// <summary>The kind of change git reports for a path (diff name-status letter / porcelain XY code).</summary>
public enum ChangeStatus
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Untracked,
    Unmerged,
    Unknown,
}

/// <summary>
/// One changed path in a <see cref="ChangeSetContext"/>: its repo-relative path, the change kind, and (for a
/// rename/copy) the path it came from. The path is always the NEW path; <see cref="OldPath"/> is the source.
/// </summary>
public sealed record ChangedFile(string Path, ChangeStatus Status, string? OldPath);

/// <summary>
/// The review/change context for a base..head change set (FR-008/009/011): the resolved refs + base SHA, the
/// deduped changed-file set (status/rename metadata), whether the working tree was unioned in, and the affected
/// source projects. Fail-closed: <see cref="RefsResolved"/> is false (with <see cref="UnresolvedReason"/>) when the
/// merge-base cannot be resolved — callers never receive a misleading change set. This is REVIEW context, not a
/// proof input; it must never enter a gate-proof hash (FR-020/SC-009).
/// </summary>
public sealed record ChangeSetContext(
    int SchemaVersion,
    string BaseRef,
    string HeadRef,
    string BaseSha,
    bool IncludesWorkingTree,
    bool RefsResolved,
    string? UnresolvedReason,
    IReadOnlyList<ChangedFile> Files,
    IReadOnlyList<string> AffectedSourceProjects);
