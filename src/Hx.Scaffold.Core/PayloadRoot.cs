using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

/// <summary>Why a <see cref="PayloadResolution"/> failed. The CLI maps these to the registry error codes
/// (<c>Integrity_PayloadRootMissing</c> / <c>Integrity_PayloadDescriptorInvalid</c>) so Hx.Scaffold.Core need
/// not depend on the kernel's generated codes.</summary>
public enum PayloadFailureKind
{
    None,
    RootMissing,
    DescriptorInvalid,
}

/// <summary>The outcome of resolving the installed payload (FR-003). On success carries the payload
/// <see cref="Root"/> and verified <see cref="Descriptor"/>; on failure a fail-closed <see cref="FailureKind"/>
/// + reason. <see cref="OverrideActive"/> records whether <c>HX_PAYLOAD_ROOT</c> was used (the CLI surfaces a
/// diagnostic when it is — SC-017).</summary>
public sealed record PayloadResolution(
    bool Ok,
    string? Root,
    PayloadDescriptor? Descriptor,
    PayloadFailureKind FailureKind,
    string? FailureReason,
    bool OverrideActive)
{
    public static PayloadResolution Success(string root, PayloadDescriptor descriptor, bool overrideActive) =>
        new(true, root, descriptor, PayloadFailureKind.None, null, overrideActive);

    public static PayloadResolution Fail(PayloadFailureKind kind, string reason, bool overrideActive) =>
        new(false, null, null, kind, reason, overrideActive);
}

/// <summary>
/// The concrete trust root (FR-003): the expected <c>payload.manifest.json</c> SHA-256 the installed executable
/// was packed with. Null in a source/dev build (no packed payload); the global-tool/MSIX pack step (T023) emits
/// the per-version digest here so <c>HX_PAYLOAD_ROOT</c> cannot redirect resolution to a payload that does not
/// match this executable. When non-null, <see cref="PayloadRoot.Resolve()"/> verifies the resolved manifest's
/// digest against it on every path.
/// </summary>
public static class PayloadTrustAnchor
{
    /// <summary>The <c>[AssemblyMetadata]</c> key the two-phase pack (Hx.Scaffold.Cli.csproj) embeds the
    /// per-version payload-manifest digest under.</summary>
    public const string MetadataKey = "HxPayloadManifestSha256";

    /// <summary>The expected <c>payload.manifest.json</c> digest this executable was packed with, read from its
    /// own embedded <c>[AssemblyMetadata("HxPayloadManifestSha256")]</c>. Non-null only in a packed global-tool /
    /// MSIX build (the pack step embeds it); null in a source/dev build and in non-entry hosts (e.g. unit tests),
    /// where there is no packed payload to anchor.</summary>
    public static string? ExpectedManifestSha256 { get; } = ReadEmbeddedAnchor();

    private static string? ReadEmbeddedAnchor()
    {
        // The anchor binds the payload to THIS executable, so read the entry assembly (hx), not this library.
        Assembly? entry = Assembly.GetEntryAssembly();
        if (entry is null)
        {
            return null;
        }

        foreach (AssemblyMetadataAttribute attribute in entry.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.Equals(attribute.Key, MetadataKey, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(attribute.Value))
            {
                return attribute.Value;
            }
        }

        return null;
    }
}

/// <summary>
/// Source-free payload resolution (FR-001/FR-003). Installed <c>hx</c> commands resolve templates, prerequisite
/// policy, <c>.doti</c> assets, and vendored tools from the payload shipped beside the executable — identified by
/// a non-source <c>payload.manifest.json</c> (never <c>scaffold-dotnet.slnx</c>), and integrity-verified against
/// the executable-anchored digest + the descriptor's per-file hashes. Precedence: <c>HX_PAYLOAD_ROOT</c> →
/// <c>AppContext.BaseDirectory</c>; the current working directory and source markers are never consulted.
/// </summary>
public static class PayloadRoot
{
    public const string OverrideEnvVar = "HX_PAYLOAD_ROOT";
    public const string ManifestFileName = "payload.manifest.json";

    /// <summary>Resolve the installed payload from beside the executable (or <c>HX_PAYLOAD_ROOT</c>), verified
    /// against the executable's anchored digest.</summary>
    public static PayloadResolution Resolve() =>
        Resolve(Environment.GetEnvironmentVariable(OverrideEnvVar), AppContext.BaseDirectory, PayloadTrustAnchor.ExpectedManifestSha256);

    /// <summary>Resolution core (explicit inputs for testability).</summary>
    public static PayloadResolution Resolve(string? overrideRoot, string baseDirectory, string? expectedManifestSha256)
    {
        bool overrideActive = !string.IsNullOrWhiteSpace(overrideRoot);
        string root = Path.GetFullPath(overrideActive ? overrideRoot! : baseDirectory);
        string manifestPath = Path.Combine(root, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return PayloadResolution.Fail(PayloadFailureKind.RootMissing,
                $"payload descriptor '{ManifestFileName}' not found beside the executable at '{root}'", overrideActive);
        }

        string manifestText = File.ReadAllText(manifestPath);
        PayloadDescriptor? descriptor;
        try
        {
            descriptor = JsonSerializer.Deserialize<PayloadDescriptor>(manifestText, JsonContractSerializerOptions.Create());
        }
        catch (JsonException ex)
        {
            return PayloadResolution.Fail(PayloadFailureKind.DescriptorInvalid,
                $"payload descriptor is unparseable: {ex.Message}", overrideActive);
        }

        if (descriptor is null)
        {
            return PayloadResolution.Fail(PayloadFailureKind.DescriptorInvalid, "payload descriptor is empty", overrideActive);
        }

        if (descriptor.SchemaVersion != PayloadDescriptor.CurrentSchemaVersion)
        {
            return PayloadResolution.Fail(PayloadFailureKind.DescriptorInvalid,
                $"payload descriptor schemaVersion {descriptor.SchemaVersion} is unsupported (expected {PayloadDescriptor.CurrentSchemaVersion})", overrideActive);
        }

        // Anti-substitution anchor: when the executable carries an expected manifest digest, the resolved
        // descriptor MUST match it — on EVERY path, including HX_PAYLOAD_ROOT — so the override cannot point at
        // a self-consistent but unanchored payload.
        if (!string.IsNullOrWhiteSpace(expectedManifestSha256)
            && !string.Equals(Sha256OfText(manifestText), expectedManifestSha256, StringComparison.OrdinalIgnoreCase))
        {
            return PayloadResolution.Fail(PayloadFailureKind.DescriptorInvalid,
                "payload descriptor digest does not match the installed executable's anchored digest", overrideActive);
        }

        // Per-file integrity: every declared payload file must exist and match its recorded hash.
        foreach (PayloadFileHash entry in descriptor.FileHashes)
        {
            string filePath = Path.Combine(root, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
            {
                return PayloadResolution.Fail(PayloadFailureKind.RootMissing,
                    $"declared payload file is missing: {entry.RelativePath}", overrideActive);
            }

            if (!string.Equals(Sha256OfFile(filePath), entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return PayloadResolution.Fail(PayloadFailureKind.DescriptorInvalid,
                    $"declared payload file hash mismatch (tampered/incomplete payload): {entry.RelativePath}", overrideActive);
            }
        }

        return PayloadResolution.Success(root, descriptor, overrideActive);
    }

    public static string Sha256OfText(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static string Sha256OfFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
