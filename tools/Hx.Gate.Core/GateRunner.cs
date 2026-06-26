using Hx.Doti.Core;
using Hx.Cycle.Core;
using Hx.Cycle.Core.Documentation;
using Hx.Cycle.Core.Tasks;
using Hx.Impact.Core;
using Hx.Impact.Core.Domain;
using Hx.Impact.Core.Graph;
using Hx.Impact.Core.Planning;
using Hx.Runner.Core.ArchitectureGate;
using Hx.Runner.Core.Gitleaks;
using Hx.Runner.Core.Hygiene;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Process;
using Hx.Security.Core;
using Hx.Sentrux.Core;
using Hx.Tooling.Contracts;
using Hx.Version.Core;

namespace Hx.Gate.Core;

/// <summary>
/// Runs the deterministic gate ladder in fixed order and aggregates
/// the results into one fail-closed <see cref="GateProof"/>. Reuses the existing gates: in-process cores
/// (hygiene, Gitleaks/Sentrux verify+check, skill-drift, architecture) and a subprocess <c>dotnet</c> for
/// restore/build/unit-tests (build-server isolation). Lanes differ by hygiene scope. Not-yet-built steps
/// (affected-change, GitVersion) appear as Skipped advisory. The gate NEVER creates a Sentrux baseline.
/// </summary>
public static class GateRunner
{
    public static GateProof Run(string repositoryRoot, Lane lane, Action<GateStep>? onStep = null)
    {
        var steps = new List<GateStep>();
        void Emit(GateStep step) => EmitStep(steps, onStep, step);

        // The repo's declared TIER (not the --profile lane) owns which opinionated gates run + in what mode
        // (FR-029). A step the tier does not declare defaults to Enforced — today's behavior.
        GateLadderResolution resolution = GateLadderResolver.Resolve(repositoryRoot);
        GateLadder ladder = resolution.Ladder ?? new GateLadder("unresolved", new Dictionary<string, GateMode>());
        if (!resolution.Ok)
        {
            Emit(new GateStep("tier-resolution", StageOutcome.Fail,
                [new GateEvidence("tier-resolution", resolution.FailureReason ?? "could not resolve the repo's gate tier")]));
        }

        Emit(HygieneStep(repositoryRoot, lane));
        Emit(GitleaksVerifyStep(repositoryRoot));
        Emit(ApplyTier(ladder, "sentrux-verify", () => SentruxVerifyStep(repositoryRoot)));
        AffectedGatePlan affected = AffectedChangeStep(repositoryRoot);
        Emit(affected.Step);
        TaskCompletionProof taskCompletionProof = DotiTaskCompletion.CreateActiveFeatureProof(repositoryRoot);
        Emit(TaskCompletionStep(taskCompletionProof));
        BuildAndTestResult buildAndTest = BuildAndTestStep(repositoryRoot, lane, affected.Plan);
        Emit(buildAndTest.Step);
        Emit(ApplyTier(ladder, "architecture-test", () => ArchitectureStep(repositoryRoot)));
        Emit(SkillDriftStep(repositoryRoot));
        Emit(DotiPayloadStep(repositoryRoot));
        Emit(ApplyTier(ladder, "sentrux-check", () => SentruxCheckStep(repositoryRoot)));
        Emit(VersionStep(repositoryRoot, lane));
        Emit(SecurityStep(repositoryRoot, lane));
        ReleaseDocumentationProof? documentationProof = null;
        if (lane == Lane.Release)
        {
            documentationProof = ReleaseDocumentationInspector.Inspect(
                repositoryRoot,
                new CycleService(repositoryRoot).GetReleaseTrain());
            Emit(ReleaseDocumentationStep(documentationProof));
        }

        AffectedTestProof affectedProof = CreateAffectedProof(affected, buildAndTest);
        return new GateProof(
            JsonContractDefaults.SchemaVersion,
            Aggregate(steps),
            steps,
            [],
            affectedProof,
            taskCompletionProof,
            documentationProof,
            ladder.Tier,
            ladder.Coverage());
    }

    // Apply the tier's mode to an opinionated gate step (FR-029): Skip → do not run; Advisory → run but never
    // fail the gate; Enforced (the default for an undeclared step) → run fail-closed as today.
    private static GateStep ApplyTier(GateLadder ladder, string stepName, Func<GateStep> run)
    {
        GateMode mode = ladder.ModeFor(stepName);
        if (mode == GateMode.Skip)
        {
            return new GateStep(stepName, StageOutcome.Skipped,
                [new GateEvidence(stepName, $"skipped by tier '{ladder.Tier}'")]);
        }

        GateStep step = run();
        return mode == GateMode.Advisory && step.Outcome is StageOutcome.Fail or StageOutcome.Blocked
            ? step with { Outcome = StageOutcome.Skipped }
            : step;
    }

    private static void EmitStep(List<GateStep> steps, Action<GateStep>? onStep, GateStep step)
    {
        steps.Add(step);
        onStep?.Invoke(step);
    }

    private static GateStep GitleaksVerifyStep(string repositoryRoot)
    {
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        ToolVerificationResult gitleaks = GitleaksManifestValidator.Verify(repositoryRoot, rid);
        return Step("gitleaks-verify", gitleaks.Outcome,
            gitleaks.Verified ? "verified" : string.Join("; ", gitleaks.Problems));
    }

    private static GateStep SentruxVerifyStep(string repositoryRoot)
    {
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        SentruxPolicy policy = SentruxPolicyLoader.Load(repositoryRoot, out _);
        ToolVerificationResult sentruxVerify = SentruxManifestValidator.Verify(repositoryRoot, rid, policy);
        return Step("sentrux-verify", sentruxVerify.Outcome,
            sentruxVerify.Verified ? "verified" : string.Join("; ", sentruxVerify.Problems));
    }

    private static GateStep ArchitectureStep(string repositoryRoot)
    {
        ArchitectureTestResult arch = ArchitectureTestRunner.Run(repositoryRoot);
        return Step("architecture-test", arch.Outcome,
            $"{arch.PassedCount}/{arch.TestCount} passed; {arch.Families.Count} families");
    }

    private static GateStep SkillDriftStep(string repositoryRoot)
    {
        DotiRenderResult drift = DotiRenderer.Render(repositoryRoot, DotiAgentTarget.All, check: true);
        return Step("skill-drift", drift.Outcome,
            drift.Outcome == StageOutcome.Pass ? "no drift" : "drifted: " + string.Join(", ", drift.Drifted));
    }

    private static GateStep DotiPayloadStep(string repositoryRoot)
    {
        DotiPayloadCheckResult result = DotiPayloadParityChecker.Check(repositoryRoot);
        return new GateStep("doti-payload", result.Outcome,
            result.Outcome == StageOutcome.Pass
                ? [new GateEvidence("doti-payload", $"{result.CheckedCount} managed payload file(s) match")]
                : result.Drifted.Select(path => new GateEvidence("doti-payload.drift", path, path)).ToArray());
    }

    private static GateStep SentruxCheckStep(string repositoryRoot)
    {
        SentruxCheckResult sentruxCheck = SentruxChecker.Check(repositoryRoot);
        return Step("sentrux-check", sentruxCheck.Outcome,
            $"signal={sentruxCheck.QualitySignal}; regression={sentruxCheck.RegressionOutcome}");
    }

    private static GateStep TaskCompletionStep(TaskCompletionProof proof)
    {
        IReadOnlyList<GateEvidence> evidence = proof.Diagnostics.Count == 0
            ? [new GateEvidence(DotiTaskCompletion.EvidenceKind, TaskCompletionSummary(proof), proof.TaskFile)]
            : proof.Diagnostics
                .Select(d => new GateEvidence($"{DotiTaskCompletion.EvidenceKind}.{d.Reason}", ToEvidenceMessage(d), d.Path))
                .ToArray();
        return new GateStep(DotiTaskCompletion.StepName, proof.Outcome, evidence);
    }

    public static GateStep ReleaseDocumentationStep(ReleaseDocumentationProof proof)
    {
        IReadOnlyList<GateEvidence> evidence = proof.Blockers.Count == 0
            ? [new GateEvidence(ReleaseDocumentationInspector.StepName, $"documentation proof passed for {proof.Documents.Count} inspected document(s)")]
            : proof.Blockers
                .Select(blocker => new GateEvidence(ReleaseDocumentationInspector.StepName, blocker))
                .ToArray();
        return new GateStep(ReleaseDocumentationInspector.StepName, proof.Outcome, evidence);
    }

    private static string TaskCompletionSummary(TaskCompletionProof proof) =>
        proof.Outcome == StageOutcome.Skipped
            ? "No active Doti cycle state found."
            : $"{proof.TaskCount} checked task(s) hash-valid for {proof.Feature}.";

    private static string ToEvidenceMessage(TaskCompletionProofDiagnostic diagnostic)
    {
        string location = diagnostic.LineNumber > 0 ? $"{diagnostic.Path}:{diagnostic.LineNumber}" : diagnostic.Path;
        string task = string.IsNullOrWhiteSpace(diagnostic.TaskId) ? "" : $" {diagnostic.TaskId}";
        return $"{location}{task}: {diagnostic.Reason} - {diagnostic.Message}";
    }

    /// <summary>The gate fails closed: any Fail or Blocked step fails the whole gate; Skipped advisory
    /// steps do not. Extracted for unit testing.</summary>
    public static StageOutcome Aggregate(IReadOnlyList<GateStep> steps) =>
        steps.Any(s => s.Outcome is StageOutcome.Fail or StageOutcome.Blocked)
            ? StageOutcome.Fail
            : StageOutcome.Pass;

    private static GateStep HygieneStep(string repositoryRoot, Lane lane)
    {
        if (lane == Lane.Advisory)
        {
            return new GateStep("hygiene", StageOutcome.Skipped,
                [new GateEvidence("hygiene", "advisory lane skips broad hygiene")]);
        }

        // normal = changed-file hygiene over the STAGED blobs (the established semantic, matching
        // Doti transition path); release = full scan (HygieneScope.All ignores the source). HygieneSource.Staged
        // for the changed scan — not WorkingTree, which is only partially wired in the scanner.
        HygieneScanRequest request = lane == Lane.Release
            ? new HygieneScanRequest(repositoryRoot, HygieneScope.All, HygieneSource.WorkingTree)
            : new HygieneScanRequest(repositoryRoot, HygieneScope.Changed, HygieneSource.Staged);
        HygieneScanResult result = HygieneScanner.Scan(request);
        return Step("hygiene", result.Outcome,
            $"{request.Scope}/{request.Source}: {result.ScannedFileCount} files, {result.Findings.Count} findings");
    }

    private static AffectedGatePlan AffectedChangeStep(string repositoryRoot)
    {
        string baseRef = ResolveBaseRef(repositoryRoot);
        try
        {
            AffectedPlan plan = new AffectedTestPlanner().Plan(repositoryRoot, baseRef, "HEAD", "Release");
            string message = plan.Outcome switch
            {
                AffectedOutcome.Affected => $"affected: {plan.SelectedTests.Count} test project(s) selected",
                AffectedOutcome.NoTestsRequired => "no test-affecting changes",
                _ => "full gate required: " + string.Join("; ", plan.Reasons.Take(3))
            };
            return new AffectedGatePlan(Step("affected-change", StageOutcome.Pass, message), plan, baseRef, "HEAD", "Release", null);
        }
        catch (Exception ex)
        {
            // Fail-safe: a planner error must NEVER skip tests — run the full suite (never under-select).
            return new AffectedGatePlan(
                Step("affected-change", StageOutcome.Pass, "planner error; running full suite (fail-safe): " + ex.Message),
                AffectedPlanFactory.FullGate("planner error: " + ex.Message),
                baseRef,
                "HEAD",
                "Release",
                "planner error: " + ex.Message);
        }
    }

    // Diff against the integration branch when it exists; otherwise HEAD. The collector also unions the
    // working tree via `git status`, so uncommitted changes are always covered.
    private static string ResolveBaseRef(string repositoryRoot)
    {
        ProcessRunResult result = ProcessRunner.Run(
            new ToolCommand("git", ["rev-parse", "--verify", "--quiet", "dev"], repositoryRoot));
        return result.ExitCode == 0 ? "dev" : "HEAD";
    }

    private static BuildAndTestResult BuildAndTestStep(string repositoryRoot, Lane lane, AffectedPlan plan)
    {
        const string filter = "FullyQualifiedName!~Architecture.Tests";

        // Release and any escalation run the full suite (the soundness backstop); normal/advisory run the
        // affected selection; an empty plan runs none.
        bool full = lane == Lane.Release || plan.Outcome == AffectedOutcome.FullGateRequired;
        IReadOnlyList<string> targets = full
            ? AllTestProjectPaths(repositoryRoot)
            : plan.SelectedTests.Select(s => s.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (targets.Count == 0)
        {
            return new BuildAndTestResult(
                new GateStep("restore-build-test", StageOutcome.Pass,
                    [new GateEvidence("dotnet-test", full ? "no test projects found" : "no affected tests to run")]),
                [],
                full,
                full ? "release lane or planner escalation" : null);
        }

        // Per-test-project `dotnet test` runs against the already-built outputs. Some scaffold test projects
        // intentionally reference Hx.Runner.Cli/Hx.Scaffold.Cli for architecture and command-surface checks;
        // rebuilding them from inside the runner process tries to overwrite loaded assemblies and self-locks.
        var failures = new List<string>();
        var executed = new List<ExecutedTestProject>();
        foreach (string target in targets)
        {
            string command = $"dotnet test {target} --nologo -c Release --no-build --no-restore --filter {filter} --disable-build-servers";
            ProcessRunResult test = ProcessRunner.Run(new ToolCommand(
                "dotnet", ["test", target, "--nologo", "-c", "Release", "--no-build", "--no-restore", "--filter", filter, "--disable-build-servers"],
                repositoryRoot), TestTimeout());
            StageOutcome outcome = test.ExitCode == 0 ? StageOutcome.Pass : StageOutcome.Fail;
            executed.Add(new ExecutedTestProject(
                Path.GetFileNameWithoutExtension(target),
                target,
                command,
                test.ExitCode,
                outcome));
            if (test.ExitCode != 0)
            {
                failures.Add(Path.GetFileNameWithoutExtension(target) + ": " + Tail(test.StandardError + test.StandardOutput, 200));
            }
        }

        string scope = full ? "full" : "affected";
        GateStep step = failures.Count == 0
            ? new GateStep("restore-build-test", StageOutcome.Pass,
                [new GateEvidence("dotnet-test", $"{targets.Count} prebuilt {scope} test project(s) passed")])
            : new GateStep("restore-build-test", StageOutcome.Fail,
                [new GateEvidence("dotnet-test", $"{scope} tests failed: " + string.Join(" | ", failures))]);
        return new BuildAndTestResult(step, executed, full, full ? "release lane or planner escalation" : null);
    }

    private static IReadOnlyList<string> AllTestProjectPaths(string repositoryRoot)
    {
        try
        {
            string[] solutions = Directory.GetFiles(repositoryRoot, "*.slnx");
            if (solutions.Length != 1)
            {
                return [];
            }

            ProjectGraph graph = new ProjectGraphBuilder().Build(repositoryRoot, Path.GetFileName(solutions[0]));
            return graph.Nodes.Values
                .Where(n => n.IsTestProject)
                .Select(n => n.Path)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static GateStep VersionStep(string repositoryRoot, Lane lane)
    {
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        ToolVerificationResult verify = GitVersionTool.Verify(repositoryRoot, rid);
        if (!verify.Verified)
        {
            // GitVersion not vendored/verified for this RID: required at release (Blocked → fail closed),
            // advisory in other lanes (Skipped). The 76 MB binary is an operational vendor step.
            StageOutcome outcome = lane == Lane.Release ? StageOutcome.Blocked : StageOutcome.Skipped;
            return new GateStep("version-calculate", outcome,
                [new GateEvidence("version", "gitversion not vendored/verified: " + string.Join("; ", verify.Problems))]);
        }

        try
        {
            VersionResult version = GitVersionTool.Calculate(repositoryRoot);
            return Step("version-calculate", StageOutcome.Pass, $"version={version.Version} ({version.Source})");
        }
        catch (Exception ex)
        {
            StageOutcome outcome = lane == Lane.Release ? StageOutcome.Fail : StageOutcome.Skipped;
            return new GateStep("version-calculate", outcome, [new GateEvidence("version", "gitversion error: " + ex.Message)]);
        }
    }

    private static GateStep SecurityStep(string repositoryRoot, Lane lane)
    {
        SecurityScanResult result = SecurityScanner.Scan(repositoryRoot);
        string headline = result.Outcome switch
        {
            StageOutcome.Pass => $"no findings >= floor ({result.Vulnerabilities.Count} reported); SAST {(result.SastStatus.StartsWith("enforced") ? "enforced" : "NOT enforced")}",
            StageOutcome.Blocked => "scan blocked: " + string.Join("; ", result.Reasons.Take(2)),
            _ => $"{result.Vulnerabilities.Count} finding(s): " + string.Join("; ", result.Reasons.Take(2))
        };

        // Enforced at release; advisory (informational) elsewhere — the build already fails on vulnerable
        // packages via NuGetAudit, so dev lanes need not re-block on the online SCA.
        if (lane == Lane.Release)
        {
            return new GateStep("security-scan", result.Outcome, [new GateEvidence("security", headline)]);
        }

        StageOutcome advisory = result.Outcome == StageOutcome.Pass ? StageOutcome.Pass : StageOutcome.Skipped;
        string note = result.Outcome == StageOutcome.Pass ? headline : "advisory (enforced at release): " + headline;
        return new GateStep("security-scan", advisory, [new GateEvidence("security", note)]);
    }

    private static TimeSpan TestTimeout()
    {
        string? configured = Environment.GetEnvironmentVariable("HX_GATE_TEST_TIMEOUT_SECONDS");
        return int.TryParse(configured, out int seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(120);
    }

    private static GateStep Step(string name, StageOutcome outcome, string message) =>
        new(name, outcome, [new GateEvidence(name, message)]);

    private static string Tail(string text, int max = 600) => text.Length <= max ? text : text[^max..];

    private static AffectedTestProof CreateAffectedProof(AffectedGatePlan affected, BuildAndTestResult buildAndTest)
    {
        string[] testScope = buildAndTest.FullSuite
            ? buildAndTest.ExecutedTests.Select(t => t.ProjectPath).ToArray()
            : affected.Plan.SelectedTests.Select(t => t.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new AffectedTestProof(
            JsonContractDefaults.SchemaVersion,
            affected.BaseRef,
            affected.HeadRef,
            affected.Configuration,
            AffectedTestProofHasher.HashPlan(affected.Plan),
            AffectedTestProofHasher.HashTestScope(testScope),
            AffectedTestProofHasher.HashExecutedTests(buildAndTest.ExecutedTests),
            buildAndTest.FullSuite,
            buildAndTest.FullSuiteReason ?? affected.FullSuiteReason,
            affected.Plan,
            buildAndTest.ExecutedTests);
    }

    private sealed record AffectedGatePlan(
        GateStep Step,
        AffectedPlan Plan,
        string BaseRef,
        string HeadRef,
        string Configuration,
        string? FullSuiteReason);

    private sealed record BuildAndTestResult(
        GateStep Step,
        IReadOnlyList<ExecutedTestProject> ExecutedTests,
        bool FullSuite,
        string? FullSuiteReason);
}
