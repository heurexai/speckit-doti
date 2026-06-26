using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

/// <summary>
/// Generates the source-free payload descriptor (<c>payload.manifest.json</c>) for a staged payload root (007 T023).
/// Walks every file under the root (except the descriptor itself), records each file's root-relative path + SHA-256,
/// and emits the <see cref="PayloadDescriptor"/> that <see cref="PayloadRoot.Resolve()"/> verifies against at runtime
/// (per-file hash + the executable-anchored manifest digest). Deterministic: entries are ordered by path so the
/// manifest digest is reproducible for the same payload.
/// </summary>
public static class PayloadManifestGenerator
{
    /// <summary>Build the descriptor for the staged payload at <paramref name="stagedRoot"/>.</summary>
    public static PayloadDescriptor Generate(
        string stagedRoot, string payloadVersion, string toolVersion, string channel, string mode)
    {
        string root = Path.GetFullPath(stagedRoot);
        string manifestPath = Path.Combine(root, PayloadRoot.ManifestFileName);

        List<PayloadFileHash> hashes = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFullPath(path), manifestPath, StringComparison.OrdinalIgnoreCase))
            .Select(path => new PayloadFileHash(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                PayloadRoot.Sha256OfFile(path)))
            .OrderBy(hash => hash.RelativePath, StringComparer.Ordinal)
            .ToList();

        return new PayloadDescriptor(
            PayloadDescriptor.CurrentSchemaVersion, payloadVersion, toolVersion, channel, mode, hashes);
    }

    /// <summary>Generate + write <c>payload.manifest.json</c> into the staged root; returns the descriptor.</summary>
    public static PayloadDescriptor Write(
        string stagedRoot, string payloadVersion, string toolVersion, string channel, string mode)
    {
        PayloadDescriptor descriptor = Generate(stagedRoot, payloadVersion, toolVersion, channel, mode);
        string manifestPath = Path.Combine(Path.GetFullPath(stagedRoot), PayloadRoot.ManifestFileName);

        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(descriptor, options));
        return descriptor;
    }
}
