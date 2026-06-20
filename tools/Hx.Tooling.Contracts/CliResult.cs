using System.Text.Json.Nodes;

namespace Hx.Tooling.Contracts;

/// <summary>The command outcome — richer than a boolean (<c>partial</c> matters for batch work).</summary>
public enum CliOutcome
{
    Success,
    Partial,
    Failed,
    Blocked,
    Skipped,
    TimedOut,
    Cancelled,
}

/// <summary>A machine-readable next step an agent can take (the Direction ring).</summary>
public sealed record CliNextAction(string Label, string Why, string? Command = null);

/// <summary>An effect the command had on the world (the Effects ring): created/modified/deleted/moved.</summary>
public sealed record CliEffect(string Kind, string Target, string? Detail = null);

/// <summary>Progress for a long-running command (the Progress ring).</summary>
public sealed record CliProgress(double? Percent = null, string? Phase = null, long? EtaMs = null);

/// <summary>
/// The agent-first output envelope every command returns — the "rings": Status, Identity, Diagnostics, Direction,
/// and Result (core), plus optional Effects/Progress (extended). The kernel's renderer serializes it LF-normalized
/// for agents (JSON) and formats it for humans (TTY). <see cref="Data"/> is the domain payload, pre-serialized to a
/// <see cref="JsonNode"/> so the envelope serializes cleanly without object-polymorphism surprises.
/// </summary>
public sealed record CliResult(
    int SchemaVersion,
    string Tool,
    string Version,
    string Command,
    CliOutcome Outcome,
    bool Ok,
    int ExitCode,
    Severity? SeverityRollup,
    string Summary,
    IReadOnlyList<Diagnostic> Errors,
    IReadOnlyList<Diagnostic> Warnings,
    IReadOnlyList<Diagnostic> Info,
    IReadOnlyList<CliNextAction> NextActions,
    bool RequiresOperator,
    OperatorQuestion? Decision,
    JsonNode? Data,
    long ElapsedMs,
    string? RunId = null,
    string? Timestamp = null,
    string? Target = null,
    string? DataSchemaRef = null,
    IReadOnlyList<CliEffect>? Effects = null,
    CliProgress? Progress = null);
