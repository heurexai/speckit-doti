using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // 022 T045 (FR-008/014/015/019): update one repo's managed Doti assets to the installed payload, reporting the
    // before→after version. Reconcile runs in a git worktree (--dry-run previews); git is required (fail closed).
    public static CliResult DotiUpdate(CliMeta meta, string? repo, string agentsCsv, bool force, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(repo))
        {
            return Usage(meta, "doti update", "doti update requires an explicit --repo <path>.");
        }

        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti update", error!);
        }

        string? source = FindDotiSource(Directory.GetCurrentDirectory());
        if (source is null)
        {
            return Usage(meta, "doti update", "Could not locate .doti/core/skills.json above the current directory.");
        }

        DotiUpdateOutcome outcome = DotiWorktreeUpdate.Run(source, repo, agents, meta.Version, force, dryRun);
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
        if (o.Customizations.Count > 0)
        {
            lines.Add($"  preserved {o.Customizations.Count} customization(s) (rerun with --force to overwrite):");
            foreach (DotiAssetOutcome c in o.Customizations)
            {
                lines.Add($"    {c.Effect}: {c.Path}");
            }
        }

        return string.Join("\n", lines);
    }
}
