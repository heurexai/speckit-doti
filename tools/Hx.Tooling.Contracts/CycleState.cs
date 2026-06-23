namespace Hx.Tooling.Contracts;

public static class CycleStageOutcome
{
    /// <summary>The stage was completed and its evidence recorded (the default stamp outcome).</summary>
    public const string Stamped = "stamped";
}

/// <summary>
/// A durable intent written immediately before the sanctioned <c>git commit</c> subprocess is invoked.
/// If the process exits after Git creates the commit but before completion state is written, the cycle
/// recovery evaluator can prove whether <c>HEAD</c> is that sanctioned commit.
/// </summary>
public sealed record CycleCompletionIntent(
    int SchemaVersion,
    string Feature,
    string Stage,
    string BaseRef,
    string PreCommitHead,
    string ChangeSetId,
    string GateChangeSetId,
    string MessageHash,
    string CreatedAtUtc,
    string? StagedTreeId = null,
    IReadOnlyList<string>? StageProofHashes = null,
    string? GateProofDigest = null,
    string? RunnerIdentity = null,
    string? ExpectedCompletionShape = null);

/// <summary>
/// A completed sanctioned cycle. The stage proofs that authorized the commit are preserved for audit, but
/// the completion record is the terminal state: those proofs must not authorize another commit.
/// </summary>
public sealed record CycleCompletionRecord(
    int SchemaVersion,
    string Feature,
    string Stage,
    string BaseRef,
    string PreCommitHead,
    string CommitSha,
    string ChangeSetId,
    string GateChangeSetId,
    string MessageHash,
    string CompletedAtUtc,
    string? StagedTreeId = null,
    IReadOnlyList<string>? StageProofHashes = null,
    string? GateProofDigest = null,
    string? RunnerIdentity = null,
    string? ExpectedCompletionShape = null);

/// <summary>
/// A non-forgeable record that a doti cycle stage was completed, bound to the diff at stamp time.
/// <see cref="ChangeSetId"/> is the code-state identity (the sorted changed-path set + each
/// path's content hash; named <c>...Id</c> rather than <c>...Identity</c> so the contracts layer
/// shares no identifier with the <c>ChangeSetIdentity</c> engine type in Hx.Cycle.Core — the
/// structural-architecture analyzer resolves a matching name to that type and would flag a false
/// contracts→core edge); <see cref="ArtifactHashes"/> are the content hashes of the stage's own
/// artifact(s) (e.g. the spec file for the specify stage; empty for stages that produce no file yet).
/// Freshness is re-derived at read time (re-hash now; any mismatch ⇒ stale) — it is deliberately not
/// stored. The stamp step records these; the cycle chokepoints gate on them.
/// </summary>
public sealed record CycleStageProof(
    string Stage,
    string Outcome,
    string ChangeSetId,
    IReadOnlyList<string> ArtifactHashes,
    string? StampedAtCommit,
    string? PrerequisiteProofHash = null);

/// <summary>
/// The persistent doti cycle state (<c>.doti/cycle-state.json</c>, gitignored). Tracks the active
/// feature/phase, the base ref the change-set identity is computed against, the current stage, and the
/// per-stage <see cref="CycleStageProof"/>s recorded so far. Single active cycle per repo (V1).
/// </summary>
public sealed record CycleState(
    int SchemaVersion,
    string Feature,
    string BaseRef,
    string CurrentStage,
    IReadOnlyList<CycleStageProof> Stages,
    CycleCompletionIntent? PendingCommit = null,
    CycleCompletionRecord? Completion = null);
