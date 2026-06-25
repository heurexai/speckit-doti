using System.Text;
using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cli.Kernel.Tests;

/// <summary>Proves the StageOutcome→envelope mapping and the NDJSON streaming event writer.</summary>
public sealed class CliResultsTests
{
    private static readonly CliMeta Meta = new("hx-test", "1.0.0");

    [Fact]
    public void FromStage_Pass_is_a_success()
    {
        CliResult r = CliResults.FromStage(Meta, "check", StageOutcome.Pass, "all good", new { n = 1 });
        Assert.True(r.Ok);
        Assert.Equal(CliOutcome.Success, r.Outcome);
        Assert.Equal((int)ExitClass.Success, r.ExitCode);
        Assert.NotNull(r.Data);
    }

    [Fact]
    public void FromStage_Skipped_is_a_no_op_success()
    {
        CliResult r = CliResults.FromStage(Meta, "check", StageOutcome.Skipped, "nothing to do", null);
        Assert.True(r.Ok);
        Assert.Equal(CliOutcome.Skipped, r.Outcome);
        Assert.Equal((int)ExitClass.Success, r.ExitCode);
    }

    [Theory]
    [InlineData(StageOutcome.Fail)]
    [InlineData(StageOutcome.Blocked)]
    public void FromStage_FailOrBlocked_fails_with_the_failclass_code(StageOutcome outcome)
    {
        CliResult r = CliResults.FromStage(Meta, "verify", outcome, "no good", new { n = 1 }, ExitClass.Integrity);
        Assert.False(r.Ok);
        Assert.Equal((int)ExitClass.Integrity, r.ExitCode);
        Assert.NotNull(r.Data); // the failing result is preserved for the agent
        Diagnostic d = Assert.Single(r.Errors);
        Assert.Equal(ErrorCodes.Integrity_VerificationFailed, d.Code);
    }

    [Fact]
    public void DefaultCode_maps_each_exit_class_to_its_canonical_code()
    {
        Assert.Equal(ErrorCodes.Usage_InvalidArguments, CliResults.DefaultCode(ExitClass.Usage));
        Assert.Equal(ErrorCodes.Validation_Failed, CliResults.DefaultCode(ExitClass.Validation));
        Assert.Equal(ErrorCodes.Integrity_VerificationFailed, CliResults.DefaultCode(ExitClass.Integrity));
        Assert.Equal(ErrorCodes.Internal_Unhandled, CliResults.DefaultCode(ExitClass.Internal));
    }

    [Fact]
    public void Blocked_requires_operator_without_fabricating_a_decision()
    {
        CliResult r = CliResults.Blocked(Meta, "sample blocked command", ExitClass.Validation,
            [Diag.Of(ErrorCodes.Validation_Failed, "stale gate proof")], "refused");
        Assert.False(r.Ok);
        Assert.Equal(CliOutcome.Blocked, r.Outcome);
        Assert.True(r.RequiresOperator);
        Assert.Null(r.Decision); // a fail-closed refusal is not a multiple-choice question
        Assert.Equal((int)ExitClass.Validation, r.ExitCode);
    }

    [Fact]
    public void WriteEvent_is_one_compact_lf_terminated_json_line()
    {
        using var stream = new MemoryStream();
        CliWriter.WriteEvent(stream, new CliEvent("step", "hygiene", "pass", "verified"));
        string text = Encoding.UTF8.GetString(stream.ToArray());

        Assert.EndsWith("\n", text);
        Assert.DoesNotContain('\r', text);
        Assert.Single(text.TrimEnd('\n').Split('\n'));
        using JsonDocument doc = JsonDocument.Parse(text);
        Assert.Equal("step", doc.RootElement.GetProperty("event").GetString());
        Assert.Equal("hygiene", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("pass", doc.RootElement.GetProperty("status").GetString());
    }
}
