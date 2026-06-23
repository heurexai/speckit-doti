using Hx.Runner.Core.Io;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Hx.Scaffold.Core.Update;

public static partial class ScaffoldUpdateService
{
    private static UpdateCacheResult ResolveAndCacheRelease(string rid, ScaffoldUpdateServices services)
    {
        UpdateRelease release = services.ResolveLatest(rid);
        string cacheRoot = Path.GetFullPath(services.CacheRoot());
        string versionRoot = Path.Combine(cacheRoot, release.Version, rid);
        string archivePath = Path.Combine(versionRoot, release.Archive.Name);
        string checksumPath = archivePath + ".sha256";
        string extractPath = Path.Combine(versionRoot, "extract");

        if (File.Exists(archivePath) && File.Exists(checksumPath))
        {
            string expected = ParseChecksum(File.ReadAllText(checksumPath));
            VerifyArchiveHash(archivePath, expected);
            EnsureExtracted(archivePath, extractPath);
            return new UpdateCacheResult(release, "reuse-verified-cache", archivePath, extractPath, FindPayloadRoot(extractPath));
        }

        Directory.CreateDirectory(versionRoot);
        byte[] checksumBytes = services.DownloadBytes(release.Checksum.DownloadUrl);
        string checksumText = Encoding.UTF8.GetString(checksumBytes);
        string checksum = ParseChecksum(checksumText);
        byte[] archiveBytes = services.DownloadBytes(release.Archive.DownloadUrl);
        string actual = Convert.ToHexString(SHA256.HashData(archiveBytes)).ToLowerInvariant();
        if (!string.Equals(actual, checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Release archive checksum mismatch for {release.Archive.Name}.");
        }

        string tempArchive = archivePath + ".tmp";
        File.WriteAllBytes(tempArchive, archiveBytes);
        File.Move(tempArchive, archivePath, overwrite: true);
        File.WriteAllText(checksumPath, checksumText);
        EnsureExtracted(archivePath, extractPath);
        PruneOlderCacheVersions(cacheRoot, release.Version);
        return new UpdateCacheResult(release, "downloaded-and-verified", archivePath, extractPath, FindPayloadRoot(extractPath));
    }

    private static void EnsureExtracted(string archivePath, string extractPath)
    {
        if (Directory.Exists(extractPath) && Directory.EnumerateFileSystemEntries(extractPath).Any())
        {
            return;
        }

        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, recursive: true);
        }

        Directory.CreateDirectory(extractPath);
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, extractPath, overwriteFiles: true);
            return;
        }

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            using FileStream file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, extractPath, overwriteFiles: true);
            return;
        }

        throw new InvalidOperationException("Unsupported update archive format: " + archivePath);
    }

    private static string FindPayloadRoot(string extractPath)
    {
        string[] dirs = Directory.GetDirectories(extractPath);
        if (dirs.Length == 1 && Directory.Exists(Path.Combine(dirs[0], "doti")))
        {
            return dirs[0];
        }

        return extractPath;
    }

    private static string ParseChecksum(string checksumText)
    {
        string token = checksumText.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "";
        if (token.Length != 64 || !token.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("Release checksum is missing or malformed.");
        }

        return token.ToLowerInvariant();
    }

    private static void VerifyArchiveHash(string archivePath, string expectedSha256)
    {
        string actual = FileHashing.Sha256OfFile(archivePath);
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cached release archive hash mismatch: {archivePath}");
        }
    }

    private static void PruneOlderCacheVersions(string cacheRoot, string keepVersion)
    {
        if (!Directory.Exists(cacheRoot))
        {
            return;
        }

        foreach (string dir in Directory.GetDirectories(cacheRoot))
        {
            if (!string.Equals(Path.GetFileName(dir), keepVersion, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
