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
using Hx.Runner.Core.Packaging;
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
        // 012 FR-019: time every step so the human trace can show per-step duration + total elapsed. Timing is pure
        // telemetry stamped onto GateStep.DurationMs — never a proof input (M1).
        void Emit(GateStep step) => EmitStep(steps, onStep, step);
        void EmitTimed(Func<GateStep> produce) => Emit(Timed(produce));

        // The repo's declared TIER (not the --profile lane) owns which opinionated gates run + in what mode
        // (FR-029). A step the tier does not declare defaults to Enforced — today's behavior.
        GateLadderResolution resolution = GateLadderResolver.Resolve(repositoryRoot);
        GateLadder ladder = resolution.Ladder ?? new GateLadder("unresolved", new Dictionary<string, GateMode>());
        if (!resolution.Ok)
        {
            Emit(new GateStep("tier-resolution", StageOutcome.Fail,
                [new GateEvidence("tier-resolution", resolution.FailureReason ?? "could not resolve the repo's gate tier")]));
        }

        EmitTimed(() => HygieneStep(repositoryRoot, lane));
        EmitTimed(() => GitleaksVerifyStep(repositoryRoot));
        // Compute the affected plan + the change SCOPE (FR-028) before the architecture/Sentrux steps so a docs-only
        // change can scope-skip them. Release always runs everything (scope never skips at release lane).
        AffectedGatePlan affected = Timed(() => AffectedChangeStep(repositoryRoot), out long affectedMs);
        Emit(affected.Step with { DurationMs = affectedMs });
        GateScope scope = lane == Lane.Release
            ? new GateScope(JsonContractDefaults.SchemaVersion, false, "scope: release lane runs every gate", [])
            : GateScopeResolver.Resolve(repositoryRoot, affected.BaseRef, affected.HeadRef, affected.Plan);
        EmitTimed(() => ScopeOrTier(scope, ladder, repositoryRoot, "sentrux-verify", () => SentruxVerifyStep(repositoryRoot)));
        TaskCompletionProof taskCompletionProof = DotiTaskCompletion.CreateActiveFeatureProof(repositoryRoot);
        EmitTimed(() => TaskCompletionStep(taskCompletionProof));
        BuildAndTestResult buildAndTest = Timed(() => BuildAndTestStep(repositoryRoot, lane, affected.Plan), out long buildMs);
        Emit(buildAndTest.Step with { DurationMs = buildMs });
        EmitTimed(() => ScopeOrTier(scope, ladder, repositoryRoot, "architecture-test", () => ArchitectureStep(repositoryRoot)));
        EmitTimed(() => NoVelopackStep(repositoryRoot)); // FR-007/SC-005: always enforced — a hard product invariant, not tier-downgradable
        EmitTimed(() => NoSourceStep(repositoryRoot)); // FR-006/SC-004: no tool build tree in the staged release layout
        EmitTimed(() => SkillDriftStep(repositoryRoot)); // render/payload/skill-drift stay ENFORCED — never scope-skipped (SC-011)
        EmitTimed(() => DotiPayloadStep(repositoryRoot));
        EmitTimed(() => ScopeOrTier(scope, ladder, repositoryRoot, "sentrux-check", () => SentruxCheckStep(repositoryRoot)));
        EmitTimed(() => VersionStep(repositoryRoot, lane));
        EmitTimed(() => SecurityStep(repositoryRoot, lane));
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
            ladder.Coverage(),
            scope);
    }

    // FR-028 (M-1): a docs-only change SCOPE-skips architecture + Sentrux with a scope reason — distinct from a tier
    // skip and from a missing-config Fail. The scope skip is a SEPARATE dimension from ApplyTier's tier modes; a
    // non-docs-only change falls through to the normal tier handling.
    private static GateStep ScopeOrTier(GateScope scope, GateLadder ladder, string repositoryRoot, string stepName, Func<GateStep> run)
    {
        if (scope.DocsOnly && scope.ScopeSkippedSteps.Contains(stepName, StringComparer.Ordinal))
        {
            return new GateStep(stepName, StageOutcome.Skipped, [new GateEvidence($"{stepName}.scope", scope.Reason)]);
        }

        return ApplyTier(ladder, repositoryRoot, stepName, run);
    }

    // Apply the tier's mode to an opinionated gate step (FR-029): Skip → do not run; Advisory → run but never
    // fail the gate; Enforced (the default for an undeclared step) → run fail-closed as today. Bypass-safety
    // (FR-030): when the tier ENFORCES a gate but its config is missing/malformed, fail closed — no
    // delete-config bypass — instead of letting the underlying gate silently skip.
    private static GateStep ApplyTier(GateLadder ladder, string repositoryRoot, string stepName, Func<GateStep> run)
    {
        GateMode mode = ladder.ModeFor(stepName);
        if (mode == GateMode.Skip)
        {
            return new GateStep(stepName, StageOutcome.Skipped,
                [new GateEvidence(stepName, $"skipped by tier '{ladder.Tier}'")]);
        }

        if (mode == GateMode.Enforced
            && GateConfigRequirements.MissingOrMalformedConfig(repositoryRoot, stepName) is { } missing)
        {
            return new GateStep(stepName, StageOutcome.Fail,
                [new GateEvidence($"{stepName}.profile-gate-missing-config",
                    $"tier '{ladder.Tier}' enforces '{stepName}' but its config is missing or malformed: {missing} — no delete-config bypass (FR-030)")]);
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

    // 012 FR-019: run a step under a stopwatch and stamp its wall-clock duration. Pure telemetry — DurationMs never
    // enters a proof hash (M1).
    private static GateStep Timed(Func<GateStep> produce)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        GateStep step = produce();
        stopwatch.Stop();
        return step with { DurationMs = stopwatch.ElapsedMilliseconds };
    }

    // Overload for the two multi-value steps (affected plan, build+test): time the producing call and surface the
    // elapsed ms so the caller can stamp the step it extracts from the result.
    private static T Timed<T>(Func<T> produce, out long elapsedMs)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        T value = produce();
        stopwatch.Stop();
        elapsedMs = stopwatch.ElapsedMilliseconds;
        return value;
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

    // Scan the conventional local release-staging dir (artifacts/) for the tool's build tree. Absent in CI / a fresh
    // clone → Pass (vacuous); the authoritative fail-closed check is in LocalReleaseService at packaging time.
    private static GateStep NoSourceStep(string repositoryRoot)
    {
        ReleaseSourceScanResult result = ReleaseSourceInspector.Scan(Path.Combine(repositoryRoot, "artifacts"));
        return result.Outcome == StageOutcome.Pass
            ? Step("no-source", StageOutcome.Pass,
                result.ScannedEntryCount == 0
                    ? "no staged release layout to scan (artifacts/ absent)"
                    : $"{result.ScannedEntryCount} staged entr(ies) scanned; no tool build tree (FR-006)")
            : new GateStep("no-source", StageOutcome.Fail,
                result.Findings.Take(20)
                    .Select(f => new GateEvidence($"no-source.{f.Marker}", $"{f.Artifact}!{f.Entry}", f.Artifact))
                    .ToArray());
    }

    private static GateStep NoVelopackStep(string repositoryRoot)
    {
        NoVelopackScanResult result = NoVelopackScanner.Scan(repositoryRoot);
        return result.Outcome == StageOutcome.Pass
            ? Step("no-velopack", StageOutcome.Pass,
                $"{result.ScannedFileCount} product file(s) scanned; no Velopack package reference, startup hook, or vpk invocation (FR-007)")
            : new GateStep("no-velopack", StageOutcome.Fail,
                result.Findings
                    .Select(f => new GateEvidence($"no-velopack.{f.Kind}", $"{f.Path}:{f.Line} {f.Snippet}", f.Path))
                    .ToArray());
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
        var evidence = new List<GateEvidence>
        {
            new("sentrux-check", $"signal={sentruxCheck.QualitySignal}; regression={sentruxCheck.RegressionOutcome}; verdict={sentruxCheck.RegressionVerdict}"),
        };

        // FR-030/SC-014: count the two optimization attempts when a run lands in the escalation band (and clear the
        // tally on a non-band verdict). After the second band attempt, surface the structural-architecture-review
        // next action instead of another blind optimization pass. Keyed by the active feature.
        string? feature = new CycleStateStore(repositoryRoot).Read()?.Feature;
        if (!string.IsNullOrWhiteSpace(feature))
        {
            SentruxOptimizationResult optimization =
                SentruxOptimizationTracker.Record(repositoryRoot, feature, sentruxCheck.RegressionVerdict);
            if (optimization.NextAction is not null)
            {
                evidence.Add(new GateEvidence("sentrux-check.optimization", optimization.NextAction));
            }
        }

        return new GateStep("sentrux-check", sentruxCheck.Outcome, evidence);
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

    // Diff against the integration branch when it exists; otherwise HEAD. The collector also unions the working tree
    // via `git status`, so uncommitted changes are always covered. CRUCIAL: resolve to a concrete commit SHA, NOT a
    // symbolic ref. The affected-test proof PERSISTS this base, and a stored `"HEAD"` re-resolves to a DIFFERENT commit
    // the moment a later transition commit advances HEAD — so the release-train validation (which runs AFTER the
    // release transition commits) saw the symbolic base move and rejected an otherwise-valid proof ("base ref does not
    // match"). A resolved SHA is a stable snapshot that survives subsequent commits.
    private static string ResolveBaseRef(string repositoryRoot)
    {
        ProcessRunResult dev = ProcessRunner.Run(
            new ToolCommand("git", ["rev-parse", "--verify", "--quiet", "dev"], repositoryRoot));
        if (dev.ExitCode == 0 && !string.IsNullOrWhiteSpace(dev.StandardOutput))
        {
            return dev.StandardOutput.Trim();
        }

        ProcessRunResult head = ProcessRunner.Run(
            new ToolCommand("git", ["rev-parse", "--verify", "--quiet", "HEAD"], repositoryRoot));
        return head.ExitCode == 0 && !string.IsNullOrWhiteSpace(head.StandardOutput)
            ? head.StandardOutput.Trim()
            : "HEAD";
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
