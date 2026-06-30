using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Runner.Cli;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// Proves the Runner CLI's input-validation mappings onto the envelope: bad invocation ⇒
/// <see cref="ExitClass.Usage"/> with a registry diagnostic, in-process (no external tools). The gate/check
/// command bodies that need real tooling are exercised end-to-end by the gate itself.
/// </summary>
public sealed class RunnerCommandsTests
{
    private static readonly CliMeta Meta = new("hx-runner", "1.0.0");

    private static void AssertUsage(CliResult r)
    {
        Assert.False(r.Ok);
        Assert.Equal((int)ExitClass.Usage, r.ExitCode);
        Assert.Equal(ErrorCodes.Usage_InvalidArguments, Assert.Single(r.Errors).Code);
    }

    [Fact]
    public void DotiRenderSkills_rejects_an_unknown_agent() =>
        AssertUsage(RunnerCommands.DotiRenderSkills(Meta, ".", "codex,bogus", check: true));

    [Fact]
    public void CycleStamp_requires_a_stage() =>
        AssertUsage(RunnerCommands.CycleStamp(Meta, ".", stage: "", feature: "", baseRef: ""));

    [Fact]
    public void CycleStamp_rejects_release_intent_outside_release_stage() =>
        AssertUsage(RunnerCommands.CycleStamp(Meta, ".", stage: "implement", feature: "", baseRef: "", releaseIntent: "minor"));

    [Fact]
    public void CycleStamp_rejects_unknown_release_intent() =>
        AssertUsage(RunnerCommands.CycleStamp(Meta, ".", stage: "release", feature: "", baseRef: "", releaseIntent: "tiny"));

    [Fact]
    public void CycleCheck_requires_a_stage() =>
        AssertUsage(RunnerCommands.CycleCheck(Meta, ".", stage: ""));

    [Fact]
    public void CycleReviewRebind_requires_a_target() =>
        AssertUsage(RunnerCommands.CycleReviewRebind(Meta, ".", target: "", attest: "no-impact", reason: ""));

    [Fact]
    public void CycleReviewRebind_requires_an_attest() =>
        AssertUsage(RunnerCommands.CycleReviewRebind(Meta, ".", target: "plan", attest: "", reason: ""));

    [Fact]
    public void QuestionCheck_requires_a_file() =>
        AssertUsage(RunnerCommands.QuestionCheck(Meta, file: ""));

    [Fact]
    public void ToolsFetch_rejects_an_unknown_tool() =>
        AssertUsage(RunnerCommands.ToolsFetch(Meta, ".", rid: "win-x64", toolFilter: "bogus"));

    [Fact]
    public void QuestionCheck_reports_a_missing_file_as_validation()
    {
        CliResult r = RunnerCommands.QuestionCheck(Meta, file: "does-not-exist-12345.json");
        Assert.False(r.Ok);
        Assert.Equal((int)ExitClass.Validation, r.ExitCode);
        Assert.Equal(ErrorCodes.Validation_Failed, Assert.Single(r.Errors).Code);
    }

    [Fact]
    public void TaskHashStamp_stamps_checked_tasks_and_reports_unchecked_with_stable_code()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-runner-task-hash-" + Guid.NewGuid().ToString("N"));
        try
        {
            string taskDir = Path.Combine(dir, "docs", "tasks");
            Directory.CreateDirectory(taskDir);
            string taskFile = Path.Combine(taskDir, "001-example-tasks.md");
            File.WriteAllText(taskFile,
                "- [x] `T001` (FR-001, SC-001) - Finish the first task.\n" +
                "- [ ] `T002` (FR-002, SC-002) - Finish the second task.\n");

            CliResult r = RunnerCommands.TaskHashStamp(Meta, dir, "001-example");

            Assert.False(r.Ok);
            Assert.Equal((int)ExitClass.Validation, r.ExitCode);
            Assert.Contains(r.Errors, e => e.Code == ErrorCodes.Validation_DotiTaskUnchecked && e.Target == "T002");
            string text = File.ReadAllText(taskFile);
            Assert.Contains("doti-task-hash", text, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void DotiPayloadCheck_reports_payload_parity()
    {
        CliResult r = RunnerCommands.DotiPayloadCheck(Meta, FindRepoRoot());

        Assert.True(r.Ok);
        Assert.Equal((int)ExitClass.Success, r.ExitCode);
        DotiPayloadCheckResult result = r.Data!.Deserialize<DotiPayloadCheckResult>(JsonContractSerializerOptions.Create())!;
        Assert.Equal(StageOutcome.Pass, result.Outcome);
        Assert.Empty(result.Drifted);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".doti", "core", "skills.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root with .doti/core/skills.json.");
    }
}
