using System.Text.Json;
using System.Text.Json.Nodes;
using Hx.Tooling.Contracts;

namespace Hx.Cli.Kernel;

/// <summary>Factories that fill the envelope boilerplate so a command returns a <see cref="CliResult"/> in one line.</summary>
public static class CliResults
{
    private static readonly JsonSerializerOptions JsonOptions = JsonContractSerializerOptions.Create();

    private static JsonNode? ToNode(object? data) =>
        data is null ? null : JsonSerializer.SerializeToNode(data, JsonOptions);

    /// <summary>A success: <paramref name="data"/> becomes the Result ring, <see cref="ExitClass.Success"/>.</summary>
    public static CliResult Ok(
        CliMeta meta, string command, string summary, object? data = null,
        IReadOnlyList<CliEffect>? effects = null, IReadOnlyList<CliNextAction>? nextActions = null) =>
        new(JsonContractDefaults.SchemaVersion, meta.Tool, meta.Version, command, CliOutcome.Success, true,
            (int)ExitClass.Success, null, summary, [], [], [], nextActions ?? [], false, null, ToNode(data), 0,
            Effects: effects);

    /// <summary>A no-op success (nothing to do / not applicable): <see cref="CliOutcome.Skipped"/>, exit 0.</summary>
    public static CliResult Skipped(CliMeta meta, string command, string summary, object? data = null) =>
        new(JsonContractDefaults.SchemaVersion, meta.Tool, meta.Version, command, CliOutcome.Skipped, true,
            (int)ExitClass.Success, null, summary, [], [], [], [], false, null, ToNode(data), 0);

    /// <summary>
    /// A failure: the <paramref name="errors"/> block the command and the exit code is <paramref name="exitClass"/>.
    /// <paramref name="data"/> still carries the structured result (e.g. the failing proof) so an agent can see why.
    /// </summary>
    public static CliResult Fail(
        CliMeta meta, string command, ExitClass exitClass, IReadOnlyList<Diagnostic> errors, string? summary = null,
        object? data = null, IReadOnlyList<CliNextAction>? nextActions = null) =>
        new(JsonContractDefaults.SchemaVersion, meta.Tool, meta.Version, command, CliOutcome.Failed, false,
            (int)exitClass, Severity.Error, summary ?? FirstMessage(errors), errors, [], [], nextActions ?? [], false,
            null, ToNode(data), 0);

    /// <summary>
    /// Blocked pending operator action: not an error in the command, but the operator must intervene before
    /// it can proceed. <paramref name="requiresOperator"/> defaults true; <paramref name="decision"/> is set only for
    /// a genuine multiple-choice decision (a fail-closed refusal carries diagnostics + next actions, not a question).
    /// </summary>
    public static CliResult Blocked(
        CliMeta meta, string command, ExitClass exitClass, IReadOnlyList<Diagnostic> errors, string summary,
        object? data = null, IReadOnlyList<CliNextAction>? nextActions = null, bool requiresOperator = true,
        OperatorQuestion? decision = null) =>
        new(JsonContractDefaults.SchemaVersion, meta.Tool, meta.Version, command, CliOutcome.Blocked, false,
            (int)exitClass, Severity.Warning, summary, errors, [], [], nextActions ?? [], requiresOperator, decision,
            ToNode(data), 0);

    /// <summary>Blocked pending a human decision: emits <c>requiresOperator</c> + the structured <paramref name="decision"/>.</summary>
    public static CliResult NeedsOperator(CliMeta meta, string command, OperatorQuestion decision, string summary) =>
        new(JsonContractDefaults.SchemaVersion, meta.Tool, meta.Version, command, CliOutcome.Blocked, false,
            (int)ExitClass.Usage, Severity.Warning, summary, [], [], [], [], true, decision, null, 0);

    /// <summary>
    /// Map a deterministic <see cref="StageOutcome"/> onto the envelope: <c>Pass→Ok</c>, <c>Skipped→Skipped</c>,
    /// <c>Fail/Blocked→Fail</c> (the result stays in <paramref name="data"/>; the canonical code for
    /// <paramref name="failClass"/> is the blocking diagnostic). The single mapping every gate/check command shares.
    /// </summary>
    public static CliResult FromStage(
        CliMeta meta, string command, StageOutcome outcome, string summary, object? data,
        ExitClass failClass = ExitClass.Validation, IReadOnlyList<CliNextAction>? nextActions = null) =>
        outcome switch
        {
            StageOutcome.Pass => Ok(meta, command, summary, data, nextActions: nextActions),
            StageOutcome.Skipped => Skipped(meta, command, summary, data),
            _ => Fail(meta, command, failClass, [Diag.Of(DefaultCode(failClass), summary)], summary, data, nextActions),
        };

    /// <summary>The canonical registry code for an <see cref="ExitClass"/> (keeps the diagnostic code ⇄ exit code consistent).</summary>
    public static string DefaultCode(ExitClass exitClass) => exitClass switch
    {
        ExitClass.Usage => ErrorCodes.Usage_InvalidArguments,
        ExitClass.Validation => ErrorCodes.Validation_Failed,
        ExitClass.Integrity => ErrorCodes.Integrity_VerificationFailed,
        _ => ErrorCodes.Internal_Unhandled,
    };

    private static string FirstMessage(IReadOnlyList<Diagnostic> errors) =>
        errors.Count > 0 ? errors[0].Message : "Command failed.";
}
