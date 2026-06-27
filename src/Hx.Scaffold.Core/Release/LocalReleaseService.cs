using System.Security.Cryptography;
using System.Text.Json;
using Hx.Cycle.Core;
using Hx.Cycle.Core.Documentation;
using Hx.Runner.Core.Packaging;
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
    // 007 T028: when no local release root is configured the release validates + tags but builds no artifacts, so it
    // produces no channel product. The BUILD path computes the real product via ComposeReleaseProduct.
    private const string ReleaseProductNone = "none";

    public static LocalReleaseResult Run(LocalReleaseRequest request)
    {
        string repo = Path.GetFullPath(request.RepositoryRoot);
        string rid = string.IsNullOrWhiteSpace(request.RuntimeIdentifier)
            ? HostPlatformDetector.DetectCurrent().RuntimeIdentifier
            : request.RuntimeIdentifier.Trim();
        string? explicitIntent = NormalizeExplicitReleaseIntent(request.ReleaseIntent);
        HxLocalConfigurationLoader.Validate(request.Configuration);
        LocalReleaseTarget target = ReleaseTargetManifest.Load(repo);
        LocalReleaseRootDecision rootDecision = ResolveRootFromConfiguration(
            request.Configuration, target.DefaultReleaseRootEnvironmentVariable);
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
        string releaseIntent = ResolveReleaseIntent(repo, version.Version, explicitIntent);
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
            ReleaseProduct: ReleaseProductNone,
            SourceArchiveExcluded: true,
            Blockers: [],
            InstallLocationProof: null);

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
                ReleaseProduct: artifacts.ReleaseProduct,
                SourceArchiveExcluded: true,
                Blockers: [],
                InstallLocationProof: artifacts.InstallLocationProof,
                PackageId: artifacts.PackageId,
                Channel: artifacts.Channel,
                ChannelInstallProofs: artifacts.ChannelInstallProofs);
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    // Resolve the local release root with a SINGLE precedence (no hard-coded machine path in a committed config):
    //   1. hx.config.json localReleaseOutput.directory (explicit, absolute) — WINS;
    //   2. else the environment variable named by localReleaseOutput.environmentVariable, then the release-target
    //      manifest's defaultReleaseRootEnvironmentVariable, then DOTI_RELEASE_ROOT;
    //   3. else unavailable — the local copy is skipped (the tag + package proofs still run).
    private static LocalReleaseRootDecision ResolveRootFromConfiguration(
        HxLocalConfiguration configuration, string defaultEnvironmentVariableName)
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

        return LocalReleaseRootResolver.Resolve(
            explicitReleaseRoot: configuration.LocalReleaseOutput.Directory,
            requestedEnvironmentVariableName: configuration.LocalReleaseOutput.EnvironmentVariable,
            readEnvironmentVariable: Environment.GetEnvironmentVariable,
            defaultEnvironmentVariableName: string.IsNullOrWhiteSpace(defaultEnvironmentVariableName)
                ? LocalReleaseRootResolver.DefaultEnvironmentVariableName
                : defaultEnvironmentVariableName);
    }

    private sealed record ReleaseArtifactBuild(
        IReadOnlyList<LocalReleaseArtifact> Artifacts,
        IReadOnlyList<LocalReleaseArtifact> VelopackArtifacts,
        IReadOnlyList<LocalReleasePayloadCheck> PayloadChecks,
        LocalReleaseInstallLocationProof? InstallLocationProof,
        string ReleaseProduct,
        string? PackageId,
        string? Channel,
        IReadOnlyList<ChannelInstallProof> ChannelInstallProofs);

    // 007 T028: the framework-dependent global-tool package + its source-free install smoke proof.
    internal sealed record GlobalToolChannelResult(
        LocalReleaseArtifact PackageArtifact,
        string PackageId,
        string PackageVersion,
        LocalReleaseInstallLocationProof InstallProof,
        ChannelInstallProof ChannelProof);

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
        (string projectName, IReadOnlyList<LocalReleasePayloadCheck> payloadChecks) =
            StagePublishAndInspect(repo, tempRoot, target, rid);

        // 007 T028: retarget off the interim no-package state — produce the framework-dependent global-tool package,
        // smoke it source-free in a no-source install location, and record the channel-neutral release identity. The
        // Windows MSIX is a CI/Store-only channel (makeappx needs the Windows SDK; the Store signs it), so its curated
        // layout + signed .msix + submission live in store-release.yml and the channel proof is recorded advisory here.
        var artifacts = new List<LocalReleaseArtifact>();
        GlobalToolChannelResult globalTool = BuildGlobalToolChannel(repo, target, tempRoot);
        artifacts.Add(globalTool.PackageArtifact);

        // The packed global tool must ALSO carry no source — the no-source gate looks INSIDE the .nupkg (FR-006).
        ReleaseSourceScanResult packageScan = ReleaseSourceInspector.Scan(tempRoot);
        if (packageScan.Outcome != StageOutcome.Pass)
        {
            throw new InvalidOperationException(
                $"{ReleaseSourceInspector.ViolationCode}: the global-tool package carries the tool's build tree (FR-006): " +
                string.Join("; ", packageScan.Findings.Take(5).Select(f => $"{f.Artifact}!{f.Entry} ({f.Marker})")));
        }

        ChannelInstallProof? msixProof = MsixChannelProof(repo);
        var channelProofs = new List<ChannelInstallProof> { globalTool.ChannelProof };
        if (msixProof is not null)
        {
            channelProofs.Add(msixProof);
        }

        string releaseProduct = ComposeReleaseProduct(globalTool: true, msix: msixProof is not null);
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
            packageId = globalTool.PackageId,
            channel = DistributionChannelId.GlobalTool,
            runtimeIdentifier = rid,
            sourceCommit,
            target,
            command = "hx release",
            commandVersion,
            configurationSource,
            configurationPath,
            documentationProof,
            releaseProduct,
            sourceArchiveExcluded = true,
            releaseTrain,
            artifacts = artifacts.ToArray(),
            velopackArtifacts = Array.Empty<LocalReleaseArtifact>(),
            payloadChecks,
            channelInstallProofs = channelProofs,
            installLocationProof = globalTool.InstallProof
        }, JsonContractSerializerOptions.Create()));
        artifacts.Add(new(
            "release.identity.json",
            Sha256(identity),
            new FileInfo(identity).Length,
            Type: "release-identity",
            RuntimeIdentifier: rid,
            Channel: DistributionChannelId.GlobalTool,
            Version: version,
            PackageId: globalTool.PackageId));
        return new ReleaseArtifactBuild(
            artifacts,
            VelopackArtifacts: [],
            payloadChecks,
            globalTool.InstallProof,
            releaseProduct,
            globalTool.PackageId,
            DistributionChannelId.GlobalTool,
            channelProofs);
    }

    // 007 T028: pack the target's tool project into a framework-dependent global-tool package, then smoke it
    // source-free (install into a no-source location and exercise the documented command path). One package for all
    // OSes — per-RID tool binaries fetch + hash-verify on demand (T022), not bundled.
    internal static GlobalToolChannelResult BuildGlobalToolChannel(string repo, LocalReleaseTarget target, string tempRoot)
    {
        string publishProject = target.PublishProject.Replace('/', Path.DirectorySeparatorChar);
        // FR-003/C2: pack via the two-phase PackAnchoredTool target (stages the payload, computes its manifest
        // digest, re-emits hx with the digest embedded as the anti-substitution anchor), NOT a plain `dotnet pack`
        // (which the csproj's _GuardPayloadAnchor refuses, since it would ship an unanchored tool).
        Dotnet(repo, $"build {Quote(publishProject)} -c Release -t:PackAnchoredTool -p:PackageOutputPath={Quote(tempRoot)} --nologo");

        // pack writes the package(s) at the tempRoot top level; the tool package (it bundles the payload) is the
        // largest .nupkg. Filter on the real extension so a symbol .snupkg is never selected.
        string nupkg = Directory.EnumerateFiles(tempRoot, "*.nupkg")
            .Where(path => string.Equals(Path.GetExtension(path), ".nupkg", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(path => new FileInfo(path).Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("dotnet pack produced no .nupkg for the global tool.");

        string fileName = Path.GetFileName(nupkg);
        (string packageId, string packageVersion) = ParseNupkgIdentity(fileName);
        var artifact = new LocalReleaseArtifact(
            fileName, Sha256(nupkg), new FileInfo(nupkg).Length,
            Type: "global-tool-package", RuntimeIdentifier: "any",
            Channel: DistributionChannelId.GlobalTool, Version: packageVersion, PackageId: packageId);

        (LocalReleaseInstallLocationProof install, ChannelInstallProof channel) =
            SmokeInstalledGlobalTool(tempRoot, fileName, packageId, packageVersion);
        return new GlobalToolChannelResult(artifact, packageId, packageVersion, install, channel);
    }

    // 007 T028 (FR-023/FR-024): install the packed tool into a no-source location and run the DOCUMENTED source-free
    // command path. Recorded, not release-blocking: an environmental install failure is advisory; a command failure
    // is a recorded fail with blockers. (The fuller `new` + `doti install` smoke is the CI release/store workflow.)
    private static (LocalReleaseInstallLocationProof Install, ChannelInstallProof Channel) SmokeInstalledGlobalTool(
        string tempRoot, string nupkgFileName, string packageId, string packageVersion)
    {
        string toolPath = Path.Combine(tempRoot, "tool-smoke");
        (int installExit, string installOut) = ProcessRunner.Run(
            "dotnet",
            $"tool install {Quote(packageId)} --version {Quote(packageVersion)} --add-source {Quote(tempRoot)} --tool-path {Quote(toolPath)}",
            tempRoot,
            ProcessRunner.NestedDotnetEnv());
        if (installExit != 0)
        {
            // Environmental (offline restore, or a non-tool package): advisory, not a release-breaking failure.
            string[] reason = ["dotnet tool install failed: " + ProcessRunner.Tail(installOut)];
            return (
                new LocalReleaseInstallLocationProof("advisory", nupkgFileName, toolPath, null, null, null, [], reason),
                new ChannelInstallProof(DistributionChannelId.GlobalTool, "advisory", null, [], reason));
        }

        string hx = Path.Combine(toolPath, OperatingSystem.IsWindows() ? "hx.exe" : "hx");
        (string Label, (int ExitCode, string Output) Result)[] runs = new[] { "--help", "version --json", "prereq check --json" }
            .Select(args => ($"hx {args}", ProcessRunner.Run(hx, args, toolPath, ProcessRunner.NestedDotnetEnv())))
            .ToArray();

        string versionOutput = runs.Single(r => r.Label == "hx version --json").Result.Output;
        bool channelReported = versionOutput.Contains(DistributionChannelId.GlobalTool, StringComparison.Ordinal)
            && versionOutput.Contains(CommandMode.Installed, StringComparison.Ordinal);
        List<string> blockers = runs.Where(r => r.Result.ExitCode != 0)
            .Select(r => $"{r.Label} exited {r.Result.ExitCode}: {ProcessRunner.Tail(r.Result.Output)}")
            .ToList();
        if (!channelReported)
        {
            blockers.Add("version --json did not report channel=global-tool / mode=installed");
        }

        string outcome = blockers.Count == 0 ? "pass" : "fail";
        string[] payloadChecks = channelReported
            ? ["version --json resolved the installed payload (channel=global-tool, mode=installed)"]
            : [];
        return (
            new LocalReleaseInstallLocationProof(
                outcome, nupkgFileName, toolPath, hx, "hx version --json",
                PayloadRoot.Sha256OfText(versionOutput), payloadChecks, blockers),
            new ChannelInstallProof(DistributionChannelId.GlobalTool, outcome, hx, runs.Select(r => r.Label).ToArray(), blockers));
    }

    // 007 T028: a NuGet package file is "{PackageId}.{Version}.nupkg"; the version starts at the first dot followed
    // by a digit. (PackageId segments are never numeric-leading, so this split is unambiguous.)
    internal static (string PackageId, string Version) ParseNupkgIdentity(string nupkgFileName)
    {
        string stem = nupkgFileName.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
            ? nupkgFileName[..^".nupkg".Length]
            : nupkgFileName;
        for (int i = 0; i < stem.Length - 1; i++)
        {
            if (stem[i] == '.' && char.IsDigit(stem[i + 1]))
            {
                return (stem[..i], stem[(i + 1)..]);
            }
        }

        return (stem, "");
    }

    // 007 T028: name the channels actually produced by this release for the release identity's releaseProduct.
    internal static string ComposeReleaseProduct(bool globalTool, bool msix) =>
        (globalTool, msix) switch
        {
            (true, true) => "global-tool+msix",
            (true, false) => "global-tool",
            (false, true) => "msix",
            _ => ReleaseProductNone,
        };

    // 007 T028: the Windows MSIX channel is applicable when the repo ships an MSIX manifest, but the curated layout +
    // signed .msix + Store submission are produced by store-release.yml (makeappx needs the Windows SDK; the Store
    // signs it), so the local release records the channel proof as advisory rather than packing the MSIX here.
    internal static ChannelInstallProof? MsixChannelProof(string repo)
    {
        string manifest = Path.Combine(repo, "packaging", "msix", "AppxManifest.xml");
        if (!File.Exists(manifest))
        {
            return null;
        }

        return new ChannelInstallProof(
            DistributionChannelId.Msix,
            "advisory",
            InstalledCommandPath: null,
            ExercisedCommands: [],
            Blockers: ["the curated MSIX layout + signed .msix are produced and submitted by the Store-release CI workflow (store-release.yml)"]);
    }

    // Publish the target's self-contained executable, stage the packaged assets, and fail closed if the staged tree
    // carries the tool's own build tree (FR-006). Returns the filesystem-safe project name + the payload hash set.
    private static (string ProjectName, IReadOnlyList<LocalReleasePayloadCheck> PayloadChecks) StagePublishAndInspect(
        string repo, string tempRoot, LocalReleaseTarget target, string rid)
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

        // 007 T020 (FR-006/SC-004): packaging fails closed if the staged release layout carries the tool's own build
        // tree — recursively, including inside any bundled .nupkg. The legitimate template pack + payload pass.
        ReleaseSourceScanResult sourceScan = ReleaseSourceInspector.Scan(publish);
        if (sourceScan.Outcome != StageOutcome.Pass)
        {
            throw new InvalidOperationException(
                $"{ReleaseSourceInspector.ViolationCode}: release staging carries the tool's build tree (FR-006): " +
                string.Join("; ", sourceScan.Findings.Take(5).Select(f => $"{f.Artifact}!{f.Entry} ({f.Marker})")));
        }

        return (projectName, InspectPayload(publish));
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

    // 007 T041 (FR-044): validate an EXPLICIT intent here (fail fast on a bad value); a blank intent is no longer
    // defaulted to `patch` — it returns null so Run resolves the cycle-type-aware default from the GitVersion-
    // calculated bump (LocalReleaseVersionPolicy.DefaultIntent), keeping the release default in lockstep with the
    // cycle's +semver trailer (minor for a feature cycle, patch for a bug-fix-only cycle).
    private static string? NormalizeExplicitReleaseIntent(string? intent)
    {
        if (string.IsNullOrWhiteSpace(intent))
        {
            return null;
        }

        string normalized = intent.Trim().ToLowerInvariant();
        return normalized is "major" or "minor" or "patch"
            ? normalized
            : throw new InvalidOperationException($"Unknown release intent '{intent}'. Use major, minor, or patch.");
    }

    // Resolve the effective intent: an explicit --major|--minor|--patch wins; a blank intent follows the
    // GitVersion-calculated bump (FR-044/SC-016). Either way the result is validated against the calculated bump.
    private static string ResolveReleaseIntent(string repo, string version, string? explicitIntent)
    {
        string? previous = LatestVersionTag(repo, version);
        string intent = explicitIntent ?? LocalReleaseVersionPolicy.DefaultIntent(previous, version);
        LocalReleaseVersionPolicy.ValidateIntent(previous, version, intent);
        return intent;
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
            DirectoryCopy.Copy(doti, Path.Combine(publish, ".doti"), _ => true, IncludeDotiReleasePayloadFile);
        }

        string legacyDoti = Path.Combine(repo, "doti");
        if (Directory.Exists(legacyDoti))
        {
            DirectoryCopy.Copy(legacyDoti, Path.Combine(publish, "doti"), _ => true);
        }
    }

    private static bool IncludeDotiReleasePayloadFile(string path)
    {
        string file = Path.GetFileName(path);
        return !string.Equals(file, "cycle-state.json", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(file, "gate-proof.json", StringComparison.OrdinalIgnoreCase);
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
