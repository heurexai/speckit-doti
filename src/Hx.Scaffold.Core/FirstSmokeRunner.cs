using Hx.Runner.Core.Hygiene;
using Hx.Runner.Core.Platform;
using Hx.Sentrux.Core;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

/// <summary>
/// Runs the first smoke against a freshly generated + finished repo and assembles a <see cref="GateProof"/>:
/// hygiene (incl. Gitleaks verify) -> build/test the generated solution -> git init -> Sentrux verify
/// -> first-smoke baseline -> Sentrux check. The gate logic runs in-process via the runner cores; only
/// the generated-solution build/test is a nested <c>dotnet</c> (hang-fixed via <see cref="ProcessRunner"/>).
/// On non-win-x64 hosts the vendored Sentrux/Gitleaks have no per-RID binary, so those steps are Blocked
/// (fail closed) and the smoke is green only on win-x64 — the documented Phase-8 constraint.
/// </summary>
public static class FirstSmokeRunner
{
    public static GateProof Run(string targetRepoRoot, Action<CliEvent>? onEvent = null)
    {
        var steps = new List<GateStep>();

        // Wraps one step: emit "running", compute it, emit its outcome, collect it. The optional callback lets a
        // human channel render live progress; it is null for agents/tests so behaviour is otherwise identical.
        GateStep Tracked(string name, Func<GateStep> run)
        {
            onEvent?.Invoke(new CliEvent("step", name, "running"));
            GateStep step = run();
            onEvent?.Invoke(new CliEvent("step", name, step.Outcome.ToString().ToLowerInvariant()));
            steps.Add(step);
            return step;
        }

        // 1. Hygiene scan (includes Gitleaks verification on the vendored binary). Runs on the working
        //    tree before git init.
        Tracked("hygiene", () =>
        {
            HygieneScanResult hygiene = HygieneScanner.Scan(
                new HygieneScanRequest(targetRepoRoot, HygieneScope.All, HygieneSource.WorkingTree));
            return Step("hygiene", hygiene.Outcome,
                $"scanned {hygiene.ScannedFileCount} files; {hygiene.Findings.Count} findings");
        });

        // 2. Restore + build + test the generated solution (the only nested dotnet — hang-fixed).
        Tracked("build-test", () => BuildAndTest(targetRepoRoot));

        // 2b. Build the vendored runner tooling. Catches a vendored-source closure gap — a copied CLI
        //     referencing a project that was not vendored (e.g. a missing Hx.Gate.Core reference). The generated
        //     repo's CLI is not running, so building it is lock-free.
        Tracked("vendored-tooling", () => BuildVendoredTooling(targetRepoRoot));

        // 3. git init + stage so Sentrux (which enumerates via `git ls-files`) sees the files.
        GitInitAndStage(targetRepoRoot);

        // 4. Sentrux verify.
        ToolVerificationResult verify = null!;
        Tracked("sentrux-verify", () =>
        {
            string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
            SentruxPolicy policy = SentruxPolicyLoader.Load(targetRepoRoot, out _);
            verify = SentruxManifestValidator.Verify(targetRepoRoot, rid, policy);
            return Step("sentrux-verify", verify.Outcome,
                verify.Verified ? "verified" : string.Join("; ", verify.Problems));
        });

        // 5. + 6. First-smoke baseline, then check (only when verify passed; else Blocked = fail closed).
        if (verify.Verified)
        {
            Tracked("sentrux-baseline", () =>
            {
                SentruxBaselineResult baseline = SentruxBaselineRunner.Save(targetRepoRoot);
                return Step("sentrux-baseline", baseline.Outcome, $"signal={baseline.QualitySignal}");
            });

            Tracked("sentrux-check", () =>
            {
                SentruxCheckResult check = SentruxChecker.Check(targetRepoRoot);
                return Step("sentrux-check", check.Outcome,
                    $"signal={check.QualitySignal}; regression={check.RegressionOutcome}");
            });
        }
        else
        {
            Tracked("sentrux-baseline", () => new GateStep("sentrux-baseline", StageOutcome.Blocked,
                [new GateEvidence("sentrux", "skipped: Sentrux not verified (per-RID binary not vendored?)")]));
        }

        // Version calculation is advisory until the `version calculate` command exists.
        Tracked("version-calculate", () => new GateStep("version-calculate", StageOutcome.Skipped,
            [new GateEvidence("version", "advisory: `version calculate` is not implemented yet")]));

        StageOutcome overall =
            steps.Any(s => s.Outcome == StageOutcome.Fail) ? StageOutcome.Fail :
            steps.Any(s => s.Outcome == StageOutcome.Blocked) ? StageOutcome.Blocked :
            StageOutcome.Pass;
        return new GateProof(JsonContractDefaults.SchemaVersion, overall, steps, []);
    }

    private static GateStep BuildAndTest(string targetRepoRoot)
    {
        (int code, string output) test = ProcessRunner.Run(
            "dotnet", "test --nologo --disable-build-servers", targetRepoRoot, ProcessRunner.NestedDotnetEnv());
        StageOutcome outcome = test.code == 0 ? StageOutcome.Pass : StageOutcome.Fail;
        string message = test.code == 0
            ? "restore + build + test passed (incl. architecture tests)"
            : "failed: " + ProcessRunner.Tail(test.output);
        return new GateStep("build-test", outcome, [new GateEvidence("dotnet-test", message)]);
    }

    private static GateStep BuildVendoredTooling(string targetRepoRoot)
    {
        (int code, string output) = ProcessRunner.Run(
            "dotnet",
            "build tools/Hx.Runner.Cli/Hx.Runner.Cli.csproj -c Release --nologo --disable-build-servers",
            targetRepoRoot,
            ProcessRunner.NestedDotnetEnv());
        StageOutcome outcome = code == 0 ? StageOutcome.Pass : StageOutcome.Fail;
        string message = code == 0
            ? "vendored runner tooling builds (closure complete)"
            : "vendored tooling build failed: " + ProcessRunner.Tail(output);
        return new GateStep("vendored-tooling", outcome, [new GateEvidence("dotnet-build", message)]);
    }

    private static void GitInitAndStage(string targetRepoRoot)
    {
        ProcessRunner.Run("git", "init", targetRepoRoot);
        ProcessRunner.Run("git", "add -A", targetRepoRoot);
    }

    private static GateStep Step(string name, StageOutcome outcome, string message) =>
        new(name, outcome, [new GateEvidence(name, message)]);
}
