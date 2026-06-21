using System.Security.Cryptography;
using Hx.Runner.Core.Io;

namespace Hx.Runner.Core.Tools;

public enum StorePopulateStatus
{
    Installed,
    AlreadyPresent,
    Failed,
}

public sealed record StorePopulateResult(
    StorePopulateStatus Status,
    string Tool,
    string Version,
    string Rid,
    string? Path,
    string Reason);

/// <summary>
/// Installs a verified tool executable into the shared <see cref="ToolStore"/>. The bytes are SHA-256-verified
/// against the manifest BEFORE anything is written, the written file is re-verified before being published
/// into place, and any mismatch fails closed (no unverified binary is ever retained). Idempotent: an
/// already-present, hash-matching entry is reported <see cref="StorePopulateStatus.AlreadyPresent"/> without
/// rewriting. Bytes come from the installer's embedded payload (offline) or from <see cref="ToolFetcher"/>
/// (online fallback); this type does the verify+write+record, not the acquisition.
/// </summary>
public static class StorePopulator
{
    public static StorePopulateResult InstallBytes(
        string tool, string version, string rid, string executableName, byte[] bytes, string expectedSha256, string source = "populated")
    {
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(expectedSha256) || string.IsNullOrWhiteSpace(executableName))
        {
            return Fail(tool, version, rid, "Missing version, executable name, or expected SHA-256 — refusing to populate the store.");
        }

        if (ToolStore.IsInstalled(tool, version, rid, executableName, expectedSha256))
        {
            return new StorePopulateResult(StorePopulateStatus.AlreadyPresent, tool, version, rid,
                ToolStore.PathFor(tool, version, rid, executableName), $"'{tool}' {version} ({rid}) already present and verified.");
        }

        string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(tool, version, rid, $"Refusing to install '{tool}' {version} ({rid}): bytes SHA-256 does not match the manifest.");
        }

        string target = ToolStore.PathFor(tool, version, rid, executableName);
        string tmp = target + ".tmp-" + Guid.NewGuid().ToString("n");
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.WriteAllBytes(tmp, bytes);

            // Defense in depth: re-verify the written file before publishing it into place.
            if (!string.Equals(FileHashing.Sha256OfFile(tmp), expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(tmp);
                return Fail(tool, version, rid, $"Written '{tool}' {version} ({rid}) failed re-verification; not published.");
            }

            File.Move(tmp, target, overwrite: true);
            ToolStore.RecordEntry(new ToolStoreEntry(tool, version, rid, executableName, expectedSha256, source));
            return new StorePopulateResult(StorePopulateStatus.Installed, tool, version, rid, target,
                $"'{tool}' {version} ({rid}) installed into the shared store.");
        }
        catch (Exception ex)
        {
            TryDelete(tmp);
            return Fail(tool, version, rid, $"Failed to populate the store for '{tool}' {version} ({rid}): {ex.Message}");
        }
    }

    private static StorePopulateResult Fail(string tool, string version, string rid, string reason) =>
        new(StorePopulateStatus.Failed, tool, version, rid, null, reason);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup of the temp file
        }
    }
}
