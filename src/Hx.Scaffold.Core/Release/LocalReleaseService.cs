using System.Security.Cryptography;
using System.Text.Json;
using Hx.Cycle.Core;
using Hx.Cycle.Core.Documentation;
using Hx.Runner.Core.Platform;
using Hx.Scaffold.Core.Configuration;
using Hx.Tooling.Contracts;
using Hx.Version.Core;

namespace Hx.Scaffold.Core.Release;

public sealed record LocalReleaseRequest(
    string RepositoryRoot,
    HxLocalConfiguration Configuration,
    string? RuntimeIdentifier,
    string CommandVersion,
    string ReleaseIntent);

public static class LocalReleaseService
{
    public static LocalReleaseResult Run(LocalReleaseRequest request)
    {
        string repo = Path.GetFullPath(request.RepositoryRoot);
        string rid = string.IsNullOrWhiteSpace(request.RuntimeIdentifier)
            ? HostPlatformDetector.DetectCurrent().RuntimeIdentifier
            : request.RuntimeIdentifier.Trim();
        string releaseIntent = NormalizeReleaseIntent(request.ReleaseIntent);
        HxLocalConfigurationLoader.Validate(request.Configuration);
        LocalReleaseRootDecision rootDecision = ResolveRootFromConfiguration(request.Configuration);
        LocalReleaseTarget target = ReleaseTargetManifest.Load(repo);
        var cycle = new CycleService(repo);
        CycleReleaseTrain releaseTrain = cycle.GetReleaseTrain();
        if (!releaseTrain.Valid)
        {
            throw new InvalidOperationException(
                "Release train is invalid: " + string.Join("; ", releaseTrain.Blockers));
        }

        ReleaseDocumentationProof documentationProof = ReleaseDocumentationInspector.Inspect(repo, releaseTrain);
        if (documentationProof.Outcome != StageOutcome.Pass)
        {
            throw new InvalidOperationException(
                "Release documentation proof failed: " + string.Join("; ", documentationProof.Blockers));
        }

        string projectName = SafeSegment(target.PackageName);
        var persistence = new LocalReleaseEnvironmentPersistence(
            Requested: false,
            VariableName: null,
            Value: null,
            Written: false,
            Scope: null,
            Limitation: "release roots are read from executable-adjacent hx.config.json; environment persistence is no longer supported");
        VersionResult version = GitVersionTool.Calculate(repo);
        string sourceCommit = Git(repo, "rev-parse HEAD").Trim();
        ValidateReleaseIntent(repo, version.Version, releaseIntent);
        LocalReleaseTag tag = EnsureReleaseTag(repo, version.Version, releaseIntent, sourceCommit);
        string velopackPackageId = projectName;
        string velopackChannel = ChannelFromRid(rid);

        if (rootDecision.ReleaseRoot is null)
        {
            return BuildSkippedResult(
                request,
                projectName,
                version,
                releaseIntent,
                tag,
                rid,
                sourceCommit,
                target,
                rootDecision,
                persistence,
                releaseTrain,
                documentationProof,
                velopackPackageId,
                velopackChannel);
        }

        return BuildAndPublishResult(
            request,
            repo,
            cycle,
            projectName,
            version,
            releaseIntent,
            tag,
            rid,
            sourceCommit,
            target,
            rootDecision,
            persistence,
            releaseTrain,
            documentationProof,
            velopackPackageId,
            velopackChannel);
    }

    private static LocalReleaseResult BuildSkippedResult(
        LocalReleaseRequest request,
        string projectName,
        VersionResult version,
        string releaseIntent,
        LocalReleaseTag tag,
        string rid,
        string sourceCommit,
        LocalReleaseTarget target,
        LocalReleaseRootDecision rootDecision,
        LocalReleaseEnvironmentPersistence persistence,
        CycleReleaseTrain releaseTrain,
        ReleaseDocumentationProof documentationProof,
        string velopackPackageId,
        string velopackChannel) =>
        new(
            JsonContractDefaults.SchemaVersion,
            projectName,
            version.Version,
            releaseIntent,
            tag,
            version.Source,
            velopackPackageId,
            velopackChannel,
            rid,
            sourceCommit,
            target,
            rootDecision,
            persistence,
            LocalCopyProduced: false,
            SkippedReason: rootDecision.Reason ?? "no local release root configured",
            VersionDirectory: null,
            LatestDirectory: null,
            Artifacts: [],
            VelopackArtifacts: [],
            PayloadChecks: [],
            ReleaseTrain: releaseTrain,
            DocumentationProof: documentationProof,
            CommandName: "hx release",
            CommandVersion: request.CommandVersion,
            ConfigurationSource: request.Configuration.Source,
            ConfigurationPath: request.Configuration.SourcePath,
            ReleaseProduct: "velopack",
            SourceArchiveExcluded: true,
            Blockers: []);

    private static LocalReleaseResult BuildAndPublishResult(
        LocalReleaseRequest request,
        string repo,
        CycleService cycle,
        string projectName,
        VersionResult version,
        string releaseIntent,
        LocalReleaseTag tag,
        string rid,
        string sourceCommit,
        LocalReleaseTarget target,
        LocalReleaseRootDecision rootDecision,
        LocalReleaseEnvironmentPersistence persistence,
        CycleReleaseTrain releaseTrain,
        ReleaseDocumentationProof documentationProof,
        string velopackPackageId,
        string velopackChannel)
    {
        string releaseRoot = Path.GetFullPath(rootDecision.ReleaseRoot!);
        EnsureRootIsSafe(repo, releaseRoot);

        string tempRoot = Path.Combine(Path.GetTempPath(), "speckit-doti-release-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            ReleaseArtifactBuild artifacts = BuildArtifacts(
                repo,
                tempRoot,
                target,
                version.Version,
                releaseIntent,
                tag,
                rid,
                sourceCommit,
                request.CommandVersion,
                request.Configuration.Source,
                request.Configuration.SourcePath,
                documentationProof,
                velopackPackageId,
                velopackChannel,
                releaseTrain);

            string projectRoot = EnsureInside(releaseRoot, Path.Combine(releaseRoot, projectName));
            string versionDir = EnsureInside(releaseRoot, Path.Combine(projectRoot, version.Version));
            string latestDir = EnsureInside(releaseRoot, Path.Combine(projectRoot, "latest"));
            PublishLocalCopy(tempRoot, versionDir, latestDir, artifacts.Artifacts, projectName, version.Version, sourceCommit);

            cycle.MarkReleaseTrainReleased();
            return new LocalReleaseResult(
                JsonContractDefaults.SchemaVersion,
                projectName,
                version.Version,
                releaseIntent,
                tag,
                version.Source,
                velopackPackageId,
                velopackChannel,
                rid,
                sourceCommit,
                target,
                rootDecision with { ReleaseRoot = releaseRoot },
                persistence,
                LocalCopyProduced: true,
                SkippedReason: null,
                VersionDirectory: versionDir,
                LatestDirectory: latestDir,
                Artifacts: artifacts.Artifacts,
                VelopackArtifacts: artifacts.VelopackArtifacts,
                PayloadChecks: artifacts.PayloadChecks,
                ReleaseTrain: releaseTrain,
                DocumentationProof: documentationProof,
                CommandName: "hx release",
                CommandVersion: request.CommandVersion,
                ConfigurationSource: request.Configuration.Source,
                ConfigurationPath: request.Configuration.SourcePath,
                ReleaseProduct: "velopack",
                SourceArchiveExcluded: true,
                Blockers: []);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static LocalReleaseRootDecision ResolveRootFromConfiguration(HxLocalConfiguration configuration)
    {
        if (!configuration.LocalReleaseOutput.Enabled)
        {
            return new LocalReleaseRootDecision(
                EffectiveEnvironmentVariableName: "",
                RequestedEnvironmentVariableName: null,
                EnvironmentVariableRead: false,
                EnvironmentVariableIgnored: false,
                Source: "hx-config",
                ReleaseRoot: null,
                Reason: "local release output disabled by hx.config.json");
        }

        string releaseDirectory = configuration.LocalReleaseOutput.Directory!.Trim();
        return new LocalReleaseRootDecision(
            EffectiveEnvironmentVariableName: "",
            RequestedEnvironmentVariableName: null,
            EnvironmentVariableRead: false,
            EnvironmentVariableIgnored: false,
            Source: "hx-config",
            ReleaseRoot: releaseDirectory,
            Reason: "local release output configured by hx.config.json");
    }

    private sealed record ReleaseArtifactBuild(
        IReadOnlyList<LocalReleaseArtifact> Artifacts,
        IReadOnlyList<LocalReleaseArtifact> VelopackArtifacts,
        IReadOnlyList<LocalReleasePayloadCheck> PayloadChecks);

    private static ReleaseArtifactBuild BuildArtifacts(
        string repo,
        string tempRoot,
        LocalReleaseTarget target,
        string version,
        string releaseIntent,
        LocalReleaseTag tag,
        string rid,
        string sourceCommit,
        string commandVersion,
        string configurationSource,
        string configurationPath,
        ReleaseDocumentationProof documentationProof,
        string velopackPackageId,
        string velopackChannel,
        CycleReleaseTrain releaseTrain)
    {
        string projectName = SafeSegment(target.PackageName);
        string publishProject = target.PublishProject.Replace('/', Path.DirectorySeparatorChar);
        string publish = Path.Combine(tempRoot, "publish");
        Dotnet(repo,
            $"publish {Quote(publishProject)} -c Release -r {Quote(rid)} --self-contained " +
            "-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true " +
            $"-o {Quote(publish)}");

        StagePackagedAssets(repo, publish);

        string executableName = ExecutableFileName(target.ExecutableName, rid);
        string publishedExe = Path.Combine(publish, ExecutableFileName(target.PublishedExecutableName, rid));
        if (!File.Exists(publishedExe))
        {
            throw new InvalidOperationException(
                $"Published release executable '{target.PublishedExecutableName}' was not found: {publishedExe}");
        }

        string expectedExe = Path.Combine(publish, executableName);
        if (!string.Equals(Path.GetFullPath(publishedExe), Path.GetFullPath(expectedExe), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(publishedExe, expectedExe, overwrite: true);
        }

        IReadOnlyList<LocalReleasePayloadCheck> payloadChecks = InspectPayload(publish);
        PackVelopack(repo, publish, tempRoot, velopackPackageId, version, executableName, target.ProductName, velopackChannel, rid);
        IReadOnlyList<LocalReleaseArtifact> velopackArtifacts = DiscoverVelopackArtifacts(tempRoot)
            .Select(artifact => artifact with
            {
                RuntimeIdentifier = rid,
                Channel = velopackChannel,
                Version = version,
                PackageId = velopackPackageId
            })
            .ToArray();
        if (velopackArtifacts.Count == 0)
        {
            throw new InvalidOperationException(
                "Velopack did not produce any recognized installer/update artifacts. Refusing raw-archive-only release output.");
        }

        var artifacts = new List<LocalReleaseArtifact>(velopackArtifacts);
        foreach (LocalReleaseArtifact velopackArtifact in velopackArtifacts)
        {
            string checksumPath = Path.Combine(tempRoot, velopackArtifact.Name + ".sha256");
            File.WriteAllText(checksumPath, $"{velopackArtifact.Sha256}  {velopackArtifact.Name}");
            artifacts.Add(new LocalReleaseArtifact(
                Path.GetFileName(checksumPath),
                Sha256(checksumPath),
                new FileInfo(checksumPath).Length,
                Type: "checksum",
                RuntimeIdentifier: rid,
                Channel: velopackChannel,
                Version: version,
                PackageId: velopackPackageId));
        }

        string identity = Path.Combine(tempRoot, "release.identity.json");
        File.WriteAllText(identity, JsonSerializer.Serialize(new
        {
            schemaVersion = JsonContractDefaults.SchemaVersion,
            projectName,
            version,
            releaseIntent,
            tag,
            gitVersionSource = $"gitversion + {tag.Name}",
            velopackPackageId,
            velopackChannel,
            runtimeIdentifier = rid,
            sourceCommit,
            target,
            command = "hx release",
            commandVersion,
            configurationSource,
            configurationPath,
            documentationProof,
            releaseProduct = "velopack",
            sourceArchiveExcluded = true,
            releaseTrain,
            artifacts,
            velopackArtifacts,
            payloadChecks
        }, JsonContractSerializerOptions.Create()));
        artifacts.Add(new(
            "release.identity.json",
            Sha256(identity),
            new FileInfo(identity).Length,
            Type: "release-identity",
            RuntimeIdentifier: rid,
            Channel: velopackChannel,
            Version: version,
            PackageId: velopackPackageId));
        return new ReleaseArtifactBuild(artifacts, velopackArtifacts, payloadChecks);
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

    private static string NormalizeReleaseIntent(string? intent)
    {
        string normalized = string.IsNullOrWhiteSpace(intent) ? "patch" : intent.Trim().ToLowerInvariant();
        return normalized is "major" or "minor" or "patch"
            ? normalized
            : throw new InvalidOperationException($"Unknown release intent '{intent}'. Use major, minor, or patch.");
    }

    private static void ValidateReleaseIntent(string repo, string version, string releaseIntent)
    {
        string? previous = LatestVersionTag(repo, version);
        LocalReleaseVersionPolicy.ValidateIntent(previous, version, releaseIntent);
    }

    private static LocalReleaseTag EnsureReleaseTag(string repo, string version, string releaseIntent, string sourceCommit)
    {
        string tagName = "v" + version;
        string existingCommit = GitOptional(repo, $"rev-list -n 1 {Quote(tagName)}").Trim();
        string message =
            $"Release {tagName}\n\n" +
            $"Release-Intent: {releaseIntent}\n" +
            $"GitVersion-Version: {version}\n" +
            $"Source-Commit: {sourceCommit}\n" +
            "Created-By: hx release";

        bool created = false;
        if (string.IsNullOrWhiteSpace(existingCommit))
        {
            string messageFile = Path.Combine(Path.GetTempPath(), "doti-release-tag-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.WriteAllText(messageFile, message);
                Git(repo, $"tag -a {Quote(tagName)} -F {Quote(messageFile)} {Quote(sourceCommit)}");
            }
            finally
            {
                try { File.Delete(messageFile); }
                catch { /* best-effort temp cleanup */ }
            }

            created = true;
            existingCommit = Git(repo, $"rev-list -n 1 {Quote(tagName)}").Trim();
        }

        if (!string.Equals(existingCommit, sourceCommit, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Release tag {tagName} points at {existingCommit}, not the release commit {sourceCommit}.");
        }

        string? objectId = GitOptional(repo, $"rev-parse {Quote(tagName)}").Trim();
        string tagMessage = GitOptional(repo, $"tag -l {Quote(tagName)} --format=%(contents)").Trim();
        if (!tagMessage.Contains($"Release-Intent: {releaseIntent}", StringComparison.Ordinal)
            || !tagMessage.Contains($"GitVersion-Version: {version}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Release tag {tagName} exists but does not carry the expected Doti release identity trailers.");
        }

        return new LocalReleaseTag(
            tagName,
            existingCommit,
            string.IsNullOrWhiteSpace(objectId) ? null : objectId,
            Created: created,
            Existing: !created,
            Message: tagMessage,
            PushCommand: $"git push origin {tagName}");
    }

    private static string? LatestVersionTag(string repo, string currentVersion)
    {
        string output = GitOptional(repo, "tag --list v[0-9]* --sort=-v:refname");
        foreach (string tag in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = tag.Trim();
            if (candidate.Length > 1 && !string.Equals(candidate[1..], currentVersion, StringComparison.OrdinalIgnoreCase))
            {
                return candidate[1..];
            }
        }

        return null;
    }

    private static void StagePackagedAssets(string repo, string publish)
    {
        foreach (string tool in new[] { "gitleaks", "sentrux", "gitversion" })
        {
            string source = Path.Combine(repo, "tools", tool, "bin");
            if (Directory.Exists(source))
            {
                DirectoryCopy.Copy(source, Path.Combine(publish, "tools", tool, "bin"), _ => true);
            }
        }

        string doti = Path.Combine(repo, ".doti");
        if (Directory.Exists(doti))
        {
            DirectoryCopy.Copy(doti, Path.Combine(publish, ".doti"), _ => true);
        }

        string legacyDoti = Path.Combine(repo, "doti");
        if (Directory.Exists(legacyDoti))
        {
            DirectoryCopy.Copy(legacyDoti, Path.Combine(publish, "doti"), _ => true);
        }
    }

    private static IReadOnlyList<LocalReleasePayloadCheck> InspectPayload(string publish)
    {
        return Directory.EnumerateFiles(publish, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new LocalReleasePayloadCheck(
                Path.GetRelativePath(publish, path).Replace('\\', '/'),
                Sha256(path),
                new FileInfo(path).Length))
            .ToArray();
    }

    private static void PackVelopack(
        string repo,
        string publish,
        string outputDir,
        string packageId,
        string version,
        string mainExe,
        string title,
        string channel,
        string rid)
    {
        string arguments =
            "pack " +
            $"--packId {Quote(packageId)} " +
            $"--packVersion {Quote(version)} " +
            $"--packDir {Quote(publish)} " +
            $"--mainExe {Quote(mainExe)} " +
            $"--packTitle {Quote(title)} " +
            $"--channel {Quote(channel)} " +
            $"--runtime {Quote(rid)} " +
            $"--outputDir {Quote(outputDir)}";

        VelopackToolInvocation tool = VelopackTool.Prepare(repo, rid, outputDir);
        (int exitCode, string output) = ProcessRunner.Run(
            tool.FileName,
            tool.ArgumentsPrefix + " " + arguments,
            repo,
            ProcessRunner.NestedDotnetEnv());
        if (exitCode != 0)
        {
            throw new InvalidOperationException("vpk pack failed: " + ProcessRunner.Tail(output));
        }
    }

    private static IReadOnlyList<LocalReleaseArtifact> DiscoverVelopackArtifacts(string tempRoot)
    {
        return Directory.EnumerateFiles(tempRoot, "*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                string name = Path.GetFileName(path);
                return VelopackArtifactClassifier.IsVelopackArtifactName(name);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                string name = Path.GetFileName(path);
                return new LocalReleaseArtifact(
                    name,
                    Sha256(path),
                    new FileInfo(path).Length,
                    Type: VelopackArtifactClassifier.Classify(name) ?? "velopack-artifact");
            })
            .ToArray();
    }

    private static string ChannelFromRid(string rid)
    {
        int dash = rid.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 ? rid[..dash] : rid;
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

    private static string GitOptional(string workingDirectory, string arguments)
    {
        (int exitCode, string output) = ProcessRunner.Run("git", arguments, workingDirectory);
        return exitCode == 0 ? output : "";
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Quote(string value) => '"' + value.Replace("\"", "\\\"", StringComparison.Ordinal) + '"';

    private static string ExecutableFileName(string baseName, string rid)
    {
        if (!rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase)
            || baseName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return baseName;
        }

        return baseName + ".exe";
    }

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
