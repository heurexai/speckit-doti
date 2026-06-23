using Hx.Doti.Core;
using Hx.Scaffold.Core.Versioning;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

/// <summary>
/// The single front door for generation (<c>Hx.Scaffold.Cli new</c>): generate the base solution via
/// the template, finish it (vendor Gitleaks/Sentrux + the runner/impact source, install Doti), run the
/// first smoke, and return a <see cref="ScaffoldProof"/>. Orchestration lives here (the CLI stays thin).
/// </summary>
public static class ScaffoldNewRunner
{
    public static ScaffoldProof Run(
        ScaffoldRequest request,
        string sourceRepoRoot,
        Action<CliEvent>? onEvent = null,
        string? scaffoldVersion = null)
    {
        // The optional callback emits one step event per phase so a human channel can render live progress;
        // it is null for agents/tests, leaving behaviour identical. The finish phases (vendor/doti/store)
        // throw on failure, so a successful return is the "pass" they emit.
        void Emit(string name, string status) => onEvent?.Invoke(new CliEvent("step", name, status));

        // 1. Generate the base solution (subprocess dotnet new).
        Emit("template", "running");
        TemplateInvocation invocation = TemplateGenerator.Generate(request, sourceRepoRoot);
        Emit("template", invocation.Outcome.ToString().ToLowerInvariant());
        if (invocation.Outcome != StageOutcome.Pass)
        {
            return new ScaffoldProof(JsonContractDefaults.SchemaVersion, StageOutcome.Fail, request, invocation,
                new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Blocked, [], []));
        }

        string targetRoot = Path.GetFullPath(request.OutputPath);

        // 2. Finish: vendor the verified tools + the runner/impact source, then install Doti assets.
        Emit("vendor-tooling", "running");
        ToolVendor.Vendor(sourceRepoRoot, targetRoot);
        SourceVendor.Vendor(sourceRepoRoot, targetRoot);
        Emit("vendor-tooling", "pass");

        Emit("doti-install", "running");
        DotiAgentTarget[] agents = request.Agents
            .Select(DotiAgentTarget.FromKey)
            .Where(a => a is not null)
            .Cast<DotiAgentTarget>()
            .ToArray();
        DotiInstaller.Install(sourceRepoRoot, targetRoot, agents, request.Name);
        ScaffoldVersionReporter.WriteStamp(targetRoot,
            ScaffoldVersionReporter.IdentityFromVersion(scaffoldVersion ?? "0.0.0", "hx-scaffold new"));
        Emit("doti-install", "pass");

        // 2b. Populate the shared tool store from the vendored binaries so the generated solution resolves
        // tools from one machine-global store (no ~127MB per-solution copy). Best-effort + fail-closed:
        // a missing/absent binary is skipped (the generated repo can self-provision in-repo via `tools fetch`).
        Emit("tool-store", "running");
        StoreProvisioner.PopulateFromVendoredTools(sourceRepoRoot);
        Emit("tool-store", "pass");

        // 3. First smoke against the finished repo.
        GateProof smoke = FirstSmokeRunner.Run(targetRoot, onEvent);

        ScaffoldHookArmorer.Arm(targetRoot, Emit);

        StageOutcome overall =
            invocation.Outcome == StageOutcome.Pass && smoke.Outcome == StageOutcome.Pass ? StageOutcome.Pass :
            smoke.Outcome == StageOutcome.Fail ? StageOutcome.Fail :
            StageOutcome.Blocked;
        return new ScaffoldProof(JsonContractDefaults.SchemaVersion, overall, request, invocation, smoke);
    }
}
