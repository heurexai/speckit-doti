using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // 022 T052 (FR-016/017/019) + 031 (FR-001/002/SC-012): batch-update every Doti repo under a root, fail-soft, with
    // per-repo before→after and a summary. The source defaults to the running tool's BUNDLED payload (fail closed if
    // unresolvable); each repo gets the same source/prune/commit behavior (--no-commit opts out). A non-zero exit
    // signals any per-repo failure (Integrity), but the batch never aborts the others.
    public static CliResult DotiUpdateAll(CliMeta meta, string? root, string agentsCsv, bool force, bool dryRun, bool noCommit)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return Usage(meta, "doti update-all", "doti update-all requires an explicit --root <dir>.");
        }

        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti update-all", error!);
        }

        if (!TryResolveDotiPayloadSource(meta, "doti update-all", out string source, out string origin, out CliResult? failure))
        {
            return failure!;
        }

        DotiUpdateAllSummary summary = DotiBatchUpdater.Run(
            source, root, agents, meta.Version, force, dryRun, commit: !noCommit, sourceOrigin: origin);
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
            string commit = r.Commit is { Status: DotiCommitStatus.Committed } c
                ? $" [committed {c.Sha?[..Math.Min(10, c.Sha.Length)]}]"
                : r.Commit is { } o ? $" [{o.Status}]" : string.Empty;
            lines.Add($"  {move,-28} {r.RepoPath}{commit}");
        }

        return string.Join("\n", lines);
    }
}
