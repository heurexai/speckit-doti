using Hx.Gate.Core;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Gate.Tests;

/// <summary>
/// 014 (T008, FR-004/006/007): the projector maps the rich architecture/sentrux results into the render-only
/// <see cref="StructuralStepViolations"/> the trace envelope carries for FAILING structural steps, deterministically
/// ordered; a passing step contributes none (no fabricated offenders); and <see cref="GateTraceProjector.Assemble"/>
/// sets <see cref="GateTrace.StructuralViolations"/> WITHOUT changing the proof/steps it carries.
/// </summary>
public sealed class StructuralViolationProjectorTests
{
    private static AffectedPlan Plan() =>
        new(JsonContractDefaults.SchemaVersion, AffectedOutcome.Affected, [], [], []);

    [Fact]
    public void Architecture_projection_flattens_failing_cases_in_deterministic_order()
    {
        var arch = new ArchitectureTestResult(
            JsonContractDefaults.SchemaVersion, StageOutcome.Fail, 3, 1, 2,
            [
                new ArchitectureTestCase("passing", StageOutcome.Pass),
                new ArchitectureTestCase("zRule", StageOutcome.Fail,
                    [new ArchitectureViolation("zRule", "z desc", ["Z1"])]),
                new ArchitectureTestCase("aRule", StageOutcome.Fail,
                    [new ArchitectureViolation("aRule", "a desc", ["A1", "A2"])]),
            ],
            ["cliSurfaceConfinement"], []);

        StructuralStepViolations? result = StructuralViolationProjector.ForArchitecture("architecture-test", arch);

        Assert.NotNull(result);
        Assert.Equal("architecture-test", result!.StepName);
        Assert.Empty(result.Sentrux);
        // Ordered by Rule (ordinal): aRule before zRule. The passing case contributes nothing.
        Assert.Equal(["aRule", "zRule"], result.Architecture.Select(v => v.Rule).ToArray());
    }

    [Fact]
    public void Architecture_projection_is_null_when_no_case_has_violations()
    {
        var arch = new ArchitectureTestResult(
            JsonContractDefaults.SchemaVersion, StageOutcome.Pass, 1, 1, 0,
            [new ArchitectureTestCase("ok", StageOutcome.Pass)], [], []);

        Assert.Null(StructuralViolationProjector.ForArchitecture("architecture-test", arch));
    }

    [Fact]
    public void Sentrux_projection_carries_structured_offenders_when_rules_failed()
    {
        SentruxCheckResult sentrux = Sentrux(StageOutcome.Fail,
            new SentruxViolation("max_cc", "Z.cs", "Zfn", 9, "30", "25", "m"),
            new SentruxViolation("max_cc", "A.cs", "Afn", 1, "28", "25", "m"));

        StructuralStepViolations? result = StructuralViolationProjector.ForSentrux("sentrux-check", sentrux);

        Assert.NotNull(result);
        Assert.Empty(result!.Architecture);
        // Ordered by Rule, then File (ordinal): A.cs before Z.cs.
        Assert.Equal(["A.cs", "Z.cs"], result.Sentrux.Select(v => v.File ?? string.Empty).ToArray());
    }

    [Fact]
    public void Sentrux_projection_is_null_when_rules_passed()
    {
        SentruxCheckResult sentrux = Sentrux(StageOutcome.Pass);
        Assert.Null(StructuralViolationProjector.ForSentrux("sentrux-check", sentrux));
    }

    [Fact]
    public void Assemble_sets_structural_violations_without_changing_the_proof_or_steps()
    {
        var scope = new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []);
        GateStep step = new("architecture-test", StageOutcome.Fail, [new GateEvidence("a", "11/12 passed")], DurationMs: 9);
        var proof = new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Fail, [step], [], Scope: scope);
        var change = new ChangeSummary(1, 0, 0, 0, 1, 0, ["src/a.cs"], [], false);
        var structural = new[]
        {
            new StructuralStepViolations("architecture-test",
                [new ArchitectureViolation("cliSurfaceConfinement", "desc", ["Hx.X.Cli.FooService"])], []),
        };

        GateTrace trace = GateTraceProjector.Assemble(proof, change, null, Plan(), Lane.Normal, 50, structural);

        // The proof's steps are the SAME reference and the offender detail rides only on the trace envelope.
        Assert.Same(proof.Steps, trace.Steps);
        Assert.NotNull(trace.StructuralViolations);
        StructuralStepViolations only = Assert.Single(trace.StructuralViolations!);
        Assert.Equal("architecture-test", only.StepName);
        // The GateStep itself is summary-only — it never gained the offender objects.
        GateEvidence evidence = Assert.Single(proof.Steps[0].Evidence);
        Assert.DoesNotContain("FooService", evidence.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_leaves_structural_violations_null_when_there_are_none()
    {
        var scope = new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []);
        var proof = new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Pass, [], [], Scope: scope);
        var change = new ChangeSummary(0, 0, 0, 0, 0, 0, [], [], false);

        GateTrace trace = GateTraceProjector.Assemble(proof, change, null, Plan(), Lane.Normal, 1, []);

        Assert.Null(trace.StructuralViolations);
    }

    private static SentruxCheckResult Sentrux(StageOutcome rulesOutcome, params SentruxViolation[] details) =>
        new(JsonContractDefaults.SchemaVersion, rulesOutcome,
            new ToolVerificationResult(JsonContractDefaults.SchemaVersion, "sentrux", true, StageOutcome.Pass, [], []),
            rulesOutcome, [], 6000, 7000, -1000, 100, rulesOutcome, [], [], "fail", details);
}
