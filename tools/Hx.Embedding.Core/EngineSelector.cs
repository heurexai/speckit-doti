namespace Hx.Embedding;

/// <summary>The active engine plus the embedder for it (FR-042). Owns the embedder — dispose it.</summary>
public sealed record EngineSelection(EngineId Active, IEmbedder Embedder) : IDisposable
{
    /// <summary>The stable wire id of the active engine (e.g. <c>qwen3-0.6b</c> / <c>bge-m3</c>) — FR-042.</summary>
    public string ActiveWireId => EngineIds.Wire(Active);

    public void Dispose() => Embedder.Dispose();
}

/// <summary>
/// M-3 (FR-039): select Qwen3-GGUF (the PRIMARY), falling back to BGE-M3-ONNX when Qwen3 is UNAVAILABLE — a missing
/// model OR a construction-time load failure (a corrupt/incompatible GGUF, a missing native backend). Both engines
/// fail lazily in their constructors with <see cref="SemanticException"/>, so the fallback is a try/catch around
/// CONSTRUCTION, not a path probe. The active engine is reported (FR-042). This is NEW logic over the verbatim-ported
/// <see cref="SemanticEngineFactory"/> (which selects an explicit engine and fails closed); the create delegate is
/// injectable so the fallback is unit-testable without provisioning a model.
/// </summary>
public sealed class EngineSelector
{
    private readonly Func<EngineId, EngineOptions?, IEmbedder> _create;

    public EngineSelector(Func<EngineId, EngineOptions?, IEmbedder>? create = null) =>
        _create = create ?? ((id, options) => new SemanticEngineFactory().Create(id, options));

    /// <summary>Try Qwen3; fall back to BGE-M3 on a <see cref="SemanticException"/>; report the active engine.</summary>
    public EngineSelection Select(EngineOptions? options = null)
    {
        try
        {
            return new EngineSelection(EngineId.Qwen3, _create(EngineId.Qwen3, options));
        }
        catch (SemanticException)
        {
            // Qwen3 unavailable (missing/corrupt model or backend) — fall back to BGE-M3 (FR-039). If BGE-M3 is also
            // unavailable its SemanticException propagates (fail-closed: no engine could be loaded).
            return new EngineSelection(EngineId.BgeM3, _create(EngineId.BgeM3, options));
        }
    }
}
