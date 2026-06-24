using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;
using Hx.Scaffold.Core.Release;
using Hx.Tooling.Contracts;
using System.Text.Json;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed class LocalReleaseRootTests
{
    [Fact]
    public void Explicit_root_wins_over_named_environment_variable()
    {
        LocalReleaseRootDecision decision = LocalReleaseRootResolver.Resolve(
            @"D:\releases",
            "CUSTOM_RELEASE_ROOT",
            name => throw new InvalidOperationException("environment should not be read: " + name));

        Assert.Equal("explicit", decision.Source);
        Assert.Equal(@"D:\releases", decision.ReleaseRoot);
        Assert.Equal("CUSTOM_RELEASE_ROOT", decision.EffectiveEnvironmentVariableName);
        Assert.False(decision.EnvironmentVariableRead);
        Assert.True(decision.EnvironmentVariableIgnored);
    }

    [Fact]
    public void Default_environment_variable_is_doti_release_root()
    {
        LocalReleaseRootDecision decision = LocalReleaseRootResolver.Resolve(
            null,
            null,
            name => name == "DOTI_RELEASE_ROOT" ? @"D:\releases" : null);

        Assert.Equal("default-environment", decision.Source);
        Assert.Equal("DOTI_RELEASE_ROOT", decision.EffectiveEnvironmentVariableName);
        Assert.Equal(@"D:\releases", decision.ReleaseRoot);
        Assert.True(decision.EnvironmentVariableRead);
    }

    [Fact]
    public void Named_environment_variable_is_read_when_no_explicit_root_is_provided()
    {
        LocalReleaseRootDecision decision = LocalReleaseRootResolver.Resolve(
            null,
            "HX_RELEASES",
            name => name == "HX_RELEASES" ? @"D:\hx-releases" : null);

        Assert.Equal("named-environment", decision.Source);
        Assert.Equal("HX_RELEASES", decision.EffectiveEnvironmentVariableName);
        Assert.Equal(@"D:\hx-releases", decision.ReleaseRoot);
    }

    [Fact]
    public void Target_default_environment_variable_is_used_when_no_override_is_provided()
    {
        LocalReleaseRootDecision decision = LocalReleaseRootResolver.Resolve(
            null,
            null,
            name => name == "ERGON_RELEASE_ROOT" ? @"D:\ergon-releases" : null,
            "ERGON_RELEASE_ROOT");

        Assert.Equal("default-environment", decision.Source);
        Assert.Equal("ERGON_RELEASE_ROOT", decision.EffectiveEnvironmentVariableName);
        Assert.Equal(@"D:\ergon-releases", decision.ReleaseRoot);
    }

    [Fact]
    public void Save_release_root_requires_an_explicit_release_root()
    {
        CliResult result = ScaffoldCommands.Release(
            new CliMeta("hx", "0.0.0-test"),
            ".",
            "",
            "",
            "",
            saveReleaseRoot: true);

        Assert.False(result.Ok);
        Assert.Equal((int)ExitClass.Usage, result.ExitCode);
        Assert.Contains("--save-release-root requires", result.Errors.Single().Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("DOTI_RELEASE_ROOT")]
    [InlineData("_CUSTOM123")]
    public void Environment_variable_name_validation_accepts_safe_names(string name)
    {
        bool valid = string.IsNullOrEmpty(name) || LocalReleaseRootResolver.IsValidEnvironmentVariableName(name);
        Assert.True(valid);
    }

    [Theory]
    [InlineData("1BAD")]
    [InlineData("BAD-NAME")]
    [InlineData("BAD NAME")]
    public void Environment_variable_name_validation_rejects_unsafe_names(string name) =>
        Assert.False(LocalReleaseRootResolver.IsValidEnvironmentVariableName(name));

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
    public void Release_target_manifest_is_required_before_release_side_effects()
    {
        using TempRepo repo = TempRepo.Create();

        CliResult result = ScaffoldCommands.Release(
            new CliMeta("hx", "0.0.0-test"),
            repo.Root,
            "",
            @"D:\releases",
            "",
            saveReleaseRoot: false);

        Assert.False(result.Ok);
        Assert.Equal((int)ExitClass.Validation, result.ExitCode);
        Assert.Contains(".doti/release.json", result.Errors.Single().Message);
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
