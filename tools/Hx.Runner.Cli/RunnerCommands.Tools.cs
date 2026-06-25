using Hx.Cli.Kernel;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // ---- tools fetch (deterministic, hash-verified vendored-tool provisioning) ----

    public static CliResult ToolsFetch(CliMeta meta, string repo, string? rid, string toolFilter)
    {
        string hostRid = string.IsNullOrWhiteSpace(rid) ? Rid() : rid;
        string root = Path.GetFullPath(repo);

        IReadOnlyList<string> manifests = SelectToolManifests(toolFilter, out string? selectError);
        if (selectError is not null)
        {
            return Usage(meta, "tools fetch", selectError);
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("scaffold-dotnet-tool-fetch");
        byte[] FetchBytes(Uri url) => http.GetByteArrayAsync(url).GetAwaiter().GetResult();

        List<ToolFetchOutcome> outcomes = manifests
            .Select(relative => ToolFetcher.Fetch(
                RepositoryPathResolver.ResolveInside(root, relative).FullPath, hostRid, FetchBytes, root))
            .ToList();

        StageOutcome outcome =
            outcomes.Any(o => o.Status == ToolFetchStatus.Failed) ? StageOutcome.Fail :
            outcomes.All(o => o.Status == ToolFetchStatus.Fetched) ? StageOutcome.Pass :
            StageOutcome.Blocked;
        var result = new ToolFetchResult(JsonContractDefaults.SchemaVersion, outcome, hostRid, outcomes);

        int fetched = outcomes.Count(o => o.Status == ToolFetchStatus.Fetched);
        int skipped = outcomes.Count(o => o.Status == ToolFetchStatus.Skipped);
        int failed = outcomes.Count(o => o.Status == ToolFetchStatus.Failed);
        string summary = $"{fetched} fetched, {skipped} skipped (no asset for {hostRid}), {failed} failed.";

        if (outcome == StageOutcome.Fail)
        {
            List<Diagnostic> errors = outcomes
                .Where(o => o.Status == ToolFetchStatus.Failed)
                .Select(o => Diag.Of(FailureCode(o.FailureKind), o.Reason, target: o.Tool))
                .ToList();
            ExitClass exitClass = errors.Any(e => Diag.ExitClassOf(e.Code) == ExitClass.Integrity)
                ? ExitClass.Integrity
                : ExitClass.Validation;
            return CliResults.Fail(meta, "tools fetch", exitClass, errors, summary, result);
        }

        var effects = outcomes
            .Where(o => o.Status == ToolFetchStatus.Fetched && o.ExecutablePath is not null)
            .Select(o => new CliEffect("write", o.ExecutablePath!, $"{o.Tool} verified"))
            .ToList();
        return CliResults.Ok(meta, "tools fetch", summary, result, effects: effects);
    }

    private static IReadOnlyList<string> SelectToolManifests(string toolFilter, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(toolFilter) || string.Equals(toolFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            return ToolFetcher.ManifestRelativePaths;
        }

        string match = $"tools/{toolFilter.ToLowerInvariant()}/";
        List<string> selected = ToolFetcher.ManifestRelativePaths
            .Where(p => p.StartsWith(match, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (selected.Count == 0)
        {
            error = $"Unknown --tool '{toolFilter}'. Known: all, gitleaks, sentrux, gitversion, velopack.";
        }

        return selected;
    }

    private static string FailureCode(ToolFetchFailureKind kind) => kind switch
    {
        ToolFetchFailureKind.AssetUnavailable => ErrorCodes.Validation_ToolAssetUnavailable,
        ToolFetchFailureKind.ArchiveHashMismatch => ErrorCodes.Integrity_ToolArchiveHashMismatch,
        ToolFetchFailureKind.ExecutableHashMismatch => ErrorCodes.Integrity_ToolExecutableHashMismatch,
        _ => ErrorCodes.Internal_ToolDownloadFailed,
    };
}
