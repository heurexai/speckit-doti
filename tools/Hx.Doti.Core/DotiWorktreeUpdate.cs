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
        bool dryRun)
    {
        string root = Path.GetFullPath(repoRoot);
        if (!Directory.Exists(Path.Combine(root, ".doti")))
        {
            return Simple(root, DotiUpdateStatus.NotARepo, installedToolVersion, dryRun,
                "No .doti directory — not a Doti-enabled repository.");
        }

        try
        {
            GitWorktree.EnsureGitAvailable(root);
        }
        catch (GitUnavailableException ex)
        {
            return Simple(root, DotiUpdateStatus.GitRequired, installedToolVersion, dryRun, ex.Message);
        }

        GitWorktree? worktree = null;
        try
        {
            worktree = GitWorktree.Create(root);
            DotiUpdateOutcome outcome = DotiUpdater.Update(
                payloadRoot, worktree.WorktreePath, agents, installedToolVersion, force);

            IReadOnlyList<GitWorktreeChange> changes = worktree.CaptureChanges();
            if (!dryRun && outcome.Status is DotiUpdateStatus.Updated or DotiUpdateStatus.AlreadyCurrent)
            {
                worktree.ApplyBack(changes);
            }

            // Re-key the outcome to the real repo (the updater saw the worktree path) and carry the dry-run flag/status.
            return outcome with
            {
                RepoPath = root,
                DryRun = dryRun,
                Status = dryRun && outcome.Status is DotiUpdateStatus.Updated or DotiUpdateStatus.AlreadyCurrent
                    ? DotiUpdateStatus.DryRun
                    : outcome.Status,
            };
        }
        catch (GitUnavailableException ex)
        {
            return Simple(root, DotiUpdateStatus.GitRequired, installedToolVersion, dryRun, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return Simple(root, DotiUpdateStatus.Failed, installedToolVersion, dryRun, ex.Message);
        }
        finally
        {
            worktree?.Dispose();
        }
    }

    private static DotiUpdateOutcome Simple(
        string root, string status, string installedToolVersion, bool dryRun, string reason) =>
        new(JsonContractDefaults.SchemaVersion, root, status, null, null, installedToolVersion,
            DotiVersionRelation.Unknown, DotiVersionRelation.Unknown, dryRun, [], [], reason);
}
