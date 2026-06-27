using Hx.Embedding.Engines;

namespace Hx.Embedding;

/// <summary>
/// Builds an <see cref="IEmbedder"/> for an <see cref="EngineId"/>, resolving the model + tokenizer from
/// <c>HEUREX_LLM_ROOT</c> via <see cref="ModelLocator"/>, <b>fail-closed</b> (a missing root/model throws
/// <see cref="SemanticException"/>). The engine constructors release any partially-acquired native handle if a
/// later acquisition fails, so a partial provisioning never leaks (FR-008).
/// </summary>
public sealed class SemanticEngineFactory
{
    private readonly ModelLocator _locator;

    public SemanticEngineFactory(ModelLocator? locator = null) => _locator = locator ?? new ModelLocator();

    /// <summary>Create and load the engine. Caller owns the returned instance (single-threaded-use) — dispose it.</summary>
    public IEmbedder Create(EngineId engine, EngineOptions? options = null)
    {
        EngineOptions opts = options ?? EngineOptions.Default;
        opts.Validate(); // fail-closed on a tuning misconfiguration before loading the model
        return engine switch
        {
            EngineId.BgeM3 => new BgeM3Embedder(_locator.BgeM3Model, _locator.BgeM3Tokenizer, opts),
            EngineId.Qwen3 => new Qwen3Embedder(_locator.Qwen3Model, opts),
            _ => throw new SemanticException($"Unknown engine '{engine}'."),
        };
    }
}
