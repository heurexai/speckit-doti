using Hx.Tooling.Contracts;

namespace Hx.Gate.Core;

/// <summary>
/// 014 (FR-004/006/007): projects the rich structural-engine results (<see cref="ArchitectureTestResult"/>,
/// <see cref="SentruxCheckResult"/>) into the render-only <see cref="StructuralStepViolations"/> the
/// <see cref="GateTrace"/> envelope carries for the failing <c>architecture-test</c>/<c>sentrux-check</c> ladder
/// steps. Returns <c>null</c> when there is no offender detail so a passing step contributes no trace noise (no
/// fabricated offenders). Deterministically ordered. Pure projection — no IO; the offenders never enter the hashed
/// proof (M1).
/// </summary>
public static class StructuralViolationProjector
{
    /// <summary>Flatten the failing architecture test cases' violations. Null when the step has no violations.</summary>
    public static StructuralStepViolations? ForArchitecture(string stepName, ArchitectureTestResult arch)
    {
        IReadOnlyList<ArchitectureViolation> violations = arch.Tests
            .Where(test => test.Outcome == StageOutcome.Fail && test.Violations is { Count: > 0 })
            .SelectMany(test => test.Violations!)
            .OrderBy(v => v.Rule, StringComparer.Ordinal)
            .ThenBy(v => v.Description, StringComparer.Ordinal)
            .ToArray();

        return violations.Count == 0
            ? null
            : new StructuralStepViolations(stepName, violations, []);
    }

    /// <summary>Carry the structured Sentrux offenders when the rule check failed and detail is present. Null when the
    /// rules passed or no structured detail was captured.</summary>
    public static StructuralStepViolations? ForSentrux(string stepName, SentruxCheckResult sentrux)
    {
        if (sentrux.RulesOutcome != StageOutcome.Fail || sentrux.RuleViolationDetails is not { Count: > 0 } details)
        {
            return null;
        }

        IReadOnlyList<SentruxViolation> ordered = details
            .OrderBy(v => v.Rule, StringComparer.Ordinal)
            .ThenBy(v => v.File ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(v => v.Function ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(v => v.Line ?? int.MaxValue)
            .ToArray();

        return new StructuralStepViolations(stepName, [], ordered);
    }
}
