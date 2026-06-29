using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // 022 T052 (FR-016/017/019): batch-update every Doti repo under a root, fail-soft, with per-repo before→after and
    // a summary. A non-zero exit signals any per-repo failure (Integrity), but the batch never aborts the others.
    public static CliResult DotiUpdateAll(CliMeta meta, string? root, string agentsCsv, bool force, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return Usage(meta, "doti update-all", "doti update-all requires an explicit --root <dir>.");
        }

        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti update-all", error!);
        }

        string? source = FindDotiSource(Directory.GetCurrentDirectory());
        if (source is null)
        {
            return Usage(meta, "doti update-all", "Could not locate .doti/core/skills.json above the current directory.");
        }

        DotiUpdateAllSummary summary = DotiBatchUpdater.Run(source, root, agents, meta.Version, force, dryRun);
        string text = UpdateAllSummary(summary);
        return summary.Failed > 0
            ? CliResults.Fail(meta, "doti update-all", ExitClass.Integrity,
                [Diag.Of(ErrorCodes.Integrity_DotiUpdateFailed, $"{summary.Failed} of {summary.Total} repo(s) failed to update.")],
                text, summary)
            : CliResults.Ok(meta, "doti update-all", text, summary);
    }

    private static string UpdateAllSummary(DotiUpdateAllSummary s)
    {
        string verb = s.DryRun ? "would update" : "updated";
        var lines = new List<string>
        {
            $"{s.Total} Doti repo(s) under {s.Root}: {s.Updated} {verb}, {s.AlreadyCurrent} already current, {s.Failed} failed.",
        };
        foreach (DotiUpdateOutcome r in s.Repos)
        {
            string move = r.BeforeVersion is null && r.AfterVersion is null
                ? r.Status
                : $"{r.Status} ({r.BeforeVersion ?? "—"} → {r.AfterVersion ?? "—"})";
            lines.Add($"  {move,-28} {r.RepoPath}");
        }

        return string.Join("\n", lines);
    }
}
