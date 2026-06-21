namespace Hx.Tooling.Contracts;

/// <summary>The status of fetching a single vendored tool.</summary>
public enum ToolFetchStatus
{
    /// <summary>The asset was downloaded, verified, and installed (or was already present + verified).</summary>
    Fetched,

    /// <summary>No asset is mapped for the host RID — reported cleanly, not a failure to install (fail-closed for that RID).</summary>
    Skipped,

    /// <summary>The fetch failed closed: a hash mismatch, a download/extraction error, or no asset for the RID when fetching was required.</summary>
    Failed,
}

/// <summary>
/// Why a tool fetch failed, decoupled from the CLI's error-code constants (Contracts cannot reference the
/// kernel). The CLI maps each kind onto the registered <c>ErrorCodes</c> constant when building diagnostics.
/// </summary>
public enum ToolFetchFailureKind
{
    /// <summary>Not a failure.</summary>
    None,

    /// <summary>No manifest asset is mapped for the host RID.</summary>
    AssetUnavailable,

    /// <summary>The downloaded archive's SHA-256 did not match the manifest's <c>archiveSha256</c>.</summary>
    ArchiveHashMismatch,

    /// <summary>The resolved executable's SHA-256 did not match the manifest's <c>executableSha256</c>.</summary>
    ExecutableHashMismatch,

    /// <summary>The download or archive extraction failed (network / IO / corrupt archive).</summary>
    DownloadFailed,
}

/// <summary>The outcome of fetching one tool's executable for the host RID.</summary>
public sealed record ToolFetchOutcome(
    string Tool,
    string Rid,
    ToolFetchStatus Status,
    ToolFetchFailureKind FailureKind,
    string? ExecutablePath,
    string Reason);

/// <summary>
/// JSON proof for <c>tools fetch</c>: the per-tool outcomes plus an overall <see cref="StageOutcome"/>
/// (Pass when every requested tool is present + verified; Fail when any failed closed). A skipped tool
/// (no asset for the RID) does not by itself fail the overall run — the host model reports unsupported RIDs.
/// </summary>
public sealed record ToolFetchResult(
    int SchemaVersion,
    StageOutcome Outcome,
    string Rid,
    IReadOnlyList<ToolFetchOutcome> Tools);
