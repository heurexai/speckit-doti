using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>022 T011 (FR-001): the extracted <see cref="RepoPayloadStore"/> round-trips .doti/payload.json and is
/// fail-soft on an absent/malformed file.</summary>
public sealed class RepoPayloadStoreTests
{
    [Fact]
    public void Write_then_read_round_trips_payload_and_tool_version()
    {
        string repo = DotiVersionTestSupport.NewTempDir();
        try
        {
            RepoPayloadStore.Write(repo, "0.13.5", "0.13.5");

            RepoPayloadStamp? stamp = RepoPayloadStore.Read(repo);
            Assert.NotNull(stamp);
            Assert.Equal("0.13.5", stamp!.PayloadVersion);
            Assert.Equal("0.13.5", stamp.ToolVersion);
            Assert.Equal("0.13.5", RepoPayloadStore.ReadPayloadVersion(repo));
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(repo);
        }
    }

    [Fact]
    public void Missing_file_reads_as_null()
    {
        string repo = DotiVersionTestSupport.NewTempDir();
        try
        {
            Assert.Null(RepoPayloadStore.Read(repo));
            Assert.Null(RepoPayloadStore.ReadPayloadVersion(repo));
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(repo);
        }
    }

    [Fact]
    public void Malformed_file_reads_as_null_not_throw()
    {
        string repo = DotiVersionTestSupport.NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(repo, ".doti"));
            File.WriteAllText(Path.Combine(repo, ".doti", "payload.json"), "{ not json ");

            Assert.Null(RepoPayloadStore.Read(repo));
            Assert.Null(RepoPayloadStore.ReadPayloadVersion(repo));
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(repo);
        }
    }
}
