using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T023: the payload-manifest generator emits a <c>payload.manifest.json</c> that <see cref="PayloadRoot.Resolve()"/>
/// accepts as a valid source-free trust root — every staged payload file is recorded + verifiable, the descriptor
/// excludes itself, and a post-generation tamper of any payload file fails resolution closed.
/// </summary>
public sealed class PayloadManifestGeneratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hx-payloadgen-" + Guid.NewGuid().ToString("n"));

    public PayloadManifestGeneratorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Generated_manifest_resolves_against_payload_root()
    {
        Write(".doti/core/skills.json", "{}");
        Write("hx.config.json", "{}");
        Write("templates/Hx.Scaffold.Templates.1.0.0.nupkg", "PK-fake-pack");
        Write("tools/sentrux/sentrux.version.json", "{}");

        PayloadDescriptor descriptor = PayloadManifestGenerator.Write(
            _root, "0.9.1", "0.9.1", DistributionChannelId.GlobalTool, CommandMode.Installed);

        Assert.Equal(PayloadDescriptor.CurrentSchemaVersion, descriptor.SchemaVersion);
        Assert.Equal(DistributionChannelId.GlobalTool, descriptor.Channel);
        Assert.Equal(CommandMode.Installed, descriptor.Mode);
        Assert.Equal(4, descriptor.FileHashes.Count); // every payload file except the manifest itself
        Assert.DoesNotContain(descriptor.FileHashes, h => h.RelativePath == PayloadRoot.ManifestFileName);

        PayloadResolution resolution = PayloadRoot.Resolve(null, _root, null);

        Assert.True(resolution.Ok, resolution.FailureReason);
        Assert.Equal(DistributionChannelId.GlobalTool, resolution.Descriptor!.Channel);
    }

    [Fact]
    public void A_tampered_payload_file_fails_resolution_closed()
    {
        Write("hx.config.json", "{}");
        PayloadManifestGenerator.Write(_root, "0.9.1", "0.9.1", DistributionChannelId.GlobalTool, CommandMode.Installed);

        File.WriteAllText(Path.Combine(_root, "hx.config.json"), "{\"tampered\":true}"); // mutate after the manifest

        PayloadResolution resolution = PayloadRoot.Resolve(null, _root, null);

        Assert.False(resolution.Ok);
        Assert.Equal(PayloadFailureKind.DescriptorInvalid, resolution.FailureKind);
    }

    private void Write(string relative, string content)
    {
        string full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
