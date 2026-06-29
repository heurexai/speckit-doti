using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // 022 T022 (FR-001/004/019): report a repo's Doti version + relation to the installed tool. Distinguishes
    // not-a-repo from version-unknown so the missing version is explained, not silently reported as `unknown`.
    public static CliResult DotiCheckVersion(CliMeta meta, string? repo)
    {
        if (string.IsNullOrWhiteSpace(repo))
        {
            return Usage(meta, "doti check-version", "doti check-version requires --repo <path>.");
        }

        DotiRepoVersion v = DotiVersionInspector.Inspect(repo, meta.Version);
        return v.Status switch
        {
            DotiVersionStatus.NotARepo => CliResults.Fail(meta, "doti check-version", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_DotiNotARepo, v.Reason!, target: v.RepoPath)],
                $"{v.RepoPath} is not a Doti-enabled repository.", v),
            DotiVersionStatus.VersionUnknown => CliResults.Fail(meta, "doti check-version", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_DotiVersionUnknown, v.Reason!, target: v.RepoPath)],
                $"{v.RepoPath}: Doti version unknown (no .doti/payload.json).", v),
            _ => CliResults.Ok(meta, "doti check-version", CheckVersionSummary(v), v),
        };
    }

    private static string CheckVersionSummary(DotiRepoVersion v)
    {
        string relation = v.Relation switch
        {
            DotiVersionRelation.Current => $"current — up to date with the installed tool {v.InstalledToolVersion}",
            DotiVersionRelation.Outdated =>
                $"outdated — installed tool is {v.InstalledToolVersion}; run `hx doti update --repo {v.RepoPath}`",
            DotiVersionRelation.Ahead => $"ahead of the installed tool {v.InstalledToolVersion}",
            _ => "unknown",
        };
        return $"{v.RepoPath}: Doti {v.PayloadVersion} — {relation}.";
    }
}
