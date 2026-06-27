using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hx.Embedding;

/// <summary>
/// BL-4 (FR-039): hash-verify a model asset against the pinned <c>model.version.json</c> BEFORE it is loaded into the
/// LLamaSharp/ONNX native runtime — so a poisoned GGUF/ONNX dropped into the operator-writable model root is rejected,
/// never executed (a poisoned model is a native memory-safety/RCE surface). Fail-closed, mirroring the other vendored
/// tools (SentruxManifestValidator): a missing manifest, a missing entry, or a digest mismatch all throw.
/// </summary>
public sealed class ModelManifestValidator
{
    public const string ManifestFileName = "model.version.json";

    private readonly IReadOnlyDictionary<string, string>? _digests;

    private ModelManifestValidator(IReadOnlyDictionary<string, string>? digests) => _digests = digests;

    /// <summary>Load the pinned digests from <c>&lt;root&gt;/model.version.json</c> (absent ⇒ no digests; Verify fails closed).</summary>
    public static ModelManifestValidator Load(string root)
    {
        string path = Path.Combine(root, ManifestFileName);
        if (!File.Exists(path))
        {
            return new ModelManifestValidator(null);
        }

        try
        {
            ModelManifest? manifest = JsonSerializer.Deserialize<ModelManifest>(
                File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new ModelManifestValidator(manifest?.Models is { } models
                ? new Dictionary<string, string>(models, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException ex)
        {
            throw new SemanticException($"'{ManifestFileName}' is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>Verify the file at <paramref name="fullPath"/> matches the digest pinned for <paramref name="relativeKey"/>.
    /// Throws (fail-closed) if the manifest is absent, has no entry for the key, or the SHA-256 differs.</summary>
    public void Verify(string relativeKey, string fullPath)
    {
        string key = relativeKey.Replace('\\', '/');
        if (_digests is null)
        {
            throw new SemanticException(
                $"No '{ManifestFileName}' in the model root; cannot hash-verify '{key}' before load (fail-closed, FR-039).");
        }

        if (!_digests.TryGetValue(key, out string? expected) || string.IsNullOrWhiteSpace(expected))
        {
            throw new SemanticException($"'{ManifestFileName}' pins no digest for model asset '{key}' (fail-closed).");
        }

        string actual = Sha256OfFile(fullPath);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new SemanticException(
                $"Model asset '{key}' SHA-256 {actual} does not match the pinned {expected} — refusing to load a tampered model.");
        }
    }

    public static string Sha256OfFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private sealed record ModelManifest([property: JsonPropertyName("models")] IReadOnlyDictionary<string, string>? Models);
}
