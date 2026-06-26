using System.IO.Compression;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using Hx.Runner.Core.Io;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Tools;

public static partial class ToolFetcher
{
    private static byte[] DownloadOrThrow(
        string tool,
        string rid,
        ToolAssetDto asset,
        Func<Uri, byte[]> fetchBytes)
    {
        if (TryDownload(tool, rid, asset, fetchBytes, out byte[]? downloaded, out ToolFetchOutcome? failure))
        {
            throw new ToolFetchFailed(failure!);
        }

        return downloaded!;
    }

    private static byte[] BuildExecutableBytesOrThrow(string tool, string rid, ToolAssetDto asset, byte[] downloaded)
    {
        if (TryBuildExecutableBytes(tool, rid, asset, downloaded, out byte[]? executableBytes, out ToolFetchOutcome? failure))
        {
            throw new ToolFetchFailed(failure!);
        }

        return executableBytes!;
    }

    private static void EnsureExecutableHash(string tool, string rid, ToolAssetDto asset, byte[] executableBytes)
    {
        if (!HashMatches(Sha256OfBytes(executableBytes), asset.ExecutableSha256))
        {
            throw new ToolFetchFailed(Failed(tool, rid, ToolFetchFailureKind.ExecutableHashMismatch, asset.ExecutablePath,
                $"Fetched '{tool}' executable SHA-256 does not match the manifest."));
        }
    }

    private static bool AlreadyVerified(string exeFullPath, string expectedSha) =>
        File.Exists(exeFullPath) && HashMatches(FileHashing.Sha256OfFile(exeFullPath), expectedSha);

    private static bool TryDownload(
        string tool,
        string rid,
        ToolAssetDto asset,
        Func<Uri, byte[]> fetchBytes,
        out byte[]? downloaded,
        out ToolFetchOutcome? failure)
    {
        downloaded = null;
        failure = null;

        // 007 T022 offline split: an invalid / non-https URL is a manifest defect → fail closed (never advisory).
        if (!Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out Uri? url)
            || !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            failure = Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                $"The '{tool}' asset for '{rid}' has an invalid or non-https downloadUrl.");
            return true;
        }

        try
        {
            downloaded = fetchBytes(url);
            return false;
        }
        catch (Exception ex) when (IsNetworkCondition(ex))
        {
            // A genuine network condition (DNS/timeout/unreachable) is Degraded — advisory-able on the core path.
            failure = new ToolFetchOutcome(tool, rid, ToolFetchStatus.Degraded, ToolFetchFailureKind.Network,
                asset.ExecutablePath, $"Network condition fetching '{tool}' ({rid}): {ex.Message}");
            return true;
        }
        catch (Exception ex)
        {
            // Anything else (a structured non-network IO error) fails closed — never a raw exception to the caller.
            failure = Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                $"Download failed for '{tool}' ({rid}): {ex.Message}");
            return true;
        }
    }

    /// <summary>A genuine network condition (DNS failure, timeout, unreachable host) — the only fetch failure the
    /// core path may degrade to advisory (T022). Walks the inner-exception chain so a wrapped socket/HTTP fault counts.</summary>
    private static bool IsNetworkCondition(Exception exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            if (e is HttpRequestException or TaskCanceledException or TimeoutException or SocketException)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildExecutableBytes(
        string tool,
        string rid,
        ToolAssetDto asset,
        byte[] downloaded,
        out byte[]? executableBytes,
        out ToolFetchOutcome? failure)
    {
        executableBytes = downloaded;
        failure = null;
        if (string.IsNullOrWhiteSpace(asset.ArchiveSha256))
        {
            return false;
        }

        if (!HashMatches(Sha256OfBytes(downloaded), asset.ArchiveSha256))
        {
            failure = Failed(tool, rid, ToolFetchFailureKind.ArchiveHashMismatch, asset.ExecutablePath,
                $"Downloaded '{tool}' archive SHA-256 does not match the manifest.");
            return true;
        }

        try
        {
            executableBytes = ExtractExecutable(downloaded, asset.ExecutableName, asset.DownloadUrl);
            return false;
        }
        catch (Exception ex)
        {
            failure = Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                $"Could not extract '{asset.ExecutableName}' from the '{tool}' archive: {ex.Message}");
            return true;
        }
    }

    private static ToolFetchOutcome WriteExecutable(
        string tool,
        string rid,
        ToolAssetDto asset,
        string exeFullPath,
        byte[] executableBytes)
    {
        try
        {
            string dir = Path.GetDirectoryName(exeFullPath)!;
            Directory.CreateDirectory(dir);

            // 007 T022 concurrency-safety: write a unique temp file in the same directory, then atomically replace.
            // Two fetchers running at once never observe a half-written executable; the final move is atomic on-volume.
            string temp = Path.Combine(dir, $".{Path.GetFileName(exeFullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(temp, executableBytes);
                File.Move(temp, exeFullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    try { File.Delete(temp); } catch { /* best-effort temp cleanup */ }
                }
            }
        }
        catch (Exception ex)
        {
            return Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                $"Could not write the '{tool}' executable to '{asset.ExecutablePath}': {ex.Message}");
        }

        return new ToolFetchOutcome(tool, rid, ToolFetchStatus.Fetched, ToolFetchFailureKind.None,
            asset.ExecutablePath, $"'{tool}' fetched and verified for '{rid}'.");
    }

    private static byte[] ExtractFromZip(byte[] archive, string executableName)
    {
        using var stream = new MemoryStream(archive, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        // Match the entry by file name (the executable may sit at the archive root or under a directory).
        ZipArchiveEntry entry = zip.Entries.FirstOrDefault(e =>
                string.Equals(e.Name, executableName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Archive does not contain '{executableName}'.");

        using Stream entryStream = entry.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Extract <paramref name="executableName"/> from a .zip (Windows) or .tar.gz (linux/macOS), chosen by the download URL.</summary>
    private static byte[] ExtractExecutable(byte[] archive, string executableName, string downloadUrl) =>
        downloadUrl.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || downloadUrl.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)
            ? ExtractFromTarGz(archive, executableName)
            : ExtractFromZip(archive, executableName);

    private static byte[] ExtractFromTarGz(byte[] archive, string executableName)
    {
        using var stream = new MemoryStream(archive, writable: false);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var tar = new System.Formats.Tar.TarReader(gzip);

        // Match the entry by file name (the executable may sit at the archive root or under a directory).
        while (tar.GetNextEntry() is { } entry)
        {
            if (entry.DataStream is not null &&
                string.Equals(Path.GetFileName(entry.Name), executableName, StringComparison.OrdinalIgnoreCase))
            {
                using var buffer = new MemoryStream();
                entry.DataStream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }

        throw new InvalidOperationException($"Archive does not contain '{executableName}'.");
    }

    // 007 T022 trust hardening: when the manifest declares an upstream-published checksums file, cross-verify the
    // manifest's recorded archiveSha256 against the publisher's independent checksum for this asset — the upgrade
    // from TOFU (a hash self-computed from the same download) to verification against the publisher's claim. Tools
    // without a checksumUrl rely on the version-drift gate (ToolHashCaptureDriftTests) for provenance.
    private static void VerifyUpstreamProvenanceOrThrow(
        string tool, string rid, ToolManifestDto manifest, ToolAssetDto asset, Func<Uri, byte[]> fetchBytes)
    {
        if (string.IsNullOrWhiteSpace(manifest.ChecksumUrl)
            || string.IsNullOrWhiteSpace(asset.ArchiveSha256)
            || string.IsNullOrWhiteSpace(asset.AssetName))
        {
            return; // no upstream checksum declared for this tool/asset
        }

        if (!Uri.TryCreate(manifest.ChecksumUrl, UriKind.Absolute, out Uri? url)
            || !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolFetchFailed(Failed(tool, rid, ToolFetchFailureKind.ProvenanceMismatch, asset.ExecutablePath,
                $"The '{tool}' manifest checksumUrl is invalid or non-https."));
        }

        byte[] checksumsBytes;
        try
        {
            checksumsBytes = fetchBytes(url);
        }
        catch (Exception ex) when (IsNetworkCondition(ex))
        {
            // A network condition fetching the checksums degrades like the binary (advisory-able on the core path).
            throw new ToolFetchFailed(new ToolFetchOutcome(tool, rid, ToolFetchStatus.Degraded, ToolFetchFailureKind.Network,
                asset.ExecutablePath, $"Network condition fetching '{tool}' upstream checksums ({rid}): {ex.Message}"));
        }
        catch (Exception ex)
        {
            throw new ToolFetchFailed(Failed(tool, rid, ToolFetchFailureKind.ProvenanceMismatch, asset.ExecutablePath,
                $"Could not read '{tool}' upstream checksums: {ex.Message}"));
        }

        string? upstream = ParseUpstreamHash(System.Text.Encoding.UTF8.GetString(checksumsBytes), asset.AssetName!);
        if (upstream is null)
        {
            throw new ToolFetchFailed(Failed(tool, rid, ToolFetchFailureKind.ProvenanceMismatch, asset.ExecutablePath,
                $"The '{tool}' upstream checksums do not list '{asset.AssetName}'."));
        }

        if (!HashMatches(upstream, asset.ArchiveSha256!))
        {
            throw new ToolFetchFailed(Failed(tool, rid, ToolFetchFailureKind.ProvenanceMismatch, asset.ExecutablePath,
                $"The '{tool}' manifest archiveSha256 for '{rid}' does not match the upstream-published checksum (provenance mismatch)."));
        }
    }

    // sha256sum-format checksums: "<hex>  <filename>" (binary mode prefixes the name with '*').
    private static string? ParseUpstreamHash(string checksums, string assetName)
    {
        foreach (string raw in checksums.Split('\n'))
        {
            string line = raw.Trim();
            int sep = line.IndexOf(' ');
            if (sep <= 0)
            {
                continue;
            }

            string name = line[(sep + 1)..].Trim().TrimStart('*', ' ');
            if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
            {
                return line[..sep].Trim();
            }
        }

        return null;
    }

    private static string Sha256OfBytes(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool HashMatches(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
