using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hx.Scaffold.Core.Release;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed class VelopackReleaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hx-velopack-tests-" + Guid.NewGuid().ToString("N"));

    public VelopackReleaseTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData("RELEASES", "velopack-update-metadata")]
    [InlineData("doti-1.2.3-full.nupkg", "velopack-package")]
    [InlineData("DotiSetup.exe", "velopack-installer")]
    [InlineData("Doti.msi", "velopack-installer")]
    public void Classifier_accepts_velopack_installer_and_update_artifacts(string name, string type)
    {
        Assert.True(VelopackArtifactClassifier.IsVelopackArtifactName(name));
        Assert.Equal(type, VelopackArtifactClassifier.Classify(name));
    }

    [Theory]
    [InlineData("speckit-doti-v1.2.3-win-x64.zip")]
    [InlineData("source.zip")]
    [InlineData("speckit-doti-v1.2.3.tar.gz")]
    [InlineData("release.identity.json")]
    public void Classifier_rejects_raw_source_or_payload_archives(string name)
    {
        Assert.False(VelopackArtifactClassifier.IsVelopackArtifactName(name));
        Assert.Null(VelopackArtifactClassifier.Classify(name));
    }

    [Fact]
    public void Prepare_extracts_verified_dotnet_tool_package()
    {
        byte[] package = FakeNupkgWithVpkDll();
        WriteVelopackManifest(package);
        string packagePath = Path.Combine(_root, "tools", "velopack", "bin", "win-x64", "vpk.1.2.0.nupkg");
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        File.WriteAllBytes(packagePath, package);

        VelopackToolInvocation invocation = VelopackTool.Prepare(_root, "win-x64", Path.Combine(_root, "tmp"));

        Assert.Equal("dotnet", invocation.FileName);
        Assert.Equal(packagePath, invocation.PackagePath);
        Assert.EndsWith(Path.Combine("tools", "net8.0", "any", "vpk.dll"), invocation.ExtractedToolPath);
        Assert.True(File.Exists(invocation.ExtractedToolPath));
        Assert.Contains("vpk.dll", invocation.ArgumentsPrefix);
    }

    [Fact]
    public void Prepare_fails_when_pinned_package_is_missing()
    {
        WriteVelopackManifest(Encoding.UTF8.GetBytes("missing"));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => VelopackTool.Prepare(_root, "win-x64", Path.Combine(_root, "tmp")));

        Assert.Contains("Pinned Velopack CLI package", ex.Message);
        Assert.Contains("tools fetch --tool velopack", ex.Message);
    }

    [Theory]
    [InlineData("tools/Hx.Scaffold.Cli/Program.cs", "tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj")]
    [InlineData("scaffold/templates/dotnet-cli/src/HxScaffoldSample.Cli/Program.cs", "scaffold/templates/dotnet-cli/src/HxScaffoldSample.Cli/HxScaffoldSample.Cli.csproj")]
    public void Velopack_packaged_executables_call_startup_hook(string programPath, string projectPath)
    {
        string root = RepoRoot();
        string program = File.ReadAllText(Path.Combine(root, programPath));
        string project = File.ReadAllText(Path.Combine(root, projectPath));

        Assert.Contains("VelopackApp.Build().Run()", program);
        Assert.Contains("PackageReference Include=\"Velopack\"", project);
    }

    [Fact]
    public void Local_release_artifact_can_carry_velopack_identity_metadata()
    {
        var artifact = new LocalReleaseArtifact(
            "DotiSetup.exe",
            "abc123",
            42,
            Type: "velopack-installer",
            RuntimeIdentifier: "win-x64",
            Channel: "win",
            Version: "1.2.3",
            PackageId: "speckit-doti");

        Assert.Equal("velopack-installer", artifact.Type);
        Assert.Equal("win-x64", artifact.RuntimeIdentifier);
        Assert.Equal("win", artifact.Channel);
        Assert.Equal("1.2.3", artifact.Version);
        Assert.Equal("speckit-doti", artifact.PackageId);
    }

    [Fact]
    public void Local_release_result_carries_velopack_train_and_source_archive_exclusion_proof()
    {
        var train = new CycleReleaseTrain(
            JsonContractDefaults.SchemaVersion,
            Valid: true,
            Features:
            [
                new CycleReleaseTrainFeature(
                    "006-feature",
                    "drift-review",
                    "abc123",
                    "base..abc123",
                    "present",
                    "present",
                    "included",
                    [])
            ],
            Blockers: []);
        var result = new LocalReleaseResult(
            JsonContractDefaults.SchemaVersion,
            "speckit-doti",
            "1.2.3",
            "minor",
            new LocalReleaseTag("v1.2.3", "abc123", "tag-object", Created: true, Existing: false, "tag body", "git push origin v1.2.3"),
            "gitversion + v1.2.3",
            "speckit-doti",
            "win",
            "win-x64",
            "abc123",
            new LocalReleaseTarget("Doti", "speckit-doti", "tools/Hx.Scaffold.Cli", "hx", "hx", "DOTI_RELEASE_ROOT"),
            new LocalReleaseRootDecision("DOTI_RELEASE_ROOT", null, EnvironmentVariableRead: false, EnvironmentVariableIgnored: false, "explicit", @"D:\releases", null),
            new LocalReleaseEnvironmentPersistence(Requested: false, null, null, Written: false, null, null),
            LocalCopyProduced: true,
            SkippedReason: null,
            VersionDirectory: @"D:\releases\speckit-doti\1.2.3",
            LatestDirectory: @"D:\releases\speckit-doti\latest",
            Artifacts:
            [
                new LocalReleaseArtifact(
                    "DotiSetup.exe",
                    "abc123",
                    42,
                    Type: "velopack-installer",
                    RuntimeIdentifier: "win-x64",
                    Channel: "win",
                    Version: "1.2.3",
                    PackageId: "speckit-doti")
            ],
            VelopackArtifacts:
            [
                new LocalReleaseArtifact(
                    "DotiSetup.exe",
                    "abc123",
                    42,
                    Type: "velopack-installer",
                    RuntimeIdentifier: "win-x64",
                    Channel: "win",
                    Version: "1.2.3",
                    PackageId: "speckit-doti")
            ],
            PayloadChecks: [new LocalReleasePayloadCheck("tools/gitversion/bin/win-x64/gitversion.exe", "hash", 7)],
            ReleaseTrain: train,
            DocumentationProof: new ReleaseDocumentationProof(
                JsonContractDefaults.SchemaVersion,
                StageOutcome.Pass,
                "Release notes\n\n- 006-feature",
                ["006-feature"],
                [new ReleaseDocumentationFileProof("README.md", "updated", "contains every included release-train feature slug")],
                []),
            CommandName: "hx release",
            CommandVersion: "1.2.3",
            ConfigurationSource: "hx.config.json",
            ConfigurationPath: @"C:\tools\hx.config.json",
            ReleaseProduct: "velopack",
            SourceArchiveExcluded: true,
            Blockers: []);

        Assert.Equal("hx release", result.CommandName);
        Assert.Equal("1.2.3", result.CommandVersion);
        Assert.Equal("hx.config.json", result.ConfigurationSource);
        Assert.EndsWith("hx.config.json", result.ConfigurationPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("velopack", result.ReleaseProduct);
        Assert.True(result.SourceArchiveExcluded);
        Assert.Equal("006-feature", Assert.Single(result.ReleaseTrain!.Features).Feature);
        Assert.Equal(StageOutcome.Pass, result.DocumentationProof!.Outcome);
        Assert.All(result.VelopackArtifacts, artifact =>
        {
            Assert.StartsWith("velopack-", artifact.Type, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(artifact.RuntimeIdentifier));
            Assert.False(string.IsNullOrWhiteSpace(artifact.Channel));
            Assert.False(string.IsNullOrWhiteSpace(artifact.Version));
            Assert.False(string.IsNullOrWhiteSpace(artifact.PackageId));
            Assert.False(string.IsNullOrWhiteSpace(artifact.Sha256));
        });
        Assert.DoesNotContain(result.Artifacts, artifact =>
            artifact.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || artifact.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("1.2.3", "2.0.0", "major")]
    [InlineData("1.2.3", "1.3.0", "minor")]
    [InlineData("1.2.3", "1.2.4", "patch")]
    public void Release_version_policy_classifies_gitversion_calculated_increment(string previous, string current, string expected) =>
        Assert.Equal(expected, LocalReleaseVersionPolicy.ClassifyVersionChange(previous, current));

    [Fact]
    public void Release_version_policy_rejects_mismatched_operator_intent()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => LocalReleaseVersionPolicy.ValidateIntent("1.2.3", "1.3.0", "patch"));

        Assert.Contains("Release intent mismatch", ex.Message);
        Assert.Contains("requested patch", ex.Message);
        Assert.Contains("as a minor release", ex.Message);
        Assert.Contains("+semver", ex.Message);
    }

    private void WriteVelopackManifest(byte[] package)
    {
        string manifestPath = Path.Combine(_root, "tools", "velopack", "velopack.version.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            tool = "velopack",
            version = "1.2.0",
            assets = new[]
            {
                new
                {
                    rid = "win-x64",
                    executablePath = "tools/velopack/bin/win-x64/vpk.1.2.0.nupkg",
                    executableSha256 = Sha256(package),
                    executableName = "vpk.1.2.0.nupkg"
                }
            }
        }, JsonContractSerializerOptions.Create()));
    }

    private static byte[] FakeNupkgWithVpkDll()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry("tools/net8.0/any/vpk.dll");
            using Stream stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes("fake-vpk-dll"));
        }

        return buffer.ToArray();
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string RepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "scaffold-dotnet.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
