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
    public void CycleCheck_requires_a_stage() =>
        AssertUsage(RunnerCommands.CycleCheck(Meta, ".", stage: ""));

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
}
