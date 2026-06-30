namespace Hx.Tooling.Contracts.Setup;

/// <summary>029 FR-009: one schema/value validation error, naming the offending <see cref="Field"/>.</summary>
public sealed record SetupValidationError(string Field, string Message);

/// <summary>029 D5: the outcome of <see cref="SetupConfigSchema"/> validation — fail-closed. <see cref="Ok"/> only when
/// <see cref="Errors"/> is empty; an invalid config must never reach generation (SC-006).</summary>
public sealed record SetupValidationResult(IReadOnlyList<SetupValidationError> Errors)
{
    public bool Ok => Errors.Count == 0;

    public static readonly SetupValidationResult Valid = new([]);
}
