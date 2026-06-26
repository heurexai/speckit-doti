using System.Text.Json;
using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T009/T010 (FR-003, SC-003, SC-017): source-free payload resolution. <c>PayloadRoot</c> resolves the
/// installed payload from beside the executable (or <c>HX_PAYLOAD_ROOT</c>) via a non-source
/// <c>payload.manifest.json</c>, never the working directory or <c>scaffold-dotnet.slnx</c>; it fails closed on
/// an absent / unparseable / unsupported-schema descriptor, a per-file hash mismatch, and (when the executable
/// carries an anchored digest) a descriptor that does not match it — on every path, including the override.
/// </summary>
public sealed class PayloadRootTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-payload-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string WriteValidPayload(out string manifestDigest, params (string path, string content)[] files)
    {
        string root = NewTempDir();
        var hashes = new List<PayloadFileHash>();
        foreach ((string path, string content) in files)
        {
            string full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            hashes.Add(new PayloadFileHash(path, PayloadRoot.Sha256OfFile(full)));
        }

        var descriptor = new PayloadDescriptor(
            PayloadDescriptor.CurrentSchemaVersion, "8", "0.9.1",
            DistributionChannelId.GlobalTool, CommandMode.Installed, hashes);
        string manifestPath = Path.Combine(root, PayloadRoot.ManifestFileName);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(descriptor, JsonContractSerializerOptions.Create()));
        manifestDigest = PayloadRoot.Sha256OfText(File.ReadAllText(manifestPath));
        return root;
    }

    [Fact]
    public void Resolves_BesideExecutable_WhenManifestPresentAndFilesMatch()
    {
        string root = WriteValidPayload(out _, (".doti/skills.json", "{}"), ("templates/pack.nupkg", "binary"));
        try
        {
            PayloadResolution r = PayloadRoot.Resolve(overrideRoot: null, baseDirectory: root, expectedManifestSha256: null);
            Assert.True(r.Ok);
            Assert.Equal(Path.GetFullPath(root), r.Root);
            Assert.Equal(DistributionChannelId.GlobalTool, r.Descriptor!.Channel);
            Assert.False(r.OverrideActive);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void FailsClosed_RootMissing_WhenManifestAbsent_AndNeverFallsBackToSlnx()
    {
        string dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "scaffold-dotnet.slnx"), "<Solution/>"); // a source marker is NOT a payload
        try
        {
            PayloadResolution r = PayloadRoot.Resolve(overrideRoot: null, baseDirectory: dir, expectedManifestSha256: null);
            Assert.False(r.Ok);
            Assert.Equal(PayloadFailureKind.RootMissing, r.FailureKind);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void FailsClosed_DescriptorInvalid_WhenUnparseable()
    {
        string dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, PayloadRoot.ManifestFileName), "{ not json");
        try
        {
            PayloadResolution r = PayloadRoot.Resolve(null, dir, null);
            Assert.Equal(PayloadFailureKind.DescriptorInvalid, r.FailureKind);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void FailsClosed_DescriptorInvalid_WhenUnsupportedSchemaVersion()
    {
        string dir = NewTempDir();
        var ahead = new PayloadDescriptor(PayloadDescriptor.CurrentSchemaVersion + 99, "8", "0.9.1",
            DistributionChannelId.GlobalTool, CommandMode.Installed, []);
        File.WriteAllText(Path.Combine(dir, PayloadRoot.ManifestFileName),
            JsonSerializer.Serialize(ahead, JsonContractSerializerOptions.Create()));
        try
        {
            PayloadResolution r = PayloadRoot.Resolve(null, dir, null);
            Assert.Equal(PayloadFailureKind.DescriptorInvalid, r.FailureKind);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void FailsClosed_WhenADeclaredPayloadFileIsTampered()
    {
        string root = WriteValidPayload(out _, (".doti/skills.json", "{}"));
        try
        {
            // Tamper a declared payload file after the descriptor recorded its hash.
            File.WriteAllText(Path.Combine(root, ".doti", "skills.json"), "{\"tampered\":true}");
            PayloadResolution r = PayloadRoot.Resolve(null, root, null);
            Assert.Equal(PayloadFailureKind.DescriptorInvalid, r.FailureKind);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Override_TakesPrecedenceOverBaseDirectory_AndIsFlaggedActive()
    {
        string payload = WriteValidPayload(out _, (".doti/skills.json", "{}"));
        string baseDir = NewTempDir(); // no manifest here
        try
        {
            PayloadResolution r = PayloadRoot.Resolve(overrideRoot: payload, baseDirectory: baseDir, expectedManifestSha256: null);
            Assert.True(r.Ok);
            Assert.Equal(Path.GetFullPath(payload), r.Root);
            Assert.True(r.OverrideActive);
        }
        finally { Directory.Delete(payload, recursive: true); Directory.Delete(baseDir, recursive: true); }
    }

    [Fact]
    public void Override_StillVerifiesTheAnchoredDigest_RejectsAnUnanchoredPayload()
    {
        string payload = WriteValidPayload(out string digest, (".doti/skills.json", "{}"));
        try
        {
            // The override points at a self-consistent payload that does NOT match the executable's anchor → reject.
            PayloadResolution bad = PayloadRoot.Resolve(payload, baseDirectory: payload, expectedManifestSha256: "0000deadbeef");
            Assert.Equal(PayloadFailureKind.DescriptorInvalid, bad.FailureKind);

            // The matching anchor digest passes (even via the override).
            PayloadResolution ok = PayloadRoot.Resolve(payload, baseDirectory: payload, expectedManifestSha256: digest);
            Assert.True(ok.Ok);
        }
        finally { Directory.Delete(payload, recursive: true); }
    }
}
