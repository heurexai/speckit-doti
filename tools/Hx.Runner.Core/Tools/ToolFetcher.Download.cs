using System.IO.Compression;
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
        try
        {
            if (!Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out Uri? url))
            {
                failure = Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                    $"The '{tool}' asset for '{rid}' has an invalid downloadUrl.");
                return true;
            }

            downloaded = fetchBytes(url);
            return false;
        }
        catch (Exception ex)
        {
            failure = Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                $"Download failed for '{tool}' ({rid}): {ex.Message}");
            return true;
        }
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
            Directory.CreateDirectory(Path.GetDirectoryName(exeFullPath)!);
            File.WriteAllBytes(exeFullPath, executableBytes);
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

    private static string Sha256OfBytes(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool HashMatches(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
