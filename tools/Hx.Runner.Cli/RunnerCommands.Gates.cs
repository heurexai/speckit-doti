using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Gate.Core;
using Hx.Impact.Core.ChangeDetection;
using Hx.Runner.Core.ArchitectureGate;
using Hx.Security.Core;
using Hx.Tooling.Contracts;
using Hx.Version.Core;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // ---- architecture ----

    public static CliResult ArchitectureTest(CliMeta meta, string repo)
    {
        ArchitectureTestResult result = ArchitectureTestRunner.Run(repo);
        return CliResults.FromStage(meta, "architecture test", result.Outcome,
            $"{result.PassedCount}/{result.TestCount} passed; {result.Families.Count} families.", result);
    }

    // ---- gate run (Gate/Proof ring + NDJSON streaming) ----

    public static CliResult GateRun(CliMeta meta, string repo, string profile, Action<CliEvent> emit)
    {
        LaneDecision lane = LaneResolver.Resolve(profile);
        GateProof proof = lane.Outcome == StageOutcome.Fail
            ? new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Fail,
                [new GateStep("lane", StageOutcome.Fail, [new GateEvidence("lane", lane.Reason)])], [])
            : GateRunner.Run(repo, lane.Lane, onStep: step => emit(new CliEvent(
                "step", step.Name, step.Outcome.ToString().ToLowerInvariant(), step.Evidence.FirstOrDefault()?.Message, step.DurationMs)));

        // 012 (FR-009/021): build the operator-facing visibility trace and carry it on the ENVELOPE (never the hashed
        // proof — M1). Best-effort: a trace failure must never fail an otherwise-passing gate. The implement-stage
        // detail tier is resolved HERE (cycle context), so GateRunner stays stage-agnostic.
        GateTrace? trace = lane.Outcome == StageOutcome.Fail ? null : TryBuildTrace(repo, lane.Lane, proof);
        var runResult = new GateRunResult(JsonContractDefaults.SchemaVersion, lane, proof, trace);

        string note = "";
        if (proof.Outcome == StageOutcome.Pass)
        {
            // Persist a change-set-bound proof so Doti transition/release paths can verify it is fresh and passing.
            try { GateProofStore.Persist(repo, lane.Lane, proof); }
            catch (Exception ex) { note = " (warning: proof not persisted: " + ex.Message + ")"; }
        }

        string summary = $"Gate {lane.Lane} ({lane.Reason}): {proof.Outcome}.{note}";
        return proof.Outcome == StageOutcome.Pass
            ? CliResults.Ok(meta, "gate run", summary, runResult)
            : CliResults.Fail(meta, "gate run", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, summary)], summary, runResult);
    }

    // 012: assemble the GateTrace from the finished proof. The affected plan + base/head/config come from the proof's
    // AffectedTestProof; the change context is recomputed (review context, cheap); the implement-stage flag is the
    // cycle's current stage == "implement". Returns null on any failure — visibility is never load-bearing.
    private static GateTrace? TryBuildTrace(string repo, Lane lane, GateProof proof)
    {
        try
        {
            AffectedTestProof? affected = proof.AffectedTestProof;
            if (affected is null)
            {
                return null;
            }

            ChangeSetContext change = new ChangeSetContextBuilder().BuildForRepo(repo, affected.BaseRef, affected.HeadRef);
            bool implementStage = string.Equals(
                new CycleStateStore(repo).Read()?.CurrentStage, "implement", StringComparison.OrdinalIgnoreCase);
            long totalMs = proof.Steps.Sum(s => s.DurationMs ?? 0);
            return new GateTraceProjector().Project(
                repo, proof, change, affected.Plan, lane,
                affected.BaseRef, affected.HeadRef, affected.Configuration, implementStage, totalMs);
        }
        catch
        {
            return null;
        }
    }

    // ---- version ----

    public static CliResult VersionCalculate(CliMeta meta, string repo)
    {
        VersionResult result = GitVersionTool.Calculate(Path.GetFullPath(repo));
        return CliResults.Ok(meta, "version calculate", $"version={result.Version} ({result.Source}).", result);
    }

    // ---- security ----

    public static CliResult SecurityScan(CliMeta meta, string repo)
    {
        SecurityScanResult result = SecurityScanner.Scan(Path.GetFullPath(repo));
        return CliResults.FromStage(meta, "security scan", result.Outcome,
            $"{result.Vulnerabilities.Count} vulnerability finding(s); SAST {result.SastStatus}.", result);
    }
}
