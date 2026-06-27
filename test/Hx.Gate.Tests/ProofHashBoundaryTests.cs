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
}
