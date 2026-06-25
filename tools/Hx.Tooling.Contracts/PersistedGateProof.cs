namespace Hx.Tooling.Contracts;

/// <summary>
/// A <see cref="GateProof"/> persisted to disk and bound to the change set it was minted against
/// (<see cref="ChangeSetId"/>). Doti transition/release paths verify it is present, <c>Proof.Outcome ==
/// Pass</c>, and <b>fresh</b> (its <see cref="ChangeSetId"/> equals the current diff's). Non-forgeable:
/// running the gate and then changing code makes it stale, so a stale proof cannot authorize a commit.
/// Written by <c>gate run</c>; local/gitignored like <c>cycle-state.json</c>.
/// </summary>
public sealed record PersistedGateProof(
    int SchemaVersion,
    string ChangeSetId,
    string BaseRef,
    Lane Lane,
    GateProof Proof,
    string? StampedAtCommit);
