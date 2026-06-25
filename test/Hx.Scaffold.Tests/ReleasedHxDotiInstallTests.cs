using System.CommandLine;
using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;
using Hx.Scaffold.Core.Configuration;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed class ReleasedHxDotiInstallTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hx-released-doti-install-" + Guid.NewGuid().ToString("N"));

    public ReleasedHxDotiInstallTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Released_hx_doti_install_uses_executable_adjacent_payload_without_source_checkout()
    {
        string exeDir = Path.Combine(_root, "installed-hx");
        string target = Path.Combine(_root, "target-repo");
        Directory.CreateDirectory(exeDir);
        WriteHxConfig(exeDir);
        WriteDotiPayload(exeDir);
        RootCommand root = ScaffoldCommandFactory.Create(new CliMeta("hx", "1.2.3-test"), exeDir);

        int exitCode = root.Parse(["doti", "install", "--repo", target, "--agents", "codex,claude", "--json"]).Invoke();

        Assert.Equal((int)ExitClass.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(target, ".doti", "core", "skills.json")));
        Assert.True(File.Exists(Path.Combine(target, ".doti", "agent-context.md")));
        Assert.True(File.Exists(Path.Combine(target, ".agents", "skills", "01-doti-specify", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(target, ".claude", "skills", "01-doti-specify", "SKILL.md")));
    }

    [Fact]
    public void Released_hx_doti_install_fails_when_installed_payload_is_missing()
    {
        string exeDir = Path.Combine(_root, "installed-hx-without-payload");
        string target = Path.Combine(_root, "target-repo");
        Directory.CreateDirectory(exeDir);
        WriteHxConfig(exeDir);

        CliResult result = ScaffoldCommands.DotiInstall(
            new CliMeta("hx", "1.2.3-test"),
            target,
            "codex",
            force: false,
            exeDir);

        Assert.False(result.Ok);
        Assert.Equal((int)ExitClass.Validation, result.ExitCode);
        Assert.Contains("installed .doti/core/skills.json", result.Errors.Single().Message);
    }

    private static void WriteHxConfig(string exeDir)
    {
        File.WriteAllText(Path.Combine(exeDir, HxLocalConfiguration.FileName),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                localReleaseOutput = new
                {
                    enabled = false
                }
            }, JsonContractSerializerOptions.Create()));
    }

    private static void WriteDotiPayload(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".doti", "core", "templates", "commands"));
        Directory.CreateDirectory(Path.Combine(root, ".doti", "profiles", "dotnet-cli"));
        Directory.CreateDirectory(Path.Combine(root, ".doti", "workflows", "doti"));
        Directory.CreateDirectory(Path.Combine(root, ".doti", "memory"));
        Directory.CreateDirectory(Path.Combine(root, ".doti", "integrations"));
        File.WriteAllText(Path.Combine(root, ".doti", "core", "skills.json"),
            """
            {
              "schemaVersion": 1,
              "maturity": "command-aware-advisory",
              "commandTemplateDir": ".doti/core/templates/commands",
              "agentContextRef": ".doti/agent-context.md",
              "introTemplate": "Read `{agentContextRef}`, then follow `{commandTemplate}`.",
              "operatorQuestionProtocol": "",
              "skills": [
                { "name": "doti-specify", "description": "Spec.", "argumentHint": "[goal]", "highlights": [], "nextStage": "Run `/02-doti-clarify`." }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(root, ".doti", "profiles", "dotnet-cli", "profile.json"),
            """
            { "selfHostingStatus": { "commandAvailabilityFootnote": "Footnote text.", "rootMaturityNote": "Maturity note." } }
            """);
        File.WriteAllText(Path.Combine(root, ".doti", "core", "templates", "agent-context-template.md"), "context body\n");
        File.WriteAllText(Path.Combine(root, ".doti", "core", "templates", "commands", "doti-specify.md"), "# specify\n");
        File.WriteAllText(Path.Combine(root, ".doti", "workflows", "doti", "workflow.yml"),
            "schemaVersion: 2\nstages:\n  - id: specify\n    prereqs: []\n");
        File.WriteAllText(Path.Combine(root, ".doti", "memory", "constitution.md"), "# Constitution\n");
        File.WriteAllText(Path.Combine(root, ".doti", "integrations", "doti.manifest.json"), "{}\n");
    }
}
