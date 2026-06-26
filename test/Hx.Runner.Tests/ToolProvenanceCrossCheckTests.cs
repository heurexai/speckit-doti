using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 007 T022 trust hardening: when the manifest declares an upstream-published checksums file, the fetch cross-verifies
/// the manifest's recorded <c>archiveSha256</c> against the publisher's independent checksum for the asset (the upgrade
/// from TOFU — a hash self-computed from the same download). A match fetches; a mismatch or a missing entry fails
/// closed with <see cref="ToolFetchFailureKind.ProvenanceMismatch"/>.
/// </summary>
public sealed class ToolProvenanceCrossCheckTests : IDisposable
{
    private const string Rid = "win-x64";
    private const string AssetName = "gitleaks_1.0.0_windows_x64.zip";
    private const string ChecksumUrl = "https://github.com/gitleaks/gitleaks/releases/download/v1.0.0/checksums.txt";
    private const string DownloadUrl = "https://github.com/gitleaks/gitleaks/releases/download/v1.0.0/" + AssetName;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hx-provenance-" + Guid.NewGuid().ToString("n"));

    public ToolProvenanceCrossCheckTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Matching_upstream_checksum_fetches()
    {
        (byte[] zip, string archiveSha, string exeSha) = Archive();
        string manifest = WriteManifest(archiveSha, exeSha);

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, FetchBoth(zip, $"{archiveSha}  {AssetName}\n"), _root);

        Assert.Equal(ToolFetchStatus.Fetched, outcome.Status);
    }

    [Fact]
    public void Mismatching_upstream_checksum_fails_closed()
    {
        (byte[] zip, string archiveSha, string exeSha) = Archive();
        string manifest = WriteManifest(archiveSha, exeSha);

        // The publisher's checksum disagrees with the manifest's recorded hash → the manifest hash is not trustworthy.
        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, FetchBoth(zip, $"{new string('0', 64)}  {AssetName}\n"), _root);

        Assert.Equal(ToolFetchStatus.Failed, outcome.Status);
        Assert.Equal(ToolFetchFailureKind.ProvenanceMismatch, outcome.FailureKind);
    }

    [Fact]
    public void Upstream_checksum_missing_the_asset_fails_closed()
    {
        (byte[] zip, string archiveSha, string exeSha) = Archive();
        string manifest = WriteManifest(archiveSha, exeSha);

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, FetchBoth(zip, $"{archiveSha}  some-other-file.zip\n"), _root);

        Assert.Equal(ToolFetchStatus.Failed, outcome.Status);
        Assert.Equal(ToolFetchFailureKind.ProvenanceMismatch, outcome.FailureKind);
    }

    private static Func<Uri, byte[]> FetchBoth(byte[] zip, string checksums) => uri =>
        uri.AbsoluteUri == ChecksumUrl ? Encoding.UTF8.GetBytes(checksums) : zip;

    private static (byte[] Zip, string ArchiveSha, string ExeSha) Archive()
    {
        byte[] exe = Encoding.UTF8.GetBytes("fake-gitleaks-binary");
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using Stream entry = zip.CreateEntry("gitleaks.exe").Open();
            entry.Write(exe);
        }

        byte[] zipBytes = buffer.ToArray();
        return (zipBytes, Sha(zipBytes), Sha(exe));
    }

    private string WriteManifest(string archiveSha, string exeSha)
    {
        string path = Path.Combine(_root, "tools", "gitleaks", "gitleaks.version.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var manifest = new
        {
            schemaVersion = 1,
            tool = "gitleaks",
            checksumUrl = ChecksumUrl,
            assets = new[]
            {
                new
                {
                    rid = Rid,
                    assetName = AssetName,
                    downloadUrl = DownloadUrl,
                    archiveSha256 = archiveSha,
                    executablePath = "tools/gitleaks/bin/win-x64/gitleaks.exe",
                    executableSha256 = exeSha,
                    executableName = "gitleaks.exe",
                },
            },
        };
        File.WriteAllText(path, JsonSerializer.Serialize(manifest));
        return path;
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
