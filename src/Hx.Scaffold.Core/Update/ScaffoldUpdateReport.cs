using Hx.Scaffold.Core.Versioning;
using Hx.Tooling.Contracts;
using Hx.Doti.Core;
using Hx.Doti.Core.ManagedAssets;
using Hx.Runner.Core.Io;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Process;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hx.Scaffold.Core.Update;

public sealed record ScaffoldUpdateRequest(
    string RepositoryRoot,
    bool DryRun,
    bool Force,
    bool NoWorktree,
    string RunningVersion,
    bool JsonOutput = false);

public sealed record ScaffoldUpdateDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path = null,
    string? Category = null);

public sealed record ScaffoldUpdateWorktreeBackup(
    string? Path,
    string? HeadSha,
    string? Ref,
    string? ReversalCommand,
    bool Created,
    bool Disabled,
    string Note);

public sealed record ScaffoldHookReport(
    string Verdict,
    string? HookPath,
    string ExpectedSha256,
    string? CurrentSha256,
    bool CanInstallOrRefresh,
    bool Changed,
    string Action,
    string Message);

public sealed record ScaffoldUpdateDelegation(
    bool Required,
    string? Reason,
    string? ExecutablePath,
    string? ExecutableSha256,
    IReadOnlyList<string> Arguments,
    int? ExitCode,
    string? ChildOutput);

public sealed record ScaffoldUpdateReport(
    int SchemaVersion,
    string TargetRepo,
    bool DryRun,
    bool Force,
    bool NoWorktree,
    string HostRid,
    string ExpectedAssetName,
    ScaffoldVersionReport Version,
    string? LatestVersion,
    string? CacheAction,
    string? ArchivePath,
    string? ExtractedPath,
    string? BackupWorktreePath,
    ScaffoldUpdateWorktreeBackup? BackupWorktree,
    bool Delegated,
    ScaffoldUpdateDelegation? Delegation,
    string TargetToLatestRelation,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<ScaffoldUpdateDiagnostic> Diagnostics,
    IReadOnlyList<string> PlannedActions,
    IReadOnlyList<string> PlannedCreatePaths,
    IReadOnlyList<string> PlannedReplacePaths,
    IReadOnlyList<string> ForceReplacedPaths,
    IReadOnlyList<string> ChangedPaths,
    ScaffoldHookReport? Hook,
    IReadOnlyList<string> PreservedLivePaths,
    IReadOnlyList<string> PossibleOrphanLegacyPaths,
    string? LegacyFollowUpInstruction,
    IReadOnlyList<string> FollowUpCommands);

public sealed record UpdateReleaseAsset(string Name, Uri DownloadUrl);

public sealed record UpdateRelease(
    string TagName,
    string Version,
    UpdateReleaseAsset Archive,
    UpdateReleaseAsset Checksum);

public sealed record UpdateCacheResult(
    UpdateRelease Release,
    string Action,
    string ArchivePath,
    string ExtractedPath,
    string PayloadRoot);
