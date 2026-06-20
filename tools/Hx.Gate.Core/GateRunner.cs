using Hx.Doti.Core;
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

        // Record each completed step and (optionally) stream it live — the kernel's NDJSON consumer.
        void Emit(GateStep step)
        {
            steps.Add(step);
            onStep?.Invoke(step);
        }

        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;

        // 1. Hygiene (lane-scoped: advisory skips, normal = changed, release = full).
        Emit(HygieneStep(repositoryRoot, lane));

        // 2. Gitleaks verify.
        ToolVerificationResult gitleaks = GitleaksManifestValidator.Verify(repositoryRoot, rid);
        Emit(Step("gitleaks-verify", gitleaks.Outcome,
            gitleaks.Verified ? "verified" : string.Join("; ", gitleaks.Problems)));

        // 3. Sentrux verify.
        SentruxPolicy policy = SentruxPolicyLoader.Load(repositoryRoot, out _);
        ToolVerificationResult sentruxVerify = SentruxManifestValidator.Verify(repositoryRoot, rid, policy);
        Emit(Step("sentrux-verify", sentruxVerify.Outcome,
            sentruxVerify.Verified ? "verified" : string.Join("; ", sentruxVerify.Problems)));

        // 4. Affected-test planning (selects the test set; fail-safe to full on a planner error).
        (GateStep affectedStep, AffectedPlan plan) = AffectedChangeStep(repositoryRoot);
        Emit(affectedStep);

        // 5. Restore + full build + lane-scoped unit tests (arch families excluded; they run next).
        Emit(BuildAndTestStep(repositoryRoot, lane, plan));

        // 5. Architecture test (per-family proof; Skipped when there is no arch project).
        ArchitectureTestResult arch = ArchitectureTestRunner.Run(repositoryRoot);
        Emit(Step("architecture-test", arch.Outcome,
            $"{arch.PassedCount}/{arch.TestCount} passed; {arch.Families.Count} families"));

        // 6. Skill render drift (fail closed on drift).
        DotiRenderResult drift = DotiRenderer.Render(repositoryRoot, DotiAgentTarget.All, check: true);
        Emit(Step("skill-drift", drift.Outcome,
            drift.Outcome == StageOutcome.Pass ? "no drift" : "drifted: " + string.Join(", ", drift.Drifted)));

        // 7. Sentrux check (fail closed if the baseline is missing/stale; never creates one).
        SentruxCheckResult sentruxCheck = SentruxChecker.Check(repositoryRoot);
        Emit(Step("sentrux-check", sentruxCheck.Outcome,
            $"signal={sentruxCheck.QualitySignal}; regression={sentruxCheck.RegressionOutcome}"));

        // Version calculation: required at release (Blocked if unavailable → fail closed), advisory otherwise.
        Emit(VersionStep(repositoryRoot, lane));

        // Security scan (SCA + analyzer SAST status): enforced at release, advisory elsewhere. The build
        // already fails on vulnerable packages via NuGetAudit; this adds the explicit proof + release gate.
        Emit(SecurityStep(repositoryRoot, lane));

        return new GateProof(JsonContractDefaults.SchemaVersion, Aggregate(steps), steps, []);
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
        // doti-commit); release = full scan (HygieneScope.All ignores the source). HygieneSource.Staged
        // for the changed scan — not WorkingTree, which is only partially wired in the scanner.
        HygieneScanRequest request = lane == Lane.Release
            ? new HygieneScanRequest(repositoryRoot, HygieneScope.All, HygieneSource.WorkingTree)
            : new HygieneScanRequest(repositoryRoot, HygieneScope.Changed, HygieneSource.Staged);
        HygieneScanResult result = HygieneScanner.Scan(request);
        return Step("hygiene", result.Outcome,
            $"{request.Scope}/{request.Source}: {result.ScannedFileCount} files, {result.Findings.Count} findings");
    }

    private static (GateStep Step, AffectedPlan Plan) AffectedChangeStep(string repositoryRoot)
    {
        try
        {
            string baseRef = ResolveBaseRef(repositoryRoot);
            AffectedPlan plan = new AffectedTestPlanner().Plan(repositoryRoot, baseRef, "HEAD", "Release");
            string message = plan.Outcome switch
            {
                AffectedOutcome.Affected => $"affected: {plan.SelectedTests.Count} test project(s) selected",
                AffectedOutcome.NoTestsRequired => "no test-affecting changes",
                _ => "full gate required: " + string.Join("; ", plan.Reasons.Take(3))
            };
            return (Step("affected-change", StageOutcome.Pass, message), plan);
        }
        catch (Exception ex)
        {
            // Fail-safe: a planner error must NEVER skip tests — run the full suite (never under-select).
            return (Step("affected-change", StageOutcome.Pass, "planner error; running full suite (fail-safe): " + ex.Message),
                AffectedPlanFactory.FullGate("planner error: " + ex.Message));
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

    private static GateStep BuildAndTestStep(string repositoryRoot, Lane lane, AffectedPlan plan)
    {
        IReadOnlyDictionary<string, string> env = NestedDotnetEnv();
        const string filter = "FullyQualifiedName!~Architecture.Tests";

        // Release and any escalation run the full suite (the soundness backstop); normal/advisory run the
        // affected selection; an empty plan runs none.
        bool full = lane == Lane.Release || plan.Outcome == AffectedOutcome.FullGateRequired;
        IReadOnlyList<string> targets = full
            ? AllTestProjectPaths(repositoryRoot)
            : plan.SelectedTests.Select(s => s.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (targets.Count == 0)
        {
            return new GateStep("restore-build-test", StageOutcome.Pass,
                [new GateEvidence("dotnet-test", full ? "no test projects found" : "no affected tests to run")]);
        }

        // Per-test-project `dotnet test` builds each test closure (cores + test project) and runs it. It
        // NEVER builds the runner CLIs, so the gate — itself invoked via `dotnet run --project Hx.Runner.Cli`
        // — cannot try to rebuild and self-lock its own running output (the MSB3021 trap of a solution build).
        var failures = new List<string>();
        foreach (string target in targets)
        {
            ProcessRunResult test = ProcessRunner.Run(new ToolCommand(
                "dotnet", ["test", target, "--nologo", "-c", "Release", "--filter", filter, "--disable-build-servers"],
                repositoryRoot, env));
            if (test.ExitCode != 0)
            {
                failures.Add(Path.GetFileNameWithoutExtension(target) + ": " + Tail(test.StandardError + test.StandardOutput, 200));
            }
        }

        string scope = full ? "full" : "affected";
        return failures.Count == 0
            ? new GateStep("restore-build-test", StageOutcome.Pass,
                [new GateEvidence("dotnet-test", $"restore + build + {targets.Count} {scope} test project(s) passed")])
            : new GateStep("restore-build-test", StageOutcome.Fail,
                [new GateEvidence("dotnet-test", $"{scope} tests failed: " + string.Join(" | ", failures))]);
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

    private static IReadOnlyDictionary<string, string> NestedDotnetEnv() => new Dictionary<string, string>
    {
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
        ["NUGET_PACKAGES"] = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
    };

    private static GateStep Step(string name, StageOutcome outcome, string message) =>
        new(name, outcome, [new GateEvidence(name, message)]);

    private static string Tail(string text, int max = 600) => text.Length <= max ? text : text[^max..];
}
