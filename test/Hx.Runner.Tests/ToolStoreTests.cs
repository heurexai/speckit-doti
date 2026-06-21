using System.Security.Cryptography;
using System.Text;
using Hx.Runner.Core.Tools;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// Fixture-based tests for the shared tool store (no network). Each test instance points the store at its
/// own temp dir via <c>HX_TOOL_STORE</c> (xUnit creates a fresh instance per test; methods in one class run
/// sequentially, so the process-global env var is not contended). Asserts verified install, idempotency,
/// fail-closed on hash mismatch, store-first resolution, and the override.
/// </summary>
public sealed class ToolStoreTests : IDisposable
{
    private const string Tool = "gitversion";
    private const string Version = "6.7.0";
    private const string Rid = "win-x64";
    private const string ExeName = "gitversion.exe";

    private readonly string _store = Path.Combine(Path.GetTempPath(), "hx-toolstore-" + Guid.NewGuid().ToString("n"));
    private readonly string? _previous = Environment.GetEnvironmentVariable(ToolStore.OverrideEnvVar);

    public ToolStoreTests() => Environment.SetEnvironmentVariable(ToolStore.OverrideEnvVar, _store);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ToolStore.OverrideEnvVar, _previous);
        if (Directory.Exists(_store))
        {
            Directory.Delete(_store, recursive: true);
        }
    }

    [Fact]
    public void OverrideEnvVarSetsTheRoot()
    {
        Assert.Equal(Path.GetFullPath(_store), ToolStore.Root());
    }

    [Fact]
    public void MatchingBytesInstallAndResolve()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("fake-gitversion-binary");
        string sha = Sha256(bytes);

        StorePopulateResult result = StorePopulator.InstallBytes(Tool, Version, Rid, ExeName, bytes, sha);

        Assert.Equal(StorePopulateStatus.Installed, result.Status);
        Assert.True(File.Exists(ToolStore.PathFor(Tool, Version, Rid, ExeName)));
        Assert.True(ToolStore.IsInstalled(Tool, Version, Rid, ExeName, sha));

        string? resolved = ToolStoreResolver.Resolve(Tool, Version, Rid, ExeName, sha);
        Assert.Equal(ToolStore.PathFor(Tool, Version, Rid, ExeName), resolved);

        ToolStoreEntry entry = Assert.Single(ToolStore.ReadIndex().Entries);
        Assert.Equal(Tool, entry.Tool);
        Assert.Equal(sha, entry.Sha256);
    }

    [Fact]
    public void HashMismatchFailsClosedAndWritesNothing()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("fake-gitversion-binary");
        string wrongSha = Sha256(Encoding.UTF8.GetBytes("a-different-binary"));

        StorePopulateResult result = StorePopulator.InstallBytes(Tool, Version, Rid, ExeName, bytes, wrongSha);

        Assert.Equal(StorePopulateStatus.Failed, result.Status);
        Assert.False(File.Exists(ToolStore.PathFor(Tool, Version, Rid, ExeName)));
        Assert.Null(ToolStoreResolver.Resolve(Tool, Version, Rid, ExeName, wrongSha));
        Assert.Empty(ToolStore.ReadIndex().Entries);
    }

    [Fact]
    public void SecondInstallIsIdempotent()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("fake-gitversion-binary");
        string sha = Sha256(bytes);

        Assert.Equal(StorePopulateStatus.Installed, StorePopulator.InstallBytes(Tool, Version, Rid, ExeName, bytes, sha).Status);
        Assert.Equal(StorePopulateStatus.AlreadyPresent, StorePopulator.InstallBytes(Tool, Version, Rid, ExeName, bytes, sha).Status);

        // Idempotent: still exactly one index entry after the second install.
        Assert.Single(ToolStore.ReadIndex().Entries);
    }

    [Fact]
    public void ResolveReturnsNullWhenAbsentOrUnverified()
    {
        Assert.Null(ToolStoreResolver.Resolve(Tool, Version, Rid, ExeName, Sha256(Encoding.UTF8.GetBytes("anything"))));

        byte[] bytes = Encoding.UTF8.GetBytes("fake-gitversion-binary");
        StorePopulator.InstallBytes(Tool, Version, Rid, ExeName, bytes, Sha256(bytes));

        // Present, but the caller asks with a different expected hash → not resolved (fail closed).
        Assert.Null(ToolStoreResolver.Resolve(Tool, Version, Rid, ExeName, Sha256(Encoding.UTF8.GetBytes("different"))));
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
