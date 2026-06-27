using Hx.Embedding;
using Xunit;

namespace Hx.Embedding.Tests;

/// <summary>
/// BL-4 / T037: ONNX Runtime loads the BGE-M3 external weight sidecar <c>model.onnx.data</c> at session creation, so a
/// poisoned sidecar would bypass the <c>model.onnx</c> hash. <see cref="ModelLocator.BgeM3Model"/> must hash-verify the
/// sidecar too when it is present, fail-closed.
/// </summary>
public sealed class ModelLocatorSidecarTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "hx-model-locator-" + Guid.NewGuid().ToString("N"));

    public ModelLocatorSidecarTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string Write(string relativeKey, string content)
    {
        string full = Path.Combine(_root, relativeKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    private void WriteManifest(params (string key, string path)[] entries)
    {
        string body = string.Join(",", entries.Select(e =>
            $"\"{e.key}\": \"{ModelManifestValidator.Sha256OfFile(e.path)}\""));
        File.WriteAllText(Path.Combine(_root, ModelManifestValidator.ManifestFileName),
            $"{{ \"models\": {{ {body} }} }}");
    }

    [Fact]
    public void Verifies_the_onnx_data_sidecar_when_present()
    {
        string model = Write("ONNX/bge-m3/model.onnx", "graph");
        string sidecar = Write("ONNX/bge-m3/model.onnx.data", "the real external weights");
        WriteManifest(("ONNX/bge-m3/model.onnx", model), ("ONNX/bge-m3/model.onnx.data", sidecar));

        // No throw == both graph and sidecar verified.
        Assert.Equal(model, new ModelLocator(_root).BgeM3Model);
    }

    [Fact]
    public void Rejects_a_tampered_onnx_data_sidecar_before_load()
    {
        string model = Write("ONNX/bge-m3/model.onnx", "graph");
        string sidecar = Write("ONNX/bge-m3/model.onnx.data", "the real external weights");
        WriteManifest(("ONNX/bge-m3/model.onnx", model), ("ONNX/bge-m3/model.onnx.data", sidecar));

        // The graph hash still matches, but an attacker swaps the external weights under it.
        File.WriteAllText(sidecar, "POISONED external weights");

        Assert.Throws<SemanticException>(() => _ = new ModelLocator(_root).BgeM3Model);
    }

    [Fact]
    public void Fails_closed_when_a_present_sidecar_is_unpinned()
    {
        string model = Write("ONNX/bge-m3/model.onnx", "graph");
        Write("ONNX/bge-m3/model.onnx.data", "external weights with no manifest entry");
        WriteManifest(("ONNX/bge-m3/model.onnx", model)); // sidecar deliberately NOT pinned

        // A present-but-unpinned sidecar must throw, not silently load.
        Assert.Throws<SemanticException>(() => _ = new ModelLocator(_root).BgeM3Model);
    }
}
