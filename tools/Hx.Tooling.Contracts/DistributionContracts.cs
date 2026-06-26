namespace Hx.Tooling.Contracts;

/// <summary>The distribution channel an installed <c>hx</c> came through (FR-009/FR-010/FR-013).</summary>
public static class DistributionChannelId
{
    public const string GlobalTool = "global-tool"; // public NuGet.org .NET global tool (Windows/Linux/macOS)
    public const string Msix = "msix";              // Microsoft Store MSIX (Windows)
    public const string Source = "source";          // a speckit-doti source checkout (developer)
    public const string Unknown = "unknown";
}

/// <summary>Whether a command runs source-free from installed payload, or requires a source checkout
/// (FR-004/FR-022). Source/developer commands are excluded from the normal installed surface.</summary>
public static class CommandMode
{
    public const string Installed = "installed";              // resolves payload beside the exe; no source needed
    public const string SourceDeveloper = "source-developer"; // may reference solution/project files
}

/// <summary>The active distribution channel + how it is updated, reported by <c>version</c>/<c>describe</c>
/// (FR-013/FR-022/FR-042): the channel id, the running tool's command mode, and the per-channel install/update
/// commands and update authority (e.g. <c>dotnet tool update</c> vs the Microsoft Store).</summary>
public sealed record DistributionChannelInfo(
    string Channel,
    string Mode,
    string? InstallCommand = null,
    string? UpdateCommand = null,
    string? UpdateAuthority = null);

/// <summary>
/// The non-source payload descriptor (<c>payload.manifest.json</c>) shipped beside the installed <c>hx</c>
/// (FR-003). Carries its OWN <see cref="SchemaVersion"/> (forked from <see cref="JsonContractDefaults"/> so an
/// unrelated contract bump does not churn the descriptor), the payload/tool versions, the channel + command
/// mode, and a per-payload-file hash set — the concrete trust root that <c>PayloadRoot.Resolve()</c> verifies
/// against the executable-embedded expected digest on every resolution path (including <c>HX_PAYLOAD_ROOT</c>).
/// DISTINCT from <see cref="RepoPayloadStamp"/> (a different file, in the target repo).
/// </summary>
public sealed record PayloadDescriptor(
    int SchemaVersion,
    string PayloadVersion,
    string ToolVersion,
    string Channel,
    string Mode,
    IReadOnlyList<PayloadFileHash> FileHashes)
{
    /// <summary>The descriptor's own schema version, independent of <see cref="JsonContractDefaults.SchemaVersion"/>.</summary>
    public const int CurrentSchemaVersion = 1;
}

/// <summary>One payload file's content hash, the per-file trust-root entry of a <see cref="PayloadDescriptor"/>.</summary>
public sealed record PayloadFileHash(string RelativePath, string Sha256);

/// <summary>
/// The per-repo payload record (<c>.doti/payload.json</c>) written into a target repo on
/// <c>hx doti install --repo</c> (FR-014/FR-015). Its <see cref="PayloadVersion"/> is copied verbatim from the
/// installing <see cref="PayloadDescriptor"/>; FR-015 reconciliation compares the bundled descriptor's payload
/// version against this recorded one. DISTINCT from <see cref="PayloadDescriptor"/>; carries its OWN schema
/// version so the two descriptors version independently.
/// </summary>
public sealed record RepoPayloadStamp(
    int SchemaVersion,
    string PayloadVersion,
    string ToolVersion)
{
    public const int CurrentSchemaVersion = 1;
}

/// <summary>
/// A per-channel source-free install proof recorded by release proof (FR-023/FR-024): the exact documented
/// operator command path was installed into a no-source location and exercised, returning the <c>CliResult</c>
/// envelope and correct exit codes. <see cref="Outcome"/> is <c>pass</c>/<c>fail</c>/<c>advisory</c> (advisory
/// where the channel's packaging tooling is unavailable in the current environment, e.g. MSIX in CI only).
/// </summary>
public sealed record ChannelInstallProof(
    string Channel,
    string Outcome,
    string? InstalledCommandPath,
    IReadOnlyList<string> ExercisedCommands,
    IReadOnlyList<string> Blockers);
