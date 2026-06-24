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
    IReadOnlyList<string> Blockers);

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

public sealed record LocalReleaseArtifact(string Name, string Sha256, long SizeBytes);

public sealed record LocalReleasePayloadCheck(
    string Path,
    string Sha256,
    long SizeBytes);

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
