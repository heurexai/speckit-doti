using Hx.Runner.Core.Git;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// 022 T044 (FR-013/014/015): orchestrate a safe, previewable update — compose <see cref="GitWorktree"/> (isolation)
/// with <see cref="DotiUpdater"/> (reconciliation). The reconcile runs INSIDE a worktree at the repo's HEAD; the
/// resulting change set is captured and applied back to the real repo, or — with <paramref name="dryRun"/> — only
/// previewed (the real working tree is never touched). Git is required: an absent git binary / non-git target is
/// reported as <see cref="DotiUpdateStatus.GitRequired"/> (fail hard, FR-014). A non-Doti directory is reported
/// before any worktree is created.
/// </summary>
public static class DotiWorktreeUpdate
{
    public static DotiUpdateOutcome Run(
        string payloadRoot,
        string repoRoot,
        IReadOnlyList<DotiAgentTarget> agents,
        string installedToolVersion,
        bool force,
        bool dryRun,
        bool commit = true,
        string? sourceOrigin = null)
    {
        string root = Path.GetFullPath(repoRoot);
        if (!Directory.Exists(Path.Combine(root, ".doti")))
        {
            return Simple(root, DotiUpdateStatus.NotARepo, installedToolVersion, dryRun,
                "No .doti directory — not a Doti-enabled repository.", sourceOrigin);
        }

        try
        {
            GitWorktree.EnsureGitAvailable(root);
        }
        catch (GitUnavailableException ex)
        {
            return Simple(root, DotiUpdateStatus.GitRequired, installedToolVersion, dryRun, ex.Message, sourceOrigin);
        }

        // 032 D1(a): sweep any prior run's leaked temp worktree BEFORE creating this run's — a leak left registered
        // can otherwise collide with (or be mistaken for) this run's worktree, and it never self-heals once `git
        // worktree prune` stops being invoked on a clean Dispose. Best-effort: a sweep failure never blocks the
        // update (the failure is instrumented on GitWorktreePruneEntry.Reason for the caller to inspect/log).
        GitWorktree.PruneLeakedTemps(root);

        try
        {
            // 032 D1(b): the worktree-scoped phase (create -> reconcile -> capture -> apply-back -> DISPOSE) is fully
            // isolated in RunWorktreeScoped, which tears the worktree down (its `.git/worktrees` registration
            // removed) before returning. The self-commit below then runs against `root` with NO worktree registered,
            // closing the race where `git add`/`git commit` raced the worktree's own `.git` index/lock.
            (DotiUpdateOutcome rekeyed, bool applied) = RunWorktreeScoped(
                payloadRoot, root, agents, installedToolVersion, force, dryRun, sourceOrigin);

            // 031 FR-007/008/009/010 (D4): the reconcile owns its commit on the REAL repo (after ApplyBack), staging
            // exactly the touched paths. A dry-run never commits; --no-commit (commit=false) reports Disabled; an
            // already-current/no-change reconcile stages nothing → NoChange (no empty commit).
            if (!applied)
            {
                return rekeyed;
            }

            DotiReconcileCommitOutcome commitOutcome = DotiReconcileCommit.Commit(
                root, DotiReconcileCommit.TouchedPaths(rekeyed), rekeyed.BeforeVersion, rekeyed.AfterVersion,
                rekeyed.Pruned ?? [], commit);
            return rekeyed with { Commit = commitOutcome };
        }
        catch (GitUnavailableException ex)
        {
            return Simple(root, DotiUpdateStatus.GitRequired, installedToolVersion, dryRun, ex.Message, sourceOrigin);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return Simple(root, DotiUpdateStatus.Failed, installedToolVersion, dryRun, ex.Message, sourceOrigin);
        }
    }

    /// <summary>
    /// 032 D1(b): create the worktree, reconcile inside it, capture + apply the change set back to <paramref
    /// name="root"/>, and dispose the worktree — all BEFORE returning — so the caller's self-commit never runs while
    /// this run's worktree is still registered. Re-keys the outcome to <paramref name="root"/> (the updater saw the
    /// worktree path) and folds in the dry-run status. Any exception during the scoped phase still disposes the
    /// worktree (the inner <c>finally</c>) before propagating to the caller's catch blocks.
    /// </summary>
    private static (DotiUpdateOutcome Rekeyed, bool Applied) RunWorktreeScoped(
        string payloadRoot,
        string root,
        IReadOnlyList<DotiAgentTarget> agents,
        string installedToolVersion,
        bool force,
        bool dryRun,
        string? sourceOrigin)
    {
        GitWorktree worktree = GitWorktree.Create(root);
        try
        {
            DotiUpdateOutcome outcome = DotiUpdater.Update(
                payloadRoot, worktree.WorktreePath, agents, installedToolVersion, force, sourceOrigin);

            IReadOnlyList<GitWorktreeChange> changes = worktree.CaptureChanges();
            bool applied = !dryRun && outcome.Status is DotiUpdateStatus.Updated or DotiUpdateStatus.AlreadyCurrent;
            if (applied)
            {
                worktree.ApplyBack(changes);
            }

            // Re-key the outcome to the real repo (the updater saw the worktree path) and carry the dry-run flag/status.
            DotiUpdateOutcome rekeyed = outcome with
            {
                RepoPath = root,
                DryRun = dryRun,
                Status = dryRun && outcome.Status is DotiUpdateStatus.Updated or DotiUpdateStatus.AlreadyCurrent
                    ? DotiUpdateStatus.DryRun
                    : outcome.Status,
            };
            return (rekeyed, applied);
        }
        finally
        {
            // Disposed HERE — before RunWorktreeScoped returns — so by the time the caller runs the self-commit, the
            // worktree's `.git/worktrees` registration is already gone (the D1(b) fix).
            worktree.Dispose();
        }
    }

    private static DotiUpdateOutcome Simple(
        string root, string status, string installedToolVersion, bool dryRun, string reason, string? sourceOrigin) =>
        new(JsonContractDefaults.SchemaVersion, root, status, null, null, installedToolVersion,
            DotiVersionRelation.Unknown, DotiVersionRelation.Unknown, dryRun, [], [], reason,
            sourceOrigin, [], [], Commit: null);
}
