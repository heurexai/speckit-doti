using System.Net.Http;
using Hx.Doti.Core;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

/// <summary>
/// The single front door for generation (<c>Hx.Scaffold.Cli new</c>): generate the base solution via
/// the template, finish it (vendor Gitleaks/Sentrux + the runner/impact source, install Doti), run the
/// first smoke, and return a <see cref="ScaffoldProof"/>. Orchestration lives here (the CLI stays thin).
/// </summary>
public static class ScaffoldNewRunner
{
    public static ScaffoldProof Run(ScaffoldRequest request, string sourceRepoRoot)
    {
        // 1. Generate the base solution (subprocess dotnet new).
        TemplateInvocation invocation = TemplateGenerator.Generate(request, sourceRepoRoot);
        if (invocation.Outcome != StageOutcome.Pass)
        {
            return new ScaffoldProof(JsonContractDefaults.SchemaVersion, StageOutcome.Fail, request, invocation,
                new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Blocked, [], []));
        }

        string targetRoot = Path.GetFullPath(request.OutputPath);

        // 2. Finish: vendor the verified tools + the runner/impact source, then install Doti assets.
        ToolVendor.Vendor(sourceRepoRoot, targetRoot);
        SourceVendor.Vendor(sourceRepoRoot, targetRoot);
        DotiAgentTarget[] agents = request.Agents
            .Select(DotiAgentTarget.FromKey)
            .Where(a => a is not null)
            .Cast<DotiAgentTarget>()
            .ToArray();
        DotiInstaller.Install(sourceRepoRoot, targetRoot, agents, request.Name);

        // 2b. Best-effort, fetch-if-missing provisioning of any vendored tool binary the copy didn't carry
        // (most visibly gitversion, whose bin is gitignored). Fail-closed per tool but never throws out of `new`:
        // offline generation must still complete (the first smoke already tolerates a blocked tool step).
        ProvisionMissingToolsBestEffort(targetRoot);

        // 3. First smoke against the finished repo.
        GateProof smoke = FirstSmokeRunner.Run(targetRoot);

        StageOutcome overall =
            invocation.Outcome == StageOutcome.Pass && smoke.Outcome == StageOutcome.Pass ? StageOutcome.Pass :
            smoke.Outcome == StageOutcome.Fail ? StageOutcome.Fail :
            StageOutcome.Blocked;
        return new ScaffoldProof(JsonContractDefaults.SchemaVersion, overall, request, invocation, smoke);
    }

    /// <summary>
    /// Fetch any vendored tool binary that is missing from the generated repo (the manifests travelled with the
    /// copy; the binaries are gitignored). Fail-closed per tool (a hash mismatch never installs), but swallow all
    /// errors here — offline `new` must still complete; the gate verifies the binaries are present at run time.
    /// </summary>
    private static void ProvisionMissingToolsBestEffort(string targetRoot)
    {
        try
        {
            string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("scaffold-dotnet-tool-fetch");
            ToolFetcher.FetchAll(targetRoot, rid, url => http.GetByteArrayAsync(url).GetAwaiter().GetResult());
        }
        catch
        {
            // Provisioning is best-effort; a network/IO failure degrades to today's behavior (a blocked tool step).
        }
    }
}
