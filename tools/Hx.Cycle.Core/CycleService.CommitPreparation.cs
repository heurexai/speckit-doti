using Hx.Runner.Core.Io;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;
using System.Text.Json;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    private PendingCycleCommit PreparePendingCommit(
        string message,
        CycleState state,
        CommitReadiness readiness,
        string? nextStage,
        string expectedCompletionShape)
    {
        string preCommitHead = GitRefs.TryHeadSha(_repositoryRoot)
            ?? throw new InvalidOperationException("Could not resolve HEAD before commit.");
        string messageHash = FileHashing.Sha256OfText(message);
        IReadOnlyList<string> stageProofHashes = StageProofHashes(state);
        string? gateProofDigest = readiness.GateProof is null ? null : DigestOf(readiness.GateProof);
        string runnerIdentity = RunnerIdentity();
        string fullMessage = BuildCommitMessage(
            message,
            state.Feature,
            state.CurrentStage,
            nextStage,
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
            readiness.GateProof?.ChangeSetId ?? readiness.Identity,
            messageHash,
            DateTimeOffset.UtcNow.ToString("O"),
            readiness.Scope.StagedTreeId,
            stageProofHashes,
            gateProofDigest,
            runnerIdentity,
            expectedCompletionShape,
            nextStage);
        CycleState stateWithIntent = state with { PendingCommit = intent, Completion = null };
        _store.Write(stateWithIntent);
        return new PendingCycleCommit(intent, stateWithIntent, fullMessage);
    }

    private ProcessRunResult RunGitCommit(string fullMessage, bool allowEmpty = false) =>
        ProcessRunner.Run(new ToolCommand(
            "git",
            allowEmpty ? ["commit", "--allow-empty", "-m", fullMessage] : ["commit", "-m", fullMessage],
            _repositoryRoot,
            new Dictionary<string, string> { [PrecommitGuard.SentinelEnvVar] = "1" }));

    private static string BuildCommitMessage(
        string message,
        string feature,
        string stage,
        string? nextStage,
        string changeSetId,
        string messageHash,
        string stagedTreeId,
        string? gateProofDigest,
        string runnerIdentity) =>
        $"{message}\n\nDoti-Cycle: {feature}/{stage}\n"
        + (string.IsNullOrWhiteSpace(nextStage) ? "" : $"Doti-Transition: {feature}/{stage}->{nextStage}\n")
        + $"Doti-ChangeSet: {changeSetId}\nDoti-Message: {messageHash}\nDoti-StagedTree: {stagedTreeId}\n"
        + (string.IsNullOrWhiteSpace(gateProofDigest) ? "" : $"Doti-GateProof: {gateProofDigest}\n")
        + $"Doti-Runner: {runnerIdentity}";

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
