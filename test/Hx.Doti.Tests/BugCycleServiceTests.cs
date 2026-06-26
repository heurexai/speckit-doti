using Hx.Doti.Core.Bug;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 007 T033 (FR-034): the enforced bug mini-cycle. Assess is read-only, fix fails closed unless bound to a confirmed
/// assessment, and test cannot over-claim a pass without evidence.
/// </summary>
public sealed class BugCycleServiceTests
{
    [Fact]
    public void Assess_writes_only_the_assessment_and_never_touches_code()
    {
        using var repo = new TempRepo();
        string sentinel = repo.Write("src/Sample.cs", "// untouched\n");

        BugStageResult result = BugCycleService.Assess(repo.Root,
            new BugAssessment(1, "001-null-ref", BugVerdict.Confirmed, "high", "guard the null", "NRE on login"));

        Assert.Equal(BugStageOutcome.Pass, result.Outcome);
        Assert.Equal(".doti/bugs/001-null-ref/assessment.json", result.ArtifactPath);
        Assert.Equal("// untouched\n", File.ReadAllText(sentinel)); // assess is read-only — code is never changed
        // The only artifact the assess stage produced under the bug dir is the assessment itself.
        Assert.Single(Directory.GetFiles(Path.Combine(repo.Root, ".doti", "bugs", "001-null-ref")));
    }

    [Fact]
    public void Fix_without_an_assessment_fails_closed_assessment_missing()
    {
        using var repo = new TempRepo();

        BugStageResult result = BugCycleService.Fix(repo.Root, "001-null-ref", boundAssessmentSha256: "", "fix it", []);

        Assert.Equal(BugStageOutcome.Blocked, result.Outcome);
        Assert.Equal(BugCycleService.CodeAssessmentMissing, result.FailureCode);
    }

    [Fact]
    public void Fix_not_bound_to_the_assessment_fails_closed_unbound()
    {
        using var repo = new TempRepo();
        BugCycleService.Assess(repo.Root, new BugAssessment(1, "b", BugVerdict.Confirmed, "high", "r", "s"));

        // The assessment exists, but the fix carries a binding hash that does not match it -> bug-fix-unbound.
        BugStageResult result = BugCycleService.Fix(repo.Root, "b", boundAssessmentSha256: "deadbeef", "fix", []);

        Assert.Equal(BugStageOutcome.Blocked, result.Outcome);
        Assert.Equal(BugCycleService.CodeFixUnbound, result.FailureCode);
    }

    [Fact]
    public void Fix_on_a_non_confirmed_assessment_fails_closed_unbound()
    {
        using var repo = new TempRepo();
        BugStageResult assess = BugCycleService.Assess(repo.Root,
            new BugAssessment(1, "b", BugVerdict.NeedsInfo, "low", "r", "s"));

        // Even bound to the right hash, a not-yet-confirmed bug cannot be fixed.
        BugStageResult result = BugCycleService.Fix(repo.Root, "b", assess.ArtifactSha256!, "fix", []);

        Assert.Equal(BugStageOutcome.Blocked, result.Outcome);
        Assert.Equal(BugCycleService.CodeFixUnbound, result.FailureCode);
    }

    [Fact]
    public void Fix_bound_to_a_confirmed_assessment_is_recorded()
    {
        using var repo = new TempRepo();
        BugStageResult assess = BugCycleService.Assess(repo.Root,
            new BugAssessment(1, "b", BugVerdict.Confirmed, "high", "r", "s"));

        BugStageResult result = BugCycleService.Fix(repo.Root, "b", assess.ArtifactSha256!, "guarded the null", ["src/Login.cs"]);

        Assert.Equal(BugStageOutcome.Pass, result.Outcome);
        Assert.Equal(assess.ArtifactSha256, result.BoundSha256);
        Assert.True(File.Exists(Path.Combine(repo.Root, ".doti", "bugs", "b", "fix.json")));
    }

    [Fact]
    public void Test_does_not_over_claim_a_pass_without_evidence()
    {
        using var repo = new TempRepo();
        BugStageResult assess = BugCycleService.Assess(repo.Root,
            new BugAssessment(1, "b", BugVerdict.Confirmed, "high", "r", "s"));
        BugCycleService.Fix(repo.Root, "b", assess.ArtifactSha256!, "fix", []);

        BugStageResult overclaim = BugCycleService.Test(repo.Root, "b", BugStageOutcome.Pass, evidence: "");
        Assert.Equal(BugStageOutcome.Fail, overclaim.Outcome); // an evidence-free pass is downgraded

        BugStageResult honest = BugCycleService.Test(repo.Root, "b", BugStageOutcome.Pass, evidence: "12 tests green; repro fixed");
        Assert.Equal(BugStageOutcome.Pass, honest.Outcome);
    }

    [Fact]
    public void Test_without_a_fix_fails_closed()
    {
        using var repo = new TempRepo();
        BugCycleService.Assess(repo.Root, new BugAssessment(1, "b", BugVerdict.Confirmed, "high", "r", "s"));

        BugStageResult result = BugCycleService.Test(repo.Root, "b", BugStageOutcome.Pass, "evidence");

        Assert.Equal(BugStageOutcome.Blocked, result.Outcome);
        Assert.Equal(BugCycleService.CodeFixUnbound, result.FailureCode);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("..")]
    public void Unsafe_bug_id_is_refused(string bugId)
    {
        using var repo = new TempRepo();

        Assert.Throws<ArgumentException>(() =>
            BugCycleService.Assess(repo.Root, new BugAssessment(1, bugId, BugVerdict.Confirmed, "high", "r", "s")));
    }

    private sealed class TempRepo : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hx-bug-" + Guid.NewGuid().ToString("n"));

        public TempRepo() => Directory.CreateDirectory(Root);

        public string Write(string relativePath, string content)
        {
            string full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
