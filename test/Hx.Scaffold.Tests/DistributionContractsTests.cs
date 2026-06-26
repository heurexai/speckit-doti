using System.Text.Json;
using System.Text.Json.Nodes;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T004: the distribution contracts are additive and channel-neutral. A pre-007 <c>release.identity.json</c>
/// (no <c>packageId</c>/<c>channel</c>/<c>channelInstallProofs</c> keys) must still deserialize, and the kept
/// <c>Velopack*</c> fields must be untouched — no <c>JsonContractDefaults.SchemaVersion</c> bump. The two payload
/// descriptors are distinct records with their own schema versions.
/// </summary>
public sealed class DistributionContractsTests
{
    private static LocalReleaseResult MinimalResult(
        string? packageId = null,
        string? channel = null,
        IReadOnlyList<ChannelInstallProof>? channelInstallProofs = null) =>
        new(
            JsonContractDefaults.SchemaVersion,
            "speckit-doti",
            "1.2.3",
            "minor",
            new LocalReleaseTag("v1.2.3", "abc123", "tag-object", Created: true, Existing: false, "tag body", "git push origin v1.2.3"),
            "gitversion + v1.2.3",
            "speckit-doti",
            "win",
            "win-x64",
            "abc123",
            new LocalReleaseTarget("Doti", "speckit-doti", "tools/Hx.Scaffold.Cli", "hx", "hx", "DOTI_RELEASE_ROOT"),
            new LocalReleaseRootDecision("DOTI_RELEASE_ROOT", null, EnvironmentVariableRead: false, EnvironmentVariableIgnored: false, "explicit", @"D:\releases", null),
            new LocalReleaseEnvironmentPersistence(Requested: false, null, null, Written: false, null, null),
            LocalCopyProduced: true,
            SkippedReason: null,
            VersionDirectory: null,
            LatestDirectory: null,
            Artifacts: [],
            VelopackArtifacts: [],
            PayloadChecks: [],
            ReleaseTrain: null,
            DocumentationProof: null,
            CommandName: "release",
            CommandVersion: "0.9.1",
            ConfigurationSource: "hx.config.json",
            ConfigurationPath: "hx.config.json",
            ReleaseProduct: "velopack",
            SourceArchiveExcluded: true,
            Blockers: [],
            InstallLocationProof: null,
            PackageId: packageId,
            Channel: channel,
            ChannelInstallProofs: channelInstallProofs);

    [Fact]
    public void LocalReleaseResult_NewChannelNeutralFields_SerializeAndRoundTrip()
    {
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        LocalReleaseResult result = MinimalResult(
            packageId: "Heurex.SpeckitDoti",
            channel: DistributionChannelId.GlobalTool,
            channelInstallProofs: [new ChannelInstallProof(DistributionChannelId.GlobalTool, "pass", "hx version --json", ["hx version --json"], [])]);

        string json = JsonSerializer.Serialize(result, options);
        Assert.Contains("Heurex.SpeckitDoti", json);
        Assert.Contains(DistributionChannelId.GlobalTool, json);

        LocalReleaseResult round = JsonSerializer.Deserialize<LocalReleaseResult>(json, options)!;
        Assert.Equal("Heurex.SpeckitDoti", round.PackageId);
        Assert.Equal(DistributionChannelId.GlobalTool, round.Channel);
        Assert.Single(round.ChannelInstallProofs!);
        Assert.Equal("speckit-doti", round.VelopackPackageId); // kept field untouched (additive change)
    }

    [Fact]
    public void LocalReleaseResult_Pre007Json_WithoutNewKeys_DeserializesWithNulls()
    {
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        // Simulate a pre-007 release.identity.json: serialize, then REMOVE the new keys entirely.
        string json = JsonSerializer.Serialize(MinimalResult(), options);
        JsonObject node = JsonNode.Parse(json)!.AsObject();
        node.Remove("packageId");
        node.Remove("channel");
        node.Remove("channelInstallProofs");
        string oldJson = node.ToJsonString();
        Assert.DoesNotContain("\"packageId\"", oldJson);

        LocalReleaseResult old = JsonSerializer.Deserialize<LocalReleaseResult>(oldJson, options)!;
        Assert.Null(old.PackageId);
        Assert.Null(old.Channel);
        Assert.Null(old.ChannelInstallProofs);
        Assert.Equal("speckit-doti", old.VelopackPackageId); // the pre-007 shape still reads
    }

    [Fact]
    public void PayloadDescriptor_And_RepoPayloadStamp_AreDistinctRecords_WithOwnSchemaVersions()
    {
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        var descriptor = new PayloadDescriptor(
            PayloadDescriptor.CurrentSchemaVersion, "8", "0.9.1", DistributionChannelId.GlobalTool, CommandMode.Installed,
            [new PayloadFileHash("payload.manifest.json", "deadbeef")]);
        var stamp = new RepoPayloadStamp(RepoPayloadStamp.CurrentSchemaVersion, "8", "0.9.1");

        Assert.Equal(descriptor.PayloadVersion, stamp.PayloadVersion); // repo stamp copies the descriptor's version
        Assert.Equal(1, PayloadDescriptor.CurrentSchemaVersion);
        Assert.Equal(1, RepoPayloadStamp.CurrentSchemaVersion);

        PayloadDescriptor roundDescriptor = JsonSerializer.Deserialize<PayloadDescriptor>(
            JsonSerializer.Serialize(descriptor, options), options)!;
        Assert.Single(roundDescriptor.FileHashes);
        Assert.Equal(CommandMode.Installed, roundDescriptor.Mode);
    }
}
