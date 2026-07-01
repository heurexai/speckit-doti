namespace Hx.Tooling.Contracts;

/// <summary>
/// 022 (FR-002): a repo's Doti payload version relative to the installed tool. Single-sourced by
/// <c>DotiVersionRelationCalculator</c> (the same comparison <c>version --repo</c> uses). Serialized camelCase:
/// <c>unknown</c>/<c>current</c>/<c>outdated</c>/<c>ahead</c>.
/// </summary>
public enum DotiVersionRelation
{
    /// <summary>No recorded payload version to compare (not a Doti repo, or no <c>.doti/payload.json</c>).</summary>
    Unknown,

    /// <summary>The repo's payload version equals the installed tool — up to date.</summary>
    Current,

    /// <summary>The repo's payload version is older than the installed tool — an update is available.</summary>
    Outdated,

    /// <summary>The repo's payload version is newer than the installed tool — never downgraded (FR-011).</summary>
    Ahead,
}

/// <summary>022 (FR-001/004): the read outcome of inspecting one repo's Doti version.</summary>
public static class DotiVersionStatus
{
    public const string Ok = "ok";                       // a Doti repo with a readable payload version
    public const string NotARepo = "not-a-repo";         // no .doti directory
    public const string VersionUnknown = "version-unknown"; // .doti present but no/unreadable payload.json
}

/// <summary>
/// 022 (FR-001/002/004): a repo's Doti version + its relation to the installed tool, the <c>data</c> ring of
/// <c>hx doti check-version</c>. <see cref="Status"/> distinguishes not-a-repo from version-unknown so the human
/// line and the agent JSON both explain a missing version rather than silently reporting <c>unknown</c>.
/// </summary>
public sealed record DotiRepoVersion(
    int SchemaVersion,
    string RepoPath,
    string Status,
    string? PayloadVersion,
    string? RecordedToolVersion,
    string InstalledToolVersion,
    DotiVersionRelation Relation,
    string? Reason);

/// <summary>022 (FR-005): one repo's row in a <see cref="DotiScanResult"/>.</summary>
public sealed record DotiScanEntry(
    string RepoPath,
    string Status,
    string? PayloadVersion,
    string InstalledToolVersion,
    DotiVersionRelation Relation,
    string? Reason);

/// <summary>
/// 022 (FR-005/006/007): every Doti repo discovered under a root + its version/relation. Read-only and
/// error-tolerant — an unreadable repo becomes a <see cref="DotiVersionRelation.Unknown"/> entry with a reason,
/// never an aborted scan. An empty tree is an explicit success with zero entries.
/// </summary>
public sealed record DotiScanResult(
    int SchemaVersion,
    string Root,
    string InstalledToolVersion,
    int Count,
    IReadOnlyList<DotiScanEntry> Repos);

/// <summary>022 (FR-009/010): one asset's effect during an update — the reused install path-effect, flattened.</summary>
public sealed record DotiAssetOutcome(string Path, string Effect, string Reason);

/// <summary>031 (FR-007/008/009/010/011, D4/D5): the outcome of the self-owned sanctioned reconcile commit.</summary>
public static class DotiCommitStatus
{
    public const string Committed = "committed";   // a single sanctioned commit was made over exactly the touched paths
    public const string NoChange = "no-change";    // nothing staged (idempotent re-run) — no commit
    public const string NonGit = "non-git";        // target is not a git work tree — reconcile applied, commit skipped
    public const string Disabled = "disabled";     // --no-commit — reconciled changes left in the working tree
    public const string Failed = "failed";         // git staging/commit failed (reported, never silently swallowed)
    // 034 (bug-only-release-doc-commit): a fail-closed GATE refusal — distinct from Failed (a git-level error). The
    // command refused BEFORE any git mutation because no release-ready bug member justified the commit.
    public const string Refused = "refused";
}

/// <summary>
/// 031 (FR-007/008/009/010/011, D4): the self-owned reconcile-commit outcome reported on the update/install result.
/// Carries the commit <see cref="Sha"/> when a commit was made, the exact <see cref="StagedPaths"/> it staged (never
/// `git add -A`), or the skip <see cref="Reason"/> (no change / non-git / --no-commit / failure).
/// </summary>
public sealed record DotiReconcileCommitOutcome(
    string Status,
    string? Sha,
    IReadOnlyList<string> StagedPaths,
    string? Reason);

/// <summary>
/// 034 (bug-only-release-doc-commit): the outcome of <c>Hx.Doti.Core.Bug.BugReleaseDocCommit.Commit</c> — the
/// sanctioned, GATED commit for the release-documentation fix (README.md/CHANGELOG.md) a bug-only release train
/// demands. <see cref="EligibleBugMembers"/> names the release-ready bug member(s) that justified proceeding (empty
/// on a <see cref="DotiCommitStatus.Refused"/> or <see cref="DotiCommitStatus.NonGit"/> outcome, since the gate never
/// ran or found nothing).
/// </summary>
public sealed record BugReleaseDocCommitOutcome(
    string Status,
    string? Sha,
    IReadOnlyList<string> StagedPaths,
    IReadOnlyList<CycleReleaseTrainFeature> EligibleBugMembers,
    string? Reason);

/// <summary>022: the per-repo update outcome status.</summary>
public static class DotiUpdateStatus
{
    public const string Updated = "updated";                 // moved forward to the installed payload
    public const string AlreadyCurrent = "already-current";  // equal version, nothing to move
    public const string Ahead = "ahead";                     // repo newer than the tool — refused, never downgraded
    public const string DryRun = "dry-run";                  // previewed in the worktree, not applied back
    public const string NotARepo = "not-a-repo";             // no .doti directory
    public const string GitRequired = "git-required";        // git absent / not a git repo
    public const string Failed = "failed";                   // reconciliation/worktree error
}

/// <summary>
/// 022 (FR-008/009/010/015): a single repo's before→after update, the <c>data</c> ring of <c>hx doti update</c>.
/// Reports the version it moved from and to (the headline gap the user asked for), the operator customizations it
/// preserved (or overwrote with <c>--force</c>), and the managed-asset changes it applied — all derived from the
/// reused <see cref="DotiInstallResult"/> so there is no second reconciliation scheme.
/// </summary>
public sealed record DotiUpdateOutcome(
    int SchemaVersion,
    string RepoPath,
    string Status,
    string? BeforeVersion,
    string? AfterVersion,
    string InstalledToolVersion,
    DotiVersionRelation BeforeRelation,
    DotiVersionRelation AfterRelation,
    bool DryRun,
    IReadOnlyList<DotiAssetOutcome> Customizations,
    IReadOnlyList<DotiAssetOutcome> Changes,
    string? Reason,
    // 031 D5/FR-011 (additive trailing-optional): the resolved source origin (bundled/dev-cwd), the pruned-orphan
    // paths, the `.new` merge-pending list (D3), and the self-owned commit outcome (D4) — surfaced in --json + the
    // human summary. Null/empty on paths that do not set them (e.g. not-a-repo, dry-run, or a pre-031 caller).
    string? SourceOrigin = null,
    IReadOnlyList<string>? Pruned = null,
    IReadOnlyList<DotiAssetOutcome>? MergePending = null,
    DotiReconcileCommitOutcome? Commit = null);

/// <summary>
/// 022 (FR-016/017/018): the aggregate of a batch update under a root, the <c>data</c> ring of
/// <c>hx doti update-all</c>. Fail-soft — one repo's failure is a <see cref="DotiUpdateStatus.Failed"/> entry, not
/// an aborted batch — with the updated / already-current / failed counts the user asked for.
/// </summary>
public sealed record DotiUpdateAllSummary(
    int SchemaVersion,
    string Root,
    string InstalledToolVersion,
    bool DryRun,
    int Total,
    int Updated,
    int AlreadyCurrent,
    int Failed,
    IReadOnlyList<DotiUpdateOutcome> Repos);
