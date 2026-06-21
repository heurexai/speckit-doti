using Hx.Doti.Core;
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

        // 2b. Populate the shared tool store from the vendored binaries so the generated solution resolves
        // tools from one machine-global store (no ~127MB per-solution copy). Best-effort + fail-closed:
        // a missing/absent binary is skipped (the generated repo can self-provision in-repo via `tools fetch`).
        StoreProvisioner.PopulateFromVendoredTools(sourceRepoRoot);

        // 3. First smoke against the finished repo.
        GateProof smoke = FirstSmokeRunner.Run(targetRoot);

        StageOutcome overall =
            invocation.Outcome == StageOutcome.Pass && smoke.Outcome == StageOutcome.Pass ? StageOutcome.Pass :
            smoke.Outcome == StageOutcome.Fail ? StageOutcome.Fail :
            StageOutcome.Blocked;
        return new ScaffoldProof(JsonContractDefaults.SchemaVersion, overall, request, invocation, smoke);
    }
}
