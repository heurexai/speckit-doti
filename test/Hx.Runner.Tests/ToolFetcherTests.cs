using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// Fixture-based verify/extract tests for <see cref="ToolFetcher"/>. The byte download is injected, so these
/// run with NO network: the fixture builds the archive/exe bytes in-memory, computes their SHA-256, and writes
/// a manifest whose hashes match — then asserts the fail-closed behavior on a deliberate mismatch.
/// </summary>
public sealed class ToolFetcherTests : IDisposable
{
    private const string Rid = "win-x64";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hx-toolfetch-" + Guid.NewGuid().ToString("n"));

    public ToolFetcherTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void MatchingZipAssetInstallsExecutable()
    {
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-gitversion-binary");
        byte[] zipBytes = ZipContaining("gitversion.exe", exeBytes);
        string manifest = WriteManifest("gitversion", "tools/gitversion/gitversion.version.json",
            downloadUrl: "https://example.test/gitversion.zip",
            archiveSha256: Sha256(zipBytes),
            executablePath: "tools/gitversion/bin/win-x64/gitversion.exe",
            executableSha256: Sha256(exeBytes),
            executableName: "gitversion.exe");

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, _ => zipBytes, _root);

        Assert.Equal(ToolFetchStatus.Fetched, outcome.Status);
        Assert.Equal(ToolFetchFailureKind.None, outcome.FailureKind);
        string installed = Path.Combine(_root, "tools", "gitversion", "bin", "win-x64", "gitversion.exe");
        Assert.True(File.Exists(installed));
        Assert.Equal(exeBytes, File.ReadAllBytes(installed));
    }

    [Fact]
    public void RawExecutableAssetInstallsDownloadedBytes()
    {
        // Sentrux-style: archiveSha256 null → the downloaded bytes ARE the executable.
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-sentrux-binary");
        string manifest = WriteManifest("sentrux", "tools/sentrux/sentrux.version.json",
            downloadUrl: "https://example.test/sentrux.exe",
            archiveSha256: null,
            executablePath: "tools/sentrux/bin/win-x64/sentrux.exe",
            executableSha256: Sha256(exeBytes),
            executableName: "sentrux.exe");

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, _ => exeBytes, _root);

        Assert.Equal(ToolFetchStatus.Fetched, outcome.Status);
        string installed = Path.Combine(_root, "tools", "sentrux", "bin", "win-x64", "sentrux.exe");
        Assert.Equal(exeBytes, File.ReadAllBytes(installed));
    }

    [Fact]
    public void ArchiveHashMismatchFailsClosed()
    {
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-binary");
        byte[] zipBytes = ZipContaining("gitversion.exe", exeBytes);
        string manifest = WriteManifest("gitversion", "tools/gitversion/gitversion.version.json",
            downloadUrl: "https://example.test/gitversion.zip",
            archiveSha256: Sha256(Encoding.UTF8.GetBytes("a-different-archive")), // deliberately wrong
            executablePath: "tools/gitversion/bin/win-x64/gitversion.exe",
            executableSha256: Sha256(exeBytes),
            executableName: "gitversion.exe");

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, _ => zipBytes, _root);

        Assert.Equal(ToolFetchStatus.Failed, outcome.Status);
        Assert.Equal(ToolFetchFailureKind.ArchiveHashMismatch, outcome.FailureKind);
        Assert.False(File.Exists(Path.Combine(_root, "tools", "gitversion", "bin", "win-x64", "gitversion.exe")));
    }

    [Fact]
    public void ExecutableHashMismatchFailsClosed()
    {
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-binary");
        byte[] zipBytes = ZipContaining("gitversion.exe", exeBytes);
        string manifest = WriteManifest("gitversion", "tools/gitversion/gitversion.version.json",
            downloadUrl: "https://example.test/gitversion.zip",
            archiveSha256: Sha256(zipBytes),
            executablePath: "tools/gitversion/bin/win-x64/gitversion.exe",
            executableSha256: Sha256(Encoding.UTF8.GetBytes("a-different-exe")), // deliberately wrong
            executableName: "gitversion.exe");

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, _ => zipBytes, _root);

        Assert.Equal(ToolFetchStatus.Failed, outcome.Status);
        Assert.Equal(ToolFetchFailureKind.ExecutableHashMismatch, outcome.FailureKind);
        Assert.False(File.Exists(Path.Combine(_root, "tools", "gitversion", "bin", "win-x64", "gitversion.exe")));
    }

    [Fact]
    public void UnknownRidIsSkippedNotThrown()
    {
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-binary");
        byte[] zipBytes = ZipContaining("gitversion.exe", exeBytes);
        string manifest = WriteManifest("gitversion", "tools/gitversion/gitversion.version.json",
            downloadUrl: "https://example.test/gitversion.zip",
            archiveSha256: Sha256(zipBytes),
            executablePath: "tools/gitversion/bin/win-x64/gitversion.exe",
            executableSha256: Sha256(exeBytes),
            executableName: "gitversion.exe");

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, "linux-arm64", _ => throw new InvalidOperationException("must not download"), _root);

        Assert.Equal(ToolFetchStatus.Skipped, outcome.Status);
        Assert.Equal(ToolFetchFailureKind.AssetUnavailable, outcome.FailureKind);
    }

    [Fact]
    public void AlreadyPresentVerifiedExecutableIsNotReDownloaded()
    {
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-binary");
        string exePath = Path.Combine(_root, "tools", "gitversion", "bin", "win-x64", "gitversion.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
        File.WriteAllBytes(exePath, exeBytes);

        string manifest = WriteManifest("gitversion", "tools/gitversion/gitversion.version.json",
            downloadUrl: "https://example.test/gitversion.zip",
            archiveSha256: Sha256(Encoding.UTF8.GetBytes("ignored")),
            executablePath: "tools/gitversion/bin/win-x64/gitversion.exe",
            executableSha256: Sha256(exeBytes),
            executableName: "gitversion.exe");

        // The byte source throws: a present + verified exe must short-circuit the download.
        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, _ => throw new InvalidOperationException("must not download"), _root);

        Assert.Equal(ToolFetchStatus.Fetched, outcome.Status);
    }

    [Fact]
    public void DownloadFailureFailsClosedWithoutThrowing()
    {
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-binary");
        string manifest = WriteManifest("sentrux", "tools/sentrux/sentrux.version.json",
            downloadUrl: "https://example.test/sentrux.exe",
            archiveSha256: null,
            executablePath: "tools/sentrux/bin/win-x64/sentrux.exe",
            executableSha256: Sha256(exeBytes),
            executableName: "sentrux.exe");

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, _ => throw new HttpRequestException("offline"), _root);

        Assert.Equal(ToolFetchStatus.Failed, outcome.Status);
        Assert.Equal(ToolFetchFailureKind.DownloadFailed, outcome.FailureKind);
    }

    private string WriteManifest(
        string tool, string relativePath, string downloadUrl, string? archiveSha256,
        string executablePath, string executableSha256, string executableName)
    {
        string archiveField = archiveSha256 is null ? "null" : $"\"{archiveSha256}\"";
        string json = $$"""
        {
          "schemaVersion": 1,
          "tool": "{{tool}}",
          "license": "MIT",
          "assets": [
            {
              "rid": "win-x64",
              "downloadUrl": "{{downloadUrl}}",
              "archiveSha256": {{archiveField}},
              "executablePath": "{{executablePath}}",
              "executableSha256": "{{executableSha256}}",
              "executableName": "{{executableName}}"
            }
          ]
        }
        """;

        string full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, json);
        return full;
    }

    private static byte[] ZipContaining(string entryName, byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry(entryName);
            using Stream stream = entry.Open();
            stream.Write(content);
        }

        return buffer.ToArray();
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
