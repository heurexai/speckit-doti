using System.Security.Cryptography;
using System.Text;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Tools;
using Hx.Scaffold.Core;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// Verifies <see cref="StoreProvisioner"/> installs a vendored binary into the shared store (no network):
/// a fixture source root with a gitleaks manifest + binary is populated into an isolated <c>HX_TOOL_STORE</c>,
/// and the store ends up with the verified entry.
/// </summary>
public sealed class StoreProvisionerTests : IDisposable
{
    private readonly string _source = Path.Combine(Path.GetTempPath(), "hx-storeprov-src-" + Guid.NewGuid().ToString("n"));
    private readonly string _store = Path.Combine(Path.GetTempPath(), "hx-storeprov-store-" + Guid.NewGuid().ToString("n"));
    private readonly string? _previous = Environment.GetEnvironmentVariable(ToolStore.OverrideEnvVar);

    public StoreProvisionerTests() => Environment.SetEnvironmentVariable(ToolStore.OverrideEnvVar, _store);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ToolStore.OverrideEnvVar, _previous);
        foreach (string dir in new[] { _source, _store })
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void PopulatesStoreFromAVendoredBinary()
    {
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        byte[] exeBytes = Encoding.UTF8.GetBytes("fake-gitleaks-binary");
        string sha = Convert.ToHexStringLower(SHA256.HashData(exeBytes));
        string exeRel = $"tools/gitleaks/bin/{rid}/gitleaks.exe";

        string exeFull = Path.Combine(_source, exeRel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(exeFull)!);
        File.WriteAllBytes(exeFull, exeBytes);

        string manifestFull = Path.Combine(_source, "tools", "gitleaks", "gitleaks.version.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestFull)!);
        File.WriteAllText(manifestFull, $$"""
        {
          "schemaVersion": 1,
          "tool": "gitleaks",
          "version": "8.30.1",
          "assets": [
            { "rid": "{{rid}}", "executablePath": "{{exeRel}}", "executableSha256": "{{sha}}", "executableName": "gitleaks.exe" }
          ]
        }
        """);

        StoreProvisioner.PopulateFromVendoredTools(_source);

        Assert.True(ToolStore.IsInstalled("gitleaks", "8.30.1", rid, "gitleaks.exe", sha));
        Assert.NotNull(ToolStoreResolver.Resolve("gitleaks", "8.30.1", rid, "gitleaks.exe", sha));
    }
}
