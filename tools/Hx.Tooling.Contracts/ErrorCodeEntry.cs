namespace Hx.Tooling.Contracts;

/// <summary>
/// One entry in the error-code registry (the single source of truth). The stable, machine filter key is the
/// structured <see cref="Code"/> = <see cref="Prefix"/> + zero-padded <see cref="Number"/> (e.g. <c>VAL0042</c>);
/// the author picks only the category + a short suffix, and the registry auto-assigns the number and composes the
/// code. <see cref="Name"/> (<c>&lt;category&gt;.&lt;suffix&gt;</c>) is a secondary human label. Once shipped the
/// code + <see cref="ExitClass"/> are frozen (the stability gate).
/// </summary>
public sealed record ErrorCodeEntry(
    string Code,
    string Category,
    string Prefix,
    int Number,
    Severity Severity,
    ExitClass ExitClass,
    string Name,
    string Message,
    string Remediation,
    bool Blocking = true);
