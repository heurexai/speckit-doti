using System.Text;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed partial class ToolFetcherTests
{
    [Fact]
    public void MatchingTarGzAssetInstallsExecutable()
    {
        // linux/macOS assets ship as .tar.gz (not .zip); the extractor is chosen by the download URL.
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-gitleaks-linux-binary");
        byte[] tgzBytes = TarGzContaining("gitleaks", exeBytes);
        string manifest = WriteManifest("gitleaks", "tools/gitleaks/gitleaks.version.json",
            downloadUrl: "https://github.com/heurexai/speckit-doti/releases/download/v1.0.0/gitleaks_8.30.1_linux_x64.tar.gz",
            archiveSha256: Sha256(tgzBytes),
            executablePath: "tools/gitleaks/bin/linux-x64/gitleaks",
            executableSha256: Sha256(exeBytes),
            executableName: "gitleaks");

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, _ => tgzBytes, _root);

        Assert.Equal(ToolFetchStatus.Fetched, outcome.Status);
        string installed = Path.Combine(_root, "tools", "gitleaks", "bin", "linux-x64", "gitleaks");
        Assert.True(File.Exists(installed));
        Assert.Equal(exeBytes, File.ReadAllBytes(installed));
    }

    private static byte[] TarGzContaining(string entryName, byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(buffer, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        using (var tar = new System.Formats.Tar.TarWriter(gzip, leaveOpen: true))
        {
            tar.WriteEntry(new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, entryName)
            {
                DataStream = new MemoryStream(content),
            });
        }

        return buffer.ToArray();
    }
}
