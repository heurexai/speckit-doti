using System.Net.Http;
using System.Text;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed partial class ToolFetcherTests
{
    [Fact]
    public void UnknownRidIsSkippedNotThrown()
    {
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-binary");
        byte[] zipBytes = ZipContaining("gitversion.exe", exeBytes);
        string manifest = WriteManifest("gitversion", "tools/gitversion/gitversion.version.json",
            downloadUrl: "https://github.com/heurexai/speckit-doti/releases/download/v1.0.0/gitversion.zip",
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
            downloadUrl: "https://github.com/heurexai/speckit-doti/releases/download/v1.0.0/gitversion.zip",
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
            downloadUrl: "https://github.com/heurexai/speckit-doti/releases/download/v1.0.0/sentrux.exe",
            archiveSha256: null,
            executablePath: "tools/sentrux/bin/win-x64/sentrux.exe",
            executableSha256: Sha256(exeBytes),
            executableName: "sentrux.exe");

        ToolFetchOutcome outcome = ToolFetcher.Fetch(manifest, Rid, _ => throw new HttpRequestException("offline"), _root);

        Assert.Equal(ToolFetchStatus.Failed, outcome.Status);
        Assert.Equal(ToolFetchFailureKind.DownloadFailed, outcome.FailureKind);
    }
}
