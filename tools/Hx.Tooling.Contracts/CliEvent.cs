namespace Hx.Tooling.Contracts;

/// <summary>
/// One NDJSON streaming event: a long-running command emits a line per phase as it happens, then the final
/// <see cref="CliResult"/> envelope as the last line. Compact + LF-terminated + flushed so a consuming agent sees
/// progress live rather than waiting for the whole run. <see cref="Status"/> mirrors the phase outcome
/// (<c>running|pass|fail|skipped|blocked</c>).
/// </summary>
public sealed record CliEvent(
    string Event,
    string Name,
    string? Status = null,
    string? Message = null,
    long? ElapsedMs = null);
