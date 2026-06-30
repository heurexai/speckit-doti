namespace Hx.Tooling.Contracts;

public static class CycleStageOutcome
{
    /// <summary>The stage was completed and its evidence recorded (the default stamp outcome).</summary>
    public const string Stamped = "stamped";

    /// <summary>028 FR-005: the stage's prerequisite binding was rebound to the current upstream content by an
    /// agent-recorded reviewed-no-impact verdict (not a re-author). The proof carries the new prerequisite content
    /// hashes; freshness decays normally on a later upstream edit (the rebind is an ordinary content-bound proof, not
    /// a snapshot). The verdict + before→after hashes are recorded in <see cref="CycleState.ReviewedRebinds"/>.</summary>
    public const string ReviewedNoImpactRebound = "reviewed-no-impact-rebound";
}

/// <summary>
/// 028 FR-005: the immutable audit record of an agent-gated reviewed-no-impact rebind. When an upstream artifact's
/// content changed but the agent attested (after reading the surfaced diff) that the change does not affect a
/// dependent stage, the engine rebinds ONLY that dependent's <see cref="CycleStageProof.PrerequisiteArtifactHashes"/>
/// to the current upstream content and records this verdict. Stored in a dedicated additive
/// <see cref="CycleState.ReviewedRebinds"/> field (NOT <see cref="CycleState.Transitions"/>, which is typed for the
/// release-train scan). <see cref="BeforeHashes"/>/<see cref="AfterHashes"/> are the dependent's bound vs the
/// freshly-computed prerequisite content hashes; <see cref="ChangedUpstreams"/> are the prerequisite stage ids whose
/// produced artifact content diverged.
/// </summary>
public sealed record CycleReviewedRebindRecord(
    int SchemaVersion,
    string DependentStage,
    IReadOnlyList<string> ChangedUpstreams,
    IReadOnlyList<string> BeforeHashes,
    IReadOnlyList<string> AfterHashes,
    string Verdict,
    string? Reason,
    string AtUtc);

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
    string? ExpectedCompletionShape = null,
    string? NextStage = null);

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
    string? ExpectedCompletionShape = null,
    string? NextStage = null);

/// <summary>
/// A sanctioned automatic commit created when the next Doti stage starts. These commits replace the
/// previously agent-visible commit command and are preserved for release-train audit.
/// </summary>
public sealed record CycleTransitionRecord(
    int SchemaVersion,
    string Feature,
    string Stage,
    string NextStage,
    string PreCommitHead,
    string CommitSha,
    string ChangeSetId,
    string MessageHash,
    string CompletedAtUtc,
    string? StagedTreeId = null,
    IReadOnlyList<string>? StageProofHashes = null,
    string? GateProofDigest = null,
    string? RunnerIdentity = null);

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
    string? PrerequisiteProofHash = null,
    // Living-Spec (FR-027): canonical content hashes of this stage's transitive prerequisite artifacts,
    // captured at stamp. Binds a dependent to upstream CONTENT (not the upstream proof object), so a real
    // upstream edit stales it while a no-content re-stamp does not. Null on proofs from a pre-FR-027 runner.
    IReadOnlyList<string>? PrerequisiteArtifactHashes = null,
    // 027 FR-010: the ordered transitive prerequisite STAGE-ID set this proof was stamped against (the stage
    // GRAPH, distinct from the prerequisite CONTENT bound above). Makes the edge-only-vs-content distinction
    // first-class and a stage-model reorder migration-detectable. Additive/nullable trailing: null on proofs
    // from a pre-027 runner (the runner never reads it for enforcement — freshness is still re-derived from the
    // content bindings), so existing proofs never wedge and no schema-version bump is forced.
    IReadOnlyList<string>? StageGraphFingerprint = null);

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
    CycleCompletionRecord? Completion = null,
    IReadOnlyList<CycleTransitionRecord>? Transitions = null,
    IReadOnlyList<CycleCompletionRecord>? CompletedUnreleasedCycles = null,
    IReadOnlyList<CycleCompletionRecord>? ReleasedCycles = null,
    // 028 FR-005/B3: the agent-gated reviewed-no-impact rebind audit log. Additive nullable trailing (mirrors
    // CompletedUnreleasedCycles/ReleasedCycles), so old readers tolerate it and no schemaVersion bump is forced.
    // Kept distinct from Transitions (typed CycleTransitionRecord, scanned by the release-train).
    IReadOnlyList<CycleReviewedRebindRecord>? ReviewedRebinds = null);

public sealed record CycleReleaseTrainFeature(
    string Feature,
    string CompletedStage,
    string CommitSha,
    string? StageCommitRange,
    string TaskCompletionStatus,
    string GateProofStatus,
    string InclusionStatus,
    IReadOnlyList<string> Blockers);

public sealed record CycleReleaseTrain(
    int SchemaVersion,
    bool Valid,
    IReadOnlyList<CycleReleaseTrainFeature> Features,
    IReadOnlyList<string> Blockers,
    // 008 FR-037: cross-feature release-train drift — a later feature changed a path an earlier completed-unreleased
    // feature owns/documents. Additive nullable trailing (null on a pre-FR-037 train; M-10).
    IReadOnlyList<ReleaseTrainDriftFinding>? DriftFindings = null);

/// <summary>FR-037/SC-019: a later release-train feature changed paths an earlier completed-unreleased feature owns
/// or documents — a cross-feature drift that must be reconciled before the train is released.</summary>
public sealed record ReleaseTrainDriftFinding(
    string EarlierFeature,
    string LaterFeature,
    IReadOnlyList<string> ConflictingPaths,
    string Reason);
