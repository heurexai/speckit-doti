using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Hx.Cli.Kernel;
using Hx.Doti.Core.ManagedAssets;
using Hx.Runner.Core.Process;
using Hx.Scaffold.Cli;
using Hx.Scaffold.Core.Update;
using Hx.Scaffold.Core.Versioning;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// Proves the Scaffold CLI's envelope mapping: <c>profile</c> succeeds; <c>new</c> validates its required
/// args up front (Usage, no generation). The heavy end-to-end <c>new</c> generation envelope is exercised by the
/// gated <see cref="ScaffoldNewSmokeTests"/> (HX_SCAFFOLD_SMOKE).
/// </summary>
public sealed partial class ScaffoldCommandsTests
{
    private static readonly CliMeta Meta = new("hx-scaffold", "1.0.0");

    [Fact]
    public void Profile_emits_the_default_profile()
    {
        CliResult r = ScaffoldCommands.Profile(Meta);
        Assert.True(r.Ok);
        Assert.Equal((int)ExitClass.Success, r.ExitCode);
        Assert.Equal("profile", r.Command);
        Assert.NotNull(r.Data);
    }

    [Theory]
    [InlineData("", "out")]
    [InlineData("Name", "")]
    public void New_requires_name_and_output(string name, string output)
    {
        CliResult r = ScaffoldCommands.New(Meta, name, "Heurex", output, "dotnet-cli", "codex,claude");
        Assert.False(r.Ok);
        Assert.Equal((int)ExitClass.Usage, r.ExitCode);
        Assert.Equal(ErrorCodes.Usage_InvalidArguments, Assert.Single(r.Errors).Code);
    }

    [Fact]
    public void Version_runningOnly_succeeds()
    {
        CliResult r = ScaffoldCommands.Version(Meta, "");

        Assert.True(r.Ok);
        ScaffoldVersionReport report = r.Data!.Deserialize<ScaffoldVersionReport>(JsonContractSerializerOptions.Create())!;
        Assert.Equal("1.0.0", report.Running.Version);
        Assert.Null(report.Target);
    }

    [Fact]
    public void Version_reports_template_and_skill_modifications_separately()
    {
        string repo = NewVersionedRepo();
        try
        {
            string workflow = Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml");
            string skill = Path.Combine(repo, ".agents", "skills", "doti-specify", "SKILL.md");
            File.WriteAllText(workflow, "schemaVersion: 2\nstages:\n  - id: clarify\n");
            File.AppendAllText(skill, "\nlocal change\n");

            CliResult r = ScaffoldCommands.Version(Meta, repo);

            Assert.True(r.Ok);
            ScaffoldVersionReport report = r.Data!.Deserialize<ScaffoldVersionReport>(JsonContractSerializerOptions.Create())!;
            Assert.Equal("modified", report.ManagedAssets?.State);
            Assert.Contains(report.ManagedAssets!.ModifiedWorkflowTemplates, m => m.Path == ".doti/workflows/doti/workflow.yml");
            Assert.Contains(report.ManagedAssets.ModifiedSkillGeneratedInstructions, m => m.Path == ".agents/skills/doti-specify/SKILL.md");
        }
        finally
        {
            ForceDelete(repo);
        }
    }

}
