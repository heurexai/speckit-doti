using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Gate.Tests;

/// <summary>
/// 012 (T013, M1 — the load-bearing boundary): the visibility trace is REVIEW context and must never influence a
/// deterministic proof. The <see cref="GateTrace"/>/<see cref="ChangeSummary"/>/<see cref="AffectedTestInventory"/>
/// live on the <see cref="GateRunResult"/> ENVELOPE, never on the hashed <c>AffectedTestProof.Plan</c>, so the
/// persisted gate-proof hashes are byte-identical whether or not the trace is populated (008 FR-020/SC-009).
/// </summary>
public sealed class ProofHashBoundaryTests
{
    private static AffectedPlan SamplePlan() =>
        new(JsonContractDefaults.SchemaVersion, AffectedOutcome.Affected,
            ["Hx.Impact.Core"],
            [new SelectedTest("Hx.Impact.Tests", "test/Hx.Impact.Tests/Hx.Impact.Tests.csproj", "dotnet test ...")],
            ["leaf change"]);

    private static AffectedTestProof SampleAffectedProof()
    {
        AffectedPlan plan = SamplePlan();
        var executed = new[]
        {
            new ExecutedTestProject("Hx.Impact.Tests", "test/Hx.Impact.Tests/Hx.Impact.Tests.csproj", "dotnet test ...", 0, StageOutcome.Pass),
        };
        return new AffectedTestProof(
            JsonContractDefaults.SchemaVersion, "base", "HEAD", "Release",
            AffectedTestProofHasher.HashPlan(plan),
            AffectedTestProofHasher.HashTestScope(plan.SelectedTests.Select(t => t.ProjectPath)),
            AffectedTestProofHasher.HashExecutedTests(executed),
            false, null, plan, executed);
    }

    private static GateProof SampleProof() =>
        new(JsonContractDefaults.SchemaVersion, StageOutcome.Pass,
            [new GateStep("hygiene", StageOutcome.Pass, [new GateEvidence("h", "ok")], DurationMs: 7)],
            [], SampleAffectedProof(), Scope: new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []));

    [Fact]
    public void Persisted_proof_hashes_are_byte_identical_with_and_without_the_trace()
    {
        GateProof proof = SampleProof();

        // The same proof, once on a bare envelope and once on an envelope carrying a fully-populated trace.
        var withoutTrace = new GateRunResult(JsonContractDefaults.SchemaVersion,
            new LaneDecision(Lane.Normal, StageOutcome.Pass, "normal"), proof, Trace: null);
        var richTrace = new GateTrace(
            proof.Scope!,
            new ChangeSummary(3, 1, 2, 0, 40, 7, ["src/a.cs", "src/b.cs"], ["Alpha", "Beta"], true),
            new AffectedTestInventory(1, 5, 12, null, 4, null, "repo total not enumerated"),
            proof.Steps, 1234, GateEffectiveMode.Partial);
        var withTrace = new GateRunResult(JsonContractDefaults.SchemaVersion,
            new LaneDecision(Lane.Normal, StageOutcome.Pass, "normal"), proof, Trace: richTrace);

        AffectedTestProof bare = withoutTrace.Proof.AffectedTestProof!;
        AffectedTestProof rich = withTrace.Proof.AffectedTestProof!;

        // The proof object is the SAME reference and the three proof hashes are unchanged by the trace.
        Assert.Same(withoutTrace.Proof, withTrace.Proof);
        Assert.Equal(bare.PlanHash, rich.PlanHash);
        Assert.Equal(bare.TestScopeHash, rich.TestScopeHash);
        Assert.Equal(bare.ExecutedTestsHash, rich.ExecutedTestsHash);

        // Re-hashing the plan independently yields the same value — the trace is provably not a hash input.
        Assert.Equal(AffectedTestProofHasher.HashPlan(rich.Plan), bare.PlanHash);
    }

    [Fact]
    public void HashPlan_ignores_a_trace_that_is_not_part_of_the_plan()
    {
        // The plan carries ChangedFiles (review context) but no trace; populating an envelope trace cannot reach it.
        AffectedPlan plan = SamplePlan() with { ChangedFiles = ["src/a.cs"] };
        string before = AffectedTestProofHasher.HashPlan(plan);

        _ = new GateTrace(
            new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []),
            new ChangeSummary(9, 9, 9, 9, 9, 9, ["x"], ["Y"], true),
            new AffectedTestInventory(9, 9, 9, 9, 9, 9, null),
            [], 9, GateEffectiveMode.Full);

        Assert.Equal(before, AffectedTestProofHasher.HashPlan(plan));
    }

    // 014 (T001, FR-007/SC-006 — the load-bearing boundary): the persisted gate proof must be BYTE-IDENTICAL whether
    // or not the trace carries structural-violation detail. gateProofDigest = SHA-256(JsonSerialize(PersistedGateProof))
    // covers the WHOLE persisted proof; the StructuralViolations live on the GateRunResult.Trace, which is NOT part of
    // PersistedGateProof. This replicates CycleService.CommitPreparation.DigestOf to prove it.
    private static readonly JsonSerializerOptions Options = JsonContractSerializerOptions.Create();

    private static string DigestOf<T>(T value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Options)));
        return Convert.ToHexStringLower(hash);
    }

    private static PersistedGateProof Persisted(GateProof proof) =>
        new(JsonContractDefaults.SchemaVersion, proof.AffectedTestProof!.BaseRef, "base", Lane.Normal, proof, null);

    [Fact]
    public void Persisted_proof_digest_is_byte_identical_with_null_vs_populated_structural_violations()
    {
        GateProof proof = SampleProof();
        PersistedGateProof persisted = Persisted(proof);

        // The SAME proof, once on a bare envelope (no structural detail) and once on a fully-populated trace.
        var withoutDetail = new GateRunResult(JsonContractDefaults.SchemaVersion,
            new LaneDecision(Lane.Normal, StageOutcome.Pass, "normal"), proof, Trace: null);
        var structural = new[]
        {
            new StructuralStepViolations("architecture-test",
                [new ArchitectureViolation("cliSurfaceConfinement", "FooService should reside in core", ["Hx.X.Cli.FooService"])],
                []),
            new StructuralStepViolations("sentrux-check", [],
                [new SentruxViolation("max_cc", "Bar.cs", "ProcessFoo", 42, "28", "25", "cc exceeded")]),
        };
        var richTrace = new GateTrace(
            proof.Scope!,
            new ChangeSummary(3, 1, 2, 0, 40, 7, ["src/a.cs"], ["Alpha"], true),
            null, proof.Steps, 1234, GateEffectiveMode.Partial, structural);
        var withDetail = new GateRunResult(JsonContractDefaults.SchemaVersion,
            new LaneDecision(Lane.Normal, StageOutcome.Pass, "normal"), proof, Trace: richTrace);

        // The persisted proof is the SAME for both — the digest cannot move with the offender detail.
        string bare = DigestOf(Persisted(withoutDetail.Proof));
        string rich = DigestOf(Persisted(withDetail.Proof));

        Assert.Equal(bare, rich);
        Assert.Equal(DigestOf(persisted), rich);
        Assert.Same(withoutDetail.Proof, withDetail.Proof);
    }

    [Fact]
    public void A_pre_014_serialized_proof_and_results_still_deserialize()
    {
        // A pre-014 GateProof/SentruxCheckResult/ArchitectureTestCase JSON (no Violations / RuleViolationDetails /
        // StructuralViolations) must still deserialize — every 014 addition is nullable/defaulted (M2).
        const string preFeatureGateRun = """
        {
          "schemaVersion": 1,
          "lane": { "lane": "Normal", "outcome": "Pass", "reason": "normal" },
          "proof": { "schemaVersion": 1, "outcome": "Pass",
            "steps": [ { "name": "architecture-test", "outcome": "Pass", "evidence": [] } ], "evidence": [] },
          "trace": { "scope": { "schemaVersion": 1, "docsOnly": false, "reason": "code", "scopeSkippedSteps": [] },
            "change": { "source": 1, "test": 0, "docs": 0, "other": 0, "linesAdded": 1, "linesRemoved": 0,
              "files": [], "classesTouched": [], "classesIncluded": false },
            "tests": null, "steps": [], "totalMs": 1, "effectiveMode": "partial" }
        }
        """;
        GateRunResult? run = JsonSerializer.Deserialize<GateRunResult>(preFeatureGateRun, Options);
        Assert.NotNull(run);
        Assert.Null(run!.Trace!.StructuralViolations); // additive nullable — absent in the pre-014 trace

        const string preSentrux = """
        { "schemaVersion": 1, "outcome": "Fail",
          "verification": { "schemaVersion": 1, "tool": "sentrux", "verified": true, "outcome": "Pass",
            "checks": [], "problems": [], "message": null },
          "rulesOutcome": "Fail", "ruleViolations": ["max_cc: 2 functions exceed"], "qualitySignal": 6100,
          "baselineSignal": 7000, "signalDelta": -900, "signalToleranceBand": 100, "regressionOutcome": "Fail",
          "notes": [], "advisoryGaps": [], "regressionVerdict": "fail" }
        """;
        SentruxCheckResult? sentrux = JsonSerializer.Deserialize<SentruxCheckResult>(preSentrux, Options);
        Assert.NotNull(sentrux);
        Assert.Null(sentrux!.RuleViolationDetails); // additive nullable
        Assert.Single(sentrux.RuleViolations);      // the legacy string list is unchanged

        ArchitectureTestCase? testCase =
            JsonSerializer.Deserialize<ArchitectureTestCase>("""{ "name": "X", "outcome": "Fail" }""", Options);
        Assert.NotNull(testCase);
        Assert.Null(testCase!.Violations); // additive nullable
    }
}
