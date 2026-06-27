using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    /// <summary>
    /// 009 FR-006: emit the current constitution — the §2 project declarations by default, the whole file with
    /// <c>--section full</c>. This is the carrier for the codified fresh-context injection at <c>/03-doti-plan</c> and
    /// the on-demand agent tool; <c>review-context</c> composes the same <see cref="ConstitutionService"/> for arch-review
    /// (FR-007/008). Absence is surfaced as an Ok note (FR-016 surface-and-proceed), NEVER a failure. Thin: delegate to
    /// <see cref="ConstitutionService"/> and render.
    /// </summary>
    public static CliResult DotiConstitution(CliMeta meta, string repo, string section)
    {
        string root = Path.GetFullPath(repo);
        ConstitutionReadResult result = ConstitutionService.Read(root);
        if (!result.Exists)
        {
            return CliResults.Ok(meta, "doti constitution", result.AbsenceNote!, result);
        }

        bool full = string.Equals(section, "full", StringComparison.OrdinalIgnoreCase);
        string summary = full
            ? $"Constitution ({result.FullContent!.Length} chars) from {result.Path} — §1 inherited invariants + §2 project declarations."
            : result.Section2Content is null
                ? result.AbsenceNote!
                : $"Constitution §2 ({result.Section2Content.Length} chars) from {result.Path} — the project declarations to evaluate the change against.";
        return CliResults.Ok(meta, "doti constitution", summary, result);
    }
}
