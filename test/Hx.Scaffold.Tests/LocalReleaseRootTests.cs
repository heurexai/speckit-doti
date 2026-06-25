using System.CommandLine;
using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;
using Hx.Scaffold.Core.Configuration;
using Hx.Scaffold.Core.Release;
using Hx.Tooling.Contracts;
using System.Text.Json;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed class LocalReleaseRootTests
{
    [Fact]
    public void Missing_executable_adjacent_hx_config_fails_operational_commands()
    {
        using TempRepo exe = TempRepo.Create();
        RootCommand root = ScaffoldCommandFactory.Create(new CliMeta("hx", "0.0.0-test"), exe.Root);

        Assert.Equal((int)ExitClass.Validation, root.Parse(["profile", "--json"]).Invoke());
        Assert.Equal((int)ExitClass.Validation, root.Parse(["version", "--json"]).Invoke());
        Assert.Equal((int)ExitClass.Validation, root.Parse(["new", "--name", "Demo", "--output", Path.Combine(exe.Root, "out"), "--json"]).Invoke());
        Assert.Equal((int)ExitClass.Validation, root.Parse(["release", "--repo", exe.Root, "--json"]).Invoke());
        Assert.Equal((int)ExitClass.Validation, root.Parse(["prereq", "check", "--json"]).Invoke());
        Assert.Equal((int)ExitClass.Validation, root.Parse(["prereq", "install", "--json"]).Invoke());
    }

    [Fact]
    public void Help_and_describe_remain_available_without_hx_config()
    {
        using TempRepo exe = TempRepo.Create();
        RootCommand root = ScaffoldCommandFactory.Create(new CliMeta("hx", "0.0.0-test"), exe.Root);

        TextWriter original = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            int helpExit = CliApp.Invoke(root, new CliMeta("hx", "0.0.0-test"),
                ["release", "--help-mode", "plain", "--help"], "speckit-doti", "tagline");
            Assert.Equal((int)ExitClass.Success, helpExit);
            Assert.Contains("hx release", writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal((int)ExitClass.Success, root.Parse(["describe", "--json"]).Invoke());
    }

    [Fact]
    public void Enabled_local_release_output_requires_absolute_directory()
    {
        var missing = new HxLocalConfiguration
        {
            SchemaVersion = 1,
            SourcePath = "hx.config.json",
            LocalReleaseOutput = new HxLocalReleaseOutputConfiguration { Enabled = true }
        };
        var blank = new HxLocalConfiguration
        {
            SchemaVersion = 1,
            SourcePath = "hx.config.json",
            LocalReleaseOutput = new HxLocalReleaseOutputConfiguration { Enabled = true, Directory = " " }
        };
        var relative = new HxLocalConfiguration
        {
            SchemaVersion = 1,
            SourcePath = "hx.config.json",
            LocalReleaseOutput = new HxLocalReleaseOutputConfiguration { Enabled = true, Directory = "releases" }
        };

        Assert.Contains("directory is required", Assert.Throws<InvalidOperationException>(() => HxLocalConfigurationLoader.Validate(missing)).Message);
        Assert.Contains("directory is required", Assert.Throws<InvalidOperationException>(() => HxLocalConfigurationLoader.Validate(blank)).Message);
        Assert.Contains("absolute path", Assert.Throws<InvalidOperationException>(() => HxLocalConfigurationLoader.Validate(relative)).Message);
    }

    [Fact]
    public void Disabled_local_release_output_succeeds_without_directory()
    {
        var configuration = new HxLocalConfiguration
        {
            SchemaVersion = 1,
            SourcePath = "hx.config.json",
            LocalReleaseOutput = new HxLocalReleaseOutputConfiguration { Enabled = false }
        };

        HxLocalConfigurationLoader.Validate(configuration);
    }

    [Fact]
    public void Hx_config_loads_from_executable_directory_not_current_directory()
    {
        using TempRepo exe = TempRepo.Create();
        using TempRepo cwd = TempRepo.Create();
        string releaseRoot = Path.Combine(exe.Root, "releases");
        WriteHxConfig(exe.Root, enabled: true, releaseRoot);
        string original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(cwd.Root);

            HxLocalConfiguration configuration = HxLocalConfigurationLoader.LoadRequired(exe.Root);

            Assert.Equal(Path.Combine(exe.Root, "hx.config.json"), configuration.SourcePath);
            Assert.Equal(releaseRoot, configuration.LocalReleaseOutput.Directory);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public void Release_intent_flags_are_mutually_exclusive()
    {
        CliResult result = ScaffoldCommands.Release(
            new CliMeta("hx", "0.0.0-test"),
            ".",
            "",
            ValidConfig(),
            major: true,
            minor: true);

        Assert.False(result.Ok);
        Assert.Equal((int)ExitClass.Usage, result.ExitCode);
        Assert.Contains("Specify at most one release intent", result.Errors.Single().Message);
    }

    [Fact]
    public void Release_help_and_describe_do_not_expose_removed_release_root_flags()
    {
        using TempRepo exe = TempRepo.Create();
        RootCommand root = ScaffoldCommandFactory.Create(new CliMeta("hx", "0.0.0-test"), exe.Root);
        Command release = root.Subcommands.Single(command => command.Name == "release");

        string help = CliRenderer.RenderPlainHelp(root, release, ["release"], new CliMeta("hx", "0.0.0-test"),
            "speckit-doti", "tagline");
        CliDescribe describe = DescribeWalker.Describe(new CliMeta("hx", "0.0.0-test"), root, ErrorCodes.All);
        CliDescribeCommand describedRelease = describe.Root.Subcommands.Single(command => command.Name == "release");

        Assert.DoesNotContain("--release-root", help);
        Assert.DoesNotContain("--release-root-env", help);
        Assert.DoesNotContain("--save-release-root", help);
        Assert.DoesNotContain(describedRelease.Options, option => option.Name == "--release-root");
        Assert.DoesNotContain(describedRelease.Options, option => option.Name == "--release-root-env");
        Assert.DoesNotContain(describedRelease.Options, option => option.Name == "--save-release-root");
    }

    [Fact]
    public void Release_target_manifest_loads_repo_declared_product()
    {
        using TempRepo repo = TempRepo.Create();
        repo.WriteProject("src/Ergon.Cli/Ergon.Cli.csproj");
        repo.WriteReleaseManifest(new
        {
            schemaVersion = 1,
            productName = "Ergon",
            packageName = "ergon",
            publishProject = "src/Ergon.Cli/Ergon.Cli.csproj",
            publishedExecutableName = "ergon",
            executableName = "ergon",
            defaultReleaseRootEnvironmentVariable = "HEUREX_RELEASE_ROOT"
        });

        LocalReleaseTarget target = ReleaseTargetManifest.Load(repo.Root);

        Assert.Equal("Ergon", target.ProductName);
        Assert.Equal("ergon", target.PackageName);
        Assert.Equal("src/Ergon.Cli/Ergon.Cli.csproj", target.PublishProject);
        Assert.Equal("ergon", target.PublishedExecutableName);
        Assert.Equal("ergon", target.ExecutableName);
        Assert.Equal("HEUREX_RELEASE_ROOT", target.DefaultReleaseRootEnvironmentVariable);
    }

    [Fact]
    public void Default_release_target_manifest_points_at_generated_cli_product()
    {
        using TempRepo repo = TempRepo.Create();
        repo.WriteProject("src/Contoso.App.Cli/Contoso.App.Cli.csproj");

        ReleaseTargetManifest.WriteDefault(
            repo.Root,
            productName: "Contoso.App",
            publishProject: "src/Contoso.App.Cli/Contoso.App.Cli.csproj",
            publishedExecutableName: "Contoso.App.Cli",
            executableName: "Contoso.App",
            defaultReleaseRootEnvironmentVariable: "DOTI_RELEASE_ROOT");

        LocalReleaseTarget target = ReleaseTargetManifest.Load(repo.Root);

        Assert.Equal("Contoso.App", target.ProductName);
        Assert.Equal("Contoso.App", target.PackageName);
        Assert.Equal("src/Contoso.App.Cli/Contoso.App.Cli.csproj", target.PublishProject);
        Assert.Equal("Contoso.App.Cli", target.PublishedExecutableName);
        Assert.Equal("Contoso.App", target.ExecutableName);
        Assert.Equal("DOTI_RELEASE_ROOT", target.DefaultReleaseRootEnvironmentVariable);
    }

    [Fact]
    public void Scaffold_release_target_writer_uses_generated_cli_defaults()
    {
        using TempRepo repo = TempRepo.Create();
        repo.WriteProject("src/Hx.Sample.Cli/Hx.Sample.Cli.csproj");

        ScaffoldReleaseTargetWriter.WriteDefault(repo.Root, "Hx.Sample");

        LocalReleaseTarget target = ReleaseTargetManifest.Load(repo.Root);
        Assert.Equal("Hx.Sample", target.ProductName);
        Assert.Equal("Hx.Sample", target.PackageName);
        Assert.Equal("src/Hx.Sample.Cli/Hx.Sample.Cli.csproj", target.PublishProject);
        Assert.Equal("Hx.Sample.Cli", target.PublishedExecutableName);
        Assert.Equal("Hx.Sample", target.ExecutableName);
        Assert.Equal("DOTI_RELEASE_ROOT", target.DefaultReleaseRootEnvironmentVariable);
    }

    [Fact]
    public void Release_target_manifest_preserves_explicit_package_name_for_velopack_package_id()
    {
        using TempRepo repo = TempRepo.Create();
        repo.WriteProject("src/Contoso.App.Cli/Contoso.App.Cli.csproj");

        ReleaseTargetManifest.WriteDefault(
            repo.Root,
            productName: "Contoso App",
            packageName: "contoso-app",
            publishProject: "src/Contoso.App.Cli/Contoso.App.Cli.csproj",
            publishedExecutableName: "Contoso.App.Cli",
            executableName: "Contoso.App",
            defaultReleaseRootEnvironmentVariable: "DOTI_RELEASE_ROOT");

        LocalReleaseTarget target = ReleaseTargetManifest.Load(repo.Root);

        Assert.Equal("Contoso App", target.ProductName);
        Assert.Equal("contoso-app", target.PackageName);
    }

    [Fact]
    public void Release_target_manifest_is_required_before_release_side_effects()
    {
        using TempRepo repo = TempRepo.Create();

        CliResult result = ScaffoldCommands.Release(
            new CliMeta("hx", "0.0.0-test"),
            repo.Root,
            "",
            ValidConfig());

        Assert.False(result.Ok);
        Assert.Equal((int)ExitClass.Validation, result.ExitCode);
        Assert.Contains(".doti/release.json", result.Errors.Single().Message);
    }

    [Fact]
    public void Invalid_hx_config_fails_before_release_manifest_inspection()
    {
        using TempRepo repo = TempRepo.Create();
        var invalid = new HxLocalConfiguration
        {
            SchemaVersion = 1,
            SourcePath = Path.Combine(repo.Root, "hx.config.json"),
            LocalReleaseOutput = new HxLocalReleaseOutputConfiguration { Enabled = true, Directory = "relative" }
        };

        CliResult result = ScaffoldCommands.Release(
            new CliMeta("hx", "0.0.0-test"),
            repo.Root,
            "",
            invalid);

        Assert.False(result.Ok);
        Assert.Equal((int)ExitClass.Validation, result.ExitCode);
        Assert.Contains("absolute path", result.Errors.Single().Message);
        Assert.DoesNotContain(".doti/release.json", result.Errors.Single().Message);
    }

    [Fact]
    public void Release_target_manifest_rejects_publish_project_that_escapes_repo()
    {
        using TempRepo repo = TempRepo.Create();
        repo.WriteReleaseManifest(new
        {
            schemaVersion = 1,
            productName = "Bad",
            packageName = "bad",
            publishProject = "../Bad.Cli/Bad.Cli.csproj",
            publishedExecutableName = "bad",
            executableName = "bad",
            defaultReleaseRootEnvironmentVariable = "DOTI_RELEASE_ROOT"
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ReleaseTargetManifest.Load(repo.Root));

        Assert.Contains("publishProject must be a relative path inside the repository", ex.Message);
    }

    private static void WriteHxConfig(string executableDirectory, bool enabled, string? releaseRoot)
    {
        File.WriteAllText(Path.Combine(executableDirectory, HxLocalConfiguration.FileName),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                localReleaseOutput = new
                {
                    enabled,
                    directory = releaseRoot
                }
            }, JsonContractSerializerOptions.Create()));
    }

    private static HxLocalConfiguration ValidConfig() => new()
    {
        SchemaVersion = 1,
        SourcePath = Path.Combine(Path.GetTempPath(), "hx.config.json"),
        LocalReleaseOutput = new HxLocalReleaseOutputConfiguration
        {
            Enabled = true,
            Directory = Path.Combine(Path.GetTempPath(), "hx-release-output")
        }
    };

    private sealed class TempRepo : IDisposable
    {
        private TempRepo(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TempRepo Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "hx-release-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempRepo(root);
        }

        public void WriteProject(string relativePath)
        {
            string full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        }

        public void WriteReleaseManifest(object manifest)
        {
            string full = Path.Combine(Root, ReleaseTargetManifest.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, JsonSerializer.Serialize(manifest, JsonContractSerializerOptions.Create()));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
