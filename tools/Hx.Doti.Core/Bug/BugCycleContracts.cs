namespace Hx.Doti.Core.Bug;

/// <summary>The assessment verdict for a bug (FR-034). Only a <see cref="Confirmed"/> bug may be fixed.</summary>
public static class BugVerdict
{
    public const string Confirmed = "confirmed";
    public const string Rejected = "rejected";
    public const string NeedsInfo = "needs-info";
}

/// <summary>A bug-cycle stage outcome. <see cref="Pass"/>/<see cref="Fail"/> are honest verdicts; <see cref="Blocked"/>
/// is a fail-closed refusal (missing assessment, unbound fix, or an unsupported pass claim).</summary>
public static class BugStageOutcome
{
    public const string Pass = "pass";
    public const string Fail = "fail";
    public const string Blocked = "blocked";
}

/// <summary>
/// The read-only ASSESS artifact (<c>.doti/bugs/&lt;bugId&gt;/assessment.json</c>): the verdict/severity/remediation
/// contract a fix must be bound to. The assess stage writes ONLY this file — it never changes code.
/// </summary>
public sealed record BugAssessment(
    int SchemaVersion,
    string BugId,
    string Verdict,
    string Severity,
    string Remediation,
    string Summary);

/// <summary>
/// The FIX artifact (<c>.doti/bugs/&lt;bugId&gt;/fix.json</c>): bound to the assessment's canonical content hash so a
/// fix can never float free of the assessment that justified it (FR-034). The fix stage is the only writer.
/// </summary>
public sealed record BugFixRecord(
    int SchemaVersion,
    string BugId,
    string BoundAssessmentSha256,
    string Summary,
    IReadOnlyList<string> ChangedPaths);

/// <summary>
/// The TEST artifact (<c>.doti/bugs/&lt;bugId&gt;/test.json</c>): an honest verification bound to the fix's content
/// hash. A <c>pass</c> requires evidence — an evidence-free pass is downgraded so the stage cannot over-claim.
/// </summary>
public sealed record BugTestRecord(
    int SchemaVersion,
    string BugId,
    string BoundFixSha256,
    string Outcome,
    string Evidence);

/// <summary>
/// The result of one bug-cycle stage. On a fail-closed refusal it carries the registry diagnostic
/// <see cref="FailureCode"/> (<c>bug-assessment-missing</c> / <c>bug-fix-unbound</c>) the CLI maps to an error code.
/// </summary>
public sealed record BugStageResult(
    string Stage,
    string BugId,
    string Outcome,
    string? ArtifactPath,
    string? ArtifactSha256,
    string? BoundSha256,
    string? FailureCode,
    string? FailureMessage);
