using Hx.Cli.Kernel;
using Hx.Runner.Core.Gitleaks;
using Hx.Runner.Core.Hygiene;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // ---- hygiene ----

    public static CliResult HygieneScan(
        CliMeta meta,
        string repo,
        string scopeRaw,
        string sourceRaw,
        string? baseRef,
        string? headRef)
    {
        bool isAll = string.Equals(scopeRaw, "all", StringComparison.OrdinalIgnoreCase);
        HygieneScope scope = isAll ? HygieneScope.All : HygieneScope.Changed;
        HygieneSource source = scope == HygieneScope.All
            ? HygieneSource.WorkingTree
            : string.Equals(sourceRaw, "range", StringComparison.OrdinalIgnoreCase) ? HygieneSource.Range : HygieneSource.Staged;

        HygieneScanResult result = HygieneScanner.Scan(new HygieneScanRequest(repo, scope, source, baseRef, headRef));
        return CliResults.FromStage(meta, "hygiene scan", result.Outcome,
            $"{scope}/{source}: {result.ScannedFileCount} file(s), {result.Findings.Count} finding(s).", result);
    }

    public static CliResult GitleaksVerify(CliMeta meta, string repo) =>
        Verify(meta, "hygiene gitleaks verify", GitleaksManifestValidator.Verify(repo, Rid()));

    public static CliResult GitleaksUpdateCheck(CliMeta meta, string repo) =>
        CliResults.Ok(meta, "hygiene gitleaks update-check", "Gitleaks upstream update check.", GitleaksUpdateChecker.Check(repo));

    public static CliResult GitleaksRenderConfig(CliMeta meta, string repo)
    {
        HygienePolicy policy = HygienePolicyLoader.Load(repo, out _);
        string toml = GitleaksConfigRenderer.Render(policy);
        string outPath = RepositoryPathResolver.ResolveInside(repo, "tools/gitleaks/config/gitleaks.toml").FullPath;
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, toml);
        return CliResults.Ok(meta, "hygiene gitleaks render-config", $"Rendered gitleaks.toml ({toml.Length} chars).",
            new { path = outPath, chars = toml.Length }, effects: [new CliEffect("write", outPath, $"{toml.Length} chars")]);
    }

}
