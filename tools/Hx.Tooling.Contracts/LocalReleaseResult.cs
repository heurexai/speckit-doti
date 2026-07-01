namespace Hx.Tooling.Contracts;

public sealed record LocalReleaseResult(
    int SchemaVersion,
    string ProjectName,
    string Version,
    string ReleaseIntent,
    LocalReleaseTag Tag,
    string GitVersionSource,
    string VelopackPackageId,
    string VelopackChannel,
    string RuntimeIdentifier,
    string SourceCommit,
    LocalReleaseTarget Target,
    LocalReleaseRootDecision RootDecision,
    LocalReleaseEnvironmentPersistence EnvironmentPersistence,
    bool LocalCopyProduced,
    string? SkippedReason,
    string? VersionDirectory,
    string? LatestDirectory,
    IReadOnlyList<LocalReleaseArtifact> Artifacts,
    IReadOnlyList<LocalReleaseArtifact> VelopackArtifacts,
    IReadOnlyList<LocalReleasePayloadCheck> PayloadChecks,
    CycleReleaseTrain? ReleaseTrain,
    ReleaseDocumentationProof? DocumentationProof,
    string CommandName,
    string CommandVersion,
    string ConfigurationSource,
    string ConfigurationPath,
    string ReleaseProduct,
    bool SourceArchiveExcluded,
    IReadOnlyList<string> Blockers,
    LocalReleaseInstallLocationProof? InstallLocationProof = null,
    // 007 T004 (additive, channel-neutral; the Velopack* fields above are kept/mapped so existing
    // release.identity.json readers do not break — no JsonContractDefaults.SchemaVersion bump). Populated when
    // LocalReleaseService is retargeted off vpk (T028).
    string? PackageId = null,
    string? Channel = null,
    IReadOnlyList<ChannelInstallProof>? ChannelInstallProofs = null,
    // 039 WI2/FR-030: on a REVERTED release, what the engine rolled back (tag/dir/cycle-state) — null on success.
    RollbackReport? Rollback = null);

public sealed record LocalReleaseRootDecision(
    string EffectiveEnvironmentVariableName,
    string? RequestedEnvironmentVariableName,
    bool EnvironmentVariableRead,
    bool EnvironmentVariableIgnored,
    string Source,
    string? ReleaseRoot,
    string? Reason);

public sealed record LocalReleaseEnvironmentPersistence(
    bool Requested,
    string? VariableName,
    string? Value,
    bool Written,
    string? Scope,
    string? Limitation);

public sealed record LocalReleaseArtifact(
    string Name,
    string Sha256,
    long SizeBytes,
    string Type = "file",
    string? RuntimeIdentifier = null,
    string? Channel = null,
    string? Version = null,
    string? PackageId = null);

public sealed record LocalReleasePayloadCheck(
    string Path,
    string Sha256,
    long SizeBytes);

public sealed record LocalReleaseInstallLocationProof(
    string Outcome,
    string? InstallerArtifact,
    string? RequestedInstallDirectory,
    string? InstalledExecutablePath,
    string? VersionCommand,
    string? VersionOutputSha256,
    IReadOnlyList<string> PayloadChecks,
    IReadOnlyList<string> Blockers);

public sealed record LocalReleaseTag(
    string Name,
    string Commit,
    string? Object,
    bool Created,
    bool Existing,
    string Message,
    string PushCommand);

public sealed record LocalReleaseTarget(
    string ProductName,
    string PackageName,
    string PublishProject,
    string PublishedExecutableName,
    string ExecutableName,
    string DefaultReleaseRootEnvironmentVariable);
