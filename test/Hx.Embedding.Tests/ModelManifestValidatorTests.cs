using Hx.Embedding;
using Xunit;

namespace Hx.Embedding.Tests;

/// <summary>
/// BL-4 (FR-039): a model asset is hash-verified against the pinned <c>model.version.json</c> BEFORE the native runtime
/// loads it, so a tampered/poisoned GGUF/ONNX is rejected, never executed. Fail-closed: a missing manifest, a missing
/// entry, or any digest mismatch throws.
/// </summary>
public sealed class ModelManifestValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "hx-model-manifest-" + Guid.NewGuid().ToString("N"));

    public ModelManifestValidatorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string WriteModel(string relativeKey, string content)
    {
        string full = Path.Combine(_root, relativeKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    private void WriteManifest(string relativeKey, string sha256) =>
        File.WriteAllText(Path.Combine(_root, ModelManifestValidator.ManifestFileName),
            $$"""{ "models": { "{{relativeKey}}": "{{sha256}}" } }""");

    [Fact]
    public void Verifies_a_matching_model()
    {
        string full = WriteModel("GGUF/model.gguf", "the real weights");
        WriteManifest("GGUF/model.gguf", ModelManifestValidator.Sha256OfFile(full));

        // No throw == verified.
        ModelManifestValidator.Load(_root).Verify("GGUF/model.gguf", full);
    }

    [Fact]
    public void Rejects_a_tampered_model_before_load()
    {
        string full = WriteModel("GGUF/model.gguf", "the real weights");
        WriteManifest("GGUF/model.gguf", ModelManifestValidator.Sha256OfFile(full));

        // An attacker swaps the file under the pinned digest after provisioning.
        File.WriteAllText(full, "POISONED weights with native RCE payload");

        SemanticException ex = Assert.Throws<SemanticException>(
            () => ModelManifestValidator.Load(_root).Verify("GGUF/model.gguf", full));
        Assert.Contains("tampered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fails_closed_when_the_manifest_is_absent()
    {
        string full = WriteModel("GGUF/model.gguf", "weights");

        SemanticException ex = Assert.Throws<SemanticException>(
            () => ModelManifestValidator.Load(_root).Verify("GGUF/model.gguf", full));
        Assert.Contains(ModelManifestValidator.ManifestFileName, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fails_closed_when_the_manifest_pins_no_entry_for_the_asset()
    {
        string full = WriteModel("GGUF/model.gguf", "weights");
        WriteManifest("GGUF/other.gguf", ModelManifestValidator.Sha256OfFile(full));

        Assert.Throws<SemanticException>(
            () => ModelManifestValidator.Load(_root).Verify("GGUF/model.gguf", full));
    }
}
