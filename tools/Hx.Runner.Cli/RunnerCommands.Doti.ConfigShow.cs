using Hx.Cli.Kernel;
using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    /// <summary>
    /// 029 FR-004/D7: <c>hx doti config show [--json]</c> — resolve the effective setup configuration from the
    /// persisted operator intent (<c>.doti/setup.json</c>) overlaid on the documented defaults, and render either the
    /// machine JSON (<c>{value, source, default}</c> per key, grouped) or the human table (grouped by what each setting
    /// drives, default-vs-custom, with a <c>N custom · M default</c> footer). Persisted-only (C4), NON-MUTATING, and an
    /// ALL-DEFAULT view when no <c>.doti/setup.json</c> is present (D7) — never an error. Thin: store → resolver → render.
    /// </summary>
    public static CliResult DotiConfigShow(CliMeta meta, string repo)
    {
        string root = Path.GetFullPath(repo);
        SetupConfig? intent = SetupConfigStore.Read(root);
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(intent, flags: null, SetupAudience.Both);

        SetupConfigShowView view = SetupConfigTableFormatter.ToView(resolved);
        string source = intent is null ? "documented defaults (no .doti/setup.json)" : SetupConfigStore.RelativePath;
        // SC-003: the human render surfaces the grouped table + the N custom · M default footer through the summary
        // (the kernel's human writer renders Summary). The --json path serializes the structured view in Data.
        string summary = $"Effective setup config — source: {source}\n\n{SetupConfigTableFormatter.FormatHuman(resolved)}";

        return CliResults.Ok(meta, "doti config show", summary, view);
    }
}
