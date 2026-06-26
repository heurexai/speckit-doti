namespace Hx.Tooling.Contracts;

/// <summary>The status of fetching a single vendored tool.</summary>
public enum ToolFetchStatus
{
    /// <summary>The asset was downloaded, verified, and installed (or was already present + verified).</summary>
    Fetched,

    /// <summary>No asset is mapped for the host RID — reported cleanly, not a failure to install (fail-closed for that RID).</summary>
    Skipped,

    /// <summary>The fetch failed closed: a hash/provenance mismatch, a malformed manifest, an invalid URL, or an
    /// extraction/write error. Never degraded to advisory (007 T022 / FR-033).</summary>
    Failed,

    /// <summary>A genuine network condition (DNS failure, timeout, unreachable host) prevented the download. The
    /// core path (hx new first smoke) MAY degrade this to advisory; every other failure stays fail-closed (T022).</summary>
    Degraded,
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

    /// <summary>The archive extraction or executable write failed (corrupt archive / IO). Fail-closed.</summary>
    DownloadFailed,

    /// <summary>A genuine network condition (DNS/timeout/unreachable). Pairs with <see cref="ToolFetchStatus.Degraded"/>;
    /// the only condition the core path may treat as advisory (T022 offline split / FR-033).</summary>
    Network,

    /// <summary>The manifest hash did not match the upstream-published checksum it claims as provenance (007 T022
    /// trust hardening): the manifest's recorded hash is not what the publisher's independent checksum says.</summary>
    ProvenanceMismatch,
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
