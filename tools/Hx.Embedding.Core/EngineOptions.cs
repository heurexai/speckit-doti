namespace Hx.Embedding;

/// <summary>
/// CPU tuning knobs (FR-012). The inference thread count defaults near the host physical-core count. For pooled
/// decoder embeddings the micro-batch MUST hold the whole (truncated) sequence in one pass, so
/// <see cref="BatchSize"/> = <see cref="UBatchSize"/> = <see cref="ContextSize"/> by default; lowering the
/// micro-batch below the sequence length silently breaks last-token pooling.
/// </summary>
public sealed record EngineOptions
{
    /// <summary>Inference threads. Defaults to the host physical-core count (≈ logical / 2 on SMT, floored at 1).</summary>
    public int Threads { get; init; } = DefaultThreads();

    /// <summary>Max context window (tokens). Inputs over <see cref="MaxTokens"/> are truncated to fit.</summary>
    public int ContextSize { get; init; } = 2048;

    /// <summary>llama.cpp logical batch — must be ≥ the longest pooled sequence.</summary>
    public int BatchSize { get; init; } = 2048;

    /// <summary>llama.cpp micro-batch — pooled embeddings need the whole sequence in one ubatch.</summary>
    public int UBatchSize { get; init; } = 2048;

    /// <summary>Hard token cap applied at tokenization (deterministic truncate-to-fit).</summary>
    public int MaxTokens { get; init; } = 2048;

    /// <summary>The shipped defaults (the configuration the determinism contract, FR-010, is scoped to).</summary>
    public static EngineOptions Default { get; } = new();

    /// <summary>Fail-closed enforcement of the tuning invariants (arch-review BLOCKER #2): the micro-batch must
    /// hold the whole truncated sequence, so <see cref="UBatchSize"/> ≥ <see cref="ContextSize"/> ≥
    /// <see cref="MaxTokens"/>. A misconfiguration throws rather than silently producing a wrong pooled vector.</summary>
    public void Validate()
    {
        if (Threads < 1)
        {
            throw new SemanticException($"Threads ({Threads}) must be >= 1.");
        }

        if (ContextSize < 1)
        {
            throw new SemanticException($"ContextSize ({ContextSize}) must be >= 1.");
        }

        if (UBatchSize < ContextSize)
        {
            throw new SemanticException(
                $"UBatchSize ({UBatchSize}) must be >= ContextSize ({ContextSize}) so a pooled sequence fits one micro-batch.");
        }

        if (MaxTokens > UBatchSize)
        {
            throw new SemanticException($"MaxTokens ({MaxTokens}) must be <= UBatchSize ({UBatchSize}).");
        }
    }

    private static int DefaultThreads()
    {
        // ProcessorCount is logical; physical ≈ logical/2 on SMT hosts. Floor at 1. Configurable per FR-012.
        return Math.Max(1, Environment.ProcessorCount / 2);
    }
}
