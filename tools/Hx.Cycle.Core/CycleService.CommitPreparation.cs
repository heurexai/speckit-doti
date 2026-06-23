using Hx.Runner.Core.Io;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;
using System.Text.Json;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    private PendingCycleCommit PreparePendingCommit(string message, CycleState state, CommitReadiness readiness)
    {
        string preCommitHead = GitRefs.TryHeadSha(_repositoryRoot)
            ?? throw new InvalidOperationException("Could not resolve HEAD before commit.");
        string messageHash = FileHashing.Sha256OfText(message);
        IReadOnlyList<string> stageProofHashes = StageProofHashes(state);
        string gateProofDigest = DigestOf(readiness.GateProof!);
        string runnerIdentity = RunnerIdentity();
        string expectedCompletionShape = "cycle-completion/v1";
        string fullMessage = BuildCommitMessage(
            message,
            state.Feature,
            state.CurrentStage,
            readiness.Identity,
            messageHash,
            readiness.Scope.StagedTreeId,
            gateProofDigest,
            runnerIdentity);
        var intent = new CycleCompletionIntent(
            JsonContractDefaults.SchemaVersion,
            state.Feature,
            state.CurrentStage,
            state.BaseRef,
            preCommitHead,
            readiness.Identity,
            readiness.GateProof!.ChangeSetId,
            messageHash,
            DateTimeOffset.UtcNow.ToString("O"),
            readiness.Scope.StagedTreeId,
            stageProofHashes,
            gateProofDigest,
            runnerIdentity,
            expectedCompletionShape);
        CycleState stateWithIntent = state with { PendingCommit = intent, Completion = null };
        _store.Write(stateWithIntent);
        return new PendingCycleCommit(intent, stateWithIntent, fullMessage);
    }

    private ProcessRunResult RunGitCommit(string fullMessage) =>
        ProcessRunner.Run(new ToolCommand(
            "git",
            ["commit", "-m", fullMessage],
            _repositoryRoot,
            new Dictionary<string, string> { [PrecommitGuard.SentinelEnvVar] = "1" }));

    private CycleCompletionRecord CompletePendingCommit(PendingCycleCommit pending, string commitSha)
    {
        CycleCompletionRecord completion = CreateCompletion(pending.Intent, commitSha);
        _store.Write(pending.StateWithIntent with
        {
            CurrentStage = "commit",
            PendingCommit = null,
            Completion = completion,
        });
        return completion;
    }

    private static string BuildCommitMessage(
        string message,
        string feature,
        string stage,
        string changeSetId,
        string messageHash,
        string stagedTreeId,
        string gateProofDigest,
        string runnerIdentity) =>
        $"{message}\n\nDoti-Cycle: {feature}/{stage}\nDoti-ChangeSet: {changeSetId}\nDoti-Message: {messageHash}\nDoti-StagedTree: {stagedTreeId}\nDoti-GateProof: {gateProofDigest}\nDoti-Runner: {runnerIdentity}";

    private static IReadOnlyList<string> StageProofHashes(CycleState state) =>
        state.Stages
            .Select(DigestOf)
            .OrderBy(h => h, StringComparer.Ordinal)
            .ToArray();

    private static string DigestOf<T>(T value) =>
        FileHashing.Sha256OfText(JsonSerializer.Serialize(value, JsonContractSerializerOptions.Create()));

    private static string RunnerIdentity()
    {
        System.Reflection.AssemblyName name = typeof(CycleService).Assembly.GetName();
        return $"{name.Name}/{name.Version}";
    }

    private sealed record CommitReadiness(
        IReadOnlyList<string> Reasons,
        string Identity,
        PersistedGateProof? GateProof,
        CommitScope Scope);

    private sealed record PendingCycleCommit(
        CycleCompletionIntent Intent,
        CycleState StateWithIntent,
        string FullMessage);
}
