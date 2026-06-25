using Hx.Cycle.Core;
using Hx.Cycle.Core.Tasks;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Tests;

public sealed partial class CycleEnforcementTests
{
    private static void WritePassingGateProofForCurrentDiff(string dir)
    {
        CycleState state = new CycleStateStore(dir).Read()
            ?? throw new InvalidOperationException("cycle state missing");
        string changeSetId = ChangeSetIdentity.Of(dir, state.BaseRef, "HEAD");
        AffectedPlan plan = new AffectedTestPlanner().Plan(dir, state.BaseRef, "HEAD", "Release");
        var affectedProof = new AffectedTestProof(
            JsonContractDefaults.SchemaVersion,
            state.BaseRef,
            "HEAD",
            "Release",
            AffectedTestProofHasher.HashPlan(plan),
            AffectedTestProofHasher.HashTestScope([]),
            AffectedTestProofHasher.HashExecutedTests([]),
            FullSuite: false,
            FullSuiteReason: null,
            plan,
            []);
        var gateProof = new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Pass, [], [], affectedProof);
        new GateProofStore(dir).Write(new PersistedGateProof(
            JsonContractDefaults.SchemaVersion,
            changeSetId,
            state.BaseRef,
            Lane.Normal,
            gateProof,
            GitHead(dir)));
    }

    private static void WriteCompletedTaskFile(string dir, string feature)
    {
        string taskDir = Path.Combine(dir, "docs", "tasks");
        Directory.CreateDirectory(taskDir);
        File.WriteAllText(Path.Combine(taskDir, feature + "-tasks.md"),
            "- [x] `T001` (FR-001, SC-001) - Complete the feature proof.\n");
        TaskHashStampResult result = DotiTaskCompletion.StampFeature(dir, feature);
        if (result.Outcome != StageOutcome.Pass)
        {
            throw new InvalidOperationException(result.Summary);
        }
    }

    private static CycleCompletionIntent PendingIntentFor(CycleState state, string dir, string messageHash = "message-hash") =>
        new(
            JsonContractDefaults.SchemaVersion,
            state.Feature,
            state.CurrentStage,
            state.BaseRef,
            GitHead(dir),
            ChangeSetIdentity.Of(dir, state.BaseRef, "HEAD"),
            ChangeSetIdentity.Of(dir, state.BaseRef, "HEAD"),
            messageHash,
            DateTimeOffset.UtcNow.ToString("O"));
}
