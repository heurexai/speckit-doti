namespace Hx.Tooling.Contracts;

/// <summary>
/// Diagnostic severity — the single canonical scale. The result envelope is the contract; the kernel maps this
/// to a logging <c>LogLevel</c> (logging is derived, not the backbone).
/// </summary>
public enum Severity
{
    Error,
    Warning,
    Info,
}

/// <summary>
/// The small, fixed set of stable process-exit outcome classes. The enum value <b>is</b> the process exit code.
/// Error codes map to one of these by category; codes are never allocated a unique exit code each.
/// </summary>
public enum ExitClass
{
    Success = 0,
    Usage = 2,
    Validation = 3,
    Integrity = 4,
    Internal = 70,
}

/// <summary>Where a diagnostic points: a path with optional line/column.</summary>
public sealed record CliLocation(string? Path, int? Line = null, int? Column = null);

/// <summary>
/// A pinpointed diagnostic. <see cref="Code"/> is the stable structured code (<c>&lt;CAT&gt;&lt;NNNN&gt;</c>) from
/// the error-code registry; <see cref="Target"/> is the offending input/field/file; <see cref="Hint"/> is the
/// remediation. <see cref="Blocking"/> false ⇒ advisory (does not fail the command by default).
/// </summary>
public sealed record Diagnostic(
    string Code,
    Severity Severity,
    string Message,
    string? Target = null,
    CliLocation? Location = null,
    string? Hint = null,
    bool Blocking = true);
