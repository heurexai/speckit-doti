namespace Hx.Runner.Core.Gitleaks;

/// <summary>Outcome of a Gitleaks process run, derived from its exit code and whether it wrote a report.</summary>
public enum GitleaksRunStatus
{
    /// <summary>No secrets found (exit 0).</summary>
    Clean,

    /// <summary>Secrets found and reported (leak exit code + report present).</summary>
    Findings,

    /// <summary>The tool failed; the run must fail closed rather than be treated as clean.</summary>
    Error,
}

/// <summary>
/// Classifies a Gitleaks run so a tool error is never mistaken for a clean scan
/// (fail closed, not open). Gitleaks exits 0 when clean, the configured leak exit
/// code (1) when it finds and reports secrets, and a non-zero code with no report
/// when it errors (e.g. an unreadable config).
/// </summary>
public static class GitleaksExitClassifier
{
    public const int LeakExitCode = 1; // matches the runner's --exit-code 1

    public static GitleaksRunStatus Classify(int exitCode, bool reportExists)
    {
        if (exitCode == 0)
        {
            return GitleaksRunStatus.Clean;
        }

        if (exitCode == LeakExitCode && reportExists)
        {
            return GitleaksRunStatus.Findings;
        }

        // Any other exit code, or the leak exit code with no report, is a tool error.
        return GitleaksRunStatus.Error;
    }
}
