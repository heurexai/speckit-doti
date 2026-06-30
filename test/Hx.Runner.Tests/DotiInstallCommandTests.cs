using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using System.Text.Json;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class DotiInstallCommandTests
{
    [Fact]
    public void Doti_install_requires_explicit_repo_argument()
    {
        CliResult result = Hx.Runner.Cli.RunnerCommands.DotiInstall(
            new CliMeta("runner", "0.0.0-test"),
            targetRepo: null,
            agentsCsv: "codex",
            force: false,
            noCommit: true);

        Assert.False(result.Ok);
        Assert.Equal((int)ExitClass.Usage, result.ExitCode);
        Assert.Contains("requires an explicit --repo", result.Errors.Single().Message);
    }

    [Fact]
    public void Doti_install_json_contract_reports_classification_and_path_effects()
    {
        string target = Path.Combine(Path.GetTempPath(), "hx-runner-doti-install-" + Guid.NewGuid().ToString("n"));
        try
        {
            CliResult result = Hx.Runner.Cli.RunnerCommands.DotiInstall(
                new CliMeta("runner", "0.0.0-test"),
                targetRepo: target,
                agentsCsv: "codex",
                force: false,
                noCommit: true);

            Assert.True(result.Ok, string.Join("\n", result.Errors.Select(e => e.Message)));
            using JsonDocument doc = JsonDocument.Parse(result.Data!.ToJsonString());
            JsonElement install = doc.RootElement.GetProperty("install");
            Assert.Equal("installed-new-target", install.GetProperty("classification").GetString());
            Assert.True(install.GetProperty("targetCreated").GetBoolean());
            Assert.True(install.GetProperty("installed").GetArrayLength() > 0);
            Assert.True(install.TryGetProperty("preserved", out _));
            Assert.True(install.TryGetProperty("removed", out _));
            Assert.True(install.TryGetProperty("skipped", out _));
            Assert.True(install.TryGetProperty("blocked", out _));
            Assert.Contains("Classification: installed-new-target", result.Summary);
        }
        finally
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }
}
