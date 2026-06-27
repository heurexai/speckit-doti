using Hx.Embedding;
using Hx.Embedding.Timing;
using Xunit;

namespace Hx.Embedding.Tests;

/// <summary>
/// M-3 (FR-039/FR-042/SC-022): the engine selector tries Qwen3 first and falls back to BGE-M3 when Qwen3's
/// constructor fails (missing/corrupt model or backend), reporting the ACTIVE engine. Driven by an injected create
/// delegate, so the fallback is proven without provisioning a real GGUF/ONNX model.
/// </summary>
public sealed class EngineSelectorTests
{
    /// <summary>A no-op embedder standing in for a successfully constructed engine.</summary>
    private sealed class StubEmbedder(string id) : IEmbedder
    {
        public string Id { get; } = id;
        public int Dimension => 8;
        public EngineTiming Timing => new(Id, 0, 0, 0);
        public float[] Embed(string text, EmbedTask task) => new float[8];
        public void Dispose() { }
    }

    [Fact]
    public void Selects_qwen3_when_it_constructs()
    {
        var selector = new EngineSelector((id, _) => new StubEmbedder(EngineIds.Wire(id)));

        using EngineSelection selection = selector.Select();

        Assert.Equal(EngineId.Qwen3, selection.Active);
        Assert.Equal(EngineIds.Qwen3, selection.ActiveWireId);
    }

    [Fact]
    public void Falls_back_to_bge_m3_when_qwen3_construction_fails()
    {
        // Qwen3 unavailable (a missing model / incompatible backend surfaces as SemanticException at construction);
        // BGE-M3 then constructs. The selector must report BGE-M3 as the active engine (FR-042).
        var selector = new EngineSelector((id, _) => id == EngineId.Qwen3
            ? throw new SemanticException("Qwen3 GGUF not provisioned.")
            : new StubEmbedder(EngineIds.Wire(id)));

        using EngineSelection selection = selector.Select();

        Assert.Equal(EngineId.BgeM3, selection.Active);
        Assert.Equal(EngineIds.BgeM3, selection.ActiveWireId);
    }

    [Fact]
    public void Propagates_when_no_engine_can_be_constructed()
    {
        // Both engines unavailable — fail-closed: the SemanticException propagates so the caller surfaces an advisory
        // SKIP (never a clean-bill, never a gate failure).
        var selector = new EngineSelector((_, _) => throw new SemanticException("No model root provisioned."));

        Assert.Throws<SemanticException>(() => selector.Select());
    }
}
