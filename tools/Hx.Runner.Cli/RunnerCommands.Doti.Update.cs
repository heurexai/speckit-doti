using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // 022 T045 (FR-008/014/015/019) + 031 (FR-001/002/007/009/011): update one repo's managed Doti assets to the
    // installed payload, reporting the before→after version. The source defaults to the running tool's BUNDLED payload
    // (fail closed if unresolvable); the reconcile runs in a git worktree (--dry-run previews; git required), and on a
    // successful apply the command makes a single sanctioned commit it owns (default on; --no-commit opts out).
    public static CliResult DotiUpdate(CliMeta meta, string? repo, string agentsCsv, bool force, bool dryRun, bool noCommit)
    {
        if (string.IsNullOrWhiteSpace(repo))
        {
            return Usage(meta, "doti update", "doti update requires an explicit --repo <path>.");
        }

        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti update", error!);
        }

        if (!TryResolveDotiPayloadSource(meta, "doti update", out string source, out string origin, out CliResult? failure))
        {
            return failure!;
        }

        DotiUpdateOutcome outcome = DotiWorktreeUpdate.Run(
            source, repo, agents, meta.Version, force, dryRun, commit: !noCommit, sourceOrigin: origin);
        return outcome.Status switch
        {
            DotiUpdateStatus.NotARepo => CliResults.Fail(meta, "doti update", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_DotiNotARepo, outcome.Reason!, target: outcome.RepoPath)],
                $"{outcome.RepoPath} is not a Doti-enabled repository.", outcome),
            DotiUpdateStatus.GitRequired => CliResults.Fail(meta, "doti update", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_GitRequired, outcome.Reason!, target: outcome.RepoPath)],
                "git is required for `hx doti update`.", outcome),
            DotiUpdateStatus.Failed => CliResults.Fail(meta, "doti update", ExitClass.Integrity,
                [Diag.Of(ErrorCodes.Integrity_DotiUpdateFailed, outcome.Reason ?? "update failed", target: outcome.RepoPath)],
                $"Doti update of {outcome.RepoPath} failed.", outcome),
            _ => CliResults.Ok(meta, "doti update", UpdateSummary(outcome), outcome),
        };
    }

    private static string UpdateSummary(DotiUpdateOutcome o)
    {
        string head = o.Status switch
        {
            DotiUpdateStatus.Updated => $"Updated {o.RepoPath}: Doti {o.BeforeVersion ?? "—"} → {o.AfterVersion}.",
            DotiUpdateStatus.AlreadyCurrent => $"{o.RepoPath} already current at Doti {o.AfterVersion}.",
            DotiUpdateStatus.Ahead =>
                $"{o.RepoPath} is ahead (Doti {o.BeforeVersion}) of the installed tool {o.InstalledToolVersion}; not downgraded.",
            DotiUpdateStatus.DryRun =>
                $"[dry-run] {o.RepoPath}: Doti {o.BeforeVersion ?? "—"} → {o.AfterVersion} (preview; no changes applied).",
            _ => $"{o.RepoPath}: {o.Status}.",
        };

        var lines = new List<string> { head };
        if (!string.IsNullOrEmpty(o.SourceOrigin))
        {
            lines.Add($"  source: {o.SourceOrigin}.");
        }

        AppendUpdateDetails(o, lines);
        return string.Join("\n", lines);
    }

    // 031 FR-011: surface the pruned orphans, the .new merge-pending list, the preserved customizations, and the
    // self-owned commit outcome in the human summary (the --json envelope carries the structured fields).
    private static void AppendUpdateDetails(DotiUpdateOutcome o, List<string> lines)
    {
        if (o.Pruned is { Count: > 0 } pruned)
        {
            lines.Add($"  pruned {pruned.Count} orphaned managed asset(s) the render no longer targets.");
        }

        if (o.Customizations.Count > 0)
        {
            lines.Add($"  preserved {o.Customizations.Count} customization(s) (rerun with --force to overwrite):");
            foreach (DotiAssetOutcome c in o.Customizations)
            {
                lines.Add($"    {c.Effect}: {c.Path}");
            }
        }

        if (o.MergePending is { Count: > 0 } mergePending)
        {
            lines.Add($"  {mergePending.Count} .new merge-helper(s) staged — merge then delete (excluded from the auto-commit):");
            foreach (DotiAssetOutcome m in mergePending)
            {
                lines.Add($"    {m.Path}");
            }
        }

        if (o.Commit is { } commit)
        {
            lines.Add(commit.Status switch
            {
                DotiCommitStatus.Committed => $"  committed {commit.StagedPaths.Count} path(s): {commit.Sha?[..Math.Min(10, commit.Sha.Length)]}.",
                DotiCommitStatus.NoChange => "  no managed-asset change to commit.",
                DotiCommitStatus.NonGit => "  not a git work tree; commit skipped.",
                DotiCommitStatus.Disabled => "  --no-commit: reconciled changes left uncommitted.",
                DotiCommitStatus.Failed => $"  commit failed: {commit.Reason}",
                _ => $"  commit: {commit.Status}.",
            });
        }
    }
}
