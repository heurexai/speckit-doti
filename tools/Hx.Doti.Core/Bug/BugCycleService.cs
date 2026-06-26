using System.Text.Json;
using Hx.Doti.Core.ManagedAssets;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core.Bug;

/// <summary>
/// 007 T033 (FR-034): the bug workflow as an ENFORCED doti mini-cycle (assess -> fix -> test), recorded under a
/// per-bug dir <c>.doti/bugs/&lt;bugId&gt;/</c>. The three entry points are the boundary:
/// <list type="bullet">
///   <item><see cref="Assess"/> is READ-ONLY — it writes only <c>assessment.json</c> (the verdict/severity/
///   remediation contract) and never touches code.</item>
///   <item><see cref="Fix"/> is the only writer of a fix record and FAILS CLOSED unless it is bound to a confirmed
///   assessment: a missing assessment yields <c>bug-assessment-missing</c>; an unbound (or non-confirmed) fix yields
///   <c>bug-fix-unbound</c>.</item>
///   <item><see cref="Test"/> records an HONEST verification bound to the fix — a <c>pass</c> with no evidence is
///   downgraded so the stage cannot over-claim.</item>
/// </list>
/// Each stage is proof-bound: the fix binds to the assessment's canonical content hash, the test to the fix's.
/// </summary>
public static class BugCycleService
{
    public const int SchemaVersion = 1;
    public const string CodeAssessmentMissing = "bug-assessment-missing";
    public const string CodeFixUnbound = "bug-fix-unbound";

    private const string AssessmentFile = "assessment.json";
    private const string FixFile = "fix.json";
    private const string TestFile = "test.json";

    /// <summary>Read-only ASSESS: record the verdict/severity/remediation contract and nothing else.</summary>
    public static BugStageResult Assess(string repoRoot, BugAssessment assessment)
    {
        string dir = EnsureBugDir(repoRoot, assessment.BugId);
        string path = Path.Combine(dir, AssessmentFile);
        WriteJson(path, assessment with { SchemaVersion = SchemaVersion });
        return Recorded("assess", assessment.BugId, repoRoot, path, BugStageOutcome.Pass, boundSha: null);
    }

    /// <summary>FIX (only writer): bind a fix to the confirmed assessment, or fail closed.</summary>
    public static BugStageResult Fix(
        string repoRoot, string bugId, string boundAssessmentSha256, string summary, IReadOnlyList<string>? changedPaths)
    {
        string dir = ResolveBugDir(repoRoot, bugId);
        string assessmentPath = Path.Combine(dir, AssessmentFile);
        if (!File.Exists(assessmentPath))
        {
            return Blocked("fix", bugId, CodeAssessmentMissing,
                "The bug-cycle assessment for this bug is missing; run `doti bug assess` first.");
        }

        string assessmentSha = HashJson(assessmentPath);
        BugAssessment? assessment = ReadJson<BugAssessment>(assessmentPath);
        bool boundToAssessment = !string.IsNullOrWhiteSpace(boundAssessmentSha256)
            && string.Equals(boundAssessmentSha256, assessmentSha, StringComparison.OrdinalIgnoreCase);
        bool confirmed = string.Equals(assessment?.Verdict, BugVerdict.Confirmed, StringComparison.OrdinalIgnoreCase);
        if (!boundToAssessment || !confirmed)
        {
            return Blocked("fix", bugId, CodeFixUnbound,
                "A bug-cycle fix is not bound to a confirmed assessment; bind the fix to the current assessment's hash.");
        }

        string path = Path.Combine(dir, FixFile);
        WriteJson(path, new BugFixRecord(SchemaVersion, bugId, assessmentSha, summary, changedPaths ?? []));
        return Recorded("fix", bugId, repoRoot, path, BugStageOutcome.Pass, boundSha: assessmentSha);
    }

    /// <summary>TEST: record an honest verification bound to the fix. An evidence-free pass is downgraded.</summary>
    public static BugStageResult Test(string repoRoot, string bugId, string outcome, string evidence)
    {
        string dir = ResolveBugDir(repoRoot, bugId);
        string fixPath = Path.Combine(dir, FixFile);
        if (!File.Exists(fixPath))
        {
            return Blocked("test", bugId, CodeFixUnbound,
                "The bug-cycle fix for this bug is missing; bind a fix before testing.");
        }

        string fixSha = HashJson(fixPath);
        // No over-claiming: a `pass` must carry evidence; anything that is not a substantiated pass is recorded fail.
        bool substantiatedPass = string.Equals(outcome, BugStageOutcome.Pass, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(evidence);
        string honest = substantiatedPass ? BugStageOutcome.Pass : BugStageOutcome.Fail;
        string path = Path.Combine(dir, TestFile);
        WriteJson(path, new BugTestRecord(SchemaVersion, bugId, fixSha, honest, evidence ?? ""));
        return Recorded("test", bugId, repoRoot, path, honest, boundSha: fixSha);
    }

    /// <summary>The current assessment's binding hash, or null if there is none — the CLI auto-binds a fix to it.</summary>
    public static string? CurrentAssessmentSha(string repoRoot, string bugId)
    {
        string path = Path.Combine(ResolveBugDir(repoRoot, bugId), AssessmentFile);
        return File.Exists(path) ? HashJson(path) : null;
    }

    private static BugStageResult Recorded(
        string stage, string bugId, string repoRoot, string path, string outcome, string? boundSha) =>
        new(stage, bugId, outcome, RepoRelative(repoRoot, path), HashJson(path), boundSha, FailureCode: null, FailureMessage: null);

    private static BugStageResult Blocked(string stage, string bugId, string code, string message) =>
        new(stage, bugId, BugStageOutcome.Blocked, ArtifactPath: null, ArtifactSha256: null, BoundSha256: null, code, message);

    private static string EnsureBugDir(string repoRoot, string bugId)
    {
        string dir = ResolveBugDir(repoRoot, bugId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string ResolveBugDir(string repoRoot, string bugId)
    {
        if (!IsSafeBugId(bugId))
        {
            throw new ArgumentException(
                $"Invalid bug id '{bugId}'. Use letters, digits, '.', '_', or '-' (no path separators or '..').", nameof(bugId));
        }

        return Path.Combine(Path.GetFullPath(repoRoot), ".doti", "bugs", bugId);
    }

    private static bool IsSafeBugId(string bugId) =>
        !string.IsNullOrWhiteSpace(bugId)
        && bugId.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
        && !bugId.StartsWith('.');

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        File.WriteAllText(path, JsonSerializer.Serialize(value, options));
    }

    private static T? ReadJson<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonContractSerializerOptions.Create());

    private static string HashJson(string path) =>
        CanonicalContentHasher.HashFile(path, HashProfile.JsonSemantic).Sha256;

    private static string RepoRelative(string repoRoot, string path) =>
        Path.GetRelativePath(Path.GetFullPath(repoRoot), path).Replace('\\', '/');
}
