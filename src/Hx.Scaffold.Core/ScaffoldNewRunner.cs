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

        // 2. Finish: vendor the verified tool binaries (Gitleaks/Sentrux), then install Doti assets. 007 T021:
        // the generated repo no longer vendors the runner/impact SOURCE — its workflow runs through the installed
        // `hx` global tool (the unified surface from T015), so SourceVendor is gone.
        Emit("vendor-tooling", "running");
        ToolVendor.Vendor(sourceRepoRoot, targetRoot);
        Emit("vendor-tooling", "pass");

        Emit("doti-install", "running");
        ScaffoldDotiInstaller.Install(sourceRepoRoot, targetRoot, request, scaffoldVersion);
        Emit("doti-install", "pass");

        // 029 FR-002/FR-006/D3/D10: project the resolved operator setup config into the just-generated repo (the
        // .csproj metadata, GitVersion seed, release env-var, constitution §2) + persist the repo-portable intent.
        // Runs AFTER doti-install so the constitution + release.json exist with their template defaults. The
        // projector/writer/intent-store type fan-out is confined to SetupProjectionStep. D10 no-op fence: a null
        // Setup early-returns inside the step before any write — SC-007 byte-identical.
        Emit("setup-config", "running");
        SetupProjectionStep.Apply(request.Setup, targetRoot, request.Name);
        Emit("setup-config", "pass");

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
