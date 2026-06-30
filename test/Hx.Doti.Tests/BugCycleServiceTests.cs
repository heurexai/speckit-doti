using Hx.Doti.Core.Bug;
using Hx.Tooling.Contracts;
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

    // ---- 030 (bug-release-bridge): release-ready bug-cycle members for the release train ----

    [Fact]
    public void ReleaseReadyBugMembers_includes_a_test_passed_bound_bug_cycle()
    {
        using var repo = new TempRepo();
        DriveTestPassedBug(repo, "030-bug-release-bridge");

        IReadOnlyList<CycleReleaseTrainFeature> members = BugCycleService.ReleaseReadyBugMembers(repo.Root);

        CycleReleaseTrainFeature member = Assert.Single(members);
        Assert.Equal("030-bug-release-bridge", member.Feature);
        Assert.Equal("bug", member.CompletedStage);
        Assert.Equal("included", member.InclusionStatus);
        Assert.Equal("pass", member.TaskCompletionStatus);
        Assert.Empty(member.Blockers);
    }

    [Fact]
    public void ReleaseReadyBugMembers_excludes_an_assessed_only_bug_cycle()
    {
        using var repo = new TempRepo();
        // Confirmed assessment but no fix + no test → not release-ready (fail-closed: omitted, not a blocking member).
        BugCycleService.Assess(repo.Root, new BugAssessment(1, "b", BugVerdict.Confirmed, "high", "r", "s"));

        Assert.Empty(BugCycleService.ReleaseReadyBugMembers(repo.Root));
    }

    [Fact]
    public void ReleaseReadyBugMembers_excludes_a_bug_cycle_whose_test_did_not_pass()
    {
        using var repo = new TempRepo();
        BugStageResult assess = BugCycleService.Assess(repo.Root,
            new BugAssessment(1, "b", BugVerdict.Confirmed, "high", "r", "s"));
        BugCycleService.Fix(repo.Root, "b", assess.ArtifactSha256!, "fix", ["src/X.cs"]);
        // An evidence-free pass is honestly downgraded to fail by the test stage → NOT release-ready.
        BugStageResult test = BugCycleService.Test(repo.Root, "b", BugStageOutcome.Pass, evidence: "");
        Assert.Equal(BugStageOutcome.Fail, test.Outcome);

        Assert.Empty(BugCycleService.ReleaseReadyBugMembers(repo.Root));
    }

    [Fact]
    public void ReleaseReadyBugMembers_is_empty_when_there_are_no_bug_cycles()
    {
        using var repo = new TempRepo();

        Assert.Empty(BugCycleService.ReleaseReadyBugMembers(repo.Root));
    }

    [Fact]
    public void ReleaseReadyBugMembers_excludes_an_already_released_bug_cycle()
    {
        using var repo = new TempRepo();
        DriveTestPassedBug(repo, "021-old-shipped");

        // 030: the injected released-predicate reports the bug already shipped → the bridge omits it (no re-release).
        Assert.Empty(BugCycleService.ReleaseReadyBugMembers(repo.Root, _ => true));
    }

    [Fact]
    public void ReleaseReadyBugMembers_keeps_an_unreleased_bug_cycle()
    {
        using var repo = new TempRepo();
        DriveTestPassedBug(repo, "030-bug-release-bridge");

        // 030: nothing released (a fresh/untagged repo) → the test-passed bug stays a member.
        CycleReleaseTrainFeature member = Assert.Single(BugCycleService.ReleaseReadyBugMembers(repo.Root, _ => false));
        Assert.Equal("030-bug-release-bridge", member.Feature);
    }

    private static void DriveTestPassedBug(TempRepo repo, string bugId)
    {
        BugStageResult assess = BugCycleService.Assess(repo.Root,
            new BugAssessment(1, bugId, BugVerdict.Confirmed, "high", "remediate", "summary"));
        BugCycleService.Fix(repo.Root, bugId, assess.ArtifactSha256!, "guarded the path", ["src/Login.cs"]);
        BugStageResult test = BugCycleService.Test(repo.Root, bugId, BugStageOutcome.Pass, "12 tests green; repro fixed");
        Assert.Equal(BugStageOutcome.Pass, test.Outcome);
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
