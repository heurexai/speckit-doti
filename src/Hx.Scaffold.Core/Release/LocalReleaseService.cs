using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Hx.Runner.Core.Platform;
using Hx.Tooling.Contracts;
using Hx.Version.Core;

namespace Hx.Scaffold.Core.Release;

public sealed record LocalReleaseRequest(
    string RepositoryRoot,
    string? ReleaseRoot,
    string? ReleaseRootEnvironmentVariable,
    bool SaveReleaseRoot,
    string? RuntimeIdentifier,
    string CommandVersion);

public static class LocalReleaseService
{
    public static LocalReleaseResult Run(LocalReleaseRequest request)
    {
        string repo = Path.GetFullPath(request.RepositoryRoot);
        string rid = string.IsNullOrWhiteSpace(request.RuntimeIdentifier)
            ? HostPlatformDetector.DetectCurrent().RuntimeIdentifier
            : request.RuntimeIdentifier.Trim();
        string projectName = SafeSegment(new DirectoryInfo(repo).Name);
        VersionResult version = GitVersionTool.Calculate(repo);
        string sourceCommit = Git(repo, "rev-parse HEAD").Trim();

        LocalReleaseRootDecision rootDecision = LocalReleaseRootResolver.Resolve(
            request.ReleaseRoot,
            request.ReleaseRootEnvironmentVariable,
            Environment.GetEnvironmentVariable);

        var persistence = new LocalReleaseEnvironmentPersistence(
            request.SaveReleaseRoot,
            request.SaveReleaseRoot ? rootDecision.EffectiveEnvironmentVariableName : null,
            request.SaveReleaseRoot ? rootDecision.ReleaseRoot : null,
            Written: false,
            Scope: null,
            Limitation: null);

        if (rootDecision.ReleaseRoot is null)
        {
            return new LocalReleaseResult(
                JsonContractDefaults.SchemaVersion,
                projectName,
                version.Version,
                rid,
                sourceCommit,
                rootDecision,
                persistence,
                LocalCopyProduced: false,
                SkippedReason: rootDecision.Reason ?? "no local release root configured",
                VersionDirectory: null,
                LatestDirectory: null,
                Artifacts: [],
                Blockers: []);
        }

        string releaseRoot = Path.GetFullPath(rootDecision.ReleaseRoot);
        EnsureRootIsSafe(repo, releaseRoot);

        string tempRoot = Path.Combine(Path.GetTempPath(), "speckit-doti-release-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            IReadOnlyList<LocalReleaseArtifact> artifacts = BuildArtifacts(
                repo,
                tempRoot,
                projectName,
                version.Version,
                rid,
                sourceCommit,
                request.CommandVersion);

            string projectRoot = EnsureInside(releaseRoot, Path.Combine(releaseRoot, projectName));
            string versionDir = EnsureInside(releaseRoot, Path.Combine(projectRoot, version.Version));
            string latestDir = EnsureInside(releaseRoot, Path.Combine(projectRoot, "latest"));
            PublishLocalCopy(tempRoot, versionDir, latestDir, artifacts, projectName, version.Version, sourceCommit);

            if (request.SaveReleaseRoot)
            {
                persistence = PersistEnvironmentRoot(rootDecision.EffectiveEnvironmentVariableName, releaseRoot);
            }

            return new LocalReleaseResult(
                JsonContractDefaults.SchemaVersion,
                projectName,
                version.Version,
                rid,
                sourceCommit,
                rootDecision with { ReleaseRoot = releaseRoot },
                persistence,
                LocalCopyProduced: true,
                SkippedReason: null,
                VersionDirectory: versionDir,
                LatestDirectory: latestDir,
                Artifacts: artifacts,
                Blockers: []);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static IReadOnlyList<LocalReleaseArtifact> BuildArtifacts(
        string repo,
        string tempRoot,
        string projectName,
        string version,
        string rid,
        string sourceCommit,
        string commandVersion)
    {
        string publish = Path.Combine(tempRoot, "publish");
        Dotnet(repo,
            $"publish tools/Hx.Scaffold.Cli -c Release -r {Quote(rid)} --self-contained " +
            "-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true " +
            $"-o {Quote(publish)}");

        string payloadName = $"{projectName}-v{version}-{rid}";
        string payload = Path.Combine(tempRoot, payloadName);
        Directory.CreateDirectory(payload);
        string sourceZip = Path.Combine(tempRoot, "source.zip");
        Git(repo, $"archive --format=zip -o {Quote(sourceZip)} HEAD");
        ZipFile.ExtractToDirectory(sourceZip, payload);

        foreach (string tool in new[] { "gitleaks", "sentrux", "gitversion" })
        {
            string source = Path.Combine(repo, "tools", tool, "bin");
            if (Directory.Exists(source))
            {
                DirectoryCopy.Copy(source, Path.Combine(payload, "tools", tool, "bin"), _ => true);
            }
        }

        string executableName = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? "hx.exe" : "hx";
        string publishedExe = Path.Combine(publish,
            rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? "Hx.Scaffold.Cli.exe" : "Hx.Scaffold.Cli");
        if (!File.Exists(publishedExe))
        {
            throw new InvalidOperationException("Published hx executable was not found: " + publishedExe);
        }

        File.Copy(publishedExe, Path.Combine(payload, executableName), overwrite: true);

        string archiveName = payloadName + ".zip";
        string archive = Path.Combine(tempRoot, archiveName);
        ZipFile.CreateFromDirectory(payload, archive, CompressionLevel.Optimal, includeBaseDirectory: false);
        string archiveSha = Sha256(archive);
        string checksum = archive + ".sha256";
        File.WriteAllText(checksum, $"{archiveSha}  {archiveName}");

        var artifacts = new List<LocalReleaseArtifact>
        {
            new(archiveName, archiveSha, new FileInfo(archive).Length),
            new(Path.GetFileName(checksum), Sha256(checksum), new FileInfo(checksum).Length)
        };

        string identity = Path.Combine(tempRoot, "release.identity.json");
        File.WriteAllText(identity, JsonSerializer.Serialize(new
        {
            schemaVersion = JsonContractDefaults.SchemaVersion,
            projectName,
            version,
            tag = "v" + version,
            runtimeIdentifier = rid,
            sourceCommit,
            command = "hx release",
            commandVersion,
            artifacts
        }, JsonContractSerializerOptions.Create()));
        artifacts.Add(new("release.identity.json", Sha256(identity), new FileInfo(identity).Length));
        return artifacts;
    }

    private static void PublishLocalCopy(
        string tempRoot,
        string versionDir,
        string latestDir,
        IReadOnlyList<LocalReleaseArtifact> artifacts,
        string projectName,
        string version,
        string sourceCommit)
    {
        string staging = versionDir + ".staging-" + Guid.NewGuid().ToString("N");
        string latestStaging = latestDir + ".staging-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(staging);
            foreach (LocalReleaseArtifact artifact in artifacts)
            {
                File.Copy(Path.Combine(tempRoot, artifact.Name), Path.Combine(staging, artifact.Name));
                VerifyArtifact(Path.Combine(staging, artifact.Name), artifact);
            }

            EnsureExistingVersionMatches(versionDir, projectName, version, sourceCommit);
            ReplaceDirectory(staging, versionDir);

            Directory.CreateDirectory(latestStaging);
            foreach (LocalReleaseArtifact artifact in artifacts)
            {
                File.Copy(Path.Combine(versionDir, artifact.Name), Path.Combine(latestStaging, artifact.Name));
                VerifyArtifact(Path.Combine(latestStaging, artifact.Name), artifact);
            }

            ReplaceDirectory(latestStaging, latestDir);
        }
        finally
        {
            TryDelete(staging);
            TryDelete(latestStaging);
        }
    }

    private static void EnsureExistingVersionMatches(string versionDir, string projectName, string version, string sourceCommit)
    {
        if (!Directory.Exists(versionDir))
        {
            return;
        }

        string identity = Path.Combine(versionDir, "release.identity.json");
        if (!File.Exists(identity))
        {
            throw new InvalidOperationException("Existing version release directory has no release.identity.json: " + versionDir);
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(identity));
        JsonElement root = document.RootElement;
        bool matches =
            StringProperty(root, "projectName") == projectName &&
            StringProperty(root, "version") == version &&
            StringProperty(root, "sourceCommit") == sourceCommit;
        if (!matches)
        {
            throw new InvalidOperationException("Existing version release directory belongs to a different release identity: " + versionDir);
        }
    }

    private static string? StringProperty(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static LocalReleaseEnvironmentPersistence PersistEnvironmentRoot(string variableName, string releaseRoot)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new InvalidOperationException("--save-release-root is supported only for the Windows user environment.");
        }

        Environment.SetEnvironmentVariable(variableName, releaseRoot, EnvironmentVariableTarget.User);
        return new LocalReleaseEnvironmentPersistence(true, variableName, releaseRoot, true, "user", null);
    }

    private static void VerifyArtifact(string path, LocalReleaseArtifact expected)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("Release artifact was not copied: " + path);
        }

        string actual = Sha256(path);
        if (!string.Equals(actual, expected.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Release artifact checksum mismatch: " + path);
        }
    }

    private static void ReplaceDirectory(string staging, string destination)
    {
        string? parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        Directory.Move(staging, destination);
    }

    private static void EnsureRootIsSafe(string repo, string releaseRoot)
    {
        string repoFull = EnsureTrailingSeparator(Path.GetFullPath(repo));
        string rootFull = EnsureTrailingSeparator(Path.GetFullPath(releaseRoot));
        if (string.Equals(repoFull, rootFull, StringComparison.OrdinalIgnoreCase)
            || rootFull.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Release root must be outside the source repository: " + releaseRoot);
        }
    }

    private static string EnsureInside(string root, string path)
    {
        string rootFull = EnsureTrailingSeparator(Path.GetFullPath(root));
        string full = Path.GetFullPath(path);
        if (!EnsureTrailingSeparator(full).StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved release output path escapes the release root: " + path);
        }

        return full;
    }

    private static string SafeSegment(string value)
    {
        string safe = new(value.Select(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-').ToArray());
        safe = safe.Trim('.', '-', '_');
        if (string.IsNullOrWhiteSpace(safe))
        {
            throw new InvalidOperationException("Project name is empty after filesystem-safe normalization.");
        }

        return safe;
    }

    private static string Dotnet(string workingDirectory, string arguments)
    {
        (int exitCode, string output) = ProcessRunner.Run("dotnet", arguments, workingDirectory, ProcessRunner.NestedDotnetEnv());
        if (exitCode != 0)
        {
            throw new InvalidOperationException("dotnet failed: " + ProcessRunner.Tail(output));
        }

        return output;
    }

    private static string Git(string workingDirectory, string arguments)
    {
        (int exitCode, string output) = ProcessRunner.Run("git", arguments, workingDirectory);
        if (exitCode != 0)
        {
            throw new InvalidOperationException("git failed: " + ProcessRunner.Tail(output));
        }

        return output;
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Quote(string value) => '"' + value.Replace("\"", "\\\"", StringComparison.Ordinal) + '"';

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; callers still receive the release result/failure that matters.
        }
    }
}
