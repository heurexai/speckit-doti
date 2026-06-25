namespace Hx.Tooling.Contracts;

/// <summary>A single Markdown task as seen by the Doti task-completion proof.</summary>
public sealed record TaskCompletionProofItem(
    string TaskId,
    string Path,
    int LineNumber,
    bool Checked,
    string CanonicalHash,
    string? StoredHash);

/// <summary>A task-completion failure bound to a specific task file location when possible.</summary>
public sealed record TaskCompletionProofDiagnostic(
    string Path,
    int LineNumber,
    string? TaskId,
    string Reason,
    string Message);

/// <summary>
/// The gate's task-completion proof. It stores every parsed task with its recomputable canonical hash,
/// plus a task-set hash, so transition/release paths can reject unchecked or tampered task ledgers instead
/// of trusting a human-edited checklist.
/// </summary>
public sealed record TaskCompletionProof(
    int SchemaVersion,
    StageOutcome Outcome,
    string? Feature,
    string? TaskFile,
    int TaskCount,
    string TaskSetHash,
    IReadOnlyList<TaskCompletionProofItem> Tasks,
    IReadOnlyList<TaskCompletionProofDiagnostic> Diagnostics);
